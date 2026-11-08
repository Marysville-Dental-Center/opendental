// The manual grants test: preflight, set-up and clean-up. The program's own code as the account is in GrantRun.Program.cs, the refusals in
// GrantRun.Refusals.cs, the Open Dental update chain and the adoption in GrantRun.Chain.cs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using DataConnectionBase;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace DbGrantsTests {
	///<summary>Thrown when the server is not in a state the harness may work in (an account or a database of that name exists, or the PUBLIC
	///role holds rights on one of the names). Nothing was created then.</summary>
	public class GrantPreflightException:Exception {
		public GrantPreflightException(string message):base(message) { }
	}

	///<summary>One run of the manual grants test on the local MariaDB: creates the temporary account with exactly the server guide's grants
	///(docs/db-connection-security.md section 5) and a random password, builds the office database from db/init/new-database.sql, runs the
	///program's own code as that account (GrantRun.Program.cs), proves that what the guide does not grant is refused and that each granted
	///right is needed (GrantRun.Refusals.cs), runs the Open Dental update chain from 21.1 and the adoption as the account (GrantRun.Chain.cs),
	///and at the end drops the account and every database it made. It changes no other account, role (PUBLIC included), setting or global
	///variable: Cleanup checks that against a snapshot taken first.</summary>
	public partial class GrantRun {
		///<summary>The first fork migration level the run leaves pending, so that the start-up runs those steps as the account: the routine
		///maintenance and inventory tracking steps (2026110201 and later), which create tables, add a column and write rows.</summary>
		public const long LEVEL_FIRST_PENDING=2026110201;
		public readonly GrantReport Report=new GrantReport();
		private readonly GrantSettings _settings;
		private readonly string _password;
		private bool _isAccountCreated;
		private string _serverStateBefore;
		private readonly List<string> _listTempPaths=new List<string>();
		private readonly DateTime _dateTimeStart=DateTime.Now;

		public GrantRun(GrantSettings grantSettings) {
			_settings=grantSettings;
			_password=GrantServer.NewPassword();
		}

		#region Names
		private string Db {
			get { return _settings.Database; }
		}

		private string Account {
			get { return _settings.Account; }
		}

		///<summary>The account as GRANT, REVOKE and DROP USER name it.</summary>
		private string AccountSql {
			get { return "'"+Account+"'@'localhost'"; }
		}

		///<summary>The guide's office database grant target: `<database>`.* with every '_' escaped.</summary>
		private string OnOffice {
			get { return "`"+GrantServer.EscapeForGrant(Db)+"`.*"; }
		}

		///<summary>The guide's backup grant target: `<database>backup\_%`.*.</summary>
		private string OnBackups {
			get { return "`"+GrantServer.EscapeForGrant(Db)+"backup\\_%`.*"; }
		}

		///<summary>A database the root account makes for the refusals: the account may neither read nor drop it.</summary>
		private string DbOther {
			get { return Db+"_other"; }
		}

		///<summary>Names the account tries to create and must not: outside the backup pattern. DbBackupX would fall under the pattern if its
		///'_' were not escaped (the guide's note on wildcards).</summary>
		private string DbNew {
			get { return Db+"_new"; }
		}

		private string DbBackupX {
			get { return Db+"backupx1"; }
		}

		private string DbBackupNoUnderscore {
			get { return Db+"backup"; }
		}

		///<summary>Every name this run may create apart from the backups, which match OnBackups.</summary>
		private List<string> GetNamesOfRun() {
			return new List<string> { Db,DbOther,DbNew,DbBackupX,DbBackupNoUnderscore };
		}

		private string GetReportPath() {
			if(!string.IsNullOrEmpty(_settings.ReportPath)) {
				return _settings.ReportPath;
			}
			return Path.Combine(Path.GetTempPath(),"dbgrants-report-"+_dateTimeStart.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture)+".txt");
		}
		#endregion Names

		///<summary>Runs everything, cleans up whatever happens, writes the report and returns its path.</summary>
		public string Run() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Preflight();//Throws before anything is created.
			try {
				SetUp();
				RunProgram();
				RunRefusals();
				if(_settings.DoRunChain) {
					RunChainAndAdoption();
				}
				else {
					Report.Section("Open Dental update chain and adoption");
					Report.Note("Skipped ("+GrantSettings.ENV_SKIP_CHAIN+"=1).");
				}
				RunOfficeDropAndCreate();
			}
			catch(Exception ex) {
				Report.Expect("The run finished",false,ex.GetType().Name+": "+ex.Message+" "+ex.StackTrace);
			}
			finally {
				CleanUp();
			}
			string path=GetReportPath();
			Report.Write(path,"Grants test of the program's database account (docs/db-connection-security.md section 5), "
				+_dateTimeStart.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture)+". Account '"+Account+"'@'localhost', office database "
				+Db+", server "+GrantServer.SERVER+":3306.");
			return path;
		}

		#region Preflight and set-up
		///<summary>Refuses to start when the account or any database of the run exists already, or when the PUBLIC role holds rights on one of
		///the names (they would hide the refusals). Records the server's version and settings.</summary>
		private void Preflight() {
			Report.Section("Server");
			using(MySqlConnection mySqlConnection=GrantServer.OpenRoot()) {
				Report.Note("Version "+GrantServer.Scalar(mySqlConnection,"SELECT VERSION()")
					+"; global sql_mode '"+GrantServer.Scalar(mySqlConnection,"SELECT @@GLOBAL.sql_mode")+"'"
					+"; log_bin "+GrantServer.Scalar(mySqlConnection,"SELECT @@GLOBAL.log_bin")
					+"; skip_name_resolve "+GrantServer.Scalar(mySqlConnection,"SELECT @@GLOBAL.skip_name_resolve")
					+"; default_storage_engine "+GrantServer.Scalar(mySqlConnection,"SELECT @@GLOBAL.default_storage_engine")
					+"; local_infile "+GrantServer.Scalar(mySqlConnection,"SELECT @@GLOBAL.local_infile")
					+"; secure_file_priv "+(GrantServer.Scalar(mySqlConnection,"SELECT @@GLOBAL.secure_file_priv")??"NULL")+".");
				string countUsers=GrantServer.Scalar(mySqlConnection,"SELECT COUNT(*) FROM mysql.user WHERE User='"+GrantServer.EscapeString(Account)+"'");
				if(countUsers!="0") {
					throw new GrantPreflightException("An account named "+Account+" exists already on this server. The test only creates and drops an "
						+"account of its own: choose another name in "+GrantSettings.ENV_ACCOUNT+", or drop that account yourself if it is a leftover.");
				}
				List<string> listExisting=GetDatabasesOfRun(mySqlConnection);
				if(listExisting.Count>0) {
					throw new GrantPreflightException("These databases exist already: "+string.Join(", ",listExisting)+". The test only creates and drops "
						+"databases of its own: choose another name in "+GrantSettings.ENV_DATABASE+", or drop them yourself if they are leftovers.");
				}
				List<string> listPublic=GetPublicGrants(mySqlConnection);
				Report.Note("PUBLIC role: "+(listPublic==null ? "none (a server before 10.11)" : (listPublic.Count==0 ? "no grants"
					: string.Join(" / ",listPublic))));
				List<string> listNames=GetNamesOfRun();
				listNames.Add(Db+"backup_"+DateTime.Today.ToString("MM_dd_yyyy",CultureInfo.InvariantCulture));
				foreach(string pattern in GrantServer.GetDatabasePatterns(listPublic??new List<string>())) {
					string nameCovered=listNames.FirstOrDefault(x => GrantServer.IsMatchGrantPattern(pattern,x));
					if(nameCovered!=null) {
						throw new GrantPreflightException("The PUBLIC role holds rights on `"+pattern+"`.*, which covers "+nameCovered+": every account "
							+"has them, so the refusals this test is for would not show. Choose another name in "+GrantSettings.ENV_DATABASE+".");
					}
				}
				_serverStateBefore=GetServerState(mySqlConnection);
			}
		}

		///<summary>The run's databases that exist now: its names and every backup of the office database.</summary>
		private List<string> GetDatabasesOfRun(MySqlConnection mySqlConnection) {
			string names=string.Join(",",GetNamesOfRun().Select(x => "'"+GrantServer.EscapeString(x)+"'"));
			return GrantServer.Column(mySqlConnection,"SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME IN ("+names+") "
				+"OR SCHEMA_NAME LIKE '"+GrantServer.EscapeForLike(Db+"backup_")+"%' ORDER BY SCHEMA_NAME");
		}

		///<summary>SHOW GRANTS FOR PUBLIC, or null on a server without the role.</summary>
		private static List<string> GetPublicGrants(MySqlConnection mySqlConnection) {
			try {
				return GrantServer.Column(mySqlConnection,"SHOW GRANTS FOR PUBLIC");
			}
			catch(MySqlException) {
				return null;
			}
		}

		///<summary>What the run must leave as it found it: the accounts and roles with their grants, and the global variables that matter here.
		///The run's own account is left out.</summary>
		private string GetServerState(MySqlConnection mySqlConnection) {
			StringBuilder stringBuilder=new StringBuilder();
			foreach(string variable in new[] { "sql_mode","require_secure_transport","local_infile","log_bin","secure_file_priv","max_allowed_packet",
				"default_storage_engine","skip_name_resolve" })
			{
				stringBuilder.AppendLine(variable+"="+GrantServer.Scalar(mySqlConnection,"SELECT @@GLOBAL."+variable));
			}
			List<string> listUsers=GrantServer.Column(mySqlConnection,"SELECT CONCAT(QUOTE(User),'@',QUOTE(Host)) FROM mysql.user "
				+"WHERE NOT (User='"+GrantServer.EscapeString(Account)+"' AND Host='localhost') ORDER BY User,Host");
			foreach(string user in listUsers) {
				stringBuilder.AppendLine(user);
				List<string> listGrants;
				try {
					listGrants=GrantServer.Column(mySqlConnection,"SHOW GRANTS FOR "+(user=="'PUBLIC'@''" ? "PUBLIC" : user));
				}
				catch(MySqlException ex) {
					listGrants=new List<string> { "(SHOW GRANTS: "+ex.Number+")" };
				}
				foreach(string grant in listGrants) {
					stringBuilder.AppendLine("  "+grant);
				}
			}
			return stringBuilder.ToString();
		}

		///<summary>As root: the office database from db/init/new-database.sql with the routine maintenance and inventory steps made pending again
		///(their tables dropped and their levels removed from ForkMigrationsApplied), a supply with stock for the stock ledger's opening balance,
		///the other database, then the account with exactly the guide's grants.</summary>
		private void SetUp() {
			Report.Section("Set-up (as root)");
			GrantServer.ExecRoot("CREATE DATABASE `"+Db+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			LoadNewDatabase();
			List<long> listLevelsKept=ForkMigrationRunner.GetSteps().Select(x => x.Level).Where(x => x<LEVEL_FIRST_PENDING).OrderBy(x => x).ToList();
			List<string> listForkTables=ForkTables.GetTableNames();
			using(MySqlConnection mySqlConnection=GrantServer.OpenRoot(Db)) {
				foreach(string table in listForkTables) {
					GrantServer.Exec(mySqlConnection,"DROP TABLE IF EXISTS `"+table+"`");
				}
				GrantServer.Exec(mySqlConnection,"UPDATE preference SET ValueString='"+string.Join(",",listLevelsKept)+"' "
					+"WHERE PrefName='"+ForkMigrationRunner.PrefNameApplied+"'");
				GrantServer.Exec(mySqlConnection,"UPDATE preference SET ValueString='"+listLevelsKept.Max()+"' "
					+"WHERE PrefName='"+ForkMigrationRunner.PrefNameSchemaLevel+"'");
				GrantServer.Exec(mySqlConnection,"INSERT INTO supply (Descript,LevelOnHand,Price) VALUES ('Synthetic gloves',5,2.5)");
			}
			Report.Note("Office database "+Db+" from db/init/new-database.sql; fork tables dropped and levels from "+LEVEL_FIRST_PENDING
				+" removed from ForkMigrationsApplied, so the start-up runs them: "+string.Join(", ",listForkTables)+".");
			GrantServer.ExecRoot("CREATE DATABASE `"+DbOther+"` CHARACTER SET utf8mb3",
				"CREATE TABLE `"+DbOther+"`.`keep` (KeepNum bigint NOT NULL PRIMARY KEY) ENGINE=MyISAM",
				"INSERT INTO `"+DbOther+"`.`keep` VALUES (1)");
			GrantServer.ExecRoot("CREATE USER "+AccountSql+" IDENTIFIED BY '"+_password+"'");
			_isAccountCreated=true;
			//Exactly the guide's grants (section 5), for <database> = Db.
			GrantServer.ExecRoot(
				"GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, ALTER, CREATE TEMPORARY TABLES ON "+OnOffice+" TO "+AccountSql,
				"GRANT SELECT, INSERT, CREATE, ALTER ON "+OnBackups+" TO "+AccountSql,
				"GRANT RELOAD, BINLOG MONITOR, REPLICA MONITOR ON *.* TO "+AccountSql);
			foreach(string grant in GetAccountGrants()) {
				Report.Note("SHOW GRANTS: "+grant);
			}
		}

		///<summary>SHOW GRANTS of the account, without its password hash.</summary>
		private List<string> GetAccountGrants() {
			return GrantServer.ColumnRoot("SHOW GRANTS FOR "+AccountSql).Select(x => GrantServer.WithoutPasswordHash(x)).ToList();
		}

		///<summary>Runs db/init/new-database.sql statement by statement as root (every statement ends with a semicolon at the end of a line).</summary>
		private void LoadNewDatabase() {
			string text=File.ReadAllText(FindRepoFile(Path.Combine("db","init","new-database.sql")),new UTF8Encoding(false)).Replace("\r\n","\n");
			using(MySqlConnection mySqlConnection=GrantServer.OpenRoot(Db)) {
				foreach(string statement in text.Split(new[] { ";\n" },StringSplitOptions.None)) {
					if(statement.Trim()=="") {
						continue;
					}
					GrantServer.Exec(mySqlConnection,statement);
				}
			}
		}

		///<summary>Walks up from the test assembly's folder to the repository root and returns the path of a file in it.</summary>
		public static string FindRepoFile(string relativePath) {
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null) {
				string candidate=Path.Combine(directoryInfo.FullName,relativePath);
				if(File.Exists(candidate)) {
					return candidate;
				}
				directoryInfo=directoryInfo.Parent;
			}
			throw new FileNotFoundException("Not found above the test folder: "+relativePath);
		}
		#endregion Preflight and set-up

		#region Connections
		///<summary>Points the program's data layer at the office database as the account, the way the program connects at start-up
		///(DataConnection.SetDb, whose connection test writes a preference).</summary>
		private void ConnectProgram() {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(GrantServer.SERVER,Db,Account,_password,"","",DatabaseType.MySql);
		}

		///<summary>Runs statements as the account on a connection of its own (default database: the office database).</summary>
		private void ExecAccount(string database,params string[] arrayCommands) {
			using(MySqlConnection mySqlConnection=GrantServer.Open(database??Db,Account,_password)) {
				foreach(string command in arrayCommands) {
					GrantServer.Exec(mySqlConnection,command);
				}
			}
		}

		private string ScalarAccount(string command,string database=null) {
			using(MySqlConnection mySqlConnection=GrantServer.Open(database??Db,Account,_password)) {
				return GrantServer.Scalar(mySqlConnection,command);
			}
		}

		private List<string> ColumnAccount(string command,string database=null) {
			using(MySqlConnection mySqlConnection=GrantServer.Open(database??Db,Account,_password)) {
				return GrantServer.Column(mySqlConnection,command);
			}
		}

		///<summary>Closes every pooled connection, so the next one is new: MariaDB applies a changed database or global right to new connections.
		///</summary>
		private static void ClearPools() {
			MySqlConnection.ClearAllPools();
		}

		///<summary>Revokes one right as root, runs body, and grants the right again whatever happens.</summary>
		private void WithoutRight(string right,string on,Action body) {
			GrantServer.ExecRoot("REVOKE "+right+" ON "+on+" FROM "+AccountSql);
			ClearPools();
			try {
				body();
			}
			finally {
				GrantServer.ExecRoot("GRANT "+right+" ON "+on+" TO "+AccountSql);
				ClearPools();
			}
		}

		///<summary>The backups of the office database that exist now.</summary>
		private List<string> GetBackupDatabases() {
			return GrantServer.ColumnRoot("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE '"
				+GrantServer.EscapeForLike(Db+"backup_")+"%' ORDER BY SCHEMA_NAME");
		}

		private string NewTempFolder(string purpose) {
			string folder=Path.Combine(Path.GetTempPath(),"dbgrants-"+purpose+"-"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			_listTempPaths.Add(folder);
			return folder;
		}
		#endregion Connections

		#region Clean-up
		///<summary>As root: drops the account and every database of the run, checks that none is left, and that every other account, the PUBLIC
		///role and the global variables are as they were. Deletes the run's temporary files.</summary>
		private void CleanUp() {
			Report.Section("Clean-up (as root)");
			ClearPools();
			try {
				if(_isAccountCreated) {
					GrantServer.ExecRoot("DROP USER IF EXISTS "+AccountSql);
				}
				using(MySqlConnection mySqlConnection=GrantServer.OpenRoot()) {
					List<string> listDatabases=GetDatabasesOfRun(mySqlConnection);
					foreach(string database in listDatabases) {
						GrantServer.Exec(mySqlConnection,"DROP DATABASE IF EXISTS `"+database+"`");
					}
					Report.Note("Dropped the account and "+listDatabases.Count+" database(s): "+string.Join(", ",listDatabases)+".");
					Report.Expect("No account "+Account+" is left",
						GrantServer.Scalar(mySqlConnection,"SELECT COUNT(*) FROM mysql.user WHERE User='"+GrantServer.EscapeString(Account)+"'")=="0");
					List<string> listLeft=GetDatabasesOfRun(mySqlConnection);
					Report.Expect("No database of the run is left",listLeft.Count==0,string.Join(", ",listLeft));
					string serverStateAfter=GetServerState(mySqlConnection);
					Report.Expect("Every other account, the PUBLIC role and the global variables are as they were",serverStateAfter==_serverStateBefore,
						serverStateAfter==_serverStateBefore ? "" : "before: "+_serverStateBefore+" after: "+serverStateAfter);
				}
			}
			catch(Exception ex) {
				Report.Expect("Clean-up finished",false,ex.GetType().Name+": "+ex.Message);
			}
			foreach(string path in _listTempPaths) {
				try {
					if(Directory.Exists(path)) {
						Directory.Delete(path,true);
					}
					else if(File.Exists(path)) {
						File.Delete(path);
					}
				}
				catch(Exception) {
					//A temporary file still in use: %TEMP% keeps it.
				}
			}
			//DataConnection.BulkCopy (the backup to another server before 2026-11-07) wrote temp_<table><ticks>.txt into the working folder and left it
			//when the load was refused. Kept for runs of an older build.
			foreach(string path in Directory.GetFiles(Directory.GetCurrentDirectory(),"temp_*.txt")) {
				if(File.GetCreationTime(path)>=_dateTimeStart.AddMinutes(-1)) {
					try {
						File.Delete(path);
					}
					catch(Exception) {
					}
				}
			}
		}
		#endregion Clean-up
	}
}
