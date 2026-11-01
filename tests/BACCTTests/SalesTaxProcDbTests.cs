// v24_4-account-salestax-proc: Quick Procs > Sales Tax adds one procedure whose fee is the tax on the highlighted completed procedures.
//  - its fee equals what the 24.3 adjustment method (Apply Sales Tax) posts for the same procedures, write-offs taken off first;
//  - only completed procedures are taxed, and the sales-tax procedure itself is never taxed (by hand, by automation, or as an estimate);
//  - a warning lists procedures that already have a sales-tax adjustment;
//  - the new procedure is complete, dated today, with the sales-tax provider (else the patient's primary provider) and the tax as its fee.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BACCTTests {
	[TestClass]
	public class SalesTaxProcDbTests {
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

		private static void InsertWriteOff(Procedure procedure,double writeOff) {
			ClaimProc claimProc=new ClaimProc();
			claimProc.ProcNum=procedure.ProcNum;
			claimProc.PatNum=procedure.PatNum;
			claimProc.Status=ClaimProcStatus.NotReceived;
			claimProc.WriteOff=writeOff;
			claimProc.ProvNum=procedure.ProvNum;
			claimProc.ProcDate=procedure.ProcDate;
			ClaimProcs.Insert(claimProc);
		}

		[TestMethod]
		public void Total_EqualsTheAdjustmentMethod_OnTheSameProcedures() {
			Patient patient=SalesTaxData.CreatePatient("TaxTotal");
			DateTime date=DateTime.Today.AddDays(-3);
			Procedure proc1=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_PLAIN,100,ProcStat.C,date);
			Procedure proc2=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_PLAIN,55.50,ProcStat.C,date);
			Procedure proc3=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_TAXED,33.30,ProcStat.C,date);
			InsertWriteOff(proc1,20);//PPO write-offs are taken off before tax.
			List<Procedure> listProcs=new List<Procedure> { proc1,proc2,proc3 };
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForProcs(listProcs.Select(x => x.ProcNum).ToList());
			double total=SalesTaxProcs.ComputeSalesTaxTotal(listProcs,listClaimProcs);
			Assert.AreEqual(8+5.55+3.33,total,0.0001,"10% of 80, 55.50 and 33.30, each rounded to cents.");
			//The 24.3 method: Apply Sales Tax posts one adjustment per highlighted procedure.
			foreach(Procedure procedure in listProcs) {
				Adjustments.CreateAdjustmentForSalesTax(procedure,canSkipIsTaxed:true);
			}
			long adjType=PrefC.GetLong(PrefName.SalesTaxAdjustmentType);
			double sumAdjustments=SalesTaxData.GetAdjustments(patient.PatNum).Where(x => x.AdjType==adjType).Sum(x => x.AdjAmt);
			Assert.AreEqual(sumAdjustments,total,0.0001);
		}

		[TestMethod]
		public void Total_RoundsEachProcedure_NotTheSum() {
			Patient patient=SalesTaxData.CreatePatient("TaxRounding");
			SalesTaxData.SetUp(8.25,SalesTaxData.CODE_TAX);
			List<Procedure> listProcs=new List<Procedure>();
			for(int i=0;i<3;i++) {
				listProcs.Add(SalesTaxData.CreateProc(patient,SalesTaxData.CODE_PLAIN,10.10,ProcStat.C,DateTime.Today));
			}
			double expectedEach=Math.Round(10.10*0.0825,2);//0.83
			Assert.AreEqual(3*expectedEach,SalesTaxProcs.ComputeSalesTaxTotal(listProcs,new List<ClaimProc>()),0.0001);
		}

		[TestMethod]
		public void TaxableProcs_AreCompletedOnly_AndNeverTheTaxProcedure() {
			Patient patient=SalesTaxData.CreatePatient("TaxTaxable");
			Procedure procComplete=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_PLAIN,50,ProcStat.C,DateTime.Today);
			Procedure procPlanned=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_PLAIN,50,ProcStat.TP,DateTime.Today);
			Procedure procExisting=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_PLAIN,50,ProcStat.EO,DateTime.Today);
			Procedure procTax=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_TAX,5,ProcStat.C,DateTime.Today);
			List<Procedure> listTaxable=SalesTaxProcs.GetTaxableProcs(new List<Procedure> { procComplete,procPlanned,procExisting,procTax });
			CollectionAssert.AreEqual(new List<long> { procComplete.ProcNum },listTaxable.Select(x => x.ProcNum).ToList());
			Assert.AreEqual(0,SalesTaxProcs.GetTaxableProcs(null).Count);
		}

		[TestMethod]
		public void TaxProcedure_IsNeverTaxedAgain_ByHandOrByAutomation() {
			SalesTaxData.SetUp(10,SalesTaxData.CODE_TAX,doAutomate:true);
			Patient patient=SalesTaxData.CreatePatient("TaxNeverAgain");
			Procedure procTax=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_TAX,7.50,ProcStat.C,DateTime.Today);//Its code is marked taxed.
			Procedure procTaxed=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_TAXED,40,ProcStat.C,DateTime.Today);
			Adjustments.CreateAdjustmentForSalesTax(procTax,canSkipIsTaxed:true);//Apply Sales Tax by hand.
			Adjustments.AddSalesTaxIfNoneExists(procTax);
			Assert.AreEqual(0,SalesTaxData.GetAdjustments(patient.PatNum).Count,"No adjustment on the sales-tax procedure.");
			Procedures.AfterProcsSetComplete(new List<Procedure> { procTax,procTaxed });//Automatic tax when procedures are set complete.
			List<Adjustment> listAdjustments=SalesTaxData.GetAdjustments(patient.PatNum);
			Assert.AreEqual(1,listAdjustments.Count);
			Assert.AreEqual(procTaxed.ProcNum,listAdjustments[0].ProcNum);
			Assert.AreEqual(4,listAdjustments[0].AdjAmt,0.0001);
		}

		[TestMethod]
		public void TaxProcedure_IsTaxedNormally_WhenTheFeatureIsOff() {
			//Without the setting the code is an ordinary taxed code, as in 24.3.
			SalesTaxData.SetUp(10,"",doAutomate:true);
			Patient patient=SalesTaxData.CreatePatient("TaxOffStill");
			Procedure procTax=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_TAX,20,ProcStat.C,DateTime.Today);
			Procedures.AfterProcsSetComplete(new List<Procedure> { procTax });
			Assert.AreEqual(2,SalesTaxData.GetAdjustments(patient.PatNum).Sum(x => x.AdjAmt),0.0001);
		}

		[TestMethod]
		public void TaxProcedure_HasNoTaxEstimate_WhenTreatmentPlanned() {
			Patient patient=SalesTaxData.CreatePatient("TaxEstimate");
			Procedure procTax=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_TAX,30,ProcStat.TP,DateTime.Today);
			Procedure procTaxed=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_TAXED,30,ProcStat.TP,DateTime.Today);
			Family family=Patients.GetFamily(patient.PatNum);
			List<InsSub> listInsSubs=InsSubs.RefreshForFam(family);
			List<InsPlan> listInsPlans=InsPlans.RefreshForSubList(listInsSubs);
			List<PatPlan> listPatPlans=PatPlans.Refresh(patient.PatNum);
			List<Benefit> listBenefits=Benefits.Refresh(listPatPlans,listInsSubs);
			foreach(Procedure procedure in new[] { procTax,procTaxed }) {
				List<ClaimProc> listClaimProcs=ClaimProcs.Refresh(patient.PatNum);
				Procedures.ComputeEstimates(procedure,patient.PatNum,ref listClaimProcs,false,listInsPlans,listPatPlans,listBenefits,
					new List<ClaimProcHist>(),new List<ClaimProcHist>(),false,patient.Age,listInsSubs);
			}
			Assert.AreEqual(0,procTax.TaxAmt,0.0001,"The sales-tax procedure carries tax; it gets no tax estimate even though its code is marked taxed.");
			Assert.AreEqual(3,procTaxed.TaxAmt,0.0001,"Other taxed procedures keep their estimate.");
		}

		[TestMethod]
		public void ProcsWithSalesTaxAdjustment_AreFoundForTheWarning() {
			Patient patient=SalesTaxData.CreatePatient("TaxWarn");
			Procedure procAdjusted=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_PLAIN,60,ProcStat.C,DateTime.Today);
			Procedure procOtherAdj=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_PLAIN,60,ProcStat.C,DateTime.Today);
			Procedure procClean=SalesTaxData.CreateProc(patient,SalesTaxData.CODE_PLAIN,60,ProcStat.C,DateTime.Today);
			Adjustments.CreateAdjustmentForSalesTax(procAdjusted,canSkipIsTaxed:true);
			SalesTaxData.CreateAdjustment(patient,-5,DateTime.Today,SalesTaxData.OtherAdjType(),procOtherAdj.ProcNum);
			List<long> listProcNums=SalesTaxProcs.GetProcNumsWithSalesTaxAdjustment(
				new List<long> { procAdjusted.ProcNum,procOtherAdj.ProcNum,procClean.ProcNum });
			CollectionAssert.AreEqual(new List<long> { procAdjusted.ProcNum },listProcNums);
			Assert.AreEqual(0,SalesTaxProcs.GetProcNumsWithSalesTaxAdjustment(new List<long>()).Count);
		}

		[TestMethod]
		public void NewTaxProcedure_IsCompleteToday_WithTheTaxAsFee() {
			Patient patient=SalesTaxData.CreatePatient("TaxNewProc");
			ProcedureCode procedureCode=ProcedureCodes.GetProcCode(SalesTaxData.CODE_TAX);
			DateTime dateTimeNow=new DateTime(2026,10,24,14,30,0);
			Procedure procedure=SalesTaxProcs.CreateSalesTaxProcedure(patient,procedureCode,12.34,dateTimeNow);
			Assert.AreEqual(ProcStat.C,procedure.ProcStatus);
			Assert.AreEqual(procedureCode.CodeNum,procedure.CodeNum);
			Assert.AreEqual(12.34,procedure.ProcFee,0.0001);
			Assert.AreEqual(dateTimeNow.Date,procedure.ProcDate);
			Assert.AreEqual(1,procedure.UnitQty);
			Assert.AreEqual(patient.PatNum,procedure.PatNum);
			Assert.AreEqual(patient.PriProv,procedure.ProvNum,"No sales-tax provider set: the patient's primary provider.");
			Assert.AreEqual(patient.ClinicNum,procedure.ClinicNum);
			Assert.IsTrue(SalesTaxProcs.IsSalesTaxProc(procedure));
		}

		[TestMethod]
		public void NewTaxProcedure_UsesTheSalesTaxDefaultProvider() {
			Provider provider=new Provider();
			provider.Abbr="SYNTAX";
			provider.LName="Tax";
			provider.FName="Synthetic";
			Providers.Insert(provider);
			Cache.Refresh(InvalidType.Providers);
			SalesTaxData.SetUp(10,SalesTaxData.CODE_TAX,defaultProvNum:provider.ProvNum);
			Patient patient=SalesTaxData.CreatePatient("TaxProv");
			Assert.AreNotEqual(provider.ProvNum,patient.PriProv);
			Procedure procedure=SalesTaxProcs.CreateSalesTaxProcedure(patient,ProcedureCodes.GetProcCode(SalesTaxData.CODE_TAX),1,DateTime.Now);
			Assert.AreEqual(provider.ProvNum,procedure.ProvNum);
		}

		[TestMethod]
		public void AuditNote_NamesTheCodeAmountAndCount() {
			string note=SalesTaxProcs.GetAuditNote(SalesTaxData.CODE_TAX,12.5,3);
			StringAssert.Contains(note,SalesTaxData.CODE_TAX);
			StringAssert.Contains(note,12.5.ToString("c"));
			StringAssert.Contains(note,"3");
		}
	}
}
