// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ImagingDeviceManager.Capture {
	///<summary>The capture session's TWAIN side (design 7.1 CAP-3), as TwainCaptureSession implements it, so that the session window can be
	///tested against a stand-in.
	///
	///Threading contract:
	///- The source is driven on one dedicated STA worker thread with its own message loop and proxy window. The thread-modal rule disables
	///  only that thread's windows, so the caller's windows stay usable.
	///- Open blocks the caller until the source is open (state 4) or the open failed; it must not be called from an event handler.
	///- Enable, RequestRedelivery, Disable, Cancel and BeginClose only post a command to the worker and return at once; they may be called
	///  from any thread, event handlers included. Commands run in the order posted.
	///- Every event is raised on the worker thread. Handlers must return quickly (the source waits) and must not block on the caller's UI
	///  thread; the session window marshals them itself. A handler that throws is logged and otherwise ignored.
	///- The worker writes the journal copy of every image before it raises ImageAcquired. After BeginClose it raises no more ImageAcquired:
	///  it stops the source in the safe order itself, and writes every later image straight to the quarantine (file and sidecar, with
	///  QuarantineContext), raising ImageQuarantined instead.</summary>
	public interface ITwainCaptureSession:IDisposable {
		TwainCaptureState State { get; }
		///<summary>The session's GUID, for the quarantine folder (Capture\quarantine\&lt;guid&gt;), the sidecars and the capture log. A new one
		///per instance; the session window may set its own before Open.</summary>
		Guid SessionGuid { get; set; }
		///<summary>The ProductName of the open source, or "".</summary>
		string ProductName { get; }
		TwainCaptureOptions Options { get; }
		///<summary>What the source reported at Open, or null.</summary>
		TwainCaptureSourceInfo SourceInfo { get; }
		///<summary>The patient the session is bound to, for quarantine sidecars. May be replaced at any time; each image uses the one in effect
		///when it arrived, read once.</summary>
		TwainCaptureQuarantineContext QuarantineContext { get; set; }
		///<summary>The caller's metadata parser, called on the worker for each image's MetadataJson (which may be null) before its journal copy
		///is written, so that the sidecar records captureId and sequence. Null: they are not recorded. Must not throw.</summary>
		Func<string,CaptureMetadataKeys> FuncReadMetadataKeys { get; set; }

		///<summary>Opens productName on the worker and keeps it open in state 4 across enables. Fails, without throwing, when the session is
		///not closed (AlreadyOpen), or when another session of this program has the same source open (SourceBusy). Before it opens, it waits up
		///to OpenTimeout for another session of this program that is still closing that source.</summary>
		TwainCaptureOpenResult Open(string productName,TwainCaptureOptions options);
		///<summary>Posts an enable: state 4 negotiation (CAP_XFERCOUNT=1, bit depth, ICAP_EXTIMAGEINFO, the device profile's custom
		///capabilities), then MSG_ENABLEDS. Returns the enable id that tags its images and its Ended, or 0 when the session is not open (nothing
		///is raised then).</summary>
		int Enable(TwainEnableRequest request);
		///<summary>Posts a redelivery enable (W15): the device profile's RedeliverCap is set TRUE in state 4 right before MSG_ENABLEDS, and FALSE
		///again as soon as the enable ends. Without a RedeliverCap, or when the source does not accept it, nothing is enabled and Ended reports
		///RedeliveryUnavailable. Returns the enable id, or 0 when the session is not open.</summary>
		int RequestRedelivery(TwainEnableRequest request);
		///<summary>Posts a stop of the running enable in the safe order (state 7: MSG_ENDXFER; state 6: every pending image transferred and
		///raised as Drained with this reason; state 5: MSG_DISABLEDS, with the bounded wait after a refused disable). Ignored when nothing is
		///enabled.</summary>
		void Disable(TwainCaptureStopReason reason);
		///<summary>Disable(Cancel).</summary>
		void Cancel();
		///<summary>Starts closing: from now on no ImageAcquired is raised; the worker stops a running enable in the safe order, quarantines what
		///arrives (also an image whose journal copy was being written when BeginClose was called), closes the source and the DSM, and ends.
		///Does not wait for the worker; it waits only for an ImageAcquired handler that is running, so that none runs once it has returned.</summary>
		void BeginClose();
		///<summary>Waits until the worker has ended (true), or the timeout passed (false). True when no worker runs.</summary>
		bool WaitForClose(TimeSpan timeout);
		///<summary>Writes image to the session quarantine with QuarantineContext (or "patient unknown"), for the window's own quarantine moves
		///(a patient change, a failed save, a declined redelivered image). Any thread. Returns the path, or null with error.</summary>
		string QuarantineImage(AcquiredImage image,string reason,bool isPatientUnknown,out string error);

		///<summary>One image, its journal copy written.</summary>
		event Action<AcquiredImage> ImageAcquired;
		///<summary>The end of one enable.</summary>
		event Action<TwainCaptureEnded> Ended;
		event Action<TwainCaptureStatus> StatusChanged;
		///<summary>After BeginClose: an image the worker put in the quarantine itself.</summary>
		event Action<AcquiredImage> ImageQuarantined;
		///<summary>A line of the capture log (also written to Capture\capture-log.txt).</summary>
		event Action<string> Logged;
	}
}
