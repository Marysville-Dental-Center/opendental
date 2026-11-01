// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Uvc.Interop;

namespace ImagingCapture.Uvc {
	///<summary>Chooses the stream format (design 8.8).
	///
	///Default: among modes that reach at least 15 frames/s, the largest frame size. Among modes of equal size, prefer compressed
	///MJPG, then YUY2, then RGB24, then anything else. If no mode reaches 15 frames/s, the fastest mode at the largest size.
	///
	///Justification, independent of any vendor or device: a USB 2.0 UVC isochronous endpoint carries at most 3 x 1024 bytes per
	///125 us microframe, about 196.6 Mbit/s. 1920x1080 YUY2 needs about 33 Mbit per frame (at most about 6 frames/s) and RGB24
	///about 50 Mbit, so uncompressed modes at high resolution cannot run at a useful preview rate. Photos want the largest size
	///that still gives a usable preview. 15 frames/s is the floor for that preview.
	///
	///capture.ini [Camera] Width, Height and Subtype override the default: the modes are narrowed to those that match each
	///override given (Width and Height above 0, Subtype other than "auto"), and the default rule picks among them. When none
	///match, the default over all modes is used and the choice says the override was not found.</summary>
	public static class ModeSelector {
		///<summary>The preview frame-rate floor of the default rule.</summary>
		public const double MinPreviewFramesPerSecond=15;
		///<summary>Frame intervals are whole 100 ns units, so 15 frames/s is often stored as 666667, which is 14.99999 frames/s.</summary>
		private const double FramesPerSecondTolerance=0.01;
		public const string SubtypeAuto="auto";

		///<summary>The default choice, or null when there are no modes.</summary>
		public static UvcMode SelectDefault(IEnumerable<UvcMode> modes) {
			List<UvcMode> listModes=(modes??Enumerable.Empty<UvcMode>()).Where(x => x!=null && x.Width>0 && x.Height>0).ToList();
			if(listModes.Count==0) {
				return null;
			}
			List<UvcMode> listFastEnough=listModes
				.Where(x => x.MaxFramesPerSecond>=MinPreviewFramesPerSecond-FramesPerSecondTolerance)
				.ToList();
			if(listFastEnough.Count>0) {
				return listFastEnough
					.OrderByDescending(x => x.Area)
					.ThenByDescending(x => x.Width)
					.ThenBy(x => GetSubtypeRank(x))
					.ThenByDescending(x => x.MaxFramesPerSecond)
					.ThenBy(x => x.Index)
					.First();
			}
			UvcMode modeLargest=listModes.OrderByDescending(x => x.Area).ThenByDescending(x => x.Width).First();
			return listModes
				.Where(x => x.Width==modeLargest.Width && x.Height==modeLargest.Height)
				.OrderByDescending(x => x.MaxFramesPerSecond)
				.ThenBy(x => GetSubtypeRank(x))
				.ThenBy(x => x.Index)
				.First();
		}

		///<summary>Applies the capture.ini overrides, then the default rule. width or height 0 means any; subtype "" or "auto"
		///means any, otherwise it is compared with UvcMode.SubtypeName ("MJPG", "YUY2", "RGB24", or another FourCC), ignoring
		///case.</summary>
		public static ModeChoice Select(IEnumerable<UvcMode> modes,int width,int height,string subtype) {
			List<UvcMode> listModes=(modes??Enumerable.Empty<UvcMode>()).Where(x => x!=null).ToList();
			bool hasSubtype=!IsSubtypeAuto(subtype);
			bool hasOverride=width>0 || height>0 || hasSubtype;
			if(!hasOverride) {
				return new ModeChoice(SelectDefault(listModes),false,false);
			}
			List<UvcMode> listMatches=listModes
				.Where(x => width<=0 || x.Width==width)
				.Where(x => height<=0 || x.Height==height)
				.Where(x => !hasSubtype || string.Equals(x.SubtypeName,subtype.Trim(),StringComparison.OrdinalIgnoreCase))
				.ToList();
			if(listMatches.Count>0) {
				return new ModeChoice(SelectDefault(listMatches),true,true);
			}
			return new ModeChoice(SelectDefault(listModes),true,false);
		}

		///<summary>True for "", null and "auto" (any case).</summary>
		public static bool IsSubtypeAuto(string subtype) {
			return string.IsNullOrWhiteSpace(subtype) || string.Equals(subtype.Trim(),SubtypeAuto,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>0 for MJPG, 1 for YUY2, 2 for RGB24, 3 for anything else.</summary>
		internal static int GetSubtypeRank(UvcMode mode) {
			if(mode.Subtype==Ds.MEDIASUBTYPE_MJPG) {
				return 0;
			}
			if(mode.Subtype==Ds.MEDIASUBTYPE_YUY2) {
				return 1;
			}
			if(mode.Subtype==Ds.MEDIASUBTYPE_RGB24) {
				return 2;
			}
			return 3;
		}
	}

	///<summary>The result of ModeSelector.Select.</summary>
	public sealed class ModeChoice {
		///<summary>The chosen mode, or null when there are no modes.</summary>
		public UvcMode Mode { get; }
		///<summary>True when capture.ini gave a Width, Height or Subtype.</summary>
		public bool HasOverride { get; }
		///<summary>True when the override matched at least one mode. False with HasOverride means the override was ignored.</summary>
		public bool IsOverrideMatched { get; }

		public ModeChoice(UvcMode mode,bool hasOverride,bool isOverrideMatched) {
			Mode=mode;
			HasOverride=hasOverride;
			IsOverrideMatched=isOverrideMatched;
		}
	}
}
