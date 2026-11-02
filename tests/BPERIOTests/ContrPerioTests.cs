// Tests for the Perio Chart grid (OpenPmsDental.ContrPerio) against this run's scratch database (see TestDb): the auto Att Ging rows, the
// Recession label, the implant marker and the row layout that FormPerio's voice navigation relies on. The control is created and drawn
// off screen; the desktop client is never launched. Synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using WpfControls.UI;

namespace BPERIOTests {
	[TestClass]
	public class ContrPerioTests {
		private const long PAT_NUM=2001;
		private const string TERM_AUTO_ATT_GING="auto Att Ging";

		[TestInitialize]
		public void TestInitialize() {
			TestDb.ResetOrSkip();
		}

		#region Helpers
		private static long InsertExam(DateTime examDate) {
			//Whole seconds, as the database stores them, so SaveCurExam sees the exam unchanged by another user.
			return PerioExams.Insert(new PerioExam { PatNum=PAT_NUM,ExamDate=examDate,DateTMeasureEdit=new DateTime(2026,1,5,8,30,0) });
		}

		///<summary>Inserts a measure row directly through the CRUD layer, the way an existing database holds it.</summary>
		private static void InsertMeasure(long perioExamNum,PerioSequenceType perioSequenceType,int intTooth,int mb=-1,int b=-1,int db=-1,int ml=-1,
			int l=-1,int dl=-1)
		{
			OpenPmsDentalBusiness.Crud.PerioMeasureCrud.Insert(new PerioMeasure { PerioExamNum=perioExamNum,SequenceType=perioSequenceType,IntTooth=intTooth,
				ToothValue=-1,MBvalue=mb,Bvalue=b,DBvalue=db,MLvalue=ml,Lvalue=l,DLvalue=dl });
		}

		///<summary>Creates the grid at 96 dpi with the patient's exams, selects one (the latest by default) and loads it, as FormPerio.FillGrid does.
		///</summary>
		private static ContrPerio LoadChart(int idxExam=-1,bool showCurrentOnly=true,Size? sizeControl=null) {
			FormOpenDental.PatNumCur=PAT_NUM;//ContrPerio reads the patient's procedures for the implant marker.
			ContrPerio contrPerio=new ContrPerio();
			contrPerio.LayoutManager.Is96dpi=true;
			contrPerio.Size=sizeControl??new Size(608,685);//default as in FormPerio.designer.cs
			Assert.IsTrue(contrPerio.GetRectangleBoundsShowing().Height>2,"The grid was not laid out.");
			contrPerio.ListPerioExams=PerioExams.Refresh(PAT_NUM);
			contrPerio.ListPerioMeasures=PerioMeasures.GetForPatient(PAT_NUM);
			if(idxExam==-1) {
				idxExam=contrPerio.ListPerioExams.Count-1;
			}
			contrPerio.IdxExamSelected=idxExam;
			contrPerio.SetShowCurrentExamOnly(showCurrentOnly);
			contrPerio.LoadData();
			return contrPerio;
		}

		///<summary>The grid column of a site, as ContrPerio.GetCell computes it.</summary>
		private static int Col(int intTooth,PerioSurf perioSurf) {
			int col;
			bool isMiddle=perioSurf==PerioSurf.B || perioSurf==PerioSurf.L;
			bool isMesial=perioSurf==PerioSurf.MB || perioSurf==PerioSurf.ML;
			if(intTooth<=16) {
				col=intTooth*3-2+(isMiddle ? 1 : 0);
				if(intTooth<=8 ? isMesial : (!isMiddle && !isMesial)) {
					col+=2;
				}
				return col;
			}
			col=(33-intTooth)*3-2+(isMiddle ? 1 : 0);
			if(intTooth>=25 ? isMesial : (!isMiddle && !isMesial)) {
				col+=2;
			}
			return col;
		}

		private static string AttGingText(ContrPerio contrPerio,int intTooth,bool isFacial,PerioSurf perioSurf) {
			int row=contrPerio.GetTableRowForSection(contrPerio.GetSection(intTooth,isFacial),PerioSequenceType.AttGing);
			Assert.AreNotEqual(-1,row,"No auto Att Ging row for tooth "+intTooth+(isFacial ? " facial" : " lingual"));
			return contrPerio.GetCellText(Col(intTooth,perioSurf),row);
		}

		private static string RowLabel(ContrPerio contrPerio,int section,PerioSequenceType perioSequenceType) {
			return contrPerio.GetCellText(0,contrPerio.GetTableRowForSection(section,perioSequenceType));
		}
		#endregion Helpers

		#region Layout
		[TestMethod]
		public void Layout_AttGingRowsSitBetweenGingMargAndMgj_WhereMgjIsCharted() {
			InsertExam(DateTime.Today);
			ContrPerio contrPerio=LoadChart();
			foreach(int section in new[] { 0,2,3 }) {//maxillary facial, mandibular lingual, mandibular facial
				int rowAttGing=contrPerio.GetTableRowForSection(section,PerioSequenceType.AttGing);
				Assert.AreNotEqual(-1,rowAttGing,"Section "+section);
				Assert.AreEqual(TERM_AUTO_ATT_GING,contrPerio.GetCellText(0,rowAttGing));
				Assert.AreEqual(1,Math.Abs(rowAttGing-contrPerio.GetTableRowForSection(section,PerioSequenceType.MGJ)),"Next to MGJ, section "+section);
				Assert.AreEqual(1,Math.Abs(rowAttGing-contrPerio.GetTableRowForSection(section,PerioSequenceType.GingMargin)),"Next to Ging Marg, section "+section);
			}
			Assert.AreEqual(-1,contrPerio.GetTableRowForSection(1,PerioSequenceType.AttGing),"The maxillary lingual has no MGJ, so no auto Att Ging.");
			Assert.AreEqual(-1,contrPerio.GetTableRowForSection(1,PerioSequenceType.MGJ));
		}

		[TestMethod]
		public void Layout_GridIsThreeShortRowsTaller_AndFitsTheWindow() {
			InsertExam(DateTime.Today);
			ContrPerio contrPerio=LoadChart();
			//24.3: 642 at 96 dpi (FormPerio's print size). Each auto Att Ging row adds a 12 pixel row plus its line.
			Assert.AreEqual(642+3*13,contrPerio.GetRectangleBoundsShowing().Height);
			Assert.IsTrue(contrPerio.GetRectangleBoundsShowing().Height<=685,"The grid fits the 608x685 control in FormPerio.");
		}

		[TestMethod]
		public void Layout_ClaimPerioAttachment_HoldsTheWholeGrid() {
			//Claim Edit > Perio: FormClaimEdit.butAttachPerioHelper builds its own grid and draws it into a bitmap that becomes the claim attachment.
			//The sizes are read from that code (96 dpi). The bottom row (the sixth exam's mandibular facial probing) and the bottom border must be in it.
			PerioUiSourceTests.ClaimPerioAttachmentSizes sizes=PerioUiSourceTests.ReadClaimPerioAttachmentSizes();
			long perioExamNum=0;
			for(int i=0;i<6;i++) {
				perioExamNum=InsertExam(DateTime.Today.AddDays(i-5));
			}
			InsertMeasure(perioExamNum,PerioSequenceType.Probing,30,b:2);
			ContrPerio contrPerio=LoadChart(showCurrentOnly:false,sizeControl:sizes.SizeGrid);
			float[] arrayRowPos=(float[])typeof(ContrPerio).GetField("_rowPosArray",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(contrPerio);
			int rowProbing=contrPerio.GetTableRowForSection(contrPerio.GetSection(30,isFacial:true),PerioSequenceType.Probing);
			Assert.AreEqual(arrayRowPos.Length-1,rowProbing,"The latest of six exams is the bottom row.");
			Assert.AreEqual("2",contrPerio.GetCellText(Col(30,PerioSurf.B),rowProbing));
			int yBottomBorder=contrPerio.GetRectangleBoundsShowing().Height-2;//the grid's bottom line, drawn at the full height of the rows
			Assert.IsTrue(yBottomBorder<sizes.SizeBitmapGrid.Height,"The bottom border (y="+yBottomBorder+") is inside the "+sizes.SizeBitmapGrid.Height
				+" pixel high bitmap.");
			using Bitmap bitmap=new Bitmap(sizes.SizeBitmapGrid.Width,sizes.SizeBitmapGrid.Height);
			using(Graphics g=Graphics.FromImage(bitmap)) {
				contrPerio.DrawChart(g);
			}
			Color colorBorder=bitmap.GetPixel(bitmap.Width/2,yBottomBorder);
			Assert.IsTrue(colorBorder.R<60 && colorBorder.G<60 && colorBorder.B<60,"The bottom border is drawn.");
			//The claim's grid is read-only (AllowPerioEdit false), so its values are drawn gray. Negative control: an empty cell of the same row.
			Assert.IsTrue(HasInkInCell(contrPerio,bitmap,Col(30,PerioSurf.B),rowProbing,yBottomBorder),"The probing depth in the bottom row is drawn.");
			Assert.IsFalse(HasInkInCell(contrPerio,bitmap,Col(29,PerioSurf.B),rowProbing,yBottomBorder),"Negative control: tooth 29 has no probing.");
		}

		///<summary>True if anything darker than light gray is drawn inside the cell, between its grid lines.</summary>
		private static bool HasInkInCell(ContrPerio contrPerio,Bitmap bitmap,int col,int row,int yBottomBorder) {
			float[] arrayColPos=(float[])typeof(ContrPerio).GetField("_colPosArray",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(contrPerio);
			float[] arrayRowPos=(float[])typeof(ContrPerio).GetField("_rowPosArray",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(contrPerio);
			int bottom=row+1<arrayRowPos.Length ? (int)arrayRowPos[row+1] : yBottomBorder;
			for(int x=(int)arrayColPos[col]+1;x<(int)arrayColPos[col+1];x++) {
				for(int y=(int)arrayRowPos[row]+1;y<bottom;y++) {
					Color color=bitmap.GetPixel(x,y);
					if(color.R<200 && color.G<200 && color.B<200) {
						return true;
					}
				}
			}
			return false;
		}

		[TestMethod]
		public void Layout_RowsUsedByVoiceNavigation() {
			//FormPerio used fixed row numbers (mobility 10 and 32; current probing 5, 15, 26 and 37). With the auto Att Ging rows they move, and
			//FormPerio now asks the grid. These are the new numbers.
			InsertExam(DateTime.Today);
			ContrPerio contrPerio=LoadChart();
			Assert.AreEqual(11,contrPerio.GetTableRowForSection(0,PerioSequenceType.Mobility));
			Assert.AreEqual(34,contrPerio.GetTableRowForSection(3,PerioSequenceType.Mobility));
			Assert.AreEqual(5,contrPerio.GetTableRowForSection(0,PerioSequenceType.Probing));
			Assert.AreEqual(16,contrPerio.GetTableRowForSection(1,PerioSequenceType.Probing));
			Assert.AreEqual(27,contrPerio.GetTableRowForSection(2,PerioSequenceType.Probing));
			Assert.AreEqual(40,contrPerio.GetTableRowForSection(3,PerioSequenceType.Probing));
			Assert.AreEqual(10,contrPerio.GetTableRowForSection(0,PerioSequenceType.Furcation));
			Assert.AreEqual(13,contrPerio.GetTableRowForSection(1,PerioSequenceType.Furcation));
			Assert.AreEqual(32,contrPerio.GetTableRowForSection(2,PerioSequenceType.Furcation));
			Assert.AreEqual(35,contrPerio.GetTableRowForSection(3,PerioSequenceType.Furcation));
			Assert.AreEqual(6,contrPerio.GetTableRowForSection(0,PerioSequenceType.MGJ));
			Assert.AreEqual(28,contrPerio.GetTableRowForSection(2,PerioSequenceType.MGJ));
			Assert.AreEqual(39,contrPerio.GetTableRowForSection(3,PerioSequenceType.MGJ));
		}

		[TestMethod]
		public void Layout_CellsKnowTheirToothAndSide() {
			//GetPerioCell used fixed row boundaries (12, 20, 30); they now come from the layout.
			InsertExam(DateTime.Today);
			ContrPerio contrPerio=LoadChart();
			int col=Col(3,PerioSurf.B);//tooth 3 on the maxilla, tooth 30 on the mandible
			(int Section,PerioSequenceType PerioSequenceType,int ToothNum,bool IsFacial)[] arrayCases=new[] {
				(0,PerioSequenceType.Probing,3,true),(0,PerioSequenceType.AttGing,3,true),(0,PerioSequenceType.Mobility,3,true),
				(1,PerioSequenceType.Probing,3,false),(1,PerioSequenceType.Furcation,3,false),
				(2,PerioSequenceType.Probing,30,false),(2,PerioSequenceType.AttGing,30,false),(2,PerioSequenceType.Furcation,30,false),
				(3,PerioSequenceType.Probing,30,true),(3,PerioSequenceType.AttGing,30,true),(3,PerioSequenceType.Mobility,30,true),
			};
			foreach((int Section,PerioSequenceType PerioSequenceType,int ToothNum,bool IsFacial) testCase in arrayCases) {
				PerioCell perioCell=contrPerio.GetPerioCellFromColRow(new ColRow(col,contrPerio.GetTableRowForSection(testCase.Section,testCase.PerioSequenceType)));
				string name="section "+testCase.Section+" "+testCase.PerioSequenceType;
				Assert.AreEqual(testCase.ToothNum,perioCell.ToothNum,name);
				Assert.AreEqual(testCase.IsFacial,perioCell.IsFacial,name);
			}
		}
		#endregion Layout

		#region Attached gingiva (v24_4-chart-perio-attached-gingiva)
		[TestMethod]
		public void AttGing_IsMgjMinusProbing_ForTheCurrentExam() {
			long perioExamNum=InsertExam(DateTime.Today);
			InsertMeasure(perioExamNum,PerioSequenceType.MGJ,3,mb:5,b:4,db:3);
			InsertMeasure(perioExamNum,PerioSequenceType.Probing,3,mb:3,b:4,db:5);
			InsertMeasure(perioExamNum,PerioSequenceType.MGJ,30,ml:6,l:7,dl:8);
			InsertMeasure(perioExamNum,PerioSequenceType.Probing,30,ml:2,l:2,dl:2);
			InsertMeasure(perioExamNum,PerioSequenceType.MGJ,19,mb:4,b:4,db:4);
			InsertMeasure(perioExamNum,PerioSequenceType.Probing,19,mb:1,b:2,db:3);
			ContrPerio contrPerio=LoadChart();
			Assert.AreEqual("2",AttGingText(contrPerio,3,isFacial:true,PerioSurf.MB));
			Assert.AreEqual("0",AttGingText(contrPerio,3,isFacial:true,PerioSurf.B),"Zero is shown.");
			Assert.AreEqual("-2",AttGingText(contrPerio,3,isFacial:true,PerioSurf.DB),"Below zero is shown as calculated.");
			Assert.AreEqual("4",AttGingText(contrPerio,30,isFacial:false,PerioSurf.ML));
			Assert.AreEqual("5",AttGingText(contrPerio,30,isFacial:false,PerioSurf.L));
			Assert.AreEqual("6",AttGingText(contrPerio,30,isFacial:false,PerioSurf.DL));
			Assert.AreEqual("3",AttGingText(contrPerio,19,isFacial:true,PerioSurf.MB));
			Assert.AreEqual("2",AttGingText(contrPerio,19,isFacial:true,PerioSurf.B));
			Assert.AreEqual("1",AttGingText(contrPerio,19,isFacial:true,PerioSurf.DB));
		}

		[TestMethod]
		public void AttGing_BlankWhenMgjOrProbingIsMissing() {
			long perioExamNum=InsertExam(DateTime.Today);
			InsertMeasure(perioExamNum,PerioSequenceType.Probing,4,mb:3,b:3,db:3);//no MGJ
			InsertMeasure(perioExamNum,PerioSequenceType.MGJ,5,mb:3,b:3,db:3);//no probing
			InsertMeasure(perioExamNum,PerioSequenceType.MGJ,6,mb:5,b:-1,db:5);
			InsertMeasure(perioExamNum,PerioSequenceType.Probing,6,mb:-1,b:2,db:2);
			ContrPerio contrPerio=LoadChart();
			Assert.AreEqual("",AttGingText(contrPerio,4,isFacial:true,PerioSurf.B));
			Assert.AreEqual("",AttGingText(contrPerio,5,isFacial:true,PerioSurf.B));
			Assert.AreEqual("",AttGingText(contrPerio,6,isFacial:true,PerioSurf.MB),"Probing blank at this site.");
			Assert.AreEqual("",AttGingText(contrPerio,6,isFacial:true,PerioSurf.B),"MGJ blank at this site.");
			Assert.AreEqual("3",AttGingText(contrPerio,6,isFacial:true,PerioSurf.DB));
		}

		[TestMethod]
		public void AttGing_OlderExamsAreNotShown() {
			long perioExamNumOld=InsertExam(DateTime.Today.AddDays(-200));
			InsertMeasure(perioExamNumOld,PerioSequenceType.MGJ,3,b:5);
			InsertMeasure(perioExamNumOld,PerioSequenceType.Probing,3,b:2);
			InsertExam(DateTime.Today);//the current exam has nothing on tooth 3
			foreach(bool showCurrentOnly in new[] { true,false }) {
				ContrPerio contrPerio=LoadChart(showCurrentOnly:showCurrentOnly);
				Assert.AreEqual("",AttGingText(contrPerio,3,isFacial:true,PerioSurf.B),"Show current only: "+showCurrentOnly);
			}
			ContrPerio contrPerioOld=LoadChart(idxExam:0);
			Assert.AreEqual("3",AttGingText(contrPerioOld,3,isFacial:true,PerioSurf.B),"Selecting the older exam shows its own value.");
		}

		[TestMethod]
		public void AttGing_StoredRowIsIgnored() {
			//Open Dental never stores SequenceType 8. If a database had such a row anyway, the chart still shows only the calculation.
			long perioExamNum=InsertExam(DateTime.Today);
			InsertMeasure(perioExamNum,PerioSequenceType.AttGing,7,mb:9,b:9,db:9);
			ContrPerio contrPerio=LoadChart();
			Assert.AreEqual("",AttGingText(contrPerio,7,isFacial:true,PerioSurf.B));
		}

		[TestMethod]
		[Timeout(60000)]
		public void AttGing_RecalculatesOnEntry_AndIsNeverSaved() {
			long perioExamNum=InsertExam(DateTime.Today);
			InsertMeasure(perioExamNum,PerioSequenceType.MGJ,3,b:4);
			InsertMeasure(perioExamNum,PerioSequenceType.Probing,3,b:4);
			ContrPerio contrPerio=LoadChart();
			contrPerio.AllowPerioEdit=true;
			int col=Col(3,PerioSurf.B);
			Assert.AreEqual("0",AttGingText(contrPerio,3,isFacial:true,PerioSurf.B));
			//Change the MGJ.
			contrPerio.SetNewCell(col,contrPerio.GetTableRowForSection(0,PerioSequenceType.MGJ));
			contrPerio.ButtonPressedNumber(6);
			Assert.AreEqual("2",AttGingText(contrPerio,3,isFacial:true,PerioSurf.B));
			//Change the probing depth.
			contrPerio.SetNewCell(col,contrPerio.GetTableRowForSection(0,PerioSequenceType.Probing));
			contrPerio.ButtonPressedNumber(1);
			Assert.AreEqual("5",AttGingText(contrPerio,3,isFacial:true,PerioSurf.B));
			//Clear the MGJ.
			contrPerio.SetNewCell(col,contrPerio.GetTableRowForSection(0,PerioSequenceType.MGJ));
			contrPerio.KeyPressed(new KeyEventArgs(Keys.Delete));
			Assert.AreEqual("",AttGingText(contrPerio,3,isFacial:true,PerioSurf.B));
			contrPerio.SetNewCell(col,contrPerio.GetTableRowForSection(0,PerioSequenceType.MGJ));
			contrPerio.ButtonPressedNumber(7);
			Assert.AreEqual("6",AttGingText(contrPerio,3,isFacial:true,PerioSurf.B));
			//Saving writes the charted values only, even if something marked an auto Att Ging cell as changed.
			contrPerio.ListPerioMeasureItemsChanged.Add(new ContrPerio.PerioMeasureItem(PerioSequenceType.AttGing,3));
			contrPerio.ListPerioMeasureItemsChanged.Add(new ContrPerio.PerioMeasureItem(PerioSequenceType.CAL,3));
			contrPerio.SaveCurExam(contrPerio.ListPerioExams[contrPerio.IdxExamSelected]);
			Assert.AreEqual(0,TestDb.GetLong("SELECT COUNT(*) FROM periomeasure WHERE SequenceType IN (7,8)"),"Auto CAL and auto Att Ging are never saved.");
			Assert.AreEqual(7,TestDb.GetLong("SELECT Bvalue FROM periomeasure WHERE SequenceType=3 AND IntTooth=3 AND PerioExamNum="+perioExamNum));
			Assert.AreEqual(1,TestDb.GetLong("SELECT Bvalue FROM periomeasure WHERE SequenceType=4 AND IntTooth=3 AND PerioExamNum="+perioExamNum));
		}

		[TestMethod]
		public void AttGing_CannotBeSelectedWithTheArrowKeys() {
			InsertExam(DateTime.Today);
			ContrPerio contrPerio=LoadChart();
			contrPerio.AllowPerioEdit=true;
			int col=Col(3,PerioSurf.B);
			int rowGingMarg=contrPerio.GetTableRowForSection(0,PerioSequenceType.GingMargin);
			int rowMGJ=contrPerio.GetTableRowForSection(0,PerioSequenceType.MGJ);
			//Section 0 is drawn upside down: MGJ is above auto Att Ging, which is above Ging Marg.
			contrPerio.SetNewCell(col,rowGingMarg);
			contrPerio.KeyPressed(new KeyEventArgs(Keys.Up));
			Assert.AreEqual(rowMGJ,contrPerio.ListColRowsSelected[0].Row,"Up from Ging Marg skips auto Att Ging.");
			contrPerio.KeyPressed(new KeyEventArgs(Keys.Down));
			Assert.AreEqual(rowGingMarg,contrPerio.ListColRowsSelected[0].Row,"Down from MGJ skips auto Att Ging.");
		}

		[TestMethod]
		public void AttGing_IsNeverRed() {
			//No "red if" threshold in 24.4. Positive control: an MGJ of 0 is red (at or below the MGJ threshold of 0).
			long perioExamNum=InsertExam(DateTime.Today);
			InsertMeasure(perioExamNum,PerioSequenceType.MGJ,3,b:9,db:0);
			InsertMeasure(perioExamNum,PerioSequenceType.Probing,3,b:1,db:0);
			ContrPerio contrPerio=LoadChart();
			Assert.AreEqual("8",AttGingText(contrPerio,3,isFacial:true,PerioSurf.B));
			using Bitmap bitmap=new Bitmap(700,720);
			using(Graphics g=Graphics.FromImage(bitmap)) {
				contrPerio.DrawChart(g);
			}
			Assert.IsFalse(HasRedPixel(contrPerio,bitmap,Col(3,PerioSurf.B),contrPerio.GetTableRowForSection(0,PerioSequenceType.AttGing)),
				"An auto Att Ging of 8 is drawn in black.");
			Assert.IsTrue(HasRedPixel(contrPerio,bitmap,Col(3,PerioSurf.DB),contrPerio.GetTableRowForSection(0,PerioSequenceType.MGJ)),
				"Positive control: an MGJ of 0 is drawn red.");
		}

		private static bool HasRedPixel(ContrPerio contrPerio,Bitmap bitmap,int col,int row) {
			float[] arrayColPos=(float[])typeof(ContrPerio).GetField("_colPosArray",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(contrPerio);
			float[] arrayRowPos=(float[])typeof(ContrPerio).GetField("_rowPosArray",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(contrPerio);
			int left=(int)arrayColPos[col];
			int right=(int)arrayColPos[col+1];
			int top=(int)arrayRowPos[row];
			int bottom=(int)arrayRowPos[row+1];
			for(int x=left;x<right;x++) {
				for(int y=top;y<bottom;y++) {
					Color color=bitmap.GetPixel(x,y);
					if(color.R>180 && color.G<90 && color.B<90) {
						return true;
					}
				}
			}
			return false;
		}
		#endregion Attached gingiva (v24_4-chart-perio-attached-gingiva)

		#region Recession term (v24_4-chart-perio-recession-term)
		[TestMethod]
		public void RecessionTerm_OffLabelsGingMarg() {
			InsertExam(DateTime.Today);
			ContrPerio contrPerio=LoadChart();
			for(int section=0;section<4;section++) {
				Assert.AreEqual("Ging Marg",RowLabel(contrPerio,section,PerioSequenceType.GingMargin),"Section "+section);
			}
			Assert.AreEqual("Ging Marg",contrPerio.GetGingMarginTerm());
		}

		[TestMethod]
		public void RecessionTerm_OnLabelsRecession_UnlessTheExamHasHyperplasia() {
			PerioMeasures.SetUseRecessionTerm(true);
			long perioExamNumOld=InsertExam(DateTime.Today.AddDays(-100));
			InsertMeasure(perioExamNumOld,PerioSequenceType.GingMargin,8,mb:0,b:103,db:0);//+3: hyperplasia on the older exam only
			long perioExamNum=InsertExam(DateTime.Today);
			InsertMeasure(perioExamNum,PerioSequenceType.GingMargin,8,mb:1,b:2,db:1);
			ContrPerio contrPerio=LoadChart();
			for(int section=0;section<4;section++) {
				Assert.AreEqual("Recession",RowLabel(contrPerio,section,PerioSequenceType.GingMargin),"Section "+section);
			}
			Assert.IsFalse(contrPerio.HasHyperplasia(),"Hyperplasia is checked on the displayed exam only.");
			ContrPerio contrPerioOld=LoadChart(idxExam:0);
			Assert.IsTrue(contrPerioOld.HasHyperplasia());
			Assert.AreEqual("Ging Marg",RowLabel(contrPerioOld,0,PerioSequenceType.GingMargin));
			Assert.AreEqual("Ging Marg",contrPerioOld.GetGingMarginTerm());
		}

		[TestMethod]
		[Timeout(60000)]
		public void RecessionTerm_EnteringHyperplasiaRevertsTheLabel() {
			PerioMeasures.SetUseRecessionTerm(true);
			InsertExam(DateTime.Today);
			ContrPerio contrPerio=LoadChart();
			int countChanged=0;
			contrPerio.GingMarginTermChanged+=(sender,e) => countChanged++;
			contrPerio.AllowPerioEdit=true;
			int col=Col(8,PerioSurf.B);
			int rowGingMarg=contrPerio.GetTableRowForSection(0,PerioSequenceType.GingMargin);
			contrPerio.SetNewCell(col,rowGingMarg);
			contrPerio.ButtonPressedNumber(2);//2 mm of recession
			Assert.AreEqual("Recession",RowLabel(contrPerio,0,PerioSequenceType.GingMargin));
			Assert.AreEqual(0,countChanged);
			contrPerio.GingMargPlus=true;
			contrPerio.SetNewCell(col,rowGingMarg);
			contrPerio.ButtonPressedNumber(2);//+2: hyperplasia, stored as 102
			Assert.AreEqual("102",contrPerio.GetCellText(col,rowGingMarg));
			Assert.AreEqual("Ging Marg",RowLabel(contrPerio,0,PerioSequenceType.GingMargin));
			Assert.AreEqual("Ging Marg",RowLabel(contrPerio,3,PerioSequenceType.GingMargin),"Every gingival margin row follows.");
			Assert.AreEqual(1,countChanged,"FormPerio relabels its count row and check box on this event.");
			contrPerio.SetNewCell(col,rowGingMarg);
			contrPerio.KeyPressed(new KeyEventArgs(Keys.Delete));
			Assert.AreEqual("Recession",RowLabel(contrPerio,0,PerioSequenceType.GingMargin),"Clearing the only hyperplasia brings Recession back.");
			Assert.AreEqual(2,countChanged);
		}

		[TestMethod]
		public void RecessionTerm_StoredValuesNeverChange() {
			PerioMeasures.SetUseRecessionTerm(true);
			long perioExamNum=InsertExam(DateTime.Today);
			InsertMeasure(perioExamNum,PerioSequenceType.GingMargin,8,b:2);
			InsertMeasure(perioExamNum,PerioSequenceType.Probing,8,b:3);
			ContrPerio contrPerio=LoadChart();
			Assert.AreEqual("2",contrPerio.GetCellText(Col(8,PerioSurf.B),contrPerio.GetTableRowForSection(0,PerioSequenceType.GingMargin)));
			Assert.AreEqual("5",contrPerio.GetCellText(Col(8,PerioSurf.B),contrPerio.GetTableRowForSection(0,PerioSequenceType.CAL)),
				"Only the label changes: CAL is still probing plus gingival margin.");
		}
		#endregion Recession term (v24_4-chart-perio-recession-term)

		#region Implant marker (v24_4-chart-perio-implant-removed)
		private static long InsertImplantProc(int intTooth,ProcStat procStat,bool hideGraphics) {
			long codeNum=TestDb.GetLong("SELECT COALESCE(MAX(CodeNum),0) FROM procedurecode WHERE ProcCode='Z9001'");
			if(codeNum==0) {
				codeNum=OpenPmsDentalBusiness.Crud.ProcedureCodeCrud.Insert(new ProcedureCode { ProcCode="Z9001",Descript="Synthetic implant body",
					AbbrDesc="SynImpl",PaintType=ToothPaintingType.Implant });
				ProcedureCodes.RefreshCache();
			}
			return OpenPmsDentalBusiness.Crud.ProcedureCrud.Insert(new Procedure { PatNum=PAT_NUM,CodeNum=codeNum,ToothNum=intTooth.ToString(),
				ProcStatus=procStat,HideGraphics=hideGraphics,ProcDate=DateTime.Today });
		}

		private static string ToothLabel(ContrPerio contrPerio,int intTooth) {
			if(intTooth<=16) {
				return contrPerio.GetCellText(3*intTooth-1,contrPerio.GetTableRowForSection(0,PerioSequenceType.Mobility)+1);//the maxillary tooth row
			}
			return contrPerio.GetCellText(3*(33-intTooth)-1,contrPerio.GetTableRowForSection(3,PerioSequenceType.Mobility)-1);//the mandibular tooth row
		}

		[TestMethod]
		public void Implant_MarkerShowsUntilTheImplantIsRemoved() {
			InsertExam(DateTime.Today);
			long procNum=InsertImplantProc(3,ProcStat.C,hideGraphics:false);
			InsertImplantProc(30,ProcStat.EO,hideGraphics:false);
			ContrPerio contrPerio=LoadChart();
			Assert.AreEqual("3i",ToothLabel(contrPerio,3));
			Assert.AreEqual("30i",ToothLabel(contrPerio,30));
			Assert.AreEqual("4",ToothLabel(contrPerio,4));
			Assert.IsTrue(ContrPerio.IsImplant(3));
			//Removed: the implant procedure's graphics are hidden (Chart module, procedure edit, Hide Graphics).
			DataCore.NonQ("UPDATE procedurelog SET HideGraphics=1 WHERE ProcNum="+procNum);
			contrPerio=LoadChart();//the Perio Chart is opened again, which reloads the procedures
			Assert.AreEqual("3",ToothLabel(contrPerio,3),"No implant marker once the implant is removed.");
			Assert.IsFalse(ContrPerio.IsImplant(3),"So FormPerio treats the tooth as missing again.");
			Assert.AreEqual("30i",ToothLabel(contrPerio,30));
		}

		[TestMethod]
		public void Implant_ReplacementAfterRemoval_ShowsTheMarker() {
			InsertExam(DateTime.Today);
			InsertImplantProc(3,ProcStat.EO,hideGraphics:true);
			InsertImplantProc(3,ProcStat.C,hideGraphics:false);
			ContrPerio contrPerio=LoadChart();
			Assert.AreEqual("3i",ToothLabel(contrPerio,3));
		}
		#endregion Implant marker (v24_4-chart-perio-implant-removed)
	}
}
