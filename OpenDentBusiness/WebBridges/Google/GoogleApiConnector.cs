using Google.Apis.Auth.OAuth2;
using Google.Apis.Http;
using Google.Apis.Services;
using OpenDentBusiness.Email;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GmailApi=Google.Apis.Gmail.v1;

namespace OpenDentBusiness {
	/// <summary>Connects Google's Gmail client library (Google.Apis.Gmail.v1, Apache 2.0) to an access token that this program got itself, with as
	/// few dependencies as possible, so the email layer needs no database, OAuth client or token cache. The token comes from GoogleTokenCache.</summary>
	public class GoogleApiConnector {

		///<summary>Tests only (tests/VendorDirectTests/Email): every Gmail service sends through this handler instead of the network. Null in
		///production.</summary>
		internal static HttpMessageHandler HttpMessageHandlerForTests;

		///<summary>Helper method that returns a ready-to-use Gmail service that is created based on the user's credentials</summary>
		public static GmailApi.GmailService CreateGmailService(BasicEmailAddress emailAddress) {
			ODGoogleUserCredential credential=new ODGoogleUserCredential() {
				AccessToken=emailAddress.AccessToken,
			};
			BaseClientService.Initializer baseService=new BaseClientService.Initializer(){
				HttpClientInitializer=credential,
				//Google's library resends a request that got a 503 by default. A send that got a 5xx may already have been accepted, so a resend could
				//mail a patient twice. EmailMessages decides what is retried (google.md 3.5): never a send after a 5xx; the inbox download backs off itself.
				DefaultExponentialBackOffPolicy=ExponentialBackOffPolicy.None
			};
			if(HttpMessageHandlerForTests!=null) {
				baseService.HttpClientFactory=new HandlerHttpClientFactory(HttpMessageHandlerForTests);
			}
			return new GmailApi.GmailService(baseService);
		}

		///<summary>Builds Google's HTTP client over a given handler (tests). The handler is shared by every service a test creates, so disposing a
		///service leaves it alone.</summary>
		private class HandlerHttpClientFactory : HttpClientFactory {
			private readonly HttpMessageHandler _httpMessageHandler;

			public HandlerHttpClientFactory(HttpMessageHandler httpMessageHandler) {
				_httpMessageHandler=httpMessageHandler;
			}

			protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args) {
				return new NonDisposingHandler(_httpMessageHandler);
			}
		}

		///<summary>Passes requests to a shared handler without disposing it.</summary>
		private class NonDisposingHandler : DelegatingHandler {
			public NonDisposingHandler(HttpMessageHandler innerHandler) : base(innerHandler) {
			}

			protected override void Dispose(bool disposing) {
				//The inner handler belongs to the caller.
			}
		}

		///<summary>Stands in for Google's UserCredential class, which would fetch and refresh tokens itself with the client ID and secret and a
		///file-based token store. This program refreshes tokens itself (GoogleTokenCache), so this only adds the bearer token to each request. When a
		///call fails with 401, the caller refreshes the token and retries once.</summary>
		public class ODGoogleUserCredential : IConfigurableHttpClientInitializer, IHttpExecuteInterceptor {

			///<summary>The access token to use to send requests to the Google API</summary>
			public string AccessToken { get; set; }

			public ODGoogleUserCredential() { }

			///<summary>This is the required method for IConfigurableHttpClientInitializer.  This matches the logic from Google's UserCredential
			///from their GitHub page</summary>
			public void Initialize(ConfigurableHttpClient httpClient) {
				httpClient.MessageHandler.Credential=this;
			}

			///<summary>This is the required method for IHttpExecuteInterceptor.  This method allows us to hijack our own requests and add headers or other values to the context.
			///Google assumes that this method will attach needed Authorization headers, which we must use a Bearer token for.</summary>
			public System.Threading.Tasks.Task InterceptAsync(HttpRequestMessage request,CancellationToken cancellationToken) {
				BearerToken.AuthorizationHeaderAccessMethod bearerTokenInterceptor=new BearerToken.AuthorizationHeaderAccessMethod();
				//Overwrite the Authorization header with a bearer token.
				bearerTokenInterceptor.Intercept(request,AccessToken);
				//We can't use Task.CompletedTask because we are not on .NET 4.6 at this time.  Simply run an empty task.
				return System.Threading.Tasks.Task.Run(() => { });
			}
		}
	}
}
