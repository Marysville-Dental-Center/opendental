// Package RDL-SRC (docs/port-notes/RDL-SRC.md): ODR.dll, RdlEngine.dll and RdlViewer.dll are built from the source in this repository, so the
// shipped binaries match the shipped source (GPL for ODR, LGPL for the fyiReporting engine and viewer). Pins that the prebuilt copies stay
// deleted and unreferenced, that the three projects are in the solution with the Debug|x86 mapping, that none of them uses MySql.Data, and that
// ODR keeps the assembly and class names report definitions name. Reads the source and the built assemblies only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectFileTests;

namespace ReportTests {
	[TestClass]
	public class RdlSourceBuildTests {
		///<summary>The prebuilt binaries this package deleted from "Required dlls".</summary>
		private static readonly string[] _arrayDeletedDlls=new[] { "ODR.dll","RdlEngine.dll","RdlViewer.dll" };
		///<summary>The three projects: name in OpenDental.sln, path, project GUID.</summary>
		private static readonly string[][] _arrayProjects=new[] {
			new[] { "xODR",@"ODR\xODR.csproj","{84688345-BB6A-4F2F-8672-A82B52FD433B}" },
			new[] { "RdlEngine",@"RdlEngine\RdlEngine.csproj","{C97E91F4-B310-44E2-9B6C-96775395722D}" },
			new[] { "RdlViewer",@"RdlViewer\RdlViewer.csproj","{D99145B1-CB6C-41F5-BA86-723325145FB5}" },
		};

		#region Helpers
		///<summary>The repository root: the first folder above the test assembly with LICENSE and NOTICE (REN-PROOF's rule: never found by the
		///solution's name).</summary>
		private static string Root() {
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null) {
				if(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))) {
					return directoryInfo.FullName;
				}
				directoryInfo=directoryInfo.Parent;
			}
			Assert.Fail("Repository root not found above "+AppDomain.CurrentDomain.BaseDirectory);
			return null;
		}

		private static string Read(string relativePath) {
			string path=Path.Combine(Root(),relativePath);
			Assert.IsTrue(File.Exists(path),"Missing: "+relativePath);
			return File.ReadAllText(path);
		}

		///<summary>Every file of these kinds in the repository, relative to the root, skipping build output, packages and other checkouts.</summary>
		private static List<string> RepoFiles(params string[] arrayPatterns) {
			string root=Root();
			string[] arraySkippedFolders=new[] { "bin","obj","packages",".git",".claude",".vs","TestResults","node_modules" };
			List<string> list=new List<string>();
			Stack<string> stackFolders=new Stack<string>();
			stackFolders.Push(root);
			while(stackFolders.Count>0) {
				string folder=stackFolders.Pop();
				foreach(string pattern in arrayPatterns) {
					list.AddRange(Directory.GetFiles(folder,pattern).Select(x => x.Substring(root.Length).TrimStart('\\')));
				}
				foreach(string subfolder in Directory.GetDirectories(folder)) {
					if(!arraySkippedFolders.Contains(Path.GetFileName(subfolder),StringComparer.OrdinalIgnoreCase)) {
						stackFolders.Push(subfolder);
					}
				}
			}
			return list;
		}

		private static List<string> ReferencedAssemblyNames(Assembly assembly) {
			return assembly.GetReferencedAssemblies().Select(x => x.Name).ToList();
		}

		///<summary>The .cs files a project compiles (its evaluated Compile items, listed in a classic project file or globbed in an SDK-style one;
		///RdlEngine\Definition also holds a few old files it does not compile), relative to the root.</summary>
		private static List<string> CompiledFiles(string projectPath) {
			string folder=Path.GetDirectoryName(projectPath);
			List<string> list=ProjectFile.Load(Path.Combine(Root(),projectPath)).GetItems("Compile")
				.Select(x => x.Include)
				.Where(x => x.EndsWith(".cs",StringComparison.OrdinalIgnoreCase) && !x.StartsWith(@"..\"))//not the linked SharedAssemblyInfo.cs
				.Select(x => Path.Combine(folder,x))
				.ToList();
			Assert.IsTrue(list.Count>0,projectPath+" compiles no files?");
			return list;
		}
		#endregion Helpers

		[TestMethod]
		public void RequiredDlls_NoLongerHoldsThePrebuiltReportBinaries() {
			foreach(string fileName in _arrayDeletedDlls) {
				Assert.IsFalse(File.Exists(Path.Combine(Root(),"Required dlls",fileName)),@"Required dlls\"+fileName+" is back; it is built from source.");
			}
		}

		[TestMethod]
		public void Projects_ReferenceNoPrebuiltOdrRdlEngineOrRdlViewer() {
			//A HintPath to a prebuilt copy anywhere (an element in a classic project file, an element or an attribute in an SDK-style one), or a file
			//Reference (not a ProjectReference) by assembly name.
			Regex regex=new Regex(@"<HintPath>[^<]*\b(ODR|RdlEngine|RdlViewer)\.dll\s*</HintPath>|\bHintPath\s*=\s*""[^""]*\b(ODR|RdlEngine|RdlViewer)\.dll\s*"""
				+@"|<Reference\s+Include=""(ODR|RdlEngine|RdlViewer)\b"
				+@"|<(None|Content|EmbeddedResource)\s+(Include|Update)=""[^""]*\b(ODR|RdlEngine|RdlViewer)\.dll""",RegexOptions.IgnoreCase);
			List<string> listOffenders=new List<string>();
			foreach(string relative in RepoFiles("*.csproj","*.vcxproj","*.vbproj","*.props","*.targets")) {
				Match match=regex.Match(Read(relative));
				if(match.Success) {
					listOffenders.Add(relative+" ("+match.Value+")");
				}
			}
			Assert.AreEqual(0,listOffenders.Count,"Still referenced as prebuilt binaries: "+string.Join(", ",listOffenders));
		}

		[TestMethod]
		public void OpenDental_ReferencesTheThreeProjects() {
			//The evaluated items, so the check holds for the classic project file and the SDK-style one of the .NET 10 move alike.
			ProjectFile projectFile=ProjectFile.Load(Path.Combine(Root(),@"OpenPmsDental\OpenPmsDental.csproj"));
			foreach(string[] project in _arrayProjects) {
				ProjectItem projectItem=projectFile.GetProjectReference(@"..\"+project[1]);
				Assert.IsNotNull(projectItem,"OpenPmsDental.csproj does not reference "+project[1]);
				//The project GUID, where the reference names one (a classic project file always does; an SDK-style one may leave it to the path),
				//is the one the solution builds; the referenced project carries the same.
				if(projectItem.HasMetadata("Project")) {
					Assert.AreEqual(project[2],projectItem.GetMetadata("Project"),true,project[1]);
				}
				else {
					Assert.IsTrue(projectFile.IsSdkStyle,"A classic project file names the GUID of each project it references: "+project[1]);
				}
				string projectGuid=ProjectFile.Load(Path.Combine(Root(),project[1])).GetProperty("ProjectGuid");
				Assert.IsTrue(projectGuid=="" || projectGuid.Equals(project[2],StringComparison.OrdinalIgnoreCase),project[1]+" has the project GUID "+projectGuid);
				Assert.AreNotEqual("false",projectItem.GetMetadata("ReferenceOutputAssembly").ToLowerInvariant(),project[1]+" must be copied next to OpenPmsDental.exe.");
				Assert.AreNotEqual("false",projectItem.GetMetadata("Private").ToLowerInvariant(),project[1]+" must be copied next to OpenPmsDental.exe.");
			}
		}

		[TestMethod]
		public void Solution_BuildsTheThreeProjects_InDebugX86() {
			string sln=Read("OpenDental.sln");
			string slnf=Read("OpenDental.Local.slnf");
			foreach(string[] project in _arrayProjects) {
				StringAssert.Contains(sln,"= \""+project[0]+"\", \""+project[1]+"\", \""+project[2]+"\"","OpenDental.sln");
				StringAssert.Contains(sln,project[2]+".Debug|x86.ActiveCfg = Debug|x86","OpenDental.sln");
				StringAssert.Contains(sln,project[2]+".Debug|x86.Build.0 = Debug|x86","OpenDental.sln");
				StringAssert.Contains(sln,project[2]+".Release|x86.Build.0 = Release|x86","OpenDental.sln");
				StringAssert.Contains(slnf,"\""+project[1].Replace(@"\",@"\\")+"\"","OpenDental.Local.slnf");
			}
		}

		[TestMethod]
		public void Sources_UseMySqlConnector_NotMySqlData() {
			foreach(string[] project in _arrayProjects) {
				string folder=Path.GetDirectoryName(project[1]);
				string csproj=Read(project[1]);
				//A reference or a HintPath in the element form of a classic project file or the attribute form an SDK-style one may use.
				StringAssert.DoesNotMatch(csproj,new Regex(@"<(Package)?Reference\s+Include=""MySql\.Data|<HintPath>[^<]*MySql\.Data|\bHintPath\s*=\s*""[^""]*MySql\.Data"
					+@"|Include=""[^""]*packages\.config",RegexOptions.IgnoreCase),project[1]);
				Assert.IsNull(ProjectFile.Load(Path.Combine(Root(),project[1])).GetReference("MySql.Data"),project[1]);
				Assert.IsFalse(File.Exists(Path.Combine(Root(),folder,"packages.config")),folder+@"\packages.config");
				foreach(string relative in CompiledFiles(project[1])) {
					StringAssert.DoesNotMatch(Read(relative),new Regex(@"^\s*using\s+MySql\.Data",RegexOptions.Multiline),relative);
				}
			}
			StringAssert.Contains(Read(@"ODR\DataConnection.cs"),"using MySqlConnector;");
			//The engine's built-in data provider list, used when there is no RdlEngineConfig.xml next to the program (there is none).
			string config=Read(@"RdlEngine\Runtime\RdlEngineConfig.cs");
			Match match=Regex.Match(config,@"<DataProvider>MySQL\.NET</DataProvider>\s*<CodeModule>([^<]*)</CodeModule>\s*<ClassName>([^<]*)</ClassName>");
			Assert.IsTrue(match.Success,"The MySQL.NET provider is missing from the built-in configuration.");
			Assert.AreEqual("MySqlConnector.dll",match.Groups[1].Value);
			Assert.AreEqual("MySqlConnector.MySqlConnection",match.Groups[2].Value);
			Assert.IsNotNull(typeof(MySqlConnector.MySqlConnection).GetConstructor(new[] { typeof(string) }),"RdlEngine creates it with the connection string.");
			Assert.IsFalse(Regex.IsMatch(Read(@"OpenPmsDental\OpenPmsDental.csproj"),@"RdlEngineConfig\.xml"),"No RdlEngineConfig.xml is shipped.");
			Assert.AreEqual(0,ProjectFile.Load(Path.Combine(Root(),@"OpenPmsDental\OpenPmsDental.csproj")).GetItemsMentioning("RdlEngineConfig.xml").Count,
				"No RdlEngineConfig.xml is shipped (nor globbed into an SDK-style project).");
		}

		[TestMethod]
		public void Odr_KeepsTheNamesReportDefinitionsUse() {
			Assembly assemblyOdr=typeof(ODR.Query).Assembly;
			Assert.AreEqual("ODR",assemblyOdr.GetName().Name);
			Assert.AreEqual("ODR.dll",Path.GetFileName(assemblyOdr.Location),true);
			Assert.IsNotNull(typeof(ODR.Query).GetConstructor(Type.EmptyTypes),"RdlEngine creates <Class> instances with no arguments.");
			Assert.IsNotNull(typeof(ODR.Query).GetMethod("Get",new[] { typeof(string),typeof(string) }));
			Assert.IsNotNull(typeof(ODR.Query).GetMethod("Get",new[] { typeof(string) }));
			//Every ODR.Class.Method( that the program's report definitions call is a public static method of that class.
			List<string> listCalls=new List<string>();
			foreach(string fileName in new[] { "Unsched Treat Plans.rdl","ReportAccountingBalanceSheet.txt","ReportAccountingGenLedger.txt" }) {
				string rdl=Read(Path.Combine("OpenPmsDental","Resources",fileName));
				StringAssert.Contains(rdl,"<CodeModule>ODR.dll</CodeModule>",fileName);
				foreach(Match match in Regex.Matches(rdl,@"\bODR\.(\w+)\.(\w+)\(")) {
					listCalls.Add(match.Groups[1].Value+"."+match.Groups[2].Value);
				}
			}
			CollectionAssert.IsSubsetOf(new[] { "DataConnection.GetODConnStr","GetData.Pref","GetData.NetIncomeThisYear","GetData.RetainedEarningsAuto",
				"Format.NumberHideZero","Aggregate.RunningSumForAccounts" },listCalls.Distinct().ToList());
			foreach(string call in listCalls.Distinct()) {
				string[] parts=call.Split('.');
				Type type=assemblyOdr.GetType("ODR."+parts[0]);
				Assert.IsNotNull(type,"ODR."+parts[0]);
				Assert.IsTrue(type.GetMethods(BindingFlags.Public|BindingFlags.Static).Any(x => x.Name==parts[1]),"ODR."+call);
			}
		}

		[TestMethod]
		public void BuiltOdr_UsesMySqlConnectorAndTheProgramsAssemblies() {
			List<string> listReferences=ReferencedAssemblyNames(typeof(ODR.Query).Assembly);
			CollectionAssert.Contains(listReferences,"MySqlConnector");
			CollectionAssert.DoesNotContain(listReferences,"MySql.Data");
			foreach(string name in new[] { "OpenPmsDentalBusiness","CodeBase","DataConnectionBase" }) {
				CollectionAssert.Contains(listReferences,name,"ODR binds to the program's assemblies by name; it is rebuilt with them.");
			}
			CollectionAssert.DoesNotContain(ReferencedAssemblyNames(typeof(fyiReporting.RDL.Report).Assembly),"MySql.Data");
		}

		[TestMethod]
		public void Docs_SayTheThreeAreBuiltFromSource() {
			//M16 integration follow-up: the README, the RDL upgrade comments and the binaries audit no longer describe the prebuilt binaries.
			string[] arrayReadmeLines=Read("README.md").Replace("\r\n","\n").Split('\n');
			string readmeNotBuilt=arrayReadmeLines.Single(x => x.StartsWith("| ") && x.Contains("not built"));
			foreach(string folder in new[] { "`ODR/`","`RdlEngine/`","`RdlViewer/`" }) {
				Assert.IsFalse(readmeNotBuilt.Contains(folder),"README lists "+folder+" as not built.");
				Assert.IsTrue(arrayReadmeLines.Any(x => x.StartsWith("| ") && x.Contains(folder) && x.Contains("built from source")),
					"README says "+folder+" is built from source.");
			}
			foreach(string form in new[] { @"OpenPmsDental\ReportingForRdl\FormReportForRdl.cs",@"OpenPmsDental\ReportingForRdl\FormReportCustom.cs" }) {
				string source=Read(form);
				StringAssert.Contains(source,"<CodeModule>MySqlConnector.dll</CodeModule>",form+": the upgrade comment names the provider the engine uses.");
				StringAssert.Contains(source,"<ClassName>MySqlConnector.MySqlConnection</ClassName>",form);
				Assert.IsFalse(source.Contains("MySql.Data.dll") || source.Contains("MySQL.Data.dll"),form+": the upgrade comment still names MySql.Data.");
			}
			string[] arrayAuditLines=Read(@"docs\third-party-binaries-audit.md").Replace("\r\n","\n").Split('\n');
			foreach(string fileName in _arrayDeletedDlls) {
				string row=arrayAuditLines.Single(x => x.StartsWith("| `"+fileName+"` |") && x.Contains("RDL-SRC"));
				StringAssert.Contains(row,"built from source",fileName+" audit row");
				//The audit is a record and keeps the project names of its time (before the rename).
				StringAssert.Contains(row,"ProjectReference: OpenDental",fileName+" audit row");
				Assert.IsFalse(row.Contains("HintPath: OpenDental"),fileName+" audit row still names the old HintPath.");
				Assert.IsFalse(row.Contains("so the build never copies it"),fileName+" audit row still says it is not copied.");
			}
		}

		[TestMethod]
		public void ProgramOutput_HoldsTheBinariesBuiltFromSource() {
			string folder=Path.Combine(Root(),"OpenPmsDental","bin","Debug");
			if(!File.Exists(Path.Combine(folder,"OpenPmsDental.exe"))) {
				BuildOutput.NotBuilt(@"OpenPmsDental\bin\Debug is not built. Build OpenDental.Local.slnf (Debug|x86) first.");
			}
			foreach(string[] pair in new[] { new[] { "ODR.dll",@"ODR\bin\Debug" },new[] { "RdlEngine.dll",@"RdlEngine\bin\Debug" },
				new[] { "RdlViewer.dll",@"RdlViewer\bin\Debug" } })
			{
				string pathOutput=Path.Combine(folder,pair[0]);
				Assert.IsTrue(File.Exists(pathOutput),"Missing from the program's output: "+pair[0]);
				string pathBuilt=Path.Combine(Root(),pair[1],pair[0]);
				Assert.IsTrue(File.Exists(pathBuilt),"Not built: "+pathBuilt);
				CollectionAssert.AreEqual(File.ReadAllBytes(pathBuilt),File.ReadAllBytes(pathOutput),pair[0]+" in the output is the one built from source.");
			}
			Assert.IsFalse(File.Exists(Path.Combine(folder,"MySql.Data.dll")),"MySql.Data.dll is not shipped.");
		}
	}
}
