// UDS+ report for FQHC dental services (Open Dental 25.4, package M2-UDS in docs/specs/port-backlog.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.ReportingComplex;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>UDS+ report (Standard Reports > Public Health). Counts visits and patients per UDS dental service line for a date range, and saves the
	///patient-level file for the center's own UDS+ tooling. Which codes count on which line is set up in FormRpUdsPlusSetup. Each run and each saved
	///file is logged under the Reports permission.</summary>
	public partial class FormRpUdsPlus:FormODBase {
		private List<Provider> _listProviders;

		public FormRpUdsPlus() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormRpUdsPlus_Load(object sender,EventArgs e) {
			//UDS reports cover a calendar year, filed early the next year: default to the last full year.
			int year=DateTime.Today.Year-1;
			dateRangePicker.SetDateTimeFrom(new DateTime(year,1,1));
			dateRangePicker.SetDateTimeTo(new DateTime(year,12,31));
			_listProviders=Providers.GetListReports();
			listProv.Items.AddList(_listProviders,x => x.GetLongDesc());
			FillLineSummary();
		}

		///<summary>Reads the setup. Shows the reason and returns null when the setting cannot be read.</summary>
		private UdsPlusLineMap GetLineMap(bool isSilent) {
			if(!UdsPlusLineMap.TryParse(PrefC.GetString(PrefName.UdsPlusDentalLineCodes),out UdsPlusLineMap udsPlusLineMap,out string error)) {
				if(!isSilent) {
					MsgBox.Show(error+"\r\n"+Lan.g(this,"Open Setup to correct the lines."));
				}
				return null;
			}
			return udsPlusLineMap;
		}

		private void FillLineSummary() {
			UdsPlusLineMap udsPlusLineMap=GetLineMap(true);
			if(udsPlusLineMap==null) {
				labelLines.Text=Lan.g(this,"The UDS line setup cannot be read. Open Setup to correct it.");
				return;
			}
			if(udsPlusLineMap.ListLines.Count==0) {
				labelLines.Text=Lan.g(this,"No UDS lines are set up yet. Open Setup and enter each dental service line with its procedure codes.");
				return;
			}
			int countWithoutCodes=udsPlusLineMap.ListLines.Count(x => string.IsNullOrWhiteSpace(x.Codes));
			labelLines.Text=Lan.g(this,"UDS lines set up:")+" "+udsPlusLineMap.ListLines.Count;
			if(countWithoutCodes>0) {
				labelLines.Text+="\r\n"+Lan.g(this,"Lines without codes:")+" "+countWithoutCodes;
			}
		}

		private void checkAllProv_Click(object sender,EventArgs e) {
			if(checkAllProv.Checked) {
				listProv.ClearSelected();
			}
		}

		private void listProv_Click(object sender,EventArgs e) {
			if(listProv.SelectedIndices.Count>0) {
				checkAllProv.Checked=false;
			}
		}

		private void butSetup_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			using FormRpUdsPlusSetup formRpUdsPlusSetup=new FormRpUdsPlusSetup();
			formRpUdsPlusSetup.ShowDialog();
			FillLineSummary();
		}

		///<summary>Checks the inputs and runs the report. Returns null when an input is wrong or the setup is missing; the user was told why.</summary>
		private UdsPlusResult RunReport(out DateTime dateFrom,out DateTime dateTo) {
			dateFrom=DateTime.MinValue;
			dateTo=DateTime.MinValue;
			if(!dateRangePicker.IsValid()) {
				MsgBox.Show(this,"Please fix the dates first.");
				return null;
			}
			dateFrom=dateRangePicker.GetDateTimeFrom().Date;
			dateTo=dateRangePicker.GetDateTimeTo().Date;
			if(dateFrom.Year<1880 || dateTo.Year<1880) {
				MsgBox.Show(this,"Please enter both dates.");
				return null;
			}
			if(dateTo<dateFrom) {
				MsgBox.Show(this,"End date cannot be before start date.");
				return null;
			}
			if(!checkAllProv.Checked && listProv.SelectedIndices.Count==0) {
				MsgBox.Show(this,"At least one provider must be selected.");
				return null;
			}
			List<long> listClinicNums=new List<long>();
			if(PrefC.HasClinicsEnabled) {
				listClinicNums=comboClinics.ListClinicNumsSelected;
				if(listClinicNums.Count==0) {
					MsgBox.Show(this,"At least one clinic must be selected.");
					return null;
				}
			}
			List<long> listProvNums=new List<long>();
			if(!checkAllProv.Checked) {
				listProvNums=listProv.GetListSelected<Provider>().Select(x => x.ProvNum).ToList();
			}
			UdsPlusLineMap udsPlusLineMap=GetLineMap(false);
			if(udsPlusLineMap==null) {
				return null;
			}
			if(udsPlusLineMap.ListLines.Count==0) {
				MsgBox.Show(this,"No UDS lines are set up yet. Open Setup and enter each dental service line with its procedure codes.");
				return null;
			}
			UdsPlusResult udsPlusResult;
			Cursor=Cursors.WaitCursor;
			try {
				udsPlusResult=RpUdsPlus.Run(udsPlusLineMap,dateFrom,dateTo,listClinicNums,listProvNums);
			}
			catch(Exception ex) {
				Cursor=Cursors.Default;
				string text=Lan.g(this,"Error getting report data:")+" "+ex.Message+"\r\n\r\n"+ex.StackTrace;
				using MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(text);
				msgBoxCopyPaste.ShowDialog();
				return null;
			}
			Cursor=Cursors.Default;
			return udsPlusResult;
		}

		private string GetDateRangeText(DateTime dateFrom,DateTime dateTo) {
			return dateFrom.ToShortDateString()+" - "+dateTo.ToShortDateString();
		}

		private string GetProviderText() {
			if(checkAllProv.Checked) {
				return Lan.g(this,"All Providers");
			}
			return string.Join(", ",listProv.GetListSelected<Provider>().Select(x => x.Abbr));
		}

		private void butReport_Click(object sender,EventArgs e) {
			UdsPlusResult udsPlusResult=RunReport(out DateTime dateFrom,out DateTime dateTo);
			if(udsPlusResult==null) {
				return;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.Reports,0,"UDS+ Dental Services report run for "+dateFrom.ToString("yyyy-MM-dd")+" to "
				+dateTo.ToString("yyyy-MM-dd")+".");
			DataTable table=new DataTable();
			table.Columns.Add("Line");
			table.Columns.Add("Service");
			table.Columns.Add("Visits",typeof(int));
			table.Columns.Add("Patients",typeof(int));
			foreach(UdsPlusLineCount udsPlusLineCount in udsPlusResult.ListLineCounts) {
				table.Rows.Add(udsPlusLineCount.Line,udsPlusLineCount.Description,udsPlusLineCount.Visits,udsPlusLineCount.Patients);
			}
			DataTable tableTotals=new DataTable();
			tableTotals.Columns.Add("Line");
			tableTotals.Columns.Add("Service");
			tableTotals.Columns.Add("Visits",typeof(int));
			tableTotals.Columns.Add("Patients",typeof(int));
			tableTotals.Rows.Add("",Lan.g(this,"Any line (each visit and patient once)"),udsPlusResult.TotalVisits,udsPlusResult.TotalPatients);
			ReportComplex report=new ReportComplex(true,false);
			Font font=new Font("Tahoma",9);
			Font fontTitle=new Font("Tahoma",17,FontStyle.Bold);
			Font fontSubTitle=new Font("Tahoma",10,FontStyle.Bold);
			report.ReportName=Lan.g(this,"UDS+ Dental Services");
			report.AddTitle("Title",Lan.g(this,"UDS+ Dental Services"),fontTitle);
			report.AddSubTitle("Practice Title",PrefC.GetString(PrefName.PracticeTitle),fontSubTitle);
			report.AddSubTitle("Dates",GetDateRangeText(dateFrom,dateTo),fontSubTitle);
			report.AddSubTitle("Providers",GetProviderText(),fontSubTitle);
			if(PrefC.HasClinicsEnabled) {
				report.AddSubTitle("Clinics",comboClinics.GetStringSelectedClinics(),fontSubTitle);
			}
			QueryObject query=report.AddQuery(table,Lan.g(this,"Visits and patients by UDS line"),"",SplitByKind.None,1,true);
			query.AddColumn(Lan.g(this,"Line"),60,FieldValueType.String,font);
			query.AddColumn(Lan.g(this,"Service"),380,FieldValueType.String,font);
			query.AddColumn(Lan.g(this,"Visits"),90,FieldValueType.Integer,font);
			query.AddColumn(Lan.g(this,"Patients"),90,FieldValueType.Integer,font);
			query=report.AddQuery(tableTotals,Lan.g(this,"All dental service lines"),"",SplitByKind.None,1,true);
			query.AddColumn("",60,FieldValueType.String,font);
			query.AddColumn(Lan.g(this,"Total"),380,FieldValueType.String,font);
			query.AddColumn(Lan.g(this,"Total Visits"),90,FieldValueType.Integer,font);
			query.AddColumn(Lan.g(this,"Total Patients"),90,FieldValueType.Integer,font);
			report.AddPageNum(font);
			if(!report.SubmitQueries()) {
				return;
			}
			using FormReportComplex formReportComplex=new FormReportComplex(report);
			formReportComplex.ShowDialog();
		}

		private void butExport_Click(object sender,EventArgs e) {
			UdsPlusResult udsPlusResult=RunReport(out DateTime dateFrom,out DateTime dateTo);
			if(udsPlusResult==null) {
				return;
			}
			if(udsPlusResult.ListVisitRows.Count==0) {
				MsgBox.Show(this,"No visits in this date range count on any UDS line.");
				return;
			}
			string fileName="UdsPlusDental_"+dateFrom.ToString("yyyyMMdd")+"_"+dateTo.ToString("yyyyMMdd")+".csv";
			string path;
			using(SaveFileDialog saveFileDialog=new SaveFileDialog()) {
				saveFileDialog.FileName=fileName;
				saveFileDialog.Filter="CSV files (*.csv)|*.csv|All files (*.*)|*.*";
				saveFileDialog.DefaultExt="csv";
				if(saveFileDialog.ShowDialog()!=DialogResult.OK) {
					return;
				}
				path=saveFileDialog.FileName;
			}
			try {
				File.WriteAllText(path,RpUdsPlus.ToCsv(udsPlusResult.ListVisitRows),new UTF8Encoding(false));
			}
			catch(Exception ex) {
				MsgBox.Show(Lan.g(this,"The file could not be saved:")+" "+ex.Message);
				return;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.Reports,0,"UDS+ Dental Services patient-level file saved for "+dateFrom.ToString("yyyy-MM-dd")+" to "
				+dateTo.ToString("yyyy-MM-dd")+": "+udsPlusResult.ListVisitRows.Count+" rows, "+udsPlusResult.TotalPatients+" patients.");
			MsgBox.Show(Lan.g(this,"File saved. It holds patient information: keep it as securely as the patient chart.")+"\r\n"+path);
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}
	}
}
