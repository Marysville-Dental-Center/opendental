// B-SHELL: data steps for the main window package. Levels 2027002301 to 2027002399 are reserved for B-SHELL.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenDentBusiness.ForkMigrations.FeatureSteps {
	//Feature package data steps, not 25.3.48 schema steps: they live in the FeatureSteps namespace (see XConnectEnableStep).

	///<summary>v25_2-bridges-dxc-credit-score-removed. Open Dental 25.2 removed the "DXC Patient Credit Score" program link because DentalXChange
	///discontinued the service; the bridge posted the ClaimConnect clearinghouse login and password to a registration page. Our bridge code is gone
	///too. This step keeps the program row (migrated data refers to it by ProgName) but disables it and deletes its toolbar buttons, so no
	///button shows anywhere. Program Links shows the row as "(discontinued)" (ProgramLinksDiscontinued); the description in the row is not changed.
	///Runs on every database, including an adopted Open Dental 25.3.48 one, because what Open Dental's own 25.2 conversion did to the row is not
	///public (INFERRED). Where the row is already disabled and has no buttons, as Open Dental most likely left it, nothing changes. Each statement
	///runs only when it has rows to change, so a second run changes nothing.</summary>
	[ForkMigration(2027002301,"Disable the discontinued DXC Patient Credit Score program link")]
	public class DxcCreditScoreRemovedStep:ForkMigrationStep {
		public const string PROG_NAME="DXCPatientCreditScore";

		public override void Apply(ForkMigrationContext context) {
			if(!context.TableExists("program")) {
				return;
			}
			string whereProgram="ProgName='"+PROG_NAME+"'";
			if(context.TableExists("toolbutitem")) {
				string whereButtons="ProgramNum IN (SELECT ProgramNum FROM program WHERE "+whereProgram+")";
				if(PIn.Long(Db.GetScalar("SELECT COUNT(*) FROM toolbutitem WHERE "+whereButtons))>0) {
					context.ExecuteData("DELETE FROM toolbutitem WHERE "+whereButtons);
				}
			}
			if(PIn.Long(Db.GetScalar("SELECT COUNT(*) FROM program WHERE "+whereProgram+" AND Enabled!=0"))>0) {
				context.ExecuteData("UPDATE program SET Enabled=0 WHERE "+whereProgram+" AND Enabled!=0");
			}
		}
	}
}
