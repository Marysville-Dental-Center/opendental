// Vendor-direct integrations: the office's PDMP client certificate (Bamboo PMP Gateway mutual TLS): import, validation, storage and loading.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CodeBase;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness {
	///<summary>Where a loaded certificate came from.</summary>
	public enum PdmpCertificateSource {
		///<summary>No certificate is set.</summary>
		None,
		///<summary>The PKCS#12 file imported into the Bamboo Client Key and Bamboo Client Password rows (osd1$).</summary>
		Database,
		///<summary>The Windows certificate store (CurrentUser\My, then LocalMachine\My), found by the thumbprint row.</summary>
		Store,
	}

	///<summary>The client certificate Bamboo issues for mutual TLS (pdmp.md sections 3.2 and 3.3). The certificate is either imported from a .pfx/.p12 file
	///into two secret programproperty rows, or left in the Windows certificate store (a non-exportable key IT deployed) and found by thumbprint.
	///Values Open Dental wrote (anything without the osd1$ prefix) are never used: they are Open Dental's own integrator credential.</summary>
	public static class PdmpCertificate {
		///<summary>The Client Authentication extended key usage.</summary>
		public const string OidClientAuthentication="1.3.6.1.5.5.7.3.2";
		///<summary>Fewer days than this before expiry gives a warning.</summary>
		public const int DaysWarnBeforeExpiry=30;

		public static string MessageNotSet {
			get { return Lans.g("PDMP","Not set: import the certificate issued for this software."); }
		}

		///<summary>Loads a PKCS#12 file for validation only, with EphemeralKeySet so the private key never touches a key store, and checks it: it opens with
		///the password, has a private key, is inside its validity dates, and, if it has an extended key usage extension, allows Client Authentication.
		///Returns the certificate (the caller disposes it). Throws PdmpException(NotConfigured) with a message for the user.</summary>
		public static X509Certificate2 Import(byte[] arrayPfxBytes,string password,DateTime dateTimeUtcNow) {
			Meth.NoCheckMiddleTierRole();
			if(arrayPfxBytes==null || arrayPfxBytes.Length==0) {
				throw new PdmpException(PdmpErrorKind.NotConfigured,Lans.g("PDMP","The certificate file is empty."));
			}
			X509Certificate2 certificate;
			try {
				certificate=new X509Certificate2(arrayPfxBytes,password??"",X509KeyStorageFlags.EphemeralKeySet);
			}
			catch(CryptographicException ex) {
				throw new PdmpException(PdmpErrorKind.NotConfigured,
					Lans.g("PDMP","The certificate file could not be opened. Check the password and that the file is a .pfx or .p12 file."),0,
					ex.GetType().Name);
			}
			string problem=GetProblem(certificate,dateTimeUtcNow);
			if(problem!="") {
				certificate.Dispose();
				throw new PdmpException(PdmpErrorKind.NotConfigured,problem);
			}
			return certificate;
		}

		///<summary>"" if the certificate can be used for client authentication now, else why not.</summary>
		public static string GetProblem(X509Certificate2 certificate,DateTime dateTimeUtcNow) {
			Meth.NoCheckMiddleTierRole();
			if(certificate==null) {
				return MessageNotSet;
			}
			if(!certificate.HasPrivateKey) {
				return Lans.g("PDMP","The certificate has no private key. Use the .pfx or .p12 file that includes the key.");
			}
			if(dateTimeUtcNow<certificate.NotBefore.ToUniversalTime()) {
				return Lans.g("PDMP","The certificate is not valid until")+" "+certificate.NotBefore.ToShortDateString()+".";
			}
			if(dateTimeUtcNow>certificate.NotAfter.ToUniversalTime()) {
				return Lans.g("PDMP","The certificate expired on")+" "+certificate.NotAfter.ToShortDateString()+".";
			}
			if(!AllowsClientAuthentication(certificate)) {
				return Lans.g("PDMP","The certificate is not issued for client authentication.");
			}
			return "";
		}

		///<summary>True if the certificate has no extended key usage extension, or has one that includes Client Authentication.</summary>
		public static bool AllowsClientAuthentication(X509Certificate2 certificate) {
			Meth.NoCheckMiddleTierRole();
			List<X509EnhancedKeyUsageExtension> listExtensions=certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().ToList();
			if(listExtensions.Count==0) {
				return true;
			}
			return listExtensions.Any(x => x.EnhancedKeyUsages.Cast<Oid>().Any(y => y.Value==OidClientAuthentication));
		}

		///<summary>True if the certificate expires within DaysWarnBeforeExpiry days of now (and hasn't expired yet).</summary>
		public static bool IsExpiringSoon(X509Certificate2 certificate,DateTime dateTimeUtcNow) {
			Meth.NoCheckMiddleTierRole();
			DateTime dateTimeNotAfterUtc=certificate.NotAfter.ToUniversalTime();
			return dateTimeNotAfterUtc>=dateTimeUtcNow && dateTimeNotAfterUtc<dateTimeUtcNow.AddDays(DaysWarnBeforeExpiry);
		}

		///<summary>A warning to show before the call when the certificate expires soon, else "".</summary>
		public static string GetExpiryWarning(X509Certificate2 certificate,DateTime dateTimeUtcNow) {
			Meth.NoCheckMiddleTierRole();
			if(certificate==null || !IsExpiringSoon(certificate,dateTimeUtcNow)) {
				return "";
			}
			return Lans.g("PDMP","The client certificate expires on")+" "+certificate.NotAfter.ToShortDateString()+". "
				+Lans.g("PDMP","Ask your vendor for a new one and import it in Setup.");
		}

		///<summary>One line for the setup grid: subject, issuer, expiry and thumbprint.</summary>
		public static string Describe(X509Certificate2 certificate) {
			Meth.NoCheckMiddleTierRole();
			if(certificate==null) {
				return MessageNotSet;
			}
			return certificate.GetNameInfo(X509NameType.SimpleName,false)
				+"; "+Lans.g("PDMP","issued by")+" "+certificate.GetNameInfo(X509NameType.SimpleName,true)
				+"; "+Lans.g("PDMP","expires")+" "+certificate.NotAfter.ToShortDateString()
				+"; "+certificate.Thumbprint;
		}

		///<summary>Which source the property values point at. The thumbprint wins when both are set. Legacy (non-osd1$) blobs count as not set.</summary>
		public static PdmpCertificateSource GetSource(PdmpProperty pdmpProperty) {
			Meth.NoCheckMiddleTierRole();
			if(!string.IsNullOrWhiteSpace(pdmpProperty.ClientCertThumbprint)) {
				return PdmpCertificateSource.Store;
			}
			if(!string.IsNullOrEmpty(pdmpProperty.BambooClientKey)) {
				return PdmpCertificateSource.Database;
			}
			return PdmpCertificateSource.None;
		}

		///<summary>Loads the certificate for a TLS call: from the store by thumbprint, else from the imported file. The imported file is loaded with
		///UserKeySet: Windows' TLS stack (SChannel) can't present a key held only in memory (EphemeralKeySet), and MachineKeySet needs write access
		///to the machine key store that non-administrators lack. The temporary user key is deleted when the certificate is disposed, so the caller
		///disposes it after the call. Throws PdmpException(NotConfigured) if nothing usable is set.</summary>
		public static X509Certificate2 Load(PdmpProperty pdmpProperty,DateTime dateTimeUtcNow) {
			Meth.NoCheckMiddleTierRole();
			X509Certificate2 certificate;
			switch(GetSource(pdmpProperty)) {
				case PdmpCertificateSource.Store:
					certificate=FindInStore(pdmpProperty.ClientCertThumbprint);
					if(certificate==null) {
						throw new PdmpException(PdmpErrorKind.NotConfigured,
							Lans.g("PDMP","The client certificate with the thumbprint in Setup is not in this computer's certificate store."));
					}
					break;
				case PdmpCertificateSource.Database:
					try {
						byte[] arrayBytes=Convert.FromBase64String(pdmpProperty.BambooClientKey);
						certificate=new X509Certificate2(arrayBytes,pdmpProperty.BambooClientPassword??"",X509KeyStorageFlags.UserKeySet);
					}
					catch(Exception ex) {
						throw new PdmpException(PdmpErrorKind.NotConfigured,
							Lans.g("PDMP","The client certificate in Setup could not be opened. Import it again."),0,ex.GetType().Name);
					}
					break;
				default:
					throw new PdmpException(PdmpErrorKind.NotConfigured,Lans.g("PDMP","Client certificate")+": "+MessageNotSet);
			}
			string problem=GetProblem(certificate,dateTimeUtcNow);
			if(problem!="") {
				certificate.Dispose();
				throw new PdmpException(PdmpErrorKind.NotConfigured,problem);
			}
			return certificate;
		}

		///<summary>Finds a certificate with a private key by thumbprint in CurrentUser\My, then LocalMachine\My. Returns null if none.</summary>
		public static X509Certificate2 FindInStore(string thumbprint) {
			Meth.NoCheckMiddleTierRole();
			string thumbprintNormal=NormalizeThumbprint(thumbprint);
			if(thumbprintNormal=="") {
				return null;
			}
			foreach(StoreLocation storeLocation in new[] { StoreLocation.CurrentUser,StoreLocation.LocalMachine }) {
				try {
					using(X509Store x509Store=new X509Store(StoreName.My,storeLocation)) {
						x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
						X509Certificate2Collection collection=x509Store.Certificates.Find(X509FindType.FindByThumbprint,thumbprintNormal,false);
						X509Certificate2 certificate=collection.OfType<X509Certificate2>().FirstOrDefault(x => x.HasPrivateKey);
						if(certificate!=null) {
							return certificate;
						}
					}
				}
				catch(Exception ex) {
					ex.DoNothing();//A store this user can't open is the same as a store without the certificate.
				}
			}
			return null;
		}

		///<summary>The certificates with a private key that allow client authentication, from CurrentUser\My and LocalMachine\My, for the store picker.</summary>
		public static List<X509Certificate2> ListStoreCertificates(DateTime dateTimeUtcNow) {
			Meth.NoCheckMiddleTierRole();
			List<X509Certificate2> listCertificates=new List<X509Certificate2>();
			foreach(StoreLocation storeLocation in new[] { StoreLocation.CurrentUser,StoreLocation.LocalMachine }) {
				try {
					using(X509Store x509Store=new X509Store(StoreName.My,storeLocation)) {
						x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
						listCertificates.AddRange(x509Store.Certificates.OfType<X509Certificate2>()
							.Where(x => x.HasPrivateKey && AllowsClientAuthentication(x) && x.NotAfter.ToUniversalTime()>dateTimeUtcNow
								&& !listCertificates.Any(y => y.Thumbprint==x.Thumbprint)));
					}
				}
				catch(Exception ex) {
					ex.DoNothing();
				}
			}
			return listCertificates;
		}

		///<summary>Upper case hex without spaces or the invisible mark that Windows' certificate dialog copies in front of a thumbprint.</summary>
		public static string NormalizeThumbprint(string thumbprint) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrWhiteSpace(thumbprint)) {
				return "";
			}
			return new string(thumbprint.Where(x => Uri.IsHexDigit(x)).ToArray()).ToUpperInvariant();
		}

		#region Storage
		///<summary>Validates the file (Import) and stores it for the program and clinic: the base64 file in Bamboo Client Key and its password in Bamboo
		///Client Password, both osd1$, masked and high security; the thumbprint row is cleared. Writes a security log entry. Returns the description
		///of the stored certificate. Throws PdmpException if the file is unusable or encryption fails.</summary>
		public static string SaveImported(long programNum,long clinicNum,byte[] arrayPfxBytes,string password) {
			Meth.NoCheckMiddleTierRole();
			string description;
			using(X509Certificate2 certificate=Import(arrayPfxBytes,password,DateTime.UtcNow)) {
				description=Describe(certificate);
			}
			if(!VendorCredentials.WriteSecret(programNum,PdmpProperty.BambooAuthToken,clinicNum,Convert.ToBase64String(arrayPfxBytes))
				|| !VendorCredentials.WriteSecret(programNum,PdmpProperty.BambooAuthPassword,clinicNum,password??""))
			{
				throw new PdmpException(PdmpErrorKind.NotConfigured,Lans.g("PDMP","The certificate could not be encrypted for storage. Nothing was saved."));
			}
			VendorCredentials.WriteSetting(programNum,PdmpProperty.BambooClientCertThumbprint,clinicNum,"");
			MakeLog(programNum,clinicNum,"Client certificate imported: "+description);
			return description;
		}

		///<summary>Uses the store certificate with this thumbprint: saves the thumbprint and clears any imported file. Writes a security log entry.</summary>
		public static void SaveThumbprint(long programNum,long clinicNum,string thumbprint) {
			Meth.NoCheckMiddleTierRole();
			string thumbprintNormal=NormalizeThumbprint(thumbprint);
			VendorCredentials.WriteSetting(programNum,PdmpProperty.BambooClientCertThumbprint,clinicNum,thumbprintNormal);
			ClearImported(programNum,clinicNum);
			MakeLog(programNum,clinicNum,"Client certificate set to the Windows store certificate "+thumbprintNormal+".");
		}

		///<summary>Clears the imported file, its password and the thumbprint for the program and clinic. Writes a security log entry.</summary>
		public static void Remove(long programNum,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			ClearImported(programNum,clinicNum);
			VendorCredentials.WriteSetting(programNum,PdmpProperty.BambooClientCertThumbprint,clinicNum,"");
			MakeLog(programNum,clinicNum,"Client certificate removed.");
		}

		private static void ClearImported(long programNum,long clinicNum) {
			foreach(string desc in new[] { PdmpProperty.BambooAuthToken,PdmpProperty.BambooAuthPassword }) {
				ProgramProperty programProperty=VendorCredentials.EnsureSecret(programNum,desc,clinicNum);
				if(programProperty.PropertyValue=="") {
					continue;
				}
				ProgramProperty programPropertyOld=programProperty.Copy();
				programProperty.PropertyValue="";
				ProgramProperties.Update(programProperty,programPropertyOld);
			}
		}

		private static void MakeLog(long programNum,long clinicNum,string text) {
			string progName=Programs.GetProgram(programNum)?.ProgName??"";
			SecurityLogs.MakeLogEntry(EnumPermType.ManageHighSecurityProgProperties,0,
				progName+(clinicNum==0 ? "" : " (ClinicNum "+POut.Long(clinicNum)+")")+": "+text,programNum,DateTime.Now);
		}
		#endregion Storage
	}
}
