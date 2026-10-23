using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace RegistryTests {
	///<summary>Preference names added by Open Dental 24.4 to 25.3. There is no census of the office database, so only names that Open Dental's
	///published 25.3 database documentation states are registered. The names the feature specs only propose are listed in
	///ListUnconfirmedProposedNames and must not be registered until they are confirmed.</summary>
	[TestClass]
	public class PrefNameTests {
		///<summary>24.4 to 25.3 preference names that the published 25.3 database documentation states (in column descriptions).</summary>
		public static readonly string[] ArrayConfirmedNames=new[] { "ApptMirrorSecondary","TaskCategoryShow" };

		///<summary>Names the specs propose but no public source confirms. Registering a guessed name would keep the office's real setting from
		///carrying over, so these stay out of PrefName until a public source (or the owner) confirms them.</summary>
		public static readonly string[] ArrayUnconfirmedProposedNames=new[] {
			"SalesTaxProcCode","PerioUseRecessionTerm","ReportsIncompleteProcsAllNoNotes","ChartLetterDefaultCategory",//24.4
			"EraAutoSplitCarcDenials",//25.1
			"ClaimMedReceivedPromptForMedicalEob","ApptProvTimeBarsOnOpBackground","ReplicationReadDelayMs","ReplicationSignalLookbackMs",
			"WikiReadOnlyExportPath","WikiReadOnlyAutoExport","ModuleSwitchBlur",//25.2
			"SecurityLockExcludesPayPlans","ApptTimeBarSecondaryColorNoProvTime","BillingStatementInserterBarcode",//25.3
		};

		[TestMethod]
		public void ConfirmedNames_AreInPrefName() {
			foreach(string name in ArrayConfirmedNames) {
				Assert.IsTrue(Enum.IsDefined(typeof(PrefName),name),name+" is missing from PrefName.");
			}
		}

		[TestMethod]
		public void UnconfirmedNames_AreNotRegistered() {
			foreach(string name in ArrayUnconfirmedProposedNames) {
				Assert.IsFalse(Enum.IsDefined(typeof(PrefName),name),name+" is only a proposed name; confirm it before adding it to PrefName.");
			}
		}

		[TestMethod]
		public void ConfirmedNames_HavePrefInfosEntries() {
			XDocument xDocument=XDocument.Load(TestPaths.GetRepoFile("OpenDental","Resources","PrefInfos.xml"));
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
