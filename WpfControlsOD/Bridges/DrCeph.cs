#if !DISABLE_WINDOWS_BRIDGES
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using OpenDentBusiness;
using CodeBase;
using Newtonsoft.Json;
using System.Windows.Forms;

namespace OpenDental.Bridges{
	/// <summary></summary>
	public class DrCeph{
		
		/// <summary></summary>
		public DrCeph(){
			
		}

		///<summary>Open Dental started Dr. Ceph, asked for the extra fields in FormDrCeph and sent everything through its closed VBbridges.dll, which
		///this program does not ship (removed 2026-11-01). Dr. Ceph publishes no interface to rebuild that from, so this says the action is not
		///available before asking the user for anything. FormDrCeph and DrCephArgs are kept for when the interface is known.</summary>
		public static void SendData(Program ProgramCur, Patient pat){
			if(pat==null){
				MessageBox.Show("Please select a patient first.");
				return;
			}
			MsgBox.Show("DrCeph",BridgeNotAvailable.DrCeph);
		}

		

	}
}
#endif