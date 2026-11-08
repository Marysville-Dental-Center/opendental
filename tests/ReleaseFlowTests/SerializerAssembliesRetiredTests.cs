// Stage 0 of the .NET 10 move (S0-2a, docs/specs/net10-plan.md): OpenPmsDental.XmlSerializers.dll and OpenPmsDentalBusiness.XmlSerializers.dll
// are retired. Release builds ran SGen for every project with WebReferenceUrl items while GenerateSerializationAssemblies was Auto, so cutting
// CallFire (the exe's only web reference) would have silently dropped one file the release required. Both projects now set Off for every
// configuration; the files left the allow-list, and the release channel and the setup retire them like every other retired file.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ReleaseFlowTests {
	[TestClass]
	public class SerializerAssembliesRetiredTests {
		private static readonly string[] _arraySerializerFiles=new[] { "OpenPmsDental.XmlSerializers.dll","OpenPmsDentalBusiness.XmlSerializers.dll" };

		[TestMethod]
		public void BothProjects_GenerateNoSerializationAssemblyInAnyConfiguration() {
			foreach(string[] project in new[] { new[] { "OpenPmsDental","OpenPmsDental.csproj" },new[] { "OpenPmsDentalBusiness","OpenPmsDentalBusiness.csproj" } }) {
				XmlDocument xmlDocument=new XmlDocument();
				xmlDocument.LoadXml(TestPaths.ReadSource(project));
				List<XmlElement> listSettings=xmlDocument.GetElementsByTagName("GenerateSerializationAssemblies").OfType<XmlElement>().ToList();
				Assert.AreEqual(1,listSettings.Count,project[1]+": one setting, so no configuration overrides it.");
				Assert.AreEqual("Off",listSettings[0].InnerText,project[1]);
				XmlElement xmlElementGroup=(XmlElement)listSettings[0].ParentNode;
				Assert.AreEqual("PropertyGroup",xmlElementGroup.LocalName);
				Assert.IsFalse(xmlElementGroup.HasAttribute("Condition"),project[1]+": set for every configuration.");
			}
		}

		[TestMethod]
		public void SerializerFiles_AreRetired() {
			foreach(string fileName in _arraySerializerFiles) {
				CollectionAssert.Contains(ForkReleases.ListRetiredFiles,fileName);
				Assert.IsFalse(ForkReleases.ShouldPublishFile(fileName),"Never published again: "+fileName);
				Assert.IsFalse(ForkReleases.ListBuildIdFiles.Contains(fileName,StringComparer.OrdinalIgnoreCase),fileName);
			}
			string buildRelease=TestPaths.ReadSource("tools","Build-Release.ps1");
			foreach(string fileName in _arraySerializerFiles) {
				Assert.IsFalse(buildRelease.Contains("'"+fileName+"'="),"Still on the allow-list ($OwnFiles): "+fileName);
				StringAssert.Contains(buildRelease,"@('"+fileName+"','pre-built XML serializers of v0.0, retired in stage 0");
			}
			string iss=TestPaths.ReadSource("installer","OpenPmsDental.iss");
			foreach(string fileName in _arraySerializerFiles) {
				StringAssert.Contains(iss,"Type: files; Name: \"{app}\\"+fileName+"\"");
			}
		}
	}
}
