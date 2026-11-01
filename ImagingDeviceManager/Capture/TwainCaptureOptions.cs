// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ImagingDeviceManager.Config;
using ImagingDeviceManager.Journal;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager.Capture {
	///<summary>The device profile a TwainCaptureSession runs with: the [Device.N] section of capture.ini for one imaging device, and the
	///[Twain] keys that apply to it (design 9.3). Device-specific numbers (custom capabilities, refusal codes) come only from here, as data
	///copied from the source's published contract; the library has none of its own. Every value has the design 9.3 default when the file
	///or the key is missing or invalid, except Bits and Allow16Bit: missing or empty, they are 16 and 1 (owner decision 2026-11-01); an invalid
	///Bits is 8 and an invalid Allow16Bit is 0, so a value that cannot be read never turns more than 8 bits on. An invalid value is listed in
	///ListWarnings. The hash check of the gated block (CAP-4) is the caller's: this class reads the file as it is.</summary>
	public sealed class TwainCaptureOptions {
		///<summary>imagingdevice.ImagingDeviceNum, N of [Device.N]. 0 when the options were not read from a file.</summary>
		public long ImagingDeviceNum { get; set; }
		///<summary>[Device.N] SessionMode, default PerSlot.</summary>
		public TwainSessionMode SessionMode { get; set; }=TwainSessionMode.PerSlot;
		///<summary>[Device.N] Bits: 16 (the default, owner decision 2026-11-01), 14 or 8; any other value is a warning and 8. The depth the capture
		///session asks for once its gates hold (CaptureBitDepthDecision); the library negotiates only what an enable request asks
		///(TwainEnableRequest.Bits) and falls back from 16 to 14 or 8 by what the source lists.</summary>
		public int Bits { get; set; }=TwainBitDepth.Bits16;
		///<summary>[Device.N] ByteOrder16, default LE (review flag). The default byte order of 16-bit samples.</summary>
		public TwainByteOrder ByteOrder16 { get; set; }=TwainByteOrder.LE;
		///<summary>[Device.N] Polarity, default AsDelivered (review flag). Carried for the saver; the library never inverts pixels.</summary>
		public TwainPolarity Polarity { get; set; }=TwainPolarity.AsDelivered;
		///<summary>[Device.N] Headless, default 0: ShowUI=1. A source that reports CAP_UICONTROLLABLE FALSE always gets ShowUI=1.</summary>
		public bool Headless { get; set; }
		///<summary>[Device.N] DisableIsSafe, default 0. In effect only when SafeDisableCap is set and MSG_SET TRUE on it succeeds (and the CAP-0
		///test of W13 passed, which is what setting it to 1 records).</summary>
		public bool DisableIsSafe { get; set; }
		///<summary>[Device.N] SessionTimeoutMinutes, default 0 (none). Measured from each MSG_ENABLEDS until the enable ends, and honoured only
		///with safe disable in effect (design 7.6). The runbook keeps the source's own session timeout shorter.</summary>
		public TimeSpan SessionTimeout { get; set; }=TimeSpan.Zero;
		///<summary>[Device.N] SafeDisableCap (hex): the W13 capability, set TRUE in state 4 before each enable when DisableIsSafe=1.</summary>
		public ushort? SafeDisableCap { get; set; }
		///<summary>[Device.N] RefusedDisableCodes (hex, separated by |): the W13 "disable refused" condition codes.</summary>
		public List<ushort> RefusedDisableCodes { get; set; }=new List<ushort>();
		///<summary>[Device.N] ContinuousCap (hex): set TRUE before each enable when SessionMode=Continuous (W8).</summary>
		public ushort? ContinuousCap { get; set; }
		///<summary>[Device.N] RedeliverCap (hex): set TRUE only right before a redelivery enable, and FALSE right after it (W15).</summary>
		public ushort? RedeliverCap { get; set; }
		///<summary>[Device.N] NoActivateCap (hex): set TRUE before each enable, so the sensor software's window does not take activation (W16).</summary>
		public ushort? NoActivateCap { get; set; }
		///<summary>[Twain] Allow16Bit, default 1 (owner decision 2026-11-01): more than 8 bits (14 or 16) are allowed on this computer. The key keeps
		///its name for compatibility; 0 is the computer's kill switch and forces 8 bits everywhere, and so does a value that is not 0 or 1 (a
		///warning).</summary>
		public bool Allow16Bit { get; set; }=true;
		///<summary>[Twain] RestrictSources and AllowedSources: the source gate, checked when the session opens the source.</summary>
		public bool RestrictSources { get; set; }
		public List<string> AllowedSources { get; set; }=new List<string>();
		///<summary>[Twain] JournalKeep, default 200.</summary>
		public int JournalKeep { get; set; }=CaptureJournal.KeepDefault;
		///<summary>How long Open waits for the worker, default 60 s.</summary>
		public TimeSpan OpenTimeout { get; set; }=TimeSpan.FromSeconds(60);
		///<summary>The invalid values FromConfig found and replaced by their defaults, as sentences for Test capture and the log.</summary>
		public List<string> ListWarnings { get; set; }=new List<string>();
		///<summary>The file the values came from, or null.</summary>
		public string ConfigPath { get; set; }
		///<summary>The library's bound for the wait after a refused disable (the contract asks for at least 20 s). Not in capture.ini.</summary>
		internal TimeSpan RefusedDisableWait { get; set; }=TwainCaptureSettings.RefusedDisableWaitDefault;
		///<summary>How often the worker's message loop wakes up. Not in capture.ini; tests shorten it.</summary>
		internal int TickMilliseconds { get; set; }=200;

		///<summary>[Device.N] Bits is 16 or 14 and [Twain] Allow16Bit=1 (the defaults). The session window still needs its other gates (the capture
		///settings check, a matching Test capture record that passed at that depth, and a target that can be written as DICOM) before it asks for
		///more than 8 bits (design 7.8, 7.10, 9.3).</summary>
		public bool IsAbove8BitConfigured => Bits>8 && Allow16Bit;

		///<summary>The [Device.N] section for imagingDeviceNum and the [Twain] keys of config, with design 9.3 defaults. config null gives the
		///defaults. Never throws.</summary>
		public static TwainCaptureOptions FromConfig(LocalCaptureConfig config,long imagingDeviceNum) {
			TwainCaptureOptions options=new TwainCaptureOptions { ImagingDeviceNum=imagingDeviceNum };
			if(config==null) {
				return options;
			}
			options.ConfigPath=config.PathLoaded;
			try {
				string section=LocalCaptureConfig.GetDeviceSection(imagingDeviceNum);
				options.SessionMode=ReadEnum(config,section,"SessionMode",TwainSessionMode.PerSlot,options.ListWarnings);
				//Missing or empty: the default. A value that does not read as a number is a warning and 8, like any other value but 8, 14 or 16.
				int bits=config.GetString(section,"Bits","").Trim()=="" ? TwainBitDepth.Bits16 : config.GetInt(section,"Bits",int.MinValue);
				if(!TwainBitDepth.IsAllowed(bits)) {
					options.ListWarnings.Add("["+section+"] Bits="+config.GetString(section,"Bits")+" is not 8, 14 or 16; 8 is used.");
					bits=TwainBitDepth.Bits8;
				}
				options.Bits=bits;
				options.ByteOrder16=ReadEnum(config,section,"ByteOrder16",TwainByteOrder.LE,options.ListWarnings);
				options.Polarity=ReadEnum(config,section,"Polarity",TwainPolarity.AsDelivered,options.ListWarnings);
				options.Headless=config.GetBool(section,"Headless",false);
				options.DisableIsSafe=config.GetBool(section,"DisableIsSafe",false);
				options.SessionTimeout=TimeSpan.FromMinutes(Math.Max(0,config.GetInt(section,"SessionTimeoutMinutes",0)));
				options.SafeDisableCap=ReadCapability(config,section,"SafeDisableCap",options.ListWarnings);
				options.ContinuousCap=ReadCapability(config,section,"ContinuousCap",options.ListWarnings);
				options.RedeliverCap=ReadCapability(config,section,"RedeliverCap",options.ListWarnings);
				options.NoActivateCap=ReadCapability(config,section,"NoActivateCap",options.ListWarnings);
				foreach(int code in config.GetHexList(section,"RefusedDisableCodes")) {
					if(code>0 && code<=ushort.MaxValue && !options.RefusedDisableCodes.Contains((ushort)code)) {
						options.RefusedDisableCodes.Add((ushort)code);
					}
				}
				options.Allow16Bit=config.Allow16Bit;
				if(config.IsAllow16BitInvalid) {
					options.ListWarnings.Add("[Twain] Allow16Bit="+config.GetString(LocalCaptureConfig.SectionTwain,"Allow16Bit").Trim()+" is not 0 or 1; 8 "
						+"bits are used.");
				}
				options.RestrictSources=config.RestrictSources;
				options.AllowedSources=config.AllowedSources;
				options.JournalKeep=config.JournalKeep;
			}
			catch(Exception ex) {
				options.ListWarnings.Add("capture.ini could not be read: "+ex.Message);
			}
			return options;
		}

		private static T ReadEnum<T>(LocalCaptureConfig config,string section,string key,T defaultValue,List<string> listWarnings) where T:struct {
			string value=config.GetString(section,key,"").Trim();
			if(value=="") {
				return defaultValue;
			}
			T result;
			if(Enum.TryParse(value,true,out result) && Enum.IsDefined(typeof(T),result) && !value.All(char.IsDigit)) {
				return result;
			}
			listWarnings.Add("["+section+"] "+key+"="+value+" is not one of "+string.Join(", ",Enum.GetNames(typeof(T)))+"; "+defaultValue+" is used.");
			return defaultValue;
		}

		private static ushort? ReadCapability(LocalCaptureConfig config,string section,string key,List<string> listWarnings) {
			string value=config.GetString(section,key,"").Trim();
			if(value=="") {
				return null;
			}
			int? number=config.GetHex(section,key);
			if(number==null || number.Value<=0 || number.Value>ushort.MaxValue) {
				listWarnings.Add("["+section+"] "+key+"="+value+" is not a capability number (hex, 0x0001-0xFFFF); it is not used.");
				return null;
			}
			return (ushort)number.Value;
		}

		public TwainCaptureOptions Clone() {
			TwainCaptureOptions clone=(TwainCaptureOptions)MemberwiseClone();
			clone.RefusedDisableCodes=new List<ushort>(RefusedDisableCodes);
			clone.AllowedSources=new List<string>(AllowedSources);
			clone.ListWarnings=new List<string>(ListWarnings);
			return clone;
		}

		///<summary>The protocol settings for productName: the safe-disable capability only with DisableIsSafe=1, the refusal codes, no acquire
		///timeout (the capture session has its own, SessionTimeout).</summary>
		internal TwainCaptureSettings ToSessionSettings(string productName) {
			TwainCaptureSettings settings=new TwainCaptureSettings();
			if(DisableIsSafe && SafeDisableCap!=null) {
				settings.SafeDisableCap=SafeDisableCap;
				settings.ListDisableIsSafeSources=new List<string> { productName??"" };
			}
			settings.ListRefusedDisableCodes=new List<ushort>(RefusedDisableCodes);
			settings.AcquireTimeout=TimeSpan.Zero;
			settings.RefusedDisableWait=RefusedDisableWait;
			return settings;
		}

		///<summary>The source gate of these options ([Twain] RestrictSources and AllowedSources) lets productName be opened: always without
		///RestrictSources, otherwise only when it is listed (ignoring case and surrounding spaces). The capture session checks it before its
		///window opens anything (design 7.12 "Source not enabled here"); Open checks it again.</summary>
		public bool IsSourceAllowed(string productName) {
			return ToSourceGate().IsAllowed(productName);
		}

		internal TwainSourceGate ToSourceGate() {
			return new TwainSourceGate { IsRestricted=RestrictSources,ListAllowedSources=new List<string>(AllowedSources) };
		}

		///<summary>The gated values in effect, as a journal sidecar records them (design 7.1, 9.3).</summary>
		internal List<SidecarField> GetSidecarFields() {
			string device="Device."+ImagingDeviceNum.ToString(CultureInfo.InvariantCulture)+".";
			Func<ushort?,string> hex=x => x==null ? "" : TwainSession.ToHex(x.Value);
			return new List<SidecarField> {
				new SidecarField(device+"SessionMode",SessionMode.ToString()),
				new SidecarField(device+"Bits",Bits.ToString(CultureInfo.InvariantCulture)),
				new SidecarField(device+"ByteOrder16",ByteOrder16.ToString()),
				new SidecarField(device+"Polarity",Polarity.ToString()),
				new SidecarField(device+"Headless",Headless ? "1" : "0"),
				new SidecarField(device+"DisableIsSafe",DisableIsSafe ? "1" : "0"),
				new SidecarField(device+"SessionTimeoutMinutes",((int)SessionTimeout.TotalMinutes).ToString(CultureInfo.InvariantCulture)),
				new SidecarField(device+"SafeDisableCap",hex(SafeDisableCap)),
				new SidecarField(device+"RefusedDisableCodes",string.Join("|",RefusedDisableCodes.Select(x => TwainSession.ToHex(x)))),
				new SidecarField(device+"ContinuousCap",hex(ContinuousCap)),
				new SidecarField(device+"RedeliverCap",hex(RedeliverCap)),
				new SidecarField(device+"NoActivateCap",hex(NoActivateCap)),
				new SidecarField("Twain.Allow16Bit",Allow16Bit ? "1" : "0"),
				new SidecarField("Twain.RestrictSources",RestrictSources ? "1" : "0"),
				new SidecarField("Twain.AllowedSources",string.Join("|",AllowedSources)),
				new SidecarField("Twain.JournalKeep",JournalKeep.ToString(CultureInfo.InvariantCulture)),
				new SidecarField("ConfigFile",ConfigPath??"(none: defaults)"),
			};
		}
	}
}
