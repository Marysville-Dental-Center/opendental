// Vendor-direct Microsoft 365 email: a small Microsoft Graph v1.0 client for the calls the program makes (profile, send, inbox list, MIME).
// Replaces the Graph SDK 4.42 calls inside the prebuilt Bridges.dll. Licensed under the GNU General Public License, version 2 or later.
// See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodeBase;
using CodeBase.VendorDirect;
using MimeKit;
using Newtonsoft.Json.Linq;

namespace OpenDentBusiness.Microsoft365 {
	//OpenDentBusiness.Task (the task list table type) would hide System.Threading.Tasks.Task otherwise.
	using Task=System.Threading.Tasks.Task;

	///<summary>Returns a bearer token for Graph with the given scopes. forceRefresh true means the last token got a 401: get a new one, or throw
	///Microsoft365AuthException if that isn't possible.</summary>
	public delegate Task<string> Microsoft365TokenProvider(IReadOnlyList<string> listScopes,bool forceRefresh,CancellationToken cancellationToken);

	///<summary>The delegated Microsoft Graph permissions the program asks for. Least privilege: Mail.Read only for inbox download, Mail.ReadWrite
	///only for the large-message (draft and upload) path. MSAL adds openid, profile and offline_access itself.</summary>
	public static class Microsoft365Scopes {
		public const string UserRead="User.Read";
		public const string MailSend="Mail.Send";
		public const string MailRead="Mail.Read";
		public const string MailReadWrite="Mail.ReadWrite";
		///<summary>GET /me.</summary>
		public static readonly IReadOnlyList<string> ListProfile=new[] { UserRead };
		///<summary>POST /me/sendMail. Also what a sign-in asks for by default.</summary>
		public static readonly IReadOnlyList<string> ListSend=new[] { UserRead,MailSend };
		///<summary>Inbox list and MIME download.</summary>
		public static readonly IReadOnlyList<string> ListRead=new[] { MailRead };
		///<summary>The large-message path: a draft, its attachments, then send.</summary>
		public static readonly IReadOnlyList<string> ListDraft=new[] { MailReadWrite,MailSend };

		///<summary>The scopes an interactive sign-in asks for.</summary>
		public static List<string> GetSignInScopes(bool includeMailRead,bool includeMailReadWrite=false) {
			List<string> listScopes=new List<string>(ListSend);
			if(includeMailRead) {
				listScopes.Add(MailRead);
			}
			if(includeMailReadWrite) {
				listScopes.Add(MailReadWrite);
			}
			return listScopes;
		}
	}

	///<summary>Where the integration's redacted log lines go: endpoint name, HTTP status, Graph error code and request ID. Never tokens, the cache,
	///authorization codes, subjects, addresses or MIME.</summary>
	public static class Microsoft365Log {
		///<summary>Replaced in tests. The default writes to the "Microsoft365" log folder.</summary>
		public static Action<string> Sink=line => Logger.WriteLine(line,"Microsoft365");

		public static void Write(string line) {
			try {
				Sink?.Invoke(line);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}
	}

	///<summary>Settings for a Microsoft365GraphClient. Production uses the defaults; tests pass a fake handler, a clock and a no-wait delay.</summary>
	public sealed class Microsoft365GraphOptions {
		///<summary>The handler under the vendor HTTP policy, or null for the real network.</summary>
		public HttpMessageHandler HttpMessageHandler;
		public Func<DateTime> GetUtcNow=() => DateTime.UtcNow;
		///<summary>How the client waits (throttling, pacing, retries). Tests replace it to avoid sleeping.</summary>
		public Func<TimeSpan,CancellationToken,Task> Delay=(timeSpan,cancellationToken) => Task.Delay(timeSpan,cancellationToken);
		public Action<string> Log=Microsoft365Log.Write;
		///<summary>Exchange Online accepts 30 messages a minute per mailbox. Sends past that wait their turn.</summary>
		public int SendsPerMinute=30;
		///<summary>Calls in flight per mailbox from this process. Outlook allows 4 per app and mailbox; 2 leaves room for a second workstation.</summary>
		public int MaxConcurrentPerMailbox=2;
		///<summary>MIME at or under this size goes to /me/sendMail in one request (base64 grows it by 4/3, staying under about 4 MB); larger
		///messages use the draft and upload path.</summary>
		public long MimeSendThresholdBytes=2900000;
		///<summary>Attachments under this size are added in one request; larger ones use an upload session.</summary>
		public int AttachmentSingleRequestLimitBytes=3*1024*1024;
		///<summary>Bytes per upload-session PUT. Microsoft advises keeping each range under 4 MB.</summary>
		public int UploadChunkBytes=3*1024*1024;
		///<summary>Interactive callers wait at most this long, in total, for throttling to clear.</summary>
		public TimeSpan MaxInteractiveThrottleWait=TimeSpan.FromSeconds(10);
		///<summary>Background callers wait at most this long per throttled attempt, for at most MaxBackgroundThrottleRetries attempts.</summary>
		public TimeSpan MaxBackgroundThrottleWait=TimeSpan.FromSeconds(60);
		public int MaxBackgroundThrottleRetries=5;
		///<summary>Network circuit breaker: after a network failure, fail every call at once for this long.</summary>
		public TimeSpan CircuitBreakDuration=TimeSpan.FromSeconds(30);
	}

	///<summary>The Graph calls the program makes, over the vendor HTTP policy (never an Open Dental host, HTTPS only, circuit breaker, redacted log
	///lines). Retry rules (docs/specs/vendor-direct/microsoft-365.md, 3.5):
	///- 401: get a fresh token once and retry once; a second 401 is Microsoft365AuthException.
	///- 429 (and 503 on GETs): honour Retry-After; interactive callers wait at most 10 s in total, background callers up to 60 s per try, 5 tries.
	///- other 5xx: GETs retry twice (1 s, then 2 s). sendMail and draft send are NEVER retried after a 5xx or a timeout, because the message may
	///  already be queued; the caller gets SendOutcomeUnknown.
	///Each mailbox has at most MaxConcurrentPerMailbox calls in flight from this process, and at most SendsPerMinute sends per rolling minute.</summary>
	public sealed class Microsoft365GraphClient {
		public const string GraphBaseUrl="https://graph.microsoft.com/v1.0/";
		///<summary>Hosts the client may call: Graph, and the pre-authenticated upload-session hosts (large attachments only).</summary>
		public static readonly IReadOnlyList<string> ListGraphHosts=new[] { "graph.microsoft.com" };
		public static readonly IReadOnlyList<string> ListUploadHosts=new[] { "outlook.office.com","outlook.office365.com" };
		private static readonly TimeSpan _timeSpanLongCall=TimeSpan.FromSeconds(100);
		private static readonly Lazy<Microsoft365GraphClient> _lazyDefault=new Lazy<Microsoft365GraphClient>(() => new Microsoft365GraphClient());

		private readonly Microsoft365GraphOptions _options;
		private readonly HttpClient _httpClientGraph;
		private readonly HttpClient _httpClientUpload;
		private readonly ConcurrentDictionary<string,MailboxLimiter> _dictLimiters=new ConcurrentDictionary<string,MailboxLimiter>();

		///<summary>The client every production caller shares.</summary>
		public static Microsoft365GraphClient Default {
			get {
				return _lazyDefault.Value;
			}
		}

		public Microsoft365GraphClient(Microsoft365GraphOptions options=null) {
			_options=options??new Microsoft365GraphOptions();
			VendorHttpOptions vendorHttpOptionsGraph=new VendorHttpOptions();
			vendorHttpOptionsGraph.VendorName="Microsoft Graph";
			vendorHttpOptionsGraph.BaseAddress=new Uri(GraphBaseUrl);
			vendorHttpOptionsGraph.ListAllowedHosts=ListGraphHosts.ToList();
			vendorHttpOptionsGraph.Timeout=TimeSpan.FromSeconds(30);
			vendorHttpOptionsGraph.MaxGetRetries=0;//This client applies Graph's own retry rules.
			vendorHttpOptionsGraph.UseCircuitBreaker=true;
			vendorHttpOptionsGraph.CircuitBreakDuration=_options.CircuitBreakDuration;
			vendorHttpOptionsGraph.GetUtcNow=_options.GetUtcNow;
			vendorHttpOptionsGraph.Log=_options.Log;
			VendorHttpOptions vendorHttpOptionsUpload=new VendorHttpOptions();
			vendorHttpOptionsUpload.VendorName="Microsoft Graph upload";
			vendorHttpOptionsUpload.ListAllowedHosts=ListUploadHosts.ToList();
			vendorHttpOptionsUpload.Timeout=_timeSpanLongCall;
			vendorHttpOptionsUpload.MaxGetRetries=0;
			vendorHttpOptionsUpload.UseCircuitBreaker=true;
			vendorHttpOptionsUpload.CircuitBreakDuration=_options.CircuitBreakDuration;
			vendorHttpOptionsUpload.GetUtcNow=_options.GetUtcNow;
			vendorHttpOptionsUpload.Log=_options.Log;
			if(_options.HttpMessageHandler==null) {
				_httpClientGraph=VendorHttp.GetClient(vendorHttpOptionsGraph);
				_httpClientUpload=VendorHttp.GetClient(vendorHttpOptionsUpload);
			}
			else {
				_httpClientGraph=VendorHttp.CreateClient(vendorHttpOptionsGraph,new NonDisposingHandler(_options.HttpMessageHandler));
				_httpClientUpload=VendorHttp.CreateClient(vendorHttpOptionsUpload,new NonDisposingHandler(_options.HttpMessageHandler));
			}
		}

		#region Calls
		///<summary>GET /me: the signed-in account. Used to check the account after sign-in.</summary>
		public async Task<MicrosoftProfile> GetProfileAsync(string mailboxKey,Microsoft365TokenProvider getToken,
			CancellationToken cancellationToken=default(CancellationToken))
		{
			using(HttpResponseMessage response=await SendAsync(mailboxKey,Microsoft365Scopes.ListProfile,getToken,
				() => CreateRequest(HttpMethod.Get,"me?$select=id,mail,userPrincipalName,displayName","me"),CallKind.Read,false,cancellationToken)
				.ConfigureAwait(false))
			{
				JObject jObject=await ReadJsonAsync(response).ConfigureAwait(false);
				MicrosoftProfile microsoftProfile=new MicrosoftProfile();
				microsoftProfile.Id=(string)jObject["id"]??"";
				microsoftProfile.Mail=(string)jObject["mail"]??"";
				microsoftProfile.UserPrincipalName=(string)jObject["userPrincipalName"]??"";
				microsoftProfile.DisplayName=(string)jObject["displayName"]??"";
				return microsoftProfile;
			}
		}

		///<summary>Lists the inbox's messages received at or after sinceUtc, newest first, with immutable IDs. Follows @odata.nextLink as given.
		///Stops after maxMessages.</summary>
		public async Task<List<MicrosoftMessageRef>> ListInboxAsync(string mailboxKey,Microsoft365TokenProvider getToken,DateTime sinceUtc,
			CancellationToken cancellationToken=default(CancellationToken),bool isBackground=false,int maxMessages=5000)
		{
			List<MicrosoftMessageRef> listMessageRefs=new List<MicrosoftMessageRef>();
			string since=sinceUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",System.Globalization.CultureInfo.InvariantCulture);
			//A property in $orderby must also be in $filter, first and in the same order, or Graph answers InefficientFilter.
			string url="me/mailFolders/inbox/messages?$select=id,receivedDateTime"
				+"&$filter="+Uri.EscapeDataString("receivedDateTime ge "+since)
				+"&$orderby="+Uri.EscapeDataString("receivedDateTime desc")
				+"&$top=100";
			while(!string.IsNullOrEmpty(url) && listMessageRefs.Count<maxMessages) {
				string urlCur=url;
				JObject jObject;
				using(HttpResponseMessage response=await SendAsync(mailboxKey,Microsoft365Scopes.ListRead,getToken,() => {
					HttpRequestMessage request=CreateRequest(HttpMethod.Get,urlCur,"inbox list");
					AddImmutableIdHeader(request);
					return request;
				},CallKind.Read,isBackground,cancellationToken).ConfigureAwait(false))
				{
					jObject=await ReadJsonAsync(response).ConfigureAwait(false);
				}
				JArray jArray=jObject["value"] as JArray;
				if(jArray!=null) {
					foreach(JToken jToken in jArray) {
						MicrosoftMessageRef microsoftMessageRef=new MicrosoftMessageRef();
						microsoftMessageRef.Id=(string)jToken["id"]??"";
						microsoftMessageRef.ReceivedDateTime=ReadUtcDate(jToken["receivedDateTime"]);
						if(microsoftMessageRef.Id!="") {
							listMessageRefs.Add(microsoftMessageRef);
						}
					}
				}
				url=(string)jObject["@odata.nextLink"];//Used exactly as given (an absolute Graph URL); checked against the host list when sent.
			}
			return listMessageRefs;
		}

		///<summary>GET /me/messages/{id}/$value: one message as RFC 822 MIME.</summary>
		public async Task<MimeMessage> GetMimeAsync(string mailboxKey,Microsoft365TokenProvider getToken,string messageId,
			CancellationToken cancellationToken=default(CancellationToken),bool isBackground=false)
		{
			if(string.IsNullOrEmpty(messageId)) {
				throw new ArgumentException("A message ID is required.",nameof(messageId));
			}
			using(HttpResponseMessage response=await SendAsync(mailboxKey,Microsoft365Scopes.ListRead,getToken,() => {
				HttpRequestMessage request=CreateRequest(HttpMethod.Get,"me/messages/"+Uri.EscapeDataString(messageId)+"/$value","message mime");
				AddImmutableIdHeader(request);
				VendorHttp.SetTimeout(request,_timeSpanLongCall);
				return request;
			},CallKind.Read,isBackground,cancellationToken).ConfigureAwait(false))
			{
				byte[] arrayBytes=await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
				using(MemoryStream memoryStream=new MemoryStream(arrayBytes)) {
					return MimeMessage.Load(memoryStream);
				}
			}
		}

		///<summary>Sends a MIME message as the signed-in user. At or under MimeSendThresholdBytes it goes to /me/sendMail in one request
		///(standard base64, Content-Type text/plain; Graph answers 202, queued). Larger messages are rebuilt as a draft with attachments (upload
		///sessions above 3 MB), then sent. Never retried after a 5xx or timeout: that throws SendOutcomeUnknown.</summary>
		public async Task SendMimeAsync(string mailboxKey,Microsoft365TokenProvider getToken,MimeMessage mimeMessage,
			CancellationToken cancellationToken=default(CancellationToken),bool isBackground=false)
		{
			if(mimeMessage==null) {
				throw new ArgumentNullException(nameof(mimeMessage));
			}
			byte[] arrayMime;
			using(MemoryStream memoryStream=new MemoryStream()) {
				mimeMessage.WriteTo(memoryStream);
				arrayMime=memoryStream.ToArray();
			}
			if(arrayMime.LongLength<=_options.MimeSendThresholdBytes) {
				await SendMimeDirectAsync(mailboxKey,getToken,arrayMime,cancellationToken,isBackground).ConfigureAwait(false);
				return;
			}
			await SendViaDraftAsync(mailboxKey,getToken,mimeMessage,cancellationToken,isBackground).ConfigureAwait(false);
		}

		private async Task SendMimeDirectAsync(string mailboxKey,Microsoft365TokenProvider getToken,byte[] arrayMime,
			CancellationToken cancellationToken,bool isBackground)
		{
			string base64=Convert.ToBase64String(arrayMime);
			await WaitForSendTurnAsync(mailboxKey,cancellationToken).ConfigureAwait(false);
			using(HttpResponseMessage response=await SendAsync(mailboxKey,Microsoft365Scopes.ListSend,getToken,() => {
				HttpRequestMessage request=CreateRequest(HttpMethod.Post,"me/sendMail","sendMail");
				request.Content=new StringContent(base64,Encoding.ASCII,"text/plain");
				VendorHttp.SetTimeout(request,_timeSpanLongCall);
				return request;
			},CallKind.Send,isBackground,cancellationToken).ConfigureAwait(false))
			{
			}
		}

		///<summary>The large-message path: POST /me/messages (a draft without attachments), each attachment by one POST or an upload session,
		///then POST /me/messages/{id}/send. The draft is deleted (best effort) if anything fails before the send was attempted, or if the send
		///was definitely refused.</summary>
		private async Task SendViaDraftAsync(string mailboxKey,Microsoft365TokenProvider getToken,MimeMessage mimeMessage,
			CancellationToken cancellationToken,bool isBackground)
		{
			JObject jObjectDraft=BuildDraftJson(mimeMessage);
			string draftId;
			using(HttpResponseMessage response=await SendAsync(mailboxKey,Microsoft365Scopes.ListDraft,getToken,() => {
				HttpRequestMessage request=CreateRequest(HttpMethod.Post,"me/messages","draft");
				request.Content=new StringContent(jObjectDraft.ToString(Newtonsoft.Json.Formatting.None),Encoding.UTF8,"application/json");
				return request;
			},CallKind.Write,isBackground,cancellationToken).ConfigureAwait(false))
			{
				draftId=(string)(await ReadJsonAsync(response).ConfigureAwait(false))["id"]??"";
			}
			if(draftId=="") {
				throw new Microsoft365Exception(Microsoft365ErrorKind.General,Microsoft365Errors.GetMessage(Microsoft365ErrorKind.General,"no draft ID"));
			}
			string draftPath="me/messages/"+Uri.EscapeDataString(draftId);
			bool canDeleteDraft=true;
			try {
				List<MimePart> listParts=GetAttachmentParts(mimeMessage);
				for(int i=0;i<listParts.Count;i++) {
					await AddAttachmentAsync(mailboxKey,getToken,draftPath,listParts[i],cancellationToken,isBackground).ConfigureAwait(false);
				}
				await WaitForSendTurnAsync(mailboxKey,cancellationToken).ConfigureAwait(false);
				try {
					using(HttpResponseMessage response=await SendAsync(mailboxKey,Microsoft365Scopes.ListDraft,getToken,() => {
						HttpRequestMessage request=CreateRequest(HttpMethod.Post,draftPath+"/send","send draft");
						request.Content=new StringContent("",Encoding.UTF8,"application/json");
						VendorHttp.SetTimeout(request,_timeSpanLongCall);
						return request;
					},CallKind.Send,isBackground,cancellationToken).ConfigureAwait(false))
					{
					}
				}
				catch(Microsoft365Exception ex) when(ex.Kind==Microsoft365ErrorKind.SendOutcomeUnknown) {
					canDeleteDraft=false;//It may have been sent: then the "draft" is the Sent Items copy. Leave it alone.
					throw;
				}
				catch(OperationCanceledException) {
					canDeleteDraft=false;
					throw;
				}
			}
			catch(Exception) {
				if(canDeleteDraft) {
					await DeleteDraftBestEffortAsync(mailboxKey,getToken,draftPath).ConfigureAwait(false);
				}
				throw;
			}
		}

		private async Task AddAttachmentAsync(string mailboxKey,Microsoft365TokenProvider getToken,string draftPath,MimePart mimePart,
			CancellationToken cancellationToken,bool isBackground)
		{
			byte[] arrayContent;
			using(MemoryStream memoryStream=new MemoryStream()) {
				if(mimePart.Content!=null) {
					mimePart.Content.DecodeTo(memoryStream,cancellationToken);
				}
				arrayContent=memoryStream.ToArray();
			}
			string name=GetAttachmentName(mimePart);
			string contentType=mimePart.ContentType?.MimeType??"application/octet-stream";
			bool isInline=!mimePart.IsAttachment && !string.IsNullOrEmpty(mimePart.ContentId);
			if(arrayContent.Length<_options.AttachmentSingleRequestLimitBytes) {
				JObject jObject=new JObject();
				jObject["@odata.type"]="#microsoft.graph.fileAttachment";
				jObject["name"]=name;
				jObject["contentType"]=contentType;
				jObject["contentBytes"]=Convert.ToBase64String(arrayContent);
				jObject["isInline"]=isInline;
				if(!string.IsNullOrEmpty(mimePart.ContentId)) {
					jObject["contentId"]=mimePart.ContentId;
				}
				using(HttpResponseMessage response=await SendAsync(mailboxKey,Microsoft365Scopes.ListDraft,getToken,() => {
					HttpRequestMessage request=CreateRequest(HttpMethod.Post,draftPath+"/attachments","attachment");
					request.Content=new StringContent(jObject.ToString(Newtonsoft.Json.Formatting.None),Encoding.UTF8,"application/json");
					VendorHttp.SetTimeout(request,_timeSpanLongCall);
					return request;
				},CallKind.Write,isBackground,cancellationToken).ConfigureAwait(false))
				{
				}
				return;
			}
			JObject jObjectItem=new JObject();
			jObjectItem["attachmentType"]="file";
			jObjectItem["name"]=name;
			jObjectItem["size"]=arrayContent.Length;
			jObjectItem["contentType"]=contentType;
			jObjectItem["isInline"]=isInline;
			if(!string.IsNullOrEmpty(mimePart.ContentId)) {
				jObjectItem["contentId"]=mimePart.ContentId;
			}
			JObject jObjectSession=new JObject();
			jObjectSession["AttachmentItem"]=jObjectItem;
			string uploadUrl;
			using(HttpResponseMessage response=await SendAsync(mailboxKey,Microsoft365Scopes.ListDraft,getToken,() => {
				HttpRequestMessage request=CreateRequest(HttpMethod.Post,draftPath+"/attachments/createUploadSession","upload session");
				request.Content=new StringContent(jObjectSession.ToString(Newtonsoft.Json.Formatting.None),Encoding.UTF8,"application/json");
				return request;
			},CallKind.Write,isBackground,cancellationToken).ConfigureAwait(false))
			{
				uploadUrl=(string)(await ReadJsonAsync(response).ConfigureAwait(false))["uploadUrl"]??"";
			}
			Uri uriUpload;
			if(!Uri.TryCreate(uploadUrl,UriKind.Absolute,out uriUpload) || VendorUrlPolicy.Check(uriUpload,ListUploadHosts)!=VendorUrlProblem.None) {
				throw new Microsoft365Exception(Microsoft365ErrorKind.General,
					Microsoft365Errors.GetMessage(Microsoft365ErrorKind.General,"the upload address was refused"));
			}
			for(int offset=0;offset<arrayContent.Length;offset+=_options.UploadChunkBytes) {
				int length=Math.Min(_options.UploadChunkBytes,arrayContent.Length-offset);
				int offsetCur=offset;
				//The upload URL is pre-authenticated: no Authorization header (getToken is null), and the URL is used exactly as returned.
				using(HttpResponseMessage response=await SendAsync(mailboxKey,null,null,() => {
					HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Put,uriUpload);
					VendorHttp.SetEndpointName(request,"upload range");
					VendorHttp.SetTimeout(request,_timeSpanLongCall);
					ByteArrayContent byteArrayContent=new ByteArrayContent(arrayContent,offsetCur,length);
					byteArrayContent.Headers.ContentType=new MediaTypeHeaderValue("application/octet-stream");
					byteArrayContent.Headers.ContentRange=new ContentRangeHeaderValue(offsetCur,offsetCur+length-1,arrayContent.Length);
					request.Content=byteArrayContent;
					return request;
				},CallKind.Write,isBackground,cancellationToken).ConfigureAwait(false))
				{
				}
			}
		}

		private async Task DeleteDraftBestEffortAsync(string mailboxKey,Microsoft365TokenProvider getToken,string draftPath) {
			try {
				using(CancellationTokenSource cancellationTokenSource=new CancellationTokenSource(TimeSpan.FromSeconds(30))) {
					using(HttpResponseMessage response=await SendAsync(mailboxKey,Microsoft365Scopes.ListDraft,getToken,
						() => CreateRequest(HttpMethod.Delete,draftPath,"delete draft"),CallKind.Write,false,cancellationTokenSource.Token)
						.ConfigureAwait(false))
					{
					}
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}
		#endregion Calls

		#region Draft building
		///<summary>The JSON for POST /me/messages built from a MIME message: subject, HTML or text body, To/Cc/Bcc. Attachments are added separately.</summary>
		public static JObject BuildDraftJson(MimeMessage mimeMessage) {
			JObject jObject=new JObject();
			jObject["subject"]=mimeMessage.Subject??"";
			JObject jObjectBody=new JObject();
			if(mimeMessage.HtmlBody!=null) {
				jObjectBody["contentType"]="HTML";
				jObjectBody["content"]=mimeMessage.HtmlBody;
			}
			else {
				jObjectBody["contentType"]="Text";
				jObjectBody["content"]=mimeMessage.TextBody??"";
			}
			jObject["body"]=jObjectBody;
			jObject["toRecipients"]=BuildRecipients(mimeMessage.To);
			jObject["ccRecipients"]=BuildRecipients(mimeMessage.Cc);
			jObject["bccRecipients"]=BuildRecipients(mimeMessage.Bcc);
			return jObject;
		}

		private static JArray BuildRecipients(InternetAddressList internetAddressList) {
			JArray jArray=new JArray();
			foreach(MailboxAddress mailboxAddress in internetAddressList.Mailboxes) {
				JObject jObjectAddress=new JObject();
				jObjectAddress["address"]=mailboxAddress.Address;
				if(!string.IsNullOrEmpty(mailboxAddress.Name)) {
					jObjectAddress["name"]=mailboxAddress.Name;
				}
				JObject jObjectRecipient=new JObject();
				jObjectRecipient["emailAddress"]=jObjectAddress;
				jArray.Add(jObjectRecipient);
			}
			return jArray;
		}

		///<summary>The parts the draft path adds as attachments: real attachments, and inline images (parts with a Content-ID that are not the
		///message body).</summary>
		public static List<MimePart> GetAttachmentParts(MimeMessage mimeMessage) {
			return mimeMessage.BodyParts.OfType<MimePart>()
				.Where(x => x.IsAttachment || (!string.IsNullOrEmpty(x.ContentId) && !(x is TextPart)))
				.ToList();
		}

		private static string GetAttachmentName(MimePart mimePart) {
			if(!string.IsNullOrWhiteSpace(mimePart.FileName)) {
				return mimePart.FileName;
			}
			if(!string.IsNullOrWhiteSpace(mimePart.ContentId)) {
				return mimePart.ContentId;
			}
			return "attachment";
		}
		#endregion Draft building

		#region Plumbing
		private enum CallKind {
			///<summary>GET: retried on 429, 503 and other 5xx.</summary>
			Read,
			///<summary>POST/PUT/DELETE that sends nothing: retried on 429 only.</summary>
			Write,
			///<summary>sendMail or draft send: retried on 429 only. A 5xx or timeout gives SendOutcomeUnknown.</summary>
			Send,
		}

		private static HttpRequestMessage CreateRequest(HttpMethod httpMethod,string relativeOrAbsoluteUrl,string endpointName) {
			HttpRequestMessage request=new HttpRequestMessage(httpMethod,new Uri(relativeOrAbsoluteUrl,UriKind.RelativeOrAbsolute));
			VendorHttp.SetEndpointName(request,endpointName);
			request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			return request;
		}

		private static void AddImmutableIdHeader(HttpRequestMessage request) {
			request.Headers.TryAddWithoutValidation("Prefer","IdType=\"ImmutableId\"");
		}

		private MailboxLimiter GetLimiter(string mailboxKey) {
			return _dictLimiters.GetOrAdd((mailboxKey??"").Trim().ToLowerInvariant(),x => new MailboxLimiter(_options.MaxConcurrentPerMailbox));
		}

		///<summary>Waits until this mailbox has sent fewer than SendsPerMinute messages in the last minute, then records a send.</summary>
		private async Task WaitForSendTurnAsync(string mailboxKey,CancellationToken cancellationToken) {
			MailboxLimiter mailboxLimiter=GetLimiter(mailboxKey);
			await mailboxLimiter.SemaphorePacing.WaitAsync(cancellationToken).ConfigureAwait(false);
			try {
				while(true) {
					DateTime dateTimeNow=_options.GetUtcNow();
					while(mailboxLimiter.QueueSendTimes.Count>0 && dateTimeNow-mailboxLimiter.QueueSendTimes.Peek()>=TimeSpan.FromMinutes(1)) {
						mailboxLimiter.QueueSendTimes.Dequeue();
					}
					if(mailboxLimiter.QueueSendTimes.Count<Math.Max(1,_options.SendsPerMinute)) {
						mailboxLimiter.QueueSendTimes.Enqueue(dateTimeNow);
						return;
					}
					TimeSpan wait=mailboxLimiter.QueueSendTimes.Peek().AddMinutes(1)-dateTimeNow;
					await _options.Delay(wait<TimeSpan.Zero ? TimeSpan.Zero : wait,cancellationToken).ConfigureAwait(false);
				}
			}
			finally {
				mailboxLimiter.SemaphorePacing.Release();
			}
		}

		///<summary>Sends one Graph request with the retry rules in the class summary, and returns a successful response with its body buffered.
		///Throws Microsoft365Exception (or Microsoft365AuthException) for every failure. getToken null means no Authorization header.</summary>
		private async Task<HttpResponseMessage> SendAsync(string mailboxKey,IReadOnlyList<string> listScopes,Microsoft365TokenProvider getToken,
			Func<HttpRequestMessage> createRequest,CallKind callKind,bool isBackground,CancellationToken cancellationToken)
		{
			MailboxLimiter mailboxLimiter=GetLimiter(mailboxKey);
			await mailboxLimiter.SemaphoreCalls.WaitAsync(cancellationToken).ConfigureAwait(false);
			try {
				string token=(getToken==null) ? null : await getToken(listScopes,false,cancellationToken).ConfigureAwait(false);
				bool hasRefreshedToken=false;
				int countServerRetries=0;
				int countThrottleRetries=0;
				TimeSpan timeSpanThrottleWaited=TimeSpan.Zero;
				HttpClient httpClient=(getToken==null) ? _httpClientUpload : _httpClientGraph;
				while(true) {
					HttpResponseMessage response;
					try {
						HttpRequestMessage request=createRequest();
						if(token!=null) {
							request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
						}
						response=await httpClient.SendAsync(request,HttpCompletionOption.ResponseContentRead,cancellationToken).ConfigureAwait(false);
					}
					catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
						throw;
					}
					catch(VendorHttpException ex) when(ex.Failure==VendorHttpFailure.UrlRefused) {
						throw new Microsoft365Exception(Microsoft365ErrorKind.General,
							Microsoft365Errors.GetMessage(Microsoft365ErrorKind.General,"the address was refused"),0,"UrlRefused","",ex);
					}
					catch(Exception ex) when(ex is HttpRequestException || ex is OperationCanceledException) {
						//DNS, connection, TLS, timeout, or the circuit breaker. A send may have reached Microsoft before the connection dropped.
						if(callKind==CallKind.Send && !(ex is VendorHttpException && ((VendorHttpException)ex).Failure==VendorHttpFailure.CircuitOpen)) {
							throw new Microsoft365Exception(Microsoft365ErrorKind.SendOutcomeUnknown,Microsoft365Errors.MessageSendOutcomeUnknown,0,
								"NetworkFailure","",ex);
						}
						throw new Microsoft365Exception(Microsoft365ErrorKind.Unreachable,Microsoft365Errors.MessageUnreachable,0,"NetworkFailure","",ex);
					}
					int status=(int)response.StatusCode;
					if(status==401 && getToken!=null && !hasRefreshedToken) {
						response.Dispose();
						hasRefreshedToken=true;
						token=await getToken(listScopes,true,cancellationToken).ConfigureAwait(false);
						continue;
					}
					if(status==429 || (status==503 && callKind==CallKind.Read)) {
						TimeSpan wait=GetRetryAfter(response)??TimeSpan.FromSeconds(Math.Pow(2,countThrottleRetries));
						bool canWait;
						if(isBackground) {
							wait=(wait>_options.MaxBackgroundThrottleWait) ? _options.MaxBackgroundThrottleWait : wait;
							canWait=countThrottleRetries<_options.MaxBackgroundThrottleRetries;
						}
						else {
							canWait=timeSpanThrottleWaited+wait<=_options.MaxInteractiveThrottleWait;
						}
						if(canWait) {
							response.Dispose();
							countThrottleRetries++;
							timeSpanThrottleWaited+=wait;
							await _options.Delay(wait,cancellationToken).ConfigureAwait(false);
							continue;
						}
						string requestIdThrottled=VendorHttp.GetVendorRequestId(response);
						response.Dispose();
						throw new Microsoft365Exception(Microsoft365ErrorKind.Throttled,Microsoft365Errors.MessageThrottled,status,"TooManyRequests",
							requestIdThrottled);
					}
					if(status>=500 && callKind==CallKind.Read && countServerRetries<2) {
						response.Dispose();
						countServerRetries++;
						await _options.Delay(TimeSpan.FromSeconds(countServerRetries),cancellationToken).ConfigureAwait(false);
						continue;
					}
					if(response.IsSuccessStatusCode) {
						return response;
					}
					throw await ToExceptionAsync(response,callKind).ConfigureAwait(false);
				}
			}
			finally {
				mailboxLimiter.SemaphoreCalls.Release();
			}
		}

		private static TimeSpan? GetRetryAfter(HttpResponseMessage response) {
			RetryConditionHeaderValue retryConditionHeaderValue=response.Headers.RetryAfter;
			if(retryConditionHeaderValue==null) {
				return null;
			}
			if(retryConditionHeaderValue.Delta.HasValue) {
				return retryConditionHeaderValue.Delta.Value;
			}
			if(retryConditionHeaderValue.Date.HasValue) {
				TimeSpan delta=retryConditionHeaderValue.Date.Value.UtcDateTime-DateTime.UtcNow;
				return (delta<TimeSpan.Zero) ? TimeSpan.Zero : delta;
			}
			return null;
		}

		///<summary>Turns an error response into an exception. Reads Graph's error.code; never copies Graph's message, which can quote addresses.</summary>
		private static async Task<Microsoft365Exception> ToExceptionAsync(HttpResponseMessage response,CallKind callKind) {
			using(response) {
				int status=(int)response.StatusCode;
				string requestId=VendorHttp.GetVendorRequestId(response);
				string code="";
				try {
					string body=(response.Content==null) ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
					if(!string.IsNullOrWhiteSpace(body) && body.TrimStart().StartsWith("{")) {
						JObject jObject=JObject.Parse(body);
						code=(string)jObject["error"]?["code"]??"";
					}
				}
				catch(Exception ex) {
					ex.DoNothing();
				}
				if(callKind==CallKind.Send && status>=500) {
					return new Microsoft365Exception(Microsoft365ErrorKind.SendOutcomeUnknown,Microsoft365Errors.MessageSendOutcomeUnknown,status,code,requestId);
				}
				return Microsoft365Errors.FromGraphError(status,code,"",requestId);
			}
		}

		///<summary>A Graph timestamp as UTC, whatever the workstation's culture. JSON.NET may already have parsed it to a date. MinValue if absent.</summary>
		private static DateTime ReadUtcDate(JToken jToken) {
			if(jToken==null) {
				return DateTime.MinValue;
			}
			if(jToken.Type==JTokenType.Date) {
				object value=((JValue)jToken).Value;
				if(value is DateTimeOffset) {
					return ((DateTimeOffset)value).UtcDateTime;
				}
				DateTime dateTime=(DateTime)value;
				return (dateTime.Kind==DateTimeKind.Unspecified) ? DateTime.SpecifyKind(dateTime,DateTimeKind.Utc) : dateTime.ToUniversalTime();
			}
			if(jToken.Type!=JTokenType.String) {
				return DateTime.MinValue;
			}
			DateTime dateTimeParsed;
			if(DateTime.TryParse((string)jToken,System.Globalization.CultureInfo.InvariantCulture,
				System.Globalization.DateTimeStyles.AdjustToUniversal|System.Globalization.DateTimeStyles.AssumeUniversal,out dateTimeParsed))
			{
				return dateTimeParsed;
			}
			return DateTime.MinValue;
		}

		private static async Task<JObject> ReadJsonAsync(HttpResponseMessage response) {
			string body=(response.Content==null) ? "" : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
			if(string.IsNullOrWhiteSpace(body)) {
				return new JObject();
			}
			try {
				return JObject.Parse(body);
			}
			catch(Newtonsoft.Json.JsonException ex) {
				throw new Microsoft365Exception(Microsoft365ErrorKind.General,Microsoft365Errors.GetMessage(Microsoft365ErrorKind.General,"unreadable answer"),
					(int)response.StatusCode,"InvalidJson",VendorHttp.GetVendorRequestId(response),ex);
			}
		}

		///<summary>Per-mailbox limits in this process.</summary>
		private sealed class MailboxLimiter {
			public readonly SemaphoreSlim SemaphoreCalls;
			public readonly SemaphoreSlim SemaphorePacing=new SemaphoreSlim(1,1);
			public readonly Queue<DateTime> QueueSendTimes=new Queue<DateTime>();

			public MailboxLimiter(int maxConcurrent) {
				SemaphoreCalls=new SemaphoreSlim(Math.Max(1,maxConcurrent),Math.Max(1,maxConcurrent));
			}
		}

		///<summary>Lets several HttpClients share a test handler without the first disposed client disposing it.</summary>
		private sealed class NonDisposingHandler : DelegatingHandler {
			public NonDisposingHandler(HttpMessageHandler innerHandler) : base(innerHandler) {
			}

			protected override void Dispose(bool disposing) {
				//The inner handler belongs to the caller.
			}
		}
		#endregion Plumbing
	}
}
