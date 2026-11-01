// Database Maintenance check for duplicate blockouts (Open Dental 26.1, package M2-SHELL in docs/specs/port-backlog.md). It replaces the
// standalone Tools, Misc Tools, Clear Duplicate Blockouts window.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Reflection;

namespace OpenDentBusiness {
	public partial class DatabaseMaintenances {
		///<summary>Duplicate blockouts slow down the Appointments module; they usually come from combined operatories or data conversions. Two blockouts
		///are duplicates when they have the same date, start time, stop time, blockout type and note and are linked to the same operatory. Check counts
		///the extra operatory links; Fix removes them (the lowest link in each group stays, see Schedules.GetDuplicateBlockoutScheduleOpNums) and deletes a
		///blockout that this leaves with no operatory, and writes one Audit Trail entry (Setup) when it removed any. Blockouts that had no operatory
		///before the run are left alone. The fix deletes rows by primary key only, so it is safe with replication.</summary>
		[DbmMethodAttr]
		public static string SchedulesDeleteDuplicateBlockouts(bool verbose,DbmMode dbmMode) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetString(MethodBase.GetCurrentMethod(),verbose,dbmMode);
			}
			string log="";
			switch(dbmMode) {
				case DbmMode.Check:
					int numFound=Schedules.GetDuplicateBlockoutCount();
					if(numFound>0 || verbose) {
						log+=Lans.g("FormDatabaseMaintenance","Duplicate blockouts found:")+" "+numFound+"\r\n";
					}
					break;
				case DbmMode.Fix:
					int numFixed=Schedules.ClearDuplicates();
					if(numFixed>0) {
						//Audit Trail entry, as the removed Clear Duplicate Blockouts tool wrote and as every other blockout deletion does (M2-APPT).
						SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,"Database Maintenance removed "+numFixed+" duplicate blockout link(s).",LogSources.DBM);
					}
					if(numFixed>0 || verbose) {
						log+=Lans.g("FormDatabaseMaintenance","Duplicate blockouts removed:")+" "+numFixed+"\r\n";
					}
					break;
			}
			return log;
		}
	}
}
