// Service Date View in the Account module (package M2-SDV in docs/specs/port-backlog.md; Open Dental 25.4 item v25_4-account-service-date-view and
// 26.1 item v26_1-account-service-date-view). The main grid can switch between the regular, chronological view and the service-date view, which
// groups the ledger by date of service with the credits applied to each charge indented beneath it. The Service Date View window stays
// available for printing and saving to Imaging; it follows the selected patient and the Family/Patient choice.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class ControlAccount {
		#region Fields - Service Date View
		///<summary>The main grid in service-date view. It lies over gridAccount (same tab page, docked) and only one of the two is visible.</summary>
		private GridOD gridServiceDateView;
		///<summary>Show tab: insurance estimate and patient portion on every procedure of the service-date view.</summary>
		private OpenPmsDental.UI.CheckBox checkServiceDateViewDetails;
		private MenuItem _menuItemServiceDateViewToggle;
		private bool _isServiceDateView;
		///<summary>The user whose settings were loaded last; the settings are loaded again after another user logs on.</summary>
		private long _userNumServiceDateView=-1;
		private List<ServiceDateViewRow> _listServiceDateViewRows=new List<ServiceDateViewRow>();
		///<summary>What the rows above were built from. The query runs again only when it changes (another patient, new data, other options).</summary>
		private AccountModules.LoadData _loadDataServiceDateView;
		private string _keyServiceDateView="";
		///<summary>What the Service Date View window last showed, so it is refreshed only when the module's data or patient changed.</summary>
		private AccountModules.LoadData _loadDataServiceDateViewWindow;
		private string _keyServiceDateViewWindow="";
		///<summary>Scroll position to restore after a double-click opened a row's object, or -1 to scroll to the end (as the regular grid).</summary>
		private int _scrollValueServiceDateView=-1;
		#endregion Fields - Service Date View

		///<summary>Called from the constructor: creates the service-date grid and the details checkbox, before the layout manager scales the module.</summary>
		private void ConstructServiceDateView() {
			gridServiceDateView=new GridOD();
			gridServiceDateView.Name="gridServiceDateView";
			gridServiceDateView.Title="Patient Account - Service Date View";
			gridServiceDateView.TranslationName="TableAccountServiceDateView";
			gridServiceDateView.Dock=DockStyle.Fill;
			gridServiceDateView.Margin=new Padding(0);
			gridServiceDateView.HScrollVisible=true;
			gridServiceDateView.SelectionMode=GridSelectionMode.MultiExtended;
			gridServiceDateView.ContextMenu=contextMenuAcctGrid;
			gridServiceDateView.Visible=false;
			gridServiceDateView.CellClick+=gridServiceDateView_CellClick;
			gridServiceDateView.MouseUp+=(sender,e) => MirrorServiceDateViewSelection();
			gridServiceDateView.CellDoubleClick+=gridServiceDateView_CellDoubleClick;
			LayoutManager.Add(gridServiceDateView,tabPagePatAccount);
			checkServiceDateViewDetails=new OpenPmsDental.UI.CheckBox();
			checkServiceDateViewDetails.Name="checkServiceDateViewDetails";
			checkServiceDateViewDetails.BackColor=SystemColors.Window;
			checkServiceDateViewDetails.Location=new Point(6,288);//Below checkShowCompletePayPlans (6,265).
			checkServiceDateViewDetails.Size=new Size(168,18);
			checkServiceDateViewDetails.Text="Service Date View details";
			checkServiceDateViewDetails.Visible=false;
			checkServiceDateViewDetails.Click+=checkServiceDateViewDetails_Click;
			LayoutManager.Add(checkServiceDateViewDetails,tabShow);
		}

		///<summary>Called from InitializeOnStartup: menu items and translations.</summary>
		private void InitializeServiceDateView() {
			checkServiceDateViewDetails.Text=Lan.g(this,"Service Date View details");
			gridServiceDateView.Title=Lan.g(this,"Patient Account - Service Date View");
			contextMenuAcctGrid.MenuItems.Add(new MenuItem("-"));
			_menuItemServiceDateViewToggle=new MenuItem(Lan.g(this,"Service Date View"),menuItemServiceDateViewToggle_Click);
			contextMenuAcctGrid.MenuItems.Add(_menuItemServiceDateViewToggle);
			contextMenuAcctGrid.MenuItems.Add(new MenuItem(Lan.g(this,"Service Date View Window"),menuItemServiceDateViewWindow_Click));
			//This handler must run before contextMenuAcctGrid_Popup, which reads gridAccount's selection.
			contextMenuAcctGrid.Popup-=contextMenuAcctGrid_Popup;
			contextMenuAcctGrid.Popup+=contextMenuAcctGridServiceDateView_Popup;
			contextMenuAcctGrid.Popup+=contextMenuAcctGrid_Popup;
			contextMenuStatement.MenuItems.Add(new MenuItem(Lan.g(this,"Service Date View Window"),menuItemServiceDateViewWindow_Click));
		}

		#region Methods - Service Date View
		///<summary>Called at the end of FillMain: shows the grid of the current view, refills the service-date grid when the data or the options
		///changed, and refreshes the Service Date View window in place.</summary>
		private void FillServiceDateView() {
			if(gridServiceDateView==null) {
				return;
			}
			LoadServiceDateViewUserSettings();
			SetServiceDateViewVisible();
			RefreshServiceDateViewWindow();
			if(!_isServiceDateView) {
				return;
			}
			DateTime dateFrom=DateTime.MinValue;
			DateTime dateTo=DateTime.MaxValue;
			if(textDateStart.IsValid() && textDateEnd.IsValid()) {//Same date range as the regular grid (RefreshModuleData).
				if(textDateStart.Text!="") {
					dateFrom=PIn.Date(textDateStart.Text);
				}
				if(textDateEnd.Text!="") {
					dateTo=PIn.Date(textDateEnd.Text);
				}
			}
			if(_patient==null) {
				_listServiceDateViewRows=new List<ServiceDateViewRow>();
				_loadDataServiceDateView=null;
				_keyServiceDateView="";
			}
			else {
				GetServiceDateViewPatient(out long patNum,out bool isFamily);
				string key=string.Join("|",patNum,isFamily,checkServiceDateViewDetails.Checked,dateFrom.Ticks,dateTo.Ticks);
				if(_loadDataServiceDateView!=_loadData || _keyServiceDateView!=key) {
					ServiceDateViewOptions options=new ServiceDateViewOptions();
					options.IsDetailed=checkServiceDateViewDetails.Checked;
					options.DateFrom=dateFrom;
					options.DateTo=dateTo;
					_listServiceDateViewRows=RpServiceDateView.GetRows(patNum,isFamily,options);
					_loadDataServiceDateView=_loadData;
					_keyServiceDateView=key;
				}
			}
			List<DisplayField> listDisplayFields=ServiceDateViewGrid.GetDisplayFields();
			gridServiceDateView.BeginUpdate();
			ServiceDateViewGrid.FillColumns(gridServiceDateView,listDisplayFields);
			gridServiceDateView.ListGridRows.Clear();
			for(int i=0;i<_listServiceDateViewRows.Count;i++) {
				GridRow gridRow=ServiceDateViewGrid.MakeGridRow(_listServiceDateViewRows[i],listDisplayFields);
				if(_listServiceDateViewRows[i].RowKind==ServiceDateViewRowKind.DayTotal) {
					gridRow.ColorLborder=Color.Black;//A line under each date of service.
				}
				gridServiceDateView.ListGridRows.Add(gridRow);
			}
			gridServiceDateView.EndUpdate();
			if(_scrollValueServiceDateView==-1) {
				gridServiceDateView.ScrollToEnd();
			}
			else {
				gridServiceDateView.ScrollValue=_scrollValueServiceDateView;
				_scrollValueServiceDateView=-1;
			}
		}

		///<summary>The patient whose account the module shows: the guarantor when the whole family is selected.</summary>
		private void GetServiceDateViewPatient(out long patNum,out bool isFamily) {
			patNum=_patient.PatNum;
			isFamily=IsFamilySelected();
			if(isFamily) {
				patNum=_family.Guarantor.PatNum;
			}
		}

		///<summary>Loads the user's settings when the user changed since the last load (log off, log on as another user).</summary>
		private void LoadServiceDateViewUserSettings() {
			if(Security.CurUser==null || Security.CurUser.UserNum==_userNumServiceDateView) {
				return;
			}
			_userNumServiceDateView=Security.CurUser.UserNum;
			_isServiceDateView=ServiceDateViewUserPrefs.GetBool(_userNumServiceDateView,ServiceDateViewUserPrefs.FKEY_ACCOUNT_VIEW,false);
			checkServiceDateViewDetails.Checked=ServiceDateViewUserPrefs.GetBool(_userNumServiceDateView,ServiceDateViewUserPrefs.FKEY_SHOW_DETAILS,true);
		}

		private void SetServiceDateViewVisible() {
			gridServiceDateView.Visible=_isServiceDateView;
			gridAccount.Visible=!_isServiceDateView;
			checkServiceDateViewDetails.Visible=_isServiceDateView;
			if(_isServiceDateView) {
				butServiceDateView.Text=Lan.g(this,"Transaction View");
			}
			else {
				butServiceDateView.Text=Lan.g(this,"Service Date View");
			}
		}

		///<summary>Switches the main grid between the regular view and the service-date view, and remembers the choice for the user.</summary>
		private void ToggleServiceDateView() {
			_isServiceDateView=!_isServiceDateView;
			if(Security.CurUser!=null) {
				ServiceDateViewUserPrefs.SetBool(Security.CurUser.UserNum,ServiceDateViewUserPrefs.FKEY_ACCOUNT_VIEW,_isServiceDateView);
				DataValid.SetInvalid(InvalidType.UserOdPrefs);
			}
			if(!_isServiceDateView) {
				gridServiceDateView.SetAll(false);
			}
			gridAccount.SetAll(false);
			FillServiceDateView();
		}

		///<summary>New statements made in the Account module start in the view the main grid shows (fork default). Statement types that cannot use
		///the service-date view keep the regular view.</summary>
		private void ApplyServiceDateViewToNewStatement(Statement statement) {
			Statements.SetServiceDateView(statement,_isServiceDateView);
		}

		///<summary>Opens the Service Date View window, or brings it to the front. It shows the selected patient, or the family when Entire Family
		///is selected, and follows later changes of either.</summary>
		private void ShowServiceDateViewWindow() {
			if(_patient==null) {
				return;
			}
			GetServiceDateViewPatient(out long patNum,out bool isFamily);
			if(_formRpServiceDateView?.IsDisposed!=false) {
				_formRpServiceDateView=new FormRpServiceDateView(patNum,isFamily);
				_formRpServiceDateView.FormClosed+=new FormClosedEventHandler((o,e1) => _formRpServiceDateView=null);
				_loadDataServiceDateViewWindow=_loadData;
				_keyServiceDateViewWindow=patNum+"|"+isFamily;
				_formRpServiceDateView.Show();
			}
			else {
				RefreshServiceDateViewWindow();
			}
			if(_formRpServiceDateView.WindowState==FormWindowState.Minimized) {
				_formRpServiceDateView.WindowState=FormWindowState.Normal;
			}
			_formRpServiceDateView.BringToFront();
		}

		///<summary>The window follows the module: another patient, the Family/Patient choice, or new data refresh it in place.</summary>
		private void RefreshServiceDateViewWindow() {
			if(_formRpServiceDateView==null || _formRpServiceDateView.IsDisposed || _patient==null) {
				return;
			}
			GetServiceDateViewPatient(out long patNum,out bool isFamily);
			string key=patNum+"|"+isFamily;
			if(_loadDataServiceDateViewWindow==_loadData && _keyServiceDateViewWindow==key) {
				return;
			}
			_loadDataServiceDateViewWindow=_loadData;
			_keyServiceDateViewWindow=key;
			_formRpServiceDateView.SetPatient(patNum,isFamily);
		}

		///<summary>The row of the regular account table that stands for the object of a service-date row, or -1. Lets the regular grid's actions and
		///double-click work on rows selected in the service-date grid.</summary>
		private int FindAccountRowIndex(DataTable table,ServiceDateViewRow row) {
			if(table==null || row==null || row.RowKind!=ServiceDateViewRowKind.Transaction) {
				return -1;
			}
			long priKey=row.GetPriKey();
			Func<DataRow,bool> funcMatch=null;
			Func<DataRow,bool> funcMatchSecond=null;
			if(row.IsProcedure) {
				funcMatch=x => PIn.Long(x["ProcNum"].ToString())==row.ProcNum;
			}
			else if(ServiceDateViews.IsAdjustmentType(row.TranType)) {
				funcMatch=x => PIn.Long(x["AdjNum"].ToString())==priKey;
			}
			else if(ServiceDateViews.IsPatPayType(row.TranType)) {
				funcMatch=x => PIn.Long(x["PayNum"].ToString())==row.ParentNum;
			}
			else if(ServiceDateViews.IsInsuranceType(row.TranType)) {
				//Insurance payments: the claim payment row; write-offs and estimates: the claim row.
				string claimPaymentNum=row.TranType.StartsWith("InsPay") ? "1" : "0";
				funcMatch=x => PIn.Long(x["ClaimNum"].ToString())==row.ParentNum && x["ClaimPaymentNum"].ToString()==claimPaymentNum;
				funcMatchSecond=x => PIn.Long(x["ClaimNum"].ToString())==row.ParentNum;
			}
			else if(ServiceDateViews.IsPayPlanChargeType(row.TranType)) {
				funcMatch=x => PIn.Long(x["PayPlanChargeNum"].ToString())==priKey;
				funcMatchSecond=x => PIn.Long(x["PayPlanNum"].ToString())==row.ParentNum;
			}
			else if(ServiceDateViews.IsPayPlanType(row.TranType)) {
				funcMatch=x => PIn.Long(x["PayPlanNum"].ToString())==row.ParentNum;
			}
			if(funcMatch==null) {
				return -1;
			}
			List<DataRow> listDataRows=table.Rows.OfType<DataRow>().ToList();
			int index=listDataRows.FindIndex(x => funcMatch(x));
			if(index<0 && funcMatchSecond!=null) {
				index=listDataRows.FindIndex(x => funcMatchSecond(x));
			}
			return index;
		}

		///<summary>Selects in the hidden regular grid the rows of the objects selected in the service-date grid, so the toolbar and right-click
		///actions that read gridAccount's selection (new claim, adjustment, invoice, refund) work in service-date view.</summary>
		private void MirrorServiceDateViewSelection() {
			if(_dataSetMain==null || _dataSetMain.Tables["account"]==null) {
				return;
			}
			DataTable table=_dataSetMain.Tables["account"];
			gridAccount.SetAll(false);
			for(int i=0;i<gridServiceDateView.SelectedIndices.Length;i++) {
				ServiceDateViewRow row=gridServiceDateView.ListGridRows[gridServiceDateView.SelectedIndices[i]].Tag as ServiceDateViewRow;
				int index=FindAccountRowIndex(table,row);
				if(index>=0 && index<gridAccount.ListGridRows.Count) {
					gridAccount.SetSelected(index,true);
				}
			}
		}

		///<summary>Opens the object of a service-date row that has no row in the regular grid (for example a credit dated after the date range).</summary>
		private void OpenServiceDateViewObject(ServiceDateViewRow row) {
			if(row.IsProcedure) {
				Procedure procedure=Procedures.GetOneProc(row.ProcNum,true);
				if(procedure==null || procedure.ProcNum==0) {
					MsgBox.Show(this,"The procedure has been deleted.");
					return;
				}
				Patient patient=_family.GetPatient(procedure.PatNum)??_patient;
				using FormProcEdit formProcEdit=new FormProcEdit(procedure,patient,_family);
				formProcEdit.ListClaimProcHists=_loadData.HistList;
				formProcEdit.ListClaimProcHistsLoop=new List<ClaimProcHist>();
				formProcEdit.ShowDialog();
			}
			else if(ServiceDateViews.IsAdjustmentType(row.TranType)) {
				Adjustment adjustment=Adjustments.GetOne(row.GetPriKey());
				if(adjustment==null) {
					MsgBox.Show(this,"The adjustment has been deleted.");
					return;
				}
				using FormAdjust formAdjust=new FormAdjust(_patient,adjustment);
				formAdjust.ShowDialog();
			}
			else if(ServiceDateViews.IsPatPayType(row.TranType)) {
				Payment payment=Payments.GetPayment(row.ParentNum);
				if(payment==null) {
					MsgBox.Show(this,"The payment has been deleted.");
					return;
				}
				using FormPayment formPayment=new FormPayment(_patient,_family,payment,false);
				formPayment.IsNew=false;
				formPayment.ShowDialog();
			}
			else if(ServiceDateViews.IsInsuranceType(row.TranType)) {
				if(!Security.IsAuthorized(EnumPermType.ClaimView)) {
					return;
				}
				Claim claim=Claims.GetClaim(row.ParentNum);
				if(claim==null) {
					MsgBox.Show(this,"The claim has been deleted.");
					return;
				}
				using FormClaimEdit formClaimEdit=new FormClaimEdit(claim,_family.GetPatient(claim.PatNum)??_patient,_family);
				formClaimEdit.IsNew=false;
				formClaimEdit.ShowDialog();
			}
			else if(ServiceDateViews.IsPayPlanType(row.TranType)) {
				PayPlan payPlan=PayPlans.GetOne(row.ParentNum);
				if(payPlan==null) {
					MsgBox.Show(this,"This pay plan has been deleted by another user.");
					return;
				}
				if(payPlan.IsDynamic) {
					using FormPayPlanDynamic formPayPlanDynamic=new FormPayPlanDynamic(payPlan);
					formPayPlanDynamic.ShowDialog();
				}
				else {
					using FormPayPlan formPayPlan=new FormPayPlan(payPlan);
					formPayPlan.ShowDialog();
				}
			}
			else {
				return;
			}
			ModuleSelected(_patient.PatNum,IsFamilySelected());
		}
		#endregion Methods - Service Date View

		#region Event Handlers - Service Date View
		private void gridServiceDateView_CellClick(object sender,ODGridClickEventArgs e) {
			MirrorServiceDateViewSelection();
		}

		private void gridServiceDateView_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			if(e.Row<0 || e.Row>=gridServiceDateView.ListGridRows.Count || _patient==null) {
				return;
			}
			ServiceDateViewRow row=gridServiceDateView.ListGridRows[e.Row].Tag as ServiceDateViewRow;
			if(row==null || row.RowKind!=ServiceDateViewRowKind.Transaction) {
				return;
			}
			_scrollValueServiceDateView=gridServiceDateView.ScrollValue;
			int index=FindAccountRowIndex(_dataSetMain?.Tables["account"],row);
			if(index>=0) {
				//Same behavior as double-clicking the row in the regular view.
				gridAccount_CellDoubleClick(gridAccount,new ODGridClickEventArgs(0,index,MouseButtons.Left));
				return;
			}
			OpenServiceDateViewObject(row);
		}

		private void checkServiceDateViewDetails_Click(object sender,EventArgs e) {
			if(Security.CurUser!=null) {
				ServiceDateViewUserPrefs.SetBool(Security.CurUser.UserNum,ServiceDateViewUserPrefs.FKEY_SHOW_DETAILS,checkServiceDateViewDetails.Checked);
				DataValid.SetInvalid(InvalidType.UserOdPrefs);
			}
			FillServiceDateView();
		}

		private void contextMenuAcctGridServiceDateView_Popup(object sender,EventArgs e) {
			if(_menuItemServiceDateViewToggle!=null) {
				_menuItemServiceDateViewToggle.Checked=_isServiceDateView;
			}
			if(_isServiceDateView) {
				MirrorServiceDateViewSelection();//A right-click selects the row under the mouse.
			}
		}

		private void menuItemServiceDateViewToggle_Click(object sender,EventArgs e) {
			if(_patient==null) {
				return;
			}
			ToggleServiceDateView();
		}

		private void menuItemServiceDateViewWindow_Click(object sender,EventArgs e) {
			ShowServiceDateViewWindow();
		}
		#endregion Event Handlers - Service Date View
	}
}
