// Behavioural tests for the EMAIL-UI package's Gmail and Microsoft 365 retry and download loops (docs/specs/vendor-direct/README.md 3.1,
// google.md 6.4, microsoft-365.md 6.3), run against a fake HttpMessageHandler under Google's client library and the Graph client. They count
// the requests that reach the vendor: a refused token is refreshed once and the message resent once, a send that got a 5xx or timed out is
// never resent (a resend could mail a patient twice), rate limits back off a bounded number of times, and Gmail's sending limit stops the rest
// of a batch without a request. No database, no network and no real waits. Licensed under the GNU General Public License, version 2 or later.
// See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.Email;
using OpenDentBusiness.Microsoft365;

namespace VendorDirectTests.Email {
	//OpenDentBusiness.Task (the task list table type) would hide System.Threading.Tasks.Task otherwise.
	using Task=System.Threading.Tasks.Task;

	[TestClass]
	public class EmailOAuthLoopTests {
		private const string MAILBOX="frontdesk@example-dental.test";
		private const string TOKEN_1="synthetic-access-token-1";
		private const string TOKEN_2="synthetic-access-token-2";
		private const long PROGRAM_NUM_MICROSOFT=7101;
		private FakeHttpHandler _handler;
		///<summary>forceRefresh of every token request the loops made, in order.</summary>
		private List<bool> _listTokenRequests;
		private List<TimeSpan> _listSleeps;

		[TestInitialize]
		public void TestInitialize() {
			_handler=new FakeHttpHandler();
			_listTokenRequests=new List<bool>();
			_listSleeps=new List<TimeSpan>();
			EmailMessages.ClearGmailSendingLimits();
			EmailMessages.FuncGetAccessTokenForTests=(emailAddress,forceRefresh) => {
				lock(_listTokenRequests) {
					_listTokenRequests.Add(forceRefresh);
				}
				return forceRefresh ? TOKEN_2 : TOKEN_1;
			};
			EmailMessages.ActionSleepForTests=timeSpan => {
				lock(_listSleeps) {
					_listSleeps.Add(timeSpan);
				}
			};
			GoogleApiConnector.HttpMessageHandlerForTests=_handler;
		}

		[TestCleanup]
		public void TestCleanup() {
			EmailMessages.FuncGetAccessTokenForTests=null;
			EmailMessages.ActionSleepForTests=null;
			GoogleApiConnector.HttpMessageHandlerForTests=null;
			MicrosoftApiConnector.GraphClientForTests=null;
			EmailMessages.ClearGmailSendingLimits();
			Programs.ClearCache();
		}

		#region Helpers
		private static EmailAddress MakeAddress(OAuthType oAuthType,long emailAddressNum=4101) {
			EmailAddress emailAddress=new EmailAddress();
			emailAddress.EmailAddressNum=emailAddressNum;
			emailAddress.EmailUsername=MAILBOX;
			emailAddress.EmailPassword="";
			emailAddress.SMTPserver="";
			emailAddress.Pop3ServerIncoming="";
			emailAddress.QueryString="";
			emailAddress.AuthenticationType=oAuthType;
			emailAddress.AccessToken="";
			emailAddress.RefreshToken="";
			emailAddress.DownloadInbox=true;
			return emailAddress;
		}

		private static BasicEmailMessage MakeMessage(string bodyText="Your appointment is on Tuesday at 9:00.") {
			BasicEmailMessage basicEmailMessage=new BasicEmailMessage();
			basicEmailMessage.FromAddress=MAILBOX;
			basicEmailMessage.ToAddress="patient@example.test";
			basicEmailMessage.CcAddress="";
			basicEmailMessage.BccAddress="";
			basicEmailMessage.Subject="Appointment reminder";
			basicEmailMessage.BodyText=bodyText;
			basicEmailMessage.IsHtml=false;
			return basicEmailMessage;
		}

		private static void SendGmail(EmailAddress emailAddress,BasicEmailMessage basicEmailMessage=null) {
			EmailMessages.SendEmailGmail(emailAddress,EmailMessages.ODEmailAddressToBasic(emailAddress),basicEmailMessage??MakeMessage(),null,
				new AlternateView[0]);
		}

		private static void SendMicrosoft(EmailAddress emailAddress) {
			EmailMessages.SendEmailMicrosoft(emailAddress,EmailMessages.ODEmailAddressToBasic(emailAddress),MakeMessage(),null,new AlternateView[0]);
		}

		///<summary>A Google API error body, as Gmail sends it.</summary>
		private static string GoogleError(int code,string reason,string message) {
			return "{\"error\":{\"code\":"+code+",\"message\":\""+message+"\",\"errors\":[{\"message\":\""+message+"\",\"domain\":\"global\","
				+"\"reason\":\""+reason+"\"}],\"status\":\"ERROR\"}}";
		}

		private FakeHttpHandler EnqueueGoogleError(HttpStatusCode statusCode,string reason,string message) {
			return _handler.Enqueue(statusCode,GoogleError((int)statusCode,reason,message));
		}

		private FakeHttpHandler EnqueueGmailSent(string id="18f0a1b2c3d4e5f6") {
			return _handler.Enqueue(HttpStatusCode.OK,"{\"id\":\""+id+"\",\"threadId\":\""+id+"\",\"labelIds\":[\"SENT\"]}");
		}

		private FakeHttpHandler EnqueueListPage(string nextPageToken,params string[] arrayIds) {
			string messages=string.Join(",",arrayIds.Select(x => "{\"id\":\""+x+"\",\"threadId\":\""+x+"\"}"));
			string next=string.IsNullOrEmpty(nextPageToken) ? "" : ",\"nextPageToken\":\""+nextPageToken+"\"";
			return _handler.Enqueue(HttpStatusCode.OK,"{\"messages\":["+messages+"]"+next+",\"resultSizeEstimate\":"+arrayIds.Length+"}");
		}

		private FakeHttpHandler EnqueueRawMessage(string id) {
			return _handler.Enqueue(HttpStatusCode.OK,"{\"id\":\""+id+"\",\"threadId\":\""+id+"\",\"raw\":\""+RawFor(id)+"\"}");
		}

		///<summary>A message's "raw" field: base64url without padding, as Gmail sends it.</summary>
		private static string RawFor(string id) {
			string mime="From: Patient <patient@example.test>\r\nTo: "+MAILBOX+"\r\nSubject: "+id+"\r\nMessage-ID: <"+id+"@example.test>\r\n\r\nHello.\r\n";
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(mime)).TrimEnd('=').Replace('+','-').Replace('/','_');
		}

		private List<RecordedRequest> GetSendRequests() {
			return _handler.Requests.Where(x => x.Method==HttpMethod.Post && x.RequestUri.AbsolutePath.EndsWith("/messages/send")).ToList();
		}

		private static bool IsListRequest(RecordedRequest recordedRequest) {
			return recordedRequest.Method==HttpMethod.Get && recordedRequest.RequestUri.AbsolutePath.EndsWith("/messages");
		}

		private static string GetQueryValue(RecordedRequest recordedRequest,string name) {
			foreach(string pair in recordedRequest.RequestUri.Query.TrimStart('?').Split('&')) {
				int index=pair.IndexOf('=');
				if(index>0 && pair.Substring(0,index)==name) {
					return Uri.UnescapeDataString(pair.Substring(index+1).Replace('+',' '));
				}
			}
			return null;
		}

		///<summary>Download with the database side replaced: storedUids are already downloaded; imported IDs are recorded.</summary>
		private static int Download(EmailAddress emailAddress,List<string> listStoredUids,List<string> listImported) {
			return EmailMessages.DownloadGmailInbox(emailAddress,() => listStoredUids,(messageId,raw) => {
				Assert.AreEqual(RawFor(messageId),raw,"The message's raw field reaches the import as Gmail sent it.");
				listImported.Add(messageId);
				return true;
			});
		}

		///<summary>Turns the Microsoft 365 program link on in the cache and sends Graph calls to the fake handler, with no real waits.</summary>
		private List<TimeSpan> UseMicrosoftFakeGraph() {
			Program program=new Program();
			program.ProgramNum=PROGRAM_NUM_MICROSOFT;
			program.ProgName=ProgramName.Microsoft365Email.ToString();
			program.ProgDesc="Microsoft 365 email";
			program.Enabled=true;
			program.Path="";
			program.CommandLine="";
			program.Note="";
			program.PluginDllName="";
			program.ButtonImage="";
			program.FileTemplate="";
			program.FilePath="";
			program.CustErr="";
			Programs.FillCacheFromTable(OpenDentBusiness.Crud.ProgramCrud.ListToTable(new List<Program> { program },"Program"));
			List<TimeSpan> listDelays=new List<TimeSpan>();
			Microsoft365GraphOptions options=new Microsoft365GraphOptions();
			options.HttpMessageHandler=_handler;
			options.Delay=(timeSpan,cancellationToken) => {
				lock(listDelays) {
					listDelays.Add(timeSpan);
				}
				return Task.CompletedTask;
			};
			options.Log=line => { };
			MicrosoftApiConnector.GraphClientForTests=new Microsoft365GraphClient(options);
			return listDelays;
		}

		///<summary>True if ex or an inner exception has the type name (the tests don't reference Google's library).</summary>
		private static bool HasInner(Exception ex,string typeName) {
			for(Exception exCur=ex;exCur!=null;exCur=exCur.InnerException) {
				if(exCur.GetType().Name==typeName) {
					return true;
				}
			}
			return false;
		}

		private static string GraphError(string code) {
			return "{\"error\":{\"code\":\""+code+"\",\"message\":\"Synthetic error.\"}}";
		}

		private List<RecordedRequest> GetSendMailRequests() {
			return _handler.Requests.Where(x => x.Method==HttpMethod.Post && x.RequestUri.AbsolutePath.EndsWith("/me/sendMail")).ToList();
		}
		#endregion Helpers

		#region Gmail send
		[TestMethod]
		public void Gmail_Send_401RefreshesOnceAndResendsOnce() {
			EnqueueGoogleError(HttpStatusCode.Unauthorized,"authError","Request had invalid authentication credentials.");
			EnqueueGmailSent();
			SendGmail(MakeAddress(OAuthType.Google));
			List<RecordedRequest> listSends=GetSendRequests();
			Assert.AreEqual(2,listSends.Count,"One send, refused; then one resend with the new token.");
			Assert.AreEqual(2,_handler.Requests.Count,"No getProfile probe or anything else.");
			Assert.AreEqual("Bearer "+TOKEN_1,listSends[0].GetHeader("Authorization"));
			Assert.AreEqual("Bearer "+TOKEN_2,listSends[1].GetHeader("Authorization"));
			CollectionAssert.AreEqual(new List<bool> { false,true },_listTokenRequests,"Exactly one forced refresh.");
			Assert.AreEqual(0,_listSleeps.Count);
		}

		[TestMethod]
		public void Gmail_Send_A401AfterTheRefreshIsNotResentAgain() {
			EnqueueGoogleError(HttpStatusCode.Unauthorized,"authError","Request had invalid authentication credentials.");
			EnqueueGoogleError(HttpStatusCode.Unauthorized,"authError","Request had invalid authentication credentials.");
			EnqueueGmailSent();
			ODException ex=AssertEx.Throws<ODException>(() => SendGmail(MakeAddress(OAuthType.Google)));
			Assert.AreEqual(2,GetSendRequests().Count);
			CollectionAssert.AreEqual(new List<bool> { false,true },_listTokenRequests);
			StringAssert.Contains(ex.Message,"needs to be authorized");
		}

		[TestMethod]
		public void Gmail_Send_ServerErrorIsNeverResent() {
			foreach(HttpStatusCode statusCode in new[] { HttpStatusCode.InternalServerError,HttpStatusCode.ServiceUnavailable }) {
				_handler=new FakeHttpHandler();
				GoogleApiConnector.HttpMessageHandlerForTests=_handler;
				_listTokenRequests.Clear();
				EnqueueGoogleError(statusCode,"backendError","Backend Error");
				EnqueueGmailSent();//Must never be reached: the first send may have been accepted.
				ODException ex=AssertEx.Throws<ODException>(() => SendGmail(MakeAddress(OAuthType.Google)));
				Assert.AreEqual(1,GetSendRequests().Count,(int)statusCode+": a resend could mail the patient twice (Google's own 503 retry included).");
				Assert.AreEqual(1,_handler.Requests.Count,(int)statusCode+"");
				Assert.AreEqual(GmailApiErrors.GetMessage(GmailErrorKind.ServerError,MAILBOX),ex.Message);
				Assert.IsTrue(HasInner(ex,"GoogleApiException"),"The Gmail error is kept as the inner exception.");
				CollectionAssert.AreEqual(new List<bool> { false },_listTokenRequests);
				Assert.AreEqual(0,_listSleeps.Count);
			}
		}

		[TestMethod]
		public void Gmail_Send_RateLimitBacksOffThenSends() {
			EnqueueGoogleError((HttpStatusCode)429,"rateLimitExceeded","Too many concurrent requests for user.");
			EnqueueGmailSent();
			SendGmail(MakeAddress(OAuthType.Google));
			Assert.AreEqual(2,GetSendRequests().Count,"A rate-limited send was refused, so one retry is safe.");
			Assert.AreEqual(1,_listSleeps.Count);
			Assert.IsTrue(_listSleeps[0]>=TimeSpan.FromSeconds(1) && _listSleeps[0]<TimeSpan.FromSeconds(2),"The first backoff is 1 s plus jitter.");
			CollectionAssert.AreEqual(new List<bool> { false,false },_listTokenRequests,"A rate limit doesn't refresh the token.");
		}

		[TestMethod]
		public void Gmail_Send_RateLimitBacksOffAtMostTheMaximum() {
			_handler.SetDefault((request,ct) => Task.FromResult(FakeHttpHandler.MakeResponse((HttpStatusCode)429,
				GoogleError(429,"rateLimitExceeded","Too many concurrent requests for user."))));
			ODException ex=AssertEx.Throws<ODException>(() => SendGmail(MakeAddress(OAuthType.Google)));
			Assert.AreEqual(1+EmailMessages.GmailSendMaxBackoffRetries,GetSendRequests().Count);
			Assert.AreEqual(EmailMessages.GmailSendMaxBackoffRetries,_listSleeps.Count);
			Assert.AreEqual(GmailApiErrors.GetMessage(GmailErrorKind.RateLimited,MAILBOX),ex.Message);
		}

		[TestMethod]
		public void Gmail_Send_SendingLimitStopsTheRestOfTheBatchWithoutARequest() {
			string retryAfter=DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'",System.Globalization.CultureInfo.InvariantCulture);
			EnqueueGoogleError((HttpStatusCode)429,"rateLimitExceeded","User-rate limit exceeded.  Retry after "+retryAfter);
			EnqueueGmailSent();
			EmailAddress emailAddress=MakeAddress(OAuthType.Google,4102);
			ODException ex=AssertEx.Throws<ODException>(() => SendGmail(emailAddress));
			Assert.AreEqual(GmailApiErrors.GetMessage(GmailErrorKind.SendingLimit,MAILBOX),ex.Message);
			Assert.AreEqual(1,_handler.Requests.Count,"The sending limit is never retried.");
			Assert.AreEqual(0,_listSleeps.Count);
			DateTime dateTimeUntilUtc;
			Assert.IsTrue(EmailMessages.IsGmailSendingLimited(emailAddress.EmailAddressNum,DateTime.UtcNow,out dateTimeUntilUtc));
			//The next message of the batch fails at once: no token request and no HTTP request.
			int countTokenRequests=_listTokenRequests.Count;
			ODException exNext=AssertEx.Throws<ODException>(() => SendGmail(emailAddress));
			Assert.AreEqual(GmailApiErrors.GetMessage(GmailErrorKind.SendingLimit,MAILBOX),exNext.Message);
			Assert.AreEqual(1,_handler.Requests.Count);
			Assert.AreEqual(countTokenRequests,_listTokenRequests.Count);
			//Another address of the office still sends.
			SendGmail(MakeAddress(OAuthType.Google,4103));
			Assert.AreEqual(2,_handler.Requests.Count);
		}

		[TestMethod]
		public void Gmail_Send_SmallMessageGoesInTheJsonBody() {
			EnqueueGmailSent();
			SendGmail(MakeAddress(OAuthType.Google));
			RecordedRequest recordedRequest=_handler.Requests.Single();
			Assert.AreEqual(HttpMethod.Post,recordedRequest.Method);
			Assert.IsFalse(recordedRequest.RequestUri.AbsolutePath.Contains("/upload/"),recordedRequest.RequestUri.ToString());
			StringAssert.EndsWith(recordedRequest.RequestUri.AbsolutePath,"/messages/send");
			Assert.AreEqual("application/json",recordedRequest.ContentType);
		}

		[TestMethod]
		public void Gmail_Send_MessageOverTheThresholdGoesToTheUploadUri() {
			StringBuilder stringBuilder=new StringBuilder();
			string line=new string('x',70)+"\r\n";
			while(stringBuilder.Length<=SendEmail.GMAIL_UPLOAD_THRESHOLD_BYTES) {
				stringBuilder.Append(line);
			}
			_handler.Enqueue(HttpStatusCode.OK,"",actionCustomize:response => response.Headers.Location=
				new Uri("https://gmail.googleapis.com/upload/gmail/v1/users/me/messages/send?uploadType=resumable&upload_id=synthetic-upload"));
			EnqueueGmailSent("18f0a1b2c3d4e5f7");
			SendGmail(MakeAddress(OAuthType.Google),MakeMessage(stringBuilder.ToString()));
			List<RecordedRequest> listRequests=_handler.Requests;
			Assert.AreEqual(2,listRequests.Count,"Start the resumable upload, then send the bytes in one chunk.");
			RecordedRequest recordedRequestStart=listRequests[0];
			Assert.AreEqual(HttpMethod.Post,recordedRequestStart.Method);
			StringAssert.Contains(recordedRequestStart.RequestUri.AbsolutePath,"/upload/gmail/v1/users/");
			StringAssert.EndsWith(recordedRequestStart.RequestUri.AbsolutePath,"/messages/send");
			Assert.AreEqual("resumable",GetQueryValue(recordedRequestStart,"uploadType"));
			Assert.AreEqual("message/rfc822",recordedRequestStart.GetHeader("X-Upload-Content-Type"));
			Assert.AreEqual("Bearer "+TOKEN_1,recordedRequestStart.GetHeader("Authorization"));
			RecordedRequest recordedRequestBytes=listRequests[1];
			Assert.AreEqual("synthetic-upload",GetQueryValue(recordedRequestBytes,"upload_id"));
			Assert.IsTrue(recordedRequestBytes.Body.Length>SendEmail.GMAIL_UPLOAD_THRESHOLD_BYTES,"The raw MIME bytes, not base64 JSON.");
			StringAssert.Contains(recordedRequestBytes.Body,"Subject: Appointment reminder");
		}
		#endregion Gmail send

		#region Gmail download
		[TestMethod]
		public void Gmail_Download_PagesThroughEveryListPageAndSkipsStoredIds() {
			EnqueueListPage("page-2","msgA1","msgA2");
			EnqueueListPage("page-3","msgA3");
			EnqueueListPage(null,"msgA4","msgA2");
			EnqueueRawMessage("msgA1");
			EnqueueRawMessage("msgA4");
			List<string> listImported=new List<string>();
			int countNew=Download(MakeAddress(OAuthType.Google),new List<string> { "msgA2","GmailIdmsgA3" },listImported);
			Assert.AreEqual(2,countNew);
			CollectionAssert.AreEqual(new List<string> { "msgA1","msgA4" },listImported,"Stored IDs (legacy prefix included) and repeats are skipped.");
			List<RecordedRequest> listRequests=_handler.Requests;
			Assert.AreEqual(5,listRequests.Count);
			List<RecordedRequest> listLists=listRequests.Take(3).ToList();
			Assert.IsTrue(listLists.All(IsListRequest));
			Assert.IsNull(GetQueryValue(listLists[0],"pageToken"));
			Assert.AreEqual("page-2",GetQueryValue(listLists[1],"pageToken"));
			Assert.AreEqual("page-3",GetQueryValue(listLists[2],"pageToken"));
			foreach(RecordedRequest recordedRequest in listLists) {
				Assert.AreEqual("500",GetQueryValue(recordedRequest,"maxResults"));
				Assert.AreNotEqual("true",GetQueryValue(recordedRequest,"includeSpamTrash"),"Spam and trash are left out.");
				Assert.AreEqual(GmailInbox.DefaultQuery,GetQueryValue(recordedRequest,"q"),"A blank query lists the last 30 days, not the whole mailbox.");
			}
			StringAssert.EndsWith(listRequests[3].RequestUri.AbsolutePath,"/messages/msgA1");
			StringAssert.EndsWith(listRequests[4].RequestUri.AbsolutePath,"/messages/msgA4");
			Assert.AreEqual("raw",GetQueryValue(listRequests[3],"format"));
			CollectionAssert.AreEqual(new List<bool> { false },_listTokenRequests);
		}

		[TestMethod]
		public void Gmail_Download_ListRateLimitBacksOff() {
			EnqueueGoogleError((HttpStatusCode)429,"rateLimitExceeded","Too many concurrent requests for user.");
			EnqueueGoogleError(HttpStatusCode.Forbidden,"userRateLimitExceeded","User Rate Limit Exceeded");
			EnqueueListPage(null,"msgB1");
			EnqueueRawMessage("msgB1");
			List<string> listImported=new List<string>();
			Assert.AreEqual(1,Download(MakeAddress(OAuthType.Google),new List<string>(),listImported));
			Assert.AreEqual(4,_handler.Requests.Count);
			Assert.AreEqual(3,_handler.Requests.Count(IsListRequest));
			Assert.AreEqual(2,_listSleeps.Count);
			Assert.IsTrue(_listSleeps[0]>=TimeSpan.FromSeconds(1) && _listSleeps[0]<TimeSpan.FromSeconds(2));
			Assert.IsTrue(_listSleeps[1]>=TimeSpan.FromSeconds(2) && _listSleeps[1]<TimeSpan.FromSeconds(3),"Exponential: 1 s, then 2 s.");
			CollectionAssert.AreEqual(new List<string> { "msgB1" },listImported);
		}

		[TestMethod]
		public void Gmail_Download_ListServerErrorBacksOffOnlyInTheLoop() {
			EnqueueGoogleError(HttpStatusCode.ServiceUnavailable,"backendError","Backend Error");
			EnqueueListPage(null);
			Assert.AreEqual(0,Download(MakeAddress(OAuthType.Google),new List<string>(),new List<string>()));
			Assert.AreEqual(2,_handler.Requests.Count,"One wait and one retry from the loop; Google's library adds none of its own.");
			Assert.AreEqual(1,_listSleeps.Count);
		}

		[TestMethod]
		public void Gmail_Download_ListRateLimitStopsAfterTheMaximumBackoff() {
			_handler.SetDefault((request,ct) => Task.FromResult(FakeHttpHandler.MakeResponse((HttpStatusCode)429,
				GoogleError(429,"rateLimitExceeded","Too many concurrent requests for user."))));
			ODException ex=AssertEx.Throws<ODException>(() => Download(MakeAddress(OAuthType.Google),new List<string>(),new List<string>()));
			Assert.AreEqual(1+GmailApiErrors.MaxBackoffAttempts,_handler.Requests.Count);
			Assert.AreEqual(GmailApiErrors.MaxBackoffAttempts,_listSleeps.Count);
			Assert.AreEqual(GmailApiErrors.GetMessage(GmailErrorKind.RateLimited,MAILBOX),ex.Message);
		}

		[TestMethod]
		public void Gmail_Download_401WhileListingRefreshesOnce() {
			EnqueueGoogleError(HttpStatusCode.Unauthorized,"authError","Request had invalid authentication credentials.");
			EnqueueListPage(null,"msgC1");
			EnqueueRawMessage("msgC1");
			List<string> listImported=new List<string>();
			Assert.AreEqual(1,Download(MakeAddress(OAuthType.Google),new List<string>(),listImported));
			List<RecordedRequest> listRequests=_handler.Requests;
			Assert.AreEqual(3,listRequests.Count);
			Assert.AreEqual("Bearer "+TOKEN_1,listRequests[0].GetHeader("Authorization"));
			Assert.AreEqual("Bearer "+TOKEN_2,listRequests[1].GetHeader("Authorization"));
			Assert.AreEqual("Bearer "+TOKEN_2,listRequests[2].GetHeader("Authorization"));
			CollectionAssert.AreEqual(new List<bool> { false,true },_listTokenRequests);
		}

		[TestMethod]
		public void Gmail_Download_401MidDownloadRefreshesOnceThenStops() {
			EnqueueListPage(null,"msgD1","msgD2","msgD3");
			EnqueueGoogleError(HttpStatusCode.Unauthorized,"authError","Request had invalid authentication credentials.");
			EnqueueRawMessage("msgD1");
			EnqueueGoogleError(HttpStatusCode.Unauthorized,"authError","Request had invalid authentication credentials.");
			EnqueueRawMessage("msgD2");//Never reached: a second 401 stops this poll.
			List<string> listImported=new List<string>();
			int countNew=Download(MakeAddress(OAuthType.Google),new List<string>(),listImported);
			Assert.AreEqual(1,countNew);
			CollectionAssert.AreEqual(new List<string> { "msgD1" },listImported,"msgD2 and msgD3 are left for the next poll (no ID stored).");
			List<RecordedRequest> listRequests=_handler.Requests;
			Assert.AreEqual(4,listRequests.Count);
			Assert.AreEqual("Bearer "+TOKEN_1,listRequests[1].GetHeader("Authorization"));
			Assert.AreEqual("Bearer "+TOKEN_2,listRequests[2].GetHeader("Authorization"),"msgD1 is fetched again with the refreshed token.");
			StringAssert.EndsWith(listRequests[2].RequestUri.AbsolutePath,"/messages/msgD1");
			CollectionAssert.AreEqual(new List<bool> { false,true },_listTokenRequests,"One refresh per poll.");
		}
		#endregion Gmail download

		#region Microsoft 365 send
		[TestMethod]
		public void Microsoft_Send_401RefreshesOnceAndResendsOnce() {
			UseMicrosoftFakeGraph();
			_handler.Enqueue(HttpStatusCode.Unauthorized,GraphError("InvalidAuthenticationToken"));
			_handler.Enqueue(HttpStatusCode.Accepted,"");
			SendMicrosoft(MakeAddress(OAuthType.Microsoft));
			List<RecordedRequest> listSends=GetSendMailRequests();
			Assert.AreEqual(2,listSends.Count);
			Assert.AreEqual(2,_handler.Requests.Count,"No GetProfile probe.");
			Assert.AreEqual("Bearer "+TOKEN_1,listSends[0].GetHeader("Authorization"));
			Assert.AreEqual("Bearer "+TOKEN_2,listSends[1].GetHeader("Authorization"));
			CollectionAssert.AreEqual(new List<bool> { false,true },_listTokenRequests);
		}

		[TestMethod]
		public void Microsoft_Send_A401AfterTheRefreshAsksForSignIn() {
			UseMicrosoftFakeGraph();
			_handler.SetDefault((request,ct) => Task.FromResult(FakeHttpHandler.MakeResponse(HttpStatusCode.Unauthorized,
				GraphError("InvalidAuthenticationToken"))));
			ODException ex=AssertEx.Throws<ODException>(() => SendMicrosoft(MakeAddress(OAuthType.Microsoft)));
			Assert.AreEqual(2,GetSendMailRequests().Count,"NeedsSignIn is retried once, not again.");
			CollectionAssert.AreEqual(new List<bool> { false,true },_listTokenRequests);
			Assert.AreEqual(EmailMessages.GetMicrosoftNeedsSignInMessage(MAILBOX),ex.Message);
			Assert.IsTrue(EmailMessages.FindException<Microsoft365Exception>(ex).IsAuthError,"The email window offers Sign in again.");
		}

		[TestMethod]
		public void Microsoft_Send_ServerErrorIsNeverResent() {
			UseMicrosoftFakeGraph();
			_handler.Enqueue(HttpStatusCode.ServiceUnavailable,GraphError("ServiceNotAvailable"));
			_handler.Enqueue(HttpStatusCode.Accepted,"");//Must never be reached.
			ODException ex=AssertEx.Throws<ODException>(() => SendMicrosoft(MakeAddress(OAuthType.Microsoft)));
			Assert.AreEqual(1,GetSendMailRequests().Count,"The message may be queued already; a resend could mail the patient twice.");
			Assert.AreEqual(1,_handler.Requests.Count);
			Microsoft365Exception microsoft365Exception=EmailMessages.FindException<Microsoft365Exception>(ex);
			Assert.AreEqual(Microsoft365ErrorKind.SendOutcomeUnknown,microsoft365Exception.Kind);
			StringAssert.Contains(ex.Message,"Sent Items");
			CollectionAssert.AreEqual(new List<bool> { false },_listTokenRequests);
		}

		[TestMethod]
		public void Microsoft_Send_TimeoutIsNeverResent() {
			UseMicrosoftFakeGraph();
			_handler.Enqueue((request,ct) => throw new TaskCanceledException("Simulated timeout."));
			_handler.Enqueue(HttpStatusCode.Accepted,"");//Must never be reached.
			ODException ex=AssertEx.Throws<ODException>(() => SendMicrosoft(MakeAddress(OAuthType.Microsoft)));
			Assert.AreEqual(1,GetSendMailRequests().Count);
			Assert.AreEqual(Microsoft365ErrorKind.SendOutcomeUnknown,EmailMessages.FindException<Microsoft365Exception>(ex).Kind);
			CollectionAssert.AreEqual(new List<bool> { false },_listTokenRequests);
		}

		[TestMethod]
		public void Microsoft_Send_429WaitsThenSends() {
			List<TimeSpan> listDelays=UseMicrosoftFakeGraph();
			_handler.Enqueue((HttpStatusCode)429,GraphError("ApplicationThrottled"),actionCustomize:response =>
				response.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(2)));
			_handler.Enqueue(HttpStatusCode.Accepted,"");
			SendMicrosoft(MakeAddress(OAuthType.Microsoft));
			Assert.AreEqual(2,GetSendMailRequests().Count,"A throttled send was refused, so it is sent again after Retry-After.");
			CollectionAssert.Contains(listDelays,TimeSpan.FromSeconds(2));
			CollectionAssert.AreEqual(new List<bool> { false },_listTokenRequests,"Throttling doesn't refresh the token.");
		}
		#endregion Microsoft 365 send
	}
}
