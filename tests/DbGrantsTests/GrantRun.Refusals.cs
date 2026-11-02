// The manual grants test, part 3: every right the guide grants is needed (the program's call is refused without it and allowed again with
// it), and what the guide does not grant is refused.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DataConnectionBase;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace DbGrantsTests {
	public partial class GrantRun {
		private void RunRefusals() {
			RunOfficeRights();
			RunBackupRights();
			RunServerRights();
			RunIndexRight();
			RunBeyondTheGuide();
			Report.Section("Grants after the refusals");
			List<string> listGrants=GetAccountGrants();
			Report.Expect("The account has exactly the guide's three grants again",listGrants.Count==3
				&& listGrants.Any(x => x.Contains("GRANT RELOAD, BINLOG MONITOR, SLAVE MONITOR ON *.*"))
				&& listGrants.Any(x => x.Contains("GRANT SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, ALTER, CREATE TEMPORARY TABLES ON "+OnOffice))
				&& listGrants.Any(x => x.Contains("GRANT SELECT, INSERT, CREATE, ALTER ON "+OnBackups)),string.Join(" / ",listGrants));
		}

		#region Office database
		///<summary>Each right on the office database, revoked alone: the program's calls that need it are refused, and allowed again once it is
		///granted back.</summary>
		private void RunOfficeRights() {
			ForkMigrationContext context=new ForkMigrationContext(Db);
			Report.Section("Office database: SELECT");
			WithoutRight("SELECT",OnOffice,() => {
				Report.Refused("Prefs.RefreshCache (SELECT * FROM preference)","SELECT",() => Prefs.RefreshCache());
				Report.Refused("Patients.GetPat","SELECT",() => Patients.GetPat(_patient.PatNum));
			});
			Report.Allowed("Prefs.RefreshCache with SELECT again",() => Prefs.RefreshCache());
			Report.Section("Office database: INSERT");
			WithoutRight("INSERT",OnOffice,() => {
				Report.Refused("Commlogs.Insert","INSERT",() => Commlogs.Insert(NewCommlog()));
				Report.Refused("ForkMigrationContext.AddColumnIfMissing (ALTER TABLE: MariaDB checks INSERT and CREATE on the database)","INSERT",
					() => context.AddColumnIfMissing(TABLE_PROBE,"NoInsert","int NOT NULL"));
				Report.Refused("DatabaseMaintenances.OptimizeTable (OPTIMIZE TABLE needs SELECT and INSERT)","INSERT",
					() => DatabaseMaintenances.OptimizeTable(TABLE_PROBE,forceOptimize:true));
			});
			Report.Allowed("Commlogs.Insert with INSERT again",() => Commlogs.Insert(NewCommlog()));
			Report.Section("Office database: UPDATE");
			WithoutRight("UPDATE",OnOffice,() => {
				Report.Refused("DataConnection.SetDb: the start-up's connection test (UPDATE preference): the program cannot start","UPDATE",
					ConnectProgram);
				Report.Refused("Prefs.UpdateString","UPDATE",() => Prefs.UpdateString(PrefName.PracticeTitle,"Synthetic Dental Office, no update"));
			});
			Report.Allowed("DataConnection.SetDb with UPDATE again",ConnectProgram);
			Report.Section("Office database: DELETE");
			WithoutRight("DELETE",OnOffice,() => {
				Report.Refused("Commlogs.Delete","DELETE",() => Commlogs.Delete(_commlog));
			});
			Report.Allowed("Commlogs.Delete with DELETE again",() => Commlogs.Delete(_commlog));
			Report.Section("Office database: CREATE");
			WithoutRight("CREATE",OnOffice,() => {
				Report.Refused("ForkMigrationContext.CreateTableIfMissing (CREATE TABLE)","CREATE",
					() => new ForkMigrationContext(Db).CreateTableIfMissing(CREATE_PROBE.Replace("`grantprobe`","`grantprobetwo`")));
				Report.Refused("ForkMigrationContext.AddColumnIfMissing (ALTER TABLE: MariaDB checks INSERT and CREATE on the database)","CREATE",
					() => context.AddColumnIfMissing(TABLE_PROBE,"NoCreate","int NOT NULL"));
				Report.Refused("EhrMeasures.GetTable(TimelyAccess): CREATE TABLE of a work table","CREATE",
					() => EhrMeasures.GetTable(EhrMeasureType.TimelyAccess,DateTime.Today.AddYears(-1),DateTime.Today,
						PrefC.GetLong(PrefName.PracticeDefaultProv)));
			});
			Report.Allowed("ForkMigrationContext.AddColumnIfMissing with CREATE again",
				() => new ForkMigrationContext(Db).AddColumnIfMissing(TABLE_PROBE,"WithCreate","int NOT NULL"));
			Report.Section("Office database: DROP");
			WithoutRight("DROP",OnOffice,() => {
				Report.Refused("DashboardARs.Truncate (TRUNCATE needs DROP)","DROP",DashboardARs.Truncate);
				Report.Refused("Ledgers.ComputeAging for two families (TRUNCATE TABLE famaging)","DROP",
					() => Ledgers.ComputeAging(new List<long> { _patient.PatNum,_patient2.PatNum },DateTime.Today));
				Report.Refused("DatabaseMaintenances.ClearOldEtransMessageText (DROP TABLE IF EXISTS of its work table)","DROP",
					DatabaseMaintenances.ClearOldEtransMessageText);
				Report.Refused("LargeTableHelper.AlterTable (DROP TABLE IF EXISTS and RENAME TABLE)","DROP",
					() => LargeTableHelper.AlterTable(TABLE_PROBE,"GrantProbeNum",new LargeTableHelper.ColNameAndDef("NoDrop","int NOT NULL")));
			});
			Report.Allowed("DashboardARs.Truncate with DROP again",DashboardARs.Truncate);
			Report.Expect("The large-table helper left the table as it was",ScalarAccount("SELECT COUNT(*) FROM grantprobe")=="3");
			Report.Section("Office database: ALTER");
			WithoutRight("ALTER",OnOffice,() => {
				Report.Refused("ForkMigrationContext.AddColumnIfMissing (ALTER TABLE ADD COLUMN)","ALTER",
					() => new ForkMigrationContext(Db).AddColumnIfMissing(TABLE_PROBE,"NoAlter","int NOT NULL"));
				Report.Refused("ForkMigrationContext.EnsureIndex (ALTER TABLE ADD INDEX)","ALTER",
					() => new ForkMigrationContext(Db).EnsureIndex(TABLE_PROBE,"NoAlter","`Amount`"));
				Report.Refused("MiscData.EnableIndexesIfNeeded (ALTER TABLE ENABLE KEYS)","ALTER",
					() => MiscData.EnableIndexesIfNeeded(new List<string> { TABLE_PROBE }));
			});
			Report.Allowed("MiscData.EnableIndexesIfNeeded with ALTER again",() => MiscData.EnableIndexesIfNeeded(new List<string> { TABLE_PROBE }));
			Report.Section("Office database: CREATE TEMPORARY TABLES");
			string userQuery="CREATE TEMPORARY TABLE tmpgrant SELECT PatNum FROM patient; SELECT COUNT(*) FROM tmpgrant; "
				+"DROP TEMPORARY TABLE IF EXISTS tmpgrant;";
			WithoutRight("CREATE TEMPORARY TABLES",OnOffice,() => {
				Report.Refused("A user query with CREATE TEMPORARY TABLE (DataConnectionCancelable)","CREATE TEMPORARY TABLES",
					() => RunUserQuery(userQuery));
			});
			Report.Allowed("The same user query with CREATE TEMPORARY TABLES again",() => RunUserQuery(userQuery));
		}

		private static void RunUserQuery(string command) {
			int serverThread=DataConnectionCancelable.GetServerThread(false,false);
			DataConnectionCancelable.GetTableConAlreadyOpen(serverThread,command,wasSqlValidated:false,suppressMessage:true);
		}
		#endregion Office database

		#region Backup databases
		///<summary>Each right of the backup grant, revoked alone, against the backups the program makes.</summary>
		private void RunBackupRights() {
			Report.Section("Backup databases: CREATE");
			WithoutRight("CREATE",OnBackups,() => {
				Report.Refused("MiscData.MakeABackup() (CREATE DATABASE of the backup)","CREATE",() => MiscData.MakeABackup());
			});
			Report.Section("Backup databases: INSERT");
			WithoutRight("INSERT",OnBackups,() => {
				Report.Refused("MiscData.MakeABackup() (INSERT INTO <backup>.<table> SELECT * FROM <database>.<table>)","INSERT",
					() => MiscData.MakeABackup());
			});
			Report.Section("Backup databases: ALTER");
			WithoutRight("ALTER",OnBackups,() => {
				Report.Refused("MiscData.MakeABackup() with BackupIndexesDisabled on, the default (ALTER TABLE ... DISABLE KEYS)","ALTER",
					() => MiscData.MakeABackup());
				Report.Allowed("MiscData.MakeABackup() with BackupIndexesDisabled off: no ALTER needed",() => {
					Prefs.UpdateBool(PrefName.BackupIndexesDisabled,false);
					Cache.Refresh(InvalidType.Prefs);
					try {
						MiscData.MakeABackup();
					}
					finally {
						Prefs.UpdateBool(PrefName.BackupIndexesDisabled,true);
						Cache.Refresh(InvalidType.Prefs);
					}
				});
			});
			Report.Section("Backup databases: SELECT");
			WithoutRight("SELECT",OnBackups,() => {
				Report.Allowed("MiscData.MakeABackup(): no SELECT on the backup needed",() => MiscData.MakeABackup());
				Report.Refused("MiscData.MakeABackup(doVerify:true), the archive tab's (SELECT COUNT(*) of each backup table)","SELECT",
					() => MiscData.MakeABackup(serverName:GrantServer.SERVER,user:Account,pass:_password,doVerify:true));
			});
		}
		#endregion Backup databases

		#region Server-wide rights
		///<summary>Each server-wide right, revoked alone.</summary>
		private void RunServerRights() {
			Report.Section("Server-wide: RELOAD");
			WithoutRight("RELOAD","*.*",() => {
				Report.Refused("MiscData.MakeABackup() (FLUSH TABLE <backup>.<table>)","RELOAD",() => MiscData.MakeABackup());
				Report.Refused("MiscData.FlushTables (the Backup window's FLUSH TABLES)","RELOAD",MiscData.FlushTables);
			});
			Report.Section("Server-wide: BINLOG MONITOR");
			WithoutRight("BINLOG MONITOR","*.*",() => {
				Report.Refused("MiscData.MakeABackup() (SHOW MASTER STATUS in ReplicationServers.IsUsingReplication)","BINLOG MONITOR",
					() => MiscData.MakeABackup());
				Report.Refused("ReplicationServers.IsUsingReplication (the maintenance window's check)","BINLOG MONITOR",
					() => ReplicationServers.IsUsingReplication());
			});
			Report.Section("Server-wide: REPLICA MONITOR");
			WithoutRight("REPLICA MONITOR","*.*",() => {
				Report.Refused("MiscData.MakeABackup() (SHOW SLAVE STATUS in ReplicationServers.IsUsingReplication)","REPLICA MONITOR",
					() => MiscData.MakeABackup());
				Report.Refused("ReplicationServers.GetSlaveStatus","REPLICA MONITOR",() => ReplicationServers.GetSlaveStatus());
			});
			Report.Allowed("MiscData.MakeABackup() with all three again",() => MiscData.MakeABackup());
		}
		#endregion Server-wide rights

		#region INDEX
		///<summary>INDEX is not granted: CREATE INDEX and DROP INDEX are refused, as Open Dental's 17.4.46 step runs it (ConvertDatabases.To17_4_46,
		///invoked as the chain invokes it). Granted for a first start as the guide says, they are allowed; revoked again, refused again.</summary>
		private void RunIndexRight() {
			Report.Section("INDEX (not granted)");
			MethodInfo methodInfo17_4_46=typeof(ConvertDatabases).GetMethod("To17_4_46",BindingFlags.NonPublic|BindingFlags.Static);
			Report.Expect("Open Dental's 17.4.46 step is found",methodInfo17_4_46!=null);
			if(methodInfo17_4_46!=null) {
				Report.Refused("ConvertDatabases.To17_4_46: CREATE INDEX MsgBoxCompound ON emailmessage","INDEX",
					() => methodInfo17_4_46.Invoke(null,new object[0]));
			}
			Report.Refused("CREATE INDEX on an office table","INDEX",() => DataCore.NonQ("CREATE INDEX GrantIdx ON grantprobe (Amount)"));
			Report.Refused("DROP INDEX ... ON an office table (17.4.53's form)","INDEX",() => DataCore.NonQ("DROP INDEX Amount ON grantprobe"));
			GrantServer.ExecRoot("GRANT INDEX ON "+OnOffice+" TO "+AccountSql);
			ClearPools();
			try {
				Report.Allowed("With INDEX granted for a first start (the guide): CREATE INDEX",
					() => DataCore.NonQ("CREATE INDEX GrantIdx ON grantprobe (Amount)"));
				Report.Allowed("With INDEX granted for a first start (the guide): DROP INDEX ... ON",
					() => DataCore.NonQ("DROP INDEX GrantIdx ON grantprobe"));
			}
			finally {
				GrantServer.ExecRoot("REVOKE INDEX ON "+OnOffice+" FROM "+AccountSql);
				ClearPools();
			}
			Report.Refused("CREATE INDEX after INDEX is revoked again","INDEX",() => DataCore.NonQ("CREATE INDEX GrantIdx ON grantprobe (Amount)"));
		}
		#endregion INDEX

		#region Beyond the guide
		///<summary>Statements that need a right the guide does not grant at all: other databases, the backups beyond CREATE, INSERT, SELECT and
		///ALTER, the mysql database, FILE, GRANT, CREATE USER, SUPER and the admin rights, other accounts' connections, views, routines,
		///triggers, events and LOCK TABLES.</summary>
		private void RunBeyondTheGuide() {
			Report.Section("Databases other than the guide's");
			Report.Refused("CREATE DATABASE "+DbNew+" (outside the backup pattern)","CREATE on that name",
				() => ExecAccount(null,"CREATE DATABASE `"+DbNew+"`"));
			Report.Refused("CREATE DATABASE "+DbBackupX+" (would match the backup pattern if its '_' were not escaped)","CREATE on that name",
				() => ExecAccount(null,"CREATE DATABASE `"+DbBackupX+"`"));
			Report.Refused("CREATE DATABASE "+DbBackupNoUnderscore+" (no '_' after backup)","CREATE on that name",
				() => ExecAccount(null,"CREATE DATABASE `"+DbBackupNoUnderscore+"`"));
			Report.Refused("DROP DATABASE "+DbOther+" (another database)","DROP on it",() => ExecAccount(null,"DROP DATABASE `"+DbOther+"`"));
			Report.Refused("SELECT from "+DbOther+".keep","SELECT on it",() => ScalarAccount("SELECT COUNT(*) FROM `"+DbOther+"`.`keep`"));
			Report.Refused("USE "+DbOther,"any right on it",() => ExecAccount(null,"USE `"+DbOther+"`"));
			//A complete backup made after the table of the run (the start-up's backup came before it; a refused backup is partial).
			string backup=GrantServer.ScalarRoot("SELECT TABLE_SCHEMA FROM information_schema.TABLES WHERE TABLE_NAME='grantprobe' "
				+"AND TABLE_SCHEMA LIKE '"+GrantServer.EscapeForLike(Db+"backup_")+"%' AND TABLE_SCHEMA IN (SELECT TABLE_SCHEMA FROM "
				+"information_schema.TABLES WHERE TABLE_NAME='zipcode') ORDER BY TABLE_SCHEMA LIMIT 1");
			Report.Expect("A complete backup with the table of the run exists",backup!=null);
			if(backup!=null) {
				Report.Section("A backup database ("+backup+")");
				Report.Allowed("SELECT from a backup table",() => ScalarAccount("SELECT COUNT(*) FROM `"+backup+"`.`preference`"));
				Report.Allowed("INSERT into a backup table",() => ExecAccount(null,"INSERT INTO `"+backup+"`.`grantprobe` (Descript,Amount) VALUES ('x',9)"));
				Report.Allowed("ALTER TABLE of a backup table, DROP COLUMN included (the guide: ALTER allows it)",
					() => ExecAccount(null,"ALTER TABLE `"+backup+"`.`grantprobe` DROP COLUMN `Descript`"));
				Report.Allowed("CREATE TABLE in a backup",() => ExecAccount(null,"CREATE TABLE `"+backup+"`.`grantnew` (Id int NOT NULL)"));
				Report.Refused("UPDATE a backup table","UPDATE",() => ExecAccount(null,"UPDATE `"+backup+"`.`preference` SET Comments='x'"));
				Report.Refused("DELETE FROM a backup table","DELETE",() => ExecAccount(null,"DELETE FROM `"+backup+"`.`grantprobe`"));
				Report.Refused("TRUNCATE a backup table","DROP",() => ExecAccount(null,"TRUNCATE TABLE `"+backup+"`.`grantprobe`"));
				Report.Refused("DROP TABLE of a backup table","DROP",() => ExecAccount(null,"DROP TABLE `"+backup+"`.`grantprobe`"));
				Report.Refused("RENAME TABLE in a backup","DROP",
					() => ExecAccount(null,"RENAME TABLE `"+backup+"`.`grantprobe` TO `"+backup+"`.`grantprobe2`"));
				Report.Refused("ALTER TABLE ... RENAME in a backup","DROP",
					() => ExecAccount(null,"ALTER TABLE `"+backup+"`.`grantprobe` RENAME TO `"+backup+"`.`grantprobe2`"));
				Report.Refused("DROP DATABASE of a backup","DROP",() => ExecAccount(null,"DROP DATABASE `"+backup+"`"));
			}
			Report.Section("The mysql database, FILE, GRANT and administration");
			Report.Refused("SELECT from mysql.user","SELECT on mysql",() => ScalarAccount("SELECT COUNT(*) FROM mysql.user"));
			Report.Refused("SELECT from mysql.db","SELECT on mysql",() => ScalarAccount("SELECT COUNT(*) FROM mysql.db"));
			Report.Refused("SHOW GRANTS FOR root@localhost","SELECT on mysql",() => ColumnAccount("SHOW GRANTS FOR 'root'@'localhost'"));
			string fileOut=Path.Combine(Path.GetTempPath(),"dbgrants-outfile-"+Guid.NewGuid().ToString("N")+".txt");
			_listTempPaths.Add(fileOut);
			Report.Refused("SELECT ... INTO OUTFILE","FILE",
				() => ExecAccount(null,"SELECT PrefName FROM preference INTO OUTFILE '"+GrantServer.EscapeString(fileOut.Replace('\\','/'))+"'"));
			Report.Expect("No file was written",!File.Exists(fileOut));
			string fileIn=Path.Combine(NewTempFolder("infile"),"in.txt");
			File.WriteAllText(fileIn,"1\tsynthetic\t1\n");
			Report.Refused("LOAD DATA INFILE (on the server)","FILE",() => ExecAccount(null,"LOAD DATA INFILE '"
				+GrantServer.EscapeString(fileIn.Replace('\\','/'))+"' INTO TABLE grantprobe"));
			string loadFile=ScalarAccount("SELECT LOAD_FILE('"+GrantServer.EscapeString(fileIn.Replace('\\','/'))+"')");
			Report.Expect("LOAD_FILE returns NULL without FILE (no error, no data)",loadFile==null);
			Report.Refused("GRANT SELECT on the office database to the account itself","GRANT OPTION",
				() => ExecAccount(null,"GRANT SELECT ON "+OnOffice+" TO "+AccountSql));
			Report.Refused("CREATE USER","CREATE USER",() => ExecAccount(null,"CREATE USER 'opmsd_grantnobody'@'localhost'"));
			string sqlModeNow=GrantServer.ScalarRoot("SELECT @@GLOBAL.sql_mode");
			Report.Refused("SET GLOBAL sql_mode (to its current value, so nothing would change)","SUPER",
				() => ExecAccount(null,"SET GLOBAL sql_mode='"+GrantServer.EscapeString(sqlModeNow)+"'"));
			Report.Refused("SET SESSION sql_log_bin=0 (the backup on a replicating server)","BINLOG ADMIN",
				() => ExecAccount(null,"SET SESSION sql_log_bin=0"));
			Report.Refused("STOP SLAVE (the replication set-up window)","REPLICATION SLAVE ADMIN",() => ExecAccount(null,"STOP SLAVE"));
			using(MySqlConnection mySqlConnectionRoot=GrantServer.OpenRoot()) {
				string threadRoot=GrantServer.Scalar(mySqlConnectionRoot,"SELECT CONNECTION_ID()");
				Report.Refused("KILL QUERY of another account's connection","CONNECTION ADMIN",() => ExecAccount(null,"KILL QUERY "+threadRoot));
				List<string> listUsers=ColumnAccount("SELECT DISTINCT USER FROM information_schema.PROCESSLIST");
				Report.Expect("information_schema.PROCESSLIST shows the account's own connections only (no PROCESS)",
					listUsers.All(x => x==Account),string.Join(", ",listUsers));
			}
			Report.Section("Views, routines, triggers, events, locks");
			Report.Refused("LOCK TABLES","LOCK TABLES",() => ExecAccount(null,"LOCK TABLES grantprobe READ"));
			Report.Refused("CREATE VIEW","CREATE VIEW",() => ExecAccount(null,"CREATE VIEW grantview AS SELECT 1 AS One"));
			Report.Refused("CREATE PROCEDURE","CREATE ROUTINE",() => ExecAccount(null,"CREATE PROCEDURE grantproc() SELECT 1"));
			Report.Refused("CREATE TRIGGER","TRIGGER",
				() => ExecAccount(null,"CREATE TRIGGER granttrigger BEFORE INSERT ON grantprobe FOR EACH ROW SET NEW.Amount=NEW.Amount"));
			Report.Refused("CREATE EVENT","EVENT",() => ExecAccount(null,"CREATE EVENT grantevent ON SCHEDULE AT CURRENT_TIMESTAMP + INTERVAL 1 DAY DO SELECT 1"));
		}
		#endregion Beyond the guide
	}
}
