// Rules that need no database: the Allergy Setup search (v26_1-setup-allergy-search), the text of the Problem merge (v26_1-lists-problem-merge-keep)
// and the shape of the AllergyWithInvalidAllergyDef check (v26_1-tools-dbm-missing-allergydefs). Synthetic values only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2MEDTests {
	[TestClass]
	public class AllergySearchTests {
		private static AllergyDef Def(long allergyDefNum,string description,bool isHidden=false) {
			return new AllergyDef { AllergyDefNum=allergyDefNum,Description=description,IsHidden=isHidden,UniiCode="" };
		}

		private static List<AllergyDef> SyntheticList() {
			//In the order AllergyDefs.GetAll returns them: by description.
			return new List<AllergyDef> {
				Def(1,"Amoxicillin"),
				Def(2,"Latex"),
				Def(3,"Penicillin",isHidden:true),
				Def(4,"Penicillin VK"),
				Def(5,"Shellfish"),
			};
		}

		[TestMethod]
		public void IsSearchMatch_ContainsIgnoringCase() {
			AllergyDef allergyDef=Def(4,"Penicillin VK");
			Assert.IsTrue(AllergyDefs.IsSearchMatch(allergyDef,"PENI"));
			Assert.IsTrue(AllergyDefs.IsSearchMatch(allergyDef,"cillin v"));
			Assert.IsTrue(AllergyDefs.IsSearchMatch(allergyDef,"  vk  "),"Spaces around the search text are ignored.");
			Assert.IsFalse(AllergyDefs.IsSearchMatch(allergyDef,"latex"));
			Assert.IsFalse(AllergyDefs.IsSearchMatch(allergyDef,"vk penicillin"),"A contains match, not a word match.");
		}

		[TestMethod]
		public void IsSearchMatch_BlankMatchesAll_NullDefinitionMatchesNothing() {
			Assert.IsTrue(AllergyDefs.IsSearchMatch(Def(1,"Latex"),""));
			Assert.IsTrue(AllergyDefs.IsSearchMatch(Def(1,"Latex"),null));
			Assert.IsTrue(AllergyDefs.IsSearchMatch(Def(1,"Latex"),"   "));
			Assert.IsTrue(AllergyDefs.IsSearchMatch(Def(1,null),""));
			Assert.IsFalse(AllergyDefs.IsSearchMatch(Def(1,null),"a"));
			Assert.IsFalse(AllergyDefs.IsSearchMatch(null,""));
		}

		[TestMethod]
		public void FilterForSetup_CombinesSearchWithShowHidden() {
			List<AllergyDef> listAllergyDefs=SyntheticList();
			CollectionAssert.AreEqual(new long[] { 1,2,4,5 },AllergyDefs.FilterForSetup(listAllergyDefs,"",false).Select(x => x.AllergyDefNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 1,2,3,4,5 },AllergyDefs.FilterForSetup(listAllergyDefs,"",true).Select(x => x.AllergyDefNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 4 },AllergyDefs.FilterForSetup(listAllergyDefs,"penicillin",false).Select(x => x.AllergyDefNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 3,4 },AllergyDefs.FilterForSetup(listAllergyDefs,"penicillin",true).Select(x => x.AllergyDefNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 1,3,4 },AllergyDefs.FilterForSetup(listAllergyDefs,"ICILLIN",true).Select(x => x.AllergyDefNum).ToArray(),
				"Matches keep the order of the list.");
			Assert.AreEqual(0,AllergyDefs.FilterForSetup(listAllergyDefs,"peanut",true).Count);
			Assert.AreEqual(5,listAllergyDefs.Count,"The list passed in is not changed.");
			Assert.AreEqual(0,AllergyDefs.FilterForSetup(null,"",true).Count);
		}
	}

	[TestClass]
	public class ProblemMergeRuleTests {
		private static DiseaseDefMergeResult Result() {
			DiseaseDefMergeResult result=new DiseaseDefMergeResult();
			result.DiseaseDefNumKept=11;
			result.DiseaseNameKept="Diabetes type 2";
			result.DiseaseDefNumDiscarded=12;
			result.DiseaseNameDiscarded="Diabetes II";
			result.CountProblemsMoved=7;
			return result;
		}

		[TestMethod]
		public void ReplaceNumInSpacedList_ReplacesAndKeepsEachNumberOnce() {
			Assert.AreEqual(" 5  9  7 ",DiseaseDefs.ReplaceNumInSpacedList(" 5  12  7 ",12,9));
			Assert.AreEqual(" 9 ",DiseaseDefs.ReplaceNumInSpacedList(" 12 ",12,9));
			//The trigger held both problems: the kept number stays once, in its first place.
			Assert.AreEqual(" 9  5 ",DiseaseDefs.ReplaceNumInSpacedList(" 9  5  12 ",12,9));
			Assert.AreEqual(" 9  5 ",DiseaseDefs.ReplaceNumInSpacedList(" 12  5  9 ",12,9));
		}

		[TestMethod]
		public void ReplaceNumInSpacedList_LeavesOtherListsAlone() {
			Assert.AreEqual(" 5  120  7 ",DiseaseDefs.ReplaceNumInSpacedList(" 5  120  7 ",12,9),"112 or 120 is not 12.");
			Assert.AreEqual(" 112 ",DiseaseDefs.ReplaceNumInSpacedList(" 112 ",12,9));
			Assert.AreEqual("",DiseaseDefs.ReplaceNumInSpacedList("",12,9));
			Assert.IsNull(DiseaseDefs.ReplaceNumInSpacedList(null,12,9));
		}

		[TestMethod]
		public void GetMergeChoiceText_ShowsNameCodesHiddenAndPatients() {
			DiseaseDef diseaseDef=new DiseaseDef { DiseaseDefNum=11,DiseaseName="Diabetes type 2",Icd10Code="E11.9",SnomedCode="44054006",ICD9Code="250.00" };
			string text=DiseaseDefs.GetMergeChoiceText(diseaseDef,3);
			StringAssert.StartsWith(text,"Diabetes type 2");
			StringAssert.Contains(text,"ICD-10 E11.9");
			StringAssert.Contains(text,"SNOMED CT 44054006");
			StringAssert.Contains(text,"3 patients");
			Assert.IsFalse(text.Contains("hidden"));
			Assert.IsFalse(text.Contains("250.00"),"The window shows the ICD-10 and SNOMED CT codes only.");
			diseaseDef.IsHidden=true;
			StringAssert.Contains(DiseaseDefs.GetMergeChoiceText(diseaseDef,1),"hidden");
			StringAssert.Contains(DiseaseDefs.GetMergeChoiceText(diseaseDef,1),"1 patient");
			DiseaseDef diseaseDefNoCodes=new DiseaseDef { DiseaseName="Asthma",Icd10Code="",SnomedCode=null,ICD9Code="" };
			string textNoCodes=DiseaseDefs.GetMergeChoiceText(diseaseDefNoCodes,0);
			Assert.IsFalse(textNoCodes.Contains("("),"No empty code list.");
			StringAssert.Contains(textNoCodes,"0 patients");
		}

		[TestMethod]
		public void GetMergeLogText_NamesBothProblems() {
			string text=DiseaseDefs.GetMergeLogText(Result());
			StringAssert.StartsWith(text,"Problem \"Diabetes II\" (#12) merged into \"Diabetes type 2\" (#11).");
			StringAssert.Contains(text,"7");
		}

		[TestMethod]
		public void GetMergeSummaryText_WarnsOnlyAboutWhatHappened() {
			DiseaseDefMergeResult result=Result();
			string text=DiseaseDefs.GetMergeSummaryText(result);
			StringAssert.Contains(text,"\"Diabetes II\"");
			StringAssert.Contains(text,"\"Diabetes type 2\"");
			Assert.IsFalse(text.Contains("twice"));
			Assert.IsFalse(text.Contains("eForm"));
			Assert.IsFalse(text.Contains("indicates"));
			result.CountPatientsWithBoth=2;
			result.CountSheetFieldDefsNamingDiscarded=1;
			result.CountEFormFieldDefsNamingDiscarded=2;
			result.IsProblemsIndicateNoneMoved=true;
			result.CountCdsTriggersMoved=1;
			text=DiseaseDefs.GetMergeSummaryText(result);
			StringAssert.Contains(text,"twice");
			StringAssert.Contains(text,"Sheet and eForm fields");
			StringAssert.Contains(text,": 3");
			StringAssert.Contains(text,"indicates");
			StringAssert.Contains(text,"CDS triggers moved: 1");
		}

		[TestMethod]
		public void Merge_IsMiddleTierSafe() {
			//Meth.GetObject sends the result across Middle Tier; it needs a public type with a parameterless constructor and public fields.
			Type type=typeof(DiseaseDefMergeResult);
			Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
			Assert.IsTrue(type.IsSerializable);
			MethodInfo methodInfo=typeof(DiseaseDefs).GetMethod(nameof(DiseaseDefs.Merge));
			Assert.AreEqual(typeof(DiseaseDefMergeResult),methodInfo.ReturnType);
		}
	}

	[TestClass]
	public class AllergyDbmShapeTests {
		[TestMethod]
		public void AllergyWithInvalidAllergyDef_IsADbmMethod() {
			MethodInfo methodInfo=typeof(DatabaseMaintenances).GetMethod("AllergyWithInvalidAllergyDef");
			Assert.IsNotNull(methodInfo,"The 26.1 name of the check.");
			DbmMethodAttr dbmMethodAttr=(DbmMethodAttr)Attribute.GetCustomAttribute(methodInfo,typeof(DbmMethodAttr));
			Assert.IsNotNull(dbmMethodAttr,"Database Maintenance lists methods that carry DbmMethodAttr.");
			Assert.IsTrue(dbmMethodAttr.IsReplicationUnsafe,"Fix may insert an allergy definition.");
			Assert.IsFalse(dbmMethodAttr.IsCanada);
			Assert.AreEqual(typeof(string),methodInfo.ReturnType);
			CollectionAssert.AreEqual(new[] { typeof(bool),typeof(DbmMode) },methodInfo.GetParameters().Select(x => x.ParameterType).ToArray());
		}

		[TestMethod]
		public void UnknownAllergyName_FollowsUnknownProblem() {
			Assert.AreEqual("UNKNOWN ALLERGY",AllergyDefs.DESCRIPTION_UNKNOWN);
		}
	}
}
