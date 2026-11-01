// V-PEARL2 (Open Dental 26.1): logs each HTTP call the Pearl bridge makes, for the "Log Level" troubleshooting log (see PearlLog).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OpenDentBusiness.Pearl {
	///<summary>Sits in front of the Pearl bridge's HttpClients (the Pearl API client and the presigned upload client in PearlApiClient). For each call it
	///logs the method, the endpoint path, the HTTP status and the time taken: as Verbose when the call succeeded, as Error when it failed (a status of
	///300 or more, a timeout or no connection). It reads no headers and no bodies, so the Authorization header, the client secret in the token
	///request, the access token in its response, patient fields and image bytes are never seen. For the upload client the address is not logged
	///either, because a presigned URL is itself a credential. The document and request come from PearlLog's current scope.</summary>
	public class PearlLoggingHandler:DelegatingHandler {
		private readonly bool _isPresignedUpload;

		public PearlLoggingHandler(bool isPresignedUpload) {
			_isPresignedUpload=isPresignedUpload;
		}

		///<summary>True for the handler of the presigned upload client.</summary>
		public bool IsPresignedUpload {
			get {
				return _isPresignedUpload;
			}
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken) {
			string endpoint=DescribeEndpoint(request);
			Stopwatch stopwatch=Stopwatch.StartNew();
			HttpResponseMessage response;
			try {
				response=await base.SendAsync(request,cancellationToken).ConfigureAwait(false);
			}
			catch(Exception ex) {
				stopwatch.Stop();
				string reason;
				if(ex is TaskCanceledException && !cancellationToken.IsCancellationRequested) {
					reason="timed out";
				}
				else {
					reason=PearlLog.DescribeException(ex.GetBaseException());
				}
				PearlLog.Error("API call failed: "+endpoint+", "+reason+" ("+FormatMs(stopwatch)+")");
				throw;
			}
			stopwatch.Stop();
			string line="API call: "+endpoint+" -> "+((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)
				+(string.IsNullOrEmpty(response.ReasonPhrase) ? "" : " "+response.ReasonPhrase)+" ("+FormatMs(stopwatch)+")";
			if(response.IsSuccessStatusCode) {
				PearlLog.Verbose(line);
			}
			else {
				PearlLog.Error(line);
			}
			return response;
		}

		///<summary>"POST /api/v1/image" for the Pearl API: method and path, no host, query or fragment. "POST presigned image upload" for the upload
		///client, whose address is never logged.</summary>
		public string DescribeEndpoint(HttpRequestMessage request) {
			string method=request?.Method?.Method??"?";
			if(_isPresignedUpload) {
				return method+" presigned image upload";
			}
			Uri uri=request?.RequestUri;
			if(uri==null) {
				return method+" (no address)";
			}
			if(!uri.IsAbsoluteUri) {
				string relative=uri.OriginalString;
				int index=relative.IndexOfAny(new[] { '?','#' });
				return method+" "+(index<0 ? relative : relative.Substring(0,index));
			}
			return method+" "+uri.AbsolutePath;
		}

		private static string FormatMs(Stopwatch stopwatch) {
			return stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)+" ms";
		}
	}
}
