using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;
using System.Linq;
using CodeBase;

namespace OpenDentBusiness{
	///<summary></summary>
	public class DisplayReports{
		#region CachePattern

		private class DisplayReportCache:CacheListAbs<DisplayReport> {
			protected override DisplayReport Copy(DisplayReport displayReport) {
				return displayReport.Copy();
			}

			protected override void FillCacheIfNeeded() {
				DisplayReports.GetTableFromCache(false);
			}

			protected override List<DisplayReport> GetCacheFromDb() {
				string command="SELECT * FROM displayreport ORDER BY ItemOrder";
				return Crud.DisplayReportCrud.SelectMany(command);
			}

			protected override DataTable ListToTable(List<DisplayReport> listDisplayReports) {
				return Crud.DisplayReportCrud.ListToTable(listDisplayReports,"DisplayReport");
			}

			protected override List<DisplayReport> TableToList(DataTable table) {
				return Crud.DisplayReportCrud.TableToList(table);
			}

			protected override bool IsInListShort(DisplayReport displayReport) {
				return  !displayReport.IsHidden;
			}
		}

		///<summary>The object that accesses the cache in a thread-safe manner.</summary>
		private static DisplayReportCache _displayReportCache=new DisplayReportCache();

		public static List<DisplayReport> GetDeepCopy(bool isShort=false) {
			return _displayReportCache.GetDeepCopy(isShort);
		}

		public static List<DisplayReport> GetWhere(Predicate<DisplayReport> match,bool isShort=false) {
			return _displayReportCache.GetWhere(match,isShort);
		}

		///<summary>Refreshes the cache and returns it as a DataTable. This will refresh the ClientWeb's cache and the ServerWeb's cache.</summary>
		public static DataTable RefreshCache() {
			return GetTableFromCache(true);
		}

		///<summary>Fills the local cache with the passed in DataTable.</summary>
		public static void FillCacheFromTable(DataTable table) {
			_displayReportCache.FillCacheFromTable(table);
		}

		///<summary>Always refreshes the ClientWeb's cache.</summary>
		public static DataTable GetTableFromCache(bool doRefreshCache) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				DataTable table=Meth.GetTable(MethodBase.GetCurrentMethod(),doRefreshCache);
				_displayReportCache.FillCacheFromTable(table);
				return table;
			}
			return _displayReportCache.GetTableFromCache(doRefreshCache);
		}

		public static void ClearCache() {
			_displayReportCache.ClearCache();
		}
		#endregion

		///<summary>Returns all reports that should show in the main menu bar.  Ordered by Description.</summary>
		public static List<DisplayReport> GetSubMenuReports() {
			Meth.NoCheckMiddleTierRole();
			return GetWhere(x => x.IsVisibleInSubMenu,true).OrderBy(x => x.Description).ToList();
		}

		///<summary>Get all display reports for the passed-in category.  Pass in true to retrieve hidden display reports.</summary>
		public static List<DisplayReport> GetForCategory(DisplayReportCategory displayReportCategory, bool showHidden) {
			Meth.NoCheckMiddleTierRole();
			return GetWhere(x => x.Category==displayReportCategory && !IsRemoved(x),!showHidden);
		}

		///<summary>Pass in true to also retrieve hidden display reports.</summary>
		public static List<DisplayReport> GetAll(bool showHidden) {
			Meth.NoCheckMiddleTierRole();
			return GetDeepCopy(!showHidden).FindAll(x => !IsRemoved(x));
		}

		///<summary>True for display reports whose report was removed. The Web Sched Appointments report listed appointments made through Open Dental's
		///hosted Web Sched, which was removed. Its displayreport row stays in the database but is not shown.</summary>
		private static bool IsRemoved(DisplayReport displayReport) {
			return displayReport.InternalName==ReportNames.WebSchedAppointments;
		}

		///<summary>Use DisplayReports.ReportNames to pass this method an internal report name. Returns null if no matches are found.
		///Returns first result if multiple results are found (this shouldn't happen).</summary>
		public static DisplayReport GetByInternalName(string reportName) {
			Meth.NoCheckMiddleTierRole();
			List<DisplayReport> listDisplayReports=GetWhere(x => x.InternalName==reportName);
			if(listDisplayReports.IsNullOrEmpty()) {
				return null;
			}
			return listDisplayReports[0];
		}

		///<summary>Gets all DisplayReports that are known to HQ (InternalName set) without using the local cache.</summary>
		public static List<DisplayReport> GetDisplayReportsNoCache() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<DisplayReport>>(MethodBase.GetCurrentMethod());
			}
			string command="SELECT * FROM displayreport WHERE InternalName!=''";
			return Crud.DisplayReportCrud.SelectMany(command);

		}

		///<summary>Must pass in a list of all current display reports, even hidden ones.</summary>
		public static bool Sync(List<DisplayReport> listDisplayReports) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),listDisplayReports);
			}
			return Crud.DisplayReportCrud.Sync(listDisplayReports,GetAll(true));//TODO: cache?
		}

		public class ReportNames {
			public const string UnfinalizedInsPay="ODUnfinalizedInsPay";
			public const string PatPortionUncollected="ODPatPortionUncollected";
			public const string WebSchedAppointments="ODWebSchedAppointments";
			public const string InsAging = "ODInsAging";
			public const string CustomAging = "ODCustomAging";
			public const string OutstandingInsClaims="ODOutstandingInsClaims";
			public const string ClaimsNotSent="ODClaimsNotSent";
			public const string TreatmentFinder="ODTreatmentFinder";
			public const string ReferredProcTracking="ODReferredProcTracking";
			public const string IncompleteProcNotes="ODIncompleteProcNotes";
			public const string ProcNotBilledIns="ODProcsNotBilled";
			///<summary>Production and Income More Options</summary>
			public const string ODMoreOptions="ODMoreOptions";
			public const string ODProcOverpaid="ODProcOverpaid";
			public const string DPPOvercharged="ODDynamicPayPlanOvercharged";
			public const string MonthlyProductionGoal="ODMonthlyProductionGoal";
			public const string EraAutoProcessed="ODEraAutoProcessed";
			///<summary>The Daily Procedures report of 24.3 and earlier, with its Individual/Grouped choice. From 24.4 its row is the itemized
			///Procedures report (ProceduresIndividual); a database that still has this name opens that report too.</summary>
			public const string Procedures="ODProcedures";
			///<summary>24.4: the itemized, interactive Procedures report (Daily). The name is the one our 24.3.63 Report Setup code already lists.
			///</summary>
			public const string ProceduresIndividual="ODProceduresIndividual";
			///<summary>24.4: Production by Procedure (Daily), the old "Grouped By Procedure Code" mode. The name is the one our 24.3.63 Report Setup
			///code already lists.</summary>
			public const string ProductionByProcedure="ODProductionByProcedure";
			///<summary>25.2: Insurance Pending Supplementals (Lists). Proposed name: Open Dental's published documentation does not list report
			///names, so confirm it against an adopted database (port-backlog A-RPT).</summary>
			public const string InsPendingSupplementals="ODInsPendingSupplementals";

			///<summary>Every internal name that FormReportsMore.OpenReportHelper opens. A displayreport row whose name is not in this list is a report this
			///program does not know, for example one a later Open Dental version added. Kept in step with the dispatcher by a test.</summary>
			public static readonly string[] ArrayKnownNames=new string[] {
				"ODToday","ODYesterday","ODThisMonth","ODLastMonth","ODThisYear",ODMoreOptions,"ODProviderPayrollSummary","ODProviderPayrollDetailed",
				"ODAdjustments","ODPayments",Procedures,ProceduresIndividual,ProductionByProcedure,ODProcOverpaid,"ODWriteoffs",IncompleteProcNotes,
				"ODRoutingSlips","ODNetProdDetailDaily",UnfinalizedInsPay,PatPortionUncollected,"ODAgingAR",ClaimsNotSent,"ODCapitation",
				"ODFinanceCharge",OutstandingInsClaims,ProcNotBilledIns,"ODPPOWriteoffs","ODPaymentPlans","ODReceivablesBreakdown","ODUnearnedIncome",
				"ODInsuranceOverpaid","ODPresentedTreatmentProd","ODTreatmentPresentationStats","ODInsurancePayPlansPastDue",InsAging,CustomAging,
				"ODActivePatients","ODAppointments","ODBirthdays","ODBrokenAppointments","ODInsurancePlans","ODNewPatients","ODPatientsRaw",
				"ODPatientNotes","ODPrescriptions","ODProcedureCodes","ODReferralsRaw","ODReferralAnalysis",ReferredProcTracking,TreatmentFinder,
				"ODRawScreeningData","ODRawPopulationData","ODDentalSealantMeasure","ODEligibilityFile","ODEncounterFile","ODDiscountPlan",
				MonthlyProductionGoal,"ODHiddenPaySplits",DPPOvercharged,EraAutoProcessed,InsPendingSupplementals,
			};

			///<summary>True if this program opens a report with this internal name. Case-sensitive, as the dispatcher is.</summary>
			public static bool IsKnown(string internalName) {
				return Array.IndexOf(ArrayKnownNames,internalName)>=0;
			}
		}
	}
}