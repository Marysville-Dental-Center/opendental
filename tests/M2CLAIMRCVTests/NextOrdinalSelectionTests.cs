// Claims.SelectNextOrdinalClaims and Claims.GetClaimOrdinal on in-memory rows (no database): which unsent claims the related-claims prompt offers
// after a claim of any ordinal is received (Open Dental 26.1). Synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

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
			//The primary claim's plan was dropped and is no longer a patplan: a primary claim counts as ordinal 1.
			Claim claimPrimaryDropped=rows.AddClaimForSub(77,"P","R",ClaimProcStatus.Received,PAT_NUM,PROC_A);
			Claim claimSecondary=rows.AddClaim(2,"S","H",ClaimProcStatus.NotReceived,PROC_A);
			AssertClaims(rows.Select(claimPrimaryDropped),"Dropped primary plan.",claimSecondary);
			Rows rowsOther=new Rows(1);
			Claim claimOtherDropped=rowsOther.AddClaimForSub(78,"Other","R",ClaimProcStatus.Received,PAT_NUM,PROC_A);
			rowsOther.AddClaim(1,"P","U",ClaimProcStatus.NotReceived,PROC_A);
			AssertClaims(rowsOther.Select(claimOtherDropped),"An 'Other' claim whose plan is gone has no known ordinal, so nothing is offered.");
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
		public void NextOrdinalStatuses() {
			CollectionAssert.AreEqual(new List<string> { "U","H" },Claims.GetNextOrdinalStatuses(false));
			CollectionAssert.AreEqual(new List<string> { "U","H","I" },Claims.GetNextOrdinalStatuses(true));
		}
	}
}
