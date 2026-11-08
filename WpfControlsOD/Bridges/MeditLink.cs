using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace OpenPmsDental.Bridges {
	///<summary></summary>
	public class MeditLink {

		///<summary></summary>
		public MeditLink(){

		}

		///<summary>Starts Medit Link with Medit's command id and, for a patient, the Command line with the patient filled in. The arguments and the
		///Partner ID (next/vendorpub-1: Medit's public article, a PartnerID key that assigns the office's lab to every case created from the Chart)
		///are built by MeditLinkPartner; a Partner ID that can't be added is refused with a message and nothing is started.</summary>
		public static void SendData(Program program,Patient patient) {
			//Purposefully surround all command... argument identifiers sent to MeditLink in double quotes.
			string commandArgs=MeditLinkPartner.GetArguments(program,patient,out string problem);
			if(commandArgs==null) {
				MessageBox.Show(problem);
				return;
			}
			string path=Programs.GetProgramPath(program);
			try {
				Process.Start(path,commandArgs);
			}
			catch(Exception ex) {
				MessageBox.Show(ex.Message);
			}
			return;
		}
	}
}
