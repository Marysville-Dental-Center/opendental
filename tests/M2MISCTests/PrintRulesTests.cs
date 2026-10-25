// v25_4-misc-print-window: the printer rules of Setup, Printers as the 25.4 documentation clarifies them (PrintSituationRules), the printer
// states Windows reports (PrinterStatuses), and the rule that a row must exist when Prompt is on (Printers.PutForSit).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2MISCTests {
	[TestClass]
	public class PrintRulesTests {
		private const string PRINTER_FRONT="Synthetic Front Desk";
		private const string PRINTER_BACK="Synthetic Back Office";
		private static readonly Func<string,bool> _funcIsInstalled=(name) => name==PRINTER_FRONT || name==PRINTER_BACK;

		private static Printer Row(PrintSituation printSituation,string printerName,bool displayPrompt=false) {
			return new Printer { PrintSit=printSituation,PrinterName=printerName,DisplayPrompt=displayPrompt };
		}

		#region Rules, no database
		[TestMethod]
		public void NoRows_WindowsDefault_NoPrompt() {
			PrinterChoice printerChoice=PrintSituationRules.Resolve(PrintSituation.Statement,null,null,_funcIsInstalled);
			Assert.AreEqual(PrinterChoiceSource.WindowsDefault,printerChoice.Source);
			Assert.AreEqual("",printerChoice.PrinterName);
			Assert.IsFalse(printerChoice.DisplayPrompt);
			Assert.IsNull(printerChoice.PrinterRow);
			Assert.AreEqual("Windows default",PrintSituationRules.GetSourceText(printerChoice));
			Assert.AreEqual("Windows default (Synthetic Win)",PrintSituationRules.GetPrinterUsedText(printerChoice,"Synthetic Win"));
			Assert.AreEqual("Windows default",PrintSituationRules.GetPrinterUsedText(printerChoice,""));
		}

		[TestMethod]
		public void DefaultSituation_IsTheProgramsDefault_NotWindows() {
			Printer printerDefault=Row(PrintSituation.Default,PRINTER_FRONT,displayPrompt:true);
			PrinterChoice printerChoice=PrintSituationRules.Resolve(PrintSituation.Statement,printerDefault,null,_funcIsInstalled);
			Assert.AreEqual(PrinterChoiceSource.DefaultSetting,printerChoice.Source);
			Assert.AreEqual(PRINTER_FRONT,printerChoice.PrinterName);
			Assert.IsTrue(printerChoice.DisplayPrompt,"Without a row of its own, the situation takes the Default row's prompt.");
			Assert.AreSame(printerDefault,printerChoice.PrinterRow);
			Assert.AreEqual("From Default setting",PrintSituationRules.GetSourceText(printerChoice));
			//The Default situation itself.
			printerChoice=PrintSituationRules.Resolve(PrintSituation.Default,printerDefault,printerDefault,_funcIsInstalled);
			Assert.AreEqual(PrinterChoiceSource.DefaultSetting,printerChoice.Source);
			Assert.AreEqual("From Default setting",PrintSituationRules.GetSourceText(printerChoice));
		}

		[TestMethod]
		public void SituationRow_WithInstalledPrinter_Wins() {
			Printer printerDefault=Row(PrintSituation.Default,PRINTER_FRONT,displayPrompt:true);
			Printer printerSit=Row(PrintSituation.Statement,PRINTER_BACK,displayPrompt:false);
			PrinterChoice printerChoice=PrintSituationRules.Resolve(PrintSituation.Statement,printerDefault,printerSit,_funcIsInstalled);
			Assert.AreEqual(PrinterChoiceSource.SituationSetting,printerChoice.Source);
			Assert.AreEqual(PRINTER_BACK,printerChoice.PrinterName);
			Assert.IsFalse(printerChoice.DisplayPrompt,"The situation's own row decides the prompt.");
			Assert.AreSame(printerSit,printerChoice.PrinterRow);
			Assert.AreEqual("From Statements setting",PrintSituationRules.GetSourceText(printerChoice));
			Assert.AreEqual(PRINTER_BACK,PrintSituationRules.GetPrinterUsedText(printerChoice,"Synthetic Win"));
		}

		[TestMethod]
		public void SituationRow_WithEmptyPrinter_UsesTheDefaultPrinter_ButItsOwnPrompt() {
			//An empty PrinterName means "use the program default, or the Windows default". The row exists for its Prompt.
			Printer printerDefault=Row(PrintSituation.Default,PRINTER_FRONT,displayPrompt:false);
			Printer printerSit=Row(PrintSituation.Claim,"",displayPrompt:true);
			PrinterChoice printerChoice=PrintSituationRules.Resolve(PrintSituation.Claim,printerDefault,printerSit,_funcIsInstalled);
			Assert.AreEqual(PrinterChoiceSource.DefaultSetting,printerChoice.Source);
			Assert.AreEqual(PRINTER_FRONT,printerChoice.PrinterName);
			Assert.IsTrue(printerChoice.DisplayPrompt);
			Assert.AreSame(printerSit,printerChoice.PrinterRow);
			Assert.AreEqual(0,printerChoice.ListPrinterNamesNotInstalled.Count);
			//No Default row either: Windows default, still with the prompt.
			printerChoice=PrintSituationRules.Resolve(PrintSituation.Claim,null,printerSit,_funcIsInstalled);
			Assert.AreEqual(PrinterChoiceSource.WindowsDefault,printerChoice.Source);
			Assert.IsTrue(printerChoice.DisplayPrompt);
		}

		[TestMethod]
		public void PrinterNotInstalled_FallsBack_AndIsNamed() {
			Printer printerDefault=Row(PrintSituation.Default,"Synthetic Gone Default");
			Printer printerSit=Row(PrintSituation.Rx,"Synthetic Gone Rx");
			PrinterChoice printerChoice=PrintSituationRules.Resolve(PrintSituation.Rx,printerDefault,printerSit,_funcIsInstalled);
			Assert.AreEqual(PrinterChoiceSource.WindowsDefault,printerChoice.Source);
			CollectionAssert.AreEqual(new List<string> { "Synthetic Gone Rx","Synthetic Gone Default" },printerChoice.ListPrinterNamesNotInstalled);
			Assert.AreEqual("Windows default (Synthetic Win); not installed: Synthetic Gone Rx, Synthetic Gone Default",
				PrintSituationRules.GetPrinterUsedText(printerChoice,"Synthetic Win"));
			//The situation's printer is gone, the Default one is there.
			printerDefault=Row(PrintSituation.Default,PRINTER_FRONT);
			printerChoice=PrintSituationRules.Resolve(PrintSituation.Rx,printerDefault,printerSit,_funcIsInstalled);
			Assert.AreEqual(PrinterChoiceSource.DefaultSetting,printerChoice.Source);
			Assert.AreEqual(PRINTER_FRONT,printerChoice.PrinterName);
		}

		[TestMethod]
		public void SpoolerError_CountsAsNotInstalled() {
			Printer printerDefault=Row(PrintSituation.Default,PRINTER_FRONT);
			PrinterChoice printerChoice=PrintSituationRules.Resolve(PrintSituation.Statement,printerDefault,null,
				(name) => throw new InvalidOperationException("spooler"));
			Assert.AreEqual(PrinterChoiceSource.WindowsDefault,printerChoice.Source);
		}

		[TestMethod]
		public void RowIsNeeded_WhenAPrinterPromptOrVirtualIsSet() {
			Assert.IsFalse(PrintSituationRules.IsRowNeeded("",false,false));
			Assert.IsFalse(PrintSituationRules.IsRowNeeded(null,false,false));
			Assert.IsTrue(PrintSituationRules.IsRowNeeded("",true,false),"A row must exist when Prompt is on, even with no printer chosen.");
			Assert.IsTrue(PrintSituationRules.IsRowNeeded(PRINTER_FRONT,false,false));
			Assert.IsTrue(PrintSituationRules.IsRowNeeded("",false,true));
		}

		[TestMethod]
		public void PrinterStatus_FromWindowsProperties() {
			Assert.AreEqual(PrinterOnlineStatus.Offline,PrinterStatuses.Interpret(true,3,3),"Use Printer Offline wins");
			Assert.AreEqual(PrinterOnlineStatus.Online,PrinterStatuses.Interpret(false,3,3));
			Assert.AreEqual(PrinterOnlineStatus.Online,PrinterStatuses.Interpret(null,4,null));
			Assert.AreEqual(PrinterOnlineStatus.Offline,PrinterStatuses.Interpret(false,7,3));
			Assert.AreEqual(PrinterOnlineStatus.Offline,PrinterStatuses.Interpret(false,11,null));
			Assert.AreEqual(PrinterOnlineStatus.Paused,PrinterStatuses.Interpret(false,8,null));
			Assert.AreEqual(PrinterOnlineStatus.Error,PrinterStatuses.Interpret(false,9,null));
			//Extended status not reported (2 Unknown): the plain status decides.
			Assert.AreEqual(PrinterOnlineStatus.Offline,PrinterStatuses.Interpret(false,2,7));
			Assert.AreEqual(PrinterOnlineStatus.Online,PrinterStatuses.Interpret(false,null,3));
			Assert.AreEqual(PrinterOnlineStatus.Unknown,PrinterStatuses.Interpret(null,null,null));
			Assert.AreEqual(PrinterOnlineStatus.Unknown,PrinterStatuses.Interpret(false,2,2));
			Assert.AreEqual("",PrinterStatuses.GetText(PrinterOnlineStatus.Unknown),"Nothing is shown where Windows reports no state.");
			Assert.AreEqual("Offline",PrinterStatuses.GetText(PrinterOnlineStatus.Offline));
		}

		[TestMethod]
		public void PrinterStatus_LookupIsCaseInsensitive_AndUnknownWhenMissing() {
			Dictionary<string,PrinterOnlineStatus> dictStatuses=new Dictionary<string,PrinterOnlineStatus>(StringComparer.OrdinalIgnoreCase) {
				{ PRINTER_FRONT,PrinterOnlineStatus.Online }
			};
			Assert.AreEqual(PrinterOnlineStatus.Online,PrinterStatuses.GetStatus(dictStatuses,PRINTER_FRONT.ToUpperInvariant()));
			Assert.AreEqual(PrinterOnlineStatus.Unknown,PrinterStatuses.GetStatus(dictStatuses,PRINTER_BACK));
			Assert.AreEqual(PrinterOnlineStatus.Unknown,PrinterStatuses.GetStatus(dictStatuses,""));
			Assert.AreEqual(PrinterOnlineStatus.Unknown,PrinterStatuses.GetStatus(null,PRINTER_FRONT));
		}

		[TestMethod]
		public void PrinterStatus_GetAll_NeverThrows() {
			Dictionary<string,PrinterOnlineStatus> dictStatuses=PrinterStatuses.GetAll();
			Assert.IsNotNull(dictStatuses);
			foreach(KeyValuePair<string,PrinterOnlineStatus> pair in dictStatuses) {
				Assert.IsFalse(string.IsNullOrEmpty(pair.Key));
			}
		}
		#endregion Rules, no database

		#region Database
		[TestMethod]
		public void PutForSit_KeepsARowForPrompt_AndRemovesAnEmptyOne() {
			TestDb.ResetOrSkip();
			long computerNum=InsertThisComputer();
			Printers.PutForSit(PrintSituation.Statement,ODEnvironment.MachineName,"",displayPrompt:true);
			Printer printer=Printers.GetOnePrinter(PrintSituation.Statement,computerNum);
			Assert.IsNotNull(printer,"A row must exist when Prompt is on, even with no printer chosen.");
			Assert.AreEqual("",printer.PrinterName);
			Assert.IsTrue(printer.DisplayPrompt);
			Printers.PutForSit(PrintSituation.Statement,ODEnvironment.MachineName,"",displayPrompt:false);
			Assert.IsNull(Printers.GetOnePrinter(PrintSituation.Statement,computerNum),"No printer, no prompt, not virtual: no row.");
			Printers.PutForSit(PrintSituation.Statement,ODEnvironment.MachineName,PRINTER_BACK,displayPrompt:false);
			Assert.AreEqual(PRINTER_BACK,Printers.GetOnePrinter(PrintSituation.Statement,computerNum).PrinterName);
		}

		[TestMethod]
		public void ResolveForThisComputer_ReadsThisComputersRows() {
			TestDb.ResetOrSkip();
			InsertThisComputer();
			Printers.PutForSit(PrintSituation.Default,ODEnvironment.MachineName,"Synthetic Not Installed Anywhere",displayPrompt:true);
			Printers.PutForSit(PrintSituation.Statement,ODEnvironment.MachineName,"",displayPrompt:false,isVirtual:true,fileExtension:"pdf");
			Printers.RefreshCache();
			PrinterChoice printerChoice=PrintSituationRules.ResolveForThisComputer(PrintSituation.Statement);
			Assert.AreEqual(PrinterChoiceSource.WindowsDefault,printerChoice.Source);
			Assert.IsFalse(printerChoice.DisplayPrompt,"The Statement row's prompt (off) applies, not the Default row's.");
			Assert.IsTrue(printerChoice.PrinterRow.IsVirtualPrinter);
			CollectionAssert.Contains(printerChoice.ListPrinterNamesNotInstalled,"Synthetic Not Installed Anywhere");
			printerChoice=PrintSituationRules.ResolveForThisComputer(PrintSituation.Claim);
			Assert.IsTrue(printerChoice.DisplayPrompt,"No Claim row: the Default row's prompt.");
		}

		private static long InsertThisComputer() {
			DataCore.NonQ("INSERT INTO computer (CompName,LastHeartBeat) VALUES('"+POut.String(ODEnvironment.MachineName)+"',NOW())");
			Computers.RefreshCache();
			return TestDb.GetLong("SELECT ComputerNum FROM computer WHERE CompName='"+POut.String(ODEnvironment.MachineName)+"'");
		}
		#endregion Database
	}
}
