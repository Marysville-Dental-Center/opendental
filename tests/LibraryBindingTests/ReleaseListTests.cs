// The release ships exactly the library files the programs need (those of "Required dlls" and, since S2-4 of the .NET 10 move, the NuGet
// packages' own files at the lock files' versions): tools\Build-Release.ps1's allow-list (which is also what installer\OpenPmsDental.iss
// installs, since it installs the staging folder as a whole) against what the built program references, all the way down, and every assembly a
// shipped file references is shipped too or is part of the .NET Framework.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProjectFileTests;

namespace LibraryBindingTests {
	[TestClass]
	public class ReleaseListTests {
		///<summary>References of shipped files that neither the release nor the .NET Framework has, with why the program still runs. Keep this list
		///short and exact. (MimeKit 2.4.1's BouncyCastle.Crypto left it with the MailKit and MimeKit 4 update, which ships BouncyCastle.Cryptography.)
		///</summary>
		private static readonly Dictionary<string,string> _dictKnownGaps=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
			{ "Health.Direct.Config.Client","Health.Direct.ResolverPlugins uses it only for the Direct configuration service; the program uses only its "
				+"LdapCertResolver" },
			{ "Health.Direct.Config.Store","Health.Direct.ResolverPlugins uses it only for the Direct configuration service; the program uses only its "
				+"LdapCertResolver" },
		};

		private static string ProgramFolder {
			get { return RepoPaths.Combine("OpenPmsDental","bin","Debug"); }
		}

		private static void SkipUnlessBuilt() {
			if(!File.Exists(Path.Combine(ProgramFolder,"OpenPmsDental.exe"))) {
				BuildOutput.NotBuilt(@"OpenPmsDental\bin\Debug is not built. Build OpenDental.Local.slnf (Debug|x86) first.");
			}
		}

		///<summary>The program's own assemblies in its output folder (and the third-party ones built from this repository): the files on the
		///allow-list that are no library files (Libraries).</summary>
		private static List<AssemblyFacts> GetBuiltAssemblies() {
			List<AssemblyFacts> listAssemblies=new List<AssemblyFacts>();
			foreach(KeyValuePair<string,string> pair in BuildRelease.ListFiles().Where(x => x.Key!="doc" && MetadataReader.IsBinary(x.Value))) {
				string path=Path.Combine(ProgramFolder,pair.Value);
				if(Libraries.Files.ContainsKey(pair.Value) || !File.Exists(path)) {
					continue;//OpenPmsDental.XmlSerializers.dll and OpenPmsDentalBusiness.XmlSerializers.dll come with Release builds only.
				}
				AssemblyFacts assemblyFacts=MetadataReader.Read(path);
				if(assemblyFacts.IsManaged) {
					listAssemblies.Add(assemblyFacts);
				}
			}
			return listAssemblies;
		}

		///<summary>The library files the program folder gets: OpenPmsDental's own references to them and the files of its packages (always copied),
		///the files its projects copy with None or Content items, and every library file that the program's assemblies reference, all the way
		///down.</summary>
		private static HashSet<string> GetLibrariesTheProgramNeeds() {
			ProjectInfo projectInfoProgram=SolutionProjects.ByName("OpenPmsDental.csproj");
			HashSet<string> hashSetNeeded=new HashSet<string>(projectInfoProgram.ListPrimaryReferencesCopied,StringComparer.OrdinalIgnoreCase);
			foreach(ProjectInfo projectInfo in SolutionProjects.Closure(projectInfoProgram)) {
				hashSetNeeded.UnionWith(projectInfo.ListCopyItems);
			}
			Queue<AssemblyFacts> queueAssemblies=new Queue<AssemblyFacts>(GetBuiltAssemblies());
			foreach(string fileName in hashSetNeeded) {
				AssemblyFacts assemblyFacts=Libraries.ByFileName(fileName);
				if(assemblyFacts!=null) {
					queueAssemblies.Enqueue(assemblyFacts);
				}
			}
			HashSet<string> hashSetSeen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			while(queueAssemblies.Count>0) {
				AssemblyFacts assemblyFacts=queueAssemblies.Dequeue();
				if(!hashSetSeen.Add(assemblyFacts.Path)) {
					continue;
				}
				foreach(AssemblyReference assemblyReference in assemblyFacts.ListReferences) {
					if(Libraries.Assemblies.TryGetValue(assemblyReference.Name,out AssemblyFacts assemblyFactsLibrary)) {
						hashSetNeeded.Add(assemblyFactsLibrary.FileName);
						queueAssemblies.Enqueue(assemblyFactsLibrary);
					}
				}
			}
			return hashSetNeeded;
		}

		[TestMethod]
		public void AllowList_NamesExactlyTheLibraryFilesTheProgramsNeed() {
			SkipUnlessBuilt();
			HashSet<string> hashSetNeeded=GetLibrariesTheProgramNeeds();
			List<string> listAllowed=BuildRelease.ListFiles().Where(x => x.Key=="third" && Libraries.Files.ContainsKey(x.Value)).Select(x => x.Value).ToList();
			List<string> listProblems=new List<string>();
			foreach(string fileName in hashSetNeeded.Where(x => !listAllowed.Contains(x,StringComparer.OrdinalIgnoreCase)).OrderBy(x => x)) {
				listProblems.Add(fileName+": the program needs it, but tools\\Build-Release.ps1 does not ship it (add it to the allow-list, with its notice).");
			}
			foreach(string fileName in listAllowed.Where(x => !hashSetNeeded.Contains(x)).OrderBy(x => x)) {
				listProblems.Add(fileName+": tools\\Build-Release.ps1 ships it, but nothing in the program uses it.");
			}
			foreach(string fileName in hashSetNeeded.Where(x => !File.Exists(Path.Combine(ProgramFolder,x))).OrderBy(x => x)) {
				listProblems.Add(fileName+": the program needs it, but the build did not copy it to OpenPmsDental\\bin\\Debug.");
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
			//The support assemblies the first library step added for the packages that follow ship once a shipped library uses them: the tier 1
			//updates (SSH.NET 2026, MailKit 4, MSAL 4.92 with System.Text.Json) use them all. System.Security.Cryptography.Pkcs, which nothing
			//used, left "Required dlls" in S2-4 of the .NET 10 move: it is no library file, in no lock file and not on the allow-list.
			foreach(string fileName in new[] { "System.Formats.Asn1.dll","System.IO.Pipelines.dll","Microsoft.Bcl.Cryptography.dll" }) {
				CollectionAssert.Contains(listAllowed,fileName);
			}
			Assert.IsFalse(Libraries.Files.ContainsKey("System.Security.Cryptography.Pkcs.dll"));
			Assert.IsFalse(Libraries.LockedVersions.ContainsKey("System.Security.Cryptography.Pkcs"),"No project gets the package.");
			CollectionAssert.DoesNotContain(listAllowed,"System.Security.Cryptography.Pkcs.dll","Nothing shipped uses it.");
			CollectionAssert.Contains(listAllowed,"Microsoft.Extensions.DependencyInjection.Abstractions.dll",
				"Microsoft.Extensions.Logging.Abstractions 10.0.15 and MySqlConnector 2.6.2 reference it, so it ships.");
		}

		[TestMethod]
		public void Installer_InstallsTheStagingFolderAsAWhole() {
			string iss=RepoPaths.ReadText("installer","OpenPmsDental.iss");
			List<string> listSources=Regex.Matches(iss,@"(?m)^\s*Source:\s*""([^""]+)""(.*)$").Cast<Match>().Select(x => x.Value.Trim()).ToList();
			List<string> listStaging=listSources.Where(x => x.StartsWith(@"Source: ""{#StagingDir}\*""")).ToList();
			Assert.AreEqual(1,listStaging.Count,"One [Files] entry installs the staging folder, which holds exactly the allow-list:\r\n"+string.Join("\r\n",listSources));
			StringAssert.Contains(listStaging[0],"recursesubdirs");
			foreach(string source in listSources.Except(listStaging)) {
				//Only the connection settings that setup writes itself.
				StringAssert.Contains(source,"external",source);
				Assert.IsFalse(Regex.IsMatch(source,@"\.(dll|exe)""",RegexOptions.IgnoreCase),"The installer names a program file of its own: "+source);
			}
			//Files an upgrade removes must never be ones the release still ships.
			Match matchDelete=Regex.Match(iss,@"(?ms)^\[InstallDelete\]\s*$(.*?)(?=^\[|\z)");
			if(matchDelete.Success) {
				List<string> listAllowed=BuildRelease.ListFiles().Select(x => x.Value).ToList();
				foreach(Match matchName in Regex.Matches(matchDelete.Groups[1].Value,@"Name:\s*""\{app\}\\([^""]+)""")) {
					Assert.IsFalse(listAllowed.Contains(matchName.Groups[1].Value,StringComparer.OrdinalIgnoreCase),
						"[InstallDelete] removes a file the release ships: "+matchName.Groups[1].Value);
				}
			}
		}

		[TestMethod]
		public void ShippedAssemblies_FindEveryAssemblyTheyReference() {
			SkipUnlessBuilt();
			List<AssemblyFacts> listShipped=GetBuiltAssemblies();
			listShipped.AddRange(GetLibrariesTheProgramNeeds().Select(x => Libraries.ByFileName(x)).Where(x => x!=null));
			HashSet<string> hashSetNames=new HashSet<string>(listShipped.Select(x => x.Name),StringComparer.OrdinalIgnoreCase);
			List<string> listProblems=new List<string>();
			HashSet<string> hashSetGapsSeen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach(AssemblyFacts assemblyFacts in listShipped.OrderBy(x => x.FileName,StringComparer.OrdinalIgnoreCase)) {
				foreach(AssemblyReference assemblyReference in assemblyFacts.ListReferences) {
					//Windows resolves Windows Runtime references (PasswordVaultWrapper's Windows.Security.Credentials) itself.
					if(assemblyReference.IsWindowsRuntime || hashSetNames.Contains(assemblyReference.Name) || Gac.Has(assemblyReference.Name)) {
						continue;
					}
					if(_dictKnownGaps.ContainsKey(assemblyReference.Name)) {
						hashSetGapsSeen.Add(assemblyReference.Name);
						continue;
					}
					listProblems.Add(assemblyFacts.FileName+" references "+assemblyReference.Name+" "+assemblyReference.Version
						+", which neither ships nor is part of the .NET Framework.");
				}
			}
			foreach(string name in _dictKnownGaps.Keys.Where(x => !hashSetGapsSeen.Contains(x))) {
				listProblems.Add("The known gap "+name+" is gone; remove it from the list in ReleaseListTests.");
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}
	}
}
