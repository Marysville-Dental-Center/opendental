// Repository paths and source-text helpers for the M2-APPT tests. The source checks read the UI files that cannot run without a window.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace M2APPTTests {
	public static class RepoFiles {
		///<summary>Walks up from the test assembly until it finds OpenDental.sln.</summary>
		public static string GetRoot() {
			string dir=Path.GetDirectoryName(typeof(RepoFiles).Assembly.Location);
			while(dir!=null) {
				if(File.Exists(Path.Combine(dir,"OpenDental.sln"))) {
					return dir;
				}
				dir=Path.GetDirectoryName(dir);
			}
			throw new InvalidOperationException("Could not find the repository root (OpenDental.sln) above the test assembly.");
		}

		///<summary>A file under the repository root, for example GetPath("OpenDental","Resources","PrefInfos.xml").</summary>
		public static string GetPath(params string[] arrayParts) {
			string path=GetRoot();
			for(int i=0;i<arrayParts.Length;i++) {
				path=Path.Combine(path,arrayParts[i]);
			}
			if(!File.Exists(path)) {
				throw new FileNotFoundException("Not found in the repository: "+path);
			}
			return path;
		}

		public static string Read(params string[] arrayParts) {
			return File.ReadAllText(GetPath(arrayParts));
		}

		///<summary>The text of a method, from its declaration to the next declaration at the same indent (a private or public member, a #region
		///or #endregion line). Good enough for the plain WinForms handlers these tests look at.</summary>
		public static string GetMethod(string source,string methodName) {
			Match match=Regex.Match(source,@"\n(\t+)(private|public|protected|internal)[^\n]*\b"+Regex.Escape(methodName)+@"\(");
			if(!match.Success) {
				throw new InvalidOperationException("Method not found: "+methodName);
			}
			string indent=match.Groups[1].Value;
			int start=match.Index+1;
			Match matchNext=new Regex(@"\n"+indent+@"(private|public|protected|internal|#region|#endregion)\b").Match(source,start+match.Length-1);
			int end=matchNext.Success ? matchNext.Index : source.Length;
			return source.Substring(start,end-start);
		}

		///<summary>The source without // comments, so a check for code is not fooled by a comment that names it.</summary>
		public static string StripLineComments(string source) {
			return Regex.Replace(source,@"//[^\n]*","");
		}
	}
}
