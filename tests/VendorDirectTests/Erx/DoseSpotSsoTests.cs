// Tests for the ERX package: DoseSpot single sign-on values computed locally, and their redaction for logs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace VendorDirectTests.Erx {
	[TestClass]
	public class DoseSpotSsoTests {
		private const string PHRASE="ABCDEFGHIJKLMNOPQRSTUVWXYZ012345";
		private const string CLINIC_KEY="Key+/=Test123";
		private const string USER_ID="1234";
		//Computed once outside this code (PowerShell, System.Security.Cryptography.SHA512) for PHRASE, CLINIC_KEY and USER_ID.
		private const string EXPECTED_CODE="ABCDEFGHIJKLMNOPQRSTUVWXYZ012345VFwb0z399bF7UfVf9AmIABCsc37ra3xHIRt//J3j9/75Ba/ckQcM7kymarMIxGKITQ/uPxfEYqnDWGJBKy4hhg";
		private const string EXPECTED_VERIFY="2+3yKhwerVS2WZYttRLRvEQ2qygQvX15PxO6oyVfqT76xyu7HLr21wu2uo9KLT0EPJVvbhFAHiLvB+alY+y4+A";

		[TestMethod]
		public void Create_KnownAnswer() {
			DoseSpotSso doseSpotSso=DoseSpotSso.Create(" 77 ",CLINIC_KEY,USER_ID,PHRASE);
			Assert.AreEqual(EXPECTED_CODE,doseSpotSso.SingleSignOnCode);
			Assert.AreEqual(EXPECTED_VERIFY,doseSpotSso.SingleSignOnUserIdVerify);
			Assert.AreEqual("77",doseSpotSso.ClinicId);
			Assert.AreEqual(USER_ID,doseSpotSso.UserId);
		}

		[TestMethod]
		public void ComputeCode_MatchesTheDocumentedSteps() {
			//phrase + Base64(SHA-512(phrase + key)) with the trailing "==" removed.
			string expected;
			using(SHA512 sha512=SHA512.Create()) {
				expected=PHRASE+Convert.ToBase64String(sha512.ComputeHash(Encoding.UTF8.GetBytes(PHRASE+CLINIC_KEY))).TrimEnd('=');
			}
			Assert.AreEqual(expected,DoseSpotSso.ComputeCode(PHRASE,CLINIC_KEY));
			Assert.IsFalse(DoseSpotSso.ComputeCode(PHRASE,CLINIC_KEY).EndsWith("="));
		}

		[TestMethod]
		public void CreatePhrase_IsFreshEachTimeAndAlphanumeric() {
			HashSet<string> hashSetPhrases=new HashSet<string>();
			for(int i=0;i<200;i++) {
				string phrase=DoseSpotSso.CreatePhrase();
				Assert.AreEqual(DoseSpotSso.PhraseLength,phrase.Length);
				Assert.IsTrue(Regex.IsMatch(phrase,"^[A-Za-z0-9]+$"),phrase);
				Assert.IsTrue(hashSetPhrases.Add(phrase),"A phrase repeated.");
			}
		}

		[TestMethod]
		public void Create_WithoutPhrase_NeverReusesThePhrase() {
			DoseSpotSso doseSpotSsoA=DoseSpotSso.Create("1",CLINIC_KEY,USER_ID);
			DoseSpotSso doseSpotSsoB=DoseSpotSso.Create("1",CLINIC_KEY,USER_ID);
			Assert.AreNotEqual(doseSpotSsoA.SingleSignOnCode.Substring(0,32),doseSpotSsoB.SingleSignOnCode.Substring(0,32));
			Assert.AreNotEqual(doseSpotSsoA.SingleSignOnUserIdVerify,doseSpotSsoB.SingleSignOnUserIdVerify);
		}

		[TestMethod]
		public void ToQueryString_EncodesOnceAndPicksTheTarget() {
			DoseSpotSso doseSpotSso=DoseSpotSso.Create("77",CLINIC_KEY,USER_ID,PHRASE);
			string queryPatient=doseSpotSso.ToQueryString("","555",false);
			StringAssert.StartsWith(queryPatient,"&SingleSignOnCode=");
			StringAssert.Contains(queryPatient,"&SingleSignOnCode="+Uri.EscapeDataString(EXPECTED_CODE)+"&");
			StringAssert.Contains(queryPatient,"&SingleSignOnUserIdVerify="+Uri.EscapeDataString(EXPECTED_VERIFY));
			StringAssert.Contains(queryPatient,"&SingleSignOnUserId=1234");
			StringAssert.Contains(queryPatient,"&SingleSignOnClinicId=77");
			StringAssert.Contains(queryPatient,"&PatientId=555");
			Assert.IsFalse(queryPatient.Contains("%25"),"Values must not be encoded twice.");
			string queryQueue=doseSpotSso.ToQueryString("4321","555",true);
			StringAssert.Contains(queryQueue,"&OnBehalfOfUserId=4321");
			StringAssert.Contains(queryQueue,"&RefillsErrors=1");
			Assert.IsFalse(queryQueue.Contains("PatientId="));
		}

		[TestMethod]
		public void Redact_HidesTheCodeAndVerificationOnly() {
			DoseSpotSso doseSpotSso=DoseSpotSso.Create("77",CLINIC_KEY,USER_ID,PHRASE);
			string query=doseSpotSso.ToQueryString("","555",false);
			string redacted=DoseSpotSso.Redact(query);
			Assert.IsFalse(redacted.Contains(Uri.EscapeDataString(EXPECTED_CODE)));
			Assert.IsFalse(redacted.Contains(Uri.EscapeDataString(EXPECTED_VERIFY)));
			Assert.IsFalse(redacted.Contains(PHRASE));
			StringAssert.Contains(redacted,"SingleSignOnCode=***");
			StringAssert.Contains(redacted,"SingleSignOnUserIdVerify=***");
			StringAssert.Contains(redacted,"SingleSignOnUserId=1234");
			StringAssert.Contains(redacted,"PatientId=555");
			string url=OpenDentBusiness.Erx.BuildDoseSpotUrl(DoseSpotConfig.UrlSsoStaging,query);
			StringAssert.StartsWith(url,DoseSpotConfig.UrlSsoStaging+"&SingleSignOnCode=");
			StringAssert.Contains(DoseSpotSso.Redact(url),"?b=2&SingleSignOnCode=***");
			Assert.AreEqual("",DoseSpotSso.Redact(null));
		}

		[TestMethod]
		public void BuildDoseSpotUrl_StartsTheQueryWithQuestionMark() {
			Assert.AreEqual(DoseSpotConfig.UrlSsoProduction+"?a=1&b=2",OpenDentBusiness.Erx.BuildDoseSpotUrl(DoseSpotConfig.UrlSsoProduction,"&a=1&b=2"));
			Assert.AreEqual(DoseSpotConfig.UrlSsoProduction,OpenDentBusiness.Erx.BuildDoseSpotUrl(DoseSpotConfig.UrlSsoProduction,null));
		}

		[TestMethod]
		public void Create_RefusesABlankKey() {
			AssertEx.Throws<ArgumentException>(() => DoseSpotSso.Create("1","",USER_ID,PHRASE));
		}
	}
}
