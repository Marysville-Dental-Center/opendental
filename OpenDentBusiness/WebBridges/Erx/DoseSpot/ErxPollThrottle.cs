// eRx (DoseSpot and NewCrop): how often the Chart may poll the vendor, per workstation.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Concurrent;

namespace OpenDentBusiness {
	///<summary>In-memory throttles for the Chart's background eRx calls, replacing "everything on every patient selection". One per process (this
	///workstation). Interactive actions (the eRx window closing, eRx > Refresh) pass isForced and always run, except while the vendor has asked
	///for a pause (HTTP 429 with Retry-After).</summary>
	public sealed class ErxPollThrottle {
		///<summary>DoseSpot notification counts: at most once every 2 minutes per user and clinic.</summary>
		public static readonly TimeSpan IntervalNotificationCounts=TimeSpan.FromMinutes(2);
		///<summary>Prescription pull for the selected patient (DoseSpot and NewCrop): at most once every 10 minutes per patient.</summary>
		public static readonly TimeSpan IntervalPrescriptionSync=TimeSpan.FromMinutes(10);
		///<summary>DoseSpot medication-history consent: re-posted at most once every 24 hours per patient.</summary>
		public static readonly TimeSpan IntervalConsent=TimeSpan.FromHours(24);
		///<summary>The pause after a 429 without Retry-After.</summary>
		public static readonly TimeSpan DefaultRetryAfter=TimeSpan.FromSeconds(60);
		private static readonly Lazy<ErxPollThrottle> _lazyDefault=new Lazy<ErxPollThrottle>(() => new ErxPollThrottle());

		private readonly ConcurrentDictionary<string,DateTime> _dictLastRuns=new ConcurrentDictionary<string,DateTime>();
		private readonly ConcurrentDictionary<string,DateTime> _dictPausedUntil=new ConcurrentDictionary<string,DateTime>();
		private readonly Func<DateTime> _funcUtcNow;

		///<summary>funcUtcNow is the clock; tests replace it.</summary>
		public ErxPollThrottle(Func<DateTime> funcUtcNow=null) {
			_funcUtcNow=funcUtcNow??(() => DateTime.UtcNow);
		}

		///<summary>The process-wide throttle used by the program.</summary>
		public static ErxPollThrottle Default {
			get {
				return _lazyDefault.Value;
			}
		}

		///<summary>True if the call named by key may run now, and records it as run. False while vendor is paused, or (unless isForced) when the
		///same key ran less than interval ago.</summary>
		public bool TryBegin(string vendor,string key,TimeSpan interval,bool isForced=false) {
			DateTime now=_funcUtcNow();
			if(IsPaused(vendor)) {
				return false;
			}
			string keyFull=vendor+"|"+key;
			DateTime dateLast;
			if(!isForced && _dictLastRuns.TryGetValue(keyFull,out dateLast) && now-dateLast<interval) {
				return false;
			}
			_dictLastRuns[keyFull]=now;
			return true;
		}

		///<summary>Forgets that key ran, so the next TryBegin runs it (after a failure that should be retried on the next selection).</summary>
		public void Forget(string vendor,string key) {
			DateTime dateLast;
			_dictLastRuns.TryRemove(vendor+"|"+key,out dateLast);
		}

		///<summary>Pauses every background call to vendor for retryAfter (DefaultRetryAfter if null), after the vendor answered 429.</summary>
		public void Pause(string vendor,TimeSpan? retryAfter) {
			TimeSpan pause=retryAfter??DefaultRetryAfter;
			if(pause<TimeSpan.Zero) {
				pause=DefaultRetryAfter;
			}
			_dictPausedUntil[vendor]=_funcUtcNow().Add(pause);
		}

		///<summary>True while vendor asked for a pause.</summary>
		public bool IsPaused(string vendor) {
			DateTime dateUntil;
			return _dictPausedUntil.TryGetValue(vendor,out dateUntil) && _funcUtcNow()<dateUntil;
		}

		///<summary>How long the pause for vendor has left, or zero.</summary>
		public TimeSpan GetPauseRemaining(string vendor) {
			DateTime dateUntil;
			if(!_dictPausedUntil.TryGetValue(vendor,out dateUntil)) {
				return TimeSpan.Zero;
			}
			TimeSpan remaining=dateUntil-_funcUtcNow();
			return (remaining<TimeSpan.Zero) ? TimeSpan.Zero : remaining;
		}
	}
}
