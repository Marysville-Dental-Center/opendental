using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace UnitTests {
	[TestClass]
	public class PdmpPropertyTest:TestBase {
		///<summary>Validate requires an absolute HTTPS URL on a host that isn't blocked (VendorUrlPolicy).</summary>
		private const string PMP_URL="https://pmp.example.test/test";

		///<summary>This method will execute only once, just before any tests in this class run.</summary>
		[ClassInitialize]
		public static void SetupClass(TestContext testContext) {
		
		}

		///<summary>This method will execute just before each test in this class.</summary>
		[TestInitialize]
		public void SetupTest() {

		}

		///<summary>This method will execute after each test in this class.</summary>
		[TestCleanup]
		public void TearDownTest() {
			
		}

		///<summary>This method will execute only once, just after all tests in this class have run.
		///However, this method is not guaranteed to execute before starting another TestMethod from another TestClass.</summary>
		[ClassCleanup]
		public static void TearDownClass() {

		}

		#region Database Value Tests
		[TestMethod]
		public void PDMP_TryValidateValues_Fail() {
			bool didThrow=false;
			OpenDentBusiness.Program progCur = Programs.GetCur(ProgramName.PDMP);
			long provNum = ProviderT.CreateProvider("invalid-doc");
			Provider prov = Providers.GetProv(provNum);
			Patient pat = null;
			string state = "MD";
			PdmpProperty properties = new PdmpProperty() {
				PdmpProv=prov,
				PdmpPat=pat,
				StateAbbr=state,
			};
			properties.PdmpPat=PatientT.CreatePatient();
			properties.PdmpProv.FName="Jerry";
			properties.PdmpProv.LName="Atric";
			try {
			properties.Validate(progCur);
			}
			catch (Exception ex) {
				ex.DoNothing();
				didThrow=true;
			}
			Assert.IsTrue(didThrow);
			didThrow=false;
			properties.ProvLicenseNum="A1-SteakSauce";
			try {
			properties.Validate(progCur);
			}
			catch (Exception ex) {
				ex.DoNothing();
				didThrow=true;
			}
			Assert.IsTrue(didThrow);
			didThrow=false;
			properties.PdmpPat.Birthdate=DateTime.Today;
			try {
			properties.Validate(progCur);
			}
			catch (Exception ex) {
				ex.DoNothing();
				didThrow=true;
			}
			Assert.IsTrue(didThrow);
			didThrow=false;
			properties.PdmpPat.Address="123 Tuhehl Rd";
			properties.PdmpPat.City="Rack City";
			properties.PdmpPat.State="MD";
			try {
			properties.Validate(progCur);
			}
			catch (Exception ex) {
				ex.DoNothing();
				didThrow=true;
			}
			Assert.IsTrue(didThrow);
			didThrow=false;
			properties.StateAbbr="KY";
			try {
			properties.Validate(progCur);
			}
			catch (Exception ex) {
				ex.DoNothing();
				didThrow=true;
			}
			Assert.IsTrue(didThrow);
			didThrow=false;
			properties.PdmpPat.SSN="555555555";
			properties.Dea="789456123";
			//Everything else is valid now. A bare host or a plain http URL is still refused.
			SetCredentials(properties,false);
			AssertUrlRefused(properties,progCur,"www.fakeurl.com/test");
			AssertUrlRefused(properties,progCur,"http://pmp.example.test/test");
			properties.Url=PMP_URL;
			try {
			properties.Validate(progCur);
			}
			catch (Exception ex) {
				ex.DoNothing();
				didThrow=true;
			}
			Assert.IsFalse(didThrow);
		}

		[TestMethod]
		public void PDMP_TryValidateValues_HappyPath() {
			OpenDentBusiness.Program progCur = Programs.GetCur(ProgramName.PDMP);
			string state = "MD";
			long provNum = ProviderT.CreateProvider("Valid-Prov",fName: "Jerry",lName: "Atric",nationalProvID: "B012");
			Provider prov = Providers.GetProv(provNum);
			Patient pat = new Patient() {
				Address="555 Haight St",
				City="San Ramon",
				State="MD",
				Birthdate=DateTime.Today,
			};
			PdmpProperty props = new PdmpProperty() {
				PdmpProv=prov,
				PdmpPat=pat,
				StateAbbr=state,
				ProvLicenseNum="A1-SteakSauce",
				Url=PMP_URL,
				Dea="123456789",
			};
			SetCredentials(props,false);
			try {
				props.Validate(progCur);
			}
			catch(Exception ex) {
				Assert.Fail(ex.Message);
			}
			AssertUrlRefused(props,progCur,"www.fakesite.com");
			AssertUrlRefused(props,progCur,"http://www.fakesite.com");
		}

		[TestMethod]
		public void Bamboo_TryValidateValues_Fail() {
			bool didThrow=false;
			OpenDentBusiness.Program progCur = Programs.GetCur(ProgramName.Bamboo);
			long provNum = ProviderT.CreateProvider("invalid-doc");
			Provider prov = Providers.GetProv(provNum);
			Patient pat = null;
			string state = "Ma";
			PdmpProperty properties = new PdmpProperty() {
				PdmpProv=prov,
				PdmpPat=pat,
				StateAbbr=state,
			};
			try {
			properties.Validate(progCur);
			}
			catch (Exception ex) {
				ex.DoNothing();
				didThrow=true;
			}
			Assert.IsTrue(didThrow);
			didThrow=false;
			properties.PdmpPat=PatientT.CreatePatient(lName: "Abbot",fName: "Hannah",zip: "43215");
			try {
			properties.Validate(progCur);
			}
			catch (Exception ex) {
				ex.DoNothing();
				didThrow=true;
			}
			Assert.IsTrue(didThrow);
			didThrow=false;;
			properties.PdmpPat.Birthdate=new DateTime(2001,1,1);
			try {
			properties.Validate(progCur);
			}
			catch (Exception ex) {
				ex.DoNothing();
				didThrow=true;
			}
			Assert.IsTrue(didThrow);
			didThrow=false;
			properties.Dea="123456789";
			properties.ProvLicenseNum="8675309";
			try {
			properties.Validate(progCur);
			}
			catch (Exception ex) {
				ex.DoNothing();
				didThrow=true;
			}
			Assert.IsTrue(didThrow);
			didThrow=false;
			properties.PdmpProv.FName="George";
			properties.PdmpProv.LName="Harris";
			try {
			properties.Validate(progCur);
			}
			catch (Exception ex) {
				ex.DoNothing();
				didThrow=true;
			}
			Assert.IsTrue(didThrow);
			didThrow=false;
			properties.Url=PMP_URL;
			try {
			properties.Validate(progCur);
			}
			catch (Exception ex) {
				ex.DoNothing();
				didThrow=true;
			}
			Assert.IsTrue(didThrow);
			didThrow=false;
			properties.StateAbbr="MA";
			SetCredentials(properties,true);
			try {
			properties.Validate(progCur);
			}
			catch (Exception ex) {
				ex.DoNothing();
				didThrow=true;
			}
			Assert.IsFalse(didThrow);
		}

		[TestMethod]
		public void Bamboo_TryValidateValues_HappyPath() {
			OpenDentBusiness.Program progCur = Programs.GetCur(ProgramName.Bamboo);
			string state = "MD";
			long provNum = ProviderT.CreateProvider("Valid-Prov",fName: "Jerry",lName: "Atric",nationalProvID: "B012");
			Provider prov = Providers.GetProv(provNum);
			Patient pat = new Patient() {
				Zip="12345",
				Birthdate=DateTime.Today,
			};
			PdmpProperty props = new PdmpProperty() {
				PdmpProv=prov,
				PdmpPat=pat,
				StateAbbr=state,
				ProvLicenseNum="A1-SteakSauce",
				Url=PMP_URL,
				Dea="123456789",
			};
			SetCredentials(props,true);
			bool didThrow=false;
			try {
			props.Validate(progCur);
			}
			catch (Exception ex) {
				ex.DoNothing();
				didThrow=true;
			}
			Assert.IsFalse(didThrow);
			AssertUrlRefused(props,progCur,"www.fakesite.com");
			AssertUrlRefused(props,progCur,"http://www.fakesite.com");
		}
		#endregion

		///<summary>What Setup holds once the office has entered its own account: a username and password, and for Bamboo an imported client
		///certificate. Validate checks these unless the database is in testing mode (IntrospectionItems). The certificate is a self-signed one made
		///here at test time, stored as PdmpCertificate.SaveImported stores it: the .pfx as base64 and its password.</summary>
		private static void SetCredentials(PdmpProperty properties,bool isBamboo) {
			properties.Username="office-user";
			properties.Password="office-password";
			if(!isBamboo) {
				return;
			}
			string password="pfx-password";
			using(RSA rsa=RSA.Create(2048)) {
				CertificateRequest certificateRequest=new CertificateRequest("CN=PDMP Unit Test Client",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
				certificateRequest.CertificateExtensions.Add(
					new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(PdmpCertificate.OidClientAuthentication) },false));
				using(X509Certificate2 certificate=certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddYears(1))) {
					properties.BambooClientKey=Convert.ToBase64String(certificate.Export(X509ContentType.Pfx,password));
				}
			}
			properties.BambooClientPassword=password;
		}

		///<summary>Sets the URL, asserts that Validate throws and names the URL, then puts the previous URL back.</summary>
		private static void AssertUrlRefused(PdmpProperty properties,OpenDentBusiness.Program progCur,string url) {
			string urlOld=properties.Url;
			properties.Url=url;
			try {
				properties.Validate(progCur);
				Assert.Fail("Validate accepted the URL "+url);
			}
			catch(ODException ex) {
				StringAssert.Contains(ex.Message,"Program url is not valid.");
			}
			finally {
				properties.Url=urlOld;
			}
		}

	}
}
