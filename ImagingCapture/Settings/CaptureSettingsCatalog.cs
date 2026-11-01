// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ImagingCapture.Session;
using ImagingDeviceManager.Config;

namespace ImagingCapture.Settings {
	///<summary>What kind of value a capture.ini key holds, which decides how the Capture settings screen checks it (with the parsers the library
	///reads it with) and what it offers.</summary>
	public enum CaptureSettingKind {
		///<summary>0 or 1 (LocalCaptureConfig.GetBool also reads true/false and yes/no).</summary>
		Switch,
		///<summary>A whole number, 0 or more (LocalCaptureConfig.GetInt).</summary>
		WholeNumber,
		///<summary>One of ArrayChoices (TwainCaptureOptions reads it).</summary>
		Choice,
		///<summary>A custom capability number in hex, 0x0001-0xFFFF, from the source's published contract; empty means not used.</summary>
		Capability,
		///<summary>Condition codes in hex separated by '|'.</summary>
		CodeList,
		///<summary>TWAIN ProductNames separated by '|'.</summary>
		SourceList,
		///<summary>A key or pedal chord (CaptureKeyParser); empty means not bound.</summary>
		Key,
		///<summary>A Windows system sound name (CaptureSoundNames); empty means silent.</summary>
		Sound,
	}

	///<summary>One key of capture.ini that the Capture settings screen edits.</summary>
	public sealed class CaptureSettingDef {
		public CaptureSettingDef(string section,string key,CaptureSettingKind kind,string defaultText,bool isGated,string help,params string[] arrayChoices) {
			Section=section;
			Key=key;
			Kind=kind;
			DefaultText=defaultText??"";
			IsGated=isGated;
			Help=help??"";
			ArrayChoices=arrayChoices??new string[0];
		}

		///<summary>"Session", "Twain", "Device" (the [Device.N] section of the device the screen is for), "Keys" or "Sounds".</summary>
		public string Section { get; }
		public string Key { get; }
		public CaptureSettingKind Kind { get; }
		///<summary>What the program uses when the key is missing or empty, as written in the file ("" for none).</summary>
		public string DefaultText { get; }
		///<summary>Marked "; gated" in design 9.3: it changes pixels, display or safety behaviour, so it changes only through this screen, with
		///Setup permission and a security-log entry, and it is part of GatedSettingsHash.</summary>
		public bool IsGated { get; }
		public string Help { get; }
		///<summary>The values a Choice key takes, as written.</summary>
		public IReadOnlyList<string> ArrayChoices { get; }
		///<summary>A WholeNumber key: the smallest value the program takes as written (0 by default). A smaller one is refused, with
		///TextBelowMinimum, because the program would use another value than the one saved and logged.</summary>
		public int MinValue { get; internal set; }
		///<summary>Why a value below MinValue is refused, one sentence after "[Twain] JournalKeep=0 ", or "" for "is below N.".</summary>
		public string TextBelowMinimum { get; internal set; }="";

		///<summary>"Session.Enabled", "Device.Bits": the name GatedSettingsHash's canonical text and the security-log entry use.</summary>
		public string Label => Section+"."+Key;

		///<summary>The capture.ini section: "Device.12" for Device, the section itself otherwise.</summary>
		public string GetSectionName(long imagingDeviceNum) {
			return Section==CaptureSettingsCatalog.SectionDevice ? LocalCaptureConfig.GetDeviceSection(imagingDeviceNum) : Section;
		}

		///<summary>"[Device.12] Bits".</summary>
		public string GetDisplayName(long imagingDeviceNum) {
			return "["+GetSectionName(imagingDeviceNum)+"] "+Key;
		}
	}

	///<summary>The keys of capture.ini that the Capture settings screen edits for one device on this computer (design 9.3): [Session], [Twain],
	///the device's [Device.N], [Keys] and [Sounds]. The camera sections ([Camera], [Trigger*], [Level.N]) stay with FormVideo's settings and Learn.
	///The gated keys are exactly the ones GatedSettingsHash covers (a test checks this), so what this screen logs is what the hash check
	///compares. Help texts say what a key does in words; device-specific numbers are never suggested: they come from the source's published
	///contract.</summary>
	public static class CaptureSettingsCatalog {
		public const string SectionDevice="Device";
		private const string HelpCapability="A custom capability number in hex (0x0001-0xFFFF) from the TWAIN source's published contract; empty means "
			+"not used.";
		private const string HelpCodes="Condition codes in hex separated by |, from the TWAIN source's published contract.";
		private const string HelpSound="A Windows system sound: SystemAsterisk, SystemHand, SystemExclamation, SystemQuestion, SystemDefault or Beep; "
			+"empty is silent.";
		private const string HelpKey="A key or pedal, such as F9, Ctrl+F9, NumPad5, Pause or a virtual-key code such as 0x7A; empty means not bound.";

		///<summary>Every key the screen edits, in the order it shows them.</summary>
		public static readonly ReadOnlyCollection<CaptureSettingDef> ListSettings=new ReadOnlyCollection<CaptureSettingDef>(Build());

		///<summary>The definition of section.key ("Device" for any [Device.N]), names ignoring case, or null.</summary>
		public static CaptureSettingDef Find(string section,string key) {
			return ListSettings.FirstOrDefault(x => string.Equals(x.Section,section,StringComparison.OrdinalIgnoreCase)
				&& string.Equals(x.Key,key,StringComparison.OrdinalIgnoreCase));
		}

		private static List<CaptureSettingDef> Build() {
			List<CaptureSettingDef> listSettings=new List<CaptureSettingDef>();
			void Add(string section,string key,CaptureSettingKind kind,string defaultText,bool isGated,string help,params string[] arrayChoices) {
				listSettings.Add(new CaptureSettingDef(section,key,kind,defaultText,isGated,help,arrayChoices));
			}
			string session=GatedSettingsHash.SectionSession;
			string twain=LocalCaptureConfig.SectionTwain;
			Add(session,"Enabled",CaptureSettingKind.Switch,"0",true,"1: Mount/Acquire on this computer runs the capture session. 0: the legacy "
				+"Mount/Acquire loop runs (the rollback).");
			Add(session,"IdleDisableMinutes",CaptureSettingKind.WholeNumber,"10",true,"Minutes without an exposure after which the session disables the "
				+"source. Used only with DisableIsSafe=1. Longer than the source's own session timeout, shorter than the automatic logoff.");
			Add(twain,"ThreadModal",CaptureSettingKind.Switch,"1",true,"1: Open Dental's windows are disabled while a named source acquires on the legacy "
				+"path.");
			Add(twain,"AcquireTimeoutMinutes",CaptureSettingKind.WholeNumber,"0",true,"Legacy path: minutes after which an acquisition is stopped; 0 is "
				+"none. Only with PerSlot and the sources in DisableIsSafeSources.");
			Add(twain,"Allow16Bit",CaptureSettingKind.Switch,"1",true,"1 (the default): more than 8 bits (16 or 14, the device's Bits) are allowed on this "
				+"computer. 0 forces 8 bits for every device here. More than 8 bits also need the capture settings saved here and a passed Test capture "
				+"for the sensor's driver version and image settings.");
			Add(twain,"RestrictSources",CaptureSettingKind.Switch,"0",true,"1: only the sources in AllowedSources can be opened on this computer (the "
				+"source gate).");
			Add(twain,"AllowedSources",CaptureSettingKind.SourceList,"",true,"TWAIN ProductNames separated by |.");
			Add(twain,"JournalKeep",CaptureSettingKind.WholeNumber,"200",true,"How many recent captures the capture journal keeps; at least 1.");
			//The journal refuses 0 and keeps its default instead (CaptureJournal.Write): a 0 saved and logged here would not be in effect.
			listSettings[listSettings.Count-1].MinValue=1;
			listSettings[listSettings.Count-1].TextBelowMinimum="is not allowed: the journal cannot be turned off.";
			Add(twain,"DisableIsSafeSources",CaptureSettingKind.SourceList,"",true,"Legacy path: TWAIN ProductNames separated by |, whose disable "
				+"passed the safe-disable acceptance test.");
			Add(twain,"SafeDisableCap",CaptureSettingKind.Capability,"",true,"Legacy path, sources in DisableIsSafeSources only. "+HelpCapability);
			Add(twain,"RefusedDisableCodes",CaptureSettingKind.CodeList,"",true,"Legacy path: the \"disable refused\" codes. "+HelpCodes);
			Add(SectionDevice,"SessionMode",CaptureSettingKind.Choice,"PerSlot",true,"PerSlot: one enable per slot. Continuous: only when the source's "
				+"contract offers it and ContinuousCap is set.","PerSlot","Continuous");
			Add(SectionDevice,"Bits",CaptureSettingKind.Choice,"16",true,"16 (the default), 14 or 8: the depth asked for. 16 falls back to 14, and either "
				+"to 8, when the source does not list it; 14-bit data is stored in a 16-bit DICOM file. More than 8 bits also need Allow16Bit=1 and a "
				+"Test capture that passed at this depth; until then, or for a target that cannot be stored as DICOM, the session takes 8 bits. A source "
				+"that lists only 14 needs Bits=14 and a Test capture at 14; with Bits=16 the session then takes 8 bits (Test capture warns).",
				"16","14","8");
			Add(SectionDevice,"ByteOrder16",CaptureSettingKind.Choice,"LE",true,"Byte order of 16-bit samples (review flag).","LE","BE");
			Add(SectionDevice,"Polarity",CaptureSettingKind.Choice,"AsDelivered",true,"AsDelivered, or Invert for 16-bit samples once verified (review "
				+"flag).","AsDelivered","Invert");
			Add(SectionDevice,"Headless",CaptureSettingKind.Switch,"0",true,"1: ask the source to hide its window when it says it can (it is shown "
				+"anyway otherwise).");
			Add(SectionDevice,"DisableIsSafe",CaptureSettingKind.Switch,"0",true,"1 only after the safe-disable acceptance test passed. In effect only "
				+"when SafeDisableCap is set and the source accepts it.");
			Add(SectionDevice,"SessionTimeoutMinutes",CaptureSettingKind.WholeNumber,"0",true,"Minutes after which an enable is ended; 0 is none. Only "
				+"with safe disable in effect. Longer than the source's own session timeout.");
			Add(SectionDevice,"SafeDisableCap",CaptureSettingKind.Capability,"",true,"The safe-disable capability. "+HelpCapability);
			Add(SectionDevice,"RefusedDisableCodes",CaptureSettingKind.CodeList,"",true,"The \"disable refused\" codes. "+HelpCodes);
			Add(SectionDevice,"ContinuousCap",CaptureSettingKind.Capability,"",true,"The continuous-mode capability. "+HelpCapability);
			Add(SectionDevice,"RedeliverCap",CaptureSettingKind.Capability,"",true,"The redelivery capability. "+HelpCapability);
			Add(SectionDevice,"NoActivateCap",CaptureSettingKind.Capability,"",true,"The capability that keeps the source's window from taking "
				+"activation. "+HelpCapability);
			foreach(CaptureSessionKey key in Enum.GetValues(typeof(CaptureSessionKey))) {
				string help=key==CaptureSessionKey.Confirm
					? "Held about 1 s, starts or re-enables; empty means the on-screen button only. Never the same as a session key. "+HelpKey
					: "A session key, registered while the source is enabled. It never confirms. "+HelpKey;
				Add(CaptureSessionConfig.SectionKeys,key.ToString(),CaptureSettingKind.Key,"",false,help);
			}
			Add(CaptureSessionConfig.SectionSounds,CaptureSound.Saved.ToString(),CaptureSettingKind.Sound,CaptureSessionConfig.SoundSavedDefault,false,
				"An image was saved. "+HelpSound);
			Add(CaptureSessionConfig.SectionSounds,CaptureSound.NotReceived.ToString(),CaptureSettingKind.Sound,CaptureSessionConfig.SoundNotReceivedDefault,
				false,"The sensor software ended without an image, or a transfer failed. "+HelpSound);
			Add(CaptureSessionConfig.SectionSounds,CaptureSound.Disabled.ToString(),CaptureSettingKind.Sound,CaptureSessionConfig.SoundDisabledDefault,false,
				"The source was disabled. "+HelpSound);
			Add(CaptureSessionConfig.SectionSounds,CaptureSound.Error.ToString(),CaptureSettingKind.Sound,"",false,"Anything else that went wrong. "
				+"When the key is missing, the NotReceived sound plays. "+HelpSound);
			return listSettings;
		}
	}
}
