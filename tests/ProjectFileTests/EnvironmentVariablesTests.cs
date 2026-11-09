// Environment variables where MSBuild's and the SDK's own files read a name that ProjectFile computes in code (package READER-ENV, follow-up of
// S2-1's second round: docs/port-notes/READER-ENV.md). MSBuild starts every evaluation with the environment variables as properties, so a
// name that no file set and that is not reserved, global or the toolset's is the variable's value wherever those files read it. ProjectFile does
// not model environment variables: each case sets one for the test process (ImportsTests.WithEnvironmentVariable, given back in a finally) and
// checks that the reader does not answer as if it were '' (what it did before this package, the value the comment names): a value MSBuild's
// files compute from it is not known (GetProperty throws), and an import or an item that depends on it throws at Load. The message names the
// variable. The expected values without a variable are MSBuild 18's for the same fixtures (S1-T2, S1-T3).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	[TestClass]
	public class EnvironmentVariablesTests {
		private const string ANCHOR_CLASSIC_TARGETS="<Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />";
		private const string ANCHOR_CLASSIC_PROPERTIES="<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>";
		private const string ANCHOR_CLASSIC_COMMON_PROPS="<Import Project=\"$(MSBuildExtensionsPath)\\$(MSBuildToolsVersion)\\Microsoft.Common.props\" "
			+"Condition=\"Exists('$(MSBuildExtensionsPath)\\$(MSBuildToolsVersion)\\Microsoft.Common.props')\" />";

		private static void AssertThrows<T>(Action action,string expectedText,string where) where T:Exception {
			try {
				action();
			}
			catch(T ex) {
				StringAssert.Contains(ex.Message,expectedText,where);
				return;
			}
			Assert.Fail(where+": no "+typeof(T).Name+" for "+expectedText);
		}

		///<summary>GetProperty throws for the name, naming the text (the variable it is not known from).</summary>
		private static void AssertNotKnown(ProjectFile projectFile,string name,string text,string where) {
			AssertThrows<NotSupportedException>(() => projectFile.GetProperty(name),text,where+": "+name);
			Assert.IsFalse(projectFile.IsPropertyKnown(name),where+": "+name);
		}

		private static void With(string name,string value,Action action) {
			ImportsTests.WithEnvironmentVariable(name,value,action);
		}

		///<summary>tests\Classic\Classic.csproj written again from Classic.csproj.fixture, with the properties added to its first PropertyGroup
		///(before Microsoft.CSharp.targets) and each pair of texts of arrayReplacements (old, new) replaced first.</summary>
		private static ProjectFile LoadClassic(FixtureFolder fixtureFolder,string properties,params string[] arrayReplacements) {
			const string ANCHOR="<ProjectGuid>";
			string path=fixtureFolder.ProjectPath("Classic");
			string text=File.ReadAllText(Path.Combine(FixtureFolder.RepoRoot(),"tests","ProjectFileTests","Fixtures","Classic.csproj.fixture"));
			for(int i=0;i<arrayReplacements.Length;i+=2) {
				Assert.IsTrue(text.Contains(arrayReplacements[i]),"Classic.csproj.fixture has no "+arrayReplacements[i]);
				text=text.Replace(arrayReplacements[i],arrayReplacements[i+1]);
			}
			Assert.IsTrue(text.Contains(ANCHOR));
			File.WriteAllText(path,text.Replace(ANCHOR,properties+ANCHOR));
			return ProjectFile.Load(path);
		}

		///<summary>An SDK-style project of its own with the properties and items, in folder (relative to the scratch folder: under tests\ it gets
		///tests\Directory.Build.props' TargetFramework net48 and AutoGenerateBindingRedirects; under tools\ none). The fixture's solution does
		///not list it, so it is evaluated for Debug|x86 without the solution's global properties.</summary>
		private static ProjectFile LoadSdk(FixtureFolder fixtureFolder,string folder,string properties,string items="") {
			string name=Path.GetFileName(folder);
			fixtureFolder.WriteFile(folder+@"\Program.cs","");
			fixtureFolder.WriteFile(folder+@"\"+name+".csproj","<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>"+properties+"</PropertyGroup><ItemGroup>"+items
				+"</ItemGroup></Project>");
			return ProjectFile.Load(fixtureFolder.Combine(folder+@"\"+name+".csproj"));
		}

		private static ProjectFile LoadTestsSdk(FixtureFolder fixtureFolder,string properties,string items="") {
			return LoadSdk(fixtureFolder,@"tests\EnvSdk",properties,items);
		}

		///<summary>tests\SdkLinks (explicit Sdk.props and Sdk.targets imports, no OutputType: a library) with the properties after Sdk.props.</summary>
		private static ProjectFile LoadSdkLinks(FixtureFolder fixtureFolder,string properties) {
			string path=fixtureFolder.ProjectPath("SdkLinks");
			string text=File.ReadAllText(Path.Combine(FixtureFolder.RepoRoot(),"tests","ProjectFileTests","Fixtures","SdkLinks.csproj.fixture"));
			const string ANCHOR="<FixtureIntermediateBeforeSdkTargets>";
			File.WriteAllText(path,text.Replace(ANCHOR,properties+ANCHOR));
			return ProjectFile.Load(path);
		}

		#region Microsoft.Common.props and Microsoft.Common.targets
		[TestMethod]
		public void TheCommonTargetsPresets_AsEnvironmentVariables_AreNotKnown() {
			//Microsoft.Common.CurrentVersion.targets lines 246, 256, 345, 348 set them only when they are ''; the toolset's VisualStudioVersion is
			//the environment variable's when there is one. The reader gave its own values over the variable.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				ProjectFile projectFile=LoadClassic(fixtureFolder,"");
				Assert.AreEqual(fixtureFolder.Combine(@"tests\Classic\Classic.csproj"),projectFile.GetProperty("ProjectPath"));
				Assert.AreEqual("18.0",projectFile.GetProperty("VisualStudioVersion"));
				With("ProjectDir",@"C:\Elsewhere\",() => {
					ProjectFile projectFileEnv=LoadClassic(fixtureFolder,"");
					AssertNotKnown(projectFileEnv,"ProjectDir","$(ProjectDir)","P1 ProjectDir");
					AssertNotKnown(projectFileEnv,"ProjectPath","$(ProjectDir)","P1 ProjectDir");
				});
				With("ProjectFileName","Other.csproj",() => {
					ProjectFile projectFileEnv=LoadClassic(fixtureFolder,"");
					AssertNotKnown(projectFileEnv,"ProjectFileName","$(ProjectFileName)","P2 ProjectFileName");
					AssertNotKnown(projectFileEnv,"ProjectPath","$(ProjectFileName)","P2 ProjectFileName");
				});
				With("ProjectPath",@"C:\Elsewhere\Other.csproj",() => AssertNotKnown(LoadClassic(fixtureFolder,""),"ProjectPath","$(ProjectPath)","P3 ProjectPath"));
				With("ProjectName","Other",() => AssertNotKnown(LoadClassic(fixtureFolder,""),"ProjectName","$(ProjectName)","P4 ProjectName"));
				With("VisualStudioVersion","17.0",() => AssertNotKnown(LoadClassic(fixtureFolder,""),"VisualStudioVersion","$(VisualStudioVersion)","P5 VisualStudioVersion"));
			}
		}

		[TestMethod]
		public void DirectoryBuildFileNames_AsEnvironmentVariables_ThrowWhereMicrosoftCommonReadsThem() {
			//Microsoft.Common.props lines 19-36 and Microsoft.Common.targets lines 45-62: the switch, the path the project names, the two parts of
			//the path MSBuild finds, and the Custom imports around it. Before this package the switch and the path read as '' (the file was left out,
			//or the one above was imported where MSBuild imports the variable's), the parts failed with an ArgumentException, and the Custom imports
			//were imported by MSBuild without a word.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.WriteFile(@"Shared\Other.props","<Project><PropertyGroup><FixtureOther>other</FixtureOther></PropertyGroup></Project>");
				string other=fixtureFolder.Combine(@"Shared\Other.props");
				foreach(string kind in new[] { "Props","Targets" }) {
					foreach(string[] arrayCase in new[] { new[] { "ImportDirectoryBuild"+kind,"true","D1 switch" },new[] { "DirectoryBuild"+kind+"Path",other,"D2 path" },
						new[] { "_DirectoryBuild"+kind+"File","Other.props","D3 file" },new[] { "_DirectoryBuild"+kind+"BasePath",fixtureFolder.Combine("Shared"),"D4 base path" },
						new[] { "CustomBeforeDirectoryBuild"+kind,other,"D5 Custom before" },new[] { "CustomAfterDirectoryBuild"+kind,other,"D6 Custom after" } })
					{
						With(arrayCase[0],arrayCase[1],() => AssertThrows<NotSupportedException>(() => LoadClassic(fixtureFolder,""),"$("+arrayCase[0]+")",arrayCase[2]+" "+kind));
					}
				}
				//With the switch off (a file sets it), MSBuild does not read the path: the variable decides nothing.
				With("DirectoryBuildPropsPath",other,() => {
					ProjectFile projectFile=LoadClassic(fixtureFolder,"",ANCHOR_CLASSIC_COMMON_PROPS,"<PropertyGroup><ImportDirectoryBuildProps>false</ImportDirectoryBuildProps>"
						+"</PropertyGroup>"+ANCHOR_CLASSIC_COMMON_PROPS);
					Assert.AreEqual("",projectFile.GetProperty("FixtureEveryProject"),"D2: not imported.");
					//A file that sets the switch decides it.
					projectFile=LoadClassic(fixtureFolder,"",ANCHOR_CLASSIC_COMMON_PROPS,"<PropertyGroup><ImportDirectoryBuildProps>true</ImportDirectoryBuildProps>"
						+"<DirectoryBuildPropsPath>"+other+"</DirectoryBuildPropsPath></PropertyGroup>"+ANCHOR_CLASSIC_COMMON_PROPS);
					Assert.AreEqual("other",projectFile.GetProperty("FixtureOther"),"D2: the path a file names.");
				});
				//The Custom import after it is read after Directory.Build.props and Directory.Build.targets, which may set it (D7).
				fixtureFolder.EditFile("Directory.Build.props","<FixtureEveryProject>root</FixtureEveryProject>","<FixtureEveryProject>root</FixtureEveryProject>"
					+"<CustomAfterDirectoryBuildProps>"+other+"</CustomAfterDirectoryBuildProps>");
				AssertThrows<NotSupportedException>(() => LoadClassic(fixtureFolder,""),"$(CustomAfterDirectoryBuildProps)","D7 props");
				fixtureFolder.EditFile("Directory.Build.props","<CustomAfterDirectoryBuildProps>"+other+"</CustomAfterDirectoryBuildProps>","");
				fixtureFolder.EditFile("Directory.Build.targets","</Project>","<PropertyGroup><CustomAfterDirectoryBuildTargets>"+other+"</CustomAfterDirectoryBuildTargets>"
					+"</PropertyGroup></Project>");
				AssertThrows<NotSupportedException>(() => LoadClassic(fixtureFolder,""),"$(CustomAfterDirectoryBuildTargets)","D7 targets");
			}
		}

		[TestMethod]
		public void TheImportsOfTheCommonFiles_AsEnvironmentVariables_Throw() {
			//The files MSBuild's common props and targets and the language targets import from a property, the switches between them, and
			//MSBuildIsRestoring. The reader did not read them: MSBuild imported another file, or none, without a word.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				string other=fixtureFolder.Combine(@"Shared\Other.props");
				fixtureFolder.WriteFile(@"Shared\Other.props","<Project />");
				foreach(string[] arrayCase in new[] {
					new[] { "CustomBeforeMicrosoftCommonProps",other,"(Microsoft.Common.props)","I1" },
					new[] { "CustomAfterMicrosoftCommonProps",other,"(Microsoft.Common.props)","I2" },
					new[] { "MSBuildUseVisualStudioDirectoryLayout","false","(Microsoft.Common.props)","I3" },
					new[] { "NuGetPropsFile",other,"(Microsoft.Common.props)","I4" },
					new[] { "IsCrossTargetingBuild","true","(Microsoft.CSharp.targets)","I5" },
					new[] { "CustomBeforeMicrosoftCSharpTargets",other,"(Microsoft.CSharp.CurrentVersion.targets)","I6" },
					new[] { "CustomAfterMicrosoftCSharpTargets",other,"(Microsoft.CSharp.CurrentVersion.targets)","I7" },
					new[] { "CustomBeforeMicrosoftCommonTargets",other,"(Microsoft.Common.CurrentVersion.targets)","I8" },
					new[] { "CustomAfterMicrosoftCommonTargets",other,"(Microsoft.Common.CurrentVersion.targets)","I9" } })
				{
					With(arrayCase[0],arrayCase[1],() => AssertThrows<NotSupportedException>(() => LoadClassic(fixtureFolder,""),"$("+arrayCase[0]+") "+arrayCase[2],arrayCase[3]));
				}
				//Microsoft.Common.CurrentVersion.targets line 18: a classic project without its own import of Microsoft.Common.props gets it there,
				//unless MicrosoftCommonPropsHasBeenImported is 'true'; the project's own import sets it.
				With("MicrosoftCommonPropsHasBeenImported","true",() => {
					AssertThrows<NotSupportedException>(() => LoadClassic(fixtureFolder,"",ANCHOR_CLASSIC_COMMON_PROPS,""),"$(MicrosoftCommonPropsHasBeenImported)","I10");
					Assert.AreEqual("root",LoadClassic(fixtureFolder,"").GetProperty("FixtureEveryProject"),"I10: the project's own import.");
				});
				//Microsoft.Common.props line 68 and Microsoft.Common.targets line 37, each read only when its switch is '' (a file sets the other).
				fixtureFolder.WriteFile(@"tests\Classic\obj\Classic.csproj.a.props","<Project><PropertyGroup><FixtureExtProps>a</FixtureExtProps></PropertyGroup></Project>");
				With("MSBuildIsRestoring","true",() => {
					fixtureFolder.EditFile("Directory.Build.props","<FixtureEveryProject>root</FixtureEveryProject>","<FixtureEveryProject>root</FixtureEveryProject>"
						+"<ImportProjectExtensionTargets>true</ImportProjectExtensionTargets>");
					AssertThrows<NotSupportedException>(() => LoadClassic(fixtureFolder,""),"$(MSBuildIsRestoring)","I11 props");
					fixtureFolder.EditFile("Directory.Build.props","<ImportProjectExtensionTargets>true</ImportProjectExtensionTargets>","");
					AssertThrows<NotSupportedException>(() => LoadClassic(fixtureFolder,"",ANCHOR_CLASSIC_COMMON_PROPS,"<PropertyGroup><ImportProjectExtensionProps>true"
						+"</ImportProjectExtensionProps></PropertyGroup>"+ANCHOR_CLASSIC_COMMON_PROPS),"$(MSBuildIsRestoring)","I12 targets");
				});
				//A file that sets it (as NuGet's restore does, a global property) turns the restore's files off, as MSBuild does.
				ProjectFile projectFileRestoring=LoadClassic(fixtureFolder,"",ANCHOR_CLASSIC_COMMON_PROPS,"<PropertyGroup><MSBuildIsRestoring>true</MSBuildIsRestoring>"
					+"</PropertyGroup>"+ANCHOR_CLASSIC_COMMON_PROPS);
				Assert.AreEqual("",projectFileRestoring.GetProperty("FixtureExtProps"));
				Assert.AreEqual("a",LoadClassic(fixtureFolder,"").GetProperty("FixtureExtProps"));
			}
		}

		[TestMethod]
		public void TheBuildExtensionsTargets_AsEnvironmentVariables_MakeTheVersionWithoutVNotKnown() {
			//Microsoft.Common.CurrentVersion.targets line 7142 imports the ImportAfter folder; its Microsoft.NET.Build.Extensions.targets imports
			//$(MicrosoftNETBuildExtensionsTargets), which sets _TargetFrameworkVersionWithoutV. The reader read the switch's variable as not 'true'
			//(the name left '') and did not read the path at all.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				Assert.AreEqual("4.8",LoadClassic(fixtureFolder,"").GetProperty("_TargetFrameworkVersionWithoutV"));
				With("ImportByWildcardAfterMicrosoftCommonTargets","true",() => AssertNotKnown(LoadClassic(fixtureFolder,""),"_TargetFrameworkVersionWithoutV",
					"from ImportByWildcardAfterMicrosoftCommonTargets","B1"));
				With("MicrosoftNETBuildExtensionsTargets",fixtureFolder.Combine("Other.targets"),() => AssertNotKnown(LoadClassic(fixtureFolder,""),
					"_TargetFrameworkVersionWithoutV","from MicrosoftNETBuildExtensionsTargets","B2"));
				//A file that names another file: not modelled either; the switch off: not set.
				AssertNotKnown(LoadClassic(fixtureFolder,"<MicrosoftNETBuildExtensionsTargets>Other.targets</MicrosoftNETBuildExtensionsTargets>"),"_TargetFrameworkVersionWithoutV",
					"$(MicrosoftNETBuildExtensionsTargets)","B2 file");
				Assert.AreEqual("",LoadClassic(fixtureFolder,"<ImportByWildcardAfterMicrosoftCommonTargets>false</ImportByWildcardAfterMicrosoftCommonTargets>")
					.GetProperty("_TargetFrameworkVersionWithoutV"));
			}
		}

		[TestMethod]
		public void TheCommonTargetsProperties_FromEnvironmentVariables_AreNotKnown() {
			//Microsoft.Common.CurrentVersion.targets: each name read where the file reads it; the value before this package in the comment.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				const string COMMON_ONLY="<Import Project=\"$(MSBuildToolsPath)\\Microsoft.Common.targets\" />";
				//Lines 61-71: TargetRuntime of a project without language targets (old: no default, TargetFrameworkIdentifier '').
				Assert.AreEqual(".NETFramework",LoadClassic(fixtureFolder,"",ANCHOR_CLASSIC_TARGETS,COMMON_ONLY).GetProperty("TargetFrameworkIdentifier"));
				With("TargetRuntime","Managed",() => AssertNotKnown(LoadClassic(fixtureFolder,"",ANCHOR_CLASSIC_TARGETS,COMMON_ONLY),"TargetFrameworkIdentifier",
					"from TargetRuntime","C1"));
				//Lines 80-92 with TargetRuntime Native (no defaults): the identifier and version as MSBuild reads them (old: moniker '', SubsystemVersion '').
				With("TargetFrameworkIdentifier",".NETFramework",() => {
					ProjectFile projectFile=LoadClassic(fixtureFolder,"<TargetRuntime>Native</TargetRuntime>",ANCHOR_CLASSIC_TARGETS,COMMON_ONLY);
					AssertNotKnown(projectFile,"TargetFrameworkMoniker","$(TargetFrameworkIdentifier)","C2 TargetFrameworkIdentifier");
					AssertNotKnown(projectFile,"SubsystemVersion","TargetFrameworkIdentifier","C3 TargetFrameworkIdentifier");
				});
				With("TargetFrameworkVersion","v4.8",() => {
					ProjectFile projectFile=LoadClassic(fixtureFolder,"<TargetRuntime>Native</TargetRuntime><TargetFrameworkIdentifier>.NETFramework</TargetFrameworkIdentifier>",
						"<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>","");
					AssertNotKnown(projectFile,"TargetFrameworkMoniker","$(TargetFrameworkVersion)","C2 TargetFrameworkVersion");
					AssertNotKnown(projectFile,"SubsystemVersion","TargetFrameworkVersion","C3 TargetFrameworkVersion");
				});
				//Lines 91-92 (old: .NETFramework,Version=v4.8 for both).
				With("TargetFrameworkMoniker",".NETFramework,Version=v4.7",() => AssertNotKnown(LoadClassic(fixtureFolder,""),"TargetFrameworkMoniker","$(TargetFrameworkMoniker)","C4"));
				With("TargetFrameworkProfile","Client",() => AssertNotKnown(LoadClassic(fixtureFolder,""),"TargetFrameworkMoniker","$(TargetFrameworkProfile)","C5"));
				//Lines 104-105 (old: Windows, 7.0).
				With("_EnableDefaultWindowsPlatform","false",() => {
					ProjectFile projectFile=LoadClassic(fixtureFolder,"");
					AssertNotKnown(projectFile,"TargetPlatformIdentifier","from _EnableDefaultWindowsPlatform","C6");
					AssertNotKnown(projectFile,"TargetPlatformVersion","from _EnableDefaultWindowsPlatform","C6");
				});
				//Line 114 (old: Windows,Version=7.0; with the defaults off, '').
				With("TargetPlatformMoniker","Windows,Version=10.0",() => AssertNotKnown(LoadClassic(fixtureFolder,""),"TargetPlatformMoniker","$(TargetPlatformMoniker)","C7"));
				With("TargetPlatformIdentifier","Windows",() => AssertNotKnown(LoadClassic(fixtureFolder,"<_EnableDefaultWindowsPlatform>false</_EnableDefaultWindowsPlatform>"
					+"<TargetPlatformVersion>7.0</TargetPlatformVersion>"),"TargetPlatformMoniker","$(TargetPlatformIdentifier)","C8 TargetPlatformIdentifier"));
				With("TargetPlatformVersion","7.0",() => AssertNotKnown(LoadClassic(fixtureFolder,"<_EnableDefaultWindowsPlatform>false</_EnableDefaultWindowsPlatform>"
					+"<TargetPlatformIdentifier>Windows</TargetPlatformIdentifier>"),"TargetPlatformMoniker","$(TargetPlatformVersion)","C8 TargetPlatformVersion"));
				//Lines 182-184 (old: WinExe).
				With("TargetType","library",() => AssertNotKnown(LoadClassic(fixtureFolder,""),"OutputType","$(TargetType)","C9"));
				//Line 186 (old: true, true, '').
				With("DebugSymbols","false",() => AssertNotKnown(LoadClassic(fixtureFolder,""),"DebugSymbols","$(DebugSymbols)","C10 DebugSymbols"));
				With("DebugType","full",() => Assert.AreEqual("",LoadClassic(fixtureFolder,"").GetProperty("DebugSymbols"),"C10 DebugType"));
				With("ConfigurationName","Debug",() => AssertNotKnown(LoadClassic(fixtureFolder,""),"DebugSymbols","from ConfigurationName","C11"));
				//Lines 230-262: the output file's extension and name (old: .exe and ClassicFixture.exe for TargetExt; for an OutputType not known,
				//TargetExt '', TargetName and TargetFileName the assembly name, GenerateBindingRedirectsOutputType and the WinMD name '').
				With("TargetExt",".dll",() => {
					ProjectFile projectFile=LoadClassic(fixtureFolder,"");
					AssertNotKnown(projectFile,"TargetExt","$(TargetExt)","C12");
					AssertNotKnown(projectFile,"TargetFileName","$(TargetExt)","C13");
				});
				With("OutputType","winmdobj",() => {
					ProjectFile projectFile=LoadSdkLinks(fixtureFolder,"");
					AssertNotKnown(projectFile,"TargetExt","from OutputType","C14");
					AssertNotKnown(projectFile,"TargetName","from OutputType","C15");
					AssertNotKnown(projectFile,"WinMDExpOutputWindowsMetadataFilename","from OutputType","C16");
					AssertNotKnown(projectFile,"TargetFileName","from OutputType","C16");
					AssertNotKnown(projectFile,"GenerateBindingRedirectsOutputType","from OutputType","C17");
				});
				//Line 253 for a WinMD (old: ClassicFixture, the assembly name; Classic, over the variable).
				With("RootNamespace","EnvNamespace",() => AssertNotKnown(LoadClassic(fixtureFolder,"","<RootNamespace>Classic</RootNamespace>","",
					"<OutputType>WinExe</OutputType>","<OutputType>winmdobj</OutputType>"),"TargetName","$(RootNamespace)","C18"));
				With("TargetName","EnvTarget",() => AssertNotKnown(LoadClassic(fixtureFolder,"","<OutputType>WinExe</OutputType>","<OutputType>winmdobj</OutputType>"),
					"TargetName","$(TargetName)","C19"));
				//Lines 240 and 250 (old: bin\Debug\, out\Classic\, out\, out\, out\).
				With("OutDirWasSpecified","true",() => AssertNotKnown(LoadClassic(fixtureFolder,"<GenerateProjectSpecificOutputFolder>true</GenerateProjectSpecificOutputFolder>"),
					"OutDir","OutDirWasSpecified","C20 OutDirWasSpecified"));
				With("OutDirWasSpecified","false",() => AssertNotKnown(LoadClassic(fixtureFolder,"<OutDir>out\\</OutDir>"
					+"<GenerateProjectSpecificOutputFolder>true</GenerateProjectSpecificOutputFolder>"),"OutDir","OutDirWasSpecified","C21 OutDirWasSpecified"));
				With("WindowsAppContainer","true",() => AssertNotKnown(LoadClassic(fixtureFolder,"<OutDir>out\\</OutDir>"),"OutDir","WindowsAppContainer","C22"));
				With("GenerateProjectSpecificOutputFolder","true",() => {
					AssertNotKnown(LoadClassic(fixtureFolder,"<OutDir>out\\</OutDir>"),"OutDir","GenerateProjectSpecificOutputFolder","C23 or");
					AssertNotKnown(LoadClassic(fixtureFolder,"<OutDir>out\\</OutDir><WindowsAppContainer>true</WindowsAppContainer>"),"OutDir",
						"GenerateProjectSpecificOutputFolder","C24 and");
				});
				Assert.AreEqual(@"out\Classic\",LoadClassic(fixtureFolder,"<OutDir>out\\</OutDir><GenerateProjectSpecificOutputFolder>true</GenerateProjectSpecificOutputFolder>")
					.GetProperty("OutDir"),"Without a variable.");
				//Line 379 (old: true over the variable; '' for the identifier and the version not known).
				const string BINDING_REDIRECTS="<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>";
				With("AutoGenerateBindingRedirects","false",() => AssertNotKnown(LoadClassic(fixtureFolder,"",BINDING_REDIRECTS,""),"AutoGenerateBindingRedirects",
					"$(AutoGenerateBindingRedirects)","C25"));
				With("TargetFrameworkIdentifier",".NETFramework",() => AssertNotKnown(LoadClassic(fixtureFolder,"",BINDING_REDIRECTS,""),"AutoGenerateBindingRedirects",
					"from TargetFrameworkIdentifier","C26"));
				With("TargetFrameworkVersion","v4.8",() => AssertNotKnown(LoadClassic(fixtureFolder,"",BINDING_REDIRECTS,"","<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>",""),
					"AutoGenerateBindingRedirects","from TargetFrameworkVersion","C27"));
				Assert.AreEqual("true",LoadClassic(fixtureFolder,"",BINDING_REDIRECTS,"").GetProperty("AutoGenerateBindingRedirects"),"Without a variable.");
				//Lines 514-515 (old: bin\Debug\app.publish\).
				With("PublishDir","out",() => AssertNotKnown(LoadClassic(fixtureFolder,""),"PublishDir","$(PublishDir)","C28"));
				//Lines 569-580 (old: CurrentRuntime; false).
				With("DisableOutOfProcTaskHost","true",() => Assert.AreEqual("",LoadClassic(fixtureFolder,"").GetProperty("TargetFrameworkAsMSBuildRuntime"),"C29"));
				With("PlatformTargetAsMSBuildArchitecture","x64",() => Assert.AreEqual("true",LoadClassic(fixtureFolder,"")
					.GetProperty("PlatformTargetAsMSBuildArchitectureExplicitlySet"),"C30"));
				//Lines 594-595 (old: '' for both).
				With("SignAssembly","true",() => AssertNotKnown(LoadClassic(fixtureFolder,"<DelaySign>true</DelaySign>"),"DelaySign","from SignAssembly","C31"));
				With("DelaySign","true",() => AssertNotKnown(LoadClassic(fixtureFolder,"<SignAssembly>true</SignAssembly>"),"DelaySign","$(DelaySign)","C32"));
				Assert.AreEqual("true",LoadClassic(fixtureFolder,"<SignAssembly>true</SignAssembly><DelaySign>true</DelaySign>").GetProperty("DelaySign"),"Without a variable.");
			}
		}

		[TestMethod]
		public void NuGetTargets_FromEnvironmentVariables_LeavesTheCentralSwitchNotKnown_UnlessAFalseOperandDecidesIt() {
			//Microsoft.Common.CurrentVersion.targets lines 7131-7138 import NuGet.targets unless IsRestoreTargetsFileLoaded is 'true', from
			//$(NuGetRestoreTargets) when a file or a variable names it; NuGet.targets line 65 sets _CentralPackageVersionsEnabled. Old: true for both.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.WriteFile("Directory.Packages.props","<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>"
					+"</PropertyGroup></Project>");
				Assert.AreEqual("true",fixtureFolder.Load("SdkStyle").GetProperty("_CentralPackageVersionsEnabled"));
				With("IsRestoreTargetsFileLoaded","true",() => AssertNotKnown(fixtureFolder.Load("SdkStyle"),"_CentralPackageVersionsEnabled",
					"from IsRestoreTargetsFileLoaded","N1"));
				With("NuGetRestoreTargets",fixtureFolder.Combine("Other.targets"),() => AssertNotKnown(fixtureFolder.Load("SdkStyle"),"_CentralPackageVersionsEnabled",
					"from NuGetRestoreTargets","N2"));
				//S2-1's review: a ManagePackageVersionsCentrally not known decides nothing when CentralPackageVersionsFileImported is known not to be
				//'true' (the import switched off): the AND is false either way, as MSBuild evaluates it (old: not known).
				fixtureFolder.EditFile(@"tests\Directory.Build.props","<Import Project=","<PropertyGroup><ImportDirectoryPackagesProps>false</ImportDirectoryPackagesProps>"
					+"</PropertyGroup><Import Project=");
				With("ManagePackageVersionsCentrally","true",() => Assert.AreEqual("",fixtureFolder.Load("SdkStyle").GetProperty("_CentralPackageVersionsEnabled"),"N3"));
			}
		}

		[TestMethod]
		public void TheFrameworkPropsDefaults_FromEnvironmentVariables_AreNotKnown() {
			//Microsoft.NETFramework.CurrentVersion.props lines 76-98 and Microsoft.NET.props lines 35-40. Old: Prefer32Bit true, SubsystemVersion
			//6.00 (6.02 over the variable), HighEntropyVA true.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				With("TargetingClr2Framework","true",() => {
					ProjectFile projectFile=LoadClassic(fixtureFolder,"");
					foreach(string name in new[] { "Prefer32Bit","SubsystemVersion","HighEntropyVA" }) {
						AssertNotKnown(projectFile,name,"from TargetingClr2Framework","F1");
					}
				});
				With("OutputType","Library",() => {
					ProjectFile projectFile=LoadClassic(fixtureFolder,"","<OutputType>WinExe</OutputType>","");
					AssertNotKnown(projectFile,"Prefer32Bit","from OutputType","F2");
					AssertNotKnown(projectFile,"SubsystemVersion","from OutputType","F3");
				});
				With("PreferNativeArm64","true",() => AssertNotKnown(LoadClassic(fixtureFolder,""),"Prefer32Bit","from PreferNativeArm64","F4"));
				With("PlatformTarget","ARM",() => AssertNotKnown(LoadClassic(fixtureFolder,""),"SubsystemVersion","from PlatformTarget","F5"));
				With("SubsystemVersion","5.01",() => AssertNotKnown(LoadClassic(fixtureFolder,"<PlatformTarget>ARM</PlatformTarget>"),"SubsystemVersion","$(SubsystemVersion)","F6"));
			}
		}
		#endregion Microsoft.Common.props and Microsoft.Common.targets

		#region The SDK's
		[TestMethod]
		public void TheImportsOfTheSdksFiles_AsEnvironmentVariables_Throw() {
			//Sdk.props, Sdk.targets, Microsoft.NET.Sdk.props and .targets, Microsoft.NET.Sdk.BeforeCommon.targets and
			//Microsoft.NET.TargetFrameworkInference.targets import a file a property names, or another file for a switch. Old: loaded.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				string other=fixtureFolder.Combine(@"Shared\Other.props");
				fixtureFolder.WriteFile(@"Shared\Other.props","<Project />");
				foreach(string[] arrayCase in new[] {
					new[] { "AlternateCommonProps",other,"(Sdk.props)","S1" },
					new[] { "AfterMicrosoftNetSdkProps",other,"(Microsoft.NET.Sdk.props)","S2" },
					new[] { "BeforeMicrosoftNETSdkTargets",other,"(Sdk.targets)","S3" },
					new[] { "IsCrossTargetingBuild","true","(Sdk.targets)","S4" },
					new[] { "LanguageTargets",other,"(Sdk.targets)","S5" },
					new[] { "AfterMicrosoftNETSdkTargets",other,"(Microsoft.NET.Sdk.targets)","S6" },
					new[] { "UsingMicrosoftNETSdkRazor","true","(Microsoft.NET.Sdk.BeforeCommon.targets)","S7" },
					new[] { "BeforeTargetFrameworkInferenceTargets",other,"(Microsoft.NET.TargetFrameworkInference.targets)","S8" },
					new[] { "AfterTargetFrameworkInferenceTargets",other,"(Microsoft.NET.TargetFrameworkInference.targets)","S9" },
					new[] { "WebPublishProfileFile",other,"(Microsoft.NET.Sdk.ImportPublishProfile.targets)","S10" },
					new[] { "PublishProfileFullPath",other,"(Microsoft.NET.Sdk.ImportPublishProfile.targets)","S11" },
					new[] { "PublishProfileName","Fixture","(Microsoft.NET.Sdk.ImportPublishProfile.targets)","S12" },
					new[] { "PublishProfile","Fixture","(Microsoft.NET.Sdk.ImportPublishProfile.targets)","S13" },
					new[] { "UseArtifactsOutput","true","","S14" },
					new[] { "UseArtifactsIntermediateOutput","true","","S15" } })
				{
					With(arrayCase[0],arrayCase[1],() => AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"$("+arrayCase[0]+") "+arrayCase[2],
						arrayCase[3]));
				}
				//A publish profile that is imported already (PublishProfileImported 'true'): not imported again.
				With("PublishProfile","Fixture",() => Assert.AreEqual("WinExe",LoadSdk(fixtureFolder,@"tests\EnvSdk","<OutputType>WinExe</OutputType>"
					+"<PublishProfileImported>true</PublishProfileImported>").GetProperty("OutputType"),"S13"));
			}
		}

		[TestMethod]
		public void TheSdksPropsDefaults_FromEnvironmentVariables_AreNotKnown() {
			//Microsoft.NET.Sdk.props lines 96-97 and Microsoft.NET.Sdk.CSharp.props lines 15-22. Old: portable, ;NU1605, TRACE;DEBUG.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				With("DebugSymbols","false",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,""),"DebugType","from DebugSymbols","K1"));
				With("WarningsAsErrors","CS0001",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,""),"WarningsAsErrors","$(WarningsAsErrors)","K2"));
				With("DefineConstants","ENV",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,""),"DefineConstants","$(DefineConstants)","K3"));
				Assert.AreEqual("TRACE;DEBUG",LoadTestsSdk(fixtureFolder,"").GetProperty("DefineConstants"),"Without a variable.");
			}
		}

		[TestMethod]
		public void TheTargetFramework_FromEnvironmentVariables_IsNotInferredAgain() {
			//Microsoft.NET.TargetFrameworkInference.targets lines 52-59: the identifier and version are inferred from TargetFramework unless both
			//are set, environment variables among them; MSBuild keeps them then. The reader inferred them again (old: .NETFramework, v4.8, 4.8,
			//bin\x86\Debug\net48\, Windows, ;NU1605, 4, CopyLocalLockFileAssemblies true, and the PackageReference items).
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				With("TargetFrameworkIdentifier",".NETCoreApp",() => With("TargetFrameworkVersion","v10.0",() => {
					ProjectFile projectFile=LoadTestsSdk(fixtureFolder,"");
					AssertNotKnown(projectFile,"TargetFrameworkIdentifier","$(TargetFrameworkIdentifier)","T1");
					AssertNotKnown(projectFile,"TargetFrameworkVersion","$(TargetFrameworkVersion)","T2");
					AssertNotKnown(projectFile,"_TargetFrameworkVersionWithoutV","$(TargetFrameworkVersion)","T3");
					AssertNotKnown(projectFile,"OutputPath","_UnsupportedTargetFrameworkError","T4");
					AssertNotKnown(projectFile,"TargetPlatformIdentifier","_EnableDefaultWindowsPlatform","T5");
					AssertNotKnown(projectFile,"WarningsAsErrors","_BinaryFormatterObsoleteAsError","T6");
					AssertNotKnown(projectFile,"WarningLevel","from TargetFrameworkIdentifier","T7");
					AssertNotKnown(projectFile,"CopyLocalLockFileAssemblies","$(TargetFrameworkIdentifier)","T8");
					AssertThrows<NotSupportedException>(() => projectFile.GetItems("PackageReference"),"$(TargetFrameworkIdentifier)","T9");
					Assert.AreEqual(1,projectFile.GetItems("Compile").Count,"The other items still answer.");
				}));
				//A TargetFramework or TargetFrameworks of a variable, for a project no file gives one (old: the common targets' .NETFramework; loaded).
				With("TargetFramework","net10.0",() => AssertNotKnown(LoadSdk(fixtureFolder,@"tools\EnvTool",""),"TargetFrameworkIdentifier","$(TargetFramework)","T10"));
				With("TargetFrameworks","net48;net10.0",() => AssertThrows<NotSupportedException>(() => LoadSdk(fixtureFolder,@"tools\EnvTool",""),"TargetFrameworks","T11"));
				Assert.AreEqual(".NETFramework",LoadSdk(fixtureFolder,@"tools\EnvTool","<TargetFramework>net48</TargetFramework>").GetProperty("TargetFrameworkIdentifier"));
			}
		}

		[TestMethod]
		public void TheVersionOfANetCoreProject_NotKnown_MakesWhatDependsOnItNotKnown() {
			//A .NET Core project that names its identifier itself and has no TargetFramework (so nothing is inferred), with TargetFrameworkVersion an
			//environment variable: _TargetFrameworkVersionWithoutV is not known (line 59), and so is what depends on it. Old: Windows (the default
			//platform), ProduceReferenceAssembly false, CopyLocalLockFileAssemblies true, ;NU1605, and loaded with the WindowsDesktop targets off.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				const string NETCORE="<TargetFramework></TargetFramework><TargetFrameworkIdentifier>.NETCoreApp</TargetFrameworkIdentifier><OutputType>Exe</OutputType>";
				With("TargetFrameworkVersion","v10.0",() => {
					ProjectFile projectFile=LoadTestsSdk(fixtureFolder,NETCORE);
					AssertNotKnown(projectFile,"TargetPlatformIdentifier","from TargetFrameworkVersion","V1");
					AssertNotKnown(projectFile,"ProduceReferenceAssembly","from TargetFrameworkVersion","V2");
					AssertNotKnown(projectFile,"CopyLocalLockFileAssemblies","from TargetFrameworkVersion","V3");
					AssertNotKnown(projectFile,"WarningsAsErrors","from TargetFrameworkVersion","V4");
					AssertThrows<NotSupportedException>(() => LoadTestsSdk(fixtureFolder,NETCORE+"<UseWPF>true</UseWPF>"),"$(TargetFrameworkVersion)","V5");
					AssertThrows<NotSupportedException>(() => LoadTestsSdk(fixtureFolder,NETCORE+"<UseWPF>true</UseWPF><ImportWindowsDesktopTargets>true</ImportWindowsDesktopTargets>"),
						"$(TargetFrameworkVersion)","V6");
				});
				ProjectFile projectFileKnown=LoadTestsSdk(fixtureFolder,NETCORE+"<TargetFrameworkVersion>v10.0</TargetFrameworkVersion>");
				Assert.AreEqual("",projectFileKnown.GetProperty("TargetPlatformIdentifier"));
				Assert.AreEqual("true",projectFileKnown.GetProperty("ProduceReferenceAssembly"));
			}
		}

		[TestMethod]
		public void TheSdksBeforeCommonProperties_FromEnvironmentVariables_AreNotKnown() {
			//Microsoft.NET.Sdk.BeforeCommon.targets, Microsoft.NET.TargetFrameworkInference.targets and Microsoft.NET.DefaultOutputPaths.targets,
			//before the common targets. The old values in the comments.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//BeforeCommon lines 24-28 (old: '' for each).
				With("OutputType","Exe",() => AssertNotKnown(LoadSdkLinks(fixtureFolder,""),"HasRuntimeOutput","from OutputType","E1"));
				With("_IsExecutable","true",() => AssertNotKnown(LoadSdkLinks(fixtureFolder,""),"HasRuntimeOutput","$(_IsExecutable)","E2"));
				With("HasRuntimeOutput","true",() => AssertNotKnown(LoadSdkLinks(fixtureFolder,""),"HasRuntimeOutput","$(HasRuntimeOutput)","E3"));
				//TargetFrameworkInference lines 93-95 with DefaultOutputPaths lines 140-147 (old: bin\x86\Debug\net48\).
				With("_UnsupportedTargetFrameworkError","true",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,""),"OutputPath","from _UnsupportedTargetFrameworkError","E4"));
				//TargetFrameworkInference line 215 (old: '' where MSBuild gives the common targets' Windows).
				With("Language","C++",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,"<TargetFramework>net10.0</TargetFramework>"),"TargetPlatformIdentifier","Language","E5"));
				With("_EnablePackageReferencesInVCProjects","false",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,"<TargetFramework>net10.0</TargetFramework>"
					+"<Language>C++</Language>"),"TargetPlatformIdentifier","_EnablePackageReferencesInVCProjects","E6"));
				Assert.AreEqual("",LoadTestsSdk(fixtureFolder,"<TargetFramework>net10.0</TargetFramework>").GetProperty("TargetPlatformIdentifier"),"Without a variable.");
				//TargetFrameworkInference lines 281-282 (old: true for both).
				With("ProduceOnlyReferenceAssembly","true",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,"<TargetFramework>net10.0</TargetFramework>"),
					"ProduceReferenceAssembly","from ProduceOnlyReferenceAssembly","E7"));
				With("ProduceReferenceAssembly","false",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,"<TargetFramework>net10.0</TargetFramework>"),
					"ProduceReferenceAssembly","$(ProduceReferenceAssembly)","E8"));
				//DefaultOutputPaths lines 25-28 and 101-114: the output paths are in DefaultItemExcludes (lines 125-127), so the default items are not
				//known either and Load throws (old: bin\Debug\net48\ where MSBuild has bin\x86\Debug\net48\; AppendPlatformToOutputPath true;
				//bin\Debug\net48\ without the variable's folder).
				With("AppendPlatformToOutputPath","true",() => AssertThrows<NotSupportedException>(() => LoadSdkLinks(fixtureFolder,""),"from AppendPlatformToOutputPath","E9"));
				With("PlatformName","AnyCPU",() => AssertThrows<NotSupportedException>(() => LoadSdkLinks(fixtureFolder,""),"from PlatformName","E10"));
				With("_PlatformToAppendToOutputPath",@"Env\",() => AssertThrows<NotSupportedException>(() => LoadSdkLinks(fixtureFolder,
					"<AppendPlatformToOutputPath>false</AppendPlatformToOutputPath>"),"$(_PlatformToAppendToOutputPath)","E11"));
				//Lines 140-147, after DefaultItemExcludes (old: bin\x86\Debug\).
				With("AppendTargetFrameworkToOutputPath","true",() => AssertNotKnown(LoadSdkLinks(fixtureFolder,""),"OutputPath","AppendTargetFrameworkToOutputPath","E13"));
				//RuntimeIdentifierInference (old: bin\x86\Debug\net48\).
				With("RuntimeIdentifier","win-x86",() => AssertNotKnown(LoadSdkLinks(fixtureFolder,""),"OutputPath","RuntimeIdentifier","E14"));
				//BeforeCommon lines 102-104, for .NET Framework 4.6.2, below the common targets' 4.7.2 (old: '').
				With("HasRuntimeOutput","true",() => AssertNotKnown(LoadSdkLinks(fixtureFolder,"<TargetFramework>net462</TargetFramework>"
					+"<AutoGenerateBindingRedirects></AutoGenerateBindingRedirects>"),"AutoGenerateBindingRedirects","from HasRuntimeOutput","E15"));
				//BeforeCommon lines 301-313 (old: '', GenerateDocumentationFile false and DocumentationFile '', and the SDK's own path).
				With("GenerateDocumentationFile","true",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,""),"DocumentationFile","from GenerateDocumentationFile","E16"));
				With("DocumentationFile","env.xml",() => {
					ProjectFile projectFile=LoadTestsSdk(fixtureFolder,"");
					Assert.AreEqual("true",projectFile.GetProperty("GenerateDocumentationFile"),"E17");
					AssertNotKnown(projectFile,"DocumentationFile","$(DocumentationFile)","E17");
					AssertNotKnown(LoadTestsSdk(fixtureFolder,"<GenerateDocumentationFile>true</GenerateDocumentationFile>"),"DocumentationFile","$(DocumentationFile)","E18");
				});
			}
		}

		[TestMethod]
		public void TheSdksTargetsProperties_FromEnvironmentVariables_AreNotKnown() {
			//Microsoft.PackageDependencyResolution.targets, Microsoft.NET.Sdk.targets, Microsoft.NET.Sdk.CSharp.targets and
			//Microsoft.NET.Sdk.Analyzers.targets, after the common targets. The old values in the comments.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				const string NET10="<TargetFramework>net10.0</TargetFramework>";
				//Lines 31-42 (old: true, false, false).
				With("CopyLocalLockFileAssemblies","false",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,""),"CopyLocalLockFileAssemblies","$(CopyLocalLockFileAssemblies)","G1"));
				With("EnableDynamicLoading","true",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,NET10),"CopyLocalLockFileAssemblies","EnableDynamicLoading","G2"));
				With("HasRuntimeOutput","true",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,NET10),"CopyLocalLockFileAssemblies","HasRuntimeOutput","G3"));
				//Microsoft.NET.Sdk.targets lines 189-201, with the WindowsDesktop targets switched off by a file, as they read UseWPF and
				//UseWindowsForms too (old: ;NU1605;SYSLIB0011, false, false, false).
				const string NO_WINDOWS_DESKTOP="<ImportWindowsDesktopTargets>false</ImportWindowsDesktopTargets>";
				With("EnableUnsafeBinaryFormatterSerialization","true",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,NET10),"WarningsAsErrors",
					"EnableUnsafeBinaryFormatterSerialization","G4"));
				foreach(string name in new[] { "UseWindowsForms","UseWPF" }) {
					With(name,"true",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,"<TargetFramework>net8.0</TargetFramework>"+NO_WINDOWS_DESKTOP),
						"EnableUnsafeBinaryFormatterSerialization",name,"G5 "+name));
				}
				With("_ProjectTypeRequiresBinaryFormatter","true",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,"<TargetFramework>net9.0</TargetFramework>"),
					"EnableUnsafeBinaryFormatterSerialization","_ProjectTypeRequiresBinaryFormatter","G6"));
				Assert.AreEqual("false",LoadTestsSdk(fixtureFolder,"<TargetFramework>net9.0</TargetFramework>").GetProperty("EnableUnsafeBinaryFormatterSerialization"));
				//Microsoft.NET.Sdk.CSharp.targets lines 16-31 and 45 (old: TRACE;DEBUG and DEBUG, ;NU1605).
				With("DisableImplicitConfigurationDefines","true",() => {
					ProjectFile projectFile=LoadTestsSdk(fixtureFolder,"");
					AssertNotKnown(projectFile,"DefineConstants","from DisableImplicitConfigurationDefines","G7");
					AssertNotKnown(projectFile,"ImplicitConfigurationDefine","from DisableImplicitConfigurationDefines","G7");
				});
				With("_BinaryFormatterObsoleteAsError","true",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,""),"WarningsAsErrors","from _BinaryFormatterObsoleteAsError","G8"));
				//Microsoft.NET.Sdk.Analyzers.targets lines 61-65 (old: 4 for both).
				With("AnalysisLevel","preview",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,""),"WarningLevel","from AnalysisLevel","G9"));
				With("WarningLevel","0",() => AssertNotKnown(LoadTestsSdk(fixtureFolder,""),"WarningLevel","$(WarningLevel)","G10"));
			}
		}

		[TestMethod]
		public void TheWindowsDesktopNames_AsEnvironmentVariables_Throw() {
			//Microsoft.NET.Sdk.targets lines 1468-1473 import the WindowsDesktop targets, whose _EnableWindowsDesktopGlobbing (line 56) and switches
			//(lines 5-7) decide the WPF items of Microsoft.NET.Sdk.WindowsDesktop.props, which every SDK-style project imports. An import and items,
			//so a name not known throws at Load. Old: loaded (without the WPF items, or with them where MSBuild has none, or with a XamlRuntime of
			//the marker's text).
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				With("UseWPF","true",() => AssertThrows<NotSupportedException>(() => LoadTestsSdk(fixtureFolder,""),"$(UseWPF)","W1"));
				With("UseWindowsForms","true",() => AssertThrows<NotSupportedException>(() => LoadTestsSdk(fixtureFolder,"<UseWPF>false</UseWPF>"),"$(UseWindowsForms)","W2"));
				With("TargetFrameworkIdentifier",".NETFramework",() => With("TargetFrameworkVersion","v4.8",() => AssertThrows<NotSupportedException>(
					() => LoadTestsSdk(fixtureFolder,"<UseWPF>true</UseWPF>"),"$(TargetFrameworkIdentifier)","W3")));
				With("TargetPlatformIdentifier","Windows",() => AssertThrows<NotSupportedException>(() => LoadTestsSdk(fixtureFolder,"<TargetFramework>net10.0</TargetFramework>"
					+"<UseWPF>true</UseWPF>"),"$(TargetPlatformIdentifier)","W4"));
				With("ImportWindowsDesktopTargets","false",() => AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"$(ImportWindowsDesktopTargets)","W5"));
				With("_EnableWindowsDesktopGlobbing","true",() => AssertThrows<NotSupportedException>(() => LoadSdkLinks(fixtureFolder,""),"$(_EnableWindowsDesktopGlobbing)","W6"));
				With("UseWPF","true",() => AssertThrows<NotSupportedException>(() => LoadSdkLinks(fixtureFolder,"<ImportWindowsDesktopTargets>true</ImportWindowsDesktopTargets>"),
					"$(UseWPF)","W7"));
				With("EnableDefaultPageItems","false",() => AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"$(EnableDefaultPageItems)","W8"));
				With("EnableDefaultApplicationDefinition","false",() => AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),
					"$(EnableDefaultApplicationDefinition)","W9"));
				With("DefaultXamlRuntime","Maui",() => AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"$(DefaultXamlRuntime)","W10"));
				//Without a variable: the WPF items, and none for a project without the WindowsDesktop targets.
				Assert.IsTrue(fixtureFolder.Load("SdkStyle").HasItem("Page",@"Frms\FrmPrint.xaml"));
				Assert.AreEqual(0,LoadSdkLinks(fixtureFolder,"").GetItems("Page").Count);
			}
		}

		[TestMethod]
		public void TheDefaultItemSwitches_AsEnvironmentVariables_Throw() {
			//Microsoft.NET.Sdk.DefaultItems.props lines 35-45 and .targets lines 18-27, 51 and 82: the default items and their excludes, and the
			//automatic Link. Items, so a switch not known throws at Load. Old: loaded (without the default items for the switches, with them for
			//DisableDefaultItemsInProjectFolder, without the excludes, with the Link).
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				foreach(string[] arrayCase in new[] { new[] { "EnableDefaultItems","true","$(EnableDefaultItems)","X1" },
					new[] { "EnableDefaultCompileItems","true","$(EnableDefaultCompileItems)","X2" },
					new[] { "EnableDefaultEmbeddedResourceItems","true","$(EnableDefaultEmbeddedResourceItems)","X3" },
					new[] { "EnableDefaultNoneItems","true","$(EnableDefaultNoneItems)","X4" },
					new[] { "DisableDefaultItemsInProjectFolder","true","DisableDefaultItemsInProjectFolder","X5" },
					new[] { "DefaultItemExcludes","Program.cs","$(DefaultItemExcludes)","X6" },
					new[] { "DefaultItemExcludesInProjectFolder","Program.cs","$(DefaultItemExcludesInProjectFolder)","X7" },
					new[] { "DefaultExcludesInProjectFolder","Program.cs","$(DefaultExcludesInProjectFolder)","X8" } })
				{
					With(arrayCase[0],arrayCase[1],() => AssertThrows<NotSupportedException>(() => LoadTestsSdk(fixtureFolder,""),arrayCase[2],arrayCase[3]));
				}
				With("SetLinkMetadataAutomatically","false",() => AssertThrows<NotSupportedException>(() => LoadSdkLinks(fixtureFolder,""),"$(SetLinkMetadataAutomatically)","X9"));
				//A file that sets the switch decides it; without a variable, the default items.
				With("DisableDefaultItemsInProjectFolder","true",() => Assert.AreEqual(1,LoadTestsSdk(fixtureFolder,"<EnableDefaultItems>true</EnableDefaultItems>")
					.GetItems("Compile").Count,"X5"));
				ProjectFile projectFileDisabled=LoadTestsSdk(fixtureFolder,"<DisableDefaultItemsInProjectFolder>true</DisableDefaultItemsInProjectFolder>");
				Assert.AreEqual("false",projectFileDisabled.GetProperty("EnableDefaultItems"),"The project's folder holds the project.");
				Assert.AreEqual(0,projectFileDisabled.GetItems("Compile").Count);
			}
		}
		#endregion The SDK's
	}
}
