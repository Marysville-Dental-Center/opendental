// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImagingCapture.Session;
using ImagingCapture.UI;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>The [Sounds] names of capture.ini (design 7.3, 9.3) as Windows system sounds (CaptureSoundNames), and the player per workstation
	///(CaptureSoundPlayer): the design defaults, empty is silent, unknown names logged once and ignored.</summary>
	[TestClass]
	public class CaptureSoundsTests {
		[TestMethod]
		public void Names_SystemSounds_IgnoringCaseAndSpaces() {
			AssertName("SystemAsterisk",CaptureSystemSound.Asterisk);
			AssertName("SystemHand",CaptureSystemSound.Hand);
			AssertName("SystemExclamation",CaptureSystemSound.Exclamation);
			AssertName("SystemQuestion",CaptureSystemSound.Question);
			AssertName("SystemDefault",CaptureSystemSound.Beep);
			AssertName("Beep",CaptureSystemSound.Beep);
			AssertName("systemasterisk",CaptureSystemSound.Asterisk);
			AssertName(" System Hand ",CaptureSystemSound.Hand);
		}

		[TestMethod]
		public void Names_Empty_IsValidAndSilent() {
			CaptureSystemSound? systemSound;
			Assert.IsTrue(CaptureSoundNames.TryParse("",out systemSound));
			Assert.IsNull(systemSound);
			Assert.IsTrue(CaptureSoundNames.TryParse(null,out systemSound));
			Assert.IsNull(systemSound);
			Assert.IsTrue(CaptureSoundNames.TryParse("  ",out systemSound));
			Assert.IsNull(systemSound);
		}

		[TestMethod]
		public void Names_Unknown_False() {
			CaptureSystemSound? systemSound;
			Assert.IsFalse(CaptureSoundNames.TryParse("Ding",out systemSound));
			Assert.IsNull(systemSound);
			Assert.IsFalse(CaptureSoundNames.TryParse("C:\\Windows\\Media\\chimes.wav",out systemSound),"Only system sounds by name.");
			Assert.IsFalse(CaptureSoundNames.TryParse("Asterisk",out systemSound),"The design's names start with System.");
		}

		[TestMethod]
		public void Player_Defaults_SavedAsterisk_NotReceivedHand_DisabledExclamation_ErrorAsNotReceived() {
			List<CaptureSystemSound> listPlayed=new List<CaptureSystemSound>();
			List<string> listLog=new List<string>();
			CaptureSoundPlayer player=new CaptureSoundPlayer(CaptureSessionConfig.Read(null),listPlayed.Add,listLog.Add);
			player.Play(CaptureSound.Saved);
			player.Play(CaptureSound.NotReceived);
			player.Play(CaptureSound.Disabled);
			player.Play(CaptureSound.Error);
			CollectionAssert.AreEqual(new[] { CaptureSystemSound.Asterisk,CaptureSystemSound.Hand,CaptureSystemSound.Exclamation,CaptureSystemSound.Hand },
				listPlayed);
			Assert.AreEqual(0,listLog.Count);
		}

		[TestMethod]
		public void Player_PerWorkstation_EmptySilent_UnknownLoggedOnceAndIgnored() {
			CaptureSessionConfig config=CaptureSessionTestConfig.Read("[Sounds]","Saved=SystemQuestion","NotReceived=","Disabled=Gong","Error=Beep");
			List<CaptureSystemSound> listPlayed=new List<CaptureSystemSound>();
			List<string> listLog=new List<string>();
			CaptureSoundPlayer player=new CaptureSoundPlayer(config,listPlayed.Add,listLog.Add);
			player.Play(CaptureSound.Saved);
			player.Play(CaptureSound.NotReceived);
			player.Play(CaptureSound.Disabled);
			player.Play(CaptureSound.Disabled);
			player.Play(CaptureSound.Error);
			CollectionAssert.AreEqual(new[] { CaptureSystemSound.Question,CaptureSystemSound.Beep },listPlayed,"NotReceived is silent, Gong is ignored.");
			Assert.AreEqual(1,listLog.Count,"The unknown name is logged once.");
			StringAssert.StartsWith(listLog[0],"[Sounds] Disabled=\"Gong\" is not a Windows system sound");
			Assert.IsNull(player.Resolve(CaptureSound.NotReceived));
			Assert.AreEqual(CaptureSystemSound.Question,player.Resolve(CaptureSound.Saved));
		}

		[TestMethod]
		public void Player_ErrorKeyMissing_PlaysTheNotReceivedSound() {
			CaptureSessionConfig config=CaptureSessionTestConfig.Read("[Sounds]","NotReceived=SystemExclamation");
			List<CaptureSystemSound> listPlayed=new List<CaptureSystemSound>();
			CaptureSoundPlayer player=new CaptureSoundPlayer(config,listPlayed.Add,null);
			player.Play(CaptureSound.Error);
			CollectionAssert.AreEqual(new[] { CaptureSystemSound.Exclamation },listPlayed);
		}

		[TestMethod]
		public void Player_PlayThrows_LoggedNeverThrown() {
			List<string> listLog=new List<string>();
			CaptureSoundPlayer player=new CaptureSoundPlayer(null,x => throw new InvalidOperationException("No sound device."),listLog.Add);
			player.Play(CaptureSound.Saved);
			Assert.AreEqual("The Saved sound could not be played: No sound device.",listLog.Single());
		}

		private static void AssertName(string name,CaptureSystemSound expected) {
			CaptureSystemSound? systemSound;
			Assert.IsTrue(CaptureSoundNames.TryParse(name,out systemSound),name);
			Assert.AreEqual(expected,systemSound,name);
		}
	}

	///<summary>A CaptureSessionConfig from capture.ini lines, through a file in a new temporary folder that is deleted again.</summary>
	internal static class CaptureSessionTestConfig {
		public static LocalCaptureConfig Load(params string[] arrayLines) {
			string dir=Path.Combine(Path.GetTempPath(),"CaptureSessionTestConfig_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			try {
				string path=Path.Combine(dir,"capture.ini");
				File.WriteAllBytes(path,new UTF8Encoding(false).GetBytes(string.Join("\r\n",arrayLines)+"\r\n"));
				return LocalCaptureConfig.Load(path,Path.Combine(dir,"fallback.ini"));
			}
			finally {
				try {
					Directory.Delete(dir,true);
				}
				catch {
				}
			}
		}

		public static CaptureSessionConfig Read(params string[] arrayLines) {
			return CaptureSessionConfig.Read(Load(arrayLines));
		}
	}
}
