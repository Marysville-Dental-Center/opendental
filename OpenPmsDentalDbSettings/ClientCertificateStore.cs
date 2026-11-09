// The client certificate in the computer's certificate store (LocalMachine\My), its private key not exportable.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace OpenPmsDentalDbSettings {
	///<summary>The client certificate (owner decision Q2): one shared certificate on every workstation, or one per computer later, in
	///LocalMachine\My, read by MySqlConnector by thumbprint. A .pfx is imported with MachineKeySet and PersistKeySet and never Exportable, and
	///BUILTIN\Users get read access to its private key, because the program runs as the signed-in user (docs/specs/db-connection-security.md
	///section 4.3). So the tool never leaves a key the users may read behind, and never touches a key it did not create (owner decision of
	///2026-11-02).</summary>
	public static class ClientCertificateStore {
		///<summary>Extended Key Usage OID of TLS client authentication.</summary>
		private const string OID_CLIENT_AUTHENTICATION="1.3.6.1.5.5.7.3.2";
		///<summary>The property of a certificate context that names its private key: CRYPT_KEY_PROV_INFO.</summary>
		private const uint CERT_KEY_PROV_INFO_PROP_ID=2;
		///<summary>What CertGetCertificateContextProperty gives for a certificate without the property.</summary>
		private const int CRYPT_E_NOT_FOUND=unchecked((int)0x80092004);
		///<summary>The machine key set in CRYPT_KEY_PROV_INFO.dwFlags: CRYPT_MACHINE_KEYSET for a CAPI key, NCRYPT_MACHINE_KEY_FLAG for a CNG
		///key, the same bit.</summary>
		private const uint KEY_PROV_INFO_MACHINE_KEYSET=0x20;
		///<summary>How to make a .pfx with one client certificate, for the messages that refuse a file: docs/db-connection-security.md section 3.
		///</summary>
		private const string PFX_HOW_TO="Make a .pfx with only the client certificate and its private key, and the office CA certificate without "
			+"its key, as docs/db-connection-security.md section 3 does: openssl pkcs12 -export -in client-cert.pem -inkey client-key.pem -certfile "
			+"office-ca.pem -out client.pfx";
		///<summary>The tests only: called with the file's certificates right after ImportPfx stored their keys (PersistKeySet), before anything
		///else of the import; throwing there is the import failing after the keys were stored. Null in the tool.</summary>
		internal static Action<X509Certificate2Collection> ActionAfterKeysStoredForTests;
		///<summary>The tests only: called with the client certificate right before ImportPfx adds it to the store (not when the store already
		///holds it with a usable key, which stays as it is); a CryptographicException there is a store that refuses it. Null in the tool.</summary>
		internal static Action<X509Certificate2> ActionBeforeStoreAddForTests;
		///<summary>The tests only: called with each certificate right before its private key is deleted (TryDeletePrivateKey); throwing there is a
		///key that cannot be deleted. Null in the tool.</summary>
		internal static Action<X509Certificate2> ActionBeforeKeyDeleteForTests;
		///<summary>The tests only: called with the certificate each time the tool is about to open its private key (X509Certificate2.PrivateKey,
		///GetRSAPrivateKey, GetECDsaPrivateKey), so that a test sees which keys are opened: never one of a certificate of the store only to
		///compare key containers (ReadKeyProvInfo). Null in the tool.</summary>
		internal static Action<X509Certificate2> ActionBeforePrivateKeyOpenForTests;
		///<summary>The tests only: called with the certificate right before its key provider information is read (ReadKeyProvInfo); throwing
		///there is a property that cannot be read. Null in the tool.</summary>
		internal static Action<X509Certificate2> ActionBeforeKeyProvInfoReadForTests;
		///<summary>The tests only: called instead of the CAPI delete (PersistKeyInCsp=false and Clear) of DeleteCapiKey; doing nothing there is a
		///CRYPT_DELETEKEYSET that fails without a word, which the check after the delete must report. Null in the tool.</summary>
		internal static Action<RSACryptoServiceProvider> ActionInsteadOfCapiKeyDeleteForTests;
		///<summary>The tests only: the name of a store of the current user (the tests' ClientCertificateTestStore) that the tool's public methods
		///(ImportPfx, RemoveImported, FindInStore) use instead of LocalMachine\My, so that DbSettingsRunner.Apply can be tested without the
		///computer's store; the import then uses the user's key set and gives no read access, as the internal overloads do for such a store.
		///Null in the tool.</summary>
		internal static string StoreNameForTests;

		[DllImport("crypt32.dll",SetLastError=true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool CertGetCertificateContextProperty(IntPtr pCertContext,uint dwPropId,IntPtr pvData,ref uint pcbData);

		///<summary>CRYPT_KEY_PROV_INFO, as CertGetCertificateContextProperty gives it for CERT_KEY_PROV_INFO_PROP_ID.</summary>
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

		///<summary>Where a certificate's private key is, as its key provider information (CERT_KEY_PROV_INFO_PROP_ID) names it, read without
		///opening the key (ReadKeyProvInfo): the key container (a CNG key's name), its provider (type 0 for a CNG key storage provider) and its
		///key set, the machine's or the user's. A certificate without the property names no key (IsNamed false); one whose property cannot be
		///read uses an unknown key (Problem says why).</summary>
		internal sealed class KeyProvInfo {
			public bool IsNamed;
			public string ContainerName="";
			public string ProviderName="";
			public int ProviderType;
			public bool IsMachine;
			///<summary>Why the property could not be read, or null.</summary>
			public string Problem;

			public bool IsUnknown {
				get {
					return Problem!=null;
				}
			}

			///<summary>The key container in its key set, compared without case (CAPI and CNG names are). Not the provider: Microsoft's software
			///CAPI providers share their containers, and a certificate may name one through another; so two certificates that name one container
			///name in one key set count as one key, which keeps a key rather than delete one another certificate may use.</summary>
			public string Identity {
				get {
					return (IsMachine ? "machine" : "user")+"|"+ContainerName;
				}
			}
		}

		///<summary>Opens the .pfx without storing its key anywhere (EphemeralKeySet) and returns its client certificate (GetClientCertificate), to
		///check the file and its password before anything is imported. Throws DbSettingsException (exit code 2).</summary>
		public static X509Certificate2 LoadPfxForCheck(string pfxPath,string password) {
			X509Certificate2Collection x509Certificate2Collection=ImportCollection(pfxPath,password,X509KeyStorageFlags.EphemeralKeySet);
			return GetClientCertificate(x509Certificate2Collection,pfxPath);
		}

		///<summary>Imports the client certificate of the .pfx (GetClientCertificate) into LocalMachine\My (private key not exportable) and grants
		///BUILTIN\Users read access to its private key. Reading the file with PersistKeySet stores the key of every certificate of the file that
		///has one; the import deletes again the ones it does not use, as DirectCertificateImport does: the keys of the file's other certificates
		///(its CA's, for example), and every key it stored, the client certificate's too, when the store refuses the certificate, or when the
		///store already holds it with a usable key in the machine key set (its entry then stays as it is, with that key: KeepCertificateInStore).
		///Never a key that a certificate of the store uses (DeleteKeysNotInStore). Returns its thumbprint; warning is "" or says what the
		///program's users may lack, which key could not be deleted, and which earlier key of the store's entry was left. isStoreKeyImported: the
		///store's entry uses the private key this import stored, so the tool made the key the certificate now has (DbSettingsRunner then
		///records it). Needs administrator rights. Throws DbSettingsException (exit code 2 for the file, 3 for the store), whose message also
		///names a key that could not be deleted again.</summary>
		public static string ImportPfx(string pfxPath,string password,out string warning,out bool isStoreKeyImported) {
			return ImportPfx(pfxPath,password,GetToolStoreLocation(),GetToolStoreName(),out warning,out isStoreKeyImported);
		}

		///<summary>ImportPfx into the store with this location and name, without isStoreKeyImported.</summary>
		internal static string ImportPfx(string pfxPath,string password,StoreLocation storeLocation,string storeName,out string warning) {
			return ImportPfx(pfxPath,password,storeLocation,storeName,out warning,out bool isStoreKeyImported);
		}

		///<summary>ImportPfx into the store with this location and name. The tool's is LocalMachine\My: the machine key set, and the users' read
		///access to the key. The tests use a store of the current user with a name of its own, which they make and delete: the current user's key
		///set, and no read access for the users (the key is the user's own), as DirectCertificateImport does for a store of the current user.
		///</summary>
		internal static string ImportPfx(string pfxPath,string password,StoreLocation storeLocation,string storeName,out string warning,
			out bool isStoreKeyImported)
		{
			X509KeyStorageFlags x509KeyStorageFlags=X509KeyStorageFlags.PersistKeySet;
			if(storeLocation==StoreLocation.LocalMachine) {
				x509KeyStorageFlags|=X509KeyStorageFlags.MachineKeySet;//The program's users on this computer use the key, not only this user.
			}
			else {
				x509KeyStorageFlags|=X509KeyStorageFlags.UserKeySet;
			}
			string storeText=storeLocation+"\\"+storeName;
			using(X509Store x509Store=new X509Store(storeName,storeLocation)) {
				//Opened before any key is stored: a store that cannot be opened leaves nothing behind.
				try {
					x509Store.Open(OpenFlags.ReadWrite);
				}
				catch(Exception ex) when(IsStoreException(ex)) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_WRITE_FAILED,"The client certificate could not be added to the computer's "
						+"certificate store ("+storeText+"): "+ex.Message.Trim());
				}
				X509Certificate2Collection x509Certificate2Collection=ImportCollection(pfxPath,password,x509KeyStorageFlags);
				try {
					return AddClientCertificate(x509Store,storeText,x509Certificate2Collection,pfxPath,storeLocation==StoreLocation.LocalMachine,
						out warning,out isStoreKeyImported);
				}
				finally {
					ResetAll(x509Certificate2Collection);//The keys in use stay in the key set (PersistKeySet); only these objects are released.
				}
			}
		}

		///<summary>ImportPfx after the file's keys were stored: adds the client certificate to the open store, deletes the keys of the file's
		///other certificates, and gives the users read access to its key (isMachine, which is also the key set the import stored the keys in). A
		///client certificate that the store already holds with a usable key in that key set is not added again (KeepCertificateInStore). When
		///anything fails before the certificate is in the store, every key the import stored is deleted again and DbSettingsException says why,
		///and which key could not be deleted. The catch lets the critical exceptions through (IsCriticalException), where
		///DirectCertificateImport.AddPrivate catches every one: docs/port-notes/DIRECT-CERT-FOLLOWUP.md, last round, item 5.</summary>
		private static string AddClientCertificate(X509Store x509Store,string storeText,X509Certificate2Collection x509Certificate2Collection,
			string pfxPath,bool isMachine,out string warning,out bool isStoreKeyImported)
		{
			warning="";
			isStoreKeyImported=false;
			//Every certificate of the file with a private key: the import just stored its key, in a key container of its own (a key that two
			//certificates of the file share, in one).
			List<X509Certificate2> listKeysStored=x509Certificate2Collection.Cast<X509Certificate2>().Where(x => x.HasPrivateKey).ToList();
			X509Certificate2 x509Certificate2;
			X509Certificate2 x509Certificate2InStore=null;
			//The store's entry's earlier key, when it is in another key set or cannot be identified: left untouched, and the warning says where.
			string warningEntryKey="";
			try {
				ActionAfterKeysStoredForTests?.Invoke(x509Certificate2Collection);
				x509Certificate2=GetClientCertificate(x509Certificate2Collection,pfxPath);
				x509Certificate2InStore=FindInStoreNow(x509Store,x509Certificate2.Thumbprint);
				if(x509Certificate2InStore!=null && !IsEntryKeyUsable(x509Certificate2InStore,isMachine,storeText,out warningEntryKey)) {
					x509Certificate2InStore.Reset();
					x509Certificate2InStore=null;
				}
				if(x509Certificate2InStore==null) {
					ActionBeforeStoreAddForTests?.Invoke(x509Certificate2);
					x509Store.Add(x509Certificate2);
				}
			}
			catch(Exception ex) when(!IsCriticalException(ex)) {
				x509Certificate2InStore?.Reset();
				//Nothing was added: every key the import stored goes again, but none a certificate of the store uses.
				string notDeleted=GetKeysNotDeletedText(listKeysStored,x509Store,new List<X509Certificate2>(),
					"A private key the tool stored could not be deleted again");
				if(ex is DbSettingsException) {
					throw new DbSettingsException(((DbSettingsException)ex).ExitCode,ex.Message+notDeleted);
				}
				if(IsStoreException(ex)) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_WRITE_FAILED,"The client certificate could not be added to the computer's "
						+"certificate store ("+storeText+"): "+EndSentence(ex.Message)+notDeleted);
				}
				throw new DbSettingsException(DbSettingsRunner.EXIT_WRITE_FAILED,"The client certificate could not be imported: "
					+EndSentence(ex.Message)+notDeleted);
			}
			if(x509Certificate2InStore!=null) {
				try {
					return KeepCertificateInStore(x509Store,x509Certificate2InStore,x509Certificate2,listKeysStored,isMachine,out warning,
						out isStoreKeyImported);
				}
				finally {
					x509Certificate2InStore.Reset();
				}
			}
			isStoreKeyImported=true;//Added: the store's entry has the key this import stored.
			warning=warningEntryKey;
			//The file's other certificates with a key (its CA's, for example): their keys were just stored and are not used, unless the client
			//certificate, or a certificate of the store, uses the same key container.
			List<X509Certificate2> listUnused=listKeysStored.FindAll(x => x!=x509Certificate2);
			warning=AppendSentence(warning,GetKeysNotDeletedText(listUnused,x509Store,new List<X509Certificate2> { x509Certificate2 },
				"The client certificate was imported, but a private key of another certificate of the file, which the tool stored and does not use, "
				+"could not be deleted").Trim());
			if(isMachine) {
				try {
					GrantUsersReadToPrivateKey(x509Certificate2);
				}
				catch(Exception ex) {
					warning=AppendSentence(warning,"The client certificate was imported, but the users of this computer may not be able to use "
						+"its private key: "+EndSentence(ex.Message)+" In certlm.msc, Personal > Certificates, right-click it, All Tasks > Manage "
						+"Private Keys, and give Users Read.");
				}
			}
			return x509Certificate2.Thumbprint;
		}

		///<summary>True when the store's entry of the client certificate can stay as it is (KeepCertificateInStore): its key provider information
		///names a key in the key set the import uses (the machine's for LocalMachine\My, isMachine; the user's for the tests' store), read without
		///opening the key, and only then the key opens and signs or decrypts (IsKeyUsable). An entry whose key is in another key set (an
		///administrator's own user key set, say, which the program's users cannot read) or cannot be identified is not usable, and
		///warningEntryKey says that the import gives the entry its own key and leaves the earlier one untouched, and where that one is. An entry
		///without a key, or with one that does not open, is not usable either, without a warning, as before.</summary>
		private static bool IsEntryKeyUsable(X509Certificate2 x509Certificate2InStore,bool isMachine,string storeText,out string warningEntryKey) {
			warningEntryKey="";
			KeyProvInfo keyProvInfoEntry=ReadKeyProvInfo(x509Certificate2InStore);
			if(!keyProvInfoEntry.IsNamed) {
				return false;
			}
			if(keyProvInfoEntry.IsUnknown || keyProvInfoEntry.IsMachine!=isMachine) {
				warningEntryKey=GetEntryKeyLeftText(keyProvInfoEntry,isMachine,storeText);
				return false;
			}
			return IsKeyUsable(x509Certificate2InStore);
		}

		///<summary>The warning for a store entry of the client certificate whose private key is in another key set than the import's, or cannot be
		///identified (IsEntryKeyUsable): the import gave the entry the key it stored, and the entry's earlier key was left untouched, where it is,
		///with the command that deletes it once no other certificate uses it. The tool deletes no key it did not make.</summary>
		private static string GetEntryKeyLeftText(KeyProvInfo keyProvInfoEntry,bool isMachine,string storeText) {
			string start="The client certificate was already in the computer's certificate store ("+storeText+"), ";
			if(keyProvInfoEntry.IsUnknown) {
				return start+"but the private key its entry names could not be identified: "+EndSentence(keyProvInfoEntry.Problem)+" The import gave "
					+"the entry the private key it stored, and left the entry's earlier key, if there is one, untouched.";
			}
			string keySetEntry=keyProvInfoEntry.IsMachine ? "the computer's key set" : "the user key set of the Windows account that put it there";
			string keySetImport=isMachine ? "the computer's key set" : "the current user's key set";
			string who=keyProvInfoEntry.IsMachine ? "An administrator" : "That account";
			string certutil="certutil "+(keyProvInfoEntry.IsMachine ? "" : "-user ")+"-csp \""+keyProvInfoEntry.ProviderName+"\" -delkey \""
				+keyProvInfoEntry.ContainerName+"\"";
			return start+"with its private key in "+keySetEntry+(isMachine ? ", which the program's users cannot use" : "")+", not in "+keySetImport
				+", which the import uses. The import gave the entry the private key it stored, and left the entry's earlier key untouched: provider "
				+keyProvInfoEntry.ProviderName+", key container "+keyProvInfoEntry.ContainerName+", in "+keySetEntry+". "+who+" can delete it, once "
				+"no other certificate uses it: "+certutil+".";
		}

		///<summary>AddClientCertificate for a client certificate that the store already holds with a usable key in the import's key set (as when
		///setup runs again with the same .pfx): the store's entry stays as it is, with its key, as DirectCertificateImport leaves such a
		///certificate; the store's add would give the entry the key the import just stored and leave the key it had on the computer, named by no
		///certificate and readable by the users. So every key this import stored is deleted again, the client certificate's own too;
		///DeleteKeysNotInStore keeps the entry's key, which a certificate of the store uses, so no key that was there before is deleted. The users
		///get read access to the entry's key (isMachine), as to an imported one: the program reads the certificate as the signed-in user. Returns
		///its thumbprint; warning is "" or says which key could not be deleted and what the users may lack. isStoreKeyImported: the entry names
		///the key container the import stored the client certificate's key in (a CAPI key whose container the entry names, deleted before and
		///stored there again by this import), so the entry's key is the one this import made.</summary>
		private static string KeepCertificateInStore(X509Store x509Store,X509Certificate2 x509Certificate2InStore,X509Certificate2 x509Certificate2,
			List<X509Certificate2> listKeysStored,bool isMachine,out string warning,out bool isStoreKeyImported)
		{
			KeyProvInfo keyProvInfoEntry=ReadKeyProvInfo(x509Certificate2InStore);
			KeyProvInfo keyProvInfoImport=ReadKeyProvInfo(x509Certificate2);
			isStoreKeyImported=keyProvInfoEntry.IsNamed && !keyProvInfoEntry.IsUnknown && keyProvInfoImport.IsNamed && !keyProvInfoImport.IsUnknown
				&& string.Equals(keyProvInfoEntry.Identity,keyProvInfoImport.Identity,StringComparison.OrdinalIgnoreCase);
			string start="The client certificate was already in the computer's certificate store with a usable private key and stays as it is, but ";
			warning=GetKeysNotDeletedText(listKeysStored,x509Store,new List<X509Certificate2>(),start+"a private key of the file, which the tool "
				+"stored and does not use, could not be deleted").Trim();
			if(isMachine) {
				try {
					GrantUsersReadToPrivateKey(x509Certificate2InStore);
				}
				catch(Exception ex) {
					warning=AppendSentence(warning,start+"the users of this computer may not be able to use its private key: "+EndSentence(ex.Message)
						+" In certlm.msc, Personal > Certificates, right-click it, All Tasks > Manage Private Keys, and give Users Read.");
				}
			}
			return x509Certificate2InStore.Thumbprint;
		}

		///<summary>The store's certificate with this thumbprint, or null, read anew through a handle of its own, as DeleteKeysNotInStore reads
		///the store: the caller's open handle may keep the certificates as it read them when it was opened. The caller resets it. Throws what
		///opening the store throws.</summary>
		private static X509Certificate2 FindInStoreNow(X509Store x509Store,string thumbprint) {
			using(X509Store x509StoreNow=new X509Store(x509Store.Name,x509Store.Location)) {
				x509StoreNow.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
				X509Certificate2 x509Certificate2Found=null;
				foreach(X509Certificate2 x509Certificate2 in x509StoreNow.Certificates) {
					if(x509Certificate2Found==null && string.Equals(x509Certificate2.Thumbprint,thumbprint,StringComparison.OrdinalIgnoreCase)) {
						x509Certificate2Found=x509Certificate2;
						continue;
					}
					x509Certificate2.Reset();//The others are released at once.
				}
				return x509Certificate2Found;
			}
		}

		///<summary>Deletes the private keys of these certificates that no certificate of the store or of listInUse uses (DeleteKeysNotInStore),
		///and returns a sentence for each one that could not be deleted (start, the certificate, why, and where its key is left), each after a
		///space; "" when all were.</summary>
		private static string GetKeysNotDeletedText(List<X509Certificate2> listX509Certificate2s,X509Store x509Store,
			List<X509Certificate2> listInUse,string start)
		{
			//The files are named before the keys are deleted: a key that is half deleted may no longer say where it was. These are the keys the
			//tool stored or imported itself, which it opens to delete anyway.
			List<string> listPaths=listX509Certificate2s.Select(x => TryGetPrivateKeyFilePath(x)).ToList();
			List<KeyNotDeleted> listNotDeleted=DeleteKeysNotInStore(x509Store,listX509Certificate2s,listInUse);
			string text="";
			for(int i=0;i<listX509Certificate2s.Count;i++) {
				if(listNotDeleted[i]==null) {
					continue;
				}
				text+=" "+start+" ("+listX509Certificate2s[i].Subject+", "+listX509Certificate2s[i].Thumbprint+"): "+EndSentence(listNotDeleted[i].Why)
					+GetKeyLeftText(listPaths[i],listNotDeleted[i].IsMaybeInUse);
			}
			return text;
		}

		///<summary>Why a private key was not deleted (DeleteKeysNotInUse), and whether it was kept because a certificate of the store may use it
		///(its key could not be identified, or the store could not be read): then the user must not simply delete it.</summary>
		private sealed class KeyNotDeleted {
			public string Why;
			public bool IsMaybeInUse;

			public KeyNotDeleted(string why,bool isMaybeInUse) {
				Why=why;
				IsMaybeInUse=isMaybeInUse;
			}
		}

		///<summary>Removes the certificate with this thumbprint from LocalMachine\My and deletes its private key, so that no key that BUILTIN\Users
		///may read is left behind. Only for the client certificate this tool imported itself and the saved settings no longer use
		///(DbSettingsRunner.PlanImportedCertificate, owner decision of 2026-11-02): a certificate the tool did not import is never removed. Returns
		///"" when it is gone (or was not there), else a warning that says what is left and how to remove it by hand. Never throws.</summary>
		public static string RemoveImported(string thumbprint) {
			return RemoveImported(thumbprint,GetToolStoreLocation(),GetToolStoreName());
		}

		///<summary>RemoveImported for the store with this location and name: the tool's is LocalMachine\My; the tests use a store of the current
		///user with a name of its own, which they make and delete, never the computer's or the user's real stores. The certificate goes first,
		///then its key, as DirectCertificateImport removes a replaced certificate (RemoveReplaced, and Undo): a certificate the store does not let
		///go of keeps a key that works, and the key is deleted only when no certificate left in the store uses the same key container
		///(DeleteKeysNotInStore). When only the key could not be deleted, the warning says that the certificate was removed and where its key
		///file is, which certlm.msc no longer shows.</summary>
		internal static string RemoveImported(string thumbprint,StoreLocation storeLocation,string storeName) {
			if(string.IsNullOrEmpty(thumbprint)) {
				return "";
			}
			string storeText=storeLocation+"\\"+storeName;
			try {
				using(X509Store x509Store=new X509Store(storeName,storeLocation)) {
					x509Store.Open(OpenFlags.ReadWrite|OpenFlags.OpenExistingOnly);
					X509Certificate2Collection x509Certificate2CollectionFound=x509Store.Certificates.Find(X509FindType.FindByThumbprint,
						thumbprint,false);
					try {
						List<X509Certificate2> listRemoved=new List<X509Certificate2>();
						foreach(X509Certificate2 x509Certificate2 in x509Certificate2CollectionFound) {
							x509Store.Remove(x509Certificate2);
							listRemoved.Add(x509Certificate2);
						}
						//Once it is out of the store: another certificate of the store may use the same key container.
						return GetKeysNotDeletedText(listRemoved,x509Store,new List<X509Certificate2>(),"The client certificate that this tool "
							+"imported before was removed from the computer's certificate store ("+storeText+"), but its private key could not be "
							+"deleted").Trim();
					}
					finally {
						ResetAll(x509Certificate2CollectionFound);
					}
				}
			}
			catch(Exception ex) when(IsStoreException(ex) || ex is IOException) {
				//The certificate and its key are both still there. certlm.msc's Delete would leave the key on the computer.
				return "The client certificate "+thumbprint+" that this tool imported before could not be removed from the computer's "
					+"certificate store ("+storeText+"): "+EndSentence(ex.Message)+" Remove it with its private key as an administrator, in "
					+"PowerShell: Remove-Item Cert:\\"+storeText+"\\"+thumbprint+" -DeleteKey (certlm.msc's Delete leaves the key).";
			}
		}

		///<summary>Deletes the private key of each certificate of listX509Certificate2s, but not one that a certificate of the store, or of
		///listInUse, still uses (DeleteKeysNotInUse), as DirectCertificateImport.DeleteKeysNotInStore does. The store is read anew, not through
		///the caller's open handle, which keeps the certificates as it read them when it was opened: a certificate another program put into the
		///store since counts too. The certificates read from the store are released again; their keys are never opened (ReadKeyProvInfo). Why
		///each key could not be deleted, in the order of listX509Certificate2s (null when it was deleted, it had none, or another certificate
		///uses it); when the store can't be read, no key is deleted.</summary>
		private static List<KeyNotDeleted> DeleteKeysNotInStore(X509Store x509Store,List<X509Certificate2> listX509Certificate2s,
			List<X509Certificate2> listInUse)
		{
			if(listX509Certificate2s.Count==0) {
				return new List<KeyNotDeleted>();
			}
			X509Certificate2Collection x509Certificate2CollectionStore;
			try {
				using(X509Store x509StoreNow=new X509Store(x509Store.Name,x509Store.Location)) {
					x509StoreNow.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
					x509Certificate2CollectionStore=x509StoreNow.Certificates;//The certificates stay valid after the store is closed.
				}
			}
			catch(Exception ex) when(IsStoreException(ex)) {
				KeyNotDeleted keyNotDeleted=new KeyNotDeleted("The store could not be read to see whether another certificate uses the key: "
					+ex.Message.Trim(),true);
				return listX509Certificate2s.Select(x => x.HasPrivateKey ? keyNotDeleted : null).ToList();
			}
			try {
				List<X509Certificate2> listInUseAll=x509Certificate2CollectionStore.Cast<X509Certificate2>().ToList();
				listInUseAll.AddRange(listInUse);
				return DeleteKeysNotInUse(listX509Certificate2s,listInUseAll);
			}
			finally {
				ResetAll(x509Certificate2CollectionStore);
			}
		}

		///<summary>Deletes the private key of each certificate of listX509Certificate2s, each key container once, but none that a certificate of
		///listInUse uses: one .pfx can give two of its certificates one key (a renewal with the same key), which Windows stores once, in one
		///container, for both. Containers are compared by their key provider information (ReadKeyProvInfo, KeyProvInfo.Identity), all read
		///before the first is deleted, and no key of listInUse is opened: a key on a TPM or a smart card could ask for its PIN, or fail. A
		///certificate whose key cannot be identified may use any of them, so then none is deleted, and each says why. Why each key could not be
		///deleted, in the order of listX509Certificate2s: null when it was deleted, it had none, or a certificate of listInUse uses it.</summary>
		private static List<KeyNotDeleted> DeleteKeysNotInUse(List<X509Certificate2> listX509Certificate2s,List<X509Certificate2> listInUse) {
			//Every container first: once a container is deleted, the other certificates that used it no longer say which one it was.
			List<KeyProvInfo> listKeyProvInfos=listX509Certificate2s.Select(x => ReadKeyProvInfo(x)).ToList();
			List<KeyProvInfo> listKeyProvInfosInUse=listInUse.Select(x => ReadKeyProvInfo(x)).ToList();
			string whyUnknownInUse=null;
			int indexUnknown=listKeyProvInfosInUse.FindIndex(x => x.IsUnknown);
			if(indexUnknown>=0) {
				whyUnknownInUse="It was kept, because the private key of the certificate "+listInUse[indexUnknown].Subject+" ("
					+listInUse[indexUnknown].Thumbprint+"), which stays, could not be identified ("+listKeyProvInfosInUse[indexUnknown].Problem
					+"), and it may be this one.";
			}
			HashSet<string> hashSetInUse=new HashSet<string>(listKeyProvInfosInUse.Where(x => x.IsNamed && !x.IsUnknown).Select(x => x.Identity),
				StringComparer.OrdinalIgnoreCase);
			HashSet<string> hashSetDeleted=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			List<KeyNotDeleted> listNotDeleted=new List<KeyNotDeleted>();
			for(int i=0;i<listX509Certificate2s.Count;i++) {
				KeyProvInfo keyProvInfo=listKeyProvInfos[i];
				if(!keyProvInfo.IsNamed) {
					listNotDeleted.Add(null);//No key stored for it.
					continue;
				}
				if(keyProvInfo.IsUnknown) {
					listNotDeleted.Add(new KeyNotDeleted("It was kept, because its key container could not be identified ("+keyProvInfo.Problem+"), so "
						+"a certificate of the store may use it.",true));
					continue;
				}
				if(whyUnknownInUse!=null) {
					listNotDeleted.Add(new KeyNotDeleted(whyUnknownInUse,true));
					continue;
				}
				if(hashSetInUse.Contains(keyProvInfo.Identity) || hashSetDeleted.Contains(keyProvInfo.Identity)) {
					listNotDeleted.Add(null);//In use by a certificate that stays, or deleted already for another certificate of the list.
					continue;
				}
				string error=TryDeletePrivateKey(listX509Certificate2s[i]);
				if(error=="") {
					hashSetDeleted.Add(keyProvInfo.Identity);
					listNotDeleted.Add(null);
					continue;
				}
				listNotDeleted.Add(new KeyNotDeleted(error,false));
			}
			return listNotDeleted;
		}

		///<summary>The certificate's key provider information (CERT_KEY_PROV_INFO_PROP_ID, through CertGetCertificateContextProperty), which names
		///its private key's container, provider and key set, read without opening the key: a key on a TPM or a smart card is never asked for its
		///PIN to compare containers. A certificate without the property names no key; one whose property cannot be read gives Problem. Never
		///throws: anything but a critical exception (IsCriticalException) is a property that cannot be read.</summary>
		internal static KeyProvInfo ReadKeyProvInfo(X509Certificate2 x509Certificate2) {
			KeyProvInfo keyProvInfo=new KeyProvInfo();
			try {
				ActionBeforeKeyProvInfoReadForTests?.Invoke(x509Certificate2);
				IntPtr intPtrCertContext=x509Certificate2.Handle;
				if(intPtrCertContext==IntPtr.Zero) {
					keyProvInfo.IsNamed=true;
					keyProvInfo.Problem="The certificate was already released.";
					return keyProvInfo;
				}
				uint byteCount=0;
				if(!CertGetCertificateContextProperty(intPtrCertContext,CERT_KEY_PROV_INFO_PROP_ID,IntPtr.Zero,ref byteCount)) {
					int error=Marshal.GetLastWin32Error();
					if(error==CRYPT_E_NOT_FOUND) {
						return keyProvInfo;//No property: the certificate names no private key.
					}
					keyProvInfo.IsNamed=true;
					keyProvInfo.Problem=new Win32Exception(error).Message.Trim();
					return keyProvInfo;
				}
				keyProvInfo.IsNamed=true;
				IntPtr intPtrData=Marshal.AllocHGlobal((int)byteCount);
				try {
					if(!CertGetCertificateContextProperty(intPtrCertContext,CERT_KEY_PROV_INFO_PROP_ID,intPtrData,ref byteCount)) {
						keyProvInfo.Problem=new Win32Exception(Marshal.GetLastWin32Error()).Message.Trim();
						return keyProvInfo;
					}
					CRYPT_KEY_PROV_INFO cryptKeyProvInfo=(CRYPT_KEY_PROV_INFO)Marshal.PtrToStructure(intPtrData,typeof(CRYPT_KEY_PROV_INFO));
					string containerName=Marshal.PtrToStringUni(cryptKeyProvInfo.pwszContainerName);//Null for IntPtr.Zero.
					if(string.IsNullOrEmpty(containerName)) {
						keyProvInfo.Problem="The certificate's key provider information names no key container.";
						return keyProvInfo;
					}
					keyProvInfo.ContainerName=containerName;
					keyProvInfo.ProviderName=Marshal.PtrToStringUni(cryptKeyProvInfo.pwszProvName)??"";
					keyProvInfo.ProviderType=(int)cryptKeyProvInfo.dwProvType;
					keyProvInfo.IsMachine=(cryptKeyProvInfo.dwFlags & KEY_PROV_INFO_MACHINE_KEYSET)!=0;
					return keyProvInfo;
				}
				finally {
					Marshal.FreeHGlobal(intPtrData);
				}
			}
			catch(Exception ex) when(!IsCriticalException(ex)) {
				keyProvInfo.IsNamed=true;
				keyProvInfo.Problem=ex.Message.Trim();
				return keyProvInfo;
			}
			finally {
				GC.KeepAlive(x509Certificate2);//Its certificate context is used until here.
			}
		}

		///<summary>Deletes the private key of the certificate from the key set it lives in: a CAPI key container (RSA) or a CNG key (RSA or ECDSA),
		///as DirectCertificateImport.TryDeletePrivateKey does. A CAPI key is deleted through CAPI (GetCapiRsaPrivateKey, DeleteCapiKey): .NET
		///Framework's GetRSAPrivateKey opens it through CNG, as an RSACng whose Key.Delete returns without deleting the CAPI container. The whole
		///container goes, also for another certificate that uses the same key, so the callers ask DeleteKeysNotInUse first. "" when deleted (or
		///there was none), else why not. Never throws: anything but a critical exception (IsCriticalException) is a key not deleted.</summary>
		internal static string TryDeletePrivateKey(X509Certificate2 x509Certificate2) {
			try {
				if(!x509Certificate2.HasPrivateKey) {
					return "";
				}
				ActionBeforeKeyDeleteForTests?.Invoke(x509Certificate2);
				RSACryptoServiceProvider rsaCryptoServiceProviderCapi=GetCapiRsaPrivateKey(x509Certificate2);
				if(rsaCryptoServiceProviderCapi!=null) {
					return DeleteCapiKey(rsaCryptoServiceProviderCapi);
				}
				using(RSA rsa=OpenRsaPrivateKey(x509Certificate2)) {
					if(rsa is RSACng) {
						((RSACng)rsa).Key.Delete();
						return "";
					}
					if(rsa is RSACryptoServiceProvider) {//How .NET (not .NET Framework) gives a CAPI key.
						return DeleteCapiKey((RSACryptoServiceProvider)rsa);
					}
					if(rsa!=null) {
						return "";
					}
				}
				using(ECDsa ecdsa=OpenECDsaPrivateKey(x509Certificate2)) {
					if(ecdsa is ECDsaCng) {
						((ECDsaCng)ecdsa).Key.Delete();
					}
				}
				return "";
			}
			catch(Exception ex) when(!IsCriticalException(ex)) {
				return ex.Message.Trim();//Windows' texts end with a line break.
			}
		}

		///<summary>Deletes a CAPI key container in the key set the key lives in: PersistKeyInCsp=false, then Clear (CRYPT_DELETEKEYSET). Clear
		///does not say when Windows did not delete it, so afterwards the container must no longer open (CspParameters with UseExistingKey, in its
		///key set, silent) and its file must be gone (GetCapiKeyFilePath, named before the delete). "" when it is gone, else what is still there.
		///Throws what reading the container's information throws.</summary>
		private static string DeleteCapiKey(RSACryptoServiceProvider rsaCryptoServiceProvider) {
			CspKeyContainerInfo cspKeyContainerInfo=rsaCryptoServiceProvider.CspKeyContainerInfo;
			string providerName=cspKeyContainerInfo.ProviderName;
			int providerType=cspKeyContainerInfo.ProviderType;
			string containerName=cspKeyContainerInfo.KeyContainerName;
			bool isMachine=cspKeyContainerInfo.MachineKeyStore;
			KeyNumber keyNumber=cspKeyContainerInfo.KeyNumber;
			string pathKey=GetCapiKeyFilePath(isMachine,cspKeyContainerInfo.UniqueKeyContainerName);
			if(ActionInsteadOfCapiKeyDeleteForTests!=null) {
				ActionInsteadOfCapiKeyDeleteForTests(rsaCryptoServiceProvider);
			}
			else {
				//Clear then deletes the key container, in the key set the certificate names for its key (user or machine).
				rsaCryptoServiceProvider.PersistKeyInCsp=false;
				rsaCryptoServiceProvider.Clear();
			}
			bool isOpening=DoesCapiKeyContainerOpen(providerName,providerType,containerName,isMachine,keyNumber);
			bool isFileThere=pathKey!=null && File.Exists(pathKey);
			if(!isOpening && !isFileThere) {
				return "";
			}
			string still;
			if(isOpening && isFileThere) {
				still="its key container still opens, and its file is still there";
			}
			else if(isOpening) {
				still="its key container still opens";
			}
			else {
				still="its file is still there";
			}
			return "Windows reported no error, but the key is still there: "+still+".";
		}

		///<summary>True when the CAPI key container opens in its key set (UseExistingKey, so nothing is made; NoPrompt, so nothing asks), which
		///leaves it as it is (opened by name, it persists).</summary>
		private static bool DoesCapiKeyContainerOpen(string providerName,int providerType,string containerName,bool isMachine,KeyNumber keyNumber) {
			CspParameters cspParameters=new CspParameters(providerType,providerName,containerName);
			cspParameters.KeyNumber=(int)keyNumber;
			cspParameters.Flags=CspProviderFlags.UseExistingKey|CspProviderFlags.NoPrompt;
			if(isMachine) {
				cspParameters.Flags|=CspProviderFlags.UseMachineKeyStore;
			}
			try {
				using(new RSACryptoServiceProvider(cspParameters)) {
				}
				return true;
			}
			catch(CryptographicException) {
				return false;//"Keyset does not exist".
			}
		}

		///<summary>The certificate's RSA private key when Windows keeps it in a CAPI key container, else null (a CNG key, an ECDSA key, no key, or
		///one that does not open: the CNG path then says why). A .pfx that names no CNG provider (OpenSSL writes none) or a CAPI provider is
		///imported into a CAPI container, and .NET Framework's GetRSAPrivateKey opens that through CNG: an RSACng whose Key.Delete leaves the CAPI
		///container, and whose Key.UniqueName is no file of the CNG folder (DIRECT-CERT, docs/port-notes/DIRECT-CERT.md). The key object comes
		///with the certificate's own key provider information, so it opens the container in the key set the key lives in. The certificate keeps
		///the object (X509Certificate2.PrivateKey), so it is not disposed here. Never throws: anything but a critical exception
		///(IsCriticalException) gives null, as in DirectCertificateImport's copy.</summary>
		private static RSACryptoServiceProvider GetCapiRsaPrivateKey(X509Certificate2 x509Certificate2) {
			try {
				ActionBeforePrivateKeyOpenForTests?.Invoke(x509Certificate2);
				return x509Certificate2.PrivateKey as RSACryptoServiceProvider;
			}
			catch(Exception ex) when(!IsCriticalException(ex)) {
				return null;//.NET Framework: "Invalid provider type specified" for a CNG key, NotSupportedException for an ECDSA one.
			}
		}

		///<summary>GetRSAPrivateKey, after ActionBeforePrivateKeyOpenForTests.</summary>
		private static RSA OpenRsaPrivateKey(X509Certificate2 x509Certificate2) {
			ActionBeforePrivateKeyOpenForTests?.Invoke(x509Certificate2);
			return x509Certificate2.GetRSAPrivateKey();
		}

		///<summary>GetECDsaPrivateKey, after ActionBeforePrivateKeyOpenForTests.</summary>
		private static ECDsa OpenECDsaPrivateKey(X509Certificate2 x509Certificate2) {
			ActionBeforePrivateKeyOpenForTests?.Invoke(x509Certificate2);
			return x509Certificate2.GetECDsaPrivateKey();
		}

		///<summary>True when the certificate's private key can be used on this computer: it opens (an RSA or ECDSA key) and signs a test digest,
		///or, for an RSA key that does not sign, decrypts what its public key encrypted. False without a key, or with one that does not open
		///(deleted, no read access, another user's). HasPrivateKey alone only says that the certificate names a key. A copy of
		///DirectCertificateImport.IsKeyUsable. Never throws: anything but a critical exception (IsCriticalException) means not usable.</summary>
		internal static bool IsKeyUsable(X509Certificate2 x509Certificate2) {
			try {
				if(!x509Certificate2.HasPrivateKey) {
					return false;
				}
				byte[] byteArrayValue=Encoding.ASCII.GetBytes("Open PMS - Dental client certificate key check");
				byte[] byteArrayDigest;
				using(SHA256 sha256=SHA256.Create()) {
					byteArrayDigest=sha256.ComputeHash(byteArrayValue);
				}
				using(RSA rsa=OpenRsaPrivateKey(x509Certificate2)) {
					if(rsa!=null) {
						return CanSign(rsa,byteArrayValue,byteArrayDigest) || CanDecrypt(rsa,x509Certificate2,byteArrayValue);
					}
				}
				using(ECDsa ecdsa=OpenECDsaPrivateKey(x509Certificate2)) {
					if(ecdsa!=null) {
						ecdsa.SignHash(byteArrayDigest);
						return true;
					}
				}
				return false;
			}
			catch(Exception ex) when(!IsCriticalException(ex)) {
				return false;
			}
		}

		private static bool CanSign(RSA rsa,byte[] byteArrayValue,byte[] byteArrayDigest) {
			try {
				rsa.SignHash(byteArrayDigest,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
				return true;
			}
			catch(Exception ex) when(!IsCriticalException(ex)) {
				//An old CAPI provider without SHA-256, or a key that only decrypts (the decryption is tried next).
			}
			try {
				byte[] byteArrayDigestSha1;
				using(SHA1 sha1=SHA1.Create()) {
					byteArrayDigestSha1=sha1.ComputeHash(byteArrayValue);
				}
				rsa.SignHash(byteArrayDigestSha1,HashAlgorithmName.SHA1,RSASignaturePadding.Pkcs1);
				return true;
			}
			catch(Exception ex) when(!IsCriticalException(ex)) {
				return false;
			}
		}

		private static bool CanDecrypt(RSA rsa,X509Certificate2 x509Certificate2,byte[] byteArrayValue) {
			try {
				using(RSA rsaPublic=x509Certificate2.GetRSAPublicKey()) {
					byte[] byteArrayEncrypted=rsaPublic.Encrypt(byteArrayValue,RSAEncryptionPadding.Pkcs1);
					return rsa.Decrypt(byteArrayEncrypted,RSAEncryptionPadding.Pkcs1).SequenceEqual(byteArrayValue);
				}
			}
			catch(Exception ex) when(!IsCriticalException(ex)) {
				return false;
			}
		}

		///<summary>True for the exceptions that a method documented "Never throws" lets through: OutOfMemoryException, StackOverflowException and
		///ThreadAbortException, after which the tool can't go on safely (DirectCertificateImport.IsCriticalException). Every other one is an
		///answer (not deleted, no name).</summary>
		internal static bool IsCriticalException(Exception ex) {
			return ex is OutOfMemoryException || ex is StackOverflowException || ex is ThreadAbortException;
		}

		///<summary>The certificate with this thumbprint in LocalMachine\My, or null.</summary>
		public static X509Certificate2 FindInStore(string thumbprint) {
			if(string.IsNullOrEmpty(thumbprint)) {
				return null;
			}
			using(X509Store x509Store=new X509Store(GetToolStoreName(),GetToolStoreLocation())) {
				x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
				X509Certificate2Collection x509Certificate2Collection=x509Store.Certificates.Find(X509FindType.FindByThumbprint,thumbprint,false);
				return x509Certificate2Collection.Count==0 ? null : x509Certificate2Collection[0];
			}
		}

		///<summary>The tool's store: LocalMachine, or the current user's for the tests' StoreNameForTests.</summary>
		private static StoreLocation GetToolStoreLocation() {
			return StoreNameForTests==null ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;
		}

		///<summary>The tool's store: My, or the tests' StoreNameForTests.</summary>
		private static string GetToolStoreName() {
			return StoreNameForTests??StoreName.My.ToString();
		}

		///<summary>"subject, expires yyyy-MM-dd" for the window and messages.</summary>
		public static string Describe(X509Certificate2 x509Certificate2) {
			if(x509Certificate2==null) {
				return "";
			}
			return x509Certificate2.Subject+", expires "+x509Certificate2.NotAfter.ToString("yyyy-MM-dd");
		}

		///<summary>Throws DbSettingsException (exit code 2) when the certificate cannot be the client certificate: no private key, expired, or an
		///Extended Key Usage without client authentication.</summary>
		public static void CheckClientCertificate(X509Certificate2 x509Certificate2,string source) {
			if(!x509Certificate2.HasPrivateKey) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The certificate "+Describe(x509Certificate2)+" ("+source+") has no "
					+"private key on this computer.");
			}
			if(x509Certificate2.NotAfter<DateTime.Now) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The certificate "+Describe(x509Certificate2)+" ("+source+") has expired.");
			}
			if(!IsForClientAuthentication(x509Certificate2)) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The certificate "+Describe(x509Certificate2)+" ("+source+") is not for "
					+"client authentication (its Extended Key Usage has no clientAuth).");
			}
		}

		///<summary>True when the certificate may be used for TLS client authentication: no Extended Key Usage, or one with clientAuth.</summary>
		private static bool IsForClientAuthentication(X509Certificate2 x509Certificate2) {
			X509EnhancedKeyUsageExtension x509EnhancedKeyUsageExtension=x509Certificate2.Extensions.OfType<X509EnhancedKeyUsageExtension>()
				.FirstOrDefault();
			return x509EnhancedKeyUsageExtension==null
				|| x509EnhancedKeyUsageExtension.EnhancedKeyUsages.Cast<Oid>().Any(x => x.Value==OID_CLIENT_AUTHENTICATION);
		}

		///<summary>True for a certificate authority's certificate: Basic Constraints with CA=true.</summary>
		private static bool IsCertificateAuthority(X509Certificate2 x509Certificate2) {
			return x509Certificate2.Extensions.OfType<X509BasicConstraintsExtension>().Any(x => x.CertificateAuthority);
		}

		private static X509Certificate2Collection ImportCollection(string pfxPath,string password,X509KeyStorageFlags x509KeyStorageFlags) {
			if(!File.Exists(pfxPath)) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The client certificate file "+pfxPath+" does not exist.");
			}
			X509Certificate2Collection x509Certificate2Collection=new X509Certificate2Collection();
			try {
				//Never X509KeyStorageFlags.Exportable: the private key cannot be exported from this computer again (owner decision Q2).
				x509Certificate2Collection.Import(pfxPath,password??"",x509KeyStorageFlags);
			}
			catch(CryptographicException) {
				//The exception text is Windows' own and names no secret, but "wrong password" is what it nearly always means.
				string message="The client certificate file "+pfxPath+" cannot be opened: the password is wrong, or it is not a .pfx file.";
				if((x509KeyStorageFlags&X509KeyStorageFlags.PersistKeySet)!=0) {
					//.NET gives no certificate when the import throws, so the tool knows of no key to delete; Windows may have stored one of the
					//file's keys before it failed (as DirectCertificateImport says).
					message+=" Nothing was imported. Windows may have left a private key of the file behind on this computer, which the tool cannot "
						+"find to delete.";
				}
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,message);
			}
			return x509Certificate2Collection;
		}

		///<summary>The client certificate of the .pfx (the lead's decision of DCFU3): of the file's certificates with a private key, those that
		///are not certificate authorities (IsCertificateAuthority) are the candidates, and of them the ones for client authentication
		///(IsForClientAuthentication) are preferred. Exactly one must be left, which is then checked (CheckClientCertificate); none, or several,
		///are refused before anything goes into the store, with what the file holds (subjects and thumbprints) and how to make a .pfx with one
		///client certificate. So a CA certificate that a file lists first with its key is never taken for the client certificate. Throws
		///DbSettingsException (exit code 2).</summary>
		private static X509Certificate2 GetClientCertificate(X509Certificate2Collection x509Certificate2Collection,string pfxPath) {
			List<X509Certificate2> listAll=x509Certificate2Collection.Cast<X509Certificate2>().ToList();
			List<X509Certificate2> listKeyed=listAll.FindAll(x => x.HasPrivateKey);
			if(listKeyed.Count==0) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The client certificate file "+pfxPath+" holds no certificate with a "
					+"private key. "+PFX_HOW_TO+".");
			}
			List<X509Certificate2> listCandidates=listKeyed.FindAll(x => !IsCertificateAuthority(x));
			List<X509Certificate2> listClientAuth=listCandidates.FindAll(x => IsForClientAuthentication(x));
			List<X509Certificate2> listChosen=listClientAuth.Count>0 ? listClientAuth : listCandidates;
			if(listChosen.Count==1) {
				//One candidate that is not for client authentication: CheckClientCertificate says so.
				CheckClientCertificate(listChosen[0],pfxPath);
				return listChosen[0];
			}
			string found;
			if(listCandidates.Count==0) {
				found="holds no client certificate: each of its certificates with a private key is a certificate authority (CA=true in its Basic "
					+"Constraints).";
			}
			else if(listClientAuth.Count==0) {
				found="holds no client certificate: none of its certificates with a private key that are not certificate authorities is for client "
					+"authentication.";
			}
			else {
				found="holds more than one certificate that can be the client certificate (with a private key, not a certificate authority, and for "
					+"client authentication): "+string.Join("; ",listChosen.Select(x => x.Subject+" ("+x.Thumbprint+")"))+".";
			}
			throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The client certificate file "+pfxPath+" "+found+" It holds: "
				+string.Join("; ",listAll.Select(x => DescribeForRefusal(x)))+". "+PFX_HOW_TO+".");
		}

		///<summary>"subject (thumbprint, what it is)" of a certificate of a refused .pfx (GetClientCertificate).</summary>
		private static string DescribeForRefusal(X509Certificate2 x509Certificate2) {
			string what;
			if(!x509Certificate2.HasPrivateKey) {
				what="without a private key";
			}
			else if(IsCertificateAuthority(x509Certificate2)) {
				what="with a private key, a certificate authority";
			}
			else if(IsForClientAuthentication(x509Certificate2)) {
				what="with a private key, for client authentication";
			}
			else {
				what="with a private key, not for client authentication";
			}
			return x509Certificate2.Subject+" ("+x509Certificate2.Thumbprint+", "+what+")";
		}

		///<summary>Gives BUILTIN\Users read access to the private key file of the certificate (GetPrivateKeyFilePath): the import's MachineKeySet
		///puts a CNG key under %ProgramData%\Microsoft\Crypto\Keys and a CAPI key under %ProgramData%\Microsoft\Crypto\RSA\MachineKeys. Throws
		///when the file is not found or its rights cannot be changed.</summary>
		private static void GrantUsersReadToPrivateKey(X509Certificate2 x509Certificate2) {
			string pathKey=GetPrivateKeyFilePath(x509Certificate2);
			if(pathKey==null || !File.Exists(pathKey)) {
				throw new FileNotFoundException("The private key file was not found.");
			}
			SecurityIdentifier securityIdentifierUsers=new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null);
			FileSecurity fileSecurity=File.GetAccessControl(pathKey);
			fileSecurity.AddAccessRule(new FileSystemAccessRule(securityIdentifierUsers,FileSystemRights.Read,AccessControlType.Allow));
			File.SetAccessControl(pathKey,fileSecurity);
		}

		///<summary>The file of the certificate's private key, in the key set it lives in (GetCapiKeyFilePath, GetCngKeyFilePath). A CAPI key is
		///named through CAPI (GetCapiRsaPrivateKey), as DirectCertificateImport does: the RSACng that .NET Framework's GetRSAPrivateKey gives for it
		///has a UniqueName that is no file of the CNG folder. Opens the key: only for a key the tool stored or imported itself, or the one it
		///gives the users read access to. Null without a key or for another kind of key. Throws CryptographicException for a key that does not
		///open.</summary>
		internal static string GetPrivateKeyFilePath(X509Certificate2 x509Certificate2) {
			if(!x509Certificate2.HasPrivateKey) {
				return null;
			}
			RSACryptoServiceProvider rsaCryptoServiceProviderCapi=GetCapiRsaPrivateKey(x509Certificate2);
			if(rsaCryptoServiceProviderCapi!=null) {
				CspKeyContainerInfo cspKeyContainerInfoCapi=rsaCryptoServiceProviderCapi.CspKeyContainerInfo;
				return GetCapiKeyFilePath(cspKeyContainerInfoCapi.MachineKeyStore,cspKeyContainerInfoCapi.UniqueKeyContainerName);
			}
			//The CngKey objects are disposed too: .NET gives a new one for each Key (.NET Framework its own, which the using of rsa or ecdsa then
			//finds disposed).
			using(RSA rsa=OpenRsaPrivateKey(x509Certificate2)) {
				if(rsa is RSACng) {
					using(CngKey cngKey=((RSACng)rsa).Key) {
						return GetCngKeyFilePath(cngKey.IsMachineKey,cngKey.UniqueName);
					}
				}
				if(rsa is RSACryptoServiceProvider) {//How .NET (not .NET Framework) gives a CAPI key.
					CspKeyContainerInfo cspKeyContainerInfo=((RSACryptoServiceProvider)rsa).CspKeyContainerInfo;
					return GetCapiKeyFilePath(cspKeyContainerInfo.MachineKeyStore,cspKeyContainerInfo.UniqueKeyContainerName);
				}
				if(rsa!=null) {
					return null;
				}
			}
			using(ECDsa ecdsa=OpenECDsaPrivateKey(x509Certificate2)) {
				if(ecdsa is ECDsaCng) {
					using(CngKey cngKey=((ECDsaCng)ecdsa).Key) {
						return GetCngKeyFilePath(cngKey.IsMachineKey,cngKey.UniqueName);
					}
				}
			}
			return null;
		}

		///<summary>GetPrivateKeyFilePath, or null when the key does not open. Never throws: anything but a critical exception (IsCriticalException)
		///gives null.</summary>
		private static string TryGetPrivateKeyFilePath(X509Certificate2 x509Certificate2) {
			try {
				return GetPrivateKeyFilePath(x509Certificate2);
			}
			catch(Exception ex) when(!IsCriticalException(ex)) {
				return null;
			}
		}

		///<summary>Where a private key that could not be deleted is left, after a sentence that says so: its file, named before the delete
		///(TryGetPrivateKeyFilePath). Once the certificate is out of the store, certlm.msc no longer shows the key. isMaybeInUse: the key was kept
		///because a certificate of the store may use it, so it is deleted by hand only once none does.</summary>
		private static string GetKeyLeftText(string pathKey,bool isMaybeInUse) {
			if(string.IsNullOrEmpty(pathKey)) {
				return " The key is left on this computer; its file could not be named.";
			}
			if(isMaybeInUse) {
				return " The key is left on this computer in "+pathKey+": delete that file as an administrator only once no certificate of the store "
					+"uses it.";
			}
			return " The key is left on this computer in "+pathKey+": delete that file as an administrator.";
		}

		///<summary>The file of a CAPI RSA key container: %ProgramData%\Microsoft\Crypto\RSA\MachineKeys for the machine key set (the tool's import),
		///%APPDATA%\Microsoft\Crypto\RSA\(the current user's SID) for the user's.</summary>
		internal static string GetCapiKeyFilePath(bool isMachineKey,string uniqueKeyContainerName) {
			if(string.IsNullOrEmpty(uniqueKeyContainerName)) {
				return null;
			}
			if(isMachineKey) {
				return Path.Combine(GetCryptoFolder(true),"RSA","MachineKeys",uniqueKeyContainerName);
			}
			using(WindowsIdentity windowsIdentity=WindowsIdentity.GetCurrent()) {
				return Path.Combine(GetCryptoFolder(false),"RSA",windowsIdentity.User.Value,uniqueKeyContainerName);
			}
		}

		///<summary>The file of a CNG key: %ProgramData%\Microsoft\Crypto\Keys for the machine key set (the tool's import),
		///%APPDATA%\Microsoft\Crypto\Keys for the user's. Null for an ephemeral key, which has no file.</summary>
		internal static string GetCngKeyFilePath(bool isMachineKey,string uniqueName) {
			if(string.IsNullOrEmpty(uniqueName)) {
				return null;
			}
			return Path.Combine(GetCryptoFolder(isMachineKey),"Keys",uniqueName);
		}

		private static string GetCryptoFolder(bool isMachineKey) {
			Environment.SpecialFolder specialFolder=isMachineKey ? Environment.SpecialFolder.CommonApplicationData
				: Environment.SpecialFolder.ApplicationData;
			return Path.Combine(Environment.GetFolderPath(specialFolder),"Microsoft","Crypto");
		}

		private static bool IsStoreException(Exception ex) {
			return ex is CryptographicException || ex is UnauthorizedAccessException || ex is SecurityException;
		}

		///<summary>Releases the certificate objects. Their keys stay where they are (PersistKeySet, or a store's).</summary>
		private static void ResetAll(X509Certificate2Collection x509Certificate2Collection) {
			foreach(X509Certificate2 x509Certificate2 in x509Certificate2Collection) {
				x509Certificate2.Reset();
			}
		}

		///<summary>The text, trimmed, ending with a full stop, so that the next sentence can follow it.</summary>
		private static string EndSentence(string text) {
			text=(text??"").Trim();
			if(text=="" || text.EndsWith(".") || text.EndsWith("!") || text.EndsWith("?")) {
				return text;
			}
			return text+".";
		}

		///<summary>The sentence after the text, with a space between them; either may be blank.</summary>
		private static string AppendSentence(string text,string sentence) {
			if(string.IsNullOrEmpty(sentence)) {
				return text;
			}
			return string.IsNullOrEmpty(text) ? sentence : text+" "+sentence;
		}
	}
}
