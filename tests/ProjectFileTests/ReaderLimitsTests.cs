// ProjectFile's queries and limits on the fixtures (package S1-T2 of the .NET 10 plan): GetItemsMentioning in both formats; GetFilesNotCompiled
// and GetFilesNotCompiledByWildcard, which never answer for a folder without source files; what it does not model (an unknown property
// function, a missing import, a Choose MSBuild rejects), which throws instead of evaluating on a guess; and an Update that writes the item type in
// another case. Since S2-3: Choose as MSBuild evaluates it, $(MSBuildToolsVersion) in a numeric comparison, and the SDK's runtime identifier.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	[TestClass]
	public class ReaderLimitsTests {
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

		#region GetItemsMentioning
		[TestMethod]
		public void GetItemsMentioning_FindsTheItemsWhoseIncludeNamesTheText_InBothFormats() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				foreach(string name in new[] { "Classic","SdkStyle" }) {
					ProjectFile projectFile=fixtureFolder.Load(name);
					CollectionAssert.AreEquivalent(new[] { @"Compile Forms\FormMain.cs",@"Compile Forms\FormMain.Designer.cs",@"EmbeddedResource Forms\FormMain.resx" },
						projectFile.GetItemsMentioning("formmain").Select(x => x.ToString()).ToArray(),name+": any case, every item type.");
					CollectionAssert.AreEquivalent(new[] { @"Forms\FormMain.cs",@"Forms\FormMain.Designer.cs" },
						projectFile.GetItemsMentioning("FormMain","Compile").Select(x => x.Include).ToArray(),name+": one item type.");
					Assert.AreEqual(1,projectFile.GetItemsMentioning("MySqlConnector","Reference","Compile").Count,name);
					Assert.AreEqual(0,projectFile.GetItemsMentioning("MySqlConnector","Compile").Count,name);
					Assert.AreEqual(0,projectFile.GetItemsMentioning("NotInTheFixture").Count,name);
				}
			}
		}

		[TestMethod]
		public void GetItemsMentioning_NoLongerFindsARemovedItem() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//The check "a removed library is gone from the project", in either format: the classic file without its line, the SDK-style one
				//with a Remove.
				fixtureFolder.EditProject("Classic","<Reference Include=\"MySqlConnector\">","<Reference Include=\"MySqlConnector\" Condition=\"false\">");
				Assert.AreEqual(0,fixtureFolder.Load("Classic").GetItemsMentioning("MySqlConnector").Count,"Its HintPath still names it, but no Include does.");
				fixtureFolder.EditProject("SdkStyle","<ProjectReference Include=\"..\\Classic\\Classic.csproj\" />",
					"<ProjectReference Include=\"..\\Classic\\Classic.csproj\" /><Reference Remove=\"MySqlConnector\" />");
				Assert.AreEqual(0,fixtureFolder.Load("SdkStyle").GetItemsMentioning("MySqlConnector").Count);
			}
		}
		#endregion GetItemsMentioning

		#region Folders without source files
		[TestMethod]
		public void GetFilesNotCompiled_ThrowsForAFolderWithoutSourceFiles() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				foreach(string name in new[] { "Classic","SdkStyle" }) {
					ProjectFile projectFile=fixtureFolder.Load(name);
					//Data holds Seed.txt only, Missing is not there: an empty answer would pass a check on nothing.
					AssertThrows<InvalidOperationException>(() => projectFile.GetFilesNotCompiled("Data"),"no .cs files under Data");
					AssertThrows<InvalidOperationException>(() => projectFile.GetFilesNotCompiled("Missing"),"no .cs files under Missing");
					AssertThrows<InvalidOperationException>(() => projectFile.GetFilesNotCompiledByWildcard("Data"),"no .cs files under Data");
				}
				//A folder whose only .cs file is in a bin folder below it counts as empty.
				fixtureFolder.WriteFile(@"tests\Classic\Data\bin\Debug\Generated.cs","");
				AssertThrows<InvalidOperationException>(() => fixtureFolder.Load("Classic").GetFilesNotCompiled("Data"),"no .cs files under Data");
			}
		}
		#endregion Folders without source files

		#region What is not modelled throws
		[TestMethod]
		public void Choose_TheFirstWhenThatIsTrue_ElseOtherwise_AsMSBuildEvaluatesIt() {
			//Since S2-3 (the WebView2 package's build\Common.targets has a Choose): the When elements in order until one is true, else the
			//Otherwise; its properties at once (a later property sees them, a nested Choose too), its items in pass 2; a When after the true one is
			//not evaluated at all (here a condition the reader could not evaluate).
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.EditProject("SdkStyle","</Project>","<Choose>"
					+"<When Condition=\"'$(Configuration)' == 'Release'\"><PropertyGroup><FixtureChosen>release</FixtureChosen></PropertyGroup>"
					+"<ItemGroup><Compile Remove=\"Program.cs\" /></ItemGroup></When>"
					+"<When Condition=\"'$(Configuration)' == 'Debug'\"><PropertyGroup><FixtureChosen>debug</FixtureChosen></PropertyGroup>"
					+"<Choose><When Condition=\"'$(FixtureChosen)' == 'debug'\"><PropertyGroup><FixtureNested>nested</FixtureNested></PropertyGroup></When></Choose>"
					+"</When>"
					+"<When Condition=\"'$([System.Guid]::NewGuid())' != ''\"><PropertyGroup><FixtureChosen>never</FixtureChosen></PropertyGroup></When>"
					+"<Otherwise><PropertyGroup><FixtureChosen>otherwise</FixtureChosen></PropertyGroup></Otherwise>"
					+"</Choose><PropertyGroup><FixtureAfter>$(FixtureChosen)</FixtureAfter></PropertyGroup></Project>");
				ProjectFile projectFileDebug=ProjectFile.Load(fixtureFolder.ProjectPath("SdkStyle"),"Debug","AnyCPU");
				Assert.AreEqual("debug",projectFileDebug.GetProperty("FixtureChosen"));
				Assert.AreEqual("debug",projectFileDebug.GetProperty("FixtureAfter"),"Its properties are set in pass 1, where the Choose stands.");
				Assert.AreEqual("nested",projectFileDebug.GetProperty("FixtureNested"));
				projectFileDebug.AssertCompiles("Program.cs");
				ProjectFile projectFileRelease=ProjectFile.Load(fixtureFolder.ProjectPath("SdkStyle"),"Release","AnyCPU");
				Assert.AreEqual("release",projectFileRelease.GetProperty("FixtureChosen"));
				Assert.AreEqual("",projectFileRelease.GetProperty("FixtureNested"),"The When not chosen is not read.");
				projectFileRelease.AssertDoesNotCompile("Program.cs");
				//No When true: the Otherwise; a Choose without one sets nothing. The Guid When is evaluated now, so it goes.
				fixtureFolder.EditProject("SdkStyle","<When Condition=\"'$([System.Guid]::NewGuid())' != ''\"><PropertyGroup><FixtureChosen>never</FixtureChosen></PropertyGroup></When>","");
				Assert.AreEqual("otherwise",ProjectFile.Load(fixtureFolder.ProjectPath("SdkStyle"),"Fixture","AnyCPU").GetProperty("FixtureChosen"));
				fixtureFolder.EditProject("SdkStyle","<Otherwise><PropertyGroup><FixtureChosen>otherwise</FixtureChosen></PropertyGroup></Otherwise>","");
				Assert.AreEqual("",ProjectFile.Load(fixtureFolder.ProjectPath("SdkStyle"),"Fixture","AnyCPU").GetProperty("FixtureChosen"));
			}
		}

		[TestMethod]
		public void Choose_WhatMSBuildRejects_Throws() {
			string[][] arrayCases=new[] {
				new[] { "<Choose Condition=\"true\"><When Condition=\"true\" /></Choose>","with a Condition" },
				new[] { "<Choose><Otherwise /></Choose>","<Otherwise> where MSBuild allows only When elements" },
				new[] { "<Choose></Choose>","without a When" },
				new[] { "<Choose><When><PropertyGroup /></When></Choose>","a When without a Condition" },
				new[] { "<Choose><When Condition=\"true\" /><Otherwise /><Otherwise /></Choose>","<Otherwise> where" },
				new[] { "<Choose><When Condition=\"true\" /><Otherwise /><When Condition=\"true\" /></Choose>","<When> where" },
				new[] { "<Choose><PropertyGroup /></Choose>","<PropertyGroup> where" },
				new[] { "<Choose><When Condition=\"true\"><Import Project=\"Other.props\" /></When></Choose>","<Import> in <When>" },
				new[] { "<Choose><When Condition=\"false\" /><Otherwise><Target Name=\"X\" /></Otherwise></Choose>","<Target> in <Otherwise>" },
			};
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				string text=File.ReadAllText(fixtureFolder.ProjectPath("SdkStyle"));
				foreach(string[] arrayCase in arrayCases) {
					fixtureFolder.WriteFile(@"tests\SdkStyle\SdkStyle.csproj",text.Replace("</Project>",arrayCase[0]+"</Project>"));
					AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),arrayCase[1]);
				}
			}
		}

		[TestMethod]
		public void MSBuildToolsVersion_ComparesAsTheVisualStudioVersion_OnlyWhereMSBuildDoesSo() {
			//MSBuild's StringExpressionNode: an operand of < > <= >= written exactly '$(MSBuildToolsVersion)' that expands to "Current" is the
			//Visual Studio version of the MSBuild that evaluates (18.0), so the WebView2 package's '$(MSBuildToolsVersion)' <= '15' is false (S2-3).
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				string text=File.ReadAllText(fixtureFolder.ProjectPath("SdkStyle"));
				fixtureFolder.WriteFile(@"tests\SdkStyle\SdkStyle.csproj",text.Replace("</Project>","<PropertyGroup>"
					+"<FixtureOld Condition=\"'$(MSBuildToolsVersion)' &lt;= '15'\">old</FixtureOld>"
					+"<FixtureNew Condition=\"'$(msbuildtoolsversion)' &gt;= '18.0' and $(MSBuildToolsVersion) &lt; 19\">new</FixtureNew>"
					+"<FixtureHolder>$(MSBuildToolsVersion)</FixtureHolder></PropertyGroup></Project>"));
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				Assert.AreEqual("",projectFile.GetProperty("FixtureOld"));
				Assert.AreEqual("new",projectFile.GetProperty("FixtureNew"),"Any case of the name, quoted or not.");
				Assert.AreEqual("Current",projectFile.GetProperty("MSBuildToolsVersion"),"The property itself stays Current.");
				//Only the operand written so: another property that holds "Current", or more text around it, is no number, as in MSBuild (MSB4086).
				foreach(string condition in new[] { "'$(FixtureHolder)' &lt;= '15'","'$(MSBuildToolsVersion).0' &lt;= '15'" }) {
					fixtureFolder.WriteFile(@"tests\SdkStyle\SdkStyle.csproj",text.Replace("</Project>","<PropertyGroup>"
						+"<FixtureHolder>$(MSBuildToolsVersion)</FixtureHolder><FixtureOld Condition=\""+condition+"\">old</FixtureOld></PropertyGroup></Project>"));
					AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"compares no numbers");
				}
			}
		}

		[TestMethod]
		public void NETCoreSdkRuntimeIdentifier_IsTheSdksOwn_ForAnSdkStyleProject_AndEmptyForAClassicOne() {
			//Microsoft.NETCoreSdk.BundledVersions.props sets it in Microsoft.NET.Sdk.props over anything the project says; the WebView2 package's
			//build\Common.targets tests it (S2-3). tools\Test-ProjectFileEvaluator.ps1 compares it with MSBuild's for every project.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				string expected=Environment.Is64BitOperatingSystem ? "win-x64" : "win-x86";
				Assert.AreEqual(expected,fixtureFolder.Load("SdkStyle").GetProperty("NETCoreSdkRuntimeIdentifier"));
				fixtureFolder.EditProject("SdkStyle","<OutputType>WinExe</OutputType>","<OutputType>WinExe</OutputType>"
					+"<FixtureRid>$(NETCoreSdkRuntimeIdentifier)</FixtureRid><NETCoreSdkRuntimeIdentifier>fixture-rid</NETCoreSdkRuntimeIdentifier>");
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				Assert.AreEqual(expected,projectFile.GetProperty("FixtureRid"),"The SDK's props come before the project's properties,");
				Assert.AreEqual("fixture-rid",projectFile.GetProperty("NETCoreSdkRuntimeIdentifier"),"which may set it again.");
				Assert.AreEqual("",fixtureFolder.Load("Classic").GetProperty("NETCoreSdkRuntimeIdentifier"));
			}
		}

		[TestMethod]
		public void AnSdkOtherThanMicrosoftNetSdk_Throws_InTheProjectAttributeAndAnSdkImport() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.EditProject("SdkStyle","<Project Sdk=\"Microsoft.NET.Sdk\">","<Project Sdk=\"Microsoft.NET.Sdk.Web\">");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"the SDK Microsoft.NET.Sdk.Web is not modelled");
				fixtureFolder.EditProject("SdkStyle","<Project Sdk=\"Microsoft.NET.Sdk.Web\">","<Project Sdk=\"Microsoft.NET.Sdk/10.0.114\">");
				Assert.IsTrue(fixtureFolder.Load("SdkStyle").IsSdkStyle,"Microsoft.NET.Sdk with a version is the SDK it models.");
				fixtureFolder.EditProject("SdkExplicitImports","<Import Project=\"Sdk.props\" Sdk=\"Microsoft.NET.Sdk\" />",
					"<Import Project=\"Sdk.props\" Sdk=\"Microsoft.NET.Sdk.Razor\" />");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkExplicitImports"),"the SDK Microsoft.NET.Sdk.Razor is not modelled");
			}
		}

		[TestMethod]
		public void AnUnknownPropertyFunction_Throws_InAConditionAnIncludeAndAStringMethod() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				string textClassic=File.ReadAllText(fixtureFolder.ProjectPath("Classic"));
				fixtureFolder.EditProject("Classic",ANCHOR_CLASSIC,"<PropertyGroup Condition=\"'$([System.Guid]::NewGuid())' != ''\"><FixtureX>1</FixtureX>"
					+"</PropertyGroup>"+ANCHOR_CLASSIC);
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("Classic"),"cannot evaluate $([System.Guid]::NewGuid())");
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(ANCHOR_CLASSIC,
					"<ItemGroup><Compile Include=\"$([System.DateTime]::Now.Year).cs\" /></ItemGroup>"+ANCHOR_CLASSIC));
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("Classic"),"cannot evaluate $([System.DateTime]::Now.Year)");
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(ANCHOR_CLASSIC,
					"<ItemGroup><Compile Include=\"$(MSBuildProjectName.Substring(1)).cs\" /></ItemGroup>"+ANCHOR_CLASSIC));
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("Classic"),"cannot evaluate $(MSBuildProjectName.Substring(1))");
				//The ones it knows evaluate.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(ANCHOR_CLASSIC,
					"<ItemGroup><Compile Include=\"$(MSBuildProjectName.Replace('Classic','Unlisted')).cs\" /></ItemGroup>"+ANCHOR_CLASSIC));
				fixtureFolder.Load("Classic").AssertCompiles("Unlisted.cs");
			}
		}

		[TestMethod]
		public void AMissingImport_Throws_UnlessItsConditionIsFalse() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				string textClassic=File.ReadAllText(fixtureFolder.ProjectPath("Classic"));
				fixtureFolder.EditProject("Classic",ANCHOR_CLASSIC,"<Import Project=\"..\\Missing.props\" />"+ANCHOR_CLASSIC);
				AssertThrows<FileNotFoundException>(() => fixtureFolder.Load("Classic"),"Missing.props");
				//MSBuild skips an import whose condition is false, so does ProjectFile.
				fixtureFolder.WriteFile(@"tests\Classic\Classic.csproj",textClassic.Replace(ANCHOR_CLASSIC,
					"<Import Project=\"..\\Missing.props\" Condition=\"Exists('..\\Missing.props')\" />"+ANCHOR_CLASSIC));
				fixtureFolder.Load("Classic").AssertCompiles("Program.cs");
				//Once it is there, it is read.
				fixtureFolder.WriteFile(@"tests\Missing.props","<Project><PropertyGroup><FixtureImported>yes</FixtureImported></PropertyGroup></Project>");
				Assert.AreEqual("yes",fixtureFolder.Load("Classic").GetProperty("FixtureImported"));
			}
		}
		#endregion What is not modelled throws

		#region Exists
		[TestMethod]
		public void Exists_InAnImportedFile_IsRelativeToThatFileForGroupsAndImports_AndToTheProjectOtherwise() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//MSBuild 18 (S1-T3's probe; Evaluator.GetCurrentDirectoryForConditionEvaluation): a PropertyGroup's, an Import's and an ImportGroup's
				//Exists('relative') is relative to the folder of the file it is in, every other condition's (a property, an ItemGroup, an item) to the
				//project's folder. Microsoft.WebApplication.targets relies on the second: AppConfig is Web.config when the project has one.
				fixtureFolder.WriteFile(@"marker-at-root.txt","");
				fixtureFolder.WriteFile(@"tests\Classic\marker-in-project.txt","");
				fixtureFolder.WriteFile("Imported.props","<Project><PropertyGroup><FixtureFromImport>yes</FixtureFromImport></PropertyGroup></Project>");
				fixtureFolder.EditFile("Directory.Build.props","</Project>",
					"<PropertyGroup Condition=\"Exists('marker-at-root.txt')\"><FixtureGroupRoot>yes</FixtureGroupRoot></PropertyGroup>"
					+"<PropertyGroup Condition=\"Exists('marker-in-project.txt')\"><FixtureGroupProject>yes</FixtureGroupProject></PropertyGroup>"
					+"<PropertyGroup><FixturePropertyRoot Condition=\"Exists('marker-at-root.txt')\">yes</FixturePropertyRoot>"
					+"<FixturePropertyProject Condition=\"Exists('marker-in-project.txt')\">yes</FixturePropertyProject></PropertyGroup>"
					+"<ItemGroup Condition=\"Exists('marker-in-project.txt')\"><FixtureGroupItem Include=\"x\" /></ItemGroup>"
					+"<ItemGroup><FixtureItemProject Include=\"x\" Condition=\"Exists('marker-in-project.txt')\" />"
					+"<FixtureItemRoot Include=\"x\" Condition=\"Exists('marker-at-root.txt')\" /></ItemGroup>"
					+"<Import Project=\"Imported.props\" Condition=\"Exists('marker-at-root.txt')\" /></Project>");
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("yes",projectFile.GetProperty("FixtureGroupRoot"),"A PropertyGroup: the file's folder.");
				Assert.AreEqual("",projectFile.GetProperty("FixtureGroupProject"));
				Assert.AreEqual("",projectFile.GetProperty("FixturePropertyRoot"),"A property: the project's folder.");
				Assert.AreEqual("yes",projectFile.GetProperty("FixturePropertyProject"));
				Assert.AreEqual(1,projectFile.GetItems("FixtureGroupItem").Count,"An ItemGroup: the project's folder.");
				Assert.AreEqual(1,projectFile.GetItems("FixtureItemProject").Count,"An item: the project's folder.");
				Assert.AreEqual(0,projectFile.GetItems("FixtureItemRoot").Count);
				Assert.AreEqual("yes",projectFile.GetProperty("FixtureFromImport"),"An Import: the file's folder.");
			}
		}
		#endregion Exists

		#region Update
		[TestMethod]
		public void Update_MatchesTheItemTypeIgnoringCase() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//MSBuild's item types ignore case: <compile Update> updates the Compile items, as <compile Include> and <compile Remove> do.
				fixtureFolder.EditProject("SdkStyle","<Compile Update=\"Forms\\FormMain.cs\" SubType=\"Form\" />","<compile Update=\"Forms\\FormMain.cs\" SubType=\"Form\" />");
				Assert.AreEqual("Form",fixtureFolder.Load("SdkStyle").AssertItem("Compile",@"Forms\FormMain.cs").GetMetadata("SubType"));
				fixtureFolder.EditProject("Classic",ANCHOR_CLASSIC,"<ItemGroup><COMPILE Update=\"Program.cs\" SubType=\"Code\" /></ItemGroup>"+ANCHOR_CLASSIC);
				ProjectFile projectFileClassic=fixtureFolder.Load("Classic");
				Assert.AreEqual("Code",projectFileClassic.AssertItem("Compile","Program.cs").GetMetadata("SubType"));
				Assert.IsFalse(projectFileClassic.AssertItem("Compile",@"Old\Keep.cs").HasMetadata("SubType"),"Only what it names.");
			}
		}

		[TestMethod]
		public void AnIncludeWithADoubledBackSlash_KeepsItsText_AndAnUpdateFindsItByItsPath() {
			//As MSBuild has it (S2-3): the WebView2 package's build\Common.targets includes $(MSBuildThisFileDirectory)\..\runtimes\..., whose
			//Include keeps "build\\.." (MSBuild -getItem prints it so), and Directory.Build.targets updates that file through another spelling.
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.EditProject("SdkStyle","</Project>","<ItemGroup><Content Include=\"$(MSBuildProjectDirectory)\\\\Data\\..\\Data\\Seed.txt\" "
					+"CopyToOutputDirectory=\"PreserveNewest\" /><Content Update=\"$(MSBuildProjectDirectory)\\Data\\Seed.txt\" Link=\"Seed.txt\" /></ItemGroup></Project>");
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				ProjectItem projectItem=projectFile.GetItems("Content").Single(x => x.Include.EndsWith("Seed.txt"));
				Assert.AreEqual(projectFile.Folder+@"\\Data\..\Data\Seed.txt",projectItem.Include,"The text as written, doubled back slash and all.");
				Assert.AreEqual(projectFile.Folder+@"\Data\Seed.txt",projectItem.FullPath);
				Assert.AreEqual("Seed.txt",projectItem.GetMetadata("Link"),"The Update found it by its full path.");
				Assert.AreEqual("PreserveNewest",projectItem.GetMetadata("CopyToOutputDirectory"));
			}
		}
		#endregion Update
	}
}
