// displayreport row for the UDS+ report that Open Dental added in 25.4 (package M2-UDS in docs/specs/port-backlog.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace OpenDentBusiness.ForkMigrations.Features {
	///<summary>Makes sure the Standard Reports row of the UDS+ report (ODUdsPlus, Public Health) exists, without duplicating a row that Open Dental
	///itself wrote:
	///<para>On a database that comes from before 25.4 (24.3, or an Open Dental 25.3 database the fork adopted) the row is added right after the FQHC
	///Dental Sealant Measure, hidden when that report is hidden (an office that hid the FQHC report is not a health center), and the user groups that
	///may run the sealant measure may run UDS+ too. Groups whose Reports permission covers every report (FKey 0) need no row.</para>
	///<para>Open Dental's published documentation does not list report internal names. When a row whose internal name this program does not know
	///carries a UDS+ description, that row is renamed to ODUdsPlus so this program opens it and its permissions carry over. On a database from
	///25.4 or later, or of unknown origin, that is the only change made.</para>
	///A second run changes nothing.</summary>
	public static class ReportRowUdsPlus {
		///<summary>Description of the added row. Users can rename it in Report Setup.</summary>
		public const string Description="FQHC UDS+ Dental Services";
		public const string InternalNameSealant="ODDentalSealantMeasure";

		///<summary>Normalized descriptions (NormalizeDescription) that identify a UDS+ row written under another internal name.</summary>
		private static readonly string[] _arrayAdoptableDescriptions=new string[] {
			"udsplus","udsplusreport","udsplusdentalservices","fqhcudsplus","fqhcudsplusreport","fqhcudsplusdentalservices",
			"udsplusreportforfqhcdentalservices","udsplusreportfqhcdentalservices","udsplusfqhcdentalservices","udsplusforfqhcdentalservices",
		};

		///<summary>Lower-case letters and digits, with "+" read as "plus", so "UDS+ Report" and "uds plus report" compare equal.</summary>
		public static string NormalizeDescription(string description) {
			StringBuilder stringBuilder=new StringBuilder();
			foreach(char c in (description??"").ToLowerInvariant()) {
				if(c=='+') {
					stringBuilder.Append("plus");
				}
				else if(char.IsLetterOrDigit(c)) {
					stringBuilder.Append(c);
				}
			}
			return stringBuilder.ToString();
		}

		///<summary>Runs the step. isFromBefore25_4 is true for a database that comes from before Open Dental 25.4 (the row is added); false for a later
		///or unknown one (only a row under another name is adopted). Returns one line per change.</summary>
		public static List<string> Run(bool isFromBefore25_4) {
			List<string> listChanges=new List<string>();
			List<Row> listRows=GetRows();
			if(listRows.Any(x => x.InternalName==DisplayReports.ReportNames.UdsPlus)) {
				return listChanges;
			}
			List<Row> listMatches=listRows.FindAll(x => !string.IsNullOrWhiteSpace(x.InternalName)
				&& !DisplayReports.ReportNames.IsKnown(x.InternalName)
				&& x.InternalName!=DisplayReports.ReportNames.WebSchedAppointments
				&& _arrayAdoptableDescriptions.Contains(NormalizeDescription(x.Description)));
			if(listMatches.Count==1) {
				DataCore.NonQ("UPDATE displayreport SET InternalName='"+POut.String(DisplayReports.ReportNames.UdsPlus)+"' "
					+"WHERE DisplayReportNum="+POut.Long(listMatches[0].DisplayReportNum));
				listChanges.Add("Renamed displayreport "+listMatches[0].InternalName+" to "+DisplayReports.ReportNames.UdsPlus+" (UDS+ description)");
				return listChanges;
			}
			if(!isFromBefore25_4 || listMatches.Count>1) {
				//Two or more candidates are ambiguous: nothing is renamed or added.
				return listChanges;
			}
			Row rowSealant=listRows.FirstOrDefault(x => x.InternalName==InternalNameSealant);
			int itemOrder;
			bool isHidden=false;
			if(rowSealant!=null && rowSealant.Category==DisplayReportCategory.PublicHealth) {
				//Right after the sealant measure, moving the later Public Health rows down one place. Report Setup expects ItemOrder to run 0, 1, 2, ...
				DataCore.NonQ("UPDATE displayreport SET ItemOrder=ItemOrder+1 WHERE Category="+POut.Int((int)DisplayReportCategory.PublicHealth)
					+" AND ItemOrder>"+POut.Int(rowSealant.ItemOrder));
				itemOrder=rowSealant.ItemOrder+1;
				isHidden=rowSealant.IsHidden;
			}
			else {
				itemOrder=PIn.Int(DataCore.GetScalar("SELECT COALESCE(MAX(ItemOrder)+1,0) FROM displayreport WHERE Category="
					+POut.Int((int)DisplayReportCategory.PublicHealth)));
				if(rowSealant!=null) {
					isHidden=rowSealant.IsHidden;
				}
			}
			long displayReportNum=DataCore.NonQ("INSERT INTO displayreport (InternalName,ItemOrder,Description,Category,IsHidden,IsVisibleInSubMenu) VALUES ("
				+"'"+POut.String(DisplayReports.ReportNames.UdsPlus)+"',"+POut.Int(itemOrder)+",'"+POut.String(Description)+"',"
				+POut.Int((int)DisplayReportCategory.PublicHealth)+","+POut.Bool(isHidden)+",0)",true);
			listChanges.Add("Added displayreport "+DisplayReports.ReportNames.UdsPlus+(isHidden ? " (hidden, as the sealant measure is)" : ""));
			if(rowSealant!=null) {
				int count=CopyReportPermissions(rowSealant.DisplayReportNum,displayReportNum);
				if(count>0) {
					listChanges.Add("Copied "+count+" report permission(s) from "+InternalNameSealant+" to "+DisplayReports.ReportNames.UdsPlus);
				}
			}
			return listChanges;
		}

		///<summary>Gives every user group that may run the source report the same permission for the target report, unless it already has it. Returns
		///the number of rows added.</summary>
		private static int CopyReportPermissions(long displayReportNumFrom,long displayReportNumTo) {
			int permType=(int)EnumPermType.Reports;
			DataTable table=DataCore.GetTable("SELECT DISTINCT UserGroupNum FROM grouppermission WHERE PermType="+POut.Int(permType)
				+" AND FKey="+POut.Long(displayReportNumFrom)
				+" AND UserGroupNum NOT IN (SELECT UserGroupNum FROM grouppermission WHERE PermType="+POut.Int(permType)
				+" AND FKey="+POut.Long(displayReportNumTo)+")");
			foreach(DataRow dataRow in table.Rows) {
				DataCore.NonQ("INSERT INTO grouppermission (NewerDate,NewerDays,UserGroupNum,PermType,FKey) VALUES ("
					+POut.Date(DateTime.MinValue)+",0,"+POut.Long(PIn.Long(dataRow[0].ToString()))+","+POut.Int(permType)+","+POut.Long(displayReportNumTo)+")");
			}
			return table.Rows.Count;
		}

		private static List<Row> GetRows() {
			DataTable table=DataCore.GetTable("SELECT DisplayReportNum,InternalName,ItemOrder,Description,Category,IsHidden FROM displayreport "
				+"ORDER BY Category,ItemOrder,DisplayReportNum");
			List<Row> listRows=new List<Row>();
			foreach(DataRow dataRow in table.Rows) {
				listRows.Add(new Row {
					DisplayReportNum=PIn.Long(dataRow["DisplayReportNum"].ToString()),
					InternalName=dataRow["InternalName"].ToString(),
					ItemOrder=PIn.Int(dataRow["ItemOrder"].ToString()),
					Description=dataRow["Description"].ToString(),
					Category=(DisplayReportCategory)PIn.Int(dataRow["Category"].ToString()),
					IsHidden=PIn.Bool(dataRow["IsHidden"].ToString()),
				});
			}
			return listRows;
		}

		///<summary>The columns of a displayreport row the step reads, straight from the table, not the cache.</summary>
		private class Row {
			public long DisplayReportNum;
			public string InternalName;
			public int ItemOrder;
			public string Description;
			public DisplayReportCategory Category;
			public bool IsHidden;
		}
	}

	///<summary>Runs ReportRowUdsPlus after the 26.1 schema and seed steps. Level 2027003501 (range 2027003501 to 2027003599, package M2-UDS; the
	///single-wave build gives each package a yyyy00PPNN range, see ForkMigrationRunner.ValidateStep). The origin is judged as Seed26_1Step judges
	///it.</summary>
	[ForkMigration(2027003501,"Report row for the UDS+ report")]
	public class ReportRowUdsPlusStep:ForkMigrationStep {
		public override void Apply(ForkMigrationContext context) {
			if(!context.TableExists("displayreport") || !context.TableExists("grouppermission")) {
				return;
			}
			foreach(string change in ReportRowUdsPlus.Run(Seeds26_1.Seed26_1Step.IsFromBefore25_4(context))) {
				context.Log(change);
			}
		}
	}
}
