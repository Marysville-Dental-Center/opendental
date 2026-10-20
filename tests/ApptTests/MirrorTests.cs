// Unit tests for the appointment mirror calculation (Open Dental 25.3 appointment mirroring, backlog item v25_3-appts-mirroring): which provider is
// secondary, which operatory the mirror goes to, and the span from PatternSecondary, including split X runs. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ApptTests {
	[TestClass]
	public class MirrorTests {
		private static readonly DateTime _date=new DateTime(2026,10,20);
		//Made-up providers and operatories. Provider 1 is a dentist, provider 2 a hygienist.
		private const long PROV_DENT=1;
		private const long PROV_HYG=2;
		private const long OP_DOC=10;
		private const long OP_HYG_B=11;
		private const long OP_HYG_A=12;
		private const long OP_SPARE=13;

		private static List<Operatory> GetOps() {
			return new List<Operatory> {
				new Operatory { OperatoryNum=OP_DOC,ItemOrder=2,ProvDentist=PROV_DENT },
				new Operatory { OperatoryNum=OP_HYG_B,ItemOrder=1,ProvHygienist=PROV_HYG },
				new Operatory { OperatoryNum=OP_HYG_A,ItemOrder=0,ProvHygienist=PROV_HYG },
				new Operatory { OperatoryNum=OP_SPARE,ItemOrder=3 },
			};
		}

		private static Schedule ProvSched(long provNum,DateTime date,int hourStart,int hourStop,params long[] arrayOpNums) {
			return new Schedule { SchedType=ScheduleType.Provider,ProvNum=provNum,SchedDate=date,StartTime=TimeSpan.FromHours(hourStart),
				StopTime=TimeSpan.FromHours(hourStop),Ops=arrayOpNums.ToList() };
		}

		///<summary>A dentist appointment at 9:00 in the doctor's op with the hygienist's time in two runs: 9:00-9:10 and 9:20-9:30.</summary>
		private static Appointment GetAppt() {
			return new Appointment { AptNum=5,AptDateTime=_date.AddHours(9),AptStatus=ApptStatus.Scheduled,Op=OP_DOC,IsHygiene=false,
				ProvNum=PROV_DENT,ProvHyg=PROV_HYG,Pattern="XXXXXX",PatternSecondary="XX//XX",IsMirrored=true };
		}

		#region Secondary provider
		[TestMethod]
		public void SecondaryProv_HygienistForDentistAppointment() {
			Assert.AreEqual(PROV_HYG,Appointments.GetSecondaryProvNum(false,PROV_DENT,PROV_HYG));
		}

		[TestMethod]
		public void SecondaryProv_DentistForHygieneAppointment() {
			Assert.AreEqual(PROV_DENT,Appointments.GetSecondaryProvNum(true,PROV_DENT,PROV_HYG));
		}

		[TestMethod]
		public void SecondaryProv_NoneWhenMissing() {
			Assert.AreEqual(0,Appointments.GetSecondaryProvNum(false,PROV_DENT,0));
			Assert.AreEqual(0,Appointments.GetSecondaryProvNum(true,0,PROV_HYG));
		}
		#endregion Secondary provider

		#region Runs from PatternSecondary
		private static string Runs(string pattern) {
			return string.Join(";",Appointments.GetMirrorRuns(pattern).Select(x => x.ToString()));
		}

		[TestMethod]
		public void Runs_EmptyOrNoX() {
			Assert.AreEqual("",Runs(null));
			Assert.AreEqual("",Runs(""));
			Assert.AreEqual("",Runs("//////"));
		}

		[TestMethod]
		public void Runs_SingleRunInMiddle() {
			Assert.AreEqual("10+10",Runs("//XX//"));
		}

		[TestMethod]
		public void Runs_SplitRunsGiveOneBlockEach() {
			Assert.AreEqual("0+10;20+15",Runs("XX//XXX"));
			Assert.AreEqual("5+5;15+5;25+5",Runs("/X/X/X"));
		}

		[TestMethod]
		public void Runs_RunAtEndAndLowerCase() {
			Assert.AreEqual("5+5",Runs("/X"));
			Assert.AreEqual("0+10",Runs("xX"));
			Assert.AreEqual("0+30",Runs("XXXXXX"));
		}
		#endregion Runs from PatternSecondary

		#region Statuses
		[TestMethod]
		public void Status_OnlyStatusesOnTheScheduleMirror() {
			Assert.IsTrue(Appointments.IsMirrorableStatus(ApptStatus.Scheduled));
			Assert.IsTrue(Appointments.IsMirrorableStatus(ApptStatus.Complete));
			Assert.IsTrue(Appointments.IsMirrorableStatus(ApptStatus.Broken));
			Assert.IsFalse(Appointments.IsMirrorableStatus(ApptStatus.Planned));
			Assert.IsFalse(Appointments.IsMirrorableStatus(ApptStatus.UnschedList));
			Assert.IsFalse(Appointments.IsMirrorableStatus(ApptStatus.PtNote));
			Assert.IsFalse(Appointments.IsMirrorableStatus(ApptStatus.PtNoteCompleted));
			Assert.IsFalse(Appointments.IsMirrorableStatus(ApptStatus.None));
		}
		#endregion Statuses

		#region Secondary operatory
		private static long MirrorOp(long provNum,bool isSecondaryDentist,int hourStart,List<Schedule> listSchedules,long opNumOriginal=OP_DOC,
			List<Operatory> listOps=null)
		{
			DateTime dateTimeStart=_date.AddHours(hourStart);
			return Appointments.GetMirrorOpNum(provNum,isSecondaryDentist,dateTimeStart,dateTimeStart.AddMinutes(20),opNumOriginal,listOps??GetOps(),listSchedules);
		}

		[TestMethod]
		public void Op_DefaultHygienistOp_LowestItemOrder() {
			//Two ops have the hygienist as their default hygienist; ItemOrder 0 wins over 1.
			Assert.AreEqual(OP_HYG_A,MirrorOp(PROV_HYG,false,9,new List<Schedule>()));
			Assert.AreEqual(OP_HYG_A,MirrorOp(PROV_HYG,false,9,null));
		}

		[TestMethod]
		public void Op_DefaultDentistOpForHygieneAppointment() {
			Assert.AreEqual(OP_DOC,MirrorOp(PROV_DENT,true,9,null,opNumOriginal:OP_HYG_A));
		}

		[TestMethod]
		public void Op_DefaultDentistMatchesOnlyProvDentist() {
			//The hygienist is secondary, so a hygienist's default-dentist op must not match.
			List<Operatory> listOps=new List<Operatory> { new Operatory { OperatoryNum=20,ItemOrder=0,ProvDentist=PROV_HYG } };
			Assert.AreEqual(0,MirrorOp(PROV_HYG,false,9,null,listOps:listOps));
		}

		[TestMethod]
		public void Op_ScheduledOpWinsOverDefault() {
			List<Schedule> listSchedules=new List<Schedule> { ProvSched(PROV_HYG,_date,8,17,OP_SPARE) };
			Assert.AreEqual(OP_SPARE,MirrorOp(PROV_HYG,false,9,listSchedules));
		}

		[TestMethod]
		public void Op_ScheduleOnAnotherDayIgnored() {
			List<Schedule> listSchedules=new List<Schedule> { ProvSched(PROV_HYG,_date.AddDays(1),8,17,OP_SPARE) };
			Assert.AreEqual(OP_HYG_A,MirrorOp(PROV_HYG,false,9,listSchedules));
		}

		[TestMethod]
		public void Op_ScheduleBlockOverlappingTheMirrorWins() {
			List<Schedule> listSchedules=new List<Schedule> { ProvSched(PROV_HYG,_date,8,12,OP_HYG_B),ProvSched(PROV_HYG,_date,13,17,OP_SPARE) };
			Assert.AreEqual(OP_SPARE,MirrorOp(PROV_HYG,false,14,listSchedules));
			Assert.AreEqual(OP_HYG_B,MirrorOp(PROV_HYG,false,9,listSchedules));
		}

		[TestMethod]
		public void Op_NoOverlappingBlock_LowestItemOrderOfTheDay() {
			List<Schedule> listSchedules=new List<Schedule> { ProvSched(PROV_HYG,_date,8,12,OP_SPARE),ProvSched(PROV_HYG,_date,13,17,OP_HYG_B) };
			//7:00 overlaps neither block: both ops are candidates and OP_HYG_B (ItemOrder 1) beats OP_SPARE (ItemOrder 3).
			Assert.AreEqual(OP_HYG_B,MirrorOp(PROV_HYG,false,7,listSchedules));
		}

		[TestMethod]
		public void Op_OtherProvidersAndScheduleTypesIgnored() {
			List<Schedule> listSchedules=new List<Schedule> {
				ProvSched(PROV_DENT,_date,8,17,OP_SPARE),
				new Schedule { SchedType=ScheduleType.Blockout,SchedDate=_date,StartTime=TimeSpan.FromHours(8),StopTime=TimeSpan.FromHours(17),
					Ops=new List<long> { OP_SPARE } },
			};
			Assert.AreEqual(OP_HYG_A,MirrorOp(PROV_HYG,false,9,listSchedules));
		}

		[TestMethod]
		public void Op_OnlyVisibleOpsCount() {
			//Scheduled into an op that the view does not show: fall back to the default op.
			List<Schedule> listSchedules=new List<Schedule> { ProvSched(PROV_HYG,_date,8,17,99) };
			Assert.AreEqual(OP_HYG_A,MirrorOp(PROV_HYG,false,9,listSchedules));
			List<Operatory> listOps=GetOps().FindAll(x => x.OperatoryNum!=OP_HYG_A);
			Assert.AreEqual(OP_HYG_B,MirrorOp(PROV_HYG,false,9,null,listOps:listOps));
		}

		[TestMethod]
		public void Op_OwnOpMeansNoMirror() {
			Assert.AreEqual(0,MirrorOp(PROV_HYG,false,9,null,opNumOriginal:OP_HYG_A));
			List<Schedule> listSchedules=new List<Schedule> { ProvSched(PROV_HYG,_date,8,17,OP_DOC) };
			Assert.AreEqual(0,MirrorOp(PROV_HYG,false,9,listSchedules,opNumOriginal:OP_DOC));
		}

		[TestMethod]
		public void Op_NoProviderOrNoOps() {
			Assert.AreEqual(0,MirrorOp(0,false,9,null));
			Assert.AreEqual(0,MirrorOp(PROV_HYG,false,9,null,listOps:new List<Operatory>()));
			Assert.AreEqual(0,MirrorOp(77,false,9,null));//nobody's op
		}
		#endregion Secondary operatory

		#region Mirrors
		[TestMethod]
		public void Mirrors_SplitRunsGiveTwoBlocksInHygienistOp() {
			List<ApptMirror> listMirrors=Appointments.GetMirrors(GetAppt(),true,GetOps(),new List<Schedule>());
			Assert.AreEqual(2,listMirrors.Count);
			Assert.IsTrue(listMirrors.All(x => x.AptNum==5 && x.OpNum==OP_HYG_A && x.ProvNumSecondary==PROV_HYG));
			Assert.AreEqual(_date.AddHours(9),listMirrors[0].DateTimeStart);
			Assert.AreEqual(10,listMirrors[0].MinutesLength);
			Assert.AreEqual(_date.AddHours(9).AddMinutes(10),listMirrors[0].DateTimeStop);
			Assert.AreEqual(_date.AddHours(9).AddMinutes(20),listMirrors[1].DateTimeStart);
			Assert.AreEqual(10,listMirrors[1].MinutesLength);
		}

		[TestMethod]
		public void Mirrors_HygieneFlagSwapsTheSecondaryProvider() {
			Appointment appointment=GetAppt();
			appointment.IsHygiene=true;
			appointment.Op=OP_HYG_A;
			appointment.PatternSecondary="//XX//";//the dentist's exam in the middle of the cleaning
			List<ApptMirror> listMirrors=Appointments.GetMirrors(appointment,true,GetOps(),null);
			Assert.AreEqual(1,listMirrors.Count);
			Assert.AreEqual(OP_DOC,listMirrors[0].OpNum);
			Assert.AreEqual(PROV_DENT,listMirrors[0].ProvNumSecondary);
			Assert.AreEqual(_date.AddHours(9).AddMinutes(10),listMirrors[0].DateTimeStart);
			Assert.AreEqual(10,listMirrors[0].MinutesLength);
		}

		[TestMethod]
		public void Mirrors_CompleteAndBrokenStillMirror() {
			foreach(ApptStatus apptStatus in new[] { ApptStatus.Complete,ApptStatus.Broken }) {
				Appointment appointment=GetAppt();
				appointment.AptStatus=apptStatus;
				Assert.AreEqual(2,Appointments.GetMirrors(appointment,true,GetOps(),null).Count,apptStatus.ToString());
			}
		}

		[TestMethod]
		public void Mirrors_NoneWhenAnyConditionFails() {
			Assert.AreEqual(0,Appointments.GetMirrors(null,true,GetOps(),null).Count);
			Assert.AreEqual(0,Appointments.GetMirrors(GetAppt(),false,GetOps(),null).Count,"View does not show mirrors.");
			Appointment appointment=GetAppt();
			appointment.IsMirrored=false;
			Assert.AreEqual(0,Appointments.GetMirrors(appointment,true,GetOps(),null).Count,"Not mirrored.");
			foreach(ApptStatus apptStatus in new[] { ApptStatus.Planned,ApptStatus.UnschedList,ApptStatus.PtNote,ApptStatus.PtNoteCompleted }) {
				appointment=GetAppt();
				appointment.AptStatus=apptStatus;
				Assert.AreEqual(0,Appointments.GetMirrors(appointment,true,GetOps(),null).Count,apptStatus.ToString());
			}
			appointment=GetAppt();
			appointment.AptDateTime=DateTime.MinValue;
			Assert.AreEqual(0,Appointments.GetMirrors(appointment,true,GetOps(),null).Count,"Not on the schedule.");
			appointment=GetAppt();
			appointment.ProvHyg=0;
			Assert.AreEqual(0,Appointments.GetMirrors(appointment,true,GetOps(),null).Count,"No secondary provider.");
			appointment=GetAppt();
			appointment.PatternSecondary="//////";
			Assert.AreEqual(0,Appointments.GetMirrors(appointment,true,GetOps(),null).Count,"No X in PatternSecondary.");
			appointment=GetAppt();
			appointment.Op=OP_HYG_A;
			Assert.AreEqual(0,Appointments.GetMirrors(appointment,true,GetOps(),null).Count,"Already in the secondary op.");
		}
		#endregion Mirrors

		#region History
		[TestMethod]
		public void History_CopiesIsMirrored() {
			Appointment appointment=GetAppt();
			HistAppointment histAppointment=new HistAppointment();
			histAppointment.SetAppt(appointment);
			Assert.IsTrue(histAppointment.IsMirrored);
			Assert.IsTrue(histAppointment.ToAppt().IsMirrored);
			appointment.IsMirrored=false;
			histAppointment.SetAppt(appointment);
			Assert.IsFalse(histAppointment.IsMirrored);
		}

		[TestMethod]
		public void Copy_KeepsIsMirrored() {
			Assert.IsTrue(GetAppt().Copy().IsMirrored);
		}
		#endregion History
	}
}
