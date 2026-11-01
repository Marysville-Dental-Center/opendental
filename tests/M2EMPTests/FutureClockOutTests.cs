// Rules for clock-outs dated in the future (Open Dental 26.1.63, feature v26_1-manage-timecard-future-clockout, package M2-EMP). No database:
// these tests call the rule methods with fixed dates.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

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
		public void GetSecondTimePrintText_TimeFirstThenTheDateOfALaterDay() {
			ClockEvent clockEvent=new ClockEvent { ClockStatus=TimeClockStatus.Home,TimeDisplayed1=_dateToday.AddHours(8),
				TimeDisplayed2=_dateToday.AddHours(17) };
			//Same day: the time alone, as the grid and 24.3 print it.
			Assert.AreEqual(clockEvent.TimeDisplayed2.ToShortTimeString(),ClockEvents.GetSecondTimePrintText(clockEvent,showSeconds:false));
			Assert.AreEqual(clockEvent.TimeDisplayed2.ToLongTimeString(),ClockEvents.GetSecondTimePrintText(clockEvent,showSeconds:true));
			//A later day, an overnight shift or a clock-out entered ahead: the time, then the date.
			clockEvent.TimeDisplayed2=_dateToday.AddDays(1).AddHours(6);
			Assert.AreEqual(clockEvent.TimeDisplayed2.ToShortTimeString()+" "+clockEvent.TimeDisplayed2.ToShortDateString(),
				ClockEvents.GetSecondTimePrintText(clockEvent,showSeconds:false));
			Assert.AreEqual(clockEvent.TimeDisplayed2.ToLongTimeString()+" "+clockEvent.TimeDisplayed2.ToShortDateString(),
				ClockEvents.GetSecondTimePrintText(clockEvent,showSeconds:true));
			clockEvent.TimeDisplayed2=DateTime.MinValue;
			Assert.AreEqual("",ClockEvents.GetSecondTimePrintText(clockEvent,showSeconds:false));
		}

		///<summary>A stand-in for the print font: five units a character.</summary>
		private static float MeasureFiveUnitsAChar(string text) {
			return text.Length*5f;
		}

		[TestMethod]
		public void GetSecondTimePrintWidthMoved_NothingWithoutALaterDate() {
			List<ClockEvent> listClockEvents=new List<ClockEvent> {
				new ClockEvent { TimeDisplayed1=_dateToday.AddHours(8),TimeDisplayed2=_dateToday.AddHours(17) },
				new ClockEvent { TimeDisplayed1=_dateToday.AddDays(1).AddHours(8),TimeDisplayed2=DateTime.MinValue },
			};
			//The printout of 24.3 stays as it was, even where a long time would not fit (seconds on).
			Assert.AreEqual(0,ClockEvents.GetSecondTimePrintWidthMoved(listClockEvents,true,x => 500f,60,130));
			Assert.AreEqual(0,ClockEvents.GetSecondTimePrintWidthMoved(new List<ClockEvent>(),false,MeasureFiveUnitsAChar,60,130));
			Assert.AreEqual(0,ClockEvents.GetSecondTimePrintWidthMoved(null,false,MeasureFiveUnitsAChar,60,130));
		}

		[TestMethod]
		public void GetSecondTimePrintWidthMoved_TakesWhatTheWidestTextNeedsFromTheNote() {
			ClockEvent clockEventLater=new ClockEvent { TimeDisplayed1=_dateToday.AddHours(20),TimeDisplayed2=_dateToday.AddDays(1).AddHours(6) };
			List<ClockEvent> listClockEvents=new List<ClockEvent> {
				new ClockEvent { TimeDisplayed1=_dateToday.AddHours(8),TimeDisplayed2=_dateToday.AddHours(17) },
				clockEventLater,
			};
			string text=ClockEvents.GetSecondTimePrintText(clockEventLater,false);
			int widthNeeded=text.Length*5+ClockEvents.PRINT_CELL_PADDING+1;
			Assert.AreEqual(widthNeeded-60,ClockEvents.GetSecondTimePrintWidthMoved(listClockEvents,false,MeasureFiveUnitsAChar,60,130));
			//Seconds make the text longer, and the column takes more.
			string textSeconds=ClockEvents.GetSecondTimePrintText(clockEventLater,true);
			Assert.AreEqual(textSeconds.Length*5+ClockEvents.PRINT_CELL_PADDING+1-60,
				ClockEvents.GetSecondTimePrintWidthMoved(listClockEvents,true,MeasureFiveUnitsAChar,60,200));
			//A fraction of a unit rounds up.
			Assert.AreEqual(100+ClockEvents.PRINT_CELL_PADDING+1-60,ClockEvents.GetSecondTimePrintWidthMoved(listClockEvents,false,x => 99.2f,60,200));
			//A column already wide enough takes nothing, and never gives width to the Note.
			Assert.AreEqual(0,ClockEvents.GetSecondTimePrintWidthMoved(listClockEvents,false,MeasureFiveUnitsAChar,widthNeeded,130));
			Assert.AreEqual(0,ClockEvents.GetSecondTimePrintWidthMoved(listClockEvents,false,MeasureFiveUnitsAChar,widthNeeded+40,130));
		}

		[TestMethod]
		public void GetSecondTimePrintWidthMoved_NoteKeepsItsMinimum() {
			List<ClockEvent> listClockEvents=new List<ClockEvent> {
				new ClockEvent { TimeDisplayed1=_dateToday.AddHours(20),TimeDisplayed2=_dateToday.AddDays(1).AddHours(6) },
			};
			Assert.AreEqual(80-ClockEvents.PRINT_WIDTH_NOTE_MIN,ClockEvents.GetSecondTimePrintWidthMoved(listClockEvents,false,x => 500f,60,80));
			Assert.AreEqual(0,ClockEvents.GetSecondTimePrintWidthMoved(listClockEvents,false,x => 500f,60,ClockEvents.PRINT_WIDTH_NOTE_MIN));
			Assert.AreEqual(0,ClockEvents.GetSecondTimePrintWidthMoved(listClockEvents,false,x => 500f,60,ClockEvents.PRINT_WIDTH_NOTE_MIN-10),
				"A Note column already narrower than the minimum gives nothing and takes nothing.");
		}

		///<summary>The printed time card with the real print font, in printer units (hundredths of an inch), on en-US: the layout of pd_PrintPage
		///(Out/In 60 units; Note 130, or 80 with clinics), each cell drawn one line high in a box 5 units narrower than its column.</summary>
		[TestMethod]
		public void PrintedTimeCard_KeepsTheClockOutTimeOnItsLine() {
			CultureInfo cultureOld=Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture=CultureInfo.GetCultureInfo("en-US");
			try {
				using(Bitmap bitmap=new Bitmap(10,10))
				using(Graphics g=Graphics.FromImage(bitmap))
				using(Font font=new Font(FontFamily.GenericSansSerif,8)) {
					g.PageUnit=GraphicsUnit.Inch;
					g.PageScale=0.01f;
					List<ClockEvent> listClockEvents=new List<ClockEvent> {
						new ClockEvent { TimeDisplayed1=new DateTime(2026,10,25,8,0,0),TimeDisplayed2=new DateTime(2026,10,25,17,0,0) },
						new ClockEvent { TimeDisplayed1=new DateTime(2026,10,25,22,0,0),TimeDisplayed2=new DateTime(2026,10,26,6,0,0) },
						new ClockEvent { TimeDisplayed1=new DateTime(2026,12,30,22,0,0),TimeDisplayed2=new DateTime(2026,12,31,23,59,59) },
					};
					ClockEvent clockEventWidest=listClockEvents[2];
					//The bug: the screen's text ("12/31/2026 11:59 PM") in the 24.3 column leaves only the date on the printed line.
					string textScreen=ClockEvents.GetSecondTimeCellText(clockEventWidest,false);
					Assert.IsFalse(textScreen.Substring(0,CharsOnFirstLine(g,font,textScreen,60)).Contains(clockEventWidest.TimeDisplayed2.ToShortTimeString()));
					foreach(bool showSeconds in new[] { false,true }) {
						foreach(int widthNote in new[] { 130,80 }) {
							int widthMoved=ClockEvents.GetSecondTimePrintWidthMoved(listClockEvents,showSeconds,x => g.MeasureString(x,font).Width,60,widthNote);
							Assert.IsTrue(widthMoved>0);
							Assert.IsTrue(widthNote-widthMoved>=ClockEvents.PRINT_WIDTH_NOTE_MIN);
							foreach(ClockEvent clockEvent in listClockEvents) {
								string text=ClockEvents.GetSecondTimePrintText(clockEvent,showSeconds);
								string time=showSeconds ? clockEvent.TimeDisplayed2.ToLongTimeString() : clockEvent.TimeDisplayed2.ToShortTimeString();
								int charsOnLine=CharsOnFirstLine(g,font,text,60+widthMoved);
								string message="Seconds "+showSeconds+", Note "+widthNote+": "+text;
								//The time always prints in full.
								Assert.IsTrue(charsOnLine>=time.Length,message);
								if(widthNote==130) {
									//Without clinics the Note column has room to give for the whole text, date and all.
									Assert.AreEqual(text.Length,charsOnLine,message);
								}
							}
						}
					}
				}
			}
			finally {
				Thread.CurrentThread.CurrentCulture=cultureOld;
			}
		}

		///<summary>How many characters of text pd_PrintPage shows in a column this wide: the ones on the first line of its box.</summary>
		private static int CharsOnFirstLine(Graphics g,Font font,string text,int widthColumn) {
			using(StringFormat stringFormat=new StringFormat(StringFormatFlags.LineLimit)) {
				SizeF sizeBox=new SizeF(widthColumn-ClockEvents.PRINT_CELL_PADDING,font.GetHeight(g)*1.5f);//Room for one whole line only.
				g.MeasureString(text,font,sizeBox,stringFormat,out int charsFitted,out int linesFilled);
				return text.Substring(0,charsFitted).TrimEnd().Length;
			}
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
