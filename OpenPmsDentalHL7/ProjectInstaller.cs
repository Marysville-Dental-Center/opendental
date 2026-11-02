using System;
using System.Collections;
using System.Configuration.Install;
using System.ServiceProcess;
using System.ComponentModel;
using System.Windows.Forms;

namespace OpenPmsDentalHL7 {
	///<summary>The installer InstallUtil.exe runs for this service. InstallUtil finds it by the RunInstaller attribute on a public Installer
	///type, whatever its namespace. /ServiceName=&lt;name&gt; installs the service under another name.</summary>
	[RunInstallerAttribute(true)]
	public class MyProjectInstaller:Installer {
		private ServiceInstaller serviceInstaller1;
		private ServiceProcessInstaller processInstaller;

		public MyProjectInstaller() {
			processInstaller = new ServiceProcessInstaller();
			serviceInstaller1 = new ServiceInstaller();
			processInstaller.Account = ServiceAccount.LocalSystem;
			serviceInstaller1.StartType = ServiceStartMode.Automatic;
			serviceInstaller1.ServiceName=CodeBase.ProgramNames.HL7ServiceName;
			string[] args=Environment.GetCommandLineArgs();
			for(int i=0;i<args.Length;i++) {
				if(args[i].StartsWith("/ServiceName")) {
					serviceInstaller1.ServiceName=args[i].Substring(13);
				}
			}
			Installers.Add(serviceInstaller1);
			Installers.Add(processInstaller);
		}
	}
}
