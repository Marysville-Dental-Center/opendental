using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using CodeBase;
using OpenPmsDentalBusiness;

///<summary>Sets this computer's clock, only through TrySetUtc. The program never sets the clock by itself: Windows Time keeps it (recommended), and
///the manual time window calls TrySetUtc on a button, after the checks of TimeSyncCheck.GetManualSetOffer and a confirmation
///(docs/specs/ehr-rebuild-plan.md, sections 4.4 and 4.5).</summary>
public class WindowsTime {
	///<summary>ERROR_PRIVILEGE_NOT_HELD: the account does not hold "Change the system time" (SE_SYSTEMTIME_NAME), or holds it only elevated.</summary>
	public const int ERROR_PRIVILEGE_NOT_HELD=1314;

	[DllImport("kernel32.dll",SetLastError=true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private extern static bool SetSystemTime(ref SYSTEMTIME lpSystemTime);

	[StructLayout(LayoutKind.Sequential)]
	private struct SYSTEMTIME {
		public ushort wYear;
		public ushort wMonth;
		public ushort wDayOfWeek;
		public ushort wDay;
		public ushort wHour;
		public ushort wMinute;
		public ushort wSecond;
		public ushort wMilliseconds;
	}

	///<summary>The seam to Windows: sets the clock to a UTC time and returns 0, or returns the Win32 error code. TrySetUtc is its only caller, and
	///tests replace it before any test runs, so no test sets the clock.</summary>
	public static Func<DateTime,int> FuncSetSystemTimeUtc=SetSystemTimeUtcNative;
	///<summary>The seam to the Event Log, which tests replace too.</summary>
	public static Action<string> ActionWriteEventLog=WriteEventLogNative;

	public WindowsTime() {
	}

	///<summary>Sets this computer's clock to newTimeUtc with SetSystemTime, which takes UTC, so no daylight-saving change can make it ambiguous
	///(SetLocalTime, which Open Dental called once, needs two calls across one). A local time is converted to UTC first; an unspecified one counts
	///as UTC. Returns true when Windows set the clock, and writes the Event Log entry. Otherwise returns false with the reason in error: Windows'
	///own result is checked, so a failure is never silent. Never asks for elevation and never changes user rights.</summary>
	public static bool TrySetUtc(DateTime newTimeUtc,out string error) {
		error="";
		DateTime dateTimeUtc=newTimeUtc;
		if(dateTimeUtc.Kind==DateTimeKind.Local) {
			dateTimeUtc=dateTimeUtc.ToUniversalTime();
		}
		dateTimeUtc=DateTime.SpecifyKind(dateTimeUtc,DateTimeKind.Utc);
		int win32Error;
		try {
			win32Error=FuncSetSystemTimeUtc(dateTimeUtc);
		}
		catch(Exception ex) {
			error=Lans.g("WindowsTime","The clock was not set:")+" "+ex.Message;
			return false;
		}
		if(win32Error!=0) {
			error=GetSetErrorText(win32Error);
			return false;
		}
		ActionWriteEventLog("System date and time set to:  "+dateTimeUtc.ToString("yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture)+" UTC ("
			+dateTimeUtc.ToLocalTime().ToString("MM/dd/yyyy hh:mm:ss.fff tt",CultureInfo.InvariantCulture)+" local).");
		return true;
	}

	///<summary>The message for a Win32 error of SetSystemTime. ERROR_PRIVILEGE_NOT_HELD says who can set the clock.</summary>
	public static string GetSetErrorText(int win32Error) {
		if(win32Error==ERROR_PRIVILEGE_NOT_HELD) {
			return Lans.g("WindowsTime","The clock was not set: this account may not change the system time. Only an administrator running the program "
				+"elevated (Run as administrator), or an account given \"Change the system time\" in Local Security Policy, can set the clock.")
				+" (Windows error "+win32Error+")";
		}
		string message;
		try {
			message=new Win32Exception(win32Error).Message;
		}
		catch(Exception ex) {
			ex.DoNothing();
			message="";
		}
		return Lans.g("WindowsTime","The clock was not set:")+" "+message+" (Windows error "+win32Error+")";
	}

	///<summary>The real call. Reached only through FuncSetSystemTimeUtc.</summary>
	private static int SetSystemTimeUtcNative(DateTime dateTimeUtc) {
		SYSTEMTIME systemTime=new SYSTEMTIME();
		systemTime.wYear=(ushort)dateTimeUtc.Year;
		systemTime.wMonth=(ushort)dateTimeUtc.Month;
		systemTime.wDayOfWeek=(ushort)dateTimeUtc.DayOfWeek;
		systemTime.wDay=(ushort)dateTimeUtc.Day;
		systemTime.wHour=(ushort)dateTimeUtc.Hour;
		systemTime.wMinute=(ushort)dateTimeUtc.Minute;
		systemTime.wSecond=(ushort)dateTimeUtc.Second;
		systemTime.wMilliseconds=(ushort)dateTimeUtc.Millisecond;
		if(SetSystemTime(ref systemTime)) {
			return 0;
		}
		int win32Error=Marshal.GetLastWin32Error();
		if(win32Error==0) {
			return -1;//Failed without a code.
		}
		return win32Error;
	}

	///<summary>Writes through ProgramNames.EventLogSource. A computer where the source is not registered gets no entry.</summary>
	private static void WriteEventLogNative(string messageText) {
		try {
			EventLog.WriteEntry(CodeBase.ProgramNames.EventLogSource,messageText,EventLogEntryType.Information);
		}
		catch(Exception ex) {
			ex.DoNothing();
		}
	}
}

namespace OpenPmsDentalBusiness {
	///<summary>The time check (docs/specs/ehr-rebuild-plan.md, section 4). The setting ForkTimeSyncMode is Off (the default) or Warn. With Warn, on the
	///database server's computer, the program asks the NTP server in the NistTimeServerUrl preference every 4 hours and raises an alert when the
	///clock is more than 5 seconds off. It never sets the clock: Windows Time does that (section 4.4). ShouldStart, Decide, Next and
	///GetManualSetOffer are pure functions; the rest reads or writes the setting, alerts, the log and the Windows Time service.</summary>
	public class TimeSyncCheck {
		///<summary>The fork's own preference row: 0 Off, 1 Warn. A missing row or any other value means Off, so no database step is needed. It is not
		///in PrefName and is read and written by name.</summary>
		public const string PREF_NAME="ForkTimeSyncMode";
		///<summary>A valid reply that shows the clock more than this many seconds off raises the alert. ONC's test tolerance is 5 seconds.</summary>
		public const double ALERT_OFFSET_SECONDS=5;
		///<summary>Failed checks in a row that make one day at one check every 4 hours.</summary>
		public const int FAILURES_PER_DAY=6;
		///<summary>The manual window sets the clock only for an offset of at least this many seconds...</summary>
		public const double MANUAL_SET_MIN_SECONDS=1;
		///<summary>...and at most this many (one hour). A larger error is fixed by hand in Windows.</summary>
		public const double MANUAL_SET_MAX_SECONDS=3600;
		///<summary>The service name of Windows Time.</summary>
		public const string WINDOWS_TIME_SERVICE_NAME="W32Time";
		///<summary>The start of the offset alert's description. An open alert that starts with it is not repeated.</summary>
		public const string ALERT_PREFIX_OFFSET="The clock of the database server's computer is ";
		///<summary>The start of the failed-for-a-day alert's description. An open alert that starts with it is not repeated.</summary>
		public const string ALERT_PREFIX_FAILED="The time check of the database server's clock has failed for a day";
		///<summary>The log folder (Logger) of the time check.</summary>
		public const string LOG_SUBDIRECTORY="TimeSync";
		///<summary>How often the check runs.</summary>
		public static readonly TimeSpan Interval=TimeSpan.FromHours(4);

		#region Setting
		///<summary>The setting. A missing row, a cache that is not loaded yet or any value but 1 reads as Off. Never throws.</summary>
		public static EnumTimeSyncMode GetMode() {
			Meth.NoCheckMiddleTierRole();
			return ParseMode(PrefC.GetRawOrDefault(PREF_NAME,"0"));
		}

		///<summary>The mode of a stored value: "1" is Warn, anything else (missing, blank, 0, another number, text) is Off.</summary>
		public static EnumTimeSyncMode ParseMode(string value) {
			if((value??"").Trim()=="1") {
				return EnumTimeSyncMode.Warn;
			}
			return EnumTimeSyncMode.Off;
		}

		///<summary>Stores the setting with PrefC.UpsertRaw, inserting the row the first time, and updates this workstation's cache. Returns true if the
		///value changed. The caller signals InvalidType.Prefs so other workstations reload, and writes the Setup audit entry (GetChangeLogLine), as
		///RecallProphyPerioSwitch's caller does. The thread reads it when it starts, so a change applies at the next start.</summary>
		public static bool SaveMode(EnumTimeSyncMode mode) {
			Meth.NoCheckMiddleTierRole();
			if(GetMode()==mode) {
				return false;
			}
			return PrefC.UpsertRaw(PREF_NAME,((int)mode).ToString(CultureInfo.InvariantCulture));
		}

		///<summary>The audit line of a change of the setting, or "" when unchanged.</summary>
		public static string GetChangeLogLine(EnumTimeSyncMode modeOld,EnumTimeSyncMode modeNew) {
			if(modeOld==modeNew) {
				return "";
			}
			return "Check the database server's clock every 4 hours (time check) changed from '"+modeOld+"' to '"+modeNew+"'";
		}
		#endregion Setting

		#region Decisions
		///<summary>True only for Warn on the database server's computer (ODEnvironment.IsRunningOnDbServer), whose clock the audit timestamps come from.
		///Off, or any computer but the database server's, starts no check. Pure.</summary>
		public static bool ShouldStart(EnumTimeSyncMode mode,bool isOnDbServer) {
			return mode==EnumTimeSyncMode.Warn && isOnDbServer;
		}

		///<summary>Alert when the mode is Warn and a valid reply shows the clock more than ALERT_OFFSET_SECONDS off; None for everything else (Off, a
		///reply within the limit, an invalid reply, no reply, a kiss-o'-death reply). There is no "set the clock" outcome. Pure.</summary>
		public static EnumTimeSyncDecision Decide(EnumTimeSyncMode mode,NtpResult ntpResult) {
			if(mode!=EnumTimeSyncMode.Warn || ntpResult==null || !ntpResult.IsValid || double.IsNaN(ntpResult.OffsetSeconds)) {
				return EnumTimeSyncDecision.None;
			}
			if(Math.Abs(ntpResult.OffsetSeconds)>ALERT_OFFSET_SECONDS) {
				return EnumTimeSyncDecision.Alert;
			}
			return EnumTimeSyncDecision.None;
		}

		///<summary>One check of the thread, after its request: Decide's outcome, plus the rules for the other replies. A valid reply clears the count of
		///failures. An invalid reply or no reply is written to the log only, and each FAILURES_PER_DAY-th failure in a row (one day) raises one alert
		///with the last reason. A kiss-o'-death reply stops the checks until the next start and logs its code. Changes only state.</summary>
		public static TimeSyncStep Next(TimeSyncCheckState state,EnumTimeSyncMode mode,NtpResult ntpResult) {
			TimeSyncStep timeSyncStep=new TimeSyncStep();
			if(ntpResult==null) {
				ntpResult=NtpResult.NoReply("No result.");
			}
			string server=ntpResult.Server;
			if(state.IsStopped) {
				timeSyncStep.IsStop=true;
				timeSyncStep.LogLine="The time check is stopped until the next start.";
				return timeSyncStep;
			}
			timeSyncStep.Decision=Decide(mode,ntpResult);
			if(ntpResult.Status==EnumNtpResult.Valid) {
				state.CountFailuresInRow=0;
				timeSyncStep.LogLine="Offset from "+server+": "+FormatSigned(ntpResult.OffsetSeconds)+" s, round trip "
					+ntpResult.DelaySeconds.ToString("0.000",CultureInfo.InvariantCulture)+" s, stratum "+ntpResult.Stratum+".";
				if(timeSyncStep.Decision==EnumTimeSyncDecision.Alert) {
					timeSyncStep.AlertPrefix=ALERT_PREFIX_OFFSET;
					timeSyncStep.AlertDescription=GetOffsetAlertText(ntpResult);
					timeSyncStep.AlertItemValue=GetOffsetAlertDetails(ntpResult);
				}
				return timeSyncStep;
			}
			if(ntpResult.Status==EnumNtpResult.KissOfDeath) {
				state.IsStopped=true;
				timeSyncStep.IsStop=true;
				timeSyncStep.LogLine=server+" sent a kiss-o'-death reply, code '"+ntpResult.KissCode+"'. The time check stops until the next start.";
				return timeSyncStep;
			}
			state.CountFailuresInRow++;
			state.LastFailureReason=ntpResult.Reason;
			timeSyncStep.IsFailure=true;
			timeSyncStep.LogLine="Check "+state.CountFailuresInRow+" in a row failed ("+ntpResult.Status+"): "+ntpResult.Reason;
			if(state.CountFailuresInRow%FAILURES_PER_DAY==0) {
				timeSyncStep.IsFailedForADay=true;
				timeSyncStep.AlertPrefix=ALERT_PREFIX_FAILED;
				timeSyncStep.AlertDescription=ALERT_PREFIX_FAILED+" ("+state.CountFailuresInRow+" checks in a row). Last reason: "+ntpResult.Reason;
				timeSyncStep.AlertItemValue=GetFailedAlertDetails(server,state);
			}
			return timeSyncStep;
		}

		///<summary>The offset alert: "The clock of the database server's computer is N seconds off server. Set up Windows Time (see the manual
		///window)."</summary>
		public static string GetOffsetAlertText(NtpResult ntpResult) {
			return ALERT_PREFIX_OFFSET+FormatSeconds(Math.Abs(ntpResult.OffsetSeconds))+" seconds off "+ntpResult.Server
				+". Set up Windows Time (see the manual window).";
		}

		///<summary>The details of the offset alert (shown with its Show Item Value action).</summary>
		public static string GetOffsetAlertDetails(NtpResult ntpResult) {
			string direction=ntpResult.OffsetSeconds>0 ? "behind" : "ahead of";
			return "The clock of the database server's computer is "+FormatSeconds(Math.Abs(ntpResult.OffsetSeconds))+" seconds "+direction+" "
				+ntpResult.Server+" (round trip "+ntpResult.DelaySeconds.ToString("0.000",CultureInfo.InvariantCulture)+" s, stratum "+ntpResult.Stratum+").\r\n"
				+GetWindowsTimeSteps(ntpResult.Server);
		}

		///<summary>The details of the failed-for-a-day alert.</summary>
		public static string GetFailedAlertDetails(string server,TimeSyncCheckState state) {
			return "The time check asked "+server+" "+state.CountFailuresInRow+" times in a row and got no valid reply. Last reason: "
				+state.LastFailureReason+"\r\nCheck the server's name in Setup > Chart > EHR > Time Synchronization and that this computer may reach it "
				+"(UDP port "+NTPv4.PORT+").\r\n"+GetWindowsTimeSteps(server);
		}

		///<summary>The steps that keep the database server's clock with Windows Time, the recommended way (section 4.3).</summary>
		public static string GetWindowsTimeSteps(string server) {
			string peer=string.IsNullOrWhiteSpace(server) ? "time.nist.gov" : server;
			return "Recommended: let Windows Time keep the clock. As administrator on the database server's computer, run "
				+"w32tm /config /manualpeerlist:"+peer+" /syncfromflags:manual /update, then check it with w32tm /query /status. "
				+"The program never sets the clock by itself.";
		}

		///<summary>True when an alert of the time check (one whose description starts with prefix) is still open, so it is not repeated. Pure.</summary>
		public static bool IsAlertOpen(List<AlertItem> listAlertItems,string prefix) {
			if(listAlertItems==null || string.IsNullOrEmpty(prefix)) {
				return false;
			}
			return listAlertItems.Any(x => x!=null && x.Type==AlertType.Generic && (x.Description??"").StartsWith(prefix,StringComparison.Ordinal));
		}

		///<summary>What the manual window may do with the last reply. The button "Set this computer's clock" is enabled only when the reply was valid,
		///the offset is from MANUAL_SET_MIN_SECONDS to MANUAL_SET_MAX_SECONDS, the user has the Setup permission and Windows Time is disabled on this
		///computer (or not installed). A running Windows Time gets the advice to resync; a stopped one that is not disabled, the advice to start it
		///first, because w32tm /resync fails while it is stopped. Pure.</summary>
		public static TimeSetOffer GetManualSetOffer(NtpResult ntpResult,bool hasSetupPermission,EnumWindowsTimeState windowsTimeState) {
			TimeSetOffer timeSetOffer=new TimeSetOffer();
			if(ntpResult==null) {
				timeSetOffer.Message="Measure first.";
				return timeSetOffer;
			}
			if(!ntpResult.IsValid) {
				timeSetOffer.Message="The time server's last reply is not valid, so the clock cannot be set: "+ntpResult.Reason;
				return timeSetOffer;
			}
			double offsetAbs=Math.Abs(ntpResult.OffsetSeconds);
			if(offsetAbs<MANUAL_SET_MIN_SECONDS) {
				timeSetOffer.Message="This computer's clock is within 1 second of "+ntpResult.Server+". There is nothing to set.";
				return timeSetOffer;
			}
			if(offsetAbs>MANUAL_SET_MAX_SECONDS) {
				timeSetOffer.Message="This computer's clock is more than an hour off "+ntpResult.Server+". Set the date and time by hand in Windows.";
				return timeSetOffer;
			}
			if(!hasSetupPermission) {
				timeSetOffer.Message="Setting the clock needs the Setup permission.";
				return timeSetOffer;
			}
			switch(windowsTimeState) {
				case EnumWindowsTimeState.Running:
					timeSetOffer.Message="Windows Time is running on this computer and keeps its clock. To correct the clock now, run "
						+"w32tm /resync as administrator.";
					return timeSetOffer;
				case EnumWindowsTimeState.Stopped:
					timeSetOffer.Message="Windows Time is stopped on this computer but not disabled, so Windows still sets the clock with it. As "
						+"administrator, start it (net start w32time), then run w32tm /resync.";
					return timeSetOffer;
				case EnumWindowsTimeState.Disabled:
				case EnumWindowsTimeState.NotInstalled:
					timeSetOffer.CanSet=true;
					timeSetOffer.Message="Windows Time is "+(windowsTimeState==EnumWindowsTimeState.Disabled ? "disabled" : "not installed")
						+" on this computer, so the clock can be set here. Another time program on this computer may change the clock again.";
					return timeSetOffer;
				default:
					timeSetOffer.Message="The Windows Time service of this computer could not be read, so the clock is not set here. To correct the "
						+"clock, run w32tm /resync as administrator.";
					return timeSetOffer;
			}
		}

		///<summary>The state of a Windows Time service from its status and start type. The start type decides first: on a computer outside a domain
		///Windows Time starts on demand, so it is often stopped although Windows still uses it to set the clock. Pure.</summary>
		public static EnumWindowsTimeState ToWindowsTimeState(ServiceControllerStatus status,ServiceStartMode serviceStartMode) {
			if(serviceStartMode==ServiceStartMode.Disabled) {
				return EnumWindowsTimeState.Disabled;
			}
			if(status==ServiceControllerStatus.Running || status==ServiceControllerStatus.StartPending
				|| status==ServiceControllerStatus.ContinuePending)
			{
				return EnumWindowsTimeState.Running;
			}
			return EnumWindowsTimeState.Stopped;
		}

		///<summary>The question before the manual window sets the clock: the server and the offset.</summary>
		public static string GetManualSetQuestion(NtpResult ntpResult) {
			string direction=ntpResult.OffsetSeconds>0 ? "behind" : "ahead of";
			return "This computer's clock is "+FormatSeconds(Math.Abs(ntpResult.OffsetSeconds))+" seconds "+direction+" "+ntpResult.Server+".\r\n"
				+"Set this computer's clock to the time of "+ntpResult.Server+"?";
		}

		///<summary>The Setup audit entry after the manual window set the clock, with the old and the new time.</summary>
		public static string GetManualSetLogText(string server,DateTime dateTimeOldUtc,DateTime dateTimeNewUtc) {
			return "This computer's clock ("+Environment.MachineName+") was set from "
				+dateTimeOldUtc.ToString("yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture)+" UTC to "
				+dateTimeNewUtc.ToString("yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture)+" UTC, the time of "+server
				+" (Time Synchronization window).";
		}
		#endregion Decisions

		#region Measuring
		///<summary>One measurement of the manual window: the time server's reply and the database server's clock against this computer's. It only
		///measures; nothing here calls WindowsTime. The functions are the request, the database server's clock and this computer's clock.</summary>
		public static TimeSyncMeasurement Measure(Func<NtpResult> funcQueryNtp,Func<DateTime> funcServerNow,Func<DateTime> funcLocalNow) {
			TimeSyncMeasurement timeSyncMeasurement=new TimeSyncMeasurement();
			timeSyncMeasurement.NtpResult=funcQueryNtp()??NtpResult.NoReply("No result.");
			double serverOffsetMs=(funcServerNow()-funcLocalNow()).TotalMilliseconds;
			timeSyncMeasurement.DateTimeLocal=funcLocalNow();
			timeSyncMeasurement.DateTimeServer=timeSyncMeasurement.DateTimeLocal.AddMilliseconds(serverOffsetMs);
			if(timeSyncMeasurement.NtpResult.IsValid) {
				timeSyncMeasurement.DateTimeNtp=timeSyncMeasurement.DateTimeLocal.AddSeconds(timeSyncMeasurement.NtpResult.OffsetSeconds);
			}
			return timeSyncMeasurement;
		}

		///<summary>The message of the manual window for a measurement. Pure.</summary>
		public static string GetMeasureMessage(TimeSyncMeasurement timeSyncMeasurement) {
			NtpResult ntpResult=timeSyncMeasurement.NtpResult;
			List<string> listLines=new List<string>();
			if(!ntpResult.IsValid) {
				listLines.Add("No valid reply from the time server "+ntpResult.Server+": "+ntpResult.Reason);
			}
			else if(Math.Abs(ntpResult.OffsetSeconds)>=MANUAL_SET_MIN_SECONDS) {
				listLines.Add("This computer's clock is "+FormatSeconds(Math.Abs(ntpResult.OffsetSeconds))+" seconds off "+ntpResult.Server+".");
			}
			double serverOffsetSeconds=(timeSyncMeasurement.DateTimeServer-timeSyncMeasurement.DateTimeLocal).TotalSeconds;
			if(Math.Abs(serverOffsetSeconds)>0.99) {
				listLines.Add("The database server's clock is "+FormatSeconds(Math.Abs(serverOffsetSeconds))+" seconds off this computer's clock. "
					+"Let Windows Time keep the database server's clock, and turn on the check below on that computer.");
			}
			if(listLines.Count==0) {
				return "All times synchronized within one second. You may close this window.";
			}
			return string.Join("\r\n",listLines);
		}
		#endregion Measuring

		#region Thread and Windows
		///<summary>Carries out a step of the thread: writes its log line and raises its alert, unless one of the same kind is open. Never throws.</summary>
		public static void Apply(TimeSyncStep timeSyncStep) {
			Meth.NoCheckMiddleTierRole();
			try {
				Logger.WriteLine(timeSyncStep.LogLine,LOG_SUBDIRECTORY);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
			if(string.IsNullOrEmpty(timeSyncStep.AlertDescription)) {
				return;
			}
			try {
				RaiseAlertIfNoneOpen(timeSyncStep.AlertPrefix,timeSyncStep.AlertDescription,timeSyncStep.AlertItemValue);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}

		///<summary>Inserts a generic alert for every user and clinic (default alert categories) unless an alert whose description starts with prefix is
		///open. Returns true if it inserted one.</summary>
		public static bool RaiseAlertIfNoneOpen(string prefix,string description,string itemValue) {
			Meth.NoCheckMiddleTierRole();
			if(IsAlertOpen(AlertItems.RefreshForType(AlertType.Generic),prefix)) {
				return false;
			}
			AlertItem alertItem=new AlertItem();
			alertItem.ClinicNum=-1;
			alertItem.UserNum=0;
			alertItem.Type=AlertType.Generic;
			alertItem.Severity=SeverityType.Medium;
			alertItem.Actions=ActionType.MarkAsRead|ActionType.Delete|ActionType.ShowItemValue;
			alertItem.FormToOpen=FormType.None;
			alertItem.FKey=0;
			alertItem.Description=description;
			alertItem.ItemValue=itemValue??"";
			AlertItems.Insert(alertItem);
			return true;
		}

		///<summary>Reads this computer's Windows Time service (its start type, then its status), as ODprintout reads the print spooler. Never throws.</summary>
		public static EnumWindowsTimeState GetWindowsTimeState() {
			Meth.NoCheckMiddleTierRole();
			ServiceController[] arrayServiceControllers=null;
			try {
				arrayServiceControllers=ServiceController.GetServices();
				ServiceController serviceController=arrayServiceControllers.FirstOrDefault(x => x.ServiceName.Equals(WINDOWS_TIME_SERVICE_NAME,StringComparison.OrdinalIgnoreCase));
				if(serviceController==null) {
					return EnumWindowsTimeState.NotInstalled;
				}
				return ToWindowsTimeState(serviceController.Status,serviceController.StartType);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return EnumWindowsTimeState.Unknown;
			}
			finally {
				if(arrayServiceControllers!=null) {
					foreach(ServiceController serviceController in arrayServiceControllers) {
						serviceController.Dispose();
					}
				}
			}
		}
		#endregion Thread and Windows

		///<summary>Seconds for a message: one decimal, the decimal point of the user's culture.</summary>
		private static string FormatSeconds(double seconds) {
			return seconds.ToString("0.#",CultureInfo.CurrentCulture);
		}

		private static string FormatSigned(double seconds) {
			return seconds.ToString("+0.000;-0.000;0.000",CultureInfo.InvariantCulture);
		}
	}

	///<summary>The time check's setting (ForkTimeSyncMode). Stored as its number.</summary>
	public enum EnumTimeSyncMode {
		///<summary>0 - No check (the default).</summary>
		Off=0,
		///<summary>1 - On the database server's computer, check the clock every 4 hours and raise an alert when it is more than 5 seconds off.</summary>
		Warn=1,
	}

	///<summary>What TimeSyncCheck.Decide says for one reply. There is no "set the clock".</summary>
	public enum EnumTimeSyncDecision {
		///<summary>Nothing to report.</summary>
		None,
		///<summary>Raise the offset alert, unless one is open.</summary>
		Alert,
	}

	///<summary>The Windows Time service of this computer, as the manual window needs it.</summary>
	public enum EnumWindowsTimeState {
		///<summary>Could not be read.</summary>
		Unknown,
		///<summary>Running (or starting).</summary>
		Running,
		///<summary>Stopped, but not disabled: Windows starts it when it sets the clock.</summary>
		Stopped,
		///<summary>Disabled: nothing of Windows sets the clock.</summary>
		Disabled,
		///<summary>Not installed on this computer.</summary>
		NotInstalled,
	}

	///<summary>The time check's state for one run of the program.</summary>
	public class TimeSyncCheckState {
		///<summary>Failed checks (an invalid reply or no reply) since the last valid reply.</summary>
		public int CountFailuresInRow;
		///<summary>The reason of the last failed check.</summary>
		public string LastFailureReason="";
		///<summary>Set by a kiss-o'-death reply: no more checks until the next start.</summary>
		public bool IsStopped;
	}

	///<summary>What one check of the thread does.</summary>
	public class TimeSyncStep {
		///<summary>Decide's outcome.</summary>
		public EnumTimeSyncDecision Decision;
		///<summary>An invalid reply or no reply.</summary>
		public bool IsFailure;
		///<summary>This failure completes a day of failures in a row: raise the failed-for-a-day alert.</summary>
		public bool IsFailedForADay;
		///<summary>Stop the checks until the next start.</summary>
		public bool IsStop;
		///<summary>The line for the log (Logger, TimeSync folder).</summary>
		public string LogLine="";
		///<summary>The alert to raise, or "" for none, with the start of the description that marks an open alert of the same kind.</summary>
		public string AlertDescription="";
		public string AlertItemValue="";
		public string AlertPrefix="";
	}

	///<summary>Whether the manual window may set the clock, and what it says.</summary>
	public class TimeSetOffer {
		public bool CanSet;
		public string Message="";
	}

	///<summary>One measurement of the manual window.</summary>
	public class TimeSyncMeasurement {
		public NtpResult NtpResult;
		public DateTime DateTimeLocal;
		public DateTime DateTimeServer;
		///<summary>MinValue when the time server gave no valid reply.</summary>
		public DateTime DateTimeNtp=DateTime.MinValue;
	}
}
