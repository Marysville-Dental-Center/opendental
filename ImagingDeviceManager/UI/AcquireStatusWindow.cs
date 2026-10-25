// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ImagingDeviceManager.UI {
	///<summary>The small status and Cancel window of a thread-modal device acquisition (design 7.1). Topmost and owned by no window, so it is
	///never behind or disabled with the windows the acquisition disabled, and the program never looks frozen when the sensor software's
	///window ends up behind it. It does not take activation when shown. Its buttons work inside the acquisition's message loop, which
	///dispatches its messages. Everything it shows and does comes from AcquireStatusController.</summary>
	internal sealed class AcquireStatusWindow:Form,IAcquireStatusView {
		private const int MarginPx=10;
		private const int WidthTextPx=330;
		private readonly AcquireStatusController _controller;
		private readonly Label _labelMessage;
		private readonly Label _labelUnassigned;
		private readonly Button _butCancel;
		private readonly Button _butBack;
		private bool _isClosingByLibrary;

		public AcquireStatusWindow(AcquireStatusController controller) {
			_controller=controller;
			SuspendLayout();
			Text="Capture";
			Font=SystemFonts.MessageBoxFont;
			AutoScaleMode=AutoScaleMode.None;//Sizes below are measured from the font.
			FormBorderStyle=FormBorderStyle.FixedToolWindow;
			ControlBox=false;
			MaximizeBox=false;
			MinimizeBox=false;
			ShowInTaskbar=false;
			TopMost=true;
			StartPosition=FormStartPosition.Manual;
			_labelMessage=new Label { AutoSize=false,UseMnemonic=false };
			_labelUnassigned=new Label { AutoSize=false,UseMnemonic=false,ForeColor=Color.DarkRed };
			_butCancel=new Button { UseMnemonic=false };
			_butBack=new Button { UseMnemonic=false,Text=AcquireStatusController.BackButtonText };
			_butCancel.Click+=(sender,e) => _controller.ClickCancel();
			_butBack.Click+=(sender,e) => _controller.ClickBack();
			Controls.Add(_labelMessage);
			Controls.Add(_labelUnassigned);
			Controls.Add(_butCancel);
			Controls.Add(_butBack);
			ResumeLayout(false);
			_controller.Changed+=RefreshFromController;
			FormClosed+=(sender,e) => _controller.Changed-=RefreshFromController;
			RefreshFromController();
		}

		protected override bool ShowWithoutActivation => true;

		public Button ButtonCancel => _butCancel;

		public Button ButtonBack => _butBack;

		public string MessageShown => _labelMessage.Text;

		///<summary>Closes the window when the acquisition ends. Harmless when it is already closed.</summary>
		void IAcquireStatusView.Close() {
			if(IsDisposed) {
				return;
			}
			_isClosingByLibrary=true;
			Close();
		}

		protected override void OnFormClosing(FormClosingEventArgs e) {
			//Alt+F4 or another close that is not the library's: a cancel request, with the warning when it needs one. The window stays
			//until the acquisition ends. Closes by Windows itself (shutdown, Task Manager) are allowed.
			if(!_isClosingByLibrary && e.CloseReason==CloseReason.UserClosing) {
				e.Cancel=true;
				_controller.ClickCancel();
			}
			base.OnFormClosing(e);
		}

		private void RefreshFromController() {
			if(IsDisposed) {
				return;
			}
			_labelMessage.Text=_controller.MessageText??"";
			_labelUnassigned.Text=_controller.UnassignedText??"";
			_labelUnassigned.Visible=_controller.UnassignedText!=null;
			_butCancel.Text=_controller.CancelText;
			_butCancel.Enabled=_controller.IsCancelEnabled;
			_butBack.Visible=_controller.IsBackVisible;
			LayoutControls();
		}

		///<summary>Sizes the window to its text and keeps it at the bottom right of the work area.</summary>
		private void LayoutControls() {
			TextFormatFlags textFormatFlags=TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl;
			int y=MarginPx;
			Size sizeMessage=TextRenderer.MeasureText(_labelMessage.Text,Font,new Size(WidthTextPx,0),textFormatFlags);
			_labelMessage.Bounds=new Rectangle(MarginPx,y,WidthTextPx,sizeMessage.Height+2);
			y=_labelMessage.Bottom+MarginPx/2;
			if(_labelUnassigned.Visible) {
				Size sizeUnassigned=TextRenderer.MeasureText(_labelUnassigned.Text,Font,new Size(WidthTextPx,0),textFormatFlags);
				_labelUnassigned.Bounds=new Rectangle(MarginPx,y,WidthTextPx,sizeUnassigned.Height+2);
				y=_labelUnassigned.Bottom+MarginPx/2;
			}
			int heightButton=Font.Height+12;
			int widthCancel=Math.Max(90,TextRenderer.MeasureText(AcquireStatusController.CancelAnywayButtonText,Font).Width+24);
			int widthBack=Math.Max(90,TextRenderer.MeasureText(AcquireStatusController.BackButtonText,Font).Width+24);
			_butCancel.Bounds=new Rectangle(MarginPx+WidthTextPx-widthCancel,y,widthCancel,heightButton);
			_butBack.Bounds=new Rectangle(_butCancel.Left-MarginPx-widthBack,y,widthBack,heightButton);
			ClientSize=new Size(WidthTextPx+2*MarginPx,_butCancel.Bottom+MarginPx);
			Rectangle rectWork=Screen.FromPoint(Cursor.Position).WorkingArea;
			Location=new Point(rectWork.Right-Width-MarginPx,rectWork.Bottom-Height-MarginPx);
		}
	}
}
