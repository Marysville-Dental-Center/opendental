// Database tests for desktop eForms (package A-EF1) on a scratch MariaDB database upgraded to the Open Dental 25.3.48 schema by the fork
// migration runner. Synthetic patients and forms only. Skipped (inconclusive) when the local MariaDB does not answer, or when EFORM_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace EFormTests {
	[TestClass]
	public class EFormDbTests {
		private static readonly string DB_NAME=TestDb.ScratchName("ef1");
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.CreateFresh25_3(DB_NAME);
			Security.CurUser=Userods.GetFirstOrDefault(x => true)??new Userod() { UserNum=1,UserName="Admin" };
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("EFORM_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//Harmless; the name is unique to this run.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DB_NAME);
		}

		private static Patient InsertPatient(string fName,string lName,long guarantor=0) {
			Patient patient=new Patient();
			patient.FName=fName;
			patient.LName=lName;
			patient.PatStatus=PatientStatus.Patient;
			patient.Address="";
			patient.City="";
			patient.State="";
			patient.Zip="";
			patient.WirelessPhone="";
			Patients.Insert(patient,false);
			Patient patientOld=patient.Copy();
			patient.Guarantor=guarantor==0 ? patient.PatNum : guarantor;
			Patients.Update(patient,patientOld);
			return patient;
		}

		private static EForm SaveForm(long patNum,EnumEFormStatus status,params EFormField[] arrayFields) {
			EForm eForm=new EForm();
			eForm.IsNew=true;
			eForm.PatNum=patNum;
			eForm.Description="Synthetic registration";
			eForm.DateTimeShown=DateTime.Now;
			eForm.Status=status;
			eForm.ListEFormFields=arrayFields.ToList();
			for(int i=0;i<arrayFields.Length;i++) {
				arrayFields[i].PatNum=patNum;
				arrayFields[i].ItemOrder=i;
			}
			EForms.SaveNewEForms(new List<EForm>() { eForm });
			return eForm;
		}

		[TestMethod]
		public void StoredRemovedStatuses_LoadAsFilled() {
			Patient patient=InsertPatient("Status","Sample");
			DataCore.NonQ("INSERT INTO eform (FormType,PatNum,DateTimeShown,Description,DateTEdited,MaxWidth,EFormDefNum,Status,RevID,ShowLabelsBold,"
				+"SpaceBelowEachField,SpaceToRightEachField,SaveImageCategory) VALUES "
				+"(0,"+patient.PatNum+",NOW(),'old imported',NOW(),450,0,3,0,0,-1,-1,0),"
				+"(0,"+patient.PatNum+",NOW(),'old unmatched',NOW(),450,0,4,0,0,-1,-1,0),"
				+"(0,"+patient.PatNum+",NOW(),'office form',NOW(),450,0,0,0,0,-1,-1,0)");
			List<EForm> listEForms=EForms.GetForPatient(patient.PatNum);
			Assert.AreEqual(3,listEForms.Count);
			Assert.AreEqual(2,listEForms.Count(x => x.Status==EnumEFormStatus.Filled));
			Assert.AreEqual(2,EForms.GetForPatient(patient.PatNum,EnumEFormStatus.Filled).Count);
			Assert.AreEqual(1,EForms.GetForPatient(patient.PatNum,EnumEFormStatus.None).Count);
			long eFormNumImported=listEForms.First(x => x.Description=="old imported").EFormNum;
			Assert.AreEqual(EnumEFormStatus.Filled,EForms.GetEForm(eFormNumImported).Status);
		}

		[TestMethod]
		public void EnglishCopies_SavedOnInsert() {
			Patient patient=InsertPatient("Lang","Sample");
			EFormFieldDef eFormFieldDef=new EFormFieldDef();
			eFormFieldDef.FieldType=EnumEFormFieldType.RadioButtons;
			eFormFieldDef.ValueLabel="Do you smoke?";
			eFormFieldDef.PickListVis="Yes|No";
			eFormFieldDef.PickListDb="|";
			eFormFieldDef.DbLink="";
			eFormFieldDef.ConditionalParent="";
			eFormFieldDef.ConditionalValue="";
			eFormFieldDef.ReportableName="";
			EFormField eFormFieldTranslated=EFormFields.FromDef(eFormFieldDef,patient.PatNum);
			//What EForms.TranslateFields does for a patient whose language has translations:
			eFormFieldTranslated.ValueLabel="¿Fuma usted?";
			eFormFieldTranslated.PickListVis="Sí|No";
			EFormField eFormFieldOld=EFormLogicTests.Field(EnumEFormFieldType.TextField,"","","Comments");//made without FromDef
			EForm eForm=SaveForm(patient.PatNum,EnumEFormStatus.None,eFormFieldTranslated,eFormFieldOld);
			DataTable table=DataCore.GetTable("SELECT ValueLabel,ValueLabelEnglish,PickListVis,PickListVisEnglish FROM eformfield WHERE EFormNum="
				+eForm.EFormNum+" ORDER BY ItemOrder");
			Assert.AreEqual("¿Fuma usted?",table.Rows[0]["ValueLabel"].ToString());
			Assert.AreEqual("Do you smoke?",table.Rows[0]["ValueLabelEnglish"].ToString());
			Assert.AreEqual("Yes|No",table.Rows[0]["PickListVisEnglish"].ToString());
			Assert.AreEqual("Comments",table.Rows[1]["ValueLabelEnglish"].ToString());
		}

		[TestMethod]
		public void AutoImport_WritesOnlyFlaggedFieldsAndLogsTheImport() {
			Patient patient=InsertPatient("Old","Keep");
			EFormField eFormFieldFName=EFormLogicTests.Field(EnumEFormFieldType.TextField,"FName","New");
			eFormFieldFName.AutoImport=true;
			EFormField eFormFieldLName=EFormLogicTests.Field(EnumEFormFieldType.TextField,"LName","Other");//reviewed by the office, not auto
			EFormField eFormFieldCity=EFormLogicTests.Field(EnumEFormFieldType.TextField,"City","");
			eFormFieldCity.AutoImport=true;//empty answer: nothing is wiped
			EForm eForm=SaveForm(patient.PatNum,EnumEFormStatus.Filled,eFormFieldFName,eFormFieldLName,eFormFieldCity);
			DataCore.NonQ("UPDATE patient SET City='Keepsville' WHERE PatNum="+patient.PatNum);
			List<string> listChanges=EFormImport.ImportAutoFields(eForm);
			Assert.AreEqual(1,listChanges.Count);
			Patient patientDb=Patients.GetPat(patient.PatNum);
			Assert.AreEqual("New",patientDb.FName);
			Assert.AreEqual("Keep",patientDb.LName);
			Assert.AreEqual("Keepsville",patientDb.City);
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PermType="+(int)EnumPermType.EFormImport+" AND PatNum="+patient.PatNum));
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PermType="+(int)EnumPermType.PatientEdit+" AND PatNum="+patient.PatNum
				+" AND LogText LIKE '%eForm #"+eForm.EFormNum+"%'"));
			//Importing never changes the status (Open Dental 25.2).
			Assert.AreEqual(((int)EnumEFormStatus.Filled).ToString(),DataCore.GetScalar("SELECT Status FROM eform WHERE EFormNum="+eForm.EFormNum));
		}

		[TestMethod]
		public void AutoImport_AllergyCheckboxes() {
			Patient patient=InsertPatient("Allergy","Sample");
			AllergyDef allergyDef=new AllergyDef();
			allergyDef.Description="Synthetic Latex";
			AllergyDefs.Insert(allergyDef);
			EFormField eFormFieldLatex=EFormLogicTests.Field(EnumEFormFieldType.CheckBox,"allergy:Synthetic Latex","X");
			eFormFieldLatex.AutoImport=true;
			EFormField eFormFieldNotAuto=EFormLogicTests.Field(EnumEFormFieldType.CheckBox,"allergy:Synthetic Pollen","X");
			EForm eForm=SaveForm(patient.PatNum,EnumEFormStatus.Filled,eFormFieldLatex,eFormFieldNotAuto);
			EFormImport.ImportAutoFields(eForm);
			List<Allergy> listAllergies=Allergies.GetAll(patient.PatNum,showInactive:false);
			Assert.AreEqual(1,listAllergies.Count);
			Assert.AreEqual(allergyDef.AllergyDefNum,listAllergies[0].AllergyDefNum);
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM allergydef WHERE Description='Synthetic Pollen'"));
			//Unchecking it on a later form makes it inactive.
			eFormFieldLatex.ValueString="";
			EForm eForm2=SaveForm(patient.PatNum,EnumEFormStatus.Filled,eFormFieldLatex);
			EFormImport.ImportAutoFields(eForm2);
			Assert.AreEqual(0,Allergies.GetAll(patient.PatNum,showInactive:false).Count);
		}

		[TestMethod]
		public void Prefill_FromGuarantorThroughTheFiller() {
			Patient patientGuarantor=InsertPatient("Parent","Prefill");
			Patient patientOld=patientGuarantor.Copy();
			patientGuarantor.Address="200 Synthetic Rd";
			patientGuarantor.City="Faketown";
			Patients.Update(patientGuarantor,patientOld);
			Patient patientChild=InsertPatient("Child","Prefill",patientGuarantor.PatNum);
			EFormFieldDef eFormFieldDefAddress=new EFormFieldDef();
			eFormFieldDefAddress.FieldType=EnumEFormFieldType.TextField;
			eFormFieldDefAddress.DbLink="Address";
			eFormFieldDefAddress.ValueLabel="Address";
			eFormFieldDefAddress.PickListVis="";
			eFormFieldDefAddress.PickListDb="";
			eFormFieldDefAddress.ConditionalParent="";
			eFormFieldDefAddress.ConditionalValue="";
			eFormFieldDefAddress.ReportableName="";
			eFormFieldDefAddress.PrefillFromGuar=true;
			EFormFieldDef eFormFieldDefCity=eFormFieldDefAddress.Copy();
			eFormFieldDefCity.DbLink="City";
			eFormFieldDefCity.ValueLabel="City";
			eFormFieldDefCity.PrefillFromGuar=false;
			EFormDef eFormDef=new EFormDef();
			eFormDef.Description="Synthetic child form";
			eFormDef.ListEFormFieldDefs=new List<EFormFieldDef>() { eFormFieldDefAddress,eFormFieldDefCity };
			EForm eFormChild=EForms.CreateEFormFromEFormDef(eFormDef,patientChild.PatNum,EnumEFormStatus.None);
			EFormFiller.FillFields(eFormChild);
			Assert.AreEqual("200 Synthetic Rd",eFormChild.ListEFormFields[0].ValueString);
			Assert.AreEqual("",eFormChild.ListEFormFields[1].ValueString,"City is not marked Prefill from Guarantor");
			//The guarantor's own form is filled from the guarantor only.
			EForm eFormGuarantor=EForms.CreateEFormFromEFormDef(eFormDef,patientGuarantor.PatNum,EnumEFormStatus.None);
			EFormFiller.FillFields(eFormGuarantor);
			Assert.AreEqual("200 Synthetic Rd",eFormGuarantor.ListEFormFields[0].ValueString);
		}

		[TestMethod]
		public void HiddenMedications_AreNotLinkedOnImport() {
			Medication medicationHidden=new Medication();
			medicationHidden.MedName="Hiddenolol";
			medicationHidden.IsHidden=true;
			Medications.Insert(medicationHidden);
			Medication medicationVisible=new Medication();
			medicationVisible.MedName="Visibolol";
			Medications.Insert(medicationVisible);
			Medications.RefreshCache();
			Assert.IsNull(EFormImport.FindMedicationForImport("hiddenolol"));
			Medication medicationFound=EFormImport.FindMedicationForImport(" Visibolol ");
			Assert.IsNotNull(medicationFound);
			Assert.AreEqual(medicationVisible.MedicationNum,medicationFound.MedicationNum);
		}
	}
}
