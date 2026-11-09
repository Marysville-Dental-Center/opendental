// The private key of the connection settings tool's client certificate (DIRECT-CERT-FOLLOWUP): RemoveImported deletes it, a CAPI key container
// through CAPI, and GetPrivateKeyFilePath names its file in the key set it lives in. DIRECT-CERT found that .NET Framework's GetRSAPrivateKey
// opens a CAPI key through CNG, as an RSACng whose Key.Delete leaves the container; the tool did the same. Throwaway keys of the current user
// only, in a store of the current user with a name of its own (ClientCertificateTestStore); never LocalMachine\My, CurrentUser\My or a real
// certificate. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalDbSettings;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class ClientCertificateKeyTests {
		private static readonly TestKeyKind[] _arrayKinds=new[] { TestKeyKind.CapiRsa,TestKeyKind.CngRsa,TestKeyKind.CngEcdsa };

		[TestCleanup]
		public void CleanUp() {
			ClientCertificateStore.ActionBeforeKeyDeleteForTests=null;
			ClientCertificateTestStore.AssertCleanedUp();
		}

		///<summary>For a CAPI key, a CNG RSA key and a CNG ECDSA key: RemoveImported takes the certificate out of the store and deletes its key (it
		///no longer opens through its own key store, and its file is gone); the other certificate of the store and its key stay and still sign.
		///</summary>
		[TestMethod]
		public void RemoveImported_DeletesTheKeyOfEachKind_AndNothingElse() {
			foreach(TestKeyKind testKeyKind in _arrayKinds) {
				string label=testKeyKind.ToString();
				using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore()) {
					TestKey testKeyRemoved=clientCertificateTestStore.AddCertificate(testKeyKind);
					TestKey testKeyKept=clientCertificateTestStore.AddCertificate(testKeyKind);
					Assert.AreEqual(testKeyKind==TestKeyKind.CapiRsa,testKeyRemoved.IsCapi,label+": the key is of the kind the test made.");
					Assert.IsFalse(testKeyRemoved.IsMachine,label+": a key of the current user, never the machine's.");
					Assert.AreNotEqual(testKeyRemoved.FilePath,testKeyKept.FilePath,label+": two keys.");
					Assert.IsTrue(testKeyRemoved.Exists(),label+": the key opens before.");
					Assert.IsTrue(File.Exists(testKeyRemoved.FilePath),label+": its file is there before: "+testKeyRemoved.FilePath);
					Assert.IsTrue(clientCertificateTestStore.CanSign(testKeyRemoved.Thumbprint),label+": it signs before.");
					Assert.AreEqual("",ClientCertificateStore.RemoveImported(testKeyRemoved.Thumbprint,StoreLocation.CurrentUser,clientCertificateTestStore.Name),
						label+": no warning.");
					CollectionAssert.AreEqual(new List<string> { testKeyKept.Thumbprint },clientCertificateTestStore.GetThumbprints(),
						label+": only the removed certificate left the store.");
					Assert.IsFalse(testKeyRemoved.Exists(),label+": its key is deleted (it no longer opens through "+(testKeyRemoved.IsCapi ? "CAPI" : "CNG")+").");
					Assert.IsFalse(File.Exists(testKeyRemoved.FilePath),label+": its key file is gone: "+testKeyRemoved.FilePath);
					Assert.IsTrue(testKeyKept.Exists(),label+": the other certificate's key is not touched.");
					Assert.IsTrue(File.Exists(testKeyKept.FilePath),label+": nor its file.");
					Assert.IsTrue(clientCertificateTestStore.CanSign(testKeyKept.Thumbprint),label+": and it still signs.");
					Assert.AreEqual("",ClientCertificateStore.RemoveImported(testKeyRemoved.Thumbprint,StoreLocation.CurrentUser,clientCertificateTestStore.Name),
						label+": a certificate that is not there: nothing.");
					Assert.AreEqual("",ClientCertificateStore.RemoveImported("",StoreLocation.CurrentUser,clientCertificateTestStore.Name),label+": no thumbprint.");
					CollectionAssert.AreEqual(new List<string> { testKeyKept.Thumbprint },clientCertificateTestStore.GetThumbprints(),label);
					Assert.IsTrue(testKeyKept.Exists(),label);
				}
			}
		}

		///<summary>What DIRECT-CERT found, for the tool's key: .NET Framework's GetRSAPrivateKey gives a CAPI key as an RSACng, whose UniqueName is
		///no file of the CNG folder and whose Key.Delete leaves the CAPI container (the tool's old way, which this test does once by hand). The
		///tool now names the key's file and deletes the key through CAPI. The first three checks pin Windows' and .NET Framework's own behaviour
		///(Windows 11, .NET Framework 4.8), not the tool's: their messages say so.</summary>
		[TestMethod]
		public void CapiKey_IsNamedAndDeletedThroughCapi_NotThroughTheCngViewOfIt() {
			const string WINDOWS_CHANGED=" This pins Windows' own behaviour (Windows 11, .NET Framework 4.8), not the tool's: if a later Windows "
				+"names or deletes a CAPI key through its CNG view (the key storage provider), this check fails while the tool, which names and "
				+"deletes it through CAPI, is still right. Then change this check, not the tool; the checks of the tool below must still pass.";
			using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore()) {
				TestKey testKey=clientCertificateTestStore.AddCertificate(TestKeyKind.CapiRsa);
				Assert.IsTrue(testKey.IsCapi);
				using(X509Certificate2 x509Certificate2=clientCertificateTestStore.Find(testKey.Thumbprint)) {
					using(RSA rsa=x509Certificate2.GetRSAPrivateKey()) {
						if(rsa is RSACng) {//.NET Framework. (.NET gives an RSACryptoServiceProvider: nothing to show there.)
							CngKey cngKey=((RSACng)rsa).Key;
							string pathCngView=ClientCertificateStore.GetCngKeyFilePath(cngKey.IsMachineKey,cngKey.UniqueName);
							Assert.IsFalse(File.Exists(pathCngView),"The CNG view's UniqueName is no file of the CNG folder: "+pathCngView+"."
								+WINDOWS_CHANGED);
							cngKey.Delete();//The tool's old DeletePrivateKey.
							Assert.IsTrue(testKey.Exists(),"RSACng.Key.Delete leaves the CAPI container."+WINDOWS_CHANGED);
							Assert.IsTrue(File.Exists(testKey.FilePath),"RSACng.Key.Delete leaves the CAPI container's file: "+testKey.FilePath+"."
								+WINDOWS_CHANGED);
						}
					}
				}
				using(X509Certificate2 x509Certificate2=clientCertificateTestStore.Find(testKey.Thumbprint)) {
					Assert.AreEqual(testKey.FilePath,ClientCertificateStore.GetPrivateKeyFilePath(x509Certificate2),"The tool names the CAPI key's own file.");
				}
				Assert.AreEqual("",ClientCertificateStore.RemoveImported(testKey.Thumbprint,StoreLocation.CurrentUser,clientCertificateTestStore.Name));
				Assert.IsFalse(testKey.Exists(),"The tool deletes the CAPI container.");
				Assert.IsFalse(File.Exists(testKey.FilePath),"And its file.");
				Assert.AreEqual(0,clientCertificateTestStore.GetThumbprints().Count);
			}
		}

		///<summary>GetPrivateKeyFilePath names the file of each kind of key in the key set it lives in (the test finds it by itself, TestKey), and
		///the file is there; the machine key set of the tool's import maps to %ProgramData%, the user's to %APPDATA%; no key, no file.</summary>
		[TestMethod]
		public void GetPrivateKeyFilePath_NamesTheKeyFileInTheKeySetOfTheKey() {
			string folderMachine=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Microsoft","Crypto");
			string folderUser=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft","Crypto");
			string sid=TestKey.GetCurrentUserSid();
			Assert.AreEqual(Path.Combine(folderMachine,"RSA","MachineKeys","synthetic-unique"),ClientCertificateStore.GetCapiKeyFilePath(true,"synthetic-unique"));
			Assert.AreEqual(Path.Combine(folderUser,"RSA",sid,"synthetic-unique"),ClientCertificateStore.GetCapiKeyFilePath(false,"synthetic-unique"));
			Assert.AreEqual(Path.Combine(folderMachine,"Keys","synthetic-unique"),ClientCertificateStore.GetCngKeyFilePath(true,"synthetic-unique"));
			Assert.AreEqual(Path.Combine(folderUser,"Keys","synthetic-unique"),ClientCertificateStore.GetCngKeyFilePath(false,"synthetic-unique"));
			Assert.IsNull(ClientCertificateStore.GetCapiKeyFilePath(true,""));
			Assert.IsNull(ClientCertificateStore.GetCngKeyFilePath(false,null),"An ephemeral CNG key has no file.");
			using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore()) {
				foreach(TestKeyKind testKeyKind in _arrayKinds) {
					TestKey testKey=clientCertificateTestStore.AddCertificate(testKeyKind);
					string folderExpected=testKey.IsCapi ? Path.Combine(folderUser,"RSA",sid) : Path.Combine(folderUser,"Keys");
					Assert.AreEqual(folderExpected,Path.GetDirectoryName(testKey.FilePath),testKeyKind+": the test's own finding.");
					using(X509Certificate2 x509Certificate2=clientCertificateTestStore.Find(testKey.Thumbprint)) {
						string path=ClientCertificateStore.GetPrivateKeyFilePath(x509Certificate2);
						Assert.AreEqual(testKey.FilePath,path,testKeyKind.ToString());
						Assert.IsTrue(File.Exists(path),testKeyKind+": "+path);
						using(X509Certificate2 x509Certificate2Public=new X509Certificate2(x509Certificate2.RawData)) {
							Assert.IsNull(ClientCertificateStore.GetPrivateKeyFilePath(x509Certificate2Public),testKeyKind+": the certificate without its key.");
						}
					}
				}
			}
		}

		///<summary>DCFU2, item 2: RemoveImported takes the certificate out of the store first, then deletes its key (as DirectCertificateImport
		///removes a replaced certificate). When only the key cannot be deleted (a seam makes the delete fail), the warning says that the
		///certificate was removed and that its key was left, and names the key's file; it no longer says the certificate could not be removed.
		///For each kind of key.</summary>
		[TestMethod]
		public void RemoveImported_WhenOnlyTheKeyIsNotDeleted_SaysTheCertificateWasRemovedAndWhereItsKeyIs() {
			foreach(TestKeyKind testKeyKind in _arrayKinds) {
				string label=testKeyKind.ToString();
				using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore()) {
					try {
						TestKey testKey=clientCertificateTestStore.AddCertificate(testKeyKind);
						List<string> listThumbprintsDeleteTried=new List<string>();
						ClientCertificateStore.ActionBeforeKeyDeleteForTests=x509Certificate2 => {
							listThumbprintsDeleteTried.Add(x509Certificate2.Thumbprint);
							throw new CryptographicException("The test's key store refuses.");
						};
						string warning=ClientCertificateStore.RemoveImported(testKey.Thumbprint,StoreLocation.CurrentUser,clientCertificateTestStore.Name);
						Assert.AreEqual("The client certificate that this tool imported before was removed from the computer's certificate store "
							+"(CurrentUser\\"+clientCertificateTestStore.Name+"), but its private key could not be deleted ("+testKey.Subject+", "
							+testKey.Thumbprint+"): The test's key store refuses. The key is left on this computer in "+testKey.FilePath+": delete that "
							+"file as an administrator.",warning,label);
						Assert.IsFalse(warning.Contains("could not be removed"),label+": the certificate was removed.");
						CollectionAssert.AreEqual(new List<string> { testKey.Thumbprint },listThumbprintsDeleteTried,label+": the delete was tried once.");
						Assert.AreEqual(0,clientCertificateTestStore.GetThumbprints().Count,label+": the certificate is out of the store.");
						Assert.IsTrue(testKey.Exists(),label+": its key is left, as the warning says.");
						Assert.IsTrue(File.Exists(testKey.FilePath),label+": in the file the warning names.");
					}
					finally {
						ClientCertificateStore.ActionBeforeKeyDeleteForTests=null;
					}
				}//The store's clean-up deletes the key the tool left.
			}
		}

		///<summary>RemoveImported deletes the key only once no certificate left in the store uses the same key container (DeleteKeysNotInStore, as
		///DirectCertificateImport does for a replaced certificate): with a second certificate of the store made with the same key, the key stays
		///and that certificate still signs; when that one is removed too, the key goes. For each kind of key.</summary>
		[TestMethod]
		public void RemoveImported_KeepsAKeyThatAnotherCertificateOfTheStoreUses() {
			foreach(TestKeyKind testKeyKind in _arrayKinds) {
				string label=testKeyKind.ToString();
				using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore()) {
					TestKey testKey=clientCertificateTestStore.AddCertificate(testKeyKind);
					string thumbprintSameKey=clientCertificateTestStore.AddCertificateUsingKeyOf(testKey);
					using(X509Certificate2 x509Certificate2SameKey=clientCertificateTestStore.Find(thumbprintSameKey)) {
						Assert.AreEqual(testKey.FilePath,ClientCertificateStore.GetPrivateKeyFilePath(x509Certificate2SameKey),
							label+": the test's premise, the second certificate of the store uses the same key container.");
					}
					Assert.AreEqual("",ClientCertificateStore.RemoveImported(testKey.Thumbprint,StoreLocation.CurrentUser,clientCertificateTestStore.Name),
						label);
					CollectionAssert.AreEqual(new List<string> { thumbprintSameKey },clientCertificateTestStore.GetThumbprints(),label);
					Assert.IsTrue(testKey.Exists(),label+": the key another certificate of the store uses stays.");
					Assert.IsTrue(File.Exists(testKey.FilePath),label);
					Assert.IsTrue(clientCertificateTestStore.CanSign(thumbprintSameKey),label+": and that certificate still signs.");
					Assert.AreEqual("",ClientCertificateStore.RemoveImported(thumbprintSameKey,StoreLocation.CurrentUser,clientCertificateTestStore.Name),
						label);
					Assert.AreEqual(0,clientCertificateTestStore.GetThumbprints().Count,label);
					Assert.IsFalse(testKey.Exists(),label+": no certificate uses it any more: deleted.");
					Assert.IsFalse(File.Exists(testKey.FilePath),label);
				}
			}
		}
	}
}
