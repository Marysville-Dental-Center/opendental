// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ImagingDeviceManager.Capture;

namespace ImagingCapture.Settings {
	///<summary>A Test capture verification record (design 9.3): what Test capture measured on a phantom for one imaging device, what the operator
	///confirmed, and what it covers. It is stored as the LogText of a Setup security-log entry with FKey=ImagingDeviceNum; there is no table of
	///its own (design 9.1, no new tables).
	///
	///Scope (owner decision 2026-11-01, "isn't just the sensor enough?"): a record is not tied to the computer it was made on. It covers the
	///device row (ImagingDeviceNum), the row's TWAIN source (ImagingDevice.TwainName), the driver build the source reported at open
	///(TW_IDENTITY ProductName and Version: MajorNum, MinorNum and Info) and the image settings (CaptureImageSettingsHash: [Device.N] Bits,
	///ByteOrder16 and Polarity). So one Test capture covers every computer whose driver version and those three settings match, and another
	///driver version or setting needs its own test. ComputerName and the whole-file GatedSettingsHash stay in the record for the audit trail and
	///are not compared; the per-computer settings check of design 9.3 (CaptureSettingsCheck) is a separate gate.
	///
	///The capture session asks for more than 8 bits (or inverts) only when the latest record for the device matches (Matches) and passed at the
	///depth it asks for (IsPassedFor). The text is key=value pairs, so it reads in the audit trail and parses back (TryParse). Values are
	///escaped: '%', ';', '=', CR and LF become %25, %3B, %3D, %0D and %0A. Records of the first format (FormatNameLegacy, before CAP-16) still
	///parse, for the audit trail and the reason, and never match.</summary>
	public sealed class CaptureVerificationRecord {
		///<summary>The format of the text; a later format gets a new name instead of being misread.</summary>
		public const string FormatName="capture-verification/2";
		///<summary>The format before CAP-16: per computer, with the whole-file hash and no driver version. Read, never matched.</summary>
		public const string FormatNameLegacy="capture-verification/1";
		///<summary>Every record's LogText starts with this (or LogPrefixLegacy for an old record).</summary>
		public const string LogPrefix="Capture verification record ("+FormatName+"): ";
		public const string LogPrefixLegacy="Capture verification record ("+FormatNameLegacy+"): ";
		public const string ByteOrderCheckNotApplicable="n/a";

		///<summary>The record is of FormatNameLegacy (before CAP-16): it names no driver version and holds the whole-file hash, so it never matches
		///and the session says "run Test capture again".</summary>
		public bool IsLegacyFormat { get; private set; }
		public long ImagingDeviceNum { get; set; }
		public string DeviceDescription { get; set; }="";
		///<summary>The TWAIN source of the device row when the test ran (ImagingDevice.TwainName), which Matches compares: the row may later name
		///another source.</summary>
		public string SourceName { get; set; }="";
		///<summary>The ProductName the source reported at open (TW_IDENTITY), for the audit trail; the driver identity compares it too.</summary>
		public string SourceProductName { get; set; }="";
		///<summary>The source build the test ran with (TwainCaptureSourceInfo.DriverIdentity at open), which Matches compares; null in a legacy
		///record.</summary>
		public TwainDriverIdentity DriverIdentity { get; set; }
		///<summary>The computer Test capture ran on (ODEnvironment.MachineName), for the audit trail. Not compared.</summary>
		public string ComputerName { get; set; }="";
		///<summary>CaptureImageSettingsHash.Compute of the settings the test ran with: 64 lowercase hex digits, compared by Matches. "" in a legacy
		///record.</summary>
		public string ImageSettingsHash { get; set; }="";
		///<summary>GatedSettingsHash.Compute of that computer's capture.ini, for the audit trail. Not compared (it was, in legacy records).</summary>
		public string GatedSettingsHash { get; set; }="";
		///<summary>When the record was written, local time.</summary>
		public DateTime DateTimeVerified { get; set; }
		///<summary>16, 14 or 8: what the test enable asked for.</summary>
		public int BitsRequested { get; set; }
		///<summary>16, 14 or 8: the depth the test enable ran at (AcquiredImage.BitDepthInEffect). Legacy records do not have it; see
		///BitDepthVerified.</summary>
		public int? BitDepthInEffect { get; set; }
		///<summary>DAT_IMAGEINFO BitsPerSample of the measured image.</summary>
		public int BitsDelivered { get; set; }
		///<summary>"Memory" or "Native".</summary>
		public string TransferMechanism { get; set; }="";
		public int Width { get; set; }
		public int Height { get; set; }
		public double XDpi { get; set; }
		public double YDpi { get; set; }
		///<summary>[Device.N] ByteOrder16 in effect: "LE" or "BE".</summary>
		public string ByteOrder16 { get; set; }="";
		///<summary>Test capture's automatic check (ByteOrderVerdict: LooksRight, LooksSwapped, Inconclusive), or "n/a" for an 8-bit image.</summary>
		public string ByteOrderCheck { get; set; }=ByteOrderCheckNotApplicable;
		///<summary>The operator confirmed the byte order on a known ramp or the phantom.</summary>
		public bool IsByteOrderConfirmed { get; set; }
		///<summary>[Device.N] Polarity in effect: "AsDelivered" or "Invert".</summary>
		public string Polarity { get; set; }="";
		///<summary>The pixel flavor the source delivered: "Chocolate" or "Vanilla".</summary>
		public string PixelFlavor { get; set; }="";
		///<summary>The operator confirmed that radiopaque markers show white after the configured polarity.</summary>
		public bool IsPolarityConfirmed { get; set; }
		///<summary>The smallest and largest non-zero sample as delivered.</summary>
		public int? MinValue { get; set; }
		public int? MaxValue { get; set; }
		///<summary>The bits needed for MaxValue.</summary>
		public int? EffectiveBitsMeasured { get; set; }
		///<summary>The metadata's effectiveBits, or null.</summary>
		public int? EffectiveBitsReported { get; set; }
		///<summary>What the BitsStored rule gives for this image (12 or 16), for images of more than 8 bits.</summary>
		public int? BitsStoredRule { get; set; }
		public int? WindowLow { get; set; }
		public int? WindowHigh { get; set; }
		///<summary>From the capture metadata, when it had them.</summary>
		public string Serial { get; set; }="";
		public string Model { get; set; }="";
		public string DsVersion { get; set; }="";
		public bool IsMetadataPresent { get; set; }

		///<summary>16 or 14: the depth the record verifies, when the measured image came from an enable of more than 8 bits over memory transfer
		///and was delivered in 16-bit containers (BitsPerSample above 8); otherwise 8 (it verifies no depth above 8). A legacy record that only
		///says 16 (BitsRequested 16, BitsDelivered 16, memory transfer) verifies 16.</summary>
		public int BitDepthVerified {
			get {
				bool isMemory=string.Equals(TransferMechanism,"Memory",StringComparison.OrdinalIgnoreCase);
				if(BitDepthInEffect==null) {
					return BitsDelivered==16 && BitsRequested==16 && isMemory ? TwainBitDepth.Bits16 : TwainBitDepth.Bits8;
				}
				int depth=TwainBitDepth.Normalize(BitDepthInEffect.Value);
				return depth>8 && isMemory && BitsDelivered>8 ? depth : TwainBitDepth.Bits8;
			}
		}

		///<summary>The record is for this device row, its TWAIN source (the row's ImagingDevice.TwainName), the driver build the open source reports
		///(TwainDriverIdentity.IsSameAs) and these image settings (CaptureImageSettingsHash). The computer is not compared. Source names compare
		///trimmed and ignoring case, as the source gate compares them. A legacy record never matches.</summary>
		public bool Matches(long imagingDeviceNum,string sourceName,TwainDriverIdentity driverIdentity,string imageSettingsHash) {
			return GetMismatch(imagingDeviceNum,sourceName,driverIdentity,imageSettingsHash)==CaptureRecordMismatch.None;
		}

		///<summary>What Matches found first that differs, or None.</summary>
		public CaptureRecordMismatch GetMismatch(long imagingDeviceNum,string sourceName,TwainDriverIdentity driverIdentity,string imageSettingsHash) {
			if(IsLegacyFormat || DriverIdentity==null || string.IsNullOrEmpty(ImageSettingsHash)) {
				return CaptureRecordMismatch.LegacyRecord;
			}
			if(ImagingDeviceNum!=imagingDeviceNum || string.IsNullOrWhiteSpace(SourceName)
				|| !string.Equals(SourceName.Trim(),(sourceName??"").Trim(),StringComparison.OrdinalIgnoreCase))
			{
				return CaptureRecordMismatch.Source;
			}
			if(driverIdentity==null) {
				return CaptureRecordMismatch.DriverUnknown;
			}
			if(!DriverIdentity.IsSameAs(driverIdentity)) {
				return CaptureRecordMismatch.Driver;
			}
			if(!string.Equals(ImageSettingsHash,(imageSettingsHash??"").Trim(),StringComparison.OrdinalIgnoreCase)) {
				return CaptureRecordMismatch.ImageSettings;
			}
			return CaptureRecordMismatch.None;
		}

		///<summary>The record verifies capture at BitDepthVerified (16 or 14): an image of that depth was measured from such an enable over memory
		///transfer, the automatic byte-order check did not find the bytes swapped, the operator confirmed byte order and polarity on the phantom
		///(design 7.8 condition 2), and the saver would write the samples (IsVanillaNotInverted is false: DicomIntraOralWriter refuses
		///TWPF_VANILLA samples stored as delivered).</summary>
		public bool IsPassed =>
			BitDepthVerified>8
			&& !string.Equals(ByteOrderCheck,"LooksSwapped",StringComparison.OrdinalIgnoreCase)
			&& IsByteOrderConfirmed
			&& IsPolarityConfirmed
			&& !IsVanillaNotInverted
			&& !Is14BitNotRightJustified;

		///<summary>The record is for a 14-bit enable whose measured image had a sample above 16383 (MaxValue): its samples are not right-justified
		///14-bit values (left-justified data, for example), so it verifies no 14-bit capture (design 7.8 condition 2: Test capture checks the
		///effective bits).</summary>
		public bool Is14BitNotRightJustified => BitDepthVerified==TwainBitDepth.Bits14 && MaxValue!=null && MaxValue.Value>16383;

		///<summary>IsPassed at exactly bits: a record that passed at 16 covers asking for 16, one that passed at 14 covers asking for 14.</summary>
		public bool IsPassedFor(int bits) {
			return IsPassed && BitDepthVerified==bits;
		}

		///<summary>The source delivered TWPF_VANILLA samples and Polarity is not Invert: the DICOM writer refuses such samples.</summary>
		public bool IsVanillaNotInverted =>
			string.Equals(PixelFlavor,"Vanilla",StringComparison.OrdinalIgnoreCase) && !string.Equals(Polarity,"Invert",StringComparison.OrdinalIgnoreCase);

		///<summary>"2026-11-01 10:15", for the texts.</summary>
		public string DateText => DateTimeVerified.ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture);

		///<summary>The security-log text: LogPrefix, then key=value pairs separated by "; ".</summary>
		public string ToLogText() {
			List<KeyValuePair<string,string>> listPairs=GetPairs();
			return LogPrefix+string.Join("; ",listPairs.Select(x => x.Key+"="+Escape(x.Value)));
		}

		///<summary>Reads a record from a security-log text that starts with LogPrefix, or LogPrefixLegacy (IsLegacyFormat). False for any other
		///text, a record without its device, a record of this format without its driver version or image-settings hash, and a legacy record
		///without its computer or hash.</summary>
		public static bool TryParse(string logText,out CaptureVerificationRecord record) {
			record=null;
			if(logText==null) {
				return false;
			}
			bool isLegacy;
			string body;
			if(logText.StartsWith(LogPrefix,StringComparison.Ordinal)) {
				isLegacy=false;
				body=logText.Substring(LogPrefix.Length);
			}
			else if(logText.StartsWith(LogPrefixLegacy,StringComparison.Ordinal)) {
				isLegacy=true;
				body=logText.Substring(LogPrefixLegacy.Length);
			}
			else {
				return false;
			}
			Dictionary<string,string> dictValues=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			foreach(string part in body.Split(';')) {
				int idx=part.IndexOf('=');
				if(idx<=0) {
					continue;
				}
				dictValues[part.Substring(0,idx).Trim()]=Unescape(part.Substring(idx+1));
			}
			CaptureVerificationRecord recordRead=new CaptureVerificationRecord { IsLegacyFormat=isLegacy };
			long imagingDeviceNum;
			if(!long.TryParse(Get(dictValues,"Device"),NumberStyles.Integer,CultureInfo.InvariantCulture,out imagingDeviceNum) || imagingDeviceNum<=0) {
				return false;
			}
			recordRead.ImagingDeviceNum=imagingDeviceNum;
			recordRead.ComputerName=Get(dictValues,"Computer");
			recordRead.GatedSettingsHash=Get(dictValues,"GatedSettingsHash").ToLowerInvariant();
			if(isLegacy) {
				if(recordRead.ComputerName=="" || recordRead.GatedSettingsHash.Length!=64) {
					return false;
				}
			}
			else {
				recordRead.ImageSettingsHash=Get(dictValues,"ImageSettingsHash").ToLowerInvariant();
				recordRead.DriverIdentity=TwainDriverIdentity.TryParse(Get(dictValues,"DriverProductName"),Get(dictValues,"DriverVersion"),
					Get(dictValues,"DriverVersionInfo"));
				if(recordRead.ImageSettingsHash.Length!=64 || recordRead.DriverIdentity==null) {
					return false;
				}
				recordRead.BitDepthInEffect=GetInt(dictValues,"BitDepthInEffect");
			}
			recordRead.DeviceDescription=Get(dictValues,"DeviceDescription");
			recordRead.SourceName=Get(dictValues,"Source");
			recordRead.SourceProductName=Get(dictValues,"SourceProductName");
			DateTime dateTime;
			if(DateTime.TryParseExact(Get(dictValues,"Date"),"yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out dateTime)) {
				recordRead.DateTimeVerified=dateTime;
			}
			recordRead.BitsRequested=GetInt(dictValues,"BitsRequested")??0;
			recordRead.BitsDelivered=GetInt(dictValues,"BitsDelivered")??0;
			recordRead.TransferMechanism=Get(dictValues,"Transfer");
			recordRead.Width=GetInt(dictValues,"Width")??0;
			recordRead.Height=GetInt(dictValues,"Height")??0;
			recordRead.XDpi=GetDouble(dictValues,"XDpi");
			recordRead.YDpi=GetDouble(dictValues,"YDpi");
			recordRead.ByteOrder16=Get(dictValues,"ByteOrder16");
			recordRead.ByteOrderCheck=Get(dictValues,"ByteOrderCheck");
			recordRead.IsByteOrderConfirmed=Get(dictValues,"ByteOrderConfirmed")=="1";
			recordRead.Polarity=Get(dictValues,"Polarity");
			recordRead.PixelFlavor=Get(dictValues,"PixelFlavor");
			recordRead.IsPolarityConfirmed=Get(dictValues,"PolarityConfirmed")=="1";
			recordRead.MinValue=GetInt(dictValues,"Min");
			recordRead.MaxValue=GetInt(dictValues,"Max");
			recordRead.EffectiveBitsMeasured=GetInt(dictValues,"EffectiveBitsMeasured");
			recordRead.EffectiveBitsReported=GetInt(dictValues,"EffectiveBitsReported");
			recordRead.BitsStoredRule=GetInt(dictValues,"BitsStoredRule");
			recordRead.WindowLow=GetInt(dictValues,"WindowLow");
			recordRead.WindowHigh=GetInt(dictValues,"WindowHigh");
			recordRead.Serial=Get(dictValues,"Serial");
			recordRead.Model=Get(dictValues,"Model");
			recordRead.DsVersion=Get(dictValues,"DsVersion");
			recordRead.IsMetadataPresent=Get(dictValues,"Metadata")=="1";
			record=recordRead;
			return true;
		}

		private List<KeyValuePair<string,string>> GetPairs() {
			List<KeyValuePair<string,string>> listPairs=new List<KeyValuePair<string,string>>();
			void Add(string key,string value) {
				listPairs.Add(new KeyValuePair<string,string>(key,value??""));
			}
			Add("Device",ImagingDeviceNum.ToString(CultureInfo.InvariantCulture));
			Add("DeviceDescription",DeviceDescription);
			Add("Source",SourceName);
			Add("SourceProductName",SourceProductName);
			Add("DriverProductName",DriverIdentity?.ProductName);
			Add("DriverVersion",DriverIdentity?.VersionText);
			Add("DriverVersionInfo",DriverIdentity?.Info);
			Add("ImageSettingsHash",ImageSettingsHash);
			Add("Computer",ComputerName);
			Add("GatedSettingsHash",GatedSettingsHash);
			Add("Date",DateTimeVerified.ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture));
			Add("BitsRequested",ToText(BitsRequested));
			Add("BitDepthInEffect",ToText(BitDepthInEffect));
			Add("BitsDelivered",ToText(BitsDelivered));
			Add("Transfer",TransferMechanism);
			Add("Width",ToText(Width));
			Add("Height",ToText(Height));
			Add("XDpi",XDpi.ToString("0.##",CultureInfo.InvariantCulture));
			Add("YDpi",YDpi.ToString("0.##",CultureInfo.InvariantCulture));
			Add("ByteOrder16",ByteOrder16);
			Add("ByteOrderCheck",ByteOrderCheck);
			Add("ByteOrderConfirmed",IsByteOrderConfirmed ? "1" : "0");
			Add("Polarity",Polarity);
			Add("PixelFlavor",PixelFlavor);
			Add("PolarityConfirmed",IsPolarityConfirmed ? "1" : "0");
			Add("Min",ToText(MinValue));
			Add("Max",ToText(MaxValue));
			Add("EffectiveBitsMeasured",ToText(EffectiveBitsMeasured));
			Add("EffectiveBitsReported",ToText(EffectiveBitsReported));
			Add("BitsStoredRule",ToText(BitsStoredRule));
			Add("WindowLow",ToText(WindowLow));
			Add("WindowHigh",ToText(WindowHigh));
			Add("Serial",Serial);
			Add("Model",Model);
			Add("DsVersion",DsVersion);
			Add("Metadata",IsMetadataPresent ? "1" : "0");
			return listPairs;
		}

		private static string ToText(int? value) {
			return value==null ? "" : value.Value.ToString(CultureInfo.InvariantCulture);
		}

		private static string Get(Dictionary<string,string> dictValues,string key) {
			string value;
			return dictValues.TryGetValue(key,out value) ? (value??"").Trim() : "";
		}

		private static int? GetInt(Dictionary<string,string> dictValues,string key) {
			int value;
			if(int.TryParse(Get(dictValues,key),NumberStyles.Integer,CultureInfo.InvariantCulture,out value)) {
				return value;
			}
			return null;
		}

		private static double GetDouble(Dictionary<string,string> dictValues,string key) {
			double value;
			if(double.TryParse(Get(dictValues,key),NumberStyles.Float,CultureInfo.InvariantCulture,out value)) {
				return value;
			}
			return 0;
		}

		internal static string Escape(string value) {
			StringBuilder stringBuilder=new StringBuilder();
			foreach(char c in value??"") {
				switch(c) {
					case '%': stringBuilder.Append("%25"); break;
					case ';': stringBuilder.Append("%3B"); break;
					case '=': stringBuilder.Append("%3D"); break;
					case '\r': stringBuilder.Append("%0D"); break;
					case '\n': stringBuilder.Append("%0A"); break;
					default: stringBuilder.Append(c); break;
				}
			}
			return stringBuilder.ToString();
		}

		internal static string Unescape(string value) {
			return (value??"").Replace("%3B",";").Replace("%3D","=").Replace("%0D","\r").Replace("%0A","\n").Replace("%25","%");
		}
	}

	///<summary>What CaptureVerificationRecord.GetMismatch found first.</summary>
	public enum CaptureRecordMismatch {
		None,
		///<summary>A record made before CAP-16: no driver version, the whole-file hash. It never matches; run Test capture again.</summary>
		LegacyRecord,
		///<summary>Another device row, or another TWAIN source of the row.</summary>
		Source,
		///<summary>The open source's driver identity is not known (the source is not open, or reported none).</summary>
		DriverUnknown,
		///<summary>Another driver build: ProductName, MajorNum, MinorNum or Info differs.</summary>
		Driver,
		///<summary>Other image settings: [Device.N] Bits, ByteOrder16 or Polarity differs.</summary>
		ImageSettings,
	}
}
