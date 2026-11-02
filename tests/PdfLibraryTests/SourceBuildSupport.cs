// What the source-build tests share: the pins of the submodules, git and tools\Build-Release.ps1 run as child processes, the files an upstream
// SDK-style project compiles (its default globs, Compile Remove and linked files), and the files the build of third-party\build gave the
// compiler (obj\...\SourceBuildItems.txt).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PdfLibraryTests {
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
				Assert.Fail("Expected "+typeof(T).Name+" but got "+ex.GetType().Name+": "+ex.Message+" "+message);
			}
			Assert.Fail("Expected "+typeof(T).Name+" but nothing was thrown. "+message);
			return null;
		}
	}

	///<summary>The recorded pins and identities of the source builds (third-party\build\PDFsharp.props, WeCantSpell.Hunspell.csproj,
	///docs\third-party-source-builds.md). An upgrade changes them here on purpose.</summary>
	public static class SourceBuild {
		public const string PDFSHARP_PATH="third-party/PDFsharp";
		public const string PDFSHARP_URL="https://github.com/empira/PDFsharp.git";
		public const string PDFSHARP_TAG="v6.5.0";
		public const string PDFSHARP_COMMIT="96d5ef81d304d314228712e28e5d7ac3d5e29f86";
		public const string PDFSHARP_INFORMATIONAL_VERSION="6.5.0+96d5ef81d304.open-pms-dental-source-build";
		public const string PDFSHARP_PUBLIC_KEY_TOKEN="f94615aa0424f9eb";
		public const string WECANTSPELL_PATH="third-party/WeCantSpell.Hunspell";
		public const string WECANTSPELL_URL="https://github.com/aarondandy/WeCantSpell.Hunspell.git";
		public const string WECANTSPELL_TAG="7.0.1";
		public const string WECANTSPELL_COMMIT="ab5709d95b2d23541984d22baa0ab2d1e783582f";
		///<summary>The one upstream file the PDFsharp build replaces, relative to the submodule.</summary>
		public const string REPLACED_FILE="src/foundation/src/PDFsharp/src/PdfSharp/root/PdfSharpLicense.cs";
		///<summary>Its replacement, relative to the repository.</summary>
		public const string REPLACEMENT_FILE="third-party/build/PdfSharp-gdi/PdfSharpLicense.cs";

		private static string _repoRoot;

		public static string RepoRoot {
			get {
				if(_repoRoot==null) {
					_repoRoot=Path.GetDirectoryName(TestDb.FindRepoFile("NOTICE")).TrimEnd('\\');
				}
				return _repoRoot;
			}
		}

		public static string Combine(string relativePath) {
			return Path.Combine(RepoRoot,relativePath.Replace('/','\\'));
		}

		///<summary>The path relative to the repository root with forward slashes, or the full path when it is outside.</summary>
		public static string Relative(string path) {
			string full=Path.GetFullPath(path);
			if(full.StartsWith(RepoRoot+"\\",StringComparison.OrdinalIgnoreCase)) {
				return full.Substring(RepoRoot.Length+1).Replace('\\','/');
			}
			return full;
		}

		public class ProcessResult {
			public int ExitCode;
			public string Output="";
			public List<string> Lines=new List<string>();
		}

		///<summary>Runs the program with the arguments and returns its exit code and output (stdout and stderr).</summary>
		public static ProcessResult Run(string fileName,string arguments,string folder,int timeoutMilliseconds=300000) {
			ProcessStartInfo processStartInfo=new ProcessStartInfo(fileName,arguments);
			processStartInfo.WorkingDirectory=folder;
			processStartInfo.UseShellExecute=false;
			processStartInfo.RedirectStandardOutput=true;
			processStartInfo.RedirectStandardError=true;
			processStartInfo.CreateNoWindow=true;
			using(Process process=Process.Start(processStartInfo)) {
				Task<string> taskOut=process.StandardOutput.ReadToEndAsync();
				Task<string> taskErr=process.StandardError.ReadToEndAsync();
				if(!process.WaitForExit(timeoutMilliseconds)) {
					process.Kill();
					Assert.Fail(fileName+" "+arguments+" did not finish in "+(timeoutMilliseconds/1000)+" s.");
				}
				process.WaitForExit();
				ProcessResult processResult=new ProcessResult();
				processResult.ExitCode=process.ExitCode;
				processResult.Output=taskOut.Result+taskErr.Result;
				processResult.Lines=processResult.Output.Split(new[] { "\r\n","\n" },StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
				return processResult;
			}
		}

		///<summary>git with the arguments in the folder (the repository root by default). Inconclusive when git is not on the PATH.</summary>
		public static ProcessResult Git(string arguments,string folder=null) {
			try {
				return Run("git.exe",arguments,folder??RepoRoot,120000);
			}
			catch(System.ComponentModel.Win32Exception) {
				Assert.Inconclusive("git.exe is not on the PATH.");
				return null;
			}
		}

		///<summary>tools\Build-Release.ps1 with the arguments.</summary>
		public static ProcessResult BuildRelease(params string[] arrayArgs) {
			return Run("powershell.exe","-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Combine("tools/Build-Release.ps1")+"\" "
				+string.Join(" ",arrayArgs),RepoRoot);
		}

		private static List<KeyValuePair<string,string>> _listReleaseFiles;

		///<summary>The allow-list as tools\Build-Release.ps1 -ListFiles prints it: kind ("own", "third", "doc") and path.</summary>
		public static List<KeyValuePair<string,string>> ListReleaseFiles() {
			if(_listReleaseFiles==null) {
				ProcessResult processResult=BuildRelease("-ListFiles");
				Assert.AreEqual(0,processResult.ExitCode,processResult.Output);
				_listReleaseFiles=processResult.Lines.Select(x => x.Split('|')).Where(x => x.Length==2)
					.Select(x => new KeyValuePair<string,string>(x[0],x[1])).ToList();
			}
			return _listReleaseFiles;
		}

		///<summary>The files an SDK-style upstream project compiles (kind "Compile") or embeds ("EmbeddedResource"), as full paths: the .NET SDK's
		///default glob of the project folder (**\*.cs or **\*.resx, without bin and obj), then each item of the project in order, Remove taking
		///files out and Include (a file or a wildcard, also outside the folder, as linked files are) adding them.</summary>
		public static HashSet<string> GetUpstreamItems(string pathProject,string kind) {
			string folder=Path.GetDirectoryName(Path.GetFullPath(pathProject));
			HashSet<string> hashSetItems=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string defaultGlob=kind=="Compile" ? @"**\*.cs" : @"**\*.resx";
			foreach(string path in ExpandPattern(folder,defaultGlob)) {
				string relative=path.Substring(folder.Length+1);
				if(!relative.StartsWith(@"bin\",StringComparison.OrdinalIgnoreCase) && !relative.StartsWith(@"obj\",StringComparison.OrdinalIgnoreCase)) {
					hashSetItems.Add(path);
				}
			}
			XDocument xDocument=XDocument.Load(pathProject);
			foreach(XElement xElement in xDocument.Descendants().Where(x => x.Name.LocalName==kind)) {
				foreach(string pattern in ((string)xElement.Attribute("Remove")??"").Split(new[] { ';' },StringSplitOptions.RemoveEmptyEntries)) {
					foreach(string path in ExpandPattern(folder,pattern.Trim())) {
						hashSetItems.Remove(path);
					}
				}
				foreach(string pattern in ((string)xElement.Attribute("Include")??"").Split(new[] { ';' },StringSplitOptions.RemoveEmptyEntries)) {
					List<string> listPaths=ExpandPattern(folder,pattern.Trim());
					if(listPaths.Count==0) {
						Assert.Fail(Relative(pathProject)+" names a file that does not exist: "+pattern);
					}
					hashSetItems.UnionWith(listPaths);
				}
			}
			return hashSetItems;
		}

		///<summary>The files a project-relative pattern names: a file, "folder\*.ext", or "folder\**" / "folder\**\*.ext" (recursive).</summary>
		private static List<string> ExpandPattern(string folder,string pattern) {
			string full=Path.Combine(folder,pattern.Replace('/','\\'));
			int indexWildcard=full.IndexOf('*');
			if(indexWildcard<0) {
				string path=Path.GetFullPath(full);
				return File.Exists(path) ? new List<string> { path } : new List<string>();
			}
			string baseFolder=Path.GetFullPath(full.Substring(0,full.LastIndexOf('\\',indexWildcard)));
			string rest=full.Substring(full.LastIndexOf('\\',indexWildcard)+1);
			if(!Directory.Exists(baseFolder)) {
				return new List<string>();
			}
			if(rest.StartsWith("**")) {
				string filter=rest.Length>3 ? rest.Substring(3) : "*";
				return FilterExtension(Directory.GetFiles(baseFolder,filter,SearchOption.AllDirectories),filter);
			}
			return FilterExtension(Directory.GetFiles(baseFolder,rest,SearchOption.TopDirectoryOnly),rest);
		}

		///<summary>Windows matches "*.cs" against short names too; MSBuild's globs do not. Keeps the files whose name really ends so.</summary>
		private static List<string> FilterExtension(string[] arrayPaths,string filter) {
			string extension=filter.StartsWith("*.") ? filter.Substring(1) : null;
			return arrayPaths.Where(x => extension==null || x.EndsWith(extension,StringComparison.OrdinalIgnoreCase)).Select(Path.GetFullPath).ToList();
		}

		///<summary>The files the last build of the third-party\build project gave the compiler (kind "Compile") or the resource step
		///("EmbeddedResource"), from its obj\...\SourceBuildItems.txt: full path, and the manifest resource name for resources.</summary>
		public static List<string[]> GetBuiltItems(string projectName,string kind) {
			string folderObj=Combine("third-party/build/"+projectName+"/obj");
			FileInfo fileInfo=Directory.Exists(folderObj)
				? new DirectoryInfo(folderObj).GetFiles("SourceBuildItems.txt",SearchOption.AllDirectories).OrderByDescending(x => x.LastWriteTimeUtc).FirstOrDefault()
				: null;
			if(fileInfo==null) {
				Assert.Fail("third-party\\build\\"+projectName+" is not built (no obj\\...\\SourceBuildItems.txt). Build OpenDental.Local.slnf first.");
			}
			return File.ReadAllLines(fileInfo.FullName).Select(x => x.Split('|')).Where(x => x[0]==kind).Select(x => x.Skip(1).ToArray()).ToList();
		}
	}
}
