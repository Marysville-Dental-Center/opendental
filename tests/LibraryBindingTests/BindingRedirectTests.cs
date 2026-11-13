// The binding redirects that let the libraries use the newer support assemblies next to them: every reference between the library files (those
// of "Required dlls" and, since S2-4 of the .NET 10 move, the NuGet packages' own files at the lock files' versions) resolves in the config of
// every program, library and test that uses them, no redirect names a version that is not there, and the built programs and tests hold those
// very files (not older copies from elsewhere) with configs that bind them.
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

		///<summary>The configs that must redirect every reference between the library assemblies: the app.config or Web.config of each project of
		///OpenDental.Local.slnf that uses one of them (the programs, the libraries that ship a .dll.config, the dev tools), and the shared test
		///config.</summary>
		private static List<string> GetConfigsOfLibraryUsers() {
			List<string> listConfigs=SolutionProjects.All.Where(x => x.ConfigPath!=null && SolutionProjects.LoadsLibraryAssemblies(x))
				.Select(x => x.ConfigPath).ToList();
			listConfigs.Add(_pathSharedTestConfig);
			return listConfigs;
		}

		[TestMethod]
		public void Libraries_EveryReferenceBetweenThemResolves_InEveryConfigThatUsesThem() {
			List<string> listConfigs=GetConfigsOfLibraryUsers();
			//The shipped programs are among them, so a project list that went wrong cannot pass by checking nothing.
			foreach(string program in new[] { @"OpenPmsDental\app.config",@"ServiceManager\app.config",@"OpenPmsDentalHL7\app.config",
				@"CentralManager\app.config",@"DatabaseIntegrityCheck\app.config",@"MySQLManager\App.config",@"OpenPmsDentalServer\Web.config" })
			{
				Assert.IsTrue(listConfigs.Any(x => RepoPaths.Relative(x).Equals(program,StringComparison.OrdinalIgnoreCase)),program+" is not checked.");
			}
			List<string> listProblems=new List<string>();
			foreach(string pathConfig in listConfigs) {
				listProblems.AddRange(BindingRules.FindUnresolvedReferences(Libraries.Assemblies,ConfigFiles.ReadRedirects(pathConfig),
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
				listProblems.AddRange(BindingRules.FindStaleRedirects(Libraries.Assemblies,ConfigFiles.ReadRedirects(pathConfig),RepoPaths.Relative(pathConfig)));
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void Programs_ThatUseTheLibraries_HaveAConfig() {
			List<string> listProblems=new List<string>();
			foreach(ProjectInfo projectInfo in SolutionProjects.All.Where(x => (x.IsExe || x.IsWeb) && SolutionProjects.LoadsLibraryAssemblies(x))) {
				if(projectInfo.ConfigPath==null) {
					listProblems.Add(RepoPaths.Relative(projectInfo.Path)+" uses the libraries but has no app.config with the binding redirects.");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			//UpdateFileCopier uses only the .NET Framework, so its config needs no redirects.
			ProjectInfo projectInfoCopier=SolutionProjects.ByName("UpdateFileCopier.csproj");
			Assert.IsTrue(projectInfoCopier.IsExe);
			Assert.AreEqual(0,SolutionProjects.LibrariesInClosure(projectInfoCopier).Count);
			Assert.IsTrue(SolutionProjects.LoadsLibraryAssemblies(SolutionProjects.ByName("ServiceManager.csproj")));
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
						+"true, so its .dll.config redirects to the library files.");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void BuiltProgramsAndTests_HoldTheLibraryFiles_AndBindThem() {
			List<ProjectInfo> listProjects=SolutionProjects.All.Where(x => (x.IsExe || x.IsWeb || x.IsTest) && SolutionProjects.LoadsLibraryAssemblies(x)).ToList();
			Assert.IsTrue(listProjects.Any(x => Path.GetFileName(x.Path).Equals("OpenPmsDental.csproj",StringComparison.OrdinalIgnoreCase)),"The program is checked.");
			CheckBuiltProgramsAndTests(listProjects,BuildOutput.IsRequired);
		}

		[TestMethod]
		public void BuiltProgramsAndTests_AProjectThatIsNotBuilt_IsNotSkipped() {
			//A project with no output folder: inconclusive on a developer's run, a failure when the build output is required, never a pass.
			ProjectInfo projectInfo=new ProjectInfo();
			projectInfo.Path=Path.Combine(Path.GetTempPath(),"LibraryBindingTests_"+Guid.NewGuid().ToString("N"),"NotBuiltFixture.csproj");
			projectInfo.Folder=Path.GetDirectoryName(projectInfo.Path);
			projectInfo.AssemblyName="NotBuiltFixture";
			projectInfo.IsExe=true;
			foreach(bool isRequired in new[] { true,false }) {
				//The check's own exception is caught; the verdict on it comes after the try, so the test fails on a pass by itself, whatever the
				//message text says.
				UnitTestAssertException exceptionCaught=null;
				try {
					CheckBuiltProgramsAndTests(new List<ProjectInfo> { projectInfo },isRequired);
				}
				catch(UnitTestAssertException ex) {
					exceptionCaught=ex;
				}
				Assert.IsNotNull(exceptionCaught,"A project that is not built passed (required: "+isRequired+").");
				Assert.AreEqual(isRequired ? typeof(AssertFailedException) : typeof(AssertInconclusiveException),exceptionCaught.GetType(),"Required: "+isRequired);
				StringAssert.Contains(exceptionCaught.Message,"NotBuiltFixture.exe");
			}
		}

		///<summary>For each project: the library files in its output folder are the library files themselves (the file of "Required dlls", or the
		///package's own file at the lock files' version), and its configs bind every reference between the assemblies there. A project with no output is not skipped: once the built ones are checked, it ends the check through
		///BuildOutput.NotBuilt (inconclusive on a developer's run, a failure under OPMS_REQUIRE_BUILD_OUTPUT=1), so a gate never passes it on fewer
		///folders.</summary>
		private static void CheckBuiltProgramsAndTests(List<ProjectInfo> listProjects,bool isRequired) {
			List<string> listProblems=new List<string>();
			List<string> listNotBuilt=new List<string>();
			int countFolders=0;
			foreach(ProjectInfo projectInfo in listProjects) {
				string folder=projectInfo.OutputFolder;
				if(folder==null) {
					listNotBuilt.Add(RepoPaths.Relative(projectInfo.Path)+" ("+projectInfo.PrimaryOutputName+")");
					continue;
				}
				countFolders++;
				string labelFolder=RepoPaths.Relative(folder);
				//The very library files: a same-named file from elsewhere (an older copy in Visual Studio's test platform folder, a stale build, another
				//version of the package) is not what the release ships.
				foreach(string path in Directory.GetFiles(folder)) {
					if(Libraries.Files.TryGetValue(Path.GetFileName(path),out string pathLibrary)
						&& MetadataReader.Sha256(path)!=MetadataReader.Sha256(pathLibrary))
					{
						string packageId=Libraries.PackageOf(Path.GetFileName(path));
						listProblems.Add(labelFolder+"\\"+Path.GetFileName(path)+" is not "+(packageId==null ? "the file in Required dlls"
							: "the file of the package "+packageId+" "+Libraries.LockedVersions[packageId])+".");
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
			Assert.AreEqual(0,listProblems.Count,listProblems.Count+" problem(s); a rebuild of the project may be needed:\r\n"
				+string.Join("\r\n",listProblems.Distinct())
				+(listNotBuilt.Count==0 ? "" : "\r\nNot built, so not checked: "+string.Join(", ",listNotBuilt)));
			if(listNotBuilt.Count>0) {
				BuildOutput.NotBuilt((countFolders==0 ? "Nothing is built" : listNotBuilt.Count+" of "+(countFolders+listNotBuilt.Count)+" programs and tests "
					+"that use the libraries are not built")+": "+string.Join(", ",listNotBuilt.Take(20))+(listNotBuilt.Count>20 ? ", ..." : "")
					+". Build OpenDental.Local.slnf (Debug|x86) first.",isRequired);
			}
		}
	}
}
