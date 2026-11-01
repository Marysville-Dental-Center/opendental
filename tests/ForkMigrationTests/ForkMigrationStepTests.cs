// Unit tests of how the runner finds and orders steps, and of the enum values this package pins. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace ForkMigrationTests {
	[TestClass]
	public class ForkMigrationStepTests {
		[TestMethod]
		public void GetSteps_FindsThe25_3StepsInLevelOrder() {
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			List<long> listLevels=listSteps.Select(x => x.Level).ToList();
			CollectionAssert.AreEqual(listLevels.OrderBy(x => x).ToList(),listLevels,"Ascending.");
			Assert.AreEqual(listLevels.Count,listLevels.Distinct().Count(),"Unique.");
			CollectionAssert.IsSubsetOf(new long[] { 2026101901,2026101902,2026101903,2026101904,2026101905,2026101906 },listLevels);
			Assert.IsTrue(listSteps.All(x => !string.IsNullOrWhiteSpace(x.Name)));
		}

		[TestMethod]
		public void Validate_RejectsBadSteps() {
			AssertThrows(() => ForkMigrationRunner.ValidateStep(new StepWithoutAttribute()),"no [ForkMigration] attribute");
			AssertThrows(() => ForkMigrationRunner.ValidateStep(new StepWithShortLevel()),"yyyyMMddNN");
			AssertThrows(() => ForkMigrationRunner.ValidateStep(new StepWithBadDate()),"yyyyMMddNN");
			AssertThrows(() => ForkMigrationRunner.ValidateStep(new StepWithoutName()),"no name");
			ForkMigrationRunner.ValidateStep(new StepGood());
			AssertThrows(() => ForkMigrationRunner.ValidateLevelsUnique(new List<ForkMigrationStep> { new StepGood(),new StepGoodTwin() }),"unique");
		}

		///<summary>Only real dated levels from 2026 on (owner decision of 2026-11-01): the yyyy00PPNN package ranges of the single-wave feature port
		///(month 00) are retired, and so are dates before 2026.</summary>
		[TestMethod]
		public void Validate_AcceptsOnlyRealDatedLevelsFrom2026() {
			ForkMigrationRunner.ValidateStep(new StepDated2027());
			AssertThrows(() => ForkMigrationRunner.ValidateStep(new StepPackageRange()),"a real date from 2026 on");
			AssertThrows(() => ForkMigrationRunner.ValidateStep(new StepBefore2026()),"a real date from 2026 on");
			AssertThrows(() => ForkMigrationRunner.ValidateStep(new StepFebruary30()),"a real date from 2026 on");
			Assert.IsNull(typeof(ForkMigrationRunner).GetMethod("IsReservedPackageLevel"),"The package range rule is gone.");
			//GetSteps validates every step of this build; the renumbered package steps (2026-11-01) run after the M2-02 seed step, in their old order.
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			CollectionAssert.AreEqual(new long[] { 2026102501,2026110101,2026110103,2026110104 },listSteps.Where(x => x.Level>=2026102501).Select(x => x.Level).ToList(),
				"2026110102 (M2-EST's back-fill) is abstract, so the runner skips it.");
		}

		[TestMethod]
		public void GuardianRelationship_Has25_1Numbers() {
			Assert.AreEqual(0,(int)GuardianRelationship.None);
			Assert.AreEqual(1,(int)GuardianRelationship.Mother);
			Assert.AreEqual(5,(int)GuardianRelationship.Grandmother);
			Assert.AreEqual(6,(int)GuardianRelationship.Father);
			Assert.AreEqual(7,(int)GuardianRelationship.Brother);
			Assert.AreEqual(22,(int)GuardianRelationship.Grandchild);
			Assert.AreEqual(23,(int)GuardianRelationship.Sitter);
			//Contiguous 0 to 23, so a position in Enum.GetNames is still the value.
			CollectionAssert.AreEqual(Enumerable.Range(0,24).ToList(),Enum.GetValues(typeof(GuardianRelationship)).Cast<int>().ToList());
		}

		[TestMethod]
		public void Enums_HavePublishedNumbers() {
			Assert.AreEqual(8,(int)EnumEFormFieldType.PatientList);
			Assert.AreEqual(9,(int)EnumEFormFieldType.InsPick);
			Assert.AreEqual(4,(int)StatementMode.API);
			Assert.AreEqual("EClipboardWeb",EmailMessageSource.EClipboardWeb.ToString(),"Stored by name.");
			Assert.AreEqual(2,(int)EnumImageAnnotVendor.BetterDiagnostics);
			Assert.AreEqual(19,Enum.GetValues(typeof(EnumCategoryBetterDiag)).Length);
			Assert.AreEqual(8,(int)EnumCategoryBetterDiag.PeriapicalRadiolucency);
			Assert.AreEqual(18,(int)EnumCategoryBetterDiag.MarginalDiscrepancy);
			Assert.AreEqual(3,(int)EnumBrandingType.OfficeDescription);
			Assert.AreEqual(2,(int)EnumUserStatus.DoNotDisturb);
		}

		private static void AssertThrows(Action action,string messagePart) {
			try {
				action();
			}
			catch(ApplicationException ex) {
				StringAssert.Contains(ex.Message,messagePart);
				return;
			}
			Assert.Fail("Expected an ApplicationException containing '"+messagePart+"'.");
		}

		private class StepWithoutAttribute:ForkMigrationStep {
			public override void Apply(ForkMigrationContext context) { }
		}

		[ForkMigration(20261019,"Short level")]
		private class StepWithShortLevel:ForkMigrationStep {
			public override void Apply(ForkMigrationContext context) { }
		}

		[ForkMigration(2026133101,"Month 13")]
		private class StepWithBadDate:ForkMigrationStep {
			public override void Apply(ForkMigrationContext context) { }
		}

		[ForkMigration(2026101999," ")]
		private class StepWithoutName:ForkMigrationStep {
			public override void Apply(ForkMigrationContext context) { }
		}

		[ForkMigration(2026101998,"Good")]
		private class StepGood:ForkMigrationStep {
			public override void Apply(ForkMigrationContext context) { }
		}

		[ForkMigration(2026101998,"Same level")]
		private class StepGoodTwin:ForkMigrationStep {
			public override void Apply(ForkMigrationContext context) { }
		}

		[ForkMigration(2027010401,"Dated 2027")]
		private class StepDated2027:ForkMigrationStep {
			public override void Apply(ForkMigrationContext context) { }
		}

		[ForkMigration(2027002301,"Retired package range")]
		private class StepPackageRange:ForkMigrationStep {
			public override void Apply(ForkMigrationContext context) { }
		}

		[ForkMigration(2025123101,"Before 2026")]
		private class StepBefore2026:ForkMigrationStep {
			public override void Apply(ForkMigrationContext context) { }
		}

		[ForkMigration(2026023001,"February 30")]
		private class StepFebruary30:ForkMigrationStep {
			public override void Apply(ForkMigrationContext context) { }
		}
	}
}
