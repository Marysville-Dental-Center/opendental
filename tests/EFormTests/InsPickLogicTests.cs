// Unit tests for the eForm Patient List and Ins Pick fields and pending insurance (package A-EF2) that need no database: value formats from Open
// Dental's 25.3 database docs, slots, child fields, conditional logic, validation, Auto Import selection and layout. Synthetic values only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace EFormTests {
	[TestClass]
	public class InsPickLogicTests {
		internal static EFormField InsPick(string dbLink,string value,string label="Primary Insurance") {
			EFormField eFormField=EFormLogicTests.Field(EnumEFormFieldType.InsPick,dbLink,value,label);
			eFormField.PickListVis="None|New|Existing";
			eFormField.PickListVisEnglish="None|New|Existing";
			eFormField.ValueLabelEnglish=label;
			eFormField.ConditionalParent="";
			eFormField.ConditionalValue="";
			return eFormField;
		}

		internal static EFormField Child(EnumEFormFieldType fieldType,string dbLink,string value,string parentLabel="Primary Insurance") {
			EFormField eFormField=EFormLogicTests.Field(fieldType,dbLink,value);
			eFormField.ConditionalParent=parentLabel;
			eFormField.ConditionalValue="New";
			return eFormField;
		}

		[TestMethod]
		public void EnumValues_MatchPublishedDocs() {
			Assert.AreEqual(8,(int)EnumEFormFieldType.PatientList);
			Assert.AreEqual(9,(int)EnumEFormFieldType.InsPick);
			Assert.AreEqual(0,(int)Relat.Self);
			Assert.AreEqual(8,(int)Relat.Dependent);
		}

		[TestMethod]
		public void Ordinal_FromDocumentedLinks() {
			Assert.AreEqual(1,EFormInsPick.GetOrdinal("ins1PlanFromList"));
			Assert.AreEqual(2,EFormInsPick.GetOrdinal("ins2PlanFromList"));
			Assert.AreEqual(0,EFormInsPick.GetOrdinal(""));
			Assert.AreEqual(0,EFormInsPick.GetOrdinal("ins1CarrierName"));
			CollectionAssert.AreEqual(new List<string>() { "None","ins1PlanFromList","ins2PlanFromList" },EFormFieldsAvailable.GetList_InsPick());
		}

		[TestMethod]
		public void Values_ParseDocumentedFormats() {
			Assert.AreEqual(0,EFormInsPick.GetOptionIndex(""));
			Assert.AreEqual(1,EFormInsPick.GetOptionIndex("New"));
			Assert.AreEqual(2,EFormInsPick.GetOptionIndex("InsSubNum:42"));
			Assert.AreEqual(2,EFormInsPick.GetOptionIndex("InsPendingNum:7"));
			Assert.AreEqual(42,EFormInsPick.GetInsSubNum("InsSubNum:42"));
			Assert.AreEqual(0,EFormInsPick.GetInsSubNum("InsPendingNum:42"));
			Assert.AreEqual(7,EFormInsPick.GetInsPendingNum("InsPendingNum:7"));
			Assert.AreEqual(0,EFormInsPick.GetInsPendingNum("InsPendingNum:x"));
			Assert.AreEqual(0,EFormInsPick.GetOptionIndex("InsSubNum:0"),"a zero key is not a valid Existing answer");
			Assert.AreEqual("InsSubNum:42",EFormInsPick.ToValueInsSub(42));
			Assert.AreEqual("InsPendingNum:7",EFormInsPick.ToValueInsPending(7));
			Assert.AreEqual(143,EFormInsPick.GetPatNumPicked(EFormLogicTests.Field(EnumEFormFieldType.PatientList,"","143")));
			Assert.AreEqual(0,EFormInsPick.GetPatNumPicked(EFormLogicTests.Field(EnumEFormFieldType.PatientList,"","")));
		}

		[TestMethod]
		public void Options_FallBackToDefaultsWhenNotThree() {
			CollectionAssert.AreEqual(new List<string>() { "None","New","Existing" },EFormInsPick.GetOptions("Yes|No"));
			CollectionAssert.AreEqual(new List<string>() { "Ninguno","Nuevo","Existente" },EFormInsPick.GetOptions("Ninguno|Nuevo|Existente"));
		}

		[TestMethod]
		public void SubscriberID_StoredWithoutDashesOrSpaces() {
			Assert.AreEqual("ABC123456",EFormInsPick.NormalizeSubscriberID(" ABC-123 456 "));
			Assert.AreEqual("",EFormInsPick.NormalizeSubscriberID(null));
		}

		[TestMethod]
		public void ChildFields_ConditionalChildrenFirst_ThenSlotLinks() {
			EFormField eFormFieldInsPick=InsPick("ins1PlanFromList","New");
			EFormField eFormFieldCarrier=Child(EnumEFormFieldType.TextField,"ins1CarrierName","Delta");
			EFormField eFormFieldOther=EFormLogicTests.Field(EnumEFormFieldType.TextField,"ins1GroupNum","G1");
			List<EFormField> listFields=new List<EFormField>() { eFormFieldInsPick,eFormFieldCarrier,eFormFieldOther };
			CollectionAssert.AreEqual(new List<EFormField>() { eFormFieldCarrier },EFormInsPick.GetChildFields(eFormFieldInsPick,listFields));
			//Without conditional children, the slot's ins1 text and radio fields are used.
			eFormFieldCarrier.ConditionalParent="";
			List<EFormField> listChildren=EFormInsPick.GetChildFields(eFormFieldInsPick,listFields);
			Assert.AreEqual(2,listChildren.Count);
			Assert.AreEqual("Delta",EFormInsPick.GetChildValue(listChildren,"CarrierName"));
			Assert.AreEqual("G1",EFormInsPick.GetChildValue(listChildren,"GroupNum"));
			Assert.AreEqual("",EFormInsPick.GetChildValue(listChildren,"Employer"));
			Assert.AreEqual("SubscriberID",EFormInsPick.GetInsFieldSuffix("ins2SubscriberID"));
			Assert.AreEqual("",EFormInsPick.GetInsFieldSuffix("ins3SubscriberID"));
		}

		[TestMethod]
		public void ConditionalLogic_InsPickShowsChildrenOnlyForNew() {
			EFormField eFormFieldInsPick=InsPick("ins1PlanFromList","");
			EFormField eFormFieldCarrier=Child(EnumEFormFieldType.TextField,"ins1CarrierName","");
			EFormField eFormFieldSubscriber=Child(EnumEFormFieldType.PatientList,"","");
			List<EFormField> listFields=new List<EFormField>() { eFormFieldInsPick,eFormFieldCarrier,eFormFieldSubscriber };
			EFormFields.SetIsHiddenConditFlags(listFields);
			Assert.IsTrue(eFormFieldCarrier.IsHiddenCondit,"None hides the New fields");
			eFormFieldInsPick.ValueString="New";
			EFormFields.SetIsHiddenConditFlags(listFields);
			Assert.IsFalse(eFormFieldCarrier.IsHiddenCondit);
			Assert.IsFalse(eFormFieldSubscriber.IsHiddenCondit);
			eFormFieldInsPick.ValueString="InsSubNum:5";
			EFormFields.SetIsHiddenConditFlags(listFields);
			Assert.IsTrue(eFormFieldCarrier.IsHiddenCondit,"Existing hides the New fields");
		}

		[TestMethod]
		public void ConditionalLogic_TranslatedInsPickComparesEnglish() {
			EFormField eFormFieldInsPick=InsPick("ins1PlanFromList","New","Seguro");
			eFormFieldInsPick.ValueLabelEnglish="Primary Insurance";
			eFormFieldInsPick.PickListVis="Ninguno|Nuevo|Existente";
			EFormField eFormFieldCarrier=Child(EnumEFormFieldType.TextField,"ins1CarrierName","");
			EFormFields.SetIsHiddenConditFlags(new List<EFormField>() { eFormFieldInsPick,eFormFieldCarrier });
			Assert.IsFalse(eFormFieldCarrier.IsHiddenCondit);
			Assert.AreEqual("New",EFormInsPick.GetValParentEnglish(eFormFieldInsPick));
		}

		[TestMethod]
		public void AutoImport_InsPickOnlyWithSlotLink() {
			EFormField eFormFieldInsPick=InsPick("ins1PlanFromList","New");
			eFormFieldInsPick.AutoImport=true;
			Assert.IsTrue(EFormImport.IsAutoImportField(eFormFieldInsPick));
			eFormFieldInsPick.ValueString="";
			Assert.IsFalse(EFormImport.IsAutoImportField(eFormFieldInsPick),"None has nothing to import");
			EFormField eFormFieldNoSlot=InsPick("","New");
			eFormFieldNoSlot.AutoImport=true;
			Assert.IsFalse(EFormImport.IsAutoImportField(eFormFieldNoSlot));
			Assert.IsTrue(EFormFieldsAvailable.IsAutoImportAllowed("ins2PlanFromList"));
		}

		[TestMethod]
		public void NewTypes_DoNotStack() {
			Assert.IsFalse(EFormFieldDefs.IsHorizStackableType(EnumEFormFieldType.PatientList));
			Assert.IsFalse(EFormFieldDefs.IsHorizStackableType(EnumEFormFieldType.InsPick));
		}

		[TestMethod]
		public void Required_PatientListNeedsAPick_InsPickNoneIsAnAnswer() {
			EFormField eFormFieldList=EFormLogicTests.Field(EnumEFormFieldType.PatientList,"","");
			eFormFieldList.ValueLabel=new EFormPatListLayout() { Label="Subscriber" }.Serialize();
			eFormFieldList.IsRequired=true;
			EFormField eFormFieldInsPick=InsPick("ins1PlanFromList","");
			eFormFieldInsPick.IsRequired=true;
			EForm eForm=EFormLogicTests.Form(eFormFieldInsPick,eFormFieldList);
			EFormValidation eFormValidation=EForms.ValidateRequired(eForm,isMedNoneChecked:false);
			Assert.AreEqual("Subscriber is a required field",eFormValidation.ErrorMsg);
			eFormFieldList.ValueString="143";
			Assert.AreEqual("",EForms.ValidateRequired(eForm,isMedNoneChecked:false).ErrorMsg);
		}

		[TestMethod]
		public void PatListLayout_RoundTripsAndReadsOddRows() {
			EFormPatListLayout eFormPatListLayout=new EFormPatListLayout() { Label="Who is the subscriber?" };
			string json=eFormPatListLayout.Serialize();
			Assert.AreEqual("Who is the subscriber?",EFormPatListLayout.Deserialize(json).Label);
			Assert.AreEqual("Subscriber",EFormPatListLayout.Deserialize("").Label);
			Assert.AreEqual("Plain label",EFormPatListLayout.Deserialize("Plain label").Label);
			Assert.AreEqual("X",EFormPatListLayout.Deserialize("{\"Label\":\"X\",\"SomethingElse\":3}").Label,"unknown members are ignored");
		}

		private static InsPending Pending(long patNum,long patNumSub,Relat relat,string subID,string carrier) {
			InsPending insPending=new InsPending();
			insPending.PatNum=patNum;
			insPending.PatNumSubscriber=patNumSub;
			insPending.Relationship=relat;
			insPending.SubscriberID=subID;
			insPending.CarrierName=carrier;
			return insPending;
		}

		[TestMethod]
		public void Validate_SelfExactlyWhenPatientIsSubscriber_AndIDRequired() {
			Assert.AreEqual("",InsPendings.Validate(Pending(1,1,Relat.Self,"A1","Delta")));
			Assert.AreEqual("",InsPendings.Validate(Pending(2,1,Relat.Child,"A1","Delta")));
			Assert.AreNotEqual("",InsPendings.Validate(Pending(1,1,Relat.Spouse,"A1","Delta")));
			Assert.AreNotEqual("",InsPendings.Validate(Pending(2,1,Relat.Self,"A1","Delta")));
			Assert.AreNotEqual("",InsPendings.Validate(Pending(1,1,Relat.Self," - ","Delta")));
			Assert.AreNotEqual("",InsPendings.Validate(Pending(1,0,Relat.Self,"A1","Delta")));
		}

		[TestMethod]
		public void FamilyDuplicates_SameSubscriberIDAndCarrierIgnoringCaseAndDashes() {
			InsPending insPending=Pending(2,1,Relat.Child,"A-1","Delta Dental");
			insPending.InsPendingNum=10;
			InsPending insPendingSibling=Pending(3,1,Relat.Child,"a1","delta dental ");
			insPendingSibling.InsPendingNum=11;
			InsPending insPendingOtherCarrier=Pending(4,1,Relat.Child,"A1","Other");
			insPendingOtherCarrier.InsPendingNum=12;
			InsPending insPendingSamePatient=Pending(2,1,Relat.Child,"A1","Delta Dental");
			insPendingSamePatient.InsPendingNum=13;
			List<InsPending> listDuplicates=InsPendings.GetFamilyDuplicates(insPending,
				new List<InsPending>() { insPending,insPendingSibling,insPendingOtherCarrier,insPendingSamePatient });
			CollectionAssert.AreEqual(new List<InsPending>() { insPendingSibling },listDuplicates);
		}

		[TestMethod]
		public void GuessRelationship_UsesPositionOnly() {
			Patient patient=new Patient() { PatNum=2,Position=PatientPosition.Child };
			Patient subscriber=new Patient() { PatNum=1,Position=PatientPosition.Married };
			Assert.AreEqual(Relat.Child,InsPendings.GuessRelationship(patient,subscriber));
			patient.Position=PatientPosition.Married;
			Assert.AreEqual(Relat.Spouse,InsPendings.GuessRelationship(patient,subscriber));
			patient.Position=PatientPosition.Single;
			Assert.AreEqual(Relat.Dependent,InsPendings.GuessRelationship(patient,subscriber));
			Assert.AreEqual(Relat.Self,InsPendings.GuessRelationship(patient,patient));
		}

		[TestMethod]
		public void ConvertPermissions() {
			CollectionAssert.AreEqual(new List<EnumPermType>() { EnumPermType.InsPlanCreate,EnumPermType.PatPlanCreate },
				InsPendings.GetPermissionsForConvert(carrierNum:0,planNum:0));
			CollectionAssert.AreEqual(new List<EnumPermType>() { EnumPermType.InsPlanEdit,EnumPermType.PatPlanCreate },
				InsPendings.GetPermissionsForConvert(carrierNum:5,planNum:9));
			CollectionAssert.AreEqual(new List<EnumPermType>() { EnumPermType.InsPlanCreate,EnumPermType.PatPlanCreate },
				InsPendings.GetPermissionsForConvert(carrierNum:5,planNum:0));
		}
	}
}
