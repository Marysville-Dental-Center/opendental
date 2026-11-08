using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Configuration;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace UnitTests.Patients_Tests {
	[TestClass]
	public class PatientsTests:TestBase {

		[TestInitialize] 
		public void RunBeforeEachTest() {
			PatientT.ClearPatientTable();
			PhoneNumberT.ClearPhoneNumberTable();
			DiscountPlanSubT.ClearDiscountPlanSubTable();
		}

		#region GetAgingList
		[TestMethod]
		public void Patients_GetAgingList_Patients_GetAgingList_FamilyGuarantorPlanExcludableNonGuarantorNoInsurance() {
			//Arrange
			Patient patientA=PatientT.CreatePatient(lName: "Dalton",fName: "Andy", hasIns: "I",balTotal: 5);
			Patient patientB=PatientT.CreatePatient(lName: "Green",fName: "A.J.",hasIns: " ",guarantor: patientA.PatNum,balTotal: 5);
			PatPlan patPlan=PatPlanT.CreatePatPlan(ordinal: 1,patNum: patientA.PatNum,subNum: 1);
			PatAgingTransaction patAgingTransaction=new PatAgingTransaction(PatAgingTransaction.TransactionTypes.Procedure, DateTime.Today);
			List<PatAgingTransaction> listPatAgingTransactions=new List<PatAgingTransaction>();
			listPatAgingTransactions.Add(patAgingTransaction);
			SerializableDictionary<long,List<PatAgingTransaction>> dictPatAgingTransactions=new SerializableDictionary<long, List<PatAgingTransaction>>();
			dictPatAgingTransactions[patientA.PatNum]=listPatAgingTransactions;
			//This list determines what PatPlanNums to exclude
			List<long> listPatNums=new List<long>();
			listPatNums.Add(patientA.PatNum);
			List<PatAging> listPatAgingExpected=new List<PatAging>();
			PatAging patAgingExpected=new PatAging();
			patAgingExpected.PatNum=patientA.PatNum;
			patAgingExpected.PatName="Dalton, Andy";
			patAgingExpected.BalTotal=5;
			patAgingExpected.AmountDue=5;
			patAgingExpected.PriProv=1;
			patAgingExpected.BillingType=40;
			patAgingExpected.Zip="";
			listPatAgingExpected.Add(patAgingExpected);
			//Act
			List<PatAging> listPatAgingActual=Patients.GetAgingList(age:"",lastStatement: DateTime.MinValue,new List<long>(),excludeAddr: false,excludeNeg: false,
			excludeLessThan: 0,excludeInactive: false,ignoreInPerson: false,new List<long>(),isSuperStatements: false,isSinglePatient:false,
			new List<long>(),new List<long>(),dictPatAgingTransactions,listPatNumsToExclude: listPatNums);
			//Assert
			Assert.IsFalse(listPatAgingActual.IsNullOrEmpty());
			Assert.AreEqual(listPatAgingActual.Count(),listPatAgingExpected.Count());
			Assert.IsTrue(listPatAgingActual.All(x=>PatientT.GetArePatAgingsEqual(patAgingExpected,x)));
		}

		[TestMethod]
		public void Patients_GetAgingList_FamilyGuarantorPlanExcludableNonGuarantorPlanIsNot() {
			//Arrange
			Patient patientA=PatientT.CreatePatient(lName: "Dalton",fName: "Andy", hasIns: "I",balTotal: 5);
			Patient patientB=PatientT.CreatePatient(lName: "Green",fName: "A.J.",hasIns: "I",guarantor: patientA.PatNum,balTotal: 5);
			PatPlan patPlanA=PatPlanT.CreatePatPlan(ordinal: 1,patNum: patientA.PatNum,subNum: 1);
			PatPlan patPlanB=PatPlanT.CreatePatPlan(ordinal: 1,patNum: patientB.PatNum,subNum: 2);
			PatAgingTransaction patAgingTransaction=new PatAgingTransaction(PatAgingTransaction.TransactionTypes.Procedure, DateTime.Today);
			List<PatAgingTransaction> listPatAgingTransactions=new List<PatAgingTransaction>();
			listPatAgingTransactions.Add(patAgingTransaction);
			SerializableDictionary<long,List<PatAgingTransaction>> dictPatAgingTransactions=new SerializableDictionary<long, List<PatAgingTransaction>>();
			dictPatAgingTransactions[patientA.PatNum]=listPatAgingTransactions;
			//This list determines what PatPlanNums to exclude
			List<long> listPatNums=new List<long>();
			listPatNums.Add(patientA.PatNum);
			List<PatAging> listPatAgingExpected=new List<PatAging>();
			PatAging patAgingExpected=new PatAging();
			patAgingExpected.PatNum=patientA.PatNum;
			patAgingExpected.PatName="Dalton, Andy";
			patAgingExpected.BalTotal=5;
			patAgingExpected.AmountDue=5;
			patAgingExpected.PriProv=1;
			patAgingExpected.BillingType=40;
			patAgingExpected.Zip="";
			listPatAgingExpected.Add(patAgingExpected);
			//Act
			List<PatAging> listPatAgingActual=Patients.GetAgingList(age:"",lastStatement: DateTime.MinValue,new List<long>(),excludeAddr: false,excludeNeg: false,
			excludeLessThan: 0,excludeInactive: false,ignoreInPerson: false,new List<long>(),isSuperStatements: false,isSinglePatient:false,
			new List<long>(),new List<long>(),dictPatAgingTransactions,listPatNumsToExclude: listPatNums);
			//Assert
			Assert.IsFalse(listPatAgingActual.IsNullOrEmpty());
			Assert.AreEqual(listPatAgingActual.Count(),listPatAgingExpected.Count());
			Assert.IsTrue(listPatAgingActual.All(x=>PatientT.GetArePatAgingsEqual(patAgingExpected,x)));
		}

		[TestMethod]
		public void Patients_GetAgingList_FamilyGuarantorNoInsuranceNonGuarantorPlanExcludable() {
			//Arrange
			Patient patientA=PatientT.CreatePatient(lName: "Dalton",fName: "Andy", hasIns: " ",balTotal: 5);
			Patient patientB=PatientT.CreatePatient(lName: "Green",fName: "A.J.",hasIns: "I",guarantor: patientA.PatNum,balTotal: 5);
			PatPlan patPlan=PatPlanT.CreatePatPlan(ordinal: 1,patNum: patientB.PatNum,subNum: 1);
			PatAgingTransaction patAgingTransaction=new PatAgingTransaction(PatAgingTransaction.TransactionTypes.Procedure, DateTime.Today);
			List<PatAgingTransaction> listPatAgingTransactions=new List<PatAgingTransaction>();
			listPatAgingTransactions.Add(patAgingTransaction);
			SerializableDictionary<long,List<PatAgingTransaction>> dictPatAgingTransactions=new SerializableDictionary<long, List<PatAgingTransaction>>();
			dictPatAgingTransactions[patientA.PatNum]=listPatAgingTransactions;
			//This list determines what PatPlanNums to exclude
			List<long> listPatNums=new List<long>();
			listPatNums.Add(patientB.PatNum);
			List<PatAging> listPatAgingExpected=new List<PatAging>();
			PatAging patAgingExpected=new PatAging();
			patAgingExpected.PatNum=patientA.PatNum;
			patAgingExpected.PatName="Dalton, Andy";
			patAgingExpected.BalTotal=5;
			patAgingExpected.AmountDue=5;
			patAgingExpected.PriProv=1;
			patAgingExpected.BillingType=40;
			patAgingExpected.Zip="";
			listPatAgingExpected.Add(patAgingExpected);
			//Act
			List<PatAging> listPatAgingActual=Patients.GetAgingList(age:"",lastStatement: DateTime.MinValue,new List<long>(),excludeAddr: false,excludeNeg: false,
			excludeLessThan: 0,excludeInactive: false,ignoreInPerson: false,new List<long>(),isSuperStatements: false,isSinglePatient:false,
			new List<long>(),new List<long>(),dictPatAgingTransactions,listPatNumsToExclude: listPatNums);
			//Assert
			Assert.IsFalse(listPatAgingActual.IsNullOrEmpty());
			Assert.AreEqual(listPatAgingActual.Count(),listPatAgingExpected.Count());
			Assert.IsTrue(listPatAgingActual.All(x=>PatientT.GetArePatAgingsEqual(patAgingExpected,x)));
		}

		[TestMethod]
		public void Patients_GetAgingList_FamilyGuarantorInsurancePlanNotExcludableNonGuarantorPlanExcludable() {
			//Arrange
			Patient patientA=PatientT.CreatePatient(lName: "Dalton",fName: "Andy", hasIns: "I",balTotal: 5);
			Patient patientB=PatientT.CreatePatient(lName: "Green",fName: "A.J.",hasIns: "I",guarantor: patientA.PatNum,balTotal: 5);
			PatPlan patPlanA=PatPlanT.CreatePatPlan(ordinal: 1,patNum: patientA.PatNum,subNum: 1);
			PatPlan patPlanB=PatPlanT.CreatePatPlan(ordinal: 1,patNum: patientB.PatNum,subNum: 2);
			PatAgingTransaction patAgingTransaction=new PatAgingTransaction(PatAgingTransaction.TransactionTypes.Procedure, DateTime.Today);
			List<PatAgingTransaction> listPatAgingTransactions=new List<PatAgingTransaction>();
			listPatAgingTransactions.Add(patAgingTransaction);
			SerializableDictionary<long,List<PatAgingTransaction>> dictPatAgingTransactions=new SerializableDictionary<long, List<PatAgingTransaction>>();
			dictPatAgingTransactions[patientA.PatNum]=listPatAgingTransactions;
			//This list determines what PatPlanNums to exclude
			List<long> listPatNums=new List<long>();
			listPatNums.Add(patientB.PatNum);
			List<PatAging> listPatAgingExpected=new List<PatAging>();
			PatAging patAgingExpected=new PatAging();
			patAgingExpected.PatNum=patientA.PatNum;
			patAgingExpected.PatName="Dalton, Andy";
			patAgingExpected.BalTotal=5;
			patAgingExpected.AmountDue=5;
			patAgingExpected.PriProv=1;
			patAgingExpected.BillingType=40;
			patAgingExpected.Zip="";
			listPatAgingExpected.Add(patAgingExpected);
			//Act
			List<PatAging> listPatAgingActual=Patients.GetAgingList(age:"",lastStatement: DateTime.MinValue,new List<long>(),excludeAddr: false,excludeNeg: false,
			excludeLessThan: 0,excludeInactive: false,ignoreInPerson: false,new List<long>(),isSuperStatements: false,isSinglePatient:false,
			new List<long>(),new List<long>(),dictPatAgingTransactions,listPatNumsToExclude: listPatNums);
			//Assert
			Assert.IsFalse(listPatAgingActual.IsNullOrEmpty());
			Assert.AreEqual(listPatAgingActual.Count(),listPatAgingExpected.Count());
			Assert.IsTrue(listPatAgingActual.All(x=>PatientT.GetArePatAgingsEqual(patAgingExpected,x)));
		}

		/// <summary>This is tested for if a non-guarantor drops a not excludable plan and joins the guarantor's plan that is excludable.</summary>
		[TestMethod]
		public void Patients_GetAgingList_FamilyGuarantorPlanExcludableNonGuarentorChangedToGuarantorPlan() {
			//Arrange
			Patient patientA=PatientT.CreatePatient(lName: "Dalton",fName: "Andy", hasIns: "I",balTotal: 5);
			Patient patientB=PatientT.CreatePatient(lName: "Green",fName: "A.J.",hasIns: "I",guarantor: patientA.PatNum,balTotal: 5);
			InsSub insSubA=InsSubT.CreateInsSub(subscriberNum: patientA.PatNum,planNum: 1);
			InsSub insSubB=InsSubT.CreateInsSub(subscriberNum: patientB.PatNum,planNum: 2);
			PatPlan patPlanA=PatPlanT.CreatePatPlan(ordinal: 1,patNum: patientA.PatNum,subNum: insSubA.InsSubNum);
			PatPlan patPlanB=PatPlanT.CreatePatPlan(ordinal: 1,patNum: patientB.PatNum,subNum: insSubA.InsSubNum);
			PatAgingTransaction patAgingTransaction=new PatAgingTransaction(PatAgingTransaction.TransactionTypes.Procedure, DateTime.Today);
			List<PatAgingTransaction> listPatAgingTransactions=new List<PatAgingTransaction>();
			listPatAgingTransactions.Add(patAgingTransaction);
			SerializableDictionary<long,List<PatAgingTransaction>> dictPatAgingTransactions=new SerializableDictionary<long, List<PatAgingTransaction>>();
			dictPatAgingTransactions[patientA.PatNum]=listPatAgingTransactions;
			//This list determines what PatPlanNums to exclude
			List<long> listPatNums=new List<long>();
			listPatNums.Add(patientA.PatNum);
			listPatNums.Add(patientB.PatNum);
			//Act
			List<PatAging> listPatAgingActual=Patients.GetAgingList(age:"",lastStatement: DateTime.MinValue,new List<long>(),excludeAddr: false,excludeNeg: false,
			excludeLessThan: 0,excludeInactive: false,ignoreInPerson: false,new List<long>(),isSuperStatements: false,isSinglePatient:false,
			new List<long>(),new List<long>(),dictPatAgingTransactions,listPatNumsToExclude: listPatNums);
			//Assert
			Assert.IsTrue(listPatAgingActual.IsNullOrEmpty());
		}

		/// <summary>This is tested for if a guarantor drops a not excludable plan and joins a non-guarantor's plan that is excludable.</summary>
		[TestMethod]
		public void Patients_GetAgingList_GuarantorChangedToNonGuarantorExcludablePlan() {
			//Arrange
			Patient patientA=PatientT.CreatePatient(lName: "Dalton",fName: "Andy", hasIns: "I",balTotal: 5);
			Patient patientB=PatientT.CreatePatient(lName: "Green",fName: "A.J.",hasIns: "I",guarantor: patientA.PatNum,balTotal: 5);
			InsSub insSubA=InsSubT.CreateInsSub(subscriberNum: patientA.PatNum,planNum: 1);
			InsSub insSubB=InsSubT.CreateInsSub(subscriberNum: patientB.PatNum,planNum: 2);
			PatPlan patPlanA=PatPlanT.CreatePatPlan(ordinal: 1,patNum: patientA.PatNum,subNum: insSubB.InsSubNum);
			PatPlan patPlanB=PatPlanT.CreatePatPlan(ordinal: 1,patNum: patientB.PatNum,subNum: insSubB.InsSubNum);
			PatAgingTransaction patAgingTransaction=new PatAgingTransaction(PatAgingTransaction.TransactionTypes.Procedure, DateTime.Today);
			List<PatAgingTransaction> listPatAgingTransactions=new List<PatAgingTransaction>();
			listPatAgingTransactions.Add(patAgingTransaction);
			SerializableDictionary<long,List<PatAgingTransaction>> dictPatAgingTransactions=new SerializableDictionary<long, List<PatAgingTransaction>>();
			dictPatAgingTransactions[patientA.PatNum]=listPatAgingTransactions;
			//This list determines what PatPlanNums to exclude
			List<long> listPatNums=new List<long>();
			listPatNums.Add(patientA.PatNum);
			listPatNums.Add(patientB.PatNum);
			PatAging patAgingExpected=new PatAging();
			//Act
			List<PatAging> listPatAgingActual=Patients.GetAgingList(age:"",lastStatement: DateTime.MinValue,new List<long>(),excludeAddr: false,excludeNeg: false,
			excludeLessThan: 0,excludeInactive: false,ignoreInPerson: false,new List<long>(),isSuperStatements: false,isSinglePatient:false,
			new List<long>(),new List<long>(),dictPatAgingTransactions,listPatNumsToExclude: listPatNums);
			//Assert
			Assert.IsTrue(listPatAgingActual.IsNullOrEmpty());
		}

		[TestMethod]
		public void Patients_GetAgingList_FamilyGuarantorInsurancePlanExcludableNonGuarantorPlanExcludable() {
			//Arrange
			Patient patientA=PatientT.CreatePatient(lName: "Dalton",fName: "Andy", hasIns: "I",balTotal: 5);
			Patient patientB=PatientT.CreatePatient(lName: "Green",fName: "A.J.",hasIns: "I",guarantor: patientA.PatNum,balTotal: 5);
			PatPlan patPlanA=PatPlanT.CreatePatPlan(ordinal: 1,patNum: patientA.PatNum,subNum: 1);
			PatPlan patPlanB=PatPlanT.CreatePatPlan(ordinal: 1,patNum: patientB.PatNum,subNum: 2);
			PatAgingTransaction patAgingTransaction=new PatAgingTransaction(PatAgingTransaction.TransactionTypes.Procedure, DateTime.Today);
			List<PatAgingTransaction> listPatAgingTransactions=new List<PatAgingTransaction>();
			listPatAgingTransactions.Add(patAgingTransaction);
			SerializableDictionary<long,List<PatAgingTransaction>> dictPatAgingTransactions=new SerializableDictionary<long, List<PatAgingTransaction>>();
			dictPatAgingTransactions[patientA.PatNum]=listPatAgingTransactions;
			//This list determines what PatPlanNums to exclude
			List<long> listPatNums=new List<long>();
			listPatNums.Add(patientA.PatNum);
			listPatNums.Add(patientB.PatNum);
			//Act
			List<PatAging> listPatAgingActual=Patients.GetAgingList(age:"",lastStatement: DateTime.MinValue,new List<long>(),excludeAddr: false,excludeNeg: false,
			excludeLessThan: 0,excludeInactive: false,ignoreInPerson: false,new List<long>(),isSuperStatements: false,isSinglePatient:false,
			new List<long>(),new List<long>(),dictPatAgingTransactions,listPatNumsToExclude: listPatNums);
			//Assert
			Assert.IsTrue(listPatAgingActual.IsNullOrEmpty());
		}

		[TestMethod]
		public void Patients_GetAgingList_ExcludesFamilyWhereGuarantorsPlanIsExcludableAndNonGuarantorOnSamePlan() {
			//Arrange
			Patient patientA=PatientT.CreatePatient(lName: "Dalton",fName: "Andy", hasIns: "I",balTotal: 5);
			Patient patientB=PatientT.CreatePatient(lName: "Green",fName: "A.J.",hasIns: "I",guarantor: patientA.PatNum,balTotal: 5);
			PatPlan patPlanA=PatPlanT.CreatePatPlan(ordinal: 1,patNum:patientA.PatNum,subNum: 1);
			PatPlan patPlanB=PatPlanT.CreatePatPlan(ordinal: 2,patNum:patientB.PatNum,subNum: 1);
			PatAgingTransaction patAgingTransaction=new PatAgingTransaction(PatAgingTransaction.TransactionTypes.Procedure, DateTime.Today);
			List<PatAgingTransaction> listPatAgingTransactions=new List<PatAgingTransaction>();
			listPatAgingTransactions.Add(patAgingTransaction);
			SerializableDictionary<long,List<PatAgingTransaction>> dictPatAgingTransactions=new SerializableDictionary<long, List<PatAgingTransaction>>();
			dictPatAgingTransactions[patientA.PatNum]=listPatAgingTransactions;
			//This list determines what PatPlanNums to exclude
			List<long> listPatNums=new List<long>();
			listPatNums.Add(patientA.PatNum);
			listPatNums.Add(patientB.PatNum);
			//Act
			List<PatAging> listPatAgingActual=Patients.GetAgingList(age:"",lastStatement: DateTime.MinValue,new List<long>(),excludeAddr: false,excludeNeg: false,
			excludeLessThan: 0,excludeInactive: false,ignoreInPerson: false,new List<long>(),isSuperStatements: false,isSinglePatient:false,
			new List<long>(),new List<long>(),dictPatAgingTransactions,listPatNumsToExclude: listPatNums);
			//Assert
			Assert.IsTrue(listPatAgingActual.IsNullOrEmpty());
		}

		[TestMethod]
		public void Patients_GetAgingList_SinglePatientPlanExcludable() {
			//Arrange
			Patient patient=PatientT.CreatePatient(lName: "Dalton",fName: "Andy", hasIns: "I",balTotal: 5);
			PatPlan patPlan=PatPlanT.CreatePatPlan(ordinal: 1,patNum: patient.PatNum,subNum: 1);
			PatAgingTransaction patAgingTransaction=new PatAgingTransaction(PatAgingTransaction.TransactionTypes.Procedure, DateTime.Today);
			List<PatAgingTransaction> listPatAgingTransactions=new List<PatAgingTransaction>();
			listPatAgingTransactions.Add(patAgingTransaction);
			SerializableDictionary<long,List<PatAgingTransaction>> dictPatAgingTransactions=new SerializableDictionary<long, List<PatAgingTransaction>>();
			dictPatAgingTransactions[patient.PatNum]=listPatAgingTransactions;
			//This list determines what PatPlanNums to exclude
			List<long> listPatNums=new List<long>();
			listPatNums.Add(patient.PatNum);
			//Act
			List<PatAging> listPatAgingActual=Patients.GetAgingList(age:"",lastStatement: DateTime.MinValue,new List<long>(),excludeAddr: false,excludeNeg: false,
			excludeLessThan: 0,excludeInactive: false,ignoreInPerson: false,new List<long>(),isSuperStatements: false,isSinglePatient:false,
			new List<long>(),new List<long>(),dictPatAgingTransactions,listPatNumsToExclude: listPatNums);
			//Assert
			Assert.IsTrue(listPatAgingActual.IsNullOrEmpty());
		}

		[TestMethod]
		public void Patients_GetAgingList_PrimaryPlanExcludableSecondaryIsNot() {
			//Arrange
			Patient patient=PatientT.CreatePatient(lName: "Dalton",fName: "Andy", hasIns: "I",balTotal: 5);
			PatPlan patPlanA=PatPlanT.CreatePatPlan(ordinal: 1,patNum: patient.PatNum,subNum: 1);
			PatPlan patPlanB=PatPlanT.CreatePatPlan(ordinal: 2,patNum: patient.PatNum,subNum: 2);
			PatAgingTransaction patAgingTransaction=new PatAgingTransaction(PatAgingTransaction.TransactionTypes.Procedure, DateTime.Today);
			List<PatAgingTransaction> listPatAgingTransactions=new List<PatAgingTransaction>();
			listPatAgingTransactions.Add(patAgingTransaction);
			SerializableDictionary<long,List<PatAgingTransaction>> dictPatAgingTransactions=new SerializableDictionary<long, List<PatAgingTransaction>>();
			dictPatAgingTransactions[patient.PatNum]=listPatAgingTransactions;
			//This list determines what PatPlanNums to exclude
			List<long> listPatNums=new List<long>();
			listPatNums.Add(patient.PatNum);
			//Act
			List<PatAging> listPatAgingActual=Patients.GetAgingList(age:"",lastStatement: DateTime.MinValue,new List<long>(),excludeAddr: false,excludeNeg: false,
			excludeLessThan: 0,excludeInactive: false,ignoreInPerson: false,new List<long>(),isSuperStatements: false,isSinglePatient:false,
			new List<long>(),new List<long>(),dictPatAgingTransactions,listPatNumsToExclude: listPatNums);
			//Assert
			Assert.IsTrue(listPatAgingActual.IsNullOrEmpty());
		}
		#endregion GetAgingList

		#region PhoneNumber table
		///<summary>Creates many phone number fields, and syncs the PhoneNumberTable.</summary>
		[TestMethod]
		public void Patients_PhoneNumberTable_SyncAllPats() {
			//Arrange
			for(int i=0;i<10;i++) {
				//A few random orphaned PhoneNumbers.
				string phoneNumberVal=PhoneNumberT.GetRandomPhoneNumber();
				PhoneNumbers.Insert(new PhoneNumber {
					PatNum=1,
					PhoneNumberDigits=PhoneNumbers.RemoveNonDigitsAndTrimStart(phoneNumberVal),
					PhoneNumberVal=phoneNumberVal,
					PhoneType=PhoneType.HmPhone,
				});
			}
			PhoneNumbers.SyncBatchSize=10;
			List<Patient> listPats=new List<Patient>();
			for(int i=0;i<5;i++) {
				Clinics.Insert(new Clinic());
			}
			Clinics.RefreshCache();
			foreach(Clinic clinic in Clinics.GetWhere(x => true).Concat(new List<Clinic> { Clinics.GetPracticeAsClinicZero() })) {
				for(int i=0;i<(PhoneNumbers.SyncBatchSize*3)+1;i++) {
					string hmPhone=PhoneNumberT.GetRandomPhoneNumber();
					string wkPhone=PhoneNumberT.GetRandomPhoneNumber();
					string wirelessPhone=PhoneNumberT.GetRandomPhoneNumber();
					listPats.Add(PatientT.CreatePatient(clinicNum:clinic.ClinicNum,phone:hmPhone,wkPhone:wkPhone,wirelessPhone:wirelessPhone));
				}
			}
			//Act
			PhoneNumbers.SyncAllPats();
			//Assert
			List<PhoneNumber> listPhoneNumbersAll=PhoneNumberT.GetAll();
			Assert.AreEqual(3*listPats.Count,listPhoneNumbersAll.Count);//3 phonenumbers per patient, with old PhoneNumbers removed.
			foreach(Patient pat in listPats) {
				string hmPhoneNumberDigits=PhoneNumbers.RemoveNonDigitsAndTrimStart(pat.HmPhone);
				string wkPhoneNumberDigits=PhoneNumbers.RemoveNonDigitsAndTrimStart(pat.WkPhone);
				string wirelessPhoneNumberDigits=PhoneNumbers.RemoveNonDigitsAndTrimStart(pat.WirelessPhone);
				List<PhoneNumber> listPhoneNumbers=PhoneNumbers.GetPhoneNumbers(pat.PatNum);
				Assert.AreEqual(3,listPhoneNumbers.Count);
				Assert.IsTrue(listPhoneNumbers.Any(x => x.PhoneNumberDigits==hmPhoneNumberDigits && x.PhoneType==PhoneType.HmPhone));
				Assert.IsTrue(listPhoneNumbers.Any(x => x.PhoneNumberDigits==wkPhoneNumberDigits && x.PhoneType==PhoneType.WkPhone));
				Assert.IsTrue(listPhoneNumbers.Any(x => x.PhoneNumberDigits==wirelessPhoneNumberDigits && x.PhoneType==PhoneType.WirelessPhone));
			}
		}
		#endregion

		#region MergeTwoPatientPointOfNoReturn
		///<summary>Tests that HasIns column updates if we merge insurance into a patient.</summary>
		[TestMethod]
		public void Patients_MergeTwoPatients_NewInsuranceUpdatesHasIns() {
			Patient patTo=PatientT.CreatePatient(lName: "Jonas",fName: "TO");
			Patient patFrom=PatientT.CreatePatientWithInsurance(lName: "Jonas",fName: "FROM");
			Patients.MergeTwoPatients(patTo.PatNum,patFrom.PatNum);
			Assert.AreEqual("I",Patients.GetPat(patTo.PatNum).HasIns);
		}

		///<summary>Tests that when merging two patients with discount plans, it keeps the plan
		///from the original patient.</summary>
		[TestMethod]
		public void Patients_MergeTwoPatients_PatIntoKeepsOriginalDiscountPlan() {
			Patient patTo=PatientT.CreatePatientWithDiscountPlan(lName: "Jonas",fName: "TO");
			long discountPlanNumTo=DiscountPlanSubs.GetDiscountPlanNumForPat(patTo.PatNum);
			Patient patFrom=PatientT.CreatePatientWithDiscountPlan(lName: "Jonas",fName: "FROM");
			Patients.MergeTwoPatients(patTo.PatNum,patFrom.PatNum);
			Assert.AreEqual(discountPlanNumTo,DiscountPlanSubs.GetDiscountPlanNumForPat(patTo.PatNum));
		}

		///<summary>Tests that when merging a patient with a discount plan into a patient
		///with insurance, the patTo keeps insurance and does not add a discount plan.</summary>
		[TestMethod]
		public void Patients_MergeTwoPatients_DiscountPlanDoesNotUpdateIfInsuranceExists() {
			Patient patTo=PatientT.CreatePatientWithInsurance(lName: "Jonas",fName: "TO");
			Patient patFrom=PatientT.CreatePatientWithDiscountPlan(lName: "Jonas",fName: "FROM");
			Patients.MergeTwoPatients(patTo.PatNum,patFrom.PatNum);
			Assert.AreEqual(false,DiscountPlanSubs.HasDiscountPlan(patTo.PatNum));
		}

		///<summary>Tests that when merging a patient with a discount plan into a patient
		///without a discount plan or insurance, the patTo gets the patFrom discount plan.</summary>
		[TestMethod]
		public void Patients_MergeTwoPatients_DiscountPlanUpdatesIfNoExistingPlanOrInsurance() {
			Patient patTo=PatientT.CreatePatient(lName: "Jonas",fName: "TO");
			Patient patFrom=PatientT.CreatePatientWithDiscountPlan(lName: "Jonas",fName: "FROM");
			long discountPlanNumFrom=DiscountPlanSubs.GetDiscountPlanNumForPat(patFrom.PatNum);
			Patients.MergeTwoPatients(patTo.PatNum,patFrom.PatNum);
			Assert.AreEqual(discountPlanNumFrom,DiscountPlanSubs.GetDiscountPlanNumForPat(patTo.PatNum));
		}

		///<summary>Merging moves the merged-away patient's eForm and every one of its field rows to the patient merged into, as it moves sheets, and
		///leaves the kept patient's own eForm alone. Open Dental 24.3 left eform.PatNum and eformfield.PatNum out of the merge (fork, package S0-5 of
		///the .NET 10 plan).</summary>
		[TestMethod]
		public void Patients_MergeTwoPatients_EFormAndItsFieldsMove() {
			Patient patTo=PatientT.CreatePatient(lName: "Jonas",fName: "TO");
			Patient patFrom=PatientT.CreatePatient(lName: "Jonas",fName: "FROM");
			EForm eFormFrom=EFormT.CreateEForm(0,new List<EFormField>(),patFrom.PatNum,EnumEFormStatus.Filled,description: "Filled by FROM");
			EForm eFormTo=EFormT.CreateEForm(0,new List<EFormField>(),patTo.PatNum,EnumEFormStatus.Filled,description: "Filled by TO");
			for(int i=0;i<3;i++) {
				EFormFields.Insert(new EFormField {
					EFormNum=eFormFrom.EFormNum,PatNum=patFrom.PatNum,FieldType=EnumEFormFieldType.TextField,ValueLabel="Question "+i,ValueString="Answer "+i,
					ItemOrder=i });
			}
			EFormFields.Insert(new EFormField {
				EFormNum=eFormTo.EFormNum,PatNum=patTo.PatNum,FieldType=EnumEFormFieldType.TextField,ValueLabel="Question",ValueString="Kept",ItemOrder=0 });
			Patients.MergeTwoPatients(patTo.PatNum,patFrom.PatNum);
			Assert.AreEqual(0,EForms.GetForPatient(patFrom.PatNum).Count);
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM eformfield WHERE PatNum="+POut.Long(patFrom.PatNum)));
			List<EForm> listEFormsTo=EForms.GetForPatient(patTo.PatNum);
			CollectionAssert.AreEquivalent(new List<long> { eFormFrom.EFormNum,eFormTo.EFormNum },listEFormsTo.Select(x => x.EFormNum).ToList());
			EForm eFormMoved=listEFormsTo.First(x => x.EFormNum==eFormFrom.EFormNum);
			Assert.AreEqual("Filled by FROM",eFormMoved.Description);
			CollectionAssert.AreEqual(new List<string> { "Answer 0","Answer 1","Answer 2" },eFormMoved.ListEFormFields.Select(x => x.ValueString).ToList());
			Assert.IsTrue(eFormMoved.ListEFormFields.All(x => x.PatNum==patTo.PatNum));
			EForm eFormKept=listEFormsTo.First(x => x.EFormNum==eFormTo.EFormNum);
			Assert.AreEqual(1,eFormKept.ListEFormFields.Count);
			Assert.AreEqual("Kept",eFormKept.ListEFormFields[0].ValueString);
		}
		#endregion

		#region GetPatsToChangeStatus
		///<summary>Tests that active patients without TP procedures since date will change to inactive/archive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_ActivePatientWOTPProcsSince() {
			PatientT.ClearPatientTable();
			ProcedureT.ClearProcedureTable();
			long provNum=ProviderT.GetFirstProvNum();
			Patient patActive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add a TP procedure a day before dateSince. 
			ProcedureT.CreateProcedure(patActive,"D1050",ProcStat.TP,"",50,procDate:dateSince.AddDays(-1));
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Patient,dateSince,true,false,false,null);
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patActive.PatNum,listPats.FirstOrDefault().PatNum);
		}

		///<summary>Tests that active patients with TP procedures since date will not change to inactive/archive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_ActivePatientWTPProcsSince() {
			PatientT.ClearPatientTable();
			ProcedureT.ClearProcedureTable();
			long provNum=ProviderT.GetFirstProvNum();
			Patient patActive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum);
			Patient patActiveBlank=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name+"_blank",provNum);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add a TP procedure a day after dateSince. 
			ProcedureT.CreateProcedure(patActive,"D1050",ProcStat.TP,"",50,procDate:dateSince.AddDays(1));
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Patient,dateSince,true,false,false,null);
			//Only blank patient should show
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patActiveBlank.PatNum,listPats.First().PatNum);
		}

		///<summary>Tests that active patients without C procedures since date will change to inactive/archive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_ActivePatientWOCProcsSince() {
			PatientT.ClearPatientTable();
			ProcedureT.ClearProcedureTable();
			long provNum=ProviderT.GetFirstProvNum();
			Patient patActive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add a C procedure a day before dateSince. 
			ProcedureT.CreateProcedure(patActive,"D1050",ProcStat.C,"",50,procDate:dateSince.AddDays(-1));
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Patient,dateSince,false,true,false,null);
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patActive.PatNum,listPats.FirstOrDefault().PatNum);
		}

		///<summary>Tests that active patients with C procedures since date will not change to inactive/archive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_ActivePatientWCProcsSince() {
			PatientT.ClearPatientTable();
			ProcedureT.ClearProcedureTable();
			long provNum=ProviderT.GetFirstProvNum();
			Patient patActive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum);
			Patient patActiveBlank=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name+"_blank",provNum);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add a C procedure a day after dateSince. 
			ProcedureT.CreateProcedure(patActive,"D1050",ProcStat.C,"",50,procDate:dateSince.AddDays(1));
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Patient,dateSince,false,true,false,null);
			//Only the blank patient should be returned
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patActiveBlank.PatNum,listPats.First().PatNum);
		}

		///<summary>Tests that active patients without an appointment since date will change to inactive/archive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_ActivePatientWOApptSince() {
			PatientT.ClearPatientTable();
			AppointmentT.ClearAppointmentTable();
			long provNum=ProviderT.GetFirstProvNum();
			Operatory op=OperatoryT.CreateOperatory();
			Patient patActive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			Appointment apt=AppointmentT.CreateAppointment(patActive.PatNum,dateSince.AddDays(-1),op.OperatoryNum,provNum);
			//Add an appointment a day before dateSince. 
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Patient,dateSince,false,false,true,null);
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patActive.PatNum,listPats.FirstOrDefault().PatNum);
		}

		///<summary>Tests that Active patients with an appointment since date will not change to inactive/archive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_ActivePatientWApptSince() {
			PatientT.ClearPatientTable();
			AppointmentT.ClearAppointmentTable();
			long provNum=ProviderT.GetFirstProvNum();
			Operatory op=OperatoryT.CreateOperatory();
			Patient patActive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum);
			Patient patActiveBlank=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name+"_blank",provNum);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add an appointment a day after dateSince. 
			Appointment apt=AppointmentT.CreateAppointment(patActive.PatNum,dateSince.AddDays(1),op.OperatoryNum,provNum);
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Patient,dateSince,false,false,true,null);
			//only the blank pat should be returned
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patActiveBlank.PatNum,listPats.First().PatNum);
		}

		///<summary>Tests that Inactive patients with TP procedures since date will not change to Active/Archive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_InactivePatientWTPProcsSince() {
			PatientT.ClearPatientTable();
			ProcedureT.ClearProcedureTable();
			long provNum=ProviderT.GetFirstProvNum();
			Patient patInactive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum,patStatus:PatientStatus.Inactive);
			Patient patInactiveBlank=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name+"_blank",provNum,patStatus:PatientStatus.Inactive);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add a TP procedure a day after dateSince. 
			ProcedureT.CreateProcedure(patInactive,"D1050",ProcStat.TP,"",50,procDate:dateSince.AddDays(1));
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Inactive,dateSince,true,false,false,null);
			//Only the blank patient should show
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patInactiveBlank.PatNum,listPats.First().PatNum);
		}

		///<summary>Tests that Inactive patients without TP procedures since date will change to Active/Archive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_InactivePatientWOTPProcsSince() {
			PatientT.ClearPatientTable();
			ProcedureT.ClearProcedureTable();
			long provNum=ProviderT.GetFirstProvNum();
			Patient patInactive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum,patStatus:PatientStatus.Inactive);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add a TP procedure a day before dateSince. 
			ProcedureT.CreateProcedure(patInactive,"D1050",ProcStat.TP,"",50,procDate: dateSince.AddDays(-1));
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Inactive,dateSince,true,false,false,null);
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patInactive.PatNum,listPats.FirstOrDefault().PatNum);
		}

		///<summary>Tests that Inactive patients with C procedures since date will not change to Active/Archive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_InactivePatientWCProcsSince() {
			PatientT.ClearPatientTable();
			ProcedureT.ClearProcedureTable();
			long provNum=ProviderT.GetFirstProvNum();
			Patient patInactive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum,patStatus:PatientStatus.Inactive);
			Patient patInactiveBlank=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name+"_blank",provNum,patStatus:PatientStatus.Inactive);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add a C procedure a day after dateSince. 
			ProcedureT.CreateProcedure(patInactive,"D1050",ProcStat.C,"",50,procDate: dateSince.AddDays(1));
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Inactive,dateSince,false,true,false,null);
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patInactiveBlank.PatNum,listPats.First().PatNum);
		}

		///<summary>Tests that Inactive patients without C procedures since date will change to Active/Archive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_InactivePatientWOCProcsSince() {
			PatientT.ClearPatientTable();
			ProcedureT.ClearProcedureTable();
			long provNum=ProviderT.GetFirstProvNum();
			Patient patInactive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum,patStatus:PatientStatus.Inactive);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add a C procedure a day before dateSince. 
			ProcedureT.CreateProcedure(patInactive,"D1050",ProcStat.C,"",50,procDate: dateSince.AddDays(-1));
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Inactive,dateSince,false,true,false,null);
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patInactive.PatNum,listPats.FirstOrDefault().PatNum);
		}

		///<summary>Tests that Inactive patients with an appointment since date will not change to Active/Archive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_InactivePatientWApptSince() {
			PatientT.ClearPatientTable();
			AppointmentT.ClearAppointmentTable();
			long provNum=ProviderT.GetFirstProvNum();
			Operatory op=OperatoryT.CreateOperatory();
			Patient patInactive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum,patStatus:PatientStatus.Inactive);
			Patient patInactiveBlank=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name+"_blank",provNum,patStatus:PatientStatus.Inactive);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add an appointment a day after dateSince. 
			Appointment apt=AppointmentT.CreateAppointment(patInactive.PatNum,dateSince.AddDays(1),op.OperatoryNum,provNum);
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Inactive,dateSince,false,false,true,null);
			//Only the blank inactive patient should show
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patInactiveBlank.PatNum,listPats.First().PatNum);
		}

		///<summary>Tests that Inactive patients without an appointment since date will change to Active/Archive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_InactivePatientWOApptSince() {
			PatientT.ClearPatientTable();
			AppointmentT.ClearAppointmentTable();
			long provNum=ProviderT.GetFirstProvNum();
			Operatory op=OperatoryT.CreateOperatory();
			Patient patInactive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum,patStatus:PatientStatus.Inactive);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			Appointment apt=AppointmentT.CreateAppointment(patInactive.PatNum,dateSince.AddDays(-1),op.OperatoryNum,provNum);
			//Add an appointment a day before dateSince. 
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Inactive,dateSince,false,false,true,null);
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patInactive.PatNum,listPats.FirstOrDefault().PatNum);
		}

		///<summary>Tests that Archive patients with TP procedures since date will not change to Active/Inactive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_ArchivePatientWTPProcsSince() {
			PatientT.ClearPatientTable();
			ProcedureT.ClearProcedureTable();
			long provNum=ProviderT.GetFirstProvNum();
			Patient patArchive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum,patStatus:PatientStatus.Archived);
			Patient patArchiveBlank=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name+"_blank",provNum,patStatus:PatientStatus.Archived);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add a TP procedure a day after dateSince. 
			ProcedureT.CreateProcedure(patArchive,"D1050",ProcStat.TP,"",50,procDate: dateSince.AddDays(1));
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Archived,dateSince,true,false,false,null);
			//Only the blank patient should show
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patArchiveBlank.PatNum,listPats.First().PatNum);
		}

		///<summary>Tests that Archive patients without TP procedures since date will change to Active/Inactive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_ArchivePatientWOTPProcsSince() {
			PatientT.ClearPatientTable();
			ProcedureT.ClearProcedureTable();
			long provNum=ProviderT.GetFirstProvNum();
			Patient patArchive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum,patStatus:PatientStatus.Archived);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add a TP procedure a day before dateSince. 
			ProcedureT.CreateProcedure(patArchive,"D1050",ProcStat.TP,"",50,procDate: dateSince.AddDays(-1));
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Archived,dateSince,true,false,false,null);
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patArchive.PatNum,listPats.FirstOrDefault().PatNum);
		}

		///<summary>Tests that Archive patients with C procedures since date will not change to Active/Inactive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_ArchivePatientWCProcsSince() {
			PatientT.ClearPatientTable();
			ProcedureT.ClearProcedureTable();
			long provNum=ProviderT.GetFirstProvNum();
			Patient patArchive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum,patStatus:PatientStatus.Archived);
			Patient patArchiveBlank=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name+"_blank",provNum,patStatus:PatientStatus.Archived);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add a C procedure a day after dateSince. 
			ProcedureT.CreateProcedure(patArchive,"D1050",ProcStat.C,"",50,procDate: dateSince.AddDays(1));
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Archived,dateSince,false,true,false,null);
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patArchiveBlank.PatNum,listPats.First().PatNum);
		}

		///<summary>Tests that Archive patients without C procedures since date will change to Active/Inactive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_ArchivePatientWOCProcsSince() {
			PatientT.ClearPatientTable();
			ProcedureT.ClearProcedureTable();
			long provNum=ProviderT.GetFirstProvNum();
			Patient patArchive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum,patStatus:PatientStatus.Archived);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add a C procedure a day before dateSince. 
			ProcedureT.CreateProcedure(patArchive,"D1050",ProcStat.C,"",50,procDate: dateSince.AddDays(-1));
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Archived,dateSince,false,true,false,null);
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patArchive.PatNum,listPats.FirstOrDefault().PatNum);
		}

		///<summary>Tests that Archive patients with an appointment since date will not change to Active/Inactive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_ArchivePatientWApptSince() {
			PatientT.ClearPatientTable();
			AppointmentT.ClearAppointmentTable();
			long provNum=ProviderT.GetFirstProvNum();
			Operatory op=OperatoryT.CreateOperatory();
			Patient patArchive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum,patStatus:PatientStatus.Archived);
			Patient patArchiveBlank=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name+"_blank",provNum,patStatus:PatientStatus.Archived);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			//Add an appointment a day after dateSince. 
			Appointment apt=AppointmentT.CreateAppointment(patArchive.PatNum,dateSince.AddDays(1),op.OperatoryNum,provNum);
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Archived,dateSince,false,false,true,null);
			//Only the blank inactive patient should show
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patArchiveBlank.PatNum,listPats.First().PatNum);
		}

		///<summary>Tests that Archive patients without an appointment since date will change to Active/Inactive patient.</summary>
		[TestMethod]
		public void Patient_GetPatsToChangeStatus_ArchivePatientWOApptSince() {
			PatientT.ClearPatientTable();
			AppointmentT.ClearAppointmentTable();
			long provNum=ProviderT.GetFirstProvNum();
			Operatory op=OperatoryT.CreateOperatory();
			Patient patArchive=PatientT.CreatePatient(MethodBase.GetCurrentMethod().Name,provNum,patStatus:PatientStatus.Archived);
			DateTime dateSince=DateTime.Now.AddDays(-100);
			Appointment apt=AppointmentT.CreateAppointment(patArchive.PatNum,dateSince.AddDays(-1),op.OperatoryNum,provNum);
			//Add an appointment a day before dateSince. 
			List<Patient> listPats=Patients.GetPatsToChangeStatus(PatientStatus.Archived,dateSince,false,false,true,null);
			Assert.AreEqual(1,listPats.Count);
			Assert.AreEqual(patArchive.PatNum,listPats.FirstOrDefault().PatNum);
		}

		///<summary>Tests that foreign keys were added to the method after adding a new patnum field.</summary>
		[TestMethod]
		public void Patient_MergeTwoPatientPointOfNoReturn_ForeignKeyCheck() {
			//This array contains commented out tables from Patients.StringArrayPatNumForeignKeys array.
			string[] stringArrayPatNumForeignKeysToExclude=new string[] {
				"anestheticrecord.PatNum",
				"anesthvsdata.PatNum",
				"custreference.PatNum", 
				"discountplansub.PatNum",
				"document.PatNum", 
				"ehrpatient.PatNum",
				"erouting.PatNum",
				"famaging.PatNum",
				"formpat.FormPatNum",
				"languagepat.LanguagePatNum",//PK for table.
				"medicationpat.MedicationPatNum",
				"oidexternal.IDInternal",
				"orthochart.PatNum",
				"orthochartlog.PatNum",
				"patfield.PatNum",
				"patient.PatNum",
				"patient.Guarantor",
				"patient.PatNumCloneFrom",
				"patientlink.PatNumFrom",
				"patientlink.PatNumTo",
				"patientnote.PatNum",
				"patientrace.PatNum",
				"procmultivisit.PatNum",
				"question.FormPatNum",
				"recall.PatNum",
				"referral.PatNum",
				"reseller.PatNum",
				"screenpat.ScreenPatNum",
				"screen.ScreenPatNum",
				"securitylog.FKey",
				"securitylog.PatNum",
				"task.KeyNum",
				"taskhist.KeyNum",
				"tsitranslog.PatNum",
				"vaccineobs.VaccinePatNum",
				"vaccinepat.VaccinePatNum",
			};
			string dbName=LargeTableHelper.GetCurrentDatabase();
			string command="SELECT CONCAT(TABLE_NAME,'.',COLUMN_NAME) " 
				+"FROM INFORMATION_SCHEMA.columns "
				+$"WHERE table_schema='{dbName}' AND COLUMN_NAME LIKE '%patnum%' "
				+"ORDER BY TABLE_NAME";
			List<string> listTablePatNumFromDb=DataCore.GetList<string>(command,(x) => x.GetString(0));
			List<string> listTablePatNumMissing=new List<string>();
			for(int i=0;i<listTablePatNumFromDb.Count;i++) {
				string tablePatNumCur=listTablePatNumFromDb[i];
				if(Patients.StringArrayPatNumForeignKeys.Contains(tablePatNumCur)
					|| stringArrayPatNumForeignKeysToExclude.Contains(tablePatNumCur)) 
				{
					continue;
				}
				listTablePatNumMissing.Add(tablePatNumCur);
			}
			string message=string.Join($", ",listTablePatNumMissing);
			Assert.AreEqual(0,listTablePatNumMissing.Count,message);
		}
		#endregion

	}
}
