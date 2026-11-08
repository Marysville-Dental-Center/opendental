// Advanced Appointment Search for several blockout types (Open Dental 26.2 Appointments items 4 and 6, port P262-APPT) on the scratch database:
// ApptSearch.GetSearchResultsForBlockouts gives, for each date, the first time slot of every selected blockout type, counts dates (not rows),
// and for one blockout type returns exactly what the 26.1 search (GetSearchResults, GetSearchResultsForBlockoutAndProvider) returns.
// Synthetic data only: made-up operatories, blockout types, a provider and a patient, on dates far from anything in the seed rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace P262ApptTests {
	[TestClass]
	public class BlockoutSearchDbTests {
		private static int _counter;

		[TestInitialize]
		public void TestInitialize() {
			ApptDb.Begin();
			PrefT.UpdateInt(PrefName.AppointmentSearchBehavior,(int)SearchBehaviorCriteria.ProviderTimeOperatory);
			Cache.Refresh(InvalidType.Prefs);
		}

		#region Helpers
		///<summary>What one test works with: its own two operatories, its own day (a Tuesday) and an appointment of 30 minutes on the pinboard.</summary>
		private class Setup {
			public long OpNumA;
			public long OpNumB;
			public DateTime Date;
			public Appointment AppointmentPin;
			public Patient Patient;

			public List<long> ListOpNums {
				get { return new List<long> { OpNumA,OpNumB }; }
			}
		}

		///<summary>Each test gets its own day, four weeks after the previous test's, and its own operatories, so the tests never see each other's
		///schedules.</summary>
		private static Setup NewSetup() {
			_counter++;
			Setup setup=new Setup();
			setup.Date=new DateTime(2031,3,4).AddDays(28*_counter);
			setup.OpNumA=OperatoryT.CreateOperatory("P262A"+_counter,"P262 Op A "+_counter,itemOrder:900+_counter*2).OperatoryNum;
			setup.OpNumB=OperatoryT.CreateOperatory("P262B"+_counter,"P262 Op B "+_counter,itemOrder:901+_counter*2).OperatoryNum;
			setup.Patient=PatientT.CreatePatient("P262Search"+_counter,lName:"Synthetic",fName:"Search");
			//Not in any operatory, so it never blocks a slot: the appointment that is being placed.
			setup.AppointmentPin=AppointmentT.CreateAppointment(setup.Patient.PatNum,new DateTime(2030,1,2,9,0,0),0,setup.Patient.PriProv,
				pattern:"//XX//",aptStatus:ApptStatus.UnschedList);
			return setup;
		}

		///<summary>A blockout type with an explicit place in the definition list.</summary>
		private static long NewBlockoutType(string name,int itemOrder) {
			Def def=new Def { Category=DefCat.BlockoutTypes,ItemOrder=itemOrder,ItemName=name+" "+_counter,ItemValue="",ItemColor=Color.LightBlue };
			long defNum=Defs.Insert(def);
			Defs.RefreshCache();
			return defNum;
		}

		private static Schedule AddBlockout(long defNum,long opNum,DateTime date,int hourStart,int hourStop) {
			return ScheduleT.CreateSchedule(date,TimeSpan.FromHours(hourStart),TimeSpan.FromHours(hourStop),ScheduleType.Blockout,blockoutType:defNum,
				listOpNums:new List<long> { opNum });
		}

		private static List<ScheduleOpening> SearchBlockouts(Setup setup,List<long> listBlockoutDefNums,List<long> listProvNums=null,int dateCount=15,
			int days=20)
		{
			return ApptSearch.GetSearchResultsForBlockouts(setup.AppointmentPin.AptNum,setup.Date.AddDays(-1),setup.Date.AddDays(days),
				listProvNums??new List<long> { 0 },setup.ListOpNums,new List<long>(),TimeSpan.Zero,TimeSpan.Zero,listBlockoutDefNums,dateCount);
		}

		///<summary>The 26.1 search for one blockout type without providers, as the Advanced Appointment Search window called it.</summary>
		private static List<ScheduleOpening> SearchOneTypeOld(Setup setup,long blockoutDefNum,int days=20) {
			return ApptSearch.GetSearchResults(setup.AppointmentPin.AptNum,setup.Date.AddDays(-1),setup.Date.AddDays(days),new List<long> { 0 },
				setup.ListOpNums,new List<long>(),TimeSpan.Zero,TimeSpan.Zero,new List<long> { blockoutDefNum },resultCount:15);
		}

		private static string Describe(IEnumerable<ScheduleOpening> listOpenings) {
			return string.Join("; ",listOpenings.Select(x => x.DateTimeAvail.ToString("yyyy-MM-dd HH:mm")+" op "+x.OpNum+" prov "+x.ProvNum
				+" type "+x.BlockoutDefNum));
		}
		#endregion Helpers

		[TestMethod]
		public void TwoTypes_ListTheFirstSlotOfEachTypeInADay() {
			Setup setup=NewSetup();
			long defNumCrown=NewBlockoutType("P262 Crown",900);
			long defNumNewPat=NewBlockoutType("P262 New Patient",901);
			long defNumOther=NewBlockoutType("P262 Other",902);
			AddBlockout(defNumCrown,setup.OpNumA,setup.Date,9,10);
			AddBlockout(defNumNewPat,setup.OpNumB,setup.Date,14,15);
			AddBlockout(defNumOther,setup.OpNumA,setup.Date,8,9);//not selected
			AddBlockout(defNumNewPat,setup.OpNumA,setup.Date.AddDays(1),11,12);
			List<ScheduleOpening> listOpenings=SearchBlockouts(setup,new List<long> { defNumCrown,defNumNewPat });
			Assert.AreEqual(3,listOpenings.Count,Describe(listOpenings));
			Assert.AreEqual(setup.Date.AddHours(9),listOpenings[0].DateTimeAvail);
			Assert.AreEqual(defNumCrown,listOpenings[0].BlockoutDefNum);
			Assert.AreEqual(setup.OpNumA,listOpenings[0].OpNum);
			Assert.AreEqual(setup.Date.AddHours(14),listOpenings[1].DateTimeAvail,"The second blockout type of the same day has its own row.");
			Assert.AreEqual(defNumNewPat,listOpenings[1].BlockoutDefNum);
			Assert.AreEqual(setup.OpNumB,listOpenings[1].OpNum);
			Assert.AreEqual(setup.Date.AddDays(1).AddHours(11),listOpenings[2].DateTimeAvail);
			Assert.AreEqual(defNumNewPat,listOpenings[2].BlockoutDefNum);
			Assert.IsFalse(listOpenings.Any(x => x.BlockoutDefNum==defNumOther),"A blockout type that is not selected is not searched.");
			//Before 26.2 the window searched one type: its first slot of the day only.
			List<ScheduleOpening> listOld=SearchOneTypeOld(setup,defNumNewPat);
			Assert.AreEqual(2,listOld.Count,Describe(listOld));
		}

		[TestMethod]
		public void OneType_SameAsTheSearchBefore26_2_InBothSearchBehaviors() {
			foreach(SearchBehaviorCriteria searchBehavior in new[] { SearchBehaviorCriteria.ProviderTimeOperatory,SearchBehaviorCriteria.ProviderTime }) {
				PrefT.UpdateInt(PrefName.AppointmentSearchBehavior,(int)searchBehavior);
				Cache.Refresh(InvalidType.Prefs);
				Setup setup=NewSetup();
				long defNum=NewBlockoutType("P262 Hygiene",900);
				long defNumOther=NewBlockoutType("P262 Surgery",901);
				AddBlockout(defNum,setup.OpNumA,setup.Date,9,11);
				AddBlockout(defNum,setup.OpNumB,setup.Date,8,9);
				AddBlockout(defNumOther,setup.OpNumB,setup.Date,10,12);
				AddBlockout(defNum,setup.OpNumB,setup.Date.AddDays(2),13,14);
				AddBlockout(defNum,setup.OpNumA,setup.Date.AddDays(3),7,8);
				//A scheduled appointment inside the first blockout of the third date: its first slot moves past it.
				AppointmentT.CreateAppointment(setup.Patient.PatNum,setup.Date.AddDays(3).AddHours(7),setup.OpNumA,setup.Patient.PriProv,
					pattern:"//XX//");
				List<ScheduleOpening> listOld=SearchOneTypeOld(setup,defNum);
				List<ScheduleOpening> listNew=SearchBlockouts(setup,new List<long> { defNum });
				Assert.AreEqual(Describe(listOld.Select(x => new ScheduleOpening { DateTimeAvail=x.DateTimeAvail,OpNum=x.OpNum,ProvNum=x.ProvNum,
					ClinicNum=x.ClinicNum,BlockoutDefNum=defNum })),Describe(listNew),searchBehavior.ToString());
				Assert.IsTrue(listOld.Count>=3,searchBehavior+": "+Describe(listOld));
				Assert.IsTrue(listOld.All(x => x.BlockoutDefNum==0),"The 26.1 search does not name the blockout type.");
			}
		}

		[TestMethod]
		public void TwoTypes_AnAppointmentInABlockoutMovesItsSlot() {
			Setup setup=NewSetup();
			long defNumCrown=NewBlockoutType("P262 Crown",900);
			long defNumNewPat=NewBlockoutType("P262 New Patient",901);
			AddBlockout(defNumCrown,setup.OpNumA,setup.Date,9,11);
			AddBlockout(defNumNewPat,setup.OpNumB,setup.Date,9,10);
			AppointmentT.CreateAppointment(setup.Patient.PatNum,setup.Date.AddHours(9),setup.OpNumA,setup.Patient.PriProv,pattern:"//XXXXXXXX//");
			List<ScheduleOpening> listOpenings=SearchBlockouts(setup,new List<long> { defNumCrown,defNumNewPat });
			Assert.AreEqual(2,listOpenings.Count,Describe(listOpenings));
			Assert.AreEqual(setup.Date.AddHours(9),listOpenings[0].DateTimeAvail);
			Assert.AreEqual(defNumNewPat,listOpenings[0].BlockoutDefNum);
			Assert.AreEqual(setup.Date.AddHours(10),listOpenings[1].DateTimeAvail,"The hour-long appointment fills the first hour of the Crown blockout.");
			Assert.AreEqual(defNumCrown,listOpenings[1].BlockoutDefNum);
		}

		[TestMethod]
		public void SameTime_RowsFollowTheOrderOfTheBlockoutTypes() {
			Setup setup=NewSetup();
			long defNumSecond=NewBlockoutType("P262 Second",951);
			long defNumFirst=NewBlockoutType("P262 First",950);
			AddBlockout(defNumSecond,setup.OpNumA,setup.Date,9,10);
			AddBlockout(defNumFirst,setup.OpNumB,setup.Date,9,10);
			List<ScheduleOpening> listOpenings=SearchBlockouts(setup,new List<long> { defNumSecond,defNumFirst });
			Assert.AreEqual(2,listOpenings.Count,Describe(listOpenings));
			Assert.AreEqual(defNumFirst,listOpenings[0].BlockoutDefNum,"Same time: the type earlier in the definition list comes first.");
			Assert.AreEqual(defNumSecond,listOpenings[1].BlockoutDefNum);
		}

		[TestMethod]
		public void DateCount_CountsDatesNotRows() {
			Setup setup=NewSetup();
			long defNumCrown=NewBlockoutType("P262 Crown",900);
			long defNumNewPat=NewBlockoutType("P262 New Patient",901);
			for(int d=0;d<3;d++) {
				AddBlockout(defNumCrown,setup.OpNumA,setup.Date.AddDays(d),9,10);
				AddBlockout(defNumNewPat,setup.OpNumB,setup.Date.AddDays(d),13,14);
			}
			List<ScheduleOpening> listOpenings=SearchBlockouts(setup,new List<long> { defNumCrown,defNumNewPat },dateCount:2);
			Assert.AreEqual(4,listOpenings.Count,Describe(listOpenings));
			Assert.AreEqual(2,listOpenings.Select(x => x.DateTimeAvail.Date).Distinct().Count());
			Assert.AreEqual(setup.Date.AddDays(1),listOpenings.Max(x => x.DateTimeAvail).Date);
			//More: the next search starts the day after the last date listed and finds the third date.
			List<ScheduleOpening> listMore=ApptSearch.GetSearchResultsForBlockouts(setup.AppointmentPin.AptNum,setup.Date.AddDays(1),setup.Date.AddDays(20),
				new List<long> { 0 },setup.ListOpNums,new List<long>(),TimeSpan.Zero,TimeSpan.Zero,new List<long> { defNumCrown,defNumNewPat },2);
			Assert.AreEqual(2,listMore.Count,Describe(listMore));
			Assert.IsTrue(listMore.All(x => x.DateTimeAvail.Date==setup.Date.AddDays(2)));
		}

		[TestMethod]
		public void StartingBeforeAndAfter_ApplyToEachType() {
			Setup setup=NewSetup();
			long defNumCrown=NewBlockoutType("P262 Crown",900);
			long defNumNewPat=NewBlockoutType("P262 New Patient",901);
			AddBlockout(defNumCrown,setup.OpNumA,setup.Date,8,9);
			AddBlockout(defNumNewPat,setup.OpNumB,setup.Date,13,14);
			List<ScheduleOpening> listOpenings=ApptSearch.GetSearchResultsForBlockouts(setup.AppointmentPin.AptNum,setup.Date.AddDays(-1),
				setup.Date.AddDays(5),new List<long> { 0 },setup.ListOpNums,new List<long>(),TimeSpan.Zero,TimeSpan.FromHours(10),
				new List<long> { defNumCrown,defNumNewPat });
			Assert.AreEqual(1,listOpenings.Count,"Starting after 10:00 leaves the afternoon type only: "+Describe(listOpenings));
			Assert.AreEqual(defNumNewPat,listOpenings[0].BlockoutDefNum);
		}

		[TestMethod]
		public void WithProvider_EachTypeIntersectsTheProviderSchedule_AndOneTypeIsAsBefore() {
			Setup setup=NewSetup();
			long provNum=ProviderT.CreateProvider("P262P"+_counter,"Search","Provider");
			long defNumCrown=NewBlockoutType("P262 Crown",900);
			long defNumNewPat=NewBlockoutType("P262 New Patient",901);
			ScheduleT.CreateSchedule(setup.Date,TimeSpan.FromHours(8),TimeSpan.FromHours(17),ScheduleType.Provider,provNum:provNum,
				listOpNums:new List<long> { setup.OpNumA });
			AddBlockout(defNumCrown,setup.OpNumA,setup.Date,9,10);
			AddBlockout(defNumNewPat,setup.OpNumA,setup.Date,14,15);
			AddBlockout(defNumNewPat,setup.OpNumB,setup.Date,10,11);//the provider does not work in B
			List<long> listProvNums=new List<long> { 0,provNum };//as the window passes them: 0 for the blockouts, then the providers
			List<ScheduleOpening> listOpenings=SearchBlockouts(setup,new List<long> { defNumCrown,defNumNewPat },listProvNums);
			Assert.AreEqual(2,listOpenings.Count,Describe(listOpenings));
			Assert.AreEqual(setup.Date.AddHours(9),listOpenings[0].DateTimeAvail);
			Assert.AreEqual(defNumCrown,listOpenings[0].BlockoutDefNum);
			Assert.AreEqual(provNum,listOpenings[0].ProvNum);
			Assert.AreEqual(setup.Date.AddHours(14),listOpenings[1].DateTimeAvail);
			Assert.AreEqual(defNumNewPat,listOpenings[1].BlockoutDefNum);
			Assert.AreEqual(provNum,listOpenings[1].ProvNum);
			foreach(long defNum in new[] { defNumCrown,defNumNewPat }) {
				List<ScheduleOpening> listOld=ApptSearch.GetSearchResultsForBlockoutAndProvider(new List<long> { 0,provNum },setup.AppointmentPin.AptNum,
					setup.Date.AddDays(-1),setup.Date.AddDays(20),setup.ListOpNums,new List<long>(),TimeSpan.Zero,TimeSpan.Zero,new List<long> { defNum },15);
				List<ScheduleOpening> listNew=SearchBlockouts(setup,new List<long> { defNum },new List<long> { 0,provNum });
				Assert.AreEqual(Describe(listOld.Select(x => new ScheduleOpening { DateTimeAvail=x.DateTimeAvail,OpNum=x.OpNum,ProvNum=x.ProvNum,
					ClinicNum=x.ClinicNum,BlockoutDefNum=defNum })),Describe(listNew));
				Assert.AreEqual(1,listNew.Count,Describe(listNew));
			}
		}

		[TestMethod]
		public void NoBlockoutType_IsTheProviderSearch() {
			Setup setup=NewSetup();
			long provNum=ProviderT.CreateProvider("P262Q"+_counter,"Search","Provider");
			ScheduleT.CreateSchedule(setup.Date,TimeSpan.FromHours(8),TimeSpan.FromHours(12),ScheduleType.Provider,provNum:provNum,
				listOpNums:new List<long> { setup.OpNumA });
			List<ScheduleOpening> listNew=ApptSearch.GetSearchResultsForBlockouts(setup.AppointmentPin.AptNum,setup.Date.AddDays(-1),setup.Date.AddDays(5),
				new List<long> { provNum },setup.ListOpNums,new List<long>(),TimeSpan.Zero,TimeSpan.Zero,new List<long>());
			List<ScheduleOpening> listOld=ApptSearch.GetSearchResults(setup.AppointmentPin.AptNum,setup.Date.AddDays(-1),setup.Date.AddDays(5),
				new List<long> { provNum },setup.ListOpNums,new List<long>(),TimeSpan.Zero,TimeSpan.Zero,resultCount:15);
			Assert.AreEqual(Describe(listOld),Describe(listNew));
			Assert.AreEqual(1,listNew.Count,Describe(listNew));
			Assert.AreEqual(0,listNew[0].BlockoutDefNum);
		}
	}
}
