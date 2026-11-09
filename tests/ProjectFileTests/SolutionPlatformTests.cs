// ProjectFile.Load(path) evaluates a project with the configuration and platform OpenDental.sln builds it with for the build lane's Debug|x86
// (package S1-T2 of the .NET 10 plan), as tools\Get-ProjectBaseline.ps1 and the solution build do: Debug|AnyCPU for WpfControlsOD and CryptoTests
// today, so the properties of their Debug|AnyCPU groups are read as the build reads them.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	[TestClass]
	public class SolutionPlatformTests {
		private const string CLASSIC_DEBUG_X86="{6E0B8C0A-1F2D-4E3B-9A55-0C1D2E3F4A5B}.Debug|x86.ActiveCfg = Debug|x86";

		[TestMethod]
		public void Load_TakesTheConfigurationAndPlatformTheSolutionMaps() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("x86",projectFile.Platform);
				Assert.AreEqual("DEBUG;TRACE",projectFile.GetProperty("DefineConstants"),"The Debug|x86 group.");
				fixtureFolder.EditFile("OpenDental.sln",CLASSIC_DEBUG_X86,"{6E0B8C0A-1F2D-4E3B-9A55-0C1D2E3F4A5B}.Debug|x86.ActiveCfg = Debug|Any CPU");
				projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("AnyCPU",projectFile.Platform,"\"Any CPU\" is AnyCPU for the project, as the solution build passes it.");
				Assert.AreEqual("Debug",projectFile.Configuration);
				Assert.AreEqual("",projectFile.GetProperty("DefineConstants"),"The Debug|x86 group is not read.");
				Assert.AreEqual(@"obj\Debug\",projectFile.GetProperty("IntermediateOutputPath"));
				//Another configuration name works the same way.
				fixtureFolder.EditFile("OpenDental.sln","{6E0B8C0A-1F2D-4E3B-9A55-0C1D2E3F4A5B}.Debug|x86.ActiveCfg = Debug|Any CPU",
					"{6E0B8C0A-1F2D-4E3B-9A55-0C1D2E3F4A5B}.Debug|x86.ActiveCfg = Release|x86");
				Assert.AreEqual(@"bin\Release\",fixtureFolder.Load("Classic").GetProperty("OutputPath"));
				//An explicit configuration and platform win.
				Assert.AreEqual(@"bin\Debug\",ProjectFile.Load(fixtureFolder.ProjectPath("Classic"),"Debug","x86").GetProperty("OutputPath"));
			}
		}

		[TestMethod]
		public void Load_OfAProjectTheSolutionDoesNotList_IsDebugX86() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.WriteFile(@"tests\Unlisted\Unlisted.csproj",File.ReadAllText(fixtureFolder.ProjectPath("Classic")));
				ProjectFile projectFile=ProjectFile.Load(fixtureFolder.Combine(@"tests\Unlisted\Unlisted.csproj"));
				Assert.AreEqual("Debug|x86",projectFile.Configuration+"|"+projectFile.Platform);
				string configuration;
				string platform;
				ProjectFile.GetSolutionConfiguration(fixtureFolder.Combine(@"tests\Unlisted\Unlisted.csproj"),"Release","x86",out configuration,out platform);
				Assert.AreEqual("Release|x86",configuration+"|"+platform);
			}
		}

		[TestMethod]
		public void RepositoryProjects_LoadWithTheSolutionsPlatform() {
			string root=FixtureFolder.RepoRoot();
			string textSln=File.ReadAllText(Path.Combine(root,"OpenDental.sln"));
			List<string> listProblems=new List<string>();
			int countAnyCpu=0;
			foreach(string path in ProjectFile.GetSolutionFilterProjects(Path.Combine(root,"OpenDental.Local.slnf"))) {
				//The mapping read straight from the solution, as tools\Get-ProjectBaseline.ps1 reads it.
				string relative=path.Substring(root.Length+1);
				Match matchProject=Regex.Match(textSln,"Project\\(\"\\{[^}]+\\}\"\\) = \"[^\"]+\", \""+Regex.Escape(relative)+"\", \"(\\{[^}]+\\})\"",RegexOptions.IgnoreCase);
				string expected="Debug|x86";
				if(matchProject.Success) {
					Match matchConfiguration=Regex.Match(textSln,Regex.Escape(matchProject.Groups[1].Value)+@"\.Debug\|x86\.ActiveCfg = ([^|\r\n]+)\|([^\r\n]+)",RegexOptions.IgnoreCase);
					if(matchConfiguration.Success) {
						expected=matchConfiguration.Groups[1].Value.Trim()+"|"+Regex.Replace(matchConfiguration.Groups[2].Value.Trim(),"^Any CPU$","AnyCPU");
					}
				}
				ProjectFile projectFile=ProjectFile.Load(path);
				if(projectFile.Configuration+"|"+projectFile.Platform!=expected) {
					listProblems.Add(relative+": "+projectFile.Configuration+"|"+projectFile.Platform+", the solution maps "+expected);
				}
				if(projectFile.Platform=="AnyCPU") {
					countAnyCpu++;
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("; ",listProblems));
			//Today WpfControlsOD and CryptoTests (until the lead's mapping fix of stage 1): their Debug|AnyCPU groups give the output folder.
			if(Regex.IsMatch(textSln,@"\{118540CC-3536-4E8B-94D4-402CA48A4CB0\}\.Debug\|x86\.ActiveCfg = Debug\|Any CPU")) {
				ProjectFile projectFileWpf=ProjectFile.Load(Path.Combine(root,"WpfControlsOD","WpfControlsOD.csproj"));
				Assert.AreEqual("AnyCPU",projectFileWpf.Platform);
				Assert.AreEqual(@"bin\Debug\",projectFileWpf.GetProperty("OutputPath"));
				Assert.AreEqual(@"obj\Debug\",projectFileWpf.GetProperty("IntermediateOutputPath"));
				//Read as x86 it would get the common targets' default instead, which the build never uses.
				Assert.AreEqual(@"bin\x86\Debug\",ProjectFile.Load(Path.Combine(root,"WpfControlsOD","WpfControlsOD.csproj"),"Debug","x86").GetProperty("OutputPath"));
				Assert.IsTrue(countAnyCpu>=1);
			}
			Assert.AreEqual("x86",ProjectFile.Load(Path.Combine(root,"OpenPmsDental","OpenPmsDental.csproj")).Platform);
		}
	}
}
