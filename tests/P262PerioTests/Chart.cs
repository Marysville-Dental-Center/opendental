// Helpers that build a Perio Chart grid (OpenPmsDental.ContrPerio) on the scratch database, the way FormPerio.FillGrid does, and drive it with
// the mouse off screen: ContrPerioDriven raises the control's own mouse handlers at the centre of grid cells. Synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using WpfControls.UI;

namespace P262PerioTests {
	///<summary>The real grid, with its protected mouse handlers callable at the centre of a cell.</summary>
	public class ContrPerioDriven : ContrPerio {
		private static readonly MethodInfo _methodInfoGetBounds=typeof(ContrPerio).GetMethod("GetBounds",BindingFlags.NonPublic|BindingFlags.Instance);

		///<summary>The centre of a cell in control pixels, as ContrPerio lays it out.</summary>
		public Point GetCellCenter(int col,int row) {
			RectangleF rectangleF=(RectangleF)_methodInfoGetBounds.Invoke(this,new object[] { col,row });
			return new Point((int)(rectangleF.X+rectangleF.Width/2),(int)(rectangleF.Y+rectangleF.Height/2));
		}

		///<summary>Mouse down and up on one cell without moving: a click.</summary>
		public void ClickCell(int col,int row) {
			Point point=GetCellCenter(col,row);
			OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,point.X,point.Y,0));
			OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,point.X,point.Y,0));
		}

		///<summary>Mouse down on the first cell, the mouse moved over every cell in turn, and up on the last one: a drag.</summary>
		public void Drag(params ColRow[] arrayColRows) {
			Point point=GetCellCenter(arrayColRows[0].Col,arrayColRows[0].Row);
			OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,point.X,point.Y,0));
			for(int i=0;i<arrayColRows.Length;i++) {
				point=GetCellCenter(arrayColRows[i].Col,arrayColRows[i].Row);
				OnMouseMove(new MouseEventArgs(MouseButtons.Left,0,point.X,point.Y,0));
			}
			OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,point.X,point.Y,0));
		}

		///<summary>Clicks the number of a tooth, which selects it (or unselects it when it was selected).</summary>
		public void ClickToothNumber(int intTooth) {
			if(intTooth<=16) {
				ClickCell(3*intTooth-1,Chart.RowToothNumbers(this,isMaxillary:true));
				return;
			}
			ClickCell(3*(33-intTooth)-1,Chart.RowToothNumbers(this,isMaxillary:false));
		}
	}

	public static class Chart {
		public const long PAT_NUM=3001;

		///<summary>Whole seconds, as the database stores them, so SaveCurExam sees the exam unchanged by another user.</summary>
		public static long InsertExam(DateTime examDate) {
			return PerioExams.Insert(new PerioExam { PatNum=PAT_NUM,ExamDate=examDate,DateTMeasureEdit=new DateTime(2026,1,5,8,30,0) });
		}

		///<summary>Inserts a measure row directly through the CRUD layer, the way an existing database holds it.</summary>
		public static void InsertMeasure(long perioExamNum,PerioSequenceType perioSequenceType,int intTooth,int mb=-1,int b=-1,int db=-1,int ml=-1,
			int l=-1,int dl=-1,int toothValue=-1)
		{
			OpenPmsDentalBusiness.Crud.PerioMeasureCrud.Insert(new PerioMeasure { PerioExamNum=perioExamNum,SequenceType=perioSequenceType,IntTooth=intTooth,
				ToothValue=toothValue,MBvalue=mb,Bvalue=b,DBvalue=db,MLvalue=ml,Lvalue=l,DLvalue=dl });
		}

		///<summary>Creates the grid at 96 dpi with the patient's exams, selects one (the latest by default) and loads it, as FormPerio.FillGrid does,
		///with the two settings of Open Dental 26.2. Editing is allowed unless allowEdit is false.</summary>
		public static ContrPerioDriven Load(bool showPreviousExams=true,bool showPreviousProbing=true,int idxExam=-1,bool allowEdit=true) {
			ContrPerioDriven contrPerio=Create(idxExam,allowEdit);
			contrPerio.SetShowPrevious(showPreviousExams,showPreviousProbing);
			contrPerio.LoadData();
			return contrPerio;
		}

		///<summary>Creates the grid with the patient's exams and selects one, without loading. The caller sets what to show, then calls LoadData.
		///</summary>
		public static ContrPerioDriven Create(int idxExam=-1,bool allowEdit=true) {
			FormOpenDental.PatNumCur=PAT_NUM;//ContrPerio reads the patient's procedures for the implant marker.
			ContrPerioDriven contrPerio=new ContrPerioDriven();
			contrPerio.LayoutManager.Is96dpi=true;
			contrPerio.Size=new Size(608,685);//as in FormPerio.designer.cs
			Assert.IsTrue(contrPerio.GetRectangleBoundsShowing().Height>2,"The grid was not laid out.");
			contrPerio.ListPerioExams=PerioExams.Refresh(PAT_NUM);
			contrPerio.ListPerioMeasures=PerioMeasures.GetForPatient(PAT_NUM);
			if(idxExam==-1) {
				idxExam=contrPerio.ListPerioExams.Count-1;
			}
			contrPerio.IdxExamSelected=idxExam;
			contrPerio.AllowPerioEdit=allowEdit;
			return contrPerio;
		}

		///<summary>The grid column of a site, as ContrPerio.GetCell computes it.</summary>
		public static int Col(int intTooth,PerioSurf perioSurf) {
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

		///<summary>The three grid columns of a tooth, left to right.</summary>
		public static int[] ColsOfTooth(int intTooth) {
			int colLeft=intTooth<=16 ? intTooth*3-2 : (33-intTooth)*3-2;
			return new[] { colLeft,colLeft+1,colLeft+2 };
		}

		///<summary>The row of a sequence type in a section (0 maxillary facial, 1 maxillary lingual, 2 mandibular lingual, 3 mandibular facial). For
		///probing, the selected exam's row.</summary>
		public static int Row(ContrPerio contrPerio,int section,PerioSequenceType perioSequenceType) {
			int row=contrPerio.GetTableRowForSection(section,perioSequenceType);
			Assert.AreNotEqual(-1,row,"No "+perioSequenceType+" row in section "+section);
			return row;
		}

		///<summary>The section of a tooth's side, as ContrPerio.GetSection(intTooth,isFacial) gives it.</summary>
		public static int Section(ContrPerio contrPerio,int intTooth,bool isFacial) {
			return contrPerio.GetSection(intTooth,isFacial);
		}

		public static int RowToothNumbers(ContrPerio contrPerio,bool isMaxillary) {
			if(isMaxillary) {
				return contrPerio.GetTableRowForSection(0,PerioSequenceType.Mobility)+1;
			}
			return contrPerio.GetTableRowForSection(3,PerioSequenceType.Mobility)-1;
		}

		///<summary>The cell of one site in a row of the tooth's side.</summary>
		public static ColRow Cell(ContrPerio contrPerio,int intTooth,PerioSurf perioSurf,PerioSequenceType perioSequenceType) {
			bool isFacial=perioSurf==PerioSurf.MB || perioSurf==PerioSurf.B || perioSurf==PerioSurf.DB;
			return new ColRow(Col(intTooth,perioSurf),Row(contrPerio,Section(contrPerio,intTooth,isFacial),perioSequenceType));
		}

		///<summary>The six probing sites of a tooth on the selected exam.</summary>
		public static List<ColRow> ProbingSites(ContrPerio contrPerio,int intTooth) {
			return new[] { PerioSurf.MB,PerioSurf.B,PerioSurf.DB,PerioSurf.ML,PerioSurf.L,PerioSurf.DL }
				.Select(x => Cell(contrPerio,intTooth,x,PerioSequenceType.Probing)).ToList();
		}

		public static BleedingFlags Flags(ContrPerio contrPerio,ColRow colRow) {
			return contrPerio.GetPerioCellFromColRow(colRow).Bleeding;
		}

		public static string OldText(ContrPerio contrPerio,ColRow colRow) {
			return contrPerio.GetPerioCellFromColRow(colRow).OldText??"";
		}

		///<summary>The rows whose label (the date column) is the given text, for example an exam date.</summary>
		public static List<int> RowsLabeled(ContrPerio contrPerio,string label) {
			List<int> listRows=new List<int>();
			for(int row=0;row<100;row++) {
				string text;
				try {
					text=contrPerio.GetCellText(0,row);
				}
				catch(IndexOutOfRangeException) {
					break;
				}
				if(text==label) {
					listRows.Add(row);
				}
			}
			return listRows;
		}

		///<summary>The cells of the given columns in one row.</summary>
		public static ColRow[] CellsInRow(int row,IEnumerable<int> listCols) {
			return listCols.Select(x => new ColRow(x,row)).ToArray();
		}
	}
}
