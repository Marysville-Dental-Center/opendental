using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenPmsDentalBusiness {
	public partial class DatabaseMaintenances {
		///<summary>Open Dental 26.1 check (v26_1-tools-dbm-missing-allergydefs). Patient allergies whose allergy definition no longer exists show no
		///name anywhere. Check counts them. Fix finds or creates the visible definition UNKNOWN ALLERGY, points them at it, so the patient keeps the
		///allergy and the office sees it for review, and writes one DBM log entry per allergy against its patient. Modeled on
		///DiseaseWithInvalidDiseaseDef. Replication unsafe because Fix may insert an allergy definition.</summary>
		[DbmMethodAttr(IsReplicationUnsafe=true)]
		public static string AllergyWithInvalidAllergyDef(bool verbose,DbmMode dbmMode) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetString(MethodBase.GetCurrentMethod(),verbose,dbmMode);
			}
			string log="";
			List<Allergy> listAllergies=Allergies.GetWithInvalidAllergyDef();
			int numFound=listAllergies.Count;
			switch(dbmMode) {
				case DbmMode.Check:
					if(numFound>0 || verbose) {
						log+=Lans.g("FormDatabaseMaintenance","Allergies with a missing allergy definition found")+": "+numFound+"\r\n";
					}
					break;
				case DbmMode.Fix:
					if(numFound>0 || verbose) {
						log+=Lans.g("FormDatabaseMaintenance","Allergies with a missing allergy definition found")+": "+numFound+"\r\n";
					}
					if(numFound==0) {
						break;
					}
					long allergyDefNumUnknown=AllergyDefs.GetOrInsertUnknown();
					long numFixed=Allergies.SetAllergyDefNum(listAllergies.Select(x => x.AllergyNum).ToList(),allergyDefNumUnknown);
					string methodName=MethodBase.GetCurrentMethod().Name;
					long userNum=Security.CurUser?.UserNum??0;
					List<DbmLog> listDbmLogs=new List<DbmLog>();
					for(int i=0;i<listAllergies.Count;i++) {
						listDbmLogs.Add(new DbmLog(userNum,listAllergies[i].PatNum,DbmLogFKeyType.Patient,DbmLogActionType.Update,methodName,
							"Allergy #"+listAllergies[i].AllergyNum+" pointed to missing allergy definition #"+listAllergies[i].AllergyDefNum
							+"; attached to "+AllergyDefs.DESCRIPTION_UNKNOWN+" (#"+allergyDefNumUnknown+") from AllergyWithInvalidAllergyDef."));
					}
					Crud.DbmLogCrud.InsertMany(listDbmLogs);
					log+=Lans.g("FormDatabaseMaintenance","Allergies attached to the allergy called")+" "+AllergyDefs.DESCRIPTION_UNKNOWN+": "+numFixed
						+"\r\n";
					break;
			}
			return log;
		}
	}
}
