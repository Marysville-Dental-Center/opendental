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
			//WpfControlsOD until S1-4 made it x86: its Debug|AnyCPU group gave the output folder.
			if(Regex.IsMatch(textSln,@"\{118540CC-3536-4E8B-94D4-402CA48A4CB0\}\.Debug\|x86\.ActiveCfg = Debug\|Any CPU")) {
				ProjectFile projectFileWpf=ProjectFile.Load(Path.Combine(root,"WpfControlsOD","WpfControlsOD.csproj"));
				Assert.AreEqual("AnyCPU",projectFileWpf.Platform);
				Assert.AreEqual(@"bin\Debug\",projectFileWpf.GetProperty("OutputPath"));
				Assert.AreEqual(@"obj\Debug\",projectFileWpf.GetProperty("IntermediateOutputPath"));
				//Read as x86 it would get the common targets' default instead, which the build never uses.
				Assert.AreEqual(@"bin\x86\Debug\",ProjectFile.Load(Path.Combine(root,"WpfControlsOD","WpfControlsOD.csproj"),"Debug","x86").GetProperty("OutputPath"));
			}
			//CryptoTests (S1-T2's item 7, asserted on its own since S1-T3): the solution builds its Debug|x86 as Debug|Any CPU, and the output folder
			//is bin\Debug\, where the test runners look for CryptoTests.dll. A mapping fix to x86 must change this test.
			StringAssert.Contains(textSln,"{E5185629-91EA-4199-9623-1FF57BBF9708}.Debug|x86.ActiveCfg = Debug|Any CPU","OpenDental.sln maps CryptoTests' Debug|x86.");
			ProjectFile projectFileCrypto=ProjectFile.Load(Path.Combine(root,"tests","CryptoTests","CryptoTests.csproj"));
			Assert.AreEqual("Debug|AnyCPU",projectFileCrypto.Configuration+"|"+projectFileCrypto.Platform);
			Assert.AreEqual(@"bin\Debug\",projectFileCrypto.GetProperty("OutputPath"));
			Assert.AreEqual(@"obj\Debug\",projectFileCrypto.GetProperty("IntermediateOutputPath"));
			Assert.AreEqual(Path.Combine(root,"tests","CryptoTests","bin","Debug","CryptoTests.dll"),projectFileCrypto.GetProperty("TargetPath"));
			Assert.AreEqual("x86",projectFileCrypto.GetProperty("PlatformTarget"),"The repository's Directory.Build.props', for the AnyCPU platform too.");
			//Read as x86 the folder is the same: an SDK-style project since S1-6, it gets the flat output folder for every platform (the classic
			//file's group for a platform other than AnyCPU gave bin\x86\Debug\, which the build never used).
			Assert.AreEqual(@"bin\Debug\",ProjectFile.Load(projectFileCrypto.FullPath,"Debug","x86").GetProperty("OutputPath"));
			Assert.IsTrue(countAnyCpu>=1,"CryptoTests at least.");
			Assert.AreEqual("x86",ProjectFile.Load(Path.Combine(root,"OpenPmsDental","OpenPmsDental.csproj")).Platform);
		}

		[TestMethod]
		public void TheSolutionsGlobalProperties_WinOverTheProjectsOwn_AndAProjectOnItsOwnKeepsItsOwn() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//MSBuild 18 (S1-T3's scratch evaluation): the solution build gives every project SolutionDir, SolutionPath, SolutionName,
				//SolutionExt and SolutionFileName as global properties, which a project's own assignment cannot change; built on its own, the
				//project's assignment wins and the common targets give the others *Undefined*.
				const string anchor="<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>";
				fixtureFolder.EditProject("Classic",anchor,anchor+"<FixtureBefore>[$(SolutionDir)]</FixtureBefore><SolutionDir>..\\..\\</SolutionDir>"
					+"<FixtureAfter>[$(SolutionDir)]</FixtureAfter>");
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				string folder=fixtureFolder.Root+@"\";
				Assert.AreEqual(folder,projectFile.GetProperty("SolutionDir"));
				Assert.AreEqual("["+folder+"]",projectFile.GetProperty("FixtureBefore"));
				Assert.AreEqual("["+folder+"]",projectFile.GetProperty("FixtureAfter"),"The project's own assignment is ignored.");
				Assert.AreEqual(fixtureFolder.Combine("OpenDental.sln"),projectFile.GetProperty("SolutionPath"));
				Assert.AreEqual("OpenDental",projectFile.GetProperty("SolutionName"));
				Assert.AreEqual(".sln",projectFile.GetProperty("SolutionExt"));
				Assert.AreEqual("OpenDental.sln",projectFile.GetProperty("SolutionFileName"));
				Assert.AreEqual("*Undefined*",projectFile.GetProperty("DevEnvDir"));
				//A project the solution does not list is built on its own.
				fixtureFolder.WriteFile(@"tests\Unlisted\Unlisted.csproj",System.IO.File.ReadAllText(fixtureFolder.ProjectPath("Classic")));
				ProjectFile projectFileUnlisted=ProjectFile.Load(fixtureFolder.Combine(@"tests\Unlisted\Unlisted.csproj"));
				Assert.AreEqual(@"..\..\",projectFileUnlisted.GetProperty("SolutionDir"));
				Assert.AreEqual("[]",projectFileUnlisted.GetProperty("FixtureBefore"));
				Assert.AreEqual(@"[..\..\]",projectFileUnlisted.GetProperty("FixtureAfter"));
				Assert.AreEqual("*Undefined*",projectFileUnlisted.GetProperty("SolutionName"));
				Assert.AreEqual("*Undefined*",projectFileUnlisted.GetProperty("SolutionPath"));
				//The repository's: the folder of OpenDental.sln, as the build lane's solution filter build and tools\Get-ProjectBaseline.ps1 give it.
				string root=FixtureFolder.RepoRoot();
				Assert.AreEqual(root+@"\",ProjectFile.Load(Path.Combine(root,"OpenPmsDental","OpenPmsDental.csproj")).GetProperty("SolutionDir"));
			}
		}
	}
}
