// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;//Task itself is named in full: OpenDentBusiness has a Task table type
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodeBase;
using ImagingCapture.Metadata;
using ImagingCapture.Settings;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using WpfControls.UI;

namespace ImagingCapture.UI {
	///<summary>Test capture (design 7.8, 9.3, 13 step 8), opened from FormImagingDeviceEdit for one imaging device. It takes a test image of a
	///phantom or a known ramp through TwainCaptureSession, with no patient, and shows the effective device settings and their gated values,
	///the delivered bit depth, a histogram, the measured range and effective bits, the byte-order check of a 16-bit image, and the 'P'
	///warning for the X-ray category. The operator confirms byte order and polarity, and Write verification record stores the record
	///(Setup permission) that the capture session needs before it asks for more than 8 bits or inverts. The record names the driver version the
	///source reported and the image settings, and covers every computer that runs them (owner decision 2026-11-01). When the gated settings are
	///not the ones last saved in Capture settings (design 9.3 hash check), it says so, runs with the safe values and writes no record.
	///
	///All decisions are in TestCaptureModel; this window drives the session and shows the model. The session's events come on its worker
	///thread and are marshalled here. Closing the window does not wait: BeginClose stops the source in the safe order on the worker, and an
	///image that arrives later goes to the capture quarantine marked as a test capture.</summary>
	public partial class FrmTestCapture:FrmODBase {
		#region Fields
		private const int HistogramWidth=256;
		private const int HistogramHeight=100;
		private readonly ImagingDevice _imagingDevice;
		private TestCaptureModel _testCaptureModel;
		private TwainCaptureSession _twainCaptureSession;
		private bool _isClosed;
		private int _versionShown=-1;
		private TestCaptureAnalysis _analysisShown;
		///<summary>The category of each comboCategory item; 0 is none.</summary>
		private List<long> _listCategoryDefNums=new List<long>();
		#endregion Fields

		///<summary>imagingDevice: a saved device row (ImagingDeviceNum above 0), with the TWAIN source to test.</summary>
		public FrmTestCapture(ImagingDevice imagingDevice) {
			InitializeComponent();
			_imagingDevice=imagingDevice??throw new ArgumentNullException(nameof(imagingDevice));
			Load+=FrmTestCapture_Load;
			FormClosing+=FrmTestCapture_FormClosing;
			comboBits.SelectionChangeCommitted+=comboBits_SelectionChangeCommitted;
			comboCategory.SelectionChangeCommitted+=comboCategory_SelectionChangeCommitted;
			linkUnassigned.LinkClicked+=linkUnassigned_LinkClicked;
		}

		#region Methods - Event Handlers
		private void FrmTestCapture_Load(object sender,EventArgs e) {
			Lang.F(this);
			TestCaptureDevice testCaptureDevice=new TestCaptureDevice {
				ImagingDeviceNum=_imagingDevice.ImagingDeviceNum,
				Description=_imagingDevice.Description??"",
				TwainName=_imagingDevice.TwainName??"",
				ComputerName=_imagingDevice.ComputerName??"",
			};
			List<MountTemplateCategory> listMountTemplates=MountDefs.GetDeepCopy()
				.Select(x => new MountTemplateCategory { Description=x.Description,DefaultCat=x.DefaultCat })
				.ToList();
			LocalCaptureConfig localCaptureConfig=LocalCaptureConfig.Load();
			//Design 9.3: the Capture settings screen writes its entries where the verification records are (Setup, FKey=ImagingDeviceNum).
			CaptureSettingsCheck settingsCheck=CaptureSettingsCheck.Run(localCaptureConfig,_imagingDevice.ImagingDeviceNum,ODEnvironment.MachineName,
				x => SecurityLogs.Refresh(0,new List<EnumPermType> { EnumPermType.Setup },x));
			_testCaptureModel=new TestCaptureModel(testCaptureDevice,localCaptureConfig,ODEnvironment.MachineName,new ImageCategoryRule(),
				listMountTemplates,CaptureQuarantine.GetUnassignedText(),settingsCheck);
			comboBits.Items.Clear();
			foreach(int bits in _testCaptureModel.ListBitsChoices) {
				comboBits.Items.Add(Lang.g(this,bits+" bits"));
			}
			comboBits.SelectedIndex=Math.Max(0,_testCaptureModel.ListBitsChoices.IndexOf(_testCaptureModel.BitsToRequest));
			FillComboCategory();
			textSettings.Text=_testCaptureModel.GetSettingsText();
			RefreshView();
		}

		private void FrmTestCapture_FormClosing(object sender,CancelEventArgs e) {
			_isClosed=true;
			_testCaptureModel?.OnClosing();
			if(_twainCaptureSession!=null) {
				UnsubscribeSession(_twainCaptureSession);
				_twainCaptureSession.BeginClose();//does not wait; late images go to the quarantine
			}
		}

		private void butStart_Click(object sender,EventArgs e) {
			if(!_testCaptureModel.CanStart) {
				return;
			}
			if(_testCaptureModel.State==TestCaptureState.Open && _twainCaptureSession!=null) {
				EnableTestImage();
				return;
			}
			OpenAndEnable();
		}

		private void butStop_Click(object sender,EventArgs e) {
			if(_twainCaptureSession==null || !_testCaptureModel.CanStop) {
				return;
			}
			string warning=_testCaptureModel.GetCancelWarning();
			if(warning!=null && !MsgBox.Show(MsgBoxButtons.OKCancel,Lang.g(this,warning)+"\r\n\r\n"+Lang.g(this,"Stop now?"))) {
				return;
			}
			_twainCaptureSession.Cancel();
		}

		private void butRecord_Click(object sender,EventArgs e) {
			if(!_testCaptureModel.CanWriteRecord) {
				MsgBox.Show(_testCaptureModel.SettingsCheck.IsVerified ? Lang.g(this,"Take a test image first.") : _testCaptureModel.SettingsCheckText);
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			string error;
			CaptureVerificationRecord record=_testCaptureModel.BuildRecord(DateTime.Now,out error);
			if(record==null) {
				MsgBox.Show(error);
				return;
			}
			string warning=_testCaptureModel.GetRecordWarning(record);
			if(warning!="" && !MsgBox.Show(MsgBoxButtons.OKCancel,warning+"\r\n\r\n"+Lang.g(this,"Write it anyway?"))) {
				return;
			}
			try {
				//Setup, with FKey=ImagingDeviceNum: the capture session looks up the latest record for the device made on any computer, and matches
				//its driver version and image settings (design 9.3, owner decision 2026-11-01).
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,record.ToLogText(),record.ImagingDeviceNum,DateTime.MinValue);
			}
			catch(Exception ex) {
				MsgBox.Show(Lang.g(this,"The verification record could not be written:")+" "+ex.Message);
				return;
			}
			_testCaptureModel.OnRecordWritten(record);
			RefreshView();
			MsgBox.Show(Lang.g(this,"The verification record was written to the security log."));
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}

		///<summary>The notice opens the unassigned captures window (design 7.12) while no test image is being taken. A test capture's own late
		///images are there marked "patient unknown", so they are offered only through Reassign or Discard.</summary>
		private void linkUnassigned_LinkClicked(object sender,EventArgs e) {
			if(_isClosed || _testCaptureModel==null || !_testCaptureModel.CanOpenUnassignedCaptures) {
				return;
			}
			FrmCaptureQuarantine.ShowModal(new Recovery.CaptureRecoveryContext { View=Recovery.CaptureRecoveryView.Unassigned });
			_testCaptureModel.SetUnassignedText(CaptureQuarantine.GetUnassignedText());
			RefreshView();
		}

		private void checkByteOrder_Click(object sender,EventArgs e) {
			_testCaptureModel.IsByteOrderConfirmed=checkByteOrder.Checked==true;
			RefreshView();
		}

		private void checkPolarity_Click(object sender,EventArgs e) {
			_testCaptureModel.IsPolarityConfirmed=checkPolarity.Checked==true;
			RefreshView();
		}

		private void comboBits_SelectionChangeCommitted(object sender,EventArgs e) {
			int idx=comboBits.SelectedIndex;
			if(idx>=0 && idx<_testCaptureModel.ListBitsChoices.Count) {
				_testCaptureModel.BitsToRequest=_testCaptureModel.ListBitsChoices[idx];
			}
			RefreshView();
		}

		private void comboCategory_SelectionChangeCommitted(object sender,EventArgs e) {
			int idx=comboCategory.SelectedIndex;
			if(idx>=0 && idx<_listCategoryDefNums.Count) {
				_testCaptureModel.CategoryDefNum=_listCategoryDefNums[idx];
			}
			RefreshView();
		}
		#endregion Methods - Event Handlers

		#region Methods - session
		///<summary>Opens the source on a thread-pool thread (Open blocks until state 4 or its timeout), then enables for one test image.</summary>
		private void OpenAndEnable() {
			if(_twainCaptureSession!=null) {
				UnsubscribeSession(_twainCaptureSession);
				_twainCaptureSession.BeginClose();
			}
			TwainCaptureSession twainCaptureSession=new TwainCaptureSession();
			twainCaptureSession.SessionGuid=Guid.NewGuid();
			twainCaptureSession.QuarantineContext=new TwainCaptureQuarantineContext(0,TestCaptureModel.QuarantinePatientName);
			twainCaptureSession.FuncReadMetadataKeys=CaptureMetadataParser.ReadKeys;
			SubscribeSession(twainCaptureSession);
			_twainCaptureSession=twainCaptureSession;
			_testCaptureModel.OnOpening();
			RefreshView();
			string twainName=_imagingDevice.TwainName??"";
			TwainCaptureOptions twainCaptureOptions=_testCaptureModel.Options.Clone();
			System.Threading.Tasks.Task.Run(() => twainCaptureSession.Open(twainName,twainCaptureOptions)).ContinueWith(task => {
				TwainCaptureOpenResult result;
				if(task.Status==TaskStatus.RanToCompletion && task.Result!=null) {
					result=task.Result;
				}
				else {
					result=new TwainCaptureOpenResult { Error=TwainCaptureOpenError.Other,
						ErrorText=task.Exception?.GetBaseException().Message??"The sensor software could not be opened." };
				}
				Dispatcher.BeginInvoke(new Action(() => OnOpenFinished(twainCaptureSession,result)));
			});
		}

		private void OnOpenFinished(TwainCaptureSession twainCaptureSession,TwainCaptureOpenResult result) {
			if(_isClosed || twainCaptureSession!=_twainCaptureSession) {
				return;
			}
			_testCaptureModel.OnOpenResult(result);
			if(result.IsOpen) {
				textSettings.Text=_testCaptureModel.GetSettingsText();
				EnableTestImage();
			}
			RefreshView();
		}

		private void EnableTestImage() {
			int enableId=_twainCaptureSession.Enable(_testCaptureModel.CreateEnableRequest());
			_testCaptureModel.OnEnablePosted(enableId);
			RefreshView();
		}

		private void SubscribeSession(TwainCaptureSession twainCaptureSession) {
			twainCaptureSession.ImageAcquired+=session_ImageAcquired;
			twainCaptureSession.Ended+=session_Ended;
			twainCaptureSession.StatusChanged+=session_StatusChanged;
		}

		private void UnsubscribeSession(TwainCaptureSession twainCaptureSession) {
			twainCaptureSession.ImageAcquired-=session_ImageAcquired;
			twainCaptureSession.Ended-=session_Ended;
			twainCaptureSession.StatusChanged-=session_StatusChanged;
		}

		//The session raises these on its worker thread; they return at once and the work runs on this window's thread.
		private void session_ImageAcquired(AcquiredImage image) {
			RunOnWindow(() => _testCaptureModel.OnImage(image));
		}

		private void session_Ended(TwainCaptureEnded ended) {
			RunOnWindow(() => _testCaptureModel.OnEnded(ended));
		}

		private void session_StatusChanged(TwainCaptureStatus status) {
			RunOnWindow(() => _testCaptureModel.OnStatus(status));
		}

		private void RunOnWindow(Action action) {
			try {
				Dispatcher.BeginInvoke(new Action(() => {
					if(_isClosed) {
						return;
					}
					action();
					RefreshView();
				}));
			}
			catch {
				//the window's dispatcher is shutting down
			}
		}
		#endregion Methods - session

		#region Methods - view
		private void FillComboCategory() {
			comboCategory.Items.Clear();
			_listCategoryDefNums=new List<long>();
			comboCategory.Items.Add(Lang.g(this,"(none)"));
			_listCategoryDefNums.Add(0);
			foreach(ImageCategoryItem item in _testCaptureModel.GetCategoryPickList()) {
				comboCategory.Items.Add(item.Name+(item.IsHidden ? " "+Lang.g(this,"(hidden)") : ""));
				_listCategoryDefNums.Add(item.DefNum);
			}
			int idx=_listCategoryDefNums.IndexOf(_testCaptureModel.CategoryDefNum);
			comboCategory.SelectedIndex=Math.Max(0,idx);
		}

		private void RefreshView() {
			TestCaptureModel model=_testCaptureModel;
			if(model==null || model.Version==_versionShown) {
				return;
			}
			_versionShown=model.Version;
			SetText(labelDevice,model.GetDeviceText());
			SetText(labelSettingsCheck,model.SettingsCheckText);
			string unassigned=model.UnassignedText??"";
			if(linkUnassigned.Text!=unassigned) {
				linkUnassigned.Text=unassigned;
			}
			int linkLength=model.CanOpenUnassignedCaptures ? unassigned.Length : 0;
			if(linkUnassigned.LinkLength!=linkLength) {
				linkUnassigned.LinkLength=linkLength;
			}
			SetText(labelStatus,model.StatusText);
			SetText(labelMessage,model.MessageText);
			string results=model.GetResultText();
			if(textResults.Text!=results) {
				textResults.Text=results;
			}
			SetText(labelCategoryWarning,model.GetCategoryWarning());
			butStart.IsEnabled=model.CanStart;
			butStop.IsEnabled=model.CanStop;
			comboBits.IsEnabled=model.CanStart;
			butRecord.IsEnabled=model.CanWriteRecord;
			checkByteOrder.IsEnabled=model.IsByteOrderCheckable;
			checkByteOrder.Checked=model.IsByteOrderConfirmed;
			checkPolarity.IsEnabled=model.IsPolarityCheckable;
			checkPolarity.Checked=model.IsPolarityConfirmed;
			if(model.Analysis!=_analysisShown) {
				_analysisShown=model.Analysis;
				ShowAnalysis(model.Analysis);
			}
		}

		///<summary>The preview (through the window's LUT for 16-bit samples) and the histogram with the window's ends marked.</summary>
		private void ShowAnalysis(TestCaptureAnalysis analysis) {
			if(analysis==null || !analysis.IsValid || analysis.Preview==null) {
				imagePreview.Source=null;
				imageHistogram.Source=null;
				SetText(labelHistogramNote,"");
				return;
			}
			imagePreview.Source=CreateGray8(analysis.Width,analysis.Height,analysis.Preview);
			int[] arrayMarkers=null;
			string note;
			if(analysis.IsGray16) {
				arrayMarkers=new[] { analysis.WindowLow>>analysis.HistogramShift,analysis.WindowHigh>>analysis.HistogramShift };
				note=Lang.g(this,"Non-zero samples from 0 to")+" "+((1<<Math.Max(8,analysis.EffectiveBitsMeasured))-1)+", "
					+Lang.g(this,"each bar")+" "+(1<<analysis.HistogramShift)+" "+Lang.g(this,"values. The grey lines mark the window, which the preview uses.");
			}
			else {
				note=Lang.g(this,"Grey values 0 to 255 as delivered.");
			}
			imageHistogram.Source=CreateGray8(HistogramWidth,HistogramHeight,HistogramPicture.Render(analysis.Histogram,HistogramWidth,HistogramHeight,arrayMarkers));
			SetText(labelHistogramNote,note);
		}

		private static BitmapSource CreateGray8(int width,int height,byte[] pixels) {
			BitmapSource bitmapSource=BitmapSource.Create(width,height,96,96,PixelFormats.Gray8,null,pixels,width);
			bitmapSource.Freeze();
			return bitmapSource;
		}

		private static void SetText(Label label,string text) {
			text=text??"";
			if(label.Text!=text) {
				label.Text=text;
			}
		}
		#endregion Methods - view
	}
}
