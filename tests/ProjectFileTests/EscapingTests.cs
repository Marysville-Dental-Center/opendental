// MSBuild's %XX escapes as ProjectFile evaluates them (package S1-T3 of the .NET 10 plan, docs/specs/net10-plan.md): MSBuild keeps property values
// and metadata escaped while it evaluates and unescapes them where it uses them, so GetProperty and the items give what MSBuild -getProperty and
// -getItem print (ApplicationVersion 1.0.0.%2a is 1.0.0.*, a BootstrapperPackage's ProductName ".NET Framework 2.0 %28x86%29" is "(x86)", a
// WebReferenceUrl's %3f is a '?'), while an escaped ';' or '*' stays part of a name instead of splitting a list or matching files. Checked
// against MSBuild 18 -getProperty and -getItem on the same text (S1-T3's probe), and on the repository's own projects.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	[TestClass]
	public class EscapingTests {
		private const string ANCHOR_CLASSIC="<Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />";

		[TestMethod]
		public void APropertyValue_IsUnescaped_AsMSBuildPrintsIt() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.EditProject("Classic","<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>",
					"<AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects><ApplicationVersion>1.0.0.%2a</ApplicationVersion>"
					+"<FixtureCopy>$(ApplicationVersion)</FixtureCopy><FixtureUrl>https://example.test/service.asmx%3fwsdl</FixtureUrl>"
					+"<FixturePercent>100%</FixturePercent><FixtureNotHex>%zz%4</FixtureNotHex>");
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("1.0.0.*",projectFile.GetProperty("ApplicationVersion"));
				Assert.AreEqual("1.0.0.*",projectFile.GetProperty("FixtureCopy"),"Through another property.");
				Assert.AreEqual("https://example.test/service.asmx?wsdl",projectFile.GetProperty("FixtureUrl"));
				//Only a '%' with two hexadecimal digits is an escape.
				Assert.AreEqual("100%",projectFile.GetProperty("FixturePercent"));
				Assert.AreEqual("%zz%4",projectFile.GetProperty("FixtureNotHex"));
			}
		}

		[TestMethod]
		public void ACondition_ComparesTheValuesUnescaped() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//MSBuild 18: both conditions are true, as the operands are unescaped before they are compared.
				fixtureFolder.EditProject("Classic",ANCHOR_CLASSIC,"<PropertyGroup><FixtureVersion>1.0.0.%2a</FixtureVersion></PropertyGroup>"
					+"<PropertyGroup Condition=\"'$(FixtureVersion)' == '1.0.0.*'\"><FixtureUnescaped>yes</FixtureUnescaped></PropertyGroup>"
					+"<PropertyGroup Condition=\"'$(FixtureVersion)' == '1.0.0.%2a'\"><FixtureEscaped>yes</FixtureEscaped></PropertyGroup>"
					+"<PropertyGroup Condition=\"'$(FixtureVersion)' == '1.0.0.%2b'\"><FixtureOther>yes</FixtureOther></PropertyGroup>"+ANCHOR_CLASSIC);
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("yes",projectFile.GetProperty("FixtureUnescaped"));
				Assert.AreEqual("yes",projectFile.GetProperty("FixtureEscaped"));
				Assert.AreEqual("",projectFile.GetProperty("FixtureOther"),"A different character is still different.");
			}
		}

		[TestMethod]
		public void AStringMethod_WorksOnTheUnescapedValue_AndItsResultStaysEscaped() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//MSBuild 18: $(V.EndsWith('*')) is True for 1.0.0.%2a, and $(V.Replace('.','-')) as an Include is one item 1-0-0-*, not a wildcard.
				fixtureFolder.EditProject("Classic",ANCHOR_CLASSIC,"<PropertyGroup><FixtureVersion>1.0.0.%2a</FixtureVersion>"
					+"<FixtureEndsStar>$(FixtureVersion.EndsWith('*'))</FixtureEndsStar><FixtureReplaced>$(FixtureVersion.Replace('.','-'))</FixtureReplaced>"
					+"</PropertyGroup><ItemGroup><FixtureItem Include=\"$(FixtureReplaced)\" /></ItemGroup>"+ANCHOR_CLASSIC);
				fixtureFolder.WriteFile(@"tests\Classic\1-0-0-x","");
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual("True",projectFile.GetProperty("FixtureEndsStar"),"A bool as MSBuild writes it.");
				Assert.AreEqual("1-0-0-*",projectFile.GetProperty("FixtureReplaced"));
				CollectionAssert.AreEqual(new[] { "1-0-0-*" },projectFile.GetItems("FixtureItem").Select(x => x.Include).ToArray(),
					"The escaped '*' of the result names no files.");
			}
		}

		[TestMethod]
		public void AnInclude_IsSplitAndGlobbedEscaped_ThenUnescaped() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//MSBuild 18 -getItem: Include="a%3bb;c" is two items, a;b and c; Include="%2a.txt" is one item *.txt; a ';' escaped in a property
				//stays one item; a '*' as written is a wildcard.
				fixtureFolder.EditProject("Classic",ANCHOR_CLASSIC,"<PropertyGroup><FixtureList>a%3bb</FixtureList></PropertyGroup><ItemGroup>"
					+"<FixtureSplit Include=\"a%3bb;c\" /><FixtureStar Include=\"%2a.txt\" /><FixtureFromProperty Include=\"$(FixtureList)\" />"
					+"<FixtureGlob Include=\"Data\\*.txt\" /></ItemGroup>"+ANCHOR_CLASSIC);
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				CollectionAssert.AreEqual(new[] { "a;b","c" },projectFile.GetItems("FixtureSplit").Select(x => x.Include).ToArray());
				CollectionAssert.AreEqual(new[] { "*.txt" },projectFile.GetItems("FixtureStar").Select(x => x.Include).ToArray());
				CollectionAssert.AreEqual(new[] { "a;b" },projectFile.GetItems("FixtureFromProperty").Select(x => x.Include).ToArray());
				CollectionAssert.AreEqual(new[] { @"Data\Seed.txt" },projectFile.GetItems("FixtureGlob").Select(x => x.Include).ToArray());
				Assert.IsTrue(projectFile.GetItems("FixtureGlob")[0].IsFromWildcard);
				Assert.IsFalse(projectFile.GetItems("FixtureStar")[0].IsFromWildcard);
			}
		}

		[TestMethod]
		public void Metadata_IsUnescaped_AsMSBuildPrintsIt() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//As OpenPmsDental.csproj and SparksToothChart.csproj write a BootstrapperPackage's ProductName and a WebReferenceUrl.
				fixtureFolder.EditProject("Classic",ANCHOR_CLASSIC,"<PropertyGroup><FixtureVersion>1.0.0.%2a</FixtureVersion></PropertyGroup><ItemGroup>"
					+"<BootstrapperPackage Include=\"Microsoft.Net.Framework.2.0\"><ProductName>.NET Framework 2.0 %28x86%29</ProductName></BootstrapperPackage>"
					+"<WebReferenceUrl Include=\"https://example.test/Service.asmx%3fwsdl\"><UpdateFromURL>https://example.test/Service.asmx%3fwsdl</UpdateFromURL>"
					+"</WebReferenceUrl><None Include=\"Data\\Seed.txt\" Version=\"$(FixtureVersion)\" Link=\"Copy%28%(Filename)%29%(Extension)\" /></ItemGroup>"
					+ANCHOR_CLASSIC);
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.AreEqual(".NET Framework 2.0 (x86)",projectFile.AssertItem("BootstrapperPackage","Microsoft.Net.Framework.2.0").GetMetadata("ProductName"));
				ProjectItem projectItemUrl=projectFile.GetItems("WebReferenceUrl").Single();
				StringAssert.EndsWith(projectItemUrl.Include,"Service.asmx?wsdl","The Include, whose slashes ProjectFile writes as back slashes.");
				Assert.AreEqual("https://example.test/Service.asmx?wsdl",projectItemUrl.GetMetadata("UpdateFromURL"));
				ProjectItem projectItemNone=projectFile.GetItems("None").Single(x => x.HasMetadata("Version"));
				Assert.AreEqual("1.0.0.*",projectItemNone.GetMetadata("Version"));
				Assert.AreEqual("Copy(Seed).txt",projectItemNone.GetMetadata("Link"),"The item's own %(Filename) between escaped parentheses.");
			}
		}

		[TestMethod]
		public void AnEscapedPathInARemove_MatchesTheFileItNames() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//Remove and Update compare the unescaped path: %5c is a back slash.
				fixtureFolder.EditProject("SdkStyle","<Compile Remove=\"Removed\\**\" />","<Compile Remove=\"Removed\\**\" /><Compile Remove=\"Forms%5cFormMain.Designer.cs\" />");
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				projectFile.AssertDoesNotCompile(@"Forms\FormMain.Designer.cs");
				projectFile.AssertCompiles(@"Forms\FormMain.cs");
			}
		}

		[TestMethod]
		public void RepositoryProjects_GetTheirEscapedValuesUnescaped() {
			string root=FixtureFolder.RepoRoot();
			//The values the S1-T2 check found wrong: MSBuild 18 -getProperty gives 1.0.0.* and -getItem ".NET Framework 2.0 (x86)".
			ProjectFile projectFile=ProjectFile.Load(Path.Combine(root,"OpenPmsDental","OpenPmsDental.csproj"));
			StringAssert.Contains(File.ReadAllText(projectFile.FullPath),"<ApplicationVersion>1.0.0.%2a</ApplicationVersion>","The file still writes it escaped.");
			Assert.AreEqual("1.0.0.*",projectFile.GetProperty("ApplicationVersion"));
			Assert.IsTrue(projectFile.GetItems("BootstrapperPackage").Any(x => x.GetMetadata("ProductName")==".NET Framework 2.0 (x86)"));
			Assert.IsFalse(projectFile.GetItems("BootstrapperPackage").Any(x => x.GetMetadata("ProductName").Contains("%28")));
			Assert.IsTrue(projectFile.GetItems("WebReferenceUrl").Any(x => x.Include.EndsWith("SMSService?wsdl")
				&& x.GetMetadata("UpdateFromURL")=="https://www.callfire.com/service/SMSService?wsdl"));
			Assert.AreEqual("1.0.0.*",ProjectFile.Load(Path.Combine(root,"SparksToothChart","SparksToothChart.csproj")).GetProperty("ApplicationVersion"));
		}
	}
}
