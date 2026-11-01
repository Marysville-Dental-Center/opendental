// Shared helpers for the BIN-B tests: repository paths and temporary folders.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BINBTests {
	///<summary>MSTest v1 has no Assert.ThrowsException.</summary>
	internal static class Expect {
		public static T Throws<T>(Action action,string message="") where T:Exception {
			try {
				action();
			}
			catch(T ex) {
				return ex;
			}
			catch(Exception ex) {
				Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail("Expected "+typeof(T).Name+" but got "+ex.GetType().Name+": "+ex.Message+" "+message);
			}
			Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail("Expected "+typeof(T).Name+" but nothing was thrown. "+message);
			return null;
		}
	}

	internal static class TestPaths {
		///<summary>Folders never searched for project and source files: build output, NuGet packages, test results, and every folder whose name
		///starts with '.' (.git, .vs, and the worktrees under .claude, which are other checkouts).</summary>
		private static readonly string[] _arrayFoldersSkipped={ "bin","obj","packages","TestResults","node_modules" };

		///<summary>The repository root: the first folder above the test assembly that holds OpenDental.sln.</summary>
		public static string RepoRoot {
			get {
				DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
				while(directoryInfo!=null) {
					if(File.Exists(Path.Combine(directoryInfo.FullName,"OpenDental.sln"))) {
						return directoryInfo.FullName;
					}
					directoryInfo=directoryInfo.Parent;
				}
				throw new InvalidOperationException("OpenDental.sln not found above "+AppDomain.CurrentDomain.BaseDirectory);
			}
		}

		public static string ReadSource(params string[] arrayParts) {
			return File.ReadAllText(Path.Combine(RepoRoot,Path.Combine(arrayParts)));
		}

		///<summary>A new empty folder under the temp folder.</summary>
		public static string NewTempFolder(string name) {
			string folder=Path.Combine(Path.GetTempPath(),"BINBTests",name+"_"+Guid.NewGuid().ToString("N").Substring(0,8));
			Directory.CreateDirectory(folder);
			return folder;
		}

		///<summary>Every file under the root with one of the extensions, skipping _arrayFoldersSkipped and folders whose names start with '.'.</summary>
		public static List<string> GetFiles(string root,params string[] arrayExtensions) {
			List<string> listFiles=new List<string>();
			Stack<string> stackFolders=new Stack<string>();
			stackFolders.Push(root);
			while(stackFolders.Count>0) {
				string folder=stackFolders.Pop();
				foreach(string file in Directory.GetFiles(folder)) {
					if(arrayExtensions.Any(x => file.EndsWith(x,StringComparison.OrdinalIgnoreCase))) {
						listFiles.Add(file);
					}
				}
				foreach(string subfolder in Directory.GetDirectories(folder)) {
					string name=Path.GetFileName(subfolder);
					if(name.StartsWith(".") || _arrayFoldersSkipped.Contains(name,StringComparer.OrdinalIgnoreCase)) {
						continue;
					}
					stackFolders.Push(subfolder);
				}
			}
			return listFiles;
		}
	}
}
