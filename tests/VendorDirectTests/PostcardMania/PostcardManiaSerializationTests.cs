// Tests for the PostcardMania (PCM Integrations) DirectMail API v3 integration: JSON shapes, list and error parsing. No network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenDentBusiness.PostcardMania;

namespace VendorDirectTests.PostcardMania {
	[TestClass]
	public class PostcardManiaSerializationTests {
		[TestMethod]
		public void OrderRequest_UsesTheDocumentedCamelCaseNamesAndOmitsNulls() {
			PcmPostcardOrderRequest request=PostcardManiaFixture.MakeOrder("ops-0-20261019120000-abcdef",1);
			request.ReturnAddress=new PcmReturnAddress { Company="Practice",Address="1 Office Way",City="Marysville",State="WA",ZipCode="98270" };
			JObject jObject=JObject.Parse(PcmJson.Serialize(request));
			Assert.AreEqual(77,(long)jObject["designID"]);
			Assert.AreEqual("FirstClass",(string)jObject["mailClass"]);
			Assert.AreEqual("ops-0-20261019120000-abcdef",(string)jObject["extRefNbr"]);
			Assert.AreEqual("2026-11-02",(string)jObject["mailDate"],"mailDate is yyyy-MM-dd.");
			Assert.AreEqual("1 Office Way",(string)jObject["returnAddress"]["address"]);
			Assert.AreEqual("98270",(string)jObject["returnAddress"]["zipCode"]);
			Assert.IsNull(jObject["addressing"],"Null optionals are left out.");
			Assert.IsNull(jObject["globalDesignVariables"]);
			Assert.IsNull(jObject["returnAddress"]["address2"]);
			JObject jObjectRecipient=(JObject)jObject["recipients"][0];
			CollectionAssert.AreEquivalent(new[] { "firstName","lastName","address","city","state","zipCode" },
				new List<string>(GetNames(jObjectRecipient)),"Only the name and the mailing address are sent by default.");
		}

		[TestMethod]
		public void AuthRequest_OmitsABlankChildReference() {
			JObject jObject=JObject.Parse(PcmJson.Serialize(new PcmAuthRequest { ApiKey="k",ApiSecret="s" }));
			Assert.AreEqual("k",(string)jObject["apiKey"]);
			Assert.AreEqual("s",(string)jObject["apiSecret"]);
			Assert.IsNull(jObject["childRefNbr"]);
			jObject=JObject.Parse(PcmJson.Serialize(new PcmAuthRequest { ApiKey="k",ApiSecret="s",ChildRefNbr="clinic-3" }));
			Assert.AreEqual("clinic-3",(string)jObject["childRefNbr"]);
		}

		[TestMethod]
		public void Responses_IgnoreUnknownFields() {
			PcmOrderResponse response=PcmJson.Deserialize<PcmOrderResponse>("{\"orderID\":5001,\"batchID\":12,\"extRefNbr\":\"r\",\"somethingNew\":{\"a\":1}}");
			Assert.AreEqual(5001,response.OrderID);
			Assert.AreEqual(12,response.BatchID);
			PcmDesign design=PcmJson.Deserialize<PcmDesign>("{\"designID\":\"9\",\"friendlyName\":\"Fall\",\"size\":{\"key\":\"46\",\"label\":\"4.25 x 6\"},"
				+"\"mailClasses\":[\"FirstClass\"],\"isUpload\":true,\"designFields\":[{\"fieldKey\":\"firstName\",\"mandatory\":true},"
				+"{\"fieldKey\":\"offerCode\",\"mandatory\":true},{\"fieldKey\":\"note\",\"mandatory\":false}]}");
			Assert.AreEqual(9,design.DesignID);
			Assert.IsTrue(design.IsUpload);
			CollectionAssert.AreEqual(new[] { "offerCode" },design.GetMandatoryCustomFields(),"Name and address fields are filled from the recipient.");
			Assert.AreEqual("Fall (4.25 x 6)",design.ToString());
		}

		[TestMethod]
		public void ParsePaged_AcceptsAnObjectWithResultsOrABareArray() {
			PcmPagedResult<PcmOrder> paged=PostcardManiaApiClient.ParsePaged<PcmOrder>(
				"{\"pagination\":{\"currentPage\":1,\"perPage\":2,\"totalPages\":3,\"totalResults\":5},\"results\":[{\"orderID\":1,\"status\":\"Pending\"},{\"orderID\":2}]}",
				"test");
			Assert.AreEqual(2,paged.Results.Count);
			Assert.IsTrue(paged.Results[0].IsCancelable);
			Assert.IsTrue(paged.HasMorePages(1));
			Assert.IsFalse(paged.HasMorePages(3));
			paged=PostcardManiaApiClient.ParsePaged<PcmOrder>("[{\"orderID\":7}]","test");
			Assert.AreEqual(7,paged.Results[0].OrderID);
			Assert.IsFalse(paged.HasMorePages(1));
			paged=PostcardManiaApiClient.ParsePaged<PcmOrder>("{\"data\":[{\"orderID\":8}],\"meta\":{\"totalPages\":1}}","test");
			Assert.AreEqual(8,paged.Results[0].OrderID);
			Assert.AreEqual(0,PostcardManiaApiClient.ParsePaged<PcmOrder>("","test").Results.Count);
		}

		[TestMethod]
		public void ParseErrorMessages_ReadsTheApiErrorShape() {
			string body="{\"error\":{\"code\":422,\"message\":\"One or more errors occurred\",\"data\":[{\"message\":\"Zip code is invalid\",\"property\":"
				+"\"recipients[0].zipCode\"},{\"message\":\"Design not approved\"}]},\"documentation\":\"https://docs.pcmintegrations.com/x\"}";
			List<string> listMessages=PostcardManiaApiClient.ParseErrorMessages(body);
			CollectionAssert.AreEqual(new[] { "recipients[0].zipCode: Zip code is invalid","Design not approved" },listMessages);
			CollectionAssert.AreEqual(new[] { "Something specific" },
				PostcardManiaApiClient.ParseErrorMessages("{\"error\":{\"code\":400,\"message\":\"Something specific\",\"data\":[]}}"));
			Assert.AreEqual(0,PostcardManiaApiClient.ParseErrorMessages("{\"error\":{\"code\":400,\"message\":\"One or more errors occurred\"}}").Count);
			Assert.AreEqual(0,PostcardManiaApiClient.ParseErrorMessages("<html>bad gateway</html>").Count);
		}

		[TestMethod]
		public void ParseExpires_ReadsIsoOrFallsBackBelowFifteenMinutes() {
			DateTime dateTimeNow=new DateTime(2026,10,19,12,0,0,DateTimeKind.Utc);
			Assert.AreEqual(new DateTime(2026,10,19,12,15,0),PostcardManiaApiClient.ParseExpires("2026-10-19T12:15:00Z",dateTimeNow));
			Assert.AreEqual(new DateTime(2026,10,19,12,15,0),PostcardManiaApiClient.ParseExpires("2026-10-19T08:15:00-04:00",dateTimeNow));
			Assert.AreEqual(dateTimeNow.AddMinutes(14),PostcardManiaApiClient.ParseExpires("",dateTimeNow));
			Assert.AreEqual(dateTimeNow.AddMinutes(14),PostcardManiaApiClient.ParseExpires("soon",dateTimeNow));
		}

		[TestMethod]
		public void Exception_NeverCarriesAnInnerException() {
			//ProgressWin rethrows the innermost exception, which would hide the friendly message.
			PostcardManiaException ex=new PostcardManiaException(PostcardManiaErrorKind.Validation,"Refused.",System.Net.HttpStatusCode.BadRequest,
				new List<string> { "a: b" });
			Assert.IsNull(ex.InnerException);
			StringAssert.Contains(ex.Message,"- a: b");
			Assert.IsFalse(ex.IsOutcomeUnknown);
			Assert.IsTrue(new PostcardManiaException(PostcardManiaErrorKind.Timeout,"t").IsOutcomeUnknown);
		}

		private static IEnumerable<string> GetNames(JObject jObject) {
			foreach(JProperty jProperty in jObject.Properties()) {
				yield return jProperty.Name;
			}
		}
	}
}
