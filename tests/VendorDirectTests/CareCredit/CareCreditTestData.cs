// Shared fixtures for the CareCredit package's tests: a complete configuration, patients, and an in-memory carecreditwebresponse store.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using OpenDentBusiness;
using OpenDentBusiness.Bridges;

namespace VendorDirectTests.CareCreditTests {
	public static class CareCreditTestData {
		public const string API_BASE="https://api.carecredit-sandbox.test/v1";
		public const string IHS_BASE="https://pages.carecredit-sandbox.test/";
		public const string MERCHANT="5348120000000001";

		///<summary>A complete configuration against made-up .test hosts. The secret is stored as a real osd1$ value.</summary>
		public static CareCreditConfig CreateConfig() {
			CareCreditConfig config=new CareCreditConfig();
			config.ClientId="office-client-id";
			config.ClientSecret="office-client-secret";
			config.ClientSecretStored=SecretsTestHelper.Protect(config.ClientSecret);
			config.Environment=CareCreditEnvironment.Sandbox;
			config.ApiBaseUrl=API_BASE;
			config.IhsBaseUrl=IHS_BASE;
			config.TokenPath="/oauth2/token";
			config.PrefillPath="/patient-data/prefill";
			config.PullbackPath="/patient-data/pullback/{refId}";
			config.BatchPath="/patient-data/quickscreen-batch";
			config.SessionParam="sid";
			config.ClientAuth=CareCreditClientAuth.BasicHeader;
			config.Scope="";
			config.PartnerCode="OFFICE42";
			return config;
		}

		public static Patient CreateAdult(long patNum,string fName="Ada",string lName="Lovelace") {
			Patient patient=new Patient();
			patient.PatNum=patNum;
			patient.Guarantor=patNum;
			patient.FName=fName;
			patient.LName=lName;
			patient.Address="1 Main St";
			patient.City="Springfield";
			patient.State="OR";
			patient.Zip="97477";
			patient.HmPhone="(541) 555-0100";
			patient.Email="ada@example.test";
			patient.Birthdate=DateTime.Today.AddYears(-40);
			patient.SSN="123456789";
			return patient;
		}

		public static Patient CreateMinor(long patNum,long guarantorNum) {
			Patient patient=CreateAdult(patNum,"Kid","Lovelace");
			patient.Guarantor=guarantorNum;
			patient.Birthdate=DateTime.Today.AddYears(-10);
			return patient;
		}
	}

	///<summary>An in-memory carecreditwebresponse table.</summary>
	public sealed class FakeResponseStore : ICareCreditResponseStore {
		public readonly List<CareCreditWebResponse> ListRows=new List<CareCreditWebResponse>();
		private long _nextNum=1;

		public long Insert(CareCreditWebResponse careCreditWebResponse) {
			careCreditWebResponse.CareCreditWebResponseNum=_nextNum++;
			ListRows.Add(careCreditWebResponse.Copy());
			return careCreditWebResponse.CareCreditWebResponseNum;
		}

		public void Update(CareCreditWebResponse careCreditWebResponse) {
			int index=ListRows.FindIndex(x => x.CareCreditWebResponseNum==careCreditWebResponse.CareCreditWebResponseNum);
			ListRows[index]=careCreditWebResponse.Copy();
		}

		public bool Update(CareCreditWebResponse careCreditWebResponse,CareCreditWebResponse careCreditWebResponseOld) {
			Update(careCreditWebResponse);
			return true;
		}

		public CareCreditWebResponse Get(long num) {
			return ListRows.First(x => x.CareCreditWebResponseNum==num);
		}
	}
}
