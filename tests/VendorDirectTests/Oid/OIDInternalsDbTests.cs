// Integration tests for OIDInternals.ApplyRoot against a scratch MariaDB database on this computer. They are skipped (inconclusive) when no
// server answers at 127.0.0.1:3306 as root with a blank password, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;

namespace VendorDirectTests.Oid {
	[TestClass]
	public class OIDInternalsDbTests {
		private const string DB_NAME="vendordirect_oid_tests";
		private const string CONNECTION_SERVER="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3";
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("VENDORDIRECT_TEST_DB")=="skip") {
				_skipReason="VENDORDIRECT_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION_SERVER)) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS "+DB_NAME);
					Exec(connection,"CREATE DATABASE "+DB_NAME+" CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
					Exec(connection,"USE "+DB_NAME);
					//The same definitions as Open Dental 24.3's oidinternal and preference tables.
					Exec(connection,"CREATE TABLE oidinternal (OIDInternalNum bigint NOT NULL auto_increment PRIMARY KEY,IDType varchar(255) NOT NULL,"
						+"IDRoot varchar(255) NOT NULL) DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE preference (PrefName varchar(255) NOT NULL DEFAULT '',ValueString text NOT NULL,"
						+"PrefNum bigint(20) NOT NULL AUTO_INCREMENT,Comments text DEFAULT NULL,PRIMARY KEY (PrefNum),KEY PrefName (PrefName)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
				}
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
				using(MySqlConnection connection=new MySqlConnection(CONNECTION_SERVER)) {
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
			//Other test classes point the static connection at their own scratch databases, so set it for every test.
			new DataConnection().SetDb("127.0.0.1",DB_NAME,"root","","root","",DatabaseType.MySql,true);
			Prefs.RefreshCache();
			using(MySqlConnection connection=new MySqlConnection(CONNECTION_SERVER+";Database="+DB_NAME)) {
				connection.Open();
				Exec(connection,"DELETE FROM oidinternal");
			}
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		[TestMethod]
		public void ApplyRoot_EmptyTableGetsEveryRow() {
			string root=OIDInternals.GenerateUuidRoot();
			OIDInternals.ApplyRoot(root,overwriteChildren:false);
			List<OIDInternal> listOIDInternals=OIDInternals.GetAll();
			Assert.AreEqual(Enum.GetValues(typeof(IdentifierType)).Length,listOIDInternals.Count);
			Assert.AreEqual(root,OIDInternals.GetForType(IdentifierType.Root).IDRoot);
			Assert.AreEqual(root+".2",OIDInternals.GetForType(IdentifierType.Patient).IDRoot);
			Assert.AreEqual(root+".8",OIDInternals.GetForType(IdentifierType.Procedure).IDRoot);
		}

		[TestMethod]
		public void ApplyRoot_KeepsCustomChildrenUnlessOverwriting() {
			OIDInternals.ApplyRoot("2.16.840.1.113883.3.4337.1486.12345",overwriteChildren:true);
			OIDInternal oIDInternalPatient=OIDInternals.GetForType(IdentifierType.Patient);
			oIDInternalPatient.IDRoot="1.3.6.1.4.1.55555.7";
			OIDInternals.Update(oIDInternalPatient);
			OIDInternals.ApplyRoot("1.3.6.1.4.1.99999.1",overwriteChildren:false);
			Assert.AreEqual("1.3.6.1.4.1.99999.1",OIDInternals.GetForType(IdentifierType.Root).IDRoot);
			Assert.AreEqual("1.3.6.1.4.1.99999.1.1",OIDInternals.GetForType(IdentifierType.LabOrder).IDRoot);
			Assert.AreEqual("1.3.6.1.4.1.55555.7",OIDInternals.GetForType(IdentifierType.Patient).IDRoot);
			OIDInternals.ApplyRoot("1.3.6.1.4.1.99999.1",overwriteChildren:true);
			Assert.AreEqual("1.3.6.1.4.1.99999.1.2",OIDInternals.GetForType(IdentifierType.Patient).IDRoot);
			Assert.AreEqual(Enum.GetValues(typeof(IdentifierType)).Length,OIDInternals.GetAll().Count,"No duplicate rows.");
		}

		[TestMethod]
		public void ApplyRoot_InvalidRootWritesNothing() {
			OIDInternals.ApplyRoot("1.2.3",overwriteChildren:true);
			try {
				OIDInternals.ApplyRoot("1.2.3.",overwriteChildren:true);
				Assert.Fail("An invalid root was accepted.");
			}
			catch(CodeBase.ODException) {
			}
			Assert.IsTrue(OIDInternals.GetAll().All(x => x.IDRoot=="1.2.3" || x.IDRoot.StartsWith("1.2.3.")));
			Assert.AreEqual("1.2.3",OIDInternals.GetForType(IdentifierType.Root).IDRoot);
		}
	}
}
