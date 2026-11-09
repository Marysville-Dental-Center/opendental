// Direct secure messaging (open point O-13 of the .NET 10 plan, owner answer of 2026-11-08: "Yes, load Direct provider's certificate from a
// file"): the office's own certificate and its Direct provider's trust anchor, imported from the files the provider (HISP) issued into the
// stores the program reads (NHINDPrivate and NHINDAnchors, EmailMessages), in place of the registration with Open Dental's servers. The rules
// are in docs/port-notes/DIRECT-CERT.md. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
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
		public bool HasPrivateKey;
		public DirectCertificateBinding Binding;
		///<summary>The address or domain the certificate names; "" for None.</summary>
		public string BoundName="";

		public static DirectCertificateDetails Create(X509Certificate2 x509Certificate2,DirectCertificateBinding binding,string boundName) {
			DirectCertificateDetails directCertificateDetails=new DirectCertificateDetails();
			directCertificateDetails.Subject=x509Certificate2.Subject;
			directCertificateDetails.Issuer=x509Certificate2.Issuer;
			directCertificateDetails.SerialNumber=x509Certificate2.SerialNumber;
			directCertificateDetails.Thumbprint=x509Certificate2.Thumbprint;
			directCertificateDetails.DateTimeNotBefore=x509Certificate2.NotBefore;
			directCertificateDetails.DateTimeNotAfter=x509Certificate2.NotAfter;
			directCertificateDetails.HasPrivateKey=x509Certificate2.HasPrivateKey;
			directCertificateDetails.Binding=binding;
			directCertificateDetails.BoundName=(binding==DirectCertificateBinding.None) ? "" : boundName;
			return directCertificateDetails;
		}

		///<summary>The certificate in a few lines: subject, issuer, serial number, validity dates, what it names, its private key, thumbprint.</summary>
		public string Describe() {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine(Lans.g("DirectCertificateImport","Subject")+": "+Subject);
			stringBuilder.AppendLine(Lans.g("DirectCertificateImport","Issuer")+": "+Issuer);
			stringBuilder.AppendLine(Lans.g("DirectCertificateImport","Serial number")+": "+SerialNumber);
			stringBuilder.AppendLine(Lans.g("DirectCertificateImport","Valid from")+" "+DirectCertificateImport.FormatDate(DateTimeNotBefore)+" "
				+Lans.g("DirectCertificateImport","to")+" "+DirectCertificateImport.FormatDate(DateTimeNotAfter));
			if(Binding==DirectCertificateBinding.Address) {
				stringBuilder.AppendLine(Lans.g("DirectCertificateImport","For the address")+" "+BoundName);
			}
			else if(Binding==DirectCertificateBinding.Domain) {
				stringBuilder.AppendLine(Lans.g("DirectCertificateImport","For every address of the domain")+" "+BoundName);
			}
			stringBuilder.AppendLine(Lans.g("DirectCertificateImport","Private key")+": "
				+(HasPrivateKey ? Lans.g("DirectCertificateImport","yes") : Lans.g("DirectCertificateImport","no")));
			stringBuilder.Append(Lans.g("DirectCertificateImport","Thumbprint")+": "+Thumbprint);
			return stringBuilder.ToString();
		}

		///<summary>One line: subject, the last day it is valid and the thumbprint.</summary>
		public string DescribeShort() {
			return Subject+"; "+Lans.g("DirectCertificateImport","valid to")+" "+DirectCertificateImport.FormatDate(DateTimeNotAfter)
				+(HasPrivateKey ? "" : "; "+Lans.g("DirectCertificateImport","no private key"))+"; "+Thumbprint;
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
		///<summary>The certificates of the file that the store already holds (with their private keys, for the private store): not added again.</summary>
		public List<DirectCertificateDetails> ListDetailsInStore=new List<DirectCertificateDetails>();
		///<summary>The older certificates of the private store for the same address or domain that the import replaces (a check) or replaced
		///(an import).</summary>
		public List<DirectCertificateDetails> ListDetailsReplaced=new List<DirectCertificateDetails>();
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

		#region The address's certificate
		///<summary>Reads the .pfx or .p12 file with its password, without storing any key (EphemeralKeySet), and checks it for the address
		///(docs/port-notes/DIRECT-CERT.md): ListDetails are the certificates an import adds, ListDetailsReplaced the older ones of the store it
		///replaces. Error says why the file can't be imported. Never throws.</summary>
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
		///their private keys never exportable, then removes the older certificates of the address or domain. listThumbprintsReplaceAllowed: the
		///older certificates the user agreed to replace (the window asks first); when the store holds an older one not in it, nothing is imported
		///and Error says so. In production (the Local Computer store) the computer's users get read access to the new private keys; Warning says so
		///if that failed. Never throws.</summary>
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
				directCertificateResult.Error=Lans.g("DirectCertificateImport","The store has an older certificate for this address or its domain that "
					+"was not agreed to be replaced. Nothing was imported. Check the file again.");
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
			foreach(X509Certificate2 x509Certificate2 in ReadStore(EmailMessages.CertStoreNamePrivate)) {
				DirectCertificateBinding binding=GetBinding(x509Certificate2,address);
				if(binding!=DirectCertificateBinding.None) {
					listDetails.Add(DirectCertificateDetails.Create(x509Certificate2,binding,GetBoundName(address,binding)));
				}
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
			List<string> listThumbprintsFile=listKeyed.Select(x => x.Thumbprint).ToList();
			for(int i=0;i<listKeyed.Count;i++) {
				DirectCertificateBinding binding=GetBinding(listKeyed[i],address);
				DirectCertificateDetails directCertificateDetails=DirectCertificateDetails.Create(listKeyed[i],binding,GetBoundName(address,binding));
				if(listStore.Any(x => x.Thumbprint==listKeyed[i].Thumbprint && x.HasPrivateKey)) {
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
			//The older certificates: the same kind (address or domain) and name as a certificate the import adds, not one of the file's.
			for(int i=0;i<listStore.Count;i++) {
				if(listThumbprintsFile.Contains(listStore[i].Thumbprint) && listStore[i].HasPrivateKey) {
					continue;//Already imported, the same certificate: stays.
				}
				DirectCertificateBinding binding=GetBinding(listStore[i],address);
				if(binding==DirectCertificateBinding.None || !directCertificateResult.ListDetails.Any(x => x.Binding==binding)) {
					continue;
				}
				directCertificateResult.ListDetailsReplaced.Add(DirectCertificateDetails.Create(listStore[i],binding,GetBoundName(address,binding)));
			}
		}

		///<summary>Imports the checked file into the open store: the keys are written to the key store now (never exportable), the certificates
		///added with FRIENDLY_NAME_IMPORTED, then the older ones removed. A key of the file the import does not use is deleted again, and so are the
		///new keys when adding fails.</summary>
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
				//Never X509KeyStorageFlags.Exportable: the private key can't be exported from this computer again.
				x509Certificate2Collection.Import(byteArrayFile,password??"",x509KeyStorageFlags);
			}
			catch(CryptographicException ex) {
				directCertificateResult.Error=Lans.g("DirectCertificateImport","The certificate's private key could not be stored on this computer")+": "+ex.Message;
				return;
			}
			try {
				AddPrivateCertificates(x509Store,x509Certificate2Collection,directCertificateResult);
			}
			finally {
				ResetAll(x509Certificate2Collection);//The keys stay in the key store (PersistKeySet); only these objects are released.
			}
		}

		private static void AddPrivateCertificates(X509Store x509Store,X509Certificate2Collection x509Certificate2Collection,
			DirectCertificateResult directCertificateResult)
		{
			List<string> listThumbprintsAdding=directCertificateResult.ListDetails.Select(x => x.Thumbprint).ToList();
			List<X509Certificate2> listAdding=new List<X509Certificate2>();
			foreach(X509Certificate2 x509Certificate2 in x509Certificate2Collection) {
				if(x509Certificate2.HasPrivateKey && listThumbprintsAdding.Contains(x509Certificate2.Thumbprint)) {
					listAdding.Add(x509Certificate2);
					continue;
				}
				TryDeletePrivateKey(x509Certificate2);//Another certificate of the file: its key was just stored and is not used.
			}
			if(listAdding.Count!=listThumbprintsAdding.Count) {//The file read differently the second time: never half an import.
				for(int i=0;i<listAdding.Count;i++) {
					TryDeletePrivateKey(listAdding[i]);
				}
				directCertificateResult.Error=Lans.g("DirectCertificateImport","The file could not be read the same way twice. Nothing was imported.");
				return;
			}
			List<X509Certificate2> listAdded=new List<X509Certificate2>();
			try {
				for(int i=0;i<listAdding.Count;i++) {
					listAdding[i].FriendlyName=FRIENDLY_NAME_IMPORTED;
					x509Store.Add(listAdding[i]);
					listAdded.Add(listAdding[i]);
				}
			}
			catch(Exception ex) when(IsStoreException(ex)) {
				for(int i=0;i<listAdded.Count;i++) {
					ODException.SwallowAnyException(() => x509Store.Remove(listAdded[i]));
				}
				for(int i=0;i<listAdding.Count;i++) {
					TryDeletePrivateKey(listAdding[i]);
				}
				directCertificateResult.Error=Lans.g("DirectCertificateImport","The certificate could not be added to this computer's Direct certificate "
					+"store")+": "+ex.Message;
				return;
			}
			directCertificateResult.IsImported=true;
			List<string> listWarnings=new List<string>();
			List<string> listThumbprintsReplaced=directCertificateResult.ListDetailsReplaced.Select(x => x.Thumbprint).ToList();
			foreach(X509Certificate2 x509Certificate2Old in x509Store.Certificates) {
				if(!listThumbprintsReplaced.Contains(x509Certificate2Old.Thumbprint) || listThumbprintsAdding.Contains(x509Certificate2Old.Thumbprint)) {
					continue;
				}
				try {
					x509Store.Remove(x509Certificate2Old);
				}
				catch(Exception ex) when(IsStoreException(ex)) {
					listWarnings.Add(Lans.g("DirectCertificateImport","The older certificate could not be removed from the store")+" ("
						+x509Certificate2Old.Subject+", "+x509Certificate2Old.Thumbprint+"): "+ex.Message);
					continue;
				}
				if(x509Certificate2Old.FriendlyName!=FRIENDLY_NAME_IMPORTED) {
					continue;//Put there another way: its key is not this import's to delete.
				}
				string errorKey=TryDeletePrivateKey(x509Certificate2Old);
				if(errorKey!="") {
					listWarnings.Add(Lans.g("DirectCertificateImport","The private key of the older certificate could not be deleted")+" ("
						+x509Certificate2Old.Thumbprint+"): "+errorKey);
				}
			}
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
			directCertificateResult.Warning=string.Join("\r\n",listWarnings);
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
				List<byte[]> listPem=GetPemCertificates(byteArrayFile);
				if(listPem.Count==0) {
					//DER, or Base64 without the PEM lines. EphemeralKeySet: a key in the file (a .pfx under another name) is never written anywhere.
					x509Certificate2Collection.Import(byteArrayFile,"",X509KeyStorageFlags.EphemeralKeySet);
				}
				for(int i=0;i<listPem.Count;i++) {
					x509Certificate2Collection.Import(listPem[i]);
				}
			}
			catch(Exception ex) when(ex is CryptographicException || ex is FormatException) {
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
				if(listStore.Any(x => x.Thumbprint==x509Certificate2File.Thumbprint)) {
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
		///<summary>The Setup security log entry of an import: the e-mail address's number (0 for one not saved yet), the thumbprints and last days of
		///the certificates added, and how many older ones were replaced. No address, password or key.</summary>
		public static string GetLogText(DirectCertificateResult directCertificateResult,long emailAddressNum,bool isAnchor) {
			Meth.NoCheckMiddleTierRole();
			string certificates=string.Join(", ",directCertificateResult.ListDetails.Select(x => x.Thumbprint+" ("+Lans.g("DirectCertificateImport","valid to")+" "
				+x.DateTimeNotAfter.ToString("yyyy-MM-dd")+")"));
			if(isAnchor) {
				return "Direct trust anchor imported from a file: "+certificates+".";
			}
			return "Direct certificate imported from the provider's file for EmailAddressNum "+POut.Long(emailAddressNum)+": "+certificates
				+"; older certificates replaced: "+directCertificateResult.ListDetailsReplaced.Count+".";
		}

		///<summary>"" for an e-mail address (a local part, "@" and a domain, no blank), else what to do.</summary>
		public static string GetAddressError(string emailAddress) {
			Meth.NoCheckMiddleTierRole();
			string address=EmailMessages.GetAddressSimple(emailAddress);
			if(address=="") {
				return Lans.g("DirectCertificateImport","Enter the email address first.");
			}
			int indexAt=address.LastIndexOf('@');
			if(indexAt<=0 || indexAt==address.Length-1 || address.Any(x => char.IsWhiteSpace(x))) {
				return address+" "+Lans.g("DirectCertificateImport","is not an email address. Enter the address the Direct certificate is for.");
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
		///Throws CryptographicException if it can't be read.</summary>
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

		///<summary>True when Windows says the store does not exist (ERROR_FILE_NOT_FOUND).</summary>
		private static bool IsStoreMissing(CryptographicException cryptographicException) {
			return cryptographicException.HResult==unchecked((int)0x80070002);
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

		///<summary>Deletes the private key of the certificate from its key store, as the connection settings tool (ClientCertificateStore) does for
		///the client certificate it replaces: a CNG key (RSA or ECDSA) or a CAPI key container. "" when deleted (or there was none), else why not.
		///</summary>
		internal static string TryDeletePrivateKey(X509Certificate2 x509Certificate2) {
			try {
				if(!x509Certificate2.HasPrivateKey) {
					return "";
				}
				RSA rsa=x509Certificate2.GetRSAPrivateKey();
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
				ECDsa ecdsa=x509Certificate2.GetECDsaPrivateKey();
				if(ecdsa is ECDsaCng) {
					((ECDsaCng)ecdsa).Key.Delete();
				}
				return "";
			}
			catch(Exception ex) when(ex is CryptographicException || ex is UnauthorizedAccessException || ex is NotSupportedException) {
				return ex.Message;
			}
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
		#endregion Shared
	}
}
