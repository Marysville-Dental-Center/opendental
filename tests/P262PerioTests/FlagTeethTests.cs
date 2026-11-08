// Open Dental 26.2 Perio Chart item 14, "Added the ability to add multiple flags to all selected teeth on the Perio Chart": teeth selected by
// their numbers get a flag on every site with one click of a flag button (or its key), and several flags in turn. The real grid on the scratch
// database, driven off screen. Synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using WpfControls.UI;

namespace P262PerioTests {
	[TestClass]
	public class FlagTeethTests {
		[TestInitialize]
		public void TestInitialize() {
			PerioDb.ResetOrSkip();
		}

		private static void AssertSites(ContrPerioDriven contrPerio,int intTooth,BleedingFlags bleedingFlagsExpected,string message) {
			List<ColRow> listColRows=Chart.ProbingSites(contrPerio,intTooth);
			Assert.AreEqual(6,listColRows.Count);
			for(int i=0;i<listColRows.Count;i++) {
				Assert.AreEqual(bleedingFlagsExpected,Chart.Flags(contrPerio,listColRows[i]),message+" (tooth "+intTooth+", site "+i+")");
			}
		}

		[TestMethod]
		public void SelectedTeeth_FlagButtons_AddEachFlagToEverySiteOfEveryTooth() {
			long perioExamNum=Chart.InsertExam(DateTime.Today);
			Chart.InsertMeasure(perioExamNum,PerioSequenceType.Probing,3,mb:3,b:2,db:3,ml:3,l:2,dl:3);//probed, flags still go to every site
			ContrPerioDriven contrPerio=Chart.Load();
			contrPerio.ClickToothNumber(3);
			contrPerio.ClickToothNumber(30);
			CollectionAssert.AreEqual(new List<int> { 3,30 },contrPerio.GetSelectedTeeth());
			Assert.AreEqual(0,contrPerio.ListColRowsSelected.Count,"Clicking a tooth number selects no cell.");
			contrPerio.ButtonPressedLetter("p");//the Plaque button
			AssertSites(contrPerio,3,BleedingFlags.Plaque,"Plaque on every site");
			AssertSites(contrPerio,30,BleedingFlags.Plaque,"Plaque on every site");
			AssertSites(contrPerio,4,BleedingFlags.None,"A tooth that is not selected");
			Assert.IsTrue(contrPerio.HasSelectedTeeth(),"The teeth stay selected for the next flag.");
			contrPerio.ButtonPressedLetter("c");//the Calculus button: several flags on the same teeth
			contrPerio.ButtonPressedLetter("b");
			AssertSites(contrPerio,3,BleedingFlags.Plaque|BleedingFlags.Calculus|BleedingFlags.Blood,"Three flags");
			AssertSites(contrPerio,30,BleedingFlags.Plaque|BleedingFlags.Calculus|BleedingFlags.Blood,"Three flags");
			Assert.AreEqual("2",contrPerio.GetCellText(Chart.Col(3,PerioSurf.B),Chart.Row(contrPerio,0,PerioSequenceType.Probing)),
				"Probing depths are unchanged.");
		}

		[TestMethod]
		public void SelectedTeeth_FlagKeys_WorkLikeTheButtons() {
			Chart.InsertExam(DateTime.Today);
			ContrPerioDriven contrPerio=Chart.Load();
			contrPerio.ClickToothNumber(14);
			contrPerio.KeyPressed(new KeyEventArgs(Keys.Space));//bleeding
			contrPerio.KeyPressed(new KeyEventArgs(Keys.S));
			AssertSites(contrPerio,14,BleedingFlags.Blood|BleedingFlags.Suppuration,"Space bar and S");
		}

		[TestMethod]
		public void SelectedTeeth_FlagOnEverySiteAlready_IsRemoved_OtherwiseCompleted() {
			long perioExamNum=Chart.InsertExam(DateTime.Today);
			//Tooth 8: plaque on the facial sites only, calculus on one site.
			Chart.InsertMeasure(perioExamNum,PerioSequenceType.BleedSupPlaqCalc,8,mb:4,b:4|8,db:4,ml:0,l:0,dl:0);
			ContrPerioDriven contrPerio=Chart.Load();
			contrPerio.ClickToothNumber(8);
			contrPerio.ClickToothNumber(9);
			contrPerio.ButtonPressedLetter("p");
			AssertSites(contrPerio,9,BleedingFlags.Plaque,"Some sites lacked plaque, so it is added");
			Assert.AreEqual(BleedingFlags.Plaque|BleedingFlags.Calculus,Chart.Flags(contrPerio,Chart.Cell(contrPerio,8,PerioSurf.B,PerioSequenceType.Probing)),
				"Other flags of a site stay.");
			contrPerio.ButtonPressedLetter("p");
			AssertSites(contrPerio,9,BleedingFlags.None,"Every site had plaque, so a second click removes it");
			Assert.AreEqual(BleedingFlags.Calculus,Chart.Flags(contrPerio,Chart.Cell(contrPerio,8,PerioSurf.B,PerioSequenceType.Probing)),
				"Removing plaque keeps calculus.");
		}

		[TestMethod]
		public void SelectedTeeth_SkippedTeethAreLeftOut() {
			long perioExamNum=Chart.InsertExam(DateTime.Today);
			PerioMeasures.SetSkipped(perioExamNum,new List<int> { 5 });
			ContrPerioDriven contrPerio=Chart.Load();
			contrPerio.ClickToothNumber(4);
			contrPerio.ClickToothNumber(5);
			contrPerio.ButtonPressedLetter("c");
			AssertSites(contrPerio,4,BleedingFlags.Calculus,"Selected tooth");
			AssertSites(contrPerio,5,BleedingFlags.None,"Skipped tooth");
			//A flag still goes on a skipped tooth by clicking its gray cell, as before.
			ColRow colRowSkipped=Chart.Cell(contrPerio,5,PerioSurf.B,PerioSequenceType.Probing);
			contrPerio.ClickCell(colRowSkipped.Col,colRowSkipped.Row);
			Assert.IsFalse(contrPerio.HasSelectedTeeth(),"Clicking a cell unselects the teeth.");
			contrPerio.ButtonPressedLetter("c");
			Assert.AreEqual(BleedingFlags.Calculus,Chart.Flags(contrPerio,colRowSkipped));
		}

		[TestMethod]
		public void SelectedTeeth_FlagsAreSaved() {
			long perioExamNum=Chart.InsertExam(DateTime.Today);
			ContrPerioDriven contrPerio=Chart.Load();
			contrPerio.ClickToothNumber(19);
			contrPerio.ButtonPressedLetter("p");
			contrPerio.ButtonPressedLetter("b");
			contrPerio.SaveCurExam(contrPerio.ListPerioExams[contrPerio.IdxExamSelected]);
			string where=" FROM periomeasure WHERE PerioExamNum="+perioExamNum+" AND IntTooth=19 AND SequenceType="+(int)PerioSequenceType.BleedSupPlaqCalc;
			Assert.AreEqual(1,PerioDb.GetLong("SELECT COUNT(*)"+where));
			foreach(string column in new[] { "MBvalue","Bvalue","DBvalue","MLvalue","Lvalue","DLvalue" }) {
				Assert.AreEqual((long)(BleedingFlags.Plaque|BleedingFlags.Blood),PerioDb.GetLong("SELECT "+column+where),column);
			}
			Assert.AreEqual(0,PerioDb.GetLong("SELECT COUNT(*) FROM periomeasure WHERE IntTooth!=19 AND SequenceType="+(int)PerioSequenceType.BleedSupPlaqCalc),
				"No other tooth was written.");
		}

		[TestMethod]
		public void CellSelected_FlagGoesToTheCellOnly_AsBefore() {
			long perioExamNum=Chart.InsertExam(DateTime.Today);
			Chart.InsertMeasure(perioExamNum,PerioSequenceType.Probing,6,mb:3,b:3,db:3);
			ContrPerioDriven contrPerio=Chart.Load();
			contrPerio.ClickToothNumber(7);
			ColRow colRow=Chart.Cell(contrPerio,6,PerioSurf.B,PerioSequenceType.Probing);
			contrPerio.ClickCell(colRow.Col,colRow.Row);
			Assert.IsFalse(contrPerio.HasSelectedTeeth());
			contrPerio.ButtonPressedLetter("b");
			Assert.AreEqual(BleedingFlags.Blood,Chart.Flags(contrPerio,colRow));
			Assert.AreEqual(BleedingFlags.None,Chart.Flags(contrPerio,Chart.Cell(contrPerio,6,PerioSurf.MB,PerioSequenceType.Probing)));
			AssertSites(contrPerio,7,BleedingFlags.None,"The tooth selected before the cell click");
		}

		[TestMethod]
		public void SelectedTeeth_DragOverCells_UnselectsTheTeeth() {
			Chart.InsertExam(DateTime.Today);
			ContrPerioDriven contrPerio=Chart.Load();
			contrPerio.ClickToothNumber(2);
			int row=Chart.Row(contrPerio,0,PerioSequenceType.Probing);
			contrPerio.Drag(Chart.CellsInRow(row,new[] { 10,11,12 }));
			Assert.IsFalse(contrPerio.HasSelectedTeeth(),"A cell selection replaces the tooth selection.");
			contrPerio.ButtonPressedLetter("p");
			AssertSites(contrPerio,2,BleedingFlags.None,"Tooth 2 is no longer selected");
		}

		[TestMethod]
		public void SelectedTeeth_ReadOnlyExam_GetsNoFlags() {
			Chart.InsertExam(DateTime.Today);
			ContrPerioDriven contrPerio=Chart.Load();
			contrPerio.ClickToothNumber(3);
			contrPerio.AllowPerioEdit=false;//the user lacks Perio Edit for the exam's date
			contrPerio.ButtonPressedLetter("p");
			Assert.AreEqual(0,contrPerio.ToggleFlagOnSelectedTeeth(BleedingFlags.Plaque));
			AssertSites(contrPerio,3,BleedingFlags.None,"Read only");
		}

		[TestMethod]
		public void SelectedTeeth_OlderExamSelected_FlagsThatExamOnly() {
			long perioExamNumOld=Chart.InsertExam(DateTime.Today.AddDays(-90));
			long perioExamNum=Chart.InsertExam(DateTime.Today);
			ContrPerioDriven contrPerio=Chart.Load(idxExam:0);
			contrPerio.ClickToothNumber(12);
			contrPerio.ButtonPressedLetter("s");
			contrPerio.SaveCurExam(contrPerio.ListPerioExams[0]);
			Assert.AreEqual(1,PerioDb.GetLong("SELECT COUNT(*) FROM periomeasure WHERE IntTooth=12 AND SequenceType="+(int)PerioSequenceType.BleedSupPlaqCalc
				+" AND PerioExamNum="+perioExamNumOld));
			Assert.AreEqual(0,PerioDb.GetLong("SELECT COUNT(*) FROM periomeasure WHERE PerioExamNum="+perioExamNum));
		}
	}
}
