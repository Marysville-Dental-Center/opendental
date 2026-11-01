// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Diagnostics;
using System.Threading;

namespace ImagingCapture.Session {
	///<summary>How the capture session controller gets back to its UI thread, and off it. TwainCaptureSession raises its events on its worker
	///thread; the controller posts them here, so that every decision, save and database write runs on the UI thread (design 7.1 "Database and
	///document writes happen on the UI thread"). Tests use one that runs everything at once, in order.</summary>
	public interface ICaptureDispatcher {
		///<summary>Runs action later on the UI thread, in the order posted. Any thread. Must not run it inside the call when called from the UI
		///thread itself, so that a handler never re-enters the controller.</summary>
		void Post(Action action);
		///<summary>Runs action on a thread that is not the UI thread, for TwainCaptureSession.Open, which blocks until the source is open
		///(design 7.1 CAP-3; FrmTestCapture opens on a thread-pool thread the same way). The action posts its own result back.</summary>
		void RunInBackground(Action action);
	}

	///<summary>The UI thread's SynchronizationContext (WinForms or WPF), captured when this is created, and the thread pool. Create it on the
	///UI thread.</summary>
	public sealed class SynchronizationContextCaptureDispatcher:ICaptureDispatcher {
		private readonly SynchronizationContext _synchronizationContext;

		///<summary>Throws InvalidOperationException when the calling thread has no SynchronizationContext (it is not a UI thread).</summary>
		public SynchronizationContextCaptureDispatcher() {
			_synchronizationContext=SynchronizationContext.Current
				??throw new InvalidOperationException("The capture session must be started on a UI thread.");
		}

		public void Post(Action action) {
			if(action==null) {
				return;
			}
			_synchronizationContext.Post(state => action(),null);
		}

		public void RunInBackground(Action action) {
			if(action==null) {
				return;
			}
			System.Threading.Tasks.Task.Run(action);//Task is named in full: OpenDentBusiness has a Task table type
		}
	}

	///<summary>The capture session's clock: local time for dates (the mount's date, the receive time of an image) and a monotonic millisecond
	///count for the timing rules (the 2 s in which key presses are ignored, the held Confirm key), which must not jump when the wall clock is
	///set.</summary>
	public interface ICaptureClock {
		DateTime Now { get; }
		///<summary>Milliseconds since an arbitrary start; never goes back.</summary>
		long NowMs { get; }
	}

	///<summary>DateTime.Now and a Stopwatch.</summary>
	public sealed class SystemCaptureClock:ICaptureClock {
		private readonly Stopwatch _stopwatch=Stopwatch.StartNew();

		public DateTime Now => DateTime.Now;

		public long NowMs => _stopwatch.ElapsedMilliseconds;
	}
}
