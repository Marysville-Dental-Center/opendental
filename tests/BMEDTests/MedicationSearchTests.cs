// v24_4-lists-medication-search: better search in the Medications list (package B-MED). Synthetic medication names only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BMEDTests {
	///<summary>The search rule, without a database.</summary>
	[TestClass]
	public class MedicationSearchRuleTests {
		[TestInitialize]
		public void TestInitialize() {
			BMedDb.UseUsEnglish();
		}

		private static Medication Med(long medicationNum,string medName,long genericNum=0,long rxCui=0) {
			Medication medication=new Medication();
			medication.MedicationNum=medicationNum;
			medication.MedName=medName;
			medication.GenericNum=(genericNum==0 ? medicationNum : genericNum);
			medication.RxCui=rxCui;
			medication.Notes="";
			return medication;
		}

		[TestMethod]
		public void BlankSearch_MatchesEverything() {
			Medication medication=Med(1,"Zorvatin HCl");
			Assert.IsTrue(Medications.IsSearchMatchWords(medication,"",null));
			Assert.IsTrue(Medications.IsSearchMatchWords(medication,"",""));
			Assert.IsTrue(Medications.IsSearchMatchWords(medication,"","   "));
			Assert.IsFalse(Medications.IsSearchMatchWords(null,"","zor"));
		}

		[TestMethod]
		public void Words_MatchInAnyOrder_IgnoringCase() {
			Medication medication=Med(1,"Zorvatin HCl");
			Assert.IsTrue(Medications.IsSearchMatchWords(medication,"","hcl zorvatin"));
			Assert.IsTrue(Medications.IsSearchMatchWords(medication,"","ZORVATIN"));
			Assert.IsTrue(Medications.IsSearchMatchWords(medication,"","zorv  hc"),"Parts of words, several spaces.");
			Assert.IsTrue(Medications.IsSearchMatchWords(medication,"","hcl,zorvatin"),"Commas separate words too.");
			Assert.IsFalse(Medications.IsSearchMatchWords(medication,"","zorvatin sodium"),"Every word must appear.");
			Assert.IsFalse(Medications.IsSearchMatchWords(Med(2,null),"","a"));
		}

		[TestMethod]
		public void Brand_MatchesByItsGenericName() {
			Medication medicationBrand=Med(2,"Quellidex",genericNum:1);
			Assert.IsTrue(Medications.IsSearchMatchWords(medicationBrand,"Zorvatin HCl","zorvatin"));
			Assert.IsTrue(Medications.IsSearchMatchWords(medicationBrand,"Zorvatin HCl","hcl zorvatin"));
			Assert.IsTrue(Medications.IsSearchMatchWords(medicationBrand,"Zorvatin HCl","quellidex zorvatin"),"Brand and generic words together.");
			Assert.IsTrue(Medications.IsSearchMatchWords(medicationBrand,"Zorvatin HCl","quelli"));
			Assert.IsFalse(Medications.IsSearchMatchWords(medicationBrand,"Zorvatin HCl","plavomide"));
			//A generic is not found by the name of one of its brands.
			Assert.IsFalse(Medications.IsSearchMatchWords(Med(1,"Zorvatin HCl"),"","quellidex"));
		}

		[TestMethod]
		public void AllDigits_AlsoMatchTheRxNormCode() {
			Medication medication=Med(1,"Zorvatin",rxCui:9912345);
			Assert.IsTrue(Medications.IsSearchMatchWords(medication,"","9912345"));
			Assert.IsTrue(Medications.IsSearchMatchWords(medication,""," 99123 "),"The code starts with the digits typed so far.");
			Assert.IsFalse(Medications.IsSearchMatchWords(medication,"","12345"),"Only the start of the code.");
			Assert.IsFalse(Medications.IsSearchMatchWords(Med(2,"Zorvatin"),"","0"),"No code (0) never matches.");
			//Digits that are part of the name still match the name.
			Assert.IsTrue(Medications.IsSearchMatchWords(Med(3,"Zorvatin 500 mg"),"","500"));
			Assert.IsFalse(Medications.IsSearchMatchWords(Med(4,"Zorvatin",rxCui:9912345),"","9912345 zorvatin"),
				"The code is only searched when the whole text is digits; otherwise every word must be in the name.");
		}

		[TestMethod]
		public void GetSearchWords_SplitsOnSpacesTabsAndCommas() {
			CollectionAssert.AreEqual(new[] { "a","bc","d" },Medications.GetSearchWords(" A \tbc,d ").ToArray());
			Assert.AreEqual(0,Medications.GetSearchWords(null).Count);
			Assert.AreEqual(0,Medications.GetSearchWords("  ").Count);
		}

		[TestMethod]
		public void OldContainsMatch_IsUnchangedForExportAndImport() {
			//Medications.GetList (export, import duplicate checks) keeps the plain "contains" match from A-MED.
			Medication medication=Med(1,"Zorvatin HCl");
			Assert.IsTrue(Medications.IsSearchMatch(medication,"VATIN H"));
			Assert.IsFalse(Medications.IsSearchMatch(medication,"hcl zorvatin"));
		}
	}

	///<summary>The search over the medication cache, against the scratch database.</summary>
	[TestClass]
	public class MedicationSearchDbTests {
		[TestInitialize]
		public void TestInitialize() {
			BMedDb.Begin();
		}

		///<summary>Inserts a synthetic medication.  A generic when medicationGeneric is null, otherwise a brand of it.</summary>
		private static Medication InsertMed(string medName,Medication medicationGeneric=null,bool isHidden=false,long rxCui=0) {
			Medication medication=new Medication();
			medication.MedName=medName;
			medication.Notes="";
			medication.IsHidden=isHidden;
			medication.RxCui=rxCui;
			medication.GenericNum=(medicationGeneric==null ? 0 : medicationGeneric.MedicationNum);
			Medications.Insert(medication);
			if(medicationGeneric==null) {
				medication.GenericNum=medication.MedicationNum;
				Medications.Update(medication);
			}
			return medication;
		}

		private static List<long> Search(string text,bool includeHidden=false) {
			return Medications.GetListForPicker(text,includeHidden).Select(x => x.MedicationNum).OrderBy(x => x).ToList();
		}

		[TestMethod]
		public void SearchingAGeneric_ListsItsBrands_AndHiddenStayOut() {
			string u=BMedDb.Unique();
			Medication medicationGeneric=InsertMed("Zorvatin"+u+" HCl");
			Medication medicationBrand=InsertMed("Quellidex"+u,medicationGeneric);
			Medication medicationBrandHidden=InsertMed("Pravolene"+u,medicationGeneric,isHidden:true);
			Medication medicationOther=InsertMed("Plavomide"+u);
			Medications.RefreshCache();
			CollectionAssert.AreEqual(new[] { medicationGeneric.MedicationNum,medicationBrand.MedicationNum },Search("hcl zorvatin"+u));
			CollectionAssert.AreEqual(new[] { medicationGeneric.MedicationNum,medicationBrand.MedicationNum,medicationBrandHidden.MedicationNum },
				Search("zorvatin"+u,includeHidden:true),"Show Hidden (and the merge tool) see the hidden brand.");
			CollectionAssert.AreEqual(new[] { medicationBrand.MedicationNum },Search("quellidex"+u+" zorvatin"));
			CollectionAssert.AreEqual(new[] { medicationOther.MedicationNum },Search(u.ToUpperInvariant()+" plavomide"));
		}

		[TestMethod]
		public void BrandOfAHiddenGeneric_IsStillFoundByTheGenericName() {
			string u=BMedDb.Unique();
			Medication medicationGeneric=InsertMed("Vexolan"+u,isHidden:true);
			Medication medicationBrand=InsertMed("Tirabel"+u,medicationGeneric);
			Medications.RefreshCache();
			CollectionAssert.AreEqual(new[] { medicationBrand.MedicationNum },Search("vexolan"+u));
		}

		[TestMethod]
		public void RxNormCode_IsSearchable() {
			string u=BMedDb.Unique();
			long rxCui=990000000+BMedDb.NextSyntheticNum()+7;//synthetic code, not a real RxNorm concept
			Medication medication=InsertMed("Corbitane"+u,rxCui:rxCui);
			Medications.RefreshCache();
			Assert.IsTrue(Search(rxCui.ToString()).Contains(medication.MedicationNum));
			Assert.IsFalse(Search(rxCui.ToString()+"9").Contains(medication.MedicationNum));
		}
	}
}
