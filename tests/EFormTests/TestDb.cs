// Scratch databases on the local MariaDB for the eForm tests. Synthetic data only: nothing here reads or copies an office database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text;
using DataConnectionBase;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace EFormTests {
	///<summary>Creates, connects to and drops scratch databases. Every name starts with "eformtest_" so a test can never drop anything else.</summary>
	public static class TestDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		private const string PREFIX="eformtest_";

		///<summary>Unique to this run, so parallel runs from other worktrees don't share a database.</summary>
		public static string ScratchName(string purpose) {
			return PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+purpose;
		}

		///<summary>Null when the server answers; otherwise why database tests are skipped.</summary>
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("EFORM_TEST_DB")=="skip") {
				return "EFORM_TEST_DB=skip";
			}
			//The shared development server can be slow to answer while other test runs load it, so try a few times before skipping.
			string reason=null;
			for(int i=0;i<3;i++) {
				try {
					using(MySqlConnection connection=OpenServer()) {
						return null;
					}
				}
				catch(Exception ex) {
					reason="No local MariaDB at "+Server+": "+ex.GetBaseException().Message;
				}
			}
			return reason;
		}

		public static MySqlConnection OpenServer() {
			MySqlConnection connection=new MySqlConnection("Server="+Server+";Port=3306;User ID="+User+";Password="+Password
				+";Connection Timeout=15;Allow User Variables=true");
			connection.Open();
			return connection;
		}

		public static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX);
			}
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("DROP DATABASE IF EXISTS `"+name+"`",connection)) {
				command.ExecuteNonQuery();
			}
		}

		///<summary>Builds a fresh database the way tools/New-DevDatabase.ps1 does (the seed dump, the frozen 24.3.63 convert chain), then runs the
		///fork migration runner so the schema is at the Open Dental 25.3.48 level. Leaves the business layer connected to it.</summary>
		public static void CreateFresh25_3(string name) {
			Drop(name);
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci",connection)) {
				command.ExecuteNonQuery();
			}
			Connect(name);
			string dumpPath=FindRepoFile(Path.Combine("UnitTests","Resources","dump.txt"));
			DataCore.NonQ(File.ReadAllText(dumpPath,new UTF8Encoding(false)));
			Security.CurUser=Security.CurUser??new Userod();
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"1");
			ConvertDatabases.FromVersion=new Version(DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='DataBaseVersion'"));
			ConvertDatabases.InvokeConvertMethods();
			Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"0");
			Cache.Refresh(InvalidType.Prefs);
			ForkMigrationRunner.Run();
			Cache.Refresh(InvalidType.Prefs);
		}

		///<summary>Points the business layer (DataCore, Db, the caches) at the database.</summary>
		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,name,User,Password,"","",DatabaseType.MySql,true);
		}

		///<summary>Walks up from the test assembly's folder to the repository root and returns the path of a file in it.</summary>
		public static string FindRepoFile(string relativePath) {
			DirectoryInfo dir=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(dir!=null) {
				string candidate=Path.Combine(dir.FullName,relativePath);
				if(File.Exists(candidate)) {
					return candidate;
				}
				dir=dir.Parent;
			}
			throw new FileNotFoundException("Not found above the test folder: "+relativePath);
		}
	}
}
