// SWMAP without a database: which names of the office's Open Dental 25.3.48 database this program reads, the moves of the release v0.0 rows
// (OdSettingNames.Plan) on made-up values, the inverted payment plan lock, the two read delay rows, the step's level and the documents.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;

namespace SWMAPTests {
	[TestClass]
	public class OdSettingNamesTests {
		///<summary>The 51 setting names of the office's Open Dental 25.3.48 database that are not this program's PrefName members (owner question
		///Switch-over 1, 2026-11-07; names only, no value). Each is in exactly one of the four lists of OdNames.</summary>
		public static readonly string[] ArrayNamesFromTheOffice={
			"ApptProvbarShowOnOps","ApptUseSecProvTimeIfEmpty","B11013Status","BetterDiagnosticsEnabled","BlurModuleOnLoad","BrandingUseDefaults",
			"ChatServerCompName","ChatServerDbName","ChatServerMySqlPassHash","ChatServerMySqlUser","ClaimCheckForMedicalEOB","ClaimUpdateFeeForSecondary",
			"CloudApiServiceUrl","DomainLoginForSigBoxes","EClipboardWebEmailMessageTemplate","EClipboardWebSubjectTemplate","EClipboardWebTextMessageTemplate",
			"EFormImportAllergiesAsOther","EFormImportProblemsAsOther","EnableXConnect","EraAutoProcessSplitByCarcCodes","ImageStore","InsHistPerioScalingCode",
			"LettersIncludeReturnAddress","MassEmailSignedUp","MiddleTierSerializeNew","MsgToPayMenuOptionShow","NewPatThankYouTypedSignatureDisable",
			"PatientCommunicationTimeFormat","PayPlansIncludeInGlobalLock","PayTypeMessageToPay","PaymentPortalReceiptEmailBodyTemplate",
			"PaymentPortalReceiptEmailSubjectTemplate","PearlDateTimeLastUsed","PerioRecessionInsteadOfGM","PerioRedAttGing","RamqMachineID","RamqMachinePassword",
			"RamqMachinePasswordDate","ReplicationWriteReadDelayEnabled","ReplicationWriteReadDelayMS","SalesTaxProcCode","ShortCodeEConfirmationTemplate",
			"StatementAutoSendEmail","StatementAutoSendSMS","StationaryDocument","StationaryImage","ToothChartLowerQuality","WSEPSubDomain",
			"WebSchedRecallDoUseSecureEmail","XRayExposureLevel",
		};

		[TestMethod]
		public void TheOfficesNames_EachHasOneDisposition() {
			Assert.AreEqual(51,ArrayNamesFromTheOffice.Distinct().Count());
			List<string> listAll=OdNames.ArrayRead.Concat(OdNames.ArrayLegacy).Concat(OdNames.ArrayDropped).Concat(OdNames.ArrayNotBuilt).ToList();
			Assert.AreEqual(listAll.Count,listAll.Distinct().Count(),"No name is in two lists.");
			CollectionAssert.AreEquivalent(ArrayNamesFromTheOffice,listAll);
			//ODOPTS (2026-11-07): the eight options of "built without" and "left out" are built and read now.
			Assert.AreEqual(18,OdNames.ArrayRead.Length);
			Assert.AreEqual(8,OdNames.ArrayLegacy.Length);
			Assert.AreEqual(24,OdNames.ArrayDropped.Length);
			Assert.AreEqual(1,OdNames.ArrayNotBuilt.Length);
			CollectionAssert.IsSubsetOf(OdOptions.ArrayPrefNames,OdNames.ArrayRead);
		}

		[TestMethod]
		public void NamesReadByThisProgram_AreTheNamesOfListReads() {
			CollectionAssert.AreEquivalent(OdNames.ArrayRead,OdSettingNames.ListReads.SelectMany(x => x.ArrayPrefNamesOd).ToList());
			//Every rename's targets are read, and every read but the sales tax code and the eight ODOPTS options (Open Dental's names from the start)
			//has a rename.
			List<string> listTargets=OdSettingNames.ListRenames.SelectMany(x => x.ListTargets).Select(x => x.PrefNameOd).ToList();
			CollectionAssert.AreEquivalent(OdNames.ArrayRead.Where(x => x!="SalesTaxProcCode" && !OdOptions.ArrayPrefNames.Contains(x)).ToList(),listTargets);
		}

		[TestMethod]
		public void TheClasses_UseOpenDentalsNames() {
			Assert.AreEqual("SalesTaxProcCode",SalesTaxProcs.PREF_PROC_CODE);
			Assert.AreEqual("ClaimCheckForMedicalEOB",ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT);
			Assert.AreEqual("ApptProvbarShowOnOps",ApptTimeBars.PREF_PROV_TIME_ON_OP_BACKGROUND);
			Assert.AreEqual("ApptUseSecProvTimeIfEmpty",ApptTimeBars.PREF_SECONDARY_COLOR_NO_PROV_TIME);
			Assert.AreEqual("EraAutoProcessSplitByCarcCodes",EraAutoSplit.PREF_NAME);
			Assert.AreEqual("PayPlansIncludeInGlobalLock",PayPlanGlobalLock.PREF_NAME);
			Assert.AreEqual("PerioRecessionInsteadOfGM",PerioMeasures.PrefNameUseRecessionTerm);
			Assert.AreEqual("ReplicationWriteReadDelayEnabled",ReplicationDelay.PREF_READ_DELAY_ENABLED);
			Assert.AreEqual("ReplicationWriteReadDelayMS",ReplicationDelay.PREF_READ_DELAY_MS);
			Assert.AreEqual("BlurModuleOnLoad",ModuleSwitchBlur.PREF_NAME);
			//ODOPTS
			Assert.AreEqual("PerioRedAttGing",PerioMeasures.PrefNameRedAttGing);
			Assert.AreEqual("DomainLoginForSigBoxes",DomainLogin.PREF_FOR_SIG_BOXES);
			Assert.AreEqual("ClaimUpdateFeeForSecondary",ClaimFeeForSecondary.PREF_NAME);
			Assert.AreEqual("EFormImportAllergiesAsOther",EFormImport.PREF_ALLERGIES_AS_OTHER);
			Assert.AreEqual("EFormImportProblemsAsOther",EFormImport.PREF_PROBLEMS_AS_OTHER);
			Assert.AreEqual("InsHistPerioScalingCode",InsHistCategories.PREF_PERIO_SCALING_CODE);
			Assert.AreEqual("StatementAutoSendEmail",StatementAutoSend.PREF_EMAIL);
			Assert.AreEqual("StatementAutoSendSMS",StatementAutoSend.PREF_SMS);
		}

		[TestMethod]
		public void TheRenames_FromTheReleaseV0Rows() {
			Dictionary<string,string> dictExpected=new Dictionary<string,string> {
				{ "ForkApptProvTimeBarsOnOpBackground","ApptProvbarShowOnOps" },
				{ "ForkApptTimeBarSecondaryColorNoProvTime","ApptUseSecProvTimeIfEmpty" },
				{ "ForkClaimMedReceivedPromptForMedicalEob","ClaimCheckForMedicalEOB" },
				{ "ForkEraAutoSplitCarcDenials","EraAutoProcessSplitByCarcCodes" },
				{ "SecurityLockExcludesPayPlans","PayPlansIncludeInGlobalLock" },
				{ "PerioUseRecessionTerm","PerioRecessionInsteadOfGM" },
				{ "ForkReplicationReadDelayMs","ReplicationWriteReadDelayEnabled,ReplicationWriteReadDelayMS" },
				{ "ForkModuleSwitchBlur","BlurModuleOnLoad" },
			};
			CollectionAssert.AreEquivalent(dictExpected.Keys.ToList(),OdSettingNames.ListRenames.Select(x => x.PrefNameV0).ToList());
			foreach(OdSettingRename rename in OdSettingNames.ListRenames) {
				Assert.AreEqual(dictExpected[rename.PrefNameV0],string.Join(",",rename.ListTargets.Select(x => x.PrefNameOd)),rename.PrefNameV0);
			}
		}

		[TestMethod]
		public void PrefName_HoldsNoneOfTheNames_ExceptXRayExposureLevel_AndThePresentOnes() {
			foreach(string name in ArrayNamesFromTheOffice) {
				Assert.AreEqual(name=="XRayExposureLevel",Enum.IsDefined(typeof(PrefName),name),name);
			}
			//Names in the office database that are this program's PrefName members, so they carry over unchanged.
			foreach(string name in OdNames.ArrayInPrefNameAlready) {
				Assert.IsTrue(Enum.IsDefined(typeof(PrefName),name),name);
			}
			//The rows kept under this program's own names: Open Dental 25.3.48 has no setting for them.
			foreach(string name in OdNames.ArrayKeptUnderOurNames) {
				Assert.IsFalse(ArrayNamesFromTheOffice.Contains(name),name);
				Assert.IsFalse(Enum.IsDefined(typeof(PrefName),name),name);
			}
			CollectionAssert.AreEquivalent(new[] { ReplicationDelay.PREF_SIGNAL_LOOKBACK_MS,RpProcNote.PREF_ALL_NO_NOTES,WikiStaticExport.PREF_EXPORT_PATH,
				WikiStaticExport.PREF_AUTO_EXPORT,StatementInserterBarcode.PREF_NAME },OdNames.ArrayKeptUnderOurNames);
		}

		#region The payment plan lock and the read delay
		[TestMethod]
		public void PayPlanLock_IsInverted() {
			Assert.IsFalse(PayPlanGlobalLock.IsOnFromValue(null),"No row: payment plans are locked, as in 24.3.");
			Assert.IsFalse(PayPlanGlobalLock.IsOnFromValue("1"),"Open Dental's 1: included in the global lock.");
			Assert.IsTrue(PayPlanGlobalLock.IsOnFromValue("0"),"Open Dental's 0: not included, so not affected.");
			Assert.IsTrue(PayPlanGlobalLock.IsOnFromValue(""),"Blank reads as 0, as Open Dental's PIn.Bool reads it.");
			Assert.AreEqual("0",PayPlanGlobalLock.GetValueForSetting(true));
			Assert.AreEqual("1",PayPlanGlobalLock.GetValueForSetting(false));
		}

		[TestMethod]
		public void ReadDelay_ComesFromTheTwoRows() {
			Assert.AreEqual(0,ReplicationDelay.GetReadDelayMsFromValues(null,null));
			Assert.AreEqual(0,ReplicationDelay.GetReadDelayMsFromValues(null,"1500"),"No Enabled row: off.");
			Assert.AreEqual(0,ReplicationDelay.GetReadDelayMsFromValues("0","1500"),"Enabled 0: off, whatever the milliseconds.");
			Assert.AreEqual(1500,ReplicationDelay.GetReadDelayMsFromValues("1","1500"));
			Assert.AreEqual(0,ReplicationDelay.GetReadDelayMsFromValues("1",null));
			Assert.AreEqual(ReplicationDelay.MAX_DELAY_MS,ReplicationDelay.GetReadDelayMsFromValues("1","999999"),"Clamped.");
		}
		#endregion The payment plan lock and the read delay

		#region Plan (no database)
		private static List<OdSettingRowChange> Plan(Dictionary<string,string> dictRows) {
			return OdSettingNames.Plan(x => dictRows.TryGetValue(x,out string value) ? value : null);
		}

		private static string Describe(List<OdSettingRowChange> listChanges) {
			return string.Join("; ",listChanges.Select(x => x.Describe()));
		}

		[TestMethod]
		public void Plan_OpenDentalDatabase_ChangesNothing() {
			//The office's rows under Open Dental's names, with made-up values, and no row of release v0.0.
			Dictionary<string,string> dictRows=new Dictionary<string,string> {
				{ "PayPlansIncludeInGlobalLock","1" },{ "PerioRecessionInsteadOfGM","1" },{ "ApptProvbarShowOnOps","0" },
				{ "ReplicationWriteReadDelayEnabled","1" },{ "ReplicationWriteReadDelayMS","1500" },{ "SalesTaxProcCode","Z9901" },
			};
			Assert.AreEqual(0,Plan(dictRows).Count);
			Assert.AreEqual(0,Plan(new Dictionary<string,string>()).Count);
		}

		[TestMethod]
		public void Plan_BoolRows_MoveAndTheDefaultIsNotInserted() {
			Dictionary<string,string> dictRows=new Dictionary<string,string> {
				{ "ForkApptProvTimeBarsOnOpBackground","1" },
				{ "ForkApptTimeBarSecondaryColorNoProvTime","0" },
				{ "PerioUseRecessionTerm","1" },
				{ "ForkModuleSwitchBlur","0" },
			};
			Assert.AreEqual("insert ApptProvbarShowOnOps='1'; delete ForkApptProvTimeBarsOnOpBackground (was '1'); "
				+"delete ForkApptTimeBarSecondaryColorNoProvTime (was '0'); "
				+"insert PerioRecessionInsteadOfGM='1'; delete PerioUseRecessionTerm (was '1'); "
				+"delete ForkModuleSwitchBlur (was '0')",Describe(Plan(dictRows)));
		}

		[TestMethod]
		public void Plan_BothRows_TheReleaseV0RowWins() {
			Dictionary<string,string> dictRows=new Dictionary<string,string> {
				{ "ForkClaimMedReceivedPromptForMedicalEob","0" },{ "ClaimCheckForMedicalEOB","1" },
				{ "ForkEraAutoSplitCarcDenials","1" },{ "EraAutoProcessSplitByCarcCodes","1" },
			};
			Assert.AreEqual("set ClaimCheckForMedicalEOB from '1' to '0'; delete ForkClaimMedReceivedPromptForMedicalEob (was '0'); "
				+"delete ForkEraAutoSplitCarcDenials (was '1')",Describe(Plan(dictRows)));
		}

		[TestMethod]
		public void Plan_PayPlanLock_IsInverted() {
			//v0.0 "not affected" on: Open Dental's row says not included.
			Assert.AreEqual("insert PayPlansIncludeInGlobalLock='0'; delete SecurityLockExcludesPayPlans (was '1')",
				Describe(Plan(new Dictionary<string,string> { { "SecurityLockExcludesPayPlans","1" } })));
			//v0.0 "not affected" off: a missing row already means included.
			Assert.AreEqual("delete SecurityLockExcludesPayPlans (was '0')",
				Describe(Plan(new Dictionary<string,string> { { "SecurityLockExcludesPayPlans","0" } })));
			//Both rows: v0.0 off wins over Open Dental's 0.
			Assert.AreEqual("set PayPlansIncludeInGlobalLock from '0' to '1'; delete SecurityLockExcludesPayPlans (was '0')",
				Describe(Plan(new Dictionary<string,string> { { "SecurityLockExcludesPayPlans","0" },{ "PayPlansIncludeInGlobalLock","0" } })));
			Assert.AreEqual("delete SecurityLockExcludesPayPlans (was '1')",
				Describe(Plan(new Dictionary<string,string> { { "SecurityLockExcludesPayPlans","1" },{ "PayPlansIncludeInGlobalLock","0" } })));
		}

		[TestMethod]
		public void Plan_ReadDelay_FillsBothOpenDentalRows() {
			Assert.AreEqual("insert ReplicationWriteReadDelayEnabled='1'; insert ReplicationWriteReadDelayMS='2000'; delete ForkReplicationReadDelayMs (was '2000')",
				Describe(Plan(new Dictionary<string,string> { { "ForkReplicationReadDelayMs","2000" } })));
			//0: Enabled off, Open Dental's milliseconds kept.
			Assert.AreEqual("set ReplicationWriteReadDelayEnabled from '1' to '0'; delete ForkReplicationReadDelayMs (was '0')",
				Describe(Plan(new Dictionary<string,string> { { "ForkReplicationReadDelayMs","0" },{ "ReplicationWriteReadDelayEnabled","1" },
					{ "ReplicationWriteReadDelayMS","1500" } })));
			Assert.AreEqual("delete ForkReplicationReadDelayMs (was '0')",Describe(Plan(new Dictionary<string,string> { { "ForkReplicationReadDelayMs","0" } })));
			//The same delay already there.
			Assert.AreEqual("delete ForkReplicationReadDelayMs (was '700')",
				Describe(Plan(new Dictionary<string,string> { { "ForkReplicationReadDelayMs","700" },{ "ReplicationWriteReadDelayEnabled","1" },
					{ "ReplicationWriteReadDelayMS","700" } })));
		}

		[TestMethod]
		public void DescribeReads_ShowsWhatThisProgramShows() {
			Dictionary<string,string> dictRows=new Dictionary<string,string> {
				{ "PayPlansIncludeInGlobalLock","1" },{ "PerioRecessionInsteadOfGM","1" },{ "ReplicationWriteReadDelayEnabled","1" },
				{ "ReplicationWriteReadDelayMS","1500" },{ "SalesTaxProcCode","Z9901" },
			};
			List<string> listLines=OdSettingNames.DescribeReads(x => dictRows.TryGetValue(x,out string value) ? value : null);
			Assert.AreEqual(OdSettingNames.ListReads.Count,listLines.Count);
			CollectionAssert.Contains(listLines,"Sales Tax procedure code (B-ACCT): Z9901 [SalesTaxProcCode='Z9901']");
			CollectionAssert.Contains(listLines,"Payment plans are not affected by the global lock (B-PAY): not ticked [PayPlansIncludeInGlobalLock='1']");
			CollectionAssert.Contains(listLines,"Use the term \"Recession\" (B-PERIO): ticked [PerioRecessionInsteadOfGM='1']");
			CollectionAssert.Contains(listLines,"Read delay after write (ms) (B-SERVER): 1500 [ReplicationWriteReadDelayEnabled='1', ReplicationWriteReadDelayMS='1500']");
			CollectionAssert.Contains(listLines,"Blur when switching between modules to prevent accidental clicking (B-SHELL): not ticked [BlurModuleOnLoad no row]");
		}
		#endregion Plan (no database)

		#region The step and the documents
		[TestMethod]
		public void Step_HasItsLevel_AndIsValid() {
			List<ForkMigrationStep> listSteps=ForkMigrationRunner.GetSteps();
			ForkMigrationStep step=listSteps.Single(x => x is OdSettingNamesStep);
			Assert.AreEqual(2026110731,step.Level);
			foreach(ForkMigrationStep stepEach in listSteps) {
				ForkMigrationRunner.ValidateStep(stepEach);
			}
			ForkMigrationRunner.ValidateLevelsUnique(listSteps);
		}

		[TestMethod]
		public void PortNote_NamesEveryNameOfTheOffice_AndTheReadmeTheStep() {
			string note=ScratchDb.ReadRepoFile("docs","port-notes","SWMAP.md");
			foreach(string name in ArrayNamesFromTheOffice.Concat(OdNames.ArrayInPrefNameAlready)) {
				StringAssert.Contains(note,"`"+name+"`",name);
			}
			StringAssert.Contains(note,"2026110731");
			string readme=ScratchDb.ReadRepoFile("OpenPmsDentalBusiness","Misc","ForkMigrations","README.md");
			StringAssert.Contains(readme,"| 2026110731 | `OdSettingNamesStep.cs`");
		}
		#endregion The step and the documents
	}

	///<summary>The names of SWMAP's port note (docs/port-notes/SWMAP.md), sorted the way it sorts them.</summary>
	public static class OdNames {
		///<summary>Read by this program, under Open Dental's names: SWMAP's ten, and the eight options ODOPTS built (2026-11-07).</summary>
		public static readonly string[] ArrayRead={ "SalesTaxProcCode","ClaimCheckForMedicalEOB","ApptProvbarShowOnOps","ApptUseSecProvTimeIfEmpty",
			"EraAutoProcessSplitByCarcCodes","PayPlansIncludeInGlobalLock","PerioRecessionInsteadOfGM","ReplicationWriteReadDelayEnabled",
			"ReplicationWriteReadDelayMS","BlurModuleOnLoad",
			"PerioRedAttGing","DomainLoginForSigBoxes","ClaimUpdateFeeForSecondary","EFormImportAllergiesAsOther","EFormImportProblemsAsOther",
			"InsHistPerioScalingCode","StatementAutoSendEmail","StatementAutoSendSMS" };
		///<summary>Rows older than 24.3, which a database from 24.3 carries too.</summary>
		public static readonly string[] ArrayLegacy={ "B11013Status","ImageStore","LettersIncludeReturnAddress","StationaryDocument","StationaryImage",
			"ShortCodeEConfirmationTemplate","ToothChartLowerQuality","XRayExposureLevel" };
		///<summary>Features this program left out.</summary>
		public static readonly string[] ArrayDropped={ "BetterDiagnosticsEnabled","BrandingUseDefaults","ChatServerCompName","ChatServerDbName",
			"ChatServerMySqlPassHash","ChatServerMySqlUser","CloudApiServiceUrl","EClipboardWebEmailMessageTemplate","EClipboardWebSubjectTemplate",
			"EClipboardWebTextMessageTemplate","MassEmailSignedUp","MiddleTierSerializeNew","MsgToPayMenuOptionShow","PayTypeMessageToPay",
			"NewPatThankYouTypedSignatureDisable","PatientCommunicationTimeFormat","PaymentPortalReceiptEmailBodyTemplate",
			"PaymentPortalReceiptEmailSubjectTemplate","PearlDateTimeLastUsed","RamqMachineID","RamqMachinePassword","RamqMachinePasswordDate",
			"WSEPSubDomain","WebSchedRecallDoUseSecureEmail" };
		///<summary>Settings of features this program built without that option.</summary>
		public static readonly string[] ArrayNotBuilt={ "EnableXConnect" };
		///<summary>Names in the office database that are this program's PrefName members.</summary>
		public static readonly string[] ArrayInPrefNameAlready={ "InsBlueBookUcrFeePercent","PayPlansVersion","ReportsIncompleteProcsNoNotes",
			"ReportsIncompleteProcsUnsigned","ReportsIncompleteProcsExcludeCodes" };
		///<summary>This program's rows for settings Open Dental 25.3.48 does not have.</summary>
		public static readonly string[] ArrayKeptUnderOurNames={ "ForkReplicationSignalLookbackMs","ForkReportsIncompleteProcsAllNoNotes",
			"ForkWikiReadOnlyExportPath","ForkWikiReadOnlyAutoExport","BillingStatementInserterBarcode" };
	}
}
