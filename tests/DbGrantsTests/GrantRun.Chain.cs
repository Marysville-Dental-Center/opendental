// The manual grants test, part 4: the frozen Open Dental update chain from 21.1 to 24.3.63 and the adoption with every fork migration step,
// as the account; then the guide's warning that DROP and CREATE on the office database also allow dropping and creating it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using DataConnectionBase;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace DbGrantsTests {
	public partial class GrantRun {
		///<summary>As root: the office database replaced by the Open Dental 21.1 seed (UnitTests\Resources\dump.txt, the start of
		///tools/New-DevDatabase.ps1). As the account: what ClassConvertDatabase.Convert does on the update server (the engine check, the
		///replication block, the backup of a Release build, the flags, ConvertDatabases.InvokeConvertMethods), the compatibility line of
		///PrefL.CheckProgramVersion, and the adoption (Adoption.Preflight and Run: the office's own patient column and key dropped, Open Dental's
		///update files deleted, the version markers, every fork migration step), then the schema verifier.</summary>
		private void RunChainAndAdoption() {
			Report.Section("Open Dental update chain from 21.1 and the adoption");
			GrantServer.ExecRoot("DROP DATABASE `"+Db+"`","CREATE DATABASE `"+Db+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			using(MySqlConnection mySqlConnection=GrantServer.OpenRoot(Db)) {
				GrantServer.Exec(mySqlConnection,File.ReadAllText(FindRepoFile(Path.Combine("UnitTests","Resources","dump.txt")),new UTF8Encoding(false)));
			}
			ClearPools();
			if(!Report.Allowed("DataConnection.SetDb on the 21.1 database",ConnectProgram)) {
				return;
			}
			Security.CurUser=new Userod();
			string versionFrom=Report.Allowed("Prefs.RefreshCache and DataBaseVersion",() => {
				Prefs.RefreshCache();
				return PrefC.GetString(PrefName.DataBaseVersion);
			});
			Report.Note("DataBaseVersion before the chain: "+versionFrom);
			Report.Allowed("ClassConvertDatabase's checks: the storage engines, ReplicationServers.ServerIsBlocked",() => {
				DatabaseMaintenances.GetStorageEngineDefaultName();
				DatabaseMaintenances.GetTableEngineTableNames();
				ReplicationServers.ServerIsBlocked();
			});
			Report.Allowed("MiscData.MakeABackup() before the chain (a Release build's Shared.MakeABackup)",() => MiscData.MakeABackup());
			bool isConverted=Report.Allowed("ConvertDatabases.InvokeConvertMethods: every Open Dental step from "+versionFrom+" to 24.3.63",() => {
				Prefs.UpdateStringNoCache(PrefName.UpdateInProgressOnComputerName,Environment.MachineName);
				Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"1");
				ConvertDatabases.FromVersion=new Version(versionFrom);
				ConvertDatabases.InvokeConvertMethods();
				Prefs.UpdateStringNoCache(PrefName.CorruptedDatabase,"0");
				Prefs.UpdateStringNoCache(PrefName.UpdateInProgressOnComputerName,"");
				Cache.Refresh(InvalidType.Prefs);
			});
			if(!isConverted) {
				return;
			}
			Report.Note("DataBaseVersion after the chain: "+PrefC.GetString(PrefName.DataBaseVersion));
			Report.Allowed("PrefL.CheckProgramVersion's compatibility line (Release builds): ProgramVersion raised to "
				+ForkVersions.CompatVersion.ToString(4),() => {
					Prefs.UpdateString(PrefName.ProgramVersion,ForkVersions.CompatVersion.ToString(4));
					Cache.Refresh(InvalidType.Prefs);
				});
			//The office's own generated column and unique key, which the adoption drops (Adoption.DropPortalActiveEmail).
			GrantServer.ExecRoot("ALTER TABLE `"+Db+"`.patient ADD COLUMN `"+Adoption.PATIENT_PORTAL_EMAIL_COLUMN+"` varchar(100) GENERATED ALWAYS AS "
				+"(case when `PatStatus` = 0 and `Email` <> '' then `Email` else NULL end) VIRTUAL, ADD UNIQUE KEY `"+Adoption.PATIENT_PORTAL_EMAIL_INDEX
				+"` (`"+Adoption.PATIENT_PORTAL_EMAIL_COLUMN+"`)");
			ClearPools();
			Report.Note("As root: the office's generated patient column and its unique key added, as an office copy has them.");
			AdoptionPreflight adoptionPreflight=Report.Allowed("Adoption.Preflight",() => Adoption.Preflight());
			Report.Expect("The 24.3.63 database can be adopted",adoptionPreflight!=null && adoptionPreflight.CanAdopt,adoptionPreflight?.Message);
			List<string> listLog=new List<string>();
			AdoptionResult adoptionResult=Report.Allowed("Adoption.Run: ALTER TABLE patient DROP INDEX and DROP COLUMN, the update files, the markers, "
				+"every fork migration step",() => Adoption.Run(x => listLog.Add(x)));
			if(adoptionResult!=null) {
				Report.Note("Adoption: "+string.Join(" ",listLog.Where(x => !x.StartsWith("Upgrading"))));
			}
			Report.Allowed("Cache.Refresh(AllLocal) on the adopted database",() => Cache.Refresh(InvalidType.AllLocal));
			ForkGuardResult forkGuardResult=Report.Allowed("ForkVersionGuard.Evaluate on the adopted database",
				() => ForkVersionGuard.Evaluate(ForkVersions.ReadState()));
			Report.Expect("The adopted database lets the program start with nothing pending",forkGuardResult!=null && forkGuardResult.CanProceed
				&& !forkGuardResult.HasPendingMigrations,forkGuardResult?.Message);
			SchemaVerifyResult schemaVerifyResult=Report.Allowed("SchemaVerifier.VerifyDatabase (26.1.70)",() => SchemaVerifier.VerifyDatabase());
			Report.Expect("The adopted schema matches Open Dental 26.1.70 apart from the documented exceptions",
				schemaVerifyResult!=null && schemaVerifyResult.IsMatch,
				schemaVerifyResult==null ? "" : string.Join("; ",schemaVerifyResult.ListMismatches.Take(5).Select(x => x.ToString())));
		}

		///<summary>The guide's warning, shown: DROP on the office database also allows DROP DATABASE of it, and CREATE allows creating it again.
		///The last thing the run does before the clean-up.</summary>
		private void RunOfficeDropAndCreate() {
			Report.Section("The office database itself");
			ClearPools();
			Report.Allowed("DROP DATABASE of the office database (the guide's warning: DROP on <database> allows it)",
				() => ExecAccount("","DROP DATABASE `"+Db+"`"));
			Report.Allowed("CREATE DATABASE of the office database again (CREATE on <database> allows it)",
				() => ExecAccount("","CREATE DATABASE `"+Db+"` CHARACTER SET utf8mb3"));
		}
	}
}
