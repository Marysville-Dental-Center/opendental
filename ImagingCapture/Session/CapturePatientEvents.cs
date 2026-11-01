// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase;

namespace ImagingCapture.Session {
	///<summary>Where the capture session hears of patient changes in the main window (design 6.4, 7.6): ODEvent, which ImagingCapture can see
	///while FormOpenDental cannot be referenced. A seam, so that tests raise their own events, from any thread, without the static ODEvent.</summary>
	public interface ICapturePatientEvents {
		void Subscribe(ODEventHandler handler);
		void Unsubscribe(ODEventHandler handler);
	}

	///<summary>ODEvent.Fired. Its handlers run on whatever thread fired the event.</summary>
	public sealed class ODEventCapturePatientEvents:ICapturePatientEvents {
		public void Subscribe(ODEventHandler handler) {
			ODEvent.Fired+=handler;
		}

		public void Unsubscribe(ODEventHandler handler) {
			ODEvent.Fired-=handler;
		}
	}
}
