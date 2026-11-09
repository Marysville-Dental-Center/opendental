// Direct secure messaging (open point O-13 of the .NET 10 plan, owner answer of 2026-11-08: "Yes, load Direct provider's certificate from a
// file"): the office's own certificate and its Direct provider's trust anchor, imported from the files the provider (HISP) issued into the
// stores the program reads (NHINDPrivate and NHINDAnchors, EmailMessages), in place of the registration with Open Dental's servers. The rules
// are in docs/port-notes/DIRECT-CERT.md. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>What a certificate names, by the program's own rule (EmailCertificates.IsForAddress and IsForDomain).</summary>
	public enum DirectCertificateBinding {
		///<summary>Neither the address nor its domain.</summary>
		None,
		///<summary>An address-level certificate: the e-mail address itself.</summary>
		Address,
		///<summary>A domain-level certificate: the domain of the address, which Direct allows for every address of the domain.</summary>
		Domain,
	}

	///<summary>What a certificate's key is for (DirectCertificateImport.GetPurpose). Some Direct providers issue one certificate for both, others
	///one for signing and one for encryption for the same address. Not stored anywhere.</summary>
	[Flags]
	public enum DirectCertificatePurpose {
		///<summary>Neither: its Extended Key Usage leaves out e-mail protection, or its Key Usage names neither.</summary>
		None=0,
		///<summary>Signing: Key Usage digitalSignature or nonRepudiation.</summary>
		Signing=1,
		///<summary>Encryption: Key Usage keyEncipherment, or keyAgreement (how an elliptic curve key encrypts).</summary>
		Encryption=2,
	}

	///<summary>Why a certificate of the store for the address or its domain stays when the file is imported (DirectCertificateResult.ListDetailsKept).
	///</summary>
	public enum DirectCertificateKeptReason {
		///<summary>Not a kept certificate: one of the file's, or one the import replaces.</summary>
		None,
		///<summary>The other kind: a domain certificate when the file's are for the address, or an address certificate when they are for the domain.</summary>
		OtherKind,
		///<summary>No certificate of the file of its kind has its purpose: a signing certificate next to the file's encryption certificate.</summary>
		OtherPurpose,
		///<summary>A certificate of the file has its purpose, but this one is valid as long as or longer than every such certificate of the file.</summary>
		ValidAsLongOrLonger,
	}

	///<summary>What the certificate window shows of one certificate. Holds no key and no password.</summary>
	public class DirectCertificateDetails {
		public string Subject="";
		public string Issuer="";
		public string SerialNumber="";
		public string Thumbprint="";
		///<summary>Local time, as Windows shows it.</summary>
		public DateTime DateTimeNotBefore;
		///<summary>Local time, as Windows shows it.</summary>
		public DateTime DateTimeNotAfter;
		///<summary>The certificate says it has a private key. Whether the key can be used is IsKeyUsable.</summary>
		public bool HasPrivateKey;
		///<summary>The private key opens on this computer and signs or decrypts a test value (DirectCertificateImport.IsKeyUsable); false without a key.</summary>
		public bool IsKeyUsable;
		public DirectCertificatePurpose Purpose;
		public DirectCertificateBinding Binding;
		///<summary>The address or domain the certificate names; "" for None.</summary>
		public string BoundName="";
		///<summary>For a certificate of the store that the import keeps: why. None for every other.</summary>
		public DirectCertificateKeptReason KeptReason;

		public static DirectCertificateDetails Create(X509Certificate2 x509Certificate2,DirectCertificateBinding binding,string boundName) {
			DirectCertificateDetails directCertificateDetails=new DirectCertificateDetails();
			directCertificateDetails.Subject=x509Certificate2.Subject;
			directCertificateDetails.Issuer=x509Certificate2.Issuer;
			directCertificateDetails.SerialNumber=x509Certificate2.SerialNumber;
			directCertificateDetails.Thumbprint=x509Certificate2.Thumbprint;
			directCertificateDetails.DateTimeNotBefore=x509Certificate2.NotBefore;
			directCertificateDetails.DateTimeNotAfter=x509Certificate2.NotAfter;
			directCertificateDetails.HasPrivateKey=x509Certificate2.HasPrivateKey;
			directCertificateDetails.IsKeyUsable=DirectCertificateImport.IsKeyUsable(x509Certificate2);
			directCertificateDetails.Purpose=DirectCertificateImport.GetPurpose(x509Certificate2);
			directCertificateDetails.Binding=binding;
			directCertificateDetails.BoundName=(binding==DirectCertificateBinding.None) ? "" : boundName;
			return directCertificateDetails;
		}

		///<summary>The certificate in a few lines: subject, issuer, serial number, validity dates, purpose, what it names, its private key, thumbprint.</summary>
		public string Describe() {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine(Lans.g("DirectCertificateImport","Subject")+": "+Subject);
			stringBuilder.AppendLine(Lans.g("DirectCertificateImport","Issuer")+": "+Issuer);
			stringBuilder.AppendLine(Lans.g("DirectCertificateImport","Serial number")+": "+SerialNumber);
			stringBuilder.AppendLine(Lans.g("DirectCertificateImport","Valid from")+" "+DirectCertificateImport.FormatDate(DateTimeNotBefore)+" "
				+Lans.g("DirectCertificateImport","to")+" "+DirectCertificateImport.FormatDate(DateTimeNotAfter));
			stringBuilder.AppendLine(Lans.g("DirectCertificateImport","Purpose")+": "+DirectCertificateImport.GetPurposeText(Purpose));
			if(Binding==DirectCertificateBinding.Address) {
				stringBuilder.AppendLine(Lans.g("DirectCertificateImport","For the address")+" "+BoundName);
			}
			else if(Binding==DirectCertificateBinding.Domain) {
				stringBuilder.AppendLine(Lans.g("DirectCertificateImport","For every address of the domain")+" "+BoundName);
			}
			string key=Lans.g("DirectCertificateImport","no");
			if(HasPrivateKey) {
				key=IsKeyUsable ? Lans.g("DirectCertificateImport","yes") : Lans.g("DirectCertificateImport","yes, but it cannot be used on this computer");
			}
			stringBuilder.AppendLine(Lans.g("DirectCertificateImport","Private key")+": "+key);
			stringBuilder.Append(Lans.g("DirectCertificateImport","Thumbprint")+": "+Thumbprint);
			return stringBuilder.ToString();
		}

		///<summary>One line: subject, purpose, valid from and to, a missing or unusable key, and the thumbprint.</summary>
		public string DescribeShort() {
			string key="";
			if(!HasPrivateKey) {
				key="; "+Lans.g("DirectCertificateImport","no private key");
			}
			else if(!IsKeyUsable) {
				key="; "+Lans.g("DirectCertificateImport","private key not usable on this computer");
			}
			return Subject+"; "+DirectCertificateImport.GetPurposeText(Purpose)+"; "+Lans.g("DirectCertificateImport","valid from")+" "
				+DirectCertificateImport.FormatDate(DateTimeNotBefore)+" "+Lans.g("DirectCertificateImport","to")+" "+DirectCertificateImport.FormatDate(DateTimeNotAfter)
				+key+"; "+Thumbprint;
		}
	}

	///<summary>The result of checking or importing a certificate file.</summary>
	public class DirectCertificateResult {
		///<summary>"" when the file can be imported (a check) or was imported (an import); else why not, for the user.</summary>
		public string Error="";
		///<summary>Something the user should know after a successful import, or "".</summary>
		public string Warning="";
		///<summary>The certificates the file adds (a check) or added (an import) to the store.</summary>
		public List<DirectCertificateDetails> ListDetails=new List<DirectCertificateDetails>();
		///<summary>The certificates of the file that the store already holds (with a private key that can be used, for the private store): not
		///added again.</summary>
		public List<DirectCertificateDetails> ListDetailsInStore=new List<DirectCertificateDetails>();
		///<summary>The certificates of the private store that the import replaces (a check) or replaced (an import): of the same kind (address or
		///domain) and name as a certificate the file adds, with a purpose of it (signing or encryption), and expiring before it.</summary>
		public List<DirectCertificateDetails> ListDetailsReplaced=new List<DirectCertificateDetails>();
		///<summary>The other certificates of the private store for the address or its domain: they stay, each with its KeptReason.</summary>
		public List<DirectCertificateDetails> ListDetailsKept=new List<DirectCertificateDetails>();
		///<summary>True once an import has added the certificates to the store.</summary>
		public bool IsImported;
	}

	///<summary>The import of the certificate window (FormEmailCertRegister, the Certificate button of Edit Email Address). The office's Direct
	///provider issues the address's certificate with its private key in a .pfx or .p12 file with a password; it goes into the store of private
	///certificates (EmailMessages.CertStoreNamePrivate, NHINDPrivate on the Local Computer) with the key never exportable, where it signs e-mail
	///and Direct messages and decrypts received Direct messages. The provider's trust anchor (a public certificate in a .cer, .crt, .der or .pem
	///file) goes into the trust anchors (EmailMessages.CertStoreNameAnchor, NHINDAnchors). The stores are the ones EmailMessages opens, so the
	///tests point them at stores of the current user (tests/LibraryUpdateTests, DirectTestStores). Nothing is sent anywhere.</summary>
	public static class DirectCertificateImport {
		///<summary>The friendly name of every certificate this import puts into the private store. A replaced certificate with this name has its
		///private key deleted with it (the import put the key there and gave the computer's users read access to it); the key of one put there
		///another way stays.</summary>
		public const string FRIENDLY_NAME_IMPORTED="Open PMS - Dental Direct certificate";
		///<summary>The file types of the address's certificate: PKCS#12, the certificate with its private key.</summary>
		public static readonly string[] ArrayExtensionsPrivate=new string[] { ".pfx",".p12" };
		///<summary>The file types of a trust anchor: a public certificate, binary (DER) or Base64 (PEM).</summary>
		public static readonly string[] ArrayExtensionsAnchor=new string[] { ".cer",".crt",".der",".pem" };
		private const string PEM_BEGIN="-----BEGIN CERTIFICATE-----";
		private const string PEM_END="-----END CERTIFICATE-----";
		///<summary>The Key Usage extension (RFC 5280, 4.2.1.3).</summary>
		private const string OID_KEY_USAGE="2.5.29.15";
		///<summary>The Extended Key Usage extension (RFC 5280, 4.2.1.12).</summary>
		private const string OID_EXTENDED_KEY_USAGE="2.5.29.37";
		///<summary>The e-mail protection purpose of the Extended Key Usage, which S/MIME and Direct certificates carry.</summary>
		private const string OID_EMAIL_PROTECTION="1.3.6.1.5.5.7.3.4";
		///<summary>anyExtendedKeyUsage: every purpose.</summary>
		private const string OID_ANY_EXTENDED_KEY_USAGE="2.5.29.37.0";
		///<summary>The tests only: called right before the import reads the file again with PersistKeySet, which stores its keys; throwing a
		///CryptographicException there is that import failing. Null in the program.</summary>
		internal static Action ActionBeforeKeysStoredForTests;
		///<summary>The tests only: called with the file's certificates right after the import stored their keys (PersistKeySet), before anything
		///else of the import. Null in the program.</summary>
		internal static Action<X509Certificate2Collection> ActionAfterKeysStoredForTests;
		///<summary>The tests only: called with each certificate right before it is added to the store; throwing stops the import there, as a store
		///that refuses would. Null in the program.</summary>
		internal static Action<X509Certificate2> ActionBeforeStoreAddForTests;
		///<summary>The tests only: called with each replaced certificate right before it is removed from the store, after the new ones were added;
		///a CryptographicException there is a store that refuses to remove it, anything else stops the import there. Null in the program.</summary>
		internal static Action<X509Certificate2> ActionBeforeStoreRemoveForTests;

		#region The address's certificate
		///<summary>Reads the .pfx or .p12 file with its password, without storing any key (EphemeralKeySet), and checks it for the address
		///(docs/port-notes/DIRECT-CERT.md): ListDetails are the certificates an import adds, ListDetailsReplaced the ones of the store it replaces
		///(the same kind, name and purpose, expiring before the file's), ListDetailsKept the others of the store for the address or its domain.
		///Error says why the file can't be imported. Never throws.</summary>
		public static DirectCertificateResult CheckPrivateFile(string emailAddress,string filePath,string password,DateTime dateTimeUtcNow) {
			Meth.NoCheckMiddleTierRole();
			DirectCertificateResult directCertificateResult=new DirectCertificateResult();
			byte[] byteArrayFile;
			directCertificateResult.Error=ReadFile(filePath,ArrayExtensionsPrivate,isPrivate:true,out byteArrayFile);
			if(directCertificateResult.Error!="") {
				return directCertificateResult;
			}
			CheckPrivate(emailAddress,byteArrayFile,password,dateTimeUtcNow,directCertificateResult);
			return directCertificateResult;
		}

		///<summary>Checks the file again (CheckPrivateFile) and imports its certificates for the address into the store of private certificates,
		///their private keys never exportable, then removes the certificates of the store it replaces (ListDetailsReplaced). The others of the
		///store stay (ListDetailsKept, GetKeptText). listThumbprintsReplaceAllowed: the certificates the user agreed to replace (the window asks
		///first, GetReplaceQuestion); when the store holds one to replace that is not in it, nothing is imported and Error says so. A failure
		///partway takes back what the import wrote, and Error says what could not be. In production (the Local Computer store) the computer's
		///users get read access to the new private keys; Warning says so if that failed. Never throws.</summary>
		public static DirectCertificateResult ImportPrivateFile(string emailAddress,string filePath,string password,List<string> listThumbprintsReplaceAllowed,
			DateTime dateTimeUtcNow)
		{
			Meth.NoCheckMiddleTierRole();
			DirectCertificateResult directCertificateResult=new DirectCertificateResult();
			byte[] byteArrayFile;
			directCertificateResult.Error=ReadFile(filePath,ArrayExtensionsPrivate,isPrivate:true,out byteArrayFile);
			if(directCertificateResult.Error!="") {
				return directCertificateResult;
			}
			CheckPrivate(emailAddress,byteArrayFile,password,dateTimeUtcNow,directCertificateResult);
			if(directCertificateResult.Error!="") {
				return directCertificateResult;
			}
			listThumbprintsReplaceAllowed=listThumbprintsReplaceAllowed??new List<string>();
			if(directCertificateResult.ListDetailsReplaced.Any(x => !listThumbprintsReplaceAllowed.Contains(x.Thumbprint,StringComparer.OrdinalIgnoreCase))) {
				directCertificateResult.Error=Lans.g("DirectCertificateImport","The store has a certificate for this address or its domain that was not "
					+"agreed to be replaced. Nothing was imported. Check the file again.");
				return directCertificateResult;
			}
			X509Store x509Store=new X509Store(EmailMessages.CertStoreNamePrivate,EmailMessages.CertStoreLocation);
			try {
				string errorOpen=OpenForWriting(x509Store);
				if(errorOpen!="") {
					directCertificateResult.Error=errorOpen;
					return directCertificateResult;
				}
				AddPrivate(x509Store,byteArrayFile,password,directCertificateResult);
			}
			finally {
				x509Store.Dispose();
			}
			return directCertificateResult;
		}

		///<summary>The certificates of the store of private certificates for the address (address-level) or its domain (domain-level), with or
		///without their keys, valid or not, for the window. None when the store does not exist yet. Throws if the store can't be read.</summary>
		public static List<DirectCertificateDetails> GetPrivateCertificates(string emailAddress) {
			Meth.NoCheckMiddleTierRole();
			List<DirectCertificateDetails> listDetails=new List<DirectCertificateDetails>();
			string address=EmailMessages.GetAddressSimple(emailAddress);
			if(GetAddressError(address)!="") {
				return listDetails;
			}
			List<X509Certificate2> listStore=ReadStore(EmailMessages.CertStoreNamePrivate);
			try {
				foreach(X509Certificate2 x509Certificate2 in listStore) {
					DirectCertificateBinding binding=GetBinding(x509Certificate2,address);
					if(binding!=DirectCertificateBinding.None) {
						listDetails.Add(DirectCertificateDetails.Create(x509Certificate2,binding,GetBoundName(address,binding)));
					}
				}
			}
			finally {
				DisposeAll(listStore);
			}
			return listDetails;
		}

		///<summary>What the certificate names for the address, by the program's own rule: the address (EmailCertificates.IsForAddress), else its
		///domain (IsForDomain), else None.</summary>
		public static DirectCertificateBinding GetBinding(X509Certificate2 x509Certificate2,string emailAddress) {
			Meth.NoCheckMiddleTierRole();
			string address=EmailMessages.GetAddressSimple(emailAddress);
			if(EmailCertificates.IsForAddress(x509Certificate2,address)) {
				return DirectCertificateBinding.Address;
			}
			if(EmailCertificates.IsForDomain(x509Certificate2,GetDomain(address))) {
				return DirectCertificateBinding.Domain;
			}
			return DirectCertificateBinding.None;
		}

		///<summary>What the certificate's key is for, by its extensions, found by OID: the Key Usage decides signing (digitalSignature,
		///nonRepudiation) and encryption (keyEncipherment, or keyAgreement for an elliptic curve key); without a Key Usage the key may do both. An
		///Extended Key Usage that names neither e-mail protection nor every purpose leaves it neither. A certificate for both counts as both, and
		///an extension that can't be read counts as neither.</summary>
		public static DirectCertificatePurpose GetPurpose(X509Certificate2 x509Certificate2) {
			Meth.NoCheckMiddleTierRole();
			DirectCertificatePurpose purpose=DirectCertificatePurpose.Signing|DirectCertificatePurpose.Encryption;
			for(int i=0;i<x509Certificate2.Extensions.Count;i++) {
				X509Extension x509Extension=x509Certificate2.Extensions[i];
				try {
					if(x509Extension.Oid.Value==OID_KEY_USAGE) {
						X509KeyUsageFlags x509KeyUsageFlags=new X509KeyUsageExtension(x509Extension,x509Extension.Critical).KeyUsages;
						DirectCertificatePurpose purposeKeyUsage=DirectCertificatePurpose.None;
						if((x509KeyUsageFlags&(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.NonRepudiation))!=0) {
							purposeKeyUsage|=DirectCertificatePurpose.Signing;
						}
						if((x509KeyUsageFlags&(X509KeyUsageFlags.KeyEncipherment|X509KeyUsageFlags.KeyAgreement))!=0) {
							purposeKeyUsage|=DirectCertificatePurpose.Encryption;
						}
						purpose&=purposeKeyUsage;
					}
					else if(x509Extension.Oid.Value==OID_EXTENDED_KEY_USAGE) {
						OidCollection oidCollection=new X509EnhancedKeyUsageExtension(x509Extension,x509Extension.Critical).EnhancedKeyUsages;
						if(!oidCollection.Cast<Oid>().Any(x => x.Value==OID_EMAIL_PROTECTION || x.Value==OID_ANY_EXTENDED_KEY_USAGE)) {
							purpose=DirectCertificatePurpose.None;
						}
					}
				}
				catch(CryptographicException) {
					purpose=DirectCertificatePurpose.None;//A damaged extension: for nothing.
				}
			}
			return purpose;
		}

		///<summary>The purpose in a few words, for the window.</summary>
		public static string GetPurposeText(DirectCertificatePurpose purpose) {
			Meth.NoCheckMiddleTierRole();
			if(purpose==(DirectCertificatePurpose.Signing|DirectCertificatePurpose.Encryption)) {
				return Lans.g("DirectCertificateImport","signing and encryption");
			}
			if(purpose==DirectCertificatePurpose.Signing) {
				return Lans.g("DirectCertificateImport","signing");
			}
			if(purpose==DirectCertificatePurpose.Encryption) {
				return Lans.g("DirectCertificateImport","encryption");
			}
			return Lans.g("DirectCertificateImport","neither signing nor encryption of e-mail");
		}

		///<summary>True when the certificate's private key can be used on this computer: it opens (an RSA or ECDSA key) and signs a test digest, or,
		///for an RSA key that does not sign, decrypts what its public key encrypted. False without a key, or with one that does not open (deleted,
		///no read access, another user's). HasPrivateKey alone only says that the certificate names a key. Never throws: anything but a critical
		///exception (IsCriticalException) means not usable.</summary>
		public static bool IsKeyUsable(X509Certificate2 x509Certificate2) {
			Meth.NoCheckMiddleTierRole();
			try {
				if(!x509Certificate2.HasPrivateKey) {
					return false;
				}
				byte[] byteArrayValue=Encoding.ASCII.GetBytes("Open PMS - Dental Direct key check");
				byte[] byteArrayDigest;
				using(SHA256 sha256=SHA256.Create()) {
					byteArrayDigest=sha256.ComputeHash(byteArrayValue);
				}
				using(RSA rsa=x509Certificate2.GetRSAPrivateKey()) {
					if(rsa!=null) {
						return CanSign(rsa,byteArrayValue,byteArrayDigest) || CanDecrypt(rsa,x509Certificate2,byteArrayValue);
					}
				}
				using(ECDsa ecdsa=x509Certificate2.GetECDsaPrivateKey()) {
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

		///<summary>True for the exceptions that a method of this import documented "Never throws" lets through: OutOfMemoryException,
		///StackOverflowException and ThreadAbortException, after which the program can't go on safely. Every other one is an answer (not usable,
		///not deleted).</summary>
		internal static bool IsCriticalException(Exception ex) {
			return ex is OutOfMemoryException || ex is StackOverflowException || ex is ThreadAbortException;
		}

		///<summary>The window's question before an import replaces certificates of the store (a check's ListDetailsReplaced), "" when it replaces
		///none. It lists both sides, the file's certificates and the store's it replaces (subject, purpose, valid from and to, thumbprint), warns
		///when a replaced one has a purpose none of the file's has (GetReplaceWarning), lists the ones kept (GetKeptText), and ends with "Choose No
		///to import nothing."</summary>
		public static string GetReplaceQuestion(DirectCertificateResult directCertificateResult) {
			Meth.NoCheckMiddleTierRole();
			if(directCertificateResult.ListDetailsReplaced.Count==0) {
				return "";
			}
			bool isOne=(directCertificateResult.ListDetailsReplaced.Count==1);
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(Lans.g("DirectCertificateImport","This computer's Direct certificate store has a certificate for this address or its "
				+"domain, for a purpose of a certificate in the file, that expires before it."));
			stringBuilder.Append("\r\n\r\n"+Lans.g("DirectCertificateImport","In the file")+":\r\n"
				+string.Join("\r\n",directCertificateResult.ListDetails.Select(x => x.DescribeShort())));
			stringBuilder.Append("\r\n\r\n"+Lans.g("DirectCertificateImport","Replaced on this computer")+":\r\n"
				+string.Join("\r\n",directCertificateResult.ListDetailsReplaced.Select(x => x.DescribeShort())));
			string warning=GetReplaceWarning(directCertificateResult);
			if(warning!="") {
				stringBuilder.Append("\r\n\r\n"+warning);
			}
			string kept=GetKeptText(directCertificateResult);
			if(kept!="") {
				stringBuilder.Append("\r\n\r\n"+kept);
			}
			stringBuilder.Append("\r\n\r\n");
			if(isOne) {
				stringBuilder.Append(Lans.g("DirectCertificateImport","Import the file and replace the certificate under Replaced on this computer? It is "
					+"removed from the store, and so is its private key if this window imported it. Messages encrypted to it can then be read here only after "
					+"importing its .pfx or .p12 file again. Choose No to import nothing."));
			}
			else {
				stringBuilder.Append(Lans.g("DirectCertificateImport","Import the file and replace the certificates under Replaced on this computer? They are "
					+"removed from the store, and so are their private keys if this window imported them. Messages encrypted to them can then be read here "
					+"only after importing their .pfx or .p12 files again. Choose No to import nothing."));
			}
			return stringBuilder.ToString();
		}

		///<summary>The warning of the question and of the window's details when a certificate the import replaces is also for a purpose that no
		///certificate of the file has (the store's signs and encrypts, the file's only signs), else "".</summary>
		public static string GetReplaceWarning(DirectCertificateResult directCertificateResult) {
			Meth.NoCheckMiddleTierRole();
			DirectCertificatePurpose purposeFile=DirectCertificatePurpose.None;
			for(int i=0;i<directCertificateResult.ListDetails.Count;i++) {
				purposeFile|=directCertificateResult.ListDetails[i].Purpose;
			}
			DirectCertificatePurpose purposeLost=DirectCertificatePurpose.None;
			for(int i=0;i<directCertificateResult.ListDetailsReplaced.Count;i++) {
				purposeLost|=(directCertificateResult.ListDetailsReplaced[i].Purpose & ~purposeFile);
			}
			if(purposeLost==DirectCertificatePurpose.None) {
				return "";
			}
			return Lans.g("DirectCertificateImport","No certificate in the file is for")+" "+GetPurposeText(purposeLost)+", "
				+Lans.g("DirectCertificateImport","which a replaced certificate is also for. Replace it only if your Direct provider told you to.");
		}

		///<summary>The certificates of the store for the address or its domain that the import keeps (ListDetailsKept), each with why, or "" when
		///there are none. The window shows it at Check, in the question and after the import.</summary>
		public static string GetKeptText(DirectCertificateResult directCertificateResult) {
			Meth.NoCheckMiddleTierRole();
			if(directCertificateResult.ListDetailsKept.Count==0) {
				return "";
			}
			return Lans.g("DirectCertificateImport","Kept on this computer")+":\r\n"
				+string.Join("\r\n",directCertificateResult.ListDetailsKept.Select(x => x.DescribeShort()+" ("+GetKeptReasonText(x)+")"));
		}

		private static string GetKeptReasonText(DirectCertificateDetails directCertificateDetails) {
			switch(directCertificateDetails.KeptReason) {
				case DirectCertificateKeptReason.OtherKind:
					if(directCertificateDetails.Binding==DirectCertificateBinding.Domain) {
						return Lans.g("DirectCertificateImport","for every address of the domain, which a certificate for one address does not replace");
					}
					return Lans.g("DirectCertificateImport","for this address only, which a domain certificate does not replace");
				case DirectCertificateKeptReason.OtherPurpose:
					return Lans.g("DirectCertificateImport","for another purpose than the certificates in the file");
				case DirectCertificateKeptReason.ValidAsLongOrLonger:
					return Lans.g("DirectCertificateImport","for the same purpose and valid as long as or longer than the one in the file, which is added next "
						+"to it");
			}
			return "";
		}

		///<summary>The checks of the file's bytes, which fill the result. The certificates of the file are disposed afterwards.</summary>
		private static void CheckPrivate(string emailAddress,byte[] byteArrayFile,string password,DateTime dateTimeUtcNow,
			DirectCertificateResult directCertificateResult)
		{
			string address=EmailMessages.GetAddressSimple(emailAddress);
			directCertificateResult.Error=GetAddressError(address);
			if(directCertificateResult.Error!="") {
				return;
			}
			X509Certificate2Collection x509Certificate2Collection=new X509Certificate2Collection();
			try {
				//EphemeralKeySet: the keys stay in memory only, nothing is written to a key store until the import.
				x509Certificate2Collection.Import(byteArrayFile,password??"",X509KeyStorageFlags.EphemeralKeySet);
			}
			catch(CryptographicException) {
				//Windows' text names no secret, but a wrong password is what it nearly always means.
				directCertificateResult.Error=Lans.g("DirectCertificateImport","The file could not be opened. Check the password, and that the file is "
					+"the .pfx or .p12 file your Direct provider issued.");
				return;
			}
			try {
				CheckPrivateCertificates(address,x509Certificate2Collection,dateTimeUtcNow,directCertificateResult);
			}
			finally {
				ResetAll(x509Certificate2Collection);
			}
		}

		private static void CheckPrivateCertificates(string address,X509Certificate2Collection x509Certificate2Collection,DateTime dateTimeUtcNow,
			DirectCertificateResult directCertificateResult)
		{
			string domain=GetDomain(address);
			if(x509Certificate2Collection.Count==0) {
				directCertificateResult.Error=Lans.g("DirectCertificateImport","The file holds no certificate.");
				return;
			}
			List<X509Certificate2> listNamed=x509Certificate2Collection.Cast<X509Certificate2>()
				.Where(x => GetBinding(x,address)!=DirectCertificateBinding.None).ToList();
			if(listNamed.Count==0) {
				directCertificateResult.Error=Lans.g("DirectCertificateImport","No certificate in the file is for")+" "+address+" "
					+Lans.g("DirectCertificateImport","or its domain")+" "+domain+". "
					+Lans.g("DirectCertificateImport","A Direct certificate names the address, or its domain, as its subject alternative name.")+"\r\n\r\n"
					+Lans.g("DirectCertificateImport","The file holds")+":\r\n"
					+string.Join("\r\n",x509Certificate2Collection.Cast<X509Certificate2>().Select(x => x.Subject));
				return;
			}
			List<X509Certificate2> listKeyed=listNamed.FindAll(x => x.HasPrivateKey);
			if(listKeyed.Count==0) {
				directCertificateResult.Error=Lans.g("DirectCertificateImport","The certificate for")+" "+address+" "
					+Lans.g("DirectCertificateImport","in the file has no private key. Ask your Direct provider for the .pfx or .p12 file that includes the "
					+"private key.")+"\r\n\r\n"+listNamed[0].Subject;
				return;
			}
			for(int i=0;i<listKeyed.Count;i++) {
				string problem=GetValidityProblem(listKeyed[i],dateTimeUtcNow);
				if(problem!="") {
					directCertificateResult.Error=problem;
					return;
				}
			}
			List<X509Certificate2> listStore;
			try {
				listStore=ReadStore(EmailMessages.CertStoreNamePrivate);
			}
			catch(Exception ex) when(IsStoreException(ex)) {
				directCertificateResult.Error=Lans.g("DirectCertificateImport","This computer's Direct certificate store could not be read")+": "+ex.Message;
				return;
			}
			try {
				CheckPrivateAgainstStore(address,listKeyed,listStore,directCertificateResult);
			}
			finally {
				DisposeAll(listStore);
			}
		}

		///<summary>Sorts the file's certificates (ListDetails, ListDetailsInStore) and the store's for the address or its domain (ListDetailsReplaced,
		///ListDetailsKept) by the replace rule of docs/port-notes/DIRECT-CERT.md.</summary>
		private static void CheckPrivateAgainstStore(string address,List<X509Certificate2> listKeyed,List<X509Certificate2> listStore,
			DirectCertificateResult directCertificateResult)
		{
			List<string> listThumbprintsFile=listKeyed.Select(x => x.Thumbprint).ToList();
			for(int i=0;i<listKeyed.Count;i++) {
				DirectCertificateBinding binding=GetBinding(listKeyed[i],address);
				DirectCertificateDetails directCertificateDetails=DirectCertificateDetails.Create(listKeyed[i],binding,GetBoundName(address,binding));
				//Already there only when its key can be used: a key that was deleted, or that this user can't open, is imported again.
				if(listStore.Any(x => x.Thumbprint==listKeyed[i].Thumbprint && IsKeyUsable(x))) {
					directCertificateResult.ListDetailsInStore.Add(directCertificateDetails);
					continue;
				}
				directCertificateResult.ListDetails.Add(directCertificateDetails);
			}
			if(directCertificateResult.ListDetails.Count==0) {
				directCertificateResult.Error=Lans.g("DirectCertificateImport","This certificate is already in this computer's Direct certificate store, with "
					+"its private key. Nothing to import.");
				return;
			}
			for(int i=0;i<listStore.Count;i++) {
				if(listThumbprintsFile.Contains(listStore[i].Thumbprint)) {
					//The same certificate as one of the file's: in the store with a usable key (stays as it is), or without one (the import's copy, with
					//its key, takes its place in the store).
					continue;
				}
				DirectCertificateBinding binding=GetBinding(listStore[i],address);
				if(binding==DirectCertificateBinding.None) {
					continue;//Another address's: not shown, never touched.
				}
				DirectCertificateDetails directCertificateDetailsStore=DirectCertificateDetails.Create(listStore[i],binding,GetBoundName(address,binding));
				List<DirectCertificateDetails> listSameKind=directCertificateResult.ListDetails.FindAll(x => x.Binding==binding);
				List<DirectCertificateDetails> listSamePurpose=listSameKind.FindAll(x => (x.Purpose & directCertificateDetailsStore.Purpose)!=0);
				if(listSamePurpose.Any(x => directCertificateDetailsStore.DateTimeNotAfter<x.DateTimeNotAfter)) {
					directCertificateResult.ListDetailsReplaced.Add(directCertificateDetailsStore);
					continue;
				}
				if(listSameKind.Count==0) {
					directCertificateDetailsStore.KeptReason=DirectCertificateKeptReason.OtherKind;
				}
				else if(listSamePurpose.Count==0) {
					directCertificateDetailsStore.KeptReason=DirectCertificateKeptReason.OtherPurpose;
				}
				else {
					directCertificateDetailsStore.KeptReason=DirectCertificateKeptReason.ValidAsLongOrLonger;
				}
				directCertificateResult.ListDetailsKept.Add(directCertificateDetailsStore);
			}
		}

		///<summary>Imports the checked file into the open store: the keys are written to the key store now (never exportable), the certificates
		///added with FRIENDLY_NAME_IMPORTED, then the replaced ones removed. A key of the file the import does not use is deleted again, unless a
		///certificate it adds or leaves in the store uses the same key (DeleteKeysNotInUse). A failure before the certificates are all added takes
		///back what the import wrote (PrivateImportRun.Undo), and Error says what could not be; after that, Warning says what stopped, after the
		///warnings collected until then.</summary>
		private static void AddPrivate(X509Store x509Store,byte[] byteArrayFile,string password,DirectCertificateResult directCertificateResult) {
			X509KeyStorageFlags x509KeyStorageFlags=X509KeyStorageFlags.PersistKeySet;
			if(EmailMessages.CertStoreLocation==StoreLocation.LocalMachine) {
				x509KeyStorageFlags|=X509KeyStorageFlags.MachineKeySet;//The program's users on this computer use the key, not only this user.
			}
			else {
				x509KeyStorageFlags|=X509KeyStorageFlags.UserKeySet;
			}
			X509Certificate2Collection x509Certificate2Collection=new X509Certificate2Collection();
			try {
				ActionBeforeKeysStoredForTests?.Invoke();
				//Never the Exportable flag: the private key can't be exported from this computer again.
				x509Certificate2Collection.Import(byteArrayFile,password??"",x509KeyStorageFlags);
			}
			catch(CryptographicException ex) {
				//.NET gives no certificate when the import throws, so this import knows of no key to delete; Windows may have stored one of the file's
				//keys before it failed (docs/port-notes/DIRECT-CERT.md).
				directCertificateResult.Error=Lans.g("DirectCertificateImport","The certificate's private key could not be stored on this computer")+": "+ex.Message
					+" "+Lans.g("DirectCertificateImport","Nothing was imported. Windows may have left a private key of the file behind on this computer, which "
					+"the program cannot find to delete.");
				return;
			}
			PrivateImportRun privateImportRun=new PrivateImportRun(x509Store,x509Certificate2Collection);
			try {
				ActionAfterKeysStoredForTests?.Invoke(x509Certificate2Collection);
				AddPrivateCertificates(x509Store,x509Certificate2Collection,privateImportRun,directCertificateResult);
			}
			catch(Exception ex) {
				if(directCertificateResult.IsImported) {
					//The new certificates are in the store with their keys; what stopped came after them.
					directCertificateResult.Warning=JoinLines(directCertificateResult.Warning,Lans.g("DirectCertificateImport","The import stopped after the "
						+"certificate was added")+": "+ex.Message);
				}
				else {
					string errorStart=IsStoreException(ex) ? Lans.g("DirectCertificateImport","The certificate could not be added to this computer's Direct "
						+"certificate store") : Lans.g("DirectCertificateImport","The import failed");
					directCertificateResult.Error=errorStart+": "+ex.Message+" "+Lans.g("DirectCertificateImport","Nothing was imported.")
						+GetNotUndoneText(privateImportRun.Undo());
				}
			}
			finally {
				privateImportRun.Dispose();
				ResetAll(x509Certificate2Collection);//The keys in use stay in the key store (PersistKeySet); only these objects are released.
			}
		}

		private static void AddPrivateCertificates(X509Store x509Store,X509Certificate2Collection x509Certificate2Collection,PrivateImportRun privateImportRun,
			DirectCertificateResult directCertificateResult)
		{
			List<string> listThumbprintsAdding=directCertificateResult.ListDetails.Select(x => x.Thumbprint).ToList();
			List<X509Certificate2> listAdding=new List<X509Certificate2>();
			List<X509Certificate2> listUnused=new List<X509Certificate2>();
			foreach(X509Certificate2 x509Certificate2 in x509Certificate2Collection) {
				if(x509Certificate2.HasPrivateKey && listThumbprintsAdding.Contains(x509Certificate2.Thumbprint)) {
					listAdding.Add(x509Certificate2);
					continue;
				}
				listUnused.Add(x509Certificate2);
			}
			if(listAdding.Count!=listThumbprintsAdding.Count) {//The file read differently the second time: never half an import.
				directCertificateResult.Error=Lans.g("DirectCertificateImport","The file could not be read the same way twice. Nothing was imported.")
					+GetNotUndoneText(privateImportRun.Undo());
				return;
			}
			List<string> listWarnings=new List<string>();
			try {
				//Another certificate of the file (its CA, one for another address, one already in the store with its key): its key was just stored and
				//is not used, unless a certificate the import adds, or one of the store, has the same key (a renewal with the same key in one file).
				listWarnings.AddRange(privateImportRun.DeleteKeys(listUnused,listAdding));
				for(int i=0;i<listAdding.Count;i++) {
					listAdding[i].FriendlyName=FRIENDLY_NAME_IMPORTED;
					privateImportRun.Add(listAdding[i]);//Throws when the store refuses: AddPrivate takes back what was written.
				}
				directCertificateResult.IsImported=true;
				RemoveReplaced(x509Store,directCertificateResult,listAdding,listWarnings);
				if(EmailMessages.CertStoreLocation==StoreLocation.LocalMachine) {
					for(int i=0;i<listAdding.Count;i++) {
						try {
							GrantUsersReadToPrivateKey(listAdding[i]);
						}
						catch(Exception ex) {
							listWarnings.Add(Lans.g("DirectCertificateImport","The users of this computer may not be able to use the private key")+": "+ex.Message+" "
								+Lans.g("DirectCertificateImport","In certlm.msc, NHINDPrivate > Certificates, right-click the certificate, All Tasks > Manage Private "
								+"Keys, and give Users Read."));
						}
					}
				}
			}
			finally {
				//Also when something throws after the certificates were added (AddPrivate then adds why the import stopped). Before that, a failure
				//takes the import back, and its Error lists what could not be.
				if(directCertificateResult.IsImported && listWarnings.Count>0) {
					directCertificateResult.Warning=JoinLines(directCertificateResult.Warning,string.Join("\r\n",listWarnings));
				}
			}
		}

		///<summary>Removes the replaced certificates (ListDetailsReplaced) from the store, then deletes the private key of each one this window
		///imported, unless a certificate left in the store or one of listAdding uses the same key (DeleteKeysNotInStore). What could not be removed
		///or deleted goes into listWarnings as it happens, for the user.</summary>
		private static void RemoveReplaced(X509Store x509Store,DirectCertificateResult directCertificateResult,List<X509Certificate2> listAdding,
			List<string> listWarnings)
		{
			List<string> listThumbprintsReplaced=directCertificateResult.ListDetailsReplaced.Select(x => x.Thumbprint).ToList();
			if(listThumbprintsReplaced.Count==0) {
				return;
			}
			List<string> listThumbprintsAdding=listAdding.Select(x => x.Thumbprint).ToList();
			X509Certificate2Collection x509Certificate2CollectionStore;
			try {
				x509Certificate2CollectionStore=x509Store.Certificates;
			}
			catch(Exception ex) when(IsStoreException(ex)) {
				listWarnings.Add(Lans.g("DirectCertificateImport","The replaced certificates could not be removed from the store")+": "+ex.Message);
				return;
			}
			try {
				List<X509Certificate2> listKeysToDelete=new List<X509Certificate2>();
				foreach(X509Certificate2 x509Certificate2Replaced in x509Certificate2CollectionStore) {
					if(!listThumbprintsReplaced.Contains(x509Certificate2Replaced.Thumbprint) || listThumbprintsAdding.Contains(x509Certificate2Replaced.Thumbprint)) {
						continue;
					}
					try {
						ActionBeforeStoreRemoveForTests?.Invoke(x509Certificate2Replaced);
						x509Store.Remove(x509Certificate2Replaced);
					}
					catch(Exception ex) when(IsStoreException(ex)) {
						listWarnings.Add(Lans.g("DirectCertificateImport","The replaced certificate could not be removed from the store")+" ("
							+x509Certificate2Replaced.Subject+", "+x509Certificate2Replaced.Thumbprint+"): "+ex.Message);
						continue;
					}
					if(x509Certificate2Replaced.FriendlyName!=FRIENDLY_NAME_IMPORTED) {
						continue;//Put there another way: its key is not this import's to delete.
					}
					listKeysToDelete.Add(x509Certificate2Replaced);
				}
				//Once they are all out of the store: a kept certificate, or another replaced one, may use the same key.
				List<string> listErrors=DeleteKeysNotInStore(x509Store,listKeysToDelete,listAdding);
				for(int i=0;i<listKeysToDelete.Count;i++) {
					if(listErrors[i]!="") {
						listWarnings.Add(Lans.g("DirectCertificateImport","The private key of the replaced certificate could not be deleted")+" ("
							+listKeysToDelete[i].Thumbprint+"): "+listErrors[i]);
					}
				}
			}
			finally {
				ResetAll(x509Certificate2CollectionStore);
			}
		}

		///<summary>Deletes the private key of each certificate of listX509Certificate2s, but not one that a certificate of the store, or of
		///listInUse, still uses (DeleteKeysNotInUse). The certificates read from the store are released again. Why each key could not be deleted,
		///in the order of listX509Certificate2s ("" when it was deleted, it had none, or another certificate uses it); when the store can't be read,
		///no key is deleted.</summary>
		private static List<string> DeleteKeysNotInStore(X509Store x509Store,List<X509Certificate2> listX509Certificate2s,List<X509Certificate2> listInUse) {
			if(listX509Certificate2s.Count==0) {
				return new List<string>();
			}
			X509Certificate2Collection x509Certificate2CollectionStore;
			try {
				x509Certificate2CollectionStore=x509Store.Certificates;
			}
			catch(Exception ex) when(IsStoreException(ex)) {
				string error=Lans.g("DirectCertificateImport","The store could not be read to see whether another certificate uses the key")+": "+ex.Message;
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
		///container, for both. Containers are compared by GetKeyContainerName, all read before the first is deleted. Why each key could not be
		///deleted, in the order of listX509Certificate2s: "" when it was deleted, it had none, or a certificate of listInUse uses it.</summary>
		private static List<string> DeleteKeysNotInUse(List<X509Certificate2> listX509Certificate2s,List<X509Certificate2> listInUse) {
			//Every name first: once a container is deleted, the other certificates that used it no longer say which one it was.
			List<string> listNames=listX509Certificate2s.Select(x => GetKeyContainerName(x)).ToList();
			HashSet<string> hashSetNamesInUse=new HashSet<string>(listInUse.Select(x => GetKeyContainerName(x)).Where(x => x!=null),StringComparer.OrdinalIgnoreCase);
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

		///<summary>"" when everything was taken back, else the lines of what could not be, after a blank line.</summary>
		private static string GetNotUndoneText(List<string> listNotUndone) {
			if(listNotUndone.Count==0) {
				return "";
			}
			return "\r\n\r\n"+Lans.g("DirectCertificateImport","Not removed from this computer again")+":\r\n"+string.Join("\r\n",listNotUndone);
		}

		private static string JoinLines(string text,string line) {
			if(text=="") {
				return line;
			}
			return text+"\r\n"+line;
		}

		///<summary>What one import into the store of private certificates wrote, so that a failure partway can take it back: the private keys it
		///stored (the file's certificates with a key, imported with PersistKeySet) that are not deleted yet, the certificates it added to the store,
		///and the copy without a usable key of the same certificate that an added one took the place of.</summary>
		private sealed class PrivateImportRun:IDisposable {
			private readonly X509Store _x509Store;
			private readonly List<X509Certificate2> _listKeysStored;
			private readonly List<X509Certificate2> _listAdded=new List<X509Certificate2>();
			///<summary>The store's own entry of the same certificate (without a usable key) that an added certificate took the place of, by the added one.</summary>
			private readonly Dictionary<X509Certificate2,X509Certificate2> _dictOverwritten=new Dictionary<X509Certificate2,X509Certificate2>();

			public PrivateImportRun(X509Store x509Store,X509Certificate2Collection x509Certificate2Collection) {
				_x509Store=x509Store;
				_listKeysStored=x509Certificate2Collection.Cast<X509Certificate2>().Where(x => x.HasPrivateKey).ToList();
			}

			///<summary>Deletes the keys this import stored for the certificates (one it stored none for is skipped), but none that a certificate of
			///the store or of listInUse still uses, and each key container once (DeleteKeysNotInStore). A key deleted, or kept for a certificate that
			///uses it, is no longer this import's to delete. What could not be deleted, for the user; none when all was.</summary>
			public List<string> DeleteKeys(List<X509Certificate2> listX509Certificate2s,List<X509Certificate2> listInUse) {
				List<X509Certificate2> listStored=listX509Certificate2s.FindAll(x => _listKeysStored.Contains(x));
				List<string> listErrors=DeleteKeysNotInStore(_x509Store,listStored,listInUse);
				List<string> listProblems=new List<string>();
				for(int i=0;i<listStored.Count;i++) {
					if(listErrors[i]!="") {
						listProblems.Add(Lans.g("DirectCertificateImport","A private key this import stored could not be deleted")+" ("+listStored[i].Subject+", "
							+listStored[i].Thumbprint+"): "+listErrors[i]);
						continue;
					}
					_listKeysStored.Remove(listStored[i]);
				}
				return listProblems;
			}

			///<summary>Adds the certificate to the store. Throws when the store refuses.</summary>
			public void Add(X509Certificate2 x509Certificate2) {
				ActionBeforeStoreAddForTests?.Invoke(x509Certificate2);
				X509Certificate2 x509Certificate2Overwritten=null;
				foreach(X509Certificate2 x509Certificate2Store in _x509Store.Certificates) {
					if(x509Certificate2Overwritten==null && x509Certificate2Store.Thumbprint==x509Certificate2.Thumbprint) {
						x509Certificate2Overwritten=x509Certificate2Store;//The same certificate without a usable key, which the added one takes the place of.
						continue;
					}
					x509Certificate2Store.Reset();
				}
				try {
					_x509Store.Add(x509Certificate2);
				}
				catch {
					x509Certificate2Overwritten?.Reset();
					throw;
				}
				_listAdded.Add(x509Certificate2);
				if(x509Certificate2Overwritten!=null) {
					_dictOverwritten[x509Certificate2]=x509Certificate2Overwritten;
				}
			}

			///<summary>Takes back what this import wrote, after a failure: removes the certificates it added (and puts back the store's copy an added
			///one took the place of) and deletes the keys it stored, each key container once. A certificate that can't be removed keeps its key, so
			///that the store's entry still works, and so does any other certificate left in the store with the same key. What could not be taken
			///back, for the user; none when all was.</summary>
			public List<string> Undo() {
				List<string> listNotUndone=new List<string>();
				for(int i=_listAdded.Count-1;i>=0;i--) {
					X509Certificate2 x509Certificate2=_listAdded[i];
					try {
						_x509Store.Remove(x509Certificate2);
					}
					catch(Exception ex) when(IsStoreException(ex)) {
						listNotUndone.Add(Lans.g("DirectCertificateImport","The certificate could not be removed from the store again; it stays there with its "
							+"private key")+" ("+x509Certificate2.Subject+", "+x509Certificate2.Thumbprint+"): "+ex.Message);
						_listKeysStored.Remove(x509Certificate2);
						continue;
					}
					if(_dictOverwritten.TryGetValue(x509Certificate2,out X509Certificate2 x509Certificate2Overwritten)) {
						try {
							_x509Store.Add(x509Certificate2Overwritten);
						}
						catch(Exception ex) when(IsStoreException(ex)) {
							listNotUndone.Add(Lans.g("DirectCertificateImport","The store's copy of the certificate without a usable key could not be put back")
								+" ("+x509Certificate2.Thumbprint+"): "+ex.Message);
						}
					}
				}
				_listAdded.Clear();
				listNotUndone.AddRange(DeleteKeys(_listKeysStored.ToList(),new List<X509Certificate2>()));
				return listNotUndone;
			}

			public void Dispose() {
				foreach(X509Certificate2 x509Certificate2 in _dictOverwritten.Values) {
					x509Certificate2.Reset();
				}
			}
		}
		#endregion The address's certificate

		#region The trust anchor
		///<summary>Reads the trust anchor file (.cer, .crt, .der or .pem: one public certificate, binary or Base64) and checks it: ListDetails is
		///the anchor an import adds. Error says why it can't be imported. Never throws.</summary>
		public static DirectCertificateResult CheckAnchorFile(string filePath,DateTime dateTimeUtcNow) {
			Meth.NoCheckMiddleTierRole();
			DirectCertificateResult directCertificateResult=new DirectCertificateResult();
			X509Certificate2 x509Certificate2=ReadAnchor(filePath,dateTimeUtcNow,directCertificateResult);
			x509Certificate2?.Dispose();
			return directCertificateResult;
		}

		///<summary>Checks the trust anchor file again (CheckAnchorFile) and adds its certificate to the trust anchors (NHINDAnchors in production).
		///The window asks first. Never throws.</summary>
		public static DirectCertificateResult ImportAnchorFile(string filePath,DateTime dateTimeUtcNow) {
			Meth.NoCheckMiddleTierRole();
			DirectCertificateResult directCertificateResult=new DirectCertificateResult();
			X509Certificate2 x509Certificate2=ReadAnchor(filePath,dateTimeUtcNow,directCertificateResult);
			if(x509Certificate2==null) {
				return directCertificateResult;
			}
			try {
				using(X509Store x509Store=new X509Store(EmailMessages.CertStoreNameAnchor,EmailMessages.CertStoreLocation)) {
					directCertificateResult.Error=OpenForWriting(x509Store);
					if(directCertificateResult.Error!="") {
						return directCertificateResult;
					}
					try {
						x509Store.Add(x509Certificate2);
					}
					catch(Exception ex) when(IsStoreException(ex)) {
						directCertificateResult.Error=Lans.g("DirectCertificateImport","The trust anchor could not be added to this computer's Direct trust anchors")
							+": "+ex.Message;
						return directCertificateResult;
					}
				}
			}
			finally {
				x509Certificate2.Dispose();
			}
			directCertificateResult.IsImported=true;
			return directCertificateResult;
		}

		///<summary>The anchor of the file as a public certificate, or null with Error set.</summary>
		private static X509Certificate2 ReadAnchor(string filePath,DateTime dateTimeUtcNow,DirectCertificateResult directCertificateResult) {
			byte[] byteArrayFile;
			directCertificateResult.Error=ReadFile(filePath,ArrayExtensionsAnchor,isPrivate:false,out byteArrayFile);
			if(directCertificateResult.Error!="") {
				return null;
			}
			X509Certificate2Collection x509Certificate2Collection=new X509Certificate2Collection();
			try {
				//EphemeralKeySet in both: a key in the file (a .pfx under another name, or one in a PEM block) is never written anywhere.
				List<byte[]> listPem=GetPemCertificates(byteArrayFile);
				if(listPem.Count==0) {
					//DER, or Base64 without the PEM lines.
					x509Certificate2Collection.Import(byteArrayFile,"",X509KeyStorageFlags.EphemeralKeySet);
				}
				for(int i=0;i<listPem.Count;i++) {
					x509Certificate2Collection.Import(listPem[i],"",X509KeyStorageFlags.EphemeralKeySet);
				}
			}
			catch(Exception ex) when(ex is CryptographicException || ex is FormatException) {
				ResetAll(x509Certificate2Collection);
				directCertificateResult.Error=Lans.g("DirectCertificateImport","The file is not a certificate file (.cer, .crt, .der or .pem).");
				return null;
			}
			try {
				if(x509Certificate2Collection.Count==0) {
					directCertificateResult.Error=Lans.g("DirectCertificateImport","The file holds no certificate.");
					return null;
				}
				if(x509Certificate2Collection.Count>1) {
					directCertificateResult.Error=Lans.g("DirectCertificateImport","The file holds")+" "+x509Certificate2Collection.Count+" "
						+Lans.g("DirectCertificateImport","certificates. Import the trust anchor from a file that holds only it.");
					return null;
				}
				X509Certificate2 x509Certificate2File=x509Certificate2Collection[0];
				if(x509Certificate2File.HasPrivateKey) {
					directCertificateResult.Error=Lans.g("DirectCertificateImport","The file holds a private key. A trust anchor is a public certificate; import "
						+"the certificate for this address above.");
					return null;
				}
				string problem=GetValidityProblem(x509Certificate2File,dateTimeUtcNow);
				if(problem!="") {
					directCertificateResult.Error=problem;
					return null;
				}
				DirectCertificateDetails directCertificateDetails=DirectCertificateDetails.Create(x509Certificate2File,DirectCertificateBinding.None,"");
				List<X509Certificate2> listStore;
				try {
					listStore=ReadStore(EmailMessages.CertStoreNameAnchor);
				}
				catch(Exception ex) when(IsStoreException(ex)) {
					directCertificateResult.Error=Lans.g("DirectCertificateImport","This computer's Direct trust anchors could not be read")+": "+ex.Message;
					return null;
				}
				bool isInStore;
				try {
					isInStore=listStore.Any(x => x.Thumbprint==x509Certificate2File.Thumbprint);
				}
				finally {
					DisposeAll(listStore);
				}
				if(isInStore) {
					directCertificateResult.ListDetailsInStore.Add(directCertificateDetails);
					directCertificateResult.Error=Lans.g("DirectCertificateImport","This certificate is already a trust anchor on this computer. Nothing to import.");
					return null;
				}
				directCertificateResult.ListDetails.Add(directCertificateDetails);
				return new X509Certificate2(x509Certificate2File.RawData);
			}
			finally {
				ResetAll(x509Certificate2Collection);
			}
		}

		///<summary>The certificates of a PEM file (between "-----BEGIN CERTIFICATE-----" and "-----END CERTIFICATE-----"), decoded. None for a file
		///without those lines (DER). Throws FormatException for a damaged one.</summary>
		private static List<byte[]> GetPemCertificates(byte[] byteArrayFile) {
			List<byte[]> listCertificates=new List<byte[]>();
			string text=Encoding.ASCII.GetString(byteArrayFile);
			int indexBegin=text.IndexOf(PEM_BEGIN,StringComparison.Ordinal);
			while(indexBegin>=0) {
				int indexStart=indexBegin+PEM_BEGIN.Length;
				int indexEnd=text.IndexOf(PEM_END,indexStart,StringComparison.Ordinal);
				if(indexEnd<0) {
					throw new FormatException("PEM end line missing.");
				}
				listCertificates.Add(Convert.FromBase64String(text.Substring(indexStart,indexEnd-indexStart)));//Line breaks and blanks are skipped.
				indexBegin=text.IndexOf(PEM_BEGIN,indexEnd+PEM_END.Length,StringComparison.Ordinal);
			}
			return listCertificates;
		}
		#endregion The trust anchor

		#region Shared
		///<summary>The Setup security log entry of an import, in plain English with invariant dates (the log is not translated): the e-mail address's
		///number (0 for one not saved yet), the thumbprints and last days of the certificates added, and how many of the store's were replaced and
		///kept. No address, password or key.</summary>
		public static string GetLogText(DirectCertificateResult directCertificateResult,long emailAddressNum,bool isAnchor) {
			Meth.NoCheckMiddleTierRole();
			string certificates=string.Join(", ",directCertificateResult.ListDetails.Select(x => x.Thumbprint+" (valid to "
				+x.DateTimeNotAfter.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+")"));
			if(isAnchor) {
				return "Direct trust anchor imported from a file: "+certificates+".";
			}
			return "Direct certificate imported from the provider's file for EmailAddressNum "+emailAddressNum.ToString(CultureInfo.InvariantCulture)+": "
				+certificates+"; certificates of the store replaced: "+directCertificateResult.ListDetailsReplaced.Count.ToString(CultureInfo.InvariantCulture)
				+", kept: "+directCertificateResult.ListDetailsKept.Count.ToString(CultureInfo.InvariantCulture)+".";
		}

		///<summary>"" for an e-mail address (a local part, "@" and a domain, no blank), else what to do. The address is Edit Email Address's user
		///name (Username or email address), which Direct sends from and the certificate is found by, so that is where it is fixed; the certificate
		///window shows it read-only. Edit Email Address checks it before it opens the window.</summary>
		public static string GetAddressError(string emailAddress) {
			Meth.NoCheckMiddleTierRole();
			string address=EmailMessages.GetAddressSimple(emailAddress);
			if(address=="") {
				return Lans.g("DirectCertificateImport","Enter the email address first.");
			}
			int indexAt=address.LastIndexOf('@');
			if(indexAt<=0 || indexAt==address.Length-1 || address.Any(x => char.IsWhiteSpace(x))) {
				return address+" "+Lans.g("DirectCertificateImport","is not an email address. The Direct certificate is found by the user name: in Edit Email "
					+"Address, enter the Direct address (name@domain) in Username or email address.");
			}
			return "";
		}

		///<summary>The validity rule of the import: "" for a certificate inside its dates that Windows finds no problem in (EmailCertificates.IsCertValid,
		///the rule the program uses it by), else why not.</summary>
		public static string GetValidityProblem(X509Certificate2 x509Certificate2,DateTime dateTimeUtcNow) {
			Meth.NoCheckMiddleTierRole();
			if(dateTimeUtcNow<x509Certificate2.NotBefore.ToUniversalTime()) {
				return Lans.g("DirectCertificateImport","The certificate is not valid yet: it is valid from")+" "+FormatDate(x509Certificate2.NotBefore)+". "
					+Lans.g("DirectCertificateImport","Import it on or after that date.")+"\r\n\r\n"+x509Certificate2.Subject;
			}
			if(dateTimeUtcNow>x509Certificate2.NotAfter.ToUniversalTime()) {
				return Lans.g("DirectCertificateImport","The certificate expired on")+" "+FormatDate(x509Certificate2.NotAfter)+". "
					+Lans.g("DirectCertificateImport","Ask your Direct provider for a new one.")+"\r\n\r\n"+x509Certificate2.Subject;
			}
			if(!EmailCertificates.IsCertValid(x509Certificate2)) {
				return Lans.g("DirectCertificateImport","Windows reports a problem with the certificate or a certificate that issued it (revoked, a bad "
					+"signature, or an issuer that is out of date), so the program would not use it.")+"\r\n\r\n"+x509Certificate2.Subject;
			}
			return "";
		}

		internal static string FormatDate(DateTime dateTime) {
			return dateTime.ToShortDateString()+" "+dateTime.ToShortTimeString();
		}

		private static string GetDomain(string address) {
			return address.Substring(address.LastIndexOf('@')+1);//As EmailCertificates.GetCertificates takes it.
		}

		private static string GetBoundName(string address,DirectCertificateBinding binding) {
			if(binding==DirectCertificateBinding.Address) {
				return address;
			}
			if(binding==DirectCertificateBinding.Domain) {
				return GetDomain(address);
			}
			return "";
		}

		///<summary>Reads the file: "" and its bytes, or what is wrong (no file picked, another file type, not found, unreadable, empty).</summary>
		private static string ReadFile(string filePath,string[] arrayExtensions,bool isPrivate,out byte[] byteArrayFile) {
			byteArrayFile=null;
			string types=string.Join(", ",arrayExtensions);
			if(string.IsNullOrWhiteSpace(filePath)) {
				return Lans.g("DirectCertificateImport","Pick the certificate file first")+" ("+types+").";
			}
			string extension;
			try {
				extension=Path.GetExtension(filePath.Trim()).ToLowerInvariant();
			}
			catch(ArgumentException) {
				return Lans.g("DirectCertificateImport","The file name is not valid.");
			}
			if(!arrayExtensions.Contains(extension)) {
				if(isPrivate) {
					return Lans.g("DirectCertificateImport","The certificate for this address must be the .pfx or .p12 file with its private key that your Direct "
						+"provider issued. A .cer file is a public certificate: a trust anchor is imported below.");
				}
				return Lans.g("DirectCertificateImport","A trust anchor must be a .cer, .crt, .der or .pem file. A .pfx or .p12 file is the certificate for "
					+"this address, imported above.");
			}
			if(!File.Exists(filePath.Trim())) {
				return Lans.g("DirectCertificateImport","The file was not found")+": "+filePath.Trim();
			}
			try {
				byteArrayFile=File.ReadAllBytes(filePath.Trim());
			}
			catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException) {
				return Lans.g("DirectCertificateImport","The file could not be read")+": "+ex.Message;
			}
			if(byteArrayFile.Length==0) {
				return Lans.g("DirectCertificateImport","The file is empty.");
			}
			return "";
		}

		///<summary>Every certificate of one of EmailMessages' stores, with or without a key (no criteria). None when the store does not exist yet.
		///The caller disposes them (DisposeAll). Throws CryptographicException if it can't be read.</summary>
		private static List<X509Certificate2> ReadStore(string storeName) {
			List<X509Certificate2> listCertificates=new List<X509Certificate2>();
			using(X509Store x509Store=new X509Store(storeName,EmailMessages.CertStoreLocation)) {
				try {
					x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
				}
				catch(CryptographicException ex) when(IsStoreMissing(ex)) {
					return listCertificates;
				}
				listCertificates.AddRange(x509Store.Certificates.Cast<X509Certificate2>());
			}
			return listCertificates;
		}

		///<summary>True when Windows says the store does not exist (ERROR_FILE_NOT_FOUND: .NET Framework gives the Win32 code, .NET the HRESULT).</summary>
		private static bool IsStoreMissing(CryptographicException cryptographicException) {
			return cryptographicException.HResult==2 || cryptographicException.HResult==unchecked((int)0x80070002);
		}

		///<summary>Opens the store for writing, which makes it if needed: "" or what the user can do (Windows refused).</summary>
		private static string OpenForWriting(X509Store x509Store) {
			try {
				x509Store.Open(OpenFlags.ReadWrite);
			}
			catch(Exception ex) when(IsStoreException(ex)) {
				return Lans.g("DirectCertificateImport","Windows refused to open this computer's Direct certificate store for writing")+" ("+ex.Message+"). "
					+Lans.g("DirectCertificateImport","Importing needs a Windows administrator: close the program, start it with Run as administrator, and "
					+"import again. Nothing was imported.");
			}
			return "";
		}

		private static bool IsStoreException(Exception ex) {
			return ex is CryptographicException || ex is UnauthorizedAccessException || ex is SecurityException;
		}

		private static void ResetAll(X509Certificate2Collection x509Certificate2Collection) {
			foreach(X509Certificate2 x509Certificate2 in x509Certificate2Collection) {
				x509Certificate2.Reset();
			}
		}

		///<summary>Releases the certificates read from a store (ReadStore). Their keys stay where they are.</summary>
		private static void DisposeAll(List<X509Certificate2> listCertificates) {
			for(int i=0;i<listCertificates.Count;i++) {
				listCertificates[i].Dispose();
			}
		}

		///<summary>Deletes the private key of the certificate from its key store, as the connection settings tool (ClientCertificateStore) does for
		///the client certificate it replaces: a CNG key (RSA or ECDSA) or a CAPI key container. The whole container goes, also for another
		///certificate that uses the same key, so the import's callers ask DeleteKeysNotInUse first. "" when deleted (or there was none), else why
		///not. Never throws: anything but a critical exception (IsCriticalException) is a key not deleted.</summary>
		internal static string TryDeletePrivateKey(X509Certificate2 x509Certificate2) {
			try {
				if(!x509Certificate2.HasPrivateKey) {
					return "";
				}
				using(RSA rsa=x509Certificate2.GetRSAPrivateKey()) {
					if(rsa is RSACng) {
						((RSACng)rsa).Key.Delete();
						return "";
					}
					if(rsa is RSACryptoServiceProvider) {
						RSACryptoServiceProvider rsaCryptoServiceProvider=(RSACryptoServiceProvider)rsa;
						rsaCryptoServiceProvider.PersistKeyInCsp=false;//Clear then deletes the key container.
						rsaCryptoServiceProvider.Clear();
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
				return ex.Message;
			}
		}

		///<summary>The key container of the certificate's private key: its kind and provider, user or machine, and unique name (CngKey.UniqueName
		///of a CNG key, CspKeyContainerInfo.UniqueKeyContainerName of a CAPI key), the same for every certificate that uses the one key. Null
		///without a key, or for a key that does not open. Never throws: anything but a critical exception (IsCriticalException) gives null.</summary>
		internal static string GetKeyContainerName(X509Certificate2 x509Certificate2) {
			try {
				if(!x509Certificate2.HasPrivateKey) {
					return null;
				}
				using(RSA rsa=x509Certificate2.GetRSAPrivateKey()) {
					if(rsa is RSACng) {
						using(CngKey cngKey=((RSACng)rsa).Key) {
							return GetCngKeyContainerName(cngKey);
						}
					}
					if(rsa is RSACryptoServiceProvider) {
						CspKeyContainerInfo cspKeyContainerInfo=((RSACryptoServiceProvider)rsa).CspKeyContainerInfo;
						return "CAPI|"+cspKeyContainerInfo.ProviderName+"|"+(cspKeyContainerInfo.MachineKeyStore ? "machine" : "user")+"|"
							+cspKeyContainerInfo.UniqueKeyContainerName;
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

		private static string GetCngKeyContainerName(CngKey cngKey) {
			if(string.IsNullOrEmpty(cngKey.UniqueName)) {
				return null;//An ephemeral key, in memory only: no container to share.
			}
			return "CNG|"+cngKey.Provider.Provider+"|"+(cngKey.IsMachineKey ? "machine" : "user")+"|"+cngKey.UniqueName;
		}

		///<summary>Gives BUILTIN\Users read access to the private key file of a certificate of the Local Computer (a CNG key under
		///%ProgramData%\Microsoft\Crypto\Keys or a CAPI key under %ProgramData%\Microsoft\Crypto\RSA\MachineKeys), as the connection settings
		///tool (ClientCertificateStore) does: the program runs as the signed-in user, who signs and decrypts with it. Throws when the file is not
		///found or its rights can't be changed.</summary>
		private static void GrantUsersReadToPrivateKey(X509Certificate2 x509Certificate2) {
			string pathKey=GetMachinePrivateKeyFilePath(x509Certificate2);
			if(pathKey==null || !File.Exists(pathKey)) {
				throw new FileNotFoundException(Lans.g("DirectCertificateImport","The private key file was not found."));
			}
			SecurityIdentifier securityIdentifierUsers=new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null);
			FileSecurity fileSecurity=File.GetAccessControl(pathKey);
			fileSecurity.AddAccessRule(new FileSystemAccessRule(securityIdentifierUsers,FileSystemRights.Read,AccessControlType.Allow));
			File.SetAccessControl(pathKey,fileSecurity);
		}

		private static string GetMachinePrivateKeyFilePath(X509Certificate2 x509Certificate2) {
			string folderCrypto=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Microsoft","Crypto");
			//The CngKey objects are disposed too: .NET gives a new one for each Key (.NET Framework its own, which the using of rsa or ecdsa then
			//finds disposed).
			using(RSA rsa=x509Certificate2.GetRSAPrivateKey()) {
				if(rsa is RSACng) {
					using(CngKey cngKey=((RSACng)rsa).Key) {
						return Path.Combine(folderCrypto,"Keys",cngKey.UniqueName);
					}
				}
				if(rsa is RSACryptoServiceProvider) {
					return Path.Combine(folderCrypto,"RSA","MachineKeys",((RSACryptoServiceProvider)rsa).CspKeyContainerInfo.UniqueKeyContainerName);
				}
				if(rsa!=null) {
					return null;
				}
			}
			using(ECDsa ecdsa=x509Certificate2.GetECDsaPrivateKey()) {
				if(ecdsa is ECDsaCng) {
					using(CngKey cngKey=((ECDsaCng)ecdsa).Key) {
						return Path.Combine(folderCrypto,"Keys",cngKey.UniqueName);
					}
				}
			}
			return null;
		}
		#endregion Shared
	}
}
