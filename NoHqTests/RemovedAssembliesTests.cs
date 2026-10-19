using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NoHqTests {
	///<summary>Checks that the proprietary Open Dental assemblies that were removed (the local REST API and the help system) stay out of the build.
	///Reads the metadata of the built assemblies, so it needs the solution to be built first, but no database or network connection.</summary>
	[TestClass]
	public class RemovedAssembliesTests {
		///<summary>ODApi.dll was Open Dental's local REST API. OpenDentalHelp.dll was the online manual, help browser and support-plan key check.</summary>
		private static readonly string[] _arrayRemovedAssemblies=new string[] { "ODApi","OpenDentalHelp" };

		[TestMethod]
		public void RemovedAssemblies_AreNotInRequiredDlls() {
			string pathRoot=GetRepositoryRoot();
			for(int i=0;i<_arrayRemovedAssemblies.Length;i++) {
				string path=Path.Combine(pathRoot,"Required dlls",_arrayRemovedAssemblies[i]+".dll");
				Assert.IsFalse(File.Exists(path),path+" should have been deleted.");
			}
		}

		[TestMethod]
		public void RemovedAssemblies_AreNotReferenced() {
			string pathRoot=GetRepositoryRoot();
			List<string> listPaths=new List<string> {
				Path.Combine(pathRoot,"OpenDental","bin","Debug","OpenDental.exe"),
				Path.Combine(pathRoot,"WpfControlsOD","bin","Debug","WpfControlsOD.dll"),
			};
			for(int i=0;i<listPaths.Count;i++) {
				if(!File.Exists(listPaths[i])) {
					Assert.Inconclusive(listPaths[i]+" is not built. Build OpenDental.Local.slnf (Debug|x86) first.");
				}
				List<string> listReferences=GetReferencedAssemblyNames(listPaths[i]);
				for(int j=0;j<_arrayRemovedAssemblies.Length;j++) {
					Assert.IsFalse(listReferences.Contains(_arrayRemovedAssemblies[j],StringComparer.OrdinalIgnoreCase),
						Path.GetFileName(listPaths[i])+" still references "+_arrayRemovedAssemblies[j]+".");
				}
			}
		}

		///<summary>Reads the assembly references of the file in a separate app domain, so the file is not locked and nothing is loaded here.</summary>
		internal static List<string> GetReferencedAssemblyNames(string path) {
			AppDomainSetup appDomainSetup=new AppDomainSetup();
			appDomainSetup.ApplicationBase=Path.GetDirectoryName(typeof(ReferenceReader).Assembly.Location);//so the new domain can load this test assembly
			AppDomain appDomain=AppDomain.CreateDomain("ReferenceReader",null,appDomainSetup);
			try {
				ReferenceReader referenceReader=(ReferenceReader)appDomain.CreateInstanceAndUnwrap(typeof(ReferenceReader).Assembly.FullName,typeof(ReferenceReader).FullName);
				return referenceReader.GetReferencedAssemblyNames(path).ToList();
			}
			finally {
				AppDomain.Unload(appDomain);
			}
		}

		///<summary>The folder that holds OpenDental.sln, found by walking up from the test assembly (MSTest may run it from a deployment folder below the repository).</summary>
		internal static string GetRepositoryRoot() {
			List<string> listStarts=new List<string> { Path.GetDirectoryName(typeof(RemovedAssembliesTests).Assembly.Location),Environment.CurrentDirectory };
			for(int i=0;i<listStarts.Count;i++) {
				DirectoryInfo directoryInfo=new DirectoryInfo(listStarts[i]);
				while(directoryInfo!=null) {
					if(File.Exists(Path.Combine(directoryInfo.FullName,"OpenDental.sln"))) {
						return directoryInfo.FullName;
					}
					directoryInfo=directoryInfo.Parent;
				}
			}
			Assert.Inconclusive("Could not find the repository root (the folder with OpenDental.sln).");
			return null;
		}
	}

	///<summary>Created in a separate app domain by RemovedAssembliesTests.</summary>
	public class ReferenceReader:MarshalByRefObject {
		public string[] GetReferencedAssemblyNames(string path) {
			Assembly assembly=Assembly.ReflectionOnlyLoadFrom(path);
			return assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
		}
	}
}
