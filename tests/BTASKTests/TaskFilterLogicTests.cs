// Logic tests for the Task Filter's "Unassigned" clinic and region choices (TaskFilters.cs). No database: the clinics are built in memory.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BTASKTests {
	[TestClass]
	public class TaskFilterLogicTests {
		private const long CLINIC_A=11;
		private const long CLINIC_B=12;
		private const long CLINIC_NO_REGION=13;
		private const long CLINIC_NO_REGION_2=14;
		private const long REGION_1=101;
		private const long REGION_2=102;

		///<summary>The clinics a user may see: A in region 1, B in region 2, and two clinics with no region.</summary>
		private static List<Clinic> GetClinics() {
			return new List<Clinic> {
				new Clinic { ClinicNum=CLINIC_A,Region=REGION_1,Abbr="A" },
				new Clinic { ClinicNum=CLINIC_B,Region=REGION_2,Abbr="B" },
				new Clinic { ClinicNum=CLINIC_NO_REGION,Region=0,Abbr="C" },
				new Clinic { ClinicNum=CLINIC_NO_REGION_2,Region=0,Abbr="D" },
			};
		}

		[TestMethod]
		public void UnassignedKey_IsNotZero_BecauseZeroMeansNoFilter() {
			Assert.AreEqual(-1,TaskFilters.UnassignedNum);
			Assert.AreNotEqual(0,TaskFilters.UnassignedNum);
		}

		[TestMethod]
		public void IsFilteringBy_MatchesTheTaskQueries() {
			Assert.IsFalse(TaskFilters.IsFilteringBy(null),"No list: no filter.");
			Assert.IsFalse(TaskFilters.IsFilteringBy(new List<long>()),"All: no filter.");
			Assert.IsFalse(TaskFilters.IsFilteringBy(new List<long> { 0 }),"The one-item {0} list is the Headquarters default, not a filter.");
			Assert.IsTrue(TaskFilters.IsFilteringBy(new List<long> { TaskFilters.UnassignedNum }));
			Assert.IsTrue(TaskFilters.IsFilteringBy(new List<long> { CLINIC_A }));
			Assert.IsTrue(TaskFilters.IsFilteringBy(new List<long> { TaskFilters.UnassignedNum,CLINIC_A }));
		}

		[TestMethod]
		public void ClinicUnassigned_KeepsTheFilter_WithOnlyClinicZero() {
			List<long> listClinicNumsForQuery=new List<long> { 0 };//What Tasks.BuildFilterWhereClause starts with.
			bool isUnassigned=TaskFilters.AddUnassignedClinicNums(listClinicNumsForQuery,new List<long> { TaskFilters.UnassignedNum },
				new List<long> { 0 },GetClinics());
			Assert.IsTrue(isUnassigned,"The query keeps the clinic condition although only clinic 0 is listed.");
			CollectionAssert.AreEqual(new List<long> { 0 },listClinicNumsForQuery,"Clinic: Unassigned adds no clinics; clinic 0 is the match.");
		}

		[TestMethod]
		public void ClinicUnassigned_WithARealClinic_AddsNothingMore() {
			List<long> listClinicNumsForQuery=new List<long> { 0,CLINIC_A };//Clinic A was added by the query from the clinic list.
			bool isUnassigned=TaskFilters.AddUnassignedClinicNums(listClinicNumsForQuery,new List<long> { TaskFilters.UnassignedNum,CLINIC_A },
				new List<long>(),GetClinics());
			Assert.IsTrue(isUnassigned);
			CollectionAssert.AreEqual(new List<long> { 0,CLINIC_A },listClinicNumsForQuery);
		}

		[TestMethod]
		public void RegionUnassigned_AddsTheClinicsWithNoRegion() {
			List<long> listClinicNumsForQuery=new List<long> { 0 };
			bool isUnassigned=TaskFilters.AddUnassignedClinicNums(listClinicNumsForQuery,new List<long>(),
				new List<long> { TaskFilters.UnassignedNum },GetClinics());
			Assert.IsTrue(isUnassigned);
			CollectionAssert.AreEquivalent(new List<long> { 0,CLINIC_NO_REGION,CLINIC_NO_REGION_2 },listClinicNumsForQuery);
		}

		[TestMethod]
		public void RegionUnassigned_WithARealRegion_AddsWithoutDuplicates() {
			//The query added the clinics of region 2 (B) already; Unassigned adds C and D, and never a clinic twice.
			List<long> listClinicNumsForQuery=new List<long> { 0,CLINIC_B,CLINIC_NO_REGION };
			bool isUnassigned=TaskFilters.AddUnassignedClinicNums(listClinicNumsForQuery,new List<long> { 0 },
				new List<long> { TaskFilters.UnassignedNum,REGION_2 },GetClinics());
			Assert.IsTrue(isUnassigned);
			CollectionAssert.AreEquivalent(new List<long> { 0,CLINIC_B,CLINIC_NO_REGION,CLINIC_NO_REGION_2 },listClinicNumsForQuery);
		}

		[TestMethod]
		public void RegionUnassigned_OnlyAddsClinicsTheUserMaySee() {
			//A user restricted to clinic A: the query only knows clinic A, which has a region.
			List<Clinic> listClinicsUnrestricted=GetClinics().FindAll(x => x.ClinicNum==CLINIC_A);
			List<long> listClinicNumsForQuery=new List<long> { 0 };
			Assert.IsTrue(TaskFilters.AddUnassignedClinicNums(listClinicNumsForQuery,new List<long>(),new List<long> { TaskFilters.UnassignedNum },
				listClinicsUnrestricted));
			CollectionAssert.AreEqual(new List<long> { 0 },listClinicNumsForQuery);
		}

		[TestMethod]
		public void ClinicListWins_OverRegionList_AsInTheQuery() {
			//Tasks.BuildFilterWhereClause filters by the clinic list when it is in effect and ignores the region list.
			List<long> listClinicNumsForQuery=new List<long> { 0,CLINIC_A };
			Assert.IsFalse(TaskFilters.AddUnassignedClinicNums(listClinicNumsForQuery,new List<long> { CLINIC_A },
				new List<long> { TaskFilters.UnassignedNum },GetClinics()),"Region: Unassigned does not apply while a clinic filter is in effect.");
			CollectionAssert.AreEqual(new List<long> { 0,CLINIC_A },listClinicNumsForQuery);
		}

		[TestMethod]
		public void NoUnassigned_ChangesNothing() {
			List<long> listClinicNumsForQuery=new List<long> { 0,CLINIC_A };
			Assert.IsFalse(TaskFilters.AddUnassignedClinicNums(listClinicNumsForQuery,new List<long> { CLINIC_A },new List<long> { 0 },GetClinics()));
			Assert.IsFalse(TaskFilters.AddUnassignedClinicNums(listClinicNumsForQuery,new List<long>(),new List<long> { REGION_1 },GetClinics()));
			Assert.IsFalse(TaskFilters.AddUnassignedClinicNums(listClinicNumsForQuery,new List<long> { 0 },new List<long> { 0 },GetClinics()),
				"The Headquarters default is unchanged.");
			Assert.IsFalse(TaskFilters.AddUnassignedClinicNums(listClinicNumsForQuery,new List<long>(),new List<long>(),GetClinics()),"All is unchanged.");
			Assert.IsFalse(TaskFilters.AddUnassignedClinicNums(listClinicNumsForQuery,null,null,null),"Missing lists are no filter.");
			CollectionAssert.AreEqual(new List<long> { 0,CLINIC_A },listClinicNumsForQuery);
		}

		[TestMethod]
		public void UnassignedChoices_AreDummyItemsWithTheUnassignedKey() {
			Clinic clinic=TaskFilters.GetUnassignedClinic();
			Assert.AreEqual(TaskFilters.UnassignedNum,clinic.ClinicNum);
			Assert.AreEqual("Unassigned",clinic.Abbr);
			Def def=TaskFilters.GetUnassignedRegion();
			Assert.AreEqual(TaskFilters.UnassignedNum,def.DefNum);
			Assert.AreEqual(DefCat.Regions,def.Category);
			Assert.AreEqual("Unassigned",def.ItemName);
			Assert.IsFalse(def.IsHidden,"Shown without the (hidden) marker.");
			Assert.AreEqual("Unassigned",TaskFilters.GetClinicFilterText(TaskFilters.UnassignedNum));
			Assert.AreEqual("Unassigned",TaskFilters.GetRegionFilterText(TaskFilters.UnassignedNum));
		}

		[TestMethod]
		public void PatientText_IsLastFirstDashPatNum() {
			Patient patient=new Patient { PatNum=42,LName="Synthetic",FName="Pat",Preferred="",MiddleI="" };
			Assert.AreEqual("Synthetic, Pat - 42",TaskFilters.GetPatientText(patient));
			Assert.AreEqual("",TaskFilters.GetPatientText(null));
		}
	}
}
