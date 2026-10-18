// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
namespace ImagingDeviceManager {
	///<summary>How an imaging device is driven.</summary>
	public enum EnumImgDeviceControlType {
		///<summary>A TWAIN device that delivers one image per acquire.</summary>
		Twain,
		///<summary>XDR sensor. Not supported: XDR capture was never functional and there is no implementation behind this value.
		///It only exists so that imaging device types stored in the database still map to something.</summary>
		XDR,
		///<summary>A TWAIN device that can deliver several images per session, such as an intraoral sensor series.</summary>
		TwainMulti,
	}

	///<summary>Status of an imaging device.</summary>
	public enum EnumDeviceStatus {
		///<summary>The status is not known.</summary>
		Unknown,
		///<summary>The device is ready to capture.</summary>
		Ready,
		///<summary>The device is capturing or transferring.</summary>
		Busy,
		///<summary>The device is not connected or not installed.</summary>
		Unavailable,
		///<summary>The device reported an error.</summary>
		Error,
	}
}
