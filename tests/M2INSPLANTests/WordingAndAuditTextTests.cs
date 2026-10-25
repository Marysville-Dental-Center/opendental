// The 25.4 wording of the write-off overrides (v25_4-family-writeoff-override-wording) and the 26.1 audit text of a dropped plan
// (v26_1-tools-audit-insplan-drop), without a database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDental;
using OpenDentBusiness;

namespace M2INSPLANTests {
	[TestClass]
	public class WordingAndAuditTextTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");//translation is skipped, so no database is needed
		}

		[TestMethod]
		public void ExclusionRule_KeepsItsNumbersAndNames() {
			//Only the shown text changes: insplan.ExclusionFeeRule rows keep their meaning.
			Assert.AreEqual(0,(int)ExclusionRule.PracticeDefault);
			Assert.AreEqual(1,(int)ExclusionRule.DoNothing);
			Assert.AreEqual(2,(int)ExclusionRule.UseUcrFee);
			Assert.AreEqual(3,Enum.GetValues(typeof(ExclusionRule)).Length);
			Assert.AreEqual(0,(int)YN.Unknown);
			Assert.AreEqual(1,(int)YN.Yes);
			Assert.AreEqual(2,(int)YN.No);
		}

		[TestMethod]
		public void ExclusionOptions_Use25_4Wording() {
			Assert.AreEqual("Practice Default",InsPlanWriteOffL.GetExclusionRuleText(ExclusionRule.PracticeDefault));
			Assert.AreEqual("Do Nothing",InsPlanWriteOffL.GetExclusionRuleText(ExclusionRule.DoNothing));
			Assert.AreEqual("Use Standard Provider Fee",InsPlanWriteOffL.GetExclusionRuleText(ExclusionRule.UseUcrFee));
		}

		[TestMethod]
		public void HelpText_ExplainsEveryOption() {
			string help=InsPlanWriteOffL.GetExclusionsHelp();
			StringAssert.Contains(help,"Practice Default");
			StringAssert.Contains(help,"Do Nothing applies the plan's normal fee and write-off.");
			StringAssert.Contains(help,"Use Standard Provider Fee bills the full standard fee with no write-off.");
			foreach(string helpLimit in new[] { InsPlanWriteOffL.GetAnnualMaxHelp(),InsPlanWriteOffL.GetFreqOrAgingHelp() }) {
				StringAssert.Contains(helpLimit,"Yes bills the full standard provider fee with no write-off");
				StringAssert.Contains(helpLimit,"No keeps the normal fee and write-off.");
				StringAssert.Contains(helpLimit,"Default follows");
			}
			StringAssert.Contains(InsPlanWriteOffL.GetAnnualMaxHelp(),"annual max");
			StringAssert.Contains(InsPlanWriteOffL.GetFreqOrAgingHelp(),"age or frequency limit");
			StringAssert.Contains(InsPlanWriteOffL.GetClaimsUseStandardFeeHelp(),"standard provider fee");
			//No "UCR" in any text the window shows.
			foreach(string text in new[] { help,InsPlanWriteOffL.GetAnnualMaxHelp(),InsPlanWriteOffL.GetFreqOrAgingHelp(),
				InsPlanWriteOffL.GetClaimsUseStandardFeeHelp() }.Concat(Enum.GetValues(typeof(ExclusionRule)).Cast<ExclusionRule>()
				.Select(x => InsPlanWriteOffL.GetExclusionRuleText(x))))
			{
				Assert.IsFalse(text.Contains("UCR"),text);
			}
		}

		[TestMethod]
		public void DropLogText_NamesThePlanNum() {
			Assert.AreEqual("The insurance plan for the carrier Example Dental (PlanNum 123) was dropped.",
				PatPlans.GetDropLogText("Example Dental",123));
			Assert.AreEqual("(PlanNum 9876543210)",PatPlans.GetPlanNumLogText(9876543210));
		}

		[TestMethod]
		public void DropLogText_TellsTwoPlansWithTheSameCarrierApart() {
			Assert.AreNotEqual(PatPlans.GetDropLogText("Same Carrier",10),PatPlans.GetDropLogText("Same Carrier",11));
		}
	}
}
