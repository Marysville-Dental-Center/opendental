// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations.Features;
using SyntheticData.Infrastructure;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	///<summary>What the fork migration's UDS+ report row step (ReportRowUdsPlusStep, backlog package M2-UDS: the report Open Dental added in 25.4)
	///changes when adoption brings an Open Dental 25.3.48 database to 26.1.70, worked out before adoption and read-only. On a database from before 25.4
	///(judged as the 25.4 and 26.1 seed step judges it, Migration26_1Plan) that has no row of the report, the step adds one, "FQHC UDS+ Dental
	///Services" in Public Health, right after the FQHC Dental Sealant Measure (the later Public Health rows move down one place, ItemOrder only), hidden
	///when the sealant measure is, and gives every user group that may run the sealant measure (a Reports grant whose FKey is its DisplayReportNum) the
	///same grant for the new row. When a single row under a name this program does not know carries a UDS+ description, it renames that row instead,
	///InternalName only. The rows are planned by the step's own rule (ReportRowUdsPlus.Plan) on the rows as the report rows step (2026101971) leaves
	///them. migration-preview prints the plan (runbook step 0.2), and adopt's row check (AdoptionRowCheck) holds the adopted copy to the plan made from
	///the original. Reads displayreport (given) and the Reports grants of grouppermission.</summary>
	public static class UdsPlusRowPlan {
		///<summary>Level of the step, for messages.</summary>
		public static long StepLevel {
			get { return new ReportRowUdsPlusStep().Level; }
		}

		public class Plan {
			///<summary>True when the database comes from before Open Dental 25.4: then the step adds the row if there is none.</summary>
			public bool IsFromBefore25_4;
			///<summary>What the step does with the rows.</summary>
			public ReportRowUdsPlus.RowPlan Rows=new ReportRowUdsPlus.RowPlan();
			///<summary>The user groups that may run the sealant measure, in UserGroupNum order: each gets the Reports permission for the added row. Empty
			///unless the row is added and the sealant measure row has such grants.</summary>
			public List<long> ListUserGroupsGranted=new List<long>();

			///<summary>What migration-preview prints for the step.</summary>
			public List<string> Lines() {
				string step="Step "+StepLevel+" (UDS+ report row)";
				string name=DisplayReports.ReportNames.UdsPlus;
				List<string> listLines=new List<string>();
				if(Rows.RowExisting!=null) {
					listLines.Add(step+" changes nothing: DisplayReportNum "+Rows.RowExisting.DisplayReportNum+" already has this program's name ("+name+").");
					return listLines;
				}
				if(Rows.RowRenamed!=null) {
					listLines.Add(step+": DisplayReportNum "+Rows.RowRenamed.DisplayReportNum+" (\""+Rows.RowRenamed.Description+"\"), under a name this program does "
						+"not know, is renamed to "+name+", InternalName only; the adoption log says \""+ReportRowUdsPlus.DescribeRename(Rows.RowRenamed.InternalName)
						+"\". Write it on the checklist (runbook step 0.2); adopt's row check must show it (step 4).");
					return listLines;
				}
				if(Rows.IsAmbiguous) {
					listLines.Add(step+" changes nothing: "+Rows.ListMatches.Count+" rows under names this program does not know have a UDS+ description "
						+"(DisplayReportNum "+string.Join(", ",Rows.ListMatches.Select(x => x.DisplayReportNum))+"), so none of them is renamed and no row is added.");
					return listLines;
				}
				if(!Rows.IsAdded) {
					listLines.Add(step+" changes nothing: this database does not come from before Open Dental 25.4, and no row under another name has a UDS+ "
						+"description.");
					return listLines;
				}
				string where=Rows.IsAfterSealant
					? "right after the FQHC Dental Sealant Measure (DisplayReportNum "+Rows.RowSealant.DisplayReportNum+"), moving "+Rows.ListRowsMoved.Count
						+" later Public Health row(s) down one place, ItemOrder only"+(Rows.ListRowsMoved.Count==0 ? "" : " (DisplayReportNum "
						+string.Join(", ",Rows.ListRowsMoved.Select(x => x.DisplayReportNum))+")")
					: "at the end of Public Health";
				string grants=Rows.RowSealant==null ? "there is no sealant measure row, so it copies no report permission"
					: ListUserGroupsGranted.Count==0 ? "no user group has a Reports grant for the sealant measure alone, so it copies no report permission"
					: "it gives the "+ListUserGroupsGranted.Count+" user group(s) that may run the sealant measure (user group "+string.Join(", ",ListUserGroupsGranted)
						+") the Reports permission for it";
				listLines.Add(step+" adds the displayreport row "+name+" (\""+ReportRowUdsPlus.Description+"\", Public Health, ItemOrder "+Rows.ItemOrder
					+(Rows.IsHidden ? ", hidden as the sealant measure is" : "")+") "+where+"; "+grants+" (this database comes from before Open Dental 25.4). The "
					+"adoption log says \""+ReportRowUdsPlus.DescribeAdd(Rows.IsHidden)+"\""+(ListUserGroupsGranted.Count==0 ? ""
					: " and \""+ReportRowUdsPlus.DescribeCopy(ListUserGroupsGranted.Count)+"\"")+"; adopt's row check (step 4) requires exactly this.");
				return listLines;
			}
		}

		///<summary>The plan for a database on this server. listRows are its displayreport rows (MigrationPreview.ReadRows) and listRenamePlans the report
		///rows step's plan for them (ReportRows25_3.PlanRenames), which runs first; isFromBefore25_4 as Migration26_1Plan judges it.</summary>
		public static Plan Make(Db db,string database,List<ReportRows25_3.DisplayReportRow> listRows,List<ReportRows25_3.ReportTargetPlan> listRenamePlans,
			bool isFromBefore25_4)
		{
			LocalOnly.EnsureSafeIdentifier(database);
			Plan plan=new Plan { IsFromBefore25_4=isFromBefore25_4 };
			//The rows as the report rows step leaves them.
			List<ReportRows25_3.DisplayReportRow> listAfterRenames=listRows.Select(x => x.Copy()).ToList();
			foreach(ReportRows25_3.ReportTargetPlan planRename in listRenamePlans.Where(x => x.RowRenamed!=null)) {
				ReportRows25_3.DisplayReportRow row=listAfterRenames.FirstOrDefault(x => x.DisplayReportNum==planRename.RowRenamed.DisplayReportNum);
				if(row!=null) {
					row.InternalName=planRename.Target.InternalName;
				}
			}
			plan.Rows=ReportRowUdsPlus.Plan(listAfterRenames,isFromBefore25_4);
			if(plan.Rows.IsAdded && plan.Rows.RowSealant!=null && Migration26_1Plan.HasTable(db,database,"grouppermission")) {
				plan.ListUserGroupsGranted=ReadReportGroups(db,database,plan.Rows.RowSealant.DisplayReportNum);
			}
			return plan;
		}

		///<summary>The user groups with a Reports grant for one report (FKey), distinct, in UserGroupNum order.</summary>
		public static List<long> ReadReportGroups(Db db,string database,long displayReportNum) {
			LocalOnly.EnsureSafeIdentifier(database);
			return db.Rows("SELECT DISTINCT UserGroupNum FROM `"+database+"`.grouppermission WHERE PermType="
				+((int)EnumPermType.Reports).ToString(CultureInfo.InvariantCulture)+" AND FKey="+displayReportNum.ToString(CultureInfo.InvariantCulture)
				+" ORDER BY UserGroupNum").Select(x => Convert.ToInt64(x[0],CultureInfo.InvariantCulture)).ToList();
		}
	}
}
