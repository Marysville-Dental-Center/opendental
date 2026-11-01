// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ImagingCapture.Camera;
using ImagingCapture.Uvc;
using ImagingDeviceManager.Config;
using OpenPmsDental;
using WpfControls.UI;

namespace ImagingCapture.UI {
	///<summary>The camera Learn tool and the button test (design 8.5), opened from FormVideo's settings. It reads camera controls
	///with Get only and never writes to the camera. All decisions are in CaptureLearnModel; this window feeds it from a
	///LearnSampler and a timer, and shows its state.
	///
	///The caller passes the camera it has open (only one program, and one graph, can hold a camera), already streaming, and the
	///loaded capture.ini. While this window is open the caller stops its own TriggerEngine, so Learn's key presses take no photo,
	///and afterwards starts a new engine from capture.ini. For a still-pin key to be learnable, the camera must have been started
	///with its still pin connected. Show it with ShowDialog; after IsDialogOK, CameraConfigSaved holds what was written.</summary>
	public partial class FrmCaptureLearn:FrmODBase {
		#region Fields
		///<summary>How often the window shows the model's state and gives it the clock. 10 per second keeps the counters and tiles
		///current without redrawing on every reading.</summary>
		private const int RefreshMs=100;
		private static readonly Color _colorTileLit=Color.FromRgb(50,170,80);
		private static readonly Color _colorTileWaiting=Color.FromRgb(220,230,240);
		private static readonly Color _colorTileNotSetUp=Color.FromRgb(240,240,240);
		private static readonly Color _colorWarning=Color.FromRgb(192,0,0);
		private static readonly Color _colorNote=Color.FromRgb(100,100,100);

		private readonly IFrameSource _frameSource;
		private readonly LocalCaptureConfig _localCaptureConfig;
		private readonly CaptureLearnMode _captureLearnMode;
		private CaptureLearnModel _captureLearnModel;
		private ImageCategoryRule _imageCategoryRule;
		private LearnSampler _learnSampler;
		private DispatcherTimer _dispatcherTimer;
		private bool _isClosed;
		///<summary>The category of each comboCategory item; 0 is the category selected in Imaging.</summary>
		private List<long> _listCategoryDefNums=new List<long>();
		private List<KeyCandidate> _listKeyCandidates=new List<KeyCandidate>();
		///<summary>The candidate of each comboLevel item after the first ("None").</summary>
		private List<LevelCandidate> _listLevelCandidates=new List<LevelCandidate>();
		private List<Label> _listLabelTiles=new List<Label>();
		private int _versionShown=-1;
		private int _versionLiveTestShown=-1;
		///<summary>The values last shown in gridValues, so it is refilled only when one changes.</summary>
		private string _gridContentShown;
		#endregion Fields

		#region Constructor
		///<summary>frameSource: the camera the caller has open and streaming. localCaptureConfig: the loaded capture.ini, which Save
		///writes and saves. When saving fails, it gets its old camera values back (CameraConfig.WriteAndSave).</summary>
		public FrmCaptureLearn(IFrameSource frameSource,LocalCaptureConfig localCaptureConfig,CaptureLearnMode captureLearnMode) {
			InitializeComponent();
			_frameSource=frameSource??throw new ArgumentNullException(nameof(frameSource));
			_localCaptureConfig=localCaptureConfig??throw new ArgumentNullException(nameof(localCaptureConfig));
			_captureLearnMode=captureLearnMode;
			Load+=FrmCaptureLearn_Load;
			FormClosed+=FrmCaptureLearn_FormClosed;
			comboKey.SelectionChangeCommitted+=comboKey_SelectionChangeCommitted;
			comboLevel.SelectionChangeCommitted+=comboLevel_SelectionChangeCommitted;
			comboCategory.SelectionChangeCommitted+=comboCategory_SelectionChangeCommitted;
		}
		#endregion Constructor

		#region Properties
		///<summary>After Save: the camera settings written to capture.ini. Null otherwise.</summary>
		public CameraConfig CameraConfigSaved { get; private set; }
		#endregion Properties

		#region Methods - Event Handlers
		private void FrmCaptureLearn_Load(object sender,EventArgs e) {
			Lang.F(this);
			if(_captureLearnMode==CaptureLearnMode.ButtonTest) {
				Text=Lang.g(this,"Camera Button Test");
				panelTiles.Margin=new Thickness(12,124+142,0,0);
				panelTiles.Width=790;
			}
			CameraConfig cameraConfig=CameraConfig.Read(_localCaptureConfig);
			_imageCategoryRule=new ImageCategoryRule();
			long categoryDefNum=0;
			bool isCategoryEditable=false;
			if(_captureLearnMode==CaptureLearnMode.Learn) {
				categoryDefNum=VideoCategoryPref.Get();
				isCategoryEditable=VideoCategoryPref.CanEdit();
			}
			_captureLearnModel=new CaptureLearnModel(_captureLearnMode,cameraConfig,_imageCategoryRule,categoryDefNum,isCategoryEditable,
				_frameSource.HasStillPin,_frameSource.IsStillPinConnected);
			FillComboCategory();
			SynchronizationContext synchronizationContext=SynchronizationContext.Current??new DispatcherSynchronizationContext(Dispatcher);
			_learnSampler=new LearnSampler(_frameSource,cameraConfig.EffectivePollMs,_captureLearnModel.GetControlsToRead(),synchronizationContext);
			_learnSampler.ControlsFound+=learnSampler_ControlsFound;
			_learnSampler.SampleTaken+=learnSampler_SampleTaken;
			_learnSampler.StillSampleSeen+=learnSampler_StillSampleSeen;
			_learnSampler.CameraError+=learnSampler_CameraError;
			_learnSampler.Start();
			_dispatcherTimer=new DispatcherTimer();
			_dispatcherTimer.Interval=TimeSpan.FromMilliseconds(RefreshMs);
			_dispatcherTimer.Tick+=dispatcherTimer_Tick;
			_dispatcherTimer.Start();
			RefreshView();
		}

		private void FrmCaptureLearn_FormClosed(object sender,EventArgs e) {
			_isClosed=true;
			_dispatcherTimer?.Stop();
			_learnSampler?.Dispose();
		}

		private void dispatcherTimer_Tick(object sender,EventArgs e) {
			if(_isClosed) {
				return;
			}
			_captureLearnModel.Tick(_learnSampler.NowMs);
			RefreshView();
		}

		private void learnSampler_ControlsFound(object sender,ControlsFoundEventArgs e) {
			_captureLearnModel.SetControls(e.Controls);
		}

		private void learnSampler_SampleTaken(object sender,ControlSampleEventArgs e) {
			_captureLearnModel.OnSample(e.Sample);
		}

		private void learnSampler_StillSampleSeen(object sender,StillSampleEventArgs e) {
			_captureLearnModel.OnStillSample(e.TimeMs);
		}

		private void learnSampler_CameraError(object sender,UvcErrorEventArgs e) {
			if(_isClosed) {
				return;
			}
			_learnSampler.Stop();
			_dispatcherTimer.Stop();
			string text=e.Kind==UvcErrorKind.DeviceLost
				? Lang.g(this,"Camera disconnected.")
				: Lang.g(this,"The camera stopped with an error.");
			MsgBox.Show(text+" "+Lang.g(this,"Nothing was saved. Reconnect the camera in the Video window, then open Learn again."));
			Close();
		}

		private void butRecord_Click(object sender,EventArgs e) {
			_captureLearnModel.StartRecording(_learnSampler.NowMs);
			RefreshView();
		}

		private void butStop_Click(object sender,EventArgs e) {
			_captureLearnModel.StopRecording();
			RefreshView();
		}

		private void butSkip_Click(object sender,EventArgs e) {
			_captureLearnModel.Skip();
			RefreshView();
		}

		private void butResetCounts_Click(object sender,EventArgs e) {
			_captureLearnModel.ResetLiveTest();
			RefreshView();
		}

		private void butBack_Click(object sender,EventArgs e) {
			_captureLearnModel.GoBack();
			RefreshView();
		}

		private void butNext_Click(object sender,EventArgs e) {
			_captureLearnModel.GoNext();
			RefreshView();
		}

		private void butSave_Click(object sender,EventArgs e) {
			Save();
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}

		private void comboKey_SelectionChangeCommitted(object sender,EventArgs e) {
			int idx=comboKey.SelectedIndex;
			if(idx>=0 && idx<_listKeyCandidates.Count) {
				_captureLearnModel.ChooseKey(_listKeyCandidates[idx]);
			}
			RefreshView();
		}

		private void comboLevel_SelectionChangeCommitted(object sender,EventArgs e) {
			int idx=comboLevel.SelectedIndex;
			LevelCandidate levelCandidate=null;
			if(idx>=1 && idx-1<_listLevelCandidates.Count) {
				levelCandidate=_listLevelCandidates[idx-1];
			}
			_captureLearnModel.ChooseLevel(levelCandidate);
			RefreshView();
		}

		private void comboCategory_SelectionChangeCommitted(object sender,EventArgs e) {
			int idx=comboCategory.SelectedIndex;
			if(idx>=0 && idx<_listCategoryDefNums.Count) {
				_captureLearnModel.SetCategory(_listCategoryDefNums[idx]);
			}
			RefreshView();
		}
		#endregion Methods - Event Handlers

		#region Methods - Private
		///<summary>Writes the configuration under test to capture.ini and, when it changed, the video image category.</summary>
		private void Save() {
			CameraConfig cameraConfig=_captureLearnModel.GetConfigToSave();
			if(cameraConfig==null) {
				return;
			}
			string categoryWarning=_captureLearnModel.GetCategoryWarning();
			if(categoryWarning!="" && !MsgBox.Show(MsgBoxButtons.OKCancel,categoryWarning+"\r\n\r\n"+Lang.g(this,"Save anyway?"))) {
				return;
			}
			string error;
			string warning;
			//The caller's window shares _localCaptureConfig. On failure WriteAndSave puts the old values back, so its next save does
			//not pick up the values this window says were not saved.
			if(!cameraConfig.WriteAndSave(_localCaptureConfig,out error,out warning)) {
				MsgBox.Show(Lang.g(this,"The camera settings could not be saved.")+" "+error);
				return;
			}
			List<string> listWarnings=new List<string>();
			if(warning!="") {
				listWarnings.Add(warning);
			}
			string errorCategory;
			if(!VideoCategoryPref.Save(_captureLearnModel.CategoryDefNumOriginal,_captureLearnModel.CategoryDefNum,_imageCategoryRule,out errorCategory)) {
				listWarnings.Add(errorCategory);
			}
			if(listWarnings.Count>0) {
				MsgBox.Show(Lang.g(this,"The camera settings were saved.")+"\r\n"+string.Join("\r\n",listWarnings));
			}
			CameraConfigSaved=cameraConfig;
			IsDialogOK=true;
		}

		private void FillComboCategory() {
			comboCategory.Items.Clear();
			_listCategoryDefNums=new List<long>();
			comboCategory.Items.Add(Lang.g(this,"(Category selected in Imaging)"));
			_listCategoryDefNums.Add(0);
			long defNumCurrent=_captureLearnModel.CategoryDefNumOriginal;
			List<ImageCategoryItem> listItems=_imageCategoryRule.GetPickList(defNumCurrent);
			for(int i=0;i<listItems.Count;i++) {
				comboCategory.Items.Add(listItems[i].Name+(listItems[i].IsHidden ? " "+Lang.g(this,"(hidden)") : ""));
				_listCategoryDefNums.Add(listItems[i].DefNum);
			}
			if(defNumCurrent!=0 && !_listCategoryDefNums.Contains(defNumCurrent)) {
				comboCategory.Items.Add(Lang.g(this,"(missing category)")+" "+defNumCurrent.ToString(CultureInfo.InvariantCulture));
				_listCategoryDefNums.Add(defNumCurrent);
			}
			comboCategory.SelectedIndex=_listCategoryDefNums.IndexOf(_captureLearnModel.CategoryDefNum);
			comboCategory.IsEnabled=_captureLearnModel.IsCategoryEditable;
		}

		private void FillComboKey() {
			_listKeyCandidates=_captureLearnModel.GetKeyCandidates().ToList();
			comboKey.Items.Clear();
			for(int i=0;i<_listKeyCandidates.Count;i++) {
				comboKey.Items.Add(_listKeyCandidates[i].Binding.ToString());
			}
			comboKey.SelectedIndex=_listKeyCandidates.IndexOf(_captureLearnModel.KeyChosen);
		}

		private void FillComboLevel() {
			_listLevelCandidates=_captureLearnModel.GetLevelCandidates();
			comboLevel.Items.Clear();
			comboLevel.Items.Add(Lang.g(this,"None"));
			for(int i=0;i<_listLevelCandidates.Count;i++) {
				string source=_listLevelCandidates[i].Source==LevelCandidateSource.LevelStep
					? Lang.g(this,"seen when put down")
					: Lang.g(this,"differs between held and resting");
				comboLevel.Items.Add(_listLevelCandidates[i].Binding+" ("+source+")");
			}
			comboLevel.SelectedIndex=_listLevelCandidates.IndexOf(_captureLearnModel.LevelChosen)+1;
		}

		///<summary>Shows the model's state. Lists, the result and the category are refilled only when the model's Version changed.</summary>
		private void RefreshView() {
			if(_captureLearnModel==null) {
				return;
			}
			CaptureLearnModel model=_captureLearnModel;
			bool isLearn=_captureLearnMode==CaptureLearnMode.Learn;
			SetText(labelStep,model.GetStepTitle());
			SetText(labelInstructions,model.GetInstructions());
			SetText(labelProgress,model.GetProgressText(_learnSampler.NowMs));
			if(model.Version!=_versionShown) {
				_versionShown=model.Version;
				SetText(labelResult,model.GetResultText());
				FillComboKey();
				FillComboLevel();
				comboCategory.SelectedIndex=_listCategoryDefNums.IndexOf(model.CategoryDefNum);
				string warning=model.GetCategoryWarning();
				if(warning=="" && !model.IsCategoryEditable) {
					labelCategoryWarning.ColorText=_colorNote;
					SetText(labelCategoryWarning,Lang.g(this,"Changing the video image category needs the Setup permission."));
				}
				else {
					labelCategoryWarning.ColorText=_colorWarning;
					SetText(labelCategoryWarning,warning);
				}
			}
			bool isPageWithRecording=isLearn && model.Page!=LearnPage.LiveTest;
			butRecord.Visible=isPageWithRecording;
			butRecord.IsEnabled=model.CanRecord;
			butStop.Visible=isLearn && (model.Page==LearnPage.KeyCapture || model.Page==LearnPage.Level);
			butStop.IsEnabled=model.CanStop;
			butSkip.Visible=isLearn && (model.Page==LearnPage.BaselineB || model.Page==LearnPage.Level);
			butSkip.IsEnabled=model.CanSkip;
			butResetCounts.Visible=model.Page==LearnPage.LiveTest;
			labelKey.Visible=model.IsKeyPickerVisible;
			comboKey.Visible=model.IsKeyPickerVisible;
			labelLevel.Visible=model.IsLevelPickerVisible;
			comboLevel.Visible=model.IsLevelPickerVisible;
			comboLevel.IsEnabled=!model.IsRecording;
			gridValues.Visible=model.IsValueGridVisible;
			panelTiles.Visibility=model.IsTilesVisible ? Visibility.Visible : Visibility.Collapsed;
			labelCategory.Visible=model.IsCategoryVisible;
			comboCategory.Visible=model.IsCategoryVisible;
			labelCategoryWarning.Visible=model.IsCategoryVisible;
			butBack.Visible=isLearn;
			butBack.IsEnabled=model.CanGoBack;
			butNext.Visible=isLearn;
			butNext.IsEnabled=model.CanGoNext;
			butSave.Visible=isLearn;
			butSave.IsEnabled=model.CanSave;
			if(model.LiveTestVersion!=_versionLiveTestShown) {
				_versionLiveTestShown=model.LiveTestVersion;
				BuildTiles();
				_gridContentShown=null;
			}
			UpdateTiles();
			if(model.IsValueGridVisible) {
				FillGridValues();
			}
		}

		///<summary>One label per tile of the live test, in rows.</summary>
		private void BuildTiles() {
			panelTiles.Items.Clear();
			_listLabelTiles=new List<Label>();
			LiveTestModel liveTestModel=_captureLearnModel.LiveTest;
			if(liveTestModel==null) {
				return;
			}
			const int widthTile=125;
			const int heightTile=44;
			const int gap=6;
			int countColumns=Math.Max(1,((int)panelTiles.Width-gap)/(widthTile+gap));
			for(int i=0;i<liveTestModel.Tiles.Count;i++) {
				Label label=new Label();
				label.Width=widthTile;
				label.Height=heightTile;
				label.Margin=new Thickness(gap+(i%countColumns)*(widthTile+gap),gap+(i/countColumns)*(heightTile+gap),0,0);
				label.HAlign=HorizontalAlignment.Center;
				label.VAlign=VerticalAlignment.Center;
				label.IsWrap=true;
				panelTiles.Items.Add(label);
				_listLabelTiles.Add(label);
			}
		}

		private void UpdateTiles() {
			LiveTestModel liveTestModel=_captureLearnModel.LiveTest;
			if(liveTestModel==null) {
				return;
			}
			for(int i=0;i<_listLabelTiles.Count && i<liveTestModel.Tiles.Count;i++) {
				LiveTile liveTile=liveTestModel.Tiles[i];
				Label label=_listLabelTiles[i];
				SetText(label,LiveTileView.GetText(liveTile));
				switch(LiveTileView.GetState(liveTile)) {
					case LiveTileState.Lit:
						label.ColorBack=_colorTileLit;
						label.ColorText=Colors.White;
						break;
					case LiveTileState.Waiting:
						label.ColorBack=_colorTileWaiting;
						label.ColorText=Colors.Black;
						break;
					default:
						label.ColorBack=_colorTileNotSetUp;
						label.ColorText=_colorNote;
						break;
				}
			}
		}

		///<summary>The raw value of every control, refilled only when a value changed.</summary>
		private void FillGridValues() {
			LiveTestModel liveTestModel=_captureLearnModel.LiveTest;
			if(liveTestModel==null) {
				return;
			}
			string content=string.Join("|",liveTestModel.Rows.Select(x => x.ValueDecimal));
			if(content==_gridContentShown) {
				return;
			}
			_gridContentShown=content;
			gridValues.BeginUpdate();
			gridValues.Columns.Clear();
			gridValues.Columns.Add(new GridColumn(Lang.g(this,"Control"),190));
			gridValues.Columns.Add(new GridColumn(Lang.g(this,"Decimal"),70,HorizontalAlignment.Right));
			gridValues.Columns.Add(new GridColumn(Lang.g(this,"Hex"),80));
			GridColumn gridColumn=new GridColumn(Lang.g(this,"Binary"),140);
			gridColumn.IsWidthDynamic=true;
			gridValues.Columns.Add(gridColumn);
			gridValues.Columns.Add(new GridColumn(Lang.g(this,"Used"),40,HorizontalAlignment.Center));
			gridValues.ListGridRows.Clear();
			for(int i=0;i<liveTestModel.Rows.Count;i++) {
				LiveValueRow liveValueRow=liveTestModel.Rows[i];
				GridRow gridRow=new GridRow();
				gridRow.Cells.Add(liveValueRow.Label);
				gridRow.Cells.Add(liveValueRow.IsReadable ? liveValueRow.ValueDecimal : Lang.g(this,"unreadable"));
				gridRow.Cells.Add(liveValueRow.ValueHex);
				gridRow.Cells.Add(liveValueRow.ValueBinary);
				gridRow.Cells.Add(liveValueRow.IsUsed ? "X" : "");
				gridValues.ListGridRows.Add(gridRow);
			}
			gridValues.EndUpdate();
		}

		///<summary>Sets the text only when it changed, so a label is not laid out again ten times a second.</summary>
		private static void SetText(Label label,string text) {
			text=text??"";
			if(label.Text!=text) {
				label.Text=text;
			}
		}
		#endregion Methods - Private
	}
}
