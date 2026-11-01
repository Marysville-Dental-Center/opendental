// B-SHELL (v25_2-ui-blur-module-switch): the "blur when switching between modules" setting and its rules.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Open Dental 25.2 added a preference that briefly blurs the main window when the user switches modules, so a double click on the module
	///bar or a click that lands while the new module redraws cannot hit a button or grid row in the new module. Our design: the incoming module is
	///covered for DurationMs by a blurred snapshot of itself, mouse clicks on it are discarded and keyboard input is not blocked. If the snapshot
	///fails (for example over a remote desktop session without a drawing surface) a plain translucent cover is used instead. Only module bar clicks
	///blur; module changes made by code ("Go to" actions, refreshes) do not.
	///<para>Open Dental's preference name is not public (the 25.2 spec only proposes ModuleSwitchBlur, which RegistryTests keeps out of PrefName
	///until it is confirmed). So the setting lives in the fork's own preference row PREF_NAME, read and written by name. No migration step adds
	///the row, so an adopted Open Dental database is left exactly as it was: a missing row means off, and Save inserts it the first time the office
	///turns the setting on.</para></summary>
	public static class ModuleSwitchBlur {
		///<summary>The fork's own preference row. Boolean, default off. Global, not per computer.</summary>
		public const string PREF_NAME="ForkModuleSwitchBlur";
		///<summary>How long the cover stays up, in milliseconds (our choice; about the time a module takes to redraw).</summary>
		public const int DURATION_MS=400;
		///<summary>The snapshot is shrunk by this factor and stretched back, which blurs it cheaply.</summary>
		public const int BLUR_FACTOR=8;
		///<summary>Opacity of the fallback cover when no snapshot could be taken.</summary>
		public const double FALLBACK_OPACITY=0.55;

		///<summary>True when the preference row exists and is on. A missing row (the setting was never turned on) means off.</summary>
		public static bool IsEnabled() {
			try {
				return PIn.Bool(PrefC.GetRawOrDefault(PREF_NAME,"0"));//B-SERVER's shared helper for fork-owned preference rows.
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>Saves the setting. Inserts the row if it is missing, and updates this workstation's cache. Returns true if anything changed; the
		///caller invalidates the preference cache so other workstations reload.</summary>
		public static bool Save(bool isEnabled) {
			return PrefC.UpsertRaw(PREF_NAME,POut.Bool(isEnabled));//B-SERVER's shared helper for fork-owned preference rows.
		}

		///<summary>Whether a module switch gets the cover: only when the setting is on and the switch came from a click on the module bar.</summary>
		public static bool ShouldBlur(bool isEnabled,bool isUserClick) {
			return isEnabled && isUserClick;
		}

		///<summary>A blurred copy of the bitmap: shrunk by factor with bilinear filtering and stretched back to the original size. Returns null for a
		///null or empty bitmap. The caller disposes the result.</summary>
		public static Bitmap BlurBitmap(Bitmap bitmap,int factor=BLUR_FACTOR) {
			if(bitmap==null || bitmap.Width<1 || bitmap.Height<1) {
				return null;
			}
			factor=Math.Max(1,factor);
			int widthSmall=Math.Max(1,bitmap.Width/factor);
			int heightSmall=Math.Max(1,bitmap.Height/factor);
			using Bitmap bitmapSmall=new Bitmap(widthSmall,heightSmall);
			using(Graphics g=Graphics.FromImage(bitmapSmall)) {
				g.InterpolationMode=InterpolationMode.HighQualityBilinear;
				g.PixelOffsetMode=PixelOffsetMode.HighQuality;
				g.DrawImage(bitmap,new Rectangle(0,0,widthSmall,heightSmall));
			}
			Bitmap bitmapBlurred=new Bitmap(bitmap.Width,bitmap.Height);
			using(Graphics g=Graphics.FromImage(bitmapBlurred)) {
				g.InterpolationMode=InterpolationMode.HighQualityBilinear;
				g.PixelOffsetMode=PixelOffsetMode.HighQuality;
				g.DrawImage(bitmapSmall,new Rectangle(0,0,bitmap.Width,bitmap.Height));
			}
			return bitmapBlurred;
		}
	}
}
