// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Open Dental 25.4: eForm fields linked to the office's patient fields are reviewed and imported here, in their own "Patient Fields"
	///section. The logic is in OpenDentBusiness.EFormPatFields; this file only builds the rows and applies them.</summary>
	public partial class FormSheetImport {
		///<summary>Row FieldName prefix for patient field rows. It never matches a built-in field name.</summary>
		private const string PATFIELD_ROW_PREFIX="PatField:";
		///<summary>What each patient field row would import, by row FieldName.</summary>
		private Dictionary<string,EFormPatFieldImport> _dictPatFieldImports=new Dictionary<string,EFormPatFieldImport>();

		///<summary>Adds one row per linked patient field. A changed answer is checked for import, except an answer that would clear a stored value,
		///which the user must check to confirm. Missing patient fields (deleted, hidden or of another type) show in red and can't be imported.
		///Checkboxes show "X" for checked.</summary>
		private void FillRowsPatFields() {
			if(EFormCur==null) {
				return;
			}
			List<EFormPatFieldImport> listImports=EFormPatFields.GetImports(EFormCur);
			if(listImports.Count==0) {
				return;
			}
			_listImportRows.Add(CreateSeparator("Patient Fields"));
			for(int i=0;i<listImports.Count;i++) {
				EFormPatFieldImport eFormPatFieldImport=listImports[i];
				ImportRow importRow=new ImportRow();
				importRow.FieldName=PATFIELD_ROW_PREFIX+eFormPatFieldImport.FieldName;
				importRow.FieldDisplay=eFormPatFieldImport.FieldName;
				importRow.TypeObj=typeof(string);
				importRow.OldValDisplay=GetPatFieldDisplay(eFormPatFieldImport.ValueOld,eFormPatFieldImport.PatFieldDef);
				importRow.OldValObj=importRow.OldValDisplay;
				importRow.NewValDisplay=eFormPatFieldImport.Answer;
				importRow.NewValObj=importRow.NewValDisplay;
				if(eFormPatFieldImport.PatFieldDef==null) {
					importRow.ImpValDisplay=Lan.g(this,"patient field missing");
					importRow.ImpValObj=null;
					importRow.IsImportRed=true;
					importRow.DoImport=false;
				}
				else if(eFormPatFieldImport.Error!="") {
					importRow.ImpValDisplay=Lan.g(this,eFormPatFieldImport.Error);
					importRow.ImpValObj=null;
					importRow.IsImportRed=true;
					importRow.DoImport=false;
				}
				else {
					importRow.ImpValDisplay=GetPatFieldDisplay(eFormPatFieldImport.ValueNew,eFormPatFieldImport.PatFieldDef);
					importRow.ImpValObj=importRow.ImpValDisplay;
					importRow.DoImport=eFormPatFieldImport.IsChange() && !eFormPatFieldImport.IsClearing();
				}
				_dictPatFieldImports[importRow.FieldName]=eFormPatFieldImport;
				_listImportRows.Add(importRow);
			}
		}

		///<summary>A stored patfield value as the grid shows it: "X" for a checked checkbox field, otherwise the value.</summary>
		private static string GetPatFieldDisplay(string value,PatFieldDef patFieldDef) {
			if(patFieldDef!=null && patFieldDef.FieldType==PatFieldType.Checkbox) {
				return value=="1" ? "X" : "";
			}
			return value??"";
		}

		///<summary>Writes the patient field rows marked for import. The import value may have been edited in the grid, so it is converted again; a
		///value that can't be stored is skipped and listed. Each change gets a PatientFieldEdit Audit Trail entry.</summary>
		private void ImportPatFieldRows() {
			if(EFormCur==null || _dictPatFieldImports.Count==0) {
				return;
			}
			List<EFormPatFieldImport> listImports=new List<EFormPatFieldImport>();
			List<string> listErrors=new List<string>();
			List<PatFieldPickItem> listPatFieldPickItems=PatFieldPickItems.GetDeepCopy();
			List<ImportRow> listImportRows=_listImportRows.FindAll(x => x.DoImport && !x.IsSectionHeader && x.FieldName!=null
				&& _dictPatFieldImports.ContainsKey(x.FieldName));
			for(int i=0;i<listImportRows.Count;i++) {
				EFormPatFieldImport eFormPatFieldImport=_dictPatFieldImports[listImportRows[i].FieldName];
				if(eFormPatFieldImport.PatFieldDef==null) {
					listErrors.Add(eFormPatFieldImport.FieldName+": "+Lan.g(this,"patient field missing"));
					continue;
				}
				string valueNew=EFormPatFields.ConvertAnswer(listImportRows[i].ImpValDisplay,eFormPatFieldImport.PatFieldDef,listPatFieldPickItems,
					eFormPatFieldImport.ValueOld,out string error);
				if(error!="") {
					listErrors.Add(eFormPatFieldImport.FieldName+": "+error);
					continue;
				}
				eFormPatFieldImport.ValueNew=valueNew;
				eFormPatFieldImport.Error="";
				listImports.Add(eFormPatFieldImport);
			}
			EFormPatFields.Import(_patient.PatNum,listImports,"eForm #"+EFormCur.EFormNum+" ("+EFormCur.Description+")");
			if(listErrors.Count>0) {
				MsgBox.Show(Lan.g(this,"These patient fields were not imported:")+"\r\n"+string.Join("\r\n",listErrors));
			}
		}
	}
}
