// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;

namespace ImagingCapture.Uvc.DirectShow {
	///<summary>The DirectShow camera backend: any device in the video input category, through the operating system's own
	///DirectShow components. DirectShow is the public Windows API that delivers the UVC still-pin trigger and exposes
	///IAMCameraControl on the same source filter (design 8.1). Stateless; one instance can serve any number of sources.</summary>
	public sealed class DirectShowUvcBackend:IUvcBackend {
		public string Name {
			get {
				return "DirectShow";
			}
		}

		public List<UvcDeviceInfo> GetDevices() {
			return DeviceEnumerator.GetDevices();
		}

		public IFrameSource Open(UvcDeviceInfo device) {
			if(device==null) {
				throw new ArgumentNullException(nameof(device));
			}
			return DirectShowFrameSource.Open(device);
		}
	}
}
