// Stage 0 of the .NET 10 move (S0-2a, docs/specs/net10-plan.md): QuickBooks Desktop (Intuit's QBFC interop, a CLR 1.1 file) is removed and
// QuickBooks Online stays. A database still set to QuickBooks Desktop works as Open PMS - Dental, and its preference value stays stored.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BINBTests {
	[TestClass]
	public class QuickBooksDesktopRemovedTests {
		[TestMethod]
		public void AccountingSoftware_KeepsItsNumbers() {
			//The AccountingSoftware preference stores the number, so QuickBooks (Desktop) keeps its place.
			Assert.AreEqual(0,(int)AccountingSoftware.OpenDental);
			Assert.AreEqual(1,(int)AccountingSoftware.QuickBooks);
			Assert.AreEqual(2,(int)AccountingSoftware.QuickBooksOnline);
		}

		[TestMethod]
		public void StoredQuickBooksDesktop_WorksAsOpenPmsDental() {
			Assert.AreEqual(AccountingSoftware.OpenDental,Accounts.GetAccountingSoftwareInUse(AccountingSoftware.QuickBooks));
			Assert.AreEqual(AccountingSoftware.OpenDental,Accounts.GetAccountingSoftwareInUse(AccountingSoftware.OpenDental));
			Assert.AreEqual(AccountingSoftware.QuickBooksOnline,Accounts.GetAccountingSoftwareInUse(AccountingSoftware.QuickBooksOnline));
		}

		[TestMethod]
		public void QuickBooksDesktopFiles_AreGone() {
			foreach(string relative in new[] { @"Required dlls\Interop.QBFC10.dll",@"WpfControlsOD\Bridges\QuickBooks.cs",
				@"OpenPmsDental\Forms\FormQuickBooksSetup.cs",@"OpenPmsDental\Forms\FormQuickBooksSetup.designer.cs",@"OpenPmsDental\Forms\FormQuickBooksSetup.resx" })
			{
				Assert.IsFalse(File.Exists(Path.Combine(TestPaths.RepoRoot,relative)),"Still in the repository: "+relative);
			}
			foreach(string project in new[] { @"OpenPmsDental\OpenPmsDental.csproj",@"WpfControlsOD\WpfControlsOD.csproj" }) {
				string text=TestPaths.ReadSource(project);
				StringAssert.DoesNotMatch(text,new System.Text.RegularExpressions.Regex("QBFC|FormQuickBooksSetup|Bridges\\\\QuickBooks\\.cs"),project);
			}
		}

		[TestMethod]
		public void AccountingWindow_OffersQuickBooksOnlineOnly() {
			string formAccounting=TestPaths.ReadSource("OpenPmsDental","Forms","FormAccounting.cs");
			Assert.IsFalse(formAccounting.Contains("menuItemSetup.Add(\"QuickBooks\","),"The Setup > QuickBooks (Desktop) menu item is gone.");
			StringAssert.Contains(formAccounting,"menuItemSetup.Add(\"QuickBooks Online\", menuItemQuickBooksOnline_Click);");
		}

		[TestMethod]
		public void DepositSoftwareList_NoLongerOffersQuickBooksDesktop() {
			string source=TestPaths.ReadSource("OpenPmsDental","User Controls","Preferences","UserControlManageGeneral.cs");
			Assert.IsFalse(source.Contains("comboDepositSoftware.Items.AddEnums<AccountingSoftware>()"),"Every enum member would include QuickBooks Desktop.");
			StringAssert.Contains(source,
				"comboDepositSoftware.Items.AddListEnum(new List<AccountingSoftware>() { AccountingSoftware.OpenDental,AccountingSoftware.QuickBooksOnline });");
			StringAssert.Contains(source,"_accountingSoftwareShown=Accounts.GetAccountingSoftwareInUse();");
			//The stored value is only written when another deposit software is picked, so a stored QuickBooks Desktop value stays.
			StringAssert.Contains(source,"if(accountingSoftwareSelected!=_accountingSoftwareShown) {");
		}

		[TestMethod]
		public void DepositWindow_HasNoQuickBooksDesktopMode() {
			string source=TestPaths.ReadSource("OpenPmsDental","Forms","FormDepositEdit.cs");
			foreach(string removed in new[] { "IsQuickBooks()","CreateDepositQB(","QuickBooks.CreateDeposit","PrefName.QuickBooksClassRefs","new FormQBAccountSelect(false)" }) {
				Assert.IsFalse(source.Contains(removed),"FormDepositEdit still has "+removed);
			}
			StringAssert.Contains(source,"_accountingSoftware=Accounts.GetAccountingSoftwareInUse();");
			string accounts=TestPaths.ReadSource("OpenPmsDentalBusiness","Data Interface","Accounts.cs");
			Assert.IsFalse(accounts.Contains("prefAccountingSoftware==(int)AccountingSoftware.QuickBooks)"),"DepositsLinked has no QuickBooks Desktop branch.");
			string accountPick=TestPaths.ReadSource("OpenPmsDental","Forms","FormAccountPick.cs");
			Assert.IsFalse(accountPick.Contains("IsQuickBooks"),"FormAccountPick has no QuickBooks Desktop mode.");
		}
	}
}
