// The Direct certificate import of open point O-13 (owner answer of 2026-11-08: "Yes, load Direct provider's certificate from a file"): the
// certificate a Direct provider (HISP) issues for an address, from its .pfx or .p12 file and password, into the store of private certificates
// with its private key never exportable, and the provider's trust anchor into the trust anchors (DirectCertificateImport, the Certificate
// window of Edit Email Address). Every store is a store of the current user made for the test and deleted afterwards (DirectTestStores, the
// seam S0-3D's tests use); the computer's own NHINDPrivate and NHINDAnchors are never opened. The certificates are made at run time by a test
// CA (DirectTestPki), the files written to a temp folder of the test and deleted, the passwords made at run time and kept in memory only, and
// every key the import stores is deleted afterwards. No network. Licensed under the GNU General Public License, version 2 or later. See LICENSE
// in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using BcPkcs=Org.BouncyCastle.Asn1.Pkcs;
using BcSecurity=Org.BouncyCastle.Security;

namespace LibraryUpdateTests {
	[TestClass]
	public class DirectCertificateImportTests {
		private const string ADDRESS="pat.q@hisp.example.test";
		private const string DOMAIN="hisp.example.test";
		private const string ADDRESS_OTHER="front@clinic.example.test";

		///<summary>The provider's file for the address: Check shows the certificate and stores nothing; Import puts it into the private store with
		///its private key, not exportable but usable, marked as imported here; the program's own search finds it; the CA certificate in the file
		///is not imported.</summary>
		[TestMethod]
		public void ImportPrivateFile_PutsTheCertificateIntoThePrivateStoreWithAKeyThatCannotBeExported() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				try {
					DirectTestPki.Issued issued=directTestPki.Issue(ADDRESS);
					string pathPfx=directTestPki.WritePfx("pat.pfx",issued,isCaIncluded:true);
					DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathPfx,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResultCheck.Error);
					Assert.IsFalse(directCertificateResultCheck.IsImported);
					Assert.AreEqual(1,directCertificateResultCheck.ListDetails.Count,"The certificate for the address; the CA certificate of the file names no address.");
					DirectCertificateDetails directCertificateDetails=directCertificateResultCheck.ListDetails[0];
					Assert.AreEqual(issued.Thumbprint,directCertificateDetails.Thumbprint);
					Assert.AreEqual(issued.Subject,directCertificateDetails.Subject);
					Assert.AreEqual(directTestPki.CaSubject,directCertificateDetails.Issuer);
					Assert.AreEqual(issued.SerialNumber,directCertificateDetails.SerialNumber);
					Assert.AreEqual(issued.DateTimeNotAfter,directCertificateDetails.DateTimeNotAfter);
					Assert.AreEqual(issued.DateTimeNotBefore,directCertificateDetails.DateTimeNotBefore);
					Assert.AreEqual(DirectCertificateBinding.Address,directCertificateDetails.Binding);
					Assert.AreEqual(ADDRESS,directCertificateDetails.BoundName);
					Assert.IsTrue(directCertificateDetails.HasPrivateKey);
					Assert.IsTrue(directCertificateDetails.IsKeyUsable);
					Assert.AreEqual(DirectCertificatePurpose.Signing|DirectCertificatePurpose.Encryption,directCertificateDetails.Purpose);
					string describe=directCertificateDetails.Describe();
					foreach(string part in new[] { issued.Subject,directTestPki.CaSubject,issued.SerialNumber,issued.Thumbprint,"For the address "+ADDRESS,"Private key: yes\r\n",
						"Purpose: signing and encryption" })
					{
						StringAssert.Contains(describe,part);
					}
					Assert.AreEqual(0,directCertificateResultCheck.ListDetailsReplaced.Count);
					Assert.IsTrue(directTestStores.AreStoresDeleted(),"Check writes nothing: no store is made.");
					//Import.
					DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathPfx,directTestPki.Password,null,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResult.Error);
					Assert.IsTrue(directCertificateResult.IsImported);
					Assert.AreEqual("",directCertificateResult.Warning,"The current user's store: no machine key to give the computer's users.");
					CollectionAssert.AreEqual(new List<string> { issued.Thumbprint },directTestStores.GetThumbprints(directTestStores.NamePrivate),
						"Only the address's certificate; not the CA.");
					Assert.AreEqual(0,directTestStores.GetThumbprints(directTestStores.NameAnchor).Count,"The private import adds no trust anchor.");
					using(X509Certificate2 x509Certificate2Stored=FindStored(directTestStores.NamePrivate,issued.Thumbprint)) {
						Assert.IsTrue(x509Certificate2Stored.HasPrivateKey);
						Assert.AreEqual(DirectCertificateImport.FRIENDLY_NAME_IMPORTED,x509Certificate2Stored.FriendlyName);
						AssertKeyNotExportableButUsable(x509Certificate2Stored);
					}
					//The program's own search finds it: the signing certificate and the Direct agent's resolver.
					Assert.AreEqual(issued.Thumbprint,EmailMessages.GetCertFromPrivateStore(ADDRESS)?.Thumbprint);
					Assert.AreEqual(issued.Thumbprint,EmailMessages.GetCertFromPrivateStore("Pat Q <PAT.Q@hisp.example.test>")?.Thumbprint);
					X509Certificate2Collection x509Certificate2CollectionResolved=new EmailPrivateResolver().GetCertificates(new System.Net.Mail.MailAddress(ADDRESS));
					Assert.AreEqual(1,x509Certificate2CollectionResolved.Count);
					List<DirectCertificateDetails> listInStore=DirectCertificateImport.GetPrivateCertificates(ADDRESS);
					Assert.AreEqual(1,listInStore.Count);
					StringAssert.Contains(listInStore[0].DescribeShort(),issued.Thumbprint);
					Assert.AreEqual(0,DirectCertificateImport.GetPrivateCertificates(ADDRESS_OTHER).Count);
					//The same file again: already there.
					DirectCertificateResult directCertificateResultAgain=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathPfx,directTestPki.Password,null,DateTime.UtcNow);
					Assert.IsFalse(directCertificateResultAgain.IsImported);
					StringAssert.Contains(directCertificateResultAgain.Error,"already in this computer's Direct certificate store");
					Assert.AreEqual(1,directCertificateResultAgain.ListDetailsInStore.Count);
					Assert.AreEqual(1,directTestStores.GetThumbprints(directTestStores.NamePrivate).Count);
				}
				finally {
					DeleteImportedKeys(directTestStores);
				}
			}
		}

		///<summary>Each refusal of the address's certificate, with its message; nothing is written to any store.</summary>
		[TestMethod]
		public void CheckAndImportPrivateFile_RefuseWhatTheProgramWouldNotUse() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				DirectTestPki.Issued issued=directTestPki.Issue(ADDRESS);
				string pathPfx=directTestPki.WritePfx("pat.pfx",issued,isCaIncluded:false);
				string pathP12=directTestPki.WritePfx("pat.P12",issued,isCaIncluded:false);
				DirectTestPki.Issued issuedOther=directTestPki.Issue(ADDRESS_OTHER);
				DirectTestPki.Issued issuedExpired=directTestPki.Issue(ADDRESS,dateTimeOffsetNotBefore:DateTimeOffset.UtcNow.AddDays(-30),
					dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(-1));
				DirectTestPki.Issued issuedNotYet=directTestPki.Issue(ADDRESS,dateTimeOffsetNotBefore:DateTimeOffset.UtcNow.AddDays(5),
					dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(20));
				string pathKeyless=directTestPki.WritePfx("keyless.pfx",issued,isCaIncluded:true,isKeyIncluded:false);
				string pathCer=directTestPki.WriteFile("pat.cer",issued.RawData);
				Assert.AreEqual("",DirectCertificateImport.CheckPrivateFile(ADDRESS,pathP12,directTestPki.Password,DateTime.UtcNow).Error,".p12 in any case is taken.");
				AssertRefused(DirectCertificateImport.CheckPrivateFile(ADDRESS,pathPfx,"not-"+directTestPki.Password,DateTime.UtcNow),"Check the password");
				AssertRefused(DirectCertificateImport.CheckPrivateFile(ADDRESS,pathPfx,"",DateTime.UtcNow),"Check the password");
				AssertRefused(DirectCertificateImport.CheckPrivateFile(ADDRESS,pathCer,"",DateTime.UtcNow),"must be the .pfx or .p12 file");
				AssertRefused(DirectCertificateImport.CheckPrivateFile(ADDRESS,"",directTestPki.Password,DateTime.UtcNow),"Pick the certificate file first");
				AssertRefused(DirectCertificateImport.CheckPrivateFile(ADDRESS,Path.Combine(directTestPki.Folder,"missing.pfx"),directTestPki.Password,DateTime.UtcNow),
					"The file was not found");
				AssertRefused(DirectCertificateImport.CheckPrivateFile(ADDRESS,directTestPki.WriteFile("empty.pfx",new byte[0]),directTestPki.Password,DateTime.UtcNow),
					"The file is empty");
				AssertRefused(DirectCertificateImport.CheckPrivateFile("",pathPfx,directTestPki.Password,DateTime.UtcNow),"Enter the email address first");
				AssertRefused(DirectCertificateImport.CheckPrivateFile("pat.q",pathPfx,directTestPki.Password,DateTime.UtcNow),"is not an email address");
				//It says where the address is fixed: Edit Email Address's user name (the certificate window shows it read-only).
				AssertRefused(DirectCertificateImport.CheckPrivateFile("pat.q",pathPfx,directTestPki.Password,DateTime.UtcNow),
					"The Direct certificate is found by the user name: in Edit Email Address, enter the Direct address (name@domain) in Username or email address.");
				Assert.AreEqual("",DirectCertificateImport.GetAddressError(" Pat Q <"+ADDRESS+"> "));
				AssertRefused(DirectCertificateImport.CheckPrivateFile("pat q@hisp.example.test",pathPfx,directTestPki.Password,DateTime.UtcNow),"is not an email address");
				//Another address's certificate: the message names the address and its domain, and lists what the file holds.
				DirectCertificateResult directCertificateResultOther=DirectCertificateImport.CheckPrivateFile(ADDRESS,
					directTestPki.WritePfx("other.pfx",issuedOther,isCaIncluded:true),directTestPki.Password,DateTime.UtcNow);
				AssertRefused(directCertificateResultOther,"No certificate in the file is for "+ADDRESS+" or its domain "+DOMAIN);
				StringAssert.Contains(directCertificateResultOther.Error,issuedOther.Subject);
				StringAssert.Contains(directCertificateResultOther.Error,directTestPki.CaSubject);
				//The same address in another domain, or another address of the domain, is not the address.
				AssertRefused(DirectCertificateImport.CheckPrivateFile("pat.q@other.example.test",pathPfx,directTestPki.Password,DateTime.UtcNow),"No certificate in the file");
				AssertRefused(DirectCertificateImport.CheckPrivateFile("someone@hisp.example.test",pathPfx,directTestPki.Password,DateTime.UtcNow),"No certificate in the file");
				AssertRefused(DirectCertificateImport.CheckPrivateFile(ADDRESS,pathKeyless,directTestPki.Password,DateTime.UtcNow),"has no private key");
				DirectCertificateResult directCertificateResultExpired=DirectCertificateImport.CheckPrivateFile(ADDRESS,
					directTestPki.WritePfx("expired.pfx",issuedExpired,isCaIncluded:false),directTestPki.Password,DateTime.UtcNow);
				AssertRefused(directCertificateResultExpired,"The certificate expired on "+DirectCertificateImport.FormatDate(issuedExpired.DateTimeNotAfter));
				DirectCertificateResult directCertificateResultNotYet=DirectCertificateImport.CheckPrivateFile(ADDRESS,
					directTestPki.WritePfx("notyet.pfx",issuedNotYet,isCaIncluded:false),directTestPki.Password,DateTime.UtcNow);
				AssertRefused(directCertificateResultNotYet,"The certificate is not valid yet: it is valid from "
					+DirectCertificateImport.FormatDate(issuedNotYet.DateTimeNotBefore));
				//The clock passed in decides: the same file a day after the certificate's end.
				AssertRefused(DirectCertificateImport.CheckPrivateFile(ADDRESS,pathPfx,directTestPki.Password,issued.DateTimeNotAfter.ToUniversalTime().AddDays(1)),
					"The certificate expired on");
				//Import refuses the same way and stores nothing.
				DirectCertificateResult directCertificateResultImport=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathKeyless,directTestPki.Password,null,DateTime.UtcNow);
				AssertRefused(directCertificateResultImport,"has no private key");
				AssertRefused(DirectCertificateImport.ImportPrivateFile(ADDRESS,pathPfx,"not-"+directTestPki.Password,null,DateTime.UtcNow),"Check the password");
				AssertRefused(DirectCertificateImport.ImportPrivateFile(ADDRESS,directTestPki.WritePfx("expired2.pfx",issuedExpired,isCaIncluded:false),
					directTestPki.Password,null,DateTime.UtcNow),"The certificate expired on");
				Assert.IsTrue(directTestStores.AreStoresDeleted(),"No refusal wrote to a store.");
			}
		}

		///<summary>A domain certificate (Direct allows one for every address of the domain) is taken for an address of the domain and found by the
		///program for it; an address certificate imported later does not replace it (a different kind: kept, with why), and a domain certificate
		///valid longer replaces only the domain certificate.</summary>
		[TestMethod]
		public void ImportPrivateFile_TakesADomainCertificateForAnAddressOfTheDomain() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				try {
					DirectTestPki.Issued issuedDomain=directTestPki.Issue(null,dnsName:DOMAIN);
					DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,
						directTestPki.WritePfx("domain.pfx",issuedDomain,isCaIncluded:true),directTestPki.Password,null,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResult.Error);
					Assert.IsTrue(directCertificateResult.IsImported);
					Assert.AreEqual(DirectCertificateBinding.Domain,directCertificateResult.ListDetails[0].Binding);
					Assert.AreEqual(DOMAIN,directCertificateResult.ListDetails[0].BoundName);
					StringAssert.Contains(directCertificateResult.ListDetails[0].Describe(),"For every address of the domain "+DOMAIN);
					Assert.AreEqual(issuedDomain.Thumbprint,EmailMessages.GetCertFromPrivateStore(ADDRESS)?.Thumbprint,"The domain certificate signs for the address.");
					Assert.AreEqual(issuedDomain.Thumbprint,EmailMessages.GetCertFromPrivateStore("someone@hisp.example.test")?.Thumbprint,"And for another address of it.");
					Assert.IsNull(EmailMessages.GetCertFromPrivateStore(ADDRESS_OTHER));
					//An address certificate next to it: not a replacement.
					DirectTestPki.Issued issuedAddress=directTestPki.Issue(ADDRESS);
					string pathAddress=directTestPki.WritePfx("pat.pfx",issuedAddress,isCaIncluded:false);
					DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathAddress,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResultCheck.Error);
					Assert.AreEqual(0,directCertificateResultCheck.ListDetailsReplaced.Count,"The domain certificate serves the other addresses of the domain too.");
					Assert.AreEqual(1,directCertificateResultCheck.ListDetailsKept.Count);
					Assert.AreEqual(issuedDomain.Thumbprint,directCertificateResultCheck.ListDetailsKept[0].Thumbprint);
					Assert.AreEqual(DirectCertificateKeptReason.OtherKind,directCertificateResultCheck.ListDetailsKept[0].KeptReason);
					StringAssert.Contains(DirectCertificateImport.GetKeptText(directCertificateResultCheck),
						"for every address of the domain, which a certificate for one address does not replace");
					Assert.IsTrue(DirectCertificateImport.ImportPrivateFile(ADDRESS,pathAddress,directTestPki.Password,null,DateTime.UtcNow).IsImported);
					Assert.AreEqual(issuedAddress.Thumbprint,EmailMessages.GetCertFromPrivateStore(ADDRESS)?.Thumbprint,"The address's own certificate comes first.");
					Assert.AreEqual(issuedDomain.Thumbprint,EmailMessages.GetCertFromPrivateStore("someone@hisp.example.test")?.Thumbprint);
					Assert.AreEqual(2,DirectCertificateImport.GetPrivateCertificates(ADDRESS).Count);
					//A domain certificate valid longer replaces the domain certificate only.
					DirectTestPki.Issued issuedDomainNew=directTestPki.Issue(null,dnsName:DOMAIN,dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(30));
					string pathDomainNew=directTestPki.WritePfx("domain-new.pfx",issuedDomainNew,isCaIncluded:false);
					DirectCertificateResult directCertificateResultCheckNew=DirectCertificateImport.CheckPrivateFile("someone@hisp.example.test",pathDomainNew,
						directTestPki.Password,DateTime.UtcNow);
					CollectionAssert.AreEqual(new List<string> { issuedDomain.Thumbprint },directCertificateResultCheckNew.ListDetailsReplaced.Select(x => x.Thumbprint).ToList());
				}
				finally {
					DeleteImportedKeys(directTestStores);
				}
			}
		}

		///<summary>A provider's certificate with an extension Windows has no name for, before its subject alternative name: the name check reads the
		///subject alternative name by its OID (before, the program's certificate search threw on the missing friendly name).</summary>
		[TestMethod]
		public void CheckPrivateFile_ReadsACertificateWithAnExtensionWindowsHasNoNameFor() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				DirectTestPki.Issued issued=directTestPki.Issue(ADDRESS,isPrivateExtensionFirst:true);
				Assert.IsNull(issued.X509Certificate2WithKey.Extensions[0].Oid.FriendlyName,"Windows has no name for the private OID.");
				Assert.AreEqual(DirectTestPki.PRIVATE_OID,issued.X509Certificate2WithKey.Extensions[0].Oid.Value);
				Assert.AreEqual(ADDRESS,EmailCertificates.GetCertRfc822Name(issued.X509Certificate2WithKey));
				Assert.AreEqual(DirectCertificateBinding.Address,DirectCertificateImport.GetBinding(issued.X509Certificate2WithKey,ADDRESS));
				DirectCertificateResult directCertificateResult=DirectCertificateImport.CheckPrivateFile(ADDRESS,directTestPki.WritePfx("pat.pfx",issued,isCaIncluded:true),
					directTestPki.Password,DateTime.UtcNow);
				Assert.AreEqual("",directCertificateResult.Error);
				Assert.AreEqual(issued.Thumbprint,directCertificateResult.ListDetails[0].Thumbprint);
				DirectTestPki.Issued issuedDomain=directTestPki.Issue(null,dnsName:DOMAIN,isPrivateExtensionFirst:true);
				Assert.AreEqual(DOMAIN,EmailCertificates.GetCertDnsName(issuedDomain.X509Certificate2WithKey));
				Assert.IsTrue(directTestStores.AreStoresDeleted());
			}
		}

		///<summary>An older certificate of the address (of the same purpose, expiring before the file's) is replaced only after the user agreed to
		///replace it (the window's question): without the agreement nothing is imported; with it the new certificate is added and the older ones
		///removed. The key of the older one this import put there is deleted with it; the key of one put there another way (certlm.msc) stays;
		///another address's certificate is not touched.</summary>
		[TestMethod]
		public void ImportPrivateFile_ReplacesAnOlderCertificateOnlyAfterAsking() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				try {
					//The older certificate, imported here last year (in this test, a moment ago); it expires before the new one.
					DirectTestPki.Issued issuedOld=directTestPki.Issue(ADDRESS,dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(10));
					Assert.IsTrue(DirectCertificateImport.ImportPrivateFile(ADDRESS,directTestPki.WritePfx("old.pfx",issuedOld,isCaIncluded:false),
						directTestPki.Password,null,DateTime.UtcNow).IsImported);
					KeyLocation keyLocationOld;
					using(X509Certificate2 x509Certificate2Old=FindStored(directTestStores.NamePrivate,issuedOld.Thumbprint)) {
						keyLocationOld=KeyLocation.Of(x509Certificate2Old);
					}
					Assert.IsTrue(keyLocationOld.Exists(),"The older certificate's key is stored.");
					//Another older one (valid for 2 days), put there another way (certlm.msc), and another address's certificate.
					X509Certificate2 x509Certificate2Manual=directTestStores.CreateCertificate(ADDRESS);
					directTestStores.AddPrivate(x509Certificate2Manual);
					KeyLocation keyLocationManual;
					using(X509Certificate2 x509Certificate2ManualStored=FindStored(directTestStores.NamePrivate,x509Certificate2Manual.Thumbprint)) {
						keyLocationManual=KeyLocation.Of(x509Certificate2ManualStored);
					}
					X509Certificate2 x509Certificate2Other=directTestStores.CreateCertificate(ADDRESS_OTHER);
					directTestStores.AddPrivate(x509Certificate2Other);
					//The new certificate from the provider.
					DirectTestPki.Issued issuedNew=directTestPki.Issue(ADDRESS);
					string pathNew=directTestPki.WritePfx("new.pfx",issuedNew,isCaIncluded:true);
					DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathNew,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResultCheck.Error);
					List<string> listReplaced=directCertificateResultCheck.ListDetailsReplaced.Select(x => x.Thumbprint).ToList();
					CollectionAssert.AreEquivalent(new List<string> { issuedOld.Thumbprint,x509Certificate2Manual.Thumbprint },listReplaced);
					//The question lists the file's certificate and both of the store's.
					string question=DirectCertificateImport.GetReplaceQuestion(directCertificateResultCheck);
					StringAssert.Contains(question,directCertificateResultCheck.ListDetails[0].DescribeShort());
					StringAssert.Contains(question,issuedOld.Thumbprint);
					StringAssert.Contains(question,x509Certificate2Manual.Thumbprint);
					StringAssert.Contains(question,"Import the file and replace the certificates under Replaced on this computer?");
					StringAssert.EndsWith(question,"importing their .pfx or .p12 files again. Choose No to import nothing.");
					Assert.AreEqual(0,directCertificateResultCheck.ListDetailsKept.Count,"Another address's certificate is not the address's: not listed.");
					List<string> listStoreBefore=directTestStores.GetThumbprints(directTestStores.NamePrivate);
					//Not agreed (the user answered No, which imports nothing), or agreed for one of the two only: nothing changes.
					AssertRefused(DirectCertificateImport.ImportPrivateFile(ADDRESS,pathNew,directTestPki.Password,null,DateTime.UtcNow),
						"was not agreed to be replaced");
					AssertRefused(DirectCertificateImport.ImportPrivateFile(ADDRESS,pathNew,directTestPki.Password,new List<string> { issuedOld.Thumbprint },
						DateTime.UtcNow),"was not agreed to be replaced");
					CollectionAssert.AreEquivalent(listStoreBefore,directTestStores.GetThumbprints(directTestStores.NamePrivate));
					Assert.IsTrue(keyLocationOld.Exists());
					//Agreed (Yes): replaced.
					DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathNew,directTestPki.Password,
						listReplaced.Select(x => x.ToLowerInvariant()).ToList(),DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResult.Error);
					Assert.IsTrue(directCertificateResult.IsImported);
					Assert.AreEqual("",directCertificateResult.Warning);
					Assert.AreEqual(2,directCertificateResult.ListDetailsReplaced.Count);
					CollectionAssert.AreEquivalent(new List<string> { issuedNew.Thumbprint,x509Certificate2Other.Thumbprint },
						directTestStores.GetThumbprints(directTestStores.NamePrivate),"The new certificate and the other address's; both older ones are gone.");
					Assert.IsFalse(keyLocationOld.Exists(),"The key this import stored for the older certificate is deleted with it.");
					Assert.IsTrue(keyLocationManual.Exists(),"The key of a certificate put there another way stays.");
					Assert.AreEqual(issuedNew.Thumbprint,EmailMessages.GetCertFromPrivateStore(ADDRESS)?.Thumbprint);
					Assert.AreEqual(x509Certificate2Other.Thumbprint,EmailMessages.GetCertFromPrivateStore(ADDRESS_OTHER)?.Thumbprint);
					//The log entry: the address's number, the thumbprint, its last day and the count; no address and no password.
					string log=DirectCertificateImport.GetLogText(directCertificateResult,42,isAnchor:false);
					StringAssert.Contains(log,"EmailAddressNum 42");
					StringAssert.Contains(log,issuedNew.Thumbprint);
					StringAssert.Contains(log,issuedNew.DateTimeNotAfter.ToString("yyyy-MM-dd"));
					StringAssert.Contains(log,"certificates of the store replaced: 2, kept: 0.");
					Assert.IsFalse(log.Contains(ADDRESS) || log.Contains(DOMAIN) || log.Contains(directTestPki.Password),log);
				}
				finally {
					DeleteImportedKeys(directTestStores);
				}
			}
		}

		///<summary>Only a certificate of the store of the same purpose that expires before the file's is offered. The store holds the renewed
		///certificate and last year's file, still valid, is picked by mistake: the renewed one is not offered, the import asks nothing, adds the
		///file's next to it and says that the renewed one was kept. A file valid longer gets the question, which lists exactly the two it replaces,
		///both sides with subject, purpose, valid from and valid to, and ends "Choose No to import nothing."; one that ends at the same moment is
		///not offered either.</summary>
		[TestMethod]
		public void CheckPrivateFile_OffersOnlyAnOlderCertificateOfTheSamePurpose() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				try {
					//The renewed certificate, imported here.
					DirectTestPki.Issued issuedRenewed=directTestPki.Issue(ADDRESS,dateTimeOffsetNotBefore:DateTimeOffset.UtcNow.AddDays(-2),
						dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(50));
					DirectCertificateResult directCertificateResultRenewed=DirectCertificateImport.ImportPrivateFile(ADDRESS,
						directTestPki.WritePfx("renewed.pfx",issuedRenewed,isCaIncluded:false),directTestPki.Password,null,DateTime.UtcNow);
					Assert.IsTrue(directCertificateResultRenewed.IsImported,directCertificateResultRenewed.Error);
					Assert.AreEqual("",DirectCertificateImport.GetReplaceQuestion(directCertificateResultRenewed),"Nothing replaced: no question.");
					Assert.AreEqual("",DirectCertificateImport.GetKeptText(directCertificateResultRenewed));
					//Last year's file, still valid for 10 days: the renewed certificate (the same purpose, valid longer) is not offered.
					DirectTestPki.Issued issuedLastYear=directTestPki.Issue(ADDRESS,dateTimeOffsetNotBefore:DateTimeOffset.UtcNow.AddDays(-50),
						dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(10));
					string pathLastYear=directTestPki.WritePfx("last-year.pfx",issuedLastYear,isCaIncluded:false);
					DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathLastYear,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResultCheck.Error,"Still valid: it can be imported.");
					Assert.AreEqual(0,directCertificateResultCheck.ListDetailsReplaced.Count,"The renewed certificate is valid longer: not offered.");
					Assert.AreEqual("",DirectCertificateImport.GetReplaceQuestion(directCertificateResultCheck),"Nothing to replace: no question.");
					Assert.AreEqual(1,directCertificateResultCheck.ListDetailsKept.Count);
					Assert.AreEqual(issuedRenewed.Thumbprint,directCertificateResultCheck.ListDetailsKept[0].Thumbprint);
					Assert.AreEqual(DirectCertificateKeptReason.ValidAsLongOrLonger,directCertificateResultCheck.ListDetailsKept[0].KeptReason);
					string kept=DirectCertificateImport.GetKeptText(directCertificateResultCheck);
					StringAssert.StartsWith(kept,"Kept on this computer:\r\n"+directCertificateResultCheck.ListDetailsKept[0].DescribeShort()
						+" (for the same purpose and valid as long as or longer than the one in the file, which is added next to it)");
					//The import asks nothing, adds the file's certificate and says that the renewed one was kept.
					DirectCertificateResult directCertificateResultLastYear=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathLastYear,directTestPki.Password,null,
						DateTime.UtcNow);
					Assert.IsTrue(directCertificateResultLastYear.IsImported,directCertificateResultLastYear.Error);
					Assert.AreEqual(0,directCertificateResultLastYear.ListDetailsReplaced.Count);
					Assert.AreEqual(kept,DirectCertificateImport.GetKeptText(directCertificateResultLastYear),"The import says so.");
					CollectionAssert.AreEquivalent(new List<string> { issuedRenewed.Thumbprint,issuedLastYear.Thumbprint },
						directTestStores.GetThumbprints(directTestStores.NamePrivate),"The renewed certificate stays.");
					StringAssert.Contains(DirectCertificateImport.GetLogText(directCertificateResultLastYear,7,isAnchor:false),"replaced: 0, kept: 1.");
					//A file valid longer than both: the question lists exactly the two, both sides with subject, purpose, valid from and valid to.
					DateTimeOffset dateTimeOffsetLonger=DateTimeOffset.UtcNow.AddDays(55);
					DirectTestPki.Issued issuedLonger=directTestPki.Issue(ADDRESS,dateTimeOffsetNotBefore:DateTimeOffset.UtcNow.AddDays(-1),
						dateTimeOffsetNotAfter:dateTimeOffsetLonger);
					string pathLonger=directTestPki.WritePfx("longer.pfx",issuedLonger,isCaIncluded:false);
					DirectCertificateResult directCertificateResultLonger=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathLonger,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResultLonger.Error);
					CollectionAssert.AreEquivalent(new List<string> { issuedRenewed.Thumbprint,issuedLastYear.Thumbprint },
						directCertificateResultLonger.ListDetailsReplaced.Select(x => x.Thumbprint).ToList());
					Assert.AreEqual(0,directCertificateResultLonger.ListDetailsKept.Count);
					Assert.AreEqual("",DirectCertificateImport.GetReplaceWarning(directCertificateResultLonger),"The file's is for both purposes too.");
					string question=DirectCertificateImport.GetReplaceQuestion(directCertificateResultLonger);
					StringAssert.StartsWith(question,"This computer's Direct certificate store has a certificate for this address or its domain, for a purpose of a "
						+"certificate in the file, that expires before it.");
					Assert.AreEqual("In the file:\r\n"+directCertificateResultLonger.ListDetails[0].DescribeShort(),GetQuestionBlock(question,"In the file:"));
					Assert.AreEqual("Replaced on this computer:\r\n"+string.Join("\r\n",directCertificateResultLonger.ListDetailsReplaced.Select(x => x.DescribeShort())),
						GetQuestionBlock(question,"Replaced on this computer:"),"Exactly the two it replaces.");
					foreach(DirectTestPki.Issued issued in new[] { issuedLonger,issuedRenewed,issuedLastYear }) {
						string line=question.Split(new[] { "\r\n" },StringSplitOptions.None).Single(x => x.Contains(issued.Thumbprint));
						foreach(string part in new[] { issued.Subject,"signing and encryption","valid from "+DirectCertificateImport.FormatDate(issued.DateTimeNotBefore),
							"to "+DirectCertificateImport.FormatDate(issued.DateTimeNotAfter) })
						{
							StringAssert.Contains(line,part);
						}
					}
					StringAssert.EndsWith(question,"Import the file and replace the certificates under Replaced on this computer? They are removed from the store, and "
						+"so are their private keys if this window imported them. Messages encrypted to them can then be read here only after importing their .pfx or "
						+".p12 files again. Choose No to import nothing.");
					Assert.IsFalse(question.ToLowerInvariant().Contains("older") || question.ToLowerInvariant().Contains("newer"),question);
					//No agreement: nothing replaced, nothing imported.
					AssertRefused(DirectCertificateImport.ImportPrivateFile(ADDRESS,pathLonger,directTestPki.Password,null,DateTime.UtcNow),"was not agreed to be replaced");
					Assert.AreEqual(2,directTestStores.GetThumbprints(directTestStores.NamePrivate).Count);
					//Agreed: the file's replaces both. Then one that ends at the same moment is not offered: it does not expire before.
					Assert.IsTrue(DirectCertificateImport.ImportPrivateFile(ADDRESS,pathLonger,directTestPki.Password,
						directCertificateResultLonger.ListDetailsReplaced.Select(x => x.Thumbprint).ToList(),DateTime.UtcNow).IsImported);
					CollectionAssert.AreEqual(new List<string> { issuedLonger.Thumbprint },directTestStores.GetThumbprints(directTestStores.NamePrivate));
					DirectTestPki.Issued issuedSameEnd=directTestPki.Issue(ADDRESS,dateTimeOffsetNotAfter:dateTimeOffsetLonger);
					Assert.AreEqual(issuedLonger.DateTimeNotAfter,issuedSameEnd.DateTimeNotAfter);
					DirectCertificateResult directCertificateResultSameEnd=DirectCertificateImport.CheckPrivateFile(ADDRESS,
						directTestPki.WritePfx("same-end.pfx",issuedSameEnd,isCaIncluded:false),directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual(0,directCertificateResultSameEnd.ListDetailsReplaced.Count);
					Assert.AreEqual(DirectCertificateKeptReason.ValidAsLongOrLonger,directCertificateResultSameEnd.ListDetailsKept.Single().KeptReason);
				}
				finally {
					DeleteImportedKeys(directTestStores);
				}
			}
		}

		///<summary>Some providers issue one certificate for signing and another for encryption for the same address. Importing one never offers the
		///other (another purpose: kept, with why), in either order, and both stay. A renewed signing certificate replaces only the signing one; a
		///certificate for both replaces both; a signing certificate that replaces one for both warns that the file has none for encryption.</summary>
		[TestMethod]
		public void ImportPrivateFile_KeepsSeparateSigningAndEncryptionCertificatesOfOneAddress() {
			foreach(bool isSigningFirst in new[] { true,false }) {
				using(DirectTestPki directTestPki=new DirectTestPki())
				using(DirectTestStores directTestStores=new DirectTestStores()) {
					try {
						DirectTestPki.Issued issuedSigning=directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.DigitalSignature);
						DirectTestPki.Issued issuedEncryption=directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.KeyEncipherment);
						DirectTestPki.Issued issuedFirst=isSigningFirst ? issuedSigning : issuedEncryption;
						DirectTestPki.Issued issuedSecond=isSigningFirst ? issuedEncryption : issuedSigning;
						DirectCertificateResult directCertificateResultFirst=DirectCertificateImport.ImportPrivateFile(ADDRESS,
							directTestPki.WritePfx("first.pfx",issuedFirst,isCaIncluded:true),directTestPki.Password,null,DateTime.UtcNow);
						Assert.IsTrue(directCertificateResultFirst.IsImported,directCertificateResultFirst.Error);
						Assert.AreEqual(isSigningFirst ? DirectCertificatePurpose.Signing : DirectCertificatePurpose.Encryption,directCertificateResultFirst.ListDetails[0].Purpose);
						string pathSecond=directTestPki.WritePfx("second.pfx",issuedSecond,isCaIncluded:true);
						DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathSecond,directTestPki.Password,DateTime.UtcNow);
						Assert.AreEqual("",directCertificateResultCheck.Error);
						Assert.AreEqual(0,directCertificateResultCheck.ListDetailsReplaced.Count,"Another purpose: not offered.");
						Assert.AreEqual(issuedFirst.Thumbprint,directCertificateResultCheck.ListDetailsKept.Single().Thumbprint);
						Assert.AreEqual(DirectCertificateKeptReason.OtherPurpose,directCertificateResultCheck.ListDetailsKept[0].KeptReason);
						StringAssert.Contains(DirectCertificateImport.GetKeptText(directCertificateResultCheck),"(for another purpose than the certificates in the file)");
						StringAssert.Contains(directCertificateResultCheck.ListDetailsKept[0].DescribeShort(),isSigningFirst ? "; signing; " : "; encryption; ");
						DirectCertificateResult directCertificateResultSecond=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathSecond,directTestPki.Password,null,
							DateTime.UtcNow);
						Assert.IsTrue(directCertificateResultSecond.IsImported,directCertificateResultSecond.Error);
						CollectionAssert.AreEquivalent(new List<string> { issuedSigning.Thumbprint,issuedEncryption.Thumbprint },
							directTestStores.GetThumbprints(directTestStores.NamePrivate),"Both stay after each import.");
						//A renewed signing certificate: only the signing one is offered; the encryption one is kept.
						DirectTestPki.Issued issuedSigningRenewed=directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.NonRepudiation,
							dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(30));
						string pathSigningRenewed=directTestPki.WritePfx("signing-renewed.pfx",issuedSigningRenewed,isCaIncluded:false);
						DirectCertificateResult directCertificateResultRenewed=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathSigningRenewed,directTestPki.Password,
							DateTime.UtcNow);
						Assert.AreEqual(issuedSigning.Thumbprint,directCertificateResultRenewed.ListDetailsReplaced.Single().Thumbprint);
						Assert.AreEqual(issuedEncryption.Thumbprint,directCertificateResultRenewed.ListDetailsKept.Single().Thumbprint);
						string question=DirectCertificateImport.GetReplaceQuestion(directCertificateResultRenewed);
						Assert.AreEqual("Replaced on this computer:\r\n"+directCertificateResultRenewed.ListDetailsReplaced[0].DescribeShort(),
							GetQuestionBlock(question,"Replaced on this computer:"));
						StringAssert.Contains(GetQuestionBlock(question,"Replaced on this computer:"),"; signing; valid from "
							+DirectCertificateImport.FormatDate(issuedSigning.DateTimeNotBefore)+" to "+DirectCertificateImport.FormatDate(issuedSigning.DateTimeNotAfter));
						StringAssert.Contains(GetQuestionBlock(question,"Kept on this computer:"),issuedEncryption.Thumbprint);
						StringAssert.EndsWith(question,"importing its .pfx or .p12 file again. Choose No to import nothing.");
						Assert.AreEqual("",DirectCertificateImport.GetReplaceWarning(directCertificateResultRenewed));
						Assert.IsTrue(DirectCertificateImport.ImportPrivateFile(ADDRESS,pathSigningRenewed,directTestPki.Password,
							new List<string> { issuedSigning.Thumbprint },DateTime.UtcNow).IsImported);
						CollectionAssert.AreEquivalent(new List<string> { issuedSigningRenewed.Thumbprint,issuedEncryption.Thumbprint },
							directTestStores.GetThumbprints(directTestStores.NamePrivate));
						//A certificate for both, valid longer: it replaces both.
						DirectTestPki.Issued issuedBoth=directTestPki.Issue(ADDRESS,dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(40));
						string pathBoth=directTestPki.WritePfx("both.pfx",issuedBoth,isCaIncluded:false);
						DirectCertificateResult directCertificateResultBoth=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathBoth,directTestPki.Password,DateTime.UtcNow);
						CollectionAssert.AreEquivalent(new List<string> { issuedSigningRenewed.Thumbprint,issuedEncryption.Thumbprint },
							directCertificateResultBoth.ListDetailsReplaced.Select(x => x.Thumbprint).ToList());
						Assert.IsTrue(DirectCertificateImport.ImportPrivateFile(ADDRESS,pathBoth,directTestPki.Password,
							directCertificateResultBoth.ListDetailsReplaced.Select(x => x.Thumbprint).ToList(),DateTime.UtcNow).IsImported);
						//A signing certificate valid longer than the one for both: offered, with the warning that the file has none for encryption.
						DirectTestPki.Issued issuedSigningLast=directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.DigitalSignature,
							dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(45));
						DirectCertificateResult directCertificateResultLast=DirectCertificateImport.CheckPrivateFile(ADDRESS,
							directTestPki.WritePfx("signing-last.pfx",issuedSigningLast,isCaIncluded:false),directTestPki.Password,DateTime.UtcNow);
						Assert.AreEqual(issuedBoth.Thumbprint,directCertificateResultLast.ListDetailsReplaced.Single().Thumbprint,"A certificate for both counts as both.");
						string warning=DirectCertificateImport.GetReplaceWarning(directCertificateResultLast);
						Assert.AreEqual("No certificate in the file is for encryption, which a replaced certificate is also for. Replace it only if your Direct provider "
							+"told you to.",warning);
						StringAssert.Contains(DirectCertificateImport.GetReplaceQuestion(directCertificateResultLast),warning);
					}
					finally {
						DeleteImportedKeys(directTestStores);
					}
				}
			}
		}

		///<summary>The purpose by the Key Usage and the Extended Key Usage: signing (digitalSignature, nonRepudiation), encryption (keyEncipherment,
		///keyAgreement), both without a Key Usage, and neither when the Extended Key Usage leaves out e-mail protection.</summary>
		[TestMethod]
		public void GetPurpose_ReadsTheKeyUsageAndTheExtendedKeyUsage() {
			using(DirectTestPki directTestPki=new DirectTestPki()) {
				DirectCertificatePurpose purposeBoth=DirectCertificatePurpose.Signing|DirectCertificatePurpose.Encryption;
				Assert.AreEqual(purposeBoth,DirectCertificateImport.GetPurpose(directTestPki.Issue(ADDRESS).X509Certificate2WithKey));
				Assert.AreEqual(DirectCertificatePurpose.Signing,DirectCertificateImport.GetPurpose(
					directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.DigitalSignature).X509Certificate2WithKey));
				Assert.AreEqual(DirectCertificatePurpose.Signing,DirectCertificateImport.GetPurpose(
					directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.NonRepudiation).X509Certificate2WithKey));
				Assert.AreEqual(DirectCertificatePurpose.Encryption,DirectCertificateImport.GetPurpose(
					directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.KeyEncipherment|X509KeyUsageFlags.DataEncipherment).X509Certificate2WithKey));
				Assert.AreEqual(DirectCertificatePurpose.Encryption,DirectCertificateImport.GetPurpose(
					directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.KeyAgreement).X509Certificate2WithKey));
				Assert.AreEqual(DirectCertificatePurpose.None,DirectCertificateImport.GetPurpose(
					directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.DataEncipherment).X509Certificate2WithKey),"Not a way to sign or to encrypt a message key.");
				Assert.AreEqual(purposeBoth,DirectCertificateImport.GetPurpose(
					directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.None,arrayEkuOids:new string[0]).X509Certificate2WithKey),"No extension: both.");
				Assert.AreEqual(DirectCertificatePurpose.None,DirectCertificateImport.GetPurpose(
					directTestPki.Issue(ADDRESS,arrayEkuOids:new[] { DirectTestPki.OID_CLIENT_AUTH }).X509Certificate2WithKey),"Not for e-mail.");
				Assert.AreEqual(purposeBoth,DirectCertificateImport.GetPurpose(
					directTestPki.Issue(ADDRESS,arrayEkuOids:new[] { DirectTestPki.OID_CLIENT_AUTH,DirectTestPki.OID_EMAIL_PROTECTION }).X509Certificate2WithKey));
				Assert.AreEqual(DirectCertificatePurpose.Signing,DirectCertificateImport.GetPurpose(
					directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.DigitalSignature,arrayEkuOids:new[] { DirectTestPki.OID_ANY_EKU }).X509Certificate2WithKey));
				Assert.AreEqual("signing and encryption",DirectCertificateImport.GetPurposeText(purposeBoth));
				Assert.AreEqual("signing",DirectCertificateImport.GetPurposeText(DirectCertificatePurpose.Signing));
				Assert.AreEqual("encryption",DirectCertificateImport.GetPurposeText(DirectCertificatePurpose.Encryption));
				Assert.AreEqual("neither signing nor encryption of e-mail",DirectCertificateImport.GetPurposeText(DirectCertificatePurpose.None));
			}
		}

		///<summary>A copy of the certificate without its key in the private store (a .cer imported into the wrong store) is the same certificate,
		///not an older one: the import asks nothing and its copy, with the key, takes the copy's place; the store then holds the one certificate,
		///with its key.</summary>
		[TestMethod]
		public void ImportPrivateFile_StoresTheKeyWithACopyOfTheSameCertificateWithoutItsKey() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				try {
					DirectTestPki.Issued issued=directTestPki.Issue(ADDRESS);
					using(X509Certificate2 x509Certificate2Public=new X509Certificate2(issued.RawData)) {
						directTestStores.AddPrivateWithoutKey(x509Certificate2Public);
					}
					Assert.IsNull(EmailMessages.GetCertFromPrivateStore(ADDRESS),"Without its key the program can't use it.");
					DirectCertificateDetails directCertificateDetailsKeyless=DirectCertificateImport.GetPrivateCertificates(ADDRESS).Single();
					Assert.IsFalse(directCertificateDetailsKeyless.HasPrivateKey);
					Assert.IsFalse(directCertificateDetailsKeyless.IsKeyUsable);
					StringAssert.Contains(directCertificateDetailsKeyless.DescribeShort(),"; no private key; ");
					string pathPfx=directTestPki.WritePfx("pat.pfx",issued,isCaIncluded:false);
					DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathPfx,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResultCheck.Error);
					Assert.AreEqual(1,directCertificateResultCheck.ListDetails.Count);
					Assert.AreEqual(0,directCertificateResultCheck.ListDetailsInStore.Count,"In the store without its key: not there yet.");
					Assert.AreEqual(0,directCertificateResultCheck.ListDetailsReplaced.Count,"The same certificate: nothing to replace, no question.");
					Assert.AreEqual(0,directCertificateResultCheck.ListDetailsKept.Count);
					DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathPfx,directTestPki.Password,null,DateTime.UtcNow);
					Assert.IsTrue(directCertificateResult.IsImported,directCertificateResult.Error);
					CollectionAssert.AreEqual(new List<string> { issued.Thumbprint },directTestStores.GetThumbprints(directTestStores.NamePrivate));
					Assert.AreEqual(issued.Thumbprint,EmailMessages.GetCertFromPrivateStore(ADDRESS)?.Thumbprint);
					using(X509Certificate2 x509Certificate2Stored=FindStored(directTestStores.NamePrivate,issued.Thumbprint)) {
						AssertKeyNotExportableButUsable(x509Certificate2Stored);
					}
				}
				finally {
					DeleteImportedKeys(directTestStores);
				}
			}
		}

		///<summary>A certificate of the store that names a key that can't be used (deleted behind the store's back, or one this user can't open)
		///is not "already in the store with its key": Check offers to import the file again instead of refusing, and the import stores a key that
		///works.</summary>
		[TestMethod]
		public void ImportPrivateFile_ImportsAgainWhenTheStoredKeyCannotBeUsed() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				try {
					DirectTestPki.Issued issued=directTestPki.Issue(ADDRESS);
					string pathPfx=directTestPki.WritePfx("pat.pfx",issued,isCaIncluded:false);
					Assert.IsTrue(DirectCertificateImport.ImportPrivateFile(ADDRESS,pathPfx,directTestPki.Password,null,DateTime.UtcNow).IsImported);
					using(X509Certificate2 x509Certificate2Stored=FindStored(directTestStores.NamePrivate,issued.Thumbprint)) {
						Assert.IsTrue(DirectCertificateImport.IsKeyUsable(x509Certificate2Stored));
						Assert.AreEqual("",DirectCertificateImport.TryDeletePrivateKey(x509Certificate2Stored),"The key is deleted behind the store's back.");
					}
					using(X509Certificate2 x509Certificate2Stored=FindStored(directTestStores.NamePrivate,issued.Thumbprint)) {
						Assert.IsTrue(x509Certificate2Stored.HasPrivateKey,"The certificate still names its key.");
						Assert.IsFalse(DirectCertificateImport.IsKeyUsable(x509Certificate2Stored),"But it does not open.");
					}
					DirectCertificateDetails directCertificateDetailsStored=DirectCertificateImport.GetPrivateCertificates(ADDRESS).Single();
					Assert.IsTrue(directCertificateDetailsStored.HasPrivateKey);
					Assert.IsFalse(directCertificateDetailsStored.IsKeyUsable);
					StringAssert.Contains(directCertificateDetailsStored.DescribeShort(),"; private key not usable on this computer; ");
					StringAssert.Contains(directCertificateDetailsStored.Describe(),"Private key: yes, but it cannot be used on this computer");
					//Check offers the import again; the import asks nothing (the same certificate) and stores a key that works.
					DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathPfx,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResultCheck.Error,"Not refused as already there.");
					Assert.AreEqual(issued.Thumbprint,directCertificateResultCheck.ListDetails.Single().Thumbprint);
					Assert.AreEqual(0,directCertificateResultCheck.ListDetailsInStore.Count);
					Assert.AreEqual(0,directCertificateResultCheck.ListDetailsReplaced.Count);
					DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathPfx,directTestPki.Password,null,DateTime.UtcNow);
					Assert.IsTrue(directCertificateResult.IsImported,directCertificateResult.Error);
					CollectionAssert.AreEqual(new List<string> { issued.Thumbprint },directTestStores.GetThumbprints(directTestStores.NamePrivate));
					using(X509Certificate2 x509Certificate2Stored=FindStored(directTestStores.NamePrivate,issued.Thumbprint)) {
						AssertKeyNotExportableButUsable(x509Certificate2Stored);
					}
					Assert.AreEqual(issued.Thumbprint,EmailMessages.GetCertFromPrivateStore(ADDRESS)?.Thumbprint);
					//Now it is there with a key that works: refused as already there.
					AssertRefused(DirectCertificateImport.CheckPrivateFile(ADDRESS,pathPfx,directTestPki.Password,DateTime.UtcNow),
						"already in this computer's Direct certificate store");
				}
				finally {
					DeleteImportedKeys(directTestStores);
				}
			}
		}

		///<summary>What an import wrote is taken back when it stops partway: the store refuses the second of two certificates, or something else
		///fails after the keys were stored. The certificate already added is removed again and every key the import stored is deleted; a key that
		///can't be deleted is named in the message. On success the key of the file's certificate it does not use (another address's) is deleted,
		///and one that can't be is named in the warning.</summary>
		[TestMethod]
		public void ImportPrivateFile_DeletesWhatItStoredWhenItStopsPartway() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				try {
					DirectTestPki.Issued issuedSigning=directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.DigitalSignature);
					DirectTestPki.Issued issuedEncryption=directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.KeyEncipherment);
					DirectTestPki.Issued issuedOther=directTestPki.Issue(ADDRESS_OTHER);
					string pathAll=directTestPki.WritePfxMany("signing-encryption-other.pfx",issuedSigning,issuedEncryption,issuedOther);
					DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathAll,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResultCheck.Error);
					Assert.AreEqual(2,directCertificateResultCheck.ListDetails.Count,"The signing and the encryption certificate; not the other address's.");
					Dictionary<string,KeyLocation> dictKeyLocations=new Dictionary<string,KeyLocation>();
					Action<X509Certificate2Collection> actionRecordKeys=x509Certificate2Collection => {
						dictKeyLocations.Clear();
						foreach(X509Certificate2 x509Certificate2 in x509Certificate2Collection) {
							dictKeyLocations[x509Certificate2.Thumbprint]=KeyLocation.Of(x509Certificate2);
						}
					};
					//(1) The store refuses the second certificate.
					int countAdds=0;
					DirectCertificateImport.ActionAfterKeysStoredForTests=actionRecordKeys;
					DirectCertificateImport.ActionBeforeStoreAddForTests=x509Certificate2 => {
						countAdds++;
						if(countAdds==2) {
							throw new CryptographicException("The test's store refuses.");
						}
					};
					DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathAll,directTestPki.Password,null,DateTime.UtcNow);
					AssertRefused(directCertificateResult,"The certificate could not be added to this computer's Direct certificate store: The test's store refuses. "
						+"Nothing was imported.");
					Assert.IsFalse(directCertificateResult.Error.Contains("Not removed"),directCertificateResult.Error);
					Assert.AreEqual(2,countAdds);
					Assert.AreEqual(3,dictKeyLocations.Count,"The import stored the file's three keys.");
					Assert.IsTrue(dictKeyLocations.Values.All(x => !x.Exists()),"Every key the import stored is deleted again.");
					Assert.AreEqual(0,directTestStores.GetThumbprints(directTestStores.NamePrivate).Count,"The first certificate is removed again.");
					//(2) Something else fails after the keys were stored.
					DirectCertificateImport.ActionBeforeStoreAddForTests=null;
					DirectCertificateImport.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						actionRecordKeys(x509Certificate2Collection);
						throw new InvalidOperationException("The test stops the import.");
					};
					directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathAll,directTestPki.Password,null,DateTime.UtcNow);
					AssertRefused(directCertificateResult,"The import failed: The test stops the import. Nothing was imported.");
					Assert.AreEqual(3,dictKeyLocations.Count);
					Assert.IsTrue(dictKeyLocations.Values.All(x => !x.Exists()));
					Assert.AreEqual(0,directTestStores.GetThumbprints(directTestStores.NamePrivate).Count);
					//(3) A key that can't be deleted again (the test deletes it first) is named.
					DirectCertificateImport.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						actionRecordKeys(x509Certificate2Collection);
						X509Certificate2 x509Certificate2Signing=x509Certificate2Collection.Cast<X509Certificate2>().Single(x => x.Thumbprint==issuedSigning.Thumbprint);
						Assert.AreEqual("",DirectCertificateImport.TryDeletePrivateKey(x509Certificate2Signing));
					};
					DirectCertificateImport.ActionBeforeStoreAddForTests=x509Certificate2 => {
						throw new CryptographicException("The test's store refuses.");
					};
					directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathAll,directTestPki.Password,null,DateTime.UtcNow);
					AssertRefused(directCertificateResult,"Nothing was imported.\r\n\r\nNot removed from this computer again:\r\nA private key this import stored could "
						+"not be deleted ("+issuedSigning.Subject+", "+issuedSigning.Thumbprint+"): ");
					Assert.AreEqual(1,Regex.Matches(directCertificateResult.Error,"could not be deleted").Count,"Only the one the test deleted.");
					Assert.IsTrue(dictKeyLocations.Values.All(x => !x.Exists()));
					//(4) Success: the key of the other address's certificate is deleted, the two imported keys stay.
					DirectCertificateImport.ActionBeforeStoreAddForTests=null;
					DirectCertificateImport.ActionAfterKeysStoredForTests=actionRecordKeys;
					directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,directTestPki.WritePfxMany("signing-other.pfx",issuedSigning,issuedOther),
						directTestPki.Password,null,DateTime.UtcNow);
					Assert.IsTrue(directCertificateResult.IsImported,directCertificateResult.Error);
					Assert.AreEqual("",directCertificateResult.Warning);
					Assert.IsFalse(dictKeyLocations[issuedOther.Thumbprint].Exists(),"The key the import does not use is deleted.");
					Assert.IsTrue(dictKeyLocations[issuedSigning.Thumbprint].Exists(),"The imported certificate's key stays.");
					//(5) Success, but the unused key can't be deleted (the test deletes it first): the warning names it.
					DirectCertificateImport.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						actionRecordKeys(x509Certificate2Collection);
						X509Certificate2 x509Certificate2Other=x509Certificate2Collection.Cast<X509Certificate2>().Single(x => x.Thumbprint==issuedOther.Thumbprint);
						Assert.AreEqual("",DirectCertificateImport.TryDeletePrivateKey(x509Certificate2Other));
					};
					directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,directTestPki.WritePfxMany("encryption-other.pfx",issuedEncryption,issuedOther),
						directTestPki.Password,null,DateTime.UtcNow);
					Assert.IsTrue(directCertificateResult.IsImported,directCertificateResult.Error);
					StringAssert.StartsWith(directCertificateResult.Warning,"A private key this import stored could not be deleted ("+issuedOther.Subject+", "
						+issuedOther.Thumbprint+"): ");
					CollectionAssert.AreEquivalent(new List<string> { issuedSigning.Thumbprint,issuedEncryption.Thumbprint },
						directTestStores.GetThumbprints(directTestStores.NamePrivate));
				}
				finally {
					DirectCertificateImport.ActionAfterKeysStoredForTests=null;
					DirectCertificateImport.ActionBeforeStoreAddForTests=null;
					DeleteImportedKeys(directTestStores);
				}
			}
		}

		///<summary>Third fix round, item 1: a .pfx with a renewed certificate and last year's of the same key (as OpenSSL writes them: one key bag,
		///the renewed certificate named by its localKeyId). Windows stores the key once for both; the import adds the renewed one only and does
		///not delete the key of last year's, which is the renewed one's. With last year's in the store (imported here, from its own file, with its
		///own key), it is replaced and its own key deleted, and the renewed one still signs. An import that stops partway deletes the shared key
		///once and names nothing as not deleted. For a file that names no key provider (OpenSSL's: .NET Framework stores the key in a CAPI
		///container) and one that names the CNG provider (a CNG key).</summary>
		[TestMethod]
		public void ImportPrivateFile_KeepsTheKeyARenewalWithTheSameKeyShares() {
			foreach(string keyProviderName in new[] { null,"Microsoft Software Key Storage Provider" }) {
				AssertRenewalWithTheSameKeyKeepsItsKey(keyProviderName);
			}
		}

		private void AssertRenewalWithTheSameKeyKeepsItsKey(string keyProviderName) {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				try {
					DirectTestPki.Issued issuedOld=directTestPki.Issue(ADDRESS,dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(10));
					DirectTestPki.Issued issuedRenewed=directTestPki.Issue(ADDRESS,dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(40),issuedSameKey:issuedOld);
					Assert.IsTrue(issuedOld.X509Certificate2WithKey.GetPublicKey().SequenceEqual(issuedRenewed.X509Certificate2WithKey.GetPublicKey()),"One key.");
					string pathShared=directTestPki.WritePfxSharedKey("renewed-and-old.pfx",keyProviderName,issuedRenewed,issuedOld);
					DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathShared,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResultCheck.Error);
					Assert.AreEqual(issuedRenewed.Thumbprint,directCertificateResultCheck.ListDetails.Single().Thumbprint,"Read without storing, the key is the renewed one's.");
					//(1) The store is empty: the renewed one is added; last year's is not, and the key it was given is the renewed one's.
					Dictionary<string,KeyLocation> dictKeyLocations=new Dictionary<string,KeyLocation>();
					DirectCertificateImport.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						dictKeyLocations.Clear();
						foreach(X509Certificate2 x509Certificate2 in x509Certificate2Collection) {
							Assert.IsTrue(x509Certificate2.HasPrivateKey,"Windows gives the stored key to both: "+x509Certificate2.Subject);
							dictKeyLocations[x509Certificate2.Thumbprint]=KeyLocation.Of(x509Certificate2);
						}
					};
					DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathShared,directTestPki.Password,null,DateTime.UtcNow);
					Assert.IsTrue(directCertificateResult.IsImported,directCertificateResult.Error);
					Assert.AreEqual(2,dictKeyLocations.Count);
					Assert.AreEqual(dictKeyLocations[issuedRenewed.Thumbprint].Name,dictKeyLocations[issuedOld.Thumbprint].Name,"One key container for both.");
					Assert.AreEqual(keyProviderName!=null,dictKeyLocations[issuedRenewed.Thumbprint].IsCng,dictKeyLocations[issuedRenewed.Thumbprint].Name);
					Assert.AreEqual("",directCertificateResult.Warning,"The key last year's was given is in use: nothing to delete, nothing to warn of.");
					CollectionAssert.AreEqual(new List<string> { issuedRenewed.Thumbprint },directTestStores.GetThumbprints(directTestStores.NamePrivate));
					Assert.IsTrue(dictKeyLocations[issuedRenewed.Thumbprint].Exists());
					using(X509Certificate2 x509Certificate2Stored=FindStored(directTestStores.NamePrivate,issuedRenewed.Thumbprint)) {
						Assert.IsTrue(DirectCertificateImport.IsKeyUsable(x509Certificate2Stored));
						AssertKeyNotExportableButUsable(x509Certificate2Stored);
					}
					Assert.AreEqual(issuedRenewed.Thumbprint,EmailMessages.GetCertFromPrivateStore(ADDRESS)?.Thumbprint);
					DeleteImportedKeys(directTestStores);
					directTestStores.RemovePrivate(issuedRenewed.X509Certificate2WithKey);
					//(2) Last year's is in the store, imported here from its own file with its own key: it is replaced, its key deleted, and the renewed
					//one, added with the key the file gives both, still signs.
					DirectCertificateImport.ActionAfterKeysStoredForTests=null;
					Assert.IsTrue(DirectCertificateImport.ImportPrivateFile(ADDRESS,directTestPki.WritePfx("old.pfx",issuedOld,isCaIncluded:false),directTestPki.Password,null,
						DateTime.UtcNow).IsImported);
					KeyLocation keyLocationOldInStore;
					using(X509Certificate2 x509Certificate2Old=FindStored(directTestStores.NamePrivate,issuedOld.Thumbprint)) {
						keyLocationOldInStore=KeyLocation.Of(x509Certificate2Old);
					}
					directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathShared,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual(issuedOld.Thumbprint,directCertificateResultCheck.ListDetailsReplaced.Single().Thumbprint);
					DirectCertificateImport.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						dictKeyLocations.Clear();
						foreach(X509Certificate2 x509Certificate2 in x509Certificate2Collection) {
							dictKeyLocations[x509Certificate2.Thumbprint]=KeyLocation.Of(x509Certificate2);
						}
					};
					directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathShared,directTestPki.Password,new List<string> { issuedOld.Thumbprint },
						DateTime.UtcNow);
					Assert.IsTrue(directCertificateResult.IsImported,directCertificateResult.Error);
					Assert.AreEqual("",directCertificateResult.Warning);
					Assert.AreNotEqual(keyLocationOldInStore.Name,dictKeyLocations[issuedRenewed.Thumbprint].Name,"Each import stores its own container.");
					Assert.IsFalse(keyLocationOldInStore.Exists(),"The replaced certificate's own key, imported here, is deleted.");
					Assert.IsTrue(dictKeyLocations[issuedRenewed.Thumbprint].Exists(),"The renewed one's stays, though last year's of the file had it too.");
					CollectionAssert.AreEqual(new List<string> { issuedRenewed.Thumbprint },directTestStores.GetThumbprints(directTestStores.NamePrivate));
					using(X509Certificate2 x509Certificate2Stored=FindStored(directTestStores.NamePrivate,issuedRenewed.Thumbprint)) {
						AssertKeyNotExportableButUsable(x509Certificate2Stored);
					}
					DeleteImportedKeys(directTestStores);
					directTestStores.RemovePrivate(issuedRenewed.X509Certificate2WithKey);
					//(3) The import stops right after the keys were stored: Undo deletes the key both were given once, and names nothing as not deleted
					//(the second certificate's key is the one already deleted).
					DirectCertificateImport.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						dictKeyLocations.Clear();
						foreach(X509Certificate2 x509Certificate2 in x509Certificate2Collection) {
							dictKeyLocations[x509Certificate2.Thumbprint]=KeyLocation.Of(x509Certificate2);
						}
						throw new InvalidOperationException("The test stops the import.");
					};
					directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathShared,directTestPki.Password,null,DateTime.UtcNow);
					Assert.AreEqual("The import failed: The test stops the import. Nothing was imported.",directCertificateResult.Error);
					Assert.AreEqual(2,dictKeyLocations.Count);
					Assert.AreEqual(dictKeyLocations[issuedRenewed.Thumbprint].Name,dictKeyLocations[issuedOld.Thumbprint].Name);
					Assert.IsTrue(dictKeyLocations.Values.All(x => !x.Exists()),"The stored key is deleted.");
					//(4) The store refuses the renewed one: the same.
					DirectCertificateImport.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						dictKeyLocations.Clear();
						foreach(X509Certificate2 x509Certificate2 in x509Certificate2Collection) {
							dictKeyLocations[x509Certificate2.Thumbprint]=KeyLocation.Of(x509Certificate2);
						}
					};
					DirectCertificateImport.ActionBeforeStoreAddForTests=x509Certificate2 => {
						throw new CryptographicException("The test's store refuses.");
					};
					directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathShared,directTestPki.Password,null,DateTime.UtcNow);
					AssertRefused(directCertificateResult,"The test's store refuses. Nothing was imported.");
					Assert.IsFalse(directCertificateResult.Error.Contains("Not removed"),directCertificateResult.Error);
					Assert.AreEqual(2,dictKeyLocations.Count);
					Assert.IsTrue(dictKeyLocations.Values.All(x => !x.Exists()),"The stored key is deleted.");
					Assert.AreEqual(0,directTestStores.GetThumbprints(directTestStores.NamePrivate).Count);
				}
				finally {
					DirectCertificateImport.ActionAfterKeysStoredForTests=null;
					DirectCertificateImport.ActionBeforeStoreAddForTests=null;
					DeleteImportedKeys(directTestStores);
				}
			}
		}

		///<summary>Third fix round, item 1: a signing and an encryption certificate of one key, both in the store (put there from one .pfx by the
		///Windows tools, which store the key once) and both marked as imported here. A renewed signing certificate replaces the signing one; the
		///encryption one stays (another purpose), and so does the key it shares with the replaced one: it still decrypts. A CAPI and a CNG key.</summary>
		[TestMethod]
		public void ImportPrivateFile_ReplacingKeepsTheKeyAKeptCertificateShares() {
			foreach(string keyProviderName in new[] { null,"Microsoft Software Key Storage Provider" }) {
				AssertReplacingKeepsTheSharedKey(keyProviderName);
			}
		}

		private void AssertReplacingKeepsTheSharedKey(string keyProviderName) {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				X509Certificate2Collection x509Certificate2CollectionShared=new X509Certificate2Collection();
				try {
					DirectTestPki.Issued issuedSigning=directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.DigitalSignature,
						dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(10));
					DirectTestPki.Issued issuedEncryption=directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.KeyEncipherment,
						dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(30),issuedSameKey:issuedSigning);
					byte[] byteArrayShared=File.ReadAllBytes(directTestPki.WritePfxSharedKey("signing-encryption-one-key.pfx",keyProviderName,issuedSigning,
						issuedEncryption));
					x509Certificate2CollectionShared.Import(byteArrayShared,directTestPki.Password,X509KeyStorageFlags.PersistKeySet|X509KeyStorageFlags.UserKeySet);
					Assert.AreEqual(2,x509Certificate2CollectionShared.Count);
					foreach(X509Certificate2 x509Certificate2 in x509Certificate2CollectionShared) {
						x509Certificate2.FriendlyName=DirectCertificateImport.FRIENDLY_NAME_IMPORTED;
						directTestStores.AddPrivate(x509Certificate2);
					}
					KeyLocation keyLocationShared;
					using(X509Certificate2 x509Certificate2Signing=FindStored(directTestStores.NamePrivate,issuedSigning.Thumbprint))
					using(X509Certificate2 x509Certificate2Encryption=FindStored(directTestStores.NamePrivate,issuedEncryption.Thumbprint))
					{
						keyLocationShared=KeyLocation.Of(x509Certificate2Encryption);
						Assert.AreEqual(keyLocationShared.Name,KeyLocation.Of(x509Certificate2Signing).Name,"One key container for both.");
						Assert.AreEqual(keyProviderName!=null,keyLocationShared.IsCng,keyLocationShared.Name);
						Assert.IsTrue(DirectCertificateImport.IsKeyUsable(x509Certificate2Encryption));
					}
					DirectTestPki.Issued issuedSigningRenewed=directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.DigitalSignature,
						dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(50));
					string pathRenewed=directTestPki.WritePfx("signing-renewed.pfx",issuedSigningRenewed,isCaIncluded:false);
					DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathRenewed,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual(issuedSigning.Thumbprint,directCertificateResultCheck.ListDetailsReplaced.Single().Thumbprint);
					Assert.AreEqual(issuedEncryption.Thumbprint,directCertificateResultCheck.ListDetailsKept.Single().Thumbprint);
					DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathRenewed,directTestPki.Password,
						new List<string> { issuedSigning.Thumbprint },DateTime.UtcNow);
					Assert.IsTrue(directCertificateResult.IsImported,directCertificateResult.Error);
					Assert.AreEqual("",directCertificateResult.Warning,"The replaced one's key is the kept one's: nothing to delete, nothing to warn of.");
					CollectionAssert.AreEquivalent(new List<string> { issuedSigningRenewed.Thumbprint,issuedEncryption.Thumbprint },
						directTestStores.GetThumbprints(directTestStores.NamePrivate));
					Assert.IsTrue(keyLocationShared.Exists(),"The kept certificate's key stays.");
					using(X509Certificate2 x509Certificate2Encryption=FindStored(directTestStores.NamePrivate,issuedEncryption.Thumbprint)) {
						Assert.IsTrue(DirectCertificateImport.IsKeyUsable(x509Certificate2Encryption),"It still decrypts.");
					}
				}
				finally {
					//Quietly: the signing and the encryption certificate may both still be in the store, with the one key (a failure above).
					using(X509Store x509Store=new X509Store(directTestStores.NamePrivate,StoreLocation.CurrentUser)) {
						x509Store.Open(OpenFlags.ReadOnly);
						foreach(X509Certificate2 x509Certificate2 in x509Store.Certificates) {
							if(x509Certificate2.FriendlyName==DirectCertificateImport.FRIENDLY_NAME_IMPORTED) {
								DirectCertificateImport.TryDeletePrivateKey(x509Certificate2);
							}
						}
					}
					foreach(X509Certificate2 x509Certificate2 in x509Certificate2CollectionShared) {
						DirectCertificateImport.TryDeletePrivateKey(x509Certificate2);//The key the test stored, if a failure left it ("" or already gone).
						x509Certificate2.Reset();
					}
				}
			}
		}

		///<summary>Third fix round, item 3: something throws after the certificate was added. The warnings collected until then (a key of the file
		///not deleted, a replaced certificate the store did not remove) are in Warning, before the line that says the import stopped.</summary>
		[TestMethod]
		public void ImportPrivateFile_KeepsTheWarningsWhenItStopsAfterAdding() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				try {
					DirectTestPki.Issued issuedStore1=directTestPki.Issue(ADDRESS,dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(10));
					DirectTestPki.Issued issuedStore2=directTestPki.Issue(ADDRESS,dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(15));
					directTestStores.AddPrivate(issuedStore1.X509Certificate2WithKey);
					directTestStores.AddPrivate(issuedStore2.X509Certificate2WithKey);
					DirectTestPki.Issued issuedNew=directTestPki.Issue(ADDRESS,dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(40));
					DirectTestPki.Issued issuedOther=directTestPki.Issue(ADDRESS_OTHER);
					string pathNew=directTestPki.WritePfxMany("new-other.pfx",issuedNew,issuedOther);
					DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathNew,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual(2,directCertificateResultCheck.ListDetailsReplaced.Count);
					//The other address's key can't be deleted (the test deletes it first); the store refuses to remove the first replaced certificate and
					//something else stops the import at the second.
					DirectCertificateImport.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						X509Certificate2 x509Certificate2Other=x509Certificate2Collection.Cast<X509Certificate2>().Single(x => x.Thumbprint==issuedOther.Thumbprint);
						Assert.AreEqual("",DirectCertificateImport.TryDeletePrivateKey(x509Certificate2Other));
					};
					List<X509Certificate2> listRemoving=new List<X509Certificate2>();
					DirectCertificateImport.ActionBeforeStoreRemoveForTests=x509Certificate2 => {
						listRemoving.Add(new X509Certificate2(x509Certificate2.RawData));
						if(listRemoving.Count==1) {
							throw new CryptographicException("The test's store refuses to remove it.");
						}
						throw new InvalidOperationException("The test stops the import.");
					};
					DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathNew,directTestPki.Password,
						directCertificateResultCheck.ListDetailsReplaced.Select(x => x.Thumbprint).ToList(),DateTime.UtcNow);
					Assert.IsTrue(directCertificateResult.IsImported);
					Assert.AreEqual("",directCertificateResult.Error);
					Assert.AreEqual(2,listRemoving.Count);
					string[] arrayLines=directCertificateResult.Warning.Split(new[] { "\r\n" },StringSplitOptions.None);
					Assert.AreEqual(3,arrayLines.Length,directCertificateResult.Warning);
					StringAssert.StartsWith(arrayLines[0],"A private key this import stored could not be deleted ("+issuedOther.Subject+", "+issuedOther.Thumbprint+"): ");
					Assert.AreEqual("The replaced certificate could not be removed from the store ("+listRemoving[0].Subject+", "+listRemoving[0].Thumbprint+"): "
						+"The test's store refuses to remove it.",arrayLines[1]);
					Assert.AreEqual("The import stopped after the certificate was added: The test stops the import.",arrayLines[2]);
					CollectionAssert.AreEquivalent(new List<string> { issuedNew.Thumbprint,issuedStore1.Thumbprint,issuedStore2.Thumbprint },
						directTestStores.GetThumbprints(directTestStores.NamePrivate),"The new one is in; neither replaced one was removed.");
					listRemoving.ForEach(x => x.Dispose());
				}
				finally {
					DirectCertificateImport.ActionAfterKeysStoredForTests=null;
					DirectCertificateImport.ActionBeforeStoreRemoveForTests=null;
					DeleteImportedKeys(directTestStores);
				}
			}
		}

		///<summary>Third fix round, item 4: IsKeyUsable, TryDeletePrivateKey and GetKeyContainerName ("Never throws") answer instead of throwing for
		///every exception but the critical ones.</summary>
		[TestMethod]
		public void KeyChecks_NeverThrowButLetTheCriticalExceptionsThrough() {
			Assert.IsFalse(DirectCertificateImport.IsKeyUsable(null),"A NullReferenceException is not usable.");
			Assert.AreNotEqual("",DirectCertificateImport.TryDeletePrivateKey(null),"A NullReferenceException is not deleted, with why.");
			Assert.IsNull(DirectCertificateImport.GetKeyContainerName(null));
			Assert.IsTrue(DirectCertificateImport.IsCriticalException(new OutOfMemoryException()));
			Assert.IsTrue(DirectCertificateImport.IsCriticalException(new StackOverflowException()));
			foreach(Exception ex in new Exception[] { new CryptographicException(),new NullReferenceException(),new InvalidOperationException(),
				new ArgumentException(),new IOException(),new UnauthorizedAccessException(),new NotSupportedException() })
			{
				Assert.IsFalse(DirectCertificateImport.IsCriticalException(ex),ex.GetType().Name);
			}
			string textBusiness=ReadNormalized(@"OpenPmsDentalBusiness\Email\DirectCertificateImport.cs");
			StringAssert.Contains(textBusiness,"return ex is OutOfMemoryException || ex is StackOverflowException || ex is ThreadAbortException;");
			foreach(string method in new[] { "public static bool IsKeyUsable(","internal static string TryDeletePrivateKey(","internal static string GetKeyContainerName(" }) {
				int indexStart=textBusiness.IndexOf(method,StringComparison.Ordinal);
				int indexEnd=textBusiness.IndexOf("\n\t\t}\n",indexStart,StringComparison.Ordinal);
				string textMethod=textBusiness.Substring(indexStart,indexEnd-indexStart);
				Assert.AreEqual(1,Regex.Matches(textMethod,@"catch\(").Count,method);
				StringAssert.Contains(textMethod,"catch(Exception ex) when(!IsCriticalException(ex)) {",method);
			}
		}

		///<summary>Third fix round, item 5: the store holds a copy without its key of each of the file's two certificates. The first added takes
		///its copy's place (with its key); the store refuses the second. Undo removes the first and puts its copy back, and deletes every key the
		///import stored: the store holds the two copies without keys, as before.</summary>
		[TestMethod]
		public void ImportPrivateFile_UndoPutsBackTheKeylessCopyItOverwrote() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				try {
					DirectTestPki.Issued issuedSigning=directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.DigitalSignature);
					DirectTestPki.Issued issuedEncryption=directTestPki.Issue(ADDRESS,keyUsageFlags:X509KeyUsageFlags.KeyEncipherment);
					directTestStores.AddPrivateWithoutKey(issuedSigning.X509Certificate2WithKey);
					directTestStores.AddPrivateWithoutKey(issuedEncryption.X509Certificate2WithKey);
					string pathBoth=directTestPki.WritePfxMany("signing-encryption.pfx",issuedSigning,issuedEncryption);
					DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathBoth,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual(2,directCertificateResultCheck.ListDetails.Count,"Both are offered: the store's copies have no key.");
					Assert.AreEqual(0,directCertificateResultCheck.ListDetailsReplaced.Count);
					Dictionary<string,KeyLocation> dictKeyLocations=new Dictionary<string,KeyLocation>();
					DirectCertificateImport.ActionAfterKeysStoredForTests=x509Certificate2Collection => {
						foreach(X509Certificate2 x509Certificate2 in x509Certificate2Collection) {
							dictKeyLocations[x509Certificate2.Thumbprint]=KeyLocation.Of(x509Certificate2);
						}
					};
					List<string> listThumbprintsAdding=new List<string>();
					bool isFirstWithKey=false;
					DirectCertificateImport.ActionBeforeStoreAddForTests=x509Certificate2 => {
						listThumbprintsAdding.Add(x509Certificate2.Thumbprint);
						if(listThumbprintsAdding.Count<2) {
							return;
						}
						using(X509Certificate2 x509Certificate2First=FindStored(directTestStores.NamePrivate,listThumbprintsAdding[0])) {
							isFirstWithKey=x509Certificate2First.HasPrivateKey;//The first took its copy's place.
						}
						throw new CryptographicException("The test's store refuses.");
					};
					DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathBoth,directTestPki.Password,null,DateTime.UtcNow);
					AssertRefused(directCertificateResult,"The certificate could not be added to this computer's Direct certificate store: The test's store refuses. "
						+"Nothing was imported.");
					Assert.IsFalse(directCertificateResult.Error.Contains("Not removed"),directCertificateResult.Error);
					Assert.AreEqual(2,listThumbprintsAdding.Count);
					Assert.IsTrue(isFirstWithKey,"Before the refusal the first was in the store with its key, in its copy's place.");
					CollectionAssert.AreEquivalent(new List<string> { issuedSigning.Thumbprint,issuedEncryption.Thumbprint },
						directTestStores.GetThumbprints(directTestStores.NamePrivate),"Both copies are there again.");
					foreach(string thumbprint in listThumbprintsAdding) {
						using(X509Certificate2 x509Certificate2Stored=FindStored(directTestStores.NamePrivate,thumbprint)) {
							Assert.IsFalse(x509Certificate2Stored.HasPrivateKey,"The copy as it was, without a key: "+thumbprint);
							Assert.AreNotEqual(DirectCertificateImport.FRIENDLY_NAME_IMPORTED,x509Certificate2Stored.FriendlyName);
						}
					}
					Assert.AreEqual(2,dictKeyLocations.Count);
					Assert.IsTrue(dictKeyLocations.Values.All(x => !x.Exists()),"Every key the import stored is deleted.");
				}
				finally {
					DirectCertificateImport.ActionAfterKeysStoredForTests=null;
					DirectCertificateImport.ActionBeforeStoreAddForTests=null;
					DeleteImportedKeys(directTestStores);
				}
			}
		}

		///<summary>Third fix round, item 6: when Windows' import of the file with PersistKeySet fails, .NET gives no certificate, so the import knows
		///of no key to delete: the message says that nothing was imported and that Windows may have left a private key of the file behind.</summary>
		[TestMethod]
		public void ImportPrivateFile_SaysWindowsMayHaveLeftAKeyWhenStoringTheKeysFails() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				try {
					DirectTestPki.Issued issued=directTestPki.Issue(ADDRESS);
					string pathPfx=directTestPki.WritePfx("pat.pfx",issued,isCaIncluded:false);
					DirectCertificateImport.ActionBeforeKeysStoredForTests=() => {
						throw new CryptographicException("The test's import fails.");
					};
					DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathPfx,directTestPki.Password,null,DateTime.UtcNow);
					Assert.IsFalse(directCertificateResult.IsImported);
					Assert.AreEqual("The certificate's private key could not be stored on this computer: The test's import fails. Nothing was imported. Windows may "
						+"have left a private key of the file behind on this computer, which the program cannot find to delete.",directCertificateResult.Error);
					Assert.AreEqual(0,directTestStores.GetThumbprints(directTestStores.NamePrivate).Count);
				}
				finally {
					DirectCertificateImport.ActionBeforeKeysStoredForTests=null;
				}
			}
		}

		///<summary>The security log text is plain English with invariant dates and numbers, whatever the user's culture (a Thai or Arabic calendar
		///writes another year), and is never translated.</summary>
		[TestMethod]
		public void GetLogText_IsPlainEnglishWithInvariantDates() {
			DirectCertificateResult directCertificateResult=new DirectCertificateResult();
			DirectCertificateDetails directCertificateDetails=new DirectCertificateDetails();
			directCertificateDetails.Thumbprint="0123456789ABCDEF0123456789ABCDEF01234567";
			directCertificateDetails.DateTimeNotAfter=new DateTime(2027,3,4,10,0,0);
			directCertificateResult.ListDetails.Add(directCertificateDetails);
			directCertificateResult.ListDetailsReplaced.Add(new DirectCertificateDetails());
			directCertificateResult.ListDetailsKept.Add(new DirectCertificateDetails());
			directCertificateResult.ListDetailsKept.Add(new DirectCertificateDetails());
			CultureInfo cultureInfoOld=Thread.CurrentThread.CurrentCulture;
			CultureInfo cultureInfoUiOld=Thread.CurrentThread.CurrentUICulture;
			try {
				foreach(string cultureName in new[] { "en-US","th-TH","ar-SA","de-DE","fr-CA" }) {
					Thread.CurrentThread.CurrentCulture=new CultureInfo(cultureName);
					Thread.CurrentThread.CurrentUICulture=new CultureInfo(cultureName);
					Assert.AreEqual("Direct certificate imported from the provider's file for EmailAddressNum 1234567: 0123456789ABCDEF0123456789ABCDEF01234567 (valid to "
						+"2027-03-04); certificates of the store replaced: 1, kept: 2.",DirectCertificateImport.GetLogText(directCertificateResult,1234567,isAnchor:false),cultureName);
					Assert.AreEqual("Direct trust anchor imported from a file: 0123456789ABCDEF0123456789ABCDEF01234567 (valid to 2027-03-04).",
						DirectCertificateImport.GetLogText(directCertificateResult,0,isAnchor:true),cultureName);
				}
			}
			finally {
				Thread.CurrentThread.CurrentCulture=cultureInfoOld;
				Thread.CurrentThread.CurrentUICulture=cultureInfoUiOld;
			}
			string textBusiness=ReadNormalized(@"OpenPmsDentalBusiness\Email\DirectCertificateImport.cs");
			int indexLog=textBusiness.IndexOf("public static string GetLogText(",StringComparison.Ordinal);
			string textLog=textBusiness.Substring(indexLog,textBusiness.IndexOf("public static string GetAddressError(",StringComparison.Ordinal)-indexLog);
			Assert.IsFalse(textLog.Contains("Lans.g"),"The log is not translated.");
			StringAssert.Contains(textLog,"ToString(\"yyyy-MM-dd\",CultureInfo.InvariantCulture)");
		}

		///<summary>The provider's trust anchor (DER .cer and PEM .pem): shown, added to the trust anchors only, and then the address's own
		///certificate, issued under it, is trusted (the inbox's IsSenderTrusted). A second import of it is refused as already there.</summary>
		[TestMethod]
		public void ImportAnchorFile_TrustsTheAddressIssuedUnderTheProvidersAnchor() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				try {
					DirectTestPki.Issued issued=directTestPki.Issue(ADDRESS);
					EmailMessages.CreateCertificateStoresIfNeeded();//As the program does when it starts.
					Assert.IsTrue(DirectCertificateImport.ImportPrivateFile(ADDRESS,directTestPki.WritePfx("pat.pfx",issued,isCaIncluded:true),directTestPki.Password,null,
						DateTime.UtcNow).IsImported);
					Assert.IsFalse(EmailMessages.IsSenderTrusted(ADDRESS),"No trust anchor yet.");
					string pathCer=directTestPki.WriteFile("hisp-anchor.cer",directTestPki.CaRawData);
					DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckAnchorFile(pathCer,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResultCheck.Error);
					Assert.AreEqual(1,directCertificateResultCheck.ListDetails.Count);
					Assert.AreEqual(directTestPki.CaSubject,directCertificateResultCheck.ListDetails[0].Subject);
					Assert.AreEqual(directTestPki.CaSubject,directCertificateResultCheck.ListDetails[0].Issuer);
					Assert.AreEqual(directTestPki.CaThumbprint,directCertificateResultCheck.ListDetails[0].Thumbprint);
					Assert.IsFalse(directCertificateResultCheck.ListDetails[0].HasPrivateKey);
					StringAssert.Contains(directCertificateResultCheck.ListDetails[0].Describe(),"Serial number: "+directTestPki.CaSerialNumber);
					Assert.AreEqual(0,directTestStores.GetThumbprints(directTestStores.NameAnchor).Count,"Check adds nothing.");
					DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportAnchorFile(pathCer,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResult.Error);
					Assert.IsTrue(directCertificateResult.IsImported);
					CollectionAssert.AreEqual(new List<string> { directTestPki.CaThumbprint },directTestStores.GetThumbprints(directTestStores.NameAnchor));
					CollectionAssert.AreEqual(new List<string> { issued.Thumbprint },directTestStores.GetThumbprints(directTestStores.NamePrivate),
						"The private store is not touched.");
					Assert.AreEqual(0,directTestStores.GetThumbprints(directTestStores.NameExternal).Count);
					Assert.IsTrue(EmailMessages.IsSenderTrusted(ADDRESS),"The address's certificate was issued under the anchor.");
					Assert.IsFalse(EmailMessages.IsSenderTrusted(ADDRESS_OTHER));
					StringAssert.Contains(DirectCertificateImport.GetLogText(directCertificateResult,0,isAnchor:true),directTestPki.CaThumbprint);
					//Again, as a PEM file: already there.
					string pathPem=directTestPki.WriteFile("hisp-anchor.pem",Encoding.ASCII.GetBytes(DirectTestPki.ToPem(directTestPki.CaRawData)));
					DirectCertificateResult directCertificateResultAgain=DirectCertificateImport.ImportAnchorFile(pathPem,DateTime.UtcNow);
					AssertRefused(directCertificateResultAgain,"already a trust anchor");
					Assert.AreEqual(1,directCertificateResultAgain.ListDetailsInStore.Count);
					Assert.AreEqual(1,directTestStores.GetThumbprints(directTestStores.NameAnchor).Count);
				}
				finally {
					DeleteImportedKeys(directTestStores);
				}
			}
		}

		///<summary>The anchor file types and formats: binary and Base64 (PEM) in .cer, .crt, .der and .pem files are read; another type, a file
		///with a private key, more than one certificate, an expired or not yet valid certificate, a damaged file: refused, nothing added.</summary>
		[TestMethod]
		public void CheckAndImportAnchorFile_RefuseWhatIsNotOneValidPublicCertificate() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				DirectTestPki.Issued issued=directTestPki.Issue(ADDRESS);
				string pem=DirectTestPki.ToPem(directTestPki.CaRawData);
				foreach(string fileName in new[] { "anchor.cer","anchor.crt","anchor.der","ANCHOR.PEM" }) {
					foreach(byte[] byteArray in new[] { directTestPki.CaRawData,Encoding.ASCII.GetBytes(pem) }) {
						DirectCertificateResult directCertificateResult=DirectCertificateImport.CheckAnchorFile(directTestPki.WriteFile(fileName,byteArray),DateTime.UtcNow);
						Assert.AreEqual("",directCertificateResult.Error,fileName);
						Assert.AreEqual(directTestPki.CaThumbprint,directCertificateResult.ListDetails[0].Thumbprint,fileName);
					}
				}
				AssertRefused(DirectCertificateImport.CheckAnchorFile(directTestPki.WritePfx("anchor.pfx",issued,isCaIncluded:false),DateTime.UtcNow),
					"A trust anchor must be a .cer, .crt, .der or .pem file");
				AssertRefused(DirectCertificateImport.CheckAnchorFile("",DateTime.UtcNow),"Pick the certificate file first");
				AssertRefused(DirectCertificateImport.CheckAnchorFile(Path.Combine(directTestPki.Folder,"missing.cer"),DateTime.UtcNow),"The file was not found");
				//A .pfx under another name: with an empty password it opens, with its key: refused (and the key is never stored).
				string pathPfxAsCer=directTestPki.WriteFile("pfx-as.cer",directTestPki.ExportPfx(issued,isCaIncluded:false,password:""));
				AssertRefused(DirectCertificateImport.CheckAnchorFile(pathPfxAsCer,DateTime.UtcNow),"The file holds a private key");
				AssertRefused(DirectCertificateImport.CheckAnchorFile(directTestPki.WriteFile("pfx-password.cer",directTestPki.ExportPfx(issued,isCaIncluded:false,
					password:directTestPki.Password)),DateTime.UtcNow),"not a certificate file");
				//A .pfx in a PEM block: read with EphemeralKeySet too, so its key is never written anywhere; refused for the key.
				string pemPfx=DirectTestPki.ToPem(directTestPki.ExportPfx(issued,isCaIncluded:false,password:""));
				AssertRefused(DirectCertificateImport.CheckAnchorFile(directTestPki.WriteFile("pfx-in.pem",Encoding.ASCII.GetBytes(pemPfx)),DateTime.UtcNow),
					"The file holds a private key");
				AssertRefused(DirectCertificateImport.ImportAnchorFile(directTestPki.WriteFile("pfx-in.crt",Encoding.ASCII.GetBytes(pemPfx)),DateTime.UtcNow),
					"The file holds a private key");
				AssertRefused(DirectCertificateImport.CheckAnchorFile(directTestPki.WriteFile("pfx-password-in.pem",Encoding.ASCII.GetBytes(
					DirectTestPki.ToPem(directTestPki.ExportPfx(issued,isCaIncluded:false,password:directTestPki.Password)))),DateTime.UtcNow),"not a certificate file");
				AssertRefused(DirectCertificateImport.CheckAnchorFile(directTestPki.WriteFile("two.pem",Encoding.ASCII.GetBytes(pem+DirectTestPki.ToPem(issued.RawData))),
					DateTime.UtcNow),"The file holds 2 certificates");
				AssertRefused(DirectCertificateImport.CheckAnchorFile(directTestPki.WriteFile("damaged.pem",Encoding.ASCII.GetBytes(pem.Replace("-----END CERTIFICATE-----",""))),
					DateTime.UtcNow),"not a certificate file");
				AssertRefused(DirectCertificateImport.CheckAnchorFile(directTestPki.WriteFile("garbage.cer",Encoding.ASCII.GetBytes("not a certificate")),DateTime.UtcNow),
					"not a certificate file");
				DirectTestPki.Issued issuedExpired=directTestPki.Issue(ADDRESS,dateTimeOffsetNotBefore:DateTimeOffset.UtcNow.AddDays(-30),
					dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(-1));
				AssertRefused(DirectCertificateImport.CheckAnchorFile(directTestPki.WriteFile("expired.cer",issuedExpired.RawData),DateTime.UtcNow),"The certificate expired on");
				DirectTestPki.Issued issuedNotYet=directTestPki.Issue(ADDRESS,dateTimeOffsetNotBefore:DateTimeOffset.UtcNow.AddDays(5),
					dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(20));
				AssertRefused(DirectCertificateImport.ImportAnchorFile(directTestPki.WriteFile("notyet.crt",issuedNotYet.RawData),DateTime.UtcNow),"not valid yet");
				AssertRefused(DirectCertificateImport.ImportAnchorFile(pathPfxAsCer,DateTime.UtcNow),"The file holds a private key");
				Assert.IsTrue(directTestStores.AreStoresDeleted(),"Nothing was written to a store.");
			}
		}

		///<summary>The certificate window, which can't run in a test: it imports the provider's file through DirectCertificateImport, asks before it
		///replaces or trusts anything, logs without the password, and no longer calls Open Dental's servers.</summary>
		[TestMethod]
		public void Window_ImportsThroughTheBusinessLayerAndAsksFirst() {
			string textWindow=ReadNormalized(@"OpenPmsDental\Forms\FormEmailCertRegister.cs");
			foreach(string gone in new[] { "CustomerUpdatesProxy","RequestEmailVerificationCode","PostEmailCertificate","RegistrationKey","VerificationCode" }) {
				Assert.IsFalse(textWindow.Contains(gone),gone);
			}
			StringAssert.Contains(textWindow,"public FormEmailCertRegister(string emailAddress,long emailAddressNum)");
			StringAssert.Contains(textWindow,"DirectCertificateImport.CheckPrivateFile(_emailAddress,textCertFilePath.Text,textPassword.Text,");
			StringAssert.Contains(textWindow,"DirectCertificateImport.ImportPrivateFile(_emailAddress,textCertFilePath.Text,textPassword.Text,");
			StringAssert.Contains(textWindow,"DirectCertificateImport.CheckAnchorFile(textAnchorFilePath.Text,DateTime.UtcNow)");
			StringAssert.Contains(textWindow,"DirectCertificateImport.ImportAnchorFile(textAnchorFilePath.Text,DateTime.UtcNow)");
			//The older certificates are replaced only with a Yes, and only those the question named.
			int indexImport=textWindow.IndexOf("private void butImport_Click(",StringComparison.Ordinal);
			string textImport=textWindow.Substring(indexImport,textWindow.IndexOf("private void butBrowseAnchor_Click(",StringComparison.Ordinal)-indexImport);
			Assert.IsTrue(Regex.IsMatch(textImport,@"if\(directCertificateResultCheck\.ListDetailsReplaced\.Count>0\) \{.*MessageBoxButtons\.YesNo\)!=DialogResult\.Yes\) \{\s*return;\s*\}"
				+@"\s*listThumbprintsReplaceAllowed=directCertificateResultCheck\.ListDetailsReplaced\.Select\(x => x\.Thumbprint\)\.ToList\(\);",RegexOptions.Singleline),
				"Yes before the thumbprints are allowed.");
			Assert.IsTrue(textImport.IndexOf("MessageBoxButtons.YesNo",StringComparison.Ordinal)<textImport.IndexOf("DirectCertificateImport.ImportPrivateFile(",StringComparison.Ordinal));
			//The question is the business layer's, which lists exactly the store's certificates it replaces, both sides with subject, purpose, valid
			//from and to, and ends "Choose No to import nothing." (CheckPrivateFile_OffersOnlyAnOlderCertificateOfTheSamePurpose); the details box
			//shows the same lists.
			StringAssert.Contains(textImport,"string question=DirectCertificateImport.GetReplaceQuestion(directCertificateResultCheck);\n"
				+"\t\t\t\tif(MessageBox.Show(this,question,Text,MessageBoxButtons.YesNo)!=DialogResult.Yes) {");
			Assert.IsFalse(textWindow.ToLowerInvariant().Contains("older"),"The window names no age; the business layer's lists say what is replaced and kept.");
			int indexDescribeCheck=textWindow.IndexOf("private string DescribeCheck(",StringComparison.Ordinal);
			string textDescribeCheck=textWindow.Substring(indexDescribeCheck,textWindow.IndexOf("private void butImport_Click(",StringComparison.Ordinal)-indexDescribeCheck);
			StringAssert.Contains(textDescribeCheck,"DirectCertificateImport.GetReplaceWarning(directCertificateResult)");
			StringAssert.Contains(textDescribeCheck,"directCertificateResult.ListDetailsInStore.Select(x => x.DescribeShort())","The file's certificates already there with their key.");
			StringAssert.Contains(textDescribeCheck,"DirectCertificateImport.GetKeptText(directCertificateResult)","The store's certificates kept.");
			//A failed re-check at Import clears the details, as Check does.
			Assert.IsTrue(Regex.IsMatch(textImport,@"if\(directCertificateResultCheck\.Error!=""""\) \{\s*textDetails\.Text="""";\s*butImport\.Enabled=false;\s*"
				+@"MessageBox\.Show\(this,directCertificateResultCheck\.Error,Text\);"),"The re-check's failure clears the details.");
			int indexCheck=textWindow.IndexOf("private void butCheck_Click(",StringComparison.Ordinal);
			Assert.IsTrue(Regex.IsMatch(textWindow.Substring(indexCheck,indexDescribeCheck-indexCheck),@"if\(directCertificateResult\.Error!=""""\) \{\s*textDetails\.Text="""";"));
			StringAssert.Contains(textImport,"SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,DirectCertificateImport.GetLogText(directCertificateResult,_emailAddressNum,isAnchor:false));");
			StringAssert.Contains(textImport,"textPassword.Text=\"\";");
			StringAssert.Contains(textImport,"string kept=DirectCertificateImport.GetKeptText(directCertificateResult);","The message after the import lists the kept ones.");
			//The certificate is stored for the user name as typed; sending uses it once Edit Email Address is saved.
			StringAssert.Contains(textImport,"\"It is stored for the user name as typed in Edit Email Address; sending uses it once that address is saved.\"");
			//A wait cursor while the window reads the store (each key is opened) and the trust anchors.
			int indexFill=textWindow.IndexOf("private void FillInStore() {",StringComparison.Ordinal);
			string textFill=textWindow.Substring(indexFill,textWindow.IndexOf("private void butBrowse_Click(",StringComparison.Ordinal)-indexFill);
			Assert.IsTrue(Regex.IsMatch(textFill,@"Cursor=Cursors\.WaitCursor;\s*try \{\s*textInStore\.Text=GetInStoreText\(\);\s*\}\s*finally \{\s*Cursor=Cursors\.Default;"),
				"The cursor waits around the store and the trust check.");
			Assert.IsTrue(textFill.IndexOf("EmailMessages.IsSenderTrusted(_emailAddress)",StringComparison.Ordinal)>textFill.IndexOf("private string GetInStoreText()",StringComparison.Ordinal));
			//The file dialogs' filters are translated.
			StringAssert.Contains(textWindow,"openFileDialogCert.Filter=Lan.g(this,\"Certificate with private key\")+\" (*.pfx;*.p12)|*.pfx;*.p12\";");
			StringAssert.Contains(textWindow,"openFileDialogAnchor.Filter=Lan.g(this,\"Trust anchor certificate\")+\" (*.cer;*.crt;*.der;*.pem)|*.cer;*.crt;*.der;*.pem\";");
			int indexAnchor=textWindow.IndexOf("private void butImportAnchor_Click(",StringComparison.Ordinal);
			string textAnchor=textWindow.Substring(indexAnchor);
			Assert.IsTrue(textAnchor.IndexOf("MessageBoxButtons.YesNo",StringComparison.Ordinal)<textAnchor.IndexOf("DirectCertificateImport.ImportAnchorFile(",StringComparison.Ordinal),
				"The anchor is trusted only after a Yes.");
			StringAssert.Contains(textAnchor,"isAnchor:true");
			//A changed file or password turns Import off until Check runs again.
			StringAssert.Contains(textWindow,"private void textCertFile_TextChanged(object sender,EventArgs e) {\n\t\t\ttextDetails.Text=\"\";\n\t\t\tbutImport.Enabled=false;");
			string textDesigner=ReadNormalized(@"OpenPmsDental\Forms\FormEmailCertRegister.designer.cs");
			StringAssert.Contains(textDesigner,"this.textPassword.PasswordChar = '*';");
			StringAssert.Contains(textDesigner,"this.butImport.Enabled = false;");
			StringAssert.Contains(textDesigner,"this.textPassword.TextChanged += new System.EventHandler(this.textCertFile_TextChanged);");
			StringAssert.Contains(textDesigner,"this.textCertFilePath.TextChanged += new System.EventHandler(this.textCertFile_TextChanged);");
			Assert.IsFalse(textDesigner.Contains(".Filter = "),"The filters are set, translated, in the constructor.");
			StringAssert.Contains(textDesigner,"this.labelAddressNote.Text = \"The certificate is stored for this user name as typed in Edit Email Address. Sending from the "
				+"address uses it once Edit Email Address is saved with this user name.\";");
			StringAssert.Contains(textDesigner,"this.Controls.Add(this.labelAddressNote);");
			Assert.IsFalse(textDesigner.Contains("VerificationCode"));
			Assert.IsFalse(ReadNormalized(@"OpenPmsDental\Forms\FormEmailCertRegister.resx").Contains("register an email certificate"));
			string textAddress=ReadNormalized(@"OpenPmsDental\Forms\FormEmailAddressEdit.cs");
			StringAssert.Contains(textAddress,"new FormEmailCertRegister(textUsername.Text.Trim(),_emailAddress.EmailAddressNum)");
			//A user name that is not an e-mail address (a plain SMTP login) stops before the window opens, with a message that names the box where
			//it is fixed: the window shows the address read-only.
			Assert.IsTrue(Regex.IsMatch(textAddress,@"private void butRegisterCertificate_Click\(object sender,EventArgs e\) \{\s*"
				+@"if\(textUsername\.Text\.Trim\(\)==""""\) \{\s*MsgBox\.Show\(this,""Enter the email address first\.""\);\s*return;\s*\}\s*"
				+@"if\(DirectCertificateImport\.GetAddressError\(textUsername\.Text\.Trim\(\)\)!=""""\) \{[^}]*"
				+@"MsgBox\.Show\(this,""The Direct certificate is found by the user name\. Enter the Direct address \(name@domain\) in Username or email address, then ""\s*"
				+@"\+""click Certificate\.""\);\s*return;\s*\}\s*using FormEmailCertRegister formEmailCertRegister=new FormEmailCertRegister\("),
				"The user name is checked as an address before the window opens.");
			StringAssert.Contains(ReadNormalized(@"OpenPmsDental\Forms\FormEmailAddressEdit.designer.cs"),"this.label3.Text = \"Username or email address\";");
			Assert.IsTrue(ReadNormalized(@"OpenPmsDental\Forms\FormEmailCertRegister.designer.cs").Contains("this.textEmailAddress.ReadOnly = true;"),
				"Why the message names Edit Email Address's box.");
			//The business layer: never Exportable; the machine key set and the users' read access on the Local Computer only.
			string textBusiness=ReadNormalized(@"OpenPmsDentalBusiness\Email\DirectCertificateImport.cs");
			Assert.IsFalse(textBusiness.Contains("X509KeyStorageFlags.Exportable"),"No key is imported exportable.");
			//The question holds the new certificate's description, then the store's.
			int indexQuestion=textBusiness.IndexOf("public static string GetReplaceQuestion(DirectCertificateResult directCertificateResult) {",StringComparison.Ordinal);
			string textQuestion=textBusiness.Substring(indexQuestion,textBusiness.IndexOf("public static string GetReplaceWarning(",StringComparison.Ordinal)-indexQuestion);
			Assert.IsTrue(textQuestion.IndexOf("directCertificateResult.ListDetails.Select(x => x.DescribeShort())",StringComparison.Ordinal)>0,"The file's certificate.");
			Assert.IsTrue(textQuestion.IndexOf("directCertificateResult.ListDetailsReplaced.Select(x => x.DescribeShort())",StringComparison.Ordinal)
				>textQuestion.IndexOf("directCertificateResult.ListDetails.Select(x => x.DescribeShort())",StringComparison.Ordinal),"Then the store's.");
			Assert.IsFalse(textQuestion.ToLowerInvariant().Contains("older"));
			StringAssert.Contains(textBusiness,"if(EmailMessages.CertStoreLocation==StoreLocation.LocalMachine) {\n\t\t\t\tx509KeyStorageFlags|=X509KeyStorageFlags.MachineKeySet;");
			StringAssert.Contains(textBusiness,"x509Certificate2Collection.Import(byteArrayFile,password??\"\",X509KeyStorageFlags.EphemeralKeySet);");
			//The trust anchor's DER and PEM paths read with EphemeralKeySet: a key in the file is never written anywhere.
			StringAssert.Contains(textBusiness,"x509Certificate2Collection.Import(byteArrayFile,\"\",X509KeyStorageFlags.EphemeralKeySet);");
			StringAssert.Contains(textBusiness,"x509Certificate2Collection.Import(listPem[i],\"\",X509KeyStorageFlags.EphemeralKeySet);");
			//No import with the default key flags (third fix round, item 8): one argument, a password and no flags, or DefaultKeySet. The check
			//finds each of those in a sample, and none in the business layer's four imports.
			List<string> listImportsDefault=GetImportsWithDefaultKeyFlags(textBusiness,out int countImports);
			Assert.AreEqual(4,countImports,"The private file's check and import, the anchor's DER and PEM reads.");
			CollectionAssert.AreEqual(new List<string>(),listImportsDefault,"No import with the default key flags.");
			Assert.IsFalse(textBusiness.Contains("DefaultKeySet"));
			string textSamples="x509Certificate2Collection.Import(byteArrayFile);\nx509Certificate2Collection.Import(byteArrayFile,password??\"\");\n"
				+"x509Certificate2Collection.Import(byteArrayFile,\n\t\tpassword,X509KeyStorageFlags.DefaultKeySet);\n"
				+"x509Certificate2Collection.Import(byteArrayFile,password,X509KeyStorageFlags.DefaultKeySet|X509KeyStorageFlags.UserKeySet);\n"
				+"x509Certificate2Collection.Import(byteArrayFile,password,X509KeyStorageFlags.EphemeralKeySet);\n";
			Assert.AreEqual(4,GetImportsWithDefaultKeyFlags(textSamples,out int countSamples).Count,"Each of the four default-flag samples is found.");
			Assert.AreEqual(5,countSamples);
			//The key objects are disposed, and so are the certificates read from a store.
			Assert.IsFalse(Regex.IsMatch(textBusiness,@"^\s*(RSA rsa|ECDsa ecdsa)=x509Certificate2\.Get(RSA|ECDsa)PrivateKey\(\);",RegexOptions.Multiline),
				"Every key object of GetRSAPrivateKey or GetECDsaPrivateKey is in a using.");
			Assert.AreEqual(8,Regex.Matches(textBusiness,@"using\((RSA rsa|ECDsa ecdsa)=x509Certificate2\.Get(RSA|ECDsa)PrivateKey\(\)\)").Count,
				"IsKeyUsable, TryDeletePrivateKey, GetKeyContainerName and GetMachinePrivateKeyFilePath, RSA and ECDSA each.");
			Assert.AreEqual(4,Regex.Matches(textBusiness,@"using\(CngKey cngKey=\(\((RSACng|ECDsaCng)\)(rsa|ecdsa)\)\.Key\)").Count,
				"The CngKey of GetKeyContainerName and GetMachinePrivateKeyFilePath.");
			//A CAPI key first (.NET Framework's GetRSAPrivateKey gives it as an RSACng): deleted, named and found (the machine key's file, for the
			//users' read access, which needs administrator rights: launch check 1) through CAPI. The deletion and the name are tested in
			//ImportPrivateFile_KeepsTheKeyARenewalWithTheSameKeyShares.
			foreach(string method in new[] { "internal static string TryDeletePrivateKey(","internal static string GetKeyContainerName(",
				"private static string GetMachinePrivateKeyFilePath(" })
			{
				int indexMethod=textBusiness.IndexOf(method,StringComparison.Ordinal);
				int indexCapi=textBusiness.IndexOf("=GetCapiRsaPrivateKey(x509Certificate2);",indexMethod,StringComparison.Ordinal);
				Assert.IsTrue(indexCapi>indexMethod && indexCapi<textBusiness.IndexOf("x509Certificate2.GetRSAPrivateKey()",indexMethod,StringComparison.Ordinal),method);
			}
			StringAssert.Contains(textBusiness,"return Path.Combine(folderCrypto,\"RSA\",\"MachineKeys\",rsaCryptoServiceProviderCapi.CspKeyContainerInfo.UniqueKeyContainerName);");
			Assert.AreEqual(3,Regex.Matches(textBusiness,@"listStore=ReadStore\(").Count);
			Assert.AreEqual(3,Regex.Matches(textBusiness,@"DisposeAll\(listStore\);").Count,"Every certificate ReadStore returns is disposed.");
			Assert.IsTrue(Regex.IsMatch(textBusiness,@"if\(EmailMessages\.CertStoreLocation==StoreLocation\.LocalMachine\) \{\s*for\(int i=0;i<listAdding\.Count;i\+\+\) \{\s*try \{\s*GrantUsersReadToPrivateKey\(listAdding\[i\]\);"),
				"The users' read access only for the Local Computer's keys.");
		}

		///<summary>The production stores stay the Local Computer's NHIND stores (the tests replace them through the seam only).</summary>
		[TestMethod]
		public void Stores_AreTheLocalComputersNhindStoresInProduction() {
			Assert.AreEqual(StoreLocation.LocalMachine,EmailMessages.CertStoreLocation);
			Assert.AreEqual("NHINDPrivate",EmailMessages.CertStoreNamePrivate);
			Assert.AreEqual("NHINDAnchors",EmailMessages.CertStoreNameAnchor);
			CollectionAssert.AreEqual(new[] { ".pfx",".p12" },DirectCertificateImport.ArrayExtensionsPrivate);
			CollectionAssert.AreEqual(new[] { ".cer",".crt",".der",".pem" },DirectCertificateImport.ArrayExtensionsAnchor);
		}

		#region Helpers
		private static string ReadNormalized(string relativePath) {
			return File.ReadAllText(ScratchDb.FindRepoFile(relativePath)).Replace("\r\n","\n");
		}

		///<summary>The calls of Import(...) in the text (over lines too) that store keys with the default flags: one argument, two (a password and no
		///flags), or flags that name DefaultKeySet. countImports: every Import(...) call found.</summary>
		private static List<string> GetImportsWithDefaultKeyFlags(string text,out int countImports) {
			List<string> listImports=new List<string>();
			MatchCollection matchCollection=Regex.Matches(text,@"\.Import\(([^;]*)\);");
			countImports=matchCollection.Count;
			foreach(Match match in matchCollection) {
				string[] arrayArguments=match.Groups[1].Value.Split(',');
				if(arrayArguments.Length<3 || arrayArguments[2].Contains("DefaultKeySet")) {
					listImports.Add(match.Value);
				}
			}
			return listImports;
		}

		///<summary>The block of the question that starts with the label line, up to the next blank line (or the end); "" when it has none.</summary>
		private static string GetQuestionBlock(string question,string label) {
			int indexStart=question.IndexOf(label+"\r\n",StringComparison.Ordinal);
			if(indexStart<0) {
				return "";
			}
			int indexEnd=question.IndexOf("\r\n\r\n",indexStart,StringComparison.Ordinal);
			return (indexEnd<0) ? question.Substring(indexStart) : question.Substring(indexStart,indexEnd-indexStart);
		}

		private static void AssertRefused(DirectCertificateResult directCertificateResult,string expected) {
			Assert.IsFalse(directCertificateResult.IsImported,expected);
			StringAssert.Contains(directCertificateResult.Error,expected);
		}

		///<summary>The key can't be exported (its policy, its parameters, a PFX of the certificate) and still signs.</summary>
		private static void AssertKeyNotExportableButUsable(X509Certificate2 x509Certificate2) {
			using(RSA rsa=x509Certificate2.GetRSAPrivateKey()) {
				Assert.IsNotNull(rsa);
				if(rsa is RSACng) {
					Assert.AreEqual(CngExportPolicies.None,((RSACng)rsa).Key.ExportPolicy&(CngExportPolicies.AllowExport|CngExportPolicies.AllowPlaintextExport),
						"CNG key: no export allowed.");
				}
				else {
					Assert.IsInstanceOfType(rsa,typeof(RSACryptoServiceProvider));
					Assert.IsFalse(((RSACryptoServiceProvider)rsa).CspKeyContainerInfo.Exportable,"CAPI key: not exportable.");
				}
				Assert.IsTrue(Throws(() => rsa.ExportParameters(true)),"The private parameters can't be read.");
				byte[] byteArraySignature=rsa.SignData(Encoding.ASCII.GetBytes("Direct"),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
				using(RSA rsaPublic=x509Certificate2.GetRSAPublicKey()) {
					Assert.IsTrue(rsaPublic.VerifyData(Encoding.ASCII.GetBytes("Direct"),byteArraySignature,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1),
						"The key signs.");
				}
			}
			Assert.IsTrue(Throws(() => x509Certificate2.Export(X509ContentType.Pfx,"x")),"No PFX with the key can be made.");
		}

		private static bool Throws(Action action) {
			try {
				action();
			}
			catch(CryptographicException) {
				return true;
			}
			return false;
		}

		private static X509Certificate2 FindStored(string storeName,string thumbprint) {
			using(X509Store x509Store=new X509Store(storeName,StoreLocation.CurrentUser)) {
				x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
				X509Certificate2Collection x509Certificate2Collection=x509Store.Certificates.Find(X509FindType.FindByThumbprint,thumbprint,false);
				Assert.AreEqual(1,x509Certificate2Collection.Count,thumbprint);
				return x509Certificate2Collection[0];
			}
		}

		///<summary>Deletes the keys the import stored for the test (the certificates it marked); DirectTestStores then deletes the stores and the
		///keys of the certificates it made itself.</summary>
		private static void DeleteImportedKeys(DirectTestStores directTestStores) {
			using(X509Store x509Store=new X509Store(directTestStores.NamePrivate,StoreLocation.CurrentUser)) {
				try {
					x509Store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);
				}
				catch(CryptographicException) {
					return;
				}
				foreach(X509Certificate2 x509Certificate2 in x509Store.Certificates) {
					if(x509Certificate2.FriendlyName==DirectCertificateImport.FRIENDLY_NAME_IMPORTED) {
						Assert.AreEqual("",DirectCertificateImport.TryDeletePrivateKey(x509Certificate2),"The test's key is deleted.");
					}
				}
			}
		}

		///<summary>Where a stored private key is, to see whether it still exists: a CNG key or a CAPI key container, in the key set it lives in (the
		///tests' keys are the current user's).</summary>
		private class KeyLocation {
			private bool _isCng;
			private bool _isMachine;
			private string _name;
			private string _provider;
			private int _providerType;

			///<summary>The kind, provider and name of the key container: the same for two certificates that use one key.</summary>
			public string Name {
				get { return (_isCng ? "CNG|" : "CAPI|")+_provider+"|"+_name; }
			}

			public bool IsCng {
				get { return _isCng; }
			}

			public static KeyLocation Of(X509Certificate2 x509Certificate2) {
				KeyLocation keyLocation=new KeyLocation();
				//A CAPI key first: .NET Framework's GetRSAPrivateKey gives it as an RSACng (a .pfx without a CNG provider, as WritePfxSharedKey writes).
				RSACryptoServiceProvider rsaCryptoServiceProviderCapi=null;
				try {
					rsaCryptoServiceProviderCapi=x509Certificate2.PrivateKey as RSACryptoServiceProvider;
				}
				catch(CryptographicException) {
					//A CNG key.
				}
				if(rsaCryptoServiceProviderCapi!=null) {
					CspKeyContainerInfo cspKeyContainerInfoCapi=rsaCryptoServiceProviderCapi.CspKeyContainerInfo;
					keyLocation._name=cspKeyContainerInfoCapi.KeyContainerName;
					keyLocation._provider=cspKeyContainerInfoCapi.ProviderName;
					keyLocation._providerType=cspKeyContainerInfoCapi.ProviderType;
					keyLocation._isMachine=cspKeyContainerInfoCapi.MachineKeyStore;
					return keyLocation;
				}
				using(RSA rsa=x509Certificate2.GetRSAPrivateKey()) {
					if(rsa is RSACng) {
						keyLocation._isCng=true;
						keyLocation._name=((RSACng)rsa).Key.KeyName;
						keyLocation._provider=((RSACng)rsa).Key.Provider.Provider;
						keyLocation._isMachine=((RSACng)rsa).Key.IsMachineKey;
						return keyLocation;
					}
					CspKeyContainerInfo cspKeyContainerInfo=((RSACryptoServiceProvider)rsa).CspKeyContainerInfo;
					keyLocation._name=cspKeyContainerInfo.KeyContainerName;
					keyLocation._provider=cspKeyContainerInfo.ProviderName;
					keyLocation._providerType=cspKeyContainerInfo.ProviderType;
					keyLocation._isMachine=cspKeyContainerInfo.MachineKeyStore;
				}
				return keyLocation;
			}

			public bool Exists() {
				if(_isCng) {
					return CngKey.Exists(_name,new CngProvider(_provider),_isMachine ? CngKeyOpenOptions.MachineKey : CngKeyOpenOptions.None);
				}
				CspParameters cspParameters=new CspParameters(_providerType,_provider,_name);
				cspParameters.Flags=CspProviderFlags.UseExistingKey;
				if(_isMachine) {
					cspParameters.Flags|=CspProviderFlags.UseMachineKeyStore;
				}
				try {
					using(new RSACryptoServiceProvider(cspParameters)) {
					}
					return true;
				}
				catch(CryptographicException) {
					return false;
				}
			}
		}
		#endregion Helpers
	}

	///<summary>A test CA of a Direct provider, made at run time, and the certificates it issues, written as files to a temp folder of the test with a
	///password made at run time (in memory only). Dispose deletes the folder and the CA's key.</summary>
	public sealed class DirectTestPki:IDisposable {
		public readonly string Folder;
		public readonly string Password;
		private readonly X509Certificate2 _x509Certificate2Ca;
		private readonly List<Issued> _listIssued=new List<Issued>();
		private int _serial=1;
		///<summary>An OID under the IANA private enterprise arc that no one has: Windows has no friendly name for it.</summary>
		public const string PRIVATE_OID="1.3.6.1.4.1.99999.4242.1";
		///<summary>The e-mail protection purpose of the Extended Key Usage.</summary>
		public const string OID_EMAIL_PROTECTION="1.3.6.1.5.5.7.3.4";
		///<summary>The TLS client authentication purpose of the Extended Key Usage: not e-mail.</summary>
		public const string OID_CLIENT_AUTH="1.3.6.1.5.5.7.3.2";
		///<summary>anyExtendedKeyUsage.</summary>
		public const string OID_ANY_EKU="2.5.29.37.0";

		public string CaSubject { get { return _x509Certificate2Ca.Subject; } }
		public string CaThumbprint { get { return _x509Certificate2Ca.Thumbprint; } }
		public string CaSerialNumber { get { return _x509Certificate2Ca.SerialNumber; } }
		public byte[] CaRawData { get { return _x509Certificate2Ca.RawData; } }

		///<summary>A certificate the CA issued, as the provider's .pfx holds it: the certificate and its key, exportable only to write the test's files.</summary>
		public class Issued {
			public X509Certificate2 X509Certificate2WithKey;
			///<summary>The key, in memory only: for a certificate of the same key (Issue's issuedSameKey) and a .pfx written by hand (WritePfxSharedKey).</summary>
			public RSAParameters RsaParameters;
			public string Thumbprint { get { return X509Certificate2WithKey.Thumbprint; } }
			public string Subject { get { return X509Certificate2WithKey.Subject; } }
			public string SerialNumber { get { return X509Certificate2WithKey.SerialNumber; } }
			public DateTime DateTimeNotBefore { get { return X509Certificate2WithKey.NotBefore; } }
			public DateTime DateTimeNotAfter { get { return X509Certificate2WithKey.NotAfter; } }
			public byte[] RawData { get { return X509Certificate2WithKey.RawData; } }
		}

		public DirectTestPki() {
			Folder=Path.Combine(Path.GetTempPath(),"OpenPmsDirectCert_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Folder);
			byte[] byteArrayPassword=new byte[18];
			using(RandomNumberGenerator randomNumberGenerator=RandomNumberGenerator.Create()) {
				randomNumberGenerator.GetBytes(byteArrayPassword);
			}
			Password=Convert.ToBase64String(byteArrayPassword);
			using(RSA rsa=RSA.Create(2048)) {
				CertificateRequest certificateRequest=new CertificateRequest("CN=DIRECT-CERT Test HISP CA, O=Example Test HISP",rsa,HashAlgorithmName.SHA256,
					RSASignaturePadding.Pkcs1);
				certificateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
				certificateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign|X509KeyUsageFlags.CrlSign,true));
				certificateRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey,false));
				using(X509Certificate2 x509Certificate2=certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-60),DateTimeOffset.UtcNow.AddDays(60))) {
					//A key container of this process only (no PersistKeySet), removed by Reset.
					_x509Certificate2Ca=new X509Certificate2(x509Certificate2.Export(X509ContentType.Pfx,""),"",X509KeyStorageFlags.Exportable);
				}
			}
		}

		///<summary>An address certificate (E= and the rfc822 name), or with dnsName a domain certificate (the DNS name), issued by the CA, valid from
		///yesterday for 20 days unless the dates are given. isPrivateExtensionFirst: an extension of a private OID that Windows has no name for
		///comes before the subject alternative name. keyUsageFlags: the Key Usage, digitalSignature and keyEncipherment unless given (None: no
		///Key Usage extension); arrayEkuOids: the Extended Key Usage, e-mail protection unless given (empty: no Extended Key Usage extension);
		///issuedSameKey: the key of that certificate instead of a new one (a renewal with the same key).</summary>
		public Issued Issue(string emailAddress,string dnsName=null,DateTimeOffset? dateTimeOffsetNotBefore=null,DateTimeOffset? dateTimeOffsetNotAfter=null,
			bool isPrivateExtensionFirst=false,X509KeyUsageFlags keyUsageFlags=X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,
			string[] arrayEkuOids=null,Issued issuedSameKey=null)
		{
			using(RSA rsa=RSA.Create(2048)) {
				if(issuedSameKey!=null) {
					rsa.ImportParameters(issuedSameKey.RsaParameters);
				}
				string subject=(dnsName==null) ? "CN=DIRECT-CERT Test Address, E="+emailAddress : "CN="+dnsName;
				CertificateRequest certificateRequest=new CertificateRequest(subject,rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
				SubjectAlternativeNameBuilder subjectAlternativeNameBuilder=new SubjectAlternativeNameBuilder();
				if(dnsName==null) {
					subjectAlternativeNameBuilder.AddEmailAddress(emailAddress);
				}
				else {
					subjectAlternativeNameBuilder.AddDnsName(dnsName);
				}
				if(isPrivateExtensionFirst) {
					certificateRequest.CertificateExtensions.Add(new X509Extension(new Oid(PRIVATE_OID),new byte[] { 0x05,0x00 },false));//ASN.1 NULL
				}
				certificateRequest.CertificateExtensions.Add(subjectAlternativeNameBuilder.Build());
				certificateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
				if(keyUsageFlags!=X509KeyUsageFlags.None) {
					certificateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsageFlags,true));
				}
				OidCollection oidCollectionEku=new OidCollection();
				foreach(string ekuOid in arrayEkuOids??new[] { OID_EMAIL_PROTECTION }) {
					oidCollectionEku.Add(new Oid(ekuOid));
				}
				if(oidCollectionEku.Count>0) {
					certificateRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(oidCollectionEku,false));
				}
				byte[] byteArraySerial=BitConverter.GetBytes(_serial++);
				Array.Reverse(byteArraySerial);
				using(X509Certificate2 x509Certificate2=certificateRequest.Create(_x509Certificate2Ca,dateTimeOffsetNotBefore??DateTimeOffset.UtcNow.AddDays(-1),
					dateTimeOffsetNotAfter??DateTimeOffset.UtcNow.AddDays(20),byteArraySerial))
				using(X509Certificate2 x509Certificate2WithKey=x509Certificate2.CopyWithPrivateKey(rsa))
				{
					Issued issued=new Issued();
					issued.X509Certificate2WithKey=new X509Certificate2(x509Certificate2WithKey.Export(X509ContentType.Pfx,""),"",X509KeyStorageFlags.Exportable);
					issued.RsaParameters=rsa.ExportParameters(true);
					_listIssued.Add(issued);
					return issued;
				}
			}
		}

		///<summary>The provider's .pfx of the certificate, with the CA's certificate in it too (as providers often give the chain), or without the key.</summary>
		public byte[] ExportPfx(Issued issued,bool isCaIncluded,string password,bool isKeyIncluded=true) {
			X509Certificate2Collection x509Certificate2Collection=new X509Certificate2Collection();
			x509Certificate2Collection.Add(isKeyIncluded ? issued.X509Certificate2WithKey : new X509Certificate2(issued.RawData));
			if(isCaIncluded) {
				x509Certificate2Collection.Add(new X509Certificate2(_x509Certificate2Ca.RawData));
			}
			return x509Certificate2Collection.Export(X509ContentType.Pfx,password);
		}

		public string WritePfx(string fileName,Issued issued,bool isCaIncluded,bool isKeyIncluded=true) {
			return WriteFile(fileName,ExportPfx(issued,isCaIncluded,Password,isKeyIncluded));
		}

		///<summary>One .pfx with several certificates and their keys, as a provider that issues a signing and an encryption certificate may give it.</summary>
		public string WritePfxMany(string fileName,params Issued[] arrayIssued) {
			X509Certificate2Collection x509Certificate2Collection=new X509Certificate2Collection();
			foreach(Issued issued in arrayIssued) {
				x509Certificate2Collection.Add(issued.X509Certificate2WithKey);
			}
			return WriteFile(fileName,x509Certificate2Collection.Export(X509ContentType.Pfx,Password));
		}

		///<summary>One .pfx in which the certificates share one key (issued with Issue's issuedSameKey), written as OpenSSL writes a key with its
		///certificate and further certificates: one key bag, the first certificate named its certificate by a localKeyId, the others after it
		///without one. Windows' import (PersistKeySet) then gives the one key it stores to every certificate of the file with that public key, in
		///one key container; .NET's EphemeralKeySet read gives it to the first only. Built with BouncyCastle's ASN.1 types, as Windows' own export
		///writes one key bag for each certificate. keyProviderName: the key bag names that provider (Microsoft's attribute, as Windows writes it),
		///so the key is stored in it; null for none, as OpenSSL writes it, and .NET Framework's import then stores it in a CAPI container.</summary>
		public string WritePfxSharedKey(string fileName,string keyProviderName,params Issued[] arrayIssued) {
			char[] charArrayPassword=Password.ToCharArray();
			BcSecurity.SecureRandom secureRandom=new BcSecurity.SecureRandom();
			byte[] byteArraySaltKey=new byte[20];
			secureRandom.NextBytes(byteArraySaltKey);
			BcPkcs.EncryptedPrivateKeyInfo encryptedPrivateKeyInfo=Org.BouncyCastle.Pkcs.EncryptedPrivateKeyInfoFactory.CreateEncryptedPrivateKeyInfo(
				BcPkcs.PkcsObjectIdentifiers.PbeWithShaAnd3KeyTripleDesCbc,charArrayPassword,byteArraySaltKey,2048,
				BcSecurity.DotNetUtilities.GetRsaKeyPair(arrayIssued[0].RsaParameters).Private);
			BcPkcs.AttributePkcs attributePkcsLocalKeyId=new BcPkcs.AttributePkcs(BcPkcs.PkcsObjectIdentifiers.Pkcs9AtLocalKeyID,
				new Org.BouncyCastle.Asn1.DerSet(new Org.BouncyCastle.Asn1.DerOctetString(new byte[] { 0x44,0x43,0x45,0x52 })));
			Org.BouncyCastle.Asn1.Asn1Set asn1SetLocalKeyId=new Org.BouncyCastle.Asn1.DerSet(attributePkcsLocalKeyId);
			Org.BouncyCastle.Asn1.Asn1Set asn1SetKeyBag=asn1SetLocalKeyId;
			if(keyProviderName!=null) {
				//szOID_PKCS_12_KEY_PROVIDER_NAME_ATTR, a BMPString.
				asn1SetKeyBag=new Org.BouncyCastle.Asn1.DerSet(attributePkcsLocalKeyId,new BcPkcs.AttributePkcs(new Org.BouncyCastle.Asn1.DerObjectIdentifier("1.3.6.1.4.1.311.17.1"),
					new Org.BouncyCastle.Asn1.DerSet(new Org.BouncyCastle.Asn1.DerBmpString(keyProviderName))));
			}
			BcPkcs.SafeBag safeBagKey=new BcPkcs.SafeBag(BcPkcs.PkcsObjectIdentifiers.Pkcs8ShroudedKeyBag,encryptedPrivateKeyInfo.ToAsn1Object(),asn1SetKeyBag);
			Org.BouncyCastle.Asn1.Asn1EncodableVector asn1EncodableVectorCerts=new Org.BouncyCastle.Asn1.Asn1EncodableVector();
			for(int i=0;i<arrayIssued.Length;i++) {
				BcPkcs.CertBag certBag=new BcPkcs.CertBag(BcPkcs.PkcsObjectIdentifiers.X509Certificate,new Org.BouncyCastle.Asn1.DerOctetString(arrayIssued[i].RawData));
				if(i==0) {
					asn1EncodableVectorCerts.Add(new BcPkcs.SafeBag(BcPkcs.PkcsObjectIdentifiers.CertBag,certBag.ToAsn1Object(),asn1SetLocalKeyId));
				}
				else {
					asn1EncodableVectorCerts.Add(new BcPkcs.SafeBag(BcPkcs.PkcsObjectIdentifiers.CertBag,certBag.ToAsn1Object()));
				}
			}
			BcPkcs.ContentInfo contentInfoKey=new BcPkcs.ContentInfo(BcPkcs.PkcsObjectIdentifiers.Data,
				new Org.BouncyCastle.Asn1.DerOctetString(new Org.BouncyCastle.Asn1.DerSequence(safeBagKey).GetEncoded()));
			BcPkcs.ContentInfo contentInfoCerts=new BcPkcs.ContentInfo(BcPkcs.PkcsObjectIdentifiers.Data,
				new Org.BouncyCastle.Asn1.DerOctetString(new Org.BouncyCastle.Asn1.DerSequence(asn1EncodableVectorCerts).GetEncoded()));
			byte[] byteArrayAuthenticatedSafe=new BcPkcs.AuthenticatedSafe(new BcPkcs.ContentInfo[] { contentInfoKey,contentInfoCerts }).GetEncoded();
			//The PKCS #12 MAC over the content: HMAC-SHA1 with a key made from the password.
			byte[] byteArraySaltMac=new byte[20];
			secureRandom.NextBytes(byteArraySaltMac);
			Org.BouncyCastle.Asn1.DerObjectIdentifier derObjectIdentifierSha1=Org.BouncyCastle.Asn1.Oiw.OiwObjectIdentifiers.IdSha1;
			Org.BouncyCastle.Asn1.Asn1Encodable asn1EncodableMacParameters=BcSecurity.PbeUtilities.GenerateAlgorithmParameters(derObjectIdentifierSha1,byteArraySaltMac,2048);
			Org.BouncyCastle.Crypto.ICipherParameters cipherParameters=BcSecurity.PbeUtilities.GenerateCipherParameters(derObjectIdentifierSha1,charArrayPassword,false,
				asn1EncodableMacParameters);
			Org.BouncyCastle.Crypto.IMac mac=(Org.BouncyCastle.Crypto.IMac)BcSecurity.PbeUtilities.CreateEngine(derObjectIdentifierSha1);
			mac.Init(cipherParameters);
			byte[] byteArrayMac=BcSecurity.MacUtilities.DoFinal(mac,byteArrayAuthenticatedSafe);
			BcPkcs.MacData macData=new BcPkcs.MacData(new Org.BouncyCastle.Asn1.X509.DigestInfo(new Org.BouncyCastle.Asn1.X509.AlgorithmIdentifier(derObjectIdentifierSha1,
				Org.BouncyCastle.Asn1.DerNull.Instance),byteArrayMac),byteArraySaltMac,2048);
			BcPkcs.Pfx pfx=new BcPkcs.Pfx(new BcPkcs.ContentInfo(BcPkcs.PkcsObjectIdentifiers.Data,new Org.BouncyCastle.Asn1.DerOctetString(byteArrayAuthenticatedSafe)),macData);
			return WriteFile(fileName,pfx.GetEncoded(Org.BouncyCastle.Asn1.Asn1Encodable.Der));
		}

		public string WriteFile(string fileName,byte[] byteArray) {
			string path=Path.Combine(Folder,fileName);
			File.WriteAllBytes(path,byteArray);
			return path;
		}

		public static string ToPem(byte[] byteArrayRaw) {
			return "-----BEGIN CERTIFICATE-----\r\n"+Convert.ToBase64String(byteArrayRaw,Base64FormattingOptions.InsertLineBreaks)+"\r\n-----END CERTIFICATE-----\r\n";
		}

		public void Dispose() {
			foreach(Issued issued in _listIssued) {
				issued.X509Certificate2WithKey.Reset();//Removes the key container of the PFX round trip.
			}
			_x509Certificate2Ca.Reset();
			try {
				Directory.Delete(Folder,true);
			}
			catch(IOException) {
			}
			catch(UnauthorizedAccessException) {
			}
		}
	}
}
