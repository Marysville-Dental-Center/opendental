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
	///program opens it and its permissions carry over. This is the only change made on a database Open Dental converted; PlanRenames works it out
	///from the rows without writing (AdoptionValidation migration-preview, and adopt's row check).</para>
	///Every step checks before it writes, so a second run changes nothing.</summary>
	public static class ReportRows25_3 {
		///<summary>Descriptions as Open Dental's report menu shows them by default. Users can rename them in Report Setup.</summary>
		public const string DescriptionProcedures="Procedures";
		public const string DescriptionProductionByProcedure="Production by Procedure";
		public const string DescriptionInsPendingSupplementals="Insurance Pending Supplementals";

		///<summary>The reports whose row is adopted from a name this program does not know, in the order the steps look for them, with the category and
		///the normalized descriptions (NormalizeDescription) that identify such a row.</summary>
		public static readonly ReportTarget[] ArrayTargets={
			new ReportTarget(DisplayReports.ReportNames.ProductionByProcedure,DescriptionProductionByProcedure,DisplayReportCategory.Daily,
				"productionbyprocedure","productionbyprocedures","productionbyprocedurecode"),
			new ReportTarget(DisplayReports.ReportNames.ProceduresIndividual,"Procedures (individual)",DisplayReportCategory.Daily,
				"individualprocedures","proceduresindividual"),
			new ReportTarget(DisplayReports.ReportNames.InsPendingSupplementals,DescriptionInsPendingSupplementals,DisplayReportCategory.Lists,
				"insurancependingsupplementals","insurancependingsupplemental","inspendingsupplementals","pendingsupplementals"),
		};

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

		///<summary>What the steps do with the rows of these reports that are under names this program does not know, worked out from listRows (every
		///displayreport row) without writing: one plan per report of ArrayTargets, in order. On a database Open Dental already converted these renames
		///are everything the steps change; on one from 24.3 the steps make the same renames and also add rows. listRows is not changed.</summary>
		public static List<ReportTargetPlan> PlanRenames(List<DisplayReportRow> listRows) {
			//The steps look for each report on the rows as the earlier steps left them, so a planned rename is applied to a copy before the next report.
			List<DisplayReportRow> listCopy=listRows.Select(x => x.Copy()).ToList();
			List<ReportTargetPlan> listPlans=new List<ReportTargetPlan>();
			foreach(ReportTarget target in ArrayTargets) {
				ReportTargetPlan plan=new ReportTargetPlan { Target=target };
				listPlans.Add(plan);
				plan.RowExisting=listCopy.FirstOrDefault(x => x.InternalName==target.InternalName)?.Copy();
				if(plan.RowExisting!=null) {
					continue;
				}
				List<DisplayReportRow> listMatches=FindUnknownRows(listCopy,target);
				plan.ListMatches.AddRange(listMatches.Select(x => x.Copy()));
				if(listMatches.Count!=1) {
					continue;
				}
				plan.RowRenamed=listMatches[0].Copy();
				listMatches[0].InternalName=target.InternalName;
			}
			return listPlans;
		}

		///<summary>The rows of the target's category whose internal name this program does not know and whose description matches the target's.
		///</summary>
		private static List<DisplayReportRow> FindUnknownRows(List<DisplayReportRow> listRows,ReportTarget target) {
			return listRows.FindAll(x => x.Category==target.Category
				&& !string.IsNullOrWhiteSpace(x.InternalName)
				&& !DisplayReports.ReportNames.IsKnown(x.InternalName)
				&& x.InternalName!=DisplayReports.ReportNames.WebSchedAppointments
				&& target.ArrayDescriptions.Contains(NormalizeDescription(x.Description)));
		}

		private static ReportTarget GetTarget(string internalName) {
			return ArrayTargets.First(x => x.InternalName==internalName);
		}

		private static void EnsureProductionByProcedure(List<string> listChanges,bool isFrom24_3) {
			List<DisplayReportRow> listRows=GetRows();
			if(listRows.Any(x => x.InternalName==DisplayReports.ReportNames.ProductionByProcedure)) {
				return;
			}
			if(TryAdoptUnknownRow(listRows,GetTarget(DisplayReports.ReportNames.ProductionByProcedure),listChanges) || !isFrom24_3) {
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
			if(TryAdoptUnknownRow(listRows,GetTarget(DisplayReports.ReportNames.ProceduresIndividual),listChanges) || !isFrom24_3) {
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
			if(TryAdoptUnknownRow(listRows,GetTarget(DisplayReports.ReportNames.InsPendingSupplementals),listChanges) || !isFrom24_3) {
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

		///<summary>Renames the one row in the target's category whose internal name this program does not know and whose description matches. Returns
		///false when there is no such row, or more than one (ambiguous, so nothing is renamed). PlanRenames predicts it.</summary>
		private static bool TryAdoptUnknownRow(List<DisplayReportRow> listRows,ReportTarget target,List<string> listChanges) {
			List<DisplayReportRow> listMatches=FindUnknownRows(listRows,target);
			if(listMatches.Count!=1) {
				return false;
			}
			DataCore.NonQ("UPDATE displayreport SET InternalName='"+POut.String(target.InternalName)+"' "
				+"WHERE DisplayReportNum="+POut.Long(listMatches[0].DisplayReportNum));
			listChanges.Add(DescribeRename(listMatches[0].InternalName,target.InternalName));
			return true;
		}

		///<summary>The change line of a rename, as the step logs it ("Renamed displayreport X to Y (same description)").</summary>
		public static string DescribeRename(string internalNameFrom,string internalNameTo) {
			return "Renamed displayreport "+internalNameFrom+" to "+internalNameTo+" (same description)";
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
		public class DisplayReportRow {
			public long DisplayReportNum;
			public string InternalName;
			public int ItemOrder;
			public string Description;
			public DisplayReportCategory Category;
			public bool IsHidden;

			public DisplayReportRow Copy() {
				return (DisplayReportRow)MemberwiseClone();
			}
		}

		///<summary>A report whose row the steps adopt from a name this program does not know.</summary>
		public class ReportTarget {
			///<summary>This program's internal name for the report.</summary>
			public readonly string InternalName;
			///<summary>For messages.</summary>
			public readonly string Title;
			public readonly DisplayReportCategory Category;
			///<summary>Normalized descriptions (NormalizeDescription) that identify a row of this report.</summary>
			public readonly string[] ArrayDescriptions;

			public ReportTarget(string internalName,string title,DisplayReportCategory category,params string[] arrayDescriptions) {
				InternalName=internalName;
				Title=title;
				Category=category;
				ArrayDescriptions=arrayDescriptions;
			}
		}

		///<summary>What the steps do about one report of ArrayTargets on a given set of rows (PlanRenames).</summary>
		public class ReportTargetPlan {
			public ReportTarget Target;
			///<summary>The row that already has this program's name; then nothing is renamed.</summary>
			public DisplayReportRow RowExisting;
			///<summary>Rows of the report's category under names this program does not know whose description matches (when no row has our name).</summary>
			public List<DisplayReportRow> ListMatches=new List<DisplayReportRow>();
			///<summary>The row the step renames to Target.InternalName, as it is before (its old name): the only match. Null when there is none, or more
			///than one (ambiguous: nothing is renamed).</summary>
			public DisplayReportRow RowRenamed;

			public bool IsAmbiguous {
				get { return RowExisting==null && ListMatches.Count>1; }
			}

			public string Describe() {
				string report=Target.Title+" ("+Target.InternalName+", "+Target.Category+")";
				if(RowExisting!=null) {
					return report+": DisplayReportNum "+RowExisting.DisplayReportNum+" already has this name; nothing is renamed.";
				}
				if(RowRenamed!=null) {
					return report+": DisplayReportNum "+RowRenamed.DisplayReportNum+" (\""+RowRenamed.Description+"\") is renamed; the adoption log says \""
						+DescribeRename(RowRenamed.InternalName,Target.InternalName)+"\".";
				}
				if(ListMatches.Count>1) {
					return report+": "+ListMatches.Count+" rows under names this program does not know have a matching description (DisplayReportNum "
						+string.Join(", ",ListMatches.Select(x => x.DisplayReportNum))+"); nothing is renamed.";
				}
				return report+": no row has this name or a matching description under an unknown name; nothing is renamed.";
			}
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
