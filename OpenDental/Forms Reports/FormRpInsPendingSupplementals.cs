using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenDental.ReportingComplex;
using OpenDental.UI;
using OpenDentBusiness;
using PendingSupplementalFilter=OpenDentBusiness.RpInsPendingSupplementals.PendingSupplementalFilter;

namespace OpenDental {
	///<summary>Insurance Pending Supplementals report (Standard Reports > Lists), non-modal. Lists every outstanding pending supplemental that Edit Claim
	///recorded for an insurance overpayment (a refund the office owes the carrier) or underpayment (more the carrier should pay). Double-click opens
	///the claim; right-click goes to the patient's account or copies cells and rows.</summary>
	public partial class FormRpInsPendingSupplementals:FormODBase {
		///<summary>The last table shown. Null until the first refresh.</summary>
		private DataTable _table;

		///<summary></summary>
		public FormRpInsPendingSupplementals() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormRpInsPendingSupplementals_Load(object sender,EventArgs e) {
			gridMain.ContextMenu=contextMenuGrid;
			comboType.Items.Add(Lan.g(this,"All"),PendingSupplementalFilter.All);
			comboType.Items.Add(Lan.g(this,"Overpaid"),PendingSupplementalFilter.Overpaid);
			comboType.Items.Add(Lan.g(this,"Underpaid"),PendingSupplementalFilter.Underpaid);
			comboType.SetSelected(0);
			//Blank dates mean every date of service.
			dateRangePicker.SetDateTimeFrom(DateTime.MinValue);
			dateRangePicker.SetDateTimeTo(DateTime.MinValue);
			FillGrid();
		}

		///<summary>The type a row shows, from the sign of its amount.</summary>
		private string GetTypeText(double amount) {
			switch(RpInsPendingSupplementals.GetKind(amount)) {
				case PendingSupplementalFilter.Overpaid:
					return Lan.g(this,"Overpaid");
				case PendingSupplementalFilter.Underpaid:
					return Lan.g(this,"Underpaid");
				default:
					return "";
			}
		}

		private void FillGrid() {
			if(!dateRangePicker.IsValid()) {
				MsgBox.Show(this,"Please fix the dates first.");
				return;
			}
			DateTime dateFrom=dateRangePicker.GetDateTimeFrom().Date;
			DateTime dateTo=dateRangePicker.GetDateTimeTo().Date;
			if(dateFrom.Year>1880 && dateTo.Year>1880 && dateTo<dateFrom) {
				MsgBox.Show(this,"End date cannot be before start date.");
				return;
			}
			List<long> listClinicNums=new List<long>();
			if(PrefC.HasClinicsEnabled) {
				//Only the clinics this user may see. "All" lists them all, plus Unassigned when the user is not restricted.
				listClinicNums=comboClinics.ListClinicNumsSelected;
				if(listClinicNums.Count==0) {
					MsgBox.Show(this,"At least one clinic must be selected.");
					return;
				}
			}
			PendingSupplementalFilter filter=comboType.GetSelected<PendingSupplementalFilter>();
			Cursor=Cursors.WaitCursor;
			try {
				_table=RpInsPendingSupplementals.GetTable(listClinicNums,filter,dateFrom,dateTo);
			}
			catch(Exception ex) {
				Cursor=Cursors.Default;
				string text=Lan.g(this,"Error getting report data:")+" "+ex.Message+"\r\n\r\n"+ex.StackTrace;
				using MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(text);
				msgBoxCopyPaste.ShowDialog();
				return;
			}
			Cursor=Cursors.Default;
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Date of Service"),95,HorizontalAlignment.Center,GridSortingStrategy.DateParse));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Patient"),170,GridSortingStrategy.StringCompare));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Carrier"),200,GridSortingStrategy.StringCompare));
			if(PrefC.HasClinicsEnabled) {
				gridMain.Columns.Add(new GridColumn(Lan.g(this,"Clinic"),100,GridSortingStrategy.StringCompare));
			}
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Type"),80,GridSortingStrategy.StringCompare));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Amount"),90,HorizontalAlignment.Right,GridSortingStrategy.AmountParse));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Claim Date Sent"),100,HorizontalAlignment.Center,GridSortingStrategy.DateParse));
			gridMain.ListGridRows.Clear();
			double total=0;
			for(int i=0;i<_table.Rows.Count;i++) {
				DataRow dataRow=_table.Rows[i];
				double amount=PIn.Double(dataRow["Amount"].ToString());
				total+=amount;
				GridRow gridRow=new GridRow();
				gridRow.Cells.Add(PIn.Date(dataRow["ProcDate"].ToString()).ToShortDateString());
				gridRow.Cells.Add(dataRow["PatName"].ToString());
				gridRow.Cells.Add(dataRow["CarrierName"].ToString());
				if(PrefC.HasClinicsEnabled) {
					gridRow.Cells.Add(Clinics.GetAbbr(PIn.Long(dataRow["ClinicNum"].ToString())));
				}
				gridRow.Cells.Add(GetTypeText(amount));
				gridRow.Cells.Add(amount.ToString("c"));
				DateTime dateSent=PIn.Date(dataRow["DateSent"].ToString());
				gridRow.Cells.Add(dateSent.Year>1880 ? dateSent.ToShortDateString() : "");
				gridRow.Tag=dataRow;
				gridMain.ListGridRows.Add(gridRow);
			}
			gridMain.EndUpdate();
			labelTotal.Text=Lan.g(this,"Total")+": "+total.ToString("c")+"   "+Lan.g(this,"Rows")+": "+_table.Rows.Count;
		}

		private void butRefresh_Click(object sender,EventArgs e) {
			FillGrid();
		}

		private void gridMain_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			if(e.Row<0 || e.Row>=gridMain.ListGridRows.Count) {
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.ClaimView)) {
				return;
			}
			DataRow dataRow=(DataRow)gridMain.ListGridRows[e.Row].Tag;
			Claim claim=Claims.GetClaim(PIn.Long(dataRow["ClaimNum"].ToString()));
			if(claim==null) {
				MsgBox.Show(this,"The claim has been deleted.");
				FillGrid();
				return;
			}
			Patient patient=Patients.GetPat(claim.PatNum);
			Family family=Patients.GetFamily(patient.PatNum);
			using FormClaimEdit formClaimEdit=new FormClaimEdit(claim,patient,family);
			formClaimEdit.IsNew=false;
			formClaimEdit.ShowDialog();
			FillGrid();//The amount may have changed, or the pending supplemental may have become a supplemental payment.
		}

		private void menuItemGoToAccount_Click(object sender,EventArgs e) {
			if(gridMain.SelectedIndices.Length!=1) {
				MsgBox.Show(this,"Please select exactly one row.");
				return;
			}
			DataRow dataRow=(DataRow)gridMain.ListGridRows[gridMain.GetSelectedIndex()].Tag;
			long patNum=PIn.Long(dataRow["PatNum"].ToString());
			GlobalFormOpenDental.GoToModule(EnumModuleType.Account,patNum:patNum);
			SendToBack();
		}

		private void butPrint_Click(object sender,EventArgs e) {
			if(_table==null) {
				return;
			}
			DataTable tablePrint=new DataTable();
			tablePrint.Columns.Add("ProcDate",typeof(DateTime));
			tablePrint.Columns.Add("PatName");
			tablePrint.Columns.Add("CarrierName");
			if(PrefC.HasClinicsEnabled) {
				tablePrint.Columns.Add("Clinic");
			}
			tablePrint.Columns.Add("Type");
			tablePrint.Columns.Add("Amount",typeof(double));
			tablePrint.Columns.Add("DateSent");
			foreach(DataRow dataRow in _table.Rows) {
				double amount=PIn.Double(dataRow["Amount"].ToString());
				DateTime dateSent=PIn.Date(dataRow["DateSent"].ToString());
				List<object> listValues=new List<object> { PIn.Date(dataRow["ProcDate"].ToString()),dataRow["PatName"].ToString(),dataRow["CarrierName"].ToString() };
				if(PrefC.HasClinicsEnabled) {
					listValues.Add(Clinics.GetAbbr(PIn.Long(dataRow["ClinicNum"].ToString())));
				}
				listValues.Add(GetTypeText(amount));
				listValues.Add(amount);
				listValues.Add(dateSent.Year>1880 ? dateSent.ToShortDateString() : "");
				tablePrint.Rows.Add(listValues.ToArray());
			}
			ReportComplex report=new ReportComplex(true,false);
			Font font=new Font("Tahoma",9);
			Font fontTitle=new Font("Tahoma",17,FontStyle.Bold);
			Font fontSubTitle=new Font("Tahoma",10,FontStyle.Bold);
			report.ReportName=Lan.g(this,"Insurance Pending Supplementals");
			report.AddTitle("Title",Lan.g(this,"Insurance Pending Supplementals"),fontTitle);
			report.AddSubTitle("Practice Title",PrefC.GetString(PrefName.PracticeTitle),fontSubTitle);
			report.AddSubTitle("Type",comboType.GetStringSelectedItems(),fontSubTitle);
			if(PrefC.HasClinicsEnabled) {
				report.AddSubTitle("Clinics",comboClinics.GetStringSelectedClinics(),fontSubTitle);
			}
			QueryObject query=report.AddQuery(tablePrint,Lan.g(this,"Date")+": "+DateTime.Today.ToString("d"));
			query.AddColumn(Lan.g(this,"Date of Service"),95,FieldValueType.Date,font);
			query.GetColumnDetail(Lan.g(this,"Date of Service")).StringFormat="d";
			query.AddColumn(Lan.g(this,"Patient"),160,FieldValueType.String,font);
			query.AddColumn(Lan.g(this,"Carrier"),180,FieldValueType.String,font);
			if(PrefC.HasClinicsEnabled) {
				query.AddColumn(Lan.g(this,"Clinic"),90,FieldValueType.String,font);
			}
			query.AddColumn(Lan.g(this,"Type"),75,FieldValueType.String,font);
			query.AddColumn(Lan.g(this,"Amount"),85,FieldValueType.Number,font);
			query.AddColumn(Lan.g(this,"Claim Date Sent"),95,FieldValueType.String,font);
			report.AddPageNum(font);
			if(!report.SubmitQueries()) {
				return;
			}
			using FormReportComplex formReportComplex=new FormReportComplex(report);
			formReportComplex.ShowDialog();
		}

		private void butExport_Click(object sender,EventArgs e) {
			gridMain.Export("Insurance Pending Supplementals");
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}
	}
}
