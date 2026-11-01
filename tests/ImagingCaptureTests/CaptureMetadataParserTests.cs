// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Text;
using ImagingCapture.Metadata;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>capture-metadata/1 (docs/specs/capture-metadata-v1.md, design 7.9): where the object may be, what counts as absent, and how
	///each field reads, including nulls, wrong types and unknown fields.</summary>
	[TestClass]
	public class CaptureMetadataParserTests {
		///<summary>A 16-bit image's metadata with every field, laid out as the sensor contract sends it (one object at the top level).
		///Made-up values.</summary>
		private const string Json16Bit=@"{
  ""schema"": ""capture-metadata/1"",
  ""manufacturer"": ""Example Imaging"",
  ""model"": null,
  ""serial"": ""SN-000123"",
  ""captureId"": ""c-7f3a91"",
  ""sequence"": 3,
  ""acquiredAt"": ""2026-10-31T14:25:30.123+01:00"",
  ""pixelSpacingMm"": [0.02, 0.025],
  ""bitsStored"": 16,
  ""effectiveBits"": null,
  ""byteOrder"": ""LE"",
  ""photometric"": ""MONOCHROME2"",
  ""window"": { ""low"": 312, ""high"": 3900 },
  ""rendering"": ""original"",
  ""detectorType"": null,
  ""redelivered"": false,
  ""configuredExposure"": { ""kv"": 70, ""ma"": 7, ""s"": 0.125, ""source"": ""configured"" },
  ""dsVersion"": ""1.1.0"",
  ""vendor"": {}
}";

		///<summary>An 8-bit image's metadata: no window, no byte order, adjustments applied, redelivered.</summary>
		private const string Json8BitAdjusted=@"{""schema"":""capture-metadata/1"",""manufacturer"":""Example Imaging"",""model"":""M2"",""serial"":""SN-9"",
""captureId"":""c-1"",""sequence"":1,""acquiredAt"":""2026-10-31T09:00:00.000Z"",""pixelSpacingMm"":[0.02,0.02],""bitsStored"":8,
""effectiveBits"":null,""byteOrder"":null,""photometric"":""MONOCHROME2"",""window"":null,""rendering"":""vendor8-hd"",
""adjustments"":{""brightness"":55,""contrast"":50,""rotate"":90,""flip"":""h""},""detectorType"":null,""redelivered"":true,
""configuredExposure"":null,""dsVersion"":""1.1.0"",""vendor"":{""anything"":[1,2,{""deep"":true}]}}";

		#region Layout
		[TestMethod]
		public void TopLevel_EveryFieldReads() {
			CaptureMetadata captureMetadata=CaptureMetadataParser.Parse(Json16Bit);
			Assert.IsNotNull(captureMetadata);
			Assert.AreEqual("Example Imaging",captureMetadata.Manufacturer);
			Assert.IsNull(captureMetadata.Model);
			Assert.AreEqual("SN-000123",captureMetadata.Serial);
			Assert.AreEqual("c-7f3a91",captureMetadata.CaptureId);
			Assert.AreEqual(3,captureMetadata.Sequence);
			Assert.AreEqual("2026-10-31T14:25:30.123+01:00",captureMetadata.AcquiredAtText);
			Assert.AreEqual(new DateTimeOffset(2026,10,31,14,25,30,123,TimeSpan.FromHours(1)),captureMetadata.AcquiredAt);
			Assert.AreEqual(TimeSpan.FromHours(1),captureMetadata.AcquiredAt.Value.Offset);
			Assert.AreEqual(0.02,captureMetadata.PixelSpacingXMm);
			Assert.AreEqual(0.025,captureMetadata.PixelSpacingYMm);
			Assert.AreEqual(16,captureMetadata.BitsStored);
			Assert.IsNull(captureMetadata.EffectiveBits);
			Assert.AreEqual("LE",captureMetadata.ByteOrder);
			Assert.AreEqual("MONOCHROME2",captureMetadata.Photometric);
			Assert.AreEqual(312,captureMetadata.Window.Low);
			Assert.AreEqual(3900,captureMetadata.Window.High);
			Assert.AreEqual(CaptureMetadata.RenderingOriginal,captureMetadata.Rendering);
			Assert.IsNull(captureMetadata.Adjustments,"Present only when adjustments were applied.");
			Assert.IsFalse(captureMetadata.HasOrientationAdjustment);
			Assert.IsNull(captureMetadata.DetectorType);
			Assert.IsFalse(captureMetadata.Redelivered);
			Assert.AreEqual(70.0,captureMetadata.ConfiguredExposure.Kv);
			Assert.AreEqual(7.0,captureMetadata.ConfiguredExposure.Ma);
			Assert.AreEqual(0.125,captureMetadata.ConfiguredExposure.S);
			Assert.IsNull(captureMetadata.ConfiguredExposure.Dap,"An unset key is left out.");
			Assert.AreEqual(CaptureMetadataExposure.SourceConfigured,captureMetadata.ConfiguredExposure.Source);
			Assert.AreEqual("1.1.0",captureMetadata.DsVersion);
			Assert.AreEqual(0,captureMetadata.ListWarnings.Count,string.Join("; ",captureMetadata.ListWarnings));
		}

		[TestMethod]
		public void TopLevel_8BitImage_AdjustmentsAndRedelivery() {
			CaptureMetadata captureMetadata=CaptureMetadataParser.Parse(Json8BitAdjusted);
			Assert.IsNotNull(captureMetadata);
			Assert.AreEqual("M2",captureMetadata.Model);
			Assert.AreEqual(8,captureMetadata.BitsStored);
			Assert.IsNull(captureMetadata.ByteOrder);
			Assert.IsNull(captureMetadata.Window);
			Assert.AreEqual(CaptureMetadata.RenderingVendor8Hd,captureMetadata.Rendering);
			Assert.AreEqual(TimeSpan.Zero,captureMetadata.AcquiredAt.Value.Offset);
			Assert.AreEqual(new DateTime(2026,10,31,9,0,0,DateTimeKind.Utc),captureMetadata.AcquiredAt.Value.UtcDateTime);
			Assert.IsTrue(captureMetadata.Redelivered);
			Assert.IsNull(captureMetadata.ConfiguredExposure);
			CaptureMetadataAdjustments adjustments=captureMetadata.Adjustments;
			Assert.AreEqual(55,adjustments.Brightness);
			Assert.AreEqual(50,adjustments.Contrast);
			Assert.AreEqual(90,adjustments.Rotate);
			Assert.AreEqual(CaptureMetadataAdjustments.FlipHorizontal,adjustments.Flip);
			Assert.IsFalse(adjustments.IsOrientationNeutral);
			Assert.IsFalse(adjustments.IsNeutral);
			Assert.IsTrue(captureMetadata.HasOrientationAdjustment,"The session warns: the slot's rotation would be applied on top.");
			Assert.AreEqual(0,captureMetadata.ListWarnings.Count,string.Join("; ",captureMetadata.ListWarnings));
		}

		[TestMethod]
		public void Nested_TwainDirectStyleWrapping_IsFound() {
			string json=@"{""version"":""1.0"",""metadata"":{""address"":{""imageNumber"":1},""vendorSpecific"":["
				+@"{""other"":{""schema"":""someone-else/3""}},{""x"":"+Json16Bit+"}]}}";
			CaptureMetadata captureMetadata=CaptureMetadataParser.Parse(json);
			Assert.IsNotNull(captureMetadata);
			Assert.AreEqual("c-7f3a91",captureMetadata.CaptureId);
			Assert.AreEqual(3,captureMetadata.Sequence);
		}

		[TestMethod]
		public void Nested_FirstObjectInDocumentOrderWins() {
			string json=@"{""a"":{""b"":[{""schema"":""capture-metadata/1"",""captureId"":""first"",""redelivered"":false}]},"
				+@"""c"":{""schema"":""capture-metadata/1"",""captureId"":""second"",""redelivered"":false}}";
			Assert.AreEqual("first",CaptureMetadataParser.Parse(json).CaptureId);
			//An object matches before anything nested in it.
			string jsonOuter=@"{""schema"":""capture-metadata/1"",""captureId"":""outer"",""redelivered"":false,"
				+@"""inner"":{""schema"":""capture-metadata/1"",""captureId"":""inner"",""redelivered"":false}}";
			Assert.AreEqual("outer",CaptureMetadataParser.Parse(jsonOuter).CaptureId);
			//The root may be an array.
			string jsonArray=@"[1,""x"",null,{""schema"":""capture-metadata/1"",""captureId"":""in-array"",""redelivered"":false}]";
			Assert.AreEqual("in-array",CaptureMetadataParser.Parse(jsonArray).CaptureId);
		}

		[TestMethod]
		public void HigherMajor_IsAbsent() {
			Assert.IsNull(CaptureMetadataParser.Parse(Json16Bit.Replace("capture-metadata/1","capture-metadata/2")));
			Assert.IsNull(CaptureMetadataParser.Parse(Json16Bit.Replace("capture-metadata/1","capture-metadata/10")));
			//The schema value must match exactly.
			Assert.IsNull(CaptureMetadataParser.Parse(Json16Bit.Replace("capture-metadata/1","Capture-Metadata/1")));
			Assert.IsNull(CaptureMetadataParser.Parse(Json16Bit.Replace("capture-metadata/1","capture-metadata/1.1")));
			Assert.IsNull(CaptureMetadataParser.Parse(Json16Bit.Replace(@"""capture-metadata/1""",@"[""capture-metadata/1""]")));
			//A later major version that carries a version-1 object for older readers: that object is found, by the same rule.
			string jsonCompat=@"{""schema"":""capture-metadata/2"",""captureId"":""v2"",""v1"":"+Json16Bit+"}";
			Assert.AreEqual("c-7f3a91",CaptureMetadataParser.Parse(jsonCompat).CaptureId);
		}

		[TestMethod]
		public void NoSchemaObject_IsAbsent() {
			string[] arrayJson={ null,"","   ","{}","[]","null","true","42",@"""capture-metadata/1""",@"{""schema"":null}",
				@"{""schema"":""capture-metadata/1 ""}",
				@"{""Schema"":""capture-metadata/1""}",@"{""metadata"":{""schema"":""other""}}" };
			foreach(string json in arrayJson) {
				Assert.IsNull(CaptureMetadataParser.Parse(json),"Absent: "+(json??"null"));
				Assert.IsNull(CaptureMetadataParser.ReadKeys(json));
			}
		}

		[TestMethod]
		public void Malformed_IsAbsent() {
			string objectV1=@"{""schema"":""capture-metadata/1"",""captureId"":""x"",""redelivered"":false}";
			Assert.IsNotNull(CaptureMetadataParser.Parse(objectV1),"The well-formed base case.");
			string[] arrayJson={
				objectV1.Substring(0,objectV1.Length-1),//truncated
				objectV1.Replace("}",",}"),//trailing comma
				objectV1+"x",//text after the value
				objectV1+objectV1,//two values
				objectV1.Replace(@"""captureId""","captureId"),//unquoted name
				objectV1.Replace(@"""x""","'x'"),//single quotes
				objectV1.Replace(@"false","False"),
				objectV1.Replace(@"false","fals"),
				objectV1.Replace(@"""x""","01"),//leading zero
				objectV1.Replace(@"""x""","1."),
				objectV1.Replace(@"""x""",".5"),
				objectV1.Replace(@"""x""","NaN"),
				objectV1.Replace(@"""x""","+1"),
				objectV1.Replace(@"""x""","\"a\tb\""),//control character in a string
				objectV1.Replace(@"""x""",@"""a\qb"""),//invalid escape
				objectV1.Replace(@"""x""","\"\\"+"u12G4\""),//invalid hex escape
				objectV1.Replace(@"""x""","\"x"),//unterminated string
				"/*c*/"+objectV1,//comment
				"{\"a\":1 \"b\":2}",
				"\0"+objectV1,
			};
			foreach(string json in arrayJson) {
				Assert.IsNull(CaptureMetadataParser.Parse(json),"Malformed: "+json);
			}
		}

		[TestMethod]
		public void Malformed_NestedTooDeep_IsAbsent() {
			string objectV1=@"{""schema"":""capture-metadata/1"",""captureId"":""deep"",""redelivered"":false}";
			//The object at depth 64 (the limit) is read; one level more makes the whole document unreadable.
			string json63=new string('[',63)+objectV1+new string(']',63);
			Assert.AreEqual("deep",CaptureMetadataParser.Parse(json63).CaptureId);
			string json64=new string('[',64)+objectV1+new string(']',64);
			Assert.IsNull(CaptureMetadataParser.Parse(json64));
			string jsonHuge=new string('[',100000)+new string(']',100000);
			Assert.IsNull(CaptureMetadataParser.Parse(jsonHuge),"No stack overflow.");
		}

		[TestMethod]
		public void NativeText_BomTrailingNulsAndEscapes() {
			string json=((char)0xFEFF).ToString()+@"{""schema"":""capture-metadata/1"",""serial"":""A\""B\\C\/D\n"
				+"\\"+"u00e9"+"\\"+"ud83d"+"\\"+"ude00"+@""",""redelivered"":false}"+"\0\0\r\n";
			CaptureMetadata captureMetadata=CaptureMetadataParser.Parse(json);
			Assert.IsNotNull(captureMetadata);
			Assert.AreEqual("A\"B\\C/D\n"+(char)0xE9+char.ConvertFromUtf32(0x1F600),captureMetadata.Serial);
		}
		#endregion Layout

		#region Fields
		[TestMethod]
		public void Nulls_ReadAsAbsent() {
			string json=@"{""schema"":""capture-metadata/1"",""manufacturer"":null,""model"":null,""serial"":null,""captureId"":null,
""sequence"":null,""acquiredAt"":null,""pixelSpacingMm"":null,""bitsStored"":null,""effectiveBits"":null,""byteOrder"":null,
""photometric"":null,""window"":null,""rendering"":null,""adjustments"":null,""detectorType"":null,""redelivered"":false,
""configuredExposure"":null,""dsVersion"":null,""vendor"":null}";
			CaptureMetadata captureMetadata=CaptureMetadataParser.Parse(json);
			Assert.IsNotNull(captureMetadata,"Nulls never make the metadata absent.");
			Assert.IsNull(captureMetadata.Manufacturer);
			Assert.IsNull(captureMetadata.Serial);
			Assert.IsNull(captureMetadata.CaptureId);
			Assert.IsNull(captureMetadata.Sequence);
			Assert.IsNull(captureMetadata.AcquiredAtText);
			Assert.IsNull(captureMetadata.AcquiredAt);
			Assert.IsNull(captureMetadata.PixelSpacingXMm);
			Assert.IsNull(captureMetadata.BitsStored);
			Assert.IsNull(captureMetadata.Window);
			Assert.IsNull(captureMetadata.Rendering);
			Assert.IsNull(captureMetadata.Adjustments);
			Assert.IsNull(captureMetadata.ConfiguredExposure);
			Assert.IsNull(captureMetadata.DsVersion);
			Assert.AreEqual(0,captureMetadata.ListWarnings.Count,string.Join("; ",captureMetadata.ListWarnings));
		}

		[TestMethod]
		public void OnlySchema_ReadsWithDefaults() {
			CaptureMetadata captureMetadata=CaptureMetadataParser.Parse(@"{""schema"":""capture-metadata/1""}");
			Assert.IsNotNull(captureMetadata);
			Assert.IsFalse(captureMetadata.Redelivered);
			Assert.IsTrue(captureMetadata.ListWarnings.Any(x => x.StartsWith("redelivered")),"redelivered is always present in the schema.");
		}

		[TestMethod]
		public void UnknownFieldsAndVendorObject_AreIgnored() {
			string json=Json16Bit.Replace(@"""vendor"": {}",@"""vendor"": {""captureId"":""not-this"",""sequence"":99},""futureField"":[1,{""a"":null}],"
				+@"""futureObject"":{""schema"":""capture-metadata/1"",""captureId"":""not-this-either""}");
			CaptureMetadata captureMetadata=CaptureMetadataParser.Parse(json);
			Assert.AreEqual("c-7f3a91",captureMetadata.CaptureId);
			Assert.AreEqual(3,captureMetadata.Sequence);
			Assert.AreEqual(0,captureMetadata.ListWarnings.Count,string.Join("; ",captureMetadata.ListWarnings));
		}

		[TestMethod]
		public void WrongTypes_ReadAsAbsentWithWarnings() {
			string json=@"{""schema"":""capture-metadata/1"",""manufacturer"":{""a"":1},""serial"":12345,""captureId"":"""",""sequence"":""3"",
""acquiredAt"":""31.10.2026 14:25"",""pixelSpacingMm"":[0.02],""bitsStored"":16.5,""effectiveBits"":20,""window"":{""low"":1},
""redelivered"":""yes"",""dsVersion"":2}";
			CaptureMetadata captureMetadata=CaptureMetadataParser.Parse(json);
			Assert.IsNotNull(captureMetadata);
			Assert.IsNull(captureMetadata.Manufacturer);
			Assert.AreEqual("12345",captureMetadata.Serial,"A number is read as written.");
			Assert.IsNull(captureMetadata.CaptureId,"An empty captureId is no captureId.");
			Assert.IsNull(captureMetadata.Sequence);
			Assert.AreEqual("31.10.2026 14:25",captureMetadata.AcquiredAtText,"Kept for display.");
			Assert.IsNull(captureMetadata.AcquiredAt);
			Assert.IsNull(captureMetadata.PixelSpacingXMm);
			Assert.IsNull(captureMetadata.BitsStored);
			Assert.IsNull(captureMetadata.EffectiveBits);
			Assert.IsNull(captureMetadata.Window);
			Assert.IsFalse(captureMetadata.Redelivered);
			Assert.AreEqual("2",captureMetadata.DsVersion);
			foreach(string name in new[] { "manufacturer","sequence","acquiredAt","pixelSpacingMm","bitsStored","effectiveBits","window","redelivered" }) {
				Assert.IsTrue(captureMetadata.ListWarnings.Any(x => x.StartsWith(name)),"Warning for "+name+": "+string.Join("; ",captureMetadata.ListWarnings));
			}
		}

		[TestMethod]
		public void Sequence_OneBasedAndWhole() {
			Assert.AreEqual(1,Parse(@"""sequence"":1").Sequence);
			Assert.AreEqual(4,Parse(@"""sequence"":4.0").Sequence);
			Assert.IsNull(Parse(@"""sequence"":0").Sequence);
			Assert.IsNull(Parse(@"""sequence"":-2").Sequence);
			Assert.IsNull(Parse(@"""sequence"":2.5").Sequence);
			Assert.IsNull(Parse(@"""sequence"":1e20").Sequence);
		}

		[TestMethod]
		public void AcquiredAt_NeedsAUtcOffset() {
			Assert.AreEqual(TimeSpan.FromHours(-5),Parse(@"""acquiredAt"":""2026-01-02T03:04:05.678-05:00""").AcquiredAt.Value.Offset);
			Assert.AreEqual(new DateTimeOffset(2026,1,2,3,4,5,TimeSpan.Zero),Parse(@"""acquiredAt"":""2026-01-02T03:04:05Z""").AcquiredAt);
			CaptureMetadata captureMetadataLocal=Parse(@"""acquiredAt"":""2026-01-02T03:04:05.678""");
			Assert.IsNull(captureMetadataLocal.AcquiredAt,"Without an offset the time is ambiguous.");
			Assert.AreEqual("2026-01-02T03:04:05.678",captureMetadataLocal.AcquiredAtText);
			Assert.IsNull(Parse(@"""acquiredAt"":""2026-13-02T03:04:05Z""").AcquiredAt);
		}

		[TestMethod]
		public void PixelSpacing_TwoPositiveNumbers() {
			Assert.AreEqual(0.02,Parse(@"""pixelSpacingMm"":[0.02,0.02]").PixelSpacingYMm);
			Assert.IsNull(Parse(@"""pixelSpacingMm"":[0.02,0.02,0.02]").PixelSpacingXMm);
			Assert.IsNull(Parse(@"""pixelSpacingMm"":[0,0.02]").PixelSpacingXMm);
			Assert.IsNull(Parse(@"""pixelSpacingMm"":[-0.02,0.02]").PixelSpacingXMm);
			Assert.IsNull(Parse(@"""pixelSpacingMm"":[""0.02"",0.02]").PixelSpacingXMm);
			Assert.IsNull(Parse(@"""pixelSpacingMm"":0.02").PixelSpacingXMm);
		}

		[TestMethod]
		public void ConfiguredExposure_UnsetKeysAndEmptyObject() {
			Assert.IsNull(Parse(@"""configuredExposure"":{}").ConfiguredExposure,"Nothing set is the same as null.");
			Assert.IsNull(Parse(@"""configuredExposure"":{""source"":""configured""}").ConfiguredExposure);
			Assert.IsNull(Parse(@"""configuredExposure"":{""kv"":null,""source"":""configured""}").ConfiguredExposure);
			CaptureMetadataExposure exposure=Parse(@"""configuredExposure"":{""dap"":12.5,""source"":""configured""}").ConfiguredExposure;
			Assert.IsNull(exposure.Kv);
			Assert.IsNull(exposure.Ma);
			Assert.IsNull(exposure.S);
			Assert.AreEqual(12.5,exposure.Dap);
			Assert.AreEqual("DAP 12.5 mGy.cm2",exposure.ToText());
			CaptureMetadataExposure exposureAll=Parse(@"""configuredExposure"":{""kv"":60.5,""ma"":4,""s"":0.08,""dap"":3,""source"":""configured""}")
				.ConfiguredExposure;
			Assert.AreEqual("60.5 kV, 4 mA, 0.08 s, DAP 3 mGy.cm2",exposureAll.ToText());
			CaptureMetadata captureMetadataBad=Parse(@"""configuredExposure"":{""kv"":""70"",""ma"":-1,""s"":0.1}");
			Assert.AreEqual("0.1 s",captureMetadataBad.ConfiguredExposure.ToText());
			Assert.AreEqual(2,captureMetadataBad.ListWarnings.Count(x => x.StartsWith("configuredExposure")));
			Assert.IsNull(Parse(@"""configuredExposure"":[70]").ConfiguredExposure);
		}

		[TestMethod]
		public void Adjustments_Neutrality() {
			CaptureMetadataAdjustments adjustmentsNeutral=Parse(@"""adjustments"":{""brightness"":50,""contrast"":50,""rotate"":0,""flip"":""none""}").Adjustments;
			Assert.IsTrue(adjustmentsNeutral.IsNeutral);
			Assert.IsTrue(adjustmentsNeutral.IsOrientationNeutral);
			CaptureMetadata captureMetadataBrightness=Parse(@"""adjustments"":{""brightness"":60,""contrast"":50,""rotate"":0,""flip"":""none""}");
			Assert.IsFalse(captureMetadataBrightness.Adjustments.IsNeutral);
			Assert.IsTrue(captureMetadataBrightness.Adjustments.IsOrientationNeutral);
			Assert.IsFalse(captureMetadataBrightness.HasOrientationAdjustment,"Only rotate and flip are warned about.");
			Assert.IsTrue(Parse(@"""adjustments"":{""rotate"":270}").HasOrientationAdjustment);
			Assert.IsTrue(Parse(@"""adjustments"":{""flip"":""Both""}").HasOrientationAdjustment);
			Assert.AreEqual("both",Parse(@"""adjustments"":{""flip"":""Both""}").Adjustments.Flip);
			Assert.IsFalse(Parse(@"""adjustments"":{}").HasOrientationAdjustment);
			//Values the fork cannot read count as not neutral.
			Assert.IsTrue(Parse(@"""adjustments"":{""rotate"":""90""}").HasOrientationAdjustment);
			Assert.IsTrue(Parse(@"""adjustments"":{""rotate"":45.5}").HasOrientationAdjustment);
			Assert.IsTrue(Parse(@"""adjustments"":{""flip"":true}").HasOrientationAdjustment);
			Assert.IsTrue(Parse(@"""adjustments"":[90]").HasOrientationAdjustment);
			Assert.IsTrue(Parse(@"""adjustments"":{""rotate"":45}").ListWarnings.Any(x => x.StartsWith("adjustments.rotate")));
		}

		[TestMethod]
		public void Rendering_UnknownValueKeptWithWarning() {
			CaptureMetadata captureMetadata=Parse(@"""rendering"":""something-new""");
			Assert.AreEqual("something-new",captureMetadata.Rendering);
			Assert.IsTrue(captureMetadata.ListWarnings.Any(x => x.StartsWith("rendering")));
			foreach(string rendering in new[] { "original","vendor8","vendor8-hd","window8" }) {
				Assert.AreEqual(0,Parse(@"""rendering"":"""+rendering+@"""").ListWarnings.Count,rendering);
			}
		}
		#endregion Fields

		#region Keys for the sidecars
		[TestMethod]
		public void ReadKeys_GivesCaptureIdAndSequence() {
			Func<string,CaptureMetadataKeys> funcReadMetadataKeys=CaptureMetadataParser.ReadKeys;//TwainCaptureSession.FuncReadMetadataKeys
			CaptureMetadataKeys keys=funcReadMetadataKeys(Json16Bit);
			Assert.AreEqual("c-7f3a91",keys.CaptureId);
			Assert.AreEqual(3,keys.Sequence);
			CaptureMetadataKeys keysNoSequence=funcReadMetadataKeys(@"{""schema"":""capture-metadata/1"",""captureId"":""z"",""redelivered"":true}");
			Assert.AreEqual("z",keysNoSequence.CaptureId);
			Assert.IsNull(keysNoSequence.Sequence);
			Assert.IsNull(funcReadMetadataKeys(null));
			Assert.IsNull(funcReadMetadataKeys("{not json"));
		}

		[TestMethod]
		public void Parse_LongText_IsAbsentWithoutThrowing() {
			StringBuilder stringBuilder=new StringBuilder(@"{""schema"":""capture-metadata/1"",""vendor"":""");
			stringBuilder.Append('x',JsonReader.MaxLength);
			stringBuilder.Append(@"""}");
			Assert.IsNull(CaptureMetadataParser.Parse(stringBuilder.ToString()));
		}
		#endregion Keys for the sidecars

		///<summary>A version-1 object with redelivered and the given members.</summary>
		private static CaptureMetadata Parse(string members) {
			CaptureMetadata captureMetadata=CaptureMetadataParser.Parse(@"{""schema"":""capture-metadata/1"",""redelivered"":false,"+members+"}");
			Assert.IsNotNull(captureMetadata,members);
			return captureMetadata;
		}
	}
}
