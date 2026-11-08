// The window code of the four 26.2 Reports items, checked from its source (the windows need a screen): the one list of the Report Setup tabs and
// what it keeps, the Save of Report Setup, the option and the column of the Patient Portion Uncollected window and its print; and that this
// package adds no fork migration step.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness.ForkMigrations;

namespace P262ReportsTests {
	[TestClass]
	public class WindowSourceTests {
		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(ReportsDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		[TestMethod]
		public void ReportSetup_OneListInsteadOfAGridPerCategory() {
			string designer=Read("OpenPmsDental","User Controls","UserControlReportSetup.Designer.cs");
			foreach(string gridOld in new[] { "gridProdInc","gridDaily","gridMonthly","gridLists","gridPublicHealth","labelODInternal" }) {
				Assert.IsFalse(designer.Contains(gridOld),gridOld+" is gone.");
			}
			Assert.AreEqual(1,CountOf(designer,"new OpenPmsDental.UI.GridOD()"),"One list.");
			StringAssert.Contains(designer,"this.gridReports.SelectionMode = OpenPmsDental.UI.GridSelectionMode.OneCell;");
			//The controls of both tabs are kept.
			foreach(string control in new[] { "butUp","butDown","butSetAll","butSetNone","comboUserGroup","labelUserGroup" }) {
				StringAssert.Contains(designer,"this."+control+" = new ",control);
			}
			StringAssert.Contains(designer,"this.Size = new System.Drawing.Size(525, 607);","The tabs and the CEMT window keep their size.");
		}

		[TestMethod]
		public void ReportSetup_KeepsEverySettingAndThePublicMembers() {
			string source=Read("OpenPmsDental","User Controls","UserControlReportSetup.cs");
			//Members Report Setup and the Central Enterprise Management Tool use.
			StringAssert.Contains(source,"public List<DisplayReport> ListDisplayReportAll;");
			StringAssert.Contains(source,"public List<GroupPermission> ListGroupPermissionsForReports;");
			StringAssert.Contains(source,"public List<GroupPermission> ListGroupPermissionsOld;");
			StringAssert.Contains(source,"public void InitializeOnStartup(bool refreshData,long userGroupNum,bool isPermissionMode,bool isCEMT=false)");
			//Display Settings: name (typed), internal name, Favorite, Hidden, Up and Down.
			foreach(string text in new[] { "\"Display Name\"),200,true)","\"Internal Name\"","\"Favorite\"","\"Hidden\"","\"Allowed\"","\"Report\"" }) {
				StringAssert.Contains(source,text);
			}
			StringAssert.Contains(source,"ReportSetupLayout.GetEntries(ListDisplayReportAll)");
			StringAssert.Contains(source,"ReportSetupLayout.ToggleFavorite(_displayReportSelected)");
			StringAssert.Contains(source,"ReportSetupLayout.ToggleHidden(_displayReportSelected)");
			StringAssert.Contains(source,"ReportSetupLayout.Move(ListDisplayReportAll,_displayReportSelected,isUp)");
			StringAssert.Contains(source,"gridReports.FuncCellIsEditable=(col,row) => !_isPermissionMode && col==COL_NAME && GetReportAt(row)!=null;",
				"Only a report's name, on the Display Settings tab, is typed into.");
			//Security Permissions: Allowed, Set All, Set None, hidden reports marked.
			StringAssert.Contains(source,"ReportPermissionEdit.Toggle(ListGroupPermissionsForReports,userGroupNum,_displayReportSelected,ListDisplayReportAll,");
			StringAssert.Contains(source,"ReportPermissionEdit.SetAll(ListGroupPermissionsForReports,userGroupNum)");
			StringAssert.Contains(source,"ReportPermissionEdit.SetNone(ListGroupPermissionsForReports,userGroupNum)");
			StringAssert.Contains(source,"(displayReport.IsHidden ? \" \"+Lan.g(this,\"(hidden)\") : \"\")");
			StringAssert.Contains(source,"if(_isCEMT) {//CEMT can only synchronize display reports with internal names.");
		}

		[TestMethod]
		public void ReportSetup_SaveTakesTheNameBeingTyped() {
			string source=Read("OpenPmsDental","Forms","FormReportSetup.cs");
			int indexCommit=source.IndexOf("userControlReportSetup.CommitPendingEdit();",StringComparison.Ordinal);
			int indexSync=source.IndexOf("DisplayReports.Sync(userControlReportSetup.ListDisplayReportAll)",StringComparison.Ordinal);
			Assert.IsTrue(indexCommit>0 && indexSync>indexCommit);
		}

		[TestMethod]
		public void PatPortionUncollected_OptionAndColumn() {
			string designer=Read("OpenPmsDental","Forms Reports","FormRpPatPortionUncollected.designer.cs");
			StringAssert.Contains(designer,"this.checkShowPrior.Text = \"Show prior uncollected balances\";");
			StringAssert.Contains(designer,"this.labelPrior.Visible = false;");
			string source=Read("OpenPmsDental","Forms Reports","FormRpPatPortionUncollected.cs");
			StringAssert.Contains(source,"_doShowPrior=checkShowPrior.Checked;");
			StringAssert.Contains(source,"RpPatPortionUncollected.GetPatUncollected(_fromDate, _toDate, _listClinicNums, _doShowPrior)");
			StringAssert.Contains(source,"new UI.GridColumn(Lan.g(this,\"Pay Plan\"),75,HorizontalAlignment.Right,UI.GridSortingStrategy.AmountParse)");
			StringAssert.Contains(source,"row.Cells.Add(PIn.Double(rowCur[RpPatPortionUncollected.ColumnPayPlan].ToString()).ToString(\"c\"));");
			StringAssert.Contains(source,"row.ColorBackG=_colorPrior;");
			//Print: the Pay Plan column, and the prior rows in their own section.
			StringAssert.Contains(source,"query.AddColumn(\"Pay Plan\",70,FieldValueType.Number,font);");
			StringAssert.Contains(source,"AddPrintQuery(report,tablePrior,Lan.g(this,\"Prior Uncollected Balances\"),font);");
			StringAssert.Contains(source,"RpPatPortionUncollected.ColumnPayPlan })","The printed columns name the Pay Plan column last.");
		}

		[TestMethod]
		public void NoForkMigrationStep_InThisPackagesLevels() {
			//The four items need no row, preference or schema change: levels 2026110761 to 2026110769 stay free.
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			List<long> listLevels=listSteps.Select(x => x.Level).Where(x => x>=2026110761 && x<=2026110769).ToList();
			Assert.AreEqual(0,listLevels.Count,"Unexpected steps: "+string.Join(",",listLevels));
		}

		private static int CountOf(string text,string value) {
			int count=0;
			int index=text.IndexOf(value,StringComparison.Ordinal);
			while(index>=0) {
				count++;
				index=text.IndexOf(value,index+value.Length,StringComparison.Ordinal);
			}
			return count;
		}
	}
}
