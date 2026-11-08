// Certificate stores of the current user for the Direct secure messaging tests (package S0-3D of the .NET 10 plan). EmailMessages opens the
// Direct stores through one seam (CertStoreLocation and the three store names; Local Computer NHINDPrivate, NHINDExternal and NHINDAnchors in
// production): this points it at three stores of the current user with names of their own, made when a certificate is first added and deleted
// afterwards (CertUnregisterSystemStore), and stands in for the DNS and LDAP search of hosted certificates (the internet). Nothing opens a store
// of the Local Computer or the user's own NHIND stores, and no administrator rights are needed. The certificates are self-signed and made at run
// time (TestCertificates.CreateEmail), their keys removed with them. Licensed under the GNU General Public License, version 2 or later. See
// LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OpenPmsDentalBusiness;

namespace LibraryUpdateTests {
	///<summary>Three stores of the current user for the Direct agent, used for as long as the instance lives. Dispose deletes them, removes the
	///certificates' keys and puts EmailMessages' stores back.</summary>
	public sealed class DirectTestStores:IDisposable {
		[DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]
		private static extern bool CertUnregisterSystemStore(string systemStore,uint flags);
		private const uint CERT_SYSTEM_STORE_CURRENT_USER=0x00010000;
		private const uint CERT_STORE_DELETE_FLAG=0x00000010;
		public readonly string NamePrivate;
		public readonly string NameExternal;
		public readonly string NameAnchor;
		private readonly StoreLocation _storeLocationOld;
		private readonly string _namePrivateOld;
		private readonly string _nameExternalOld;
		private readonly string _nameAnchorOld;
		private readonly Func<MailAddress,X509Certificate2Collection> _funcFindHostedOld;
		private readonly List<X509Certificate2> _listCertificates=new List<X509Certificate2>();
		///<summary>The certificates "hosted in DNS or LDAP" for an address (lower case), which FindPublicCertForAddress finds.</summary>
		private readonly Dictionary<string,X509Certificate2Collection> _dictHosted=new Dictionary<string,X509Certificate2Collection>();
		///<summary>Every address FindPublicCertForAddress looked up, in order.</summary>
		public readonly List<string> ListHostedLookups=new List<string>();

		public DirectTestStores() {
			string id=Guid.NewGuid().ToString("N");
			NamePrivate="OpenPmsS0dPrivate_"+id;
			NameExternal="OpenPmsS0dExternal_"+id;
			NameAnchor="OpenPmsS0dAnchors_"+id;
			_storeLocationOld=EmailMessages.CertStoreLocation;
			_namePrivateOld=EmailMessages.CertStoreNamePrivate;
			_nameExternalOld=EmailMessages.CertStoreNameExternal;
			_nameAnchorOld=EmailMessages.CertStoreNameAnchor;
			_funcFindHostedOld=EmailMessages.FuncFindHostedCertsForTests;
			EmailMessages.CertStoreLocation=StoreLocation.CurrentUser;
			EmailMessages.CertStoreNamePrivate=NamePrivate;
			EmailMessages.CertStoreNameExternal=NameExternal;
			EmailMessages.CertStoreNameAnchor=NameAnchor;
			EmailMessages.FuncFindHostedCertsForTests=mailAddress => {
				ListHostedLookups.Add(mailAddress.Address);
				_dictHosted.TryGetValue(mailAddress.Address.ToLowerInvariant(),out X509Certificate2Collection x509Certificate2Collection);
				return x509Certificate2Collection;
			};
		}

		///<summary>A self-signed email certificate for the address with its private key (TestCertificates.CreateEmail), removed on Dispose.</summary>
		public X509Certificate2 CreateCertificate(string emailAddress,DateTimeOffset? dateTimeOffsetNotAfter=null) {
			X509Certificate2 x509Certificate2=TestCertificates.CreateEmail(emailAddress,dateTimeOffsetNotAfter:dateTimeOffsetNotAfter);
			_listCertificates.Add(x509Certificate2);
			return x509Certificate2;
		}

		///<summary>The public part of a certificate only.</summary>
		public static X509Certificate2 Public(X509Certificate2 x509Certificate2) {
			return new X509Certificate2(x509Certificate2.RawData);
		}

		///<summary>Adds the certificate with its private key to the private store (NHINDPrivate in production).</summary>
		public void AddPrivate(X509Certificate2 x509Certificate2) {
			Add(NamePrivate,x509Certificate2);
		}

		///<summary>Adds the public part only of the certificate to the private store, as a .cer imported into the wrong store would be there.</summary>
		public void AddPrivateWithoutKey(X509Certificate2 x509Certificate2) {
			Add(NamePrivate,Public(x509Certificate2));
		}

		///<summary>Adds the public certificate to the store of other addresses' certificates (NHINDExternal in production).</summary>
		public void AddExternal(X509Certificate2 x509Certificate2) {
			Add(NameExternal,Public(x509Certificate2));
		}

		///<summary>Adds the public certificate to the trust anchors (NHINDAnchors in production).</summary>
		public void AddAnchor(X509Certificate2 x509Certificate2) {
			Add(NameAnchor,Public(x509Certificate2));
		}

		///<summary>Makes the public certificate the one "hosted" for the address: FindPublicCertForAddress finds it, as it would in DNS or LDAP.</summary>
		public void Host(string emailAddress,X509Certificate2 x509Certificate2) {
			_dictHosted[emailAddress.ToLowerInvariant()]=new X509Certificate2Collection(Public(x509Certificate2));
		}

		private void Add(string storeName,X509Certificate2 x509Certificate2) {
			using(X509Store x509Store=new X509Store(storeName,StoreLocation.CurrentUser)) {
				x509Store.Open(OpenFlags.ReadWrite);//Creates the store.
				x509Store.Add(x509Certificate2);
			}
		}

		public void RemovePrivate(X509Certificate2 x509Certificate2) {
			using(X509Store x509Store=new X509Store(NamePrivate,StoreLocation.CurrentUser)) {
				x509Store.Open(OpenFlags.ReadWrite);
				x509Store.Remove(x509Certificate2);
			}
		}

		///<summary>The thumbprints of the certificates in one of the three stores; none when the store was never made.</summary>
		public List<string> GetThumbprints(string storeName) {
			List<string> listThumbprints=new List<string>();
			using(X509Store x509Store=new X509Store(storeName,StoreLocation.CurrentUser)) {
				try {
					x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
				}
				catch(CryptographicException) {
					return listThumbprints;
				}
				foreach(X509Certificate2 x509Certificate2 in x509Store.Certificates) {
					listThumbprints.Add(x509Certificate2.Thumbprint);
				}
			}
			return listThumbprints;
		}

		///<summary>True when none of the three stores exists any more.</summary>
		public bool AreStoresDeleted() {
			foreach(string storeName in new[] { NamePrivate,NameExternal,NameAnchor }) {
				using(X509Store x509Store=new X509Store(storeName,StoreLocation.CurrentUser)) {
					try {
						x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
						return false;
					}
					catch(CryptographicException) {
					}
				}
			}
			return true;
		}

		public void Dispose() {
			EmailMessages.CertStoreLocation=_storeLocationOld;
			EmailMessages.CertStoreNamePrivate=_namePrivateOld;
			EmailMessages.CertStoreNameExternal=_nameExternalOld;
			EmailMessages.CertStoreNameAnchor=_nameAnchorOld;
			EmailMessages.FuncFindHostedCertsForTests=_funcFindHostedOld;
			foreach(string storeName in new[] { NamePrivate,NameExternal,NameAnchor }) {
				CertUnregisterSystemStore(storeName,CERT_SYSTEM_STORE_CURRENT_USER|CERT_STORE_DELETE_FLAG);
			}
			foreach(X509Certificate2 x509Certificate2 in _listCertificates) {
				x509Certificate2.Reset();//Removes the key container of the PFX round trip.
			}
		}
	}
}
