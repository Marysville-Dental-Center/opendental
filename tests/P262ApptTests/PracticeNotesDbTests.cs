// Practice and clinic schedule notes in the Appointments module (Open Dental 26.2 Appointments item 5, port P262-APPT): which schedule rows the
// upper left corner of the operatory header shows (Schedules.GetPracticeNotesForApptModule) and the text of its hover tip
// (Schedules.GetPracticeNotesText), with clinics off and on. The schedule rows are built in memory; the database holds only the preferences and
// the made-up clinics the clinic cases need. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace P262ApptTests {
	[TestClass]
	public class PracticeNotesDbTests {
		private static readonly DateTime _date=new DateTime(2031,6,10);//a Tuesday

		[TestInitialize]
		public void TestInitialize() {
			ApptDb.Begin();
			SetClinicsOn(false);
		}

		[TestCleanup]
		public void TestCleanup() {
			if(ApptDb.SkipReason==null) {
				SetClinicsOn(false);
			}
		}

		private static void SetClinicsOn(bool isOn) {
			PrefT.UpdateBool(PrefName.EasyNoClinics,!isOn);
			Cache.Refresh(InvalidType.Prefs);
		}

		private static long _scheduleNum=1000000;

		private static Schedule NewSchedule(ScheduleType schedType,string note,SchedStatus status=SchedStatus.Open,long clinicNum=0,DateTime? date=null,
			long provNum=0)
		{
			_scheduleNum++;
			return new Schedule { ScheduleNum=_scheduleNum,SchedDate=date??_date,SchedType=schedType,Note=note,Status=status,ClinicNum=clinicNum,
				ProvNum=provNum,StartTime=TimeSpan.Zero,StopTime=TimeSpan.Zero };
		}

		private static long NewClinic(string abbr) {
			Clinic clinic=new Clinic { Description=abbr+" clinic",Abbr=abbr,Address="1 Synthetic Way",City="Testville",State="ZZ",Zip="00000" };
			Clinics.Insert(clinic);
			Clinics.RefreshCache();
			return clinic.ClinicNum;
		}

		[TestMethod]
		public void NoClinics_PracticeNotesAndHolidaysOnly() {
			Schedule scheduleNote=NewSchedule(ScheduleType.Practice,"Staff meeting at noon");
			Schedule scheduleHoliday=NewSchedule(ScheduleType.Practice,"",SchedStatus.Holiday);
			Schedule scheduleEmpty=NewSchedule(ScheduleType.Practice,"  ");
			Schedule scheduleProvNote=NewSchedule(ScheduleType.Provider,"Provider note",provNum:1);
			Schedule scheduleBlockout=NewSchedule(ScheduleType.Blockout,"Blockout note");
			Schedule scheduleClinic=NewSchedule(ScheduleType.Practice,"Clinic note",clinicNum:77);
			List<Schedule> listNotes=Schedules.GetPracticeNotesForApptModule(new List<Schedule> { scheduleNote,scheduleHoliday,scheduleEmpty,
				scheduleProvNote,scheduleBlockout,scheduleClinic },0);
			CollectionAssert.AreEqual(new[] { scheduleHoliday.ScheduleNum,scheduleNote.ScheduleNum },listNotes.Select(x => x.ScheduleNum).ToArray(),
				"Holidays first; a note without text, provider and blockout notes and (clinics off) a clinic's note are left out.");
			Assert.AreEqual("Holiday\r\nNote: Staff meeting at noon",Schedules.GetPracticeNotesText(listNotes,false));
			Assert.AreEqual(0,Schedules.GetPracticeNotesForApptModule(null,0).Count);
			Assert.AreEqual("",Schedules.GetPracticeNotesText(new List<Schedule>(),false));
		}

		[TestMethod]
		public void WeekView_EachLineStartsWithItsDate_InDateOrder() {
			DateTime dateWed=_date.AddDays(1);
			Schedule scheduleWed=NewSchedule(ScheduleType.Practice,"Inventory count",date:dateWed);
			Schedule scheduleTue=NewSchedule(ScheduleType.Practice,"Closed for the holiday",SchedStatus.Holiday);
			List<Schedule> listNotes=Schedules.GetPracticeNotesForApptModule(new List<Schedule> { scheduleWed,scheduleTue },0);
			string text=Schedules.GetPracticeNotesText(listNotes,true);
			Assert.AreEqual(_date.ToString("ddd")+" "+_date.ToShortDateString()+" Holiday: Closed for the holiday\r\n"
				+dateWed.ToString("ddd")+" "+dateWed.ToShortDateString()+" Note: Inventory count",text);
		}

		[TestMethod]
		public void Clinics_PracticeAndTheSelectedClinic_WithTheirAbbreviations() {
			long clinicNumNorth=NewClinic("P262North");
			long clinicNumSouth=NewClinic("P262South");
			SetClinicsOn(true);
			Assert.IsTrue(PrefC.HasClinicsEnabled);
			Schedule schedulePractice=NewSchedule(ScheduleType.Practice,"All clinics: fire drill");
			Schedule scheduleNorth=NewSchedule(ScheduleType.Practice,"North: new hours",clinicNum:clinicNumNorth);
			Schedule scheduleNorthHoliday=NewSchedule(ScheduleType.Practice,"",SchedStatus.Holiday,clinicNum:clinicNumNorth);
			Schedule scheduleSouth=NewSchedule(ScheduleType.Practice,"South: closed early",clinicNum:clinicNumSouth);
			List<Schedule> listAll=new List<Schedule> { schedulePractice,scheduleNorth,scheduleNorthHoliday,scheduleSouth };
			List<Schedule> listNorth=Schedules.GetPracticeNotesForApptModule(listAll,clinicNumNorth);
			CollectionAssert.AreEqual(new[] { scheduleNorthHoliday.ScheduleNum,schedulePractice.ScheduleNum,scheduleNorth.ScheduleNum },
				listNorth.Select(x => x.ScheduleNum).ToArray(),"Holidays first, then the practice's notes before the clinic's.");
			Assert.AreEqual("Holiday (P262North)\r\nNote (Headquarters): All clinics: fire drill\r\nNote (P262North): North: new hours",
				Schedules.GetPracticeNotesText(listNorth,false));
			//Headquarters selected: the practice's notes only.
			List<Schedule> listHq=Schedules.GetPracticeNotesForApptModule(listAll,0);
			CollectionAssert.AreEqual(new[] { schedulePractice.ScheduleNum },listHq.Select(x => x.ScheduleNum).ToArray());
		}
	}
}
