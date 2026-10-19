// Tests for the PDMP package (Bamboo PMP Gateway, LogiCoy): fixture patients, providers, properties and test certificates.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using OpenDentBusiness;

namespace VendorDirectTests.Pdmp {
	///<summary>Made-up people and places. Nothing here is real data.</summary>
	public static class PdmpTestData {
		public const string PatientStreet="555 Patient Lane";
		public const string PatientLastName="Testpatient";
		public const string ClinicStreet="100 Clinic Way";
		public const string GatewayUrl="https://gateway.example.test/v5_1/patient";
		public const string ViewableReportUrl="https://gateway.example.test/v5_1/report/123";
		public const string ReportLink="https://report.example.test/view/abc";
		public const string LogicoyUrl="https://pmp.example.test/summary";
		public const string Username="office-user";
		public const string Password="Pässword-1";

		///<summary>Lans.g translates through the database cache unless the culture is en-US. Tests run in en-US.</summary>
		public static void UseEnUs() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
		}

		public static Patient MakePatient() {
			Patient patient=new Patient();
			patient.PatNum=1;
			patient.FName="Pat";
			patient.MiddleI="Q";
			patient.LName=PatientLastName;
			patient.Birthdate=new DateTime(1960,2,3);
			patient.Gender=PatientGender.Female;
			patient.Address=PatientStreet;
			patient.City="Patientville";
			patient.State="KS";
			patient.Zip="67203";
			patient.SSN="999001234";
			return patient;
		}

		public static Provider MakeProvider() {
			Provider provider=new Provider();
			provider.ProvNum=1;
			provider.FName="Doc";
			provider.LName="Testprovider";
			provider.NationalProvID="1234567893";
			return provider;
		}

		///<summary>A property set as SendData would build it after loading the rows, for state stateAbbr.</summary>
		public static PdmpProperty MakeProperty(string progName,string stateAbbr,string url) {
			PdmpProperty pdmpProperty=new PdmpProperty();
			pdmpProperty.ProgName=progName;
			pdmpProperty.PdmpPat=MakePatient();
			pdmpProperty.PdmpProv=MakeProvider();
			pdmpProperty.StateAbbr=stateAbbr;
			pdmpProperty.Dea="AB1234563";
			pdmpProperty.FacilityId="Main Office";
			pdmpProperty.ProvLicenseType=nameof(ProviderClinic.StateLicense);
			pdmpProperty.ProvLicenseNum="L-12345";
			pdmpProperty.Url=url;
			pdmpProperty.Username=Username;
			pdmpProperty.Password=Password;
			pdmpProperty.LocationStreet=ClinicStreet;
			pdmpProperty.LocationCity="Clinictown";
			pdmpProperty.LocationStateAbbr="KS";
			pdmpProperty.LocationZip="67000";
			return pdmpProperty;
		}

		///<summary>A self-signed PKCS#12 made at test time (never committed). ekuOid null means no extended key usage extension.</summary>
		public static byte[] MakePfx(string password,DateTimeOffset notBefore,DateTimeOffset notAfter,string ekuOid) {
			using(RSA rsa=RSA.Create(2048)) {
				CertificateRequest certificateRequest=new CertificateRequest("CN=PDMP Test Client",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
				if(ekuOid!=null) {
					certificateRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new System.Security.Cryptography.Oid(ekuOid) },false));
				}
				using(X509Certificate2 certificate=certificateRequest.CreateSelfSigned(notBefore,notAfter)) {
					return certificate.Export(X509ContentType.Pfx,password);
				}
			}
		}

		///<summary>A valid client-authentication certificate, one year either side of now.</summary>
		public static byte[] MakeClientPfx(string password) {
			return MakePfx(password,DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddYears(1),PdmpCertificate.OidClientAuthentication);
		}
	}
}
