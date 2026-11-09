// A certificate store of the current user for the client certificate key tests (DIRECT-CERT-FOLLOWUP): a store with a name of its own, made
// when the first certificate is added and deleted afterwards (CertUnregisterSystemStore), and the throwaway keys of its certificates, made at
// run time in the current user's key set (a CAPI key container, a CNG RSA key, a CNG ECDSA key) and deleted afterwards. Nothing opens
// LocalMachine\My, CurrentUser\My or any other real store, and no real certificate or key is touched. No administrator rights are needed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;

namespace DbConnectionSecurityTests {
	///<summary>The kinds of private key a client certificate's .pfx can give: a CAPI key container (a .pfx that names no CNG provider, as
	///OpenSSL writes it, or one that names a CAPI provider), a CNG RSA key and a CNG ECDSA key (a .pfx that names the CNG provider).</summary>
	public enum TestKeyKind {
		CapiRsa,
		CngRsa,
		CngEcdsa,
	}

	///<summary>A store of the current user with a name of its own, used for as long as the instance lives. Dispose deletes every key of the
	///certificates it added that is still there, then the store.</summary>
	public sealed class ClientCertificateTestStore:IDisposable {
		[DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]
		private static extern bool CertUnregisterSystemStore(string systemStore,uint flags);
		private const uint CERT_SYSTEM_STORE_CURRENT_USER=0x00010000;
		private const uint CERT_STORE_DELETE_FLAG=0x00000010;
		///<summary>PROV_RSA_AES, the type of the "Microsoft Enhanced RSA and AES Cryptographic Provider".</summary>
		private const int PROV_RSA_AES=24;
		private const string PROVIDER_CAPI="Microsoft Enhanced RSA and AES Cryptographic Provider";
		public readonly string Name="OpenPmsDbSecTest_"+Guid.NewGuid().ToString("N");
		///<summary>The .pfx files' password, made at run time and kept in memory only.</summary>
		private readonly string _password=Guid.NewGuid().ToString("N");
		private readonly List<TestKey> _listKeys=new List<TestKey>();

		///<summary>Makes a self-signed client certificate with a throwaway key of this kind, writes it as a .pfx in memory and imports that as the
		///connection settings tool does (PersistKeySet, never Exportable), but into the current user's key set (UserKeySet) and this store.
		///Returns where its key is.</summary>
		public TestKey AddCertificate(TestKeyKind testKeyKind) {
			return AddPfx(MakePfx(testKeyKind));
		}

		///<summary>AddCertificate for a certificate the test made in memory with its key (TestHelpers.NewCertificate): written as a .pfx in memory
		///and imported the same way.</summary>
		public TestKey AddCertificate(X509Certificate2 x509Certificate2WithKey) {
			return AddPfx(x509Certificate2WithKey.Export(X509ContentType.Pfx,_password));
		}

		private TestKey AddPfx(byte[] arrayPfx) {
			using(X509Certificate2 x509Certificate2=new X509Certificate2(arrayPfx,_password,X509KeyStorageFlags.UserKeySet|X509KeyStorageFlags.PersistKeySet)) {
				TestKey testKey=TestKey.Of(x509Certificate2);
				_listKeys.Add(testKey);
				using(X509Store x509Store=new X509Store(Name,StoreLocation.CurrentUser)) {
					x509Store.Open(OpenFlags.ReadWrite);//Creates the store.
					x509Store.Add(x509Certificate2);
				}
				return testKey;
			}
		}

		///<summary>The store's certificate with this thumbprint, or null. The caller disposes it.</summary>
		public X509Certificate2 Find(string thumbprint) {
			using(X509Store x509Store=new X509Store(Name,StoreLocation.CurrentUser)) {
				x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
				X509Certificate2Collection x509Certificate2Collection=x509Store.Certificates.Find(X509FindType.FindByThumbprint,thumbprint,false);
				return x509Certificate2Collection.Count==0 ? null : x509Certificate2Collection[0];
			}
		}

		///<summary>The thumbprints of the certificates in the store; none when it was never made.</summary>
		public List<string> GetThumbprints() {
			List<string> listThumbprints=new List<string>();
			using(X509Store x509Store=new X509Store(Name,StoreLocation.CurrentUser)) {
				try {
					x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
				}
				catch(CryptographicException) {
					return listThumbprints;
				}
				foreach(X509Certificate2 x509Certificate2 in x509Store.Certificates) {
					listThumbprints.Add(x509Certificate2.Thumbprint);
					x509Certificate2.Reset();
				}
			}
			return listThumbprints;
		}

		///<summary>True when the store's certificate with this thumbprint signs a test value with its private key, and the signature verifies
		///with its public key.</summary>
		public bool CanSign(string thumbprint) {
			byte[] arrayData=Encoding.ASCII.GetBytes("DIRECT-CERT-FOLLOWUP");
			using(X509Certificate2 x509Certificate2=Find(thumbprint)) {
				if(x509Certificate2==null) {
					return false;
				}
				using(RSA rsa=x509Certificate2.GetRSAPrivateKey()) {
					if(rsa!=null) {
						byte[] arraySignature=rsa.SignData(arrayData,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
						using(RSA rsaPublic=x509Certificate2.GetRSAPublicKey()) {
							return rsaPublic.VerifyData(arrayData,arraySignature,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
						}
					}
				}
				using(ECDsa ecdsa=x509Certificate2.GetECDsaPrivateKey())
				using(ECDsa ecdsaPublic=x509Certificate2.GetECDsaPublicKey())
				{
					byte[] arraySignature=ecdsa.SignData(arrayData,HashAlgorithmName.SHA256);
					return ecdsaPublic.VerifyData(arrayData,arraySignature,HashAlgorithmName.SHA256);
				}
			}
		}

		///<summary>Deletes every key of the certificates this store added that is still there, then the store with its certificates.</summary>
		public void Dispose() {
			foreach(TestKey testKey in _listKeys) {
				try {
					testKey.DeleteIfExists();
				}
				catch(CryptographicException) {
					//Reported by the test that left it; the store goes anyway.
				}
			}
			CertUnregisterSystemStore(Name,CERT_SYSTEM_STORE_CURRENT_USER|CERT_STORE_DELETE_FLAG);
		}

		private byte[] MakePfx(TestKeyKind testKeyKind) {
			string subject="CN=synthetic-dbsec-key-"+Guid.NewGuid().ToString("N");
			switch(testKeyKind) {
				case TestKeyKind.CapiRsa:
					//A key in a scratch CAPI container of the current user, so the .pfx names a CAPI provider and Windows imports it into a CAPI
					//container, as it does a .pfx that names no CNG provider. The scratch container is deleted before the import, which makes its own.
					CspParameters cspParameters=new CspParameters(PROV_RSA_AES,PROVIDER_CAPI,"OpenPmsDbSecTest_"+Guid.NewGuid().ToString("N"));
					RSACryptoServiceProvider rsaCryptoServiceProviderScratch=new RSACryptoServiceProvider(2048,cspParameters);
					try {
						return ExportPfx(new CertificateRequest(subject,rsaCryptoServiceProviderScratch,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
					}
					finally {
						rsaCryptoServiceProviderScratch.PersistKeyInCsp=false;//Clear then deletes the scratch container.
						rsaCryptoServiceProviderScratch.Clear();
					}
				case TestKeyKind.CngRsa:
					using(RSACng rsaCng=new RSACng(2048)) {//An ephemeral CNG key, in memory only: the .pfx names the CNG provider.
						return ExportPfx(new CertificateRequest(subject,rsaCng,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
					}
				default:
					using(ECDsaCng ecdsaCng=new ECDsaCng(256)) {
						return ExportPfx(new CertificateRequest(subject,ecdsaCng,HashAlgorithmName.SHA256));
					}
			}
		}

		private byte[] ExportPfx(CertificateRequest certificateRequest) {
			OidCollection oidCollection=new OidCollection();
			oidCollection.Add(new Oid("1.3.6.1.5.5.7.3.2"));//TLS client authentication, as the tool's client certificate has.
			certificateRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(oidCollection,false));
			using(X509Certificate2 x509Certificate2=certificateRequest.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1),DateTimeOffset.Now.AddDays(30))) {
				return x509Certificate2.Export(X509ContentType.Pfx,_password);
			}
		}
	}

	///<summary>Where a test certificate's private key is, found by the test itself, apart from the code under test: a CAPI key container (named
	///through CAPI, X509Certificate2.PrivateKey) or a CNG key, in the key set it lives in, and its file. Exists opens it through its own key
	///store, CAPI or CNG.</summary>
	public sealed class TestKey {
		public string Thumbprint;
		public bool IsCapi;
		public bool IsMachine;
		public string Provider;
		public int ProviderType;
		///<summary>The CAPI key container's name or the CNG key's name.</summary>
		public string Name;
		///<summary>The key's file: under Microsoft\Crypto\RSA\(the user's SID) or RSA\MachineKeys for a CAPI key, Microsoft\Crypto\Keys for a CNG
		///key, of %APPDATA% (the user's key set) or %ProgramData% (the machine's).</summary>
		public string FilePath;

		public static TestKey Of(X509Certificate2 x509Certificate2) {
			TestKey testKey=new TestKey();
			testKey.Thumbprint=x509Certificate2.Thumbprint;
			RSACryptoServiceProvider rsaCryptoServiceProviderCapi=null;
			try {
				rsaCryptoServiceProviderCapi=x509Certificate2.PrivateKey as RSACryptoServiceProvider;
			}
			catch(CryptographicException) {
				//A CNG RSA key.
			}
			catch(NotSupportedException) {
				//An ECDSA key.
			}
			if(rsaCryptoServiceProviderCapi!=null) {
				CspKeyContainerInfo cspKeyContainerInfo=rsaCryptoServiceProviderCapi.CspKeyContainerInfo;
				testKey.IsCapi=true;
				testKey.IsMachine=cspKeyContainerInfo.MachineKeyStore;
				testKey.Provider=cspKeyContainerInfo.ProviderName;
				testKey.ProviderType=cspKeyContainerInfo.ProviderType;
				testKey.Name=cspKeyContainerInfo.KeyContainerName;
				string folderRsa=testKey.IsMachine ? "MachineKeys" : GetCurrentUserSid();
				testKey.FilePath=Path.Combine(GetCryptoFolder(testKey.IsMachine),"RSA",folderRsa,cspKeyContainerInfo.UniqueKeyContainerName);
				return testKey;
			}
			using(RSA rsa=x509Certificate2.GetRSAPrivateKey()) {
				if(rsa is RSACng) {
					SetCng(testKey,((RSACng)rsa).Key);
					return testKey;
				}
			}
			using(ECDsa ecdsa=x509Certificate2.GetECDsaPrivateKey()) {
				SetCng(testKey,((ECDsaCng)ecdsa).Key);
			}
			return testKey;
		}

		///<summary>True when the key still opens, through CAPI for a CAPI key container and through CNG for a CNG key, in its key set.</summary>
		public bool Exists() {
			if(IsCapi) {
				try {
					using(new RSACryptoServiceProvider(GetCspParameters())) {
					}
					return true;
				}
				catch(CryptographicException) {
					return false;//"Keyset does not exist".
				}
			}
			return CngKey.Exists(Name,new CngProvider(Provider),IsMachine ? CngKeyOpenOptions.MachineKey : CngKeyOpenOptions.None);
		}

		///<summary>The test's clean-up: deletes the key if it is still there, through its own key store.</summary>
		public void DeleteIfExists() {
			if(!Exists()) {
				return;
			}
			if(IsCapi) {
				using(RSACryptoServiceProvider rsaCryptoServiceProvider=new RSACryptoServiceProvider(GetCspParameters())) {
					rsaCryptoServiceProvider.PersistKeyInCsp=false;//Dispose then deletes the key container.
				}
				return;
			}
			CngKey cngKey=CngKey.Open(Name,new CngProvider(Provider),IsMachine ? CngKeyOpenOptions.MachineKey : CngKeyOpenOptions.None);
			cngKey.Delete();//Also disposes it.
		}

		private CspParameters GetCspParameters() {
			CspParameters cspParameters=new CspParameters(ProviderType,Provider,Name);
			cspParameters.Flags=CspProviderFlags.UseExistingKey;
			if(IsMachine) {
				cspParameters.Flags|=CspProviderFlags.UseMachineKeyStore;
			}
			return cspParameters;
		}

		private static void SetCng(TestKey testKey,CngKey cngKey) {
			testKey.IsMachine=cngKey.IsMachineKey;
			testKey.Provider=cngKey.Provider.Provider;
			testKey.Name=cngKey.KeyName;
			testKey.FilePath=Path.Combine(GetCryptoFolder(testKey.IsMachine),"Keys",cngKey.UniqueName);
		}

		public static string GetCryptoFolder(bool isMachine) {
			Environment.SpecialFolder specialFolder=isMachine ? Environment.SpecialFolder.CommonApplicationData : Environment.SpecialFolder.ApplicationData;
			return Path.Combine(Environment.GetFolderPath(specialFolder),"Microsoft","Crypto");
		}

		public static string GetCurrentUserSid() {
			using(WindowsIdentity windowsIdentity=WindowsIdentity.GetCurrent()) {
				return windowsIdentity.User.Value;
			}
		}
	}
}
