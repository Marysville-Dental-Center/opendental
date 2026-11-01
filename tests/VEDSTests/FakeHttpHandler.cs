// Test helper: an HttpMessageHandler that answers from a script and records what was sent. Never touches the network.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VEDSTests {
	///<summary>What a request looked like when it reached the fake handler.</summary>
	public sealed class RecordedRequest {
		public HttpMethod Method;
		public Uri RequestUri;
		public string Authorization="";
		public string ContentType="";
		public string Body="";
	}

	public sealed class FakeHttpHandler:HttpMessageHandler {
		private readonly Queue<Func<RecordedRequest,CancellationToken,Task<HttpResponseMessage>>> _queueResponders=
			new Queue<Func<RecordedRequest,CancellationToken,Task<HttpResponseMessage>>>();
		private readonly List<RecordedRequest> _listRequests=new List<RecordedRequest>();

		public List<RecordedRequest> Requests {
			get {
				lock(_listRequests) {
					return _listRequests.ToList();
				}
			}
		}

		public FakeHttpHandler Enqueue(HttpStatusCode statusCode,string body,string contentType="application/json") {
			return Enqueue((request,ct) => Task.FromResult(MakeResponse(statusCode,body,contentType)));
		}

		public FakeHttpHandler Enqueue(Func<RecordedRequest,CancellationToken,Task<HttpResponseMessage>> responder) {
			lock(_queueResponders) {
				_queueResponders.Enqueue(responder);
			}
			return this;
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) {
			RecordedRequest recordedRequest=new RecordedRequest();
			recordedRequest.Method=request.Method;
			recordedRequest.RequestUri=request.RequestUri;
			recordedRequest.Authorization=request.Headers.Authorization==null ? "" : request.Headers.Authorization.ToString();
			if(request.Content!=null) {
				recordedRequest.Body=await request.Content.ReadAsStringAsync().ConfigureAwait(false);
				recordedRequest.ContentType=request.Content.Headers.ContentType?.MediaType??"";
			}
			lock(_listRequests) {
				_listRequests.Add(recordedRequest);
			}
			Func<RecordedRequest,CancellationToken,Task<HttpResponseMessage>> responder=null;
			lock(_queueResponders) {
				if(_queueResponders.Count>0) {
					responder=_queueResponders.Dequeue();
				}
			}
			if(responder==null) {
				return MakeResponse(HttpStatusCode.NotFound,"","text/plain");
			}
			return await responder(recordedRequest,cancellationToken).ConfigureAwait(false);
		}

		public static HttpResponseMessage MakeResponse(HttpStatusCode statusCode,string body,string contentType) {
			HttpResponseMessage response=new HttpResponseMessage(statusCode);
			response.Content=new StringContent(body??"",Encoding.UTF8,contentType);
			return response;
		}
	}
}
