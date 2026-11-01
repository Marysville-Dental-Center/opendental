// The Database Maintenance tool "Invalid Characters (GDI+)" (Open Dental 25.2, backlog package B-DBM): scans chosen text columns for characters the
// WinForms GDI+ renderer can't draw (C0 control characters other than tab, CR and LF, unpaired surrogates, U+FFFE and U+FFFF), previews them and
// removes them. Signed rows are reported and never changed. The rules are in DatabaseMaintenances.InvalidCharsGdi.cs and DbmTextCleaner.cs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormDatabaseMaintInvalidChars:FormODBase {
		///<summary>False when the replication check was not passed: the window can scan but not fix.</summary>
		private bool _canFix;
		///<summary>The last scan, one entry per column scanned. Empty before the first scan.</summary>
		private List<DbmInvalidCharResult> _listResults=new List<DbmInvalidCharResult>();
		///<summary>True once a fix has run, so the caller invalidates the caches and saves LogText to the DBM log.</summary>
		public bool HasFixed;
		///<summary>The results of every fix run in this window, for the DBM log.</summary>
		public string LogText="";

		public FormDatabaseMaintInvalidChars(bool canFix) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_canFix=canFix;
		}

		private void FormDatabaseMaintInvalidChars_Load(object sender,EventArgs e) {
			labelExplain.Text=Lan.g(this,"Finds characters that grids, the chart and printouts can't draw: control characters other than tab and line "
				+"breaks, broken surrogate pairs, and U+FFFE and U+FFFF. Select the columns, click Scan to count them and see the first 50 values, then "
				+"Fix to remove them. Signed procedure notes, signed commlogs and fields of signed sheets are only reported, because removing a character "
				+"would invalidate the signature.");
			if(!_canFix) {
				labelCannotFix.Text=Lan.g(this,"Fix is disabled: the replication check was not passed.");
			}
			labelCannotFix.Visible=!_canFix;
			FillGridColumns();
			gridColumns.SetAll(true);
			FillGridPreview();
			butFix.Enabled=false;
		}

		private void FillGridColumns() {
			List<string> listLabelsSelected=gridColumns.SelectedTags<string>();
			gridColumns.BeginUpdate();
			gridColumns.Columns.Clear();
			gridColumns.Columns.Add(new GridColumn(Lan.g(this,"Column"),200));
			gridColumns.Columns.Add(new GridColumn(Lan.g(this,"Values"),70,HorizontalAlignment.Right));
			gridColumns.Columns.Add(new GridColumn(Lan.g(this,"Characters"),80,HorizontalAlignment.Right));
			gridColumns.Columns.Add(new GridColumn(Lan.g(this,"Signed (not changed)"),130,HorizontalAlignment.Right){ IsWidthDynamic=true });
			gridColumns.ListGridRows.Clear();
			List<DbmInvalidCharTarget> listTargets=DatabaseMaintenances.GetInvalidCharTargets();
			for(int i=0;i<listTargets.Count;i++) {
				GridRow row=new GridRow();
				row.Cells.Add(listTargets[i].Label);
				DbmInvalidCharResult result=_listResults.FirstOrDefault(x => x.Label==listTargets[i].Label);
				if(result==null) {
					row.Cells.Add("");
					row.Cells.Add("");
					row.Cells.Add("");
				}
				else {
					row.Cells.Add(result.CountRows.ToString());
					row.Cells.Add(result.CountChars.ToString());
					row.Cells.Add(result.CountRowsSigned.ToString());
				}
				row.Tag=listTargets[i].Label;
				gridColumns.ListGridRows.Add(row);
			}
			gridColumns.EndUpdate();
			for(int i=0;i<gridColumns.ListGridRows.Count;i++) {
				if(listLabelsSelected.Contains((string)gridColumns.ListGridRows[i].Tag)) {
					gridColumns.SetSelected(i,true);
				}
			}
		}

		private void FillGridPreview() {
			gridPreview.BeginUpdate();
			gridPreview.Columns.Clear();
			gridPreview.Columns.Add(new GridColumn(Lan.g(this,"Column"),160));
			gridPreview.Columns.Add(new GridColumn(Lan.g(this,"Row"),80,HorizontalAlignment.Right));
			gridPreview.Columns.Add(new GridColumn(Lan.g(this,"Signed"),50,HorizontalAlignment.Center));
			gridPreview.Columns.Add(new GridColumn(Lan.g(this,"Value"),300){ IsWidthDynamic=true });
			gridPreview.ListGridRows.Clear();
			List<DbmInvalidCharPreview> listPreviews=_listResults.SelectMany(x => x.ListPreviews).ToList();
			for(int i=0;i<listPreviews.Count;i++) {
				GridRow row=new GridRow();
				row.Cells.Add(listPreviews[i].Label);
				row.Cells.Add(listPreviews[i].PriKey.ToString());
				row.Cells.Add(listPreviews[i].IsSigned ? "X" : "");
				row.Cells.Add(listPreviews[i].ValueShown);
				gridPreview.ListGridRows.Add(row);
			}
			gridPreview.EndUpdate();
		}

		private void butScan_Click(object sender,EventArgs e) {
			List<string> listLabels=gridColumns.SelectedTags<string>();
			if(listLabels.Count==0) {
				MsgBox.Show(this,"Select at least one column.");
				return;
			}
			Scan(listLabels);
		}

		private void Scan(List<string> listLabels) {
			Cursor=Cursors.WaitCursor;
			try {
				_listResults=DatabaseMaintenances.InvalidCharsGdiScan(listLabels);
			}
			finally {
				Cursor=Cursors.Default;
			}
			FillGridColumns();
			FillGridPreview();
			labelSummary.Text=Lan.g(this,"Values with invalid characters:")+" "+_listResults.Sum(x => x.CountRows)+"   "
				+Lan.g(this,"Characters:")+" "+_listResults.Sum(x => x.CountChars)+"   "
				+Lan.g(this,"Signed values (not changed):")+" "+_listResults.Sum(x => x.CountRowsSigned);
			butFix.Enabled=_canFix && _listResults.Any(x => x.CountRows>0);
		}

		private void butFix_Click(object sender,EventArgs e) {
			List<string> listLabels=_listResults.FindAll(x => x.CountRows>0).Select(x => x.Label).ToList();
			if(!_canFix || listLabels.Count==0) {
				return;
			}
			string msg=Lan.g(this,"Make a backup of the database before you continue.")+"\r\n\r\n"
				+Lan.g(this,"This removes")+" "+_listResults.Sum(x => x.CountChars)+" "+Lan.g(this,"characters from")+" "+_listResults.Sum(x => x.CountRows)
				+" "+Lan.g(this,"values and cannot be undone. Signed values are never changed.")+"\r\n"+Lan.g(this,"Continue?");
			if(MessageBox.Show(msg,Lan.g(this,"Invalid Characters (GDI+)"),MessageBoxButtons.OKCancel)!=DialogResult.OK) {
				return;
			}
			List<DbmInvalidCharResult> listResultsFixed=null;
			ProgressWin progressOD=new ProgressWin();
			progressOD.ActionMain=() => listResultsFixed=DatabaseMaintenances.InvalidCharsGdiFix(listLabels);
			try {
				progressOD.ShowDialog();
			}
			catch(Exception ex) {
				FriendlyException.Show(Lan.g(this,"Error removing invalid characters."),ex);
			}
			if(listResultsFixed==null) {
				Scan(_listResults.Select(x => x.Label).ToList());
				return;
			}
			HasFixed=true;
			string logText=DatabaseMaintenances.GetInvalidCharsGdiLogText(listResultsFixed);
			LogText+=logText+"\r\n";
			Scan(_listResults.Select(x => x.Label).ToList());
			MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(logText);
			msgBoxCopyPaste.ShowDialog();
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}
	}
}
