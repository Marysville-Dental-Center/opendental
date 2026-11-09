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
		public void TheProgram_CompilesItsMainWindow_AndLinksTheSharedAssemblyInfo() {
			ProjectFile projectFile=Projects().Single(x => x.Name=="OpenPmsDental.csproj");
			projectFile.AssertCompiles(@"Main Modules\FormOpenDental.cs",@"Properties\AssemblyInfo.cs");
			projectFile.AssertEmbeds(@"Main Modules\FormOpenDental.resx");
			Assert.AreEqual(@"Properties\SharedAssemblyInfo.cs",projectFile.AssertItem("Compile",@"..\SharedAssemblyInfo.cs").GetMetadata("Link"));
			Assert.AreEqual(@"bin\Debug\",projectFile.GetProperty("OutputPath"));
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
