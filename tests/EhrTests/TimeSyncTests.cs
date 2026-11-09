// The time check (docs/specs/ehr-rebuild-plan.md, section 4): the setting ForkTimeSyncMode, TimeSyncCheck.ShouldStart and Decide for every
// combination, the rules for failed and kiss-o'-death replies, the manual window's set button (it measures again and applies only that fresh
// measurement), WindowsTime.TrySetUtc through its seam (a failure gives its reason), and source checks that the thread and the window never set
// the clock by themselves. The clock is never set: the seam is a fake in every test (AssemblySetup), and no request goes out.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace EhrTests {
	[TestClass]
	public class TimeSyncTests {
		private const string SERVER="time.example.test";
		///<summary>The server of a stale measurement, which the set button never applies.</summary>
		private const string SERVER_OLD="time-old.example.test";

		private static NtpResult Valid(double offsetSeconds,string server=SERVER) {
			NtpResult ntpResult=new NtpResult();
			ntpResult.Status=EnumNtpResult.Valid;
			ntpResult.Server=server;
			ntpResult.OffsetSeconds=offsetSeconds;
			ntpResult.DelaySeconds=0.03;
			ntpResult.Stratum=1;
			return ntpResult;
		}

		private static NtpResult WithServer(NtpResult ntpResult) {
			ntpResult.Server=SERVER;
			return ntpResult;
		}

		private static NtpResult KissOfDeath() {
			NtpResult ntpResult=new NtpResult();
			ntpResult.Status=EnumNtpResult.KissOfDeath;
			ntpResult.Server=SERVER;
			ntpResult.KissCode="RATE";
			ntpResult.Reason="kiss-o'-death RATE";
			return ntpResult;
		}

		///<summary>Every kind of result Decide and Next can get.</summary>
		private static List<NtpResult> AllKindsOfResults() {
			return new List<NtpResult> { Valid(0),Valid(4.99),Valid(5),Valid(5.001),Valid(-5.001),Valid(30),Valid(-30),Valid(86400),Valid(double.NaN),
				WithServer(NtpResult.Invalid("mode 3")),WithServer(NtpResult.NoReply("timeout")),KissOfDeath(),null };
		}

		#region The setting
		[TestMethod]
		public void Setting_IsAForkRow_OffUnlessItHolds1() {
			Assert.AreEqual("ForkTimeSyncMode",TimeSyncCheck.PREF_NAME);
			Assert.IsFalse(Enum.GetNames(typeof(PrefName)).Any(x => x.IndexOf("TimeSyncMode",StringComparison.OrdinalIgnoreCase)>=0),
				"The setting is read by name, not a PrefName member.");
			Assert.AreEqual(EnumTimeSyncMode.Warn,TimeSyncCheck.ParseMode("1"));
			Assert.AreEqual(EnumTimeSyncMode.Warn,TimeSyncCheck.ParseMode(" 1 "));
			foreach(string value in new[] { null,"","0","2","-1","01x","Warn","true","on" }) {
				Assert.AreEqual(EnumTimeSyncMode.Off,TimeSyncCheck.ParseMode(value),"'"+value+"'");
			}
			Assert.AreEqual(0,(int)EnumTimeSyncMode.Off);
			Assert.AreEqual(1,(int)EnumTimeSyncMode.Warn);
			CollectionAssert.AreEqual(new[] { "Off","Warn" },Enum.GetNames(typeof(EnumTimeSyncMode)),"Off and Warn only (E-3): no mode sets the clock.");
		}

		[TestMethod]
		public void Setting_WithoutTheCache_ReadsAsOff() {
			//No database here: the preference cache is not filled, so the missing row reads as Off and nothing throws.
			Assert.AreEqual(EnumTimeSyncMode.Off,TimeSyncCheck.GetMode());
		}

		[TestMethod]
		public void Setting_IsWrittenWithUpsertRaw_ThePrefsSignal_AndASetupAuditEntry() {
			string business=CSharpSource.GetBlock(TestPaths.ReadSource(@"OpenPmsDentalBusiness\Misc\WindowsTime.cs"),"public static bool SaveMode(");
			StringAssert.Contains(business,"PrefC.UpsertRaw(PREF_NAME,");
			Assert.IsFalse(business.Contains("UpsertRawNoCache"),"UpsertRawNoCache would leave this workstation's cache stale.");
			StringAssert.Contains(CSharpSource.GetBlock(TestPaths.ReadSource(@"OpenPmsDentalBusiness\Misc\WindowsTime.cs"),"public static EnumTimeSyncMode GetMode("),
				"PrefC.GetRawOrDefault(PREF_NAME,\"0\")");
			string window=CSharpSource.GetBlock(TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEhrTimeSynch.cs"),"private void checkTimeSyncWarn_Click(");
			StringAssert.Contains(window,"Security.IsAuthorized(EnumPermType.Setup)");
			StringAssert.Contains(window,"if(!TimeSyncCheck.SaveMode(timeSyncModeNew)) {");
			StringAssert.Contains(window,"DataValid.SetInvalid(InvalidType.Prefs);");
			StringAssert.Contains(window,"SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,TimeSyncCheck.GetChangeLogLine(timeSyncModeOld,timeSyncModeNew));");
			Assert.AreEqual("",TimeSyncCheck.GetChangeLogLine(EnumTimeSyncMode.Warn,EnumTimeSyncMode.Warn));
			StringAssert.Contains(TimeSyncCheck.GetChangeLogLine(EnumTimeSyncMode.Off,EnumTimeSyncMode.Warn),"from 'Off' to 'Warn'");
			StringAssert.Contains(TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEhrTimeSynch.designer.cs"),
				"this.checkTimeSyncWarn.Text = \"Check the database server's clock every 4 hours\";");
		}
		#endregion The setting

		#region ShouldStart and Decide
		[TestMethod]
		public void ShouldStart_OnlyWarnOnTheDatabaseServer() {
			Assert.IsFalse(TimeSyncCheck.ShouldStart(EnumTimeSyncMode.Off,false));
			Assert.IsFalse(TimeSyncCheck.ShouldStart(EnumTimeSyncMode.Off,true),"Off on the database server's computer.");
			Assert.IsFalse(TimeSyncCheck.ShouldStart(EnumTimeSyncMode.Warn,false),"Warn on a workstation.");
			Assert.IsTrue(TimeSyncCheck.ShouldStart(EnumTimeSyncMode.Warn,true));
			foreach(int value in new[] { -1,2,3,255 }) {
				Assert.IsFalse(TimeSyncCheck.ShouldStart((EnumTimeSyncMode)value,true),"Unknown mode "+value);
				Assert.IsFalse(TimeSyncCheck.ShouldStart((EnumTimeSyncMode)value,false),"Unknown mode "+value);
			}
		}

		[TestMethod]
		public void Decide_AlertsOnlyForWarnAndAValidReplyMoreThan5SecondsOff() {
			CollectionAssert.AreEqual(new[] { "None","Alert" },Enum.GetNames(typeof(EnumTimeSyncDecision)),"There is no \"set the clock\" outcome.");
			List<EnumTimeSyncMode> listModes=new List<EnumTimeSyncMode> { EnumTimeSyncMode.Off,EnumTimeSyncMode.Warn,(EnumTimeSyncMode)2 };
			int countAlerts=0;
			foreach(EnumTimeSyncMode mode in listModes) {
				foreach(NtpResult ntpResult in AllKindsOfResults()) {
					bool isAlertExpected=mode==EnumTimeSyncMode.Warn && ntpResult!=null && ntpResult.IsValid && Math.Abs(ntpResult.OffsetSeconds)>5;
					EnumTimeSyncDecision decision=TimeSyncCheck.Decide(mode,ntpResult);
					string what=mode+", "+(ntpResult==null ? "null" : ntpResult.Status+" "+ntpResult.OffsetSeconds);
					Assert.AreEqual(isAlertExpected ? EnumTimeSyncDecision.Alert : EnumTimeSyncDecision.None,decision,what);
					if(decision==EnumTimeSyncDecision.Alert) {
						countAlerts++;
					}
				}
			}
			Assert.AreEqual(5,countAlerts,"5.001, -5.001, 30, -30 and a day off, for Warn only.");
			Assert.AreEqual(EnumTimeSyncDecision.None,TimeSyncCheck.Decide(EnumTimeSyncMode.Warn,Valid(5)),"Exactly 5 seconds is within the tolerance.");
		}
		#endregion ShouldStart and Decide

		#region The thread's steps
		[TestMethod]
		public void Next_ValidReplyOff_RaisesTheAlertWithTheServerAndTheSeconds() {
			TimeSyncCheckState state=new TimeSyncCheckState();
			TimeSyncStep timeSyncStep=TimeSyncCheck.Next(state,EnumTimeSyncMode.Warn,Valid(30));
			Assert.AreEqual(EnumTimeSyncDecision.Alert,timeSyncStep.Decision);
			Assert.AreEqual("The clock of the database server's computer is 30 seconds off "+SERVER+". Set up Windows Time (see the manual window).",
				timeSyncStep.AlertDescription);
			Assert.AreEqual(TimeSyncCheck.ALERT_PREFIX_OFFSET,timeSyncStep.AlertPrefix);
			StringAssert.Contains(timeSyncStep.AlertItemValue,"behind "+SERVER);
			StringAssert.Contains(timeSyncStep.AlertItemValue,"w32tm /config /manualpeerlist:"+SERVER+" /syncfromflags:manual /update");
			Assert.IsFalse(timeSyncStep.IsStop);
			Assert.IsFalse(timeSyncStep.IsFailure);
			StringAssert.Contains(TimeSyncCheck.Next(state,EnumTimeSyncMode.Warn,Valid(-30)).AlertItemValue,"ahead of "+SERVER);
		}

		[TestMethod]
		public void Next_ValidReplyWithin5Seconds_OnlyLogs() {
			TimeSyncStep timeSyncStep=TimeSyncCheck.Next(new TimeSyncCheckState(),EnumTimeSyncMode.Warn,Valid(-4.2));
			Assert.AreEqual(EnumTimeSyncDecision.None,timeSyncStep.Decision);
			Assert.AreEqual("",timeSyncStep.AlertDescription);
			StringAssert.Contains(timeSyncStep.LogLine,"-4.200 s");
		}

		[TestMethod]
		public void Next_FailedChecks_AreLoggedOnly_AndADayOfThemRaisesOneAlert() {
			TimeSyncCheckState state=new TimeSyncCheckState();
			for(int i=1;i<=13;i++) {
				TimeSyncStep timeSyncStep=TimeSyncCheck.Next(state,EnumTimeSyncMode.Warn,
					WithServer(i%2==0 ? NtpResult.Invalid("reason "+i) : NtpResult.NoReply("reason "+i)));
				Assert.IsTrue(timeSyncStep.IsFailure);
				Assert.IsFalse(timeSyncStep.IsStop);
				Assert.AreEqual(EnumTimeSyncDecision.None,timeSyncStep.Decision);
				StringAssert.Contains(timeSyncStep.LogLine,"reason "+i);
				bool isDay=i==6 || i==12;
				Assert.AreEqual(isDay,timeSyncStep.IsFailedForADay,"check "+i);
				if(isDay) {
					StringAssert.StartsWith(timeSyncStep.AlertDescription,TimeSyncCheck.ALERT_PREFIX_FAILED);
					StringAssert.Contains(timeSyncStep.AlertDescription,"Last reason: reason "+i);
					Assert.AreEqual(TimeSyncCheck.ALERT_PREFIX_FAILED,timeSyncStep.AlertPrefix);
				}
				else {
					Assert.AreEqual("",timeSyncStep.AlertDescription,"check "+i+": the log only.");
				}
			}
			Assert.AreEqual(13,state.CountFailuresInRow);
			//A valid reply starts the count again.
			TimeSyncCheck.Next(state,EnumTimeSyncMode.Warn,Valid(1));
			Assert.AreEqual(0,state.CountFailuresInRow);
			for(int i=1;i<=5;i++) {
				Assert.IsFalse(TimeSyncCheck.Next(state,EnumTimeSyncMode.Warn,WithServer(NtpResult.NoReply("again"))).IsFailedForADay);
			}
			Assert.IsTrue(TimeSyncCheck.Next(state,EnumTimeSyncMode.Warn,WithServer(NtpResult.NoReply("again"))).IsFailedForADay);
		}

		[TestMethod]
		public void Next_KissOfDeath_StopsTheChecksUntilTheNextStart_AndLogsItsCode() {
			TimeSyncCheckState state=new TimeSyncCheckState();
			TimeSyncStep timeSyncStep=TimeSyncCheck.Next(state,EnumTimeSyncMode.Warn,KissOfDeath());
			Assert.IsTrue(timeSyncStep.IsStop);
			Assert.IsTrue(state.IsStopped);
			StringAssert.Contains(timeSyncStep.LogLine,"'RATE'");
			Assert.AreEqual("",timeSyncStep.AlertDescription,"Logged only.");
			Assert.IsFalse(timeSyncStep.IsFailure);
			//Whatever comes next, the checks stay stopped.
			TimeSyncStep timeSyncStepAfter=TimeSyncCheck.Next(state,EnumTimeSyncMode.Warn,Valid(300));
			Assert.IsTrue(timeSyncStepAfter.IsStop);
			Assert.AreEqual("",timeSyncStepAfter.AlertDescription);
			//The thread does not start again while it is stopped (the state lives as long as the program).
			string thread=CSharpSource.GetBlock(TestPaths.ReadSource(@"OpenPmsDental\Main Modules\FormOpenDentalThreads.cs"),"private void BeginTimeSyncThread()");
			StringAssert.Contains(thread,"_timeSyncCheckState.IsStopped");
		}

		[TestMethod]
		public void OpenAlerts_AreNotRepeated() {
			AlertItem alertItemOpen=new AlertItem { Type=AlertType.Generic,Description=TimeSyncCheck.ALERT_PREFIX_OFFSET+"9 seconds off "+SERVER+"." };
			AlertItem alertItemOther=new AlertItem { Type=AlertType.Generic,Description="Something else." };
			AlertItem alertItemOtherType=new AlertItem { Type=AlertType.RadiologyProcedures,Description=TimeSyncCheck.ALERT_PREFIX_OFFSET+"9 seconds" };
			Assert.IsTrue(TimeSyncCheck.IsAlertOpen(new List<AlertItem> { alertItemOther,alertItemOpen },TimeSyncCheck.ALERT_PREFIX_OFFSET));
			Assert.IsFalse(TimeSyncCheck.IsAlertOpen(new List<AlertItem> { alertItemOther,alertItemOtherType },TimeSyncCheck.ALERT_PREFIX_OFFSET));
			Assert.IsFalse(TimeSyncCheck.IsAlertOpen(new List<AlertItem> { alertItemOpen },TimeSyncCheck.ALERT_PREFIX_FAILED),"The two kinds are separate.");
			Assert.IsFalse(TimeSyncCheck.IsAlertOpen(null,TimeSyncCheck.ALERT_PREFIX_OFFSET));
			Assert.IsFalse(TimeSyncCheck.IsAlertOpen(new List<AlertItem> { null },TimeSyncCheck.ALERT_PREFIX_OFFSET));
			string raise=CSharpSource.GetBlock(TestPaths.ReadSource(@"OpenPmsDentalBusiness\Misc\WindowsTime.cs"),"public static bool RaiseAlertIfNoneOpen(");
			StringAssert.Contains(raise,"if(IsAlertOpen(AlertItems.RefreshForType(AlertType.Generic),prefix)) {");
			StringAssert.Contains(raise,"alertItem.ClinicNum=-1;");
			StringAssert.Contains(raise,"alertItem.Type=AlertType.Generic;");
		}

		[TestMethod]
		public void Thread_StartsOnlyWhenShouldStartSays_AndNeverSetsTheClock() {
			string source=TestPaths.ReadSource(@"OpenPmsDental\Main Modules\FormOpenDentalThreads.cs");
			string begin=CSharpSource.GetBlock(source,"private void BeginTimeSyncThread()");
			StringAssert.Contains(begin,"TimeSyncCheck.GetMode()");
			StringAssert.Contains(begin,"ODEnvironment.IsRunningOnDbServer(MiscData.GetODServer())");
			StringAssert.Contains(begin,"if(!TimeSyncCheck.ShouldStart(timeSyncMode,isOnDbServer)");
			Assert.IsFalse(begin.Contains("ShowFeatureEhr"),"TimeSync no longer follows ShowFeatureEhr.");
			string worker=CSharpSource.GetBlock(source,"private void TimeSyncWorker(");
			StringAssert.Contains(worker,"new NTPv4().Query(PrefC.GetString(PrefName.NistTimeServerUrl))");
			StringAssert.Contains(worker,"TimeSyncCheck.Next(_timeSyncCheckState,timeSyncMode,ntpResult)");
			StringAssert.Contains(worker,"TimeSyncCheck.Apply(timeSyncStep)");
			StringAssert.Contains(CSharpSource.GetBlock(TestPaths.ReadSource(@"OpenPmsDentalBusiness\Misc\WindowsTime.cs"),"public static TimeSyncStep Next("),
				"timeSyncStep.Decision=Decide(mode,ntpResult);");
			int indexRegion=source.IndexOf("#region TimeSyncThread",StringComparison.Ordinal);
			string region=source.Substring(indexRegion,source.IndexOf("#endregion",indexRegion,StringComparison.Ordinal)-indexRegion);
			Assert.IsFalse(Regex.IsMatch(CSharpSource.Read(region).CodeWithoutComments,@"\bWindowsTime\."),"The thread never sets the clock.");
			Assert.AreEqual(TimeSpan.FromHours(4),TimeSyncCheck.Interval);
		}
		#endregion The thread's steps

		#region The manual window
		[TestMethod]
		public void SetButton_NeedsAValidReply_1SecondTo1Hour_Setup_AndWindowsTimeDisabled() {
			EnumWindowsTimeState disabled=EnumWindowsTimeState.Disabled;
			Assert.IsFalse(TimeSyncCheck.GetManualSetOffer(null,true,disabled).CanSet,"Not measured yet.");
			Assert.IsFalse(TimeSyncCheck.GetManualSetOffer(WithServer(NtpResult.Invalid("mode 3")),true,disabled).CanSet);
			StringAssert.Contains(TimeSyncCheck.GetManualSetOffer(WithServer(NtpResult.Invalid("mode 3")),true,disabled).Message,"mode 3");
			Assert.IsFalse(TimeSyncCheck.GetManualSetOffer(WithServer(NtpResult.NoReply("timeout")),true,disabled).CanSet);
			Assert.IsFalse(TimeSyncCheck.GetManualSetOffer(KissOfDeath(),true,disabled).CanSet);
			foreach(double offsetSeconds in new[] { 0.0,0.5,-0.999 }) {
				Assert.IsFalse(TimeSyncCheck.GetManualSetOffer(Valid(offsetSeconds),true,disabled).CanSet,offsetSeconds+" s: nothing to set.");
			}
			foreach(double offsetSeconds in new[] { 1.0,-1.0,30.0,-600.0,3600.0,-3600.0 }) {
				TimeSetOffer timeSetOffer=TimeSyncCheck.GetManualSetOffer(Valid(offsetSeconds),true,disabled);
				Assert.IsTrue(timeSetOffer.CanSet,offsetSeconds+" s");
				StringAssert.Contains(timeSetOffer.Message,"Another time program on this computer may change the clock again.");
			}
			foreach(double offsetSeconds in new[] { 3600.5,-7200.0,86400.0 }) {
				TimeSetOffer timeSetOffer=TimeSyncCheck.GetManualSetOffer(Valid(offsetSeconds),true,disabled);
				Assert.IsFalse(timeSetOffer.CanSet,offsetSeconds+" s: fixed by hand in Windows.");
				StringAssert.Contains(timeSetOffer.Message,"by hand in Windows");
			}
			Assert.IsFalse(TimeSyncCheck.GetManualSetOffer(Valid(30),false,disabled).CanSet,"Without the Setup permission.");
			Assert.IsTrue(TimeSyncCheck.GetManualSetOffer(Valid(30),true,EnumWindowsTimeState.NotInstalled).CanSet,"No Windows Time at all.");
		}

		[TestMethod]
		public void SetButton_GivesWayToWindowsTime_UnlessItIsDisabled() {
			TimeSetOffer timeSetOfferRunning=TimeSyncCheck.GetManualSetOffer(Valid(30),true,EnumWindowsTimeState.Running);
			Assert.IsFalse(timeSetOfferRunning.CanSet);
			StringAssert.Contains(timeSetOfferRunning.Message,"w32tm /resync");
			Assert.IsFalse(timeSetOfferRunning.Message.Contains("net start"));
			TimeSetOffer timeSetOfferStopped=TimeSyncCheck.GetManualSetOffer(Valid(30),true,EnumWindowsTimeState.Stopped);
			Assert.IsFalse(timeSetOfferStopped.CanSet);
			StringAssert.Contains(timeSetOfferStopped.Message,"net start w32time");
			Assert.IsTrue(timeSetOfferStopped.Message.IndexOf("net start w32time")<timeSetOfferStopped.Message.IndexOf("w32tm /resync"),
				"Start it first: w32tm /resync fails while the service is stopped.");
			Assert.IsFalse(TimeSyncCheck.GetManualSetOffer(Valid(30),true,EnumWindowsTimeState.Unknown).CanSet,"A service that cannot be read.");
		}

		[TestMethod]
		public void WindowsTimeState_TheStartTypeDecidesFirst() {
			Assert.AreEqual(EnumWindowsTimeState.Disabled,TimeSyncCheck.ToWindowsTimeState(ServiceControllerStatus.Stopped,ServiceStartMode.Disabled));
			Assert.AreEqual(EnumWindowsTimeState.Disabled,TimeSyncCheck.ToWindowsTimeState(ServiceControllerStatus.Running,ServiceStartMode.Disabled));
			//Outside a domain Windows Time is "Manual (Trigger Start)": often stopped, but Windows still sets the clock with it.
			Assert.AreEqual(EnumWindowsTimeState.Stopped,TimeSyncCheck.ToWindowsTimeState(ServiceControllerStatus.Stopped,ServiceStartMode.Manual));
			Assert.AreEqual(EnumWindowsTimeState.Stopped,TimeSyncCheck.ToWindowsTimeState(ServiceControllerStatus.Paused,ServiceStartMode.Automatic));
			Assert.AreEqual(EnumWindowsTimeState.Running,TimeSyncCheck.ToWindowsTimeState(ServiceControllerStatus.Running,ServiceStartMode.Automatic));
			Assert.AreEqual(EnumWindowsTimeState.Running,TimeSyncCheck.ToWindowsTimeState(ServiceControllerStatus.StartPending,ServiceStartMode.Manual));
			string read=CSharpSource.GetBlock(TestPaths.ReadSource(@"OpenPmsDentalBusiness\Misc\WindowsTime.cs"),"public static EnumWindowsTimeState GetWindowsTimeState(");
			StringAssert.Contains(read,"ServiceController.GetServices()");
			StringAssert.Contains(read,"serviceController.StartType");
			Assert.AreEqual("W32Time",TimeSyncCheck.WINDOWS_TIME_SERVICE_NAME);
		}

		[TestMethod]
		public void Measuring_NeverSetsTheClock() {
			int countSetCalls=0;
			WindowsTime.FuncSetSystemTimeUtc=(dateTimeUtc) => { countSetCalls++; return 0; };
			try {
				DateTime dateTimeLocal=new DateTime(2026,11,8,9,30,0,DateTimeKind.Local);
				TimeSyncMeasurement timeSyncMeasurement=TimeSyncCheck.Measure(() => Valid(2.5),() => dateTimeLocal.AddSeconds(1.5),() => dateTimeLocal);
				Assert.AreEqual(0,countSetCalls,"Measuring calls no clock.");
				Assert.AreEqual(1.5,(timeSyncMeasurement.DateTimeServer-timeSyncMeasurement.DateTimeLocal).TotalSeconds,0.0001);
				Assert.AreEqual(2.5,(timeSyncMeasurement.DateTimeNtp-timeSyncMeasurement.DateTimeLocal).TotalSeconds,0.0001);
				string message=TimeSyncCheck.GetMeasureMessage(timeSyncMeasurement);
				StringAssert.Contains(message,"This computer's clock is 2.5 seconds off "+SERVER);
				StringAssert.Contains(message,"The database server's clock is 1.5 seconds off this computer's clock");
				TimeSyncMeasurement timeSyncMeasurementInvalid=TimeSyncCheck.Measure(() => WithServer(NtpResult.Invalid("stratum 16")),() => dateTimeLocal,
					() => dateTimeLocal);
				Assert.AreEqual(DateTime.MinValue,timeSyncMeasurementInvalid.DateTimeNtp);
				StringAssert.Contains(TimeSyncCheck.GetMeasureMessage(timeSyncMeasurementInvalid),"stratum 16");
				StringAssert.Contains(TimeSyncCheck.GetMeasureMessage(TimeSyncCheck.Measure(() => Valid(0.2),() => dateTimeLocal,() => dateTimeLocal)),
					"All times synchronized within one second.");
				Assert.AreEqual(0,countSetCalls);
			}
			finally {
				ClockGuard.Install();
			}
		}

		[TestMethod]
		public void OpeningTheWindow_OnlyMeasures_AndOnlyTheButtonSetsTheClock() {
			string source=TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEhrTimeSynch.cs");
			string load=CSharpSource.GetBlock(source,"private void FormEhrTime_Load(");
			string measure=CSharpSource.GetBlock(source,"private void MeasureTimes()");
			string refresh=CSharpSource.GetBlock(source,"private void refreshDisplays()");
			string measureClick=CSharpSource.GetBlock(source,"private void butRefreshTime_Click(");
			StringAssert.Contains(load,"MeasureTimes();");
			StringAssert.Contains(measure,"TimeSyncCheck.Measure(() => new NTPv4().Query(server),MiscData.GetNowDateTimeWithMilli,() => DateTime.Now)");
			string show=CSharpSource.GetBlock(source,"private void ShowMeasurement(");
			StringAssert.Contains(measure,"ShowMeasurement(timeSyncMeasurement);");
			foreach(string block in new[] { load,measure,show,refresh,measureClick,CSharpSource.GetBlock(source,"private void RefreshSetButton()") }) {
				Assert.IsFalse(Regex.IsMatch(block,@"\bWindowsTime\.|\bTrySetManual\("),"Measuring never sets the clock: "+block.Substring(0,40));
			}
			//The one call that sets the clock: on the button, from its own fresh measurement, after the offer's checks on that measurement and the
			//confirmation, with the audit entry after it.
			string code=CSharpSource.Read(source).CodeWithoutComments;
			Assert.AreEqual(0,Regex.Matches(code,@"WindowsTime\.").Count,"The window reaches the clock only through TimeSyncCheck.TrySetManual.");
			Assert.AreEqual(1,Regex.Matches(code,@"TimeSyncCheck\.TrySetManual\(").Count);
			string button=CSharpSource.GetBlock(source,"private void butSetClock_Click(");
			int indexAuthorized=button.IndexOf("Security.IsAuthorized(EnumPermType.Setup)",StringComparison.Ordinal);
			int indexLimit=button.IndexOf("if(timerSendingLimit.Enabled) {",StringComparison.Ordinal);
			int indexMeasure=button.IndexOf("TimeSyncCheck.PrepareManualSet(() => new NTPv4().Query(server),MiscData.GetNowDateTimeWithMilli,",StringComparison.Ordinal);
			int indexStopwatch=button.IndexOf("Stopwatch stopwatchSinceMeasured=Stopwatch.StartNew();",StringComparison.Ordinal);
			int indexOffer=button.IndexOf("if(!timeSyncManualSet.TimeSetOffer.CanSet) {",StringComparison.Ordinal);
			int indexRefusal=button.IndexOf("MsgBox.Show(TimeSyncCheck.GetManualSetRefusal(timeSyncManualSet));",StringComparison.Ordinal);
			int indexConfirm=button.IndexOf("MsgBox.Show(MsgBoxButtons.OKCancel,timeSyncManualSet.Question)",StringComparison.Ordinal);
			int indexSet=button.IndexOf("TimeSyncCheck.TrySetManual(timeSyncManualSet,stopwatchSinceMeasured.Elapsed,() => DateTime.UtcNow,",StringComparison.Ordinal);
			int indexLog=button.IndexOf("SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,",StringComparison.Ordinal);
			Assert.IsTrue(indexAuthorized>=0 && indexAuthorized<indexLimit && indexLimit<indexMeasure && indexMeasure<indexStopwatch && indexStopwatch<indexOffer
				&& indexOffer<indexRefusal && indexRefusal<indexConfirm && indexConfirm<indexSet && indexSet<indexLog,
				"Setup, the 4-second limit, the fresh measurement, its stopwatch, the offer on it, the refusal, the confirmation, the set, the audit entry: "
				+indexAuthorized+" "+indexLimit+" "+indexMeasure+" "+indexStopwatch+" "+indexOffer+" "+indexRefusal+" "+indexConfirm+" "+indexSet+" "+indexLog);
			StringAssert.Contains(button,"() => DateTime.Now,true,TimeSyncCheck.GetWindowsTimeState());");
			StringAssert.Contains(button,"TimeSyncCheck.GetManualSetLogText(timeSyncManualSet.TimeSyncMeasurement.NtpResult.Server,dateTimeOldUtc,dateTimeNewUtc)");
			StringAssert.Contains(button,"MsgBox.Show(error);");
			//The window's last reply is never read by the button: it is only cleared after the clock was set.
			Assert.AreEqual(0,Regex.Matches(button,@"_ntpResultLast(?!=null;)").Count,"The button never applies the last measurement.");
			StringAssert.Contains(button,"_ntpResultLast=null;");
			StringAssert.Contains(CSharpSource.GetBlock(source,"private void RefreshSetButton()"),"butSetClock.Enabled=timeSetOffer.CanSet;");
			//The paths that set the clock without these checks are gone.
			foreach(string removed in new[] { "IsAutoLaunch","TimesInSynchFast","ServerInSynchFast","SynchTimes","SetTime(" }) {
				Assert.IsFalse(code.Contains(removed),removed);
			}
			StringAssert.Contains(TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEhrTimeSynch.designer.cs"),"this.butSetClock.Enabled = false;");
		}

		[TestMethod]
		public void SetButton_MeasuresAgain_AndAppliesOnlyTheFreshMeasurement() {
			List<DateTime> listCalls=new List<DateTime>();
			WindowsTime.FuncSetSystemTimeUtc=(dateTimeUtc) => { listCalls.Add(dateTimeUtc); return 0; };
			try {
				DateTime dateTimeLocal=new DateTime(2026,11,8,9,30,0,DateTimeKind.Local);
				DateTime dateTimeUtcNow=new DateTime(2026,11,8,17,30,0,DateTimeKind.Utc);
				//The window's last measurement, taken long ago against another server: 600 seconds off. It would be offered, but it is never applied.
				TimeSyncMeasurement timeSyncMeasurementStale=TimeSyncCheck.Measure(() => Valid(600,SERVER_OLD),() => dateTimeLocal,() => dateTimeLocal);
				Assert.IsTrue(TimeSyncCheck.GetManualSetOffer(timeSyncMeasurementStale.NtpResult,true,EnumWindowsTimeState.Disabled).CanSet);
				//The button's own measurement, through the same seams: the clock is now 30 seconds behind.
				int countQueries=0;
				TimeSyncManualSet timeSyncManualSet=TimeSyncCheck.PrepareManualSet(() => { countQueries++; return Valid(30); },() => dateTimeLocal,
					() => dateTimeLocal,true,EnumWindowsTimeState.Disabled);
				Assert.AreEqual(1,countQueries,"The button measures again.");
				Assert.AreEqual(0,listCalls.Count,"Measuring sets nothing.");
				Assert.IsTrue(timeSyncManualSet.TimeSetOffer.CanSet);
				Assert.AreEqual(30,(timeSyncManualSet.TimeSyncMeasurement.DateTimeNtp-timeSyncManualSet.TimeSyncMeasurement.DateTimeLocal).TotalSeconds,0.0001,
					"The window shows the fresh measurement.");
				StringAssert.Contains(timeSyncManualSet.Question,"Measured again just now: this computer's clock is 30 seconds behind "+SERVER+".");
				StringAssert.Contains(timeSyncManualSet.Question,"to the time of "+SERVER+"?");
				Assert.IsFalse(timeSyncManualSet.Question.Contains("600") || timeSyncManualSet.Question.Contains(SERVER_OLD),"The confirmation shows the fresh server and offset.");
				Assert.IsTrue(TimeSyncCheck.TrySetManual(timeSyncManualSet,TimeSpan.FromSeconds(8),() => dateTimeUtcNow,out DateTime dateTimeOldUtc,
					out DateTime dateTimeNewUtc,out string error),error);
				Assert.AreEqual("",error);
				Assert.AreEqual(1,listCalls.Count);
				Assert.AreEqual(dateTimeUtcNow.AddSeconds(30),listCalls[0],"The fresh offset, never the stale 600 seconds.");
				Assert.AreEqual(dateTimeUtcNow,dateTimeOldUtc);
				Assert.AreEqual(dateTimeUtcNow.AddSeconds(30),dateTimeNewUtc);
				Assert.AreEqual(DateTimeKind.Utc,dateTimeOldUtc.Kind);
				//A clock that is ahead is set back by the fresh offset; a local "now" is converted to UTC first.
				TimeSyncManualSet timeSyncManualSetAhead=TimeSyncCheck.PrepareManualSet(() => Valid(-12.5),() => dateTimeLocal,() => dateTimeLocal,true,
					EnumWindowsTimeState.NotInstalled);
				Assert.IsTrue(TimeSyncCheck.TrySetManual(timeSyncManualSetAhead,TimeSpan.Zero,() => dateTimeUtcNow.ToLocalTime(),out dateTimeOldUtc,
					out dateTimeNewUtc,out error),error);
				Assert.AreEqual(2,listCalls.Count);
				Assert.AreEqual(dateTimeUtcNow.AddSeconds(-12.5),listCalls[1]);
				Assert.AreEqual(dateTimeUtcNow,dateTimeOldUtc);
			}
			finally {
				ClockGuard.Install();
			}
		}

		[TestMethod]
		public void SetButton_FreshReplyInvalidOrOfferGone_SetsNothingAndSaysWhy() {
			int countSetCalls=0;
			WindowsTime.FuncSetSystemTimeUtc=(dateTimeUtc) => { countSetCalls++; return 0; };
			try {
				DateTime dateTimeLocal=new DateTime(2026,11,8,9,30,0,DateTimeKind.Local);
				DateTime dateTimeUtcNow=new DateTime(2026,11,8,17,30,0,DateTimeKind.Utc);
				//The last measurement allowed setting the clock (30 seconds off, Windows Time disabled); each fresh one below does not.
				Assert.IsTrue(TimeSyncCheck.GetManualSetOffer(Valid(30),true,EnumWindowsTimeState.Disabled).CanSet);
				List<Tuple<NtpResult,bool,EnumWindowsTimeState,string>> listCases=new List<Tuple<NtpResult,bool,EnumWindowsTimeState,string>> {
					Tuple.Create(WithServer(NtpResult.Invalid("stratum 16")),true,EnumWindowsTimeState.Disabled,"is not valid, so the clock cannot be set: stratum 16"),
					Tuple.Create(WithServer(NtpResult.NoReply("timeout")),true,EnumWindowsTimeState.Disabled,"timeout"),
					Tuple.Create(KissOfDeath(),true,EnumWindowsTimeState.Disabled,"kiss-o'-death RATE"),
					Tuple.Create(Valid(double.NaN),true,EnumWindowsTimeState.Disabled,"gave no offset"),
					Tuple.Create(Valid(0.3),true,EnumWindowsTimeState.Disabled,"There is nothing to set."),
					Tuple.Create(Valid(7200),true,EnumWindowsTimeState.Disabled,"by hand in Windows"),
					Tuple.Create(Valid(30),false,EnumWindowsTimeState.Disabled,"Setup permission"),
					Tuple.Create(Valid(30),true,EnumWindowsTimeState.Running,"w32tm /resync"),
					Tuple.Create(Valid(30),true,EnumWindowsTimeState.Stopped,"net start w32time"),
					Tuple.Create(Valid(30),true,EnumWindowsTimeState.Unknown,"could not be read"),
				};
				foreach(Tuple<NtpResult,bool,EnumWindowsTimeState,string> tuple in listCases) {
					string what=tuple.Item1.Status+" "+tuple.Item1.OffsetSeconds+", Setup "+tuple.Item2+", "+tuple.Item3;
					TimeSyncManualSet timeSyncManualSet=TimeSyncCheck.PrepareManualSet(() => tuple.Item1,() => dateTimeLocal,() => dateTimeLocal,tuple.Item2,tuple.Item3);
					Assert.IsFalse(timeSyncManualSet.TimeSetOffer.CanSet,what);
					Assert.AreEqual("",timeSyncManualSet.Question,what+": no confirmation.");
					string refusal=TimeSyncCheck.GetManualSetRefusal(timeSyncManualSet);
					StringAssert.StartsWith(refusal,"Measured again just now; the clock was not set.",what);
					StringAssert.Contains(refusal,tuple.Item4,what);
					Assert.IsFalse(TimeSyncCheck.TrySetManual(timeSyncManualSet,TimeSpan.Zero,() => dateTimeUtcNow,out DateTime dateTimeOldUtc,
						out DateTime dateTimeNewUtc,out string error),what);
					StringAssert.Contains(error,tuple.Item4,what);
					Assert.AreEqual(DateTime.MinValue,dateTimeNewUtc,what);
				}
				//No measurement at all.
				Assert.IsFalse(TimeSyncCheck.TrySetManual(null,TimeSpan.Zero,() => dateTimeUtcNow,out _,out _,out string errorNull));
				StringAssert.Contains(errorNull,"the clock was not set");
				//An offer changed after the measurement (not by the window) still cannot reach the clock with a reply that breaks the rules.
				TimeSyncManualSet timeSyncManualSetForged=TimeSyncCheck.PrepareManualSet(() => Valid(7200),() => dateTimeLocal,() => dateTimeLocal,true,
					EnumWindowsTimeState.Disabled);
				timeSyncManualSetForged.TimeSetOffer.CanSet=true;
				Assert.IsFalse(TimeSyncCheck.TrySetManual(timeSyncManualSetForged,TimeSpan.Zero,() => dateTimeUtcNow,out _,out _,out string errorForged));
				StringAssert.Contains(errorForged,"The clock was not set");
				//A confirmation answered too late: the measurement may no longer describe the clock, so nothing is set.
				TimeSyncManualSet timeSyncManualSetOld=TimeSyncCheck.PrepareManualSet(() => Valid(30),() => dateTimeLocal,() => dateTimeLocal,true,
					EnumWindowsTimeState.Disabled);
				Assert.IsTrue(timeSyncManualSetOld.TimeSetOffer.CanSet);
				Assert.AreEqual(TimeSpan.FromMinutes(2),TimeSyncCheck.ManualSetMaxAge);
				Assert.IsFalse(TimeSyncCheck.TrySetManual(timeSyncManualSetOld,TimeSyncCheck.ManualSetMaxAge.Add(TimeSpan.FromSeconds(1)),() => dateTimeUtcNow,
					out _,out _,out string errorOld));
				StringAssert.Contains(errorOld,"more than 2 minutes old");
				Assert.IsFalse(TimeSyncCheck.TrySetManual(timeSyncManualSetOld,TimeSpan.FromSeconds(-1),() => dateTimeUtcNow,out _,out _,out errorOld),
					"A stopwatch never runs backwards.");
				Assert.AreEqual(0,countSetCalls,"Nothing above reached the clock.");
				Assert.IsTrue(TimeSyncCheck.TrySetManual(timeSyncManualSetOld,TimeSyncCheck.ManualSetMaxAge,() => dateTimeUtcNow,out _,out _,out errorOld),
					"Answered within the limit.");
				Assert.AreEqual(1,countSetCalls);
			}
			finally {
				ClockGuard.Install();
			}
		}

		[TestMethod]
		public void ManualSet_QuestionAndAuditText_NameTheServerAndBothTimes() {
			StringAssert.Contains(TimeSyncCheck.GetManualSetQuestion(Valid(12)),"12 seconds behind "+SERVER);
			StringAssert.Contains(TimeSyncCheck.GetManualSetQuestion(Valid(-12)),"12 seconds ahead of "+SERVER);
			string log=TimeSyncCheck.GetManualSetLogText(SERVER,new DateTime(2026,11,8,17,0,0,DateTimeKind.Utc),new DateTime(2026,11,8,17,0,12,250,DateTimeKind.Utc));
			StringAssert.Contains(log,"2026-11-08 17:00:00.000 UTC to 2026-11-08 17:00:12.250 UTC");
			StringAssert.Contains(log,SERVER);
		}
		#endregion The manual window

		#region TrySetUtc
		[TestMethod]
		public void TheSeam_IsReplacedBeforeAnyTest() {
			Assert.AreEqual("SetSystemTimeUtcNative",ClockGuard.NameOfOriginalSeam,"The default seam is the real call, which no test reaches.");
			Assert.AreNotEqual("SetSystemTimeUtcNative",WindowsTime.FuncSetSystemTimeUtc.Method.Name);
			Assert.AreEqual(0,ClockGuard.CountBlockedCalls,"No test reached the clock without a fake.");
			//Both Event Log seams too: their defaults write this computer's Event Log, which no test does.
			Assert.AreEqual("WriteEventLogNative",ClockGuard.NameOfOriginalEventLogSeamNtp,"NTPv4's default Event Log seam is the real call.");
			Assert.AreEqual("WriteEventLogNative",ClockGuard.NameOfOriginalEventLogSeamWindowsTime,"WindowsTime's default Event Log seam is the real call.");
			Assert.AreNotEqual("WriteEventLogNative",NTPv4.ActionWriteEventLog.Method.Name,"NTPv4's Event Log seam is replaced.");
			Assert.AreNotEqual("WriteEventLogNative",WindowsTime.ActionWriteEventLog.Method.Name,"WindowsTime's Event Log seam is replaced.");
		}

		[TestMethod]
		public void TrySetUtc_PassesUtc_AndWritesTheEventLog() {
			List<DateTime> listCalls=new List<DateTime>();
			List<string> listEventLog=new List<string>();
			WindowsTime.FuncSetSystemTimeUtc=(dateTimeUtc) => { listCalls.Add(dateTimeUtc); return 0; };
			WindowsTime.ActionWriteEventLog=(text) => listEventLog.Add(text);
			try {
				DateTime dateTimeUtc=new DateTime(2026,11,8,17,0,12,250,DateTimeKind.Utc);
				Assert.IsTrue(WindowsTime.TrySetUtc(dateTimeUtc,out string error));
				Assert.AreEqual("",error);
				Assert.IsTrue(WindowsTime.TrySetUtc(dateTimeUtc.ToLocalTime(),out error),"A local time is converted to UTC.");
				Assert.IsTrue(WindowsTime.TrySetUtc(DateTime.SpecifyKind(dateTimeUtc,DateTimeKind.Unspecified),out error),"Unspecified counts as UTC.");
				Assert.AreEqual(3,listCalls.Count);
				foreach(DateTime dateTimeCall in listCalls) {
					Assert.AreEqual(DateTimeKind.Utc,dateTimeCall.Kind);
					Assert.AreEqual(dateTimeUtc.Ticks,dateTimeCall.Ticks);
				}
				Assert.AreEqual(3,listEventLog.Count);
				StringAssert.Contains(listEventLog[0],"System date and time set to:  2026-11-08 17:00:12.250 UTC");
			}
			finally {
				ClockGuard.Install();
			}
		}

		[TestMethod]
		public void TrySetUtc_FailureGivesTheReason_NotSilence() {
			List<string> listEventLog=new List<string>();
			WindowsTime.ActionWriteEventLog=(text) => listEventLog.Add(text);
			try {
				WindowsTime.FuncSetSystemTimeUtc=(dateTimeUtc) => WindowsTime.ERROR_PRIVILEGE_NOT_HELD;
				Assert.IsFalse(WindowsTime.TrySetUtc(DateTime.UtcNow,out string error));
				StringAssert.Contains(error,"Change the system time");
				StringAssert.Contains(error,"administrator");
				StringAssert.Contains(error,"1314");
				WindowsTime.FuncSetSystemTimeUtc=(dateTimeUtc) => 87;//ERROR_INVALID_PARAMETER
				Assert.IsFalse(WindowsTime.TrySetUtc(DateTime.UtcNow,out error));
				StringAssert.Contains(error,"The clock was not set:");
				StringAssert.Contains(error,"(Windows error 87)");
				WindowsTime.FuncSetSystemTimeUtc=(dateTimeUtc) => -1;
				Assert.IsFalse(WindowsTime.TrySetUtc(DateTime.UtcNow,out error));
				StringAssert.Contains(error,"The clock was not set:");
				WindowsTime.FuncSetSystemTimeUtc=(dateTimeUtc) => { throw new InvalidOperationException("seam failure"); };
				Assert.IsFalse(WindowsTime.TrySetUtc(DateTime.UtcNow,out error));
				StringAssert.Contains(error,"seam failure");
				Assert.AreEqual(0,listEventLog.Count,"A failure writes no success entry.");
				Assert.AreEqual(WindowsTime.ERROR_PRIVILEGE_NOT_HELD,1314);
			}
			finally {
				ClockGuard.Install();
			}
		}

		[TestMethod]
		public void OnlyTrySetUtc_ReachesSetSystemTime() {
			string source=TestPaths.ReadSource(@"OpenPmsDentalBusiness\Misc\WindowsTime.cs");
			string code=CSharpSource.Read(source).CodeWithoutComments;
			StringAssert.Contains(code,"[DllImport(\"kernel32.dll\",SetLastError=true)]");
			Assert.AreEqual(1,Regex.Matches(code,@"extern static bool SetSystemTime\(ref SYSTEMTIME lpSystemTime\);").Count,"The import.");
			Assert.AreEqual(2,Regex.Matches(code,@"\bSetSystemTime\(").Count,"The import and one call, in the seam's real method.");
			StringAssert.Contains(CSharpSource.GetBlock(source,"private static int SetSystemTimeUtcNative("),"if(SetSystemTime(ref systemTime)) {");
			Assert.AreEqual(1,Regex.Matches(code,@"FuncSetSystemTimeUtc\(").Count,"One call of the seam, in TrySetUtc.");
			StringAssert.Contains(CSharpSource.GetBlock(source,"public static bool TrySetUtc("),"win32Error=FuncSetSystemTimeUtc(dateTimeUtc);");
			StringAssert.Contains(CSharpSource.GetBlock(source,"private static int SetSystemTimeUtcNative("),"Marshal.GetLastWin32Error()");
			//TrySetUtc has one caller, TimeSyncCheck.TrySetManual, which sets the clock from the set button's own measurement.
			Assert.AreEqual(2,Regex.Matches(code,@"\bTrySetUtc\(").Count,"The method and its one call.");
			StringAssert.Contains(CSharpSource.GetBlock(source,"public static bool TrySetManual("),"return WindowsTime.TrySetUtc(dateTimeNewUtc,out error);");
			//Nowhere else in the program: no SetLocalTime, no other SetSystemTime, no other caller of TrySetUtc, and no caller of TrySetManual but the
			//manual window's button.
			List<string> listProblems=new List<string>();
			foreach(string path in TestPaths.ProgramSourceFiles()) {
				string relative=TestPaths.Relative(path);
				string codeFile=CSharpSource.Read(System.IO.File.ReadAllText(path)).CodeWithoutComments;
				if(codeFile.Contains("SetLocalTime")) {
					listProblems.Add(relative+": SetLocalTime");
				}
				if(!relative.Equals(@"OpenPmsDentalBusiness\Misc\WindowsTime.cs",StringComparison.OrdinalIgnoreCase)
					&& (Regex.IsMatch(codeFile,@"\bSetSystemTime\b") || codeFile.Contains("FuncSetSystemTimeUtc")))
				{
					listProblems.Add(relative+": SetSystemTime");
				}
				if(!relative.Equals(@"OpenPmsDentalBusiness\Misc\WindowsTime.cs",StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(codeFile,@"\bTrySetUtc\(")) {
					listProblems.Add(relative+": WindowsTime.TrySetUtc");
				}
				if(!relative.Equals(@"OpenPmsDental\Forms\FormEhrTimeSynch.cs",StringComparison.OrdinalIgnoreCase)
					&& !relative.Equals(@"OpenPmsDentalBusiness\Misc\WindowsTime.cs",StringComparison.OrdinalIgnoreCase)
					&& Regex.IsMatch(codeFile,@"\bTrySetManual\("))
				{
					listProblems.Add(relative+": TimeSyncCheck.TrySetManual");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("; ",listProblems));
		}

		[TestMethod]
		public void BothTimeFiles_WriteTheEventLogThroughProgramNames() {
			//ReleaseFlowTests forbids a literal source in both files (ProgramNamesTests.RuntimeNameSites_UseProgramNames); this checks they still write.
			foreach(string path in new[] { @"OpenPmsDentalBusiness\Misc\NTPv4.cs",@"OpenPmsDentalBusiness\Misc\WindowsTime.cs" }) {
				string code=CSharpSource.Read(TestPaths.ReadSource(path)).CodeWithoutComments;
				Assert.AreEqual(1,Regex.Matches(code,@"EventLog\.WriteEntry\(CodeBase\.ProgramNames\.EventLogSource,").Count,path);
				Assert.IsFalse(Regex.IsMatch(code,@"EventLog\.WriteEntry\(\s*"""),path+": a literal source.");
			}
		}
		#endregion TrySetUtc
	}
}
