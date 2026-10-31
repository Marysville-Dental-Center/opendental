// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ImagingCapture.Uvc {
	///<summary>A video capture device as the backend lists it. Immutable. Devices are told apart by data the operating system
	///reports, never by matching names against a list.</summary>
	public sealed class UvcDeviceInfo {
		///<summary>The name the driver reports, for display. Two identical cameras can have the same name.</summary>
		public string FriendlyName { get; }
		///<summary>The device interface path, or "" when the device has none (for example a software camera).</summary>
		public string DevicePath { get; }
		///<summary>The DirectShow moniker display name. Unique among the devices present, and stable for one device on one USB
		///port. Saved as [Camera] Moniker in capture.ini.</summary>
		public string MonikerDisplayName { get; }

		public UvcDeviceInfo(string friendlyName,string devicePath,string monikerDisplayName) {
			FriendlyName=friendlyName??"";
			DevicePath=devicePath??"";
			MonikerDisplayName=monikerDisplayName??"";
		}

		///<summary>True when both describe the same device: the same moniker display name, or else the same non-empty device path.
		///Comparisons ignore case.</summary>
		public bool IsSameDevice(UvcDeviceInfo other) {
			if(other==null) {
				return false;
			}
			if(MonikerDisplayName!="" && string.Equals(MonikerDisplayName,other.MonikerDisplayName,StringComparison.OrdinalIgnoreCase)) {
				return true;
			}
			return DevicePath!="" && string.Equals(DevicePath,other.DevicePath,StringComparison.OrdinalIgnoreCase);
		}

		public override string ToString() {
			return FriendlyName;
		}
	}
}
