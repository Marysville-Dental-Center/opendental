// v25_2-chart-eform-allergy-other-convert and v25_2-chart-eform-problem-other-convert: converting eForm "Other" allergies and problems to listed
// ones (package B-MED). Synthetic patients, allergies and problems only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BMEDTests {
	///<summary>The conversion rules, without a database.</summary>
	[TestClass]
	public class OtherConvertRuleTests {
		[TestInitialize]
		public void TestInitialize() {
			BMedDb.UseUsEnglish();
		}

		///<summary>MSTest v1 has no Assert.ThrowsException.</summary>
		public static void ExpectApplicationException(Action action) {
			try {
				action();
			}
			catch(ApplicationException) {
				return;
			}
			Assert.Fail("Expected an ApplicationException.");
		}

		[TestMethod]
		public void OtherName_IsTheEFormCatchAll() {
			Assert.IsTrue(OtherEntryConverts.IsOtherName("Other"));
			Assert.IsTrue(OtherEntryConverts.IsOtherName(" other "));
			Assert.IsFalse(OtherEntryConverts.IsOtherName("Other allergy"));
			Assert.IsFalse(OtherEntryConverts.IsOtherName(null));
			Assert.IsTrue(OtherEntryConverts.IsOtherAllergyDef(new AllergyDef { Description="Other" }));
			Assert.IsFalse(OtherEntryConverts.IsOtherAllergyDef(null));
			Assert.IsTrue(OtherEntryConverts.IsOtherDiseaseDef(new DiseaseDef { DiseaseName="OTHER" }));
			Assert.IsFalse(OtherEntryConverts.IsOtherProblem(new Disease()),"An unsaved problem is never converted.");
			Assert.IsFalse(OtherEntryConverts.IsOtherAllergy(new Allergy()),"An unsaved allergy is never converted.");
			Assert.AreEqual("Patient entered: peanut butter",OtherEntryConverts.GetPatientEnteredText("peanut butter"));
		}

		[TestMethod]
		public void AllergyConvert_ClearsTheTypedTextUnlessKept() {
			Allergy allergy=new Allergy { AllergyNum=5,AllergyDefNum=1,PatNum=9,Reaction="latex gloves",StatusIsActive=true };
			OtherEntryConverts.ApplyAllergyConvert(allergy,allergyDefNumNew:7,keepTextAsReaction:false,isMerge:false);
			Assert.AreEqual(7,allergy.AllergyDefNum);
			Assert.AreEqual("",allergy.Reaction);
			Assert.IsTrue(allergy.StatusIsActive);
			allergy=new Allergy { AllergyNum=5,AllergyDefNum=1,PatNum=9,Reaction="latex gloves",StatusIsActive=true };
			OtherEntryConverts.ApplyAllergyConvert(allergy,7,keepTextAsReaction:true,isMerge:false);
			Assert.AreEqual("latex gloves",allergy.Reaction);
		}

		[TestMethod]
		public void AllergyMerge_SetsTheConvertedEntryInactive() {
			Allergy allergy=new Allergy { AllergyNum=5,AllergyDefNum=1,PatNum=9,Reaction="latex",StatusIsActive=true };
			OtherEntryConverts.ApplyAllergyConvert(allergy,7,keepTextAsReaction:false,isMerge:true);
			Assert.AreEqual(7,allergy.AllergyDefNum);
			Assert.IsFalse(allergy.StatusIsActive);
		}

		[TestMethod]
		public void ProblemConvert_KeepsStatusAndDates() {
			DateTime dateStart=new DateTime(2024,3,4);
			DateTime dateStop=new DateTime(2025,6,7);
			Disease disease=new Disease { DiseaseNum=3,DiseaseDefNum=1,PatNote="sugar problems",ProbStatus=ProblemStatus.Resolved,DateStart=dateStart,
				DateStop=dateStop };
			OtherEntryConverts.ApplyProblemConvert(disease,diseaseDefNumNew:8,keepTextAsNote:false,isMerge:false);
			Assert.AreEqual(8,disease.DiseaseDefNum);
			Assert.AreEqual("",disease.PatNote);
			Assert.AreEqual(ProblemStatus.Resolved,disease.ProbStatus);
			Assert.AreEqual(dateStart,disease.DateStart);
			Assert.AreEqual(dateStop,disease.DateStop);
			disease=new Disease { DiseaseNum=3,DiseaseDefNum=1,PatNote="sugar problems",ProbStatus=ProblemStatus.Active };
			OtherEntryConverts.ApplyProblemConvert(disease,8,keepTextAsNote:true,isMerge:true);
			Assert.AreEqual("sugar problems",disease.PatNote);
			Assert.AreEqual(ProblemStatus.Inactive,disease.ProbStatus,"A merged entry becomes inactive.");
		}

		[TestMethod]
		public void LogTexts() {
			Assert.AreEqual("Allergy 'Other: latex gloves' converted to 'Latex'.",OtherEntryConverts.GetAllergyConvertLogText("latex gloves","Latex",false));
			Assert.AreEqual("Problem 'Other: sugar' converted to 'Diabetes'.",OtherEntryConverts.GetProblemConvertLogText("sugar","Diabetes",false));
			StringAssert.StartsWith(OtherEntryConverts.GetAllergyConvertLogText("x","Latex",true),"Allergy 'Other: x' converted to 'Latex', merged");
			StringAssert.StartsWith(OtherEntryConverts.GetProblemConvertLogText("x","Diabetes",true),"Problem 'Other: x' converted to 'Diabetes', merged");
		}

		[TestMethod]
		public void ConvertErrors() {
			Allergy allergy=new Allergy { AllergyNum=5,AllergyDefNum=1 };
			Assert.AreNotEqual("",OtherEntryConverts.GetAllergyConvertError(null,new AllergyDef { AllergyDefNum=2,Description="Latex" }));
			Assert.AreNotEqual("",OtherEntryConverts.GetAllergyConvertError(new Allergy(),new AllergyDef { AllergyDefNum=2,Description="Latex" }));
			Assert.AreNotEqual("",OtherEntryConverts.GetAllergyConvertError(allergy,null));
			Assert.AreNotEqual("",OtherEntryConverts.GetAllergyConvertError(allergy,new AllergyDef { AllergyDefNum=3,Description="Other" }),
				"Converting to Other again is refused.");
			Assert.AreEqual("",OtherEntryConverts.GetAllergyConvertError(allergy,new AllergyDef { AllergyDefNum=2,Description="Latex" }));
			Disease disease=new Disease { DiseaseNum=3,DiseaseDefNum=1 };
			Assert.AreNotEqual("",OtherEntryConverts.GetProblemConvertError(new Disease(),new DiseaseDef { DiseaseDefNum=2,DiseaseName="Asthma" }));
			Assert.AreNotEqual("",OtherEntryConverts.GetProblemConvertError(disease,new DiseaseDef { DiseaseDefNum=4,DiseaseName="Other" }));
			Assert.AreEqual("",OtherEntryConverts.GetProblemConvertError(disease,new DiseaseDef { DiseaseDefNum=2,DiseaseName="Asthma" }));
			ExpectApplicationException(() => OtherEntryConverts.ConvertAllergy(allergy,"x",null,false,false));
		}

		[TestMethod]
		public void FilterByWords_PreSearchesTheList() {
			List<AllergyDef> listAllergyDefs=new List<AllergyDef> {
				new AllergyDef { AllergyDefNum=1,Description="Peanuts" },
				new AllergyDef { AllergyDefNum=2,Description="Tree nuts" },
				new AllergyDef { AllergyDefNum=3,Description="Latex" },
			};
			CollectionAssert.AreEqual(new long[] { 1,2 },OtherEntryConverts.FilterByWords(listAllergyDefs,x => x.Description,"NUTS")
				.Select(x => x.AllergyDefNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 2 },OtherEntryConverts.FilterByWords(listAllergyDefs,x => x.Description,"nuts tree")
				.Select(x => x.AllergyDefNum).ToArray());
			Assert.AreEqual(3,OtherEntryConverts.FilterByWords(listAllergyDefs,x => x.Description,"").Count,"Blank keeps the whole list.");
			Assert.AreEqual(0,OtherEntryConverts.FilterByWords(listAllergyDefs,x => x.Description,"shellfish").Count);
			Assert.AreEqual(0,OtherEntryConverts.FilterByWords<AllergyDef>(null,x => x.Description,"a").Count);
		}
	}

	///<summary>Conversions against the scratch database, with rows shaped like the ones EFormImport writes for unmatched typed entries.</summary>
	[TestClass]
	public class OtherConvertDbTests {
		[TestInitialize]
		public void TestInitialize() {
			BMedDb.Begin();
		}

		private static long NewPatNum() {
			return BMedDb.NextSyntheticNum();//synthetic patient numbers; no patient row is needed for these tables
		}

		private static AllergyDef InsertAllergyDef(string description) {
			AllergyDef allergyDef=new AllergyDef();
			allergyDef.Description=description;
			allergyDef.UniiCode="";
			AllergyDefs.Insert(allergyDef);
			return allergyDef;
		}

		///<summary>The catch-all definition, found or added the way EFormImport does.</summary>
		private static AllergyDef GetOtherAllergyDef() {
			AllergyDef allergyDef=AllergyDefs.GetAll(includeHidden:false).Find(x => x.Description=="Other");
			return allergyDef??InsertAllergyDef("Other");
		}

		private static Allergy InsertAllergy(long patNum,long allergyDefNum,string reaction,bool isActive=true) {
			Allergy allergy=new Allergy();
			allergy.PatNum=patNum;
			allergy.AllergyDefNum=allergyDefNum;
			allergy.Reaction=reaction;
			allergy.StatusIsActive=isActive;
			allergy.SnomedReaction="";
			Allergies.Insert(allergy);
			return allergy;
		}

		private static DiseaseDef InsertDiseaseDef(string diseaseName) {
			DiseaseDef diseaseDef=new DiseaseDef();
			diseaseDef.DiseaseName=diseaseName;
			diseaseDef.ICD9Code="";
			diseaseDef.Icd10Code="";
			diseaseDef.SnomedCode="";
			DiseaseDefs.Insert(diseaseDef);
			DiseaseDefs.RefreshCache();
			return diseaseDef;
		}

		private static DiseaseDef GetOtherDiseaseDef() {
			DiseaseDefs.RefreshCache();
			return DiseaseDefs.GetFirstOrDefault(x => x.DiseaseName=="Other")??InsertDiseaseDef("Other");
		}

		private static Disease InsertDisease(long patNum,long diseaseDefNum,string patNote,ProblemStatus problemStatus=ProblemStatus.Active,
			DateTime dateStart=default)
		{
			Disease disease=new Disease();
			disease.PatNum=patNum;
			disease.DiseaseDefNum=diseaseDefNum;
			disease.PatNote=patNote;
			disease.ProbStatus=problemStatus;
			disease.DateStart=dateStart;
			disease.SnomedProblemType="";
			Diseases.Insert(disease);
			return disease;
		}

		private static string LastLogText(EnumPermType permType,long patNum) {
			return DataCore.GetScalar("SELECT LogText FROM securitylog WHERE PermType="+POut.Int((int)permType)+" AND PatNum="+POut.Long(patNum)
				+" ORDER BY SecurityLogNum DESC LIMIT 1");
		}

		[TestMethod]
		public void Allergy_ConvertsToTheListedOne_AndIsAudited() {
			long patNum=NewPatNum();
			long patNumOther=patNum+1;
			AllergyDef allergyDefOther=GetOtherAllergyDef();
			AllergyDef allergyDefLatex=InsertAllergyDef("Latex "+BMedDb.Unique());
			Allergy allergy=InsertAllergy(patNum,allergyDefOther.AllergyDefNum,"latex gloves");
			Allergy allergyOtherPatient=InsertAllergy(patNumOther,allergyDefOther.AllergyDefNum,"latex gloves");
			Assert.IsTrue(OtherEntryConverts.IsOtherAllergy(allergy));
			Assert.IsNull(OtherEntryConverts.GetActiveAllergyWithDef(patNum,allergyDefLatex.AllergyDefNum,allergy.AllergyNum));
			string logText=OtherEntryConverts.ConvertAllergy(allergy,"latex gloves",allergyDefLatex,keepTextAsReaction:false,isMerge:false);
			Allergy allergyDb=Allergies.GetOne(allergy.AllergyNum);
			Assert.AreEqual(allergyDefLatex.AllergyDefNum,allergyDb.AllergyDefNum);
			Assert.AreEqual("",allergyDb.Reaction);
			Assert.IsTrue(allergyDb.StatusIsActive);
			Assert.IsFalse(OtherEntryConverts.IsOtherAllergy(allergyDb));
			Assert.AreEqual("Allergy 'Other: latex gloves' converted to '"+allergyDefLatex.Description+"'.",logText);
			Assert.AreEqual(logText,LastLogText(EnumPermType.PatAllergyListEdit,patNum));
			//Another patient who typed the same text is not touched.
			Allergy allergyOtherPatientDb=Allergies.GetOne(allergyOtherPatient.AllergyNum);
			Assert.AreEqual(allergyDefOther.AllergyDefNum,allergyOtherPatientDb.AllergyDefNum);
			Assert.AreEqual("latex gloves",allergyOtherPatientDb.Reaction);
		}

		[TestMethod]
		public void Allergy_MergeWithAnActiveOne_KeepsTheExistingRow() {
			long patNum=NewPatNum();
			AllergyDef allergyDefOther=GetOtherAllergyDef();
			AllergyDef allergyDefPeanut=InsertAllergyDef("Peanut "+BMedDb.Unique());
			Allergy allergyExisting=InsertAllergy(patNum,allergyDefPeanut.AllergyDefNum,"hives");
			Allergy allergy=InsertAllergy(patNum,allergyDefOther.AllergyDefNum,"peanut butter");
			Allergy allergyFound=OtherEntryConverts.GetActiveAllergyWithDef(patNum,allergyDefPeanut.AllergyDefNum,allergy.AllergyNum);
			Assert.IsNotNull(allergyFound);
			Assert.AreEqual(allergyExisting.AllergyNum,allergyFound.AllergyNum);
			OtherEntryConverts.ConvertAllergy(allergy,"peanut butter",allergyDefPeanut,keepTextAsReaction:true,isMerge:true);
			Allergy allergyDb=Allergies.GetOne(allergy.AllergyNum);
			Assert.AreEqual(allergyDefPeanut.AllergyDefNum,allergyDb.AllergyDefNum);
			Assert.IsFalse(allergyDb.StatusIsActive,"The Other entry is set inactive.");
			Assert.AreEqual("peanut butter",allergyDb.Reaction,"Kept as the reaction when asked.");
			Allergy allergyExistingDb=Allergies.GetOne(allergyExisting.AllergyNum);
			Assert.IsTrue(allergyExistingDb.StatusIsActive);
			Assert.AreEqual("hives",allergyExistingDb.Reaction);
			Assert.AreEqual(1,Allergies.GetAll(patNum,showInactive:false).Count(x => x.AllergyDefNum==allergyDefPeanut.AllergyDefNum));
			StringAssert.Contains(LastLogText(EnumPermType.PatAllergyListEdit,patNum),"merged");
		}

		[TestMethod]
		public void Allergy_ToOtherAgain_IsRefusedAndNothingChanges() {
			long patNum=NewPatNum();
			AllergyDef allergyDefOther=GetOtherAllergyDef();
			Allergy allergy=InsertAllergy(patNum,allergyDefOther.AllergyDefNum,"bee stings");
			OtherConvertRuleTests.ExpectApplicationException(
				() => OtherEntryConverts.ConvertAllergy(allergy.Copy(),"bee stings",allergyDefOther,keepTextAsReaction:false,isMerge:false));
			Assert.AreEqual("bee stings",Allergies.GetOne(allergy.AllergyNum).Reaction);
			string logText=LastLogText(EnumPermType.PatAllergyListEdit,patNum);
			Assert.IsTrue(string.IsNullOrEmpty(logText),"No audit entry, but found: '"+logText+"'");
		}

		[TestMethod]
		public void Problem_ConvertsToTheListedOne_KeepingStatusAndDates() {
			long patNum=NewPatNum();
			DiseaseDef diseaseDefOther=GetOtherDiseaseDef();
			DiseaseDef diseaseDefAsthma=InsertDiseaseDef("Asthma "+BMedDb.Unique());
			DateTime dateStart=new DateTime(2023,5,6);
			Disease disease=InsertDisease(patNum,diseaseDefOther.DiseaseDefNum,"wheezing at night",ProblemStatus.Resolved,dateStart);
			Assert.IsTrue(OtherEntryConverts.IsOtherProblem(disease));
			string logText=OtherEntryConverts.ConvertProblem(disease,"wheezing at night",diseaseDefAsthma,keepTextAsNote:false,isMerge:false);
			Disease diseaseDb=Diseases.GetOne(disease.DiseaseNum);
			Assert.AreEqual(diseaseDefAsthma.DiseaseDefNum,diseaseDb.DiseaseDefNum);
			Assert.AreEqual("",diseaseDb.PatNote);
			Assert.AreEqual(ProblemStatus.Resolved,diseaseDb.ProbStatus);
			Assert.AreEqual(dateStart,diseaseDb.DateStart);
			Assert.IsFalse(OtherEntryConverts.IsOtherProblem(diseaseDb));
			Assert.AreEqual("Problem 'Other: wheezing at night' converted to '"+diseaseDefAsthma.DiseaseName+"'.",logText);
			Assert.AreEqual(logText,LastLogText(EnumPermType.PatProblemListEdit,patNum));
		}

		[TestMethod]
		public void Problem_MergeWithAnActiveOne_SetsTheOtherEntryInactive() {
			long patNum=NewPatNum();
			DiseaseDef diseaseDefOther=GetOtherDiseaseDef();
			DiseaseDef diseaseDefHtn=InsertDiseaseDef("Hypertension "+BMedDb.Unique());
			Disease diseaseExisting=InsertDisease(patNum,diseaseDefHtn.DiseaseDefNum,"");
			Disease disease=InsertDisease(patNum,diseaseDefOther.DiseaseDefNum,"high blood pressure");
			Assert.AreEqual(diseaseExisting.DiseaseNum,
				OtherEntryConverts.GetActiveProblemWithDef(patNum,diseaseDefHtn.DiseaseDefNum,disease.DiseaseNum).DiseaseNum);
			OtherEntryConverts.ConvertProblem(disease,"high blood pressure",diseaseDefHtn,keepTextAsNote:true,isMerge:true);
			Disease diseaseDb=Diseases.GetOne(disease.DiseaseNum);
			Assert.AreEqual(diseaseDefHtn.DiseaseDefNum,diseaseDb.DiseaseDefNum);
			Assert.AreEqual(ProblemStatus.Inactive,diseaseDb.ProbStatus);
			Assert.AreEqual("high blood pressure",diseaseDb.PatNote);
			Assert.AreEqual(ProblemStatus.Active,Diseases.GetOne(diseaseExisting.DiseaseNum).ProbStatus);
			Assert.AreEqual(1,Diseases.GetDiseasesForPatient(patNum,diseaseDefHtn.DiseaseDefNum,showActiveOnly:true).Count);
		}

		[TestMethod]
		public void Problem_OnlyTheActiveOnesCountForMerging() {
			long patNum=NewPatNum();
			DiseaseDef diseaseDefOther=GetOtherDiseaseDef();
			DiseaseDef diseaseDefGerd=InsertDiseaseDef("Reflux "+BMedDb.Unique());
			InsertDisease(patNum,diseaseDefGerd.DiseaseDefNum,"",ProblemStatus.Resolved);
			Disease disease=InsertDisease(patNum,diseaseDefOther.DiseaseDefNum,"heartburn");
			Assert.IsNull(OtherEntryConverts.GetActiveProblemWithDef(patNum,diseaseDefGerd.DiseaseDefNum,disease.DiseaseNum));
		}
	}

	///<summary>The windows that start the conversion (the GUI is not launched, so these read the sources).</summary>
	[TestClass]
	public class OtherConvertWiringTests {
		[TestMethod]
		public void AllergyEditWindow_OffersConvertForOtherAllergies() {
			string source=BMedDb.ReadRepoFile("OpenDental","Forms","FormAllergyEdit.cs");
			string designer=BMedDb.ReadRepoFile("OpenDental","Forms","FormAllergyEdit.designer.cs");
			StringAssert.Contains(source,"if(OtherEntryConverts.IsOtherAllergy(AllergyCur)) {");
			StringAssert.Contains(source,"OtherEntryConverts.GetPatientEnteredText(_textOtherEntered)");
			StringAssert.Contains(source,"OtherEntryConvertL.ConvertOtherAllergy(allergy,_textOtherEntered,checkKeepOtherText.Checked)");
			StringAssert.Contains(designer,"this.checkKeepOtherText.Text = \"Keep entered text as reaction\";");
			Assert.IsFalse(designer.Contains("this.checkKeepOtherText.Checked = true"),"Cleared by default.");
		}

		[TestMethod]
		public void ProblemEditWindow_OffersConvertForOtherProblems() {
			string source=BMedDb.ReadRepoFile("WpfControlsOD","Frms","FrmDiseaseEdit.xaml.cs");
			string xaml=BMedDb.ReadRepoFile("WpfControlsOD","Frms","FrmDiseaseEdit.xaml");
			StringAssert.Contains(source,"if(!IsNew && OtherEntryConverts.IsOtherProblem(_disease)) {");
			StringAssert.Contains(source,"OtherEntryConvertL.ConvertOtherProblem(disease,_textOtherEntered,checkKeepOtherText.Checked==true)");
			StringAssert.Contains(xaml,"x:Name=\"butConvertOther\"");
			StringAssert.Contains(xaml,"Click=\"butConvertOther_Click\"");
		}

		[TestMethod]
		public void MedicalWindow_HasRightClickConvertItems() {
			string source=BMedDb.ReadRepoFile("OpenDental","Forms","FormMedical.cs");
			StringAssert.Contains(source,"InitOtherConvertMenus();");
			StringAssert.Contains(source,"Convert \\\"Other\\\" allergy...");
			StringAssert.Contains(source,"Convert \\\"Other\\\" problem...");
			StringAssert.Contains(source,"gridAllergies.ContextMenu=contextMenuAllergies;");
			StringAssert.Contains(source,"gridDiseases.ContextMenu=contextMenuProblems;");
			StringAssert.Contains(source,"OtherEntryConvertL.ConvertOtherAllergy(allergy.Copy(),allergy.Reaction,keepTextAsReaction:null)");
			StringAssert.Contains(source,"OtherEntryConvertL.ConvertOtherProblem(disease.Copy(),disease.PatNote,keepTextAsNote:null)");
		}

		[TestMethod]
		public void Lists_OpenPreSearchedAndAddStartsWithTheTypedText() {
			string flow=BMedDb.ReadRepoFile("WpfControlsOD","Logic","OtherEntryConvertL.cs");
			StringAssert.Contains(flow,"new FormLauncher(EnumFormName.FormAllergySetup)");
			StringAssert.Contains(flow,"new FormLauncher(EnumFormName.FormDiseaseDefs)");
			Assert.AreEqual(2,System.Text.RegularExpressions.Regex.Matches(flow,"SetField\\(\"TextOtherToConvert\",textEntered\\?\\?\"\"\\)").Count);
			Assert.AreEqual(2,System.Text.RegularExpressions.Regex.Matches(flow,"SetField\\(\"IsSelectionMode\",true\\)").Count);
			StringAssert.Contains(flow,"Security.IsAuthorized(EnumPermType.PatAllergyListEdit)");
			StringAssert.Contains(flow,"Security.IsAuthorized(EnumPermType.PatProblemListEdit)");
			string allergySetup=BMedDb.ReadRepoFile("OpenDental","Forms","FormAllergySetup.cs");
			StringAssert.Contains(allergySetup,"_listAllergyDefs=FilterForOtherConvert(_listAllergyDefs);");
			StringAssert.Contains(allergySetup,"SetNewDefForOtherConvert(formAllergyDefEdit.AllergyDefCur);");
			StringAssert.Contains(allergySetup,"if(PickNewDefForOtherConvert(formAllergyDefEdit.DialogResult,formAllergyDefEdit.AllergyDefCur)) {");
			//Adding a definition still needs AllergyDefEdit, checked before the edit window opens.
			int indexPermission=allergySetup.IndexOf("Security.IsAuthorized(EnumPermType.AllergyDefEdit)");
			Assert.IsTrue(indexPermission>=0 && indexPermission<allergySetup.IndexOf("SetNewDefForOtherConvert("));
			StringAssert.Contains(BMedDb.ReadRepoFile("OpenDental","Forms","FormAllergySetup.OtherConvert.cs"),"public string TextOtherToConvert;");
			string diseaseDefs=BMedDb.ReadRepoFile("OpenDental","Forms","FormDiseaseDefs.cs");
			StringAssert.Contains(diseaseDefs,"ShowSearchForOtherConvert();");
			StringAssert.Contains(diseaseDefs,"SetNewDefForOtherConvert(diseaseDef);");
			StringAssert.Contains(diseaseDefs,"PickNewDefForOtherConvert(formDiseaseDefEdit.DiseaseDefCur);");
			indexPermission=diseaseDefs.IndexOf("Security.IsAuthorized(EnumPermType.ProblemDefEdit)");
			Assert.IsTrue(indexPermission>=0 && indexPermission<diseaseDefs.IndexOf("SetNewDefForOtherConvert("));
			string diseaseDefsConvert=BMedDb.ReadRepoFile("OpenDental","Forms","FormDiseaseDefs.OtherConvert.cs");
			StringAssert.Contains(diseaseDefsConvert,"public string TextOtherToConvert;");
			StringAssert.Contains(diseaseDefsConvert,"textDescript.Text=TextOtherToConvert;");
		}
	}
}
