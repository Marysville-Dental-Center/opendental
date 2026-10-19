// Tests for the in-repo Microsoft Graph client against a fake HTTP handler (docs/specs/vendor-direct/microsoft-365.md, 6.3). No network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MimeKit;
using OpenDentBusiness.Microsoft365;

namespace VendorDirectTests.Microsoft365 {
	[TestClass]
	public class Microsoft365GraphClientTests {
		private const string MAILBOX="frontdesk@example-dental.test";
		private const string TOKEN_1="graph-token-one-0000000000";
		private const string TOKEN_2="graph-token-two-1111111111";

		///<summary>A Graph client over the fake handler: no waiting, a settable clock, and every log line kept.</summary>
		private sealed class Harness {
			public FakeHttpHandler Handler=new FakeHttpHandler();
			public List<TimeSpan> ListDelays=new List<TimeSpan>();
			public List<string> ListLogLines=new List<string>();
			public DateTime NowUtc=new DateTime(2026,10,19,12,0,0,DateTimeKind.Utc);
			public int CountTokenCalls;
			public int CountForcedRefreshes;
			public Microsoft365GraphClient Client;

			public Harness(Action<Microsoft365GraphOptions> actionOptions=null) {
				Microsoft365GraphOptions options=new Microsoft365GraphOptions();
				options.HttpMessageHandler=Handler;
				options.GetUtcNow=() => NowUtc;
				options.Delay=(timeSpan,ct) => {
					lock(ListDelays) {
						ListDelays.Add(timeSpan);
					}
					NowUtc=NowUtc.Add(timeSpan);
					return Task.CompletedTask;
				};
				options.Log=line => {
					lock(ListLogLines) {
						ListLogLines.Add(line);
					}
				};
				actionOptions?.Invoke(options);
				Client=new Microsoft365GraphClient(options);
			}

			///<summary>Gives TOKEN_1, then TOKEN_2 after a forced refresh.</summary>
			public Task<string> GetToken(IReadOnlyList<string> listScopes,bool forceRefresh,CancellationToken cancellationToken) {
				Interlocked.Increment(ref CountTokenCalls);
				if(forceRefresh) {
					Interlocked.Increment(ref CountForcedRefreshes);
					return Task.FromResult(TOKEN_2);
				}
				return Task.FromResult(TOKEN_1);
			}
		}

		private static HttpResponseMessage Json(HttpStatusCode statusCode,string json) {
			return FakeHttpHandler.MakeResponse(statusCode,json,"application/json");
		}

		private static string GraphError(string code) {
			return "{\"error\":{\"code\":\""+code+"\",\"message\":\"Mailbox frontdesk@example-dental.test said no.\"}}";
		}

		private static MimeMessage MakeMessage(int attachmentBytes=0,string subject="Recall reminder for Jane Patient") {
			MimeMessage mimeMessage=new MimeMessage();
			mimeMessage.From.Add(new MailboxAddress("Front Desk",MAILBOX));
			mimeMessage.To.Add(new MailboxAddress("Jane Patient","jane.patient@example.test"));
			mimeMessage.Bcc.Add(new MailboxAddress("","archive@example-dental.test"));
			mimeMessage.Subject=subject;
			BodyBuilder bodyBuilder=new BodyBuilder();
			bodyBuilder.HtmlBody="<p>Your cleaning is due.</p><img src=\"cid:logo.png\">";
			MimeEntity mimeEntityImage=bodyBuilder.LinkedResources.Add("logo.png",new byte[] { 1,2,3,4 });
			mimeEntityImage.ContentId="logo.png";
			if(attachmentBytes>0) {
				byte[] arrayAttachment=new byte[attachmentBytes];
				new Random(7).NextBytes(arrayAttachment);
				bodyBuilder.Attachments.Add("statement.pdf",arrayAttachment,new ContentType("application","pdf"));
			}
			mimeMessage.Body=bodyBuilder.ToMessageBody();
			return mimeMessage;
		}

		[TestMethod]
		public void Profile_ParsesTheFieldsAndSendsTheBearerToken() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.Enqueue(HttpStatusCode.OK,"{\"id\":\"oid-1\",\"mail\":\""+MAILBOX+"\",\"userPrincipalName\":\"fd@x.test\",\"displayName\":\"Front\"}");
				MicrosoftProfile microsoftProfile=await harness.Client.GetProfileAsync(MAILBOX,harness.GetToken);
				Assert.AreEqual("oid-1",microsoftProfile.Id);
				Assert.AreEqual(MAILBOX,microsoftProfile.Mail);
				RecordedRequest recordedRequest=harness.Handler.Requests.Single();
				Assert.AreEqual("https://graph.microsoft.com/v1.0/me?$select=id,mail,userPrincipalName,displayName",recordedRequest.RequestUri.AbsoluteUri);
				Assert.AreEqual("Bearer "+TOKEN_1,recordedRequest.GetHeader("Authorization"));
			});
		}

		[TestMethod]
		public void Unauthorized_RefreshesOnceAndRetriesOnce() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.Enqueue(HttpStatusCode.Unauthorized,GraphError("InvalidAuthenticationToken"));
				harness.Handler.Enqueue(HttpStatusCode.OK,"{\"id\":\"oid-1\"}");
				await harness.Client.GetProfileAsync(MAILBOX,harness.GetToken);
				Assert.AreEqual(1,harness.CountForcedRefreshes);
				Assert.AreEqual(2,harness.Handler.Requests.Count);
				Assert.AreEqual("Bearer "+TOKEN_2,harness.Handler.Requests[1].GetHeader("Authorization"));
			});
		}

		[TestMethod]
		public void Unauthorized_TwiceIsAnAuthError() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.Enqueue(HttpStatusCode.Unauthorized,GraphError("InvalidAuthenticationToken"));
				harness.Handler.Enqueue(HttpStatusCode.Unauthorized,GraphError("InvalidAuthenticationToken"));
				Microsoft365Exception ex=await AssertEx.ThrowsAsync<Microsoft365AuthException>(() => harness.Client.GetProfileAsync(MAILBOX,harness.GetToken));
				Assert.AreEqual(401,ex.HttpStatus);
				Assert.AreEqual(2,harness.Handler.Requests.Count,"One retry only.");
				Assert.IsTrue(ex.InnerException.Message.Contains("InvalidAuthenticationToken"));
			});
		}

		[TestMethod]
		public void FixedTokenProvider_TurnsA401IntoAnAuthError() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.Enqueue(HttpStatusCode.Unauthorized,GraphError("InvalidAuthenticationToken"));
				Microsoft365Exception ex=await AssertEx.ThrowsAsync<Microsoft365AuthException>(
					() => harness.Client.GetProfileAsync(MAILBOX,MicrosoftApiConnector.GetFixedTokenProvider(TOKEN_1)));
				Assert.AreEqual(1,harness.Handler.Requests.Count,"A bare token can't be refreshed here; the caller refreshes through the session.");
				Assert.IsTrue(ex.InnerException.Message.StartsWith("InvalidAuthenticationToken"));
			});
		}

		[TestMethod]
		public void Throttled_HonoursRetryAfter() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.Enqueue((HttpStatusCode)429,GraphError("ApplicationThrottled"),"application/json",
					response => response.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(3)));
				harness.Handler.Enqueue(HttpStatusCode.OK,"{\"id\":\"oid-1\"}");
				await harness.Client.GetProfileAsync(MAILBOX,harness.GetToken);
				CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(3) },harness.ListDelays);
			});
		}

		[TestMethod]
		public void Throttled_InteractiveGivesUpAfterTenSeconds() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.Enqueue((HttpStatusCode)429,GraphError("ApplicationThrottled"),"application/json",
					response => response.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30)));
				Microsoft365Exception ex=await AssertEx.ThrowsAsync<Microsoft365Exception>(() => harness.Client.GetProfileAsync(MAILBOX,harness.GetToken));
				Assert.AreEqual(Microsoft365ErrorKind.Throttled,ex.Kind);
				Assert.AreEqual(0,harness.ListDelays.Count,"A 30-second wait is too long for someone at the screen.");
			});
		}

		[TestMethod]
		public void Throttled_BackgroundBacksOffUpToFiveTimes() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.SetDefault((request,ct) => Task.FromResult(Json((HttpStatusCode)429,GraphError("ApplicationThrottled"))));
				Microsoft365Exception ex=await AssertEx.ThrowsAsync<Microsoft365Exception>(
					() => harness.Client.ListInboxAsync(MAILBOX,harness.GetToken,DateTime.UtcNow.AddDays(-1),CancellationToken.None,isBackground:true));
				Assert.AreEqual(Microsoft365ErrorKind.Throttled,ex.Kind);
				Assert.AreEqual(5,harness.ListDelays.Count);
				Assert.IsTrue(harness.ListDelays.All(x => x<=TimeSpan.FromSeconds(60)));
				Assert.AreEqual(6,harness.Handler.Requests.Count);
			});
		}

		[TestMethod]
		public void ServerError_GetsRetriedTwice() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.Enqueue(HttpStatusCode.ServiceUnavailable,"");
				harness.Handler.Enqueue(HttpStatusCode.InternalServerError,"");
				harness.Handler.Enqueue(HttpStatusCode.OK,"{\"id\":\"oid-1\"}");
				MicrosoftProfile microsoftProfile=await harness.Client.GetProfileAsync(MAILBOX,harness.GetToken);
				Assert.AreEqual("oid-1",microsoftProfile.Id);
				Assert.AreEqual(3,harness.Handler.Requests.Count);
				harness=new Harness();
				harness.Handler.SetDefault((request,ct) => Task.FromResult(Json(HttpStatusCode.BadGateway,"")));
				await AssertEx.ThrowsAsync<Microsoft365Exception>(() => harness.Client.GetProfileAsync(MAILBOX,harness.GetToken));
				Assert.AreEqual(3,harness.Handler.Requests.Count,"Two retries, then the error.");
			});
		}

		[TestMethod]
		public void SendMail_SmallMessageGoesAsBase64Mime() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.Enqueue(HttpStatusCode.Accepted,"");
				MimeMessage mimeMessage=MakeMessage();
				await harness.Client.SendMimeAsync(MAILBOX,harness.GetToken,mimeMessage);
				RecordedRequest recordedRequest=harness.Handler.Requests.Single();
				Assert.AreEqual(HttpMethod.Post,recordedRequest.Method);
				Assert.AreEqual("https://graph.microsoft.com/v1.0/me/sendMail",recordedRequest.RequestUri.AbsoluteUri);
				Assert.AreEqual("text/plain",recordedRequest.ContentType);
				byte[] arrayMime=Convert.FromBase64String(recordedRequest.Body);//Standard base64, not URL-safe.
				MimeMessage mimeMessageSent=MimeMessage.Load(new MemoryStream(arrayMime));
				Assert.AreEqual(mimeMessage.Subject,mimeMessageSent.Subject);
				Assert.AreEqual("archive@example-dental.test",mimeMessageSent.Bcc.Mailboxes.Single().Address,"Bcc travels inside the MIME.");
			});
		}

		[TestMethod]
		public void SendMail_NeverRetriedAfterAServerError() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.SetDefault((request,ct) => Task.FromResult(Json(HttpStatusCode.InternalServerError,GraphError("UnknownError"))));
				Microsoft365Exception ex=await AssertEx.ThrowsAsync<Microsoft365Exception>(
					() => harness.Client.SendMimeAsync(MAILBOX,harness.GetToken,MakeMessage()));
				Assert.AreEqual(Microsoft365ErrorKind.SendOutcomeUnknown,ex.Kind);
				Assert.AreEqual(1,harness.Handler.Requests.Count,"The message may be queued already; resending would duplicate patient mail.");
				StringAssert.Contains(ex.Message,"Sent Items");
			});
		}

		[TestMethod]
		public void SendMail_NeverRetriedAfterANetworkFailureOrTimeout() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.EnqueueNetworkFailure();
				Microsoft365Exception ex=await AssertEx.ThrowsAsync<Microsoft365Exception>(
					() => harness.Client.SendMimeAsync(MAILBOX,harness.GetToken,MakeMessage()));
				Assert.AreEqual(Microsoft365ErrorKind.SendOutcomeUnknown,ex.Kind);
				Assert.AreEqual(1,harness.Handler.Requests.Count);
				//The circuit breaker is now open: the next call fails at once, without reaching the handler.
				Microsoft365Exception exNext=await AssertEx.ThrowsAsync<Microsoft365Exception>(() => harness.Client.GetProfileAsync(MAILBOX,harness.GetToken));
				Assert.AreEqual(Microsoft365ErrorKind.Unreachable,exNext.Kind);
				Assert.AreEqual(1,harness.Handler.Requests.Count);
				StringAssert.Contains(exNext.Message,"could not be reached");
			});
		}

		[TestMethod]
		public void SendMail_429IsSafeToRetry() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.Enqueue((HttpStatusCode)429,GraphError("ApplicationThrottled"),"application/json",
					response => response.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(2)));
				harness.Handler.Enqueue(HttpStatusCode.Accepted,"");
				await harness.Client.SendMimeAsync(MAILBOX,harness.GetToken,MakeMessage());
				Assert.AreEqual(2,harness.Handler.Requests.Count,"A 429 means Graph did not take the message, so sending again is safe.");
			});
		}

		[TestMethod]
		public void SendMail_PacesThirtyAMinutePerMailbox() {
			AssertEx.Run(async () => {
				Harness harness=new Harness(options => options.SendsPerMinute=2);
				harness.Handler.SetDefault((request,ct) => Task.FromResult(Json(HttpStatusCode.Accepted,"")));
				await harness.Client.SendMimeAsync(MAILBOX,harness.GetToken,MakeMessage());
				await harness.Client.SendMimeAsync(MAILBOX,harness.GetToken,MakeMessage());
				Assert.AreEqual(0,harness.ListDelays.Count);
				await harness.Client.SendMimeAsync(MAILBOX,harness.GetToken,MakeMessage());
				Assert.AreEqual(1,harness.ListDelays.Count,"The third send in a minute waits.");
				Assert.AreEqual(TimeSpan.FromMinutes(1),harness.ListDelays[0]);
				await harness.Client.SendMimeAsync("other@example-dental.test",harness.GetToken,MakeMessage());
				Assert.AreEqual(1,harness.ListDelays.Count,"Each mailbox has its own allowance.");
				Assert.AreEqual(4,harness.Handler.Requests.Count);
			});
		}

		[TestMethod]
		public void SendMail_LargeMessageUsesDraftAttachmentsAndUploadSession() {
			AssertEx.Run(async () => {
				Harness harness=new Harness(options => {
					options.MimeSendThresholdBytes=2000;//Small numbers keep the test quick; the logic is the same as 2.9 MB / 3 MB.
					options.AttachmentSingleRequestLimitBytes=1000;
					options.UploadChunkBytes=1500;
				});
				string uploadUrl="https://outlook.office.com/api/v2.0/Users('x')/Messages('d')/AttachmentSessions('s')?authtoken=pre-authenticated";
				harness.Handler.SetDefault((request,ct) => {
					string path=request.RequestUri.AbsolutePath;
					if(request.Method==HttpMethod.Post && path=="/v1.0/me/messages") {
						return Task.FromResult(Json(HttpStatusCode.Created,"{\"id\":\"draft-1\"}"));
					}
					if(path.EndsWith("/attachments/createUploadSession")) {
						return Task.FromResult(Json(HttpStatusCode.Created,"{\"uploadUrl\":\""+uploadUrl+"\"}"));
					}
					if(path.EndsWith("/attachments")) {
						return Task.FromResult(Json(HttpStatusCode.Created,"{}"));
					}
					if(request.Method==HttpMethod.Put) {
						return Task.FromResult(Json(HttpStatusCode.OK,"{}"));
					}
					if(path.EndsWith("/send")) {
						return Task.FromResult(Json(HttpStatusCode.Accepted,""));
					}
					return Task.FromResult(Json(HttpStatusCode.NotFound,""));
				});
				await harness.Client.SendMimeAsync(MAILBOX,harness.GetToken,MakeMessage(attachmentBytes:4000));
				List<RecordedRequest> listRequests=harness.Handler.Requests;
				Assert.IsFalse(listRequests.Any(x => x.RequestUri.AbsolutePath.EndsWith("/sendMail")),"Too large for sendMail.");
				RecordedRequest requestDraft=listRequests[0];
				StringAssert.Contains(requestDraft.Body,"\"bccRecipients\"");
				StringAssert.Contains(requestDraft.Body,"archive@example-dental.test");
				StringAssert.Contains(requestDraft.Body,"\"contentType\":\"HTML\"");
				RecordedRequest requestInline=listRequests.Single(x => x.RequestUri.AbsolutePath=="/v1.0/me/messages/draft-1/attachments");
				StringAssert.Contains(requestInline.Body,"\"isInline\":true");
				StringAssert.Contains(requestInline.Body,"\"contentId\":\"logo.png\"");
				Assert.AreEqual(1,listRequests.Count(x => x.RequestUri.AbsolutePath.EndsWith("/createUploadSession")));
				List<RecordedRequest> listPuts=listRequests.Where(x => x.Method==HttpMethod.Put).ToList();
				Assert.AreEqual(3,listPuts.Count,"4000 bytes in 1500-byte ranges.");
				Assert.IsTrue(listPuts.All(x => x.GetHeader("Authorization")==null),"The upload URL is pre-authenticated: no bearer token.");
				Assert.IsTrue(listPuts.All(x => x.RequestUri.AbsoluteUri==uploadUrl),"Used exactly as returned.");
				Assert.AreEqual("/v1.0/me/messages/draft-1/send",listRequests.Last().RequestUri.AbsolutePath);
			});
		}

		[TestMethod]
		public void SendMail_DraftDeletedWhenAnAttachmentFails() {
			AssertEx.Run(async () => {
				Harness harness=new Harness(options => {
					options.MimeSendThresholdBytes=2000;
					options.AttachmentSingleRequestLimitBytes=100000;
				});
				harness.Handler.SetDefault((request,ct) => {
					string path=request.RequestUri.AbsolutePath;
					if(request.Method==HttpMethod.Post && path=="/v1.0/me/messages") {
						return Task.FromResult(Json(HttpStatusCode.Created,"{\"id\":\"draft-1\"}"));
					}
					if(path.EndsWith("/attachments")) {
						return Task.FromResult(Json(HttpStatusCode.BadRequest,GraphError("ErrorInvalidRequest")));
					}
					return Task.FromResult(Json(HttpStatusCode.NoContent,""));
				});
				await AssertEx.ThrowsAsync<Microsoft365Exception>(() => harness.Client.SendMimeAsync(MAILBOX,harness.GetToken,MakeMessage(attachmentBytes:4000)));
				RecordedRequest requestLast=harness.Handler.Requests.Last();
				Assert.AreEqual(HttpMethod.Delete,requestLast.Method);
				Assert.AreEqual("/v1.0/me/messages/draft-1",requestLast.RequestUri.AbsolutePath);
				Assert.IsFalse(harness.Handler.Requests.Any(x => x.RequestUri.AbsolutePath.EndsWith("/send")));
			});
		}

		[TestMethod]
		public void SendMail_UploadUrlOnAnotherHostIsRefused() {
			AssertEx.Run(async () => {
				Harness harness=new Harness(options => {
					options.MimeSendThresholdBytes=2000;
					options.AttachmentSingleRequestLimitBytes=1000;
				});
				harness.Handler.SetDefault((request,ct) => {
					string path=request.RequestUri.AbsolutePath;
					if(request.Method==HttpMethod.Post && path=="/v1.0/me/messages") {
						return Task.FromResult(Json(HttpStatusCode.Created,"{\"id\":\"draft-1\"}"));
					}
					if(path.EndsWith("/createUploadSession")) {
						return Task.FromResult(Json(HttpStatusCode.Created,"{\"uploadUrl\":\"https://upload.example.test/x\"}"));
					}
					return Task.FromResult(Json(HttpStatusCode.Created,"{}"));
				});
				await AssertEx.ThrowsAsync<Microsoft365Exception>(() => harness.Client.SendMimeAsync(MAILBOX,harness.GetToken,MakeMessage(attachmentBytes:4000)));
				Assert.IsFalse(harness.Handler.Requests.Any(x => x.RequestUri.Host=="upload.example.test"));
			});
		}

		[TestMethod]
		public void Inbox_FollowsNextLinkWithImmutableIds() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				string nextLink="https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages?$skiptoken=abc";
				harness.Handler.Enqueue(HttpStatusCode.OK,"{\"value\":[{\"id\":\"AAMk-1\",\"receivedDateTime\":\"2026-10-18T10:00:00Z\"},"
					+"{\"id\":\"AAMk-2\",\"receivedDateTime\":\"2026-10-17T10:00:00Z\"}],\"@odata.nextLink\":\""+nextLink+"\"}");
				harness.Handler.Enqueue(HttpStatusCode.OK,"{\"value\":[{\"id\":\"aamk-1\",\"receivedDateTime\":\"2026-10-16T10:00:00Z\"}]}");
				List<MicrosoftMessageRef> listMessageRefs=await harness.Client.ListInboxAsync(MAILBOX,harness.GetToken,
					new DateTime(2026,9,19,0,0,0,DateTimeKind.Utc));
				CollectionAssert.AreEqual(new[] { "AAMk-1","AAMk-2","aamk-1" },listMessageRefs.Select(x => x.Id).ToList(),"IDs are case-sensitive.");
				Assert.AreEqual(new DateTime(2026,10,18,10,0,0,DateTimeKind.Utc),listMessageRefs[0].ReceivedDateTime);
				List<RecordedRequest> listRequests=harness.Handler.Requests;
				Assert.AreEqual(2,listRequests.Count);
				string query=Uri.UnescapeDataString(listRequests[0].RequestUri.Query);
				StringAssert.Contains(query,"$filter=receivedDateTime ge 2026-09-19T00:00:00Z");
				StringAssert.Contains(query,"$orderby=receivedDateTime desc");
				StringAssert.Contains(query,"$top=100");
				Assert.AreEqual(nextLink,listRequests[1].RequestUri.AbsoluteUri,"nextLink is used as given.");
				Assert.IsTrue(listRequests.All(x => x.GetHeader("Prefer")=="IdType=\"ImmutableId\""));
			});
		}

		[TestMethod]
		public void Inbox_NextLinkToAnotherHostIsRefused() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.Enqueue(HttpStatusCode.OK,"{\"value\":[],\"@odata.nextLink\":\"https://elsewhere.example.test/next\"}");
				await AssertEx.ThrowsAsync<Microsoft365Exception>(
					() => harness.Client.ListInboxAsync(MAILBOX,harness.GetToken,DateTime.UtcNow.AddDays(-1)));
				Assert.AreEqual(1,harness.Handler.Requests.Count);
			});
		}

		[TestMethod]
		public void Mime_DownloadsAndParses() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				MimeMessage mimeMessage=MakeMessage(subject:"Lab case update");
				MemoryStream memoryStream=new MemoryStream();
				mimeMessage.WriteTo(memoryStream);
				harness.Handler.Enqueue(HttpStatusCode.OK,Encoding.UTF8.GetString(memoryStream.ToArray()),"message/rfc822");
				MimeMessage mimeMessageDownloaded=await harness.Client.GetMimeAsync(MAILBOX,harness.GetToken,"AAMk/1+x=");
				Assert.AreEqual("Lab case update",mimeMessageDownloaded.Subject);
				RecordedRequest recordedRequest=harness.Handler.Requests.Single();
				Assert.AreEqual("/v1.0/me/messages/AAMk%2F1%2Bx%3D/$value",recordedRequest.RequestUri.AbsolutePath,"The ID is escaped, never parsed.");
			});
		}

		[TestMethod]
		public void Errors_MissingMailboxAndMissingScope() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.Enqueue(HttpStatusCode.NotFound,GraphError("MailboxNotEnabledForRESTAPI"));
				Microsoft365Exception ex=await AssertEx.ThrowsAsync<Microsoft365Exception>(
					() => harness.Client.ListInboxAsync(MAILBOX,harness.GetToken,DateTime.UtcNow.AddDays(-1)));
				Assert.AreEqual(Microsoft365ErrorKind.NoMailbox,ex.Kind);
				harness.Handler.Enqueue(HttpStatusCode.Forbidden,GraphError("ErrorAccessDenied"));
				ex=await AssertEx.ThrowsAsync<Microsoft365AuthException>(() => harness.Client.ListInboxAsync(MAILBOX,harness.GetToken,DateTime.UtcNow));
				Assert.AreEqual(Microsoft365ErrorKind.MissingPermission,ex.Kind);
				Assert.IsFalse(ex.Message.Contains("frontdesk"),"Graph's own message is never copied.");
			});
		}

		[TestMethod]
		public void Logs_HoldNoTokensAddressesOrContent() {
			AssertEx.Run(async () => {
				Harness harness=new Harness();
				harness.Handler.Enqueue(HttpStatusCode.Unauthorized,GraphError("InvalidAuthenticationToken"));
				harness.Handler.Enqueue(HttpStatusCode.Accepted,"","application/json",response => response.Headers.Add("request-id","req-123"));
				harness.Handler.Enqueue(HttpStatusCode.OK,"{\"value\":[{\"id\":\"AAMk-secret-id\"}]}");
				await harness.Client.SendMimeAsync(MAILBOX,harness.GetToken,MakeMessage());
				await harness.Client.ListInboxAsync(MAILBOX,harness.GetToken,DateTime.UtcNow.AddDays(-1));
				Assert.IsTrue(harness.ListLogLines.Count>=3,"Each call leaves a line.");
				string allLines=string.Join("\n",harness.ListLogLines);
				StringAssert.Contains(allLines,"req-123","The vendor request ID is logged for support.");
				foreach(string forbidden in new[] { TOKEN_1,TOKEN_2,MAILBOX,"jane.patient","Recall reminder","cleaning","AAMk-secret-id","$filter","base64" }) {
					Assert.IsFalse(allLines.Contains(forbidden),"The log must not contain: "+forbidden);
				}
			});
		}
	}
}
