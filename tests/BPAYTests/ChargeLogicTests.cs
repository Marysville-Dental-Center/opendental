// 25.1 v25_1-account-payplan-charge-logic-deprecated: the "Pay Plan charge logic" choice is retired. PayPlansVersion keeps its meaning, has no
// selectable UI, and a legacy value shows as a read-only line. No database needed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using CodeBase;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BPAYTests {
	[TestClass]
	public class ChargeLogicTests {
		[TestInitialize]
		public void TestInitialize() {
			BPayDb.UseEnglishCulture();
		}

		[TestMethod]
		public void AgeCreditsAndDebits_IsNotLegacyAndShowsNothing() {
			Assert.IsFalse(PayPlanChargeLogic.IsLegacyMode((int)PayPlanVersions.AgeCreditsAndDebits));
			Assert.AreEqual("",PayPlanChargeLogic.GetReadOnlyText((int)PayPlanVersions.AgeCreditsAndDebits));
		}

		[TestMethod]
		public void LegacyModes_ShowAReadOnlyLineNamingTheMode() {
			foreach(PayPlanVersions payPlanVersions in new[] { PayPlanVersions.DoNotAge,PayPlanVersions.AgeCreditsOnly,PayPlanVersions.NoCharges }) {
				Assert.IsTrue(PayPlanChargeLogic.IsLegacyMode((int)payPlanVersions),payPlanVersions.ToString());
				string text=PayPlanChargeLogic.GetReadOnlyText((int)payPlanVersions);
				StringAssert.Contains(text,"no longer selectable");
				StringAssert.Contains(text,payPlanVersions.GetDescription());
			}
		}

		[TestMethod]
		public void UnknownValue_IsLegacyAndNamedUnknown() {
			Assert.IsTrue(PayPlanChargeLogic.IsLegacyMode(0));
			StringAssert.Contains(PayPlanChargeLogic.GetReadOnlyText(9),"Unknown (9)");
		}

		[TestMethod]
		public void EnumNumbersAreUnchanged() {
			//Stored values keep their meaning: nothing converts PayPlansVersion.
			Assert.AreEqual(1,(int)PayPlanVersions.DoNotAge);
			Assert.AreEqual(2,(int)PayPlanVersions.AgeCreditsAndDebits);
			Assert.AreEqual(3,(int)PayPlanVersions.AgeCreditsOnly);
			Assert.AreEqual(4,(int)PayPlanVersions.NoCharges);
		}

		[TestMethod]
		public void PreferencePages_HideTheCombo() {
			foreach(string userControl in new[] { "UserControlAccountPayments","UserControlEnterpriseAccount" }) {
				string designer=File.ReadAllText(TestDb.FindRepoFile(Path.Combine("OpenDental","User Controls","Preferences",userControl+".Designer.cs")));
				StringAssert.Contains(designer,"this.comboPayPlansVersion.Visible = false;",userControl);
				string code=File.ReadAllText(TestDb.FindRepoFile(Path.Combine("OpenDental","User Controls","Preferences",userControl+".cs")));
				StringAssert.Contains(code,"PayPlanChargeLogic.GetReadOnlyText(",userControl);
				StringAssert.Contains(code,"comboPayPlansVersion.Visible=false;",userControl);
			}
		}
	}
}
