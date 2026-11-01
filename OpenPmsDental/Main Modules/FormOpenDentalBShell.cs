// B-SHELL additions to the main window: the module switch blur (25.2) and the File Monitor menu item (25.3). The hooks in FormOpenDental.cs and
// FormOpenDentalMenus.cs are one line each.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormOpenDental {
		///<summary>The cover from the last module switch, while it is up.</summary>
		private ModuleSwitchBlurOverlay _moduleSwitchBlurOverlay;

		///<summary>Open Dental 25.2, "Blur when switching between modules to prevent accidental clicking" (Preferences, Main Window, Misc). Covers the
		///module that was just selected with a blurred snapshot of itself for ModuleSwitchBlur.DURATION_MS and discards mouse clicks on it meanwhile.
		///Keyboard input is not blocked. Called after a module bar click has selected and filled the new module; isUserClick is false when code
		///raised the click (no button), which never blurs.</summary>
		private void ShowModuleSwitchBlur(bool isUserClick) {
			try {
				if(!ModuleSwitchBlur.ShouldBlur(ModuleSwitchBlur.IsEnabled(),isUserClick)) {
					return;
				}
				Control controlModule=GetVisibleModuleControl();
				if(controlModule==null || controlModule.Width<1 || controlModule.Height<1 || WindowState==FormWindowState.Minimized) {
					return;
				}
				CloseModuleSwitchBlur();
				Rectangle rectangleScreen=controlModule.RectangleToScreen(controlModule.ClientRectangle);
				Bitmap bitmapBlurred=null;
				try {
					controlModule.Update();//Paint the new module now, so the snapshot shows it.
					using Bitmap bitmapSnapshot=new Bitmap(rectangleScreen.Width,rectangleScreen.Height);
					using(Graphics g=Graphics.FromImage(bitmapSnapshot)) {
						g.CopyFromScreen(rectangleScreen.Location,Point.Empty,rectangleScreen.Size);
					}
					bitmapBlurred=ModuleSwitchBlur.BlurBitmap(bitmapSnapshot);
				}
				catch(Exception ex) {
					ex.DoNothing();//For example a remote desktop session with no drawing surface. The cover is then plain and translucent.
					bitmapBlurred?.Dispose();
					bitmapBlurred=null;
				}
				_moduleSwitchBlurOverlay=new ModuleSwitchBlurOverlay(rectangleScreen,bitmapBlurred,ModuleSwitchBlur.DURATION_MS);
				_moduleSwitchBlurOverlay.Show(this);
			}
			catch(Exception ex) {
				ex.DoNothing();//The cover only guards against stray clicks. It must never stop a module switch.
				CloseModuleSwitchBlur();
			}
		}

		private void CloseModuleSwitchBlur() {
			if(_moduleSwitchBlurOverlay==null) {
				return;
			}
			ModuleSwitchBlurOverlay moduleSwitchBlurOverlay=_moduleSwitchBlurOverlay;
			_moduleSwitchBlurOverlay=null;
			ODException.SwallowAnyException(() => {
				if(!moduleSwitchBlurOverlay.IsDisposed) {
					moduleSwitchBlurOverlay.Close();
				}
			});
		}

		///<summary>The main module control that is showing, or null.</summary>
		private Control GetVisibleModuleControl() {
			Control[] arrayControls={ controlAppt,controlFamily,controlFamilyEcw,controlAccount,controlTreat,controlChart,controlImagesOld,controlImages,
				controlManage };
			return arrayControls.FirstOrDefault(x => x!=null && x.Visible);
		}

		///<summary>Help, File Monitor (25.3). Paths in the feed name patient folders, so it needs the Setup permission (our hardening; Open Dental's Help
		///menu is open to everyone).</summary>
		private void menuItemFileMonitor_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"File Monitor opened.");
			FormFileMonitor.ShowSingle(this);
		}
	}

	///<summary>The cover shown over a module for a moment after a module switch. A borderless window owned by the main window that never takes focus
	///and swallows the mouse clicks that land on it. Shows a blurred snapshot, or a translucent white cover when there is no snapshot. Closes itself.
	///</summary>
	public class ModuleSwitchBlurOverlay:Form {
		private const int WM_MOUSEACTIVATE=0x0021;
		///<summary>Do not activate the window, and discard the mouse message.</summary>
		private const int MA_NOACTIVATEANDEAT=4;
		private const int WS_EX_TOOLWINDOW=0x00000080;
		private const int WS_EX_NOACTIVATE=0x08000000;
		private readonly Timer _timerClose;
		private Bitmap _bitmapBlurred;

		///<summary>bitmapBlurred can be null for the translucent fallback. The window owns it and disposes it.</summary>
		public ModuleSwitchBlurOverlay(Rectangle rectangleScreen,Bitmap bitmapBlurred,int durationMs) {
			FormBorderStyle=FormBorderStyle.None;
			ShowInTaskbar=false;
			ControlBox=false;
			StartPosition=FormStartPosition.Manual;
			Bounds=rectangleScreen;
			DoubleBuffered=true;
			Text="";
			_bitmapBlurred=bitmapBlurred;
			if(_bitmapBlurred==null) {
				BackColor=Color.White;
				Opacity=ModuleSwitchBlur.FALLBACK_OPACITY;
			}
			else {
				BackgroundImage=_bitmapBlurred;
				BackgroundImageLayout=ImageLayout.None;
			}
			_timerClose=new Timer();
			_timerClose.Interval=Math.Max(1,durationMs);
			_timerClose.Tick+=(sender,e) => {
				_timerClose.Stop();
				Close();
			};
		}

		protected override bool ShowWithoutActivation {
			get { return true; }
		}

		protected override CreateParams CreateParams {
			get {
				CreateParams createParams=base.CreateParams;
				createParams.ExStyle|=WS_EX_NOACTIVATE|WS_EX_TOOLWINDOW;
				return createParams;
			}
		}

		protected override void OnShown(EventArgs e) {
			base.OnShown(e);
			_timerClose.Start();
		}

		protected override void WndProc(ref Message m) {
			if(m.Msg==WM_MOUSEACTIVATE) {
				m.Result=(IntPtr)MA_NOACTIVATEANDEAT;
				return;
			}
			base.WndProc(ref m);
		}

		protected override void Dispose(bool disposing) {
			if(disposing) {
				_timerClose.Dispose();
				BackgroundImage=null;
				_bitmapBlurred?.Dispose();
				_bitmapBlurred=null;
			}
			base.Dispose(disposing);
		}
	}
}
