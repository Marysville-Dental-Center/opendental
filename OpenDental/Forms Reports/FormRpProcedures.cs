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

namespace OpenDental {
	///<summary>The itemized Procedures report (Standard Reports > Daily), non-modal. Open Dental 24.4 split the Daily Procedures report: this window
	///lists each completed procedure, and Production by Procedure (FormRpProcSheet) groups them by code. It opens empty; the user sets the filters and
	///clicks Refresh, and can change the filters and refresh again without closing. Right-click or double-click a row to go to the patient's chart.
	///</summary>
	public partial class FormRpProcedures:FormODBase {
		///<summary>The last table shown, with the PatNum, ProcNum and ClinicNum columns. Null until the first refresh.</summary>
		private DataTable _table;
		private bool _isAnyClinicMedical;
		private DateTime _dateFrom;
		private DateTime _dateTo;
		private List<Provider> _listProviders;

		///<summary></summary>
		public FormRpProcedures() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormRpProcedures_Load(object sender,EventArgs e) {
			gridMain.ContextMenu=contextMenuGrid;
			dateRangePicker.SetDateTimeFrom(DateTime.Today);
			dateRangePicker.SetDateTimeTo(DateTime.Today);
			FillProvs();
			FillGridColumns();
			labelTotal.Text="";
		}

		///<summary>Users without "Daily Reports - View All Providers" see only their own provider. FormReportsMore already blocks users who are not a
		///provider and lack that permission.</summary>
		private void FillProvs() {
			_listProviders=Providers.GetListReports();
			comboProvs.Items.Clear();
			if(Security.IsAuthorized(EnumPermType.ReportDailyAllProviders,true)) {
				comboProvs.IncludeAll=true;
				comboProvs.Items.AddProvsFull(_listProviders);
				comboProvs.IsAllSelected=true;
				return;
			}
			_listProviders=_listProviders.FindAll(x => x.ProvNum==Security.CurUser.ProvNum);
			comboProvs.IncludeAll=false;
			comboProvs.Items.AddProvsFull(_listProviders);
			if(_listProviders.Count>0) {
				comboProvs.SetSelected(0);
			}
			comboProvs.Enabled=false;
		}

		private bool AnyClinicSelectedIsMedical(List<long> listClinicNums) {
			if(!PrefC.HasClinicsEnabled) {
				return Clinics.IsMedicalPracticeOrClinic(0);
			}
			return listClinicNums.Any(x => Clinics.IsMedicalPracticeOrClinic(x));
		}

		private void butRefresh_Click(object sender,EventArgs e) {
			if(!dateRangePicker.IsValid()) {
				MsgBox.Show(this,"Please fix the dates first.");
				return;
			}
			DateTime dateFrom=dateRangePicker.GetDateTimeFrom().Date;
			DateTime dateTo=dateRangePicker.GetDateTimeTo().Date;
			if(dateFrom.Year<1880 || dateTo.Year<1880) {
				MsgBox.Show(this,"Please enter a From and a To date.");
				return;
			}
			if(dateTo<dateFrom) {
				MsgBox.Show(this,"End date cannot be before start date.");
				return;
			}
			bool hasAllProvs=comboProvs.IncludeAll && comboProvs.IsAllSelected;
			List<long> listProvNums=comboProvs.GetSelectedProvNums();
			if(!hasAllProvs && listProvNums.Count==0) {
				MsgBox.Show(this,"At least one provider must be selected.");
				return;
			}
			List<long> listClinicNums=new List<long>();
			if(PrefC.HasClinicsEnabled) {
				listClinicNums=comboClinics.ListClinicNumsSelected;
				if(listClinicNums.Count==0) {
					MsgBox.Show(this,"At least one clinic must be selected.");
					return;
				}
			}
			_isAnyClinicMedical=AnyClinicSelectedIsMedical(listClinicNums);
			Cursor=Cursors.WaitCursor;
			try {
				_table=RpProcSheet.GetProceduresTable(dateFrom,dateTo,listProvNums,listClinicNums,textCode.Text,_isAnyClinicMedical,hasAllProvs,
					PrefC.HasClinicsEnabled);
			}
			catch(Exception ex) {
				Cursor=Cursors.Default;
				string text=Lan.g(this,"Error getting report data:")+" "+ex.Message+"\r\n\r\n"+ex.StackTrace;
				using MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(text);
				msgBoxCopyPaste.ShowDialog();
				return;
			}
			Cursor=Cursors.Default;
			_dateFrom=dateFrom;
			_dateTo=dateTo;
			FillGrid();
		}

		private void FillGridColumns() {
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Date"),80,HorizontalAlignment.Center,GridSortingStrategy.DateParse));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Patient"),160,GridSortingStrategy.StringCompare));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Code"),_isAnyClinicMedical ? 140 : 70,GridSortingStrategy.StringCompare));
			if(!_isAnyClinicMedical) {
				gridMain.Columns.Add(new GridColumn(Lan.g(this,"Tooth"),45,GridSortingStrategy.ToothNumberParse));
			}
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Area"),55,GridSortingStrategy.StringCompare));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Description"),200,GridSortingStrategy.StringCompare));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Prov"),70,GridSortingStrategy.StringCompare));
			if(PrefC.HasClinicsEnabled) {
				gridMain.Columns.Add(new GridColumn(Lan.g(this,"Clinic"),100,GridSortingStrategy.StringCompare));
			}
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Fee"),80,HorizontalAlignment.Right,GridSortingStrategy.AmountParse));
		}

		private void FillGrid() {
			gridMain.BeginUpdate();
			FillGridColumns();
			gridMain.ListGridRows.Clear();
			double total=0;
			for(int i=0;i<_table.Rows.Count;i++) {
				DataRow dataRow=_table.Rows[i];
				GridRow gridRow=new GridRow();
				gridRow.Cells.Add(PIn.Date(dataRow["ProcDate"].ToString()).ToShortDateString());
				gridRow.Cells.Add(dataRow["plfname"].ToString());
				gridRow.Cells.Add(dataRow["ProcCode"].ToString());
				if(!_isAnyClinicMedical) {
					gridRow.Cells.Add(Tooth.Display(dataRow["ToothNum"].ToString()));
				}
				gridRow.Cells.Add(dataRow["Area"].ToString());
				gridRow.Cells.Add(dataRow["Descript"].ToString());
				gridRow.Cells.Add(dataRow["Abbr"].ToString());
				if(PrefC.HasClinicsEnabled) {
					gridRow.Cells.Add(dataRow["Clinic"].ToString());
				}
				double fee=PIn.Double(dataRow["$fee"].ToString());
				total+=fee;
				gridRow.Cells.Add(fee.ToString("c"));
				gridRow.Tag=dataRow;
				gridMain.ListGridRows.Add(gridRow);
			}
			gridMain.EndUpdate();
			labelTotal.Text=Lan.g(this,"Total")+": "+total.ToString("c")+"   "+Lan.g(this,"Procedures")+": "+_table.Rows.Count;
		}

		private void gridMain_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			if(e.Row<0 || e.Row>=gridMain.ListGridRows.Count) {
				return;
			}
			GoToPatient((DataRow)gridMain.ListGridRows[e.Row].Tag);
		}

		private void menuItemGoToPatient_Click(object sender,EventArgs e) {
			if(gridMain.SelectedIndices.Length!=1) {
				MsgBox.Show(this,"Please select exactly one procedure.");
				return;
			}
			GoToPatient((DataRow)gridMain.ListGridRows[gridMain.GetSelectedIndex()].Tag);
		}

		///<summary>Selects the patient in the main window and opens the Chart module. The report stays open. An archived patient needs the Archived
		///Patient Select permission, as everywhere else; without it the usual message shows and nothing changes.</summary>
		private void GoToPatient(DataRow dataRow) {
			if(dataRow==null) {
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.ChartModule)) {
				return;
			}
			long patNum=PIn.Long(dataRow["PatNum"].ToString());
			Patient patient=Patients.GetPat(patNum);
			if(patient==null) {
				MsgBox.Show(this,"The patient no longer exists.");
				return;
			}
			if(patient.PatStatus==PatientStatus.Archived && !Security.IsAuthorized(EnumPermType.ArchivedPatientSelect)) {
				return;
			}
			GlobalFormOpenDental.PatientSelected(patient,false);
			GlobalFormOpenDental.GoToModule(EnumModuleType.Chart,patNum:patient.PatNum);
			SendToBack();//Stays open behind the main window, as the Procedures Overpaid report does.
		}

		private void butPrint_Click(object sender,EventArgs e) {
			if(_table==null) {
				MsgBox.Show(this,"Click Refresh first.");
				return;
			}
			//The report table holds only the printed columns, in the printed order.
			DataTable tablePrint=_table.Copy();
			foreach(string columnName in new[] { "PatNum","ProcNum","ClinicNum" }) {
				if(tablePrint.Columns.Contains(columnName)) {
					tablePrint.Columns.Remove(columnName);
				}
			}
			if(tablePrint.Columns.Contains("ToothNum")) {
				foreach(DataRow dataRow in tablePrint.Rows) {
					dataRow["ToothNum"]=Tooth.Display(dataRow["ToothNum"].ToString());
				}
			}
			ReportComplex report=new ReportComplex(true,false);
			Font font=new Font("Tahoma",9);
			Font fontTitle=new Font("Tahoma",17,FontStyle.Bold);
			Font fontSubTitle=new Font("Tahoma",10,FontStyle.Bold);
			report.ReportName=Lan.g(this,"Procedures");
			report.AddTitle("Title",Lan.g(this,"Procedures"),fontTitle);
			report.AddSubTitle("Practice Title",PrefC.GetString(PrefName.PracticeTitle),fontSubTitle);
			report.AddSubTitle("Dates of Report",_dateFrom.ToString("d")+" - "+_dateTo.ToString("d"),fontSubTitle);
			string subtitleProvs=Lan.g(this,"All Providers");
			if(!(comboProvs.IncludeAll && comboProvs.IsAllSelected)) {
				subtitleProvs=string.Join(", ",comboProvs.GetSelectedProvNums().Select(x => Providers.GetAbbr(x)));
			}
			report.AddSubTitle("Providers",subtitleProvs,fontSubTitle);
			if(PrefC.HasClinicsEnabled) {
				report.AddSubTitle("Clinics",comboClinics.GetStringSelectedClinics(),fontSubTitle);
			}
			QueryObject query=report.AddQuery(tablePrint,Lan.g(this,"Date")+": "+DateTime.Today.ToString("d"));
			query.AddColumn(Lan.g(this,"Date"),85,FieldValueType.Date,font);
			query.GetColumnDetail(Lan.g(this,"Date")).StringFormat="d";
			query.AddColumn(Lan.g(this,"Patient Name"),145,FieldValueType.String,font);
			if(_isAnyClinicMedical) {
				query.AddColumn(Lan.g(this,"Code"),140,FieldValueType.String,font);
			}
			else {
				query.AddColumn(Lan.g(this,"Code"),70,FieldValueType.String,font);
				query.AddColumn(Lan.g(this,"Tooth"),40,FieldValueType.String,font);
			}
			query.AddColumn(Lan.g(this,"Area"),60,FieldValueType.String,font);
			query.AddColumn(Lan.g(this,"Description"),140,FieldValueType.String,font);
			query.AddColumn(Lan.g(this,"Prov"),60,FieldValueType.String,font);
			if(PrefC.HasClinicsEnabled) {
				query.AddColumn(Lan.g(this,"Clinic"),100,FieldValueType.String,font);
			}
			query.AddColumn(Lan.g(this,"Fee"),65,FieldValueType.Number,font);
			report.AddPageNum(font);
			if(!report.SubmitQueries()) {
				return;
			}
			using FormReportComplex formReportComplex=new FormReportComplex(report);
			formReportComplex.ShowDialog();
		}

		private void butExport_Click(object sender,EventArgs e) {
			if(_table==null) {
				MsgBox.Show(this,"Click Refresh first.");
				return;
			}
			gridMain.Export("Procedures");
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}
	}
}
