// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ImagingCapture.Session;

namespace ImagingCapture.UI {
	///<summary>The modifier keys of a key chord. The values are RegisterHotKey's fsModifiers (MOD_ALT 1, MOD_CONTROL 2, MOD_SHIFT 4, MOD_WIN 8),
	///which are also the values of WPF's ModifierKeys, so the window converts with a cast.</summary>
	[Flags]
	public enum CaptureKeyModifiers {
		None=0,
		Alt=1,
		Control=2,
		Shift=4,
		Windows=8,
	}

	///<summary>One key with its modifiers, as a [Keys] value names it (design 9.3): "F9", "Ctrl+F9", "Shift+Alt+F12", "NumPad5", "Pause". The key
	///is a Windows virtual-key code. Two chords are equal when the modifiers and the key are.</summary>
	public sealed class CaptureKeyChord:IEquatable<CaptureKeyChord> {
		public CaptureKeyChord(CaptureKeyModifiers modifiers,int virtualKey) {
			Modifiers=modifiers;
			VirtualKey=virtualKey;
		}

		public CaptureKeyModifiers Modifiers { get; }
		///<summary>The Windows virtual-key code, 0x01 to 0xFE.</summary>
		public int VirtualKey { get; }

		///<summary>"Ctrl+Shift+F9": the modifiers in the order Ctrl, Shift, Alt, Win, then the key's name.</summary>
		public string Text {
			get {
				string text="";
				if(Modifiers.HasFlag(CaptureKeyModifiers.Control)) {
					text+="Ctrl+";
				}
				if(Modifiers.HasFlag(CaptureKeyModifiers.Shift)) {
					text+="Shift+";
				}
				if(Modifiers.HasFlag(CaptureKeyModifiers.Alt)) {
					text+="Alt+";
				}
				if(Modifiers.HasFlag(CaptureKeyModifiers.Windows)) {
					text+="Win+";
				}
				return text+CaptureKeyParser.GetKeyName(VirtualKey);
			}
		}

		public bool Equals(CaptureKeyChord other) {
			return other!=null && other.Modifiers==Modifiers && other.VirtualKey==VirtualKey;
		}

		public override bool Equals(object obj) {
			return Equals(obj as CaptureKeyChord);
		}

		public override int GetHashCode() {
			return ((int)Modifiers<<16)^VirtualKey;
		}

		public override string ToString() {
			return Text;
		}
	}

	///<summary>Reads a [Keys] value (design 9.3) into a CaptureKeyChord: modifiers (Ctrl or Control, Shift, Alt, Win or Windows), each followed by
	///'+', then one key. Keys: F1-F24, A-Z, 0-9 (or D0-D9), NumPad0-NumPad9, Multiply, Add, Separator, Subtract, Decimal, Divide, Pause,
	///ScrollLock, NumLock, CapsLock, Insert, Delete, Home, End, PageUp, PageDown, Left, Up, Right, Down, Space, Enter, Tab, Escape, Backspace,
	///PrintScreen, Apps, the browser, volume and media keys, or a virtual-key code in hex ("0x7A") for a pedal that sends an unusual key. Names
	///ignore case and spaces. A modifier alone is not a key. No vendor or device names: these are Windows key names.</summary>
	public static class CaptureKeyParser {
		private static readonly Dictionary<string,int> _dictNames=BuildNames();
		private static readonly Dictionary<int,string> _dictNamesByKey=BuildNamesByKey();

		///<summary>The chord in text, or false with error. An empty text is not a chord (error says the key is not set).</summary>
		public static bool TryParse(string text,out CaptureKeyChord chord,out string error) {
			chord=null;
			error="";
			string textTrimmed=(text??"").Trim();
			if(textTrimmed=="") {
				error="No key is set.";
				return false;
			}
			string[] arrayParts=textTrimmed.Split('+');
			CaptureKeyModifiers modifiers=CaptureKeyModifiers.None;
			for(int i=0;i<arrayParts.Length;i++) {
				string part=arrayParts[i].Replace(" ","").Replace("\t","");
				if(part=="") {
					error="\""+textTrimmed+"\" has an empty part: write a key such as F9 or Ctrl+F9.";
					return false;
				}
				CaptureKeyModifiers modifier=GetModifier(part);
				bool isLast=i==arrayParts.Length-1;
				if(!isLast) {
					if(modifier==CaptureKeyModifiers.None) {
						error="\""+part+"\" in \""+textTrimmed+"\" is not a modifier (Ctrl, Shift, Alt or Win); only the last part is the key.";
						return false;
					}
					modifiers|=modifier;
					continue;
				}
				if(modifier!=CaptureKeyModifiers.None) {
					error="\""+textTrimmed+"\" has no key after its modifiers.";
					return false;
				}
				int virtualKey;
				if(!TryGetVirtualKey(part,out virtualKey)) {
					error="\""+part+"\" is not a key name this program knows. Use a name such as F9, NumPad5 or Pause, or a virtual-key code such as 0x7A.";
					return false;
				}
				chord=new CaptureKeyChord(modifiers,virtualKey);
			}
			return chord!=null;
		}

		///<summary>The name TryParse reads for a virtual-key code: "F9", "NumPad5", "Pause", or "0x7A" for a code without a name.</summary>
		public static string GetKeyName(int virtualKey) {
			string name;
			if(_dictNamesByKey.TryGetValue(virtualKey,out name)) {
				return name;
			}
			return "0x"+virtualKey.ToString("X2",CultureInfo.InvariantCulture);
		}

		///<summary>A virtual-key code that is a modifier key itself (Shift, Ctrl, Alt, the Windows keys), which is never bound alone.</summary>
		public static bool IsModifierKey(int virtualKey) {
			return (virtualKey>=0x10 && virtualKey<=0x12)//VK_SHIFT, VK_CONTROL, VK_MENU
				|| virtualKey==0x5B || virtualKey==0x5C//VK_LWIN, VK_RWIN
				|| (virtualKey>=0xA0 && virtualKey<=0xA5);//VK_LSHIFT to VK_RMENU
		}

		private static CaptureKeyModifiers GetModifier(string part) {
			switch(part.ToLowerInvariant()) {
				case "ctrl":
				case "control":
					return CaptureKeyModifiers.Control;
				case "shift":
					return CaptureKeyModifiers.Shift;
				case "alt":
					return CaptureKeyModifiers.Alt;
				case "win":
				case "windows":
					return CaptureKeyModifiers.Windows;
				default:
					return CaptureKeyModifiers.None;
			}
		}

		private static bool TryGetVirtualKey(string part,out int virtualKey) {
			virtualKey=0;
			string lower=part.ToLowerInvariant();
			if(lower.StartsWith("0x",StringComparison.Ordinal)) {
				int code;
				if(lower.Length<3 || lower.Length>4
					|| !int.TryParse(lower.Substring(2),NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out code))
				{
					return false;
				}
				if(code<0x01 || code>0xFE || IsModifierKey(code)) {
					return false;
				}
				virtualKey=code;
				return true;
			}
			return _dictNames.TryGetValue(lower,out virtualKey);
		}

		///<summary>Every accepted name, lower case, to its virtual-key code (the Windows VK_ constants).</summary>
		private static Dictionary<string,int> BuildNames() {
			Dictionary<string,int> dictNames=new Dictionary<string,int>();
			for(int n=1;n<=24;n++) {
				dictNames["f"+n.ToString(CultureInfo.InvariantCulture)]=0x6F+n;//VK_F1 0x70 to VK_F24 0x87
			}
			for(char c='a';c<='z';c++) {
				dictNames[c.ToString()]=0x41+(c-'a');
			}
			for(int n=0;n<=9;n++) {
				string digit=n.ToString(CultureInfo.InvariantCulture);
				dictNames[digit]=0x30+n;
				dictNames["d"+digit]=0x30+n;
				dictNames["numpad"+digit]=0x60+n;
			}
			dictNames["multiply"]=0x6A;
			dictNames["add"]=0x6B;
			dictNames["separator"]=0x6C;
			dictNames["subtract"]=0x6D;
			dictNames["decimal"]=0x6E;
			dictNames["divide"]=0x6F;
			dictNames["pause"]=0x13;
			dictNames["scrolllock"]=0x91;
			dictNames["scroll"]=0x91;
			dictNames["numlock"]=0x90;
			dictNames["capslock"]=0x14;
			dictNames["capital"]=0x14;
			dictNames["insert"]=0x2D;
			dictNames["delete"]=0x2E;
			dictNames["del"]=0x2E;
			dictNames["home"]=0x24;
			dictNames["end"]=0x23;
			dictNames["pageup"]=0x21;
			dictNames["prior"]=0x21;
			dictNames["pagedown"]=0x22;
			dictNames["next"]=0x22;
			dictNames["left"]=0x25;
			dictNames["up"]=0x26;
			dictNames["right"]=0x27;
			dictNames["down"]=0x28;
			dictNames["space"]=0x20;
			dictNames["enter"]=0x0D;
			dictNames["return"]=0x0D;
			dictNames["tab"]=0x09;
			dictNames["escape"]=0x1B;
			dictNames["esc"]=0x1B;
			dictNames["backspace"]=0x08;
			dictNames["back"]=0x08;
			dictNames["printscreen"]=0x2C;
			dictNames["snapshot"]=0x2C;
			dictNames["apps"]=0x5D;
			dictNames["browserback"]=0xA6;
			dictNames["browserforward"]=0xA7;
			dictNames["browserrefresh"]=0xA8;
			dictNames["browserhome"]=0xAC;
			dictNames["volumemute"]=0xAD;
			dictNames["volumedown"]=0xAE;
			dictNames["volumeup"]=0xAF;
			dictNames["medianexttrack"]=0xB0;
			dictNames["mediaprevioustrack"]=0xB1;
			dictNames["mediastop"]=0xB2;
			dictNames["mediaplaypause"]=0xB3;
			return dictNames;
		}

		///<summary>The name GetKeyName gives each code: the first spelling of BuildNames, in the case the design and Windows use.</summary>
		private static Dictionary<int,string> BuildNamesByKey() {
			Dictionary<int,string> dictNamesByKey=new Dictionary<int,string>();
			for(int n=1;n<=24;n++) {
				dictNamesByKey[0x6F+n]="F"+n.ToString(CultureInfo.InvariantCulture);
			}
			for(char c='A';c<='Z';c++) {
				dictNamesByKey[0x41+(c-'A')]=c.ToString();
			}
			for(int n=0;n<=9;n++) {
				dictNamesByKey[0x30+n]=n.ToString(CultureInfo.InvariantCulture);
				dictNamesByKey[0x60+n]="NumPad"+n.ToString(CultureInfo.InvariantCulture);
			}
			string[][] arrayPairs={
				new[] { "6A","Multiply" },new[] { "6B","Add" },new[] { "6C","Separator" },new[] { "6D","Subtract" },new[] { "6E","Decimal" },
				new[] { "6F","Divide" },new[] { "13","Pause" },new[] { "91","ScrollLock" },new[] { "90","NumLock" },new[] { "14","CapsLock" },
				new[] { "2D","Insert" },new[] { "2E","Delete" },new[] { "24","Home" },new[] { "23","End" },new[] { "21","PageUp" },
				new[] { "22","PageDown" },new[] { "25","Left" },new[] { "26","Up" },new[] { "27","Right" },new[] { "28","Down" },
				new[] { "20","Space" },new[] { "0D","Enter" },new[] { "09","Tab" },new[] { "1B","Escape" },new[] { "08","Backspace" },
				new[] { "2C","PrintScreen" },new[] { "5D","Apps" },new[] { "A6","BrowserBack" },new[] { "A7","BrowserForward" },
				new[] { "A8","BrowserRefresh" },new[] { "AC","BrowserHome" },new[] { "AD","VolumeMute" },new[] { "AE","VolumeDown" },
				new[] { "AF","VolumeUp" },new[] { "B0","MediaNextTrack" },new[] { "B1","MediaPreviousTrack" },new[] { "B2","MediaStop" },
				new[] { "B3","MediaPlayPause" },
			};
			foreach(string[] pair in arrayPairs) {
				dictNamesByKey[int.Parse(pair[0],NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture)]=pair[1];
			}
			return dictNamesByKey;
		}
	}

	///<summary>The [Keys] of capture.ini (design 9.3) as chords: which key or pedal runs which CaptureSessionKey. Built once per session from
	///CaptureSessionConfig. An entry that does not parse, or that repeats an earlier entry's chord, is left out with a warning. The Confirm key is
	///left out when a session key has the same chord, because a session key never confirms (design 7.3 step 2). Confirm is matched only in the
	///window (it is never a hotkey); the session keys are also registered as hotkeys while the source is enabled (CaptureHotkeys).</summary>
	public sealed class CaptureKeyBindings {
		private readonly Dictionary<CaptureSessionKey,CaptureKeyChord> _dictChords=new Dictionary<CaptureSessionKey,CaptureKeyChord>();
		private readonly List<string> _listWarnings=new List<string>();

		private CaptureKeyBindings() {
		}

		///<summary>Why entries were left out, one sentence each, for the session log.</summary>
		public IReadOnlyList<string> ListWarnings => _listWarnings;

		///<summary>The session keys that have a chord (not Confirm), in the order of CaptureSessionKey: what CaptureHotkeys registers.</summary>
		public List<CaptureSessionKey> GetSessionKeysBound() {
			return _dictChords.Keys.Where(x => x!=CaptureSessionKey.Confirm).OrderBy(x => (int)x).ToList();
		}

		///<summary>The chord of key, or null when it has none.</summary>
		public CaptureKeyChord GetChord(CaptureSessionKey key) {
			CaptureKeyChord chord;
			return _dictChords.TryGetValue(key,out chord) ? chord : null;
		}

		///<summary>The key bound to exactly this chord, or null.</summary>
		public CaptureSessionKey? Find(int virtualKey,CaptureKeyModifiers modifiers) {
			CaptureKeyChord chord=new CaptureKeyChord(modifiers,virtualKey);
			foreach(KeyValuePair<CaptureSessionKey,CaptureKeyChord> pair in _dictChords) {
				if(pair.Value.Equals(chord)) {
					return pair.Key;
				}
			}
			return null;
		}

		///<summary>The Confirm key's own key went up (whatever the modifiers are by then: they may be released first).</summary>
		public bool IsConfirmKey(int virtualKey) {
			CaptureKeyChord chord=GetChord(CaptureSessionKey.Confirm);
			return chord!=null && chord.VirtualKey==virtualKey;
		}

		///<summary>The bindings of config's [Keys]; null config gives none.</summary>
		public static CaptureKeyBindings FromConfig(CaptureSessionConfig config) {
			Dictionary<CaptureSessionKey,string> dictTexts=new Dictionary<CaptureSessionKey,string>();
			foreach(CaptureSessionKey key in Enum.GetValues(typeof(CaptureSessionKey))) {
				dictTexts[key]=config==null ? "" : config.GetKeyText(key);
			}
			return FromTexts(dictTexts);
		}

		///<summary>The bindings of the [Keys] texts, by key; a missing or empty text leaves the key unbound.</summary>
		public static CaptureKeyBindings FromTexts(IDictionary<CaptureSessionKey,string> dictTexts) {
			CaptureKeyBindings bindings=new CaptureKeyBindings();
			Dictionary<CaptureSessionKey,CaptureKeyChord> dictParsed=new Dictionary<CaptureSessionKey,CaptureKeyChord>();
			foreach(CaptureSessionKey key in Enum.GetValues(typeof(CaptureSessionKey))) {
				string text;
				if(dictTexts==null || !dictTexts.TryGetValue(key,out text) || string.IsNullOrWhiteSpace(text)) {
					continue;
				}
				CaptureKeyChord chord;
				string error;
				if(!CaptureKeyParser.TryParse(text,out chord,out error)) {
					bindings._listWarnings.Add("[Keys] "+key+" is not used: "+error);
					continue;
				}
				dictParsed[key]=chord;
			}
			//The session keys first, in order: an entry that repeats an earlier one's chord is left out.
			foreach(CaptureSessionKey key in dictParsed.Keys.Where(x => x!=CaptureSessionKey.Confirm).OrderBy(x => (int)x).ToList()) {
				CaptureKeyChord chord=dictParsed[key];
				KeyValuePair<CaptureSessionKey,CaptureKeyChord> pairEarlier=bindings._dictChords.FirstOrDefault(x => x.Value.Equals(chord));
				if(pairEarlier.Value!=null) {
					bindings._listWarnings.Add("[Keys] "+key+" is not used: "+chord.Text+" is already "+pairEarlier.Key+".");
					continue;
				}
				bindings._dictChords[key]=chord;
			}
			CaptureKeyChord chordConfirm;
			if(dictParsed.TryGetValue(CaptureSessionKey.Confirm,out chordConfirm)) {
				KeyValuePair<CaptureSessionKey,CaptureKeyChord> pairSame=bindings._dictChords.FirstOrDefault(x => x.Value.Equals(chordConfirm));
				if(pairSame.Value!=null) {
					//Design 7.3 step 2: the session keys and pedals never confirm, so the shared chord stays the session key's.
					bindings._listWarnings.Add("[Keys] Confirm is not used: "+chordConfirm.Text+" is also "+pairSame.Key+", and a session key never "
						+"confirms. Start with the on-screen button.");
				}
				else {
					bindings._dictChords[CaptureSessionKey.Confirm]=chordConfirm;
				}
			}
			return bindings;
		}
	}
}
