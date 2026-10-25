// Rules for clock-outs dated in the future (Open Dental 26.1.63, feature v26_1-manage-timecard-future-clockout, package M2-EMP). No database:
// these tests call the rule methods with fixed dates.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2EMPTests {
	[TestClass]
	public class FutureClockOutTests {
		private static readonly DateTime _dateToday=new DateTime(2026,10,25);

		private static EnumClockEventFutureDate Check(TimeClockStatus statusOld,TimeClockStatus statusNew,DateTime time1,DateTime time2,bool hasEditAll) {
			return ClockEvents.CheckFutureDates(statusOld,statusNew,time1,time2,_dateToday,hasEditAll);
		}

		[TestMethod]
		public void CheckFutureDates_ClockOutTomorrow_NeedsConfirmationForEditAll() {
			Assert.AreEqual(EnumClockEventFutureDate.ClockOutConfirm,
				Check(TimeClockStatus.Home,TimeClockStatus.Home,_dateToday.AddHours(8),_dateToday.AddDays(1).AddHours(17),hasEditAll:true));
			Assert.AreEqual(EnumClockEventFutureDate.ClockOutConfirm,
				Check(TimeClockStatus.Lunch,TimeClockStatus.Lunch,_dateToday.AddHours(8),_dateToday.AddDays(30).AddHours(1),hasEditAll:true));
		}

		[TestMethod]
		public void CheckFutureDates_ClockOutTomorrow_RefusedWithoutEditAll() {
			Assert.AreEqual(EnumClockEventFutureDate.SecondTimeRefused,
				Check(TimeClockStatus.Home,TimeClockStatus.Home,_dateToday.AddHours(8),_dateToday.AddDays(1).AddHours(17),hasEditAll:false));
		}

		[TestMethod]
		public void CheckFutureDates_Breaks_StillRefuseBothTimes() {
			DateTime time1=_dateToday.AddHours(10);
			DateTime time2=_dateToday.AddDays(1).AddHours(10);
			Assert.AreEqual(EnumClockEventFutureDate.SecondTimeRefused,Check(TimeClockStatus.Break,TimeClockStatus.Break,time1,time2,hasEditAll:true));
			//Changing a break into a Home event, or the other way round, in the same edit is refused too.
			Assert.AreEqual(EnumClockEventFutureDate.SecondTimeRefused,Check(TimeClockStatus.Break,TimeClockStatus.Home,time1,time2,hasEditAll:true));
			Assert.AreEqual(EnumClockEventFutureDate.SecondTimeRefused,Check(TimeClockStatus.Home,TimeClockStatus.Break,time1,time2,hasEditAll:true));
			Assert.AreEqual(EnumClockEventFutureDate.FirstTimeRefused,
				Check(TimeClockStatus.Break,TimeClockStatus.Break,_dateToday.AddDays(1).AddHours(10),DateTime.MinValue,hasEditAll:true));
		}

		[TestMethod]
		public void CheckFutureDates_ClockInTomorrow_AlwaysRefused() {
			Assert.AreEqual(EnumClockEventFutureDate.FirstTimeRefused,
				Check(TimeClockStatus.Home,TimeClockStatus.Home,_dateToday.AddDays(1).AddHours(8),_dateToday.AddDays(1).AddHours(17),hasEditAll:true));
			Assert.AreEqual(EnumClockEventFutureDate.FirstTimeRefused,
				Check(TimeClockStatus.Home,TimeClockStatus.Home,_dateToday.AddDays(1).AddHours(8),DateTime.MinValue,hasEditAll:true));
		}

		[TestMethod]
		public void CheckFutureDates_TodayOrEarlier_Ok() {
			//Only dates count, as in 24.3: a clock-out later today needs no question, for anyone.
			Assert.AreEqual(EnumClockEventFutureDate.Ok,
				Check(TimeClockStatus.Home,TimeClockStatus.Home,_dateToday.AddHours(8),_dateToday.AddHours(23).AddMinutes(59),hasEditAll:false));
			Assert.AreEqual(EnumClockEventFutureDate.Ok,
				Check(TimeClockStatus.Break,TimeClockStatus.Break,_dateToday.AddHours(10),_dateToday.AddHours(23),hasEditAll:false));
			Assert.AreEqual(EnumClockEventFutureDate.Ok,
				Check(TimeClockStatus.Home,TimeClockStatus.Home,_dateToday.AddDays(-3).AddHours(8),_dateToday.AddDays(-3).AddHours(17),hasEditAll:false));
			Assert.AreEqual(EnumClockEventFutureDate.Ok,Check(TimeClockStatus.Home,TimeClockStatus.Home,_dateToday.AddHours(8),DateTime.MinValue,false));
		}

		[TestMethod]
		public void HasFutureClockOut_OnlyHomeOrLunchWithAClockOutAhead() {
			DateTime now=_dateToday.AddHours(12);
			ClockEvent clockEvent=new ClockEvent { ClockStatus=TimeClockStatus.Home,TimeDisplayed1=_dateToday.AddHours(8),TimeDisplayed2=now.AddMinutes(1) };
			Assert.IsTrue(ClockEvents.HasFutureClockOut(clockEvent,now));
			clockEvent.ClockStatus=TimeClockStatus.Lunch;
			Assert.IsTrue(ClockEvents.HasFutureClockOut(clockEvent,now));
			clockEvent.ClockStatus=TimeClockStatus.Break;
			Assert.IsFalse(ClockEvents.HasFutureClockOut(clockEvent,now),"A break's second time is the return from the break.");
			clockEvent.ClockStatus=TimeClockStatus.Home;
			clockEvent.TimeDisplayed2=now;
			Assert.IsFalse(ClockEvents.HasFutureClockOut(clockEvent,now),"A clock-out at this very moment has happened.");
			clockEvent.TimeDisplayed2=now.AddHours(-1);
			Assert.IsFalse(ClockEvents.HasFutureClockOut(clockEvent,now));
			clockEvent.TimeDisplayed2=DateTime.MinValue;
			Assert.IsFalse(ClockEvents.HasFutureClockOut(clockEvent,now),"Not clocked out at all is the ordinary clocked-in case.");
			Assert.IsFalse(ClockEvents.HasFutureClockOut(null,now));
		}

		[TestMethod]
		public void GetSecondTimeCellText_ShowsTheDateOnlyForALaterDate() {
			ClockEvent clockEvent=new ClockEvent { ClockStatus=TimeClockStatus.Home,TimeDisplayed1=_dateToday.AddHours(8),
				TimeDisplayed2=_dateToday.AddHours(17) };
			Assert.AreEqual(clockEvent.TimeDisplayed2.ToShortTimeString(),ClockEvents.GetSecondTimeCellText(clockEvent,showSeconds:false));
			Assert.AreEqual(clockEvent.TimeDisplayed2.ToLongTimeString(),ClockEvents.GetSecondTimeCellText(clockEvent,showSeconds:true));
			clockEvent.TimeDisplayed2=_dateToday.AddDays(1).AddHours(17);
			Assert.AreEqual(clockEvent.TimeDisplayed2.ToShortDateString()+" "+clockEvent.TimeDisplayed2.ToShortTimeString(),
				ClockEvents.GetSecondTimeCellText(clockEvent,showSeconds:false));
			clockEvent.TimeDisplayed2=DateTime.MinValue;
			Assert.AreEqual("",ClockEvents.GetSecondTimeCellText(clockEvent,showSeconds:false));
		}

		[TestMethod]
		public void GetSecondTimeColumnWidth_WidensOnlyWhenADateShows() {
			List<ClockEvent> listClockEvents=new List<ClockEvent> {
				new ClockEvent { TimeDisplayed1=_dateToday.AddHours(8),TimeDisplayed2=_dateToday.AddHours(17) },
				new ClockEvent { TimeDisplayed1=_dateToday.AddDays(1).AddHours(8),TimeDisplayed2=DateTime.MinValue },
			};
			Assert.AreEqual(64,ClockEvents.GetSecondTimeColumnWidth(listClockEvents,64));
			Assert.AreEqual(64,ClockEvents.GetSecondTimeColumnWidth(new List<ClockEvent>(),64));
			Assert.AreEqual(64,ClockEvents.GetSecondTimeColumnWidth(null,64));
			listClockEvents.Add(new ClockEvent { TimeDisplayed1=_dateToday.AddHours(8),TimeDisplayed2=_dateToday.AddDays(1).AddHours(17) });
			Assert.AreEqual(ClockEvents.WIDTH_SECOND_TIME_WITH_DATE,ClockEvents.GetSecondTimeColumnWidth(listClockEvents,64));
			Assert.AreEqual(200,ClockEvents.GetSecondTimeColumnWidth(listClockEvents,200),"Never narrower than the default.");
		}

		[TestMethod]
		public void GetFutureClockOutLogText_HasTheOldAndNewTimes() {
			ClockEvent clockEventOld=new ClockEvent { TimeDisplayed1=_dateToday.AddHours(8),TimeDisplayed2=DateTime.MinValue };
			ClockEvent clockEventNew=new ClockEvent { TimeDisplayed1=_dateToday.AddHours(8),TimeDisplayed2=_dateToday.AddDays(1).AddHours(17) };
			string text=ClockEvents.GetFutureClockOutLogText("Pat Synthetic",clockEventOld,clockEventNew);
			StringAssert.Contains(text,"Pat Synthetic");
			StringAssert.Contains(text,clockEventOld.TimeDisplayed1.ToString()+" -> "+clockEventNew.TimeDisplayed1.ToString());
			StringAssert.Contains(text,"none -> "+clockEventNew.TimeDisplayed2.ToString());
		}
	}
}
