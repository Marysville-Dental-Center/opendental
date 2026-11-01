using System;
using System.Collections.Generic;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>The Reports To filter of the employee time clock (Open Dental 25.3). The rules are in OpenDentBusiness.EmployeeReportsTo.</summary>
	public partial class ControlManage {
		///<summary>Fills the Reports To filter from the time clock's employees (after the clinic filter) and returns the supervisor to filter on, 0
		///for All. The user's choice is kept for the session. A supervisor who is no longer offered (hidden, deleted, or with no reports left in
		///the list) falls back to All.</summary>
		private long FillComboReportsTo() {
			labelReportsTo.Text=Lan.g(this,"Reports To");
			List<Employee> listSupervisors=EmployeeReportsTo.GetSupervisors(_listEmployees);
			long userNum=GetUserNumForReportsTo();
			long supervisorEmployeeNum=EmployeeReportsTo.GetValidSelection(EmployeeReportsTo.GetRemembered(userNum),listSupervisors);
			if(supervisorEmployeeNum==0) {
				EmployeeReportsTo.Remember(userNum,0);
			}
			comboReportsTo.Items.Clear();
			comboReportsTo.Items.Add(Lan.g(this,"All"),null);
			comboReportsTo.Items.AddList(listSupervisors,x => Employees.GetNameFL(x));
			comboReportsTo.SelectedIndex=listSupervisors.FindIndex(x => x.EmployeeNum==supervisorEmployeeNum)+1;//0 is All
			return supervisorEmployeeNum;
		}

		private long GetUserNumForReportsTo() {
			if(Security.CurUser==null) {
				return 0;
			}
			return Security.CurUser.UserNum;
		}

		private void comboReportsTo_SelectionChangeCommitted(object sender,EventArgs e) {
			Employee employeeSupervisor=comboReportsTo.GetSelected<Employee>();//null for All
			long supervisorEmployeeNum=0;
			if(employeeSupervisor!=null) {
				supervisorEmployeeNum=employeeSupervisor.EmployeeNum;
			}
			EmployeeReportsTo.Remember(GetUserNumForReportsTo(),supervisorEmployeeNum);
			FillEmps(false);
		}
	}
}
