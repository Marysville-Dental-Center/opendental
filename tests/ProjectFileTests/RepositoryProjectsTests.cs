// ProjectFile on the repository's own project files, in whatever format they are in: every C# project of OpenDental.Local.slnf evaluates (its
// conditions, imports and property functions are all understood), every source, resource and page it names is on disk, and for the
// third-party source builds the evaluated items are exactly the ones the last build handed the compiler (SourceBuildItems.txt, which MSBuild
// writes from the real evaluation), so the reader agrees with MSBuild on projects whose items come through imported props and wildcards.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ProjectFileTests {
	[TestClass]
	public class RepositoryProjectsTests {
		private static List<ProjectFile> _listProjects;

		private static List<ProjectFile> Projects() {
			if(_listProjects==null) {
				_listProjects=ProjectFile.GetSolutionFilterProjects(Path.Combine(FixtureFolder.RepoRoot(),"OpenDental.Local.slnf"))
					.Select(x => ProjectFile.Load(x)).ToList();
			}
			return _listProjects;
		}

		[TestMethod]
		public void EveryProjectOfTheSolutionFilter_Evaluates_AndNamesFilesThatExist() {
			List<string> listProblems=new List<string>();
			foreach(ProjectFile projectFile in Projects()) {
				if(projectFile.GetItems("Compile").Count==0) {
					listProblems.Add(projectFile.Name+" compiles nothing");
				}
				foreach(ProjectItem projectItem in projectFile.GetItems("Compile").Concat(projectFile.GetItems("EmbeddedResource")).Concat(projectFile.GetItems("Page"))) {
					if(projectItem.FullPath==null || !File.Exists(projectItem.FullPath)) {
						listProblems.Add(projectFile.Name+": "+projectItem+" is not on disk");
					}
				}
			}
			Assert.IsTrue(Projects().Count>100,"The solution filter lists "+Projects().Count+" C# projects.");
			Assert.AreEqual(0,listProblems.Count,string.Join("; ",listProblems.Take(20)));
		}

		[TestMethod]
		public void EveryProjectFileOfTheRepository_Evaluates() {
			//Also the projects the solution filter leaves out (the plug-in example, and those stage 1 adds), which the tests that scan every
			//project file of the repository read too. The upstream projects inside the submodules are not built and not read.
			string root=FixtureFolder.RepoRoot();
			string[] arraySkipped=new[] { "bin","obj","packages",".git",".claude",".vs","TestResults","node_modules" };
			List<string> listPaths=new List<string>();
			Stack<string> stackFolders=new Stack<string>();
			stackFolders.Push(root);
			while(stackFolders.Count>0) {
				string folder=stackFolders.Pop();
				listPaths.AddRange(Directory.GetFiles(folder,"*.csproj"));
				foreach(string subfolder in Directory.GetDirectories(folder)) {
					string relative=subfolder.Substring(root.Length+1);
					if(!arraySkipped.Contains(Path.GetFileName(subfolder),StringComparer.OrdinalIgnoreCase)
						&& !relative.Equals(@"third-party\PDFsharp",StringComparison.OrdinalIgnoreCase)
						&& !relative.Equals(@"third-party\WeCantSpell.Hunspell",StringComparison.OrdinalIgnoreCase))
					{
						stackFolders.Push(subfolder);
					}
				}
			}
			Assert.IsTrue(listPaths.Count>Projects().Count,"The repository holds more project files than the solution filter lists.");
			List<string> listProblems=new List<string>();
			foreach(string path in listPaths) {
				try {
					ProjectFile.Load(path);
				}
				catch(Exception ex) {
					listProblems.Add(path.Substring(root.Length+1)+": "+ex.Message);
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("; ",listProblems));
		}

		[TestMethod]
		public void TheProgram_CompilesItsMainWindow_AndLinksTheSharedAssemblyInfo() {
			ProjectFile projectFile=Projects().Single(x => x.Name=="OpenPmsDental.csproj");
			projectFile.AssertCompiles(@"Main Modules\FormOpenDental.cs",@"Properties\AssemblyInfo.cs");
			projectFile.AssertEmbeds(@"Main Modules\FormOpenDental.resx");
			Assert.AreEqual(@"Properties\SharedAssemblyInfo.cs",projectFile.AssertItem("Compile",@"..\SharedAssemblyInfo.cs").GetMetadata("Link"));
			Assert.AreEqual(@"bin\Debug\",projectFile.GetProperty("OutputPath"));
		}

		[TestMethod]
		public void EveryProject_ReadsTheNearestDirectoryBuildProps_AndGetsTheExtensionsPathMSBuildGives() {
			//MSBuild 18 -getProperty gives every project of the solution filter its own obj\ folder as MSBuildProjectExtensionsPath, the classic
			//projects without a Microsoft.Common.props import of their own too (S1-T2's review checked OpenPmsDental, OpenPmsDentalBusiness,
			//OpenPmsDentalHL7, ServiceManager, SparksToothChart, UnitTests and xCentralManager), and imports the nearest Directory.Build.props into
			//each: through the project's own import, Sdk.props, or Microsoft.Common.CurrentVersion.targets.
			List<string> listProblems=new List<string>();
			int countWithoutImport=0;
			foreach(ProjectFile projectFile in Projects()) {
				string expected=projectFile.Folder+@"\obj\";
				string actual=projectFile.GetProperty("MSBuildProjectExtensionsPath");
				if(!actual.Equals(expected,StringComparison.OrdinalIgnoreCase)) {
					listProblems.Add(projectFile.Name+": MSBuildProjectExtensionsPath "+actual+", not "+expected);
				}
				string pathProps=null;
				for(DirectoryInfo directoryInfo=new DirectoryInfo(projectFile.Folder);directoryInfo!=null && pathProps==null;directoryInfo=directoryInfo.Parent) {
					string path=Path.Combine(directoryInfo.FullName,"Directory.Build.props");
					if(File.Exists(path)) {
						pathProps=path;
					}
				}
				if(pathProps==null || !projectFile.FilesRead.Contains(pathProps,StringComparer.OrdinalIgnoreCase)) {
					listProblems.Add(projectFile.Name+" does not read "+(pathProps??"a Directory.Build.props"));
				}
				if(!projectFile.IsSdkStyle && !File.ReadAllText(projectFile.FullPath).Contains("Microsoft.Common.props")) {
					countWithoutImport++;
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("; ",listProblems));
			Console.WriteLine(countWithoutImport+" classic projects without a Microsoft.Common.props import of their own.");
		}

		[TestMethod]
		public void SdkStyleProjects_GetTheSdksDefineConstantsAndNoWarn_AsMSBuildEvaluatesThem() {
			//What MSBuild 18 -getProperty gave the SDK-style projects of the solution filter in S1-T2's review: TRACE;DEBUG and 1701;1702 for every
			//project whose own file sets neither (S1-1's, as xCodeBase, xODR, UpdateFileCopier, RdlEngine and DevDbTool), and NoWarn
			//1701;1702;CS0436;CS1591 for the seven third-party source builds, whose DefineConstants are their own (third-party\build\SourceBuild.props
			//sets DisableImplicitConfigurationDefines and adds CS0436;CS1591 to $(NoWarn)).
			List<string> listProblems=new List<string>();
			int countOwnDefaults=0;
			int countSourceBuilds=0;
			foreach(ProjectFile projectFile in Projects().Where(x => x.IsSdkStyle)) {
				string defineConstants=projectFile.GetProperty("DefineConstants");
				string noWarn=projectFile.GetProperty("NoWarn");
				if(projectFile.FullPath.IndexOf(@"\third-party\build\",StringComparison.OrdinalIgnoreCase)>=0) {
					countSourceBuilds++;
					if(noWarn!="1701;1702;CS0436;CS1591") {
						listProblems.Add(projectFile.Name+": NoWarn "+noWarn);
					}
					if(defineConstants.EndsWith(";DEBUG",StringComparison.Ordinal)) {
						listProblems.Add(projectFile.Name+": DefineConstants "+defineConstants+" (no configuration define in a source build)");
					}
					continue;
				}
				if(System.Xml.Linq.XDocument.Load(projectFile.FullPath).Descendants().Any(x => x.Name.LocalName=="DefineConstants" || x.Name.LocalName=="NoWarn")) {
					continue;//Its own values.
				}
				countOwnDefaults++;
				if(defineConstants!="TRACE;DEBUG" || noWarn!="1701;1702") {
					listProblems.Add(projectFile.Name+": DefineConstants "+defineConstants+", NoWarn "+noWarn);
				}
			}
			Assert.AreEqual(7,countSourceBuilds,"The seven source-build projects of third-party\\build.");
			Assert.IsTrue(countOwnDefaults>=18,"S1-1's 18 SDK-style projects set neither; "+countOwnDefaults+" found.");
			Assert.AreEqual(0,listProblems.Count,string.Join("; ",listProblems));
		}

		[TestMethod]
		public void ThirdPartySourceBuilds_ProjectFileAgreesWithTheBuild() {
			List<ProjectFile> listProjects=Projects().Where(x => x.FullPath.IndexOf(@"\third-party\build\",StringComparison.OrdinalIgnoreCase)>=0).ToList();
			Assert.AreEqual(7,listProjects.Count,"The seven source-build projects of third-party\\build.");
			List<string> listProblems=new List<string>();
			foreach(ProjectFile projectFile in listProjects) {
				string folderObj=Path.Combine(projectFile.Folder,"obj");
				FileInfo fileInfo=Directory.Exists(folderObj)
					? new DirectoryInfo(folderObj).GetFiles("SourceBuildItems.txt",SearchOption.AllDirectories).OrderByDescending(x => x.LastWriteTimeUtc).FirstOrDefault()
					: null;
				if(fileInfo==null) {
					BuildOutput.NotBuilt(projectFile.Name+" is not built (no obj\\...\\SourceBuildItems.txt). Build OpenDental.Local.slnf (Debug|x86) first.");
				}
				foreach(string itemType in new[] { "Compile","EmbeddedResource" }) {
					//What the build gave the compiler or the resource step, less the files it generated in obj itself.
					HashSet<string> hashSetBuilt=new HashSet<string>(File.ReadAllLines(fileInfo.FullName).Select(x => x.TrimStart('﻿').Split('|'))
						.Where(x => x[0]==itemType).Select(x => Path.GetFullPath(x[1]))
						.Where(x => !x.StartsWith(folderObj+@"\",StringComparison.OrdinalIgnoreCase)),StringComparer.OrdinalIgnoreCase);
					HashSet<string> hashSetEvaluated=new HashSet<string>(projectFile.GetItems(itemType).Select(x => x.FullPath),StringComparer.OrdinalIgnoreCase);
					foreach(string path in hashSetBuilt.Except(hashSetEvaluated,StringComparer.OrdinalIgnoreCase).Take(5)) {
						listProblems.Add(projectFile.Name+": built but not evaluated: "+itemType+" "+path);
					}
					foreach(string path in hashSetEvaluated.Except(hashSetBuilt,StringComparer.OrdinalIgnoreCase).Take(5)) {
						listProblems.Add(projectFile.Name+": evaluated but not built: "+itemType+" "+path);
					}
					if(itemType=="Compile" && hashSetBuilt.Count<5) {
						listProblems.Add(projectFile.Name+": the build record has only "+hashSetBuilt.Count+" Compile items");
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("; ",listProblems));
		}
	}
}
