// PDFsharp and MigraDoc are referenced as a git submodule, not copied: .gitmodules names empira's GitHub project, the pin is the commit of tag
// v6.5.0 and the checkout is unchanged; the project files of third-party\build compile exactly what upstream's project files compile, from the
// submodule paths, except the one replaced file; they need no NuGet restore, no GitVersion and no internet, and are in the solution and the
// filter. WeCantSpell.Hunspell's submodule is checked here too (its build is checked by LibSpellTests).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectFileTests;

namespace PdfLibraryTests {
	[TestClass]
	public class SourceBuildTests {
		///<summary>Each PDFsharp project of third-party\build, the upstream project it builds as (relative to src\foundation\src of the submodule), and
		///the files of the build folder it compiles besides upstream's (relative to third-party\build).</summary>
		private static readonly (string Project,string Upstream,string[] OwnFiles)[] _arrayProjects=new[] {
			("PdfSharp.Shared",@"shared\src\PdfSharp.Shared\PdfSharp.Shared.csproj",new[] { "PDFsharp/GlobalUsings.cs" }),
			("PdfSharp.System",@"shared\src\PdfSharp.System\PdfSharp.System.csproj",new[] { "PDFsharp/GlobalUsings.cs" }),
			("PdfSharp-gdi",@"PDFsharp\src\PdfSharp-gdi\PdfSharp-gdi.csproj",new[] { "PDFsharp/GlobalUsings.cs","PDFsharp/GlobalUsings.WindowsForms.cs",
				"PdfSharp-gdi/PdfSharpLicense.cs","PdfSharp-gdi/LoggerMessages.cs" }),
			("PdfSharp.Charting-gdi",@"PDFsharp\src\PdfSharp.Charting-gdi\PdfSharp.Charting-gdi.csproj",new[] { "PDFsharp/GlobalUsings.cs",
				"PDFsharp/GlobalUsings.WindowsForms.cs" }),
			("MigraDoc.DocumentObjectModel",@"MigraDoc\src\MigraDoc.DocumentObjectModel\MigraDoc.DocumentObjectModel.csproj",new[] { "PDFsharp/GlobalUsings.cs",
				"MigraDoc.DocumentObjectModel/LoggerMessages.cs" }),
			("MigraDoc.Rendering-gdi",@"MigraDoc\src\MigraDoc.Rendering-gdi\MigraDoc.Rendering-gdi.csproj",new[] { "PDFsharp/GlobalUsings.cs" }),
		};
		///<summary>The resources of empira's 6.5.0 NuGet files, by assembly. The build must embed the same, under the same names.</summary>
		private static readonly Dictionary<string,string[]> _dictResources=new Dictionary<string,string[]> {
			{ "PdfSharp-gdi",new[] { "PdfSharp.Forms.PagePreview.resources","PdfSharp.Forms.PagePreviewCanvas.resources","PdfSharp.Resources.sRGB2014.icc" } },
			{ "MigraDoc.Rendering-gdi",new[] { "MigraDoc.Rendering.Forms.DocumentPreview.resources" } },
		};

		private static string SubmoduleFolder(string relativePath) {
			return SourceBuild.Combine(relativePath);
		}

		[TestMethod]
		public void GitModules_NameTheGitHubProjects() {
			string gitModules=File.ReadAllText(SourceBuild.Combine(".gitmodules"));
			List<(string Name,string Path,string Url)> listModules=new List<(string,string,string)>();
			foreach(Match match in Regex.Matches(gitModules,@"\[submodule ""([^""]+)""\]\s*((?:\s*\w+\s*=\s*[^\r\n]+)+)")) {
				Dictionary<string,string> dictKeys=Regex.Matches(match.Groups[2].Value,@"(\w+)\s*=\s*([^\r\n]+)").Cast<Match>()
					.ToDictionary(x => x.Groups[1].Value,x => x.Groups[2].Value.Trim());
				listModules.Add((match.Groups[1].Value,dictKeys["path"],dictKeys["url"]));
			}
			CollectionAssert.AreEqual(new[] { SourceBuild.PDFSHARP_PATH+"|"+SourceBuild.PDFSHARP_PATH+"|"+SourceBuild.PDFSHARP_URL,
				SourceBuild.WECANTSPELL_PATH+"|"+SourceBuild.WECANTSPELL_PATH+"|"+SourceBuild.WECANTSPELL_URL },
				listModules.Select(x => x.Name+"|"+x.Path+"|"+x.Url).ToArray(),gitModules);
			//The repository holds no copy of the upstream source next to the submodules.
			CollectionAssert.AreEquivalent(new[] { "build","PDFsharp","WeCantSpell.Hunspell" },
				Directory.GetDirectories(SourceBuild.Combine("third-party")).Select(Path.GetFileName).ToArray());
		}

		[TestMethod]
		public void Pins_AreTheCommitsOfTheReleaseTags_CheckedOutUnchanged() {
			foreach((string path,string tag,string commit) in new[] { (SourceBuild.PDFSHARP_PATH,SourceBuild.PDFSHARP_TAG,SourceBuild.PDFSHARP_COMMIT),
				(SourceBuild.WECANTSPELL_PATH,SourceBuild.WECANTSPELL_TAG,SourceBuild.WECANTSPELL_COMMIT) })
			{
				SourceBuild.ProcessResult processResult=SourceBuild.Git("ls-tree HEAD -- "+path);
				Assert.AreEqual(0,processResult.ExitCode,processResult.Output);
				Assert.AreEqual("160000 commit "+commit+"\t"+path,processResult.Output.Trim(),"The release commit pins "+path+" to the tag "+tag+".");
				string folder=SubmoduleFolder(path);
				Assert.IsTrue(File.Exists(Path.Combine(folder,".git")) || Directory.Exists(Path.Combine(folder,".git")),
					path+" is not checked out: git -c protocol.file.allow=always submodule update --init");
				processResult=SourceBuild.Git("rev-parse HEAD",folder);
				Assert.AreEqual(commit,processResult.Output.Trim(),path+" is checked out at its pin.");
				processResult=SourceBuild.Git("rev-parse "+tag+"^{commit}",folder);
				Assert.AreEqual(0,processResult.ExitCode,"The tag "+tag+" is in the submodule's repository: "+processResult.Output);
				Assert.AreEqual(commit,processResult.Output.Trim(),"The pin is the commit of the tag "+tag+".");
				processResult=SourceBuild.Git("status --porcelain",folder);
				Assert.AreEqual(0,processResult.ExitCode,processResult.Output);
				Assert.AreEqual("",processResult.Output.Trim(),"Nothing inside "+path+" is changed.");
			}
			//The recorded pins agree.
			string props=File.ReadAllText(SourceBuild.Combine("third-party/build/PDFsharp.props"));
			StringAssert.Contains(props,"<SourceBuildTag>"+SourceBuild.PDFSHARP_TAG+"</SourceBuildTag>");
			StringAssert.Contains(props,"<SourceBuildCommit>"+SourceBuild.PDFSHARP_COMMIT+"</SourceBuildCommit>");
			StringAssert.Contains(props,"<SourceBuildInformationalVersion>"+SourceBuild.PDFSHARP_INFORMATIONAL_VERSION+"</SourceBuildInformationalVersion>");
			//The evaluated properties, so the check holds for the classic project file and the SDK-style one of the .NET 10 move alike.
			ProjectFile projectFileSpell=ProjectFile.Load(SourceBuild.Combine("third-party/build/WeCantSpell.Hunspell/WeCantSpell.Hunspell.csproj"));
			Assert.AreEqual(SourceBuild.WECANTSPELL_TAG,projectFileSpell.GetProperty("SourceBuildTag"));
			Assert.AreEqual(SourceBuild.WECANTSPELL_COMMIT,projectFileSpell.GetProperty("SourceBuildCommit"));
			foreach(string file in new[] { "THIRD-PARTY-NOTICES.txt","NOTICE","docs/third-party-source-builds.md" }) {
				StringAssert.Contains(File.ReadAllText(SourceBuild.Combine(file)),SourceBuild.PDFSHARP_COMMIT,file);
			}
			foreach(string file in new[] { "THIRD-PARTY-NOTICES.txt","docs/third-party-source-builds.md" }) {
				StringAssert.Contains(File.ReadAllText(SourceBuild.Combine(file)),SourceBuild.WECANTSPELL_COMMIT,file);
			}
		}

		[TestMethod]
		public void ProjectFiles_CompileWhatUpstreamCompiles_FromTheSubmodule_ExceptTheOneReplacedFile() {
			string submodule=SubmoduleFolder(SourceBuild.PDFSHARP_PATH);
			string sourceRoot=Path.Combine(submodule,@"src\foundation\src");
			string buildFolder=SourceBuild.Combine("third-party/build");
			List<string> listProblems=new List<string>();
			List<string> listLeftOut=new List<string>();
			foreach((string project,string upstream,string[] arrayOwnFiles) in _arrayProjects) {
				HashSet<string> hashSetUpstream=SourceBuild.GetUpstreamItems(Path.Combine(sourceRoot,upstream),"Compile");
				List<string> listBuilt=SourceBuild.GetBuiltItems(project,"Compile").Select(x => Path.GetFullPath(x[0])).ToList();
				string folderObj=Path.Combine(buildFolder,project,"obj")+"\\";
				List<string> listFromSubmodule=listBuilt.Where(x => x.StartsWith(submodule+"\\",StringComparison.OrdinalIgnoreCase)).ToList();
				List<string> listGenerated=listBuilt.Where(x => x.StartsWith(folderObj,StringComparison.OrdinalIgnoreCase)).ToList();
				List<string> listOwn=listBuilt.Except(listFromSubmodule).Except(listGenerated).ToList();
				foreach(string path in listFromSubmodule.Where(x => !hashSetUpstream.Contains(x))) {
					listProblems.Add(project+" compiles "+SourceBuild.Relative(path)+", which upstream's "+Path.GetFileName(upstream)+" does not.");
				}
				foreach(string path in hashSetUpstream.Where(x => !listFromSubmodule.Contains(x,StringComparer.OrdinalIgnoreCase))) {
					listLeftOut.Add(project+": "+path.Substring(submodule.Length+1).Replace('\\','/'));
				}
				CollectionAssert.AreEquivalent(arrayOwnFiles.Select(x => Path.GetFullPath(Path.Combine(buildFolder,x.Replace('/','\\'))).ToLowerInvariant()).ToArray(),
					listOwn.Select(x => x.ToLowerInvariant()).ToArray(),project+": the files of the build folder.");
				CollectionAssert.AreEquivalent(new[] { "SourceBuildInfo.g.cs",".NETFramework,Version=v4.8.AssemblyAttributes.cs" },
					listGenerated.Select(Path.GetFileName).ToArray(),project+": the generated files.");
				//Resources: the same files as upstream, under the names of empira's files.
				HashSet<string> hashSetUpstreamResources=SourceBuild.GetUpstreamItems(Path.Combine(sourceRoot,upstream),"EmbeddedResource");
				List<string> listBuiltResources=SourceBuild.GetBuiltItems(project,"EmbeddedResource").Select(x => Path.GetFullPath(x[0])).ToList();
				CollectionAssert.AreEquivalent(hashSetUpstreamResources.Select(x => x.ToLowerInvariant()).ToArray(),
					listBuiltResources.Select(x => x.ToLowerInvariant()).ToArray(),project+": the embedded resources.");
				Assembly assembly=Assembly.Load(new AssemblyName(project+", Version=6.5.0.0, Culture=neutral, PublicKeyToken="+SourceBuild.PDFSHARP_PUBLIC_KEY_TOKEN));
				CollectionAssert.AreEquivalent(_dictResources.TryGetValue(project,out string[] arrayResources) ? arrayResources : new string[0],
					assembly.GetManifestResourceNames(),project+": the manifest resource names.");
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			//Exactly one upstream file is not compiled, the wrapper around the closed licence library, and its replacement is.
			CollectionAssert.AreEqual(new[] { "PdfSharp-gdi: "+SourceBuild.REPLACED_FILE },listLeftOut.ToArray(),string.Join("\r\n",listLeftOut));
			Assert.IsTrue(File.Exists(Path.Combine(submodule,SourceBuild.REPLACED_FILE.Replace('/','\\'))),"The replaced file is still in the submodule.");
			//The project file itself, evaluated (either project format): the upstream file left out, the replacement compiled.
			ProjectFile projectFileGdi=ProjectFile.Load(Path.Combine(buildFolder,"PdfSharp-gdi","PdfSharp-gdi.csproj"));
			string pathReplaced=Path.Combine(submodule,SourceBuild.REPLACED_FILE.Replace('/','\\'));
			Assert.AreEqual(pathReplaced,Path.GetFullPath(projectFileGdi.GetProperty("PdfSharpCoreFolder")+@"root\PdfSharpLicense.cs"),true);
			projectFileGdi.AssertDoesNotCompile(pathReplaced);
			projectFileGdi.AssertCompiles("PdfSharpLicense.cs");
			Assert.AreEqual(SourceBuild.Combine(SourceBuild.REPLACEMENT_FILE),projectFileGdi.AssertItem("Compile","PdfSharpLicense.cs").FullPath,true);
		}

		[TestMethod]
		public void Outputs_HoldTheSupportAssembliesOfRequiredDlls_NotOlderCopies() {
			//Reference resolution must take the support assemblies (System.Memory, Microsoft.Extensions.Logging.Abstractions, ...) from "Required
			//dlls", not older copies of the exact versions a dependency was built against (Visual Studio's test platform folder has some).
			string folderRequired=SourceBuild.Combine("Required dlls");
			List<string> listProblems=new List<string>();
			int countFolders=0;
			foreach(string project in _arrayProjects.Select(x => x.Project).Concat(new[] { "WeCantSpell.Hunspell" })) {
				foreach(string configuration in new[] { "Debug","Release" }) {
					string folder=SourceBuild.Combine("third-party/build/"+project+"/bin/"+configuration);
					if(!Directory.Exists(folder)) {
						continue;
					}
					countFolders++;
					foreach(string path in Directory.GetFiles(folder,"*.dll")) {
						string pathRequired=Path.Combine(folderRequired,Path.GetFileName(path));
						if(File.Exists(pathRequired) && !File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(pathRequired))) {
							listProblems.Add(SourceBuild.Relative(path)+" is not the file of Required dlls.");
						}
					}
				}
			}
			Assert.IsTrue(countFolders>=7,"third-party\\build is not built.");
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void ProjectFiles_BuildOffline_FromTheSubmodulePaths_InTheSolutionAndTheFilter() {
			string buildFolder=SourceBuild.Combine("third-party/build");
			string sln=File.ReadAllText(SourceBuild.Combine("OpenDental.sln"));
			string slnf=File.ReadAllText(SourceBuild.Combine("OpenDental.Local.slnf"));
			List<string> listProjects=Directory.GetFiles(buildFolder,"*.csproj",SearchOption.AllDirectories)
				.Where(x => !x.Contains(@"\bin\") && !x.Contains(@"\obj\")).ToList();
			CollectionAssert.AreEquivalent(_arrayProjects.Select(x => x.Project).Concat(new[] { "WeCantSpell.Hunspell" }).ToArray(),
				listProjects.Select(Path.GetFileNameWithoutExtension).ToArray());
			int countHintPaths=0;
			foreach(string path in listProjects) {
				string name=Path.GetFileNameWithoutExtension(path);
				string relative=@"third-party\build\"+name+@"\"+name+".csproj";
				string text=File.ReadAllText(path);
				//The project file evaluated with what it imports, so the checks hold for the classic project files and the SDK-style ones of the .NET 10
				//move alike: no NuGet package, GitVersion or SourceLink in either format (an SDK-style wrapper is fine, an upstream project is not).
				ProjectFile projectFile=ProjectFile.Load(path);
				foreach(string forbidden in new[] { "<PackageReference","GitVersion.MsBuild","SourceLink","packages.config" }) {
					Assert.IsFalse(text.IndexOf(forbidden,StringComparison.OrdinalIgnoreCase)>=0,name+" uses "+forbidden);
				}
				Assert.AreEqual(0,projectFile.GetItems("PackageReference").Count+projectFile.GetItems("PackageDownload").Count,name+" asks NuGet for a package.");
				Assert.IsFalse(File.Exists(Path.Combine(Path.GetDirectoryName(path),"packages.config")),name+" has packages.config");
				CollectionAssert.Contains(projectFile.FilesRead,Path.Combine(buildFolder,"SourceBuild.props"),name);
				CollectionAssert.Contains(projectFile.FilesRead,Path.Combine(buildFolder,"SourceBuild.targets"),name);
				Assert.AreNotEqual("",projectFile.GetProperty("SourceBuildUpstreamProject"),name);
				StringAssert.Contains(sln,"\""+name+"\", \""+relative+"\"",name+" is in OpenDental.sln.");
				StringAssert.Contains(slnf,"\""+relative.Replace(@"\",@"\\")+"\"",name+" is in OpenDental.Local.slnf.");
				//The Required dlls they use are support assemblies, never a PDFsharp, MigraDoc or WeCantSpell file.
				List<string> listHintPaths=projectFile.GetItems("Reference").Select(x => x.GetMetadata("HintPath")).Where(x => x!="").ToList();
				countHintPaths+=listHintPaths.Count;
				foreach(string hintPath in listHintPaths) {
					StringAssert.StartsWith(hintPath,@"..\..\..\Required dlls\",name);
					Assert.IsFalse(Regex.IsMatch(Path.GetFileName(hintPath),"^(PdfSharp|MigraDoc|WeCantSpell)"),name+": "+hintPath);
				}
				//.NET Framework 4.8, x86 like the program (SourceBuild.props for the classic projects, the SDK's net48 and Directory.Build.props for SDK-style).
				Assert.IsTrue(projectFile.GetProperty("TargetFrameworkVersion")=="v4.8" || projectFile.GetProperty("TargetFramework")=="net48",name);
				Assert.AreEqual("x86",projectFile.GetProperty("PlatformTarget"),name);
				if(name!="WeCantSpell.Hunspell") {
					CollectionAssert.Contains(projectFile.FilesRead,Path.Combine(buildFolder,"PDFsharp.props"),name);
					Assert.AreEqual("true",projectFile.GetProperty("SignAssembly"),name);
					string pathKey=Path.Combine(SubmoduleFolder(SourceBuild.PDFSHARP_PATH),"src","StrongnameKey.snk");
					Assert.AreEqual(pathKey,Path.GetFullPath(projectFile.GetProperty("AssemblyOriginatorKeyFile")),true,name+": signed with upstream's key from the submodule.");
					Assert.IsTrue(File.Exists(pathKey),pathKey);
					Assert.AreEqual(Path.Combine(SubmoduleFolder(SourceBuild.PDFSHARP_PATH),"src","foundation","src")+@"\",
						Path.GetFullPath(projectFile.GetProperty("PdfSharpSourceRoot")),true,name);
				}
			}
			Assert.IsTrue(countHintPaths>0,"No source-build project references a support assembly of Required dlls: the HintPaths were not read.");
			//The build of the upstream projects (SDK style, NuGet, GitVersion) is never invoked: no project of the solution points into a submodule.
			foreach(Match match in Regex.Matches(sln,@"^Project\(""[^""]+""\) = ""[^""]+"", ""([^""]+)""",RegexOptions.Multiline)) {
				Assert.IsFalse(match.Groups[1].Value.StartsWith(@"third-party\PDFsharp\") || match.Groups[1].Value.StartsWith(@"third-party\WeCantSpell.Hunspell\"),
					match.Groups[1].Value);
			}
		}
	}
}
