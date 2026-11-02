// Insurance Verification List filters of Open Dental 25.4 (M2-INSV), without a database: the appointment day range, the carrier filter and the
// Status Date text. Synthetic values only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace M2INSVTests {
	[TestClass]
	public class InsVerifyFilterTests {
		private CultureInfo _cultureInfoOld;

		[TestInitialize]
		public void TestInitialize() {
			//Lans.g returns the English text without a database only for en-US, and "g" date formats depend on the culture.
			_cultureInfoOld=Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US",useUserOverride:false);
		}

		[TestCleanup]
		public void TestCleanup() {
			Thread.CurrentThread.CurrentCulture=_cultureInfoOld;
		}

		#region Appointment day range (v25_4-appts-insverify-daterange)
		[TestMethod]
		public void ApptDateRange_MinAndMaxDaysFromToday() {
			InsVerifies.GetApptDateRange(new DateTime(2026,10,25),7,14,out DateTime dateStart,out DateTime dateEnd);
			Assert.AreEqual(new DateTime(2026,11,1),dateStart);
			Assert.AreEqual(new DateTime(2026,11,8),dateEnd);
		}

		[TestMethod]
		public void ApptDateRange_MinZeroIsThe24_3List() {
			//24.3 searched today through today+N. Min 0 (the default every time the window opens) gives exactly that.
			InsVerifies.GetApptDateRange(new DateTime(2026,10,25),0,7,out DateTime dateStart,out DateTime dateEnd);
			Assert.AreEqual(new DateTime(2026,10,25),dateStart);
			Assert.AreEqual(new DateTime(2026,11,1),dateEnd);
		}

		[TestMethod]
		public void ApptDateRange_IsWholeDaysWhateverTheTimeOfToday() {
			InsVerifies.GetApptDateRange(new DateTime(2026,10,25,16,45,12),1,1,out DateTime dateStart,out DateTime dateEnd);
			Assert.AreEqual(new DateTime(2026,10,26),dateStart);
			Assert.AreEqual(new DateTime(2026,10,26),dateEnd);
		}

		[TestMethod]
		public void ApptDayRange_MinMayNotExceedMax() {
			Assert.IsTrue(InsVerifies.IsApptDayRangeValid(0,0));
			Assert.IsTrue(InsVerifies.IsApptDayRangeValid(0,7));
			Assert.IsTrue(InsVerifies.IsApptDayRangeValid(7,14));
			Assert.IsTrue(InsVerifies.IsApptDayRangeValid(14,14));
			Assert.IsFalse(InsVerifies.IsApptDayRangeValid(15,14),"min above max");
			Assert.IsFalse(InsVerifies.IsApptDayRangeValid(-1,14),"negative min");
			Assert.IsFalse(InsVerifies.IsApptDayRangeValid(0,-1),"negative max");
		}
		#endregion Appointment day range

		#region Carrier filter (v25_4-appts-insverify-carriers)
		[TestMethod]
		public void CarrierFilterClause_EmptyMeansAllCarriers() {
			Assert.AreEqual("",InsVerifies.GetCarrierFilterClause(null));
			Assert.AreEqual("",InsVerifies.GetCarrierFilterClause(new List<long>()));
			Assert.AreEqual("",InsVerifies.GetCarrierFilterClause(new List<long> { 0,-1 }),"no real CarrierNum");
		}

		[TestMethod]
		public void CarrierFilterClause_FiltersOnCarrierNumsOnce() {
			Assert.AreEqual("AND carrier.CarrierNum IN(5,3)",InsVerifies.GetCarrierFilterClause(new List<long> { 5,3,5,0 }));
		}

		[TestMethod]
		public void CarrierFilterText_AllNamesOrCount() {
			Assert.AreEqual("All",InsVerifies.GetCarrierFilterText(null,13));
			Assert.AreEqual("All",InsVerifies.GetCarrierFilterText(new List<Carrier>(),13));
			Assert.AreEqual("A Long Synthetic Carrier Name",InsVerifies.GetCarrierFilterText(new List<Carrier> { NewCarrier("A Long Synthetic Carrier Name") },13),
				"one carrier always shows its name");
			Assert.AreEqual("Syn A, Syn B",InsVerifies.GetCarrierFilterText(new List<Carrier> { NewCarrier("Syn A"),NewCarrier("Syn B") },13));
			Assert.AreEqual("3 carriers",InsVerifies.GetCarrierFilterText(new List<Carrier> { NewCarrier("Syn A"),NewCarrier("Syn B"),NewCarrier("Syn C") },13));
		}

		[TestMethod]
		public void CarrierPick_SearchChangesDoNotLosePicks() {
			HashSet<long> hashSetPicked=new HashSet<long> { 1,2 };
			//The grid lists carriers 2, 3 and 4 (a search hides 1); the user selects 3 and 4, so 2 is no longer picked.
			FormCarriers.MergePickedCarrierNums(hashSetPicked,new List<long> { 2,3,4 },new List<long> { 3,4 });
			CollectionAssert.AreEquivalent(new List<long> { 1,3,4 },hashSetPicked.ToList(),"1 was not listed, so it stays picked");
			//Nothing listed (a search with no match): nothing changes.
			FormCarriers.MergePickedCarrierNums(hashSetPicked,new List<long>(),new List<long>());
			CollectionAssert.AreEquivalent(new List<long> { 1,3,4 },hashSetPicked.ToList());
			//Everything listed and nothing selected: no carrier picked, which the list treats as all carriers.
			FormCarriers.MergePickedCarrierNums(hashSetPicked,new List<long> { 1,2,3,4 },new List<long>());
			Assert.AreEqual(0,hashSetPicked.Count);
		}
		#endregion Carrier filter

		#region Assignment time
		[TestMethod]
		public void StatusDate_ShowsDateAndTimeOrBlank() {
			Assert.AreEqual("",FormInsVerificationList.GetDateLastAssignedText(DateTime.MinValue),"never assigned");
			Assert.AreEqual("10/25/2026 2:05 PM",FormInsVerificationList.GetDateLastAssignedText(new DateTime(2026,10,25,14,5,59)));
			Assert.AreEqual("10/25/2026 12:00 AM",FormInsVerificationList.GetDateLastAssignedText(new DateTime(2026,10,25)),
				"a row assigned before 25.4 (date only) shows midnight");
		}

		[TestMethod]
		public void AssignmentWrites_KeepTheTimeOfDay() {
			//Every place that stamps an assignment or a status change stores DateTime.Now since the column became a datetime (25.4). Resets to
			//DateTime.MinValue are unchanged. Commented-out code is ignored.
			string[] arrayFiles=new string[] {
				Path.Combine("OpenPmsDental","Forms","FormInsVerificationList.cs"),
				Path.Combine("OpenPmsDentalBusiness","Data Interface","InsVerifies.cs"),
				Path.Combine("OpenPmsDentalBusiness","Data Interface","InsVerifyHists.cs"),
				Path.Combine("OpenPmsDentalBusiness","Data Interface","PatPlans.cs"),
			};
			List<string> listOffending=new List<string>();
			foreach(string relativePath in arrayFiles) {
				string[] arrayLines=File.ReadAllLines(TestDb.FindRepoFile(relativePath));
				for(int i=0;i<arrayLines.Length;i++) {
					string line=arrayLines[i].Trim();
					if(line.StartsWith("//")) {
						continue;
					}
					if(Regex.IsMatch(line,@"DateLastAssigned\s*=\s*DateTime\.Today")) {
						listOffending.Add(relativePath+":"+(i+1)+": "+line);
					}
				}
			}
			Assert.AreEqual(0,listOffending.Count,string.Join(Environment.NewLine,listOffending));
		}
		#endregion Assignment time

		private static Carrier NewCarrier(string carrierName) {
			Carrier carrier=new Carrier();
			carrier.CarrierName=carrierName;
			return carrier;
		}
	}
}
