// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;
using SyntheticData.Infrastructure;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	///<summary>What the fork migration's Trojan Express Collect step (TrojanExpressCollectRemovedStep, package P262-LINKS: Open Dental 26.2 deprecated
	///the link because the service is no longer offered) changes when adoption brings an Open Dental 25.3.48 database to 26.2.0, worked out before
	///adoption and read-only. On every database, an adopted office copy included (what Open Dental's own 26.2 conversion does to the row is not
	///public, INFERRED), the step sets Enabled=0 on each enabled program row of the link (ProgName TrojanExpressCollect) and deletes every
	///toolbutitem row of such a row; the row itself, its text and its properties are kept. The plan is the step's own
	///(TrojanExpressCollectRemoved.Plan) on the rows DxcCreditScorePlan reads with plain SQL. migration-preview prints it (runbook step 0.2), and
	///adopt's row check (AdoptionRowCheck) holds the adopted copy's program and toolbutitem rows to it and to the DXC step's plan together. Reads
	///program and toolbutitem.</summary>
	public static class TrojanExpressCollectPlan {
		///<summary>Level of the step, for messages.</summary>
		public static long StepLevel {
			get { return new TrojanExpressCollectRemovedStep().Level; }
		}

		public class Plan {
			///<summary>False when the database has no program table: the step then changes nothing (on a real database it always has one).</summary>
			public bool HasProgramTable;
			public bool HasToolButItemTable;
			///<summary>The step's plan.</summary>
			public TrojanExpressCollectRemoved.RowPlan Rows=new TrojanExpressCollectRemoved.RowPlan();

			///<summary>True when the step changes rows of this database.</summary>
			public bool IsChanging {
				get { return HasProgramTable && Rows.IsChanging; }
			}

			///<summary>What migration-preview prints for the step.</summary>
			public List<string> Lines() {
				string name=TrojanExpressCollectRemoved.LINK_NAME;
				string step="Step "+StepLevel+" ("+name+" program link)";
				List<string> listLines=new List<string>();
				if(!HasProgramTable) {
					listLines.Add(step+" changes nothing: there is no program table.");
					return listLines;
				}
				if(Rows.ListPrograms.Count==0) {
					listLines.Add(step+" changes nothing: no program row is the "+name+" link (ProgName "+TrojanExpressCollectRemoved.PROG_NAME+").");
					return listLines;
				}
				string rows="ProgramNum "+string.Join(", ",Rows.ListPrograms.Select(x => x.ProgramNum));
				if(!Rows.IsChanging) {
					listLines.Add(step+" changes nothing: the discontinued "+name+" program link ("+rows+") is already disabled and has no toolbar button.");
					return listLines;
				}
				List<string> listParts=new List<string>();
				if(Rows.ListProgramsDisabled.Count>0) {
					listParts.Add("disables "+Rows.ListProgramsDisabled.Count+" program row(s) of it (ProgramNum "+string.Join(", ",Rows.ListProgramsDisabled
						.Select(x => x.ProgramNum))+"), Enabled only");
				}
				if(Rows.ListButtonsDeleted.Count>0) {
					listParts.Add("deletes its "+Rows.ListButtonsDeleted.Count+" toolbar button(s) (ToolButItemNum "+string.Join(", ",Rows.ListButtonsDeleted
						.Select(x => x.ToolButItemNum+" on "+(OpenPmsDentalBusiness.EnumToolBar)x.ToolBar))+")");
				}
				listLines.Add(step+": the discontinued "+name+" program link ("+rows+") stays, with its text and properties; the step "+string.Join(" and ",listParts)
					+" (on every database, an Open Dental 25.3.48 one included). The adoption log says "+string.Join(" and ",Rows.GetLogLines().Select(x => "\""+x+"\""))
					+"; adopt's row check (step 4) requires exactly this, with the DXC Patient Credit Score step's rows: no other program row or column and no other "
					+"toolbutitem row changes.");
				return listLines;
			}
		}

		///<summary>The plan for a database on this server, read with plain SQL (the DXC plan's readers: the two steps read the same columns).</summary>
		public static Plan Make(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			Plan plan=new Plan();
			plan.HasProgramTable=Migration26_1Plan.HasTable(db,database,DxcCreditScorePlan.TABLE_PROGRAM);
			plan.HasToolButItemTable=Migration26_1Plan.HasTable(db,database,DxcCreditScorePlan.TABLE_TOOLBUTITEM);
			if(!plan.HasProgramTable) {
				return plan;
			}
			plan.Rows=TrojanExpressCollectRemoved.Plan(DxcCreditScorePlan.ReadPrograms(db,database),
				plan.HasToolButItemTable ? DxcCreditScorePlan.ReadToolButItems(db,database) : new List<DxcCreditScoreRemoved.ToolButItemRow>());
			return plan;
		}
	}
}
