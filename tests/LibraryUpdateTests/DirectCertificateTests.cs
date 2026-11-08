// Direct secure messaging's certificates and trust (package S0-3D of the .NET 10 plan: Direct is kept, owner decision 2026-11-08), without a
// database: the resolvers the Direct agent uses (EmailPrivateResolver, EmailPublicResolver, which go through EmailCertificates), the trust
// count and checks (GetReceiverUntrustedCount, IsSenderTrusted), adding trust for a received signature (TryAddTrustForSignature) and for a
// Direct address whose certificate is hosted (TryAddTrustDirect, FindPublicCertForAddress), the three stores CreateCertificateStoresIfNeeded
// makes, and the e-mail preview's parts of an encrypted message, which the agent decrypts first. Every store is a store of the current user made
// for the test and deleted afterwards (DirectTestStores); the DNS and LDAP search is stood in for, so no test reaches the internet. Synthetic
// addresses only. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography.X509Certificates;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace LibraryUpdateTests {
	[TestClass]
	public class DirectCertificateTests {
		private const string INBOX="inbox@example.test";
		private const string SENDER="sender@example.test";

		[TestMethod]
		public void Resolvers_FindTheValidCertificateOfTheAddress() {
			DirectTestStores directTestStores=new DirectTestStores();
			try {
				X509Certificate2 x509Certificate2Valid=directTestStores.CreateCertificate("pat.q@hisp.example.test");
				X509Certificate2 x509Certificate2Expired=directTestStores.CreateCertificate("pat.q@hisp.example.test",DateTimeOffset.UtcNow.AddDays(-3));
				X509Certificate2 x509Certificate2Other=directTestStores.CreateCertificate("front@clinic.example.test");
				EmailMessages.CreateCertificateStoresIfNeeded();//As the program does when it starts.
				directTestStores.AddPrivate(x509Certificate2Expired);
				directTestStores.AddPrivate(x509Certificate2Valid);
				directTestStores.AddPrivate(x509Certificate2Other);
				X509Certificate2Collection x509Certificate2Collection=new EmailPrivateResolver().GetCertificates(new MailAddress("Pat Q <PAT.Q@hisp.example.test>"));
				Assert.AreEqual(1,x509Certificate2Collection.Count,"The valid certificate of the address only; the expired one is left out.");
				Assert.AreEqual(x509Certificate2Valid.Thumbprint,x509Certificate2Collection[0].Thumbprint);
				Assert.IsTrue(x509Certificate2Collection[0].HasPrivateKey,"The private store gives the key with the certificate.");
				Assert.IsNull(new EmailPrivateResolver().GetCertificates(new MailAddress("someone@other.example.test")),"Nothing for another address.");
				Assert.AreEqual(x509Certificate2Valid.Thumbprint,EmailMessages.GetCertFromPrivateStore("pat.q@hisp.example.test")?.Thumbprint,
					"The signing certificate comes from the same store.");
				//The public store: none yet, then an expired one (untrusted count 1), then a valid one (-1: trusted certificate known).
				Assert.AreEqual(0,EmailMessages.GetReceiverUntrustedCount("pat.q@hisp.example.test"));
				directTestStores.AddExternal(x509Certificate2Expired);
				Assert.AreEqual(1,EmailMessages.GetReceiverUntrustedCount("pat.q@hisp.example.test"));
				directTestStores.AddExternal(x509Certificate2Valid);
				Assert.AreEqual(-1,EmailMessages.GetReceiverUntrustedCount("pat.q@hisp.example.test"));
				Assert.AreEqual(1,new EmailPublicResolver().GetCertificatesForAddress("pat.q@hisp.example.test").Count);
			}
			finally {
				directTestStores.Dispose();
			}
			Assert.IsTrue(directTestStores.AreStoresDeleted(),"The test stores are deleted.");
		}

		///<summary>The signature window's Trust button: the signer's certificate goes to the public store and the trust anchors.</summary>
		[TestMethod]
		public void TryAddTrustForSignature_StoresAndTrustsTheSigner() {
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				X509Certificate2 x509Certificate2=directTestStores.CreateCertificate(SENDER);
				EmailMessages.CreateCertificateStoresIfNeeded();//As the program does when it starts.
				Assert.AreNotEqual(-1,EmailMessages.GetReceiverUntrustedCount(SENDER));
				EmailMessages.TryAddTrustForSignature(DirectTestStores.Public(x509Certificate2));
				CollectionAssert.Contains(directTestStores.GetThumbprints(directTestStores.NameExternal),x509Certificate2.Thumbprint);
				CollectionAssert.Contains(directTestStores.GetThumbprints(directTestStores.NameAnchor),x509Certificate2.Thumbprint);
				Assert.AreEqual(-1,EmailMessages.GetReceiverUntrustedCount(SENDER),"Trusted now (the signature window shows Trusted).");
			}
		}

		///<summary>The inbox's trust check for an address of the office (its private certificate against the trust anchors).</summary>
		[TestMethod]
		public void IsSenderTrusted_IsTheAnchorOfThePrivateCertificate() {
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				X509Certificate2 x509Certificate2=directTestStores.CreateCertificate(INBOX);
				directTestStores.AddPrivate(x509Certificate2);
				EmailMessages.CreateCertificateStoresIfNeeded();
				Assert.IsFalse(EmailMessages.IsSenderTrusted(INBOX));
				Assert.IsFalse(EmailMessages.IsSenderTrusted(""));
				Assert.IsFalse(EmailMessages.IsSenderTrusted("someone@other.example.test"),"No private certificate for the address: not trusted.");
				directTestStores.AddAnchor(x509Certificate2);
				Assert.IsTrue(EmailMessages.IsSenderTrusted(INBOX));
			}
		}

		///<summary>The referral window's "E-mail Trust for Direct" and every Direct send: a certificate hosted for a Direct address (DNS or LDAP,
		///stood in for here) replaces the address's certificates in the public store and is trusted; an address with nothing hosted is a standard
		///encrypted address and keeps what the store has.</summary>
		[TestMethod]
		public void TryAddTrustDirect_StoresAndTrustsTheHostedCertificate() {
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				X509Certificate2 x509Certificate2Old=directTestStores.CreateCertificate("pat@hisp.example.test");
				X509Certificate2 x509Certificate2Hosted=directTestStores.CreateCertificate("pat@hisp.example.test");
				directTestStores.AddExternal(x509Certificate2Old);
				directTestStores.AddAnchor(x509Certificate2Old);
				directTestStores.Host("pat@hisp.example.test",x509Certificate2Hosted);
				EmailMessages.CreateCertificateStoresIfNeeded();//As the program does when it starts: the agent needs all three stores.
				Assert.IsTrue(EmailMessages.TryAddTrustDirect("pat@hisp.example.test"));
				CollectionAssert.AreEqual(new List<string> { "pat@hisp.example.test" },directTestStores.ListHostedLookups);
				CollectionAssert.AreEqual(new List<string> { x509Certificate2Hosted.Thumbprint },directTestStores.GetThumbprints(directTestStores.NameExternal),
					"The hosted certificate replaced the stored one of the address.");
				CollectionAssert.AreEqual(new List<string> { x509Certificate2Hosted.Thumbprint },directTestStores.GetThumbprints(directTestStores.NameAnchor));
				Assert.AreEqual(-1,EmailMessages.GetReceiverUntrustedCount("pat@hisp.example.test"));
				//Nothing hosted and nothing stored: trust can't be added, the address stays unknown.
				Assert.IsTrue(EmailMessages.TryAddTrustDirect("someone@other.example.test"));
				Assert.AreEqual(0,EmailMessages.GetReceiverUntrustedCount("someone@other.example.test"));
				Assert.IsFalse(EmailMessages.TryAddTrustDirect(" "));
			}
		}

		[TestMethod]
		public void CreateCertificateStoresIfNeeded_MakesThePrivateExternalAndAnchorStores() {
			DirectTestStores directTestStores=new DirectTestStores();
			try {
				Assert.IsTrue(directTestStores.AreStoresDeleted(),"None of the test stores exists yet.");
				EmailMessages.CreateCertificateStoresIfNeeded();
				foreach(string storeName in new[] { directTestStores.NamePrivate,directTestStores.NameExternal,directTestStores.NameAnchor }) {
					using(X509Store x509Store=new X509Store(storeName,StoreLocation.CurrentUser)) {
						x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);//Throws if the store was not made.
					}
				}
			}
			finally {
				directTestStores.Dispose();
			}
			Assert.IsTrue(directTestStores.AreStoresDeleted());
			Assert.AreEqual(StoreLocation.LocalMachine,EmailMessages.CertStoreLocation,"Production opens the stores of the Local Computer.");
			Assert.AreEqual("NHINDPrivate",EmailMessages.CertStoreNamePrivate);
			Assert.AreEqual("NHINDExternal",EmailMessages.CertStoreNameExternal);
			Assert.AreEqual("NHINDAnchors",EmailMessages.CertStoreNameAnchor);
			Assert.IsNull(EmailMessages.FuncFindHostedCertsForTests);
		}

		///<summary>The e-mail preview of an encrypted message: the corpus's encrypted message, made for this run's certificates, is decrypted by the
		///Direct agent and its parts read with MimeKit; without the inbox's private certificate, or without trust for the sender, it has none.</summary>
		[TestMethod]
		public void EncryptedCorpusMessage_PreviewPartsAreTheDecryptedOnes() {
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				X509Certificate2 x509Certificate2Inbox=directTestStores.CreateCertificate(INBOX);
				X509Certificate2 x509Certificate2Sender=directTestStores.CreateCertificate(SENDER);
				string raw=ReceivedMailCorpus.MakeEncrypted(x509Certificate2Inbox,x509Certificate2Sender);
				EmailAddress emailAddressInbox=new EmailAddress() { EmailUsername=INBOX };
				EmailMessages.CreateCertificateStoresIfNeeded();
				Assert.AreEqual(0,EmailMessages.GetMimePartsForMimeTypes(raw,emailAddressInbox,"text/plain")[0].Count,"No private certificate yet.");
				directTestStores.AddPrivate(x509Certificate2Inbox);
				Assert.AreEqual(0,EmailMessages.GetMimePartsForMimeTypes(raw,emailAddressInbox,"text/plain")[0].Count,"The sender is not trusted yet.");
				directTestStores.AddAnchor(x509Certificate2Sender);
				List<List<EmailMimePart>> listListParts=EmailMessages.GetMimePartsForMimeTypes(raw,emailAddressInbox,"text/plain","application/pkcs7-signature");
				Assert.AreEqual(1,listListParts[0].Count);
				Assert.AreEqual("Only the holder of the private key can read this.",EmailMessages.ProcessMimeTextPart(listListParts[0][0]).TrimEnd('\r','\n'));
				Assert.AreEqual(0,listListParts[1].Count,"The agent took the signature out.");
			}
		}
	}
}
