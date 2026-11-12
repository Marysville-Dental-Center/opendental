// The program's own check of a database server's certificate (OfficeCaServerVerifier, DB-SEC 3 decided as (c),
// docs/specs/db-connection-security.md section 8; docs/port-notes/S2-2.md section 3.6), called directly with throwaway certificates: the
// chain to exactly the office CA, intermediates, dates, the host name (subject alternative names, IP addresses, the common name only without
// them), the server-authentication usage, and revocation with a CRL made in the test. Nothing is added to a certificate store of this
// computer and nothing reaches the network (every revocation case gives its CRL to the check; the certificates have no CRL distribution point).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class OfficeCaServerVerifierTests {
		private const string HOST="db.example.test";
		private static readonly IPAddress _ipAddress=IPAddress.Parse(TestHelpers.NETWORK_ADDRESS);
		private string _folder;
		private TestCertificateAuthority _caOffice;
		private string _pathOfficeCa;

		[TestInitialize]
		public void SetUp() {
			_folder=TestHelpers.NewTempFolder();
			_caOffice=TestCertificateAuthority.CreateRoot("CN=Synthetic Office CA "+Guid.NewGuid().ToString("N"));
			_pathOfficeCa=_caOffice.WritePem(_folder,"office-ca.pem");
		}

		[TestCleanup]
		public void CleanUp() {
			_caOffice.Dispose();
			TestHelpers.DeleteFolder(_folder);
		}

		///<summary>The check of a VerifyFull connection to server (the program's own account over the network), or of a VerifyCA connection
		///(a read-only or reporting server with a CA preference) when isHostNameChecked is false.</summary>
		private OfficeCaServerVerifier NewVerifier(string server=HOST,bool isHostNameChecked=true,string caPath=null) {
			return new OfficeCaServerVerifier(caPath??_pathOfficeCa,server,isHostNameChecked);
		}

		private static void AssertAccepted(OfficeCaServerVerifier verifier,X509Certificate2 certificate,params X509Certificate2[] arrayIntermediates) {
			Assert.AreEqual("",verifier.GetRefusal(certificate,arrayIntermediates),"Accepted.");
		}

		private static string AssertRefused(OfficeCaServerVerifier verifier,X509Certificate2 certificate,string partOfRefusal,
			params X509Certificate2[] arrayIntermediates)
		{
			string refusal=verifier.GetRefusal(certificate,arrayIntermediates);
			Assert.AreNotEqual("",refusal,"Refused.");
			StringAssert.Contains(refusal,partOfRefusal);
			return refusal;
		}

		[TestMethod]
		public void ServerCertificateOfTheOfficeCa_ThatNamesTheHost_IsAccepted() {
			X509Certificate2 certificate=_caOffice.Issue("CN="+HOST,new[] { HOST },null);
			OfficeCaServerVerifier verifier=NewVerifier();
			Assert.IsTrue(verifier.IsHostNameChecked);
			Assert.AreEqual(X509RevocationMode.Online,verifier.RevocationMode,"VerifyFull: the online revocation check the driver asked SslStream for.");
			AssertAccepted(verifier,certificate);
		}

		[TestMethod]
		public void ChainToAnotherRoot_IsRefused() {
			using(TestCertificateAuthority caOther=TestCertificateAuthority.CreateRoot("CN=Synthetic Other CA "+Guid.NewGuid().ToString("N"))) {
				X509Certificate2 certificate=caOther.Issue("CN="+HOST,new[] { HOST },null);
				AssertRefused(NewVerifier(),certificate,"does not chain to the office CA");
				//The server sending its own root with it changes nothing: only the office CA file's certificates are roots.
				AssertRefused(NewVerifier(),certificate,"does not chain to the office CA",caOther.GetPublic());
				//Under VerifyCA the chain is still required.
				AssertRefused(NewVerifier(isHostNameChecked:false),certificate,"does not chain to the office CA",caOther.GetPublic());
			}
		}

		[TestMethod]
		public void SelfSignedServerCertificate_IsRefused() {
			X509Certificate2 certificate=TestHelpers.NewCertificate("CN="+HOST,isCa:false,isServerAuthOnly:true);
			AssertRefused(NewVerifier(),new X509Certificate2(certificate.RawData),"does not chain to the office CA");
		}

		[TestMethod]
		public void AnotherCaWithTheOfficeCasName_IsRefused() {
			//The same subject as the office CA but another key: the root must be the office CA's certificate byte for byte, not a name.
			using(TestCertificateAuthority caImpostor=TestCertificateAuthority.CreateRoot(_caOffice.Certificate.Subject)) {
				X509Certificate2 certificate=caImpostor.Issue("CN="+HOST,new[] { HOST },null);
				//Windows either ends the chain at the impostor ("does not chain to the office CA") or at the office CA with a signature that does
				//not verify ("NotSignatureValid"); both refuse, and both name the server's certificate.
				AssertRefused(NewVerifier(),certificate,"CN="+HOST,caImpostor.GetPublic());
			}
		}

		[TestMethod]
		public void IntermediateMissing_IsRefused_SentOrInTheCaFile_IsAccepted() {
			using(TestCertificateAuthority caIntermediate=_caOffice.CreateIntermediate("CN=Synthetic Office Intermediate "+Guid.NewGuid().ToString("N"))) {
				X509Certificate2 certificate=caIntermediate.Issue("CN="+HOST,new[] { HOST },null);
				AssertRefused(NewVerifier(),certificate,"does not chain to the office CA");
				AssertAccepted(NewVerifier(),certificate,caIntermediate.GetPublic());
				//The intermediate in the office CA file, as SslCa allowed: a certificate of the file that is not self-signed is never a root, but
				//it may complete the chain.
				string pathWithIntermediate=_caOffice.WritePem(_folder,"office-ca-and-intermediate.pem",caIntermediate.GetPublic());
				AssertAccepted(NewVerifier(caPath:pathWithIntermediate),certificate);
				//A CA file that holds only the intermediate is not the office CA: the chain must end at a root of the file.
				string pathIntermediateOnly=caIntermediate.WritePem(_folder,"intermediate-only.pem");
				AssertRefused(NewVerifier(caPath:pathIntermediateOnly),certificate,"does not chain to the office CA",caIntermediate.GetPublic());
			}
		}

		[TestMethod]
		public void Expired_IsRefused() {
			X509Certificate2 certificate=_caOffice.Issue("CN="+HOST,new[] { HOST },null,notBefore:DateTimeOffset.UtcNow.AddDays(-60),
				notAfter:DateTimeOffset.UtcNow.AddDays(-1));
			AssertRefused(NewVerifier(),certificate,"expired on");
			AssertRefused(NewVerifier(isHostNameChecked:false),certificate,"expired on");
		}

		[TestMethod]
		public void NotYetValid_IsRefused() {
			X509Certificate2 certificate=_caOffice.Issue("CN="+HOST,new[] { HOST },null,notBefore:DateTimeOffset.UtcNow.AddDays(2),
				notAfter:DateTimeOffset.UtcNow.AddDays(60));
			AssertRefused(NewVerifier(),certificate,"is not valid before");
		}

		[TestMethod]
		public void WrongName_IsRefused_UnderVerifyFull_NotChecked_UnderVerifyCA() {
			X509Certificate2 certificate=_caOffice.Issue("CN=other.example.test",new[] { "other.example.test" },null);
			string refusal=AssertRefused(NewVerifier(),certificate,"does not name the host "+HOST);
			StringAssert.Contains(refusal,"other.example.test");
			AssertAccepted(NewVerifier(isHostNameChecked:false),certificate);
			Assert.AreEqual(X509RevocationMode.NoCheck,NewVerifier(isHostNameChecked:false).RevocationMode,"VerifyCA: the driver asked for no revocation check.");
		}

		[TestMethod]
		public void SubjectAlternativeNames_Decide_TheCommonNameIsNotUsedWithThem() {
			//The common name names the host, the subject alternative names do not: refused, as VerifyFull (Windows' SSL policy) did.
			X509Certificate2 certificate=_caOffice.Issue("CN="+HOST,new[] { "other.example.test" },null);
			AssertRefused(NewVerifier(),certificate,"does not name the host "+HOST);
			AssertAccepted(NewVerifier("other.example.test"),certificate);
			//A name only in the subject alternative names, with a common name that is no host at all.
			X509Certificate2 certificateSanOnly=_caOffice.Issue("CN=Synthetic Database Server",new[] { "db2.example.test",HOST },null);
			AssertAccepted(NewVerifier(),certificateSanOnly);
			AssertAccepted(NewVerifier("DB.Example.Test"),certificateSanOnly);
		}

		[TestMethod]
		public void WithoutSubjectAlternativeNames_TheCommonNameIsUsed() {
			X509Certificate2 certificate=_caOffice.Issue("CN="+HOST,null,null);
			AssertAccepted(NewVerifier(),certificate);
			AssertRefused(NewVerifier("other.example.test"),certificate,"does not name the host other.example.test");
		}

		[TestMethod]
		public void IpAddressSubjectAlternativeName_IsAcceptedForThatAddressOnly() {
			X509Certificate2 certificate=_caOffice.Issue("CN="+HOST,new[] { HOST },new[] { _ipAddress });
			AssertAccepted(NewVerifier(TestHelpers.NETWORK_ADDRESS),certificate);
			AssertRefused(NewVerifier("192.0.2.11"),certificate,"does not name the host 192.0.2.11");
			//A certificate with DNS names only does not name an address.
			X509Certificate2 certificateDnsOnly=_caOffice.Issue("CN="+HOST,new[] { HOST },null);
			AssertRefused(NewVerifier(TestHelpers.NETWORK_ADDRESS),certificateDnsOnly,"does not name the host "+TestHelpers.NETWORK_ADDRESS);
		}

		[TestMethod]
		public void HostList_OneHostIsEnough_TrailingDotAndBracketsAreLeftOut() {
			X509Certificate2 certificate=_caOffice.Issue("CN="+HOST,new[] { HOST },null);
			AssertAccepted(NewVerifier("db-old.example.test,"+HOST),certificate);
			AssertAccepted(NewVerifier(HOST+"."),certificate);
			CollectionAssert.AreEqual(new[] { "db-old.example.test",HOST,"2001:db8::1" },
				new OfficeCaServerVerifier(_pathOfficeCa," db-old.example.test, "+HOST+".,[2001:db8::1]",true).ListHosts);
			AssertRefused(NewVerifier(""),certificate,"names no host");
		}

		[TestMethod]
		public void ClientCertificateOfTheOfficeCa_IsNotAServerCertificate() {
			//The shared client certificate of every workstation is signed by the office CA too; it must never pass as the server.
			X509Certificate2 certificate=_caOffice.Issue("CN="+HOST,new[] { HOST },null,isServerAuth:false,isClientAuth:true);
			AssertRefused(NewVerifier(),certificate,"NotValidForUsage");
			AssertRefused(NewVerifier(isHostNameChecked:false),certificate,"NotValidForUsage");
			//Without any extended key usage a certificate is valid for every use, as Windows reads it.
			AssertAccepted(NewVerifier(),_caOffice.Issue("CN="+HOST,new[] { HOST },null,isServerAuth:false,isClientAuth:false));
		}

		[TestMethod]
		public void Revoked_IsRefused_ByTheCrlOfTheOfficeCa() {
			byte[] arraySerial;
			X509Certificate2 certificate=_caOffice.Issue("CN="+HOST,new[] { HOST },null,out arraySerial);
			foreach(X509RevocationMode revocationMode in new[] { X509RevocationMode.Offline,X509RevocationMode.Online }) {
				OfficeCaServerVerifier verifierRevoked=NewVerifier();
				verifierRevoked.RevocationMode=revocationMode;
				verifierRevoked.ListCrls.Add(_caOffice.CreateCrl(arraySerial));
				AssertRefused(verifierRevoked,certificate,"Revoked");
				OfficeCaServerVerifier verifierGood=NewVerifier();
				verifierGood.RevocationMode=revocationMode;
				verifierGood.ListCrls.Add(_caOffice.CreateCrl());//The CRL revokes nothing.
				AssertAccepted(verifierGood,certificate);
			}
		}

		[TestMethod]
		public void RevokedIntermediate_IsRefused() {
			using(TestCertificateAuthority caIntermediate=_caOffice.CreateIntermediate("CN=Synthetic Office Intermediate "+Guid.NewGuid().ToString("N"))) {
				X509Certificate2 certificate=caIntermediate.Issue("CN="+HOST,new[] { HOST },null);
				OfficeCaServerVerifier verifier=NewVerifier();
				verifier.RevocationMode=X509RevocationMode.Offline;
				verifier.ListCrls.Add(_caOffice.CreateCrl(GetSerial(caIntermediate.Certificate)));
				verifier.ListCrls.Add(caIntermediate.CreateCrl());
				AssertRefused(verifier,certificate,"Revoked",caIntermediate.GetPublic());
			}
		}

		[TestMethod]
		public void RevocationUnknown_IsNotARefusal() {
			//No CRL anywhere (the server guide's certificates have no CRL distribution point): Windows reports the status as unknown, which the
			//driver never refused for either. Online as the program runs it; without a distribution point nothing is fetched.
			X509Certificate2 certificate=_caOffice.Issue("CN="+HOST,new[] { HOST },null);
			AssertAccepted(NewVerifier(),certificate);
			OfficeCaServerVerifier verifierOffline=NewVerifier();
			verifierOffline.RevocationMode=X509RevocationMode.Offline;
			AssertAccepted(verifierOffline,certificate);
			//A CRL of another CA says nothing about this certificate.
			using(TestCertificateAuthority caOther=TestCertificateAuthority.CreateRoot("CN=Synthetic Other CA "+Guid.NewGuid().ToString("N"))) {
				OfficeCaServerVerifier verifierOtherCrl=NewVerifier();
				verifierOtherCrl.RevocationMode=X509RevocationMode.Offline;
				verifierOtherCrl.ListCrls.Add(caOther.CreateCrl(GetSerial(certificate)));
				AssertAccepted(verifierOtherCrl,certificate);
			}
		}

		[TestMethod]
		public void UnderVerifyCA_RevocationIsNotChecked_AsTheDriverDid() {
			byte[] arraySerial;
			X509Certificate2 certificate=_caOffice.Issue("CN="+HOST,new[] { HOST },null,out arraySerial);
			OfficeCaServerVerifier verifier=NewVerifier(isHostNameChecked:false);
			verifier.ListCrls.Add(_caOffice.CreateCrl(arraySerial));
			AssertAccepted(verifier,certificate);
		}

		[TestMethod]
		public void CaFileMissingOrNotACertificate_IsRefused() {
			X509Certificate2 certificate=_caOffice.Issue("CN="+HOST,new[] { HOST },null);
			AssertRefused(NewVerifier(caPath:Path.Combine(_folder,"missing.pem")),certificate,"cannot be read");
			string pathText=Path.Combine(_folder,"not-a-certificate.pem");
			File.WriteAllText(pathText,"not a certificate");
			AssertRefused(NewVerifier(caPath:pathText),certificate,"is not a certificate file");
			AssertRefused(new OfficeCaServerVerifier("",HOST,true),certificate,"no office CA certificate file");
			//One DER certificate is read as the driver read SslCa.
			string pathDer=Path.Combine(_folder,"office-ca.cer");
			File.WriteAllBytes(pathDer,_caOffice.Certificate.RawData);
			AssertAccepted(NewVerifier(caPath:pathDer),certificate);
		}

		[TestMethod]
		public void Callback_ReturnsFalseForARefusal_AndKeepsTheReason_NeverThrows() {
			OfficeCaServerVerifier verifier=NewVerifier();
			X509Certificate2 certificateGood=_caOffice.Issue("CN="+HOST,new[] { HOST },null);
			//sslPolicyErrors, SslStream's own result against the Windows stores, decides nothing: the office CA is not a Windows root.
			Assert.IsTrue(verifier.ValidateServerCertificate(null,certificateGood,null,SslPolicyErrors.RemoteCertificateChainErrors));
			Assert.AreEqual("",verifier.LastRefusal);
			X509Certificate2 certificateWrongName=_caOffice.Issue("CN=other.example.test",new[] { "other.example.test" },null);
			Assert.IsFalse(verifier.ValidateServerCertificate(null,certificateWrongName,null,SslPolicyErrors.None));
			StringAssert.Contains(verifier.LastRefusal,"does not name the host");
			Assert.IsFalse(verifier.ValidateServerCertificate(null,null,null,SslPolicyErrors.RemoteCertificateNotAvailable));
			StringAssert.Contains(verifier.LastRefusal,"no certificate");
			//The certificates SslStream's chain holds (what the server sent) may complete the chain, never root it.
			using(TestCertificateAuthority caIntermediate=_caOffice.CreateIntermediate("CN=Synthetic Office Intermediate "+Guid.NewGuid().ToString("N"))) {
				X509Certificate2 certificateFromIntermediate=caIntermediate.Issue("CN="+HOST,new[] { HOST },null);
				using(X509Chain x509Chain=new X509Chain()) {
					x509Chain.ChainPolicy.ExtraStore.Add(caIntermediate.GetPublic());
					Assert.IsTrue(verifier.ValidateServerCertificate(null,certificateFromIntermediate,x509Chain,SslPolicyErrors.RemoteCertificateChainErrors),
						verifier.LastRefusal);
				}
			}
		}

		///<summary>The serial number of a certificate, as the CRL lists it (big-endian bytes).</summary>
		private static byte[] GetSerial(X509Certificate2 certificate) {
			byte[] arraySerial=certificate.GetSerialNumber();//little-endian
			Array.Reverse(arraySerial);
			return arraySerial;
		}
	}
}
