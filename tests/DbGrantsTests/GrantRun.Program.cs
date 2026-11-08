// The manual grants test, part 2: the program's own code as the account, with exactly the guide's grants. Every call here must be allowed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using DataConnectionBase;
using MySqlConnector;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace DbGrantsTests {
	public partial class GrantRun {
		///<summary>A table of the run in the office database, made by the account through the fork migration helpers.</summary>
		private const string TABLE_PROBE="grantprobe";
		private const string CREATE_PROBE="CREATE TABLE `grantprobe` (\n"
			+"  `GrantProbeNum` bigint(20) NOT NULL AUTO_INCREMENT,\n"
			+"  `Descript` varchar(255) NOT NULL,\n"
			+"  `Amount` double NOT NULL,\n"
			+"  PRIMARY KEY (`GrantProbeNum`)\n"
			+") ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci";
		private Patient _patient;
		private Patient _patient2;
		private Commlog _commlog;

		///<summary>Everything the program does with the guide's grants, in the order of a start-up and a working day.</summary>
		private void RunProgram() {
			RunIdentity();
			RunStartUp();
			RunReadsAndWrites();
			RunWorkTablesAndHelpers();
			RunMaintenance();
			RunBackups();
			RunReleaseChannel();
		}

		#region Identity
		private void RunIdentity() {
			Report.Section("The account");
			string currentUser=Report.Allowed("SELECT CURRENT_USER() over TCP to "+GrantServer.SERVER,() => ScalarAccount("SELECT CURRENT_USER()"));
			Report.Expect("The account '"+Account+"'@'localhost' matches a connection to "+GrantServer.SERVER,currentUser==Account+"@localhost",
				currentUser);
			List<string> listDatabases=Report.Allowed("SHOW DATABASES (no right needed; lists what the account may use)",
				() => ColumnAccount("SHOW DATABASES"));
			Report.Note("SHOW DATABASES as the account: "+string.Join(", ",listDatabases??new List<string>()));
			Report.Expect("SHOW DATABASES does not list the other database",listDatabases!=null && !listDatabases.Contains(DbOther));
		}
		#endregion Identity

		#region Start-up
		///<summary>FormOpenDental.PrefsStartup and what follows it, as far as it runs without a window: the connection test, the preference
		///cache, the fork version guard, the indexes of a restored backup, the MariaDB version check, sql_mode, the fork migration hand-off
		///(PrefL.RunForkMigrations: the backup Release builds make, the in-progress flags, the pending steps, the security log), the release
		///channel's decision, the computer's preferences, the caches and the log-on.</summary>
		private void RunStartUp() {
			Report.Section("Start-up");
			if(!Report.Allowed("DataConnection.SetDb: the connection test (UPDATE preference, DataConnection.cs TestConnection)",ConnectProgram)) {
				throw new ApplicationException("The account cannot connect; nothing else can run.");
			}
			Report.Allowed("Cache.Refresh(Prefs)",() => Cache.Refresh(InvalidType.Prefs));
			Report.Expect("PrefL.CheckForkVersionGuard (silent) lets the program start",
				Report.Allowed("PrefL.CheckForkVersionGuard (silent)",() => PrefL.CheckForkVersionGuard(null,true)));
			Report.Allowed("MiscData.GetTablesDisabledIndexes and EnableIndexesIfNeeded (Shared.EnableIndexesIfNeeded)",
				() => MiscData.EnableIndexesIfNeeded(MiscData.GetTablesDisabledIndexes()));
			Report.Expect("PrefL.CheckMySqlVersion (silent) lets the program start",
				Report.Allowed("PrefL.CheckMySqlVersion (silent): the version preference changes from 5.5",() => PrefL.CheckMySqlVersion(true)));
			string sqlMode=GrantServer.ScalarRoot("SELECT @@GLOBAL.sql_mode");
			if(sqlMode=="" || sqlMode.ToUpperInvariant()=="NO_AUTO_CREATE_USER") {
				Report.Allowed("MiscData.SetSqlMode: the server's sql_mode is '"+sqlMode+"', so the program does not set it",MiscData.SetSqlMode);
			}
			else {
				//The program stops here (FormOpenDental.PrefsStartup); the message names the option-file fix. The run goes on with the session's
				//own sql_mode, which the business layer does not set: later checks may fail for that reason, not for a right.
				Report.Refused("MiscData.SetSqlMode: the server's sql_mode is '"+sqlMode+"', SET GLOBAL sql_mode",
					"SUPER",MiscData.SetSqlMode);
				Report.Note("The program stops with: "+MiscData.GetSetSqlModeFailedMessage());
			}
			Report.Allowed("The update-in-progress preference",() => PrefC.GetStringSilent(PrefName.UpdateInProgressOnComputerName));
			RunForkMigrationHandOff();
			Report.Expect("PrefL.CheckForkVersionGuard (silent) after the steps",
				Report.Allowed("PrefL.CheckForkVersionGuard (silent), again",() => PrefL.CheckForkVersionGuard(null,true)));
			Report.Expect("PrefL.CheckProgramVersion (silent; a Debug build neither publishes nor pulls)",
				Report.Allowed("PrefL.CheckProgramVersion (silent)",() => PrefL.CheckProgramVersion(null,true,null,null)));
			Report.Allowed("ComputerPrefs.LocalComputer (inserts this computer's row)",() => ComputerPrefs.LocalComputer);
			Report.Allowed("Lans.RefreshCache, LanguageForeigns.RefreshCache",() => { Lans.RefreshCache(); LanguageForeigns.RefreshCache(); });
			Report.Allowed("Cache.Refresh(AllLocal): every cache the main window loads",() => Cache.Refresh(InvalidType.AllLocal));
			Report.Allowed("Log-on: Userods.GetUser(1) as Security.CurUser",() => { Security.CurUser=Userods.GetUser(1); });
			Report.Allowed("Computers.UpdateHeartBeat (start-up)",() => Computers.UpdateHeartBeat(Environment.MachineName,true));
			Report.Allowed("SecurityLogs.MakeLogEntry (log-on)",() => SecurityLogs.MakeLogEntry(EnumPermType.UserLogOnOff,0,"Grants test log-on"));
			Report.Allowed("Signalods.SetInvalid and the signal timer's read",() => {
				Signalods.SetInvalid(InvalidType.Prefs);
				Signalods.RefreshTimed(DateTime.Now.AddMinutes(-5));
			});
		}

		///<summary>PrefL.RunForkMigrations for an adopted database with pending steps, on the update server, step by step (PrefL.ConvertDB exits the
		///process when it fails, so the harness runs its parts): the guard, the backup a Release build makes first, the in-progress flags, the
		///steps, the flags again, the caches and the security log.</summary>
		private void RunForkMigrationHandOff() {
			ForkDbState forkDbState=Report.Allowed("ForkVersions.ReadState",() => ForkVersions.ReadState());
			if(forkDbState==null) {
				return;
			}
			Report.Expect("The office database is adopted (fork markers)",forkDbState.IsForkMarked);
			IForkMigrationRunner runner=ForkMigrationHandOff.Runner;
			ForkGuardResult forkGuardResult=Report.Allowed("ForkVersionGuard.Evaluate with the runner",() => ForkVersionGuard.Evaluate(forkDbState,runner));
			if(forkGuardResult==null) {
				return;
			}
			Report.Expect("The guard lets the program proceed with pending fork migration steps",forkGuardResult.CanProceed
				&& forkGuardResult.HasPendingMigrations,string.Join(", ",forkGuardResult.ListPendingLevels));
			Report.Expect("This computer is the update server (WebServiceServerName blank)",PrefL.IsUpdateServer());
			string backup=Report.Allowed("MiscData.MakeABackup() before the steps (Shared.MakeABackup in a Release build): the default backup",
				() => MiscData.MakeABackup());
			Report.Note("Backup made: "+backup);
			Report.Allowed("Prefs: UpdateInProgressOnComputerName and CorruptedDatabase set",() => {
				Prefs.UpdateString(PrefName.UpdateInProgressOnComputerName,Environment.MachineName);
				Prefs.UpdateBool(PrefName.CorruptedDatabase,true);
			});
			List<string> listLog=new List<string>();
			Report.Allowed("IForkMigrationRunner.RunPending: "+string.Join(", ",forkGuardResult.ListPendingLevels),
				() => runner.RunPending(x => listLog.Add(x)));
			Report.Allowed("Prefs: CorruptedDatabase and UpdateInProgressOnComputerName cleared, caches refreshed, security log",() => {
				Prefs.UpdateBool(PrefName.CorruptedDatabase,false,true);
				Prefs.UpdateString(PrefName.UpdateInProgressOnComputerName,"");
				Cache.Refresh(InvalidType.Prefs,InvalidType.Programs);
				SecurityLog securityLog=new SecurityLog();
				securityLog.PermType=EnumPermType.UpdateInstall;
				securityLog.UserNum=0;
				securityLog.LogDateTime=DateTime.Now;
				securityLog.LogText="Grants test: fork migration steps "+string.Join(", ",forkGuardResult.ListPendingLevels)+".";
				securityLog.CompName=Security.GetComplexComputerName();
				SecurityLogs.MakeLogEntry(securityLog);
			});
			ForkGuardResult forkGuardResultAfter=Report.Allowed("ForkVersionGuard.Evaluate after the steps",
				() => ForkVersionGuard.Evaluate(ForkVersions.ReadState(),runner));
			Report.Expect("No step is pending after the start-up",forkGuardResultAfter!=null && forkGuardResultAfter.CanProceed
				&& !forkGuardResultAfter.HasPendingMigrations);
			Report.Expect("The fork tables are back and match their declarations",
				Report.Allowed("ForkTables.VerifyDatabase",() => ForkTables.VerifyDatabase(Db).IsMatch));
			Report.Expect("The stock ledger has the opening balance of the synthetic supply",
				ScalarAccount("SELECT COUNT(*) FROM supplyledger WHERE LedgerType="+(int)SupplyLedgerType.Opening)=="1");
		}
		#endregion Start-up

		#region Reads and writes
		///<summary>Ordinary reads and writes of the modules on several tables: patient, commlog, adjustment, procedurelog, appointment,
		///definition, preference, securitylog, signalod.</summary>
		private void RunReadsAndWrites() {
			Report.Section("Reads and writes");
			Report.Allowed("Patients.Insert, Update and GetPat (two families)",() => {
				_patient=InsertPatient("Grantone");
				_patient2=InsertPatient("Granttwo");
				Patient patientOld=_patient.Copy();
				_patient.Address="1 Synthetic Way";
				Patients.Update(_patient,patientOld);
				if(Patients.GetPat(_patient.PatNum).Address!="1 Synthetic Way") {
					throw new ApplicationException("The patient was not updated.");
				}
			});
			Report.Allowed("Commlogs.Insert, Update and GetOne",() => {
				_commlog=NewCommlog();
				Commlogs.Insert(_commlog);
				_commlog.Note="Synthetic note, changed";
				Commlogs.Update(_commlog);
				Commlogs.GetOne(_commlog.CommlogNum);
			});
			Report.Allowed("Adjustments.Insert and Delete",() => {
				Adjustment adjustment=new Adjustment();
				adjustment.PatNum=_patient.PatNum;
				adjustment.AdjAmt=12.5;
				adjustment.AdjDate=DateTime.Today;
				adjustment.ProcDate=DateTime.Today;
				adjustment.DateEntry=DateTime.Today;
				adjustment.AdjType=Defs.GetDefsForCategory(DefCat.AdjTypes,true).First().DefNum;
				adjustment.ProvNum=PrefC.GetLong(PrefName.PracticeDefaultProv);
				Adjustments.Insert(adjustment);
				Adjustments.Delete(adjustment);
			});
			Report.Allowed("Procedures.Insert, Update and Delete",() => {
				Procedure procedure=new Procedure();
				procedure.PatNum=_patient.PatNum;
				procedure.CodeNum=ProcedureCodes.GetCodeNum("D0120");
				procedure.ProcDate=DateTime.Today;
				procedure.ProcStatus=ProcStat.TP;
				procedure.ProvNum=PrefC.GetLong(PrefName.PracticeDefaultProv);
				procedure.ProcFee=45;
				Procedures.Insert(procedure);
				Procedure procedureOld=procedure.Copy();
				procedure.ProcFee=50;
				Procedures.Update(procedure,procedureOld);
				Procedures.Delete(procedure.ProcNum);
			});
			Report.Allowed("Appointments.Insert, Update and Delete",() => {
				Appointment appointment=new Appointment();
				appointment.PatNum=_patient.PatNum;
				appointment.AptStatus=ApptStatus.Scheduled;
				appointment.AptDateTime=DateTime.Today.AddDays(1).AddHours(9);
				appointment.Pattern="//XXXX//";
				appointment.ProvNum=PrefC.GetLong(PrefName.PracticeDefaultProv);
				Appointments.Insert(appointment);
				Appointment appointmentOld=appointment.Copy();
				appointment.Note="Synthetic appointment";
				Appointments.Update(appointment,appointmentOld);
				Appointments.Delete(appointment.AptNum);
			});
			Report.Allowed("Defs.Insert, Update, the Defs signal and cache",() => {
				Def def=new Def();
				def.Category=DefCat.CommLogTypes;
				def.ItemName="Synthetic grant type";
				def.ItemOrder=Defs.GetDefsForCategory(DefCat.CommLogTypes).Count;
				Defs.Insert(def);
				def.ItemName="Synthetic grant type, changed";
				Defs.Update(def);
				Signalods.SetInvalid(InvalidType.Defs);
				Cache.Refresh(InvalidType.Defs);
			});
			Report.Allowed("Prefs.UpdateString and the preference cache",() => {
				Prefs.UpdateString(PrefName.PracticeTitle,"Synthetic Dental Office");
				Cache.Refresh(InvalidType.Prefs);
			});
			Report.Allowed("SecurityLogs.MakeLogEntry and Refresh (the audit trail window)",() => {
				SecurityLogs.MakeLogEntry(EnumPermType.PatientEdit,_patient.PatNum,"Grants test edit");
				SecurityLogs.Refresh(DateTime.Today.AddDays(-1),DateTime.Today.AddDays(1),EnumPermType.None,0,DateTime.MinValue,DateTime.MinValue);
			});
		}

		private static Patient InsertPatient(string lName) {
			Patient patient=new Patient();
			patient.LName=lName;
			patient.FName="Synthetic";
			patient.PatStatus=PatientStatus.Patient;
			patient.Birthdate=new DateTime(1980,1,1);
			patient.BillingType=PrefC.GetLong(PrefName.PracticeDefaultBillType);
			patient.PriProv=PrefC.GetLong(PrefName.PracticeDefaultProv);
			patient.ChartNumber="";
			Patients.Insert(patient,false);
			Patient patientOld=patient.Copy();
			patient.Guarantor=patient.PatNum;
			Patients.Update(patient,patientOld);
			return patient;
		}

		private Commlog NewCommlog() {
			Commlog commlog=new Commlog();
			commlog.PatNum=_patient.PatNum;
			commlog.CommDateTime=DateTime.Now;
			commlog.CommType=Defs.GetDefsForCategory(DefCat.CommLogTypes,true).First().DefNum;
			commlog.Mode_=CommItemMode.Phone;
			commlog.SentOrReceived=CommSentOrReceived.Sent;
			commlog.Note="Synthetic note";
			commlog.UserNum=Security.CurUser?.UserNum??1;
			return commlog;
		}
		#endregion Reads and writes

		#region Work tables, user queries and schema helpers
		///<summary>The statements the spec's evidence names beyond the modules: work tables (CREATE and DROP TABLE), TRUNCATE (A/R dashboard,
		///recall triggers, family aging), user queries with a temporary table and an index on it, cancelling a running user query (KILL QUERY of
		///the account's own connection), the fork migration context's helpers and the large-table helper (RENAME TABLE) on a table of the run,
		///the redundant index tool, the mixed storage engine conversion, the replication question.</summary>
		private void RunWorkTablesAndHelpers() {
			Report.Section("Work tables, user queries and schema helpers");
			Report.Allowed("DatabaseMaintenances.ClearOldEtransMessageText (the Etrans tool): DROP TABLE IF EXISTS, CREATE TABLE ... SELECT of a work "
				+"table, ALTER TABLE ADD INDEX on it, DELETE, DROP TABLE, OPTIMIZE TABLE",DatabaseMaintenances.ClearOldEtransMessageText);
			Report.Allowed("DashboardARs.Truncate (TRUNCATE dashboardar)",DashboardARs.Truncate);
			Report.Allowed("RecallTypes.SetToDefaultCA (DELETE, INSERT, TRUNCATE recalltrigger)",RecallTypes.SetToDefaultCA);
			Report.Allowed("Ledgers.ComputeAging for two families (TRUNCATE TABLE famaging, INSERT, UPDATE patient)",
				() => Ledgers.ComputeAging(new List<long> { _patient.PatNum,_patient2.PatNum },DateTime.Today));
			Report.Allowed("A user query (DataConnectionCancelable): CREATE TEMPORARY TABLE, CREATE INDEX on it, SELECT, DROP TEMPORARY TABLE",
				() => {
					int serverThread=DataConnectionCancelable.GetServerThread(false,false);
					DataTable table=DataConnectionCancelable.GetTableConAlreadyOpen(serverThread,
						"CREATE TEMPORARY TABLE tmpgrant SELECT PatNum,LName FROM patient; "
						+"CREATE INDEX tmpgrantidx ON tmpgrant (LName); "
						+"SELECT COUNT(*) FROM tmpgrant; "
						+"DROP TEMPORARY TABLE IF EXISTS tmpgrant;",
						wasSqlValidated:false,suppressMessage:true);
					if(table.Rows.Count!=1) {
						throw new ApplicationException("The user query returned no row.");
					}
				});
			Report.Allowed("Cancelling a running user query: KILL QUERY of the account's own connection (DataConnectionCancelable.CancelQuery)",
				RunCancelledUserQuery);
			RunForkMigrationContextHelpers();
			RunLargeTableHelper();
			Report.Allowed("ReplicationServers.IsUsingReplication (SHOW MASTER STATUS, SHOW SLAVE STATUS) and ServerIsBlocked",() => {
				ReplicationServers.IsUsingReplication();
				ReplicationServers.ServerIsBlocked();
			});
		}

		///<summary>SELECT SLEEP(30) on a user query's own connection, cancelled after a second from another thread, as the query window's Cancel
		///button does. The query must end well before 30 seconds.</summary>
		private void RunCancelledUserQuery() {
			int serverThread=DataConnectionCancelable.GetServerThread(false,false);
			Exception exceptionQuery=null;
			Stopwatch stopwatch=Stopwatch.StartNew();
			Thread thread=new Thread(() => {
				try {
					DataConnectionCancelable.GetTableConAlreadyOpen(serverThread,"SELECT SLEEP(30)",wasSqlValidated:true);
				}
				catch(Exception ex) {
					exceptionQuery=ex;
				}
			});
			thread.Start();
			Thread.Sleep(1500);
			DataConnectionCancelable.CancelQuery(serverThread);
			if(!thread.Join(TimeSpan.FromSeconds(25))) {
				throw new ApplicationException("The user query was not cancelled.");
			}
			Report.Note("The cancelled query ended after "+stopwatch.Elapsed.TotalSeconds.ToString("0.0")+" s"
				+(exceptionQuery==null ? "" : " with "+GrantReport.Clean(exceptionQuery.GetBaseException().Message)));
			if(exceptionQuery!=null && GrantReport.IsAccessDenied(exceptionQuery,out int _,out string _)) {
				throw exceptionQuery;
			}
		}

		///<summary>Every helper of ForkMigrationContext on a table of the run: CREATE TABLE, INSERT, ALTER TABLE ADD COLUMN, MODIFY COLUMN with a
		///NULL fill, CHANGE COLUMN, ADD INDEX, a replaced index (DROP INDEX and ADD INDEX in one ALTER), DROP INDEX, DROP COLUMN, CONVERT TO
		///CHARACTER SET; then the disabled keys of a restored backup re-enabled (MiscData.EnableIndexesIfNeeded), the redundant index tool, and
		///the mixed storage engine conversion of the update chain (DatabaseMaintenances.ConvertToDefaultEngine).</summary>
		private void RunForkMigrationContextHelpers() {
			ForkMigrationContext context=new ForkMigrationContext(Db);
			Report.Allowed("ForkMigrationContext.CreateTableIfMissing (CREATE TABLE)",() => context.CreateTableIfMissing(CREATE_PROBE));
			Report.Allowed("ForkMigrationContext.ExecuteData (INSERT)",
				() => context.ExecuteData("INSERT INTO grantprobe (Descript,Amount) VALUES ('one',1),('two',2),('three',3)"));
			Report.Allowed("ForkMigrationContext.AddColumnIfMissing (ALTER TABLE ADD COLUMN)",
				() => context.AddColumnIfMissing(TABLE_PROBE,"Note","varchar(255) DEFAULT NULL","Descript"));
			Report.Allowed("ForkMigrationContext.EnsureColumnDefinition with a NULL fill (UPDATE, ALTER TABLE MODIFY COLUMN)",
				() => context.EnsureColumnDefinition(TABLE_PROBE,"Note","text NOT NULL","''"));
			Report.Allowed("ForkMigrationContext.RenameColumnIfNeeded (ALTER TABLE CHANGE COLUMN)",
				() => context.RenameColumnIfNeeded(TABLE_PROBE,"Note","Remark","text NOT NULL"));
			Report.Allowed("ForkMigrationContext.EnsureIndex (ALTER TABLE ADD INDEX; no INDEX right)",
				() => context.EnsureIndex(TABLE_PROBE,"Descript","`Descript`"));
			Report.Allowed("ForkMigrationContext.EnsureIndex replacing a key (ALTER TABLE DROP INDEX, ADD INDEX)",
				() => context.EnsureIndex(TABLE_PROBE,"Descript","`Descript`,`Amount`"));
			Report.Allowed("ForkMigrationContext.DropIndexIfExists (ALTER TABLE DROP INDEX)",() => context.DropIndexIfExists(TABLE_PROBE,"Descript"));
			Report.Allowed("ForkMigrationContext.DropColumnIfExists (ALTER TABLE DROP COLUMN)",() => context.DropColumnIfExists(TABLE_PROBE,"Remark"));
			Report.Allowed("ForkMigrationContext.EnsureTableCharset (ALTER TABLE CONVERT TO CHARACTER SET)",
				() => context.EnsureTableCharset(TABLE_PROBE,"utf8mb4","utf8mb4_general_ci"));
			Report.Allowed("ALTER TABLE ... DISABLE KEYS, then MiscData.GetTablesDisabledIndexes and EnableIndexesIfNeeded (ALTER TABLE ENABLE KEYS)",
				() => {
					context.EnsureIndex(TABLE_PROBE,"Amount","`Amount`");
					context.Execute("ALTER TABLE `grantprobe` DISABLE KEYS",TABLE_PROBE);
					List<string> listTables=MiscData.GetTablesDisabledIndexes();
					if(!listTables.Contains(TABLE_PROBE)) {
						throw new ApplicationException("The disabled keys were not found.");
					}
					MiscData.EnableIndexesIfNeeded(listTables);
				});
			Report.Allowed("The redundant index tool: GetRedundantIndexesTable and DropRedundantIndexes (ALTER TABLE DROP INDEX)",() => {
				context.EnsureIndex(TABLE_PROBE,"AmountDup","`Amount`");
				List<DataRow> listRows=DatabaseMaintenances.GetRedundantIndexesTable().Select()
					.Where(x => x["TABLE_NAME"].ToString()==TABLE_PROBE).ToList();
				if(listRows.Count==0) {
					throw new ApplicationException("The redundant index was not found.");
				}
				DatabaseMaintenances.DropRedundantIndexes(listRows);
			});
			if(InnoDb.IsInnodbAvail()) {
				Report.Allowed("A table in another engine, then DatabaseMaintenances.GetTableEngineTableNames and ConvertToDefaultEngine (ALTER TABLE "
					+"ENGINE), as the update chain's start does",() => {
						context.Execute("ALTER TABLE `grantprobe` ENGINE=InnoDB",TABLE_PROBE);
						DatabaseMaintenances.GetStorageEngineDefaultName();
						DatabaseMaintenances.GetTableEngineTableNames();
						if(!DatabaseMaintenances.ConvertToDefaultEngine()) {
							throw new ApplicationException("ConvertToDefaultEngine returned false.");
						}
					});
			}
			else {
				Report.Note("InnoDB is not available on this server: the engine conversion was not tried.");
			}
		}

		///<summary>LargeTableHelper.AlterTable on the table of the run. With the UpdateAlterLargeTablesDirectly preference on (a new database's
		///value) it runs one ALTER TABLE. With it off (an office may have it off) and a table of more than one batch (a wide column, so that a batch
		///holds 1,000 rows, and 2,003 rows): DROP TABLE IF EXISTS, RENAME TABLE, CREATE TABLE LIKE, ALTER TABLE, DISABLE KEYS, INSERT in batches,
		///ENABLE KEYS, DROP TABLE. The preference stays off for the refusals.</summary>
		private void RunLargeTableHelper() {
			Report.Allowed("LargeTableHelper.AlterTable with UpdateAlterLargeTablesDirectly on: one ALTER TABLE",
				() => LargeTableHelper.AlterTable(TABLE_PROBE,"GrantProbeNum",new LargeTableHelper.ColNameAndDef("Direct","int NOT NULL"),
					new LargeTableHelper.IndexColsAndName("Direct","Direct")));
			GrantServer.ExecRoot("UPDATE `"+Db+"`.preference SET ValueString='0' WHERE PrefName='"+nameof(PrefName.UpdateAlterLargeTablesDirectly)+"'");
			Report.Note("As root: UpdateAlterLargeTablesDirectly set to 0, so the large-table helper copies the table.");
			ForkMigrationContext context=new ForkMigrationContext(Db);
			Report.Allowed("The table of the run made wide (ALTER TABLE ADD COLUMN) and long (INSERT ... SELECT, 2,000 rows)",() => {
				context.AddColumnIfMissing(TABLE_PROBE,"Wide","varchar(7000) NOT NULL DEFAULT ''");
				string digits="(SELECT 0 n UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5 "
					+"UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8 UNION ALL SELECT 9)";
				for(int i=0;i<2;i++) {
					context.ExecuteData("INSERT INTO grantprobe (Descript,Amount) SELECT 'bulk',a.n*100+b.n*10+c.n FROM "+digits+" a,"+digits+" b,"
						+digits+" c");
				}
			});
			RunLargeTableAlter("Allowed","Extra",isExpectedAllowed:true);
		}

		///<summary>LargeTableHelper.AlterTable adding the column, expected allowed or refused for the missing right. When allowed, checks that it
		///took the path for a large table (more than one batch) and kept every row.</summary>
		private void RunLargeTableAlter(string right,string columnName,bool isExpectedAllowed) {
			string countBefore=GrantServer.ScalarRoot("SELECT COUNT(*) FROM `"+Db+"`.grantprobe");
			string what="LargeTableHelper.AlterTable on a table of more than one batch (DROP TABLE IF EXISTS, RENAME TABLE, CREATE TABLE LIKE, "
				+"ALTER TABLE, DISABLE KEYS, INSERT, ENABLE KEYS, DROP TABLE)";
			Action action=() => LargeTableHelper.AlterTable(TABLE_PROBE,"GrantProbeNum",new LargeTableHelper.ColNameAndDef(columnName,"int NOT NULL"),
				new LargeTableHelper.IndexColsAndName(columnName,columnName));
			if(isExpectedAllowed) {
				Report.Allowed(what,action);
				int countBatches=LargeTableHelper.ListPriKeyMaxPerBatch?.Count??0;
				Report.Expect("The large-table helper used more than one batch, so it renamed and copied the table",countBatches>1,
					countBatches+" batch(es)");
			}
			else {
				Report.Refused(what,right,action);
			}
			Report.Expect("The large-table helper kept every row and the table's name",
				GrantServer.ScalarRoot("SELECT COUNT(*) FROM `"+Db+"`.grantprobe")==countBefore
				&& GrantServer.ScalarRoot("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+Db+"' AND TABLE_NAME LIKE 'tempConv\\_%'")=="0",
				countBefore+" rows");
		}
		#endregion Work tables, user queries and schema helpers

		#region Database maintenance
		///<summary>Database Maintenance as the window runs it: the table check and repair, every method of the grid in Check and in Fix (with
		///DateLastRun), the tools that work on the database only, and the backup-repair-optimize of a MariaDB version change. An error that is not
		///"access denied" is recorded and does not fail the run; synthetic data can make a method stop for its own reasons.</summary>
		private void RunMaintenance() {
			Report.Section("Database maintenance");
			foreach(DbmMode dbmMode in new[] { DbmMode.Check,DbmMode.Fix }) {
				Report.Allowed("DatabaseMaintenances.MySQLTables("+dbmMode+"): DROP TABLE IF EXISTS of the old signal table, CHECK TABLE of every table",
					() => {
						//MySQLTables records a CHECK TABLE that fails instead of throwing.
						Result result=DatabaseMaintenances.MySQLTables(false,dbmMode);
						if(!result.IsSuccess) {
							throw new ApplicationException(result.Msg);
						}
					});
			}
			List<MethodInfo> listMethodInfos=Report.Allowed("GetMethodsForDisplay, InsertMissingDBMs, GetAll",() => {
				List<MethodInfo> listMethods=DatabaseMaintenances.GetMethodsForDisplay(0);
				DatabaseMaintenances.InsertMissingDBMs(listMethods.Select(x => x.Name).ToList());
				DatabaseMaintenances.GetAll();
				return listMethods;
			})??new List<MethodInfo>();
			int countAllowed=0;
			foreach(DbmMode dbmMode in new[] { DbmMode.Check,DbmMode.Fix }) {
				foreach(MethodInfo methodInfo in listMethodInfos) {
					DbmMethodAttr dbmMethodAttr=(DbmMethodAttr)Attribute.GetCustomAttribute(methodInfo,typeof(DbmMethodAttr));
					List<object> listParameters=new List<object> { false,dbmMode };
					if(dbmMethodAttr.HasPatNum) {
						listParameters.Add(0L);
					}
					bool isAllowed=Report.Allowed("DBM "+dbmMode+" "+methodInfo.Name,() => {
						try {
							methodInfo.Invoke(null,listParameters.ToArray());
						}
						catch(TargetInvocationException ex) when(ex.InnerException!=null) {
							throw ex.InnerException;
						}
						if(dbmMode==DbmMode.Fix) {
							DatabaseMaintenances.UpdateDateLastRun(methodInfo.Name);
						}
					},isOtherErrorTolerated:true);
					if(isAllowed) {
						countAllowed++;
					}
				}
			}
			Report.Note(listMethodInfos.Count+" maintenance methods, "+countAllowed+" of "+(2*listMethodInfos.Count)+" runs (Check and Fix) finished.");
			Report.Allowed("Tool: Special Characters (FixSpecialCharacters)",DatabaseMaintenances.FixSpecialCharacters);
			Report.Allowed("Tool: Remove Nulls (MySqlRemoveNullStrings)",() => DatabaseMaintenances.MySqlRemoveNullStrings());
			Report.Allowed("Tool: Etrans (ClearOldEtransMessageText: work table, ALTER TABLE ADD INDEX, DELETE, OPTIMIZE)",
				DatabaseMaintenances.ClearOldEtransMessageText);
			Report.Allowed("Tool: Active TPs (GetProcsNoActiveTp, CreateMissingActiveTPs)",
				() => DatabaseMaintenances.CreateMissingActiveTPs(DatabaseMaintenances.GetProcsNoActiveTp()));
			Report.Allowed("Tool: Patient Missing",() => DatabaseMaintenances.PatientMissing());
			Report.Allowed("Tool: Ortho Chart duplicates",() => DatabaseMaintenances.OrthoChartDeleteDuplicates());
			Report.Allowed("Tool: Recalc Estimates",() => DatabaseMaintenances.RecalcEstimates(Procedures.GetProcsWithOldEstimates()));
			Report.Allowed("Tool: Pay Plan Payments",() => DatabaseMaintenances.DetachInvalidPaymentPlanPayments());
			Report.Allowed("Tool: Duplicate Emails",() => DatabaseMaintenances.CleanUpDuplicateEmails());
			Report.Allowed("Tool: Insurance plan type (Category, then Percentage)",() => {
				DatabaseMaintenances.UpdateInsurancePlanType("Category");
				DatabaseMaintenances.UpdateInsurancePlanType("Percentage");
			});
			Report.Allowed("DatabaseMaintenances.RepairAndOptimize: REPAIR TABLE and OPTIMIZE TABLE of every table",
				DatabaseMaintenances.RepairAndOptimize);
		}
		#endregion Database maintenance

		#region Backups
		///<summary>Every variant of MiscData.MakeABackup the program calls, besides the default one of the start-up: with BackupIndexesDisabled
		///off, not as an automatic backup (the Backup window), the archive tab's backup with verification on the same server, the archive tab's
		///backup to another server, the backup before repair and optimize; then FLUSH TABLES of the Backup window's file copy.</summary>
		private void RunBackups() {
			Report.Section("Backups");
			Report.Allowed("MiscData.MakeABackup() with BackupIndexesDisabled off (no DISABLE KEYS)",() => {
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
			Report.Allowed("MiscData.MakeABackup(isAutoBackup:false): the Backup window's backup of an InnoDB database",
				() => MiscData.MakeABackup(isAutoBackup:false));
			Report.Allowed("MiscData.MakeABackup(serverName,user,pass,doVerify:true) on this server: the archive tab's backup with the row counts",
				() => MiscData.MakeABackup(serverName:GrantServer.SERVER,user:Account,pass:_password,doVerify:true));
			//The archive tab's backup to another server (here this server under another name, so that the program takes that path) creates the
			//backup database and its tables there, then copies each table's rows with INSERT statements (owner question DB-SEC 2, 2026-11-07: before,
			//MySqlBulkLoader's LOAD DATA LOCAL INFILE, which MySqlConnector refuses without AllowLoadLocalInfile=true, stopped it at the first table
			//with rows). It needs the rights of a backup on this server: CREATE, INSERT and SELECT on the backup database.
			Report.Allowed("MiscData.MakeABackup to another server (the archive tab; here "+GrantServer.SERVER
				+":3306): CREATE DATABASE and the tables there, then INSERT statements with the rows",
				() => MiscData.MakeABackup(serverName:GrantServer.SERVER+":3306",user:Account,pass:_password,doVerify:true));
			Report.Allowed("Backup, repair and optimize after a MariaDB version change (Shared.BackupRepairAndOptimize: MakeABackup, "
				+"RepairAndOptimize)",() => {
					MiscData.MakeABackup();
					DatabaseMaintenances.RepairAndOptimize();
				});
			Report.Allowed("MiscData.FlushTables: FLUSH TABLES before the Backup window's file copy",MiscData.FlushTables);
			List<string> listBackups=GetBackupDatabases();
			Report.Note("Backup databases now: "+string.Join(", ",listBackups));
			List<string> listShown=Report.Allowed("SHOW DATABASES as the account lists the backups (what MakeABackup reads to name the next one)",
				() => ColumnAccount("SHOW DATABASES"));
			Report.Expect("Every backup is listed for the account",listShown!=null && listBackups.All(x => listShown.Contains(x)));
		}
		#endregion Backups

		#region Release channel
		///<summary>The release channel's rows (documentmisc): publishing a release of synthetic files as the update server does (PublishRelease),
		///reading the manifest, downloading and checking it as a workstation does, publishing again over it, and deleting Open Dental's update
		///files as the adoption does.</summary>
		private void RunReleaseChannel() {
			Report.Section("Release channel");
			string folderInstall=NewTempFolder("install");
			File.WriteAllText(Path.Combine(folderInstall,"OpenPmsDental.exe"),"synthetic program "+Guid.NewGuid());
			File.WriteAllText(Path.Combine(folderInstall,"OpenPmsDentalBusiness.dll"),"synthetic business layer");
			File.WriteAllText(Path.Combine(folderInstall,"UpdateFileCopier.exe"),"synthetic copier");
			ReleaseIdentity releaseIdentity=new ReleaseIdentity { ReleaseVersion="0.0",BuildId=ForkReleases.ComputeBuildId(folderInstall) };
			Report.Allowed("ForkReleases.Publish with the in-progress flag, UpdateHistories (PrefL.PublishRelease)",() => {
				Prefs.UpdateString(PrefName.UpdateInProgressOnComputerName,Environment.MachineName);
				try {
					ForkReleases.Publish(folderInstall,releaseIdentity,"GRANTTEST");
				}
				finally {
					Prefs.UpdateString(PrefName.UpdateInProgressOnComputerName,"");
				}
				if(UpdateHistories.GetForVersion(releaseIdentity.ReleaseVersion)==null) {
					UpdateHistories.Insert(new UpdateHistory(releaseIdentity.ReleaseVersion));
				}
				Cache.Refresh(InvalidType.Prefs);
			});
			Report.Allowed("ForkReleases.GetPublishedManifest and DownloadAndVerify",() => {
				ReleaseManifest releaseManifest=ForkReleases.GetPublishedManifest(out string error);
				if(releaseManifest==null) {
					throw new ApplicationException("No manifest: "+error);
				}
				ReleaseDownloadResult releaseDownloadResult=ForkReleases.DownloadAndVerify(Path.Combine(NewTempFolder("pull"),"ForkRelease"));
				if(!releaseDownloadResult.IsOk) {
					throw new ApplicationException(string.Join("; ",releaseDownloadResult.ListProblems));
				}
			});
			Report.Allowed("ForkReleases.Publish again over the published release (DocumentMiscs.ReplaceForkRelease: DELETE, INSERT)",() => {
				File.WriteAllText(Path.Combine(folderInstall,"OpenPmsDental.exe"),"synthetic program "+Guid.NewGuid());
				ForkReleases.Publish(folderInstall,new ReleaseIdentity { ReleaseVersion="0.0",BuildId=ForkReleases.ComputeBuildId(folderInstall) },
					"GRANTTEST");
			});
			Report.Allowed("DocumentMiscs.DeleteOpenDentalUpdateFiles (the adoption's step 3)",() => DocumentMiscs.DeleteOpenDentalUpdateFiles());
		}
		#endregion Release channel
	}
}
