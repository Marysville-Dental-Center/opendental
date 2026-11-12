// The layouts of the crypt32 structs that OfficeCaServerVerifier hands to Windows (its private NativeChain class), checked against the sizes
// and offsets Windows' own headers give, for the bitness of the test process. The lane builds and tests x86 today; net10-plan.md decision 19
// sends stage 3b to x64 and makes x64 test runs a stage 3a entry condition, so these tests are written for both: a struct whose layout is
// right only on x86 (as CERT_CHAIN_PARA's flat declaration was) fails here in a 64-bit run instead of crashing the process inside
// CertGetCertificateChain. The structs are private, so they are found by reflection.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class OfficeCaServerVerifierNativeLayoutTests {
		private static bool Is64Bit {
			get { return IntPtr.Size==8; }
		}

		private static Type NativeType(string name) {
			Type typeNativeChain=typeof(OfficeCaServerVerifier).GetNestedType("NativeChain",BindingFlags.NonPublic);
			Assert.IsNotNull(typeNativeChain,"OfficeCaServerVerifier.NativeChain was not found.");
			Type type=typeNativeChain.GetNestedType(name,BindingFlags.NonPublic);
			Assert.IsNotNull(type,"OfficeCaServerVerifier.NativeChain."+name+" was not found.");
			return type;
		}

		private static void AssertOffset(Type type,string field,int offsetX86,int offsetX64) {
			int offsetExpected=Is64Bit ? offsetX64 : offsetX86;
			Assert.AreEqual(offsetExpected,Marshal.OffsetOf(type,field).ToInt32(),type.Name+"."+field+" (process is "+(Is64Bit ? "64" : "32")+"-bit)");
		}

		private static void AssertSize(Type type,int sizeX86,int sizeX64) {
			int sizeExpected=Is64Bit ? sizeX64 : sizeX86;
			Assert.AreEqual(sizeExpected,Marshal.SizeOf(type),"Size of "+type.Name+" (process is "+(Is64Bit ? "64" : "32")+"-bit)");
		}

		[TestMethod]
		public void CertChainPara_HasWindowsNestedLayout() {
			//Native: {DWORD cbSize; CERT_USAGE_MATCH RequestedUsage{DWORD dwType; CERT_ENHKEY_USAGE Usage{DWORD cUsageIdentifier; LPSTR* rgpszUsageIdentifier}}}.
			//x86: 0/4/8/12, size 16. x64: the nested structs are aligned to 8, so 0/8/16/24, size 32.
			Type typeChainPara=NativeType("CERT_CHAIN_PARA");
			Type typeUsageMatch=NativeType("CERT_USAGE_MATCH");
			Type typeEnhkeyUsage=NativeType("CERT_ENHKEY_USAGE");
			AssertSize(typeChainPara,16,32);
			AssertOffset(typeChainPara,"cbSize",0,0);
			AssertOffset(typeChainPara,"RequestedUsage",4,8);
			AssertSize(typeUsageMatch,12,24);
			AssertOffset(typeUsageMatch,"dwType",0,0);
			AssertOffset(typeUsageMatch,"Usage",4,8);
			AssertSize(typeEnhkeyUsage,8,16);
			AssertOffset(typeEnhkeyUsage,"cUsageIdentifier",0,0);
			AssertOffset(typeEnhkeyUsage,"rgpszUsageIdentifier",4,8);
			//The absolute offsets Windows reads dwType, cUsageIdentifier and rgpszUsageIdentifier from.
			int offsetRequestedUsage=Marshal.OffsetOf(typeChainPara,"RequestedUsage").ToInt32();
			int offsetUsage=offsetRequestedUsage+Marshal.OffsetOf(typeUsageMatch,"Usage").ToInt32();
			Assert.AreEqual(Is64Bit ? 8 : 4,offsetRequestedUsage+Marshal.OffsetOf(typeUsageMatch,"dwType").ToInt32(),"dwType");
			Assert.AreEqual(Is64Bit ? 16 : 8,offsetUsage+Marshal.OffsetOf(typeEnhkeyUsage,"cUsageIdentifier").ToInt32(),"cUsageIdentifier");
			Assert.AreEqual(Is64Bit ? 24 : 12,offsetUsage+Marshal.OffsetOf(typeEnhkeyUsage,"rgpszUsageIdentifier").ToInt32(),"rgpszUsageIdentifier");
		}

		[TestMethod]
		public void CertChainEngineConfig_HasWindowsLayout() {
			//Native (Windows 8 and later members): DWORD cbSize; HCERTSTORE hRestrictedRoot, hRestrictedTrust, hRestrictedOther; DWORD cAdditionalStore;
			//HCERTSTORE* rghAdditionalStore; DWORD dwFlags, dwUrlRetrievalTimeout, MaximumCachedCertificates, CycleDetectionModulus;
			//HCERTSTORE hExclusiveRoot, hExclusiveTrustedPeople; DWORD dwExclusiveFlags. x86 size 52, x64 size 88.
			Type type=NativeType("CERT_CHAIN_ENGINE_CONFIG");
			AssertSize(type,52,88);
			AssertOffset(type,"cbSize",0,0);
			AssertOffset(type,"hRestrictedRoot",4,8);
			AssertOffset(type,"hRestrictedTrust",8,16);
			AssertOffset(type,"hRestrictedOther",12,24);
			AssertOffset(type,"cAdditionalStore",16,32);
			AssertOffset(type,"rghAdditionalStore",20,40);
			AssertOffset(type,"dwFlags",24,48);
			AssertOffset(type,"dwUrlRetrievalTimeout",28,52);
			AssertOffset(type,"MaximumCachedCertificates",32,56);
			AssertOffset(type,"CycleDetectionModulus",36,60);
			AssertOffset(type,"hExclusiveRoot",40,64);
			AssertOffset(type,"hExclusiveTrustedPeople",44,72);
			AssertOffset(type,"dwExclusiveFlags",48,80);
		}

		[TestMethod]
		public void SslExtraCertChainPolicyPara_HasWindowsLayout() {
			//Native: union{DWORD cbStruct; DWORD cbSize}; DWORD dwAuthType; DWORD fdwChecks; WCHAR* pwszServerName. x86 size 16, x64 size 24.
			Type type=NativeType("SSL_EXTRA_CERT_CHAIN_POLICY_PARA");
			AssertSize(type,16,24);
			AssertOffset(type,"cbSize",0,0);
			AssertOffset(type,"dwAuthType",4,4);
			AssertOffset(type,"fdwChecks",8,8);
			AssertOffset(type,"pwszServerName",12,16);
		}

		[TestMethod]
		public void CertChainPolicyParaAndStatus_HaveWindowsLayout() {
			//CERT_CHAIN_POLICY_PARA: DWORD cbSize; DWORD dwFlags; void* pvExtraPolicyPara. x86 size 12, x64 size 16.
			Type typePara=NativeType("CERT_CHAIN_POLICY_PARA");
			AssertSize(typePara,12,16);
			AssertOffset(typePara,"cbSize",0,0);
			AssertOffset(typePara,"dwFlags",4,4);
			AssertOffset(typePara,"pvExtraPolicyPara",8,8);
			//CERT_CHAIN_POLICY_STATUS: DWORD cbSize; DWORD dwError; LONG lChainIndex; LONG lElementIndex; void* pvExtraPolicyStatus. x86 20, x64 24.
			Type typeStatus=NativeType("CERT_CHAIN_POLICY_STATUS");
			AssertSize(typeStatus,20,24);
			AssertOffset(typeStatus,"cbSize",0,0);
			AssertOffset(typeStatus,"dwError",4,4);
			AssertOffset(typeStatus,"lChainIndex",8,8);
			AssertOffset(typeStatus,"lElementIndex",12,12);
			AssertOffset(typeStatus,"pvExtraPolicyStatus",16,16);
		}
	}
}
