// 25.1 v25_1-account-payplan-unlock-permission: a saved, locked dynamic payment plan can be unlocked only with Pay Plan Unlock (267), and the
// unlock is audited. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace PayPlanTests {
	[TestClass]
	public class PayPlansTests {
		[TestInitialize]
		public void TestInitialize() {
			PayPlanDb.Begin();
		}

		[TestCleanup]
		public void TestCleanup() {
			Security.CurUser=new Userod();
		}

		///<summary>A dynamic payment plan with an APR, which the test helper saves locked, as the Locked box does.</summary>
		private static PayPlan CreateLockedDynamicPlan(string suffix) {
			Patient patient=PayPlanDb.CreatePatient(suffix);
			Adjustment adjustment=AdjustmentT.MakeAdjustment(patient.PatNum,300,DateTime.Today);
			PayPlan payPlan=PayPlanT.CreateDynamicPaymentPlan(patient.PatNum,patient.PatNum,DateTime.Today,0,10,50,new List<Procedure>(),
				new List<Adjustment> { adjustment },PayPlanFrequency.Monthly,runService:false,doInsertChargeForToday:false);
			Assert.IsTrue(PayPlans.GetOne(payPlan.PayPlanNum).IsLocked,"Set up: the plan is saved locked.");
			return PayPlans.GetOne(payPlan.PayPlanNum);
		}

		private static DynamicPaymentPlanModuleData Save(PayPlan payPlan,bool isLocking,bool isUnlocking) {
			DynamicPaymentPlanModuleData moduleData=PayPlanEdit.GetDynamicPaymentPlanModuleData(payPlan);
			PayPlanTerms terms=PayPlanEdit.GetPayPlanTerms(moduleData.PayPlan,moduleData.ListPayPlanLinks);
			return PayPlanEdit.SaveDynamicPaymentPlanAndTerms(terms,moduleData,isLocking,moduleData.PayPlan.PlanCategory,isUnlocking:isUnlocking);
		}

		private static int CountUnlockLogs(PayPlan payPlan) {
			return PIn.Int(DataCore.GetScalar("SELECT COUNT(*) FROM securitylog WHERE PermType="+POut.Int((int)EnumPermType.PayPlanUnlock)
				+" AND FKey="+POut.Long(payPlan.PayPlanNum)));
		}

		[TestMethod]
		public void PermissionNumber_Is267() {
			Assert.AreEqual(267,(int)EnumPermType.PayPlanUnlock);
		}

		[TestMethod]
		public void CanUnlockPayPlan_OnlyWithPayPlanUnlock() {
			PayPlanDb.LogInWith(EnumPermType.PayPlanEdit);
			Assert.IsFalse(PayPlanEdit.CanUnlockPayPlan(),"Pay Plan Edit alone does not unlock.");
			PayPlanDb.LogInWith(EnumPermType.PayPlanEdit,EnumPermType.PayPlanUnlock);
			Assert.IsTrue(PayPlanEdit.CanUnlockPayPlan());
		}

		[TestMethod]
		public void Unlock_WithoutPermission_IsRefusedAndThePlanStaysLocked() {
			PayPlanDb.LogInWith(EnumPermType.PayPlanEdit);
			PayPlan payPlan=CreateLockedDynamicPlan("NoUnlock");
			try {
				Save(payPlan,isLocking:false,isUnlocking:true);
				Assert.Fail("Unlocking without Pay Plan Unlock must throw.");
			}
			catch(ODException ex) {
				StringAssert.Contains(ex.Message,"Pay Plan Unlock");
			}
			Assert.IsTrue(PayPlans.GetOne(payPlan.PayPlanNum).IsLocked);
			Assert.AreEqual(0,CountUnlockLogs(payPlan));
		}

		[TestMethod]
		public void Unlock_WithPermission_ClearsLockAndIsAudited() {
			Userod userod=PayPlanDb.LogInWith(EnumPermType.PayPlanEdit,EnumPermType.PayPlanUnlock);
			PayPlan payPlan=CreateLockedDynamicPlan("Unlock");
			DynamicPaymentPlanModuleData moduleData=Save(payPlan,isLocking:false,isUnlocking:true);
			Assert.IsFalse(moduleData.PayPlan.IsLocked);
			PayPlan payPlanDb=PayPlans.GetOne(payPlan.PayPlanNum);
			Assert.IsFalse(payPlanDb.IsLocked,"Saved unlocked.");
			Assert.IsTrue(PayPlans.IsPayPlanHashValid(payPlanDb),"The row hash covers IsLocked and is recomputed.");
			Assert.AreEqual(1,CountUnlockLogs(payPlan));
			DataTable table=DataCore.GetTable("SELECT PatNum,UserNum,LogText FROM securitylog WHERE PermType="+POut.Int((int)EnumPermType.PayPlanUnlock)
				+" AND FKey="+POut.Long(payPlan.PayPlanNum));
			Assert.AreEqual(payPlan.PatNum.ToString(),table.Rows[0]["PatNum"].ToString());
			Assert.AreEqual(userod.UserNum.ToString(),table.Rows[0]["UserNum"].ToString());
			StringAssert.Contains(table.Rows[0]["LogText"].ToString(),"unlocked");
		}

		[TestMethod]
		public void Unlock_ThenLockAgain_LocksAndDoesNotLogAnotherUnlock() {
			PayPlanDb.LogInWith(EnumPermType.PayPlanEdit,EnumPermType.PayPlanUnlock);
			PayPlan payPlan=CreateLockedDynamicPlan("Relock");
			Save(payPlan,isLocking:false,isUnlocking:true);
			Save(PayPlans.GetOne(payPlan.PayPlanNum),isLocking:true,isUnlocking:false);
			Assert.IsTrue(PayPlans.GetOne(payPlan.PayPlanNum).IsLocked);
			Assert.AreEqual(1,CountUnlockLogs(payPlan));
		}

		[TestMethod]
		public void SaveWithoutUnlocking_NeverUnlocks() {
			//The Close Payment Plan tool passes isLocking=false for locked plans; that alone must not unlock them.
			PayPlanDb.LogInWith(EnumPermType.PayPlanEdit,EnumPermType.PayPlanUnlock);
			PayPlan payPlan=CreateLockedDynamicPlan("NoChange");
			Save(payPlan,isLocking:false,isUnlocking:false);
			Assert.IsTrue(PayPlans.GetOne(payPlan.PayPlanNum).IsLocked);
			Assert.AreEqual(0,CountUnlockLogs(payPlan));
		}

		[TestMethod]
		public void Unlock_IgnoredWhenThePlanIsNotLocked() {
			PayPlanDb.LogInWith(EnumPermType.PayPlanEdit);
			Patient patient=PayPlanDb.CreatePatient("NotLocked");
			Adjustment adjustment=AdjustmentT.MakeAdjustment(patient.PatNum,300,DateTime.Today);
			PayPlan payPlan=PayPlanT.CreateDynamicPaymentPlan(patient.PatNum,patient.PatNum,DateTime.Today,0,0,50,new List<Procedure>(),
				new List<Adjustment> { adjustment },PayPlanFrequency.Monthly,runService:false,doInsertChargeForToday:false);
			Save(PayPlans.GetOne(payPlan.PayPlanNum),isLocking:false,isUnlocking:true);//No permission needed: nothing to unlock.
			Assert.IsFalse(PayPlans.GetOne(payPlan.PayPlanNum).IsLocked);
			Assert.AreEqual(0,CountUnlockLogs(payPlan));
		}

		[TestMethod]
		public void AprRule_StillRequiresLockedWhenThePreferenceIsOn() {
			Prefs.UpdateBool(PrefName.PayPlanRequireLockForAPR,true);
			try {
				PayPlanTerms terms=new PayPlanTerms { APR=10,PeriodPayment=50,PrincipalAmount=300,DateAgreement=DateTime.Today };
				string errors=PayPlanEdit.ValidateDynamicPaymentPlanTerms(terms,isNew:false,isLocked:false,doCheckApr:true,linkedProdCount:1,isCloseout:true);
				StringAssert.Contains(errors,"must be locked","An unlocked APR plan must be locked again before saving.");
				errors=PayPlanEdit.ValidateDynamicPaymentPlanTerms(terms,isNew:false,isLocked:true,doCheckApr:true,linkedProdCount:1,isCloseout:true);
				Assert.IsFalse(errors.Contains("must be locked"));
			}
			finally {
				Prefs.UpdateBool(PrefName.PayPlanRequireLockForAPR,false);
			}
		}

		[TestMethod]
		public void UpgradeGrant_GroupsWithPayPlanEditGetPayPlanUnlock() {
			//Seed25_3 (M1-02) grants Pay Plan Unlock to every group that had Pay Plan Edit on a database that comes from 24.3.
			DataTable table=DataCore.GetTable("SELECT edit.UserGroupNum,(SELECT COUNT(*) FROM grouppermission unlock_ WHERE "
				+"unlock_.UserGroupNum=edit.UserGroupNum AND unlock_.PermType="+POut.Int((int)EnumPermType.PayPlanUnlock)+") CountUnlock "
				+"FROM grouppermission edit WHERE edit.PermType="+POut.Int((int)EnumPermType.PayPlanEdit)+" AND edit.UserGroupNum IN "
				+"(SELECT UserGroupNum FROM usergroup WHERE Description NOT LIKE 'A-PAY test group%')");
			Assert.IsTrue(table.Rows.Count>0,"The seed database has groups with Pay Plan Edit.");
			for(int i=0;i<table.Rows.Count;i++) {
				Assert.AreNotEqual("0",table.Rows[i]["CountUnlock"].ToString(),"Group "+table.Rows[i]["UserGroupNum"]+" has Pay Plan Edit but not Unlock.");
			}
		}
	}
}
