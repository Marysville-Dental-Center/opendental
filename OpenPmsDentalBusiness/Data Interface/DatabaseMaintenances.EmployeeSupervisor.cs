// Database Maintenance check EmployeeMissingSupervisor (Open Dental 25.3, backlog package B-DBM).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Reflection;

namespace OpenPmsDentalBusiness {
	public partial class DatabaseMaintenances {
		///<summary>Employees whose Reports To (employee.ReportsTo) names an employee that no longer exists. ReportsTo=0 means "no supervisor" and is
		///valid. Fix sets ReportsTo to 0 for them.</summary>
		[DbmMethodAttr]
		public static string EmployeeMissingSupervisor(bool verbose,DbmMode dbmMode) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetString(MethodBase.GetCurrentMethod(),verbose,dbmMode);
			}
			//A self join, because MySQL can't update a table that a subquery of the same statement reads.
			string command="SELECT employee.EmployeeNum FROM employee "
				+"LEFT JOIN employee supervisor ON supervisor.EmployeeNum=employee.ReportsTo "
				+"WHERE employee.ReportsTo!=0 AND supervisor.EmployeeNum IS NULL";
			List<long> listEmployeeNums=Db.GetListLong(command);
			string log="";
			switch(dbmMode) {
				case DbmMode.Check:
					if(listEmployeeNums.Count>0 || verbose) {
						log+=Lans.g("FormDatabaseMaintenance","Employees with an invalid supervisor:")+" "+listEmployeeNums.Count+"\r\n";
					}
					break;
				case DbmMode.Fix:
					long countFixed=0;
					if(listEmployeeNums.Count>0) {
						command="UPDATE employee SET ReportsTo=0 WHERE ReportsTo!=0 AND EmployeeNum IN("+string.Join(",",listEmployeeNums)+")";
						countFixed=Db.NonQ(command);
					}
					if(countFixed>0 || verbose) {
						log+=Lans.g("FormDatabaseMaintenance","Employees with an invalid supervisor set to no supervisor:")+" "+countFixed+"\r\n";
					}
					break;
			}
			return log;
		}
	}
}
