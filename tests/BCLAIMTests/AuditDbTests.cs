// Database tests for the B-CLAIM audit entries: Ins Est Total and Allowed overrides (v25_1-misc-claimproc-override-audit) from every business
// save path the fork hooks, and Fee Billed / Code Sent under ClaimProcFieldsBilledToInsEdit (v25_3-security-claimproc-billedtoins).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BCLAIMTests {
	[TestClass]
	public class AuditDbTests {
		[TestInitialize]
		public void TestInitialize() {
			BClaimDb.Begin();
		}

		[TestMethod]
		public void Overrides_EstimateLogsUnderProcEditWithCodeToothAndCarrier() {
			ClaimScenario scenario=ClaimScenario.Create("Z9711");
			ClaimProc claimProc=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanDental,scenario.InsSubNumDental,0,ClaimProcStatus.Estimate);
			ClaimProc claimProcOld=claimProc.Copy();
			claimProc.InsEstTotalOverride=55;
			claimProc.AllowedOverride=90;
			ClaimProcs.Update(claimProc,claimProcOld);
			ClaimProcs.CreateAuditTrailEntryForOverrides(claimProc,claimProcOld);
			List<SecurityLog> listLogs=BClaimDb.GetLogs(EnumPermType.ProcEdit,claimProc.ClaimProcNum);
			Assert.AreEqual(1,listLogs.Count,"One entry per claimproc per save.");
			Assert.AreEqual(scenario.Patient_.PatNum,listLogs[0].PatNum);
			StringAssert.Contains(listLogs[0].LogText,"Procedure Z9711");
			StringAssert.Contains(listLogs[0].LogText,"tooth 3");
			StringAssert.Contains(listLogs[0].LogText,scenario.CarrierNameDental);
			StringAssert.Contains(listLogs[0].LogText,"Ins Est Total override changed from blank to $55.00.");
			StringAssert.Contains(listLogs[0].LogText,"Allowed override changed from blank to $90.00.");
			Assert.AreEqual(0,BClaimDb.GetLogs(EnumPermType.ClaimProcReceivedEdit,claimProc.ClaimProcNum).Count);
		}

		[TestMethod]
		public void Overrides_ReceivedLogsUnderClaimProcReceivedEdit() {
			ClaimScenario scenario=ClaimScenario.Create("Z9712");
			Claim claim=BClaimDb.CreateClaim(scenario.Patient_,scenario.PlanDental,scenario.InsSubNumDental,"P",EnumClaimMedType.Dental,"R",
				scenario.Procedure_.ProcDate);
			ClaimProc claimProc=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanDental,scenario.InsSubNumDental,claim.ClaimNum,
				ClaimProcStatus.Received);
			claimProc.AllowedOverride=80;
			ClaimProcs.Update(claimProc);
			ClaimProc claimProcOld=claimProc.Copy();
			claimProc.AllowedOverride=-1;
			ClaimProcs.CreateAuditTrailEntryForOverrides(claimProc,claimProcOld);
			List<SecurityLog> listLogs=BClaimDb.GetLogs(EnumPermType.ClaimProcReceivedEdit,claimProc.ClaimProcNum);
			Assert.AreEqual(1,listLogs.Count);
			StringAssert.Contains(listLogs[0].LogText,"Allowed override changed from $80.00 to blank.");
		}

		[TestMethod]
		public void Overrides_NothingWhenUnchangedNewOrMismatched() {
			ClaimScenario scenario=ClaimScenario.Create("Z9713");
			ClaimProc claimProc=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanDental,scenario.InsSubNumDental,0,ClaimProcStatus.Estimate);
			ClaimProc claimProcOld=claimProc.Copy();
			claimProc.CopayOverride=10;//Not one of the audited overrides.
			ClaimProcs.CreateAuditTrailEntryForOverrides(claimProc,claimProcOld);
			ClaimProc claimProcOther=claimProc.Copy();
			claimProcOther.ClaimProcNum=claimProc.ClaimProcNum+100000;
			claimProcOther.InsEstTotalOverride=5;
			ClaimProcs.CreateAuditTrailEntryForOverrides(claimProcOther,claimProcOld);//Not the same claimproc.
			ClaimProc claimProcNew=new ClaimProc { PatNum=scenario.Patient_.PatNum,InsEstTotalOverride=5,AllowedOverride=-1 };
			ClaimProcs.CreateAuditTrailEntriesForOverrides(new List<ClaimProc> { claimProcNew },new List<ClaimProc> { claimProcOld });//Not saved yet.
			Assert.AreEqual(0,BClaimDb.GetLogsForPatient(EnumPermType.ProcEdit,scenario.Patient_.PatNum).Count);
		}

		[TestMethod]
		public void Overrides_PaymentAuditPathLogsThem() {
			//FormClaimPayTotal and the "no assignment of benefits" auto-receive call CreateAuditTrailEntryForClaimProcPayment. Manual ERA posting
			//calls it too, but with isPaymentFromERA, and audits the overrides against the database instead (tests below).
			ClaimScenario scenario=ClaimScenario.Create("Z9714");
			Claim claim=BClaimDb.CreateClaim(scenario.Patient_,scenario.PlanDental,scenario.InsSubNumDental,"P",EnumClaimMedType.Dental,"S",
				scenario.Procedure_.ProcDate);
			ClaimProc claimProc=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanDental,scenario.InsSubNumDental,claim.ClaimNum,
				ClaimProcStatus.NotReceived);
			ClaimProc claimProcOld=claimProc.Copy();
			claimProc.Status=ClaimProcStatus.Received;
			claimProc.InsPayAmt=70;
			claimProc.AllowedOverride=100;
			ClaimProcs.CreateAuditTrailEntryForClaimProcPayment(claimProc,claimProcOld,isInsPayCreate:true);
			List<SecurityLog> listLogs=BClaimDb.GetLogs(EnumPermType.ClaimProcReceivedEdit,claimProc.ClaimProcNum);
			Assert.AreEqual(1,listLogs.Count);
			StringAssert.Contains(listLogs[0].LogText,"Allowed override changed from blank to $100.00.");
			//The existing payment entry is still written.
			Assert.IsTrue(BClaimDb.GetLogsForPatient(EnumPermType.InsPayCreate,scenario.Patient_.PatNum).Count>=1);
		}

		[TestMethod]
		public void Overrides_PreAuthEstimatesLogged() {
			//Entering a preauthorization's estimates sets the Ins Est Total override of the procedure's estimate.
			ClaimScenario scenario=ClaimScenario.Create("Z9715");
			ClaimProc claimProcEst=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanDental,scenario.InsSubNumDental,0,ClaimProcStatus.Estimate);
			List<ClaimProc> listClaimProcs=new List<ClaimProc> { claimProcEst };
			ClaimProcs.SetInsEstTotalOverride(scenario.Procedure_.ProcNum,scenario.PlanDental.PlanNum,scenario.InsSubNumDental,65,listClaimProcs);
			List<SecurityLog> listLogs=BClaimDb.GetLogs(EnumPermType.ProcEdit,claimProcEst.ClaimProcNum);
			Assert.AreEqual(1,listLogs.Count);
			StringAssert.Contains(listLogs[0].LogText,"Ins Est Total override changed from blank to $65.00.");
			Assert.AreEqual(65,ClaimProcs.GetOneClaimProc(claimProcEst.ClaimProcNum).InsEstTotalOverride);
			//The same value again changes nothing and logs nothing.
			ClaimProcs.SetInsEstTotalOverride(scenario.Procedure_.ProcNum,scenario.PlanDental.PlanNum,scenario.InsSubNumDental,65,listClaimProcs);
			Assert.AreEqual(1,BClaimDb.GetLogs(EnumPermType.ProcEdit,claimProcEst.ClaimProcNum).Count);
		}

		[TestMethod]
		public void Overrides_ListMatchesByClaimProcNum() {
			ClaimScenario scenario=ClaimScenario.Create("Z9716");
			ClaimProc claimProcA=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanDental,scenario.InsSubNumDental,0,ClaimProcStatus.Estimate);
			ClaimProc claimProcB=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanMedical,scenario.InsSubNumMedical,0,ClaimProcStatus.Estimate);
			List<ClaimProc> listOld=new List<ClaimProc> { claimProcB.Copy(),claimProcA.Copy() };//Different order on purpose.
			claimProcA.AllowedOverride=30;
			List<ClaimProc> listNew=new List<ClaimProc> { claimProcA,claimProcB };
			ClaimProcs.CreateAuditTrailEntriesForOverrides(listNew,listOld);
			Assert.AreEqual(1,BClaimDb.GetLogs(EnumPermType.ProcEdit,claimProcA.ClaimProcNum).Count);
			Assert.AreEqual(0,BClaimDb.GetLogs(EnumPermType.ProcEdit,claimProcB.ClaimProcNum).Count);
		}

		///<summary>What FormEtrans835ClaimPay.butSave_Click does with the claimprocs, in the same order: Etranss.ReceiveEraPayment saves them, the
		///override audit compares them with the copies read from the database in Load, then the payment audit runs against the Load-time copies
		///of ListClaimProcs (isPaymentFromERA), which no longer audits overrides itself.</summary>
		private static void PostLikeFormEtrans835ClaimPay(Claim claim,InsPlan insPlan,List<ClaimProc> listClaimProcs,List<ClaimProc> listClaimProcsOld,
			List<ClaimProc> listClaimProcsDb,bool isSupplementalPay)
		{
			Etranss.ReceiveEraPayment(claim,new Hx835_Claim { DateReceived=DateTime.Today },listClaimProcs,true,isSupplementalPay,insPlan);
			ClaimProcs.CreateAuditTrailEntriesForOverrides(listClaimProcs,listClaimProcsDb);
			for(int i=0;i<listClaimProcs.Count;i++) {
				ClaimProc claimProcOld=listClaimProcsOld.FirstOrDefault(x => x.ClaimProcNum==listClaimProcs[i].ClaimProcNum);
				ClaimProcs.CreateAuditTrailEntryForClaimProcPayment(listClaimProcs[i],claimProcOld,isInsPayCreate:true,isPaymentFromERA:true);
			}
		}

		[TestMethod]
		public void Overrides_ManualEraPostingLogsTheEraAllowedAmountOnce() {
			ClaimScenario scenario=ClaimScenario.Create("Z9720");
			Claim claim=BClaimDb.CreateClaim(scenario.Patient_,scenario.PlanDental,scenario.InsSubNumDental,"P",EnumClaimMedType.Dental,"S",
				scenario.Procedure_.ProcDate);
			ClaimProc claimProcSaved=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanDental,scenario.InsSubNumDental,claim.ClaimNum,
				ClaimProcStatus.NotReceived);//Saved with AllowedOverride -1.
			//EtransL.ImportEraClaimData: working copies of the claim's claimprocs, which Etranss.TryImportEraClaimData(isAutomatic:false) fills
			//with the 835 amounts in memory.
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForClaims(new List<long> { claim.ClaimNum }).Select(x => x.Copy()).ToList();
			ClaimProc claimProc=listClaimProcs.Single(x => x.ClaimProcNum==claimProcSaved.ClaimProcNum);
			claimProc.Status=ClaimProcStatus.Received;
			claimProc.AllowedOverride=80;
			claimProc.InsPayAmt=60;
			claimProc.DateEntry=DateTime.Now;
			claimProc.DateCP=DateTime.Today;
			//FormEtrans835ClaimPay_Load: the old copies already hold the ERA values; the database copies do not.
			List<ClaimProc> listClaimProcsOld=listClaimProcs.Select(x => x.Copy()).ToList();
			List<ClaimProc> listClaimProcsDb=ClaimProcs.RefreshForClaims(new List<long> { claim.ClaimNum });
			Assert.AreEqual(-1,listClaimProcsDb.Single(x => x.ClaimProcNum==claimProc.ClaimProcNum).AllowedOverride);
			PostLikeFormEtrans835ClaimPay(claim,scenario.PlanDental,listClaimProcs,listClaimProcsOld,listClaimProcsDb,isSupplementalPay:false);
			Assert.AreEqual(80,ClaimProcs.GetOneClaimProc(claimProc.ClaimProcNum).AllowedOverride,"The ERA allowed amount was saved.");
			List<SecurityLog> listLogs=BClaimDb.GetLogs(EnumPermType.ClaimProcReceivedEdit,claimProc.ClaimProcNum);
			Assert.AreEqual(1,listLogs.Count,"Exactly one override entry for the claimproc.");
			StringAssert.Contains(listLogs[0].LogText,"Allowed override changed from blank to $80.00.");
			Assert.AreEqual(0,BClaimDb.GetLogs(EnumPermType.ProcEdit,claimProc.ClaimProcNum).Count);
			//The existing ERA payment entry is still written.
			Assert.IsTrue(BClaimDb.GetLogsForPatient(EnumPermType.InsPayCreate,scenario.Patient_.PatNum)
				.Any(x => x.LogText.Contains("(Payment from ERA)")));
		}

		[TestMethod]
		public void Overrides_ManualEraPostingWithRetypedAllowedLogsOnceAgainstTheDatabase() {
			ClaimScenario scenario=ClaimScenario.Create("Z9721");
			Claim claim=BClaimDb.CreateClaim(scenario.Patient_,scenario.PlanDental,scenario.InsSubNumDental,"P",EnumClaimMedType.Dental,"S",
				scenario.Procedure_.ProcDate);
			ClaimProc claimProcSaved=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanDental,scenario.InsSubNumDental,claim.ClaimNum,
				ClaimProcStatus.NotReceived);
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForClaims(new List<long> { claim.ClaimNum }).Select(x => x.Copy()).ToList();
			ClaimProc claimProc=listClaimProcs.Single(x => x.ClaimProcNum==claimProcSaved.ClaimProcNum);
			claimProc.Status=ClaimProcStatus.Received;
			claimProc.AllowedOverride=80;//From the ERA.
			claimProc.InsPayAmt=60;
			List<ClaimProc> listClaimProcsOld=listClaimProcs.Select(x => x.Copy()).ToList();
			List<ClaimProc> listClaimProcsDb=ClaimProcs.RefreshForClaims(new List<long> { claim.ClaimNum });
			claimProc.AllowedOverride=75;//Retyped by the user in the grid.
			PostLikeFormEtrans835ClaimPay(claim,scenario.PlanDental,listClaimProcs,listClaimProcsOld,listClaimProcsDb,isSupplementalPay:false);
			List<SecurityLog> listLogs=BClaimDb.GetLogs(EnumPermType.ClaimProcReceivedEdit,claimProc.ClaimProcNum);
			Assert.AreEqual(1,listLogs.Count,"The payment audit no longer adds a second entry for the retyped value.");
			StringAssert.Contains(listLogs[0].LogText,"Allowed override changed from blank to $75.00.");
			Assert.IsFalse(listLogs[0].LogText.Contains("$80.00"),"Compared with the database, not with the ERA value.");
		}

		[TestMethod]
		public void Overrides_ManualEraSupplementalPostingLogsThePreInsertedClaimProc() {
			ClaimScenario scenario=ClaimScenario.Create("Z9722");
			Claim claim=BClaimDb.CreateClaim(scenario.Patient_,scenario.PlanDental,scenario.InsSubNumDental,"P",EnumClaimMedType.Dental,"R",
				scenario.Procedure_.ProcDate);
			ClaimProc claimProcReceived=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanDental,scenario.InsSubNumDental,claim.ClaimNum,
				ClaimProcStatus.Received);
			claimProcReceived.InsPayAmt=50;
			ClaimProcs.Update(claimProcReceived);
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForClaims(new List<long> { claim.ClaimNum }).Select(x => x.Copy()).ToList();
			//Etranss.TryImportEraClaimData pre-inserts the supplemental claimprocs (copies of the received ones), then fills them in memory.
			List<ClaimProc> listClaimProcsSupp=ClaimProcs.CreateSuppClaimProcs(listClaimProcs.Select(x => x.Copy()).ToList(),false,false);
			Assert.AreEqual(1,listClaimProcsSupp.Count);
			listClaimProcs.AddRange(listClaimProcsSupp);
			ClaimProc claimProcSupp=listClaimProcsSupp[0];
			claimProcSupp.AllowedOverride=20;
			claimProcSupp.InsPayAmt=15;
			claimProcSupp.FeeBilled=0;
			List<ClaimProc> listClaimProcsOld=listClaimProcs.Select(x => x.Copy()).ToList();
			List<ClaimProc> listClaimProcsDb=ClaimProcs.RefreshForClaims(new List<long> { claim.ClaimNum });
			Assert.IsTrue(listClaimProcsDb.Any(x => x.ClaimProcNum==claimProcSupp.ClaimProcNum),"The database copies include the pre-inserted row.");
			PostLikeFormEtrans835ClaimPay(claim,scenario.PlanDental,listClaimProcs,listClaimProcsOld,listClaimProcsDb,isSupplementalPay:true);
			List<SecurityLog> listLogs=BClaimDb.GetLogs(EnumPermType.ClaimProcReceivedEdit,claimProcSupp.ClaimProcNum);
			Assert.AreEqual(1,listLogs.Count);
			StringAssert.Contains(listLogs[0].LogText,"Allowed override changed from blank to $20.00.");
			Assert.AreEqual(0,BClaimDb.GetLogs(EnumPermType.ClaimProcReceivedEdit,claimProcReceived.ClaimProcNum).Count,
				"The received claimproc the ERA did not change gets no entry.");
		}

		[TestMethod]
		public void Overrides_EraPaymentAuditPathLeavesOverridesToTheDatabaseComparison() {
			ClaimScenario scenario=ClaimScenario.Create("Z9723");
			Claim claim=BClaimDb.CreateClaim(scenario.Patient_,scenario.PlanDental,scenario.InsSubNumDental,"P",EnumClaimMedType.Dental,"S",
				scenario.Procedure_.ProcDate);
			ClaimProc claimProc=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanDental,scenario.InsSubNumDental,claim.ClaimNum,
				ClaimProcStatus.NotReceived);
			ClaimProc claimProcOld=claimProc.Copy();
			claimProc.Status=ClaimProcStatus.Received;
			claimProc.InsPayAmt=70;
			claimProc.AllowedOverride=100;
			ClaimProcs.CreateAuditTrailEntryForClaimProcPayment(claimProc,claimProcOld,isInsPayCreate:true,isPaymentFromERA:true);
			Assert.AreEqual(0,BClaimDb.GetLogs(EnumPermType.ClaimProcReceivedEdit,claimProc.ClaimProcNum).Count);
			Assert.IsTrue(BClaimDb.GetLogsForPatient(EnumPermType.InsPayCreate,scenario.Patient_.PatNum)
				.Any(x => x.LogText.Contains("(Payment from ERA)")));
		}

		[TestMethod]
		public void BilledToIns_ChangeLoggedUnder252() {
			ClaimScenario scenario=ClaimScenario.Create("Z9717");
			Claim claim=BClaimDb.CreateClaim(scenario.Patient_,scenario.PlanDental,scenario.InsSubNumDental,"P",EnumClaimMedType.Dental,"U",
				scenario.Procedure_.ProcDate);
			ClaimProc claimProc=BClaimDb.CreateClaimProc(scenario.Procedure_,scenario.PlanDental,scenario.InsSubNumDental,claim.ClaimNum,
				ClaimProcStatus.NotReceived);
			ClaimProc claimProcOld=claimProc.Copy();
			ClaimProcs.SetBilledToInsFields(claimProc,"Z9718",140,isAuthorized:true);
			ClaimProcs.CreateAuditTrailEntryForBilledToIns(claimProc,claimProcOld);
			List<SecurityLog> listLogs=BClaimDb.GetLogs(EnumPermType.ClaimProcFieldsBilledToInsEdit,claimProc.ClaimProcNum);
			Assert.AreEqual(1,listLogs.Count);
			StringAssert.Contains(listLogs[0].LogText,"Fee billed changed from $120.00 to $140.00.");
			StringAssert.Contains(listLogs[0].LogText,"Code sent changed from Z9717 to Z9718.");
			//Nothing changed: nothing logged.
			ClaimProcs.CreateAuditTrailEntryForBilledToIns(claimProc,claimProc.Copy());
			Assert.AreEqual(1,BClaimDb.GetLogs(EnumPermType.ClaimProcFieldsBilledToInsEdit,claimProc.ClaimProcNum).Count);
		}

		[TestMethod]
		public void TotalPayment_PrefixNamesTotalPaymentAndCarrier() {
			ClaimScenario scenario=ClaimScenario.Create("Z9719");
			ClaimProc claimProc=new ClaimProc { ProcNum=0,PlanNum=scenario.PlanDental.PlanNum };
			Assert.AreEqual("Total payment, "+scenario.CarrierNameDental+". ",ClaimProcs.GetAuditTextPrefix(claimProc));
		}
	}
}
