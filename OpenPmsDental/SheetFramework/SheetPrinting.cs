using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace OpenPmsDental {
	public class SheetPrinting {
		///<summary>Print margin of the default printer. only used in page break calulations, and only top and bottom are used.</summary>
		private static Margins _printMargin=new Margins(0,0,40,60);//jordan static only because it's an unchanging val.

		#region Properties
		///<summary>The treatment finder needs this so that it can use the same Margins in its page calculations.</summary>
		public static Margins PrintMargin {
			get {
				return _printMargin;
			}
		}
		#endregion Properties

		#region Methods - Public Printing
		///<summary>If printing a statement, use the polymorphism that takes a DataSet otherwise this method will make another call to the db.</summary>
		public static void Print(Sheet sheet,int copies=1,bool isRxControlled=false,Statement stmt=null,MedLab medLab=null,bool isPrintDocument=true,bool isPreviewMode=false,bool isPrintRemote=false,long printerNumOverride=0) {
			SheetPrintingJob sheetPrintingJob=new SheetPrintingJob{IsRemotePrintingJob=isPrintRemote,PrinterNumOverride=printerNumOverride};
			sheetPrintingJob.Print(sheet,copies,isRxControlled,stmt,medLab,isPrintDocument,isPreviewMode);
		}

		///<summary>Prints a filled Check sheet (Open Dental 25.3). The printer window writes the usual Printing audit entry once a printer is chosen;
		///for a check it reads, for example, "Check #1001, $1,250.00, Payee sheet from (date) printed". Check sheets are never saved, so the
		///description is only used for that entry and the print job name.</summary>
		public static void PrintCheck(Sheet sheet,string auditDescription) {
			if(!string.IsNullOrWhiteSpace(auditDescription)) {
				sheet.Description=auditDescription;
			}
			Print(sheet);
		}

		///<Summary>DataSet should be prefilled with AccountModules.GetAccount() before calling this method if printing a statement.  Returns null if document failed to print.</Summary>
		public static void Print(Sheet sheet,DataSet dataSet,int copies=1,bool isRxControlled=false,Statement stmt=null,MedLab medLab=null,
			bool isPrintDocument=true,bool isPreviewMode=false,bool isPrintRemote=false,long printerNumOverride=0)
		{
			SheetPrintingJob sheetPrintingJob=new SheetPrintingJob{IsRemotePrintingJob=isPrintRemote,PrinterNumOverride=printerNumOverride};
			sheetPrintingJob.Print(sheet,dataSet,copies,isRxControlled,stmt,medLab);
		}

		public static void PrintBatch(List<Sheet> sheetBatch){
			SheetPrintingJob sheetPrintingJob=new SheetPrintingJob();
			sheetPrintingJob.PrintBatch(sheetBatch);
		}

		///<summary>Performs validation on each Rx before printing.  If one of the given Rx does not pass validation, then the entire batch is aborted, because it is easy for user to fix errors.</summary>
		public static void PrintMultiRx(List<RxPat> listRxs) {
			SheetPrintingJob sheetPrintingJob=new SheetPrintingJob();
			sheetPrintingJob.PrintMultiRx(listRxs);
		}

		///<summary>Performs validation on the given Rx before printing.  Returns false if validation failed.</summary>
		public static bool PrintRx(Sheet sheet,RxPat rx,bool isRemotePrint=false,long printerNumOverride=0){
			SheetPrintingJob sheetPrintingJob=new SheetPrintingJob{IsRemotePrintingJob=isRemotePrint,PrinterNumOverride=printerNumOverride};
			return sheetPrintingJob.PrintRx(sheet,rx);
		}

		///<summary>Validates one Rx.  Returns a string of error messages.  Blank string indicates no errors.
		///Some Rx require certain data to be present when printing.</summary>
		public static string ValidateRxForSheet(RxPat rx) {
			if(!PrefC.GetBool(PrefName.RxHasProc)) {
				return "";//The global preference allows the user to completely disable Rx ProcCode validation, even if some Rx are flagged as required.
			}
			if(Clinics.ClinicNum!=0) {//Not HQ
				Clinic clinic=Clinics.GetClinic(Clinics.ClinicNum);
				if(!clinic.HasProcOnRx) {
					//The clinic option allows the user to completely disable Rx ProcCode validation for one clinic.
					//A customer who needs this feature has some clinics in states which require ProCode and some clinics in states which do not.
					return "";
				}
			}
			if(!rx.IsProcRequired) {
				return "";//No ProCode validation required for this specific Rx.
			}
			StringBuilder sb=new StringBuilder();
			if(rx.ProcNum==0) {
				if(sb.Length > 0) {
					sb.Append(", ");
				}
				sb.Append(Lans.g("Sheets","Procedure"));
			}
			if(rx.DaysOfSupply<=0) {
				if(sb.Length > 0) {
					sb.Append(", ");
				}
				sb.Append(Lans.g("Sheets","Days of Supply"));
			}
			return sb.ToString();
		}
		#endregion Methods - Public Printing

		#region Methods - Drawing
		///<Summary>DataSet should be prefilled with AccountModules.GetAccount() before calling this method if printing a statement.  If drawing to screen, pass in the current ms scale, like 1.5.</Summary>
		public static void DrawFieldGrid(SheetField field,Sheet sheet,Graphics g,XGraphics gx,DataSet dataSet,Statement stmt,MedLab medLab,
			bool isPrinting=false,Patient pat=null,Patient patGuar=null,float scaleMS=1) {
			SheetDrawingJob sheetDrawingJob=new SheetDrawingJob();
			sheetDrawingJob.DrawFieldGrid(field,sheet,g,gx,dataSet,stmt,medLab,isPrinting,pat,patGuar,scaleMS);
		}

		///<summary>Generates an Image of the ToothChart.  Set isForSheet=true for dimensions and colors appropriate for Sheets, false for dimensions and colors appropriate for display in Patient Dashboard (matches dimensions for .</summary>
		public static Image GetToothChartHelper(long patNum,bool showCompleted,TreatPlan treatPlan=null,List<Procedure> listProceduresFilteredOverride=null
			,bool isInPatientDashboard=false,Patient patCur=null,List<Appointment> listAppts=null) 
		{
			//This method is designed to stay in the OD.exe.
			int colorBackgroundIndex=14;
			int colorTextIndex=15;
			int width=500;
			int height=370;
			if(isInPatientDashboard) {
				colorBackgroundIndex=10;
				colorTextIndex=11;
				width=410;
				height=307;
			}
			Form formOldBitmap=null;
			SparksToothChart.ToothChartWrapper toothChartWrapper=new SparksToothChart.ToothChartWrapper();
			ToothChartRelay toothChartRelay= new ToothChartRelay(false);
			toothChartRelay.SetToothChartWrapper(toothChartWrapper);
			toothChartWrapper.Size=new Size(width,height);
			ToothChartRelay.ApplyComputerSettings(toothChartWrapper,ToothChartSettingsUse.StoredMode);
			formOldBitmap=new Form();
			formOldBitmap.FormBorderStyle=FormBorderStyle.None;
			formOldBitmap.Size=new Size(width,height);
			formOldBitmap.Controls.Add(toothChartWrapper);
			//formOldBitmap.Show();
			toothChartRelay.SetToothNumberingNomenclature((ToothNumberingNomenclature)PrefC.GetInt(PrefName.UseInternationalToothNumbers));
			List<Def> listDefs=Defs.GetDefsForCategory(DefCat.ChartGraphicColors);
			toothChartRelay.ColorBackgroundMain=listDefs[colorBackgroundIndex].ItemColor;
			toothChartRelay.ColorText=listDefs[colorTextIndex].ItemColor;
			toothChartRelay.ResetTeeth();
			toothChartRelay.SetOrthoMode(false);
			List<ToothInitial> toothInitialList=patNum==0?new List<ToothInitial>():ToothInitials.GetPatientData(patNum);
			ToothChartPainter.PaintSheetToothInitials(toothChartRelay,toothInitialList);
			//We passed in a list of procs we want to see on the toothchart.  Use that.
			List<Procedure> listProceduresFiltered=new List<Procedure>();
			if(listProceduresFilteredOverride!=null) {
				listProceduresFiltered=listProceduresFilteredOverride;
			}
			else if(patNum>0) {//Custom list of procedures was not passed in, go get all for the patient like we always have.
				List<Procedure> listProceduresAll=Procedures.Refresh(patNum);
				listProceduresFiltered=OpenPmsDentalBusiness.SheetPrinting.FilterProceduresForToothChart(listProceduresAll,treatPlan,showCompleted);
			}
			listProceduresFiltered.Sort(OpenPmsDentalBusiness.SheetPrinting.CompareProcListFiltered);
			//Draw tooth chart
			ToothChartPainter.PaintSheetProcs(toothChartRelay,listProceduresFiltered,toothInitialList,isInPatientDashboard,patCur,listAppts);
			List<OrthoHardware> listOrthoHardwares=OrthoHardwares.GetPatientData(patNum);
			ToothChartPainter.PaintSheetOrthoHardware(toothChartRelay,listOrthoHardwares);
			toothChartWrapper.AutoFinish=true;
			toothChartRelay.EndUpdate();
			Image retVal=toothChartRelay.GetBitmap();
			formOldBitmap.Close();//automatically disposes, too
			return retVal;
		}

		/*
		///<summary>If drawing grids for a statement, use the overload that takes a DataSet otherwise this method will make another call to the db.</summary>
		public static void DrawFieldGrid(SheetField field,Sheet sheet,Graphics g,XGraphics gx,Statement stmt=null,MedLab medLab=null) {
			DataSet dataSet=null;
			if(sheet.SheetType==SheetTypeEnum.Statement && stmt!=null) {
				//This should never get hit.  This line of code is here just in case I forgot to update a random spot in our code.
				//Worst case scenario we will end up calling the database a few extra times for the same data set.
				//It use to call this method many, many times so anything is an improvement at this point.
				dataSet=AccountModules.GetAccount(stmt.PatNum,stmt,doShowHiddenPaySplits:stmt.IsReceipt);
			}
		}*/
		#endregion Methods - Drawing

		#region Methods - CreatePdf
		///<summary>Creates a file where fullFileName is a local path. If fullFileName is in the A to Z folder, 
		///pass in fullFileName as a temp file and then upload that file to the cloud.</summary>
		public static PdfDocument CreatePdf(Sheet sheet,string fullFileName=null,Statement stmt=null,MedLab medLab=null,DataSet dataSet=null,Patient pat=null,Patient patGuar=null,bool doSave=true) {
			SheetDrawingJob sheetDrawingJob=new SheetDrawingJob();
			PdfDocument pdf=sheetDrawingJob.CreatePdf(sheet,stmt,medLab,dataSet,pat,patGuar);
			if (doSave) {
				SavePdfToFile(pdf,fullFileName);
			}
			return pdf;
		}

		/// <summary>This has a built in try/catch that uses a FriendlyException™.
		/// <br>Returns true if file was successfully saved. False if not.</br></summary>
		public static bool SavePdfToFile(PdfDocument pdf, string fileNameAndPath) {
			try {
				SheetDrawingJob.SavePdfToFile(pdf,fileNameAndPath);
			}
			catch (Exception ex) {
				FriendlyException.Show(Lans.g("PdfDocument","An error has occurred while trying to create this document"),ex,true);
				return false;
			}
			return true;
		}

		///<summary>Called for every page that is generated for a PDF document. Pages and yPos must be tracked outside of this function. See also pd_PrintPage.  DataSet should be prefilled with AccountModules.GetAccount() before calling this method if making a statement.</Summary>
		public static int CreatePdfPage(Sheet sheet,PdfPage page,DataSet dataSet,Patient pat,Patient patGuar,int yPos,int pagesPrinted) {
			SheetDrawingJob sheetDrawingJob=new SheetDrawingJob();
			return sheetDrawingJob.CreatePdfPage(sheet,page,dataSet,pat,patGuar,yPos,pagesPrinted);
		}
		#endregion Methods - CreatePdf

		///<summary>Draws all images from the sheet onto the graphic passed in.  Used when printing and rendering the sheet fill edit window.</summary>
		//public static void DrawImages(Sheet sheet,Graphics graphic,bool drawAll=false) {
		//	DrawImages(sheet,graphic,null,drawAll);
		//}

		//this was no longer being used, so comment it out

		/*public void SetZero() {
			_sheetsPrinted=0;
			_yPosPrint=0;
		}*/

		/////<summary>Not used. This code is copied and pasted in several locations. Easiest to find by searching for "info.Verb="print";"</summary>
		//public static void PrintStatement(object parameters) {
		//	List<object> listParams=(List<object>)parameters;
		//	SheetDef sheetDef=(SheetDef)listParams[0];
		//	Statement stmt=(Statement)listParams[1];
		//	string filePath=(string)listParams[2];
		//	try {
		//		ProcessStartInfo info=new ProcessStartInfo();
		//		info.Arguments = "\"" + Printers.GetForSit(PrintSituation.Statement).PrinterName + "\"";
		//		info.UseShellExecute = true;
		//		info.Verb="PrintTo";
		//		info.FileName=filePath;
		//		info.CreateNoWindow=true;
		//		info.WindowStyle=ProcessWindowStyle.Hidden;
		//		Process p=new Process();
		//		p.StartInfo=info;
		//		p.Start();
		//		p.WaitForInputIdle();
		//		System.Threading.Thread.Sleep(3000);
		//		if(p.CloseMainWindow()==false) {
		//			p.Kill();
		//		}
		//	}
		//	catch(Exception ex) {
		//		//Must restet sheet, as PDF printing modifies fields.
		//		Sheet sheet=SheetUtil.CreateSheet(sheetDef,stmt.PatNum,stmt.HidePayment);
		//		SheetFiller.FillFields(sheet,stmt);
		//		SheetUtil.CalculateHeights(sheet,GraphicsHelper.GM,stmt);
		//		SheetPrinting.Print(sheet,1,false,stmt);//use GDI+ printing, which is slightly different than the pdf.
		//	}
		//}
	}
}
