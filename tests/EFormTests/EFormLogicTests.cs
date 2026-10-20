// Unit tests for desktop eForms (package A-EF1) that need no database: prefill from the guarantor, Auto Import field selection and patient
// field mapping, the removed statuses, English copies of translated fields, conditional logic and required fields. Synthetic values only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace EFormTests {
	[TestClass]
	public class EFormLogicTests {
		internal static EFormField Field(EnumEFormFieldType fieldType,string dbLink,string value,string label="") {
			EFormField eFormField=new EFormField();
			eFormField.FieldType=fieldType;
			eFormField.DbLink=dbLink;
			eFormField.ValueString=value;
			eFormField.ValueLabel=label=="" ? dbLink : label;
			return eFormField;
		}

		internal static EForm Form(params EFormField[] arrayFields) {
			EForm eForm=new EForm();
			eForm.ListEFormFields=arrayFields.ToList();
			return eForm;
		}

		private static Patient Child() {
			Patient patient=new Patient();
			patient.PatNum=2;
			patient.Guarantor=1;
			patient.FName="Kid";
			patient.LName="Sample";
			patient.Address="";
			patient.City="";
			patient.Zip="";
			patient.WirelessPhone="";
			patient.HmPhone="";
			return patient;
		}

		private static Patient Guarantor() {
			Patient patient=new Patient();
			patient.PatNum=1;
			patient.Guarantor=1;
			patient.FName="Parent";
			patient.LName="Sample";
			patient.Address="100 Test Way";
			patient.City="Testville";
			patient.Zip="99999";
			patient.WirelessPhone="(555)555-0100";
			patient.HmPhone="(555)555-0199";
			return patient;
		}

		#region Prefill from guarantor
		[TestMethod]
		public void Prefill_FillsEmptyEligibleFieldsFromGuarantor() {
			EFormField eFormFieldAddress=Field(EnumEFormFieldType.TextField,"Address","");
			eFormFieldAddress.PrefillFromGuar=true;
			EFormField eFormFieldWireless=Field(EnumEFormFieldType.TextField,"WirelessPhone","");
			eFormFieldWireless.PrefillFromGuar=true;
			EForm eForm=Form(eFormFieldAddress,eFormFieldWireless);
			EFormFiller.ApplyPrefillFromGuarantor(eForm,Child(),Guarantor());
			Assert.AreEqual("100 Test Way",eFormFieldAddress.ValueString);
			Assert.AreEqual("(555)555-0100",eFormFieldWireless.ValueString);
		}

		[TestMethod]
		public void Prefill_OnlyWhenPatientValueIsEmpty() {
			Patient patient=Child();
			patient.City="Own Town";
			EFormField eFormFieldCity=Field(EnumEFormFieldType.TextField,"City","Own Town");
			eFormFieldCity.PrefillFromGuar=true;
			EFormFiller.ApplyPrefillFromGuarantor(Form(eFormFieldCity),patient,Guarantor());
			Assert.AreEqual("Own Town",eFormFieldCity.ValueString);
		}

		[TestMethod]
		public void Prefill_NotWithoutTheFlag() {
			EFormField eFormFieldAddress=Field(EnumEFormFieldType.TextField,"Address","");
			EFormFiller.ApplyPrefillFromGuarantor(Form(eFormFieldAddress),Child(),Guarantor());
			Assert.AreEqual("",eFormFieldAddress.ValueString);
		}

		[TestMethod]
		public void Prefill_NotForOwnGuarantor() {
			Patient patient=Child();
			patient.Guarantor=patient.PatNum;
			EFormField eFormFieldAddress=Field(EnumEFormFieldType.TextField,"Address","");
			eFormFieldAddress.PrefillFromGuar=true;
			EFormFiller.ApplyPrefillFromGuarantor(Form(eFormFieldAddress),patient,patient);
			Assert.AreEqual("",eFormFieldAddress.ValueString);
			EFormFiller.ApplyPrefillFromGuarantor(Form(eFormFieldAddress),Child(),null);
			Assert.AreEqual("",eFormFieldAddress.ValueString);
		}

		[TestMethod]
		public void Prefill_IgnoresIneligibleLinks() {
			//HmPhone and FName are not in Open Dental's published list for PrefillFromGuar, even if the flag is set.
			EFormField eFormFieldHmPhone=Field(EnumEFormFieldType.TextField,"HmPhone","");
			eFormFieldHmPhone.PrefillFromGuar=true;
			EFormField eFormFieldFName=Field(EnumEFormFieldType.TextField,"FName","");
			eFormFieldFName.PrefillFromGuar=true;
			EFormFiller.ApplyPrefillFromGuarantor(Form(eFormFieldHmPhone,eFormFieldFName),Child(),Guarantor());
			Assert.AreEqual("",eFormFieldHmPhone.ValueString);
			Assert.AreEqual("",eFormFieldFName.ValueString);
		}

		[TestMethod]
		public void Prefill_EligibleListMatchesPublishedDocs() {
			CollectionAssert.AreEquivalent(new[] { "Address","Address2","City","State","Zip","WirelessPhone" },EFormFieldsAvailable.GetList_PrefillFromGuar());
			Assert.IsFalse(EFormFieldsAvailable.IsPrefillFromGuarAllowed(""));
			Assert.IsFalse(EFormFieldsAvailable.IsPrefillFromGuarAllowed("FName"));
		}
		#endregion Prefill from guarantor

		#region Auto import
		[TestMethod]
		public void AutoImport_OnlyFlaggedVisibleAnsweredLinkedFields() {
			EFormField eFormFieldFName=Field(EnumEFormFieldType.TextField,"FName","Ann");
			eFormFieldFName.AutoImport=true;
			EFormField eFormFieldLName=Field(EnumEFormFieldType.TextField,"LName","Zed");//not flagged
			EFormField eFormFieldAddressEmpty=Field(EnumEFormFieldType.TextField,"Address","  ");
			eFormFieldAddressEmpty.AutoImport=true;//empty answers never wipe data
			EFormField eFormFieldNoLink=Field(EnumEFormFieldType.TextField,"","free text","Comments");
			eFormFieldNoLink.AutoImport=true;
			EFormField eFormFieldWkPhoneOnUnsupported=Field(EnumEFormFieldType.TextField,"ins1GroupNum","123");
			eFormFieldWkPhoneOnUnsupported.AutoImport=true;//insurance text links are not in the published Auto Import list
			EFormField eFormFieldAllergyNone=Field(EnumEFormFieldType.CheckBox,"allergiesNone","");
			eFormFieldAllergyNone.AutoImport=true;//an unchecked box is still an answer
			List<EFormField> listEFormFields=EFormImport.GetAutoImportFields(Form(eFormFieldFName,eFormFieldLName,eFormFieldAddressEmpty,eFormFieldNoLink,
				eFormFieldWkPhoneOnUnsupported,eFormFieldAllergyNone));
			CollectionAssert.AreEqual(new[] { eFormFieldFName,eFormFieldAllergyNone },listEFormFields);
		}

		[TestMethod]
		public void AutoImport_SkipsFieldsHiddenByConditionalLogic() {
			EFormField eFormFieldParent=Field(EnumEFormFieldType.CheckBox,"","","Has a nickname");
			EFormField eFormFieldChild=Field(EnumEFormFieldType.TextField,"Preferred","Bo");
			eFormFieldChild.AutoImport=true;
			eFormFieldChild.ConditionalParent="Has a nickname";
			eFormFieldChild.ConditionalValue="X";
			EForm eForm=Form(eFormFieldParent,eFormFieldChild);
			Assert.AreEqual(0,EFormImport.GetAutoImportFields(eForm).Count);
			eFormFieldParent.ValueString="X";
			CollectionAssert.AreEqual(new[] { eFormFieldChild },EFormImport.GetAutoImportFields(eForm));
		}

		[TestMethod]
		public void AutoImport_WritesOnlyFlaggedFieldsOntoThePatient() {
			EFormField eFormFieldFName=Field(EnumEFormFieldType.TextField,"FName","Ann");
			eFormFieldFName.AutoImport=true;
			EFormField eFormFieldLName=Field(EnumEFormFieldType.TextField,"LName","Changed");
			EFormField eFormFieldGender=Field(EnumEFormFieldType.RadioButtons,"Gender","Female");
			eFormFieldGender.AutoImport=true;
			EFormField eFormFieldIce=Field(EnumEFormFieldType.TextField,"ICEName","Contact Person");
			eFormFieldIce.AutoImport=true;
			EForm eForm=Form(eFormFieldFName,eFormFieldLName,eFormFieldGender,eFormFieldIce);
			Patient patient=Child();
			patient.Gender=PatientGender.Unknown;
			PatientNote patientNote=new PatientNote();
			patientNote.ICEName="";
			List<string> listChanges=EFormImport.ApplyPatientFields(EFormImport.GetAutoImportFields(eForm),patient,patientNote);
			Assert.AreEqual("Ann",patient.FName);
			Assert.AreEqual("Sample",patient.LName);
			Assert.AreEqual(PatientGender.Female,patient.Gender);
			Assert.AreEqual("Contact Person",patientNote.ICEName);
			Assert.AreEqual(3,listChanges.Count);
		}

		[TestMethod]
		public void AutoImport_SkipsUnreadableValuesAndKeepsSsnOutOfTheLog() {
			Patient patient=Child();
			patient.Gender=PatientGender.Male;
			patient.SSN="";
			patient.Birthdate=new DateTime(2015,1,2);
			List<EFormField> listEFormFields=new List<EFormField>() {
				Field(EnumEFormFieldType.RadioButtons,"Gender","NotAGender"),
				Field(EnumEFormFieldType.DateField,"Birthdate","not a date"),
				Field(EnumEFormFieldType.TextField,"SSN","123-45-6789"),
				Field(EnumEFormFieldType.RadioButtons,"StudentStatus","Q"),
			};
			List<string> listChanges=EFormImport.ApplyPatientFields(listEFormFields,patient,new PatientNote());
			Assert.AreEqual(PatientGender.Male,patient.Gender);
			Assert.AreEqual(new DateTime(2015,1,2),patient.Birthdate);
			Assert.AreEqual(1,listChanges.Count);
			Assert.IsFalse(listChanges[0].Contains("6789"));
		}

		[TestMethod]
		public void AutoImport_AllowedLinksFollowPublishedDocs() {
			Assert.IsTrue(EFormFieldsAvailable.IsAutoImportAllowed("FName"));
			Assert.IsTrue(EFormFieldsAvailable.IsAutoImportAllowed("allergy:Penicillin"));
			Assert.IsTrue(EFormFieldsAvailable.IsAutoImportAllowed("problem:Asthma"));
			Assert.IsTrue(EFormFieldsAvailable.IsAutoImportAllowed("referredFrom"));
			Assert.IsFalse(EFormFieldsAvailable.IsAutoImportAllowed(""));
			Assert.IsFalse(EFormFieldsAvailable.IsAutoImportAllowed("None"));
			Assert.IsFalse(EFormFieldsAvailable.IsAutoImportAllowed("StateNoValidation"));
			Assert.IsFalse(EFormFieldsAvailable.IsAutoImportAllowed("ins1CarrierName"));
		}

		[TestMethod]
		public void AutoImport_RunsOnlyOnTheSaveThatMakesTheFormFilled() {
			Assert.IsTrue(EFormImport.ShouldAutoImportOnSave(EnumEFormStatus.None));
			Assert.IsTrue(EFormImport.ShouldAutoImportOnSave(EnumEFormStatus.ShowInEClipboard));
			Assert.IsFalse(EFormImport.ShouldAutoImportOnSave(EnumEFormStatus.Filled));
		}

		[TestMethod]
		public void AutoImport_YesNoOfAllergyAndProblemFields() {
			Assert.AreEqual(true,EFormImport.GetYesNo(Field(EnumEFormFieldType.CheckBox,"allergy:Latex","X")));
			Assert.AreEqual(false,EFormImport.GetYesNo(Field(EnumEFormFieldType.CheckBox,"allergy:Latex","")));
			Assert.AreEqual(true,EFormImport.GetYesNo(Field(EnumEFormFieldType.RadioButtons,"problem:Asthma","Y")));
			Assert.AreEqual(false,EFormImport.GetYesNo(Field(EnumEFormFieldType.RadioButtons,"problem:Asthma","N")));
			Assert.IsNull(EFormImport.GetYesNo(Field(EnumEFormFieldType.RadioButtons,"problem:Asthma","")));
		}
		#endregion Auto import

		#region Status
		[TestMethod]
		public void Status_RemovedValuesLoadAsFilled() {
			foreach(int stored in new[] { 3,4,99 }) {
				EForm eForm=new EForm();
				eForm.Status=(EnumEFormStatus)stored;
				EForms.NormalizeStatus(eForm);
				Assert.AreEqual(EnumEFormStatus.Filled,eForm.Status,"stored "+stored);
			}
			EForm eFormNone=new EForm();
			eFormNone.Status=EnumEFormStatus.None;
			EForms.NormalizeStatus(eFormNone);
			Assert.AreEqual(EnumEFormStatus.None,eFormNone.Status);
		}

		[TestMethod]
		public void Status_FilledFilterAlsoMatchesRemovedValues() {
			CollectionAssert.AreEquivalent(new[] { 2,3,4 },EForms.GetStatusValuesForFilter(EnumEFormStatus.Filled));
			CollectionAssert.AreEquivalent(new[] { 0 },EForms.GetStatusValuesForFilter(EnumEFormStatus.None));
			Assert.AreEqual(3,Enum.GetValues(typeof(EnumEFormStatus)).Length,"25.2 keeps only None, ShowInEClipboard and Filled");
		}
		#endregion Status

		#region Defs, English copies and conditional logic
		[TestMethod]
		public void FromDef_CopiesFlagsAndKeepsEnglishText() {
			EFormFieldDef eFormFieldDef=new EFormFieldDef();
			eFormFieldDef.FieldType=EnumEFormFieldType.RadioButtons;
			eFormFieldDef.DbLink="";
			eFormFieldDef.ValueLabel="Do you smoke?";
			eFormFieldDef.PickListVis="Yes|No";
			eFormFieldDef.PickListDb="|";
			eFormFieldDef.AutoImport=true;
			eFormFieldDef.PrefillFromGuar=true;
			EFormField eFormField=EFormFields.FromDef(eFormFieldDef);
			Assert.IsTrue(eFormField.AutoImport);
			Assert.IsTrue(eFormField.PrefillFromGuar);
			Assert.AreEqual("Do you smoke?",eFormField.ValueLabelEnglish);
			Assert.AreEqual("Yes|No",eFormField.PickListVisEnglish);
			EFormFieldDef eFormFieldDefBack=EFormFields.ToDef(eFormField);
			Assert.IsTrue(eFormFieldDefBack.AutoImport);
			Assert.IsTrue(eFormFieldDefBack.PrefillFromGuar);
		}

		[TestMethod]
		public void EnsureEnglishCopies_FillsOnlyWhenEmpty() {
			EFormField eFormField=Field(EnumEFormFieldType.TextField,"","","Nombre");
			eFormField.PickListVis="";
			EFormFields.EnsureEnglishCopies(eFormField);
			Assert.AreEqual("Nombre",eFormField.ValueLabelEnglish);
			eFormField.ValueLabelEnglish="Name";
			EFormFields.EnsureEnglishCopies(eFormField);
			Assert.AreEqual("Name",eFormField.ValueLabelEnglish);
		}

		[TestMethod]
		public void ConditionalLogic_UsesEnglishOnTranslatedForms() {
			//Translated parent: the patient sees Spanish, the def (and the children's conditions) are English.
			EFormField eFormFieldParent=Field(EnumEFormFieldType.RadioButtons,"","Sí","¿Fuma usted?");
			eFormFieldParent.ValueLabelEnglish="Do you smoke?";
			eFormFieldParent.PickListVis="Sí|No";
			eFormFieldParent.PickListVisEnglish="Yes|No";
			eFormFieldParent.PickListDb="|";
			EFormField eFormFieldChildYes=Field(EnumEFormFieldType.TextField,"","","How much?");
			eFormFieldChildYes.ConditionalParent="Do you smoke?";
			eFormFieldChildYes.ConditionalValue="Yes";
			EFormField eFormFieldChildNo=Field(EnumEFormFieldType.TextField,"","","Never smoked?");
			eFormFieldChildNo.ConditionalParent="Do you smoke?";
			eFormFieldChildNo.ConditionalValue="No";
			List<EFormField> listEFormFields=new List<EFormField>() { eFormFieldParent,eFormFieldChildYes,eFormFieldChildNo };
			EFormFields.SetIsHiddenConditFlags(listEFormFields);
			Assert.IsFalse(eFormFieldChildYes.IsHiddenCondit);
			Assert.IsTrue(eFormFieldChildNo.IsHiddenCondit);
			Assert.AreEqual("Yes",EFormFields.GetValParent(eFormFieldParent));
		}

		[TestMethod]
		public void ConditionalLogic_LinkedRadioReturnsEnglishItem() {
			EFormField eFormFieldParent=Field(EnumEFormFieldType.RadioButtons,"Gender","Female","Sexo");
			eFormFieldParent.ValueLabelEnglish="Gender";
			eFormFieldParent.PickListVis="Masculino|Femenino";
			eFormFieldParent.PickListVisEnglish="Male|Female";
			eFormFieldParent.PickListDb="Male|Female";
			Assert.AreEqual("Female",EFormFields.GetValParent(eFormFieldParent));
		}

		[TestMethod]
		public void ConditionalLogic_HiddenParentHidesGrandchildren() {
			EFormField eFormFieldTop=Field(EnumEFormFieldType.CheckBox,"","","Top");
			EFormField eFormFieldMiddle=Field(EnumEFormFieldType.CheckBox,"","X","Middle");
			eFormFieldMiddle.ConditionalParent="Top";
			eFormFieldMiddle.ConditionalValue="X";
			EFormField eFormFieldBottom=Field(EnumEFormFieldType.TextField,"","","Bottom");
			eFormFieldBottom.ConditionalParent="Middle";
			eFormFieldBottom.ConditionalValue="X";
			List<EFormField> listEFormFields=new List<EFormField>() { eFormFieldTop,eFormFieldMiddle,eFormFieldBottom };
			EFormFields.SetIsHiddenConditFlags(listEFormFields);
			Assert.IsTrue(eFormFieldMiddle.IsHiddenCondit);
			Assert.IsTrue(eFormFieldBottom.IsHiddenCondit);
		}
		#endregion Defs, English copies and conditional logic

		#region Required fields
		[TestMethod]
		public void Required_VisibleEmptyFieldFails() {
			EFormField eFormField=Field(EnumEFormFieldType.TextField,"FName","  ","First name");
			eFormField.IsRequired=true;
			EFormValidation eFormValidation=EForms.ValidateRequired(Form(eFormField),isMedNoneChecked:false);
			Assert.AreNotEqual("",eFormValidation.ErrorMsg);
			Assert.AreEqual(1,eFormValidation.PageNum);
		}

		[TestMethod]
		public void Required_HiddenFieldIsNotRequired() {
			EFormField eFormFieldParent=Field(EnumEFormFieldType.CheckBox,"","","Pregnant");
			EFormField eFormFieldChild=Field(EnumEFormFieldType.TextField,"","","Due date");
			eFormFieldChild.IsRequired=true;
			eFormFieldChild.ConditionalParent="Pregnant";
			eFormFieldChild.ConditionalValue="X";
			Assert.AreEqual("",EForms.ValidateRequired(Form(eFormFieldParent,eFormFieldChild),isMedNoneChecked:false).ErrorMsg);
			eFormFieldParent.ValueString="X";
			Assert.AreNotEqual("",EForms.ValidateRequired(Form(eFormFieldParent,eFormFieldChild),isMedNoneChecked:false).ErrorMsg);
		}

		[TestMethod]
		public void Required_OnlyAllergiesNoneAndProblemsNoneCheckboxes() {
			Assert.IsTrue(EFormFieldsAvailable.CanCheckBoxBeRequired("allergiesNone"));
			Assert.IsTrue(EFormFieldsAvailable.CanCheckBoxBeRequired("problemsNone"));
			Assert.IsFalse(EFormFieldsAvailable.CanCheckBoxBeRequired("allergy:Latex"));
			Assert.IsFalse(EFormFieldsAvailable.CanCheckBoxBeRequired(""));
			//A stored Required flag on any other checkbox is not enforced.
			EFormField eFormField=Field(EnumEFormFieldType.CheckBox,"","","I agree");
			eFormField.IsRequired=true;
			Assert.AreEqual("",EForms.ValidateRequired(Form(eFormField),isMedNoneChecked:false).ErrorMsg);
		}

		[TestMethod]
		public void Required_ReportsThePageOfTheField() {
			EFormField eFormFieldPage1=Field(EnumEFormFieldType.TextField,"FName","Ann");
			EFormField eFormFieldBreak=Field(EnumEFormFieldType.PageBreak,"","");
			EFormField eFormFieldPage2=Field(EnumEFormFieldType.DateField,"Birthdate","","Birthdate");
			eFormFieldPage2.IsRequired=true;
			EFormValidation eFormValidation=EForms.ValidateRequired(Form(eFormFieldPage1,eFormFieldBreak,eFormFieldPage2),isMedNoneChecked:false);
			Assert.AreEqual(2,eFormValidation.PageNum);
		}
		#endregion Required fields
	}
}
