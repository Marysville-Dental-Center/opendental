// Database tests for the database maintenance check AllergyWithInvalidAllergyDef (v26_1-tools-dbm-missing-allergydefs, package M2-MED) and the
// allergy definition list the Allergy Setup search filters, on a scratch MariaDB database with the Open Dental 26.1.70 schema and synthetic rows
// only. Skipped (inconclusive) when no server answers at 127.0.0.1:3306 as root with a blank password, or when M2MED_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2MEDTests {
	[TestClass]
	public class AllergyDbmDbTests {
		private static readonly string _dbName=ScratchDb.Name("allergy");
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
			foreach(string table in new[] { "allergy","allergydef","dbmlog" }) {
				DataCore.NonQ("DELETE FROM "+table);
			}
		}

		#region Synthetic data
		private static AllergyDef InsertDef(string description,bool isHidden=false) {
			AllergyDef allergyDef=new AllergyDef();
			allergyDef.Description=description;
			allergyDef.IsHidden=isHidden;
			allergyDef.UniiCode="";
			AllergyDefs.Insert(allergyDef);
			return allergyDef;
		}

		private static Allergy InsertAllergy(long patNum,long allergyDefNum,string reaction) {
			Allergy allergy=new Allergy();
			allergy.PatNum=patNum;
			allergy.AllergyDefNum=allergyDefNum;
			allergy.Reaction=reaction;
			allergy.StatusIsActive=true;
			allergy.SnomedReaction="";
			Allergies.Insert(allergy);
			return allergy;
		}

		private static long Count(string table,string where="1") {
			return PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM "+table+" WHERE "+where));
		}

		///<summary>Latex is a real definition. Patients 301 and 302 have allergies whose definitions were deleted, and 303 has one with AllergyDefNum 0.</summary>
		private static (AllergyDef latex,Allergy valid,List<Allergy> listOrphans) FillSyntheticOffice() {
			AllergyDef allergyDefLatex=InsertDef("Latex");
			AllergyDef allergyDefGone=InsertDef("Sulfa");
			Allergy allergyValid=InsertAllergy(300,allergyDefLatex.AllergyDefNum,"Rash");
			List<Allergy> listOrphans=new List<Allergy> {
				InsertAllergy(301,allergyDefGone.AllergyDefNum,"Hives"),
				InsertAllergy(302,allergyDefGone.AllergyDefNum,"Swelling"),
				InsertAllergy(303,0,"Unknown reaction"),
			};
			DataCore.NonQ("DELETE FROM allergydef WHERE AllergyDefNum="+allergyDefGone.AllergyDefNum);
			return (allergyDefLatex,allergyValid,listOrphans);
		}
		#endregion Synthetic data

		[TestMethod]
		public void Check_CountsOrphansAndChangesNothing() {
			(AllergyDef latex,Allergy valid,List<Allergy> listOrphans)=FillSyntheticOffice();
			string log=DatabaseMaintenances.AllergyWithInvalidAllergyDef(verbose:false,DbmMode.Check);
			StringAssert.Contains(log,": 3");
			Assert.AreEqual(1,Count("allergydef"),"Check inserts nothing.");
			Assert.AreEqual(2,Count("allergy","NOT EXISTS(SELECT 1 FROM allergydef WHERE allergydef.AllergyDefNum=allergy.AllergyDefNum) AND AllergyDefNum!=0"));
			Assert.AreEqual(0,Count("dbmlog"));
			CollectionAssert.AreEqual(listOrphans.Select(x => x.AllergyNum).ToArray(),Allergies.GetWithInvalidAllergyDef().Select(x => x.AllergyNum).ToArray());
		}

		[TestMethod]
		public void Check_IsQuietWhenNothingIsWrong() {
			AllergyDef allergyDef=InsertDef("Latex");
			InsertAllergy(300,allergyDef.AllergyDefNum,"Rash");
			Assert.AreEqual("",DatabaseMaintenances.AllergyWithInvalidAllergyDef(verbose:false,DbmMode.Check));
			Assert.AreEqual("",DatabaseMaintenances.AllergyWithInvalidAllergyDef(verbose:false,DbmMode.Fix));
			Assert.AreEqual(1,Count("allergydef"),"Fix with nothing to fix creates no UNKNOWN ALLERGY.");
			StringAssert.Contains(DatabaseMaintenances.AllergyWithInvalidAllergyDef(verbose:true,DbmMode.Check),": 0");
		}

		[TestMethod]
		public void Fix_AttachesOrphansToAVisibleUnknownAllergy() {
			(AllergyDef latex,Allergy valid,List<Allergy> listOrphans)=FillSyntheticOffice();
			string log=DatabaseMaintenances.AllergyWithInvalidAllergyDef(verbose:false,DbmMode.Fix);
			StringAssert.Contains(log,"UNKNOWN ALLERGY");
			List<AllergyDef> listUnknown=AllergyDefs.GetAll(includeHidden:true).FindAll(x => x.Description==AllergyDefs.DESCRIPTION_UNKNOWN);
			Assert.AreEqual(1,listUnknown.Count);
			Assert.IsFalse(listUnknown[0].IsHidden);
			for(int i=0;i<listOrphans.Count;i++) {
				Allergy allergy=Allergies.GetOne(listOrphans[i].AllergyNum);
				Assert.AreEqual(listUnknown[0].AllergyDefNum,allergy.AllergyDefNum);
				Assert.AreEqual(listOrphans[i].PatNum,allergy.PatNum,"The patient keeps the allergy.");
				Assert.AreEqual(listOrphans[i].Reaction,allergy.Reaction);
				Assert.IsTrue(allergy.StatusIsActive);
			}
			Assert.AreEqual(latex.AllergyDefNum,Allergies.GetOne(valid.AllergyNum).AllergyDefNum,"Valid allergies are not touched.");
			Assert.AreEqual(3,Count("dbmlog","MethodName='AllergyWithInvalidAllergyDef'"));
			Assert.AreEqual(1,Count("dbmlog","FKey=301 AND FKeyType="+(int)DbmLogFKeyType.Patient+" AND ActionType="+(int)DbmLogActionType.Update));
			Assert.AreEqual("",DatabaseMaintenances.AllergyWithInvalidAllergyDef(verbose:false,DbmMode.Check),"Nothing left to fix.");
		}

		[TestMethod]
		public void Fix_ReusesAnExistingUnknownAllergy_PreferringAVisibleOne() {
			FillSyntheticOffice();
			AllergyDef allergyDefHidden=InsertDef("UNKNOWN ALLERGY",isHidden:true);
			AllergyDef allergyDefVisible=InsertDef("Unknown Allergy");
			DatabaseMaintenances.AllergyWithInvalidAllergyDef(verbose:false,DbmMode.Fix);
			Assert.AreEqual(3,Count("allergy","AllergyDefNum="+allergyDefVisible.AllergyDefNum));
			Assert.AreEqual(0,Count("allergy","AllergyDefNum="+allergyDefHidden.AllergyDefNum));
			Assert.AreEqual(3,Count("allergydef"),"No new definition.");
		}

		[TestMethod]
		public void GetOrInsertUnknown_UsesAHiddenOneWhenThatIsAllThereIs() {
			AllergyDef allergyDefHidden=InsertDef("UNKNOWN ALLERGY",isHidden:true);
			Assert.AreEqual(allergyDefHidden.AllergyDefNum,AllergyDefs.GetOrInsertUnknown());
			DataCore.NonQ("DELETE FROM allergydef");
			long allergyDefNum=AllergyDefs.GetOrInsertUnknown();
			AllergyDef allergyDef=AllergyDefs.GetOne(allergyDefNum);
			Assert.AreEqual("UNKNOWN ALLERGY",allergyDef.Description);
			Assert.IsFalse(allergyDef.IsHidden);
			Assert.AreEqual(allergyDefNum,AllergyDefs.GetOrInsertUnknown(),"Found again, not inserted twice.");
			Assert.AreEqual(1,Count("allergydef"));
		}

		[TestMethod]
		public void SetAllergyDefNum_ChangesOnlyTheGivenRows() {
			AllergyDef allergyDefA=InsertDef("Latex");
			AllergyDef allergyDefB=InsertDef("Peanuts");
			Allergy allergy1=InsertAllergy(401,allergyDefA.AllergyDefNum,"Rash");
			Allergy allergy2=InsertAllergy(402,allergyDefA.AllergyDefNum,"Rash");
			Assert.AreEqual(1,Allergies.SetAllergyDefNum(new List<long> { allergy2.AllergyNum },allergyDefB.AllergyDefNum));
			Assert.AreEqual(allergyDefA.AllergyDefNum,Allergies.GetOne(allergy1.AllergyNum).AllergyDefNum);
			Assert.AreEqual(allergyDefB.AllergyDefNum,Allergies.GetOne(allergy2.AllergyNum).AllergyDefNum);
			Assert.AreEqual(0,Allergies.SetAllergyDefNum(new List<long>(),allergyDefB.AllergyDefNum));
			Assert.AreEqual(0,Allergies.SetAllergyDefNum(null,allergyDefB.AllergyDefNum));
		}

		[TestMethod]
		public void SetupSearch_FiltersTheDatabaseList() {
			//What Allergy Setup does: read every definition once, then filter in memory as the user types.
			InsertDef("Penicillin");
			InsertDef("Amoxicillin");
			InsertDef("Latex");
			InsertDef("Penicillin G",isHidden:true);
			List<AllergyDef> listAll=AllergyDefs.GetAll(includeHidden:true);
			CollectionAssert.AreEqual(new[] { "Amoxicillin","Penicillin" },
				AllergyDefs.FilterForSetup(listAll,"cillin",includeHidden:false).Select(x => x.Description).ToArray());
			CollectionAssert.AreEqual(new[] { "Amoxicillin","Penicillin","Penicillin G" },
				AllergyDefs.FilterForSetup(listAll,"CILLIN",includeHidden:true).Select(x => x.Description).ToArray());
			CollectionAssert.AreEqual(AllergyDefs.GetAll(includeHidden:false).Select(x => x.AllergyDefNum).ToArray(),
				AllergyDefs.FilterForSetup(listAll,"",includeHidden:false).Select(x => x.AllergyDefNum).ToArray(),
				"A blank search shows what the window showed before 26.1.");
		}
	}
}
