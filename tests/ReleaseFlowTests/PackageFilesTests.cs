// S2-4 of the .NET 10 move (docs/port-notes/S2-4.md): the release takes every third-party file of a NuGet package from that package, at the
// version OpenPmsDental's packages.lock.json names, and refuses a staged file that is not byte for byte the package's own (tools\Build-Release.ps1,
// Test-PackageFiles); THIRD-PARTY-NOTICES.txt names the versions the lock file names. The checks run on a scratch folder under the temp folder;
// they read the program's restore (obj\project.assets.json) and the package folders, and are inconclusive before the first build.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectFileTests;

namespace ReleaseFlowTests {
	[TestClass]
	public class PackageFilesTests {
		///<summary>Each package of OpenPmsDental's lock file whose files ship, and the component of THIRD-PARTY-NOTICES.txt that names it.</summary>
		private static readonly Dictionary<string,string> _dictComponents=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
			{ "BouncyCastle.Cryptography","Bouncy Castle Cryptography" },
			{ "Google.Apis","Google APIs Client Library for .NET" },
			{ "Google.Apis.Auth","Google APIs Client Library for .NET" },
			{ "Google.Apis.Core","Google APIs Client Library for .NET" },
			{ "Google.Apis.Gmail.v1","Google APIs Client Library for .NET" },
			{ "MailKit","MailKit" },
			{ "MimeKit","MimeKit" },
			{ "Microsoft.Identity.Client","MSAL.NET" },
			{ "Microsoft.IdentityModel.Abstractions","MSAL.NET" },
			{ "Microsoft.Web.WebView2","Microsoft Edge WebView2 SDK" },
			{ "MySqlConnector","MySqlConnector" },
			{ "Newtonsoft.Json","Newtonsoft.Json" },
			{ "SSH.NET","SSH.NET" },
			{ "Microsoft.Bcl.AsyncInterfaces",".NET libraries" },
			{ "Microsoft.Bcl.Cryptography",".NET libraries" },
			{ "Microsoft.Extensions.DependencyInjection.Abstractions",".NET libraries" },
			{ "Microsoft.Extensions.Logging.Abstractions",".NET libraries" },
			{ "System.Buffers",".NET libraries" },
			{ "System.Diagnostics.DiagnosticSource",".NET libraries" },
			{ "System.Formats.Asn1",".NET libraries" },
			{ "System.IO.Pipelines",".NET libraries" },
			{ "System.Memory",".NET libraries" },
			{ "System.Numerics.Vectors",".NET libraries" },
			{ "System.Runtime.CompilerServices.Unsafe",".NET libraries" },
			{ "System.Text.Encodings.Web",".NET libraries" },
			{ "System.Text.Json",".NET libraries" },
			{ "System.Threading.Tasks.Extensions",".NET libraries" },
		};

		///<summary>Packages of the program's lock file that ship no file, with why.</summary>
		private static readonly Dictionary<string,string> _dictNotShipped=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
			{ "System.ValueTuple","no assembly for .NET Framework 4.7.1 and later (lib\\net471\\_._): the framework's own" },
		};

		private static string FolderProgram {
			get { return Path.Combine(TestPaths.RepoRoot,"OpenPmsDental"); }
		}

		///<summary>Build-Release.ps1 with the arguments: its exit code and its output lines.</summary>
		private static KeyValuePair<int,List<string>> RunBuildRelease(string arguments) {
			ProcessStartInfo processStartInfo=new ProcessStartInfo("powershell.exe");
			processStartInfo.Arguments="-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Path.Combine(TestPaths.RepoRoot,"tools","Build-Release.ps1")+"\" "
				+arguments;
			processStartInfo.UseShellExecute=false;
			processStartInfo.RedirectStandardOutput=true;
			processStartInfo.RedirectStandardError=true;
			processStartInfo.CreateNoWindow=true;
			using(Process process=Process.Start(processStartInfo)) {
				Task<string> taskOut=process.StandardOutput.ReadToEndAsync();
				Task<string> taskErr=process.StandardError.ReadToEndAsync();
				if(!process.WaitForExit(300000)) {
					process.Kill();
					Assert.Fail("Build-Release.ps1 did not finish in 5 minutes.");
				}
				process.WaitForExit();
				List<string> listLines=(taskOut.Result+taskErr.Result).Split(new[] { "\r\n","\n" },StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
				return new KeyValuePair<int,List<string>>(process.ExitCode,listLines);
			}
		}

		///<summary>The third-party files of the allow-list (Build-Release.ps1 -ListFiles).</summary>
		private static List<string> ListThirdPartyFiles() {
			KeyValuePair<int,List<string>> pairResult=RunBuildRelease("-ListFiles");
			Assert.AreEqual(0,pairResult.Key,string.Join("\r\n",pairResult.Value));
			return pairResult.Value.Where(x => x.StartsWith("third|")).Select(x => x.Substring("third|".Length)).ToList();
		}

		///<summary>The files the program's restore copies from its packages; inconclusive (a failure with OPMS_REQUIRE_BUILD_OUTPUT=1) before the first
		///restore.</summary>
		private static Dictionary<string,PackageFile> ReadProgramPackageFiles() {
			Dictionary<string,PackageFile> dictFiles=NuGetFiles.ReadCopiedFiles(TestPaths.RepoRoot,FolderProgram);
			if(dictFiles==null) {
				BuildOutput.NotBuilt(@"OpenPmsDental is not restored yet (obj\project.assets.json). Build OpenDental.Local.slnf first.");
			}
			return dictFiles;
		}

		///<summary>A staging folder in which every third-party file of a package is the package's own, from the package folder at the version the
		///lock file names (the offline folder's, else the restore's global packages folder's), and every other allowed file is synthetic text; the
		///real THIRD-PARTY-NOTICES.txt. Returns the names of the package files.</summary>
		private static List<string> FillStaging(string folder,Dictionary<string,PackageFile> dictPackageFiles) {
			List<string> listPackageNames=new List<string>();
			foreach(string line in RunBuildRelease("-ListFiles").Value) {
				string path=line.Substring(line.IndexOf('|')+1);
				string pathStaged=Path.Combine(folder,path);
				Directory.CreateDirectory(Path.GetDirectoryName(pathStaged));
				PackageFile packageFile;
				if(line.StartsWith("third|") && dictPackageFiles.TryGetValue(path,out packageFile)) {
					Assert.IsNotNull(packageFile.Path,packageFile+" is neither in the offline folder nor in the restore's global packages folder.");
					File.Copy(packageFile.Path,pathStaged);
					listPackageNames.Add(path);
				}
				else if(path=="THIRD-PARTY-NOTICES.txt") {
					File.Copy(Path.Combine(TestPaths.RepoRoot,path),pathStaged);
				}
				else {
					File.WriteAllText(pathStaged,"synthetic "+path);
				}
			}
			return listPackageNames;
		}

		private static bool IsPackageProblem(string line) {
			return line.StartsWith("Not the package's file:") || line.StartsWith("No package for:") || line.StartsWith("Another package:")
				|| line.StartsWith("Package not found:") || line.StartsWith("Not the locked package:") || line.StartsWith("Not in the package:")
				|| line.StartsWith("No restore of the program:") || line.StartsWith("No lock file:");
		}

		///<summary>The release's staging check (-CheckFolder with -Full, the checks of step 3) passes every third-party file that is byte for byte
		///its package's own file at the version OpenPmsDental's lock file names, and refuses, with the package, its version and the file versions
		///in the message, a file of another version of the same package (Microsoft.Extensions.Logging.Abstractions 8.0.3, which the program used
		///before S2-2, when the offline folder has it; else a file of other bytes) and a file that is no assembly at all. The other checks of a
		///staging folder with synthetic files fail as they must; only the lines about package files are looked at.</summary>
		[TestMethod]
		public void CheckFolderFull_RefusesAPackageFileOfAnotherVersion_AndPassesThePackagesOwn() {
			Dictionary<string,PackageFile> dictPackageFiles=ReadProgramPackageFiles();
			string folder=TestPaths.NewTempFolder("packages");
			try {
				List<string> listPackageNames=FillStaging(folder,dictPackageFiles);
				Assert.AreEqual(30,listPackageNames.Count,"The third-party files of packages: "+string.Join(", ",listPackageNames));
				KeyValuePair<int,List<string>> pairResult=RunBuildRelease("-CheckFolder \""+folder+"\" -Full");
				Assert.AreEqual(1,pairResult.Key,"Synthetic files fail the other checks.");
				List<string> listPackageLines=pairResult.Value.Where(x => IsPackageProblem(x)).ToList();
				Assert.AreEqual(0,listPackageLines.Count,"The packages' own files pass:\r\n"+string.Join("\r\n",listPackageLines));
				//Another version of a package's file, and a file that is no assembly.
				const string NAME_OLDER="Microsoft.Extensions.Logging.Abstractions.dll";
				string folderOffline=NuGetFiles.GetOfflineFolder(TestPaths.RepoRoot);
				string pathOlder=folderOffline==null ? null
					: Path.Combine(NuGetFiles.GetPackageFolder(folderOffline,"Microsoft.Extensions.Logging.Abstractions","8.0.3"),"lib","net462",NAME_OLDER);
				if(pathOlder!=null && File.Exists(pathOlder)) {
					File.Copy(pathOlder,Path.Combine(folder,NAME_OLDER),true);
				}
				else {
					File.AppendAllText(Path.Combine(folder,NAME_OLDER),"another build");
				}
				File.WriteAllText(Path.Combine(folder,"Newtonsoft.Json.dll"),"synthetic Newtonsoft.Json.dll");
				pairResult=RunBuildRelease("-CheckFolder \""+folder+"\" -Full");
				Assert.AreEqual(1,pairResult.Key);
				listPackageLines=pairResult.Value.Where(x => IsPackageProblem(x)).ToList();
				Assert.AreEqual(2,listPackageLines.Count,string.Join("\r\n",pairResult.Value));
				PackageFile packageFile=dictPackageFiles[NAME_OLDER];
				string lineOlder=listPackageLines.Single(x => x.StartsWith("Not the package's file: "+NAME_OLDER+" "));
				StringAssert.Contains(lineOlder,"differs from "+packageFile.Asset+" of the NuGet package "+packageFile.Id+" "+packageFile.Version+" ");
				StringAssert.Contains(lineOlder,@"the version OpenPmsDental\packages.lock.json names");
				if(pathOlder!=null && File.Exists(pathOlder)) {
					StringAssert.Contains(lineOlder,"(file version "+FileVersionInfo.GetVersionInfo(pathOlder).FileVersion+")","The staged file's version.");
				}
				StringAssert.Contains(lineOlder,"(file version "+FileVersionInfo.GetVersionInfo(packageFile.Path).FileVersion+")","The package file's version.");
				Assert.IsTrue(listPackageLines.Any(x => x.StartsWith("Not the package's file: Newtonsoft.Json.dll ")),string.Join("\r\n",listPackageLines));
			}
			finally {
				try {
					Directory.Delete(folder,true);
				}
				catch(IOException) { }
				catch(UnauthorizedAccessException) { }
			}
		}

		///<summary>The allow-list's third-party files of packages are exactly the files the program's restore copies from the packages that ship
		///(every asset of them, none of another package), and Build-Release.ps1 names each with the package it comes from.</summary>
		[TestMethod]
		public void AllowList_ShipsEveryFileOfTheShippedPackages_FromItsPackage() {
			string script=TestPaths.ReadSource("tools","Build-Release.ps1");
			List<string> listThird=ListThirdPartyFiles();
			Dictionary<string,PackageFile> dictPackageFiles=ReadProgramPackageFiles();
			List<string> listProblems=new List<string>();
			foreach(PackageFile packageFile in dictPackageFiles.Values.OrderBy(x => x.FileName,StringComparer.OrdinalIgnoreCase)) {
				if(_dictNotShipped.ContainsKey(packageFile.Id)) {
					listProblems.Add(packageFile+" is copied, but the package is listed as shipping no file.");
					continue;
				}
				if(!_dictComponents.ContainsKey(packageFile.Id)) {
					//Mono.Cecil is the tests' package, which the program does not get.
					listProblems.Add(packageFile+": a package of the program that is not listed here (add its THIRD-PARTY-NOTICES.txt component).");
					continue;
				}
				if(!listThird.Contains(packageFile.FileName,StringComparer.OrdinalIgnoreCase)) {
					listProblems.Add(packageFile+" is copied to the program's folder but not on the allow-list.");
				}
				if(!script.Contains("'"+packageFile.FileName+"'='package:"+packageFile.Id+"'")) {
					listProblems.Add("Build-Release.ps1 does not name "+packageFile.FileName+" as a file of the package "+packageFile.Id+".");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			Assert.AreEqual(30,dictPackageFiles.Count,string.Join(", ",dictPackageFiles.Keys));
		}

		///<summary>THIRD-PARTY-NOTICES.txt names, for each package of OpenPmsDental's lock file whose files ship, the version the lock file names: in
		///the Version field of its component (with the package's id where one component lists several packages, the .NET libraries). A package the
		///lock file adds fails here until its component is listed.</summary>
		[TestMethod]
		public void Notices_NameTheVersionsOfTheProgramsLockFile() {
			string notices=TestPaths.ReadSource("THIRD-PARTY-NOTICES.txt").Replace("\r\n","\n");
			List<LockedPackage> listLocked=NuGetFiles.ReadLockFile(Path.Combine(FolderProgram,"packages.lock.json"));
			Assert.IsTrue(listLocked.Count>=25,"The program's lock file names its packages.");
			List<string> listProblems=new List<string>();
			foreach(LockedPackage lockedPackage in listLocked.OrderBy(x => x.Id,StringComparer.OrdinalIgnoreCase)) {
				if(_dictNotShipped.ContainsKey(lockedPackage.Id)) {
					continue;
				}
				string component;
				if(!_dictComponents.TryGetValue(lockedPackage.Id,out component)) {
					listProblems.Add(lockedPackage.Id+" "+lockedPackage.Version+": no component of THIRD-PARTY-NOTICES.txt is listed for it here.");
					continue;
				}
				Match match=Regex.Match(notices,@"(?m)^--- Component: "+Regex.Escape(component)+@"\n(?:(?!^--- ).*\n)*?Version:(.*\n(?:[ \t]+\S.*\n)*)");
				if(!match.Success) {
					listProblems.Add(component+": no Version field.");
					continue;
				}
				string version=string.Join(" ",match.Groups[1].Value.Split('\n').Select(x => x.Trim()).Where(x => x!=""));
				string expected=(component==".NET libraries" ? lockedPackage.Id+" " : "")+lockedPackage.Version;
				if(!Regex.IsMatch(version,@"(?<![\w.])"+Regex.Escape(expected)+@"(?![\w.]*\d)")) {
					listProblems.Add(component+" names \""+version+"\", not "+expected+" ("+lockedPackage.Id+" of OpenPmsDental's lock file).");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			//The sentence about System.Security.Cryptography.Pkcs went with the file (S2-4).
			Assert.IsFalse(notices.Contains("System.Security.Cryptography.Pkcs"),"No package of that name is shipped or kept.");
		}
	}
}
