// Shared helpers for the LIB-WEB tests: repository paths, the shipped source files, and temporary folders.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace LibWebTests {
	internal static class TestPaths {
		///<summary>Folders never searched: build output, NuGet packages, test results, and every folder whose name starts with '.' (.git, .vs, and
		///the worktrees under .claude, which are other checkouts).</summary>
		private static readonly string[] _arrayFoldersSkipped={ "bin","obj","packages","TestResults","node_modules" };
		///<summary>The top folders that hold no shipped code: tests, test projects, documentation and the developer tools.</summary>
		private static readonly string[] _arrayFoldersNotShipped={ "tests","UnitTests","UnitTestsCore","NoHqTests","docs","tools" };

		///<summary>The repository root: the first folder above the test assembly that holds LICENSE and NOTICE.</summary>
		public static string RepoRoot {
			get {
				DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
				while(directoryInfo!=null) {
					if(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))) {
						return directoryInfo.FullName;
					}
					directoryInfo=directoryInfo.Parent;
				}
				throw new InvalidOperationException("LICENSE and NOTICE not found above "+AppDomain.CurrentDomain.BaseDirectory);
			}
		}

		public static string ReadSource(params string[] arrayParts) {
			return File.ReadAllText(Path.Combine(RepoRoot,Path.Combine(arrayParts)));
		}

		///<summary>The path relative to the repository root, for messages.</summary>
		public static string Relative(string path) {
			string root=RepoRoot.TrimEnd('\\')+"\\";
			return path.StartsWith(root,StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : path;
		}

		///<summary>Every file of the shipped code with one of the extensions: everything under the root except _arrayFoldersNotShipped at the top,
		///_arrayFoldersSkipped anywhere and folders whose names start with '.'.</summary>
		public static List<string> GetShippedFiles(params string[] arrayExtensions) {
			List<string> listFiles=new List<string>();
			string root=RepoRoot;
			Stack<string> stackFolders=new Stack<string>();
			foreach(string folder in Directory.GetDirectories(root)) {
				string name=Path.GetFileName(folder);
				if(name.StartsWith(".") || _arrayFoldersSkipped.Contains(name,StringComparer.OrdinalIgnoreCase)
					|| _arrayFoldersNotShipped.Contains(name,StringComparer.OrdinalIgnoreCase))
				{
					continue;
				}
				stackFolders.Push(folder);
			}
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

		///<summary>A new empty folder under the temp folder.</summary>
		public static string NewTempFolder(string name) {
			string folder=Path.Combine(Path.GetTempPath(),"LibWebTests",name+"_"+Guid.NewGuid().ToString("N").Substring(0,8));
			Directory.CreateDirectory(folder);
			return folder;
		}

		///<summary>Deletes a folder, waiting for browser processes to let go of it.</summary>
		public static void DeleteFolder(string folder) {
			for(int i=0;i<20;i++) {
				try {
					if(Directory.Exists(folder)) {
						Directory.Delete(folder,true);
					}
					return;
				}
				catch(Exception) {
					Thread.Sleep(500);
				}
			}
		}
	}
}
