// Tests for the USPS Addresses v3 client: response parsing, request building, the token cache and the error mapping. Fake handlers only; no
// network call is made and no real credentials are used.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using Task=System.Threading.Tasks.Task;

namespace VendorDirectTests.Usps {
	[TestClass]
	public class UspsApiClientTests {
		private static readonly DateTime _dateTimeStart=new DateTime(2026,10,19,12,0,0,DateTimeKind.Utc);
		///<summary>Modelled on the USPS api-examples README sample (3120 M ST NW, Washington DC), plus a field this program doesn't know.</summary>
		private const string JSON_ADDRESS_SAMPLE="{\"firm\":null,"
			+"\"address\":{\"streetAddress\":\"3120 M ST NW\",\"streetAddressAbbreviation\":null,\"secondaryAddress\":null,\"cityAbbreviation\":null,"
			+"\"city\":\"WASHINGTON\",\"state\":\"DC\",\"ZIPCode\":\"20007\",\"ZIPPlus4\":\"3704\",\"urbanization\":null},"
			+"\"additionalInfo\":{\"deliveryPoint\":\"99\",\"carrierRoute\":\"C000\",\"DPVConfirmation\":\"Y\",\"DPVCMRA\":\"N\",\"business\":\"Y\","
			+"\"centralDeliveryPoint\":\"N\",\"vacant\":\"N\",\"someIndicatorAddedLater\":{\"nested\":true}},"
			+"\"corrections\":[{\"code\":\"\",\"text\":\"\"}],"
			+"\"matches\":[{\"code\":\"31\",\"text\":\"Single Response - exact match\"}],"
			+"\"warnings\":[]}";
		private DateTime _nowUtc;

		[TestInitialize]
		public void TestInitialize() {
			_nowUtc=_dateTimeStart;
			UspsAddresses.ClearQuotaCooldown();
		}

		[TestCleanup]
		public void TestCleanup() {
			UspsAddresses.ClearQuotaCooldown();
		}

		private UspsApiClient MakeClient(FakeHttpHandler fakeHttpHandler,List<string> listLog=null) {
			return new UspsApiClient(fakeHttpHandler,() => _nowUtc,x => listLog?.Add(x));
		}

		private static UspsConfig MakeConfig(string key="test-key",string secret="test-secret",UspsEnvironment uspsEnvironment=UspsEnvironment.Production) {
			UspsConfig uspsConfig=new UspsConfig();
			uspsConfig.ConsumerKey=key;
			uspsConfig.ConsumerSecret=secret;
			uspsConfig.Environment=uspsEnvironment;
			return uspsConfig;
		}

		private static string TokenJson(string accessToken,object expiresIn) {
			string expires=(expiresIn is string) ? "\""+expiresIn+"\"" : expiresIn.ToString();
			return "{\"access_token\":\""+accessToken+"\",\"token_type\":\"Bearer\",\"issued_at\":1760875200000,\"expires_in\":"+expires+","
				+"\"status\":\"approved\",\"scope\":\"addresses\",\"issuer\":\"api.usps.com\",\"client_id\":\"test-key\"}";
		}

		private static UspsAddressQuery MakeQuery(string address1="3120 M St NW",string address2="",string city="Washington",string state="DC",
			string zip="20007")
		{
			Address address=new Address();
			address.Address1=address1;
			address.Address2=address2;
			address.City=city;
			address.State=state;
			address.Zip=zip;
			string errorMessage;
			UspsAddressQuery query=UspsAddressQuery.FromAddress(address,out errorMessage);
			Assert.IsNotNull(query,errorMessage);
			return query;
		}

		private static T Run<T>(Func<Task<T>> funcAsync) {
			return Task.Run(funcAsync).GetAwaiter().GetResult();
		}

		private static UspsApiException RunExpectingError(Func<Task> funcAsync) {
			return AssertEx.Throws<UspsApiException>(() => Task.Run(funcAsync).GetAwaiter().GetResult());
		}

		private static Dictionary<string,string> ParseQuery(string query) {
			return query.TrimStart('?').Split(new[] { '&' },StringSplitOptions.RemoveEmptyEntries)
				.Select(x => x.Split(new[] { '=' },2))
				.ToDictionary(x => x[0],x => Uri.UnescapeDataString(x.Length>1 ? x[1] : ""));
		}

		#region Deserialization
		[TestMethod]
		public void Deserialize_ReadmeSample_MapsAddressAndDpv() {
			USPSAddressValidationResponse response=UspsApiClient.Deserialize(JSON_ADDRESS_SAMPLE);
			Assert.IsNotNull(response);
			Assert.AreEqual("3120 M ST NW",response.address.streetAddress);
			Assert.IsNull(response.address.secondaryAddress);
			Assert.AreEqual("WASHINGTON",response.address.city);
			Assert.AreEqual("DC",response.address.state);
			Assert.AreEqual("20007",response.address.ZIPCode);
			Assert.AreEqual("3704",response.address.ZIPPlus4);
			Assert.AreEqual("Y",response.additionalInfo.DPVConfirmation);
			Assert.AreEqual("31",response.matches[0].code);
			Assert.IsNull(response.ErrorMessage);
		}

		[TestMethod]
		public void Deserialize_UnknownOrChangedFields_AreIgnored() {
			//warnings changed from strings to objects, plus a new top-level field: the rest of the answer is still read.
			string json="{\"newTopLevel\":[1,2],\"address\":{\"streetAddress\":\"1 MAIN ST\",\"ZIPCode\":\"12345\"},"
				+"\"warnings\":[{\"code\":\"W1\"}],\"additionalInfo\":{\"DPVConfirmation\":\"D\",\"DPVEnhancedConfirmation\":\"R\"}}";
			USPSAddressValidationResponse response=UspsApiClient.Deserialize(json);
			Assert.IsNotNull(response);
			Assert.AreEqual("1 MAIN ST",response.address.streetAddress);
			Assert.AreEqual("D",response.additionalInfo.DPVConfirmation);
			Assert.AreEqual("R",response.additionalInfo.DPVEnhancedConfirmation);
			//A field inside the address changed shape: that field is skipped, the others are kept.
			json="{\"address\":{\"streetAddress\":\"1 MAIN ST\",\"city\":{\"name\":\"SPRINGFIELD\"},\"state\":\"IL\",\"ZIPCode\":\"62701\"}}";
			response=UspsApiClient.Deserialize(json);
			Assert.IsNotNull(response?.address);
			Assert.AreEqual("1 MAIN ST",response.address.streetAddress);
			Assert.AreEqual("IL",response.address.state);
			Assert.AreEqual("62701",response.address.ZIPCode);
		}

		[TestMethod]
		public void Deserialize_NotJson_ReturnsNull() {
			Assert.IsNull(UspsApiClient.Deserialize("<html>gateway error</html>"));
			Assert.IsNull(UspsApiClient.Deserialize(""));
		}

		[TestMethod]
		public void DpvConfirmation_ParsesAsTheCallersDo() {
			Assert.AreEqual(EnumDPVConfirmation.Y,(EnumDPVConfirmation)Enum.Parse(typeof(EnumDPVConfirmation),"Y"));
			Assert.AreEqual(EnumDPVConfirmation.D,(EnumDPVConfirmation)Enum.Parse(typeof(EnumDPVConfirmation),"D"));
			Assert.AreEqual(EnumDPVConfirmation.S,(EnumDPVConfirmation)Enum.Parse(typeof(EnumDPVConfirmation),"S"));
			Assert.AreEqual(EnumDPVConfirmation.N,(EnumDPVConfirmation)Enum.Parse(typeof(EnumDPVConfirmation),"N"));
			Assert.AreEqual("",CodeBase.ODPrimitiveExtensions.GetDescription(EnumDPVConfirmation.Y));
			Assert.IsTrue(CodeBase.ODPrimitiveExtensions.GetDescription(EnumDPVConfirmation.D).Contains("missing"));
			Assert.IsTrue(CodeBase.ODPrimitiveExtensions.GetDescription(EnumDPVConfirmation.None).Length>0);
			//FrmAddressCompare catches this and shows None.
			AssertEx.Throws<ArgumentException>(() => Enum.Parse(typeof(EnumDPVConfirmation),""));
		}
		#endregion Deserialization

		#region Request building
		[TestMethod]
		public void Query_ZipPlus4_IsSplit() {
			UspsAddressQuery query=MakeQuery(zip:"12345-6789");
			Assert.AreEqual("12345",query.ZIPCode);
			Assert.AreEqual("6789",query.ZIPPlus4);
			query=MakeQuery(zip:"123456789");
			Assert.AreEqual("12345",query.ZIPCode);
			Assert.AreEqual("6789",query.ZIPPlus4);
		}

		[TestMethod]
		public void Query_Zip5_GivesZipCodeOnly() {
			UspsAddressQuery query=MakeQuery(zip:" 12345 ");
			Assert.AreEqual("12345",query.ZIPCode);
			Assert.AreEqual("",query.ZIPPlus4);
			Assert.IsFalse(query.ToQueryString().Contains("ZIPPlus4"));
		}

		[TestMethod]
		public void Query_BadZip_IsLeftOut() {
			foreach(string zip in new[] { "1234","12345-67","ABCDE","12345-678901" }) {
				UspsAddressQuery query=MakeQuery(zip:zip);
				Assert.AreEqual("",query.ZIPCode,zip);
				Assert.AreEqual("",query.ZIPPlus4,zip);
				Assert.IsFalse(query.ToQueryString().Contains("ZIP"),zip);
			}
		}

		[TestMethod]
		public void Query_State_IsUpperCasedAndInvalidLeftOut() {
			Assert.AreEqual("VA",MakeQuery(state:"va").State);
			Assert.AreEqual("PR",MakeQuery(state:" pr ").State);
			Assert.AreEqual("AE",MakeQuery(state:"AE").State);
			UspsAddressQuery query=MakeQuery(state:"XX",zip:"20007");
			Assert.AreEqual("",query.State);
			Assert.IsFalse(query.ToQueryString().Contains("state="));
		}

		[TestMethod]
		public void Query_StreetIsCutTo50_AndNoFirmIsSent() {
			string street=new string('A',45)+" BLVD WEST";
			UspsAddressQuery query=MakeQuery(address1:street);
			Assert.IsTrue(query.StreetAddress.Length<=50);
			Assert.IsTrue(street.StartsWith(query.StreetAddress));
			string queryString=query.ToQueryString();
			Assert.IsFalse(queryString.Contains("firm"));
			Assert.IsFalse(queryString.Contains("urbanization"));
		}

		[TestMethod]
		public void Query_ValuesArePercentEncoded() {
			UspsAddressQuery query=MakeQuery(address1:"475 L'Enfant Plaza SW #2 & Co",address2:"STE (100)",city:"Washington",state:"DC",zip:"20260");
			string queryString=query.ToQueryString();
			Assert.IsFalse(queryString.Contains("'"),queryString);
			Assert.IsFalse(queryString.Contains("#"),queryString);
			Assert.IsFalse(queryString.Contains(" "),queryString);
			Assert.IsFalse(queryString.Contains("("),queryString);
			Assert.IsTrue(queryString.Contains("%27"),queryString);
			Assert.IsTrue(queryString.Contains("%23"),queryString);
			Assert.IsTrue(queryString.Contains("%26"),queryString);
			Dictionary<string,string> dictParams=ParseQuery(queryString);
			Assert.AreEqual("475 L'Enfant Plaza SW #2 & Co",dictParams["streetAddress"]);
			Assert.AreEqual("STE (100)",dictParams["secondaryAddress"]);
			Assert.AreEqual("Washington",dictParams["city"]);
			Assert.AreEqual("DC",dictParams["state"]);
			Assert.AreEqual("20260",dictParams["ZIPCode"]);
			//The escaping survives building the request URI.
			Uri uri=UspsApiClient.GetAddressUri(UspsEnvironment.Production,query);
			Assert.AreEqual("475 L'Enfant Plaza SW #2 & Co",ParseQuery(uri.Query)["streetAddress"]);
		}

		[TestMethod]
		public void Query_NotEnoughToCheck_MakesNoQuery() {
			Address address=new Address();
			address.Address1="1 Main St";
			address.City="Springfield";
			string errorMessage;
			Assert.IsNull(UspsAddressQuery.FromAddress(address,out errorMessage),"No state and no ZIP.");
			Assert.IsTrue(errorMessage.Contains("ZIP"));
			address.Address1="  ";
			address.State="IL";
			address.Zip="62701";
			Assert.IsNull(UspsAddressQuery.FromAddress(address,out errorMessage),"No street.");
			address.Address1="1 Main St";
			address.City="";
			address.State="";
			Assert.IsNotNull(UspsAddressQuery.FromAddress(address,out errorMessage),"Street and ZIP are enough.");
		}
		#endregion Request building

		#region Hosts
		[TestMethod]
		public void Urls_AreUspsHostsOnly() {
			UspsAddressQuery query=MakeQuery();
			foreach(UspsEnvironment uspsEnvironment in new[] { UspsEnvironment.Production,UspsEnvironment.Testing }) {
				foreach(Uri uri in new[] { UspsApiClient.GetBaseUri(uspsEnvironment),UspsApiClient.GetTokenUri(uspsEnvironment),
					UspsApiClient.GetAddressUri(uspsEnvironment,query) })
				{
					Assert.IsTrue(uri.AbsoluteUri.StartsWith("https://apis.usps.com/") || uri.AbsoluteUri.StartsWith("https://apis-tem.usps.com/"),
						uri.AbsoluteUri);
					Assert.IsFalse(HqHosts.IsBlocked(uri));
				}
			}
			Assert.AreEqual("https://apis.usps.com/oauth2/v3/token",UspsApiClient.GetTokenUri(UspsEnvironment.Production).AbsoluteUri);
			Assert.AreEqual("https://apis-tem.usps.com/oauth2/v3/token",UspsApiClient.GetTokenUri(UspsEnvironment.Testing).AbsoluteUri);
			StringAssert.StartsWith(UspsApiClient.GetAddressUri(UspsEnvironment.Testing,query).AbsoluteUri,"https://apis-tem.usps.com/addresses/v3/address?");
		}

		[TestMethod]
		public void Environment_ParsesLeniently() {
			Assert.AreEqual(UspsEnvironment.Testing,UspsAddresses.ParseEnvironment("testing"));
			Assert.AreEqual(UspsEnvironment.Production,UspsAddresses.ParseEnvironment(""));
			Assert.AreEqual(UspsEnvironment.Production,UspsAddresses.ParseEnvironment(null));
			Assert.AreEqual(UspsEnvironment.Production,UspsAddresses.ParseEnvironment("Production"));
		}
		#endregion Hosts

		#region Token and address calls
		[TestMethod]
		public void GetAddress_HappyPath_SendsClientCredentialsThenBearer() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TokenJson("tok-1",28799))
				.Enqueue(HttpStatusCode.OK,JSON_ADDRESS_SAMPLE);
			UspsApiClient uspsApiClient=MakeClient(fakeHttpHandler);
			USPSAddressValidationResponse response=Run(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
			Assert.AreEqual("20007",response.address.ZIPCode);
			List<RecordedRequest> listRequests=fakeHttpHandler.Requests;
			Assert.AreEqual(2,listRequests.Count);
			RecordedRequest requestToken=listRequests[0];
			Assert.AreEqual(HttpMethod.Post,requestToken.Method);
			Assert.AreEqual("https://apis.usps.com/oauth2/v3/token",requestToken.RequestUri.AbsoluteUri);
			Assert.AreEqual("application/x-www-form-urlencoded",requestToken.ContentType);
			Dictionary<string,string> dictForm=ParseQuery(requestToken.Body.Replace("+","%20"));
			Assert.AreEqual("client_credentials",dictForm["grant_type"]);
			Assert.AreEqual("addresses",dictForm["scope"]);
			Assert.AreEqual("test-key",dictForm["client_id"]);
			Assert.AreEqual("test-secret",dictForm["client_secret"]);
			RecordedRequest requestAddress=listRequests[1];
			Assert.AreEqual(HttpMethod.Get,requestAddress.Method);
			StringAssert.StartsWith(requestAddress.RequestUri.AbsoluteUri,"https://apis.usps.com/addresses/v3/address?");
			Assert.AreEqual("Bearer tok-1",requestAddress.GetHeader("Authorization"));
			StringAssert.Contains(requestAddress.GetHeader("Accept"),"application/json");
			StringAssert.StartsWith(requestAddress.GetHeader("User-Agent"),ProductIdentity.ProductToken);
		}

		[TestMethod]
		public void Token_ExpiresInAsStringOrNumber_BothParse() {
			foreach(object expiresIn in new object[] { "28799",28799 }) {
				_nowUtc=_dateTimeStart;
				FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().Enqueue(HttpStatusCode.OK,TokenJson("tok",expiresIn));
				UspsApiClient uspsApiClient=MakeClient(fakeHttpHandler);
				OAuthAccessToken token=Run(() => uspsApiClient.GetAccessTokenAsync(MakeConfig(),CancellationToken.None));
				Assert.AreEqual("tok",token.Value);
				Assert.AreEqual(_dateTimeStart.AddSeconds(28799),token.ExpiresUtc,expiresIn.GetType().Name);
				Assert.AreEqual("addresses",token.Scope);
			}
		}

		[TestMethod]
		public void Token_IsReusedWithinItsLifetime() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TokenJson("tok-1",28799))
				.SetDefault((request,ct) => Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK,JSON_ADDRESS_SAMPLE)));
			UspsApiClient uspsApiClient=MakeClient(fakeHttpHandler);
			for(int i=0;i<5;i++) {
				Run(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
				_nowUtc=_nowUtc.AddHours(1);
			}
			Assert.AreEqual(1,fakeHttpHandler.Requests.Count(x => x.RequestUri.AbsolutePath.EndsWith("/token")),"One token for five checks.");
			Assert.AreEqual(5,fakeHttpHandler.Requests.Count(x => x.RequestUri.AbsolutePath.EndsWith("/address")));
		}

		[TestMethod]
		public void Token_IsRenewedFiveMinutesBeforeExpiry() {
			int countTokens=0;
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().SetDefault((request,ct) => {
				if(request.RequestUri.AbsolutePath.EndsWith("/token")) {
					countTokens++;
					return Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK,TokenJson("tok-"+countTokens,28800)));
				}
				return Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK,JSON_ADDRESS_SAMPLE));
			});
			UspsApiClient uspsApiClient=MakeClient(fakeHttpHandler);
			Run(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
			_nowUtc=_dateTimeStart.AddHours(8).AddMinutes(-6);
			Run(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
			Assert.AreEqual(1,countTokens,"Six minutes left: the token is still used.");
			_nowUtc=_dateTimeStart.AddHours(8).AddMinutes(-4);
			Run(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
			Assert.AreEqual(2,countTokens,"Four minutes left: a new token.");
			Assert.AreEqual("Bearer tok-2",fakeHttpHandler.Requests.Last().GetHeader("Authorization"));
		}

		[TestMethod]
		public void Token_ChangedSettings_GetANewToken() {
			int countTokens=0;
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().SetDefault((request,ct) => {
				if(request.RequestUri.AbsolutePath.EndsWith("/token")) {
					countTokens++;
					return Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK,TokenJson("tok-"+countTokens,28799)));
				}
				return Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.OK,JSON_ADDRESS_SAMPLE));
			});
			UspsApiClient uspsApiClient=MakeClient(fakeHttpHandler);
			UspsConfig uspsConfigA=MakeConfig();
			uspsConfigA.Fingerprint=TokenCache.Fingerprint("1","0","test-key","osd1$a","Production");
			UspsConfig uspsConfigB=MakeConfig(secret:"new-secret");
			uspsConfigB.Fingerprint=TokenCache.Fingerprint("1","0","test-key","osd1$b","Production");
			Run(() => uspsApiClient.GetAddressAsync(uspsConfigA,MakeQuery(),CancellationToken.None));
			Run(() => uspsApiClient.GetAddressAsync(uspsConfigA,MakeQuery(),CancellationToken.None));
			Assert.AreEqual(1,countTokens);
			Run(() => uspsApiClient.GetAddressAsync(uspsConfigB,MakeQuery(),CancellationToken.None));
			Assert.AreEqual(2,countTokens,"A changed secret drops the old token.");
			Assert.AreEqual("Bearer tok-2",fakeHttpHandler.Requests.Last().GetHeader("Authorization"));
			Run(() => uspsApiClient.GetAddressAsync(MakeConfig(uspsEnvironment:UspsEnvironment.Testing),MakeQuery(),CancellationToken.None));
			Assert.AreEqual(3,countTokens,"Another environment gets its own token.");
			StringAssert.StartsWith(fakeHttpHandler.Requests.Last().RequestUri.AbsoluteUri,"https://apis-tem.usps.com/");
		}

		[TestMethod]
		public void Address401_GetsOneNewTokenAndOneRetry() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TokenJson("tok-1",28799))
				.Enqueue(HttpStatusCode.Unauthorized,"")
				.Enqueue(HttpStatusCode.OK,TokenJson("tok-2",28799))
				.Enqueue(HttpStatusCode.OK,JSON_ADDRESS_SAMPLE);
			UspsApiClient uspsApiClient=MakeClient(fakeHttpHandler);
			USPSAddressValidationResponse response=Run(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
			Assert.AreEqual("3704",response.address.ZIPPlus4);
			List<RecordedRequest> listRequests=fakeHttpHandler.Requests;
			Assert.AreEqual(4,listRequests.Count);
			Assert.AreEqual("Bearer tok-2",listRequests[3].GetHeader("Authorization"));
		}

		[TestMethod]
		public void Address401Twice_IsACredentialsError() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TokenJson("tok-1",28799))
				.Enqueue(HttpStatusCode.Unauthorized,"")
				.Enqueue(HttpStatusCode.OK,TokenJson("tok-2",28799))
				.Enqueue(HttpStatusCode.Unauthorized,"");
			UspsApiClient uspsApiClient=MakeClient(fakeHttpHandler);
			UspsApiException ex=RunExpectingError(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
			Assert.AreEqual(UspsErrorKind.Credentials,ex.Kind);
			Assert.AreEqual(4,fakeHttpHandler.Requests.Count,"Exactly one retry.");
		}

		[TestMethod]
		public void TokenRejected_IsCredentials_AndNotRetriedUntilReset() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.Unauthorized,"{\"error\":\"invalid_client\",\"error_description\":\"Client credentials are invalid.\"}");
			UspsApiClient uspsApiClient=MakeClient(fakeHttpHandler);
			UspsApiException ex=RunExpectingError(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
			Assert.AreEqual(UspsErrorKind.Credentials,ex.Kind);
			StringAssert.Contains(ex.Message,"invalid_client");
			Assert.IsFalse(ex.Message.Contains("test-secret"));
			ex=RunExpectingError(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
			Assert.AreEqual(UspsErrorKind.Credentials,ex.Kind);
			Assert.AreEqual(1,fakeHttpHandler.Requests.Count,"A rejected configuration fails at once, without another billed call.");
			uspsApiClient.Reset(MakeConfig());
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,TokenJson("tok-ok",28799)).Enqueue(HttpStatusCode.OK,JSON_ADDRESS_SAMPLE);
			Run(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
			Assert.AreEqual(3,fakeHttpHandler.Requests.Count);
		}
		#endregion Token and address calls

		#region Errors
		[TestMethod]
		public void Address404_ReturnsNoAddressWithUspsMessage() {
			foreach(string message in new[] { "Address Not Found.","Invalid State Code.","Invalid City.","Insufficient Address Data.",
				"More than one address was found matching the information entered.","Invalid Delivery Address." })
			{
				FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
					.Enqueue(HttpStatusCode.OK,TokenJson("tok",28799))
					.Enqueue(HttpStatusCode.NotFound,"{\"apiVersion\":\"3.0\",\"error\":{\"code\":\"404\",\"message\":\""+message+"\",\"errors\":[]}}");
				UspsApiClient uspsApiClient=MakeClient(fakeHttpHandler);
				USPSAddressValidationResponse response=Run(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
				Assert.IsNull(response.address,message);
				Assert.AreEqual(message,response.ErrorMessage);
			}
		}

		[TestMethod]
		public void Address404_WithDetailOnly_UsesTheDetail() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TokenJson("tok",28799))
				.Enqueue(HttpStatusCode.NotFound,"{\"error\":{\"code\":\"404\",\"errors\":[{\"title\":\"Not found\",\"detail\":\"No match.\"}]}}");
			USPSAddressValidationResponse response=Run(() => MakeClient(fakeHttpHandler).GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
			Assert.IsNull(response.address);
			Assert.AreEqual("No match.",response.ErrorMessage);
		}

		[TestMethod]
		public void AddressErrors_MapToTheirKinds() {
			Dictionary<HttpStatusCode,UspsErrorKind> dictCases=new Dictionary<HttpStatusCode,UspsErrorKind> {
				{ HttpStatusCode.BadRequest,UspsErrorKind.BadRequest },
				{ HttpStatusCode.Forbidden,UspsErrorKind.Forbidden },
				{ HttpStatusCode.ServiceUnavailable,UspsErrorKind.Offline },
				{ HttpStatusCode.InternalServerError,UspsErrorKind.Offline },
				{ HttpStatusCode.BadGateway,UspsErrorKind.Offline },
			};
			foreach(KeyValuePair<HttpStatusCode,UspsErrorKind> pair in dictCases) {
				FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
					.Enqueue(HttpStatusCode.OK,TokenJson("tok",28799))
					.Enqueue(pair.Key,"{\"apiVersion\":\"3.0\",\"error\":{\"code\":\""+(int)pair.Key+"\",\"message\":\"Problem text.\"}}");
				List<string> listLog=new List<string>();
				UspsApiClient uspsApiClient=MakeClient(fakeHttpHandler,listLog);
				UspsApiException ex=RunExpectingError(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
				Assert.AreEqual(pair.Value,ex.Kind,pair.Key.ToString());
				Assert.AreEqual((int)pair.Key,ex.HttpStatus);
				Assert.AreEqual("Problem text.",ex.UspsMessage);
				Assert.AreEqual(2,fakeHttpHandler.Requests.Count,pair.Key+": never retried automatically (every lookup is billed).");
				Assert.IsTrue(listLog.Count>0);
				Assert.IsFalse(listLog.Any(x => x.Contains("3120") || x.Contains("tok") || x.Contains("test-secret") || x.Contains("?")),
					"Log lines carry no address, token, secret or query.");
			}
			Assert.IsFalse(UspsAddresses.IsQuotaCoolingDown(_nowUtc),"Only a 429 starts the cool-down.");
		}

		[TestMethod]
		public void Address429_IsQuota_WithRetryAfter_AndStartsTheCooldown() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TokenJson("tok",28799))
				.Enqueue((HttpStatusCode)429,"{\"error\":{\"code\":\"429\",\"message\":\"Quota exceeded.\"}}",
					actionCustomize:x => x.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromSeconds(1800)));
			UspsApiClient uspsApiClient=MakeClient(fakeHttpHandler);
			UspsApiException ex=RunExpectingError(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
			Assert.AreEqual(UspsErrorKind.Quota,ex.Kind);
			Assert.AreEqual(TimeSpan.FromSeconds(1800),ex.RetryAfter);
			Assert.AreEqual(2,fakeHttpHandler.Requests.Count,"No automatic retry after 429.");
			Assert.IsTrue(UspsAddresses.IsQuotaCoolingDown(_nowUtc.AddMinutes(1)));
			Assert.IsFalse(UspsAddresses.IsQuotaCoolingDown(_nowUtc.AddMinutes(2).AddSeconds(1)));
		}

		[TestMethod]
		public void Token429And503_AreQuotaAndOffline_NotARejection() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue((HttpStatusCode)429,"")
				.Enqueue(HttpStatusCode.ServiceUnavailable,"")
				.Enqueue(HttpStatusCode.OK,TokenJson("tok",28799))
				.Enqueue(HttpStatusCode.OK,JSON_ADDRESS_SAMPLE);
			UspsApiClient uspsApiClient=MakeClient(fakeHttpHandler);
			Assert.AreEqual(UspsErrorKind.Quota,
				RunExpectingError(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None)).Kind);
			Assert.AreEqual(UspsErrorKind.Offline,
				RunExpectingError(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None)).Kind);
			//Neither marked the configuration as rejected: the next call works.
			Run(() => uspsApiClient.GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
		}

		[TestMethod]
		public void NetworkFailureAndTimeout_AreOffline() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler().EnqueueNetworkFailure();
			UspsApiException ex=RunExpectingError(() => MakeClient(fakeHttpHandler).GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
			Assert.AreEqual(UspsErrorKind.Offline,ex.Kind);
			Assert.AreEqual(1,fakeHttpHandler.Requests.Count,"No retry.");
			fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TokenJson("tok",28799))
				.Enqueue((request,ct) => throw new TaskCanceledException("Simulated timeout."));
			ex=RunExpectingError(() => MakeClient(fakeHttpHandler).GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
			Assert.AreEqual(UspsErrorKind.Offline,ex.Kind);
			Assert.AreEqual(2,fakeHttpHandler.Requests.Count,"No retry.");
		}

		[TestMethod]
		public void UnreadableOkAnswer_IsOffline() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler()
				.Enqueue(HttpStatusCode.OK,TokenJson("tok",28799))
				.Enqueue(HttpStatusCode.OK,"<html></html>","text/html");
			UspsApiException ex=RunExpectingError(() => MakeClient(fakeHttpHandler).GetAddressAsync(MakeConfig(),MakeQuery(),CancellationToken.None));
			Assert.AreEqual(UspsErrorKind.Offline,ex.Kind);
		}

		[TestMethod]
		public void MissingKeyOrSecret_MakesNoCall() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			UspsApiClient uspsApiClient=MakeClient(fakeHttpHandler);
			Assert.AreEqual(UspsErrorKind.NotConfigured,
				RunExpectingError(() => uspsApiClient.GetAddressAsync(MakeConfig(key:""),MakeQuery(),CancellationToken.None)).Kind);
			Assert.AreEqual(UspsErrorKind.NotConfigured,
				RunExpectingError(() => uspsApiClient.GetAddressAsync(MakeConfig(secret:""),MakeQuery(),CancellationToken.None)).Kind);
			Assert.AreEqual(0,fakeHttpHandler.Requests.Count);
		}
		#endregion Errors
	}
}
