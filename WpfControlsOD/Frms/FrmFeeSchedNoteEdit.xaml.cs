using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using OpenDentBusiness;
using WpfControls.UI;

namespace OpenDental {
	///<summary>Adds, edits or deletes one fee schedule note. Needs FeeSchedEdit to save or delete. The entering user and entry date are set when the
	///note is inserted and never change; the Date box is the note's own date.</summary>
	public partial class FrmFeeSchedNoteEdit:FrmODBase {
		///<summary>Required. For a new note, set FeeSchedNum (and optionally DateEntry and ClinicNums) and IsNew.</summary>
		public FeeSchedNote FeeSchedNoteCur;
		public bool IsNew;
		///<summary>True to only view the note.</summary>
		public bool IsReadOnly;
		///<summary>Clinics in the note that this user cannot see in the clinic box (clinic-restricted users). Kept as they are on save.</summary>
		private List<long> _listClinicNumsHidden=new List<long>();

		public FrmFeeSchedNoteEdit() {
			InitializeComponent();
			Load+=FrmFeeSchedNoteEdit_Load;
			PreviewKeyDown+=FrmFeeSchedNoteEdit_PreviewKeyDown;
		}

		private void FrmFeeSchedNoteEdit_Load(object sender,EventArgs e) {
			Lang.F(this);
			labelFeeSched.Text=Lans.g(this,"Fee Schedule")+": "+FeeScheds.GetDescription(FeeSchedNoteCur.FeeSchedNum);
			if(FeeSchedNoteCur.DateEntry.Year>1880) {
				textVDateEntry.Text=FeeSchedNoteCur.DateEntry.ToShortDateString();
			}
			textNote.Text=FeeSchedNoteCur.Note;
			List<long> listClinicNums=FeeSchedNotes.GetClinicNums(FeeSchedNoteCur);
			if(PrefC.HasClinicsEnabled) {
				List<long> listClinicNumsShown=comboClinics.ListClinics.Select(x => x.ClinicNum).ToList();
				_listClinicNumsHidden=listClinicNums.FindAll(x => !listClinicNumsShown.Contains(x));
				comboClinics.ListClinicNumsSelected=listClinicNums.FindAll(x => listClinicNumsShown.Contains(x));//empty selects nothing: all clinics
			}
			else {
				labelClinics.Visible=false;
				labelClinicsAll.Visible=false;
				_listClinicNumsHidden=listClinicNums;//clinics off: keep whatever the note has
			}
			if(IsNew) {
				butDelete.IsEnabled=false;
			}
			if(IsReadOnly) {
				butDelete.IsEnabled=false;
				butSave.IsEnabled=false;
				textVDateEntry.IsEnabled=false;
				comboClinics.IsEnabled=false;
				textNote.ReadOnly=true;
			}
		}

		private void FrmFeeSchedNoteEdit_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butSave.IsAltKey(Key.S,e)) {
				butSave_Click(this,new EventArgs());
			}
			if(butDelete.IsAltKey(Key.D,e)) {
				butDelete_Click(this,new EventArgs());
			}
		}

		private void butDelete_Click(object sender,EventArgs e) {
			if(IsNew || IsReadOnly) {
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.FeeSchedEdit)) {
				return;
			}
			if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Delete this fee schedule note?")) {
				return;
			}
			FeeSchedNotes.Delete(FeeSchedNoteCur.FeeSchedNoteNum);
			FeeSchedNotes.MakeLogEntry(FeeSchedNoteCur,Lans.g(this,"deleted"));
			IsDialogOK=true;
		}

		private void butSave_Click(object sender,EventArgs e) {
			if(IsReadOnly) {
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.FeeSchedEdit)) {
				return;
			}
			if(!textVDateEntry.IsValid()) {
				MsgBox.Show(this,"Please fix the date.");
				return;
			}
			if(textVDateEntry.Text=="") {
				MsgBox.Show(this,"Please enter a date.");
				return;
			}
			if(textNote.Text.Trim()=="") {
				MsgBox.Show(this,"Please enter a note.");
				return;
			}
			List<long> listClinicNums=new List<long>(_listClinicNumsHidden);
			if(PrefC.HasClinicsEnabled) {
				listClinicNums.AddRange(comboClinics.ListClinicNumsSelected);
			}
			FeeSchedNoteCur.DateEntry=PIn.Date(textVDateEntry.Text);
			FeeSchedNoteCur.ClinicNums=FeeSchedNotes.ClinicNumsToString(listClinicNums);
			FeeSchedNoteCur.Note=textNote.Text;
			if(IsNew) {
				FeeSchedNotes.Insert(FeeSchedNoteCur);
				FeeSchedNotes.MakeLogEntry(FeeSchedNoteCur,Lans.g(this,"added"));
			}
			else {
				FeeSchedNotes.Update(FeeSchedNoteCur);
				FeeSchedNotes.MakeLogEntry(FeeSchedNoteCur,Lans.g(this,"edited"));
			}
			IsDialogOK=true;
		}
	}
}
