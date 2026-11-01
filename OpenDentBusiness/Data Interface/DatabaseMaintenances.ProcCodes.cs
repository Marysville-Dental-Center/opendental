// Database Maintenance check ProcedurecodeCodeHasSeparator (Open Dental 26.1 procedure code rule; backlog package M2-CAN, feature
// v26_1-lists-proccode-no-hyphens).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Reflection;

namespace OpenDentBusiness {
	public partial class DatabaseMaintenances {
		///<summary>Procedure codes that contain a comma, a hyphen or a non-breaking hyphen (U+2011). Since 26.1 such codes can't be added, because code
		///groups and carrier preauthorization lists use those characters to separate codes and ranges. Codes added earlier are not changed
		///automatically: procedures, fees and claims refer to them, so the office renames them. Check and Fix only report them; Breakdown lists
		///them.</summary>
		[DbmMethodAttr(HasBreakDown=true)]
		public static string ProcedurecodeCodeHasSeparator(bool verbose,DbmMode dbmMode) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetString(MethodBase.GetCurrentMethod(),verbose,dbmMode);
			}
			//Every code, filtered in C# with the same rule as new codes (a few thousand rows; no character set questions in SQL).
			List<ProcedureCode> listProcedureCodes=ProcedureCodes.GetCodesWithSeparators(
				Crud.ProcedureCodeCrud.SelectMany("SELECT * FROM procedurecode ORDER BY ProcCode"));
			if(listProcedureCodes.Count==0 && !verbose) {
				return "";
			}
			string log=Lans.g("FormDatabaseMaintenance","Procedure codes that contain a comma or a hyphen:")+" "+listProcedureCodes.Count;
			switch(dbmMode) {
				case DbmMode.Check:
				case DbmMode.Fix:
					log+="\r\n";
					if(listProcedureCodes.Count>0) {
						log+="   "+Lans.g("FormDatabaseMaintenance","Manual fix needed.  Double click to see a break down.")+"\r\n";
					}
					break;
				case DbmMode.Breakdown:
					log+="\r\n";
					for(int i=0;i<listProcedureCodes.Count;i++) {
						log+="   "+listProcedureCodes[i].ProcCode+" - "+listProcedureCodes[i].Descript+"\r\n";
					}
					if(listProcedureCodes.Count>0) {
						log+=Lans.g("FormDatabaseMaintenance","   Code groups and carrier preauthorization lists can't refer to these codes. They need to be "
							+"fixed manually: add a replacement code without commas or hyphens and move the old code to a hidden category.")+"\r\n";
					}
					break;
			}
			return log;
		}
	}
}
