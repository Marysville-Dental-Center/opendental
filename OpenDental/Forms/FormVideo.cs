using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using CodeBase;
using ImagingCapture.Camera;
using ImagingCapture.UI;
using ImagingCapture.Uvc;
using ImagingCapture.Uvc.DirectShow;
using ImagingCapture.Video;
using ImagingDeviceManager.Config;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>The Imaging module's camera window (design 8.1-8.10). Non-modal, opened by ControlImages through FormLauncher. Each
	///photo (Capture, Space, or a camera button set up with Learn) is raised as BitmapCaptured, which ControlImages saves into the
	///video image category and the selected mount slot.
	///
	///The camera work is in ImagingCapture.Video.VideoCaptureSession, over the in-repo DirectShow layer (no camera library ships with
	///the program). This window shows the preview, passes requests to the session's CaptureGate, and closes when the patient changes,
	///so a photo is never saved for a patient other than the one in its title.</summary>
	public partial class FormVideo:FormODBase {
		#region Fields
		///<summary>Subfolder of the Open Dental log folder for camera problems.</summary>
		private const string LOG_FOLDER="Video";
		private VideoCaptureSession _videoCaptureSession;
		///<summary>1 while a preview update is posted to the UI thread and has not run yet, so a fast camera cannot flood the message
		///queue. Set by the streaming thread, cleared on the UI thread.</summary>
		private int _isFramePosted;
		///<summary>True while a level input with the PausePreview action is active (design 8.6).</summary>
		private bool _isPreviewPaused;
		///<summary>True while the Learn window or the button test is open over this window.</summary>
		private bool _isLearnOpen;
		///<summary>The patient changed while Learn was open: close when it closes.</summary>
		private bool _isCloseRequested;
		///<summary>A camera error that arrived while Learn was open, handled when it closes.</summary>
		private object _senderErrorPending;
		private UvcErrorEventArgs _uvcErrorEventArgsPending;
		private bool _isFillingCombo;
		///<summary>The settings menu last shown, disposed when the next one is built or the window closes.</summary>
		private ContextMenu _contextMenuSettings;
		#endregion Fields

		///<summary>Fires for each photo: the Capture button, Space, or a camera button. The bitmap is a copy made for the handler,
		///which may dispose it; it is disposed after the handler returns. No new photo is raised while a handler is running.</summary>
		[Category("OD")]
		[Description("Fires for each photo: the Capture button, Space, or a camera button.")]
		public event EventHandler<Bitmap> BitmapCaptured;

		public FormVideo() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		#region Methods - Event Handlers
		private void butCapture_Click(object sender,EventArgs e) {
			//tabstop had to be set to false to prevent duplicates when using the space bar.
			RequestCapture(CaptureRequestSource.Button,null);
		}

		private void butRefresh_Click(object sender,EventArgs e) {
			RefreshCamera();
		}

		private void butSettings_Click(object sender,EventArgs e) {
			ShowSettingsMenu();
		}

		private void comboCameras_SelectionChangeCommitted(object sender,EventArgs e) {
			if(_isFillingCombo || _videoCaptureSession==null) {
				return;
			}
			UvcDeviceInfo uvcDeviceInfo=comboCameras.GetSelected<UvcDeviceInfo>();
			if(uvcDeviceInfo==null) {
				return;
			}
			ShowStatus(VideoStatusText.Starting());
			VideoStatus videoStatus;
			string error;
			string warning;
			Cursor=Cursors.WaitCursor;
			try {
				videoStatus=_videoCaptureSession.SelectDevice(uvcDeviceInfo,out error,out warning);
			}
			finally {
				Cursor=Cursors.Default;
			}
			_isPreviewPaused=false;
			ShowStatus(videoStatus);
			ShowSaveResult(error,warning);
		}

		private void FormVideo_FormClosing(object sender,FormClosingEventArgs e) {
			if(e.Cancel) {
				return;
			}
			ReleaseCamera();
			RectangleConverter rectangleConverter=new RectangleConverter();
			String str=rectangleConverter.ConvertToInvariantString(DesktopBounds);
			if(WindowState==FormWindowState.Maximized){
				str=rectangleConverter.ConvertToInvariantString(RestoreBounds);
				str+=",Max";
			}
			ComputerPrefs.LocalComputer.VideoRectangle=str;
			ComputerPrefs.Update(ComputerPrefs.LocalComputer);
		}

		private void FormVideo_KeyDown(object sender,KeyEventArgs e){
			if(e.KeyCode==Keys.Space && !butCapture.Focused) {//if focus is on butCapture, pressing space will be the same as clicking
				RequestCapture(CaptureRequestSource.SpaceInWindow,null);
			}
		}

		private void FormVideo_Load(object sender, EventArgs e){
			string str=ComputerPrefs.LocalComputer.VideoRectangle;
			if(str==""){
				Rectangle rectangleWorkingArea=System.Windows.Forms.Screen.FromHandle(this.Handle).WorkingArea;
				Location=new Point(rectangleWorkingArea.Left+rectangleWorkingArea.Width/2-Width/2,//center L/R
					rectangleWorkingArea.Bottom-Height);//bottom
			}
			else{
				RectangleConverter rectangleConverter=new RectangleConverter();
				if(str.EndsWith(",Max")){
					str=str.Substring(0,str.Length-4);
					Rectangle rectangle=(Rectangle)rectangleConverter.ConvertFromInvariantString(str);
					DesktopBounds=rectangle;
					//by setting max after bounds, it can restore back to the bounds we set.
					WindowState=FormWindowState.Maximized;
				}
				else{
					Rectangle rectangle=(Rectangle)rectangleConverter.ConvertFromInvariantString(str);
					DesktopBounds=rectangle;
				}
			}
			//The patient is the one the main window shows now (design 8.10). Imaging enables Video only with a patient selected.
			long patNum=FormOpenDental.PatNumCur;
			SetTitle(patNum);
			//capture.ini, missing or corrupt, gives the defaults: the first camera and no trigger (design 8.9).
			LocalCaptureConfig localCaptureConfig=LocalCaptureConfig.Load();
			if(localCaptureConfig.LastError!="") {
				Log("capture.ini: "+localCaptureConfig.LastError+" The camera settings use their defaults.");
			}
			_videoCaptureSession=new VideoCaptureSession(new DirectShowUvcBackend(),localCaptureConfig,patNum);
			_videoCaptureSession.ActionLog=Log;
			_videoCaptureSession.FuncIsWindowEnabled=IsWindowEnabledForCapture;
			_videoCaptureSession.FrameReady+=videoCaptureSession_FrameReady;
			_videoCaptureSession.CameraError+=videoCaptureSession_CameraError;
			_videoCaptureSession.TriggerPressed+=videoCaptureSession_TriggerPressed;
			_videoCaptureSession.LevelChanged+=videoCaptureSession_LevelChanged;
			ODEvent.Fired+=ODEvent_Fired;
			ShowStatus(VideoStatusText.Starting());
		}

		private void FormVideo_Shown(object sender,EventArgs e) {
			//After the first paint, so the window shows "Starting the camera..." while the camera opens.
			RefreshCamera();
		}

		///<summary>ODEvent can be fired on any thread. A patient change closes the gate at once, so no photo is taken from here on,
		///and closes the window on the UI thread (design 8.10).</summary>
		private void ODEvent_Fired(ODEventArgs e) {
			VideoCaptureSession videoCaptureSession=_videoCaptureSession;
			if(videoCaptureSession==null || !videoCaptureSession.PatientBinding.IsPatientChange(e)) {
				return;
			}
			videoCaptureSession.Gate.BeginClosing();
			try {
				BeginInvoke((Action)CloseForPatientChange);
			}
			catch(Exception ex) {
				ex.DoNothing();//The window is already closing.
			}
		}

		///<summary>Streaming thread. Posts one preview update at a time.</summary>
		private void videoCaptureSession_FrameReady(object sender,EventArgs e) {
			if(Interlocked.Exchange(ref _isFramePosted,1)==1) {
				return;
			}
			try {
				BeginInvoke((Action)ShowNewFrame);
			}
			catch(Exception ex) {
				ex.DoNothing();//The window handle is gone: the window is closing.
				Interlocked.Exchange(ref _isFramePosted,0);
			}
		}

		///<summary>Thread-pool thread.</summary>
		private void videoCaptureSession_CameraError(object sender,UvcErrorEventArgs e) {
			try {
				BeginInvoke((Action)(() => HandleCameraError(sender,e)));
			}
			catch(Exception ex) {
				ex.DoNothing();//The window is closing; the camera is released there.
			}
		}

		private void videoCaptureSession_LevelChanged(object sender,LevelChangedEventArgs e) {
			_isPreviewPaused=e.IsPausePreviewActive;
			ShowStatus(_videoCaptureSession?.Status);
		}

		private void videoCaptureSession_TriggerPressed(object sender,TriggerPressEventArgs e) {
			RequestCapture(CaptureRequestSource.Trigger,e.StillFrame);
		}
		#endregion Methods - Event Handlers

		#region Methods
		///<summary>Space in the main window, passed on by ControlImages (by reflection, through FormLauncher). Takes a photo unless
		///capture.ini has [Camera] MainWindowSpace=0, because any Space typed in the main window triggers it (design 8.10).</summary>
		public void Parent_KeyDown(Keys keys){
			//because FormVideo_KeyDown would not be reliable enough on its own. This form might not have focus.
			if(keys==Keys.Space){
				RequestCapture(CaptureRequestSource.SpaceInMainWindow,null);
			}
		}

		///<summary>Asks the session for a photo and raises BitmapCaptured with it. The gate drops requests while the previous handler
		///runs (its message box can deliver more presses), while this window is disabled, while it closes, and without a camera or a
		///patient. The copy is disposed after the handler returns, also when it throws.</summary>
		private void RequestCapture(CaptureRequestSource captureRequestSource,UvcFrame frameStill) {
			VideoCaptureSession videoCaptureSession=_videoCaptureSession;
			if(videoCaptureSession==null) {
				return;//closed
			}
			CaptureStart captureStart=videoCaptureSession.BeginCapture(captureRequestSource,frameStill);
			if(!captureStart.IsAccepted) {
				ShowInfo(CaptureGate.GetDropText(captureStart.DropReason));
				return;
			}
			try {
				ShowInfo("");
				BitmapCaptured?.Invoke(this,captureStart.Bitmap);
			}
			finally {
				videoCaptureSession.EndCapture(captureStart);
			}
		}

		///<summary>False while this window cannot take input: disabled (for example under a modal window), not created, or closed.</summary>
		private bool IsWindowEnabledForCapture() {
			if(IsDisposed || Disposing || !IsHandleCreated || !Enabled) {
				return false;
			}
			return IsWindowEnabled(Handle);
		}

		///<summary>Lists the cameras again and opens one: the current camera when still present, else the one saved in capture.ini,
		///else the first. Also Reconnect after the camera was unplugged.</summary>
		private void RefreshCamera() {
			if(_videoCaptureSession==null || _videoCaptureSession.Gate.IsClosing) {
				return;
			}
			ShowStatus(VideoStatusText.Starting());
			VideoStatus videoStatus;
			Cursor=Cursors.WaitCursor;
			try {
				videoStatus=_videoCaptureSession.Refresh();
			}
			finally {
				Cursor=Cursors.Default;
			}
			_isPreviewPaused=false;
			FillComboCameras();
			ShowStatus(videoStatus);
		}

		private void FillComboCameras() {
			_isFillingCombo=true;
			try {
				comboCameras.Items.Clear();
				IReadOnlyList<UvcDeviceInfo> listDevices=_videoCaptureSession.Devices;
				List<string> listNames=VideoCaptureSession.GetDeviceNames(listDevices);
				int idxSelected=-1;
				for(int i=0;i<listDevices.Count;i++) {
					comboCameras.Items.Add(listNames[i],listDevices[i]);
					if(idxSelected==-1 && listDevices[i].IsSameDevice(_videoCaptureSession.Device)) {
						idxSelected=i;
					}
				}
				if(idxSelected>=0) {
					comboCameras.SelectedIndex=idxSelected;
				}
			}
			finally {
				_isFillingCombo=false;
			}
		}

		///<summary>UI thread. Shows the newest preview frame and disposes the bitmap it replaces, so no bitmap is shared with the
		///streaming thread and none is leaked (design 8.2).</summary>
		private void ShowNewFrame() {
			Interlocked.Exchange(ref _isFramePosted,0);
			if(IsDisposed || _videoCaptureSession==null || _isPreviewPaused) {
				return;
			}
			UvcFrame frame=_videoCaptureSession.TakePreviewFrame();
			if(frame==null) {
				return;
			}
			Bitmap bitmap;
			try {
				bitmap=frame.ToBitmap();
			}
			catch(Exception ex) {
				ex.DoNothing();//Out of memory for one frame: skip it; the next one tries again.
				return;
			}
			Image imageOld=pictureBoxCamera.Image;
			pictureBoxCamera.Image=bitmap;
			imageOld?.Dispose();
		}

		private void ClearPreview() {
			Image imageOld=pictureBoxCamera.Image;
			pictureBoxCamera.Image=null;
			imageOld?.Dispose();
		}

		private void HandleCameraError(object sender,UvcErrorEventArgs uvcErrorEventArgs) {
			if(IsDisposed || _videoCaptureSession==null) {
				return;
			}
			if(_isLearnOpen) {
				//Learn is using this camera and closes itself on the same error. Handle it when Learn has closed.
				_senderErrorPending=sender;
				_uvcErrorEventArgsPending=uvcErrorEventArgs;
				return;
			}
			VideoStatus videoStatus=_videoCaptureSession.HandleCameraError(sender,uvcErrorEventArgs);
			if(videoStatus==null) {
				return;//from a camera that is no longer open
			}
			_isPreviewPaused=false;
			ShowStatus(videoStatus);
		}

		private void CloseForPatientChange() {
			if(IsDisposed) {
				return;
			}
			if(_isLearnOpen) {
				_isCloseRequested=true;
				return;
			}
			Close();
		}

		///<summary>Stops the triggers and the graph and releases the camera's COM objects. Called when closing, and from Dispose.</summary>
		private void ReleaseCamera() {
			ODEvent.Fired-=ODEvent_Fired;
			VideoCaptureSession videoCaptureSession=_videoCaptureSession;
			_videoCaptureSession=null;
			if(videoCaptureSession!=null) {
				videoCaptureSession.Gate.BeginClosing();
				videoCaptureSession.FrameReady-=videoCaptureSession_FrameReady;
				videoCaptureSession.CameraError-=videoCaptureSession_CameraError;
				videoCaptureSession.TriggerPressed-=videoCaptureSession_TriggerPressed;
				videoCaptureSession.LevelChanged-=videoCaptureSession_LevelChanged;
				videoCaptureSession.Dispose();
			}
			if(!IsDisposed && pictureBoxCamera!=null) {
				ClearPreview();
			}
			_contextMenuSettings?.Dispose();
			_contextMenuSettings=null;
		}

		private void SetTitle(long patNum) {
			string title=Lan.g(this,"Video Capture");
			if(patNum==0) {
				Text=title+" - "+Lan.g(this,"no patient selected");
				return;
			}
			string patientName="";
			try {
				patientName=Patients.GetLim(patNum).GetNameFL();
			}
			catch(Exception ex) {
				ex.DoNothing();//The title is only a reminder; the binding is the PatNum.
			}
			Text=patientName=="" ? title : title+" - "+patientName;
		}

		///<summary>The band over the preview: the camera's problem and its Refresh or Reconnect button, "Starting the camera...", or
		///"Preview paused". Hidden while the preview runs normally.</summary>
		private void ShowStatus(VideoStatus videoStatus) {
			if(IsDisposed) {
				return;
			}
			if(videoStatus==null || videoStatus.IsStreaming) {
				labelStatus.Text=Lan.g(this,"Preview paused");
				labelStatus.Visible=_isPreviewPaused;
				butRefresh.Text=Lan.g(this,"Refresh");
				butCapture.Enabled=videoStatus!=null;
			}
			else {
				string text=Lan.g(this,videoStatus.Text);
				if(videoStatus.Detail!="") {
					text+=" "+videoStatus.Detail;
				}
				labelStatus.Text=text;
				labelStatus.Visible=true;
				butRefresh.Text=videoStatus.Action==VideoStatusAction.Reconnect ? Lan.g(this,"Reconnect") : Lan.g(this,"Refresh");
				butCapture.Enabled=false;
				if(videoStatus.IsProblem) {
					ClearPreview();
				}
			}
			labelStatus.Refresh();
		}

		///<summary>A short note under the preview, for example why a press did nothing. "" clears it.</summary>
		private void ShowInfo(string text) {
			if(IsDisposed) {
				return;
			}
			labelInfo.Text=string.IsNullOrEmpty(text) ? "" : Lan.g(this,text);
		}

		private static void Log(string line) {
			try {
				Logger.WriteLine(line,LOG_FOLDER);
			}
			catch(Exception ex) {
				ex.DoNothing();//Logging must never stop the camera.
			}
		}

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool IsWindowEnabled(IntPtr hWnd);
		#endregion Methods

		#region Methods - Settings
		///<summary>The settings (design 8.5, 8.8): the resolution, where photos come from, Space in the main window, Learn and the
		///button test, and a note for each trigger. There is no button for the camera's own property pages (design 8.7).</summary>
		private void ShowSettingsMenu() {
			VideoCaptureSession videoCaptureSession=_videoCaptureSession;
			if(videoCaptureSession==null) {
				return;
			}
			IFrameSource frameSource=videoCaptureSession.FrameSource;
			CameraConfig cameraConfig=videoCaptureSession.CameraConfig;
			_contextMenuSettings?.Dispose();
			ContextMenu contextMenu=new ContextMenu();
			_contextMenuSettings=contextMenu;
			MenuItem menuItemResolution=new MenuItem(Lan.g(this,"Resolution"));
			if(frameSource==null) {
				menuItemResolution.Enabled=false;
			}
			else {
				foreach(ResolutionChoice resolutionChoice in ResolutionChoice.GetChoices(frameSource.Modes,cameraConfig)) {
					UvcMode mode=resolutionChoice.Mode;
					MenuItem menuItem=new MenuItem(resolutionChoice.Text,(sender,e) => SetResolution(mode));
					menuItem.RadioCheck=true;
					menuItem.Checked=resolutionChoice.IsSelected;
					menuItemResolution.MenuItems.Add(menuItem);
				}
			}
			contextMenu.MenuItems.Add(menuItemResolution);
			MenuItem menuItemPhotoSource=new MenuItem(Lan.g(this,"Take photos from"));
			MenuItem menuItemPreview=new MenuItem(Lan.g(this,"The preview picture"),(sender,e) => SetPhotoSource(CameraPhotoSource.Preview));
			menuItemPreview.RadioCheck=true;
			menuItemPreview.Checked=cameraConfig.PhotoSource==CameraPhotoSource.Preview;
			menuItemPhotoSource.MenuItems.Add(menuItemPreview);
			MenuItem menuItemStill=new MenuItem(Lan.g(this,"The camera's still image, for still-pin camera buttons"),(sender,e) => SetPhotoSource(CameraPhotoSource.Still));
			menuItemStill.RadioCheck=true;
			menuItemStill.Checked=cameraConfig.PhotoSource==CameraPhotoSource.Still;
			menuItemStill.Enabled=frameSource!=null && frameSource.HasStillPin;
			menuItemPhotoSource.MenuItems.Add(menuItemStill);
			contextMenu.MenuItems.Add(menuItemPhotoSource);
			MenuItem menuItemSpace=new MenuItem(Lan.g(this,"Space in the main window takes a photo"),(sender,e) => SetMainWindowSpace(!cameraConfig.MainWindowSpace));
			menuItemSpace.Checked=cameraConfig.MainWindowSpace;
			contextMenu.MenuItems.Add(menuItemSpace);
			contextMenu.MenuItems.Add("-");
			MenuItem menuItemLearn=new MenuItem(Lan.g(this,"Learn camera buttons..."),(sender,e) => OpenLearn(CaptureLearnMode.Learn));
			menuItemLearn.Enabled=videoCaptureSession.IsStreaming;
			contextMenu.MenuItems.Add(menuItemLearn);
			MenuItem menuItemButtonTest=new MenuItem(Lan.g(this,"Camera button test..."),(sender,e) => OpenLearn(CaptureLearnMode.ButtonTest));
			menuItemButtonTest.Enabled=videoCaptureSession.IsStreaming;
			contextMenu.MenuItems.Add(menuItemButtonTest);
			contextMenu.MenuItems.Add("-");
			foreach(string line in videoCaptureSession.GetTriggerNotes()) {
				MenuItem menuItemNote=new MenuItem(line);
				menuItemNote.Enabled=false;
				contextMenu.MenuItems.Add(menuItemNote);
			}
			contextMenu.Show(butSettings,new Point(0,butSettings.Height));
		}

		private void SetResolution(UvcMode mode) {
			SaveSettings(x => ResolutionChoice.Apply(x,mode));
			ApplySettings();
		}

		private void SetPhotoSource(CameraPhotoSource cameraPhotoSource) {
			SaveSettings(x => x.PhotoSource=cameraPhotoSource);
			ApplySettings();
		}

		private void SetMainWindowSpace(bool isOn) {
			SaveSettings(x => x.MainWindowSpace=isOn);
		}

		private void SaveSettings(Action<CameraConfig> change) {
			if(_videoCaptureSession==null) {
				return;
			}
			string error;
			string warning;
			_videoCaptureSession.SaveSettings(change,out error,out warning);
			ShowSaveResult(error,warning);
		}

		///<summary>Under the preview: why the camera settings could not be saved, or the warning of a save that worked, for example
		///that they went to the per-user file and do not take effect the next time this window opens. Clears the note otherwise.</summary>
		private void ShowSaveResult(string error,string warning) {
			if(IsDisposed) {
				return;
			}
			if(!string.IsNullOrEmpty(error)) {
				labelInfo.Text=Lan.g(this,"The camera settings could not be saved.")+" "+error;
			}
			else if(!string.IsNullOrEmpty(warning)) {
				labelInfo.Text=Lan.g(this,"The camera settings were saved.")+" "+warning;
			}
			else {
				ShowInfo("");
			}
		}

		///<summary>Reopens the camera when the new settings need another stream format or the still pin, otherwise restarts the
		///triggers.</summary>
		private void ApplySettings() {
			if(_videoCaptureSession==null) {
				return;
			}
			bool isReopen=_videoCaptureSession.NeedsReopen();
			if(isReopen) {
				ShowStatus(VideoStatusText.Starting());
			}
			VideoStatus videoStatus;
			Cursor=Cursors.WaitCursor;
			try {
				videoStatus=_videoCaptureSession.ApplySettings();
			}
			finally {
				Cursor=Cursors.Default;
			}
			_isPreviewPaused=false;
			ShowStatus(videoStatus);
		}

		///<summary>Opens Learn or the button test (design 8.5) on the camera this window holds: only one program and one graph can
		///hold a camera. The triggers stop while it is open, so its key presses take no photo, and the camera is reopened with its
		///still pin first when it has one, so a still-pin key can be learned. Afterwards the triggers restart from capture.ini.</summary>
		private void OpenLearn(CaptureLearnMode captureLearnMode) {
			if(_videoCaptureSession==null || !_videoCaptureSession.IsStreaming) {
				return;
			}
			bool isSaved=false;
			_isLearnOpen=true;
			_isPreviewPaused=false;
			try {
				Cursor=Cursors.WaitCursor;
				VideoStatus videoStatus;
				try {
					videoStatus=_videoCaptureSession.PrepareForLearn();
				}
				finally {
					Cursor=Cursors.Default;
				}
				ShowStatus(videoStatus);
				IFrameSource frameSource=_videoCaptureSession.FrameSource;
				if(!videoStatus.IsStreaming || frameSource==null) {
					return;
				}
				FrmCaptureLearn frmCaptureLearn=new FrmCaptureLearn(frameSource,_videoCaptureSession.LocalCaptureConfig,captureLearnMode);
				frmCaptureLearn.ShowDialog();
				isSaved=captureLearnMode==CaptureLearnMode.Learn && frmCaptureLearn.IsDialogOK && frmCaptureLearn.CameraConfigSaved!=null;
			}
			finally {
				_isLearnOpen=false;
				FinishLearn(isSaved);
			}
			if(_isCloseRequested && !IsDisposed) {
				Close();
			}
		}

		private void FinishLearn(bool isSaved) {
			if(IsDisposed || _videoCaptureSession==null) {
				return;
			}
			if(_uvcErrorEventArgsPending!=null) {
				object senderError=_senderErrorPending;
				UvcErrorEventArgs uvcErrorEventArgs=_uvcErrorEventArgsPending;
				_senderErrorPending=null;
				_uvcErrorEventArgsPending=null;
				HandleCameraError(senderError,uvcErrorEventArgs);
			}
			if(_isCloseRequested) {
				return;
			}
			VideoStatus videoStatus;
			Cursor=Cursors.WaitCursor;
			try {
				videoStatus=_videoCaptureSession.FinishLearn(isSaved);
			}
			finally {
				Cursor=Cursors.Default;
			}
			ShowStatus(videoStatus);
		}
		#endregion Methods - Settings
	}
}
