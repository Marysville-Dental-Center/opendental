// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ImagingCapture.Uvc {
	///<summary>Hands frames from one writer (the streaming thread) to one consumer (the UI) without sharing a buffer between them.
	///Three buffers rotate: the writer fills the back buffer and publishes it as the pending frame; the consumer takes the
	///pending frame as its front frame. The writer never touches the pending or front frame, and the consumer's front frame is
	///never handed back to the writer until the consumer takes a newer one. A pending frame that is not taken before the next
	///publish is overwritten (skipped), so a slow consumer never builds a queue. Buffers are reused, so a steady stream
	///allocates nothing.</summary>
	internal sealed class FrameExchange {
		private readonly object _lock=new object();
		///<summary>Writer only, outside the lock.</summary>
		private UvcFrame _frameBack;
		private UvcFrame _framePending;
		private UvcFrame _frameFront;
		private bool _hasPending;
		private long _sequence;
		private long _countSkipped;

		///<summary>Frames published.</summary>
		public long CountPublished {
			get {
				lock(_lock) {
					return _sequence;
				}
			}
		}

		///<summary>Frames published and then overwritten before the consumer took them.</summary>
		public long CountSkipped {
			get {
				lock(_lock) {
					return _countSkipped;
				}
			}
		}

		///<summary>Writer: the buffer to fill next, width x height. Reallocated when the size changes. Call Publish when it is filled.</summary>
		public UvcFrame GetBackBuffer(int width,int height) {
			if(_frameBack==null || _frameBack.Width!=width || _frameBack.Height!=height) {
				_frameBack=new UvcFrame(width,height);
			}
			return _frameBack;
		}

		///<summary>Writer: publishes the back buffer as the newest frame and numbers it. The previous pending frame, if it was not
		///taken, becomes the next back buffer.</summary>
		public void Publish(double sampleTime) {
			if(_frameBack==null) {
				throw new InvalidOperationException("GetBackBuffer was not called.");
			}
			lock(_lock) {
				_sequence++;
				_frameBack.Sequence=_sequence;
				_frameBack.SampleTime=sampleTime;
				if(_hasPending) {
					_countSkipped++;
				}
				UvcFrame frame=_framePending;
				_framePending=_frameBack;
				_frameBack=frame;
				_hasPending=true;
			}
		}

		///<summary>Consumer: the newest published frame not taken yet, or null. It stays valid until the next TakeLatest.</summary>
		public UvcFrame TakeLatest() {
			lock(_lock) {
				if(!_hasPending) {
					return null;
				}
				UvcFrame frame=_frameFront;
				_frameFront=_framePending;
				_framePending=frame;
				_hasPending=false;
				return _frameFront;
			}
		}
	}
}
