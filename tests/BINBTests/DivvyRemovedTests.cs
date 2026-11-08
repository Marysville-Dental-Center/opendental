// Stage 0 of the .NET 10 move (S0-2a, docs/specs/net10-plan.md, decision 11): Divvy postcards are cut. The WCF client (System.ServiceModel),
// its endpoint in OpenPmsDental's app.config and the eCards button of the Recall List go; ProgramName.Divvy and the program link row stay.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BINBTests {
	[TestClass]
	public class DivvyRemovedTests {
		[TestMethod]
		public void DivvyClientAndItsConfiguration_AreGone() {
			Assert.IsFalse(File.Exists(Path.Combine(TestPaths.RepoRoot,"WpfControlsOD","Bridges","DivvyPostcardService.cs")));
			string wpfControls=TestPaths.ReadSource("WpfControlsOD","WpfControlsOD.csproj");
			Assert.IsFalse(wpfControls.Contains("DivvyPostcardService"));
			Assert.IsFalse(wpfControls.Contains("<Reference Include=\"System.ServiceModel\" />"),"Nothing else in WpfControlsOD uses WCF.");
			string appConfig=TestPaths.ReadSource("OpenPmsDental","app.config");
			Assert.IsFalse(appConfig.Contains("system.serviceModel"),"The Divvy endpoint and binding were the only WCF configuration.");
			Assert.IsFalse(appConfig.IndexOf("divvy",StringComparison.OrdinalIgnoreCase)>=0);
		}

		[TestMethod]
		public void RecallList_HasNoECardsButton() {
			foreach(string fileName in new[] { "FormRecallList.cs","FormRecallList.designer.cs" }) {
				string source=TestPaths.ReadSource("OpenPmsDental","Forms",fileName);
				Assert.IsFalse(source.Contains("butECards"),fileName);
				Assert.IsFalse(source.Contains("DivvyConnect") || source.Contains("ProgramName.Divvy"),fileName);
			}
		}

		[TestMethod]
		public void ProgramName_Stays() {
			//program.ProgName rows name it.
			Assert.IsTrue(Enum.IsDefined(typeof(ProgramName),"Divvy"));
		}
	}
}
