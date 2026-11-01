// Integration tests for the Task Filter against the scratch database of TaskFilterDb: the lists the Task Filter window hands to the task
// queries, with the "Unassigned" clinic and region choices and with one specific patient. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BTASKTests {
	[TestClass]
	public class TaskFilterDbTests {
		private static readonly long U=TaskFilters.UnassignedNum;

		[TestInitialize]
		public void TestInitialize() {
			TaskFilterDb.Begin();
		}

		///<summary>The TaskNums a task list shows with these filters, the way UserControlTasks asks for them (Tasks.RefreshChildren).</summary>
		private static List<long> GetShown(List<long> listClinicNums,List<long> listRegionNums,Patient patient=null,Userod userod=null) {
			userod=userod??TaskFilterDb.UserAll;
			return Tasks.RefreshChildren(TaskFilterDb.TaskListMain.TaskListNum,false,DateTime.MinValue,userod.UserNum,0,TaskType.All,false,
				listClinicNums,listRegionNums,DateTime.MinValue,DateTime.MinValue,patient)
				.Select(x => x.TaskNum).ToList();
		}

		private static List<long> L(params long[] arrayNums) {
			return arrayNums.ToList();
		}

		private static void AssertShown(List<long> listExpected,List<long> listActual,string message) {
			CollectionAssert.AreEquivalent(listExpected,listActual,message+" Expected "+string.Join(",",listExpected)+"; got "+string.Join(",",listActual)+".");
		}

		///<summary>The tasks whose patient or appointment has no clinic, plus the task with nothing attached.</summary>
		private static List<long> GetNoClinicTasks() {
			return L(TaskFilterDb.TaskPat0,TaskFilterDb.TaskAptPatANoClinic,TaskFilterDb.TaskNone);
		}

		[TestMethod]
		public void AllAndDefault_ShowEveryTask_AsBefore() {
			List<long> listAll=L(TaskFilterDb.TaskPatA,TaskFilterDb.TaskPatB,TaskFilterDb.TaskPatC,TaskFilterDb.TaskPat0,TaskFilterDb.TaskAptPatANoClinic,
				TaskFilterDb.TaskAptPat0ClinicB,TaskFilterDb.TaskNone);
			AssertShown(listAll,GetShown(L(),L()),"All clinics and all regions.");
			AssertShown(listAll,GetShown(L(0),L(0)),"The Headquarters default ({0} and {0}) for a user who may see every clinic.");
		}

		[TestMethod]
		public void ClinicUnassigned_ShowsTasksWithNoClinic() {
			AssertShown(GetNoClinicTasks(),GetShown(L(U),L()),"Clinic: Unassigned.");
		}

		[TestMethod]
		public void ClinicUnassigned_UsesTheAppointmentsClinic_ForAppointmentTasks() {
			List<long> listShown=GetShown(L(U),L());
			Assert.IsTrue(listShown.Contains(TaskFilterDb.TaskAptPatANoClinic),"Patient A has clinic A, but the appointment has none.");
			Assert.IsFalse(listShown.Contains(TaskFilterDb.TaskAptPat0ClinicB),"The patient has no clinic, but the appointment is in clinic B.");
		}

		[TestMethod]
		public void ClinicUnassigned_TogetherWithARealClinic() {
			AssertShown(GetNoClinicTasks().Concat(L(TaskFilterDb.TaskPatA)).ToList(),GetShown(L(U,TaskFilterDb.ClinicA.ClinicNum),L()),
				"Clinic: Unassigned and A.");
			AssertShown(GetNoClinicTasks().Concat(L(TaskFilterDb.TaskPatB,TaskFilterDb.TaskAptPat0ClinicB)).ToList(),
				GetShown(L(TaskFilterDb.ClinicB.ClinicNum,U),L(0)),"Clinic: B and Unassigned, with the region list at its default.");
		}

		[TestMethod]
		public void RealClinic_StillShowsTasksWithNoClinic_As24_3Did() {
			//INFERRED (docs/port-notes/B-TASK.md): tasks with clinic 0 stay visible under every clinic filter, as in 24.3.
			AssertShown(GetNoClinicTasks().Concat(L(TaskFilterDb.TaskPatA)).ToList(),GetShown(L(TaskFilterDb.ClinicA.ClinicNum),L()),"Clinic: A.");
		}

		[TestMethod]
		public void RegionUnassigned_ShowsTasksOfClinicsWithNoRegion() {
			AssertShown(GetNoClinicTasks().Concat(L(TaskFilterDb.TaskPatC)).ToList(),GetShown(L(),L(U)),"Region: Unassigned.");
			AssertShown(GetNoClinicTasks().Concat(L(TaskFilterDb.TaskPatC)).ToList(),GetShown(L(0),L(U)),
				"Region: Unassigned, with the clinic list at its default.");
		}

		[TestMethod]
		public void RegionUnassigned_TogetherWithARealRegion() {
			AssertShown(GetNoClinicTasks().Concat(L(TaskFilterDb.TaskPatC,TaskFilterDb.TaskPatA)).ToList(),
				GetShown(L(),L(U,TaskFilterDb.Region1.DefNum)),"Region: Unassigned and region 1.");
			AssertShown(GetNoClinicTasks().Concat(L(TaskFilterDb.TaskPatB,TaskFilterDb.TaskAptPat0ClinicB)).ToList(),
				GetShown(L(),L(TaskFilterDb.Region2.DefNum)),"Region: region 2 alone is unchanged.");
		}

		[TestMethod]
		public void RegionUnassigned_RespectsClinicRestrictions() {
			//The user may only see clinic A. Clinic C has no region but is not the user's, so its task stays hidden.
			AssertShown(GetNoClinicTasks(),GetShown(L(),L(U),userod:TaskFilterDb.UserClinicA),"Region: Unassigned for a user restricted to A.");
			AssertShown(GetNoClinicTasks(),GetShown(L(U),L(),userod:TaskFilterDb.UserClinicA),"Clinic: Unassigned for a user restricted to A.");
		}

		[TestMethod]
		public void UnassignedKey_WorksInEveryTaskQuery() {
			//The open-tickets query of the Patient Tickets tab joins differently; the filter must hold there as well.
			long userNum=TaskFilterDb.UserAll.UserNum;
			CollectionAssert.AreEqual(L(TaskFilterDb.TaskPat0),
				Tasks.RefreshPatientTickets(TaskFilterDb.Pat0.PatNum,userNum,L(U),L()).Select(x => x.TaskNum).ToList());
			Assert.AreEqual(0,Tasks.RefreshPatientTickets(TaskFilterDb.PatA.PatNum,userNum,L(U),L()).Count,"Patient A has clinic A.");
			Assert.AreEqual(0,Tasks.RefreshPatientTickets(TaskFilterDb.PatC.PatNum,userNum,L(U),L()).Count,"Clinic C is a clinic.");
			CollectionAssert.AreEqual(L(TaskFilterDb.TaskPatC),
				Tasks.RefreshPatientTickets(TaskFilterDb.PatC.PatNum,userNum,L(),L(U)).Select(x => x.TaskNum).ToList(),"Clinic C has no region.");
		}

		[TestMethod]
		public void SpecificPatient_ShowsTasksOfThePatientAndOfItsAppointments() {
			AssertShown(L(TaskFilterDb.TaskPatA,TaskFilterDb.TaskAptPatANoClinic),GetShown(L(),L(),TaskFilterDb.PatA),"Patient A, all clinics.");
			AssertShown(L(TaskFilterDb.TaskPat0,TaskFilterDb.TaskAptPat0ClinicB),GetShown(L(),L(),TaskFilterDb.Pat0),"The patient with no clinic.");
			AssertShown(L(TaskFilterDb.TaskPatB),GetShown(L(),L(),TaskFilterDb.PatB),"Patient B.");
		}

		[TestMethod]
		public void SpecificPatient_CombinesWithUnassigned() {
			AssertShown(L(TaskFilterDb.TaskAptPatANoClinic),GetShown(L(U),L(),TaskFilterDb.PatA),"Patient A and Clinic: Unassigned.");
			AssertShown(L(TaskFilterDb.TaskPat0,TaskFilterDb.TaskAptPat0ClinicB),GetShown(L(TaskFilterDb.ClinicB.ClinicNum),L(),TaskFilterDb.Pat0),
				"The patient with no clinic and Clinic: B.");
			AssertShown(L(TaskFilterDb.TaskPatC),GetShown(L(),L(U),TaskFilterDb.PatC),"Patient C and Region: Unassigned.");
		}

		[TestMethod]
		public void FilterText_NamesUnassigned() {
			Assert.AreEqual("Unassigned,Synthetic A",string.Join(",",L(U,TaskFilterDb.ClinicA.ClinicNum).Select(x => TaskFilters.GetClinicFilterText(x))));
			Assert.AreEqual("Unassigned,Synthetic Region 2",
				string.Join(",",L(U,TaskFilterDb.Region2.DefNum).Select(x => TaskFilters.GetRegionFilterText(x))));
		}
	}
}
