// Tests for the CareCredit settings rules, the JSON reader and the status map (docs/specs/vendor-direct/carecredit.md section 6). No network,
// no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.Bridges;

namespace VendorDirectTests.CareCreditTests {
	[TestClass]
	public class CareCreditConfigTests {
		[TestMethod]
		public void Config_CompleteHasNoProblems() {
			CareCreditConfig config=CareCreditTestData.CreateConfig();
			Assert.AreEqual(0,config.GetProblems().Count,string.Join("; ",config.GetProblems()));
			Assert.IsTrue(config.IsComplete);
		}

		[TestMethod]
		public void Config_PartnerCode1065IsRefused() {
			StringAssert.Contains(CareCreditConfig.GetPartnerCodeProblem("1065"),"different software vendor");
			StringAssert.Contains(CareCreditConfig.GetPartnerCodeProblem(" 1065 "),"different software vendor");
			Assert.AreNotEqual("",CareCreditConfig.GetPartnerCodeProblem(""));
			Assert.AreEqual("",CareCreditConfig.GetPartnerCodeProblem("OFFICE42"));
			CareCreditConfig config=CareCreditTestData.CreateConfig();
			config.PartnerCode="1065";
			Assert.IsFalse(config.IsComplete);
		}

		[TestMethod]
		public void Config_UrlsMustBeHttpsAndNeverAnOpenDentalHost() {
			string blockedDomain=HqHosts.BlockedDomains.First();
			Assert.AreNotEqual("",CareCreditConfig.GetUrlProblem("http://api.carecredit-sandbox.test/"),"Plain http is refused.");
			Assert.AreNotEqual("",CareCreditConfig.GetUrlProblem("https://api."+blockedDomain+"/carecredit"),"An Open Dental host is refused.");
			Assert.AreNotEqual("",CareCreditConfig.GetUrlProblem("api.carecredit-sandbox.test"),"A relative address is refused.");
			Assert.AreEqual("",CareCreditConfig.GetUrlProblem(CareCreditTestData.API_BASE));
			CareCreditConfig config=CareCreditTestData.CreateConfig();
			config.IhsBaseUrl="https://"+blockedDomain+"/pages";
			Assert.IsFalse(config.IsComplete);
		}

		[TestMethod]
		public void Config_PathsAreRelativeOnly() {
			Assert.AreEqual("",CareCreditConfig.GetPathProblem("/v1/token",isRequired:true));
			Assert.AreEqual("",CareCreditConfig.GetPathProblem("",isRequired:false));
			Assert.AreNotEqual("",CareCreditConfig.GetPathProblem("",isRequired:true));
			Assert.AreNotEqual("",CareCreditConfig.GetPathProblem("https://elsewhere.test/token",isRequired:true));
			Assert.AreNotEqual("",CareCreditConfig.GetPathProblem("//elsewhere.test/token",isRequired:true));
			Assert.AreNotEqual("",CareCreditConfig.GetPathProblem("/token?x=1",isRequired:true));
		}

		[TestMethod]
		public void Config_EndpointStaysOnTheApiHost() {
			CareCreditConfig config=CareCreditTestData.CreateConfig();
			Assert.AreEqual("https://api.carecredit-sandbox.test/v1/oauth2/token",config.GetEndpoint("/oauth2/token").AbsoluteUri);
			Assert.AreEqual("https://api.carecredit-sandbox.test/v1/oauth2/token",config.GetEndpoint("oauth2/token").AbsoluteUri);
			AssertEx.Throws<ArgumentException>(() => config.GetEndpoint("//other.test/x"));
		}

		[TestMethod]
		public void Config_HostedPagesOnlyOnTheIhsHost() {
			CareCreditConfig config=CareCreditTestData.CreateConfig();
			Assert.IsTrue(config.IsHostedPageUrl("https://pages.carecredit-sandbox.test/apply?sid=abc"));
			Assert.IsFalse(config.IsHostedPageUrl("https://evil.test/apply?sid=abc"));
			Assert.IsFalse(config.IsHostedPageUrl("http://pages.carecredit-sandbox.test/apply"));
			Assert.IsFalse(config.IsHostedPageUrl(""));
		}

		[TestMethod]
		public void Config_VerificationFingerprintChangesWithAnySetting() {
			CareCreditConfig config=CareCreditTestData.CreateConfig();
			string fingerprint=config.GetVerificationFingerprint();
			config.VerifiedFingerprint=fingerprint;
			Assert.IsTrue(config.IsVerified);
			config.PrefillPath="/other";
			Assert.AreNotEqual(fingerprint,config.GetVerificationFingerprint());
			Assert.IsFalse(config.IsVerified);
			Assert.IsFalse(fingerprint.Contains("office-client-secret"),"The plain secret never goes into a fingerprint.");
		}

		[TestMethod]
		public void Config_UnreadableSecretNeedsReentry() {
			CareCreditConfig config=CareCreditTestData.CreateConfig();
			config.ClientSecret="";
			config.IsClientSecretUnreadable=true;
			Assert.IsTrue(config.GetProblems().Any(x => x.Contains("can't be read")));
		}

		[TestMethod]
		public void Settings_TimeOfDayParsing() {
			Assert.IsTrue(CareCreditSettings.TryParseTimeOfDay("07:20",out TimeSpan time));
			Assert.AreEqual(new TimeSpan(7,20,0),time);
			Assert.IsTrue(CareCreditSettings.TryParseTimeOfDay("7:05",out time));
			Assert.AreEqual(new TimeSpan(7,5,0),time);
			Assert.IsFalse(CareCreditSettings.TryParseTimeOfDay("later",out time));
			Assert.AreEqual(new TimeSpan(0,0,0),CareCreditSettings.GetPrefValueForTime(TimeSpan.Zero).TimeOfDay);
			Assert.IsTrue(CareCreditSettings.GetPrefValueForTime(TimeSpan.Zero).Year>1880,"Midnight must not look like 'never set'.");
		}

		[TestMethod]
		public void Enums_KeepTheValuesCallersStore() {
			Assert.AreEqual(0,(int)EnumCareCreditBatch.Off);
			Assert.AreEqual(1,(int)EnumCareCreditBatch.On);
			CollectionAssert.AreEqual(new[] { "HM","AP","LU","PR","QS","RF" },Enum.GetNames(typeof(CareCreditLandingPage)));
			CollectionAssert.AreEqual(new[] { "A","C","L","R" },Enum.GetNames(typeof(EnumCareCreditPrefillServiceType)));
		}
	}

	[TestClass]
	public class CareCreditJsonTests {
		[TestMethod]
		public void StatusMap_KnownNamesAndSynonyms() {
			Assert.AreEqual(CareCreditWebStatus.Completed,CareCreditStatusMap.Map("COMPLETED",false));
			Assert.AreEqual(CareCreditWebStatus.Completed,CareCreditStatusMap.Map("approved",false));
			Assert.AreEqual(CareCreditWebStatus.PreApproved,CareCreditStatusMap.Map("approved",true));
			Assert.AreEqual(CareCreditWebStatus.PreApproved,CareCreditStatusMap.Map("Pre-Approved",true));
			Assert.AreEqual(CareCreditWebStatus.Declined,CareCreditStatusMap.Map("DECLINED",true));
			Assert.AreEqual(CareCreditWebStatus.AccountFound,CareCreditStatusMap.Map("Cardholder",true));
			Assert.AreEqual(CareCreditWebStatus.AccountNotFoundQS,CareCreditStatusMap.Map("ACCOUNT_NOT_FOUND",true));
			Assert.AreEqual(CareCreditWebStatus.Pending,CareCreditStatusMap.Map("in progress",false));
			Assert.AreEqual(CareCreditWebStatus.Cancelled,CareCreditStatusMap.Map("Canceled",false));
			Assert.AreEqual(CareCreditWebStatus.CallForAuth,CareCreditStatusMap.Map("Call For Auth",false));
			Assert.AreEqual(CareCreditWebStatus.Unknown,CareCreditStatusMap.Map("SOMETHING_NEW",false));
			Assert.AreEqual(CareCreditWebStatus.Unknown,CareCreditStatusMap.Map("",false));
		}

		[TestMethod]
		public void ReadPullback_FindsNestedFieldsWithoutCaseOrSeparators() {
			string json="{\"data\":{\"transaction\":{\"Transaction_Type\":\"PURCHASE\",\"status\":\"COMPLETED\",\"amount\":\"125.50\","
				+"\"merchantFee\":-9.75,\"promoCode\":\"PROMO6\",\"referenceId\":\"R-1\",\"userName\":\"frontdesk\"}}}";
			CareCreditPullbackResult result=CareCreditJson.ReadPullback(json,isQuickScreen:false);
			Assert.AreEqual(CareCreditWebStatus.Completed,result.Status);
			Assert.AreEqual(CareCreditTransType.Purchase,result.TransType);
			Assert.AreEqual(125.50,result.Amount,0.001);
			Assert.AreEqual(9.75,result.MerchantFee,0.001,"The fee is kept positive.");
			Assert.AreEqual("PROMO6",result.PromoCode);
			Assert.AreEqual("R-1",result.ReferenceId);
			Assert.AreEqual("frontdesk",result.UserName);
		}

		[TestMethod]
		public void Scrub_RemovesSsnAndBirthDateAtAnyDepth() {
			string json="{\"consumer\":{\"firstName\":\"Ada\",\"ssn\":\"123456789\",\"date_of_birth\":\"1980-01-01\",\"items\":[{\"SSNLast4\":\"6789\"}]},"
				+"\"DOB\":\"1980-01-01\",\"status\":\"PENDING\"}";
			string scrubbed=CareCreditJson.Scrub(json);
			Assert.IsFalse(scrubbed.Contains("123456789"));
			Assert.IsFalse(scrubbed.Contains("1980-01-01"));
			Assert.IsFalse(scrubbed.Contains("6789"));
			StringAssert.Contains(scrubbed,"Ada");
			StringAssert.Contains(scrubbed,"PENDING");
		}

		[TestMethod]
		public void BatchResponse_ReadsNewAndMigratedShapes() {
			CareCreditWebResponse rowLocal=new CareCreditWebResponse();
			rowLocal.LastResponseStr=CareCreditQSBatchResponse.ToLocalErrorJson("Missing ZIP.",isInvalidInput:true);
			CareCreditQSBatchResponse responseLocal=CareCreditQSBatchResponse.From(rowLocal);
			Assert.IsTrue(responseLocal.IsInvalidInput);
			Assert.AreEqual("Missing ZIP.",responseLocal.ResponseDesc);
			//A sanitized row in the shape a serialized object with PascalCase properties would have.
			CareCreditWebResponse rowMigrated=new CareCreditWebResponse();
			rowMigrated.LastResponseStr="{\"IsInvalidInput\":false,\"ResponseDesc\":\"Merchant Closed\",\"RefNumber\":\"\"}";
			CareCreditQSBatchResponse responseMigrated=CareCreditQSBatchResponse.From(rowMigrated);
			Assert.IsFalse(responseMigrated.IsInvalidInput);
			Assert.AreEqual("Merchant Closed",responseMigrated.ResponseDesc);
			Assert.IsTrue(responseMigrated.IsMerchantClosed);
			CareCreditWebResponse rowText=new CareCreditWebResponse();
			rowText.LastResponseStr="Network error: timeout";
			Assert.IsNull(CareCreditQSBatchResponse.From(rowText));
			Assert.IsNull(CareCredit.GetBatchResponse(null));
		}
	}
}
