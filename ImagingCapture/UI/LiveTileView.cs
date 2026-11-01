// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using ImagingCapture.Camera;

namespace ImagingCapture.UI {
	///<summary>How a live tile is drawn.</summary>
	public enum LiveTileState {
		///<summary>The configuration under test does not set this input up, and it is not firing.</summary>
		NotSetUp=0,
		///<summary>Set up, not active now.</summary>
		Waiting=1,
		///<summary>Active now: a key held, a still sample within its lockout, or a level at its active state.</summary>
		Lit=2,
	}

	///<summary>The state and text of a LiveTestModel tile, for the Learn window's test page and the button test.</summary>
	public static class LiveTileView {
		///<summary>Lit whenever the tile is lit, even when not set up: an unconfigured still tile lights to show that the still pin
		///responds.</summary>
		public static LiveTileState GetState(LiveTile liveTile) {
			if(liveTile==null) {
				return LiveTileState.NotSetUp;
			}
			if(liveTile.IsLit) {
				return LiveTileState.Lit;
			}
			return liveTile.IsConfigured ? LiveTileState.Waiting : LiveTileState.NotSetUp;
		}

		///<summary>Two lines: the tile's label, then the press count for a trigger, "Active" or "Inactive" for a level, or "Not set
		///up".</summary>
		public static string GetText(LiveTile liveTile) {
			if(liveTile==null) {
				return "";
			}
			string line2;
			if(!liveTile.IsConfigured) {
				line2="Not set up";
			}
			else if(liveTile.Kind==LiveTileKind.Level) {
				line2=liveTile.IsLit ? "Active" : "Inactive";
			}
			else {
				line2="Presses: "+liveTile.PressCount.ToString(CultureInfo.InvariantCulture);
			}
			return liveTile.Label+"\r\n"+line2;
		}
	}
}
