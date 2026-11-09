// The properties MSBuild's own files give a project that ProjectFile gave as '' before package S1-T3 of the .NET 10 plan (docs/specs/net10-plan.md):
// S1-T2's check found them by comparing GetProperty with MSBuild 18 -getProperty on every project of the solution filter. Each is now computed
// where MSBuild computes it, or throws as not modelled. The expected values are MSBuild 18's for the same fixture files (S1-T3 laid the fixtures out
// in a scratch folder and evaluated them with -getProperty); tools\Test-ProjectFileEvaluator.ps1 checks the repository's projects the same way.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	[TestClass]
	public class ComputedDefaultsTests {
		private const string ANCHOR_CLASSIC="<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>";
		private const string ANCHOR_SDKLINKS="<FixtureIntermediateBeforeSdkTargets>";

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

		private static void AssertProperties(ProjectFile projectFile,params string[] arrayNameValues) {
			for(int i=0;i<arrayNameValues.Length;i+=2) {
				Assert.AreEqual(arrayNameValues[i+1],projectFile.GetProperty(arrayNameValues[i]),projectFile.Describe()+": "+arrayNameValues[i]);
			}
		}

		#region The common props and targets
		[TestMethod]
		public void Classic_GetsTheCommonTargetsAndFrameworkDefaults() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//Microsoft.Common.props and .targets, Microsoft.Common.CurrentVersion.targets, Microsoft.NETFramework.CurrentVersion.props,
				//Microsoft.NET.props, Microsoft.CSharp.CurrentVersion.targets and Microsoft.NET.Build.Extensions.targets of MSBuild 18.
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				AssertProperties(projectFile,"PublishDir",@"bin\Debug\app.publish\","Prefer32Bit","true","SubsystemVersion","6.00","HighEntropyVA","true",
					"ProduceReferenceAssembly","false","Utf8Output","true","ProjectExt",".csproj","PlatformTargetAsMSBuildArchitecture","CurrentArchitecture",
					"PlatformTargetAsMSBuildArchitectureExplicitlySet","false","TargetFrameworkAsMSBuildRuntime","CurrentRuntime","_TargetFrameworkVersionWithoutV","4.8",
					"TargetingClr2Framework","","ImportDirectoryBuildProps","true","ImportDirectoryBuildTargets","true","ImportProjectExtensionProps","true",
					"ImportProjectExtensionTargets","true","_DirectoryBuildPropsFile","Directory.Build.props","_DirectoryBuildTargetsFile","Directory.Build.targets",
					"DirectoryBuildPropsPath",fixtureFolder.Combine(@"tests\Directory.Build.props"),"_DirectoryBuildPropsBasePath",fixtureFolder.Combine("tests"),
					"DirectoryBuildTargetsPath",fixtureFolder.Combine("Directory.Build.targets"),"_DirectoryBuildTargetsBasePath",fixtureFolder.Root,
					"_InitialMSBuildProjectExtensionsPath",fixtureFolder.Combine(@"tests\Classic\obj\"));
				//The SDK's own are '' in a classic project, as MSBuild has them.
				AssertProperties(projectFile,"DisableHandlePackageFileConflicts","","RestoreProjectStyle","","PackRelease","","IsPublishable","",
					"CopyLocalLockFileAssemblies","","GenerateDocumentationFile","","DocumentationFile","","ProjectAssetsFile","");
			}
		}

		[TestMethod]
		public void Classic_TheFrameworkAndThePlatform_DecidePrefer32BitSubsystemAndArchitecture() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				string textClassic=System.IO.File.ReadAllText(fixtureFolder.ProjectPath("Classic"));
				//.NET Framework 4.0: no Prefer32Bit, SubsystemVersion or HighEntropyVA default; the task host's runtime is CLR4.
				fixtureFolder.EditProject("Classic","<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>","<TargetFrameworkVersion>v4.0</TargetFrameworkVersion>");
				AssertProperties(fixtureFolder.Load("Classic"),"Prefer32Bit","false","SubsystemVersion","","HighEntropyVA","false","TargetFrameworkAsMSBuildRuntime","CLR4",
					"TargetingClr2Framework","");
				//3.5 runs on CLR 2.
				fixtureFolder.EditProject("Classic","<TargetFrameworkVersion>v4.0</TargetFrameworkVersion>","<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>");
				AssertProperties(fixtureFolder.Load("Classic"),"Prefer32Bit","false","HighEntropyVA","false","TargetingClr2Framework","true",
					"TargetFrameworkAsMSBuildRuntime","CLR2");
				//A library does not prefer 32 bits.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace("<OutputType>WinExe</OutputType>","<OutputType>Library</OutputType>"));
				AssertProperties(fixtureFolder.Load("Classic"),"Prefer32Bit","false","SubsystemVersion","6.00","HighEntropyVA","true");
				//x64 is itself (on a 64-bit Windows, where MSBuild sets MSBuildExtensionsPath64); a PublishDir of the project's own gets its trailing
				//back slash; a Prefer32Bit of its own wins.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(ANCHOR_CLASSIC,ANCHOR_CLASSIC
					+"<PlatformTarget>x64</PlatformTarget><PublishDir>out</PublishDir><Prefer32Bit>false</Prefer32Bit>"));
				AssertProperties(fixtureFolder.Load("Classic"),"PlatformTargetAsMSBuildArchitecture",Environment.Is64BitOperatingSystem ? "x64" : "CurrentArchitecture",
					"PublishDir",@"out\","Prefer32Bit","false","PlatformTargetAsMSBuildArchitectureExplicitlySet","false");
				//ARM: subsystem 6.02, the x86 task host; PreferNativeArm64 turns Prefer32Bit's default off.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(ANCHOR_CLASSIC,ANCHOR_CLASSIC
					+"<PlatformTarget>ARM</PlatformTarget><PreferNativeArm64>true</PreferNativeArm64>"));
				AssertProperties(fixtureFolder.Load("Classic"),"PlatformTargetAsMSBuildArchitecture","x86","SubsystemVersion","6.02","Prefer32Bit","false");
				//x86 is itself.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(ANCHOR_CLASSIC,ANCHOR_CLASSIC+"<PlatformTarget>x86</PlatformTarget>"));
				AssertProperties(fixtureFolder.Load("Classic"),"PlatformTargetAsMSBuildArchitecture","x86");
			}
		}

		[TestMethod]
		public void TheDirectoryBuildSwitches_TurnTheImportOff_AndAPathOfTheProjectsOwnIsImported() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//ImportDirectoryBuildProps is a switch MSBuild sets to true unless the project sets it; anything but 'true' turns the import off.
				fixtureFolder.WriteFile(@"tests\Classic\Directory.Build.props","<Project><PropertyGroup><FixtureNearest>classic</FixtureNearest></PropertyGroup></Project>");
				Assert.AreEqual("classic",fixtureFolder.Load("Classic").GetProperty("FixtureNearest"),"The nearest one.");
				fixtureFolder.WriteFile(@"tests\Classic\Directory.Build.props","<Project><PropertyGroup><FixtureNearest>classic</FixtureNearest>"
					+"<DirectoryBuildTargetsPath>"+fixtureFolder.Combine("Other.targets")+"</DirectoryBuildTargetsPath></PropertyGroup></Project>");
				fixtureFolder.WriteFile("Other.targets","<Project><PropertyGroup><FixtureOtherTargets>other</FixtureOtherTargets></PropertyGroup></Project>");
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("other",projectFile.GetProperty("FixtureOtherTargets"),"DirectoryBuildTargetsPath of the project's own is imported.");
				Assert.AreEqual("",projectFile.GetProperty("_DirectoryBuildTargetsFile"),"MSBuild does not look for one then.");
				Assert.IsFalse(projectFile.FilesRead.Contains(fixtureFolder.Combine("Directory.Build.targets")));
				//Not 'true': nothing imported.
				fixtureFolder.EditProject("Classic","<Project ToolsVersion=\"15.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">",
					"<Project ToolsVersion=\"15.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"><PropertyGroup><ImportDirectoryBuildProps>no</ImportDirectoryBuildProps>"
					+"</PropertyGroup>");
				projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("",projectFile.GetProperty("FixtureNearest"));
				Assert.AreEqual("",projectFile.GetProperty("DirectoryBuildPropsPath"));
				//A Custom* import around it is not modelled.
				fixtureFolder.EditProject("Classic","<ImportDirectoryBuildProps>no</ImportDirectoryBuildProps>",
					"<CustomBeforeDirectoryBuildProps>"+fixtureFolder.Combine("Other.targets")+"</CustomBeforeDirectoryBuildProps>");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("Classic"),"$(CustomBeforeDirectoryBuildProps)");
			}
		}
		#endregion The common props and targets

		#region The SDK's
		[TestMethod]
		public void Sdk_GetsTheSdksDefaults() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				AssertProperties(projectFile,"DisableHandlePackageFileConflicts","true","RestoreProjectStyle","PackageReference","PackRelease","true",
					"IsPublishable","true","CopyLocalLockFileAssemblies","true","GenerateDocumentationFile","false","DocumentationFile","",
					"ProduceReferenceAssembly","false","Prefer32Bit","false","SubsystemVersion","6.00","HighEntropyVA","true","Utf8Output","true",
					"PlatformTargetAsMSBuildArchitecture","x86","_TargetFrameworkVersionWithoutV","4.8",
					"ProjectAssetsFile",fixtureFolder.Combine(@"tests\SdkStyle\obj\project.assets.json"));
			}
		}

		[TestMethod]
		public void Sdk_TheTargetFramework_DecidesCopyLocalAndTheReferenceAssembly() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//Microsoft.PackageDependencyResolution.targets and Microsoft.NET.TargetFrameworkInference.targets, as MSBuild 18 evaluates them.
				foreach(string[] arrayCase in new[] { new[] { "net10.0","","false","true","" },new[] { "net10.0","<OutputType>Exe</OutputType>","true","true","" },
					new[] { "netstandard2.0","","false","false","" },new[] { "net48","","true","false","6.00" } })
				{
					WriteSdkLinks(fixtureFolder,"<TargetFramework>"+arrayCase[0]+"</TargetFramework>"+arrayCase[1]);
					ProjectFile projectFile=fixtureFolder.Load("SdkLinks");
					AssertProperties(projectFile,"CopyLocalLockFileAssemblies",arrayCase[2],"ProduceReferenceAssembly",arrayCase[3],"SubsystemVersion",arrayCase[4],
						"HighEntropyVA","true");
				}
				WriteSdkLinks(fixtureFolder,"<TargetFramework>netstandard2.0</TargetFramework>");
				Assert.AreEqual("CLR2",fixtureFolder.Load("SdkLinks").GetProperty("TargetFrameworkAsMSBuildRuntime"),"Its version v2.0, as MSBuild compares it.");
				//A framework the reader does not know: they cannot be evaluated.
				WriteSdkLinks(fixtureFolder,"<TargetFramework>uap10.0</TargetFramework>");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkLinks").GetProperty("CopyLocalLockFileAssemblies"),"uap10.0");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkLinks").GetProperty("ProduceReferenceAssembly"),"uap10.0");
			}
		}

		[TestMethod]
		public void Sdk_TheDocumentationFile_FollowsGenerateDocumentationFile() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//Microsoft.NET.Sdk.BeforeCommon.targets: on, the file is in the intermediate folder; off, a file of the project's own is dropped;
				//a file of the project's own turns it on.
				WriteSdkLinks(fixtureFolder,"<GenerateDocumentationFile>true</GenerateDocumentationFile>");
				AssertProperties(fixtureFolder.Load("SdkLinks"),"GenerateDocumentationFile","true","DocumentationFile",@"obj\x86\Debug\net48\SdkLinks.xml");
				WriteSdkLinks(fixtureFolder,"<GenerateDocumentationFile>false</GenerateDocumentationFile><DocumentationFile>doc.xml</DocumentationFile>");
				AssertProperties(fixtureFolder.Load("SdkLinks"),"GenerateDocumentationFile","false","DocumentationFile","");
				WriteSdkLinks(fixtureFolder,"<DocumentationFile>doc.xml</DocumentationFile>");
				AssertProperties(fixtureFolder.Load("SdkLinks"),"GenerateDocumentationFile","true","DocumentationFile","doc.xml");
			}
		}

		[TestMethod]
		public void Sdk_ARuntimeIdentifierOfTheProjectsOwn_MakesTheOutputPathsUnknown() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//Microsoft.NET.RuntimeIdentifierInference.targets adds it to the output paths, which is not modelled; the repository's
				//Directory.Build.props turns that off (AppendRuntimeIdentifierToOutputPath false).
				WriteSdkLinks(fixtureFolder,"<RuntimeIdentifier>win-x86</RuntimeIdentifier>");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkLinks").GetProperty("OutputPath"),"RuntimeIdentifier win-x86");
				WriteSdkLinks(fixtureFolder,"<RuntimeIdentifier>win-x86</RuntimeIdentifier><AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>");
				Assert.AreEqual(@"bin\x86\Debug\net48\",fixtureFolder.Load("SdkLinks").GetProperty("OutputPath"));
			}
		}

		///<summary>The SdkLinks fixture as it is, with the properties before its first property of its own.</summary>
		private static void WriteSdkLinks(FixtureFolder fixtureFolder,string properties) {
			string text=System.IO.File.ReadAllText(System.IO.Path.Combine(FixtureFolder.RepoRoot(),"tests","ProjectFileTests","Fixtures","SdkLinks.csproj.fixture"));
			Assert.IsTrue(text.Contains(ANCHOR_SDKLINKS));
			fixtureFolder.WriteFile(@"tests\SdkLinks\SdkLinks.csproj",text.Replace(ANCHOR_SDKLINKS,properties+ANCHOR_SDKLINKS));
		}
		#endregion The SDK's

		#region Not modelled
		[TestMethod]
		public void TheToolsetsNames_AreNotModelled_InClassicAndSdkStyleProjectsAlike() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//MSBuild 18 gives the classic fixture Version 1.0.0.0 (Microsoft.WinFx.targets), RuntimeIdentifiers win;win-x86;win-x64;win-arm64 and
				//NuGetTargetMoniker .NETFramework,Version=v4.8 (Microsoft.NuGet.targets): files of the .NET Framework and of Visual Studio's components,
				//which the common targets import for every project. Not read, so they throw instead of reading as ''.
				foreach(string name in new[] { "Classic","SdkStyle" }) {
					ProjectFile projectFile=fixtureFolder.Load(name);
					foreach(string property in new[] { "Version","RuntimeIdentifiers","NuGetTargetMoniker" }) {
						AssertThrows<NotSupportedException>(() => projectFile.GetProperty(property),"$("+property+")");
						Assert.IsFalse(projectFile.IsPropertyKnown(property),name+": "+property);
					}
				}
				//And the SDK's own in an SDK-style project.
				foreach(string property in new[] { "PublishDir","RuntimeIdentifier","EnableNETAnalyzers","VersionPrefix" }) {
					AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle").GetProperty(property),"$("+property+")");
				}
			}
		}

		[TestMethod]
		public void TheNotModelledNames_AreEmpty_BeforeMSBuildsFilesSetThem() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//MSBuild 18: in the project's own properties, before the common targets (and before Sdk.targets), they are '' as nothing has set
				//them yet; NuGet's obj\*.nuget.g.props, read with Microsoft.Common.props, tests '$(ProjectAssetsFile)' == '' there.
				fixtureFolder.EditProject("Classic",ANCHOR_CLASSIC,ANCHOR_CLASSIC+"<FixtureBody>[$(Version)][$(RuntimeIdentifiers)][$(NuGetTargetMoniker)]</FixtureBody>");
				Assert.AreEqual("[][][]",fixtureFolder.Load("Classic").GetProperty("FixtureBody"));
				WriteSdkLinks(fixtureFolder,"<FixtureBodyRid>[$(RuntimeIdentifier)][$(Version)][$(PublishDir)]</FixtureBodyRid>");
				Assert.AreEqual("[][][]",fixtureFolder.Load("SdkLinks").GetProperty("FixtureBodyRid"));
				//After them (Directory.Build.targets is read after the common targets' properties): not known, so a condition on one throws.
				fixtureFolder.EditFile("Directory.Build.targets","</Project>","<PropertyGroup Condition=\"'$(Version)' != ''\"><FixtureLate>1</FixtureLate></PropertyGroup></Project>");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("Classic"),"$(Version)");
			}
		}

		[TestMethod]
		public void AWebApplicationsAppConfig_IsNotModelled_AndAnotherFileOfTheToolsThrows() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//OpenPmsDentalServer.csproj imports Microsoft.WebApplication.targets through $(VSToolsPath); MSBuild 18 gives it AppConfig Web.config
				//from there (S1-T2's check). That file is not read, so its AppConfig throws instead of reading as ''.
				const string anchor="<Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />";
				string textClassic=System.IO.File.ReadAllText(fixtureFolder.ProjectPath("Classic"));
				const string web="<PropertyGroup><VSToolsPath Condition=\"'$(VSToolsPath)' == ''\">$(MSBuildExtensionsPath32)\\Microsoft\\VisualStudio\\v$(VisualStudioVersion)"
					+"</VSToolsPath></PropertyGroup><Import Project=\"$(VSToolsPath)\\WebApplications\\Microsoft.WebApplication.targets\" Condition=\"'$(VSToolsPath)' != ''\" />";
				fixtureFolder.WriteFile(@"tests\Classic\Web.config","");
				fixtureFolder.EditProject("Classic",anchor,anchor+web);
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				AssertThrows<NotSupportedException>(() => projectFile.GetProperty("AppConfig"),"$(AppConfig)");
				AssertThrows<NotSupportedException>(() => projectFile.GetProperty("ProjectConfigFileName"),"$(ProjectConfigFileName)");
				Assert.AreEqual("ClassicFixture",projectFile.GetProperty("AssemblyName"),"The rest of the project still evaluates.");
				//A false condition imports nothing: the project's own AppConfig rules.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(anchor,anchor
					+"<Import Project=\"$(MSBuildExtensionsPath32)\\Microsoft\\VisualStudio\\v10.0\\WebApplications\\Microsoft.WebApplication.targets\" Condition=\"false\" />"));
				Assert.AreEqual("",fixtureFolder.Load("Classic").GetProperty("AppConfig"));
				//Any other file of MSBuild, Visual Studio or the SDK that a project imports throws: what it sets is not known.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(anchor,anchor
					+"<Import Project=\"$(MSBuildExtensionsPath)\\Microsoft\\VisualStudio\\v$(VisualStudioVersion)\\TeamTest\\Microsoft.TestTools.targets\" />"));
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("Classic"),"Microsoft.TestTools.targets");
				//The repository's web application.
				ProjectFile projectFileServer=ProjectFile.Load(System.IO.Path.Combine(FixtureFolder.RepoRoot(),"OpenPmsDentalServer","OpenPmsDentalServer.csproj"));
				Assert.IsFalse(projectFileServer.IsPropertyKnown("AppConfig"));
				Assert.AreEqual("OpenPmsDentalServer",projectFileServer.GetProperty("AssemblyName"));
			}
		}
		#endregion Not modelled
	}
}
