// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Camera;
using ImagingCapture.Uvc;

namespace ImagingCapture.Video {
	///<summary>One entry of the Video window's resolution choice (design 8.8): Automatic, or one stream format of the open camera.
	///The choice is saved as capture.ini [Camera] Width, Height and Subtype, which ModeSelector applies.</summary>
	public sealed class ResolutionChoice {
		///<summary>The format, or null for Automatic (the default rule of ModeSelector).</summary>
		public UvcMode Mode { get; }
		///<summary>The menu text, in English: "Automatic (1920x1080 MJPG 30 fps)" or "1280x720 YUY2 10 fps".</summary>
		public string Text { get; }
		///<summary>True for the entry that matches what capture.ini asks for.</summary>
		public bool IsSelected { get; }

		public ResolutionChoice(UvcMode mode,string text,bool isSelected) {
			Mode=mode;
			Text=text??"";
			IsSelected=isSelected;
		}

		public bool IsAutomatic {
			get {
				return Mode==null;
			}
		}

		///<summary>Automatic first, then every format of modes, largest first (then MJPG, YUY2, RGB24, others; then the faster).
		///Exactly one entry is selected: the format ModeSelector picks for the saved Width, Height and Subtype, or Automatic when
		///nothing is saved or the saved values match no format (its text then says so).</summary>
		public static List<ResolutionChoice> GetChoices(IEnumerable<UvcMode> modes,CameraConfig cameraConfig) {
			List<UvcMode> listModes=(modes??Enumerable.Empty<UvcMode>()).Where(x => x!=null && x.Width>0 && x.Height>0).ToList();
			cameraConfig=cameraConfig??new CameraConfig();
			ModeChoice modeChoice=ModeSelector.Select(listModes,cameraConfig.Width,cameraConfig.Height,cameraConfig.Subtype);
			bool isAutomatic=!modeChoice.HasOverride || !modeChoice.IsOverrideMatched;
			UvcMode modeDefault=ModeSelector.SelectDefault(listModes);
			string textAutomatic="Automatic";
			if(modeDefault!=null) {
				textAutomatic+=" ("+modeDefault+")";
			}
			if(modeChoice.HasOverride && !modeChoice.IsOverrideMatched) {
				textAutomatic+=" - the saved resolution is not offered by this camera";
			}
			List<ResolutionChoice> listChoices=new List<ResolutionChoice>();
			listChoices.Add(new ResolutionChoice(null,textAutomatic,isAutomatic));
			IEnumerable<UvcMode> modesOrdered=listModes
				.OrderByDescending(x => x.Area)
				.ThenByDescending(x => x.Width)
				.ThenBy(x => ModeSelector.GetSubtypeRank(x))
				.ThenBy(x => x.SubtypeName,StringComparer.OrdinalIgnoreCase)
				.ThenByDescending(x => x.MaxFramesPerSecond)
				.ThenBy(x => x.Index);
			foreach(UvcMode mode in modesOrdered) {
				listChoices.Add(new ResolutionChoice(mode,mode.ToString(),!isAutomatic && mode==modeChoice.Mode));
			}
			return listChoices;
		}

		///<summary>Sets [Camera] Width, Height and Subtype in cameraConfig for this choice: the format's size and subtype name, or 0,
		///0 and "auto" for Automatic.</summary>
		public static void Apply(CameraConfig cameraConfig,UvcMode mode) {
			if(cameraConfig==null) {
				return;
			}
			if(mode==null) {
				cameraConfig.Width=0;
				cameraConfig.Height=0;
				cameraConfig.Subtype=ModeSelector.SubtypeAuto;
				return;
			}
			cameraConfig.Width=mode.Width;
			cameraConfig.Height=mode.Height;
			cameraConfig.Subtype=mode.SubtypeName;
		}
	}
}
