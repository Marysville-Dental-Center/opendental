// displayreport rows for the reports Open Dental added in 24.4 and 25.2 (package A-RPT in docs/specs/port-backlog.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

//Feature data steps (as opposed to the Open Dental 25.3.48 schema steps and the M1-02 seed in OpenDentBusiness.ForkMigrations) live in this
//namespace. The runner finds them the same way, by reflection over the assembly.
namespace OpenDentBusiness.ForkMigrations.Features {
	///<summary>Makes sure the displayreport rows for the 24.4 and 25.2 standard reports exist, without duplicating rows that Open Dental itself already
	///wrote into an office database:
	///<para>24.4 split the Daily Procedures report in two. The row of the old report (ODProcedures) becomes the itemized Procedures report
	///(ODProceduresIndividual), keeping its DisplayReportNum so report permissions carry over, and a Production by Procedure row
	///(ODProductionByProcedure) is added beside it with a copy of the old report's permissions.</para>
	///<para>25.2 added Insurance Pending Supplementals (Lists). Its row (ODInsPendingSupplementals) goes to the user groups that may run Insurance
	///Overpaid (our choice, from the 25.2 feature spec).</para>
	///<para>Those rows and permission copies are written only on a database that comes from 24.3. A database that Open Dental already converted has
	///Open Dental's own rows (rule 2 of ForkMigrationStep), so nothing is added there.</para>
	///<para>Open Dental's published documentation does not list report internal names. When a row for one of these reports exists under a name this
	///program does not know (a row Open Dental's own conversion wrote) and its description matches, that row is renamed to our name, so this
	///program opens it and its permissions carry over. This is the only change made on a database Open Dental converted.</para>
	///Every step checks before it writes, so a second run changes nothing.</summary>
	public static class ReportRows25_3 {
		///<summary>Descriptions as Open Dental's report menu shows them by default. Users can rename them in Report Setup.</summary>
		public const string DescriptionProcedures="Procedures";
		public const string DescriptionProductionByProcedure="Production by Procedure";
		public const string DescriptionInsPendingSupplementals="Insurance Pending Supplementals";

		///<summary>Runs every step. isFrom24_3 is true for a database that comes from 24.3 or earlier (rows are added); false for one that Open Dental
		///already converted, or of unknown origin (only rows under other names are adopted). Returns one line per change.</summary>
		public static List<string> Run(bool isFrom24_3) {
			List<string> listChanges=new List<string>();
			EnsureProductionByProcedure(listChanges,isFrom24_3);
			EnsureProceduresIndividual(listChanges,isFrom24_3);
			EnsureInsPendingSupplementals(listChanges,isFrom24_3);
			return listChanges;
		}

		///<summary>Lower-case letters and digits only, so "Production By Procedures" and "production-by-procedure" compare equal.</summary>
		public static string NormalizeDescription(string description) {
			StringBuilder stringBuilder=new StringBuilder();
			foreach(char c in (description??"").ToLowerInvariant()) {
				if(char.IsLetterOrDigit(c)) {
					stringBuilder.Append(c);
				}
			}
			return stringBuilder.ToString();
		}

		private static void EnsureProductionByProcedure(List<string> listChanges,bool isFrom24_3) {
			List<DisplayReportRow> listRows=GetRows();
			if(listRows.Any(x => x.InternalName==DisplayReports.ReportNames.ProductionByProcedure)) {
				return;
			}
			if(TryAdoptUnknownRow(listRows,DisplayReportCategory.Daily,DisplayReports.ReportNames.ProductionByProcedure,listChanges,
				"productionbyprocedure","productionbyprocedures","productionbyprocedurecode") || !isFrom24_3)
			{
				return;
			}
			DisplayReportRow rowSource=listRows.FirstOrDefault(x => x.InternalName==DisplayReports.ReportNames.ProceduresIndividual)
				??listRows.FirstOrDefault(x => x.InternalName==DisplayReports.ReportNames.Procedures);
			long displayReportNum;
			if(rowSource!=null && rowSource.Category==DisplayReportCategory.Daily) {
				//Right after the procedures report, moving the later Daily rows down one place. Report Setup expects ItemOrder to run 0, 1, 2, ...
				DataCore.NonQ("UPDATE displayreport SET ItemOrder=ItemOrder+1 WHERE Category="+POut.Int((int)DisplayReportCategory.Daily)
					+" AND ItemOrder>"+POut.Int(rowSource.ItemOrder));
				displayReportNum=InsertRow(DisplayReports.ReportNames.ProductionByProcedure,rowSource.ItemOrder+1,DescriptionProductionByProcedure,
					DisplayReportCategory.Daily,rowSource.IsHidden);
			}
			else {
				displayReportNum=InsertRow(DisplayReports.ReportNames.ProductionByProcedure,GetNextItemOrder(DisplayReportCategory.Daily),
					DescriptionProductionByProcedure,DisplayReportCategory.Daily,false);
			}
			listChanges.Add("Added displayreport "+DisplayReports.ReportNames.ProductionByProcedure);
			if(rowSource!=null) {
				int count=CopyReportPermissions(rowSource.DisplayReportNum,displayReportNum);
				if(count>0) {
					listChanges.Add("Copied "+count+" report permission(s) from "+rowSource.InternalName+" to "+DisplayReports.ReportNames.ProductionByProcedure);
				}
			}
		}

		private static void EnsureProceduresIndividual(List<string> listChanges,bool isFrom24_3) {
			List<DisplayReportRow> listRows=GetRows();
			if(listRows.Any(x => x.InternalName==DisplayReports.ReportNames.ProceduresIndividual)) {
				return;
			}
			if(TryAdoptUnknownRow(listRows,DisplayReportCategory.Daily,DisplayReports.ReportNames.ProceduresIndividual,listChanges,
				"individualprocedures","proceduresindividual") || !isFrom24_3)
			{
				return;
			}
			DisplayReportRow rowOld=listRows.FirstOrDefault(x => x.InternalName==DisplayReports.ReportNames.Procedures);
			if(rowOld!=null) {
				DataCore.NonQ("UPDATE displayreport SET InternalName='"+POut.String(DisplayReports.ReportNames.ProceduresIndividual)+"' "
					+"WHERE DisplayReportNum="+POut.Long(rowOld.DisplayReportNum));
				listChanges.Add("Renamed displayreport "+DisplayReports.ReportNames.Procedures+" to "+DisplayReports.ReportNames.ProceduresIndividual);
				return;
			}
			InsertRow(DisplayReports.ReportNames.ProceduresIndividual,GetNextItemOrder(DisplayReportCategory.Daily),DescriptionProcedures,
				DisplayReportCategory.Daily,false);
			listChanges.Add("Added displayreport "+DisplayReports.ReportNames.ProceduresIndividual);
		}

		private static void EnsureInsPendingSupplementals(List<string> listChanges,bool isFrom24_3) {
			List<DisplayReportRow> listRows=GetRows();
			if(listRows.Any(x => x.InternalName==DisplayReports.ReportNames.InsPendingSupplementals)) {
				return;
			}
			if(TryAdoptUnknownRow(listRows,DisplayReportCategory.Lists,DisplayReports.ReportNames.InsPendingSupplementals,listChanges,
				"insurancependingsupplementals","insurancependingsupplemental","inspendingsupplementals","pendingsupplementals") || !isFrom24_3)
			{
				return;
			}
			long displayReportNum=InsertRow(DisplayReports.ReportNames.InsPendingSupplementals,GetNextItemOrder(DisplayReportCategory.Lists),
				DescriptionInsPendingSupplementals,DisplayReportCategory.Lists,false);
			listChanges.Add("Added displayreport "+DisplayReports.ReportNames.InsPendingSupplementals);
			DisplayReportRow rowOverpaid=listRows.FirstOrDefault(x => x.InternalName=="ODInsuranceOverpaid");
			if(rowOverpaid!=null) {
				int count=CopyReportPermissions(rowOverpaid.DisplayReportNum,displayReportNum);
				if(count>0) {
					listChanges.Add("Copied "+count+" report permission(s) from ODInsuranceOverpaid to "+DisplayReports.ReportNames.InsPendingSupplementals);
				}
			}
		}

		///<summary>Renames the one row in the category whose internal name this program does not know and whose description matches. Returns false
		///when there is no such row, or more than one (ambiguous, so nothing is renamed).</summary>
		private static bool TryAdoptUnknownRow(List<DisplayReportRow> listRows,DisplayReportCategory category,string internalName,List<string> listChanges,
			params string[] arrayDescriptions)
		{
			List<DisplayReportRow> listMatches=listRows.FindAll(x => x.Category==category
				&& !string.IsNullOrWhiteSpace(x.InternalName)
				&& !DisplayReports.ReportNames.IsKnown(x.InternalName)
				&& x.InternalName!=DisplayReports.ReportNames.WebSchedAppointments
				&& arrayDescriptions.Contains(NormalizeDescription(x.Description)));
			if(listMatches.Count!=1) {
				return false;
			}
			DataCore.NonQ("UPDATE displayreport SET InternalName='"+POut.String(internalName)+"' "
				+"WHERE DisplayReportNum="+POut.Long(listMatches[0].DisplayReportNum));
			listChanges.Add("Renamed displayreport "+listMatches[0].InternalName+" to "+internalName+" (same description)");
			return true;
		}

		private static long InsertRow(string internalName,int itemOrder,string description,DisplayReportCategory category,bool isHidden) {
			return DataCore.NonQ("INSERT INTO displayreport (InternalName,ItemOrder,Description,Category,IsHidden,IsVisibleInSubMenu) VALUES ("
				+"'"+POut.String(internalName)+"',"+POut.Int(itemOrder)+",'"+POut.String(description)+"',"+POut.Int((int)category)+","
				+POut.Bool(isHidden)+",0)",true);
		}

		private static int GetNextItemOrder(DisplayReportCategory category) {
			string value=DataCore.GetScalar("SELECT COALESCE(MAX(ItemOrder)+1,0) FROM displayreport WHERE Category="+POut.Int((int)category));
			return PIn.Int(value);
		}

		///<summary>Gives every user group that may run the source report (a Reports permission with FKey = its DisplayReportNum) the same permission for
		///the target report, unless the group already has it. Groups with FKey 0 may run every report and need no row. Returns the number of rows
		///added.</summary>
		private static int CopyReportPermissions(long displayReportNumFrom,long displayReportNumTo) {
			int permType=(int)EnumPermType.Reports;
			DataTable table=DataCore.GetTable("SELECT DISTINCT UserGroupNum FROM grouppermission WHERE PermType="+POut.Int(permType)
				+" AND FKey="+POut.Long(displayReportNumFrom)
				+" AND UserGroupNum NOT IN (SELECT UserGroupNum FROM grouppermission WHERE PermType="+POut.Int(permType)
				+" AND FKey="+POut.Long(displayReportNumTo)+")");
			foreach(DataRow row in table.Rows) {
				DataCore.NonQ("INSERT INTO grouppermission (NewerDate,NewerDays,UserGroupNum,PermType,FKey) VALUES ("
					+POut.Date(DateTime.MinValue)+",0,"+POut.Long(PIn.Long(row[0].ToString()))+","+POut.Int(permType)+","+POut.Long(displayReportNumTo)+")");
			}
			return table.Rows.Count;
		}

		private static List<DisplayReportRow> GetRows() {
			DataTable table=DataCore.GetTable("SELECT DisplayReportNum,InternalName,ItemOrder,Description,Category,IsHidden FROM displayreport "
				+"ORDER BY Category,ItemOrder,DisplayReportNum");
			List<DisplayReportRow> listRows=new List<DisplayReportRow>();
			foreach(DataRow row in table.Rows) {
				listRows.Add(new DisplayReportRow {
					DisplayReportNum=PIn.Long(row["DisplayReportNum"].ToString()),
					InternalName=row["InternalName"].ToString(),
					ItemOrder=PIn.Int(row["ItemOrder"].ToString()),
					Description=row["Description"].ToString(),
					Category=(DisplayReportCategory)PIn.Int(row["Category"].ToString()),
					IsHidden=PIn.Bool(row["IsHidden"].ToString()),
				});
			}
			return listRows;
		}

		///<summary>The columns of a displayreport row that the steps read. Read straight from the table, not the cache.</summary>
		private class DisplayReportRow {
			public long DisplayReportNum;
			public string InternalName;
			public int ItemOrder;
			public string Description;
			public DisplayReportCategory Category;
			public bool IsHidden;
		}
	}

	///<summary>Runs ReportRows25_3 after the 25.3.48 schema and seed steps. On a database that comes from 24.3 (judged as Seed25_3Step judges it) it
	///adds the rows; on one that Open Dental already converted it only adopts a row whose internal name differs from ours (see ReportRows25_3).
	///</summary>
	[ForkMigration(2026101971,"Report rows for Procedures, Production by Procedure and Insurance Pending Supplementals")]
	public class ReportRows25_3Step:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			if(!context.TableExists("displayreport") || !context.TableExists("grouppermission")) {
				return;
			}
			foreach(string change in ReportRows25_3.Run(Seed25_3Step.IsFrom24_3(context))) {
				context.Log(change);
			}
		}
	}
}
