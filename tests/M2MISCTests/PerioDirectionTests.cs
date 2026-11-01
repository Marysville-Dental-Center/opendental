// v25_4-chart-perio-direction: the Perio Chart's Current Direction buttons (ContrPerio.Direction.cs and FormPerio). The grid is created off
// screen with a scratch database; the desktop client is never launched. Synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDental;
using OpenDentBusiness;

namespace M2MISCTests {
	[TestClass]
	public class PerioDirectionTests {
		private const long PAT_NUM=3001;
		private int _countEventsLeft;
		private int _countEventsRight;

		#region Rules, no database
		[TestMethod]
		public void Manual_HoldsOnlyInItsSection_AndWhileUnchanged() {
			Assert.IsTrue(PerioDirectionRules.IsManualInSection(0,0,EnumCurrentDirection.Right,EnumCurrentDirection.Right));
			Assert.IsFalse(PerioDirectionRules.IsManualInSection(0,3,EnumCurrentDirection.Right,EnumCurrentDirection.Right),"Another section");
			Assert.IsFalse(PerioDirectionRules.IsManualInSection(0,0,EnumCurrentDirection.Right,EnumCurrentDirection.Left),"Changed since");
			Assert.IsFalse(PerioDirectionRules.IsManualInSection(-1,0,EnumCurrentDirection.Left,EnumCurrentDirection.Left),"No choice made");
		}

		[TestMethod]
		public void Forward_IsTheChosenDirection_WhileItHolds() {
			Assert.AreEqual(EnumCurrentDirection.Right,
				PerioDirectionRules.GetForwardDirection(EnumCurrentDirection.Left,EnumCurrentDirection.Right,isManual:true));
			Assert.AreEqual(EnumCurrentDirection.Left,
				PerioDirectionRules.GetForwardDirection(EnumCurrentDirection.Left,EnumCurrentDirection.Right,isManual:false));
		}

		[TestMethod]
		public void AgainstPath_ReverseChoiceOrBackspace_ButNotBoth() {
			Assert.IsFalse(PerioDirectionRules.IsAgainstPath(EnumCurrentDirection.Left,EnumCurrentDirection.Left,isReverse:false));
			Assert.IsTrue(PerioDirectionRules.IsAgainstPath(EnumCurrentDirection.Left,EnumCurrentDirection.Left,isReverse:true),"Backspace");
			Assert.IsTrue(PerioDirectionRules.IsAgainstPath(EnumCurrentDirection.Left,EnumCurrentDirection.Right,isReverse:false),"Reverse chosen");
			Assert.IsFalse(PerioDirectionRules.IsAgainstPath(EnumCurrentDirection.Left,EnumCurrentDirection.Right,isReverse:true),
				"Backspace while going in reverse moves along the path");
		}

		[TestMethod]
		public void PathDirection_AndForwardReverseLabel() {
			Assert.AreEqual(EnumCurrentDirection.Left,PerioDirectionRules.GetPathDirection(EnumAdvanceSequence.FacialsFirst,5,isFacial:true));
			Assert.AreEqual(EnumCurrentDirection.Right,PerioDirectionRules.GetPathDirection(EnumAdvanceSequence.FacialsFirst,20,isFacial:true));
			Assert.AreEqual(EnumCurrentDirection.Left,PerioDirectionRules.GetPathDirection(EnumAdvanceSequence.FacialsFirst,20,isFacial:false));
			Assert.AreEqual(EnumCurrentDirection.Left,PerioDirectionRules.GetPathDirection(EnumAdvanceSequence.MaxFirst,20,isFacial:true));
			Assert.AreEqual(EnumCurrentDirection.Right,PerioDirectionRules.GetPathDirection(EnumAdvanceSequence.MaxFirst,5,isFacial:false));
			Assert.IsNull(PerioDirectionRules.GetPathDirection(EnumAdvanceSequence.MaxFirst,0,isFacial:true));
			Assert.AreEqual("Forward",PerioDirectionRules.GetForwardReverseText(EnumAdvanceSequence.MaxFirst,5,true,EnumCurrentDirection.Left));
			Assert.AreEqual("Reverse",PerioDirectionRules.GetForwardReverseText(EnumAdvanceSequence.MaxFirst,5,true,EnumCurrentDirection.Right));
			Assert.AreEqual("",PerioDirectionRules.GetForwardReverseText(EnumAdvanceSequence.MaxFirst,0,true,EnumCurrentDirection.Right));
		}
		#endregion Rules, no database

		#region The grid
		///<summary>A chart with one exam of today, loaded as FormPerio.FillGrid does. The cursor starts on the first maxillary facial probing cell
		///(column 1) going Left.</summary>
		private ContrPerio LoadChart(EnumAdvanceSequence enumAdvanceSequence) {
			TestDb.ResetOrSkip();
			PerioExams.Insert(new PerioExam { PatNum=PAT_NUM,ExamDate=DateTime.Today,DateTMeasureEdit=new DateTime(2026,1,5,8,30,0) });
			FormOpenDental.PatNumCur=PAT_NUM;//ContrPerio reads the patient's procedures for the implant marker.
			ContrPerio contrPerio=new ContrPerio();
			contrPerio.LayoutManager.Is96dpi=true;
			contrPerio.Size=new Size(608,685);//as in FormPerio.designer.cs
			contrPerio.ListPerioExams=PerioExams.Refresh(PAT_NUM);
			contrPerio.ListPerioMeasures=PerioMeasures.GetForPatient(PAT_NUM);
			contrPerio.IdxExamSelected=contrPerio.ListPerioExams.Count-1;
			contrPerio.SetShowCurrentExamOnly(true);
			contrPerio.EnumAdvanceSequence_=enumAdvanceSequence;
			contrPerio.AllowPerioEdit=true;
			contrPerio.LoadData();
			contrPerio.DirectionChangedLeft+=(sender,e) => _countEventsLeft++;
			contrPerio.DirectionChangedRight+=(sender,e) => _countEventsRight++;
			Assert.AreEqual(1,contrPerio.ListColRowsSelected[0].Col);
			Assert.AreEqual(EnumCurrentDirection.Left,contrPerio.EnumCurrentDirection_);
			ResetEventCounts();
			return contrPerio;
		}

		private void ResetEventCounts() {
			_countEventsLeft=0;
			_countEventsRight=0;
		}

		private static int Col(ContrPerio contrPerio) {
			return contrPerio.ListColRowsSelected[0].Col;
		}

		private static int Row(ContrPerio contrPerio) {
			return contrPerio.ListColRowsSelected[0].Row;
		}

		private static void Arrow(ContrPerio contrPerio,Keys keys) {
			contrPerio.KeyPressed(new KeyEventArgs(keys));
		}

		[TestMethod]
		public void MaxillaryFirst_ChosenDirection_AppliesToTheNextKeystroke() {
			ContrPerio contrPerio=LoadChart(EnumAdvanceSequence.MaxFirst);
			contrPerio.ButtonPressedNumber(3);
			contrPerio.ButtonPressedNumber(3);
			Assert.AreEqual(3,Col(contrPerio));
			contrPerio.SetDirectionManual(EnumCurrentDirection.Right);
			Assert.AreEqual(1,_countEventsRight,"The buttons and the label follow at once.");
			contrPerio.ButtonPressedNumber(2);
			Assert.AreEqual(2,Col(contrPerio),"Entered at column 3, then moved back toward tooth 1.");
			Assert.AreEqual("2",contrPerio.GetCellText(3,Row(contrPerio)));
			Assert.AreEqual(EnumCurrentDirection.Right,contrPerio.EnumCurrentDirection_);
			Assert.IsTrue(contrPerio.IsDirectionManual(0));
		}

		[TestMethod]
		public void FacialsFirst_ChosenDirection_HoldsInTheSection() {
			//Before 25.4 the Facials First path set the direction again on every keystroke, so the button had no effect.
			ContrPerio contrPerio=LoadChart(EnumAdvanceSequence.FacialsFirst);
			contrPerio.ButtonPressedNumber(3);
			contrPerio.ButtonPressedNumber(3);
			Assert.AreEqual(3,Col(contrPerio));
			contrPerio.SetDirectionManual(EnumCurrentDirection.Right);
			contrPerio.ButtonPressedNumber(2);
			Assert.AreEqual(2,Col(contrPerio));
			Assert.AreEqual(EnumCurrentDirection.Right,contrPerio.EnumCurrentDirection_);
			contrPerio.ButtonPressedNumber(2);
			Assert.AreEqual(1,Col(contrPerio));
			Assert.AreEqual(EnumCurrentDirection.Right,contrPerio.EnumCurrentDirection_);
			Assert.AreEqual(1,_countEventsRight,"Only the button changed the direction.");
			Assert.AreEqual(0,_countEventsLeft);
		}

		[TestMethod]
		public void FacialsFirst_BackspaceKeepsTheDirectionShown() {
			ContrPerio contrPerio=LoadChart(EnumAdvanceSequence.FacialsFirst);
			contrPerio.ButtonPressedNumber(3);
			contrPerio.ButtonPressedNumber(3);
			Assert.AreEqual(3,Col(contrPerio));
			Arrow(contrPerio,Keys.Back);
			Assert.AreEqual(2,Col(contrPerio),"Backspace moves back one cell and clears it.");
			Assert.IsTrue(string.IsNullOrEmpty(contrPerio.GetCellText(2,Row(contrPerio))));
			Assert.AreEqual(EnumCurrentDirection.Left,contrPerio.EnumCurrentDirection_);
			Assert.AreEqual(0,_countEventsRight);
		}

		[TestMethod]
		public void FacialsFirst_ArrowKeysNeverChangeTheDirection() {
			ContrPerio contrPerio=LoadChart(EnumAdvanceSequence.FacialsFirst);
			contrPerio.ButtonPressedNumber(3);
			Assert.AreEqual(2,Col(contrPerio));
			ResetEventCounts();
			Arrow(contrPerio,Keys.Left);
			Assert.AreEqual(1,Col(contrPerio),"The arrow moved the cursor.");
			Arrow(contrPerio,Keys.Right);
			Arrow(contrPerio,Keys.Right);
			Assert.AreEqual(3,Col(contrPerio));
			Assert.AreEqual(EnumCurrentDirection.Left,contrPerio.EnumCurrentDirection_);
			Assert.AreEqual(0,_countEventsLeft+_countEventsRight,"No direction events from the arrow keys.");
		}

		[TestMethod]
		public void MaxillaryFirst_ArrowKeysCrossingASection_DoNotChangeTheDirection() {
			ContrPerio contrPerio=LoadChart(EnumAdvanceSequence.MaxFirst);
			int rowMaxFacial=Row(contrPerio);
			contrPerio.SetNewCell(48,rowMaxFacial);//tooth 16 facial, last site
			Arrow(contrPerio,Keys.Right);
			Assert.AreNotEqual(rowMaxFacial,Row(contrPerio),"The arrow moved on to the maxillary lingual.");
			Assert.AreEqual(EnumCurrentDirection.Left,contrPerio.EnumCurrentDirection_);
			Assert.AreEqual(0,_countEventsLeft+_countEventsRight);
		}

		[TestMethod]
		public void FacialsFirst_AutoAdvanceIntoTheNextSection_TakesThePathsDirection() {
			ContrPerio contrPerio=LoadChart(EnumAdvanceSequence.FacialsFirst);
			int rowMaxFacial=Row(contrPerio);
			contrPerio.SetNewCell(48,rowMaxFacial);//tooth 16 facial, last site
			contrPerio.SetDirectionManual(EnumCurrentDirection.Left);//the path's own direction here, chosen with the button
			Assert.IsTrue(contrPerio.IsDirectionManual(0));
			ResetEventCounts();
			contrPerio.ButtonPressedNumber(3);
			PerioCell perioCell=contrPerio.GetCurrentCell();
			Assert.AreEqual(17,perioCell.ToothNum,"Facials First goes from 16 facial to 17 facial.");
			Assert.IsTrue(perioCell.IsFacial);
			Assert.AreEqual(EnumCurrentDirection.Right,contrPerio.EnumCurrentDirection_,"17-32 facial goes Right; shown at once.");
			Assert.AreEqual(1,_countEventsRight);
			Assert.IsFalse(contrPerio.IsDirectionManual(0),"The choice ends when entry crosses into another section.");
			Assert.IsFalse(contrPerio.IsDirectionManual(3));
		}

		[TestMethod]
		public void MaxillaryFirst_ArrowFromTheMandibularFacial_RaisesNoFalseRightEvent() {
			//24.3 raised DirectionChangedRight here even when the direction did not change (missing braces), so the Right button lit up wrongly.
			ContrPerio contrPerio=LoadChart(EnumAdvanceSequence.FacialsFirst);
			contrPerio.SetNewCell(48,Row(contrPerio));
			contrPerio.ButtonPressedNumber(3);//Facials First carries the cursor to 17 facial: the mandibular facial probing row
			int rowMandFacial=Row(contrPerio);
			contrPerio.EnumAdvanceSequence_=EnumAdvanceSequence.MaxFirst;
			contrPerio.EnumCurrentDirection_=EnumCurrentDirection.Left;
			contrPerio.SetNewCell(48,rowMandFacial);//tooth 17 facial, last site
			ResetEventCounts();
			Arrow(contrPerio,Keys.Right);
			Assert.AreNotEqual(rowMandFacial,Row(contrPerio),"The arrow moved on to the mandibular lingual.");
			Assert.AreEqual(EnumCurrentDirection.Left,contrPerio.EnumCurrentDirection_);
			Assert.AreEqual(0,_countEventsRight,"No Right event when the direction did not change.");
		}
		#endregion The grid

		#region The window
		[TestMethod]
		public void Window_DirectionButtons_ApplyAtOnce_AndReturnInputToTheChart() {
			string code=TestDb.ReadRepoFile("OpenDental","Forms","FormPerio.cs");
			foreach(string direction in new[] { "Right","Left" }) {
				string method=TaskAttachmentWindowTests.GetMethod(code,"private void radio"+direction+"_Click(");
				StringAssert.Contains(method,"contrPerio.SetDirectionManual(EnumCurrentDirection."+direction+");");
				StringAssert.Contains(method,"SetLabelFwdReverse();");
				StringAssert.Contains(method,"textInputBox.Focus();");
				Assert.IsFalse(method.Contains("contrPerio.EnumCurrentDirection_="),"The button must go through SetDirectionManual.");
				string handler=TaskAttachmentWindowTests.GetMethod(code,"private void gridP_DirectionChanged"+direction+"(");
				StringAssert.Contains(handler,"radio"+direction+".Checked=true;");
				StringAssert.Contains(handler,"SetLabelFwdReverse();");
			}
			//Voice perio keeps its own direction updates.
			StringAssert.Contains(TaskAttachmentWindowTests.GetMethod(code,"private void SetCurrentDirection("),"contrPerio.EnumCurrentDirection_=");
		}
		#endregion The window
	}
}
