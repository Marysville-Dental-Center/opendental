// Integration tests for the Perio Chart business layer against this run's scratch database (see TestDb): calculated rows are never stored, and
// the Recession option's preference row. Synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BPERIOTests {
	[TestClass]
	public class PerioDbTests {
		private const string PREF_ROW_COUNT="SELECT COUNT(*) FROM preference WHERE PrefName='PerioRecessionInsteadOfGM'";

		[TestInitialize]
		public void TestInitialize() {
			TestDb.ResetOrSkip();
		}

		private static long InsertExam(long patNum) {
			return PerioExams.Insert(new PerioExam { PatNum=patNum,ExamDate=DateTime.Today,DateTMeasureEdit=DateTime.Now });
		}

		private static PerioMeasure MakeMeasure(long perioExamNum,PerioSequenceType perioSequenceType,int intTooth) {
			return new PerioMeasure { PerioExamNum=perioExamNum,SequenceType=perioSequenceType,IntTooth=intTooth,ToothValue=-1,
				MBvalue=3,Bvalue=2,DBvalue=3,MLvalue=-1,Lvalue=-1,DLvalue=-1 };
		}

		private static long CountMeasures(PerioSequenceType perioSequenceType) {
			return TestDb.GetLong("SELECT COUNT(*) FROM periomeasure WHERE SequenceType="+(int)perioSequenceType);
		}

		#region Calculated rows are never stored (v24_4-chart-perio-attached-gingiva)
		[TestMethod]
		public void Insert_NeverStoresAttGingOrCal() {
			long perioExamNum=InsertExam(patNum:1001);
			Assert.AreEqual(0,PerioMeasures.Insert(MakeMeasure(perioExamNum,PerioSequenceType.AttGing,3)));
			Assert.AreEqual(0,PerioMeasures.Insert(MakeMeasure(perioExamNum,PerioSequenceType.CAL,3)));
			Assert.AreNotEqual(0,PerioMeasures.Insert(MakeMeasure(perioExamNum,PerioSequenceType.MGJ,3)));
			Assert.AreEqual(0,CountMeasures(PerioSequenceType.AttGing),"SequenceType 8 must never be written to periomeasure.");
			Assert.AreEqual(0,CountMeasures(PerioSequenceType.CAL));
			Assert.AreEqual(1,CountMeasures(PerioSequenceType.MGJ),"Charted values are still saved.");
		}

		[TestMethod]
		public void InsertMany_LeavesOutAttGingAndCal() {
			long perioExamNum=InsertExam(patNum:1002);
			PerioMeasures.InsertMany(new List<PerioMeasure> {
				MakeMeasure(perioExamNum,PerioSequenceType.Probing,3),
				MakeMeasure(perioExamNum,PerioSequenceType.AttGing,3),
				MakeMeasure(perioExamNum,PerioSequenceType.CAL,3),
				MakeMeasure(perioExamNum,PerioSequenceType.Probing,4),
			});
			Assert.AreEqual(2,CountMeasures(PerioSequenceType.Probing));
			Assert.AreEqual(0,CountMeasures(PerioSequenceType.AttGing));
			Assert.AreEqual(0,CountMeasures(PerioSequenceType.CAL));
		}

		[TestMethod]
		public void Update_NeverTurnsARowIntoAttGing() {
			long perioExamNum=InsertExam(patNum:1003);
			PerioMeasure perioMeasure=MakeMeasure(perioExamNum,PerioSequenceType.MGJ,3);
			PerioMeasures.Insert(perioMeasure);
			perioMeasure.SequenceType=PerioSequenceType.AttGing;
			perioMeasure.Bvalue=9;
			PerioMeasures.Update(perioMeasure);
			Assert.AreEqual(0,CountMeasures(PerioSequenceType.AttGing));
			Assert.AreEqual(2,TestDb.GetLong("SELECT Bvalue FROM periomeasure WHERE PerioMeasureNum="+perioMeasure.PerioMeasureNum),"The stored MGJ row is unchanged.");
		}

		[TestMethod]
		public void Sync_LeavesOutAttGing() {
			long perioExamNum=InsertExam(patNum:1004);
			List<PerioMeasure> listNew=new List<PerioMeasure> {
				MakeMeasure(perioExamNum,PerioSequenceType.Probing,5),
				MakeMeasure(perioExamNum,PerioSequenceType.AttGing,5),
			};
			PerioMeasures.Sync(listNew,new List<PerioMeasure>());
			Assert.AreEqual(1,CountMeasures(PerioSequenceType.Probing));
			Assert.AreEqual(0,CountMeasures(PerioSequenceType.AttGing));
		}
		#endregion Calculated rows are never stored (v24_4-chart-perio-attached-gingiva)

		#region Recession option (v24_4-chart-perio-recession-term)
		[TestMethod]
		public void RecessionOption_MissingRowReadsAsOff() {
			Assert.AreEqual(0,TestDb.GetLong(PREF_ROW_COUNT));
			Assert.IsFalse(PerioMeasures.GetUseRecessionTerm(),"A database without the row behaves as 24.3.");
		}

		[TestMethod]
		public void RecessionOption_SavingOffOnADatabaseWithoutTheRow_WritesNothing() {
			Assert.IsFalse(PerioMeasures.SetUseRecessionTerm(false),"Nothing changed.");
			Assert.AreEqual(0,TestDb.GetLong(PREF_ROW_COUNT),"No row is added until someone turns the option on.");
		}

		[TestMethod]
		public void RecessionOption_TurningOn_InsertsTheRowAndUpdatesTheCache() {
			Assert.IsTrue(PerioMeasures.SetUseRecessionTerm(true));
			Assert.AreEqual(1,TestDb.GetLong(PREF_ROW_COUNT));
			Assert.AreEqual("1",DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='PerioRecessionInsteadOfGM'"));
			Assert.IsTrue(PerioMeasures.GetUseRecessionTerm(),"The local cache follows at once.");
			Assert.IsFalse(PerioMeasures.SetUseRecessionTerm(true),"Saving the same value again is no change.");
			Assert.AreEqual(1,TestDb.GetLong(PREF_ROW_COUNT),"Never a second row.");
		}

		[TestMethod]
		public void RecessionOption_TurningOff_UpdatesTheRow() {
			PerioMeasures.SetUseRecessionTerm(true);
			Assert.IsTrue(PerioMeasures.SetUseRecessionTerm(false));
			Assert.AreEqual(1,TestDb.GetLong(PREF_ROW_COUNT));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='PerioRecessionInsteadOfGM'"));
			Assert.IsFalse(PerioMeasures.GetUseRecessionTerm());
		}

		[TestMethod]
		public void RecessionOption_ReadsARowWrittenElsewhere() {
			//For example another workstation, or an adopted Open Dental 25.3.48 database, whose office set it in Open Dental (docs/port-notes/SWMAP.md).
			DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES('PerioRecessionInsteadOfGM','1')");
			Prefs.RefreshCache();
			Assert.IsTrue(PerioMeasures.GetUseRecessionTerm());
		}
		#endregion Recession option (v24_4-chart-perio-recession-term)
	}
}
