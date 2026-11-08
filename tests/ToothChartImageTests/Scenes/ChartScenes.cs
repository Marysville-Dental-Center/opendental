// The named, synthetic tooth chart scenes of docs/specs/wpf-tooth-chart-plan.md (section 8). Every image test, recording and gallery draws these.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace ToothChartImageTests {
	///<summary>What a scene draws: the normal chart, or the graphical perio chart (DirectX only today).</summary>
	public enum ChartSceneKind {
		Chart,
		Perio,
	}

	///<summary>The colours a chart image is made with.</summary>
	public enum ChartColorSet {
		///<summary>The Chart module, the big chart window and the Patient Dashboard: Chart Graphic Colors 10 to 13.</summary>
		Module,
		///<summary>Treatment plan printouts and sheets: Chart Graphic Colors 14 and 15.</summary>
		Printout,
		///<summary>The graphical perio chart: white background, black text and the perio colours.</summary>
		Perio,
	}

	///<summary>A pixel size with the colour set that the program uses at that size.</summary>
	public class ChartSize {
		public Size Size;
		public ChartColorSet ColorSet;

		public ChartSize(int width,int height,ChartColorSet colorSet) {
			Size=new Size(width,height);
			ColorSet=colorSet;
		}

		///<summary>The folder name of the size in the baselines, for example "410x307".</summary>
		public string FolderName {
			get {
				return Size.Width+"x"+Size.Height;
			}
		}
	}

	///<summary>One named, synthetic chart state. No patient data and no database: Apply sets the state through ToothChartRelay's public methods
	///only, after ChartScenes.Prepare has reset the chart as the Chart module's refill does. Later packages add scenes but never remove or rename
	///one, because the baselines and galleries are keyed by Name.</summary>
	public class ChartScene {
		///<summary>Unique, lower case words joined by '-'. Also the file name of the scene's images.</summary>
		public string Name;
		///<summary>The gallery group, for example "paint" or "perio".</summary>
		public string Group;
		///<summary>The scene's data in words, for the gallery row.</summary>
		public string Description;
		public ChartSceneKind Kind=ChartSceneKind.Chart;
		public ToothNumberingNomenclature Nomenclature=ToothNumberingNomenclature.Universal;
		public bool IsOrthoMode;
		///<summary>Sizes besides the standard ones of the scene's kind (ChartScenes.GetStandardSizes).</summary>
		public List<ChartSize> ListSizesExtra=new List<ChartSize>();
		///<summary>Sets the scene's state: primary teeth first, then missing teeth, movements and drawings, then procedures, then orthodontic
		///hardware, the order of the Chart module's refill (ControlChart.FillToothChart).</summary>
		public Action<ToothChartRelay> Apply;

		public override string ToString() {
			return Name;
		}
	}

	///<summary>The colours of a procedure status: the dark colour (fillings dark, crowns dark and the other "Dark" paint types, the X, implants, RCT,
	///build-ups, sealants, text) and the light colour (the "Light" paint types and veneers).</summary>
	public class StatusColors {
		public string Status;
		public Color Dark;
		public Color Light;

		public StatusColors(string status,int argbDark,int argbLight) {
			Status=status;
			Dark=Color.FromArgb(argbDark);
			Light=Color.FromArgb(argbLight);
		}
	}

	///<summary>The scenes and the colours and helpers they share.</summary>
	public static class ChartScenes {
		#region Colours
		//Chart Graphic Colors (definition category 22) as a new database has them (db/init/new-database.sql), by item order.
		public static readonly StatusColors TP=new StatusColors("TP",-3801088,-1933441);//items 0 and 5
		public static readonly StatusColors C=new StatusColors("C",-16777023,-8421408);//items 1 and 6
		public static readonly StatusColors EC=new StatusColors("EC",-16777025,-8421409);//items 2 and 7
		public static readonly StatusColors EO=new StatusColors("EO",-16751616,-8408705);//items 3 and 8
		public static readonly StatusColors R=new StatusColors("R",-3276800,-1671297);//items 4 and 9
		public static readonly StatusColors Cn=new StatusColors("Cn",-5169880,-1678747);//items 16 and 17
		///<summary>The six statuses that have colours, in the order the status scenes paint them.</summary>
		public static readonly StatusColors[] ArrayStatuses={ TP,C,EC,EO,R,Cn };
		///<summary>A status the colour code does not handle (D, TPi without a code colour): the Chart module and sheets paint it white.</summary>
		public static readonly StatusColors Unhandled=new StatusColors("unhandled",Color.White.ToArgb(),Color.White.ToArgb());
		public static readonly Color ModuleBackground=Color.FromArgb(-6909543);//10 Main Background
		public static readonly Color ModuleText=Color.FromArgb(-16777216);//11 Text
		public static readonly Color ModuleTextHighlight=Color.FromArgb(-65536);//12 Highlighted Text
		public static readonly Color ModuleBackHighlight=Color.FromArgb(-1);//13 Highlighted Background
		public static readonly Color PrintoutBackground=Color.FromArgb(-1);//14 Background on TPs
		public static readonly Color PrintoutText=Color.FromArgb(-16777216);//15 Text on TPs
		//The wrapper's own highlight colours (ToothChartData's constructor), which printouts and sheets leave as they are.
		public static readonly Color DefaultTextHighlight=Color.Red;
		public static readonly Color DefaultBackHighlight=Color.White;
		//The perio colours of a new database: Misc Colors (category 21) items 1 and 2, and the PerioColor* and PerioRed* preferences.
		public static readonly Color PerioBleeding=Color.FromArgb(-1043436);
		public static readonly Color PerioSuppuration=Color.FromArgb(-24576);
		public static readonly Color PerioProbing=Color.FromArgb(-16744448);
		public static readonly Color PerioProbingRed=Color.FromArgb(-65536);
		public static readonly Color PerioGM=Color.FromArgb(-8388480);
		public static readonly Color PerioCAL=Color.FromArgb(-16777011);
		public static readonly Color PerioMGJ=Color.FromArgb(-29696);
		public static readonly Color PerioFurcations=Color.FromArgb(-16777216);
		public static readonly Color PerioFurcationsRed=Color.FromArgb(-7667712);
		public const int PerioRedProbing=4;
		public const int PerioRedFurcations=1;
		public const int PerioRedMobility=1;
		#endregion Colours

		#region Sizes
		public static readonly ChartSize SizeModule=new ChartSize(410,307,ChartColorSet.Module);
		public static readonly ChartSize SizePrintout=new ChartSize(500,370,ChartColorSet.Printout);
		public static readonly ChartSize SizePerio=new ChartSize(649,696,ChartColorSet.Perio);
		public static readonly ChartSize SizeBigWindow=new ChartSize(926,858,ChartColorSet.Module);
		public static readonly ChartSize SizeSheetWide=new ChartSize(640,320,ChartColorSet.Printout);
		public static readonly ChartSize SizeSheetTall=new ChartSize(360,480,ChartColorSet.Printout);

		///<summary>Every chart scene is drawn at 410x307 with the module colours and at 500x370 with the printout colours; every perio scene at
		///649x696, the graphical perio window's size.</summary>
		public static List<ChartSize> GetStandardSizes(ChartSceneKind kind) {
			if(kind==ChartSceneKind.Perio) {
				return new List<ChartSize> { SizePerio };
			}
			return new List<ChartSize> { SizeModule,SizePrintout };
		}

		///<summary>The standard sizes of the scene's kind, then its extra sizes.</summary>
		public static List<ChartSize> GetSizes(ChartScene scene) {
			List<ChartSize> listSizes=GetStandardSizes(scene.Kind);
			listSizes.AddRange(scene.ListSizesExtra);
			return listSizes;
		}
		#endregion Sizes

		#region Prepare
		///<summary>Resets the chart and applies the scene, in the order of the program's callers. A chart scene: ortho mode, numbering, the colours of
		///colorSet, ResetTeeth, then the scene (ControlChart.FillToothChart; printouts as SheetPrinting.GetToothChartHelper). A perio scene: numbering,
		///white and black, the perio colours, ResetTeeth, perio mode, then the scene (FormPerioGraphical.LayoutTeeth). Everything the scene can change
		///is set again here or cleared by ResetTeeth, so a wrapper can draw scenes one after another; perio measures are not cleared by ResetTeeth, so
		///a perio scene needs a new wrapper.</summary>
		public static void Prepare(ToothChartRelay relay,ChartScene scene,ChartColorSet colorSet) {
			relay.BeginUpdate();
			if(scene.Kind==ChartSceneKind.Perio) {
				relay.SetToothNumberingNomenclature(scene.Nomenclature);
				relay.ColorBackgroundMain=Color.White;
				relay.ColorText=Color.Black;
				relay.SetPerioColors(PerioBleeding,PerioSuppuration,PerioProbing,PerioProbingRed,PerioGM,PerioCAL,PerioMGJ,PerioFurcations,
					PerioFurcationsRed,PerioRedProbing,PerioRedFurcations);
				relay.ResetTeeth();
				relay.SetOrthoMode(false);
				relay.IsPerioMode=true;
				scene.Apply?.Invoke(relay);
				relay.EndUpdate();
				return;
			}
			relay.SetOrthoMode(scene.IsOrthoMode);
			relay.SetToothNumberingNomenclature(scene.Nomenclature);
			if(colorSet==ChartColorSet.Printout) {
				relay.ColorBackgroundMain=PrintoutBackground;
				relay.ColorText=PrintoutText;
				relay.ColorTextHighlightFore=DefaultTextHighlight;
				relay.ColorTextHighlightBack=DefaultBackHighlight;
			}
			else {
				relay.ColorBackgroundMain=ModuleBackground;
				relay.ColorText=ModuleText;
				relay.ColorTextHighlightFore=ModuleTextHighlight;
				relay.ColorTextHighlightBack=ModuleBackHighlight;
			}
			relay.ColorDrawing=Color.Black;
			relay.ResetTeeth();
			scene.Apply?.Invoke(relay);
			relay.EndUpdate();
		}
		#endregion Prepare

		#region Painting helpers
		//Teeth that the status scenes paint, one status each in ArrayStatuses order: three right and three left, so mirrored teeth (9-24) are in.
		private static readonly string[] _arrayTeethMaxillary={ "3","5","7","10","12","14" };
		private static readonly string[] _arrayTeethMandibular={ "30","28","26","23","21","19" };

		///<summary>Paints one procedure as the Chart module's paint switch does (ControlChart.DrawProcGraphics) for the given paint type, tooth,
		///surfaces or range and status colours. listMissing holds the teeth that are missing or hidden, which turns bridge and denture teeth into
		///pontics. The caller has already set those teeth missing.</summary>
		public static void Paint(ToothChartRelay relay,ToothPaintingType paintType,string toothNum,string surf,StatusColors statusColors,
			params string[] arrayMissing)
		{
			Color colorDark=statusColors.Dark;
			Color colorLight=statusColors.Light;
			switch(paintType) {
				case ToothPaintingType.BridgeDark:
				case ToothPaintingType.BridgeLight:
					Color colorBridge=paintType==ToothPaintingType.BridgeDark?colorDark:colorLight;
					if(arrayMissing.Contains(toothNum)) {
						relay.SetPontic(toothNum,colorBridge);
					}
					else {
						relay.SetCrown(toothNum,colorBridge);
					}
					break;
				case ToothPaintingType.CrownDark:
					relay.SetCrown(toothNum,colorDark);
					break;
				case ToothPaintingType.CrownLight:
					relay.SetCrown(toothNum,colorLight);
					break;
				case ToothPaintingType.DentureDark:
				case ToothPaintingType.DentureLight:
					Color colorDenture=paintType==ToothPaintingType.DentureDark?colorDark:colorLight;
					string[] arrayTeeth;
					if(surf=="U") {
						arrayTeeth=Enumerable.Range(2,14).Select(x => x.ToString()).ToArray();
					}
					else if(surf=="L") {
						arrayTeeth=Enumerable.Range(18,14).Select(x => x.ToString()).ToArray();
					}
					else {
						arrayTeeth=toothNum.Split(',');//the tooth range
					}
					foreach(string toothDenture in arrayTeeth) {
						if(arrayMissing.Contains(toothDenture)) {
							relay.SetPontic(toothDenture,colorDenture);
							continue;
						}
						relay.SetCrown(toothDenture,colorDenture);
					}
					break;
				case ToothPaintingType.Extraction:
					relay.SetBigX(toothNum,colorDark);
					break;
				case ToothPaintingType.FillingDark:
					relay.SetSurfaceColors(toothNum,surf,colorDark);
					break;
				case ToothPaintingType.FillingLight:
					relay.SetSurfaceColors(toothNum,surf,colorLight);
					break;
				case ToothPaintingType.Implant:
					relay.SetImplant(toothNum,colorDark);
					break;
				case ToothPaintingType.PostBU:
					relay.SetBU(toothNum,colorDark);
					break;
				case ToothPaintingType.RCT:
					relay.SetRCT(toothNum,colorDark);
					break;
				case ToothPaintingType.RetainedRoot:
					relay.SetRetainedRoot(toothNum,colorDark);
					break;
				case ToothPaintingType.Sealant:
					relay.SetSealant(toothNum,colorDark);
					break;
				case ToothPaintingType.SpaceMaintainer:
					foreach(string toothSpace in toothNum.Split(',')) {
						relay.SetSpaceMaintainer(toothSpace,colorDark);
					}
					break;
				case ToothPaintingType.Text:
					relay.SetText(toothNum,colorDark,surf);//surf holds the procedure code's paint text
					break;
				case ToothPaintingType.Veneer:
					relay.SetVeneer(toothNum,colorLight);
					break;
			}
		}

		///<summary>Paints the type in each of the six status colours on six maxillary and six mandibular teeth (3, 5, 7, 10, 12, 14 and 30, 28, 26, 23,
		///21, 19), right and left, so mirrored teeth are in.</summary>
		private static void PaintStatuses(ToothChartRelay relay,ToothPaintingType paintType,string surf) {
			for(int i=0;i<ArrayStatuses.Length;i++) {
				Paint(relay,paintType,_arrayTeethMaxillary[i],surf,ArrayStatuses[i]);
				Paint(relay,paintType,_arrayTeethMandibular[i],surf,ArrayStatuses[i]);
			}
		}

		private static void SetMissing(ToothChartRelay relay,params string[] arrayTeeth) {
			foreach(string toothNum in arrayTeeth) {
				relay.SetMissing(toothNum);
			}
		}

		private static void SetPrimary(ToothChartRelay relay,params string[] arrayTeeth) {
			foreach(string toothNum in arrayTeeth) {
				relay.SetPrimary(toothNum);
			}
		}

		///<summary>Every permanent tooth that has a primary tooth: 4-13 and 20-29.</summary>
		private static string[] GetTeethWithPrimary() {
			return Enumerable.Range(4,10).Concat(Enumerable.Range(20,10)).Select(x => x.ToString()).ToArray();
		}

		///<summary>Moves each tooth by the same amounts (MoveTooth adds to what the tooth has).</summary>
		private static void Move(ToothChartRelay relay,string[] arrayTeeth,float rotate,float tipM,float tipB,float shiftM,float shiftO,float shiftB) {
			foreach(string toothNum in arrayTeeth) {
				relay.MoveTooth(toothNum,rotate,tipM,tipB,shiftM,shiftO,shiftB);
			}
		}

		private static void SetSelected(ToothChartRelay relay,params string[] arrayTeeth) {
			foreach(string toothNum in arrayTeeth) {
				relay.SetSelected(toothNum,true);
			}
		}

		///<summary>A pen drawing as the Chart module stores it: points "x,y" in the 410x307 drawing space, joined by ';'.</summary>
		private static void AddDrawing(ToothChartRelay relay,Color color,params PointF[] arrayPoints) {
			ToothInitial toothInitial=new ToothInitial();
			toothInitial.InitialType=ToothInitialType.Drawing;
			toothInitial.ColorDraw=color;
			toothInitial.DrawingSegment=string.Join(";",arrayPoints.Select(x => x.X.ToString(System.Globalization.CultureInfo.InvariantCulture)+","
				+x.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)));
			relay.AddDrawingSegment(toothInitial);
		}

		///<summary>Points on a circle in the 410x307 drawing space, rounded to tenths as the pen stores them.</summary>
		private static PointF[] GetCircle(float xCenter,float yCenter,float radius,int countPoints) {
			PointF[] arrayPoints=new PointF[countPoints+1];
			for(int i=0;i<=countPoints;i++) {
				double angle=2*Math.PI*i/countPoints;
				arrayPoints[i]=new PointF((float)Math.Round(xCenter+radius*Math.Cos(angle),1),(float)Math.Round(yCenter+radius*Math.Sin(angle),1));
			}
			return arrayPoints;
		}
		#endregion Painting helpers

		#region Perio helpers
		///<summary>A synthetic perio exam. The values come from simple formulas of the tooth and site numbers, so the exam is the same every time.</summary>
		public class PerioExamSpec {
			///<summary>Teeth marked "skip" (missing).</summary>
			public List<int> ListSkipped=new List<int>();
			///<summary>Missing teeth with an implant, drawn as FormPerioGraphical draws them (Gainsboro implant).</summary>
			public List<int> ListImplants=new List<int>();
			///<summary>Implant teeth that also have a crown (WhiteSmoke).</summary>
			public List<int> ListImplantCrowns=new List<int>();
			///<summary>Probing depth for tooth t and site s (0 MB, 1 B, 2 DB, 3 ML, 4 L, 5 DL); -1 means no measurement.</summary>
			public Func<int,int,int> FuncProbing=(t,s) => 2+(t+s)%3;
			///<summary>Gingival margin; over 100 is hyperplasia (101 means -1), -1 means no measurement.</summary>
			public Func<int,int,int> FuncGM=(t,s) => (t+2*s)%4==0?2:(t+s)%5==0?1:0;
			///<summary>Mucogingival junction from the gingival margin; -1 means no measurement.</summary>
			public Func<int,int,int> FuncMGJ=(t,s) => s<3?3+(t%3):(t<17?-1:3+(t%2));
			///<summary>BleedingFlags per site.</summary>
			public Func<int,int,int> FuncBleeding=(t,s) => (t*7+s)%9==0?(int)BleedingFlags.Blood:(t*5+s)%13==0?(int)BleedingFlags.Suppuration
				:(t*3+s)%17==0?(int)(BleedingFlags.Blood|BleedingFlags.Suppuration):0;
			///<summary>Furcation grade per site, for molars; -1 or 0 means none.</summary>
			public Func<int,int,int> FuncFurcation=(t,s) => -1;
			///<summary>Mobility per tooth; -1 means no measurement.</summary>
			public Func<int,int> FuncMobility=t => -1;
		}

		///<summary>Adds the exam through the relay in FormPerioGraphical's order: the CAL of every site that has a probing depth and a gingival margin
		///(gingival margin plus probing depth, hyperplasia counted negative, never below 0), then the measures, the skipped teeth as missing, the
		///mobility (red from PerioRedMobility on), then the implants.</summary>
		public static void AddPerioExam(ToothChartRelay relay,PerioExamSpec spec) {
			List<int> listTeeth=Enumerable.Range(1,32).Where(x => !spec.ListSkipped.Contains(x)).ToList();
			foreach(int t in listTeeth) {
				int[] arrayCal=new int[6];
				bool hasCal=false;
				for(int s=0;s<6;s++) {
					int gm=spec.FuncGM(t,s);
					int pd=spec.FuncProbing(t,s);
					arrayCal[s]=-1;
					if(gm!=-1 && pd!=-1) {
						int gmAdjusted=gm>100?100-gm:gm;
						arrayCal[s]=Math.Max(0,gmAdjusted+pd);
						hasCal=true;
					}
				}
				if(hasCal) {
					relay.AddPerioMeasure(t,PerioSequenceType.CAL,arrayCal[0],arrayCal[1],arrayCal[2],arrayCal[3],arrayCal[4],arrayCal[5]);
				}
			}
			foreach(int t in listTeeth) {
				AddSites(relay,t,PerioSequenceType.Probing,spec.FuncProbing);
				AddSites(relay,t,PerioSequenceType.GingMargin,spec.FuncGM);
				AddSites(relay,t,PerioSequenceType.MGJ,spec.FuncMGJ);
				AddSites(relay,t,PerioSequenceType.BleedSupPlaqCalc,spec.FuncBleeding);
				if(IsMolar(t)) {
					AddSites(relay,t,PerioSequenceType.Furcation,spec.FuncFurcation);
				}
			}
			foreach(int t in spec.ListSkipped) {
				relay.SetMissing(t.ToString());
			}
			foreach(int t in listTeeth) {
				int mobility=spec.FuncMobility(t);
				if(mobility!=-1) {
					relay.SetMobility(t.ToString(),mobility.ToString(),mobility>=PerioRedMobility?Color.Red:Color.Black);
				}
			}
			foreach(int t in spec.ListImplants) {
				relay.SetMissing(t.ToString());
				relay.SetImplant(t.ToString(),Color.Gainsboro);
				if(spec.ListImplantCrowns.Contains(t)) {
					relay.SetCrown(t.ToString(),Color.WhiteSmoke);
				}
			}
		}

		private static void AddSites(ToothChartRelay relay,int t,PerioSequenceType perioSequenceType,Func<int,int,int> funcSite) {
			relay.AddPerioMeasure(t,perioSequenceType,funcSite(t,0),funcSite(t,1),funcSite(t,2),funcSite(t,3),funcSite(t,4),funcSite(t,5));
		}

		private static bool IsMolar(int t) {
			return t<=3 || (t>=14 && t<=19) || t>=30;
		}
		#endregion Perio helpers

		#region Scenes
		private static List<ChartScene> _listScenes;

		///<summary>Every scene, in a fixed order: the order of the recordings, the manifests and the galleries.</summary>
		public static List<ChartScene> GetAll() {
			if(_listScenes==null) {
				List<ChartScene> listScenes=new List<ChartScene>();
				AddEmptyScenes(listScenes);
				AddStateScenes(listScenes);
				AddPaintScenes(listScenes);
				AddKnownDifferenceScenes(listScenes);
				AddColorScenes(listScenes);
				AddInteractionScenes(listScenes);
				AddFreeTextScenes(listScenes);
				AddOrthoScenes(listScenes);
				AddSizeScenes(listScenes);
				AddPerioScenes(listScenes);
				_listScenes=listScenes;
			}
			return _listScenes;
		}

		///<summary>The scene with the name. Throws when there is none.</summary>
		public static ChartScene Get(string name) {
			ChartScene scene=GetAll().FirstOrDefault(x => x.Name==name);
			if(scene==null) {
				throw new ArgumentException("No chart scene is named "+name);
			}
			return scene;
		}

		private static ChartScene Add(List<ChartScene> listScenes,string group,string name,string description,Action<ToothChartRelay> apply) {
			ChartScene scene=new ChartScene();
			scene.Group=group;
			scene.Name=name;
			scene.Description=description;
			scene.Apply=apply;
			listScenes.Add(scene);
			return scene;
		}

		private static void AddEmptyScenes(List<ChartScene> listScenes) {
			const string group="empty";
			Add(listScenes,group,"empty-universal","An adult chart with all 32 teeth and no work, in Universal numbering.",null);
			Add(listScenes,group,"empty-palmer","An adult chart with all 32 teeth and no work, in Palmer numbering.",null)
				.Nomenclature=ToothNumberingNomenclature.Palmer;
			Add(listScenes,group,"empty-fdi","An adult chart with all 32 teeth and no work, in FDI numbering.",null)
				.Nomenclature=ToothNumberingNomenclature.FDI;
			Add(listScenes,group,"empty-haderup","An adult chart with all 32 teeth and no work, in Haderup numbering.",null)
				.Nomenclature=ToothNumberingNomenclature.Haderup;
			Add(listScenes,group,"primary-dentition","All 20 primary teeth (A-T), with the permanent teeth 4-13 and 20-29 unerupted under them, in "
				+"Universal numbering.",relay => SetPrimary(relay,GetTeethWithPrimary()));
			Add(listScenes,group,"primary-dentition-fdi","All 20 primary teeth, in FDI numbering.",relay => SetPrimary(relay,GetTeethWithPrimary()))
				.Nomenclature=ToothNumberingNomenclature.FDI;
			Add(listScenes,group,"primary-dentition-palmer","All 20 primary teeth, in Palmer numbering.",relay => SetPrimary(relay,GetTeethWithPrimary()))
				.Nomenclature=ToothNumberingNomenclature.Palmer;
			Add(listScenes,group,"mixed-dentition","Mixed dentition: primary canines and molars A, B, C, H, I, J, K, L, M, R, S and T with the "
				+"permanent teeth 4-6, 11-13, 20-22 and 27-29 unerupted under them; permanent incisors and first molars erupted; second and third molars "
				+"missing.",relay => {
					SetPrimary(relay,"4","5","6","11","12","13","20","21","22","27","28","29");
					SetMissing(relay,"1","2","15","16","17","18","31","32");
				});
			Add(listScenes,group,"mixed-dentition-haderup","The mixed dentition scene in Haderup numbering.",relay => {
					SetPrimary(relay,"4","5","6","11","12","13","20","21","22","27","28","29");
					SetMissing(relay,"1","2","15","16","17","18","31","32");
				}).Nomenclature=ToothNumberingNomenclature.Haderup;
			Add(listScenes,group,"unerupted-third-molars","Third molars 1, 16, 17 and 32 unerupted (shifted 12 mm toward the root), drawn at half colour.",
				relay => Move(relay,new[] { "1","16","17","32" },0,0,0,0,-12,0));
		}

		private static void AddStateScenes(List<ChartScene> listScenes) {
			const string group="states";
			string[] arrayMoved={ "4","7","10","13","20","23","26","29" };
			const string movedText=" on teeth 4, 7, 10, 13, 20, 23, 26 and 29 (all four quadrants, mirrored teeth included).";
			Add(listScenes,group,"missing-posterior","First molars 3, 14, 19 and 30 missing.",relay => SetMissing(relay,"3","14","19","30"));
			Add(listScenes,group,"missing-anterior","Central incisors 8, 9, 24 and 25 missing.",relay => SetMissing(relay,"8","9","24","25"));
			Add(listScenes,group,"missing-many","Third and second molars and the second premolars 4, 13, 20 and 29 missing.",
				relay => SetMissing(relay,"1","2","4","13","15","16","17","18","20","29","31","32"));
			Add(listScenes,group,"hidden-premolars","First premolars 5, 12, 21 and 28 hidden (missing with the number gone, as after ortho closes the space).",
				relay => {
					foreach(string toothNum in new[] { "5","12","21","28" }) {
						relay.SetHidden(toothNum);
					}
				});
			Add(listScenes,group,"missing-and-hidden-primary","Primary dentition with D and G missing and the letters of K and T hidden.",relay => {
				SetPrimary(relay,GetTeethWithPrimary());
				SetMissing(relay,"D","G");
				relay.SetHidden("K");
				relay.SetHidden("T");
			});
			Add(listScenes,group,"rotate-plus","Rotated +30 degrees"+movedText,relay => Move(relay,arrayMoved,30,0,0,0,0,0));
			Add(listScenes,group,"rotate-minus","Rotated -30 degrees"+movedText,relay => Move(relay,arrayMoved,-30,0,0,0,0,0));
			Add(listScenes,group,"tipm-plus","Tipped mesially +15 degrees"+movedText,relay => Move(relay,arrayMoved,0,15,0,0,0,0));
			Add(listScenes,group,"tipm-minus","Tipped distally (TipM -15 degrees)"+movedText,relay => Move(relay,arrayMoved,0,-15,0,0,0,0));
			Add(listScenes,group,"tipb-plus","Tipped buccally +15 degrees"+movedText,relay => Move(relay,arrayMoved,0,0,15,0,0,0));
			Add(listScenes,group,"tipb-minus","Tipped lingually (TipB -15 degrees)"+movedText,relay => Move(relay,arrayMoved,0,0,-15,0,0,0));
			Add(listScenes,group,"shiftm-plus","Shifted 2 mm mesially"+movedText,relay => Move(relay,arrayMoved,0,0,0,2,0,0));
			Add(listScenes,group,"shiftm-minus","Shifted 2 mm distally"+movedText,relay => Move(relay,arrayMoved,0,0,0,-2,0,0));
			Add(listScenes,group,"shifto-plus","Supererupted 3 mm"+movedText,relay => Move(relay,arrayMoved,0,0,0,0,3,0));
			Add(listScenes,group,"shifto-minus","Intruded 5 mm (still erupted: drawn in full colour)"+movedText,relay => Move(relay,arrayMoved,0,0,0,0,-5,0));
			Add(listScenes,group,"shiftb-plus","Shifted 2 mm buccally"+movedText,relay => Move(relay,arrayMoved,0,0,0,0,0,2));
			Add(listScenes,group,"shiftb-minus","Shifted 2 mm lingually"+movedText,relay => Move(relay,arrayMoved,0,0,0,0,0,-2));
			Add(listScenes,group,"shifto-unerupted","Canines 6, 11, 22 and 27 shifted 11 mm toward the root: just past the unerupted limit of -10 mm, so drawn "
				+"at half colour.",relay => Move(relay,new[] { "6","11","22","27" },0,0,0,0,-11,0));
			Add(listScenes,group,"shiftm-fractions","Teeth 3 and 14 shifted 1.6 mm mesially and 19 and 30 shifted 2.5 mm mesially: the number boxes move "
				+"with the teeth.",relay => {
					Move(relay,new[] { "3","14" },0,0,0,1.6f,0,0);
					Move(relay,new[] { "19","30" },0,0,0,2.5f,0,0);
				});
			Add(listScenes,group,"movements-combined","Several movements on one tooth: 8 rotated 20, tipped 10 and shifted 1 mm mesially; 25 tipped -10 "
				+"buccally and supererupted 2 mm; 14 rotated -45 and shifted 1 mm buccally; 19 tipped 20 mesially and intruded 2 mm.",relay => {
					relay.MoveTooth("8",20,10,0,1,0,0);
					relay.MoveTooth("25",0,0,-10,0,2,0);
					relay.MoveTooth("14",-45,0,0,0,0,1);
					relay.MoveTooth("19",0,20,0,0,-2,0);
				});
		}

		private static void AddPaintScenes(List<ChartScene> listScenes) {
			const string group="paint";
			const string statusText=" in the six status colours (TP, C, EC, EO, R, Cn) on teeth 3, 5, 7, 10, 12, 14 and 30, 28, 26, 23, 21, 19.";
			#region Fillings
			Add(listScenes,group,"filling-dark-posterior-single","Dark TP fillings, one surface per tooth: 2 M, 3 O, 4 D, 5 B, 12 L, 13 V, 14 M, 15 D, "
				+"18 O, 19 B, 20 L, 21 V, 28 M, 29 D, 30 O, 31 L.",relay => {
					string[][] arrayFillings={ new[] { "2","M" },new[] { "3","O" },new[] { "4","D" },new[] { "5","B" },new[] { "12","L" },new[] { "13","V" },
						new[] { "14","M" },new[] { "15","D" },new[] { "18","O" },new[] { "19","B" },new[] { "20","L" },new[] { "21","V" },new[] { "28","M" },
						new[] { "29","D" },new[] { "30","O" },new[] { "31","L" } };
					foreach(string[] arrayFilling in arrayFillings) {
						Paint(relay,ToothPaintingType.FillingDark,arrayFilling[0],arrayFilling[1],TP);
					}
				});
			Add(listScenes,group,"filling-dark-anterior-single","Dark TP fillings, one surface per tooth: 6 M, 7 D, 8 F, 9 I, 10 L, 11 V, 22 M, 23 D, 24 F, "
				+"25 I, 26 L, 27 V.",relay => {
					string[][] arrayFillings={ new[] { "6","M" },new[] { "7","D" },new[] { "8","F" },new[] { "9","I" },new[] { "10","L" },new[] { "11","V" },
						new[] { "22","M" },new[] { "23","D" },new[] { "24","F" },new[] { "25","I" },new[] { "26","L" },new[] { "27","V" } };
					foreach(string[] arrayFilling in arrayFillings) {
						Paint(relay,ToothPaintingType.FillingDark,arrayFilling[0],arrayFilling[1],TP);
					}
				});
			Add(listScenes,group,"filling-dark-posterior-combinations","Dark C fillings on several surfaces: 2 MO, 3 DO, 4 MOD, 5 MODB, 12 MODL, 13 BV, "
				+"14 MODBL, 15 OL, 18 MOD, 19 MODBLV, 20 DO, 21 MO, 28 OB, 29 MODL, 30 MOB, 31 OV.",relay => {
					string[][] arrayFillings={ new[] { "2","MO" },new[] { "3","DO" },new[] { "4","MOD" },new[] { "5","MODB" },new[] { "12","MODL" },
						new[] { "13","BV" },new[] { "14","MODBL" },new[] { "15","OL" },new[] { "18","MOD" },new[] { "19","MODBLV" },new[] { "20","DO" },
						new[] { "21","MO" },new[] { "28","OB" },new[] { "29","MODL" },new[] { "30","MOB" },new[] { "31","OV" } };
					foreach(string[] arrayFilling in arrayFillings) {
						Paint(relay,ToothPaintingType.FillingDark,arrayFilling[0],arrayFilling[1],C);
					}
				});
			Add(listScenes,group,"filling-dark-anterior-combinations","Dark C fillings on several surfaces: 6 MI, 7 DI, 8 MIF, 9 DFL, 10 FV, 11 MDFIL, "
				+"22 ML, 23 MDI, 24 FIL, 25 MF, 26 DLV, 27 MDF.",relay => {
					string[][] arrayFillings={ new[] { "6","MI" },new[] { "7","DI" },new[] { "8","MIF" },new[] { "9","DFL" },new[] { "10","FV" },
						new[] { "11","MDFIL" },new[] { "22","ML" },new[] { "23","MDI" },new[] { "24","FIL" },new[] { "25","MF" },new[] { "26","DLV" },
						new[] { "27","MDF" } };
					foreach(string[] arrayFilling in arrayFillings) {
						Paint(relay,ToothPaintingType.FillingDark,arrayFilling[0],arrayFilling[1],C);
					}
				});
			Add(listScenes,group,"filling-light-posterior","Light EC fillings: 2 MO, 3 MOD, 4 DO, 5 O, 12 MODB, 13 OL, 14 MODBL, 15 B, 18 MO, 19 DOB, 20 V, "
				+"21 MOD, 28 O, 29 MODBL, 30 OL, 31 MO.",relay => {
					string[][] arrayFillings={ new[] { "2","MO" },new[] { "3","MOD" },new[] { "4","DO" },new[] { "5","O" },new[] { "12","MODB" },
						new[] { "13","OL" },new[] { "14","MODBL" },new[] { "15","B" },new[] { "18","MO" },new[] { "19","DOB" },new[] { "20","V" },
						new[] { "21","MOD" },new[] { "28","O" },new[] { "29","MODBL" },new[] { "30","OL" },new[] { "31","MO" } };
					foreach(string[] arrayFilling in arrayFillings) {
						Paint(relay,ToothPaintingType.FillingLight,arrayFilling[0],arrayFilling[1],EC);
					}
				});
			Add(listScenes,group,"filling-light-anterior","Light EO fillings: 6 M, 7 MI, 8 DIF, 9 F, 10 L, 11 MDIFL, 22 V, 23 MF, 24 DL, 25 MDI, 26 I, 27 F.",
				relay => {
					string[][] arrayFillings={ new[] { "6","M" },new[] { "7","MI" },new[] { "8","DIF" },new[] { "9","F" },new[] { "10","L" },
						new[] { "11","MDIFL" },new[] { "22","V" },new[] { "23","MF" },new[] { "24","DL" },new[] { "25","MDI" },new[] { "26","I" },
						new[] { "27","F" } };
					foreach(string[] arrayFilling in arrayFillings) {
						Paint(relay,ToothPaintingType.FillingLight,arrayFilling[0],arrayFilling[1],EO);
					}
				});
			Add(listScenes,group,"filling-dark-statuses","Dark MOD fillings"+statusText,relay => PaintStatuses(relay,ToothPaintingType.FillingDark,"MOD"));
			Add(listScenes,group,"filling-light-statuses","Light MOD fillings"+statusText,relay => PaintStatuses(relay,ToothPaintingType.FillingLight,"MOD"));
			Add(listScenes,group,"filling-mirror-pairs","The same surfaces on mirrored teeth: M on 3 and 14, D on 19 and 30, F on 8 and 9, I on 24 and 25, "
				+"MO on 4 and 13, DO on 20 and 29 (TP dark).",relay => {
					foreach(string toothNum in new[] { "3","14" }) { Paint(relay,ToothPaintingType.FillingDark,toothNum,"M",TP); }
					foreach(string toothNum in new[] { "19","30" }) { Paint(relay,ToothPaintingType.FillingDark,toothNum,"D",TP); }
					foreach(string toothNum in new[] { "8","9" }) { Paint(relay,ToothPaintingType.FillingDark,toothNum,"F",TP); }
					foreach(string toothNum in new[] { "24","25" }) { Paint(relay,ToothPaintingType.FillingDark,toothNum,"I",TP); }
					foreach(string toothNum in new[] { "4","13" }) { Paint(relay,ToothPaintingType.FillingDark,toothNum,"MO",TP); }
					foreach(string toothNum in new[] { "20","29" }) { Paint(relay,ToothPaintingType.FillingDark,toothNum,"DO",TP); }
				});
			Add(listScenes,group,"filling-primary","Primary dentition with dark TP fillings on the primary molars and canines: A MO, B DO, C F, H M, I MOD, "
				+"J O, K MO, L DO, M I, R F, S MOD, T O.",relay => {
					SetPrimary(relay,GetTeethWithPrimary());
					string[][] arrayFillings={ new[] { "A","MO" },new[] { "B","DO" },new[] { "C","F" },new[] { "H","M" },new[] { "I","MOD" },
						new[] { "J","O" },new[] { "K","MO" },new[] { "L","DO" },new[] { "M","I" },new[] { "R","F" },new[] { "S","MOD" },new[] { "T","O" } };
					foreach(string[] arrayFilling in arrayFillings) {
						Paint(relay,ToothPaintingType.FillingDark,arrayFilling[0],arrayFilling[1],TP);
					}
				});
			#endregion Fillings
			#region Crowns, bridges, dentures
			Add(listScenes,group,"crown-dark-statuses","Dark crowns"+statusText,relay => PaintStatuses(relay,ToothPaintingType.CrownDark,""));
			Add(listScenes,group,"crown-light-statuses","Light crowns"+statusText,relay => PaintStatuses(relay,ToothPaintingType.CrownLight,""));
			Add(listScenes,group,"crown-on-missing-tooth","Dark TP crowns on missing teeth 3 and 30: the root is hidden and the crown shows only with an "
				+"implant (none here).",relay => {
					SetMissing(relay,"3","30");
					Paint(relay,ToothPaintingType.CrownDark,"3","",TP);
					Paint(relay,ToothPaintingType.CrownDark,"30","",TP);
				});
			Add(listScenes,group,"crown-primary","Primary dentition with dark C crowns on A, J, K and T.",relay => {
				SetPrimary(relay,GetTeethWithPrimary());
				foreach(string toothNum in new[] { "A","J","K","T" }) {
					Paint(relay,ToothPaintingType.CrownDark,toothNum,"",C);
				}
			});
			Add(listScenes,group,"bridge-dark-tp","Dark TP bridges 3-4-5 and 19-20-21: 4 and 20 missing (pontics), the others crowned.",relay => {
				SetMissing(relay,"4","20");
				foreach(string toothNum in new[] { "3","4","5","19","20","21" }) {
					Paint(relay,ToothPaintingType.BridgeDark,toothNum,"",TP,"4","20");
				}
			});
			Add(listScenes,group,"bridge-dark-statuses","Dark bridges with the middle tooth missing: 2-3-4 C, 12-13-14 EC, 18-19-20 EO, 28-29-30 R.",relay => {
				SetMissing(relay,"3","13","19","29");
				string[] arrayMissing={ "3","13","19","29" };
				foreach(string toothNum in new[] { "2","3","4" }) { Paint(relay,ToothPaintingType.BridgeDark,toothNum,"",C,arrayMissing); }
				foreach(string toothNum in new[] { "12","13","14" }) { Paint(relay,ToothPaintingType.BridgeDark,toothNum,"",EC,arrayMissing); }
				foreach(string toothNum in new[] { "18","19","20" }) { Paint(relay,ToothPaintingType.BridgeDark,toothNum,"",EO,arrayMissing); }
				foreach(string toothNum in new[] { "28","29","30" }) { Paint(relay,ToothPaintingType.BridgeDark,toothNum,"",R,arrayMissing); }
			});
			Add(listScenes,group,"bridge-light","Light bridges: 3-4-5 TP and 12-13-14 C with 4 and 13 missing, 19-20-21 EC and 28-29-30 Cn with 20 and 29 "
				+"missing.",relay => {
					string[] arrayMissing={ "4","13","20","29" };
					SetMissing(relay,arrayMissing);
					foreach(string toothNum in new[] { "3","4","5" }) { Paint(relay,ToothPaintingType.BridgeLight,toothNum,"",TP,arrayMissing); }
					foreach(string toothNum in new[] { "12","13","14" }) { Paint(relay,ToothPaintingType.BridgeLight,toothNum,"",C,arrayMissing); }
					foreach(string toothNum in new[] { "19","20","21" }) { Paint(relay,ToothPaintingType.BridgeLight,toothNum,"",EC,arrayMissing); }
					foreach(string toothNum in new[] { "28","29","30" }) { Paint(relay,ToothPaintingType.BridgeLight,toothNum,"",Cn,arrayMissing); }
				});
			Add(listScenes,group,"bridge-anterior","Dark TP anterior bridge 7-8-9-10 with 8 and 9 missing, and 23-24-25-26 EC with 24 and 25 missing.",
				relay => {
					string[] arrayMissing={ "8","9","24","25" };
					SetMissing(relay,arrayMissing);
					foreach(string toothNum in new[] { "7","8","9","10" }) { Paint(relay,ToothPaintingType.BridgeDark,toothNum,"",TP,arrayMissing); }
					foreach(string toothNum in new[] { "23","24","25","26" }) { Paint(relay,ToothPaintingType.BridgeDark,toothNum,"",EC,arrayMissing); }
				});
			Add(listScenes,group,"bridge-on-present-teeth","Dark TP bridge 3-4-5 with no tooth missing: every tooth gets a crown, as the Chart module "
				+"paints it.",relay => {
					foreach(string toothNum in new[] { "3","4","5" }) {
						Paint(relay,ToothPaintingType.BridgeDark,toothNum,"",TP);
					}
				});
			Add(listScenes,group,"denture-upper-dark","Upper complete denture (surface U), dark TP: teeth 1-16 missing, 2-15 drawn as pontics.",relay => {
				string[] arrayMissing=Enumerable.Range(1,16).Select(x => x.ToString()).ToArray();
				SetMissing(relay,arrayMissing);
				Paint(relay,ToothPaintingType.DentureDark,"","U",TP,arrayMissing);
			});
			Add(listScenes,group,"denture-lower-dark","Lower complete denture (surface L), dark C: teeth 17-32 missing, 18-31 drawn as pontics.",relay => {
				string[] arrayMissing=Enumerable.Range(17,16).Select(x => x.ToString()).ToArray();
				SetMissing(relay,arrayMissing);
				Paint(relay,ToothPaintingType.DentureDark,"","L",C,arrayMissing);
			});
			Add(listScenes,group,"denture-both-light","Upper and lower complete dentures, light EO, every tooth missing.",relay => {
				string[] arrayMissing=Enumerable.Range(1,32).Select(x => x.ToString()).ToArray();
				SetMissing(relay,arrayMissing);
				Paint(relay,ToothPaintingType.DentureLight,"","U",EO,arrayMissing);
				Paint(relay,ToothPaintingType.DentureLight,"","L",EO,arrayMissing);
			});
			Add(listScenes,group,"denture-upper-on-present-teeth","Upper denture, dark EC, with no tooth missing: every tooth 2-15 gets a crown, as the "
				+"Chart module paints it.",relay => Paint(relay,ToothPaintingType.DentureDark,"","U",EC));
			Add(listScenes,group,"denture-partial-dark","Upper partial denture over the range 3, 4, 13, 14 (missing), dark TP.",relay => {
				string[] arrayMissing={ "3","4","13","14" };
				SetMissing(relay,arrayMissing);
				Paint(relay,ToothPaintingType.DentureDark,"3,4,13,14","",TP,arrayMissing);
			});
			Add(listScenes,group,"denture-partial-light","Lower partial denture over the range 19, 20, 29, 30 (missing), light C.",relay => {
				string[] arrayMissing={ "19","20","29","30" };
				SetMissing(relay,arrayMissing);
				Paint(relay,ToothPaintingType.DentureLight,"19,20,29,30","",C,arrayMissing);
			});
			#endregion Crowns, bridges, dentures
			#region Extractions, implants, RCT, build-ups
			Add(listScenes,group,"extraction-statuses","The big X: TP on 1, 16, 17 and 32, R on 4, Cn on 29. Completed extractions draw no X (the tooth is "
				+"missing instead).",relay => {
					foreach(string toothNum in new[] { "1","16","17","32" }) {
						Paint(relay,ToothPaintingType.Extraction,toothNum,"",TP);
					}
					Paint(relay,ToothPaintingType.Extraction,"4","",R);
					Paint(relay,ToothPaintingType.Extraction,"29","",Cn);
				});
			Add(listScenes,group,"extraction-anterior","TP extractions of 8 and 24.",relay => {
				Paint(relay,ToothPaintingType.Extraction,"8","",TP);
				Paint(relay,ToothPaintingType.Extraction,"24","",TP);
			});
			Add(listScenes,group,"extraction-primary","Primary dentition with TP extractions of E, F, O and P.",relay => {
				SetPrimary(relay,GetTeethWithPrimary());
				foreach(string toothNum in new[] { "E","F","O","P" }) {
					Paint(relay,ToothPaintingType.Extraction,toothNum,"",TP);
				}
			});
			Add(listScenes,group,"implant-statuses","Implants on missing teeth"+statusText,relay => {
				SetMissing(relay,_arrayTeethMaxillary);
				SetMissing(relay,_arrayTeethMandibular);
				PaintStatuses(relay,ToothPaintingType.Implant,"");
			});
			Add(listScenes,group,"implant-with-crown","Completed implants with dark C crowns on missing teeth 3, 14, 19 and 30.",relay => {
				SetMissing(relay,"3","14","19","30");
				foreach(string toothNum in new[] { "3","14","19","30" }) {
					Paint(relay,ToothPaintingType.Implant,toothNum,"",C);
					Paint(relay,ToothPaintingType.CrownDark,toothNum,"",C);
				}
			});
			Add(listScenes,group,"implant-anterior","Anterior implants: 8 and 9 missing with EC implants and crowns, 24 missing with a TP implant and no "
				+"crown.",relay => {
					SetMissing(relay,"8","9","24");
					foreach(string toothNum in new[] { "8","9" }) {
						Paint(relay,ToothPaintingType.Implant,toothNum,"",EC);
						Paint(relay,ToothPaintingType.CrownDark,toothNum,"",EC);
					}
					Paint(relay,ToothPaintingType.Implant,"24","",TP);
				});
			Add(listScenes,group,"implant-on-present-tooth","TP implants on teeth 4 and 20 that are not missing: the implant is drawn with the tooth.",
				relay => {
					Paint(relay,ToothPaintingType.Implant,"4","",TP);
					Paint(relay,ToothPaintingType.Implant,"20","",TP);
				});
			Add(listScenes,group,"rct-statuses","Root canals"+statusText,relay => PaintStatuses(relay,ToothPaintingType.RCT,""));
			Add(listScenes,group,"rct-with-post-and-crown","Completed root canal, post and build-up and crown (C) on 3, 14, 19 and 30, the usual endodontic "
				+"sequence.",relay => {
					foreach(string toothNum in new[] { "3","14","19","30" }) {
						Paint(relay,ToothPaintingType.RCT,toothNum,"",C);
						Paint(relay,ToothPaintingType.PostBU,toothNum,"",C);
						Paint(relay,ToothPaintingType.CrownDark,toothNum,"",C);
					}
				});
			Add(listScenes,group,"rct-primary","Primary dentition with TP root canals (pulpectomies) on A, J, K and T.",relay => {
				SetPrimary(relay,GetTeethWithPrimary());
				foreach(string toothNum in new[] { "A","J","K","T" }) {
					Paint(relay,ToothPaintingType.RCT,toothNum,"",TP);
				}
			});
			Add(listScenes,group,"rct-on-missing-tooth","A TP root canal on missing tooth 3 (not drawn) and on present tooth 14.",relay => {
				SetMissing(relay,"3");
				Paint(relay,ToothPaintingType.RCT,"3","",TP);
				Paint(relay,ToothPaintingType.RCT,"14","",TP);
			});
			Add(listScenes,group,"postbu-statuses","Posts and build-ups"+statusText,relay => PaintStatuses(relay,ToothPaintingType.PostBU,""));
			Add(listScenes,group,"postbu-with-crown","Dark TP build-ups with dark TP crowns on 4, 5, 12, 13, 20, 21, 28 and 29.",relay => {
				foreach(string toothNum in new[] { "4","5","12","13","20","21","28","29" }) {
					Paint(relay,ToothPaintingType.PostBU,toothNum,"",TP);
					Paint(relay,ToothPaintingType.CrownDark,toothNum,"",TP);
				}
			});
			#endregion Extractions, implants, RCT, build-ups
			#region Sealants, veneers, text, types drawn by nothing yet
			Add(listScenes,group,"sealant-statuses","Sealants (the occlusal \"S\") on 2, 3, 14, 15 (TP, C, EC, EO) and 18, 19, 30, 31 (R, Cn, TP, C).",
				relay => {
					string[] arrayTeeth={ "2","3","14","15","18","19","30","31" };
					StatusColors[] arrayStatusesSealant={ TP,C,EC,EO,R,Cn,TP,C };
					for(int i=0;i<arrayTeeth.Length;i++) {
						Paint(relay,ToothPaintingType.Sealant,arrayTeeth[i],"",arrayStatusesSealant[i]);
					}
				});
			Add(listScenes,group,"sealant-premolars","TP sealants on the premolars 4, 5, 12, 13, 20, 21, 28 and 29.",relay => {
				foreach(string toothNum in new[] { "4","5","12","13","20","21","28","29" }) {
					Paint(relay,ToothPaintingType.Sealant,toothNum,"",TP);
				}
			});
			Add(listScenes,group,"sealant-primary","Primary dentition with C sealants on the primary molars A, B, I, J, K, L, S and T.",relay => {
				SetPrimary(relay,GetTeethWithPrimary());
				foreach(string toothNum in new[] { "A","B","I","J","K","L","S","T" }) {
					Paint(relay,ToothPaintingType.Sealant,toothNum,"",C);
				}
			});
			Add(listScenes,group,"veneer-statuses","Veneers (light colours) on 6-11 in TP, C, EC, EO, R, Cn.",relay => {
				for(int i=0;i<6;i++) {
					Paint(relay,ToothPaintingType.Veneer,(6+i).ToString(),"",ArrayStatuses[i]);
				}
			});
			Add(listScenes,group,"veneer-lower","Veneers on the lower anteriors 22-27, TP.",relay => {
				for(int t=22;t<=27;t++) {
					Paint(relay,ToothPaintingType.Veneer,t.ToString(),"",TP);
				}
			});
			Add(listScenes,group,"veneer-posterior","Veneers on premolars 4, 5, 12 and 13 (TP), which colour only the facial surface.",relay => {
				foreach(string toothNum in new[] { "4","5","12","13" }) {
					Paint(relay,ToothPaintingType.Veneer,toothNum,"",TP);
				}
			});
			Add(listScenes,group,"text-watch","Paint type Text with \"W\": the watch mark on 3 (TP), 8 (C), 14 (EC), 19 (EO), 24 (R) and 30 (Cn).",relay => {
				string[] arrayTeeth={ "3","8","14","19","24","30" };
				for(int i=0;i<arrayTeeth.Length;i++) {
					Paint(relay,ToothPaintingType.Text,arrayTeeth[i],"W",ArrayStatuses[i]);
				}
			});
			Add(listScenes,group,"text-watch-on-missing-tooth","Watch marks on missing teeth 3 and 30 (the mark still shows) and on present tooth 19.",
				relay => {
					SetMissing(relay,"3","30");
					foreach(string toothNum in new[] { "3","19","30" }) {
						Paint(relay,ToothPaintingType.Text,toothNum,"W",TP);
					}
				});
			Add(listScenes,group,"text-watch-primary","Primary dentition with watch marks on A, J, K and T.",relay => {
				SetPrimary(relay,GetTeethWithPrimary());
				foreach(string toothNum in new[] { "A","J","K","T" }) {
					Paint(relay,ToothPaintingType.Text,toothNum,"W",TP);
				}
			});
			Add(listScenes,group,"text-other","Paint type Text with text other than \"W\" (\"Fx\" on 3, \"?\" on 14, \"ABC\" on 19, \"1\" on 30). Today's chart "
				+"draws nothing for it (O-22).",relay => {
					Paint(relay,ToothPaintingType.Text,"3","Fx",TP);
					Paint(relay,ToothPaintingType.Text,"14","?",C);
					Paint(relay,ToothPaintingType.Text,"19","ABC",EC);
					Paint(relay,ToothPaintingType.Text,"30","1",Cn);
				});
			Add(listScenes,group,"retained-root","Retained roots (type 16) on 4, 13 and 20 (TP) and 29 (C). Today's chart draws nothing for them (O-23).",
				relay => {
					foreach(string toothNum in new[] { "4","13","20" }) {
						Paint(relay,ToothPaintingType.RetainedRoot,toothNum,"",TP);
					}
					Paint(relay,ToothPaintingType.RetainedRoot,"29","",C);
				});
			Add(listScenes,group,"space-maintainer-tooth","Space maintainers (type 17) on single teeth 4 (TP) and 29 (C). Today's chart draws nothing for "
				+"them (O-23).",relay => {
					Paint(relay,ToothPaintingType.SpaceMaintainer,"4","",TP);
					Paint(relay,ToothPaintingType.SpaceMaintainer,"29","",C);
				});
			Add(listScenes,group,"space-maintainer-range","Space maintainers over the ranges 19-21 (C, 20 missing) and 28-30 (TP, 29 missing).",relay => {
				SetMissing(relay,"20","29");
				Paint(relay,ToothPaintingType.SpaceMaintainer,"19,20,21","",C);
				Paint(relay,ToothPaintingType.SpaceMaintainer,"28,29,30","",TP);
			});
			Add(listScenes,group,"space-maintainer-quadrant","Space maintainers by quadrant: UR (4-8, TP) and LL (20-24, C), the teeth the Chart module "
				+"paints for a quadrant code.",relay => {
					Paint(relay,ToothPaintingType.SpaceMaintainer,"4,5,6,7,8","",TP);
					Paint(relay,ToothPaintingType.SpaceMaintainer,"20,21,22,23,24","",C);
				});
			#endregion Sealants, veneers, text, types drawn by nothing yet
			#region Whole mouths
			Add(listScenes,group,"restored-adult","A heavily restored adult: 1, 16, 17, 32 missing; bridge 12-13-14 (13 missing, EO); implant with crown on "
				+"30 (missing, C); RCT, post and crown on 3 (C); crowns on 8 and 9 (EC); fillings MOD 4, DO 5, O 15, MO 18, OB 19, MOD 20, DO 29, MO 31 "
				+"(EC and EO); sealants 2 and 31 (EO); veneers 23-26 (EO); TP: MOD 21, crown 28, X on 2, RCT and build-up on 14, watch on 19.",relay => {
					string[] arrayMissing={ "1","13","16","17","30","32" };
					SetMissing(relay,arrayMissing);
					foreach(string toothNum in new[] { "12","13","14" }) { Paint(relay,ToothPaintingType.BridgeDark,toothNum,"",EO,arrayMissing); }
					Paint(relay,ToothPaintingType.Implant,"30","",C);
					Paint(relay,ToothPaintingType.CrownDark,"30","",C);
					Paint(relay,ToothPaintingType.RCT,"3","",C);
					Paint(relay,ToothPaintingType.PostBU,"3","",C);
					Paint(relay,ToothPaintingType.CrownDark,"3","",C);
					Paint(relay,ToothPaintingType.CrownDark,"8","",EC);
					Paint(relay,ToothPaintingType.CrownDark,"9","",EC);
					Paint(relay,ToothPaintingType.FillingDark,"4","MOD",EC);
					Paint(relay,ToothPaintingType.FillingDark,"5","DO",EC);
					Paint(relay,ToothPaintingType.FillingDark,"15","O",EO);
					Paint(relay,ToothPaintingType.FillingDark,"18","MO",EO);
					Paint(relay,ToothPaintingType.FillingDark,"19","OB",EC);
					Paint(relay,ToothPaintingType.FillingDark,"20","MOD",EO);
					Paint(relay,ToothPaintingType.FillingDark,"29","DO",EC);
					Paint(relay,ToothPaintingType.FillingDark,"31","MO",EO);
					Paint(relay,ToothPaintingType.Sealant,"2","",EO);
					Paint(relay,ToothPaintingType.Sealant,"31","",EO);
					for(int t=23;t<=26;t++) {
						Paint(relay,ToothPaintingType.Veneer,t.ToString(),"",EO);
					}
					Paint(relay,ToothPaintingType.FillingDark,"21","MOD",TP);
					Paint(relay,ToothPaintingType.CrownDark,"28","",TP);
					Paint(relay,ToothPaintingType.Extraction,"2","",TP);
					Paint(relay,ToothPaintingType.RCT,"14","",TP);
					Paint(relay,ToothPaintingType.PostBU,"14","",TP);
					Paint(relay,ToothPaintingType.Text,"19","W",TP);
				});
			Add(listScenes,group,"treatment-plan","A treatment plan, all TP: X on 1, 16, 17 and 32; fillings MO 3, DO 4, MOD 13, O 14, MO 19, OL 30; RCT, "
				+"build-up and crown on 18; implant and crown on missing 29; bridge 6-7-8 with 7 missing; sealants 2 and 15.",relay => {
					SetMissing(relay,"7","29");
					foreach(string toothNum in new[] { "1","16","17","32" }) { Paint(relay,ToothPaintingType.Extraction,toothNum,"",TP); }
					Paint(relay,ToothPaintingType.FillingDark,"3","MO",TP);
					Paint(relay,ToothPaintingType.FillingDark,"4","DO",TP);
					Paint(relay,ToothPaintingType.FillingDark,"13","MOD",TP);
					Paint(relay,ToothPaintingType.FillingDark,"14","O",TP);
					Paint(relay,ToothPaintingType.FillingDark,"19","MO",TP);
					Paint(relay,ToothPaintingType.FillingDark,"30","OL",TP);
					Paint(relay,ToothPaintingType.RCT,"18","",TP);
					Paint(relay,ToothPaintingType.PostBU,"18","",TP);
					Paint(relay,ToothPaintingType.CrownDark,"18","",TP);
					Paint(relay,ToothPaintingType.Implant,"29","",TP);
					Paint(relay,ToothPaintingType.CrownDark,"29","",TP);
					foreach(string toothNum in new[] { "6","7","8" }) { Paint(relay,ToothPaintingType.BridgeDark,toothNum,"",TP,"7"); }
					Paint(relay,ToothPaintingType.Sealant,"2","",TP);
					Paint(relay,ToothPaintingType.Sealant,"15","",TP);
				});
			#endregion Whole mouths
		}

		private static void AddKnownDifferenceScenes(List<ChartScene> listScenes) {
			const string group="known-difference";
			Add(listScenes,group,"x-under-moved-neighbour","TP X on 4 and on 20, with 5 and 21 shifted 3 mm distally over them: DirectX draws the moved "
				+"neighbour after the X, so it can cover it (plan 5.1).",relay => {
					relay.MoveTooth("5",0,0,0,-3,0,0);
					relay.MoveTooth("21",0,0,0,-3,0,0);
					Paint(relay,ToothPaintingType.Extraction,"4","",TP);
					Paint(relay,ToothPaintingType.Extraction,"20","",TP);
				});
			Add(listScenes,group,"buildup-under-moved-neighbour","TP build-ups on 12 and 28, with 13 and 29 shifted 3 mm mesially over them (plan 5.1).",
				relay => {
					relay.MoveTooth("13",0,0,0,3,0,0);
					relay.MoveTooth("29",0,0,0,3,0,0);
					Paint(relay,ToothPaintingType.PostBU,"12","",TP);
					Paint(relay,ToothPaintingType.PostBU,"28","",TP);
				});
			Add(listScenes,group,"x-on-implant-tooth","TP X and TP implant on present tooth 19, and on missing tooth 14: DirectX draws the implant after the X "
				+"with depth testing on (plan 5.1).",relay => {
					SetMissing(relay,"14");
					foreach(string toothNum in new[] { "14","19" }) {
						Paint(relay,ToothPaintingType.Extraction,toothNum,"",TP);
						Paint(relay,ToothPaintingType.Implant,toothNum,"",TP);
					}
				});
		}

		private static void AddColorScenes(List<ChartScene> listScenes) {
			const string group="colours";
			Add(listScenes,group,"colour-code-override","Procedure codes with their own graphic colour, which overrides the status colour: purple crowns on "
				+"3 and 14, teal MOD fillings on 19 and 30, orange RCT on 8, magenta sealant on 2.",relay => {
					StatusColors purple=new StatusColors("code",Color.Purple.ToArgb(),Color.Purple.ToArgb());
					StatusColors teal=new StatusColors("code",Color.Teal.ToArgb(),Color.Teal.ToArgb());
					StatusColors orange=new StatusColors("code",Color.Orange.ToArgb(),Color.Orange.ToArgb());
					StatusColors magenta=new StatusColors("code",Color.Magenta.ToArgb(),Color.Magenta.ToArgb());
					Paint(relay,ToothPaintingType.CrownDark,"3","",purple);
					Paint(relay,ToothPaintingType.CrownDark,"14","",purple);
					Paint(relay,ToothPaintingType.FillingDark,"19","MOD",teal);
					Paint(relay,ToothPaintingType.FillingDark,"30","MOD",teal);
					Paint(relay,ToothPaintingType.RCT,"8","",orange);
					Paint(relay,ToothPaintingType.Sealant,"2","",magenta);
				});
			Add(listScenes,group,"colour-by-provider","Colour by provider: another provider's completed work drawn in the EO colours (the user preference "
				+"ToothChartUsesDiffColorByProv): C crowns on 3 and 19 and C fillings MO on 4 and 20 next to the same work shown as EO on 14, 30, 13 and 29.",
				relay => {
					Paint(relay,ToothPaintingType.CrownDark,"3","",C);
					Paint(relay,ToothPaintingType.CrownDark,"19","",C);
					Paint(relay,ToothPaintingType.FillingDark,"4","MO",C);
					Paint(relay,ToothPaintingType.FillingDark,"20","MO",C);
					Paint(relay,ToothPaintingType.CrownDark,"14","",EO);
					Paint(relay,ToothPaintingType.CrownDark,"30","",EO);
					Paint(relay,ToothPaintingType.FillingDark,"13","MO",EO);
					Paint(relay,ToothPaintingType.FillingDark,"29","MO",EO);
				});
			Add(listScenes,group,"colour-tpi-and-unhandled","The TPi exception and unhandled statuses: a code colour (purple) on a TP crown on 3, but white "
				+"on the same crown in status TPi on 14; white MOD fillings on 19 and 30 for a status the colour code does not handle (the Chart module "
				+"and sheets paint it white).",relay => {
					Paint(relay,ToothPaintingType.CrownDark,"3","",new StatusColors("code",Color.Purple.ToArgb(),Color.Purple.ToArgb()));
					Paint(relay,ToothPaintingType.CrownDark,"14","",Unhandled);
					Paint(relay,ToothPaintingType.FillingDark,"19","MOD",Unhandled);
					Paint(relay,ToothPaintingType.FillingDark,"30","MOD",Unhandled);
				});
			Add(listScenes,group,"colour-custom-chart-colours","Chart colours changed in Definitions: dark blue background, yellow numbers, white highlight "
				+"text on red; teeth 3, 8 and 30 selected, crowns on 3 and 14 (TP).",relay => {
					relay.ColorBackgroundMain=Color.FromArgb(20,30,90);
					relay.ColorText=Color.Yellow;
					relay.ColorTextHighlightFore=Color.White;
					relay.ColorTextHighlightBack=Color.Red;
					Paint(relay,ToothPaintingType.CrownDark,"3","",TP);
					Paint(relay,ToothPaintingType.CrownDark,"14","",TP);
					SetSelected(relay,"3","8","30");
				});
		}

		private static void AddInteractionScenes(List<ChartScene> listScenes) {
			const string group="interaction";
			Add(listScenes,group,"select-one","Tooth 3 selected (highlighted number).",relay => SetSelected(relay,"3"));
			Add(listScenes,group,"select-several","Teeth 3, 8, 14, 19, 25 and 30 selected.",relay => SetSelected(relay,"3","8","14","19","25","30"));
			Add(listScenes,group,"select-range","Teeth 6-11 selected, as a drag across them gives.",relay => SetSelected(relay,"6","7","8","9","10","11"));
			Add(listScenes,group,"select-whole-arch","Every maxillary tooth 1-16 selected.",
				relay => SetSelected(relay,Enumerable.Range(1,16).Select(x => x.ToString()).ToArray()));
			Add(listScenes,group,"select-primary-letter","Mixed dentition with the primary letters C, D and K selected (the primary band) and permanent "
				+"tooth 3 selected.",relay => {
					SetPrimary(relay,"6","7","20","21");
					SetSelected(relay,"C","D","K","3");
				});
			Add(listScenes,group,"select-missing-and-hidden","Missing tooth 3 selected (its number still shows) and hidden tooth 14 selected (no number to "
				+"highlight).",relay => {
					relay.SetMissing("3");
					relay.SetHidden("14");
					SetSelected(relay,"3","14");
				});
			Add(listScenes,group,"select-fdi","Teeth 3, 14, 19 and 30 selected in FDI numbering.",relay => SetSelected(relay,"3","14","19","30"))
				.Nomenclature=ToothNumberingNomenclature.FDI;
			Add(listScenes,group,"drawing-segments","Pen drawings in black, red and blue: a line across the maxillary teeth, a zig-zag over the mandibular "
				+"teeth and a short stroke near the centre line.",relay => {
					AddDrawing(relay,Color.Black,new PointF(40,60),new PointF(120,55),new PointF(200,50),new PointF(280,55),new PointF(370,60));
					AddDrawing(relay,Color.Red,new PointF(60,250),new PointF(90,220),new PointF(120,250),new PointF(150,220),new PointF(180,250));
					AddDrawing(relay,Color.Blue,new PointF(190,150),new PointF(205,160),new PointF(220,150));
				});
			Add(listScenes,group,"drawing-circles","Pen circles around teeth 3 and 19 (green) and 9 (purple), as a dentist marks a tooth.",relay => {
				AddDrawing(relay,Color.Green,GetCircle(75,70,22,24));
				AddDrawing(relay,Color.Green,GetCircle(290,235,22,24));
				AddDrawing(relay,Color.Purple,GetCircle(218,90,16,20));
			});
			Add(listScenes,group,"drawing-edges","Pen strokes along the edges and through the corners of the 410x307 drawing space, and one that runs past "
				+"them.",relay => {
					AddDrawing(relay,Color.Black,new PointF(0,0),new PointF(410,0),new PointF(410,307),new PointF(0,307),new PointF(0,0));
					AddDrawing(relay,Color.Red,new PointF(0,0),new PointF(410,307));
					AddDrawing(relay,Color.Blue,new PointF(-20,150),new PointF(430,160));
				});
			Add(listScenes,group,"drawing-erased","The drawing-segments scene after the eraser took the red zig-zag away: only the black and blue strokes "
				+"remain.",relay => {
					AddDrawing(relay,Color.Black,new PointF(40,60),new PointF(120,55),new PointF(200,50),new PointF(280,55),new PointF(370,60));
					AddDrawing(relay,Color.Blue,new PointF(190,150),new PointF(205,160),new PointF(220,150));
				});
			Add(listScenes,group,"drawing-recoloured","The drawing-segments scene after the colour changer made the black line orange.",relay => {
				AddDrawing(relay,Color.Orange,new PointF(40,60),new PointF(120,55),new PointF(200,50),new PointF(280,55),new PointF(370,60));
				AddDrawing(relay,Color.Red,new PointF(60,250),new PointF(90,220),new PointF(120,250),new PointF(150,220),new PointF(180,250));
				AddDrawing(relay,Color.Blue,new PointF(190,150),new PointF(205,160),new PointF(220,150));
			});
			Add(listScenes,group,"drawing-dense","A dense pen spiral of 200 points over the right maxillary teeth.",relay => {
				PointF[] arrayPoints=new PointF[200];
				for(int i=0;i<arrayPoints.Length;i++) {
					double angle=i*0.25;
					double radius=4+i*0.22;
					arrayPoints[i]=new PointF((float)Math.Round(100+radius*Math.Cos(angle),1),(float)Math.Round(80+radius*Math.Sin(angle),1));
				}
				AddDrawing(relay,Color.DarkRed,arrayPoints);
			});
		}

		private static void AddFreeTextScenes(List<ChartScene> listScenes) {
			const string group="free-text";
			Add(listScenes,group,"text-label-long","A long free-text label (\"Patient reports sensitivity to cold on the upper right\") at 20,140. Today's "
				+"chart draws no free text.",relay => relay.AddText("Patient reports sensitivity to cold on the upper right",new PointF(20,140),Color.Black,1));
			Add(listScenes,group,"text-labels-over-teeth","Free-text labels over teeth: \"cracked\" on 3, \"watch\" on 14, \"mobile\" on 24 and \"sore\" on "
				+"30, in four colours. Today's chart draws no free text.",relay => {
					relay.AddText("cracked",new PointF(60,40),Color.Red,2);
					relay.AddText("watch",new PointF(310,40),Color.Blue,3);
					relay.AddText("mobile",new PointF(195,260),Color.Green,4);
					relay.AddText("sore",new PointF(80,265),Color.Purple,5);
				});
			Add(listScenes,group,"text-labels-at-edges","Free-text labels at the corners and edges (0,0; 380,0; 0,290; 370,290; 200,0). Today's chart "
				+"draws no free text.",relay => {
					relay.AddText("TL",new PointF(0,0),Color.Black,6);
					relay.AddText("TR",new PointF(380,0),Color.Black,7);
					relay.AddText("BL",new PointF(0,290),Color.Black,8);
					relay.AddText("BR",new PointF(370,290),Color.Black,9);
					relay.AddText("top",new PointF(200,0),Color.Black,10);
				});
			Add(listScenes,group,"text-label-moved","A free-text label after a move to 250,180 (moving is TC-2 and TC-5). Today's chart draws no free text.",
				relay => relay.AddText("moved here",new PointF(250,180),Color.DarkBlue,11));
		}

		private static void AddOrthoScenes(List<ChartScene> listScenes) {
			const string group="ortho";
			string[] arrayMaxillary=Enumerable.Range(3,12).Select(x => x.ToString()).ToArray();
			string[] arrayMandibular=Enumerable.Range(19,12).Select(x => x.ToString()).ToArray();
			Add(listScenes,group,"ortho-brackets","Brackets on 3-14 and 19-30 (silver). Today's chart draws no orthodontic hardware (O-24).",relay => {
				foreach(string toothNum in arrayMaxillary.Concat(arrayMandibular)) {
					relay.SetBracket(toothNum,Color.Silver);
				}
			});
			Add(listScenes,group,"ortho-wires","Brackets on 3-14 and 19-30 with wires between each pair of brackets (steel blue).",relay => {
				foreach(string toothNum in arrayMaxillary.Concat(arrayMandibular)) {
					relay.SetBracket(toothNum,Color.Silver);
				}
				for(int i=0;i<arrayMaxillary.Length-1;i++) {
					relay.AddOrthoWireBetweenBrackets(arrayMaxillary[i],arrayMaxillary[i+1],Color.SteelBlue);
					relay.AddOrthoWireBetweenBrackets(arrayMandibular[i],arrayMandibular[i+1],Color.SteelBlue);
				}
			});
			Add(listScenes,group,"ortho-wire-in-bracket","Brackets on 3-14 with a wire inside each bracket (steel blue).",relay => {
				foreach(string toothNum in arrayMaxillary) {
					relay.SetBracket(toothNum,Color.Silver);
					relay.AddOrthoWireInBracket(toothNum,Color.SteelBlue);
				}
			});
			Add(listScenes,group,"ortho-elastics","Brackets on 3-14 and 19-30 with elastics 6 to 27 and 11 to 22 (red) and 3 to 30 (green).",relay => {
				foreach(string toothNum in arrayMaxillary.Concat(arrayMandibular)) {
					relay.SetBracket(toothNum,Color.Silver);
				}
				relay.AddOrthoElastic("6","27",Color.Red);
				relay.AddOrthoElastic("11","22",Color.Red);
				relay.AddOrthoElastic("3","30",Color.Green);
			});
			ChartScene sceneOrthoMode=Add(listScenes,group,"ortho-mode","Ortho mode on: the Chart module switches Universal numbering to Palmer "
				+"(ControlChart.FillToothChart); brackets on 3-14.",relay => {
					foreach(string toothNum in arrayMaxillary) {
						relay.SetBracket(toothNum,Color.Silver);
					}
				});
			sceneOrthoMode.IsOrthoMode=true;
			sceneOrthoMode.Nomenclature=ToothNumberingNomenclature.Palmer;
		}

		private static void AddSizeScenes(List<ChartScene> listScenes) {
			const string group="sizes";
			Add(listScenes,group,"size-big-window","The restored-adult work with teeth 3 and 19 selected, also drawn at 926x858 with the module colours (the "
				+"big chart window, FormToothChartBig).",relay => {
					ApplyRestoredSample(relay);
					SetSelected(relay,"3","19");
				}).ListSizesExtra.Add(SizeBigWindow);
			Add(listScenes,group,"size-sheet-wide","The restored-adult work, also drawn at 640x320 (a wide sheet field) with the printout colours.",
				relay => ApplyRestoredSample(relay)).ListSizesExtra.Add(SizeSheetWide);
			Add(listScenes,group,"size-sheet-tall","The restored-adult work, also drawn at 360x480 (a tall sheet field) with the printout colours.",
				relay => ApplyRestoredSample(relay)).ListSizesExtra.Add(SizeSheetTall);
		}

		///<summary>A short sample of work for the size scenes: missing 1, 16, 17, 32; crown 3 (C), MOD 14 (EC), RCT 19 (TP), X 30 (TP), sealant 2.</summary>
		private static void ApplyRestoredSample(ToothChartRelay relay) {
			SetMissing(relay,"1","16","17","32");
			Paint(relay,ToothPaintingType.CrownDark,"3","",C);
			Paint(relay,ToothPaintingType.FillingDark,"14","MOD",EC);
			Paint(relay,ToothPaintingType.RCT,"19","",TP);
			Paint(relay,ToothPaintingType.Extraction,"30","",TP);
			Paint(relay,ToothPaintingType.Sealant,"2","",TP);
		}

		private static void AddPerioScenes(List<ChartScene> listScenes) {
			AddPerio(listScenes,"perio-full-exam","A complete synthetic exam: probing 2-4 mm, gingival margins 0-2 mm, MGJ on the buccal and the mandibular "
				+"lingual, some bleeding and suppuration, furcations 1-3 on molars, mobility 1-2 on a few teeth.",new PerioExamSpec {
					FuncFurcation=(t,s) => (t+s)%4==0?1+(t%3):-1,
					FuncMobility=t => t%7==0?1+(t%2):-1,
				});
			AddPerio(listScenes,"perio-empty","A perio chart with no measurements.",new PerioExamSpec {
				FuncProbing=(t,s) => -1,
				FuncGM=(t,s) => -1,
				FuncMGJ=(t,s) => -1,
				FuncBleeding=(t,s) => 0,
			});
			AddPerio(listScenes,"perio-missing-and-implants","The full exam with 1, 5, 16, 17 and 32 skipped (missing), an implant with a crown on 19 and an "
				+"implant without a crown on 30.",new PerioExamSpec {
					ListSkipped=new List<int> { 1,5,16,17,32 },
					ListImplants=new List<int> { 19,30 },
					ListImplantCrowns=new List<int> { 19 },
					FuncFurcation=(t,s) => (t+s)%4==0?1+(t%3):-1,
				});
			AddPerio(listScenes,"perio-deep-pockets","Deep pockets: probing 5-9 mm everywhere (red from 4 mm), recession 1-3 mm.",new PerioExamSpec {
				FuncProbing=(t,s) => 5+(t+s)%5,
				FuncGM=(t,s) => 1+(t+s)%3,
			});
			AddPerio(listScenes,"perio-hyperplasia","Gingival hyperplasia: gingival margins 101-104 (1-4 mm above the CEJ) on the maxilla, recession 2 mm on "
				+"the mandible.",new PerioExamSpec {
					FuncGM=(t,s) => t<=16?101+(t+s)%4:2,
				});
			ChartScene sceneFurcations=AddPerio(listScenes,"perio-furcations","Furcation grades 1, 2 and 3 on every molar site that has one, with every "
				+"grade drawn in the red furcation colour (red limit 1, the default).",new PerioExamSpec {
					FuncFurcation=(t,s) => 1+(t+s)%3,
				});
			sceneFurcations.Description+=" The default red limit is 1, so all furcations are red.";
			AddPerio(listScenes,"perio-bleeding-suppuration","Bleeding on every buccal site and suppuration on every lingual site of the anterior teeth "
				+"6-11 and 22-27; both on 3 and 30.",new PerioExamSpec {
					FuncBleeding=(t,s) => (t==3 || t==30)?(int)(BleedingFlags.Blood|BleedingFlags.Suppuration)
						:((t>=6 && t<=11) || (t>=22 && t<=27))?(s<3?(int)BleedingFlags.Blood:(int)BleedingFlags.Suppuration):0,
				});
			AddPerio(listScenes,"perio-mobility","Mobility 0 (black), 1, 2 and 3 (red from 1, the default) on the incisors and molars.",new PerioExamSpec {
				FuncMobility=t => (t>=7 && t<=10) || (t>=23 && t<=26) || t==3 || t==14 || t==19 || t==30?t%4:-1,
			});
			AddPerio(listScenes,"perio-fdi","The full exam in FDI numbering.",new PerioExamSpec {
				FuncFurcation=(t,s) => (t+s)%4==0?1+(t%3):-1,
				FuncMobility=t => t%7==0?1+(t%2):-1,
			}).Nomenclature=ToothNumberingNomenclature.FDI;
			AddPerio(listScenes,"perio-partial-exam","A partial exam: only the maxilla is probed, the mandible has no measurements.",new PerioExamSpec {
				FuncProbing=(t,s) => t<=16?2+(t+s)%3:-1,
				FuncGM=(t,s) => t<=16?(t+s)%3:-1,
				FuncMGJ=(t,s) => t<=16 && s<3?4:-1,
				FuncBleeding=(t,s) => t<=16 && (t+s)%5==0?(int)BleedingFlags.Blood:0,
			});
		}

		private static ChartScene AddPerio(List<ChartScene> listScenes,string name,string description,PerioExamSpec spec) {
			ChartScene scene=Add(listScenes,"perio",name,description,relay => AddPerioExam(relay,spec));
			scene.Kind=ChartSceneKind.Perio;
			return scene;
		}
		#endregion Scenes
	}
}
