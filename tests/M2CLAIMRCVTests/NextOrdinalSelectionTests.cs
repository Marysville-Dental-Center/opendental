// Claims.SelectNextOrdinalClaims and Claims.GetClaimOrdinal on in-memory rows (no database): which unsent claims the related-claims prompt offers
// after a claim of any ordinal is received (Open Dental 26.1). Synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2CLAIMRCVTests {
	[TestClass]
	public class NextOrdinalSelectionTests {
		private const long PAT_NUM=7001;
		private const long PROC_A=501;
		private const long PROC_B=502;
		private const long PROC_C=503;

		///<summary>Rows for one patient: patplans with ordinals 1..countPlans on InsSubNums 11, 12, ... and the claims and claimprocs the test
		///adds.</summary>
		private class Rows {
			public List<PatPlan> ListPatPlans=new List<PatPlan>();
			public List<Claim> ListClaims=new List<Claim>();
			public List<ClaimProc> ListClaimProcs=new List<ClaimProc>();
			private long _claimNumNext=9001;
			private long _claimProcNumNext=1;

			public Rows(int countPlans,long patNum=PAT_NUM) {
				for(int i=1;i<=countPlans;i++) {
					ListPatPlans.Add(new PatPlan { PatPlanNum=100+i,PatNum=patNum,InsSubNum=SubNum(i),Ordinal=(byte)i });
				}
			}

			public static long SubNum(int ordinal) {
				return 10+ordinal;
			}

			///<summary>Adds a claim on the insurance with this ordinal, with one claimproc per procedure.</summary>
			public Claim AddClaim(int ordinal,string claimType,string claimStatus,ClaimProcStatus claimProcStatus,params long[] arrayProcNums) {
				return AddClaimForSub(SubNum(ordinal),claimType,claimStatus,claimProcStatus,PAT_NUM,arrayProcNums);
			}

			public Claim AddClaimForSub(long insSubNum,string claimType,string claimStatus,ClaimProcStatus claimProcStatus,long patNum,params long[] arrayProcNums) {
				Claim claim=new Claim { ClaimNum=_claimNumNext++,PatNum=patNum,InsSubNum=insSubNum,PlanNum=insSubNum+1000,ClaimType=claimType,ClaimStatus=claimStatus };
				ListClaims.Add(claim);
				for(int i=0;i<arrayProcNums.Length;i++) {
					ListClaimProcs.Add(new ClaimProc { ClaimProcNum=_claimProcNumNext++,ClaimNum=claim.ClaimNum,ProcNum=arrayProcNums[i],PatNum=patNum,
						InsSubNum=insSubNum,PlanNum=claim.PlanNum,Status=claimProcStatus });
				}
				return claim;
			}

			public List<ClaimProc> ForClaim(Claim claim) {
				return ListClaimProcs.FindAll(x => x.ClaimNum==claim.ClaimNum);
			}

			public List<long> Select(Claim claimReceived,bool includeHoldForInProcess=false) {
				return Select(ForClaim(claimReceived),includeHoldForInProcess);
			}

			public List<long> Select(List<ClaimProc> listClaimProcsReceived,bool includeHoldForInProcess=false) {
				return Claims.SelectNextOrdinalClaims(listClaimProcsReceived,ListClaimProcs,ListClaims,ListPatPlans,includeHoldForInProcess)
					.Select(x => x.ClaimNum).OrderBy(x => x).ToList();
			}
		}

		private static void AssertClaims(List<long> listClaimNumsActual,string message,params Claim[] arrayClaimsExpected) {
			List<long> listClaimNumsExpected=arrayClaimsExpected.Select(x => x.ClaimNum).OrderBy(x => x).ToList();
			CollectionAssert.AreEqual(listClaimNumsExpected,listClaimNumsActual,message+" Expected: "+string.Join(",",listClaimNumsExpected)
				+"; actual: "+string.Join(",",listClaimNumsActual));
		}

		[TestMethod]
		public void PrimaryReceived_OffersTheSecondaryClaim() {
			Rows rows=new Rows(3);
			Claim claimPrimary=rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_A,PROC_B);
			Claim claimSecondary=rows.AddClaim(2,"S","H",ClaimProcStatus.NotReceived,PROC_A,PROC_B);
			rows.AddClaim(3,"Other","U",ClaimProcStatus.NotReceived,PROC_A,PROC_B);
			AssertClaims(rows.Select(claimPrimary),"24.3 behavior is kept: a primary offers its secondary claims, and only those.",claimSecondary);
		}

		[TestMethod]
		public void SecondaryReceived_OffersTheTertiaryClaim() {
			Rows rows=new Rows(3);
			rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_A);
			Claim claimSecondary=rows.AddClaim(2,"S","R",ClaimProcStatus.Received,PROC_A);
			Claim claimTertiary=rows.AddClaim(3,"Other","H",ClaimProcStatus.NotReceived,PROC_A);
			AssertClaims(rows.Select(claimSecondary),"26.1: receiving a secondary claim offers the tertiary claim.",claimTertiary);
		}

		[TestMethod]
		public void AnyOrdinal_UpToTen() {
			Rows rows=new Rows(10);
			List<Claim> listClaims=new List<Claim>();
			for(int ordinal=1;ordinal<=10;ordinal++) {
				listClaims.Add(rows.AddClaim(ordinal,ordinal==1 ? "P" : (ordinal==2 ? "S" : "Other"),"U",ClaimProcStatus.NotReceived,PROC_A));
			}
			for(int ordinal=1;ordinal<10;ordinal++) {
				Claim claimReceived=listClaims[ordinal-1];
				claimReceived.ClaimStatus="R";
				rows.ForClaim(claimReceived).ForEach(x => x.Status=ClaimProcStatus.Received);
				AssertClaims(rows.Select(claimReceived),"Ordinal "+ordinal+" offers ordinal "+(ordinal+1)+".",listClaims[ordinal]);
			}
			Claim claimLast=listClaims[9];
			claimLast.ClaimStatus="R";
			AssertClaims(rows.Select(claimLast),"The last insurance has no next insurance.");
		}

		[TestMethod]
		public void NextInsuranceSentOrWaiting_LaterInsuranceIsNotOffered() {
			foreach(string statusSecondary in new[] { "S","W","P" }) {
				Rows rows=new Rows(3);
				Claim claimPrimary=rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_A);
				rows.AddClaim(2,"S",statusSecondary,ClaimProcStatus.NotReceived,PROC_A);
				rows.AddClaim(3,"Other","H",ClaimProcStatus.NotReceived,PROC_A);
				AssertClaims(rows.Select(claimPrimary),"Secondary status "+statusSecondary+": the tertiary claim waits for the secondary claim.");
			}
		}

		[TestMethod]
		public void LaterInsuranceAlreadyReceived_IsPassedOver() {
			//Received out of order: the secondary claim came back before the primary claim.
			Rows rows=new Rows(3);
			Claim claimPrimary=rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_A);
			rows.AddClaim(2,"S","R",ClaimProcStatus.Received,PROC_A);
			Claim claimTertiary=rows.AddClaim(3,"Other","H",ClaimProcStatus.NotReceived,PROC_A);
			AssertClaims(rows.Select(claimPrimary),"Every prior insurance has received the procedure now.",claimTertiary);
		}

		[TestMethod]
		public void InsuranceWithoutClaimForTheProcedures_IsSkipped() {
			//Ordinal 2 is, for example, a medical plan with no claim for these dental procedures.
			Rows rows=new Rows(3);
			Claim claimPrimary=rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_A);
			rows.AddClaim(2,"P","U",ClaimProcStatus.NotReceived,PROC_C);//Another procedure only.
			Claim claimDentalSecondary=rows.AddClaim(3,"S","H",ClaimProcStatus.NotReceived,PROC_A);
			AssertClaims(rows.Select(claimPrimary),"The next insurance is the next one that has a claim for the received procedures.",claimDentalSecondary);
		}

		[TestMethod]
		public void ClaimsMustShareAProcedure() {
			Rows rows=new Rows(2);
			Claim claimPrimary=rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_A);
			rows.AddClaim(2,"S","H",ClaimProcStatus.NotReceived,PROC_B);
			AssertClaims(rows.Select(claimPrimary),"A secondary claim for other procedures is not related.");
		}

		[TestMethod]
		public void SplitClaimsAtTheNextOrdinal_AreAllOffered() {
			Rows rows=new Rows(2);
			Claim claimPrimary=rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_A,PROC_B);
			Claim claimSecondaryA=rows.AddClaim(2,"S","H",ClaimProcStatus.NotReceived,PROC_A);
			Claim claimSecondaryB=rows.AddClaim(2,"S","U",ClaimProcStatus.NotReceived,PROC_B);
			AssertClaims(rows.Select(claimPrimary),"Both claims of the next insurance are offered.",claimSecondaryA,claimSecondaryB);
		}

		[TestMethod]
		public void PreAuthorizations_AreNeverRelated() {
			Rows rows=new Rows(2);
			Claim claimPrimary=rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_A);
			rows.AddClaim(2,"PreAuth","U",ClaimProcStatus.Preauth,PROC_A);
			AssertClaims(rows.Select(claimPrimary),"A preauthorization of the next insurance is not offered.");
			Rows rowsPreAuthReceived=new Rows(2);
			Claim claimPreAuth=rowsPreAuthReceived.AddClaim(1,"PreAuth","R",ClaimProcStatus.Preauth,PROC_A);
			rowsPreAuthReceived.AddClaim(2,"S","H",ClaimProcStatus.NotReceived,PROC_A);
			AssertClaims(rowsPreAuthReceived.Select(claimPreAuth),"A received preauthorization offers nothing.");
		}

		[TestMethod]
		public void HoldForInProcess_OnlyWhenAsked() {
			Rows rows=new Rows(2);
			Claim claimPrimary=rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_A);
			Claim claimSecondary=rows.AddClaim(2,"S","I",ClaimProcStatus.NotReceived,PROC_A,PROC_B);
			AssertClaims(rows.Select(claimPrimary),"The prompt never offers a claim on hold for an in-process multi-visit group.");
			AssertClaims(rows.Select(claimPrimary,includeHoldForInProcess:true),"Recalculation includes it.",claimSecondary);
		}

		[TestMethod]
		public void RelatedClaim_NeedsANotReceivedClaimProcOnTheSharedProcedures() {
			Rows rows=new Rows(2);
			Claim claimPrimary=rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_A);
			rows.AddClaim(2,"S","U",ClaimProcStatus.Received,PROC_A);//An odd row: status U with a received claimproc.
			AssertClaims(rows.Select(claimPrimary),"Like 24.3, only claims with a not-received claimproc for the procedures are offered.");
		}

		[TestMethod]
		public void OtherPatientsClaims_AreIgnored() {
			Rows rows=new Rows(2);
			Claim claimPrimary=rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_A);
			rows.ListPatPlans.Add(new PatPlan { PatPlanNum=900,PatNum=PAT_NUM+1,InsSubNum=99,Ordinal=2 });
			rows.AddClaimForSub(99,"S","U",ClaimProcStatus.NotReceived,PAT_NUM+1,PROC_A);
			AssertClaims(rows.Select(claimPrimary),"A claim of another patient is never related.");
		}

		[TestMethod]
		public void SeveralReceivedClaims_EachGetsItsNextInsurance_NoDuplicates() {
			Rows rows=new Rows(3);
			Claim claimPrimary=rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_A,PROC_B);
			Claim claimSecondary=rows.AddClaim(2,"S","H",ClaimProcStatus.NotReceived,PROC_A,PROC_B);
			Claim claimOtherPrimary=rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_B);
			List<ClaimProc> listClaimProcsReceived=rows.ForClaim(claimPrimary).Concat(rows.ForClaim(claimOtherPrimary)).ToList();
			AssertClaims(rows.Select(listClaimProcsReceived),"The secondary claim is listed once.",claimSecondary);
		}

		[TestMethod]
		public void DroppedPlan_FallsBackToTheClaimType() {
			Rows rows=new Rows(2);
			//The primary claim's plan was dropped and a new primary plan (no claim for these procedures) took ordinal 1, so the secondary plan is
			//still ordinal 2: a primary claim whose patplan is gone counts as ordinal 1.
			Claim claimPrimaryDropped=rows.AddClaimForSub(77,"P","R",ClaimProcStatus.Received,PAT_NUM,PROC_A);
			Claim claimSecondary=rows.AddClaim(2,"S","H",ClaimProcStatus.NotReceived,PROC_A);
			AssertClaims(rows.Select(claimPrimaryDropped),"Dropped primary plan.",claimSecondary);
			Rows rowsOther=new Rows(1);
			Claim claimOtherDropped=rowsOther.AddClaimForSub(78,"Other","R",ClaimProcStatus.Received,PAT_NUM,PROC_A);
			rowsOther.AddClaim(1,"P","U",ClaimProcStatus.NotReceived,PROC_A);
			AssertClaims(rowsOther.Select(claimOtherDropped),"An 'Other' claim whose plan is gone has no known ordinal, so nothing is offered.");
		}

		[TestMethod]
		public void DroppedPrimary_SecondaryMovedToOrdinalOne_IsStillOffered() {
			//PatPlans.Delete lowers every later patplan's ordinal by one: after the primary plan is dropped, the secondary plan is the only patplan,
			//at ordinal 1, the same ordinal the dropped primary claim falls back to. 24.3 offered the secondary claim here.
			Rows rows=new Rows(1);
			Claim claimPrimaryDropped=rows.AddClaimForSub(77,"P","R",ClaimProcStatus.Received,PAT_NUM,PROC_A);
			Claim claimSecondary=rows.AddClaimForSub(Rows.SubNum(1),"S","H",ClaimProcStatus.NotReceived,PAT_NUM,PROC_A);
			AssertClaims(rows.Select(claimPrimaryDropped),"The secondary claim is offered after the dropped primary claim is received.",claimSecondary);
			AssertClaims(rows.Select(claimPrimaryDropped,includeHoldForInProcess:true),"It is recalculated too.",claimSecondary);
		}

		[TestMethod]
		public void DroppedPrimary_TertiaryWaitsForTheSecondary() {
			//Primary dropped: the secondary plan is ordinal 1 now and the tertiary plan ordinal 2.
			Rows rows=new Rows(2);
			Claim claimPrimaryDropped=rows.AddClaimForSub(77,"P","R",ClaimProcStatus.Received,PAT_NUM,PROC_A);
			Claim claimSecondary=rows.AddClaim(1,"S","H",ClaimProcStatus.NotReceived,PROC_A);
			Claim claimTertiary=rows.AddClaim(2,"Other","H",ClaimProcStatus.NotReceived,PROC_A);
			AssertClaims(rows.Select(claimPrimaryDropped),"Only the secondary claim; the tertiary claim does not jump ahead of it.",claimSecondary);
			claimSecondary.ClaimStatus="R";
			rows.ForClaim(claimSecondary).ForEach(x => x.Status=ClaimProcStatus.Received);
			AssertClaims(rows.Select(claimSecondary),"Receiving the secondary claim offers the tertiary claim.",claimTertiary);
			AssertClaims(rows.Select(claimPrimaryDropped),"Secondary received first: the tertiary claim is next.",claimTertiary);
		}

		[TestMethod]
		public void DroppedSecondary_SecondaryFirst_ThenTheTertiary() {
			//PatPlans.Delete on the secondary plan: the primary plan stays ordinal 1 and the tertiary plan moves to ordinal 2, the ordinal the dropped
			//secondary claim falls back to. The secondary claim still comes first, alone, and the tertiary claim follows it.
			Rows rows=new Rows(2);
			Claim claimPrimary=rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_A);
			Claim claimSecondaryDropped=rows.AddClaimForSub(78,"S","H",ClaimProcStatus.NotReceived,PAT_NUM,PROC_A);
			Claim claimTertiary=rows.AddClaim(2,"Other","H",ClaimProcStatus.NotReceived,PROC_A);
			AssertClaims(rows.Select(claimPrimary),"Only the secondary claim; the tertiary claim does not come with it.",claimSecondaryDropped);
			claimSecondaryDropped.ClaimStatus="R";
			rows.ForClaim(claimSecondaryDropped).ForEach(x => x.Status=ClaimProcStatus.Received);
			AssertClaims(rows.Select(claimSecondaryDropped),"The dropped secondary claim's next insurance is the tertiary plan (ordinal 2 now).",claimTertiary);
			AssertClaims(rows.Select(claimPrimary),"Secondary received: the tertiary claim is next after the primary claim too.",claimTertiary);
		}

		[TestMethod]
		public void BothPlansDropped_SecondaryStillComesFirst() {
			//Primary and secondary plans dropped: the tertiary plan is ordinal 1 now. The claim types still say the secondary claim is next.
			Rows rows=new Rows(1);
			Claim claimPrimaryDropped=rows.AddClaimForSub(77,"P","R",ClaimProcStatus.Received,PAT_NUM,PROC_A);
			Claim claimSecondaryDropped=rows.AddClaimForSub(78,"S","H",ClaimProcStatus.NotReceived,PAT_NUM,PROC_A);
			Claim claimTertiary=rows.AddClaim(1,"Other","H",ClaimProcStatus.NotReceived,PROC_A);
			AssertClaims(rows.Select(claimPrimaryDropped),"The secondary claim, not the tertiary claim.",claimSecondaryDropped);
			claimSecondaryDropped.ClaimStatus="R";
			rows.ForClaim(claimSecondaryDropped).ForEach(x => x.Status=ClaimProcStatus.Received);
			AssertClaims(rows.Select(claimPrimaryDropped),"Then the tertiary claim.",claimTertiary);
		}

		[TestMethod]
		public void LivePlans_TheOrdinalsDecide() {
			//Every plan is on the patient and the secondary claim's plan is later than another plan with a claim for the procedure (for example a
			//medical plan first, then the dental primary and secondary): the next ordinal comes first.
			Rows rows=new Rows(3);
			Claim claimFirst=rows.AddClaim(1,"P","R",ClaimProcStatus.Received,PROC_A);
			Claim claimDentalPrimary=rows.AddClaim(2,"P","H",ClaimProcStatus.NotReceived,PROC_A);
			Claim claimDentalSecondary=rows.AddClaim(3,"S","H",ClaimProcStatus.NotReceived,PROC_A);
			AssertClaims(rows.Select(claimFirst),"Ordinal 2 is next.",claimDentalPrimary);
			claimDentalPrimary.ClaimStatus="R";
			rows.ForClaim(claimDentalPrimary).ForEach(x => x.Status=ClaimProcStatus.Received);
			AssertClaims(rows.Select(claimDentalPrimary),"Then ordinal 3.",claimDentalSecondary);
		}

		[TestMethod]
		public void ReorderedPlans_SecondaryStillFollowsThePrimary() {
			//The plans were swapped after the claims were made: the primary claim's plan is ordinal 2 now, the secondary claim's plan ordinal 1.
			Rows rows=new Rows(3);
			Claim claimPrimary=rows.AddClaim(2,"P","R",ClaimProcStatus.Received,PROC_A);
			Claim claimSecondary=rows.AddClaim(1,"S","U",ClaimProcStatus.NotReceived,PROC_A);
			rows.AddClaim(3,"Other","H",ClaimProcStatus.NotReceived,PROC_A);
			AssertClaims(rows.Select(claimPrimary),"24.3 order: the secondary claim comes first after a primary claim.",claimSecondary);
		}

		[TestMethod]
		public void LowerOrdinal_OnlySecondaryAfterPrimaryIsKept() {
			//The 24.3 exception is for a secondary claim after a primary claim only: other claims at a lower ordinal are prior insurance.
			Rows rows=new Rows(3);
			rows.AddClaim(1,"P","U",ClaimProcStatus.NotReceived,PROC_A);
			rows.AddClaim(2,"S","H",ClaimProcStatus.NotReceived,PROC_A);
			Claim claimTertiary=rows.AddClaim(3,"Other","R",ClaimProcStatus.Received,PROC_A);
			AssertClaims(rows.Select(claimTertiary),"A received tertiary claim offers neither the primary nor the secondary claim.");
			Rows rowsSecondary=new Rows(2);
			rowsSecondary.AddClaim(1,"P","U",ClaimProcStatus.NotReceived,PROC_A);
			Claim claimSecondary=rowsSecondary.AddClaim(2,"S","R",ClaimProcStatus.Received,PROC_A);
			AssertClaims(rowsSecondary.Select(claimSecondary),"A received secondary claim does not offer the primary claim.");
		}

		[TestMethod]
		public void EmptyInput_ReturnsNothing() {
			Rows rows=new Rows(2);
			Assert.AreEqual(0,Claims.SelectNextOrdinalClaims(new List<ClaimProc>(),rows.ListClaimProcs,rows.ListClaims,rows.ListPatPlans).Count);
			Assert.AreEqual(0,Claims.SelectNextOrdinalClaims(null,rows.ListClaimProcs,rows.ListClaims,rows.ListPatPlans).Count);
			Assert.AreEqual(0,Claims.GetNextOrdinalClaimsNotReceived(new List<ClaimProc>()).Count,"No database call for an empty list.");
			Assert.AreEqual(0,Claims.GetNextOrdinalClaimsNotReceived(null).Count);
		}

		[TestMethod]
		public void GetClaimOrdinal_PatPlanFirstThenClaimType() {
			List<PatPlan> listPatPlans=new List<PatPlan> {
				new PatPlan { PatNum=PAT_NUM,InsSubNum=21,Ordinal=1 },
				new PatPlan { PatNum=PAT_NUM,InsSubNum=22,Ordinal=2 },
				new PatPlan { PatNum=PAT_NUM,InsSubNum=23,Ordinal=3 },
			};
			Assert.AreEqual(3,Claims.GetClaimOrdinal(new Claim { PatNum=PAT_NUM,InsSubNum=23,ClaimType="P" },listPatPlans),"The patplan wins over the type.");
			Assert.AreEqual(1,Claims.GetClaimOrdinal(new Claim { PatNum=PAT_NUM,InsSubNum=21,ClaimType="S" },listPatPlans));
			Assert.AreEqual(1,Claims.GetClaimOrdinal(new Claim { PatNum=PAT_NUM,InsSubNum=99,ClaimType="P" },listPatPlans));
			Assert.AreEqual(2,Claims.GetClaimOrdinal(new Claim { PatNum=PAT_NUM,InsSubNum=99,ClaimType="S" },listPatPlans));
			Assert.AreEqual(0,Claims.GetClaimOrdinal(new Claim { PatNum=PAT_NUM,InsSubNum=99,ClaimType="Other" },listPatPlans));
			Assert.AreEqual(0,Claims.GetClaimOrdinal(new Claim { PatNum=PAT_NUM,InsSubNum=99,ClaimType="Cap" },null));
			Assert.AreEqual(0,Claims.GetClaimOrdinal(null,listPatPlans));
			Assert.AreEqual(1,Claims.GetClaimOrdinal(new Claim { PatNum=PAT_NUM+1,InsSubNum=23,ClaimType="P" },listPatPlans),
				"Another patient's patplan on the same subscriber is not this claim's insurance.");
		}

		[TestMethod]
		public void GetClaimPlace_DroppedPlansGoJustBeforeTheirOrdinal() {
			List<PatPlan> listPatPlans=new List<PatPlan> {
				new PatPlan { PatNum=PAT_NUM,InsSubNum=21,Ordinal=1 },
				new PatPlan { PatNum=PAT_NUM,InsSubNum=22,Ordinal=2 },
				new PatPlan { PatNum=PAT_NUM,InsSubNum=23,Ordinal=3 },
			};
			Assert.AreEqual(4,Claims.GetClaimPlace(new Claim { PatNum=PAT_NUM,InsSubNum=21,ClaimType="S" },listPatPlans),"The patplan decides.");
			Assert.AreEqual(12,Claims.GetClaimPlace(new Claim { PatNum=PAT_NUM,InsSubNum=23,ClaimType="Other" },listPatPlans));
			Assert.AreEqual(2,Claims.GetClaimPlace(new Claim { PatNum=PAT_NUM,InsSubNum=99,ClaimType="P" },listPatPlans),
				"A dropped primary plan: before today's ordinal 1.");
			Assert.AreEqual(6,Claims.GetClaimPlace(new Claim { PatNum=PAT_NUM,InsSubNum=99,ClaimType="S" },listPatPlans),
				"A dropped secondary plan: after today's ordinal 1, before today's ordinal 2.");
			Assert.AreEqual(0,Claims.GetClaimPlace(new Claim { PatNum=PAT_NUM,InsSubNum=99,ClaimType="Other" },listPatPlans),"Unknown.");
			Assert.AreEqual(0,Claims.GetClaimPlace(null,listPatPlans));
			Assert.AreEqual(2,Claims.GetClaimPlace(new Claim { PatNum=PAT_NUM,InsSubNum=21,ClaimType="P" },null),"No patplans loaded: the plan counts as dropped.");
		}

		[TestMethod]
		public void IsSecondaryAfterPrimary_OnlyThatPair() {
			Assert.IsTrue(Claims.IsSecondaryAfterPrimary(new Claim { ClaimType="P" },new Claim { ClaimType="S" }));
			Assert.IsFalse(Claims.IsSecondaryAfterPrimary(new Claim { ClaimType="S" },new Claim { ClaimType="P" }));
			Assert.IsFalse(Claims.IsSecondaryAfterPrimary(new Claim { ClaimType="S" },new Claim { ClaimType="Other" }));
			Assert.IsFalse(Claims.IsSecondaryAfterPrimary(new Claim { ClaimType="P" },new Claim { ClaimType="Other" }));
			Assert.IsFalse(Claims.IsSecondaryAfterPrimary(null,new Claim { ClaimType="S" }));
			Assert.IsFalse(Claims.IsSecondaryAfterPrimary(new Claim { ClaimType="P" },null));
		}

		[TestMethod]
		public void NextOrdinalStatuses() {
			CollectionAssert.AreEqual(new List<string> { "U","H" },Claims.GetNextOrdinalStatuses(false));
			CollectionAssert.AreEqual(new List<string> { "U","H","I" },Claims.GetNextOrdinalStatuses(true));
		}
	}
}
