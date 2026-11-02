// V-BRIDGE1: the DTX Studio and DTX Studio Acquire program link buttons (Open Dental 25.2). Start DTXsync.exe with the selected patient; the
// rules are in OpenPmsDentalBusiness/VendorDirect/ProgramLinks/DtxStudioLink.cs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace OpenPmsDental.Bridges {
	///<summary>DTX Studio and DTX Studio Acquire program links. Both need a patient. The arguments come from the Patient arguments property, which
	///the office fills in from the vendor's integration guide.</summary>
	public class DTXStudio {
		public static void SendData(Program program,Patient patient) {
			BridgeLaunch bridgeLaunch=DtxStudioLink.GetLaunch(program,patient);
			if(!bridgeLaunch.IsReady) {
				string message=Lans.g("DTXStudio",BridgeLaunch.Describe(bridgeLaunch.Problem));
				if(bridgeLaunch.Problem==BridgeLaunchProblem.ProgramNotFound) {
					message+="\r\n"+bridgeLaunch.FileName;
				}
				MsgBox.Show(message);
				return;
			}
			try {
				ODFileUtils.ProcessStart(bridgeLaunch.FileName,bridgeLaunch.Arguments);
			}
			catch(Exception ex) {
				FriendlyException.Show(Lans.g("DTXStudio","DTX Studio could not be started from this path:")+"\r\n"+bridgeLaunch.FileName,ex);
			}
		}
	}
}
