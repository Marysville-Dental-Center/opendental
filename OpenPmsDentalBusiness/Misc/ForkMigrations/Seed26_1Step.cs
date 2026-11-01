// Runs the M2-02 seed rows (Seed26_1) as a fork migration step.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness.ForkMigrations.Seeds26_1 {
	///<summary>The preference rows and the 25.4 blockout permission split of Seed26_1. Level 2026102501 (range 2026102501 to 2026102549, package
	///M2-02).
	///<para>Preference rows are inserted on every database that lacks them and never changed afterwards. The permission split runs only on a
	///database that comes from before 25.4, judged like Seed25_3Step: by the source in ForkAdoptedFrom on an adopted database, otherwise by
	///DataBaseVersion; an unknown origin is never converted. It runs once per database: when the runner already recorded this level (a RunAll
	///rerun), it is skipped, because by then 272 means BlockoutAdd and must not be renumbered again.</para></summary>
	[ForkMigration(2026102501,"Open Dental 25.4 and 26.1 seed rows")]
	public class Seed26_1Step:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			foreach(string change in Seed26_1.InsertMissingPrefs()) {
				context.Log(change);
			}
			if(!IsFromBefore25_4(context)) {
				return;
			}
			if(ForkMigrationRunner.GetAppliedLevels().Contains(Level)) {
				return;
			}
			foreach(string change in Seed26_1.RunPermissionSplit()) {
				context.Log(change);
			}
		}

		///<summary>True when the database comes from before Open Dental 25.4. Unknown origins return false.</summary>
		public static bool IsFromBefore25_4(ForkMigrationContext context) {
			string adoptedFrom=context.GetPrefRaw(ForkVersions.PREF_FORK_ADOPTED_FROM);
			if(adoptedFrom!=null) {
				Version versionSource=Adoption.ParseAdoptedFromSource(adoptedFrom);
				return versionSource!=null && versionSource<Seed26_1.VersionFirst25_4;
			}
			return Seed26_1.IsVersionBefore25_4(context.GetPrefRaw("DataBaseVersion"));
		}
	}
}
