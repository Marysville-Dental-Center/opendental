// Test helper for every vendor-direct package: per-run names for the scratch databases on the local MariaDB.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;

namespace VendorDirectTests {
	///<summary>Names the scratch databases the database test classes create, drop and rebuild. Several test runs share the one local MariaDB server
	///(other worktrees, other test runners, at the same time), so a fixed name let one run drop or empty a database another run was still testing.
	///Every name is the class's base name plus an id unique to this run.</summary>
	public static class ScratchDb {
		private const string PREFIX="vendordirect_";
		private const string SERVER_CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3";
		///<summary>Databases left by a run that started this long ago (a crash, or a cleanup that could not reach the server) are dropped by later
		///runs. No run takes nearly this long.</summary>
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>The start time (UTC, yyMMddHHmmss), the process id and a random number.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);

		///<summary>This run's database for the given base name, like vendordirect_pdmp_tests_261019191500_1234_5678. At most 64 characters, the
		///MariaDB limit, and only letters, digits and underscores, so callers may use it unquoted.</summary>
		public static string Name(string baseName) {
			if(!baseName.StartsWith(PREFIX) || !Regex.IsMatch(baseName,"^[a-z0-9_]+$")) {
				throw new ArgumentException("Not a vendor-direct test database name: "+baseName);
			}
			string name=baseName+"_"+_runId;
			if(name.Length>64) {
				throw new ArgumentException("Scratch database name too long: "+name);
			}
			return name;
		}

		///<summary>Drops the databases earlier runs left behind, once they are older than _timeSpanStale. Never drops a database of a run that may
		///still be going, and never a name without the per-run id, such as the fixed names older versions of these tests used.</summary>
		public static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"[a-z0-9_]+_(\d{12})_\d+_\d{4}$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=new MySqlConnection(SERVER_CONNECTION)) {
				connection.Open();
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
					using(MySqlCommand command=new MySqlCommand("DROP DATABASE IF EXISTS `"+name+"`",connection)) {
						command.ExecuteNonQuery();
					}
				}
			}
		}
	}

	[TestClass]
	public class ScratchDbAssemblySetup {
		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("VENDORDIRECT_TEST_DB")=="skip") {
				return;
			}
			try {
				ScratchDb.DropStale();
			}
			catch(Exception) {
				//No server, or no rights: the database test classes report that themselves and skip.
			}
		}
	}
}
