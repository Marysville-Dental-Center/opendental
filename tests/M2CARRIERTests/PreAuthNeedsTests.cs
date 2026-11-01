// Which treatment-planned procedures need a preauthorization (carrier.PreAuthCodes, Open Dental 26.1): code matching with the shared range parser
// and the rules of CarrierPreAuths.GetNeeds, on in-memory synthetic objects. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2CARRIERTests {
	[TestClass]
	public class PreAuthNeedsTests {
		private const long PAT_NUM=10;
		private const long CARRIER_PRI=100;
		private const long CARRIER_SEC=200;
		private const long CARRIER_NONE=300;
		private Dictionary<long,Carrier> _dictCarriers;
		private Dictionary<long,string> _dictProcCodes;
		private List<PatPlan> _listPatPlans;
		private List<InsSub> _listInsSubs;
		private List<InsPlan> _listInsPlans;

		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			_dictCarriers=new Dictionary<long,Carrier> {
				{ CARRIER_PRI,new Carrier { CarrierNum=CARRIER_PRI,CarrierName="Synthetic Primary",PreAuthCodes="D0100-D0999,D2740" } },
				{ CARRIER_SEC,new Carrier { CarrierNum=CARRIER_SEC,CarrierName="Synthetic Secondary",PreAuthCodes="D2740,D2950-D2954" } },
				{ CARRIER_NONE,new Carrier { CarrierNum=CARRIER_NONE,CarrierName="Synthetic None",PreAuthCodes="" } },
			};
			//CodeNum -> ProcCode.
			_dictProcCodes=new Dictionary<long,string> { { 1,"D0120" },{ 2,"D2740" },{ 3,"D2952" },{ 4,"D1110" },{ 5,"" } };
			//Primary plan 1001 (carrier 100) through subscription 501, secondary plan 1002 (carrier 200) through subscription 502.
			_listInsPlans=new List<InsPlan> {
				new InsPlan { PlanNum=1001,CarrierNum=CARRIER_PRI },
				new InsPlan { PlanNum=1002,CarrierNum=CARRIER_SEC },
				new InsPlan { PlanNum=1003,CarrierNum=CARRIER_NONE },
			};
			_listInsSubs=new List<InsSub> {
				new InsSub { InsSubNum=501,PlanNum=1001 },
				new InsSub { InsSubNum=502,PlanNum=1002 },
				new InsSub { InsSubNum=503,PlanNum=1003 },
			};
			_listPatPlans=new List<PatPlan> {
				new PatPlan { PatPlanNum=71,PatNum=PAT_NUM,InsSubNum=502,Ordinal=2 },
				new PatPlan { PatPlanNum=70,PatNum=PAT_NUM,InsSubNum=501,Ordinal=1 },
			};
		}

		private List<PreAuthNeed> GetNeeds(List<Procedure> listProcedures,List<ClaimProc> listClaimProcs=null) {
			return CarrierPreAuths.GetNeeds(listProcedures,_listPatPlans,_listInsSubs,_listInsPlans,listClaimProcs??new List<ClaimProc>(),
				x => _dictCarriers.TryGetValue(x,out Carrier carrier) ? carrier : null,
				x => _dictProcCodes.TryGetValue(x,out string procCode) ? procCode : "");
		}

		private static Procedure Proc(long procNum,long codeNum,ProcStat procStat=ProcStat.TP,long patNum=PAT_NUM) {
			return new Procedure { ProcNum=procNum,PatNum=patNum,CodeNum=codeNum,ProcStatus=procStat,ToothNum="3",Surf="",AptNum=900+procNum };
		}

		#region Code matching
		[TestMethod]
		public void Matching_RangesAndCodes() {
			Carrier carrier=new Carrier { PreAuthCodes="D0100-D0999,D2140-D2161,D2750" };
			foreach(string procCode in new[] { "D0100","D0120","D0999","D2140","D2150","D2161","D2750","d0120" }) {
				Assert.IsTrue(CarrierPreAuths.DoesCarrierRequire(carrier,procCode),procCode);
			}
			foreach(string procCode in new[] { "D1110","D2162","D275","D27500","D0099","N0120","","  " }) {
				Assert.IsFalse(CarrierPreAuths.DoesCarrierRequire(carrier,procCode),procCode);
			}
		}

		[TestMethod]
		public void Matching_RangeComparesCodesOfTheSameLengthAndPrefix() {
			Carrier carrier=new Carrier { PreAuthCodes="D2950-D2954" };
			Assert.IsTrue(CarrierPreAuths.DoesCarrierRequire(carrier,"D2952"));
			Assert.IsFalse(CarrierPreAuths.DoesCarrierRequire(carrier,"D29520"),"A longer code is not inside the range.");
			Assert.IsFalse(CarrierPreAuths.DoesCarrierRequire(carrier,"X2952"),"Another prefix is not inside the range.");
		}

		[TestMethod]
		public void Matching_NoListOrNoCarrierMatchesNothing() {
			Assert.IsFalse(CarrierPreAuths.DoesCarrierRequire(null,"D0120"));
			Assert.IsFalse(CarrierPreAuths.DoesCarrierRequire(new Carrier(),"D0120"));
			Assert.IsFalse(CarrierPreAuths.DoesCarrierRequire(new Carrier { PreAuthCodes=null },"D0120"));
		}
		#endregion Code matching

		#region Rules
		[TestMethod]
		public void Needs_TreatmentPlannedProcedureOfAListedCode() {
			List<PreAuthNeed> listPreAuthNeeds=GetNeeds(new List<Procedure> { Proc(1,1) });
			Assert.AreEqual(1,listPreAuthNeeds.Count);
			PreAuthNeed preAuthNeed=listPreAuthNeeds[0];
			Assert.AreEqual(1,preAuthNeed.ProcNum);
			Assert.AreEqual("D0120",preAuthNeed.ProcCode);
			Assert.AreEqual(1001,preAuthNeed.PlanNum);
			Assert.AreEqual(501,preAuthNeed.InsSubNum);
			Assert.AreEqual(70,preAuthNeed.PatPlanNum);
			Assert.AreEqual(1,preAuthNeed.Ordinal);
			Assert.AreEqual(CARRIER_PRI,preAuthNeed.CarrierNum);
			Assert.AreEqual("Synthetic Primary",preAuthNeed.CarrierName);
			Assert.AreEqual(901,preAuthNeed.AptNum);
			Assert.AreEqual("3",preAuthNeed.ToothNum);
		}

		[TestMethod]
		public void Needs_InactiveTreatmentPlanToo() {
			Assert.AreEqual(1,GetNeeds(new List<Procedure> { Proc(1,1,ProcStat.TPi) }).Count);
		}

		[TestMethod]
		public void Needs_OnlyTreatmentPlannedStatuses() {
			foreach(ProcStat procStat in new[] { ProcStat.C,ProcStat.EC,ProcStat.EO,ProcStat.R,ProcStat.D,ProcStat.Cn }) {
				Assert.AreEqual(0,GetNeeds(new List<Procedure> { Proc(1,1,procStat) }).Count,procStat.ToString());
			}
		}

		[TestMethod]
		public void Needs_EveryCarrierThatListsTheCodeInPlanOrder() {
			List<PreAuthNeed> listPreAuthNeeds=GetNeeds(new List<Procedure> { Proc(2,2) });//D2740: both carriers list it.
			CollectionAssert.AreEqual(new long[] { 1001,1002 },listPreAuthNeeds.Select(x => x.PlanNum).ToList());
			CollectionAssert.AreEqual(new byte[] { 1,2 },listPreAuthNeeds.Select(x => x.Ordinal).ToList());
		}

		[TestMethod]
		public void Needs_OnlyTheCarriersThatListTheCode() {
			List<PreAuthNeed> listPreAuthNeeds=GetNeeds(new List<Procedure> { Proc(3,3),Proc(4,4),Proc(5,5) });//D2952 secondary only, D1110 none, blank code
			Assert.AreEqual(1,listPreAuthNeeds.Count);
			Assert.AreEqual(3,listPreAuthNeeds[0].ProcNum);
			Assert.AreEqual(1002,listPreAuthNeeds[0].PlanNum);
		}

		[TestMethod]
		public void Needs_ClearedByAPreauthForThatPlan() {
			List<ClaimProc> listClaimProcs=new List<ClaimProc> { new ClaimProc { ProcNum=2,PlanNum=1001,InsSubNum=501,Status=ClaimProcStatus.Preauth } };
			List<PreAuthNeed> listPreAuthNeeds=GetNeeds(new List<Procedure> { Proc(2,2) },listClaimProcs);
			CollectionAssert.AreEqual(new long[] { 1002 },listPreAuthNeeds.Select(x => x.PlanNum).ToList(),"The secondary still needs one.");
			listClaimProcs.Add(new ClaimProc { ProcNum=2,PlanNum=1002,InsSubNum=502,Status=ClaimProcStatus.Preauth });
			Assert.AreEqual(0,GetNeeds(new List<Procedure> { Proc(2,2) },listClaimProcs).Count);
		}

		[TestMethod]
		public void Needs_NotClearedByOtherClaimProcs() {
			List<ClaimProc> listClaimProcs=new List<ClaimProc> {
				new ClaimProc { ProcNum=1,PlanNum=1001,Status=ClaimProcStatus.Estimate },
				new ClaimProc { ProcNum=1,PlanNum=1001,Status=ClaimProcStatus.NotReceived },
				new ClaimProc { ProcNum=99,PlanNum=1001,Status=ClaimProcStatus.Preauth },
			};
			Assert.AreEqual(1,GetNeeds(new List<Procedure> { Proc(1,1) },listClaimProcs).Count);
		}

		[TestMethod]
		public void Needs_NotForAPlanTheProcedureIsNotBilledTo() {
			List<ClaimProc> listClaimProcs=new List<ClaimProc> { new ClaimProc { ProcNum=2,PlanNum=1001,Status=ClaimProcStatus.Estimate,NoBillIns=true } };
			CollectionAssert.AreEqual(new long[] { 1002 },GetNeeds(new List<Procedure> { Proc(2,2) },listClaimProcs).Select(x => x.PlanNum).ToList());
		}

		[TestMethod]
		public void Needs_NoneWithoutInsuranceOrForOtherPatients() {
			Assert.AreEqual(0,GetNeeds(new List<Procedure> { Proc(1,1,patNum:11) }).Count,"Another patient without plans.");
			_listPatPlans.Clear();
			Assert.AreEqual(0,GetNeeds(new List<Procedure> { Proc(1,1) }).Count);
		}

		[TestMethod]
		public void Needs_CarrierWithoutAListNeverFlags() {
			_listPatPlans=new List<PatPlan> { new PatPlan { PatPlanNum=72,PatNum=PAT_NUM,InsSubNum=503,Ordinal=1 } };
			Assert.AreEqual(0,GetNeeds(new List<Procedure> { Proc(1,1),Proc(2,2) }).Count);
		}

		[TestMethod]
		public void Needs_LabProceduresAndRepeatsAreSkipped() {
			Procedure procedureLab=Proc(6,1);
			procedureLab.ProcNumLab=1;
			Assert.AreEqual(0,GetNeeds(new List<Procedure> { procedureLab }).Count);
			Assert.AreEqual(1,GetNeeds(new List<Procedure> { Proc(1,1),Proc(1,1) }).Count,"The same procedure twice counts once.");
		}

		[TestMethod]
		public void Needs_SamePlanThroughTwoSubscriptionsCountsOnce() {
			_listInsSubs.Add(new InsSub { InsSubNum=504,PlanNum=1001 });
			_listPatPlans.Add(new PatPlan { PatPlanNum=73,PatNum=PAT_NUM,InsSubNum=504,Ordinal=3 });
			Assert.AreEqual(1,GetNeeds(new List<Procedure> { Proc(1,1) }).Count);
		}

		[TestMethod]
		public void Needs_EmptyInputs() {
			Assert.AreEqual(0,GetNeeds(new List<Procedure>()).Count);
			Assert.AreEqual(0,GetNeeds(null).Count);
		}
		#endregion Rules
	}
}
