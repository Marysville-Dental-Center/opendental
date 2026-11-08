// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Open Dental 26.2: eForm fields linked to vital signs are reviewed in their own "Vital Signs" section and imported as one new vital sign
	///entry (Medical window, Vital Signs tab). The logic is in OpenPmsDentalBusiness.EFormVitalSigns; this file only builds the rows and applies them.
	///</summary>
	public partial class FormSheetImport {
		///<summary>Row FieldName prefix for vital sign rows. It never matches a built-in field name.</summary>
		private const string VITAL_ROW_PREFIX="Vital:";
		///<summary>What each vital sign row would import, by row FieldName.</summary>
		private Dictionary<string,EFormVitalImport> _dictVitalImports=new Dictionary<string,EFormVitalImport>();
		///<summary>The date of the entry the import adds (EFormVitalSigns.GetDateTaken).</summary>
		private DateTime _dateVitalsTaken;

		///<summary>Adds one row per answered vital sign. Current Value shows the patient's latest entry. An answer that can be imported is checked; one
		///that can't shows why in red and stays unchecked until the office types a value that can (double click the Import Value).</summary>
		private void FillRowsVitalSigns() {
			if(EFormCur==null) {
				return;
			}
			List<EFormVitalImport> listImports=EFormVitalSigns.GetImports(EFormCur,Vitalsigns.GetMostRecent(_patient.PatNum));
			if(listImports.Count==0) {
				return;
			}
			_dateVitalsTaken=EFormVitalSigns.GetDateTaken(EFormCur,DateTime.Today);
			_listImportRows.Add(CreateSeparator(Lan.g(this,"Vital Signs, taken")+" "+_dateVitalsTaken.ToShortDateString()));
			for(int i=0;i<listImports.Count;i++) {
				EFormVitalImport eFormVitalImport=listImports[i];
				ImportRow importRow=new ImportRow();
				importRow.FieldName=VITAL_ROW_PREFIX+eFormVitalImport.DbLink;
				importRow.FieldDisplay=Lan.g(this,eFormVitalImport.FieldDisplay);
				importRow.TypeObj=typeof(string);
				importRow.OldValDisplay=eFormVitalImport.ValueOld;
				importRow.OldValObj=importRow.OldValDisplay;
				importRow.NewValDisplay=eFormVitalImport.Answer;
				importRow.NewValObj=importRow.NewValDisplay;
				if(eFormVitalImport.Error!="") {
					importRow.ImpValDisplay=Lan.g(this,eFormVitalImport.Error);
					importRow.ImpValObj=null;
					importRow.IsImportRed=true;
					importRow.DoImport=false;
				}
				else {
					importRow.ImpValDisplay=EFormVitalSigns.FormatValue(eFormVitalImport.Value);
					importRow.ImpValObj=importRow.ImpValDisplay;
					importRow.DoImport=true;//a new measurement, even when it equals the latest one
				}
				_dictVitalImports[importRow.FieldName]=eFormVitalImport;
				_listImportRows.Add(importRow);
			}
		}

		///<summary>Adds the vital sign entry from the rows marked for import. The import value may have been edited in the grid, so it is read again; a
		///value that can't be imported is skipped and listed. Nothing is added when the same entry is already on file.</summary>
		private void ImportVitalSignRows() {
			if(EFormCur==null || _dictVitalImports.Count==0) {
				return;
			}
			List<EFormVitalImport> listImports=new List<EFormVitalImport>();
			List<ImportRow> listImportRows=_listImportRows.FindAll(x => x.DoImport && !x.IsSectionHeader && x.FieldName!=null
				&& _dictVitalImports.ContainsKey(x.FieldName));
			for(int i=0;i<listImportRows.Count;i++) {
				EFormVitalImport eFormVitalImportSource=_dictVitalImports[listImportRows[i].FieldName];
				EFormVitalImport eFormVitalImport=new EFormVitalImport();
				eFormVitalImport.DbLink=eFormVitalImportSource.DbLink;
				eFormVitalImport.FieldDisplay=eFormVitalImportSource.FieldDisplay;
				eFormVitalImport.ValueOld=eFormVitalImportSource.ValueOld;
				eFormVitalImport.Answer=listImportRows[i].ImpValDisplay??"";
				if(EFormVitalSigns.TryParse(eFormVitalImport.DbLink,eFormVitalImport.Answer,out double value,out string error)) {
					eFormVitalImport.Value=value;
				}
				else {
					eFormVitalImport.Error=error=="" ? "no value" : error;
				}
				listImports.Add(eFormVitalImport);
			}
			EFormVitalSigns.CheckBloodPressure(listImports);
			List<string> listErrors=listImports.FindAll(x => x.Error!="").Select(x => Lan.g(this,x.FieldDisplay)+": "+Lan.g(this,x.Error)).ToList();
			EFormVitalImportResult eFormVitalImportResult=EFormVitalSigns.Import(_patient.PatNum,_dateVitalsTaken,listImports,
				"eForm #"+EFormCur.EFormNum+" ("+EFormCur.Description+")");
			if(eFormVitalImportResult.IsDuplicate) {
				MsgBox.Show(Lan.g(this,"Vital signs were not added:")+" "+eFormVitalImportResult.Description);
			}
			if(listErrors.Count>0) {
				MsgBox.Show(Lan.g(this,"These vital signs were not imported:")+"\r\n"+string.Join("\r\n",listErrors));
			}
		}
	}
}
