// V-PEARL2 (Open Dental 26.1): the per-image lines of the Pearl bridge's "Log Level" troubleshooting log (see PearlLog).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using CodeBase;

namespace OpenDentBusiness.Pearl {
	///<summary>Wraps the Pearl API client that the bridge calls through PearlApiClient.Inst, and logs what happens to each image:
	///- Information: one line per uploaded image (document, request, time taken) and one per completed result (how many findings, after how many
	///  checks and how long).
	///- Error: a result that Pearl rejected or deleted (the failed-result case the 24.3 code leaves as a TODO).
	///- Verbose: the start of each upload, each check for results while Pearl is still working (check number and time since the first check), and
	///  calls that threw (the worker logs the failure itself as an error, see PearlLog.LogWorkerFailure).
	///Each call runs inside a PearlLog scope, so the HTTP lines of the call carry the same document and request. Every other member passes straight
	///through. Never changes what the wrapped client returns or throws.</summary>
	public class PearlApiClientLogging:IPearlApiClient {
		///<summary>Requests are forgotten this long after their upload or first check, in case polling stopped without a final result.</summary>
		private static readonly TimeSpan _timeSpanForget=TimeSpan.FromHours(1);
		private readonly IPearlApiClient _pearlApiClientInner;
		///<summary>Keyed by request ID.</summary>
		private readonly ConcurrentDictionary<string,RequestProgress> _dictRequests=new ConcurrentDictionary<string,RequestProgress>();
		///<summary>The clock. Replaceable for tests.</summary>
		public Func<DateTime> FuncNow=() => DateTime.Now;
		///<summary>The document of a request this client did not upload (polling restarted for an older request): read once per request from its
		///pearlrequest row. Replaceable for tests.</summary>
		public Func<string,long> FuncGetDocNum=requestId => PearlRequests.GetOneByRequestId(requestId)?.DocNum??0;

		public PearlApiClientLogging(IPearlApiClient pearlApiClientInner) {
			_pearlApiClientInner=pearlApiClientInner??throw new ArgumentNullException(nameof(pearlApiClientInner));
		}

		///<summary>The wrapped client.</summary>
		public IPearlApiClient Inner {
			get {
				return _pearlApiClientInner;
			}
		}

		public string SendOneImageToPearl(long docNum,Bitmap bitmap,Patient patient) {
			string requestId;
			Stopwatch stopwatch=Stopwatch.StartNew();
			using(PearlLog.BeginScope(docNum,null)) {
				PearlLog.Verbose("Upload starting"+(bitmap==null ? "" : " ("+bitmap.Width+" x "+bitmap.Height+" pixels)"));
				try {
					requestId=_pearlApiClientInner.SendOneImageToPearl(docNum,bitmap,patient);
				}
				catch(Exception ex) {
					PearlLog.Verbose("Upload failed after "+FormatMs(stopwatch)+": "+PearlLog.DescribeException(ex));
					throw;
				}
				if(string.IsNullOrWhiteSpace(requestId)) {
					PearlLog.Verbose("Upload returned no request ID after "+FormatMs(stopwatch));
					return requestId;
				}
			}
			ForgetOld();
			_dictRequests[requestId]=new RequestProgress(docNum,FuncNow());
			using(PearlLog.BeginScope(docNum,requestId)) {
				PearlLog.Info("Image uploaded ("+FormatMs(stopwatch)+")");
			}
			return requestId;
		}

		public Result GetOneImageFromPearl(string requestId) {
			if(string.IsNullOrWhiteSpace(requestId)) {
				return _pearlApiClientInner.GetOneImageFromPearl(requestId);
			}
			ForgetOld();
			DateTime dateTimeNow=FuncNow();
			RequestProgress requestProgress=_dictRequests.GetOrAdd(requestId,x => new RequestProgress(LookUpDocNum(x),dateTimeNow));
			int countChecks=requestProgress.StartCheck(dateTimeNow);
			using(PearlLog.BeginScope(requestProgress.DocNum,requestId)) {
				Result result;
				try {
					result=_pearlApiClientInner.GetOneImageFromPearl(requestId);
				}
				catch(Exception ex) {
					//Kept, not forgotten: if the bridge polls again after a failed check, the count and the document carry on. Old entries expire.
					PearlLog.Verbose("Results check "+countChecks+" failed: "+PearlLog.DescribeException(ex));
					throw;
				}
				string progress="check "+countChecks+", "+FormatSeconds(FuncNow()-requestProgress.DateTimeFirstCheck)+" since the first check";
				if(result==null) {
					PearlLog.Verbose("Results not ready ("+progress+")");
				}
				else if(result.is_deleted || result.is_rejected) {
					Forget(requestId);
					string state=result.is_rejected ? "rejected" : "deleted";
					if(result.is_rejected && result.is_deleted) {
						state="rejected and deleted";
					}
					PearlLog.Error("Pearl "+state+" the image; no results will come ("+progress
						+(string.IsNullOrWhiteSpace(result.message_type) ? "" : ", message type "+result.message_type)+")");
				}
				else if(result.is_completed) {
					Forget(requestId);
					int countAnnotations=result.annotations?.Length??0;
					int countToothParts=result.toothParts?.Length??0;
					PearlLog.Info("Results received: "+countAnnotations+" annotations, "+countToothParts+" tooth parts ("+progress+")");
				}
				else {
					PearlLog.Verbose("Results not ready, Pearl is still processing ("+progress+")");
				}
				return result;
			}
		}

		public void RefreshAuthTokenIfNeeded(string clientId,string clientSecret) {
			_pearlApiClientInner.RefreshAuthTokenIfNeeded(clientId,clientSecret);
		}

		public AuthTokenResponse GetAuthToken(string clientId,string clientSecret) {
			return _pearlApiClientInner.GetAuthToken(clientId,clientSecret);
		}

		public ImageResponse GetAwsPresignedUrlInfo(ImageRequest image) {
			return _pearlApiClientInner.GetAwsPresignedUrlInfo(image);
		}

		public bool UploadToAwsPresignedUrl(Bitmap bitmap,ImageResponse image) {
			return _pearlApiClientInner.UploadToAwsPresignedUrl(bitmap,image);
		}

		public ImageRequestIdResponse GetImageByRequestId(string organizationId,string requestId) {
			return _pearlApiClientInner.GetImageByRequestId(organizationId,requestId);
		}

		public string BitmapToImageTempCopy(Bitmap bitmap,string fileName) {
			return _pearlApiClientInner.BitmapToImageTempCopy(bitmap,fileName);
		}

		///<summary>The number of requests being tracked. For tests.</summary>
		public int CountTracked {
			get {
				return _dictRequests.Count;
			}
		}

		///<summary>FuncGetDocNum, or 0 when it fails. Never throws.</summary>
		private long LookUpDocNum(string requestId) {
			try {
				return FuncGetDocNum?.Invoke(requestId)??0;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return 0;
			}
		}

		private void Forget(string requestId) {
			RequestProgress requestProgress;
			_dictRequests.TryRemove(requestId,out requestProgress);
		}

		private void ForgetOld() {
			DateTime dateTimeCutoff=FuncNow()-_timeSpanForget;
			foreach(string requestId in _dictRequests.Where(x => x.Value.DateTimeStarted<dateTimeCutoff).Select(x => x.Key).ToList()) {
				Forget(requestId);
			}
		}

		private static string FormatMs(Stopwatch stopwatch) {
			return stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)+" ms";
		}

		private static string FormatSeconds(TimeSpan timeSpan) {
			return Math.Max(0,(int)timeSpan.TotalSeconds).ToString(CultureInfo.InvariantCulture)+" s";
		}

		///<summary>What is known about one request: its document, when tracking started, and the checks for results so far.</summary>
		private sealed class RequestProgress {
			private readonly object _lock=new object();
			private int _countChecks;
			private DateTime _dateTimeFirstCheck=DateTime.MinValue;

			public RequestProgress(long docNum,DateTime dateTimeStarted) {
				DocNum=docNum;
				DateTimeStarted=dateTimeStarted;
			}

			///<summary>0 when the request was not uploaded through this client (not known).</summary>
			public long DocNum { get; }
			public DateTime DateTimeStarted { get; }

			public DateTime DateTimeFirstCheck {
				get {
					lock(_lock) {
						return _dateTimeFirstCheck;
					}
				}
			}

			///<summary>Counts a check and returns its number, starting at 1.</summary>
			public int StartCheck(DateTime dateTimeNow) {
				lock(_lock) {
					_countChecks++;
					if(_countChecks==1) {
						_dateTimeFirstCheck=dateTimeNow;
					}
					return _countChecks;
				}
			}
		}
	}
}
