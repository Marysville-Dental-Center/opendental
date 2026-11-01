// Test helper for V-PEARL2: an HttpMessageHandler that never touches the network. It answers every request with a fixed status, or throws.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenDentBusiness.Pearl;

namespace VPEARL2Tests {
	public sealed class ScriptedHttpHandler:HttpMessageHandler {
		public HttpStatusCode StatusCode=HttpStatusCode.OK;
		public string Body="{}";
		///<summary>When set, thrown instead of answering.</summary>
		public Exception ExceptionToThrow;
		///<summary>The URI of every request that reached this handler.</summary>
		public readonly List<Uri> ListUris=new List<Uri>();
		///<summary>PearlLog's scope as this handler saw it on each request, to prove the scope reaches the HTTP layer.</summary>
		public readonly List<PearlLogContext> ListContexts=new List<PearlLogContext>();

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) {
			lock(ListUris) {
				ListUris.Add(request.RequestUri);
				ListContexts.Add(PearlLog.ContextCur);
			}
			if(ExceptionToThrow!=null) {
				throw ExceptionToThrow;
			}
			HttpResponseMessage response=new HttpResponseMessage(StatusCode);
			response.Content=new StringContent(Body,Encoding.UTF8,"application/json");
			response.RequestMessage=request;
			return Task.FromResult(response);
		}

		///<summary>An HttpClient over a PearlLoggingHandler over this handler, like the bridge's own clients but offline.</summary>
		public HttpClient CreateLoggedClient(bool isPresignedUpload,string baseAddress="https://pearl-api.example.test") {
			PearlLoggingHandler pearlLoggingHandler=new PearlLoggingHandler(isPresignedUpload);
			pearlLoggingHandler.InnerHandler=this;
			HttpClient httpClient=new HttpClient(pearlLoggingHandler);
			if(baseAddress!=null) {
				httpClient.BaseAddress=new Uri(baseAddress);
			}
			return httpClient;
		}
	}
}
