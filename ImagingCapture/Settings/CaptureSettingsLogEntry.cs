// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenDentBusiness;

namespace ImagingCapture.Settings {
	///<summary>One value the Capture settings screen changed.</summary>
	public sealed class CaptureSettingChange {
		///<summary>The capture.ini section as written: "Session", "Twain", "Device.12", "Keys" or "Sounds".</summary>
		public string Section { get; set; }="";
		public string Key { get; set; }="";
		///<summary>The value in the file before the save, trimmed; "" when the key was missing or empty (both read as the default).</summary>
		public string OldValue { get; set; }="";
		///<summary>The value in the file after the save, trimmed.</summary>
		public string NewValue { get; set; }="";
		///<summary>A gated key (part of GatedSettingsHash).</summary>
		public bool IsGated { get; set; }
		///<summary>Not an edit of the screen's: a gated value that was changed outside Open Dental since the last save, which this save accepts.
		///OldValue is the value last saved, NewValue the one in the file (design 9.3, 10.7).</summary>
		public bool IsChangedOutside { get; set; }

		public const string TextChangedOutside="(changed outside Open Dental)";

		///<summary>"Device.12.Bits".</summary>
		public string Label => Section+"."+Key;

		///<summary>"[Device.12] Bits: 8 -> 16", with " (changed outside Open Dental)" for a change accepted from the file.</summary>
		public string GetText() {
			return "["+Section+"] "+Key+": "+Show(OldValue)+" -> "+Show(NewValue)+(IsChangedOutside ? " "+TextChangedOutside : "");
		}

		private static string Show(string value) {
			return string.IsNullOrEmpty(value) ? "(empty)" : value;
		}
	}

	///<summary>A save of the Capture settings screen for one device on one computer (design 9.3, 10.7): the computer, the device, every changed
	///key's old and new value, the gated values as saved, and GatedSettingsHash of the saved file for that device. It is stored as the LogText
	///of a Setup security-log entry with PatNum 0 and FKey=ImagingDeviceNum, like a Test capture verification record (no table of its own,
	///design 9.1). The capture session, Test capture and the 16-bit decision compare the file's current hash with the latest entry for the device
	///and this computer (CaptureSettingsCheck).
	///
	///The text is key=value pairs separated by "; ", escaped as CaptureVerificationRecord escapes them, so it reads in the audit trail and parses
	///back (TryParse):
	///  Capture settings saved (capture-settings/1): Device=12; DeviceDescription=..; Computer=..; File=..; GatedSettingsHash=..;
	///  PreviousGatedSettingsHash=..; Date=2026-10-31T09:15:00; Note=..; Changes=2; Change1=Twain.Allow16Bit; Change1.Old=0; Change1.New=1;
	///  Change1.Gated=1; ...; Gated.Session.Enabled=1; ...; Gated.Device.NoActivateCap=
	///A gated value changed outside Open Dental since the last save, which the save accepts, is listed as a change too, with ChangeN.Outside=1,
	///its old value as last saved and its new value as in the file.
	///The gated values are under their GatedSettingsHash names ("Device." for the device's section), so the entry also says what the settings
	///were when last saved: the screen shows which ones changed since, and the safe values keep RestrictSources and AllowedSources as last
	///saved.</summary>
	public sealed class CaptureSettingsLogEntry {
		public const string FormatName="capture-settings/1";
		///<summary>Every entry's LogText starts with this.</summary>
		public const string LogPrefix="Capture settings saved ("+FormatName+"): ";
		private const string PrefixGated="Gated.";

		public long ImagingDeviceNum { get; set; }
		public string DeviceDescription { get; set; }="";
		///<summary>The computer the file is on (ODEnvironment.MachineName). capture.ini is per computer.</summary>
		public string ComputerName { get; set; }="";
		///<summary>The capture.ini file that was saved.</summary>
		public string FilePath { get; set; }="";
		///<summary>GatedSettingsHash.Compute of the file as saved: 64 lowercase hex digits.</summary>
		public string GatedSettingsHash { get; set; }="";
		///<summary>The hash of the file before the save, or "".</summary>
		public string PreviousGatedSettingsHash { get; set; }="";
		///<summary>Local time of the save.</summary>
		public DateTime DateTimeSaved { get; set; }
		///<summary>Why the entry was written, one sentence: a save of this device's settings, or shared [Session] and [Twain] keys changed while
		///another device's settings were saved.</summary>
		public string Note { get; set; }="";
		public List<CaptureSettingChange> ListChanges { get; set; }=new List<CaptureSettingChange>();
		///<summary>The gated values as saved, by their GatedSettingsHash names ("Session.Enabled", "Twain.AllowedSources", "Device.Bits"), trimmed
		///as in the file.</summary>
		public Dictionary<string,string> DictGatedValues { get; set; }=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

		///<summary>The gated value saved under label ("Twain.RestrictSources"), or null when the entry does not have it.</summary>
		public string GetGatedValue(string label) {
			string value;
			return DictGatedValues.TryGetValue(label??"",out value) ? value : null;
		}

		///<summary>The security-log text: LogPrefix, then key=value pairs separated by "; ".</summary>
		public string ToLogText() {
			List<KeyValuePair<string,string>> listPairs=new List<KeyValuePair<string,string>>();
			void Add(string key,string value) {
				listPairs.Add(new KeyValuePair<string,string>(key,value??""));
			}
			Add("Device",ImagingDeviceNum.ToString(CultureInfo.InvariantCulture));
			Add("DeviceDescription",DeviceDescription);
			Add("Computer",ComputerName);
			Add("File",FilePath);
			Add("GatedSettingsHash",GatedSettingsHash);
			Add("PreviousGatedSettingsHash",PreviousGatedSettingsHash);
			Add("Date",DateTimeSaved.ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture));
			Add("Note",Note);
			Add("Changes",ListChanges.Count.ToString(CultureInfo.InvariantCulture));
			for(int i=0;i<ListChanges.Count;i++) {
				string name="Change"+(i+1).ToString(CultureInfo.InvariantCulture);
				Add(name,ListChanges[i].Label);
				Add(name+".Old",ListChanges[i].OldValue);
				Add(name+".New",ListChanges[i].NewValue);
				Add(name+".Gated",ListChanges[i].IsGated ? "1" : "0");
				if(ListChanges[i].IsChangedOutside) {
					Add(name+".Outside","1");
				}
			}
			foreach(KeyValuePair<string,string> pair in DictGatedValues) {
				Add(PrefixGated+pair.Key,pair.Value);
			}
			return LogPrefix+string.Join("; ",listPairs.Select(x => x.Key+"="+CaptureVerificationRecord.Escape(x.Value)));
		}

		///<summary>Reads an entry from a security-log text that starts with LogPrefix. False for any other text, or an entry without its device,
		///computer and hash.</summary>
		public static bool TryParse(string logText,out CaptureSettingsLogEntry entry) {
			entry=null;
			if(logText==null || !logText.StartsWith(LogPrefix,StringComparison.Ordinal)) {
				return false;
			}
			//In order, so that the gated values keep the order they were written in.
			List<KeyValuePair<string,string>> listPairs=new List<KeyValuePair<string,string>>();
			Dictionary<string,string> dictValues=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			foreach(string part in logText.Substring(LogPrefix.Length).Split(';')) {
				int idx=part.IndexOf('=');
				if(idx<=0) {
					continue;
				}
				string key=part.Substring(0,idx).Trim();
				string value=CaptureVerificationRecord.Unescape(part.Substring(idx+1)).Trim();
				listPairs.Add(new KeyValuePair<string,string>(key,value));
				dictValues[key]=value;
			}
			CaptureSettingsLogEntry entryRead=new CaptureSettingsLogEntry();
			long imagingDeviceNum;
			if(!long.TryParse(Get(dictValues,"Device"),NumberStyles.Integer,CultureInfo.InvariantCulture,out imagingDeviceNum) || imagingDeviceNum<=0) {
				return false;
			}
			entryRead.ImagingDeviceNum=imagingDeviceNum;
			entryRead.ComputerName=Get(dictValues,"Computer");
			entryRead.GatedSettingsHash=Get(dictValues,"GatedSettingsHash").ToLowerInvariant();
			if(entryRead.ComputerName=="" || entryRead.GatedSettingsHash.Length!=64) {
				return false;
			}
			entryRead.DeviceDescription=Get(dictValues,"DeviceDescription");
			entryRead.FilePath=Get(dictValues,"File");
			entryRead.PreviousGatedSettingsHash=Get(dictValues,"PreviousGatedSettingsHash").ToLowerInvariant();
			DateTime dateTime;
			if(DateTime.TryParseExact(Get(dictValues,"Date"),"yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out dateTime)) {
				entryRead.DateTimeSaved=dateTime;
			}
			entryRead.Note=Get(dictValues,"Note");
			int countChanges;
			int.TryParse(Get(dictValues,"Changes"),NumberStyles.Integer,CultureInfo.InvariantCulture,out countChanges);
			for(int i=1;i<=countChanges;i++) {
				string name="Change"+i.ToString(CultureInfo.InvariantCulture);
				string label=Get(dictValues,name);
				int idxDot=label.LastIndexOf('.');
				if(idxDot<=0 || idxDot==label.Length-1) {
					continue;
				}
				entryRead.ListChanges.Add(new CaptureSettingChange {
					Section=label.Substring(0,idxDot),
					Key=label.Substring(idxDot+1),
					OldValue=Get(dictValues,name+".Old"),
					NewValue=Get(dictValues,name+".New"),
					IsGated=Get(dictValues,name+".Gated")=="1",
					IsChangedOutside=Get(dictValues,name+".Outside")=="1",
				});
			}
			foreach(KeyValuePair<string,string> pair in listPairs) {
				if(pair.Key.StartsWith(PrefixGated,StringComparison.OrdinalIgnoreCase) && pair.Key.Length>PrefixGated.Length) {
					entryRead.DictGatedValues[pair.Key.Substring(PrefixGated.Length)]=pair.Value;
				}
			}
			entry=entryRead;
			return true;
		}

		///<summary>The latest entry for imagingDeviceNum and computerName (compared trimmed and ignoring case) among listSecurityLogs, by
		///LogDateTime and then SecurityLogNum, or null when the screen never saved this device's settings on this computer.</summary>
		public static CaptureSettingsLogEntry FindLatest(IEnumerable<SecurityLog> listSecurityLogs,long imagingDeviceNum,string computerName) {
			string computer=(computerName??"").Trim();
			CaptureSettingsLogEntry entryLatest=null;
			SecurityLog securityLogLatest=null;
			foreach(SecurityLog securityLog in (listSecurityLogs??Enumerable.Empty<SecurityLog>()).Where(x => x!=null)) {
				CaptureSettingsLogEntry entry;
				if(!TryParse(securityLog.LogText,out entry)) {
					continue;
				}
				if(entry.ImagingDeviceNum!=imagingDeviceNum
					|| !string.Equals((entry.ComputerName??"").Trim(),computer,StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				if(securityLogLatest!=null && (securityLog.LogDateTime<securityLogLatest.LogDateTime
					|| (securityLog.LogDateTime==securityLogLatest.LogDateTime && securityLog.SecurityLogNum<securityLogLatest.SecurityLogNum)))
				{
					continue;
				}
				securityLogLatest=securityLog;
				entryLatest=entry;
			}
			return entryLatest;
		}

		private static string Get(Dictionary<string,string> dictValues,string key) {
			string value;
			return dictValues.TryGetValue(key,out value) ? (value??"") : "";
		}
	}
}
