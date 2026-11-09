// The keys the connection settings tool's import stores (DCFU2, the follow-up round of DIRECT-CERT-FOLLOWUP): ImportPfx reads the .pfx with
// PersistKeySet, which stores the key of every certificate of the file that has one; it deletes again the ones it does not use (the file's
// other certificates', and all of them when the store refuses the client certificate), as DirectCertificateImport does, and never a key a
// certificate of the store uses. Throwaway keys of the current user only, in a store of the current user with a name of its own
// (ClientCertificateTestStore, through ImportPfx's internal overload); never LocalMachine\My, CurrentUser\My or a real certificate.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalDbSettings;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class ClientCertificateImportTests {
		private static readonly TestKeyKind[] _arrayKinds=new[] { TestKeyKind.CapiRsa,TestKeyKind.CngRsa,TestKeyKind.CngEcdsa };
		private const string STORE_REFUSES="The test's store refuses.";
		private const string KEY_STORE_REFUSES="The test's key store refuses.";
		private string _folder;

		[TestInitialize]
		public void SetUp() {
			_folder=TestHelpers.NewTempFolder();
		}

		[TestCleanup]
		public void CleanUp() {
			ClientCertificateStore.ActionAfterKeysStoredForTests=null;
			ClientCertificateStore.ActionBeforeStoreAddForTests=null;
			ClientCertificateStore.ActionBeforeKeyDeleteForTests=null;
			TestHelpers.DeleteFolder(_folder);
			ClientCertificateTestStore.AssertCleanedUp();
		}

		///<summary>A .pfx with the client certificate and the CA certificate that issued it, each with its key: the import stores both keys, adds
		///the client certificate only, and deletes the CA's key again (it no longer opens, its file is gone); the client certificate's key stays
		///and signs. No warning. For each kind of key.</summary>
		[TestMethod]
		public void ImportPfx_DeletesTheKeyOfTheFilesCaCertificate_AndKeepsTheClientCertificates() {
			foreach(TestKeyKind testKeyKind in _arrayKinds) {
				string label=testKeyKind.ToString();
				using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore()) {
					string pathPfx=clientCertificateTestStore.WritePfxWithCa(_folder,testKeyKind,out string thumbprintClient,out string thumbprintCa);
					Dictionary<string,TestKey> dictKeys=null;
					ClientCertificateStore.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						dictKeys=clientCertificateTestStore.TrackKeys(x509Certificate2Collection);
					};
					string thumbprint=ClientCertificateStore.ImportPfx(pathPfx,clientCertificateTestStore.Password,StoreLocation.CurrentUser,
						clientCertificateTestStore.Name,out string warning);
					ClientCertificateStore.ActionAfterKeysStoredForTests=null;
					Assert.AreEqual(thumbprintClient,thumbprint,label+": the client certificate is the one imported.");
					Assert.AreEqual("",warning,label);
					CollectionAssert.AreEqual(new List<string> { thumbprintClient },clientCertificateTestStore.GetThumbprints(),
						label+": only the client certificate is added.");
					Assert.IsNotNull(dictKeys,label);
					CollectionAssert.AreEquivalent(new List<string> { thumbprintClient,thumbprintCa },new List<string>(dictKeys.Keys),
						label+": the import stored the keys of both certificates of the file.");
					TestKey testKeyClient=dictKeys[thumbprintClient];
					TestKey testKeyCa=dictKeys[thumbprintCa];
					Assert.AreEqual(testKeyKind==TestKeyKind.CapiRsa,testKeyCa.IsCapi,label+": the key is of the kind the test made.");
					Assert.IsFalse(testKeyCa.IsMachine,label+": a key of the current user, never the machine's.");
					Assert.AreNotEqual(testKeyClient.FilePath,testKeyCa.FilePath,label+": two keys.");
					Assert.IsFalse(testKeyCa.Exists(),label+": the CA certificate's key is deleted again.");
					Assert.IsFalse(File.Exists(testKeyCa.FilePath),label+": its file is gone: "+testKeyCa.FilePath);
					Assert.IsTrue(testKeyClient.Exists(),label+": the client certificate's key stays.");
					Assert.IsTrue(File.Exists(testKeyClient.FilePath),label+": and its file: "+testKeyClient.FilePath);
					Assert.IsTrue(clientCertificateTestStore.CanSign(thumbprintClient),label+": and the client certificate signs with it.");
				}
			}
		}

		///<summary>When the import stops before the client certificate is in the store, every key it stored is deleted again, the client
		///certificate's too: (1) the store refuses it, (2) anything else stops it after the keys were stored, (3) the file's check refuses it
		///after the keys were stored (the exit code is kept); (4) a key that cannot be deleted again (a seam makes its delete fail) is named in
		///the error with its file; (5) a read with PersistKeySet that fails says that Windows may have left a key behind (the check's read,
		///which stores nothing, does not). For each kind of key.</summary>
		[TestMethod]
		public void ImportPfx_ARefusedImportDeletesEveryKeyItStored() {
			foreach(TestKeyKind testKeyKind in _arrayKinds) {
				string label=testKeyKind.ToString();
				using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore()) {
					string pathPfx=clientCertificateTestStore.WritePfxWithCa(_folder,testKeyKind,out string thumbprintClient,out string thumbprintCa);
					Dictionary<string,TestKey> dictKeys=null;
					Action<X509Certificate2Collection> actionTrackKeys=x509Certificate2Collection => {
						dictKeys=clientCertificateTestStore.TrackKeys(x509Certificate2Collection);
					};
					string storeText="CurrentUser\\"+clientCertificateTestStore.Name;
					//(1) The store refuses the client certificate.
					ClientCertificateStore.ActionAfterKeysStoredForTests=actionTrackKeys;
					ClientCertificateStore.ActionBeforeStoreAddForTests=x509Certificate2 => {
						throw new CryptographicException(STORE_REFUSES);
					};
					DbSettingsException dbSettingsException=AssertImportThrows(pathPfx,clientCertificateTestStore.Password,clientCertificateTestStore);
					Assert.AreEqual(DbSettingsRunner.EXIT_WRITE_FAILED,dbSettingsException.ExitCode,label);
					Assert.AreEqual("The client certificate could not be added to the computer's certificate store ("+storeText+"): "+STORE_REFUSES,
						dbSettingsException.Message,label+": every key was deleted again, so none is named.");
					AssertBothKeysDeleted(dictKeys,thumbprintClient,thumbprintCa,label+" (1)");
					Assert.AreEqual(0,clientCertificateTestStore.GetThumbprints().Count,label+": nothing is in the store.");
					//(2) Anything else stops the import after the keys were stored.
					dictKeys=null;
					ClientCertificateStore.ActionBeforeStoreAddForTests=null;
					ClientCertificateStore.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						actionTrackKeys(x509Certificate2Collection);
						throw new InvalidOperationException("The test stops the import");
					};
					dbSettingsException=AssertImportThrows(pathPfx,clientCertificateTestStore.Password,clientCertificateTestStore);
					Assert.AreEqual(DbSettingsRunner.EXIT_WRITE_FAILED,dbSettingsException.ExitCode,label);
					Assert.AreEqual("The client certificate could not be imported: The test stops the import.",dbSettingsException.Message,label);
					AssertBothKeysDeleted(dictKeys,thumbprintClient,thumbprintCa,label+" (2)");
					Assert.AreEqual(0,clientCertificateTestStore.GetThumbprints().Count,label);
					//(3) The file's check refuses it after the keys were stored: its message and exit code stay.
					dictKeys=null;
					ClientCertificateStore.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						actionTrackKeys(x509Certificate2Collection);
						throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The test's file check refuses it.");
					};
					dbSettingsException=AssertImportThrows(pathPfx,clientCertificateTestStore.Password,clientCertificateTestStore);
					Assert.AreEqual(DbSettingsRunner.EXIT_BAD_VALUE,dbSettingsException.ExitCode,label);
					Assert.AreEqual("The test's file check refuses it.",dbSettingsException.Message,label);
					AssertBothKeysDeleted(dictKeys,thumbprintClient,thumbprintCa,label+" (3)");
					//(4) The store refuses, and the CA certificate's key cannot be deleted again: the error names it and its file.
					dictKeys=null;
					ClientCertificateStore.ActionAfterKeysStoredForTests=actionTrackKeys;
					ClientCertificateStore.ActionBeforeStoreAddForTests=x509Certificate2 => {
						throw new CryptographicException(STORE_REFUSES);
					};
					ClientCertificateStore.ActionBeforeKeyDeleteForTests=x509Certificate2 => {
						if(x509Certificate2.Thumbprint==thumbprintCa) {
							throw new CryptographicException(KEY_STORE_REFUSES);
						}
					};
					dbSettingsException=AssertImportThrows(pathPfx,clientCertificateTestStore.Password,clientCertificateTestStore);
					ClientCertificateStore.ActionBeforeKeyDeleteForTests=null;
					Assert.AreEqual(DbSettingsRunner.EXIT_WRITE_FAILED,dbSettingsException.ExitCode,label);
					TestKey testKeyCa=dictKeys[thumbprintCa];
					Assert.AreEqual("The client certificate could not be added to the computer's certificate store ("+storeText+"): "+STORE_REFUSES
						+" A private key the tool stored could not be deleted again ("+testKeyCa.Subject+", "+thumbprintCa+"): "+KEY_STORE_REFUSES
						+" The key is left on this computer in "+testKeyCa.FilePath+": delete that file as an administrator.",dbSettingsException.Message,
						label);
					Assert.IsFalse(dictKeys[thumbprintClient].Exists(),label+": the client certificate's key is deleted.");
					Assert.IsTrue(testKeyCa.Exists(),label+": the CA certificate's key is left, as the error says.");
					Assert.IsTrue(File.Exists(testKeyCa.FilePath),label+": in the file the error names.");
					//(5) A read with PersistKeySet that fails (a wrong password here): Windows may have stored a key before it failed.
					dictKeys=null;
					ClientCertificateStore.ActionBeforeStoreAddForTests=null;
					dbSettingsException=AssertImportThrows(pathPfx,"wrong",clientCertificateTestStore);
					Assert.AreEqual(DbSettingsRunner.EXIT_BAD_VALUE,dbSettingsException.ExitCode,label);
					Assert.AreEqual("The client certificate file "+pathPfx+" cannot be opened: the password is wrong, or it is not a .pfx file. Nothing "
						+"was imported. Windows may have left a private key of the file behind on this computer, which the tool cannot find to delete.",
						dbSettingsException.Message,label);
					Assert.IsNull(dictKeys,label+": .NET gave no certificate, so no key is known.");
					DbSettingsException dbSettingsExceptionCheck=TestHelpers.AssertThrows<DbSettingsException>(
						() => ClientCertificateStore.LoadPfxForCheck(pathPfx,"wrong"));
					Assert.IsFalse(dbSettingsExceptionCheck.Message.Contains("Windows may have left"),label+": the check stores no key: "
						+dbSettingsExceptionCheck.Message);
					Assert.AreEqual(0,clientCertificateTestStore.GetThumbprints().Count,label);
				}
			}
		}

		///<summary>A key that a certificate of the store uses is never deleted, as DirectCertificateImport.DeleteKeysNotInStore keeps one: the test
		///gives a certificate of the store the key the import stored for the file's CA certificate (as a key that was on the computer before the
		///import would be). After a successful import the CA's key stays and that certificate still signs; when the store refuses the client
		///certificate, the client's key is deleted and the CA's stays. For each kind of key.</summary>
		[TestMethod]
		public void ImportPfx_NeverDeletesAKeyThatACertificateOfTheStoreUses() {
			foreach(TestKeyKind testKeyKind in _arrayKinds) {
				foreach(bool isRefused in new[] { false,true }) {
					string label=testKeyKind+(isRefused ? ", refused" : ", imported");
					using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore()) {
						string pathPfx=clientCertificateTestStore.WritePfxWithCa(_folder,testKeyKind,out string thumbprintClient,out string thumbprintCa);
						Dictionary<string,TestKey> dictKeys=null;
						string thumbprintSameKey=null;
						ClientCertificateStore.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
							dictKeys=clientCertificateTestStore.TrackKeys(x509Certificate2Collection);
							thumbprintSameKey=clientCertificateTestStore.AddCertificateUsingKeyOf(dictKeys[thumbprintCa]);
						};
						if(isRefused) {
							ClientCertificateStore.ActionBeforeStoreAddForTests=x509Certificate2 => {
								throw new CryptographicException(STORE_REFUSES);
							};
							DbSettingsException dbSettingsException=AssertImportThrows(pathPfx,clientCertificateTestStore.Password,clientCertificateTestStore);
							Assert.AreEqual("The client certificate could not be added to the computer's certificate store (CurrentUser\\"
								+clientCertificateTestStore.Name+"): "+STORE_REFUSES,dbSettingsException.Message,label+": no key is named as not deleted.");
							Assert.IsFalse(dictKeys[thumbprintClient].Exists(),label+": the client certificate's key is deleted.");
							CollectionAssert.AreEqual(new List<string> { thumbprintSameKey },clientCertificateTestStore.GetThumbprints(),label);
						}
						else {
							string thumbprint=ClientCertificateStore.ImportPfx(pathPfx,clientCertificateTestStore.Password,StoreLocation.CurrentUser,
								clientCertificateTestStore.Name,out string warning);
							Assert.AreEqual(thumbprintClient,thumbprint,label);
							Assert.AreEqual("",warning,label);
							Assert.IsTrue(dictKeys[thumbprintClient].Exists(),label+": the client certificate's key stays.");
							Assert.IsTrue(clientCertificateTestStore.CanSign(thumbprintClient),label);
							CollectionAssert.AreEquivalent(new List<string> { thumbprintClient,thumbprintSameKey },clientCertificateTestStore.GetThumbprints(),
								label);
						}
						ClientCertificateStore.ActionAfterKeysStoredForTests=null;
						ClientCertificateStore.ActionBeforeStoreAddForTests=null;
						TestKey testKeyCa=dictKeys[thumbprintCa];
						using(X509Certificate2 x509Certificate2SameKey=clientCertificateTestStore.Find(thumbprintSameKey)) {
							Assert.AreEqual(testKeyCa.FilePath,ClientCertificateStore.GetPrivateKeyFilePath(x509Certificate2SameKey),
								label+": the test's premise, the certificate of the store uses the key the import stored for the CA certificate.");
						}
						Assert.IsTrue(testKeyCa.Exists(),label+": the key a certificate of the store uses stays.");
						Assert.IsTrue(File.Exists(testKeyCa.FilePath),label+": and its file.");
						Assert.IsTrue(clientCertificateTestStore.CanSign(thumbprintSameKey),label+": and that certificate still signs.");
					}
				}
			}
		}

		///<summary>ImportPfx into the test store, which must throw DbSettingsException.</summary>
		private static DbSettingsException AssertImportThrows(string pathPfx,string password,ClientCertificateTestStore clientCertificateTestStore) {
			return TestHelpers.AssertThrows<DbSettingsException>(() => ClientCertificateStore.ImportPfx(pathPfx,password,StoreLocation.CurrentUser,
				clientCertificateTestStore.Name,out string warning));
		}

		private static void AssertBothKeysDeleted(Dictionary<string,TestKey> dictKeys,string thumbprintClient,string thumbprintCa,string label) {
			Assert.IsNotNull(dictKeys,label+": the keys were stored.");
			Assert.AreEqual(2,dictKeys.Count,label+": the import stored both keys.");
			foreach(string thumbprint in new[] { thumbprintClient,thumbprintCa }) {
				Assert.IsFalse(dictKeys[thumbprint].Exists(),label+": every key the import stored is deleted again ("+thumbprint+").");
				Assert.IsFalse(File.Exists(dictKeys[thumbprint].FilePath),label+": and its file: "+dictKeys[thumbprint].FilePath);
			}
		}
	}
}
