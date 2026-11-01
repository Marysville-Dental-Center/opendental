// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Session;
using ImagingCapture.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>The [Keys] texts of capture.ini (design 9.3) as chords (CaptureKeyParser), the session's bindings (CaptureKeyBindings: invalid and
	///repeated entries left out, Confirm never shared with a session key), and the hotkeys while the source is enabled (CaptureHotkeys, on a
	///stand-in for RegisterHotKey).</summary>
	[TestClass]
	public class CaptureKeysTests {
		#region Parser
		[TestMethod]
		public void Parse_FunctionKey_NoModifiers() {
			CaptureKeyChord chord=Parse("F9");
			Assert.AreEqual(CaptureKeyModifiers.None,chord.Modifiers);
			Assert.AreEqual(0x78,chord.VirtualKey);//VK_F9
			Assert.AreEqual("F9",chord.Text);
		}

		[TestMethod]
		public void Parse_Modifiers_AnyOrderAndCase_CanonicalText() {
			CaptureKeyChord chord=Parse("Ctrl+F9");
			Assert.AreEqual(CaptureKeyModifiers.Control,chord.Modifiers);
			Assert.AreEqual(0x78,chord.VirtualKey);
			chord=Parse("Shift+Alt+F12");
			Assert.AreEqual(CaptureKeyModifiers.Shift|CaptureKeyModifiers.Alt,chord.Modifiers);
			Assert.AreEqual(0x7B,chord.VirtualKey);
			Assert.AreEqual("Shift+Alt+F12",chord.Text);
			chord=Parse(" alt + control + shift + win + f24 ");
			Assert.AreEqual(CaptureKeyModifiers.Alt|CaptureKeyModifiers.Control|CaptureKeyModifiers.Shift|CaptureKeyModifiers.Windows,chord.Modifiers);
			Assert.AreEqual(0x87,chord.VirtualKey);
			Assert.AreEqual("Ctrl+Shift+Alt+Win+F24",chord.Text,"Ctrl, Shift, Alt, Win, then the key.");
			Assert.AreEqual(Parse("Control+Shift+X"),Parse("shift+ctrl+x"),"Equal chords, whatever the order and the spelling.");
		}

		[TestMethod]
		public void Parse_NamedKeys() {
			Assert.AreEqual(0x65,Parse("NumPad5").VirtualKey);
			Assert.AreEqual(0x60,Parse("numpad0").VirtualKey);
			Assert.AreEqual(0x13,Parse("Pause").VirtualKey);
			Assert.AreEqual(0x91,Parse("ScrollLock").VirtualKey);
			Assert.AreEqual(0x91,Parse("Scroll").VirtualKey);
			Assert.AreEqual(0x22,Parse("PageDown").VirtualKey);
			Assert.AreEqual(0x22,Parse("Next").VirtualKey);
			Assert.AreEqual(0x20,Parse("Space").VirtualKey);
			Assert.AreEqual(0x0D,Parse("Enter").VirtualKey);
			Assert.AreEqual(0x41,Parse("A").VirtualKey);
			Assert.AreEqual(0x5A,Parse("z").VirtualKey);
			Assert.AreEqual(0x37,Parse("7").VirtualKey);
			Assert.AreEqual(0x37,Parse("D7").VirtualKey);
			Assert.AreEqual(0x6B,Parse("Add").VirtualKey);
			Assert.AreEqual(0xB3,Parse("MediaPlayPause").VirtualKey);
			Assert.AreEqual("NumPad5",Parse("Num Pad 5").Text,"Spaces inside a name are ignored.");
		}

		[TestMethod]
		public void Parse_HexVirtualKey_ForAPedalWithAnUnusualKey() {
			CaptureKeyChord chord=Parse("0x7C");
			Assert.AreEqual(0x7C,chord.VirtualKey);
			Assert.AreEqual("F13",chord.Text,"A code with a name shows its name.");
			Assert.AreEqual("0xE9",Parse("0xe9").Text,"A code without a name shows as hex.");
			Assert.AreEqual(CaptureKeyModifiers.Control,Parse("Ctrl+0x7C").Modifiers);
		}

		[TestMethod]
		public void Parse_Invalid_FalseWithReason() {
			AssertInvalid("","No key is set.");
			AssertInvalid("   ","No key is set.");
			AssertInvalid(null,"No key is set.");
			AssertInvalid("F25","not a key name");
			AssertInvalid("Ctrl","no key after its modifiers");
			AssertInvalid("Ctrl+Shift","no key after its modifiers");
			AssertInvalid("Ctrl+","empty part");
			AssertInvalid("+F9","empty part");
			AssertInvalid("F9+Ctrl","only the last part is the key");
			AssertInvalid("Hyper+F9","not a modifier");
			AssertInvalid("Pedal","not a key name");
			AssertInvalid("0x10","not a key name");//VK_SHIFT: a modifier alone is not a key
			AssertInvalid("0xA2","not a key name");//VK_LCONTROL
			AssertInvalid("0x5B","not a key name");//VK_LWIN
			AssertInvalid("0xFF","not a key name");
			AssertInvalid("0x00","not a key name");
			AssertInvalid("0x123","not a key name");
			AssertInvalid("0xZZ","not a key name");
		}

		private static CaptureKeyChord Parse(string text) {
			CaptureKeyChord chord;
			string error;
			Assert.IsTrue(CaptureKeyParser.TryParse(text,out chord,out error),"\""+text+"\": "+error);
			Assert.AreEqual("",error);
			return chord;
		}

		private static void AssertInvalid(string text,string errorPart) {
			CaptureKeyChord chord;
			string error;
			Assert.IsFalse(CaptureKeyParser.TryParse(text,out chord,out error),"\""+text+"\" must not parse.");
			Assert.IsNull(chord);
			StringAssert.Contains(error,errorPart,"\""+text+"\"");
		}
		#endregion Parser

		#region Bindings
		[TestMethod]
		public void Bindings_EmptyByDefault() {
			CaptureKeyBindings bindings=CaptureKeyBindings.FromConfig(CaptureSessionConfig.Read(null));
			Assert.AreEqual(0,bindings.GetSessionKeysBound().Count);
			Assert.IsNull(bindings.GetChord(CaptureSessionKey.Confirm),"Confirm is empty by default: the on-screen button only.");
			Assert.AreEqual(0,bindings.ListWarnings.Count);
			Assert.IsNull(bindings.Find(0x78,CaptureKeyModifiers.None));
		}

		[TestMethod]
		public void Bindings_FindByExactChord() {
			CaptureKeyBindings bindings=Bindings(CaptureSessionKey.Confirm,"F12",CaptureSessionKey.Retake,"F9",CaptureSessionKey.End,"Ctrl+F9");
			Assert.AreEqual(CaptureSessionKey.Retake,bindings.Find(0x78,CaptureKeyModifiers.None));
			Assert.AreEqual(CaptureSessionKey.End,bindings.Find(0x78,CaptureKeyModifiers.Control));
			Assert.IsNull(bindings.Find(0x78,CaptureKeyModifiers.Shift),"Other modifiers are another chord.");
			Assert.AreEqual(CaptureSessionKey.Confirm,bindings.Find(0x7B,CaptureKeyModifiers.None));
			Assert.IsTrue(bindings.IsConfirmKey(0x7B));
			Assert.IsFalse(bindings.IsConfirmKey(0x78));
			CollectionAssert.AreEqual(new[] { CaptureSessionKey.Retake,CaptureSessionKey.End },bindings.GetSessionKeysBound(),
				"Confirm is not a session key (never a hotkey).");
		}

		[TestMethod]
		public void Bindings_InvalidEntry_LeftOutWithWarning_OthersStillWork() {
			CaptureKeyBindings bindings=Bindings(CaptureSessionKey.Retake,"F99",CaptureSessionKey.Skip,"F10");
			Assert.IsNull(bindings.GetChord(CaptureSessionKey.Retake));
			Assert.AreEqual(CaptureSessionKey.Skip,bindings.Find(0x79,CaptureKeyModifiers.None));
			Assert.AreEqual(1,bindings.ListWarnings.Count);
			StringAssert.StartsWith(bindings.ListWarnings[0],"[Keys] Retake is not used: ");
		}

		[TestMethod]
		public void Bindings_RepeatedChord_TheLaterSessionKeyLeftOut() {
			CaptureKeyBindings bindings=Bindings(CaptureSessionKey.Skip,"F10",CaptureSessionKey.End,"f10");
			Assert.AreEqual(CaptureSessionKey.Skip,bindings.Find(0x79,CaptureKeyModifiers.None));
			Assert.IsNull(bindings.GetChord(CaptureSessionKey.End));
			Assert.AreEqual("[Keys] End is not used: F10 is already Skip.",bindings.ListWarnings.Single());
		}

		[TestMethod]
		public void Bindings_ConfirmSameAsASessionKey_ConfirmLeftOut_TheSessionKeyNeverConfirms() {
			CaptureKeyBindings bindings=Bindings(CaptureSessionKey.Confirm,"F9",CaptureSessionKey.Retake,"F9");
			Assert.IsNull(bindings.GetChord(CaptureSessionKey.Confirm));
			Assert.AreEqual(CaptureSessionKey.Retake,bindings.Find(0x78,CaptureKeyModifiers.None));
			Assert.IsFalse(bindings.IsConfirmKey(0x78));
			StringAssert.StartsWith(bindings.ListWarnings.Single(),"[Keys] Confirm is not used: F9 is also Retake");
		}

		[TestMethod]
		public void Bindings_FromConfig_ReadsKeysSection() {
			CaptureSessionConfig config=CaptureSessionTestConfig.Read("[Keys]","Confirm=Pause","NextSlot=NumPad6","PreviousSlot=NumPad4","Disable=");
			CaptureKeyBindings bindings=CaptureKeyBindings.FromConfig(config);
			Assert.AreEqual(0x13,bindings.GetChord(CaptureSessionKey.Confirm).VirtualKey);
			Assert.AreEqual(0x66,bindings.GetChord(CaptureSessionKey.NextSlot).VirtualKey);
			Assert.AreEqual(0x64,bindings.GetChord(CaptureSessionKey.PreviousSlot).VirtualKey);
			Assert.IsNull(bindings.GetChord(CaptureSessionKey.Disable),"Empty: not bound.");
			Assert.AreEqual(0,bindings.ListWarnings.Count);
		}

		private static CaptureKeyBindings Bindings(params object[] arrayPairs) {
			Dictionary<CaptureSessionKey,string> dictTexts=new Dictionary<CaptureSessionKey,string>();
			for(int i=0;i<arrayPairs.Length;i+=2) {
				dictTexts[(CaptureSessionKey)arrayPairs[i]]=(string)arrayPairs[i+1];
			}
			return CaptureKeyBindings.FromTexts(dictTexts);
		}
		#endregion Bindings

		#region Hotkeys
		[TestMethod]
		public void Hotkeys_RegisteredOnlyWhileActive_ReleasedOnInactive() {
			FakeHotkeyRegistrar registrar=new FakeHotkeyRegistrar();
			List<string> listLog=new List<string>();
			CaptureHotkeys hotkeys=new CaptureHotkeys(Bindings(CaptureSessionKey.Confirm,"F12",CaptureSessionKey.Retake,"F9",
				CaptureSessionKey.Skip,"Ctrl+F10"),listLog.Add);
			hotkeys.SetRegistrar(registrar);
			Assert.AreEqual(0,registrar.DictRegistered.Count,"Nothing until the source is enabled.");
			hotkeys.SetActive(true);
			Assert.AreEqual(2,registrar.DictRegistered.Count);
			Assert.IsFalse(registrar.DictRegistered.Values.Any(x => x.VirtualKey==0x7B),"Confirm is never a hotkey.");
			CollectionAssert.AreEqual(new[] { CaptureSessionKey.Retake,CaptureSessionKey.Skip },hotkeys.GetKeysRegistered());
			int idRetake=registrar.DictRegistered.Single(x => x.Value.VirtualKey==0x78).Key;
			int idSkip=registrar.DictRegistered.Single(x => x.Value.VirtualKey==0x79).Key;
			Assert.AreEqual(CaptureKeyModifiers.Control,registrar.DictRegistered[idSkip].Modifiers);
			Assert.AreEqual(CaptureSessionKey.Retake,hotkeys.GetKey(idRetake));
			Assert.AreEqual(CaptureSessionKey.Skip,hotkeys.GetKey(idSkip));
			Assert.IsNull(hotkeys.GetKey(idSkip+100));
			hotkeys.SetActive(true);
			Assert.AreEqual(2,registrar.CountRegisterCalls,"Active again: nothing registered twice.");
			hotkeys.SetActive(false);
			Assert.AreEqual(0,registrar.DictRegistered.Count,"Released when the source is no longer enabled.");
			Assert.IsNull(hotkeys.GetKey(idRetake),"A late WM_HOTKEY of a released id does nothing.");
			hotkeys.SetActive(true);
			Assert.AreEqual(2,registrar.DictRegistered.Count,"Registered again for the next enable.");
			hotkeys.ReleaseAll();
			hotkeys.ReleaseAll();
			Assert.AreEqual(0,registrar.DictRegistered.Count);
			Assert.AreEqual(4,registrar.CountUnregisterCalls,"Each registration released once.");
			Assert.AreEqual(0,listLog.Count);
		}

		[TestMethod]
		public void Hotkeys_RefusedRegistration_LoggedOnceAndSkipped_OthersRegistered() {
			FakeHotkeyRegistrar registrar=new FakeHotkeyRegistrar();
			registrar.HashSetRefused.Add(0x78);
			List<string> listLog=new List<string>();
			CaptureHotkeys hotkeys=new CaptureHotkeys(Bindings(CaptureSessionKey.Retake,"F9",CaptureSessionKey.End,"F11"),listLog.Add);
			hotkeys.SetRegistrar(registrar);
			hotkeys.SetActive(true);
			CollectionAssert.AreEqual(new[] { CaptureSessionKey.End },hotkeys.GetKeysRegistered());
			Assert.AreEqual(1,listLog.Count);
			StringAssert.StartsWith(listLog[0],"[Keys] Retake (F9) could not be registered as a hotkey: ");
			StringAssert.Contains(listLog[0],"(error 1409)");
			hotkeys.SetActive(false);
			hotkeys.SetActive(true);
			Assert.AreEqual(1,listLog.Count,"Logged once per session, not at every enable.");
		}

		[TestMethod]
		public void Hotkeys_RegistrarThrows_LoggedAndSkipped() {
			FakeHotkeyRegistrar registrar=new FakeHotkeyRegistrar { IsThrowing=true };
			List<string> listLog=new List<string>();
			CaptureHotkeys hotkeys=new CaptureHotkeys(Bindings(CaptureSessionKey.Retake,"F9"),listLog.Add);
			hotkeys.SetRegistrar(registrar);
			hotkeys.SetActive(true);
			Assert.AreEqual(0,hotkeys.GetKeysRegistered().Count);
			StringAssert.Contains(listLog.Single(),"RegisterHotKey failed");
		}

		[TestMethod]
		public void Hotkeys_ActiveBeforeTheHandle_RegisteredWhenItComes_MovedToANewHandle() {
			CaptureHotkeys hotkeys=new CaptureHotkeys(Bindings(CaptureSessionKey.Rotate,"F8"),null);
			hotkeys.SetActive(true);
			Assert.AreEqual(0,hotkeys.GetKeysRegistered().Count,"No window handle yet.");
			FakeHotkeyRegistrar registrar1=new FakeHotkeyRegistrar();
			hotkeys.SetRegistrar(registrar1);
			Assert.AreEqual(1,registrar1.DictRegistered.Count);
			FakeHotkeyRegistrar registrar2=new FakeHotkeyRegistrar();
			hotkeys.SetRegistrar(registrar2);
			Assert.AreEqual(0,registrar1.DictRegistered.Count,"Released on the old handle.");
			Assert.AreEqual(1,registrar2.DictRegistered.Count);
			hotkeys.SetRegistrar(null);
			Assert.AreEqual(0,registrar2.DictRegistered.Count,"The handle is gone: released.");
			Assert.AreEqual(0,hotkeys.GetKeysRegistered().Count);
		}
		#endregion Hotkeys
	}

	///<summary>RegisterHotKey for the tests: records what is registered; HashSetRefused holds virtual keys Windows refuses (error 1409,
	///ERROR_HOTKEY_ALREADY_REGISTERED).</summary>
	internal sealed class FakeHotkeyRegistrar:ICaptureHotkeyRegistrar {
		public readonly Dictionary<int,CaptureKeyChord> DictRegistered=new Dictionary<int,CaptureKeyChord>();
		public readonly HashSet<int> HashSetRefused=new HashSet<int>();
		public bool IsThrowing;
		public int CountRegisterCalls;
		public int CountUnregisterCalls;

		public bool Register(int id,CaptureKeyChord chord,out int errorCode) {
			CountRegisterCalls++;
			if(IsThrowing) {
				throw new InvalidOperationException("RegisterHotKey failed");
			}
			if(HashSetRefused.Contains(chord.VirtualKey)) {
				errorCode=1409;
				return false;
			}
			if(DictRegistered.ContainsKey(id)) {
				throw new InvalidOperationException("Hotkey id "+id+" registered twice.");
			}
			errorCode=0;
			DictRegistered[id]=chord;
			return true;
		}

		public void Unregister(int id) {
			CountUnregisterCalls++;
			if(!DictRegistered.Remove(id)) {
				throw new InvalidOperationException("Hotkey id "+id+" is not registered.");
			}
		}
	}
}
