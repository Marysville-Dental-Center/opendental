// Tests for the PDMP package: client certificate import checks. Certificates are generated at test time with CertificateRequest, never committed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace VendorDirectTests.Pdmp {
	[TestClass]
	public class PdmpCertificateTests {
		private const string PFX_PASSWORD="test-pfx-password";
		private const string OID_SERVER_AUTHENTICATION="1.3.6.1.5.5.7.3.1";

		[TestInitialize]
		public void TestInitialize() {
			PdmpTestData.UseEnUs();
		}

		[TestMethod]
		public void Import_AcceptsAClientAuthCertificateWithAKey() {
			byte[] arrayPfx=PdmpTestData.MakeClientPfx(PFX_PASSWORD);
			using(X509Certificate2 certificate=PdmpCertificate.Import(arrayPfx,PFX_PASSWORD,DateTime.UtcNow)) {
				Assert.IsTrue(certificate.HasPrivateKey);
				string description=PdmpCertificate.Describe(certificate);
				StringAssert.Contains(description,"PDMP Test Client");
				StringAssert.Contains(description,certificate.Thumbprint);
				Assert.IsFalse(PdmpCertificate.IsExpiringSoon(certificate,DateTime.UtcNow));
				Assert.AreEqual("",PdmpCertificate.GetExpiryWarning(certificate,DateTime.UtcNow));
				Assert.AreNotEqual("",PdmpCertificate.GetExpiryWarning(certificate,certificate.NotAfter.ToUniversalTime().AddDays(-10)),"Warns 30 days ahead.");
			}
		}

		[TestMethod]
		public void Import_AcceptsACertificateWithoutAnEku() {
			byte[] arrayPfx=PdmpTestData.MakePfx(PFX_PASSWORD,DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddYears(1),null);
			using(PdmpCertificate.Import(arrayPfx,PFX_PASSWORD,DateTime.UtcNow)) {
			}
		}

		[TestMethod]
		public void Import_AcceptsAFileWithoutAPassword() {
			//As openssl pkcs12 -export -passout pass: makes it. The import dialog passes the blank password box as "".
			byte[] arrayPfx=PdmpTestData.MakeClientPfx("");
			using(X509Certificate2 certificate=PdmpCertificate.Import(arrayPfx,"",DateTime.UtcNow)) {
				Assert.IsTrue(certificate.HasPrivateKey);
			}
			using(PdmpCertificate.Import(arrayPfx,null,DateTime.UtcNow)) {
			}
			PdmpProperty pdmpProperty=new PdmpProperty();
			pdmpProperty.BambooClientKey=Convert.ToBase64String(arrayPfx);
			pdmpProperty.BambooClientPassword="";
			Assert.AreEqual("",PdmpProperty.GetCertificateProblem(pdmpProperty,DateTime.UtcNow));
			using(X509Certificate2 certificate=PdmpCertificate.Load(pdmpProperty,DateTime.UtcNow)) {
				Assert.IsTrue(certificate.HasPrivateKey);
			}
			//A file that has a password, stored without one, is refused the same way by import and by validation.
			pdmpProperty.BambooClientKey=Convert.ToBase64String(PdmpTestData.MakeClientPfx(PFX_PASSWORD));
			StringAssert.Contains(PdmpProperty.GetCertificateProblem(pdmpProperty,DateTime.UtcNow),"Check the password");
		}

		[TestMethod]
		public void Import_RejectsAWrongPassword() {
			byte[] arrayPfx=PdmpTestData.MakeClientPfx(PFX_PASSWORD);
			PdmpException ex=AssertEx.Throws<PdmpException>(() => PdmpCertificate.Import(arrayPfx,"wrong",DateTime.UtcNow));
			StringAssert.Contains(ex.Message,"password");
			Assert.IsNull(ex.InnerException);
		}

		[TestMethod]
		public void Import_RejectsAnExpiredCertificate() {
			byte[] arrayPfx=PdmpTestData.MakePfx(PFX_PASSWORD,DateTimeOffset.UtcNow.AddYears(-2),DateTimeOffset.UtcNow.AddYears(-1),
				PdmpCertificate.OidClientAuthentication);
			PdmpException ex=AssertEx.Throws<PdmpException>(() => PdmpCertificate.Import(arrayPfx,PFX_PASSWORD,DateTime.UtcNow));
			StringAssert.Contains(ex.Message,"expired");
		}

		[TestMethod]
		public void Import_RejectsAServerOnlyCertificate() {
			byte[] arrayPfx=PdmpTestData.MakePfx(PFX_PASSWORD,DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddYears(1),OID_SERVER_AUTHENTICATION);
			PdmpException ex=AssertEx.Throws<PdmpException>(() => PdmpCertificate.Import(arrayPfx,PFX_PASSWORD,DateTime.UtcNow));
			StringAssert.Contains(ex.Message,"client authentication");
		}

		[TestMethod]
		public void Import_RejectsACertificateWithoutAPrivateKey() {
			byte[] arrayPfx=PdmpTestData.MakeClientPfx(PFX_PASSWORD);
			byte[] arrayCertOnly;
			using(X509Certificate2 certificate=new X509Certificate2(arrayPfx,PFX_PASSWORD,X509KeyStorageFlags.EphemeralKeySet)) {
				arrayCertOnly=certificate.Export(X509ContentType.Cert);
			}
			PdmpException ex=AssertEx.Throws<PdmpException>(() => PdmpCertificate.Import(arrayCertOnly,"",DateTime.UtcNow));
			StringAssert.Contains(ex.Message,"private key");
			AssertEx.Throws<PdmpException>(() => PdmpCertificate.Import(new byte[0],"",DateTime.UtcNow));
		}

		[TestMethod]
		public void Load_UsesTheImportedFileAndRefusesNothingSet() {
			byte[] arrayPfx=PdmpTestData.MakeClientPfx(PFX_PASSWORD);
			PdmpProperty pdmpProperty=new PdmpProperty();
			Assert.AreEqual(PdmpCertificateSource.None,PdmpCertificate.GetSource(pdmpProperty));
			PdmpException ex=AssertEx.Throws<PdmpException>(() => PdmpCertificate.Load(pdmpProperty,DateTime.UtcNow));
			StringAssert.Contains(ex.Message,PdmpCertificate.MessageNotSet);
			pdmpProperty.BambooClientKey=Convert.ToBase64String(arrayPfx);
			pdmpProperty.BambooClientPassword=PFX_PASSWORD;
			Assert.AreEqual(PdmpCertificateSource.Database,PdmpCertificate.GetSource(pdmpProperty));
			using(X509Certificate2 certificate=PdmpCertificate.Load(pdmpProperty,DateTime.UtcNow)) {
				Assert.IsTrue(certificate.HasPrivateKey);
			}
			Assert.AreEqual("",PdmpProperty.GetCertificateProblem(pdmpProperty,DateTime.UtcNow));
			pdmpProperty.ClientCertThumbprint="00";
			Assert.AreEqual(PdmpCertificateSource.Store,PdmpCertificate.GetSource(pdmpProperty),"A thumbprint wins over the file.");
		}

		[TestMethod]
		public void NormalizeThumbprint_DropsSpacesAndHiddenMarks() {
			Assert.AreEqual("AB12CD",PdmpCertificate.NormalizeThumbprint("‎ab 12 cd"));
			Assert.AreEqual("",PdmpCertificate.NormalizeThumbprint(null));
		}
	}
}
