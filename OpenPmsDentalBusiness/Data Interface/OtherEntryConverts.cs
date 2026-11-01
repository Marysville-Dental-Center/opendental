// Converting the catch-all "Other" allergies and problems that eForm import creates into listed ones (Open Dental 25.2, package B-MED).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>When an imported eForm lists an allergy or a problem that matches no definition, the import attaches the patient to a catch-all
	///definition named "Other" and keeps the typed text: in allergy.Reaction for allergies, in disease.PatNote for problems (EFormImport).  These
	///methods turn one such entry into a real allergy or problem of the definition the user picks.  The windows (FormAllergyEdit, FrmDiseaseEdit and
	///the Medical window) run the pick and the questions; the rules and the database work are here.  Converting one patient's entry never touches
	///other patients who typed the same text.</summary>
	public class OtherEntryConverts {
		///<summary>The name EFormImport gives the catch-all allergy and problem definitions.</summary>
		public const string OTHER_NAME="Other";

		///<summary>True when the definition name is the catch-all "Other", ignoring case and surrounding spaces.</summary>
		public static bool IsOtherName(string name) {
			Meth.NoCheckMiddleTierRole();
			return string.Equals((name??"").Trim(),OTHER_NAME,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>The text shown above the Convert button, like "Patient entered: peanuts".</summary>
		public static string GetPatientEnteredText(string textEntered) {
			Meth.NoCheckMiddleTierRole();
			return Lans.g("OtherEntryConverts","Patient entered:")+" "+(textEntered??"");
		}

		#region Allergies
		///<summary>True when the allergy definition is the catch-all "Other".</summary>
		public static bool IsOtherAllergyDef(AllergyDef allergyDef) {
			Meth.NoCheckMiddleTierRole();
			return allergyDef!=null && IsOtherName(allergyDef.Description);
		}

		///<summary>True when the saved allergy points to the catch-all "Other" definition.  Reads the definition from the database, because
		///allergydef is not cached.  False for a new allergy that has not been saved.</summary>
		public static bool IsOtherAllergy(Allergy allergy) {
			Meth.NoCheckMiddleTierRole();
			if(allergy==null || allergy.AllergyNum==0 || allergy.AllergyDefNum==0) {
				return false;
			}
			return IsOtherAllergyDef(AllergyDefs.GetOne(allergy.AllergyDefNum));
		}

		///<summary>The patient's other active allergy with this definition, or null.  Never returns the allergy being converted.</summary>
		public static Allergy GetActiveAllergyWithDef(long patNum,long allergyDefNum,long allergyNumConverting) {
			Meth.NoCheckMiddleTierRole();
			return Allergies.GetAll(patNum,showInactive:false).FirstOrDefault(x => x.AllergyDefNum==allergyDefNum && x.AllergyNum!=allergyNumConverting);
		}

		///<summary>An empty string when the allergy can be converted to the definition; otherwise why not.</summary>
		public static string GetAllergyConvertError(Allergy allergy,AllergyDef allergyDefNew) {
			Meth.NoCheckMiddleTierRole();
			if(allergy==null || allergy.AllergyNum==0) {
				return Lans.g("OtherEntryConverts","Only a saved allergy can be converted.");
			}
			if(allergyDefNew==null || allergyDefNew.AllergyDefNum==0) {
				return Lans.g("OtherEntryConverts","Pick an allergy from the list.");
			}
			if(IsOtherAllergyDef(allergyDefNew)) {
				return Lans.g("OtherEntryConverts","Pick an allergy other than Other.");
			}
			return "";
		}

		///<summary>Sets the fields of a conversion without saving.  The allergy points to the new definition.  Its Reaction, which holds what the
		///patient typed, is cleared unless keepTextAsReaction is true.  When isMerge is true the allergy is set inactive, because the patient already
		///has an active allergy with that definition and that one is kept.</summary>
		public static void ApplyAllergyConvert(Allergy allergy,long allergyDefNumNew,bool keepTextAsReaction,bool isMerge) {
			Meth.NoCheckMiddleTierRole();
			allergy.AllergyDefNum=allergyDefNumNew;
			if(!keepTextAsReaction) {
				allergy.Reaction="";
			}
			if(isMerge) {
				allergy.StatusIsActive=false;
			}
		}

		///<summary>The audit text: Allergy 'Other: (text)' converted to '(definition)'.</summary>
		public static string GetAllergyConvertLogText(string textEntered,string allergyDefDescription,bool isMerge) {
			Meth.NoCheckMiddleTierRole();
			string logText="Allergy 'Other: "+(textEntered??"")+"' converted to '"+(allergyDefDescription??"")+"'";
			if(isMerge) {
				logText+=", merged with the patient's active allergy of that name (the converted entry was set inactive)";
			}
			return logText+".";
		}

		///<summary>Converts an "Other" allergy to the definition the user picked, saves it and writes a PatAllergyListEdit audit entry.  textEntered is
		///what the patient typed (for the audit text).  Throws an ApplicationException when GetAllergyConvertError finds a problem.  Returns the audit
		///text.  The caller checks the PatAllergyListEdit permission first.</summary>
		public static string ConvertAllergy(Allergy allergy,string textEntered,AllergyDef allergyDefNew,bool keepTextAsReaction,bool isMerge) {
			Meth.NoCheckMiddleTierRole();
			string error=GetAllergyConvertError(allergy,allergyDefNew);
			if(error!="") {
				throw new ApplicationException(error);
			}
			ApplyAllergyConvert(allergy,allergyDefNew.AllergyDefNum,keepTextAsReaction,isMerge);
			Allergies.Update(allergy);
			string logText=GetAllergyConvertLogText(textEntered,allergyDefNew.Description,isMerge);
			SecurityLogs.MakeLogEntry(EnumPermType.PatAllergyListEdit,allergy.PatNum,logText);
			return logText;
		}
		#endregion Allergies

		#region Problems
		///<summary>True when the problem definition is the catch-all "Other".</summary>
		public static bool IsOtherDiseaseDef(DiseaseDef diseaseDef) {
			Meth.NoCheckMiddleTierRole();
			return diseaseDef!=null && IsOtherName(diseaseDef.DiseaseName);
		}

		///<summary>True when the saved problem points to the catch-all "Other" definition.  Uses the problem definition cache.  False for a new
		///problem that has not been saved.</summary>
		public static bool IsOtherProblem(Disease disease) {
			Meth.NoCheckMiddleTierRole();
			if(disease==null || disease.DiseaseNum==0 || disease.DiseaseDefNum==0) {
				return false;
			}
			return IsOtherDiseaseDef(DiseaseDefs.GetItem(disease.DiseaseDefNum));
		}

		///<summary>The patient's other active problem with this definition, or null.  Never returns the problem being converted.</summary>
		public static Disease GetActiveProblemWithDef(long patNum,long diseaseDefNum,long diseaseNumConverting) {
			Meth.NoCheckMiddleTierRole();
			return Diseases.GetDiseasesForPatient(patNum,diseaseDefNum,showActiveOnly:true).FirstOrDefault(x => x.DiseaseNum!=diseaseNumConverting);
		}

		///<summary>An empty string when the problem can be converted to the definition; otherwise why not.</summary>
		public static string GetProblemConvertError(Disease disease,DiseaseDef diseaseDefNew) {
			Meth.NoCheckMiddleTierRole();
			if(disease==null || disease.DiseaseNum==0) {
				return Lans.g("OtherEntryConverts","Only a saved problem can be converted.");
			}
			if(diseaseDefNew==null || diseaseDefNew.DiseaseDefNum==0) {
				return Lans.g("OtherEntryConverts","Pick a problem from the list.");
			}
			if(IsOtherDiseaseDef(diseaseDefNew)) {
				return Lans.g("OtherEntryConverts","Pick a problem other than Other.");
			}
			return "";
		}

		///<summary>Sets the fields of a conversion without saving.  The problem points to the new definition and keeps its status, start date and
		///stop date.  Its PatNote, which holds what the patient typed, is cleared unless keepTextAsNote is true.  When isMerge is true the problem is
		///set Inactive (dates unchanged), because the patient already has an active problem with that definition and that one is kept.</summary>
		public static void ApplyProblemConvert(Disease disease,long diseaseDefNumNew,bool keepTextAsNote,bool isMerge) {
			Meth.NoCheckMiddleTierRole();
			disease.DiseaseDefNum=diseaseDefNumNew;
			if(!keepTextAsNote) {
				disease.PatNote="";
			}
			if(isMerge) {
				disease.ProbStatus=ProblemStatus.Inactive;
			}
		}

		///<summary>The audit text: Problem 'Other: (text)' converted to '(definition)'.</summary>
		public static string GetProblemConvertLogText(string textEntered,string diseaseName,bool isMerge) {
			Meth.NoCheckMiddleTierRole();
			string logText="Problem 'Other: "+(textEntered??"")+"' converted to '"+(diseaseName??"")+"'";
			if(isMerge) {
				logText+=", merged with the patient's active problem of that name (the converted entry was set inactive)";
			}
			return logText+".";
		}

		///<summary>Converts an "Other" problem to the definition the user picked, saves it and writes a PatProblemListEdit audit entry.  textEntered is
		///what the patient typed (for the audit text).  Throws an ApplicationException when GetProblemConvertError finds a problem, and the Exception
		///of Diseases.VerifyCanUpdate when the problem is a pregnancy diagnosis whose dates no longer cover its vital sign exams.  Returns the audit
		///text.  The caller checks the PatProblemListEdit permission first.</summary>
		public static string ConvertProblem(Disease disease,string textEntered,DiseaseDef diseaseDefNew,bool keepTextAsNote,bool isMerge) {
			Meth.NoCheckMiddleTierRole();
			string error=GetProblemConvertError(disease,diseaseDefNew);
			if(error!="") {
				throw new ApplicationException(error);
			}
			Diseases.VerifyCanUpdate(disease);
			ApplyProblemConvert(disease,diseaseDefNew.DiseaseDefNum,keepTextAsNote,isMerge);
			Diseases.Update(disease);
			string logText=GetProblemConvertLogText(textEntered,diseaseDefNew.DiseaseName,isMerge);
			SecurityLogs.MakeLogEntry(EnumPermType.PatProblemListEdit,disease.PatNum,logText);
			return logText;
		}
		#endregion Problems

		#region Search
		///<summary>The definitions whose name contains every word of the search text, in any order and ignoring case.  Used by the Allergy list
		///while it is opened to convert an "Other" allergy, pre-filled with what the patient typed.  Blank text returns the whole list.</summary>
		public static List<T> FilterByWords<T>(List<T> listItems,Func<T,string> funcGetName,string searchText) {
			Meth.NoCheckMiddleTierRole();
			if(listItems==null) {
				return new List<T>();
			}
			List<string> listWords=Medications.GetSearchWords(searchText);
			if(listWords.Count==0) {
				return new List<T>(listItems);
			}
			return listItems.FindAll(x => {
				string name=(funcGetName(x)??"").ToLowerInvariant();
				return listWords.All(y => name.Contains(y));
			});
		}
		#endregion Search
	}
}
