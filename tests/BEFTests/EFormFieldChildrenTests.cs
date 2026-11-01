// Logic tests for the conditional children view of eForm setup (Open Dental 25.1, package B-EF). No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using OpenDentBusiness;

namespace BEFTests {
	[TestClass]
	public class EFormFieldChildrenTests {
		private static EFormField Field(EnumEFormFieldType fieldType,string label,string condParent="",string condValue="") {
			EFormField eFormField=new EFormField();
			eFormField.FieldType=fieldType;
			eFormField.ValueLabel=label;
			eFormField.ConditionalParent=condParent;
			eFormField.ConditionalValue=condValue;
			return eFormField;
		}

		[TestMethod]
		public void GetChildren_MatchesConditionalParentToLabel_InFormOrder() {
			EFormField parent=Field(EnumEFormFieldType.CheckBox,"Do you smoke?");
			EFormField child1=Field(EnumEFormFieldType.TextField,"How many per day?","Do you smoke?","X");
			EFormField other=Field(EnumEFormFieldType.TextField,"Name");
			EFormField child2=Field(EnumEFormFieldType.DateField,"Since when?","Do you smoke?","X");
			EFormField childOfOther=Field(EnumEFormFieldType.TextField,"Spouse","Married?","Yes");
			List<EFormField> listFields=new List<EFormField> { parent,child1,other,child2,childOfOther };
			List<EFormField> listChildren=EFormFieldChildren.GetChildren(parent,listFields);
			CollectionAssert.AreEqual(new[] { child1,child2 },listChildren);
		}

		[TestMethod]
		public void GetChildren_SkipsSelfDeletedAndBlankLabels() {
			EFormField parent=Field(EnumEFormFieldType.CheckBox,"Allergic?");
			parent.ConditionalParent="Allergic?";//typed by hand: a field can't be its own child
			EFormField childDeleted=Field(EnumEFormFieldType.TextField,"Which?","Allergic?","X");
			childDeleted.IsDeleted=true;
			EFormField child=Field(EnumEFormFieldType.TextField,"Reaction","Allergic?","X");
			List<EFormField> listFields=new List<EFormField> { parent,childDeleted,child };
			CollectionAssert.AreEqual(new[] { child },EFormFieldChildren.GetChildren(parent,listFields));
			//A field with no label can't be a parent, even when other fields have a blank ConditionalParent.
			EFormField parentBlank=Field(EnumEFormFieldType.CheckBox,"");
			EFormField noParent=Field(EnumEFormFieldType.TextField,"Free text");
			Assert.AreEqual(0,EFormFieldChildren.GetChildren(parentBlank,new List<EFormField> { parentBlank,noParent }).Count);
			Assert.AreEqual(0,EFormFieldChildren.GetChildren((EFormField)null,listFields).Count);
		}

		[TestMethod]
		public void GetChildren_LongLabelsMatchOnFirst255Characters() {
			string labelLong=new string('a',300);
			EFormField parent=Field(EnumEFormFieldType.RadioButtons,labelLong);
			EFormField child=Field(EnumEFormFieldType.TextField,"Child",labelLong.Substring(0,255),"Yes");
			Assert.AreEqual(255,EFormFieldChildren.GetParentKey(labelLong).Length);
			CollectionAssert.AreEqual(new[] { child },EFormFieldChildren.GetChildren(parent,new List<EFormField> { parent,child }));
		}

		[TestMethod]
		public void GetChildren_TranslatedFormMatchesOnEnglishLabel() {
			//On a filled, translated form the label is translated and the English copy is kept (Open Dental 25.2). Children name the English label.
			EFormField parent=Field(EnumEFormFieldType.CheckBox,"Fuma usted?");
			parent.ValueLabelEnglish="Do you smoke?";
			EFormField child=Field(EnumEFormFieldType.TextField,"Cuantos?","Do you smoke?","X");
			List<EFormField> listFields=new List<EFormField> { parent,child };
			CollectionAssert.AreEqual(new[] { child },EFormFieldChildren.GetChildren(EFormFields.GetLabelEnglish(parent),listFields,parent));
			Assert.AreEqual(0,EFormFieldChildren.GetChildren(parent.ValueLabel,listFields,parent).Count);
		}

		[TestMethod]
		public void RenameParent_PointsChildrenAtTheNewLabel() {
			EFormField parent=Field(EnumEFormFieldType.RadioButtons,"Insured?");
			EFormField child1=Field(EnumEFormFieldType.TextField,"Carrier","Insured?","Yes");
			EFormField child2=Field(EnumEFormFieldType.TextField,"Subscriber ID","Insured?","Yes");
			EFormField pageBreak=Field(EnumEFormFieldType.PageBreak,"","Insured?","Yes");
			EFormField unrelated=Field(EnumEFormFieldType.TextField,"Spouse","Married?","Yes");
			List<EFormField> listFields=new List<EFormField> { parent,child1,child2,pageBreak,unrelated };
			Assert.AreEqual(3,EFormFieldChildren.CountChildrenToRename(listFields,parent,"Insured?","Do you have dental insurance?"));
			int countChanged=EFormFieldChildren.RenameParent(listFields,parent,"Insured?","Do you have dental insurance?");
			Assert.AreEqual(3,countChanged);
			Assert.AreEqual("Do you have dental insurance?",child1.ConditionalParent);
			Assert.AreEqual("Do you have dental insurance?",child2.ConditionalParent);
			Assert.AreEqual("Do you have dental insurance?",pageBreak.ConditionalParent);
			Assert.AreEqual("Married?",unrelated.ConditionalParent);
			Assert.AreEqual("Yes",child1.ConditionalValue,"The trigger value is not touched.");
		}

		[TestMethod]
		public void RenameParent_DoesNothingForSameOrBlankLabels() {
			EFormField parent=Field(EnumEFormFieldType.CheckBox,"Pregnant?");
			EFormField child=Field(EnumEFormFieldType.TextField,"Due date","Pregnant?","X");
			List<EFormField> listFields=new List<EFormField> { parent,child };
			Assert.AreEqual(0,EFormFieldChildren.CountChildrenToRename(listFields,parent,"Pregnant?","Pregnant?"));
			Assert.AreEqual(0,EFormFieldChildren.RenameParent(listFields,parent,"Pregnant?","Pregnant?"));
			Assert.AreEqual(0,EFormFieldChildren.RenameParent(listFields,parent,"Pregnant?",""));
			Assert.AreEqual(0,EFormFieldChildren.RenameParent(listFields,parent,"","Pregnant now?"));
			Assert.AreEqual("Pregnant?",child.ConditionalParent);
			//Labels that differ only after character 255 are the same parent.
			string label300=new string('b',300);
			string label301=label300+"c";
			EFormField parentLong=Field(EnumEFormFieldType.CheckBox,label300);
			EFormField childLong=Field(EnumEFormFieldType.TextField,"Child",label300.Substring(0,255),"X");
			Assert.AreEqual(0,EFormFieldChildren.RenameParent(new List<EFormField> { parentLong,childLong },parentLong,label300,label301));
		}

		[TestMethod]
		public void CanBeParent_CheckBoxRadioDateAndInsPick() {
			Assert.IsTrue(EFormFieldChildren.CanBeParent(Field(EnumEFormFieldType.CheckBox,"a")));
			Assert.IsTrue(EFormFieldChildren.CanBeParent(Field(EnumEFormFieldType.RadioButtons,"a")));
			Assert.IsTrue(EFormFieldChildren.CanBeParent(Field(EnumEFormFieldType.DateField,"a")));
			Assert.IsTrue(EFormFieldChildren.CanBeParent(Field(EnumEFormFieldType.InsPick,"a")));
			Assert.IsFalse(EFormFieldChildren.CanBeParent(Field(EnumEFormFieldType.TextField,"a")));
			Assert.IsFalse(EFormFieldChildren.CanBeParent(Field(EnumEFormFieldType.Label,"a")));
			Assert.IsFalse(EFormFieldChildren.CanBeParent(null));
		}

		[TestMethod]
		public void GetTriggerText_ShowsTheParentValueThatShowsTheChild() {
			EFormField checkBox=Field(EnumEFormFieldType.CheckBox,"Smoker");
			Assert.AreEqual("Checked",EFormFieldChildren.GetTriggerText(checkBox,Field(EnumEFormFieldType.TextField,"x","Smoker","X")));
			Assert.AreEqual("Unchecked",EFormFieldChildren.GetTriggerText(checkBox,Field(EnumEFormFieldType.TextField,"x","Smoker","")));
			EFormField radio=Field(EnumEFormFieldType.RadioButtons,"Relationship");
			Assert.AreEqual("Child, Spouse",EFormFieldChildren.GetTriggerText(radio,Field(EnumEFormFieldType.TextField,"x","Relationship","Child|Spouse")));
			EFormField date=Field(EnumEFormFieldType.DateField,"Birthdate");
			Assert.AreEqual("age <18",EFormFieldChildren.GetTriggerText(date,Field(EnumEFormFieldType.TextField,"x","Birthdate","<18")));
			EFormField insPick=Field(EnumEFormFieldType.InsPick,"Primary insurance");
			Assert.AreEqual("New",EFormFieldChildren.GetTriggerText(insPick,Field(EnumEFormFieldType.TextField,"x","Primary insurance","New")));
			Assert.AreEqual("raw",EFormFieldChildren.GetTriggerText(null,Field(EnumEFormFieldType.TextField,"x","Gone","raw")));
		}

		[TestMethod]
		public void GetDisplayLabel_ReadableTextForEveryFieldType() {
			EFormField label=Field(EnumEFormFieldType.Label,"<FlowDocument><Paragraph><Run FontWeight=\"Bold\">Medical</Run> <Run>history</Run></Paragraph>"
				+"<Paragraph><Run>Please answer &amp; sign</Run></Paragraph></FlowDocument>");
			Assert.AreEqual("Medical history Please answer & sign",EFormFieldChildren.GetDisplayLabel(label));
			EFormMedListLayout eFormMedListLayout=new EFormMedListLayout();
			eFormMedListLayout.Title="Current Medications";
			EFormField medList=Field(EnumEFormFieldType.MedicationList,JsonConvert.SerializeObject(eFormMedListLayout));
			Assert.AreEqual("Current Medications",EFormFieldChildren.GetDisplayLabel(medList));
			Assert.AreEqual("Medication List",EFormFieldChildren.GetDisplayLabel(Field(EnumEFormFieldType.MedicationList,"not json")));
			Assert.AreEqual("(page break)",EFormFieldChildren.GetDisplayLabel(Field(EnumEFormFieldType.PageBreak,"")));
			Assert.AreEqual("(no label)",EFormFieldChildren.GetDisplayLabel(Field(EnumEFormFieldType.TextField,"  ")));
			Assert.AreEqual("Phone",EFormFieldChildren.GetDisplayLabel(Field(EnumEFormFieldType.TextField," Phone\r\n")));
			string display=EFormFieldChildren.GetDisplayLabel(Field(EnumEFormFieldType.TextField,new string('z',200)));
			Assert.AreEqual(EFormFieldChildren.MAX_LENGTH_DISPLAY,display.Length);
			Assert.IsTrue(display.EndsWith("..."));
		}

		[TestMethod]
		public void GetPlainTextFromFlowDocument_HandlesBadXmlAndPlainText() {
			Assert.AreEqual("",EFormFieldChildren.GetPlainTextFromFlowDocument(""));
			Assert.AreEqual("plain words",EFormFieldChildren.GetPlainTextFromFlowDocument("plain words"));
			string text=EFormFieldChildren.GetPlainTextFromFlowDocument("<FlowDocument><Paragraph><Run>broken</Paragraph>");
			Assert.IsTrue(text.Contains("broken"));
			Assert.IsFalse(text.Contains("<"));
			Assert.AreEqual("line one line two",EFormFieldChildren.GetPlainTextFromFlowDocument(
				"<FlowDocument><Paragraph><Run>line one</Run><LineBreak /><Run>line two</Run></Paragraph></FlowDocument>"));
		}

		[TestMethod]
		public void GetTypeText_UsesDescriptions() {
			Assert.AreEqual("Text Field",EFormFieldChildren.GetTypeText(EnumEFormFieldType.TextField));
			Assert.AreEqual("Check Box",EFormFieldChildren.GetTypeText(EnumEFormFieldType.CheckBox));
			Assert.AreEqual("Radio Button Group",EFormFieldChildren.GetTypeText(EnumEFormFieldType.RadioButtons));
			Assert.AreEqual("Page Break",EFormFieldChildren.GetTypeText(EnumEFormFieldType.PageBreak));
			Assert.AreEqual("Label",EFormFieldChildren.GetTypeText(EnumEFormFieldType.Label));
		}

		[TestMethod]
		public void Children_AreTheFieldsTheFillLogicHides() {
			//The list must agree with the fill logic: every child it lists is hidden while the parent is unchecked, and shown when checked.
			EFormField parent=Field(EnumEFormFieldType.CheckBox,"Any allergies?");
			EFormField child=Field(EnumEFormFieldType.TextField,"List them","Any allergies?","X");
			EFormField other=Field(EnumEFormFieldType.TextField,"Name");
			List<EFormField> listFields=new List<EFormField> { parent,child,other };
			parent.ValueString="";
			EFormFields.SetIsHiddenConditFlags(listFields);
			List<EFormField> listChildren=EFormFieldChildren.GetChildren(parent,listFields);
			Assert.IsTrue(listChildren.All(x => x.IsHiddenCondit));
			Assert.IsFalse(other.IsHiddenCondit);
			parent.ValueString="X";
			EFormFields.SetIsHiddenConditFlags(listFields);
			Assert.IsTrue(listChildren.All(x => !x.IsHiddenCondit));
		}
	}
}
