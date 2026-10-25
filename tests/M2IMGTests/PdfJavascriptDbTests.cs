// The PDF JavaScript preference on a real database: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded by the fork
// migration runner, with a unique per-run name, dropped afterwards. Synthetic data only. Skipped (inconclusive) when the local MariaDB does not
// answer or when FORKMIGRATION_TEST_DB=skip. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository
// root.
using System;
using System.Globalization;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace M2IMGTests {
	[TestClass]
	public class PdfJavascriptDbTests {
		private static readonly string _dbName=TestDb.ScratchName("m2img");
		///<summary>Null when the database is ready; otherwise why the test is skipped.</summary>
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.CreateFresh24_3(_dbName);
			ForkMigrationRunner.Run();
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(_dbName);
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		[TestMethod]
		public void UpgradedDatabase_PdfScriptsStayOffUntilTheOfficeAllowsThem() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(_dbName);
			Cache.Refresh(InvalidType.Prefs);
			Assert.AreEqual("0",DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='ImagingAllowPdfJavascript'"),
				"The upgrade adds the preference, off.");
			Assert.IsFalse(ImageHelper.IsPdfJavascriptAllowed(),"PDFs open with JavaScript off by default.");
			//What Preferences > Imaging > General saves when the box is checked, then unchecked.
			Assert.IsTrue(Prefs.UpdateBool(PrefName.ImagingAllowPdfJavascript,true));
			Cache.Refresh(InvalidType.Prefs);
			Assert.AreEqual("1",DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='ImagingAllowPdfJavascript'"));
			Assert.IsTrue(ImageHelper.IsPdfJavascriptAllowed());
			Assert.IsTrue(Prefs.UpdateBool(PrefName.ImagingAllowPdfJavascript,false));
			Cache.Refresh(InvalidType.Prefs);
			Assert.IsFalse(ImageHelper.IsPdfJavascriptAllowed());
		}
	}
}
