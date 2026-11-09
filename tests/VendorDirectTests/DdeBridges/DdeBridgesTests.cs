// S0-R (2026-11-08): the DentX (ProImage), Dexis Integrator and Vipersoft program links, which send the patient over DDE, are kept (the owner:
// "Don't make the assumption that if the office doesn't use, we'd cut it."). Removed on 2026-11-07 with NDde.dll (REL-FILES 2), they are back:
// the bridge code, its Compile lines and ProgramL branches, ordinary (not discontinued) program rows. NDde.dll itself stays out, because its own
// shared source licence puts restrictions on recipients that the GPL does not allow: the links use the in-house DDE client CodeBase.ODDdeClient,
// no project or built assembly references NDde, its licence text and notices entry stay gone, the release refuses NDde.dll and setup deletes it.
// Reads the source and the built assemblies only; no database, no DDE conversation, nothing leaves this computer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using ProjectFileTests;
using VendorDirectTests.Foundation;

namespace VendorDirectTests.DdeBridges {
	[TestClass]
	public class DdeBridgesTests {
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
		public void BridgeCode_IsBack_AndProgramLDispatchesIt_AfterTheDiscontinuedCheck() {
			//The evaluated items, so the check holds for the classic project file and the SDK-style one of the .NET 10 move alike.
			ProjectFile projectFile=ProjectFile.Load(Path.Combine(GetRoot(),"WpfControlsOD","WpfControlsOD.csproj"));
			foreach(string file in _arrayBridgeFiles) {
				Assert.IsTrue(File.Exists(Path.Combine(GetRoot(),"WpfControlsOD","Bridges",file)),file);
				projectFile.AssertCompiles(@"Bridges\"+file);
			}
			string programL=ReadRepoFile("WpfControlsOD","Logic","ProgramL.cs");
			int indexDiscontinued=programL.IndexOf("ProgramLinksDiscontinued.IsDiscontinued(program)",StringComparison.Ordinal);
			Assert.IsTrue(indexDiscontinued>0);
			foreach(string programName in _arrayProgramNames) {
				int indexBranch=programL.IndexOf("program.ProgName==ProgramName."+programName+".ToString()",StringComparison.Ordinal);
				Assert.IsTrue(indexBranch>indexDiscontinued,"A dispatch branch for "+programName);
				int indexCall=programL.IndexOf(programName+".SendData(program,patient);",indexBranch,StringComparison.Ordinal);
				Assert.IsTrue(indexCall>indexBranch && indexCall-indexBranch<120,"The branch calls its bridge: "+programName);
			}
		}

		[TestMethod]
		public void Bridges_SendThroughTheInHouseDdeClient() {
			foreach(string file in _arrayBridgeFiles) {
				string source=ReadRepoFile("WpfControlsOD","Bridges",file);
				StringAssert.Contains(source,"ODDdeClient.Create(",file);
				Assert.IsFalse(Regex.IsMatch(source,@"^\s*using\s+NDde\b",RegexOptions.Multiline),file);
				Assert.IsFalse(Regex.IsMatch(source,@"new\s+Dde(Context|Client)\b|\bDde(Context|Client)\s+\w+\s*="),"No NDde type is used: "+file);
				StringAssert.StartsWith(source,"#if !DISABLE_WINDOWS_BRIDGES","As before: "+file);
			}
			string client=ReadRepoFile("CodeBase","Utilities","ODDdeClient.cs");
			foreach(string function in new[] { "DdeInitializeA","DdeConnect","DdeClientTransaction","DdeDisconnect","DdeUninitialize" }) {
				StringAssert.Contains(client,@"EntryPoint="""+function+@"""",function);
			}
			ProjectFile.Load(Path.Combine(GetRoot(),"CodeBase","xCodeBase.csproj")).AssertCompiles(@"Utilities\ODDdeClient.cs");
		}

		[TestMethod]
		public void ProgramRows_AreOrdinaryLinks_AgainNotRemovedOrDiscontinued() {
			foreach(string programName in _arrayProgramNames) {
				Assert.IsTrue(Enum.IsDefined(typeof(ProgramName),programName),programName);
				Program program=new Program { ProgName=programName,ProgDesc="Some link",Enabled=true };
				Assert.IsFalse(ProgramLinksDiscontinued.IsDiscontinued(program),"Can be enabled, launched and given buttons: "+programName);
			}
			//The "(removed)" category of 2026-11-07 is gone with its last members.
			Type type=typeof(ProgramLinksDiscontinued);
			foreach(string member in new[] { "ListProgramNamesRemoved","LABEL_REMOVED","IsRemoved","GetLabel","GetLaunchMessage" }) {
				Assert.AreEqual(0,type.GetMember(member,BindingFlags.Public|BindingFlags.Static).Length,member);
			}
			Assert.IsTrue(ProgramLinksDiscontinued.IsDiscontinued(nameof(ProgramName.DXCPatientCreditScore)),"The discontinued links stay so.");
			Assert.IsTrue(ProgramLinksDiscontinued.IsDiscontinued(nameof(ProgramName.TrojanExpressCollect)));
			Assert.IsFalse(ProgramLinksDiscontinued.IsDiscontinued(nameof(ProgramName.Dexis)),"Dexis itself was never affected.");
		}

		[TestMethod]
		public void ProgramRows_AreInTheNewDatabase_WithTheirProperties() {
			//0d2ab2722 deleted no row, so no migration step brings them back; a new database has them as Open Dental's conversions made them.
			string sql=ReadRepoFile("db","init","new-database.sql");
			Dictionary<string,string[]> dictProperties=new Dictionary<string,string[]> {
				["'DentX','ProImage'"]=new[] { "'Enter 0 to use PatientNum, or 1 to use ChartNum','0'" },
				["'Vipersoft','Vipersoft aka Clarity'"]=new[] { "'Enter 0 to use PatientNum, or 1 to use ChartNum','0'" },
				["'DexisIntegrator','Dexis Integrator from www.kavo.com'"]=new[] { "'Enter 0 to use PatientNum, or 1 to use ChartNum','0'",
					"'Enter 0 to use DDE, or 1 to use communication file','0'","'Communication files folder path',''" },
			};
			foreach(KeyValuePair<string,string[]> pair in dictProperties) {
				Match match=Regex.Match(sql,@"^\((\d+),"+Regex.Escape(pair.Key)+@",0,",RegexOptions.Multiline);
				Assert.IsTrue(match.Success,"A disabled program row: "+pair.Key);
				string programNum=match.Groups[1].Value;
				foreach(string property in pair.Value) {
					Assert.IsTrue(Regex.IsMatch(sql,@"^\(\d+,"+programNum+","+Regex.Escape(property)+",",RegexOptions.Multiline),pair.Key+": "+property);
				}
			}
		}

		[TestMethod]
		public void ProgramLinks_LabelsOnlyDiscontinuedLinks() {
			string list=ReadRepoFile("OpenPmsDental","Forms","FormProgramLinks.cs");
			StringAssert.Contains(list,"ProgramLinksDiscontinued.GetLabeledDesc(_listPrograms[i].ProgDesc)");
			Assert.IsFalse(list.Contains("(removed)"));
			StringAssert.Contains(ReadRepoFile("OpenPmsDental","Forms","FormProgramLinkEdit.cs"),"Lan.g(this,ProgramLinksDiscontinued.LABEL)");
			StringAssert.Contains(ReadRepoFile("WpfControlsOD","Logic","ProgramL.cs"),
				"MsgBox.Show(\"Program\",\"This program link was discontinued and can no longer be used.\");");
			//Kept from 2026-11-07 for the discontinued links: no toolbar button even when the row is enabled.
			StringAssert.Contains(ReadRepoFile("OpenPmsDentalBusiness","Data Interface","ToolButItems.cs"),
				"&& !ProgramLinksDiscontinued.IsDiscontinued(Programs.GetFirstOrDefault(y => y.ProgramNum==x.ProgramNum))");
		}

		[TestMethod]
		public void NDde_StaysOut_OfRequiredDllsTheProjectsAndTheLicenseList() {
			Assert.IsFalse(File.Exists(Path.Combine(GetRoot(),"Required dlls","NDde.dll")),"Its licence does not fit the GPL (docs/port-notes/S0-R.md).");
			List<string> listOffenders=GetProjectFiles().Where(x => Regex.IsMatch(File.ReadAllText(x),@"\bNDde\b")).ToList();
			Assert.AreEqual(0,listOffenders.Count,"Still named in: "+string.Join(", ",listOffenders));
			Assert.IsFalse(File.Exists(Path.Combine(GetRoot(),"OpenPmsDental","Resources","NDde.txt")),"Its licence text, for the license list.");
			Assert.IsFalse(ReadRepoFile("OpenPmsDental","Properties","Resources.resx").Contains("NDde"));
			Assert.IsFalse(ReadRepoFile("OpenPmsDental","Forms","FormLicense.cs").Contains("NDde"));
			Assert.IsFalse(ReadRepoFile("THIRD-PARTY-NOTICES.txt").Contains("NDde"),"No notices entry for a file that does not ship.");
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
			foreach(string pathRelative in new[] { @"OpenPmsDental\bin\Debug\OpenPmsDental.exe",@"WpfControlsOD\bin\Debug\WpfControlsOD.dll",
				@"CodeBase\bin\Debug\CodeBase.dll" })
			{
				string path=Path.Combine(GetRoot(),pathRelative);
				Assert.IsTrue(File.Exists(path),"Build first: "+path);
				string name=AssemblyName.GetAssemblyName(path).FullName;
				Assembly assembly=AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies().FirstOrDefault(x => x.FullName==name)
					??Assembly.ReflectionOnlyLoad(File.ReadAllBytes(path));
				Assert.IsFalse(assembly.GetReferencedAssemblies().Any(x => x.Name.Equals("NDde",StringComparison.OrdinalIgnoreCase)),pathRelative);
			}
		}

		[TestMethod]
		public void Release_RefusesNDde_AndSetupDeletesIt() {
			CollectionAssert.Contains(OpenPmsDentalBusiness.ForkReleases.ListRetiredFiles,"NDde.dll","Never published again by the release channel.");
			string script=ReadRepoFile("tools","Build-Release.ps1");
			StringAssert.Contains(script,"@('NDde.dll',","On the deny list.");
			Assert.IsFalse(script.Contains("'NDde.dll',\r\n") || Regex.IsMatch(script,@"'MySqlConnector\.dll','NDde\.dll'"),"Not on the allow list.");
			StringAssert.Contains(ReadRepoFile("installer","OpenPmsDental.iss"),@"Type: files; Name: ""{app}\NDde.dll""");
		}

		[TestMethod]
		public void Vipersoft_KeepsTheCallerNameItExpects_AllowedByTheNameCheck() {
			StringAssert.Contains(ReadRepoFile("WpfControlsOD","Bridges","Vipersoft.cs"),"+\"OpenDental|\";//name");
			StringAssert.Contains(ReadRepoFile("tools","Test-NoOldNames.ps1"),
				@"@{ Path='WpfControlsOD\Bridges\Vipersoft.cs'; Match='""OpenDental\|""'; Reason='The program name Vipersoft expects.' }");
		}
	}
}
