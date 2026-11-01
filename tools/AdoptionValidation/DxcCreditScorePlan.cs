// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;
using SyntheticData.Infrastructure;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	///<summary>What the fork migration's DXC Patient Credit Score step (DxcCreditScoreRemovedStep, backlog package B-SHELL: Open Dental 25.2 removed
	///the discontinued program link) changes when adoption brings an Open Dental 25.3.48 database to 26.1.70, worked out before adoption and
	///read-only. On every database, an adopted office copy included (what Open Dental's own 25.2 conversion did to the row is not public, INFERRED),
	///the step sets Enabled=0 on each enabled program row of the link (ProgName DXCPatientCreditScore) and deletes every toolbutitem row of such a
	///row; the row itself and its text are kept. The plan is the step's own (DxcCreditScoreRemoved.Plan) on the rows read here with plain SQL.
	///migration-preview prints it (runbook step 0.2), and adopt's row check (AdoptionRowCheck) holds the adopted copy's program and toolbutitem rows
	///to the plan made from the original. Reads program and toolbutitem.</summary>
	public static class DxcCreditScorePlan {
		///<summary>The one program column the step changes. allowed-differences.adoption.json allows it as a whole column (ColumnChanged), and
		///adopt's row check holds every program row to the plan.</summary>
		public const string TABLE_PROGRAM="program";
		public const string COLUMN_ENABLED="Enabled";
		///<summary>The table whose rows the step deletes. allowed-differences.adoption.json allows it as a whole table (a deleted row changes every
		///column checksum), and adopt's row check holds every row to the plan.</summary>
		public const string TABLE_TOOLBUTITEM="toolbutitem";

		///<summary>Level of the step, for messages.</summary>
		public static long StepLevel {
			get { return new DxcCreditScoreRemovedStep().Level; }
		}

		public class Plan {
			///<summary>False when the database has no program table: the step then changes nothing (on a real database it always has one).</summary>
			public bool HasProgramTable;
			public bool HasToolButItemTable;
			///<summary>The step's plan.</summary>
			public DxcCreditScoreRemoved.RowPlan Rows=new DxcCreditScoreRemoved.RowPlan();

			///<summary>True when the step changes rows of this database.</summary>
			public bool IsChanging {
				get { return HasProgramTable && Rows.IsChanging; }
			}

			///<summary>What migration-preview prints for the step.</summary>
			public List<string> Lines() {
				string step="Step "+StepLevel+" (DXC Patient Credit Score program link)";
				List<string> listLines=new List<string>();
				if(!HasProgramTable) {
					listLines.Add(step+" changes nothing: there is no program table.");
					return listLines;
				}
				if(Rows.ListPrograms.Count==0) {
					listLines.Add(step+" changes nothing: no program row is the DXC Patient Credit Score link (ProgName "+DxcCreditScoreRemoved.PROG_NAME+").");
					return listLines;
				}
				string rows="ProgramNum "+string.Join(", ",Rows.ListPrograms.Select(x => x.ProgramNum));
				if(!Rows.IsChanging) {
					listLines.Add(step+" changes nothing: the discontinued DXC Patient Credit Score program link ("+rows+") is already disabled and has no toolbar "
						+"button.");
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
				listLines.Add(step+": the discontinued DXC Patient Credit Score program link ("+rows+") stays, with its text; the step "+string.Join(" and ",listParts)
					+" (on every database, an Open Dental 25.3.48 one included). The adoption log says "+string.Join(" and ",Rows.GetLogLines().Select(x => "\""+x+"\""))
					+"; adopt's row check (step 4) requires exactly this: no other program row or column and no other toolbutitem row changes.");
				return listLines;
			}
		}

		///<summary>The plan for a database on this server, read with plain SQL.</summary>
		public static Plan Make(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			Plan plan=new Plan();
			plan.HasProgramTable=Migration26_1Plan.HasTable(db,database,TABLE_PROGRAM);
			plan.HasToolButItemTable=Migration26_1Plan.HasTable(db,database,TABLE_TOOLBUTITEM);
			if(!plan.HasProgramTable) {
				return plan;
			}
			plan.Rows=DxcCreditScoreRemoved.Plan(ReadPrograms(db,database),
				plan.HasToolButItemTable ? ReadToolButItems(db,database) : new List<DxcCreditScoreRemoved.ToolButItemRow>());
			return plan;
		}

		///<summary>Every program row, with the columns the step reads, in ProgramNum order.</summary>
		public static List<DxcCreditScoreRemoved.ProgramRow> ReadPrograms(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			return db.Rows("SELECT ProgramNum,ProgName,Enabled FROM `"+database+"`.program ORDER BY ProgramNum").Select(x => new DxcCreditScoreRemoved.ProgramRow {
				ProgramNum=Convert.ToInt64(x[0],CultureInfo.InvariantCulture),
				ProgName=Convert.ToString(x[1],CultureInfo.InvariantCulture)??"",
				IsEnabled=Convert.ToInt64(x[2],CultureInfo.InvariantCulture)!=0,
			}).ToList();
		}

		///<summary>Every toolbutitem row, with the columns the step reads, in ToolButItemNum order.</summary>
		public static List<DxcCreditScoreRemoved.ToolButItemRow> ReadToolButItems(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			return db.Rows("SELECT ToolButItemNum,ProgramNum,ToolBar,ButtonText FROM `"+database+"`.toolbutitem ORDER BY ToolButItemNum")
				.Select(x => new DxcCreditScoreRemoved.ToolButItemRow {
					ToolButItemNum=Convert.ToInt64(x[0],CultureInfo.InvariantCulture),
					ProgramNum=Convert.ToInt64(x[1],CultureInfo.InvariantCulture),
					ToolBar=Convert.ToInt32(x[2],CultureInfo.InvariantCulture),
					ButtonText=Convert.ToString(x[3],CultureInfo.InvariantCulture)??"",
				}).ToList();
		}
	}
}
