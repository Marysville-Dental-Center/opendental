using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using CodeBase;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using System.Windows.Media.Media3D;
using Newtonsoft.Json;

namespace OpenPmsDentalBusiness.SheetFramework{
	public class ToothChartHelper{
		//The image used to be drawn by Open Dental's closed DirectX 11 chart (Sparks3D.dll), which this program does not ship (removed 2026-11-01).
		//SparksToothChart, the only chart now, is a WinForms control in a project that references this one, so it cannot draw here. OpenPmsDental.exe
		//draws its own tooth chart images through SheetPrinting.GetToothChartHelper. The callers here are the treatment plan and perio PDFs of the
		//API and mobile paths and procedure-complete automation, which OpenPmsDental.exe does not use; they get a plain image that says the chart is
		//not available, so a sheet still prints.
		
		///<summary>Do not call this from OpenPmsDental.exe. There's a better version there in SheetPrinting.  This generates an Image of the ToothChart.  Set isForSheet=true for dimensions and colors appropriate for Sheets, false for dimensions and colors appropriate for display in Patient Dashboard (matches dimensions for). Set isForWinForms to true if there is an associated window handle. Set isForPerio to true in order to get an image of the Perio Chart.</summary>
		public static Image GetImage(long patNum,bool showCompleted,TreatPlan treatPlan=null,List<Procedure> listProceduresFilteredOverride=null
			,bool isInPatientDashboard=false,bool isForWinForms=false,bool isForPerio=false,PerioExam perioExam=null
			,List<PerioMeasure> listPerioMeasures=null,int width=500,int height=370,bool isSmaller=false,bool isForChartModule=false) 
		{
			//Get all the data needed
			List<Def> listDefs=Defs.GetDefsForCategory(DefCat.ChartGraphicColors);
			ToothNumberingNomenclature toothNumberingNomenclature=(ToothNumberingNomenclature)PrefC.GetInt(PrefName.UseInternationalToothNumbers);
			List<ToothInitial> toothInitialList=patNum==0?new List<ToothInitial>():ToothInitials.GetPatientData(patNum);
			//We passed in a list of procs we want to see on the toothchart.  Use that.
			List<Procedure> listProceduresFiltered=new List<Procedure>();
			if(listProceduresFilteredOverride!=null) {
				listProceduresFiltered=listProceduresFilteredOverride;
			}
			else if(patNum>0) {//Custom list of procedures was not passed in, go get all for the patient like we always have.
				List<Procedure> listProceduresAll=Procedures.Refresh(patNum);
				if(!isForChartModule) {
					listProceduresFiltered=SheetPrinting.FilterProceduresForToothChart(listProceduresAll,treatPlan,showCompleted);
				}
				else {
					listProceduresFiltered=listProceduresAll;
				}
			}
			listProceduresFiltered.Sort(SheetPrinting.CompareProcListFiltered);
			if(listPerioMeasures==null) {
				listPerioMeasures=new List<PerioMeasure>();
				if(perioExam!=null) {
					listPerioMeasures=PerioMeasures.GetAllForExam(perioExam.PerioExamNum);
				}
			}
			//Setup perio lists
			Dictionary<PrefName,Color> dictPrefToColor=new Dictionary<PrefName,Color>();
			List<Procedure> listProcsForPerio=new List<Procedure>();
			List<ProcedureCode> listProcedureCodesForPerio=new List<ProcedureCode>();
			List<Def> listDefsPerio=new List<Def>();
			if(isForPerio){
				listDefsPerio=Defs.GetDefsForCategory(DefCat.MiscColors,true);
				dictPrefToColor.Add(PrefName.PerioColorCAL,PrefC.GetColor(PrefName.PerioColorCAL));
				dictPrefToColor.Add(PrefName.PerioColorFurcations,PrefC.GetColor(PrefName.PerioColorFurcations));
				dictPrefToColor.Add(PrefName.PerioColorFurcationsRed,PrefC.GetColor(PrefName.PerioColorFurcationsRed));
				dictPrefToColor.Add(PrefName.PerioColorGM,PrefC.GetColor(PrefName.PerioColorGM));
				dictPrefToColor.Add(PrefName.PerioColorMGJ,PrefC.GetColor(PrefName.PerioColorMGJ));
				dictPrefToColor.Add(PrefName.PerioColorProbing,PrefC.GetColor(PrefName.PerioColorProbing));
				dictPrefToColor.Add(PrefName.PerioColorProbingRed,PrefC.GetColor(PrefName.PerioColorProbingRed));
				listProcsForPerio=Procedures.Refresh(patNum);
				listProcedureCodesForPerio=ProcedureCodes.GetWhere(x=>x.CodeNum.In(listProcsForPerio.Select(x=>x.CodeNum).ToArray()));
			}
			ToothChartHelperData args=GetToothChartData(listDefs,
				listProceduresFiltered,
				toothInitialList,
				toothNumberingNomenclature,
				listPerioMeasures,
				dictPrefToColor,
				listDefsPerio,
				listProcsForPerio,
				listProcedureCodesForPerio,
				isInPatientDashboard,
				isForWinForms,
				isForPerio,
				width,
				height,
				isSmaller,
				isForChartModule);
			//Actually set the stage / draw the tooth image
			return GetToothChartImageProcess(args.ListDefs,args.ListProceduresFiltered,args.ToothInitialList,args.ToothNumberingNomenclature,args.ListPerioMeasures,
				args.DictPrefToColor,args.ListDefsPerio,args.ListProcsForPerio,args.ListProcedureCodesForPerio,args.ListProcedureCodesForProcs,args.IsInPatientDashboard,
				args.IsForWinForms,args.IsForPerio,args.Width,args.Height,args.IsSmaller,args.IsForChartModule);
		}

		///<summary>Do not call this from OpenPmsDental.exe. There's a better version there in SheetPrinting.  This generates an Image of the ToothChart.  Set isForSheet=true for dimensions and colors appropriate for Sheets, false for dimensions and colors appropriate for display in Patient Dashboard (matches dimensions for). Set isForWinForms to true if there is an associated window handle. Set isForPerio to true in order to get an image of the Perio Chart.</summary>
		public static ToothChartHelperData GetDAta(long patNum,bool showCompleted,TreatPlan treatPlan=null,List<Procedure> listProceduresFilteredOverride=null
			,bool isInPatientDashboard=false,bool isForWinForms=false,bool isForPerio=false,PerioExam perioExam=null
			,List<PerioMeasure> listPerioMeasures=null,int width=500,int height=370,bool isSmaller=false,bool isForChartModule=false) 
		{
			//Get all the data needed
			List<Def> listDefs=Defs.GetDefsForCategory(DefCat.ChartGraphicColors);
			ToothNumberingNomenclature toothNumberingNomenclature=(ToothNumberingNomenclature)PrefC.GetInt(PrefName.UseInternationalToothNumbers);
			List<ToothInitial> toothInitialList=patNum==0?new List<ToothInitial>():ToothInitials.GetPatientData(patNum);
			//We passed in a list of procs we want to see on the toothchart.  Use that.
			List<Procedure> listProceduresFiltered=new List<Procedure>();
			if(listProceduresFilteredOverride!=null) {
				listProceduresFiltered=listProceduresFilteredOverride;
			}
			else if(patNum>0) {//Custom list of procedures was not passed in, go get all for the patient like we always have.
				List<Procedure> listProceduresAll=Procedures.Refresh(patNum);
				if(!isForChartModule) {
					listProceduresFiltered=SheetPrinting.FilterProceduresForToothChart(listProceduresAll,treatPlan,showCompleted);
				}
				else {
					listProceduresFiltered=listProceduresAll;
				}
			}
			listProceduresFiltered.Sort(SheetPrinting.CompareProcListFiltered);
			if(listPerioMeasures==null) {
				listPerioMeasures=new List<PerioMeasure>();
				if(perioExam!=null) {
					listPerioMeasures=PerioMeasures.GetAllForExam(perioExam.PerioExamNum);
				}
			}
			//Setup perio lists
			Dictionary<PrefName,Color> dictPrefToColor=new Dictionary<PrefName,Color>();
			List<Procedure> listProcsForPerio=new List<Procedure>();
			List<ProcedureCode> listProcedureCodesForPerio=new List<ProcedureCode>();
			List<Def> listDefsPerio=new List<Def>();
			if(isForPerio){
				listDefsPerio=Defs.GetDefsForCategory(DefCat.MiscColors,true);
				dictPrefToColor.Add(PrefName.PerioColorCAL,PrefC.GetColor(PrefName.PerioColorCAL));
				dictPrefToColor.Add(PrefName.PerioColorFurcations,PrefC.GetColor(PrefName.PerioColorFurcations));
				dictPrefToColor.Add(PrefName.PerioColorFurcationsRed,PrefC.GetColor(PrefName.PerioColorFurcationsRed));
				dictPrefToColor.Add(PrefName.PerioColorGM,PrefC.GetColor(PrefName.PerioColorGM));
				dictPrefToColor.Add(PrefName.PerioColorMGJ,PrefC.GetColor(PrefName.PerioColorMGJ));
				dictPrefToColor.Add(PrefName.PerioColorProbing,PrefC.GetColor(PrefName.PerioColorProbing));
				dictPrefToColor.Add(PrefName.PerioColorProbingRed,PrefC.GetColor(PrefName.PerioColorProbingRed));
				listProcsForPerio=Procedures.Refresh(patNum);
				listProcedureCodesForPerio=ProcedureCodes.GetWhere(x=>x.CodeNum.In(listProcsForPerio.Select(x=>x.CodeNum).ToArray()));
			}
			ToothChartHelperData args=GetToothChartData(listDefs,
				listProceduresFiltered,
				toothInitialList,
				toothNumberingNomenclature,
				listPerioMeasures,
				dictPrefToColor,
				listDefsPerio,
				listProcsForPerio,
				listProcedureCodesForPerio,
				isInPatientDashboard,
				isForWinForms,
				isForPerio,
				width,
				height,
				isSmaller,
				isForChartModule);
			//Actually set the stage / draw the tooth image
			return args;
		}

		public static Image GetToothChartImageProcess(List<Def> ListDefs,List<Procedure> ListProceduresFiltered,List<ToothInitial> ToothInitialList,OpenPmsDentalBusiness.ToothNumberingNomenclature ToothNumberingNomenclature,
			List<OpenPmsDentalBusiness.PerioMeasure> ListPerioMeasures, Dictionary<PrefName,Color> DictPrefToColor,List<Def> ListDefsPerio,List<Procedure> ListProcsForPerio,List<ProcedureCode> ListProcedureCodesForPerio,
			List<ProcedureCode> ListProcedureCodesForProcs,bool IsInPatientDashboard,bool IsForWinForms,bool IsForPerio,int Width,int Height,bool IsSmaller,bool IsForChartModule){
			int colorBackgroundIndex=14;
			int colorTextIndex=15;
			//determine the width and height for the image based on what it is for (perio, chart, etc)
			if(IsForPerio) {
				//The width and height come from FormPerioGraphical in which it excplicitly says not to change width and height of the ToothChartWrapper
				Width=649;
				Height=696;
			}
			else if(IsInPatientDashboard) {
				colorBackgroundIndex=10;
				colorTextIndex=11;
				Width=410;
				Height=307;
			}
			else if(IsForChartModule) {
				colorBackgroundIndex=10;
				colorTextIndex=11;
				if(Width<500 || Height<370) {
					Width=500;
					Height=370;
				}
				//The scale of the width of the canvas to the bitmap, which could be smaller than the bitmap
				double scaleWidth=Width/500;
				//Same explanation as above, but for the height 
				double scaleHeight=Height/370;
				//Doesn't matter which scale we choose, just needs to be the scaled correctly, so pick that
				double scale=scaleWidth<scaleHeight?scaleWidth:scaleHeight;
				Width=Width*(int)scale;
				Height=Height*(int)scale;
			}
			Color colorBackground=Color.White;
			Color colorText=Color.Black;
			if(!IsForPerio && ListDefs!=null && ListDefs.Count>Math.Max(colorBackgroundIndex,colorTextIndex)) {
				colorBackground=ListDefs[colorBackgroundIndex].ItemColor;
				colorText=ListDefs[colorTextIndex].ItemColor;
			}
			return GetImageNotAvailable(Width,Height,colorBackground,colorText);
		}

		///<summary>The text drawn on the image that GetImage returns.</summary>
		public const string TEXT_NOT_AVAILABLE="Tooth chart image not available";

		///<summary>A plain image of the given size in the chart's colors with TEXT_NOT_AVAILABLE centered on it. Never returns null, so sheets and
		///PDFs that expect an image still print.</summary>
		public static Image GetImageNotAvailable(int width,int height,Color colorBackground,Color colorText) {
			Bitmap bitmap=new Bitmap(Math.Max(1,width),Math.Max(1,height));
			using Graphics g=Graphics.FromImage(bitmap);
			g.Clear(colorBackground);
			using Font font=new Font(FontFamily.GenericSansSerif,10f);
			using SolidBrush solidBrush=new SolidBrush(colorText);
			using StringFormat stringFormat=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center };
			g.DrawString(TEXT_NOT_AVAILABLE,font,solidBrush,new RectangleF(0,0,bitmap.Width,bitmap.Height),stringFormat);
			return bitmap;
		}

		public static ToothChartHelperData GetToothChartData(List<Def> listDefs,List<Procedure> listProceduresFiltered,List<ToothInitial> toothInitialList,
		ToothNumberingNomenclature toothNumberingNomenclature,List<PerioMeasure> listPerioMeasures, Dictionary<PrefName,Color> dictPrefToColor,List<Def> listDefsPerio,
		List<Procedure> listProcsForPerio,List<ProcedureCode> listProcedureCodesForPerio,
		bool isInPatientDashboard=false,bool isForWinForms=false,bool isForPerio=false,
		int width=500,int height=370,bool isSmaller=false,bool isForChartModule=false) {
			List<ProcedureCode> listProcedureCodesForProcs=ProcedureCodes.GetWhere(x=>x.CodeNum.In(listProceduresFiltered.Select(x=>x.CodeNum).ToArray()));
			ToothChartHelperData toothChartHelperData=new ToothChartHelperData();
			toothChartHelperData.ListDefs=listDefs;
			toothChartHelperData.ListProceduresFiltered=listProceduresFiltered;
			toothChartHelperData.ToothInitialList=toothInitialList;
			toothChartHelperData.ToothNumberingNomenclature=toothNumberingNomenclature;
			toothChartHelperData.ListPerioMeasures=listPerioMeasures;
			toothChartHelperData.DictPrefToColor=dictPrefToColor;
			toothChartHelperData.ListDefsPerio=listDefsPerio;
			toothChartHelperData.ListProcsForPerio=listProcsForPerio;
			toothChartHelperData.ListProcedureCodesForPerio=listProcedureCodesForPerio;
			toothChartHelperData.ListProcedureCodesForProcs=listProcedureCodesForProcs;
			toothChartHelperData.IsInPatientDashboard=isInPatientDashboard;
			toothChartHelperData.IsForWinForms=isForWinForms;
			toothChartHelperData.IsForPerio=isForPerio;
			toothChartHelperData.Width=width;
			toothChartHelperData.Height=height;
			toothChartHelperData.IsSmaller=isSmaller;
			toothChartHelperData.IsForChartModule=isForChartModule;
			return toothChartHelperData;
		}

	}
	public class ToothChartHelperData {
 		public List<Def> ListDefs=new List<Def>();
		public List<Procedure> ListProceduresFiltered=new List<Procedure>();
		public List<ToothInitial> ToothInitialList=new List<ToothInitial>();
		public OpenPmsDentalBusiness.ToothNumberingNomenclature ToothNumberingNomenclature;
		public List<OpenPmsDentalBusiness.PerioMeasure> ListPerioMeasures=new List<OpenPmsDentalBusiness.PerioMeasure>();
		public Dictionary<PrefName,Color> DictPrefToColor=new Dictionary<PrefName,Color>();
		public List<Def> ListDefsPerio=new List<Def>();
		public List<Procedure> ListProcsForPerio=new List<Procedure>();
		public List<ProcedureCode> ListProcedureCodesForPerio;
		public List<ProcedureCode> ListProcedureCodesForProcs;
		public bool IsInPatientDashboard=false;
		public bool IsForWinForms=false;
		public bool IsForPerio=false;
		public int Width=500;
		public int Height=370;
		public bool IsSmaller=false;
		public bool IsForChartModule=false;
	}
}
