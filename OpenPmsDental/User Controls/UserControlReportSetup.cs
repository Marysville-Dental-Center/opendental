using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using OpenPmsDental.UI;

namespace OpenPmsDental.User_Controls {
	///<summary>The Display Settings and Security Permissions tabs of Report Setup (and the report permissions of the Central Enterprise Management
	///Tool). Redesigned with Open Dental 26.2 (items 31 and 32): one list of every report under a row per category, in the order of the Reports
	///window, instead of a small grid per category, and the internal name of each report as a column instead of a label under the grids. Every
	///setting of 26.1 is kept; the rules are in ReportSetupLayout and ReportPermissionEdit.</summary>
	public partial class UserControlReportSetup:UserControl {
		public List<DisplayReport> ListDisplayReportAll;
		public List<GroupPermission> ListGroupPermissionsForReports;
		public List<GroupPermission> ListGroupPermissionsOld;
		private List<UserGroup> _listUserGroups;
		private bool _isPermissionMode=false;
		private long _userGroupNum;
		private bool _isCEMT;
		///<summary>The report of the selected cell, kept while the list is filled again. Null when nothing or a category row is selected.</summary>
		private DisplayReport _displayReportSelected=null;
		///<summary>The column of the selected cell, -1 when none.</summary>
		private int _colSelected=-1;
		///<summary>Whether the office uses the Arizona Primary Care reports, read once per refresh of the data.</summary>
		private bool _isArizonaPrimaryCareUsed;
		private static readonly Color _colorCategoryRow=Color.FromArgb(224,230,238);
		//Columns of the list. Display Settings: name, internal name, Favorite, Hidden. Security Permissions: name, internal name, Allowed.
		private const int COL_NAME=0;
		private const int COL_INTERNAL=1;
		private const int COL_FAVORITE=2;
		private const int COL_HIDDEN=3;
		private const int COL_ALLOWED=2;

		public UserControlReportSetup() {
			InitializeComponent();
			Font=LayoutManagerForms.FontInitial;
			gridReports.ColorSelectedRow=Color.FromArgb(30,55,55,55); //set a transparent colour so you see which cell is selected.
			//Only the display name of a report can be typed into, and only on the Display Settings tab; never a category row.
			gridReports.FuncCellIsEditable=(col,row) => !_isPermissionMode && col==COL_NAME && GetReportAt(row)!=null;
		}

		public void InitializeOnStartup(bool refreshData,long userGroupNum,bool isPermissionMode,bool isCEMT=false) {
			CommitPendingEdit();//A name typed on the Display Settings tab before switching tabs.
			_userGroupNum=userGroupNum;
			_isPermissionMode=isPermissionMode;
			_isCEMT=isCEMT;
			butUp.Visible=!_isPermissionMode;
			butDown.Visible=!_isPermissionMode;
			labelInstructions.Visible=!_isPermissionMode;
			butSetAll.Visible=_isPermissionMode;
			butSetNone.Visible=_isPermissionMode;
			comboUserGroup.Visible=_isPermissionMode;
			labelUserGroup.Visible=_isPermissionMode;
			labelAllowedCount.Visible=_isPermissionMode;
			if(refreshData) {
				ListDisplayReportAll=DisplayReports.GetAll(true);
				if(_isCEMT) {//CEMT can only synchronize display reports with internal names.
					ListDisplayReportAll.RemoveAll(x => string.IsNullOrWhiteSpace(x.InternalName));
				}
				ListGroupPermissionsForReports=GroupPermissions.GetPermsForReports();
				ListGroupPermissionsOld=new List<GroupPermission>();
				foreach(GroupPermission perm in ListGroupPermissionsForReports) {
					ListGroupPermissionsOld.Add(perm.Copy());
				}
				if(!isCEMT) {
					_listUserGroups=UserGroups.GetList();
				}
				else {
					_listUserGroups=UserGroups.GetList(true);
				}
				comboUserGroup.Items.Clear();
				for(int i=0;i<_listUserGroups.Count;i++) {
					comboUserGroup.Items.Add(_listUserGroups[i].Description);
					if(_listUserGroups[i].UserGroupNum==_userGroupNum) {
						comboUserGroup.SelectedIndex=i;
					}
				}
				if(comboUserGroup.SelectedIndex==-1 && comboUserGroup.Items.Count>0) {
					comboUserGroup.SelectedIndex=0;
				}
				_isArizonaPrimaryCareUsed=FormReportsMore.UsingArizonaPrimaryCare();
			}
			FillGrid();
		}

		///<summary>Saves the display name being typed into the list. The Save button of Report Setup calls it before it saves the reports.</summary>
		public void CommitPendingEdit() {
			if(_isPermissionMode || _displayReportSelected==null || ListDisplayReportAll==null) {
				return;
			}
			int row=GetRowOfReport(_displayReportSelected);
			if(row<0) {
				return;
			}
			_displayReportSelected.Description=gridReports.ListGridRows[row].Cells[COL_NAME].Text;
		}

		///<summary>The user group the Security Permissions tab shows, 0 when there is none.</summary>
		private long GetUserGroupNumSelected() {
			if(_listUserGroups==null || comboUserGroup.SelectedIndex<0 || comboUserGroup.SelectedIndex>=_listUserGroups.Count) {
				return 0;
			}
			return _listUserGroups[comboUserGroup.SelectedIndex].UserGroupNum;
		}

		///<summary>The report of a row of the list; null for a category row or a row out of range.</summary>
		private DisplayReport GetReportAt(int row) {
			if(row<0 || row>=gridReports.ListGridRows.Count) {
				return null;
			}
			return gridReports.ListGridRows[row].Tag as DisplayReport;
		}

		private int GetRowOfReport(DisplayReport displayReport) {
			for(int i=0;i<gridReports.ListGridRows.Count;i++) {
				if(gridReports.ListGridRows[i].Tag==displayReport) {
					return i;
				}
			}
			return -1;
		}

		///<summary>Fills the list: a row per category (ReportSetupLayout.ArrayCategories) and its reports under it in the order of the Reports window. If
		///columns are reordered or added, the COL_ constants and gridReports_CellClick follow.</summary>
		private void FillGrid() {
			if(ListDisplayReportAll is null){
				return;
			}
			long userGroupNum=GetUserGroupNumSelected();
			gridReports.BeginUpdate();
			gridReports.Columns.Clear();
			if(_isPermissionMode) {
				gridReports.Columns.Add(new GridColumn(Lan.g(this,"Report"),230));
				gridReports.Columns.Add(new GridColumn(Lan.g(this,"Internal Name"),180));
				gridReports.Columns.Add(new GridColumn(Lan.g(this,"Allowed"),60,HorizontalAlignment.Center){ IsWidthDynamic=true });
			}
			else {
				gridReports.Columns.Add(new GridColumn(Lan.g(this,"Display Name"),200,true));
				gridReports.Columns.Add(new GridColumn(Lan.g(this,"Internal Name"),160));
				gridReports.Columns.Add(new GridColumn(Lan.g(this,"Favorite"),60,HorizontalAlignment.Center){ IsWidthDynamic=true });
				gridReports.Columns.Add(new GridColumn(Lan.g(this,"Hidden"),60,HorizontalAlignment.Center){ IsWidthDynamic=true });
			}
			gridReports.ListGridRows.Clear();
			int rowSelected=-1;
			foreach(ReportSetupLayout.Entry entry in ReportSetupLayout.GetEntries(ListDisplayReportAll)) {
				GridRow row=new GridRow();
				if(entry.IsCategory) {
					row.Cells.Add(Lan.g(this,ReportSetupLayout.GetCategoryTitle(entry.Category)));
					for(int i=1;i<gridReports.Columns.Count;i++) {
						row.Cells.Add("");
					}
					row.Bold=true;
					row.ColorBackG=_colorCategoryRow;
					gridReports.ListGridRows.Add(row);
					continue;
				}
				DisplayReport displayReport=entry.DisplayReport;
				if(_isPermissionMode) {
					row.Cells.Add(displayReport.Description+(displayReport.IsHidden ? " "+Lan.g(this,"(hidden)") : ""));
					row.Cells.Add(GetInternalName(displayReport));
					row.Cells.Add(ReportPermissionEdit.IsAllowed(ListGroupPermissionsForReports,userGroupNum,displayReport.DisplayReportNum) ? "X" : "");
				}
				else {
					row.Cells.Add(displayReport.Description);
					row.Cells.Add(GetInternalName(displayReport));
					row.Cells.Add(displayReport.IsVisibleInSubMenu ? "X" : "");
					row.Cells.Add(displayReport.IsHidden ? "X" : "");
				}
				row.Tag=displayReport;
				if(displayReport==_displayReportSelected) {
					rowSelected=gridReports.ListGridRows.Count;
					row.ColorBackG=Color.LightCyan;
				}
				gridReports.ListGridRows.Add(row);
			}
			gridReports.EndUpdate();
			if(rowSelected>=0) {
				if(_colSelected>=0 && _colSelected<gridReports.Columns.Count) {
					gridReports.SetSelected(new Point(_colSelected,rowSelected));
				}
				else {
					gridReports.SetSelected(rowSelected,true);
				}
			}
			if(_isPermissionMode) {
				List<DisplayReport> listShown=ReportSetupLayout.GetEntries(ListDisplayReportAll).Where(x => !x.IsCategory).Select(x => x.DisplayReport).ToList();
				labelAllowedCount.Text=Lan.g(this,"Reports allowed:")+" "
					+ReportPermissionEdit.CountAllowed(ListGroupPermissionsForReports,userGroupNum,listShown)+" / "+listShown.Count;
			}
		}

		private void gridReports_CellClick(object sender,ODGridClickEventArgs e) {
			//Save the name typed into the cell that was selected before this one.
			CommitPendingEdit();
			_displayReportSelected=GetReportAt(e.Row);
			_colSelected=e.Col;
			if(_displayReportSelected==null) {//A category row: nothing to change.
				FillGrid();
				return;
			}
			if(_isPermissionMode) {
				long userGroupNum=GetUserGroupNumSelected();
				if(e.Col==COL_ALLOWED && userGroupNum!=0) {
					ReportPermissionEdit.Toggle(ListGroupPermissionsForReports,userGroupNum,_displayReportSelected,ListDisplayReportAll,_isArizonaPrimaryCareUsed);
				}
			}
			else {
				if(e.Col==COL_FAVORITE) {
					ReportSetupLayout.ToggleFavorite(_displayReportSelected);
				}
				else if(e.Col==COL_HIDDEN) {
					ReportSetupLayout.ToggleHidden(_displayReportSelected);
				}
			}
			FillGrid();
		}

		///<summary>Returns the appropriate user-friendly internal name.</summary>
		private string GetInternalName(DisplayReport displayReport) {
			switch(displayReport.InternalName) {
				case "ODToday":
					return "Today";
				case "ODYesterday":
					return "Yesterday";
				case "ODThisMonth":
					return "This Month";
				case "ODLastMonth":
					return "Last Month";
				case "ODThisYear":
					return "This Year";
				case "ODMoreOptions":
					return "More Options";
				case "ODProviderPayrollSummary":
					return "Provider Payroll Summary";
				case "ODProviderPayrollDetailed":
					return "Provider Payroll Detailed";
				case "ODAdjustments":
					return "Adjustments";
				case "ODPayments":
					return "Payments";
				case DisplayReports.ReportNames.Procedures://Before 24.4. The fork migration renames it to ProceduresIndividual.
					return "Procedures";
				case "ODWriteoffs":
					return "Write-offs";
				case DisplayReports.ReportNames.IncompleteProcNotes:
					return "Incomplete Proc Notes";
				case "ODRoutingSlips":
					return "Routing Slips";
				case "ODAgingAR":
					return "Aging AR";
				case DisplayReports.ReportNames.ClaimsNotSent:
					return "Claims Not Sent";
				case "ODCapitation":
					return "Capitation";
				case "ODFinanceCharge":
					return "Finance Charge";
				case DisplayReports.ReportNames.OutstandingInsClaims:
					return "Outstanding Ins Claims";
				case "ODProcsNotBilled":
					return "Procs Not Billed";
				case "ODPPOWriteoffs":
					return "PPO Write-offs";
				case "ODPaymentPlans":
					return "Payment Plans";
				case "ODReceivablesBreakdown":
					return "Receivables Breakdown";
				case "ODUnearnedIncome":
					return "Unearned Income";
				case "ODInsuranceOverpaid":
					return "Insurance Overpaid";
				case "ODActivePatients":
					return "Active Patients";
				case "ODAppointments":
					return "Appointments";
				case "ODBirthdays":
					return "Birthdays";
				case "ODBrokenAppointments":
					return "Broken Appointments";
				case "ODInsurancePlans":
					return "Insurance Plans";
				case "ODNewPatients":
					return "New Patients";
				case "ODPatientsRaw":
					return "Patients Raw";
				case "ODPatientNotes":
					return "Patient Notes";
				case "ODPrescriptions":
					return "Prescriptions";
				case "ODProcedureCodes":
					return "Procedure Codes";
				case DisplayReports.ReportNames.ODProcOverpaid:
					return "Procedures Overpaid";
				case "ODReferralsRaw":
					return "Referrals Raw";
				case "ODReferralAnalysis":
					return "Referral Analysis";
				case DisplayReports.ReportNames.ReferredProcTracking:
					return "Referred Proc Tracking";
				case DisplayReports.ReportNames.TreatmentFinder:
					return "Treatment Finder";
				case "ODRawScreeningData":
					return "Raw Screening Data";
				case "ODRawPopulationData":
					return "Raw Population Data";
				case "ODEligibilityFile":
					return "Eligibility File";
				case "ODEncounterFile":
					return "Encounter File";
				case "ODPresentedTreatmentProd":
					return "Presented Treatment Prod";
				case "ODTreatmentPresentationStats":
					return "Treatment Presentation Stats";
				case "ODNetProdDetailDaily":
					return "Net Prod Detail Daily";
				case "ODDentalSealantMeasure":
					return "Dental Sealant Measure";
				case "ODInsurancePayPlansPastDue":
					return "Insurance Pay Plans Past Due";
				case DisplayReports.ReportNames.UnfinalizedInsPay:
					return "Unfinalized Insurance";
				case DisplayReports.ReportNames.PatPortionUncollected:
					return "Patient Portion Uncollected";
				case DisplayReports.ReportNames.InsAging:
					return "Insurance Aging Report";
				case DisplayReports.ReportNames.EraAutoProcessed:
					return "ERAs Automatically Processed Report";
				case "ODMonthlyProductionGoal":
					return "Monthly Production Goal";
				case DisplayReports.ReportNames.ProceduresIndividual:
					return "Procedures Individual";
				case DisplayReports.ReportNames.ProductionByProcedure:
					return "Production By Procedure";
				case DisplayReports.ReportNames.InsPendingSupplementals:
					return "Insurance Pending Supplementals";
				case DisplayReports.ReportNames.UdsPlus:
					return "UDS+ Dental Services";
				case "ODCustomAging":
					return "Custom Aging";
				case "ODDynamicPayPlanOvercharged":
					return "Dynamic Pay Plan Overcharged";
				case "ODDiscountPlan":
					return "Discount Plan";
				case "ODHiddenPaySplits":
					return "Hidden Pay Splits";
				default:
					return "";
			}
		}

		private void gridReports_CellLeave(object sender,ODGridClickEventArgs e) {
			CommitPendingEdit();
		}

		private void butUp_Click(object sender,EventArgs e) {
			MoveSelected(isUp:true);
		}

		private void butDown_Click(object sender,EventArgs e) {
			MoveSelected(isUp:false);
		}

		private void MoveSelected(bool isUp) {
			CommitPendingEdit();
			if(_displayReportSelected==null) {
				MsgBox.Show(this,"Please select a report first.");
				return;
			}
			if(!ReportSetupLayout.Move(ListDisplayReportAll,_displayReportSelected,isUp)) {
				return;//Already the first or the last of its category.
			}
			FillGrid();
		}

		private void butSetAll_Click(object sender,EventArgs e) {
			long userGroupNum=GetUserGroupNumSelected();
			if(userGroupNum==0) {
				return;
			}
			ReportPermissionEdit.SetAll(ListGroupPermissionsForReports,userGroupNum);
			FillGrid();
		}

		private void butSetNone_Click(object sender,EventArgs e) {
			long userGroupNum=GetUserGroupNumSelected();
			if(userGroupNum==0) {
				return;
			}
			ReportPermissionEdit.SetNone(ListGroupPermissionsForReports,userGroupNum);
			FillGrid();
		}

		private void comboUserGroup_SelectionChangeCommitted(object sender,EventArgs e) {
			FillGrid();
		}
	}
}
