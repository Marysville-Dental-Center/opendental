// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ImagingCapture.Camera {
	///<summary>The one debounce gate every camera trigger passes through (design 8.4, 10.6): a press is accepted only when no press
	///was accepted in the last LockoutMs, so one physical press gives one photo whichever trigger sees it first, and key bounce is
	///dropped. A rejected press does not extend the lockout. Thread-safe: the still pin and the control poll call it from
	///different threads.</summary>
	public sealed class DebounceGate {
		private readonly object _lock=new object();
		private bool _hasAccepted;
		private long _timeAcceptedMs;

		///<summary>The lockout after an accepted press, in milliseconds. 0 accepts every press.</summary>
		public int LockoutMs { get; }

		public DebounceGate(int lockoutMs) {
			LockoutMs=Math.Max(0,lockoutMs);
		}

		///<summary>True, and the lockout starts at nowMs, when no press was accepted within the lockout before nowMs. nowMs is a
		///monotonic clock in milliseconds.</summary>
		public bool TryAccept(long nowMs) {
			lock(_lock) {
				if(_hasAccepted && nowMs-_timeAcceptedMs<LockoutMs) {
					return false;
				}
				_hasAccepted=true;
				_timeAcceptedMs=nowMs;
				return true;
			}
		}

		///<summary>True while a lockout started by an accepted press is still running at nowMs.</summary>
		public bool IsLockedOut(long nowMs) {
			lock(_lock) {
				return _hasAccepted && nowMs-_timeAcceptedMs<LockoutMs;
			}
		}

		///<summary>Forgets the last accepted press.</summary>
		public void Reset() {
			lock(_lock) {
				_hasAccepted=false;
			}
		}
	}
}
