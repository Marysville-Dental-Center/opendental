// The Database Maintenance tool Conversion Payment Dates (Open Dental 26.2 item 24, "New database maintenance tool to clean up incorrectly dated
// conversion payments"; package P262-TOOLS). On demand only: opened from the Tools tab. Lists the income transfers dated after the conversion date
// that the user enters, selects the ones that only move money of the converted history, and dates the chosen ones (and their splits) on the
// conversion date. The rules are in DatabaseMaintenances.ConversionPayments.cs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	public partial class FormDatabaseMaintConvPayDates:FormODBase {
		///<summary>False when the replication check was not passed: the window can list but not change dates.</summary>
		private bool _canFix;
		///<summary>The income transfers of the last Find.</summary>
		private List<DbmConvPaymentDateRow> _listRows=new List<DbmConvPaymentDateRow>();
		///<summary>The conversion date of the last Find. MinValue before the first.</summary>
		private DateTime _dateListed=DateTime.MinValue;
		///<summary>True once dates were changed, so the caller saves LogText to the DBM log.</summary>
		public bool HasFixed;
		///<summary>The results of every change made in this window, for the DBM log.</summary>
		public string LogText="";

		public FormDatabaseMaintConvPayDates(bool canFix) {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
			_canFix=canFix;
		}

		private void FormDatabaseMaintConvPayDates_Load(object sender,EventArgs e) {
			labelExplain.Text=Lan.g(this,"After a data conversion, the income transfers that clean up the converted history are dated on the conversion "
				+"date, so that they do not change the reports of the days after it. Enter the conversion date and click Find: the list shows the income "
				+"transfers dated after it (or with no date). The ones that only move money of entries dated on or before the conversion date are "
				+"selected. Change the selection if needed, then click Change Dates to date the selected transfers and their splits on the conversion date.");
			if(!_canFix) {
				labelCannotFix.Text=Lan.g(this,"Change Dates is disabled: the replication check was not passed.");
			}
			labelCannotFix.Visible=!_canFix;
			FillGrid();
			butFix.Enabled=false;
		}

		private void FillGrid() {
			gridMain.BeginUpdate();
			gridMain.Columns.Clear();
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Date"),80));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Entered"),80));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Pat Num"),70,HorizontalAlignment.Right));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Patient"),180));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Splits"),50,HorizontalAlignment.Right));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Converted"),70,HorizontalAlignment.Right));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"After Conv"),70,HorizontalAlignment.Right));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Pay Plan"),60,HorizontalAlignment.Right));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Unearned"),65,HorizontalAlignment.Right));
			gridMain.Columns.Add(new GridColumn(Lan.g(this,"Suggested"),70,HorizontalAlignment.Center){ IsWidthDynamic=true });
			gridMain.ListGridRows.Clear();
			for(int i=0;i<_listRows.Count;i++) {
				DbmConvPaymentDateRow dbmConvPaymentDateRow=_listRows[i];
				GridRow row=new GridRow();
				row.Cells.Add(dbmConvPaymentDateRow.PayDate.Year<1880 ? Lan.g(this,"none") : dbmConvPaymentDateRow.PayDate.ToShortDateString());
				row.Cells.Add(dbmConvPaymentDateRow.DateEntry.Year<1880 ? "" : dbmConvPaymentDateRow.DateEntry.ToShortDateString());
				row.Cells.Add(dbmConvPaymentDateRow.PatNum.ToString());
				row.Cells.Add(dbmConvPaymentDateRow.PatientName);
				row.Cells.Add(dbmConvPaymentDateRow.CountSplits.ToString());
				row.Cells.Add(dbmConvPaymentDateRow.CountSplitsConverted.ToString());
				row.Cells.Add(dbmConvPaymentDateRow.CountSplitsAfterConversion.ToString());
				row.Cells.Add(dbmConvPaymentDateRow.CountSplitsPayPlan.ToString());
				row.Cells.Add(dbmConvPaymentDateRow.CountSplitsUnattached.ToString());
				row.Cells.Add(dbmConvPaymentDateRow.IsConversionTransfer ? "X" : "");
				row.Tag=dbmConvPaymentDateRow;
				gridMain.ListGridRows.Add(row);
			}
			gridMain.EndUpdate();
			for(int i=0;i<_listRows.Count;i++) {
				if(_listRows[i].IsConversionTransfer) {
					gridMain.SetSelected(i,true);
				}
			}
		}

		///<summary>The conversion date in the box, or MinValue (after a message) when it is blank or invalid.</summary>
		private DateTime GetDateConversion() {
			if(!textDateConversion.IsValid() || textDateConversion.Text.Trim()=="") {
				MsgBox.Show(this,"Enter a valid conversion date.");
				return DateTime.MinValue;
			}
			DateTime dateConversion=PIn.Date(textDateConversion.Text).Date;
			if(dateConversion.Year<1880) {
				MsgBox.Show(this,"Enter a valid conversion date.");
				return DateTime.MinValue;
			}
			return dateConversion;
		}

		private void butFind_Click(object sender,EventArgs e) {
			DateTime dateConversion=GetDateConversion();
			if(dateConversion==DateTime.MinValue) {
				return;
			}
			Find(dateConversion);
		}

		private void Find(DateTime dateConversion) {
			Cursor=Cursors.WaitCursor;
			try {
				_listRows=DatabaseMaintenances.GetConversionPaymentDateRows(dateConversion);
			}
			finally {
				Cursor=Cursors.Default;
			}
			_dateListed=dateConversion;
			FillGrid();
			labelSummary.Text=Lan.g(this,"Income transfers dated after")+" "+dateConversion.ToShortDateString()+": "+_listRows.Count+".  "
				+Lan.g(this,"Suggested (they only move money of the converted history):")+" "+_listRows.Count(x => x.IsConversionTransfer)+".";
			butFix.Enabled=(_canFix && _listRows.Count>0);
		}

		private void butFix_Click(object sender,EventArgs e) {
			if(!_canFix || _dateListed==DateTime.MinValue) {
				return;
			}
			DateTime dateConversion=GetDateConversion();
			if(dateConversion==DateTime.MinValue) {
				return;
			}
			if(dateConversion!=_dateListed) {
				MsgBox.Show(this,"The conversion date changed since the list was filled. Click Find first.");
				return;
			}
			List<DbmConvPaymentDateRow> listRowsSelected=gridMain.SelectedTags<DbmConvPaymentDateRow>();
			if(listRowsSelected.Count==0) {
				MsgBox.Show(this,"Select the income transfers to date on the conversion date.");
				return;
			}
			//Back dating a payment: the user needs Payment Edit for the conversion date (the lock date applies).
			if(!Security.IsAuthorized(EnumPermType.PaymentEdit,dateConversion)) {
				return;
			}
			int countNotSuggested=listRowsSelected.Count(x => !x.IsConversionTransfer);
			string msg=Lan.g(this,"Income transfers to date on")+" "+dateConversion.ToShortDateString()+", "+Lan.g(this,"with their splits:")+" "
				+listRowsSelected.Count+"\r\n";
			if(countNotSuggested>0) {
				msg+=Lan.g(this,"Of these, not suggested (they move money of entries after the conversion date, of a payment plan, or only unearned money):")
					+" "+countNotSuggested+"\r\n";
			}
			msg+="\r\n"+Lan.g(this,"Reports for the old dates will change. Each change is written to the audit trail and the database maintenance log. "
				+"Make a backup of the database before you continue.")+"\r\n\r\n"+Lan.g(this,"Continue?");
			if(MessageBox.Show(msg,Lan.g(this,"Conversion Payment Dates"),MessageBoxButtons.OKCancel)!=DialogResult.OK) {
				return;
			}
			string strResult;
			Cursor=Cursors.WaitCursor;
			try {
				strResult=DatabaseMaintenances.FixConversionPaymentDates(listRowsSelected.Select(x => x.PayNum).ToList(),dateConversion);
			}
			catch(Exception ex) {
				Cursor=Cursors.Default;
				FriendlyException.Show(Lan.g(this,"Error changing the dates of the income transfers."),ex);
				return;
			}
			Cursor=Cursors.Default;
			HasFixed=true;
			LogText+=Lan.g(this,"Conversion Payment Dates")+":\r\n"+strResult;
			Find(dateConversion);
			MsgBoxCopyPaste msgBoxCopyPaste=new MsgBoxCopyPaste(strResult);
			msgBoxCopyPaste.Show();
		}

		private void butClose_Click(object sender,EventArgs e) {
			Close();
		}
	}
}
