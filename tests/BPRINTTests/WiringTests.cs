// B-PRINT: checks that the windows (OpenDental.exe and WPF windows, which the tests do not open) call the B-PRINT business rules. They read the
// source files.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BPRINTTests {
	[TestClass]
	public class WiringTests {
		private static string Read(string relativePath) {
			return File.ReadAllText(TestDb.FindRepoFile(relativePath));
		}

		private static int Count(string text,string value) {
			return Regex.Matches(text,Regex.Escape(value)).Count;
		}

		#region v24_4-manage-accounting-export and v25_3-misc-export-csv-txt
		[TestMethod]
		public void TransactionHistory_HasExportTxtAndExportCsv() {
			string source=Read(@"OpenDental\Forms\FormJournal.cs");
			StringAssert.Contains(source,"new ODToolBarButton(Lan.g(this,\"Export .txt\")");
			StringAssert.Contains(source,"new ODToolBarButton(Lan.g(this,\"Export .csv\")");
			StringAssert.Contains(source,"Export_Click(GridExportFormat.Txt);");
			StringAssert.Contains(source,"Export_Click(GridExportFormat.Csv);");
			StringAssert.Contains(source,"gridMain.ExportAs(gridMain.Title,gridExportFormat);");
			Assert.IsFalse(source.Contains("gridMain.Export(gridMain.Title)"),"The old single text export is gone.");
		}

		[TestMethod]
		public void ChartOfAccounts_UsesTheSharedExport() {
			string source=Read(@"OpenDental\Forms\FormAccounting.cs");
			StringAssert.Contains(source,"gridMain.ExportAs(gridMain.Title,gridExportFormat);");
			Assert.IsFalse(source.Contains("new StreamWriter("),"No private copy of the export routine.");
		}

		[TestMethod]
		public void GridWindows_OfferExportCsvAndExportTxt() {
			string[][] arrayCalls={
				new[] { @"OpenDental\Forms\FormTransactionEdit.cs","gridMain.ShowExportMenu(butExport,gridMain.Title);" },
				new[] { @"OpenDental\Forms\FormReconcileEdit.cs","gridMain.ShowExportMenu(butExport,gridMain.Title);" },
				new[] { @"OpenDental\Forms\FormEmployeeSelect.cs","gridMain.ShowExportMenu(butExport,\"Employees\");" },
				new[] { @"OpenDental\Forms\FormArManager.cs","gridUnsent.ShowExportMenu(butUnsentExport,\"TSI Unsent Accounts\");" },
				new[] { @"OpenDental\Forms\FormArManager.cs","gridSent.ShowExportMenu(butSentExport,\"TSI Sent Accounts\");" },
				new[] { @"OpenDental\Forms\FormArManager.cs","gridExcluded.ShowExportMenu(butExcludedExport,\"TSI Excluded Accounts\");" },
				new[] { @"OpenDental\Forms Reports\FormRpProcNote.cs","gridMain.ShowExportMenu(butExport,\"Incomplete Procedure Notes\");" },
				new[] { @"OpenDental\Forms Reports\FormRpProcedures.cs","gridMain.ShowExportMenu(butExport,\"Procedures\");" },
				new[] { @"OpenDental\Forms Reports\FormRpInsPendingSupplementals.cs","gridMain.ShowExportMenu(butExport,\"Insurance Pending Supplementals\");" },
			};
			foreach(string[] arrayCall in arrayCalls) {
				StringAssert.Contains(Read(arrayCall[0]),arrayCall[1],arrayCall[0]);
			}
			//No window still calls the old one-format export of a grid.
			foreach(string path in arrayCalls.Select(x => x[0]).Distinct()) {
				Assert.IsFalse(Regex.IsMatch(Read(path),@"\bgrid\w*\.Export\("),path);
			}
		}
		#endregion

		#region v25_3-manage-statement-email-lastname
		[TestMethod]
		public void EmailedStatements_AreNamedAfterTheLastName() {
			string[] arrayPaths={
				@"OpenDental\Forms\FormBilling.cs",
				@"OpenDental\Forms\FormStatementOptions.cs",
				@"OpenDentBusiness\Logic\BillingL.cs",
				@"OpenDentBusiness\Data Interface\EmailMessages.cs",
			};
			foreach(string path in arrayPaths) {
				string source=Read(path);
				StringAssert.Contains(source,"emailAttach.DisplayedFileName=Statements.GetEmailPdfDisplayName(patient);",path);
				Assert.IsFalse(source.Contains("DisplayedFileName=\"Statement.pdf\""),path);
			}
			StringAssert.Contains(Read(@"OpenDental\Forms\FormSheetFillEdit.cs"),
				"shortFileName=Path.GetFileNameWithoutExtension(Statements.GetEmailPdfDisplayName(patient));");
		}
		#endregion

		#region v25_3-manage-statement-barcode
		[TestMethod]
		public void BillingDefaults_HasTheBarcodeOption() {
			string designer=Read(@"OpenDental\Forms\FormBillingDefaults.Designer.cs");
			StringAssert.Contains(designer,"this.checkInserterBarcode.Text = \"Print inserter barcode on statements\";");
			StringAssert.Contains(designer,"this.Controls.Add(this.checkInserterBarcode);");
			string source=Read(@"OpenDental\Forms\FormBillingDefaults.cs");
			StringAssert.Contains(source,"checkInserterBarcode.Checked=StatementInserterBarcode.IsEnabled();");
			StringAssert.Contains(source,"| StatementInserterBarcode.SetEnabled(checkInserterBarcode.Checked))");
		}

		[TestMethod]
		public void Barcode_IsPrintedAndStamped() {
			string billing=Read(@"OpenDental\Forms\FormBilling.cs");
			int idxAttach=billing.IndexOf("StatementInserterBarcodeBatch.Attach(sendStatementsIO);");
			int idxSend=billing.IndexOf("BillingL.SendStatements(sendStatementsIO);");
			int idxStamp=billing.IndexOf("statementInserterBarcodeBatch.Stamp(sendStatementsIO);");
			int idxSave=billing.IndexOf("sendStatementsIO.PdfMasterDocument.Save(tempFileOutputDocument);");
			Assert.IsTrue(idxAttach>0 && idxAttach<idxSend && idxSend<idxStamp && idxStamp<idxSave,"Attach, send, stamp, then save the print document.");
			StringAssert.Contains(Read(@"OpenDental\SheetFramework\SheetPrintingJob.cs"),
				"StatementInserterBarcode.DrawOnPrintedPage(g,sheet,_sheetsPrinted+1,_pagesPrinted+1,Sheets.CalculatePageCount(sheet,_printMargin));");
		}

		[TestMethod]
		public void SheetEditor_DrawsTheBarcodeField_AndWarnsAboutTheClearZone() {
			string source=Read(@"OpenDental\Forms\FormSheetDefEdit.cs");
			StringAssert.Contains(source,"case StatementInserterBarcode.FIELD_NAME:");
			StringAssert.Contains(source,"DrawInserterBarcode(sheetFieldDef,g);");
			StringAssert.Contains(source,"if(!ConfirmInserterBarcodeClearZone()) {");
			string partial=Read(@"OpenDental\Forms\FormSheetDefEdit.InserterBarcode.cs");
			StringAssert.Contains(partial,"StatementInserterBarcode.GetOverlappingFields(SheetDef_)");
			StringAssert.Contains(Read(@"OpenDental\OpenDental.csproj"),"<Compile Include=\"Forms\\FormSheetDefEdit.InserterBarcode.cs\">");
		}
		#endregion

		#region v25_1-sheets-autosave-images
		[TestMethod]
		public void FillSheet_HasNoAutosaveBox_AndCopiesWhenTheSaveChangedTheSheet() {
			string source=Read(@"OpenDental\Forms\FormSheetFillEdit.cs");
			Assert.IsFalse(source.Contains("checkSaveToImages"));
			Assert.IsFalse(Read(@"OpenDental\Forms\FormSheetFillEdit.designer.cs").Contains("checkSaveToImages"));
			//E-mail, PDF, Print, Print/E-mail and OK each copy after their save.
			Assert.AreEqual(5,Count(source,"if(IsAutoSaveCopyDue()) {"));
			Assert.AreEqual(5,Count(source,"SaveAsDocument('U',\"PatientForm\");"));
			string partial=Read(@"OpenDental\Forms\FormSheetFillEdit.AutoSaveImages.cs");
			StringAssert.Contains(partial,"_sheetAutoSaveTracker.IsCopyDue(SheetCur,_isAutoSave,SheetAutoSaveImages.IsLayoutOptionOn(SheetCur))");
			StringAssert.Contains(partial,"protected override void OnShown(EventArgs e)");
			StringAssert.Contains(partial,"_sheetAutoSaveTracker.TakeSnapshot(SheetCur);");
			//A kiosk or terminal save copies too, and a failed copy shows the patient no error.
			int idxTerminal=partial.IndexOf("if(!isCopyDue || !IsInTerminal) {");
			int idxTry=partial.IndexOf("try {",idxTerminal);
			int idxCopy=partial.IndexOf("SaveAsDocument('U',\"PatientForm\");",idxTerminal);
			int idxCatch=partial.IndexOf("catch(Exception ex) {",idxTerminal);
			Assert.IsTrue(idxTerminal>0 && idxTry>idxTerminal && idxCopy>idxTry && idxCatch>idxCopy);
			StringAssert.Contains(Read(@"OpenDental\OpenDental.csproj"),"<Compile Include=\"Forms\\FormSheetFillEdit.AutoSaveImages.cs\">");
		}

		[TestMethod]
		public void SheetSetup_DescribesTheCopy() {
			StringAssert.Contains(Read(@"OpenDental\Forms\FormSheetDef.designer.cs"),"this.checkAutoSaveCheck.Text = \"Copy to Imaging when saved\";");
		}
		#endregion

		#region v25_3-sheets-checkbox-cleanup
		[TestMethod]
		public void EditCheckBox_HasNoLongInstructions_AndKeepsThemAsTooltips() {
			string designer=Read(@"OpenDental\Forms\FormSheetFieldCheckBox.designer.cs");
			Assert.IsFalse(Regex.IsMatch(designer,@"\blabel9\b"),"The misc radio button instructions label is gone.");
			Assert.IsFalse(Regex.IsMatch(designer,@"\blabel1\b"),"The radio button value instructions label is gone.");
			foreach(Match match in Regex.Matches(designer,"this\\.label\\w+\\.Text = \"([^\"]|\\\\\")*\"( \\+)?")) {
				Assert.IsFalse(match.Value.EndsWith("+"),"No label text runs over several lines: "+match.Value);
			}
			StringAssert.Contains(designer,"this.labelRequired.Text = \"Same for the whole group\";");
			StringAssert.Contains(designer,"this.labelMiscInstructions.Text = \"One Reportable Name per button\";");
			StringAssert.Contains(designer,"this.labelYesNo.Text = \"Pair with a \\\"No\\\" box\";");
			string partial=Read(@"OpenDental\Forms\FormSheetFieldCheckBox.Instructions.cs");
			foreach(string tip in new[] { "SetTip(groupRadioMisc,TIP_RADIO_MISC);","SetTip(groupRadio,TIP_RADIO);","SetTip(checkRequired,TIP_REQUIRED);",
				"SetTip(textReportableName,TIP_REPORTABLE);","SetTip(radioYes,TIP_YES_NO);","SetTip(radioNo,TIP_YES_NO);" })
			{
				StringAssert.Contains(partial,tip);
			}
			StringAssert.Contains(Read(@"OpenDental\OpenDental.csproj"),"<Compile Include=\"Forms\\FormSheetFieldCheckBox.Instructions.cs\">");
			Assert.IsFalse(Read(@"WpfControlsOD\Frms\FrmEFormCheckBoxEdit.xaml.cs").Contains("B-PRINT"),"The eForm checkbox window is left alone.");
		}
		#endregion

		#region v25_3-misc-printer-options
		[TestMethod]
		public void Printing_ShowsThePrintOptionsWindow_InsteadOfTheWindowsDialog() {
			string printerL=Read(@"OpenDental\Logic\PrinterL.cs");
			Assert.IsFalse(printerL.Contains("new PrintDialog()"),"No Windows print dialog.");
			StringAssert.Contains(printerL,"if((showPrompt || isPromptForced) && !isRemotePrint) {");
			StringAssert.Contains(printerL,"if(!ShowPrintOptions(printerSettings,printSituation,printerForSit)) {");
			StringAssert.Contains(printerL,"SecurityLogs.MakeLogEntry(EnumPermType.Printing,patNum,auditDescription);");
			StringAssert.Contains(printerL,"ApplyPageRange(printout.PrintDoc);");
			string printOptions=Read(@"OpenDental\Logic\PrinterL.PrintOptions.cs");
			StringAssert.Contains(printOptions,"FrmPrintOptions frmPrintOptions=new FrmPrintOptions();");
			StringAssert.Contains(printOptions,"PrintOptions.Remember(printSituation,printOptionsChoice);");
			StringAssert.Contains(printOptions,"PageRangePrintController.ApplyTo(printDocument);");
			StringAssert.Contains(Read(@"OpenDental\UI\FormPrintPreview.cs"),"PrinterL.TryPrint(_printout,isPromptForced:true)");
		}

		///<summary>Reports, User Query and RDL reports apply FromPage/ToPage in their own page handlers and reach the page-range controller through
		///PrinterL.TryPrint or PrinterL.SetPrinter. PageRangePrintController.ApplyTo puts the settings back to all pages, so the range is applied
		///once (PrintOptionsTests.PageRange_HandlerThatSkipsPagesItself_PrintsExactlyTheChosenPages).</summary>
		[TestMethod]
		public void PageRange_ReportsThatSkipPagesThemselves_GoThroughTheController() {
			string printerL=Read(@"OpenDental\Logic\PrinterL.cs");
			StringAssert.Contains(printerL,"ApplyPageRange(printout.PrintDoc);");
			StringAssert.Contains(printerL,"ApplyPageRange(printDocument);");
			string printOptions=Read(@"OpenDentBusiness\Misc\PrintOptions.cs");
			StringAssert.Contains(printOptions,"printDocument.PrinterSettings.PrintRange=PrintRange.AllPages;");
			string reportComplex=Read(@"OpenDental\ReportingComplex\FormReportComplex.cs");
			StringAssert.Contains(reportComplex,"ev.PageSettings.PrinterSettings.FromPage>=_currentPage");
			StringAssert.Contains(reportComplex,"PrinterL.TryPrint(_printout)");
			string query=Read(@"OpenDental\Forms Reports\FormQuery.cs");
			StringAssert.Contains(query,"ev.PageSettings.PrinterSettings.FromPage>=_currentPage");
			StringAssert.Contains(query,"PrinterL.TryPrint(ODprintout.CurPrintout)");
			StringAssert.Contains(Read(@"OpenDental\ReportingForRdl\FormReportForRdl.cs"),"PrinterL.SetPrinter(pd,PrintSituation.Default,0,");
			StringAssert.Contains(Read(@"OpenDental\ReportingForRdl\FormReportCustom.cs"),"PrinterL.SetPrinter(pd,PrintSituation.Default,0,");
			StringAssert.Contains(Read(@"RdlViewer\RdlViewer.cs"),"case PrintRange.SomePages:");
		}

		[TestMethod]
		public void WpfPrinting_UsesThePrintOptionsWindowToo() {
			StringAssert.Contains(Read(@"WpfControlsOD\Logic\PrinterL.cs"),"return PrintOptionsWpf.TryPrintFixedDocument(printout.FixedDocument_,printout.Descript);");
			//The remembered choice pre-fills the window only; a print without the window uses the rules' printer and one copy
			//(PrintOptionsTests.UnpromptedPrint_UsesTheRulesPrinterWithOneCopy_NeverTheRememberedChoice).
			string printOptionsWpf=Read(@"WpfControlsOD\Logic\PrintOptionsWpf.cs");
			StringAssert.Contains(printOptionsWpf,"bool isWindowShown=isPrompt || isPromptForced;");
			StringAssert.Contains(printOptionsWpf,"PrintOptions.GetChoiceToPrint(printSituation,printerNameFromRules,isWindowShown,listPrinters);");
			Assert.IsFalse(printOptionsWpf.Contains("GetInitialChoice(") || printOptionsWpf.Contains("GetRemembered("),
				"WPF printing gets the remembered choice only through GetChoiceToPrint.");
			string preview=Read(@"WpfControlsOD\Frms\FrmPrintPreview.xaml.cs");
			StringAssert.Contains(preview,"CommandManager.AddPreviewExecutedHandler(documentViewer,documentViewer_PreviewExecuted);");
			StringAssert.Contains(preview,"isPromptForced:true");
			string xaml=Read(@"WpfControlsOD\Frms\FrmPrintOptions.xaml");
			foreach(string name in new[] { "comboPrinter","textCopies","checkCollate","radioAll","radioRange","textFromPage","textToPage","butPrint","butCancel" }) {
				StringAssert.Contains(xaml,"x:Name=\""+name+"\"");
			}
			string csproj=Read(@"WpfControlsOD\WpfControlsOD.csproj");
			StringAssert.Contains(csproj,"<Page Include=\"Frms\\FrmPrintOptions.xaml\">");
			StringAssert.Contains(csproj,"<Compile Include=\"Logic\\PrintOptionsWpf.cs\" />");
		}
		#endregion
	}
}
