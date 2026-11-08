// P262-LINKS (Open Dental 26.2, item 44): the Allied Star Connect program link button. Writes the selected patient to the .json info file, then
// starts Connect; the rules are in OpenPmsDentalBusiness/VendorDirect/ProgramLinks/AlliedstarConnectLink.cs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text;
using CodeBase;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.ProgramLinks;

namespace OpenPmsDental.Bridges {
	///<summary>Allied Star Connect. With a patient, the info file is written first; without one, an old info file is removed first, so Connect can
	///never open the previous patient. If either fails, Connect is not started.</summary>
	public class AlliedstarConnect {
		public static void SendData(Program program,Patient patient) {
			BridgeFileLaunch bridgeFileLaunch=AlliedstarConnectLink.GetLaunch(program,patient);
			if(!bridgeFileLaunch.IsReady) {
				string message=Lans.g("AlliedstarConnect",BridgeLaunch.Describe(bridgeFileLaunch.Problem));
				if(bridgeFileLaunch.Problem==BridgeLaunchProblem.ProgramNotFound) {
					message+="\r\n"+bridgeFileLaunch.FileName;
				}
				else if(bridgeFileLaunch.Problem==BridgeLaunchProblem.InfoFolderMissing) {
					message+="\r\n"+Path.GetDirectoryName(bridgeFileLaunch.HandOffFilePath);
				}
				else if(bridgeFileLaunch.Problem==BridgeLaunchProblem.InfoFilePathInvalid) {
					message+="\r\n"+bridgeFileLaunch.HandOffFilePath;
				}
				MsgBox.Show(message);
				return;
			}
			try {
				if(bridgeFileLaunch.HandOffText==null) {
					if(File.Exists(bridgeFileLaunch.HandOffFilePath)) {
						File.Delete(bridgeFileLaunch.HandOffFilePath);
					}
				}
				else {
					File.WriteAllText(bridgeFileLaunch.HandOffFilePath,bridgeFileLaunch.HandOffText,new UTF8Encoding(false));
				}
			}
			catch(Exception ex) {
				string text=bridgeFileLaunch.HandOffText==null
					? "The info file of the previous patient could not be removed, so Connect was not started:"
					: "The patient could not be written to the info file, so Connect was not started:";
				FriendlyException.Show(Lans.g("AlliedstarConnect",text)+"\r\n"+bridgeFileLaunch.HandOffFilePath,ex);
				return;
			}
			try {
				ODFileUtils.ProcessStart(bridgeFileLaunch.FileName,bridgeFileLaunch.Arguments);
			}
			catch(Exception ex) {
				FriendlyException.Show(Lans.g("AlliedstarConnect","Connect could not be started from this path:")+"\r\n"+bridgeFileLaunch.FileName,ex);
			}
		}
	}
}
