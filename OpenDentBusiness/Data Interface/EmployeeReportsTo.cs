using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDentBusiness {
	///<summary>The Reports To filter of the Manage module's employee time clock (Open Dental 25.3). Choosing a supervisor shows only the
	///employees whose employee.ReportsTo is that supervisor (direct reports). It works together with the clinic and name filters, which pick the
	///employees passed in here.</summary>
	public class EmployeeReportsTo {
		///<summary>The supervisor chosen per user for this session (UserNum to EmployeeNum). 0 or no entry means All.</summary>
		private static Dictionary<long,long> _dictSupervisorByUserNum=new Dictionary<long,long>();
		private static object _lockSupervisorByUserNum=new object();

		///<summary>The supervisors to offer, sorted by name: every non-hidden employee that at least one of the passed in employees reports to.
		///listEmployeesShown is the time clock's list after the clinic filter. listEmployeesNonHidden is where supervisors are looked up; pass
		///null to use the employee cache (non-hidden employees only). A supervisor who is hidden or missing, and an employee who reports to
		///themselves, are left out.</summary>
		public static List<Employee> GetSupervisors(List<Employee> listEmployeesShown,List<Employee> listEmployeesNonHidden=null) {
			Meth.NoCheckMiddleTierRole();
			if(listEmployeesShown==null || listEmployeesShown.Count==0) {
				return new List<Employee>();
			}
			if(listEmployeesNonHidden==null) {
				listEmployeesNonHidden=Employees.GetDeepCopy(isShort:true);
			}
			List<long> listSupervisorNums=listEmployeesShown
				.Where(x => x.ReportsTo>0 && x.ReportsTo!=x.EmployeeNum)
				.Select(x => x.ReportsTo)
				.Distinct()
				.ToList();
			return listEmployeesNonHidden
				.Where(x => !x.IsHidden && listSupervisorNums.Contains(x.EmployeeNum))
				.GroupBy(x => x.EmployeeNum)
				.Select(x => x.First())
				.OrderBy(x => x.FName,StringComparer.CurrentCultureIgnoreCase)
				.ThenBy(x => x.LName,StringComparer.CurrentCultureIgnoreCase)
				.ThenBy(x => x.EmployeeNum)
				.ToList();
		}

		///<summary>True if the employee passes the filter: always for All (supervisorEmployeeNum 0), otherwise only when the employee reports
		///directly to that supervisor. The supervisor is not listed under themselves.</summary>
		public static bool IsShown(Employee employee,long supervisorEmployeeNum) {
			Meth.NoCheckMiddleTierRole();
			if(employee==null) {
				return false;
			}
			if(supervisorEmployeeNum<=0) {
				return true;
			}
			return employee.ReportsTo==supervisorEmployeeNum;
		}

		///<summary>The employees that pass the filter, in the order passed in.</summary>
		public static List<Employee> Filter(List<Employee> listEmployees,long supervisorEmployeeNum) {
			Meth.NoCheckMiddleTierRole();
			if(listEmployees==null) {
				return new List<Employee>();
			}
			return listEmployees.Where(x => IsShown(x,supervisorEmployeeNum)).ToList();
		}

		///<summary>The supervisor to select: the requested one when it is still offered, else 0 (All). This is how a supervisor who was hidden,
		///deleted or no longer has reports in the list falls back to All.</summary>
		public static long GetValidSelection(long supervisorEmployeeNum,List<Employee> listSupervisors) {
			Meth.NoCheckMiddleTierRole();
			if(supervisorEmployeeNum<=0 || listSupervisors==null || !listSupervisors.Any(x => x.EmployeeNum==supervisorEmployeeNum)) {
				return 0;
			}
			return supervisorEmployeeNum;
		}

		///<summary>The supervisor this user chose earlier in this session, or 0 (All). Not saved to the database.</summary>
		public static long GetRemembered(long userNum) {
			Meth.NoCheckMiddleTierRole();
			lock(_lockSupervisorByUserNum) {
				if(_dictSupervisorByUserNum.TryGetValue(userNum,out long supervisorEmployeeNum)) {
					return supervisorEmployeeNum;
				}
			}
			return 0;
		}

		///<summary>Remembers the user's choice for the rest of this session. 0 means All.</summary>
		public static void Remember(long userNum,long supervisorEmployeeNum) {
			Meth.NoCheckMiddleTierRole();
			lock(_lockSupervisorByUserNum) {
				if(supervisorEmployeeNum<=0) {
					_dictSupervisorByUserNum.Remove(userNum);
					return;
				}
				_dictSupervisorByUserNum[userNum]=supervisorEmployeeNum;
			}
		}
	}
}
