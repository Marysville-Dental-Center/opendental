// Tests for the Reports To filter of the Manage module's employee time clock (Open Dental 25.3, v25_3-manage-timeclock-reportsto). These tests
// need no database: the employee lists are passed in.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BTIMETests {
	[TestClass]
	public class EmployeeReportsToTests {
		///<summary>A small synthetic office. Ann and Bob are supervisors; Dee reports to Bob, so she is an indirect report of Ann.</summary>
		private static List<Employee> GetEmployees() {
			return new List<Employee> {
				new Employee { EmployeeNum=1,FName="Ann",LName="Alpha",ReportsTo=0 },
				new Employee { EmployeeNum=2,FName="Bob",LName="Bravo",ReportsTo=1 },
				new Employee { EmployeeNum=3,FName="Cy",LName="Charlie",ReportsTo=1 },
				new Employee { EmployeeNum=4,FName="Dee",LName="Delta",ReportsTo=2 },
				new Employee { EmployeeNum=5,FName="Eve",LName="Echo",ReportsTo=5 },//reports to herself: bad data, not a supervisor
				new Employee { EmployeeNum=6,FName="Fay",LName="Foxtrot",ReportsTo=99 },//supervisor was deleted
				new Employee { EmployeeNum=8,FName="Hal",LName="Hotel",ReportsTo=7 },//supervisor is hidden
			};
		}

		///<summary>The non-hidden employees, which is what the employee cache's short list holds.</summary>
		private static List<Employee> GetNonHidden() {
			return GetEmployees();
		}

		[TestMethod]
		public void GetSupervisors_EveryoneSomeoneReportsTo_SortedByName() {
			List<Employee> listSupervisors=EmployeeReportsTo.GetSupervisors(GetEmployees(),GetNonHidden());
			CollectionAssert.AreEqual(new List<long> { 1,2 },listSupervisors.Select(x => x.EmployeeNum).ToList(),
				"Ann and Bob; not Eve (reports to herself), not the deleted 99 and not the hidden 7.");
		}

		[TestMethod]
		public void GetSupervisors_OnlyForTheEmployeesShown() {
			//After the clinic filter only Dee is in the time clock, so only her supervisor is offered.
			List<Employee> listShown=GetEmployees().Where(x => x.EmployeeNum==4).ToList();
			List<Employee> listSupervisors=EmployeeReportsTo.GetSupervisors(listShown,GetNonHidden());
			CollectionAssert.AreEqual(new List<long> { 2 },listSupervisors.Select(x => x.EmployeeNum).ToList());
		}

		[TestMethod]
		public void GetSupervisors_SupervisorAtAnotherClinic_IsStillOffered() {
			//Bob and Cy work at a clinic that Ann does not; Ann is still their supervisor.
			List<Employee> listShown=GetEmployees().Where(x => x.EmployeeNum==2 || x.EmployeeNum==3).ToList();
			List<Employee> listSupervisors=EmployeeReportsTo.GetSupervisors(listShown,GetNonHidden());
			CollectionAssert.AreEqual(new List<long> { 1 },listSupervisors.Select(x => x.EmployeeNum).ToList());
		}

		[TestMethod]
		public void GetSupervisors_HiddenSupervisorPassedIn_IsLeftOut() {
			List<Employee> listAll=GetEmployees();
			listAll.Add(new Employee { EmployeeNum=7,FName="Gus",LName="Golf",IsHidden=true });
			List<Employee> listSupervisors=EmployeeReportsTo.GetSupervisors(listAll,listAll);
			Assert.IsFalse(listSupervisors.Any(x => x.EmployeeNum==7));
		}

		[TestMethod]
		public void GetSupervisors_NobodyReportsToAnyone_OnlyAll() {
			List<Employee> listEmployees=new List<Employee> {
				new Employee { EmployeeNum=1,FName="Ann" },
				new Employee { EmployeeNum=2,FName="Bob" },
			};
			Assert.AreEqual(0,EmployeeReportsTo.GetSupervisors(listEmployees,listEmployees).Count);
			Assert.AreEqual(0,EmployeeReportsTo.GetSupervisors(new List<Employee>(),listEmployees).Count);
			Assert.AreEqual(0,EmployeeReportsTo.GetSupervisors(null,listEmployees).Count);
		}

		[TestMethod]
		public void Filter_DirectReportsOnly() {
			List<Employee> listEmployees=GetEmployees();
			CollectionAssert.AreEqual(new List<long> { 2,3 },EmployeeReportsTo.Filter(listEmployees,1).Select(x => x.EmployeeNum).ToList(),
				"Ann's direct reports: not Ann herself and not Dee, who reports to Bob.");
			CollectionAssert.AreEqual(new List<long> { 4 },EmployeeReportsTo.Filter(listEmployees,2).Select(x => x.EmployeeNum).ToList());
			Assert.AreEqual(listEmployees.Count,EmployeeReportsTo.Filter(listEmployees,0).Count,"All shows everyone.");
			Assert.AreEqual(0,EmployeeReportsTo.Filter(null,1).Count);
		}

		[TestMethod]
		public void IsShown_CombinesWithTheNameFilter() {
			//The time clock applies the name filter first, then this one; both must pass.
			List<Employee> listEmployees=GetEmployees();
			List<Employee> listShown=listEmployees
				.Where(x => x.FName.ToLower().StartsWith("c"))
				.Where(x => EmployeeReportsTo.IsShown(x,1))
				.ToList();
			CollectionAssert.AreEqual(new List<long> { 3 },listShown.Select(x => x.EmployeeNum).ToList());
			Assert.IsFalse(EmployeeReportsTo.IsShown(null,0));
		}

		[TestMethod]
		public void GetValidSelection_FallsBackToAll() {
			List<Employee> listSupervisors=EmployeeReportsTo.GetSupervisors(GetEmployees(),GetNonHidden());
			Assert.AreEqual(2,EmployeeReportsTo.GetValidSelection(2,listSupervisors));
			Assert.AreEqual(0,EmployeeReportsTo.GetValidSelection(7,listSupervisors),"A hidden supervisor falls back to All.");
			Assert.AreEqual(0,EmployeeReportsTo.GetValidSelection(4,listSupervisors),"Someone nobody reports to falls back to All.");
			Assert.AreEqual(0,EmployeeReportsTo.GetValidSelection(0,listSupervisors));
			Assert.AreEqual(0,EmployeeReportsTo.GetValidSelection(1,null));
		}

		[TestMethod]
		public void Remember_PerUser_ForTheSession() {
			//User numbers that no other test uses, because the remembered choices are static for the session.
			EmployeeReportsTo.Remember(9001,1);
			EmployeeReportsTo.Remember(9002,2);
			Assert.AreEqual(1,EmployeeReportsTo.GetRemembered(9001));
			Assert.AreEqual(2,EmployeeReportsTo.GetRemembered(9002));
			Assert.AreEqual(0,EmployeeReportsTo.GetRemembered(9003),"A user who never chose sees All.");
			EmployeeReportsTo.Remember(9001,0);
			Assert.AreEqual(0,EmployeeReportsTo.GetRemembered(9001),"Choosing All forgets the supervisor.");
			Assert.AreEqual(2,EmployeeReportsTo.GetRemembered(9002));
			EmployeeReportsTo.Remember(9002,0);
		}
	}
}
