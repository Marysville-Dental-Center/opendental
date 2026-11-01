// The income transfer limit (25.4) against stored payments, adjustments and procedures in the scratch database. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace M2PAYWTests {
	[TestClass]
	public class IncomeTransferLimitDbTests {
		[TestInitialize]
		public void TestInitialize() {
			PaywDb.Begin();
		}

		private static long GetDefNum(DefCat defCat,string name) {
			Def def=Defs.GetDefsForCategory(defCat,true).FirstOrDefault(x => x.ItemName==name);
			if(def==null) {
				def=DefT.CreateDefinition(defCat,name);
			}
			return def.DefNum;
		}

		private static long UnearnedPrepay => GetDefNum(DefCat.PaySplitUnearnedType,"M2PAYW Prepayment");
		private static long PayTypeCheck => GetDefNum(DefCat.PaymentTypes,"M2PAYW Check");

		///<summary>An empty income transfer (PayType 0, amount 0) as the Payment window inserts it before it opens.</summary>
		private static Payment NewTransfer(Patient patient) {
			return PaymentT.MakePaymentNoSplits(patient.PatNum,0,payType:0);
		}

		private static PaySplit Split(Patient patient,Payment payment,double amount,long procNum=0,long adjNum=0,long unearnedType=0) {
			return new PaySplit {
				PatNum=patient.PatNum,
				PayNum=payment.PayNum,
				ProcNum=procNum,
				AdjNum=adjNum,
				UnearnedType=unearnedType,
				ProvNum=patient.PriProv,
				SplitAmt=amount,
				DatePay=DateTime.Today,
			};
		}

		[TestMethod]
		public void Preference_IsSeededOffAndReadByIsEnabled() {
			Assert.AreEqual("0",PaywDb.SeededLimitValue,"Seed26_1 inserts the preference with its default, off.");
			try {
				PaywDb.SetLimit(true);
				Assert.IsTrue(IncomeTransferLimits.IsEnabled());
				PaywDb.SetLimit(false);
				Assert.IsFalse(IncomeTransferLimits.IsEnabled());
			}
			finally {
				PaywDb.SetLimit(false);
			}
		}

		[TestMethod]
		public void FromAProcedurePaidByThePatient_UpToWhatThePaymentHolds() {
			Patient patient=PaywDb.CreatePatient("Proc");
			Procedure procedure=ProcedureT.CreateProcedure(patient,"Z9101",ProcStat.C,"",100);
			PaymentT.MakePayment(patient.PatNum,100,procNum:procedure.ProcNum,payType:PayTypeCheck,provNum:patient.PriProv);
			Payment paymentTransfer=NewTransfer(patient);
			List<IncomeTransferLimitViolation> listViolations=IncomeTransferLimits.GetViolations(paymentTransfer.PayNum,new List<PaySplit> {
				Split(patient,paymentTransfer,-60,procNum:procedure.ProcNum),
				Split(patient,paymentTransfer,60,unearnedType:UnearnedPrepay),
			});
			Assert.AreEqual(0,listViolations.Count);
			listViolations=IncomeTransferLimits.GetViolations(paymentTransfer.PayNum,new List<PaySplit> {
				Split(patient,paymentTransfer,-120,procNum:procedure.ProcNum),
				Split(patient,paymentTransfer,120,unearnedType:UnearnedPrepay),
			});
			Assert.AreEqual(1,listViolations.Count);
			Assert.AreEqual(120m,listViolations[0].AmountTaken);
			Assert.AreEqual(100m,listViolations[0].AmountAvailable);
			StringAssert.Contains(listViolations[0].Description,"Z9101");
			StringAssert.Contains(listViolations[0].Description,patient.GetNameLF());
			StringAssert.Contains(IncomeTransferLimits.GetMessage(listViolations),100m.ToString("c"));
		}

		[TestMethod]
		public void FromACreditAdjustment_IsBlocked() {
			Patient patient=PaywDb.CreatePatient("Adj");
			Adjustment adjustment=AdjustmentT.MakeAdjustment(patient.PatNum,-50,adjType:GetDefNum(DefCat.AdjTypes,"M2PAYW Courtesy"),provNum:patient.PriProv);
			Payment paymentTransfer=NewTransfer(patient);
			List<IncomeTransferLimitViolation> listViolations=IncomeTransferLimits.GetViolations(paymentTransfer.PayNum,new List<PaySplit> {
				Split(patient,paymentTransfer,-50,adjNum:adjustment.AdjNum),
				Split(patient,paymentTransfer,50,unearnedType:UnearnedPrepay),
			});
			Assert.AreEqual(1,listViolations.Count);
			Assert.AreEqual(EnumTransferSourceKind.Adjustment,listViolations[0].Source.Kind);
			Assert.AreEqual(0m,listViolations[0].AmountAvailable);
			StringAssert.StartsWith(listViolations[0].Description,"Adjustment");
		}

		[TestMethod]
		public void FromAProcedureOverpaidByInsuranceOnly_IsBlocked() {
			Patient patient=PaywDb.CreatePatient("Ins");
			Procedure procedure=ProcedureT.CreateProcedure(patient,"Z9102",ProcStat.C,"",100);
			ClaimProc claimProc=new ClaimProc {
				ProcNum=procedure.ProcNum,
				PatNum=patient.PatNum,
				ProvNum=patient.PriProv,
				Status=ClaimProcStatus.Received,
				InsPayAmt=150,
				DateCP=DateTime.Today,
				ProcDate=DateTime.Today,
			};
			ClaimProcs.Insert(claimProc);
			Payment paymentTransfer=NewTransfer(patient);
			List<IncomeTransferLimitViolation> listViolations=IncomeTransferLimits.GetViolations(paymentTransfer.PayNum,new List<PaySplit> {
				Split(patient,paymentTransfer,-50,procNum:procedure.ProcNum),
				Split(patient,paymentTransfer,50,unearnedType:UnearnedPrepay),
			});
			Assert.AreEqual(1,listViolations.Count);
			Assert.AreEqual(0m,listViolations[0].AmountAvailable,"Insurance income is not patient payment money.");
		}

		[TestMethod]
		public void FromUnearned_UpToThePatientsUnearnedIncomeOfAnyType() {
			Patient patient=PaywDb.CreatePatient("Pre");
			Patient patientOther=PaywDb.CreatePatient("PreOther");
			Procedure procedure=ProcedureT.CreateProcedure(patient,"Z9103",ProcStat.C,"",80);
			PaymentT.MakePayment(patient.PatNum,40,unearnedType:UnearnedPrepay,payType:PayTypeCheck);
			PaymentT.MakePayment(patient.PatNum,10,unearnedType:GetDefNum(DefCat.PaySplitUnearnedType,"M2PAYW Deposit"),payType:PayTypeCheck);
			PaymentT.MakePayment(patientOther.PatNum,500,unearnedType:UnearnedPrepay,payType:PayTypeCheck);
			Payment paymentTransfer=NewTransfer(patient);
			Assert.AreEqual(0,IncomeTransferLimits.GetViolations(paymentTransfer.PayNum,new List<PaySplit> {
				Split(patient,paymentTransfer,-50,unearnedType:UnearnedPrepay),
				Split(patient,paymentTransfer,50,procNum:procedure.ProcNum),
			}).Count);
			List<IncomeTransferLimitViolation> listViolations=IncomeTransferLimits.GetViolations(paymentTransfer.PayNum,new List<PaySplit> {
				Split(patient,paymentTransfer,-80,unearnedType:UnearnedPrepay),
				Split(patient,paymentTransfer,80,procNum:procedure.ProcNum),
			});
			Assert.AreEqual(1,listViolations.Count);
			Assert.AreEqual(50m,listViolations[0].AmountAvailable,"Another patient's prepayment is not available.");
			StringAssert.StartsWith(listViolations[0].Description,"Unearned income ("+patient.GetNameLF()+")");
		}

		[TestMethod]
		public void SavedTransfer_ReopenedUnchanged_IsNotBlocked_ButTakingMoreIs() {
			Patient patient=PaywDb.CreatePatient("Old");
			Adjustment adjustment=AdjustmentT.MakeAdjustment(patient.PatNum,-50,adjType:GetDefNum(DefCat.AdjTypes,"M2PAYW Courtesy"));
			//Saved before the preference was turned on.
			Payment paymentTransfer=NewTransfer(patient);
			PaySplits.Insert(Split(patient,paymentTransfer,-50,adjNum:adjustment.AdjNum));
			PaySplits.Insert(Split(patient,paymentTransfer,50,unearnedType:UnearnedPrepay));
			List<PaySplit> listPaySplitsLoaded=PaySplits.GetForPayment(paymentTransfer.PayNum);
			Assert.AreEqual(0,IncomeTransferLimits.GetViolations(paymentTransfer.PayNum,listPaySplitsLoaded).Count);
			List<PaySplit> listPaySplitsMore=listPaySplitsLoaded.Select(x => x.Copy()).ToList();
			listPaySplitsMore.ForEach(x => x.SplitAmt=(x.SplitAmt<0 ? -70 : 70));
			Assert.AreEqual(1,IncomeTransferLimits.GetViolations(paymentTransfer.PayNum,listPaySplitsMore).Count);
		}

		[TestMethod]
		public void EarlierTransfers_MovePatientMoneyBetweenSources() {
			Patient patient=PaywDb.CreatePatient("Chain");
			Procedure procedureA=ProcedureT.CreateProcedure(patient,"Z9104",ProcStat.C,"",100);
			Procedure procedureB=ProcedureT.CreateProcedure(patient,"Z9105",ProcStat.C,"",100);
			PaymentT.MakePayment(patient.PatNum,100,procNum:procedureA.ProcNum,payType:PayTypeCheck);
			//An earlier transfer moved 30 from procedure A to procedure B.
			Payment paymentEarlier=NewTransfer(patient);
			PaySplits.Insert(Split(patient,paymentEarlier,-30,procNum:procedureA.ProcNum));
			PaySplits.Insert(Split(patient,paymentEarlier,30,procNum:procedureB.ProcNum));
			Payment paymentTransfer=NewTransfer(patient);
			//Procedure B now holds 30 of patient money; procedure A holds 70.
			Assert.AreEqual(0,IncomeTransferLimits.GetViolations(paymentTransfer.PayNum,new List<PaySplit> {
				Split(patient,paymentTransfer,-30,procNum:procedureB.ProcNum),
				Split(patient,paymentTransfer,-70,procNum:procedureA.ProcNum),
				Split(patient,paymentTransfer,100,unearnedType:UnearnedPrepay),
			}).Count);
			List<IncomeTransferLimitViolation> listViolations=IncomeTransferLimits.GetViolations(paymentTransfer.PayNum,new List<PaySplit> {
				Split(patient,paymentTransfer,-80,procNum:procedureA.ProcNum),
				Split(patient,paymentTransfer,80,unearnedType:UnearnedPrepay),
			});
			Assert.AreEqual(1,listViolations.Count);
			Assert.AreEqual(70m,listViolations[0].AmountAvailable);
		}

		[TestMethod]
		public void GetSplitsOnSources_LeavesOutThePaymentItselfAndOtherSources() {
			Patient patient=PaywDb.CreatePatient("Src");
			Procedure procedure=ProcedureT.CreateProcedure(patient,"Z9106",ProcStat.C,"",100);
			Procedure procedureOther=ProcedureT.CreateProcedure(patient,"Z9107",ProcStat.C,"",100);
			PaymentT.MakePayment(patient.PatNum,25,procNum:procedure.ProcNum,payType:PayTypeCheck);
			PaymentT.MakePayment(patient.PatNum,35,procNum:procedureOther.ProcNum,payType:PayTypeCheck);
			PaymentT.MakePayment(patient.PatNum,15,payType:PayTypeCheck);//Unallocated.
			Payment paymentTransfer=NewTransfer(patient);
			PaySplits.Insert(Split(patient,paymentTransfer,-5,procNum:procedure.ProcNum));
			List<PaySplit> listPaySplits=IncomeTransferLimits.GetSplitsOnSources(paymentTransfer.PayNum,new List<long> { procedure.ProcNum },
				new List<long>(),new List<long>(),new List<long> { patient.PatNum });
			CollectionAssert.AreEquivalent(new[] { 25d,15d },listPaySplits.Select(x => x.SplitAmt).ToArray());
			Assert.AreEqual(0,IncomeTransferLimits.GetSplitsOnSources(0,null,null,null,null).Count);
		}
	}
}
