// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ImagingCapture.Session;
using ImagingDeviceManager.Capture;
using OpenDental;
using OpenDentBusiness;

namespace ImagingCapture.UI {
	///<summary>The capture session window (design 7.3, 7.5, 7.12, 10.2, 10.3): modal, bound to one patient. The banner shows the patient, the
	///provider (editable before Start) and the target mount, or the tooth picker without a mount. The operator starts with the "Start X-rays for
	///&lt;name&gt;" button or the Confirm key held about 1 s; the indicator area shows the controller's state and its notices; the slot mini-map shows
	///the mount's slots in ItemOrder with the target; the last image shows briefly with its display rotation; the operator's commands are buttons,
	///keys and pedals; the summary shows at the end and the window closes by itself.
	///
	///All the logic is in CaptureSessionController and CaptureSessionViewModel; this window shows the view model and passes on clicks, keys,
	///hotkeys and its timer. No message boxes: everything shows in the indicator area. While the source is enabled the [Keys] session keys are
	///registered as hotkeys on this window's handle (an HwndSource hook receives WM_HOTKEY) and released when it is no longer enabled and when the
	///window closes. FormClosing takes the controller's forced-close path, which never waits for the worker. The window's X while the source is
	///enabled is End instead (with the warning first when disabling is not safe).
	///
	///Hooks for CAP-4 implementer C (partial methods below): the "N unassigned captures on this computer" link opens the quarantine window, and
	///"Recover recent captures" opens the recover-from-journal view. Without an implementation the notice is plain text and the Recover link is
	///hidden.</summary>
	public partial class FrmCaptureSession:FrmODBase {
		#region Fields
		///<summary>How often the window gives the controller its tick (the held Confirm key, the 2 s arming, the idle timer) and refreshes.</summary>
		private const int TickMs=100;
		private const double ToothTileSize=30;
		private static readonly Color _colorWindow=Color.FromRgb(252,253,254);
		private static readonly Color _colorBlack=Color.FromRgb(0,0,0);
		private static readonly Color _colorWhite=Color.FromRgb(255,255,255);
		private readonly CaptureSessionViewModel _viewModel;
		private DispatcherTimer _dispatcherTimer;
		private HwndSource _hwndSource;
		private HwndSourceHook _hwndSourceHook;
		private bool _isClosed;
		private bool _isFillingProviders;
		private int _versionShown=-1;
		private string _slotsShown;
		private string _teethShown;
		private AcquiredImage _imagePreviewShown;
		private int _degreesPreviewShown;
		private bool _isFlippedPreviewShown;
		private readonly List<Border> _listBordersTeeth=new List<Border>();
		private string _viewErrorLogged;
		#endregion Fields

		#region Constructor
		///<summary>controller: made by CaptureSessionLauncher, not yet begun. Show the window with ShowDialog (CaptureSessionWindowRunner).</summary>
		public FrmCaptureSession(CaptureSessionController controller) {
			InitializeComponent();
			_viewModel=new CaptureSessionViewModel(controller);
			EscClosesWindow=false;//Esc must never end a session
			MinMaxBoxes=false;
			//The window itself takes the keyboard focus (its buttons cannot), so that the Confirm key and the session keys reach PreviewKeyDown.
			Focusable=true;
			FocusVisualStyle=null;
			Load+=FrmCaptureSession_Load;
			Shown+=FrmCaptureSession_Shown;
			FormClosing+=FrmCaptureSession_FormClosing;
			FormClosed+=FrmCaptureSession_FormClosed;
			PreviewKeyDown+=FrmCaptureSession_PreviewKeyDown;
			PreviewKeyUp+=FrmCaptureSession_PreviewKeyUp;
			comboProv.SelectionChangeCommitted+=comboProv_SelectionChangeCommitted;
			linkUnassigned.LinkClicked+=linkUnassigned_LinkClicked;
			linkRecover.LinkClicked+=linkRecover_LinkClicked;
			textTeeth.LostKeyboardFocus+=textTeeth_LostKeyboardFocus;
		}
		#endregion Constructor

		#region Hooks for CAP-4 implementer C
		///<summary>Implementer C: the quarantine window (design 7.12 "Visibility", "Assign", "Reassign", "Discard") can be opened from here; sets
		///canShow. Not implemented: the unassigned-captures notice stays plain text.</summary>
		partial void CanShowUnassignedCapturesExt(ref bool canShow);
		///<summary>Implementer C: opens the quarantine window modal to this one and sets isShown. Called only while the source is not enabled
		///(CaptureSessionViewModel.CanOpenCaptureLinks); the count is read again afterwards.</summary>
		partial void ShowUnassignedCapturesExt(CaptureSessionController controller,ref bool isShown);
		///<summary>Implementer C: the recover-from-journal view ("Recover recent captures", design 12.1 CAP-4) can be opened from here.</summary>
		partial void CanShowRecoverCapturesExt(ref bool canShow);
		///<summary>Implementer C: opens the recover-from-journal view modal to this one and sets isShown, under the same rule as the quarantine.</summary>
		partial void ShowRecoverCapturesExt(CaptureSessionController controller,ref bool isShown);
		#endregion Hooks for CAP-4 implementer C

		#region Methods - public
		///<summary>Releases every hotkey. The window does this when it closes; the runner calls it again after ShowDialog, for a failure path.</summary>
		public void ReleaseHotkeys() {
			try {
				_viewModel.ReleaseHotkeys();
			}
			catch {
				//nothing is registered any more, or the handle is gone
			}
		}
		#endregion Methods - public

		#region Methods - Event Handlers
		private void FrmCaptureSession_Load(object sender,EventArgs e) {
			Lang.F(this);
			Text=_viewModel.WindowTitle;
			_viewModel.Changed+=viewModel_Changed;
			if(_formFrame!=null) {
				_formFrame.CloseXClicked+=formFrame_CloseXClicked;
			}
			FillProviders();
			ShowPatientPicture();
			if(_viewModel.IsToothPickerVisible) {
				BuildTeeth();
			}
			AttachHotkeyWindow();
			_viewModel.Begin();
			_dispatcherTimer=new DispatcherTimer();
			_dispatcherTimer.Interval=TimeSpan.FromMilliseconds(TickMs);
			_dispatcherTimer.Tick+=dispatcherTimer_Tick;
			_dispatcherTimer.Start();
			RefreshView();
		}

		private void FrmCaptureSession_Shown(object sender,EventArgs e) {
			if(_hwndSource==null) {
				AttachHotkeyWindow();//the hosting window had no handle at Load
			}
			Focus();//not the tooth numbers box: keys and pedals go to the session until the operator clicks into it
		}

		private void FrmCaptureSession_FormClosing(object sender,CancelEventArgs e) {
			//Never cancelled: a forced close (a logoff) must close. The controller stops the source in the safe order on its worker and does not wait.
			_isClosed=true;
			_dispatcherTimer?.Stop();
			try {
				_viewModel.OnClosing();
			}
			finally {
				DetachHotkeyWindow();
				_viewModel.Changed-=viewModel_Changed;
			}
		}

		private void FrmCaptureSession_FormClosed(object sender,EventArgs e) {
			_isClosed=true;
			_dispatcherTimer?.Stop();
			ReleaseHotkeys();
		}

		///<summary>The window's X: End while the source is enabled (the window stays until the session finishes), otherwise a close.</summary>
		private void formFrame_CloseXClicked(object sender,CancelEventArgs e) {
			if(_isClosed) {
				return;
			}
			if(!_viewModel.OnCloseRequestedByOperator()) {
				e.Cancel=true;
				RefreshView();
			}
		}

		private void dispatcherTimer_Tick(object sender,EventArgs e) {
			if(_isClosed) {
				return;
			}
			try {
				_viewModel.OnTick();
				RefreshView();
			}
			catch(Exception ex) {
				LogViewError(ex);//a display fault must not stop a session that may be waiting for an exposure
			}
			if(_viewModel.IsCloseDue) {
				Close();
			}
		}

		private void viewModel_Changed() {
			if(_isClosed) {
				return;
			}
			try {
				RefreshView();
			}
			catch(Exception ex) {
				LogViewError(ex);
			}
		}

		private void FrmCaptureSession_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(_isClosed) {
				return;
			}
			if(IsTypingTeeth(e)) {
				if(e.Key==Key.Enter) {
					ApplyTeethText();
					e.Handled=true;
				}
				return;//typing tooth numbers, not a session key
			}
			int virtualKey=GetVirtualKey(e);
			if(virtualKey==0) {
				return;
			}
			if(_viewModel.OnKeyDown(virtualKey,(CaptureKeyModifiers)(int)Keyboard.Modifiers,e.IsRepeat)) {
				e.Handled=true;//a bound key never reaches a focused control
				RefreshView();
			}
		}

		private void FrmCaptureSession_PreviewKeyUp(object sender,KeyEventArgs e) {
			if(_isClosed || IsTypingTeeth(e)) {
				return;
			}
			int virtualKey=GetVirtualKey(e);
			if(virtualKey==0) {
				return;
			}
			if(_viewModel.OnKeyUp(virtualKey)) {
				e.Handled=true;
				RefreshView();
			}
		}

		private void comboProv_SelectionChangeCommitted(object sender,EventArgs e) {
			if(_isFillingProviders) {
				return;
			}
			if(!_viewModel.SetProvider(comboProv.GetSelectedProvNum())) {
				SelectProvider();
			}
			RefreshView();
		}

		private void textTeeth_LostKeyboardFocus(object sender,KeyboardFocusChangedEventArgs e) {
			ApplyTeethText();
		}

		private void linkUnassigned_LinkClicked(object sender,EventArgs e) {
			if(_isClosed || !_viewModel.CanOpenCaptureLinks || !CanShowUnassigned()) {
				return;
			}
			bool isShown=false;
			ShowUnassignedCapturesExt(_viewModel.Controller,ref isShown);
			if(!isShown) {
				return;
			}
			_viewModel.RefreshUnassignedText();
			RefreshView();
		}

		private void linkRecover_LinkClicked(object sender,EventArgs e) {
			if(_isClosed || !_viewModel.CanOpenCaptureLinks || !CanShowRecover()) {
				return;
			}
			bool isShown=false;
			ShowRecoverCapturesExt(_viewModel.Controller,ref isShown);
			if(!isShown) {
				return;
			}
			_viewModel.RefreshUnassignedText();
			RefreshView();
		}

		private void butStart_Click(object sender,EventArgs e) {
			_viewModel.RequestStart();
			RefreshView();
		}

		private void butUseMount_Click(object sender,EventArgs e) {
			_viewModel.ConfirmOlderMount();
			RefreshView();
		}

		private void butDeclineMount_Click(object sender,EventArgs e) {
			_viewModel.DeclineOlderMount();
			RefreshView();
		}

		private void butRequestRedelivery_Click(object sender,EventArgs e) {
			_viewModel.RequestRedelivery();
			RefreshView();
		}

		private void butConfirmRedelivered_Click(object sender,EventArgs e) {
			_viewModel.ConfirmRedelivered();
			RefreshView();
		}

		private void butDeclineRedelivered_Click(object sender,EventArgs e) {
			_viewModel.DeclineRedelivered();
			RefreshView();
		}

		private void butRetake_Click(object sender,EventArgs e) {
			ExecuteCommand(CaptureSessionAction.Retake);
		}

		private void butSkip_Click(object sender,EventArgs e) {
			ExecuteCommand(CaptureSessionAction.Skip);
		}

		private void butPrevious_Click(object sender,EventArgs e) {
			ExecuteCommand(CaptureSessionAction.PreviousSlot);
		}

		private void butNext_Click(object sender,EventArgs e) {
			ExecuteCommand(CaptureSessionAction.NextSlot);
		}

		private void butRotate_Click(object sender,EventArgs e) {
			ExecuteCommand(CaptureSessionAction.Rotate);
		}

		private void butAlwaysRotate_Click(object sender,EventArgs e) {
			_viewModel.AlwaysRotateSlot();
			RefreshView();
		}

		private void butDisable_Click(object sender,EventArgs e) {
			ExecuteCommand(CaptureSessionAction.Disable);
		}

		private void butEnd_Click(object sender,EventArgs e) {
			ExecuteCommand(CaptureSessionAction.End);
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}

		private void borderSlot_MouseLeftButtonUp(object sender,MouseButtonEventArgs e) {
			if(!(sender is Border border) || !(border.Tag is int idx)) {
				return;
			}
			_viewModel.SelectSlot(idx);
			RefreshView();
		}

		private void borderTooth_MouseLeftButtonUp(object sender,MouseButtonEventArgs e) {
			if(!(sender is Border border) || !(border.Tag is string universal) || !_viewModel.IsToothPickerEnabled) {
				return;
			}
			_viewModel.ToggleTooth(universal);
			textTeeth.Text=_viewModel.ToothNumbersText;
			RefreshView();
		}

		private IntPtr hwndSource_Hook(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled) {
			if(msg!=Win32HotkeyRegistrar.WM_HOTKEY || _isClosed) {
				return IntPtr.Zero;
			}
			try {
				if(_viewModel.OnHotkey(wParam.ToInt32())) {
					handled=true;
					RefreshView();
				}
			}
			catch(Exception ex) {
				LogViewError(ex);//never let an exception out of a window procedure
			}
			return IntPtr.Zero;
		}
		#endregion Methods - Event Handlers

		#region Methods - setup
		///<summary>The hotkeys register on the handle of the window that hosts this one's content, whose hook receives WM_HOTKEY.</summary>
		private void AttachHotkeyWindow() {
			if(_hwndSource!=null) {
				return;
			}
			try {
				_hwndSource=PresentationSource.FromVisual(this) as HwndSource;
				if(_hwndSource==null || _hwndSource.Handle==IntPtr.Zero) {
					_hwndSource=null;
					return;
				}
				_hwndSourceHook=hwndSource_Hook;
				_hwndSource.AddHook(_hwndSourceHook);
				_viewModel.SetHotkeyRegistrar(new Win32HotkeyRegistrar(_hwndSource.Handle));
			}
			catch(Exception ex) {
				_hwndSource=null;
				_viewModel.Controller.LogFromWindow("The hotkeys are not available: "+ex.Message);
			}
		}

		private void DetachHotkeyWindow() {
			try {
				_viewModel.SetHotkeyRegistrar(null);//releases every hotkey on the old handle
			}
			catch(Exception ex) {
				_viewModel.Controller.LogFromWindow("The hotkeys could not be released: "+ex.Message);
			}
			try {
				if(_hwndSource!=null && _hwndSourceHook!=null) {
					_hwndSource.RemoveHook(_hwndSourceHook);
				}
			}
			catch {
				//the source is already disposed
			}
			_hwndSource=null;
		}

		private void FillProviders() {
			_isFillingProviders=true;
			try {
				comboProv.Items.Clear();
				comboProv.Items.AddProvNone();
				comboProv.Items.AddProvsAbbr(Providers.GetDeepCopy(isShort:true));
				SelectProvider();
			}
			catch(Exception ex) {
				_viewModel.Controller.LogFromWindow("The providers could not be listed: "+ex.Message);
			}
			finally {
				_isFillingProviders=false;
			}
		}

		private void SelectProvider() {
			bool isFilling=_isFillingProviders;
			_isFillingProviders=true;
			try {
				comboProv.SetSelectedProvNum(_viewModel.ProvNum);
			}
			finally {
				_isFillingProviders=isFilling;
			}
		}

		///<summary>The patient picture (Documents.GetPatPict, as the Family module shows it), or nothing.</summary>
		private void ShowPatientPicture() {
			try {
				Patient patient=_viewModel.Controller.Patient;
				string patFolder=ImageStore.GetPatientFolder(patient,ImageStore.GetPreferredAtoZpath());
				using(System.Drawing.Bitmap bitmap=Documents.GetPatPict(patient.PatNum,patFolder)) {
					imagePatient.Source=bitmap==null ? null : OpenDental.Drawing.Graphics.ConvertBitmapToWpf(bitmap);
				}
			}
			catch(Exception ex) {
				imagePatient.Source=null;
				_viewModel.Controller.LogFromWindow("The patient picture could not be shown: "+ex.Message);
			}
		}

		///<summary>The tooth picker's 32 tiles, maxillary 1-16 above mandibular 32-17.</summary>
		private void BuildTeeth() {
			canvasTeeth.Children.Clear();
			_listBordersTeeth.Clear();
			foreach(CaptureToothTile tile in _viewModel.GetToothTiles()) {
				TextBlock textBlock=new TextBlock { Text=tile.Universal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center };
				Border border=new Border {
					Width=ToothTileSize,
					Height=ToothTileSize,
					BorderBrush=Brushes.DarkGray,
					BorderThickness=new Thickness(1),
					Child=textBlock,
					Tag=tile.Universal,
					Cursor=Cursors.Hand,
				};
				Canvas.SetLeft(border,4+tile.Column*(ToothTileSize+2));
				Canvas.SetTop(border,4+tile.Row*(ToothTileSize+4));
				border.MouseLeftButtonUp+=borderTooth_MouseLeftButtonUp;
				canvasTeeth.Children.Add(border);
				_listBordersTeeth.Add(border);
			}
			_teethShown=null;
		}

		private void ApplyTeethText() {
			if(!_viewModel.IsToothPickerVisible || !_viewModel.IsToothPickerEnabled) {
				return;
			}
			if(textTeeth.Text!=_viewModel.ToothNumbersText) {
				_viewModel.SetToothNumbersText(textTeeth.Text);
			}
			if(_viewModel.ToothErrorText=="") {
				textTeeth.Text=_viewModel.ToothNumbersText;
			}
			RefreshView();
		}
		#endregion Methods - setup

		#region Methods - view
		private void RefreshView() {
			if(_isClosed) {
				return;
			}
			CaptureSessionViewModel viewModel=_viewModel;
			if(viewModel.Version==_versionShown) {
				return;
			}
			_versionShown=viewModel.Version;
			//Banner
			SetText(labelPatientName,viewModel.PatientName);
			SetText(labelPatientDetails,viewModel.PatientDetailsText);
			comboProv.IsEnabled=viewModel.CanEditProvider;
			if(!viewModel.CanEditProvider && comboProv.GetSelectedProvNum()!=viewModel.ProvNum) {
				SelectProvider();
			}
			labelMount.Visible=viewModel.IsMountVisible;
			SetText(labelMount,viewModel.MountText);
			//Start, the older-mount question, redelivery
			butStart.Visible=viewModel.IsStartVisible;
			butStart.IsEnabled=viewModel.IsStartEnabled;
			SetButtonText(butStart,viewModel.StartText);
			SetText(labelStartHint,viewModel.StartHintText);
			butUseMount.Visible=viewModel.IsOlderMountQuestionVisible;
			butDeclineMount.Visible=viewModel.IsOlderMountQuestionVisible;
			butRequestRedelivery.Visible=viewModel.IsRequestRedeliveryVisible;
			butRequestRedelivery.IsEnabled=viewModel.IsRequestRedeliveryEnabled;
			SetButtonText(butRequestRedelivery,viewModel.RequestRedeliveryText);
			RefreshLinks();
			//Indicator area
			ShowLine(labelPatientChanged,viewModel.PatientChangedText);
			SetText(labelIndicator,viewModel.IndicatorText);
			SetTone(labelIndicator,viewModel.IndicatorTone);
			ShowLine(labelTarget,viewModel.TargetText);
			ShowLine(labelNextTarget,viewModel.NextTargetText);
			ShowLine(labelRedStrip,viewModel.RedStripText);
			ShowLine(labelWarning,viewModel.WarningText);
			ShowLine(labelNotice,viewModel.NoticeText);
			ShowLine(labelSummary,viewModel.SummaryText);
			//Slots or teeth
			labelSlots.Visible=viewModel.IsSlotMapVisible;
			borderSlots.Visibility=viewModel.IsSlotMapVisible ? Visibility.Visible : Visibility.Collapsed;
			if(viewModel.IsSlotMapVisible) {
				RefreshSlots();
			}
			labelTeethPicker.Visible=viewModel.IsToothPickerVisible;
			borderTeeth.Visibility=viewModel.IsToothPickerVisible ? Visibility.Visible : Visibility.Collapsed;
			labelTeeth.Visible=viewModel.IsToothPickerVisible;
			textTeeth.Visible=viewModel.IsToothPickerVisible;
			labelToothError.Visible=viewModel.IsToothPickerVisible;
			if(viewModel.IsToothPickerVisible) {
				RefreshTeeth();
			}
			//Preview and the redelivered image
			RefreshPreview();
			bool isRedelivered=viewModel.IsRedeliveredVisible;
			labelRedelivered.Visible=isRedelivered;
			labelRedeliveredDetails.Visible=isRedelivered;
			labelRedeliveredCount.Visible=isRedelivered;
			butConfirmRedelivered.Visible=isRedelivered;
			butDeclineRedelivered.Visible=isRedelivered;
			SetText(labelRedelivered,viewModel.RedeliveredText);
			SetText(labelRedeliveredDetails,viewModel.RedeliveredDetailsText);
			SetText(labelRedeliveredCount,viewModel.RedeliveredCountText);
			butConfirmRedelivered.IsEnabled=viewModel.IsConfirmRedeliveredEnabled;
			butDeclineRedelivered.IsEnabled=viewModel.IsDeclineRedeliveredEnabled;
			//Commands
			RefreshCommand(butRetake,CaptureSessionAction.Retake);
			RefreshCommand(butSkip,CaptureSessionAction.Skip);
			RefreshCommand(butPrevious,CaptureSessionAction.PreviousSlot);
			RefreshCommand(butNext,CaptureSessionAction.NextSlot);
			RefreshCommand(butRotate,CaptureSessionAction.Rotate);
			RefreshCommand(butDisable,CaptureSessionAction.Disable);
			RefreshCommand(butEnd,CaptureSessionAction.End);
			butAlwaysRotate.Visible=viewModel.IsAlwaysRotateVisible;
			butAlwaysRotate.IsEnabled=viewModel.IsAlwaysRotateEnabled;
			butClose.Visible=viewModel.IsSummaryVisible;
		}

		private void RefreshLinks() {
			string unassigned=_viewModel.UnassignedText;
			linkUnassigned.Visible=unassigned!="";
			if(linkUnassigned.Text!=unassigned) {
				linkUnassigned.Text=unassigned;
			}
			int linkLength=CanShowUnassigned() && _viewModel.CanOpenCaptureLinks ? unassigned.Length : 0;
			if(linkUnassigned.LinkLength!=linkLength) {
				linkUnassigned.LinkLength=linkLength;
			}
			linkUnassigned.ColorBack=_colorWindow;
			bool canRecover=CanShowRecover();
			linkRecover.Visible=canRecover;
			int linkLengthRecover=canRecover && _viewModel.CanOpenCaptureLinks ? linkRecover.Text.Length : 0;
			if(linkRecover.LinkLength!=linkLengthRecover) {
				linkRecover.LinkLength=linkLengthRecover;
			}
		}

		private bool CanShowUnassigned() {
			bool canShow=false;
			CanShowUnassignedCapturesExt(ref canShow);
			return canShow;
		}

		private bool CanShowRecover() {
			bool canShow=false;
			CanShowRecoverCapturesExt(ref canShow);
			return canShow;
		}

		private void RefreshCommand(WpfControls.UI.Button button,CaptureSessionAction action) {
			button.Visible=_viewModel.IsCommandVisible(action);
			button.IsEnabled=_viewModel.IsCommandEnabled(action);
			SetButtonText(button,_viewModel.GetCommandText(action));
		}

		///<summary>The mini-map, rebuilt when a tile changed.</summary>
		private void RefreshSlots() {
			double width=borderSlots.Width-2;
			double height=borderSlots.Height-2;
			List<CaptureSlotTile> listTiles=_viewModel.GetSlotTiles(width,height);
			string slots=string.Join(";",listTiles.Select(x => x.Idx+"|"+x.State+"|"+x.IsEnableTarget+"|"+x.IsClickable+"|"+x.Left.ToString("0")
				+"|"+x.Top.ToString("0")));
			if(slots==_slotsShown) {
				return;
			}
			_slotsShown=slots;
			canvasSlots.Children.Clear();
			foreach(CaptureSlotTile tile in listTiles) {
				canvasSlots.Children.Add(CreateSlotBorder(tile));
			}
		}

		private Border CreateSlotBorder(CaptureSlotTile tile) {
			Color colorBack;
			Color colorText=_colorBlack;
			Color colorBorder=Color.FromRgb(120,120,120);
			double thickness=1;
			switch(tile.State) {
				case CaptureSlotTileState.Filled:
					colorBack=Color.FromRgb(78,122,78);
					colorText=_colorWhite;
					break;
				case CaptureSlotTileState.Target:
					colorBack=Color.FromRgb(255,242,168);
					colorBorder=Color.FromRgb(224,160,0);
					thickness=3;
					break;
				case CaptureSlotTileState.Skipped:
					colorBack=Color.FromRgb(240,192,128);
					break;
				default:
					colorBack=Color.FromRgb(200,206,212);
					break;
			}
			if(tile.IsEnableTarget) {
				colorBorder=Color.FromRgb(32,176,64);//the running enable's slot: its image goes here
				thickness=3;
			}
			StackPanel stackPanel=new StackPanel { HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center };
			stackPanel.Children.Add(new TextBlock { Text=tile.Number,FontWeight=FontWeights.Bold,Foreground=new SolidColorBrush(colorText),
				HorizontalAlignment=HorizontalAlignment.Center });
			if(tile.TeethText!="" && tile.Height>=32) {
				stackPanel.Children.Add(new TextBlock { Text=tile.TeethText,FontSize=10,Foreground=new SolidColorBrush(colorText),
					HorizontalAlignment=HorizontalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=Math.Max(4,tile.Width-4) });
			}
			Border border=new Border {
				Width=tile.Width,
				Height=tile.Height,
				Background=new SolidColorBrush(colorBack),
				BorderBrush=new SolidColorBrush(colorBorder),
				BorderThickness=new Thickness(thickness),
				Child=stackPanel,
				Tag=tile.Idx,
				ToolTip=tile.ToolTip==""? null : tile.ToolTip,
				Cursor=tile.IsClickable ? Cursors.Hand : Cursors.Arrow,
			};
			Canvas.SetLeft(border,tile.Left);
			Canvas.SetTop(border,tile.Top);
			if(tile.IsClickable) {
				border.MouseLeftButtonUp+=borderSlot_MouseLeftButtonUp;
			}
			return border;
		}

		private void RefreshTeeth() {
			List<CaptureToothTile> listTiles=_viewModel.GetToothTiles();
			bool isEnabled=_viewModel.IsToothPickerEnabled;
			string teeth=isEnabled+"|"+string.Join(",",listTiles.Where(x => x.IsSelected).Select(x => x.Universal));
			textTeeth.ReadOnly=!isEnabled;
			SetText(labelToothError,_viewModel.ToothErrorText);
			if(!textTeeth.IsKeyboardFocusWithin && textTeeth.Text!=_viewModel.ToothNumbersText && _viewModel.ToothErrorText=="") {
				textTeeth.Text=_viewModel.ToothNumbersText;
			}
			if(teeth==_teethShown) {
				return;
			}
			_teethShown=teeth;
			for(int i=0;i<_listBordersTeeth.Count && i<listTiles.Count;i++) {
				Border border=_listBordersTeeth[i];
				bool isSelected=listTiles[i].IsSelected;
				border.Background=new SolidColorBrush(isSelected ? Color.FromRgb(28,81,128) : _colorWhite);
				if(border.Child is TextBlock textBlock) {
					textBlock.Foreground=new SolidColorBrush(isSelected ? _colorWhite : _colorBlack);
				}
				border.Opacity=isEnabled ? 1 : 0.6;
				border.Cursor=isEnabled ? Cursors.Hand : Cursors.Arrow;
			}
		}

		///<summary>The preview: the picture when the image changed, the display transform (mirrored first, then turned, as ImageHelper draws a
		///document) when the rotation or the flip changed, and the highlight while the image is fresh.</summary>
		private void RefreshPreview() {
			SetText(labelPreview,_viewModel.PreviewCaption);
			AcquiredImage image=_viewModel.PreviewImage;
			int degrees=_viewModel.PreviewDegrees;
			bool isFlipped=_viewModel.IsPreviewFlipped;
			if(image!=_imagePreviewShown) {
				_imagePreviewShown=image;
				string error;
				CapturePreviewPixels pixels=_viewModel.GetPreviewPixels(out error);
				imagePreview.Source=pixels==null ? null : CreateBitmapSource(pixels);
				if(pixels==null && image!=null && error!="") {
					_viewModel.Controller.LogFromWindow(error);
				}
				_degreesPreviewShown=-1;
			}
			if(degrees!=_degreesPreviewShown || isFlipped!=_isFlippedPreviewShown) {
				_degreesPreviewShown=degrees;
				_isFlippedPreviewShown=isFlipped;
				TransformGroup transformGroup=new TransformGroup();
				if(isFlipped) {
					transformGroup.Children.Add(new ScaleTransform(-1,1));
				}
				transformGroup.Children.Add(new RotateTransform(degrees));
				imagePreview.LayoutTransform=transformGroup;
			}
			bool isFresh=_viewModel.IsPreviewFresh;
			borderPreview.BorderBrush=new SolidColorBrush(isFresh ? Color.FromRgb(32,176,64) : Color.FromRgb(169,169,169));
			borderPreview.BorderThickness=new Thickness(isFresh ? 4 : 2);
		}

		private static BitmapSource CreateBitmapSource(CapturePreviewPixels pixels) {
			try {
				PixelFormat pixelFormat=pixels.IsGray ? PixelFormats.Gray8 : PixelFormats.Bgr24;
				BitmapSource bitmapSource=BitmapSource.Create(pixels.Width,pixels.Height,96,96,pixelFormat,null,pixels.Pixels,pixels.Stride);
				bitmapSource.Freeze();
				return bitmapSource;
			}
			catch {
				return null;
			}
		}

		private static void SetTone(WpfControls.UI.Label label,CaptureIndicatorTone tone) {
			Color colorBack;
			Color colorText=Color.FromRgb(0,0,0);
			switch(tone) {
				case CaptureIndicatorTone.Busy:
					colorBack=Color.FromRgb(255,244,192);
					break;
				case CaptureIndicatorTone.Waiting:
					colorBack=Color.FromRgb(46,139,87);
					colorText=Color.FromRgb(255,255,255);
					break;
				case CaptureIndicatorTone.Good:
					colorBack=Color.FromRgb(184,232,184);
					break;
				case CaptureIndicatorTone.Warning:
					colorBack=Color.FromRgb(255,216,160);
					break;
				case CaptureIndicatorTone.Error:
					colorBack=Color.FromRgb(192,0,0);
					colorText=Color.FromRgb(255,255,255);
					break;
				default:
					colorBack=Color.FromRgb(232,238,244);
					break;
			}
			if(label.ColorBack!=colorBack) {
				label.ColorBack=colorBack;
			}
			if(label.ColorText!=colorText) {
				label.ColorText=colorText;
			}
		}

		///<summary>A line of the indicator area: shown with its text, or collapsed when there is none.</summary>
		private static void ShowLine(WpfControls.UI.Label label,string text) {
			text=text??"";
			label.Visible=text!="";
			SetText(label,text);
		}

		private static void SetText(WpfControls.UI.Label label,string text) {
			text=text??"";
			if(label.Text!=text) {
				label.Text=text;
			}
		}

		private static void SetButtonText(WpfControls.UI.Button button,string text) {
			text=text??"";
			if(button.Text!=text) {
				button.Text=text;
			}
		}

		///<summary>The tooth numbers box has the focus and the key is one that types into it (digits, letters, comma, editing keys) without Ctrl or
		///Alt. Every other key, a function key or Pause for example, still goes to the session.</summary>
		private bool IsTypingTeeth(KeyEventArgs e) {
			if(!textTeeth.IsKeyboardFocusWithin || (Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Alt))!=0) {
				return false;
			}
			Key key=e.Key;
			if((key>=Key.D0 && key<=Key.D9) || (key>=Key.NumPad0 && key<=Key.NumPad9) || (key>=Key.A && key<=Key.Z)) {
				return true;
			}
			switch(key) {
				case Key.OemComma:
				case Key.Space:
				case Key.Back:
				case Key.Delete:
				case Key.Left:
				case Key.Right:
				case Key.Home:
				case Key.End:
				case Key.Tab:
				case Key.Enter:
				case Key.LeftShift:
				case Key.RightShift:
					return true;
				default:
					return false;
			}
		}

		private static int GetVirtualKey(KeyEventArgs e) {
			Key key=e.Key;
			if(key==Key.System) {
				key=e.SystemKey;
			}
			else if(key==Key.ImeProcessed) {
				key=e.ImeProcessedKey;
			}
			else if(key==Key.DeadCharProcessed) {
				key=e.DeadCharProcessedKey;
			}
			if(key==Key.None) {
				return 0;
			}
			return KeyInterop.VirtualKeyFromKey(key);
		}

		private void ExecuteCommand(CaptureSessionAction action) {
			_viewModel.Execute(action);
			RefreshView();
		}

		///<summary>An exception while showing the session: logged once per message, never shown in a message box (design 7.3).</summary>
		private void LogViewError(Exception ex) {
			string text="The capture session window could not show its state: "+ex.Message;
			if(text==_viewErrorLogged) {
				return;
			}
			_viewErrorLogged=text;
			try {
				_viewModel.Controller.LogFromWindow(text+" "+ex.StackTrace);
			}
			catch {
			}
		}
		#endregion Methods - view
	}
}
