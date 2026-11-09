// The client certificate in the computer's certificate store (LocalMachine\My), its private key not exportable.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading;

namespace OpenPmsDentalDbSettings {
	///<summary>The client certificate (owner decision Q2): one shared certificate on every workstation, or one per computer later, in
	///LocalMachine\My, read by MySqlConnector by thumbprint. A .pfx is imported with MachineKeySet and PersistKeySet and never Exportable, and
	///BUILTIN\Users get read access to its private key, because the program runs as the signed-in user (docs/specs/db-connection-security.md
	///section 4.3).</summary>
	public static class ClientCertificateStore {
		///<summary>Extended Key Usage OID of TLS client authentication.</summary>
		private const string OID_CLIENT_AUTHENTICATION="1.3.6.1.5.5.7.3.2";
		///<summary>The tests only: called with the file's certificates right after ImportPfx stored their keys (PersistKeySet), before anything
		///else of the import; throwing there is the import failing after the keys were stored. Null in the tool.</summary>
		internal static Action<X509Certificate2Collection> ActionAfterKeysStoredForTests;
		///<summary>The tests only: called with the client certificate right before ImportPfx adds it to the store; a CryptographicException there
		///is a store that refuses it. Null in the tool.</summary>
		internal static Action<X509Certificate2> ActionBeforeStoreAddForTests;
		///<summary>The tests only: called with each certificate right before its private key is deleted (TryDeletePrivateKey); throwing there is a
		///key that cannot be deleted. Null in the tool.</summary>
		internal static Action<X509Certificate2> ActionBeforeKeyDeleteForTests;

		///<summary>Opens the .pfx without storing its key anywhere (EphemeralKeySet) and returns its certificate with a private key, to check the
		///file and its password before anything is imported. Throws DbSettingsException (exit code 2).</summary>
		public static X509Certificate2 LoadPfxForCheck(string pfxPath,string password) {
			X509Certificate2Collection x509Certificate2Collection=ImportCollection(pfxPath,password,X509KeyStorageFlags.EphemeralKeySet);
			return GetClientCertificate(x509Certificate2Collection,pfxPath);
		}

		///<summary>Imports the client certificate of the .pfx into LocalMachine\My (private key not exportable) and grants BUILTIN\Users read
		///access to its private key. Reading the file with PersistKeySet stores the key of every certificate of the file that has one; the import
		///deletes again the ones it does not use, as DirectCertificateImport does: the keys of the file's other certificates (its CA's, for
		///example), and every key it stored, the client certificate's too, when the store refuses the certificate. Never a key that a certificate
		///of the store uses (DeleteKeysNotInStore). Returns its thumbprint; warning is "" or says what the program's users may lack and which key
		///could not be deleted. Needs administrator rights. Throws DbSettingsException (exit code 2 for the file, 3 for the store), whose message
		///also names a key that could not be deleted again.</summary>
		public static string ImportPfx(string pfxPath,string password,out string warning) {
			return ImportPfx(pfxPath,password,StoreLocation.LocalMachine,StoreName.My.ToString(),out warning);
		}

		///<summary>ImportPfx into the store with this location and name. The tool's is LocalMachine\My: the machine key set, and the users' read
		///access to the key. The tests use a store of the current user with a name of its own, which they make and delete: the current user's key
		///set, and no read access for the users (the key is the user's own), as DirectCertificateImport does for a store of the current user.
		///</summary>
		internal static string ImportPfx(string pfxPath,string password,StoreLocation storeLocation,string storeName,out string warning) {
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
						out warning);
				}
				finally {
					ResetAll(x509Certificate2Collection);//The keys in use stay in the key set (PersistKeySet); only these objects are released.
				}
			}
		}

		///<summary>ImportPfx after the file's keys were stored: adds the client certificate to the open store, deletes the keys of the file's
		///other certificates, and gives the users read access to its key (isMachine). When anything fails before the certificate is in the store,
		///every key the import stored is deleted again and DbSettingsException says why, and which key could not be deleted.</summary>
		private static string AddClientCertificate(X509Store x509Store,string storeText,X509Certificate2Collection x509Certificate2Collection,
			string pfxPath,bool isMachine,out string warning)
		{
			warning="";
			//Every certificate of the file with a private key: the import just stored its key, in a key container of its own (a key that two
			//certificates of the file share, in one).
			List<X509Certificate2> listKeysStored=x509Certificate2Collection.Cast<X509Certificate2>().Where(x => x.HasPrivateKey).ToList();
			X509Certificate2 x509Certificate2;
			try {
				ActionAfterKeysStoredForTests?.Invoke(x509Certificate2Collection);
				x509Certificate2=GetClientCertificate(x509Certificate2Collection,pfxPath);
				ActionBeforeStoreAddForTests?.Invoke(x509Certificate2);
				x509Store.Add(x509Certificate2);
			}
			catch(Exception ex) when(!IsCriticalException(ex)) {
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
			//The file's other certificates with a key (its CA's, for example): their keys were just stored and are not used, unless the client
			//certificate, or a certificate of the store, uses the same key container.
			List<X509Certificate2> listUnused=listKeysStored.FindAll(x => x!=x509Certificate2);
			warning=GetKeysNotDeletedText(listUnused,x509Store,new List<X509Certificate2> { x509Certificate2 },
				"The client certificate was imported, but a private key of another certificate of the file, which the tool stored and does not use, "
				+"could not be deleted").Trim();
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

		///<summary>Deletes the private keys of these certificates that no certificate of the store or of listInUse uses (DeleteKeysNotInStore),
		///and returns a sentence for each one that could not be deleted (start, the certificate, why, and where its key is left), each after a
		///space; "" when all were.</summary>
		private static string GetKeysNotDeletedText(List<X509Certificate2> listX509Certificate2s,X509Store x509Store,
			List<X509Certificate2> listInUse,string start)
		{
			//The files are named before the keys are deleted: a key that is half deleted may no longer say where it was.
			List<string> listPaths=listX509Certificate2s.Select(x => TryGetPrivateKeyFilePath(x)).ToList();
			List<string> listErrors=DeleteKeysNotInStore(x509Store,listX509Certificate2s,listInUse);
			string text="";
			for(int i=0;i<listX509Certificate2s.Count;i++) {
				if(listErrors[i]=="") {
					continue;
				}
				text+=" "+start+" ("+listX509Certificate2s[i].Subject+", "+listX509Certificate2s[i].Thumbprint+"): "+EndSentence(listErrors[i])
					+GetKeyLeftText(listPaths[i]);
			}
			return text;
		}

		///<summary>Removes the certificate with this thumbprint from LocalMachine\My and deletes its private key, so that no key that BUILTIN\Users
		///may read is left behind. Only for the client certificate this tool imported itself and the saved settings no longer use
		///(DbSettingsRunner.PlanImportedCertificate, owner decision of 2026-11-02): a certificate the tool did not import is never removed. Returns
		///"" when it is gone (or was not there), else a warning that says what is left and how to remove it by hand. Never throws.</summary>
		public static string RemoveImported(string thumbprint) {
			return RemoveImported(thumbprint,StoreLocation.LocalMachine,StoreName.My.ToString());
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
		///store since counts too. The certificates read from the store are released again. Why each key could not be deleted, in the order of
		///listX509Certificate2s ("" when it was deleted, it had none, or another certificate uses it); when the store can't be read, no key is
		///deleted.</summary>
		private static List<string> DeleteKeysNotInStore(X509Store x509Store,List<X509Certificate2> listX509Certificate2s,
			List<X509Certificate2> listInUse)
		{
			if(listX509Certificate2s.Count==0) {
				return new List<string>();
			}
			X509Certificate2Collection x509Certificate2CollectionStore;
			try {
				using(X509Store x509StoreNow=new X509Store(x509Store.Name,x509Store.Location)) {
					x509StoreNow.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
					x509Certificate2CollectionStore=x509StoreNow.Certificates;//The certificates stay valid after the store is closed.
				}
			}
			catch(Exception ex) when(IsStoreException(ex)) {
				string error="The store could not be read to see whether another certificate uses the key: "+ex.Message.Trim();
				return listX509Certificate2s.Select(x => x.HasPrivateKey ? error : "").ToList();
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
		///container, for both. Containers are compared by GetKeyContainerName, all read before the first is deleted (DirectCertificateImport's
		///DeleteKeysNotInUse). Why each key could not be deleted, in the order of listX509Certificate2s: "" when it was deleted, it had none, or a
		///certificate of listInUse uses it.</summary>
		private static List<string> DeleteKeysNotInUse(List<X509Certificate2> listX509Certificate2s,List<X509Certificate2> listInUse) {
			//Every name first: once a container is deleted, the other certificates that used it no longer say which one it was.
			List<string> listNames=listX509Certificate2s.Select(x => GetKeyContainerName(x)).ToList();
			HashSet<string> hashSetNamesInUse=new HashSet<string>(listInUse.Select(x => GetKeyContainerName(x)).Where(x => x!=null),
				StringComparer.OrdinalIgnoreCase);
			HashSet<string> hashSetNamesDeleted=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			List<string> listErrors=new List<string>();
			for(int i=0;i<listX509Certificate2s.Count;i++) {
				if(listNames[i]!=null && (hashSetNamesInUse.Contains(listNames[i]) || hashSetNamesDeleted.Contains(listNames[i]))) {
					listErrors.Add("");//In use by a certificate that stays, or deleted already for another certificate of the list.
					continue;
				}
				string error=TryDeletePrivateKey(listX509Certificate2s[i]);
				if(error=="" && listNames[i]!=null) {
					hashSetNamesDeleted.Add(listNames[i]);
				}
				listErrors.Add(error);
			}
			return listErrors;
		}

		///<summary>Deletes the private key of the certificate from the key set it lives in: a CAPI key container (RSA) or a CNG key (RSA or ECDSA),
		///as DirectCertificateImport.TryDeletePrivateKey does. A CAPI key is deleted through CAPI (GetCapiRsaPrivateKey): .NET Framework's
		///GetRSAPrivateKey opens it through CNG, as an RSACng whose Key.Delete returns without deleting the CAPI container. The whole container
		///goes, also for another certificate that uses the same key, so the callers ask DeleteKeysNotInUse first. "" when deleted (or there was
		///none), else why not. Never throws: anything but a critical exception (IsCriticalException) is a key not deleted.</summary>
		internal static string TryDeletePrivateKey(X509Certificate2 x509Certificate2) {
			try {
				if(!x509Certificate2.HasPrivateKey) {
					return "";
				}
				ActionBeforeKeyDeleteForTests?.Invoke(x509Certificate2);
				RSACryptoServiceProvider rsaCryptoServiceProviderCapi=GetCapiRsaPrivateKey(x509Certificate2);
				if(rsaCryptoServiceProviderCapi!=null) {
					//Clear then deletes the key container, in the key set the certificate names for its key (user or machine).
					rsaCryptoServiceProviderCapi.PersistKeyInCsp=false;
					rsaCryptoServiceProviderCapi.Clear();
					return "";
				}
				using(RSA rsa=x509Certificate2.GetRSAPrivateKey()) {
					if(rsa is RSACng) {
						((RSACng)rsa).Key.Delete();
						return "";
					}
					if(rsa is RSACryptoServiceProvider) {//How .NET (not .NET Framework) gives a CAPI key.
						RSACryptoServiceProvider rsaCryptoServiceProvider=(RSACryptoServiceProvider)rsa;
						rsaCryptoServiceProvider.PersistKeyInCsp=false;//Clear then deletes the key container.
						rsaCryptoServiceProvider.Clear();
						return "";
					}
					if(rsa!=null) {
						return "";
					}
				}
				using(ECDsa ecdsa=x509Certificate2.GetECDsaPrivateKey()) {
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

		///<summary>The certificate's RSA private key when Windows keeps it in a CAPI key container, else null (a CNG key, an ECDSA key, no key, or
		///one that does not open: the CNG path then says why). A .pfx that names no CNG provider (OpenSSL writes none) or a CAPI provider is
		///imported into a CAPI container, and .NET Framework's GetRSAPrivateKey opens that through CNG: an RSACng whose Key.Delete leaves the CAPI
		///container, and whose Key.UniqueName is no file of the CNG folder (DIRECT-CERT, docs/port-notes/DIRECT-CERT.md). The key object comes
		///with the certificate's own key provider information, so it opens the container in the key set the key lives in. The certificate keeps
		///the object (X509Certificate2.PrivateKey), so it is not disposed here. Never throws: anything but a critical exception
		///(IsCriticalException) gives null, as in DirectCertificateImport's copy.</summary>
		private static RSACryptoServiceProvider GetCapiRsaPrivateKey(X509Certificate2 x509Certificate2) {
			try {
				return x509Certificate2.PrivateKey as RSACryptoServiceProvider;
			}
			catch(Exception ex) when(!IsCriticalException(ex)) {
				return null;//.NET Framework: "Invalid provider type specified" for a CNG key, NotSupportedException for an ECDSA one.
			}
		}

		///<summary>The key container of the certificate's private key: its kind and provider, user or machine, and unique name (CngKey.UniqueName
		///of a CNG key, CspKeyContainerInfo.UniqueKeyContainerName of a CAPI key), the same for every certificate that uses the one key, as
		///DirectCertificateImport.GetKeyContainerName gives it. Null without a key, or for a key that does not open. Never throws: anything but a
		///critical exception (IsCriticalException) gives null.</summary>
		internal static string GetKeyContainerName(X509Certificate2 x509Certificate2) {
			try {
				if(!x509Certificate2.HasPrivateKey) {
					return null;
				}
				RSACryptoServiceProvider rsaCryptoServiceProviderCapi=GetCapiRsaPrivateKey(x509Certificate2);
				if(rsaCryptoServiceProviderCapi!=null) {
					return GetCapiKeyContainerName(rsaCryptoServiceProviderCapi.CspKeyContainerInfo);
				}
				using(RSA rsa=x509Certificate2.GetRSAPrivateKey()) {
					if(rsa is RSACng) {
						using(CngKey cngKey=((RSACng)rsa).Key) {
							return GetCngKeyContainerName(cngKey);
						}
					}
					if(rsa is RSACryptoServiceProvider) {
						return GetCapiKeyContainerName(((RSACryptoServiceProvider)rsa).CspKeyContainerInfo);
					}
					if(rsa!=null) {
						return null;
					}
				}
				using(ECDsa ecdsa=x509Certificate2.GetECDsaPrivateKey()) {
					if(ecdsa is ECDsaCng) {
						using(CngKey cngKey=((ECDsaCng)ecdsa).Key) {
							return GetCngKeyContainerName(cngKey);
						}
					}
				}
				return null;
			}
			catch(Exception ex) when(!IsCriticalException(ex)) {
				return null;
			}
		}

		private static string GetCapiKeyContainerName(CspKeyContainerInfo cspKeyContainerInfo) {
			return "CAPI|"+cspKeyContainerInfo.ProviderName+"|"+(cspKeyContainerInfo.MachineKeyStore ? "machine" : "user")+"|"
				+cspKeyContainerInfo.UniqueKeyContainerName;
		}

		private static string GetCngKeyContainerName(CngKey cngKey) {
			if(string.IsNullOrEmpty(cngKey.UniqueName)) {
				return null;//An ephemeral key, in memory only: no container to share.
			}
			return "CNG|"+cngKey.Provider.Provider+"|"+(cngKey.IsMachineKey ? "machine" : "user")+"|"+cngKey.UniqueName;
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
			using(X509Store x509Store=new X509Store(StoreName.My,StoreLocation.LocalMachine)) {
				x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
				X509Certificate2Collection x509Certificate2Collection=x509Store.Certificates.Find(X509FindType.FindByThumbprint,thumbprint,false);
				return x509Certificate2Collection.Count==0 ? null : x509Certificate2Collection[0];
			}
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
			X509EnhancedKeyUsageExtension x509EnhancedKeyUsageExtension=x509Certificate2.Extensions.OfType<X509EnhancedKeyUsageExtension>()
				.FirstOrDefault();
			if(x509EnhancedKeyUsageExtension!=null
				&& !x509EnhancedKeyUsageExtension.EnhancedKeyUsages.Cast<Oid>().Any(x => x.Value==OID_CLIENT_AUTHENTICATION))
			{
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The certificate "+Describe(x509Certificate2)+" ("+source+") is not for "
					+"client authentication (its Extended Key Usage has no clientAuth).");
			}
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

		private static X509Certificate2 GetClientCertificate(X509Certificate2Collection x509Certificate2Collection,string pfxPath) {
			X509Certificate2 x509Certificate2=x509Certificate2Collection.Cast<X509Certificate2>().FirstOrDefault(x => x.HasPrivateKey);
			if(x509Certificate2==null) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The client certificate file "+pfxPath+" holds no certificate with a "
					+"private key.");
			}
			CheckClientCertificate(x509Certificate2,pfxPath);
			return x509Certificate2;
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
		///has a UniqueName that is no file of the CNG folder. Null without a key or for another kind of key. Throws CryptographicException for a
		///key that does not open.</summary>
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
			using(RSA rsa=x509Certificate2.GetRSAPrivateKey()) {
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
			using(ECDsa ecdsa=x509Certificate2.GetECDsaPrivateKey()) {
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
		///(TryGetPrivateKeyFilePath). Once the certificate is out of the store, certlm.msc no longer shows the key.</summary>
		private static string GetKeyLeftText(string pathKey) {
			if(string.IsNullOrEmpty(pathKey)) {
				return " The key is left on this computer; its file could not be named.";
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
