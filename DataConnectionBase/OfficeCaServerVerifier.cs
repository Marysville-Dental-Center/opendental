// The program's own check of a database server's certificate, made during the TLS handshake before any login is sent (DB-SEC 3, decided as
// (c), docs/specs/db-connection-security.md section 8; docs/port-notes/S2-2.md section 3.6).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace DataConnectionBase {
	///<summary>Verifies a database server's certificate against the office CA, as the connection's own RemoteCertificateValidationCallback,
	///so that a server outside the office CA is refused in the TLS handshake, before MySqlConnector sends the login (owner decision Q5).
	///MySqlConnector 2.6.2 with SslCa logs in first and decides after the server's OK packet, and accepts a server that proves the account's
	///stored hash with any certificate (docs/port-notes/S2-2.md section 3.6), so DbConnectionSecurity never hands SslCa to the driver for a
	///server over the network: it uses SslMode=Required and this callback, which the driver passes to SslStream, whose handshake fails when
	///the callback returns false.
	///The check, for the certificate the server sent and the certificates it sent with it:
	///1. The chain is built by Windows' chain engine with the certificates of the office CA file as its only trusted roots (an exclusive root
	///   store: CERT_CHAIN_ENGINE_CONFIG.hExclusiveRoot), so no root of the Windows stores is ever accepted; the intermediates come from the
	///   server's TLS message and the CA file, never from the network (AIA retrieval off). Then the root of the built chain must be, byte for
	///   byte, a certificate of the office CA file, and every element must be free of errors: trusted, signatures valid, within its dates
	///   (also checked here directly), valid for server authentication (the shared client certificate, also signed by the office CA, is not a
	///   server certificate), and not revoked.
	///2. Revocation as the driver asked for it: under VerifyFull an online revocation check of the chain without the root (what MySqlConnector
	///   requested from SslStream, checkCertificateRevocation with SkipCertificateRevocationCheck off), under VerifyCA none (the driver asked
	///   for none). A revoked certificate is refused; a revocation status Windows cannot find (a certificate without a CRL distribution point,
	///   as the server guide's certificates are, or a CRL server that does not answer) is not a refusal, as the driver never refused for it.
	///   X509Chain with AllowUnknownCertificateAuthority cannot be used for this: Windows checks no revocation for a chain to a root that is not
	///   trusted (OfficeCaServerVerifierTests shows it), which is also why the driver's SslCa check never saw a revocation.
	///3. Under VerifyFull the certificate must name one of the connection's hosts, by Windows' SSL policy (CERT_CHAIN_POLICY_SSL), the rules
	///   SslStream applied for VerifyFull: the DNS names and IP addresses of the subject alternative names, the common name only when the
	///   certificate has no subject alternative name. A trailing dot of a host name is left out. Under VerifyCA the name is not checked.
	///A refusal returns false and keeps its reason in LastRefusal; it never throws into the handshake. Nothing here reads or changes a
	///certificate store of the computer. See DbConnectionSecurity.GetDriverConnectionString for where it is used.</summary>
	public sealed class OfficeCaServerVerifier {
		///<summary>The extended key usage of a TLS server certificate.</summary>
		public const string OID_SERVER_AUTHENTICATION="1.3.6.1.5.5.7.3.1";
		private static readonly Regex _regexPemCertificate=new Regex(
			@"-----BEGIN CERTIFICATE-----\s*(?<base64>[A-Za-z0-9+/=\s]+?)\s*-----END CERTIFICATE-----",RegexOptions.CultureInvariant);
		///<summary>The chain statuses that are not a refusal: Windows could not find the revocation status (no CRL distribution point, or the
		///CRL server did not answer).</summary>
		private const X509ChainStatusFlags REVOCATION_UNKNOWN=X509ChainStatusFlags.RevocationStatusUnknown|X509ChainStatusFlags.OfflineRevocation;
		private readonly object _lockLastRefusal=new object();
		private string _lastRefusal="";

		///<summary>The office CA certificate file (PEM, one or more CERTIFICATE blocks, or one DER certificate). Read at every check, so the
		///check uses the file as it is at the handshake.</summary>
		public string CaPath { get; private set; }
		///<summary>The hosts the certificate may name (the connection's Server value, split at commas, each trimmed, without a trailing dot or
		///square brackets). The certificate must name one of them under VerifyFull: MySqlConnector does not tell the callback which host of a
		///list it reached.</summary>
		public List<string> ListHosts { get; private set; }
		///<summary>True for VerifyFull (the host name is checked), false for VerifyCA (the chain only).</summary>
		public bool IsHostNameChecked { get; private set; }
		///<summary>Online for VerifyFull and NoCheck for VerifyCA, as the driver asked SslStream for them. Offline uses only what Windows already
		///has (the URL cache and ListCrls).</summary>
		public X509RevocationMode RevocationMode { get; set; }
		///<summary>CRLs (DER) the revocation check uses besides those Windows finds through the certificates' CRL distribution points. The program
		///adds none; a test adds its own, so that its revocation cases need no network.</summary>
		public List<byte[]> ListCrls { get; private set; }

		///<summary>Why the last check refused the server, "" when it accepted it or has not run. Never holds a secret.</summary>
		public string LastRefusal {
			get {
				lock(_lockLastRefusal) {
					return _lastRefusal;
				}
			}
		}

		///<summary>caPath is the office CA file (a connection string's SslCa), server the connection's Server value as MySqlConnector reads it,
		///isHostNameChecked true for VerifyFull and false for VerifyCA.</summary>
		public OfficeCaServerVerifier(string caPath,string server,bool isHostNameChecked) {
			CaPath=caPath??"";
			ListHosts=(server??"").Split(',').Select(x => NormalizeHost(x)).Where(x => x!="").ToList();
			IsHostNameChecked=isHostNameChecked;
			RevocationMode=isHostNameChecked ? X509RevocationMode.Online : X509RevocationMode.NoCheck;
			ListCrls=new List<byte[]>();
		}

		///<summary>The RemoteCertificateValidationCallback: true only when GetRefusal finds nothing. sslPolicyErrors, the result of SslStream's own
		///check against the Windows stores, is not used: the office CA is not a Windows root. The certificates the server sent with its own
		///(chain.ChainPolicy.ExtraStore and the chain's elements) may serve as intermediates; none of them can become a root.</summary>
		public bool ValidateServerCertificate(object sender,X509Certificate certificate,X509Chain chain,SslPolicyErrors sslPolicyErrors) {
			string refusal;
			try {
				List<X509Certificate2> listSent=new List<X509Certificate2>();
				if(chain!=null) {
					foreach(X509Certificate2 x509Certificate2 in chain.ChainPolicy.ExtraStore) {
						listSent.Add(x509Certificate2);
					}
					foreach(X509ChainElement x509ChainElement in chain.ChainElements) {
						listSent.Add(x509ChainElement.Certificate);
					}
				}
				refusal=GetRefusal(certificate==null ? null : new X509Certificate2(certificate),listSent);
			}
			catch(Exception ex) {
				refusal="the server's certificate could not be checked ("+ex.GetType().Name+": "+ex.Message+")";
			}
			lock(_lockLastRefusal) {
				_lastRefusal=refusal;
			}
			return refusal=="";
		}

		///<summary>Why the server's certificate is refused, "" when it is accepted (see the class summary). listIntermediates are the other
		///certificates the server sent (or a test gives); any of them may also be the server certificate itself or a root, which changes
		///nothing.</summary>
		public string GetRefusal(X509Certificate2 certificate,IEnumerable<X509Certificate2> listIntermediates) {
			if(certificate==null) {
				return "the server sent no certificate";
			}
			string subject=certificate.Subject;
			List<X509Certificate2> listCa;
			string refusalCa=LoadCaCertificates(CaPath,out listCa);
			if(refusalCa!="") {
				return refusalCa;
			}
			if(IsHostNameChecked && ListHosts.Count==0) {
				return "the connection names no host to check the server's certificate ("+subject+") against";
			}
			DateTime dateTimeNow=DateTime.Now;
			if(certificate.NotAfter<dateTimeNow) {
				return "the server's certificate ("+subject+") expired on "+certificate.NotAfter.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture);
			}
			if(certificate.NotBefore>dateTimeNow) {
				return "the server's certificate ("+subject+") is not valid before "
					+certificate.NotBefore.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture);
			}
			List<X509Certificate2> listOthers=(listIntermediates??Enumerable.Empty<X509Certificate2>()).Where(x => x!=null).ToList();
			using(NativeChain nativeChain=NativeChain.Build(certificate,listCa,listOthers,ListCrls,RevocationMode)) {
				X509Chain x509Chain=nativeChain.X509Chain;
				int countElements=x509Chain.ChainElements.Count;
				X509Certificate2 x509Certificate2Root=x509Chain.ChainElements[countElements-1].Certificate;
				bool isRootOfficeCa=x509Certificate2Root.Subject==x509Certificate2Root.Issuer
					&& listCa.Any(x => x.RawData.SequenceEqual(x509Certificate2Root.RawData));
				if(!isRootOfficeCa) {
					return "the server's certificate ("+subject+") does not chain to the office CA ("+CaPath+"); its chain ends at "
						+x509Certificate2Root.Subject+" ("+GetStatusText(x509Chain.ChainStatus)+")";
				}
				for(int i=0;i<countElements;i++) {
					X509ChainElement x509ChainElement=x509Chain.ChainElements[i];
					X509Certificate2 x509Certificate2=x509ChainElement.Certificate;
					if(x509Certificate2.NotAfter<dateTimeNow || x509Certificate2.NotBefore>dateTimeNow) {
						return "the certificate "+x509Certificate2.Subject+" in the server's chain is outside its dates ("
							+x509Certificate2.NotBefore.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+" to "
							+x509Certificate2.NotAfter.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+")";
					}
					X509ChainStatusFlags flags=GetFlags(x509ChainElement.ChainElementStatus)&~REVOCATION_UNKNOWN;
					if(flags!=X509ChainStatusFlags.NoError) {
						return "the certificate "+x509Certificate2.Subject+" in the server's chain is refused: "+flags;
					}
				}
				X509ChainStatusFlags flagsChain=GetFlags(x509Chain.ChainStatus)&~REVOCATION_UNKNOWN;
				if(flagsChain!=X509ChainStatusFlags.NoError) {
					return "the server's certificate ("+subject+") is refused: "+flagsChain;
				}
				if(!IsHostNameChecked) {
					int errorPolicy=nativeChain.VerifySslPolicy(null);
					if(errorPolicy!=0) {
						return "the server's certificate ("+subject+") fails the TLS server policy (0x"+errorPolicy.ToString("X8")+")";
					}
					return "";
				}
				foreach(string host in ListHosts) {
					int errorPolicy=nativeChain.VerifySslPolicy(host);
					if(errorPolicy==0) {
						return "";
					}
					if(errorPolicy!=NativeChain.CERT_E_CN_NO_MATCH) {
						return "the server's certificate ("+subject+") fails the TLS server policy for "+host+" (0x"+errorPolicy.ToString("X8")+")";
					}
				}
				return "the server's certificate ("+subject+") does not name the host "+string.Join(" or ",ListHosts)
					+" (its subject alternative names: "+GetSubjectAlternativeNames(certificate)+")";
			}
		}

		///<summary>A host as the certificate must name it: trimmed, without square brackets around an address and without one trailing dot.
		///</summary>
		public static string NormalizeHost(string host) {
			host=(host??"").Trim();
			if(host.Length>=2 && host[0]=='[' && host[host.Length-1]==']') {
				host=host.Substring(1,host.Length-2);
			}
			if(host.EndsWith(".") && !host.EndsWith("..")) {
				host=host.Substring(0,host.Length-1);
			}
			return host;
		}

		///<summary>Reads the certificates of the office CA file into listCa: the CERTIFICATE blocks of a PEM file, or the whole file as one DER
		///certificate. Returns the refusal when the file is missing, unreadable or holds no certificate, "" otherwise.</summary>
		public static string LoadCaCertificates(string caPath,out List<X509Certificate2> listCa) {
			listCa=new List<X509Certificate2>();
			if(string.IsNullOrWhiteSpace(caPath)) {
				return "no office CA certificate file is set";
			}
			byte[] arrayBytes;
			try {
				arrayBytes=File.ReadAllBytes(caPath);
			}
			catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException
				|| ex is NotSupportedException || ex is System.Security.SecurityException)
			{
				return "the office CA certificate file "+caPath+" cannot be read ("+ex.Message+")";
			}
			try {
				MatchCollection matchCollection=_regexPemCertificate.Matches(Encoding.ASCII.GetString(arrayBytes));
				if(matchCollection.Count>0) {
					foreach(Match match in matchCollection) {
						listCa.Add(new X509Certificate2(Convert.FromBase64String(match.Groups["base64"].Value)));
					}
				}
				else {
					listCa.Add(new X509Certificate2(arrayBytes));
				}
			}
			catch(Exception ex) when(ex is CryptographicException || ex is FormatException) {
				listCa.Clear();
				return "the office CA certificate file "+caPath+" is not a certificate file (PEM or DER)";
			}
			return "";
		}

		private static X509ChainStatusFlags GetFlags(X509ChainStatus[] arrayStatus) {
			X509ChainStatusFlags flags=X509ChainStatusFlags.NoError;
			foreach(X509ChainStatus x509ChainStatus in arrayStatus??new X509ChainStatus[0]) {
				flags|=x509ChainStatus.Status;
			}
			return flags;
		}

		private static string GetStatusText(X509ChainStatus[] arrayStatus) {
			X509ChainStatusFlags flags=GetFlags(arrayStatus);
			return flags==X509ChainStatusFlags.NoError ? "no error" : flags.ToString();
		}

		private static string GetSubjectAlternativeNames(X509Certificate2 certificate) {
			X509Extension x509Extension=certificate.Extensions.Cast<X509Extension>().FirstOrDefault(x => x.Oid!=null && x.Oid.Value=="2.5.29.17");
			if(x509Extension==null) {
				return "none";
			}
			return new AsnEncodedData(x509Extension.Oid,x509Extension.RawData).Format(false);
		}

		///<summary>A chain built by Windows' chain engine with an exclusive root store, and its SSL policy check. Owns the native chain context,
		///the engine and the stores; Dispose frees them.</summary>
		private sealed class NativeChain:IDisposable {
			public const int CERT_E_CN_NO_MATCH=unchecked((int)0x800B010F);
			private const int X509_ASN_ENCODING=0x00000001;
			private const int PKCS_7_ASN_ENCODING=0x00010000;
			private const int CERT_STORE_ADD_REPLACE_EXISTING=3;
			private const int CERT_STORE_PROV_MEMORY=2;
			private const int CERT_CHAIN_DISABLE_AIA=0x00002000;
			private const int CERT_CHAIN_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT=0x40000000;
			private const int CERT_CHAIN_REVOCATION_CHECK_CACHE_ONLY=unchecked((int)0x80000000);
			private const int CERT_CHAIN_POLICY_SSL=4;
			private const int CERT_CHAIN_POLICY_IGNORE_ALL_REV_UNKNOWN_FLAGS=0x00000F00;
			private const int AUTHTYPE_SERVER=2;
			private const int SECURITY_FLAG_IGNORE_CERT_CN_INVALID=0x00001000;
			private const int USAGE_MATCH_TYPE_AND=0;
			private IntPtr _hStoreRoot=IntPtr.Zero;
			private IntPtr _hStoreAdditional=IntPtr.Zero;
			private IntPtr _hEngine=IntPtr.Zero;
			private IntPtr _pChainContext=IntPtr.Zero;
			public X509Chain X509Chain { get; private set; }

			public static NativeChain Build(X509Certificate2 certificate,List<X509Certificate2> listRoots,List<X509Certificate2> listIntermediates,
				List<byte[]> listCrls,X509RevocationMode revocationMode)
			{
				NativeChain nativeChain=new NativeChain();
				try {
					nativeChain.BuildChain(certificate,listRoots,listIntermediates,listCrls,revocationMode);
					return nativeChain;
				}
				catch {
					nativeChain.Dispose();
					throw;
				}
			}

			private void BuildChain(X509Certificate2 certificate,List<X509Certificate2> listRoots,List<X509Certificate2> listIntermediates,
				List<byte[]> listCrls,X509RevocationMode revocationMode)
			{
				_hStoreRoot=OpenMemoryStore();
				_hStoreAdditional=OpenMemoryStore();
				foreach(X509Certificate2 x509Certificate2 in listRoots) {
					AddCertificate(_hStoreRoot,x509Certificate2);
					AddCertificate(_hStoreAdditional,x509Certificate2);//A non-root certificate of the CA file may be an intermediate, as with SslCa.
				}
				foreach(X509Certificate2 x509Certificate2 in listIntermediates) {
					AddCertificate(_hStoreAdditional,x509Certificate2);
				}
				foreach(byte[] arrayCrl in listCrls??new List<byte[]>()) {
					if(!CertAddEncodedCRLToStore(_hStoreAdditional,X509_ASN_ENCODING|PKCS_7_ASN_ENCODING,arrayCrl,arrayCrl.Length,
						CERT_STORE_ADD_REPLACE_EXISTING,IntPtr.Zero))
					{
						throw new Win32Exception(Marshal.GetLastWin32Error(),"A CRL could not be read.");
					}
				}
				CERT_CHAIN_ENGINE_CONFIG certChainEngineConfig=new CERT_CHAIN_ENGINE_CONFIG();
				certChainEngineConfig.cbSize=Marshal.SizeOf(typeof(CERT_CHAIN_ENGINE_CONFIG));
				certChainEngineConfig.hExclusiveRoot=_hStoreRoot;
				if(!CertCreateCertificateChainEngine(ref certChainEngineConfig,out _hEngine)) {
					_hEngine=IntPtr.Zero;
					throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows' certificate chain engine could not be made.");
				}
				int flags=CERT_CHAIN_DISABLE_AIA;
				if(revocationMode==X509RevocationMode.Online) {
					flags|=CERT_CHAIN_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT;
				}
				else if(revocationMode==X509RevocationMode.Offline) {
					flags|=CERT_CHAIN_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT|CERT_CHAIN_REVOCATION_CHECK_CACHE_ONLY;
				}
				IntPtr pOid=Marshal.StringToHGlobalAnsi(OID_SERVER_AUTHENTICATION);
				IntPtr pArrayOids=Marshal.AllocHGlobal(IntPtr.Size);
				try {
					Marshal.WriteIntPtr(pArrayOids,pOid);
					CERT_CHAIN_PARA certChainPara=new CERT_CHAIN_PARA();
					certChainPara.cbSize=Marshal.SizeOf(typeof(CERT_CHAIN_PARA));
					certChainPara.dwType=USAGE_MATCH_TYPE_AND;
					certChainPara.cUsageIdentifier=1;
					certChainPara.rgpszUsageIdentifier=pArrayOids;
					if(!CertGetCertificateChain(_hEngine,certificate.Handle,IntPtr.Zero,_hStoreAdditional,ref certChainPara,flags,IntPtr.Zero,
						out _pChainContext))
					{
						_pChainContext=IntPtr.Zero;
						throw new Win32Exception(Marshal.GetLastWin32Error(),"The server's certificate chain could not be built.");
					}
				}
				finally {
					Marshal.FreeHGlobal(pArrayOids);
					Marshal.FreeHGlobal(pOid);
				}
				X509Chain=new X509Chain(_pChainContext);
				GC.KeepAlive(certificate);
			}

			///<summary>CertVerifyCertificateChainPolicy with the SSL policy for a server: 0 when it passes, otherwise its error (CERT_E_CN_NO_MATCH
			///when the certificate does not name host). host null: the name is not checked. An unknown revocation status is not an error.</summary>
			public int VerifySslPolicy(string host) {
				SSL_EXTRA_CERT_CHAIN_POLICY_PARA sslExtraCertChainPolicyPara=new SSL_EXTRA_CERT_CHAIN_POLICY_PARA();
				sslExtraCertChainPolicyPara.cbSize=Marshal.SizeOf(typeof(SSL_EXTRA_CERT_CHAIN_POLICY_PARA));
				sslExtraCertChainPolicyPara.dwAuthType=AUTHTYPE_SERVER;
				sslExtraCertChainPolicyPara.fdwChecks=host==null ? SECURITY_FLAG_IGNORE_CERT_CN_INVALID : 0;
				sslExtraCertChainPolicyPara.pwszServerName=host;
				IntPtr pExtra=Marshal.AllocHGlobal(sslExtraCertChainPolicyPara.cbSize);
				try {
					Marshal.StructureToPtr(sslExtraCertChainPolicyPara,pExtra,false);
					try {
						CERT_CHAIN_POLICY_PARA certChainPolicyPara=new CERT_CHAIN_POLICY_PARA();
						certChainPolicyPara.cbSize=Marshal.SizeOf(typeof(CERT_CHAIN_POLICY_PARA));
						certChainPolicyPara.dwFlags=CERT_CHAIN_POLICY_IGNORE_ALL_REV_UNKNOWN_FLAGS;
						certChainPolicyPara.pvExtraPolicyPara=pExtra;
						CERT_CHAIN_POLICY_STATUS certChainPolicyStatus=new CERT_CHAIN_POLICY_STATUS();
						certChainPolicyStatus.cbSize=Marshal.SizeOf(typeof(CERT_CHAIN_POLICY_STATUS));
						if(!CertVerifyCertificateChainPolicy((IntPtr)CERT_CHAIN_POLICY_SSL,_pChainContext,ref certChainPolicyPara,ref certChainPolicyStatus)) {
							throw new Win32Exception(Marshal.GetLastWin32Error(),"The TLS server policy could not be checked.");
						}
						return certChainPolicyStatus.dwError;
					}
					finally {
						Marshal.DestroyStructure(pExtra,typeof(SSL_EXTRA_CERT_CHAIN_POLICY_PARA));
					}
				}
				finally {
					Marshal.FreeHGlobal(pExtra);
				}
			}

			public void Dispose() {
				if(X509Chain!=null) {
					X509Chain.Reset();
					X509Chain=null;
				}
				if(_pChainContext!=IntPtr.Zero) {
					CertFreeCertificateChain(_pChainContext);
					_pChainContext=IntPtr.Zero;
				}
				if(_hEngine!=IntPtr.Zero) {
					CertFreeCertificateChainEngine(_hEngine);
					_hEngine=IntPtr.Zero;
				}
				if(_hStoreAdditional!=IntPtr.Zero) {
					CertCloseStore(_hStoreAdditional,0);
					_hStoreAdditional=IntPtr.Zero;
				}
				if(_hStoreRoot!=IntPtr.Zero) {
					CertCloseStore(_hStoreRoot,0);
					_hStoreRoot=IntPtr.Zero;
				}
			}

			private static IntPtr OpenMemoryStore() {
				IntPtr hStore=CertOpenStore((IntPtr)CERT_STORE_PROV_MEMORY,0,IntPtr.Zero,0,IntPtr.Zero);
				if(hStore==IntPtr.Zero) {
					throw new Win32Exception(Marshal.GetLastWin32Error(),"A certificate store in memory could not be made.");
				}
				return hStore;
			}

			private static void AddCertificate(IntPtr hStore,X509Certificate2 x509Certificate2) {
				byte[] arrayBytes=x509Certificate2.RawData;
				if(!CertAddEncodedCertificateToStore(hStore,X509_ASN_ENCODING|PKCS_7_ASN_ENCODING,arrayBytes,arrayBytes.Length,
					CERT_STORE_ADD_REPLACE_EXISTING,IntPtr.Zero))
				{
					throw new Win32Exception(Marshal.GetLastWin32Error(),"A certificate could not be read.");
				}
			}

			///<summary>CERT_CHAIN_ENGINE_CONFIG with the members of Windows 8 and later (hExclusiveRoot, hExclusiveTrustedPeople,
			///dwExclusiveFlags). Without CERT_CHAIN_EXCLUSIVE_ENABLE_CA_FLAG only the self-signed certificates of hExclusiveRoot are roots.</summary>
			[StructLayout(LayoutKind.Sequential)]
			private struct CERT_CHAIN_ENGINE_CONFIG {
				public int cbSize;
				public IntPtr hRestrictedRoot;
				public IntPtr hRestrictedTrust;
				public IntPtr hRestrictedOther;
				public int cAdditionalStore;
				public IntPtr rghAdditionalStore;
				public int dwFlags;
				public int dwUrlRetrievalTimeout;
				public int MaximumCachedCertificates;
				public int CycleDetectionModulus;
				public IntPtr hExclusiveRoot;
				public IntPtr hExclusiveTrustedPeople;
				public int dwExclusiveFlags;
			}

			///<summary>CERT_CHAIN_PARA up to RequestedUsage (a CERT_USAGE_MATCH: dwType and a CERT_ENHKEY_USAGE).</summary>
			[StructLayout(LayoutKind.Sequential)]
			private struct CERT_CHAIN_PARA {
				public int cbSize;
				public int dwType;
				public int cUsageIdentifier;
				public IntPtr rgpszUsageIdentifier;
			}

			[StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
			private struct SSL_EXTRA_CERT_CHAIN_POLICY_PARA {
				public int cbSize;
				public int dwAuthType;
				public int fdwChecks;
				public string pwszServerName;
			}

			[StructLayout(LayoutKind.Sequential)]
			private struct CERT_CHAIN_POLICY_PARA {
				public int cbSize;
				public int dwFlags;
				public IntPtr pvExtraPolicyPara;
			}

			[StructLayout(LayoutKind.Sequential)]
			private struct CERT_CHAIN_POLICY_STATUS {
				public int cbSize;
				public int dwError;
				public int lChainIndex;
				public int lElementIndex;
				public IntPtr pvExtraPolicyStatus;
			}

			[DllImport("crypt32.dll",SetLastError=true)]
			private static extern bool CertCreateCertificateChainEngine(ref CERT_CHAIN_ENGINE_CONFIG pConfig,out IntPtr phChainEngine);

			[DllImport("crypt32.dll")]
			private static extern void CertFreeCertificateChainEngine(IntPtr hChainEngine);

			[DllImport("crypt32.dll",SetLastError=true)]
			private static extern bool CertGetCertificateChain(IntPtr hChainEngine,IntPtr pCertContext,IntPtr pTime,IntPtr hAdditionalStore,
				ref CERT_CHAIN_PARA pChainPara,int dwFlags,IntPtr pvReserved,out IntPtr ppChainContext);

			[DllImport("crypt32.dll")]
			private static extern void CertFreeCertificateChain(IntPtr pChainContext);

			[DllImport("crypt32.dll",SetLastError=true)]
			private static extern bool CertVerifyCertificateChainPolicy(IntPtr pszPolicyOID,IntPtr pChainContext,ref CERT_CHAIN_POLICY_PARA pPolicyPara,
				ref CERT_CHAIN_POLICY_STATUS pPolicyStatus);

			[DllImport("crypt32.dll",SetLastError=true)]
			private static extern IntPtr CertOpenStore(IntPtr lpszStoreProvider,int dwEncodingType,IntPtr hCryptProv,int dwFlags,IntPtr pvPara);

			[DllImport("crypt32.dll",SetLastError=true)]
			private static extern bool CertCloseStore(IntPtr hCertStore,int dwFlags);

			[DllImport("crypt32.dll",SetLastError=true)]
			private static extern bool CertAddEncodedCertificateToStore(IntPtr hCertStore,int dwCertEncodingType,byte[] pbCertEncoded,int cbCertEncoded,
				int dwAddDisposition,IntPtr ppCertContext);

			[DllImport("crypt32.dll",SetLastError=true)]
			private static extern bool CertAddEncodedCRLToStore(IntPtr hCertStore,int dwCertEncodingType,byte[] pbCrlEncoded,int cbCrlEncoded,
				int dwAddDisposition,IntPtr ppCrlContext);
		}
	}
}
