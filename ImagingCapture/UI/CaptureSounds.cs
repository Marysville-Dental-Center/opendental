// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Media;
using ImagingCapture.Session;

namespace ImagingCapture.UI {
	///<summary>A Windows system sound (System.Media.SystemSounds): what Windows plays for that event on this computer, as the user set it in the
	///Sound control panel.</summary>
	public enum CaptureSystemSound {
		///<summary>SystemAsterisk.</summary>
		Asterisk,
		///<summary>SystemHand ("Critical Stop").</summary>
		Hand,
		///<summary>SystemExclamation.</summary>
		Exclamation,
		///<summary>SystemQuestion.</summary>
		Question,
		///<summary>SystemDefault, or Beep.</summary>
		Beep,
	}

	///<summary>The [Sounds] names of capture.ini (design 9.3): Windows system sounds by name.</summary>
	public static class CaptureSoundNames {
		///<summary>The system sound name, ignoring case and spaces: SystemAsterisk, SystemHand, SystemExclamation, SystemQuestion, SystemDefault or
		///Beep. An empty name is valid and silent (true, with systemSound null). False for any other name.</summary>
		public static bool TryParse(string name,out CaptureSystemSound? systemSound) {
			systemSound=null;
			string text=(name??"").Replace(" ","").Trim().ToLowerInvariant();
			switch(text) {
				case "":
					return true;
				case "systemasterisk":
					systemSound=CaptureSystemSound.Asterisk;
					return true;
				case "systemhand":
					systemSound=CaptureSystemSound.Hand;
					return true;
				case "systemexclamation":
					systemSound=CaptureSystemSound.Exclamation;
					return true;
				case "systemquestion":
					systemSound=CaptureSystemSound.Question;
					return true;
				case "systemdefault":
				case "beep":
					systemSound=CaptureSystemSound.Beep;
					return true;
				default:
					return false;
			}
		}
	}

	///<summary>Plays the controller's sounds (CaptureSessionController.SoundRequested) per workstation (design 7.3, 9.3): the [Sounds] name of
	///Saved, NotReceived, Disabled or Error (CaptureSessionConfig.GetSoundName, with the design defaults) as a Windows system sound. An empty
	///name is silent. An unknown name is logged once and ignored. Playing never throws.</summary>
	public sealed class CaptureSoundPlayer {
		private readonly CaptureSessionConfig _config;
		private readonly Action<CaptureSystemSound> _actionPlay;
		private readonly Action<string> _actionLog;
		private readonly HashSet<CaptureSound> _hashSetUnknownLogged=new HashSet<CaptureSound>();

		///<summary>actionPlay: plays a system sound (null: PlaySystemSound). actionLog: a line for the session log.</summary>
		public CaptureSoundPlayer(CaptureSessionConfig config,Action<CaptureSystemSound> actionPlay,Action<string> actionLog) {
			_config=config??CaptureSessionConfig.Read(null);
			_actionPlay=actionPlay??PlaySystemSound;
			_actionLog=actionLog;
		}

		///<summary>The system sound of sound on this computer, or null when it is silent or its name is unknown (logged once).</summary>
		public CaptureSystemSound? Resolve(CaptureSound sound) {
			string name=_config.GetSoundName(sound);
			CaptureSystemSound? systemSound;
			if(CaptureSoundNames.TryParse(name,out systemSound)) {
				return systemSound;
			}
			if(_hashSetUnknownLogged.Add(sound)) {
				_actionLog?.Invoke("[Sounds] "+sound+"=\""+name+"\" is not a Windows system sound (SystemAsterisk, SystemHand, SystemExclamation, "
					+"SystemQuestion, SystemDefault or Beep); it is not played.");
			}
			return null;
		}

		public void Play(CaptureSound sound) {
			CaptureSystemSound? systemSound=Resolve(sound);
			if(systemSound==null) {
				return;
			}
			try {
				_actionPlay(systemSound.Value);
			}
			catch(Exception ex) {
				_actionLog?.Invoke("The "+sound+" sound could not be played: "+ex.Message);
			}
		}

		///<summary>System.Media.SystemSounds; plays without waiting.</summary>
		public static void PlaySystemSound(CaptureSystemSound systemSound) {
			switch(systemSound) {
				case CaptureSystemSound.Asterisk:
					SystemSounds.Asterisk.Play();
					return;
				case CaptureSystemSound.Hand:
					SystemSounds.Hand.Play();
					return;
				case CaptureSystemSound.Exclamation:
					SystemSounds.Exclamation.Play();
					return;
				case CaptureSystemSound.Question:
					SystemSounds.Question.Play();
					return;
				default:
					SystemSounds.Beep.Play();
					return;
			}
		}
	}
}
