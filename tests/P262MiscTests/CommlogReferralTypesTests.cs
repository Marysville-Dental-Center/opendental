// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace P262MiscTests {
	///<summary>Commlog Referral Types (Open Dental 26.2 item 39), on objects only: the default type of a new referral commlog, the types offered, the
	///names shown.</summary>
	[TestClass]
	public class CommlogReferralTypesTests {
		private static Def DefOf(long defNum,string name,int itemOrder,bool isHidden=false,DefCat defCat=DefCat.CommLogReferralTypes) {
			return new Def { DefNum=defNum,Category=defCat,ItemName=name,ItemOrder=itemOrder,IsHidden=isHidden,ItemValue="" };
		}

		private static List<Def> Defs3() {
			//Out of list order on purpose; 11 is hidden; 99 is a patient commlog type.
			return new List<Def> { DefOf(12,"Phone call",2),DefOf(11,"Old",0,isHidden:true),DefOf(10,"Letter",1),DefOf(99,"Misc",0,defCat:DefCat.CommLogTypes) };
		}

		[TestMethod]
		public void DefCat57_IsOpenDentalsValue() {
			Assert.AreEqual(57,(int)DefCat.CommLogReferralTypes);
			Assert.AreEqual("Commlog Referral Types",DefCat.CommLogReferralTypes.GetDescription());
		}

		[TestMethod]
		public void DefaultType_FirstVisibleInListOrder() {
			Assert.AreEqual(10,CommlogReferralTypes.GetDefaultCommType(Defs3()),"Letter: item order 1, after the hidden one.");
		}

		[TestMethod]
		public void DefaultType_NoneWhenCategoryEmptyOrAllHidden() {
			Assert.AreEqual(0,CommlogReferralTypes.GetDefaultCommType(new List<Def>()));
			Assert.AreEqual(0,CommlogReferralTypes.GetDefaultCommType(null));
			Assert.AreEqual(0,CommlogReferralTypes.GetDefaultCommType(new List<Def> { DefOf(11,"Old",0,isHidden:true) }));
			Assert.AreEqual(0,CommlogReferralTypes.GetDefaultCommType(new List<Def> { DefOf(99,"Misc",0,defCat:DefCat.CommLogTypes) }),
				"Patient commlog types are never used for referral commlogs.");
		}

		[TestMethod]
		public void PickList_VisibleTypesInOrder_PlusCurrentHiddenOne() {
			CollectionAssert.AreEqual(new long[] { 10,12 },CommlogReferralTypes.GetPickList(0,Defs3()).Select(x => x.DefNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 10,12 },CommlogReferralTypes.GetPickList(12,Defs3()).Select(x => x.DefNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 11,10,12 },CommlogReferralTypes.GetPickList(11,Defs3()).Select(x => x.DefNum).ToArray(),
				"A commlog with a hidden type keeps it.");
			CollectionAssert.AreEqual(new long[] { 10,12 },CommlogReferralTypes.GetPickList(99,Defs3()).Select(x => x.DefNum).ToArray(),
				"Another category's definition is never offered.");
		}

		[TestMethod]
		public void TypeName() {
			Assert.AreEqual("",CommlogReferralTypes.GetTypeName(0,Defs3()),"Referral commlogs made before 26.2 have no type.");
			Assert.AreEqual("Letter",CommlogReferralTypes.GetTypeName(10,Defs3()));
			Assert.AreEqual("Old (hidden)",CommlogReferralTypes.GetTypeName(11,Defs3()));
			Assert.AreEqual("",CommlogReferralTypes.GetTypeName(99,Defs3()));
			Assert.AreEqual("",CommlogReferralTypes.GetTypeName(5000,Defs3()));
		}

		[TestMethod]
		public void KnownType() {
			Assert.IsTrue(CommlogReferralTypes.IsKnownCommType(0,Defs3()));
			Assert.IsTrue(CommlogReferralTypes.IsKnownCommType(11,Defs3()));
			Assert.IsFalse(CommlogReferralTypes.IsKnownCommType(99,Defs3()));
			Assert.IsFalse(CommlogReferralTypes.IsKnownCommType(5000,null));
		}
	}

	///<summary>Commlog Referral Types on the scratch database: the cache, the in-use check of the Definitions window, and a referral commlog keeping its
	///type.</summary>
	[TestClass]
	public class CommlogReferralTypesDbTests {
		[TestInitialize]
		public void Init() {
			MiscDb.Begin();
		}

		[TestMethod]
		public void NewDatabase_HasNoReferralTypes() {
			//No definition ships with this program and no migration step adds one; the office adds its own in Setup, Definitions.
			Assert.AreEqual("0",MiscDb.CountReferralTypesAtStart);
			Assert.AreEqual(0,CommlogReferralTypes.GetDefaultCommType(new List<Def>()),"With none, a new referral commlog has no type, as before 26.2.");
		}

		[TestMethod]
		public void Cache_DefaultIsFirstVisible() {
			DefT.DeleteAllForCategory(DefCat.CommLogReferralTypes);
			try {
				Def defHidden=DefT.CreateDefinition(DefCat.CommLogReferralTypes,"P262MiscHidden",isHidden:true);
				Def defCall=DefT.CreateDefinition(DefCat.CommLogReferralTypes,"P262MiscCall",itemColor:Color.LightBlue);
				DefT.CreateDefinition(DefCat.CommLogReferralTypes,"P262MiscLetter");
				Assert.AreEqual(3,CommlogReferralTypes.GetDefsAll().Count,"Hidden ones are read too.");
				Assert.AreEqual(defCall.DefNum,CommlogReferralTypes.GetDefaultCommType());
				Assert.AreEqual(2,CommlogReferralTypes.GetPickList(0,CommlogReferralTypes.GetDefsAll()).Count);
				Assert.AreEqual(3,CommlogReferralTypes.GetPickList(defHidden.DefNum,CommlogReferralTypes.GetDefsAll()).Count);
			}
			finally {
				DefT.DeleteAllForCategory(DefCat.CommLogReferralTypes);
			}
		}

		[TestMethod]
		public void InUse_OnlyReferralCommlogsCount() {
			Def defUsed=DefT.CreateDefinition(DefCat.CommLogReferralTypes,"P262MiscUsed");
			Def defUnused=DefT.CreateDefinition(DefCat.CommLogReferralTypes,"P262MiscUnused");
			Def defOnPatientCommlog=DefT.CreateDefinition(DefCat.CommLogReferralTypes,"P262MiscOnPatient");
			Referral referral=ReferralT.CreateReferralWithoutRefAttach(0);
			Commlog commlogReferral=new Commlog { ReferralNum=referral.ReferralNum,CommType=defUsed.DefNum,CommDateTime=DateTime.Now,Note="Called the office",
				UserNum=Security.CurUser.UserNum };
			Commlogs.Insert(commlogReferral);
			Patient patient=PatientT.CreatePatient(suffix:"RefType");
			Commlogs.Insert(new Commlog { PatNum=patient.PatNum,CommType=defOnPatientCommlog.DefNum,CommDateTime=DateTime.Now,Note="Not a referral commlog",
				UserNum=Security.CurUser.UserNum });
			Assert.IsTrue(Defs.IsDefinitionInUse(defUsed));
			Assert.IsFalse(Defs.IsDefinitionInUse(defUnused));
			Assert.IsFalse(Defs.IsDefinitionInUse(defOnPatientCommlog),"Only commlogs with a ReferralNum use referral types.");
			Commlog commlogRead=Commlogs.GetForReferral(referral.ReferralNum).Single();
			Assert.AreEqual(defUsed.DefNum,commlogRead.CommType,"The type is stored with the referral commlog.");
			Assert.AreEqual(0,commlogRead.PatNum);
		}
	}
}
