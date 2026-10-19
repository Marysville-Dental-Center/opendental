// Tests for the CareCredit background work that replaced Open Dental's webhook relay and eConnector: the polling schedule, applying pullback
// results (payments created once, patient fields), and who batch QuickScreen screens. No network, no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.Bridges;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.CareCreditTests {
	[TestClass]
	public class CareCreditPollScheduleTests {
		private static readonly TimeSpan _pullbackTime=new TimeSpan(7,50,0);

		private static CareCreditWebResponse CreateRow(CareCreditServiceType serviceType,DateTime dateTimePending) {
			CareCreditWebResponse row=new CareCreditWebResponse();
			row.RefNumber="R";
			row.ServiceType=serviceType;
			row.ProcessingStatus=CareCreditWebStatus.Pending;
			row.DateTimePending=dateTimePending;
			return row;
		}

		[TestMethod]
		public void Prefill_BacksOffWithAge() {
			DateTime start=new DateTime(2026,10,19,9,0,0);
			CareCreditWebResponse row=CreateRow(CareCreditServiceType.Prefill,start);
			Assert.AreEqual(TimeSpan.FromMinutes(1),CareCreditPollSchedule.GetInterval(row,start.AddMinutes(5)));
			Assert.AreEqual(TimeSpan.FromMinutes(10),CareCreditPollSchedule.GetInterval(row,start.AddHours(1)));
			Assert.AreEqual(TimeSpan.FromMinutes(60),CareCreditPollSchedule.GetInterval(row,start.AddHours(5)));
			Assert.IsTrue(CareCreditPollSchedule.IsDue(row,DateTime.MinValue,start.AddMinutes(1),_pullbackTime));
			Assert.IsFalse(CareCreditPollSchedule.IsDue(row,start.AddHours(1),start.AddHours(1).AddMinutes(5),_pullbackTime));
			Assert.IsTrue(CareCreditPollSchedule.IsDue(row,start.AddHours(1),start.AddHours(1).AddMinutes(10),_pullbackTime));
			Assert.IsFalse(CareCreditPollSchedule.IsExpired(row,start.AddHours(71)));
			Assert.IsTrue(CareCreditPollSchedule.IsExpired(row,start.AddHours(72)));
		}

		[TestMethod]
		public void Batch_WaitsForThePullbackTimeAndExpiresAfterADay() {
			DateTime submitted=new DateTime(2026,10,19,7,20,0);
			CareCreditWebResponse row=CreateRow(CareCreditServiceType.Batch,submitted);
			Assert.IsFalse(CareCreditPollSchedule.IsDue(row,DateTime.MinValue,submitted.AddMinutes(20),_pullbackTime),"Before 07:50.");
			Assert.IsTrue(CareCreditPollSchedule.IsDue(row,DateTime.MinValue,submitted.AddMinutes(30),_pullbackTime));
			Assert.IsFalse(CareCreditPollSchedule.IsDue(row,submitted.AddMinutes(30),submitted.AddMinutes(40),_pullbackTime),"Every 15 minutes.");
			DateTime submittedLate=new DateTime(2026,10,19,10,0,0);
			CareCreditWebResponse rowLate=CreateRow(CareCreditServiceType.Batch,submittedLate);
			Assert.IsFalse(CareCreditPollSchedule.IsDue(rowLate,DateTime.MinValue,submittedLate.AddMinutes(10),_pullbackTime));
			Assert.IsTrue(CareCreditPollSchedule.IsDue(rowLate,DateTime.MinValue,submittedLate.AddMinutes(15),_pullbackTime));
			Assert.IsTrue(CareCreditPollSchedule.IsExpired(row,submitted.AddHours(24)));
			Assert.IsFalse(CareCreditPollSchedule.IsExpired(row,submitted.AddHours(23)));
		}

		[TestMethod]
		public void RowsWithoutReference_AreNeverPulledBack() {
			CareCreditWebResponse row=CreateRow(CareCreditServiceType.Prefill,new DateTime(2026,10,19,9,0,0));
			row.RefNumber="";
			Assert.IsFalse(CareCreditPollSchedule.IsDue(row,DateTime.MinValue,new DateTime(2026,10,19,10,0,0),_pullbackTime));
		}
	}

	[TestClass]
	public class CareCreditApplyResultTests {
		private static readonly DateTime _dateTimeNow=new DateTime(2026,10,19,9,30,0);
		private List<Tuple<long,long,double,string,double>> _listInserted;
		private Dictionary<long,Payment> _dictPayments;
		private List<Payment> _listUpdatedPayments;
		private List<CareCreditWebResponse> _listRowWrites;
		private List<Tuple<string,long,bool>> _listPatFields;
		private List<Tuple<double,long>> _listPreApproval;
		private List<Tuple<double,long>> _listAvailableCredit;
		private List<string> _listClosedMerchants;
		private CareCreditResultActions _actions;

		[TestInitialize]
		public void TestInitialize() {
			_listInserted=new List<Tuple<long,long,double,string,double>>();
			_dictPayments=new Dictionary<long,Payment>();
			_listUpdatedPayments=new List<Payment>();
			_listRowWrites=new List<CareCreditWebResponse>();
			_listPatFields=new List<Tuple<string,long,bool>>();
			_listPreApproval=new List<Tuple<double,long>>();
			_listAvailableCredit=new List<Tuple<double,long>>();
			_listClosedMerchants=new List<string>();
			_actions=new CareCreditResultActions();
			_actions.FuncInsertPayment=(patNum,clinicNum,amount,note,fee) => {
				_listInserted.Add(Tuple.Create(patNum,clinicNum,amount,note,fee));
				return 900+_listInserted.Count;
			};
			_actions.FuncGetPayment=x => _dictPayments.TryGetValue(x,out Payment payment) ? payment : null;
			_actions.ActionUpdatePayment=(payment,paymentOld) => _listUpdatedPayments.Add(payment);
			_actions.FuncUpdateRow=(row,rowOld) => {
				_listRowWrites.Add(row.Copy());
				return true;
			};
			_actions.ActionUpdatePatField=(status,patNum,isBatch) => _listPatFields.Add(Tuple.Create(status,patNum,isBatch));
			_actions.ActionUpsertPreApprovalAmt=(amount,patNum) => _listPreApproval.Add(Tuple.Create(amount,patNum));
			_actions.ActionUpsertAvailableCredit=(amount,patNum) => _listAvailableCredit.Add(Tuple.Create(amount,patNum));
			_actions.ActionMarkMerchantClosed=x => _listClosedMerchants.Add(x);
		}

		private static CareCreditWebResponse CreatePendingRow(CareCreditTransType transType,long payNum,CareCreditServiceType serviceType=CareCreditServiceType.Prefill) {
			CareCreditWebResponse row=new CareCreditWebResponse();
			row.CareCreditWebResponseNum=7;
			row.PatNum=55;
			row.ClinicNum=2;
			row.PayNum=payNum;
			row.RefNumber="REF-7";
			row.MerchantNumber=CareCreditTestData.MERCHANT;
			row.ServiceType=serviceType;
			row.TransType=transType;
			row.ProcessingStatus=CareCreditWebStatus.Pending;
			row.Amount=80;
			row.LastResponseStr="";
			return row;
		}

		[TestMethod]
		public void PurchaseFinishedOnPatientDevice_CreatesOnePayment() {
			CareCreditWebResponse row=CreatePendingRow(CareCreditTransType.Purchase,payNum:0);
			CareCreditPullbackResult result=CareCreditJson.ReadPullback("{\"status\":\"COMPLETED\",\"amount\":\"80.00\",\"merchantFee\":\"4.00\"}",false);
			CareCreditJobs.ApplyResult(row,result,_dateTimeNow,_actions);
			Assert.AreEqual(1,_listInserted.Count);
			Assert.AreEqual(55,_listInserted[0].Item1);
			Assert.AreEqual(2,_listInserted[0].Item2);
			Assert.AreEqual(80,_listInserted[0].Item3,0.001);
			Assert.AreEqual(4,_listInserted[0].Item5,0.001);
			Assert.AreEqual(901,row.PayNum);
			Assert.AreEqual(CareCreditWebStatus.Completed,row.ProcessingStatus);
			Assert.AreEqual(CareCreditWebStatus.Completed,_listRowWrites.First().ProcessingStatus,"The completed status is saved before the payment is created.");
			Assert.AreEqual(901,_listRowWrites.Last().PayNum);
			//The same result again (a repeated pullback) creates nothing more.
			CareCreditJobs.ApplyResult(row,result,_dateTimeNow,_actions);
			Assert.AreEqual(1,_listInserted.Count);
		}

		[TestMethod]
		public void RefundFinishedElsewhere_IsANegativePayment() {
			CareCreditWebResponse row=CreatePendingRow(CareCreditTransType.Refund,payNum:0);
			CareCreditJobs.ApplyResult(row,CareCreditJson.ReadPullback("{\"status\":\"COMPLETED\",\"amount\":\"30\"}",false),_dateTimeNow,_actions);
			Assert.AreEqual(-30,_listInserted.Single().Item3,0.001);
		}

		[TestMethod]
		public void CompletedWithPayment_CompletesThatPaymentOnce() {
			Payment payment=new Payment();
			payment.PayNum=44;
			payment.PayNote="Saved before launch";
			_dictPayments[44]=payment;
			CareCreditWebResponse row=CreatePendingRow(CareCreditTransType.Purchase,payNum:44);
			CareCreditJobs.ApplyResult(row,CareCreditJson.ReadPullback("{\"status\":\"COMPLETED\",\"merchantFee\":\"3.5\"}",false),_dateTimeNow,_actions);
			Assert.AreEqual(0,_listInserted.Count,"Never a second payment.");
			Payment paymentUpdated=_listUpdatedPayments.Single();
			Assert.IsTrue(paymentUpdated.IsCcCompleted);
			Assert.AreEqual(CreditCardSource.CareCredit,paymentUpdated.PaymentSource);
			Assert.AreEqual(3.5,paymentUpdated.MerchantFee,0.001);
			StringAssert.Contains(paymentUpdated.PayNote,"Saved before launch");
			StringAssert.Contains(paymentUpdated.PayNote,"REF-7");
		}

		[TestMethod]
		public void PaymentAlreadyCompleted_IsLeftAlone() {
			Payment payment=new Payment();
			payment.PayNum=44;
			payment.IsCcCompleted=true;
			_dictPayments[44]=payment;
			CareCreditWebResponse row=CreatePendingRow(CareCreditTransType.Purchase,payNum:44);
			CareCreditJobs.ApplyResult(row,CareCreditJson.ReadPullback("{\"status\":\"COMPLETED\"}",false),_dateTimeNow,_actions);
			Assert.AreEqual(0,_listUpdatedPayments.Count);
			Assert.AreEqual(0,_listInserted.Count);
		}

		[TestMethod]
		public void PendingResult_ChangesNothingButTheResponse() {
			CareCreditWebResponse row=CreatePendingRow(CareCreditTransType.Purchase,payNum:0);
			CareCreditJobs.ApplyResult(row,CareCreditJson.ReadPullback("{\"status\":\"PENDING\"}",false),_dateTimeNow,_actions);
			Assert.AreEqual(CareCreditWebStatus.Pending,row.ProcessingStatus);
			Assert.AreEqual(0,_listInserted.Count);
			Assert.AreEqual(0,_listPatFields.Count);
		}

		[TestMethod]
		public void QuickScreen_WritesPatientFields() {
			CareCreditWebResponse row=CreatePendingRow(CareCreditTransType.None,0,CareCreditServiceType.Batch);
			CareCreditJobs.ApplyResult(row,CareCreditJson.ReadPullback("{\"status\":\"PRE-APPROVED\",\"preApprovalAmount\":\"3000\"}",true),_dateTimeNow,_actions);
			Assert.AreEqual(CareCreditWebStatus.PreApproved,row.ProcessingStatus);
			Assert.AreEqual(CareCreditWebStatus.PreApproved.GetDescription(),_listPatFields.Single().Item1);
			Assert.IsTrue(_listPatFields.Single().Item3,"Batch results are copied to minors.");
			Assert.AreEqual(3000,_listPreApproval.Single().Item1,0.001);
			Assert.AreEqual(0,_listAvailableCredit.Count);
			Assert.AreEqual(0,_listInserted.Count);
		}

		[TestMethod]
		public void Lookup_WritesAvailableCredit() {
			CareCreditWebResponse row=CreatePendingRow(CareCreditTransType.None,0,CareCreditServiceType.IndividualQS);
			CareCreditJobs.ApplyResult(row,CareCreditJson.ReadPullback("{\"status\":\"Cardholder\",\"availableCredit\":\"1200\"}",true),_dateTimeNow,_actions);
			Assert.AreEqual(CareCreditWebStatus.AccountFound,row.ProcessingStatus);
			Assert.IsFalse(_listPatFields.Single().Item3);
			Assert.AreEqual(1200,_listAvailableCredit.Single().Item1,0.001);
		}

		[TestMethod]
		public void BatchMerchantClosed_BecomesBatchErrorAndMarksTheMerchant() {
			CareCreditWebResponse row=CreatePendingRow(CareCreditTransType.None,0,CareCreditServiceType.Batch);
			CareCreditJobs.ApplyResult(row,CareCreditJson.ReadPullback("{\"status\":\"ERROR\",\"errorDescription\":\"Merchant closed\"}",true),_dateTimeNow,_actions);
			Assert.AreEqual(CareCreditWebStatus.BatchError,row.ProcessingStatus);
			Assert.AreEqual(_dateTimeNow,row.DateTimeLastError);
			CollectionAssert.AreEqual(new[] { CareCreditTestData.MERCHANT },_listClosedMerchants);
			Assert.IsTrue(CareCreditQSBatchResponse.From(row).IsMerchantClosed);
		}
	}

	[TestClass]
	public class CareCreditBatchPlannerTests {
		private static CareCreditBatchCandidate CreateCandidate(Patient patient,Patient guarantor=null,double fee=250.4,double patPortion=100,
			string merchant=CareCreditTestData.MERCHANT,int dayOffset=1)
		{
			CareCreditBatchCandidate candidate=new CareCreditBatchCandidate();
			candidate.AptNum=patient.PatNum*10+dayOffset;
			candidate.AptDateTime=new DateTime(2026,10,19).AddDays(dayOffset);
			candidate.ClinicNum=1;
			candidate.ProvNum=3;
			candidate.ProviderAbbr="DOC";
			candidate.Patient=patient;
			candidate.Guarantor=guarantor??patient;
			candidate.EstimatedFee=fee;
			candidate.EstimatedPatientPortion=patPortion;
			candidate.MerchantNumber=merchant;
			return candidate;
		}

		[TestMethod]
		public void Off_PlansNothing() {
			CareCreditBatchPlan plan=CareCreditBatchPlanner.Plan(new List<CareCreditBatchCandidate> { CreateCandidate(CareCreditTestData.CreateAdult(1)) },
				EnumCareCreditBatch.Off,null);
			Assert.AreEqual(0,plan.ListSubmissions.Count+plan.ListErrors.Count);
		}

		[TestMethod]
		public void WithBalance_NeedsAPatientPortionOverOneDollar() {
			List<CareCreditBatchCandidate> listCandidates=new List<CareCreditBatchCandidate> {
				CreateCandidate(CareCreditTestData.CreateAdult(1),patPortion:1),
				CreateCandidate(CareCreditTestData.CreateAdult(2),patPortion:1.01),
			};
			CareCreditBatchPlan planOn=CareCreditBatchPlanner.Plan(listCandidates,EnumCareCreditBatch.On,null);
			CollectionAssert.AreEqual(new long[] { 2 },planOn.ListSubmissions.Select(x => x.PatNum).ToList());
			CareCreditBatchPlan planNoBal=CareCreditBatchPlanner.Plan(listCandidates,EnumCareCreditBatch.NoBalance,null);
			Assert.AreEqual(2,planNoBal.ListSubmissions.Count);
		}

		[TestMethod]
		public void RecentPatients_AreExcluded() {
			CareCreditBatchPlan plan=CareCreditBatchPlanner.Plan(new List<CareCreditBatchCandidate> {
				CreateCandidate(CareCreditTestData.CreateAdult(1)),CreateCandidate(CareCreditTestData.CreateAdult(2)) },EnumCareCreditBatch.NoBalance,new long[] { 1 });
			CollectionAssert.AreEqual(new long[] { 2 },plan.ListSubmissions.Select(x => x.PatNum).ToList());
		}

		[TestMethod]
		public void Minors_AreScreenedThroughTheirGuarantor() {
			Patient guarantor=CareCreditTestData.CreateAdult(10);
			Patient minor=CareCreditTestData.CreateMinor(11,10);
			CareCreditBatchPlan plan=CareCreditBatchPlanner.Plan(new List<CareCreditBatchCandidate> { CreateCandidate(minor,guarantor) },
				EnumCareCreditBatch.NoBalance,null);
			Assert.AreEqual(10,plan.ListSubmissions.Single().PatNum);
			Assert.AreEqual("Ada",plan.ListSubmissions.Single().Request.Consumer.FirstName);
		}

		[TestMethod]
		public void MinorWithoutAdultGuarantor_IsABatchError() {
			Patient minor=CareCreditTestData.CreateMinor(11,11);
			CareCreditBatchPlan plan=CareCreditBatchPlanner.Plan(new List<CareCreditBatchCandidate> { CreateCandidate(minor,minor) },
				EnumCareCreditBatch.NoBalance,null);
			Assert.AreEqual(0,plan.ListSubmissions.Count);
			CareCreditBatchError error=plan.ListErrors.Single();
			Assert.AreEqual(11,error.PatNum);
			StringAssert.Contains(error.Description,"guarantor");
		}

		[TestMethod]
		public void MissingDemographics_AreABatchError() {
			Patient patient=CareCreditTestData.CreateAdult(1);
			patient.Zip="";
			patient.HmPhone="";
			CareCreditBatchPlan plan=CareCreditBatchPlanner.Plan(new List<CareCreditBatchCandidate> { CreateCandidate(patient) },EnumCareCreditBatch.NoBalance,null);
			Assert.AreEqual(0,plan.ListSubmissions.Count);
			CareCreditBatchError error=plan.ListErrors.Single();
			StringAssert.Contains(error.Description,"ZIP");
			StringAssert.Contains(error.Description,"phone");
			Assert.IsTrue(error.IsInvalidInput);
		}

		[TestMethod]
		public void LookupOnly_NeedsTheSsnAndSendsOnlyTheLastFour() {
			Patient patientNoSsn=CareCreditTestData.CreateAdult(1);
			patientNoSsn.SSN="";
			Patient patient=CareCreditTestData.CreateAdult(2);
			CareCreditBatchPlan plan=CareCreditBatchPlanner.Plan(new List<CareCreditBatchCandidate> { CreateCandidate(patientNoSsn),CreateCandidate(patient) },
				EnumCareCreditBatch.LookupOnly,null);
			StringAssert.Contains(plan.ListErrors.Single().Description,"SSN");
			CareCreditBatchSubmission submission=plan.ListSubmissions.Single();
			Assert.IsTrue(submission.Request.IsLookupOnly);
			Assert.AreEqual("6789",submission.Request.Consumer.SsnLast4);
			CareCreditBatchPlan planQs=CareCreditBatchPlanner.Plan(new List<CareCreditBatchCandidate> { CreateCandidate(patient) },EnumCareCreditBatch.NoBalance,null);
			Assert.AreEqual("",planQs.ListSubmissions.Single().Request.Consumer.SsnLast4,"QuickScreen sends no SSN digits.");
		}

		[TestMethod]
		public void OneRequestPerPatient_WithTheLargestRoundedFee() {
			Patient patient=CareCreditTestData.CreateAdult(1);
			CareCreditBatchPlan plan=CareCreditBatchPlanner.Plan(new List<CareCreditBatchCandidate> {
				CreateCandidate(patient,fee:120.4,dayOffset:1),CreateCandidate(patient,fee:250.5,dayOffset:2) },EnumCareCreditBatch.NoBalance,null);
			Assert.AreEqual(251,plan.ListSubmissions.Single().Request.EstimatedFeeAmount,0.001);
		}

		[TestMethod]
		public void ByProvider_MissingOrClosedMerchantIsABatchError() {
			CareCreditBatchPlan plan=CareCreditBatchPlanner.Plan(new List<CareCreditBatchCandidate> {
				CreateCandidate(CareCreditTestData.CreateAdult(1),merchant:""),
				CreateCandidate(CareCreditTestData.CreateAdult(2),merchant:"x"+CareCreditTestData.MERCHANT) },EnumCareCreditBatch.NoBalance,null);
			Assert.AreEqual(0,plan.ListSubmissions.Count);
			Assert.AreEqual(2,plan.ListErrors.Count);
			StringAssert.Contains(plan.ListErrors[0].Description,"DOC");
		}

		[TestMethod]
		public void Jobs_AreDiscoveredForTheVendorJobHost() {
			List<IVendorBackgroundJob> listJobs=VendorJobScheduler.DiscoverJobs();
			Assert.IsTrue(listJobs.Any(x => x.Name=="CareCreditPoll" && x.ProgramName==ProgramName.CareCredit));
			Assert.IsTrue(listJobs.Any(x => x.Name=="CareCreditBatch" && x.ProgramName==ProgramName.CareCredit));
		}

		[TestMethod]
		public void Jobs_DoNotRunWhileTheProgramIsDisabled() {
			int countShouldRun=0;
			List<IVendorBackgroundJob> listJobs=VendorJobScheduler.DiscoverJobs().Where(x => x.ProgramName==ProgramName.CareCredit).ToList();
			VendorJobScheduler scheduler=new VendorJobScheduler(listJobs,funcIsEnabled:x => false,lease:null,actionLog:x => countShouldRun++);
			Assert.AreEqual(0,scheduler.RunDueJobs().Count);
			Assert.AreEqual(0,countShouldRun,"Nothing ran and nothing failed.");
		}
	}
}
