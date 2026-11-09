// Proof that a check through ProjectFile still fails when what it checks is broken, in either project format: the item taken out of a classic
// project file, the file taken off the disk or removed from an SDK-style project, the default glob or WPF switched off. The tests that check the
// repository's project files ask the same questions (Compiles, AssertCompiles, AssertEmbeds, AssertPages, metadata), so these failures are
// theirs too.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	[TestClass]
	public class BrokenFixtureTests {
		private static void AssertFails(Action action,string expectedText) {
			try {
				action();
			}
			catch(AssertFailedException ex) {
				StringAssert.Contains(ex.Message,expectedText);
				return;
			}
			Assert.Fail("The check passed although its condition is broken: "+expectedText);
		}

		[TestMethod]
		public void Classic_AnItemTakenOut_FailsItsCheck() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.Load("Classic").AssertCompiles("Program.cs");
				fixtureFolder.EditProject("Classic","<Compile Include=\"Program.cs\" />","");
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				Assert.IsFalse(projectFile.Compiles("Program.cs"),"The file is still on disk, but a classic project compiles only what it lists.");
				AssertFails(() => projectFile.AssertCompiles("Program.cs"),"does not compile Program.cs");
				fixtureFolder.EditProject("Classic","<EmbeddedResource Include=\"Forms\\FormMain.resx\">","<EmbeddedResource Include=\"Forms\\FormOther.resx\">");
				fixtureFolder.EditProject("Classic","<Page Include=\"Frms\\FrmPrint.xaml\">","<Page Include=\"Frms\\FrmOther.xaml\">");
				fixtureFolder.EditProject("Classic","<Link>Properties\\SharedAssemblyInfo.cs</Link>","");
				projectFile=fixtureFolder.Load("Classic");
				AssertFails(() => projectFile.AssertEmbeds(@"Forms\FormMain.resx"),@"does not embed Forms\FormMain.resx");
				AssertFails(() => projectFile.AssertPages(@"Frms\FrmPrint.xaml"),@"does not build the page Frms\FrmPrint.xaml");
				Assert.AreEqual("",projectFile.AssertItem("Compile",@"..\..\Shared\SharedAssemblyInfo.cs").GetMetadata("Link"));
				AssertFails(() => projectFile.AssertItem("ProjectReference",@"..\Missing\Missing.csproj"),@"no ProjectReference item for ..\Missing\Missing.csproj");
			}
		}

		[TestMethod]
		public void Classic_AFileTheWildcardNoLongerFinds_FailsItsCheck() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.DeleteFile(@"tests\Classic\VendorDirect\Lab\Sub\LabHelper.cs");
				fixtureFolder.EditProject("Classic","Exclude=\"Old\\Skip.cs\"","");
				ProjectFile projectFile=fixtureFolder.Load("Classic");
				AssertFails(() => projectFile.AssertCompiles(@"VendorDirect\Lab\Sub\LabHelper.cs"),@"does not compile VendorDirect\Lab\Sub\LabHelper.cs");
				Assert.IsTrue(projectFile.Compiles(@"Old\Skip.cs"),"Without the Exclude, the wildcard compiles it.");
				AssertFails(() => projectFile.AssertDoesNotCompile(@"Old\Skip.cs"),@"still compiles Old\Skip.cs");
			}
		}

		[TestMethod]
		public void Sdk_AFileOffTheDiskOrRemoved_FailsItsCheck() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.Load("SdkStyle").AssertCompiles("Program.cs",@"Forms\FormMain.cs");
				fixtureFolder.DeleteFile(@"tests\SdkStyle\Program.cs");
				fixtureFolder.EditProject("SdkStyle","<Compile Remove=\"Removed\\**\" />","<Compile Remove=\"Removed\\**;Forms\\FormMain.cs\" />");
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				AssertFails(() => projectFile.AssertCompiles("Program.cs"),"does not compile Program.cs");
				AssertFails(() => projectFile.AssertCompiles(@"Forms\FormMain.cs"),@"does not compile Forms\FormMain.cs");
				Assert.IsNull(projectFile.GetItem("Compile",@"Forms\FormMain.cs"),"An Update after a Remove has nothing to update.");
				fixtureFolder.DeleteFile(@"tests\SdkStyle\Forms\FormMain.resx");
				fixtureFolder.DeleteFile(@"tests\SdkStyle\Frms\FrmPrint.xaml");
				projectFile=fixtureFolder.Load("SdkStyle");
				AssertFails(() => projectFile.AssertEmbeds(@"Forms\FormMain.resx"),@"does not embed Forms\FormMain.resx");
				AssertFails(() => projectFile.AssertPages(@"Frms\FrmPrint.xaml"),@"does not build the page Frms\FrmPrint.xaml");
			}
		}

		[TestMethod]
		public void Sdk_TheDefaultGlobsSwitchedOff_FailTheirChecks() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.EditProject("SdkStyle","<UseWPF>true</UseWPF>","<UseWPF>false</UseWPF><EnableDefaultCompileItems>false</EnableDefaultCompileItems>");
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				AssertFails(() => projectFile.AssertCompiles("Program.cs"),"does not compile Program.cs");
				projectFile.AssertCompiles(@"..\..\Shared\SharedAssemblyInfo.cs");//the explicit item stays
				Assert.AreEqual(0,projectFile.GetItems("Page").Count,"Without UseWPF no XAML file is a page.");
				AssertFails(() => projectFile.AssertPages(@"Frms\FrmPrint.xaml"),"does not build the page");
				projectFile.AssertEmbeds(@"Forms\FormMain.resx");//its own switch is still on
				fixtureFolder.EditProject("SdkStyle","<UseWPF>false</UseWPF>","<UseWPF>false</UseWPF><EnableDefaultItems>false</EnableDefaultItems>");
				projectFile=fixtureFolder.Load("SdkStyle");
				AssertFails(() => projectFile.AssertEmbeds(@"Forms\FormMain.resx"),@"does not embed Forms\FormMain.resx");
				Assert.AreEqual(0,projectFile.GetItems("None").Count(x => x.IsFromWildcard));
			}
		}

		[TestMethod]
		public void Sdk_AnExcludeOrAMissingMetadatum_FailsItsCheck() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				fixtureFolder.EditProject("SdkStyle","$(DefaultItemExcludes);Nested.Tests\\**","$(DefaultItemExcludes);Nested.Tests\\**;VendorDirect\\Lab\\Sub\\**");
				fixtureFolder.EditProject("SdkStyle"," Link=\"Properties\\SharedAssemblyInfo.cs\"","");
				fixtureFolder.EditProject("SdkStyle","<Compile Update=\"Forms\\FormMain.cs\" SubType=\"Form\" />","");
				ProjectFile projectFile=fixtureFolder.Load("SdkStyle");
				AssertFails(() => projectFile.AssertCompiles(@"VendorDirect\Lab\Sub\LabHelper.cs"),@"does not compile VendorDirect\Lab\Sub\LabHelper.cs");
				CollectionAssert.AreEqual(new[] { @"VendorDirect\Lab\Sub\LabHelper.cs" },projectFile.GetFilesNotCompiled("VendorDirect"));
				//Without its own Link the SDK gives the file outside the project folder its file name (SetLinkMetadataAutomatically), not
				//Properties\SharedAssemblyInfo.cs, so a check of the explicit Link still fails.
				Assert.AreEqual("SharedAssemblyInfo.cs",projectFile.AssertItem("Compile",@"..\..\Shared\SharedAssemblyInfo.cs").GetMetadata("Link"));
				Assert.AreEqual("",projectFile.AssertItem("Compile",@"Forms\FormMain.cs").GetMetadata("SubType"));
			}
		}
	}
}
