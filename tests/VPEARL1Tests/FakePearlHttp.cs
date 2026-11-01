// A fake HTTP layer for the Pearl client. It stands in for APIRequest.Inst, records every request, and answers the way the Pearl calls that the
// bridge already makes are answered (token, upload address, upload, result by request ID). The real PearlApiClient runs on top of it, so the
// tests never reach the network. Any request it does not expect throws. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Result=OpenDentBusiness.Pearl.Result;
using Newtonsoft.Json;
using OpenDentBusiness;
using OpenDentBusiness.Pearl;
using OpenDentBusiness.WebBridges;

namespace VPEARL1Tests {
	public class FakePearlHttp:IApiRequest {
		///<summary>Where the fake upload address points. The .invalid top-level domain never resolves.</summary>
		public const string UPLOAD_URL="https://upload.invalid/vpearl1-bucket";
		private readonly object _lock=new object();
		private readonly List<string> _listCalls=new List<string>();
		private readonly List<string> _listUploadFileNames=new List<string>();
		///<summary>The token request fails.</summary>
		public bool FailAuth;
		///<summary>The upload of the image to the address Pearl gave fails, after the request row was inserted as Uploading.</summary>
		public bool FailUpload;
		///<summary>The answer to a poll for a request ID. May throw, like a failed call.</summary>
		public Func<string,ImageRequestIdResponse> FuncPoll=(requestId) => Completed(requestId,hasFindings:true);

		///<summary>Every request so far, as "METHOD endpoint", oldest first.</summary>
		public List<string> GetCalls() {
			lock(_lock) {
				return new List<string>(_listCalls);
			}
		}

		///<summary>The file names the client was told to upload to, which it saved as temporary files first.</summary>
		public List<string> GetUploadFileNames() {
			lock(_lock) {
				return new List<string>(_listUploadFileNames);
			}
		}

		///<summary>A finished result, with both lists present as a finished result carries them. With findings it holds one tooth part (drawn as one
		///polygon); without, both lists are empty, which is NoResults.</summary>
		public static ImageRequestIdResponse Completed(string requestId,bool hasFindings) {
			Result result=new Result();
			result.request_id=requestId;
			result.is_completed=true;
			result.annotations=new Annotation[0];
			result.toothParts=new ToothPart[0];
			if(hasFindings) {
				ToothPart toothPart=new ToothPart();
				toothPart.condition_id=EnumCondition.Enamel;
				toothPart.category_id=EnumCategory.ToothParts;
				toothPart.polygon=new Polygon[] {
					new Polygon { x=0.1,y=0.1 },
					new Polygon { x=0.5,y=0.1 },
					new Polygon { x=0.5,y=0.5 },
				};
				result.toothParts=new ToothPart[] { toothPart };
			}
			return new ImageRequestIdResponse { result=result };
		}

		public T SendRequest<T>(string urlEndpoint,HttpMethod method,AuthenticationHeaderValue authHeaderVal,string body,
			HttpContentType contentType=HttpContentType.Json,HttpClient clientOverride=null,List<string> queryParameters=null,
			JsonSerializerSettings deserializeSettings=null)
		{
			return Answer<T>(urlEndpoint,method);
		}

		public T SendRequest<T,U>(string urlEndpoint,HttpMethod method,AuthenticationHeaderValue authHeaderVal,U body,
			HttpContentType contentType=HttpContentType.Json,HttpClient clientOverride=null,List<string> queryParameters=null,
			JsonSerializerSettings deserializeSettings=null)
		{
			try {
				return Answer<T>(urlEndpoint,method);
			}
			finally {
				//APIRequest disposes the request message, and with it the content (the upload's file stream), whether or not the call worked.
				(body as IDisposable)?.Dispose();
			}
		}

		public System.Threading.Tasks.Task<T> GetRequestAsync<T>(string urlEndpoint,HttpClient clientOverride=null) {
			return System.Threading.Tasks.Task.FromResult(Answer<T>(urlEndpoint,HttpMethod.Get));
		}

		public System.Threading.Tasks.Task<T> PostRequestAsync<T>(string urlEndpoint,string body,HttpContentType contentType=HttpContentType.Json,HttpClient clientOverride=null) {
			return System.Threading.Tasks.Task.FromResult(Answer<T>(urlEndpoint,HttpMethod.Post));
		}

		public System.Threading.Tasks.Task<T> SendRequestAsync<T,U>(string urlEndpoint,HttpMethod method,AuthenticationHeaderValue authHeaderVal,U body,
			HttpContentType contentType=HttpContentType.Json,HttpClient clientOverride=null,List<string> queryParameters=null,
			JsonSerializerSettings deserializeSettings=null)
		{
			try {
				return System.Threading.Tasks.Task.FromResult(Answer<T>(urlEndpoint,method));
			}
			finally {
				(body as IDisposable)?.Dispose();
			}
		}

		private T Answer<T>(string urlEndpoint,HttpMethod method) {
			lock(_lock) {
				_listCalls.Add(method.Method+" "+urlEndpoint);
			}
			if(method==HttpMethod.Post && urlEndpoint=="/api/v1/auth/token") {
				if(FailAuth) {
					throw new HttpRequestException("Synthetic token failure.");
				}
				//expires_in 0: the client asks for a new token every time, so each test sees its own token request.
				return (T)(object)new AuthTokenResponse { access_token="vpearl1-synthetic-token",token_type="Bearer",expires_in=0 };
			}
			if(method==HttpMethod.Post && urlEndpoint=="/api/v1/image") {
				string fileName="vpearl1_"+Guid.NewGuid().ToString("N")+".jpg";
				lock(_lock) {
					_listUploadFileNames.Add(fileName);
				}
				ImageResponse imageResponse=new ImageResponse();
				imageResponse.image_url=UPLOAD_URL+"/"+fileName;
				imageResponse.presigned_url=new PresignedUrl { url=UPLOAD_URL,fields=new Field[0] };
				return (T)(object)imageResponse;
			}
			if(method==HttpMethod.Post && urlEndpoint==UPLOAD_URL) {
				if(FailUpload) {
					throw new HttpRequestException("Synthetic upload failure.");
				}
				return (T)(object)new HttpResponseMessage(HttpStatusCode.NoContent);
			}
			Match match=Regex.Match(urlEndpoint,"^/api/v1/image/([^/]*)/byRequestId/([^/]+)$");
			if(method==HttpMethod.Get && match.Success) {
				return (T)(object)FuncPoll(match.Groups[2].Value);
			}
			throw new InvalidOperationException("Unexpected request in a test: "+method.Method+" "+urlEndpoint);
		}
	}
}
