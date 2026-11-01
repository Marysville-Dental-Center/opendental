using System;
using System.IO;
using System.Text.RegularExpressions;

namespace BSHELLTests {
	///<summary>Locates the repository and reads its source files, for the tests that check how windows are wired (no window is ever shown).</summary>
	public static class TestPaths {
		///<summary>Walks up from the test assembly until it finds the repository root (LICENSE and NOTICE).</summary>
		public static string GetRepositoryRoot() {
			string dir=Path.GetDirectoryName(typeof(TestPaths).Assembly.Location);
			while(dir!=null) {
				if(File.Exists(Path.Combine(dir,"LICENSE")) && File.Exists(Path.Combine(dir,"NOTICE"))) {
					return dir;
				}
				dir=Path.GetDirectoryName(dir);
			}
			throw new InvalidOperationException("Could not find the repository root (LICENSE and NOTICE) above the test assembly.");
		}

		public static string GetRepoFile(params string[] arrayParts) {
			string path=GetRepositoryRoot();
			for(int i=0;i<arrayParts.Length;i++) {
				path=Path.Combine(path,arrayParts[i]);
			}
			return path;
		}

		public static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(GetRepoFile(arrayParts));
		}

		///<summary>The body of the first method whose declaration contains signature, from its opening brace to the matching closing brace.</summary>
		public static string GetMethodBody(string source,string signature) {
			int index=source.IndexOf(signature,StringComparison.Ordinal);
			if(index<0) {
				throw new InvalidOperationException("Not found: "+signature);
			}
			int indexOpen=source.IndexOf('{',index);
			int depth=0;
			for(int i=indexOpen;i<source.Length;i++) {
				if(source[i]=='{') {
					depth++;
				}
				else if(source[i]=='}') {
					depth--;
					if(depth==0) {
						return source.Substring(indexOpen,i-indexOpen+1);
					}
				}
			}
			throw new InvalidOperationException("Unbalanced braces after: "+signature);
		}

		public static int Count(string source,string text) {
			return Regex.Matches(source,Regex.Escape(text)).Count;
		}
	}
}
