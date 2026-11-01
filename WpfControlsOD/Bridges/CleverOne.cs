// V-BRIDGE2 (Open Dental 25.4, v25_4-bridges-cleverone): launches Clever One by VATECH for the selected patient.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect.ProgramLinks;

namespace OpenDental.Bridges{
	///<summary>Clever One by VATECH. The hand-off is the EzDent-i one (linkage.xml next to the program, then start it), without the Social Security
	///number. VATECH has not published how Clever One takes a patient, so this is the fork's choice (CleverOneProgram).</summary>
	public class CleverOne{

		///<summary>Writes linkage.xml for the patient and starts Clever One. Without a patient, any old linkage.xml is removed and Clever One starts
		///without one.</summary>
		public static void SendData(Program ProgramCur,Patient pat){
			string path=Programs.GetProgramPath(ProgramCur);
			if(!File.Exists(path)) {
				MessageBox.Show(path+" could not be found.");
				return;
			}
			string linkage=CleverOneProgram.GetLinkageFilePath(path);
			if(File.Exists(linkage)){
				try {
					File.Delete(linkage);
				}
				catch(Exception ex) {
					ex.DoNothing();
				}
			}
			if(pat!=null) {
				bool useChartNumber=CleverOneProgram.UsesChartNumber(ProgramCur);
				string xml=CleverOneProgram.BuildLinkageXml(pat,useChartNumber);
				try {
					ODFileUtils.WriteAllText(linkage,xml);
				}
				catch(Exception ex) {
					//Clever One could not be given the patient (a read-only program folder, for example). Say so, then start it without one.
					MessageBox.Show(Lans.g("CleverOne","The patient could not be sent to Clever One:")+" "+ex.Message);
				}
			}
			try {
				//whether there is a patient or not.
				ODFileUtils.ProcessStart(path);
			}
			catch {
				MessageBox.Show(Lans.g("CleverOne","Error launching")+" "+path);
			}
		}
	}
}
