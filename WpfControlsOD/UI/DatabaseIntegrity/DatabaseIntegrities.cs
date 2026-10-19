using System;
using System.Collections.Generic;
using System.Linq;
using OpenDentBusiness;

namespace OpenDental {
	///<summary>Local Database Integrity policy. Open Dental used to download this policy from its HQ (which plugins may load, which modules pop up warnings, and the text and style of the warning triangles). That policy no longer exists: plugins chosen by the office always load (PluginLoader no longer asks), modules never pop up, and an invalid SecurityHash shows a static triangle with a neutral local explanation. The DatabaseIntegritiesWhiteList preference row is kept for schema compatibility but is no longer read.</summary>
	public class DatabaseIntegrities {
		///<summary>List used to keep track of which patients and modules have been selected in a session. Will contain, at most, one element per patient.</summary>
		private static List<PatientModule> _listPatientModules=new List<PatientModule>() { };

		///<summary>Shown when a record's SecurityHash does not match. [Class] is replaced with the record type.</summary>
		private const string CLASS_MESSAGE="This [Class] record was changed outside of this program, or before tamper detection was turned on, "
			+"so its tamper-detection hash does not match.\r\n\r\n"
			+"Review the record. If the change is expected, a user with Security Admin permission can recalculate the hashes with "
			+"Database Maintenance, Tools tab, Security Hashes.";

		///<summary>Returns the local behavior for an invalid SecurityHash on the passed in record type: a static triangle with a neutral message.</summary>
		public static DatabaseIntegrity GetOneClass(EnumWarningIntegrityType warningIntegrityType){
			DatabaseIntegrity databaseIntegrity=new DatabaseIntegrity();
			databaseIntegrity.WarningIntegrityType=warningIntegrityType;
			databaseIntegrity.Behavior=EnumIntegrityBehavior.Triangle;
			databaseIntegrity.Message=Lans.g("DatabaseIntegrities",CLASS_MESSAGE);
			return databaseIntegrity;
		}

		///<summary>Always null. Modules never pop up Database Integrity messages.</summary>
		public static DatabaseIntegrity GetModule() {
			return null;
		}

		///<summary>Always false. Modules never pop up Database Integrity messages.</summary>
		public static bool DoShowPopup(long patNum,EnumModuleType moduleType) {
			return false;
		}

		///<summary>Updates _listPatientModule cache to include the passed in patient and module. Creates a new object when new patient is first selected. Updates existing object with each additonal module selected.</summary>
		public static void AddPatientModuleToCache(long patNum,EnumModuleType moduleType) {
			PatientModule patientModule=_listPatientModules.Find(x => x.PatNum==patNum);
			if(patientModule is null) {
				patientModule=new PatientModule();
				patientModule.PatNum=patNum;
				_listPatientModules.Add(patientModule);
			}
			if(patientModule.ListModuleTypes.Contains(moduleType)) {//already cached for patient
				return;
			}
			patientModule.ListModuleTypes.Add(moduleType);
		}

	}
}
