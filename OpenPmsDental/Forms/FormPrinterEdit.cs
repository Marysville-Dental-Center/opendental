using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormPrinterEdit:FormODBase {
		private PrintSituation _printSituation;
		///<summary>The printer saved for this situation when it is not installed on this computer. It stays in the list, marked "(not installed)",
		///so saving without choosing another printer keeps it. Empty otherwise.</summary>
		private string _printerNameNotInstalled="";
		///<summary>What Windows reports about each installed printer, read once when the window opens (25.4).</summary>
		private Dictionary<string,PrinterOnlineStatus> _dictPrinterStatuses;

		public FormPrinterEdit(Printer printer) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_printSituation=printer.PrintSit;
			textFileExtension.Text=printer.FileExtension;
			checkPrompt.Checked=printer.DisplayPrompt;
			checkVirtualPrinter.Checked=printer.IsVirtualPrinter;
			textSituation.Text=_printSituation.GetDescription();
			_dictPrinterStatuses=PrinterStatuses.GetAll();
			FillComboPrinter(printer.PrinterName);
			//25.4: what the first item of the list means for this situation.
			if(_printSituation==PrintSituation.Default) {
				labelPrinterRule.Text=Lan.g(this,"Windows default: the printer Windows uses as its default.");
			}
			else {
				labelPrinterRule.Text=Lan.g(this,"default: the printer of the Default situation, or the Windows default printer if that has none.");
			}
			comboPrinter.SelectedIndexChanged+=comboPrinter_SelectedIndexChanged;
			SetLabelStatus();
		}

		///<summary>The printer chosen in the list, or "" for the first item (default / Windows default).</summary>
		private string GetPrinterNameSelected() {
			if(comboPrinter.SelectedIndex<=0) {
				return "";
			}
			if(_printerNameNotInstalled!="" && comboPrinter.SelectedIndex==comboPrinter.Items.Count-1) {
				return _printerNameNotInstalled;//the last item, added by FillComboPrinter
			}
			return comboPrinter.SelectedItem.ToString();
		}

		///<summary>Shows what Windows reports about the chosen printer: Online, Offline, Paused or Error, or nothing when Windows reports no
		///state (25.4).</summary>
		private void SetLabelStatus() {
			string printerName=GetPrinterNameSelected();
			if(printerName=="" && _printSituation==PrintSituation.Default) {
				printerName=PrintSituationRules.GetWindowsDefaultPrinterName();
			}
			if(printerName!="" && printerName==_printerNameNotInstalled) {
				labelStatus.Text=Lan.g(this,"Not installed");
				return;
			}
			string statusText=PrinterStatuses.GetText(PrinterStatuses.GetStatus(_dictPrinterStatuses,printerName));
			labelStatus.Text=(statusText=="") ? "" : Lan.g(this,statusText);
		}

		private void comboPrinter_SelectedIndexChanged(object sender,EventArgs e) {
			SetLabelStatus();
		}

		private void FillComboPrinter(string printerName) {
			PrinterSettings.StringCollection installedPrinters=null;
			try {
				installedPrinters=PrinterSettings.InstalledPrinters;
			}
			catch(Exception ex) {//do not let the window open if printers cannot be accessed
				FriendlyException.Show(Lan.g(this,"Unable to access installed printers."),ex);
				DialogResult=DialogResult.Cancel;
				return;
			}
			comboPrinter.Items.Clear();
			if(_printSituation==PrintSituation.Default){
				comboPrinter.Items.Add(Lan.g(this,"Windows default"));
			}
			else{
				comboPrinter.Items.Add(Lan.g(this,"default"));
			}
			for(int i=0;i<installedPrinters.Count;i++){
				comboPrinter.Items.Add(installedPrinters[i]);
				if(printerName==installedPrinters[i]){
					comboPrinter.SelectedIndex=i+1;
				}
			}
			if(comboPrinter.SelectedIndex==-1 && !string.IsNullOrEmpty(printerName)) {
				//The saved printer is not installed on this computer. Keep it selectable instead of silently switching to the default.
				_printerNameNotInstalled=printerName;
				comboPrinter.Items.Add(printerName+" "+Lan.g(this,"(not installed)"));
				comboPrinter.SelectedIndex=comboPrinter.Items.Count-1;
			}
			if(comboPrinter.SelectedIndex==-1){
				comboPrinter.SelectedIndex=0;
			}
		}

		private void butSave_Click(object sender,EventArgs e) {
			string compName=ODEnvironment.MachineName;
			string printerName="";
			bool isChecked=checkPrompt.Checked;
			//PrintSituation sit=PrintSituation.Default;
			//first: main Default, since not in panel Simple
			if(comboPrinter.SelectedIndex>0){
				printerName=GetPrinterNameSelected();
			}
			Printers.PutForSit(_printSituation,compName,printerName,isChecked,isVirtual:checkVirtualPrinter.Checked,fileExtension:textFileExtension.Text);
			DataValid.SetInvalid(InvalidType.Computers);
			Printers.RefreshCache();//the other computers don't care
			DialogResult=DialogResult.OK;
		}
	}
}