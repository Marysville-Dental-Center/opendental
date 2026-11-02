// V-PEARL2: the bridge really uses the logging pieces: its API client, its three HttpClients, the worker's error hook and the setup window.
// Offline: APIRequest is replaced by a fake that answers without sending anything.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.Pearl;
using OpenPmsDentalBusiness.WebBridges;

namespace VPEARL2Tests {
	[TestClass]
	public class PearlWiringTests {
		///<summary>Answers every request with a made-up token and records the HttpClient it was given. Sends nothing.</summary>
		private sealed class FakeApiRequest:IApiRequest {
			public readonly List<HttpClient> ListClients=new List<HttpClient>();

			public T SendRequest<T>(string urlEndpoint,HttpMethod method,AuthenticationHeaderValue authHeaderVal,string body,
				HttpContentType contentType=HttpContentType.Json,HttpClient clientOverride=null,List<string> queryParameters=null,
				JsonSerializerSettings deserializeSettings=null)
			{
				ListClients.Add(clientOverride);
				if(typeof(T)==typeof(AuthTokenResponse)) {
					return (T)(object)new AuthTokenResponse { access_token="synthetic-token",token_type="Bearer",expires_in=7200 };
				}
				return default(T);
			}

			public T SendRequest<T,U>(string urlEndpoint,HttpMethod method,AuthenticationHeaderValue authHeaderVal,U body,
				HttpContentType contentType=HttpContentType.Json,HttpClient clientOverride=null,List<string> queryParameters=null,
				JsonSerializerSettings deserializeSettings=null)
			{
				ListClients.Add(clientOverride);
				return default(T);
			}

			public Task<T> GetRequestAsync<T>(string urlEndpoint,HttpClient clientOverride=null) {
				throw new NotImplementedException();
			}

			public Task<T> PostRequestAsync<T>(string urlEndpoint,string body,HttpContentType contentType=HttpContentType.Json,HttpClient clientOverride=null) {
				throw new NotImplementedException();
			}

			public Task<T> SendRequestAsync<T,U>(string urlEndpoint,HttpMethod method,AuthenticationHeaderValue authHeaderVal,U body,
				HttpContentType contentType=HttpContentType.Json,HttpClient clientOverride=null,List<string> queryParameters=null,
				JsonSerializerSettings deserializeSettings=null)
			{
				throw new NotImplementedException();
			}
		}

		///<summary>The handler an HttpClient was built with (HttpMessageInvoker keeps it in a private field).</summary>
		private static HttpMessageHandler GetHandler(HttpClient httpClient) {
			FieldInfo fieldInfo=typeof(HttpMessageInvoker).GetFields(BindingFlags.Instance|BindingFlags.NonPublic)
				.FirstOrDefault(x => x.FieldType==typeof(HttpMessageHandler));
			Assert.IsNotNull(fieldInfo,"HttpMessageInvoker's handler field.");
			return (HttpMessageHandler)fieldInfo.GetValue(httpClient);
		}

		private static HttpClient GetStaticClient(string fieldName) {
			FieldInfo fieldInfo=typeof(PearlApiClient).GetField(fieldName,BindingFlags.Static|BindingFlags.NonPublic);
			Assert.IsNotNull(fieldInfo,fieldName);
			return (HttpClient)fieldInfo.GetValue(null);
		}

		[TestMethod]
		public void Inst_IsTheLoggingClient_OverTheRealClient() {
			PearlApiClientLogging pearlApiClientLogging=PearlApiClient.Inst as PearlApiClientLogging;
			Assert.IsNotNull(pearlApiClientLogging,"Pearl.cs calls PearlApiClient.Inst for uploads and results.");
			Assert.IsInstanceOfType(pearlApiClientLogging.Inner,typeof(PearlApiClient));
		}

		[TestMethod]
		public void HttpClients_AreBuiltOnTheLoggingHandler() {
			PearlLoggingHandler pearlLoggingHandlerApi=GetHandler(GetStaticClient("_httpPearlClient")) as PearlLoggingHandler;
			Assert.IsNotNull(pearlLoggingHandlerApi,"Pearl API client.");
			Assert.IsFalse(pearlLoggingHandlerApi.IsPresignedUpload);
			Assert.IsInstanceOfType(pearlLoggingHandlerApi.InnerHandler,typeof(HttpClientHandler),"The same transport as before.");
			PearlLoggingHandler pearlLoggingHandlerUpload=GetHandler(GetStaticClient("_httpAwsClient")) as PearlLoggingHandler;
			Assert.IsNotNull(pearlLoggingHandlerUpload,"Presigned upload client.");
			Assert.IsTrue(pearlLoggingHandlerUpload.IsPresignedUpload,"Its address is never logged.");
		}

		[TestMethod]
		public void RefreshedAuthClient_IsBuiltOnTheLoggingHandler_AndCallsGoThroughIt() {
			IApiRequest apiRequestOld=APIRequest.Inst;
			FakeApiRequest fakeApiRequest=new FakeApiRequest();
			APIRequest.Inst=fakeApiRequest;
			try {
				FieldInfo fieldInfoRefresh=typeof(PearlApiClient).GetField("_dateTimeLastAuthTokenRefresh",BindingFlags.Static|BindingFlags.NonPublic);
				Assert.IsNotNull(fieldInfoRefresh);
				fieldInfoRefresh.SetValue(null,DateTime.MinValue);//Force a refresh.
				PearlApiClient pearlApiClient=new PearlApiClient();
				pearlApiClient.RefreshAuthTokenIfNeeded("synthetic-id","synthetic-secret");
				HttpClient httpClient=GetStaticClient("_httpPearlClient");
				Assert.AreEqual("synthetic-token",httpClient.DefaultRequestHeaders.Authorization.Parameter,"The refresh ran.");
				Assert.IsInstanceOfType(GetHandler(httpClient),typeof(PearlLoggingHandler),"The client made on refresh logs too.");
				pearlApiClient.GetImageByRequestId("org-17","request-1");
				Assert.AreSame(httpClient,fakeApiRequest.ListClients.Last(),"Results checks use the logged client.");
			}
			finally {
				APIRequest.Inst=apiRequestOld;
			}
		}

		private static string ReadSource(string relativePath) {
			return File.ReadAllText(TestDb.FindRepoFile(relativePath));
		}

		[TestMethod]
		public void Worker_LogsItsFailure_BeforeTheAlert() {
			string source=ReadSource(Path.Combine("OpenPmsDentalBusiness","Bridges","Pearl","Pearl.cs"));
			int indexHandler=source.IndexOf("oDThreadPearl.AddExceptionHandler(",StringComparison.Ordinal);
			Assert.IsTrue(indexHandler>0,"SendOnThread's exception handler.");
			int indexLog=source.IndexOf("PearlLog.LogWorkerFailure(Document_?.DocNum??0,_pearlRequest?.RequestId,ex);",indexHandler,StringComparison.Ordinal);
			int indexAlert=source.IndexOf("AlertUserOfError(ex.Message);",indexHandler,StringComparison.Ordinal);
			Assert.IsTrue(indexLog>indexHandler && indexLog<indexAlert,"Logged inside the handler, before the alert.");
		}

		[TestMethod]
		public void SetupWindow_AddsTheRow_AndOffersTheLevels() {
			string source=ReadSource(Path.Combine("OpenPmsDental","Forms","FormProgramLinkEdit.cs"));
			int indexLoad=source.IndexOf("private void FormProgramLinkEdit_Load(",StringComparison.Ordinal);
			int indexFillForm=source.IndexOf("FillForm();",indexLoad,StringComparison.Ordinal);
			int indexEnsure=source.IndexOf("OpenPmsDentalBusiness.Pearl.PearlLog.EnsureLogLevelProperty(ProgramCur);",indexLoad,StringComparison.Ordinal);
			Assert.IsTrue(indexEnsure>indexLoad && indexEnsure<indexFillForm,"The row exists before the grid is filled.");
			int indexDoubleClick=source.IndexOf("private void gridMain_CellDoubleClick(",StringComparison.Ordinal);
			int indexCase=source.IndexOf("case nameof(ProgramName.Pearl):",indexDoubleClick,StringComparison.Ordinal);
			int indexShowForm=source.IndexOf("ShowFormProgramProperty(programProperty);",indexDoubleClick,StringComparison.Ordinal);
			Assert.IsTrue(indexCase>indexDoubleClick && indexCase<indexShowForm,"A list of levels instead of free text.");
			StringAssert.Contains(source.Substring(indexCase,indexShowForm-indexCase),"PearlLog.GetLogLevelValues()");
		}
	}
}
