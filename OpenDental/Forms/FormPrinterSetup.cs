using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using OpenDentBusiness;
using CodeBase;
using OpenDental.UI;
using System.Collections.Generic;
using System.Linq;

namespace OpenDental{
///<summary></summary>
	public partial class FormPrinterSetup : FormODBase {

		///<summary></summary>
		public FormPrinterSetup(){
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormPrinterSetup_Load(object sender, System.EventArgs e) {
			checkSimple.Checked=PrefC.GetBool(PrefName.EasyHidePrinters);
			FillGrid();
		}

		public void FillGrid(){
			gridPrinters.BeginUpdate();
			//Clear and reset the columns
			gridPrinters.Columns.Clear();
			GridColumn col=new GridColumn("Situation",150);
			gridPrinters.Columns.Add(col);
			col=new GridColumn("Printer",190);
			gridPrinters.Columns.Add(col);
			col=new GridColumn("Prompt",50,HorizontalAlignment.Center);
			gridPrinters.Columns.Add(col);
			col=new GridColumn("Virtual",50,HorizontalAlignment.Center);
			gridPrinters.Columns.Add(col);
			col=new GridColumn("File Extension",50,HorizontalAlignment.Center);
			gridPrinters.Columns.Add(col);
			//25.4: the printer that prints for the situation on this workstation, the rule that picked it, and what Windows reports about it.
			col=new GridColumn("Printer Used",220);
			gridPrinters.Columns.Add(col);
			col=new GridColumn("Rule",130);
			gridPrinters.Columns.Add(col);
			col=new GridColumn("Status",60,HorizontalAlignment.Center);
			gridPrinters.Columns.Add(col);
			//Clear out the rows.
			gridPrinters.ListGridRows.Clear();
			GridRow row;
			//Get a list of all of the print situations
			List<PrintSituation> listPrintSituations=new List<PrintSituation>{
				PrintSituation.Default, 
				PrintSituation.Appointments, 
				PrintSituation.Claim, 
				PrintSituation.LabelSheet,
				PrintSituation.LabelSingle, 
				PrintSituation.Postcard, 
				PrintSituation.Rx, 
				PrintSituation.RxControlled, 
				PrintSituation.RxMulti, 
				PrintSituation.Statement, 
				PrintSituation.TPPerio, 
				PrintSituation.Receipt};
			//If simple only show the default row.
			if(checkSimple.Checked){
				listPrintSituations.RemoveAll(x=>x!=PrintSituation.Default);
			}
			string windowsDefaultPrinterName=PrintSituationRules.GetWindowsDefaultPrinterName();
			Dictionary<string,PrinterOnlineStatus> dictPrinterStatuses=PrinterStatuses.GetAll();
			for(int i=0;i<listPrintSituations.Count;i++){
				Printer printerForSit=Printers.GetForSit(listPrintSituations[i]);
				row=new GridRow();
				row.Cells.Add(listPrintSituations[i].GetDescription());
				if(printerForSit!=null){
					row.Cells.Add(printerForSit.PrinterName);
					row.Cells.Add(printerForSit.DisplayPrompt ? "X" : "");
					row.Cells.Add(printerForSit.IsVirtualPrinter ? "X" : "");
					row.Cells.Add(printerForSit.FileExtension);
				}
				else {
					row.Cells.Add("");
					row.Cells.Add("");
					row.Cells.Add("");
					row.Cells.Add("");
					printerForSit=new Printer{PrintSit=listPrintSituations[i]};
				}
				FillPrinterUsedCells(row,listPrintSituations[i],windowsDefaultPrinterName,dictPrinterStatuses);
				row.Tag=printerForSit;
				gridPrinters.ListGridRows.Add(row);
			}
			gridPrinters.EndUpdate();
		}

		///<summary>Adds the Printer Used, Rule and Status cells of a situation (25.4). Uses the cached printer rows, which FillGrid's callers refresh
		///after a change.</summary>
		private void FillPrinterUsedCells(GridRow row,PrintSituation printSituation,string windowsDefaultPrinterName,
			Dictionary<string,PrinterOnlineStatus> dictPrinterStatuses)
		{
			PrinterChoice printerChoice=PrintSituationRules.ResolveForThisComputer(printSituation);
			row.Cells.Add(PrintSituationRules.GetPrinterUsedText(printerChoice,windowsDefaultPrinterName));
			row.Cells.Add(PrintSituationRules.GetSourceText(printerChoice));
			string printerNameUsed=printerChoice.PrinterName;
			if(printerChoice.Source==PrinterChoiceSource.WindowsDefault) {
				printerNameUsed=windowsDefaultPrinterName;
			}
			string statusText=PrinterStatuses.GetText(PrinterStatuses.GetStatus(dictPrinterStatuses,printerNameUsed));
			row.Cells.Add(statusText=="" ? "" : Lan.g(this,statusText));//blank where Windows reports no state
		}

		#region Events
		private void gridPrinters_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			Printer printer=gridPrinters.SelectedTag<Printer>();
			using FormPrinterEdit formPrinterEdit=new FormPrinterEdit(printer);
			if(formPrinterEdit.ShowDialog()==DialogResult.Cancel){
				return;
			}
			FillGrid();
		}
		#endregion

		private void checkSimple_Click(object sender, System.EventArgs e) {
			FillGrid();
		}

		private void butSave_Click(object sender, System.EventArgs e){
			string compName=ODEnvironment.MachineName;
			if(checkSimple.Checked && !PrefC.GetBool(PrefName.EasyHidePrinters)){
				//if user clicked the simple option
				if(!MsgBox.Show(this,MsgBoxButtons.YesNo,"Warning!  You have selected the simple interface option."+
					"  This will force all computers to use the simple mode."+
					"  This will also clear all printing preferences for all other computers and set them back to default."+
					"  Are you sure you wish to continue?"))
				{
					return;
				}
				Printers.ClearAll();
				Printers.RefreshCache();
				//Set the default printer.
				Printer printerDefault=gridPrinters.ListGridRows.Select(x=>((Printer)x.Tag)).FirstOrDefault(x=>x.PrintSit==PrintSituation.Default);
				Printers.PutForSit(printerDefault.PrintSit,compName,printerDefault.PrinterName,true,isVirtual:printerDefault.IsVirtualPrinter,fileExtension:printerDefault.FileExtension);
			}
			DataValid.SetInvalid(InvalidType.Computers);
			if(checkSimple.Checked!=PrefC.GetBool(PrefName.EasyHidePrinters)){
				Prefs.UpdateBool(PrefName.EasyHidePrinters,checkSimple.Checked);
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			Printers.RefreshCache();//the other computers don't care
			DialogResult=DialogResult.OK;
		}

	}
}