// The opt-in proof that ProjectFile evaluates what MSBuild evaluates (package S1-T2 of the .NET 10 plan, docs/specs/net10-plan.md, section 3):
// for every C# project of OpenDental.Local.slnf, the Compile, EmbeddedResource, Page, Resource, None, Content, PackageReference and PackageVersion
// (since S2-1) items it evaluates, with their metadata, equal the items.txt that tools\Get-ProjectBaseline.ps1 recorded from MSBuild's own
// evaluation (-getItem), line for line. So the tests that
// read project files through ProjectFile see what the build sees, classic and SDK-style projects alike. It needs a record of the same tree,
// taken after a full lane build and before any test run, so it runs only with OPMS_CHECK_PROJECT_BASELINE=1 (Inconclusive otherwise), against
// the folder OPMS_PROJECT_BASELINE_DIR names, or tools\project-baseline. The stage gates run it against a record taken at the gate, and S1-3
// to S1-6 in their proof; with OPMS_PROJECT_BASELINE_DIR it writes what it compared and every difference to items-check.txt in that folder.
// The other tests here check, on the fixtures, that the lines are written as the script writes them.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	[TestClass]
	public class ProjectBaselineTests {
		public const string VARIABLE_CHECK="OPMS_CHECK_PROJECT_BASELINE";
		public const string VARIABLE_FOLDER="OPMS_PROJECT_BASELINE_DIR";

		[TestMethod]
		public void EveryProjectOfTheSolutionFilter_EvaluatesTheItemsMSBuildRecorded() {
			if(!BuildOutput.IsRequiredValue(Environment.GetEnvironmentVariable(VARIABLE_CHECK))) {
				Assert.Inconclusive(VARIABLE_CHECK+" is not 1: this check needs a record of this tree by tools\\Get-ProjectBaseline.ps1, taken after a full "
					+"lane build and before any test run (folder in "+VARIABLE_FOLDER+", or tools\\project-baseline).");
			}
			string root=FixtureFolder.RepoRoot();
			string folder=Environment.GetEnvironmentVariable(VARIABLE_FOLDER);
			bool isOwnRecord=!string.IsNullOrWhiteSpace(folder);
			if(!isOwnRecord) {
				folder=Path.Combine(root,"tools","project-baseline");
			}
			folder=folder.Trim();
			string pathItems=Path.Combine(folder,"items.txt");
			Assert.IsTrue(File.Exists(pathItems),"No record at "+pathItems);
			List<string> listRecorded=File.ReadAllLines(pathItems).Where(x => x!="").ToList();
			List<ProjectFile> listProjects=ProjectFile.GetSolutionFilterProjects(Path.Combine(root,"OpenDental.Local.slnf")).Select(x => ProjectFile.Load(x)).ToList();
			List<string> listEvaluated=BaselineRecord.GetItemLines(listProjects,root);
			//Every project is in the record, so a project the script skipped cannot pass by being absent.
			HashSet<string> hashSetRecordedProjects=new HashSet<string>(listRecorded.Select(x => x.Split('|')[0]),StringComparer.Ordinal);
			List<string> listMissingProjects=listProjects.Select(x => Path.GetFileNameWithoutExtension(x.FullPath)).Where(x => !hashSetRecordedProjects.Contains(x)).ToList();
			HashSet<string> hashSetRecorded=new HashSet<string>(listRecorded,StringComparer.Ordinal);
			HashSet<string> hashSetEvaluated=new HashSet<string>(listEvaluated,StringComparer.Ordinal);
			List<string> listOnlyRecorded=listRecorded.Where(x => !hashSetEvaluated.Contains(x)).ToList();
			List<string> listOnlyEvaluated=listEvaluated.Where(x => !hashSetRecorded.Contains(x)).ToList();
			List<ProjectFile> listSdkStyle=listProjects.Where(x => x.IsSdkStyle).ToList();
			string summary=listProjects.Count+" projects ("+listSdkStyle.Count+" SDK-style: "+string.Join(", ",listSdkStyle.Select(x => x.Name))+"), "
				+listRecorded.Count+" recorded items, "+listEvaluated.Count+" evaluated items, "+listOnlyRecorded.Count+" only in "+pathItems+", "
				+listOnlyEvaluated.Count+" only evaluated";
			//The summary and the differences: on the console, and for a record named by OPMS_PROJECT_BASELINE_DIR (a gate's or a package's own,
			//never the committed one) in items-check.txt next to it, so a passing run shows what it compared too.
			List<string> listReport=new List<string> { summary };
			listReport.AddRange(listMissingProjects.Select(x => "  missing project: "+x));
			listReport.AddRange(listOnlyRecorded.Take(500).Select(x => "  - "+x));
			listReport.AddRange(listOnlyEvaluated.Take(500).Select(x => "  + "+x));
			listReport.Add(listMissingProjects.Count+listOnlyRecorded.Count+listOnlyEvaluated.Count==0 ? "No differences." : "The items differ.");
			foreach(string line in listReport) {
				Console.WriteLine(line);
			}
			if(isOwnRecord) {
				File.WriteAllLines(Path.Combine(folder,"items-check.txt"),listReport);
			}
			Assert.AreEqual(0,listMissingProjects.Count,"Projects the record does not hold: "+string.Join(", ",listMissingProjects));
			Assert.AreEqual(0,listOnlyRecorded.Count+listOnlyEvaluated.Count,summary+". First differences: "
				+string.Join("; ",listOnlyRecorded.Take(10).Select(x => "-"+x).Concat(listOnlyEvaluated.Take(10).Select(x => "+"+x))));
		}

		[TestMethod]
		public void Lines_AreWrittenAsTheScriptWritesThem() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				List<string> listLines=BaselineRecord.GetItemLines(new[] { fixtureFolder.Load("SdkExplicitImports"),fixtureFolder.Load("Classic") },fixtureFolder.Root);
				//project|item type|include|metadata, sorted ordinally, metadata by name; a duplicate numbered.
				CollectionAssert.AreEqual(new[] {
					@"Classic|Compile|..\..\Shared\SharedAssemblyInfo.cs|Link=Properties\SharedAssemblyInfo.cs",
					@"Classic|Compile|Forms\FormMain.Designer.cs|DependentUpon=FormMain.cs",
					@"Classic|Compile|Forms\FormMain.cs|SubType=Form",
					@"Classic|Compile|Frms\FrmPrint.xaml.cs|DependentUpon=FrmPrint.xaml",
					@"Classic|Compile|Old\Keep.cs|",
					@"Classic|Compile|Program.cs|",
					@"Classic|Compile|VendorDirect\Lab\LabClient.cs|",
					@"Classic|Compile|VendorDirect\Lab\Sub\LabHelper.cs|",
					@"Classic|EmbeddedResource|Branding\logo.png|LogicalName=Fixture.Branding.logo.png",
					@"Classic|EmbeddedResource|Forms\FormMain.resx|DependentUpon=FormMain.cs",
					@"Classic|None|Data\Seed.txt|CopyToOutputDirectory=PreserveNewest",
					@"Classic|Page|Frms\FrmPrint.xaml|Generator=MSBuild:Compile;SubType=Designer",
					@"Classic|Resource|Resources\Icon.png|",
					@"SdkExplicitImports|Compile|Forms\FormMain.cs|",
					@"SdkExplicitImports|Compile|Program.cs|",
					@"SdkExplicitImports|Compile|Program.cs||#2",
					@"SdkExplicitImports|EmbeddedResource|Forms\FormMain.resx|",
				},listLines.ToArray());
			}
		}

		[TestMethod]
		public void Lines_WriteRepositoryPathsRelative_AndOtherFoldersAsPlaceholders() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//An item named by a full path inside the repository is written relative to the repository, as the third-party wrappers' linked
				//sources are; a value outside it, under the temp folder, say, with a placeholder.
				fixtureFolder.EditProject("Classic","<Resource Include=\"Resources\\Icon.png\" />","<Resource Include=\"Resources\\Icon.png\" />"
					+"<None Include=\"$(MSBuildThisFileDirectory)..\\..\\Shared\\Data\\Seed.json\" CopyToOutputDirectory=\"PreserveNewest\" />");
				List<string> listLines=BaselineRecord.GetItemLines(new[] { fixtureFolder.Load("Classic") },fixtureFolder.Root);
				CollectionAssert.Contains(listLines,@"Classic|None|tests\Classic\..\..\Shared\Data\Seed.json|CopyToOutputDirectory=PreserveNewest");
				BaselineRecord.RecordPaths recordPaths=new BaselineRecord.RecordPaths(fixtureFolder.Root);
				Assert.AreEqual(".",recordPaths.Relative(fixtureFolder.Root));
				Assert.AreEqual(@"tests\x.cs",recordPaths.Relative(fixtureFolder.Combine(@"tests\x.cs")));
				Assert.AreEqual(@"TESTS\X.CS",recordPaths.Relative(fixtureFolder.Combine(@"tests\x.cs").ToUpperInvariant()),"Ignoring case.");
				Assert.AreEqual("tests/x.cs",recordPaths.Relative(fixtureFolder.Root.Replace('\\','/')+"/tests/x.cs"),"Forward slashes.");
				string temp=Environment.GetEnvironmentVariable("TEMP");
				if(!string.IsNullOrEmpty(temp)) {
					//The temp folder lies inside the local app data folder: the longest folder's placeholder wins.
					Assert.AreEqual(@"<Temp>\elsewhere.txt",new BaselineRecord.RecordPaths(@"C:\NotThisRoot").Relative(Path.Combine(temp,"elsewhere.txt")));
				}
			}
		}

		[TestMethod]
		public void AResourceItem_IsRecorded_AndOneTakenOutIsADifference() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//The WPF resources, as tools\Get-ProjectBaseline.ps1 records them since S1-T3 (-getItem:Resource): a Resource the project no longer
				//names is a difference the check reports.
				List<string> listBefore=BaselineRecord.GetItemLines(new[] { fixtureFolder.Load("SdkStyle") },fixtureFolder.Root);
				CollectionAssert.IsSubsetOf(new[] { @"SdkStyle|Resource|Frms\Dictionary.xaml|",@"SdkStyle|Resource|Resources\Icon.png|" },listBefore);
				fixtureFolder.EditProject("SdkStyle","<Resource Include=\"Resources\\Icon.png\" />","");
				List<string> listAfter=BaselineRecord.GetItemLines(new[] { fixtureFolder.Load("SdkStyle") },fixtureFolder.Root);
				CollectionAssert.AreEqual(new[] { @"SdkStyle|Resource|Resources\Icon.png|" },listBefore.Except(listAfter).ToArray());
				Assert.AreEqual(0,listAfter.Except(listBefore).Count());
			}
		}

		[TestMethod]
		public void PackageItems_AreRecorded_WithTheCentralVersionsOfDirectoryPackagesProps() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				//As tools\Get-ProjectBaseline.ps1 records them since S2-1 (-getItem:PackageReference, -getItem:PackageVersion): a project's own
				//PackageReference items, and the PackageVersion items it reads from Directory.Packages.props, which a classic project's guarded
				//groups leave out.
				fixtureFolder.WriteFile("Directory.Packages.props","<Project><PropertyGroup Condition=\"'$(UsingMicrosoftNETSdk)' == 'true'\">"
					+"<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup Condition=\"'$(UsingMicrosoftNETSdk)' == 'true'\">"
					+"<PackageVersion Include=\"Fixture.Package\" Version=\"1.2.3\" /></ItemGroup></Project>");
				fixtureFolder.EditProject("SdkExplicitImports","<Compile Include=\"Program.cs\" />","<Compile Include=\"Program.cs\" />"
					+"<PackageReference Include=\"Fixture.Package\" PrivateAssets=\"compile\" />");
				List<string> listLines=BaselineRecord.GetItemLines(new[] { fixtureFolder.Load("SdkExplicitImports"),fixtureFolder.Load("Classic") },fixtureFolder.Root);
				CollectionAssert.IsSubsetOf(new[] { "SdkExplicitImports|PackageReference|Fixture.Package|PrivateAssets=compile",
					"SdkExplicitImports|PackageVersion|Fixture.Package|Version=1.2.3" },listLines);
				Assert.AreEqual(0,listLines.Count(x => x.StartsWith("Classic|Package")),string.Join("; ",listLines));
			}
		}

		[TestMethod]
		public void AChangedItem_IsADifference() {
			using(FixtureFolder fixtureFolder=FixtureFolder.Create()) {
				List<string> listBefore=BaselineRecord.GetItemLines(new[] { fixtureFolder.Load("SdkStyle") },fixtureFolder.Root);
				fixtureFolder.EditProject("SdkStyle","<Compile Update=\"Forms\\FormMain.cs\" SubType=\"Form\" />","<Compile Update=\"Forms\\FormMain.cs\" SubType=\"UserControl\" />");
				List<string> listAfter=BaselineRecord.GetItemLines(new[] { fixtureFolder.Load("SdkStyle") },fixtureFolder.Root);
				CollectionAssert.AreEqual(new[] { @"SdkStyle|Compile|Forms\FormMain.cs|SubType=Form" },listBefore.Except(listAfter).ToArray());
				CollectionAssert.AreEqual(new[] { @"SdkStyle|Compile|Forms\FormMain.cs|SubType=UserControl" },listAfter.Except(listBefore).ToArray());
			}
		}
	}

	///<summary>The lines of items.txt as tools\Get-ProjectBaseline.ps1 writes them, from ProjectFile's evaluation instead of MSBuild's.</summary>
	internal static class BaselineRecord {
		///<summary>The item types the script records. Resource since S1-T3, PackageReference and PackageVersion (the central versions of
		///Directory.Packages.props) since S2-1: a record taken before holds no such lines, so it differs from this evaluation by every one of those
		///items until it is taken again.</summary>
		public static readonly string[] ArrayItemTypes=new[] { "Compile","EmbeddedResource","Page","Resource","None","Content","PackageReference",
			"PackageVersion" };

		///<summary>project|item type|include|metadata for every item of the recorded types: the project's file name without its extension, the
		///include and every metadata value with paths relative to the repository (RecordPaths), the item's own metadata as name=value sorted by name
		///as Sort-Object sorts them (the current culture, ignoring case) and joined with ';'. The lines sorted ordinally; a line that occurs again
		///gets |#2, |#3 and so on, as Write-Record numbers them.</summary>
		public static List<string> GetItemLines(IEnumerable<ProjectFile> projectFiles,string root) {
			RecordPaths recordPaths=new RecordPaths(root);
			StringComparer stringComparerNames=StringComparer.Create(CultureInfo.CurrentCulture,true);
			List<string> listLines=new List<string>();
			foreach(ProjectFile projectFile in projectFiles) {
				string name=Path.GetFileNameWithoutExtension(projectFile.FullPath);
				foreach(string itemType in ArrayItemTypes) {
					foreach(ProjectItem projectItem in projectFile.GetItems(itemType)) {
						string metadata=string.Join(";",projectItem.Metadata.OrderBy(x => x.Key,stringComparerNames)
							.Select(x => x.Key+"="+recordPaths.Relative(x.Value)));
						listLines.Add(name+"|"+itemType+"|"+recordPaths.Relative(projectItem.Include)+"|"+metadata);
					}
				}
			}
			string[] arrayLines=listLines.ToArray();
			Array.Sort(arrayLines,StringComparer.Ordinal);
			string previous=null;
			int count=0;
			for(int i=0;i<arrayLines.Length;i++) {
				if(arrayLines[i]==previous) {
					count++;
					arrayLines[i]=arrayLines[i]+"|#"+count;
					continue;
				}
				previous=arrayLines[i];
				count=1;
			}
			return arrayLines.ToList();
		}

		///<summary>Get-Relative of the script: the repository's own folder becomes "." and a path below it relative to it; a path below the Program
		///Files, Windows, local app data, temp or user profile folder starts with that folder's placeholder; both with back or forward slashes, the
		///longest folder first, ignoring case.</summary>
		internal sealed class RecordPaths {
			private readonly string _root;
			private readonly string _rootForward;
			private readonly List<string[]> _listReplacements=new List<string[]>();

			public RecordPaths(string root) {
				_root=root.TrimEnd('\\','/');
				_rootForward=_root.Replace('\\','/');
				Add(_root,"");
				Add(Environment.GetEnvironmentVariable("ProgramFiles(x86)"),@"<ProgramFiles(x86)>\");
				Add(Environment.GetEnvironmentVariable("ProgramFiles"),@"<ProgramFiles>\");
				Add(Environment.GetEnvironmentVariable("LOCALAPPDATA"),@"<LocalAppData>\");
				Add(Environment.GetEnvironmentVariable("USERPROFILE"),@"<UserProfile>\");
				Add(Environment.GetEnvironmentVariable("TEMP"),@"<Temp>\");
				Add(Environment.GetEnvironmentVariable("windir"),@"<Windows>\");
				_listReplacements.Sort((x,y) => y[0].Length.CompareTo(x[0].Length));
			}

			private void Add(string path,string token) {
				if(string.IsNullOrEmpty(path)) {
					return;
				}
				string folder=path.TrimEnd('\\','/');
				_listReplacements.Add(new[] { folder+@"\",token });
				_listReplacements.Add(new[] { folder.Replace('\\','/')+"/",token.Replace('\\','/') });
			}

			public string Relative(string text) {
				if(string.IsNullOrEmpty(text)) {
					return text;
				}
				if(text.Equals(_root,StringComparison.OrdinalIgnoreCase) || text.Equals(_rootForward,StringComparison.OrdinalIgnoreCase)) {
					return ".";
				}
				foreach(string[] arrayPair in _listReplacements) {
					int index=text.IndexOf(arrayPair[0],StringComparison.OrdinalIgnoreCase);
					while(index>=0) {
						text=text.Substring(0,index)+arrayPair[1]+text.Substring(index+arrayPair[0].Length);
						index=text.IndexOf(arrayPair[0],index+arrayPair[1].Length,StringComparison.OrdinalIgnoreCase);
					}
				}
				return text;
			}
		}
	}
}
