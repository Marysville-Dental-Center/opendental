// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImagingCapture.Recovery;
using OpenDental;
using OpenDentBusiness;

namespace ImagingCapture.UI {
	///<summary>Unassigned captures and Recover recent captures (design 7.1, 7.12, 10.6, 10.7): the quarantine files or the journal copies of this
	///computer with their thumbnails and sidecar details, and per capture Assign (the originating patient only, with an explicit confirmation),
	///Reassign (a patient found with the patient search, never preselected, with a typed reason) and Discard. Each action checks its permission
	///when it is begun and again when it is confirmed.
	///
	///All the logic is in CaptureRecoveryModel and CaptureRecoveryFiler; this window shows the model and passes on clicks. It opens modal, from the
	///capture session window while its source is not enabled, from Test capture, from FormVideo and from Setup > Imaging > Devices.</summary>
	public partial class FrmCaptureQuarantine:FrmODBase {
		#region Fields
		private const double ThumbnailSide=76;
		private static readonly Color _colorSelected=Color.FromRgb(204,228,255);
		private static readonly Color _colorDone=Color.FromRgb(232,232,232);
		private static readonly Color _colorOverdue=Color.FromRgb(192,0,0);
		private readonly CaptureRecoveryModel _model;
		private readonly Dictionary<string,BitmapSource> _dictBitmaps=new Dictionary<string,BitmapSource>(StringComparer.OrdinalIgnoreCase);
		private int _versionShown=-1;
		private bool _isFillingCategories;
		private List<long> _listCategoryDefNums=new List<long>();
		#endregion Fields

		///<summary>model: not loaded yet; the window loads it.</summary>
		public FrmCaptureQuarantine(CaptureRecoveryModel model) {
			InitializeComponent();
			_model=model??throw new ArgumentNullException(nameof(model));
			Load+=FrmCaptureQuarantine_Load;
			FormClosing+=FrmCaptureQuarantine_FormClosing;
			comboCategory.SelectionChangeCommitted+=comboCategory_SelectionChangeCommitted;
		}

		///<summary>Opens the window modal and returns its model, whose ListResults says what was filed. Errors show in the window.</summary>
		public static CaptureRecoveryModel ShowModal(CaptureRecoveryContext context) {
			CaptureRecoveryModel model=new CaptureRecoveryModel(context,CaptureRecoveryServices.CreateDefault());
			FrmCaptureQuarantine frmCaptureQuarantine=new FrmCaptureQuarantine(model);
			frmCaptureQuarantine.ShowDialog();
			return model;
		}

		#region Methods - Event Handlers
		private void FrmCaptureQuarantine_Load(object sender,EventArgs e) {
			Lang.F(this);
			_model.Changed+=model_Changed;
			_model.Load();
			RefreshView();
		}

		private void FrmCaptureQuarantine_FormClosing(object sender,CancelEventArgs e) {
			_model.Changed-=model_Changed;
		}

		private void model_Changed() {
			RefreshView();
		}

		private void butUnassigned_Click(object sender,EventArgs e) {
			_model.SetView(CaptureRecoveryView.Unassigned);
		}

		private void butJournal_Click(object sender,EventArgs e) {
			_model.SetView(CaptureRecoveryView.Journal);
		}

		private void checkOthers_Click(object sender,EventArgs e) {
			_model.SetOthersShown(checkOthers.Checked==true);
		}

		private void butAssign_Click(object sender,EventArgs e) {
			_model.BeginAssign();
		}

		private void butReassign_Click(object sender,EventArgs e) {
			textReason.Text="";
			_model.BeginReassign();
		}

		private void butDiscard_Click(object sender,EventArgs e) {
			_model.BeginDiscard();
		}

		///<summary>The patient search, with nothing filled in and no patient selected (design 7.12: never preselected, not even the session's).</summary>
		private void butFindPatient_Click(object sender,EventArgs e) {
			FrmPatientSelect frmPatientSelect=new FrmPatientSelect();
			frmPatientSelect.ShowDialog();
			if(frmPatientSelect.IsDialogCancel) {
				return;
			}
			_model.SetReassignPatient(frmPatientSelect.PatNumSelected);
		}

		private void comboCategory_SelectionChangeCommitted(object sender,EventArgs e) {
			if(_isFillingCategories) {
				return;
			}
			int idx=comboCategory.SelectedIndex;
			_model.SetCategory(idx>=0 && idx<_listCategoryDefNums.Count ? _listCategoryDefNums[idx] : 0);
		}

		private void butConfirm_Click(object sender,EventArgs e) {
			_model.Confirm(textReason.Text);
		}

		private void butCancelStep_Click(object sender,EventArgs e) {
			_model.CancelStep();
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}

		private void row_MouseLeftButtonUp(object sender,MouseButtonEventArgs e) {
			if(sender is Border border && border.Tag is int) {
				_model.Select((int)border.Tag);
			}
		}
		#endregion Methods - Event Handlers

		#region Methods - view
		private void RefreshView() {
			if(_model.Version==_versionShown) {
				return;
			}
			_versionShown=_model.Version;
			Text=_model.WindowTitle;
			butUnassigned.IsEnabled=_model.View!=CaptureRecoveryView.Unassigned;
			butJournal.IsEnabled=_model.View!=CaptureRecoveryView.Journal;
			checkOthers.Visible=_model.IsOthersOptionVisible;
			checkOthers.Checked=_model.IsOthersShown;
			SetText(labelList,_model.ListText);
			RefreshList();
			CaptureRecoveryItem item=_model.Selected;
			string error;
			CapturePreviewPixels pixels=_model.GetPreview(out error);
			imagePreview.Source=pixels==null ? null : CreateBitmapSource(pixels);
			textDetails.Text=item==null ? "" : _model.DetailsText+(error=="" ? "" : "\r\n"+error);
			butAssign.IsEnabled=_model.IsAssignEnabled;
			butReassign.IsEnabled=_model.IsReassignEnabled;
			butDiscard.Visible=_model.IsDiscardVisible;
			butDiscard.IsEnabled=_model.IsDiscardEnabled;
			RefreshStep(pixels);
			SetText(labelError,_model.ErrorText);
			SetText(labelResult,_model.ErrorText=="" ? _model.ResultText : "");
			labelError.Visible=_model.ErrorText!="";
			labelResult.Visible=_model.ErrorText=="" && _model.ResultText!="";
		}

		///<summary>One row per capture: its thumbnail, the patient, the receive time and the slot, and what can be done with it.</summary>
		private void RefreshList() {
			stackList.Children.Clear();
			labelEmpty.Visible=_model.Items.Count==0;
			SetText(labelEmpty,_model.EmptyText);
			for(int i=0;i<_model.Items.Count;i++) {
				CaptureRecoveryItem item=_model.Items[i];
				Grid grid=new Grid();
				grid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(ThumbnailSide+8) });
				grid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
				Border borderThumb=new Border { Width=ThumbnailSide,Height=ThumbnailSide,Background=Brushes.Black,Margin=new Thickness(2) };
				borderThumb.Child=new Image { Source=GetThumbnail(item),Stretch=Stretch.Uniform };
				grid.Children.Add(borderThumb);
				StackPanel stackText=new StackPanel { Margin=new Thickness(4,2,4,2) };
				stackText.Children.Add(new TextBlock { Text=item.TitleText,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap });
				string note=item.IsDone ? item.DoneText : item.NoteText;
				if(note!="") {
					stackText.Children.Add(new TextBlock { Text=note,TextWrapping=TextWrapping.Wrap,
						Foreground=item.IsOverdue && !item.IsDone ? new SolidColorBrush(_colorOverdue) : Brushes.Black });
				}
				Grid.SetColumn(stackText,1);
				grid.Children.Add(stackText);
				Border borderRow=new Border {
					Child=grid,
					Tag=i,
					BorderBrush=Brushes.LightGray,
					BorderThickness=new Thickness(0,0,0,1),
					Background=new SolidColorBrush(i==_model.IdxSelected ? _colorSelected : item.IsDone ? _colorDone : Colors.White),
					Cursor=Cursors.Hand,
				};
				borderRow.MouseLeftButtonUp+=row_MouseLeftButtonUp;
				stackList.Children.Add(borderRow);
			}
		}

		private void RefreshStep(CapturePreviewPixels pixels) {
			bool isStep=_model.Step!=CaptureRecoveryStep.None;
			borderStep.Visibility=isStep ? Visibility.Visible : Visibility.Collapsed;
			borderStepImage.Visibility=isStep ? Visibility.Visible : Visibility.Collapsed;
			imageStep.Source=isStep && pixels!=null ? CreateBitmapSource(pixels) : null;
			labelStep.Visible=isStep;
			labelStepPatient.Visible=isStep;
			labelStepDetails.Visible=isStep;
			SetText(labelStep,_model.StepText);
			SetText(labelStepPatient,_model.StepPatientText);
			SetText(labelStepDetails,_model.StepDetailsText);
			butFindPatient.Visible=_model.IsFindPatientVisible;
			labelReason.Visible=_model.IsReasonVisible;
			textReason.Visible=_model.IsReasonVisible;
			labelCategory.Visible=_model.IsCategoryVisible;
			comboCategory.Visible=_model.IsCategoryVisible;
			if(_model.IsCategoryVisible) {
				FillCategories();
			}
			butConfirm.Visible=_model.IsConfirmVisible;
			butConfirm.IsEnabled=_model.IsConfirmEnabled;
			butConfirm.Text=_model.ConfirmText;
			butCancelStep.Visible=isStep;
		}

		private void FillCategories() {
			_isFillingCategories=true;
			try {
				comboCategory.Items.Clear();
				_listCategoryDefNums=new List<long>();
				foreach(Def def in _model.ListCategories) {
					comboCategory.Items.Add(def.ItemName);
					_listCategoryDefNums.Add(def.DefNum);
				}
				comboCategory.SelectedIndex=_listCategoryDefNums.IndexOf(_model.CategoryDefNum);
			}
			finally {
				_isFillingCategories=false;
			}
		}

		private BitmapSource GetThumbnail(CaptureRecoveryItem item) {
			string path=item.Entry.ImagePath;
			BitmapSource bitmapSource;
			if(_dictBitmaps.TryGetValue(path,out bitmapSource)) {
				return bitmapSource;
			}
			string error;
			CapturePreviewPixels pixels=_model.GetThumbnail(item,out error);
			bitmapSource=pixels==null ? null : CreateBitmapSource(pixels);
			_dictBitmaps[path]=bitmapSource;
			return bitmapSource;
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

		private static void SetText(WpfControls.UI.Label label,string text) {
			text=text??"";
			if(label.Text!=text) {
				label.Text=text;
			}
		}
		#endregion Methods - view
	}
}
