// ProjectFile's queries and limits on the fixtures (package S1-T2 of the .NET 10 plan): GetItemsMentioning in both formats; GetFilesNotCompiled
// and GetFilesNotCompiledByWildcard, which never answer for a folder without source files; what it does not model (Choose, an unknown property
// function, a missing import), which throws instead of evaluating on a guess; and an Update that writes the item type in another case.
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
		public void Choose_Throws() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.EditProject("SdkStyle","</Project>","<Choose><When Condition=\"'$(Configuration)' == 'Debug'\"><ItemGroup>"
					+"<Compile Remove=\"Program.cs\" /></ItemGroup></When></Choose></Project>");
				AssertThrows<NotSupportedException>(() => fixtureFolder.Load("SdkStyle"),"Choose is not modelled");
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
		#endregion Update
	}
}
