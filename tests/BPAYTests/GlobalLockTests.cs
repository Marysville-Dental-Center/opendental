// 25.3 v25_3-account-payplan-bypass-globallock: with the setting on, the global lock date and lock days no longer block PayPlanEdit; payments,
// adjustments and the rest stay locked. Every listed call site routes through Security.IsGlobalDateLock, so each is covered by the same switch.
// Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text.RegularExpressions;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BPAYTests {
	[TestClass]
	public class GlobalLockTests {
		private string _lockDateOld;
		private int _lockDaysOld;

		[TestInitialize]
		public void TestInitialize() {
			BPayDb.Begin();
			_lockDateOld=PrefC.GetString(PrefName.SecurityLockDate);
			_lockDaysOld=PrefC.GetInt(PrefName.SecurityLockDays);
			PayPlanGlobalLock.Save(false);
			BPayDb.LogInWith(EnumPermType.PayPlanEdit,EnumPermType.PaymentCreate,EnumPermType.PaymentEdit,EnumPermType.AdjustmentEdit);
		}

		[TestCleanup]
		public void TestCleanup() {
			if(BPayDb.SkipReason!=null) {
				return;
			}
			Prefs.UpdateString(PrefName.SecurityLockDate,_lockDateOld);
			Prefs.UpdateInt(PrefName.SecurityLockDays,_lockDaysOld);
			PayPlanGlobalLock.Save(false);
			Security.CurUser=new Userod();
		}

		private static void SetLockDate(DateTime date) {
			Prefs.UpdateString(PrefName.SecurityLockDate,POut.Date(date,false));
			Prefs.UpdateInt(PrefName.SecurityLockDays,0);
			Cache.Refresh(InvalidType.Prefs);
		}

		private static void SetLockDays(int days) {
			Prefs.UpdateString(PrefName.SecurityLockDate,POut.Date(DateTime.MinValue,false));
			Prefs.UpdateInt(PrefName.SecurityLockDays,days);
			Cache.Refresh(InvalidType.Prefs);
		}

		private static int CountRows() {
			return PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+PayPlanGlobalLock.PREF_NAME+"'"));
		}

		[TestMethod]
		public void ProposedName_IsNotInPrefName() {
			Assert.AreEqual("SecurityLockExcludesPayPlans",PayPlanGlobalLock.PREF_NAME);
			Assert.IsFalse(Enum.IsDefined(typeof(PrefName),PayPlanGlobalLock.PREF_NAME),"An unconfirmed name stays out of PrefName (RegistryTests).");
		}

		[TestMethod]
		public void MissingRow_ReadsOff() {
			DataCore.NonQ("DELETE FROM preference WHERE PrefName='"+PayPlanGlobalLock.PREF_NAME+"'");
			Cache.Refresh(InvalidType.Prefs);
			Assert.IsFalse(PayPlanGlobalLock.IsOn());
			Assert.IsFalse(PayPlanGlobalLock.IsExcludedFromGlobalLock(EnumPermType.PayPlanEdit));
			Assert.IsFalse(PayPlanGlobalLock.Save(false),"Off is the default: nothing to store.");
			Assert.AreEqual(0,CountRows(),"No row is inserted until the office turns the setting on.");
		}

		[TestMethod]
		public void Save_InsertsOneRowThenUpdatesIt() {
			DataCore.NonQ("DELETE FROM preference WHERE PrefName='"+PayPlanGlobalLock.PREF_NAME+"'");
			Cache.Refresh(InvalidType.Prefs);
			Assert.IsTrue(PayPlanGlobalLock.Save(true));
			Assert.AreEqual(1,CountRows());
			Assert.IsTrue(PayPlanGlobalLock.IsOn());
			Assert.IsFalse(PayPlanGlobalLock.Save(true),"Unchanged.");
			Assert.IsTrue(PayPlanGlobalLock.Save(false));
			Assert.AreEqual(1,CountRows());
			Assert.AreEqual("0",DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='"+PayPlanGlobalLock.PREF_NAME+"'"));
			Cache.Refresh(InvalidType.Prefs);//another workstation reloads
			Assert.IsFalse(PayPlanGlobalLock.IsOn());
		}

		[TestMethod]
		public void GlobalLockDate_BlocksPayPlansOnlyWhileTheSettingIsOff() {
			SetLockDate(DateTime.Today.AddDays(-10));
			DateTime dateLocked=DateTime.Today.AddDays(-20);
			Assert.IsTrue(Security.IsGlobalDateLock(EnumPermType.PayPlanEdit,dateLocked,true),"Off: payment plans are locked.");
			PayPlanGlobalLock.Save(true);
			Assert.IsFalse(Security.IsGlobalDateLock(EnumPermType.PayPlanEdit,dateLocked,true),"On: payment plans ignore the global lock.");
			Assert.IsTrue(Security.IsGlobalDateLock(EnumPermType.PaymentCreate,dateLocked,true),"Payments stay locked.");
			Assert.IsTrue(Security.IsGlobalDateLock(EnumPermType.PaymentEdit,dateLocked,true),"Payments stay locked.");
			Assert.IsTrue(Security.IsGlobalDateLock(EnumPermType.AdjustmentEdit,dateLocked,true),"Adjustments stay locked.");
		}

		[TestMethod]
		public void GlobalLockDays_BlocksPayPlansOnlyWhileTheSettingIsOff() {
			SetLockDays(5);
			DateTime dateLocked=DateTime.Today.AddDays(-30);
			Assert.IsTrue(Security.IsGlobalDateLock(EnumPermType.PayPlanEdit,dateLocked,true));
			PayPlanGlobalLock.Save(true);
			Assert.IsFalse(Security.IsGlobalDateLock(EnumPermType.PayPlanEdit,dateLocked,true));
			Assert.IsTrue(Security.IsGlobalDateLock(EnumPermType.PaymentCreate,dateLocked,true));
		}

		[TestMethod]
		public void DatesAfterTheLock_AreNeverBlocked() {
			SetLockDate(DateTime.Today.AddDays(-10));
			Assert.IsFalse(Security.IsGlobalDateLock(EnumPermType.PayPlanEdit,DateTime.Today,true));
			PayPlanGlobalLock.Save(true);
			Assert.IsFalse(Security.IsGlobalDateLock(EnumPermType.PayPlanEdit,DateTime.Today,true));
		}

		[TestMethod]
		public void DatedIsAuthorized_FollowsTheSetting() {
			//Security.IsAuthorized calls IsGlobalDateLock, so every dated IsAuthorized(PayPlanEdit,...) check follows the setting.
			SetLockDate(DateTime.Today.AddDays(-10));
			DateTime dateLocked=DateTime.Today.AddDays(-20);
			Assert.IsFalse(Security.IsAuthorized(EnumPermType.PayPlanEdit,dateLocked,true));
			PayPlanGlobalLock.Save(true);
			Assert.IsTrue(Security.IsAuthorized(EnumPermType.PayPlanEdit,dateLocked,true));
			Assert.IsFalse(Security.IsAuthorized(EnumPermType.PaymentCreate,dateLocked,true),"Payments stay locked.");
		}

		[TestMethod]
		public void WithoutPayPlanEdit_TheSettingGrantsNothing() {
			BPayDb.LogInWith(EnumPermType.PaymentCreate);
			PayPlanGlobalLock.Save(true);
			Assert.IsFalse(Security.IsAuthorized(EnumPermType.PayPlanEdit,DateTime.Today,true));
		}

		[TestMethod]
		public void DynamicPlanTermsValidation_CallSite_FollowsTheSetting() {
			//PayPlanEdit.ValidateDynamicPaymentPlanTerms (used when saving a dynamic plan) checks the agreement date against the global lock.
			SetLockDate(DateTime.Today.AddDays(-10));
			PayPlanTerms payPlanTerms=new PayPlanTerms();
			payPlanTerms.DateAgreement=DateTime.Today.AddDays(-20);
			payPlanTerms.PeriodPayment=50;
			payPlanTerms.PrincipalAmount=300;
			string errors=PayPlanEdit.ValidateDynamicPaymentPlanTerms(payPlanTerms,isNew:false,isLocked:false,doCheckApr:false,linkedProdCount:1);
			StringAssert.Contains(errors,"Locked by Administrator");
			PayPlanGlobalLock.Save(true);
			errors=PayPlanEdit.ValidateDynamicPaymentPlanTerms(payPlanTerms,isNew:false,isLocked:false,doCheckApr:false,linkedProdCount:1);
			Assert.AreEqual("",errors);
		}

		[TestMethod]
		public void ListedCallSites_AllRouteThroughIsGlobalDateLock() {
			//The spec lists the direct callers; each must keep calling Security.IsGlobalDateLock(EnumPermType.PayPlanEdit,...) so the setting reaches it.
			AssertCallCount(Path.Combine("OpenDental","Forms","FormPayPlan.cs"),4);
			AssertCallCount(Path.Combine("OpenDental","Forms","FormPayPlanDynamic.cs"),1);
			AssertCallCount(Path.Combine("OpenDental","Forms","FormPayPlanCredits.cs"),2);
			AssertCallCount(Path.Combine("OpenDentBusiness","Db Multi Table","PayPlanEdit.cs"),1);
			string security=File.ReadAllText(TestDb.FindRepoFile(Path.Combine("OpenDentBusiness","Db Multi Table","Security.cs")));
			StringAssert.Contains(security,"PayPlanGlobalLock.IsExcludedFromGlobalLock(perm)");
		}

		private static void AssertCallCount(string relativePath,int countExpected) {
			string source=File.ReadAllText(TestDb.FindRepoFile(relativePath));
			int count=Regex.Matches(source,@"Security\.IsGlobalDateLock\(EnumPermType\.PayPlanEdit,").Count;
			Assert.AreEqual(countExpected,count,relativePath);
		}

		[TestMethod]
		public void ChangeLogLine_UsesTheLockWindowForm() {
			Assert.AreEqual("",PayPlanGlobalLock.GetChangeLogLine(true,true));
			Assert.AreEqual("Payment plans are not affected by the global lock changed from 'False' to 'True'",PayPlanGlobalLock.GetChangeLogLine(false,true));
		}
	}
}
