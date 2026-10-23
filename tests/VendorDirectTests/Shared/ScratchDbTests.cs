// Tests for ScratchDb: per-run names, and which leftover databases a later run drops. The drop test needs the local MariaDB and is skipped
// (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password, or when VENDORDIRECT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;

namespace VendorDirectTests {
	[TestClass]
	public class ScratchDbTests {
		private const string CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3";

		[TestMethod]
		public void Name_IsUniqueToThisRunAndFitsMariaDb() {
			string name=ScratchDb.Name("vendordirect_payconnect1_removed_tests");
			StringAssert.StartsWith(name,"vendordirect_payconnect1_removed_tests_");
			StringAssert.Matches(name,new System.Text.RegularExpressions.Regex(@"^[a-z0-9_]+_\d{12}_\d+_\d{4}$"));
			Assert.IsTrue(name.Length<=64,name);
			Assert.AreEqual(name,ScratchDb.Name("vendordirect_payconnect1_removed_tests"),"Stable within one run.");
			AssertEx.Throws<ArgumentException>(() => ScratchDb.Name("forkmig_tests"));
			AssertEx.Throws<ArgumentException>(() => ScratchDb.Name("vendordirect_bad-name"));
		}

		[TestMethod]
		public void DropStale_DropsOnlyOldPerRunDatabases() {
			if(Environment.GetEnvironmentVariable("VENDORDIRECT_TEST_DB")=="skip") {
				Assert.Inconclusive("VENDORDIRECT_TEST_DB=skip");
			}
			string nameOld="vendordirect_zz_scratchdb_tests_200101000000_1_1234";
			string nameRecent="vendordirect_zz_scratchdb_tests_"+DateTime.UtcNow.AddHours(-1).ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_1_1234";
			string nameFixed="vendordirect_zz_scratchdb_tests";
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					foreach(string name in new[] { nameOld,nameRecent,nameFixed }) {
						Exec(connection,"CREATE DATABASE IF NOT EXISTS "+name);
					}
				}
			}
			catch(Exception ex) {
				Assert.Inconclusive("No scratch MariaDB database: "+ex.GetBaseException().Message);
			}
			try {
				ScratchDb.DropStale();
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Assert.IsFalse(Exists(connection,nameOld),"Left by a run that started long ago.");
					Assert.IsTrue(Exists(connection,nameRecent),"A run that may still be going.");
					Assert.IsTrue(Exists(connection,nameFixed),"No per-run id: never this helper's to drop.");
				}
			}
			finally {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					foreach(string name in new[] { nameOld,nameRecent,nameFixed }) {
						Exec(connection,"DROP DATABASE IF EXISTS "+name);
					}
				}
			}
		}

		private static bool Exists(MySqlConnection connection,string name) {
			using(MySqlCommand command=new MySqlCommand("SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME=@name",connection)) {
				command.Parameters.AddWithValue("@name",name);
				return Convert.ToInt64(command.ExecuteScalar())>0;
			}
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}
	}
}
