using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using OpenPmsDentalBusiness;

namespace OpenPmsDental {
	///<summary>Setup > Chart > EHR > Time Synchronization (hidden without the Setup permission). Measures this computer's clock against an NTP time
	///server and against the database server's clock (docs/specs/ehr-rebuild-plan.md, section 4.5). Opening the window and Measure only measure:
	///they never set the clock. Only "Set this computer's clock" does: it measures again on the click, and sets the clock from that measurement only,
	///when TimeSyncCheck.GetManualSetOffer allows it on that fresh reply (a valid reply, an offset from 1 second to 1 hour, the Setup permission,
	///Windows Time disabled) and after a confirmation that shows its server and offset. The check box is the time check's setting
	///(ForkTimeSyncMode, section 4.2).</summary>
	public partial class FormEhrTimeSynch:FormODBase {
		private DateTime _timeNist;
		private DateTime _timeServer;
		private DateTime _timeLocal;
		///<summary>The time server's last reply, checked by NtpReply. Null before the first measurement and after the clock was set. It only enables
		///the set button: the button measures again and never applies this reply.</summary>
		private NtpResult _ntpResultLast;

		public FormEhrTimeSynch() {
			InitializeComponent();
			InitializeLayoutManager();
			Lan.F(this);
		}

		private void FormEhrTime_Load(object sender,EventArgs e) {
			textNistUrl.Text=PrefC.GetString(PrefName.NistTimeServerUrl);
			checkTimeSyncWarn.Checked=TimeSyncCheck.GetMode()==EnumTimeSyncMode.Warn;
			MeasureTimes();
		}

		///<summary>Measures the three clocks and refreshes the window. Never sets the clock.</summary>
		private void MeasureTimes() {
			this.Cursor=Cursors.WaitCursor;
			string server=textNistUrl.Text;
			TimeSyncMeasurement timeSyncMeasurement=TimeSyncCheck.Measure(() => new NTPv4().Query(server),MiscData.GetNowDateTimeWithMilli,() => DateTime.Now);
			this.Cursor=Cursors.Default;
			ShowMeasurement(timeSyncMeasurement);
		}

		///<summary>Shows a measurement (Measure's or the set button's own) and starts the 4-second limit on requests. Never sets the clock.</summary>
		private void ShowMeasurement(TimeSyncMeasurement timeSyncMeasurement) {
			timerSendingLimit.Enabled=true;
			_ntpResultLast=timeSyncMeasurement.NtpResult;
			_timeLocal=timeSyncMeasurement.DateTimeLocal;
			_timeServer=timeSyncMeasurement.DateTimeServer;
			_timeNist=timeSyncMeasurement.DateTimeNtp;
			textMessage.Text=TimeSyncCheck.GetMeasureMessage(timeSyncMeasurement);
			refreshDisplays();
		}

		///<summary>Measure: stops users from sending requests to the time server more than once every 4 seconds.</summary>
		private void butRefreshTime_Click(object sender,EventArgs e) {
			if(timerSendingLimit.Enabled) {
				MsgBox.Show(this,"Cannot send a time request more than once every four seconds");
				return;
			}
			MeasureTimes();
		}

		///<summary>Refresh the time textboxes and the set button. Saves the server's address when it gave a valid reply.</summary>
		private void refreshDisplays() {
			textLocalTime.Text=_timeLocal==DateTime.MinValue ? "" : _timeLocal.ToString("hh:mm:ss.fff tt");
			textServerTime.Text=_timeServer==DateTime.MinValue ? "" : _timeServer.ToString("hh:mm:ss.fff tt");
			textNistTime.Text=_timeNist==DateTime.MinValue ? "" : _timeNist.ToString("hh:mm:ss.fff tt");
			if(_ntpResultLast!=null && _ntpResultLast.IsValid && Prefs.UpdateString(PrefName.NistTimeServerUrl,textNistUrl.Text)) {
				DataValid.SetInvalid(InvalidType.Prefs);
			}
			RefreshSetButton();
		}

		///<summary>The set button follows TimeSyncCheck.GetManualSetOffer: enabled only when it allows setting the clock here, otherwise its advice
		///(resync with Windows Time, start Windows Time first, set it by hand) is shown in its place.</summary>
		private void RefreshSetButton() {
			TimeSetOffer timeSetOffer=TimeSyncCheck.GetManualSetOffer(_ntpResultLast,Security.IsAuthorized(EnumPermType.Setup,true),
				TimeSyncCheck.GetWindowsTimeState());
			butSetClock.Enabled=timeSetOffer.CanSet;
			labelSetClock.Text=timeSetOffer.Message;
		}

		///<summary>Sets this computer's clock to the time server's. The window's last measurement may be hours old, so the button never applies it: it
		///measures again (TimeSyncCheck.PrepareManualSet, through the same functions as Measure), checks GetManualSetOffer on that fresh reply, shows
		///the fresh server and offset in the confirmation, and sets the clock from that measurement only (TimeSyncCheck.TrySetManual), if the
		///confirmation is answered within TimeSyncCheck.ManualSetMaxAge. A fresh reply that is not valid, or an offer that no longer holds, is
		///shown with its reason and sets nothing. WindowsTime.TrySetUtc checks Windows' result: on success it writes the Event Log entry and this
		///writes the Setup audit entry with the old and the new time; a failure shows its reason.</summary>
		private void butSetClock_Click(object sender,EventArgs e) {
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				return;
			}
			if(timerSendingLimit.Enabled) {
				MsgBox.Show(this,"Cannot send a time request more than once every four seconds");
				return;
			}
			this.Cursor=Cursors.WaitCursor;
			string server=textNistUrl.Text;
			TimeSyncManualSet timeSyncManualSet=TimeSyncCheck.PrepareManualSet(() => new NTPv4().Query(server),MiscData.GetNowDateTimeWithMilli,
				() => DateTime.Now,true,TimeSyncCheck.GetWindowsTimeState());
			Stopwatch stopwatchSinceMeasured=Stopwatch.StartNew();
			this.Cursor=Cursors.Default;
			ShowMeasurement(timeSyncManualSet.TimeSyncMeasurement);
			if(!timeSyncManualSet.TimeSetOffer.CanSet) {
				MsgBox.Show(TimeSyncCheck.GetManualSetRefusal(timeSyncManualSet));
				return;
			}
			if(!MsgBox.Show(MsgBoxButtons.OKCancel,timeSyncManualSet.Question)) {
				return;
			}
			if(!TimeSyncCheck.TrySetManual(timeSyncManualSet,stopwatchSinceMeasured.Elapsed,() => DateTime.UtcNow,out DateTime dateTimeOldUtc,
				out DateTime dateTimeNewUtc,out string error))
			{
				MsgBox.Show(error);
				return;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,
				TimeSyncCheck.GetManualSetLogText(timeSyncManualSet.TimeSyncMeasurement.NtpResult.Server,dateTimeOldUtc,dateTimeNewUtc));
			_ntpResultLast=null;//The measurement no longer describes the clock.
			_timeLocal=DateTime.Now;
			_timeNist=DateTime.MinValue;
			textMessage.Text=Lan.g(this,"The clock was set. Measure again to check it.");
			refreshDisplays();
		}

		///<summary>The time check's setting: saved at once, with the preference signal and a Setup audit entry, as the Recall Types window saves its
		///switch. The database server's computer reads it at the next start.</summary>
		private void checkTimeSyncWarn_Click(object sender,EventArgs e) {
			EnumTimeSyncMode timeSyncModeOld=TimeSyncCheck.GetMode();
			if(!Security.IsAuthorized(EnumPermType.Setup)) {
				checkTimeSyncWarn.Checked=timeSyncModeOld==EnumTimeSyncMode.Warn;
				return;
			}
			EnumTimeSyncMode timeSyncModeNew=checkTimeSyncWarn.Checked ? EnumTimeSyncMode.Warn : EnumTimeSyncMode.Off;
			if(!TimeSyncCheck.SaveMode(timeSyncModeNew)) {
				return;
			}
			DataValid.SetInvalid(InvalidType.Prefs);
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,TimeSyncCheck.GetChangeLogLine(timeSyncModeOld,timeSyncModeNew));
		}

		///<summary>Do not allow user to send another request until timer has ticked.</summary>
		private void timerSendingLimit_Tick(object sender,EventArgs e) {
			timerSendingLimit.Enabled=false;
		}

	}
}
