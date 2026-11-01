// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ImagingCapture.Metadata {
	///<summary>One image's capture metadata, schema capture-metadata/1 (docs/specs/capture-metadata-v1.md, design 7.9), as the source sent
	///it in TWEI_TWAINDIRECTMETADATA. CaptureMetadataParser builds it; a null CaptureMetadata means the image has none. Every field is
	///optional to the reader: a field that is missing, null or of the wrong type reads as null (false for Redelivered), and a value that
	///breaks the schema's rules is listed in ListWarnings. Nothing here is a measurement the fork made.</summary>
	public sealed class CaptureMetadata {
		///<summary>The schema value this reader accepts. Additive changes keep it; a breaking change gets a higher major number, which this
		///reader treats as no metadata.</summary>
		public const string SchemaV1="capture-metadata/1";
		///<summary>rendering of a 16-bit image, or of an 8-bit image as the sensor software wrote it.</summary>
		public const string RenderingOriginal="original";
		///<summary>rendering: the sensor software's standard 8-bit display rendering.</summary>
		public const string RenderingVendor8="vendor8";
		///<summary>rendering: the sensor software's high-definition 8-bit enhancement.</summary>
		public const string RenderingVendor8Hd="vendor8-hd";
		///<summary>rendering: the source's own linear window of the 16-bit image.</summary>
		public const string RenderingWindow8="window8";
		public const string ByteOrderLE="LE";

		///<summary>manufacturer, as the source reports it. Never interpreted by the fork.</summary>
		public string Manufacturer { get; set; }
		///<summary>model, null when the source does not know it.</summary>
		public string Model { get; set; }
		///<summary>serial: the sensor's serial number.</summary>
		public string Serial { get; set; }
		///<summary>captureId: unique per image and the same when the image is delivered again (W15). Matches a redelivered image to an
		///earlier journal or quarantine copy. Null when missing or empty.</summary>
		public string CaptureId { get; set; }
		///<summary>sequence: the 1-based number of the image within its source session. A redelivered image keeps its first number. Null when
		///missing or not a whole number of 1 or more.</summary>
		public int? Sequence { get; set; }
		///<summary>acquiredAt exactly as sent, for display (a redelivered image is shown with it, design 7.12). Null when missing.</summary>
		public string AcquiredAtText { get; set; }
		///<summary>acquiredAt, when it is ISO 8601 with a UTC offset (or Z). Null otherwise.</summary>
		public DateTimeOffset? AcquiredAt { get; set; }
		///<summary>pixelSpacingMm[0]: the horizontal distance between pixel centres, in millimetres. Null unless the array holds two positive
		///numbers.</summary>
		public double? PixelSpacingXMm { get; set; }
		///<summary>pixelSpacingMm[1]: the vertical distance between pixel centres, in millimetres.</summary>
		public double? PixelSpacingYMm { get; set; }
		///<summary>bitsStored: 8 or 16, as delivered.</summary>
		public int? BitsStored { get; set; }
		///<summary>effectiveBits: how many low bits of a 16-bit sample are significant. Null until the source's maker has measured it.</summary>
		public int? EffectiveBits { get; set; }
		///<summary>byteOrder: "LE" at 16 bits, null at 8 bits.</summary>
		public string ByteOrder { get; set; }
		///<summary>photometric, for example "MONOCHROME2".</summary>
		public string Photometric { get; set; }
		///<summary>window: the source's own display window of a 16-bit image, null at 8 bits. The fork computes its own window for DICOM.</summary>
		public CaptureMetadataWindow Window { get; set; }
		///<summary>rendering: which delivered rendering the image is (the Rendering constants). Kept as sent when it is another string.</summary>
		public string Rendering { get; set; }
		///<summary>adjustments: present only when the source's installation adjustments were applied to the image. Null otherwise.</summary>
		public CaptureMetadataAdjustments Adjustments { get; set; }
		///<summary>detectorType, for example "SCINTILLATOR", or null.</summary>
		public string DetectorType { get; set; }
		///<summary>redelivered: true for an image delivered again under W15. False when missing (the schema says it is always present; a
		///missing one is listed in ListWarnings).</summary>
		public bool Redelivered { get; set; }
		///<summary>configuredExposure: exposure values typed into the sensor software, never measured. Null when nothing is set.</summary>
		public CaptureMetadataExposure ConfiguredExposure { get; set; }
		///<summary>dsVersion: the source's version text.</summary>
		public string DsVersion { get; set; }
		///<summary>Sentences about values that break the schema, for the capture log. They never make the metadata absent.</summary>
		public List<string> ListWarnings { get; set; }=new List<string>();

		///<summary>Adjustments are present and rotate or flip is not neutral, so the slot's own rotation would be applied on top of the
		///source's. The session window warns (design 7.9, 7.10).</summary>
		public bool HasOrientationAdjustment => Adjustments!=null && !Adjustments.IsOrientationNeutral;
	}

	///<summary>capture-metadata/1 window {low, high}: raw sample values.</summary>
	public sealed class CaptureMetadataWindow {
		public int Low { get; set; }
		public int High { get; set; }
	}

	///<summary>capture-metadata/1 adjustments {brightness, contrast, rotate, flip}: the source's installation adjustments that were applied to
	///the delivered 8-bit image.</summary>
	public sealed class CaptureMetadataAdjustments {
		public const string FlipNone="none";
		public const string FlipHorizontal="h";
		public const string FlipVertical="v";
		public const string FlipBoth="both";

		///<summary>brightness, 0-100, where 50 means unchanged. Null when missing or not a whole number.</summary>
		public int? Brightness { get; set; }
		///<summary>contrast, 0-100, where 50 means unchanged.</summary>
		public int? Contrast { get; set; }
		///<summary>rotate: clockwise degrees, one of 0, 90, 180 or 270.</summary>
		public int? Rotate { get; set; }
		///<summary>flip: none, h, v or both (lowercase), applied after the rotation. Kept as sent (lowercased) when it is another string.</summary>
		public string Flip { get; set; }
		///<summary>rotate is missing, null or 0, and flip is missing, null or "none". A value that cannot be read counts as not neutral.</summary>
		public bool IsOrientationNeutral { get; set; }=true;
		///<summary>IsOrientationNeutral, and brightness and contrast are each missing, null or 50.</summary>
		public bool IsNeutral { get; set; }=true;
	}

	///<summary>capture-metadata/1 configuredExposure: values the operator typed into the sensor software, never measured (source
	///"configured"). A value that is not set is missing, never null.</summary>
	public sealed class CaptureMetadataExposure {
		public const string SourceConfigured="configured";

		///<summary>kv, in kV.</summary>
		public double? Kv { get; set; }
		///<summary>ma, in mA.</summary>
		public double? Ma { get; set; }
		///<summary>s, the exposure time in seconds.</summary>
		public double? S { get; set; }
		///<summary>dap, the dose-area product in mGy.cm2 (mGy*cm^2 written in ASCII).</summary>
		public double? Dap { get; set; }
		///<summary>source, "configured" in this schema.</summary>
		public string Source { get; set; }

		public bool HasAnyValue => Kv!=null || Ma!=null || S!=null || Dap!=null;

		///<summary>The set values with their units, in the order kV, mA, s, DAP, for example "70 kV, 7 mA, 0.125 s, DAP 12.5 mGy.cm2". ASCII
		///only (UCUM-style units), for DICOM ImageComments and the security log. "" when nothing is set.</summary>
		public string ToText() {
			List<string> listParts=new List<string>();
			if(Kv!=null) {
				listParts.Add(FormatNumber(Kv.Value)+" kV");
			}
			if(Ma!=null) {
				listParts.Add(FormatNumber(Ma.Value)+" mA");
			}
			if(S!=null) {
				listParts.Add(FormatNumber(S.Value)+" s");
			}
			if(Dap!=null) {
				listParts.Add("DAP "+FormatNumber(Dap.Value)+" mGy.cm2");
			}
			return string.Join(", ",listParts);
		}

		internal static string FormatNumber(double value) {
			return value.ToString("0.######",CultureInfo.InvariantCulture);
		}
	}
}
