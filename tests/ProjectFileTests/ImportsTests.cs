// The files MSBuild imports for a project that ProjectFile skipped before package S1-T3 of the .NET 10 plan (docs/specs/net10-plan.md, S1-T2's
// review): the project's .user file (Microsoft.Common.CurrentVersion.targets) and the project extension files NuGet's restore writes
// (obj\Project.csproj.*.props, read with Microsoft.Common.props, and obj\Project.csproj.*.targets, read with Microsoft.Common.targets), now read
// where MSBuild reads them; and environment variables, which MSBuild reads as properties and ProjectFile does not model, so a $(Name) that is
// one throws instead of reading as ''. The expected values are MSBuild 18's for the same files (S1-T3's scratch evaluation of the fixtures).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	[TestClass]
	public class ImportsTests {
		private const string ANCHOR_CLASSIC="<Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />";

		private static void AssertThrows<T>(Action action,string expectedText) where T:Exception {
			try {
				action();
			}
			catch(T ex) {
				StringAssert.Contains(ex.Message,expectedText);
				return;
			}
			Assert.Fail("No "+typeof(T).Name+" for "+expectedText);
		}

		#region Project extensions
		[TestMethod]
		public void TheRestoresProjectExtensions_AreRead_InTheOrderOfTheirNames() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//MSBuild 18: obj\Classic.csproj.a.props then .b.props (MSBuild sorts the wildcard's files), before the project's own properties;
				//obj\Classic.csproj.x.targets after the common targets' properties and before Directory.Build.targets; another project's file not.
				fixtureFolder.WriteFile(@"tests\Classic\obj\Classic.csproj.b.props","<Project><PropertyGroup><FixtureExtProps>b</FixtureExtProps></PropertyGroup></Project>");
				fixtureFolder.WriteFile(@"tests\Classic\obj\Classic.csproj.a.props","<Project><PropertyGroup><FixtureExtProps>a</FixtureExtProps>"
					+"<FixtureExtFirst>a</FixtureExtFirst></PropertyGroup></Project>");
				fixtureFolder.WriteFile(@"tests\Classic\obj\Classic.csproj.x.targets","<Project><PropertyGroup><FixtureExtTargets>targets:$(OutputPath)</FixtureExtTargets>"
					+"</PropertyGroup></Project>");
				fixtureFolder.WriteFile(@"tests\Classic\obj\Other.csproj.x.props","<Project><PropertyGroup><FixtureOther>other</FixtureOther></PropertyGroup></Project>");
				fixtureFolder.EditFile("Directory.Build.targets","</Project>","<PropertyGroup><FixtureSeenByTargets>[$(FixtureExtTargets)]</FixtureSeenByTargets>"
					+"</PropertyGroup></Project>");
				fixtureFolder.EditProject("Classic","<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>",
					"<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects><FixtureBodySees>$(FixtureExtProps)</FixtureBodySees>");
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("b",projectFile.GetProperty("FixtureExtProps"),"Read in the order of their names, so b's value is the last.");
				Assert.AreEqual("a",projectFile.GetProperty("FixtureExtFirst"));
				Assert.AreEqual("b",projectFile.GetProperty("FixtureBodySees"),"Before the project's own properties.");
				Assert.AreEqual(@"targets:bin\Debug\",projectFile.GetProperty("FixtureExtTargets"),"After the common targets' OutputPath.");
				Assert.AreEqual(@"[targets:bin\Debug\]",projectFile.GetProperty("FixtureSeenByTargets"),"Before Directory.Build.targets.");
				Assert.AreEqual("",projectFile.GetProperty("FixtureOther"),"Another project's file is not imported.");
				CollectionAssert.IsSubsetOf(new[] { fixtureFolder.Combine(@"tests\Classic\obj\Classic.csproj.a.props"),
					fixtureFolder.Combine(@"tests\Classic\obj\Classic.csproj.b.props"),fixtureFolder.Combine(@"tests\Classic\obj\Classic.csproj.x.targets") },
					projectFile.FilesRead);
				//The switches turn them off.
				fixtureFolder.EditFile(@"tests\Directory.Build.props","<Import Project=","<PropertyGroup><ImportProjectExtensionProps>false</ImportProjectExtensionProps>"
					+"<ImportProjectExtensionTargets>false</ImportProjectExtensionTargets></PropertyGroup><Import Project=");
				projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("",projectFile.GetProperty("FixtureExtProps"));
				Assert.AreEqual("",projectFile.GetProperty("FixtureExtTargets"));
				Assert.AreEqual("",projectFile.GetProperty("_InitialMSBuildProjectExtensionsPath"),"Set only when they are imported.");
			}
		}

		[TestMethod]
		public void NuGetsRestoreProps_GiveTheSdkStyleProjectItsAssetsFile_AndTheirEnvironmentVariableThrows() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//The form NuGet's restore writes obj\Project.csproj.nuget.g.props in for every SDK-style project of the repository: its
				//NuGetPackageRoot comes from the environment variable UserProfile, which this reader does not model.
				fixtureFolder.WriteFile(@"tests\SdkStyle\obj\SdkStyle.csproj.nuget.g.props","<Project><PropertyGroup Condition=\" '$(ExcludeRestorePackageImports)' != 'true' \">"
					+"<RestoreSuccess Condition=\" '$(RestoreSuccess)' == '' \">True</RestoreSuccess>"
					+"<ProjectAssetsFile Condition=\" '$(ProjectAssetsFile)' == '' \">$(MSBuildThisFileDirectory)project.assets.json</ProjectAssetsFile>"
					+"<NuGetPackageRoot Condition=\" '$(NuGetPackageRoot)' == '' \">$(UserProfile)\\.nuget\\packages\\</NuGetPackageRoot>"
					+"<NuGetPackageFolders Condition=\" '$(NuGetPackageFolders)' == '' \">C:\\Packages\\</NuGetPackageFolders>"
					+"</PropertyGroup><ItemGroup Condition=\" '$(ExcludeRestorePackageImports)' != 'true' \"><SourceRoot Include=\"C:\\Packages\\\" /></ItemGroup></Project>");
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				Assert.AreEqual("True",projectFile.GetProperty("RestoreSuccess"));
				Assert.AreEqual(fixtureFolder.Combine(@"tests\SdkStyle\obj\project.assets.json"),projectFile.GetProperty("ProjectAssetsFile"));
				Assert.AreEqual(@"C:\Packages\",projectFile.GetProperty("NuGetPackageFolders"));
				Assert.AreEqual(1,projectFile.GetItems("SourceRoot").Count);
				if(Environment.GetEnvironmentVariable("UserProfile")!=null) {
					AssertThrows<NotSupportedException>(() => projectFile.GetProperty("NuGetPackageRoot"),"$(UserProfile)");
				}
				//Without the restore's file, the assets file is the SDK's default in the same folder, and the package root is ''.
				fixtureFolder.DeleteFile(@"tests\SdkStyle\obj\SdkStyle.csproj.nuget.g.props");
				projectFile=fixtureFolder.Load("SdkStyle");
				Assert.AreEqual(fixtureFolder.Combine(@"tests\SdkStyle\obj\project.assets.json"),projectFile.GetProperty("ProjectAssetsFile"));
				Assert.AreEqual("",projectFile.GetProperty("NuGetPackageRoot"));
			}
		}

		[TestMethod]
		public void RepositoryProjects_ReadTheirRestoresProps() {
			//Every SDK-style project of the solution filter that the build restored reads its obj\*.nuget.g.props, as MSBuild does.
			string root=FixtureFolder.RepoRoot();
			ProjectFile projectFile=ProjectFile.Load(Path.Combine(root,"CodeBase","xCodeBase.csproj"));
			string pathProps=Path.Combine(root,"CodeBase","obj","xCodeBase.csproj.nuget.g.props");
			if(!File.Exists(pathProps)) {
				BuildOutput.NotBuilt("CodeBase's restore output "+pathProps);
				return;
			}
			CollectionAssert.Contains(projectFile.FilesRead,pathProps);
			Assert.AreEqual("PackageReference",projectFile.GetProperty("NuGetProjectStyle"));
			Assert.AreEqual(Path.Combine(root,"CodeBase","obj","project.assets.json"),projectFile.GetProperty("ProjectAssetsFile"));
		}
		#endregion Project extensions

		#region The .user file
		[TestMethod]
		public void TheProjectsUserFile_IsRead_AfterItsOwnPropertiesAndBeforeTheCommonTargets() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//MSBuild 18: Classic.csproj.user sees the project's OutputType, not yet the common targets' TargetExt; Directory.Build.targets sees it.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj.user","<Project><PropertyGroup><FixtureFromUser>user:$(OutputType)</FixtureFromUser>"
					+"<FixtureUserSeesTargets>$(TargetExt)</FixtureUserSeesTargets></PropertyGroup></Project>");
				fixtureFolder.EditFile("Directory.Build.targets","</Project>","<PropertyGroup><FixtureSeenByTargets>[$(FixtureFromUser)]</FixtureSeenByTargets>"
					+"</PropertyGroup></Project>");
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("user:WinExe",projectFile.GetProperty("FixtureFromUser"));
				Assert.AreEqual("",projectFile.GetProperty("FixtureUserSeesTargets"));
				Assert.AreEqual("[user:WinExe]",projectFile.GetProperty("FixtureSeenByTargets"));
				CollectionAssert.Contains(projectFile.FilesRead,fixtureFolder.Combine(@"tests\Classic\Classic.csproj.user"));
				//The SDK-style fixture has one too (an empty project).
				CollectionAssert.Contains(fixtureFolder.Load("SdkStyle").FilesRead,fixtureFolder.Combine(@"tests\SdkStyle\SdkStyle.csproj.user"));
				//An empty one fails MSBuild's evaluation; ProjectFile's too.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj.user","");
				AssertThrows<XmlException>(() => fixtureFolder.Load("Classic"),"");
			}
		}
		#endregion The .user file

		#region Environment variables
		[TestMethod]
		public void AnEnvironmentVariable_IsNotModelled_UnlessAFileSetsIt() {
			const string name="OPMS_PROJECTFILE_FIXTURE_VARIABLE";
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				string textClassic=File.ReadAllText(fixtureFolder.ProjectPath("Classic"));
				Environment.SetEnvironmentVariable(name,"set");
				try {
					//MSBuild would read 'set'; ProjectFile throws in a condition, an include and GetProperty.
					fixtureFolder.EditProject("Classic",ANCHOR_CLASSIC,"<PropertyGroup Condition=\"'$("+name+")' != ''\"><FixtureX>1</FixtureX></PropertyGroup>"+ANCHOR_CLASSIC);
					AssertThrows<NotSupportedException>(() => fixtureFolder.Load("Classic"),"$("+name+")");
					fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(ANCHOR_CLASSIC,"<ItemGroup><None Include=\"$("+name+").txt\" /></ItemGroup>"
						+ANCHOR_CLASSIC));
					AssertThrows<NotSupportedException>(() => fixtureFolder.Load("Classic"),"$("+name+")");
					fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic);
					ProjectFile projectFile=fixtureFolder.Load("Classic");
					AssertThrows<NotSupportedException>(() => projectFile.GetProperty(name),"environment variable");
					Assert.IsFalse(projectFile.IsPropertyKnown(name));
					//A file that sets it decides it, as in MSBuild.
					fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(ANCHOR_CLASSIC,"<PropertyGroup><"+name+">project</"+name+">"
						+"</PropertyGroup><PropertyGroup Condition=\"'$("+name+")' == 'project'\"><FixtureX>1</FixtureX></PropertyGroup>"+ANCHOR_CLASSIC));
					projectFile=fixtureFolder.Load("Classic");
					Assert.AreEqual("project",projectFile.GetProperty(name));
					Assert.AreEqual("1",projectFile.GetProperty("FixtureX"));
				}
				finally {
					Environment.SetEnvironmentVariable(name,null);
				}
				//Without the variable: ''.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(ANCHOR_CLASSIC,"<PropertyGroup Condition=\"'$("+name+")' == ''\">"
					+"<FixtureX>1</FixtureX></PropertyGroup>"+ANCHOR_CLASSIC));
				Assert.AreEqual("1",fixtureFolder.Load("Classic").GetProperty("FixtureX"));
				Assert.AreEqual("",fixtureFolder.Load("Classic").GetProperty(name));
			}
		}
		#endregion Environment variables
	}
}
