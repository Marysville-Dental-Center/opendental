// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Open Dental 25.4: an eForm field's database link can point to one of the office's own patient fields (Setup, Patient Field Defs).
	///The form starts with the patient's stored value and the reviewed import (FormSheetImport) writes the answer back to the patfield row.
	///Storage format of the link (INFERRED, Open Dental's own format is not public): "PatField:" followed by patfielddef.FieldName, the same key the
	///patfield rows use. Type mapping: Text and Currency to a text box (Currency must be a number), Date to a date field, Checkbox to a check box,
	///PickList to radio buttons whose choices are the pick list items. A link whose definition was deleted, is hidden, or no longer has a compatible
	///type shows as "(missing)" in the designer and is skipped on fill and import. Patient fields are never Auto Import fields.</summary>
	public class EFormPatFields {
		///<summary>Prefix of a database link to a patient field.</summary>
		public const string PREFIX="PatField:";
		///<summary>Appended to the link in the designer when the definition is missing.</summary>
		public const string MISSING=" (missing)";

		#region Links
		///<summary>True if the database link points to a patient field.</summary>
		public static bool IsPatFieldLink(string dbLink) {
			return !string.IsNullOrEmpty(dbLink) && dbLink.StartsWith(PREFIX,StringComparison.Ordinal) && dbLink.Length>PREFIX.Length;
		}

		///<summary>The patfielddef.FieldName in a link, or empty string when the link is not a patient field link.</summary>
		public static string GetFieldName(string dbLink) {
			if(!IsPatFieldLink(dbLink)) {
				return "";
			}
			return dbLink.Substring(PREFIX.Length);
		}

		///<summary>The database link for a patient field name.</summary>
		public static string ToDbLink(string fieldName) {
			return PREFIX+fieldName;
		}

		///<summary>The patient field types that an eForm field type can hold. Empty for every other field type.</summary>
		public static List<PatFieldType> GetCompatibleTypes(EnumEFormFieldType eFormFieldType) {
			switch(eFormFieldType) {
				case EnumEFormFieldType.TextField:
					return new List<PatFieldType>() { PatFieldType.Text,PatFieldType.Currency };
				case EnumEFormFieldType.DateField:
					return new List<PatFieldType>() { PatFieldType.Date };
				case EnumEFormFieldType.CheckBox:
					return new List<PatFieldType>() { PatFieldType.Checkbox };
				case EnumEFormFieldType.RadioButtons:
					return new List<PatFieldType>() { PatFieldType.PickList };
				default:
					return new List<PatFieldType>();
			}
		}

		///<summary>The links offered in the designer for a field type: one per visible patient field definition of a compatible type, in the order of
		///Setup, Patient Field Defs.</summary>
		public static List<string> GetDbLinksAvailable(EnumEFormFieldType eFormFieldType,List<PatFieldDef> listPatFieldDefs) {
			List<PatFieldType> listPatFieldTypes=GetCompatibleTypes(eFormFieldType);
			return listPatFieldDefs.FindAll(x => !x.IsHidden && listPatFieldTypes.Contains(x.FieldType) && !string.IsNullOrEmpty(x.FieldName))
				.OrderBy(x => x.ItemOrder)
				.Select(x => ToDbLink(x.FieldName))
				.Distinct()
				.ToList();
		}

		///<summary>Same as the other overload, from the cache.</summary>
		public static List<string> GetDbLinksAvailable(EnumEFormFieldType eFormFieldType) {
			return GetDbLinksAvailable(eFormFieldType,PatFieldDefs.GetDeepCopy());
		}

		///<summary>The definition a link points to, or null when the link is missing: no definition has that name (deleted, or renamed by another
		///program), the definition is hidden, or its type no longer fits the field type. Names compare like the database does (ignoring case).</summary>
		public static PatFieldDef GetDefForLink(string dbLink,EnumEFormFieldType eFormFieldType,List<PatFieldDef> listPatFieldDefs) {
			if(!IsPatFieldLink(dbLink)) {
				return null;
			}
			string fieldName=GetFieldName(dbLink);
			PatFieldDef patFieldDef=listPatFieldDefs.FirstOrDefault(x => x.FieldName==fieldName)
				??listPatFieldDefs.FirstOrDefault(x => string.Equals(x.FieldName,fieldName,StringComparison.OrdinalIgnoreCase));
			if(patFieldDef==null || patFieldDef.IsHidden) {
				return null;
			}
			if(!GetCompatibleTypes(eFormFieldType).Contains(patFieldDef.FieldType)) {
				return null;
			}
			return patFieldDef;
		}

		///<summary>What the designer shows for a link: the link itself, with " (missing)" when its definition is missing.</summary>
		public static string GetDisplayText(string dbLink,bool isMissing) {
			if(isMissing) {
				return dbLink+MISSING;
			}
			return dbLink;
		}

		///<summary>The label a new field gets when the user picks a link: the patient field name for a patient field link, otherwise the link.</summary>
		public static string GetDefaultLabel(string dbLink) {
			if(IsPatFieldLink(dbLink)) {
				return GetFieldName(dbLink);
			}
			return dbLink;
		}

		///<summary>For radio buttons linked to a PickList patient field: the names of its visible pick list items, in order. These are both the stored
		///values and the default visible labels. Empty when the link is missing.</summary>
		public static List<string> GetPickListNames(string dbLink,List<PatFieldDef> listPatFieldDefs,List<PatFieldPickItem> listPatFieldPickItems) {
			PatFieldDef patFieldDef=GetDefForLink(dbLink,EnumEFormFieldType.RadioButtons,listPatFieldDefs);
			if(patFieldDef==null) {
				return new List<string>();
			}
			return listPatFieldPickItems.FindAll(x => x.PatFieldDefNum==patFieldDef.PatFieldDefNum && !x.IsHidden)
				.OrderBy(x => x.ItemOrder)
				.Select(x => x.Name)
				.ToList();
		}

		///<summary>Same as the other overload, from the cache.</summary>
		public static List<string> GetPickListNames(string dbLink) {
			return GetPickListNames(dbLink,PatFieldDefs.GetDeepCopy(),PatFieldPickItems.GetDeepCopy());
		}
		#endregion Links

		#region Fill
		///<summary>The value a linked eForm field starts with: the stored text, date or currency as it is; "X" for a check box whose patient field is
		///checked ("1"); the stored pick list item for radio buttons when it is one of their stored values. Empty when there is nothing to show.</summary>
		public static string GetFillValue(EFormField eFormField,PatFieldDef patFieldDef,PatField patField) {
			if(patFieldDef==null || patField==null || string.IsNullOrEmpty(patField.FieldValue)) {
				return "";
			}
			switch(patFieldDef.FieldType) {
				case PatFieldType.Checkbox:
					return patField.FieldValue=="1" ? "X" : "";
				case PatFieldType.PickList:
					List<string> listPickDb=(eFormField.PickListDb??"").Split('|').ToList();
					if(listPickDb.Contains(patField.FieldValue)) {
						return patField.FieldValue;
					}
					return "";
				default:
					return patField.FieldValue;
			}
		}

		///<summary>Fills every linked field of a new eForm from the patient's stored patient fields. Missing links stay empty.</summary>
		public static void FillFields(EForm eForm,List<PatFieldDef> listPatFieldDefs,List<PatField> listPatFields) {
			for(int i=0;i<eForm.ListEFormFields.Count;i++) {
				EFormField eFormField=eForm.ListEFormFields[i];
				if(!IsPatFieldLink(eFormField.DbLink)) {
					continue;
				}
				PatFieldDef patFieldDef=GetDefForLink(eFormField.DbLink,eFormField.FieldType,listPatFieldDefs);
				if(patFieldDef==null) {
					eFormField.ValueString="";
					continue;
				}
				PatField patField=GetPatField(listPatFields,patFieldDef.FieldName);
				eFormField.ValueString=GetFillValue(eFormField,patFieldDef,patField);
			}
		}

		///<summary>Same as the other overload, with the definitions from the cache and the patient's rows from the database. Does nothing when the form
		///has no patient field links.</summary>
		public static void FillFields(EForm eForm) {
			if(eForm.PatNum==0 || !eForm.ListEFormFields.Exists(x => IsPatFieldLink(x.DbLink))) {
				return;
			}
			FillFields(eForm,PatFieldDefs.GetDeepCopy(),PatFields.GetPatientData(eForm.PatNum));
		}

		///<summary>The patient's row for a field name. Null when there is none.</summary>
		public static PatField GetPatField(List<PatField> listPatFields,string fieldName) {
			return listPatFields.FirstOrDefault(x => x.FieldName==fieldName)
				??listPatFields.FirstOrDefault(x => string.Equals(x.FieldName,fieldName,StringComparison.OrdinalIgnoreCase));
		}
		#endregion Fill

		#region Validation and conversion
		///<summary>Checked when the form is saved: a text box linked to a Currency patient field must hold a number (or nothing). Returns the error,
		///or empty string. Fields hidden by conditional logic and missing links are not checked.</summary>
		public static string Validate(EFormField eFormField,List<PatFieldDef> listPatFieldDefs) {
			if(!IsPatFieldLink(eFormField.DbLink) || eFormField.IsHiddenCondit) {
				return "";
			}
			PatFieldDef patFieldDef=GetDefForLink(eFormField.DbLink,eFormField.FieldType,listPatFieldDefs);
			if(patFieldDef==null || patFieldDef.FieldType!=PatFieldType.Currency) {
				return "";
			}
			if(string.IsNullOrWhiteSpace(eFormField.ValueString) || TryParseCurrency(eFormField.ValueString,out decimal _)) {
				return "";
			}
			string label=eFormField.ValueLabel;
			if(string.IsNullOrWhiteSpace(label)) {
				label=patFieldDef.FieldName;
			}
			return label+" must be a number.";
		}

		///<summary>Same as the other overload, from the cache.</summary>
		public static string Validate(EFormField eFormField) {
			if(!IsPatFieldLink(eFormField.DbLink)) {
				return "";
			}
			return Validate(eFormField,PatFieldDefs.GetDeepCopy());
		}

		///<summary>A currency answer: a number in the current culture, with or without the currency symbol and group separators.</summary>
		public static bool TryParseCurrency(string text,out decimal value) {
			return decimal.TryParse((text??"").Trim(),NumberStyles.Currency,CultureInfo.CurrentCulture,out value);
		}

		///<summary>Turns an answer (as the eForm stores it, or as typed in the import window) into the value the patfield row stores. Empty result
		///means the row is removed. Error is set when the answer can't be stored: not a number, not a date, or not one of the pick list items.
		///Stored values that mean the same as the answer are kept as they are, so an unchanged answer is not a change:
		///<para>Text: the answer, trimmed.</para>
		///<para>Currency: a number; when the stored value is the same number it is kept, otherwise the answer is stored with two decimals.</para>
		///<para>Date: a date; when the stored value is the same date it is kept, otherwise the short date of the current culture.</para>
		///<para>Checkbox: "1" for a checked box ("X"), otherwise empty (patient fields store no row for an unchecked box).</para>
		///<para>PickList: one of the definition's pick list items (hidden items too, since a form may have been designed before an item was hidden).
		///</para></summary>
		public static string ConvertAnswer(string answer,PatFieldDef patFieldDef,List<PatFieldPickItem> listPatFieldPickItems,string valueOld,
			out string error)
		{
			error="";
			answer=(answer??"").Trim();
			valueOld=valueOld??"";
			switch(patFieldDef.FieldType) {
				case PatFieldType.Checkbox:
					if(answer=="X" || answer=="1") {
						return "1";
					}
					return "";
				case PatFieldType.Currency:
					if(answer=="") {
						return "";
					}
					if(!TryParseCurrency(answer,out decimal valueNew)) {
						error="not a number";
						return valueOld;
					}
					if(TryParseCurrency(valueOld,out decimal valueOldDecimal) && valueOldDecimal==valueNew) {
						return valueOld;
					}
					return valueNew.ToString("F2",CultureInfo.CurrentCulture);
				case PatFieldType.Date:
					if(answer=="") {
						return "";
					}
					if(!DateTime.TryParse(answer,CultureInfo.CurrentCulture,DateTimeStyles.None,out DateTime dateNew) || dateNew.Year<1880) {
						error="not a date";
						return valueOld;
					}
					if(DateTime.TryParse(valueOld,CultureInfo.CurrentCulture,DateTimeStyles.None,out DateTime dateOld) && dateOld.Date==dateNew.Date) {
						return valueOld;
					}
					return dateNew.ToShortDateString();
				case PatFieldType.PickList:
					if(answer=="") {
						return "";
					}
					List<PatFieldPickItem> listItems=listPatFieldPickItems.FindAll(x => x.PatFieldDefNum==patFieldDef.PatFieldDefNum);
					PatFieldPickItem patFieldPickItem=listItems.FirstOrDefault(x => x.Name==answer)
						??listItems.FirstOrDefault(x => string.Equals(x.Name,answer,StringComparison.OrdinalIgnoreCase));
					if(patFieldPickItem==null) {
						error="not one of the pick list items";
						return valueOld;
					}
					return patFieldPickItem.Name;
				default://Text
					return answer;
			}
		}
		#endregion Validation and conversion

		#region Import
		///<summary>What importing the linked fields of a filled eForm would change, one item per patient field. Fields hidden by conditional logic are
		///left out. Radio buttons with no choice made are left out (no answer means no change, as for other radio buttons). When two fields link to the
		///same patient field, the last one with an answer counts. A missing link gives an item with an error, so the import window can show it; such
		///items are never imported. Does not touch the database.</summary>
		public static List<EFormPatFieldImport> GetImports(EForm eForm,List<PatFieldDef> listPatFieldDefs,List<PatFieldPickItem> listPatFieldPickItems,
			List<PatField> listPatFields)
		{
			List<EFormPatFieldImport> listImports=new List<EFormPatFieldImport>();
			if(!eForm.ListEFormFields.Exists(x => IsPatFieldLink(x.DbLink))) {
				return listImports;
			}
			EFormFields.SetIsHiddenConditFlags(eForm.ListEFormFields);
			for(int i=0;i<eForm.ListEFormFields.Count;i++) {
				EFormField eFormField=eForm.ListEFormFields[i];
				if(!IsPatFieldLink(eFormField.DbLink) || eFormField.IsHiddenCondit) {
					continue;
				}
				EFormPatFieldImport eFormPatFieldImport=new EFormPatFieldImport();
				eFormPatFieldImport.EFormField=eFormField;
				eFormPatFieldImport.Answer=eFormField.ValueString??"";
				PatFieldDef patFieldDef=GetDefForLink(eFormField.DbLink,eFormField.FieldType,listPatFieldDefs);
				if(patFieldDef==null) {
					eFormPatFieldImport.FieldName=GetFieldName(eFormField.DbLink);
					eFormPatFieldImport.Error="patient field missing";
					AddOrReplace(listImports,eFormPatFieldImport);
					continue;
				}
				if(eFormField.FieldType==EnumEFormFieldType.RadioButtons && string.IsNullOrEmpty(eFormField.ValueString)) {
					continue;//no choice made
				}
				eFormPatFieldImport.PatFieldDef=patFieldDef;
				eFormPatFieldImport.FieldName=patFieldDef.FieldName;
				PatField patField=GetPatField(listPatFields,patFieldDef.FieldName);
				eFormPatFieldImport.ValueOld=patField?.FieldValue??"";
				eFormPatFieldImport.ValueNew=ConvertAnswer(eFormField.ValueString,patFieldDef,listPatFieldPickItems,eFormPatFieldImport.ValueOld,
					out string error);
				eFormPatFieldImport.Error=error;
				AddOrReplace(listImports,eFormPatFieldImport);
			}
			return listImports;
		}

		///<summary>Same as the other overload, with the definitions from the cache and the patient's rows from the database.</summary>
		public static List<EFormPatFieldImport> GetImports(EForm eForm) {
			if(eForm.PatNum==0 || !eForm.ListEFormFields.Exists(x => IsPatFieldLink(x.DbLink))) {
				return new List<EFormPatFieldImport>();
			}
			return GetImports(eForm,PatFieldDefs.GetDeepCopy(),PatFieldPickItems.GetDeepCopy(),PatFields.GetPatientData(eForm.PatNum));
		}

		///<summary>Keeps one item per patient field: a later field with an answer replaces an earlier one.</summary>
		private static void AddOrReplace(List<EFormPatFieldImport> listImports,EFormPatFieldImport eFormPatFieldImport) {
			int idx=listImports.FindIndex(x => string.Equals(x.FieldName,eFormPatFieldImport.FieldName,StringComparison.OrdinalIgnoreCase));
			if(idx==-1) {
				listImports.Add(eFormPatFieldImport);
				return;
			}
			if(eFormPatFieldImport.Answer.Trim()=="" && listImports[idx].Answer.Trim()!="") {
				return;//the earlier field has an answer and this one doesn't
			}
			listImports[idx]=eFormPatFieldImport;
		}

		///<summary>Writes the given values into the patient's patfield rows: inserts, updates, or removes the row when the value is empty. Each change
		///gets its own PatientFieldEdit audit entry naming the source (for example the eForm). Values equal to the stored one are skipped. Returns one
		///line per change. Items with an error or without a definition are skipped.</summary>
		public static List<string> Import(long patNum,List<EFormPatFieldImport> listImports,string source) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<string>>(MethodBase.GetCurrentMethod(),patNum,listImports,source);
			}
			List<string> listChanges=new List<string>();
			if(patNum==0) {
				return listChanges;
			}
			List<PatField> listPatFields=PatFields.GetPatientData(patNum);
			for(int i=0;i<listImports.Count;i++) {
				EFormPatFieldImport eFormPatFieldImport=listImports[i];
				if(eFormPatFieldImport.Error!="" || eFormPatFieldImport.PatFieldDef==null) {
					continue;
				}
				string fieldName=eFormPatFieldImport.PatFieldDef.FieldName;
				string valueNew=eFormPatFieldImport.ValueNew??"";
				PatField patField=GetPatField(listPatFields,fieldName);
				string valueOld=patField?.FieldValue??"";
				if(valueOld==valueNew) {
					continue;
				}
				if(valueNew=="") {
					List<PatField> listPatFieldsToDelete=listPatFields.FindAll(x => string.Equals(x.FieldName,fieldName,StringComparison.OrdinalIgnoreCase));
					for(int d=0;d<listPatFieldsToDelete.Count;d++) {
						PatFields.Delete(listPatFieldsToDelete[d]);
						listPatFields.Remove(listPatFieldsToDelete[d]);
					}
					string change="Deleted patient field "+fieldName+" from "+source+". Value before deletion: \""+valueOld+"\"";
					SecurityLogs.MakeLogEntry(EnumPermType.PatientFieldEdit,patNum,change);
					listChanges.Add(change);
					continue;
				}
				if(patField==null) {
					patField=new PatField();
					patField.PatNum=patNum;
					patField.FieldName=fieldName;
					patField.FieldValue=valueNew;
					PatFields.Insert(patField);
					listPatFields.Add(patField);
					string changeAdded="Added patient field "+fieldName+" from "+source+". Value: \""+valueNew+"\"";
					SecurityLogs.MakeLogEntry(EnumPermType.PatientFieldEdit,patNum,changeAdded);
					listChanges.Add(changeAdded);
					continue;
				}
				patField.FieldValue=valueNew;
				PatFields.Update(patField);
				string changeEdited="Edited patient field "+fieldName+" from "+source+"\r\nOld value: \""+valueOld+"\"  New value: \""+valueNew+"\"";
				SecurityLogs.MakeLogEntry(EnumPermType.PatientFieldEdit,patNum,changeEdited);
				listChanges.Add(changeEdited);
			}
			return listChanges;
		}
		#endregion Import

		#region Rename
		///<summary>Called when a patient field definition is renamed (PatFields.UpdateFieldName): every eForm field definition and filled eForm field that
		///links to the old name is changed to the new name, so links keep working. Returns the number of eForm field definitions changed.</summary>
		public static long UpdateDbLinksForRename(string fieldNameNew,string fieldNameOld) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod(),fieldNameNew,fieldNameOld);
			}
			if(string.IsNullOrEmpty(fieldNameOld) || string.IsNullOrEmpty(fieldNameNew) || fieldNameNew==fieldNameOld) {
				return 0;
			}
			string dbLinkNew=POut.String(ToDbLink(fieldNameNew));
			string dbLinkOld=POut.String(ToDbLink(fieldNameOld));
			string command="UPDATE eformfielddef SET DbLink='"+dbLinkNew+"' WHERE DbLink='"+dbLinkOld+"'";
			long countDefs=Db.NonQ(command);
			command="UPDATE eformfield SET DbLink='"+dbLinkNew+"' WHERE DbLink='"+dbLinkOld+"'";
			Db.NonQ(command);
			if(countDefs>0) {
				EFormFieldDefs.RefreshCache();
				Signalods.SetInvalid(InvalidType.Sheets);//eForm definitions are cached with the sheets
			}
			return countDefs;
		}
		#endregion Rename
	}

	///<summary>What importing one patient field from an eForm would do.</summary>
	[Serializable]
	public class EFormPatFieldImport {
		///<summary>The eForm field the answer comes from.</summary>
		public EFormField EFormField;
		///<summary>Null when the link is missing.</summary>
		public PatFieldDef PatFieldDef;
		///<summary>The patient field name.</summary>
		public string FieldName="";
		///<summary>The answer as the eForm stores it.</summary>
		public string Answer="";
		///<summary>The stored value. Empty when the patient has no row.</summary>
		public string ValueOld="";
		///<summary>The value to store. Empty removes the row.</summary>
		public string ValueNew="";
		///<summary>Why the answer can't be imported. Empty when it can.</summary>
		public string Error="";

		///<summary>True when importing would change the stored value.</summary>
		public bool IsChange() {
			return Error=="" && PatFieldDef!=null && (ValueOld??"")!=(ValueNew??"");
		}

		///<summary>True when importing would remove a stored value. The import window leaves these unchecked so the user confirms them.</summary>
		public bool IsClearing() {
			return IsChange() && (ValueNew??"")=="";
		}
	}
}
