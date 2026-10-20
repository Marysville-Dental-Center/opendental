using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Writes eForm answers into the patient record. The office reviews and imports most answers in FormSheetImport. Fields marked Auto Import
	///(Open Dental 25.1, eformfield.AutoImport) are written here, without review, by the save that first stores the filled form.</summary>
	public class EFormImport {
		//Any changes to the DbLinks handled here should also be duplicated in EFormFiller and EFormFieldsAvailable.

		///<summary>Call before the save. Auto import runs once, on the save that first inserts the form, and only for a form tied to a patient. A form
		///already in the database (saved earlier on the desktop, or filled and handled by Open Dental) is never imported again by opening and saving
		///it, whatever its status: Open Dental leaves desktop-filled forms at None, so the status cannot tell whether a form was imported.</summary>
		public static bool ShouldAutoImportOnSave(EForm eForm) {
			return eForm.IsNew && eForm.PatNum!=0;
		}

		///<summary>True if this field's answer is written into the database without review: it is marked Auto Import, its link supports it, it is not
		///hidden by conditional logic, and it has an answer. Text, date and radio-button fields with no answer are skipped, so an empty answer never
		///wipes stored data. Checkboxes are imported as they are. Call EFormFields.SetIsHiddenConditFlags first.</summary>
		public static bool IsAutoImportField(EFormField eFormField) {
			if(!eFormField.AutoImport || eFormField.IsHiddenCondit) {
				return false;
			}
			if(!EFormFieldsAvailable.IsAutoImportAllowed(eFormField.DbLink)) {
				return false;
			}
			if(!eFormField.FieldType.In(EnumEFormFieldType.TextField,EnumEFormFieldType.DateField,EnumEFormFieldType.RadioButtons,EnumEFormFieldType.CheckBox)) {
				return false;
			}
			if(eFormField.FieldType!=EnumEFormFieldType.CheckBox && string.IsNullOrWhiteSpace(eFormField.ValueString)) {
				return false;
			}
			return true;
		}

		///<summary>The fields of the form that auto import would write. Sets the conditional-logic flags on the form's fields.</summary>
		public static List<EFormField> GetAutoImportFields(EForm eForm) {
			EFormFields.SetIsHiddenConditFlags(eForm.ListEFormFields);
			return eForm.ListEFormFields.FindAll(x => IsAutoImportField(x));
		}

		///<summary>Copies the patient and patient-note answers of these fields onto the objects passed in. Fields with other links are ignored. Returns
		///one line per value that changed, for the audit trail. Values that can't be read (an unknown gender, an invalid date) are skipped. Does not
		///touch the database.</summary>
		public static List<string> ApplyPatientFields(List<EFormField> listEFormFields,Patient patient,PatientNote patientNote) {
			List<string> listChanges=new List<string>();
			for(int i=0;i<listEFormFields.Count;i++) {
				string value=listEFormFields[i].ValueString??"";
				string dbLink=listEFormFields[i].DbLink;
				switch(dbLink) {
					case "Address":
						SetString(listChanges,dbLink,patient.Address,value,x => patient.Address=x);
						break;
					case "Address2":
						SetString(listChanges,dbLink,patient.Address2,value,x => patient.Address2=x);
						break;
					case "Birthdate":
						DateTime dateBirth=PIn.Date(value);
						if(dateBirth.Year<1880) {
							break;//not a usable date
						}
						if(patient.Birthdate.Date!=dateBirth.Date) {
							listChanges.Add(dbLink+" changed from '"+patient.Birthdate.ToShortDateString()+"' to '"+dateBirth.ToShortDateString()+"'");
							patient.Birthdate=dateBirth.Date;
						}
						break;
					case "City":
						SetString(listChanges,dbLink,patient.City,value,x => patient.City=x);
						break;
					case "Email":
						SetString(listChanges,dbLink,patient.Email,value,x => patient.Email=x);
						break;
					case "FName":
						SetString(listChanges,dbLink,patient.FName,value,x => patient.FName=x);
						break;
					case "Gender":
						if(Enum.TryParse(value,out PatientGender patientGender) && Enum.IsDefined(typeof(PatientGender),patientGender)) {
							SetEnum(listChanges,dbLink,patient.Gender,patientGender,x => patient.Gender=x);
						}
						break;
					case "HmPhone":
						SetString(listChanges,dbLink,patient.HmPhone,value,x => patient.HmPhone=x);
						break;
					case "ICEName":
						SetString(listChanges,dbLink,patientNote.ICEName,value,x => patientNote.ICEName=x);
						break;
					case "ICEPhone":
						SetString(listChanges,dbLink,patientNote.ICEPhone,value,x => patientNote.ICEPhone=x);
						break;
					case "LName":
						SetString(listChanges,dbLink,patient.LName,value,x => patient.LName=x);
						break;
					case "MiddleI":
						SetString(listChanges,dbLink,patient.MiddleI,value,x => patient.MiddleI=x);
						break;
					case "Position":
						if(Enum.TryParse(value,out PatientPosition patientPosition) && Enum.IsDefined(typeof(PatientPosition),patientPosition)) {
							SetEnum(listChanges,dbLink,patient.Position,patientPosition,x => patient.Position=x);
						}
						break;
					case "PreferConfirmMethod":
						if(TryParseContactMethod(value,out ContactMethod contactMethodConfirm)) {
							SetEnum(listChanges,dbLink,patient.PreferConfirmMethod,contactMethodConfirm,x => patient.PreferConfirmMethod=x);
						}
						break;
					case "PreferContactMethod":
						if(TryParseContactMethod(value,out ContactMethod contactMethodContact)) {
							SetEnum(listChanges,dbLink,patient.PreferContactMethod,contactMethodContact,x => patient.PreferContactMethod=x);
						}
						break;
					case "PreferRecallMethod":
						if(TryParseContactMethod(value,out ContactMethod contactMethodRecall)) {
							SetEnum(listChanges,dbLink,patient.PreferRecallMethod,contactMethodRecall,x => patient.PreferRecallMethod=x);
						}
						break;
					case "Preferred":
						SetString(listChanges,dbLink,patient.Preferred,value,x => patient.Preferred=x);
						break;
					case "SSN":
						string ssn=value;
						if(CultureInfo.CurrentCulture.Name=="en-US") {
							ssn=ssn.Replace("-","");//stored without dashes
						}
						if(patient.SSN!=ssn) {
							listChanges.Add("SSN changed");//the number itself is not written to the audit trail
							patient.SSN=ssn;
						}
						break;
					case "State":
						SetString(listChanges,dbLink,patient.State,value,x => patient.State=x);
						break;
					case "StudentStatus":
						if(value.In("N","P","F")) {
							SetString(listChanges,dbLink,patient.StudentStatus,value,x => patient.StudentStatus=x);
						}
						break;
					case "WirelessPhone":
						SetString(listChanges,dbLink,patient.WirelessPhone,value,x => patient.WirelessPhone=x);
						break;
					case "WkPhone":
						SetString(listChanges,dbLink,patient.WkPhone,value,x => patient.WkPhone=x);
						break;
					case "Zip":
						SetString(listChanges,dbLink,patient.Zip,value,x => patient.Zip=x);
						break;
				}
			}
			return listChanges;
		}

		private static void SetString(List<string> listChanges,string fieldName,string valueOld,string valueNew,Action<string> actionSet) {
			if((valueOld??"")==valueNew) {
				return;
			}
			listChanges.Add(fieldName+" changed from '"+valueOld+"' to '"+valueNew+"'");
			actionSet(valueNew);
		}

		private static void SetEnum<T>(List<string> listChanges,string fieldName,T valueOld,T valueNew,Action<T> actionSet) where T:struct {
			if(valueOld.Equals(valueNew)) {
				return;
			}
			listChanges.Add(fieldName+" changed from '"+valueOld+"' to '"+valueNew+"'");
			actionSet(valueNew);
		}

		private static bool TryParseContactMethod(string value,out ContactMethod contactMethod) {
			return Enum.TryParse(value,out contactMethod) && Enum.IsDefined(typeof(ContactMethod),contactMethod);
		}

		///<summary>Writes the Auto Import fields of a form into the patient record, logs each change, and adds one EFormImport audit entry for the form.
		///Returns the changes made (empty if there was nothing to import). Status is not changed: since Open Dental 25.2 an eForm's status does not
		///record imports, the audit trail does.</summary>
		public static List<string> ImportAutoFields(EForm eForm) {
			List<string> listChangesAll=new List<string>();
			List<EFormField> listEFormFields=GetAutoImportFields(eForm);
			if(listEFormFields.Count==0) {
				return listChangesAll;
			}
			Patient patientOld=Patients.GetPat(eForm.PatNum);
			if(patientOld==null) {
				return listChangesAll;
			}
			string strSource="eForm #"+eForm.EFormNum+" ("+eForm.Description+")";
			Patient patient=patientOld.Copy();
			PatientNote patientNote=PatientNotes.Refresh(patient.PatNum,patient.Guarantor);
			List<string> listChangesPatient=ApplyPatientFields(listEFormFields,patient,patientNote);
			if(listChangesPatient.Count>0) {
				Patients.Update(patient,patientOld);
				PatientNotes.Update(patientNote,patient.Guarantor);
				SecurityLogs.MakeLogEntry(EnumPermType.PatientEdit,patient.PatNum,"Auto import from "+strSource+":\r\n"+string.Join("\r\n",listChangesPatient));
				listChangesAll.AddRange(listChangesPatient);
			}
			List<string> listChangesAllergies=ImportAllergies(listEFormFields,eForm.ListEFormFields,patient.PatNum);
			if(listChangesAllergies.Count>0) {
				SecurityLogs.MakeLogEntry(EnumPermType.PatAllergyListEdit,patient.PatNum,"Auto import from "+strSource+":\r\n"+string.Join("\r\n",listChangesAllergies));
				listChangesAll.AddRange(listChangesAllergies);
			}
			List<string> listChangesProblems=ImportProblems(listEFormFields,eForm.ListEFormFields,patient.PatNum);
			if(listChangesProblems.Count>0) {
				SecurityLogs.MakeLogEntry(EnumPermType.PatProblemListEdit,patient.PatNum,"Auto import from "+strSource+":\r\n"+string.Join("\r\n",listChangesProblems));
				listChangesAll.AddRange(listChangesProblems);
			}
			EFormField eFormFieldReferral=listEFormFields.Find(x => x.DbLink=="referredFrom");
			if(eFormFieldReferral!=null) {
				string changeReferral=ImportReferredFrom(patient.PatNum,eFormFieldReferral.ValueString);
				if(changeReferral!="") {
					SecurityLogs.MakeLogEntry(EnumPermType.RefAttachAdd,patient.PatNum,changeReferral+" (auto import from "+strSource+")");
					listChangesAll.Add(changeReferral);
				}
			}
			if(listChangesAll.Count>0) {
				SecurityLogs.MakeLogEntry(EnumPermType.EFormImport,patient.PatNum,"Auto import from "+strSource+": "+listChangesAll.Count+" change(s).");
			}
			return listChangesAll;
		}

		///<summary>allergiesNone, allergiesOther and allergy: fields. listEFormFieldsAll is every field of the form; typed-in allergies already covered
		///by an allergy: field are skipped.</summary>
		private static List<string> ImportAllergies(List<EFormField> listEFormFields,List<EFormField> listEFormFieldsAll,long patNum) {
			List<string> listChanges=new List<string>();
			if(!listEFormFields.Exists(x => x.DbLink.In("allergiesNone","allergiesOther") || x.DbLink.StartsWith("allergy:"))) {
				return listChanges;
			}
			List<Allergy> listAllergiesPat=Allergies.GetAll(patNum,showInactive:false);//just active allergies
			List<AllergyDef> listAllergyDefs=AllergyDefs.GetAll(includeHidden:false);//AllergyDefs are not cached
			for(int i=0;i<listEFormFields.Count;i++) {
				EFormField eFormField=listEFormFields[i];
				if(eFormField.DbLink=="allergiesNone") {
					if(eFormField.ValueString!="X") {
						continue;
					}
					for(int a=0;a<listAllergiesPat.Count;a++) {
						listAllergiesPat[a].StatusIsActive=false;
						Allergies.Update(listAllergiesPat[a]);
						listChanges.Add("Allergy set inactive: "+GetAllergyName(listAllergiesPat[a],listAllergyDefs));
					}
					listAllergiesPat=Allergies.GetAll(patNum,showInactive:false);
					continue;
				}
				if(eFormField.DbLink=="allergiesOther") {
					List<string> listStrsInput=eFormField.ValueString.Split(new char[]{','},StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).Where(x=>x!="").ToList();
					List<string> listStrAllergiesChecks=listEFormFieldsAll.FindAll(x=>x.DbLink.StartsWith("allergy:")).Select(x=>x.DbLink.Substring(8)).ToList();
					for(int a=0;a<listStrsInput.Count;a++) {
						if(listStrAllergiesChecks.Contains(listStrsInput[a])) {
							continue;//covered by a checkbox or radio button
						}
						AllergyDef allergyDef=listAllergyDefs.Find(x=>x.Description.ToLower()==listStrsInput[a].ToLower());
						if(allergyDef==null) {
							//No matching definition: the catch-all "Other" definition, with the typed text in Reaction.
							AllergyDef allergyDefOther=listAllergyDefs.Find(x=>x.Description=="Other");
							if(allergyDefOther==null) {
								allergyDefOther=new AllergyDef();
								allergyDefOther.Description="Other";
								AllergyDefs.Insert(allergyDefOther);
								listAllergyDefs=AllergyDefs.GetAll(includeHidden:false);
							}
							if(listAllergiesPat.Exists(x=>x.AllergyDefNum==allergyDefOther.AllergyDefNum && x.Reaction==listStrsInput[a])) {
								continue;
							}
							Allergy allergyOther=new Allergy();
							allergyOther.PatNum=patNum;
							allergyOther.AllergyDefNum=allergyDefOther.AllergyDefNum;
							allergyOther.Reaction=listStrsInput[a];
							allergyOther.StatusIsActive=true;
							Allergies.Insert(allergyOther);
							listChanges.Add("Allergy added: Other ("+listStrsInput[a]+")");
							listAllergiesPat=Allergies.GetAll(patNum,showInactive:false);
							continue;
						}
						if(listAllergiesPat.Exists(x=>x.AllergyDefNum==allergyDef.AllergyDefNum)) {
							continue;
						}
						Allergy allergyMatch=new Allergy();
						allergyMatch.PatNum=patNum;
						allergyMatch.AllergyDefNum=allergyDef.AllergyDefNum;
						allergyMatch.StatusIsActive=true;
						Allergies.Insert(allergyMatch);
						listChanges.Add("Allergy added: "+allergyDef.Description);
						listAllergiesPat=Allergies.GetAll(patNum,showInactive:false);
					}
					continue;
				}
				if(eFormField.DbLink.StartsWith("allergy:")) {
					bool? hasAllergy=GetYesNo(eFormField);
					if(hasAllergy==null) {
						continue;//no answer means no change
					}
					string strAllergy=eFormField.DbLink.Substring(8);
					AllergyDef allergyDef=listAllergyDefs.Find(x=>x.Description.ToLower()==strAllergy.ToLower());
					Allergy allergyPat=null;
					if(allergyDef!=null) {
						allergyPat=listAllergiesPat.Find(x=>x.AllergyDefNum==allergyDef.AllergyDefNum);
					}
					if(hasAllergy.Value && allergyPat==null) {
						if(allergyDef==null) {
							//The definition was renamed or never existed in this database. Add it so the answer is kept.
							allergyDef=new AllergyDef();
							allergyDef.Description=strAllergy;
							AllergyDefs.Insert(allergyDef);
							listAllergyDefs=AllergyDefs.GetAll(includeHidden:false);
						}
						allergyPat=new Allergy();
						allergyPat.PatNum=patNum;
						allergyPat.AllergyDefNum=allergyDef.AllergyDefNum;
						allergyPat.StatusIsActive=true;
						Allergies.Insert(allergyPat);
						listChanges.Add("Allergy added: "+allergyDef.Description);
						listAllergiesPat=Allergies.GetAll(patNum,showInactive:false);
					}
					else if(!hasAllergy.Value && allergyPat!=null) {
						allergyPat.StatusIsActive=false;
						Allergies.Update(allergyPat);
						listChanges.Add("Allergy set inactive: "+allergyDef.Description);
						listAllergiesPat=Allergies.GetAll(patNum,showInactive:false);
					}
				}
			}
			return listChanges;
		}

		private static string GetAllergyName(Allergy allergy,List<AllergyDef> listAllergyDefs) {
			AllergyDef allergyDef=listAllergyDefs.Find(x => x.AllergyDefNum==allergy.AllergyDefNum);
			if(allergyDef==null) {
				return "#"+allergy.AllergyDefNum;
			}
			if(allergyDef.Description=="Other" && !string.IsNullOrEmpty(allergy.Reaction)) {
				return "Other ("+allergy.Reaction+")";
			}
			return allergyDef.Description;
		}

		///<summary>For allergy: and problem: fields. True for a checked box or a "Y" radio button, false for an unchecked box or "N", null for a
		///radio group with no answer.</summary>
		public static bool? GetYesNo(EFormField eFormField) {
			if(eFormField.ValueString=="X" || eFormField.ValueString=="Y") {
				return true;
			}
			if(eFormField.ValueString=="N") {
				return false;
			}
			if(eFormField.FieldType==EnumEFormFieldType.CheckBox && string.IsNullOrEmpty(eFormField.ValueString)) {
				return false;
			}
			return null;
		}

		///<summary>problemsNone, problemsOther and problem: fields. Same rules as allergies.</summary>
		private static List<string> ImportProblems(List<EFormField> listEFormFields,List<EFormField> listEFormFieldsAll,long patNum) {
			List<string> listChanges=new List<string>();
			if(!listEFormFields.Exists(x => x.DbLink.In("problemsNone","problemsOther") || x.DbLink.StartsWith("problem:"))) {
				return listChanges;
			}
			List<Disease> listDiseasesPat=Diseases.Refresh(patNum,showActiveOnly:true);
			for(int i=0;i<listEFormFields.Count;i++) {
				EFormField eFormField=listEFormFields[i];
				if(eFormField.DbLink=="problemsNone") {
					if(eFormField.ValueString!="X") {
						continue;
					}
					for(int a=0;a<listDiseasesPat.Count;a++) {
						listDiseasesPat[a].ProbStatus=ProblemStatus.Inactive;
						listDiseasesPat[a].DateStop=DateTime.Today.AddDays(-1);//yesterday
						Diseases.Update(listDiseasesPat[a]);
						listChanges.Add("Problem set inactive: "+DiseaseDefs.GetName(listDiseasesPat[a].DiseaseDefNum));
					}
					listDiseasesPat=Diseases.Refresh(patNum,showActiveOnly:true);
					continue;
				}
				if(eFormField.DbLink=="problemsOther") {
					List<string> listStrsInput=eFormField.ValueString.Split(new char[]{','},StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).Where(x=>x!="").ToList();
					List<string> listStrProbsChecks=listEFormFieldsAll.FindAll(x=>x.DbLink.StartsWith("problem:")).Select(x=>x.DbLink.Substring(8)).ToList();
					for(int a=0;a<listStrsInput.Count;a++) {
						if(listStrProbsChecks.Contains(listStrsInput[a])) {
							continue;
						}
						DiseaseDef diseaseDef=DiseaseDefs.GetFirstOrDefault(x=>x.DiseaseName.ToLower()==listStrsInput[a].ToLower());
						if(diseaseDef==null) {
							DiseaseDef diseaseDefOther=GetOrInsertDiseaseDef("Other");
							if(listDiseasesPat.Exists(x=>x.DiseaseDefNum==diseaseDefOther.DiseaseDefNum && x.PatNote==listStrsInput[a])) {
								continue;
							}
							Disease diseaseOther=new Disease();
							diseaseOther.PatNum=patNum;
							diseaseOther.DiseaseDefNum=diseaseDefOther.DiseaseDefNum;
							diseaseOther.PatNote=listStrsInput[a];
							diseaseOther.ProbStatus=ProblemStatus.Active;
							Diseases.Insert(diseaseOther);
							listChanges.Add("Problem added: Other ("+listStrsInput[a]+")");
							listDiseasesPat=Diseases.Refresh(patNum,showActiveOnly:true);
							continue;
						}
						if(listDiseasesPat.Exists(x=>x.DiseaseDefNum==diseaseDef.DiseaseDefNum)) {
							continue;
						}
						Disease diseaseMatch=new Disease();
						diseaseMatch.PatNum=patNum;
						diseaseMatch.DiseaseDefNum=diseaseDef.DiseaseDefNum;
						diseaseMatch.ProbStatus=ProblemStatus.Active;
						Diseases.Insert(diseaseMatch);
						listChanges.Add("Problem added: "+diseaseDef.DiseaseName);
						listDiseasesPat=Diseases.Refresh(patNum,showActiveOnly:true);
					}
					continue;
				}
				if(eFormField.DbLink.StartsWith("problem:")) {
					bool? hasProblem=GetYesNo(eFormField);
					if(hasProblem==null) {
						continue;
					}
					string strProblem=eFormField.DbLink.Substring(8);
					DiseaseDef diseaseDef=DiseaseDefs.GetFirstOrDefault(x=>x.DiseaseName.ToLower()==strProblem.ToLower());
					Disease diseasePat=null;
					if(diseaseDef!=null) {
						diseasePat=listDiseasesPat.Find(x=>x.DiseaseDefNum==diseaseDef.DiseaseDefNum);
					}
					if(hasProblem.Value && diseasePat==null) {
						if(diseaseDef==null) {
							diseaseDef=GetOrInsertDiseaseDef(strProblem);
						}
						diseasePat=new Disease();
						diseasePat.PatNum=patNum;
						diseasePat.DiseaseDefNum=diseaseDef.DiseaseDefNum;
						diseasePat.ProbStatus=ProblemStatus.Active;
						Diseases.Insert(diseasePat);
						listChanges.Add("Problem added: "+diseaseDef.DiseaseName);
						listDiseasesPat=Diseases.Refresh(patNum,showActiveOnly:true);
					}
					else if(!hasProblem.Value && diseasePat!=null) {
						diseasePat.ProbStatus=ProblemStatus.Inactive;
						diseasePat.DateStop=DateTime.Today.AddDays(-1);//yesterday
						Diseases.Update(diseasePat);
						listChanges.Add("Problem set inactive: "+diseaseDef.DiseaseName);
						listDiseasesPat=Diseases.Refresh(patNum,showActiveOnly:true);
					}
				}
			}
			return listChanges;
		}

		private static DiseaseDef GetOrInsertDiseaseDef(string diseaseName) {
			DiseaseDef diseaseDef=DiseaseDefs.GetFirstOrDefault(x=>x.DiseaseName==diseaseName);
			if(diseaseDef!=null) {
				return diseaseDef;
			}
			diseaseDef=new DiseaseDef();
			diseaseDef.DiseaseName=diseaseName;
			DiseaseDefs.Insert(diseaseDef);
			Signalods.SetInvalid(InvalidType.Diseases);
			DiseaseDefs.RefreshCache();
			return diseaseDef;
		}

		///<summary>The referral named in a referredFrom answer is attached as "referred from" only when the patient has no referral source yet and
		///exactly one visible referral has that name (first and last, ignoring case). Otherwise nothing changes and the office picks the referral
		///in the import review, as before. Returns a line for the audit trail, or empty string.</summary>
		private static string ImportReferredFrom(long patNum,string strReferral) {
			if(string.IsNullOrWhiteSpace(strReferral)) {
				return "";
			}
			if(Referrals.GetReferralForPat(patNum)!=null) {
				return "";//the manual import doesn't change an existing referral source either
			}
			string strName=strReferral.Trim();
			List<Referral> listReferrals=Referrals.GetWhere(x => !x.IsHidden && string.Equals(x.GetNameFL().Trim(),strName,StringComparison.OrdinalIgnoreCase));
			if(listReferrals.Count!=1) {
				return "";
			}
			RefAttach refAttach=new RefAttach();
			refAttach.RefType=ReferralType.RefFrom;
			refAttach.ItemOrder=1;
			refAttach.PatNum=patNum;
			refAttach.RefDate=DateTime.Today;
			refAttach.ReferralNum=listReferrals[0].ReferralNum;
			RefAttaches.Insert(refAttach);
			return "Referred From "+listReferrals[0].GetNameFL();
		}

		///<summary>The master-list medication that a typed eForm medication name links to on import: an exact name match (ignoring case) among the
		///medications that are not hidden (Open Dental 25.1, medication.IsHidden). Null if there is none, in which case the import stores the typed name
		///on the patient's medication without a link.</summary>
		public static Medication FindMedicationForImport(string medName) {
			if(string.IsNullOrWhiteSpace(medName)) {
				return null;
			}
			string medNameLower=medName.Trim().ToLower();
			return Medications.GetFirstOrDefault(x => !x.IsHidden && x.MedName.ToLower()==medNameLower);
		}
	}
}
