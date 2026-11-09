// ProjectFile on a classic fixture project and on its SDK-style form: the same questions get the same answers, the SDK's default globs and their
// excludes, removes and updates are applied as MSBuild applies them, and imports, conditions and metadata in either form are read.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	[TestClass]
	public class ProjectFormatTests {
		private static string[] Includes(ProjectFile projectFile,string itemType) {
			return projectFile.GetItems(itemType).Select(x => x.Include).ToArray();
		}

		#region Classic
		[TestMethod]
		public void Classic_IsClassic_AndImportsTheDirectoryBuildProps() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.IsFalse(projectFile.IsSdkStyle);
				CollectionAssert.AreEqual(new[] { fixtureFolder.ProjectPath("Classic"),fixtureFolder.Combine(@"tests\Directory.Build.props"),
					fixtureFolder.Combine("Directory.Build.props"),fixtureFolder.Combine("Directory.Packages.props"),fixtureFolder.Combine("Directory.Build.targets") },
					projectFile.FilesRead,"The props through Microsoft.Common.props, then the one it imports, then NuGet.props' Directory.Packages.props at the end "
					+"of Microsoft.Common.props, then the targets through Microsoft.CSharp.targets.");
				Assert.AreEqual("",projectFile.GetProperty("AppConfig"),"Directory.Build.targets: the name does not end with Style.");
				Assert.AreEqual("root",projectFile.GetProperty("FixtureEveryProject"));
				Assert.AreEqual("",projectFile.GetProperty("FixtureSdkOnly"),"The SDK-only group is not for a classic project.");
				Assert.AreEqual("",projectFile.GetProperty("TargetFramework"));
				Assert.AreEqual("v4.8",projectFile.GetProperty("TargetFrameworkVersion"));
				Assert.AreEqual("true",projectFile.GetProperty("AutoGenerateBindingRedirects"));
				Assert.AreEqual("ClassicFixture",projectFile.GetProperty("AssemblyName"));
				//Its own MSTest reference only: the one of tests\Directory.Build.props is for SDK-style projects.
				Assert.AreEqual(1,projectFile.GetItems("Reference").Count(x => x.Include.StartsWith("Microsoft.VisualStudio.QualityTools.UnitTestFramework")));
				Assert.AreEqual(projectFile.FullPath,projectFile.GetReference("Microsoft.VisualStudio.QualityTools.UnitTestFramework").DefiningFile);
			}
		}

		[TestMethod]
		public void Classic_WithoutTheCommonPropsImport_GetsItFromTheCommonTargets() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//As OpenPmsDental.csproj and six other classic projects of the solution filter: no Microsoft.Common.props import of its own.
				//Microsoft.Common.CurrentVersion.targets imports it then (unless MicrosoftCommonPropsHasBeenImported is true), so MSBuild 18 reads
				//Directory.Build.props at the Microsoft.CSharp.targets import, after the project's own properties, and gives MSBuildProjectExtensionsPath
				//the project's obj\ folder.
				string textClassic=File.ReadAllText(fixtureFolder.ProjectPath("Classic"));
				const string importCommonProps="<Import Project=\"$(MSBuildExtensionsPath)\\$(MSBuildToolsVersion)\\Microsoft.Common.props\" "
					+"Condition=\"Exists('$(MSBuildExtensionsPath)\\$(MSBuildToolsVersion)\\Microsoft.Common.props')\" />";
				const string anchor="<AssemblyName>ClassicFixture</AssemblyName>";
				Assert.IsTrue(textClassic.Contains(importCommonProps),"The Classic fixture imports Microsoft.Common.props.");
				string textOwnValue=textClassic.Replace(anchor,anchor+"<FixtureEveryProject>project</FixtureEveryProject>");
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textOwnValue.Replace(importCommonProps,""));
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual(fixtureFolder.Combine(@"tests\Classic\obj\"),projectFile.GetProperty("MSBuildProjectExtensionsPath"));
				Assert.AreEqual("root",projectFile.GetProperty("FixtureEveryProject"),"Directory.Build.props, read after the project's own value.");
				Assert.AreEqual("true",projectFile.GetProperty("MicrosoftCommonPropsHasBeenImported"));
				Assert.AreEqual(@"obj\x86\Debug\",projectFile.GetProperty("IntermediateOutputPath"));
				CollectionAssert.AreEqual(new[] { fixtureFolder.ProjectPath("Classic"),fixtureFolder.Combine(@"tests\Directory.Build.props"),
					fixtureFolder.Combine("Directory.Build.props"),fixtureFolder.Combine("Directory.Packages.props"),fixtureFolder.Combine("Directory.Build.targets") },
					projectFile.FilesRead);
				//With its own import at the top, the project's own value comes after Directory.Build.props; the props are read once.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textOwnValue);
				projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("project",projectFile.GetProperty("FixtureEveryProject"));
				Assert.AreEqual(fixtureFolder.Combine(@"tests\Classic\obj\"),projectFile.GetProperty("MSBuildProjectExtensionsPath"));
				Assert.AreEqual(5,projectFile.FilesRead.Count);
				//A project that says it has imported them, without the import: MSBuild reads no Directory.Build.props, and nothing sets the path.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textOwnValue.Replace(importCommonProps,"")
					.Replace(anchor,anchor+"<MicrosoftCommonPropsHasBeenImported>true</MicrosoftCommonPropsHasBeenImported>"));
				projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("project",projectFile.GetProperty("FixtureEveryProject"));
				Assert.AreEqual("",projectFile.GetProperty("MSBuildProjectExtensionsPath"));
				CollectionAssert.AreEqual(new[] { fixtureFolder.ProjectPath("Classic"),fixtureFolder.Combine("Directory.Build.targets") },projectFile.FilesRead);
			}
		}

		[TestMethod]
		public void Classic_CompilesTheListedFilesOnly() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				CollectionAssert.AreEquivalent(new[] { @"..\..\Shared\SharedAssemblyInfo.cs",@"Forms\FormMain.cs",@"Forms\FormMain.Designer.cs",
					@"Frms\FrmPrint.xaml.cs",@"Old\Keep.cs","Program.cs",@"VendorDirect\Lab\LabClient.cs",@"VendorDirect\Lab\Sub\LabHelper.cs" },
					Includes(projectFile,"Compile"));
				Assert.IsFalse(projectFile.Compiles("Unlisted.cs"),"A classic project compiles only what it lists.");
				Assert.IsFalse(projectFile.Compiles(@"Old\Skip.cs"),"Exclude.");
				Assert.IsFalse(projectFile.Compiles(@"bin\Debug\Stale.cs"));
				Assert.IsTrue(projectFile.Compiles(@"Forms/FormMain.cs"),"Either slash.");
				Assert.IsTrue(projectFile.Compiles(fixtureFolder.Combine(@"tests\Classic\Program.cs")),"A full path.");
				Assert.IsTrue(projectFile.GetItem("Compile",@"VendorDirect\Lab\Sub\LabHelper.cs").IsFromWildcard);
				Assert.IsFalse(projectFile.GetItem("Compile","Program.cs").IsFromWildcard);
				Assert.AreEqual(projectFile.FullPath,projectFile.GetItem("Compile","Program.cs").DefiningFile);
				CollectionAssert.AreEqual(new string[0],projectFile.GetFilesNotCompiled("VendorDirect"));
				CollectionAssert.AreEqual(new[] { "Unlisted.cs" },projectFile.GetFilesNotCompiled(".").Where(x => !x.StartsWith(@"Old\")).ToArray());
			}
		}

		[TestMethod]
		public void Classic_ReadsResourcesPagesReferencesAndMetadata() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("Form",projectFile.GetItem("Compile",@"Forms\FormMain.cs").GetMetadata("SubType"));
				Assert.AreEqual(@"Properties\SharedAssemblyInfo.cs",projectFile.GetItem("Compile",@"..\..\Shared\SharedAssemblyInfo.cs").GetMetadata("Link"));
				Assert.AreEqual(fixtureFolder.Combine(@"Shared\SharedAssemblyInfo.cs"),projectFile.GetItem("Compile",@"..\..\Shared\SharedAssemblyInfo.cs").FullPath);
				CollectionAssert.AreEquivalent(new[] { @"Branding\logo.png",@"Forms\FormMain.resx" },Includes(projectFile,"EmbeddedResource"));
				Assert.AreEqual("FormMain.cs",projectFile.GetItem("EmbeddedResource",@"Forms\FormMain.resx").GetMetadata("DependentUpon"));
				Assert.AreEqual("Fixture.Branding.logo.png",projectFile.GetItem("EmbeddedResource",@"Branding\logo.png").GetMetadata("LogicalName"));
				CollectionAssert.AreEquivalent(new[] { @"Frms\FrmPrint.xaml" },Includes(projectFile,"Page"));
				CollectionAssert.AreEquivalent(new[] { @"Resources\Icon.png" },Includes(projectFile,"Resource"));
				CollectionAssert.AreEquivalent(new[] { @"Data\Seed.txt" },Includes(projectFile,"None"),"No default None items in a classic project.");
				Assert.AreEqual("PreserveNewest",projectFile.GetItem("None",@"Data\Seed.txt").GetMetadata("CopyToOutputDirectory"));
				Assert.IsNotNull(projectFile.GetReference("Microsoft.VisualStudio.QualityTools.UnitTestFramework"),"By its simple name, the version left out.");
				Assert.IsNull(projectFile.GetReference("Microsoft.VisualStudio"),"Not by a prefix.");
				Assert.AreEqual(@"..\..\Required dlls\MySqlConnector.dll",projectFile.GetReference("MySqlConnector").GetMetadata("HintPath"));
				Assert.AreEqual("False",projectFile.GetReference("Windows").GetMetadata("Private"));
				Assert.IsNotNull(projectFile.GetReference("System"));
				Assert.AreEqual("{0A1B2C3D-4E5F-4A6B-8C7D-9E0F1A2B3C4D}",projectFile.GetProjectReference(@"..\SdkStyle\SdkStyle.csproj").GetMetadata("Project"));
				Assert.AreEqual("",projectFile.GetProjectReference(@"..\SdkStyle\SdkStyle.csproj").GetMetadata("Private"),"Metadata it does not have.");
				Assert.AreEqual(0,projectFile.GetItems("ApplicationDefinition").Count);
			}
		}

		[TestMethod]
		public void Classic_TheConfigurationDecidesTheConditionalProperties() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				Assert.AreEqual(@"bin\Debug\",fixtureFolder.Load("Classic").GetProperty("OutputPath"));
				Assert.AreEqual(@"bin\Release\",ProjectFile.Load(fixtureFolder.ProjectPath("Classic"),"Release","x86").GetProperty("OutputPath"));
				ProjectFile projectFileX64=ProjectFile.Load(fixtureFolder.ProjectPath("Classic"),"Debug","x64");
				Assert.AreEqual("",projectFileX64.GetProperty("DefineConstants"),"No Debug|x64 group.");
				//Microsoft.Common.CurrentVersion.targets' default for a configuration without an OutputPath, as MSBuild evaluates it (a build then
				//stops in _CheckForInvalidConfigurationAndPlatform).
				Assert.AreEqual(@"bin\x64\Debug\",projectFileX64.GetProperty("OutputPath"));
				Assert.AreEqual("Debug",fixtureFolder.Load("Classic").GetProperty("Configuration"));
			}
		}
		#endregion Classic

		#region SDK-style
		[TestMethod]
		public void Sdk_IsSdkStyle_AndGetsTheSharedProps() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				Assert.IsTrue(projectFile.IsSdkStyle);
				Assert.AreEqual("sdk",projectFile.GetProperty("FixtureSdkOnly"));
				Assert.AreEqual("root",projectFile.GetProperty("FixtureEveryProject"));
				Assert.AreEqual("net48",projectFile.GetProperty("TargetFramework"),"From tests\\Directory.Build.props.");
				Assert.AreEqual("true",projectFile.GetProperty("AutoGenerateBindingRedirects"));
				Assert.AreEqual("",projectFile.GetProperty("FixtureReleaseOnly"),"The Release group is not read for Debug.");
				//MSBuild 18 evaluates this fixture to TRACE;DEBUG: TRACE from Microsoft.NET.Sdk.CSharp.props, DEBUG from its targets.
				Assert.AreEqual("TRACE;DEBUG",projectFile.GetProperty("DefineConstants"));
				ProjectFile projectFileRelease=ProjectFile.Load(fixtureFolder.ProjectPath("SdkStyle"),"Release","x86");
				Assert.AreEqual("release",projectFileRelease.GetProperty("FixtureReleaseOnly"),"The Release group is read for Release.");
				Assert.AreEqual("TRACE;RELEASE",projectFileRelease.GetProperty("DefineConstants"),"The Release group's TRACE, then the SDK's RELEASE.");
				Assert.AreEqual(fixtureFolder.Combine(@"tests\BindingRedirects.config"),projectFile.GetProperty("AppConfig"),"Directory.Build.targets.");
				Assert.IsFalse(projectFile.Compiles("FromATarget.cs"),"Items of a target are not evaluated items.");
				ProjectItem projectItemMsTest=projectFile.GetReference("Microsoft.VisualStudio.QualityTools.UnitTestFramework");
				Assert.IsNotNull(projectItemMsTest,"The MSTest v1 reference of tests\\Directory.Build.props.");
				Assert.AreEqual(fixtureFolder.Combine(@"tests\Directory.Build.props"),projectItemMsTest.DefiningFile);
			}
		}

		[TestMethod]
		public void Sdk_CompilesTheFolder_LessTheDefaultExcludesAndTheRemoves() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				CollectionAssert.AreEquivalent(new[] { @"..\..\Shared\SharedAssemblyInfo.cs","App.xaml.cs",@"Forms\FormMain.cs",@"Forms\FormMain.Designer.cs",
					@"Frms\FrmPrint.xaml.cs","Program.cs",@"VendorDirect\Lab\LabClient.cs",@"VendorDirect\Lab\Sub\LabHelper.cs" },
					Includes(projectFile,"Compile"));
				Assert.IsTrue(projectFile.GetItem("Compile","Program.cs").IsFromWildcard);
				Assert.IsNull(projectFile.GetItem("Compile","Program.cs").DefiningFile,"The SDK's default glob.");
				foreach(string file in new[] { @"Removed\Old.cs",@"Nested.Tests\NestedTest.cs",@"bin\Debug\Stale.cs",@"obj\Debug\Generated.cs",@".vs\Hidden.cs" }) {
					Assert.IsFalse(projectFile.Compiles(file),file);
				}
				CollectionAssert.AreEqual(new string[0],projectFile.GetFilesNotCompiled("VendorDirect"));
			}
		}

		[TestMethod]
		public void Sdk_ReadsMetadataFromAttributesAndElements() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				Assert.AreEqual("Form",projectFile.GetItem("Compile",@"Forms\FormMain.cs").GetMetadata("SubType"),"Compile Update.");
				Assert.AreEqual(@"Properties\SharedAssemblyInfo.cs",projectFile.GetItem("Compile",@"..\..\Shared\SharedAssemblyInfo.cs").GetMetadata("Link"));
				Assert.AreEqual("FormMain.cs",projectFile.GetItem("EmbeddedResource",@"Forms\FormMain.resx").GetMetadata("DependentUpon"));
				Assert.AreEqual("Fixture.Branding.logo.png",projectFile.GetItem("EmbeddedResource",@"Branding\logo.png").GetMetadata("LogicalName"));
				Assert.AreEqual("PreserveNewest",projectFile.GetItem("None",@"Data\Seed.txt").GetMetadata("CopyToOutputDirectory"),"None Update.");
				Assert.AreEqual(@"..\..\Required dlls\MySqlConnector.dll",projectFile.GetReference("MySqlConnector").GetMetadata("HintPath"),"Attribute.");
				Assert.AreEqual(@"..\..\winmd\Windows.winmd",projectFile.GetReference("Windows").GetMetadata("HintPath"),"Element.");
				Assert.AreEqual("False",projectFile.GetReference("Windows").GetMetadata("Private"));
				Assert.IsFalse(projectFile.GetItem("Compile","Program.cs").HasMetadata("SubType"),"An Update touches only what it names.");
				Assert.IsNotNull(projectFile.GetProjectReference(@"..\Classic\Classic.csproj"));
				Assert.AreEqual("",projectFile.GetProjectReference(@"..\Classic\Classic.csproj").GetMetadata("Project"));
			}
		}

		[TestMethod]
		public void Sdk_BuildsTheXamlFilesAsPages_ButNotResourcesNoneOrTheApplication() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				CollectionAssert.AreEquivalent(new[] { @"Frms\FrmPrint.xaml" },Includes(projectFile,"Page"));
				CollectionAssert.AreEquivalent(new[] { "App.xaml" },Includes(projectFile,"ApplicationDefinition"));
				CollectionAssert.AreEquivalent(new[] { @"Frms\Dictionary.xaml",@"Resources\Icon.png" },Includes(projectFile,"Resource"));
				CollectionAssert.AreEquivalent(new[] { @"Branding\logo.png",@"Forms\FormMain.resx" },Includes(projectFile,"EmbeddedResource"));
			}
		}

		[TestMethod]
		public void Sdk_NoneHoldsTheOtherFiles() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				//As MSBuild has it: every file of the folder that is not source, a resx or a XAML file, and not excluded; the explicit None XAML file.
				CollectionAssert.AreEquivalent(new[] { @"Branding\logo.png",@"Data\Seed.txt",@"Frms\NotAPage.xaml",@"Resources\Icon.png",
					@"VendorDirect\Lab\Notes.txt" },Includes(projectFile,"None"));
			}
		}

		[TestMethod]
		public void Sdk_ExplicitSdkImports_GlobAsTheSdkAttributeDoes() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("SdkExplicitImports");
				Assert.IsTrue(projectFile.IsSdkStyle);
				projectFile.AssertCompiles("Program.cs",@"Forms\FormMain.cs");
				//Program.cs from the glob and again from the explicit item: two items, as MSBuild has them (the SDK then stops with NETSDK1022).
				Assert.AreEqual(2,projectFile.GetItems("Compile").Count(x => x.Include=="Program.cs"));
				CollectionAssert.AreEquivalent(new[] { @"Forms\FormMain.resx" },Includes(projectFile,"EmbeddedResource"),"EnableDefaultEmbeddedResourceItems=false.");
				Assert.AreEqual("true",projectFile.GetProperty("FixtureAfterSdkTargets"),"Properties after Sdk.targets see its defaults.");
				Assert.AreEqual("sdk",projectFile.GetProperty("FixtureSdkOnly"));
			}
		}

		[TestMethod]
		public void BothFormats_GiveTheSameAnswers() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFileClassic=fixtureFolder.Load("Classic");
				ProjectFile projectFileSdk=fixtureFolder.Load("SdkStyle");
				foreach(ProjectFile projectFile in new[] { projectFileClassic,projectFileSdk }) {
					projectFile.AssertCompiles("Program.cs",@"Forms\FormMain.cs",@"Forms\FormMain.Designer.cs",@"VendorDirect\Lab\LabClient.cs",
						@"VendorDirect\Lab\Sub\LabHelper.cs",@"Frms\FrmPrint.xaml.cs",@"..\..\Shared\SharedAssemblyInfo.cs");
					projectFile.AssertEmbeds(@"Forms\FormMain.resx",@"Branding\logo.png");
					projectFile.AssertPages(@"Frms\FrmPrint.xaml");
					Assert.AreEqual(@"Properties\SharedAssemblyInfo.cs",projectFile.AssertItem("Compile",@"..\..\Shared\SharedAssemblyInfo.cs").GetMetadata("Link"));
					Assert.AreEqual("Form",projectFile.AssertItem("Compile",@"Forms\FormMain.cs").GetMetadata("SubType"));
					Assert.AreEqual("FormMain.cs",projectFile.AssertItem("EmbeddedResource",@"Forms\FormMain.resx").GetMetadata("DependentUpon"));
					Assert.AreEqual("PreserveNewest",projectFile.AssertItem("None",@"Data\Seed.txt").GetMetadata("CopyToOutputDirectory"));
					Assert.AreEqual(@"..\..\Required dlls\MySqlConnector.dll",projectFile.GetReference("MySqlConnector").GetMetadata("HintPath"));
					Assert.AreEqual("False",projectFile.GetReference("Windows").GetMetadata("Private"));
					Assert.IsNotNull(projectFile.GetReference("Microsoft.VisualStudio.QualityTools.UnitTestFramework"));
					Assert.AreEqual("WinExe",projectFile.GetProperty("OutputType"));
					Assert.AreEqual("true",projectFile.GetProperty("AutoGenerateBindingRedirects"));
				}
			}
		}
		#endregion SDK-style

		#region ProjectXml
		[TestMethod]
		public void ProjectXml_ReadsBothFormats_WithoutTheNamespace() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				foreach(string name in new[] { "Classic","SdkStyle" }) {
					List<ProjectItemElement> listItems=ProjectXml.ReadItemElements(fixtureFolder.ProjectPath(name));
					ProjectItemElement projectItemElementMySql=listItems.Single(x => x.ItemType=="Reference" && x.Include=="MySqlConnector");
					Assert.AreEqual(@"..\..\Required dlls\MySqlConnector.dll",projectItemElementMySql.GetMetadata("HintPath"),name);
					ProjectItemElement projectItemElementWindows=listItems.Single(x => x.ItemType=="Reference" && x.Include=="Windows");
					Assert.AreEqual("False",projectItemElementWindows.GetMetadata("Private"),name);
					Assert.AreEqual(@"..\..\Shared\SharedAssemblyInfo.cs",listItems.Single(x => x.GetMetadata("Link")==@"Properties\SharedAssemblyInfo.cs").Include,name);
				}
				List<ProjectItemElement> listSdkItems=ProjectXml.ReadItemElements(fixtureFolder.ProjectPath("SdkStyle"));
				Assert.AreEqual("Removed\\**",listSdkItems.Single(x => x.ItemType=="Compile" && x.Remove!="").Remove);
				Assert.AreEqual(@"Forms\FormMain.cs",listSdkItems.Single(x => x.ItemType=="Compile" && x.Update!="").Specs.Single());
				Assert.IsTrue(ProjectFile.IsSdkStyleDocument(System.Xml.Linq.XDocument.Load(fixtureFolder.ProjectPath("SdkExplicitImports"))));
				Assert.IsFalse(ProjectFile.IsSdkStyleDocument(System.Xml.Linq.XDocument.Load(fixtureFolder.ProjectPath("Classic"))));
			}
		}
		#endregion ProjectXml

		[TestMethod]
		public void GetSolutionFilterProjects_ListsTheCSharpProjects() {
			string root=FixtureFolder.RepoRoot();
			List<string> listProjects=ProjectFile.GetSolutionFilterProjects(Path.Combine(root,"OpenDental.Local.slnf"));
			CollectionAssert.Contains(listProjects,Path.Combine(root,"OpenPmsDental","OpenPmsDental.csproj"));
			CollectionAssert.Contains(listProjects,Path.Combine(root,"tests","ProjectFileTests","ProjectFileTests.csproj"));
			Assert.IsFalse(listProjects.Any(x => x.EndsWith(".vcxproj")));
			Assert.IsTrue(listProjects.All(File.Exists));
		}
	}
}
