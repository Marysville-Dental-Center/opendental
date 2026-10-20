// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using SyntheticData.Infrastructure;

namespace SyntheticData.Generation {
	///<summary>Working days, holidays, provider schedules (one per provider per working day, linked to the provider's operatory) and
	///lunch blockouts, from the start of history to the end of the future booking window.</summary>
	public static class ScheduleGenerator {
		public static void Run(World world) {
			Rng rng=Rng.Derive(world.Settings.Seed,"schedule");
			DateTime start=world.Settings.HistoryStart;
			DateTime end=world.Settings.FutureEnd;
			for(int year=start.Year;year<=end.Year;year++) {
				foreach(DateTime holiday in Holidays(year)) {
					world.SetHolidays.Add(holiday);
				}
			}
			long blockoutLunch=world.Def(OdValues.DefBlockoutTypes,"Lunch");
			long blockoutEmergency=world.Def(OdValues.DefBlockoutTypes,"Emergency Hold");
			long blockoutMeeting=world.Def(OdValues.DefBlockoutTypes,"Staff Meeting");
			for(DateTime date=start;date<=end;date=date.AddDays(1)) {
				if(date.DayOfWeek==DayOfWeek.Saturday || date.DayOfWeek==DayOfWeek.Sunday) {
					continue;
				}
				if(world.SetHolidays.Contains(date)) {
					world.Sink.Insert("schedule",new Row {
						["ScheduleNum"]=world.Sink.NextId("schedule"),["SchedDate"]=date,["StartTime"]=TimeSpan.Zero,["StopTime"]=TimeSpan.Zero,
						["SchedType"]=OdValues.SchedPractice,["Note"]="Office closed (holiday)",["Status"]=2,["ClinicNum"]=world.ClinicNum,
						["DateTStamp"]=date.AddDays(-30).AddHours(12),
					});
					continue;
				}
				world.ListWorkdays.Add(date);
				foreach(ProviderInfo provider in world.AllProviders) {
					long scheduleNum=world.Sink.NextId("schedule");
					world.Sink.Insert("schedule",new Row {
						["ScheduleNum"]=scheduleNum,["SchedDate"]=date,["StartTime"]=new TimeSpan(8,0,0),["StopTime"]=new TimeSpan(17,0,0),
						["SchedType"]=OdValues.SchedProvider,["ProvNum"]=provider.ProvNum,["Note"]="",["Status"]=0,["ClinicNum"]=world.ClinicNum,
						["DateTStamp"]=date.AddDays(-30).AddHours(12),
					});
					world.Sink.Insert("scheduleop",new Row { ["ScheduleOpNum"]=world.Sink.NextId("scheduleop"),["ScheduleNum"]=scheduleNum,["OperatoryNum"]=provider.OpNum });
					long lunchNum=world.Sink.NextId("schedule");
					world.Sink.Insert("schedule",new Row {
						["ScheduleNum"]=lunchNum,["SchedDate"]=date,["StartTime"]=new TimeSpan(12,0,0),["StopTime"]=new TimeSpan(13,0,0),
						["SchedType"]=OdValues.SchedBlockout,["BlockoutType"]=blockoutLunch,["Note"]="",["Status"]=0,["ClinicNum"]=world.ClinicNum,
						["DateTStamp"]=date.AddDays(-30).AddHours(12),
					});
					world.Sink.Insert("scheduleop",new Row { ["ScheduleOpNum"]=world.Sink.NextId("scheduleop"),["ScheduleNum"]=lunchNum,["OperatoryNum"]=provider.OpNum });
				}
				//An emergency hold now and then in a dentist's operatory, booked out of the slot book so appointments never overlap it.
				if(rng.Chance(0.15)) {
					ProviderInfo dentist=rng.Pick(world.ListDentists);
					DateTime? holdStart=world.SlotBook.TryBook(dentist.OpNum,date,6,rng.Pick(new[] { 0,72 }));
					if(holdStart.HasValue) {
						long holdNum=world.Sink.NextId("schedule");
						world.Sink.Insert("schedule",new Row {
							["ScheduleNum"]=holdNum,["SchedDate"]=date,["StartTime"]=holdStart.Value.TimeOfDay,["StopTime"]=holdStart.Value.TimeOfDay.Add(TimeSpan.FromMinutes(30)),
							["SchedType"]=OdValues.SchedBlockout,["BlockoutType"]=blockoutEmergency,["Note"]="Keep open for emergencies",["Status"]=0,
							["ClinicNum"]=world.ClinicNum,["DateTStamp"]=date.AddDays(-7).AddHours(16),
						});
						world.Sink.Insert("scheduleop",new Row { ["ScheduleOpNum"]=world.Sink.NextId("scheduleop"),["ScheduleNum"]=holdNum,["OperatoryNum"]=dentist.OpNum });
					}
				}
				//Monthly staff meeting on the first Monday, before patients arrive (outside bookable hours).
				if(date.DayOfWeek==DayOfWeek.Monday && date.Day<=7) {
					world.Sink.Insert("schedule",new Row {
						["ScheduleNum"]=world.Sink.NextId("schedule"),["SchedDate"]=date,["StartTime"]=new TimeSpan(7,15,0),["StopTime"]=new TimeSpan(7,55,0),
						["SchedType"]=OdValues.SchedBlockout,["BlockoutType"]=blockoutMeeting,["Note"]="Staff meeting",["Status"]=0,["ClinicNum"]=world.ClinicNum,
						["DateTStamp"]=date.AddDays(-14).AddHours(9),
					});
				}
			}
		}

		public static List<DateTime> Holidays(int year) {
			DateTime thanksgiving=NthWeekday(year,11,DayOfWeek.Thursday,4);
			return new List<DateTime> {
				new DateTime(year,1,1),
				LastWeekday(year,5,DayOfWeek.Monday),
				new DateTime(year,7,4),
				NthWeekday(year,9,DayOfWeek.Monday,1),
				thanksgiving,
				thanksgiving.AddDays(1),
				new DateTime(year,12,24),
				new DateTime(year,12,25),
			};
		}

		private static DateTime NthWeekday(int year,int month,DayOfWeek dayOfWeek,int n) {
			DateTime date=new DateTime(year,month,1);
			while(date.DayOfWeek!=dayOfWeek) {
				date=date.AddDays(1);
			}
			return date.AddDays(7*(n-1));
		}

		private static DateTime LastWeekday(int year,int month,DayOfWeek dayOfWeek) {
			DateTime date=new DateTime(year,month,1).AddMonths(1).AddDays(-1);
			while(date.DayOfWeek!=dayOfWeek) {
				date=date.AddDays(-1);
			}
			return date;
		}
	}
}
