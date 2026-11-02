// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.Features;
using SyntheticData.Infrastructure;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	///<summary>What the fork migration that Adoption.Run runs will change in the rows of an Open Dental 25.3.48 database, worked out before adoption
	///and read-only ("AdoptionValidation migration-preview", runbook step 0.2). On such a database the 25.3 schema steps find nothing to change, and
	///the 25.3 seed and XConnect steps run only on a database from 24.3. These steps change it:
	///- the report rows step (ReportRows25_3Step) renames the displayreport row of Production by Procedure, Procedures (individual) or Insurance
	///  Pending Supplementals that Open Dental wrote under an internal name this program does not know. The plan is ReportRows25_3.PlanRenames, the
	///  matching the step itself uses;
	///- the Open Dental 25.4 and 26.1 steps bring the schema to 26.1.70 (the schema verifier then confirms the exact match), insert their
	///  preferences and split the blockout permission (Migration26_1Plan);
	///- the DXC Patient Credit Score step (DxcCreditScoreRemovedStep, B-SHELL) disables that discontinued program link and deletes its toolbar
	///  buttons, on every database (DxcCreditScorePlan);
	///- the UDS+ report row step (ReportRowUdsPlusStep, M2-UDS) adds the row of the 25.4 UDS+ report after the FQHC Dental Sealant Measure, moves the
	///  later Public Health rows down one place and copies the sealant measure's report permissions to it (UdsPlusRowPlan);
	///- the Insurance History step (InsHistCodeGroupsStep, M2-HIST) marks the code groups of the InsHist*Codes preferences Show In History, adds
	///  their codes and creates a group for a category without one (InsHistCodeGroupPlan);
	///- the routine maintenance grant step (MaintPermissionGrantStep) gives every user group with Security Admin Maintenance Setup and Maintenance
	///  Record Completion, the rows it lacks (MaintenanceGrantPlan). The routine maintenance tables are created empty.
	///adopt's row check (AdoptionRowCheck) holds the adopted copy to the same plans, made from the original. The last line lists the program rows
	///that name a plug-in DLL, which adoption does not change but which stop loading in this program (GetPluginLine). Reads the displayreport
	///table, the preferences, grouppermission, whether claimpayment notes are NULL, program, toolbutitem, codegroup and the procedure codes.</summary>
	public static class MigrationPreview {
		public class Result {
			public string Database;
			public string DataBaseVersion;
			///<summary>True when the database comes from 24.3: then the steps also convert the schema and add rows, which this preview does not list.
			///</summary>
			public bool IsFrom24_3;
			public int CountRows;
			public List<ReportRows25_3.ReportTargetPlan> ListPlans=new List<ReportRows25_3.ReportTargetPlan>();
			///<summary>What the Open Dental 25.4 and 26.1 steps change.</summary>
			public Migration26_1Plan.Plan Plan26_1=new Migration26_1Plan.Plan();
			///<summary>What the DXC Patient Credit Score step changes.</summary>
			public DxcCreditScorePlan.Plan PlanDxc=new DxcCreditScorePlan.Plan();
			///<summary>What the UDS+ report row step changes.</summary>
			public UdsPlusRowPlan.Plan PlanUdsPlus=new UdsPlusRowPlan.Plan();
			///<summary>What the Insurance History step changes.</summary>
			public InsHistCodeGroupPlan.Plan PlanInsHist=new InsHistCodeGroupPlan.Plan();
			///<summary>What the routine maintenance grant step changes.</summary>
			public MaintenanceGrantPlan.Plan PlanMaintGrants=new MaintenanceGrantPlan.Plan();
			///<summary>The program rows that name a plug-in DLL (PluginDllName), in ProgramNum order. Adoption changes none of them; it is listed so
			///that an office sees, before it switches, which plug-ins will stop loading.</summary>
			public List<PluginRow> ListPluginRows=new List<PluginRow>();

			///<summary>The plans that rename a row.</summary>
			public List<ReportRows25_3.ReportTargetPlan> ListRenames {
				get { return ListPlans.Where(x => x.RowRenamed!=null).ToList(); }
			}

			///<summary>What the command prints.</summary>
			public List<string> Lines() {
				List<string> listLines=new List<string>();
				listLines.Add("Fork migration preview of "+Database+" (DataBaseVersion "+(DataBaseVersion??"missing")+"): "+CountRows+" displayreport rows read. "
					+"Step "+StepReportRows+" (report rows) on the reports this program names itself:");
				foreach(ReportRows25_3.ReportTargetPlan plan in ListPlans) {
					listLines.Add("  "+plan.Describe());
				}
				List<ReportRows25_3.ReportTargetPlan> listRenames=ListRenames;
				if(listRenames.Count==0) {
					listLines.Add("Adoption's fork migration will rename no displayreport row.");
				}
				else {
					listLines.Add("Adoption's fork migration will rename "+listRenames.Count+" displayreport row(s), changing only their InternalName: "
						+string.Join("; ",listRenames.Select(x => x.RowRenamed.InternalName+" to "+x.Target.InternalName+" (DisplayReportNum "+x.RowRenamed.DisplayReportNum+")"))
						+". Write them on the checklist (runbook step 0.2); adopt's row check must show exactly these (step 4).");
				}
				if(IsFrom24_3) {
					listLines.Add("This database comes from 24.3: the fork migration also converts the schema to 25.3.48 and adds rows and permissions, which this "
						+"preview does not list. The office runbook adopts only an Open Dental 25.3.48 database.");
				}
				else {
					listLines.Add("The Open Dental 24.4 to 25.3 steps change no row of an Open Dental 25.3.48 database: their schema steps find the 25.3.48 "
						+"schema, and their seed and XConnect steps run only on a database from 24.3.");
				}
				listLines.AddRange(Plan26_1.Lines());
				listLines.AddRange(PlanDxc.Lines());
				listLines.AddRange(PlanUdsPlus.Lines());
				listLines.AddRange(PlanInsHist.Lines());
				listLines.AddRange(PlanMaintGrants.Lines());
				listLines.Add(GetPluginLine(ListPluginRows));
				return listLines;
			}
		}

		///<summary>A program row that names a plug-in DLL.</summary>
		public class PluginRow {
			public long ProgramNum;
			public string ProgName;
			public bool IsEnabled;
			public string PluginDllName;
		}

		///<summary>The preview's one line about plug-ins: the program rows that name a PluginDllName. Plug-ins built against Open Dental's assemblies
		///(its exe and business layer dll) do not load in this program, whose assemblies and namespaces have other names (owner decision);
		///the program skips them without a message. Adoption changes none of these rows.</summary>
		public static string GetPluginLine(List<PluginRow> listPluginRows) {
			if(listPluginRows.Count==0) {
				return "Plug-ins: no program row names a plug-in DLL (PluginDllName), so no plug-in stops loading.";
			}
			return "Plug-ins: "+listPluginRows.Count+" program row(s) name a plug-in DLL (PluginDllName); a plug-in built for Open Dental does not load in "
				+"this program (adoption changes none of these rows): "
				+string.Join("; ",listPluginRows.Select(x => x.ProgName+" (ProgramNum "+x.ProgramNum+", "+(x.IsEnabled ? "enabled" : "disabled")+", "
				+x.PluginDllName+")"))+".";
		}

		///<summary>Every program row with a PluginDllName, read with plain SQL in ProgramNum order. Empty when there is no program table or column.
		///</summary>
		public static List<PluginRow> ReadPluginRows(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			bool hasColumn=db.ScalarLong("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA="+SqlLiteral.Quote(database)
				+" AND TABLE_NAME='program' AND COLUMN_NAME='PluginDllName'")>0;
			if(!hasColumn) {
				return new List<PluginRow>();
			}
			return db.Rows("SELECT ProgramNum,ProgName,Enabled,PluginDllName FROM `"+database+"`.program "
				+"WHERE COALESCE(TRIM(PluginDllName),'')<>'' ORDER BY ProgramNum")
				.Select(x => new PluginRow {
					ProgramNum=Convert.ToInt64(x[0],CultureInfo.InvariantCulture),
					ProgName=Convert.ToString(x[1],CultureInfo.InvariantCulture)??"",
					IsEnabled=Convert.ToInt64(x[2],CultureInfo.InvariantCulture)!=0,
					PluginDllName=Convert.ToString(x[3],CultureInfo.InvariantCulture)??"",
				}).ToList();
		}

		///<summary>The level of the report rows step, for messages.</summary>
		public static long StepReportRows {
			get { return new ReportRows25_3Step().Level; }
		}

		public static Result Run(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			if(!db.DatabaseExists(database)) {
				throw new RefusedException("Database "+database+" does not exist on this server.");
			}
			Result result=new Result { Database=database };
			ForkDbState state=HeadlessAdoption.ReadState(db,database);
			result.DataBaseVersion=state.DataBaseVersion;
			result.IsFrom24_3=state.ForkAdoptedFrom==null && Seed25_3.IsVersionFrom24_3(state.DataBaseVersion);
			List<ReportRows25_3.DisplayReportRow> listRows=ReadRows(db,database);
			result.CountRows=listRows.Count;
			result.ListPlans=ReportRows25_3.PlanRenames(listRows);
			result.Plan26_1=Migration26_1Plan.Make(db,database);
			result.PlanDxc=DxcCreditScorePlan.Make(db,database);
			result.PlanUdsPlus=UdsPlusRowPlan.Make(db,database,listRows,result.ListPlans,result.Plan26_1.IsFromBefore25_4);
			result.PlanInsHist=InsHistCodeGroupPlan.Make(db,database,result.Plan26_1.IsFromBefore25_4);
			result.PlanMaintGrants=MaintenanceGrantPlan.Make(db,database);
			result.ListPluginRows=ReadPluginRows(db,database);
			return result;
		}

		///<summary>Every displayreport row of the database, read with plain SQL in the order the step reads them. Empty when there is no such table.
		///</summary>
		public static List<ReportRows25_3.DisplayReportRow> ReadRows(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			if(!HasTable(db,database)) {
				return new List<ReportRows25_3.DisplayReportRow>();
			}
			return db.Rows("SELECT DisplayReportNum,InternalName,ItemOrder,Description,Category,IsHidden FROM `"+database+"`.displayreport "
				+"ORDER BY Category,ItemOrder,DisplayReportNum")
				.Select(x => new ReportRows25_3.DisplayReportRow {
					DisplayReportNum=Convert.ToInt64(x[0],CultureInfo.InvariantCulture),
					InternalName=Convert.ToString(x[1],CultureInfo.InvariantCulture)??"",
					ItemOrder=Convert.ToInt32(x[2],CultureInfo.InvariantCulture),
					Description=Convert.ToString(x[3],CultureInfo.InvariantCulture)??"",
					Category=(DisplayReportCategory)Convert.ToInt32(x[4],CultureInfo.InvariantCulture),
					IsHidden=Convert.ToInt32(x[5],CultureInfo.InvariantCulture)!=0,
				}).ToList();
		}

		public static bool HasTable(Db db,string database) {
			return db.ScalarLong("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA="+SqlLiteral.Quote(database)+" AND TABLE_NAME='displayreport'")>0;
		}
	}
}
