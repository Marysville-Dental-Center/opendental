// P262-LINKS (Open Dental 26.2, item 45): the OrthoComposite program link button. Starts OrthoComposite with the selected patient's name and
// birthdate; the rules are in OpenPmsDentalBusiness/VendorDirect/ProgramLinks/OrthoCompositeLink.cs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace OpenPmsDental.Bridges {
	///<summary>OrthoComposite. The arguments come from the Patient arguments property, which the office fills in from OrthoComposite's instructions;
	///only [FName], [LName] and [Birthdate] are filled in.</summary>
	public class OrthoComposite {
		public static void SendData(Program program,Patient patient) {
			BridgeLaunch bridgeLaunch=OrthoCompositeLink.GetLaunch(program,patient);
			if(!bridgeLaunch.IsReady) {
				string message=Lans.g("OrthoComposite",BridgeLaunch.Describe(bridgeLaunch.Problem));
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
				FriendlyException.Show(Lans.g("OrthoComposite","OrthoComposite could not be started from this path:")+"\r\n"+bridgeLaunch.FileName,ex);
			}
		}
	}
}
