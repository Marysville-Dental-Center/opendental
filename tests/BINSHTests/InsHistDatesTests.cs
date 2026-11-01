// Rules of the two-date Insurance History window (Open Dental 25.1) with explicit edit and remove (25.2), without a database: which procedures
// fill the two boxes, what saving a row does, the equal-date check and the permissions. The rows are code groups from Open Dental 26.1 (M2-HIST),
// built here in memory. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BINSHTests {
	[TestClass]
	public class InsHistDatesTests {
		private const long CODE_PROPHY=9001;
		private const long CODE_PROPHY_ALT=9002;
		private const long CODE_SRP=9003;
		private static long _procNum=100;
		///<summary>The ProcCode of each CodeNum above, in place of the procedure code cache.</summary>
		private static readonly Dictionary<long,string> _dictProcCodes=new Dictionary<long,string> {
			{ CODE_PROPHY,"Z1110" },{ CODE_PROPHY_ALT,"Z1120" },{ CODE_SRP,"Z4341" } };
		private static readonly Func<long,string> _funcGetProcCode=x => _dictProcCodes.TryGetValue(x,out string procCode) ? procCode : "";
		///<summary>A Prophy row with both prophy codes.</summary>
		private static readonly InsHistCategory _categoryProphy=Row("Prophy","Z1110,Z1120");
		private static readonly InsHistCategory _categoryProphyOneCode=Row("Prophy","Z1110");
		private static readonly InsHistCategory _categoryBitewing=Row("Bitewing","Z0272");

		///<summary>A row of a code group with these codes (the first is its History Proc Code), or one quadrant of a Perio Four Quadrants group.
		///</summary>
		private static InsHistCategory Row(string description,string procCodes,string quadrant="") {
			return new InsHistCategory { CodeGroupNum=1,Description=description,ProcCodes=procCodes,HistProcCode=procCodes.Split(',')[0],
				Quadrant=quadrant };
		}

		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		private static Procedure Proc(long codeNum,ProcStat procStat,DateTime date,string surf="") {
			return new Procedure { ProcNum=_procNum++,CodeNum=codeNum,ProcStatus=procStat,ProcDate=date,Surf=surf,DateEntryC=date };
		}

		private static InsHistBox Box(Procedure procedure,DateTime dateEntered) {
			return new InsHistBox { ProcedureLoaded=procedure,DateEntered=dateEntered };
		}

		[TestMethod]
		public void GetMostRecentProcs_TwoMostRecentOfTheCategoryNewestFirst() {
			List<Procedure> listProcedures=new List<Procedure> {
				Proc(CODE_PROPHY,ProcStat.EO,new DateTime(2025,3,1)),
				Proc(CODE_PROPHY_ALT,ProcStat.C,new DateTime(2026,2,10)),
				Proc(CODE_PROPHY,ProcStat.EO,new DateTime(2025,9,15)),
				Proc(CODE_SRP,ProcStat.EO,new DateTime(2026,5,1),"UR"),//Another category.
			};
			List<Procedure> listMostRecent=InsHistDates.GetMostRecentProcs(listProcedures,_categoryProphy,_funcGetProcCode);
			Assert.AreEqual(2,listMostRecent.Count);
			Assert.AreEqual(new DateTime(2026,2,10),listMostRecent[0].ProcDate,"Most Recent comes first.");
			Assert.AreEqual(new DateTime(2025,9,15),listMostRecent[1].ProcDate,"Previous: the third date is not shown.");
		}

		[TestMethod]
		public void GetMostRecentProcs_PerioQuadrantsUseTheSurface() {
			List<Procedure> listProcedures=new List<Procedure> {
				Proc(CODE_SRP,ProcStat.EO,new DateTime(2026,5,1),"UR"),
				Proc(CODE_SRP,ProcStat.EO,new DateTime(2026,5,2),"UL"),
				Proc(CODE_SRP,ProcStat.C,new DateTime(2025,5,1),"UR"),
			};
			List<Procedure> listUR=InsHistDates.GetMostRecentProcs(listProcedures,Row("SRP","Z4341","UR"),_funcGetProcCode);
			CollectionAssert.AreEqual(new[] { new DateTime(2026,5,1),new DateTime(2025,5,1) },listUR.Select(x => x.ProcDate).ToArray());
			List<Procedure> listUL=InsHistDates.GetMostRecentProcs(listProcedures,Row("SRP","Z4341","UL"),_funcGetProcCode);
			Assert.AreEqual(1,listUL.Count);
			Assert.AreEqual(0,InsHistDates.GetMostRecentProcs(listProcedures,Row("SRP","Z4341","LL"),_funcGetProcCode).Count);
		}

		[TestMethod]
		public void GetMostRecentProcs_CompletedBeforeExistingOtherOnTheSameDay() {
			DateTime date=new DateTime(2026,1,5);
			List<Procedure> listProcedures=new List<Procedure> { Proc(CODE_PROPHY,ProcStat.EO,date),Proc(CODE_PROPHY,ProcStat.C,date) };
			List<Procedure> listMostRecent=InsHistDates.GetMostRecentProcs(listProcedures,_categoryProphyOneCode,_funcGetProcCode);
			Assert.AreEqual(ProcStat.C,listMostRecent[0].ProcStatus);
		}

		[TestMethod]
		public void GetDatesText_BothDatesOrBlank() {
			List<Procedure> listProcedures=new List<Procedure> {
				Proc(CODE_PROPHY,ProcStat.EO,new DateTime(2025,9,15)),
				Proc(CODE_PROPHY,ProcStat.C,new DateTime(2026,3,1)),
			};
			Assert.AreEqual("3/1/2026, 9/15/2025",InsHistDates.GetDatesText(listProcedures,_categoryProphyOneCode,_funcGetProcCode));
			Assert.AreEqual("9/15/2025",InsHistDates.GetDatesText(listProcedures.Take(1).ToList(),_categoryProphyOneCode,_funcGetProcCode));
			Assert.AreEqual("",InsHistDates.GetDatesText(new List<Procedure>(),_categoryProphyOneCode,_funcGetProcCode));
		}

		[TestMethod]
		public void GetActions_NothingChangedDoesNothing() {
			Procedure procedureEo=Proc(CODE_PROPHY,ProcStat.EO,new DateTime(2026,3,1));
			Procedure procedureC=Proc(CODE_PROPHY,ProcStat.C,new DateTime(2025,9,1));
			List<InsHistAction> listActions=InsHistDates.GetActions(_categoryProphy,
				new List<InsHistBox> { Box(procedureEo,procedureEo.ProcDate),Box(procedureC,DateTime.MinValue) },out string error);
			Assert.AreEqual("",error);
			Assert.AreEqual(0,listActions.Count,"A completed date is read-only, so its box never counts as changed.");
		}

		[TestMethod]
		public void GetActions_DateInEmptyBoxInsertsOne() {
			List<InsHistAction> listActions=InsHistDates.GetActions(_categoryProphy,
				new List<InsHistBox> { Box(null,new DateTime(2026,4,2)),Box(null,new DateTime(2025,10,3)) },out string error);
			Assert.AreEqual("",error);
			Assert.AreEqual(2,listActions.Count,"Both empty boxes filled: two new EO procedures.");
			Assert.IsTrue(listActions.All(x => x.ActionType==InsHistActionType.Insert && x.Procedure==null));
			CollectionAssert.AreEquivalent(new[] { new DateTime(2026,4,2),new DateTime(2025,10,3) },listActions.Select(x => x.DateNew).ToArray());
		}

		[TestMethod]
		public void GetActions_NewDateForExistingOtherChangesItsDate() {
			Procedure procedureEo=Proc(CODE_PROPHY,ProcStat.EO,new DateTime(2026,3,1));
			List<InsHistAction> listActions=InsHistDates.GetActions(_categoryProphy,
				new List<InsHistBox> { Box(procedureEo,new DateTime(2026,3,8)),Box(null,DateTime.MinValue) },out string error);
			Assert.AreEqual("",error);
			Assert.AreEqual(1,listActions.Count);
			Assert.AreEqual(InsHistActionType.ChangeDate,listActions[0].ActionType);
			Assert.AreSame(procedureEo,listActions[0].Procedure);
			Assert.AreEqual(new DateTime(2026,3,8),listActions[0].DateNew);
		}

		[TestMethod]
		public void GetActions_ClearedExistingOtherIsRemoved() {
			Procedure procedureEo=Proc(CODE_PROPHY,ProcStat.EO,new DateTime(2025,9,1));
			Procedure procedureC=Proc(CODE_PROPHY,ProcStat.C,new DateTime(2026,3,1));
			List<InsHistAction> listActions=InsHistDates.GetActions(_categoryProphy,
				new List<InsHistBox> { Box(procedureC,DateTime.MinValue),Box(procedureEo,DateTime.MinValue) },out string error);
			Assert.AreEqual("",error);
			Assert.AreEqual(1,listActions.Count,"Only the EO procedure is removed; the completed one is never touched.");
			Assert.AreEqual(InsHistActionType.Remove,listActions[0].ActionType);
			Assert.AreSame(procedureEo,listActions[0].Procedure);
		}

		[TestMethod]
		public void GetActions_TwoDatesOnOneDayAreRefused() {
			Procedure procedureC=Proc(CODE_PROPHY,ProcStat.C,new DateTime(2026,3,1));
			List<InsHistAction> listActions=InsHistDates.GetActions(_categoryProphy,
				new List<InsHistBox> { Box(procedureC,DateTime.MinValue),Box(null,new DateTime(2026,3,1)) },out string error);
			Assert.AreEqual(0,listActions.Count);
			StringAssert.Contains(error,"Prophy");
			StringAssert.Contains(error,"can't be the same");
			listActions=InsHistDates.GetActions(_categoryBitewing,
				new List<InsHistBox> { Box(null,new DateTime(2026,3,1)),Box(null,new DateTime(2026,3,1)) },out error);
			Assert.AreEqual(0,listActions.Count);
			StringAssert.Contains(error,"Bitewing");
		}

		[TestMethod]
		public void GetActions_UntouchedHistoryOnOneDayDoesNotBlock() {
			DateTime date=new DateTime(2026,1,5);
			Procedure procedureC=Proc(CODE_PROPHY,ProcStat.C,date);
			Procedure procedureEo=Proc(CODE_PROPHY,ProcStat.EO,date);
			InsHistDates.GetActions(_categoryProphy,new List<InsHistBox> { Box(procedureC,DateTime.MinValue),Box(procedureEo,date) },
				out string error);
			Assert.AreEqual("",error,"Data already on file is not checked unless the user changes the category.");
		}

		[TestMethod]
		public void GetActions_OrderOfTheTwoBoxesDoesNotMatter() {
			//A newer date typed into the Previous box, below an older completed date, is accepted; the window shows them re-sorted after saving.
			Procedure procedureC=Proc(CODE_PROPHY,ProcStat.C,new DateTime(2025,9,1));
			List<InsHistAction> listActions=InsHistDates.GetActions(_categoryProphy,
				new List<InsHistBox> { Box(procedureC,DateTime.MinValue),Box(null,new DateTime(2026,4,1)) },out string error);
			Assert.AreEqual("",error);
			Assert.AreEqual(1,listActions.Count);
			Assert.AreEqual(InsHistActionType.Insert,listActions[0].ActionType);
		}

		[TestMethod]
		public void InsHistBox_ProcedureWithoutValidDateLoadsBlank() {
			Procedure procedureEo=Proc(CODE_PROPHY,ProcStat.EO,DateTime.MinValue);
			InsHistBox insHistBox=Box(procedureEo,DateTime.MinValue);
			Assert.AreEqual(DateTime.MinValue,insHistBox.GetDateLoaded());
			Assert.IsFalse(insHistBox.IsChanged(),"Left blank, it is not removed (24.3 showed it as No History and skipped it).");
			insHistBox.DateEntered=new DateTime(2026,2,2);
			Assert.IsTrue(insHistBox.IsChanged());
		}

		[TestMethod]
		public void TryGetPermission_EditNeedsProcExistingEditAndRemoveNeedsProcDelete() {
			DateTime dateEntry=new DateTime(2026,1,9);
			Procedure procedureEo=Proc(CODE_PROPHY,ProcStat.EO,new DateTime(2025,6,1));
			procedureEo.DateEntryC=dateEntry;
			InsHistAction insHistActionEdit=new InsHistAction { ActionType=InsHistActionType.ChangeDate,Procedure=procedureEo,DateNew=new DateTime(2025,7,1) };
			Assert.IsTrue(InsHistDates.TryGetPermission(insHistActionEdit,out EnumPermType enumPermType,out DateTime date));
			Assert.AreEqual(EnumPermType.ProcExistingEdit,enumPermType);
			Assert.AreEqual(dateEntry,date,"EO procedures are checked with their entry date, like the Chart.");
			InsHistAction insHistActionRemove=new InsHistAction { ActionType=InsHistActionType.Remove,Procedure=procedureEo };
			Assert.IsTrue(InsHistDates.TryGetPermission(insHistActionRemove,out enumPermType,out date));
			Assert.AreEqual(EnumPermType.ProcDelete,enumPermType);
			Assert.AreEqual(dateEntry,date);
			InsHistAction insHistActionInsert=new InsHistAction { ActionType=InsHistActionType.Insert,DateNew=new DateTime(2025,7,1) };
			Assert.IsFalse(InsHistDates.TryGetPermission(insHistActionInsert,out _,out _),"A new date needs no permission, as in 24.3.");
		}

		///<summary>The rows of the 24.3 window come back from the code groups the M2-HIST migration marks: one per group, and four for the SRP group,
		///whose quadrant rows keep the 24.3 labels. The actions carry their row, whose label the messages and the log use.</summary>
		[TestMethod]
		public void Rows_TheCodeGroupsMarkedShowInHistory() {
			List<CodeGroup> listCodeGroups=new List<CodeGroup> {
				new CodeGroup { CodeGroupNum=1,GroupName="Prophy",ProcCodes="Z1110",ShowInHistory=true,HistProcCode="Z1110",ItemOrder=0 },
				new CodeGroup { CodeGroupNum=2,GroupName="SRP",ProcCodes="Z4341",ShowInHistory=true,HistProcCode="Z4341",IsPerioFourQuads=true,ItemOrder=1,
					CodeGroupFixed=EnumCodeGroupFixed.SRP },
				new CodeGroup { CodeGroupNum=3,GroupName="Crowns",ProcCodes="Z2740",ItemOrder=2 },
			};
			List<InsHistCategory> listInsHistCategories=InsHistCategories.GetAll(listCodeGroups);
			CollectionAssert.AreEqual(new[] { "Prophy","Perio Scaling UL","Perio Scaling UR","Perio Scaling LL","Perio Scaling LR" },
				listInsHistCategories.Select(x => x.Description).ToArray());
			List<InsHistAction> listActions=InsHistDates.GetActions(listInsHistCategories[4],new List<InsHistBox> { Box(null,new DateTime(2026,4,2)) },
				out string error);
			Assert.AreEqual("",error);
			Assert.AreSame(listInsHistCategories[4],listActions.Single().InsHistCategory);
		}
	}
}
