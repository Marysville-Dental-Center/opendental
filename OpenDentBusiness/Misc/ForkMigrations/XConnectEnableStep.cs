// V-DXC1: keeps XConnect on for offices that already used it in 24.3, now that it has its own switch (clearinghouse.EnableXConnect, Open Dental 25.2).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenDentBusiness.ForkMigrations.FeatureSteps {
	//A feature package's data step, not one of the 25.3.48 schema steps: it lives in its own namespace, so it is not held to the rule that the
	//schema steps come before Seed25_3Step (HandOffIntegrationTests). The runner finds steps in any namespace.
	///<summary>In 24.3, XConnect was on for a ClaimConnect clearinghouse whenever its LocationID (the XConnect API key) was not blank. From 25.2 it
	///also needs clearinghouse.EnableXConnect, which the 25.2 schema step (OdParity25_3.cs, level 2026101903) adds with a default of 0. This step
	///sets it to 1 where 24.3 had XConnect on, so attachments keep going through XConnect:
	///- HQ rows (ClinicNum 0) with CommBridge ClaimConnect and a LocationID;
	///- clinic override rows whose HQ row is ClaimConnect and whose own or inherited LocationID is not blank (a blank override inherits the HQ key).
	///Only for a database that comes from 24.3 (Seed25_3Step.IsFrom24_3). An Open Dental 25.3.48 database already carries the office's own
	///setting and is never changed.</summary>
	[ForkMigration(2026101951,"XConnect switch for clearinghouses that used XConnect in 24.3")]
	public class XConnectEnableStep:ForkMigrationStep {
		///<summary>EclaimsCommBridge.ClaimConnect.</summary>
		private const int COMM_BRIDGE_CLAIMCONNECT=4;

		public override void Apply(ForkMigrationContext context) {
			if(!Seed25_3Step.IsFrom24_3(context)) {
				return;
			}
			if(!context.ColumnExists("clearinghouse","EnableXConnect")) {
				throw new ApplicationException("clearinghouse.EnableXConnect is missing; the 25.2 schema step must run first.");
			}
			//Statements run (and are logged) only when they have rows to change, so running the step again changes and logs nothing.
			string whereHq="WHERE ClinicNum=0 AND CommBridge="+COMM_BRIDGE_CLAIMCONNECT+" AND TRIM(LocationID)!='' AND EnableXConnect=0";
			if(PIn.Long(Db.GetScalar("SELECT COUNT(*) FROM clearinghouse "+whereHq))>0) {
				context.ExecuteData("UPDATE clearinghouse SET EnableXConnect=1 "+whereHq);
			}
			string joinClin="FROM clearinghouse clin "
				+"INNER JOIN clearinghouse hq ON hq.ClearinghouseNum=clin.HqClearinghouseNum AND hq.ClinicNum=0 ";
			string whereClin="WHERE clin.ClinicNum!=0 AND hq.CommBridge="+COMM_BRIDGE_CLAIMCONNECT+" "
				+"AND (TRIM(clin.LocationID)!='' OR TRIM(hq.LocationID)!='') AND clin.EnableXConnect=0";
			if(PIn.Long(Db.GetScalar("SELECT COUNT(*) "+joinClin+whereClin))>0) {
				context.ExecuteData("UPDATE clearinghouse clin "
					+"INNER JOIN clearinghouse hq ON hq.ClearinghouseNum=clin.HqClearinghouseNum AND hq.ClinicNum=0 "
					+"SET clin.EnableXConnect=1 "+whereClin);
			}
		}
	}
}
