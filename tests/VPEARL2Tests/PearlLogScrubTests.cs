// V-PEARL2: the log never holds the client secret, auth tokens, presigned upload URLs or signatures (26.1 spec, business rule 2).
// All values below are made up.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness.Pearl;

namespace VPEARL2Tests {
	[TestClass]
	public class PearlLogScrubTests {
		private const string CLIENT_SECRET="synthetic-client-secret-7Hq2";
		private const string ACCESS_TOKEN="eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJzeW50aGV0aWMifQ.c2lnbmF0dXJlLW5vdC1yZWFs";

		[TestMethod]
		public void ClientSecret_IsRedactedWherever_ItAppears() {
			using(LogCapture logCapture=new LogCapture("2")) {
				logCapture.DictProps[OpenPmsDentalBusiness.Bridges.Pearl.PEARL_CLIENT_SECRET_PROPERTY]=CLIENT_SECRET;
				PearlLog.Error("Auth failed for "+CLIENT_SECRET+" (echoed by the server)");
				PearlLog.Verbose("{\"client_id\":\"office-17\",\"client_secret\":\""+CLIENT_SECRET+"\"}");
				string text=logCapture.AllText;
				Assert.IsFalse(text.Contains(CLIENT_SECRET),text);
				Assert.IsFalse(text.Contains("7Hq2"),text);
				StringAssert.Contains(text,"office-17","Identifiers that are not secrets stay readable.");
			}
		}

		[TestMethod]
		public void ClientSecret_StoredEncrypted_IsRedactedToo() {
			string stored;
			Assert.IsTrue(OpenPmsDentalBusiness.VendorDirect.SecretValue.Protect(CLIENT_SECRET,out stored));
			Assert.AreNotEqual(CLIENT_SECRET,stored);
			using(LogCapture logCapture=new LogCapture("0")) {
				logCapture.DictProps[OpenPmsDentalBusiness.Bridges.Pearl.PEARL_CLIENT_SECRET_PROPERTY]=stored;
				PearlLog.Error("value "+CLIENT_SECRET+" and stored "+stored);
				Assert.IsFalse(logCapture.AllText.Contains(CLIENT_SECRET),logCapture.AllText);
				Assert.IsFalse(logCapture.AllText.Contains(stored),logCapture.AllText);
			}
		}

		[TestMethod]
		public void ShortSecret_DoesNotBlankOrdinaryText() {
			using(LogCapture logCapture=new LogCapture("0")) {
				logCapture.DictProps[OpenPmsDentalBusiness.Bridges.Pearl.PEARL_CLIENT_SECRET_PROPERTY]="a";
				PearlLog.Error("a plain message");
				Assert.AreEqual("ERROR\ta plain message",logCapture.Lines.Single());
			}
		}

		[TestMethod]
		public void Tokens_AreRedacted() {
			string[] arrayInputs=new[] {
				"Authorization: Bearer "+ACCESS_TOKEN,
				"{\"access_token\":\""+ACCESS_TOKEN+"\",\"token_type\":\"Bearer\",\"expires_in\":604800}",
				"raw token "+ACCESS_TOKEN,
				"{\"Message\":\"Response status code does not indicate success: 401\",\"Response\":\"{\\\"access_token\\\":\\\"abc123secret\\\"}\"}",
				"access_token=abc123secret&scope=api",
				"Basic dXNlcjpwYXNzd29yZA==",
			};
			foreach(string input in arrayInputs) {
				string scrubbed=PearlLog.Scrub(input);
				Assert.IsFalse(scrubbed.Contains(ACCESS_TOKEN),scrubbed);
				Assert.IsFalse(scrubbed.Contains("abc123secret"),scrubbed);
				Assert.IsFalse(scrubbed.Contains("dXNlcjpwYXNzd29yZA"),scrubbed);
				StringAssert.Contains(scrubbed,"[redacted]",input);
			}
			StringAssert.Contains(PearlLog.Scrub("{\"token_type\":\"Bearer\",\"expires_in\":604800}"),"604800","Harmless fields stay.");
		}

		[TestMethod]
		public void PresignedUploadUrls_AreNeverLogged() {
			string urlPresigned="https://pearl-uploads-synthetic.s3.us-west-2.amazonaws.com/incoming/abc.jpg?X-Amz-Algorithm=AWS4-HMAC-SHA256"
				+"&X-Amz-Credential=AKIASYNTHETIC%2F20261025&X-Amz-Signature=0123456789abcdef0123456789abcdef";
			string scrubbed=PearlLog.Scrub("upload to "+urlPresigned+" failed");
			Assert.AreEqual("upload to [presigned storage URL] failed",scrubbed);
			string scrubbedBucket=PearlLog.Scrub("POST https://pearl-uploads-synthetic.s3.amazonaws.com/ returned 403");
			Assert.IsFalse(scrubbedBucket.Contains("pearl-uploads-synthetic"),scrubbedBucket);
			//An S3 error document names the signature material; none of it is kept.
			string s3Error="<Error><Code>SignatureDoesNotMatch</Code><AWSAccessKeyId>AKIASYNTHETIC0000</AWSAccessKeyId>"
				+"<StringToSign>AWS4-HMAC-SHA256 synthetic</StringToSign><SignatureProvided>feedface</SignatureProvided>"
				+"<Policy>eyJleHBpcmF0aW9uIjoic3ludGhldGljIn0=</Policy></Error>";
			string scrubbedXml=PearlLog.Scrub(s3Error);
			StringAssert.Contains(scrubbedXml,"SignatureDoesNotMatch","The error code stays, it is what troubleshooting needs.");
			foreach(string secret in new[] { "AKIASYNTHETIC0000","AWS4-HMAC-SHA256 synthetic","feedface","eyJleHBpcmF0aW9uIjoic3ludGhldGljIn0" }) {
				Assert.IsFalse(scrubbedXml.Contains(secret),scrubbedXml);
			}
			//Form fields of the presigned POST, if an exception ever named them.
			string scrubbedFields=PearlLog.Scrub("policy=eyJleHAiOjF9 x-amz-signature=cafebabe X-Amz-Credential: AKIASYNTHETIC/2026");
			Assert.IsFalse(scrubbedFields.Contains("cafebabe"),scrubbedFields);
			Assert.IsFalse(scrubbedFields.Contains("AKIASYNTHETIC"),scrubbedFields);
		}

		[TestMethod]
		public void OtherUrls_KeepHostAndPath_LoseQueryAndUserInfo() {
			Assert.AreEqual("https://pearl-api.example.test/api/v1/image",PearlLog.ScrubUrl("https://pearl-api.example.test/api/v1/image"));
			Assert.AreEqual("https://pearl-api.example.test/api/v1/image?[redacted]",PearlLog.ScrubUrl("https://pearl-api.example.test/api/v1/image?key=abc#x"));
			Assert.AreEqual("https://pearl-api.example.test/path?[redacted]",PearlLog.ScrubUrl("https://user:pass@pearl-api.example.test/path"));
			Assert.AreEqual("[URL]",PearlLog.ScrubUrl("https://"));
		}

		[TestMethod]
		public void LongOpaqueStrings_AreRedacted_ButGuidsAndPathsStay() {
			string base64Image=Convert.ToBase64String(Enumerable.Range(0,300).Select(x => (byte)x).ToArray()).Replace("/","A");
			string scrubbed=PearlLog.Scrub("data "+base64Image);
			Assert.IsFalse(scrubbed.Contains(base64Image.Substring(0,40)),scrubbed);
			string guid="6f1c0b5e-1111-4d2a-9c3b-000000000001";
			Assert.AreEqual("GET /api/v1/image/org-17/byRequestId/"+guid,PearlLog.Scrub("GET /api/v1/image/org-17/byRequestId/"+guid));
		}

		[TestMethod]
		public void Lines_StayOnOneLine_AndAreCut() {
			Assert.AreEqual("There was an error uploading an image: | Could not upload image successfully.",
				PearlLog.Scrub("There was an error uploading an image:\r\nCould not upload image successfully."));
			Assert.AreEqual("a b",PearlLog.Scrub("a\tb"));
			string longText=string.Join(" ",Enumerable.Repeat("word",2000));
			string scrubbed=PearlLog.Scrub(longText);
			Assert.AreEqual(PearlLog.MAX_LINE_LENGTH+3,scrubbed.Length);
			Assert.IsTrue(scrubbed.EndsWith("..."));
			Assert.AreEqual("",PearlLog.Scrub(null));
		}

		[TestMethod]
		public void EveryWrittenLine_IsScrubbed_IncludingTheScope() {
			using(LogCapture logCapture=new LogCapture("2")) {
				logCapture.DictProps[OpenPmsDentalBusiness.Bridges.Pearl.PEARL_CLIENT_SECRET_PROPERTY]=CLIENT_SECRET;
				using(PearlLog.BeginScope(3,CLIENT_SECRET)) {
					PearlLog.Write(LogLevel.Verbose,"Bearer "+ACCESS_TOKEN);
				}
				string line=logCapture.Lines.Single();
				Assert.IsFalse(line.Contains(CLIENT_SECRET),line);
				Assert.IsFalse(line.Contains(ACCESS_TOKEN),line);
				Assert.IsFalse(line.Contains("\n"));
			}
		}
	}
}
