// Source checks for the Service Date View hooks in the Windows Forms code (package M2-SDV). The windows themselves are never shown in tests:
// these checks make sure the hooks that connect them to the business layer stay in place when other packages edit the same files.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2SDVTests {
	[TestClass]
	public class ServiceDateViewUiSourceTests {
		private static string ReadSource(string relativePath) {
			return File.ReadAllText(TestDb.FindRepoFile(relativePath));
		}

		private static int CountOf(string text,string value) {
			return Regex.Matches(text,Regex.Escape(value)).Count;
		}

		[TestMethod]
		public void ControlAccount_HooksTheServiceDateView() {
			string source=ReadSource(Path.Combine("OpenDental","Main Modules","ControlAccount.cs"));
			Assert.AreEqual(1,CountOf(source,"ConstructServiceDateView();"),"Constructor.");
			Assert.AreEqual(1,CountOf(source,"InitializeServiceDateView();"),"InitializeOnStartup.");
			Assert.AreEqual(1,CountOf(source,"FillServiceDateView();"),"End of FillMain.");
			Assert.AreEqual(3,CountOf(source,"ApplyServiceDateViewToNewStatement(statement);"),"PrintStatement, More Options and Invoice.");
			StringAssert.Contains(source,"ToggleServiceDateView();");
			//The proc breakdown setting is read and written in its own row (Fkey 0): the Service Date View settings share its FkeyType.
			Assert.IsFalse(source.Contains("GetByUserAndFkeyType(Security.CurUser.UserNum,UserOdFkeyType.AcctProcBreakdown)"));
			Assert.IsFalse(source.Contains("GetFirstOrNewByUserAndFkeyType(Security.CurUser.UserNum,UserOdFkeyType.AcctProcBreakdown)"));
			string sourcePartial=ReadSource(Path.Combine("OpenDental","Main Modules","ControlAccount.ServiceDateView.cs"));
			StringAssert.Contains(sourcePartial,"contextMenuStatement.MenuItems.Add(");
			StringAssert.Contains(sourcePartial,"contextMenuAcctGrid.MenuItems.Add(");
			StringAssert.Contains(sourcePartial,"_formRpServiceDateView.SetPatient(patNum,isFamily);");
			string sourceProject=ReadSource(Path.Combine("OpenDental","OpenDental.csproj"));
			StringAssert.Contains(sourceProject,@"Main Modules\ControlAccount.ServiceDateView.cs");
		}

		[TestMethod]
		public void StatementOptions_SavesTheServiceDateViewBox() {
			string source=ReadSource(Path.Combine("OpenDental","Forms","FormStatementOptions.cs"));
			StringAssert.Contains(source,"checkServiceDateView.Checked=StatementCur.IsServiceDateView;");
			StringAssert.Contains(source,"Statements.SetServiceDateView(StatementCur,checkServiceDateView.Checked);");
			StringAssert.Contains(source,"Statements.SetServiceDateView(ListStatements[i],checkServiceDateView.Checked);");
			string sourceDesigner=ReadSource(Path.Combine("OpenDental","Forms","FormStatementOptions.Designer.cs"));
			StringAssert.Contains(sourceDesigner,"this.Controls.Add(this.checkServiceDateView);");
		}

		[TestMethod]
		public void BillingOptions_DefaultFromThePreferenceAndSavedOnStatements() {
			string source=ReadSource(Path.Combine("OpenDental","Forms","FormBillingOptions.cs"));
			StringAssert.Contains(source,"checkServiceDateView.Checked=Statements.GetBillingDefaultServiceDateView();");
			StringAssert.Contains(source,"Prefs.UpdateBool(PrefName.BillingDefaultServiceDateView,checkServiceDateView.Checked)");
			StringAssert.Contains(source,"Statements.SetServiceDateView(statement,checkServiceDateView.Checked);");
			string sourceDesigner=ReadSource(Path.Combine("OpenDental","Forms","FormBillingOptions.Designer.cs"));
			StringAssert.Contains(sourceDesigner,"this.Controls.Add(this.checkServiceDateView);");
		}

		[TestMethod]
		public void StatementMainGrid_UsesTheServiceDateViewWhenTheStatementSaysSo() {
			string source=ReadSource(Path.Combine("OpenDentBusiness","SheetFramework","SheetDataTableUtil.cs"));
			int idxMethod=source.IndexOf("public static DataTable GetTable_StatementMain(");
			int idxHook=source.IndexOf("Statements.IsShownInServiceDateView(stmt)");
			Assert.IsTrue(idxMethod>0 && idxHook>idxMethod && idxHook-idxMethod<300,"First thing the main grid table does.");
		}

		[TestMethod]
		public void DisplayFieldCategories_ListTheServiceDateViewCategory() {
			//FormDisplayFieldCategories lists every DisplayFieldCategory except None, so category 22 appears with its description.
			string source=ReadSource(Path.Combine("OpenDental","Forms","FormDisplayFieldCategories.cs"));
			StringAssert.Contains(source,"Enum.GetValues(typeof(DisplayFieldCategory))");
			Assert.IsFalse(source.Contains("AccountModuleServiceDateView"),"Not filtered out.");
			//B-ERA's filter lists a category only once it has fields (placeholders for later versions are hidden): 22 has them (M2-02).
			StringAssert.Contains(source,"DisplayFields.HasAvailableFields(");
			Assert.IsTrue(DisplayFields.HasAvailableFields(DisplayFieldCategory.AccountModuleServiceDateView),"Listed.");
			Assert.AreEqual(22,(int)DisplayFieldCategory.AccountModuleServiceDateView);
			Assert.AreEqual("Account Module Service Date View",DisplayFieldCategory.AccountModuleServiceDateView.GetDescription());
			Assert.IsFalse(CodeBase.EnumTools.GetAttributeOrDefault<PermissionAttribute>(DisplayFieldCategory.AccountModuleServiceDateView).IsCEMT);
		}

		[TestMethod]
		public void ServiceDateViewWindow_ColumnsFromDisplayFieldsAndDetailPerUser() {
			string source=ReadSource(Path.Combine("OpenDental","Forms Reports","FormRpServiceDateView.cs"));
			StringAssert.Contains(source,"DisplayFieldCategory.AccountModuleServiceDateView");
			StringAssert.Contains(source,"ServiceDateViewUserPrefs.FKEY_SHOW_DETAILS");
			StringAssert.Contains(source,"public void SetPatient(long patNum,bool isFamily)");
			//Every column the Display Fields window offers has a cell.
			foreach(DisplayField displayField in DisplayFields.GetAllAvailableList(DisplayFieldCategory.AccountModuleServiceDateView)) {
				StringAssert.Contains(source,"case \""+displayField.InternalName+"\":",displayField.InternalName);
			}
		}
	}
}
