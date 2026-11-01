// The related-claims lookup against a scratch database (Open Dental 26.1, package M2-CLAIMRCV): the query the Edit Claim prompt runs after a
// claim of any ordinal is received, the medical-to-primary branch, and the recalculation of the next insurance's claims. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace M2CLAIMRCVTests {
	[TestClass]
	public class NextOrdinalDbTests {
		[TestInitialize]
		public void TestInitialize() {
			ClaimRcvDb.Begin();
		}

		///<summary>A patient with one insurance per entry of arrayIsMedical (ordinals 1, 2, ...), and countProcs completed procedures.</summary>
		private class Family {
			public Patient Patient;
			public List<InsPlan> ListInsPlans=new List<InsPlan>();
			public List<InsSub> ListInsSubs=new List<InsSub>();
			public List<Procedure> ListProcedures=new List<Procedure>();

			public Family(int countProcs,params bool[] arrayIsMedical) {
				string suffix=Guid.NewGuid().ToString("N").Substring(0,6);
				Patient=PatientT.CreatePatient(suffix,lName:"Rcv",fName:"Test");
				for(int i=0;i<arrayIsMedical.Length;i++) {
					Carrier carrier=CarrierT.CreateCarrier("M2CLAIMRCV carrier "+suffix+" "+(i+1));
					InsPlan insPlan=InsPlanT.CreateInsPlan(carrier.CarrierNum,isMedical:arrayIsMedical[i]);
					InsSub insSub=InsSubT.CreateInsSub(Patient.PatNum,insPlan.PlanNum);
					PatPlanT.CreatePatPlan((byte)(i+1),Patient.PatNum,insSub.InsSubNum);
					ListInsPlans.Add(insPlan);
					ListInsSubs.Add(insSub);
				}
				for(int i=0;i<countProcs;i++) {
					ListProcedures.Add(ProcedureT.CreateProcedure(Patient,"Z97"+(10+i),ProcStat.C,"",100));
				}
			}

			public static Family Dental(int countPlans,int countProcs=1) {
				return new Family(countProcs,Enumerable.Repeat(false,countPlans).ToArray());
			}

			///<summary>Inserts a claim on the insurance with this ordinal (1-based), with a claimproc per procedure index.</summary>
			public Claim AddClaim(int ordinal,string claimType,string claimStatus,ClaimProcStatus claimProcStatus,params int[] arrayProcIndexes) {
				InsSub insSub=ListInsSubs[ordinal-1];
				Claim claim=new Claim {
					PatNum=Patient.PatNum,
					PlanNum=insSub.PlanNum,
					InsSubNum=insSub.InsSubNum,
					ClaimType=claimType,
					ClaimStatus=claimStatus,
					DateService=DateTime.Today,
					ClaimFee=999,
					MedType=ListInsPlans[ordinal-1].IsMedical ? EnumClaimMedType.Medical : EnumClaimMedType.Dental,
				};
				Claims.Insert(claim);
				for(int i=0;i<arrayProcIndexes.Length;i++) {
					Procedure procedure=ListProcedures[arrayProcIndexes[i]];
					ClaimProcs.Insert(new ClaimProc {
						ClaimNum=claim.ClaimNum,
						ProcNum=procedure.ProcNum,
						PatNum=Patient.PatNum,
						PlanNum=insSub.PlanNum,
						InsSubNum=insSub.InsSubNum,
						ProvNum=procedure.ProvNum,
						ProcDate=procedure.ProcDate,
						DateCP=procedure.ProcDate,
						FeeBilled=procedure.ProcFee,
						Status=claimProcStatus,
					});
				}
				return claim;
			}
		}

		private static List<long> Nums(List<Claim> listClaims) {
			return listClaims.Select(x => x.ClaimNum).OrderBy(x => x).ToList();
		}

		[TestMethod]
		public void SecondaryReceived_PromptQueryReturnsTheTertiaryClaim() {
			Family family=Family.Dental(3,2);
			family.AddClaim(1,"P","R",ClaimProcStatus.Received,0,1);
			Claim claimSecondary=family.AddClaim(2,"S","R",ClaimProcStatus.Received,0,1);
			Claim claimTertiary=family.AddClaim(3,"Other","H",ClaimProcStatus.NotReceived,0,1);
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForClaim(claimSecondary.ClaimNum);
			CollectionAssert.AreEqual(new List<long> { claimTertiary.ClaimNum },Nums(Claims.GetNextOrdinalClaimsNotReceived(listClaimProcs)));
			//The prompt (ClaimL.PromptForPrimaryOrSecondaryClaim) and every 24.3 caller go through this method.
			CollectionAssert.AreEqual(new List<long> { claimTertiary.ClaimNum },Nums(Claims.GetPrimaryOrSecondaryClaimsNotReceived(listClaimProcs)));
		}

		[TestMethod]
		public void PrimaryReceived_ReturnsOnlyTheSecondaryClaim() {
			Family family=Family.Dental(3);
			Claim claimPrimary=family.AddClaim(1,"P","R",ClaimProcStatus.Received,0);
			Claim claimSecondary=family.AddClaim(2,"S","H",ClaimProcStatus.NotReceived,0);
			family.AddClaim(3,"Other","U",ClaimProcStatus.NotReceived,0);
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForClaim(claimPrimary.ClaimNum);
			CollectionAssert.AreEqual(new List<long> { claimSecondary.ClaimNum },Nums(Claims.GetPrimaryOrSecondaryClaimsNotReceived(listClaimProcs)));
		}

		[TestMethod]
		public void PrimaryPlanDropped_SecondaryClaimIsStillOfferedAndRecalculated() {
			Family family=Family.Dental(3);
			Claim claimPrimary=family.AddClaim(1,"P","R",ClaimProcStatus.Received,0);
			Claim claimSecondary=family.AddClaim(2,"S","H",ClaimProcStatus.NotReceived,0);
			Claim claimTertiary=family.AddClaim(3,"Other","H",ClaimProcStatus.NotReceived,0);
			//The office drops the primary plan (FormInsPlan's Drop) while its claim is still outstanding; the later plans move up one ordinal.
			PatPlan patPlanPrimary=PatPlans.Refresh(family.Patient.PatNum).Find(x => x.InsSubNum==family.ListInsSubs[0].InsSubNum);
			PatPlans.Delete(patPlanPrimary.PatPlanNum);
			List<PatPlan> listPatPlans=PatPlans.Refresh(family.Patient.PatNum);
			Assert.AreEqual(2,listPatPlans.Count);
			Assert.AreEqual(1,listPatPlans.Find(x => x.InsSubNum==family.ListInsSubs[1].InsSubNum).Ordinal,"The secondary plan is ordinal 1 now.");
			Assert.AreEqual(2,listPatPlans.Find(x => x.InsSubNum==family.ListInsSubs[2].InsSubNum).Ordinal);
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForClaim(claimPrimary.ClaimNum);
			Assert.IsTrue(listClaimProcs.Count>0,"The dropped plan's claim keeps its claimprocs.");
			//Edit Claim's prompt, the ERA claim payment window and the ERA automation (which moves these claims to Waiting to Send) use this lookup.
			CollectionAssert.AreEqual(new List<long> { claimSecondary.ClaimNum },Nums(Claims.GetPrimaryOrSecondaryClaimsNotReceived(listClaimProcs)),
				"As in 24.3, the secondary claim is offered after the dropped primary claim is received; the tertiary claim waits.");
			Claims.CalculateAndUpdateSecondariesFromPrimaries(new List<Claim> { Claims.GetClaim(claimPrimary.ClaimNum) });
			Assert.AreEqual(100,Claims.GetClaim(claimSecondary.ClaimNum).ClaimFee,0.001,"The secondary claim's totals were recalculated.");
			Assert.AreEqual(999,Claims.GetClaim(claimTertiary.ClaimNum).ClaimFee,0.001,"The tertiary claim waits for the secondary claim.");
		}

		[TestMethod]
		public void SecondaryPlanDropped_SecondaryClaimFirstThenTheTertiary() {
			Family family=Family.Dental(3);
			Claim claimPrimary=family.AddClaim(1,"P","R",ClaimProcStatus.Received,0);
			Claim claimSecondary=family.AddClaim(2,"S","H",ClaimProcStatus.NotReceived,0);
			Claim claimTertiary=family.AddClaim(3,"Other","H",ClaimProcStatus.NotReceived,0);
			//The secondary plan is dropped while its claim is outstanding: the tertiary plan moves to ordinal 2, the dropped secondary claim's fallback.
			PatPlan patPlanSecondary=PatPlans.Refresh(family.Patient.PatNum).Find(x => x.InsSubNum==family.ListInsSubs[1].InsSubNum);
			PatPlans.Delete(patPlanSecondary.PatPlanNum);
			Assert.AreEqual(2,PatPlans.Refresh(family.Patient.PatNum).Find(x => x.InsSubNum==family.ListInsSubs[2].InsSubNum).Ordinal,
				"The tertiary plan is ordinal 2 now.");
			CollectionAssert.AreEqual(new List<long> { claimSecondary.ClaimNum },
				Nums(Claims.GetPrimaryOrSecondaryClaimsNotReceived(ClaimProcs.RefreshForClaim(claimPrimary.ClaimNum))),
				"Only the secondary claim is offered; the tertiary claim waits for it.");
			//The secondary claim is received.
			Claim claimSecondaryOld=Claims.GetClaim(claimSecondary.ClaimNum);
			Claim claimSecondaryReceived=claimSecondaryOld.Copy();
			claimSecondaryReceived.ClaimStatus="R";
			Claims.Update(claimSecondaryReceived,claimSecondaryOld);
			List<ClaimProc> listClaimProcsSecondary=ClaimProcs.RefreshForClaim(claimSecondary.ClaimNum);
			for(int i=0;i<listClaimProcsSecondary.Count;i++) {
				ClaimProc claimProcOld=listClaimProcsSecondary[i].Copy();
				listClaimProcsSecondary[i].Status=ClaimProcStatus.Received;
				ClaimProcs.Update(listClaimProcsSecondary[i],claimProcOld);
			}
			CollectionAssert.AreEqual(new List<long> { claimTertiary.ClaimNum },
				Nums(Claims.GetPrimaryOrSecondaryClaimsNotReceived(ClaimProcs.RefreshForClaim(claimSecondary.ClaimNum))),
				"Receiving the dropped plan's secondary claim offers the tertiary claim.");
		}

		[TestMethod]
		public void FifthInsurance_AfterTheFourth() {
			Family family=Family.Dental(5);
			for(int ordinal=1;ordinal<=3;ordinal++) {
				family.AddClaim(ordinal,ordinal==1 ? "P" : (ordinal==2 ? "S" : "Other"),"R",ClaimProcStatus.Received,0);
			}
			Claim claimFourth=family.AddClaim(4,"Other","R",ClaimProcStatus.Received,0);
			Claim claimFifth=family.AddClaim(5,"Other","U",ClaimProcStatus.NotReceived,0);
			CollectionAssert.AreEqual(new List<long> { claimFifth.ClaimNum },
				Nums(Claims.GetNextOrdinalClaimsNotReceived(ClaimProcs.RefreshForClaim(claimFourth.ClaimNum))));
		}

		[TestMethod]
		public void NextInsuranceSent_NothingIsOffered() {
			Family family=Family.Dental(3);
			Claim claimPrimary=family.AddClaim(1,"P","R",ClaimProcStatus.Received,0);
			family.AddClaim(2,"S","S",ClaimProcStatus.NotReceived,0);
			family.AddClaim(3,"Other","H",ClaimProcStatus.NotReceived,0);
			Assert.AreEqual(0,Claims.GetPrimaryOrSecondaryClaimsNotReceived(ClaimProcs.RefreshForClaim(claimPrimary.ClaimNum)).Count);
		}

		[TestMethod]
		public void MedicalReceived_PrimaryBranchIsUnchanged() {
			//Medical insurance first, then dental primary and secondary. ClaimMedReceivedPromptForPrimaryClaim looks for primary (type P) claims.
			Family family=new Family(1,true,false,false);
			Claim claimMedical=family.AddClaim(1,"P","R",ClaimProcStatus.Received,0);
			Claim claimDentalPrimary=family.AddClaim(2,"P","H",ClaimProcStatus.NotReceived,0);
			family.AddClaim(3,"S","H",ClaimProcStatus.NotReceived,0);
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForClaim(claimMedical.ClaimNum);
			CollectionAssert.AreEqual(new List<long> { claimDentalPrimary.ClaimNum },
				Nums(Claims.GetPrimaryOrSecondaryClaimsNotReceived(listClaimProcs,isSecondaryClaim:false)));
			//With the medical prompt off, the next-insurance prompt also lands on the dental primary claim, which comes next.
			CollectionAssert.AreEqual(new List<long> { claimDentalPrimary.ClaimNum },Nums(Claims.GetPrimaryOrSecondaryClaimsNotReceived(listClaimProcs)));
		}

		[TestMethod]
		public void HoldForInProcess_OnlyForRecalculation() {
			Family family=Family.Dental(2);
			Claim claimPrimary=family.AddClaim(1,"P","R",ClaimProcStatus.Received,0);
			Claim claimSecondary=family.AddClaim(2,"S","I",ClaimProcStatus.NotReceived,0);
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForClaim(claimPrimary.ClaimNum);
			Assert.AreEqual(0,Claims.GetPrimaryOrSecondaryClaimsNotReceived(listClaimProcs).Count);
			CollectionAssert.AreEqual(new List<long> { claimSecondary.ClaimNum },
				Nums(Claims.GetNextOrdinalClaimsNotReceived(listClaimProcs,includeHoldForInProcess:true)));
		}

		[TestMethod]
		public void RecalcAfterSecondary_UpdatesTheTertiaryClaimOnly() {
			Family family=Family.Dental(4);
			family.AddClaim(1,"P","R",ClaimProcStatus.Received,0);
			Claim claimSecondary=family.AddClaim(2,"S","R",ClaimProcStatus.Received,0);
			Claim claimTertiary=family.AddClaim(3,"Other","H",ClaimProcStatus.NotReceived,0);
			Claim claimFourth=family.AddClaim(4,"Other","H",ClaimProcStatus.NotReceived,0);
			//Claims.ReceiveAsNoPaymentIfNeeded and the ERA and Outstanding Insurance paths call this with the received claims.
			Claims.CalculateAndUpdateSecondariesFromPrimaries(new List<Claim> { Claims.GetClaim(claimSecondary.ClaimNum) });
			Assert.AreEqual(100,Claims.GetClaim(claimTertiary.ClaimNum).ClaimFee,0.001,"The tertiary claim's totals were recalculated.");
			Assert.AreEqual(999,Claims.GetClaim(claimFourth.ClaimNum).ClaimFee,0.001,"The fourth insurance waits for the tertiary claim.");
		}

		[TestMethod]
		public void ZeroClaims_SecondaryRecalculatesTheTertiary() {
			Family family=Family.Dental(3);
			family.AddClaim(1,"P","R",ClaimProcStatus.Received,0);
			Claim claimSecondary=family.AddClaim(2,"S","S",ClaimProcStatus.NotReceived,0);
			Claim claimTertiary=family.AddClaim(3,"Other","H",ClaimProcStatus.NotReceived,0);
			bool isRecalcOld=PrefC.GetBool(PrefName.ClaimPrimaryReceivedRecalcSecondary);
			try {
				Prefs.UpdateBool(PrefName.ClaimPrimaryReceivedRecalcSecondary,true);
				RpOutstandingIns.ZeroClaims(new List<Claim> { Claims.GetClaim(claimSecondary.ClaimNum) });
				Assert.AreEqual("R",Claims.GetClaim(claimSecondary.ClaimNum).ClaimStatus);
				Assert.AreEqual(100,Claims.GetClaim(claimTertiary.ClaimNum).ClaimFee,0.001,"Zeroing a secondary claim recalculates the tertiary claim (26.1).");
				Assert.AreEqual("H",Claims.GetClaim(claimTertiary.ClaimNum).ClaimStatus,"Silent path: the status is not changed.");
			}
			finally {
				Prefs.UpdateBool(PrefName.ClaimPrimaryReceivedRecalcSecondary,isRecalcOld);
			}
		}

		[TestMethod]
		public void ReceiveAsNoPayment_SecondaryRecalculatesTheTertiary() {
			Family family=Family.Dental(3);
			family.AddClaim(1,"P","R",ClaimProcStatus.Received,0);
			InsSub insSubSecondary=family.ListInsSubs[1];
			insSubSecondary.AssignBen=false;
			InsSubs.Update(insSubSecondary);
			Claim claimSecondary=family.AddClaim(2,"S","S",ClaimProcStatus.NotReceived,0);
			Claim claimTertiary=family.AddClaim(3,"Other","H",ClaimProcStatus.NotReceived,0);
			bool isAutoReceiveOld=PrefC.GetBool(PrefName.InsAutoReceiveNoAssign);
			bool isRecalcOld=PrefC.GetBool(PrefName.ClaimPrimaryReceivedRecalcSecondary);
			try {
				Prefs.UpdateBool(PrefName.InsAutoReceiveNoAssign,true);
				Prefs.UpdateBool(PrefName.ClaimPrimaryReceivedRecalcSecondary,true);
				Assert.IsTrue(Claims.ReceiveAsNoPaymentIfNeeded(claimSecondary.ClaimNum),"The secondary claim has no assignment of benefits.");
				Assert.AreEqual("R",Claims.GetClaim(claimSecondary.ClaimNum).ClaimStatus);
				Assert.AreEqual(100,Claims.GetClaim(claimTertiary.ClaimNum).ClaimFee,0.001,
					"26.1: a secondary claim received with no payment recalculates the tertiary claim (24.3 did this only after a primary claim).");
			}
			finally {
				Prefs.UpdateBool(PrefName.InsAutoReceiveNoAssign,isAutoReceiveOld);
				Prefs.UpdateBool(PrefName.ClaimPrimaryReceivedRecalcSecondary,isRecalcOld);
			}
		}
	}
}
