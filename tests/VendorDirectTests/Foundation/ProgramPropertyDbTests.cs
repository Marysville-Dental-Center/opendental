// Integration tests for the programproperty helpers against a scratch MariaDB database on this computer. They are skipped (inconclusive) when no
// server answers at 127.0.0.1:3306 as root with a blank password, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase.VendorDirect;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.Foundation {
	[TestClass]
	public class ProgramPropertyDbTests {
		private static readonly string DB_NAME=ScratchDb.Name("vendordirect_f0_tests");
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("VENDORDIRECT_TEST_DB")=="skip") {
				_skipReason="VENDORDIRECT_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3")) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS "+DB_NAME);
					Exec(connection,"CREATE DATABASE "+DB_NAME+" CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
					Exec(connection,"USE "+DB_NAME);
					//The same definitions as Open Dental 24.3's program, programproperty and preference tables.
					Exec(connection,"CREATE TABLE program (ProgramNum bigint(20) NOT NULL AUTO_INCREMENT,ProgName varchar(100) DEFAULT '',"
						+"ProgDesc varchar(100) DEFAULT '',Enabled tinyint(1) unsigned NOT NULL DEFAULT 0,Path text NOT NULL DEFAULT '',"
						+"CommandLine text NOT NULL DEFAULT '',Note text DEFAULT NULL,PluginDllName varchar(255) NOT NULL,ButtonImage text NOT NULL,"
						+"FileTemplate text NOT NULL,FilePath varchar(255) NOT NULL,IsDisabledByHq tinyint(4) NOT NULL,CustErr varchar(255) NOT NULL,"
						+"PRIMARY KEY (ProgramNum)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE programproperty (ProgramPropertyNum bigint(20) NOT NULL AUTO_INCREMENT,ProgramNum bigint(20) NOT NULL,"
						+"PropertyDesc varchar(255) DEFAULT '',PropertyValue text DEFAULT NULL,ComputerName varchar(255) NOT NULL,ClinicNum bigint(20) NOT NULL,"
						+"IsMasked tinyint(4) NOT NULL,IsHighSecurity tinyint(4) NOT NULL,PRIMARY KEY (ProgramPropertyNum),KEY ClinicNum (ClinicNum)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE preference (PrefName varchar(255) NOT NULL DEFAULT '',ValueString text NOT NULL,"
						+"PrefNum bigint(20) NOT NULL AUTO_INCREMENT,Comments text DEFAULT NULL,PRIMARY KEY (PrefNum),KEY PrefName (PrefName)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
				}
				new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
				Prefs.RefreshCache();
			}
			catch(Exception ex) {
				_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection("Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3")) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS "+DB_NAME);
				}
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless; the next run drops it.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static long EnsureProgram(ProgramName programName) {
			Programs.InsertIfMissing(programName,programName.ToString());
			Programs.RefreshCache();
			return Programs.GetProgramNum(programName);
		}

		[TestMethod]
		public void Programs_InsertIfMissing_InsertsDisabledOnce() {
			Assert.IsTrue(Programs.InsertIfMissing(ProgramName.USPSAddresses,"USPS address validation"));
			Assert.IsFalse(Programs.InsertIfMissing(ProgramName.USPSAddresses,"USPS address validation"));
			Programs.RefreshCache();
			Program program=Programs.GetCur(ProgramName.USPSAddresses);
			Assert.IsNotNull(program);
			Assert.IsFalse(program.Enabled,"New vendor program links start disabled (rule 6).");
			Assert.AreEqual("USPS address validation",program.ProgDesc);
			Assert.AreEqual(1,Programs.GetWhere(x => x.ProgName==ProgramName.USPSAddresses.ToString()).Count);
		}

		[TestMethod]
		public void InsertIfMissing_IsIdempotentAndOnlyRaisesFlags() {
			long programNum=EnsureProgram(ProgramName.GmailApi);
			ProgramProperty programProperty=ProgramProperties.InsertIfMissing(programNum,"Client ID",0,"default",false,false);
			Assert.AreEqual("default",programProperty.PropertyValue);
			ProgramProperty programPropertyAgain=ProgramProperties.InsertIfMissing(programNum,"Client ID",0,"other",false,false);
			Assert.AreEqual(programProperty.ProgramPropertyNum,programPropertyAgain.ProgramPropertyNum);
			Assert.AreEqual("default",programPropertyAgain.PropertyValue,"An existing value is never overwritten.");
			ProgramProperty programPropertySecret=ProgramProperties.InsertIfMissing(programNum,"Client ID",0,"",true,true);
			Assert.IsTrue(programPropertySecret.IsMasked);
			Assert.IsTrue(programPropertySecret.IsHighSecurity);
			ProgramProperty programPropertyLowered=ProgramProperties.InsertIfMissing(programNum,"Client ID",0,"",false,false);
			Assert.IsTrue(programPropertyLowered.IsMasked,"Flags are never lowered.");
			Assert.IsTrue(ProgramProperties.GetOneFromDb(programNum,"Client ID",0).IsHighSecurity);
			ProgramProperty programPropertyClinic=ProgramProperties.InsertIfMissing(programNum,"Client ID",7,"clinic",false,false);
			Assert.AreNotEqual(programProperty.ProgramPropertyNum,programPropertyClinic.ProgramPropertyNum);
		}

		[TestMethod]
		public void GetValOrDefault_ClinicThenPracticeThenBlank() {
			long programNum=EnsureProgram(ProgramName.PostcardMania);
			ProgramProperties.InsertIfMissing(programNum,"Environment",0,"Sandbox",false,false);
			ProgramProperties.InsertIfMissing(programNum,"Environment",3,"Production",false,false);
			ProgramProperties.RefreshCache();
			Assert.AreEqual("Production",ProgramProperties.GetValOrDefault(programNum,"Environment",3));
			Assert.AreEqual("Sandbox",ProgramProperties.GetValOrDefault(programNum,"Environment",4));
			Assert.AreEqual("",ProgramProperties.GetValOrDefault(programNum,"No Such Row",3));
			Assert.AreEqual("",ProgramProperties.GetValOrDefault(-1,"Environment",0));
		}

		[TestMethod]
		public void UpdateValueIfUnchanged_IsAnExactCompareAndSwap() {
			long programNum=EnsureProgram(ProgramName.Microsoft365Email);
			ProgramProperty programProperty=ProgramProperties.InsertIfMissing(programNum,"CasTest",0,"abc",false,false);
			Assert.IsFalse(ProgramProperties.UpdateValueIfUnchanged(programProperty.ProgramPropertyNum,"ABC","new"),"Letter case matters.");
			Assert.IsTrue(ProgramProperties.UpdateValueIfUnchanged(programProperty.ProgramPropertyNum,"abc","new"));
			Assert.IsFalse(ProgramProperties.UpdateValueIfUnchanged(programProperty.ProgramPropertyNum,"abc","newer"),"Stale old value.");
			Assert.AreEqual("new",ProgramProperties.GetOneFromDb(programNum,"CasTest",0).PropertyValue);
			Assert.IsTrue(ProgramProperties.UpdateValueIfUnchanged(programProperty.ProgramPropertyNum,"new","new"),"Same value: true if it is stored.");
			Assert.IsFalse(ProgramProperties.UpdateValueIfUnchanged(programProperty.ProgramPropertyNum,"other","other"));
			string longValue=new string('x',20000)+"'\\\"";
			Assert.IsTrue(ProgramProperties.UpdateValueIfUnchanged(programProperty.ProgramPropertyNum,"new",longValue));
			Assert.IsTrue(ProgramProperties.UpdateValueIfUnchanged(programProperty.ProgramPropertyNum,longValue,""));
			Assert.IsFalse(ProgramProperties.UpdateValueIfUnchanged(-5,"",""));
		}

		[TestMethod]
		public void RefreshTokenStore_ReadsDbAndSwapsCiphertexts() {
			long programNum=EnsureProgram(ProgramName.GmailApi);
			VendorCredentials.EnsureSecret(programNum,"Refresh Token",0);
			ProgramPropertyRefreshTokenStore store=new ProgramPropertyRefreshTokenStore(programNum,"Refresh Token");
			Assert.AreEqual("",store.ReadCurrent());
			Assert.IsTrue(VendorCredentials.WriteSecret(programNum,"Refresh Token",0,"rt1"));
			ProgramProperty programProperty=ProgramProperties.GetOneFromDb(programNum,"Refresh Token",0);
			SecretsTestHelper.AssertRoundTrip("rt1",programProperty.PropertyValue);
			Assert.IsTrue(programProperty.IsMasked && programProperty.IsHighSecurity);
			Assert.AreEqual("rt1",store.ReadCurrent());
			Assert.IsFalse(store.TryReplace("stale","rt2"));
			Assert.IsTrue(store.TryReplace("rt1","rt2"));
			Assert.AreEqual("rt2",store.ReadCurrent());
			//A token stored as plain text under Open Dental is never sent.
			ProgramProperties.UpdateValueIfUnchanged(programProperty.ProgramPropertyNum,ProgramProperties.GetOneFromDb(programNum,"Refresh Token",0).PropertyValue,
				"legacy-plain-token");
			Assert.AreEqual("",store.ReadCurrent());
		}

		[TestMethod]
		public void InsertForClinic_CopiesFlagsAndSkipsPracticeWideRows() {
			long programNum=EnsureProgram(ProgramName.PayConnect);
			ProgramProperties.InsertIfMissing(programNum,"API Key",0,SecretsTestHelper.Protect("integrator"),true,true);
			ProgramProperties.InsertIfMissing(programNum,"API Secret",0,SecretsTestHelper.Protect("merchant"),true,true);
			ProgramProperties.InsertIfMissing(programNum,"PaymentType",0,"12",false,false);
			ProgramProperties.InsertIfMissing(programNum,VendorJobLease.PropertyDescPrefix+"Recon",0,"",false,false);
			Assert.IsTrue(ProgramProperties.InsertForClinic(programNum,new List<long> { 21 }));
			Assert.IsNull(ProgramProperties.GetOneFromDb(programNum,"API Key",21),"The integrator key is practice-wide.");
			Assert.IsNull(ProgramProperties.GetOneFromDb(programNum,VendorJobLease.PropertyDescPrefix+"Recon",21));
			ProgramProperty programPropertySecret=ProgramProperties.GetOneFromDb(programNum,"API Secret",21);
			Assert.IsNotNull(programPropertySecret);
			Assert.IsTrue(programPropertySecret.IsMasked,"A copied secret stays masked.");
			Assert.IsTrue(programPropertySecret.IsHighSecurity);
			Assert.IsFalse(ProgramProperties.GetOneFromDb(programNum,"PaymentType",21).IsMasked);
		}

		[TestMethod]
		public void VendorCredentials_WriteSecretAndSetting() {
			long programNum=EnsureProgram(ProgramName.USPSAddresses);
			Assert.IsTrue(VendorCredentials.WriteSecret(programNum,"Consumer Secret",0,"s3cret"));
			VendorCredentials.WriteSetting(programNum,"Consumer Key",0,"key-1");
			VendorCredentials.WriteSetting(programNum,"Consumer Key",0,"key-2");
			ProgramProperties.RefreshCache();
			SecretReadResult result=VendorCredentials.ReadSecret(programNum,"Consumer Secret",5,LegacyPolicy.RejectPlaintext);
			Assert.AreEqual(SecretReadStatus.Ok,result.Status);
			Assert.AreEqual("s3cret",result.PlainText);
			Assert.AreEqual("key-2",ProgramProperties.GetValOrDefault(programNum,"Consumer Key",0));
			Assert.IsFalse(ProgramProperties.GetOneFromDb(programNum,"Consumer Key",0).IsMasked);
			string fingerprint1=VendorCredentials.GetFingerprint(programNum,0,"Consumer Key","Consumer Secret");
			VendorCredentials.WriteSetting(programNum,"Consumer Key",0,"key-3");
			ProgramProperties.RefreshCache();
			Assert.AreNotEqual(fingerprint1,VendorCredentials.GetFingerprint(programNum,0,"Consumer Key","Consumer Secret"));
		}

		[TestMethod]
		public void VendorJobLease_OneHolderAtATime() {
			long programNum=EnsureProgram(ProgramName.PaySimple);
			Programs.RefreshCache();
			VendorJobLease lease=new VendorJobLease();
			LeaseJob job=new LeaseJob();
			Assert.IsTrue(lease.TryAcquire(job,TimeSpan.FromMinutes(10)));
			Assert.IsTrue(lease.TryAcquire(job,TimeSpan.FromMinutes(10)),"The holder renews.");
			ProgramProperty programProperty=ProgramProperties.GetOneFromDb(programNum,VendorJobLease.PropertyDescPrefix+job.Name,0);
			StringAssert.StartsWith(programProperty.PropertyValue,VendorJobLease.CurrentOwner+"|");
			//Another session on this same computer (a terminal server) takes over the row with an unexpired lease.
			VendorJobLeaseValue valueOther=new VendorJobLeaseValue { Owner=CodeBase.ODEnvironment.MachineName+"/1/other-session",ExpiresUtc=DateTime.UtcNow.AddHours(1) };
			Assert.IsTrue(ProgramProperties.UpdateValueIfUnchanged(programProperty.ProgramPropertyNum,programProperty.PropertyValue,valueOther.ToString()));
			Assert.IsFalse(lease.TryAcquire(job,TimeSpan.FromMinutes(10)),"Another process on the same computer holds the lease.");
			ProgramProperties.RefreshCache();
			Program program=Programs.GetProgram(programNum);
			Assert.IsFalse(ProgramProperties.FilterProperties(program,ProgramProperties.GetForProgram(programNum)).Any(x => VendorJobLease.IsLeaseProperty(x.PropertyDesc)),
				"Lease rows are hidden from the property grid.");
		}

		private sealed class LeaseJob : IVendorBackgroundJob {
			public string Name { get { return "DbLeaseTest"; } }
			public ProgramName ProgramName { get { return ProgramName.PaySimple; } }
			public TimeSpan Interval { get { return TimeSpan.FromMinutes(10); } }
			public bool ShouldRun() { return true; }
			public void Run() { }
		}
	}
}
