using System;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental.Bridges{
	public class RapidCall {

		///<summary></summary>
		public RapidCall(){
			
		}

		///<summary></summary>
		public static void SendData(Program ProgramCur) {
			string path=Programs.GetProgramPath(ProgramCur);
			try {
				ODFileUtils.ProcessStart(path,ProgramCur.CommandLine);
			}
			catch(Exception ex) {
				MessageBox.Show(ex.Message);
			}
		}

		///<summary>Launches Rapid Call from the Appointments toolbar. Does nothing unless the Rapid Call program link is enabled.</summary>
		public static void Launch() {
			if(!Programs.IsEnabled(ProgramName.RapidCall)) {
				return;
			}
			SendData(Programs.GetCur(ProgramName.RapidCall));
		}

		///<summary>Removes semicolons and spaces.</summary>
		private static string Tidy(string input) {
			string retVal=input.Replace(";","");//get rid of any semicolons.
			retVal=retVal.Replace(" ","");
			return retVal;
		}

	}
}







