// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ImagingDeviceManager.Capture;

namespace ImagingCapture.Settings {
	///<summary>The SHA-256 hash of the [Device.N] settings that change the stored pixels of a capture: Bits, ByteOrder16 and Polarity (owner
	///decision 2026-11-01: a Test capture verification record covers every computer that runs the same driver version with the same image
	///settings). Unlike GatedSettingsHash it is computed from the values in effect (TwainCaptureOptions, after its defaults and warnings), so
	///two files that read the same hash the same: a missing Bits and Bits=16, "le" and "LE". Settings that do not change the pixels (the session
	///mode, the source gate, the journal, the custom capabilities, the timeouts) are not part of it, so a record still matches after they
	///change; the per-computer settings check of design 9.3 (CaptureSettingsCheck) is a separate gate that covers them. The text starts with
	///FormatName, so a later change of these rules gives different hashes.</summary>
	public static class CaptureImageSettingsHash {
		///<summary>The first line of the canonical text.</summary>
		public const string FormatName="capture-image-settings/1";
		///<summary>The [Device.N] keys it covers, in hash order.</summary>
		public static readonly ReadOnlyCollection<string> ListKeys=new ReadOnlyCollection<string>(new[] { "Bits","ByteOrder16","Polarity" });

		///<summary>The hash of the image settings in options for options.ImagingDeviceNum: 64 lowercase hex digits. Throws ArgumentNullException
		///for null and ArgumentOutOfRangeException when the device number is not positive.</summary>
		public static string Compute(TwainCaptureOptions options) {
			if(options==null) {
				throw new ArgumentNullException(nameof(options));
			}
			return GatedSettingsHash.ComputeFromCanonicalText(GetCanonicalText(options.ImagingDeviceNum,options.Bits,options.ByteOrder16,options.Polarity));
		}

		///<summary>"capture-image-settings/1\nDevice=12\nBits=16\nByteOrder16=LE\nPolarity=AsDelivered\n". Bits is normalized as the library reads it
		///(a depth that is not 8, 14 or 16 counts as 8).</summary>
		public static string GetCanonicalText(long imagingDeviceNum,int bits,TwainByteOrder byteOrder16,TwainPolarity polarity) {
			if(imagingDeviceNum<=0) {
				throw new ArgumentOutOfRangeException(nameof(imagingDeviceNum),"An ImagingDeviceNum is positive.");
			}
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(FormatName).Append('\n');
			stringBuilder.Append("Device=").Append(imagingDeviceNum.ToString(CultureInfo.InvariantCulture)).Append('\n');
			stringBuilder.Append("Bits=").Append(TwainBitDepth.Normalize(bits).ToString(CultureInfo.InvariantCulture)).Append('\n');
			stringBuilder.Append("ByteOrder16=").Append(byteOrder16.ToString()).Append('\n');
			stringBuilder.Append("Polarity=").Append(polarity.ToString()).Append('\n');
			return stringBuilder.ToString();
		}
	}
}
