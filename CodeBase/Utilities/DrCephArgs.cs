using Newtonsoft.Json;
using System;
using System.Diagnostics;

namespace CodeBase {
	///<summary>Class used to carry arguments to pass into the DrCeph launch method.</summary>
	public class DrCephArgs {
		#region DrCeph fields
		public string ID;
		public string FName;
		public string LName;
		public string MiddleI;
		public string Address1;
		public string Address2;
		public string City;
		public string State;
		public string Zip;
		public string Phone;
		public string SSN;
		public string Sex;
		public string Race;
		public string AngleClass;
		public string Birthdate;
		public string RecordsDate;
		public string ReferringDr;
		public string TreatingDr;
		public string ResponsibleName;
		public string ResponsibleAddress1;
		public string ResponsibleAddress2;
		public string ResponsibleCity;
		public string ResponsibleState;
		public string ResponsibleZip;
		public string ResponsiblePhone;
		public string ResponsibleRelationship;
		public int TreatmentPhase;
		public string CephXRayLocation;
		public string PhotoFileLocation;
		public string XRayDate;
		#endregion
	}

	public class DrCephUtils {
		///<summary>Always throws BridgeNotAvailableException. Open Dental sent these fields to Dr. Ceph through its closed VBbridges.dll, which this
		///program does not ship, and Dr. Ceph publishes no interface to rebuild it from. The Dr. Ceph bridge shows BridgeNotAvailable.DrCeph before
		///it gets here.</summary>
		public static void Launch(DrCephArgs args) {
			throw new BridgeNotAvailableException(BridgeNotAvailable.DrCeph);
		}

	}
}
