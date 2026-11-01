// V-BRIDGE2 (Open Dental 25.4, v25_4-family-clerri): the Family module Clerri button. Opens the office's Clerri dashboard in the default browser.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Diagnostics;
using CodeBase;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace OpenPmsDental.Bridges{
	///<summary>Clerri. Launch only: no patient data is sent except the PatNum, and only when the dashboard address the office entered asks for it
	///([PatNum]). No data sync (Open Dental's runs through its eConnector, which this fork removed).</summary>
	public class Clerri{

		///<summary>Opens the dashboard for the current clinic, with the patient's PatNum when one is selected and the address asks for it. The launch is
		///written to the local program-link log.</summary>
		public static void SendData(Program ProgramCur,Patient pat){
			if(!ProgramCur.Enabled) {
				MsgBox.Show("Clerri","Clerri must be enabled in Program Links.");
				return;
			}
			long clinicNum=Clinics.ClinicNum;
			long patNum=(pat==null) ? 0 : pat.PatNum;
			string error;
			string url=ClerriProgram.GetDashboardUrl(ProgramCur.ProgramNum,clinicNum,patNum,out error);
			if(url==null) {
				MsgBox.Show(Lans.g("Clerri",error)+"\r\n"+Lans.g("Clerri","Check Setup, Program Links, Clerri."));
				return;
			}
			ProgramLinkLaunchLog.Write(ProgramCur,clinicNum,patNum);
			try {
				//An https address only (checked above), so this opens the default browser and never starts a program.
				ProcessStartInfo processStartInfo=new ProcessStartInfo(url);
				processStartInfo.UseShellExecute=true;
				Process.Start(processStartInfo);
			}
			catch(Exception ex) {
				FriendlyException.Show(Lans.g("Clerri","The Clerri dashboard could not be opened."),ex);
			}
		}
	}
}
