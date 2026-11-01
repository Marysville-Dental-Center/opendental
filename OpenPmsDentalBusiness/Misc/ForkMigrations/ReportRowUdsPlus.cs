// displayreport row for the UDS+ report that Open Dental added in 25.4 (package M2-UDS in docs/specs/port-backlog.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenPmsDentalBusiness.ForkMigrations.Features {
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

		///<summary>What the step does on listRows (every displayreport row, in the order ReportRows25_3.GetRows reads them), worked out without writing.
		///Run applies it; AdoptionValidation's migration-preview and adopt's row check use it too, so the three never disagree. isFromBefore25_4 as for
		///Run. listRows is not changed.</summary>
		public static RowPlan Plan(List<ReportRows25_3.DisplayReportRow> listRows,bool isFromBefore25_4) {
			RowPlan plan=new RowPlan();
			plan.RowExisting=listRows.FirstOrDefault(x => x.InternalName==DisplayReports.ReportNames.UdsPlus)?.Copy();
			if(plan.RowExisting!=null) {
				return plan;
			}
			plan.ListMatches=listRows.FindAll(x => !string.IsNullOrWhiteSpace(x.InternalName)
				&& !DisplayReports.ReportNames.IsKnown(x.InternalName)
				&& x.InternalName!=DisplayReports.ReportNames.WebSchedAppointments
				&& _arrayAdoptableDescriptions.Contains(NormalizeDescription(x.Description))).Select(x => x.Copy()).ToList();
			if(plan.ListMatches.Count==1) {
				plan.RowRenamed=plan.ListMatches[0];
				return plan;
			}
			if(!isFromBefore25_4 || plan.ListMatches.Count>1) {
				//Two or more candidates are ambiguous: nothing is renamed or added.
				return plan;
			}
			plan.IsAdded=true;
			plan.RowSealant=listRows.FirstOrDefault(x => x.InternalName==InternalNameSealant)?.Copy();
			plan.IsHidden=plan.RowSealant!=null && plan.RowSealant.IsHidden;
			List<ReportRows25_3.DisplayReportRow> listPublicHealth=listRows.FindAll(x => x.Category==DisplayReportCategory.PublicHealth);
			if(plan.IsAfterSealant) {
				//Right after the sealant measure; the later Public Health rows move down one place.
				plan.ItemOrder=plan.RowSealant.ItemOrder+1;
				plan.ListRowsMoved=listPublicHealth.FindAll(x => x.ItemOrder>plan.RowSealant.ItemOrder).Select(x => x.Copy()).ToList();
			}
			else {
				plan.ItemOrder=listPublicHealth.Count==0 ? 0 : listPublicHealth.Max(x => x.ItemOrder)+1;
			}
			return plan;
		}

		///<summary>Runs the step. isFromBefore25_4 is true for a database that comes from before Open Dental 25.4 (the row is added); false for a later
		///or unknown one (only a row under another name is adopted). Returns one line per change.</summary>
		public static List<string> Run(bool isFromBefore25_4) {
			List<string> listChanges=new List<string>();
			RowPlan plan=Plan(ReportRows25_3.GetRows(),isFromBefore25_4);
			if(plan.RowRenamed!=null) {
				DataCore.NonQ("UPDATE displayreport SET InternalName='"+POut.String(DisplayReports.ReportNames.UdsPlus)+"' "
					+"WHERE DisplayReportNum="+POut.Long(plan.RowRenamed.DisplayReportNum));
				listChanges.Add(DescribeRename(plan.RowRenamed.InternalName));
				return listChanges;
			}
			if(!plan.IsAdded) {
				return listChanges;
			}
			if(plan.IsAfterSealant) {
				//Moving the later Public Health rows down one place. Report Setup expects ItemOrder to run 0, 1, 2, ...
				DataCore.NonQ("UPDATE displayreport SET ItemOrder=ItemOrder+1 WHERE Category="+POut.Int((int)DisplayReportCategory.PublicHealth)
					+" AND ItemOrder>"+POut.Int(plan.RowSealant.ItemOrder));
			}
			long displayReportNum=ReportRows25_3.InsertRow(DisplayReports.ReportNames.UdsPlus,plan.ItemOrder,Description,DisplayReportCategory.PublicHealth,
				plan.IsHidden);
			listChanges.Add(DescribeAdd(plan.IsHidden));
			if(plan.RowSealant!=null) {
				int count=ReportRows25_3.CopyReportPermissions(plan.RowSealant.DisplayReportNum,displayReportNum);
				if(count>0) {
					listChanges.Add(DescribeCopy(count));
				}
			}
			return listChanges;
		}

		///<summary>The change line of the rename, as the step logs it.</summary>
		public static string DescribeRename(string internalNameFrom) {
			return "Renamed displayreport "+internalNameFrom+" to "+DisplayReports.ReportNames.UdsPlus+" (UDS+ description)";
		}

		///<summary>The change line of the added row, as the step logs it.</summary>
		public static string DescribeAdd(bool isHidden) {
			return "Added displayreport "+DisplayReports.ReportNames.UdsPlus+(isHidden ? " (hidden, as the sealant measure is)" : "");
		}

		///<summary>The change line of the copied report permissions, as the step logs it.</summary>
		public static string DescribeCopy(int count) {
			return "Copied "+count+" report permission(s) from "+InternalNameSealant+" to "+DisplayReports.ReportNames.UdsPlus;
		}

		///<summary>What the step does on a given set of rows (Plan). At most one of RowExisting, RowRenamed and IsAdded applies.</summary>
		public class RowPlan {
			///<summary>The row that already has this program's name; then nothing changes.</summary>
			public ReportRows25_3.DisplayReportRow RowExisting;
			///<summary>Rows under names this program does not know whose description is a UDS+ one (when no row has our name).</summary>
			public List<ReportRows25_3.DisplayReportRow> ListMatches=new List<ReportRows25_3.DisplayReportRow>();
			///<summary>The row renamed to ODUdsPlus, as it is before (its old name): the only match. Null when there is none, or more than one.</summary>
			public ReportRows25_3.DisplayReportRow RowRenamed;
			///<summary>True when the row is added (a database from before 25.4 with no row of the report).</summary>
			public bool IsAdded;
			///<summary>The ItemOrder and IsHidden of the added row.</summary>
			public int ItemOrder;
			public bool IsHidden;
			///<summary>The FQHC Dental Sealant Measure row, whose report permissions the added row gets. Null when there is none.</summary>
			public ReportRows25_3.DisplayReportRow RowSealant;
			///<summary>The Public Health rows after the sealant measure, as they are before; each moves down one place (ItemOrder+1).</summary>
			public List<ReportRows25_3.DisplayReportRow> ListRowsMoved=new List<ReportRows25_3.DisplayReportRow>();

			///<summary>True when the added row goes right after the sealant measure (a Public Health row); otherwise it is appended to Public Health.
			///</summary>
			public bool IsAfterSealant {
				get { return IsAdded && RowSealant!=null && RowSealant.Category==DisplayReportCategory.PublicHealth; }
			}

			public bool IsAmbiguous {
				get { return RowExisting==null && ListMatches.Count>1; }
			}
		}
	}

	///<summary>Runs ReportRowUdsPlus after the 26.1 schema and seed steps. Level 2026110103 (package M2-UDS; renumbered on 2026-11-01 from the
	///retired package range level 2027003501). The origin is judged as Seed26_1Step judges it.</summary>
	[ForkMigration(2026110103,"Report row for the UDS+ report")]
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
