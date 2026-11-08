// Source checks for the P262-APPT window code that cannot run without a screen: the Advanced Appointment Search window (several blockout types,
// the Blockout column, More), the upper left corner of the Appointments module (practice and clinic notes) and the insurance plan window
// (appointment estimates on save). The behaviour behind them is tested on the database in the other test classes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace P262ApptTests {
	[TestClass]
	public class SourceWiringTests {
		///<summary>Walks up from the test assembly until it finds the repository root (LICENSE and NOTICE).</summary>
		private static string GetRoot() {
			string dir=Path.GetDirectoryName(typeof(SourceWiringTests).Assembly.Location);
			while(dir!=null) {
				if(File.Exists(Path.Combine(dir,"LICENSE")) && File.Exists(Path.Combine(dir,"NOTICE"))) {
					return dir;
				}
				dir=Path.GetDirectoryName(dir);
			}
			throw new InvalidOperationException("Could not find the repository root (LICENSE and NOTICE) above the test assembly.");
		}

		///<summary>The file's text without // comments and with LF line ends, so a check for code is not fooled by a comment.</summary>
		private static string Read(params string[] arrayParts) {
			string text=File.ReadAllText(Path.Combine(GetRoot(),Path.Combine(arrayParts))).Replace("\r\n","\n");
			return Regex.Replace(text,@"//[^\n]*","");
		}

		///<summary>The text of a method, from its declaration to the next member declaration at the same indent.</summary>
		private static string Method(string source,string declaration) {
			int start=source.IndexOf(declaration,StringComparison.Ordinal);
			Assert.IsTrue(start>=0,"Not found: "+declaration);
			Match match=new Regex(@"\n\t\t(private|public|protected|internal|#region|#endregion)\b").Match(source,start+declaration.Length);
			int end=match.Success ? match.Index : source.Length;
			return source.Substring(start,end-start);
		}

		[TestMethod]
		public void AdvancedSearch_BlockoutComboIsMultiSelect_AndSearchesEverySelectedType() {
			string designer=Read("OpenPmsDental","Forms","FormApptSearchAdvanced.designer.cs");
			StringAssert.Contains(designer,"this.comboBlockout.SelectionModeMulti = true;");
			StringAssert.Contains(designer,"this.comboBlockout.SelectionChangeCommitted += new System.EventHandler(this.comboBlockout_SelectionChangeCommitted);");
			string code=Read("OpenPmsDental","Forms","FormApptSearchAdvanced.cs");
			string doSearch=Method(code,"private void DoSearch(");
			StringAssert.Contains(doSearch,"List<long> listBlockoutDefNums=GetSelectedBlockoutDefNums();");
			StringAssert.Contains(doSearch,"ApptSearch.GetSearchResultsForBlockouts(_appointment.AptNum,startDate,endDate,listProvNums,listOpNums,");
			Assert.IsFalse(doSearch.Contains("GetSelectedDefNum()"),"A multi-select combo has no single selected DefNum.");
			Assert.IsFalse(doSearch.Contains("GetSearchResultsForBlockoutAndProvider("),"One method for one or more types, with or without providers.");
			StringAssert.Contains(Method(code,"private void FillGrid("),"Defs.GetName(DefCat.BlockoutTypes,_listScheduleOpenings[i].BlockoutDefNum)");
			StringAssert.Contains(Method(code,"private void butMore_Click("),".Date.AddDays(1).ToShortDateString();");
			string handler=Method(code,"private void comboBlockout_SelectionChangeCommitted(");
			StringAssert.Contains(handler,"comboBlockout.SelectedIndices=new List<int>() { 0 };");
			StringAssert.Contains(handler,"comboBlockout.SelectedIndices=listIndices.FindAll(x => x!=0);");
		}

		[TestMethod]
		public void AppointmentsModule_UpperLeftCornerShowsThePracticeNotes() {
			string panel=Read("OpenPmsDental","UI","Appt","ControlApptPanel.cs");
			StringAssert.Contains(Method(panel,"protected override void OnPaint("),"DrawPracticeNotesCorner(graphics);");
			StringAssert.Contains(Method(panel,"private List<Schedule> GetPracticeNotes("),
				"Schedules.GetPracticeNotesForApptModule(_listSchedules,Clinics.ClinicNum);");
			string tip=Method(panel,"private void HeaderTipDraw(");
			StringAssert.Contains(tip,"Schedules.GetPracticeNotesText(GetPracticeNotes(),includeDate:IsWeeklyView);");
			Assert.IsTrue(tip.IndexOf("GetPracticeNotesText",StringComparison.Ordinal)<tip.IndexOf("point.X<_widthTime",StringComparison.Ordinal),
				"The corner is handled before the check that hides the tip left of the provider headers.");
			string mouseDown=Method(panel,"protected override void OnMouseDown(");
			int indexCorner=mouseDown.IndexOf("if(e.Y<_heightProvOpHeaders && e.X<=_widthTime) {",StringComparison.Ordinal);
			Assert.IsTrue(indexCorner>0,"A click in the corner shows the notes too.");
			StringAssert.Contains(mouseDown.Substring(indexCorner,200),"HeaderTipDraw(e.Location);");
		}

		[TestMethod]
		public void InsurancePlan_SaveComputesTheAppointmentEstimatesOfThePlan() {
			string code=Read("OpenPmsDental","Forms","FormInsPlan.cs");
			string save=Method(code,"private void butSave_Click(");
			StringAssert.Contains(save,"listPatNumsEstimated=InsPlans.GetPatNumsForSubscriber(_insSub.Subscriber);");
			StringAssert.Contains(save,"InsPlans.ComputeEstimatesForApptsOnPlan(_insPlan.PlanNum,DateTime.Today,listPatNumsEstimated);");
			int indexSubscriber=save.IndexOf("InsPlans.ComputeEstimatesForSubscriber(_insSub.Subscriber);",StringComparison.Ordinal);
			int indexPlan=save.IndexOf("InsPlans.ComputeEstimatesForApptsOnPlan(",StringComparison.Ordinal);
			int indexOk=save.LastIndexOf("DialogResult=DialogResult.OK;",StringComparison.Ordinal);
			Assert.IsTrue(indexSubscriber>0 && indexSubscriber<indexPlan && indexPlan<indexOk,
				"After the plan, benefits and subscriber are saved, before the window closes.");
			string region=save.Substring(save.IndexOf("#region Appointment Estimates",StringComparison.Ordinal));
			StringAssert.Contains(region,"if(Security.IsAuthorized(EnumPermType.InsPlanEdit,true)) {");
		}
	}
}
