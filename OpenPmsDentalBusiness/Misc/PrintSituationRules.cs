// M2-MISC (Open Dental 25.4, "minor Print window enhancements"): the printer rules of Setup, Printers, as the 25.4 database documentation
// clarifies them, and the online/offline state Windows reports for a printer. Used when printing (OpenDental.PrinterL.SetPrinter and WPF
// printing, PrintOptionsWpf), by the Printers setup windows to show which rule picks the printer for each print situation, and by the Print
// Options window (B-PRINT's FrmPrintOptions) to show which rule picked the printer and its state.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Management;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Which rule picked the printer for a print situation.</summary>
	public enum PrinterChoiceSource {
		///<summary>The situation's own row names an installed printer.</summary>
		SituationSetting,
		///<summary>The situation has no row, or its row names no installed printer, and the Default situation's row names an installed printer.
		///PrintSituation.Default is this program's own default situation, not the Windows default printer.</summary>
		DefaultSetting,
		///<summary>No row names an installed printer: Windows' default printer prints.</summary>
		WindowsDefault,
	}

	///<summary>Where the printer selected in the Print Options window comes from (25.4).</summary>
	public enum PrintWindowPrinterSource {
		///<summary>The printer the situation's rules pick (Setup, Printers, otherwise the Windows default).</summary>
		Rules,
		///<summary>The printer last chosen for the situation in this session (PrintOptions.Remember), which is not the rules' printer.</summary>
		LastUsed,
		///<summary>Another printer, chosen in the window.</summary>
		ChosenHere,
	}

	///<summary>The printer a print situation uses on this computer, and why.</summary>
	public class PrinterChoice {
		public PrintSituation PrintSituation;
		///<summary>The printer that prints. "" means the Windows default printer.</summary>
		public string PrinterName="";
		public PrinterChoiceSource Source;
		///<summary>True if the print shows the prompt (the Print window). Taken from the situation's row when it has one, otherwise from the Default
		///row, otherwise false.</summary>
		public bool DisplayPrompt;
		///<summary>The row whose prompt and virtual printer settings apply: the situation's row, otherwise the Default row. Null when neither
		///exists.</summary>
		public Printer PrinterRow;
		///<summary>Printer names of rows that were passed over because the printer is not installed on this computer, situation row first.</summary>
		public List<string> ListPrinterNamesNotInstalled=new List<string>();
	}

	///<summary>The printer rules of Setup, Printers. OpenDental.PrinterL.SetPrinter (WinForms printing) and WpfControls.PrintOptionsWpf (WPF
	///printing) pick the printer with ResolveForThisComputer, so the setup windows, the Print Options window and printing cannot drift apart.</summary>
	public static class PrintSituationRules {
		///<summary>Applies the rules to the rows of one computer. printerDefault is the computer's row for PrintSituation.Default, printerSit its row
		///for printSituation (pass the same row, or null, for the Default situation). An empty PrinterName, or no row, means "use the Default
		///situation's printer, or the Windows default".</summary>
		public static PrinterChoice Resolve(PrintSituation printSituation,Printer printerDefault,Printer printerSit,Func<string,bool> funcIsInstalled) {
			PrinterChoice printerChoice=new PrinterChoice();
			printerChoice.PrintSituation=printSituation;
			printerChoice.Source=PrinterChoiceSource.WindowsDefault;
			if(printSituation==PrintSituation.Default) {
				printerSit=null;//The Default situation has only the one row.
			}
			if(printerDefault!=null) {
				printerChoice.PrinterRow=printerDefault;
				printerChoice.DisplayPrompt=printerDefault.DisplayPrompt;
			}
			if(printerSit!=null) {
				printerChoice.PrinterRow=printerSit;
				printerChoice.DisplayPrompt=printerSit.DisplayPrompt;
			}
			if(printerSit!=null && IsNamedAndInstalled(printerSit.PrinterName,funcIsInstalled,printerChoice)) {
				printerChoice.PrinterName=printerSit.PrinterName;
				printerChoice.Source=PrinterChoiceSource.SituationSetting;
				return printerChoice;
			}
			if(printerDefault!=null && IsNamedAndInstalled(printerDefault.PrinterName,funcIsInstalled,printerChoice)) {
				printerChoice.PrinterName=printerDefault.PrinterName;
				printerChoice.Source=PrinterChoiceSource.DefaultSetting;
				return printerChoice;
			}
			return printerChoice;
		}

		///<summary>The rules for this computer's rows (cache) and installed printers.</summary>
		public static PrinterChoice ResolveForThisComputer(PrintSituation printSituation) {
			Meth.NoCheckMiddleTierRole();
			Printer printerDefault=Printers.GetForSit(PrintSituation.Default);
			Printer printerSit=(printSituation==PrintSituation.Default) ? null : Printers.GetForSit(printSituation);
			return Resolve(printSituation,printerDefault,printerSit,Printers.PrinterIsInstalled);
		}

		///<summary>Which rule picked the printer, for example "From Statements setting", "From Default setting" or "Windows default". Not
		///translated here.</summary>
		public static string GetSourceText(PrinterChoice printerChoice) {
			switch(printerChoice.Source) {
				case PrinterChoiceSource.SituationSetting:
					return "From "+printerChoice.PrintSituation.GetDescription()+" setting";
				case PrinterChoiceSource.DefaultSetting:
					return "From "+PrintSituation.Default.GetDescription()+" setting";
				default:
					return "Windows default";
			}
		}

		///<summary>The printer that prints, for display: the chosen printer's name, or "Windows default" followed by the Windows default printer's
		///name in parentheses when it is known. A passed-over printer that is not installed is named too.</summary>
		public static string GetPrinterUsedText(PrinterChoice printerChoice,string windowsDefaultPrinterName) {
			string text=printerChoice.PrinterName;
			if(printerChoice.Source==PrinterChoiceSource.WindowsDefault) {
				text="Windows default";
				if(!string.IsNullOrWhiteSpace(windowsDefaultPrinterName)) {
					text+=" ("+windowsDefaultPrinterName+")";
				}
			}
			if(printerChoice.ListPrinterNamesNotInstalled.Count>0) {
				text+="; not installed: "+string.Join(", ",printerChoice.ListPrinterNamesNotInstalled);
			}
			return text;
		}

		///<summary>The printer the rules pick, by name: the chosen printer, or the Windows default printer's name when no row names an installed
		///printer ("" when Windows has no default printer).</summary>
		public static string GetRulesPrinterName(PrinterChoice printerChoice,string windowsDefaultPrinterName) {
			if(printerChoice.Source==PrinterChoiceSource.WindowsDefault) {
				return windowsDefaultPrinterName??"";
			}
			return printerChoice.PrinterName;
		}

		///<summary>Where the printer selected in the Print Options window comes from: the rules' printer, the printer last chosen for the situation
		///in this session (PrintOptions.Remember), or another one. An empty selection counts as the rules' printer.</summary>
		public static PrintWindowPrinterSource GetPrintWindowSource(PrinterChoice printerChoice,string printerNameSelected,string printerNameLastUsed,
			string windowsDefaultPrinterName)
		{
			if(string.IsNullOrEmpty(printerNameSelected) || IsSamePrinter(printerNameSelected,GetRulesPrinterName(printerChoice,windowsDefaultPrinterName))) {
				return PrintWindowPrinterSource.Rules;
			}
			if(IsSamePrinter(printerNameSelected,printerNameLastUsed)) {
				return PrintWindowPrinterSource.LastUsed;
			}
			return PrintWindowPrinterSource.ChosenHere;
		}

		///<summary>The line under the printer in the Print Options window (25.4): which rule picked the selected printer, for example "From
		///Statements setting", "From Default setting" or "Windows default". When the selected printer is not the rules' printer, "Last used this
		///session" or "Chosen in this window", followed in parentheses by the rule and the printer it picks, which is what a print without the window
		///uses. Printers of rows passed over because they are not installed are named. Not translated here.</summary>
		public static string GetPrintWindowSourceText(PrinterChoice printerChoice,string printerNameSelected,string printerNameLastUsed,
			string windowsDefaultPrinterName)
		{
			string textNotInstalled="";
			if(printerChoice.ListPrinterNamesNotInstalled.Count>0) {
				textNotInstalled="; not installed: "+string.Join(", ",printerChoice.ListPrinterNamesNotInstalled);
			}
			PrintWindowPrinterSource printWindowPrinterSource=GetPrintWindowSource(printerChoice,printerNameSelected,printerNameLastUsed,
				windowsDefaultPrinterName);
			if(printWindowPrinterSource==PrintWindowPrinterSource.Rules) {
				return GetSourceText(printerChoice)+textNotInstalled;
			}
			string textRules=GetSourceText(printerChoice);
			string printerNameRules=GetRulesPrinterName(printerChoice,windowsDefaultPrinterName);
			if(!string.IsNullOrWhiteSpace(printerNameRules)) {
				textRules+=": "+printerNameRules;
			}
			string textSource="Chosen in this window";
			if(printWindowPrinterSource==PrintWindowPrinterSource.LastUsed) {
				textSource="Last used this session";
			}
			return textSource+" ("+textRules+textNotInstalled+")";
		}

		///<summary>True if a situation needs a printer row: a printer is chosen, Prompt is on, or it is a virtual printer. A row must exist when
		///Prompt is on even with no printer chosen; with none of the three the row is removed (Printers.PutForSit).</summary>
		public static bool IsRowNeeded(string printerName,bool displayPrompt,bool isVirtual) {
			return !string.IsNullOrEmpty(printerName) || displayPrompt || isVirtual;
		}

		///<summary>The name of Windows' default printer, or "" when there is none or the spooler does not answer.</summary>
		public static string GetWindowsDefaultPrinterName() {
			try {
				return new PrinterSettings().PrinterName??"";
			}
			catch(Exception ex) {
				ex.DoNothing();
				return "";
			}
		}

		///<summary>Printer names are compared as Windows does, ignoring case. "" is never the same printer.</summary>
		private static bool IsSamePrinter(string printerName1,string printerName2) {
			if(string.IsNullOrEmpty(printerName1) || string.IsNullOrEmpty(printerName2)) {
				return false;
			}
			return string.Equals(printerName1,printerName2,StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsNamedAndInstalled(string printerName,Func<string,bool> funcIsInstalled,PrinterChoice printerChoice) {
			if(string.IsNullOrEmpty(printerName)) {
				return false;
			}
			bool isInstalled=false;
			try {
				isInstalled=funcIsInstalled(printerName);
			}
			catch(Exception ex) {
				ex.DoNothing();//The spooler is down: treat as not installed, as printing would.
			}
			if(!isInstalled && !printerChoice.ListPrinterNamesNotInstalled.Contains(printerName)) {
				printerChoice.ListPrinterNamesNotInstalled.Add(printerName);
			}
			return isInstalled;
		}
	}

	///<summary>What Windows reports about a printer.</summary>
	public enum PrinterOnlineStatus {
		///<summary>Windows does not report a state (or was not asked). Nothing is shown.</summary>
		Unknown,
		Online,
		Offline,
		Paused,
		Error,
	}

	///<summary>The online/offline state of the installed printers, read from WMI (Win32_Printer) on this computer.</summary>
	public static class PrinterStatuses {
		///<summary>The state from the Win32_Printer properties WorkOffline, ExtendedPrinterStatus and PrinterStatus (null when not reported).
		///"Use Printer Offline" wins; then the extended status; then the plain status.</summary>
		public static PrinterOnlineStatus Interpret(bool? workOffline,int? extendedPrinterStatus,int? printerStatus) {
			if(workOffline==true) {
				return PrinterOnlineStatus.Offline;
			}
			switch(extendedPrinterStatus) {
				case 7://Offline
				case 11://Not Available
					return PrinterOnlineStatus.Offline;
				case 8://Paused
					return PrinterOnlineStatus.Paused;
				case 6://Stopped Printing
				case 9://Error
					return PrinterOnlineStatus.Error;
				case 3://Idle
				case 4://Printing
				case 5://Warming Up
				case 10://Busy
				case 12://Waiting
				case 13://Processing
				case 14://Initialization
				case 15://Power Save
				case 17://I/O Active
				case 18://Manual Feed
					return PrinterOnlineStatus.Online;
			}
			switch(printerStatus) {
				case 7://Offline
					return PrinterOnlineStatus.Offline;
				case 6://Stopped Printing
					return PrinterOnlineStatus.Error;
				case 3://Idle
				case 4://Printing
				case 5://Warmup
					return PrinterOnlineStatus.Online;
			}
			return PrinterOnlineStatus.Unknown;
		}

		///<summary>The text shown for a state: "Online", "Offline", "Paused", "Error", or "" when unknown.</summary>
		public static string GetText(PrinterOnlineStatus printerOnlineStatus) {
			if(printerOnlineStatus==PrinterOnlineStatus.Unknown) {
				return "";
			}
			return printerOnlineStatus.ToString();
		}

		///<summary>The state of every printer installed on this computer, by printer name (case-insensitive). Empty when WMI does not answer
		///within a few seconds. Network printers can take a moment, so call this once per window, not per row.</summary>
		public static Dictionary<string,PrinterOnlineStatus> GetAll() {
			Dictionary<string,PrinterOnlineStatus> dictStatuses=new Dictionary<string,PrinterOnlineStatus>(StringComparer.OrdinalIgnoreCase);
			try {
				EnumerationOptions enumerationOptions=new EnumerationOptions();
				enumerationOptions.Timeout=TimeSpan.FromSeconds(5);
				using(ManagementObjectSearcher managementObjectSearcher=new ManagementObjectSearcher("root\\CIMV2",
					"SELECT Name,WorkOffline,ExtendedPrinterStatus,PrinterStatus FROM Win32_Printer",enumerationOptions))
				{
					foreach(ManagementBaseObject managementBaseObject in managementObjectSearcher.Get()) {
						string name=managementBaseObject["Name"]?.ToString();
						if(string.IsNullOrEmpty(name)) {
							continue;
						}
						dictStatuses[name]=Interpret(ToBool(managementBaseObject["WorkOffline"]),ToInt(managementBaseObject["ExtendedPrinterStatus"]),
							ToInt(managementBaseObject["PrinterStatus"]));
					}
				}
			}
			catch(Exception ex) {
				ex.DoNothing();//WMI is not available: no states are shown.
			}
			return dictStatuses;
		}

		///<summary>The state of one printer from a GetAll result. Unknown for "" or a printer Windows did not list.</summary>
		public static PrinterOnlineStatus GetStatus(Dictionary<string,PrinterOnlineStatus> dictStatuses,string printerName) {
			if(dictStatuses==null || string.IsNullOrEmpty(printerName) || !dictStatuses.TryGetValue(printerName,out PrinterOnlineStatus printerOnlineStatus)) {
				return PrinterOnlineStatus.Unknown;
			}
			return printerOnlineStatus;
		}

		private static bool? ToBool(object value) {
			if(value==null) {
				return null;
			}
			try {
				return Convert.ToBoolean(value);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
		}

		private static int? ToInt(object value) {
			if(value==null) {
				return null;
			}
			try {
				return Convert.ToInt32(value);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
		}
	}
}
