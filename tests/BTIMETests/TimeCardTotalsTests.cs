// Tests for the Time Card window's Totals for Period (Open Dental 25.1, v25_1-manage-timecard-totals-grid): the totals keep Open Dental 24.3's
// math and are shown in one format at a time, decimal or colon. These tests need no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BTIMETests {
	[TestClass]
	public class TimeCardTotalsTests {
		private CultureInfo _cultureInfoOld;
		private static readonly DateTime _date=new DateTime(2026,3,2);

		[TestInitialize]
		public void TestInitialize() {
			//Decimal totals use the "n" format, so pin the culture for the expected strings.
			_cultureInfoOld=Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		[TestCleanup]
		public void TestCleanup() {
			Thread.CurrentThread.CurrentCulture=_cultureInfoOld;
		}

		///<summary>A finished clock event on _date plus dayOffset, from hourIn to hourOut. Pass hourOut null for one not finished yet.</summary>
		private static ClockEvent MakeEvent(int dayOffset,double hourIn,double? hourOut) {
			ClockEvent clockEvent=new ClockEvent();//The constructor sets the OT, Rate2 and Rate3 overrides to -1 hour, "not overridden".
			clockEvent.TimeDisplayed1=_date.AddDays(dayOffset).AddHours(hourIn);
			clockEvent.TimeEntered1=clockEvent.TimeDisplayed1;
			if(hourOut.HasValue) {
				clockEvent.TimeDisplayed2=_date.AddDays(dayOffset).AddHours(hourOut.Value);
				clockEvent.TimeEntered2=clockEvent.TimeDisplayed2;
			}
			return clockEvent;
		}

		private static TimeSpan H(double hours) {
			return TimeSpan.FromHours(hours);
		}

		[TestMethod]
		public void Calculate_FinishedEvents_AddToTotalAndDay() {
			List<ClockEvent> listClockEvents=new List<ClockEvent> {
				MakeEvent(0,8,12),
				MakeEvent(0,13,17.5),
				MakeEvent(1,8,16.25),
			};
			TimeCardPeriodTotals totals=TimeCardTotals.Calculate(listClockEvents,new List<TimeAdjust>(),isBreaks:false);
			Assert.AreEqual(H(16.75),totals.Total);
			Assert.AreEqual(H(16.75),totals.Regular,"With no adjustments or overtime, the Day total is the Total.");
			Assert.AreEqual(TimeSpan.Zero,totals.Adjust);
			Assert.AreEqual(TimeSpan.Zero,totals.OT);
			Assert.AreEqual(TimeSpan.Zero,totals.Rate2);
			Assert.AreEqual(TimeSpan.Zero,totals.Rate3);
			Assert.AreEqual(TimeSpan.Zero,totals.PTO);
			Assert.AreEqual(TimeSpan.Zero,totals.ProtectedLeave);
		}

		[TestMethod]
		public void Calculate_UnfinishedEvent_CountsNothing() {
			List<ClockEvent> listClockEvents=new List<ClockEvent> {
				MakeEvent(0,8,12),
				MakeEvent(0,13,null),//still clocked in
			};
			TimeCardPeriodTotals totals=TimeCardTotals.Calculate(listClockEvents,new List<TimeAdjust>(),isBreaks:false);
			Assert.AreEqual(H(4),totals.Total,"Only completed clock events count.");
			Assert.AreEqual(H(4),totals.Regular);
		}

		[TestMethod]
		public void Calculate_Adjust_UsesOverrideElseAutomatic() {
			ClockEvent clockEventAuto=MakeEvent(0,8,17);
			clockEventAuto.AdjustAuto=-H(0.5);//automatic lunch deduction
			ClockEvent clockEventOverride=MakeEvent(1,8,17);
			clockEventOverride.AdjustAuto=-H(0.5);
			clockEventOverride.Adjust=H(0.25);
			clockEventOverride.AdjustIsOverridden=true;
			TimeCardPeriodTotals totals=TimeCardTotals.Calculate(new List<ClockEvent> { clockEventAuto,clockEventOverride },null,isBreaks:false);
			Assert.AreEqual(H(18),totals.Total);
			Assert.AreEqual(H(-0.25),totals.Adjust,"-0:30 automatic plus the 0:15 override; the overridden event's automatic value is ignored.");
			Assert.AreEqual(H(17.75),totals.Regular);
		}

		[TestMethod]
		public void Calculate_Overtime_IsTakenOutOfTheDayTotal() {
			ClockEvent clockEventAuto=MakeEvent(0,6,18);
			clockEventAuto.OTimeAuto=H(4);
			ClockEvent clockEventOverride=MakeEvent(1,6,16);
			clockEventOverride.OTimeAuto=H(2);
			clockEventOverride.OTimeHours=H(1);//override
			TimeCardPeriodTotals totals=TimeCardTotals.Calculate(new List<ClockEvent> { clockEventAuto,clockEventOverride },null,isBreaks:false);
			Assert.AreEqual(H(22),totals.Total);
			Assert.AreEqual(H(5),totals.OT);
			Assert.AreEqual(H(17),totals.Regular,"Day (regular time) is Total plus Adjust minus clock event overtime.");
		}

		[TestMethod]
		public void Calculate_Rate2AndRate3_UseOverrideElseAutomatic() {
			ClockEvent clockEventAuto=MakeEvent(0,8,20);
			clockEventAuto.Rate2Auto=H(2);
			clockEventAuto.Rate3Auto=H(1);
			ClockEvent clockEventOverride=MakeEvent(1,8,20);
			clockEventOverride.Rate2Auto=H(2);
			clockEventOverride.Rate2Hours=H(3);
			clockEventOverride.Rate3Auto=H(1);
			clockEventOverride.Rate3Hours=TimeSpan.Zero;//an override to zero still overrides
			TimeCardPeriodTotals totals=TimeCardTotals.Calculate(new List<ClockEvent> { clockEventAuto,clockEventOverride },null,isBreaks:false);
			Assert.AreEqual(H(5),totals.Rate2);
			Assert.AreEqual(H(1),totals.Rate3);
			Assert.AreEqual(H(24),totals.Regular,"Rate2 and Rate3 hours are part of regular time, as in 24.3.");
		}

		[TestMethod]
		public void Calculate_TimeAdjusts_GoToAdjustPtoOtAndProtectedLeave() {
			List<ClockEvent> listClockEvents=new List<ClockEvent> { MakeEvent(0,8,18) };
			List<TimeAdjust> listTimeAdjusts=new List<TimeAdjust> {
				new TimeAdjust { TimeEntry=_date.AddHours(19),RegHours=H(1.5) },//regular adjustment
				new TimeAdjust { TimeEntry=_date.AddDays(1).AddHours(9),PtoDefNum=7,PtoHours=H(8) },//PTO
				new TimeAdjust { TimeEntry=_date.AddDays(2).AddHours(9),IsUnpaidProtectedLeave=true,RegHours=H(4) },//protected leave
				new TimeAdjust { TimeEntry=_date.AddDays(3).AddHours(9),RegHours=-H(2),OTimeHours=H(2) },//overtime adjustment (Calc Week OT)
			};
			TimeCardPeriodTotals totals=TimeCardTotals.Calculate(listClockEvents,listTimeAdjusts,isBreaks:false);
			Assert.AreEqual(H(10),totals.Total);
			Assert.AreEqual(H(-0.5),totals.Adjust,"1:30 regular adjustment and the -2:00 of the overtime adjustment.");
			Assert.AreEqual(H(8),totals.PTO);
			Assert.AreEqual(H(4),totals.ProtectedLeave);
			Assert.AreEqual(H(2),totals.OT);
			Assert.AreEqual(H(9.5),totals.Regular,"PTO and protected leave are not regular time; the overtime adjustment already moved its hours.");
		}

		[TestMethod]
		public void Calculate_Breaks_IgnoreAdjustments() {
			List<ClockEvent> listBreaks=new List<ClockEvent> {
				MakeEvent(0,10,10.25),
				MakeEvent(0,15,15.25),
				MakeEvent(1,10,null),//on break now
			};
			List<TimeAdjust> listTimeAdjusts=new List<TimeAdjust> { new TimeAdjust { RegHours=H(3) } };
			TimeCardPeriodTotals totals=TimeCardTotals.Calculate(listBreaks,listTimeAdjusts,isBreaks:true);
			Assert.AreEqual(H(0.5),totals.Total);
			Assert.AreEqual(H(0.5),totals.Regular,"The Breaks view shows the same totals, from its break events only.");
			Assert.AreEqual(TimeSpan.Zero,totals.Adjust);
		}

		[TestMethod]
		public void Calculate_NullOrEmpty_GivesZeros() {
			TimeCardPeriodTotals totals=TimeCardTotals.Calculate(null,null,isBreaks:false);
			foreach(TimeSpan timeSpan in TimeCardTotals.GetValues(totals)) {
				Assert.AreEqual(TimeSpan.Zero,timeSpan);
			}
		}

		[TestMethod]
		public void FormatTotal_Colon() {
			Assert.AreEqual("8:30",TimeCardTotals.FormatTotal(H(8.5),EnumTimeCardTotalsFormat.Colon,showSeconds:false));
			Assert.AreEqual("40:15",TimeCardTotals.FormatTotal(H(40.25),EnumTimeCardTotalsFormat.Colon,showSeconds:false),"Hours go past 24.");
			Assert.AreEqual("-1:30",TimeCardTotals.FormatTotal(H(-1.5),EnumTimeCardTotalsFormat.Colon,showSeconds:false));
			Assert.AreEqual("8:30",TimeCardTotals.FormatTotal(new TimeSpan(8,30,59),EnumTimeCardTotalsFormat.Colon,showSeconds:false),
				"Seconds are dropped, as in the 24.3 total boxes.");
			Assert.AreEqual("0:00",TimeCardTotals.FormatTotal(TimeSpan.Zero,EnumTimeCardTotalsFormat.Colon,showSeconds:false),"A zero total is shown.");
		}

		[TestMethod]
		public void FormatTotal_ColonWithSeconds_OnlyWhenThePreferenceIsOn() {
			Assert.AreEqual("8:30:15",TimeCardTotals.FormatTotal(new TimeSpan(8,30,15),EnumTimeCardTotalsFormat.Colon,showSeconds:true));
			Assert.AreEqual("0:00:00",TimeCardTotals.FormatTotal(TimeSpan.Zero,EnumTimeCardTotalsFormat.Colon,showSeconds:true));
			Assert.AreEqual("8.50",TimeCardTotals.FormatTotal(H(8.5),EnumTimeCardTotalsFormat.Decimal,showSeconds:true),
				"Decimal format ignores the seconds preference.");
		}

		[TestMethod]
		public void FormatTotal_Decimal() {
			Assert.AreEqual("8.50",TimeCardTotals.FormatTotal(H(8.5),EnumTimeCardTotalsFormat.Decimal,showSeconds:false));
			Assert.AreEqual("40.25",TimeCardTotals.FormatTotal(H(40.25),EnumTimeCardTotalsFormat.Decimal,showSeconds:false));
			Assert.AreEqual("-1.50",TimeCardTotals.FormatTotal(H(-1.5),EnumTimeCardTotalsFormat.Decimal,showSeconds:false));
			Assert.AreEqual("0.33",TimeCardTotals.FormatTotal(TimeSpan.FromMinutes(20),EnumTimeCardTotalsFormat.Decimal,showSeconds:false),
				"Rounded to the hundredth of an hour.");
			Assert.AreEqual("1,234.50",TimeCardTotals.FormatTotal(H(1234.5),EnumTimeCardTotalsFormat.Decimal,showSeconds:false));
			Assert.AreEqual("0.00",TimeCardTotals.FormatTotal(TimeSpan.Zero,EnumTimeCardTotalsFormat.Decimal,showSeconds:false));
		}

		[TestMethod]
		public void GetCells_OneFormatForEveryColumn_InGridOrder() {
			TimeCardPeriodTotals totals=new TimeCardPeriodTotals {
				Total=H(40),Adjust=H(-1),Rate2=H(2),Rate3=H(3),PTO=H(8),OT=H(4.5),ProtectedLeave=H(6),Regular=H(34.5),
			};
			CollectionAssert.AreEqual(new List<string> { "Total","Adjust","Rate2","Rate3","PTO","OT","PL","Day" },TimeCardTotals.GetColumnCaptions(),
				"The totals line up under the time card grid's columns.");
			CollectionAssert.AreEqual(new List<string> { "40.00","-1.00","2.00","3.00","8.00","4.50","6.00","34.50" },
				TimeCardTotals.GetCells(totals,EnumTimeCardTotalsFormat.Decimal,showSeconds:false));
			CollectionAssert.AreEqual(new List<string> { "40:00","-1:00","2:00","3:00","8:00","4:30","6:00","34:30" },
				TimeCardTotals.GetCells(totals,EnumTimeCardTotalsFormat.Colon,showSeconds:false));
		}

		[TestMethod]
		public void GetDefaultFormat_FollowsThePreference() {
			FillPrefs(useDecimal:true);
			Assert.AreEqual(EnumTimeCardTotalsFormat.Decimal,TimeCardTotals.GetDefaultFormat());
			FillPrefs(useDecimal:false);
			Assert.AreEqual(EnumTimeCardTotalsFormat.Colon,TimeCardTotals.GetDefaultFormat());
		}

		///<summary>Fills the preference cache in memory. The database tests refresh it from their own database before each test.</summary>
		private static void FillPrefs(bool useDecimal) {
			Prefs.FillCacheFromTable(OpenDentBusiness.Crud.PrefCrud.ListToTable(new List<Pref> {
				new Pref { PrefNum=1,PrefName=PrefName.TimeCardsUseDecimalInsteadOfColon.ToString(),ValueString=useDecimal ? "1" : "0" },
				new Pref { PrefNum=2,PrefName=PrefName.TimeCardShowSeconds.ToString(),ValueString="0" },
			}));
		}
	}
}
