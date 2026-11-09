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
		public void TheRestoresPackageImports_InAnotherFormOrWithoutAPackageFolder_Throw() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//ReadPackageImport reads only the form the restore writes, from the first folder of $(NuGetPackageFolders) as a full path; anything
				//else throws instead of importing nothing.
				const string IMPORT="$(NuGetPackageRoot)fixture.package\\1.2.3\\buildTransitive\\net462\\Fixture.Package.targets";
				fixtureFolder.WriteFile(@"Packages\fixture.package\1.2.3\buildTransitive\net462\Fixture.Package.targets","<Project><PropertyGroup>"
					+"<FixtureFromPackage>package</FixtureFromPackage></PropertyGroup></Project>");
				Action<string> writeFolders=folders => fixtureFolder.WriteFile(@"tests\SdkStyle\obj\SdkStyle.csproj.nuget.g.props","<Project>"
					+"<PropertyGroup Condition=\" '$(ExcludeRestorePackageImports)' != 'true' \">"
					+(folders==null ? "" : "<NuGetPackageFolders Condition=\" '$(NuGetPackageFolders)' == '' \">"+folders+"</NuGetPackageFolders>")
					+"</PropertyGroup></Project>");
				Action<string> writeImport=project => fixtureFolder.WriteFile(@"tests\SdkStyle\obj\SdkStyle.csproj.nuget.g.targets","<Project>"
					+"<ImportGroup Condition=\" '$(ExcludeRestorePackageImports)' != 'true' \"><Import Project=\""+project+"\" Condition=\"Exists('"+project+"')\" />"
					+"</ImportGroup></Project>");
				writeFolders(fixtureFolder.Combine("Packages")+@"\");
				writeImport(IMPORT);
				Assert.AreEqual("package",fixtureFolder.Load("SdkStyle").GetProperty("FixtureFromPackage"),"The form the restore writes.");
				//A property, an item list or metadata in the path after $(NuGetPackageRoot).
				foreach(string project in new[] { "$(NuGetPackageRoot)fixture.package\\$(FixtureVersion)\\buildTransitive\\net462\\Fixture.Package.targets",
					"$(NuGetPackageRoot)@(FixtureItems)\\Fixture.Package.targets","$(NuGetPackageRoot)%(FixtureItems.Identity)\\Fixture.Package.targets" })
				{
					writeImport(project);
					AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"another form");
				}
				//No package folder: none, a relative one, or one this reader cannot evaluate.
				writeImport(IMPORT);
				foreach(string folders in new[] { null,"","Packages\\","$(MSBuildExtensionsPath)\\packages\\" }) {
					writeFolders(folders);
					AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"names no package folder");
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
				//Unless RestoreEnableGlobalPackageReference is false: NuGet.targets then leaves them as they are, and so does the reader.
				fixtureFolder.EditFile("Directory.Packages.props","<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>",
					"<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally><RestoreEnableGlobalPackageReference>false</RestoreEnableGlobalPackageReference>");
				Assert.AreEqual(1,fixtureFolder.Load("SdkStyle").GetItems("GlobalPackageReference").Count);
			}
		}

		[TestMethod]
		public void TwoPackageVersionsOfOnePackage_Throw_StricterThanNuGetsWarning() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//NuGet reports them as NU1506, a warning, keeps one and restores; the reader refuses, as it does not model which one NuGet keeps.
				fixtureFolder.WriteFile("Directory.Packages.props",PACKAGES_PROPS.Replace("<PackageVersion Include=\"Fixture.Other\"",
					"<PackageVersion Include=\"fixture.package\" Version=\"1.2.4\" /><PackageVersion Include=\"Fixture.Other\""));
				fixtureFolder.EditProject("SdkStyle",ANCHOR_SDK_ITEMS,ANCHOR_SDK_ITEMS+"<PackageReference Include=\"Fixture.Package\" />"
					+"<PackageReference Include=\"Fixture.Other\" />");
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				Assert.AreEqual(3,projectFile.GetItems("PackageVersion").Count,"Both are read, as MSBuild evaluates them.");
				AssertThrows<InvalidOperationException>(() => projectFile.GetPackageVersion("Fixture.Package"),"NU1506");
				Assert.AreEqual("4.5.6",projectFile.GetPackageVersion("Fixture.Other"),"Another package's one version still answers.");
			}
		}

		[TestMethod]
		public void DirectoryPackagesPropsPath_IsResolved_OnlyWhenTheSwitchIsOn() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//NuGet.props tests ImportDirectoryPackagesProps first in each condition, so with the switch off MSBuild never expands the path: a
				//path this reader cannot evaluate (or a relative one) does not matter then. With the switch on, it throws.
				fixtureFolder.WriteFile("Directory.Packages.props",PACKAGES_PROPS);
				const string ANCHOR_TESTS_PROPS="<Import Project=";
				foreach(string[] arrayCase in new[] { new[] { "$(MSBuildExtensionsPath)\\Directory.Packages.props","$(DirectoryPackagesPropsPath)" },
					new[] { @"Shared\Other.Packages.props","a relative $(DirectoryPackagesPropsPath)" } })
				{
					string textTestsProps=File.ReadAllText(fixtureFolder.Combine(@"tests\Directory.Build.props"));
					fixtureFolder.EditFile(@"tests\Directory.Build.props",ANCHOR_TESTS_PROPS,"<PropertyGroup><ImportDirectoryPackagesProps>false</ImportDirectoryPackagesProps>"
						+"<DirectoryPackagesPropsPath>"+arrayCase[0]+"</DirectoryPackagesPropsPath></PropertyGroup>"+ANCHOR_TESTS_PROPS);
					ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
					Assert.AreEqual("false",projectFile.GetProperty("ImportDirectoryPackagesProps"),arrayCase[0]);
					Assert.AreEqual("",projectFile.GetProperty("CentralPackageVersionsFileImported"),arrayCase[0]);
					Assert.AreEqual("",projectFile.GetProperty("ManagePackageVersionsCentrally"),arrayCase[0]+": the Directory.Packages.props above is not read either.");
					Assert.AreEqual("",projectFile.GetProperty("_DirectoryPackagesPropsBasePath"),arrayCase[0]+": not looked for.");
					fixtureFolder.EditFile(@"tests\Directory.Build.props","<ImportDirectoryPackagesProps>false</ImportDirectoryPackagesProps>","");
					AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),arrayCase[1]);
					fixtureFolder.WriteFile(@"tests\Directory.Build.props",textTestsProps);
				}
				//The switch itself must be known: MSBuild always tests it.
				fixtureFolder.EditFile(@"tests\Directory.Build.props",ANCHOR_TESTS_PROPS,"<PropertyGroup>"
					+"<ImportDirectoryPackagesProps>$(MSBuildExtensionsPath)</ImportDirectoryPackagesProps></PropertyGroup>"+ANCHOR_TESTS_PROPS);
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"$(ImportDirectoryPackagesProps)");
				//So must the parts of the path NuGet.props finds, when it looks for one.
				foreach(string name in new[] { "_DirectoryPackagesPropsFile","_DirectoryPackagesPropsBasePath" }) {
					fixtureFolder.EditFile(@"tests\Directory.Build.props","<ImportDirectoryPackagesProps>$(MSBuildExtensionsPath)</ImportDirectoryPackagesProps>",
						"<"+name+">$(MSBuildExtensionsPath)</"+name+">");
					AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"$("+name+")");
					fixtureFolder.EditFile(@"tests\Directory.Build.props","<"+name+">$(MSBuildExtensionsPath)</"+name+">",
						"<ImportDirectoryPackagesProps>$(MSBuildExtensionsPath)</ImportDirectoryPackagesProps>");
				}
			}
		}

		[TestMethod]
		public void ADirectoryPackagesPropsAboveTheScratchFolder_IsNotRead() {
			//The fixture's empty Directory.Packages.props at the scratch folder stops NuGet.props' search there, as its Directory.Build.targets stops
			//that search: one in a folder above (the temp folder, say) reaches no fixture project.
			string folderOuter=Path.Combine(Path.GetTempPath(),"ProjectFileTests_Outer_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folderOuter);
			try {
				string pathOuter=Path.Combine(folderOuter,"Directory.Packages.props");
				File.WriteAllText(pathOuter,"<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>"
					+"<FixtureOuterPackages>outer</FixtureOuterPackages></PropertyGroup><ItemGroup><PackageVersion Include=\"Fixture.Outer\" Version=\"9.9.9\" />"
					+"</ItemGroup></Project>");
				using(FixtureFolder fixtureFolder=FixtureFolder.Create(folderOuter)) {
					foreach(string name in new[] { "Classic","SdkStyle","SdkExplicitImports","SdkLinks" }) {
						ProjectFile projectFile=fixtureFolder.Load(name);
						Assert.IsFalse(projectFile.FilesRead.Contains(pathOuter,StringComparer.OrdinalIgnoreCase),name);
						Assert.AreEqual(fixtureFolder.Combine("Directory.Packages.props"),projectFile.GetProperty("DirectoryPackagesPropsPath"),name);
						Assert.AreEqual("",projectFile.GetProperty("FixtureOuterPackages"),name);
						Assert.AreEqual("",projectFile.GetProperty("ManagePackageVersionsCentrally"),name);
						Assert.AreEqual("",projectFile.GetProperty("_CentralPackageVersionsEnabled"),name);
						Assert.AreEqual(0,projectFile.GetItems("PackageVersion").Count,name);
					}
					//A fixture's own Directory.Packages.props still works: over the empty one, or in a project folder, found first.
					fixtureFolder.WriteFile("Directory.Packages.props",PACKAGES_PROPS);
					Assert.AreEqual(2,fixtureFolder.Load("SdkStyle").GetItems("PackageVersion").Count);
					fixtureFolder.WriteFile(@"tests\SdkStyle\Directory.Packages.props","<Project><ItemGroup><PackageVersion Include=\"Fixture.Near\" Version=\"1.0.0\" />"
						+"</ItemGroup></Project>");
					CollectionAssert.AreEqual(new[] { "Fixture.Near" },fixtureFolder.Load("SdkStyle").GetItems("PackageVersion").Select(x => x.Include).ToArray());
					//Without the empty one, the search would go on to the folder above: what the fixture's file keeps out.
					fixtureFolder.DeleteFile(@"tests\SdkStyle\Directory.Packages.props");
					fixtureFolder.DeleteFile("Directory.Packages.props");
					ProjectFile projectFileLeaked=fixtureFolder.Load("SdkStyle");
					CollectionAssert.Contains(projectFileLeaked.FilesRead,pathOuter);
					Assert.AreEqual("outer",projectFileLeaked.GetProperty("FixtureOuterPackages"));
				}
			}
			finally {
				try {
					Directory.Delete(folderOuter,true);
				}
				catch(IOException) {
				}
				catch(UnauthorizedAccessException) {
				}
			}
		}

		///<summary>NuGet.props' branch for a project that no Directory.Packages.props is found for, reached without a file above the scratch
		///folder: _DirectoryPackagesPropsFile is set (in tests\Directory.Build.props, before NuGet.props) to a name with a new GUID, which no folder
		///has, so the search walks up to the root of the drive, as MSBuild's does, and finds nothing. What NuGet.props (SDK 10.0.114, the same file
		///as Visual Studio's copy in the Build Tools 2026) does then:
		///- line 22: ImportDirectoryPackagesProps true, as it was not set;
		///- line 29: the group runs, as the switch is true and DirectoryPackagesPropsPath is '';
		///- line 30: _DirectoryPackagesPropsFile keeps the name it was given;
		///- line 31: _DirectoryPackagesPropsBasePath is $([MSBuild]::GetDirectoryNameOfFileAbove(...)), '' when no folder has the file;
		///- line 32: DirectoryPackagesPropsPath is set only when the base path and the file are not '', so it stays '';
		///- line 35: the Import's condition is false (the path is ''), so nothing is imported;
		///- lines 37-39: CentralPackageVersionsFileImported is set only under that condition, so it stays ''.
		///And NuGet.targets (line 65 of Visual Studio's copy, line 63 of the SDK's): _CentralPackageVersionsEnabled stays '' even with
		///ManagePackageVersionsCentrally true, so a PackageReference restores with its own Version.</summary>
		[TestMethod]
		public void NoDirectoryPackagesPropsFound_NothingIsImported_AsNuGetPropsHasIt() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				string fileName="NoFolderHasThis."+Guid.NewGuid().ToString("N")+".props";
				fixtureFolder.EditFile(@"tests\Directory.Build.props","<Import Project=","<PropertyGroup><_DirectoryPackagesPropsFile>"+fileName+"</_DirectoryPackagesPropsFile>"
					+"<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><Import Project=");
				//The fixture's own (empty) one and a full one in the project folder have the usual name, so neither is found.
				fixtureFolder.WriteFile(@"tests\SdkStyle\Directory.Packages.props",PACKAGES_PROPS);
				fixtureFolder.EditProject("SdkStyle",ANCHOR_SDK_ITEMS,ANCHOR_SDK_ITEMS+"<PackageReference Include=\"Fixture.Package\" Version=\"9.9.9\" />");
				foreach(string name in new[] { "SdkStyle","Classic" }) {
					ProjectFile projectFile=fixtureFolder.Load(name);
					Assert.AreEqual("true",projectFile.GetProperty("ImportDirectoryPackagesProps"),name+": line 22.");
					Assert.AreEqual(fileName,projectFile.GetProperty("_DirectoryPackagesPropsFile"),name+": line 30.");
					Assert.AreEqual("",projectFile.GetProperty("_DirectoryPackagesPropsBasePath"),name+": line 31, none found.");
					Assert.AreEqual("",projectFile.GetProperty("DirectoryPackagesPropsPath"),name+": line 32.");
					Assert.AreEqual("",projectFile.GetProperty("CentralPackageVersionsFileImported"),name+": lines 37-39.");
					Assert.AreEqual("true",projectFile.GetProperty("ManagePackageVersionsCentrally"),name);
					Assert.AreEqual("",projectFile.GetProperty("_CentralPackageVersionsEnabled"),name+": NuGet.targets, the file was not imported.");
					Assert.AreEqual(0,projectFile.GetItems("PackageVersion").Count,name);
					//Line 35: no Directory.Packages.props (nor the file of the GUID's name) is read, and nothing outside the scratch folder.
					Assert.IsFalse(projectFile.FilesRead.Any(x => Path.GetFileName(x).Equals("Directory.Packages.props",StringComparison.OrdinalIgnoreCase)
						|| Path.GetFileName(x).Equals(fileName,StringComparison.OrdinalIgnoreCase)),name);
					Assert.IsTrue(projectFile.FilesRead.All(x => x.StartsWith(fixtureFolder.Root+@"\",StringComparison.OrdinalIgnoreCase)),name+": "
						+string.Join(", ",projectFile.FilesRead));
				}
				Assert.AreEqual("9.9.9",fixtureFolder.Load("SdkStyle").GetPackageVersion("Fixture.Package"),"Central package management is off: its own Version.");
				//The same name in the project folder is found there, so the branch above was the 'none found' one.
				fixtureFolder.WriteFile(@"tests\SdkStyle\"+fileName,PACKAGES_PROPS);
				ProjectFile projectFileFound=fixtureFolder.Load("SdkStyle");
				Assert.AreEqual(fixtureFolder.Combine(@"tests\SdkStyle"),projectFileFound.GetProperty("_DirectoryPackagesPropsBasePath"));
				Assert.AreEqual("true",projectFileFound.GetProperty("_CentralPackageVersionsEnabled"));
			}
		}

		[TestMethod]
		public void ThePackageReferencesTheSdkGivesItself_ThrowFromEveryItemQuery() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//Microsoft.NET.Sdk.DefaultItems.props and Microsoft.NET.Sdk.DefaultItems.Shared.targets (SDK 10.0.114) add PackageReference items or
				//metadata this reader does not: each query that can return PackageReference items throws, so none of them is left out silently.
				foreach(string[] arrayCase in new[] {
					//The implicit NETStandard.Library and Microsoft.NETCore.App.
					new[] { "<TargetFramework>netstandard2.0</TargetFramework>","","implicit PackageReference of netstandard2.0" },
					new[] { "<TargetFramework>netstandard1.6</TargetFramework>","","implicit PackageReference of netstandard1.6" },
					new[] { "<TargetFramework>netcoreapp2.1</TargetFramework>","","implicit PackageReference of netcoreapp2.1" },
					//AllowExplicitVersion on every PackageReference.
					new[] { "<DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>","","DisableImplicitFrameworkReferences" },
					//PrivateAssets all and Publish true for ASP.NET Core without a Version (always so with central package management).
					new[] { "","<PackageReference Include=\"Microsoft.AspNetCore.App\" />","Microsoft.AspNetCore.App without a Version" },
					new[] { "","<PackageReference Include=\"microsoft.aspnetcore.all\" />","microsoft.aspnetcore.all without a Version" },
					//Version and AllowExplicitVersion for Microsoft.NETCore.App with RuntimeFrameworkVersion.
					new[] { "<RuntimeFrameworkVersion>2.1.30</RuntimeFrameworkVersion>","<PackageReference Include=\"Microsoft.NETCore.App\" />","RuntimeFrameworkVersion set" } })
				{
					ProjectFile projectFile=LoadSdkPackages(fixtureFolder,arrayCase[0],arrayCase[1]);
					string where=arrayCase[0]+arrayCase[1];
					AssertThrows<NotSupportedException>(() => projectFile.GetItems("PackageReference"),arrayCase[2]);
					AssertThrows<NotSupportedException>(() => projectFile.GetItems("packagereference"),arrayCase[2]);
					AssertThrows<NotSupportedException>(() => projectFile.GetItem("PackageReference","Fixture.Package"),arrayCase[2]);
					AssertThrows<NotSupportedException>(() => projectFile.HasItem("PackageReference","Fixture.Package"),arrayCase[2]);
					AssertThrows<NotSupportedException>(() => projectFile.GetPackageReference("Fixture.Package"),arrayCase[2]);
					AssertThrows<NotSupportedException>(() => projectFile.GetPackageVersion("Fixture.Package"),arrayCase[2]);
					AssertThrows<NotSupportedException>(() => projectFile.GetAllItems(),arrayCase[2]);
					AssertThrows<NotSupportedException>(() => projectFile.GetItemsMentioning("Fixture"),arrayCase[2]);
					AssertThrows<NotSupportedException>(() => projectFile.GetItemsMentioning("Fixture","Compile","PackageReference"),arrayCase[2]);
					//The other item types still answer.
					Assert.IsTrue(projectFile.Compiles("Program.cs"),where);
					Assert.AreEqual(1,projectFile.GetItems("Compile").Count,where);
					Assert.AreEqual(0,projectFile.GetItemsMentioning("Fixture","Compile").Count,where);
				}
				//Not refused: the frameworks whose SDK adds no PackageReference, ASP.NET Core with a Version of its own (no central package
				//management), Microsoft.NETCore.App without RuntimeFrameworkVersion, and a classic project, which does not import the SDK's files.
				foreach(string[] arrayCase in new[] { new[] { "","" },new[] { "<TargetFramework>netstandard2.1</TargetFramework>","" },
					new[] { "<TargetFramework>net10.0</TargetFramework>","" },
					new[] { "","<PackageReference Include=\"Microsoft.AspNetCore.App\" Version=\"2.1.1\" />" },
					new[] { "","<PackageReference Include=\"Microsoft.NETCore.App\" />" } })
				{
					ProjectFile projectFile=LoadSdkPackages(fixtureFolder,arrayCase[0],arrayCase[1]);
					Assert.AreEqual("1.2.3",projectFile.GetPackageVersion("Fixture.Package"),arrayCase[0]+arrayCase[1]);
					Assert.AreEqual(arrayCase[1]=="" ? 1 : 2,projectFile.GetItems("PackageReference").Count,arrayCase[0]+arrayCase[1]);
					Assert.AreEqual(projectFile.GetItems("PackageReference").Count+projectFile.GetItems("Compile").Count,
						projectFile.GetAllItems().Count(x => x.ItemType=="PackageReference" || x.ItemType=="Compile"),arrayCase[0]+arrayCase[1]);
				}
				fixtureFolder.EditProject("Classic",ANCHOR_CLASSIC,"<ItemGroup><PackageReference Include=\"Microsoft.AspNetCore.App\" /></ItemGroup>"+ANCHOR_CLASSIC);
				ProjectFile projectFileClassic=fixtureFolder.Load("Classic");
				Assert.AreEqual("",projectFileClassic.GetPackageReference("Microsoft.AspNetCore.App").GetMetadata("PrivateAssets"));
				Assert.AreEqual(1,projectFileClassic.GetItemsMentioning("AspNetCore").Count);
			}
		}

		///<summary>An SDK-style project of its own (tests\SdkPackages\SdkPackages.csproj, which the fixture's solution does not list) with the
		///properties and items given after a PackageReference of Fixture.Package 1.2.3; the fixture's Directory.Packages.props leaves central package
		///management off.</summary>
		private static ProjectFile LoadSdkPackages(FixtureFolder fixtureFolder,string properties,string items) {
			fixtureFolder.WriteFile(@"tests\SdkPackages\Program.cs","");
			fixtureFolder.WriteFile(@"tests\SdkPackages\SdkPackages.csproj","<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"+properties+"</PropertyGroup>"
				+"<ItemGroup><PackageReference Include=\"Fixture.Package\" Version=\"1.2.3\" />"+items+"</ItemGroup></Project>");
			return ProjectFile.Load(fixtureFolder.Combine(@"tests\SdkPackages\SdkPackages.csproj"));
		}

		[TestMethod]
		public void NuGetsOwnFiles_AreNotModelled() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//Where NuGet.props and NuGet.targets are (Visual Studio's folder for the Build Tools MSBuild): MSBuild 18 gives every project a value;
				//ImportNuGetProps and _CentralPackageVersionsEnabled read what the files do, not where they are, so the names throw.
				foreach(string name in new[] { "Classic","SdkStyle" }) {
					ProjectFile projectFile=fixtureFolder.Load(name);
					foreach(string property in new[] { "NuGetPropsFile","MSBuildUseVisualStudioDirectoryLayout","NuGetRestoreTargets" }) {
						AssertThrows<NotSupportedException>(() => projectFile.GetProperty(property),"$("+property+")");
						Assert.IsFalse(projectFile.IsPropertyKnown(property),name+": "+property);
					}
				}
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

		///<summary>Runs the action with the environment variable of this test process set to the value, and gives the variable its old value (none,
		///normally) afterwards, whatever the action does. ProjectFileTests' tests run one after another in one test host (MSTest v1 runs the tests
		///of an assembly in turn; run-tests.ps1 starts each assembly in a vstest process of its own), so no other test sees the variable.</summary>
		private static void WithEnvironmentVariable(string name,string value,Action action) {
			string valueOld=Environment.GetEnvironmentVariable(name);
			Environment.SetEnvironmentVariable(name,value);
			try {
				action();
			}
			finally {
				Environment.SetEnvironmentVariable(name,valueOld);
			}
		}

		///<summary>The message of the NotSupportedException for a name MSBuild would read from an environment variable (GetKnownValue).</summary>
		private static string EnvironmentVariableMessage(string name) {
			return "$("+name+") is an environment variable";
		}

		[TestMethod]
		public void NuGetPropsNames_AsEnvironmentVariables_ThrowWhereNuGetPropsReadsThem() {
			//MSBuild starts every evaluation with the environment variables as properties, so NuGet.props reads a variable of one of its names when no
			//file set it. ProjectFile does not model them: each throws where NuGet.props reads it, and only there; a file that sets the name decides it.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.WriteFile("Directory.Packages.props",PACKAGES_PROPS);
				fixtureFolder.WriteFile(@"Shared\Other.Packages.props","<Project><PropertyGroup><FixtureOtherPackages>other</FixtureOtherPackages></PropertyGroup></Project>");
				string textTestsProps=File.ReadAllText(fixtureFolder.Combine(@"tests\Directory.Build.props"));
				Action<string> writeTestsProps=properties => fixtureFolder.WriteFile(@"tests\Directory.Build.props",properties=="" ? textTestsProps
					: textTestsProps.Replace("<Import Project=","<PropertyGroup>"+properties+"</PropertyGroup><Import Project="));
				//DirectoryPackagesPropsPath: MSBuild imports the variable's file, so the reader may not look for one instead (it found the scratch
				//folder's before, silently).
				WithEnvironmentVariable("DirectoryPackagesPropsPath",fixtureFolder.Combine(@"Shared\Other.Packages.props"),() => {
					foreach(string name in new[] { "SdkStyle","Classic" }) {
						AssertThrows<NotSupportedException>(() => fixtureFolder.Load(name),EnvironmentVariableMessage("DirectoryPackagesPropsPath"));
					}
					//With the switch off, NuGet.props never reads it.
					writeTestsProps("<ImportDirectoryPackagesProps>false</ImportDirectoryPackagesProps>");
					ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
					Assert.AreEqual("",projectFile.GetProperty("ManagePackageVersionsCentrally"));
					AssertThrows<NotSupportedException>(() => projectFile.GetProperty("DirectoryPackagesPropsPath"),EnvironmentVariableMessage("DirectoryPackagesPropsPath"));
					//A file that sets it decides it.
					writeTestsProps("<DirectoryPackagesPropsPath>$(MSBuildThisFileDirectory)..\\Shared\\Other.Packages.props</DirectoryPackagesPropsPath>");
					Assert.AreEqual("other",fixtureFolder.Load("SdkStyle").GetProperty("FixtureOtherPackages"));
					writeTestsProps("");
				});
				//The switch and the two parts of the path NuGet.props finds (these threw before this round too, through SetIfEmpty).
				foreach(string name in new[] { "ImportDirectoryPackagesProps","_DirectoryPackagesPropsFile","_DirectoryPackagesPropsBasePath" }) {
					WithEnvironmentVariable(name,"false",() => {
						AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"$("+name+")");
						//A file that sets it decides it: false for the switch; the scratch folder's own name and folder for the parts.
						string value=name=="ImportDirectoryPackagesProps" ? "false" : name=="_DirectoryPackagesPropsFile" ? "Directory.Packages.props" : fixtureFolder.Root;
						writeTestsProps("<"+name+">"+value+"</"+name+">");
						Assert.AreEqual(value,fixtureFolder.Load("SdkStyle").GetProperty(name),name);
						writeTestsProps("");
					});
				}
				//The two parts are not read when NuGet.props does not look for the file: the switch off, or a path a file names.
				foreach(string name in new[] { "_DirectoryPackagesPropsFile","_DirectoryPackagesPropsBasePath" }) {
					WithEnvironmentVariable(name,"false",() => {
						writeTestsProps("<ImportDirectoryPackagesProps>false</ImportDirectoryPackagesProps>");
						Assert.AreEqual("",fixtureFolder.Load("SdkStyle").GetProperty("CentralPackageVersionsFileImported"),name);
						writeTestsProps("<DirectoryPackagesPropsPath>$(MSBuildThisFileDirectory)..\\Shared\\Other.Packages.props</DirectoryPackagesPropsPath>");
						Assert.AreEqual("other",fixtureFolder.Load("SdkStyle").GetProperty("FixtureOtherPackages"),name);
						writeTestsProps("");
					});
				}
			}
		}

		[TestMethod]
		public void CentralPackageSwitches_AsEnvironmentVariables_ThrowWhereNuGetReadsThem() {
			//ManagePackageVersionsCentrally, CentralPackageVersionsFileImported and _CentralPackageVersionsEnabled (NuGet.targets' switch),
			//CentralPackageVersionOverrideEnabled and RestoreEnableGlobalPackageReference: each read as MSBuild reads it, so a variable no file set
			//throws where NuGet reads it instead of reading as '' (before this round GetPackageVersion gave the PackageReference's own Version for
			//each of the first three, and the VersionOverride for the fourth, where the restore fails with NU1008 or NU1013).
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				const string REFERENCE_VERSION="<PackageReference Include=\"Fixture.Package\" Version=\"9.9.9\" />";
				string textTestsProps=File.ReadAllText(fixtureFolder.Combine(@"tests\Directory.Build.props"));
				Action<string> writeTestsProps=properties => fixtureFolder.WriteFile(@"tests\Directory.Build.props",properties=="" ? textTestsProps
					: textTestsProps.Replace("<Import Project=","<PropertyGroup>"+properties+"</PropertyGroup><Import Project="));
				fixtureFolder.EditProject("SdkStyle",ANCHOR_SDK_ITEMS,ANCHOR_SDK_ITEMS+REFERENCE_VERSION);
				//ManagePackageVersionsCentrally (the fixture's empty Directory.Packages.props is imported, so the file is): MSBuild turns central
				//package management on, the restore fails with NU1008.
				Assert.AreEqual("9.9.9",fixtureFolder.Load("SdkStyle").GetPackageVersion("Fixture.Package"),"Without the variable: off.");
				WithEnvironmentVariable("ManagePackageVersionsCentrally","true",() => {
					ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
					AssertThrows<NotSupportedException>(() => projectFile.GetPackageVersion("Fixture.Package"),"from ManagePackageVersionsCentrally");
					AssertThrows<NotSupportedException>(() => projectFile.GetProperty("_CentralPackageVersionsEnabled"),"from ManagePackageVersionsCentrally");
					//NuGet.targets turns a GlobalPackageReference into a PackageReference with it on.
					fixtureFolder.EditProject("SdkStyle",REFERENCE_VERSION,REFERENCE_VERSION+"<GlobalPackageReference Include=\"Fixture.Global\" Version=\"1.0.0\" />");
					AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),EnvironmentVariableMessage("ManagePackageVersionsCentrally"));
					//A file that sets it decides it.
					writeTestsProps("<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>");
					projectFile=fixtureFolder.Load("SdkStyle");
					Assert.AreEqual("9.9.9",projectFile.GetPackageVersion("Fixture.Package"));
					Assert.AreEqual(1,projectFile.GetItems("GlobalPackageReference").Count);
					writeTestsProps("");
					fixtureFolder.EditProject("SdkStyle","<GlobalPackageReference Include=\"Fixture.Global\" Version=\"1.0.0\" />","");
				});
				//CentralPackageVersionsFileImported, with ManagePackageVersionsCentrally true and the import switched off: NuGet.props does not set it,
				//so MSBuild reads the variable and central package management is on.
				writeTestsProps("<ImportDirectoryPackagesProps>false</ImportDirectoryPackagesProps><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>");
				Assert.AreEqual("9.9.9",fixtureFolder.Load("SdkStyle").GetPackageVersion("Fixture.Package"),"Without the variable: not imported, off.");
				WithEnvironmentVariable("CentralPackageVersionsFileImported","true",() => {
					AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle").GetPackageVersion("Fixture.Package"),"from CentralPackageVersionsFileImported");
					//MSBuild does not read it when ManagePackageVersionsCentrally is not true.
					writeTestsProps("<ImportDirectoryPackagesProps>false</ImportDirectoryPackagesProps>");
					ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
					Assert.AreEqual("",projectFile.GetProperty("_CentralPackageVersionsEnabled"));
					Assert.AreEqual("9.9.9",projectFile.GetPackageVersion("Fixture.Package"));
					//NuGet.props sets it when it imports the file, over the variable: central package management on, the Version refused (NU1008).
					writeTestsProps("<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>");
					projectFile=fixtureFolder.Load("SdkStyle");
					Assert.AreEqual("true",projectFile.GetProperty("_CentralPackageVersionsEnabled"));
					AssertThrows<InvalidOperationException>(() => projectFile.GetPackageVersion("Fixture.Package"),"NU1008");
				});
				writeTestsProps("");
				//_CentralPackageVersionsEnabled itself: NuGet.targets only ever sets it true, so with central package management off MSBuild keeps
				//the variable's value.
				WithEnvironmentVariable("_CentralPackageVersionsEnabled","true",() => {
					ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
					AssertThrows<NotSupportedException>(() => projectFile.GetPackageVersion("Fixture.Package"),EnvironmentVariableMessage("_CentralPackageVersionsEnabled"));
					fixtureFolder.WriteFile("Directory.Packages.props",PACKAGES_PROPS);
					AssertThrows<InvalidOperationException>(() => fixtureFolder.Load("SdkStyle").GetPackageVersion("Fixture.Package"),"NU1008");
				});
				//CentralPackageVersionOverrideEnabled, read for a VersionOverride only: false fails the restore with NU1013.
				fixtureFolder.EditProject("SdkStyle",REFERENCE_VERSION,"<PackageReference Include=\"Fixture.Package\" />"
					+"<PackageReference Include=\"Fixture.Other\" VersionOverride=\"4.5.7\" />");
				Assert.AreEqual("4.5.7",fixtureFolder.Load("SdkStyle").GetPackageVersion("Fixture.Other"),"Without the variable: the override.");
				WithEnvironmentVariable("CentralPackageVersionOverrideEnabled","false",() => {
					ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
					AssertThrows<NotSupportedException>(() => projectFile.GetPackageVersion("Fixture.Other"),EnvironmentVariableMessage("CentralPackageVersionOverrideEnabled"));
					Assert.AreEqual("1.2.3",projectFile.GetPackageVersion("Fixture.Package"),"Not read without a VersionOverride.");
					writeTestsProps("<CentralPackageVersionOverrideEnabled>true</CentralPackageVersionOverrideEnabled>");
					Assert.AreEqual("4.5.7",fixtureFolder.Load("SdkStyle").GetPackageVersion("Fixture.Other"));
					writeTestsProps("");
				});
				//RestoreEnableGlobalPackageReference, read for a GlobalPackageReference with central package management on: false leaves the items as
				//they are (the reader refused them for the wrong reason before).
				fixtureFolder.EditProject("SdkStyle",ANCHOR_SDK_ITEMS,ANCHOR_SDK_ITEMS+"<GlobalPackageReference Include=\"Fixture.Global\" Version=\"1.0.0\" />");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"GlobalPackageReference items are not modelled");
				WithEnvironmentVariable("RestoreEnableGlobalPackageReference","false",() => {
					AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),EnvironmentVariableMessage("RestoreEnableGlobalPackageReference"));
					writeTestsProps("<RestoreEnableGlobalPackageReference>false</RestoreEnableGlobalPackageReference>");
					Assert.AreEqual(1,fixtureFolder.Load("SdkStyle").GetItems("GlobalPackageReference").Count);
					writeTestsProps("");
					//Not read with central package management off.
					fixtureFolder.EditFile("Directory.Packages.props","<ManagePackageVersionsCentrally>true","<ManagePackageVersionsCentrally>false");
					Assert.AreEqual(1,fixtureFolder.Load("SdkStyle").GetItems("GlobalPackageReference").Count);
				});
			}
		}

		[TestMethod]
		public void TheSdksPackageReferenceNames_AsEnvironmentVariablesOrNotKnown_Throw() {
			//ThrowIfImplicitPackageReferences reads DisableImplicitFrameworkReferences, the target framework and RuntimeFrameworkVersion as
			//Microsoft.NET.Sdk.DefaultItems.props and .Shared.targets read them: one not known here (an environment variable no file set, a framework
			//the reader does not know) throws from the PackageReference queries, where the reader gave the items without the SDK's before.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				WithEnvironmentVariable("DisableImplicitFrameworkReferences","true",() => {
					ProjectFile projectFile=LoadSdkPackages(fixtureFolder,"","");
					AssertThrows<NotSupportedException>(() => projectFile.GetItems("PackageReference"),EnvironmentVariableMessage("DisableImplicitFrameworkReferences"));
					AssertThrows<NotSupportedException>(() => projectFile.GetPackageVersion("Fixture.Package"),EnvironmentVariableMessage("DisableImplicitFrameworkReferences"));
					Assert.AreEqual(1,projectFile.GetItems("Compile").Count,"The other item types still answer.");
					Assert.AreEqual("1.2.3",LoadSdkPackages(fixtureFolder,"<DisableImplicitFrameworkReferences>false</DisableImplicitFrameworkReferences>","")
						.GetPackageVersion("Fixture.Package"),"A file that sets it decides it.");
				});
				WithEnvironmentVariable("RuntimeFrameworkVersion","2.1.30",() => {
					ProjectFile projectFile=LoadSdkPackages(fixtureFolder,"","<PackageReference Include=\"Microsoft.NETCore.App\" />");
					AssertThrows<NotSupportedException>(() => projectFile.GetItems("PackageReference"),EnvironmentVariableMessage("RuntimeFrameworkVersion"));
					Assert.AreEqual("1.2.3",LoadSdkPackages(fixtureFolder,"","").GetPackageVersion("Fixture.Package"),"Not read without Microsoft.NETCore.App.");
					Assert.AreEqual(2,LoadSdkPackages(fixtureFolder,"<RuntimeFrameworkVersion></RuntimeFrameworkVersion>","<PackageReference Include=\"Microsoft.NETCore.App\" />")
						.GetItems("PackageReference").Count,"A file that sets it decides it.");
				});
				//A framework the reader does not know: whether the SDK adds its implicit PackageReference is not known either.
				ProjectFile projectFileUnknown=LoadSdkPackages(fixtureFolder,"<TargetFramework>uap10.0</TargetFramework>","");
				AssertThrows<NotSupportedException>(() => projectFileUnknown.GetItems("PackageReference"),"uap10.0");
				AssertThrows<NotSupportedException>(() => projectFileUnknown.GetAllItems(),"uap10.0");
				Assert.AreEqual(1,projectFileUnknown.GetItems("Compile").Count);
			}
		}

		[TestMethod]
		public void TheRestoresImportNames_AsEnvironmentVariables_Throw() {
			//The switches of the restore's project extension files and the package folder of its package imports: MSBuild reads a variable of the
			//name when no file set it. The switches read as '' before this round, so the restore's files were left out without a word.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.WriteFile(@"tests\Classic\obj\Classic.csproj.a.props","<Project><PropertyGroup><FixtureExtProps>a</FixtureExtProps></PropertyGroup></Project>");
				fixtureFolder.WriteFile(@"tests\Classic\obj\Classic.csproj.x.targets","<Project><PropertyGroup><FixtureExtTargets>x</FixtureExtTargets></PropertyGroup></Project>");
				foreach(string name in new[] { "ImportProjectExtensionProps","ImportProjectExtensionTargets" }) {
					WithEnvironmentVariable(name,"true",() => {
						AssertThrows<NotSupportedException>(() => fixtureFolder.Load("Classic"),"$("+name+")");
						//A file that sets it decides it.
						fixtureFolder.EditFile(@"tests\Directory.Build.props","<Import Project=","<PropertyGroup><"+name+">true</"+name+"></PropertyGroup><Import Project=");
						ProjectFile projectFile=fixtureFolder.Load("Classic");
						Assert.AreEqual("a",projectFile.GetProperty("FixtureExtProps"),name);
						Assert.AreEqual("x",projectFile.GetProperty("FixtureExtTargets"),name);
						fixtureFolder.EditFile(@"tests\Directory.Build.props","<PropertyGroup><"+name+">true</"+name+"></PropertyGroup>","");
					});
				}
				//NuGetPackageFolders, when the restore's props do not set it: MSBuild takes the variable's folder.
				const string IMPORT="$(NuGetPackageRoot)fixture.package\\1.2.3\\buildTransitive\\net462\\Fixture.Package.targets";
				fixtureFolder.WriteFile(@"tests\SdkStyle\obj\SdkStyle.csproj.nuget.g.targets","<Project><ImportGroup Condition=\" '$(ExcludeRestorePackageImports)' != 'true' \">"
					+"<Import Project=\""+IMPORT+"\" Condition=\"Exists('"+IMPORT+"')\" /></ImportGroup></Project>");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"names no package folder");
				WithEnvironmentVariable("NuGetPackageFolders",fixtureFolder.Combine("Packages")+@"\",() => {
					AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),EnvironmentVariableMessage("NuGetPackageFolders"));
				});
			}
		}
		#endregion Environment variables
	}
}
