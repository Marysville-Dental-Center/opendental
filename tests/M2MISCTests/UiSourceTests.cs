// Source checks for the M2-MISC windows that the tests cannot open (the desktop client is never launched): About (diagnostics, attribution kept),
// Printer Setup and Edit Printer, Edit Referral, and the automation condition windows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2MISCTests {
	[TestClass]
	public class UiSourceTests {
		#region About and Diagnostics
		[TestMethod]
		public void About_DiagnosticsAreLocal_AndTheAttributionStays() {
			string code=TestDb.ReadRepoFile("OpenPmsDental","Forms","FormAbout.cs");
			StringAssert.Contains(TaskAttachmentWindowTests.GetMethod(code,"private void butDiagnostics_Click("),
				"LocalDiagnostics.GetDiagnostics(FormOpenDental.PatNumCur)");
			StringAssert.Contains(code,"labelAttribution.Text=AboutBoxInfo.Attribution;");
			StringAssert.Contains(code,"\"Open PMS - Dental is a fork of Open Dental (c) Open Dental Software, Inc. and contributors, licensed under \"");
		}

		[TestMethod]
		public void Diagnostics_SendNothing() {
			string code=TestDb.ReadRepoFile("OpenPmsDentalBusiness","Misc","LocalDiagnostics.cs");
			foreach(string forbidden in new[] { "http://","https://","WebClient","HttpClient","WebRequest","WebServiceMainHQ","Socket","IsODHQ" }) {
				Assert.IsFalse(code.Contains(forbidden),"LocalDiagnostics.cs contains "+forbidden);
			}
		}
		#endregion About and Diagnostics

		#region Printers
		[TestMethod]
		public void PrinterSetup_ShowsPrinterUsedRuleAndStatus() {
			string code=TestDb.ReadRepoFile("OpenPmsDental","Forms","FormPrinterSetup.cs");
			StringAssert.Contains(code,"new GridColumn(\"Printer Used\",");
			StringAssert.Contains(code,"new GridColumn(\"Rule\",");
			StringAssert.Contains(code,"new GridColumn(\"Status\",");
			StringAssert.Contains(code,"PrintSituationRules.ResolveForThisComputer(printSituation)");
			StringAssert.Contains(code,"PrinterStatuses.GetAll()");
			//Every row has the same number of cells as there are columns, also a situation without a row.
			string method=TaskAttachmentWindowTests.GetMethod(code,"public void FillGrid(");
			Assert.AreEqual(8,Regex.Matches(method,@"gridPrinters\.Columns\.Add\(col\);").Count);
			Assert.AreEqual(9,Regex.Matches(method,@"row\.Cells\.Add\(").Count,"1 situation + 4 row cells + 4 blanks");
			Assert.AreEqual(3,Regex.Matches(TaskAttachmentWindowTests.GetMethod(code,"private void FillPrinterUsedCells("),@"row\.Cells\.Add\(").Count);
		}

		[TestMethod]
		public void EditPrinter_KeepsAPrinterThatIsNotInstalled_AndShowsItsState() {
			string code=TestDb.ReadRepoFile("OpenPmsDental","Forms","FormPrinterEdit.cs");
			StringAssert.Contains(code,"_printerNameNotInstalled=printerName;");
			StringAssert.Contains(TaskAttachmentWindowTests.GetMethod(code,"private void butSave_Click("),"printerName=GetPrinterNameSelected();");
			StringAssert.Contains(code,"labelStatus.Text=");
			string designer=TestDb.ReadRepoFile("OpenPmsDental","Forms","FormPrinterEdit.Designer.cs");
			StringAssert.Contains(designer,"this.Controls.Add(this.labelStatus);");
			StringAssert.Contains(designer,"this.Controls.Add(this.labelPrinterRule);");
		}
		#endregion Printers

		#region Referral
		[TestMethod]
		public void ReferralEdit_OnePhoneBox_ThirtyCharacters_NoTenDigitCheck() {
			string xaml=TestDb.ReadRepoFile("WpfControlsOD","Frms","FrmReferralEdit.xaml");
			StringAssert.Contains(xaml,"x:Name=\"textPhone\"");
			StringAssert.Contains(xaml,"MaxLength=\"30\"");
			foreach(string removed in new[] { "textPhone1","textPhone2","textPhone3" }) {
				Assert.IsFalse(xaml.Contains(removed),removed);
			}
			string code=TestDb.ReadRepoFile("WpfControlsOD","Frms","FrmReferralEdit.xaml.cs");
			Assert.IsFalse(code.Contains("\"Invalid phone\""),"The 10-digit check is back.");
			Assert.IsFalse(code.Contains("phone.Length < 10"));
			StringAssert.Contains(code,"textPhone.Text=ReferralPhones.GetDisplay(ReferralCur.Telephone);");
			StringAssert.Contains(code,"string phone=ReferralPhones.GetStorage(textPhone.Text);");
			StringAssert.Contains(code,"textPhone.TextChanged+=PatientL.ValidPhone_TextChanged;");
		}
		#endregion Referral

		#region Automations
		[TestMethod]
		public void ConditionEdit_ListsTheClinicMismatchFieldOnlyForProcedureComplete() {
			string code=TestDb.ReadRepoFile("OpenPmsDental","Forms","FormAutomationConditionEdit.cs");
			string method=TaskAttachmentWindowTests.GetMethod(code,"private bool IsFieldListed(");
			StringAssert.Contains(method,"AutomationClinicMismatches.IsTriggerAllowed(AutomationTriggerCur.Value)");
			//The list index is no longer the enum value.
			Assert.IsFalse(code.Contains("(AutoCondField)listCompareField.SelectedIndex"),"Cast of the list index to the field is back.");
			//No comparison and no text are saved for it.
			string save=TaskAttachmentWindowTests.GetMethod(code,"private void butSave_Click(");
			StringAssert.Contains(save,"GetFieldSelected()==AutoCondField.PatientProcedureClinicMismatch");
			StringAssert.Contains(save,"AutomationConditionCur.Comparison=AutoCondComparison.None;");
			StringAssert.Contains(TaskAttachmentWindowTests.GetMethod(code,"private void ShowOrHideFields("),"AutoCondField.PatientProcedureClinicMismatch");
		}

		[TestMethod]
		public void AutomationEdit_PassesTheTrigger_AndRefusesTheWrongTrigger() {
			string code=TestDb.ReadRepoFile("OpenPmsDental","Forms","FormAutomationEdit.cs");
			Assert.AreEqual(2,Regex.Matches(code,@"\.AutomationTriggerCur=GetTriggerSelected\(\);").Count,"Add and edit");
			StringAssert.Contains(TaskAttachmentWindowTests.GetMethod(code,"private void butSave_Click("),"AutomationClinicMismatches.GetSaveError(");
		}
		#endregion Automations
	}
}
