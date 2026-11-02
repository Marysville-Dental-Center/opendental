// The client certificate in the computer's certificate store (LocalMachine\My), its private key not exportable.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace OpenPmsDentalDbSettings {
	///<summary>The client certificate (owner decision Q2): one shared certificate on every workstation, or one per computer later, in
	///LocalMachine\My, read by MySqlConnector by thumbprint. A .pfx is imported with MachineKeySet and PersistKeySet and never Exportable, and
	///BUILTIN\Users get read access to its private key, because the program runs as the signed-in user (docs/specs/db-connection-security.md
	///section 4.3).</summary>
	public static class ClientCertificateStore {
		///<summary>Extended Key Usage OID of TLS client authentication.</summary>
		private const string OID_CLIENT_AUTHENTICATION="1.3.6.1.5.5.7.3.2";

		///<summary>Opens the .pfx without storing its key anywhere (EphemeralKeySet) and returns its certificate with a private key, to check the
		///file and its password before anything is imported. Throws DbSettingsException (exit code 2).</summary>
		public static X509Certificate2 LoadPfxForCheck(string pfxPath,string password) {
			X509Certificate2Collection x509Certificate2Collection=ImportCollection(pfxPath,password,X509KeyStorageFlags.EphemeralKeySet);
			return GetClientCertificate(x509Certificate2Collection,pfxPath);
		}

		///<summary>Imports the client certificate of the .pfx into LocalMachine\My (private key not exportable) and grants BUILTIN\Users read
		///access to its private key. Returns its thumbprint; warning is "" or says what the program's users may lack. Needs administrator rights.
		///Throws DbSettingsException (exit code 2 for the file, 3 for the store).</summary>
		public static string ImportPfx(string pfxPath,string password,out string warning) {
			X509Certificate2Collection x509Certificate2Collection=ImportCollection(pfxPath,password,
				X509KeyStorageFlags.MachineKeySet|X509KeyStorageFlags.PersistKeySet);
			X509Certificate2 x509Certificate2=GetClientCertificate(x509Certificate2Collection,pfxPath);
			try {
				using(X509Store x509Store=new X509Store(StoreName.My,StoreLocation.LocalMachine)) {
					x509Store.Open(OpenFlags.ReadWrite);
					x509Store.Add(x509Certificate2);
				}
			}
			catch(Exception ex) when(ex is CryptographicException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_WRITE_FAILED,"The client certificate could not be added to the computer's "
					+"certificate store (LocalMachine\\My): "+ex.Message);
			}
			warning="";
			try {
				GrantUsersReadToPrivateKey(x509Certificate2);
			}
			catch(Exception ex) {
				warning="The client certificate was imported, but the users of this computer may not be able to use its private key: "+ex.Message
					+" In certlm.msc, Personal > Certificates, right-click it, All Tasks > Manage Private Keys, and give Users Read.";
			}
			return x509Certificate2.Thumbprint;
		}

		///<summary>Removes the certificate with this thumbprint from the store (LocalMachine\My; the tests use CurrentUser\My) and deletes its
		///private key, so that no key that BUILTIN\Users may read is left behind. Only for the client certificate this tool imported itself and
		///the saved settings no longer use (DbSettingsRunner.PlanImportedCertificate, owner decision of 2026-11-02): a certificate the tool did not
		///import is never removed. Returns "" when it is gone (or was not there), else a warning that says to remove it by hand. Never throws.
		///</summary>
		public static string RemoveImported(string thumbprint,StoreLocation storeLocation=StoreLocation.LocalMachine) {
			if(string.IsNullOrEmpty(thumbprint)) {
				return "";
			}
			try {
				using(X509Store x509Store=new X509Store(StoreName.My,storeLocation)) {
					x509Store.Open(OpenFlags.ReadWrite|OpenFlags.OpenExistingOnly);
					foreach(X509Certificate2 x509Certificate2 in x509Store.Certificates.Find(X509FindType.FindByThumbprint,thumbprint,false)) {
						x509Store.Remove(x509Certificate2);
						DeletePrivateKey(x509Certificate2);
					}
				}
				return "";
			}
			catch(Exception ex) when(ex is CryptographicException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException
				|| ex is IOException)
			{
				return "The client certificate "+thumbprint+" that this tool imported before could not be removed from the computer's certificate "
					+"store: "+ex.Message+" Remove it in certlm.msc, Personal > Certificates.";
			}
		}

		///<summary>Deletes the private key of the certificate from its key store: a CNG key (RSA or ECDSA) or a CAPI key container. Throws
		///CryptographicException.</summary>
		private static void DeletePrivateKey(X509Certificate2 x509Certificate2) {
			if(!x509Certificate2.HasPrivateKey) {
				return;
			}
			RSA rsa=x509Certificate2.GetRSAPrivateKey();
			if(rsa is RSACng) {
				((RSACng)rsa).Key.Delete();
				return;
			}
			if(rsa is RSACryptoServiceProvider) {
				RSACryptoServiceProvider rsaCryptoServiceProvider=(RSACryptoServiceProvider)rsa;
				rsaCryptoServiceProvider.PersistKeyInCsp=false;//Clear then deletes the key container.
				rsaCryptoServiceProvider.Clear();
				return;
			}
			ECDsa ecdsa=x509Certificate2.GetECDsaPrivateKey();
			if(ecdsa is ECDsaCng) {
				((ECDsaCng)ecdsa).Key.Delete();
			}
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
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The client certificate file "+pfxPath+" cannot be opened: the password "
					+"is wrong, or it is not a .pfx file.");
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

		///<summary>Gives BUILTIN\Users read access to the private key file of the certificate: a CNG key under %ProgramData%\Microsoft\Crypto\Keys
		///or a CAPI key under %ProgramData%\Microsoft\Crypto\RSA\MachineKeys. Throws when the file is not found or its rights cannot be changed.
		///</summary>
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

		private static string GetPrivateKeyFilePath(X509Certificate2 x509Certificate2) {
			string folderCrypto=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Microsoft","Crypto");
			string uniqueNameCng=null;
			RSA rsa=x509Certificate2.GetRSAPrivateKey();
			if(rsa is RSACng) {
				uniqueNameCng=((RSACng)rsa).Key.UniqueName;
			}
			else if(rsa is RSACryptoServiceProvider) {
				return Path.Combine(folderCrypto,"RSA","MachineKeys",((RSACryptoServiceProvider)rsa).CspKeyContainerInfo.UniqueKeyContainerName);
			}
			else {
				ECDsa ecdsa=x509Certificate2.GetECDsaPrivateKey();
				if(ecdsa is ECDsaCng) {
					uniqueNameCng=((ECDsaCng)ecdsa).Key.UniqueName;
				}
			}
			if(uniqueNameCng==null) {
				return null;
			}
			return Path.Combine(folderCrypto,"Keys",uniqueNameCng);
		}
	}
}
