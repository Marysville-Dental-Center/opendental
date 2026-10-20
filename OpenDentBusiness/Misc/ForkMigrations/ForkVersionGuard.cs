// The startup version guard (backlog Risk R1, package M1-03). Pure decision logic, so every case is unit tested.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;

namespace OpenDentBusiness {
	public enum ForkGuardAction {
		///<summary>An unadopted database on the 24.3.63 line (or older, for the frozen legacy chain to convert). It works as before.</summary>
		ProceedCompat,
		///<summary>An adopted database. Fork migration steps may be pending (ForkGuardResult.HasPendingMigrations).</summary>
		ProceedFork,
		///<summary>An unadopted database on the adoption allowlist, or an adoption that did not finish. Refused at startup; an administrator may run
		///the adoption tool against a restored copy.</summary>
		NeedsAdoption,
		///<summary>An adopted database whose ForkSchemaLevel is newer than this build knows. This workstation needs the newer release.</summary>
		NeedsNewerBuild,
		///<summary>Anything else. Nothing is converted, nothing is installed, no downgrade is offered.</summary>
		Refuse,
	}

	public class ForkGuardResult {
		public ForkGuardAction Action;
		///<summary>Plain-language explanation for the user. Blank when the action is a Proceed action with nothing to say.</summary>
		public string Message="";
		///<summary>Process exit code for silent runs; 0 for the Proceed actions.</summary>
		public int ExitCode;
		public bool HasPendingMigrations;
		///<summary>For NeedsAdoption: true when a started adoption must be finished.</summary>
		public bool IsAdoptionResume;

		public bool CanProceed {
			get { return Action==ForkGuardAction.ProceedCompat || Action==ForkGuardAction.ProceedFork; }
		}
	}

	///<summary>Decides, before any conversion, whether this build may use the database. Runs in Debug and Release builds alike: Open Dental skips its
	///version check in Debug builds, the fork does not, so a developer build pointed at an office copy cannot adopt or convert it by accident.
	///Never reads Open Dental's update files.</summary>
	public class ForkVersionGuard {
		///<summary>Unadopted database of an Open Dental version that is not on the allowlist, or no readable version.</summary>
		public const int EXIT_CODE_UNSUPPORTED_VERSION=170;
		///<summary>Unadopted database on the allowlist: adoption needed.</summary>
		public const int EXIT_CODE_NEEDS_ADOPTION=171;
		///<summary>Adopted database with a newer fork schema level than this build.</summary>
		public const int EXIT_CODE_NEWER_SCHEMA=172;
		///<summary>Fork markers and version preferences disagree.</summary>
		public const int EXIT_CODE_INCONSISTENT=173;
		///<summary>This workstation runs a newer release than the update server published.</summary>
		public const int EXIT_CODE_RELEASE_NEWER_THAN_PUBLISHED=174;
		///<summary>The published release could not be downloaded or failed its SHA-256 checks.</summary>
		public const int EXIT_CODE_RELEASE_INVALID=175;
		///<summary>A fork migration step failed.</summary>
		public const int EXIT_CODE_MIGRATION_FAILED=176;

		///<summary>Evaluates the database state with the registered fork migration runner.</summary>
		public static ForkGuardResult Evaluate(ForkDbState forkDbState) {
			return Evaluate(forkDbState,ForkMigrationHandOff.Runner);
		}

		public static ForkGuardResult Evaluate(ForkDbState forkDbState,IForkMigrationRunner runner) {
			if(forkDbState==null) {
				throw new ArgumentNullException(nameof(forkDbState));
			}
			runner=runner??new BaselineForkMigrationRunner();
			Version versionDb=ForkVersions.ParseVersion(forkDbState.DataBaseVersion);
			Version versionProgram=ForkVersions.ParseVersion(forkDbState.ProgramVersion);
			if(forkDbState.IsForkMarked) {
				return EvaluateMarked(forkDbState,versionDb,versionProgram,runner);
			}
			if(forkDbState.IsAdoptedWithMarkersMissing) {
				//ForkAdoptedFrom says the adoption finished, but ForkSchemaLevel is gone. Not an unfinished adoption: never resumed, never migrated again.
				return Result(ForkGuardAction.Refuse,EXIT_CODE_INCONSISTENT,
					"This database was adopted by this program ("+forkDbState.ForkAdoptedFrom+"), but its version markers are missing (ForkSchemaLevel '"
					+(forkDbState.ForkSchemaLevel??"missing")+"', OdSchemaCompat '"+(forkDbState.OdSchemaCompat??"missing")+"').\r\n"
					+"Restore a backup of the adopted database. Do not edit the version preferences by hand. Nothing was changed.");
			}
			if(forkDbState.IsAdoptionIncomplete) {
				return Result(ForkGuardAction.NeedsAdoption,EXIT_CODE_NEEDS_ADOPTION,
					"An adoption of this database was started but did not finish ("+forkDbState.ForkAdoptedFrom+").\r\n"
					+"Run the adoption tool again on this copy to finish it, or restore the backup taken before the adoption.",isResume:true);
			}
			if(versionDb==null) {
				return Result(ForkGuardAction.Refuse,EXIT_CODE_UNSUPPORTED_VERSION,
					"The database has no valid DataBaseVersion preference ("+(forkDbState.DataBaseVersion??"missing")+"), so its schema is unknown.");
			}
			if(versionDb==ForkVersions.SentinelVersion || (versionProgram!=null && versionProgram==ForkVersions.SentinelVersion)) {
				return Result(ForkGuardAction.Refuse,EXIT_CODE_INCONSISTENT,
					"This database was adopted by this program, but its version markers (ForkSchemaLevel, OdSchemaCompat) are missing.\r\n"
					+"Restore a backup of the adopted database. Do not edit the version preferences by hand.");
			}
			if(versionDb>ForkVersions.CompatVersion) {
				if(ForkVersions.IsAdoptable(versionDb)) {
					return Result(ForkGuardAction.NeedsAdoption,EXIT_CODE_NEEDS_ADOPTION,
						"This is an Open Dental "+versionDb.ToString(3)+" database that has not been adopted.\r\n"
						+"Normal startup never converts an Open Dental database. An administrator adopts a restored copy with the adoption tool "
						+"(see docs/release-process.md). After adoption, Open Dental programs can no longer use that copy.");
				}
				return Result(ForkGuardAction.Refuse,EXIT_CODE_UNSUPPORTED_VERSION,
					"This database is from Open Dental "+versionDb.ToString(3)+", which this program does not support.\r\n"
					+"Only these Open Dental versions can be adopted: "+string.Join(", ",ForkVersions.ListAdoptableVersions.Select(x => x.ToString(3)))
					+".\r\nNothing was changed.");
			}
			//At or below 24.3.63: the database is on our compatibility line. The frozen legacy chain converts older ones.
			if(versionProgram!=null && versionProgram>ForkVersions.CompatVersion) {
				return Result(ForkGuardAction.Refuse,EXIT_CODE_UNSUPPORTED_VERSION,
					"The database says it is at version "+versionDb.ToString(3)+", but a newer program ("+versionProgram.ToString(3)+") last updated it.\r\n"
					+"It may be part way through an Open Dental update. Nothing was changed.");
			}
			if(versionDb==ForkVersions.CompatVersion && ForkMigrationHandOff.HasSteps(runner)) {
				return Result(ForkGuardAction.NeedsAdoption,EXIT_CODE_NEEDS_ADOPTION,
					"This database is on the 24.3.63 line and has no fork version markers yet. This build needs them for its schema updates.\r\n"
					+"An administrator runs the adoption tool once on this database (see docs/release-process.md).");
			}
			return Result(ForkGuardAction.ProceedCompat,0,"");
		}

		private static ForkGuardResult EvaluateMarked(ForkDbState forkDbState,Version versionDb,Version versionProgram,IForkMigrationRunner runner) {
			if(versionDb!=ForkVersions.SentinelVersion || versionProgram!=ForkVersions.SentinelVersion) {
				return Result(ForkGuardAction.Refuse,EXIT_CODE_INCONSISTENT,
					"This database was adopted by this program, but DataBaseVersion ("+(forkDbState.DataBaseVersion??"missing")+") or ProgramVersion ("
					+(forkDbState.ProgramVersion??"missing")+") no longer holds the adoption value "+ForkVersions.SentinelVersion+".\r\n"
					+"Another program may have used this database. Nothing was changed. Restore a backup or check the database before continuing.");
			}
			if(string.IsNullOrWhiteSpace(forkDbState.OdSchemaCompat) || string.IsNullOrWhiteSpace(forkDbState.ForkSchemaLevel)) {
				return Result(ForkGuardAction.Refuse,EXIT_CODE_INCONSISTENT,
					"This database was adopted by this program, but its version markers are incomplete (ForkSchemaLevel '"+forkDbState.ForkSchemaLevel
					+"', OdSchemaCompat '"+forkDbState.OdSchemaCompat+"'). Nothing was changed.");
			}
			int compare=runner.CompareLevels(forkDbState.ForkSchemaLevel,runner.LatestLevel);
			if(compare>0) {
				return Result(ForkGuardAction.NeedsNewerBuild,EXIT_CODE_NEWER_SCHEMA,
					"The database was updated by a newer release of this program (schema level "+forkDbState.ForkSchemaLevel+"; this program knows up to "
					+runner.LatestLevel+").\r\nThis workstation needs the newer release.");
			}
			ForkGuardResult forkGuardResult=Result(ForkGuardAction.ProceedFork,0,"");
			forkGuardResult.HasPendingMigrations=compare<0;
			return forkGuardResult;
		}

		private static ForkGuardResult Result(ForkGuardAction forkGuardAction,int exitCode,string message,bool isResume=false) {
			ForkGuardResult forkGuardResult=new ForkGuardResult();
			forkGuardResult.Action=forkGuardAction;
			forkGuardResult.ExitCode=exitCode;
			forkGuardResult.Message=message;
			forkGuardResult.IsAdoptionResume=isResume;
			return forkGuardResult;
		}
	}
}
