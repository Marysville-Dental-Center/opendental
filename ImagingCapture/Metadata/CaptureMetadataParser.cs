// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ImagingDeviceManager.Capture;

namespace ImagingCapture.Metadata {
	///<summary>Reads capture-metadata/1 (docs/specs/capture-metadata-v1.md) from the raw TWEI_TWAINDIRECTMETADATA text of an image
	///(AcquiredImage.MetadataJson). It takes the first JSON object anywhere in the document, in document order, whose "schema" member is
	///the string "capture-metadata/1": the top-level object (as twain-contract 1.1 sends it) or one nested at any depth (as a TWAIN Direct
	///wrapping would, design 7.9). Anything else counts as no metadata: null or empty text, malformed JSON, and JSON with no such object,
	///including one whose schema has a higher major number. Inside the object, unknown members are ignored and a member that is null or of
	///the wrong type reads as absent. No member throws.</summary>
	public static class CaptureMetadataParser {
		private static readonly Regex _regexIsoDateTime=new Regex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})$",
			RegexOptions.CultureInvariant);

		///<summary>The image's capture metadata, or null when it has none.</summary>
		public static CaptureMetadata Parse(string json) {
			try {
				JsonValue jsonValueRoot=JsonReader.Parse(json);
				if(jsonValueRoot==null) {
					return null;
				}
				JsonValue jsonValueObject=FindSchemaObject(jsonValueRoot);
				if(jsonValueObject==null) {
					return null;
				}
				return Read(jsonValueObject);
			}
			catch(Exception) {
				return null;//never expected; a metadata problem must never stop a capture
			}
		}

		///<summary>captureId and sequence, for TwainCaptureSession.FuncReadMetadataKeys (the journal and quarantine sidecars, design 7.9).
		///Null when the image has no capture metadata.</summary>
		public static CaptureMetadataKeys ReadKeys(string json) {
			CaptureMetadata captureMetadata=Parse(json);
			if(captureMetadata==null) {
				return null;
			}
			return new CaptureMetadataKeys { CaptureId=captureMetadata.CaptureId,Sequence=captureMetadata.Sequence };
		}

		///<summary>The first object, in document order (pre-order), whose "schema" member is the string SchemaV1. Null when there is none.
		///JsonReader bounds the depth, so the recursion is bounded too.</summary>
		internal static JsonValue FindSchemaObject(JsonValue jsonValue) {
			if(jsonValue==null) {
				return null;
			}
			if(jsonValue.Kind==JsonKind.Object) {
				JsonValue jsonValueSchema=jsonValue.Get("schema");
				if(jsonValueSchema!=null && jsonValueSchema.Kind==JsonKind.String && jsonValueSchema.Text==CaptureMetadata.SchemaV1) {
					return jsonValue;
				}
				foreach(KeyValuePair<string,JsonValue> keyValuePair in jsonValue.ListMembers) {
					JsonValue jsonValueFound=FindSchemaObject(keyValuePair.Value);
					if(jsonValueFound!=null) {
						return jsonValueFound;
					}
				}
				return null;
			}
			if(jsonValue.Kind==JsonKind.Array) {
				foreach(JsonValue jsonValueItem in jsonValue.ListItems) {
					JsonValue jsonValueFound=FindSchemaObject(jsonValueItem);
					if(jsonValueFound!=null) {
						return jsonValueFound;
					}
				}
			}
			return null;
		}

		private static CaptureMetadata Read(JsonValue jsonObject) {
			CaptureMetadata captureMetadata=new CaptureMetadata();
			captureMetadata.Manufacturer=ReadText(jsonObject,"manufacturer",captureMetadata);
			captureMetadata.Model=ReadText(jsonObject,"model",captureMetadata);
			captureMetadata.Serial=ReadText(jsonObject,"serial",captureMetadata);
			string captureId=ReadText(jsonObject,"captureId",captureMetadata);
			captureMetadata.CaptureId=string.IsNullOrEmpty(captureId) ? null : captureId;
			int? sequence=ReadInt(jsonObject,"sequence",captureMetadata);
			if(sequence!=null && sequence.Value<1) {
				captureMetadata.ListWarnings.Add("sequence is not 1 or more: "+sequence.Value.ToString(CultureInfo.InvariantCulture));
				sequence=null;
			}
			captureMetadata.Sequence=sequence;
			ReadAcquiredAt(jsonObject,captureMetadata);
			ReadPixelSpacing(jsonObject,captureMetadata);
			captureMetadata.BitsStored=ReadInt(jsonObject,"bitsStored",captureMetadata);
			captureMetadata.EffectiveBits=ReadInt(jsonObject,"effectiveBits",captureMetadata);
			if(captureMetadata.EffectiveBits!=null && (captureMetadata.EffectiveBits<1 || captureMetadata.EffectiveBits>16)) {
				captureMetadata.ListWarnings.Add("effectiveBits is not 1-16: "+captureMetadata.EffectiveBits.Value.ToString(CultureInfo.InvariantCulture));
				captureMetadata.EffectiveBits=null;
			}
			captureMetadata.ByteOrder=ReadText(jsonObject,"byteOrder",captureMetadata);
			captureMetadata.Photometric=ReadText(jsonObject,"photometric",captureMetadata);
			ReadWindow(jsonObject,captureMetadata);
			captureMetadata.Rendering=ReadText(jsonObject,"rendering",captureMetadata);
			if(captureMetadata.Rendering!=null && captureMetadata.Rendering!=CaptureMetadata.RenderingOriginal
				&& captureMetadata.Rendering!=CaptureMetadata.RenderingVendor8 && captureMetadata.Rendering!=CaptureMetadata.RenderingVendor8Hd
				&& captureMetadata.Rendering!=CaptureMetadata.RenderingWindow8)
			{
				captureMetadata.ListWarnings.Add("rendering is not a capture-metadata/1 value: "+captureMetadata.Rendering);
			}
			ReadAdjustments(jsonObject,captureMetadata);
			captureMetadata.DetectorType=ReadText(jsonObject,"detectorType",captureMetadata);
			JsonValue jsonValueRedelivered=jsonObject.Get("redelivered");
			if(jsonValueRedelivered!=null && jsonValueRedelivered.Kind==JsonKind.Bool) {
				captureMetadata.Redelivered=jsonValueRedelivered.Bool;
			}
			else {
				captureMetadata.ListWarnings.Add("redelivered is missing or not true/false; read as false.");
			}
			ReadExposure(jsonObject,captureMetadata);
			captureMetadata.DsVersion=ReadText(jsonObject,"dsVersion",captureMetadata);
			return captureMetadata;
		}

		private static void ReadAcquiredAt(JsonValue jsonObject,CaptureMetadata captureMetadata) {
			string text=ReadText(jsonObject,"acquiredAt",captureMetadata);
			captureMetadata.AcquiredAtText=text;
			if(text==null) {
				return;
			}
			DateTimeOffset dateTimeOffset;
			if(_regexIsoDateTime.IsMatch(text)
				&& DateTimeOffset.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.None,out dateTimeOffset))
			{
				captureMetadata.AcquiredAt=dateTimeOffset;
				return;
			}
			captureMetadata.ListWarnings.Add("acquiredAt is not ISO 8601 with a UTC offset: "+text);
		}

		private static void ReadPixelSpacing(JsonValue jsonObject,CaptureMetadata captureMetadata) {
			JsonValue jsonValue=jsonObject.Get("pixelSpacingMm");
			if(jsonValue==null || jsonValue.Kind==JsonKind.Null) {
				return;
			}
			double x;
			double y;
			if(jsonValue.Kind==JsonKind.Array && jsonValue.ListItems.Count==2 && jsonValue.ListItems[0].TryGetDouble(out x)
				&& jsonValue.ListItems[1].TryGetDouble(out y) && x>0 && y>0)
			{
				captureMetadata.PixelSpacingXMm=x;
				captureMetadata.PixelSpacingYMm=y;
				return;
			}
			captureMetadata.ListWarnings.Add("pixelSpacingMm is not two positive numbers.");
		}

		private static void ReadWindow(JsonValue jsonObject,CaptureMetadata captureMetadata) {
			JsonValue jsonValue=jsonObject.Get("window");
			if(jsonValue==null || jsonValue.Kind==JsonKind.Null) {
				return;
			}
			int low;
			int high;
			JsonValue jsonValueLow=jsonValue.Get("low");
			JsonValue jsonValueHigh=jsonValue.Get("high");
			if(jsonValueLow!=null && jsonValueHigh!=null && jsonValueLow.TryGetInt(out low) && jsonValueHigh.TryGetInt(out high)) {
				captureMetadata.Window=new CaptureMetadataWindow { Low=low,High=high };
				return;
			}
			captureMetadata.ListWarnings.Add("window is not {low, high} with whole numbers.");
		}

		private static void ReadAdjustments(JsonValue jsonObject,CaptureMetadata captureMetadata) {
			JsonValue jsonValue=jsonObject.Get("adjustments");
			if(jsonValue==null || jsonValue.Kind==JsonKind.Null) {
				return;
			}
			CaptureMetadataAdjustments adjustments=new CaptureMetadataAdjustments();
			if(jsonValue.Kind!=JsonKind.Object) {
				//Present but unreadable: the image was adjusted in some way the fork cannot tell, so it is not neutral.
				captureMetadata.ListWarnings.Add("adjustments is not an object.");
				adjustments.IsOrientationNeutral=false;
				adjustments.IsNeutral=false;
				captureMetadata.Adjustments=adjustments;
				return;
			}
			bool isBrightnessNeutral;
			bool isContrastNeutral;
			bool isRotateNeutral;
			adjustments.Brightness=ReadAdjustmentInt(jsonValue,"brightness",50,captureMetadata,out isBrightnessNeutral);
			adjustments.Contrast=ReadAdjustmentInt(jsonValue,"contrast",50,captureMetadata,out isContrastNeutral);
			adjustments.Rotate=ReadAdjustmentInt(jsonValue,"rotate",0,captureMetadata,out isRotateNeutral);
			if(adjustments.Brightness!=null && (adjustments.Brightness<0 || adjustments.Brightness>100)) {
				captureMetadata.ListWarnings.Add("adjustments.brightness is not 0-100.");
			}
			if(adjustments.Contrast!=null && (adjustments.Contrast<0 || adjustments.Contrast>100)) {
				captureMetadata.ListWarnings.Add("adjustments.contrast is not 0-100.");
			}
			if(adjustments.Rotate!=null && adjustments.Rotate!=0 && adjustments.Rotate!=90 && adjustments.Rotate!=180 && adjustments.Rotate!=270) {
				captureMetadata.ListWarnings.Add("adjustments.rotate is not 0, 90, 180 or 270.");
			}
			bool isFlipNeutral=true;
			JsonValue jsonValueFlip=jsonValue.Get("flip");
			if(jsonValueFlip!=null && jsonValueFlip.Kind!=JsonKind.Null) {
				if(jsonValueFlip.Kind==JsonKind.String) {
					adjustments.Flip=jsonValueFlip.Text.Trim().ToLowerInvariant();
					isFlipNeutral=adjustments.Flip==CaptureMetadataAdjustments.FlipNone;
					if(adjustments.Flip!=CaptureMetadataAdjustments.FlipNone && adjustments.Flip!=CaptureMetadataAdjustments.FlipHorizontal
						&& adjustments.Flip!=CaptureMetadataAdjustments.FlipVertical && adjustments.Flip!=CaptureMetadataAdjustments.FlipBoth)
					{
						captureMetadata.ListWarnings.Add("adjustments.flip is not none, h, v or both: "+adjustments.Flip);
					}
				}
				else {
					captureMetadata.ListWarnings.Add("adjustments.flip is not a string.");
					isFlipNeutral=false;
				}
			}
			adjustments.IsOrientationNeutral=isRotateNeutral && isFlipNeutral;
			adjustments.IsNeutral=adjustments.IsOrientationNeutral && isBrightnessNeutral && isContrastNeutral;
			captureMetadata.Adjustments=adjustments;
		}

		///<summary>A whole-number adjustment. isNeutral is true when the member is missing or null or equals neutralValue, and false for any
		///other value, including one that is not a whole number.</summary>
		private static int? ReadAdjustmentInt(JsonValue jsonObject,string name,int neutralValue,CaptureMetadata captureMetadata,out bool isNeutral) {
			isNeutral=true;
			JsonValue jsonValue=jsonObject.Get(name);
			if(jsonValue==null || jsonValue.Kind==JsonKind.Null) {
				return null;
			}
			int value;
			if(!jsonValue.TryGetInt(out value)) {
				captureMetadata.ListWarnings.Add("adjustments."+name+" is not a whole number.");
				isNeutral=false;
				return null;
			}
			isNeutral=value==neutralValue;
			return value;
		}

		private static void ReadExposure(JsonValue jsonObject,CaptureMetadata captureMetadata) {
			JsonValue jsonValue=jsonObject.Get("configuredExposure");
			if(jsonValue==null || jsonValue.Kind==JsonKind.Null) {
				return;
			}
			if(jsonValue.Kind!=JsonKind.Object) {
				captureMetadata.ListWarnings.Add("configuredExposure is not an object.");
				return;
			}
			CaptureMetadataExposure exposure=new CaptureMetadataExposure();
			exposure.Kv=ReadExposureValue(jsonValue,"kv",captureMetadata);
			exposure.Ma=ReadExposureValue(jsonValue,"ma",captureMetadata);
			exposure.S=ReadExposureValue(jsonValue,"s",captureMetadata);
			exposure.Dap=ReadExposureValue(jsonValue,"dap",captureMetadata);
			JsonValue jsonValueSource=jsonValue.Get("source");
			if(jsonValueSource!=null && jsonValueSource.Kind==JsonKind.String) {
				exposure.Source=jsonValueSource.Text;
			}
			if(!exposure.HasAnyValue) {
				return;//the schema sends null when nothing is set; an object with no values means the same
			}
			captureMetadata.ConfiguredExposure=exposure;
		}

		private static double? ReadExposureValue(JsonValue jsonObject,string name,CaptureMetadata captureMetadata) {
			JsonValue jsonValue=jsonObject.Get(name);
			if(jsonValue==null) {
				return null;
			}
			double value;
			if(jsonValue.TryGetDouble(out value) && value>=0) {
				return value;
			}
			//null breaks the schema too (an unset value is left out), but it means the same.
			if(jsonValue.Kind!=JsonKind.Null) {
				captureMetadata.ListWarnings.Add("configuredExposure."+name+" is not a number of 0 or more.");
			}
			return null;
		}

		///<summary>A string member, or a number member as written (a serial or version sent as a number). Null when missing, null or of
		///another type; another type is listed as a warning.</summary>
		private static string ReadText(JsonValue jsonObject,string name,CaptureMetadata captureMetadata) {
			JsonValue jsonValue=jsonObject.Get(name);
			if(jsonValue==null || jsonValue.Kind==JsonKind.Null) {
				return null;
			}
			if(jsonValue.Kind==JsonKind.String || jsonValue.Kind==JsonKind.Number) {
				return jsonValue.Text;
			}
			captureMetadata.ListWarnings.Add(name+" is not a string.");
			return null;
		}

		private static int? ReadInt(JsonValue jsonObject,string name,CaptureMetadata captureMetadata) {
			JsonValue jsonValue=jsonObject.Get(name);
			if(jsonValue==null || jsonValue.Kind==JsonKind.Null) {
				return null;
			}
			int value;
			if(jsonValue.TryGetInt(out value)) {
				return value;
			}
			captureMetadata.ListWarnings.Add(name+" is not a whole number.");
			return null;
		}
	}
}
