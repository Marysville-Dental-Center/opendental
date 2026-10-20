// Connects the fork migration runner (M1-01) to the version guard and adoption tool (M1-03) through IForkMigrationRunner.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDentBusiness.ForkMigrations {
	///<summary>The IForkMigrationRunner that ForkMigrationHandOff discovers: it hands the version guard, startup (PrefL.ConvertDB) and adoption
	///(Adoption.Run) to ForkMigrationRunner. Levels are the yyyyMMddNN step levels; the baseline "0" written at adoption sorts below every step.
	///ForkMigrationRunner itself advances ForkSchemaLevel after each step. After the steps, OdSchemaCompat is set to the Open Dental version of the
	///schema verifier's reference when the database matches it.</summary>
	public class ForkMigrationRunnerAdapter:IForkMigrationRunner {
		///<summary>The highest step level in this build, or the baseline when there are no steps.</summary>
		public string LatestLevel {
			get {
				List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
				if(listSteps.Count==0) {
					return ForkVersions.SCHEMA_LEVEL_BASELINE;
				}
				return listSteps.Max(x => x.Level).ToString();
			}
		}

		public int CompareLevels(string levelA,string levelB) {
			return BaselineForkMigrationRunner.CompareLevelsDefault(levelA,levelB);
		}

		public void RunPending(Action<string> actionLog) {
			Action<string> log=actionLog??(x => { });
			ForkMigrationResult result=ForkMigrationRunner.Run();
			foreach(ForkMigrationStep step in result.ListStepsRun) {
				log("Fork migration step "+step+(result.ListStepsChanged.Contains(step) ? " changed the database." : " found nothing to change."));
			}
			log("Fork migrations: "+result.ListStepsRun.Count+" step(s) run, "+result.ListStepsSkipped.Count+" already done. Fork schema level "
				+ForkVersions.GetMarker(ForkVersions.PREF_FORK_SCHEMA_LEVEL)+".");
			if(ForkVersions.GetMarker(ForkVersions.PREF_OD_SCHEMA_COMPAT)==null) {
				return;//Not an adopted database: the fork markers do not govern it.
			}
			SchemaVerifyResult schemaVerifyResult=SchemaVerifier.VerifyDatabase();
			if(!schemaVerifyResult.IsMatch) {
				log("The schema differs from Open Dental "+SchemaVerifier.ReferenceVersion+" in "+schemaVerifyResult.ListMismatches.Count
					+" place(s); OdSchemaCompat stays "+ForkVersions.GetMarker(ForkVersions.PREF_OD_SCHEMA_COMPAT)+".");
				return;
			}
			if(ForkVersions.GetMarker(ForkVersions.PREF_OD_SCHEMA_COMPAT)!=SchemaVerifier.ReferenceVersion) {
				ForkVersions.SetMarker(ForkVersions.PREF_OD_SCHEMA_COMPAT,SchemaVerifier.ReferenceVersion);
			}
			log("The schema matches Open Dental "+SchemaVerifier.ReferenceVersion+" (OdSchemaCompat).");
		}
	}
}
