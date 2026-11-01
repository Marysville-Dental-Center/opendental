// Conditional children of an eForm field in eForm setup (Open Dental 25.1, backlog package B-EF).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using CodeBase;
using Newtonsoft.Json;

namespace OpenPmsDentalBusiness {
	///<summary>The conditional children of an eForm field: every field whose ConditionalParent names this field's label, so it shows only when this
	///field has the child's ConditionalValue. The checkbox and radio button editors in eForm setup use this to list a parent's children, open them
	///and keep them attached when the parent's label is renamed (Open Dental 25.1). Pure logic on the designer's list of fields; nothing here reads
	///or writes the database.</summary>
	public class EFormFieldChildren {
		///<summary>ConditionalParent holds at most this many characters. A longer parent label is matched on its first 255 characters.</summary>
		public const int MAX_LENGTH_PARENT=255;
		///<summary>Longest label text shown in the children list. Longer text is cut and ends with "...".</summary>
		public const int MAX_LENGTH_DISPLAY=80;

		///<summary>True for the field types that conditional logic treats as parents (EFormFields.SetIsHiddenConditFlags): checkboxes, radio button
		///groups, date fields (age rules) and Ins Pick fields.</summary>
		public static bool CanBeParent(EFormField eFormField) {
			Meth.NoCheckMiddleTierRole();
			if(eFormField==null) {
				return false;
			}
			return eFormField.FieldType.In(EnumEFormFieldType.CheckBox,EnumEFormFieldType.RadioButtons,EnumEFormFieldType.DateField,
				EnumEFormFieldType.InsPick);
		}

		///<summary>The text a child's ConditionalParent holds for a parent with this label: the label cut to 255 characters. Empty for a blank label,
		///which can't be a parent.</summary>
		public static string GetParentKey(string labelParent) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(labelParent)) {
				return "";
			}
			return labelParent.Substring(0,Math.Min(labelParent.Length,MAX_LENGTH_PARENT));
		}

		///<summary>The fields in listEFormFields whose ConditionalParent names labelParent, in form order. The parent itself is never its own child.
		///In eForm setup a field's ValueLabel is always the English text, because translations are kept in languagepat, so setup matches on
		///ValueLabel. A filled, translated form keeps the English text in ValueLabelEnglish (Open Dental 25.2); pass EFormFields.GetLabelEnglish
		///there. Fields marked IsDeleted are skipped.</summary>
		public static List<EFormField> GetChildren(string labelParent,List<EFormField> listEFormFields,EFormField eFormFieldParent=null) {
			Meth.NoCheckMiddleTierRole();
			List<EFormField> listEFormFieldsChildren=new List<EFormField>();
			string key=GetParentKey(labelParent);
			if(key=="" || listEFormFields==null) {
				return listEFormFieldsChildren;
			}
			for(int i=0;i<listEFormFields.Count;i++) {
				EFormField eFormField=listEFormFields[i];
				if(eFormField==null || eFormField==eFormFieldParent || eFormField.IsDeleted) {
					continue;
				}
				if(eFormField.ConditionalParent=="" || eFormField.ConditionalParent!=key) {
					continue;
				}
				listEFormFieldsChildren.Add(eFormField);
			}
			return listEFormFieldsChildren;
		}

		///<summary>The children of a field in eForm setup, matched on its current ValueLabel.</summary>
		public static List<EFormField> GetChildren(EFormField eFormFieldParent,List<EFormField> listEFormFields) {
			Meth.NoCheckMiddleTierRole();
			if(eFormFieldParent==null) {
				return new List<EFormField>();
			}
			return GetChildren(eFormFieldParent.ValueLabel,listEFormFields,eFormFieldParent);
		}

		///<summary>When a parent's label changes from labelOld to labelNew, points every child that named labelOld at labelNew instead. Returns how
		///many children changed. Does nothing when either label is blank or they are the same after cutting to 255 characters.</summary>
		public static int RenameParent(List<EFormField> listEFormFields,EFormField eFormFieldParent,string labelOld,string labelNew) {
			Meth.NoCheckMiddleTierRole();
			string keyOld=GetParentKey(labelOld);
			string keyNew=GetParentKey(labelNew);
			if(keyOld=="" || keyNew=="" || keyOld==keyNew) {
				return 0;
			}
			List<EFormField> listEFormFieldsChildren=GetChildren(labelOld,listEFormFields,eFormFieldParent);
			for(int i=0;i<listEFormFieldsChildren.Count;i++) {
				listEFormFieldsChildren[i].ConditionalParent=keyNew;
			}
			return listEFormFieldsChildren.Count;
		}

		///<summary>How many children would follow a rename from labelOld to labelNew: zero when the labels match (after cutting to 255 characters)
		///or labelOld has no children. Used to decide whether to ask the user.</summary>
		public static int CountChildrenToRename(List<EFormField> listEFormFields,EFormField eFormFieldParent,string labelOld,string labelNew) {
			Meth.NoCheckMiddleTierRole();
			string keyOld=GetParentKey(labelOld);
			string keyNew=GetParentKey(labelNew);
			if(keyOld=="" || keyNew=="" || keyOld==keyNew) {
				return 0;
			}
			return GetChildren(labelOld,listEFormFields,eFormFieldParent).Count;
		}

		///<summary>A short, readable name for a field in the children list. Rich text labels are reduced to their plain text, a medication list shows
		///its title, and a field with no label says so.</summary>
		public static string GetDisplayLabel(EFormField eFormField) {
			Meth.NoCheckMiddleTierRole();
			if(eFormField==null) {
				return "";
			}
			string text;
			switch(eFormField.FieldType) {
				case EnumEFormFieldType.Label:
					text=GetPlainTextFromFlowDocument(eFormField.ValueLabel);
					break;
				case EnumEFormFieldType.MedicationList:
					text=GetMedicationListTitle(eFormField.ValueLabel);
					break;
				case EnumEFormFieldType.PageBreak:
					text="(page break)";
					break;
				default:
					text=eFormField.ValueLabel??"";
					break;
			}
			text=Regex.Replace(text,@"\s+"," ").Trim();
			if(text=="") {
				return "(no label)";
			}
			if(text.Length>MAX_LENGTH_DISPLAY) {
				text=text.Substring(0,MAX_LENGTH_DISPLAY-3)+"...";
			}
			return text;
		}

		///<summary>The field type as shown in the children list, from the enum's Description (Text Field and Label have none).</summary>
		public static string GetTypeText(EnumEFormFieldType enumEFormFieldType) {
			Meth.NoCheckMiddleTierRole();
			if(enumEFormFieldType==EnumEFormFieldType.TextField) {
				return "Text Field";
			}
			return enumEFormFieldType.GetDescription();
		}

		///<summary>The value(s) of the parent that make the child show, as shown in the children list. A checkbox parent shows Checked or Unchecked,
		///radio buttons and Ins Pick list their values, and a date parent shows its age rule, like "age &lt;18".</summary>
		public static string GetTriggerText(EFormField eFormFieldParent,EFormField eFormFieldChild) {
			Meth.NoCheckMiddleTierRole();
			if(eFormFieldChild==null) {
				return "";
			}
			string condValue=eFormFieldChild.ConditionalValue??"";
			if(eFormFieldParent==null) {
				return condValue;
			}
			switch(eFormFieldParent.FieldType) {
				case EnumEFormFieldType.CheckBox:
					if(condValue=="") {
						return "Unchecked";
					}
					if(condValue=="X") {
						return "Checked";
					}
					return condValue;
				case EnumEFormFieldType.DateField:
					if(condValue=="") {
						return "";
					}
					return "age "+condValue;
				case EnumEFormFieldType.RadioButtons:
				case EnumEFormFieldType.InsPick:
					return string.Join(", ",condValue.Split('|').Where(x => x!=""));
				default:
					return condValue;
			}
		}

		///<summary>The plain text of a rich text label, which is stored as a WPF FlowDocument in XML. Paragraphs and line breaks become spaces. Text
		///that isn't valid XML is returned with anything that looks like a tag removed.</summary>
		public static string GetPlainTextFromFlowDocument(string xml) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(xml)) {
				return "";
			}
			if(!xml.TrimStart().StartsWith("<")) {
				return xml;
			}
			try {
				XmlDocument xmlDocument=new XmlDocument();
				//A space between two runs is text in a FlowDocument, so keep whitespace. Runs of white space are collapsed by the caller.
				xmlDocument.PreserveWhitespace=true;
				xmlDocument.LoadXml(xml);
				StringBuilder stringBuilder=new StringBuilder();
				AppendText(xmlDocument.DocumentElement,stringBuilder);
				return stringBuilder.ToString().Trim();
			}
			catch(XmlException) {
				return Regex.Replace(xml,"<[^>]*>"," ").Trim();
			}
		}

		private static void AppendText(XmlNode xmlNode,StringBuilder stringBuilder) {
			if(xmlNode==null) {
				return;
			}
			if(xmlNode.NodeType.In(XmlNodeType.Text,XmlNodeType.CDATA,XmlNodeType.Whitespace,XmlNodeType.SignificantWhitespace)) {
				stringBuilder.Append(xmlNode.Value);
				return;
			}
			if(xmlNode.LocalName=="LineBreak") {
				stringBuilder.Append(' ');
			}
			for(int i=0;i<xmlNode.ChildNodes.Count;i++) {
				AppendText(xmlNode.ChildNodes[i],stringBuilder);
			}
			if(xmlNode.LocalName.In("Paragraph","ListItem","TableCell","Section")) {
				stringBuilder.Append(' ');
			}
		}

		///<summary>The Title of a medication list field, whose ValueLabel holds an EFormMedListLayout as JSON.</summary>
		private static string GetMedicationListTitle(string json) {
			if(string.IsNullOrEmpty(json)) {
				return "Medication List";
			}
			try {
				EFormMedListLayout eFormMedListLayout=JsonConvert.DeserializeObject<EFormMedListLayout>(json);
				if(eFormMedListLayout!=null && !string.IsNullOrWhiteSpace(eFormMedListLayout.Title)) {
					return eFormMedListLayout.Title;
				}
			}
			catch(Exception) {
				//Not JSON: fall through to the default title.
			}
			return "Medication List";
		}
	}
}
