// The Statement Options window (Open Dental 26.2 release notes: "Moved Statement dropdown options to a new window"; package P262-BILLING,
// docs/port-notes/P262-BILLING.md). Opened by the dropdown arrow of the Account module's Statement button (ControlAccount.StatementOptions.cs).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Lists the kinds of statement with a description of each. The caller adds the choices and runs the one at IndexSelected after OK.
	///</summary>
	public partial class FormStatementChoices:FormODBase {
		private List<string> _listTitles=new List<string>();
		private List<string> _listDescriptions=new List<string>();
		///<summary>The index of the chosen entry, in the order AddChoice was called. -1 when nothing was chosen.</summary>
		public int IndexSelected=-1;

		public FormStatementChoices() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		///<summary>Adds a choice. Call before ShowDialog.</summary>
		public void AddChoice(string title,string description) {
			_listTitles.Add(title??"");
			_listDescriptions.Add(description??"");
		}

		private void FormStatementChoices_Load(object sender,EventArgs e) {
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			GridColumn gridColumn=new GridColumn(Lan.g(gridMain.TranslationName,"Statement"),150);
			gridMain.Columns.Add(gridColumn);
			gridColumn=new GridColumn(Lan.g(gridMain.TranslationName,"Description"),200);
			gridColumn.IsWidthDynamic=true;
			gridMain.Columns.Add(gridColumn);
			gridMain.ListGridRows.Clear();
			for(int i=0;i<_listTitles.Count;i++) {
				GridRow gridRow=new GridRow();
				gridRow.Cells.Add(_listTitles[i]);
				gridRow.Cells.Add(_listDescriptions[i]);
				gridMain.ListGridRows.Add(gridRow);
			}
			gridMain.EndUpdate();
			if(gridMain.ListGridRows.Count>0) {
				gridMain.SetSelected(0,true);
			}
		}

		private void gridMain_CellDoubleClick(object sender,ODGridClickEventArgs e) {
			if(e.Row<0) {
				return;
			}
			IndexSelected=e.Row;
			DialogResult=DialogResult.OK;
		}

		private void butOK_Click(object sender,EventArgs e) {
			if(gridMain.GetSelectedIndex()<0) {
				MsgBox.Show(this,"Please select a statement first.");
				return;
			}
			IndexSelected=gridMain.GetSelectedIndex();
			DialogResult=DialogResult.OK;
		}
	}
}
