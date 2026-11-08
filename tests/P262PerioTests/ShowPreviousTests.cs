// Open Dental 26.2 Perio Chart item 16, "Added new settings to show values for past exams in the Perio Chart": Show previous exams (userodpref
// PerioCurrentExamOnly, now hiding the earlier exams' values in every row) and Show previous probing values (userodpref
// PerioShowCurrentProbingOnly, 31). The grid on the scratch database, and the per-user rows. Synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using WpfControls.UI;

namespace P262PerioTests {
	[TestClass]
	public class ShowPreviousTests {
		private static readonly DateTime _dateOld=DateTime.Today.AddDays(-120);
		private static readonly DateTime _dateOlder=DateTime.Today.AddDays(-240);

		[TestInitialize]
		public void TestInitialize() {
			PerioDb.ResetOrSkip();
		}

		///<summary>Three exams. The two earlier ones have probing, ging marg, MGJ, furcation, mobility and a bleeding flag on tooth 3; the selected
		///(latest) exam has probing on tooth 3 only, so its other rows show the earlier exam's values grayed when previous exams are shown.</summary>
		private static void InsertThreeExams() {
			long perioExamNumOlder=Chart.InsertExam(_dateOlder);
			Chart.InsertMeasure(perioExamNumOlder,PerioSequenceType.Probing,3,b:7);
			Chart.InsertMeasure(perioExamNumOlder,PerioSequenceType.GingMargin,3,b:1);
			long perioExamNumOld=Chart.InsertExam(_dateOld);
			Chart.InsertMeasure(perioExamNumOld,PerioSequenceType.Probing,3,mb:4,b:5,db:6);
			Chart.InsertMeasure(perioExamNumOld,PerioSequenceType.BleedSupPlaqCalc,3,b:(int)BleedingFlags.Blood);
			Chart.InsertMeasure(perioExamNumOld,PerioSequenceType.GingMargin,3,b:2);
			Chart.InsertMeasure(perioExamNumOld,PerioSequenceType.MGJ,3,b:3);
			Chart.InsertMeasure(perioExamNumOld,PerioSequenceType.Furcation,3,b:1);
			Chart.InsertMeasure(perioExamNumOld,PerioSequenceType.Mobility,3,toothValue:2);
			long perioExamNum=Chart.InsertExam(DateTime.Today);
			Chart.InsertMeasure(perioExamNum,PerioSequenceType.Probing,3,mb:2,b:3,db:2);
		}

		private static string Grayed(ContrPerio contrPerio,PerioSequenceType perioSequenceType) {
			return Chart.OldText(contrPerio,Chart.Cell(contrPerio,3,PerioSurf.B,perioSequenceType));
		}

		[TestMethod]
		public void BothShown_EarlierProbingRowsAndGrayedValues() {
			InsertThreeExams();
			ContrPerioDriven contrPerio=Chart.Load(showPreviousExams:true,showPreviousProbing:true);
			Assert.AreEqual(4,Chart.RowsLabeled(contrPerio,_dateOld.ToShortDateString()).Count,"A probing row for the earlier exam in each section.");
			Assert.AreEqual(4,Chart.RowsLabeled(contrPerio,_dateOlder.ToShortDateString()).Count);
			int rowOld=Chart.RowsLabeled(contrPerio,_dateOld.ToShortDateString())[0];
			Assert.AreEqual("5",contrPerio.GetPerioCellFromColRow(new ColRow(Chart.Col(3,PerioSurf.B),rowOld)).OldText);
			Assert.AreEqual(BleedingFlags.Blood,Chart.Flags(contrPerio,new ColRow(Chart.Col(3,PerioSurf.B),rowOld)));
			Assert.AreEqual("2",Grayed(contrPerio,PerioSequenceType.GingMargin),"The latest earlier value, grayed.");
			Assert.AreEqual("3",Grayed(contrPerio,PerioSequenceType.MGJ));
			Assert.AreEqual("1",Grayed(contrPerio,PerioSequenceType.Furcation));
			Assert.AreEqual("2",Grayed(contrPerio,PerioSequenceType.Mobility));
			Assert.AreEqual("3",contrPerio.GetCellText(Chart.Col(3,PerioSurf.B),Chart.Row(contrPerio,0,PerioSequenceType.Probing)));
		}

		[TestMethod]
		public void PreviousProbingHidden_GrayedValuesStay() {
			InsertThreeExams();
			ContrPerioDriven contrPerio=Chart.Load(showPreviousExams:true,showPreviousProbing:false);
			Assert.AreEqual(0,Chart.RowsLabeled(contrPerio,_dateOld.ToShortDateString()).Count,"No probing rows for earlier exams.");
			Assert.AreEqual(0,Chart.RowsLabeled(contrPerio,_dateOlder.ToShortDateString()).Count);
			Assert.AreEqual(4,Chart.RowsLabeled(contrPerio,DateTime.Today.ToShortDateString()).Count,"The selected exam's probing rows.");
			Assert.AreEqual("2",Grayed(contrPerio,PerioSequenceType.GingMargin));
			Assert.AreEqual("3",Grayed(contrPerio,PerioSequenceType.MGJ));
			Assert.AreEqual("1",Grayed(contrPerio,PerioSequenceType.Furcation));
			Assert.AreEqual("2",Grayed(contrPerio,PerioSequenceType.Mobility));
			Assert.AreEqual("3",contrPerio.GetCellText(Chart.Col(3,PerioSurf.B),Chart.Row(contrPerio,0,PerioSequenceType.Probing)));
			Assert.AreEqual("",Chart.OldText(contrPerio,Chart.Cell(contrPerio,3,PerioSurf.B,PerioSequenceType.Probing)),
				"The selected exam's probing row shows no earlier probing.");
		}

		[TestMethod]
		public void PreviousExamsHidden_OnlyTheSelectedExam_InEveryRow() {
			InsertThreeExams();
			foreach(bool showPreviousProbing in new[] { true,false }) {//the probing setting only counts while previous exams are shown
				ContrPerioDriven contrPerio=Chart.Load(showPreviousExams:false,showPreviousProbing:showPreviousProbing);
				string name="Show previous probing values: "+showPreviousProbing;
				Assert.AreEqual(0,Chart.RowsLabeled(contrPerio,_dateOld.ToShortDateString()).Count,name);
				Assert.AreEqual(0,Chart.RowsLabeled(contrPerio,_dateOlder.ToShortDateString()).Count,name);
				foreach(PerioSequenceType perioSequenceType in new[] { PerioSequenceType.GingMargin,PerioSequenceType.MGJ,PerioSequenceType.Furcation,
					PerioSequenceType.Mobility })
				{
					Assert.AreEqual("",Grayed(contrPerio,perioSequenceType),name+", "+perioSequenceType+": no grayed value of an earlier exam");
				}
				Assert.AreEqual("3",contrPerio.GetCellText(Chart.Col(3,PerioSurf.B),Chart.Row(contrPerio,0,PerioSequenceType.Probing)),name);
			}
		}

		[TestMethod]
		public void PreviousExamsHidden_GrayedValueIsNotDrawn() {
			InsertThreeExams();
			ContrPerioDriven contrPerioShown=Chart.Load(showPreviousExams:true,showPreviousProbing:false);
			ContrPerioDriven contrPerioHidden=Chart.Load(showPreviousExams:false,showPreviousProbing:false);
			ColRow colRowGingMarg=Chart.Cell(contrPerioShown,3,PerioSurf.B,PerioSequenceType.GingMargin);
			using Bitmap bitmapShown=Draw(contrPerioShown);
			using Bitmap bitmapHidden=Draw(contrPerioHidden);
			Assert.IsTrue(HasInk(contrPerioShown,bitmapShown,colRowGingMarg),"Positive control: the grayed ging marg of the earlier exam is drawn.");
			Assert.IsFalse(HasInk(contrPerioHidden,bitmapHidden,colRowGingMarg),"Without previous exams it is not.");
		}

		private static Bitmap Draw(ContrPerio contrPerio) {
			Bitmap bitmap=new Bitmap(700,720);
			using(Graphics g=Graphics.FromImage(bitmap)) {
				contrPerio.DrawChart(g);
			}
			return bitmap;
		}

		///<summary>True if text is drawn inside the cell, between its grid lines: a pixel with a channel well below the light gray backgrounds and
		///lines. Grayed values are gray, or light red at or above the "red if" threshold (a ging marg of 2 here).</summary>
		private static bool HasInk(ContrPerio contrPerio,Bitmap bitmap,ColRow colRow) {
			float[] arrayColPos=(float[])typeof(ContrPerio).GetField("_colPosArray",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(contrPerio);
			float[] arrayRowPos=(float[])typeof(ContrPerio).GetField("_rowPosArray",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(contrPerio);
			for(int x=(int)arrayColPos[colRow.Col]+1;x<(int)arrayColPos[colRow.Col+1];x++) {
				for(int y=(int)arrayRowPos[colRow.Row]+1;y<(int)arrayRowPos[colRow.Row+1];y++) {
					Color color=bitmap.GetPixel(x,y);
					if(Math.Min(color.R,Math.Min(color.G,color.B))<160) {
						return true;
					}
				}
			}
			return false;
		}

		[TestMethod]
		public void OlderExamSelected_ShowsOnlyWhatCameBefore() {
			InsertThreeExams();
			ContrPerioDriven contrPerio=Chart.Load(showPreviousExams:true,showPreviousProbing:true,idxExam:1);
			Assert.AreEqual(4,Chart.RowsLabeled(contrPerio,_dateOlder.ToShortDateString()).Count);
			Assert.AreEqual(0,Chart.RowsLabeled(contrPerio,DateTime.Today.ToShortDateString()).Count,"A later exam is never shown.");
			Assert.AreEqual("2",contrPerio.GetCellText(Chart.Col(3,PerioSurf.B),Chart.Row(contrPerio,0,PerioSequenceType.GingMargin)));
			contrPerio=Chart.Load(showPreviousExams:false,showPreviousProbing:true,idxExam:1);
			Assert.AreEqual(0,Chart.RowsLabeled(contrPerio,_dateOlder.ToShortDateString()).Count);
			Assert.AreEqual("2",contrPerio.GetCellText(Chart.Col(3,PerioSurf.B),Chart.Row(contrPerio,0,PerioSequenceType.GingMargin)),
				"The selected exam's own values.");
		}

		[TestMethod]
		public void UnconfiguredGrid_AndTheOldMethod_KeepTheBehaviourBefore26_2() {
			//The perio image attached to a claim builds a grid without any setting: only the selected exam's probing, the grayed values stay.
			InsertThreeExams();
			ContrPerioDriven contrPerio=Chart.Create();
			contrPerio.LoadData();
			Assert.AreEqual(0,Chart.RowsLabeled(contrPerio,_dateOld.ToShortDateString()).Count);
			Assert.AreEqual("2",Grayed(contrPerio,PerioSequenceType.GingMargin));
			//SetShowCurrentExamOnly is the check box before 26.2: it hid earlier probing rows only.
			contrPerio=Chart.Create();
			contrPerio.SetShowCurrentExamOnly(true);
			contrPerio.LoadData();
			Assert.AreEqual(0,Chart.RowsLabeled(contrPerio,_dateOld.ToShortDateString()).Count);
			Assert.AreEqual("2",Grayed(contrPerio,PerioSequenceType.GingMargin));
			contrPerio=Chart.Create();
			contrPerio.SetShowCurrentExamOnly(false);
			contrPerio.LoadData();
			Assert.AreEqual(4,Chart.RowsLabeled(contrPerio,_dateOld.ToShortDateString()).Count);
			Assert.AreEqual("2",Grayed(contrPerio,PerioSequenceType.GingMargin));
		}

		[TestMethod]
		public void PreviousExamsHidden_EntryIsSavedToTheSelectedExam() {
			InsertThreeExams();
			long perioExamNum=PerioDb.GetLong("SELECT PerioExamNum FROM perioexam WHERE ExamDate="+POut.Date(DateTime.Today));
			ContrPerioDriven contrPerio=Chart.Load(showPreviousExams:false,showPreviousProbing:false);
			ColRow colRow=Chart.Cell(contrPerio,3,PerioSurf.B,PerioSequenceType.GingMargin);
			contrPerio.ClickCell(colRow.Col,colRow.Row);
			contrPerio.ButtonPressedNumber(4);
			contrPerio.SaveCurExam(contrPerio.ListPerioExams[contrPerio.IdxExamSelected]);
			Assert.AreEqual(4,PerioDb.GetLong("SELECT Bvalue FROM periomeasure WHERE IntTooth=3 AND SequenceType="+(int)PerioSequenceType.GingMargin
				+" AND PerioExamNum="+perioExamNum));
			Assert.AreEqual(2,PerioDb.GetLong("SELECT COUNT(*) FROM periomeasure WHERE IntTooth=3 AND SequenceType="+(int)PerioSequenceType.GingMargin
				+" AND PerioExamNum!="+perioExamNum),"The earlier exams' rows are untouched.");
		}

		#region Per-user settings
		private static string ValueString(long userNum,UserOdFkeyType userOdFkeyType) {
			return PerioDb.GetString("SELECT ValueString FROM userodpref WHERE UserNum="+userNum+" AND FkeyType="+(int)userOdFkeyType);
		}

		[TestMethod]
		public void Settings_NoRows_BothShown() {
			Assert.IsTrue(PerioMeasures.GetShowPreviousExams(PerioDb.USER_NUM));
			Assert.IsTrue(PerioMeasures.GetShowPreviousProbing(PerioDb.USER_NUM));
		}

		[TestMethod]
		public void Settings_SavedAsOpenDentalsRows_EachTheOppositeOfItsCheckBox() {
			Assert.IsTrue(PerioMeasures.SaveShowPrevious(PerioDb.USER_NUM,showPreviousExams:false,showPreviousProbing:false));
			Assert.AreEqual("1",ValueString(PerioDb.USER_NUM,UserOdFkeyType.PerioCurrentExamOnly));
			Assert.AreEqual("1",ValueString(PerioDb.USER_NUM,UserOdFkeyType.PerioShowCurrentProbingOnly));
			Assert.AreEqual(1,PerioDb.GetLong("SELECT COUNT(*) FROM userodpref WHERE FkeyType=31"),"Open Dental's value 31.");
			UserOdPrefs.RefreshCache();
			Assert.IsFalse(PerioMeasures.GetShowPreviousExams(PerioDb.USER_NUM));
			Assert.IsFalse(PerioMeasures.GetShowPreviousProbing(PerioDb.USER_NUM));
			Assert.IsFalse(PerioMeasures.SaveShowPrevious(PerioDb.USER_NUM,showPreviousExams:false,showPreviousProbing:false),"Nothing changed.");
			Assert.IsTrue(PerioMeasures.SaveShowPrevious(PerioDb.USER_NUM,showPreviousExams:true,showPreviousProbing:false));
			Assert.AreEqual("0",ValueString(PerioDb.USER_NUM,UserOdFkeyType.PerioCurrentExamOnly));
			Assert.AreEqual("1",ValueString(PerioDb.USER_NUM,UserOdFkeyType.PerioShowCurrentProbingOnly));
			Assert.AreEqual(2,PerioDb.GetLong("SELECT COUNT(*) FROM userodpref"),"Updated, not inserted again.");
			UserOdPrefs.RefreshCache();
			Assert.IsTrue(PerioMeasures.GetShowPreviousExams(PerioDb.USER_NUM));
			Assert.IsFalse(PerioMeasures.GetShowPreviousProbing(PerioDb.USER_NUM));
		}

		[TestMethod]
		public void Settings_PerUser() {
			const long userNumOther=2;
			PerioMeasures.SaveShowPrevious(userNumOther,showPreviousExams:false,showPreviousProbing:false);
			UserOdPrefs.RefreshCache();
			Assert.IsTrue(PerioMeasures.GetShowPreviousExams(PerioDb.USER_NUM),"Another user's setting does not count.");
			Assert.IsTrue(PerioMeasures.GetShowPreviousProbing(PerioDb.USER_NUM));
			PerioMeasures.SaveShowPrevious(PerioDb.USER_NUM,showPreviousExams:true,showPreviousProbing:true);
			Assert.AreEqual("1",ValueString(userNumOther,UserOdFkeyType.PerioCurrentExamOnly),"Saving one user leaves the other alone.");
		}

		[TestMethod]
		public void Settings_RowFromBefore26_2_IsShowPreviousExamsUnchecked() {
			//A user who had "Show current exam only" checked has PerioCurrentExamOnly=1 and no probing row.
			OpenPmsDentalBusiness.Crud.UserOdPrefCrud.Insert(new UserOdPref { UserNum=PerioDb.USER_NUM,FkeyType=UserOdFkeyType.PerioCurrentExamOnly,
				ValueString="1" });
			UserOdPrefs.RefreshCache();
			Assert.IsFalse(PerioMeasures.GetShowPreviousExams(PerioDb.USER_NUM));
			Assert.IsTrue(PerioMeasures.GetShowPreviousProbing(PerioDb.USER_NUM));
		}
		#endregion Per-user settings
	}
}
