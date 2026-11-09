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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

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
					string describe=directCertificateDetails.Describe();
					foreach(string part in new[] { issued.Subject,directTestPki.CaSubject,issued.SerialNumber,issued.Thumbprint,"For the address "+ADDRESS,"Private key: yes" }) {
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
		///program for it; an address certificate imported later does not replace it (a different kind), and the domain certificate replaces only
		///an older domain certificate.</summary>
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
					Assert.IsTrue(DirectCertificateImport.ImportPrivateFile(ADDRESS,pathAddress,directTestPki.Password,null,DateTime.UtcNow).IsImported);
					Assert.AreEqual(issuedAddress.Thumbprint,EmailMessages.GetCertFromPrivateStore(ADDRESS)?.Thumbprint,"The address's own certificate comes first.");
					Assert.AreEqual(issuedDomain.Thumbprint,EmailMessages.GetCertFromPrivateStore("someone@hisp.example.test")?.Thumbprint);
					Assert.AreEqual(2,DirectCertificateImport.GetPrivateCertificates(ADDRESS).Count);
					//A newer domain certificate replaces the domain certificate only.
					DirectTestPki.Issued issuedDomainNew=directTestPki.Issue(null,dnsName:DOMAIN);
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

		///<summary>An older certificate of the address is replaced only after the user agreed to replace it (the window's question): without the
		///agreement nothing is imported; with it the new certificate is added and the older ones removed. The key of the older one this import put
		///there is deleted with it; the key of one put there another way (certlm.msc) stays; another address's certificate is not touched.</summary>
		[TestMethod]
		public void ImportPrivateFile_ReplacesAnOlderCertificateOnlyAfterAsking() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				try {
					//The older certificate, imported here last year (in this test, a moment ago).
					DirectTestPki.Issued issuedOld=directTestPki.Issue(ADDRESS);
					Assert.IsTrue(DirectCertificateImport.ImportPrivateFile(ADDRESS,directTestPki.WritePfx("old.pfx",issuedOld,isCaIncluded:false),
						directTestPki.Password,null,DateTime.UtcNow).IsImported);
					KeyLocation keyLocationOld;
					using(X509Certificate2 x509Certificate2Old=FindStored(directTestStores.NamePrivate,issuedOld.Thumbprint)) {
						keyLocationOld=KeyLocation.Of(x509Certificate2Old);
					}
					Assert.IsTrue(keyLocationOld.Exists(),"The older certificate's key is stored.");
					//Another older one, put there another way (certlm.msc), and another address's certificate.
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
					StringAssert.Contains(question,"Replace the certificates on this computer with the one in the file?");
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
					StringAssert.Contains(log,"certificates of the store replaced: 2");
					Assert.IsFalse(log.Contains(ADDRESS) || log.Contains(DOMAIN) || log.Contains(directTestPki.Password),log);
				}
				finally {
					DeleteImportedKeys(directTestStores);
				}
			}
		}

		///<summary>The store's certificate is not compared by date and may be the newer one: the store holds the renewed certificate and last year's
		///file, still valid, is picked by mistake. The question calls neither older, lists both (the file's first), and warns before the Yes or No
		///that the store's is valid longer; the details warn the same way. A file valid longer than the store's gets no warning. Nothing is
		///replaced without the agreement, and nothing replaced at all needs no question.</summary>
		[TestMethod]
		public void CheckPrivateFile_TheQuestionShowsBothCertificatesAndWarnsWhenTheStoresIsValidLonger() {
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
					Assert.IsFalse(directCertificateResultRenewed.IsReplacedValidLonger());
					//Last year's file, still valid for 10 days.
					DirectTestPki.Issued issuedLastYear=directTestPki.Issue(ADDRESS,dateTimeOffsetNotBefore:DateTimeOffset.UtcNow.AddDays(-50),
						dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(10));
					string pathLastYear=directTestPki.WritePfx("last-year.pfx",issuedLastYear,isCaIncluded:false);
					DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathLastYear,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResultCheck.Error,"Still valid: it can be imported, after asking.");
					CollectionAssert.AreEqual(new List<string> { issuedRenewed.Thumbprint },directCertificateResultCheck.ListDetailsReplaced.Select(x => x.Thumbprint).ToList());
					Assert.IsTrue(directCertificateResultCheck.IsReplacedValidLonger());
					string warning=DirectCertificateImport.GetReplaceWarning(directCertificateResultCheck);
					Assert.AreEqual("The certificate on this computer is valid longer than the one in the file. Replace it only if your Direct provider told you to.",
						warning);
					string question=DirectCertificateImport.GetReplaceQuestion(directCertificateResultCheck);
					string describeFile=directCertificateResultCheck.ListDetails[0].DescribeShort();
					string describeStore=directCertificateResultCheck.ListDetailsReplaced[0].DescribeShort();
					foreach(string part in new[] { issuedLastYear.Subject,DirectCertificateImport.FormatDate(issuedLastYear.DateTimeNotAfter),issuedLastYear.Thumbprint }) {
						StringAssert.Contains(describeFile,part);
					}
					StringAssert.StartsWith(question,"This computer's Direct certificate store already has a certificate for this address or its domain.");
					Assert.IsFalse(question.ToLowerInvariant().Contains("older") || question.ToLowerInvariant().Contains("newer"),question);
					int indexFile=question.IndexOf("In the file:\r\n"+describeFile,StringComparison.Ordinal);
					int indexStore=question.IndexOf("On this computer now:\r\n"+describeStore,StringComparison.Ordinal);
					int indexWarning=question.IndexOf(warning,StringComparison.Ordinal);
					int indexAsk=question.IndexOf("Replace the certificate on this computer with the one in the file?",StringComparison.Ordinal);
					Assert.IsTrue(indexFile>0 && indexStore>indexFile && indexWarning>indexStore && indexAsk>indexWarning,question);
					StringAssert.Contains(question,"importing its .pfx or .p12 file again. No imports nothing.");
					//No agreement, nothing replaced: the renewed certificate stays.
					AssertRefused(DirectCertificateImport.ImportPrivateFile(ADDRESS,pathLastYear,directTestPki.Password,null,DateTime.UtcNow),"was not agreed to be replaced");
					CollectionAssert.AreEqual(new List<string> { issuedRenewed.Thumbprint },directTestStores.GetThumbprints(directTestStores.NamePrivate));
					Assert.AreEqual(issuedRenewed.Thumbprint,EmailMessages.GetCertFromPrivateStore(ADDRESS)?.Thumbprint);
					//A file valid longer than the store's: the question shows both, without the warning.
					DirectTestPki.Issued issuedLonger=directTestPki.Issue(ADDRESS,dateTimeOffsetNotBefore:DateTimeOffset.UtcNow.AddDays(-1),
						dateTimeOffsetNotAfter:DateTimeOffset.UtcNow.AddDays(55));
					DirectCertificateResult directCertificateResultLonger=DirectCertificateImport.CheckPrivateFile(ADDRESS,
						directTestPki.WritePfx("longer.pfx",issuedLonger,isCaIncluded:false),directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResultLonger.Error);
					Assert.IsFalse(directCertificateResultLonger.IsReplacedValidLonger());
					Assert.AreEqual("",DirectCertificateImport.GetReplaceWarning(directCertificateResultLonger));
					string questionLonger=DirectCertificateImport.GetReplaceQuestion(directCertificateResultLonger);
					StringAssert.Contains(questionLonger,"In the file:\r\n"+directCertificateResultLonger.ListDetails[0].DescribeShort());
					StringAssert.Contains(questionLonger,"On this computer now:\r\n"+directCertificateResultLonger.ListDetailsReplaced[0].DescribeShort());
					Assert.IsFalse(questionLonger.Contains(warning),questionLonger);
				}
				finally {
					DeleteImportedKeys(directTestStores);
				}
			}
		}

		///<summary>A copy of the certificate without its key in the private store (a .cer imported into the wrong store) is replaced by the file's
		///certificate with its key, after asking; the store then holds the one certificate, with its key.</summary>
		[TestMethod]
		public void ImportPrivateFile_ReplacesTheSameCertificateWithoutItsKey() {
			using(DirectTestPki directTestPki=new DirectTestPki())
			using(DirectTestStores directTestStores=new DirectTestStores()) {
				try {
					DirectTestPki.Issued issued=directTestPki.Issue(ADDRESS);
					using(X509Certificate2 x509Certificate2Public=new X509Certificate2(issued.RawData)) {
						directTestStores.AddPrivateWithoutKey(x509Certificate2Public);
					}
					Assert.IsNull(EmailMessages.GetCertFromPrivateStore(ADDRESS),"Without its key the program can't use it.");
					string pathPfx=directTestPki.WritePfx("pat.pfx",issued,isCaIncluded:false);
					DirectCertificateResult directCertificateResultCheck=DirectCertificateImport.CheckPrivateFile(ADDRESS,pathPfx,directTestPki.Password,DateTime.UtcNow);
					Assert.AreEqual("",directCertificateResultCheck.Error);
					Assert.AreEqual(1,directCertificateResultCheck.ListDetails.Count);
					Assert.AreEqual(1,directCertificateResultCheck.ListDetailsReplaced.Count);
					Assert.IsFalse(directCertificateResultCheck.ListDetailsReplaced[0].HasPrivateKey);
					StringAssert.Contains(directCertificateResultCheck.ListDetailsReplaced[0].DescribeShort(),"no private key");
					DirectCertificateResult directCertificateResult=DirectCertificateImport.ImportPrivateFile(ADDRESS,pathPfx,directTestPki.Password,
						new List<string> { issued.Thumbprint },DateTime.UtcNow);
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
			//The question is the business layer's, which lists the file's certificate (subject, valid to, thumbprint) and the store's, names no age, and
			//warns when the store's is valid longer (CheckPrivateFile_TheQuestionShowsBothCertificatesAndWarnsWhenTheStoresIsValidLonger); the
			//details box warns the same way.
			StringAssert.Contains(textImport,"string question=DirectCertificateImport.GetReplaceQuestion(directCertificateResultCheck);\n"
				+"\t\t\t\tif(MessageBox.Show(this,question,Text,MessageBoxButtons.YesNo)!=DialogResult.Yes) {");
			Assert.IsFalse(textWindow.ToLowerInvariant().Contains("older"),"The store's certificates are not compared by date.");
			int indexDescribeCheck=textWindow.IndexOf("private string DescribeCheck(",StringComparison.Ordinal);
			string textDescribeCheck=textWindow.Substring(indexDescribeCheck,textWindow.IndexOf("private void butImport_Click(",StringComparison.Ordinal)-indexDescribeCheck);
			StringAssert.Contains(textDescribeCheck,"DirectCertificateImport.GetReplaceWarning(directCertificateResult)");
			StringAssert.Contains(textImport,"SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,DirectCertificateImport.GetLogText(directCertificateResult,_emailAddressNum,isAnchor:false));");
			StringAssert.Contains(textImport,"textPassword.Text=\"\";");
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
			StringAssert.Contains(textDesigner,"|*.pfx;*.p12\";");
			StringAssert.Contains(textDesigner,"|*.cer;*.crt;*.der;*.pem\";");
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

		///<summary>Where a stored private key is, to see whether it still exists: a CNG key or a CAPI key container of the current user.</summary>
		private class KeyLocation {
			private bool _isCng;
			private string _name;
			private string _provider;
			private int _providerType;

			public static KeyLocation Of(X509Certificate2 x509Certificate2) {
				KeyLocation keyLocation=new KeyLocation();
				using(RSA rsa=x509Certificate2.GetRSAPrivateKey()) {
					if(rsa is RSACng) {
						keyLocation._isCng=true;
						keyLocation._name=((RSACng)rsa).Key.KeyName;
						keyLocation._provider=((RSACng)rsa).Key.Provider.Provider;
						return keyLocation;
					}
					CspKeyContainerInfo cspKeyContainerInfo=((RSACryptoServiceProvider)rsa).CspKeyContainerInfo;
					keyLocation._name=cspKeyContainerInfo.KeyContainerName;
					keyLocation._provider=cspKeyContainerInfo.ProviderName;
					keyLocation._providerType=cspKeyContainerInfo.ProviderType;
				}
				return keyLocation;
			}

			public bool Exists() {
				if(_isCng) {
					return CngKey.Exists(_name,new CngProvider(_provider),CngKeyOpenOptions.None);
				}
				CspParameters cspParameters=new CspParameters(_providerType,_provider,_name);
				cspParameters.Flags=CspProviderFlags.UseExistingKey;
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

		public string CaSubject { get { return _x509Certificate2Ca.Subject; } }
		public string CaThumbprint { get { return _x509Certificate2Ca.Thumbprint; } }
		public string CaSerialNumber { get { return _x509Certificate2Ca.SerialNumber; } }
		public byte[] CaRawData { get { return _x509Certificate2Ca.RawData; } }

		///<summary>A certificate the CA issued, as the provider's .pfx holds it: the certificate and its key, exportable only to write the test's files.</summary>
		public class Issued {
			public X509Certificate2 X509Certificate2WithKey;
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
		///comes before the subject alternative name.</summary>
		public Issued Issue(string emailAddress,string dnsName=null,DateTimeOffset? dateTimeOffsetNotBefore=null,DateTimeOffset? dateTimeOffsetNotAfter=null,
			bool isPrivateExtensionFirst=false)
		{
			using(RSA rsa=RSA.Create(2048)) {
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
				certificateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,true));
				certificateRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.4") },false));//email protection
				byte[] byteArraySerial=BitConverter.GetBytes(_serial++);
				Array.Reverse(byteArraySerial);
				using(X509Certificate2 x509Certificate2=certificateRequest.Create(_x509Certificate2Ca,dateTimeOffsetNotBefore??DateTimeOffset.UtcNow.AddDays(-1),
					dateTimeOffsetNotAfter??DateTimeOffset.UtcNow.AddDays(20),byteArraySerial))
				using(X509Certificate2 x509Certificate2WithKey=x509Certificate2.CopyWithPrivateKey(rsa))
				{
					Issued issued=new Issued();
					issued.X509Certificate2WithKey=new X509Certificate2(x509Certificate2WithKey.Export(X509ContentType.Pfx,""),"",X509KeyStorageFlags.Exportable);
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
