using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using OpenDentBusiness;
using WpfControls.UI;

namespace OpenDental {
	///<summary>The dated notes of one fee schedule (Open Dental 24.4 feature, re-implemented from the public release notes). Opened from Edit Fee
	///Schedule (Notes button) and, read-only, from the Procedure Codes window. Adding, editing and deleting need FeeSchedEdit; viewing needs no
	///permission.</summary>
	public partial class FrmFeeSchedNotes:FrmODBase {
		///<summary>Required. The fee schedule whose notes are shown.</summary>
		public long FeeSchedNum;
		///<summary>True to only view the notes: no Add, and the edit window opens read-only.</summary>
		public bool IsReadOnly;
		///<summary>The clinic to filter on when the window opens. -1 (the default) or 0 shows notes for every clinic.</summary>
		public long ClinicNumFilter=-1;
		private List<FeeSchedNote> _listFeeSchedNotes=new List<FeeSchedNote>();

		public FrmFeeSchedNotes() {
			InitializeComponent();
			Load+=FrmFeeSchedNotes_Load;
			gridMain.CellDoubleClick+=gridMain_CellDoubleClick;
			comboClinic.SelectionChangeCommitted+=comboClinic_SelectionChangeCommitted;
			PreviewKeyDown+=FrmFeeSchedNotes_PreviewKeyDown;
		}

		private void FrmFeeSchedNotes_Load(object sender,EventArgs e) {
			Lang.F(this);
			labelFeeSched.Text=Lans.g(this,"Fee Schedule")+": "+FeeScheds.GetDescription(FeeSchedNum);
			if(IsReadOnly) {
				butAdd.IsEnabled=false;
			}
			if(PrefC.HasClinicsEnabled) {
				if(ClinicNumFilter>0 && comboClinic.ListClinics.Any(x => x.ClinicNum==ClinicNumFilter)) {
					comboClinic.ClinicNumSelected=ClinicNumFilter;
				}
				else {
					comboClinic.IsAllSelected=true;
				}
			}
			FillGrid();
		}

		///<summary>-1 when every note is shown (clinics off, or All selected).</summary>
		private long GetClinicNumFilter() {
			if(!PrefC.HasClinicsEnabled || comboClinic.IsAllSelected || comboClinic.IsNothingSelected) {
				return -1;
			}
			return comboClinic.ClinicNumSelected;
		}

		private void FillGrid() {
			_listFeeSchedNotes=FeeSchedNotes.SortForDisplay(FeeSchedNotes.FilterForClinic(FeeSchedNotes.GetForFeeSched(FeeSchedNum),GetClinicNumFilter()));
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lans.g(this,"Date"),80));
			if(PrefC.HasClinicsEnabled) {
				gridMain.Columns.Add(new GridColumn(Lans.g(this,"Clinics"),150));
			}
			GridColumn gridColumn=new GridColumn(Lans.g(this,"Note"),200);
			gridColumn.IsWidthDynamic=true;
			gridMain.Columns.Add(gridColumn);
			gridMain.Columns.Add(new GridColumn(Lans.g(this,"Entered By"),100));
			gridMain.ListGridRows.Clear();
			for(int i=0;i<_listFeeSchedNotes.Count;i++) {
				FeeSchedNote feeSchedNote=_listFeeSchedNotes[i];
				GridRow gridRow=new GridRow();
				gridRow.Cells.Add(feeSchedNote.DateEntry.Year<1880 ? "" : feeSchedNote.DateEntry.ToShortDateString());
				if(PrefC.HasClinicsEnabled) {
					gridRow.Cells.Add(GetClinicsText(feeSchedNote));
				}
				gridRow.Cells.Add(FeeSchedNotes.GetNoteSummary(feeSchedNote));
				gridRow.Cells.Add(Userods.GetName(feeSchedNote.SecUserNumEntry));
				gridRow.Tag=feeSchedNote;
				gridMain.ListGridRows.Add(gridRow);
			}
			gridMain.EndUpdate();
		}

		private string GetClinicsText(FeeSchedNote feeSchedNote) {
			List<long> listClinicNums=FeeSchedNotes.GetClinicNums(feeSchedNote);
			if(listClinicNums.Count==0) {
				return Lans.g(this,"All");
			}
			return string.Join(", ",listClinicNums.Select(x => Clinics.GetAbbr(x)).Where(x => x!=""));
		}

		private void comboClinic_SelectionChangeCommitted(object sender,EventArgs e) {
			FillGrid();
		}

		private void gridMain_CellDoubleClick(object sender,GridClickEventArgs e) {
			if(e.Row<0 || e.Row>=gridMain.ListGridRows.Count) {
				return;
			}
			FeeSchedNote feeSchedNote=(FeeSchedNote)gridMain.ListGridRows[e.Row].Tag;
			FrmFeeSchedNoteEdit frmFeeSchedNoteEdit=new FrmFeeSchedNoteEdit();
			frmFeeSchedNoteEdit.FeeSchedNoteCur=feeSchedNote.Copy();
			frmFeeSchedNoteEdit.IsReadOnly=IsReadOnly || !Security.IsAuthorized(EnumPermType.FeeSchedEdit,true);
			frmFeeSchedNoteEdit.ShowDialog();
			if(frmFeeSchedNoteEdit.IsDialogOK) {
				FillGrid();
			}
		}

		private void butAdd_Click(object sender,EventArgs e) {
			if(IsReadOnly || !Security.IsAuthorized(EnumPermType.FeeSchedEdit)) {
				return;
			}
			FeeSchedNote feeSchedNote=new FeeSchedNote();
			feeSchedNote.FeeSchedNum=FeeSchedNum;
			feeSchedNote.DateEntry=DateTime.Today;
			long clinicNumFilter=GetClinicNumFilter();
			if(clinicNumFilter>0) {
				feeSchedNote.ClinicNums=FeeSchedNotes.ClinicNumsToString(new List<long> { clinicNumFilter });
			}
			FrmFeeSchedNoteEdit frmFeeSchedNoteEdit=new FrmFeeSchedNoteEdit();
			frmFeeSchedNoteEdit.FeeSchedNoteCur=feeSchedNote;
			frmFeeSchedNoteEdit.IsNew=true;
			frmFeeSchedNoteEdit.ShowDialog();
			if(frmFeeSchedNoteEdit.IsDialogOK) {
				FillGrid();
			}
		}

		private void FrmFeeSchedNotes_PreviewKeyDown(object sender,KeyEventArgs e) {
			if(butAdd.IsAltKey(Key.A,e)) {
				butAdd_Click(this,new EventArgs());
			}
			if(butClose.IsAltKey(Key.C,e)) {
				butClose_Click(this,new EventArgs());
			}
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}
	}
}
