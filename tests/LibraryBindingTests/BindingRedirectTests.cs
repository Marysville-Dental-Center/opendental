// The binding redirects that let the libraries in "Required dlls" use the newer support assemblies next to them: every reference between them
// resolves in the config of every program, library and test that uses them, no redirect names a version that is not there, and the built
// programs and tests hold the very files of "Required dlls" (not older copies from elsewhere) with configs that bind them.
// When a later package adds or replaces a DLL, these tests name each config line it needs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectFileTests;

namespace LibraryBindingTests {
	[TestClass]
	public class BindingRedirectTests {
		///<summary>The config of every test project that has no app.config of its own (Directory.Build.targets).</summary>
		private static readonly string _pathSharedTestConfig=RepoPaths.Combine("tests","BindingRedirects.config");

		///<summary>The configs that must redirect every reference between the assemblies of "Required dlls": the app.config or Web.config of each
		///project of OpenDental.Local.slnf that uses one of them (the programs, the libraries that ship a .dll.config, the dev tools), and the shared
		///test config.</summary>
		private static List<string> GetConfigsOfRequiredDllsUsers() {
			List<string> listConfigs=SolutionProjects.All.Where(x => x.ConfigPath!=null && SolutionProjects.LoadsRequiredAssemblies(x))
				.Select(x => x.ConfigPath).ToList();
			listConfigs.Add(_pathSharedTestConfig);
			return listConfigs;
		}

		[TestMethod]
		public void RequiredDlls_EveryReferenceBetweenThemResolves_InEveryConfigThatUsesThem() {
			List<string> listConfigs=GetConfigsOfRequiredDllsUsers();
			//The shipped programs are among them, so a project list that went wrong cannot pass by checking nothing.
			foreach(string program in new[] { @"OpenPmsDental\app.config",@"ServiceManager\app.config",@"OpenPmsDentalHL7\app.config",
				@"CentralManager\app.config",@"DatabaseIntegrityCheck\app.config",@"MySQLManager\App.config",@"OpenPmsDentalServer\Web.config" })
			{
				Assert.IsTrue(listConfigs.Any(x => RepoPaths.Relative(x).Equals(program,StringComparison.OrdinalIgnoreCase)),program+" is not checked.");
			}
			List<string> listProblems=new List<string>();
			foreach(string pathConfig in listConfigs) {
				listProblems.AddRange(BindingRules.FindUnresolvedReferences(RequiredDlls.Assemblies,ConfigFiles.ReadRedirects(pathConfig),
					RepoPaths.Relative(pathConfig)));
			}
			Assert.AreEqual(0,listProblems.Count,"References that do not bind:\r\n"+string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void Configs_RedirectOnlyToVersionsThatAreThere() {
			List<string> listConfigs=SolutionProjects.All.Where(x => x.ConfigPath!=null).Select(x => x.ConfigPath).ToList();
			listConfigs.Add(_pathSharedTestConfig);
			List<string> listProblems=new List<string>();
			foreach(string pathConfig in listConfigs) {
				listProblems.AddRange(BindingRules.FindStaleRedirects(RequiredDlls.Assemblies,ConfigFiles.ReadRedirects(pathConfig),RepoPaths.Relative(pathConfig)));
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void Programs_ThatUseRequiredDlls_HaveAConfig() {
			List<string> listProblems=new List<string>();
			foreach(ProjectInfo projectInfo in SolutionProjects.All.Where(x => (x.IsExe || x.IsWeb) && SolutionProjects.LoadsRequiredAssemblies(x))) {
				if(projectInfo.ConfigPath==null) {
					listProblems.Add(RepoPaths.Relative(projectInfo.Path)+" uses Required dlls but has no app.config with the binding redirects.");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			//UpdateFileCopier uses only the .NET Framework, so its config needs no redirects.
			ProjectInfo projectInfoCopier=SolutionProjects.ByName("UpdateFileCopier.csproj");
			Assert.IsTrue(projectInfoCopier.IsExe);
			Assert.AreEqual(0,SolutionProjects.RequiredDllsInClosure(projectInfoCopier).Count);
			Assert.IsTrue(SolutionProjects.LoadsRequiredAssemblies(SolutionProjects.ByName("ServiceManager.csproj")));
		}

		[TestMethod]
		public void TestProjects_GenerateTheirRedirects_FromTheSharedTestConfig() {
			string targets=RepoPaths.ReadText("Directory.Build.targets");
			StringAssert.Contains(targets,@"<AppConfig>$(MSBuildThisFileDirectory)tests\BindingRedirects.config</AppConfig>");
			StringAssert.Contains(targets,"$(MSBuildProjectName.EndsWith('Tests'))");
			List<string> listProblems=new List<string>();
			foreach(ProjectInfo projectInfo in SolutionProjects.All.Where(x => x.IsTest && !x.IsExe)) {
				if(!projectInfo.AutoGeneratesRedirects) {
					listProblems.Add(RepoPaths.Relative(projectInfo.Path)+" should set AutoGenerateBindingRedirects and GenerateBindingRedirectsOutputType to "
						+"true, so its .dll.config redirects to the files of Required dlls.");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void BuiltProgramsAndTests_HoldTheFilesOfRequiredDlls_AndBindThem() {
			List<string> listProblems=new List<string>();
			int countFolders=0;
			foreach(ProjectInfo projectInfo in SolutionProjects.All.Where(x => (x.IsExe || x.IsWeb || x.IsTest) && SolutionProjects.LoadsRequiredAssemblies(x))) {
				string folder=projectInfo.OutputFolder;
				if(folder==null) {
					continue;//not built
				}
				countFolders++;
				string labelFolder=RepoPaths.Relative(folder);
				//The very files of "Required dlls": a same-named file from elsewhere (an older copy in Visual Studio's test platform folder, a stale
				//build) is not what the release ships.
				foreach(string path in Directory.GetFiles(folder)) {
					if(RequiredDlls.Files.TryGetValue(Path.GetFileName(path),out string pathRequired)
						&& MetadataReader.Sha256(path)!=MetadataReader.Sha256(pathRequired))
					{
						listProblems.Add(labelFolder+"\\"+Path.GetFileName(path)+" is not the file in Required dlls.");
					}
				}
				List<string> listConfigs=new List<string>();
				if(projectInfo.IsWeb) {
					listConfigs.Add(projectInfo.ConfigPath);
				}
				else {
					string pathOwnConfig=Path.Combine(folder,projectInfo.PrimaryOutputName+".config");
					if(!File.Exists(pathOwnConfig)) {
						listProblems.Add(RepoPaths.Relative(pathOwnConfig)+" is missing; it should hold the binding redirects.");
					}
					listConfigs.Add(pathOwnConfig);
					if(Path.GetFileName(projectInfo.Path).Equals("OpenPmsDental.csproj",StringComparison.OrdinalIgnoreCase)) {
						//The program folder the release ships also holds ServiceManager.exe, which runs from there with its own config.
						listConfigs.AddRange(Directory.GetFiles(folder,"*.exe.config").Where(x => File.Exists(x.Substring(0,x.Length-".config".Length))));
					}
				}
				Dictionary<string,AssemblyFacts> dictAssemblies=MetadataReader.ReadFolder(folder);
				foreach(string pathConfig in listConfigs.Distinct(StringComparer.OrdinalIgnoreCase).Where(x => x!=null && File.Exists(x))) {
					List<BindingRedirect> listRedirects=ConfigFiles.ReadRedirects(pathConfig);
					string label=RepoPaths.Relative(pathConfig);
					listProblems.AddRange(BindingRules.FindUnresolvedReferences(dictAssemblies,listRedirects,label+" (built)"));
					listProblems.AddRange(BindingRules.FindStaleRedirects(dictAssemblies,listRedirects,label+" (built)"));
				}
			}
			if(countFolders==0) {
				BuildOutput.NotBuilt("Nothing is built. Build OpenDental.Local.slnf (Debug|x86) first.");
			}
			Assert.AreEqual(0,listProblems.Count,listProblems.Count+" problem(s); a rebuild of the project may be needed:\r\n"
				+string.Join("\r\n",listProblems.Distinct()));
		}
	}
}
