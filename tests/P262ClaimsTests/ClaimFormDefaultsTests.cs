// P262-CLAIMS item 20: the default claim form at the provider level (provider.ClaimFormNum). Rules without a database, then the scratch database:
// new claims take the treating provider's claim form over the plan's, and a claim form a provider uses cannot be deleted.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace P262ClaimsTests {
	[TestClass]
	public class ClaimFormDefaultsTests {
		[TestMethod]
		public void Pick_ProviderFormFirst() {
			Assert.AreEqual(7,ClaimFormDefaults.Pick(7,3,x => true),"The provider's claim form takes priority over the plan's.");
			Assert.AreEqual(3,ClaimFormDefaults.Pick(0,3,x => true),"No provider claim form: the plan's.");
			Assert.AreEqual(3,ClaimFormDefaults.Pick(7,3,x => false),"The provider's claim form no longer exists: the plan's.");
			Assert.AreEqual(0,ClaimFormDefaults.Pick(0,0,x => true),"Neither: 0, as before (the plan's).");
			Assert.AreEqual(7,ClaimFormDefaults.Pick(7,0,x => true));
			Assert.AreEqual(7,ClaimFormDefaults.Pick(7,3,null),"No existence check given: the provider's.");
		}
	}

	[TestClass]
	public class ClaimFormDefaultsDbTests {
		[TestInitialize]
		public void Init() {
			ClaimsDb.Begin();
		}

		[TestMethod]
		public void GetClaimFormNumForNewClaim_TreatingProvidersFormOverridesThePlans() {
			string suffix=ClaimsDb.NewSuffix();
			long claimFormNumPlan=AddClaimForm("Plan form "+suffix);
			long claimFormNumProv=AddClaimForm("Provider form "+suffix);
			long provNumWith=AddProvider("PW"+suffix.Substring(0,4),claimFormNumProv);
			long provNumWithout=AddProvider("PO"+suffix.Substring(0,4),0);
			Assert.AreEqual(claimFormNumProv,ClaimFormDefaults.GetClaimFormNumForNewClaim(provNumWith,claimFormNumPlan));
			Assert.AreEqual(claimFormNumPlan,ClaimFormDefaults.GetClaimFormNumForNewClaim(provNumWithout,claimFormNumPlan));
			Assert.AreEqual(claimFormNumPlan,ClaimFormDefaults.GetClaimFormNumForNewClaim(0,claimFormNumPlan),"No treating provider: the plan's.");
			Assert.AreEqual(claimFormNumPlan,ClaimFormDefaults.GetClaimFormNumForNewClaim(999999999,claimFormNumPlan),"Unknown provider: the plan's.");
		}

		[TestMethod]
		public void GetClaimFormNumForNewClaim_DeletedFormFallsBackToThePlans() {
			string suffix=ClaimsDb.NewSuffix();
			long claimFormNumPlan=AddClaimForm("Plan form "+suffix);
			long provNum=AddProvider("PD"+suffix.Substring(0,4),999999998);//a claim form that does not exist
			Assert.AreEqual(claimFormNumPlan,ClaimFormDefaults.GetClaimFormNumForNewClaim(provNum,claimFormNumPlan));
		}

		[TestMethod]
		public void CreateClaimForRepeatCharge_UsesTheTreatingProvidersForm() {
			string suffix=ClaimsDb.NewSuffix();
			long claimFormNumPlan=AddClaimForm("Plan form "+suffix);
			long claimFormNumProv=AddClaimForm("Provider form "+suffix);
			long provNumWith=AddProvider("RW"+suffix.Substring(0,4),claimFormNumProv);
			long provNumWithout=AddProvider("RO"+suffix.Substring(0,4),0);
			Claim claimWith=CreateRepeatChargeClaim(suffix+"a",provNumWith,claimFormNumPlan);
			Assert.AreEqual(provNumWith,claimWith.ProvTreat);
			Assert.AreEqual(claimFormNumProv,claimWith.ClaimForm);
			Assert.AreEqual(claimFormNumProv,Claims.GetClaim(claimWith.ClaimNum).ClaimForm,"Saved with the provider's claim form.");
			Claim claimWithout=CreateRepeatChargeClaim(suffix+"b",provNumWithout,claimFormNumPlan);
			Assert.AreEqual(claimFormNumPlan,Claims.GetClaim(claimWithout.ClaimNum).ClaimForm,"As before 26.2: the plan's claim form.");
		}

		[TestMethod]
		public void Delete_RefusedWhileAProviderUsesTheForm() {
			string suffix=ClaimsDb.NewSuffix();
			long claimFormNum=AddClaimForm("Provider only form "+suffix);
			string abbr="DF"+suffix.Substring(0,4);
			long provNum=AddProvider(abbr,claimFormNum);
			CollectionAssert.AreEqual(new List<string>() { abbr },ClaimFormDefaults.GetProviderAbbrsUsing(claimFormNum));
			ClaimForm claimForm=new ClaimForm() { ClaimFormNum=claimFormNum };
			Assert.IsFalse(ClaimForms.Delete(claimForm),"A provider's default claim form is in use.");
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM claimform WHERE ClaimFormNum="+POut.Long(claimFormNum)));
			Provider provider=Providers.GetProv(provNum).Copy();
			provider.ClaimFormNum=0;
			Providers.Update(provider);
			Providers.RefreshCache();
			Assert.AreEqual(0,ClaimFormDefaults.GetProviderAbbrsUsing(claimFormNum).Count);
			Assert.IsTrue(ClaimForms.Delete(claimForm),"No longer used by anyone.");
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM claimform WHERE ClaimFormNum="+POut.Long(claimFormNum)));
			Assert.AreEqual(0,ClaimFormDefaults.GetProviderAbbrsUsing(0).Count);
		}

		#region Helpers
		private static long AddClaimForm(string description) {
			ClaimForm claimForm=new ClaimForm() { Description=description,FontName="Arial",FontSize=8,Width=850,Height=1100,Items=new List<ClaimFormItem>() };
			long claimFormNum=ClaimForms.Insert(claimForm,includeClaimFormItems:false);
			ClaimForms.RefreshCache();
			return claimFormNum;
		}

		private static long AddProvider(string abbr,long claimFormNum) {
			Provider provider=new Provider() { Abbr=abbr,LName="Synthetic",FName="Prov",ClaimFormNum=claimFormNum };
			Providers.Insert(provider);
			Providers.RefreshCache();
			return provider.ProvNum;
		}

		///<summary>A patient with one primary plan whose claim form is claimFormNumPlan, one completed procedure by provNum, and the claim the repeat
		///charge tool creates for it.</summary>
		private static Claim CreateRepeatChargeClaim(string suffix,long provNum,long claimFormNumPlan) {
			Patient patient=PatientT.CreatePatient(suffix,priProvNum:provNum,lName:"Synthetic");
			Carrier carrier=CarrierT.CreateCarrier("Carrier "+suffix);
			InsPlan insPlan=InsPlanT.CreateInsPlan(carrier.CarrierNum);
			insPlan.ClaimFormNum=claimFormNumPlan;
			InsPlans.Update(insPlan);
			InsSub insSub=InsSubT.CreateInsSub(patient.PatNum,insPlan.PlanNum);
			PatPlanT.CreatePatPlan(1,patient.PatNum,insSub.InsSubNum);
			Procedure procedure=ProcedureT.CreateProcedure(patient,"Z"+suffix.Substring(0,4),ProcStat.C,"",100,provNum:provNum);
			List<PatPlan> listPatPlans=PatPlans.Refresh(patient.PatNum);
			List<InsSub> listInsSubs=InsSubs.GetMany(listPatPlans.Select(x => x.InsSubNum).ToList());
			List<InsPlan> listInsPlans=InsPlans.RefreshForSubList(listInsSubs);
			List<ClaimProc> listClaimProcs=ClaimProcs.Refresh(patient.PatNum);
			return Claims.CreateClaimForRepeatCharge("P",listPatPlans,listInsPlans,listClaimProcs,procedure,listInsSubs,patient);
		}
		#endregion Helpers
	}
}
