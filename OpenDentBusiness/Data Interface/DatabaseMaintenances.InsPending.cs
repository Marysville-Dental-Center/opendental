using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenDentBusiness {
	public partial class DatabaseMaintenances {
		///<summary>Pending insurance (inspending, Open Dental 25.2) whose patient or subscriber is missing or marked deleted can never be converted. Check
		///counts them; Fix deletes them and logs each one against the patient.</summary>
		[DbmMethodAttr(IsReplicationUnsafe=true)]
		public static string InsPendingWithInvalidPatient(bool verbose,DbmMode dbmMode) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetString(MethodBase.GetCurrentMethod(),verbose,dbmMode);
			}
			string log="";
			List<InsPending> listInsPendings=InsPendings.GetOrphans();
			switch(dbmMode) {
				case DbmMode.Check:
					if(listInsPendings.Count>0 || verbose) {
						log+=Lans.g("FormDatabaseMaintenance","Pending insurance found with a missing or deleted patient or subscriber: ")
							+listInsPendings.Count+"\r\n";
					}
					break;
				case DbmMode.Fix:
					List<DbmLog> listDbmLogs=new List<DbmLog>();
					string methodName=MethodBase.GetCurrentMethod().Name;
					for(int i=0;i<listInsPendings.Count;i++) {
						InsPendings.Delete(listInsPendings[i].InsPendingNum);
						listDbmLogs.Add(new DbmLog(Security.CurUser.UserNum,listInsPendings[i].PatNum,DbmLogFKeyType.Patient,DbmLogActionType.Delete,
							methodName,"Deleted pending insurance #"+listInsPendings[i].InsPendingNum+" ("+listInsPendings[i].CarrierName
							+") from InsPendingWithInvalidPatient."));
					}
					if(listInsPendings.Count>0 || verbose) {
						log+=Lans.g("FormDatabaseMaintenance","Pending insurance with a missing or deleted patient or subscriber deleted: ")
							+listInsPendings.Count+"\r\n";
						Crud.DbmLogCrud.InsertMany(listDbmLogs);
					}
					break;
			}
			return log;
		}
	}
}
