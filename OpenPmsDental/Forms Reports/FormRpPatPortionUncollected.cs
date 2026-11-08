using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental.ReportingComplex;
using OpenPmsDentalBusiness;

namespace OpenPmsDental{
///<summary></summary>
	public partial class FormRpPatPortionUncollected : FormODBase{
		private DataTable _table;// used to fill grid and print
		private DateTime _fromDate;
		private DateTime _toDate;
		private List<Clinic> _listClinics;
		private List<long> _listClinicNums;
		private bool _isAllSelected;
		///<summary>The "Show prior uncollected balances" option as of the last refresh (Open Dental 26.2 item 33).</summary>
		private bool _doShowPrior;
		///<summary>The background of a prior procedure's row.</summary>
		private static readonly Color _colorPrior=Color.FromArgb(235,235,235);

		///<summary></summary>
		public FormRpPatPortionUncollected(){
			InitializeComponent();
			InitializeLayoutManager();
 			Lan.F(this);
		}

		private void FormPaymentSheet_Load(object sender,System.EventArgs e) {
			odDateRangePicker.SetDateTimeFrom(DateTime.Today);
			odDateRangePicker.SetDateTimeTo(DateTime.Today);
			FillGrid();
		}

		private void FillGrid() {
			_fromDate = odDateRangePicker.GetDateTimeFrom();
			_toDate = odDateRangePicker.GetDateTimeTo();
			_listClinicNums = comboBoxClinicPicker.ListClinicNumsSelected;
			_listClinics = Clinics.GetClinics(comboBoxClinicPicker.ListClinicNumsSelected);
			_isAllSelected = comboBoxClinicPicker.IsAllSelected;
			_doShowPrior=checkShowPrior.Checked;
			gridOD.BeginUpdate();
			gridOD.ListGridRows.Clear();
			gridOD.Columns.Clear();
			gridOD.Title="Patient Portion Uncollected";
			gridOD.Columns.Add(new UI.GridColumn(Lan.g(this,"Date"),90,UI.GridSortingStrategy.DateParse));
			gridOD.Columns.Add(new UI.GridColumn(Lan.g(this,"Patient Name"),150,UI.GridSortingStrategy.StringCompare));
			gridOD.Columns.Add(new UI.GridColumn(Lan.g(this,"Procedure"),80,UI.GridSortingStrategy.StringCompare));
			gridOD.Columns.Add(new UI.GridColumn(Lan.g(this,"Fee"),60,HorizontalAlignment.Right,UI.GridSortingStrategy.AmountParse));
			gridOD.Columns.Add(new UI.GridColumn(Lan.g(this,"Patient"),65,HorizontalAlignment.Right,UI.GridSortingStrategy.StringCompare));
			gridOD.Columns.Add(new UI.GridColumn(Lan.g(this,"Adjustment"),75,HorizontalAlignment.Right,UI.GridSortingStrategy.AmountParse));
			gridOD.Columns.Add(new UI.GridColumn(Lan.g(this,"Patient Paid"),90,HorizontalAlignment.Right,UI.GridSortingStrategy.AmountParse));
			gridOD.Columns.Add(new UI.GridColumn(Lan.g(this,"Uncollected"),80,HorizontalAlignment.Right,UI.GridSortingStrategy.AmountParse));
			//26.2 item 34: what of the procedure is on a payment plan. It does not change the Uncollected column.
			gridOD.Columns.Add(new UI.GridColumn(Lan.g(this,"Pay Plan"),75,HorizontalAlignment.Right,UI.GridSortingStrategy.AmountParse));
			if(odDateRangePicker.IsValid()) {
				_table = RpPatPortionUncollected.GetPatUncollected(_fromDate, _toDate, _listClinicNums, _doShowPrior);
			}
			UI.GridRow row;
			if(_table!=null) {
				foreach(DataRow rowCur in _table.Rows) {
					row=new UI.GridRow() { Tag=rowCur };
					row.Cells.Add(PIn.Date(rowCur["ProcDate"].ToString()).ToShortDateString());
					row.Cells.Add(PIn.String(rowCur["Patient"].ToString()).ToString());
					row.Cells.Add(PIn.String(rowCur["AbbrDesc"].ToString()).ToString());
					row.Cells.Add(PIn.Double(rowCur["Fee"].ToString()).ToString("c"));
					row.Cells.Add(PIn.Double(rowCur["PatPortion"].ToString()).ToString("c"));
					row.Cells.Add(PIn.Double(rowCur["Adjustment"].ToString()).ToString("c"));
					row.Cells.Add(PIn.Double(rowCur["Payment"].ToString()).ToString("c"));
					row.Cells.Add(PIn.Double(rowCur["Uncollected"].ToString()).ToString("c"));
					row.Cells.Add(PIn.Double(rowCur[RpPatPortionUncollected.ColumnPayPlan].ToString()).ToString("c"));
					if(IsPriorRow(rowCur)) {
						row.ColorBackG=_colorPrior;
					}
					gridOD.ListGridRows.Add(row);
				}
			}
			gridOD.EndUpdate();
			labelPrior.Visible=_doShowPrior;
		}

		private static bool IsPriorRow(DataRow row) {
			return PIn.Int(row[RpPatPortionUncollected.ColumnIsPrior].ToString())==1;
		}

		///<summary>The rows of the date range (isPrior false) or the prior rows, in the printed columns' order.</summary>
		private DataTable GetPrintTable(bool isPrior) {
			DataTable table=new DataTable();
			foreach(string columnName in new[] { "PatNum","ProcDate","Patient","AbbrDesc","Fee","PatPortion","Adjustment","Payment","Uncollected",
				RpPatPortionUncollected.ColumnPayPlan })
			{
				table.Columns.Add(columnName,_table.Columns[columnName].DataType);
			}
			foreach(DataRow rowCur in _table.Rows) {
				if(IsPriorRow(rowCur)!=isPrior) {
					continue;
				}
				DataRow row=table.NewRow();
				foreach(DataColumn column in table.Columns) {
					row[column.ColumnName]=rowCur[column.ColumnName];
				}
				table.Rows.Add(row);
			}
			return table;
		}

		private void AddPrintQuery(ReportComplex report,DataTable table,string title,Font font) {
			QueryObject query=report.AddQuery(table,title);
			query.AddColumn("PatNum",55,FieldValueType.Integer,font);
			query.AddColumn("Date",75,FieldValueType.Date,font);
			query.GetColumnDetail("Date").StringFormat="d";
			query.AddColumn("Patient Name",140,FieldValueType.String,font);
			query.AddColumn("Procedure",80,FieldValueType.String,font);
			query.AddColumn("Fee",70,FieldValueType.Number,font);
			query.AddColumn("Patient",70,FieldValueType.Number,font);
			query.AddColumn("Adjustment",70,FieldValueType.Number,font);
			query.AddColumn("Patient Paid",75,FieldValueType.Number,font);
			query.AddColumn("Uncollected",75,FieldValueType.Number,font);
			query.AddColumn("Pay Plan",70,FieldValueType.Number,font);
		}

		private void PrintReport() {
			if(_table == null || _table.Rows.Count==0) {
				MsgBox.Show(this,Lan.g(this,"No rows to print."));
				return;
			}
			if(PrefC.HasClinicsEnabled && _listClinicNums.Count==0) {
				MsgBox.Show(this,Lan.g(this,"At least one clinic must be selected."));
				return;
			}
			ReportComplex report=new ReportComplex(true,false);
			string subtitleClinics="";
			if(PrefC.HasClinicsEnabled) {
				if(_isAllSelected) {//all clinics selected.
					subtitleClinics=Lan.g(this,"All Clinics (includes hidden)");
				}
				else {
					if(_listClinicNums.Contains(0)){
						subtitleClinics+=Lan.g(this,"Unassigned");
						subtitleClinics+=_listClinics.Count>0 ? ", " : "";
					}
					for(int i = 0;i<_listClinics.Count;i++) {
						if(i>0) {
							subtitleClinics+=", ";
						}
							subtitleClinics+=_listClinics[i].Abbr;
						}
					}
				}
			Font font=new Font("Tahoma",9);
			Font fontBold=new Font("Tahoma",9,FontStyle.Bold);
			Font fontTitle=new Font("Tahoma",17,FontStyle.Bold);
			Font fontSubTitle=new Font("Tahoma",10,FontStyle.Bold);
			report.ReportName=Lan.g(this,"Patient Portion Uncollected");
			report.AddTitle("Title",Lan.g(this,"Patient Portion Uncollected"),fontTitle);
			report.AddSubTitle("Practice Title",PrefC.GetString(PrefName.PracticeTitle),fontSubTitle);
			report.AddSubTitle("Dates of Report",_fromDate.ToString("d")+" - "+_toDate.ToString("d"),fontSubTitle);
			if(PrefC.HasClinicsEnabled) {
				report.AddSubTitle("Clinics",subtitleClinics,fontSubTitle);
			}
			if(_doShowPrior) {
				report.AddSubTitle("Prior Balances",Lan.g(this,"Includes prior uncollected balances"),fontSubTitle);
			}
			//The procedures of the date range, then (with the option) the prior ones, each with its own totals.
			DataTable tableRange=GetPrintTable(isPrior:false);
			DataTable tablePrior=GetPrintTable(isPrior:true);
			if(tableRange.Rows.Count>0) {
				AddPrintQuery(report,tableRange,Lan.g(this,"Patient Portion Uncollected"),font);
			}
			if(tablePrior.Rows.Count>0) {
				AddPrintQuery(report,tablePrior,Lan.g(this,"Prior Uncollected Balances"),font);
			}
			report.AddPageNum(font);
			report.AddGridLines();
			if(!report.SubmitQueries()) {
				return;
			}
			using FormReportComplex FormR=new FormReportComplex(report);
			FormR.ShowDialog();
			DialogResult=DialogResult.OK;
		}

		private void butRefresh_Click(object sender,EventArgs e) {
			FillGrid();
		}

		private void butPrint_Click(object sender,EventArgs e) {
			PrintReport();
		}

		private void menuItemAccount_Click(object sender, EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.AccountModule)) {
				return;
			}
			if(gridOD.SelectedIndices.Length==0) {
				MsgBox.Show(this,"Please select a patient first.");
				return;
			}
			long patNum=PIn.Long(gridOD.SelectedTag<DataRow>()["PatNum"].ToString());
			GlobalFormOpenDental.GoToModule(EnumModuleType.Account,patNum:patNum);
		}

	}
}
