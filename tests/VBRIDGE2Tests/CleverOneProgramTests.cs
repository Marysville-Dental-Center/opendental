// Clever One by VATECH (Open Dental 25.4, v25_4-bridges-cleverone): the linkage.xml hand-off and the program link defaults. Nothing is launched.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect.ProgramLinks;

namespace VBRIDGE2Tests {
	[TestClass]
	public class CleverOneProgramTests {
		[TestMethod]
		public void LinkageXml_HasThePatientInTheEzDentiLayout() {
			string xml=CleverOneProgram.BuildLinkageXml(SyntheticPatients.Create(),useChartNumber:false);
			XElement root=XElement.Parse(xml);
			Assert.AreEqual("LinkageParameter",root.Name.LocalName);
			XElement patient=root.Element("Patient");
			Assert.IsNotNull(patient);
			Assert.AreEqual("O'Testa",patient.Attribute("LastName").Value);
			Assert.AreEqual("Ann \"Annie\"",patient.Attribute("FirstName").Value,"XML escaping keeps quotes intact.");
			Assert.AreEqual("1042",patient.Attribute("ChartNumber").Value,"Property 0 sends the PatNum.");
			Assert.AreEqual("09/03/1980",patient.Element("Birthday").Value);
			Assert.AreEqual("12 Sample St, Unit 4, Exampleton, WA",patient.Element("Address").Value);
			Assert.AreEqual("98000",patient.Element("ZipCode").Value);
			Assert.AreEqual("(555)010-0001",patient.Element("Phone").Value);
			Assert.AreEqual("(555)010-0002",patient.Element("Mobile").Value);
			Assert.AreEqual("Female",patient.Element("Gender").Value);
			CollectionAssert.AreEqual(new[] { "Birthday","Address","ZipCode","Phone","Mobile","Gender" },
				patient.Elements().Select(x => x.Name.LocalName).ToArray());
		}

		[TestMethod]
		public void LinkageXml_NeverHasTheSocialSecurityNumber() {
			Patient patient=SyntheticPatients.Create();
			string xml=CleverOneProgram.BuildLinkageXml(patient,useChartNumber:true);
			Assert.IsFalse(xml.Contains("SocialID"),"EzDent-i's SocialID element is dropped for Clever One.");
			Assert.IsFalse(xml.Contains(patient.SSN));
		}

		[TestMethod]
		public void LinkageXml_ChartNumberWhenThePropertyIsOne() {
			XElement patient=XElement.Parse(CleverOneProgram.BuildLinkageXml(SyntheticPatients.Create(),useChartNumber:true)).Element("Patient");
			Assert.AreEqual("CH-77",patient.Attribute("ChartNumber").Value);
		}

		[TestMethod]
		public void LinkageXml_FormatIgnoresRegionalSettings() {
			CultureInfo cultureOld=Thread.CurrentThread.CurrentCulture;
			try {
				Thread.CurrentThread.CurrentCulture=new CultureInfo("de-DE");
				string xmlDe=CleverOneProgram.BuildLinkageXml(SyntheticPatients.Create(),false);
				Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
				string xmlUs=CleverOneProgram.BuildLinkageXml(SyntheticPatients.Create(),false);
				Assert.AreEqual(xmlUs,xmlDe);
				StringAssert.Contains(xmlDe,"<Birthday>09/03/1980</Birthday>");
			}
			finally {
				Thread.CurrentThread.CurrentCulture=cultureOld;
			}
		}

		[TestMethod]
		public void LinkageXml_NoDeclarationThreeSpaceIndentCrLf() {
			string xml=CleverOneProgram.BuildLinkageXml(SyntheticPatients.Create(),false);
			Assert.IsTrue(xml.StartsWith("<LinkageParameter>\r\n   <Patient "),xml);
			Assert.IsFalse(xml.Contains("<?xml"));
		}

		[TestMethod]
		public void LinkageXml_MaleAndUnknownGenderAreMale() {
			Patient patient=SyntheticPatients.Create();
			patient.Gender=PatientGender.Male;
			StringAssert.Contains(CleverOneProgram.BuildLinkageXml(patient,false),"<Gender>Male</Gender>");
			patient.Gender=PatientGender.Unknown;
			StringAssert.Contains(CleverOneProgram.BuildLinkageXml(patient,false),"<Gender>Male</Gender>","As the EzDent-i bridge.");
		}

		[TestMethod]
		public void AddressLine_SkipsABlankAddress2() {
			Patient patient=SyntheticPatients.Create();
			patient.Address2="";
			Assert.AreEqual("12 Sample St, Exampleton, WA",CleverOneProgram.GetAddressLine(patient));
		}

		[TestMethod]
		public void LinkageFile_IsNextToTheProgram() {
			Assert.AreEqual(@"C:\Program Files\VATECH\CleverOne\Bin\linkage.xml",CleverOneProgram.GetLinkageFilePath(CleverOneProgram.DefaultPath));
			Assert.AreEqual("",CleverOneProgram.GetLinkageFilePath(""));
		}

		[TestMethod]
		public void UsesChartNumber_NoProgramSendsThePatNum() {
			//The rule itself is BridgePatientTokens.IsChartNumberSelected (only "1"), shared with the V-BRIDGE1 links; ProgramLinksDbTests reads it
			//from a saved property.
			Assert.IsFalse(CleverOneProgram.UsesChartNumber(null));
			Assert.IsTrue(BridgePatientTokens.IsChartNumberSelected(" 1 "));
			Assert.IsFalse(BridgePatientTokens.IsChartNumberSelected(""));
			Assert.IsFalse(BridgePatientTokens.IsChartNumberSelected("2"));
		}

		[TestMethod]
		public void Definition_HasTheSetupPageDefaults() {
			BridgeLinkDefinition definition=CleverOneProgram.GetDefinition();
			Assert.AreEqual(ProgramName.CleverOne,definition.ProgName);
			Assert.AreEqual("Clever One by VATECH",definition.ProgDesc);
			Assert.AreEqual(CleverOneProgram.DefaultPath,definition.Path);
			Assert.AreEqual("",definition.CommandLine);
			Assert.AreEqual("Clever One",definition.ButtonText);
			CollectionAssert.AreEqual(new[] { EnumToolBar.ChartModule },definition.ListToolBars.ToArray());
			Assert.AreEqual(1,definition.ListProperties.Count);
			Assert.AreEqual("Enter 0 to use PatientNum, or 1 to use ChartNum",definition.ListProperties[0].Key);
			Assert.AreEqual("0",definition.ListProperties[0].Value);
			StringAssert.Contains(definition.Note,"linkage.xml");
			Assert.IsFalse(definition.Note.Contains("http"),"The note names no web address.");
		}
		[TestMethod]
		public void Defaults_MatchThePublicSetupPage() {
			Assert.AreEqual(@"C:\Program Files\VATECH\CleverOne\Bin\CleverOne.exe",CleverOneProgram.DefaultPath);
			Assert.AreEqual("Clever One",CleverOneProgram.DefaultButtonText);
			Assert.AreEqual(EnumToolBar.ChartModule,CleverOneProgram.DefaultToolBar);
			Assert.AreEqual("Enter 0 to use PatientNum, or 1 to use ChartNum",CleverOneProgram.PropPatOrChartNum);
		}

		[TestMethod]
		public void BuildLinkageXml_NeedsAPatient() {
			Expect.Throws<ArgumentNullException>(() => CleverOneProgram.BuildLinkageXml(null,false));
		}
	}
}
