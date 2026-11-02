// The support assemblies in "Required dlls" are the versions of the library updates (lib\net462 of the NuGet packages the owner approved), the
// .NET Framework's own System.ValueTuple is used instead of a copy, and fo-dicom 4 and the packages only fo-dicom 5 needed stay out (the owner
// dropped fo-dicom for the in-house DICOM reader on 2026-11-02). A later package that replaces one of these files changes its line here on purpose.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LibraryBindingTests {
	[TestClass]
	public class RequiredDllsTests {
		///<summary>File, assembly version, file version and NuGet package version of each support assembly. The tier 1 library updates use all but
		///System.Security.Cryptography.Pkcs, which waits in the folder and ships once a shipped library needs it (ReleaseListTests). The libraries
		///themselves are checked by tests/LibraryUpdateTests (LibraryFilesTests).</summary>
		private static readonly string[][] _arraySupportAssemblies=new[] {
			new[] { "Microsoft.Bcl.AsyncInterfaces.dll","10.0.0.14","10.0.1426.52326","10.0.14" },
			new[] { "Microsoft.Bcl.Cryptography.dll","10.0.0.10","10.0.1026.32716","10.0.10" },
			new[] { "Microsoft.Extensions.DependencyInjection.Abstractions.dll","8.0.0.2","8.0.1024.46610","8.0.2" },
			new[] { "Microsoft.Extensions.Logging.Abstractions.dll","8.0.0.3","8.0.1325.6609","8.0.3" },
			new[] { "System.Buffers.dll","4.0.5.0","4.600.125.16908","4.6.1" },
			new[] { "System.Diagnostics.DiagnosticSource.dll","8.0.0.1","8.0.424.16909","8.0.1" },
			new[] { "System.Formats.Asn1.dll","10.0.0.13","10.0.1326.50504","10.0.13" },
			new[] { "System.IO.Pipelines.dll","10.0.0.14","10.0.1426.52326","10.0.14" },
			new[] { "System.Memory.dll","4.0.5.0","4.600.325.20307","4.6.3" },
			new[] { "System.Numerics.Vectors.dll","4.1.6.0","4.600.125.16908","4.6.1" },
			new[] { "System.Runtime.CompilerServices.Unsafe.dll","6.0.3.0","6.100.225.20307","6.1.2" },
			new[] { "System.Security.Cryptography.Pkcs.dll","8.0.0.1","8.0.1024.46610","8.0.1" },
			new[] { "System.Threading.Tasks.Extensions.dll","4.2.4.0","4.600.325.20307","4.6.3" },
		};

		///<summary>fo-dicom 4 (Dicom.Core.dll and its native codecs Dicom.Native.dll), which the in-house DICOM reader replaced, and the packages
		///only fo-dicom 5 needed (and fo-dicom 5 itself). No library that stays uses them: MimeKit's net48 build does not reference
		///System.Text.Encoding.CodePages, and Microsoft.Extensions.Logging.Abstractions 8.0.3 needs only DependencyInjection.Abstractions.</summary>
		private static readonly string[] _arrayFoDicomOnly=new[] {
			"Dicom.Core.dll","Dicom.Native.dll",
			"CommunityToolkit.HighPerformance.dll","Microsoft.Bcl.HashCode.dll","Microsoft.Extensions.Configuration.Abstractions.dll",
			"Microsoft.Extensions.Configuration.Binder.dll","Microsoft.Extensions.DependencyInjection.dll","Microsoft.Extensions.Logging.dll",
			"Microsoft.Extensions.Options.dll","Microsoft.Extensions.Options.ConfigurationExtensions.dll","Microsoft.Extensions.Primitives.dll",
			"System.Text.Encoding.CodePages.dll","System.Threading.Channels.dll","fo-dicom.core.dll","fo-dicom.Imaging.Desktop.dll",
		};

		[TestMethod]
		public void SupportAssemblies_AreTheVersionsOfTheLibraryUpdates() {
			List<string> listProblems=new List<string>();
			foreach(string[] arrayRow in _arraySupportAssemblies) {
				string path=Path.Combine(RepoPaths.RequiredDlls,arrayRow[0]);
				if(!File.Exists(path)) {
					listProblems.Add(arrayRow[0]+" is missing (NuGet package "+arrayRow[3]+").");
					continue;
				}
				AssemblyFacts assemblyFacts=MetadataReader.Read(path);
				string fileVersion=FileVersionInfo.GetVersionInfo(path).FileVersion;
				if(!assemblyFacts.IsManaged || assemblyFacts.Version.ToString()!=arrayRow[1] || fileVersion!=arrayRow[2]) {
					listProblems.Add(arrayRow[0]+" is "+assemblyFacts.Version+" (file "+fileVersion+"), not "+arrayRow[1]+" (file "+arrayRow[2]+", NuGet package "
						+arrayRow[3]+").");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void SystemValueTuple_ComesFromTheFramework() {
			//.NET Framework 4.7 and later have System.ValueTuple in the global assembly cache and bind every 4.0.x reference to it, so a copy in
			//"Required dlls" (4.0.3.0 used to sit there, referenced by nothing) is never used and must not come back.
			Assert.IsFalse(File.Exists(Path.Combine(RepoPaths.RequiredDlls,"System.ValueTuple.dll")),"Required dlls\\System.ValueTuple.dll is back.");
			List<AssemblyReference> listReferences=RequiredDlls.Assemblies.Values.SelectMany(x => x.ListReferences)
				.Where(x => x.Name=="System.ValueTuple").ToList();
			foreach(Version version in listReferences.Select(x => x.Version).Distinct()) {
				AssemblyName assemblyName=new AssemblyName("System.ValueTuple, Version="+version+", Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
				Assembly assembly=Assembly.Load(assemblyName);//the framework's own assembly, not a file of this repository
				Assert.IsTrue(assembly.GlobalAssemblyCache,"System.ValueTuple "+version+" should bind to the framework's copy, not "+assembly.Location);
			}
		}

		[TestMethod]
		public void FoDicomAndItsDependencies_AreNotInRequiredDlls() {
			List<string> listFound=_arrayFoDicomOnly.Where(x => RequiredDlls.Files.ContainsKey(x)).ToList();
			Assert.AreEqual(0,listFound.Count,"fo-dicom was dropped for the in-house DICOM reader, so these are not needed: "+string.Join(", ",listFound));
			//Nothing that stays references a DICOM library.
			List<string> listReferencing=RequiredDlls.Assemblies.Values.Where(x => x.ListReferences.Any(y => y.Name.StartsWith("Dicom.",StringComparison.OrdinalIgnoreCase)
				|| y.Name.StartsWith("fo-dicom",StringComparison.OrdinalIgnoreCase))).Select(x => x.FileName).ToList();
			Assert.AreEqual(0,listReferencing.Count,"These reference fo-dicom: "+string.Join(", ",listReferencing));
		}
	}
}
