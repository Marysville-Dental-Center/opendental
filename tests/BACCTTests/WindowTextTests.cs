// Window text items of B-ACCT, checked in the source because the tests never open the program's windows:
//  - v25_2-account-prodselect-amount-unpaid: Select Production's "Amount End" column header reads "Amount Unpaid".
//  - v25_3-account-orthocase-cleanup: the Ortho Case window no longer carries the long instruction texts; the help moves to a tooltip.
//  - v24_4-reports-dailypayments-merchant-fees: Daily Payments has one "Show merchant fees" box instead of the CareCredit and PayConnect boxes.
//  - v24_4-account-salestax-proc: Quick Procs shows Sales Tax first, and Preferences has the Sales Tax procedure code box.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BACCTTests {
	[TestClass]
	public class WindowTextTests {
		private static string ReadSource(params string[] arrayParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		[TestMethod]
		public void SelectProduction_HeaderReadsAmountUnpaid() {
			string source=ReadSource("OpenPmsDental","Forms","FormProdSelect.cs");
			StringAssert.Contains(source,"\"Amount\\r\\nUnpaid\"");
			Assert.IsFalse(source.Contains("\"Amount\\r\\nEnd\""),"The old header is gone.");
			StringAssert.Contains(source,".AmountEnd.ToString(\"C\")","Only the label changes: the column still shows AmountEnd.");
		}

		[TestMethod]
		public void OrthoCase_LongInstructionTextsAreRemoved() {
			string designer=ReadSource("OpenPmsDental","Forms","FormOrthoCase.designer.cs");
			string resx=ReadSource("OpenPmsDental","Forms","FormOrthoCase.resx");
			Assert.IsFalse(designer.Contains("label2"),"The numbered steps label is gone.");
			Assert.IsFalse(designer.Contains("label4"),"The purpose paragraph label is gone.");
			Assert.IsFalse(resx.Contains("label2.Text"));
			Assert.IsFalse(resx.Contains("label4.Text"));
			StringAssert.Contains(designer,"toolTipOrthoCase.SetToolTip(this.labelBandingProc");
			StringAssert.Contains(designer,"this.ClientSize = new System.Drawing.Size(1019, 410);","The window closes the gaps the texts left.");
		}

		[TestMethod]
		public void DailyPayments_HasOneMerchantFeeBox() {
			string designer=ReadSource("OpenPmsDental","Forms Reports","FormRpPaySheet.designer.cs");
			string form=ReadSource("OpenPmsDental","Forms Reports","FormRpPaySheet.cs");
			StringAssert.Contains(designer,"\"Show merchant fees\"");
			Assert.IsFalse(designer.Contains("checkShowCareCreditFees"));
			Assert.IsFalse(designer.Contains("checkShowPayConnectFees"));
			Assert.IsFalse(form.Contains("CareCredit Fee"));
			Assert.IsFalse(form.Contains("PayConnect Fee"));
			StringAssert.Contains(form,"\"Total Merchant Fees:\"");
			StringAssert.Contains(form,"\"Total Patient Payments After Fees:\"");
			StringAssert.Contains(form,"\"Total All Payments After Fees:\"");
		}

		[TestMethod]
		public void QuickProcs_AndPreferences_OfferSalesTax() {
			string account=ReadSource("OpenPmsDental","Main Modules","ControlAccount.SalesTax.cs");
			string accountMain=ReadSource("OpenPmsDental","Main Modules","ControlAccount.cs");
			string prefs=ReadSource("OpenPmsDental","User Controls","Preferences","UserControlAccountAdjustments.cs");
			string prefsDesigner=ReadSource("OpenPmsDental","User Controls","Preferences","UserControlAccountAdjustments.Designer.cs");
			StringAssert.Contains(account,"contextMenuQuickProcs.MenuItems.Add(0,new MenuItem(Lan.g(this,\"Sales Tax\")");
			StringAssert.Contains(account,"EnumPermType.AccountProcsQuickAdd");
			StringAssert.Contains(account,"EnumPermType.ProcComplCreate");
			StringAssert.Contains(accountMain,"AddQuickProcsSalesTaxItem();");
			StringAssert.Contains(accountMain,"-_countQuickProcsLeadingItems]","Quick charge definitions still map to the right menu item.");
			StringAssert.Contains(prefs,"SalesTaxProcs.IsValidSetting(textSalesTaxProcCode.Text)");
			StringAssert.Contains(prefs,"SalesTaxProcs.SaveProcCodeSetting(textSalesTaxProcCode.Text)");
			StringAssert.Contains(prefsDesigner,"\"Sales Tax procedure code\"");
		}
	}
}
