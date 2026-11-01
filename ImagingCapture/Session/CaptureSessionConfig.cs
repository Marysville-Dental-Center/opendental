// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using ImagingCapture.Settings;
using ImagingDeviceManager.Config;

namespace ImagingCapture.Session {
	///<summary>A key of capture.ini [Keys] (design 9.3): what a key or pedal does in the capture session. Confirm is the separate key that starts
	///or re-enables when held about 1 s; the others are session keys, which never confirm (design 7.3 step 2).</summary>
	public enum CaptureSessionKey {
		Confirm,
		Retake,
		Skip,
		NextSlot,
		PreviousSlot,
		Rotate,
		Disable,
		End,
	}

	///<summary>A sound of capture.ini [Sounds] (design 7.3, 9.3): which one the controller asks the window to play.</summary>
	public enum CaptureSound {
		///<summary>An image was saved.</summary>
		Saved,
		///<summary>The sensor software ended without an image, or a transfer failed.</summary>
		NotReceived,
		///<summary>The source was disabled (the operator's Disable, or the sensor software closed).</summary>
		Disabled,
		///<summary>Anything else that went wrong: a failed save, open or enable.</summary>
		Error,
	}

	///<summary>The capture session's own sections of capture.ini (design 9.3): [Session] Enabled and IdleDisableMinutes (gated), and the hardware
	///bindings [Keys] and [Sounds] (not gated). Read once when the session starts. Missing or invalid values have the design defaults. The
	///window parses the key texts (RegisterHotKey) and plays the sounds by name; this class only reads the file.</summary>
	public sealed class CaptureSessionConfig {
		public const string SectionKeys="Keys";
		public const string SectionSounds="Sounds";
		///<summary>[Session] IdleDisableMinutes default: 10 (design 9.3; used only with DisableIsSafe).</summary>
		public const int IdleDisableMinutesDefault=10;
		///<summary>The Windows system sounds design 9.3 names as defaults.</summary>
		public const string SoundSavedDefault="SystemAsterisk";
		public const string SoundNotReceivedDefault="SystemHand";
		public const string SoundDisabledDefault="SystemExclamation";

		private readonly Dictionary<CaptureSessionKey,string> _dictKeys=new Dictionary<CaptureSessionKey,string>();
		private readonly Dictionary<CaptureSound,string> _dictSounds=new Dictionary<CaptureSound,string>();

		///<summary>[Session] Enabled, default 0 (gated): 0 means the legacy Mount/Acquire loop runs, the rollback (design 7.3).</summary>
		public bool IsEnabled { get; private set; }
		///<summary>[Session] IdleDisableMinutes, default 10 (gated). 0 or less turns idle disable off.</summary>
		public int IdleDisableMinutes { get; private set; }=IdleDisableMinutesDefault;

		///<summary>The [Keys] value for key as written ("" when not set: the key is not bound; Confirm empty means the on-screen button only).</summary>
		public string GetKeyText(CaptureSessionKey key) {
			string text;
			return _dictKeys.TryGetValue(key,out text) ? text : "";
		}

		///<summary>The [Sounds] name for sound, or its design default. "" turns the sound off. Error has no key of its own in design 9.3; [Sounds]
		///Error is read when present, otherwise it plays the NotReceived sound ("not-received or error", design 7.3).</summary>
		public string GetSoundName(CaptureSound sound) {
			string name;
			return _dictSounds.TryGetValue(sound,out name) ? name : "";
		}

		///<summary>The values of config; null gives the defaults. Never throws.</summary>
		public static CaptureSessionConfig Read(LocalCaptureConfig config) {
			CaptureSessionConfig sessionConfig=new CaptureSessionConfig();
			sessionConfig._dictSounds[CaptureSound.Saved]=SoundSavedDefault;
			sessionConfig._dictSounds[CaptureSound.NotReceived]=SoundNotReceivedDefault;
			sessionConfig._dictSounds[CaptureSound.Disabled]=SoundDisabledDefault;
			sessionConfig._dictSounds[CaptureSound.Error]=SoundNotReceivedDefault;
			if(config==null) {
				return sessionConfig;
			}
			try {
				sessionConfig.IsEnabled=config.GetBool(GatedSettingsHash.SectionSession,"Enabled",false);
				int idleDisableMinutes=config.GetInt(GatedSettingsHash.SectionSession,"IdleDisableMinutes",IdleDisableMinutesDefault);
				sessionConfig.IdleDisableMinutes=Math.Max(0,idleDisableMinutes);
				foreach(CaptureSessionKey key in Enum.GetValues(typeof(CaptureSessionKey))) {
					sessionConfig._dictKeys[key]=config.GetString(SectionKeys,key.ToString(),"").Trim();
				}
				foreach(CaptureSound sound in Enum.GetValues(typeof(CaptureSound))) {
					if(config.ContainsKey(SectionSounds,sound.ToString())) {
						sessionConfig._dictSounds[sound]=config.GetString(SectionSounds,sound.ToString(),"").Trim();
					}
				}
				if(!config.ContainsKey(SectionSounds,CaptureSound.Error.ToString())) {
					sessionConfig._dictSounds[CaptureSound.Error]=sessionConfig._dictSounds[CaptureSound.NotReceived];
				}
			}
			catch {
				//Design 9.3: capture works with defaults when the file is unreadable.
			}
			return sessionConfig;
		}
	}
}
