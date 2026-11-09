// The tier 1 library files: "Required dlls" holds the versions the owner approved (NuGet packages restored for net48, lib-manifest), and the
// libraries they replace stay out of the repository, the projects, the source and the build output: OpenPop.NET (MailKit's POP3 client),
// SharpSSH with its Mentalis.org and DiffieHellman helpers (SSH.NET), the Google APIs 1.44 platform shims, and MimeKit 2's BouncyCastle.Crypto.
// Reads files and assembly metadata only. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibraryUpdateTests {
	[TestClass]
	public class LibraryFilesTests {
		///<summary>File, assembly version, file version and NuGet package of each library this update brings.</summary>
		private static readonly string[][] _arrayLibraries=new[] {
			new[] { "Newtonsoft.Json.dll","13.0.0.0","13.0.4.30916","Newtonsoft.Json 13.0.4" },
			new[] { "MailKit.dll","4.18.0.0","4.18.2.0","MailKit 4.18.2" },
			new[] { "MimeKit.dll","4.18.0.0","4.18.1.0","MimeKit 4.18.2" },
			new[] { "BouncyCastle.Cryptography.dll","2.0.0.0","2.7.0.16391","BouncyCastle.Cryptography 2.7.0" },
			new[] { "Google.Apis.dll","1.78.0.0","1.78.0.0","Google.Apis 1.78.0" },
			new[] { "Google.Apis.Core.dll","1.78.0.0","1.78.0.0","Google.Apis.Core 1.78.0" },
			new[] { "Google.Apis.Auth.dll","1.78.0.0","1.78.0.0","Google.Apis.Auth 1.78.0" },
			new[] { "Google.Apis.Gmail.v1.dll","1.77.0.4295","1.77.0.4295","Google.Apis.Gmail.v1 1.77.0.4295" },
			new[] { "Microsoft.Identity.Client.dll","4.92.0.0","4.92.0.0","Microsoft.Identity.Client 4.92.0" },
			new[] { "Microsoft.IdentityModel.Abstractions.dll","8.14.0.0","8.14.0.60815","Microsoft.IdentityModel.Abstractions 8.14.0" },
			new[] { "Renci.SshNet.dll","2026.0.0.1","2026.0.0.1","SSH.NET 2026.0.0" },
			new[] { "System.Text.Json.dll","10.0.0.14","10.0.1426.52326","System.Text.Json 10.0.14" },
			new[] { "System.Text.Encodings.Web.dll","10.0.0.14","10.0.1426.52326","System.Text.Encodings.Web 10.0.14" },
		};

		///<summary>The assemblies that went, by assembly name (the file is the name plus .dll).</summary>
		private static readonly string[] _arrayRemoved={ "OpenPop","Tamir.SharpSSH","Org.Mentalis.Security","DiffieHellman","Google.Apis.PlatformServices",
			"Google.Apis.Auth.PlatformServices","BouncyCastle.Crypto" };

		private static string RepoRoot {
			get {
				DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
				while(directoryInfo!=null && !File.Exists(Path.Combine(directoryInfo.FullName,"OpenDental.sln"))) {
					directoryInfo=directoryInfo.Parent;
				}
				Assert.IsNotNull(directoryInfo,"The repository root is not above "+AppDomain.CurrentDomain.BaseDirectory);
				return directoryInfo.FullName;
			}
		}

		///<summary>Files of the repository with one of the extensions, outside bin, obj, packages and .git.</summary>
		private static List<string> GetFiles(params string[] arrayExtensions) {
			List<string> listFiles=new List<string>();
			Stack<string> stackFolders=new Stack<string>();
			stackFolders.Push(RepoRoot);
			while(stackFolders.Count>0) {
				string folder=stackFolders.Pop();
				foreach(string sub in Directory.GetDirectories(folder)) {
					string name=Path.GetFileName(sub).ToLowerInvariant();
					if(name=="bin" || name=="obj" || name=="packages" || name==".git" || name==".vs" || name==".claude") {
						continue;
					}
					stackFolders.Push(sub);
				}
				listFiles.AddRange(Directory.GetFiles(folder).Where(x => arrayExtensions.Contains(Path.GetExtension(x),StringComparer.OrdinalIgnoreCase)));
			}
			return listFiles;
		}

		[TestMethod]
		public void RequiredDlls_HoldTheApprovedVersions() {
			List<string> listProblems=new List<string>();
			foreach(string[] arrayRow in _arrayLibraries) {
				string path=Path.Combine(RepoRoot,"Required dlls",arrayRow[0]);
				if(!File.Exists(path)) {
					listProblems.Add(arrayRow[0]+" is missing ("+arrayRow[3]+").");
					continue;
				}
				string version=AssemblyName.GetAssemblyName(path).Version.ToString();
				string fileVersion=FileVersionInfo.GetVersionInfo(path).FileVersion;
				if(version!=arrayRow[1] || fileVersion!=arrayRow[2]) {
					listProblems.Add(arrayRow[0]+" is "+version+" (file "+fileVersion+"), not "+arrayRow[1]+" (file "+arrayRow[2]+", "+arrayRow[3]+").");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void RemovedLibraries_AreNotInTheRepository() {
			List<string> listFound=GetFiles(".dll").Where(x => _arrayRemoved.Contains(Path.GetFileNameWithoutExtension(x),StringComparer.OrdinalIgnoreCase))
				.ToList();
			Assert.AreEqual(0,listFound.Count,"Back in the repository: "+string.Join(", ",listFound));
			foreach(string name in new[] { "Tamir.txt","Mentalis.txt" }) {
				Assert.IsFalse(File.Exists(Path.Combine(RepoRoot,"OpenPmsDental","Resources",name)),"The license text of a removed library: "+name);
			}
		}

		[TestMethod]
		public void RemovedLibraries_AreNotNamedByAProject() {
			List<string> listViolations=new List<string>();
			foreach(string path in GetFiles(".csproj",".vbproj",".vcxproj",".targets",".props")) {
				XDocument xDocument=XDocument.Load(path);
				foreach(XElement xElement in xDocument.Descendants().Where(x => x.Name.LocalName=="Reference" || x.Name.LocalName=="HintPath"
					|| x.Name.LocalName=="None" || x.Name.LocalName=="Content"))
				{
					string value=(xElement.Name.LocalName=="HintPath") ? xElement.Value : ((string)xElement.Attribute("Include")??"");
					//And the attribute form of the metadata, which an SDK-style project file may use (HintPath="...").
					foreach(string valueCur in new[] { value,(string)xElement.Attribute("HintPath")??"" }) {
						string name=Path.GetFileName(valueCur.Split(',')[0].Trim().Replace('/','\\'));
						if(name.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)) {
							name=name.Substring(0,name.Length-4);
						}
						if(_arrayRemoved.Contains(name,StringComparer.OrdinalIgnoreCase)) {
							listViolations.Add(path.Substring(RepoRoot.Length)+": <"+xElement.Name.LocalName+"> "+valueCur);
						}
					}
				}
			}
			Assert.AreEqual(0,listViolations.Count,string.Join("\r\n",listViolations));
		}

		[TestMethod]
		public void RemovedLibraries_AreNotUsedBySource() {
			Regex regex=new Regex(@"^\s*using\s+(OpenPop|Tamir|Org\.Mentalis|DiffieHellman)(\.\w+)*\s*;|\b(OpenPop\.(Pop3|Mime)|Tamir\.SharpSsh|Org\.Mentalis\.Security)\.[A-Za-z]+\b(?<!\.dll)",
				RegexOptions.Multiline);
			List<string> listSources=GetFiles(".cs");
			Assert.IsTrue(listSources.Count>1000,"The source files were not found under "+RepoRoot);
			List<string> listViolations=new List<string>();
			string self=Path.Combine("tests","LibraryUpdateTests","LibraryFilesTests.cs");
			foreach(string path in listSources.Where(x => !x.EndsWith(self,StringComparison.OrdinalIgnoreCase))) {
				foreach(Match match in regex.Matches(File.ReadAllText(path))) {
					listViolations.Add(path.Substring(RepoRoot.Length)+": "+match.Value.Trim());
				}
			}
			Assert.AreEqual(0,listViolations.Count,string.Join("\r\n",listViolations));
		}

		[TestMethod]
		public void RemovedLibraries_AreNotInTheBuildOutput_AndNotReferenced() {
			List<string> listFolders=new List<string> { AppDomain.CurrentDomain.BaseDirectory };
			foreach(string relative in new[] { @"OpenPmsDental\bin\Debug",@"OpenPmsDentalHL7\bin\Debug",@"ServiceManager\bin\Debug" }) {
				string folder=Path.Combine(RepoRoot,relative);
				if(Directory.Exists(folder)) {
					listFolders.Add(folder);
				}
			}
			List<string> listProblems=new List<string>();
			foreach(string folder in listFolders) {
				listProblems.AddRange(_arrayRemoved.Select(x => Path.Combine(folder,x+".dll")).Where(File.Exists));
				foreach(string path in Directory.GetFiles(folder,"*.dll").Concat(Directory.GetFiles(folder,"*.exe"))) {
					string name=Path.GetFileNameWithoutExtension(path);
					if(!name.StartsWith("OpenPmsDental",StringComparison.OrdinalIgnoreCase)) {
						continue;//The program's own assemblies.
					}
					AssemblyName[] arrayReferences;
					try {
						arrayReferences=Assembly.ReflectionOnlyLoadFrom(path).GetReferencedAssemblies();
					}
					catch(Exception) {
						continue;//Loaded already from another folder; the copy in this test's own folder is checked.
					}
					listProblems.AddRange(arrayReferences.Where(x => _arrayRemoved.Contains(x.Name,StringComparer.OrdinalIgnoreCase))
						.Select(x => path+" references "+x.Name));
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}
	}
}
