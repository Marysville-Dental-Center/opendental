// Runs the M1-02 seed rows (Seed25_3) as a fork migration step, after the M1-01 schema steps.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenDentBusiness.ForkMigrations {
	///<summary>The preference, permission-grant and eForm status rows of Seed25_3, for databases that come from 24.3. It runs after the 25.3.48
	///schema steps (OdParity25_3.cs, levels 2026101901 to 2026101906), because the rows it writes belong to that schema.
	///Where the database comes from: adoption sets DataBaseVersion to the sentinel before it runs the steps, so an adopted database is judged by the
	///source in ForkAdoptedFrom; any other database by DataBaseVersion. An adopted Open Dental 25.3.48 database is never seeded.</summary>
	[ForkMigration(2026101907,"Open Dental 25.3 seed rows for databases from 24.3")]
	public class Seed25_3Step:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			if(!IsFrom24_3(context)) {
				return;
			}
			foreach(string change in Seed25_3.RunSteps()) {
				context.Log(change);
			}
		}

		///<summary>True when the database comes from 24.3 or earlier. Unknown origins return false, so they are never seeded.</summary>
		public static bool IsFrom24_3(ForkMigrationContext context) {
			string adoptedFrom=context.GetPrefRaw(ForkVersions.PREF_FORK_ADOPTED_FROM);
			if(adoptedFrom!=null) {
				Version versionSource=Adoption.ParseAdoptedFromSource(adoptedFrom);
				return versionSource!=null && versionSource<Seed25_3.VersionFirstAfter24_3;
			}
			return Seed25_3.IsVersionFrom24_3(context.GetPrefRaw("DataBaseVersion"));
		}
	}
}
