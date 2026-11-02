using CodeBase;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Windows.Forms;

namespace ServiceManager {
	public partial class FormMain:Form {
		public FormMain() {
			InitializeComponent();
		}

		private void FormMain_Load(object sender,EventArgs e) {
			FillList(isLoad:true);
		}

		private void FillList(bool isLoad=false) {
			listMain.Items.Clear();
			//This program's services and Open Dental's (ProgramNames.LegacyServicePrefix), which are shown as Open Dental's.
			List<ServiceController> listServicesAll=ServicesHelper.GetAllOpenDentServices();
			try {
				listServicesAll.AddRange(ServicesHelper.GetServicesByExe("mysqld.exe"));
			}
			catch(Exception ex) {
				ex.DoNothing();
				if(isLoad) {
					MessageBox.Show("Error loading 'mysqld.exe' Window services.\r\n"+ex.Message);
				}
			}
			listServicesAll.ForEach(x => listMain.Items.Add(new ServiceListItem(x.ServiceName)));
		}

		private void listMain_DoubleClick(object sender,EventArgs e) {
			if(listMain.SelectedIndex==-1) {
				return;
			}
			using FormServiceManage FormS=new FormServiceManage(((ServiceListItem)listMain.SelectedItem).ServiceName,false,false);
			FormS.ShowDialog();
			FillList();
		}

		private void butAdd_Click(object sender,EventArgs e) {
			using FormServiceManage FormS=new FormServiceManage(CodeBase.ProgramNames.ServicePrefix,false,true);
			FormS.ShowDialog();
			FillList();
		}

		///<summary>A row of the list: the service name, followed by "(Open Dental)" for a service whose name starts with
		///ProgramNames.LegacyServicePrefix (ProgramNames.IsLegacyServiceName). The program never starts those.</summary>
		private class ServiceListItem {
			public string ServiceName;

			public ServiceListItem(string serviceName) {
				ServiceName=serviceName;
			}

			public override string ToString() {
				return GetDisplayText(ServiceName);
			}
		}

		///<summary>The text ServiceManager's list shows for a service: its name, followed by " (Open Dental)" when the name starts with
		///ProgramNames.LegacyServicePrefix.</summary>
		private static string GetDisplayText(string serviceName) {
			if(ProgramNames.IsLegacyServiceName(serviceName)) {
				return serviceName+" (Open Dental)";
			}
			return serviceName;
		}

	}
}
