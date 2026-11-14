// A certificate store of the current user for the client certificate key tests (DIRECT-CERT-FOLLOWUP): a store with a name of its own, made
// when the first certificate is added and deleted afterwards (CertUnregisterSystemStore), and the throwaway keys of its certificates, made at
// run time in the current user's key set (a CAPI key container, a CNG RSA key, a CNG ECDSA key) and deleted afterwards. Nothing opens
// LocalMachine\My, CurrentUser\My or any other real store, and no real certificate or key is touched. No administrator rights are needed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DbConnectionSecurityTests {
	///<summary>The kinds of private key a client certificate's .pfx can give: a CAPI key container (a .pfx that names no CNG provider, as
	///OpenSSL writes it, or one that names a CAPI provider), a CNG RSA key and a CNG ECDSA key (a .pfx that names the CNG provider).</summary>
	public enum TestKeyKind {
		CapiRsa,
		CngRsa,
		CngEcdsa,
	}

	///<summary>What a certificate of a test .pfx is (ClientCertificateTestStore.WritePfxOf): a client certificate (clientAuth, not a CA) or a
	///CA certificate (Basic Constraints CA=true), each with its key in the file.</summary>
	public enum TestPfxCertificate {
		Client,
		Ca,
	}

	///<summary>A store of the current user with a name of its own, used for as long as the instance lives. Dispose deletes every key it
	///recorded (the keys of the certificates it added, and those TrackKeys was given) that is still there, then the store. Dispose never
	///throws, so that it never hides a test's own failure: what it could not delete is kept for AssertCleanedUp, which the tests call after
	///each test.</summary>
	public sealed class ClientCertificateTestStore:IDisposable {
		[DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]
		private static extern bool CertUnregisterSystemStore(string systemStore,uint flags);
		private const uint CERT_SYSTEM_STORE_CURRENT_USER=0x00010000;
		private const uint CERT_STORE_DELETE_FLAG=0x00000010;
		///<summary>What CertUnregisterSystemStore gives for a store that does not exist (one no certificate was ever added to).</summary>
		private const int ERROR_FILE_NOT_FOUND=2;
		///<summary>PROV_RSA_AES, the type of the "Microsoft Enhanced RSA and AES Cryptographic Provider".</summary>
		private const int PROV_RSA_AES=24;
		private const string PROVIDER_CAPI="Microsoft Enhanced RSA and AES Cryptographic Provider";
		///<summary>What the clean-ups could not do (a key or a store left behind), until AssertCleanedUp reports it.</summary>
		private static readonly List<string> _listCleanUpProblems=new List<string>();
		public readonly string Name="OpenPmsDbSecTest_"+Guid.NewGuid().ToString("N");
		///<summary>The .pfx files' password, made at run time and kept in memory only.</summary>
		private readonly string _password=Guid.NewGuid().ToString("N");
		private readonly List<TestKey> _listKeys=new List<TestKey>();

		///<summary>The password of the .pfx files this store writes (WritePfxWithCa), for the import under test.</summary>
		public string Password {
			get {
				return _password;
			}
		}

		///<summary>Makes a self-signed client certificate with a throwaway key of this kind, writes it as a .pfx in memory and imports that as the
		///connection settings tool does (PersistKeySet, never Exportable), but into the current user's key set (UserKeySet) and this store.
		///Returns where its key is.</summary>
		public TestKey AddCertificate(TestKeyKind testKeyKind) {
			return AddPfx(MakePfx(testKeyKind,out string thumbprint));
		}

		///<summary>AddCertificate for a .pfx file this store wrote (WritePfx), imported the same way: the store's certificate gets a key of its
		///own, apart from any key that an import of the same file under test stores.</summary>
		public TestKey AddCertificate(string pathPfx) {
			return AddPfx(File.ReadAllBytes(pathPfx));
		}

		///<summary>AddCertificate for a certificate the test made in memory with its key (TestHelpers.NewCertificate): written as a .pfx in memory
		///and imported the same way.</summary>
		public TestKey AddCertificate(X509Certificate2 x509Certificate2WithKey) {
			return AddPfx(x509Certificate2WithKey.Export(X509ContentType.Pfx,_password));
		}

		private TestKey AddPfx(byte[] arrayPfx) {
			using(X509Certificate2 x509Certificate2=new X509Certificate2(arrayPfx,_password,X509KeyStorageFlags.UserKeySet|X509KeyStorageFlags.PersistKeySet)) {
				//Recorded before it is inspected (TestKey.Track): a throw while the test finds the key cannot leak it.
				TestKey testKey=TestKey.Track(x509Certificate2,_listKeys);
				using(X509Store x509Store=new X509Store(Name,StoreLocation.CurrentUser)) {
					x509Store.Open(OpenFlags.ReadWrite);//Creates the store.
					x509Store.Add(x509Certificate2);
				}
				return testKey;
			}
		}

		///<summary>Records the key of each certificate with one, as an import with PersistKeySet has just stored them (the code under test's
		///ActionAfterKeysStoredForTests), so that Dispose deletes each one still there, whatever the code under test did. By thumbprint.</summary>
		public Dictionary<string,TestKey> TrackKeys(X509Certificate2Collection x509Certificate2Collection) {
			Dictionary<string,TestKey> dictKeys=new Dictionary<string,TestKey>();
			foreach(X509Certificate2 x509Certificate2 in x509Certificate2Collection) {
				if(x509Certificate2.HasPrivateKey) {
					dictKeys[x509Certificate2.Thumbprint]=TestKey.Track(x509Certificate2,_listKeys);
				}
			}
			return dictKeys;
		}

		///<summary>Adds to the store a new self-signed certificate made with this key, opened where it is, so that a certificate of the store uses
		///the same key container (as a key that was there before an import would be). Its key is the one testKey records, which Dispose deletes.
		///Returns its thumbprint.</summary>
		public string AddCertificateUsingKeyOf(TestKey testKey) {
			string subject="CN=synthetic-dbsec-samekey-"+Guid.NewGuid().ToString("N");
			using(AsymmetricAlgorithm asymmetricAlgorithm=testKey.OpenKey())
			using(X509Certificate2 x509Certificate2=CreateSelfSigned(NewRequest(subject,asymmetricAlgorithm,isCa:false),DateTimeOffset.Now.AddDays(30)))
			{
				using(X509Store x509Store=new X509Store(Name,StoreLocation.CurrentUser)) {
					x509Store.Open(OpenFlags.ReadWrite);
					x509Store.Add(x509Certificate2);
				}
				return x509Certificate2.Thumbprint;
			}
		}

		///<summary>Writes into the folder a .pfx with a self-signed client certificate and a throwaway key of this kind, made as AddCertificate
		///makes one; nothing is stored until the file is imported (AddCertificate(string), or the import under test), each import into a key
		///container of its own. Returns the path.</summary>
		public string WritePfx(string folder,TestKeyKind testKeyKind,out string thumbprint) {
			string path=Path.Combine(folder,"client-"+testKeyKind+".pfx");
			File.WriteAllBytes(path,MakePfx(testKeyKind,out thumbprint));
			return path;
		}

		///<summary>WritePfx twice for one certificate and one key: the files differ in the key container name they record for a CAPI key. A .pfx
		///written from a CAPI key records the name of its container, and Windows' import names the container it stores the key in after it
		///when no container has that name (measured on Windows 11: an import after the first one's key was deleted lands in the same container
		///again), so the second file's key comes from a scratch container of its own, which holds the same key. A CNG key is ephemeral and
		///records no name: both files are written from it. Returns the first file's path.</summary>
		public string WritePfxTwice(string folder,TestKeyKind testKeyKind,out string thumbprint,out string pathSecond) {
			string subject="CN=synthetic-dbsec-key-"+Guid.NewGuid().ToString("N");
			string pathFirst=Path.Combine(folder,"client-"+testKeyKind+"-1.pfx");
			pathSecond=Path.Combine(folder,"client-"+testKeyKind+"-2.pfx");
			AsymmetricAlgorithm asymmetricAlgorithm=NewScratchKey(testKeyKind);
			RSACryptoServiceProvider rsaCryptoServiceProviderSecond=null;
			try {
				using(X509Certificate2 x509Certificate2=CreateSelfSigned(NewRequest(subject,asymmetricAlgorithm,isCa:false),DateTimeOffset.Now.AddDays(30))) {
					thumbprint=x509Certificate2.Thumbprint;
					File.WriteAllBytes(pathFirst,x509Certificate2.Export(X509ContentType.Pfx,_password));
					if(!(asymmetricAlgorithm is RSACryptoServiceProvider)) {
						File.WriteAllBytes(pathSecond,x509Certificate2.Export(X509ContentType.Pfx,_password));
						return pathFirst;
					}
					CspParameters cspParameters=new CspParameters(PROV_RSA_AES,PROVIDER_CAPI,"OpenPmsDbSecTest_"+Guid.NewGuid().ToString("N"));
					rsaCryptoServiceProviderSecond=new RSACryptoServiceProvider(cspParameters);
					rsaCryptoServiceProviderSecond.ImportCspBlob(((RSACryptoServiceProvider)asymmetricAlgorithm).ExportCspBlob(true));
					using(X509Certificate2 x509Certificate2Public=new X509Certificate2(x509Certificate2.RawData))
					using(X509Certificate2 x509Certificate2Second=x509Certificate2Public.CopyWithPrivateKey(rsaCryptoServiceProviderSecond))
					{
						File.WriteAllBytes(pathSecond,x509Certificate2Second.Export(X509ContentType.Pfx,_password));
					}
				}
			}
			finally {
				DeleteScratchKey(asymmetricAlgorithm);
				DeleteScratchKey(rsaCryptoServiceProviderSecond);
			}
			return pathFirst;
		}

		///<summary>Writes into the folder a .pfx with a client certificate and the CA certificate that issued it, each with a throwaway key of this
		///kind (the CA's key in the file, as some providers' files have it), the client certificate first (WritePfxOf). Returns the path.
		///</summary>
		public string WritePfxWithCa(string folder,TestKeyKind testKeyKind,out string thumbprintClient,out string thumbprintCa) {
			string path=WritePfxOf(folder,testKeyKind,out List<string> listThumbprints,out List<string> listSubjects,TestPfxCertificate.Client,
				TestPfxCertificate.Ca);
			thumbprintClient=listThumbprints[0];
			thumbprintCa=listThumbprints[1];
			return path;
		}

		///<summary>Writes into the folder a .pfx (a file name of its own) with these certificates, in this order, each with a throwaway key of
		///this kind made as AddCertificate makes one (a scratch CAPI container, deleted again, or an ephemeral CNG key), so the file names their
		///provider; nothing is stored until the file is imported. A CA certificate is self-signed, with Basic Constraints CA=true; a client
		///certificate has the client authentication EKU, and is issued by the file's first CA certificate, or self-signed when the file has none.
		///Returns the path; the thumbprints and subjects in the file's order.</summary>
		public string WritePfxOf(string folder,TestKeyKind testKeyKind,out List<string> listThumbprints,out List<string> listSubjects,
			params TestPfxCertificate[] arrayCertificates)
		{
			string id=Guid.NewGuid().ToString("N");
			string path=Path.Combine(folder,"pfx-"+testKeyKind+"-"+string.Join("-",arrayCertificates)+"-"+id.Substring(0,8)+".pfx");
			List<AsymmetricAlgorithm> listScratchKeys=new List<AsymmetricAlgorithm>();
			X509Certificate2[] arrayMade=new X509Certificate2[arrayCertificates.Length];
			try {
				//The first CA certificate first, so that the client certificates can be issued by it.
				int indexCa=Array.IndexOf(arrayCertificates,TestPfxCertificate.Ca);
				AsymmetricAlgorithm asymmetricAlgorithmCa=null;
				if(indexCa>=0) {
					asymmetricAlgorithmCa=NewScratchKey(testKeyKind);
					listScratchKeys.Add(asymmetricAlgorithmCa);
					arrayMade[indexCa]=CreateSelfSigned(NewRequest("CN=synthetic-dbsec-ca-"+id+"-"+indexCa,asymmetricAlgorithmCa,isCa:true),
						DateTimeOffset.Now.AddDays(60));
				}
				for(int i=0;i<arrayCertificates.Length;i++) {
					if(arrayMade[i]!=null) {
						continue;
					}
					bool isCa=arrayCertificates[i]==TestPfxCertificate.Ca;
					AsymmetricAlgorithm asymmetricAlgorithm=NewScratchKey(testKeyKind);
					listScratchKeys.Add(asymmetricAlgorithm);
					CertificateRequest certificateRequest=NewRequest((isCa ? "CN=synthetic-dbsec-ca-" : "CN=synthetic-dbsec-client-")+id+"-"+i,
						asymmetricAlgorithm,isCa);
					if(isCa || asymmetricAlgorithmCa==null) {
						arrayMade[i]=CreateSelfSigned(certificateRequest,DateTimeOffset.Now.AddDays(30));
						continue;
					}
					X509SignatureGenerator x509SignatureGeneratorCa=(asymmetricAlgorithmCa is RSA)
						? X509SignatureGenerator.CreateForRSA((RSA)asymmetricAlgorithmCa,RSASignaturePadding.Pkcs1)
						: X509SignatureGenerator.CreateForECDsa((ECDsa)asymmetricAlgorithmCa);
					byte[] arraySerial=Guid.NewGuid().ToByteArray().Take(8).ToArray();
					arraySerial[0]&=0x7F;//A positive serial number.
					using(X509Certificate2 x509Certificate2Public=certificateRequest.Create(arrayMade[indexCa].SubjectName,x509SignatureGeneratorCa,
						DateTimeOffset.Now.AddDays(-1),DateTimeOffset.Now.AddDays(30),arraySerial))
					{
						arrayMade[i]=(asymmetricAlgorithm is RSA)
							? x509Certificate2Public.CopyWithPrivateKey((RSA)asymmetricAlgorithm)
							: x509Certificate2Public.CopyWithPrivateKey((ECDsa)asymmetricAlgorithm);
					}
				}
				X509Certificate2Collection x509Certificate2Collection=new X509Certificate2Collection();
				for(int i=0;i<arrayMade.Length;i++) {
					x509Certificate2Collection.Add(arrayMade[i]);
				}
				File.WriteAllBytes(path,x509Certificate2Collection.Export(X509ContentType.Pfx,_password));
				listThumbprints=arrayMade.Select(x => x.Thumbprint).ToList();
				listSubjects=arrayMade.Select(x => x.Subject).ToList();
			}
			finally {
				foreach(X509Certificate2 x509Certificate2 in arrayMade) {
					x509Certificate2?.Dispose();
				}
				foreach(AsymmetricAlgorithm asymmetricAlgorithm in listScratchKeys) {
					DeleteScratchKey(asymmetricAlgorithm);
				}
			}
			return path;
		}

		///<summary>Two providers with one key container name (the DCFU3 fix round's second review, finding 1): adds to the store a self-signed
		///client certificate whose entry has a CNG key of the Microsoft Software Key Storage Provider in the current user's key set (imported
		///from a .pfx that names the CNG provider, as AddCertificate imports one), and writes into the folder a .pfx of the same certificate and
		///key that names a CAPI provider and records a CAPI key container with the CNG key's name. That CAPI container is made only to write the
		///file and deleted again at once (after checking that no CAPI container had the name before), so an import of the file stores its key in
		///a CAPI container of that very name (WritePfxTwice's measurement). Returns the path; testKeyEntry is the entry's CNG key.</summary>
		public string AddCngEntryAndWriteCapiPfxOfItsName(string folder,out string thumbprint,out TestKey testKeyEntry) {
			string subject="CN=synthetic-dbsec-key-"+Guid.NewGuid().ToString("N");
			string path=Path.Combine(folder,"client-capi-named-as-cng-"+Guid.NewGuid().ToString("N").Substring(0,8)+".pfx");
			using(RSACng rsaCng=new RSACng(2048))//Ephemeral: only the store's import below stores it.
			using(X509Certificate2 x509Certificate2=CreateSelfSigned(NewRequest(subject,rsaCng,isCa:false),DateTimeOffset.Now.AddDays(30)))
			{
				thumbprint=x509Certificate2.Thumbprint;
				testKeyEntry=AddPfx(x509Certificate2.Export(X509ContentType.Pfx,_password));
				Assert.IsFalse(testKeyEntry.IsCapi,"The test's premise: the entry's key is a CNG key.");
				Assert.IsFalse(testKeyEntry.IsMachine,"The test's premise: in the current user's key set.");
				CspParameters cspParameters=new CspParameters(PROV_RSA_AES,PROVIDER_CAPI,testKeyEntry.Name);
				Assert.IsFalse(IsCapiContainerThere(cspParameters),"The test's premise: no CAPI container of the current user is named "
					+testKeyEntry.Name+"; the test never writes into one it did not make.");
				RSACryptoServiceProvider rsaCryptoServiceProviderCapi=new RSACryptoServiceProvider(cspParameters);
				try {
					rsaCryptoServiceProviderCapi.ImportParameters(rsaCng.ExportParameters(true));
					using(X509Certificate2 x509Certificate2Public=new X509Certificate2(x509Certificate2.RawData))
					using(X509Certificate2 x509Certificate2Capi=x509Certificate2Public.CopyWithPrivateKey(rsaCryptoServiceProviderCapi))
					{
						File.WriteAllBytes(path,x509Certificate2Capi.Export(X509ContentType.Pfx,_password));
					}
				}
				finally {
					DeleteScratchKey(rsaCryptoServiceProviderCapi);
				}
				if(IsCapiContainerThere(cspParameters)) {
					AddCleanUpProblem("The scratch CAPI container "+testKeyEntry.Name+" of the current user is still there. Delete it by hand: certutil "
						+"-user -csp \""+PROVIDER_CAPI+"\" -delkey \""+testKeyEntry.Name+"\".");
					Assert.Fail("The test's premise: the scratch CAPI container "+testKeyEntry.Name+" is deleted before the import.");
				}
			}
			return path;
		}

		///<summary>True when a CAPI key container with these parameters' provider and name opens in the current user's key set.</summary>
		private static bool IsCapiContainerThere(CspParameters cspParameters) {
			CspParameters cspParametersExisting=new CspParameters(cspParameters.ProviderType,cspParameters.ProviderName,cspParameters.KeyContainerName);
			cspParametersExisting.Flags=CspProviderFlags.UseExistingKey;
			try {
				using(new RSACryptoServiceProvider(cspParametersExisting)) {
				}
				return true;
			}
			catch(CryptographicException) {
				return false;//"Keyset does not exist".
			}
		}

		///<summary>Makes the store's entry for the certificate name its key container in the machine key set instead of the user's (the
		///CRYPT_MACHINE_KEYSET flag of its key provider information, TestKeyProvInfo), as an entry whose key is in another key set than the
		///import's (the test store's import uses the user's): nothing is made, opened or deleted in the machine key set, and the user key the
		///entry named stays recorded for the clean-up. Checked by reading the entry again. Returns its key provider information now.</summary>
		public TestKeyProvInfo NameMachineKeySetForEntry(string thumbprint) {
			using(X509Store x509Store=new X509Store(Name,StoreLocation.CurrentUser)) {
				x509Store.Open(OpenFlags.ReadWrite|OpenFlags.OpenExistingOnly);
				X509Certificate2Collection x509Certificate2Collection=x509Store.Certificates.Find(X509FindType.FindByThumbprint,thumbprint,false);
				try {
					Assert.AreEqual(1,x509Certificate2Collection.Count,"The test's premise: the store has the certificate.");
					TestKeyProvInfo testKeyProvInfo=TestKeyProvInfo.Read(x509Certificate2Collection[0]);
					Assert.IsTrue(testKeyProvInfo.IsNamed && !testKeyProvInfo.IsMachine,"The test's premise: the entry names a key of the user's key set.");
					testKeyProvInfo.Flags|=TestKeyProvInfo.CRYPT_MACHINE_KEYSET;
					testKeyProvInfo.Write(x509Certificate2Collection[0]);
				}
				finally {
					foreach(X509Certificate2 x509Certificate2 in x509Certificate2Collection) {
						x509Certificate2.Reset();
					}
				}
			}
			using(X509Certificate2 x509Certificate2=Find(thumbprint)) {
				TestKeyProvInfo testKeyProvInfoNow=TestKeyProvInfo.Read(x509Certificate2);
				Assert.IsTrue(testKeyProvInfoNow.IsMachine,"The test's premise: the store keeps the entry's key provider information as the test set it.");
				return testKeyProvInfoNow;
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

		///<summary>Deletes every key this store recorded that is still there, then the store with its certificates. Never throws: what is left
		///is kept for AssertCleanedUp.</summary>
		public void Dispose() {
			foreach(TestKey testKey in _listKeys) {
				string problem=testKey.CleanUp();
				if(problem!="") {
					AddCleanUpProblem(problem);
				}
			}
			if(!CertUnregisterSystemStore(Name,CERT_SYSTEM_STORE_CURRENT_USER|CERT_STORE_DELETE_FLAG)) {
				int error=Marshal.GetLastWin32Error();
				if(error!=ERROR_FILE_NOT_FOUND) {//Not found: no certificate was ever added, so there is no store to delete.
					AddCleanUpProblem("The test store CurrentUser\\"+Name+" could not be deleted: CertUnregisterSystemStore failed with Win32 error "
						+error+". Delete it by hand (certmgr.msc, or the registry key HKCU\\Software\\Microsoft\\SystemCertificates\\"+Name+").");
					return;
				}
			}
			if(IsStoreThere()) {
				AddCleanUpProblem("The test store CurrentUser\\"+Name+" is still there after CertUnregisterSystemStore. Delete it by hand.");
			}
		}

		///<summary>The tests' check after each test (their TestCleanup): fails with what the test stores' clean-ups could not do since the last
		///check, and forgets it.</summary>
		public static void AssertCleanedUp() {
			List<string> listProblems;
			lock(_listCleanUpProblems) {
				listProblems=_listCleanUpProblems.ToList();
				_listCleanUpProblems.Clear();
			}
			if(listProblems.Count>0) {
				Assert.Fail("A test store left something behind on this computer:\r\n"+string.Join("\r\n",listProblems));
			}
		}

		private static void AddCleanUpProblem(string problem) {
			Console.WriteLine(problem);//In the test's output too.
			lock(_listCleanUpProblems) {
				_listCleanUpProblems.Add(problem);
			}
		}

		private bool IsStoreThere() {
			using(X509Store x509Store=new X509Store(Name,StoreLocation.CurrentUser)) {
				try {
					x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
					return true;
				}
				catch(CryptographicException) {
					return false;
				}
			}
		}

		private byte[] MakePfx(TestKeyKind testKeyKind,out string thumbprint) {
			string subject="CN=synthetic-dbsec-key-"+Guid.NewGuid().ToString("N");
			AsymmetricAlgorithm asymmetricAlgorithm=NewScratchKey(testKeyKind);
			try {
				using(X509Certificate2 x509Certificate2=CreateSelfSigned(NewRequest(subject,asymmetricAlgorithm,isCa:false),DateTimeOffset.Now.AddDays(30))) {
					thumbprint=x509Certificate2.Thumbprint;
					return x509Certificate2.Export(X509ContentType.Pfx,_password);
				}
			}
			finally {
				DeleteScratchKey(asymmetricAlgorithm);
			}
		}

		///<summary>A throwaway key of this kind for a certificate the test writes as a .pfx: for a CAPI key, a key in a scratch CAPI container of
		///the current user, so the .pfx names a CAPI provider and Windows imports it into a CAPI container, as it does a .pfx that names no CNG
		///provider (DeleteScratchKey deletes the scratch container, and the import makes its own); for a CNG key, an ephemeral key, in memory
		///only, so the .pfx names the CNG provider.</summary>
		private static AsymmetricAlgorithm NewScratchKey(TestKeyKind testKeyKind) {
			switch(testKeyKind) {
				case TestKeyKind.CapiRsa:
					CspParameters cspParameters=new CspParameters(PROV_RSA_AES,PROVIDER_CAPI,"OpenPmsDbSecTest_"+Guid.NewGuid().ToString("N"));
					return new RSACryptoServiceProvider(2048,cspParameters);
				case TestKeyKind.CngRsa:
					return new RSACng(2048);
				default:
					return new ECDsaCng(256);
			}
		}

		private static void DeleteScratchKey(AsymmetricAlgorithm asymmetricAlgorithm) {
			if(asymmetricAlgorithm is RSACryptoServiceProvider) {
				((RSACryptoServiceProvider)asymmetricAlgorithm).PersistKeyInCsp=false;//Clear then deletes the scratch container.
				((RSACryptoServiceProvider)asymmetricAlgorithm).Clear();
				return;
			}
			asymmetricAlgorithm?.Dispose();
		}

		///<summary>A certificate request for this key with the client authentication EKU, as the tool's client certificate has, or a CA's basic
		///constraints and key usage.</summary>
		private static CertificateRequest NewRequest(string subject,AsymmetricAlgorithm asymmetricAlgorithm,bool isCa) {
			CertificateRequest certificateRequest;
			if(asymmetricAlgorithm is RSA) {
				certificateRequest=new CertificateRequest(subject,(RSA)asymmetricAlgorithm,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
			}
			else {
				certificateRequest=new CertificateRequest(subject,(ECDsa)asymmetricAlgorithm,HashAlgorithmName.SHA256);
			}
			if(isCa) {
				certificateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
				certificateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign|X509KeyUsageFlags.CrlSign,true));
				return certificateRequest;
			}
			OidCollection oidCollection=new OidCollection();
			oidCollection.Add(new Oid("1.3.6.1.5.5.7.3.2"));//TLS client authentication, as the tool's client certificate has.
			certificateRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(oidCollection,false));
			return certificateRequest;
		}

		private static X509Certificate2 CreateSelfSigned(CertificateRequest certificateRequest,DateTimeOffset dateTimeOffsetNotAfter) {
			return certificateRequest.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1),dateTimeOffsetNotAfter);
		}
	}

	///<summary>Where a test certificate's private key is, found by the test itself, apart from the code under test: a CAPI key container (named
	///through CAPI, X509Certificate2.PrivateKey) or a CNG key, in the key set it lives in, and its file. Exists opens it through its own key
	///store, CAPI or CNG.</summary>
	public sealed class TestKey {
		public string Thumbprint;
		public string Subject;
		public bool IsCapi;
		public bool IsMachine;
		public string Provider;
		public int ProviderType;
		///<summary>The CAPI key container's name or the CNG key's name.</summary>
		public string Name;
		///<summary>The key's file: under Microsoft\Crypto\RSA\(the user's SID) or RSA\MachineKeys for a CAPI key, Microsoft\Crypto\Keys for a CNG
		///key, of %APPDATA% (the user's key set) or %ProgramData% (the machine's).</summary>
		public string FilePath;
		///<summary>A copy of the certificate with its key (the same certificate context, so the same key provider information), taken before
		///anything inspects the key and released by CleanUp: when Locate throws, CleanUp deletes the key through it.</summary>
		private X509Certificate2 _x509Certificate2;
		private bool _isLocated;

		private TestKey() {
		}

		///<summary>Records the certificate's key in listKeys first, then finds where it is (Locate): a throw while the key is inspected cannot leak
		///it, since the clean-up then deletes it through the certificate. A copy of the certificate that could not be made is reported by the
		///clean-up (DeleteThroughCertificate), never taken for a key that is not there. Called for certificates with a key only.</summary>
		public static TestKey Track(X509Certificate2 x509Certificate2,List<TestKey> listKeys) {
			TestKey testKey=new TestKey();
			listKeys.Add(testKey);
			testKey.Thumbprint=x509Certificate2.Thumbprint;
			testKey.Subject=x509Certificate2.Subject;
			testKey._x509Certificate2=new X509Certificate2(x509Certificate2);
			testKey.Locate();
			return testKey;
		}

		private void Locate() {
			RSACryptoServiceProvider rsaCryptoServiceProviderCapi=null;
			try {
				rsaCryptoServiceProviderCapi=_x509Certificate2.PrivateKey as RSACryptoServiceProvider;
			}
			catch(CryptographicException) {
				//A CNG RSA key.
			}
			catch(NotSupportedException) {
				//An ECDSA key.
			}
			if(rsaCryptoServiceProviderCapi!=null) {
				CspKeyContainerInfo cspKeyContainerInfo=rsaCryptoServiceProviderCapi.CspKeyContainerInfo;
				IsCapi=true;
				IsMachine=cspKeyContainerInfo.MachineKeyStore;
				Provider=cspKeyContainerInfo.ProviderName;
				ProviderType=cspKeyContainerInfo.ProviderType;
				Name=cspKeyContainerInfo.KeyContainerName;
				string folderRsa=IsMachine ? "MachineKeys" : GetCurrentUserSid();
				FilePath=Path.Combine(GetCryptoFolder(IsMachine),"RSA",folderRsa,cspKeyContainerInfo.UniqueKeyContainerName);
				_isLocated=true;
				return;
			}
			using(RSA rsa=_x509Certificate2.GetRSAPrivateKey()) {
				if(rsa is RSACng) {
					SetCng(((RSACng)rsa).Key);
					return;
				}
			}
			using(ECDsa ecdsa=_x509Certificate2.GetECDsaPrivateKey()) {
				SetCng(((ECDsaCng)ecdsa).Key);
			}
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

		///<summary>The key, opened where it is through its own key store (CAPI or CNG): an RSA or ECDSA object, which the caller disposes. Disposing
		///it leaves the key.</summary>
		public AsymmetricAlgorithm OpenKey() {
			if(IsCapi) {
				return new RSACryptoServiceProvider(GetCspParameters());//Opened by name, so it persists: Dispose leaves the container.
			}
			using(CngKey cngKey=CngKey.Open(Name,new CngProvider(Provider),IsMachine ? CngKeyOpenOptions.MachineKey : CngKeyOpenOptions.None)) {
				//The RSACng and ECDsaCng take their own handle of the key.
				if(cngKey.AlgorithmGroup==CngAlgorithmGroup.Rsa) {
					return new RSACng(cngKey);
				}
				return new ECDsaCng(cngKey);
			}
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

		///<summary>The store's clean-up: deletes the key if it is still there (DeleteIfExists, or through the certificate when Locate never found
		///it), checks that it is gone, and releases the copy of the certificate. "" when it is gone, else what is left. Never throws.</summary>
		public string CleanUp() {
			try {
				if(!_isLocated) {
					return DeleteThroughCertificate();
				}
				DeleteIfExists();
				if(Exists()) {
					return "The test key of "+Subject+" ("+Thumbprint+") is still there: "+FilePath;
				}
				return "";
			}
			catch(Exception ex) {
				return "The test key of "+Subject+" ("+Thumbprint+") could not be deleted: "+ex.Message.Trim()+" "+FilePath;
			}
			finally {
				_x509Certificate2?.Dispose();
			}
		}

		///<summary>For a key Locate did not find: deletes it through the certificate (a CAPI container through X509Certificate2.PrivateKey, a CNG
		///key through its Key). "" when it was deleted or there was none to delete, else why not. Without the copy of the certificate (Track
		///could not make it) the key cannot be found, which is a problem, never "nothing to delete": Track is called only for a certificate with
		///a key, which the import has just stored.</summary>
		private string DeleteThroughCertificate() {
			if(_x509Certificate2==null) {
				return "The test key of "+Subject+" ("+Thumbprint+") was never located, and the copy of its certificate could not be made, so the "
					+"clean-up cannot find it to delete it. Look for it by hand under "+GetCryptoFolder(false)+".";
			}
			if(!_x509Certificate2.HasPrivateKey) {
				return "";
			}
			RSACryptoServiceProvider rsaCryptoServiceProviderCapi=null;
			try {
				rsaCryptoServiceProviderCapi=_x509Certificate2.PrivateKey as RSACryptoServiceProvider;
			}
			catch(CryptographicException) {
			}
			catch(NotSupportedException) {
			}
			if(rsaCryptoServiceProviderCapi!=null) {
				rsaCryptoServiceProviderCapi.PersistKeyInCsp=false;//Clear then deletes the key container.
				rsaCryptoServiceProviderCapi.Clear();
				return "";
			}
			using(RSA rsa=_x509Certificate2.GetRSAPrivateKey()) {
				if(rsa is RSACng) {
					((RSACng)rsa).Key.Delete();
					return "";
				}
			}
			using(ECDsa ecdsa=_x509Certificate2.GetECDsaPrivateKey()) {
				if(ecdsa is ECDsaCng) {
					((ECDsaCng)ecdsa).Key.Delete();
					return "";
				}
			}
			return "The test key of "+Subject+" ("+Thumbprint+") was never located and could not be deleted through its certificate.";
		}

		private CspParameters GetCspParameters() {
			CspParameters cspParameters=new CspParameters(ProviderType,Provider,Name);
			cspParameters.Flags=CspProviderFlags.UseExistingKey;
			if(IsMachine) {
				cspParameters.Flags|=CspProviderFlags.UseMachineKeyStore;
			}
			return cspParameters;
		}

		private void SetCng(CngKey cngKey) {
			IsMachine=cngKey.IsMachineKey;
			Provider=cngKey.Provider.Provider;
			Name=cngKey.KeyName;
			FilePath=Path.Combine(GetCryptoFolder(IsMachine),"Keys",cngKey.UniqueName);
			_isLocated=true;
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

	///<summary>A certificate's key provider information (CERT_KEY_PROV_INFO_PROP_ID), read and written by the tests themselves, apart from the
	///code under test: the key container, provider and key set the certificate names, read without opening the key. Write sets it on a
	///certificate of the test store (the store keeps it), with no provider parameters.</summary>
	public sealed class TestKeyProvInfo {
		[DllImport("crypt32.dll",SetLastError=true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool CertGetCertificateContextProperty(IntPtr pCertContext,uint dwPropId,IntPtr pvData,ref uint pcbData);
		[DllImport("crypt32.dll",SetLastError=true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool CertSetCertificateContextProperty(IntPtr pCertContext,uint dwPropId,uint dwFlags,IntPtr pvData);
		private const uint CERT_KEY_PROV_INFO_PROP_ID=2;
		///<summary>CRYPT_MACHINE_KEYSET (CAPI), NCRYPT_MACHINE_KEY_FLAG (CNG): the same bit of dwFlags.</summary>
		public const uint CRYPT_MACHINE_KEYSET=0x20;

		[StructLayout(LayoutKind.Sequential)]
		private struct CRYPT_KEY_PROV_INFO {
			public IntPtr pwszContainerName;
			public IntPtr pwszProvName;
			public uint dwProvType;
			public uint dwFlags;
			public uint cProvParam;
			public IntPtr rgProvParam;
			public uint dwKeySpec;
		}

		///<summary>False when the certificate has no key provider information (no key).</summary>
		public bool IsNamed;
		public string ContainerName;
		public string ProviderName;
		public uint ProviderType;
		public uint Flags;
		public uint KeySpec;

		public bool IsMachine {
			get {
				return (Flags & CRYPT_MACHINE_KEYSET)!=0;
			}
		}

		///<summary>The certificate's key provider information. Throws when it cannot be read for another reason than its absence.</summary>
		public static TestKeyProvInfo Read(X509Certificate2 x509Certificate2) {
			if(x509Certificate2.Handle==IntPtr.Zero) {
				throw new InvalidOperationException("The certificate was already released.");
			}
			TestKeyProvInfo testKeyProvInfo=new TestKeyProvInfo();
			uint byteCount=0;
			if(!CertGetCertificateContextProperty(x509Certificate2.Handle,CERT_KEY_PROV_INFO_PROP_ID,IntPtr.Zero,ref byteCount)) {
				int error=Marshal.GetLastWin32Error();
				if(error==unchecked((int)0x80092004)) {//CRYPT_E_NOT_FOUND
					return testKeyProvInfo;
				}
				throw new System.ComponentModel.Win32Exception(error);
			}
			IntPtr intPtrData=Marshal.AllocHGlobal((int)byteCount);
			try {
				if(!CertGetCertificateContextProperty(x509Certificate2.Handle,CERT_KEY_PROV_INFO_PROP_ID,intPtrData,ref byteCount)) {
					throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
				}
				CRYPT_KEY_PROV_INFO cryptKeyProvInfo=(CRYPT_KEY_PROV_INFO)Marshal.PtrToStructure(intPtrData,typeof(CRYPT_KEY_PROV_INFO));
				testKeyProvInfo.IsNamed=true;
				testKeyProvInfo.ContainerName=Marshal.PtrToStringUni(cryptKeyProvInfo.pwszContainerName);
				testKeyProvInfo.ProviderName=Marshal.PtrToStringUni(cryptKeyProvInfo.pwszProvName);
				testKeyProvInfo.ProviderType=cryptKeyProvInfo.dwProvType;
				testKeyProvInfo.Flags=cryptKeyProvInfo.dwFlags;
				testKeyProvInfo.KeySpec=cryptKeyProvInfo.dwKeySpec;
				return testKeyProvInfo;
			}
			finally {
				Marshal.FreeHGlobal(intPtrData);
				GC.KeepAlive(x509Certificate2);
			}
		}

		///<summary>Sets this key provider information on the certificate (a certificate of the test store, opened for writing, which keeps it).
		///</summary>
		public void Write(X509Certificate2 x509Certificate2) {
			IntPtr intPtrContainer=Marshal.StringToHGlobalUni(ContainerName);
			IntPtr intPtrProvider=Marshal.StringToHGlobalUni(ProviderName);
			IntPtr intPtrData=IntPtr.Zero;
			try {
				CRYPT_KEY_PROV_INFO cryptKeyProvInfo=new CRYPT_KEY_PROV_INFO();
				cryptKeyProvInfo.pwszContainerName=intPtrContainer;
				cryptKeyProvInfo.pwszProvName=intPtrProvider;
				cryptKeyProvInfo.dwProvType=ProviderType;
				cryptKeyProvInfo.dwFlags=Flags;
				cryptKeyProvInfo.dwKeySpec=KeySpec;
				intPtrData=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(CRYPT_KEY_PROV_INFO)));
				Marshal.StructureToPtr(cryptKeyProvInfo,intPtrData,false);
				if(!CertSetCertificateContextProperty(x509Certificate2.Handle,CERT_KEY_PROV_INFO_PROP_ID,0,intPtrData)) {
					throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
				}
			}
			finally {
				if(intPtrData!=IntPtr.Zero) {
					Marshal.FreeHGlobal(intPtrData);
				}
				Marshal.FreeHGlobal(intPtrContainer);
				Marshal.FreeHGlobal(intPtrProvider);
				GC.KeepAlive(x509Certificate2);
			}
		}
	}
}
