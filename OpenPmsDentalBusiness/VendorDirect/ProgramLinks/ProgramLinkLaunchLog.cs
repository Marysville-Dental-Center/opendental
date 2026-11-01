// V-BRIDGE2: the fork's local log of program-link launches that open a patient in another vendor's system (first used by the Clerri link).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase;

namespace OpenPmsDentalBusiness.VendorDirect.ProgramLinks {
	///<summary>Writes one line per launch to the workstation's log folder (Logger, subfolder ProgramLinks): time (from Logger), program, user, clinic and
	///PatNum. Only numbers and the program name; no patient name or other patient data. Never throws: a full disk or a locked file never stops a
	///launch.</summary>
	public static class ProgramLinkLaunchLog {
		///<summary>The Logger subfolder.</summary>
		public const string SubDirectory="ProgramLinks";

		///<summary>The log line, for example "Launch Clerri ProgramNum=12 UserNum=3 ClinicNum=0 PatNum=1042".</summary>
		public static string FormatLine(string progName,long programNum,long userNum,long clinicNum,long patNum) {
			Meth.NoCheckMiddleTierRole();
			return "Launch "+(progName??"")+" ProgramNum="+POut.Long(programNum)+" UserNum="+POut.Long(userNum)+" ClinicNum="+POut.Long(clinicNum)
				+" PatNum="+POut.Long(patNum);
		}

		///<summary>Writes the launch line for the current user. patNum is 0 when no patient is selected.</summary>
		public static void Write(Program program,long clinicNum,long patNum) {
			Meth.NoCheckMiddleTierRole();
			try {
				long userNum=Security.CurUser?.UserNum??0;
				Logger.WriteLine(FormatLine(program?.ProgName,program?.ProgramNum??0,userNum,clinicNum,patNum),SubDirectory);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}
	}
}
