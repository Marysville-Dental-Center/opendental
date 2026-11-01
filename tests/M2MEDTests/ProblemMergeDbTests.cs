// Database tests for the Problem merge that keeps the problem the user chooses (v26_1-lists-problem-merge-keep, package M2-MED), on a scratch MariaDB
// database with the Open Dental 26.1.70 schema and synthetic rows only. Skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root
// with a blank password, or when M2MED_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2MEDTests {
	[TestClass]
	public class ProblemMergeDbTests {
		private static readonly string _dbName=ScratchDb.Name("merge");
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			ScratchDb.CreateFrom26_1Reference(_dbName);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason==null) {
				ScratchDb.DropQuietly(_dbName);
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ScratchDb.Connect(_dbName);
			foreach(string table in new[] { "diseasedef","disease","rxalert","eduresource","familyhealth","reminderrule","ehrtrigger","sheetfielddef",
				"eformfielddef" })
			{
				DataCore.NonQ("DELETE FROM "+table);
			}
			DataCore.NonQ("UPDATE preference SET ValueString='0' WHERE PrefName='ProblemsIndicateNone'");
			Prefs.RefreshCache();
		}

		#region Synthetic data
		private static DiseaseDef InsertDef(string name,string icd10="",string snomed="",bool isHidden=false) {
			DiseaseDef diseaseDef=new DiseaseDef();
			diseaseDef.DiseaseName=name;
			diseaseDef.ICD9Code="";
			diseaseDef.Icd10Code=icd10;
			diseaseDef.SnomedCode=snomed;
			diseaseDef.IsHidden=isHidden;
			diseaseDef.ItemOrder=(int)Count("diseasedef");
			DiseaseDefs.Insert(diseaseDef);
			return diseaseDef;
		}

		private static Disease InsertProblem(long patNum,long diseaseDefNum,string patNote="") {
			Disease disease=new Disease();
			disease.PatNum=patNum;
			disease.DiseaseDefNum=diseaseDefNum;
			disease.PatNote=patNote;
			disease.ProbStatus=ProblemStatus.Active;
			disease.SnomedProblemType="";
			OpenPmsDentalBusiness.Crud.DiseaseCrud.Insert(disease);
			return disease;
		}

		private static long Count(string table,string where="1") {
			return PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM "+table+" WHERE "+where));
		}

		///<summary>MSTest v1 has no Assert.ThrowsException.</summary>
		private static void AssertThrowsApplicationException(Action action) {
			try {
				action();
			}
			catch(ApplicationException) {
				return;
			}
			Assert.Fail("Expected an ApplicationException.");
		}

		private static string Scalar(string command) {
			return DataCore.GetScalar(command);
		}

		///<summary>Two problem definitions with every kind of reference to the discarded one, plus references to an unrelated third one.</summary>
		private static (DiseaseDef keep,DiseaseDef discard,DiseaseDef other) FillSyntheticOffice() {
			DiseaseDef diseaseDefKeep=InsertDef("Diabetes type 2","E11.9","44054006");
			DiseaseDef diseaseDefDiscard=InsertDef("Diabetes II","E11.8","",isHidden:true);
			DiseaseDef diseaseDefOther=InsertDef("Asthma","J45.909","195967001");
			//Patients 101 and 102 have the discarded problem, 103 has both, 104 has the kept one only, 105 has asthma.
			InsertProblem(101,diseaseDefDiscard.DiseaseDefNum,"since 2019");
			InsertProblem(102,diseaseDefDiscard.DiseaseDefNum);
			InsertProblem(103,diseaseDefDiscard.DiseaseDefNum,"old entry");
			InsertProblem(103,diseaseDefKeep.DiseaseDefNum,"new entry");
			InsertProblem(104,diseaseDefKeep.DiseaseDefNum);
			InsertProblem(105,diseaseDefOther.DiseaseDefNum);
			OpenPmsDentalBusiness.Crud.RxAlertCrud.Insert(new RxAlert { RxDefNum=1,DiseaseDefNum=diseaseDefDiscard.DiseaseDefNum,NotificationMsg="" });
			OpenPmsDentalBusiness.Crud.RxAlertCrud.Insert(new RxAlert { RxDefNum=2,DiseaseDefNum=diseaseDefOther.DiseaseDefNum,NotificationMsg="" });
			OpenPmsDentalBusiness.Crud.EduResourceCrud.Insert(new EduResource { DiseaseDefNum=diseaseDefDiscard.DiseaseDefNum,ResourceUrl="http://localhost/edu",SmokingSnoMed="" });
			OpenPmsDentalBusiness.Crud.FamilyHealthCrud.Insert(new FamilyHealth { PatNum=101,DiseaseDefNum=diseaseDefDiscard.DiseaseDefNum,PersonName="Synthetic parent" });
			OpenPmsDentalBusiness.Crud.ReminderRuleCrud.Insert(new ReminderRule { ReminderCriterion=EhrCriterion.Problem,CriterionFK=diseaseDefDiscard.DiseaseDefNum,CriterionValue="",
				Message="Synthetic problem reminder" });
			//An allergy rule whose allergy number happens to equal the discarded problem's number must not move.
			OpenPmsDentalBusiness.Crud.ReminderRuleCrud.Insert(new ReminderRule { ReminderCriterion=EhrCriterion.Allergy,CriterionFK=diseaseDefDiscard.DiseaseDefNum,CriterionValue="",
				Message="Synthetic allergy reminder" });
			EhrTrigger ehrTrigger=new EhrTrigger();
			ehrTrigger.Description="Synthetic trigger";
			ehrTrigger.ProblemDefNumList=" "+diseaseDefOther.DiseaseDefNum+"  "+diseaseDefDiscard.DiseaseDefNum+" ";
			OpenPmsDentalBusiness.Crud.EhrTriggerCrud.Insert(ehrTrigger);
			EhrTrigger ehrTriggerBoth=new EhrTrigger();
			ehrTriggerBoth.Description="Synthetic trigger with both";
			ehrTriggerBoth.ProblemDefNumList=" "+diseaseDefKeep.DiseaseDefNum+"  "+diseaseDefDiscard.DiseaseDefNum+" ";
			OpenPmsDentalBusiness.Crud.EhrTriggerCrud.Insert(ehrTriggerBoth);
			return (diseaseDefKeep,diseaseDefDiscard,diseaseDefOther);
		}

		private static void InsertSheetFieldDef(string fieldName) {
			SheetFieldDef sheetFieldDef=new SheetFieldDef();
			sheetFieldDef.SheetDefNum=1;
			sheetFieldDef.FieldType=SheetFieldType.CheckBox;
			sheetFieldDef.FieldName=fieldName;
			sheetFieldDef.FieldValue="";
			sheetFieldDef.FontName="";
			sheetFieldDef.RadioButtonValue="Y";
			sheetFieldDef.RadioButtonGroup="";
			sheetFieldDef.ReportableName="";
			sheetFieldDef.UiLabelMobile="";
			sheetFieldDef.UiLabelMobileRadioButton="";
			sheetFieldDef.Language="";
			OpenPmsDentalBusiness.Crud.SheetFieldDefCrud.Insert(sheetFieldDef);
		}

		private static void InsertEFormFieldDef(string dbLink) {
			EFormFieldDef eFormFieldDef=new EFormFieldDef();
			eFormFieldDef.EFormDefNum=1;
			eFormFieldDef.FieldType=EnumEFormFieldType.CheckBox;
			eFormFieldDef.DbLink=dbLink;
			eFormFieldDef.ValueLabel="Synthetic label";
			eFormFieldDef.PickListVis="";
			eFormFieldDef.PickListDb="";
			eFormFieldDef.ConditionalParent="";
			eFormFieldDef.ConditionalValue="";
			eFormFieldDef.ReportableName="";
			OpenPmsDentalBusiness.Crud.EFormFieldDefCrud.Insert(eFormFieldDef);
		}
		#endregion Synthetic data

		[TestMethod]
		public void Merge_MovesEveryReferenceToTheKeptProblem() {
			(DiseaseDef keep,DiseaseDef discard,DiseaseDef other)=FillSyntheticOffice();
			string keepNum=keep.DiseaseDefNum.ToString();
			string discardNum=discard.DiseaseDefNum.ToString();
			DiseaseDefMergeResult result=DiseaseDefs.Merge(keep.DiseaseDefNum,discard.DiseaseDefNum);
			Assert.AreEqual(0,Count("disease","DiseaseDefNum="+discardNum));
			Assert.AreEqual(5,Count("disease","DiseaseDefNum="+keepNum));
			Assert.AreEqual(1,Count("disease","DiseaseDefNum="+other.DiseaseDefNum),"Other problems are not touched.");
			Assert.AreEqual(3,result.CountProblemsMoved);
			Assert.AreEqual(1,Count("rxalert","DiseaseDefNum="+keepNum+" AND RxDefNum=1"));
			Assert.AreEqual(1,Count("rxalert","DiseaseDefNum="+other.DiseaseDefNum+" AND RxDefNum=2"));
			Assert.AreEqual(1,result.CountRxAlertsMoved);
			Assert.AreEqual(1,Count("eduresource","DiseaseDefNum="+keepNum));
			Assert.AreEqual(1,result.CountEduResourcesMoved);
			Assert.AreEqual(1,Count("familyhealth","DiseaseDefNum="+keepNum));
			Assert.AreEqual(1,result.CountFamilyHealthMoved);
			Assert.AreEqual(keepNum,Scalar("SELECT CriterionFK FROM reminderrule WHERE ReminderCriterion="+(int)EhrCriterion.Problem));
			Assert.AreEqual(discardNum,Scalar("SELECT CriterionFK FROM reminderrule WHERE ReminderCriterion="+(int)EhrCriterion.Allergy),
				"Only problem reminder rules point to problems.");
			Assert.AreEqual(1,result.CountReminderRulesMoved);
			Assert.AreEqual(" "+other.DiseaseDefNum+"  "+keepNum+" ",Scalar("SELECT ProblemDefNumList FROM ehrtrigger WHERE Description='Synthetic trigger'"));
			Assert.AreEqual(" "+keepNum+" ",Scalar("SELECT ProblemDefNumList FROM ehrtrigger WHERE Description='Synthetic trigger with both'"));
			Assert.AreEqual(2,result.CountCdsTriggersMoved);
			Assert.AreEqual(0,Count("diseasedef","DiseaseDefNum="+discardNum),"The discarded problem is deleted.");
			Assert.AreEqual(2,Count("diseasedef"));
			Assert.AreEqual(keep.DiseaseDefNum,result.DiseaseDefNumKept);
			Assert.AreEqual("Diabetes II",result.DiseaseNameDiscarded);
		}

		[TestMethod]
		public void Merge_KeepsTheChosenProblemsOwnFields() {
			(DiseaseDef keep,DiseaseDef discard,DiseaseDef other)=FillSyntheticOffice();
			//The user may keep either one; here the hidden one with no SNOMED CT code survives, unchanged.
			DiseaseDefs.Merge(discard.DiseaseDefNum,keep.DiseaseDefNum);
			DiseaseDef diseaseDefKept=DiseaseDefs.GetOne(discard.DiseaseDefNum);
			Assert.IsNotNull(diseaseDefKept);
			Assert.AreEqual("Diabetes II",diseaseDefKept.DiseaseName);
			Assert.AreEqual("E11.8",diseaseDefKept.Icd10Code);
			Assert.AreEqual("",diseaseDefKept.SnomedCode,"Codes are never mixed from the two problems.");
			Assert.IsTrue(diseaseDefKept.IsHidden);
			Assert.IsNull(DiseaseDefs.GetOne(keep.DiseaseDefNum));
			Assert.AreEqual(5,Count("disease","DiseaseDefNum="+discard.DiseaseDefNum));
		}

		[TestMethod]
		public void Merge_KeepsBothRowsForPatientsWhoHadBoth() {
			(DiseaseDef keep,DiseaseDef discard,DiseaseDef other)=FillSyntheticOffice();
			DiseaseDefMergeResult result=DiseaseDefs.Merge(keep.DiseaseDefNum,discard.DiseaseDefNum);
			Assert.AreEqual(1,result.CountPatientsWithBoth,"Patient 103.");
			Assert.AreEqual(2,Count("disease","PatNum=103 AND DiseaseDefNum="+keep.DiseaseDefNum),"History is kept: both rows stay.");
			Assert.AreEqual(1,Count("disease","PatNum=103 AND PatNote='old entry'"));
			StringAssert.Contains(DiseaseDefs.GetMergeSummaryText(result),"twice");
		}

		[TestMethod]
		public void Merge_MovesTheProblemsIndicateNonePreference() {
			DiseaseDef diseaseDefNone=InsertDef("None");
			DiseaseDef diseaseDefNoneDuplicate=InsertDef("NONE");
			Prefs.UpdateLong(PrefName.ProblemsIndicateNone,diseaseDefNoneDuplicate.DiseaseDefNum);
			DiseaseDefMergeResult result=DiseaseDefs.Merge(diseaseDefNone.DiseaseDefNum,diseaseDefNoneDuplicate.DiseaseDefNum);
			Assert.IsTrue(result.IsProblemsIndicateNoneMoved);
			Assert.AreEqual(diseaseDefNone.DiseaseDefNum.ToString(),Scalar("SELECT ValueString FROM preference WHERE PrefName='ProblemsIndicateNone'"));
			Assert.AreEqual(diseaseDefNone.DiseaseDefNum,PrefC.GetLong(PrefName.ProblemsIndicateNone));
			//A merge of two other problems leaves the preference alone.
			DiseaseDef diseaseDefA=InsertDef("Asthma");
			DiseaseDef diseaseDefB=InsertDef("Asthma, mild");
			result=DiseaseDefs.Merge(diseaseDefA.DiseaseDefNum,diseaseDefB.DiseaseDefNum);
			Assert.IsFalse(result.IsProblemsIndicateNoneMoved);
			Assert.AreEqual(diseaseDefNone.DiseaseDefNum.ToString(),Scalar("SELECT ValueString FROM preference WHERE PrefName='ProblemsIndicateNone'"));
		}

		[TestMethod]
		public void Merge_CountsFormFieldsThatStillNameTheDiscardedProblem() {
			(DiseaseDef keep,DiseaseDef discard,DiseaseDef other)=FillSyntheticOffice();
			InsertSheetFieldDef("problem:Diabetes II");
			InsertSheetFieldDef("problem:Diabetes type 2");
			InsertSheetFieldDef("problem:DIABETES II");//Sheets compare names exactly, so this one never matched the discarded problem.
			InsertEFormFieldDef("problem:Diabetes II");
			InsertEFormFieldDef("problem:Asthma");
			DiseaseDefMergeResult result=DiseaseDefs.Merge(keep.DiseaseDefNum,discard.DiseaseDefNum);
			Assert.AreEqual(1,result.CountSheetFieldDefsNamingDiscarded);
			Assert.AreEqual(1,result.CountEFormFieldDefsNamingDiscarded);
			Assert.AreEqual(1,Count("sheetfielddef","BINARY FieldName='problem:Diabetes II'"),"Form definitions are counted, not changed.");
			Assert.AreEqual(1,Count("sheetfielddef","BINARY FieldName='problem:DIABETES II'"));
			Assert.AreEqual(1,Count("eformfielddef","BINARY DbLink='problem:Diabetes II'"));
		}

		[TestMethod]
		public void Merge_SameNameDuplicatesNeedNoFormWarning() {
			DiseaseDef diseaseDefKeep=InsertDef("Hypertension");
			DiseaseDef diseaseDefDiscard=InsertDef("Hypertension");
			InsertSheetFieldDef("problem:Hypertension");
			DiseaseDefMergeResult result=DiseaseDefs.Merge(diseaseDefKeep.DiseaseDefNum,diseaseDefDiscard.DiseaseDefNum);
			Assert.AreEqual(0,result.CountSheetFieldDefsNamingDiscarded,"The field still matches the kept problem by name.");
		}

		[TestMethod]
		public void Merge_RefusesTheSameProblemOrAMissingOne() {
			DiseaseDef diseaseDef=InsertDef("Asthma");
			InsertProblem(201,diseaseDef.DiseaseDefNum);
			AssertThrowsApplicationException(() => DiseaseDefs.Merge(diseaseDef.DiseaseDefNum,diseaseDef.DiseaseDefNum));
			AssertThrowsApplicationException(() => DiseaseDefs.Merge(diseaseDef.DiseaseDefNum,diseaseDef.DiseaseDefNum+1000));
			AssertThrowsApplicationException(() => DiseaseDefs.Merge(diseaseDef.DiseaseDefNum+1000,diseaseDef.DiseaseDefNum));
			Assert.IsNotNull(DiseaseDefs.GetOne(diseaseDef.DiseaseDefNum),"Nothing is deleted when the merge is refused.");
			Assert.AreEqual(1,Count("disease","DiseaseDefNum="+diseaseDef.DiseaseDefNum));
		}

		[TestMethod]
		public void Merge_AfterAnInterruptedMerge_Finishes() {
			//MyISAM has no transactions. Simulate a merge that moved the patient problems and then stopped: running it again finishes the job.
			(DiseaseDef keep,DiseaseDef discard,DiseaseDef other)=FillSyntheticOffice();
			DataCore.NonQ("UPDATE disease SET DiseaseDefNum="+keep.DiseaseDefNum+" WHERE DiseaseDefNum="+discard.DiseaseDefNum);
			DiseaseDefMergeResult result=DiseaseDefs.Merge(keep.DiseaseDefNum,discard.DiseaseDefNum);
			Assert.AreEqual(0,result.CountProblemsMoved);
			Assert.AreEqual(1,result.CountRxAlertsMoved);
			Assert.AreEqual(0,Count("diseasedef","DiseaseDefNum="+discard.DiseaseDefNum));
			Assert.AreEqual(0,Count("rxalert","DiseaseDefNum="+discard.DiseaseDefNum));
		}

		[TestMethod]
		public void GetPatientCount_CountsPatientsNotRows() {
			(DiseaseDef keep,DiseaseDef discard,DiseaseDef other)=FillSyntheticOffice();
			Assert.AreEqual(3,DiseaseDefs.GetPatientCount(discard.DiseaseDefNum));
			Assert.AreEqual(2,DiseaseDefs.GetPatientCount(keep.DiseaseDefNum));
			InsertProblem(104,keep.DiseaseDefNum,"second row");
			Assert.AreEqual(2,DiseaseDefs.GetPatientCount(keep.DiseaseDefNum));
			Assert.AreEqual(0,DiseaseDefs.GetPatientCount(0));
		}

		[TestMethod]
		public void Merge_ThenCacheRefresh_ClosesTheOrderGap() {
			(DiseaseDef keep,DiseaseDef discard,DiseaseDef other)=FillSyntheticOffice();
			DiseaseDefs.Merge(other.DiseaseDefNum,discard.DiseaseDefNum);
			//What the Problems window does after a merge.
			DiseaseDefs.RefreshCache();
			DiseaseDefs.FixItemOrders();
			List<DiseaseDef> listDiseaseDefs=DiseaseDefs.GetDeepCopy();
			CollectionAssert.AreEqual(new[] { 0,1 },listDiseaseDefs.Select(x => x.ItemOrder).ToArray());
			CollectionAssert.AreEqual(new[] { keep.DiseaseDefNum,other.DiseaseDefNum },listDiseaseDefs.Select(x => x.DiseaseDefNum).ToArray());
		}
	}
}
