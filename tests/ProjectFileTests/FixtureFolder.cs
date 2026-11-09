// A scratch folder in the temp folder with the fixture project files of tests\ProjectFileTests\Fixtures laid out as the repository lays out its
// own (Directory.Build.props and OpenDental.sln at the root, tests\Directory.Build.props importing the props, one folder per project), the
// source files each project folder holds and the shared files outside them that the fixtures link. The files are empty: ProjectFile reads
// which files exist, never their text. Deleted when the test ends.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	internal sealed class FixtureFolder : IDisposable {
		///<summary>The files of tests\Classic (the classic fixture): what it lists, what its wildcards find, and what it leaves out.</summary>
		public static readonly string[] ArrayClassicFiles=new[] { "Program.cs",@"Forms\FormMain.cs",@"Forms\FormMain.Designer.cs",
			@"Forms\FormMain.resx",@"VendorDirect\Lab\LabClient.cs",@"VendorDirect\Lab\Sub\LabHelper.cs",@"VendorDirect\Lab\Notes.txt",@"Old\Keep.cs",
			@"Old\Skip.cs","Unlisted.cs",@"Frms\FrmPrint.xaml",@"Frms\FrmPrint.xaml.cs",@"Resources\Icon.png",@"Branding\logo.png",@"Data\Seed.txt",
			@"bin\Debug\Stale.cs" };
		///<summary>The files of tests\SdkStyle (the SDK-style fixture), with the ones the default globs must leave out: bin, obj, a hidden folder,
		///a nested project's folder (DefaultItemExcludes), a removed folder (Compile Remove), the .user file.</summary>
		public static readonly string[] ArraySdkStyleFiles=new[] { "Program.cs",@"Forms\FormMain.cs",@"Forms\FormMain.Designer.cs",
			@"Forms\FormMain.resx",@"VendorDirect\Lab\LabClient.cs",@"VendorDirect\Lab\Sub\LabHelper.cs",@"VendorDirect\Lab\Notes.txt",
			@"Frms\FrmPrint.xaml",@"Frms\FrmPrint.xaml.cs",@"Frms\Dictionary.xaml",@"Frms\NotAPage.xaml","App.xaml","App.xaml.cs",
			@"Resources\Icon.png",@"Branding\logo.png",@"Data\Seed.txt",@"Removed\Old.cs",@"Nested.Tests\NestedTest.cs",
			@"Nested.Tests\Nested.Tests.csproj",@"bin\Debug\Stale.cs",@"obj\Debug\Generated.cs",@".vs\Hidden.cs","SdkStyle.csproj.user" };
		public static readonly string[] ArraySdkExplicitImportsFiles=new[] { "Program.cs",@"Forms\FormMain.cs",@"Forms\FormMain.resx",
			@"Forms\Other.resx" };
		public static readonly string[] ArraySdkLinksFiles=new[] { "Program.cs","Notes.txt" };
		///<summary>The files outside the project folders that the fixtures link (paths relative to the scratch folder).</summary>
		public static readonly string[] ArraySharedFiles=new[] { @"Shared\SharedAssemblyInfo.cs",@"Shared\Linked\Top.cs",@"Shared\Linked\Sub\Deep.cs",
			@"Shared\Data\Seed.json",@"Shared\Strings.resx",@"Required dlls\Native.dll",@"Required dlls\After.dll" };

		public string Root { get; private set; }

		private FixtureFolder() {
		}

		public static FixtureFolder Create() {
			FixtureFolder fixtureFolder=new FixtureFolder();
			fixtureFolder.Root=Path.Combine(Path.GetTempPath(),"ProjectFileTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(fixtureFolder.Root);
			string folderFixtures=Path.Combine(RepoRoot(),"tests","ProjectFileTests","Fixtures");
			File.Copy(Path.Combine(folderFixtures,"Directory.Build.props.fixture"),fixtureFolder.Combine("Directory.Build.props"));
			//Also stops the search for a Directory.Build.targets at the scratch folder, so nothing above the temp folder is read.
			File.Copy(Path.Combine(folderFixtures,"Directory.Build.targets.fixture"),fixtureFolder.Combine("Directory.Build.targets"));
			fixtureFolder.WriteFile(@"tests\Directory.Build.props",File.ReadAllText(Path.Combine(folderFixtures,"tests.Directory.Build.props.fixture")));
			//The solution that maps each fixture project to Debug|x86, as OpenDental.sln maps nearly every project.
			fixtureFolder.WriteFile("OpenDental.sln",File.ReadAllText(Path.Combine(folderFixtures,"OpenDental.sln.fixture")));
			foreach(string file in ArraySharedFiles) {
				fixtureFolder.WriteFile(file,"");
			}
			fixtureFolder.AddProject("Classic",File.ReadAllText(Path.Combine(folderFixtures,"Classic.csproj.fixture")),ArrayClassicFiles);
			fixtureFolder.AddProject("SdkStyle",File.ReadAllText(Path.Combine(folderFixtures,"SdkStyle.csproj.fixture")),ArraySdkStyleFiles);
			fixtureFolder.AddProject("SdkExplicitImports",File.ReadAllText(Path.Combine(folderFixtures,"SdkExplicitImports.csproj.fixture")),
				ArraySdkExplicitImportsFiles);
			fixtureFolder.AddProject("SdkLinks",File.ReadAllText(Path.Combine(folderFixtures,"SdkLinks.csproj.fixture")),ArraySdkLinksFiles);
			return fixtureFolder;
		}

		private void AddProject(string name,string text,string[] arrayFiles) {
			WriteFile(@"tests\"+name+@"\"+name+".csproj",text);
			foreach(string file in arrayFiles) {
				WriteFile(@"tests\"+name+@"\"+file,"");
			}
		}

		public string Combine(string relativePath) {
			return Path.Combine(Root,relativePath);
		}

		///<summary>The fixture project file of the name: tests\Name\Name.csproj.</summary>
		public string ProjectPath(string name) {
			return Combine(@"tests\"+name+@"\"+name+".csproj");
		}

		public ProjectFile Load(string name) {
			return ProjectFile.Load(ProjectPath(name));
		}

		public void WriteFile(string relativePath,string text) {
			string path=Combine(relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllText(path,text);
		}

		public void DeleteFile(string relativePath) {
			File.Delete(Combine(relativePath));
		}

		///<summary>Replaces text in a fixture project file; fails the test when the text is not there, so a broken fixture never passes.</summary>
		public void EditProject(string name,string oldText,string newText) {
			EditFile(@"tests\"+name+@"\"+name+".csproj",oldText,newText);
		}

		///<summary>Replaces text in any file of the scratch folder (OpenDental.sln, say); fails the test when the text is not there.</summary>
		public void EditFile(string relativePath,string oldText,string newText) {
			string path=Combine(relativePath);
			string text=File.ReadAllText(path);
			Assert.IsTrue(text.Contains(oldText),relativePath+" has no "+oldText);
			File.WriteAllText(path,text.Replace(oldText,newText));
		}

		public void Dispose() {
			try {
				Directory.Delete(Root,true);
			}
			catch(IOException) {
			}
			catch(UnauthorizedAccessException) {
			}
		}

		///<summary>The repository root: the first folder above the test assembly with LICENSE and NOTICE.</summary>
		public static string RepoRoot() {
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null) {
				if(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))) {
					return directoryInfo.FullName;
				}
				directoryInfo=directoryInfo.Parent;
			}
			Assert.Fail("The repository root (the folder with LICENSE and NOTICE) was not found above "+AppDomain.CurrentDomain.BaseDirectory);
			return null;
		}
	}
}
