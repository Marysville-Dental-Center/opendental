// The Edit Text window of the Imaging module keeps its Actual Size and Apparent Size boxes in step while the user types (Open Dental 25.3, backlog
// package B-IMG). The window only forwards its text-changed events here. Licensed under the GNU General Public License, version 2 or later. See
// LICENSE in the repository root.
using System;
using System.Globalization;

namespace OpenPmsDentalBusiness {
	///<summary>Keeps two font size boxes in step: the actual size of a text drawing (in image or mount pixels) and the size it appears at the current
	///zoom. Typing a valid number in one box updates the other at once; blank or invalid text is ignored until it becomes valid. Setting the other box
	///raises its own text-changed event, and a guard flag stops that event from updating the first box back, so the two never update each other in a
	///loop.</summary>
	public class ImageDrawFontSizeSync {
		///<summary>The smallest and largest sizes the boxes accept. The same limits as the boxes of the window.</summary>
		public const double MIN_SIZE=1;
		public const double MAX_SIZE=100000000;
		private readonly float _zoomVal;
		private readonly float _scale;
		private readonly Action<string> _actionSetActualText;
		private readonly Action<string> _actionSetApparentText;
		///<summary>True while this object sets one of the boxes, so the text-changed event that the box raises is ignored.</summary>
		private bool _isUpdating;

		///<summary>zoomVal is the zoom of the image window in percent (typically 100). scale is the screen scale of the window (1 at 96 dpi). The two
		///actions set the text of the boxes.</summary>
		public ImageDrawFontSizeSync(float zoomVal,float scale,Action<string> actionSetActualText,Action<string> actionSetApparentText) {
			_zoomVal=zoomVal;
			_scale=scale;
			_actionSetActualText=actionSetActualText;
			_actionSetApparentText=actionSetApparentText;
		}

		///<summary>Call from the Actual Size box's text-changed event.</summary>
		public void OnActualTextChanged(string text) {
			if(_isUpdating || !CanConvert()) {
				return;
			}
			float fontSize;
			if(!TryParseSize(text,out fontSize)) {
				return;
			}
			SetGuarded(_actionSetApparentText,FormatSize(ToApparent(fontSize,_zoomVal,_scale)));
		}

		///<summary>Call from the Apparent Size box's text-changed event.</summary>
		public void OnApparentTextChanged(string text) {
			if(_isUpdating || !CanConvert()) {
				return;
			}
			float fontSizeApparent;
			if(!TryParseSize(text,out fontSizeApparent)) {
				return;
			}
			SetGuarded(_actionSetActualText,FormatSize(ToActual(fontSizeApparent,_zoomVal,_scale)));
		}

		///<summary>A zoom or scale of zero would divide by zero, so nothing is converted.</summary>
		private bool CanConvert() {
			return _zoomVal>0 && _scale>0;
		}

		private void SetGuarded(Action<string> actionSetText,string text) {
			if(actionSetText==null) {
				return;
			}
			_isUpdating=true;
			try {
				actionSetText(text);
			}
			finally {
				_isUpdating=false;
			}
		}

		///<summary>The size the text appears at the given zoom. At 50% zoom the apparent size is half the actual size. The same math the window used
		///before 25.3.</summary>
		public static float ToApparent(float fontSize,float zoomVal,float scale) {
			return fontSize*zoomVal/100f/scale;
		}

		///<summary>The actual size that appears at the given size at the given zoom. At 50% zoom the actual size is twice the apparent size. The same
		///math is used when a new text drawing is made outside the window.</summary>
		public static float ToActual(float fontSizeApparent,float zoomVal,float scale) {
			return fontSizeApparent/zoomVal*100f*scale;
		}

		///<summary>The text the boxes show: at most two decimals, as before 25.3.</summary>
		public static string FormatSize(float size) {
			return size.ToString("#.##");
		}

		///<summary>True if the text is a number the boxes accept (the same parsing and limits as the boxes). False for blank text, letters or symbols,
		///and numbers out of range.</summary>
		public static bool TryParseSize(string text,out float size) {
			size=0;
			if(string.IsNullOrWhiteSpace(text)) {
				return false;
			}
			double value;
			//Convert.ToDouble, which the boxes use, parses with these styles in the current culture.
			if(!double.TryParse(text,NumberStyles.Float|NumberStyles.AllowThousands,CultureInfo.CurrentCulture,out value)) {
				return false;
			}
			if(double.IsNaN(value) || double.IsInfinity(value) || value<MIN_SIZE || value>MAX_SIZE) {
				return false;
			}
			size=(float)value;
			return true;
		}
	}
}
