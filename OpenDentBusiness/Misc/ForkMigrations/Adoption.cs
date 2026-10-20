// Explicit adoption of an Open Dental database (backlog Risk R1, package M1-03). Normal startup never calls this; an administrator runs it from
// FormAdoptDatabase against a restored copy.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using CodeBase;
using CodeBase.VendorDirect;
using DataConnectionBase;

namespace OpenDentBusiness {
	public class AdoptionPreflight {
		public bool CanAdopt;
		///<summary>True when an earlier adoption of this copy was started and is being finished.</summary>
		public bool IsResume;
		public bool IsAlreadyAdopted;
		///<summary>The Open Dental version the database comes from (on the allowlist when CanAdopt).</summary>
		public Version VersionSource;
		public string Message="";
	}

	public class AdoptionResult {
		public Version VersionSource;
		public bool WasPortalEmailColumnDropped;
		public List<string> ListIndexesDropped=new List<string>();
		public long CountUpdateFileRowsDeleted;
		public List<string> ListPrefsBlanked=new List<string>();
		public List<string> ListLog=new List<string>();
	}

	///<summary>Adopts an Open Dental database whose DataBaseVersion is on the allowlist (24.3.63.0 or 25.3.48.0). Every step is idempotent, and the
	///order is chosen so an interrupted adoption (MyISAM has no transactions) can be run again to finish:
	///1. ForkAdoptedFrom records the start. 2. The office-only patient._portal_active_email column and its unique index are dropped. 3. Open Dental's
	///UpdateFiles and UpdateFilesSegment rows are deleted unread. 4. Preferences that point at Open Dental's servers are blanked. 5. DataBaseVersion and
	///ProgramVersion get the sentinel, so Open Dental clients can no longer use the copy. 6. OdSchemaCompat, then ForkSchemaLevel are written: from
	///here the fork markers govern the database. 7. The fork migration runner runs its pending steps. 8. ForkAdoptedFrom records the finish.</summary>
	public class Adoption {
		public const string PATIENT_PORTAL_EMAIL_COLUMN="_portal_active_email";
		///<summary>The unique index the office copy has on the column (fox-db-schema.sql). Any other index on the column is dropped too.</summary>
		public const string PATIENT_PORTAL_EMAIL_INDEX="portal_active_email_uniq";
		///<summary>Preferences that only ever held addresses of Open Dental's servers. They are blanked whatever they hold. Any other preference whose
		///value is a web address on one of Open Dental's hosts (CodeBase.VendorDirect.HqHosts) is blanked too.</summary>
		public static readonly List<string> ListHqUrlPrefNames=new List<string>() {
			"WebServiceHQServerURL",
			"UpdateServerAddress",
			"UpdateWebsitePath",
			"WebHostSynchServerURL",
			"MobileSyncServerURL",
			"EmailHostingEndpoint",
		};

		///<summary>Decides from the version preferences and markers alone whether the database can be adopted.</summary>
		public static AdoptionPreflight Preflight(ForkDbState forkDbState) {
			AdoptionPreflight adoptionPreflight=new AdoptionPreflight();
			if(forkDbState.IsForkMarked) {
				adoptionPreflight.IsAlreadyAdopted=true;
				adoptionPreflight.Message="This database is already adopted ("+(forkDbState.ForkAdoptedFrom??"no adoption record")+"). Nothing to do.";
				return adoptionPreflight;
			}
			if(forkDbState.IsAdoptionRecordFinished) {
				//The adoption finished (and its fork migration steps ran), but ForkSchemaLevel was removed since. Adopting again would reset the schema
				//level to the baseline and run every step a second time on a migrated database.
				adoptionPreflight.Message="This database was already adopted ("+forkDbState.ForkAdoptedFrom+"), but its version markers (ForkSchemaLevel, "
					+"OdSchemaCompat) were removed. It is not adopted again. Restore a backup of the adopted database.";
				return adoptionPreflight;
			}
			if(forkDbState.IsAdoptionIncomplete) {
				Version versionStarted=ParseAdoptedFromSource(forkDbState.ForkAdoptedFrom);
				if(!ForkVersions.IsAdoptable(versionStarted)) {
					adoptionPreflight.Message="The unfinished adoption record is not readable ("+forkDbState.ForkAdoptedFrom+"). Restore the backup taken "
						+"before the adoption.";
					return adoptionPreflight;
				}
				adoptionPreflight.CanAdopt=true;
				adoptionPreflight.IsResume=true;
				adoptionPreflight.VersionSource=versionStarted;
				adoptionPreflight.Message="An adoption from Open Dental "+versionStarted.ToString(3)+" was started on this copy and will be finished.";
				return adoptionPreflight;
			}
			Version versionDb=ForkVersions.ParseVersion(forkDbState.DataBaseVersion);
			Version versionProgram=ForkVersions.ParseVersion(forkDbState.ProgramVersion);
			adoptionPreflight.VersionSource=versionDb;
			if(!ForkVersions.IsAdoptable(versionDb)) {
				adoptionPreflight.Message="DataBaseVersion is "+(forkDbState.DataBaseVersion??"missing")+". Only these Open Dental versions can be adopted: "
					+string.Join(", ",ForkVersions.ListAdoptableVersions.Select(x => x.ToString(4)))+".";
				return adoptionPreflight;
			}
			if(versionProgram!=versionDb) {
				adoptionPreflight.Message="ProgramVersion ("+(forkDbState.ProgramVersion??"missing")+") differs from DataBaseVersion ("
					+forkDbState.DataBaseVersion+"). The copy may be part way through an Open Dental update; restore a clean backup.";
				return adoptionPreflight;
			}
			adoptionPreflight.CanAdopt=true;
			adoptionPreflight.Message="Open Dental "+versionDb.ToString(3)+" database. It can be adopted.";
			return adoptionPreflight;
		}

		///<summary>Reads the database and decides whether it can be adopted: version preferences and markers, no failed update, no update in progress,
		///a direct MySQL/MariaDB connection.</summary>
		public static AdoptionPreflight Preflight() {
			Meth.NoCheckMiddleTierRole();
			AdoptionPreflight adoptionPreflight;
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT || DataConnection.DBtype!=DatabaseType.MySql) {
				adoptionPreflight=new AdoptionPreflight();
				adoptionPreflight.Message="Adoption needs a direct connection to a MySQL or MariaDB database.";
				return adoptionPreflight;
			}
			adoptionPreflight=Preflight(ForkVersions.ReadState());
			if(!adoptionPreflight.CanAdopt) {
				return adoptionPreflight;
			}
			if(PIn.Bool(ForkVersions.GetMarker("CorruptedDatabase")??"0")) {
				adoptionPreflight.CanAdopt=false;
				adoptionPreflight.Message="The database is flagged as corrupted by a failed update (CorruptedDatabase). Restore a clean backup.";
				return adoptionPreflight;
			}
			string updateComputer=ForkVersions.GetMarker("UpdateInProgressOnComputerName")??"";
			if(updateComputer!="") {
				adoptionPreflight.CanAdopt=false;
				adoptionPreflight.Message="An update is marked in progress on '"+updateComputer+"'. Restore a copy taken while no update was running.";
				return adoptionPreflight;
			}
			return adoptionPreflight;
		}

		///<summary>Adopts the connected database. Throws if the preflight fails or a step fails; running it again finishes an interrupted adoption.
		///Pass the runner for tests; null uses the registered fork migration runner.</summary>
		public static AdoptionResult Run(Action<string> actionLog=null,IForkMigrationRunner runner=null,DateTime? dateTimeUtcNow=null) {
			Meth.NoCheckMiddleTierRole();
			runner=runner??ForkMigrationHandOff.Runner;
			AdoptionResult adoptionResult=new AdoptionResult();
			void Log(string text) {
				adoptionResult.ListLog.Add(text);
				actionLog?.Invoke(text);
			}
			AdoptionPreflight adoptionPreflight=Preflight();
			if(!adoptionPreflight.CanAdopt) {
				throw new ApplicationException(adoptionPreflight.Message);
			}
			Version versionSource=adoptionPreflight.VersionSource;
			adoptionResult.VersionSource=versionSource;
			string started=FormatUtc(dateTimeUtcNow??DateTime.UtcNow);
			if(adoptionPreflight.IsResume) {
				Log("Finishing the adoption started earlier: "+ForkVersions.GetMarker(ForkVersions.PREF_FORK_ADOPTED_FROM));
				started=ParseAdoptedFromField(ForkVersions.GetMarker(ForkVersions.PREF_FORK_ADOPTED_FROM),"started")??started;
			}
			else {
				ForkVersions.SetMarker(ForkVersions.PREF_FORK_ADOPTED_FROM,"source="+versionSource.ToString(4)+";started="+started);
				Log("Adoption from Open Dental "+versionSource.ToString(4)+" started.");
			}
			//2. The office-only generated column and its unique index.
			DropPortalActiveEmail(adoptionResult,Log);
			//3. Open Dental's update files: deleted without being read.
			adoptionResult.CountUpdateFileRowsDeleted=DocumentMiscs.DeleteOpenDentalUpdateFiles();
			Log("Deleted "+adoptionResult.CountUpdateFileRowsDeleted+" Open Dental update file row(s).");
			//4. Addresses of Open Dental's servers.
			adoptionResult.ListPrefsBlanked=BlankHqUrlPrefs();
			Log("Blanked "+adoptionResult.ListPrefsBlanked.Count+" preference(s) that pointed at Open Dental's servers"
				+(adoptionResult.ListPrefsBlanked.Count==0?".":": "+string.Join(", ",adoptionResult.ListPrefsBlanked)+"."));
			//5. Lock out Open Dental clients.
			ForkVersions.SetMarker("DataBaseVersion",ForkVersions.SentinelVersion.ToString(4));
			ForkVersions.SetMarker("ProgramVersion",ForkVersions.SentinelVersion.ToString(4));
			Log("DataBaseVersion and ProgramVersion set to "+ForkVersions.SentinelVersion.ToString(4)+".");
			//6. The fork markers take over. ForkSchemaLevel last: its presence is what marks the database as adopted.
			if(ForkVersions.GetMarker(ForkVersions.PREF_OD_SCHEMA_COMPAT)==null) {
				ForkVersions.SetMarker(ForkVersions.PREF_OD_SCHEMA_COMPAT,versionSource.ToString(3));
			}
			if(ForkVersions.GetMarker(ForkVersions.PREF_FORK_SCHEMA_LEVEL)==null) {
				ForkVersions.SetMarker(ForkVersions.PREF_FORK_SCHEMA_LEVEL,ForkVersions.SCHEMA_LEVEL_BASELINE);
			}
			Log("Fork markers written: OdSchemaCompat "+ForkVersions.GetMarker(ForkVersions.PREF_OD_SCHEMA_COMPAT)+", ForkSchemaLevel "
				+ForkVersions.GetMarker(ForkVersions.PREF_FORK_SCHEMA_LEVEL)+".");
			//7. Fork migration steps (24.4 to 25.3.48 for a 24.3.63 source; idempotent no-ops for 25.3.48).
			if(ForkMigrationHandOff.HasSteps(runner)) {
				Log("Running fork migration steps up to level "+runner.LatestLevel+".");
				runner.RunPending(Log);
			}
			else {
				Log("No fork migration steps in this build.");
			}
			//8. Done.
			ForkVersions.SetMarker(ForkVersions.PREF_FORK_ADOPTED_FROM,"source="+versionSource.ToString(4)+";started="+started
				+";adopted="+FormatUtc(dateTimeUtcNow??DateTime.UtcNow));
			Log("Adoption finished.");
			ODException.SwallowAnyException(() => {
				SecurityLog securityLog=new SecurityLog();
				securityLog.PermType=EnumPermType.UpdateInstall;
				securityLog.UserNum=0;//Adoption runs before anyone logs in.
				securityLog.LogDateTime=DateTime.Now;
				securityLog.LogText="Database adopted from Open Dental "+versionSource.ToString(4)+".";
				securityLog.CompName=Security.GetComplexComputerName();
				SecurityLogs.MakeLogEntry(securityLog);
			});
			return adoptionResult;
		}

		private static void DropPortalActiveEmail(AdoptionResult adoptionResult,Action<string> actionLog) {
			string command="SELECT DISTINCT INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='patient' "
				+"AND (COLUMN_NAME='"+PATIENT_PORTAL_EMAIL_COLUMN+"' OR INDEX_NAME='"+PATIENT_PORTAL_EMAIL_INDEX+"')";
			DataTable table=Db.GetTable(command);
			foreach(DataRow row in table.Rows) {
				string indexName=PIn.String(row["INDEX_NAME"].ToString());
				if(indexName.ToUpper()=="PRIMARY") {
					continue;
				}
				Db.NonQ("ALTER TABLE patient DROP INDEX `"+indexName.Replace("`","``")+"`");
				adoptionResult.ListIndexesDropped.Add(indexName);
				actionLog("Dropped index patient."+indexName+".");
			}
			command="SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='patient' "
				+"AND COLUMN_NAME='"+PATIENT_PORTAL_EMAIL_COLUMN+"'";
			if(PIn.Long(Db.GetScalar(command))>0) {
				Db.NonQ("ALTER TABLE patient DROP COLUMN `"+PATIENT_PORTAL_EMAIL_COLUMN+"`");
				adoptionResult.WasPortalEmailColumnDropped=true;
				actionLog("Dropped column patient."+PATIENT_PORTAL_EMAIL_COLUMN+".");
			}
			else {
				actionLog("patient."+PATIENT_PORTAL_EMAIL_COLUMN+" is not present.");
			}
		}

		///<summary>Blanks the listed HQ preferences and any preference whose value is a web address on one of Open Dental's hosts. Returns the
		///names of the preferences changed.</summary>
		private static List<string> BlankHqUrlPrefs() {
			List<string> listBlanked=new List<string>();
			DataTable table=Db.GetTable("SELECT PrefNum,PrefName,ValueString FROM preference");
			foreach(DataRow row in table.Rows) {
				string prefName=PIn.String(row["PrefName"].ToString());
				string value=PIn.String(row["ValueString"].ToString());
				if(value=="") {
					continue;
				}
				if(!ListHqUrlPrefNames.Contains(prefName,StringComparer.OrdinalIgnoreCase) && !IsHqWebAddress(value)) {
					continue;
				}
				Db.NonQ("UPDATE preference SET ValueString='' WHERE PrefNum="+POut.Long(PIn.Long(row["PrefNum"].ToString())));
				listBlanked.Add(prefName);
			}
			return listBlanked;
		}

		///<summary>True when the whole value is one http or https address on one of Open Dental's hosts. Text that merely mentions a host (an e-mail
		///template, say) is left alone.</summary>
		public static bool IsHqWebAddress(string value) {
			string trimmed=(value??"").Trim();
			if(!trimmed.StartsWith("http://",StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("https://",StringComparison.OrdinalIgnoreCase)) {
				return false;
			}
			if(trimmed.Any(x => char.IsWhiteSpace(x))) {
				return false;
			}
			return HqHosts.IsBlocked(trimmed);
		}

		///<summary>The source version from a ForkAdoptedFrom value ("source=25.3.48.0;started=...;adopted=..."), or null.</summary>
		public static Version ParseAdoptedFromSource(string adoptedFrom) {
			return ForkVersions.ParseVersion(ParseAdoptedFromField(adoptedFrom,"source"));
		}

		///<summary>One field of a ForkAdoptedFrom value, or null.</summary>
		public static string ParseAdoptedFromField(string adoptedFrom,string field) {
			return ForkVersions.ParseAdoptedFromField(adoptedFrom,field);
		}

		private static string FormatUtc(DateTime dateTimeUtc) {
			return dateTimeUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",CultureInfo.InvariantCulture);
		}
	}
}
