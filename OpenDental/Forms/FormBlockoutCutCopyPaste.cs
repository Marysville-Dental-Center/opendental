using System;
using System.Windows.Forms;
using OpenDentBusiness;

namespace OpenDental {
	/// <summary>
	/// Summary description for FormBasicTemplate.
	/// </summary>
	public partial class FormBlockoutCutCopyPaste:FormODBase {
		private static DateTime _dateCopyStart=DateTime.MinValue;
		private static DateTime _dateCopyEnd=DateTime.MinValue;
		public long ApptViewNum;
		private static long _apptViewNumPrevious;
		public DateTime DateSelected;

		///<summary></summary>
		public FormBlockoutCutCopyPaste()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormBlockoutCutCopyPaste_Load(object sender,EventArgs e) {
			if(DateSelected.DayOfWeek==DayOfWeek.Saturday || DateSelected.DayOfWeek==DayOfWeek.Sunday) {
				checkWeekend.Checked=true;
			}			
			if(ApptViewNum!=_apptViewNumPrevious){
				_dateCopyStart=DateTime.MinValue;
				_dateCopyEnd=DateTime.MinValue;
			}
			FillClipboard();
			_apptViewNumPrevious=ApptViewNum;//remember the appt view for next time.
		}

		private void butClearDay_Click(object sender,EventArgs e) {
			//25.1: only the kinds of blockouts the user may manage are cleared.
			bool hasPermBlockouts=Security.IsAuthorized(EnumPermType.Blockouts,true);
			bool hasPermBlockoutsFlagged=Security.IsAuthorized(EnumPermType.BlockoutsFlagged,true);
			if(!hasPermBlockouts && !hasPermBlockoutsFlagged) {
				Security.IsAuthorized(EnumPermType.Blockouts);//Shows the "not authorized" message.
				return;
			}
			int countSkipped;
			if(PrefC.HasClinicsEnabled) {
				string clinicAbbr=(Clinics.ClinicNum==0?Lan.g(this,"Headquarters"):Clinics.GetAbbr(Clinics.ClinicNum));
				if(MessageBox.Show(Lan.g(this,"Clear all blockouts for day for clinic: ")+clinicAbbr+Lan.g(this,"?")+"\r\n"
					+Lan.g(this,"(This may include blockouts not shown in the current appointment view)")
					,Lan.g(this,"Clear Blockouts"),MessageBoxButtons.OKCancel)!=DialogResult.OK) 
				{ 
					return;
				}
				countSkipped=Schedules.ClearBlockoutsForClinic(Clinics.ClinicNum,DateSelected,hasPermBlockouts,hasPermBlockoutsFlagged);//currently selected clinic only, works for daily or weekly
				LogClear(Clinics.ClinicNum,hasPermBlockouts,hasPermBlockoutsFlagged);
			}
			else {
				if(!MsgBox.Show(this,MsgBoxButtons.OKCancel,"Clear all blockouts for day? (This may include blockouts not shown in the current appointment view)")) {
					return;
				}
				countSkipped=Schedules.ClearBlockoutsForDay(DateSelected,hasPermBlockouts,hasPermBlockoutsFlagged);//works for daily or weekly
				LogClear(-1,hasPermBlockouts,hasPermBlockoutsFlagged);
			}
			if(countSkipped>0) {
				MessageBox.Show(countSkipped+" "+Lan.g(this,"blockout(s) were not cleared because you do not have permission for their blockout type."));
			}
			Close();
		}

		///<summary>25.1: one clear entry per kind of blockout the user was allowed to clear, under that kind's permission.</summary>
		private void LogClear(long clinicNum,bool hasPermBlockouts,bool hasPermBlockoutsFlagged) {
			if(hasPermBlockouts) {
				Schedules.BlockoutLogHelper(BlockoutAction.Clear,dateTime:DateSelected,clinicNum:clinicNum,permTypeClear:EnumPermType.Blockouts);
			}
			if(hasPermBlockoutsFlagged) {
				Schedules.BlockoutLogHelper(BlockoutAction.Clear,dateTime:DateSelected,clinicNum:clinicNum,permTypeClear:EnumPermType.BlockoutsFlagged);
			}
		}

		private void FillClipboard(){
			if(_dateCopyStart.Year<1880){
				textClipboard.Text="";
			}
			else if(_dateCopyStart==_dateCopyEnd) {
				textClipboard.Text=_dateCopyStart.ToShortDateString();
			}
			else {
				textClipboard.Text=_dateCopyStart.ToShortDateString()+"-"+_dateCopyEnd.ToShortDateString();
			}
		}

		private void butCopyDay_Click(object sender,EventArgs e) {
			_dateCopyStart=DateSelected;
			_dateCopyEnd=DateSelected;
			Close();
		}

		private void butCopyWeek_Click(object sender,EventArgs e) {
			//Always start week on Monday
			if(DateSelected.DayOfWeek==DayOfWeek.Sunday) {//if selecting Sunday, go back to the previous Monday.
				_dateCopyStart=DateSelected.AddDays(-6);
			}
			else {//Any other day. eg Wed.AddDays(1-3)=Wed.AddDays(-2)=Monday
				_dateCopyStart=DateSelected.AddDays(1-(int)DateSelected.DayOfWeek);//eg Wed.AddDays(1-3)=Wed.AddDays(-2)=Monday
			}
			if(checkWeekend.Checked){
				_dateCopyEnd=_dateCopyStart.AddDays(6);
			}
			else{
				_dateCopyEnd=_dateCopyStart.AddDays(4);
			}
			Close();
		}

		private void butPaste_Click(object sender,EventArgs e) {
			CopyOverBlockouts(1);
		}

		private void butRepeat_Click(object sender,EventArgs e) {
			try {
				int.Parse(textRepeat.Text);
			}
			catch {
				MsgBox.Show(this,"Please fix number box first.");
				return;
			}
			CopyOverBlockouts(PIn.Int(textRepeat.Text));
		}

		private void CopyOverBlockouts(int numRepeat) {
			if(_dateCopyStart.Year < 1880) {
				MsgBox.Show(this,"Please copy a selection to the clipboard first.");
				return;
			}
			if(DateSelected.DayOfWeek==DayOfWeek.Saturday || DateSelected.DayOfWeek==DayOfWeek.Sunday) {//copying from a weekend
				if(!checkWeekend.Checked) {//but they didn't indicate pasting onto weekends
					MsgBox.Show(this,"You must check 'Include Weekends' if you would like to paste into weekends.");
					return;
				}
			}
			//calculate which day or week is currently selected.
			DateTime dateSelectedStart;
			DateTime dateSelectedEnd;
			bool isWeek=_dateCopyStart!=_dateCopyEnd;
			if(isWeek) {
				//Always start week on Monday
				if(DateSelected.DayOfWeek==DayOfWeek.Sunday) {//if selecting Sunday, go back to the previous Monday.
					dateSelectedStart=DateSelected.AddDays(-6);
				}
				else {//Any other day. eg Wed.AddDays(1-3)=Wed.AddDays(-2)=Monday
					dateSelectedStart=DateSelected.AddDays(1-(int)DateSelected.DayOfWeek);//eg Wed.AddDays(1-3)=Wed.AddDays(-2)=Monday
				}
				//DateCopyEnd is greater than DateCopyStart and is either 4 days greater or 6 days greater, so clear/paste the same number of days
				dateSelectedEnd=dateSelectedStart.AddDays((_dateCopyEnd-_dateCopyStart).Days);
			}
			else {
				dateSelectedStart=DateSelected;
				dateSelectedEnd=DateSelected;
			}
			//When pasting, it's not allowed to paste back over the same day or week.
			if(dateSelectedStart==_dateCopyStart && numRepeat==1) {
				MsgBox.Show(this,"Not allowed to paste back onto the same date as is on the clipboard.");
				return;
			}
			Cursor=Cursors.WaitCursor;
			//25.1: NS-flagged blockouts need BlockoutsFlagged and unflagged ones need Blockouts. DC-flagged ones are never copied.
			string errors=Schedules.CopyBlockouts(ApptViewNum,isWeek,checkWeekend.Checked,checkReplace.Checked,_dateCopyStart,_dateCopyEnd,
				dateSelectedStart,dateSelectedEnd,numRepeat,Security.IsAuthorized(EnumPermType.Blockouts,true),
				Security.IsAuthorized(EnumPermType.BlockoutsFlagged,true));
			Cursor=Cursors.Default;
			if(!string.IsNullOrEmpty(errors)) {
				MessageBox.Show(errors);//Error was translated inside of the S class method.
				return;
			}
			Close();
		}
	}
}