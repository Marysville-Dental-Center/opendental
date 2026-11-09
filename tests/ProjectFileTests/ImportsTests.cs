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
		public void TheRestoresPackageImports_AreRead_FromItsPackageFolder() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//The form NuGet's restore writes (S2-1's packages with build files): $(NuGetPackageRoot) through $(UserProfile), the same folder
				//literally in NuGetPackageFolders, and each package's build file behind an Exists condition.
				string packages=fixtureFolder.Combine(@"Packages")+@"\";
				fixtureFolder.WriteFile(@"tests\SdkStyle\obj\SdkStyle.csproj.nuget.g.props","<Project><PropertyGroup Condition=\" '$(ExcludeRestorePackageImports)' != 'true' \">"
					+"<NuGetPackageRoot Condition=\" '$(NuGetPackageRoot)' == '' \">$(UserProfile)\\.nuget\\packages\\</NuGetPackageRoot>"
					+"<NuGetPackageFolders Condition=\" '$(NuGetPackageFolders)' == '' \">"+packages+"</NuGetPackageFolders></PropertyGroup></Project>");
				const string IMPORT="$(NuGetPackageRoot)fixture.package\\1.2.3\\buildTransitive\\net462\\Fixture.Package.targets";
				const string IMPORT_MISSING="$(NuGetPackageRoot)fixture.other\\4.5.6\\buildTransitive\\net462\\Fixture.Other.targets";
				fixtureFolder.WriteFile(@"tests\SdkStyle\obj\SdkStyle.csproj.nuget.g.targets","<Project><ImportGroup Condition=\" '$(ExcludeRestorePackageImports)' != 'true' \">"
					+"<Import Project=\""+IMPORT+"\" Condition=\"Exists('"+IMPORT+"')\" /><Import Project=\""+IMPORT_MISSING+"\" Condition=\"Exists('"+IMPORT_MISSING+"')\" />"
					+"</ImportGroup></Project>");
				fixtureFolder.WriteFile(@"Packages\fixture.package\1.2.3\buildTransitive\net462\Fixture.Package.targets","<Project><PropertyGroup>"
					+"<FixtureFromPackage>$(MSBuildThisFileDirectory)</FixtureFromPackage></PropertyGroup><ItemGroup><RedistList Include=\"$(MSBuildThisFileDirectory)List.xml\" />"
					+"</ItemGroup><Target Name=\"FixturePackageTarget\" /></Project>");
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				string folderBuild=fixtureFolder.Combine(@"Packages\fixture.package\1.2.3\buildTransitive\net462")+@"\";
				Assert.AreEqual(folderBuild,projectFile.GetProperty("FixtureFromPackage"));
				Assert.AreEqual(1,projectFile.GetItems("RedistList").Count);
				CollectionAssert.Contains(projectFile.FilesRead,folderBuild+"Fixture.Package.targets");
				Assert.IsFalse(projectFile.FilesRead.Any(x => x.EndsWith("Fixture.Other.targets")),"A package file that is not there is not imported.");
				if(Environment.GetEnvironmentVariable("UserProfile")!=null) {
					AssertThrows<NotSupportedException>(() => projectFile.GetProperty("NuGetPackageRoot"),"$(UserProfile)");
				}
				//Another form of the import, or such an import outside the restore's files, is not read.
				fixtureFolder.EditFile(@"tests\SdkStyle\obj\SdkStyle.csproj.nuget.g.targets","Condition=\"Exists('"+IMPORT+"')\"","");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"another form");
				fixtureFolder.DeleteFile(@"tests\SdkStyle\obj\SdkStyle.csproj.nuget.g.targets");
				fixtureFolder.EditProject("SdkStyle",ANCHOR_SDK_ITEMS,ANCHOR_SDK_ITEMS+"</ItemGroup><Import Project=\""+IMPORT+"\" Condition=\"Exists('"+IMPORT+"')\" /><ItemGroup>");
				if(Environment.GetEnvironmentVariable("UserProfile")!=null) {
					//Its condition needs $(UserProfile), so whether MSBuild imports it is not known here.
					AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"does not read");
				}
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

		#region Central package versions
		///<summary>A Directory.Packages.props as the repository's (S2-1): everything guarded for SDK-style projects, two central versions, and a
		///property that shows what it sees of Directory.Build.props.</summary>
		private const string PACKAGES_PROPS="<Project><PropertyGroup Condition=\"'$(UsingMicrosoftNETSdk)' == 'true'\">"
			+"<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally><FixturePackagesSee>[$(FixtureEveryProject)]</FixturePackagesSee>"
			+"</PropertyGroup><ItemGroup Condition=\"'$(UsingMicrosoftNETSdk)' == 'true'\"><PackageVersion Include=\"Fixture.Package\" Version=\"1.2.3\" />"
			+"<PackageVersion Include=\"Fixture.Other\" Version=\"4.5.6\" /></ItemGroup></Project>";
		private const string ANCHOR_SDK_ITEMS="<ProjectReference Include=\"..\\Classic\\Classic.csproj\" />";

		[TestMethod]
		public void DirectoryPackagesProps_IsImported_AfterDirectoryBuildProps_AndGivesTheCentralVersions() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//NuGet.props (the end of Microsoft.Common.props): found above the project, read after Directory.Build.props and before the project's
				//own properties.
				fixtureFolder.WriteFile("Directory.Packages.props",PACKAGES_PROPS);
				fixtureFolder.EditProject("SdkStyle",ANCHOR_SDK_ITEMS,ANCHOR_SDK_ITEMS+"<PackageReference Include=\"Fixture.Package\" />"
					+"<PackageReference Include=\"Fixture.Other\" VersionOverride=\"4.5.7\" />");
				fixtureFolder.EditProject("SdkStyle","<OutputType>WinExe</OutputType>","<OutputType>WinExe</OutputType>"
					+"<FixtureBodySees>$(ManagePackageVersionsCentrally)</FixtureBodySees>");
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				Assert.AreEqual("true",projectFile.GetProperty("ImportDirectoryPackagesProps"));
				Assert.AreEqual("Directory.Packages.props",projectFile.GetProperty("_DirectoryPackagesPropsFile"));
				Assert.AreEqual(fixtureFolder.Root,projectFile.GetProperty("_DirectoryPackagesPropsBasePath"));
				Assert.AreEqual(fixtureFolder.Combine("Directory.Packages.props"),projectFile.GetProperty("DirectoryPackagesPropsPath"));
				Assert.AreEqual("true",projectFile.GetProperty("CentralPackageVersionsFileImported"));
				Assert.AreEqual("true",projectFile.GetProperty("ManagePackageVersionsCentrally"));
				Assert.AreEqual("true",projectFile.GetProperty("_CentralPackageVersionsEnabled"),"NuGet.targets' switch.");
				Assert.AreEqual("[root]",projectFile.GetProperty("FixturePackagesSee"),"After Directory.Build.props.");
				Assert.AreEqual("true",projectFile.GetProperty("FixtureBodySees"),"Before the project's own properties.");
				CollectionAssert.Contains(projectFile.FilesRead,fixtureFolder.Combine("Directory.Packages.props"));
				Assert.AreEqual(2,projectFile.GetItems("PackageVersion").Count);
				Assert.AreEqual("1.2.3",projectFile.GetPackageVersion("Fixture.Package"));
				Assert.AreEqual("1.2.3",projectFile.GetPackageVersion("fixture.package"),"NuGet's ids ignore case.");
				Assert.AreEqual("4.5.7",projectFile.GetPackageVersion("Fixture.Other"),"VersionOverride.");
				Assert.IsNull(projectFile.GetPackageVersion("Fixture.NotReferenced"));
				Assert.IsNotNull(projectFile.GetPackageReference("Fixture.Package"));
				//A classic project imports it too, but its guarded groups give it nothing.
				ProjectFile projectFileClassic=fixtureFolder.Load("Classic");
				Assert.AreEqual("true",projectFileClassic.GetProperty("CentralPackageVersionsFileImported"));
				Assert.AreEqual("",projectFileClassic.GetProperty("ManagePackageVersionsCentrally"));
				Assert.AreEqual("",projectFileClassic.GetProperty("_CentralPackageVersionsEnabled"));
				Assert.AreEqual(0,projectFileClassic.GetItems("PackageVersion").Count);
				//Without central package management, a PackageReference's own Version.
				fixtureFolder.EditFile("Directory.Packages.props","<ManagePackageVersionsCentrally>true","<ManagePackageVersionsCentrally>false");
				fixtureFolder.EditProject("SdkStyle","<PackageReference Include=\"Fixture.Package\" />","<PackageReference Include=\"Fixture.Package\" Version=\"9.9.9\" />");
				projectFile=fixtureFolder.Load("SdkStyle");
				Assert.AreEqual("",projectFile.GetProperty("_CentralPackageVersionsEnabled"));
				Assert.AreEqual("9.9.9",projectFile.GetPackageVersion("Fixture.Package"));
			}
		}

		[TestMethod]
		public void CentralPackageVersions_WhatTheRestoreRefuses_Throws() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.WriteFile("Directory.Packages.props",PACKAGES_PROPS);
				//NU1008: a version on the PackageReference; NU1010: no PackageVersion; NU1013: VersionOverride turned off.
				fixtureFolder.EditProject("SdkStyle",ANCHOR_SDK_ITEMS,ANCHOR_SDK_ITEMS+"<PackageReference Include=\"Fixture.Package\" Version=\"1.0.0\" />"
					+"<PackageReference Include=\"Fixture.Missing\" /><PackageReference Include=\"Fixture.Other\" VersionOverride=\"4.5.7\" />");
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				AssertThrows<InvalidOperationException>(() => projectFile.GetPackageVersion("Fixture.Package"),"NU1008");
				AssertThrows<InvalidOperationException>(() => projectFile.GetPackageVersion("Fixture.Missing"),"NU1010");
				fixtureFolder.EditFile("Directory.Packages.props","<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>",
					"<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally><CentralPackageVersionOverrideEnabled>false</CentralPackageVersionOverrideEnabled>");
				AssertThrows<InvalidOperationException>(() => fixtureFolder.Load("SdkStyle").GetPackageVersion("Fixture.Other"),"NU1013");
				//GlobalPackageReference items, which NuGet.targets turns into PackageReference and PackageVersion items, are not modelled.
				fixtureFolder.EditFile("Directory.Packages.props","<PackageVersion Include=\"Fixture.Package\"","<GlobalPackageReference Include=\"Fixture.Global\" Version=\"1.0.0\" />"
					+"<PackageVersion Include=\"Fixture.Package\"");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"GlobalPackageReference");
			}
		}

		[TestMethod]
		public void DirectoryPackagesProps_TheSwitchAndThePathTheProjectNames() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.WriteFile("Directory.Packages.props",PACKAGES_PROPS);
				fixtureFolder.WriteFile(@"Shared\Other.Packages.props","<Project><PropertyGroup><FixtureOtherPackages>other</FixtureOtherPackages></PropertyGroup></Project>");
				//ImportDirectoryPackagesProps false: nothing is imported, and the path is not looked for.
				fixtureFolder.EditFile(@"tests\Directory.Build.props","<Import Project=","<PropertyGroup><ImportDirectoryPackagesProps>false</ImportDirectoryPackagesProps>"
					+"</PropertyGroup><Import Project=");
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				Assert.AreEqual("false",projectFile.GetProperty("ImportDirectoryPackagesProps"));
				Assert.AreEqual("",projectFile.GetProperty("DirectoryPackagesPropsPath"));
				Assert.AreEqual("",projectFile.GetProperty("CentralPackageVersionsFileImported"));
				Assert.AreEqual("",projectFile.GetProperty("ManagePackageVersionsCentrally"));
				Assert.AreEqual(0,projectFile.GetItems("PackageVersion").Count);
				//A path the project names itself is the file imported.
				fixtureFolder.EditFile(@"tests\Directory.Build.props","<ImportDirectoryPackagesProps>false</ImportDirectoryPackagesProps>",
					"<DirectoryPackagesPropsPath>$(MSBuildThisFileDirectory)..\\Shared\\Other.Packages.props</DirectoryPackagesPropsPath>");
				projectFile=fixtureFolder.Load("SdkStyle");
				Assert.AreEqual("other",projectFile.GetProperty("FixtureOtherPackages"));
				Assert.AreEqual("true",projectFile.GetProperty("CentralPackageVersionsFileImported"));
				Assert.AreEqual("",projectFile.GetProperty("ManagePackageVersionsCentrally"),"Not the Directory.Packages.props above.");
				Assert.AreEqual("",projectFile.GetProperty("_DirectoryPackagesPropsBasePath"),"Not looked for.");
			}
		}

		[TestMethod]
		public void RepositoryProjects_GetTheirPackageVersions_FromDirectoryPackagesProps() {
			//The repository's own file (S2-1): every SDK-style project reads it, a classic one gets none of its versions.
			string root=FixtureFolder.RepoRoot();
			string pathPackagesProps=Path.Combine(root,"Directory.Packages.props");
			ProjectFile projectFile=ProjectFile.Load(Path.Combine(root,"OpenPmsDentalBusiness","OpenPmsDentalBusiness.csproj"));
			CollectionAssert.Contains(projectFile.FilesRead,pathPackagesProps);
			Assert.AreEqual("true",projectFile.GetProperty("_CentralPackageVersionsEnabled"));
			Assert.AreEqual("true",projectFile.GetProperty("CentralPackageTransitivePinningEnabled"));
			Assert.AreEqual("4.92.0",projectFile.GetPackageVersion("Microsoft.Identity.Client"));
			Assert.AreEqual("13.0.4",projectFile.GetPackageVersion("Newtonsoft.Json"));
			Assert.AreEqual("compile",projectFile.GetPackageReference("Google.Apis").GetMetadata("PrivateAssets"));
			ProjectFile projectFileServer=ProjectFile.Load(Path.Combine(root,"OpenPmsDentalServer","OpenPmsDentalServer.csproj"));
			Assert.IsFalse(projectFileServer.IsSdkStyle);
			Assert.AreEqual("",projectFileServer.GetProperty("ManagePackageVersionsCentrally"));
			Assert.AreEqual(0,projectFileServer.GetItems("PackageVersion").Count);
		}
		#endregion Central package versions

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
