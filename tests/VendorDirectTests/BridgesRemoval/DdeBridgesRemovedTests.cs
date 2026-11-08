// Owner question REL-FILES 2, answered 2026-11-07: NDde.dll (its own shared source licence, which does not fit the GPL) goes, and with it the
// three program links that used it to send the patient over DDE, which the office does not use: DentX (ProImage), Dexis Integrator and Vipersoft.
// The bridge code, the reference, the file in Required dlls, its licence text and notices entry are gone; the release refuses NDde.dll and setup
// deletes it on an upgrade. The ProgramName values stay (program rows name them): such a row is listed as "(removed)", cannot be enabled or
// launched and shows no toolbar button. Reads the source and the built assemblies only; no database, nothing leaves this computer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using VendorDirectTests.Foundation;

namespace VendorDirectTests.BridgesRemoval {
	[TestClass]
	public class DdeBridgesRemovedTests {
		private static readonly string[] _arrayBridgeFiles={ "DentX.cs","DexisIntegrator.cs","Vipersoft.cs" };
		private static readonly string[] _arrayProgramNames={ "DentX","DexisIntegrator","Vipersoft" };

		private static string GetRoot() {
			return FoundationLayoutTests.GetRepositoryRoot();
		}

		private static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(Path.Combine(new[] { GetRoot() }.Concat(arrayParts).ToArray()));
		}

		private static IEnumerable<string> GetProjectFiles() {
			string root=GetRoot();
			foreach(string path in Directory.EnumerateFiles(root,"*.csproj",SearchOption.AllDirectories)) {
				string relative=path.Substring(root.Length);
				if(relative.Contains(@"\bin\") || relative.Contains(@"\obj\") || relative.Contains(@"\.claude\") || relative.Contains(@"\.git\")
					|| relative.StartsWith(@"\third-party\"))
				{
					continue;
				}
				yield return path;
			}
		}

		[TestMethod]
		public void NDde_IsGone_FromRequiredDllsTheProjectsAndTheLicenseList() {
			Assert.IsFalse(File.Exists(Path.Combine(GetRoot(),"Required dlls","NDde.dll")));
			List<string> listOffenders=GetProjectFiles().Where(x => Regex.IsMatch(File.ReadAllText(x),@"\bNDde\b")).ToList();
			Assert.AreEqual(0,listOffenders.Count,"Still named in: "+string.Join(", ",listOffenders));
			Assert.IsFalse(File.Exists(Path.Combine(GetRoot(),"OpenPmsDental","Resources","NDde.txt")),"Its licence text, for the license list.");
			Assert.IsFalse(ReadRepoFile("OpenPmsDental","Properties","Resources.resx").Contains("NDde"));
			Assert.IsFalse(ReadRepoFile("OpenPmsDental","Forms","FormLicense.cs").Contains("NDde"));
			Assert.IsFalse(ReadRepoFile("THIRD-PARTY-NOTICES.txt").Contains("NDde"),"No notices entry for a file that does not ship.");
		}

		[TestMethod]
		public void BridgeCode_IsGone_AndNothingDispatchesToIt() {
			foreach(string file in _arrayBridgeFiles) {
				Assert.IsFalse(File.Exists(Path.Combine(GetRoot(),"WpfControlsOD","Bridges",file)),file);
				Assert.IsFalse(ReadRepoFile("WpfControlsOD","WpfControlsOD.csproj").Contains(@"Bridges\"+file),file);
			}
			string programL=ReadRepoFile("WpfControlsOD","Logic","ProgramL.cs");
			foreach(string programName in _arrayProgramNames) {
				Assert.IsFalse(programL.Contains("ProgramName."+programName+".ToString()"),"No dispatch branch for "+programName);
				Assert.IsFalse(programL.Contains(programName+".SendData("),programName);
			}
			foreach(string project in new[] { "OpenPmsDental","OpenPmsDentalBusiness","WpfControlsOD","CodeBase" }) {
				foreach(string path in Directory.EnumerateFiles(Path.Combine(GetRoot(),project),"*.cs",SearchOption.AllDirectories)) {
					if(path.Contains(@"\obj\") || path.Contains(@"\bin\")) {
						continue;
					}
					Assert.IsFalse(Regex.IsMatch(File.ReadAllText(path),@"^\s*using\s+NDde\b",RegexOptions.Multiline),path);
				}
			}
		}

		[TestMethod]
		public void BuiltAssemblies_DoNotReferenceNDde() {
			foreach(string pathRelative in new[] { @"OpenPmsDental\bin\Debug\OpenPmsDental.exe",@"WpfControlsOD\bin\Debug\WpfControlsOD.dll" }) {
				string path=Path.Combine(GetRoot(),pathRelative);
				Assert.IsTrue(File.Exists(path),"Build first: "+path);
				string name=AssemblyName.GetAssemblyName(path).FullName;
				Assembly assembly=AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies().FirstOrDefault(x => x.FullName==name)
					??Assembly.ReflectionOnlyLoad(File.ReadAllBytes(path));
				Assert.IsFalse(assembly.GetReferencedAssemblies().Any(x => x.Name.Equals("NDde",StringComparison.OrdinalIgnoreCase)),pathRelative);
			}
		}

		[TestMethod]
		public void ProgramRows_StayNamed_ButAreRemovedLinks() {
			foreach(string programName in _arrayProgramNames) {
				Assert.IsTrue(Enum.IsDefined(typeof(ProgramName),programName),"Program rows name it: "+programName);
				Program program=new Program { ProgName=programName,ProgDesc="Some link",Enabled=true };
				Assert.IsTrue(ProgramLinksDiscontinued.IsRemoved(program),programName);
				Assert.IsTrue(ProgramLinksDiscontinued.IsDiscontinued(program),"Cannot be enabled, launched or given buttons: "+programName);
				Assert.AreEqual(ProgramLinksDiscontinued.LABEL_REMOVED,ProgramLinksDiscontinued.GetLabel(program));
				Assert.AreEqual("Some link (removed)",ProgramLinksDiscontinued.GetLabeledDesc(program.ProgDesc,ProgramLinksDiscontinued.GetLabel(program)));
				StringAssert.Contains(ProgramLinksDiscontinued.GetLaunchMessage(program),"removed from this version");
			}
			Program programDxc=new Program { ProgName=nameof(ProgramName.DXCPatientCreditScore) };
			Assert.IsFalse(ProgramLinksDiscontinued.IsRemoved(programDxc),"A discontinued service, not a removed link.");
			Assert.AreEqual(ProgramLinksDiscontinued.LABEL,ProgramLinksDiscontinued.GetLabel(programDxc));
			StringAssert.Contains(ProgramLinksDiscontinued.GetLaunchMessage(programDxc),"discontinued");
			Assert.IsFalse(ProgramLinksDiscontinued.IsRemoved(new Program { ProgName=nameof(ProgramName.Dexis) }),"Dexis itself stays.");
			Assert.IsFalse(ProgramLinksDiscontinued.IsRemoved((Program)null));
		}

		[TestMethod]
		public void ToolBars_ShowNoButtonOfARemovedLink() {
			string source=ReadRepoFile("OpenPmsDentalBusiness","Data Interface","ToolButItems.cs");
			StringAssert.Contains(source,"&& !ProgramLinksDiscontinued.IsDiscontinued(Programs.GetFirstOrDefault(y => y.ProgramNum==x.ProgramNum))");
			string programL=ReadRepoFile("WpfControlsOD","Logic","ProgramL.cs");
			StringAssert.Contains(programL,"MsgBox.Show(ProgramLinksDiscontinued.GetLaunchMessage(program));");
		}

		[TestMethod]
		public void Release_RefusesNDde_AndSetupDeletesIt() {
			CollectionAssert.Contains(OpenPmsDentalBusiness.ForkReleases.ListRetiredFiles,"NDde.dll","Never published again by the release channel.");
			string script=ReadRepoFile("tools","Build-Release.ps1");
			StringAssert.Contains(script,"@('NDde.dll',","On the deny list.");
			Assert.IsFalse(script.Contains("'NDde.dll',\r\n") || Regex.IsMatch(script,@"'MySqlConnector\.dll','NDde\.dll'"),"Not on the allow list.");
			StringAssert.Contains(ReadRepoFile("installer","OpenPmsDental.iss"),@"Type: files; Name: ""{app}\NDde.dll""");
		}
	}
}
