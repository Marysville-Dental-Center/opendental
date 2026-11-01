// EmployeeMissingSupervisor (Open Dental 25.3), on the scratch database. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace BDBMTests {
	[TestClass]
	public class EmployeeSupervisorDbTests {
		[TestInitialize]
		public void Init() {
			BDbmDb.Begin();
		}

		private static long ReportsTo(long employeeNum) {
			return long.Parse(DataCore.GetScalar("SELECT ReportsTo FROM employee WHERE EmployeeNum="+employeeNum));
		}

		private static void SetReportsTo(long employeeNum,long reportsTo) {
			DataCore.NonQ("UPDATE employee SET ReportsTo="+reportsTo+" WHERE EmployeeNum="+employeeNum);
		}

		[TestMethod]
		public void CheckCountsAndFixClearsOnlyMissingSupervisors() {
			DatabaseMaintenances.EmployeeMissingSupervisor(false,DbmMode.Fix);//Start clean.
			Employee employeeBoss=EmployeeT.CreateEmployee("BdbmBoss");
			Employee employeeGoodReport=EmployeeT.CreateEmployee("BdbmGood");
			Employee employeeNoSupervisor=EmployeeT.CreateEmployee("BdbmNone");
			Employee employeeOrphan1=EmployeeT.CreateEmployee("BdbmOrphanA");
			Employee employeeOrphan2=EmployeeT.CreateEmployee("BdbmOrphanB");
			Employee employeeGone=EmployeeT.CreateEmployee("BdbmGone");
			SetReportsTo(employeeGoodReport.EmployeeNum,employeeBoss.EmployeeNum);
			SetReportsTo(employeeNoSupervisor.EmployeeNum,0);
			SetReportsTo(employeeOrphan1.EmployeeNum,employeeGone.EmployeeNum);
			SetReportsTo(employeeOrphan2.EmployeeNum,-5);
			DataCore.NonQ("DELETE FROM employee WHERE EmployeeNum="+employeeGone.EmployeeNum);
			Assert.AreEqual("Employees with an invalid supervisor: 2\r\n",DatabaseMaintenances.EmployeeMissingSupervisor(false,DbmMode.Check));
			Assert.AreEqual(employeeGone.EmployeeNum,ReportsTo(employeeOrphan1.EmployeeNum),"Check changes nothing.");
			Assert.AreEqual("Employees with an invalid supervisor set to no supervisor: 2\r\n",DatabaseMaintenances.EmployeeMissingSupervisor(false,DbmMode.Fix));
			Assert.AreEqual(0,ReportsTo(employeeOrphan1.EmployeeNum));
			Assert.AreEqual(0,ReportsTo(employeeOrphan2.EmployeeNum));
			Assert.AreEqual(employeeBoss.EmployeeNum,ReportsTo(employeeGoodReport.EmployeeNum));
			Assert.AreEqual(0,ReportsTo(employeeNoSupervisor.EmployeeNum));
			Assert.AreEqual("",DatabaseMaintenances.EmployeeMissingSupervisor(false,DbmMode.Check));
			Assert.AreEqual("Employees with an invalid supervisor: 0\r\n",DatabaseMaintenances.EmployeeMissingSupervisor(true,DbmMode.Check));
		}

		[TestMethod]
		public void IsARegularCheck() {
			MethodInfo methodInfo=typeof(DatabaseMaintenances).GetMethod(nameof(DatabaseMaintenances.EmployeeMissingSupervisor));
			Assert.IsTrue(DatabaseMaintenances.GetMethodsForDisplay().Contains(methodInfo));
			Assert.IsFalse(DatabaseMaintenances.MethodHasBreakDown(methodInfo));
		}
	}
}
