// Source wiring of the P262-LINKS changes that the test run cannot reach through the windows: the ProgramName values, the ProgramL dispatch, the
// Program Links setups, the project files, the exit scrub, the removed Suni support and the kept Ez3D-i link.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using ProjectFileTests;

namespace P262LinksTests {
	[TestClass]
	public class WiringTests {
		[TestMethod]
		public void ProgramName_NewValuesAreAppendedLast_ExistingNumbersKept() {
			List<string> listNames=Enum.GetNames(typeof(ProgramName)).ToList();
			int indexMeet=listNames.IndexOf("MeetInTheCloud");
			Assert.IsTrue(indexMeet>0);
			//Only the first two names after MeetInTheCloud: P262-GLIDEWELL appends Glidewell after them (docs/port-notes/P262-GLIDEWELL.md).
			CollectionAssert.AreEqual(new[] { "AlliedstarConnect","OrthoComposite" },listNames.Skip(indexMeet+1).Take(2).ToList(),"Appended after Global Payments V2.");
			Assert.AreEqual(indexMeet+1,(int)ProgramName.AlliedstarConnect);
			Assert.IsTrue(listNames.IndexOf("CleverOne")<indexMeet && listNames.IndexOf("DTXStudioAcquire")<listNames.IndexOf("CleverOne"));
		}

		[TestMethod]
		public void ProgramL_DispatchesTheNewLinks_AfterTheDiscontinuedCheck() {
			string programL=TestPaths.ReadRepoFile("WpfControlsOD","Logic","ProgramL.cs");
			int indexDiscontinued=programL.IndexOf("ProgramLinksDiscontinued.IsDiscontinued(program)",StringComparison.Ordinal);
			Assert.IsTrue(indexDiscontinued>0);
			foreach(string pair in new[] { "AlliedstarConnect|AlliedstarConnect.SendData(program,patient);","OrthoComposite|OrthoComposite.SendData(program,patient);" }) {
				string[] arrayParts=pair.Split('|');
				int indexBranch=programL.IndexOf("program.ProgName==ProgramName."+arrayParts[0]+".ToString()",StringComparison.Ordinal);
				Assert.IsTrue(indexBranch>indexDiscontinued,arrayParts[0]);
				int indexCall=programL.IndexOf(arrayParts[1],indexBranch,StringComparison.Ordinal);
				Assert.IsTrue(indexCall>indexBranch && indexCall-indexBranch<120,"The branch calls its bridge: "+arrayParts[0]);
			}
			//The Ez3D-i link stays (owner decision), dispatched with EzDenti.
			StringAssert.Contains(programL,"ProgramName.Ez3Di.ToString()");
		}

		[TestMethod]
		public void Bridges_UseTheBusinessRules() {
			string allied=TestPaths.ReadRepoFile("WpfControlsOD","Bridges","AlliedstarConnect.cs");
			StringAssert.Contains(allied,"AlliedstarConnectLink.GetLaunch(program,patient)");
			StringAssert.Contains(allied,"new UTF8Encoding(false)");
			//The file is written or the stale file removed before Connect starts, and a failure there stops the launch.
			int indexWrite=allied.IndexOf("File.WriteAllText(",StringComparison.Ordinal);
			int indexDelete=allied.IndexOf("File.Delete(",StringComparison.Ordinal);
			int indexStart=allied.IndexOf("ODFileUtils.ProcessStart(",StringComparison.Ordinal);
			Assert.IsTrue(indexWrite>0 && indexDelete>0 && indexStart>indexWrite && indexStart>indexDelete);
			string catchBlock=allied.Substring(indexWrite,indexStart-indexWrite);
			StringAssert.Contains(catchBlock,"return;","A file that could not be written or removed stops the launch.");
			Assert.IsFalse(allied.Contains("CreateDirectory"),"The link never creates the Info file path's folders (public page).");
			string ortho=TestPaths.ReadRepoFile("WpfControlsOD","Bridges","OrthoComposite.cs");
			StringAssert.Contains(ortho,"OrthoCompositeLink.GetLaunch(program,patient)");
			//The evaluated items, so the check holds for the classic project file and the SDK-style one of the .NET 10 move alike.
			ProjectFile.Load(TestPaths.GetRepoFile("WpfControlsOD","WpfControlsOD.csproj")).AssertCompiles(@"Bridges\AlliedstarConnect.cs",@"Bridges\OrthoComposite.cs");
		}

		[TestMethod]
		public void ProgramLinks_RegistersBothSetups() {
			string setups=TestPaths.ReadRepoFile("OpenPmsDental","Logic","VendorDirect","P262ProgramLinkSetups.cs");
			StringAssert.Contains(setups,"class AlliedstarConnectProgramLinkSetup:StandardProgramLinkSetup");
			StringAssert.Contains(setups,"return AlliedstarConnectLink.EnsureProgram();");
			StringAssert.Contains(setups,"class OrthoCompositeProgramLinkSetup:StandardProgramLinkSetup");
			StringAssert.Contains(setups,"return OrthoCompositeLink.EnsureProgram();");
			ProjectFile.Load(TestPaths.GetRepoFile("OpenPmsDental","OpenPmsDental.csproj")).AssertCompiles(@"Logic\VendorDirect\P262ProgramLinkSetups.cs");
		}

		[TestMethod]
		public void ExitScrub_RemovesTheInfoFile() {
			string programs=TestPaths.ReadRepoFile("OpenPmsDentalBusiness","Data Interface","Programs.cs");
			string scrub=TestPaths.GetMethodBody(programs,"public static void ScrubExportedPatientData()");
			StringAssert.Contains(scrub,"Programs.GetCur(ProgramName.AlliedstarConnect)");
			StringAssert.Contains(scrub,"ScrubFile(VendorDirect.ProgramLinks.AlliedstarConnectLink.GetInfoFilePath(programAlliedstar),true)");
		}

		[TestMethod]
		public void NewFiles_NameNoWebAddressAndNoOtherProgram() {
			Regex regexUrl=new Regex(@"https?://|www\.",RegexOptions.IgnoreCase);
			foreach(string[] arrayPath in new[] {
				new[] { "OpenPmsDentalBusiness","VendorDirect","ProgramLinks","AlliedstarConnectLink.cs" },
				new[] { "OpenPmsDentalBusiness","VendorDirect","ProgramLinks","OrthoCompositeLink.cs" },
				new[] { "WpfControlsOD","Bridges","AlliedstarConnect.cs" },
				new[] { "WpfControlsOD","Bridges","OrthoComposite.cs" },
				new[] { "OpenPmsDental","Logic","VendorDirect","P262ProgramLinkSetups.cs" },
				new[] { "OpenPmsDentalBusiness","Misc","ForkMigrations","TrojanExpressCollectRemovedStep.cs" } })
			{
				string text=TestPaths.ReadRepoFile(arrayPath);
				Assert.IsFalse(regexUrl.IsMatch(text),string.Join("\\",arrayPath)+" names a web address.");
				Assert.IsTrue(text.All(x => x<128),string.Join("\\",arrayPath)+" should be plain ASCII.");
			}
		}

		[TestMethod]
		public void Suni_SupportIsRemoved_ColumnsStay() {
			//Suni's direct capture went in v0.0 (BIN-A); Open Dental 26.2 removed Suni support altogether.
			foreach(string field in new[] { "SensorType","SensorBinned","SensorPort","SensorExposure" }) {
				Assert.IsNotNull(typeof(ComputerPref).GetField(field),field+" stays: Open Dental's 26.2 schema keeps the column.");
			}
			string computerPref=TestPaths.ReadRepoFile("OpenPmsDentalBusiness","TableTypes","ComputerPref.cs");
			StringAssert.Contains(computerPref,"Not used: Suni support is removed (Open Dental 26.2; P262-LINKS).");
			string pref=TestPaths.ReadRepoFile("OpenPmsDentalBusiness","TableTypes","Pref.cs");
			Assert.IsFalse(pref.Contains("Required for Suni capture"),"Nothing requires the old Images module for Suni any more.");
			Assert.IsFalse(pref.Contains("by enabling/disabling Suni"));
			//No code looks for a Suni program link or reads the Suni settings.
			//The removed driver's control type is spelled in two parts, so this file is not a hit for BINATests' own source scan.
			Regex regexUse=new Regex(@"ProgDesc\s*==\s*""Suni""|\.SensorType\b|\.SensorBinned\b|\.SensorPort\b|\.SensorExposure\b|"+"Suni"+"DeviceControl");
			List<string> listOffenders=new List<string>();
			foreach(string project in new[] { "OpenPmsDental","WpfControlsOD","OpenPmsDentalBusiness" }) {
				foreach(string file in Directory.EnumerateFiles(TestPaths.GetRepoFile(project),"*.cs",SearchOption.AllDirectories)) {
					if(file.Contains("\\bin\\") || file.Contains("\\obj\\") || file.EndsWith("ComputerPrefCrud.cs") || file.EndsWith("ComputerPrefs.cs")) {
						continue;//The generated CRUD reads and writes every column; ComputerPrefs writes the defaults of a new row.
					}
					if(regexUse.IsMatch(File.ReadAllText(file))) {
						listOffenders.Add(file);
					}
				}
			}
			Assert.AreEqual(0,listOffenders.Count,"Suni code left in: "+string.Join(", ",listOffenders));
		}

		[TestMethod]
		public void Ez3Di_LinkStays() {
			//Owner decision: Open Dental 26.2 removed the Ez3D-i bridge, this program keeps it.
			Assert.IsTrue(Enum.IsDefined(typeof(ProgramName),"Ez3Di"));
			Assert.IsFalse(OpenPmsDentalBusiness.ProgramLinksDiscontinued.IsDiscontinued("Ez3Di"));
			StringAssert.Contains(TestPaths.ReadRepoFile("OpenPmsDentalBusiness","Data Interface","Programs.cs"),"Programs.GetCur(ProgramName.Ez3Di)");
		}
	}
}
