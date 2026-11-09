// Synthetic patients and scenes for the chart's call-log tests (package TC-2). The patients live in the scratch database of ChartTestDb and
// cover every paint type and every procedure status, every kind of ToothInitial, orthodontic hardware with two exams and hidden pieces, the
// Chart module's date slider and the per-provider colours. Each scene names the inputs one caller gives its chart code; every run of a scene
// reads its data from the database again, so one caller's run cannot change another's (the procedure filters change the lists they get).
// No real patient, practice or person: every name is "Synthetic".
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using CodeBase;
using DataConnectionBase;
using OpenPmsDentalBusiness;

namespace ToothChartImageTests {
	///<summary>The user state a scene runs with: whether the logged-on user has the per-provider tooth colours (ToothChartUsesDiffColorByProv)
	///and which provider the user is (0 for none).</summary>
	public class CallLogUser {
		public bool IsToothColorPref;
		public long ProvNum;

		public static readonly CallLogUser Plain=new CallLogUser();
	}

	///<summary>A Chart module scene (ControlChart.FillToothChart and, when IsPrintRecolor, the recolour of menuItemChartSave).</summary>
	public class ChartModuleScene {
		public string Name;
		public long PatNum;
		public DateTime DateLimit;
		///<summary>Null: "Treatment Plans" unchecked. Otherwise the ProcNums of the selected plans' ProcTPs.</summary>
		public Func<List<long>> FuncProcNumsTreatPlans;
		public bool IsOrthoGraphics=true;
		public DateTime DateOrthoExam;
		public bool IsShowHidden;
		public bool IsPrintRecolor;
		public CallLogUser User=CallLogUser.Plain;
		public bool HasTodaysAppointment;
	}

	///<summary>A Treatment Plan printout scene (ControlTreat.PrepImageForPrinting). FuncProceduresFiltered gives what ComputeProcListFiltered would.</summary>
	public class TreatPlanScene {
		public string Name;
		public long PatNum;
		public Func<long,List<Procedure>> FuncProceduresFiltered;
		public CallLogUser User=CallLogUser.Plain;
	}

	///<summary>A sheet or Patient Dashboard scene (SheetPrinting.GetToothChartHelper).</summary>
	public class SheetScene {
		public string Name;
		public long PatNum;
		public bool ShowCompleted;
		public Func<long,TreatPlan> FuncTreatPlan;
		public Func<long,List<Procedure>> FuncProceduresOverride;
		public bool IsInPatientDashboard;
		public bool HasTodaysAppointment;
		public CallLogUser User=CallLogUser.Plain;
	}

	///<summary>A big chart scene (FormToothChartingBig.FillToothChart): the rows, initials and hardware the Chart module hands it.</summary>
	public class BigChartScene {
		public string Name;
		public long PatNum;
		public bool HasTodaysAppointment;
		public CallLogUser User=CallLogUser.Plain;
	}

	///<summary>The fixture patients and the scenes of the four callers.</summary>
	public static class CallLogFixtures {
		public const string CALLER_CHART="ControlChart";
		public const string CALLER_TREAT="ControlTreat";
		public const string CALLER_SHEET="SheetPrinting";
		public const string CALLER_BIG="FormToothChartBig";
		///<summary>The synthetic users the scenes run as: one who is no provider and one who is provider 1 (userod rows, because the user cache
		///replaces Security.CurUser with its own copy).</summary>
		public static long UserNumPlain;
		public static long UserNumProv1;

		public static long ProvNum1;
		public static long ProvNum2;
		public static long PatNumPaint;
		public static long PatNumSlider;
		public static long PatNumOrtho;
		public static long PatNumProvColors;
		public static long PatNumEmpty;
		private static bool _isBuilt;
		private static readonly object _lock=new object();
		public static readonly DateTime DateExam1=new DateTime(2024,5,1);
		public static readonly DateTime DateExam2=new DateTime(2024,8,1);

		///<summary>The procedure codes: (code, paint type, treatment area, graphic colour, paint text, also tooth range).</summary>
		private static readonly object[][] _arrayCodes=new object[][] {
			new object[] { "Z2000",ToothPaintingType.None,TreatmentArea.Tooth,0,"",false },
			new object[] { "Z2001",ToothPaintingType.Extraction,TreatmentArea.Tooth,0,"",false },
			new object[] { "Z2002",ToothPaintingType.Implant,TreatmentArea.Tooth,0,"",false },
			new object[] { "Z2003",ToothPaintingType.RCT,TreatmentArea.Tooth,0,"",false },
			new object[] { "Z2004",ToothPaintingType.PostBU,TreatmentArea.Tooth,0,"",false },
			new object[] { "Z2005",ToothPaintingType.FillingDark,TreatmentArea.Surf,0,"",false },
			new object[] { "Z2006",ToothPaintingType.FillingLight,TreatmentArea.Surf,0,"",false },
			new object[] { "Z2007",ToothPaintingType.CrownDark,TreatmentArea.Tooth,0,"",false },
			new object[] { "Z2008",ToothPaintingType.CrownLight,TreatmentArea.Tooth,0,"",false },
			new object[] { "Z2009",ToothPaintingType.BridgeDark,TreatmentArea.Tooth,0,"",false },
			new object[] { "Z2010",ToothPaintingType.BridgeLight,TreatmentArea.Tooth,0,"",false },
			new object[] { "Z2011",ToothPaintingType.DentureDark,TreatmentArea.Arch,0,"",false },
			new object[] { "Z2012",ToothPaintingType.DentureLight,TreatmentArea.ToothRange,0,"",false },
			new object[] { "Z2013",ToothPaintingType.Sealant,TreatmentArea.Tooth,0,"",false },
			new object[] { "Z2014",ToothPaintingType.Veneer,TreatmentArea.Tooth,0,"",false },
			new object[] { "Z2015",ToothPaintingType.Text,TreatmentArea.Tooth,0,"W",false },
			new object[] { "Z2016",ToothPaintingType.Text,TreatmentArea.Tooth,0,"Abc",false },
			new object[] { "Z2017",ToothPaintingType.RetainedRoot,TreatmentArea.Tooth,0,"",false },
			new object[] { "Z2018",ToothPaintingType.SpaceMaintainer,TreatmentArea.Tooth,0,"",false },
			new object[] { "Z2019",ToothPaintingType.SpaceMaintainer,TreatmentArea.ToothRange,0,"",false },
			new object[] { "Z2020",ToothPaintingType.SpaceMaintainer,TreatmentArea.Quad,0,"",false },
			new object[] { "Z2021",ToothPaintingType.SpaceMaintainer,TreatmentArea.Arch,0,"",true },
			new object[] { "Z2022",ToothPaintingType.TextBelow,TreatmentArea.Tooth,0,"Below",false },
			new object[] { "Z2023",ToothPaintingType.CrownDark,TreatmentArea.Tooth,Color.FromArgb(255,12,34,56).ToArgb(),"",false },
			new object[] { "Z2024",ToothPaintingType.FillingDark,TreatmentArea.Mouth,0,"",false },
			new object[] { "Z2025",ToothPaintingType.DentureDark,TreatmentArea.ToothRange,0,"",false },
			new object[] { "Z2026",ToothPaintingType.FillingLight,TreatmentArea.Surf,Color.FromArgb(255,90,10,160).ToArgb(),"",false },
		};
		private static readonly ProcStat[] _arrayStatuses=new[] { ProcStat.TP,ProcStat.C,ProcStat.EC,ProcStat.EO,ProcStat.R,ProcStat.D,ProcStat.Cn,ProcStat.TPi };
		private static readonly string[] _arraySurfaces=new[] { "MOD","B","L","O","MODBL","I","V","F" };
		private static readonly string[] _arrayQuads=new[] { "UR","UL","LL","LR","" };

		///<summary>Makes the database and the fixture patients the first time; marks the calling test inconclusive without a server.</summary>
		public static void EnsureBuilt() {
			ChartTestDb.EnsureOrSkip();
			lock(_lock) {
				if(_isBuilt) {
					return;
				}
				SetCulture();
				Build();
				_isBuilt=true;
			}
		}

		///<summary>The call logs are compared in en-US: the progress notes table writes its dates with the workstation's short date format.</summary>
		public static void SetCulture() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
		}

		private static void Build() {
			//The chart's 18 colours (ChartGraphicColors), replaced by synthetic ones so the logs do not follow the new database's defaults.
			DataCore.NonQ("DELETE FROM definition WHERE Category="+POut.Int((int)DefCat.ChartGraphicColors));
			for(int i=0;i<18;i++) {
				OpenPmsDentalBusiness.Crud.DefCrud.Insert(new Def { Category=DefCat.ChartGraphicColors,ItemOrder=i,ItemName="Synthetic colour "+i,
					ItemValue="",ItemColor=Color.FromArgb(255,(i*13)%256,(40+i*7)%256,(200+i*11)%256),IsHidden=false });
			}
			ProvNum1=OpenPmsDentalBusiness.Crud.ProviderCrud.Insert(new Provider { Abbr="SYN1",LName="Synthetic",FName="One" });
			ProvNum2=OpenPmsDentalBusiness.Crud.ProviderCrud.Insert(new Provider { Abbr="SYN2",LName="Synthetic",FName="Two" });
			UserNumPlain=OpenPmsDentalBusiness.Crud.UserodCrud.Insert(new Userod { UserName="SyntheticPlain",ProvNum=0 });
			UserNumProv1=OpenPmsDentalBusiness.Crud.UserodCrud.Insert(new Userod { UserName="SyntheticProv1",ProvNum=ProvNum1 });
			Dictionary<string,long> dictCodeNums=new Dictionary<string,long>();
			foreach(object[] arrayCode in _arrayCodes) {
				ProcedureCode procedureCode=new ProcedureCode {
					ProcCode=(string)arrayCode[0],
					Descript="Synthetic "+arrayCode[1],
					AbbrDesc="Syn"+((string)arrayCode[0]).Substring(1),
					ProcTime="/X/",
					PaintType=(ToothPaintingType)arrayCode[1],
					TreatArea=(TreatmentArea)arrayCode[2],
					GraphicColor=Color.FromArgb((int)arrayCode[3]),
					PaintText=(string)arrayCode[4],
					AreaAlsoToothRange=(bool)arrayCode[5],
				};
				dictCodeNums[procedureCode.ProcCode]=OpenPmsDentalBusiness.Crud.ProcedureCodeCrud.Insert(procedureCode);
			}
			Cache.Refresh(InvalidType.Defs,InvalidType.ProcCodes,InvalidType.Providers,InvalidType.Security);
			PatNumPaint=InsertPatient("Paint");
			PatNumSlider=InsertPatient("Slider");
			PatNumOrtho=InsertPatient("Ortho");
			PatNumProvColors=InsertPatient("ProvColors");
			PatNumEmpty=InsertPatient("Empty");
			BuildPaint(dictCodeNums);
			BuildSlider(dictCodeNums);
			BuildOrtho();
			BuildProvColors(dictCodeNums);
			OrthoHardwareSpecs.ClearCache();
		}

		private static long InsertPatient(string firstName) {
			return OpenPmsDentalBusiness.Crud.PatientCrud.Insert(new Patient { LName="Synthetic",FName=firstName,PriProv=ProvNum1,
				PatStatus=PatientStatus.Patient,Birthdate=new DateTime(1980,1,1) });
		}

		private static void InsertProc(long patNum,long codeNum,string toothNum,string surf,string toothRange,ProcStat procStat,long provNum,
			DateTime dateProc,DateTime dateTP,DateTime dateEntryC,bool isHidden=false,bool isLocked=false)
		{
			long procNum=OpenPmsDentalBusiness.Crud.ProcedureCrud.Insert(new Procedure { PatNum=patNum,CodeNum=codeNum,ToothNum=toothNum,Surf=surf,
				ToothRange=toothRange,ProcStatus=procStat,ProvNum=provNum,ProcDate=dateProc,DateTP=dateTP,HideGraphics=isHidden,IsLocked=isLocked });
			//The insert writes the entry date as now (DateEntryEditable), so the completion date the slider compares is set afterwards.
			DataCore.NonQ("UPDATE procedurelog SET DateEntryC="+POut.Date(dateEntryC)+" WHERE ProcNum="+POut.Long(procNum));
		}

		private static void InsertInitial(long patNum,string toothNum,ToothInitialType toothInitialType,float movement=0,string drawingSegment="",
			Color colorDraw=default(Color),string drawText="")
		{
			OpenPmsDentalBusiness.Crud.ToothInitialCrud.Insert(new ToothInitial { PatNum=patNum,ToothNum=toothNum,InitialType=toothInitialType,
				Movement=movement,DrawingSegment=drawingSegment,ColorDraw=colorDraw,DrawText=drawText });
		}

		///<summary>Every code in every status, on teeth that cycle through 1-32, with the surfaces, ranges, quadrants and arches their areas use;
		///bridges and dentures over missing and hidden teeth; a hidden and a locked deleted procedure; and every kind of ToothInitial.</summary>
		private static void BuildPaint(Dictionary<string,long> dictCodeNums) {
			DateTime dateTP=new DateTime(2021,1,4);
			int index=0;
			foreach(object[] arrayCode in _arrayCodes) {
				string code=(string)arrayCode[0];
				TreatmentArea treatmentArea=(TreatmentArea)arrayCode[2];
				for(int s=0;s<_arrayStatuses.Length;s++) {
					ProcStat procStat=_arrayStatuses[s];
					string toothNum=((index%32)+1).ToString();
					string surf="";
					string toothRange="";
					switch(treatmentArea) {
						case TreatmentArea.Surf:
							surf=_arraySurfaces[s];
							break;
						case TreatmentArea.Arch:
							toothNum="";
							surf=(s%2==0) ? "U" : "L";
							if((bool)arrayCode[5] && s%3==0) {
								toothRange="22,23,24";
							}
							break;
						case TreatmentArea.ToothRange:
							toothNum="";
							toothRange=new[] { "2,3,4","6,7,8","27,28" }[s%3];
							break;
						case TreatmentArea.Quad:
							toothNum="";
							surf=_arrayQuads[s%_arrayQuads.Length];
							break;
						case TreatmentArea.Mouth:
							toothNum="";
							break;
					}
					if(code=="Z2018" && s==7) {
						toothNum="";//a tooth-area space maintainer without a tooth draws nothing
					}
					DateTime dateEntryC=procStat.In(ProcStat.C,ProcStat.EC,ProcStat.EO) ? new DateTime(2021,2,1).AddDays(index%20) : DateTime.MinValue;
					InsertProc(PatNumPaint,dictCodeNums[code],toothNum,surf,toothRange,procStat,ProvNum1,new DateTime(2021,3,1).AddDays(index%40),dateTP,
						dateEntryC,isLocked:procStat==ProcStat.D);
					index++;
				}
			}
			//Bridges over a missing tooth (3) and a hidden one (14), dentures over them, and a hidden procedure.
			foreach(string code in new[] { "Z2009","Z2010" }) {
				InsertProc(PatNumPaint,dictCodeNums[code],"3","","",ProcStat.TP,ProvNum1,new DateTime(2021,4,1),dateTP,DateTime.MinValue);
				InsertProc(PatNumPaint,dictCodeNums[code],"14","","",ProcStat.C,ProvNum1,new DateTime(2021,4,2),dateTP,new DateTime(2021,4,2));
			}
			InsertProc(PatNumPaint,dictCodeNums["Z2025"],"","","13,14,15",ProcStat.C,ProvNum1,new DateTime(2021,4,3),dateTP,new DateTime(2021,4,3));
			InsertProc(PatNumPaint,dictCodeNums["Z2007"],"30","","",ProcStat.C,ProvNum1,new DateTime(2021,4,4),dateTP,new DateTime(2021,4,4),isHidden:true);
			//A primary tooth's procedure.
			InsertProc(PatNumPaint,dictCodeNums["Z2005"],"J","DO","",ProcStat.TP,ProvNum1,new DateTime(2021,4,5),dateTP,DateTime.MinValue);
			//Every kind of ToothInitial.
			InsertInitial(PatNumPaint,"3",ToothInitialType.Missing);
			InsertInitial(PatNumPaint,"6",ToothInitialType.Missing);
			InsertInitial(PatNumPaint,"14",ToothInitialType.Hidden);
			foreach(string tooth in new[] { "4","13","20","29","8","21","28" }) {
				InsertInitial(PatNumPaint,tooth,ToothInitialType.Primary);
			}
			InsertInitial(PatNumPaint,"K",ToothInitialType.Missing);
			InsertInitial(PatNumPaint,"9",ToothInitialType.ShiftM,1.5f);
			InsertInitial(PatNumPaint,"9",ToothInitialType.ShiftM,-0.25f);
			InsertInitial(PatNumPaint,"10",ToothInitialType.ShiftO,-2f);
			InsertInitial(PatNumPaint,"11",ToothInitialType.ShiftB,0.75f);
			InsertInitial(PatNumPaint,"12",ToothInitialType.Rotate,15f);
			InsertInitial(PatNumPaint,"21",ToothInitialType.TipM,-10f);
			InsertInitial(PatNumPaint,"22",ToothInitialType.TipB,7.5f);
			InsertInitial(PatNumPaint,"",ToothInitialType.Drawing,0,"45,68;48,70;49,72",Color.FromArgb(255,200,0,0));
			InsertInitial(PatNumPaint,"",ToothInitialType.Drawing,0,"100.5,200.25;110,210",Color.FromArgb(255,0,0,200));
			InsertInitial(PatNumPaint,"",ToothInitialType.Drawing,0,"45,68;48,70;49,72",Color.FromArgb(255,0,150,0));
			InsertInitial(PatNumPaint,"",ToothInitialType.Text,0,"",Color.FromArgb(255,0,128,0),"25.3,123.8;This shows");
			InsertInitial(PatNumPaint,"",ToothInitialType.Text,0,"",Color.FromArgb(255,80,80,80),"200,50;Second");
			InsertInitial(PatNumPaint,"",ToothInitialType.Text,0,"",Color.FromArgb(255,1,2,3),"No point");
		}

		///<summary>Procedures whose treatment-plan and completion dates the slider passes: completed and existing extractions (with their missing
		///teeth), a crown completed after it was planned, a planned extraction, a referral and an existing condition.</summary>
		private static void BuildSlider(Dictionary<string,long> dictCodeNums) {
			InsertProc(PatNumSlider,dictCodeNums["Z2001"],"19","","",ProcStat.C,ProvNum1,new DateTime(2022,3,15),new DateTime(2022,1,10),new DateTime(2022,3,15));
			InsertInitial(PatNumSlider,"19",ToothInitialType.Missing);
			InsertProc(PatNumSlider,dictCodeNums["Z2001"],"31","","",ProcStat.C,ProvNum1,new DateTime(2022,2,1),new DateTime(2022,1,10),new DateTime(2022,2,1));
			InsertInitial(PatNumSlider,"31",ToothInitialType.Missing);
			InsertProc(PatNumSlider,dictCodeNums["Z2001"],"2","","",ProcStat.EO,ProvNum1,new DateTime(2022,1,5),DateTime.MinValue,new DateTime(2022,1,5));
			InsertInitial(PatNumSlider,"2",ToothInitialType.Missing);
			InsertProc(PatNumSlider,dictCodeNums["Z2001"],"30","","",ProcStat.TP,ProvNum1,new DateTime(2022,2,1),new DateTime(2022,2,1),DateTime.MinValue);
			InsertProc(PatNumSlider,dictCodeNums["Z2007"],"8","","",ProcStat.C,ProvNum1,new DateTime(2022,2,20),new DateTime(2022,1,10),new DateTime(2022,2,20));
			InsertProc(PatNumSlider,dictCodeNums["Z2005"],"14","MOD","",ProcStat.C,ProvNum1,new DateTime(2022,2,25),new DateTime(2022,2,25),new DateTime(2022,2,25));
			InsertProc(PatNumSlider,dictCodeNums["Z2003"],"5","","",ProcStat.R,ProvNum1,new DateTime(2022,1,15),new DateTime(2022,1,15),new DateTime(2022,1,15));
			InsertProc(PatNumSlider,dictCodeNums["Z2008"],"12","","",ProcStat.EC,ProvNum1,new DateTime(2021,12,1),DateTime.MinValue,new DateTime(2021,12,1));
			InsertProc(PatNumSlider,dictCodeNums["Z2013"],"18","","",ProcStat.Cn,ProvNum1,new DateTime(2022,1,20),new DateTime(2022,1,20),new DateTime(2022,1,20));
		}

		///<summary>Two exams of brackets, wires (both directions, one of a single tooth, one with commas) and elastics, with hidden pieces.</summary>
		private static void BuildOrtho() {
			long specBracket=OpenPmsDentalBusiness.Crud.OrthoHardwareSpecCrud.Insert(new OrthoHardwareSpec { OrthoHardwareType=EnumOrthoHardwareType.Bracket,
				Description="Synthetic bracket",ItemColor=Color.FromArgb(255,30,60,200),ItemOrder=0 });
			long specBracketHidden=OpenPmsDentalBusiness.Crud.OrthoHardwareSpecCrud.Insert(new OrthoHardwareSpec { OrthoHardwareType=EnumOrthoHardwareType.Bracket,
				Description="Synthetic hidden bracket",ItemColor=Color.FromArgb(255,90,90,90),IsHidden=true,ItemOrder=1 });
			long specWire=OpenPmsDentalBusiness.Crud.OrthoHardwareSpecCrud.Insert(new OrthoHardwareSpec { OrthoHardwareType=EnumOrthoHardwareType.Wire,
				Description="Synthetic wire",ItemColor=Color.FromArgb(255,180,180,190),ItemOrder=0 });
			long specElastic=OpenPmsDentalBusiness.Crud.OrthoHardwareSpecCrud.Insert(new OrthoHardwareSpec { OrthoHardwareType=EnumOrthoHardwareType.Elastic,
				Description="Synthetic elastic",ItemColor=Color.FromArgb(255,220,40,40),ItemOrder=0 });
			for(int t=3;t<=14;t++) {
				InsertOrtho(DateExam1,EnumOrthoHardwareType.Bracket,t==7 ? specBracketHidden : specBracket,t.ToString(),false);
			}
			InsertOrtho(DateExam1,EnumOrthoHardwareType.Bracket,specBracket,"30",true);
			InsertOrtho(DateExam1,EnumOrthoHardwareType.Wire,specWire,"3-14",false);
			InsertOrtho(DateExam1,EnumOrthoHardwareType.Wire,specWire,"30-19",true);
			InsertOrtho(DateExam1,EnumOrthoHardwareType.Wire,specWire,"8-8",false);
			InsertOrtho(DateExam1,EnumOrthoHardwareType.Wire,specWire,"3,5",false);
			InsertOrtho(DateExam1,EnumOrthoHardwareType.Elastic,specElastic,"6,11",false);
			InsertOrtho(DateExam1,EnumOrthoHardwareType.Elastic,specElastic,"22,27,28",false);
			InsertOrtho(DateExam1,EnumOrthoHardwareType.Elastic,specElastic,"9",false);
			for(int t=19;t<=30;t++) {
				InsertOrtho(DateExam2,EnumOrthoHardwareType.Bracket,specBracket,t.ToString(),false);
			}
			InsertOrtho(DateExam2,EnumOrthoHardwareType.Wire,specWire,"19-30",false);
			InsertOrtho(DateExam2,EnumOrthoHardwareType.Elastic,specElastic,"3,30",false);
			InsertOrtho(DateExam2,EnumOrthoHardwareType.Elastic,specElastic,"14,19",true);
			InsertInitial(PatNumOrtho,"5",ToothInitialType.Missing);
		}

		private static void InsertOrtho(DateTime dateExam,EnumOrthoHardwareType orthoHardwareType,long specNum,string toothRange,bool isHidden) {
			OpenPmsDentalBusiness.Crud.OrthoHardwareCrud.Insert(new OrthoHardware { PatNum=PatNumOrtho,DateExam=dateExam,OrthoHardwareType=orthoHardwareType,
				OrthoHardwareSpecNum=specNum,ToothRange=toothRange,Note="",IsHidden=isHidden });
		}

		///<summary>Completed work by both providers, so the per-provider colours show other providers' completed work as existing.</summary>
		private static void BuildProvColors(Dictionary<string,long> dictCodeNums) {
			DateTime date=new DateTime(2023,6,1);
			InsertProc(PatNumProvColors,dictCodeNums["Z2007"],"3","","",ProcStat.C,ProvNum1,date,date,date);
			InsertProc(PatNumProvColors,dictCodeNums["Z2007"],"4","","",ProcStat.C,ProvNum2,date,date,date);
			InsertProc(PatNumProvColors,dictCodeNums["Z2005"],"5","MOD","",ProcStat.C,ProvNum2,date,date,date);
			InsertProc(PatNumProvColors,dictCodeNums["Z2006"],"12","B","",ProcStat.TP,ProvNum2,date,date,DateTime.MinValue);
			InsertProc(PatNumProvColors,dictCodeNums["Z2008"],"7","","",ProcStat.EC,ProvNum2,date,DateTime.MinValue,date);
			InsertProc(PatNumProvColors,dictCodeNums["Z2023"],"19","","",ProcStat.C,ProvNum2,date,date,date);
			InsertProc(PatNumProvColors,dictCodeNums["Z2013"],"30","","",ProcStat.C,ProvNum1,date,date,date);
		}

		#region Per-scene data, read from the database each time
		///<summary>Sets the user state of a scene: Security.CurUser and the per-provider colour preference, with the caches the chart reads.</summary>
		public static void ApplyUser(CallLogUser callLogUser) {
			if(callLogUser.ProvNum!=0 && callLogUser.ProvNum!=ProvNum1) {
				throw new ArgumentException("The scenes' users are provider 1 or no provider.");
			}
			long userNum=(callLogUser.ProvNum==0) ? UserNumPlain : UserNumProv1;
			Security.CurUser=Userods.GetFirstOrDefault(x => x.UserNum==userNum);
			DataCore.NonQ("DELETE FROM userodpref WHERE FkeyType="+POut.Int((int)UserOdFkeyType.ToothChartUsesDiffColorByProv));
			if(callLogUser.IsToothColorPref) {
				OpenPmsDentalBusiness.Crud.UserOdPrefCrud.Insert(new UserOdPref { UserNum=userNum,FkeyType=UserOdFkeyType.ToothChartUsesDiffColorByProv,
					Fkey=0,ValueString="1" });
			}
			UserOdPrefs.ClearCache();
			OpenPmsDental.ToothChartRelay.RefreshToothColorsPrefs();
		}

		public static Patient GetPatient(long patNum) {
			return Patients.GetPat(patNum);
		}

		///<summary>A scheduled appointment today with provider 2, as Pd.ListAppointments holds it, or an empty list.</summary>
		public static List<Appointment> GetAppointments(long patNum,bool hasTodaysAppointment) {
			List<Appointment> listAppointments=new List<Appointment>();
			if(hasTodaysAppointment) {
				listAppointments.Add(new Appointment { PatNum=patNum,AptDateTime=DateTime.Today.AddHours(9),ProvNum=ProvNum2,AptStatus=ApptStatus.Scheduled });
			}
			return listAppointments;
		}

		///<summary>The progress notes table the Chart module gets (Pd.TableProgNotes).</summary>
		public static DataTable GetProgNotes(long patNum) {
			return ChartModules.GetProgNotes(patNum,false,new ChartModuleFilters());
		}

		///<summary>The procedure rows of the progress notes, as ControlChart.FillProgNotes keeps them for the chart (_listDataRowsProcsOrig): rows
		///of a separate copy of the table.</summary>
		public static List<DataRow> GetProcRowsOrig(DataTable tableProgNotes) {
			return tableProgNotes.Copy().Select().Where(x => x["ProcNum"].ToString()!="0" && x["ProcNum"].ToString()!="").ToList();
		}

		///<summary>Every procedure of the patient, deleted ones included, in the order they were entered.</summary>
		public static List<Procedure> GetAllProcedures(long patNum) {
			return OpenPmsDentalBusiness.Crud.ProcedureCrud.SelectMany("SELECT * FROM procedurelog WHERE PatNum="+POut.Long(patNum)+" ORDER BY ProcNum");
		}

		///<summary>A plan of the patient's planned procedures (every TP), as the sheets and the dashboard get the active plan.</summary>
		public static TreatPlan GetTreatPlanAllTP(long patNum) {
			TreatPlan treatPlan=new TreatPlan { PatNum=patNum,TPStatus=TreatPlanStatus.Active,Heading="Synthetic plan" };
			treatPlan.ListProcTPs=new List<ProcTP>();
			foreach(Procedure procedure in Procedures.Refresh(patNum).FindAll(x => x.ProcStatus==ProcStat.TP)) {
				ProcTP procTP=new ProcTP();
				procTP.ProcNumOrig=procedure.ProcNum;
				procTP.ToothNumTP=Tooth.Display(procedure.ToothNum);
				procTP.ProcCode=ProcedureCodes.GetStringProcCode(procedure.CodeNum);
				if(ProcedureCodes.GetProcCode(procedure.CodeNum).TreatArea==TreatmentArea.Surf) {
					procTP.Surf=Tooth.SurfTidyFromDbToDisplay(procedure.Surf,procedure.ToothNum);
				}
				else {
					procTP.Surf=procedure.Surf;
				}
				treatPlan.ListProcTPs.Add(procTP);
			}
			return treatPlan;
		}

		///<summary>The procedures a Treatment Plan printout or a sheet draws: completed work when showCompleted, referrals, conditions and the
		///plan's procedures as planned, sorted by date and status (OpenPmsDentalBusiness.SheetPrinting, the rule ControlTreat's
		///ComputeProcListFiltered also follows).</summary>
		public static List<Procedure> GetFilteredProcedures(long patNum,bool showCompleted) {
			List<Procedure> listProcedures=OpenPmsDentalBusiness.SheetPrinting.FilterProceduresForToothChart(Procedures.Refresh(patNum),
				GetTreatPlanAllTP(patNum),showCompleted);
			listProcedures.Sort(OpenPmsDentalBusiness.SheetPrinting.CompareProcListFiltered);
			return listProcedures;
		}

		///<summary>The ProcNums of the plan's procedures, as ControlChart takes them from the selected plans when "Treatment Plans" is checked: the
		///first half of the planned procedures, so others stay out.</summary>
		public static List<long> GetProcNumsTreatPlanHalf(long patNum) {
			List<long> listProcNums=Procedures.Refresh(patNum).FindAll(x => x.ProcStatus==ProcStat.TP).Select(x => x.ProcNum).ToList();
			return listProcNums.Take((listProcNums.Count+1)/2).ToList();
		}
		#endregion Per-scene data, read from the database each time

		#region Scenes
		public static List<ChartModuleScene> GetChartModuleScenes() {
			List<ChartModuleScene> listScenes=new List<ChartModuleScene>();
			listScenes.Add(new ChartModuleScene { Name="Paint_Today",PatNum=PatNumPaint,DateLimit=DateTime.Today,DateOrthoExam=DateTime.Today });
			listScenes.Add(new ChartModuleScene { Name="Paint_TreatPlans",PatNum=PatNumPaint,DateLimit=DateTime.Today,DateOrthoExam=DateTime.Today,
				FuncProcNumsTreatPlans=() => GetProcNumsTreatPlanHalf(PatNumPaint) });
			listScenes.Add(new ChartModuleScene { Name="Paint_PrintRecolor",PatNum=PatNumPaint,DateLimit=DateTime.Today,DateOrthoExam=DateTime.Today,
				IsPrintRecolor=true });
			foreach(DateTime date in GetSliderDates()) {
				listScenes.Add(new ChartModuleScene { Name="Slider_"+GetDateName(date),PatNum=PatNumSlider,DateLimit=date,DateOrthoExam=DateTime.Today });
			}
			listScenes.Add(new ChartModuleScene { Name="Slider_2022-02-10_TreatPlans",PatNum=PatNumSlider,DateLimit=new DateTime(2022,2,10),
				DateOrthoExam=DateTime.Today,FuncProcNumsTreatPlans=() => GetProcNumsTreatPlanHalf(PatNumSlider) });
			listScenes.Add(new ChartModuleScene { Name="Ortho_Exam1",PatNum=PatNumOrtho,DateLimit=DateTime.Today,DateOrthoExam=DateExam1 });
			listScenes.Add(new ChartModuleScene { Name="Ortho_Exam1_ShowHidden",PatNum=PatNumOrtho,DateLimit=DateTime.Today,DateOrthoExam=DateExam1,
				IsShowHidden=true });
			listScenes.Add(new ChartModuleScene { Name="Ortho_Exam2",PatNum=PatNumOrtho,DateLimit=DateTime.Today,DateOrthoExam=DateExam2 });
			listScenes.Add(new ChartModuleScene { Name="Ortho_GraphicsOff",PatNum=PatNumOrtho,DateLimit=DateTime.Today,DateOrthoExam=DateExam1,
				IsOrthoGraphics=false });
			listScenes.Add(new ChartModuleScene { Name="ProvColors_PrefOff",PatNum=PatNumProvColors,DateLimit=DateTime.Today,DateOrthoExam=DateTime.Today,
				User=new CallLogUser { ProvNum=ProvNum1 } });
			listScenes.Add(new ChartModuleScene { Name="ProvColors_UserIsProvider",PatNum=PatNumProvColors,DateLimit=DateTime.Today,
				DateOrthoExam=DateTime.Today,User=new CallLogUser { IsToothColorPref=true,ProvNum=ProvNum1 } });
			listScenes.Add(new ChartModuleScene { Name="ProvColors_UserIsProvider_PrintRecolor",PatNum=PatNumProvColors,DateLimit=DateTime.Today,
				DateOrthoExam=DateTime.Today,User=new CallLogUser { IsToothColorPref=true,ProvNum=ProvNum1 },IsPrintRecolor=true });
			listScenes.Add(new ChartModuleScene { Name="ProvColors_TodaysAppointment",PatNum=PatNumProvColors,DateLimit=DateTime.Today,
				DateOrthoExam=DateTime.Today,User=new CallLogUser { IsToothColorPref=true },HasTodaysAppointment=true });
			listScenes.Add(new ChartModuleScene { Name="Empty",PatNum=PatNumEmpty,DateLimit=DateTime.Today,DateOrthoExam=DateTime.Today });
			return listScenes;
		}

		public static List<TreatPlanScene> GetTreatPlanScenes() {
			List<TreatPlanScene> listScenes=new List<TreatPlanScene>();
			listScenes.Add(new TreatPlanScene { Name="Paint_ShowCompleted",PatNum=PatNumPaint,FuncProceduresFiltered=x => GetFilteredProcedures(x,true) });
			listScenes.Add(new TreatPlanScene { Name="Paint_PlanOnly",PatNum=PatNumPaint,FuncProceduresFiltered=x => GetFilteredProcedures(x,false) });
			listScenes.Add(new TreatPlanScene { Name="Paint_AllStatuses",PatNum=PatNumPaint,FuncProceduresFiltered=GetAllProcedures });
			listScenes.Add(new TreatPlanScene { Name="Slider_ShowCompleted",PatNum=PatNumSlider,FuncProceduresFiltered=x => GetFilteredProcedures(x,true) });
			listScenes.Add(new TreatPlanScene { Name="Ortho",PatNum=PatNumOrtho,FuncProceduresFiltered=x => GetFilteredProcedures(x,true) });
			listScenes.Add(new TreatPlanScene { Name="ProvColors_UserIsProvider",PatNum=PatNumProvColors,FuncProceduresFiltered=GetAllProcedures,
				User=new CallLogUser { IsToothColorPref=true,ProvNum=ProvNum1 } });
			listScenes.Add(new TreatPlanScene { Name="Empty",PatNum=PatNumEmpty,FuncProceduresFiltered=x => GetFilteredProcedures(x,true) });
			return listScenes;
		}

		public static List<SheetScene> GetSheetScenes() {
			List<SheetScene> listScenes=new List<SheetScene>();
			listScenes.Add(new SheetScene { Name="Paint_Sheet",PatNum=PatNumPaint,FuncTreatPlan=GetTreatPlanAllTP });
			listScenes.Add(new SheetScene { Name="Paint_Sheet_ShowCompleted",PatNum=PatNumPaint,ShowCompleted=true,FuncTreatPlan=GetTreatPlanAllTP });
			listScenes.Add(new SheetScene { Name="Paint_Automation_AllStatuses",PatNum=PatNumPaint,FuncProceduresOverride=GetAllProcedures });
			listScenes.Add(new SheetScene { Name="Paint_Dashboard",PatNum=PatNumPaint,ShowCompleted=true,FuncTreatPlan=GetTreatPlanAllTP,
				FuncProceduresOverride=x => GetFilteredProcedures(x,true),IsInPatientDashboard=true });
			listScenes.Add(new SheetScene { Name="Slider_Sheet_ShowCompleted",PatNum=PatNumSlider,ShowCompleted=true,FuncTreatPlan=GetTreatPlanAllTP });
			listScenes.Add(new SheetScene { Name="Ortho_Sheet",PatNum=PatNumOrtho,ShowCompleted=true,FuncTreatPlan=GetTreatPlanAllTP });
			listScenes.Add(new SheetScene { Name="ProvColors_Sheet_UserIsProvider",PatNum=PatNumProvColors,ShowCompleted=true,
				FuncTreatPlan=GetTreatPlanAllTP,User=new CallLogUser { IsToothColorPref=true,ProvNum=ProvNum1 } });
			listScenes.Add(new SheetScene { Name="ProvColors_Dashboard_UserIsProvider",PatNum=PatNumProvColors,ShowCompleted=true,
				FuncTreatPlan=GetTreatPlanAllTP,FuncProceduresOverride=x => GetFilteredProcedures(x,true),IsInPatientDashboard=true,
				User=new CallLogUser { IsToothColorPref=true,ProvNum=ProvNum1 } });
			listScenes.Add(new SheetScene { Name="ProvColors_Dashboard_TodaysAppointment",PatNum=PatNumProvColors,ShowCompleted=true,
				FuncTreatPlan=GetTreatPlanAllTP,FuncProceduresOverride=x => GetFilteredProcedures(x,true),IsInPatientDashboard=true,
				HasTodaysAppointment=true,User=new CallLogUser { IsToothColorPref=true } });
			listScenes.Add(new SheetScene { Name="NoPatient",PatNum=0 });
			listScenes.Add(new SheetScene { Name="Empty",PatNum=PatNumEmpty,ShowCompleted=true,FuncTreatPlan=GetTreatPlanAllTP });
			return listScenes;
		}

		public static List<BigChartScene> GetBigChartScenes() {
			List<BigChartScene> listScenes=new List<BigChartScene>();
			listScenes.Add(new BigChartScene { Name="Paint",PatNum=PatNumPaint });
			listScenes.Add(new BigChartScene { Name="Slider",PatNum=PatNumSlider });
			listScenes.Add(new BigChartScene { Name="Ortho",PatNum=PatNumOrtho });
			listScenes.Add(new BigChartScene { Name="ProvColors_UserIsProvider",PatNum=PatNumProvColors,User=new CallLogUser { IsToothColorPref=true,ProvNum=ProvNum1 } });
			listScenes.Add(new BigChartScene { Name="ProvColors_TodaysAppointment",PatNum=PatNumProvColors,HasTodaysAppointment=true,
				User=new CallLogUser { IsToothColorPref=true } });
			listScenes.Add(new BigChartScene { Name="Empty",PatNum=PatNumEmpty });
			return listScenes;
		}

		///<summary>Slider dates before, between and after the Slider patient's planned and completed dates, and today.</summary>
		public static List<DateTime> GetSliderDates() {
			return new List<DateTime> { new DateTime(2021,12,31),new DateTime(2022,1,10),new DateTime(2022,1,20),new DateTime(2022,2,10),
				new DateTime(2022,3,1),new DateTime(2022,3,15),DateTime.Today };
		}

		private static string GetDateName(DateTime date) {
			if(date==DateTime.Today) {
				return "Today";
			}
			return date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
		}
		#endregion Scenes
	}
}
