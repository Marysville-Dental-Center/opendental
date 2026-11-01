// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ImagingCapture.Settings {
	///<summary>A Test capture verification record (design 9.3): what Test capture measured on a phantom for one imaging device on one computer,
	///what the operator confirmed, and the SHA-256 hash of that device's gated settings at the time (GatedSettingsHash). It is stored as the
	///LogText of a Setup security-log entry with FKey=ImagingDeviceNum; there is no table of its own (design 9.1, no new tables).
	///
	///The session (CAP-4) refuses a 16-bit or inverted capture unless the latest record for the device and this computer matches the
	///device row's TWAIN source and the current gated settings (Matches) and passed (IsPassedFor16Bit). The text is key=value pairs, so it reads in the audit trail and parses
	///back (TryParse). Values are escaped: '%', ';', '=', CR and LF become %25, %3B, %3D, %0D and %0A.</summary>
	public sealed class CaptureVerificationRecord {
		///<summary>The format of the text; a later format gets a new name instead of being misread.</summary>
		public const string FormatName="capture-verification/1";
		///<summary>Every record's LogText starts with this.</summary>
		public const string LogPrefix="Capture verification record ("+FormatName+"): ";
		public const string ByteOrderCheckNotApplicable="n/a";

		public long ImagingDeviceNum { get; set; }
		public string DeviceDescription { get; set; }="";
		///<summary>The TWAIN source of the device row when the test ran (ImagingDevice.TwainName), which Matches compares: the gated settings
		///hash covers capture.ini only, and the row may later name another source.</summary>
		public string SourceName { get; set; }="";
		///<summary>The ProductName the source reported at open (TW_IDENTITY), for the audit trail. Not compared.</summary>
		public string SourceProductName { get; set; }="";
		///<summary>The computer Test capture ran on (ODEnvironment.MachineName). capture.ini is per computer.</summary>
		public string ComputerName { get; set; }="";
		///<summary>GatedSettingsHash.Compute of the capture.ini the test ran with: 64 lowercase hex digits.</summary>
		public string GatedSettingsHash { get; set; }="";
		///<summary>When the record was written, local time.</summary>
		public DateTime DateTimeVerified { get; set; }
		///<summary>8 or 16: what the test enable asked for.</summary>
		public int BitsRequested { get; set; }
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
		///<summary>What the BitsStored rule gives for this image (12 or 16), for 16-bit images.</summary>
		public int? BitsStoredRule { get; set; }
		public int? WindowLow { get; set; }
		public int? WindowHigh { get; set; }
		///<summary>From the capture metadata, when it had them.</summary>
		public string Serial { get; set; }="";
		public string Model { get; set; }="";
		public string DsVersion { get; set; }="";
		public bool IsMetadataPresent { get; set; }

		///<summary>The record is for this device, computer and TWAIN source (the device row's ImagingDevice.TwainName) and was made with these
		///gated settings. Computer and source names compare trimmed and ignoring case, as the source gate compares source names.</summary>
		public bool Matches(long imagingDeviceNum,string computerName,string sourceName,string gatedSettingsHash) {
			return ImagingDeviceNum==imagingDeviceNum
				&& string.Equals((ComputerName??"").Trim(),(computerName??"").Trim(),StringComparison.OrdinalIgnoreCase)
				&& !string.IsNullOrWhiteSpace(SourceName)
				&& string.Equals(SourceName.Trim(),(sourceName??"").Trim(),StringComparison.OrdinalIgnoreCase)
				&& !string.IsNullOrEmpty(GatedSettingsHash)
				&& string.Equals(GatedSettingsHash,(gatedSettingsHash??"").Trim(),StringComparison.OrdinalIgnoreCase);
		}

		///<summary>The record verifies 16-bit capture: a 16-bit image was measured from a 16-bit enable over memory transfer (IsNegotiated16Bit),
		///the automatic byte-order check did not find the bytes swapped, the operator confirmed byte order and polarity on the phantom (design
		///7.8 condition 2), and the saver would write the samples (IsVanillaNotInverted is false: DicomIntraOralWriter refuses TWPF_VANILLA
		///samples stored as delivered).</summary>
		public bool IsPassedFor16Bit =>
			IsNegotiated16Bit
			&& !string.Equals(ByteOrderCheck,"LooksSwapped",StringComparison.OrdinalIgnoreCase)
			&& IsByteOrderConfirmed
			&& IsPolarityConfirmed
			&& !IsVanillaNotInverted;

		///<summary>A 16-bit image from a 16-bit enable over memory transfer: the only 16-bit images the saver writes as DICOM (a 16bpp native DIB,
		///or 16-bit samples in an 8-bit enable, are refused).</summary>
		public bool IsNegotiated16Bit =>
			BitsDelivered==16 && BitsRequested==16 && string.Equals(TransferMechanism,"Memory",StringComparison.OrdinalIgnoreCase);

		///<summary>The source delivered TWPF_VANILLA samples and Polarity is not Invert: the DICOM writer refuses such samples.</summary>
		public bool IsVanillaNotInverted =>
			string.Equals(PixelFlavor,"Vanilla",StringComparison.OrdinalIgnoreCase) && !string.Equals(Polarity,"Invert",StringComparison.OrdinalIgnoreCase);

		///<summary>The security-log text: LogPrefix, then key=value pairs separated by "; ".</summary>
		public string ToLogText() {
			List<KeyValuePair<string,string>> listPairs=GetPairs();
			return LogPrefix+string.Join("; ",listPairs.Select(x => x.Key+"="+Escape(x.Value)));
		}

		///<summary>Reads a record from a security-log text that starts with LogPrefix. False for any other text or a record without its
		///device, computer and hash.</summary>
		public static bool TryParse(string logText,out CaptureVerificationRecord record) {
			record=null;
			if(logText==null || !logText.StartsWith(LogPrefix,StringComparison.Ordinal)) {
				return false;
			}
			Dictionary<string,string> dictValues=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			foreach(string part in logText.Substring(LogPrefix.Length).Split(';')) {
				int idx=part.IndexOf('=');
				if(idx<=0) {
					continue;
				}
				dictValues[part.Substring(0,idx).Trim()]=Unescape(part.Substring(idx+1));
			}
			CaptureVerificationRecord recordRead=new CaptureVerificationRecord();
			long imagingDeviceNum;
			if(!long.TryParse(Get(dictValues,"Device"),NumberStyles.Integer,CultureInfo.InvariantCulture,out imagingDeviceNum) || imagingDeviceNum<=0) {
				return false;
			}
			recordRead.ImagingDeviceNum=imagingDeviceNum;
			recordRead.ComputerName=Get(dictValues,"Computer");
			recordRead.GatedSettingsHash=Get(dictValues,"GatedSettingsHash").ToLowerInvariant();
			if(recordRead.ComputerName=="" || recordRead.GatedSettingsHash.Length!=64) {
				return false;
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
			Add("Computer",ComputerName);
			Add("GatedSettingsHash",GatedSettingsHash);
			Add("Date",DateTimeVerified.ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture));
			Add("BitsRequested",ToText(BitsRequested));
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

		private static string Escape(string value) {
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

		private static string Unescape(string value) {
			return (value??"").Replace("%3B",";").Replace("%3D","=").Replace("%0D","\r").Replace("%0A","\n").Replace("%25","%");
		}
	}
}
