// Setup of the UDS+ report (Open Dental 25.4, package M2-UDS in docs/specs/port-backlog.md): the UDS dental service lines and their procedure codes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Edits the UdsPlusDentalLineCodes preference: one row per UDS dental service line with the procedure codes counted on it. HRSA revises
	///the lines every year and this program ships no codes, so the office enters them from the UDS manual of the reporting year. Needs the Setup
	///permission (the same as Report Setup); saving logs a Setup entry.</summary>
	public partial class FormRpUdsPlusSetup:FormODBase {
		private const int COLUMN_LINE=0;
		private const int COLUMN_DESCRIPTION=1;
		private const int COLUMN_CODES=2;
		///<summary>The value the window opened with, to tell whether anything changed.</summary>
		private string _jsonOld;

		public FormRpUdsPlusSetup() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormRpUdsPlusSetup_Load(object sender,EventArgs e) {
			_jsonOld=PrefC.GetString(PrefName.UdsPlusDentalLineCodes);
			if(!UdsPlusLineMap.TryParse(_jsonOld,out UdsPlusLineMap udsPlusLineMap,out string error)) {
				//Keep the unreadable text where the user can see and copy it before replacing it.
				using MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(error+"\r\n\r\n"
					+Lan.g(this,"The saved setting follows. Saving this window replaces it.")+"\r\n\r\n"+_jsonOld);
				msgBoxCopyPaste.ShowDialog();
				udsPlusLineMap=new UdsPlusLineMap();
			}
			FillGrid(udsPlusLineMap.ListLines);
		}

		private void FillGrid(List<UdsPlusLine> listLines) {
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"UDS Line"),70) { IsEditable=true });
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Service"),250) { IsEditable=true });
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Procedure Codes"),400) { IsEditable=true });
			gridMain.ListGridRows.Clear();
			foreach(UdsPlusLine udsPlusLine in listLines) {
				GridRow gridRow=new GridRow();
				gridRow.Cells.Add(udsPlusLine.Line);
				gridRow.Cells.Add(udsPlusLine.Description);
				gridRow.Cells.Add(udsPlusLine.Codes);
				gridMain.ListGridRows.Add(gridRow);
			}
			gridMain.EndUpdate();
		}

		///<summary>The lines as the grid shows them now, including edits.</summary>
		private List<UdsPlusLine> GetLinesFromGrid() {
			List<UdsPlusLine> listLines=new List<UdsPlusLine>();
			foreach(GridRow gridRow in gridMain.ListGridRows) {
				listLines.Add(new UdsPlusLine {
					Line=gridRow.Cells[COLUMN_LINE].Text.Trim(),
					Description=gridRow.Cells[COLUMN_DESCRIPTION].Text.Trim(),
					Codes=gridRow.Cells[COLUMN_CODES].Text.Trim(),
				});
			}
			return listLines;
		}

		private void butAdd_Click(object sender,EventArgs e) {
			List<UdsPlusLine> listLines=GetLinesFromGrid();
			listLines.Add(new UdsPlusLine());
			FillGrid(listLines);
			gridMain.SetSelected(new System.Drawing.Point(COLUMN_LINE,listLines.Count-1));
		}

		private void butDelete_Click(object sender,EventArgs e) {
			int row=gridMain.SelectedCell.Y;
			if(row<0 || row>=gridMain.ListGridRows.Count) {
				MsgBox.Show(this,"Please select a line first.");
				return;
			}
			List<UdsPlusLine> listLines=GetLinesFromGrid();
			listLines.RemoveAt(row);
			FillGrid(listLines);
		}

		private void butTemplate_Click(object sender,EventArgs e) {
			List<UdsPlusLine> listLines=GetLinesFromGrid();
			int countAdded=0;
			foreach(UdsPlusLine udsPlusLine in UdsPlusLineMap.GetTable6ALineTemplate()) {
				if(listLines.Any(x => x.Line.Equals(udsPlusLine.Line,StringComparison.OrdinalIgnoreCase))) {
					continue;
				}
				listLines.Add(udsPlusLine);
				countAdded++;
			}
			FillGrid(listLines);
			if(countAdded==0) {
				MsgBox.Show(this,"Every line of the list is already here.");
				return;
			}
			MsgBox.Show(this,"Line numbers and names were added without codes. Check them against the UDS manual for the reporting year and enter "
				+"the procedure codes of each line.");
		}

		private void butSave_Click(object sender,EventArgs e) {
			UdsPlusLineMap udsPlusLineMap=new UdsPlusLineMap();
			udsPlusLineMap.ListLines=GetLinesFromGrid()
				.FindAll(x => x.Line!="" || x.Description!="" || x.Codes!="");//A row left completely blank is dropped.
			List<string> listErrors=udsPlusLineMap.Validate();
			if(listErrors.Count>0) {
				using MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(Lan.g(this,"Please fix these lines first:")+"\r\n"+string.Join("\r\n",listErrors));
				msgBoxCopyPaste.ShowDialog();
				return;
			}
			string json=udsPlusLineMap.ToJson();
			if(json!=_jsonOld) {
				if(Prefs.UpdateString(PrefName.UdsPlusDentalLineCodes,json)) {
					DataValid.SetInvalid(InvalidType.Prefs);
				}
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"UDS+ report setup changed: "+udsPlusLineMap.ListLines.Count+" UDS lines.");
			}
			DialogResult=DialogResult.OK;
		}
	}
}
