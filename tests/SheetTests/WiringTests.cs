// Checks that the windows (OpenDental.exe, which the tests cannot load) use the A-SHEET business rules. They read the source files.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SheetTests {
	[TestClass]
	public class WiringTests {
		private static string Read(string relativePath) {
			return File.ReadAllText(TestDb.FindRepoFile(relativePath));
		}

		[TestMethod]
		public void TransactionEdit_PrintsChecksFromAssetAccounts() {
			string source=Read(@"OpenDental\Forms\FormTransactionEdit.cs");
			StringAssert.Contains(source,"butPrintCheck.Visible=CheckSheets.CanPrintFrom(_accountOfOrigin);");
			StringAssert.Contains(source,"CheckSheets.GetPrintError(_accountOfOrigin,GetEntriesOnScreen())");
			StringAssert.Contains(source,"SheetPrinting.PrintCheck(sheet,CheckSheets.GetAuditDescription(journalEntryCheck));");
			StringAssert.Contains(source,"\"Did the check print correctly?\"");
			StringAssert.Contains(Read(@"OpenDental\Forms\FormTransactionEdit.designer.cs"),"this.butPrintCheck.Click += new System.EventHandler(this.butPrintCheck_Click);");
		}

		[TestMethod]
		public void SheetSetup_OffersTheCheckType() {
			string sourceSheetDef=Read(@"OpenDental\Forms\FormSheetDef.cs");
			StringAssert.Contains(sourceSheetDef,"!x.In(SheetTypeEnum.None,SheetTypeEnum.MedLabResults)");
			Assert.IsFalse(sourceSheetDef.Contains("SheetTypeEnum.Check"),"Check is no longer kept out of the sheet type list.");
			StringAssert.Contains(Read(@"OpenDental\Forms\FormSheetDefEdit.cs"),"CheckSheets.FilterFieldTypes(listSheetFieldTypes)");
			StringAssert.Contains(Read(@"OpenDental\Forms\FormSheetFieldStatic.cs"),"CheckSheets.IsStaticTextFieldAllowed(x)");
		}

		[TestMethod]
		public void ClaimForms_KnowTheHospitalizationFields() {
			foreach(string fieldName in new[] { "HospitalizationDateFrom","HospitalizationDateTo" }) {
				StringAssert.Contains(Read(@"OpenDental\Forms\FormClaimFormItemEdit.cs"),"\""+fieldName+"\",");
				StringAssert.Contains(Read(@"OpenDental\Forms\FormClaimFormEdit.cs"),"case \""+fieldName+"\":");
				StringAssert.Contains(Read(@"OpenDental\Forms\FormClaimPrint.cs"),"case \""+fieldName+"\":");
			}
			StringAssert.Contains(Read(@"OpenDental\Forms\FormClaimPrint.cs"),"EhrPatients.GetHospitalizationDates(_claim,patient,");
		}
	}
}
