using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>Which way the Time Card window shows its period totals. Only window state: it is never saved. The window starts from the
	///TimeCardsUseDecimalInsteadOfColon preference each time it opens.</summary>
	public enum EnumTimeCardTotalsFormat {
		///<summary>Hours and minutes, like 7:45 (with seconds when TimeCardShowSeconds is on).</summary>
		Colon,
		///<summary>Hours with two decimals, like 7.75.</summary>
		Decimal,
	}

	///<summary>The totals for one pay period of one employee's time card, one per numeric column of the time card grid. Filled by
	///TimeCardTotals.Calculate.</summary>
	public class TimeCardPeriodTotals {
		///<summary>Total column: the length of every completed clock event (or break) in the period.</summary>
		public TimeSpan Total;
		///<summary>Adjust column: clock event adjustments (the override when there is one, else the automatic one) plus regular-hours time card
		///adjustments that are neither PTO nor protected leave.</summary>
		public TimeSpan Adjust;
		///<summary>Rate2 column: the override when there is one, else the automatic value.</summary>
		public TimeSpan Rate2;
		///<summary>Rate3 column: the override when there is one, else the automatic value.</summary>
		public TimeSpan Rate3;
		///<summary>PTO column: the PTO hours of PTO adjustments.</summary>
		public TimeSpan PTO;
		///<summary>OT column: clock event overtime (override, else automatic) plus the overtime of time card adjustments.</summary>
		public TimeSpan OT;
		///<summary>PL column: the hours of protected leave adjustments.</summary>
		public TimeSpan ProtectedLeave;
		///<summary>Day column: the sum of the daily totals, which is the period's regular time (Open Dental 24.3's "Regular Time" box).</summary>
		public TimeSpan Regular;
	}

	///<summary>Period totals of the Time Card window (Open Dental 25.1, "Totals for Period"): one total under each numeric column of the time
	///card grid, all in one format, chosen in the window. The math is the same as Open Dental 24.3's total boxes.</summary>
	public class TimeCardTotals {
		///<summary>The columns of the totals grid, in the order of the time card grid. GetCells returns the values in this order.</summary>
		public static List<string> GetColumnCaptions() {
			Meth.NoCheckMiddleTierRole();
			return new List<string> { "Total","Adjust","Rate2","Rate3","PTO","OT","PL","Day" };
		}

		///<summary>The format the Time Card window starts with: decimal when the TimeCardsUseDecimalInsteadOfColon preference is on, else
		///colon.</summary>
		public static EnumTimeCardTotalsFormat GetDefaultFormat() {
			Meth.NoCheckMiddleTierRole();
			if(PrefC.GetBool(PrefName.TimeCardsUseDecimalInsteadOfColon)) {
				return EnumTimeCardTotalsFormat.Decimal;
			}
			return EnumTimeCardTotalsFormat.Colon;
		}

		///<summary>Adds up one pay period the way the time card grid does. Pass the clock events (or breaks) and the time card adjustments of the
		///period, without the pay period note row. For breaks, pass isBreaks true and no adjustments. Events that are not finished yet (no
		///second time) add nothing to Total or Day.</summary>
		public static TimeCardPeriodTotals Calculate(List<ClockEvent> listClockEvents,List<TimeAdjust> listTimeAdjusts,bool isBreaks) {
			Meth.NoCheckMiddleTierRole();
			TimeCardPeriodTotals totals=new TimeCardPeriodTotals();
			TimeSpan timeSpanNotOverridden=TimeSpan.FromHours(-1);
			List<ClockEvent> listClockEventsAll=listClockEvents??new List<ClockEvent>();
			for(int i=0;i<listClockEventsAll.Count;i++) {
				ClockEvent clockEvent=listClockEventsAll[i];
				if(clockEvent.TimeDisplayed2.Year>=1880) {//Only completed clock events count.
					TimeSpan timeSpanOne=clockEvent.TimeDisplayed2-clockEvent.TimeDisplayed1;
					totals.Total+=timeSpanOne;
					totals.Regular+=timeSpanOne;
				}
				TimeSpan timeSpanAdjust=clockEvent.AdjustAuto;//typically zero
				if(clockEvent.AdjustIsOverridden) {
					timeSpanAdjust=clockEvent.Adjust;
				}
				totals.Adjust+=timeSpanAdjust;
				totals.Regular+=timeSpanAdjust;
				if(clockEvent.Rate2Hours!=timeSpanNotOverridden) {
					totals.Rate2+=clockEvent.Rate2Hours;
				}
				else {
					totals.Rate2+=clockEvent.Rate2Auto;
				}
				if(clockEvent.Rate3Hours!=timeSpanNotOverridden) {
					totals.Rate3+=clockEvent.Rate3Hours;
				}
				else {
					totals.Rate3+=clockEvent.Rate3Auto;
				}
				TimeSpan timeSpanOT=clockEvent.OTimeAuto;//typically zero
				if(clockEvent.OTimeHours!=timeSpanNotOverridden) {
					timeSpanOT=clockEvent.OTimeHours;
				}
				totals.OT+=timeSpanOT;
				totals.Regular-=timeSpanOT;
			}
			if(isBreaks || listTimeAdjusts==null) {
				return totals;//The breaks view has no adjustment rows.
			}
			for(int i=0;i<listTimeAdjusts.Count;i++) {
				TimeAdjust timeAdjust=listTimeAdjusts[i];
				if(timeAdjust.IsUnpaidProtectedLeave) {
					totals.ProtectedLeave+=timeAdjust.RegHours;
				}
				else if(timeAdjust.PtoDefNum==0) {
					totals.Adjust+=timeAdjust.RegHours;//might be negative
					totals.Regular+=timeAdjust.RegHours;
				}
				else {
					totals.PTO+=timeAdjust.PtoHours;
				}
				totals.OT+=timeAdjust.OTimeHours;
			}
			return totals;
		}

		///<summary>One total in the given format. Colon is H:mm, or H:mm:ss when showSeconds is true; decimal is hours with two decimals. Hours
		///can go past 24 and negative totals keep their sign. Unlike the time card grid's cells, a zero total is shown (0:00 or 0.00) rather
		///than left blank.</summary>
		public static string FormatTotal(TimeSpan timeSpan,EnumTimeCardTotalsFormat format,bool showSeconds) {
			Meth.NoCheckMiddleTierRole();
			if(format==EnumTimeCardTotalsFormat.Decimal) {
				return timeSpan.TotalHours.ToString("n");
			}
			if(timeSpan==TimeSpan.Zero) {
				if(showSeconds) {
					return "0:00:00";
				}
				return "0:00";
			}
			if(showSeconds) {
				return timeSpan.ToStringHmmss();
			}
			return timeSpan.ToStringHmm();
		}

		///<summary>The cells of the totals row, in the order of GetColumnCaptions.</summary>
		public static List<string> GetCells(TimeCardPeriodTotals totals,EnumTimeCardTotalsFormat format,bool showSeconds) {
			Meth.NoCheckMiddleTierRole();
			return GetValues(totals).Select(x => FormatTotal(x,format,showSeconds)).ToList();
		}

		///<summary>The totals in the order of GetColumnCaptions.</summary>
		public static List<TimeSpan> GetValues(TimeCardPeriodTotals totals) {
			Meth.NoCheckMiddleTierRole();
			return new List<TimeSpan> { totals.Total,totals.Adjust,totals.Rate2,totals.Rate3,totals.PTO,totals.OT,totals.ProtectedLeave,totals.Regular };
		}
	}
}
