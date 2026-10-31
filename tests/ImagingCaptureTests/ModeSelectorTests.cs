// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using ImagingCapture.Uvc;
using ImagingCapture.Uvc.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>Stream-format ranking over synthetic capability lists (design 8.8). The lists are made up; they describe no real
	///camera.</summary>
	[TestClass]
	public class ModeSelectorTests {
		private static readonly Guid _guidNv12=new Guid("3231564E-0000-0010-8000-00AA00389B71");
		private List<UvcMode> _listModes;

		[TestInitialize]
		public void Init() {
			_listModes=new List<UvcMode>();
		}

		///<summary>Adds a mode with the given frame rate (0 = unknown) and returns it.</summary>
		private UvcMode Add(int width,int height,Guid subtype,double framesPerSecond) {
			long interval=framesPerSecond>0 ? (long)Math.Round(10000000.0/framesPerSecond) : 0;
			return AddInterval(width,height,subtype,interval);
		}

		private UvcMode AddInterval(int width,int height,Guid subtype,long interval) {
			UvcMode mode=new UvcMode(_listModes.Count,width,height,subtype,subtype==Ds.MEDIASUBTYPE_YUY2 ? 16 : 24,interval);
			_listModes.Add(mode);
			return mode;
		}

		[TestMethod]
		public void Default_LargestSizeThatReaches15Fps() {
			Add(640,480,Ds.MEDIASUBTYPE_MJPG,30);
			Add(1280,720,Ds.MEDIASUBTYPE_MJPG,30);
			UvcMode modeExpected=Add(1920,1080,Ds.MEDIASUBTYPE_MJPG,30);
			Add(1920,1080,Ds.MEDIASUBTYPE_YUY2,5);
			Add(2592,1944,Ds.MEDIASUBTYPE_YUY2,2);
			Assert.AreSame(modeExpected,ModeSelector.SelectDefault(_listModes));
		}

		[TestMethod]
		public void Default_EqualSizePrefersMjpgThenYuy2ThenRgb24() {
			UvcMode modeRgb24=Add(1280,720,Ds.MEDIASUBTYPE_RGB24,30);
			UvcMode modeNv12=Add(1280,720,_guidNv12,30);
			UvcMode modeYuy2=Add(1280,720,Ds.MEDIASUBTYPE_YUY2,30);
			UvcMode modeMjpg=Add(1280,720,Ds.MEDIASUBTYPE_MJPG,30);
			Assert.AreSame(modeMjpg,ModeSelector.SelectDefault(_listModes));
			_listModes.Remove(modeMjpg);
			Assert.AreSame(modeYuy2,ModeSelector.SelectDefault(_listModes));
			_listModes.Remove(modeYuy2);
			Assert.AreSame(modeRgb24,ModeSelector.SelectDefault(_listModes),"Other subtypes rank after RGB24.");
			_listModes.Remove(modeRgb24);
			Assert.AreSame(modeNv12,ModeSelector.SelectDefault(_listModes));
		}

		[TestMethod]
		public void Default_SizeOutranksSubtype() {
			Add(1280,720,Ds.MEDIASUBTYPE_MJPG,30);
			UvcMode modeExpected=Add(1600,1200,Ds.MEDIASUBTYPE_YUY2,15);
			Assert.AreSame(modeExpected,ModeSelector.SelectDefault(_listModes));
		}

		[TestMethod]
		public void Default_FrameRateFloorAllowsIntervalRounding() {
			Add(1280,720,Ds.MEDIASUBTYPE_MJPG,30);
			UvcMode modeLarge=AddInterval(1920,1080,Ds.MEDIASUBTYPE_MJPG,666667);//14.99999 frames/s, stored for 15
			Assert.AreSame(modeLarge,ModeSelector.SelectDefault(_listModes));
		}

		[TestMethod]
		public void Default_BelowTheFloorIsNotChosenWhileAFasterModeExists() {
			UvcMode modeSmall=Add(1280,720,Ds.MEDIASUBTYPE_MJPG,30);
			Add(1920,1080,Ds.MEDIASUBTYPE_MJPG,14);
			Add(2592,1944,Ds.MEDIASUBTYPE_MJPG,0);//unknown rate counts as below the floor
			Assert.AreSame(modeSmall,ModeSelector.SelectDefault(_listModes));
		}

		[TestMethod]
		public void Default_NothingReaches15Fps_FastestModeAtLargestSize() {
			Add(1280,720,Ds.MEDIASUBTYPE_YUY2,10);
			Add(2592,1944,Ds.MEDIASUBTYPE_YUY2,2);
			UvcMode modeExpected=Add(2592,1944,Ds.MEDIASUBTYPE_MJPG,10);
			Add(2592,1944,Ds.MEDIASUBTYPE_RGB24,1);
			Assert.AreSame(modeExpected,ModeSelector.SelectDefault(_listModes));
		}

		[TestMethod]
		public void Default_NothingReaches15Fps_EqualRateAtLargestSizePrefersMjpg() {
			Add(2592,1944,Ds.MEDIASUBTYPE_YUY2,5);
			UvcMode modeExpected=Add(2592,1944,Ds.MEDIASUBTYPE_MJPG,5);
			Assert.AreSame(modeExpected,ModeSelector.SelectDefault(_listModes));
		}

		[TestMethod]
		public void Default_AllRatesUnknown_LargestSize() {
			Add(640,480,Ds.MEDIASUBTYPE_YUY2,0);
			UvcMode modeExpected=Add(1280,960,Ds.MEDIASUBTYPE_YUY2,0);
			Assert.AreSame(modeExpected,ModeSelector.SelectDefault(_listModes));
		}

		[TestMethod]
		public void Default_EqualFormatsKeepTheFasterThenTheFirst() {
			Add(1280,720,Ds.MEDIASUBTYPE_MJPG,15);
			UvcMode modeFast=Add(1280,720,Ds.MEDIASUBTYPE_MJPG,30);
			Add(1280,720,Ds.MEDIASUBTYPE_MJPG,30);
			Assert.AreSame(modeFast,ModeSelector.SelectDefault(_listModes));
		}

		[TestMethod]
		public void Default_NoModes() {
			Assert.IsNull(ModeSelector.SelectDefault(_listModes));
			Assert.IsNull(ModeSelector.SelectDefault(null));
			ModeChoice modeChoice=ModeSelector.Select(null,0,0,"auto");
			Assert.IsNull(modeChoice.Mode);
			Assert.IsFalse(modeChoice.HasOverride);
		}

		[TestMethod]
		public void Override_NoneGiven_UsesDefault() {
			Add(640,480,Ds.MEDIASUBTYPE_MJPG,30);
			UvcMode modeExpected=Add(1280,720,Ds.MEDIASUBTYPE_MJPG,30);
			foreach(string subtype in new[] { null,"","auto","AUTO"," auto " }) {
				ModeChoice modeChoice=ModeSelector.Select(_listModes,0,0,subtype);
				Assert.AreSame(modeExpected,modeChoice.Mode,"Subtype '"+subtype+"'");
				Assert.IsFalse(modeChoice.HasOverride);
			}
		}

		[TestMethod]
		public void Override_SizeNarrowsTheChoice() {
			Add(640,480,Ds.MEDIASUBTYPE_YUY2,30);
			UvcMode modeExpected=Add(640,480,Ds.MEDIASUBTYPE_MJPG,30);
			Add(1920,1080,Ds.MEDIASUBTYPE_MJPG,30);
			ModeChoice modeChoice=ModeSelector.Select(_listModes,640,480,"auto");
			Assert.AreSame(modeExpected,modeChoice.Mode);
			Assert.IsTrue(modeChoice.HasOverride);
			Assert.IsTrue(modeChoice.IsOverrideMatched);
		}

		[TestMethod]
		public void Override_SubtypeNarrowsTheChoiceIgnoringCase() {
			Add(1920,1080,Ds.MEDIASUBTYPE_MJPG,30);
			UvcMode modeExpected=Add(1280,720,Ds.MEDIASUBTYPE_YUY2,15);
			Add(640,480,Ds.MEDIASUBTYPE_YUY2,30);
			ModeChoice modeChoice=ModeSelector.Select(_listModes,0,0,"yuy2");
			Assert.AreSame(modeExpected,modeChoice.Mode);
			Assert.IsTrue(modeChoice.IsOverrideMatched);
		}

		[TestMethod]
		public void Override_OtherFourCCMatchesByName() {
			Add(1280,720,Ds.MEDIASUBTYPE_MJPG,30);
			UvcMode modeExpected=Add(1280,720,_guidNv12,30);
			Assert.AreSame(modeExpected,ModeSelector.Select(_listModes,0,0,"NV12").Mode);
		}

		[TestMethod]
		public void Override_MatchBelowTheFloorIsStillHonoured() {
			Add(1920,1080,Ds.MEDIASUBTYPE_MJPG,30);
			UvcMode modeExpected=Add(2592,1944,Ds.MEDIASUBTYPE_YUY2,2);
			ModeChoice modeChoice=ModeSelector.Select(_listModes,2592,1944,"");
			Assert.AreSame(modeExpected,modeChoice.Mode);
			Assert.IsTrue(modeChoice.IsOverrideMatched);
		}

		[TestMethod]
		public void Override_NotFound_FallsBackToDefaultAndSaysSo() {
			Add(640,480,Ds.MEDIASUBTYPE_MJPG,30);
			UvcMode modeDefault=Add(1280,720,Ds.MEDIASUBTYPE_MJPG,30);
			ModeChoice modeChoice=ModeSelector.Select(_listModes,1024,768,"MJPG");
			Assert.AreSame(modeDefault,modeChoice.Mode);
			Assert.IsTrue(modeChoice.HasOverride);
			Assert.IsFalse(modeChoice.IsOverrideMatched);
			modeChoice=ModeSelector.Select(_listModes,0,0,"RGB24");
			Assert.AreSame(modeDefault,modeChoice.Mode);
			Assert.IsFalse(modeChoice.IsOverrideMatched);
		}

		[TestMethod]
		public void Override_WidthOnly() {
			UvcMode modeExpected=Add(1280,960,Ds.MEDIASUBTYPE_MJPG,30);
			Add(1280,720,Ds.MEDIASUBTYPE_MJPG,30);
			Add(1920,1080,Ds.MEDIASUBTYPE_MJPG,30);
			Assert.AreSame(modeExpected,ModeSelector.Select(_listModes,1280,0,"auto").Mode);
		}
	}
}
