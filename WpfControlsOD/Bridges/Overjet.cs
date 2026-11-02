// V-BRIDGE1: the Overjet program link button (Open Dental 25.1). Opens the vendor's web app in the default browser; the rules are in
// OpenPmsDentalBusiness/VendorDirect/ProgramLinks/OverjetLink.cs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace OpenPmsDental.Bridges {
	///<summary>Overjet program link. With a patient selected, and the Patient URL parameters property set up, the address carries the patient's
	///values; otherwise the vendor's sign-in page opens.</summary>
	public class Overjet {
		public static void SendData(Program program,Patient patient) {
			BridgeLaunch bridgeLaunch=OverjetLink.GetLaunch(program,patient);
			if(!bridgeLaunch.IsReady) {
				MsgBox.Show(Lans.g("Overjet",BridgeLaunch.Describe(bridgeLaunch.Problem)));
				return;
			}
			try {
				ODFileUtils.ProcessStart(bridgeLaunch.FileName);
			}
			catch(Exception ex) {
				FriendlyException.Show(Lans.g("Overjet","The web browser could not open the Overjet address."),ex);
			}
		}
	}
}
