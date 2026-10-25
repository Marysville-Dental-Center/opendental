// v25_1-manage-era-autosplit-carc and v25_2-manage-era-carc-logic: what automatic processing decides for an ERA claim with listed CARCs, the
// remaining ERA claim after a split, and the claimproc matching. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using static BERATests.EraObjects;

namespace BERATests {
	[TestClass]
	public class EraAutoSplitPlanTests {
		private static readonly List<EraCarcRule> _listRules=EraCarcRules.Parse("97,16,B7");

		[TestInitialize]
		public void SetCulture() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		private static Hx835_Claim ClaimOnePaidOneDenied() {
			return EraClaim(EraProc("Z0120",60,48,1001,EraAdj("CO","45",12)),EraProc("Z0220",40,0,1002,EraAdj("CO","97",40)));
		}

		[TestMethod]
		public void Plan_NoListedCarcs_ProcessAsUsual() {
			Hx835_Claim claim=EraClaim(EraProc("Z0120",60,48,1001,EraAdj("CO","45",12)));
			Assert.AreEqual(EraAutoSplitDecision.NoListedCarcs,EraAutoSplit.GetPlan(claim,_listRules,isOptionOn:true).Decision);
			Assert.AreEqual(EraAutoSplitDecision.NoListedCarcs,EraAutoSplit.GetPlan(claim,_listRules,isOptionOn:false).Decision);
		}

		[TestMethod]
		public void Plan_OptionOff_Skips() {
			EraAutoSplitPlan plan=EraAutoSplit.GetPlan(ClaimOnePaidOneDenied(),_listRules,isOptionOn:false);
			Assert.AreEqual(EraAutoSplitDecision.OptionOff,plan.Decision);
			Assert.AreEqual(0,plan.ListProcsToSplit.Count);
			Assert.AreEqual("",plan.GetSkipReason());
		}

		[TestMethod]
		public void Plan_SomeProcsDenied_SplitsThoseProcs() {
			Hx835_Claim claim=ClaimOnePaidOneDenied();
			EraAutoSplitPlan plan=EraAutoSplit.GetPlan(claim,_listRules,isOptionOn:true);
			Assert.AreEqual(EraAutoSplitDecision.Split,plan.Decision);
			Assert.AreEqual(1,plan.ListProcsToSplit.Count);
			Assert.AreSame(claim.ListProcs[1],plan.ListProcsToSplit[0]);
			Assert.AreEqual("",plan.GetSkipReason());
		}

		[TestMethod]
		public void Plan_ClaimLevelCarc_NoSplit() {
			Hx835_Claim claim=ClaimOnePaidOneDenied();
			claim.ListClaimAdjustments.Add(EraAdj("CO","B7",10));
			EraAutoSplitPlan plan=EraAutoSplit.GetPlan(claim,_listRules,isOptionOn:true);
			Assert.AreEqual(EraAutoSplitDecision.ClaimLevelCarc,plan.Decision);
			StringAssert.Contains(plan.GetSkipReason(),"on the claim itself");
		}

		[TestMethod]
		public void Plan_EveryProcDenied_NoSplit() {
			Hx835_Claim claim=EraClaim(EraProc("Z0220",40,0,1002,EraAdj("CO","97",40)),EraProc("Z0230",30,0,1003,EraAdj("CO","16",30)));
			EraAutoSplitPlan plan=EraAutoSplit.GetPlan(claim,_listRules,isOptionOn:true);
			Assert.AreEqual(EraAutoSplitDecision.AllProcsDenied,plan.Decision);
			StringAssert.Contains(plan.GetSkipReason(),"every procedure");
		}

		[TestMethod]
		public void Plan_PreauthReversalOrPayerSplit_NotEligible() {
			Hx835_Claim claim=ClaimOnePaidOneDenied();
			claim.IsPreauth=true;
			Assert.AreEqual(EraAutoSplitDecision.NotEligible,EraAutoSplit.GetPlan(claim,_listRules,true).Decision);
			claim=ClaimOnePaidOneDenied();
			claim.IsReversal=true;
			Assert.AreEqual(EraAutoSplitDecision.NotEligible,EraAutoSplit.GetPlan(claim,_listRules,true).Decision);
			claim=ClaimOnePaidOneDenied();
			claim.IsSplitClaim=true;
			Assert.AreEqual(EraAutoSplitDecision.NotEligible,EraAutoSplit.GetPlan(claim,_listRules,true).Decision);
		}

		[TestMethod]
		public void Plan_ListedCodeOnAPaidLine_NoSplit() {
			Hx835_Claim claim=EraClaim(EraProc("Z0120",60,48,1001,EraAdj("CO","45",12)),EraProc("Z0220",40,10,1002,EraAdj("CO","97",30)));
			Assert.AreEqual(EraAutoSplitDecision.DeniedProcPaid,EraAutoSplit.GetPlan(claim,_listRules,true).Decision);
		}

		[TestMethod]
		public void Plan_QualifiedEntryOtherGroup_NotListed() {
			Hx835_Claim claim=ClaimOnePaidOneDenied();
			Assert.AreEqual(EraAutoSplitDecision.NoListedCarcs,EraAutoSplit.GetPlan(claim,EraCarcRules.Parse("PR-97"),true).Decision);
			Assert.AreEqual(EraAutoSplitDecision.Split,EraAutoSplit.GetPlan(claim,EraCarcRules.Parse("CO-97"),true).Decision);
		}

		[TestMethod]
		public void RemainingClaim_LeavesOutSplitLinesAndTheirAmounts() {
			Hx835_Claim claim=EraClaim(EraProc("Z0120",60,40,1001,EraAdj("CO","45",12),EraAdj("PR","1",8)),EraProc("Z0220",40,0,1002,EraAdj("CO","97",35),EraAdj("PR","2",5)));
			Hx835_Claim claimRemaining=EraAutoSplit.GetRemainingClaim(claim,new List<Hx835_Proc> { claim.ListProcs[1] });
			Assert.AreEqual(1,claimRemaining.ListProcs.Count);
			Assert.AreEqual("Z0120",claimRemaining.ListProcs[0].ProcCodeBilled);
			Assert.AreSame(claimRemaining,claimRemaining.ListProcs[0].ClaimPaid);
			Assert.AreEqual(60m,claimRemaining.ClaimFee);
			Assert.AreEqual(40m,claimRemaining.InsPaid);
			Assert.AreEqual(12m,claimRemaining.WriteoffAmt);
			Assert.AreEqual(8m,claimRemaining.PatientDeductAmt);
			Assert.AreEqual(0m,claimRemaining.PatientPortionAmt);
			Assert.AreEqual(8m,claimRemaining.PatientRespAmt);
			Assert.AreEqual(48m,claimRemaining.AllowedAmt);
			Assert.AreEqual(claim.ClpSegmentIndex,claimRemaining.ClpSegmentIndex);
			//The ERA claim itself is unchanged.
			Assert.AreEqual(2,claim.ListProcs.Count);
			Assert.AreEqual(100m,claim.ClaimFee);
			Assert.AreEqual(47m,claim.WriteoffAmt);
			Assert.AreSame(claim,claim.ListProcs[1].ClaimPaid);
		}

		[TestMethod]
		public void RemainingClaim_HasNoListedCarcs() {
			Hx835_Claim claim=ClaimOnePaidOneDenied();
			EraAutoSplitPlan plan=EraAutoSplit.GetPlan(claim,_listRules,true);
			Hx835_Claim claimRemaining=EraAutoSplit.GetRemainingClaim(claim,plan.ListProcsToSplit);
			//25.2: the no-auto-process check applies to each resulting claim separately.
			Assert.AreEqual(0,EraCarcRules.GetMatches(_listRules,claimRemaining).Count);
		}

		[TestMethod]
		public void MatchClaimProcs_ByProcNumThenCodeAndFee() {
			List<ClaimProc> listClaimProcs=new List<ClaimProc> {
				NewClaimProc(1,1001,"Z0120",60),
				NewClaimProc(2,1002,"Z0220",40),
				NewClaimProc(3,1003,"Z0230",30),
			};
			List<ClaimProc> listMatched=EraAutoSplit.MatchClaimProcs(new List<Hx835_Proc> { EraProc("Z0220",40,0,1002) },listClaimProcs);
			Assert.AreEqual(2,listMatched.Single().ClaimProcNum);
			listMatched=EraAutoSplit.MatchClaimProcs(new List<Hx835_Proc> { EraProc("Z0230",30,0,0) },listClaimProcs);//No ProcNum on the line.
			Assert.AreEqual(3,listMatched.Single().ClaimProcNum);
			Assert.IsNull(EraAutoSplit.MatchClaimProcs(new List<Hx835_Proc> { EraProc("Z0330",30,0,0) },listClaimProcs));
		}

		[TestMethod]
		public void MatchClaimProcs_OnlyNotReceivedAndEachClaimProcOnce() {
			List<ClaimProc> listClaimProcs=new List<ClaimProc> {
				NewClaimProc(1,1001,"Z0220",40,ClaimProcStatus.Received),
				NewClaimProc(2,1002,"Z0220",40),
			};
			List<ClaimProc> listMatched=EraAutoSplit.MatchClaimProcs(new List<Hx835_Proc> { EraProc("Z0220",40,0,0) },listClaimProcs);
			Assert.AreEqual(2,listMatched.Single().ClaimProcNum);
			Assert.IsNull(EraAutoSplit.MatchClaimProcs(new List<Hx835_Proc> { EraProc("Z0220",40,0,0),EraProc("Z0220",40,0,0) },listClaimProcs));
		}

		[TestMethod]
		public void DoAllProcsMatch_TrueOnlyWhenLinesAndClaimProcsPairUp() {
			Hx835_Claim claim=EraClaim(EraProc("Z0120",60,48,1001));
			Assert.IsTrue(EraAutoSplit.DoAllProcsMatch(claim,new List<ClaimProc> { NewClaimProc(1,1001,"Z0120",60) }));
			//A claimproc with no line on the ERA.
			Assert.IsFalse(EraAutoSplit.DoAllProcsMatch(claim,new List<ClaimProc> { NewClaimProc(1,1001,"Z0120",60),NewClaimProc(2,1005,"Z0330",30) }));
			//A line with no claimproc.
			Assert.IsFalse(EraAutoSplit.DoAllProcsMatch(EraClaim(EraProc("Z0120",60,48,1001),EraProc("Z0330",30,0,0)),new List<ClaimProc> { NewClaimProc(1,1001,"Z0120",60) }));
			Assert.IsFalse(EraAutoSplit.DoAllProcsMatch(claim,new List<ClaimProc>()));
		}

		[TestMethod]
		public void ClaimSkipReason_ReceivedPreauthOrCapitation() {
			List<ClaimProc> listClaimProcs=new List<ClaimProc> { NewClaimProc(1,1001,"Z0120",60) };
			Assert.AreEqual("",EraAutoSplit.GetClaimSkipReason(new Claim { ClaimStatus="S",ClaimType="P" },listClaimProcs));
			StringAssert.Contains(EraAutoSplit.GetClaimSkipReason(new Claim { ClaimStatus="R",ClaimType="P" },listClaimProcs),"received");
			StringAssert.Contains(EraAutoSplit.GetClaimSkipReason(new Claim { ClaimStatus="S",ClaimType="PreAuth" },listClaimProcs),"preauthorization");
			StringAssert.Contains(EraAutoSplit.GetClaimSkipReason(new Claim { ClaimStatus="S",ClaimType="Cap" },listClaimProcs),"capitation");
			listClaimProcs.Add(NewClaimProc(2,1002,"Z0220",40,ClaimProcStatus.Received));
			StringAssert.Contains(EraAutoSplit.GetClaimSkipReason(new Claim { ClaimStatus="S",ClaimType="P" },listClaimProcs),"received");
		}
	}
}
