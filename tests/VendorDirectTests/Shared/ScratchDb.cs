// Test helper for every vendor-direct package: names for the scratch databases on the local MariaDB.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;

namespace VendorDirectTests {
	///<summary>Several test runs share the one local MariaDB server: other worktrees, other test runners, at the same time. With a fixed database
	///name, one run's ClassInitialize or TestInitialize dropped, rebuilt or emptied the tables another run was still testing, and that run's
	///assertions failed at random (for example "Program url was not set" right after the test had set it). Every run gets its own names.</summary>
	[TestClass]
	public class ScratchDb {
		private const string CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=10";
		private const string PREFIX="vendordirect_";
		///<summary>Databases a run left behind (a crash or an aborted run skips ClassCleanup) are dropped once they are this old. No run takes
		///nearly this long, so a run that is still going is never touched.</summary>
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>The start time of this run (UTC, yyMMddHHmmss) and the process id. Live processes never share an id.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id;

		///<summary>This run's database for a test class, like vendordirect_pdmp_tests_r261019191500_1234. purpose starts with vendordirect_. At
		///most 64 characters, the MariaDB limit.</summary>
		public static string Name(string purpose) {
			if(!purpose.StartsWith(PREFIX) || !Regex.IsMatch(purpose,"^[a-z0-9_]+$")) {
				throw new ArgumentException("Scratch database names start with "+PREFIX+" and use only a-z, 0-9 and _: "+purpose);
			}
			string name=purpose+"_r"+_runId;
			if(name.Length>64) {
				throw new ArgumentException("Scratch database name too long: "+name);
			}
			return name;
		}

		///<summary>Drops stale databases named by Name. Never a database without that naming scheme, such as the fixed names earlier versions of
		///these tests used, and never one younger than _timeSpanStale. Does nothing when no server answers.</summary>
		[AssemblyInitialize]
		public static void DropStale(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("VENDORDIRECT_TEST_DB")=="skip") {
				return;
			}
			Regex regex=new Regex("^"+PREFIX+@"[a-z0-9_]+_r(\d{12})_\d+$");
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					List<string> listNames=new List<string>();
					using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'vendordirect\\_%'",
						connection))
					using(MySqlDataReader reader=command.ExecuteReader())
					{
						while(reader.Read()) {
							listNames.Add(reader.GetString(0));
						}
					}
					foreach(string name in listNames) {
						Match match=regex.Match(name);
						DateTime dateStarted;
						if(!match.Success || !DateTime.TryParseExact(match.Groups[1].Value,"yyMMddHHmmss",CultureInfo.InvariantCulture,
							DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out dateStarted))
						{
							continue;
						}
						if(DateTime.UtcNow-dateStarted<_timeSpanStale) {
							continue;
						}
						try {
							using(MySqlCommand command=new MySqlCommand("DROP DATABASE IF EXISTS `"+name+"`",connection)) {
								command.ExecuteNonQuery();
							}
						}
						catch(Exception) {
							//Another run may be dropping it at the same time.
						}
					}
				}
			}
			catch(Exception) {
				//No server: the database test classes skip themselves.
			}
		}
	}
}
