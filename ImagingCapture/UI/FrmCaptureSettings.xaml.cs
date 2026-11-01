// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using ImagingCapture.Settings;
using OpenDental;
using OpenDentBusiness;
using WpfControls.UI;

namespace ImagingCapture.UI {
	///<summary>Capture settings (design 9.3), opened from FormImagingDeviceEdit for one saved imaging device: this computer's capture.ini for the
	///device, that is [Session], [Twain], the device's [Device.N], [Keys] and [Sounds], with the gated keys marked. Opening and saving need the
	///Setup permission; every save writes a Setup security-log entry with the old and new values and the new settings hash, which the capture
	///session, Test capture and the 16-bit decision check the file against. All decisions are in CaptureSettingsModel; this window shows it.
	///No source is open here, so confirmations are message boxes.</summary>
	public partial class FrmCaptureSettings:FrmODBase {
		#region Fields
		private static readonly Color _colorProblem=Color.FromRgb(192,0,0);
		private static readonly Color _colorChangedOutside=Color.FromRgb(255,240,200);
		private readonly CaptureSettingsModel _model;
		private int _idxSelected=-1;
		///<summary>The values behind comboValue's items, in order.</summary>
		private List<string> _listComboValues=new List<string>();
		///<summary>Set while the window fills its controls, so their change events do not edit the model.</summary>
		private bool _isFilling;
		private bool _isSaved;
		#endregion Fields

		public FrmCaptureSettings(CaptureSettingsModel model) {
			InitializeComponent();
			_model=model??throw new ArgumentNullException(nameof(model));
			Load+=FrmCaptureSettings_Load;
			FormClosing+=FrmCaptureSettings_FormClosing;
			gridSettings.SelectionCommitted+=gridSettings_SelectionCommitted;
			gridSettings.CellClick+=gridSettings_CellClick;
			textValue.TextChanged+=textValue_TextChanged;
			comboValue.SelectionChangeCommitted+=comboValue_SelectionChangeCommitted;
		}

		///<summary>Opens the screen for a saved device row, modal. Needs Setup (the refusal is shown). Returns true when settings were saved.</summary>
		public static bool ShowForDevice(ImagingDevice imagingDevice) {
			if(imagingDevice==null || imagingDevice.ImagingDeviceNum<=0) {
				return false;
			}
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return false;
			}
			CaptureSettingsDevice device=new CaptureSettingsDevice {
				ImagingDeviceNum=imagingDevice.ImagingDeviceNum,
				Description=imagingDevice.Description??"",
				TwainName=imagingDevice.TwainName??"",
			};
			FrmCaptureSettings frmCaptureSettings=new FrmCaptureSettings(new CaptureSettingsModel(device,CaptureSettingsServices.CreateDefault()));
			frmCaptureSettings.ShowDialog();
			return frmCaptureSettings._isSaved;
		}

		#region Methods - Event Handlers
		private void FrmCaptureSettings_Load(object sender,EventArgs e) {
			Lang.F(this);
			_model.Load();
			_idxSelected=_model.ListRows.Count>0 ? 0 : -1;
			RefreshView();
		}

		private void FrmCaptureSettings_FormClosing(object sender,CancelEventArgs e) {
			if(_model.HasChanges && !MsgBox.Show(MsgBoxButtons.OKCancel,Lang.g(this,"Discard the changes made in this window?"))) {
				e.Cancel=true;
			}
		}

		private void gridSettings_SelectionCommitted(object sender,EventArgs e) {
			SelectRow(gridSettings.GetSelectedIndex());
		}

		private void gridSettings_CellClick(object sender,GridClickEventArgs e) {
			SelectRow(gridSettings.GetSelectedIndex());
		}

		private void textValue_TextChanged(object sender,EventArgs e) {
			if(_isFilling || _idxSelected<0) {
				return;
			}
			_model.SetText(_idxSelected,textValue.Text);
			RefreshView(isEditorKept:true);
		}

		private void comboValue_SelectionChangeCommitted(object sender,EventArgs e) {
			int idx=comboValue.SelectedIndex;
			if(_isFilling || _idxSelected<0 || idx<0 || idx>=_listComboValues.Count) {
				return;
			}
			_model.SetText(_idxSelected,_listComboValues[idx]);
			RefreshView(isEditorKept:true);
		}

		private void butUndo_Click(object sender,EventArgs e) {
			_model.UndoChanges();
			RefreshView();
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			if(_model.HasProblems) {
				MsgBox.Show(Lang.g(this,"Fix these values first:")+"\r\n"+string.Join("\r\n",_model.GetProblemTexts()));
				return;
			}
			List<CaptureSettingChange> listChanges=_model.GetPendingChanges();
			List<CaptureSettingChange> listChangesOutside=_model.GetChangesOutside();
			string question;
			if(listChanges.Count==0) {
				question=Lang.g(this,"Save without changes? This accepts the values in the file as reviewed and records them, with the settings hash, "
					+"in the security log.");
			}
			else {
				question=Lang.g(this,"Save these changes to capture.ini on this computer?")+"\r\n\r\n"
					+string.Join("\r\n",listChanges.Select(x => x.GetText()+(x.IsGated ? " "+Lang.g(this,"(gated)") : "")))+"\r\n\r\n"
					+Lang.g(this,"The save is recorded in the security log.");
			}
			if(listChangesOutside.Count>0) {
				//Design 9.3, 10.7: a value changed outside Open Dental is accepted only once it was shown, also by a save that edits another key.
				question+="\r\n\r\n"+Lang.g(this,"These gated values were changed outside Open Dental since the last save. Saving accepts them as they "
					+"are in the file (last saved value -> value in the file):")+"\r\n"+string.Join("\r\n",listChangesOutside.Select(x => x.GetText()));
			}
			if(!MsgBox.Show(MsgBoxButtons.OKCancel,question)) {
				return;
			}
			CaptureSettingsSaveResult result=_model.Save();
			string warnings=result.ListWarnings.Count==0 ? "" : "\r\n\r\n"+string.Join("\r\n",result.ListWarnings);
			if(result.IsFileChangedSinceLoad) {
				if(MsgBox.Show(MsgBoxButtons.OKCancel,result.Error+"\r\n\r\n"+Lang.g(this,"Read the file again now? The changes made in this window are "
					+"discarded.")))
				{
					_model.Load();
				}
				RefreshView();
				return;
			}
			if(result.IsSaved) {
				_isSaved=true;
			}
			RefreshView();
			if(!result.IsSaved || !result.IsLogged) {
				MsgBox.Show(result.Error+warnings);
				return;
			}
			string shared=result.ListEntriesShared.Count==0 ? ""
				: "\r\n\r\n"+Lang.g(this,"The new settings hash was also recorded for the other devices that share the changed [Session] or [Twain] keys:")
					+" "+string.Join(", ",result.ListEntriesShared.Select(x => x.DeviceDescription+" ("+x.ImagingDeviceNum+")"))+".";
			MsgBox.Show(Lang.g(this,"The capture settings were saved and recorded in the security log.")+shared+warnings);
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}
		#endregion Methods - Event Handlers

		#region Methods - view
		private void SelectRow(int idx) {
			if(idx<0 || idx>=_model.ListRows.Count || idx==_idxSelected) {
				return;
			}
			_idxSelected=idx;
			FillEditor();
		}

		///<summary>Fills the grid and the labels from the model. isEditorKept: the value box is being typed in, so it is not refilled.</summary>
		private void RefreshView(bool isEditorKept=false) {
			_isFilling=true;
			try {
				labelDevice.Text=Lang.g(this,"Device")+": "+_model.Device.Description+" (ImagingDeviceNum "+_model.Device.ImagingDeviceNum+"), "
					+Lang.g(this,"TWAIN source")+" \""+_model.Device.TwainName+"\". "+Lang.g(this,"This computer")+": "+_model.ComputerName+".";
				labelFile.Text=_model.FileText;
				labelCheck.Text=_model.SettingsCheck==null ? "" : _model.SettingsCheck.Text;
				FillGrid();
				if(_idxSelected>=0) {
					gridSettings.SetSelected(_idxSelected);
				}
				if(!isEditorKept) {
					FillEditor();
				}
				else {
					FillEditorLabels();
				}
				List<string> listProblems=_model.GetProblemTexts();
				labelProblems.Text=listProblems.Count==0 ? "" : Lang.g(this,"Not saved until fixed:")+" "+string.Join(" ",listProblems);
				butSave.IsEnabled=!_model.HasProblems;
				butUndo.IsEnabled=_model.HasChanges;
			}
			finally {
				_isFilling=false;
			}
		}

		private void FillGrid() {
			gridSettings.BeginUpdate();
			gridSettings.Columns.Clear();
			gridSettings.Columns.Add(new GridColumn(Lang.g(this,"Setting"),200));
			gridSettings.Columns.Add(new GridColumn(Lang.g(this,"Value"),170));
			gridSettings.Columns.Add(new GridColumn(Lang.g(this,"Default"),100));
			gridSettings.Columns.Add(new GridColumn(Lang.g(this,"Gated"),50,System.Windows.HorizontalAlignment.Center));
			gridSettings.Columns.Add(new GridColumn(Lang.g(this,"Last saved"),110));
			GridColumn gridColumnNote=new GridColumn(Lang.g(this,"Note"),300);
			gridColumnNote.IsWidthDynamic=true;
			gridSettings.Columns.Add(gridColumnNote);
			gridSettings.ListGridRows.Clear();
			foreach(CaptureSettingRow row in _model.ListRows) {
				GridRow gridRow=new GridRow();
				gridRow.Cells.Add(row.DisplayName);
				gridRow.Cells.Add(row.Text);
				gridRow.Cells.Add(row.Def.DefaultText);
				gridRow.Cells.Add(row.Def.IsGated ? "X" : "");
				gridRow.Cells.Add(row.SavedText??"");
				gridRow.Cells.Add(GetNote(row));
				if(row.Problem!="") {
					gridRow.ColorText=_colorProblem;
				}
				if(row.IsChangedOutside) {
					gridRow.ColorBackG=_colorChangedOutside;
				}
				gridRow.Bold=row.IsChanged;
				gridSettings.ListGridRows.Add(gridRow);
			}
			gridSettings.EndUpdate();
		}

		private string GetNote(CaptureSettingRow row) {
			if(row.Problem!="") {
				return row.Problem;
			}
			List<string> listNotes=new List<string>();
			if(row.IsChangedOutside) {
				listNotes.Add(Lang.g(this,"changed outside Open Dental"));
			}
			if(row.IsChanged) {
				listNotes.Add(Lang.g(this,"edited, was")+" "+(row.BaselineText=="" ? Lang.g(this,"(empty)") : row.BaselineText));
			}
			return string.Join("; ",listNotes);
		}

		///<summary>The value editor for the selected row: a list for switches and choices, a text box otherwise.</summary>
		private void FillEditor() {
			bool isFillingBefore=_isFilling;
			_isFilling=true;
			try {
				CaptureSettingRow row=_idxSelected>=0 && _idxSelected<_model.ListRows.Count ? _model.ListRows[_idxSelected] : null;
				bool isList=row!=null && (row.Def.Kind==CaptureSettingKind.Switch || row.Def.Kind==CaptureSettingKind.Choice);
				comboValue.Visible=isList;
				textValue.Visible=row!=null && !isList;
				if(row!=null && isList) {
					_listComboValues=new List<string> { "" };
					_listComboValues.AddRange(row.Def.Kind==CaptureSettingKind.Switch ? new List<string> { "0","1" } : row.Def.ArrayChoices.ToList());
					if(row.Text!="" && !_listComboValues.Contains(row.Text)) {
						_listComboValues.Add(row.Text);//as written, for example "yes"
					}
					comboValue.Items.Clear();
					foreach(string value in _listComboValues) {
						comboValue.Items.Add(value=="" ? Lang.g(this,"(default)") : value);
					}
					comboValue.SelectedIndex=_listComboValues.IndexOf(row.Text);
				}
				else if(row!=null) {
					textValue.Text=row.Text;
				}
				FillEditorLabels();
			}
			finally {
				_isFilling=isFillingBefore;
			}
		}

		private void FillEditorLabels() {
			CaptureSettingRow row=_idxSelected>=0 && _idxSelected<_model.ListRows.Count ? _model.ListRows[_idxSelected] : null;
			if(row==null) {
				labelSelected.Text="";
				labelHelp.Text="";
				labelDefault.Text="";
				return;
			}
			labelSelected.Text=row.DisplayName+(row.Def.IsGated ? " "+Lang.g(this,"(gated)") : "");
			labelHelp.Text=row.Def.Help;
			string text=Lang.g(this,"Default")+": "+(row.Def.DefaultText=="" ? Lang.g(this,"(none)") : row.Def.DefaultText);
			if(row.SavedText!=null) {
				text+="; "+Lang.g(this,"last saved")+": "+(row.SavedText=="" ? Lang.g(this,"(empty)") : row.SavedText);
			}
			labelDefault.Text=text;
		}
		#endregion Methods - view
	}
}
