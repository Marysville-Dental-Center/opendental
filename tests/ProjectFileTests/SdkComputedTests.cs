// What MSBuild's and the .NET SDK's own files compute, which ProjectFile now computes where they do (package S1-T2 of the .NET 10 plan,
// docs/specs/net10-plan.md): the Link the SDK gives an item outside the project folder, the output and intermediate paths, the target framework,
// the common targets' defaults and the WindowsDesktop SDK's page metadata, in the classic and the SDK-style fixtures, with the cases where
// MSBuild gives none; and the properties it does not compute, which throw instead of reading as ''.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	[TestClass]
	public class SdkComputedTests {
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

		#region Link
		[TestMethod]
		public void Sdk_AnItemOutsideTheFolder_GetsTheLinkTheSdkGivesIt() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("SdkLinks");
				//The native file at the top of the output with no Link of its own, as OpenPmsDental.csproj's WebView2Loader.dll once S1-5 drops its
				//Link (tests\ReleaseFlowTests\ReleaseFilesTests.cs checks it).
				ProjectItem projectItemNative=projectFile.AssertItem("None",@"..\..\Required dlls\Native.dll");
				Assert.AreEqual("Native.dll",projectItemNative.GetMetadata("Link"));
				Assert.AreEqual("PreserveNewest",projectItemNative.GetMetadata("CopyToOutputDirectory"));
				Assert.AreEqual("Top.cs",projectFile.AssertItem("Compile",@"..\..\Shared\Linked\Top.cs").GetMetadata("Link"));
				Assert.AreEqual(@"Sub\Deep.cs",projectFile.AssertItem("Compile",@"..\..\Shared\Linked\Sub\Deep.cs").GetMetadata("Link"),
					"The folders the wildcard matched (%(RecursiveDir)).");
				ProjectItem projectItemSeed=projectFile.AssertItem("Content",@"..\..\Shared\Data\Seed.json");
				Assert.AreEqual(@"Data\Seed.json",projectItemSeed.GetMetadata("Link"),"Under LinkBase.");
				Assert.AreEqual(@"Data\",projectItemSeed.GetMetadata("LinkBase"),"LinkBase gets a trailing back slash.");
				Assert.AreEqual(@"Resources\Strings.resx",projectFile.AssertItem("EmbeddedResource",@"..\..\Shared\Strings.resx").GetMetadata("Link"),
					"An explicit Link stays.");
				//Inside the project folder: none (the default globs' Program.cs and Notes.txt).
				Assert.IsFalse(projectFile.AssertItem("Compile","Program.cs").HasMetadata("Link"));
				Assert.IsFalse(projectFile.AssertItem("None","Notes.txt").HasMetadata("Link"));
			}
		}

		[TestMethod]
		public void Sdk_AnItemAfterAnExplicitSdkTargetsImport_GetsNoLink() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//The automatic Link is an Update inside Sdk.targets: an item the project adds after an explicit Sdk.targets import has none, so a
				//check of its Link fails, as MSBuild would copy it under its path, not to the top of the output.
				ProjectItem projectItemAfter=fixtureFolder.Load("SdkLinks").AssertItem("None",@"..\..\Required dlls\After.dll");
				Assert.IsFalse(projectItemAfter.HasMetadata("Link"));
				Assert.AreNotEqual("After.dll",projectItemAfter.GetMetadata("Link"));
			}
		}

		[TestMethod]
		public void Sdk_AnItemOfDirectoryBuildTargets_GetsTheLinkToo() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//Microsoft.Common.targets imports Directory.Build.targets before Microsoft.NET.Sdk.targets applies the Link.
				fixtureFolder.EditFile("Directory.Build.targets","</Project>","<ItemGroup Condition=\"'$(MSBuildProjectName)' == 'SdkStyle'\">"
					+"<None Include=\"$(MSBuildThisFileDirectory)Shared\\Data\\Seed.json\" /></ItemGroup></Project>");
				ProjectItem projectItem=fixtureFolder.Load("SdkStyle").AssertItem("None",fixtureFolder.Combine(@"Shared\Data\Seed.json"));
				Assert.AreEqual("Seed.json",projectItem.GetMetadata("Link"));
				Assert.AreEqual(fixtureFolder.Combine("Directory.Build.targets"),projectItem.DefiningFile);
			}
		}

		[TestMethod]
		public void Sdk_SetLinkMetadataAutomaticallyFalse_GivesNoLink() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.EditProject("SdkLinks","<FixtureIntermediateBeforeSdkTargets>",
					"<SetLinkMetadataAutomatically>false</SetLinkMetadataAutomatically><FixtureIntermediateBeforeSdkTargets>");
				ProjectFile projectFile=fixtureFolder.Load("SdkLinks");
				Assert.AreEqual("",projectFile.AssertItem("None",@"..\..\Required dlls\Native.dll").GetMetadata("Link"));
				Assert.AreEqual("",projectFile.AssertItem("Compile",@"..\..\Shared\Linked\Sub\Deep.cs").GetMetadata("Link"));
				Assert.AreEqual("Data",projectFile.AssertItem("Content",@"..\..\Shared\Data\Seed.json").GetMetadata("LinkBase"),"Not touched either.");
			}
		}

		[TestMethod]
		public void Classic_GetsNoAutomaticLink() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//Only the SDK's targets add the Link: a classic project copies such a file under its relative path unless it names a Link.
				fixtureFolder.EditProject("Classic","<Resource Include=\"Resources\\Icon.png\" />",
					"<Resource Include=\"Resources\\Icon.png\" /><None Include=\"..\\..\\Required dlls\\Native.dll\" />");
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.IsFalse(projectFile.AssertItem("None",@"..\..\Required dlls\Native.dll").HasMetadata("Link"));
			}
		}
		#endregion Link

		#region Paths and the target framework
		[TestMethod]
		public void Classic_GetsTheCommonTargetsPaths() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual(@"obj\",projectFile.GetProperty("BaseIntermediateOutputPath"));
				Assert.AreEqual(fixtureFolder.Combine(@"tests\Classic\obj\"),projectFile.GetProperty("MSBuildProjectExtensionsPath"));
				Assert.AreEqual(@"obj\x86\Debug\",projectFile.GetProperty("IntermediateOutputPath"));
				Assert.AreEqual(@"bin\",projectFile.GetProperty("BaseOutputPath"));
				Assert.AreEqual(@"bin\Debug\",projectFile.GetProperty("OutDir"),"OutputPath of the Debug|x86 group.");
				Assert.AreEqual(".NETFramework",projectFile.GetProperty("TargetFrameworkIdentifier"));
				Assert.AreEqual(".NETFramework,Version=v4.8",projectFile.GetProperty("TargetFrameworkMoniker"));
				Assert.AreEqual("Windows",projectFile.GetProperty("TargetPlatformIdentifier"));
				Assert.AreEqual(".exe",projectFile.GetProperty("TargetExt"));
				Assert.AreEqual(fixtureFolder.Combine(@"tests\Classic\bin\Debug\"),projectFile.GetProperty("TargetDir"));
				Assert.AreEqual(fixtureFolder.Combine(@"tests\Classic\bin\Debug\ClassicFixture.exe"),projectFile.GetProperty("TargetPath"));
				Assert.AreEqual("true",projectFile.GetProperty("GenerateBindingRedirectsOutputType"),"An exe.");
				Assert.AreEqual("18.0",projectFile.GetProperty("VisualStudioVersion"));
				Assert.AreEqual("",projectFile.GetProperty("RuntimeIdentifier"),"The SDK's, not a classic project's.");
				Assert.AreEqual(@"obj\x86\Release\",ProjectFile.Load(fixtureFolder.ProjectPath("Classic"),"Release","x86").GetProperty("IntermediateOutputPath"));
				ProjectFile projectFileAnyCpu=ProjectFile.Load(fixtureFolder.ProjectPath("Classic"),"Debug","AnyCPU");
				Assert.AreEqual(@"obj\Debug\",projectFileAnyCpu.GetProperty("IntermediateOutputPath"),"No platform folder for AnyCPU.");
				Assert.AreEqual(@"bin\Debug\",projectFileAnyCpu.GetProperty("OutputPath"),"The default for a configuration without an OutputPath.");
			}
		}

		[TestMethod]
		public void Classic_AutoGenerateBindingRedirects_FollowsTheCommonTargets() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//MSBuild 18 turns it on for a .NET Framework 4.7.2+ project that leaves it empty, and only an exe generates the redirects.
				fixtureFolder.EditProject("Classic","<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>","");
				fixtureFolder.EditProject("Classic","<OutputType>WinExe</OutputType>","<OutputType>Library</OutputType>");
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("true",projectFile.GetProperty("AutoGenerateBindingRedirects"));
				Assert.AreEqual("",projectFile.GetProperty("GenerateBindingRedirectsOutputType"),"A library sets none.");
				Assert.AreEqual(".dll",projectFile.GetProperty("TargetExt"));
				fixtureFolder.EditProject("Classic","<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>","<TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion>");
				Assert.AreEqual("",fixtureFolder.Load("Classic").GetProperty("AutoGenerateBindingRedirects"),"Before 4.7.2.");
			}
		}

		[TestMethod]
		public void Sdk_GetsTheSdkPaths_WithThePlatformAndTheTargetFrameworkFolders() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//The fixture's Directory.Build.props, unlike the repository's, keeps the SDK's platform and target framework folders.
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				Assert.AreEqual(".NETFramework",projectFile.GetProperty("TargetFrameworkIdentifier"));
				Assert.AreEqual("v4.8",projectFile.GetProperty("TargetFrameworkVersion"));
				Assert.AreEqual(@"bin\x86\Debug\net48\",projectFile.GetProperty("OutputPath"));
				Assert.AreEqual(@"obj\x86\Debug\net48\",projectFile.GetProperty("IntermediateOutputPath"));
				Assert.AreEqual(fixtureFolder.Combine(@"tests\SdkStyle\bin\x86\Debug\net48\SdkStyleFixture.exe"),projectFile.GetProperty("TargetPath"));
				Assert.AreEqual("SdkStyle",projectFile.GetProperty("RootNamespace"),"Microsoft.NET.Sdk.props: the project's name.");
				Assert.AreEqual("x86",projectFile.GetProperty("PlatformTarget"),"From the platform.");
				Assert.AreEqual("Windows",projectFile.GetProperty("TargetPlatformIdentifier"));
				//The repository's Directory.Build.props turns both folders off: bin\Debug\ and obj\Debug\, as the classic projects have them.
				fixtureFolder.EditProject("SdkStyle","<UseWPF>true</UseWPF>","<UseWPF>true</UseWPF><AppendPlatformToOutputPath>false</AppendPlatformToOutputPath>"
					+"<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>");
				projectFile=fixtureFolder.Load("SdkStyle");
				Assert.AreEqual(@"bin\Debug\",projectFile.GetProperty("OutputPath"));
				Assert.AreEqual(@"obj\Debug\",projectFile.GetProperty("IntermediateOutputPath"));
			}
		}

		[TestMethod]
		public void Sdk_TheDefaultGlobs_LeaveOutTheOutputPath() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//Microsoft.NET.DefaultOutputPaths.targets adds $(OutputPath)/** to DefaultItemExcludes before it appends the target framework.
				fixtureFolder.EditProject("SdkStyle","<UseWPF>true</UseWPF>","<UseWPF>true</UseWPF><OutputPath>out\\</OutputPath>");
				fixtureFolder.WriteFile(@"tests\SdkStyle\out\Stale.cs","");
				fixtureFolder.WriteFile(@"tests\SdkStyle\out\net48\Stale.cs","");
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				Assert.AreEqual(@"out\net48\",projectFile.GetProperty("OutputPath"));
				projectFile.AssertDoesNotCompile(@"out\Stale.cs",@"out\net48\Stale.cs");
				projectFile.AssertCompiles("Program.cs");
			}
		}

		[TestMethod]
		public void Sdk_AProjectWithoutItsOwnNames_GetsTheSdkDefaults() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("SdkLinks");
				Assert.AreEqual("SdkLinks",projectFile.GetProperty("AssemblyName"),"Microsoft.NET.Sdk.props: the project's name.");
				Assert.AreEqual("Library",projectFile.GetProperty("OutputType"));
				Assert.AreEqual(".dll",projectFile.GetProperty("TargetExt"));
				//Microsoft.Common.CurrentVersion.targets sets it for an exe only, so a test project (a library) needs its own (S1-6).
				Assert.AreEqual("",projectFile.GetProperty("GenerateBindingRedirectsOutputType"));
			}
		}

		[TestMethod]
		public void SdkExplicitImports_APropertyAfterSdkTargets_SeesTheIntermediatePath() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//Why S1-5 imports Sdk.props and Sdk.targets explicitly: the program's build stamp file is $(IntermediateOutputPath)ForkBuildStamp.cs.
				ProjectFile projectFile=fixtureFolder.Load("SdkLinks");
				Assert.AreEqual("",projectFile.GetProperty("FixtureIntermediateBeforeSdkTargets"),"Before Sdk.targets the SDK has not computed it.");
				Assert.AreEqual(@"obj\x86\Debug\net48\",projectFile.GetProperty("FixtureIntermediateAfterSdkTargets"));
			}
		}

		[TestMethod]
		public void Sdk_TheTargetFrameworkNames_GiveTheirIdentifierAndVersion() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				foreach(string[] arrayCase in new[] { new[] { "net48",".NETFramework","v4.8","Windows" },new[] { "net472",".NETFramework","v4.7.2","Windows" },
					new[] { "net481",".NETFramework","v4.8.1","Windows" },new[] { "netstandard2.0",".NETStandard","v2.0","Windows" },
					new[] { "net10.0-windows",".NETCoreApp","v10.0","Windows" },new[] { "net10.0",".NETCoreApp","v10.0","" } })
				{
					WriteSdkLinksWithTargetFramework(fixtureFolder,arrayCase[0]);
					ProjectFile projectFile=fixtureFolder.Load("SdkLinks");
					Assert.AreEqual(arrayCase[1],projectFile.GetProperty("TargetFrameworkIdentifier"),arrayCase[0]);
					Assert.AreEqual(arrayCase[2],projectFile.GetProperty("TargetFrameworkVersion"),arrayCase[0]);
					Assert.AreEqual(arrayCase[3],projectFile.GetProperty("TargetPlatformIdentifier"),arrayCase[0]);
					Assert.AreEqual(@"obj\x86\Debug\"+arrayCase[0]+@"\",projectFile.GetProperty("IntermediateOutputPath"),arrayCase[0]);
				}
				//A .NET 10 target without a platform has no platform version, as the SDK has it.
				Assert.AreEqual("",fixtureFolder.Load("SdkLinks").GetProperty("TargetPlatformVersion"),"net10.0");
				//The SDK's platform files give net10.0-windows its platform version later on; not computed, so asking throws.
				WriteSdkLinksWithTargetFramework(fixtureFolder,"net10.0-windows");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkLinks").GetProperty("TargetPlatformVersion"),"TargetPlatformVersion");
				WriteSdkLinksWithTargetFramework(fixtureFolder,"net10.0-windows10.0.19041.0");
				Assert.AreEqual("10.0.19041.0",fixtureFolder.Load("SdkLinks").GetProperty("TargetPlatformVersion"));
				//A name this reader does not know: every property derived from it throws.
				WriteSdkLinksWithTargetFramework(fixtureFolder,"uap10.0");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkLinks").GetProperty("TargetFrameworkIdentifier"),"uap10.0");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkLinks").GetProperty("OutputPath"),"uap10.0");
			}
		}

		///<summary>The SdkLinks fixture as it is, with its own TargetFramework.</summary>
		private static void WriteSdkLinksWithTargetFramework(FixtureFolder fixtureFolder,string targetFramework) {
			string text=System.IO.File.ReadAllText(System.IO.Path.Combine(FixtureFolder.RepoRoot(),"tests","ProjectFileTests","Fixtures","SdkLinks.csproj.fixture"));
			Assert.IsTrue(text.Contains("<FixtureIntermediateBeforeSdkTargets>"));
			fixtureFolder.WriteFile(@"tests\SdkLinks\SdkLinks.csproj",text.Replace("<FixtureIntermediateBeforeSdkTargets>",
				"<TargetFramework>"+targetFramework+"</TargetFramework><FixtureIntermediateBeforeSdkTargets>"));
		}
		#endregion Paths and the target framework

		#region WPF pages
		[TestMethod]
		public void Sdk_WpfPages_GetTheWindowsDesktopItemDefinitions() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				foreach(ProjectItem projectItem in new[] { projectFile.AssertItem("Page",@"Frms\FrmPrint.xaml"),projectFile.AssertItem("ApplicationDefinition","App.xaml") }) {
					Assert.AreEqual("MSBuild:Compile",projectItem.GetMetadata("Generator"),projectItem.ToString());
					Assert.AreEqual("Wpf",projectItem.GetMetadata("XamlRuntime"),projectItem.ToString());
					Assert.AreEqual("Designer",projectItem.GetMetadata("SubType"),projectItem.ToString());
				}
				//A classic project has its own metadata only.
				ProjectItem projectItemClassic=fixtureFolder.Load("Classic").AssertItem("Page",@"Frms\FrmPrint.xaml");
				Assert.AreEqual("MSBuild:Compile",projectItemClassic.GetMetadata("Generator"));
				Assert.IsFalse(projectItemClassic.HasMetadata("XamlRuntime"));
				//The item's own metadata win.
				fixtureFolder.EditProject("SdkStyle","<Compile Update=\"Forms\\FormMain.cs\" SubType=\"Form\" />",
					"<Compile Update=\"Forms\\FormMain.cs\" SubType=\"Form\" /><Page Update=\"Frms\\FrmPrint.xaml\" SubType=\"Code\" />");
				Assert.AreEqual("Code",fixtureFolder.Load("SdkStyle").AssertItem("Page",@"Frms\FrmPrint.xaml").GetMetadata("SubType"));
			}
		}
		#endregion WPF pages

		#region Not modelled
		[TestMethod]
		public void NotModelledProperties_ThrowInAConditionAnIncludeMetadataAndGetProperty() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				const string anchor="<Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />";
				string textClassic=System.IO.File.ReadAllText(fixtureFolder.ProjectPath("Classic"));
				//In a condition: the solution build's $(SolutionDir).
				fixtureFolder.EditProject("Classic",anchor,"<PropertyGroup Condition=\"'$(SolutionDir)' != ''\"><FixtureX>1</FixtureX></PropertyGroup>"+anchor);
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("Classic"),"$(SolutionDir)");
				//In an Include, through another property: a machine path.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(anchor,"<PropertyGroup><FixtureTools>$(MSBuildExtensionsPath)\\Tools"
					+"</FixtureTools></PropertyGroup><ItemGroup><None Include=\"$(FixtureTools)\\*.dll\" /></ItemGroup>"+anchor));
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("Classic"),"$(MSBuildExtensionsPath)");
				//In an Exists() of a condition.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(anchor,"<PropertyGroup Condition=\"Exists('$(VsInstallRoot)\\Common7')\">"
					+"<FixtureX>1</FixtureX></PropertyGroup>"+anchor));
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("Classic"),"$(VsInstallRoot)");
				//In metadata.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(anchor,"<ItemGroup><Reference Include=\"Fixture\">"
					+"<HintPath>$(NuGetPackageRoot)fixture\\Fixture.dll</HintPath></Reference></ItemGroup>"+anchor));
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("Classic"),"$(NuGetPackageRoot)");
				//In a property only: the project loads, and asking for the property throws.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(anchor,"<PropertyGroup><FixtureTools>$(MSBuildExtensionsPath)\\Tools"
					+"</FixtureTools></PropertyGroup>"+anchor));
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				AssertThrows<NotSupportedException>(() => projectFile.GetProperty("FixtureTools"),"$(MSBuildExtensionsPath)");
				AssertThrows<NotSupportedException>(() => projectFile.GetProperty("SolutionDir"),"$(SolutionDir)");
				Assert.IsFalse(projectFile.IsPropertyKnown("FixtureTools"));
				Assert.IsFalse(projectFile.IsPropertyKnown("SolutionDir"));
				Assert.IsTrue(projectFile.IsPropertyKnown("OutputPath"));
				Assert.IsTrue(projectFile.IsPropertyKnown("FixtureNothingSetsThis"),"A name nothing computes is ''.");
				//Set by the project itself: known.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(anchor,"<PropertyGroup><SolutionDir>..\\..\\</SolutionDir></PropertyGroup>"
					+"<PropertyGroup Condition=\"'$(SolutionDir)' != ''\"><FixtureX>1</FixtureX></PropertyGroup>"+anchor));
				Assert.AreEqual("1",fixtureFolder.Load("Classic").GetProperty("FixtureX"));
				//The SDK's own: '' in a classic project, not computed in an SDK-style one.
				Assert.AreEqual("",fixtureFolder.Load("Classic").GetProperty("RuntimeIdentifier"));
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle").GetProperty("RuntimeIdentifier"),"$(RuntimeIdentifier)");
			}
		}
		#endregion Not modelled
	}
}
