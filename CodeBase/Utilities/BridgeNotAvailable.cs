using System;

namespace CodeBase {
	///<summary>Messages for the vendor bridge actions that Open Dental ran through its closed VBbridges.dll helper, which this program does not ship
	///(removed 2026-11-01). The helper's behaviour was never published, and the vendors (FYI Technologies for Dr. Ceph, eClinicalWorks, Patterson,
	///Schick for CDR version 4) publish no interface it could be rebuilt from, so each action says it is not available instead of guessing.
	///The bridges themselves stay: their program links, properties and the parts that do not need the helper (starting the program, Schick CDR
	///DICOM version 5) are unchanged.</summary>
	public static class BridgeNotAvailable {
		///<summary>Dr. Ceph: sending the patient's demographics to a running Dr. Ceph.</summary>
		public const string DrCeph="Sending the patient to Dr. Ceph is not available in this version of Open PMS - Dental. "
			+"Find or add the patient in Dr. Ceph itself.";
		///<summary>eClinicalWorks tight or full mode: the Rx button opened eClinicalWorks' own prescription window.</summary>
		public const string EcwRx="The eClinicalWorks prescription window is not available in this version of Open PMS - Dental. "
			+"Write the prescription in eClinicalWorks itself.";
		///<summary>Patterson Imaging: sending the patient, provider and login to Patterson Imaging.</summary>
		public const string Patterson="Sending the patient to Patterson Imaging is not available in this version of Open PMS - Dental. "
			+"Find or add the patient in Patterson Imaging itself.";
		///<summary>Schick CDR version 4. Version 5 (CDR DICOM) still works, through Schick's own CDRDicom automation interface.</summary>
		public const string SchickVersion4="Sending the patient to Schick CDR version 4 is not available in this version of Open PMS - Dental. "
			+"Set the Schick bridge to version 5 (CDR DICOM) in Program Links, or find the patient in Schick itself.";
	}

	///<summary>Thrown by a bridge entry point that Open Dental's closed VBbridges.dll used to provide. Its message is one of
	///BridgeNotAvailable's messages.</summary>
	public class BridgeNotAvailableException:Exception {
		public BridgeNotAvailableException(string message):base(message) {
		}
	}
}
