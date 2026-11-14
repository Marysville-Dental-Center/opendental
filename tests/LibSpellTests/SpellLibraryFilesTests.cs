// NHunspell 0.9.6 and its native Hunspell 1.2 (Hunspellx86.dll, Hunspellx64.dll) are gone from the repository, the projects, the sources and the
// build output; WeCantSpell.Hunspell 7.0.1 (managed) is the one spell checker library, used only through WpfControls.SpellCheckL, which both
// boxes call. It is built from its source: the git submodule third-party\WeCantSpell.Hunspell (GitHub, tag 7.0.1) compiled by
// third-party\build\WeCantSpell.Hunspell, the library folder whole and nothing else, with upstream's assembly identity and a marked build; no
// DLL of it is in "Required dlls".
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectFileTests;
using WeCantSpell.Hunspell;

namespace LibSpellTests {
	[TestClass]
	public class SpellLibraryFilesTests {
		private static readonly string[] _arrayRemovedFiles=new string[] { "NHunspell.dll","Hunspellx86.dll","Hunspellx64.dll" };
		private const string SUBMODULE_PATH="third-party/WeCantSpell.Hunspell";
		private const string SUBMODULE_URL="https://github.com/aarondandy/WeCantSpell.Hunspell.git";
		private const string TAG="7.0.1";
		private const string COMMIT="ab5709d95b2d23541984d22baa0ab2d1e783582f";
		///<summary>The informational version of the build here: upstream's version, the commit and the mark of this build.</summary>
		private const string INFORMATIONAL_VERSION="7.0.1+ab5709d95b2d.open-pms-dental-source-build";

		private static string Sha256(string path) {
			using(SHA256 sha256=SHA256.Create()) {
				return string.Concat(sha256.ComputeHash(File.ReadAllBytes(path)).Select(x => x.ToString("x2")));
			}
		}

		///<summary>git with the arguments in the folder; its output, or Inconclusive when git is not on the PATH.</summary>
		private static string Git(string arguments,string folder) {
			ProcessStartInfo processStartInfo=new ProcessStartInfo("git.exe",arguments);
			processStartInfo.WorkingDirectory=folder;
			processStartInfo.UseShellExecute=false;
			processStartInfo.RedirectStandardOutput=true;
			processStartInfo.RedirectStandardError=true;
			processStartInfo.CreateNoWindow=true;
			try {
				using(Process process=Process.Start(processStartInfo)) {
					System.Threading.Tasks.Task<string> taskErr=process.StandardError.ReadToEndAsync();
					string output=process.StandardOutput.ReadToEnd();
					process.WaitForExit();
					Assert.AreEqual(0,process.ExitCode,"git "+arguments+": "+output+taskErr.Result);
					return output.Trim();
				}
			}
			catch(System.ComponentModel.Win32Exception) {
				Assert.Inconclusive("git.exe is not on the PATH.");
				return null;
			}
		}

		[TestMethod]
		public void RequiredDlls_HoldNoSpellCheckerDll() {
			string folder=RepoPaths.Combine("Required dlls");
			List<string> listLeft=_arrayRemovedFiles.Concat(new[] { "WeCantSpell.Hunspell.dll" }).Where(x => File.Exists(Path.Combine(folder,x))).ToList();
			Assert.AreEqual(0,listLeft.Count,"In Required dlls: "+string.Join(", ",listLeft));
		}

		[TestMethod]
		public void BuiltAssembly_IsWeCantSpell701_BuiltFromTheSource() {
			//The assembly the tests load: the output of third-party\build\WeCantSpell.Hunspell, managed, no native file.
			Assembly assembly=typeof(WordList).Assembly;
			Assert.AreEqual(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"WeCantSpell.Hunspell.dll"),assembly.Location,true);
			Assert.AreEqual(new Version(7,0,0,0),assembly.GetName().Version,"Upstream's assembly version (MinVer: the major version), as the NuGet file had.");
			Assert.AreEqual(0,assembly.GetName().GetPublicKeyToken().Length,"No strong name, as upstream's.");
			FileVersionInfo fileVersionInfo=FileVersionInfo.GetVersionInfo(assembly.Location);
			Assert.AreEqual("7.0.1.0",fileVersionInfo.FileVersion);
			Assert.AreEqual(INFORMATIONAL_VERSION,fileVersionInfo.ProductVersion);
			Assert.AreEqual(INFORMATIONAL_VERSION,assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
			Dictionary<string,string> dictMetadata=assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(x => x.Key,x => x.Value);
			Assert.AreEqual(COMMIT,dictMetadata["SourceCommit"]);
			Assert.AreEqual(TAG,dictMetadata["SourceTag"]);
			Assert.AreEqual("https://github.com/aarondandy/WeCantSpell.Hunspell",dictMetadata["SourceRepository"]);
			Assert.AreEqual(@"Open PMS - Dental, third-party\build\WeCantSpell.Hunspell",dictMetadata["SourceBuild"]);
			Assert.AreEqual(".NETFramework,Version=v4.8",assembly.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName);
			Assert.AreEqual("aarondandy",assembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company);
			CollectionAssert.AreEquivalent(new[] { "mscorlib","System.Core","System.Memory","System.Buffers","System.Runtime.CompilerServices.Unsafe" },
				assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray());
		}

		[TestMethod]
		public void Submodule_IsTheGitHubProject_PinnedToTag701_AndTheBuildCompilesTheLibraryFolderWhole() {
			string gitModules=RepoPaths.ReadText(".gitmodules");
			Match match=Regex.Match(gitModules,@"\[submodule """+Regex.Escape(SUBMODULE_PATH)+@"""\]\s*path\s*=\s*([^\r\n]+)\s*url\s*=\s*([^\r\n]+)");
			Assert.IsTrue(match.Success,gitModules);
			Assert.AreEqual(SUBMODULE_PATH,match.Groups[1].Value.Trim());
			Assert.AreEqual(SUBMODULE_URL,match.Groups[2].Value.Trim());
			Assert.AreEqual("160000 commit "+COMMIT+"\t"+SUBMODULE_PATH,Git("ls-tree HEAD -- "+SUBMODULE_PATH,RepoPaths.Root),"The pin.");
			string folderSubmodule=RepoPaths.Combine(SUBMODULE_PATH.Replace('/','\\'));
			Assert.AreEqual(COMMIT,Git("rev-parse HEAD",folderSubmodule),"Checked out at the pin.");
			Assert.AreEqual(COMMIT,Git("rev-parse "+TAG+"^{commit}",folderSubmodule),"The pin is the commit of the tag "+TAG+".");
			Assert.AreEqual("",Git("status --porcelain",folderSubmodule),"Nothing inside the submodule is changed.");
			//What the build compiled (obj\...\SourceBuildItems.txt): every .cs of the library folder, as upstream's project does by default, and
			//nothing of the build folder but the generated assembly attributes. No other file of the repository is used.
			string folderLibrary=Path.Combine(folderSubmodule,"WeCantSpell.Hunspell");
			List<string> listUpstream=Directory.GetFiles(folderLibrary,"*.cs",SearchOption.AllDirectories).Where(x => x.EndsWith(".cs"))
				.Where(x => !x.Substring(folderLibrary.Length).StartsWith(@"\bin\") && !x.Substring(folderLibrary.Length).StartsWith(@"\obj\"))
				.Select(x => Path.GetFullPath(x).ToLowerInvariant()).ToList();
			string folderObj=RepoPaths.Combine("third-party","build","WeCantSpell.Hunspell","obj");
			FileInfo fileInfo=new DirectoryInfo(folderObj).GetFiles("SourceBuildItems.txt",SearchOption.AllDirectories).OrderByDescending(x => x.LastWriteTimeUtc)
				.FirstOrDefault();
			Assert.IsNotNull(fileInfo,"third-party\\build\\WeCantSpell.Hunspell is not built.");
			List<string> listCompiled=File.ReadAllLines(fileInfo.FullName).Where(x => x.StartsWith("Compile|")).Select(x => Path.GetFullPath(x.Substring(8)).ToLowerInvariant())
				.ToList();
			CollectionAssert.AreEquivalent(listUpstream.ToArray(),listCompiled.Where(x => x.StartsWith(folderLibrary.ToLowerInvariant()+"\\")).ToArray());
			CollectionAssert.AreEquivalent(new[] { "sourcebuildinfo.g.cs",".netframework,version=v4.8.assemblyattributes.cs" },
				listCompiled.Where(x => !x.StartsWith(folderLibrary.ToLowerInvariant()+"\\")).Select(x => Path.GetFileName(x)).ToArray(),
				"Besides the library folder, only the generated attributes.");
			Assert.IsFalse(File.ReadAllLines(fileInfo.FullName).Any(x => x.StartsWith("EmbeddedResource|")),"No resources, as upstream.");
			//The project file: no WeCantSpell package, no MinVer, not strong-named; the support assemblies from their packages (S2-1 of the .NET 10
			//move: the files Required dlls held); in the solution and the filter. Its text and its evaluated properties and items, so the checks hold
			//for the classic project file and the SDK-style one of the .NET 10 move alike (an SDK-style wrapper is fine; the library's NuGet
			//package, MinVer or a signature is not).
			string project=RepoPaths.ReadText("third-party","build","WeCantSpell.Hunspell","WeCantSpell.Hunspell.csproj");
			foreach(string forbidden in new[] { "MinVer\"","<SignAssembly>","HintPath" }) {
				Assert.IsFalse(project.Contains(forbidden),"WeCantSpell.Hunspell.csproj: "+forbidden);
			}
			ProjectFile projectFile=ProjectFile.Load(RepoPaths.Combine("third-party","build","WeCantSpell.Hunspell","WeCantSpell.Hunspell.csproj"));
			Assert.AreEqual(0,projectFile.GetItems("PackageDownload").Count,"WeCantSpell.Hunspell.csproj downloads a package.");
			CollectionAssert.AreEquivalent(new[] { "System.Buffers","System.Memory","System.Runtime.CompilerServices.Unsafe" },
				projectFile.GetItems("PackageReference").Select(x => x.Include).ToArray(),"WeCantSpell.Hunspell.csproj asks NuGet only for the support assemblies.");
			foreach(string package in new[] { "System.Buffers","System.Memory","System.Runtime.CompilerServices.Unsafe" }) {
				Assert.IsFalse(string.IsNullOrEmpty(projectFile.GetPackageVersion(package)),package+": the version of Directory.Packages.props.");
			}
			Assert.AreNotEqual("true",projectFile.GetProperty("SignAssembly").ToLowerInvariant(),"WeCantSpell.Hunspell.csproj is not signed.");
			Assert.AreEqual(COMMIT,projectFile.GetProperty("SourceBuildCommit"));
			Assert.AreEqual(INFORMATIONAL_VERSION,projectFile.GetProperty("SourceBuildInformationalVersion"));
			StringAssert.Contains(RepoPaths.ReadText("OpenDental.sln"),@"""WeCantSpell.Hunspell"", ""third-party\build\WeCantSpell.Hunspell\WeCantSpell.Hunspell.csproj""");
			StringAssert.Contains(RepoPaths.ReadText("OpenDental.Local.slnf"),@"""third-party\\build\\WeCantSpell.Hunspell\\WeCantSpell.Hunspell.csproj""");
		}

		[TestMethod]
		public void Notices_SayItIsBuiltFromSource_UnderTheLgpl_WithNoMplText() {
			string notices=RepoPaths.ReadText("THIRD-PARTY-NOTICES.txt");
			Match match=Regex.Match(notices,@"(?ms)^--- Component: WeCantSpell\.Hunspell\r?\n(.*?)(?=^--- )");
			Assert.IsTrue(match.Success);
			string section=Regex.Replace(match.Groups[1].Value,@"\s+"," ");
			StringAssert.Contains(section,"built by this project from its source, unmodified: the git submodule third-party/WeCantSpell.Hunspell at tag 7.0.1 (commit "+COMMIT+")");
			StringAssert.Contains(section,"This release uses it under the GPL / LGPL: it distributes it under the GNU Lesser General Public License, version 2.1 or later "
				+"(LICENSE-LGPL-2.1.txt), which also allows its use under the GNU General Public License, version 2 or later (LICENSE). No MPL 1.1 text is needed.");
			StringAssert.Contains(section,"as built, in the source archive of each release");
			Assert.IsFalse(section.Contains("nuget.org"),"Not the NuGet file any more.");
			//The LGPL 2.1 text ships with the release; no MPL text does.
			string script=RepoPaths.ReadText("tools","Build-Release.ps1");
			StringAssert.Contains(script,"'LICENSE-LGPL-2.1.txt'='LICENSE-LGPL-2.1.txt'");
			Assert.IsFalse(Regex.IsMatch(script,@"MPL[^ ]*\.txt"),"No MPL text is staged.");
			Assert.IsFalse(Directory.GetFiles(RepoPaths.Root,"*MPL*").Any(),"No MPL text in the repository root.");
			StringAssert.Contains(script,@"$ThirdPartyFiles[$name+'.dll']='third-party\build\'+$name+'\bin\Release'");
		}

		[TestMethod]
		public void Projects_NameNoNHunspellFile_AndOnlyWpfControlsReferencesWeCantSpell() {
			List<string> listProblems=new List<string>();
			List<string> listUsers=new List<string>();
			foreach(string path in RepoPaths.Files(".csproj").Concat(RepoPaths.Files(".vcxproj")).Concat(RepoPaths.Files(".targets"))
				.Concat(RepoPaths.Files(".props")))
			{
				if(RepoPaths.IsInSubmodule(path)) {
					continue;//upstream's own projects (WeCantSpell's NHunspell benchmark among them) are not built
				}
				XDocument xDocument=XDocument.Load(path);
				foreach(XElement xElement in xDocument.Descendants().Where(x => x.Name.LocalName=="ProjectReference")) {
					if(((string)xElement.Attribute("Include")??"").EndsWith(@"third-party\build\WeCantSpell.Hunspell\WeCantSpell.Hunspell.csproj",StringComparison.OrdinalIgnoreCase)) {
						listUsers.Add(RepoPaths.Relative(path));
					}
				}
				foreach(XElement xElement in xDocument.Descendants()) {
					string local=xElement.Name.LocalName;
					List<string> listValues=new List<string>();
					if(local=="Reference" || local=="None" || local=="Content") {
						listValues.Add((string)xElement.Attribute("Include")??"");
					}
					if(local=="HintPath") {
						listValues.Add(xElement.Value);
					}
					//The attribute form of the metadata, which an SDK-style project file may use.
					if(xElement.Attribute("HintPath")!=null) {
						listValues.Add((string)xElement.Attribute("HintPath"));
					}
					foreach(string value in listValues) {
						string fileName=Path.GetFileName(value.Split(',')[0].Trim().Replace('/','\\'));
						if(fileName.StartsWith("NHunspell",StringComparison.OrdinalIgnoreCase)
							|| fileName.StartsWith("Hunspellx",StringComparison.OrdinalIgnoreCase))
						{
							listProblems.Add(RepoPaths.Relative(path)+": <"+local+"> "+value.Trim());
						}
						if(fileName.StartsWith("WeCantSpell",StringComparison.OrdinalIgnoreCase) && local!="None") {
							listProblems.Add(RepoPaths.Relative(path)+": <"+local+"> "+value.Trim()+" (the library is a project of third-party\\build)");
						}
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,"NHunspell, or a WeCantSpell DLL, is named: "+string.Join("; ",listProblems));
			//The program code reaches the library only through WpfControlsOD; this test project reads the dictionary with it too.
			CollectionAssert.AreEquivalent(new[] { @"WpfControlsOD\WpfControlsOD.csproj",@"tests\LibSpellTests\LibSpellTests.csproj" },listUsers.ToArray());
		}

		[TestMethod]
		public void Sources_UseNoNHunspell_AndOnlySpellCheckLReadsADictionary() {
			//A using directive, a constructed NHunspell object or a qualified type name (NHunspell.Hunspell; not the file name NHunspell.dll in a
			//list of denied files).
			Regex regexNHunspell=new Regex(@"^\s*using\s+NHunspell\s*;|\bnew\s+Hunspell\s*\(|\bNHunspell\.[A-Z]",RegexOptions.Multiline);
			//A using directive, a qualified type name (WeCantSpell.Hunspell.WordList; not the file name WeCantSpell.Hunspell.dll in a list) or a
			//dictionary read.
			Regex regexWeCantSpell=new Regex(@"^\s*using\s+WeCantSpell\b|\bWeCantSpell\.Hunspell\.[A-Z]|\bWordList\.Create",RegexOptions.Multiline);
			List<string> listProblems=new List<string>();
			List<string> listUsers=new List<string>();
			List<string> listSources=RepoPaths.Files(".cs");
			Assert.IsTrue(listSources.Count>1000,"The sources were not found under "+RepoPaths.Root);
			foreach(string path in listSources) {
				string relative=RepoPaths.Relative(path);
				if(relative.StartsWith(@"tests\LibSpellTests\",StringComparison.OrdinalIgnoreCase) || RepoPaths.IsInSubmodule(path)) {
					continue;//the library's own source (and upstream's tests and benchmarks) is not the program's
				}
				string text=File.ReadAllText(path);
				if(text.IndexOf("Hunspell",StringComparison.Ordinal)<0 && text.IndexOf("WordList",StringComparison.Ordinal)<0
					&& text.IndexOf("WeCantSpell",StringComparison.Ordinal)<0)
				{
					continue;//most files: neither regex can match
				}
				if(regexNHunspell.IsMatch(text)) {
					listProblems.Add(relative);
				}
				if(regexWeCantSpell.IsMatch(text)) {
					listUsers.Add(relative);
				}
			}
			Assert.AreEqual(0,listProblems.Count,"Source uses NHunspell: "+string.Join("; ",listProblems));
			CollectionAssert.AreEqual(new[] { @"WpfControlsOD\Logic\SpellCheckL.cs" },listUsers.ToArray(),"Only SpellCheckL reads a dictionary.");
		}

		[TestMethod]
		public void Boxes_SpellCheckThroughSpellCheckL() {
			string odTextBox=RepoPaths.ReadText("OpenPmsDental","User Controls","ODtextBox.cs");
			string textRich=RepoPaths.ReadText("WpfControlsOD","UI","Controls","TextRich.xaml.cs");
			foreach(string source in new[] { odTextBox,textRich }) {
				//Created: the dictionary read starts on the thread pool; never a dictionary of the box's own.
				StringAssert.Contains(source,"SpellCheckL.ReadInBackground();");
				StringAssert.Contains(source,"SpellCheckL.Suggest(");
				StringAssert.Contains(source,"SpellCheckL.AddCustomWord(");
				Assert.IsFalse(source.Contains("HunspellGlobal"));
				Assert.IsFalse(source.Contains("DictCustoms.Insert("),"Adding a word goes through SpellCheckL.AddCustomWord.");
				Assert.IsFalse(source.Contains("Required dlls"),"No native file is copied at run time.");
			}
			StringAssert.Contains(odTextBox,"SpellCheckL.IsInDictionary(");
			StringAssert.Contains(odTextBox,"SpellCheckL.IsCustomWord(");
			StringAssert.Contains(textRich,"SpellCheckL.GetMisspelledWords(run,dictCorrect)");
		}

		[TestMethod]
		public void BuildOutput_HoldsWeCantSpell_AndNoNHunspellFile() {
			List<string> listFolders=new List<string> { AppDomain.CurrentDomain.BaseDirectory };
			foreach(string relative in new[] { @"OpenPmsDental\bin\Debug",@"WpfControlsOD\bin\Debug" }) {
				string folder=RepoPaths.Combine(relative);
				if(Directory.Exists(folder)) {
					listFolders.Add(folder);
				}
			}
			//Every copy is the file built from source: the same bytes as the project's output.
			string pathBuilt=RepoPaths.Combine("third-party","build","WeCantSpell.Hunspell","bin","Debug","WeCantSpell.Hunspell.dll");
			Assert.IsTrue(File.Exists(pathBuilt),"third-party\\build\\WeCantSpell.Hunspell is not built.");
			string sha256Built=Sha256(pathBuilt);
			Assert.AreEqual(INFORMATIONAL_VERSION,FileVersionInfo.GetVersionInfo(pathBuilt).ProductVersion);
			List<string> listProblems=new List<string>();
			foreach(string folder in listFolders) {
				listProblems.AddRange(_arrayRemovedFiles.Where(x => File.Exists(Path.Combine(folder,x))).Select(x => Path.Combine(folder,x)+" is there."));
				string path=Path.Combine(folder,"WeCantSpell.Hunspell.dll");
				if(!File.Exists(path)) {
					listProblems.Add(path+" is missing.");
				}
				else if(Sha256(path)!=sha256Built) {
					listProblems.Add(path+" is not the file built from source ("+FileVersionInfo.GetVersionInfo(path).ProductVersion+").");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}
	}
}
