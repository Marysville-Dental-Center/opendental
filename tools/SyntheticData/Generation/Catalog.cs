// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;

namespace SyntheticData.Generation {
	///<summary>A procedure code of the synthetic practice. Codes are invented "Z" codes with plain descriptions written for this tool;
	///no ADA CDT code text is used. The numeric part loosely follows the usual category blocks so coverage spans and code-group ranges
	///look like a real office's.</summary>
	public class ProcCodeDef {
		public string Code;
		public string Descript;
		public string Abbr;
		public int TreatArea;
		///<summary>Time pattern in 5-minute steps: '/' assistant or hygienist time, 'X' provider time.</summary>
		public string ProcTime;
		public decimal Ucr;
		public string Category;
		public string CovCat;
		public bool IsHygiene;
		public int PaintType;
		public bool IsProsth;
		public bool NoBillIns;
		public long CodeNum;
		public long CategoryDefNum;
	}

	public class CovCatDef {
		public string Description;
		public int DefaultPercent;
		public int EbenefitCat;
		public string FromCode;
		public string ToCode;
		public long CovCatNum;
	}

	public class CodeGroupDef {
		public string GroupName;
		public string ProcCodes;
		public int Fixed;
		public bool ShowInAgeLimit;
		public bool ShowInFrequency;
		public bool ShowInOther;
		public bool IsHidden;
		public long CodeGroupNum;
	}

	public static class Catalog {
		public static List<ProcCodeDef> CreateProcCodes() {
			List<ProcCodeDef> list=new List<ProcCodeDef>();
			void Add(string code,string descript,string abbr,int area,string time,decimal ucr,string category,string covCat,
				bool hyg=false,int paint=OdValues.PaintNone,bool prosth=false,bool noBillIns=false)
			{
				list.Add(new ProcCodeDef { Code=code,Descript=descript,Abbr=abbr,TreatArea=area,ProcTime=time,Ucr=ucr,Category=category,CovCat=covCat,
					IsHygiene=hyg,PaintType=paint,IsProsth=prosth,NoBillIns=noBillIns });
			}
			const string exams="Exams & X-Rays", clean="Cleanings", fill="Fillings", crown="Crowns & Bridges", endo="Endodontics",
				perio="Periodontics", surg="Oral Surgery", remov="Dentures", impl="Implants", ortho="Orthodontics", misc="Miscellaneous";
			Add("Z0120","Recall exam","RecExam",OdValues.AreaMouth,"/X/",62,exams,"Diagnostic");
			Add("Z0140","Problem-focused exam","ProbExam",OdValues.AreaMouth,"/XX/",88,exams,"Diagnostic");
			Add("Z0150","New patient full exam","NPExam",OdValues.AreaMouth,"/XX/",112,exams,"Diagnostic");
			Add("Z0180","Gum health exam","PerioEx",OdValues.AreaMouth,"/XX/",118,exams,"Diagnostic");
			Add("Z0210","Full set of x-rays","FullXR",OdValues.AreaMouth,"/XX/",152,exams,"X-Ray");
			Add("Z0220","Single tooth x-ray","1PA",OdValues.AreaTooth,"/X/",36,exams,"X-Ray");
			Add("Z0230","Extra single tooth x-ray","PA+",OdValues.AreaTooth,"/X/",29,exams,"X-Ray");
			Add("Z0272","Two bite-wing x-rays","2BW",OdValues.AreaMouth,"/X/",56,exams,"X-Ray");
			Add("Z0274","Four bite-wing x-rays","4BW",OdValues.AreaMouth,"/X/",77,exams,"X-Ray");
			Add("Z0330","Panoramic x-ray","Pan",OdValues.AreaMouth,"/X/",128,exams,"X-Ray");
			Add("Z1110","Adult cleaning","AdClean",OdValues.AreaMouth,"/XXXXXXX/",108,clean,"Preventive",hyg:true);
			Add("Z1120","Child cleaning","ChClean",OdValues.AreaMouth,"/XXXXX/",78,clean,"Preventive",hyg:true);
			Add("Z1206","Fluoride varnish","FlVarn",OdValues.AreaMouth,"X",42,clean,"Preventive",hyg:true);
			Add("Z1351","Sealant on one tooth","Seal",OdValues.AreaTooth,"/X/",52,clean,"Preventive",hyg:true,paint:OdValues.PaintSealant);
			Add("Z2140","Silver filling, one surface","Ag1",OdValues.AreaSurf,"/XX/",142,fill,"Restorative",paint:OdValues.PaintFillingDark);
			Add("Z2150","Silver filling, two surfaces","Ag2",OdValues.AreaSurf,"/XXX/",178,fill,"Restorative",paint:OdValues.PaintFillingDark);
			Add("Z2330","White filling, front tooth, one surface","WhF1",OdValues.AreaSurf,"/XX/",162,fill,"Restorative",paint:OdValues.PaintFillingLight);
			Add("Z2331","White filling, front tooth, two surfaces","WhF2",OdValues.AreaSurf,"/XXX/",198,fill,"Restorative",paint:OdValues.PaintFillingLight);
			Add("Z2391","White filling, back tooth, one surface","WhB1",OdValues.AreaSurf,"/XX/",188,fill,"Restorative",paint:OdValues.PaintFillingLight);
			Add("Z2392","White filling, back tooth, two surfaces","WhB2",OdValues.AreaSurf,"/XXX/",238,fill,"Restorative",paint:OdValues.PaintFillingLight);
			Add("Z2393","White filling, back tooth, three surfaces","WhB3",OdValues.AreaSurf,"/XXXX/",289,fill,"Restorative",paint:OdValues.PaintFillingLight);
			Add("Z2394","White filling, back tooth, four or more surfaces","WhB4",OdValues.AreaSurf,"/XXXX/",334,fill,"Restorative",paint:OdValues.PaintFillingLight);
			Add("Z2740","All-ceramic crown","CrnCer",OdValues.AreaTooth,"/XXXXXXX/",1260,crown,"Crowns",paint:OdValues.PaintCrownLight,prosth:true);
			Add("Z2750","Metal-ceramic crown","CrnMC",OdValues.AreaTooth,"/XXXXXXX/",1160,crown,"Crowns",paint:OdValues.PaintCrownLight,prosth:true);
			Add("Z2920","Re-cement a crown","Recem",OdValues.AreaTooth,"/X/",122,crown,"Restorative");
			Add("Z2950","Core build-up","CoreBU",OdValues.AreaTooth,"/XX/",278,crown,"Crowns",paint:OdValues.PaintPostBU);
			Add("Z3220","Pulp removal, baby or young tooth","Pulpot",OdValues.AreaTooth,"/XX/",224,endo,"Endodontics");
			Add("Z3310","Root canal, front tooth","RCTAnt",OdValues.AreaTooth,"/XXXXXX/",858,endo,"Endodontics",paint:OdValues.PaintRct);
			Add("Z3320","Root canal, premolar","RCTPm",OdValues.AreaTooth,"/XXXXXXX/",982,endo,"Endodontics",paint:OdValues.PaintRct);
			Add("Z3330","Root canal, molar","RCTMol",OdValues.AreaTooth,"/XXXXXXXXX/",1164,endo,"Endodontics",paint:OdValues.PaintRct);
			Add("Z4341","Deep cleaning, 4 or more teeth in a quadrant","DCl4+",OdValues.AreaQuad,"/XXXXXX/",248,perio,"Periodontics",hyg:true);
			Add("Z4342","Deep cleaning, 1 to 3 teeth in a quadrant","DCl1-3",OdValues.AreaQuad,"/XXXX/",168,perio,"Periodontics",hyg:true);
			Add("Z4355","Full-mouth debris removal","Debr",OdValues.AreaMouth,"/XXXXXX/",162,perio,"Periodontics",hyg:true);
			Add("Z4910","Gum maintenance cleaning","PerMnt",OdValues.AreaMouth,"/XXXXXXXX/",152,perio,"Periodontics",hyg:true);
			Add("Z5110","Full upper denture","DentU",OdValues.AreaArch,"/XXXX/",1920,remov,"Prosthodontics",paint:OdValues.PaintDentureLight,prosth:true);
			Add("Z5120","Full lower denture","DentL",OdValues.AreaArch,"/XXXX/",1920,remov,"Prosthodontics",paint:OdValues.PaintDentureLight,prosth:true);
			Add("Z5213","Upper partial denture","PartU",OdValues.AreaArch,"/XXXX/",1675,remov,"Prosthodontics",paint:OdValues.PaintDentureLight,prosth:true);
			Add("Z6010","Implant post placement","ImpPost",OdValues.AreaTooth,"/XXXXXXXXX/",2140,impl,"Prosthodontics",paint:OdValues.PaintImplant);
			Add("Z6065","Crown on an implant","ImpCrn",OdValues.AreaTooth,"/XXXXXX/",1575,impl,"Prosthodontics",paint:OdValues.PaintCrownLight,prosth:true);
			Add("Z7140","Simple tooth removal","Ext",OdValues.AreaTooth,"/XX/",188,surg,"Oral Surgery",paint:OdValues.PaintExtraction);
			Add("Z7210","Surgical tooth removal","SurgExt",OdValues.AreaTooth,"/XXXX/",328,surg,"Oral Surgery",paint:OdValues.PaintExtraction);
			Add("Z8080","Full orthodontic treatment, teen","OrthoT",OdValues.AreaMouth,"/XXX/",5600,ortho,"Orthodontics");
			Add("Z9110","Emergency pain relief","Pallia",OdValues.AreaMouth,"/XX/",96,misc,"Adjunctive");
			Add("Z9230","Laughing gas","N2O",OdValues.AreaMouth,"/",66,misc,"Adjunctive");
			Add("Z9944","Night guard","NGuard",OdValues.AreaArch,"/XXX/",498,misc,"Adjunctive");
			Add("Z9972","Take-home whitening","Whiten",OdValues.AreaArch,"/XX/",355,misc,"Adjunctive",noBillIns:true);
			return list;
		}

		public static List<CovCatDef> CreateCovCats() {
			return new List<CovCatDef> {
				new CovCatDef { Description="Diagnostic",DefaultPercent=100,EbenefitCat=OdValues.EbDiagnostic,FromCode="Z0100",ToCode="Z0199" },
				new CovCatDef { Description="X-Ray",DefaultPercent=100,EbenefitCat=OdValues.EbDiagnosticXRay,FromCode="Z0200",ToCode="Z0399" },
				new CovCatDef { Description="Preventive",DefaultPercent=100,EbenefitCat=OdValues.EbRoutinePreventive,FromCode="Z1000",ToCode="Z1999" },
				new CovCatDef { Description="Restorative",DefaultPercent=80,EbenefitCat=OdValues.EbRestorative,FromCode="Z2000",ToCode="Z2699" },
				new CovCatDef { Description="Endodontics",DefaultPercent=80,EbenefitCat=OdValues.EbEndodontics,FromCode="Z3000",ToCode="Z3999" },
				new CovCatDef { Description="Periodontics",DefaultPercent=80,EbenefitCat=OdValues.EbPeriodontics,FromCode="Z4000",ToCode="Z4999" },
				new CovCatDef { Description="Crowns",DefaultPercent=50,EbenefitCat=OdValues.EbCrowns,FromCode="Z2700",ToCode="Z2999" },
				new CovCatDef { Description="Prosthodontics",DefaultPercent=50,EbenefitCat=OdValues.EbProsthodontics,FromCode="Z5000",ToCode="Z6999" },
				new CovCatDef { Description="Oral Surgery",DefaultPercent=80,EbenefitCat=OdValues.EbOralSurgery,FromCode="Z7000",ToCode="Z7999" },
				new CovCatDef { Description="Orthodontics",DefaultPercent=50,EbenefitCat=OdValues.EbOrthodontics,FromCode="Z8000",ToCode="Z8999" },
				new CovCatDef { Description="Adjunctive",DefaultPercent=50,EbenefitCat=OdValues.EbAdjunctive,FromCode="Z9000",ToCode="Z9999" },
			};
		}

		///<summary>Code groups (24.4 ranges, 25.1 hiding and frequency flag, 25.3 Other Benefits flag).</summary>
		public static List<CodeGroupDef> CreateCodeGroups() {
			return new List<CodeGroupDef> {
				new CodeGroupDef { GroupName="BWs",ProcCodes="Z0272,Z0274",Fixed=OdValues.CgBW,ShowInFrequency=true },
				new CodeGroupDef { GroupName="Pano/FMX",ProcCodes="Z0210,Z0330",Fixed=OdValues.CgPanoFMX,ShowInFrequency=true },
				new CodeGroupDef { GroupName="Exams",ProcCodes="Z0120-Z0150,Z0180",Fixed=OdValues.CgExam,ShowInFrequency=true },
				new CodeGroupDef { GroupName="Perio Maint",ProcCodes="Z4910",Fixed=OdValues.CgPerio,ShowInFrequency=true },
				new CodeGroupDef { GroupName="Prophy",ProcCodes="Z1110-Z1120",Fixed=OdValues.CgProphy,ShowInFrequency=true },
				new CodeGroupDef { GroupName="SRP",ProcCodes="Z4341-Z4342",Fixed=OdValues.CgSRP,ShowInFrequency=true },
				new CodeGroupDef { GroupName="FM Debride",ProcCodes="Z4355",Fixed=OdValues.CgFMDebride,ShowInFrequency=true },
				new CodeGroupDef { GroupName="Fluoride",ProcCodes="Z1206",Fixed=OdValues.CgFluoride,ShowInFrequency=true,ShowInAgeLimit=true },
				new CodeGroupDef { GroupName="Sealant",ProcCodes="Z1351",Fixed=OdValues.CgSealant,ShowInAgeLimit=true },
				new CodeGroupDef { GroupName="Crowns",ProcCodes="Z2740-Z2750,Z6065",ShowInFrequency=true,ShowInOther=true },
				new CodeGroupDef { GroupName="Dentures",ProcCodes="Z5110-Z5213",ShowInOther=true },
				new CodeGroupDef { GroupName="Implants",ProcCodes="Z6010,Z6065",ShowInOther=true },
				new CodeGroupDef { GroupName="Night Guards",ProcCodes="Z9944",ShowInOther=true,IsHidden=true },
			};
		}

		public static CovCatDef CovCatFor(List<CovCatDef> listCovCats,string code) {
			return listCovCats.FirstOrDefault(x => string.CompareOrdinal(code,x.FromCode)>=0 && string.CompareOrdinal(code,x.ToCode)<=0);
		}
	}
}
