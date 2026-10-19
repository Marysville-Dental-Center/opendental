using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace NoHqTests {
	///<summary>Checks that Open Dental's cloud edition (Thinfinity "ODCloud" and AWS AppStream) stays removed: the VirtualWeb shim and the AWS SDK are gone,
	///and the cloud switches (ODBuild.IsThinfinity, ODEnvironment.IsCloudServer, PrefC.IsAppStream, the ODCloudClient) no longer exist.
	///The OpenDentalCloud project (Dropbox and Google Drive image storage) is not part of the cloud edition and is not checked here.</summary>
	[TestClass]
	public class CloudEditionTests {
		///<summary>VirtualWeb.dll was the Thinfinity browser-hosting shim. The AWS SDK was only used for the AppStream user administration window.</summary>
		private static readonly string[] _arrayRemovedAssemblies=new string[] { "VirtualWeb","AWSSDK.Core","AWSSDK.IdentityStore" };

		[TestMethod]
		public void CloudEditionAssemblies_AreNotInRequiredDlls() {
			string pathRoot=RemovedAssembliesTests.GetRepositoryRoot();
			for(int i=0;i<_arrayRemovedAssemblies.Length;i++) {
				string path=Path.Combine(pathRoot,"Required dlls",_arrayRemovedAssemblies[i]+".dll");
				Assert.IsFalse(File.Exists(path),path+" should have been deleted.");
			}
		}

		[TestMethod]
		public void CloudEditionAssemblies_AreNotReferenced() {
			string pathRoot=RemovedAssembliesTests.GetRepositoryRoot();
			List<string> listPaths=new List<string> {
				Path.Combine(pathRoot,"OpenDental","bin","Debug","OpenDental.exe"),
				Path.Combine(pathRoot,"WpfControlsOD","bin","Debug","WpfControlsOD.dll"),
				Path.Combine(pathRoot,"OpenDentBusiness","bin","Debug","OpenDentBusiness.dll"),
				Path.Combine(pathRoot,"OpenDentalGraph","bin","Debug","OpenDentalGraph.dll"),
				Path.Combine(pathRoot,"CodeBase","bin","Debug","CodeBase.dll"),
			};
			for(int i=0;i<listPaths.Count;i++) {
				if(!File.Exists(listPaths[i])) {
					Assert.Inconclusive(listPaths[i]+" is not built. Build OpenDental.Local.slnf (Debug|x86) first.");
				}
				List<string> listReferences=RemovedAssembliesTests.GetReferencedAssemblyNames(listPaths[i]);
				for(int j=0;j<_arrayRemovedAssemblies.Length;j++) {
					Assert.IsFalse(listReferences.Contains(_arrayRemovedAssemblies[j],StringComparer.OrdinalIgnoreCase),
						Path.GetFileName(listPaths[i])+" still references "+_arrayRemovedAssemblies[j]+".");
				}
			}
		}

		[TestMethod]
		public void CloudEditionSwitches_AreRemoved() {
			Assert.IsNull(typeof(ODBuild).GetMethod("IsThinfinity"),"ODBuild.IsThinfinity should have been removed.");
			Assert.IsNull(typeof(ODEnvironment).GetProperty("IsCloudServer"),"ODEnvironment.IsCloudServer should have been removed.");
			Assert.IsNull(typeof(ODEnvironment).GetProperty("IsCloudInstance"),"ODEnvironment.IsCloudInstance should have been removed.");
			Assert.IsNull(typeof(ODEnvironment).GetMethod("SetMachineName"),"ODEnvironment.SetMachineName (machine name from the cloud client) should have been removed.");
			Assert.IsNull(typeof(PrefC).GetProperty("IsAppStream"),"PrefC.IsAppStream should have been removed.");
			Assert.IsNull(typeof(ODBuild).Assembly.GetType("CodeBase.ODCloudClient"),"CodeBase.ODCloudClient should have been removed.");
			Assert.IsNull(typeof(ODBuild).Assembly.GetType("CodeBase.Utilities.ODCloudDcvExtension"),"CodeBase.Utilities.ODCloudDcvExtension should have been removed.");
		}

		[TestMethod]
		public void CloudEditionHqMembers_AreRemoved() {
			string[] arrayMethodNames=new string[] { "GetCloudMaxSessions","GetThinfinityLicenseUsers","RecordCloudSessionsInUse","RecordCloudStorageSizeUsed",
				"GetMaxAtoZSizeGB","UpsertCloudMaxSessions","GetLatestCloudClientVersion" };
			List<Type> listTypes=new List<Type> { typeof(IWebServiceMainHQ),typeof(WebServiceMainHQNoHq),typeof(WebServiceMainHQProxy) };
			for(int i=0;i<listTypes.Count;i++) {
				for(int j=0;j<arrayMethodNames.Length;j++) {
					MethodInfo methodInfo=listTypes[i].GetMethod(arrayMethodNames[j],BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly);
					Assert.IsNull(methodInfo,listTypes[i].Name+"."+arrayMethodNames[j]+" should have been removed.");
				}
			}
		}
	}
}
