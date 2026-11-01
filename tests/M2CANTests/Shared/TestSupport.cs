// Shared helpers for the M2-CAN tests: repository paths, a culture scope, the STA thread for windows, and one scratch MariaDB database for the
// whole test run. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace M2CANTests {
	///<summary>Locates the repository.</summary>
	public static class TestPaths {
		///<summary>Walks up from the test assembly until it finds the repository root (LICENSE and NOTICE).</summary>
		public static string GetRepositoryRoot() {
			string dir=Path.GetDirectoryName(typeof(TestPaths).Assembly.Location);
			while(dir!=null) {
				if(File.Exists(Path.Combine(dir,"LICENSE")) && File.Exists(Path.Combine(dir,"NOTICE"))) {
					return dir;
				}
				dir=Path.GetDirectoryName(dir);
			}
			throw new InvalidOperationException("Could not find the repository root (LICENSE and NOTICE) above the test assembly.");
		}

		///<summary>A file under the repository root, for example GetRepoFile("OpenDental","Forms","FormProcTools.cs").</summary>
		public static string GetRepoFile(params string[] arrayParts) {
			string path=GetRepositoryRoot();
			for(int i=0;i<arrayParts.Length;i++) {
				path=Path.Combine(path,arrayParts[i]);
			}
			return path;
		}

		public static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(GetRepoFile(arrayParts));
		}
	}

	///<summary>Sets the thread's culture for a test and puts it back afterwards. The business layer reads the culture for translations (en-US needs
	///no database) and Canadian behavior (en-CA).</summary>
	public class CultureScope:IDisposable {
		private readonly CultureInfo _cultureInfoOld;
		private readonly CultureInfo _cultureInfoUiOld;

		public CultureScope(string name) {
			_cultureInfoOld=Thread.CurrentThread.CurrentCulture;
			_cultureInfoUiOld=Thread.CurrentThread.CurrentUICulture;
			Thread.CurrentThread.CurrentCulture=new CultureInfo(name);
			Thread.CurrentThread.CurrentUICulture=new CultureInfo(name);
		}

		public void Dispose() {
			Thread.CurrentThread.CurrentCulture=_cultureInfoOld;
			Thread.CurrentThread.CurrentUICulture=_cultureInfoUiOld;
		}
	}

	public static class Sta {
		///<summary>Runs the action on a single-threaded apartment thread with the given culture, as WinForms controls need.</summary>
		public static void Run(string cultureName,Action action) {
			Exception exception=null;
			Thread thread=new Thread(() => {
				try {
					Thread.CurrentThread.CurrentCulture=new CultureInfo(cultureName);
					Thread.CurrentThread.CurrentUICulture=new CultureInfo(cultureName);
					action();
				}
				catch(Exception ex) {
					exception=ex;
				}
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			if(exception!=null) {
				throw new AssertFailedException("Failed on the STA thread: "+exception,exception);
			}
		}
	}

	///<summary>One scratch database for the run, with the Open Dental 26.1.70 schema from the reference embedded in OpenDentBusiness. Its name is
	///unique to the run (time, process id, random), because several worktrees and test runners share the one local server; it is dropped when the
	///run ends. Database tests are inconclusive when no server answers at 127.0.0.1:3306 as root with a blank password, or when
	///M2CAN_TEST_DB=skip.</summary>
	[TestClass]
	public class ScratchDb {
		private const string SERVER="127.0.0.1";
		private const string CONNECTION="Server="+SERVER+";Port=3306;User ID=root;Password=;Connection Timeout=15";
		public static readonly string Name="m2can_"+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);
		private static string _skipReason="Not set up yet.";
		private static bool _isCreated;
		private static readonly object _lock=new object();

		///<summary>Creates the database the first time a database test asks for it. Returns null when it is ready, otherwise why the tests skip.
		///</summary>
		public static string EnsureCreated() {
			lock(_lock) {
				if(_isCreated) {
					return _skipReason;
				}
				_isCreated=true;
				if(Environment.GetEnvironmentVariable("M2CAN_TEST_DB")=="skip") {
					_skipReason="M2CAN_TEST_DB=skip";
					return _skipReason;
				}
				try {
					using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
						connection.Open();
						Exec(connection,"CREATE DATABASE `"+Name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
					}
				}
				catch(Exception ex) {
					_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
					return _skipReason;
				}
				//The server answers, so from here on a failure is a real failure, not a reason to skip.
				using(MySqlConnection connection=new MySqlConnection(CONNECTION+";Database="+Name)) {
					connection.Open();
					foreach(SchemaTable table in SchemaVerifier.GetReference().Tables) {
						Exec(connection,table.ToDdl());
					}
					//The few preferences the code under test reads (generated inserts read RandomPrimaryKeys).
					Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES('RandomPrimaryKeys','0'),('DxIcdVersion','10')");
				}
				RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
				new DataConnection().SetDb(SERVER,Name,"root","","","",DatabaseType.MySql,true);
				Security.CurUser=new Userod { UserNum=1,UserName="Synthetic" };
				_skipReason=null;
				return null;
			}
		}

		///<summary>Empties the tables these tests write.</summary>
		public static void Clear() {
			foreach(string table in new[] { "procedurecode","definition","securitylog","securityloghash","claimproc","procedurelog","language" }) {
				DataCore.NonQ("DELETE FROM "+table);
			}
			Defs.RefreshCache();
			ProcedureCodes.RefreshCache();
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(_skipReason!=null) {
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS `"+Name+"`");
				}
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless; its name is unique to this run.
			}
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}
	}
}
