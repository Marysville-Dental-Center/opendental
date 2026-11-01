// v25_1-account-salestax-procs-on-claims: procedures with the sales-tax code go on insurance claims like any other procedure, picked by the user or
// by Create Claim. Only the existing "Do not usually bill to insurance" rule keeps them off.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BACCTTests {
	[TestClass]
	public class SalesTaxClaimDbTests {
		[TestInitialize]
		public void TestInitialize() {
			TestDbAssembly.ConnectOrSkip();
			SalesTaxData.SetUp(10,SalesTaxData.CODE_TAX);
		}

		[TestCleanup]
		public void TestCleanup() {
			if(TestDbAssembly.SkipReason!=null) {
				return;
			}
			SalesTaxData.SetUp(0,"");
		}

		private class Insured {
			public Patient Patient;
			public InsPlan InsPlan;
			public InsSub InsSub;
			public List<PatPlan> ListPatPlans;
			public List<InsSub> ListInsSubs;
			public List<InsPlan> ListInsPlans;
		}

		private static Insured CreateInsured(string lName) {
			Insured insured=new Insured();
			insured.Patient=SalesTaxData.CreatePatient(lName);
			var insurance=SalesTaxData.AddInsurance(insured.Patient);
			insured.InsPlan=insurance.InsPlan;
			insured.InsSub=insurance.InsSub;
			insured.ListPatPlans=PatPlans.Refresh(insured.Patient.PatNum);
			insured.ListInsSubs=InsSubs.RefreshForFam(Patients.GetFamily(insured.Patient.PatNum));
			insured.ListInsPlans=InsPlans.RefreshForSubList(insured.ListInsSubs);
			return insured;
		}

		///<summary>Insurance estimates, as setting a procedure complete makes them.</summary>
		private static List<ClaimProc> ComputeEstimates(Insured insured,List<Procedure> listProcs) {
			List<ClaimProc> listClaimProcs=ClaimProcs.Refresh(insured.Patient.PatNum);
			List<Benefit> listBenefits=Benefits.Refresh(insured.ListPatPlans,insured.ListInsSubs);
			foreach(Procedure procedure in listProcs) {
				Procedures.ComputeEstimates(procedure,insured.Patient.PatNum,ref listClaimProcs,true,insured.ListInsPlans,insured.ListPatPlans,listBenefits,
					null,null,true,insured.Patient.Age,insured.ListInsSubs);
			}
			return ClaimProcs.Refresh(insured.Patient.PatNum);
		}

		[TestMethod]
		public void TaxProcedure_NeedsSending_AndGoesOnTheClaim() {
			Insured insured=CreateInsured("ClaimTax");
			Procedure procPlain=SalesTaxData.CreateProc(insured.Patient,SalesTaxData.CODE_PLAIN,100,ProcStat.C,DateTime.Today);
			Procedure procTax=SalesTaxData.CreateProc(insured.Patient,SalesTaxData.CODE_TAX,10,ProcStat.C,DateTime.Today);
			Assert.IsTrue(SalesTaxProcs.IsSalesTaxProc(procTax));
			List<Procedure> listProcs=new List<Procedure> { procPlain,procTax };
			List<ClaimProc> listClaimProcs=ComputeEstimates(insured,listProcs);
			//Create Claim with nothing highlighted picks every procedure that needs sending; the sales-tax procedure is one of them.
			Assert.IsTrue(Procedures.NeedsSent(procTax.ProcNum,insured.InsSub.InsSubNum,listClaimProcs));
			Assert.IsFalse(Procedures.NoBillIns(procTax,listClaimProcs,insured.InsPlan.PlanNum));
			Claim claim=new Claim();
			claim.DateSent=DateTime.Today;
			claim.DateSentOrig=DateTime.MinValue;
			claim.ClaimStatus="W";
			claim=AccountModules.CreateClaim(claim,"P",insured.ListPatPlans,insured.ListInsPlans,listClaimProcs,Procedures.Refresh(insured.Patient.PatNum),
				insured.ListInsSubs,insured.Patient,null,listProcs,"",insured.InsPlan,insured.InsSub,Relat.Self);
			Assert.AreNotEqual(0,claim.ClaimNum);
			List<long> listProcNumsOnClaim=ClaimProcs.RefreshForClaim(claim.ClaimNum).Select(x => x.ProcNum).Distinct().ToList();
			CollectionAssert.AreEquivalent(new List<long> { procPlain.ProcNum,procTax.ProcNum },listProcNumsOnClaim);
		}

		[TestMethod]
		public void TaxProcedure_MarkedDoNotBill_IsLeftOffByTheExistingRule() {
			//An office that does not bill tax to insurance marks the code "Do not usually bill to insurance".
			SalesTaxData.SetUp(10,SalesTaxData.CODE_NOBILL);
			Insured insured=CreateInsured("ClaimTaxNoBill");
			Procedure procTax=SalesTaxData.CreateProc(insured.Patient,SalesTaxData.CODE_NOBILL,10,ProcStat.C,DateTime.Today);
			Assert.IsTrue(SalesTaxProcs.IsSalesTaxProc(procTax));
			List<ClaimProc> listClaimProcs=ComputeEstimates(insured,new List<Procedure> { procTax });
			Assert.IsFalse(Procedures.NeedsSent(procTax.ProcNum,insured.InsSub.InsSubNum,listClaimProcs),"Create Claim does not pick it.");
			Assert.IsTrue(Procedures.NoBillIns(procTax,listClaimProcs,insured.InsPlan.PlanNum),"ClaimL leaves it off with its usual message.");
		}
	}
}
