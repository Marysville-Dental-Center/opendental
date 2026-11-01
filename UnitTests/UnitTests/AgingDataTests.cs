using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace UnitTests.AgingData_Tests {
	[TestClass]
	public class AgingDataTests:TestBase {

		///<summary>This method will execute only once, just before any tests in this class run.</summary>
		[ClassInitialize]
		public static void SetupClass(TestContext testContext) {
		}

		///<summary>This method will execute just before each test in this class.</summary>
		[TestInitialize]
		public void SetupTest() {
		}

		///<summary>This method will execute after each test in this class.</summary>
		[TestCleanup]
		public void TearDownTest() {
			PatientT.ClearPatientTable();
			StatementT.ClearStatementTable();
			ProcedureT.ClearProcedureTable();
		}

		///<summary>This method will execute only once, just after all tests in this class have run.</summary>
		[ClassCleanup]
		public static void TearDownClass() {
		}

		///<summary>Make sure that patients show up in the billing list (aging list) when there has been no statement.</summary>
		[TestMethod]
		public void AgingData_GetAgingData_PayPlanBillInAdvanceDays_NoStatement() {
			string suffix=MethodBase.GetCurrentMethod().Name;
			Patient patient=PatientT.CreatePatient(suffix);
			long provNum=ProviderT.CreateProvider(suffix);
			//Create a payment plan where the first charge date is in the future.
			DateTime dateTimePayPlanCharge=DateTime.Today.AddDays(5);
			DateTime dateStatement=DateTime.Today;
			PayPlanT.CreatePayPlan(patient.PatNum,1000,500,dateTimePayPlanCharge,provNum);
			//Make sure that the preference PayPlansBillInAdvanceDays is set to a day range that encompasses the first payment plan charge date.
			PrefT.UpdateLong(PrefName.PayPlansBillInAdvanceDays,10);
			SerializableDictionary<long,PatAgingData> dictPatAgingData=AgingData.GetAgingData(false,true,false,false,false,new List<long>(),0);
			//Assert that the patient has been returned due to owing money on a payment plan that falls within the "bill in advance days" preference.
			Assert.IsTrue(dictPatAgingData.ContainsKey(patient.PatNum),"No aging data was returned for the patient.");
			//Assert all pertinent PatAgingData for this unit test.
			Assert.IsNotNull(dictPatAgingData[patient.PatNum].ListPatAgingTransactions);
			PatAgingTransaction patAgingTransaction=dictPatAgingData[patient.PatNum].ListPatAgingTransactions
				.FirstOrDefault(x => x.TransactionType==PatAgingTransaction.TransactionTypes.PayPlanCharge);
			Assert.IsNotNull(patAgingTransaction);
			Assert.AreEqual(dateTimePayPlanCharge,patAgingTransaction.DateLastTrans);
			//Do NOT insert a statement.  This should cause the patient to be included in the PatAging list returned.
			//StatementT.CreateStatement(patient.PatNum,mode_:StatementMode.Mail,isSent:true,dateSent:dateStatement);
			SerializableDictionary<long,List<PatAgingTransaction>> dictPatAgingTrans=new SerializableDictionary<long, List<PatAgingTransaction>>();
			foreach(KeyValuePair<long,PatAgingData> kvp in dictPatAgingData) {
				dictPatAgingTrans[kvp.Key]=kvp.Value.ListPatAgingTransactions;
			}
			//Assert that GetDateLastTrans() returns the date we are expecting.
			Assert.AreEqual(DateTime.MinValue,AgingData.GetDateLastTrans(dictPatAgingTrans[patient.PatNum],dateStatement).Date);
			List<PatAging> listPatAging=Patients.GetAgingList("",DateTime.Today.AddMonths(-1),new List<long>(),false,false,0,false,false,new List<long>(),false,false,
				new List<long>(),new List<long>(),dictPatAgingTrans);
			//Assert that the patient will not get a new statement due to the statement that was created above.
			Assert.IsTrue(listPatAging.Any(x => x.PatNum==patient.PatNum),"The patient was supposed to be present in the AgingList.");
		}

		///<summary>Make sure that patients do not show up in the billing list (aging list) when there has been a statement within
		///the PayPlansBillInAdvanceDays date range (assume the statement within the date range encompasses the payment plan charge).</summary>
		[TestMethod]
		public void AgingData_GetAgingData_PayPlanBillInAdvanceDays_WithStatement() {
			string suffix=MethodBase.GetCurrentMethod().Name;
			Patient patient=PatientT.CreatePatient(suffix);
			long provNum=ProviderT.CreateProvider(suffix);
			//Create a payment plan where the first charge date is in the future.
			DateTime dateTimePayPlanCharge=DateTime.Today.AddDays(5);
			DateTime dateStatement=DateTime.Today;
			PayPlanT.CreatePayPlan(patient.PatNum,1000,500,dateTimePayPlanCharge,provNum);
			//Make sure that the preference PayPlansBillInAdvanceDays is set to a day range that encompasses the first payment plan charge date.
			PrefT.UpdateLong(PrefName.PayPlansBillInAdvanceDays,10);
			SerializableDictionary<long,PatAgingData> dictPatAgingData=AgingData.GetAgingData(false,true,false,false,false,new List<long>(),0);
			//Assert that the patient has been returned due to owing money on a payment plan that falls within the "bill in advance days" preference.
			Assert.IsTrue(dictPatAgingData.ContainsKey(patient.PatNum),"No aging data was returned for the patient.");
			//Assert all pertinent PatAgingData for this unit test.
			Assert.IsNotNull(dictPatAgingData[patient.PatNum].ListPatAgingTransactions);
			PatAgingTransaction patAgingTransaction=dictPatAgingData[patient.PatNum].ListPatAgingTransactions
				.FirstOrDefault(x => x.TransactionType==PatAgingTransaction.TransactionTypes.PayPlanCharge);
			Assert.IsNotNull(patAgingTransaction);
			Assert.AreEqual(dateTimePayPlanCharge,patAgingTransaction.DateLastTrans);
			//Insert a statement that was sent today.  This should cause the patient to be excluded from the PatAging list returned.
			StatementT.CreateStatement(patient.PatNum,mode_:StatementMode.Mail,isSent:true,dateSent:dateStatement);
			SerializableDictionary<long,List<PatAgingTransaction>> dictPatAgingTrans=new SerializableDictionary<long, List<PatAgingTransaction>>();
			foreach(KeyValuePair<long,PatAgingData> kvp in dictPatAgingData) {
				dictPatAgingTrans[kvp.Key]=kvp.Value.ListPatAgingTransactions;
			}
			//Assert that GetDateLastTrans() returns the date we are expecting.
			Assert.AreEqual(DateTime.MinValue,AgingData.GetDateLastTrans(dictPatAgingTrans[patient.PatNum],dateStatement).Date);
			List<PatAging> listPatAging=Patients.GetAgingList("",DateTime.Today.AddMonths(-1),new List<long>(),false,false,0,false,false,new List<long>(),false,false,
				new List<long>(),new List<long>(),dictPatAgingTrans);
			//Assert that the patient will not get a new statement due to the statement that was created above.
			Assert.IsFalse(listPatAging.Any(x => x.PatNum==patient.PatNum),"The patient was not supposed to be present in the AgingList.");
		}

		///<summary>Make sure that patients show up in the billing list (aging list) when there has been a statement within the PayPlansBillInAdvanceDays
		///date range (they should cancel each other out) BUT there is a completed procedure (or claimproc) AFTER the statement.</summary>
		[TestMethod]
		public void AgingData_GetAgingData_PayPlanBillInAdvanceDays_WithPendingProc() {
			string suffix=MethodBase.GetCurrentMethod().Name;
			Patient patient=PatientT.CreatePatient(suffix);
			long provNum=ProviderT.CreateProvider(suffix);
			DateTime datePayPlan=DateTime.Today.AddDays(5);
			DateTime datePayPlanCreate=DateTime.Today.AddMonths(-1);//Payment Plan was created a month ago.
			DateTime dateProc=DateTime.Today;
			DateTime dateStatement=DateTime.Today.AddDays(-5);
			//Create a payment plan where the first charge date in the future.
			PayPlanT.CreatePayPlan(patient.PatNum,1000,500,datePayPlan,provNum);
			//Create a completed procedure that was completed today, before the first payplan charge date.
			ProcedureT.CreateProcedure(patient,"D1100",ProcStat.C,"",5,dateProc);
			//Insert a statement that was sent during the "bill in advance days" for the payment plan charge above.
			StatementT.CreateStatement(patient.PatNum,mode_:StatementMode.Mail,isSent:true,dateSent:dateStatement);
			//Make sure that the preference PayPlansBillInAdvanceDays is set to a day range that encompasses the first payment plan charge date.
			PrefT.UpdateLong(PrefName.PayPlansBillInAdvanceDays,10);
			//This scenario is exploiting the fact that the statement created 5 days ago was technically created for the payment plan (in advance).
			//Because of this fact, the patient shouldn't show up in the billing list until something new happens after the statement date.
			//The procedure that was completed today should cause the patient to show up in the billing list (something new happened).
			SerializableDictionary<long,PatAgingData> dictPatAgingData=AgingData.GetAgingData(false,true,false,false,false,new List<long>(),0);
			//Assert that the patient has been returned due to the completed procedure.
			Assert.IsTrue(dictPatAgingData.ContainsKey(patient.PatNum),"No aging data was returned for the patient.");
			//Assert all pertinent PatAgingData for this unit test.
			Assert.IsNotNull(dictPatAgingData[patient.PatNum].ListPatAgingTransactions);
			PatAgingTransaction patAgingTransactionPP=dictPatAgingData[patient.PatNum].ListPatAgingTransactions
				.FirstOrDefault(x => x.TransactionType==PatAgingTransaction.TransactionTypes.PayPlanCharge);
			//Act like the payment plan was created a month ago.
			patAgingTransactionPP.SecDateTEntryTrans=datePayPlanCreate;
			PatAgingTransaction patAgingTransactionProc=dictPatAgingData[patient.PatNum].ListPatAgingTransactions
				.FirstOrDefault(x => x.TransactionType==PatAgingTransaction.TransactionTypes.Procedure);
			Assert.IsNotNull(patAgingTransactionPP);
			Assert.IsNotNull(patAgingTransactionProc);
			Assert.AreEqual(datePayPlan,patAgingTransactionPP.DateLastTrans);
			Assert.AreEqual(dateProc,patAgingTransactionProc.DateLastTrans);
			SerializableDictionary<long,List<PatAgingTransaction>> dictPatAgingTrans=new SerializableDictionary<long,List<PatAgingTransaction>>();
			foreach(KeyValuePair<long,PatAgingData> kvp in dictPatAgingData) {
				dictPatAgingTrans[kvp.Key]=kvp.Value.ListPatAgingTransactions;
			}
			//The last transaction date should be the procedure date and not the pay plan charge date (even though pay plan is later).
			Assert.AreEqual(dateProc,AgingData.GetDateLastTrans(dictPatAgingTrans[patient.PatNum],dateStatement).Date);
			List<PatAging> listPatAging=Patients.GetAgingList("",DateTime.Today.AddMonths(-1),new List<long>(),false,false,0,false,false,new List<long>(),
				false,false,new List<long>(),new List<long>(),dictPatAgingTrans);
			//Assert that the patient has been flagged to get a new statement due to procedure that was completed above.
			Assert.IsTrue(listPatAging.Any(x => x.PatNum==patient.PatNum),"The expected patient was not present in the AgingList.");
		}

		///<summary>Make sure that patients show up in the billing list (aging list) when there has been a statement within the PayPlansBillInAdvanceDays
		///date range (they should cancel each other out) BUT there is a completed procedure (or claimproc) AFTER the statement.</summary>
		[TestMethod]
		public void AgingData_GetAgingData_PayPlanBillInAdvanceDays_WithNewPayPlan() {
			string suffix=MethodBase.GetCurrentMethod().Name;
			Patient patient=PatientT.CreatePatient(suffix);
			long provNum=ProviderT.CreateProvider(suffix);
			DateTime datePayPlanCharge=DateTime.Today.AddDays(5);
			DateTime datePayPlanCreate=DateTime.Today;//The payment plan that we are about to create will automatically have this date as the SecTDateEntry
			DateTime dateProc=DateTime.Today.AddDays(-1);
			DateTime dateStatement=DateTime.Today.AddDays(-1);
			//Create a payment plan where the first charge date in the future.
			PayPlanT.CreatePayPlan(patient.PatNum,1000,500,datePayPlanCharge,provNum);
			//Create a completed procedure that was completed yesterday, before the first payplan charge date AND before the payment plan creation date.
			ProcedureT.CreateProcedure(patient,"D1100",ProcStat.C,"",5,dateProc);
			//Insert a statement that was sent during the "bill in advance days" for the payment plan charge AND before the payment plan creation date.
			StatementT.CreateStatement(patient.PatNum,mode_:StatementMode.Mail,isSent:true,dateSent:dateStatement);
			//Make sure that the preference PayPlansBillInAdvanceDays is set to a day range that encompasses the first payment plan charge date.
			PrefT.UpdateLong(PrefName.PayPlansBillInAdvanceDays,10);
			//This scenario is exploiting the fact that the statement created yesterday was NOT technically created for the payment plan (in advance).
			//Because of this fact, the patient should show up in the billing list because something new has happened after the statement date.
			//The new payment plan should not be associated to the previous statement due to the SecTDateEntry.
			SerializableDictionary<long,PatAgingData> dictPatAgingData=AgingData.GetAgingData(false,true,false,false,false,new List<long>(),0);
			//Assert that the patient has been returned due to owing money on the payment plan that was created.
			Assert.IsTrue(dictPatAgingData.ContainsKey(patient.PatNum),"No aging data was returned for the patient.");
			Assert.IsNotNull(dictPatAgingData[patient.PatNum].ListPatAgingTransactions);
			PatAgingTransaction patAgingTransactionPP=dictPatAgingData[patient.PatNum].ListPatAgingTransactions
				.FirstOrDefault(x => x.TransactionType==PatAgingTransaction.TransactionTypes.PayPlanCharge);
			PatAgingTransaction patAgingTransactionProc=dictPatAgingData[patient.PatNum].ListPatAgingTransactions
				.FirstOrDefault(x => x.TransactionType==PatAgingTransaction.TransactionTypes.Procedure);
			Assert.IsNotNull(patAgingTransactionPP);
			Assert.IsNotNull(patAgingTransactionProc);
			Assert.AreEqual(datePayPlanCharge,patAgingTransactionPP.DateLastTrans);
			Assert.AreEqual(dateProc,patAgingTransactionProc.DateLastTrans);
			SerializableDictionary<long,List<PatAgingTransaction>> dictPatAgingTrans=new SerializableDictionary<long,List<PatAgingTransaction>>();
			foreach(KeyValuePair<long,PatAgingData> kvp in dictPatAgingData) {
				dictPatAgingTrans[kvp.Key]=kvp.Value.ListPatAgingTransactions;
			}
			//The last transaction date should be the charge date of the pay plan charge which indicates that the statement doesn't apply
			//to the payment plan because the payment plan was created AFTER the statement that just so happens to fall within the "bill in advance days".
			Assert.AreEqual(datePayPlanCharge,AgingData.GetDateLastTrans(dictPatAgingTrans[patient.PatNum],dateStatement).Date);
			List<PatAging> listPatAging=Patients.GetAgingList("",DateTime.Today.AddMonths(-1),new List<long>(),false,false,0,false,false,new List<long>(),
				false,false,new List<long>(),new List<long>(),dictPatAgingTrans);
			Assert.IsTrue(listPatAging.Any(x => x.PatNum==patient.PatNum),"The expected patient was not present in the AgingList.");
		}

		[TestMethod]
		public void AgingData_GetAgingData_DynamicPayPlanExplicitAdjustment() {
			PrefT.UpdateInt(PrefName.PayPlansVersion,(int)PayPlanVersions.AgeCreditsAndDebits);
			string suffix=MethodBase.GetCurrentMethod().Name;
			long provNum=ProviderT.CreateProvider($"{suffix}-1");
			Patient pat=PatientT.CreatePatient(suffix);
			//Create a procedure made due during the 61_90 bucket and explicitly attach an adjustment to said procedure.
			DateTime procDate=DateTime.Today.AddDays(-70);
			Procedure proc=ProcedureT.CreateProcedure(pat,"DPPEA1",ProcStat.C,"",100,procDate: procDate,provNum: provNum);
			//Adjustments that are explicitly linked to procedures should inflate the value of the procedure.
			//Create an adjustment made due during the 31_60 bucket 
			DateTime adjDate=DateTime.Today.AddDays(-40);
			Adjustment adj=AdjustmentT.MakeAdjustment(pat.PatNum,50,adjDate: adjDate,procNum: proc.ProcNum,provNum: provNum);
			//Create a dynamic payment plan that is only attached to the procedure.
			//Act like the payment plan was created and made due during the 0_30 bucket.
			DateTime datePayPlan=DateTime.Today.AddDays(-10);
			PayPlan dynamicPayPlan=PayPlanT.CreateDynamicPaymentPlan(pat.PatNum,pat.Guarantor,datePayPlan,0,0,0,new List<Procedure>(){ proc },
				new List<Adjustment>{ },frequency:PayPlanFrequency.Monthly,payCount:1);
			Ledgers.ComputeAging(pat.Guarantor,DateTime.Today);
			List<PatAging> listPatAgingSimple=Patients.GetAgingListSimple(null,new List<long>(){ pat.Guarantor });
			Assert.AreEqual(1,listPatAgingSimple.Count);
			Assert.AreEqual(1,listPatAgingSimple.Count(x => x.BalTotal==150
				&& x.Bal_0_30==150  //Payment plan - all due as of 10 days ago
				&& x.Bal_31_60==0   //Adjustment is technically in this bucket but the credit from the payment plan is applied to this bucket.
				&& x.Bal_61_90==0));//Procedure is technically in this bucket but the credit from the payment plan is applied to this bucket.
		}

		[TestMethod]
		public void AgingData_GetAgingData_DynamicPaymentPlanImplicitAdjustmentIgnore() {
			PrefT.UpdateInt(PrefName.PayPlansVersion,(int)PayPlanVersions.AgeCreditsAndDebits);
			string suffix=MethodBase.GetCurrentMethod().Name;
			long provNum=ProviderT.CreateProvider($"{suffix}-1");
			long provNum2=ProviderT.CreateProvider($"{suffix}-2");
			Patient pat=PatientT.CreatePatient(suffix);
			//Create a procedure made due during the 61_90 bucket and implicitly attach an adjustment to said procedure.
			DateTime procDate=DateTime.Today.AddDays(-70);
			Procedure proc=ProcedureT.CreateProcedure(pat,"DPPIA1",ProcStat.C,"",100,procDate,provNum:provNum);
			//Adjustments that are implicitly linked to procedures should NOT inflate the value of the procedure.
			//Create an adjustment made due during the 31_60 bucket 
			DateTime adjDate=DateTime.Today.AddDays(-40);
			Adjustment adj=AdjustmentT.MakeAdjustment(pat.PatNum,50,adjDate: adjDate,procNum: proc.ProcNum,provNum: provNum2);
			//Create a dynamic payment plan that is only attached to the procedure.
			//Act like the payment plan was created and made due during the 0_30 bucket.
			DateTime datePayPlan=DateTime.Today.AddDays(-10);
			PayPlan dynamicPayPlan=PayPlanT.CreateDynamicPaymentPlan(pat.PatNum,pat.Guarantor,datePayPlan,0,0,0,new List<Procedure>(){ proc },new List<Adjustment>{ },
				frequency:PayPlanFrequency.Monthly,payCount:1);
			Ledgers.ComputeAging(pat.Guarantor,DateTime.Today);
			List<PatAging> listPatAgingSimple=Patients.GetAgingListSimple(null,new List<long>(){ pat.Guarantor });
			Assert.AreEqual(1,listPatAgingSimple.Count);
			Assert.AreEqual(1,listPatAgingSimple.Count(x => x.BalTotal==150
				&& x.Bal_0_30==100  //Payment plan - all due as of 10 days ago
				&& x.Bal_31_60==50  //Adjustment is technically in this bucket which there isn't enough credit from the payment plan to cover.
				&& x.Bal_61_90==0));//Procedure is technically in this bucket but the credit from the payment plan is applied to this bucket.
		}

		[TestMethod]
		public void AgingData_GetAgingData_DynamicPaymentPlanImplicitAdjustmentAttached() {
			PrefT.UpdateInt(PrefName.PayPlansVersion,(int)PayPlanVersions.AgeCreditsAndDebits);
			string suffix=MethodBase.GetCurrentMethod().Name;
			long provNum=ProviderT.CreateProvider($"{suffix}-1");
			long provNum2=ProviderT.CreateProvider($"{suffix}-2");
			Patient pat=PatientT.CreatePatient(suffix);
			//Create a procedure made due during the 61_90 bucket and implicitly attach an adjustment to said procedure.
			DateTime procDate=DateTime.Today.AddDays(-70);
			Procedure proc=ProcedureT.CreateProcedure(pat,"DPPIA1",ProcStat.C,"",100,procDate,provNum:provNum);
			//Adjustments that are implicitly linked to procedures should NOT inflate the value of the procedure.
			//Create an adjustment made due during the 31_60 bucket 
			DateTime adjDate=DateTime.Today.AddDays(-40);
			Adjustment adj=AdjustmentT.MakeAdjustment(pat.PatNum,50,adjDate: adjDate,procNum: proc.ProcNum,provNum: provNum2);
			//Create a dynamic payment plan that is attached to the procedure and the adjustment.
			//Act like the payment plan was created and made due during the 0_30 bucket.
			DateTime datePayPlan=DateTime.Today.AddDays(-10);
			PayPlan dynamicPayPlan=PayPlanT.CreateDynamicPaymentPlan(pat.PatNum,pat.Guarantor,datePayPlan,0,0,0,new List<Procedure>(){ proc },
				new List<Adjustment>{ adj },frequency:PayPlanFrequency.Monthly,payCount:1);
			Ledgers.ComputeAging(pat.Guarantor,DateTime.Today);
			List<PatAging> listPatAgingSimple=Patients.GetAgingListSimple(null,new List<long>(){ pat.Guarantor });
			Assert.AreEqual(1,listPatAgingSimple.Count);
			Assert.AreEqual(1,listPatAgingSimple.Count(x => x.BalTotal==150
				&& x.Bal_0_30==150  //Payment plan - all due as of 10 days ago
				&& x.Bal_31_60==0   //Adjustment is technically in this bucket but the credit from the payment plan is applied to this bucket.
				&& x.Bal_61_90==0));//Procedure is technically in this bucket but the credit from the payment plan is applied to this bucket.
		}

		///<summary>Tests that the patients returned in GetAgingList() and GetAgingListSimple() are identical.</summary>
		[TestMethod]
		public void AgingData_GetAgingList_GetAgingListSimple_Compare() {
			PatientT.CreatePatWithProcAndStatement(2,DateTime.Today,false,PatientStatus.Patient,StatementMode.Mail,false,50);
			//Compare the results of GetAgingList() and GetAgingListSimple() methods (output should be identical)
			List<PatAging> listPatAging=Patients.GetAgingList("",DateTime.Today,new List<long>(),false,false,0,false,false,new List<long>(),false,false
				,new List<long>(),new List<long>(),new SerializableDictionary<long,List<PatAgingTransaction>>(),false,false,true);
			List<PatAging> listPatAgingSimple=Patients.GetAgingListSimple(new List<long>(),new List<long>());//Ordered by PatNum, for thread concurrency
			Assert.IsTrue(listPatAging.Count!=0);
			Assert.IsTrue(listPatAgingSimple.Count!=0);
			Assert.AreEqual(listPatAging.Count,listPatAgingSimple.Count);
			//Ensure both methods return the exact same patients.
			for(int i=0;i<listPatAging.Count;i++) {
				Assert.AreEqual(listPatAging[i].PatNum,listPatAgingSimple[i].PatNum);
			}
		}

		/// <summary>Tests the excludeAddr behavior in GetAgingList().</summary>
		[TestMethod]
		public void AgingData_GetAgingList_ExcludeBadAddress() {
			PatientT.CreatePatWithProcAndStatement(1,DateTime.Today,false,PatientStatus.Patient,StatementMode.Mail,false,50);//Bad Address (no zip code)
			PatientT.CreatePatWithProcAndStatement(1,DateTime.Today,true,PatientStatus.Patient,StatementMode.Mail,false,50);//Valid Address
			List<PatAging> listPatAging=Patients.GetAgingList("",DateTime.Today,new List<long>(),true,false,0
				,false,false,new List<long>(),false,false,new List<long>(),
				new List<long>(),new SerializableDictionary<long,List<PatAgingTransaction>>(),false,false,true);
			Assert.AreEqual(listPatAging.Count,1);
			Assert.IsTrue(listPatAging[0].Zip!="");
		}

		/// <summary>Tests the excludeInactive bool in GetAgingList().</summary>
		[TestMethod]
		public void AgingData_GetAgingList_ExcludeInactiveFamilies() {
			PatientT.CreatePatWithProcAndStatement(1,DateTime.Today,false,PatientStatus.Inactive,StatementMode.Mail,false,50);//Inactive patient
			PatientT.CreatePatWithProcAndStatement(1,DateTime.Today,false,PatientStatus.Patient,StatementMode.Mail,false,50);//Non-inactive patient
			List<PatAging> listPatAging=Patients.GetAgingList("",DateTime.Today,new List<long>(),false,false,0
				,true,false,new List<long>(),false,false,new List<long>(),
				new List<long>(),new SerializableDictionary<long,List<PatAgingTransaction>>(),false,false,true);
			Assert.AreEqual(listPatAging.Count,1);
			Assert.IsTrue(listPatAging[0].PatStatus!=PatientStatus.Inactive);
		}

		///<summary>Tests the excludeInsPending parameter in GetAgingList().</summary>
		[TestMethod]
		public void AgingData_GetAgingList_ExcludeInsPending() {
			PatientT.CreatePatWithProcAndStatement(2,DateTime.Today,false,PatientStatus.Patient,StatementMode.Mail,false,50);
			SerializableDictionary<long,PatAgingData> dictPatAgingData=AgingData.GetAgingData(false,true,true,false,false,new List<long>(),0);
			dictPatAgingData.ToList()[0].Value.HasPendingIns=true;//Set the first Aging Patient to have pending insurance
			List<long> listPendingInsPatNums=new List<long>();			
			foreach(KeyValuePair<long,PatAgingData> kvp in dictPatAgingData) {//Grab the patnum since GetAgingList() requires a list of patnums
				if(kvp.Value.HasPendingIns) {
					listPendingInsPatNums.Add(kvp.Key);
				}
			}
			List<PatAging> listPatAging=Patients.GetAgingList("",DateTime.Today,new List<long>(),false,false,0
				,true,false,new List<long>(),false,false,listPendingInsPatNums,
				new List<long>(),new SerializableDictionary<long,List<PatAgingTransaction>>(),false,false,true);
			Assert.AreEqual(listPatAging.Count,1);
			Assert.IsTrue(listPatAging[0].HasInsPending==false);
		}

		///<summary>Tests the daysExcludeInsPending in GetAgingData() for excluding any patients with pending insurance.</summary>
		[TestMethod]
		public void AgingData_GetAgingData_ExcludeInsPendingZeroDays() {
			Patient patientWeek=PatientT.CreatePatWithProcAndClaim(DateTime.Today.AddDays(-7),false,PatientStatus.Patient,false,50);
			Patient patientThreeWeeks=PatientT.CreatePatWithProcAndClaim(DateTime.Today.AddDays(-21),false,PatientStatus.Patient,false,50);
			SerializableDictionary<long,PatAgingData> dictPatAgingData=AgingData.GetAgingData(false,true,true,false,false,new List<long>(),0);
			List<long> listPendingInsPatNums=new List<long>();
			for(int i=0;i<dictPatAgingData.Count;i++) {
				KeyValuePair<long,PatAgingData> keyValuePair=dictPatAgingData.ElementAt(i);
				if(keyValuePair.Value.HasPendingIns) {
					listPendingInsPatNums.Add(keyValuePair.Key);
				}
			}
			Assert.AreEqual(2,listPendingInsPatNums.Count);
			Assert.IsTrue(listPendingInsPatNums.Contains(patientWeek.PatNum));
			Assert.IsTrue(listPendingInsPatNums.Contains(patientThreeWeeks.PatNum));
		}

		///<summary>Tests the daysExcludeInsPending in GetAgingData() for only excluding patients with insurance pending less than 1 day.</summary>
		[TestMethod]
		public void AgingData_GetAgingData_ExcludeInsPendingOneDay() {
			Patient patientWeek=PatientT.CreatePatWithProcAndClaim(DateTime.Today.AddDays(-7),false,PatientStatus.Patient,false,50);
			Patient patientThreeWeeks=PatientT.CreatePatWithProcAndClaim(DateTime.Today.AddDays(-21),false,PatientStatus.Patient,false,50);
			SerializableDictionary<long,PatAgingData> dictPatAgingData=AgingData.GetAgingData(false,true,true,false,false,new List<long>(),1);
			List<long> listPendingInsPatNums=new List<long>();
			for(int i=0;i<dictPatAgingData.Count;i++) {
				KeyValuePair<long,PatAgingData> keyValuePair=dictPatAgingData.ElementAt(i);
				if(keyValuePair.Value.HasPendingIns) {
					listPendingInsPatNums.Add(keyValuePair.Key);
				}
			}
			Assert.AreEqual(0,listPendingInsPatNums.Count);
		}

		///<summary>Tests the daysExcludeInsPending in GetAgingData() for only excluding patients with insurance pending less than 2 weeks.</summary>
		[TestMethod]
		public void AgingData_GetAgingData_ExcludeInsPendingTwoWeeks() {
			Patient patientWeek=PatientT.CreatePatWithProcAndClaim(DateTime.Today.AddDays(-7),false,PatientStatus.Patient,false,50);
			Patient patientThreeWeeks=PatientT.CreatePatWithProcAndClaim(DateTime.Today.AddDays(-21),false,PatientStatus.Patient,false,50);
			SerializableDictionary<long,PatAgingData> dictPatAgingData=AgingData.GetAgingData(false,true,true,false,false,new List<long>(),14);
			List<long> listPendingInsPatNums=new List<long>();
			for(int i=0;i<dictPatAgingData.Count;i++) {
				KeyValuePair<long,PatAgingData> keyValuePair=dictPatAgingData.ElementAt(i);
				if(keyValuePair.Value.HasPendingIns) {
					listPendingInsPatNums.Add(keyValuePair.Key);
				}
			}
			Assert.AreEqual(1,listPendingInsPatNums.Count);
			Assert.IsTrue(listPendingInsPatNums.Contains(patientWeek.PatNum));
		}

		///<summary>Tests the daysExcludeInsPending in GetAgingData() for only excluding patients with insurance pending less than 31 days.</summary>
		[TestMethod]
		public void AgingData_GetAgingData_ExcludeInsPendingOneMonth() {
			Patient patientWeek=PatientT.CreatePatWithProcAndClaim(DateTime.Today.AddDays(-7),false,PatientStatus.Patient,false,50);
			Patient patientThreeWeeks=PatientT.CreatePatWithProcAndClaim(DateTime.Today.AddDays(-21),false,PatientStatus.Patient,false,50);
			SerializableDictionary<long,PatAgingData> dictPatAgingData=AgingData.GetAgingData(false,true,true,false,false,new List<long>(),31);
			List<long> listPendingInsPatNums=new List<long>();
			for(int i=0;i<dictPatAgingData.Count;i++) {
				KeyValuePair<long,PatAgingData> keyValuePair=dictPatAgingData.ElementAt(i);
				if(keyValuePair.Value.HasPendingIns) {
					listPendingInsPatNums.Add(keyValuePair.Key);
				}
			}
			Assert.AreEqual(2,listPendingInsPatNums.Count);
			Assert.IsTrue(listPendingInsPatNums.Contains(patientWeek.PatNum));
			Assert.IsTrue(listPendingInsPatNums.Contains(patientThreeWeeks.PatNum));
		}

		///<summary>Tests the daysExcludeInsPending in GetAgingData() for excluding patients with many pending payments, but one falling in the exclusion timeframe.</summary>
		[TestMethod]
		public void AgingData_GetAgingData_ExcludeInsPendingOneOfMany() {
			Patient patientWithMany=PatientT.CreatePatient("",0,0,"","",ContactMethod.Email,"","","",default(DateTime),0,0);
			Carrier carrier=CarrierT.CreateCarrier("F31366");
			InsPlan insPlan=InsPlanT.CreateInsPlan(carrier.CarrierNum);
			InsSub insSub=InsSubT.CreateInsSub(patientWithMany.PatNum,insPlan.PlanNum);
			PatPlan patPlan=PatPlanT.CreatePatPlan(1,patientWithMany.PatNum,insSub.InsSubNum);
			for(int i=1;i<=3;i++) {
				Procedure procedure=ProcedureT.CreateProcedure(patientWithMany,"D1100",ProcStat.C,"",50,DateTime.Today.AddDays(-7*i));
				Claim claim=ClaimT.CreateClaim("P",new List<PatPlan>(){patPlan},new List<InsPlan>(),new List<ClaimProc>(), new List<Procedure>() {procedure},patientWithMany,new List<Procedure>() {procedure},new List<Benefit>(),new List<InsSub>());
				claim.DateSent=DateTime.Today.AddDays(-7*i);
				Claims.Update(claim);
			}
			Patient patientWeek=PatientT.CreatePatWithProcAndClaim(DateTime.Today.AddDays(-7),false,PatientStatus.Patient,false,50);
			Patient patientThreeWeeks=PatientT.CreatePatWithProcAndClaim(DateTime.Today.AddDays(-21),false,PatientStatus.Patient,false,50);
			SerializableDictionary<long,PatAgingData> dictPatAgingData=AgingData.GetAgingData(false,true,true,false,false,new List<long>(),10);
			List<long> listPendingInsPatNums=new List<long>();
			for(int i=0;i<dictPatAgingData.Count;i++) {
				KeyValuePair<long,PatAgingData> keyValuePair=dictPatAgingData.ElementAt(i);
				if(keyValuePair.Value.HasPendingIns) {
					listPendingInsPatNums.Add(keyValuePair.Key);
				}
			}
			Assert.AreEqual(2,listPendingInsPatNums.Count);
			Assert.IsTrue(listPendingInsPatNums.Contains(patientWeek.PatNum));
			Assert.IsTrue(listPendingInsPatNums.Contains(patientWithMany.PatNum));
		}

		///<summary>Tests the daysExcludeInsPending in GetAgingData() for excluding patients with many pending payments, all but one falling in the exclusion timeframe.</summary>
		[TestMethod]
		public void AgingData_GetAgingData_ExcludeInsPendingMany() {
			Patient patientWithMany=PatientT.CreatePatient("",0,0,"","",ContactMethod.Email,"","","",default(DateTime),0,0);
			Carrier carrier=CarrierT.CreateCarrier("F31366");
			InsPlan insPlan=InsPlanT.CreateInsPlan(carrier.CarrierNum);
			InsSub insSub=InsSubT.CreateInsSub(patientWithMany.PatNum,insPlan.PlanNum);
			PatPlan patPlan=PatPlanT.CreatePatPlan(1,patientWithMany.PatNum,insSub.InsSubNum);
			for(int i=0;i<=4;i++) {
				Procedure procedure=ProcedureT.CreateProcedure(patientWithMany,"D1100",ProcStat.C,"",50,DateTime.Today.AddDays(-7*i));
				Claim claim=ClaimT.CreateClaim("P",new List<PatPlan>(){patPlan},new List<InsPlan>(),new List<ClaimProc>(), new List<Procedure>() {procedure},patientWithMany,new List<Procedure>() {procedure},new List<Benefit>(),new List<InsSub>());
				claim.DateSent=DateTime.Today.AddDays(-7*i);
				Claims.Update(claim);
			}
			Patient patientWeek=PatientT.CreatePatWithProcAndClaim(DateTime.Today.AddDays(-7),false,PatientStatus.Patient,false,50);
			Patient patientThreeWeeks=PatientT.CreatePatWithProcAndClaim(DateTime.Today.AddDays(-21),false,PatientStatus.Patient,false,50);
			SerializableDictionary<long,PatAgingData> dictPatAgingData=AgingData.GetAgingData(false,true,true,false,false,new List<long>(),25);
			List<long> listPendingInsPatNums=new List<long>();
			for(int i=0;i<dictPatAgingData.Count;i++) {
				KeyValuePair<long,PatAgingData> keyValuePair=dictPatAgingData.ElementAt(i);
				if(keyValuePair.Value.HasPendingIns) {
					listPendingInsPatNums.Add(keyValuePair.Key);
				}
			}
			Assert.AreEqual(3,listPendingInsPatNums.Count); //PatientWithMany should only be counted once.
			Assert.IsTrue(listPendingInsPatNums.Contains(patientWeek.PatNum));
			Assert.IsTrue(listPendingInsPatNums.Contains(patientThreeWeeks.PatNum));
			Assert.IsTrue(listPendingInsPatNums.Contains(patientWithMany.PatNum));
		}

		/// <summary>Tests the ignoreInPerson parameter in GetAgingList().</summary>
		[TestMethod]
		public void AgingData_GetAgingList_ExcludeInPersonStatements() {
			PatientT.CreatePatWithProcAndStatement(1,DateTime.Today,false,PatientStatus.Patient,StatementMode.InPerson,false,50);//In Person Statement
			PatientT.CreatePatWithProcAndStatement(1,DateTime.Today,false,PatientStatus.Patient,StatementMode.Mail,false,50);//Mail Statement
			List<PatAging> listPatAging=Patients.GetAgingList("",DateTime.Today,new List<long>(),false,false,0
				,false,false,new List<long>(),false,false,new List<long>(),
				new List<long>(),new SerializableDictionary<long,List<PatAgingTransaction>>(),false,true,true);
			Assert.AreEqual(listPatAging.Count,2);
			listPatAging=Patients.GetAgingList("",DateTime.Today,new List<long>(),false,false,0
				,false,true,new List<long>(),false,false,new List<long>(),
				new List<long>(),new SerializableDictionary<long,List<PatAgingTransaction>>(),false,true,true);
			Assert.AreEqual(listPatAging.Count,1);
			Statement statement=StatementT.GetStatementsForPat(listPatAging[0].PatNum).First();
			Assert.IsTrue(statement.Mode_!=StatementMode.InPerson);
		}

		/// <summary>Filter out accounts without Truth In Lending.</summary>
		[TestMethod]
		public void AgingData_GetAgingList_ExcludeNoTruthInLending() {
			PatientT.CreatePatWithProcAndStatement(1,DateTime.Today,false,PatientStatus.Patient,StatementMode.Mail,false,50);//No Signed Truth in Lending
			PatientT.CreatePatWithProcAndStatement(1,DateTime.Today,false,PatientStatus.Patient,StatementMode.Mail,true,50);//Signed Truth in Lending
			List<PatAging> listPatAging=Patients.GetAgingList("",DateTime.Today,new List<long>(),false,false,0
				,false,false,new List<long>(),false,false,new List<long>(),
				new List<long>(),new SerializableDictionary<long,List<PatAgingTransaction>>(),true,false,true);
			Assert.AreNotEqual(listPatAging.Count,0);
			listPatAging.ForEach(x=>Assert.IsTrue(x.HasSignedTil));
		}

		/// <summary>Tests the excludeLessThan parameter of GetAgingList().</summary>
		[TestMethod]
		public void AgingData_GetAgingList_ExcludeBalLessThan() {
			double balMin=50;
			PatientT.CreatePatWithProcAndStatement(1,DateTime.Today,false,PatientStatus.Patient,StatementMode.Mail,false,balMin-1);//Less than Balance
			PatientT.CreatePatWithProcAndStatement(1,DateTime.Today,false,PatientStatus.Patient,StatementMode.Mail,false,balMin);//Equal to Balance
			PatientT.CreatePatWithProcAndStatement(1,DateTime.Today,false,PatientStatus.Patient,StatementMode.Mail,false,balMin+1);//Greater than Balance
			List<PatAging> listPatAging=Patients.GetAgingList("",DateTime.Today,new List<long>(),false,false,balMin,false,false,new List<long>()
				,false,false,new List<long>(),new List<long>(),new SerializableDictionary<long,List<PatAgingTransaction>>(),false,false,true);
			Assert.AreEqual(listPatAging.Count,2);
			Assert.IsTrue(listPatAging[0].BalTotal>=balMin);
			Assert.IsTrue(listPatAging[1].BalTotal>=balMin);
		}

		/// <summary>Tests the parameter filterSinceLastFinancialStatement in GetAgingList().</summary>
		[TestMethod]
		public void AgingData_GetAgingList_FilterAccountsNotBilledSince() {
			DateTime dateTimeSinceDate=DateTime.Today;
			//Create dummy Patients with sent dates from today and the last two days
			PatientT.CreatePatWithProcAndStatement(1,dateTimeSinceDate.AddDays(-1),false,PatientStatus.Patient,StatementMode.Mail,false,50);
			PatientT.CreatePatWithProcAndStatement(1,dateTimeSinceDate,false,PatientStatus.Patient,StatementMode.Mail,false,50);
			PatientT.CreatePatWithProcAndStatement(1,dateTimeSinceDate.AddDays(1),false,PatientStatus.Patient,StatementMode.Mail,false,50);
			List<PatAging> listPatAging=Patients.GetAgingList("",dateTimeSinceDate,new List<long>(),false,false,0
				,false,false,new List<long>(),false,false,new List<long>(),
				new List<long>(),new SerializableDictionary<long,List<PatAgingTransaction>>(),false,true,true);
			Assert.AreEqual(listPatAging.Count,2);
			Assert.IsTrue(listPatAging[0].DateLastStatement>=dateTimeSinceDate);
			Assert.IsTrue(listPatAging[1].DateLastStatement>=dateTimeSinceDate);
		}

		[TestMethod]
		[Documentation.Numbering(Documentation.EnumTestNum.AgingData_GetAgingData_IncomeTransferUnearnedToProcedure)]
		[Documentation.VersionAdded("24.4")]
		[Documentation.Description(@"A procedure for $1,000 that was completed one year ago should yield $1,000 in the 'over 90' aging bucket. A $100 payment made to unearned 11 months ago that is eventually transferred to the procedure via an income transfer should cause the 'over 90' aging bucket to yield $900. The negative and positive payment splits associated with the income transfer should not impact the aging buckets.
<table>
  <tr>
    <th colspan=""5"">Patient Account</th>
  </tr>
  <tr>
    <th>Date</th>
    <th>Description</th>
    <th>Charges</th>
    <th>Credits</th>
    <th>Balance</th>
  </tr>
  <tr>
    <td>02/18/2024</td>
    <td>Procedure</td>
    <td>1,000</td>
    <td></td>
    <td>1,000</td>
  </tr>
  <tr>
    <td>03/18/2024</td>
    <td>Payment (unearned)</td>
    <td></td>
    <td>100</td>
    <td>900</td>
  </tr>
  <tr>
    <td>02/24/2025</td>
    <td>Income Transfer (from unearned)</td>
    <td></td>
    <td>-100</td>
    <td>1,000</td>
  </tr>
  <tr>
    <td>02/24/2025</td>
    <td>Income Transfer (to procedure)</td>
    <td></td>
    <td>100</td>
    <td>900</td>
  </tr>
</table>
<table>
  <tr>
    <th colspan=""4"">Family Aging</th>
  </tr>
  <tr>
    <th>0-30</th>
    <th>31-60</th>
    <th>61-90</th>
    <th>over 90</th>
  </tr>
  <tr>
    <td>0</td>
    <td>0</td>
    <td>0</td>
    <td>900</td>
  </tr>
</table>")]
		public void AgingData_GetAgingData_IncomeTransferUnearnedToProcedure() {
			//Income transfer payments that transfer unearned income directly to old production should not impact aging buckets.
			string suffix=MethodBase.GetCurrentMethod().Name;
			Patient patient=PatientT.CreatePatient(suffix);
			long provNum=ProviderT.CreateProvider(suffix);
			//Create a completed procedure that was due a year ago (over 90) bucket.
			DateTime procDate=DateTime.Today.AddYears(-1);
			Procedure procedure=ProcedureT.CreateProcedure(patient,"ITUTP",ProcStat.C,"",1000,procDate,provNum:provNum);
			//Create an unearned payment directly to unearned for less than the procedure value dated 11 months ago.
			long unearnedType=PrefC.GetLong(PrefName.PrepaymentUnearnedType);
			PaymentT.MakePayment(patient.PatNum,100,payDate:DateTime.Today.AddMonths(-11),unearnedType:unearnedType);
			//Execute an income transfer dated today so that the $100 in unearned is allocated to the procedure.
			PaymentEdit.IncomeTransferData incomeTransferData=PaymentT.BalanceAndIncomeTransfer(patient.PatNum);
			PaySplits.InsertMany(incomeTransferData.ListSplitsCur);
			//Compute aging and assert that the aging buckets yield desirable results.
			Ledgers.ComputeAging(patient.Guarantor,DateTime.Today);
			List<PatAging> listPatAgingSimple=Patients.GetAgingListSimple(null,ListTools.FromSingle(patient.Guarantor));
			Assert.AreEqual(1,listPatAgingSimple.Count);
			Assert.AreEqual(1,listPatAgingSimple.Count(x => x.BalTotal==900
				&& x.Bal_0_30==0
				&& x.Bal_31_60==0
				&& x.Bal_61_90==0
				&& x.BalOver90==900));
		}

		[TestMethod]
		[Documentation.Numbering(Documentation.EnumTestNum.AgingData_GetAgingData_NegativePaymentPartialRefund)]
		[Documentation.VersionAdded("24.4")]
		[Documentation.Description(@"A procedure for $100 that was completed one year ago should yield $100 in the 'over 90' aging bucket. A $100 payment made to the procedure 11 months ago should cause the 'over 90' aging bucket to yield $0. A negative payment of ($50) made today, in order to partially refund the payment, should cause the 'over 90' to yield $50.
<table>
  <tr>
    <th colspan=""5"">Patient Account</th>
  </tr>
  <tr>
    <th>Date</th>
    <th>Description</th>
    <th>Charges</th>
    <th>Credits</th>
    <th>Balance</th>
  </tr>
  <tr>
    <td>02/18/2024</td>
    <td>Procedure</td>
    <td>100</td>
    <td></td>
    <td>100</td>
  </tr>
  <tr>
    <td>03/18/2024</td>
    <td>Payment</td>
    <td></td>
    <td>100</td>
    <td>0</td>
  </tr>
  <tr>
    <td>02/24/2025</td>
    <td>Partial Refund</td>
    <td></td>
    <td>-50</td>
    <td>50</td>
  </tr>
</table>
<table>
  <tr>
    <th colspan=""4"">Family Aging</th>
  </tr>
  <tr>
    <th>0-30</th>
    <th>31-60</th>
    <th>61-90</th>
    <th>over 90</th>
  </tr>
  <tr>
    <td>0</td>
    <td>0</td>
    <td>0</td>
    <td>50</td>
  </tr>
</table>
Arguably, we could have placed the partial refund in the 0-30 aging bucket. 
But since the patient still owes money for the procedure from a year ago, the over 90 aging makes the most sense.
Also, none of these aging decisions affect any financial reports because those never allow backdating.")]
		//2025-02-26-Nathan was pushing for aging refunds (negative payments) in the 0-30 bucket.
		//But he couldn't think of any actual scenario where that would be needed.
		public void AgingData_GetAgingData_NegativePaymentPartialRefund() {
			//Old aging logic would age negative payments as if they were production.
			//Thus, a partial refund payment of ($50) dated today made to refund part of a payment from 11 months ago would cause aging to look like this:
			/******************************************************
			 * 0-30    =  50.00  =>  used date on negative payment
			 * 31-60   =   0.00
			 * 61-90   =   0.00
			 * over 90 =   0.00
			 ******************************************************/
			//New aging logic does not age negative payments and instead ages via the date on outstanding production.
			//Thus, a refund payment of ($50) dated today made to refund part of a payment from 11 months ago causes aging to look like this:
			/******************************************************
			 * 0-30    =   0.00
			 * 31-60   =   0.00
			 * 61-90   =   0.00
			 * over 90 =  50.00  =>  uses date on procedure
			 ******************************************************/
			string suffix=MethodBase.GetCurrentMethod().Name;
			Patient patient=PatientT.CreatePatient(suffix);
			//Create a completed procedure that was due a year ago (over 90) bucket.
			Procedure procedure=ProcedureT.CreateProcedure(patient,"NPPR1",ProcStat.C,"",100,procDate:DateTime.Today.AddYears(-1));
			//Create a payment for the completed procedure dated 11 months ago.
			PaymentT.MakePayment(patient.PatNum,100,payDate:DateTime.Today.AddMonths(-11),procNum:procedure.ProcNum);
			//Refund part the payment today and assert that the outstanding balanced is aged via the date on the procedure instead of the date of this negative payment.
			PaymentT.MakePayment(patient.PatNum,-50,payDate:DateTime.Today,procNum:procedure.ProcNum);
			Ledgers.ComputeAging(patient.Guarantor,DateTime.Today);
			List<PatAging> listPatAgingSimple=Patients.GetAgingListSimple(null,ListTools.FromSingle(patient.Guarantor));
			Assert.AreEqual(1,listPatAgingSimple.Count);
			Assert.AreEqual(1,listPatAgingSimple.Count(x => x.BalTotal==50
				&& x.Bal_0_30==0
				&& x.Bal_31_60==0
				&& x.Bal_61_90==0
				&& x.BalOver90==50));
		}
	}
}
