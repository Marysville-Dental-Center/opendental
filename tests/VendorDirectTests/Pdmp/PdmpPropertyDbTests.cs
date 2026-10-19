// Tests for the PDMP package against a scratch MariaDB database on this computer: runtime rows, per-clinic reads, legacy (Open Dental) values,
// validation messages and the setup check. Skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password, or when
// VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.Pdmp {
	[TestClass]
	public class PdmpPropertyDbTests {
		private const string DB_NAME="vendordirect_pdmp_tests";
		private const string CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=20";
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("VENDORDIRECT_TEST_DB")=="skip") {
				_skipReason="VENDORDIRECT_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS "+DB_NAME);
					Exec(connection,"CREATE DATABASE "+DB_NAME+" CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
					Exec(connection,"USE "+DB_NAME);
					//The same definitions as Open Dental 24.3's program, programproperty, preference and signalod tables.
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
					Exec(connection,"CREATE TABLE signalod (SignalNum bigint(20) NOT NULL AUTO_INCREMENT,DateViewing date NOT NULL DEFAULT '0001-01-01',"
						+"SigDateTime datetime NOT NULL DEFAULT '0001-01-01 00:00:00',FKey bigint(20) NOT NULL,FKeyType varchar(255) NOT NULL,"
						+"IType tinyint(4) NOT NULL,RemoteRole tinyint(4) NOT NULL,MsgValue text NOT NULL,PRIMARY KEY (SignalNum)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0'),('PracticeAddress','1 Practice St'),"
						+"('PracticeCity','Practiceton'),('PracticeST','KS'),('PracticeZip','67100')");
				}
				new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
				Prefs.RefreshCache();
				Programs.InsertIfMissing(ProgramName.Bamboo,"Bamboo");
				Programs.InsertIfMissing(ProgramName.PDMP,"PDMP");
				Programs.RefreshCache();
				ProgramProperties.RefreshCache();
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
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
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
			PdmpTestData.UseEnUs();
			//Other test classes may have pointed the connection at their own scratch database.
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
				connection.Open();
				Exec(connection,"DELETE FROM "+DB_NAME+".programproperty");
			}
			Prefs.RefreshCache();
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static Program Bamboo {
			get { return Programs.GetCur(ProgramName.Bamboo); }
		}

		private static void SetRaw(long programNum,string desc,long clinicNum,string value) {
			ProgramProperty programProperty=ProgramProperties.InsertIfMissing(programNum,desc,clinicNum,"",false,false);
			ProgramProperty programPropertyOld=programProperty.Copy();
			programProperty.PropertyValue=value;
			ProgramProperties.Update(programProperty,programPropertyOld);
		}

		[TestMethod]
		public void EnsureProperties_InsertsMissingRowsOnce() {
			Program program=Bamboo;
			SetRaw(program.ProgramNum,PdmpProperty.BambooUrl,0,PdmpTestData.GatewayUrl);
			ProgramProperties.RefreshCache();
			Assert.IsTrue(PdmpProperty.EnsureProperties(program));
			List<ProgramProperty> listRows=ProgramProperties.GetWhere(x => x.ProgramNum==program.ProgramNum && x.ClinicNum==0);
			foreach(Tuple<string,bool,string> row in PdmpProperty.GetRowDefinitions(program.ProgName)) {
				ProgramProperty programProperty=listRows.Single(x => x.PropertyDesc==row.Item1);
				Assert.AreEqual(row.Item2,programProperty.IsMasked,row.Item1);
				Assert.AreEqual(row.Item2,programProperty.IsHighSecurity,row.Item1);
			}
			Assert.AreEqual(PdmpTestData.GatewayUrl,ProgramProperties.GetValOrDefault(program.ProgramNum,PdmpProperty.BambooUrl,0),"Existing values kept.");
			Assert.AreEqual(nameof(ProviderClinic.StateLicense),ProgramProperties.GetValOrDefault(program.ProgramNum,PdmpProperty.PdmpProvLicenseField,0));
			int countRows=listRows.Count;
			Assert.IsFalse(PdmpProperty.EnsureProperties(program),"Idempotent.");
			Assert.AreEqual(countRows,ProgramProperties.GetWhere(x => x.ProgramNum==program.ProgramNum).Count);
			Assert.IsTrue(PdmpProperty.EnsureProperties(Programs.GetCur(ProgramName.PDMP)));
			Assert.IsTrue(ProgramProperties.GetWhere(x => x.PropertyDesc=="Kentucky PDMP Password").Single().IsMasked);
		}

		[TestMethod]
		public void Reads_ClinicRowOverridesDefaultAndMissingIsBlank() {
			Program program=Bamboo;
			PdmpProperty pdmpPropertyEmpty=PdmpProperty.LoadBambooForSetup(program,0);
			Assert.AreEqual("",pdmpPropertyEmpty.Url,"No rows yet: blank, no exception.");
			Assert.AreEqual("",pdmpPropertyEmpty.Password);
			Assert.IsFalse(pdmpPropertyEmpty.IsPasswordReentryNeeded);
			SetRaw(program.ProgramNum,PdmpProperty.BambooUrl,0,PdmpTestData.GatewayUrl);
			SetRaw(program.ProgramNum,PdmpProperty.BambooUrl,5,"https://clinic5.example.test/patient");
			Assert.IsTrue(VendorCredentials.WriteSecret(program.ProgramNum,PdmpProperty.BambooPassword,5,"clinic-five-password"));
			ProgramProperties.RefreshCache();
			PdmpProperty pdmpPropertyClinic=PdmpProperty.LoadBambooForSetup(program,5);
			Assert.AreEqual("https://clinic5.example.test/patient",pdmpPropertyClinic.Url);
			Assert.AreEqual("clinic-five-password",pdmpPropertyClinic.Password);
			PdmpProperty pdmpPropertyOther=PdmpProperty.LoadBambooForSetup(program,6);
			Assert.AreEqual(PdmpTestData.GatewayUrl,pdmpPropertyOther.Url,"Falls back to Default.");
			Assert.AreEqual("",pdmpPropertyOther.Password);
		}

		[TestMethod]
		public void LegacyValues_ReadAsNotSetAndFailValidation() {
			Program program=Bamboo;
			//Values Open Dental wrote: its integrator certificate in its own ciphertext, and an office password in Open Dental's v2 format.
			SetRaw(program.ProgramNum,PdmpProperty.BambooAuthToken,0,"QmFzZTY0T3BlbkRlbnRhbENpcGhlcnRleHQ=");
			SetRaw(program.ProgramNum,PdmpProperty.BambooAuthPassword,0,"legacyCiphertext==");
			SetRaw(program.ProgramNum,PdmpProperty.BambooPassword,0,"odv2e$notreadable");
			SetRaw(program.ProgramNum,PdmpProperty.BambooUrl,0,PdmpTestData.GatewayUrl);
			SetRaw(program.ProgramNum,PdmpProperty.BambooUserName,0,PdmpTestData.Username);
			ProgramProperties.RefreshCache();
			Assert.IsTrue(PdmpProperty.HasLegacyValues(program.ProgramNum));
			PdmpProperty pdmpProperty=PdmpProperty.LoadBambooForSetup(program,0);
			Assert.IsTrue(pdmpProperty.IsClientCertLegacy);
			Assert.AreEqual("",pdmpProperty.BambooClientKey,"Never passed to X509Certificate2.");
			Assert.AreEqual("",pdmpProperty.BambooClientPassword);
			Assert.IsTrue(pdmpProperty.IsPasswordReentryNeeded);
			Assert.AreEqual("",pdmpProperty.Password,"Never hashed or sent.");
			Assert.AreEqual(PdmpCertificate.MessageNotSet,PdmpProperty.GetCertificateStatusText(pdmpProperty,DateTime.UtcNow));
			pdmpProperty.PdmpPat=PdmpTestData.MakePatient();
			pdmpProperty.PdmpProv=PdmpTestData.MakeProvider();
			pdmpProperty.StateAbbr="KS";
			pdmpProperty.Dea="AB1234563";
			pdmpProperty.ProvLicenseType=nameof(ProviderClinic.StateLicense);
			pdmpProperty.ProvLicenseNum="L-12345";
			ODException ex=AssertEx.Throws<ODException>(() => pdmpProperty.Validate(program));
			StringAssert.Contains(ex.Message,PdmpCertificate.MessageNotSet);
			StringAssert.Contains(ex.Message,"Re-enter the password");
			Assert.IsFalse(ex.Message.Contains("contact support"));
		}

		[TestMethod]
		public void PlainTextPassword_IsAlsoRejected() {
			Program program=Programs.GetCur(ProgramName.PDMP);
			SetRaw(program.ProgramNum,"Kentucky PDMP Password",0,"plaintextFromOpenDental");
			ProgramProperties.RefreshCache();
			List<PdmpSetupCheckItem> listItems=PdmpProperty.CheckSetup(program,0,new FakeHttpHandler());
			Assert.IsTrue(listItems.Any(x => x.Level==PdmpSetupCheckLevel.Error && x.Text.StartsWith("Kentucky: ") && x.Text.Contains("Re-enter the password")),
				string.Join("\n",listItems.Select(x => x.ToString())));
		}

		[TestMethod]
		public void CheckSetup_BambooWithAnImportedCertificate() {
			Program program=Bamboo;
			PdmpProperty.EnsureProperties(program);
			SetRaw(program.ProgramNum,PdmpProperty.BambooUrl,0,PdmpTestData.GatewayUrl);
			SetRaw(program.ProgramNum,PdmpProperty.BambooUserName,0,PdmpTestData.Username);
			SetRaw(program.ProgramNum,PdmpProperty.BambooFacilityID,0,"Main Office");
			Assert.IsTrue(VendorCredentials.WriteSecret(program.ProgramNum,PdmpProperty.BambooPassword,0,PdmpTestData.Password));
			byte[] arrayPfx=PdmpTestData.MakeClientPfx("pfx-pass");
			Assert.IsTrue(VendorCredentials.WriteSecret(program.ProgramNum,PdmpProperty.BambooAuthToken,0,Convert.ToBase64String(arrayPfx)));
			Assert.IsTrue(VendorCredentials.WriteSecret(program.ProgramNum,PdmpProperty.BambooAuthPassword,0,"pfx-pass"));
			ProgramProperties.RefreshCache();
			Assert.IsFalse(PdmpProperty.HasLegacyValues(program.ProgramNum));
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().Enqueue(HttpStatusCode.NotFound,"","text/plain");
			List<PdmpSetupCheckItem> listItems=PdmpProperty.CheckSetup(program,0,fakeHttpHandler);
			string text=string.Join("\n",listItems.Select(x => x.ToString()));
			Assert.IsFalse(listItems.Any(x => x.Level==PdmpSetupCheckLevel.Error),text);
			StringAssert.Contains(text,"PDMP Test Client");
			StringAssert.Contains(text,"The service answered.");
			RecordedRequest recorded=fakeHttpHandler.Requests.Single();
			Assert.AreEqual(System.Net.Http.HttpMethod.Get,recorded.Method);
			Assert.AreEqual("https://gateway.example.test/",recorded.RequestUri.AbsoluteUri);
			Assert.IsNull(recorded.GetHeader("X-Auth-Username"),"The setup check sends no credentials but the certificate.");
			StringAssert.Contains(PdmpProperty.GetCertificateStatusText(PdmpProperty.LoadBambooForSetup(program,0),DateTime.UtcNow),"PDMP Test Client");
		}

		[TestMethod]
		public void Validate_BambooAcceptsAnImportedCertificateWithoutAPassword() {
			Program program=Bamboo;
			PdmpProperty.EnsureProperties(program);
			SetRaw(program.ProgramNum,PdmpProperty.BambooUrl,0,PdmpTestData.GatewayUrl);
			SetRaw(program.ProgramNum,PdmpProperty.BambooUserName,0,PdmpTestData.Username);
			SetRaw(program.ProgramNum,PdmpProperty.BambooFacilityID,0,"Main Office");
			Assert.IsTrue(VendorCredentials.WriteSecret(program.ProgramNum,PdmpProperty.BambooPassword,0,PdmpTestData.Password));
			//What PdmpCertificate.SaveImported stores for a .pfx exported without a password: the file, and a blank password.
			byte[] arrayPfx=PdmpTestData.MakeClientPfx("");
			Assert.IsTrue(VendorCredentials.WriteSecret(program.ProgramNum,PdmpProperty.BambooAuthToken,0,Convert.ToBase64String(arrayPfx)));
			Assert.IsTrue(VendorCredentials.WriteSecret(program.ProgramNum,PdmpProperty.BambooAuthPassword,0,""));
			ProgramProperties.RefreshCache();
			PdmpProperty pdmpProperty=PdmpProperty.LoadBambooForSetup(program,0);
			Assert.IsFalse(pdmpProperty.IsClientCertLegacy);
			Assert.AreEqual("",pdmpProperty.BambooClientPassword);
			Assert.AreEqual(PdmpCertificateSource.Database,PdmpCertificate.GetSource(pdmpProperty));
			//Check setup and the query path agree: both accept it.
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().Enqueue(HttpStatusCode.NotFound,"","text/plain");
			List<PdmpSetupCheckItem> listItems=PdmpProperty.CheckSetup(program,0,fakeHttpHandler);
			string text=string.Join("\n",listItems.Select(x => x.ToString()));
			Assert.IsFalse(listItems.Any(x => x.Level==PdmpSetupCheckLevel.Error),text);
			StringAssert.Contains(text,"PDMP Test Client");
			SetQueryFields(pdmpProperty);
			pdmpProperty.Validate(program);//Throws if refused.
			//A file that does have a password, stored with a blank one, is refused with the import message by both.
			Assert.IsTrue(VendorCredentials.WriteSecret(program.ProgramNum,PdmpProperty.BambooAuthToken,0,
				Convert.ToBase64String(PdmpTestData.MakeClientPfx("pfx-pass"))));
			ProgramProperties.RefreshCache();
			PdmpProperty pdmpPropertyWrong=PdmpProperty.LoadBambooForSetup(program,0);
			SetQueryFields(pdmpPropertyWrong);
			ODException ex=AssertEx.Throws<ODException>(() => pdmpPropertyWrong.Validate(program));
			StringAssert.Contains(ex.Message,"Check the password");
			listItems=PdmpProperty.CheckSetup(program,0,new FakeHttpHandler().Enqueue(HttpStatusCode.NotFound,"","text/plain"));
			Assert.IsTrue(listItems.Any(x => x.Level==PdmpSetupCheckLevel.Error && x.Text.Contains("Check the password")),
				string.Join("\n",listItems.Select(x => x.ToString())));
		}

		///<summary>The patient and provider fields SendData fills before it validates.</summary>
		private static void SetQueryFields(PdmpProperty pdmpProperty) {
			pdmpProperty.PdmpPat=PdmpTestData.MakePatient();
			pdmpProperty.PdmpProv=PdmpTestData.MakeProvider();
			pdmpProperty.StateAbbr="KS";
			pdmpProperty.Dea="AB1234563";
			pdmpProperty.ProvLicenseType=nameof(ProviderClinic.StateLicense);
			pdmpProperty.ProvLicenseNum="L-12345";
		}

		[TestMethod]
		public void CheckSetup_BambooNotConfigured() {
			Program program=Bamboo;
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			List<PdmpSetupCheckItem> listItems=PdmpProperty.CheckSetup(program,0,fakeHttpHandler);
			string text=string.Join("\n",listItems.Select(x => x.ToString()));
			StringAssert.Contains(text,"Program url was not set.");
			StringAssert.Contains(text,"Username and password are required.");
			StringAssert.Contains(text,PdmpCertificate.MessageNotSet);
			Assert.AreEqual(0,fakeHttpHandler.Requests.Count,"Nothing is contacted without a valid URL.");
			Assert.IsTrue(ProgramProperties.GetWhere(x => x.ProgramNum==program.ProgramNum).Count>0,"The check inserted the missing rows.");
		}
	}
}
