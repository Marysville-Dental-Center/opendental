// The steps the user sees when converting an eForm "Other" allergy or problem to a listed one (Open Dental 25.2, package B-MED).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Shared by the Allergy edit window, the Problem edit window (FrmDiseaseEdit) and the right-click menus of the Medical window.  The rules
	///and the database work are in OpenDentBusiness.OtherEntryConverts.  Lives in WpfControlsOD so that FrmDiseaseEdit can use it; the lists are
	///opened through FormLauncher.</summary>
	public class OtherEntryConvertL {
		///<summary>Converts one saved "Other" allergy.  Opens the Allergy list in selection mode, searched for what the patient typed; its Add button
		///starts a new allergy with that text as the description.  Then asks whether to merge when the patient already has an active allergy with the
		///picked definition, and whether to keep the typed text as the reaction when keepTextAsReaction is null.  Saves the allergy and writes the
		///audit entry.  textEntered is what the patient typed, as it was saved.  Returns true when the allergy was converted.</summary>
		public static bool ConvertOtherAllergy(Allergy allergy,string textEntered,bool? keepTextAsReaction) {
			if(!Security.IsAuthorized(EnumPermType.PatAllergyListEdit)) {
				return false;
			}
			if(!OtherEntryConverts.IsOtherAllergy(allergy)) {
				MsgBox.Show(Lans.g("OtherEntryConvertL","Only a saved allergy entered as Other can be converted."));
				return false;
			}
			FormLauncher formLauncher=new FormLauncher(EnumFormName.FormAllergySetup);
			formLauncher.SetField("IsSelectionMode",true);
			formLauncher.SetField("TextOtherToConvert",textEntered??"");
			formLauncher.ShowDialog();
			if(formLauncher.IsDialogCancel) {
				return false;
			}
			AllergyDef allergyDef=AllergyDefs.GetOne(formLauncher.GetField<long>("AllergyDefNumSelected"));
			string error=OtherEntryConverts.GetAllergyConvertError(allergy,allergyDef);
			if(error!="") {
				MsgBox.Show(error);
				return false;
			}
			bool isMerge=false;
			if(allergy.StatusIsActive && OtherEntryConverts.GetActiveAllergyWithDef(allergy.PatNum,allergyDef.AllergyDefNum,allergy.AllergyNum)!=null) {
				isMerge=MsgBox.Show(MsgBoxButtons.YesNo,Lans.g("OtherEntryConvertL","This patient already has an active allergy named")+" '"
					+allergyDef.Description+"'.\r\n"
					+Lans.g("OtherEntryConvertL","Merge them?  Yes sets the Other entry inactive and keeps the existing allergy.  No keeps both active."));
			}
			bool isKeepText=GetKeepText(keepTextAsReaction,textEntered,Lans.g("OtherEntryConvertL","Keep the patient's entered text as the reaction?"));
			try {
				OtherEntryConverts.ConvertAllergy(allergy,textEntered,allergyDef,isKeepText,isMerge);
			}
			catch(Exception ex) {
				MsgBox.Show(ex.Message);
				return false;
			}
			return true;
		}

		///<summary>Converts one saved "Other" problem.  Opens the Problem list in selection mode, searched for what the patient typed; its Add button
		///starts a new problem with that text as the name.  Keeps the problem's status, start date and stop date.  Asks whether to merge when the
		///patient already has an active problem with the picked definition, and whether to keep the typed text as the note when keepTextAsNote is
		///null.  Saves the problem and writes the audit entry.  textEntered is what the patient typed, as it was saved.  Returns true when the
		///problem was converted.</summary>
		public static bool ConvertOtherProblem(Disease disease,string textEntered,bool? keepTextAsNote) {
			if(!Security.IsAuthorized(EnumPermType.PatProblemListEdit)) {
				return false;
			}
			if(!OtherEntryConverts.IsOtherProblem(disease)) {
				MsgBox.Show(Lans.g("OtherEntryConvertL","Only a saved problem entered as Other can be converted."));
				return false;
			}
			FormLauncher formLauncher=new FormLauncher(EnumFormName.FormDiseaseDefs);
			formLauncher.SetField("IsSelectionMode",true);
			formLauncher.SetField("TextOtherToConvert",textEntered??"");
			formLauncher.ShowDialog();
			if(formLauncher.IsDialogCancel) {
				return false;
			}
			List<DiseaseDef> listDiseaseDefs=formLauncher.GetField<List<DiseaseDef>>("ListDiseaseDefsSelected");
			if(listDiseaseDefs==null || listDiseaseDefs.Count==0) {
				return false;
			}
			DiseaseDef diseaseDef=DiseaseDefs.GetItem(listDiseaseDefs[0].DiseaseDefNum)??listDiseaseDefs[0];
			string error=OtherEntryConverts.GetProblemConvertError(disease,diseaseDef);
			if(error!="") {
				MsgBox.Show(error);
				return false;
			}
			bool isMerge=false;
			if(disease.ProbStatus==ProblemStatus.Active
				&& OtherEntryConverts.GetActiveProblemWithDef(disease.PatNum,diseaseDef.DiseaseDefNum,disease.DiseaseNum)!=null)
			{
				isMerge=MsgBox.Show(MsgBoxButtons.YesNo,Lans.g("OtherEntryConvertL","This patient already has an active problem named")+" '"
					+diseaseDef.DiseaseName+"'.\r\n"
					+Lans.g("OtherEntryConvertL","Merge them?  Yes sets the Other entry inactive and keeps the existing problem.  No keeps both active."));
			}
			bool isKeepText=GetKeepText(keepTextAsNote,textEntered,Lans.g("OtherEntryConvertL","Keep the patient's entered text as the note?"));
			try {
				OtherEntryConverts.ConvertProblem(disease,textEntered,diseaseDef,isKeepText,isMerge);
			}
			catch(Exception ex) {
				MsgBox.Show(ex.Message);
				return false;
			}
			return true;
		}

		///<summary>The window's check box when it has one.  Otherwise asks, unless there is no text to keep.</summary>
		private static bool GetKeepText(bool? isKeepText,string textEntered,string question) {
			if(isKeepText.HasValue) {
				return isKeepText.Value;
			}
			if(string.IsNullOrWhiteSpace(textEntered)) {
				return false;
			}
			return MsgBox.Show(MsgBoxButtons.YesNo,question+"\r\n"+textEntered);
		}
	}
}
