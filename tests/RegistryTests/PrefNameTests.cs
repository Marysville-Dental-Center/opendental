using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace RegistryTests {
	///<summary>Preference names added by Open Dental 24.4 to 25.3. Only names that Open Dental's published 25.3 database documentation states are
	///registered in PrefName. The names the feature specs only proposed are listed in ArrayUnconfirmedProposedNames and are never registered. The
	///setting names of the office's Open Dental 25.3.48 database (owner question Switch-over 1, answered 2026-11-07; docs/port-notes/SWMAP.md) gave
	///Open Dental's own names for nine of those settings, which this program now reads by name (DictOpenDentalRowByProposedName).</summary>
	[TestClass]
	public class PrefNameTests {
		///<summary>24.4 to 25.3 preference names that the published 25.3 database documentation states (in column descriptions).</summary>
		public static readonly string[] ArrayConfirmedNames=new[] { "ApptMirrorSecondary","TaskCategoryShow" };

		///<summary>Names the specs proposed for the 24.4 to 25.3 settings. Registering a guessed name would keep the office's real setting from
		///carrying over, so none of them is in PrefName.</summary>
		public static readonly string[] ArrayUnconfirmedProposedNames=new[] {
			"SalesTaxProcCode","PerioUseRecessionTerm","ReportsIncompleteProcsAllNoNotes","ChartLetterDefaultCategory",//24.4
			"EraAutoSplitCarcDenials",//25.1
			"ClaimMedReceivedPromptForMedicalEob","ApptProvTimeBarsOnOpBackground","ReplicationReadDelayMs","ReplicationSignalLookbackMs",
			"WikiReadOnlyExportPath","WikiReadOnlyAutoExport","ModuleSwitchBlur",//25.2
			"SecurityLockExcludesPayPlans","ApptTimeBarSecondaryColorNoProvTime","BillingStatementInserterBarcode",//25.3
		};

		///<summary>For each proposed name whose setting the office's Open Dental 25.3.48 database has under Open Dental's own name, the row this program
		///reads it from now: Open Dental's, so the office's value carries over as it is. Read by name, outside PrefName (a database from 24.3 has no
		///such row, and a missing row reads as the default).</summary>
		public static readonly Dictionary<string,string> DictOpenDentalRowByProposedName=new Dictionary<string,string> {
			{ "SalesTaxProcCode",SalesTaxProcs.PREF_PROC_CODE },//B-ACCT, the same name
			{ "PerioUseRecessionTerm",PerioMeasures.PrefNameUseRecessionTerm },//B-PERIO
			{ "EraAutoSplitCarcDenials",EraAutoSplit.PREF_NAME },//B-ERA
			{ "ClaimMedReceivedPromptForMedicalEob",ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT },//B-CLAIM
			{ "ApptProvTimeBarsOnOpBackground",ApptTimeBars.PREF_PROV_TIME_ON_OP_BACKGROUND },//B-APPT
			{ "ApptTimeBarSecondaryColorNoProvTime",ApptTimeBars.PREF_SECONDARY_COLOR_NO_PROV_TIME },//B-APPT
			{ "ReplicationReadDelayMs",ReplicationDelay.PREF_READ_DELAY_MS },//B-SERVER, with ReplicationWriteReadDelayEnabled
			{ "ModuleSwitchBlur",ModuleSwitchBlur.PREF_NAME },//B-SHELL
			{ "SecurityLockExcludesPayPlans",PayPlanGlobalLock.PREF_NAME },//B-PAY, inverted
		};

		///<summary>For each proposed name whose setting is built but which the office's Open Dental 25.3.48 database has no setting for, the row this
		///program keeps it in. It starts at its default after adoption, so docs/adoption-runbook.md step 0.11 lists each row for the office to look for
		///in Open Dental before the day and set again in step 7.1.</summary>
		public static readonly Dictionary<string,string> DictRowReadByUnconfirmedName=new Dictionary<string,string> {
			{ "ReportsIncompleteProcsAllNoNotes",RpProcNote.PREF_ALL_NO_NOTES },//B-SERVER
			{ "ReplicationSignalLookbackMs",ReplicationDelay.PREF_SIGNAL_LOOKBACK_MS },//B-SERVER
			{ "WikiReadOnlyExportPath",WikiStaticExport.PREF_EXPORT_PATH },//B-WIKI
			{ "WikiReadOnlyAutoExport",WikiStaticExport.PREF_AUTO_EXPORT },//B-WIKI
			{ "BillingStatementInserterBarcode",StatementInserterBarcode.PREF_NAME },//B-PRINT
		};
		///<summary>Unconfirmed names of settings this program does not build, so there is nothing to carry over.</summary>
		public static readonly string[] ArrayUnconfirmedNamesNotBuilt=new[] { "ChartLetterDefaultCategory" };

		[TestMethod]
		public void ProposedNames_EachIsReadUnderOpenDentalsName_OrRecordedAndSetAgainByTheAdoptionRunbook() {
			//Combined review (2026-11-01): the runbook said nothing about these settings, so an office that had one on in Open Dental 25.3.48 lost it.
			//SWMAP (2026-11-07): those the office database has are read under Open Dental's names; step 0.11 keeps the rest.
			CollectionAssert.AreEquivalent(ArrayUnconfirmedProposedNames,DictOpenDentalRowByProposedName.Keys.Concat(DictRowReadByUnconfirmedName.Keys)
				.Concat(ArrayUnconfirmedNamesNotBuilt).ToArray(),
				"Every proposed name is read under Open Dental's name, or built (with the row it is read from), or listed as not built.");
			string runbook=File.ReadAllText(TestPaths.GetRepoFile("docs","adoption-runbook.md"));
			int idxStep=runbook.IndexOf("0.11. **Settings that do not carry over**");
			Assert.IsTrue(idxStep>0,"Step 0.11 records the settings before the day.");
			int idxStepEnd=runbook.IndexOf("\n## ",idxStep);
			string step=runbook.Substring(idxStep,idxStepEnd-idxStep);
			foreach(KeyValuePair<string,string> pair in DictRowReadByUnconfirmedName) {
				Assert.AreEqual(1,step.Split(new[] { "| `"+pair.Value+"` (" },StringSplitOptions.None).Length-1,
					"Step 0.11 has one table row for "+pair.Value+" (the setting the spec proposed to call "+pair.Key+").");
			}
			foreach(KeyValuePair<string,string> pair in DictOpenDentalRowByProposedName) {
				Assert.IsFalse(step.Contains("| `"+pair.Value+"` ("),"Step 0.11 has no row for "+pair.Value+": it carries over.");
				StringAssert.Contains(runbook,"`"+pair.Value+"`","The runbook names "+pair.Value+" among the settings that carry over.");
			}
			StringAssert.Contains(runbook,"7.1. **[workstation] The settings of step 0.11.**","Step 7.1 sets the recorded values again.");
			StringAssert.Contains(runbook,"| 0.11 | Settings that do not carry over","The checklist has the recording.");
			StringAssert.Contains(runbook,"| 7.1 | Recorded settings set again","The checklist has the setting again.");
		}

		[TestMethod]
		public void ProposedNames_AreNotRegistered_AndOpenDentalsNamesAreReadByName() {
			foreach(string name in ArrayUnconfirmedProposedNames) {
				Assert.IsFalse(Enum.IsDefined(typeof(PrefName),name),name+" is only a proposed name; it is not added to PrefName.");
			}
			foreach(KeyValuePair<string,string> pair in DictOpenDentalRowByProposedName) {
				Assert.IsFalse(Enum.IsDefined(typeof(PrefName),pair.Value),pair.Value+" is read by name, so a database without the row reads as the default.");
			}
			CollectionAssert.AreEquivalent(new[] { "SalesTaxProcCode","PerioRecessionInsteadOfGM","EraAutoProcessSplitByCarcCodes","ClaimCheckForMedicalEOB",
				"ApptProvbarShowOnOps","ApptUseSecProvTimeIfEmpty","ReplicationWriteReadDelayMS","BlurModuleOnLoad","PayPlansIncludeInGlobalLock" },
				DictOpenDentalRowByProposedName.Values.ToArray());
		}

		[TestMethod]
		public void ConfirmedNames_AreInPrefName() {
			foreach(string name in ArrayConfirmedNames) {
				Assert.IsTrue(Enum.IsDefined(typeof(PrefName),name),name+" is missing from PrefName.");
			}
		}

		[TestMethod]
		public void ConfirmedNames_HavePrefInfosEntries() {
			XDocument xDocument=XDocument.Load(TestPaths.GetRepoFile("OpenPmsDental","Resources","PrefInfos.xml"));
			List<XElement> listPrefInfs=xDocument.Root.Elements("PrefInf").ToList();
			HashSet<string> setCategories=new HashSet<string>(Enum.GetNames(typeof(EnumPrefInfoCategory)));
			foreach(string name in ArrayConfirmedNames) {
				List<XElement> listMatches=listPrefInfs.FindAll(x => (string)x.Element("PrefName")==name);
				Assert.AreEqual(1,listMatches.Count,name+" needs exactly one PrefInfos.xml entry.");
				Assert.IsTrue(setCategories.Contains((string)listMatches[0].Element("Category")),name+" has an unknown category.");
				Assert.IsFalse(string.IsNullOrWhiteSpace((string)listMatches[0].Element("ControlText")),name+" needs ControlText.");
			}
		}

		[TestMethod]
		public void ConfirmedNames_AreBoolAndSeededOff() {
			foreach(string name in ArrayConfirmedNames) {
				PrefName prefName=(PrefName)Enum.Parse(typeof(PrefName),name);
				Assert.AreEqual("0",Seed25_3.ListPrefDefaults.Single(x => x.PrefName==prefName).ValueString,name+" defaults to off.");
			}
			Assert.AreEqual(ArrayConfirmedNames.Length,Seed25_3.ListPrefDefaults.Count);
		}
	}
}
