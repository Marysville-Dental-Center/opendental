// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ImagingDeviceManager.UI {
	///<summary>A non-modal, topmost notice with an OK button (design 7.1: quarantine, "ended without an image", "may still hold images",
	///source gate). It has no owner, so it never sits behind a window a thread-modal acquisition disabled, and it does not take
	///activation when shown, so it does not pull the foreground away from the source's own window.</summary>
	internal sealed class CaptureNoticeWindow:Form {
		private const int MarginPx=12;
		private const int TextWidthPx=420;
		///<summary>Each further notice is shown a little lower and to the right, so that two notices never cover each other exactly.</summary>
		private const int CascadePx=24;

		public CaptureNoticeWindow(string text) {
			SuspendLayout();
			Text="Capture";
			Font=SystemFonts.MessageBoxFont;
			AutoScaleMode=AutoScaleMode.None;//Sizes below are measured from the font.
			FormBorderStyle=FormBorderStyle.FixedDialog;
			MaximizeBox=false;
			MinimizeBox=false;
			ShowInTaskbar=true;//Findable from the taskbar if another program covers it.
			TopMost=true;
			StartPosition=FormStartPosition.Manual;
			string textWindows=(text??"").Replace("\r\n","\n").Replace("\n","\r\n");
			Size sizeText=TextRenderer.MeasureText(textWindows,Font,new Size(TextWidthPx,0),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl);
			Label label=new Label {
				AutoSize=false,
				Location=new Point(MarginPx,MarginPx),
				Size=new Size(TextWidthPx,sizeText.Height+4),
				Text=textWindows,
				UseMnemonic=false,
			};
			Button butOK=new Button {
				Text="OK",
				Size=new Size(Math.Max(88,TextRenderer.MeasureText("OK",Font).Width+32),Font.Height+14),
			};
			butOK.Location=new Point(MarginPx+TextWidthPx-butOK.Width,label.Bottom+MarginPx);
			butOK.Click+=(sender,e) => Close();
			Controls.Add(label);
			Controls.Add(butOK);
			AcceptButton=butOK;
			CancelButton=butOK;
			ClientSize=new Size(TextWidthPx+2*MarginPx,butOK.Bottom+MarginPx);
			ResumeLayout(false);
		}

		protected override bool ShowWithoutActivation => true;

		///<summary>Centers the notice on the screen under the mouse pointer, moved by CascadePx for each notice shown before it.</summary>
		public void PlaceOnScreen(int indexNotice) {
			Rectangle rectWork=Screen.FromPoint(Cursor.Position).WorkingArea;
			int offset=(indexNotice%5)*CascadePx;
			int x=rectWork.Left+(rectWork.Width-Width)/2+offset;
			int y=rectWork.Top+(rectWork.Height-Height)/3+offset;
			Location=new Point(Math.Max(rectWork.Left,x),Math.Max(rectWork.Top,y));
		}
	}
}
