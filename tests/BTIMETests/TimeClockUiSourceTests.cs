// Source checks for the B-TIME windows. The Time Card window and the Manage module are WinForms code that these tests cannot open (the desktop
// client is never launched by tests), so they check that the windows use the tested business rules and show the expected controls.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectFileTests;

namespace BTIMETests {
	[TestClass]
	public class TimeClockUiSourceTests {
		private static string GetRepoRoot() {
			DirectoryInfo dir=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(dir!=null) {
				if(File.Exists(Path.Combine(dir.FullName,"LICENSE")) && File.Exists(Path.Combine(dir.FullName,"NOTICE"))) {
					return dir.FullName;
				}
				dir=dir.Parent;
			}
			throw new DirectoryNotFoundException("The repository root (LICENSE and NOTICE) was not found above the test folder.");
		}

		private static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(Path.Combine(new[] { GetRepoRoot() }.Concat(arrayParts).ToArray()));
		}

		[TestMethod]
		public void TimeCard_TotalsGridReplacesTheTotalBoxes() {
			string designer=ReadRepoFile("OpenPmsDental","Forms","FormTimeCard.designer.cs");
			StringAssert.Contains(designer,"this.gridTotals = new OpenPmsDental.UI.GridOD();");
			StringAssert.Contains(designer,"this.gridTotals.Title = \"Totals for Period\";");
			StringAssert.Contains(designer,"this.radioTotalsDecimal.Text = \"Decimal Format\";");
			StringAssert.Contains(designer,"this.radioTotalsColon.Text = \"Colon Format\";");
			foreach(string removed in new[] { "textTotal","textTotal2","textOvertime","textRateTwo","textRateThree","textPTO","textUnpaidProtectedLeave",
				"labelRegularTime" })
			{
				Assert.IsFalse(designer.Contains("this."+removed+" "),"The 24.3 total box "+removed+" is replaced by the totals grid.");
			}
		}

		[TestMethod]
		public void TimeCard_TotalsUseTheTestedRules_AndNeverChangeThePreference() {
			string code=ReadRepoFile("OpenPmsDental","Forms","FormTimeCard.cs");
			StringAssert.Contains(code,"TimeCardTotals.Calculate(_listClockEvents,_listTimeAdjusts,IsBreaks)");
			StringAssert.Contains(code,"SetTotalsFormat(TimeCardTotals.GetDefaultFormat());");
			StringAssert.Contains(code,"TimeCardTotals.GetCells(_timeCardPeriodTotals,_enumTimeCardTotalsFormat,PrefC.GetBool(PrefName.TimeCardShowSeconds))");
			StringAssert.Contains(code,"PrintTotalsRow(","Printing prints the totals row in the chosen format.");
			Assert.IsFalse(code.Contains("PrefName.TimeCardsUseDecimalInsteadOfColon"),
				"The window starts from the preference through TimeCardTotals.GetDefaultFormat and never writes it.");
			Assert.IsFalse(code.Contains("Prefs.Update"),"Switching the format in the window does not change any preference.");
		}

		[TestMethod]
		public void TimeClock_ReportsToFilterIsWiredIntoTheEmployeeGrid() {
			string designer=ReadRepoFile("OpenPmsDental","Main Modules","ControlManage.designer.cs");
			string code=ReadRepoFile("OpenPmsDental","Main Modules","ControlManage.cs");
			string codeReportsTo=ReadRepoFile("OpenPmsDental","Main Modules","ControlManage.ReportsTo.cs");
			StringAssert.Contains(designer,"this.comboReportsTo = new OpenPmsDental.UI.ComboBox();");
			StringAssert.Contains(designer,"this.groupBox1.Controls.Add(this.comboReportsTo);");
			StringAssert.Contains(designer,"this.labelReportsTo.Text = \"Reports To\";");
			StringAssert.Contains(designer,"this.comboReportsTo.SelectionChangeCommitted += new System.EventHandler(this.comboReportsTo_SelectionChangeCommitted);");
			StringAssert.Contains(code,"long supervisorEmployeeNum=FillComboReportsTo();");
			StringAssert.Contains(code,"if(!EmployeeReportsTo.IsShown(_listEmployees[i],supervisorEmployeeNum)) {");
			StringAssert.Contains(codeReportsTo,"EmployeeReportsTo.GetSupervisors(_listEmployees)");
			StringAssert.Contains(codeReportsTo,"EmployeeReportsTo.GetValidSelection(EmployeeReportsTo.GetRemembered(userNum),listSupervisors)");
			StringAssert.Contains(codeReportsTo,"EmployeeReportsTo.Remember(");
			//The evaluated items, so the check holds for the classic project file and the SDK-style one of the .NET 10 move alike.
			ProjectFile.Load(Path.Combine(GetRepoRoot(),"OpenPmsDental","OpenPmsDental.csproj")).AssertCompiles(@"Main Modules\ControlManage.ReportsTo.cs");
		}
	}
}
