// P262-CLAIMS item 20: the default claim form at the provider level (provider.ClaimFormNum). Rules without a database, then the scratch database:
// new claims take the billing provider's claim form, else the treating provider's, over the plan's (owner decision of 2026-11-07), and a claim
// form a provider uses cannot be deleted.
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
		///<summary>Owner decision of 2026-11-07: the billing provider's default claim form first, then the treating provider's, then the plan's.</summary>
		[TestMethod]
		public void Pick_BillingProviderFirst_ThenTreating_ThenThePlans() {
			Assert.AreEqual(5,ClaimFormDefaults.Pick(5,7,3,x => true),"The billing provider's claim form takes priority over the treating provider's.");
			Assert.AreEqual(7,ClaimFormDefaults.Pick(0,7,3,x => true),"No billing provider claim form: the treating provider's.");
			Assert.AreEqual(3,ClaimFormDefaults.Pick(0,0,3,x => true),"Neither provider has one: the plan's.");
			Assert.AreEqual(7,ClaimFormDefaults.Pick(5,7,3,x => x!=5),"The billing provider's claim form no longer exists: the treating provider's.");
			Assert.AreEqual(3,ClaimFormDefaults.Pick(5,7,3,x => false),"Neither exists any more: the plan's.");
			Assert.AreEqual(0,ClaimFormDefaults.Pick(0,0,0,x => true),"None: 0, as before (the plan's).");
			Assert.AreEqual(7,ClaimFormDefaults.Pick(0,7,0,x => true));
			Assert.AreEqual(5,ClaimFormDefaults.Pick(5,7,3,null),"No existence check given: the billing provider's.");
		}
	}

	[TestClass]
	public class ClaimFormDefaultsDbTests {
		[TestInitialize]
		public void Init() {
			ClaimsDb.Begin();
		}

		[TestMethod]
		public void GetClaimFormNumForNewClaim_BillingThenTreatingProvidersFormOverridesThePlans() {
			string suffix=ClaimsDb.NewSuffix();
			long claimFormNumPlan=AddClaimForm("Plan form "+suffix);
			long claimFormNumProv=AddClaimForm("Provider form "+suffix);
			long provNumWith=AddProvider("PW"+suffix.Substring(0,4),claimFormNumProv);
			long provNumWithout=AddProvider("PO"+suffix.Substring(0,4),0);
			long claimFormNumBill=AddClaimForm("Billing form "+suffix);
			long provNumBill=AddProvider("PB"+suffix.Substring(0,4),claimFormNumBill);
			//The treating provider alone (billing provider without a form, or none).
			Assert.AreEqual(claimFormNumProv,ClaimFormDefaults.GetClaimFormNumForNewClaim(provNumWithout,provNumWith,claimFormNumPlan));
			Assert.AreEqual(claimFormNumProv,ClaimFormDefaults.GetClaimFormNumForNewClaim(0,provNumWith,claimFormNumPlan));
			Assert.AreEqual(claimFormNumPlan,ClaimFormDefaults.GetClaimFormNumForNewClaim(provNumWithout,provNumWithout,claimFormNumPlan));
			Assert.AreEqual(claimFormNumPlan,ClaimFormDefaults.GetClaimFormNumForNewClaim(0,0,claimFormNumPlan),"No provider: the plan's.");
			Assert.AreEqual(claimFormNumPlan,ClaimFormDefaults.GetClaimFormNumForNewClaim(999999999,999999999,claimFormNumPlan),"Unknown provider: the plan's.");
			//The billing provider's form first (owner decision of 2026-11-07).
			Assert.AreEqual(claimFormNumBill,ClaimFormDefaults.GetClaimFormNumForNewClaim(provNumBill,provNumWith,claimFormNumPlan));
			Assert.AreEqual(claimFormNumBill,ClaimFormDefaults.GetClaimFormNumForNewClaim(provNumBill,provNumWithout,claimFormNumPlan));
			Assert.AreEqual(claimFormNumBill,ClaimFormDefaults.GetClaimFormNumForNewClaim(provNumBill,0,claimFormNumPlan));
		}

		[TestMethod]
		public void GetClaimFormNumForNewClaim_DeletedFormFallsBackToThePlans() {
			string suffix=ClaimsDb.NewSuffix();
			long claimFormNumPlan=AddClaimForm("Plan form "+suffix);
			long provNum=AddProvider("PD"+suffix.Substring(0,4),999999998);//a claim form that does not exist
			Assert.AreEqual(claimFormNumPlan,ClaimFormDefaults.GetClaimFormNumForNewClaim(provNum,provNum,claimFormNumPlan));
			Assert.AreEqual(claimFormNumPlan,ClaimFormDefaults.GetClaimFormNumForNewClaim(provNum,0,claimFormNumPlan));
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

		///<summary>Owner decision of 2026-11-07: a new claim takes its billing provider's default claim form before its treating provider's.</summary>
		[TestMethod]
		public void CreateClaimForRepeatCharge_UsesTheBillingProvidersFormFirst() {
			string suffix=ClaimsDb.NewSuffix();
			long claimFormNumPlan=AddClaimForm("Plan form "+suffix);
			long claimFormNumTreat=AddClaimForm("Treating form "+suffix);
			long claimFormNumBill=AddClaimForm("Billing form "+suffix);
			long provNumTreat=AddProvider("BT"+suffix.Substring(0,4),claimFormNumTreat);
			long provNumBill=AddProvider("BB"+suffix.Substring(0,4),claimFormNumBill);
			long provNumBillWithout=AddProvider("BN"+suffix.Substring(0,4),0);
			long insBillingProvOld=PrefC.GetLong(PrefName.InsBillingProv);
			try {
				//Claims are billed under provNumBill (Setup, Insurance billing provider: a specific provider).
				Prefs.UpdateLong(PrefName.InsBillingProv,provNumBill);
				Prefs.RefreshCache();
				Claim claim=CreateRepeatChargeClaim(suffix+"c",provNumTreat,claimFormNumPlan);
				Assert.AreEqual(provNumBill,claim.ProvBill);
				Assert.AreEqual(provNumTreat,claim.ProvTreat);
				Assert.AreEqual(claimFormNumBill,Claims.GetClaim(claim.ClaimNum).ClaimForm,"The billing provider's claim form first.");
				//A billing provider without a default claim form: the treating provider's.
				Prefs.UpdateLong(PrefName.InsBillingProv,provNumBillWithout);
				Prefs.RefreshCache();
				claim=CreateRepeatChargeClaim(suffix+"d",provNumTreat,claimFormNumPlan);
				Assert.AreEqual(provNumBillWithout,claim.ProvBill);
				Assert.AreEqual(claimFormNumTreat,Claims.GetClaim(claim.ClaimNum).ClaimForm,"Then the treating provider's.");
			}
			finally {
				Prefs.UpdateLong(PrefName.InsBillingProv,insBillingProvOld);
				Prefs.RefreshCache();
			}
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
