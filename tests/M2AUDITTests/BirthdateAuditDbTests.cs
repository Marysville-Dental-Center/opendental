// Database tests for the birthdate audit (feature v26_1-tools-audit-birthdate): every save path that writes patient.Birthdate leaves exactly one
// PatientEdit entry with the old and new dates. Runs on a scratch MariaDB database built from the seed dump, the frozen 24.3.63 convert chain and
// the fork migration runner, with synthetic patients only. Skipped (inconclusive) when the local MariaDB does not answer, or when
// M2AUDIT_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2AUDITTests {
	[TestClass]
	public class BirthdateAuditDbTests {
		private static readonly string DB_NAME=ScratchDb.Name("audit");
		private static string _skipReason;
		private static Userod _userod;
		private CultureInfo _cultureInfoOld;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			//The server answers, so from here on a failure is a real failure, not a reason to skip.
			ScratchDb.DropStale();
			ScratchDb.CreateFresh(DB_NAME);
			Cache.Refresh(InvalidType.Security,InvalidType.Providers,InvalidType.Defs);
			_userod=Userods.GetFirstOrDefault(x => true)??new Userod { UserNum=1,UserName="Admin" };
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			ScratchDb.DropQuietly(DB_NAME);
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ScratchDb.Connect(DB_NAME);
			Security.CurUser=_userod;
			_cultureInfoOld=Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			DataCore.NonQ("DELETE FROM securitylog");
		}

		[TestCleanup]
		public void TestCleanup() {
			if(_cultureInfoOld!=null) {
				Thread.CurrentThread.CurrentCulture=_cultureInfoOld;
			}
			Security.CurUser=_userod;
		}

		#region Helpers
		///<summary>Inserts a patient the way the Add Family window does: Insert, then set the guarantor with Update.</summary>
		private static Patient InsertPatient(string fName,DateTime dateBirth) {
			Patient patient=new Patient();
			patient.FName=fName;
			patient.LName="Synthetic";
			patient.PatStatus=PatientStatus.Patient;
			patient.Birthdate=dateBirth;
			patient.Address="";
			patient.City="";
			patient.State="";
			patient.Zip="";
			patient.WirelessPhone="";
			Patients.Insert(patient,false);
			Patient patientOld=patient.Copy();
			patient.Guarantor=patient.PatNum;
			Patients.Update(patient,patientOld);
			return patient;
		}

		///<summary>LogText and UserNum of the patient's birthdate entries, oldest first.</summary>
		private static List<DataRow> GetBirthdateLogs(long patNum) {
			DataTable table=DataCore.GetTable("SELECT LogText,UserNum FROM securitylog WHERE PermType="+POut.Int((int)EnumPermType.PatientEdit)
				+" AND PatNum="+POut.Long(patNum)+" AND LogText LIKE 'Patient birthdate%' ORDER BY SecurityLogNum");
			return table.Select().ToList();
		}

		private static List<string> GetBirthdateLogTexts(long patNum) {
			return GetBirthdateLogs(patNum).Select(x => x["LogText"].ToString()).ToList();
		}

		private static void ClearLogs() {
			DataCore.NonQ("DELETE FROM securitylog");
		}

		///<summary>Saves the patient the way Edit Patient Information does (FormPatientEdit.butOK_Click), with every "same for family" box off.</summary>
		private static void SaveLikeEditPatientInformation(Patient patient,Patient patientOld,bool isNew) {
			PatientNote patientNote=PatientNotes.Refresh(patient.PatNum,patient.Guarantor);
			EhrPatient ehrPatient=EhrPatients.Refresh(patient.PatNum);
			Family family=Patients.GetFamily(patient.PatNum);
			Patients.SavePatientEdit(Security.CurUser,patient,patientOld,patientNote,ehrPatient,family,isNew,restrictSched:false,arriveEarlySame:false,
				addressSame:false,addressSameSuperFamily:false,billProvSame:false,notesSame:false,emailPhoneSame:false,defLink:null,specialtyDefNum:0,
				listPatientRaces:new List<PatientRace>(),commOptOut:CommOptOuts.Refresh(patient.PatNum));
		}
		#endregion Helpers

		#region Patients.Update and Patients.Insert
		[TestMethod]
		public void Update_ChangedBirthdate_LogsOldAndNewDates() {
			Patient patient=InsertPatient("Changed",new DateTime(1985,3,4));
			ClearLogs();
			Patient patientOld=patient.Copy();
			patient.Birthdate=new DateTime(1986,3,4);
			Patients.Update(patient,patientOld);
			List<DataRow> listRows=GetBirthdateLogs(patient.PatNum);
			Assert.AreEqual(1,listRows.Count);
			Assert.AreEqual("Patient birthdate changed from '3/4/1985' to '3/4/1986'",listRows[0]["LogText"].ToString());
			Assert.AreEqual(_userod.UserNum.ToString(),listRows[0]["UserNum"].ToString());
		}

		[TestMethod]
		public void Update_OtherFieldsOnly_NoBirthdateEntry() {
			Patient patient=InsertPatient("Other",new DateTime(1985,3,4));
			ClearLogs();
			Patient patientOld=patient.Copy();
			patient.City="Synthville";
			patient.Preferred="Sam";
			Patients.Update(patient,patientOld);
			Assert.AreEqual(0,GetBirthdateLogs(patient.PatNum).Count);
		}

		[TestMethod]
		public void Update_BlankToDateAndBack_LogsBoth() {
			Patient patient=InsertPatient("Blank",DateTime.MinValue);
			Assert.AreEqual(0,GetBirthdateLogs(patient.PatNum).Count,"No birthdate yet: nothing to audit.");
			Patient patientOld=patient.Copy();
			patient.Birthdate=new DateTime(2010,7,15);
			Patients.Update(patient,patientOld);
			patientOld=patient.Copy();
			patient.Birthdate=DateTime.MinValue;
			Patients.Update(patient,patientOld);
			CollectionAssert.AreEqual(new List<string> {
					"Patient birthdate changed from '' to '7/15/2010'",
					"Patient birthdate changed from '7/15/2010' to ''"
				},GetBirthdateLogTexts(patient.PatNum));
			Assert.AreEqual(DateTime.MinValue.Date,Patients.GetPat(patient.PatNum).Birthdate.Date);
		}

		[TestMethod]
		public void Insert_WithBirthdate_LogsTheAdditionOnce() {
			Patient patient=InsertPatient("Inserted",new DateTime(1970,1,2));
			CollectionAssert.AreEqual(new List<string> { "Patient birthdate changed from '' to '1/2/1970'" },GetBirthdateLogTexts(patient.PatNum));
		}

		[TestMethod]
		public void Insert_WithoutBirthdate_NoEntry() {
			Patient patient=InsertPatient("NoDate",DateTime.MinValue);
			Assert.AreEqual(0,GetBirthdateLogs(patient.PatNum).Count);
		}

		[TestMethod]
		public void NoSignedInUser_LogsForUser0() {
			Patient patient=InsertPatient("Service",new DateTime(1985,3,4));
			ClearLogs();
			Security.CurUser=null;//as in a background service that imports patients
			Patient patientOld=patient.Copy();
			patient.Birthdate=new DateTime(1985,3,5);
			Patients.Update(patient,patientOld);
			Security.CurUser=_userod;
			List<DataRow> listRows=GetBirthdateLogs(patient.PatNum);
			Assert.AreEqual(1,listRows.Count);
			Assert.AreEqual("0",listRows[0]["UserNum"].ToString());
			Assert.AreEqual(new DateTime(1985,3,5),Patients.GetPat(patient.PatNum).Birthdate.Date,"The save itself never fails because of the audit.");
		}
		#endregion Patients.Update and Patients.Insert

		#region Save paths
		[TestMethod]
		public void EditPatientInformation_ChangedBirthdate_OneEntry() {
			Patient patient=InsertPatient("Edit",new DateTime(1985,3,4));
			ClearLogs();
			Patient patientOld=patient.Copy();
			patient.Birthdate=new DateTime(1985,4,3);
			patient.City="Synthville";//logged in the address entry, which stays separate
			SaveLikeEditPatientInformation(patient,patientOld,isNew:false);
			CollectionAssert.AreEqual(new List<string> { "Patient birthdate changed from '3/4/1985' to '4/3/1985'" },GetBirthdateLogTexts(patient.PatNum));
			string countAddress=DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PermType="+POut.Int((int)EnumPermType.PatientEdit)
				+" AND PatNum="+POut.Long(patient.PatNum)+" AND LogText LIKE '%city changed%'");
			Assert.AreEqual("1",countAddress);
			string countBirthdateMentions=DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PatNum="+POut.Long(patient.PatNum)
				+" AND LogText LIKE '%birthdate%'");
			Assert.AreEqual("1",countBirthdateMentions,"The birthdate is logged once per save.");
		}

		[TestMethod]
		public void EditPatientInformation_BirthdateUntouched_NoEntry() {
			//With the birthdate masked, Edit Patient Information leaves Patient.Birthdate as loaded.
			Patient patient=InsertPatient("Masked",new DateTime(1985,3,4));
			ClearLogs();
			Patient patientOld=patient.Copy();
			patient.Preferred="Max";
			SaveLikeEditPatientInformation(patient,patientOld,isNew:false);
			Assert.AreEqual(0,GetBirthdateLogs(patient.PatNum).Count);
		}

		[TestMethod]
		public void AddFamilyMember_BirthdateEnteredInNewPatient_LoggedAsAdded() {
			//The Family module's Add button inserts a blank patient, then Edit Patient Information saves it with IsNew.
			Patient patientGuarantor=InsertPatient("Guar",new DateTime(1980,1,1));
			Patient patient=new Patient();
			patient.LName=patientGuarantor.LName;
			patient.FName="";
			patient.PatStatus=PatientStatus.Patient;
			patient.Guarantor=patientGuarantor.PatNum;
			patient.Address="";
			patient.City="";
			patient.State="";
			patient.Zip="";
			Patients.Insert(patient,false);
			Assert.AreEqual(0,GetBirthdateLogs(patient.PatNum).Count);
			Patient patientOld=patient.Copy();
			patient.FName="Kid";
			patient.Birthdate=new DateTime(2015,6,30);
			SaveLikeEditPatientInformation(patient,patientOld,isNew:true);
			CollectionAssert.AreEqual(new List<string> { "Patient birthdate changed from '' to '6/30/2015'" },GetBirthdateLogTexts(patient.PatNum));
		}

		[TestMethod]
		public void NewPatient_CreateNewPatient_LoggedAsAddedOnce() {
			Patient patient=Patients.CreateNewPatient("Synthetic","Created",new DateTime(1999,9,9),0,0,"Created by the M2-AUDIT tests.");
			CollectionAssert.AreEqual(new List<string> { "Patient birthdate changed from '' to '9/9/1999'" },GetBirthdateLogTexts(patient.PatNum));
		}

		[TestMethod]
		public void SheetImport_Birthdate_Logged() {
			//What FormSheetImport does for the Birthdate row: the imported value goes on the patient, then Patients.Update(_patient,_patientOld),
			//then B-AUDIT's preferred-name and clinic entry. BirthdateSavePathTests checks that the window still saves that way.
			Patient patient=InsertPatient("Sheet",new DateTime(1985,3,4));
			ClearLogs();
			Patient patientOld=patient.Copy();
			object impValObj=new DateTime(1984,3,4);
			patient.Birthdate=(DateTime)impValObj;
			patient.Preferred="Sam";
			Patients.Update(patient,patientOld);
			Patients.InsertPreferredAndClinicChangeSecurityLogEntry(patientOld,patient,"sheet import");
			CollectionAssert.AreEqual(new List<string> { "Patient birthdate changed from '3/4/1985' to '3/4/1984'" },GetBirthdateLogTexts(patient.PatNum));
			string countBirthdateMentions=DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PatNum="+POut.Long(patient.PatNum)
				+" AND LogText LIKE '%birthdate%'");
			Assert.AreEqual("1",countBirthdateMentions,"B-AUDIT's import entry does not repeat the birthdate.");
			string countPreferred=DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PermType="+POut.Int((int)EnumPermType.PatientEdit)
				+" AND PatNum="+POut.Long(patient.PatNum)+" AND LogText LIKE 'Patient preferred name changed from '''' to ''Sam''%From sheet import.'");
			Assert.AreEqual("1",countPreferred,"B-AUDIT's import entry is kept beside the birthdate entry.");
		}

		[TestMethod]
		public void EFormAutoImport_Birthdate_Logged() {
			Patient patient=InsertPatient("EForm",new DateTime(1985,3,4));
			ClearLogs();
			EFormField eFormField=new EFormField();
			eFormField.FieldType=EnumEFormFieldType.DateField;
			eFormField.DbLink="Birthdate";
			eFormField.ValueString="1985-03-14";
			eFormField.ValueLabel="Birthdate";
			eFormField.AutoImport=true;
			eFormField.PatNum=patient.PatNum;
			EForm eForm=new EForm();
			eForm.IsNew=true;
			eForm.PatNum=patient.PatNum;
			eForm.Description="Synthetic registration";
			eForm.DateTimeShown=DateTime.Now;
			eForm.Status=EnumEFormStatus.Filled;
			eForm.ListEFormFields=new List<EFormField> { eFormField };
			EForms.SaveNewEForms(new List<EForm> { eForm });
			List<string> listChanges=EFormImport.ImportAutoFields(eForm);
			Assert.AreEqual(1,listChanges.Count);
			Assert.AreEqual(new DateTime(1985,3,14),Patients.GetPat(patient.PatNum).Birthdate.Date);
			CollectionAssert.AreEqual(new List<string> { "Patient birthdate changed from '3/4/1985' to '3/14/1985'" },GetBirthdateLogTexts(patient.PatNum));
		}
		#endregion Save paths
	}
}
