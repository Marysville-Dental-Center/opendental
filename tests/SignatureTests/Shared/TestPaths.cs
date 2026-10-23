using System;
using System.IO;

namespace SignatureTests {
	///<summary>Locates the repository.</summary>
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

		///<summary>A file under the repository root, for example GetRepoFile("OpenDental","Forms","FormLogOn.cs").</summary>
		public static string GetRepoFile(params string[] arrayParts) {
			string path=GetRepositoryRoot();
			for(int i=0;i<arrayParts.Length;i++) {
				path=Path.Combine(path,arrayParts[i]);
			}
			return path;
		}

		///<summary>The text of a source file under the repository root.</summary>
		public static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(GetRepoFile(arrayParts));
		}
	}
}
