// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.Serialization;

namespace ImagingDeviceManager {
	///<summary>Thrown for imaging device failures that callers should report to the user.</summary>
	[Serializable]
	public class ImagingDeviceException:Exception {
		///<summary>The EZTwain-style error code associated with the failure, or EZTEC_NONE.</summary>
		public EZTwainErrorCode ErrorCode { get; }

		public ImagingDeviceException() {
		}

		public ImagingDeviceException(string message):base(message) {
		}

		public ImagingDeviceException(string message,Exception innerException):base(message,innerException) {
		}

		public ImagingDeviceException(string message,EZTwainErrorCode errorCode):base(message) {
			ErrorCode=errorCode;
		}

		protected ImagingDeviceException(SerializationInfo info,StreamingContext context):base(info,context) {
			ErrorCode=(EZTwainErrorCode)info.GetInt32(nameof(ErrorCode));
		}

		public override void GetObjectData(SerializationInfo info,StreamingContext context) {
			base.GetObjectData(info,context);
			info.AddValue(nameof(ErrorCode),(int)ErrorCode);
		}
	}
}
