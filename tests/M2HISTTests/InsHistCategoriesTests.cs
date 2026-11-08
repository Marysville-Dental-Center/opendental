// Insurance History rows from code groups (Open Dental 26.1) without a database: which rows the code groups give and in what order, their labels,
// which procedures count for a row, the eligibility (271) mapping, and the Code Group Edit rules. Synthetic data only (made-up Z codes).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2HISTTests {
	[TestClass]
	public class InsHistCategoriesTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		private static CodeGroup Group(long codeGroupNum,string name,int itemOrder,string procCodes,bool showInHistory,string histProcCode="",
			bool isPerioFourQuads=false,EnumCodeGroupFixed codeGroupFixed=EnumCodeGroupFixed.None,bool isHidden=false)
		{
			return new CodeGroup { CodeGroupNum=codeGroupNum,GroupName=name,ItemOrder=itemOrder,ProcCodes=procCodes,ShowInHistory=showInHistory,
				HistProcCode=histProcCode,IsPerioFourQuads=isPerioFourQuads,CodeGroupFixed=codeGroupFixed,IsHidden=isHidden };
		}

		private static Procedure Proc(long procNum,long codeNum,DateTime date,string surf="",ProcStat procStat=ProcStat.EO) {
			return new Procedure { ProcNum=procNum,CodeNum=codeNum,ProcDate=date,Surf=surf,ProcStatus=procStat };
		}

		///<summary>CodeNum to ProcCode for the made-up codes of these tests.</summary>
		private static readonly Dictionary<long,string> _dictCodes=new Dictionary<long,string> {
			{ 1,"Z0120" },{ 2,"Z0150" },{ 3,"Z4341" },{ 4,"Z4342" },{ 5,"Z1206" },{ 6,"Z9999" },{ 7,"Z0272" },
		};

		private static string GetCode(long codeNum) {
			return _dictCodes.TryGetValue(codeNum,out string procCode) ? procCode : "";
		}

		[TestMethod]
		public void GetAll_OnlyShowInHistory_InCodeGroupOrder_HiddenIncluded() {
			List<CodeGroup> listCodeGroups=new List<CodeGroup> {
				Group(30,"Fluoride",3,"Z1206",showInHistory:true,histProcCode:"Z1206"),
				Group(10,"Exam",1,"Z0120,Z0150",showInHistory:true,histProcCode:"Z0120"),
				Group(20,"Crowns",2,"Z2740",showInHistory:false),
				Group(40,"Retired",4,"Z9999",showInHistory:true,histProcCode:"Z9999",isHidden:true),
			};
			List<InsHistCategory> listCategories=InsHistCategories.GetAll(listCodeGroups);
			CollectionAssert.AreEqual(new[] { "Exam","Fluoride","Retired" },listCategories.Select(x => x.Description).ToArray());
			Assert.IsTrue(listCategories.Last().IsHidden,"A retired group keeps its history row (the flags are independent).");
			Assert.AreEqual("10",listCategories[0].Key);
			Assert.AreEqual("Z0120",listCategories[0].HistProcCode);
		}

		[TestMethod]
		public void GetAll_PerioFourQuads_GivesFourRowsInOrderULURLLLR() {
			List<CodeGroup> listCodeGroups=new List<CodeGroup> {
				Group(5,"SRP",0,"Z4341-Z4342",showInHistory:true,histProcCode:"Z4341",isPerioFourQuads:true,codeGroupFixed:EnumCodeGroupFixed.SRP),
				Group(6,"Irrigation",1,"Z4921",showInHistory:true,histProcCode:"Z4921",isPerioFourQuads:true),
			};
			List<InsHistCategory> listCategories=InsHistCategories.GetAll(listCodeGroups);
			Assert.AreEqual(8,listCategories.Count);
			CollectionAssert.AreEqual(new[] { "Perio Scaling UL","Perio Scaling UR","Perio Scaling LL","Perio Scaling LR" },
				listCategories.Take(4).Select(x => x.Description).ToArray(),"The fixed SRP group keeps the published labels.");
			CollectionAssert.AreEqual(new[] { "Irrigation UL","Irrigation UR","Irrigation LL","Irrigation LR" },
				listCategories.Skip(4).Select(x => x.Description).ToArray(),"Another four-quadrant group shows its own name.");
			CollectionAssert.AreEqual(new[] { "5:UL","5:UR","5:LL","5:LR" },listCategories.Take(4).Select(x => x.Key).ToArray());
			Assert.IsTrue(listCategories.All(x => x.IsQuadrant));
			Assert.AreEqual("Perio Scaling LL",InsHistCategories.GetByKey("5:LL",listCategories).Description);
			Assert.IsNull(InsHistCategories.GetByKey("5",listCategories));
		}

		[TestMethod]
		public void IsMatch_RangesHistProcCodeAndQuadrant() {
			InsHistCategory category=new InsHistCategory { CodeGroupNum=1,ProcCodes="Z4341-Z4342,Z4910",HistProcCode="Z4999" };
			Assert.IsTrue(category.IsMatch("Z4342",""),"Inside a range.");
			Assert.IsTrue(category.IsMatch("z4910",""),"Codes compare without regard to case.");
			Assert.IsTrue(category.IsMatch("Z4999",""),"The History Proc Code counts even when the group's own list leaves it out.");
			Assert.IsFalse(category.IsMatch("Z4343",""));
			Assert.IsFalse(category.IsMatch("",""));
			InsHistCategory categoryUR=category.Copy();
			categoryUR.Quadrant="UR";
			Assert.IsTrue(categoryUR.IsMatch("Z4341","UR"));
			Assert.IsTrue(categoryUR.IsMatch("Z4341","ULUR"),"Surf contains the quadrant, as in 24.3.");
			Assert.IsFalse(categoryUR.IsMatch("Z4341","UL"));
			Assert.IsFalse(categoryUR.IsMatch("Z4341",null));
		}

		[TestMethod]
		public void GetProcs_NewestFirst_SameDayLaterInListFirst_LikeTheOldRows() {
			InsHistCategory category=new InsHistCategory { CodeGroupNum=1,ProcCodes="Z0120,Z0150",HistProcCode="Z0120" };
			List<Procedure> listProcedures=new List<Procedure> {
				Proc(1,1,new DateTime(2025,3,1)),
				Proc(2,2,new DateTime(2026,1,15),procStat:ProcStat.C),
				Proc(3,6,new DateTime(2026,6,1)),//Another code.
				Proc(4,1,new DateTime(2026,1,15)),
			};
			List<Procedure> listShown=InsHistCategories.GetProcs(listProcedures,category,GetCode);
			CollectionAssert.AreEqual(new long[] { 4,2,1 },listShown.Select(x => x.ProcNum).ToArray());
			Assert.AreEqual(4,InsHistCategories.GetMostRecentProc(listProcedures,category,GetCode).ProcNum);
			Assert.IsNull(InsHistCategories.GetMostRecentProc(new List<Procedure>(),category,GetCode));
		}

		[TestMethod]
		public void GetProcs_QuadrantRowsReadTheirOwnSurface() {
			List<InsHistCategory> listCategories=InsHistCategories.GetAll(new List<CodeGroup> {
				Group(5,"SRP",0,"Z4341,Z4342",showInHistory:true,histProcCode:"Z4341",isPerioFourQuads:true,codeGroupFixed:EnumCodeGroupFixed.SRP) });
			List<Procedure> listProcedures=new List<Procedure> {
				Proc(1,3,new DateTime(2026,2,1),surf:"UL"),
				Proc(2,4,new DateTime(2026,3,1),surf:"LR"),
				Proc(3,3,new DateTime(2026,4,1),surf:""),
			};
			Dictionary<string,long?> dictMostRecent=listCategories.ToDictionary(x => x.Quadrant,x => InsHistCategories.GetMostRecentProc(listProcedures,x,GetCode)?.ProcNum);
			Assert.AreEqual(1L,dictMostRecent["UL"]);
			Assert.IsNull(dictMostRecent["UR"]);
			Assert.IsNull(dictMostRecent["LL"]);
			Assert.AreEqual(2L,dictMostRecent["LR"]);
		}

		[TestMethod]
		public void GetForEligibility_FixedCategoryFirst_ThenHistProcCode_NeverAQuadrant() {
			List<InsHistCategory> listCategories=InsHistCategories.GetAll(new List<CodeGroup> {
				Group(1,"BW",0,"Z0272,Z0274",showInHistory:true,histProcCode:"Z0272",codeGroupFixed:EnumCodeGroupFixed.BW),
				Group(2,"Fluoride",1,"Z1206",showInHistory:true,histProcCode:"Z1206",codeGroupFixed:EnumCodeGroupFixed.Fluoride),
				Group(3,"SRP",2,"Z4341",showInHistory:true,histProcCode:"Z4341",isPerioFourQuads:true,codeGroupFixed:EnumCodeGroupFixed.SRP),
			});
			Assert.AreEqual("1",InsHistCategories.GetForEligibility(listCategories,EnumCodeGroupFixed.BW,"Z1206",out string reason).Key,
				"The category named by the response wins over its procedure code.");
			Assert.AreEqual("",reason);
			Assert.AreEqual("2",InsHistCategories.GetForEligibility(listCategories,EnumCodeGroupFixed.Exam,"z1206",out reason).Key,
				"No Exam row: matched on the History Proc Code.");
			Assert.IsNull(InsHistCategories.GetForEligibility(listCategories,EnumCodeGroupFixed.Exam,"Z0120",out reason,x => "Z0120,Z0150"));
			StringAssert.Contains(reason,"no code group");
			Assert.IsNull(InsHistCategories.GetForEligibility(listCategories,EnumCodeGroupFixed.SRP,"",out reason));
			StringAssert.Contains(reason,"quadrant");
			Assert.IsNull(InsHistCategories.GetForEligibility(listCategories,EnumCodeGroupFixed.None,"Z4341",out reason));
			StringAssert.Contains(reason,"quadrant");
			Assert.IsNull(InsHistCategories.GetForEligibility(new List<InsHistCategory>(),EnumCodeGroupFixed.BW,"Z0272",out reason,x => "Z0272,Z0274"));
		}

		[TestMethod]
		public void GetForEligibility_AHistoryOnlyGroup_ByTheCodesOfTheCategorysGroup() {
			//The Insurance History step's groups (owner decision of 2026-11-07) have no CodeGroupFixed: a response that names a category goes to the
			//history row whose History Proc Code is one of the codes of that category's code group.
			List<InsHistCategory> listCategories=InsHistCategories.GetAll(new List<CodeGroup> {
				Group(10,"Exam History",20,"Z0120,Z0150",showInHistory:true,histProcCode:"Z0120"),
				Group(11,"Bitewing History",21,"Z0272-Z0274",showInHistory:true,histProcCode:"Z0274"),
				Group(12,"Perio Scaling History",22,"Z4341,Z4342",showInHistory:true,histProcCode:"Z4341",isPerioFourQuads:true),
			});
			Func<EnumCodeGroupFixed,string> funcGetProcCodesFixed=x => {
				switch(x) {
					case EnumCodeGroupFixed.Exam: return "Z0120,Z0140,Z0150";
					case EnumCodeGroupFixed.BW: return "Z0270-Z0279";
					case EnumCodeGroupFixed.SRP: return "Z4341,Z4342";
					default: return "";
				}
			};
			Assert.AreEqual("10",InsHistCategories.GetForEligibility(listCategories,EnumCodeGroupFixed.Exam,"",out string reason,funcGetProcCodesFixed).Key);
			Assert.AreEqual("11",InsHistCategories.GetForEligibility(listCategories,EnumCodeGroupFixed.BW,"",out reason,funcGetProcCodesFixed).Key,
				"A range of the category's group counts.");
			Assert.AreEqual("10",InsHistCategories.GetForEligibility(listCategories,EnumCodeGroupFixed.BW,"Z0120",out reason,funcGetProcCodesFixed).Key,
				"The procedure code the response names comes first.");
			Assert.IsNull(InsHistCategories.GetForEligibility(listCategories,EnumCodeGroupFixed.SRP,"",out reason,funcGetProcCodesFixed));
			StringAssert.Contains(reason,"quadrant");
			Assert.IsNull(InsHistCategories.GetForEligibility(listCategories,EnumCodeGroupFixed.Prophy,"",out reason,funcGetProcCodesFixed));
			StringAssert.Contains(reason,"no code group");
		}

		[TestMethod]
		public void GetCodeGroupFixed_MapsTheTenPreferences() {
			Assert.AreEqual(EnumCodeGroupFixed.BW,InsHistCategories.GetCodeGroupFixed(PrefName.InsHistBWCodes));
			Assert.AreEqual(EnumCodeGroupFixed.PanoFMX,InsHistCategories.GetCodeGroupFixed(PrefName.InsHistPanoCodes));
			Assert.AreEqual(EnumCodeGroupFixed.Exam,InsHistCategories.GetCodeGroupFixed(PrefName.InsHistExamCodes));
			Assert.AreEqual(EnumCodeGroupFixed.Prophy,InsHistCategories.GetCodeGroupFixed(PrefName.InsHistProphyCodes));
			Assert.AreEqual(EnumCodeGroupFixed.Perio,InsHistCategories.GetCodeGroupFixed(PrefName.InsHistPerioMaintCodes));
			Assert.AreEqual(EnumCodeGroupFixed.FMDebride,InsHistCategories.GetCodeGroupFixed(PrefName.InsHistDebridementCodes));
			foreach(PrefName prefName in new[] { PrefName.InsHistPerioULCodes,PrefName.InsHistPerioURCodes,PrefName.InsHistPerioLLCodes,
				PrefName.InsHistPerioLRCodes })
			{
				Assert.AreEqual(EnumCodeGroupFixed.SRP,InsHistCategories.GetCodeGroupFixed(prefName));
			}
			Assert.AreEqual(EnumCodeGroupFixed.None,InsHistCategories.GetCodeGroupFixed(PrefName.InsBatchVerifyChangeInsHist));
		}

		#region Code Group Edit rules
		private static bool IsCode(string procCode) {
			return _dictCodes.Values.Contains(procCode);
		}

		[TestMethod]
		public void GetError_HistProcCodeRequiredValidAndSingle() {
			Assert.AreEqual("",CodeGroupHistoryOptions.GetError(false,"",false,IsCode));
			Assert.AreEqual("",CodeGroupHistoryOptions.GetError(false,"Z0120",false,IsCode),"A code left in the box is cleared on save, not refused.");
			StringAssert.Contains(CodeGroupHistoryOptions.GetError(false,"",true,IsCode),"Perio Four Quadrants requires Show In History");
			StringAssert.Contains(CodeGroupHistoryOptions.GetError(true," ",false,IsCode),"required");
			StringAssert.Contains(CodeGroupHistoryOptions.GetError(true,"Z0001",false,IsCode),"not a valid procedure code");
			StringAssert.Contains(CodeGroupHistoryOptions.GetError(true,"Z0120,Z0150",false,IsCode),"single");
			StringAssert.Contains(CodeGroupHistoryOptions.GetError(true,"Z4341-Z4342",false,IsCode),"single");
			Assert.AreEqual("",CodeGroupHistoryOptions.GetError(true," Z0120 ",true,IsCode));
		}

		[TestMethod]
		public void GetWarning_WhenTheHistProcCodeIsNotOneOfTheGroupsCodes() {
			Assert.AreEqual("",CodeGroupHistoryOptions.GetWarning(true,"Z4342","Z4341-Z4343"));
			Assert.AreEqual("",CodeGroupHistoryOptions.GetWarning(false,"Z0120","Z4341"));
			StringAssert.Contains(CodeGroupHistoryOptions.GetWarning(true,"Z0120","Z4341"),"not in this group's Proc Codes");
		}

		[TestMethod]
		public void Apply_ClearsTheOptionsWithoutShowInHistory() {
			CodeGroup codeGroup=Group(1,"Exam",0,"Z0120",showInHistory:true,histProcCode:"Z0120",isPerioFourQuads:true);
			CodeGroupHistoryOptions.Apply(codeGroup,false,"Z0120",true);
			Assert.IsFalse(codeGroup.ShowInHistory);
			Assert.AreEqual("",codeGroup.HistProcCode);
			Assert.IsFalse(codeGroup.IsPerioFourQuads);
			CodeGroupHistoryOptions.Apply(codeGroup,true," Z0150 ",true);
			Assert.IsTrue(codeGroup.ShowInHistory);
			Assert.AreEqual("Z0150",codeGroup.HistProcCode);
			Assert.IsTrue(codeGroup.IsPerioFourQuads);
		}

		[TestMethod]
		public void GetLogChanges_NamesEachChangedOption() {
			CodeGroup codeGroupOld=Group(1,"Exam",0,"Z0120",showInHistory:false);
			CodeGroup codeGroup=Group(1,"Exam",0,"Z0120",showInHistory:true,histProcCode:"Z0120",isPerioFourQuads:true);
			List<string> listChanges=CodeGroupHistoryOptions.GetLogChanges(codeGroupOld,codeGroup);
			CollectionAssert.AreEqual(new[] { "shown in history","history proc code from '' to 'Z0120'","perio four quadrants on" },listChanges);
			Assert.AreEqual(0,CodeGroupHistoryOptions.GetLogChanges(codeGroup,codeGroup.Copy()).Count);
			CollectionAssert.AreEqual(new[] { "removed from history","history proc code from 'Z0120' to ''","perio four quadrants off" },
				CodeGroupHistoryOptions.GetLogChanges(codeGroup,codeGroupOld));
		}
		#endregion Code Group Edit rules
	}
}
