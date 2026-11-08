// P262-LINKS: data step for the Open Dental 26.2 program link changes. Level 2026110781 (the package's range is 2026110781 to 2026110789).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness.ForkMigrations.FeatureSteps {
	///<summary>Open Dental 26.2 item 46, "Deprecated Trojan Express Collect bridge": the public Trojan Express Collect pages say the service is no
	///longer offered. Our bridge code is gone (its window wrote collection requests, with the guarantor's Social Security number, to a folder that
	///Trojan's own communicator program sent on). This step does what DxcCreditScoreRemovedStep (B-SHELL, 2026110101) did for the DXC Patient
	///Credit Score link: it keeps the program row (migrated data refers to it by ProgName, and its properties stay) but disables it and deletes its
	///toolbar buttons, so no button shows anywhere. Program Links shows the row as "(discontinued)" (ProgramLinksDiscontinued); the description in
	///the row is not changed. Runs on every database, an adopted Open Dental 25.3.48 one included, because what Open Dental's own 26.2 conversion
	///does to the row is not public (INFERRED). Where the row is already disabled and has no buttons (a new database, or an office that never used
	///the link) nothing changes. The rule is TrojanExpressCollectRemoved.Plan, which AdoptionValidation's migration-preview prints and adopt's row
	///check holds the adopted copy to; a second run finds nothing to change.</summary>
	[ForkMigration(2026110781,"Disable the discontinued Trojan Express Collect program link")]
	public class TrojanExpressCollectRemovedStep:ForkMigrationStep {
		public const string PROG_NAME=TrojanExpressCollectRemoved.PROG_NAME;

		public override void Apply(ForkMigrationContext context) {
			if(!context.TableExists("program")) {
				return;
			}
			List<DxcCreditScoreRemoved.ToolButItemRow> listButtons=context.TableExists("toolbutitem")
				? DxcCreditScoreRemoved.ReadToolButItems()
				: new List<DxcCreditScoreRemoved.ToolButItemRow>();
			TrojanExpressCollectRemoved.RowPlan plan=TrojanExpressCollectRemoved.Plan(DxcCreditScoreRemoved.ReadPrograms(),listButtons);
			foreach(string change in TrojanExpressCollectRemoved.Run(plan)) {
				context.Log(change);
			}
		}
	}

	///<summary>The rule of TrojanExpressCollectRemovedStep, worked out without writing (Plan) and applied (Run), shared by the step, AdoptionValidation's
	///migration-preview and adopt's row check (as DxcCreditScoreRemoved is for the DXC step). It reads the same rows as the DXC rule
	///(DxcCreditScoreRemoved.ProgramRow and ToolButItemRow, read by its ReadPrograms and ReadToolButItems). On the program rows of the Trojan Express
	///Collect link (ProgName TrojanExpressCollect, compared as MariaDB compares it: case and trailing spaces ignored): each enabled one gets
	///Enabled=0, and nothing else in program changes; every toolbutitem row of one of them is deleted, and no other toolbutitem row.</summary>
	public static class TrojanExpressCollectRemoved {
		public const string PROG_NAME="TrojanExpressCollect";
		///<summary>The link's name in log lines and messages.</summary>
		public const string LINK_NAME="Trojan Express Collect";

		///<summary>What the step does on one database.</summary>
		public class RowPlan {
			///<summary>Every program row of the link, in ProgramNum order (normally one). They are kept.</summary>
			public List<DxcCreditScoreRemoved.ProgramRow> ListPrograms=new List<DxcCreditScoreRemoved.ProgramRow>();
			///<summary>The ones the step disables (Enabled=0, no other column): those that are enabled.</summary>
			public List<DxcCreditScoreRemoved.ProgramRow> ListProgramsDisabled=new List<DxcCreditScoreRemoved.ProgramRow>();
			///<summary>The toolbutitem rows the step deletes, in ToolButItemNum order: every button of a row of the link, enabled or not.</summary>
			public List<DxcCreditScoreRemoved.ToolButItemRow> ListButtonsDeleted=new List<DxcCreditScoreRemoved.ToolButItemRow>();

			///<summary>True when the step changes rows of this database.</summary>
			public bool IsChanging {
				get { return ListProgramsDisabled.Count>0 || ListButtonsDeleted.Count>0; }
			}

			///<summary>What the step writes to the fork migration log, in order: the deleted buttons, then each disabled row. Empty when it changes
			///nothing.</summary>
			public List<string> GetLogLines() {
				List<string> listLines=new List<string>();
				if(ListButtonsDeleted.Count>0) {
					listLines.Add(DescribeDelete(ListButtonsDeleted));
				}
				listLines.AddRange(ListProgramsDisabled.Select(x => DescribeDisable(x)));
				return listLines;
			}
		}

		///<summary>True for the ProgName of the link, compared as the database compares it (utf8 general collation: case and trailing spaces ignored).
		///</summary>
		public static bool IsTrojanExpressCollectProgName(string progName) {
			return string.Equals((progName??"").TrimEnd(' '),PROG_NAME,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>The plan for the given rows (every program row and every toolbutitem row of the database). The lists are not changed.</summary>
		public static RowPlan Plan(List<DxcCreditScoreRemoved.ProgramRow> listPrograms,List<DxcCreditScoreRemoved.ToolButItemRow> listButtons) {
			RowPlan plan=new RowPlan();
			plan.ListPrograms=(listPrograms??new List<DxcCreditScoreRemoved.ProgramRow>()).Where(x => IsTrojanExpressCollectProgName(x.ProgName))
				.OrderBy(x => x.ProgramNum).ToList();
			plan.ListProgramsDisabled=plan.ListPrograms.Where(x => x.IsEnabled).ToList();
			HashSet<long> setProgramNums=new HashSet<long>(plan.ListPrograms.Select(x => x.ProgramNum));
			plan.ListButtonsDeleted=(listButtons??new List<DxcCreditScoreRemoved.ToolButItemRow>()).Where(x => setProgramNums.Contains(x.ProgramNum))
				.OrderBy(x => x.ToolButItemNum).ToList();
			return plan;
		}

		///<summary>Writes the plan with plain DELETE and UPDATE statements on the planned rows only, and returns the lines for the fork migration log
		///(RowPlan.GetLogLines).</summary>
		public static List<string> Run(RowPlan plan) {
			if(plan==null || !plan.IsChanging) {
				return new List<string>();
			}
			if(plan.ListButtonsDeleted.Count>0) {
				Db.NonQ("DELETE FROM toolbutitem WHERE ToolButItemNum IN ("+string.Join(",",plan.ListButtonsDeleted.Select(x => POut.Long(x.ToolButItemNum)))+")");
			}
			if(plan.ListProgramsDisabled.Count>0) {
				Db.NonQ("UPDATE program SET Enabled=0 WHERE ProgramNum IN ("+string.Join(",",plan.ListProgramsDisabled.Select(x => POut.Long(x.ProgramNum)))+")");
			}
			return plan.GetLogLines();
		}

		///<summary>The log line of the deleted buttons, for example "Deleted 2 toolbar button(s) of the discontinued Trojan Express Collect program link
		///(ToolButItemNum 4, 5)".</summary>
		public static string DescribeDelete(List<DxcCreditScoreRemoved.ToolButItemRow> listButtons) {
			return "Deleted "+listButtons.Count+" toolbar button(s) of the discontinued "+LINK_NAME+" program link (ToolButItemNum "
				+string.Join(", ",listButtons.Select(x => x.ToolButItemNum))+")";
		}

		///<summary>The log line of a disabled row, for example "Disabled the discontinued Trojan Express Collect program link (ProgramNum 12)".</summary>
		public static string DescribeDisable(DxcCreditScoreRemoved.ProgramRow program) {
			return "Disabled the discontinued "+LINK_NAME+" program link (ProgramNum "+program.ProgramNum+")";
		}
	}
}
