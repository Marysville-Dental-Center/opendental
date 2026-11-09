// The keys the connection settings tool's import stores (DCFU2, the follow-up round of DIRECT-CERT-FOLLOWUP): ImportPfx reads the .pfx with
// PersistKeySet, which stores the key of every certificate of the file that has one; it deletes again the ones it does not use (the file's
// other certificates', and all of them when the store refuses the client certificate, or already holds it with a usable key, which then
// stays as it is: the DCFU2 fix round), as DirectCertificateImport does, and never a key a certificate of the store uses. The last round
// (DCFU3): which certificate of the file is the client certificate, an entry whose key is in another key set, and the record of an import
// over an entry without a usable key. Throwaway keys of the current user only, in a store of the current user with a name of its own
// (ClientCertificateTestStore, through ImportPfx's internal overload, or StoreNameForTests); never LocalMachine\My, CurrentUser\My or a real
// certificate. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DataConnectionBase;
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
			ClientCertificateStore.ActionBeforePrivateKeyOpenForTests=null;
			ClientCertificateStore.ActionBeforeKeyProvInfoReadForTests=null;
			ClientCertificateStore.StoreNameForTests=null;
			TestHelpers.DeleteFolder(_folder);
			ClientCertificateTestStore.AssertCleanedUp();
		}

		///<summary>DCFU3, decision 1: a .pfx that lists its CA certificate, with the CA's key, before the client certificate (the read gives the
		///CA's first, the test's premise) gives the client certificate, both to the check and to the import: only it is added, the CA's key is
		///deleted again, the client certificate's key stays and signs, no warning. Before, the tool took the file's first certificate with a key,
		///the CA's, which passes the check when it has no Extended Key Usage. For each kind of key.</summary>
		[TestMethod]
		public void ImportPfx_TakesTheClientCertificate_NotTheCaCertificateTheFileListsFirst() {
			foreach(TestKeyKind testKeyKind in _arrayKinds) {
				string label=testKeyKind.ToString();
				using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore()) {
					string pathPfx=clientCertificateTestStore.WritePfxOf(_folder,testKeyKind,out List<string> listThumbprints,out List<string> listSubjects,
						TestPfxCertificate.Ca,TestPfxCertificate.Client);
					string thumbprintCa=listThumbprints[0];
					string thumbprintClient=listThumbprints[1];
					Assert.AreEqual(thumbprintCa,GetFirstWithKeyAsRead(pathPfx,clientCertificateTestStore.Password),
						label+": the test's premise, the file's first certificate with a key, as .NET reads it, is the CA's.");
					using(X509Certificate2 x509Certificate2Check=ClientCertificateStore.LoadPfxForCheck(pathPfx,clientCertificateTestStore.Password)) {
						Assert.AreEqual(thumbprintClient,x509Certificate2Check.Thumbprint,label+": the check shows the client certificate.");
					}
					Dictionary<string,TestKey> dictKeys=null;
					ClientCertificateStore.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						dictKeys=clientCertificateTestStore.TrackKeys(x509Certificate2Collection);
					};
					string thumbprint=ClientCertificateStore.ImportPfx(pathPfx,clientCertificateTestStore.Password,StoreLocation.CurrentUser,
						clientCertificateTestStore.Name,out string warning,out bool isStoreKeyImported);
					ClientCertificateStore.ActionAfterKeysStoredForTests=null;
					Assert.AreEqual(thumbprintClient,thumbprint,label+": the client certificate is the one imported.");
					Assert.AreEqual("",warning,label);
					Assert.IsTrue(isStoreKeyImported,label+": added, with the key the import stored.");
					CollectionAssert.AreEqual(new List<string> { thumbprintClient },clientCertificateTestStore.GetThumbprints(),
						label+": only the client certificate is added.");
					Assert.IsNotNull(dictKeys,label);
					Assert.AreEqual(2,dictKeys.Count,label+": the import stored both keys.");
					Assert.IsFalse(dictKeys[thumbprintCa].Exists(),label+": the CA certificate's key is deleted again.");
					Assert.IsFalse(File.Exists(dictKeys[thumbprintCa].FilePath),label+": its file is gone.");
					Assert.IsTrue(dictKeys[thumbprintClient].Exists(),label+": the client certificate keeps its key.");
					Assert.IsTrue(clientCertificateTestStore.CanSign(thumbprintClient),label+": and signs with it.");
				}
			}
		}

		///<summary>DCFU3, decision 1: a .pfx without exactly one client certificate is refused, by the check and by the import, which then puts
		///nothing into the store and deletes every key it stored: (1) two client certificates, each with its key (both named, with their
		///thumbprints); (2) only a CA certificate with its key (named as a certificate authority). The message says how to make a .pfx with one
		///client certificate. For each kind of key.</summary>
		[TestMethod]
		public void ImportPfx_RefusesAFileWithoutExactlyOneClientCertificate() {
			foreach(TestKeyKind testKeyKind in _arrayKinds) {
				foreach(bool isTwoClients in new[] { true,false }) {
					string label=testKeyKind+(isTwoClients ? ", two client certificates" : ", a CA certificate only");
					using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore()) {
						TestPfxCertificate[] arrayCertificates=isTwoClients ? new[] { TestPfxCertificate.Client,TestPfxCertificate.Client }
							: new[] { TestPfxCertificate.Ca };
						string pathPfx=clientCertificateTestStore.WritePfxOf(_folder,testKeyKind,out List<string> listThumbprints,
							out List<string> listSubjects,arrayCertificates);
						DbSettingsException dbSettingsExceptionCheck=TestHelpers.AssertThrows<DbSettingsException>(
							() => ClientCertificateStore.LoadPfxForCheck(pathPfx,clientCertificateTestStore.Password));
						Assert.AreEqual(DbSettingsRunner.EXIT_BAD_VALUE,dbSettingsExceptionCheck.ExitCode,label);
						string message=dbSettingsExceptionCheck.Message;
						StringAssert.StartsWith(message,"The client certificate file "+pathPfx+(isTwoClients
							? " holds more than one certificate that can be the client certificate (with a private key, not a certificate authority, and "
								+"for client authentication): "
							: " holds no client certificate: each of its certificates with a private key is a certificate authority (CA=true in its "
								+"Basic Constraints). It holds: "),label);
						for(int i=0;i<listThumbprints.Count;i++) {
							if(isTwoClients) {
								StringAssert.Contains(message,listSubjects[i]+" ("+listThumbprints[i]+")",label+": names the client certificates it found.");
							}
							StringAssert.Contains(message,listSubjects[i]+" ("+listThumbprints[i]+", with a private key, "
								+(isTwoClients ? "for client authentication)" : "a certificate authority)"),label);
						}
						StringAssert.EndsWith(message,"Make a .pfx with only the client certificate and its private key, and the office CA certificate "
							+"without its key, as docs/db-connection-security.md section 3 does: openssl pkcs12 -export -in client-cert.pem -inkey "
							+"client-key.pem -certfile office-ca.pem -out client.pfx.",label);
						Dictionary<string,TestKey> dictKeys=null;
						ClientCertificateStore.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
							dictKeys=clientCertificateTestStore.TrackKeys(x509Certificate2Collection);
						};
						bool isAdded=false;
						ClientCertificateStore.ActionBeforeStoreAddForTests=x509Certificate2 => {
							isAdded=true;
						};
						DbSettingsException dbSettingsException=AssertImportThrows(pathPfx,clientCertificateTestStore.Password,clientCertificateTestStore);
						ClientCertificateStore.ActionAfterKeysStoredForTests=null;
						ClientCertificateStore.ActionBeforeStoreAddForTests=null;
						Assert.AreEqual(DbSettingsRunner.EXIT_BAD_VALUE,dbSettingsException.ExitCode,label);
						Assert.AreEqual(message,dbSettingsException.Message,label+": the import refuses it the same way, and names no key left.");
						Assert.IsFalse(isAdded,label+": nothing is added.");
						Assert.AreEqual(0,clientCertificateTestStore.GetThumbprints().Count,label+": the store is empty.");
						Assert.IsNotNull(dictKeys,label);
						CollectionAssert.AreEquivalent(listThumbprints,new List<string>(dictKeys.Keys),label+": the import stored every key of the file.");
						foreach(TestKey testKey in dictKeys.Values) {
							Assert.IsFalse(testKey.Exists(),label+": every key the import stored is deleted again ("+testKey.Thumbprint+").");
							Assert.IsFalse(File.Exists(testKey.FilePath),label+": and its file: "+testKey.FilePath);
						}
					}
				}
			}
		}

		///<summary>DCFU3, decision 2: a store entry of the client certificate whose key provider information names a key in another key set than
		///the import's (the test makes the test store's entry name the machine key set; the test store's import uses the user's) is not usable,
		///whether that key would open or not, and its key is never opened: the import adds the certificate with its own key, which the entry then
		///uses, and the warning says that the entry's earlier key was left untouched, names its provider, container and key set, and the command
		///that deletes it. The user key the entry named before is not touched. (2) An entry whose key provider information cannot be read (a seam)
		///is not usable either: the import adds the certificate, and says the earlier key could not be identified. For each kind of key.
		///</summary>
		[TestMethod]
		public void ImportPfx_OfACertificateWhoseEntryHasItsKeyInAnotherKeySet_GivesItTheImportsKey_AndLeavesTheEarlierOne() {
			foreach(TestKeyKind testKeyKind in _arrayKinds) {
				foreach(bool isUnreadable in new[] { false,true }) {
					string label=testKeyKind+(isUnreadable ? ", unreadable" : ", another key set");
					using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore()) {
						string pathPfx=clientCertificateTestStore.WritePfx(_folder,testKeyKind,out string thumbprintClient);
						TestKey testKeyFirst=clientCertificateTestStore.AddCertificate(pathPfx);
						string storeText="CurrentUser\\"+clientCertificateTestStore.Name;
						string warningExpected;
						if(isUnreadable) {
							ClientCertificateStore.ActionBeforeKeyProvInfoReadForTests=x509Certificate2 => {
								if(string.Equals(TestKeyProvInfo.Read(x509Certificate2).ContainerName,testKeyFirst.Name,StringComparison.OrdinalIgnoreCase)) {
									throw new CryptographicException("The test's key provider information cannot be read.");
								}
							};
							warningExpected="The client certificate was already in the computer's certificate store ("+storeText+"), but the private key its "
								+"entry names could not be identified: The test's key provider information cannot be read. The import gave the entry the "
								+"private key it stored, and left the entry's earlier key, if there is one, untouched.";
						}
						else {
							TestKeyProvInfo testKeyProvInfoEntry=clientCertificateTestStore.NameMachineKeySetForEntry(thumbprintClient);
							Assert.IsTrue(string.Equals(testKeyFirst.Name,testKeyProvInfoEntry.ContainerName,StringComparison.OrdinalIgnoreCase),
								label+": the test's premise, the entry names the same container, in the other key set.");
							warningExpected="The client certificate was already in the computer's certificate store ("+storeText+"), with its private key in "
								+"the computer's key set, not in the current user's key set, which the import uses. The import gave the entry the private key "
								+"it stored, and left the entry's earlier key untouched: provider "+testKeyProvInfoEntry.ProviderName+", key container "
								+testKeyProvInfoEntry.ContainerName+", in the computer's key set. An administrator can delete it, once no other certificate "
								+"uses it: certutil -csp \""+testKeyProvInfoEntry.ProviderName+"\" -delkey \""+testKeyProvInfoEntry.ContainerName+"\".";
						}
						Dictionary<string,TestKey> dictKeys=null;
						ClientCertificateStore.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
							dictKeys=clientCertificateTestStore.TrackKeys(x509Certificate2Collection);
						};
						bool isAdded=false;
						ClientCertificateStore.ActionBeforeStoreAddForTests=x509Certificate2 => {
							isAdded=true;
						};
						List<TestKeyProvInfo> listOpened=new List<TestKeyProvInfo>();
						ClientCertificateStore.ActionBeforePrivateKeyOpenForTests=x509Certificate2 => {
							listOpened.Add(TestKeyProvInfo.Read(x509Certificate2));
						};
						string thumbprint=ClientCertificateStore.ImportPfx(pathPfx,clientCertificateTestStore.Password,StoreLocation.CurrentUser,
							clientCertificateTestStore.Name,out string warning,out bool isStoreKeyImported);
						ClientCertificateStore.ActionAfterKeysStoredForTests=null;
						ClientCertificateStore.ActionBeforeStoreAddForTests=null;
						ClientCertificateStore.ActionBeforePrivateKeyOpenForTests=null;
						ClientCertificateStore.ActionBeforeKeyProvInfoReadForTests=null;
						Assert.AreEqual(thumbprintClient,thumbprint,label);
						Assert.AreEqual(warningExpected,warning,label);
						Assert.IsTrue(isAdded,label+": the certificate is added again.");
						Assert.IsTrue(isStoreKeyImported,label+": the entry has the key the import stored.");
						Assert.IsFalse(listOpened.Any(x => x.IsMachine),label+": no key of the machine key set is opened.");
						Assert.IsFalse(listOpened.Any(x => string.Equals(x.ContainerName,testKeyFirst.Name,StringComparison.OrdinalIgnoreCase)),
							label+": the entry's earlier key is never opened.");
						Assert.IsNotNull(dictKeys,label);
						AssertEntryUsesKey(clientCertificateTestStore,thumbprintClient,dictKeys[thumbprintClient],label);
						Assert.IsTrue(testKeyFirst.Exists(),label+": the entry's earlier key is left untouched (the store's clean-up deletes it).");
						Assert.IsTrue(File.Exists(testKeyFirst.FilePath),label+": and its file.");
					}
				}
			}
		}

		///<summary>DCFU3, decision 6, through DbSettingsRunner.Apply (with the test store in place of LocalMachine\My, StoreNameForTests): a store
		///entry of the client certificate that the tool did not import, whose key cannot be used, takes the key of the tool's import, so the tool
		///records the certificate as its own; a later save with the password removes it, with the key the tool made (it no longer opens, its file
		///is gone), and deletes the record. (2) An entry with a usable key that the tool did not import is left as it is and not recorded; a later
		///save with the password leaves it, with its key. For each kind of key.</summary>
		[TestMethod]
		public void Apply_AnImportOverAnEntryWithoutAUsableKey_IsRecorded_AndALaterSaveRemovesItWithTheKeyTheToolMade() {
			foreach(TestKeyKind testKeyKind in _arrayKinds) {
				foreach(bool isEntryKeyUsable in new[] { false,true }) {
					string label=testKeyKind+(isEntryKeyUsable ? ", an entry with a usable key" : ", an entry without a usable key");
					string folder=Path.Combine(_folder,testKeyKind+(isEntryKeyUsable ? "-usable" : "-unusable"));
					Directory.CreateDirectory(folder);
					string config=TestHelpers.WriteConfig(folder,"ComputerName","localhost","Database","opendental","User","root","Password","");
					string caCopy=DbConnectionConfig.GetOfficeCaCopyPath(Path.Combine(folder,"program"));
					string pathRecord=ImportedCertificateRecord.GetPath(caCopy);
					using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore()) {
						string pathPfx=clientCertificateTestStore.WritePfx(folder,testKeyKind,out string thumbprintClient);
						TestKey testKeyFirst=clientCertificateTestStore.AddCertificate(pathPfx);//Put there another way, not by the tool.
						if(!isEntryKeyUsable) {
							DeleteEntryKey(clientCertificateTestStore,thumbprintClient,testKeyFirst,label);
						}
						Dictionary<string,TestKey> dictKeys=null;
						ClientCertificateStore.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
							dictKeys=clientCertificateTestStore.TrackKeys(x509Certificate2Collection);
						};
						ClientCertificateStore.StoreNameForTests=clientCertificateTestStore.Name;
						DbSettingsResult dbSettingsResult=DbSettingsRunner.Apply(new DbSettingsRequest() { Auth=DbSettingsAuth.Certificate,PfxFile=pathPfx,
							PfxPassword=clientCertificateTestStore.Password },config,caCopy);
						ClientCertificateStore.ActionAfterKeysStoredForTests=null;
						Assert.AreEqual(DbSettingsRunner.EXIT_OK,dbSettingsResult.ExitCode,label+": "+dbSettingsResult.Message);
						Assert.AreEqual("Saved the connection settings for the database opendental on localhost (client certificate).",
							dbSettingsResult.Message,label+": no warning.");
						Assert.AreEqual(thumbprintClient,DbConnectionConfig.ReadFile(config).ClientCertThumbprint,label);
						Assert.IsNotNull(dictKeys,label);
						TestKey testKeyImport=dictKeys[thumbprintClient];
						if(isEntryKeyUsable) {
							Assert.AreEqual("",ImportedCertificateRecord.Read(pathRecord),label+": the entry keeps its own key, which the tool did not make: "
								+"not recorded.");
							AssertEntryKeepsItsKey(clientCertificateTestStore,thumbprintClient,testKeyFirst,label);
							Assert.IsFalse(testKeyImport.Exists(),label+": the key the import stored is deleted again.");
						}
						else {
							Assert.AreEqual(thumbprintClient,ImportedCertificateRecord.Read(pathRecord),label+": the entry took the key the tool made: "
								+"recorded as the tool's own.");
							AssertEntryUsesKey(clientCertificateTestStore,thumbprintClient,testKeyImport,label);
						}
						//A later save stops using the certificate.
						dbSettingsResult=DbSettingsRunner.Apply(new DbSettingsRequest() { Auth=DbSettingsAuth.Password,DbPassword="" },config,caCopy);
						ClientCertificateStore.StoreNameForTests=null;
						Assert.AreEqual(DbSettingsRunner.EXIT_OK,dbSettingsResult.ExitCode,label+": "+dbSettingsResult.Message);
						Assert.AreEqual("Saved the connection settings for the database opendental on localhost (password).",dbSettingsResult.Message,
							label+": no warning.");
						Assert.AreEqual("",ImportedCertificateRecord.Read(pathRecord),label+": no record is left.");
						if(isEntryKeyUsable) {
							CollectionAssert.AreEqual(new List<string> { thumbprintClient },clientCertificateTestStore.GetThumbprints(),
								label+": the certificate the tool did not import stays.");
							Assert.IsTrue(testKeyFirst.Exists(),label+": with its key.");
							Assert.IsTrue(clientCertificateTestStore.CanSign(thumbprintClient),label);
						}
						else {
							Assert.AreEqual(0,clientCertificateTestStore.GetThumbprints().Count,label+": the recorded certificate is removed.");
							Assert.IsFalse(testKeyImport.Exists(),label+": with the key the tool made.");
							Assert.IsFalse(File.Exists(testKeyImport.FilePath),label+": its file is gone: "+testKeyImport.FilePath);
						}
					}
				}
			}
		}

		///<summary>The thumbprint of the file's first certificate with a private key, in the order .NET reads the file (EphemeralKeySet: nothing
		///is stored).</summary>
		private static string GetFirstWithKeyAsRead(string pathPfx,string password) {
			X509Certificate2Collection x509Certificate2Collection=new X509Certificate2Collection();
			x509Certificate2Collection.Import(pathPfx,password,X509KeyStorageFlags.EphemeralKeySet);
			try {
				foreach(X509Certificate2 x509Certificate2 in x509Certificate2Collection) {
					if(x509Certificate2.HasPrivateKey) {
						return x509Certificate2.Thumbprint;
					}
				}
				return null;
			}
			finally {
				foreach(X509Certificate2 x509Certificate2 in x509Certificate2Collection) {
					x509Certificate2.Reset();
				}
			}
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

		///<summary>A .pfx whose certificate the store already holds with a usable key, as when setup runs again with the same file: the import
		///does not add it again (DirectCertificateImport leaves such a certificate "as it is"), since the store's add would give the entry the
		///key the import just stored and leave the key it had with no certificate naming it. The test adds the certificate from the .pfx it keeps
		///on disk (AddCertificate), then imports the same file: the first key still exists and is still the entry's key, the key the import
		///stored is deleted again (it no longer opens, its file is gone), the certificate signs, and there is no warning. (2) The same import
		///when the key it stored cannot be deleted again (a seam makes its delete fail): the warning names that key and its file, which is still
		///there, and the entry still keeps its own key. For each kind of key.</summary>
		[TestMethod]
		public void ImportPfx_OfACertificateTheStoreHasWithAUsableKey_KeepsItsEntryAndKey_AndDeletesTheKeyItStored() {
			foreach(TestKeyKind testKeyKind in _arrayKinds) {
				string label=testKeyKind.ToString();
				using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore()) {
					string pathPfx=clientCertificateTestStore.WritePfx(_folder,testKeyKind,out string thumbprintClient);
					TestKey testKeyFirst=clientCertificateTestStore.AddCertificate(pathPfx);
					Assert.AreEqual(thumbprintClient,testKeyFirst.Thumbprint,label+": the test's premise, the store has the file's certificate.");
					Assert.IsTrue(clientCertificateTestStore.CanSign(thumbprintClient),label+": the test's premise, with a usable key.");
					Dictionary<string,TestKey> dictKeys=null;
					ClientCertificateStore.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						dictKeys=clientCertificateTestStore.TrackKeys(x509Certificate2Collection);
					};
					bool isAdded=false;
					ClientCertificateStore.ActionBeforeStoreAddForTests=x509Certificate2 => {
						isAdded=true;
					};
					string thumbprint=ClientCertificateStore.ImportPfx(pathPfx,clientCertificateTestStore.Password,StoreLocation.CurrentUser,
						clientCertificateTestStore.Name,out string warning);
					Assert.AreEqual(thumbprintClient,thumbprint,label+": the client certificate is the one the settings use.");
					Assert.AreEqual("",warning,label);
					Assert.IsNotNull(dictKeys,label);
					CollectionAssert.AreEqual(new List<string> { thumbprintClient },new List<string>(dictKeys.Keys),label+": the import stored one key.");
					TestKey testKeyImport=dictKeys[thumbprintClient];
					Assert.AreEqual(testKeyKind==TestKeyKind.CapiRsa,testKeyImport.IsCapi,label+": the key is of the kind the test made.");
					Assert.IsFalse(testKeyImport.IsMachine,label+": a key of the current user, never the machine's.");
					Assert.AreNotEqual(testKeyFirst.FilePath,testKeyImport.FilePath,label+": the import stored a key of its own.");
					AssertEntryKeepsItsKey(clientCertificateTestStore,thumbprintClient,testKeyFirst,label);
					Assert.IsFalse(testKeyImport.Exists(),label+": the key the import stored is deleted again.");
					Assert.IsFalse(File.Exists(testKeyImport.FilePath),label+": its file is gone: "+testKeyImport.FilePath);
					Assert.IsFalse(isAdded,label+": the certificate the store holds is not added again.");
					//(2) The key the import stored cannot be deleted again: the warning names it, and the entry is still as it was.
					dictKeys=null;
					ClientCertificateStore.ActionBeforeKeyDeleteForTests=x509Certificate2 => {
						throw new CryptographicException(KEY_STORE_REFUSES);
					};
					thumbprint=ClientCertificateStore.ImportPfx(pathPfx,clientCertificateTestStore.Password,StoreLocation.CurrentUser,
						clientCertificateTestStore.Name,out warning);
					ClientCertificateStore.ActionBeforeKeyDeleteForTests=null;
					ClientCertificateStore.ActionAfterKeysStoredForTests=null;
					ClientCertificateStore.ActionBeforeStoreAddForTests=null;
					Assert.AreEqual(thumbprintClient,thumbprint,label+" (2)");
					Assert.IsFalse(isAdded,label+" (2): not added again.");
					Assert.IsNotNull(dictKeys,label+" (2)");
					testKeyImport=dictKeys[thumbprintClient];
					Assert.AreEqual("The client certificate was already in the computer's certificate store with a usable private key and stays as it "
						+"is, but a private key of the file, which the tool stored and does not use, could not be deleted ("+testKeyImport.Subject+", "
						+thumbprintClient+"): "+KEY_STORE_REFUSES+" The key is left on this computer in "+testKeyImport.FilePath+": delete that file as "
						+"an administrator.",warning,label+" (2)");
					Assert.IsTrue(testKeyImport.Exists(),label+" (2): the key the import stored is left, as the warning says (the store's clean-up "
						+"deletes it).");
					Assert.IsTrue(File.Exists(testKeyImport.FilePath),label+" (2): in the file the warning names.");
					AssertEntryKeepsItsKey(clientCertificateTestStore,thumbprintClient,testKeyFirst,label+" (2)");
				}
			}
		}

		///<summary>A .pfx whose certificate the store holds without a usable key (the test deletes the entry's key, as a key that was deleted, or
		///one that does not open, would be): (1) the import adds the certificate, as DirectCertificateImport imports such a certificate again,
		///and the entry takes the key the import stored, which stays and signs; no warning. The test adds the certificate from one file and
		///imports another file of the same certificate and key (WritePfxTwice), so that the import's key gets a container of its own for every
		///kind of key. (2) The entry's key deleted again and the same file imported again: whichever way Windows names the key the import stores
		///(a CAPI key lands in the container the file records, the very one the entry names, which makes the entry usable as it is, so it is
		///not added; a CNG key gets a new name, and the entry takes it), the entry uses that key, which is never deleted as unused, and signs.
		///For each kind of key.</summary>
		[TestMethod]
		public void ImportPfx_OfACertificateTheStoreHasWithoutAUsableKey_GivesItTheKeyTheImportStored() {
			foreach(TestKeyKind testKeyKind in _arrayKinds) {
				string label=testKeyKind.ToString();
				using(ClientCertificateTestStore clientCertificateTestStore=new ClientCertificateTestStore()) {
					string pathPfxEntry=clientCertificateTestStore.WritePfxTwice(_folder,testKeyKind,out string thumbprintClient,out string pathPfx);
					TestKey testKeyFirst=clientCertificateTestStore.AddCertificate(pathPfxEntry);
					Assert.AreEqual(thumbprintClient,testKeyFirst.Thumbprint,label+": the test's premise, the store has the file's certificate.");
					DeleteEntryKey(clientCertificateTestStore,thumbprintClient,testKeyFirst,label);
					Dictionary<string,TestKey> dictKeys=null;
					ClientCertificateStore.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						dictKeys=clientCertificateTestStore.TrackKeys(x509Certificate2Collection);
					};
					bool isAdded=false;
					ClientCertificateStore.ActionBeforeStoreAddForTests=x509Certificate2 => {
						isAdded=true;
					};
					string thumbprint=ClientCertificateStore.ImportPfx(pathPfx,clientCertificateTestStore.Password,StoreLocation.CurrentUser,
						clientCertificateTestStore.Name,out string warning);
					Assert.AreEqual(thumbprintClient,thumbprint,label);
					Assert.AreEqual("",warning,label);
					Assert.IsNotNull(dictKeys,label);
					TestKey testKeyImport=dictKeys[thumbprintClient];
					Assert.AreNotEqual(testKeyFirst.FilePath,testKeyImport.FilePath,label+": the test's premise, the import's key has a container of "
						+"its own.");
					Assert.IsTrue(isAdded,label+": the certificate is added again.");
					AssertEntryUsesKey(clientCertificateTestStore,thumbprintClient,testKeyImport,label);
					//(2) The entry's key deleted again, and the same file imported again.
					DeleteEntryKey(clientCertificateTestStore,thumbprintClient,testKeyImport,label+" (2)");
					dictKeys=null;
					isAdded=false;
					thumbprint=ClientCertificateStore.ImportPfx(pathPfx,clientCertificateTestStore.Password,StoreLocation.CurrentUser,
						clientCertificateTestStore.Name,out warning);
					ClientCertificateStore.ActionAfterKeysStoredForTests=null;
					ClientCertificateStore.ActionBeforeStoreAddForTests=null;
					Assert.AreEqual(thumbprintClient,thumbprint,label+" (2)");
					Assert.AreEqual("",warning,label+" (2)");
					Assert.IsNotNull(dictKeys,label+" (2)");
					TestKey testKeyImportAgain=dictKeys[thumbprintClient];
					bool isSameContainer=testKeyImportAgain.FilePath==testKeyImport.FilePath;
					Assert.AreEqual(!isSameContainer,isAdded,label+" (2): added only when the import's key is not in the container the entry names "
						+"(same container: "+isSameContainer+").");
					AssertEntryUsesKey(clientCertificateTestStore,thumbprintClient,testKeyImportAgain,label+" (2)");
				}
			}
		}

		///<summary>Deletes the key of the store's entry for the certificate (the test's premise of an entry without a usable key): the key is
		///gone, the entry still names it (HasPrivateKey), and ClientCertificateStore.IsKeyUsable says it cannot be used.</summary>
		private static void DeleteEntryKey(ClientCertificateTestStore clientCertificateTestStore,string thumbprintClient,TestKey testKey,string label) {
			using(X509Certificate2 x509Certificate2Entry=clientCertificateTestStore.Find(thumbprintClient)) {
				Assert.AreEqual(testKey.FilePath,ClientCertificateStore.GetPrivateKeyFilePath(x509Certificate2Entry),label+": the test's premise, "
					+"the entry names that key.");
			}
			testKey.DeleteIfExists();
			Assert.IsFalse(testKey.Exists(),label+": the test's premise, the entry's key is gone.");
			using(X509Certificate2 x509Certificate2Entry=clientCertificateTestStore.Find(thumbprintClient)) {
				Assert.IsTrue(x509Certificate2Entry.HasPrivateKey,label+": the test's premise, the entry still says it has a key.");
				Assert.IsFalse(ClientCertificateStore.IsKeyUsable(x509Certificate2Entry),label+": the test's premise, which cannot be used.");
			}
		}

		///<summary>The store's entry for the certificate is the only one and uses the key the import stored, which exists, with its file, and the
		///certificate signs with it.</summary>
		private static void AssertEntryUsesKey(ClientCertificateTestStore clientCertificateTestStore,string thumbprintClient,TestKey testKeyImport,
			string label)
		{
			CollectionAssert.AreEqual(new List<string> { thumbprintClient },clientCertificateTestStore.GetThumbprints(),label+": one entry.");
			Assert.IsTrue(testKeyImport.Exists(),label+": the key the import stored stays.");
			Assert.IsTrue(File.Exists(testKeyImport.FilePath),label+": and its file: "+testKeyImport.FilePath);
			using(X509Certificate2 x509Certificate2Entry=clientCertificateTestStore.Find(thumbprintClient)) {
				Assert.AreEqual(testKeyImport.FilePath,ClientCertificateStore.GetPrivateKeyFilePath(x509Certificate2Entry),
					label+": the entry uses the key the import stored.");
			}
			Assert.IsTrue(clientCertificateTestStore.CanSign(thumbprintClient),label+": and signs with it.");
		}

		///<summary>The store's entry for the certificate is the only one and still uses the key the test stored first (by the key's file, as the
		///code under test and the test each name it), which still exists, and the certificate signs with it.</summary>
		private static void AssertEntryKeepsItsKey(ClientCertificateTestStore clientCertificateTestStore,string thumbprintClient,TestKey testKeyFirst,
			string label)
		{
			CollectionAssert.AreEqual(new List<string> { thumbprintClient },clientCertificateTestStore.GetThumbprints(),label+": one entry.");
			Assert.IsTrue(testKeyFirst.Exists(),label+": the key the entry had stays.");
			Assert.IsTrue(File.Exists(testKeyFirst.FilePath),label+": and its file: "+testKeyFirst.FilePath);
			using(X509Certificate2 x509Certificate2Entry=clientCertificateTestStore.Find(thumbprintClient)) {
				Assert.AreEqual(testKeyFirst.FilePath,ClientCertificateStore.GetPrivateKeyFilePath(x509Certificate2Entry),
					label+": the entry still uses the key it had.");
			}
			Assert.IsTrue(clientCertificateTestStore.CanSign(thumbprintClient),label+": the certificate signs with it.");
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
