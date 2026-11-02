// Source checks for the window wiring that the tests cannot drive without the GUI: the Payment window hooks, the Invoice drop-down and the
// preference checkbox. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2PAYWTests {
	[TestClass]
	public class WiringTests {
		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		[TestMethod]
		public void PaymentWindow_SavingAnIncomeTransferChecksTheLimit() {
			string source=Read("OpenPmsDental","Forms","FormPayment.cs");
			int indexSave=source.IndexOf("private bool SavePaymentToDb()");
			int indexBranch=source.IndexOf("paymentTypeParam=\"\";",indexSave);
			int indexCheck=source.IndexOf("if(!IncomeTransferLimit_IsValid()) {",indexSave);
			int indexElse=source.IndexOf("else {//Not an income transfer",indexSave);
			Assert.IsTrue(indexSave>0 && indexBranch>indexSave,"SavePaymentToDb's income transfer branch was not found.");
			Assert.IsTrue(indexCheck>indexBranch && indexCheck<indexElse,"The limit is checked inside the income transfer branch.");
			//The automatic Transfer button's result is remembered right after it replaces the splits.
			int indexAuto=source.IndexOf("PaymentEdit.TryCreateIncomeTransfer(constructResults.ListAccountEntries");
			int indexRemember=source.IndexOf("IncomeTransferLimit_RememberAutomaticSplits();",indexAuto);
			int indexFailed=source.IndexOf("new MsgBoxCopyPaste(incomeTransferData.StringBuilderErrors",indexAuto);
			Assert.IsTrue(indexAuto>0 && indexRemember>indexAuto && indexRemember<indexFailed,"Remembered in the success branch only.");
		}

		[TestMethod]
		public void PaymentWindow_InvoiceFilterIsFilledAndApplied() {
			string source=Read("OpenPmsDental","Forms","FormPayment.cs");
			Match matchFill=Regex.Match(source,@"private void FillFiltersChargesTreatPlan\(bool doPreserveValues\) \{(?<body>.*?)\n\t\t\}",RegexOptions.Singleline);
			Assert.IsTrue(matchFill.Success);
			StringAssert.Contains(matchFill.Groups["body"].Value,"FillFilterInvoices(doPreserveValues);");
			Match matchGrid=Regex.Match(source,@"private void FillGridCharges\(\) \{(?<body>.*?)#region Group By Provider",RegexOptions.Singleline);
			Assert.IsTrue(matchGrid.Success);
			StringAssert.Contains(matchGrid.Groups["body"].Value,
				"listAccountEntriesOutstandingCharges=FilterAccountEntriesByInvoice(listAccountEntriesOutstandingCharges);");
		}

		[TestMethod]
		public void PaymentWindow_InvoiceDropDownSitsInTheOutstandingFilterGroup() {
			string designer=Read("OpenPmsDental","Forms","FormPayment.designer.cs");
			StringAssert.Contains(designer,"this.groupBoxFilteringOutstanding.Controls.Add(this.comboInvoiceOutstandingFilter);");
			StringAssert.Contains(designer,"this.groupBoxFilteringOutstanding.Controls.Add(this.labelInvoiceOutstandingFilter);");
			StringAssert.Contains(designer,"this.labelInvoiceOutstandingFilter.Text = \"Invoice:\";");
			StringAssert.Contains(designer,
				"this.comboInvoiceOutstandingFilter.SelectionChangeCommitted += new System.EventHandler(this.FilterChangeCommittedChargesTreatPlan);");
			Assert.IsFalse(designer.Contains("this.comboInvoiceOutstandingFilter.SelectionModeMulti = true;"),"One invoice at a time.");
			//The group grew by one row and the charges tab moved down below it.
			StringAssert.Contains(designer,"this.groupBoxFilteringOutstanding.Size = new System.Drawing.Size(603, 103);");
			StringAssert.Contains(designer,"this.tabControlCharges.Location = new System.Drawing.Point(498, 107);");
		}

		[TestMethod]
		public void Preferences_CheckboxMatchesItsPrefInfosEntryAndSavesWithASetupLog() {
			XDocument xDocument=XDocument.Parse(Read("OpenPmsDental","Resources","PrefInfos.xml"));
			XElement xElement=xDocument.Descendants("PrefInf").Single(x => (string)x.Element("PrefName")=="IncomeTransfersLimitToPatPayments");
			Assert.AreEqual("AccountPayments",(string)xElement.Element("Category"));
			string controlName=(string)xElement.Element("ControlName");
			string controlText=(string)xElement.Element("ControlText");
			Assert.AreEqual("checkIncomeTransfersLimitToPatPayments",controlName);
			string designer=Read("OpenPmsDental","User Controls","Preferences","UserControlAccountPayments.Designer.cs");
			StringAssert.Contains(designer,"this.groupBoxPayments.Controls.Add(this."+controlName+");");
			StringAssert.Contains(designer,"this."+controlName+".Text = \""+controlText+"\";");
			Assert.AreEqual("Limit manual income transfers to the amount of patient payments",controlText,"The label the release note gives.");
			string code=Read("OpenPmsDental","User Controls","Preferences","UserControlAccountPayments.cs");
			StringAssert.Contains(code,controlName+".Checked=PrefC.GetBool(PrefName.IncomeTransfersLimitToPatPayments);");
			StringAssert.Contains(code,"Prefs.UpdateBool(PrefName.IncomeTransfersLimitToPatPayments,"+controlName+".Checked)");
			StringAssert.Contains(code,"SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,");
		}
	}
}
