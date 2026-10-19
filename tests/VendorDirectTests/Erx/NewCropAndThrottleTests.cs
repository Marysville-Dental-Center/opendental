// Tests for the ERX package that need no database: NewCrop settings and account IDs, credential redaction, the poll throttle.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.Erx {
	[TestClass]
	public class NewCropAndThrottleTests {
		[TestInitialize]
		public void TestInitialize() {
			ErxTestData.UseEnUs();
		}

		private static NewCropConfig MakeConfig() {
			NewCropConfig newCropConfig=new NewCropConfig();
			newCropConfig.PartnerName="PartnerX";
			newCropConfig.AccountName="AccountY";
			newCropConfig.Password="S3cr3t&<pw>";
			newCropConfig.PasswordStatus=SecretReadStatus.Ok;
			newCropConfig.AccountId="OPMS-ABCDEFGHJK";
			newCropConfig.SiteId="1";
			newCropConfig.ProductName="Product Z";
			return newCropConfig;
		}

		[TestMethod]
		public void GenerateAccountId_IsPrefixedRandomAndUnique() {
			HashSet<string> hashSetIds=new HashSet<string>();
			for(int i=0;i<200;i++) {
				string accountId=NewCropConfig.GenerateAccountId();
				Assert.IsTrue(Regex.IsMatch(accountId,"^"+NewCropConfig.AccountIdPrefix+"-[A-Z0-9]{"+NewCropConfig.AccountIdRandomLength+"}$"),accountId);
				Assert.IsTrue(hashSetIds.Add(accountId));
			}
		}

		[TestMethod]
		public void IsConfigured_NeedsEveryValueAndAReadablePassword() {
			Assert.IsTrue(MakeConfig().IsConfigured());
			NewCropConfig newCropConfig=MakeConfig();
			newCropConfig.Password="";
			newCropConfig.PasswordStatus=SecretReadStatus.Unreadable;
			Assert.IsFalse(newCropConfig.IsConfigured());
			StringAssert.Contains(newCropConfig.GetNotConfiguredMessage(),"can't be read");
			newCropConfig=MakeConfig();
			newCropConfig.AccountId=" ";
			Assert.IsFalse(newCropConfig.IsConfigured());
			StringAssert.Contains(newCropConfig.GetNotConfiguredMessage(),"account ID");
			Assert.IsFalse(new NewCropConfig().IsConfigured(),"A fresh configuration is not configured.");
		}

		[TestMethod]
		public void IsConfigured_FalseWhenTheStoredPasswordIsCorrupt() {
			string stored=SecretsTestHelper.Protect("pw");
			string corrupted=stored.Substring(0,stored.Length-4)+(stored.EndsWith("AAAA") ? "BBBB" : "AAAA");
			SecretReadResult secretReadResult=SecretValue.Read(corrupted,NewCropConfig.PasswordLegacyPolicy);
			NewCropConfig newCropConfig=MakeConfig();
			newCropConfig.Password=secretReadResult.PlainText;
			newCropConfig.PasswordStatus=secretReadResult.Status;
			Assert.AreEqual(SecretReadStatus.Unreadable,secretReadResult.Status);
			Assert.AreEqual("",newCropConfig.Password,"A corrupted osd1$ value is never handed back as the password.");
			Assert.IsFalse(newCropConfig.IsConfigured());
		}

		[TestMethod]
		public void ParseEnvironment_DefaultsToProduction() {
			Assert.AreEqual(NewCropEnvironment.Preproduction,NewCropConfig.ParseEnvironment(" preproduction "));
			Assert.AreEqual(NewCropEnvironment.Production,NewCropConfig.ParseEnvironment(""));
			Assert.AreEqual(NewCropEnvironment.Production,NewCropConfig.ParseEnvironment("Debug"));
			Assert.AreEqual(DoseSpotEnvironment.Staging,DoseSpotConfig.ParseEnvironment("STAGING"));
			Assert.AreEqual(DoseSpotEnvironment.Production,DoseSpotConfig.ParseEnvironment(null));
		}

		[TestMethod]
		public void SoapCredentials_ComeFromTheConfig() {
			NewCropConfig newCropConfig=MakeConfig();
			OpenDentBusiness.NewCropWebService.Credentials credentials=newCropConfig.GetSoapCredentials();
			Assert.AreEqual("PartnerX",credentials.PartnerName);
			Assert.AreEqual("AccountY",credentials.Name);
			Assert.AreEqual("S3cr3t&<pw>",credentials.Password);
			OpenDentBusiness.NewCropWebService.AccountRequest accountRequest=newCropConfig.GetSoapAccountRequest();
			Assert.AreEqual("OPMS-ABCDEFGHJK",accountRequest.AccountId);
			Assert.AreEqual("1",accountRequest.SiteId);
		}

		[TestMethod]
		public void RedactNewCropCredentials_LeavesNoCredentials() {
			NCScript ncScript=new NCScript();
			ncScript.Credentials=new CredentialsType();
			ncScript.Credentials.partnerName="PartnerX";
			ncScript.Credentials.name="AccountY";
			ncScript.Credentials.password="S3cr3t&<pw>";
			ncScript.Credentials.productName="Product Z";
			ncScript.Credentials.productVersion="1.2";
			System.Xml.Serialization.XmlSerializer xmlSerializer=new System.Xml.Serialization.XmlSerializer(typeof(NCScript));
			string xml;
			using(System.IO.StringWriter stringWriter=new System.IO.StringWriter()) {
				xmlSerializer.Serialize(stringWriter,ncScript);
				xml=stringWriter.ToString();
			}
			StringAssert.Contains(xml,"S3cr3t");
			string redacted=ErxXml.RedactNewCropCredentials(xml);
			Assert.IsFalse(redacted.Contains("S3cr3t"));
			Assert.IsFalse(redacted.Contains("PartnerX"));
			Assert.IsFalse(redacted.Contains("AccountY"));
			StringAssert.Contains(redacted,"Product Z");
			Assert.AreEqual(redacted,OpenDentBusiness.Erx.RedactForLog(xml,ErxOption.NewCrop));
			//Text that isn't well-formed XML still loses its password.
			string broken="<NCScript><Credentials><password>S3cr3t</password><name>x</name>";
			Assert.IsFalse(ErxXml.RedactNewCropCredentials(broken).Contains("S3cr3t"));
		}

		[TestMethod]
		public void RedactForLog_DoseSpotHidesTheSsoValues() {
			string query=DoseSpotSso.Create("1","key","2").ToQueryString("","3",false);
			string redacted=OpenDentBusiness.Erx.RedactForLog(query,ErxOption.DoseSpot);
			Assert.IsFalse(Regex.IsMatch(redacted,"SingleSignOnCode=[^*]"));
			Assert.IsFalse(Regex.IsMatch(redacted,"SingleSignOnUserIdVerify=[^*]"));
		}

		[TestMethod]
		public void NormalizeNpi_KeepsDigitsOnly() {
			Assert.AreEqual("1234567893",NewCropConfig.NormalizeNpi(" 123-456-7893 "));
			Assert.AreEqual("",NewCropConfig.NormalizeNpi(null));
		}

		[TestMethod]
		public void Throttle_LimitsUnlessForcedAndHonoursPauses() {
			DateTime now=new DateTime(2026,10,19,8,0,0,DateTimeKind.Utc);
			ErxPollThrottle erxPollThrottle=new ErxPollThrottle(() => now);
			Assert.IsTrue(erxPollThrottle.TryBegin("V","k",TimeSpan.FromMinutes(2)));
			Assert.IsFalse(erxPollThrottle.TryBegin("V","k",TimeSpan.FromMinutes(2)));
			Assert.IsTrue(erxPollThrottle.TryBegin("V","other",TimeSpan.FromMinutes(2)),"Keys are independent.");
			Assert.IsTrue(erxPollThrottle.TryBegin("V","k",TimeSpan.FromMinutes(2),isForced:true));
			now=now.AddMinutes(2);
			Assert.IsTrue(erxPollThrottle.TryBegin("V","k",TimeSpan.FromMinutes(2)));
			erxPollThrottle.Forget("V","k");
			Assert.IsTrue(erxPollThrottle.TryBegin("V","k",TimeSpan.FromMinutes(2)));
			erxPollThrottle.Pause("V",null);
			Assert.AreEqual(ErxPollThrottle.DefaultRetryAfter,erxPollThrottle.GetPauseRemaining("V"));
			Assert.IsFalse(erxPollThrottle.TryBegin("V","new",TimeSpan.FromMinutes(2),isForced:true),"A pause stops even forced calls.");
			Assert.IsTrue(erxPollThrottle.TryBegin("W","new",TimeSpan.FromMinutes(2)),"Another vendor is not paused.");
			now=now.AddSeconds(61);
			Assert.IsTrue(erxPollThrottle.TryBegin("V","new",TimeSpan.FromMinutes(2)));
		}
	}
}
