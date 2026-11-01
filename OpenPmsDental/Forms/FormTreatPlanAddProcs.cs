// Add Procedures window for the Treatment Plan module's Add Procs button (Open Dental 25.3, "easier adding of procedures to the active treatment
// plan", backlog package B-TP). The user picks codes (code search, typed code, the Chart's quick buttons) and types the teeth, surfaces or area
// each code needs; rows that are missing something are marked and block OK. The caller creates the procedures with ProcAddHelper.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormTreatPlanAddProcs:FormODBase {
		///<summary>Set when DialogResult is OK: the rows to add, every one valid.</summary>
		public List<ProcAddEntry> ListProcAddEntries=new List<ProcAddEntry>();
		///<summary>Set when DialogResult is OK: the TxPriorities DefNum for every new procedure, or 0 for no priority.</summary>
		public long PriorityNum;
		private Patient _patient;
		///<summary>The Active plan the procedures go to. Null when the patient has no treatment plan yet (an Active plan will be created).</summary>
		private TreatPlan _treatPlan;
		private List<ProcAddEntry> _listProcAddEntries=new List<ProcAddEntry>();
		private List<ProcButtonQuick> _listProcButtonQuicks=new List<ProcButtonQuick>();
		private const int COL_TOOTH=3;
		private const int COL_SURF=4;
		private const int COL_NOTICE=5;

		public FormTreatPlanAddProcs(Patient patient,TreatPlan treatPlan) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_patient=patient;
			_treatPlan=treatPlan;
		}

		private void FormTreatPlanAddProcs_Load(object sender,EventArgs e) {
			if(_treatPlan==null) {
				labelPlan.Text=_patient.GetNameFL()+": "+Lan.g(this,"an Active treatment plan will be created.");
			}
			else {
				labelPlan.Text=_patient.GetNameFL()+": "+Lan.g(this,"adding to")+" "+_treatPlan.Heading;
			}
			comboPriority.Items.AddDefNone(Lan.g(this,"no priority"));
			comboPriority.Items.AddDefs(Defs.GetDefsForCategory(DefCat.TxPriorities,true));
			comboPriority.SelectedIndex=0;
			_listProcButtonQuicks=ProcAddHelper.GetQuickButtons(ProcButtonQuicks.GetAll());
			FillGridQuick();
			FillGrid();
		}

		private void FillGridQuick() {
			gridQuick.BeginUpdate();
			gridQuick.Columns.Clear();
			gridQuick.Columns.Add(new GridColumn(Lan.g("TableTreatPlanAddProcs","Button"),100));
			gridQuick.Columns.Add(new GridColumn(Lan.g("TableTreatPlanAddProcs","Code"),70));
			gridQuick.ListGridRows.Clear();
			for(int i=0;i<_listProcButtonQuicks.Count;i++) {
				GridRow row=new GridRow();
				row.Cells.Add(_listProcButtonQuicks[i].Description);
				string code=_listProcButtonQuicks[i].CodeValue;
				if(!string.IsNullOrWhiteSpace(_listProcButtonQuicks[i].Surf)) {
					code+=" "+_listProcButtonQuicks[i].Surf;
				}
				row.Cells.Add(code);
				row.Tag=_listProcButtonQuicks[i];
				gridQuick.ListGridRows.Add(row);
			}
			gridQuick.EndUpdate();
		}

		private void FillGrid() {
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g("TableTreatPlanAddProcs","Code"),70));
			gridMain.Columns.Add(new GridColumn(Lan.g("TableTreatPlanAddProcs","Description"),190));
			gridMain.Columns.Add(new GridColumn(Lan.g("TableTreatPlanAddProcs","Area"),75));
			GridColumn col=new GridColumn(Lan.g("TableTreatPlanAddProcs","Tooth"),70);
			col.IsEditable=true;
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g("TableTreatPlanAddProcs","Surf/Area"),70);
			col.IsEditable=true;
			gridMain.Columns.Add(col);
			col=new GridColumn(Lan.g("TableTreatPlanAddProcs","Notice"),170);
			col.IsWidthDynamic=true;
			gridMain.Columns.Add(col);
			gridMain.ListGridRows.Clear();
			for(int i=0;i<_listProcAddEntries.Count;i++) {
				ProcedureCode procedureCode=ProcedureCodes.GetProcCode(_listProcAddEntries[i].CodeNum);
				GridRow row=new GridRow();
				row.Cells.Add(procedureCode.ProcCode);
				row.Cells.Add(procedureCode.Descript);
				row.Cells.Add(ProcAddHelper.GetTreatAreaText(procedureCode.TreatArea));
				row.Cells.Add(_listProcAddEntries[i].ToothEntry);
				row.Cells.Add(_listProcAddEntries[i].SurfEntry);
				row.Cells.Add("");
				row.Tag=_listProcAddEntries[i];
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
			RefreshNotices();
		}

		///<summary>Copies what was typed in the Tooth and Surf/Area cells into the rows.</summary>
		private void SyncEntriesFromGrid() {
			for(int i=0;i<gridMain.ListGridRows.Count && i<_listProcAddEntries.Count;i++) {
				_listProcAddEntries[i].ToothEntry=gridMain.ListGridRows[i].Cells[COL_TOOTH].Text.Trim();
				_listProcAddEntries[i].SurfEntry=gridMain.ListGridRows[i].Cells[COL_SURF].Text.Trim().ToUpper();
			}
		}

		///<summary>Shows each row's problem in red, or how many procedures it will add.</summary>
		private void RefreshNotices() {
			ToothNumberingNomenclature toothNumberingNomenclature=(ToothNumberingNomenclature)PrefC.GetInt(PrefName.UseInternationalToothNumbers);
			for(int i=0;i<gridMain.ListGridRows.Count && i<_listProcAddEntries.Count;i++) {
				GridCell gridCell=gridMain.ListGridRows[i].Cells[COL_NOTICE];
				string error=ProcAddHelper.GetEntryError(_listProcAddEntries[i],toothNumberingNomenclature);
				if(error!="") {
					gridCell.Text=error;
					gridCell.ColorText=Color.Red;
					continue;
				}
				ProcedureCode procedureCode=ProcedureCodes.GetProcCode(_listProcAddEntries[i].CodeNum);
				int count=ProcAddHelper.GetPlacements(procedureCode,_listProcAddEntries[i].ToothEntry,_listProcAddEntries[i].SurfEntry,
					toothNumberingNomenclature,out string _).Count;
				gridCell.Text=(count==1 ? Lan.g(this,"Ready") : Lan.g(this,"Adds")+" "+count+" "+Lan.g(this,"procedures"));
				gridCell.ColorText=Color.Empty;
			}
			gridMain.Invalidate();
		}

		private void AddEntry(ProcAddEntry procAddEntry) {
			ProcedureCode procedureCode=ProcedureCodes.GetProcCode(procAddEntry.CodeNum);
			string error=ProcAddHelper.GetCodeError(procedureCode);
			if(error!="") {
				MessageBox.Show(procedureCode.ProcCode+": "+error);
				return;
			}
			SyncEntriesFromGrid();
			_listProcAddEntries.Add(procAddEntry);
			FillGrid();
			//Put the cursor where the new row needs typing, so a code can be followed straight by its tooth or area.
			int col=-1;
			if(procedureCode.TreatArea.In(TreatmentArea.Surf,TreatmentArea.Tooth,TreatmentArea.ToothRange)) {
				col=COL_TOOTH;
			}
			else if(ProcAddHelper.UsesSurfEntry(procedureCode) && procAddEntry.SurfEntry=="") {
				col=COL_SURF;
			}
			if(col>=0) {
				gridMain.SetSelected(new Point(col,_listProcAddEntries.Count-1));
			}
		}

		private void gridQuick_CellClick(object sender,ODGridClickEventArgs e) {
			if(e.Row<0 || e.Row>=gridQuick.ListGridRows.Count) {
				return;
			}
			AddEntry(ProcAddHelper.GetEntryForQuickButton((ProcButtonQuick)gridQuick.ListGridRows[e.Row].Tag));
		}

		private void butAddCode_Click(object sender,EventArgs e) {
			long codeNum=ProcAddHelper.FindTypedCode(textCode.Text);
			if(codeNum==0) {
				MsgBox.Show(this,"Invalid code.");
				textCode.SelectionStart=textCode.Text.Length;
				return;
			}
			AddEntry(new ProcAddEntry { CodeNum=codeNum });
			textCode.Text="";
			textCode.Focus();
		}

		private void textCode_KeyDown(object sender,KeyEventArgs e) {
			if(e.KeyCode!=Keys.Enter) {
				return;
			}
			e.SuppressKeyPress=true;
			butAddCode_Click(this,EventArgs.Empty);
		}

		private void butSearch_Click(object sender,EventArgs e) {
			using FormProcCodes formProcCodes=new FormProcCodes();
			formProcCodes.IsSelectionMode=true;
			formProcCodes.CanAllowMultipleSelections=true;
			formProcCodes.ShowDialog();
			if(formProcCodes.DialogResult!=DialogResult.OK) {
				return;
			}
			for(int i=0;i<formProcCodes.ListProcedureCodesSelected.Count;i++) {
				AddEntry(new ProcAddEntry { CodeNum=formProcCodes.ListProcedureCodesSelected[i].CodeNum });
			}
		}

		private void gridMain_CellLeave(object sender,ODGridClickEventArgs e) {
			SyncEntriesFromGrid();
			RefreshNotices();
		}

		private void butRemove_Click(object sender,EventArgs e) {
			int idx=gridMain.GetSelectedIndex();
			if(idx<0 && gridMain.SelectedCell.Y>=0) {
				idx=gridMain.SelectedCell.Y;
			}
			if(idx<0 || idx>=_listProcAddEntries.Count) {
				MsgBox.Show(this,"Select a row first.");
				return;
			}
			SyncEntriesFromGrid();
			_listProcAddEntries.RemoveAt(idx);
			FillGrid();
		}

		private void butOK_Click(object sender,EventArgs e) {
			SyncEntriesFromGrid();
			RefreshNotices();
			if(_listProcAddEntries.Count==0) {
				MsgBox.Show(this,"Add at least one procedure first.");
				return;
			}
			List<string> listErrors=new List<string>();
			for(int i=0;i<_listProcAddEntries.Count;i++) {
				string error=ProcAddHelper.GetEntryError(_listProcAddEntries[i]);
				if(error!="") {
					listErrors.Add(Lan.g(this,"Row")+" "+(i+1)+" ("+ProcedureCodes.GetStringProcCode(_listProcAddEntries[i].CodeNum)+"): "+error);
				}
			}
			if(listErrors.Count>0) {
				MessageBox.Show(Lan.g(this,"Fix these rows first:")+"\r\n"+string.Join("\r\n",listErrors));
				return;
			}
			if(_treatPlan!=null) {//Another workstation may have changed the plan while this window was open.
				string error=ProcAddHelper.GetTargetError(TreatPlans.GetOne(_treatPlan.TreatPlanNum),_patient.PatNum);
				if(error!="") {
					MessageBox.Show(error);
					return;
				}
			}
			ListProcAddEntries=_listProcAddEntries.Select(x => x.Copy()).ToList();
			PriorityNum=comboPriority.GetSelectedDefNum();
			DialogResult=DialogResult.OK;
		}
	}
}
