// Source checks that the windows of package M2-EMP call the business rules tested elsewhere in this project. The windows themselves are not
// opened (the tests never start the program); these checks keep the hooks from being lost in a merge.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2EMPTests {
	[TestClass]
	public class WindowHookTests {
		[TestMethod]
		public void EmployeeEdit_HasTheTitlePicker() {
			string code=RepoFiles.ReadAllText("OpenDental","Forms","FormEmployeeEdit.cs");
			StringAssert.Contains(code,"comboTitle.Items.AddDefNone();");
			StringAssert.Contains(code,"comboTitle.Items.AddDefs(Employees.GetTitlesForPicker(EmployeeCur.Title));");
			StringAssert.Contains(code,"comboTitle.SetSelectedDefNum(EmployeeCur.Title);");
			StringAssert.Contains(code,"EmployeeCur.Title=comboTitle.GetSelectedDefNum();");
			string designer=RepoFiles.ReadAllText("OpenDental","Forms","FormEmployeeEdit.designer.cs");
			StringAssert.Contains(designer,"this.labelTitle.Text = \"Title\";");
			StringAssert.Contains(designer,"this.Controls.Add(this.comboTitle);");
		}

		[TestMethod]
		public void EmployeeSelect_HasTheTitleColumn() {
			string code=RepoFiles.ReadAllText("OpenDental","Forms","FormEmployeeSelect.cs");
			StringAssert.Contains(code,"new GridColumn(Lan.g(\"FormEmployeeSelect\",\"Title\"),100)");
			StringAssert.Contains(code,"Employees.GetTitleText(_listEmployeesShowing[i].Title,listDefsTitles)");
		}

		[TestMethod]
		public void ClockEventEdit_UsesTheFutureClockOutRules() {
			string code=RepoFiles.ReadAllText("OpenDental","Forms","FormClockEventEdit.cs");
			StringAssert.Contains(code,"ClockEvents.CheckFutureDates(");
			StringAssert.Contains(code,"Security.IsAuthorized(EnumPermType.TimecardsEditAll,true)");
			StringAssert.Contains(code,"\"Clock-out is in the future. Save anyway?");
			StringAssert.Contains(code,"SecurityLogs.MakeLogEntry(EnumPermType.TimecardsEditAll,0,");
			//The clock-in check of 24.3 stays.
			StringAssert.Contains(code,"if(timeDisplayed1.Date > DateTime.Today) {");
			//The confirmation comes after every refusal, just before the event is changed.
			Assert.IsTrue(code.IndexOf("\"Clock-out is in the future.")>code.IndexOf("Overtime and adjustments cannot exceed the total time."));
		}

		[TestMethod]
		public void TimeCard_ShowsTheDateOfALaterClockOut() {
			string code=RepoFiles.ReadAllText("OpenDental","Forms","FormTimeCard.cs");
			StringAssert.Contains(code,"ClockEvents.GetSecondTimeColumnWidth(_listClockEvents,64)");
			StringAssert.Contains(code,"ClockEvents.GetSecondTimeCellText(clockEvent,PrefC.GetBool(PrefName.TimeCardShowSeconds))");
		}

		[TestMethod]
		public void ManageModule_FutureClockOutKeepsClockOutEnabled() {
			string code=RepoFiles.ReadAllText("OpenDental","Main Modules","ControlManage.cs");
			StringAssert.Contains(code,"if(clockEvent.TimeDisplayed2.Year<1880 || ClockEvents.HasFutureClockOut(clockEvent)) {");
		}
	}
}
