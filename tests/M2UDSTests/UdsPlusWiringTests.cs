// How the UDS+ report is wired in: report name, dispatcher, Report Setup, the migration step and its level, the preference, the permissions and
// the audit entries. Source and reflection checks only; no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using OpenDentBusiness.ForkMigrations.Features;
using OpenDentBusiness.ForkMigrations.Seeds26_1;

namespace M2UDSTests {
	[TestClass]
	public class UdsPlusWiringTests {
		private static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(UdsTestDb.GetRepoFile(arrayParts));
		}

		[TestMethod]
		public void ReportName_IsKnownAndDispatched() {
			Assert.AreEqual("ODUdsPlus",DisplayReports.ReportNames.UdsPlus);
			Assert.IsTrue(DisplayReports.ReportNames.IsKnown(DisplayReports.ReportNames.UdsPlus));
			string source=ReadRepoFile("OpenDental","Forms","FormReportsMore.cs");
			int index=source.IndexOf("case DisplayReports.ReportNames.UdsPlus:",StringComparison.Ordinal);
			Assert.IsTrue(index>=0,"OpenReportHelper opens the report.");
			int indexBreak=source.IndexOf("break;",index,StringComparison.Ordinal);
			StringAssert.Contains(source.Substring(index,indexBreak-index),"new FormRpUdsPlus()");
			Assert.IsTrue(index>source.IndexOf("public static ReportNonModalSelection OpenReportHelper(",StringComparison.Ordinal),"Inside the dispatcher.");
		}

		[TestMethod]
		public void ReportSetup_NamesTheReport() {
			string source=ReadRepoFile("OpenDental","User Controls","UserControlReportSetup.cs");
			StringAssert.Contains(source,"case DisplayReports.ReportNames.UdsPlus:");
		}

		[TestMethod]
		public void Window_LogsRunsAndFilesAndGuardsSetup() {
			string source=ReadRepoFile("OpenDental","Forms Reports","FormRpUdsPlus.cs");
			StringAssert.Contains(source,"SecurityLogs.MakeLogEntry(EnumPermType.Reports,0,\"UDS+ Dental Services report run for ");
			StringAssert.Contains(source,"SecurityLogs.MakeLogEntry(EnumPermType.Reports,0,\"UDS+ Dental Services patient-level file saved for ");
			StringAssert.Contains(source,"Security.IsAuthorized(EnumPermType.Setup)","Report Setup security guards the line setup.");
			string sourceSetup=ReadRepoFile("OpenDental","Forms Reports","FormRpUdsPlusSetup.cs");
			StringAssert.Contains(sourceSetup,"Prefs.UpdateString(PrefName.UdsPlusDentalLineCodes,");
			StringAssert.Contains(sourceSetup,"SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,");
		}

		[TestMethod]
		public void Preference_IsSeededBlank() {
			//Registered and seeded by M2-02; this package only reads and writes it.
			Assert.IsTrue(Seed26_1.ListPrefDefaults.Any(x => x.PrefName==PrefName.UdsPlusDentalLineCodes && x.ValueString==""));
		}

		[TestMethod]
		public void Step_HasItsDatedLevel() {
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			ForkMigrationStep step=listSteps.SingleOrDefault(x => x is ReportRowUdsPlusStep);
			Assert.IsNotNull(step);
			Assert.AreEqual(2026110103,step.Level,"Renumbered on 2026-11-01 from the retired package range level 2027003501.");
			Assert.IsTrue(step.Level>new Seed26_1Step().Level,"After the 26.1 seed rows.");
			Assert.AreEqual("OpenDentBusiness.ForkMigrations.Features",step.GetType().Namespace,
				"Not in OpenDentBusiness.ForkMigrations, whose steps must sort before Seed25_3Step.");
		}

		[TestMethod]
		public void ValidateStep_AcceptsDatedLevelsOnly() {
			ForkMigrationRunner.ValidateStep(new ReportRowUdsPlusStep());
			//The yyyy00PPNN package ranges are retired (owner decision of 2026-11-01).
			AssertRejected(new StepPackageRange());
			AssertRejected(new StepMonth00Of2026());
			AssertRejected(new StepMonth13());
			AssertRejected(new StepShort());
		}

		private static void AssertRejected(ForkMigrationStep step) {
			try {
				ForkMigrationRunner.ValidateStep(step);
			}
			catch(ApplicationException ex) {
				StringAssert.Contains(ex.Message,"yyyyMMddNN");
				return;
			}
			Assert.Fail("Level "+step.Level+" must be rejected.");
		}

		[ForkMigration(2027009999,"Package range test step")]
		private class StepPackageRange:ForkMigrationStep {
			public override void Apply(ForkMigrationContext context) { }
		}

		[ForkMigration(2026003501,"Month 00 before 2027")]
		private class StepMonth00Of2026:ForkMigrationStep {
			public override void Apply(ForkMigrationContext context) { }
		}

		[ForkMigration(2027133501,"Month 13")]
		private class StepMonth13:ForkMigrationStep {
			public override void Apply(ForkMigrationContext context) { }
		}

		[ForkMigration(202700351,"Short")]
		private class StepShort:ForkMigrationStep {
			public override void Apply(ForkMigrationContext context) { }
		}
	}
}
