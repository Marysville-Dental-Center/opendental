// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ImagingDeviceManager.Config;

namespace ImagingCapture.Settings {
	///<summary>The SHA-256 hash of the gated settings of capture.ini for one imaging device (design 9.3): the gated keys of [Session] and
	///[Twain], and those of that device's [Device.N] section. A Setup save of the Capture settings screen and a Test capture verification
	///record store it in the security log; the session compares the file's current hash with the latest one logged for this computer
	///(CAP-4), so a hand edit of a gated value is noticed. Keys that are not gated (hardware bindings such as [Camera], [Keys] and
	///[Sounds]) and other devices' sections never change the hash.
	///
	///The hash is over a canonical text (GetCanonicalText), so that only a change of meaning changes it:
	///- a fixed list of keys in a fixed order, whatever the order of sections and keys in the file, and whatever the case of their names
	///  (LocalCaptureConfig reads names case-insensitively, and the last of repeated keys wins);
	///- each value trimmed and lowercased (every gated value is read case-insensitively: numbers, 0/1 switches, enumerations, hex numbers,
	///  and source names, which the source gate compares ignoring case). Spaces and tabs inside a value are kept as written: the source gate
	///  and DisableIsSafeSources compare source names with them, so "Sensor  Source" and "Sensor Source" behave differently and must hash
	///  differently; for every other key such a value is invalid anyway;
	///- in the |-separated lists (AllowedSources, DisableIsSafeSources, RefusedDisableCodes) each entry normalised the same way and empty
	///  entries dropped, keeping the order;
	///- a missing key written like an empty value: both read as the key's default.
	///Comments and blank lines are not part of any value. The text starts with FormatName, so a later change of these rules gives different
	///hashes instead of colliding with this format.</summary>
	public static class GatedSettingsHash {
		///<summary>The first line of the canonical text.</summary>
		public const string FormatName="capture-gated-settings/1";
		public const string SectionSession="Session";

		///<summary>The gated keys of [Session], in hash order.</summary>
		public static readonly ReadOnlyCollection<string> ListSessionKeys=new ReadOnlyCollection<string>(new[] { "Enabled","IdleDisableMinutes" });
		///<summary>The gated keys of [Twain], in hash order.</summary>
		public static readonly ReadOnlyCollection<string> ListTwainKeys=new ReadOnlyCollection<string>(new[] { "ThreadModal","AcquireTimeoutMinutes",
			"Allow16Bit","RestrictSources","AllowedSources","JournalKeep","DisableIsSafeSources","SafeDisableCap","RefusedDisableCodes" });
		///<summary>The gated keys of [Device.N], in hash order.</summary>
		public static readonly ReadOnlyCollection<string> ListDeviceKeys=new ReadOnlyCollection<string>(new[] { "SessionMode","Bits","ByteOrder16",
			"Polarity","Headless","DisableIsSafe","SessionTimeoutMinutes","SafeDisableCap","RefusedDisableCodes","ContinuousCap","RedeliverCap",
			"NoActivateCap" });
		///<summary>Keys whose value is a list separated by '|'.</summary>
		private static readonly string[] _arrayListKeys={ "AllowedSources","DisableIsSafeSources","RefusedDisableCodes" };

		///<summary>True for a key this hash covers: a gated key of [Session], [Twain] or any [Device.N] section. Names are case-insensitive.</summary>
		public static bool IsGatedKey(string section,string key) {
			if(section==null || key==null) {
				return false;
			}
			section=section.Trim();
			key=key.Trim();
			if(section.Equals(SectionSession,StringComparison.OrdinalIgnoreCase)) {
				return ListSessionKeys.Contains(key,StringComparer.OrdinalIgnoreCase);
			}
			if(section.Equals(LocalCaptureConfig.SectionTwain,StringComparison.OrdinalIgnoreCase)) {
				return ListTwainKeys.Contains(key,StringComparer.OrdinalIgnoreCase);
			}
			if(section.StartsWith(LocalCaptureConfig.SectionDevicePrefix,StringComparison.OrdinalIgnoreCase)) {
				return ListDeviceKeys.Contains(key,StringComparer.OrdinalIgnoreCase);
			}
			return false;
		}

		///<summary>The hash of the gated settings for imagingDeviceNum: 64 lowercase hex digits. config null hashes as a file with no gated
		///values. Throws ArgumentOutOfRangeException when imagingDeviceNum is not positive.</summary>
		public static string Compute(LocalCaptureConfig config,long imagingDeviceNum) {
			return ComputeFromCanonicalText(GetCanonicalText(config,imagingDeviceNum));
		}

		///<summary>SHA-256 of the UTF-8 bytes of canonicalText, as 64 lowercase hex digits.</summary>
		public static string ComputeFromCanonicalText(string canonicalText) {
			byte[] bytesHash;
			using(SHA256 sha256=SHA256.Create()) {
				bytesHash=sha256.ComputeHash(Encoding.UTF8.GetBytes(canonicalText ?? ""));
			}
			StringBuilder stringBuilder=new StringBuilder(64);
			foreach(byte b in bytesHash) {
				stringBuilder.Append(b.ToString("x2",CultureInfo.InvariantCulture));
			}
			return stringBuilder.ToString();
		}

		///<summary>The text that is hashed, one "Section.Key=value" line per gated key, each ending in "\n":
		///  capture-gated-settings/1
		///  Device=12
		///  Session.Enabled=1
		///  ...
		///  Twain.RefusedDisableCodes=0x8001|0x8002
		///  Device.SessionMode=perslot
		///  ...
		///The device number is a line of its own, so the hash of one device never matches another's. Throws ArgumentOutOfRangeException
		///when imagingDeviceNum is not positive.</summary>
		public static string GetCanonicalText(LocalCaptureConfig config,long imagingDeviceNum) {
			if(imagingDeviceNum<=0) {
				throw new ArgumentOutOfRangeException(nameof(imagingDeviceNum),"An ImagingDeviceNum is positive.");
			}
			string sectionDevice=LocalCaptureConfig.GetDeviceSection(imagingDeviceNum);
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(FormatName).Append('\n');
			stringBuilder.Append("Device=").Append(imagingDeviceNum.ToString(CultureInfo.InvariantCulture)).Append('\n');
			AppendSection(stringBuilder,config,SectionSession,SectionSession,ListSessionKeys);
			AppendSection(stringBuilder,config,LocalCaptureConfig.SectionTwain,LocalCaptureConfig.SectionTwain,ListTwainKeys);
			AppendSection(stringBuilder,config,sectionDevice,"Device",ListDeviceKeys);
			return stringBuilder.ToString();
		}

		private static void AppendSection(StringBuilder stringBuilder,LocalCaptureConfig config,string section,string label,IEnumerable<string> listKeys) {
			foreach(string key in listKeys) {
				string value=config==null ? "" : config.GetString(section,key,"");
				stringBuilder.Append(label).Append('.').Append(key).Append('=').Append(NormalizeValue(key,value)).Append('\n');
			}
		}

		///<summary>The canonical form of one value (see the class summary).</summary>
		internal static string NormalizeValue(string key,string value) {
			if(!_arrayListKeys.Contains(key,StringComparer.OrdinalIgnoreCase)) {
				return NormalizeText(value);
			}
			List<string> listEntries=new List<string>();
			foreach(string entry in (value ?? "").Split('|')) {
				string entryNormalized=NormalizeText(entry);
				if(entryNormalized!="") {
					listEntries.Add(entryNormalized);
				}
			}
			return string.Join("|",listEntries);
		}

		///<summary>Trimmed (as the source gate trims names) and lowercased; spaces and tabs inside are kept.</summary>
		private static string NormalizeText(string value) {
			if(value==null) {
				return "";
			}
			return value.Trim().ToLowerInvariant();
		}
	}
}
