// v25_4-eforms-patfields without a database: links, type mapping, fill values, validation, answer conversion and the import plan.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2EFTests {
	[TestClass]
	public class PatFieldLogicTests {
		private CultureInfo _cultureInfoOld;

		[TestInitialize]
		public void TestInitialize() {
			_cultureInfoOld=Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		[TestCleanup]
		public void TestCleanup() {
			Thread.CurrentThread.CurrentCulture=_cultureInfoOld;
		}

		internal static PatFieldDef Def(long num,string name,PatFieldType type,int itemOrder=0,bool isHidden=false) {
			return new PatFieldDef() { PatFieldDefNum=num,FieldName=name,FieldType=type,ItemOrder=itemOrder,IsHidden=isHidden,PickList="" };
		}

		internal static PatFieldPickItem Item(long defNum,string name,int itemOrder,bool isHidden=false) {
			return new PatFieldPickItem() { PatFieldDefNum=defNum,Name=name,Abbreviation="",ItemOrder=itemOrder,IsHidden=isHidden };
		}

		internal static EFormField Field(EnumEFormFieldType type,string dbLink,string value="",string label="",string pickListDb="") {
			EFormField eFormField=new EFormField();
			eFormField.FieldType=type;
			eFormField.DbLink=dbLink;
			eFormField.ValueString=value;
			eFormField.ValueLabel=label;
			eFormField.PickListDb=pickListDb;
			eFormField.PickListVis=pickListDb;
			return eFormField;
		}

		private static List<PatFieldDef> Defs() {
			return new List<PatFieldDef>() {
				Def(1,"Hobby",PatFieldType.Text,3),
				Def(2,"Deposit",PatFieldType.Currency,1),
				Def(3,"First Visit",PatFieldType.Date,2),
				Def(4,"Morning Only",PatFieldType.Checkbox,4),
				Def(5,"Heard From",PatFieldType.PickList,5),
				Def(6,"Old Code",PatFieldType.Text,6,isHidden:true),
				Def(7,"CareCredit status",PatFieldType.CareCreditStatus,7)
			};
		}

		private static List<PatFieldPickItem> Items() {
			return new List<PatFieldPickItem>() { Item(5,"Friend",1),Item(5,"Web",0),Item(5,"Radio",2,isHidden:true) };
		}

		[TestMethod]
		public void Link_PrefixAndName() {
			Assert.IsTrue(EFormPatFields.IsPatFieldLink("PatField:Hobby"));
			Assert.IsFalse(EFormPatFields.IsPatFieldLink("PatField:"));
			Assert.IsFalse(EFormPatFields.IsPatFieldLink("Hobby"));
			Assert.IsFalse(EFormPatFields.IsPatFieldLink(""));
			Assert.IsFalse(EFormPatFields.IsPatFieldLink(null));
			Assert.AreEqual("First Visit",EFormPatFields.GetFieldName("PatField:First Visit"));
			Assert.AreEqual("",EFormPatFields.GetFieldName("allergy:Latex"));
			Assert.AreEqual("PatField:Hobby",EFormPatFields.ToDbLink("Hobby"));
			Assert.AreEqual("Hobby",EFormPatFields.GetDefaultLabel("PatField:Hobby"));
			Assert.AreEqual("LName",EFormPatFields.GetDefaultLabel("LName"));
		}

		[TestMethod]
		public void TypeMapping_FollowsTheSpec() {
			CollectionAssert.AreEquivalent(new[] { PatFieldType.Text,PatFieldType.Currency },EFormPatFields.GetCompatibleTypes(EnumEFormFieldType.TextField));
			CollectionAssert.AreEquivalent(new[] { PatFieldType.Date },EFormPatFields.GetCompatibleTypes(EnumEFormFieldType.DateField));
			CollectionAssert.AreEquivalent(new[] { PatFieldType.Checkbox },EFormPatFields.GetCompatibleTypes(EnumEFormFieldType.CheckBox));
			CollectionAssert.AreEquivalent(new[] { PatFieldType.PickList },EFormPatFields.GetCompatibleTypes(EnumEFormFieldType.RadioButtons));
			Assert.AreEqual(0,EFormPatFields.GetCompatibleTypes(EnumEFormFieldType.Label).Count);
			Assert.AreEqual(0,EFormPatFields.GetCompatibleTypes(EnumEFormFieldType.SigBox).Count);
		}

		[TestMethod]
		public void DbLinksAvailable_VisibleCompatibleDefsInSetupOrder() {
			CollectionAssert.AreEqual(new[] { "PatField:Deposit","PatField:Hobby" },
				EFormPatFields.GetDbLinksAvailable(EnumEFormFieldType.TextField,Defs()));
			CollectionAssert.AreEqual(new[] { "PatField:First Visit" },EFormPatFields.GetDbLinksAvailable(EnumEFormFieldType.DateField,Defs()));
			CollectionAssert.AreEqual(new[] { "PatField:Morning Only" },EFormPatFields.GetDbLinksAvailable(EnumEFormFieldType.CheckBox,Defs()));
			CollectionAssert.AreEqual(new[] { "PatField:Heard From" },EFormPatFields.GetDbLinksAvailable(EnumEFormFieldType.RadioButtons,Defs()));
			Assert.AreEqual(0,EFormPatFields.GetDbLinksAvailable(EnumEFormFieldType.MedicationList,Defs()).Count);
		}

		[TestMethod]
		public void DefForLink_MissingWhenDeletedHiddenOrOtherType() {
			Assert.AreEqual(1,EFormPatFields.GetDefForLink("PatField:Hobby",EnumEFormFieldType.TextField,Defs()).PatFieldDefNum);
			Assert.AreEqual(1,EFormPatFields.GetDefForLink("PatField:hobby",EnumEFormFieldType.TextField,Defs()).PatFieldDefNum,"names compare like MySQL");
			Assert.IsNull(EFormPatFields.GetDefForLink("PatField:Gone",EnumEFormFieldType.TextField,Defs()),"deleted");
			Assert.IsNull(EFormPatFields.GetDefForLink("PatField:Old Code",EnumEFormFieldType.TextField,Defs()),"hidden");
			Assert.IsNull(EFormPatFields.GetDefForLink("PatField:Hobby",EnumEFormFieldType.CheckBox,Defs()),"type changed");
			Assert.AreEqual("PatField:Gone (missing)",EFormPatFields.GetDisplayText("PatField:Gone",isMissing:true));
			Assert.AreEqual("PatField:Hobby",EFormPatFields.GetDisplayText("PatField:Hobby",isMissing:false));
		}

		[TestMethod]
		public void PickListNames_VisibleItemsInOrder() {
			CollectionAssert.AreEqual(new[] { "Web","Friend" },EFormPatFields.GetPickListNames("PatField:Heard From",Defs(),Items()));
			Assert.AreEqual(0,EFormPatFields.GetPickListNames("PatField:Hobby",Defs(),Items()).Count,"not a pick list");
		}

		[TestMethod]
		public void FillValue_PerType() {
			List<PatFieldDef> listDefs=Defs();
			Assert.AreEqual("kites",EFormPatFields.GetFillValue(Field(EnumEFormFieldType.TextField,"PatField:Hobby"),listDefs[0],
				new PatField() { FieldName="Hobby",FieldValue="kites" }));
			Assert.AreEqual("X",EFormPatFields.GetFillValue(Field(EnumEFormFieldType.CheckBox,"PatField:Morning Only"),listDefs[3],
				new PatField() { FieldName="Morning Only",FieldValue="1" }));
			EFormField eFormFieldRadio=Field(EnumEFormFieldType.RadioButtons,"PatField:Heard From",pickListDb:"Web|Friend");
			Assert.AreEqual("Friend",EFormPatFields.GetFillValue(eFormFieldRadio,listDefs[4],new PatField() { FieldName="Heard From",FieldValue="Friend" }));
			Assert.AreEqual("",EFormPatFields.GetFillValue(eFormFieldRadio,listDefs[4],new PatField() { FieldName="Heard From",FieldValue="Radio" }),
				"a value the radio buttons don't offer stays unanswered");
			Assert.AreEqual("",EFormPatFields.GetFillValue(Field(EnumEFormFieldType.TextField,"PatField:Hobby"),listDefs[0],null));
		}

		[TestMethod]
		public void FillFields_FillsLinkedFieldsAndBlanksMissingOnes() {
			EForm eForm=new EForm();
			eForm.ListEFormFields=new List<EFormField>() {
				Field(EnumEFormFieldType.TextField,"PatField:Deposit"),
				Field(EnumEFormFieldType.DateField,"PatField:First Visit"),
				Field(EnumEFormFieldType.CheckBox,"PatField:Morning Only"),
				Field(EnumEFormFieldType.TextField,"PatField:Gone","typed by prefill"),
				Field(EnumEFormFieldType.TextField,"FName","Ann")
			};
			List<PatField> listPatFields=new List<PatField>() {
				new PatField() { FieldName="Deposit",FieldValue="25.00" },
				new PatField() { FieldName="first visit",FieldValue="3/4/2021" },
				new PatField() { FieldName="Gone",FieldValue="old" }
			};
			EFormPatFields.FillFields(eForm,Defs(),listPatFields);
			Assert.AreEqual("25.00",eForm.ListEFormFields[0].ValueString);
			Assert.AreEqual("3/4/2021",eForm.ListEFormFields[1].ValueString);
			Assert.AreEqual("",eForm.ListEFormFields[2].ValueString,"no row means unchecked");
			Assert.AreEqual("",eForm.ListEFormFields[3].ValueString,"missing link");
			Assert.AreEqual("Ann",eForm.ListEFormFields[4].ValueString,"other links untouched");
		}

		[TestMethod]
		public void Validate_CurrencyNeedsANumber() {
			List<PatFieldDef> listDefs=Defs();
			Assert.AreEqual("",EFormPatFields.Validate(Field(EnumEFormFieldType.TextField,"PatField:Deposit","12.50"),listDefs));
			Assert.AreEqual("",EFormPatFields.Validate(Field(EnumEFormFieldType.TextField,"PatField:Deposit","$1,200"),listDefs));
			Assert.AreEqual("",EFormPatFields.Validate(Field(EnumEFormFieldType.TextField,"PatField:Deposit",""),listDefs));
			Assert.AreEqual("Deposit amount must be a number.",
				EFormPatFields.Validate(Field(EnumEFormFieldType.TextField,"PatField:Deposit","about ten","Deposit amount"),listDefs));
			Assert.AreEqual("Deposit must be a number.",EFormPatFields.Validate(Field(EnumEFormFieldType.TextField,"PatField:Deposit","x"),listDefs));
			Assert.AreEqual("",EFormPatFields.Validate(Field(EnumEFormFieldType.TextField,"PatField:Hobby","anything"),listDefs),"text is free");
			EFormField eFormFieldHidden=Field(EnumEFormFieldType.TextField,"PatField:Deposit","x");
			eFormFieldHidden.IsHiddenCondit=true;
			Assert.AreEqual("",EFormPatFields.Validate(eFormFieldHidden,listDefs),"hidden by conditional logic");
		}

		[TestMethod]
		public void ConvertAnswer_PerType() {
			List<PatFieldDef> listDefs=Defs();
			string error;
			Assert.AreEqual("kites",EFormPatFields.ConvertAnswer("  kites ",listDefs[0],Items(),"",out error));
			Assert.AreEqual("",error);
			//Currency: a number, kept as stored when it is the same amount
			Assert.AreEqual("12.50",EFormPatFields.ConvertAnswer("12.5",listDefs[1],Items(),"",out error));
			Assert.AreEqual("12.5",EFormPatFields.ConvertAnswer("12.50",listDefs[1],Items(),"12.5",out error));
			Assert.AreEqual("1200.00",EFormPatFields.ConvertAnswer("$1,200",listDefs[1],Items(),"",out error));
			Assert.AreEqual("7",EFormPatFields.ConvertAnswer("ten",listDefs[1],Items(),"7",out error));
			Assert.AreEqual("not a number",error);
			//Date
			Assert.AreEqual("3/4/2021",EFormPatFields.ConvertAnswer("2021-03-04",listDefs[2],Items(),"",out error));
			Assert.AreEqual("03/04/2021",EFormPatFields.ConvertAnswer("3/4/2021",listDefs[2],Items(),"03/04/2021",out error),"same date kept as stored");
			EFormPatFields.ConvertAnswer("someday",listDefs[2],Items(),"",out error);
			Assert.AreEqual("not a date",error);
			//Checkbox
			Assert.AreEqual("1",EFormPatFields.ConvertAnswer("X",listDefs[3],Items(),"",out error));
			Assert.AreEqual("",EFormPatFields.ConvertAnswer("",listDefs[3],Items(),"1",out error));
			//Pick list: one of the items, hidden ones too
			Assert.AreEqual("Friend",EFormPatFields.ConvertAnswer("friend",listDefs[4],Items(),"",out error));
			Assert.AreEqual("Radio",EFormPatFields.ConvertAnswer("Radio",listDefs[4],Items(),"",out error));
			EFormPatFields.ConvertAnswer("TV",listDefs[4],Items(),"",out error);
			Assert.AreEqual("not one of the pick list items",error);
		}

		[TestMethod]
		public void GetImports_ChangesClearingHiddenAndMissing() {
			EForm eForm=new EForm();
			EFormField eFormFieldParent=Field(EnumEFormFieldType.CheckBox,"","","Has deposit");
			EFormField eFormFieldDeposit=Field(EnumEFormFieldType.TextField,"PatField:Deposit","50");
			eFormFieldDeposit.ConditionalParent="Has deposit";
			eFormFieldDeposit.ConditionalValue="X";
			eForm.ListEFormFields=new List<EFormField>() {
				Field(EnumEFormFieldType.TextField,"PatField:Hobby","chess"),
				Field(EnumEFormFieldType.DateField,"PatField:First Visit",""),
				Field(EnumEFormFieldType.RadioButtons,"PatField:Heard From","",pickListDb:"Web|Friend"),
				Field(EnumEFormFieldType.CheckBox,"PatField:Morning Only","X"),
				Field(EnumEFormFieldType.TextField,"PatField:Gone","x"),
				eFormFieldParent,
				eFormFieldDeposit
			};
			List<PatField> listPatFields=new List<PatField>() {
				new PatField() { FieldName="Hobby",FieldValue="kites" },
				new PatField() { FieldName="First Visit",FieldValue="1/2/2020" },
				new PatField() { FieldName="Heard From",FieldValue="Web" }
			};
			List<EFormPatFieldImport> listImports=EFormPatFields.GetImports(eForm,Defs(),Items(),listPatFields);
			Assert.AreEqual(4,listImports.Count,"the unanswered radio buttons and the hidden deposit are left out");
			EFormPatFieldImport importHobby=listImports.Single(x => x.FieldName=="Hobby");
			Assert.IsTrue(importHobby.IsChange());
			Assert.IsFalse(importHobby.IsClearing());
			Assert.AreEqual("kites",importHobby.ValueOld);
			Assert.AreEqual("chess",importHobby.ValueNew);
			EFormPatFieldImport importDate=listImports.Single(x => x.FieldName=="First Visit");
			Assert.IsTrue(importDate.IsClearing(),"a blank answer would clear the stored date");
			EFormPatFieldImport importCheck=listImports.Single(x => x.FieldName=="Morning Only");
			Assert.AreEqual("1",importCheck.ValueNew);
			Assert.IsTrue(importCheck.IsChange());
			EFormPatFieldImport importMissing=listImports.Single(x => x.FieldName=="Gone");
			Assert.IsNull(importMissing.PatFieldDef);
			Assert.AreEqual("patient field missing",importMissing.Error);
			Assert.IsFalse(importMissing.IsChange());
		}

		[TestMethod]
		public void GetImports_TwoFieldsForOnePatientField_LastAnswerCounts() {
			EForm eForm=new EForm();
			eForm.ListEFormFields=new List<EFormField>() {
				Field(EnumEFormFieldType.TextField,"PatField:Hobby","chess"),
				Field(EnumEFormFieldType.TextField,"PatField:Hobby",""),
				Field(EnumEFormFieldType.TextField,"PatField:Deposit","5"),
				Field(EnumEFormFieldType.TextField,"PatField:Deposit","6")
			};
			List<EFormPatFieldImport> listImports=EFormPatFields.GetImports(eForm,Defs(),Items(),new List<PatField>());
			Assert.AreEqual("chess",listImports.Single(x => x.FieldName=="Hobby").ValueNew,"an empty later answer doesn't win");
			Assert.AreEqual("6.00",listImports.Single(x => x.FieldName=="Deposit").ValueNew);
		}

		[TestMethod]
		public void PatFieldLinks_AreNeverAutoImport() {
			Assert.IsFalse(EFormFieldsAvailable.IsAutoImportAllowed("PatField:Hobby"));
		}
	}
}
