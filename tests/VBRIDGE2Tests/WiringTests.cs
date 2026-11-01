// How the V-BRIDGE2 program links are wired into the program: the ProgramName values, the button dispatch, the Program Links registry, the
// project files and the neutrality rules. Source checks for the parts in OpenDental.exe and WpfControlsOD.dll, which this test project does not load.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace VBRIDGE2Tests {
	[TestClass]
	public class WiringTests {
		[TestMethod]
		public void ProgramName_NewValuesAreAppendedAfterVBridge1_ExistingNumbersKept() {
			string[] arrayNames=Enum.GetNames(typeof(ProgramName));
			//Appended after the V-BRIDGE1 links, the way every program link added since 24.3 is, so no existing value changes its number.
			int indexDtxStudioAcquire=Array.IndexOf(arrayNames,"DTXStudioAcquire");
			CollectionAssert.AreEqual(new[] { "DTXStudioAcquire","Clerri","CleverOne" },arrayNames.Skip(indexDtxStudioAcquire).Take(3).ToArray());
			Assert.AreEqual((int)ProgramName.DTXStudioAcquire+1,(int)ProgramName.Clerri);
			Assert.AreEqual((int)ProgramName.Clerri+1,(int)ProgramName.CleverOne);
			//The 24.3 values and the vendor-direct foundation's keep their numbers (VendorDirectTests.FoundationLayoutTests, VBRIDGE1Tests).
			Assert.AreEqual((int)ProgramName.ZImage+1,(int)ProgramName.Microsoft365Email);
			Assert.AreEqual((int)ProgramName.CleaRay+1,(int)ProgramName.CliniView,"Nothing is inserted between the 24.3 values.");
		}

		[TestMethod]
		public void ProgramL_DispatchesBothNewLinks() {
			string source=TestPaths.ReadRepoFile("WpfControlsOD","Logic","ProgramL.cs");
			StringAssert.Contains(source,"program.ProgName==ProgramName.Clerri.ToString()");
			StringAssert.Contains(source,"Clerri.SendData(program,patient);");
			StringAssert.Contains(source,"program.ProgName==ProgramName.CleverOne.ToString()");
			StringAssert.Contains(source,"CleverOne.SendData(program,patient);");
		}

		[TestMethod]
		public void TrophyEnhanced_UsesTheCommandLine() {
			string source=TestPaths.ReadRepoFile("WpfControlsOD","Bridges","TrophyEnhanced.cs");
			StringAssert.Contains(source,"TrophyEnhancedCommandLine.SkipsFolders(ProgramCur.CommandLine,storagePath)");
			StringAssert.Contains(source,"TrophyEnhancedCommandLine.Build(ProgramCur.CommandLine,pat,patFolder,");
			StringAssert.Contains(source,"Patients.ReplacePatient(text,patient)");
			//The 24.3 arguments are still there for a blank Command line.
			StringAssert.Contains(source,"string comline=\"-P\"+patFolder");
		}

		[TestMethod]
		public void Bridges_AreCompiled() {
			string csproj=TestPaths.ReadRepoFile("WpfControlsOD","WpfControlsOD.csproj");
			StringAssert.Contains(csproj,"<Compile Include=\"Bridges\\Clerri.cs\" />");
			StringAssert.Contains(csproj,"<Compile Include=\"Bridges\\CleverOne.cs\" />");
			string csprojProgram=TestPaths.ReadRepoFile("OpenDental","OpenDental.csproj");
			StringAssert.Contains(csprojProgram,"<Compile Include=\"Forms\\FormClerriSetup.cs\">");
			StringAssert.Contains(csprojProgram,"<Compile Include=\"Forms\\FormClerriSetup.Designer.cs\">");
			StringAssert.Contains(csprojProgram,"<EmbeddedResource Include=\"Forms\\FormClerriSetup.resx\">");
			StringAssert.Contains(csprojProgram,"<Compile Include=\"Forms\\FormProgramLinkEdit.Bridges2.cs\">");
			StringAssert.Contains(csprojProgram,"<Compile Include=\"Logic\\VendorDirect\\CleverOneProgramLinkSetup.cs\" />");
			//The business classes are picked up by the VendorDirect wildcard, so OpenDentBusiness.csproj needs no line of its own.
			StringAssert.Contains(TestPaths.ReadRepoFile("OpenDentBusiness","OpenDentBusiness.csproj"),"<Compile Include=\"VendorDirect\\**\\*.cs\" />");
		}

		[TestMethod]
		public void ProgramLinks_RegistersBothSetups() {
			//ProgramLinkSetupRegistry finds IProgramLinkSetup implementations in OpenDental.exe by reflection.
			string clerri=TestPaths.ReadRepoFile("OpenDental","Forms","FormClerriSetup.cs");
			StringAssert.Contains(clerri,"public class ClerriProgramLinkSetup:IProgramLinkSetup");
			StringAssert.Contains(clerri,"return ProgramName.Clerri;");
			StringAssert.Contains(clerri,"return ClerriProgram.EnsureProgram();");
			string cleverOne=TestPaths.ReadRepoFile("OpenDental","Logic","VendorDirect","CleverOneProgramLinkSetup.cs");
			//Clever One opens in the standard Program Link window through V-BRIDGE1's StandardProgramLinkSetup.
			StringAssert.Contains(cleverOne,"public class CleverOneProgramLinkSetup:StandardProgramLinkSetup");
			StringAssert.Contains(cleverOne,"return ProgramName.CleverOne;");
			StringAssert.Contains(cleverOne,"return CleverOneProgram.EnsureProgram();");
			StringAssert.Contains(TestPaths.ReadRepoFile("OpenDental","Logic","VendorDirect","BridgeProgramLinkSetups.cs"),
				"public abstract class StandardProgramLinkSetup:IProgramLinkSetup");
		}

		[TestMethod]
		public void ClerriSetup_SavesTheKeyEncryptedAndLocksTheButtonWhileEnabled() {
			string source=TestPaths.ReadRepoFile("OpenDental","Forms","FormClerriSetup.cs");
			StringAssert.Contains(source,"VendorCredentials.WriteSecret(_program.ProgramNum,ClerriProgram.PropClientKey,");
			StringAssert.Contains(source,"ClerriProgram.CanChangeToolbarButton(_program.Enabled,checkEnabled.Checked)");
			//Show Toolbar Button is saved from the saved state (ClerriProgram.SaveToolbarButtonAndEnabled), never from whether the box is editable.
			StringAssert.Contains(source,"ClerriProgram.SaveToolbarButtonAndEnabled(_program,checkShowToolbarButton.Checked,checkEnabled.Checked);");
			Assert.IsFalse(source.Contains("checkShowToolbarButton.Enabled &&"),"The locked box's value is saved, not skipped.");
			StringAssert.Contains(source,"ProgramProperties.CanEditProperties(");
			Assert.IsFalse(source.Contains("ProgramProperties.Update("),"The key is written through VendorCredentials only.");
			string designer=TestPaths.ReadRepoFile("OpenDental","Forms","FormClerriSetup.Designer.cs");
			StringAssert.Contains(designer,"this.textClientKey.PasswordChar = '*';");
			StringAssert.Contains(designer,"this.textProgName.ReadOnly = true;");
			StringAssert.Contains(designer,"this.textProgDesc.ReadOnly = true;");
		}

		[TestMethod]
		public void FormProgramLinkEdit_HasOneHookLine() {
			string source=TestPaths.ReadRepoFile("OpenDental","Forms","FormProgramLinkEdit.cs");
			Assert.AreEqual(1,Regex.Matches(source,Regex.Escape("SetCommandLineHint();")).Count);
			string partial=TestPaths.ReadRepoFile("OpenDental","Forms","FormProgramLinkEdit.Bridges2.cs");
			StringAssert.Contains(partial,"ProgramName.TrophyEnhanced");
			StringAssert.Contains(partial,"TrophyEnhancedCommandLine.CommandLineHint");
		}

		[TestMethod]
		public void LinkageXml_IsScrubbedOnExitLikeEzDenti() {
			string source=TestPaths.ReadRepoFile("OpenDentBusiness","Data Interface","Programs.cs");
			StringAssert.Contains(source,"program=Programs.GetCur(ProgramName.CleverOne);\r\n\t\t\tRemoveLinkageXMLFile(program);".Replace("\r\n",
				source.Contains("\r\n") ? "\r\n" : "\n"));
		}

		[TestMethod]
		public void NewFiles_HaveNoVendorAddressesOrPromotion() {
			string[][] arrayFiles={
				new[] { "WpfControlsOD","Bridges","Clerri.cs" },
				new[] { "WpfControlsOD","Bridges","CleverOne.cs" },
				new[] { "OpenDental","Forms","FormClerriSetup.cs" },
				new[] { "OpenDental","Forms","FormClerriSetup.Designer.cs" },
				new[] { "OpenDental","Logic","VendorDirect","CleverOneProgramLinkSetup.cs" },
				new[] { "OpenDentBusiness","VendorDirect","ProgramLinks","ClerriProgram.cs" },
				new[] { "OpenDentBusiness","VendorDirect","ProgramLinks","CleverOneProgram.cs" },
				new[] { "OpenDentBusiness","VendorDirect","ProgramLinks","TrophyEnhancedCommandLine.cs" },
			};
			foreach(string[] arrayParts in arrayFiles) {
				string source=TestPaths.ReadRepoFile(arrayParts);
				string name=string.Join("/",arrayParts);
				//No vendor web address: the office types the Clerri address it gets from Clerri.
				Assert.IsFalse(Regex.IsMatch(source,@"https?://[A-Za-z0-9]"),name+" holds a web address.");
				Assert.IsFalse(Regex.IsMatch(source,@"\b(sign up|signup|free trial|learn more|www\.)",RegexOptions.IgnoreCase),name+" promotes a vendor.");
				//Open Dental hosts are checked for the whole repository by tools/Test-NoHqHosts.ps1.
			}
		}
	}
}
