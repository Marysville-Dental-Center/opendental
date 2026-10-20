// DentalXChange tests against a scratch MariaDB database on this computer, with tables from the Open Dental 25.3.48 reference schema: the partner
// identity rows, the "not set up" gate on every call that sends it, the etrans log of XConnect validation, and the fork migration step that keeps
// XConnect on for 24.3 offices. Synthetic data only. Skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank
// password, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.Eclaims;
using OpenDentBusiness.ForkMigrations;
using OpenDentBusiness.ForkMigrations.FeatureSteps;
using OpenDentBusiness.VendorDirect.DentalXChange;

namespace VendorDirectTests.DentalXChange {
	[TestClass]
	public class DentalXChangeDbTests {
		private const string DB_NAME="vendordirect_dxc_tests";
		private const string CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=15";
		private static readonly string[] _arrayTables={ "preference","clearinghouse","etrans","etransmessagetext","insplan" };
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
					for(int i=0;i<_arrayTables.Length;i++) {
						Exec(connection,DxcTestFiles.GetReferenceCreateTable(_arrayTables[i]));
					}
				}
				RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
				new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
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
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			using(MySqlConnection connection=OpenDb()) {
				for(int i=0;i<_arrayTables.Length;i++) {
					Exec(connection,"DELETE FROM "+_arrayTables[i]);
				}
				Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0'),('DataBaseVersion','24.3.63.0')");
			}
			Prefs.RefreshCache();
			XConnect.HttpHandlerForTests=null;
		}

		[TestCleanup]
		public void TestCleanup() {
			XConnect.HttpHandlerForTests=null;
		}

		private static MySqlConnection OpenDb() {
			MySqlConnection connection=new MySqlConnection(CONNECTION+";Database="+DB_NAME);
			connection.Open();
			Exec(connection,"SET SESSION sql_mode=''");//Test rows leave out columns that have no default.
			return connection;
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static string Scalar(string command) {
			using(MySqlConnection connection=OpenDb())
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				object value=mySqlCommand.ExecuteScalar();
				return value==null || value==DBNull.Value ? null : value.ToString();
			}
		}

		#region Partner identity
		[TestMethod]
		public void PartnerIdentity_BlankUntilSaved_ThenStoredInForkRows() {
			Assert.IsFalse(DxcPartnerIdentity.GetCur().IsConfigured,"An Open Dental database has no rows: blank and off.");
			Assert.IsNull(Scalar("SELECT ValueString FROM preference WHERE PrefName='"+DxcPartnerIdentity.PREF_CLIENT+"'"));
			Assert.IsTrue(DxcPartnerIdentity.Save(" Test Partner ","TEST-SERVICE-0001"));
			Assert.AreEqual("Test Partner",Scalar("SELECT ValueString FROM preference WHERE PrefName='"+DxcPartnerIdentity.PREF_CLIENT+"'"));
			Assert.AreEqual("TEST-SERVICE-0001",Scalar("SELECT ValueString FROM preference WHERE PrefName='"+DxcPartnerIdentity.PREF_SERVICE_ID+"'"));
			Assert.IsTrue(DxcPartnerIdentity.GetCur().IsConfigured,"This workstation's cache is updated.");
			Prefs.RefreshCache();
			DxcPartnerIdentity identity=DxcPartnerIdentity.GetCur();
			Assert.AreEqual("Test Partner",identity.Client);
			Assert.AreEqual("TEST-SERVICE-0001",identity.ServiceID);
			Assert.IsFalse(DxcPartnerIdentity.Save("Test Partner","TEST-SERVICE-0001"),"No change.");
			//Clearing it turns the services off again; the rows stay, blank.
			Assert.IsTrue(DxcPartnerIdentity.Save("",""));
			Prefs.RefreshCache();
			Assert.IsFalse(DxcPartnerIdentity.GetCur().IsConfigured);
			Assert.AreEqual("1",Scalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+DxcPartnerIdentity.PREF_CLIENT+"'"));
		}

		[TestMethod]
		public void NotSetUp_EveryCallThatSendsTheIdentityRefusesBeforeTheNetwork() {
			Clearinghouse clearinghouse=new Clearinghouse {
				CommBridge=EclaimsCommBridge.ClaimConnect,
				LoginID="office-login",
				Password="office-password",
				IsEraDownloadAllowed=EraBehaviors.DownloadAndReceive,
				ResponsePath=System.IO.Path.GetTempPath(),
			};
			string message=DxcPartnerIdentity.GetNotConfiguredMessage();
			//Eligibility and the payer list return the message instead of calling DentalXChange.
			Etrans etransHtml;
			Assert.AreEqual(message,ClaimConnect.Benefits270(clearinghouse,"ISA*",out etransHtml));
			Assert.IsNull(etransHtml);
			Assert.AreEqual(message,ClaimConnect.GetPayerList(clearinghouse));
			//ERA download stops with the message.
			Assert.IsFalse(ClaimConnect.Retrieve(clearinghouse));
			Assert.AreEqual(message,ClaimConnect.ErrorMessage);
			//Every SOAP call builds its credentials here, which refuses.
			CodeBase.ODException ex=AssertEx.Throws<CodeBase.ODException>(() => ClaimConnect.DxcCredentials.GetDentalxchangeCredentials(null,clearinghouse));
			Assert.AreEqual(message,ex.Message);
		}

		[TestMethod]
		public void SetUp_CredentialsCarryTheSavedIdentity() {
			DxcPartnerIdentity.Save("Test Partner","TEST-SERVICE-0001");
			Clearinghouse clearinghouse=new Clearinghouse { LoginID="office-login",Password="office-password" };
			ClaimConnect.DxcCredentials cred=ClaimConnect.DxcCredentials.GetDentalxchangeCredentials(null,clearinghouse);
			Assert.AreEqual("Test Partner",cred.Client);
			Assert.AreEqual("TEST-SERVICE-0001",cred.ServiceID);
		}
		#endregion Partner identity

		#region XConnect etrans log
		[TestMethod]
		public void LogValidation_RequestAndAcknowledgementLinked() {
			Claim claim=new Claim { ClaimNum=501,PatNum=601,PlanNum=0,InsSubNum=701 };
			Clearinghouse clearinghouse=new Clearinghouse { ClearinghouseNum=11,HqClearinghouseNum=3 };
			Etrans etransRequest=XConnect.LogValidation(claim,clearinghouse,"{\"claim\":{}}","{\"status\":{\"code\":0}}");
			Assert.IsNotNull(etransRequest);
			DataTable table=DataCore.GetTable("SELECT EtransNum,Etype,ClaimNum,PatNum,InsSubNum,ClearingHouseNum,AckEtransNum,EtransMessageTextNum FROM etrans ORDER BY EtransNum");
			Assert.AreEqual(2,table.Rows.Count);
			DataRow rowAck=table.Rows[0];
			DataRow rowRequest=table.Rows[1];
			Assert.AreEqual(((int)EtransType.ClaimAck_XConnect).ToString(),rowAck["Etype"].ToString());
			Assert.AreEqual("37",rowAck["Etype"].ToString());
			Assert.AreEqual("36",rowRequest["Etype"].ToString());
			Assert.AreEqual(rowAck["EtransNum"].ToString(),rowRequest["AckEtransNum"].ToString(),"The request points to its acknowledgement.");
			Assert.AreEqual("501",rowRequest["ClaimNum"].ToString());
			Assert.AreEqual("601",rowRequest["PatNum"].ToString());
			Assert.AreEqual("701",rowRequest["InsSubNum"].ToString());
			Assert.AreEqual("3",rowRequest["ClearingHouseNum"].ToString(),"The HQ clearinghouse.");
			Assert.AreEqual("{\"claim\":{}}",EtransMessageTexts.GetMessageText(PIn.Long(rowRequest["EtransMessageTextNum"].ToString())));
			Assert.AreEqual("{\"status\":{\"code\":0}}",EtransMessageTexts.GetMessageText(PIn.Long(rowAck["EtransMessageTextNum"].ToString())));
		}

		[TestMethod]
		public void LogValidation_FailedCallLogsTheRequestOnly() {
			Claim claim=new Claim { ClaimNum=502,PatNum=602 };
			XConnect.LogValidation(claim,new Clearinghouse { ClearinghouseNum=4 },"{}",null);
			DataTable table=DataCore.GetTable("SELECT Etype,AckEtransNum,Note FROM etrans");
			Assert.AreEqual(1,table.Rows.Count);
			Assert.AreEqual("36",table.Rows[0]["Etype"].ToString());
			Assert.AreEqual("0",table.Rows[0]["AckEtransNum"].ToString());
			StringAssert.Contains(table.Rows[0]["Note"].ToString(),"No response");
		}
		#endregion XConnect etrans log

		#region Fork migration step
		///<summary>A ClaimConnect HQ row with a key, its clinic overrides (one blank, which inherits the key; one with its own key), a ClaimConnect
		///HQ row without a key, and an EDS row with a LocationID. Synthetic values.</summary>
		private static void InsertClearinghouses() {
			using(MySqlConnection connection=OpenDb()) {
				Exec(connection,"INSERT INTO clearinghouse (ClearinghouseNum,Description,CommBridge,LocationID,ClinicNum,HqClearinghouseNum,EnableXConnect) VALUES "
					+"(1,'ClaimConnect',4,'test-key-hq',0,1,0),"
					+"(2,'',0,'',7,1,0),"
					+"(3,'',0,'test-key-clinic',8,1,0),"
					+"(4,'ClaimConnect without key',4,'',0,4,0),"
					+"(5,'',0,'',9,4,0),"
					+"(6,'EDS',22,'eds-location',0,6,0)");
			}
		}

		private static string GetEnabled(long clearinghouseNum) {
			return Scalar("SELECT EnableXConnect FROM clearinghouse WHERE ClearinghouseNum="+clearinghouseNum);
		}

		[TestMethod]
		public void XConnectEnableStep_From24_3_KeepsXConnectWhereItWasOn() {
			InsertClearinghouses();
			ForkMigrationContext context=new ForkMigrationContext(DB_NAME);
			new XConnectEnableStep().Apply(context);
			Assert.AreEqual("1",GetEnabled(1),"HQ ClaimConnect row with a key.");
			Assert.AreEqual("1",GetEnabled(2),"Clinic override that inherits the HQ key.");
			Assert.AreEqual("1",GetEnabled(3),"Clinic override with its own key.");
			Assert.AreEqual("0",GetEnabled(4),"No key: XConnect was off in 24.3.");
			Assert.AreEqual("0",GetEnabled(5),"No key anywhere.");
			Assert.AreEqual("0",GetEnabled(6),"Not ClaimConnect.");
			//Running again changes nothing.
			ForkMigrationContext contextAgain=new ForkMigrationContext(DB_NAME);
			new XConnectEnableStep().Apply(contextAgain);
			Assert.AreEqual(0,contextAgain.ListChanges.Count,string.Join(Environment.NewLine,contextAgain.ListChanges));
		}

		[TestMethod]
		public void XConnectEnableStep_OpenDental25_3DatabaseIsLeftAlone() {
			InsertClearinghouses();
			using(MySqlConnection connection=OpenDb()) {
				Exec(connection,"UPDATE preference SET ValueString='25.3.48.0' WHERE PrefName='DataBaseVersion'");
			}
			ForkMigrationContext context=new ForkMigrationContext(DB_NAME);
			new XConnectEnableStep().Apply(context);
			Assert.AreEqual(0,context.ListChanges.Count);
			Assert.AreEqual("0",GetEnabled(1),"The office's own 25.3 setting stays.");
		}

		[TestMethod]
		public void XConnectEnableStep_IsRegisteredAfterThe25_2Schema() {
			ForkMigrationAttribute attribute=new XConnectEnableStep().Attribute;
			Assert.IsTrue(attribute.Level>2026101903,"After the 25.2 schema step that adds the column.");
			Assert.AreEqual(2026101951,attribute.Level);
		}
		#endregion Fork migration step
	}
}
