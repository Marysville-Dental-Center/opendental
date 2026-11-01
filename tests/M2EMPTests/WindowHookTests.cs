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
		public void TimeCard_TotalsStayUnderTheirColumns() {
			//Integration with B-TIME: the Totals for Period grid's first column spans Date, Day, In and Out of gridMain, so it takes their widths from
			//gridMain, which includes a second-time column widened for a later-day clock-out.
			string code=RepoFiles.ReadAllText("OpenDental","Forms","FormTimeCard.cs");
			int indexFill=code.IndexOf("private void FillGridTotals(");
			Assert.IsTrue(indexFill>0);
			string fill=code.Substring(indexFill);
			StringAssert.Contains(fill,
				"widthSpacer=gridMain.Columns[0].ColWidth+gridMain.Columns[1].ColWidth+gridMain.Columns[2].ColWidth+gridMain.Columns[3].ColWidth;");
			StringAssert.Contains(fill,"gridTotals.Columns.Add(new GridColumn(\"\",widthSpacer));");
			Assert.IsFalse(fill.Contains("gridTotals.Columns.Add(new GridColumn(\"\",70+45+64+64));"));
			//FillMain sets gridMain's columns before it fills the totals.
			string fillMain=code.Substring(code.IndexOf("private void FillMain("));
			Assert.IsTrue(fillMain.IndexOf("FillGridTotals();")>fillMain.IndexOf("ClockEvents.GetSecondTimeColumnWidth(_listClockEvents,64)"));
		}

		[TestMethod]
		public void TimeCard_PrintKeepsTheClockOutTime() {
			string code=RepoFiles.ReadAllText("OpenDental","Forms","FormTimeCard.cs");
			int indexPrint=code.IndexOf("private void pd_PrintPage(");
			Assert.IsTrue(indexPrint>0);
			string print=code.Substring(indexPrint);
			//The Out/In column takes the room its widest text needs from the Note column, before the column positions are worked out.
			StringAssert.Contains(print,"int widthSecondTimeMoved=ClockEvents.GetSecondTimePrintWidthMoved(_listClockEvents,PrefC.GetBool(PrefName.TimeCardShowSeconds),");
			StringAssert.Contains(print,"x => g.MeasureString(x,font).Width,intArrayColW[3],intArrayColW[intArrayColW.Length-1]);");
			int indexWiden=print.IndexOf("intArrayColW[3]+=widthSecondTimeMoved;");
			int indexNarrow=print.IndexOf("intArrayColW[intArrayColW.Length-1]-=widthSecondTimeMoved;");
			Assert.IsTrue(indexWiden>print.IndexOf("intArrayColW[14]=80;"),"After the clinic layout sets the Note width.");
			Assert.IsTrue(indexNarrow>indexWiden);
			Assert.IsTrue(indexNarrow<print.IndexOf("int[] intArrayColPos=new int[intArrayColW.Length+1];"));
			//Every cell prints through GetPrintCellText, which prints a clock event's second time with its time first.
			StringAssert.Contains(print,"e.Graphics.DrawString(GetPrintCellText(gridMain.ListGridRows[_linesPrinted],i),font,solidBrush");
			StringAssert.Contains(print,"e.Graphics.DrawString(GetPrintCellText(gridMain.ListGridRows[_linesPrinted],i),font,Brushes.Red");
			Assert.IsFalse(print.Contains("DrawString(gridMain.ListGridRows[_linesPrinted].Cells[i].Text"));
			StringAssert.Contains(print,"if(indexColumn==3 && gridRow.Tag is ClockEvent clockEvent) {");
			StringAssert.Contains(print,"return ClockEvents.GetSecondTimePrintText(clockEvent,PrefC.GetBool(PrefName.TimeCardShowSeconds));");
		}

		[TestMethod]
		public void ManageModule_FutureClockOutKeepsClockOutEnabled() {
			string code=RepoFiles.ReadAllText("OpenDental","Main Modules","ControlManage.cs");
			StringAssert.Contains(code,"if(clockEvent.TimeDisplayed2.Year<1880 || ClockEvents.HasFutureClockOut(clockEvent)) {");
		}
	}
}
