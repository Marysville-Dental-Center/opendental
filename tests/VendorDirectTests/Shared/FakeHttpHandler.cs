// Test helper for every vendor-direct package: an HttpMessageHandler that answers from a script and records what was sent.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VendorDirectTests {
	///<summary>What a request looked like when it reached the fake handler. The body is read at send time, because the request may be disposed later.</summary>
	public sealed class RecordedRequest {
		public HttpMethod Method;
		public Uri RequestUri;
		public Dictionary<string,string> DictHeaders=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
		public string Body="";
		public string ContentType="";

		public string GetHeader(string name) {
			string value;
			return DictHeaders.TryGetValue(name,out value) ? value : null;
		}
	}

	///<summary>Answers requests from a queue of responders, or from a single responder for every request. Records every request.
	///Never touches the network. Use it as the innerHandler of VendorHttp.CreateClient, or directly with HttpClient.</summary>
	public sealed class FakeHttpHandler : HttpMessageHandler {
		private readonly Queue<Func<RecordedRequest,CancellationToken,Task<HttpResponseMessage>>> _queueResponders=
			new Queue<Func<RecordedRequest,CancellationToken,Task<HttpResponseMessage>>>();
		private Func<RecordedRequest,CancellationToken,Task<HttpResponseMessage>> _responderDefault;
		private readonly List<RecordedRequest> _listRequests=new List<RecordedRequest>();

		public List<RecordedRequest> Requests {
			get {
				lock(_listRequests) {
					return _listRequests.ToList();
				}
			}
		}

		///<summary>Queues one response. Responses are used in order; after the queue empties the default responder answers (404 if none).</summary>
		public FakeHttpHandler Enqueue(HttpStatusCode statusCode,string body="",string contentType="application/json",
			Action<HttpResponseMessage> actionCustomize=null)
		{
			lock(_queueResponders) {
				_queueResponders.Enqueue((request,ct) => Task.FromResult(MakeResponse(statusCode,body,contentType,actionCustomize)));
			}
			return this;
		}

		///<summary>Queues a responder that decides at send time (to inspect the request, delay, or throw).</summary>
		public FakeHttpHandler Enqueue(Func<RecordedRequest,CancellationToken,Task<HttpResponseMessage>> responder) {
			lock(_queueResponders) {
				_queueResponders.Enqueue(responder);
			}
			return this;
		}

		///<summary>Queues an HttpRequestException, as a network failure would give.</summary>
		public FakeHttpHandler EnqueueNetworkFailure() {
			return Enqueue((request,ct) => throw new HttpRequestException("Simulated network failure."));
		}

		///<summary>Answers every request not covered by the queue.</summary>
		public FakeHttpHandler SetDefault(Func<RecordedRequest,CancellationToken,Task<HttpResponseMessage>> responder) {
			_responderDefault=responder;
			return this;
		}

		public static HttpResponseMessage MakeResponse(HttpStatusCode statusCode,string body="",string contentType="application/json",
			Action<HttpResponseMessage> actionCustomize=null)
		{
			HttpResponseMessage response=new HttpResponseMessage(statusCode);
			response.Content=new StringContent(body??"",Encoding.UTF8,contentType);
			actionCustomize?.Invoke(response);
			return response;
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) {
			RecordedRequest recorded=new RecordedRequest();
			recorded.Method=request.Method;
			recorded.RequestUri=request.RequestUri;
			foreach(KeyValuePair<string,IEnumerable<string>> header in request.Headers) {
				recorded.DictHeaders[header.Key]=string.Join(",",header.Value);
			}
			if(request.Content!=null) {
				recorded.Body=await request.Content.ReadAsStringAsync().ConfigureAwait(false);
				recorded.ContentType=request.Content.Headers.ContentType?.MediaType??"";
			}
			lock(_listRequests) {
				_listRequests.Add(recorded);
			}
			Func<RecordedRequest,CancellationToken,Task<HttpResponseMessage>> responder=null;
			lock(_queueResponders) {
				if(_queueResponders.Count>0) {
					responder=_queueResponders.Dequeue();
				}
			}
			responder=responder??_responderDefault;
			if(responder==null) {
				return MakeResponse(HttpStatusCode.NotFound,"");
			}
			HttpResponseMessage response=await responder(recorded,cancellationToken).ConfigureAwait(false);
			response.RequestMessage=request;
			return response;
		}
	}
}
