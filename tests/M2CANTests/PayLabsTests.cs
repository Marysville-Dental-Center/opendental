// v25_4-canada-labfee-pay: Pay Labs in the claim payment window (Canada). The insurance payment entered on a procedure is handed out again to its
// labs (at most two), each up to its fee billed, and the procedure keeps the rest. Synthetic claim procedures only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2CANTests {
	[TestClass]
	public class PayLabsTests {
		private const long CLAIM_NUM=50;
		private const long PLAN_NUM=60;

		///<summary>Without the constructor, which reads a preference (these tests need no database).</summary>
		private static Procedure Proc(long procNum,long procNumLab=0) {
			Procedure procedure=(Procedure)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Procedure));
			procedure.ProcNum=procNum;
			procedure.ProcNumLab=procNumLab;
			return procedure;
		}

		private static ClaimProc Cp(long claimProcNum,long procNum,double feeBilled,double insPayAmt,ClaimProcStatus status=ClaimProcStatus.Received) {
			return new ClaimProc { ClaimProcNum=claimProcNum,ProcNum=procNum,ClaimNum=CLAIM_NUM,PlanNum=PLAN_NUM,FeeBilled=feeBilled,InsPayAmt=insPayAmt,
				Status=status,WriteOff=7,DedApplied=3 };
		}

		[TestMethod]
		public void FullPaymentOnTheProcedure_LabsGetTheirFees() {
			List<Procedure> listProcedures=new List<Procedure> { Proc(1),Proc(2,procNumLab:1),Proc(3,procNumLab:1),Proc(4) };
			ClaimProc[] arrayClaimProcs=new[] {
				Cp(11,1,feeBilled:500,insPayAmt:650),
				Cp(12,2,feeBilled:100,insPayAmt:0),
				Cp(13,3,feeBilled:50,insPayAmt:0),
				Cp(14,4,feeBilled:80,insPayAmt:80),//No labs: untouched.
			};
			Assert.IsTrue(ClaimProcs.HasPayLabsGroups(arrayClaimProcs,listProcedures));
			Assert.AreEqual(3,ClaimProcs.PayLabs(arrayClaimProcs,listProcedures));
			CollectionAssert.AreEqual(new[] { 500d,100d,50d,80d },arrayClaimProcs.Select(x => x.InsPayAmt).ToArray());
			//Write-offs and deductibles are not touched.
			Assert.IsTrue(arrayClaimProcs.All(x => x.WriteOff==7 && x.DedApplied==3));
			//Twice gives the same result.
			Assert.AreEqual(0,ClaimProcs.PayLabs(arrayClaimProcs,listProcedures));
			CollectionAssert.AreEqual(new[] { 500d,100d,50d,80d },arrayClaimProcs.Select(x => x.InsPayAmt).ToArray());
		}

		[TestMethod]
		public void PaymentSmallerThanTheLabs_ProcedureLeftAtZero() {
			List<Procedure> listProcedures=new List<Procedure> { Proc(1),Proc(2,procNumLab:1),Proc(3,procNumLab:1) };
			ClaimProc[] arrayClaimProcs=new[] { Cp(11,1,500,120),Cp(12,2,100,0),Cp(13,3,50,0) };
			ClaimProcs.PayLabs(arrayClaimProcs,listProcedures);
			CollectionAssert.AreEqual(new[] { 0d,100d,20d },arrayClaimProcs.Select(x => x.InsPayAmt).ToArray());
			Assert.IsTrue(arrayClaimProcs.All(x => x.InsPayAmt>=0));
		}

		[TestMethod]
		public void AmountsAlreadyOnTheLabs_AreAddedUpFirst() {
			List<Procedure> listProcedures=new List<Procedure> { Proc(1),Proc(2,procNumLab:1) };
			//The user typed 130 on the lab, more than its fee: the extra goes back to the procedure.
			ClaimProc[] arrayClaimProcs=new[] { Cp(11,1,500,400),Cp(12,2,100,130) };
			ClaimProcs.PayLabs(arrayClaimProcs,listProcedures);
			CollectionAssert.AreEqual(new[] { 430d,100d },arrayClaimProcs.Select(x => x.InsPayAmt).ToArray());
		}

		[TestMethod]
		public void CentsAreKept() {
			List<Procedure> listProcedures=new List<Procedure> { Proc(1),Proc(2,procNumLab:1) };
			ClaimProc[] arrayClaimProcs=new[] { Cp(11,1,200.10,215.25),Cp(12,2,15.15,0) };
			ClaimProcs.PayLabs(arrayClaimProcs,listProcedures);
			Assert.AreEqual(200.10,arrayClaimProcs[0].InsPayAmt,0.0001);
			Assert.AreEqual(15.15,arrayClaimProcs[1].InsPayAmt,0.0001);
		}

		[TestMethod]
		public void NothingPaid_NothingMoves() {
			List<Procedure> listProcedures=new List<Procedure> { Proc(1),Proc(2,procNumLab:1) };
			ClaimProc[] arrayClaimProcs=new[] { Cp(11,1,500,0),Cp(12,2,100,0) };
			Assert.AreEqual(0,ClaimProcs.PayLabs(arrayClaimProcs,listProcedures));
			//A negative supplemental total is left to the user.
			arrayClaimProcs=new[] { Cp(11,1,0,-40,ClaimProcStatus.Supplemental),Cp(12,2,0,0,ClaimProcStatus.Supplemental) };
			Assert.AreEqual(0,ClaimProcs.PayLabs(arrayClaimProcs,listProcedures));
			Assert.AreEqual(-40d,arrayClaimProcs[0].InsPayAmt);
		}

		[TestMethod]
		public void LabsMustShareClaimPlanAndStatus_AtMostTwo() {
			List<Procedure> listProcedures=new List<Procedure> { Proc(1),Proc(2,procNumLab:1),Proc(3,procNumLab:1),Proc(5,procNumLab:1),Proc(6,procNumLab:1) };
			ClaimProc claimProcOtherStatus=Cp(15,5,40,0,ClaimProcStatus.Supplemental);
			ClaimProc claimProcOtherPlan=Cp(16,6,40,0);
			claimProcOtherPlan.PlanNum=PLAN_NUM+1;
			ClaimProc[] arrayClaimProcs=new[] { Cp(11,1,500,1000),claimProcOtherStatus,claimProcOtherPlan,Cp(12,2,100,0),Cp(13,3,50,0) };
			List<PayLabsGroup> listPayLabsGroups=ClaimProcs.GetPayLabsGroups(arrayClaimProcs,listProcedures);
			Assert.AreEqual(1,listPayLabsGroups.Count);
			CollectionAssert.AreEqual(new long[] { 12,13 },listPayLabsGroups[0].ListClaimProcsLab.Select(x => x.ClaimProcNum).ToArray());
			ClaimProcs.PayLabs(arrayClaimProcs,listProcedures);
			Assert.AreEqual(0d,claimProcOtherStatus.InsPayAmt);
			Assert.AreEqual(0d,claimProcOtherPlan.InsPayAmt);
			Assert.AreEqual(850d,arrayClaimProcs[0].InsPayAmt);
			//A third lab of the same claim and status is left alone (a procedure has at most two).
			ClaimProc[] arrayThree=new[] { Cp(11,1,500,1000),Cp(12,2,100,0),Cp(13,3,50,0),Cp(17,5,40,0) };
			listPayLabsGroups=ClaimProcs.GetPayLabsGroups(arrayThree,listProcedures);
			Assert.AreEqual(2,listPayLabsGroups[0].ListClaimProcsLab.Count);
		}

		[TestMethod]
		public void NoLabs_NoButton() {
			List<Procedure> listProcedures=new List<Procedure> { Proc(1),Proc(4) };
			ClaimProc[] arrayClaimProcs=new[] { Cp(11,1,500,400),Cp(14,4,80,80),Cp(18,0,0,25) };//ProcNum 0: a total payment row.
			Assert.IsFalse(ClaimProcs.HasPayLabsGroups(arrayClaimProcs,listProcedures));
			Assert.AreEqual(0,ClaimProcs.PayLabs(arrayClaimProcs,listProcedures));
			//A lab whose procedure is not in the payment.
			listProcedures.Add(Proc(2,procNumLab:99));
			Assert.IsFalse(ClaimProcs.HasPayLabsGroups(new[] { Cp(12,2,100,0),Cp(11,1,500,400) },listProcedures));
			Assert.IsFalse(ClaimProcs.HasPayLabsGroups(null,listProcedures));
		}
	}
}
