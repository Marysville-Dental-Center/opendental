// B-SHELL: data steps for the main window package. Level 2026110101 (renumbered on 2026-11-01 from the retired package range level 2027002301).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace OpenPmsDentalBusiness.ForkMigrations.FeatureSteps {
	//Feature package data steps, not 25.3.48 schema steps: they live in the FeatureSteps namespace (see XConnectEnableStep).

	///<summary>v25_2-bridges-dxc-credit-score-removed. Open Dental 25.2 removed the "DXC Patient Credit Score" program link because DentalXChange
	///discontinued the service; the bridge posted the ClaimConnect clearinghouse login and password to a registration page. Our bridge code is gone
	///too. This step keeps the program row (migrated data refers to it by ProgName) but disables it and deletes its toolbar buttons, so no
	///button shows anywhere. Program Links shows the row as "(discontinued)" (ProgramLinksDiscontinued); the description in the row is not changed.
	///Runs on every database, including an adopted Open Dental 25.3.48 one, because what Open Dental's own 25.2 conversion did to the row is not
	///public (INFERRED). Where the row is already disabled and has no buttons, as Open Dental most likely left it, nothing changes. The rule is
	///DxcCreditScoreRemoved.Plan, which AdoptionValidation's migration-preview prints and adopt's row check holds the adopted copy to; a second run
	///finds nothing to change.</summary>
	[ForkMigration(2026110101,"Disable the discontinued DXC Patient Credit Score program link")]
	public class DxcCreditScoreRemovedStep:ForkMigrationStep {
		public const string PROG_NAME=DxcCreditScoreRemoved.PROG_NAME;

		public override void Apply(ForkMigrationContext context) {
			if(!context.TableExists("program")) {
				return;
			}
			List<DxcCreditScoreRemoved.ToolButItemRow> listButtons=context.TableExists("toolbutitem")
				? DxcCreditScoreRemoved.ReadToolButItems()
				: new List<DxcCreditScoreRemoved.ToolButItemRow>();
			DxcCreditScoreRemoved.RowPlan plan=DxcCreditScoreRemoved.Plan(DxcCreditScoreRemoved.ReadPrograms(),listButtons);
			foreach(string change in DxcCreditScoreRemoved.Run(plan)) {
				context.Log(change);
			}
		}
	}

	///<summary>The rule of DxcCreditScoreRemovedStep, worked out without writing (Plan) and applied (Run). The step, AdoptionValidation's
	///migration-preview (runbook step 0.2) and adopt's row check share it, so the three never disagree, as ReportRowUdsPlus.Plan and
	///InsHistCodeGroupsMigration.CreatePlan are shared for their steps. On the program rows of the DXC Patient Credit Score link (ProgName
	///DXCPatientCreditScore, compared as MariaDB compares it: case and trailing spaces ignored): each enabled one gets Enabled=0, and nothing else
	///in program changes; every toolbutitem row of one of them is deleted, and no other toolbutitem row.</summary>
	public static class DxcCreditScoreRemoved {
		public const string PROG_NAME="DXCPatientCreditScore";

		///<summary>The columns of a program row the plan reads.</summary>
		public class ProgramRow {
			public long ProgramNum;
			public string ProgName="";
			public bool IsEnabled;
		}

		///<summary>The columns of a toolbutitem row the plan reads.</summary>
		public class ToolButItemRow {
			public long ToolButItemNum;
			public long ProgramNum;
			public int ToolBar;
			public string ButtonText="";
		}

		///<summary>What the step does on one database.</summary>
		public class RowPlan {
			///<summary>Every program row of the DXC link, in ProgramNum order (normally one). They are kept.</summary>
			public List<ProgramRow> ListPrograms=new List<ProgramRow>();
			///<summary>The ones the step disables (Enabled=0, no other column): those that are enabled.</summary>
			public List<ProgramRow> ListProgramsDisabled=new List<ProgramRow>();
			///<summary>The toolbutitem rows the step deletes, in ToolButItemNum order: every button of a DXC row, enabled or not.</summary>
			public List<ToolButItemRow> ListButtonsDeleted=new List<ToolButItemRow>();

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

		///<summary>True for the ProgName of the DXC link, compared as the database compares it (utf8 general collation: case and trailing spaces
		///ignored).</summary>
		public static bool IsDxcProgName(string progName) {
			return string.Equals((progName??"").TrimEnd(' '),PROG_NAME,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>The plan for the given rows (every program row and every toolbutitem row of the database). The lists are not changed.</summary>
		public static RowPlan Plan(List<ProgramRow> listPrograms,List<ToolButItemRow> listButtons) {
			RowPlan plan=new RowPlan();
			plan.ListPrograms=(listPrograms??new List<ProgramRow>()).Where(x => IsDxcProgName(x.ProgName)).OrderBy(x => x.ProgramNum).ToList();
			plan.ListProgramsDisabled=plan.ListPrograms.Where(x => x.IsEnabled).ToList();
			HashSet<long> setProgramNums=new HashSet<long>(plan.ListPrograms.Select(x => x.ProgramNum));
			plan.ListButtonsDeleted=(listButtons??new List<ToolButItemRow>()).Where(x => setProgramNums.Contains(x.ProgramNum)).OrderBy(x => x.ToolButItemNum).ToList();
			return plan;
		}

		///<summary>Every program row of the connected database, in ProgramNum order.</summary>
		public static List<ProgramRow> ReadPrograms() {
			DataTable table=Db.GetTable("SELECT ProgramNum,ProgName,Enabled FROM program ORDER BY ProgramNum");
			List<ProgramRow> listPrograms=new List<ProgramRow>();
			for(int i=0;i<table.Rows.Count;i++) {
				listPrograms.Add(new ProgramRow {
					ProgramNum=PIn.Long(table.Rows[i]["ProgramNum"].ToString()),
					ProgName=table.Rows[i]["ProgName"].ToString(),
					IsEnabled=PIn.Bool(table.Rows[i]["Enabled"].ToString()),//Any value but 0 ("True" when the driver reads tinyint as bool).
				});
			}
			return listPrograms;
		}

		///<summary>Every toolbutitem row of the connected database, in ToolButItemNum order.</summary>
		public static List<ToolButItemRow> ReadToolButItems() {
			DataTable table=Db.GetTable("SELECT ToolButItemNum,ProgramNum,ToolBar,ButtonText FROM toolbutitem ORDER BY ToolButItemNum");
			List<ToolButItemRow> listButtons=new List<ToolButItemRow>();
			for(int i=0;i<table.Rows.Count;i++) {
				listButtons.Add(new ToolButItemRow {
					ToolButItemNum=PIn.Long(table.Rows[i]["ToolButItemNum"].ToString()),
					ProgramNum=PIn.Long(table.Rows[i]["ProgramNum"].ToString()),
					ToolBar=PIn.Int(table.Rows[i]["ToolBar"].ToString()),
					ButtonText=table.Rows[i]["ButtonText"].ToString(),
				});
			}
			return listButtons;
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

		///<summary>The log line of the deleted buttons, for example "Deleted 2 toolbar button(s) of the discontinued DXC Patient Credit Score program
		///link (ToolButItemNum 4, 5)".</summary>
		public static string DescribeDelete(List<ToolButItemRow> listButtons) {
			return "Deleted "+listButtons.Count+" toolbar button(s) of the discontinued DXC Patient Credit Score program link (ToolButItemNum "
				+string.Join(", ",listButtons.Select(x => x.ToolButItemNum))+")";
		}

		///<summary>The log line of a disabled row, for example "Disabled the discontinued DXC Patient Credit Score program link (ProgramNum 12)".</summary>
		public static string DescribeDisable(ProgramRow program) {
			return "Disabled the discontinued DXC Patient Credit Score program link (ProgramNum "+program.ProgramNum+")";
		}
	}
}
