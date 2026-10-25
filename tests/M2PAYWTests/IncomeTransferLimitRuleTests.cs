// The income transfer limit rule (25.4, v25_4-account-incometransfer-limit) on made-up splits, without a database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2PAYWTests {
	[TestClass]
	public class IncomeTransferLimitRuleTests {
		private const long PAT=10;
		private const long PAT_OTHER=11;
		private const long PROC_A=100;
		private const long PROC_B=101;
		private const long ADJ=200;
		private const long PAYPLAN=300;
		private const long UNEARNED_PREPAY=40;
		private const long UNEARNED_OTHER=41;

		private static PaySplit Split(double amount,long procNum=0,long adjNum=0,long payPlanNum=0,long unearnedType=0,long patNum=PAT,long payNum=1) {
			return new PaySplit {
				PatNum=patNum,
				PayNum=payNum,
				ProcNum=procNum,
				AdjNum=adjNum,
				PayPlanNum=payPlanNum,
				UnearnedType=unearnedType,
				SplitAmt=amount,
			};
		}

		private static List<PaySplit> List(params PaySplit[] arrayPaySplits) {
			return arrayPaySplits.ToList();
		}

		[TestMethod]
		public void GetSource_ProcedureThenAdjustmentThenPayPlanThenUnearnedThenUnallocated() {
			Assert.AreEqual(new TransferSource(EnumTransferSourceKind.Procedure,PROC_A,0),
				IncomeTransferLimits.GetSource(Split(1,procNum:PROC_A,adjNum:ADJ,payPlanNum:PAYPLAN,unearnedType:UNEARNED_PREPAY)));
			Assert.AreEqual(new TransferSource(EnumTransferSourceKind.Adjustment,ADJ,0),
				IncomeTransferLimits.GetSource(Split(1,adjNum:ADJ,payPlanNum:PAYPLAN)));
			Assert.AreEqual(new TransferSource(EnumTransferSourceKind.PayPlan,PAYPLAN,0),IncomeTransferLimits.GetSource(Split(1,payPlanNum:PAYPLAN)));
			Assert.AreEqual(new TransferSource(EnumTransferSourceKind.Unearned,0,PAT),IncomeTransferLimits.GetSource(Split(1,unearnedType:UNEARNED_PREPAY)));
			Assert.AreEqual(new TransferSource(EnumTransferSourceKind.Unearned,0,PAT),IncomeTransferLimits.GetSource(Split(1,unearnedType:UNEARNED_OTHER)),
				"Unearned types are pooled, as the Payment window pools them in transfer mode.");
			Assert.AreEqual(new TransferSource(EnumTransferSourceKind.Unallocated,0,PAT),IncomeTransferLimits.GetSource(Split(1)));
			//Provider and clinic do not split a source.
			PaySplit paySplitProv=Split(1,procNum:PROC_A);
			paySplitProv.ProvNum=7;
			paySplitProv.ClinicNum=3;
			Assert.AreEqual(IncomeTransferLimits.GetSource(Split(1,procNum:PROC_A)),IncomeTransferLimits.GetSource(paySplitProv));
		}

		[TestMethod]
		public void TakingNoMoreThanPatientPaymentsHold_IsAllowed() {
			List<PaySplit> listOther=List(Split(100,procNum:PROC_A,payNum:5));
			Assert.AreEqual(0,IncomeTransferLimits.GetViolations(List(Split(-100,procNum:PROC_A),Split(100,unearnedType:UNEARNED_PREPAY)),
				new List<PaySplit>(),listOther).Count);
			Assert.AreEqual(0,IncomeTransferLimits.GetViolations(List(Split(-60,procNum:PROC_A),Split(60,procNum:PROC_B)),
				new List<PaySplit>(),listOther).Count);
		}

		[TestMethod]
		public void TakingMoreThanAPaymentStillHolds_IsBlockedWithTheAvailableAmount() {
			//Paid 100 on the procedure; an earlier transfer already moved 30 of it away.
			List<PaySplit> listOther=List(Split(100,procNum:PROC_A,payNum:5),Split(-30,procNum:PROC_A,payNum:6),Split(30,procNum:PROC_B,payNum:6));
			List<IncomeTransferLimitViolation> listViolations=IncomeTransferLimits.GetViolations(
				List(Split(-80,procNum:PROC_A),Split(80,unearnedType:UNEARNED_PREPAY)),new List<PaySplit>(),listOther);
			Assert.AreEqual(1,listViolations.Count);
			Assert.AreEqual(new TransferSource(EnumTransferSourceKind.Procedure,PROC_A,0),listViolations[0].Source);
			Assert.AreEqual(80m,listViolations[0].AmountTaken);
			Assert.AreEqual(70m,listViolations[0].AmountAvailable);
		}

		[TestMethod]
		public void InsuranceIncome_CannotBeMovedIntoPatientCredit() {
			//A procedure overpaid by insurance shows a credit in the account, but insurance payments are claimprocs, not paysplits: nothing to take.
			List<IncomeTransferLimitViolation> listViolations=IncomeTransferLimits.GetViolations(
				List(Split(-50,procNum:PROC_A),Split(50,unearnedType:UNEARNED_PREPAY)),new List<PaySplit>(),new List<PaySplit>());
			Assert.AreEqual(1,listViolations.Count);
			Assert.AreEqual(50m,listViolations[0].AmountTaken);
			Assert.AreEqual(0m,listViolations[0].AmountAvailable);
		}

		[TestMethod]
		public void CreditAdjustment_CannotBeMovedIntoPatientCredit() {
			List<IncomeTransferLimitViolation> listViolations=IncomeTransferLimits.GetViolations(
				List(Split(-25,adjNum:ADJ),Split(25,unearnedType:UNEARNED_PREPAY)),new List<PaySplit>(),new List<PaySplit>());
			Assert.AreEqual(1,listViolations.Count);
			Assert.AreEqual(EnumTransferSourceKind.Adjustment,listViolations[0].Source.Kind);
			//A charge adjustment that the patient paid can give back what was paid on it.
			Assert.AreEqual(0,IncomeTransferLimits.GetViolations(List(Split(-25,adjNum:ADJ),Split(25,unearnedType:UNEARNED_PREPAY)),
				new List<PaySplit>(),List(Split(25,adjNum:ADJ,payNum:5))).Count);
		}

		[TestMethod]
		public void Unearned_IsLimitedPerPatient_AllTypesTogether() {
			List<PaySplit> listOther=List(Split(40,unearnedType:UNEARNED_PREPAY,payNum:5),Split(60,unearnedType:UNEARNED_OTHER,payNum:6),
				Split(500,unearnedType:UNEARNED_PREPAY,patNum:PAT_OTHER,payNum:7));
			Assert.AreEqual(0,IncomeTransferLimits.GetViolations(List(Split(-100,unearnedType:UNEARNED_PREPAY),Split(100,procNum:PROC_A)),
				new List<PaySplit>(),listOther).Count);
			List<IncomeTransferLimitViolation> listViolations=IncomeTransferLimits.GetViolations(
				List(Split(-101,unearnedType:UNEARNED_PREPAY),Split(101,procNum:PROC_A)),new List<PaySplit>(),listOther);
			Assert.AreEqual(1,listViolations.Count);
			Assert.AreEqual(new TransferSource(EnumTransferSourceKind.Unearned,0,PAT),listViolations[0].Source);
			Assert.AreEqual(100m,listViolations[0].AmountAvailable,"Another patient's unearned income is not available.");
		}

		[TestMethod]
		public void Unallocated_IsLimitedPerPatient() {
			List<PaySplit> listOther=List(Split(20,payNum:5),Split(300,patNum:PAT_OTHER,payNum:6));
			Assert.AreEqual(0,IncomeTransferLimits.GetViolations(List(Split(-20),Split(20,procNum:PROC_A)),new List<PaySplit>(),listOther).Count);
			Assert.AreEqual(1,IncomeTransferLimits.GetViolations(List(Split(-21),Split(21,procNum:PROC_A)),new List<PaySplit>(),listOther).Count);
		}

		[TestMethod]
		public void SplitsOnOneSource_AreNettedBeforeTheCheck() {
			//Takes 100 and gives back 30 on the same procedure: 70 taken overall.
			List<PaySplit> listOther=List(Split(70,procNum:PROC_A,payNum:5));
			Assert.AreEqual(0,IncomeTransferLimits.GetViolations(List(Split(-100,procNum:PROC_A),Split(30,procNum:PROC_A),Split(70,procNum:PROC_B)),
				new List<PaySplit>(),listOther).Count);
		}

		[TestMethod]
		public void PositiveLegs_AreNeverLimited() {
			Assert.AreEqual(0,IncomeTransferLimits.GetViolations(List(Split(500,procNum:PROC_A),Split(500,unearnedType:UNEARNED_PREPAY)),
				new List<PaySplit>(),new List<PaySplit>()).Count);
		}

		[TestMethod]
		public void StoredTransferSavedAgainUnchanged_IsNotBlocked() {
			//A transfer saved before the preference was turned on took 50 from a credit adjustment.
			List<PaySplit> listSaved=List(Split(-50,adjNum:ADJ),Split(50,unearnedType:UNEARNED_PREPAY));
			List<PaySplit> listCur=List(Split(-50,adjNum:ADJ),Split(50,unearnedType:UNEARNED_PREPAY));
			Assert.AreEqual(0,IncomeTransferLimits.GetViolations(listCur,listSaved,new List<PaySplit>()).Count);
			//Taking less is fine too.
			Assert.AreEqual(0,IncomeTransferLimits.GetViolations(List(Split(-20,adjNum:ADJ),Split(20,unearnedType:UNEARNED_PREPAY)),listSaved,
				new List<PaySplit>()).Count);
			//Taking more is checked against the patient payments.
			Assert.AreEqual(1,IncomeTransferLimits.GetViolations(List(Split(-60,adjNum:ADJ),Split(60,unearnedType:UNEARNED_PREPAY)),listSaved,
				new List<PaySplit>()).Count);
		}

		[TestMethod]
		public void HalfCentRounding_IsTolerated() {
			Assert.AreEqual(0,IncomeTransferLimits.GetViolations(List(Split(-33.334,procNum:PROC_A),Split(33.334,procNum:PROC_B)),new List<PaySplit>(),
				List(Split(33.33,procNum:PROC_A,payNum:5))).Count);
		}

		[TestMethod]
		public void SeveralSources_AreReportedInAStableOrder() {
			List<IncomeTransferLimitViolation> listViolations=IncomeTransferLimits.GetViolations(
				List(Split(-10,unearnedType:UNEARNED_PREPAY),Split(-10,adjNum:ADJ),Split(-10,procNum:PROC_B),Split(-10,procNum:PROC_A),Split(40,payPlanNum:PAYPLAN)),
				new List<PaySplit>(),new List<PaySplit>());
			CollectionAssert.AreEqual(new[] { "Procedure #100","Procedure #101","Adjustment #200","Unearned, PatNum #10" },
				listViolations.Select(x => x.Source.ToString()).ToArray());
		}

		[TestMethod]
		public void Message_NamesEachSourceWithTakenAndAvailable() {
			IncomeTransferLimitViolation violation=new IncomeTransferLimitViolation {
				Source=new TransferSource(EnumTransferSourceKind.Procedure,PROC_A,0),
				AmountTaken=80m,
				AmountAvailable=70m,
				Description="Procedure Z9001 on 01/02/2026 (Payw, Synthetic)",
			};
			string message=IncomeTransferLimits.GetMessage(new List<IncomeTransferLimitViolation> { violation });
			StringAssert.Contains(message,"Procedure Z9001 on 01/02/2026 (Payw, Synthetic)");
			StringAssert.Contains(message,80m.ToString("c"));
			StringAssert.Contains(message,70m.ToString("c"));
			StringAssert.Contains(message,"limited to the amount of patient payments");
		}

		[TestMethod]
		public void Fingerprint_IgnoresOrderButNotValues() {
			PaySplit paySplitA=Split(-50,procNum:PROC_A);
			PaySplit paySplitB=Split(50,unearnedType:UNEARNED_PREPAY);
			string fingerprint=IncomeTransferLimits.GetFingerprint(List(paySplitA,paySplitB));
			Assert.AreEqual(fingerprint,IncomeTransferLimits.GetFingerprint(List(paySplitB.Copy(),paySplitA.Copy())));
			Assert.AreNotEqual(fingerprint,IncomeTransferLimits.GetFingerprint(List(Split(-60,procNum:PROC_A),Split(60,unearnedType:UNEARNED_PREPAY))));
			Assert.AreNotEqual(fingerprint,IncomeTransferLimits.GetFingerprint(List(Split(-50,procNum:PROC_B),Split(50,unearnedType:UNEARNED_PREPAY))));
			Assert.AreNotEqual(fingerprint,IncomeTransferLimits.GetFingerprint(List(paySplitA)));
		}
	}
}
