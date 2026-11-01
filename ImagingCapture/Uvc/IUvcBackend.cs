// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;

namespace ImagingCapture.Uvc {
	///<summary>A camera API. The production implementation is DirectShowUvcBackend. Tests supply a fake. The seam leaves room
	///for another backend (for example Media Foundation) without touching the window or the triggers.</summary>
	public interface IUvcBackend {
		///<summary>Short name for logs, for example "DirectShow".</summary>
		string Name { get; }

		///<summary>The video capture devices present, in the order the system lists them. Empty when there are none. Throws
		///UvcException only when device enumeration itself is unavailable.</summary>
		List<UvcDeviceInfo> GetDevices();

		///<summary>Opens a device: the stream formats, the still pin and the camera controls become available, but nothing streams
		///until Start. Throws UvcException (NoDevice, InUse, AccessDenied, NoCapturePin, NotAvailable, Failed). The caller
		///disposes the result.</summary>
		IFrameSource Open(UvcDeviceInfo device);
	}

	///<summary>One opened camera.
	///
	///Threading: members may be called from any thread. FrameReady and StillCaptured are raised on the backend's streaming
	///threads: their handlers must return quickly, must not wait on the UI thread (use BeginInvoke, never Invoke), and must not
	///call Start, Stop, ReadControl, ReadControlRange or Dispose (those throw InvalidOperationException there). Error is raised
	///on a thread-pool thread, and its handler may call any member.
	///
	///Frames: each new preview frame is published and FrameReady is raised. The consumer calls TakeLatestFrame, which returns
	///the newest frame not yet taken, or null. Frames that arrive while the consumer is busy replace the untaken one, so a slow
	///consumer skips frames instead of queueing them. The streaming thread never writes into a frame the consumer holds.</summary>
	public interface IFrameSource:IDisposable {
		UvcDeviceInfo Device { get; }

		///<summary>The stream formats of the capture pin, in the order the device reports them, without duplicates. Read once at Open.</summary>
		IReadOnlyList<UvcMode> Modes { get; }

		///<summary>True when the device has a still-image pin (PIN_CATEGORY_STILL).</summary>
		bool HasStillPin { get; }

		bool IsRunning { get; }

		///<summary>The mode passed to the first Start, or null before Start or when Start used the device's current format.</summary>
		UvcMode ModeStarted { get; }

		///<summary>True when Start was asked to connect the still pin and that succeeded.</summary>
		bool IsStillPinConnected { get; }

		///<summary>Preview frames received since Open.</summary>
		long FramesReceived { get; }

		///<summary>Selects the stream format and starts streaming. mode is one of Modes, or null to keep the device's current format.
		///When connectStillPin is true and the device has a still pin, still samples are delivered through StillCaptured; preview
		///works whether or not that succeeds. The format and the still choice are fixed by the first call: a later call after
		///Stop resumes with them, and asking for another mode or still choice throws InvalidOperationException (open the device
		///again instead). Throws UvcException (InUse, AccessDenied, DeviceLost, NotAvailable, Failed).</summary>
		void Start(UvcMode mode,bool connectStillPin);

		///<summary>Stops streaming. Does nothing when not running.</summary>
		void Stop();

		///<summary>The newest preview frame not yet taken, or null. The frame stays valid until the next call to TakeLatestFrame;
		///copy it (UvcFrame.ToBitmap or Clone) to keep it longer. Meant for one consumer.</summary>
		UvcFrame TakeLatestFrame();

		///<summary>Reads a camera control with Get. Never writes. An invalid property number gives Unsupported without a call.</summary>
		UvcControlValue ReadControl(UvcControlInterface controlInterface,int property);

		///<summary>Reads a camera control's range with GetRange. Never writes.</summary>
		UvcControlRange ReadControlRange(UvcControlInterface controlInterface,int property);

		///<summary>A preview frame was published. See TakeLatestFrame.</summary>
		event EventHandler FrameReady;

		///<summary>The still pin delivered a sample. The frame in the event belongs to the handler.</summary>
		event EventHandler<UvcStillEventArgs> StillCaptured;

		///<summary>The device was lost or streaming stopped with an error. Dispose the source and open the device again to recover.</summary>
		event EventHandler<UvcErrorEventArgs> Error;
	}

	///<summary>A still-pin sample.</summary>
	public sealed class UvcStillEventArgs:EventArgs {
		///<summary>A new frame that no one else references.</summary>
		public UvcFrame Frame { get; }

		public UvcStillEventArgs(UvcFrame frame) {
			Frame=frame;
		}
	}
}
