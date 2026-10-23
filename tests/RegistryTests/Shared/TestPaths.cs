using System;
using System.IO;

namespace RegistryTests {
	///<summary>Locates the repository and the test data files.</summary>
	public static class TestPaths {
		///<summary>Walks up from the test assembly until it finds OpenDental.sln.</summary>
		public static string GetRepositoryRoot() {
			string dir=Path.GetDirectoryName(typeof(TestPaths).Assembly.Location);
			while(dir!=null) {
				if(File.Exists(Path.Combine(dir,"OpenDental.sln"))) {
					return dir;
				}
				dir=Path.GetDirectoryName(dir);
			}
			throw new InvalidOperationException("Could not find the repository root (OpenDental.sln) above the test assembly.");
		}

		///<summary>A file under the repository root, for example GetRepoFile("OpenDental","Resources","PrefInfos.xml").</summary>
		public static string GetRepoFile(params string[] arrayParts) {
			string path=GetRepositoryRoot();
			for(int i=0;i<arrayParts.Length;i++) {
				path=Path.Combine(path,arrayParts[i]);
			}
			return path;
		}

		///<summary>A data file copied next to the test assembly (Data folder).</summary>
		public static string GetDataFile(string fileName) {
			string path=Path.Combine(Path.GetDirectoryName(typeof(TestPaths).Assembly.Location),"Data",fileName);
			if(File.Exists(path)) {
				return path;
			}
			//Fall back to the source folder, for test runners that do not copy content files.
			return GetRepoFile("tests","RegistryTests","Data",fileName);
		}
	}
}
