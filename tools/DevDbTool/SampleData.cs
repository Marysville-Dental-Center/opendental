// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using OpenDentBusiness;

namespace DevDbTool {
	///<summary>Adds a small, entirely fictional practice on top of the seed data: two providers, two operatories, six patients in four
	///families, provider schedules for the next two weeks and a few appointments. Phone numbers use the 555-01xx range reserved for
	///fiction, e-mail addresses use example.com, and no SSNs, insurance or procedures are created.</summary>
	public static class SampleData {
		private const string AppointmentNote="Sample appointment (fictional dev data).";

		public static void Insert() {
			Program.Info("Adding fictional sample data");
			//Patients and appointments record who entered them.
			Security.CurUser=Userods.GetUserByNameNoCache("Admin")??new Userod();
			//Copy fee schedule and specialty from the seed providers (DOC and HYG) so the new ones look like a normal setup.
			Providers.RefreshCache();
			Provider provSeedDentist=Providers.GetProv(1);
			Provider provSeedHygienist=Providers.GetProv(2);
			long feeSchedNum=provSeedDentist?.FeeSched??0;
			Provider provDentist=new Provider {
				Abbr="DDEV",
				FName="Dana",
				LName="Devlin",
				Suffix="DDS",
				ItemOrder=NextItemOrder("provider"),
				FeeSched=feeSchedNum,
				Specialty=provSeedDentist?.Specialty??0,
				IsSecondary=false,
				ProvColor=Color.FromArgb(178,214,255),
				OutlineColor=Color.FromArgb(64,64,64),
				ProvStatus=ProviderStatus.Active,
			};
			Providers.Insert(provDentist);
			Provider provHygienist=new Provider {
				Abbr="RHYG",
				FName="Riley",
				LName="Hart",
				Suffix="RDH",
				ItemOrder=provDentist.ItemOrder+1,
				FeeSched=feeSchedNum,
				Specialty=provSeedHygienist?.Specialty??0,
				IsSecondary=true,
				ProvColor=Color.FromArgb(196,240,196),
				OutlineColor=Color.FromArgb(64,64,64),
				ProvStatus=ProviderStatus.Active,
			};
			Providers.Insert(provHygienist);
			Providers.RefreshCache();
			Operatory opDentist=new Operatory {
				OpName="Dev Op A",
				Abbrev="DEVA",
				ItemOrder=NextItemOrder("operatory"),
				ProvDentist=provDentist.ProvNum,
			};
			Operatories.Insert(opDentist);
			Operatory opHygiene=new Operatory {
				OpName="Dev Hygiene B",
				Abbrev="DEVB",
				ItemOrder=opDentist.ItemOrder+1,
				ProvDentist=provDentist.ProvNum,
				ProvHygienist=provHygienist.ProvNum,
				IsHygiene=true,
			};
			Operatories.Insert(opHygiene);
			Operatories.RefreshCache();
			//Families. The first patient of each family is the guarantor.
			Patient patAlex=InsertPatient("Sampleton","Alex","",new DateTime(1984,3,12),PatientGender.Female,PatientPosition.Married,
				"101 Example Way","555-0101","alex.sampleton@example.com",provDentist,provHygienist,guarantor:null);
			Patient patJamie=InsertPatient("Sampleton","Jamie","",new DateTime(1985,7,4),PatientGender.Male,PatientPosition.Married,
				"101 Example Way","555-0102","jamie.sampleton@example.com",provDentist,provHygienist,guarantor:patAlex);
			Patient patSam=InsertPatient("Sampleton","Samuel","Sam",new DateTime(2015,9,20),PatientGender.Male,PatientPosition.Child,
				"101 Example Way","555-0101","",provDentist,provHygienist,guarantor:patAlex);
			Patient patMorgan=InsertPatient("Placeholder","Morgan","",new DateTime(1972,11,2),PatientGender.Unknown,PatientPosition.Single,
				"22 Fixture Lane","555-0110","morgan.placeholder@example.com",provDentist,provHygienist,guarantor:null);
			Patient patCasey=InsertPatient("Fixture","Casey","",new DateTime(1999,1,15),PatientGender.Female,PatientPosition.Single,
				"7 Mock Street","555-0120","casey.fixture@example.com",provDentist,provHygienist,guarantor:null);
			Patient patRobin=InsertPatient("Mockford","Robin","",new DateTime(1958,5,30),PatientGender.Male,PatientPosition.Widowed,
				"300 Stub Road","555-0130","",provDentist,provHygienist,guarantor:null);
			//Two weeks of provider time from today, Monday to Friday, 8:00 to 17:00, in each provider's operatory.
			DateTime dateFirst=NextWeekday(DateTime.Today);
			List<Schedule> listSchedules=new List<Schedule>();
			for(DateTime date=dateFirst;date<dateFirst.AddDays(14);date=date.AddDays(1)) {
				if(date.DayOfWeek==DayOfWeek.Saturday || date.DayOfWeek==DayOfWeek.Sunday) {
					continue;
				}
				listSchedules.Add(NewProviderSchedule(date,provDentist.ProvNum,opDentist.OperatoryNum));
				listSchedules.Add(NewProviderSchedule(date,provHygienist.ProvNum,opHygiene.OperatoryNum));
			}
			Schedules.Insert(false,true,listSchedules);
			//Appointments on the first two working days.
			DateTime dateSecond=NextWeekday(dateFirst.AddDays(1));
			InsertAppointment(patAlex,dateFirst.AddHours(9),opDentist,provDentist,provHygienist,"//XXXXXX//",isHygiene:false);
			InsertAppointment(patJamie,dateFirst.AddHours(9),opHygiene,provDentist,provHygienist,"//XXXXXXXX",isHygiene:true);
			InsertAppointment(patSam,dateFirst.AddHours(10),opHygiene,provDentist,provHygienist,"/XXXXX/",isHygiene:true);
			InsertAppointment(patMorgan,dateFirst.AddHours(13),opDentist,provDentist,provHygienist,"//XXXXXXXXXX//",isHygiene:false);
			InsertAppointment(patCasey,dateSecond.AddHours(8),opHygiene,provDentist,provHygienist,"//XXXXXXXX",isHygiene:true);
			InsertAppointment(patRobin,dateSecond.AddHours(10).AddMinutes(30),opDentist,provDentist,provHygienist,"//XXXX//",
				isHygiene:false);
			Program.Info("Sample data: 2 providers, 2 operatories, 6 patients, "+listSchedules.Count+" provider schedules, 6 appointments "
				+"("+dateFirst.ToString("yyyy-MM-dd")+" and "+dateSecond.ToString("yyyy-MM-dd")+")");
		}

		private static Patient InsertPatient(string lName,string fName,string preferred,DateTime birthdate,PatientGender gender,
			PatientPosition position,string address,string phone,string email,Provider provPri,Provider provSec,Patient guarantor)
		{
			Patient patient=new Patient {
				LName=lName,
				FName=fName,
				Preferred=preferred,
				Birthdate=birthdate,
				Gender=gender,
				Position=position,
				PatStatus=PatientStatus.Patient,
				Address=address,
				City="Springfield",
				State="OR",
				Zip="97000",
				HmPhone=phone,
				WirelessPhone=phone,
				Email=email,
				PriProv=provPri.ProvNum,
				SecProv=provSec.ProvNum,
				BillingType=PrefC.GetLong(PrefName.PracticeDefaultBillType),
				PreferConfirmMethod=ContactMethod.HmPhone,
				PreferContactMethod=ContactMethod.HmPhone,
				PreferRecallMethod=ContactMethod.HmPhone,
				DateFirstVisit=DateTime.Today,
			};
			Patients.Insert(patient,false);
			Patient patientOld=patient.Copy();
			patient.Guarantor=guarantor?.PatNum??patient.PatNum;
			Patients.Update(patient,patientOld);
			return patient;
		}

		private static Schedule NewProviderSchedule(DateTime date,long provNum,long opNum) {
			return new Schedule {
				SchedDate=date,
				StartTime=TimeSpan.FromHours(8),
				StopTime=TimeSpan.FromHours(17),
				SchedType=ScheduleType.Provider,
				ProvNum=provNum,
				Status=SchedStatus.Open,
				Note="",
				Ops=new List<long> { opNum },
			};
		}

		///<summary>Each character of pattern is 5 minutes; X is provider time, / is assistant-only time.</summary>
		private static void InsertAppointment(Patient patient,DateTime dateTime,Operatory op,Provider provDentist,Provider provHygienist,
			string pattern,bool isHygiene)
		{
			Appointment appointment=new Appointment {
				PatNum=patient.PatNum,
				AptStatus=ApptStatus.Scheduled,
				AptDateTime=dateTime,
				Op=op.OperatoryNum,
				ProvNum=provDentist.ProvNum,
				ProvHyg=isHygiene ? provHygienist.ProvNum : 0,
				IsHygiene=isHygiene,
				Pattern=pattern,
				Note=AppointmentNote,
				Priority=ApptPriority.Normal,
			};
			Appointments.Insert(appointment);
		}

		private static DateTime NextWeekday(DateTime date) {
			while(date.DayOfWeek==DayOfWeek.Saturday || date.DayOfWeek==DayOfWeek.Sunday) {
				date=date.AddDays(1);
			}
			return date.Date;
		}

		private static int NextItemOrder(string table) {
			return PIn.Int(DataCore.GetScalar("SELECT COALESCE(MAX(ItemOrder)+1,0) FROM "+table));
		}
	}
}
