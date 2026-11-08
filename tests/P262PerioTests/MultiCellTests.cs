// Open Dental 26.2 Perio Chart item 15, "Multiple cells can be selected from any editable data row on the Perio Chart": a drag selects several
// cells in the probing row of the selected exam (as before) and now also in the Mobility, Furc, Ging Marg and MGJ rows; one entry fills them all.
// The real grid on the scratch database, driven off screen. Synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using WpfControls.UI;

namespace P262PerioTests {
	[TestClass]
	public class MultiCellTests {
		[TestInitialize]
		public void TestInitialize() {
			PerioDb.ResetOrSkip();
		}

		///<summary>Every column of the teeth, left to right as the grid draws them.</summary>
		private static int[] ColsOfTeeth(params int[] arrayTeeth) {
			return arrayTeeth.SelectMany(x => Chart.ColsOfTooth(x)).OrderBy(x => x).ToArray();
		}

		private static void AssertSelected(ContrPerio contrPerio,IEnumerable<ColRow> listColRowsExpected,string message) {
			List<string> listExpected=listColRowsExpected.Select(x => x.Col+","+x.Row).OrderBy(x => x).ToList();
			List<string> listActual=contrPerio.ListColRowsSelected.Select(x => x.Col+","+x.Row).OrderBy(x => x).ToList();
			CollectionAssert.AreEqual(listExpected,listActual,message+". Selected: "+string.Join(" ",listActual));
		}

		[TestMethod]
		public void Drag_InEachEditableRow_SelectsEveryCellDraggedOver() {
			Chart.InsertExam(DateTime.Today);
			ContrPerioDriven contrPerio=Chart.Load();
			//Furc, Ging Marg and MGJ in the sections that have them, and the selected exam's probing (which could already be dragged before 26.2).
			(int Section,PerioSequenceType PerioSequenceType)[] arrayRows=new[] {
				(0,PerioSequenceType.Furcation),(1,PerioSequenceType.Furcation),(2,PerioSequenceType.Furcation),(3,PerioSequenceType.Furcation),
				(0,PerioSequenceType.GingMargin),(1,PerioSequenceType.GingMargin),(2,PerioSequenceType.GingMargin),(3,PerioSequenceType.GingMargin),
				(0,PerioSequenceType.MGJ),(2,PerioSequenceType.MGJ),(3,PerioSequenceType.MGJ),
				(0,PerioSequenceType.Probing),(1,PerioSequenceType.Probing),(2,PerioSequenceType.Probing),(3,PerioSequenceType.Probing),
			};
			foreach((int Section,PerioSequenceType PerioSequenceType) testRow in arrayRows) {
				ColRow[] arrayColRows=Chart.CellsInRow(Chart.Row(contrPerio,testRow.Section,testRow.PerioSequenceType),new[] { 4,5,6,7,8 });
				contrPerio.Drag(arrayColRows);
				AssertSelected(contrPerio,arrayColRows,"Section "+testRow.Section+" "+testRow.PerioSequenceType);
			}
		}

		[TestMethod]
		public void Drag_Mobility_SelectsTheOneCellOfEachTooth() {
			Chart.InsertExam(DateTime.Today);
			ContrPerioDriven contrPerio=Chart.Load();
			foreach(int section in new[] { 0,3 }) {
				int row=Chart.Row(contrPerio,section,PerioSequenceType.Mobility);
				contrPerio.Drag(Chart.CellsInRow(row,new[] { 7,8,9,10,11,12,13,14,15 }));//all three columns of three teeth
				AssertSelected(contrPerio,Chart.CellsInRow(row,new[] { 8,11,14 }),"Section "+section+": the middle cells only, each once");
			}
		}

		[TestMethod]
		public void Drag_OneEntry_FillsEveryCell_AndIsSaved() {
			long perioExamNum=Chart.InsertExam(DateTime.Today);
			Chart.InsertMeasure(perioExamNum,PerioSequenceType.Probing,3,mb:3,b:4,db:5);
			ContrPerioDriven contrPerio=Chart.Load();
			int rowGingMarg=Chart.Row(contrPerio,0,PerioSequenceType.GingMargin);
			contrPerio.Drag(Chart.CellsInRow(rowGingMarg,ColsOfTeeth(2,3)));
			contrPerio.ButtonPressedNumber(2);
			foreach(int col in ColsOfTeeth(2,3)) {
				Assert.AreEqual("2",contrPerio.GetCellText(col,rowGingMarg),"Ging Marg column "+col);
			}
			int rowCal=Chart.Row(contrPerio,0,PerioSequenceType.CAL);
			Assert.AreEqual("6",contrPerio.GetCellText(Chart.Col(3,PerioSurf.B),rowCal),"auto CAL follows every cell (probing 4 + ging marg 2).");
			Assert.AreEqual("",contrPerio.GetCellText(Chart.Col(2,PerioSurf.B),rowCal),"No probing on tooth 2, so no CAL.");
			Assert.AreEqual(6,contrPerio.ListColRowsSelected.Count,"The selection stays after the entry (no advance).");
			int rowMobility=Chart.Row(contrPerio,3,PerioSequenceType.Mobility);
			contrPerio.Drag(Chart.CellsInRow(rowMobility,ColsOfTeeth(30,29)));
			contrPerio.ButtonPressedNumber(1);
			int rowFurc=Chart.Row(contrPerio,2,PerioSequenceType.Furcation);
			contrPerio.Drag(Chart.CellsInRow(rowFurc,Chart.ColsOfTooth(30)));//the lingual furcation of tooth 30
			contrPerio.ButtonPressedNumber(2);
			contrPerio.SaveCurExam(contrPerio.ListPerioExams[contrPerio.IdxExamSelected]);
			string where=" FROM periomeasure WHERE PerioExamNum="+perioExamNum;
			foreach(int intTooth in new[] { 2,3 }) {
				Assert.AreEqual("2,2,2",PerioDb.GetString("SELECT CONCAT(MBvalue,',',Bvalue,',',DBvalue)"+where+" AND IntTooth="+intTooth
					+" AND SequenceType="+(int)PerioSequenceType.GingMargin),"Ging Marg of tooth "+intTooth);
			}
			Assert.AreEqual(1,PerioDb.GetLong("SELECT ToothValue"+where+" AND IntTooth=30 AND SequenceType="+(int)PerioSequenceType.Mobility));
			Assert.AreEqual(1,PerioDb.GetLong("SELECT ToothValue"+where+" AND IntTooth=29 AND SequenceType="+(int)PerioSequenceType.Mobility));
			Assert.AreEqual("2,2,2",PerioDb.GetString("SELECT CONCAT(MLvalue,',',Lvalue,',',DLvalue)"+where+" AND IntTooth=30 AND SequenceType="
				+(int)PerioSequenceType.Furcation));
		}

		[TestMethod]
		public void Drag_DeleteClearsEveryCell() {
			long perioExamNum=Chart.InsertExam(DateTime.Today);
			Chart.InsertMeasure(perioExamNum,PerioSequenceType.MGJ,20,mb:4,b:4,db:4);
			Chart.InsertMeasure(perioExamNum,PerioSequenceType.MGJ,21,mb:5,b:5,db:5);
			ContrPerioDriven contrPerio=Chart.Load();
			int rowMgj=Chart.Row(contrPerio,3,PerioSequenceType.MGJ);
			contrPerio.Drag(Chart.CellsInRow(rowMgj,ColsOfTeeth(20,21)));
			contrPerio.KeyPressed(new KeyEventArgs(Keys.Delete));
			foreach(int col in ColsOfTeeth(20,21)) {
				Assert.AreEqual("",contrPerio.GetCellText(col,rowMgj),"MGJ column "+col);
			}
		}

		[TestMethod]
		public void Drag_StaysInTheRowTypeWhereItStarted() {
			Chart.InsertExam(DateTime.Today);
			ContrPerioDriven contrPerio=Chart.Load();
			int rowGingMarg=Chart.Row(contrPerio,0,PerioSequenceType.GingMargin);
			int rowCal=Chart.Row(contrPerio,0,PerioSequenceType.CAL);
			int rowFurc=Chart.Row(contrPerio,0,PerioSequenceType.Furcation);
			int rowMgj=Chart.Row(contrPerio,0,PerioSequenceType.MGJ);
			//Start in Ging Marg, wander through auto CAL into Furc, back to Ging Marg, and up into MGJ.
			contrPerio.Drag(new ColRow(4,rowGingMarg),new ColRow(5,rowGingMarg),new ColRow(5,rowCal),new ColRow(6,rowFurc),new ColRow(7,rowFurc),
				new ColRow(7,rowGingMarg),new ColRow(8,rowMgj));
			AssertSelected(contrPerio,Chart.CellsInRow(rowGingMarg,new[] { 4,5,7 }),"Ging Marg cells only");
		}

		[TestMethod]
		public void Drag_CalculatedRowsAndEarlierExams_AreNeverSelected() {
			Chart.InsertExam(DateTime.Today.AddDays(-30));
			Chart.InsertExam(DateTime.Today);
			ContrPerioDriven contrPerio=Chart.Load(showPreviousExams:true,showPreviousProbing:true);
			int rowProbingEarlier=Chart.Row(contrPerio,0,PerioSequenceType.Probing)+1;//section 0 is drawn upside down: the earlier exam is below
			Assert.AreEqual(DateTime.Today.AddDays(-30).ToShortDateString(),contrPerio.GetCellText(0,rowProbingEarlier));
			foreach(int row in new[] { Chart.Row(contrPerio,0,PerioSequenceType.CAL),Chart.Row(contrPerio,0,PerioSequenceType.AttGing),
				Chart.Row(contrPerio,3,PerioSequenceType.AttGing),rowProbingEarlier })
			{
				contrPerio.Drag(Chart.CellsInRow(row,new[] { 4,5,6 }));
				Assert.AreEqual(0,contrPerio.ListColRowsSelected.Count,"Row "+row+" takes no data.");
			}
		}

		[TestMethod]
		public void Drag_PassesOverSkippedTeeth_InEveryRow() {
			long perioExamNum=Chart.InsertExam(DateTime.Today);
			PerioMeasures.SetSkipped(perioExamNum,new List<int> { 3 });
			ContrPerioDriven contrPerio=Chart.Load();
			foreach(PerioSequenceType perioSequenceType in new[] { PerioSequenceType.Probing,PerioSequenceType.GingMargin,PerioSequenceType.Furcation }) {
				int row=Chart.Row(contrPerio,0,perioSequenceType);
				contrPerio.Drag(Chart.CellsInRow(row,ColsOfTeeth(2,3,4)));
				AssertSelected(contrPerio,Chart.CellsInRow(row,ColsOfTeeth(2,4)),perioSequenceType+": tooth 3 is skipped");
			}
		}

		[TestMethod]
		public void Drag_FlagsGoToProbingCellsOnly() {
			Chart.InsertExam(DateTime.Today);
			ContrPerioDriven contrPerio=Chart.Load();
			int rowFurc=Chart.Row(contrPerio,1,PerioSequenceType.Furcation);
			contrPerio.Drag(Chart.CellsInRow(rowFurc,new[] { 4,5,6 }));
			contrPerio.ButtonPressedLetter("b");
			Assert.AreEqual(0,contrPerio.ListPerioMeasureItemsChanged.Count,"Bleeding on several Furc cells changes nothing.");
			int rowProbing=Chart.Row(contrPerio,1,PerioSequenceType.Probing);
			contrPerio.Drag(Chart.CellsInRow(rowProbing,new[] { 4,5,6 }));
			contrPerio.ButtonPressedLetter("b");
			foreach(int col in new[] { 4,5,6 }) {
				Assert.AreEqual(BleedingFlags.Blood,Chart.Flags(contrPerio,new ColRow(col,rowProbing)),"Probing column "+col);
			}
		}

		[TestMethod]
		public void Drag_WithinOneCell_IsAClick() {
			Chart.InsertExam(DateTime.Today);
			ContrPerioDriven contrPerio=Chart.Load();
			int countRight=0;
			contrPerio.DirectionChangedRight+=(sender,e) => countRight++;
			int rowGingMargLingual=Chart.Row(contrPerio,1,PerioSequenceType.GingMargin);
			contrPerio.Drag(new ColRow(7,rowGingMargLingual),new ColRow(7,rowGingMargLingual));
			AssertSelected(contrPerio,new[] { new ColRow(7,rowGingMargLingual) },"One cell");
			Assert.AreEqual(1,countRight,"A click on a lingual cell sets the direction, also when the mouse moved a little.");
			int rowMobility=Chart.Row(contrPerio,0,PerioSequenceType.Mobility);
			contrPerio.Drag(new ColRow(10,rowMobility));//the left cell of tooth 4
			AssertSelected(contrPerio,new[] { new ColRow(11,rowMobility) },"Mobility: the middle cell of the tooth");
		}

		[TestMethod]
		public void Drag_ReadOnly_SelectsNothing() {
			Chart.InsertExam(DateTime.Today);
			ContrPerioDriven contrPerio=Chart.Load(allowEdit:false);
			int row=Chart.Row(contrPerio,0,PerioSequenceType.GingMargin);
			int countBefore=contrPerio.ListColRowsSelected.Count;
			contrPerio.Drag(Chart.CellsInRow(row,new[] { 4,5,6 }));
			Assert.IsFalse(contrPerio.ListColRowsSelected.Any(x => x.Row==row),"No Ging Marg cell is selected on a read-only exam.");
			Assert.AreEqual(countBefore,contrPerio.ListColRowsSelected.Count);
		}
	}
}
