// Database tests for the Open Dental 25.1 PayConnect columns (backlog package V-PC): computerpref.CreditCardTerminalId (the last terminal used on
// this workstation) and creditcard.CardHolderName (the cardholder on receipts). Each run builds a scratch MariaDB database on this computer whose
// tables are copied from the local 25.3.48 reference schema (od_253_schema, schema only, no rows). Skipped (inconclusive) when no server answers at
// 127.0.0.1:3306 as root with a blank password, when od_253_schema is missing, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;

namespace VendorDirectTests.PayConnectV2 {
	[TestClass]
	public class PayConnectParityDbTests {
		private static readonly string DB_NAME=ScratchDb.Name("vendordirect_payconnect_parity_tests");
		private const string DB_REFERENCE="od_253_schema";
		private const string CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=15";
		private static readonly string[] _arrayTables=new[] { "computerpref","creditcard","payterminal","preference" };
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
					using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='"+DB_REFERENCE+"'",
						connection))
					{
						if(Convert.ToInt32(mySqlCommand.ExecuteScalar())==0) {
							_skipReason="The 25.3.48 reference schema "+DB_REFERENCE+" is not loaded on this server.";
							return;
						}
					}
					Exec(connection,"DROP DATABASE IF EXISTS "+DB_NAME);
					Exec(connection,"CREATE DATABASE "+DB_NAME+" CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
					foreach(string table in _arrayTables) {
						Exec(connection,"CREATE TABLE "+DB_NAME+"."+table+" LIKE "+DB_REFERENCE+"."+table);
					}
					Exec(connection,"INSERT INTO "+DB_NAME+".preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
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
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			Prefs.RefreshCache();
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		[TestMethod]
		public void TerminalMemory_IsStoredOnThisWorkstationsRow() {
			ComputerPrefs.UpdateCreditCardTerminalId("T-200");
			ComputerPref computerPrefDb=ComputerPrefs.GetForComputer(ODEnvironment.MachineName);
			Assert.AreEqual("T-200",computerPrefDb.CreditCardTerminalId,"Saved in computerpref.CreditCardTerminalId.");
			Assert.AreEqual("T-200",ComputerPrefs.GetCreditCardTerminalId());
			Assert.AreEqual(1,PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM computerpref WHERE ComputerName='"
				+POut.String(ODEnvironment.MachineName)+"'")),"One row per workstation.");
			//The next payment preselects it among the clinic's terminals.
			PayTerminals.Insert(new PayTerminal { Name="Front Desk",ClinicNum=0,TerminalID="T-100" });
			PayTerminals.Insert(new PayTerminal { Name="Checkout",ClinicNum=0,TerminalID="T-200" });
			PayTerminals.Insert(new PayTerminal { Name="Other clinic",ClinicNum=9,TerminalID="T-900" });
			List<PayTerminal> listPayTerminals=PayTerminals.Refresh(0);
			Assert.AreEqual("T-200",listPayTerminals[PayTerminals.GetIndexToSelect(listPayTerminals,ComputerPrefs.GetCreditCardTerminalId())].TerminalID);
			ComputerPrefs.UpdateCreditCardTerminalId("T-900");
			Assert.AreEqual("T-900",ComputerPrefs.GetForComputer(ODEnvironment.MachineName).CreditCardTerminalId);
			Assert.AreEqual(0,PayTerminals.GetIndexToSelect(PayTerminals.Refresh(0),ComputerPrefs.GetCreditCardTerminalId()),
				"Another clinic's terminal falls back to the first.");
			DataCore.NonQ("DELETE FROM payterminal");
		}

		[TestMethod]
		public void CardHolderName_IsSavedOnTheCardAndPrintedOnTheReceipt() {
			CreditCard creditCard=new CreditCard();
			creditCard.PatNum=31;
			creditCard.CCNumberMasked="XXXXXXXXXXXX4242";
			creditCard.PayConnectToken="TKNsynthetic4242";
			creditCard.CCExpiration=new DateTime(2030,12,31);
			creditCard.CCSource=CreditCardSource.PayConnect;
			Assert.IsTrue(PayConnect2.SetCardHolderNameFromVendor(creditCard,"Synthetic Cardholder",wasCardEntered:true));
			long creditCardNum=CreditCards.Insert(creditCard);
			CreditCard creditCardDb=CreditCards.GetOne(creditCardNum);
			Assert.AreEqual("Synthetic Cardholder",creditCardDb.CardHolderName);
			//A later token charge (recurring too) sends and prints the saved name.
			Assert.AreEqual("Synthetic Cardholder",PayConnect2.GetCardHolderForRequest(new Patient { FName="Pat",LName="Synthetic" },creditCardDb.CardHolderName));
			Assert.AreEqual("Synthetic Cardholder",PayConnect2.GetCardHolderForReceipt("",creditCardDb));
			Assert.AreEqual("XXXXXXXXXXXX4242",PayConnect2.GetSavedCardDisplayText(creditCardDb));
			DataCore.NonQ("DELETE FROM creditcard");
		}

		[TestMethod]
		public void CardHolderName_ExistingRowsReadAsBlank() {
			//A card saved before 25.1 (or by another processor): no name was ever set, and the column holds ''.
			CreditCard creditCard=new CreditCard { PatNum=32,CCNumberMasked="XXXXXXXXXXXX1111",PayConnectToken="TKNsynthetic1111",
				CCSource=CreditCardSource.PayConnect };
			CreditCards.Insert(creditCard);
			Assert.AreEqual("",DataCore.GetScalar("SELECT CardHolderName FROM creditcard WHERE PatNum=32"));
			CreditCard creditCardDb=CreditCards.RefreshAll(32)[0];
			Assert.AreEqual("",creditCardDb.CardHolderName);
			Assert.AreEqual("Pat Synthetic",PayConnect2.GetCardHolderForRequest(new Patient { FName="Pat",LName="Synthetic" },creditCardDb.CardHolderName),
				"No saved name: the patient's, as before.");
			DataCore.NonQ("DELETE FROM creditcard");
		}
	}
}
