// B-PRINT: the preference row behind "Print inserter barcode on statements" in Billing Defaults (Open Dental 25.3 statement barcode).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenDentBusiness.ForkMigrations.FeatureSteps {
	///<summary>Adds the fork preference BillingStatementInserterBarcode with the value 0 (off) when the row is missing. Open Dental's published
	///documents name no preference for the 25.3 statement barcode, so the row is the fork's own (StatementInserterBarcode.PREF_NAME). The step never
	///changes an existing row, so running it again, or on a database where the office already turned the barcode on, changes nothing.
	///It is a data step in a feature package's level range (B-PRINT: 2027001501 to 2027001599); it adds no schema.</summary>
	[ForkMigration(2027001501,"Statement inserter barcode preference")]
	public class InserterBarcodePrefStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			if(context.GetPrefRaw(StatementInserterBarcode.PREF_NAME)!=null) {
				return;
			}
			context.ExecuteData("INSERT INTO preference (PrefName,ValueString) VALUES('"+POut.String(StatementInserterBarcode.PREF_NAME)+"','0')");
		}
	}
}
