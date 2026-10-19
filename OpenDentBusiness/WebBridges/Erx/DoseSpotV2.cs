using CodeBase;
using CodeBase.VendorDirect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenDentBusiness {
	///<summary>Synchronous calls to DoseSpot's REST API v2 for the program, through DoseSpotApiClient.Default. Every call takes the office's own
	///credentials (DoseSpotCredentials.FromConfig) and goes straight to DoseSpot; nothing goes through Open Dental's servers. The v1 REST and SOAP
	///paths and the v1-to-v2 migration request are gone: an office's DoseSpot account is v2 (Medi-Span) from the start. Throws DoseSpotException.
	///Run these from a background thread or behind a progress window, never on the UI thread's context directly (VendorHttp.RunSync is used).</summary>
	public class DoseSpotRESTV2 {
		///<summary>Gets (or reuses) an access token, which proves the clinic ID, clinic key, user ID and subscription key work together.</summary>
		public static void CheckCredentials(DoseSpotCredentials doseSpotCredentials,bool isInteractive) {
			Meth.NoCheckMiddleTierRole();
			VendorHttp.RunSync(() => DoseSpotApiClient.Default.GetAccessTokenAsync(doseSpotCredentials,isInteractive));
		}

		public static DoseSpotNotificationCounts GetNotificationCounts(DoseSpotCredentials doseSpotCredentials,bool isInteractive) {
			Meth.NoCheckMiddleTierRole();
			return VendorHttp.RunSync(() => DoseSpotApiClient.Default.GetNotificationCountsAsync(doseSpotCredentials,isInteractive));
		}

		///<summary>Every prescription DoseSpot has for patientId (the DoseSpot patient ID, see DoseSpot.GetDoseSpotPatID).</summary>
		public static List<DoseSpotPrescription> GetPrescriptions(DoseSpotCredentials doseSpotCredentials,string patientId,bool isInteractive) {
			Meth.NoCheckMiddleTierRole();
			return VendorHttp.RunSync(() => DoseSpotApiClient.Default.GetPrescriptionsAsync(doseSpotCredentials,patientId,isInteractive));
		}

		///<summary>Every self-reported medication DoseSpot has for patientId.</summary>
		public static List<DoseSpotSelfReported> GetSelfReported(DoseSpotCredentials doseSpotCredentials,string patientId,bool isInteractive) {
			Meth.NoCheckMiddleTierRole();
			return VendorHttp.RunSync(() => DoseSpotApiClient.Default.GetSelfReportedAsync(doseSpotCredentials,patientId,isInteractive));
		}

		public static string GetPharmacyName(DoseSpotCredentials doseSpotCredentials,int pharmacyId,bool isInteractive) {
			Meth.NoCheckMiddleTierRole();
			return VendorHttp.RunSync(() => DoseSpotApiClient.Default.GetPharmacyNameAsync(doseSpotCredentials,pharmacyId,isInteractive));
		}

		///<summary>Adds the patient to DoseSpot and returns DoseSpot's patient ID. Throws ODException if a patient under 18 has no height and weight.</summary>
		public static string AddPatient(DoseSpotCredentials doseSpotCredentials,Patient patient,bool isInteractive) {
			Meth.NoCheckMiddleTierRole();
			object body=BuildPatientBody(patient);
			return VendorHttp.RunSync(() => DoseSpotApiClient.Default.AddPatientAsync(doseSpotCredentials,body,isInteractive));
		}

		///<summary>Overwrites DoseSpot's copy of the patient's demographics.</summary>
		public static void EditPatient(DoseSpotCredentials doseSpotCredentials,Patient patient,string doseSpotPatId,bool isInteractive) {
			Meth.NoCheckMiddleTierRole();
			object body=BuildPatientBody(patient);
			VendorHttp.RunSync(() => DoseSpotApiClient.Default.EditPatientAsync(doseSpotCredentials,doseSpotPatId,body,isInteractive));
		}

		public static void PostMedicationHistoryConsent(DoseSpotCredentials doseSpotCredentials,string patientId,bool isInteractive) {
			Meth.NoCheckMiddleTierRole();
			VendorHttp.RunSync(() => DoseSpotApiClient.Default.PostMedicationHistoryConsentAsync(doseSpotCredentials,patientId,isInteractive));
		}

		///<summary>Registers another location under the same DoseSpot account; the user in doseSpotCredentials is added to it by DoseSpot.
		///Puts out DoseSpot's new clinic ID and key.</summary>
		public static void PostClinic(DoseSpotCredentials doseSpotCredentials,Clinic clinic,out string clinicID,out string clinicKey) {
			Meth.NoCheckMiddleTierRole();
			//PrimaryPhoneType 7 is "Primary": a clinic's phone has no other type.
			object body=new {
				ClinicName=clinic.Abbr.Trim(),
				Address1=clinic.Address.Trim(),
				City=clinic.City.Trim(),
				State=clinic.State.Trim(),
				ZipCode=clinic.Zip.Trim(),
				PrimaryPhone=clinic.Phone.Trim(),
				PrimaryPhoneType="7",
			};
			KeyValuePair<string,string> pairIdKey=VendorHttp.RunSync(() => DoseSpotApiClient.Default.PostClinicAsync(doseSpotCredentials,body));
			clinicID=pairIdKey.Key;
			clinicKey=pairIdKey.Value;
		}

		///<summary>Adds (SelfReportedMedicationId 0) or updates a free-text self-reported medication. Returns DoseSpot's ID for it.</summary>
		public static int SaveSelfReported(DoseSpotCredentials doseSpotCredentials,string patientId,DoseSpotSelfReported doseSpotSelfReported,bool isInteractive) {
			Meth.NoCheckMiddleTierRole();
			if(doseSpotSelfReported==null || doseSpotSelfReported.SelfReportedMedicationId==null) {
				throw new ODException(Lans.g("DoseSpot","Error creating self reported medication ID: Could not convert medication ID to DoseSpot ID."));
			}
			object body;
			if(doseSpotSelfReported.DateInactive.HasValue && doseSpotSelfReported.DateInactive.Value.Year>1880) {
				body=new {
					DisplayName=doseSpotSelfReported.DisplayName.Trim(),
					Status=(int)doseSpotSelfReported.MedicationStatus,
					InactiveDate=doseSpotSelfReported.DateInactive,
					Comment=doseSpotSelfReported.Comment.Trim(),
				};
			}
			else {
				body=new {
					DisplayName=doseSpotSelfReported.DisplayName.Trim(),
					Status=(int)doseSpotSelfReported.MedicationStatus,
					Comment=doseSpotSelfReported.Comment.Trim(),
				};
			}
			int medicationId=doseSpotSelfReported.SelfReportedMedicationId.Value;
			return VendorHttp.RunSync(() => DoseSpotApiClient.Default.SaveSelfReportedAsync(doseSpotCredentials,patientId,medicationId,body,isInteractive));
		}

		///<summary>The JSON body for adding or editing a patient. Under 18, DoseSpot needs height and weight from a vital sign. Gender is our enum + 1,
		///with Other sent as Unknown (DoseSpot: Male, Female, Unknown).</summary>
		public static object BuildPatientBody(Patient patient) {
			Vitalsign vitalsign=Vitalsigns.GetOneWithValidHeightAndWeight(patient.PatNum);
			string primaryPhone=DoseSpot.GetPhoneAndType(patient,0,out string phoneType);
			return BuildPatientBody(patient,vitalsign,primaryPhone,phoneType);
		}

		///<summary>BuildPatientBody without database reads, for tests.</summary>
		public static object BuildPatientBody(Patient patient,Vitalsign vitalsign,string primaryPhone,string phoneType) {
			if(patient.Age<18 && vitalsign==null) {
				throw new ODException(Lans.g("DoseSpot","All patients under 18 must have a vital sign reading that includes height and weight. "
					+"To add a vital sign to the patient, go to the 'Chart' module, and double click on the pink medical area. "
					+"There is a tab in the next window labeled vitals, click it and add a vital reading that includes height and weight."));
			}
			PatientGender gender=patient.Gender;
			if(gender==PatientGender.Other) {
				gender=PatientGender.Unknown;
			}
			if(vitalsign==null) {
				return new {
					FirstName=patient.FName.Trim(),
					LastName=patient.LName.Trim(),
					DateOfBirth=patient.Birthdate,
					Gender=gender+1,
					Address1=patient.Address.Trim(),
					City=patient.City.Trim(),
					State=patient.State.Trim(),
					ZipCode=patient.Zip.Trim(),
					PrimaryPhone=(primaryPhone??"").Trim(),
					PrimaryPhoneType=(phoneType??"").Trim(),
					Active="true",
				};
			}
			return new {
				FirstName=patient.FName.Trim(),
				LastName=patient.LName.Trim(),
				DateOfBirth=patient.Birthdate,
				Gender=gender+1,
				Address1=patient.Address.Trim(),
				City=patient.City.Trim(),
				State=patient.State.Trim(),
				ZipCode=patient.Zip.Trim(),
				PrimaryPhone=(primaryPhone??"").Trim(),
				PrimaryPhoneType=(phoneType??"").Trim(),
				Weight=vitalsign.Weight,
				WeightMetric=1,
				Height=vitalsign.Height,
				HeightMetric=1,
				Active="true",
			};
		}

		///<summary>Helper function that takes in a MedicationPat and creates a DoseSpot SelfReported object with all required fields for a POST/PUT.</summary>
		public static DoseSpotSelfReported MedicationPatToDoseSpotSelfReport(MedicationPat medicationPat) {
			DoseSpotSelfReported doseSpotSelfReported=new DoseSpotSelfReported();
			doseSpotSelfReported.SelfReportedMedicationId=0;
			//If we have the ErxGuid then it has already been sent to DS.
			if(!medicationPat.ErxGuid.IsNullOrEmpty()) {
				//Remove any possible prefixes from the ErxGuid. It will either be a DS prefix, an OD prefix, or have no prefix at all if it came from an Rx.
				int ID=0;
				string guid=medicationPat.ErxGuid;
				//If it has the unsent prefix we want to intentionally set the ID to 0 so we post a new self reported and get an ID back from DS.
				if(!guid.StartsWith(Erx.UnsentPrefix)) {
					guid=Regex.Replace(guid,Erx.DoseSpotPatReportedPrefix,"");//Remove DS prefix.
					guid=Regex.Replace(guid,Erx.OpenDentalErxPrefix,"");//Remove OD prefix.
					int.TryParse(guid,out ID);
				}
				doseSpotSelfReported.SelfReportedMedicationId=ID;
			}
			//Set the DisplayName.
			if(string.IsNullOrWhiteSpace(medicationPat.MedDescript) && medicationPat.MedicationNum!=0) {
				Medication medication=Medications.GetMedication(medicationPat.MedicationNum);
				doseSpotSelfReported.DisplayName=medication.MedName;
			}
			else {
				doseSpotSelfReported.DisplayName=medicationPat.MedDescript;
			}
			//Strip any newlines before sending. Newlines cause parsing errors for the DoseSpot API. The changes to this note will be synced back when the eRx window is closed.
			doseSpotSelfReported.Comment=Regex.Replace(medicationPat.PatNote,@"\n|\r"," ");
			//500 characters is the max size for the comment field. Going over 500 will return a 400 error from the DoseSpot API.
			if(doseSpotSelfReported.Comment.Length>500) {
				SecurityLogs.MakeLogEntry(EnumPermType.LogDoseSpotMedicationNoteEdit,medicationPat.PatNum,"Medication patient note automatically reduced to 500 characters for sending to DoseSpot. Original note: "+"\n"
					+medicationPat.PatNote);
				doseSpotSelfReported.Comment=doseSpotSelfReported.Comment.Substring(0,500);
			}
			//Set the MedicationStatus.
			doseSpotSelfReported.MedicationStatus=DoseSpotREST.MedicationStatus.Discontinued;
			if(medicationPat.DateStop.Year<1880 || medicationPat.DateStop>=DateTime.Today) {
				doseSpotSelfReported.MedicationStatus=DoseSpotREST.MedicationStatus.Active;
			}
			else if(medicationPat.DateStop<DateTime.Today) {
				doseSpotSelfReported.MedicationStatus=DoseSpotREST.MedicationStatus.Completed;
				doseSpotSelfReported.DateInactive=medicationPat.DateStop;
				//A comment is required when a medication has been discontinued (figured out through testing, not in docs)
				if(string.IsNullOrWhiteSpace(doseSpotSelfReported.Comment)) {
					doseSpotSelfReported.Comment="Discontinued";
				}
			}
			return doseSpotSelfReported;
		}
	}
}
