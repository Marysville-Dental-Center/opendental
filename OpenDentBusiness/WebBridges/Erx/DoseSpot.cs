using CodeBase;
using Newtonsoft.Json;
using OpenDentBusiness.VendorDirect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenDentBusiness {

	///<summary>DoseSpot eRx, direct to DoseSpot with the office's own credentials (see DoseSpotConfig). DoseSpot's own prescribing screen opens in
	///the eRx window through a single sign-on URL computed here, locally; the REST calls go through DoseSpotRESTV2. Whether a clinic or provider may
	///use eRx is decided locally (configured credentials, Lists > Providers) and by DoseSpot itself, never by Open Dental's servers.</summary>
	public class DoseSpot {
		///<summary>The middle arc of the DoseSpot patient-ID root in oidexternal. Kept so rows written by Open Dental still match.</summary>
		private const string DOSESPOT_ARC="25128";
		///<summary>The suffix of a root created by this program. The root is only a local lookup key; it is never sent to DoseSpot.</summary>
		public const string LocalRootSuffix="0";

		#region OID
		///<summary>The prefix every DoseSpot patient-ID root starts with: LegacyOidArc.25128.</summary>
		public static string GetDoseSpotRootPrefix() {
			return OIDInternals.LegacyOidArc+"."+DOSESPOT_ARC;
		}

		///<summary>Returns the rootExternal that identifies OIDs as DoseSpot patient IDs. A database that used DoseSpot under Open Dental keeps its
		///root (LegacyOidArc.25128.&lt;customer number&gt;); otherwise a local root (LegacyOidArc.25128.0) is inserted. No server is asked.</summary>
		public static string GetDoseSpotRoot() {
			Meth.NoCheckMiddleTierRole();
			OIDExternal oIDExternal=OIDExternals.GetByPartialRootExternal(GetDoseSpotRootPrefix());
			if(oIDExternal==null) {
				oIDExternal=new OIDExternal();
				oIDExternal.IDType=IdentifierType.Root;
				oIDExternal.rootExternal=GetDoseSpotRootPrefix()+"."+LocalRootSuffix;
				OIDExternals.Insert(oIDExternal);
			}
			return oIDExternal.rootExternal;
		}

		///<summary>Gets the OIDExternal corresponding to Dose Spot and the patnum given.  Returns null if no match found.</summary>
		public static OIDExternal GetDoseSpotPatID(long patNum) {
			Meth.NoCheckMiddleTierRole();
			return OIDExternals.GetOidExternal(GetDoseSpotRoot()+"."+POut.Int((int)IdentifierType.Patient),patNum,IdentifierType.Patient);
		}

		///<summary>Gets the OIDExternal corresponding to Dose Spot oid given.  Returns null if no match found.
		///If its not null this means the practice has used DoseSpot at least once.</summary>
		public static OIDExternal GetDoseSpotRootOid() {
			Meth.NoCheckMiddleTierRole();
			return OIDExternals.GetByPartialRootExternal(GetDoseSpotRootPrefix());
		}

		public static OIDExternal CreateOIDForPatient(int doseSpotPatID,long patNum) {
			OIDExternal oIDExternal=new OIDExternal();
			oIDExternal.rootExternal=DoseSpot.GetDoseSpotRoot()+"."+POut.Int((int)IdentifierType.Patient);
			oIDExternal.IDExternal=doseSpotPatID.ToString();
			oIDExternal.IDInternal=patNum;
			oIDExternal.IDType=IdentifierType.Patient;
			OIDExternals.Insert(oIDExternal);
			return oIDExternal;
		}
		#endregion OID

		///<summary>Returns true if the passed in accountId is a DoseSpot account id (a repeat charge account ID that Open Dental's billing used).</summary>
		public static bool IsDoseSpotAccountId(string accountId) {
			Meth.NoCheckMiddleTierRole();
			return accountId.ToUpper().StartsWith("DS;");//ToUpper because user might have manually typed in.
		}

		///<summary>Creates a unique account id for DoseSpot.  Uses the same generation logic as NewCrop, with DS; preceeding it.</summary>
		public static string GenerateAccountId(long patNum) {
			string accountId="DS;"+POut.Long(patNum);
			accountId+="-"+CodeBase.MiscUtils.CreateRandomAlphaNumericString(3);
			long checkSum=patNum;
			checkSum+=Convert.ToByte(accountId[accountId.IndexOf('-')+1])*3;
			checkSum+=Convert.ToByte(accountId[accountId.IndexOf('-')+2])*5;
			checkSum+=Convert.ToByte(accountId[accountId.IndexOf('-')+3])*7;
			accountId+=(checkSum%100).ToString().PadLeft(2,'0');
			return accountId;
		}

		///<summary>A single sign-on code for clinicKey with a fresh random phrase, URL-encoded if isQueryStr. See DoseSpotSso.</summary>
		public static string CreateSsoCode(string clinicKey,bool isQueryStr=true) {
			Meth.NoCheckMiddleTierRole();
			string code=DoseSpotSso.ComputeCode(DoseSpotSso.CreatePhrase(),clinicKey);
			return isQueryStr ? Uri.EscapeDataString(code) : code;
		}

		///<summary>Can throw exceptions.  Returns true if changes were made to medications.</summary>
		public static bool SyncPrescriptionsFromDoseSpot(string clinicID,string clinicKey,string userID,long patNum,Action<List<RxPat>> onRxAdd=null,
			bool isInteractive=false)
		{
			Meth.NoCheckMiddleTierRole();
			OIDExternal oIDExternal=DoseSpot.GetDoseSpotPatID(patNum);
			if(oIDExternal==null) {
				return false;//We don't have a PatID from DoseSpot for this patient.  Therefore there is nothing to sync with.
			}
			DoseSpotCredentials doseSpotCredentials=DoseSpotCredentials.FromConfig(clinicID,clinicKey,userID);
			bool hasChangedPrescriptions=false;
			Patient patient=Patients.GetPat(patNum);
			List<long> listMedicationPatNumsActive=new List<long>();
			Dictionary<int,string> dictPharmacyIdToPharmacyName=new Dictionary<int,string>();
			List<RxPat> listRxPats=new List<RxPat>();
			//Get rid of any deleted prescriptions.
			List<DoseSpotPrescription> listDoseSpotPerscriptions=DoseSpotRESTV2.GetPrescriptions(doseSpotCredentials,oIDExternal.IDExternal,isInteractive);
			List<DoseSpotMedicationWrapper> listDoseSpotMedicationWrappers=listDoseSpotPerscriptions.Select(x => new DoseSpotMedicationWrapper(x,null)).ToList();
			//Add self reported medications.
			listDoseSpotMedicationWrappers.AddRange(DoseSpotRESTV2.GetSelfReported(doseSpotCredentials,oIDExternal.IDExternal,isInteractive)
				.Select(x => new DoseSpotMedicationWrapper(null,x)).ToList());
			foreach(DoseSpotMedicationWrapper doseSpotMedicationWrapper in listDoseSpotMedicationWrappers.Where(x=>x.MedicationStatus!=DoseSpotREST.MedicationStatus.Deleted)) {
				RxPat rxPatOld=null;
				if(doseSpotMedicationWrapper.IsSelfReported) {
					//Get self reported that originated in this program
					rxPatOld=RxPats.GetErxByIdForPat(Erx.OpenDentalErxPrefix+doseSpotMedicationWrapper.MedicationId.ToString(),patNum);
					if(rxPatOld==null) {
						//Get self reported that originated in DS
						rxPatOld=RxPats.GetErxByIdForPat(Erx.DoseSpotPatReportedPrefix+doseSpotMedicationWrapper.MedicationId.ToString(),patNum);
					}
				}
				else {
					//Isn't self reported, Guid won't have a prefix.
					rxPatOld=RxPats.GetErxByIdForPat(doseSpotMedicationWrapper.MedicationId.ToString(),patNum);
				}
				RxPat rxPat=new RxPat();
				long rxCui=doseSpotMedicationWrapper.RxCUI;//If this is zero either DoseSpot didn't send the value or there was an issue casting from string to long.
				rxPat.IsControlled=(PIn.Int(doseSpotMedicationWrapper.Schedule)!=0);//Controlled if Schedule is I,II,III,IV,V
				rxPat.DosageCode="";
				rxPat.SendStatus=RxSendStatus.Unsent;
				switch(doseSpotMedicationWrapper.PrescriptionStatus) {
					case DoseSpotREST.PrescriptionStatus.PharmacyVerified:
					case DoseSpotREST.PrescriptionStatus.eRxSent:
						rxPat.SendStatus=RxSendStatus.SentElect;
						break;
					case DoseSpotREST.PrescriptionStatus.FaxSent:
						rxPat.SendStatus=RxSendStatus.Faxed;
						break;
					case DoseSpotREST.PrescriptionStatus.Printed:
						rxPat.SendStatus=RxSendStatus.Printed;
						break;
					case DoseSpotREST.PrescriptionStatus.Sending:
						rxPat.SendStatus=RxSendStatus.Pending;
						break;
					case DoseSpotREST.PrescriptionStatus.Deleted:
						if(rxPatOld==null) {
							//DoseSpot sent a deleted medication that we don't have a record of. Skip it.
							continue;
						}
						MedicationPat medicationPat=MedicationPats.GetMedicationOrderByErxIdAndPat(rxPatOld.ErxGuid,rxPatOld.PatNum);
						RxPats.Delete(rxPatOld.RxNum);
						hasChangedPrescriptions=true;
						SecurityLog securityLog = new SecurityLog();
						securityLog.UserNum=0;//Don't attach to user since this is being done by DoseSpot
						securityLog.CompName=Security.GetComplexComputerName();
						securityLog.PermType=EnumPermType.RxEdit;
						securityLog.FKey=0;
						securityLog.LogSource=LogSources.eRx;
						securityLog.LogText="FROM("+rxPatOld.RxDate.ToShortDateString()+","+rxPatOld.Drug+","+rxPatOld.ProvNum+","+rxPatOld.Disp+","+rxPatOld.Refills+")"+"\r\nTO 'deleted' change made by DoseSpot eRx.";
						securityLog.PatNum=rxPatOld.PatNum;
						securityLog.DefNum=0;
						securityLog.DefNumError=0;
						securityLog.DateTPrevious=DateTime.MinValue;
						SecurityLogs.MakeLogEntry(securityLog);
						if(medicationPat!=null) {
							MedicationPats.Delete(medicationPat);
							securityLog.PermType=EnumPermType.PatMedicationListEdit;
							securityLog.LogText=(medicationPat.MedicationNum==0 ? medicationPat.MedDescript : Medications.GetMedication(medicationPat.MedicationNum).MedName
								)+" deleted by DoseSpot"+"\r\n"
								+(String.IsNullOrEmpty(medicationPat.PatNote) ? "" : "Pat note: "+medicationPat.PatNote);
							securityLog.PatNum=medicationPat.PatNum;//probably the same but better safe than sorry.
							securityLog.SecurityLogNum=0;//Reset primary key to guarantee insert will work no matter what changes get made to securitylog code.
							SecurityLogs.MakeLogEntry(securityLog);
						}
						break;
					case DoseSpotREST.PrescriptionStatus.Error:
					case DoseSpotREST.PrescriptionStatus.EpcsError:
						continue;//Skip these medications since DoseSpot is saying that they are invalid
					case DoseSpotREST.PrescriptionStatus.Edited:
					case DoseSpotREST.PrescriptionStatus.Entered:
					case DoseSpotREST.PrescriptionStatus.EpcsSigned:
					case DoseSpotREST.PrescriptionStatus.ReadyToSign:
					case DoseSpotREST.PrescriptionStatus.Requested:
					default:
						rxPat.SendStatus=RxSendStatus.Unsent;
						break;
				}
				rxPat.Refills=doseSpotMedicationWrapper.Refills;
				rxPat.Disp=doseSpotMedicationWrapper.Quantity;//In DoseSpot, the Quanitity textbox's label says "Dispense".
				rxPat.Drug=doseSpotMedicationWrapper.DisplayName;
				if(doseSpotMedicationWrapper.PharmacyId.HasValue) {
					try {
						if(!dictPharmacyIdToPharmacyName.ContainsKey(doseSpotMedicationWrapper.PharmacyId.Value)) {
							dictPharmacyIdToPharmacyName.Add(doseSpotMedicationWrapper.PharmacyId.Value,
								DoseSpotRESTV2.GetPharmacyName(doseSpotCredentials,doseSpotMedicationWrapper.PharmacyId.Value,isInteractive));
						}
						rxPat.ErxPharmacyInfo=dictPharmacyIdToPharmacyName[doseSpotMedicationWrapper.PharmacyId.Value];
					}
					catch(Exception ex) {
						ex.DoNothing();
						//Do nothing.  It was a nicety anyways.
					}
				}
				rxPat.PatNum=patNum;
				rxPat.Sig=doseSpotMedicationWrapper.Directions;
				rxPat.Notes=doseSpotMedicationWrapper.RxNotes;
				rxPat.RxDate=DateTime.MinValue;
				//If none of dates have values, the RxDate will be MinValue.
				//This is acceptable if DoseSpot doesn't give us anything, which should never happen.
				if(doseSpotMedicationWrapper.DateWritten.HasValue) {
					rxPat.RxDate=doseSpotMedicationWrapper.DateWritten.Value;
				}
				else if(doseSpotMedicationWrapper.DateReported.HasValue) {
					rxPat.RxDate=doseSpotMedicationWrapper.DateReported.Value;
				}
				else if(doseSpotMedicationWrapper.DateLastFilled.HasValue) {
					rxPat.RxDate=doseSpotMedicationWrapper.DateLastFilled.Value;
				}
				else if(doseSpotMedicationWrapper.DateInactive.HasValue) {
					rxPat.RxDate=doseSpotMedicationWrapper.DateInactive.Value;
				}
				//Save DoseSpot's unique ID into our rx
				int doseSpotMedId=(int?)doseSpotMedicationWrapper.MedicationId??0;//If this changes, we need to ensure that Erx.IsFromDoseSpot() is updated to match.
				rxPat.ErxGuid=doseSpotMedId.ToString();
				bool isProvider=false;
				if(doseSpotMedicationWrapper.IsSelfReported) {//Self Reported medications won't have a prescriber number
					if(rxPatOld==null) {//Rx doesn't exist in the database.  This probably originated from DoseSpot
						MedicationPat medicationPat=MedicationPats.GetMedicationOrderByErxIdAndPat(Erx.OpenDentalErxPrefix+doseSpotMedicationWrapper.MedicationId.ToString(),patNum);
						if(medicationPat==null) {//If there isn't a record of the medication
							medicationPat=MedicationPats.GetMedicationOrderByErxIdAndPat(Erx.DoseSpotPatReportedPrefix+doseSpotMedicationWrapper.MedicationId.ToString(),patNum);
						}
						if(medicationPat==null) {//If medPat is null at this point we don't have a record of this patient having the medication, so it probably was just made in DoseSpot.
							rxPat.ErxGuid=Erx.DoseSpotPatReportedPrefix+doseSpotMedicationWrapper.MedicationId;
						}
						else {
							rxPat.ErxGuid=medicationPat.ErxGuid;//Maintain the ErxGuid that was assigned for the MedicationPat that already exists.
						}
					}
					else {
						rxPat.ErxGuid=rxPatOld.ErxGuid;//Maintain the ErxGuid that was already assigned for the Rx.
					}
				}
				else {
					//The prescriber ID for each medication is the doctor that approved the prescription.
					UserOdPref userOdPref=UserOdPrefs.GetByFkeyAndFkeyType(Programs.GetCur(ProgramName.eRx).ProgramNum,UserOdFkeyType.Program)
					.FirstOrDefault(x => x.ValueString==doseSpotMedicationWrapper.PrescriberId.ToString());
					if(userOdPref==null) {//The Dose Spot User ID from this medication is not present in this database.
						continue;//I don't know if we want to do anything with this.  Maybe we want to just get the ErxLog from before this medication was made.
					}
					Userod userod=Userods.GetUser(userOdPref.UserNum);
					Provider provider=new Provider();
					isProvider=!Erx.IsUserAnEmployee(userod);
					if(isProvider) {//A user always be a provider if there is a ProvNum > 0
						provider=Providers.GetProv(userod.ProvNum);
					}
					else {
						provider=Providers.GetProv(patient.PriProv);
					}
					if(provider==null && rxPatOld!=null) {//Provide a fallback in case the provider is 'missing' due to being detached from the userod
						rxPat.ProvNum=rxPatOld.ProvNum;
					}
					if(provider!=null) {
						rxPat.ProvNum=provider.ProvNum;
					}
				}
				//These fields are possibly set above, preserve old values if they are not.
				if(rxPatOld!=null) {
					rxPat.Disp=rxPatOld.Disp; //The medication Disp currently always returns 0. Preserve the old value.
					rxPat.Refills=rxPat.Refills==null ? rxPatOld.Refills : rxPat.Refills;
					rxPat.Notes=rxPatOld.Notes.IsNullOrEmpty() ? rxPat.Notes : rxPatOld.Notes;//Preserve the note already in the database no matter what if there is one.
					rxPat.PatientInstruction=rxPat.PatientInstruction.IsNullOrEmpty() ? rxPatOld.PatientInstruction : rxPat.PatientInstruction;
					rxPat.ErxPharmacyInfo=rxPat.ErxPharmacyInfo==null ? rxPatOld.ErxPharmacyInfo : rxPat.ErxPharmacyInfo;
					rxPat.IsControlled=rxPatOld.IsControlled;
					if(rxPatOld.RxDate.Year>1880) {
						rxPat.RxDate=rxPatOld.RxDate;
					}
				}
				long medicationPatNum=0;
				hasChangedPrescriptions|=RxPats.UpdateComparison(rxPat,rxPatOld);
				if(Erx.IsDoseSpotPatReported(rxPat.ErxGuid) || Erx.IsTwoWayIntegrated(rxPat.ErxGuid)) {//For DoseSpot self reported, do not insert a prescription.
					medicationPatNum=Erx.InsertOrUpdateErxMedication(rxPatOld,rxPat,rxCui,doseSpotMedicationWrapper.DisplayName,doseSpotMedicationWrapper.GenericProductName,isProvider,false);
				}
				else {
					medicationPatNum=Erx.InsertOrUpdateErxMedication(rxPatOld,rxPat,rxCui,doseSpotMedicationWrapper.DisplayName,doseSpotMedicationWrapper.GenericProductName,isProvider);
				}
				if(rxPatOld==null && !doseSpotMedicationWrapper.MedicationStatus.In(DoseSpotREST.MedicationStatus.Unknown, DoseSpotREST.MedicationStatus.Inactive)) {//Only add the rx if it is new and it's status isn't deprecated.  We don't want to trigger automation for existing prescriptions.
					listRxPats.Add(rxPat);
				}
				if(doseSpotMedicationWrapper.MedicationStatus==DoseSpotREST.MedicationStatus.Active) {
					listMedicationPatNumsActive.Add(medicationPatNum);
				}
			}
			List<MedicationPat> listMedicationPats=MedicationPats.Refresh(patNum,false);
			for(int i=0;i<listMedicationPats.Count;i++) {
			//This loop should update the end date for: Perscriptions made in DoseSpot, Medications made in DoseSpot, and Medications made locally
				string eRxGuidCur=listMedicationPats[i].ErxGuid;
				if(!Erx.IsFromDoseSpot(eRxGuidCur) && !Erx.IsDoseSpotPatReported(eRxGuidCur) && !Erx.IsTwoWayIntegrated(eRxGuidCur))
				{
					continue;//This medication is not synced with DoseSpot, don't update.
				}
				if(listMedicationPatNumsActive.Contains(listMedicationPats[i].MedicationPatNum)) {
					continue;//The medication is still active.
				}
				if(listMedicationPats[i].DateStop.Year>1880) {
					continue;//The medication is already discontinued locally.
				}
				//The medication was discontinued inside the eRx interface.
				DoseSpotMedicationWrapper doseSpotMedicationWrapper=listDoseSpotMedicationWrappers.FirstOrDefault(x => Erx.OpenDentalErxPrefix+x.MedicationId.ToString()==eRxGuidCur);
				if(doseSpotMedicationWrapper==null) {
					doseSpotMedicationWrapper=listDoseSpotMedicationWrappers.FirstOrDefault(x => Erx.DoseSpotPatReportedPrefix+x.MedicationId.ToString()==eRxGuidCur);
				}
				if(doseSpotMedicationWrapper==null) {
					//We don't have a medication from DS for this medicationpat, kick out.
					continue;
				}
				//Try to get the date stop.
				if(doseSpotMedicationWrapper.DateInactive!=null) {
					listMedicationPats[i].DateStop=doseSpotMedicationWrapper.DateInactive.Value;
				}
				else {
					listMedicationPats[i].DateStop=DateTime.Today.AddDays(-1);//Discontinue the medication as of yesterday so that it will immediately show as discontinued.
				}
				MedicationPats.Update(listMedicationPats[i]);//Discontinue the medication locally to match what shows in the eRx interface.
				string medDescript=listMedicationPats[i].MedDescript;
				if(listMedicationPats[i].MedicationNum!=0) {
					medDescript=Medications.GetMedication(listMedicationPats[i].MedicationNum).MedName;
				}
				SecurityLogs.MakeLogEntry(EnumPermType.PatMedicationListEdit,listMedicationPats[i].PatNum,medDescript+" DoseSpot set inactive",LogSources.eRx);
			}
			if(onRxAdd!=null && listRxPats.Count!=0) {
				onRxAdd(listRxPats);
			}
			return hasChangedPrescriptions;
		}

		///<summary>Can throw exception. Sends this program's medications for the patient to DoseSpot as self-reported medications.</summary>
		public static void SyncPrescriptionsToDoseSpot(string clinicID,string clinicKey,string userID,long patNum) {
			Meth.NoCheckMiddleTierRole();
			OIDExternal oIDExternal=DoseSpot.GetDoseSpotPatID(patNum);
			if(oIDExternal==null) {
				return;//We don't have a PatID from DoseSpot for this patient.  Therefore there is nothing to sync with.
			}
			DoseSpotCredentials doseSpotCredentials=DoseSpotCredentials.FromConfig(clinicID,clinicKey,userID);
			List<MedicationPat> listMedicationPats=MedicationPats.Refresh(patNum,true).FindAll(x => !Erx.IsFromDoseSpot(x.ErxGuid));
			if(listMedicationPats.Count==0) {
				return;//There are no medications to send to DoseSpot.
			}
			foreach(MedicationPat medicationPat in listMedicationPats) {
				//Medications originating from DS are filtered out when the list is retrieved.
				DoseSpotSelfReported doseSpotSelfReported=DoseSpotRESTV2.MedicationPatToDoseSpotSelfReport(medicationPat);
				if(string.IsNullOrWhiteSpace(doseSpotSelfReported.DisplayName)) {
					//Couldn't get a name from the medicationpat or the medication, don't send to DS without a name.
					continue;
				}
				if(doseSpotSelfReported.SelfReportedMedicationId>0) {
					//We were able to get the external ID for this self reported from the medicationpat, therefore we want to do an edit.
					try {
						DoseSpotRESTV2.SaveSelfReported(doseSpotCredentials,oIDExternal.IDExternal,doseSpotSelfReported,false);
					}
					catch(ODException e) {
						if(e.Message.Contains("does not belong to PatientID")) {
							//skip these, get past a bug in the DS API where it returns medications for the wrong patient.
							continue;
						}
						throw;
					}
				}
				else {
					//Save the ID returned by DoseSpot.
					RxPat rxPat=null;
					if(medicationPat.ErxGuid.StartsWith(Erx.UnsentPrefix)) {
						rxPat=RxPats.GetErxByIdForPat(medicationPat.ErxGuid,medicationPat.PatNum);
					}
					medicationPat.ErxGuid=Erx.OpenDentalErxPrefix+DoseSpotRESTV2.SaveSelfReported(doseSpotCredentials,oIDExternal.IDExternal,doseSpotSelfReported,false);
					if(rxPat!=null) {
						rxPat.ErxGuid=medicationPat.ErxGuid;
						RxPats.Update(rxPat);
					}
				}
				//Update the medPat to store an ErxGuid and the returned ID from DoseSpot so that we don't keep sending this unnecessarily.
				MedicationPats.Update(medicationPat);
			}
		}

		///<summary>Returns true if at least one of the counts is greater than 0.
		///This is used for notifying prescribers when they need to take action is DoseSpot.</summary>
		public static bool GetPrescriberNotificationCounts(string clinicID,string clinicKey,string userID
			,out int countRefillReqs,out int countTransactionErrors,out int countPendingPrescriptionsCount)
		{
			Meth.NoCheckMiddleTierRole();
			DoseSpotNotificationCounts counts=DoseSpotRESTV2.GetNotificationCounts(DoseSpotCredentials.FromConfig(clinicID,clinicKey,userID),false);
			countRefillReqs=counts.RefillRequestsCount;
			countTransactionErrors=counts.TransactionErrorsCount;
			countPendingPrescriptionsCount=counts.PendingPrescriptionsCount;
			return (countRefillReqs>0 || countTransactionErrors>0 || countPendingPrescriptionsCount>0);//Return true if there is anything that the prescriber needs to see/take action on.
		}

		///<summary>If pat is null, it is assumed that we are making a SSO request for errors and refill requests. The query string starts with '&amp;'.
		///Every call gets a fresh random phrase. Log it only through DoseSpotSso.Redact.</summary>
		public static string GetSingleSignOnQueryString(string clinicID,string clinicKey,string userID,string onBehalfOfUserId,Patient patient) {
			Meth.NoCheckMiddleTierRole();
			DoseSpotSso doseSpotSso=DoseSpotSso.Create(clinicID,clinicKey,userID);
			string patientId="";
			if(patient!=null) {
				OIDExternal oIDExternal=DoseSpot.GetDoseSpotPatID(patient.PatNum);
				if(oIDExternal!=null) {
					patientId=oIDExternal.IDExternal;
				}
			}
			return doseSpotSso.ToQueryString(onBehalfOfUserId,patientId,patient==null);
		}

		///<summary>Throws ODException naming what is missing. Puts out the clinic ID and the decrypted clinic key for clinicNum (no fallback to another
		///clinic: each DoseSpot clinic is one location). An osd1$ key that does not decrypt counts as missing; it is never sent.
		///doseSpotUserID, program and listProgramProperties are optional.</summary>
		public static void GetClinicIdAndKey(long clinicNum,string doseSpotUserID,Program program,List<ProgramProperty> listProgramProperties
			,out string clinicID,out string clinicKey)
		{
			Meth.NoCheckMiddleTierRole();
			clinicID="";
			clinicKey="";
			if(program==null) {
				program=Programs.GetCur(ProgramName.eRx);
			}
			if(listProgramProperties==null) {
				listProgramProperties=ProgramProperties.GetForProgram(program.ProgramNum)
					.FindAll(x => x.ClinicNum==clinicNum
						&& (x.PropertyDesc==Erx.PropertyDescs.ClinicID || x.PropertyDesc==Erx.PropertyDescs.ClinicKey));
			}
			ProgramProperty programPropertyClinicID=listProgramProperties.FirstOrDefault(x => x.ClinicNum==clinicNum && x.PropertyDesc==Erx.PropertyDescs.ClinicID);
			ProgramProperty programPropertyClinicKey=listProgramProperties.FirstOrDefault(x => x.ClinicNum==clinicNum && x.PropertyDesc==Erx.PropertyDescs.ClinicKey);
			string clinicIdStored=programPropertyClinicID?.PropertyValue??"";
			SecretReadResult secretReadResult=DoseSpotConfig.ReadClinicKey(programPropertyClinicKey?.PropertyValue);
			if(string.IsNullOrWhiteSpace(clinicIdStored) || !secretReadResult.HasValue) {
				string message=DoseSpotConfig.GetClinicName(clinicNum)+" "+Lans.g("DoseSpot","has no DoseSpot clinic ID and key.");
				if(secretReadResult.Status==SecretReadStatus.Unreadable) {
					message=Lans.g("DoseSpot","The DoseSpot clinic key for")+" "+DoseSpotConfig.GetClinicName(clinicNum)+" "
						+Lans.g("DoseSpot","can't be read on this computer.");
				}
				throw new ODException(message+" "+Lans.g("DoseSpot","Enter it in Setup > Program Links > eRx."));
			}
			clinicID=clinicIdStored.Trim();
			clinicKey=secretReadResult.PlainText.Trim();
		}

		///<summary>Throws Exceptions if clinic is deemed invalid for DoseSpot. Registers clinicNum with DoseSpot, using an already registered clinic's
		///ID and plain key plus the user's DoseSpot ID, and puts out the new ID and key. Saves them for clinicNum: the key encrypted, in a masked,
		///high-security row. Refreshes the cache.</summary>
		public static void RegisterClinic(long clinicNum,string clinicID,string clinicKey,string userID
			,out string clinicIdNew,out string clinicKeyNew)
		{
			Meth.NoCheckMiddleTierRole();
			Clinic clinic=GetClinicOrPracticeInfo(clinicNum);
			DoseSpotCredentials doseSpotCredentials=DoseSpotCredentials.FromConfig(clinicID,clinicKey,userID);
			DoseSpotRESTV2.PostClinic(doseSpotCredentials,clinic,out clinicIdNew,out clinicKeyNew);
			long programNumErx=Programs.GetCur(ProgramName.eRx).ProgramNum;
			string clinicKeyStored;
			if(!SecretValue.Protect(clinicKeyNew,out clinicKeyStored)) {
				throw new ODException(Lans.g("DoseSpot","DoseSpot registered the clinic, but its key could not be encrypted, so it was not saved.")+" "
					+Lans.g("DoseSpot","Clinic ID:")+" "+clinicIdNew);
			}
			VendorCredentials.WriteSetting(programNumErx,Erx.PropertyDescs.ClinicID,clinicNum,clinicIdNew);
			VendorCredentials.WriteSecret(programNumErx,Erx.PropertyDescs.ClinicKey,clinicNum,clinicKeyNew);
			//Ensure cache is not stale after setting the values.
			Cache.Refresh(InvalidType.Programs);
		}

		///<summary>Throws ODException when the user has no DoseSpot user ID for clinicNum (or the default). Also validates the clinic or practice info.</summary>
		public static string GetUserID(Userod userod,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			GetClinicOrPracticeInfo(clinicNum);
			//At this point we know that we have a valid clinic/practice info.
			UserOdPref userOdPref=GetDoseSpotUserIdFromPref(userod.UserNum,clinicNum);
			if(userOdPref==null || string.IsNullOrWhiteSpace(userOdPref.ValueString)) {
				throw new ODException(Lans.g("DoseSpot","User")+" "+userod.UserName+" "+Lans.g("DoseSpot","has no DoseSpot user ID. "
					+"DoseSpot issues one for each prescriber and proxy user; enter it in Security > User Edit."));
			}
			return userOdPref.ValueString.Trim();
		}

		///<summary>Parses the passed in queryString for the value of the passed in parameter name.
		///Returns null if a match isn't found.</summary>
		public static string GetQueryParameterFromQueryString(string queryString,string parameterNameToReturn) {
			Meth.NoCheckMiddleTierRole();
			if(!string.IsNullOrWhiteSpace(parameterNameToReturn) && !string.IsNullOrEmpty(queryString)) {
				if(queryString[0]=='?') {
					queryString=queryString.Substring(1);
				}
				string[] arrayQueries=queryString.Split('&');
				foreach(string queryCur in arrayQueries) {
					if(!String.IsNullOrWhiteSpace(queryCur) && queryCur.Contains("=")) {
						string[] arrayQueryCur=queryCur.Split('=');
						string paramNameCur=arrayQueryCur[0];
						if(paramNameCur!=null && paramNameCur.ToUpper().Equals(parameterNameToReturn.ToUpper())) {
							return arrayQueryCur[1];
						}
					}
				}
			}
			return null;
		}

		public static UserOdPref GetDoseSpotUserIdFromPref(long userNum,long clinicNum) {
			Program program=Programs.GetCur(ProgramName.eRx);
			UserOdPref userOdPref=UserOdPrefs.GetByCompositeKey(userNum,program.ProgramNum,UserOdFkeyType.Program,clinicNum);
			if(clinicNum!=0 && userOdPref.IsNew || string.IsNullOrWhiteSpace(userOdPref.ValueString)) {
				userOdPref=UserOdPrefs.GetByCompositeKey(userNum,program.ProgramNum,UserOdFkeyType.Program,0);//Try the default userodpref if the clinic specific one is empty.
			}
			return userOdPref;
		}

		///<summary>Throws exceptions if any of the clinic/practice info is invalid.
		///Ensures that the Clinic that is returned is a valid clinic that can turned
		/// into a DoseSpot Clinic to be used for DoseSpot API calls that require a Clinic (like registering a new clinic). </summary>
		private static Clinic GetClinicOrPracticeInfo(long clinicNum) {
			Clinic clinic=Clinics.GetClinic(clinicNum);
			bool isPractice=false;
			if(clinic==null) {//Make a fake ClinicNum 0 clinic containing practice info for validation/registering a new clinician if needed.
				clinic=new Clinic();
				clinic.Abbr=PrefC.GetString(PrefName.PracticeTitle);
				clinic.Address=PrefC.GetString(PrefName.PracticeAddress);
				clinic.Address2=PrefC.GetString(PrefName.PracticeAddress2);
				clinic.City=PrefC.GetString(PrefName.PracticeCity);
				clinic.State=PrefC.GetString(PrefName.PracticeST);
				clinic.Zip=PrefC.GetString(PrefName.PracticeZip);
				clinic.Phone=PrefC.GetString(PrefName.PracticePhone);
				clinic.Fax=PrefC.GetString(PrefName.PracticeFax);
				isPractice=true;
			}
			ValidateClinic(clinic,isPractice);
			//At this point we know the clinic is valid since we did not throw an exception.
			return clinic;
		}

		///<summary>Throws exceptions if anything about the provider is invalid.
		///Not throwing an exception means that the provider is valid</summary>
		public static void ValidateProvider(Provider provider,long clinicNum=0) {
			Meth.NoCheckMiddleTierRole();
			if(provider==null) {
				throw new Exception("Invalid provider.");
			}
			ProviderClinic providerClinic=ProviderClinics.GetOneOrDefault(provider.ProvNum,clinicNum);
			StringBuilder stringBuilder=new StringBuilder();
			if(provider.IsErxEnabled==ErxEnabledStatus.Disabled) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","eRx is not enabled for this provider. Enable it in Lists > Providers."));
			}
			if(provider.IsHidden) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Provider is hidden"));
			}
			if(provider.IsNotPerson) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Provider must be a person"));
			}
			string fName=provider.FName.Trim();
			if(fName=="") {
				stringBuilder.AppendLine(Lans.g("DoseSpot","First name missing"));
			}
			if(Regex.Replace(fName,"[^A-Za-z\\- ]*","")!=fName) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","First name can only contain letters, dashes, or spaces"));
			}
			string lName=provider.LName.Trim();
			if(lName=="") {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Last name missing"));
			}
			string deaNum="";
			if(providerClinic!=null) {
				deaNum=providerClinic.DEANum;
			}
			if(deaNum.ToLower()!="none" && !Regex.IsMatch(deaNum,"^[A-Za-z]{2}[0-9]{7}$") ) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Provider DEA Number must be 2 letters followed by 7 digits.  If no DEA Number, enter NONE."));
			}
			string npi=Regex.Replace(provider.NationalProvID,"[^0-9]*","");//NPI with all non-numeric characters removed.
			if(npi.Length!=10) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","NPI must be exactly 10 digits"));
			}
			if(providerClinic==null || providerClinic.StateLicense=="") {
				stringBuilder.AppendLine(Lans.g("DoseSpot","State license missing"));
			}
			if(providerClinic==null || !USlocales.IsValidAbbr(providerClinic.StateWhereLicensed)) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","State where licensed invalid"));
			}
			if(provider.Birthdate.Year<1880) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Birthdate invalid"));
			}
			if(stringBuilder.ToString().Length>0) {
				string clinicText="";
				if(PrefC.HasClinicsEnabled) {
					clinicText=" "+Lans.g("DoseSpot","in clinic")+" "+(clinicNum==0?Lans.g("DoseSpot","Headquarters"):Clinics.GetAbbr(clinicNum));
				}
				throw new ODException(Lans.g("DoseSpot","Issues found for provider")+" "+provider.Abbr+clinicText+":\r\n"+stringBuilder.ToString());
			}
		}

		///<summary>Throws exceptions if anything about the clinic is invalid.
		///Not throwing an exception means that the clinic is valid.
		///In the case that the user is validating clinicNum 0 or doesn't have clinics enabled, the clinic here has
		/// practice information in it.</summary>
		private static void ValidateClinic(Clinic clinic,bool isPractice = false) {
			if(clinic==null) {
				throw new Exception(Lans.g("DoseSpot","Invalid "+(isPractice ? "practice info." : "clinic.")));
			}
			StringBuilder stringBuilder=new StringBuilder();
			if(clinic.IsHidden) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Clinic is hidden"));
			}
			if(string.IsNullOrWhiteSpace(clinic.Phone)) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Phone number is blank"));
			}
			else if(!IsPhoneNumberValid(clinic.Phone)) {//If the phone number isn't valid, DoseSpot will break.
				stringBuilder.AppendLine(Lans.g("DoseSpot","Phone number invalid: ")+clinic.Phone);
			}
			if(clinic.Address=="") {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Address is blank"));
			}
			if(IsAddressPOBox(clinic.Address)) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Address cannot be a PO BOX"));
			}
			if(clinic.City=="") {
				stringBuilder.AppendLine(Lans.g("DoseSpot","City is blank"));
			}
			if(string.IsNullOrWhiteSpace(clinic.State)) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","State abbreviation is blank"));
			}
			else if(clinic.State.Length<=2 && (clinic.State=="" || (clinic.State!="" && !USlocales.IsValidAbbr(clinic.State)))) {
				//Don't validate state values that are longer than 2 characters.
				stringBuilder.AppendLine(Lans.g("DoseSpot","State abbreviation is invalid"));
			}
			if(!IsZipValid(clinic.Zip)) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Zip invalid."));
			}
			if(stringBuilder.ToString().Length>0) {
				if(isPractice) {
					throw new ODException(Lans.g("DoseSpot","Issues found for practice information:")+"\r\n"+stringBuilder.ToString());
				}
				throw new ODException(Lans.g("DoseSpot","Issues found for clinic")+" "+clinic.Abbr+":\r\n"+stringBuilder.ToString());
			}
		}

		///<summary>True for #####, #####-#### or #########. Blank is invalid: DoseSpot needs a ZIP code for every clinic and patient.</summary>
		public static bool IsZipValid(string zip) {
			return Regex.IsMatch((zip??"").Trim(),@"^([0-9]{9})$|^([0-9]{5}-[0-9]{4})$|^([0-9]{5})$");
		}

		///<summary>Search the given address for a PO Box format.  Searches for the string "PO" case insensitive with or without periods. Ignores any match that has a non-space character before it (ex. "123 Ampo St" should be ignored even though it has 'po ')</summary>
		public static bool IsAddressPOBox(string address) {
			string regex=@".*( |^)P\.?O\.? .*";
			return Regex.IsMatch(address,regex,RegexOptions.IgnoreCase);
		}

		///<summary>Throws exceptions for invalid Patient data.</summary>
		public static void ValidatePatientData(long patNum) {
			ValidatePatientData(Patients.GetPat(patNum));
		}

		///<summary>Throws exceptions for invalid Patient data.</summary>
		public static void ValidatePatientData(Patient patient) {
			string primaryPhone=GetPhoneAndType(patient,0,out string phoneType);
			StringBuilder stringBuilder=new StringBuilder();
			if(patient.FName=="") {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Missing first name."));
			}
			if(patient.LName=="") {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Missing last name."));
			}
			if(patient.Birthdate.Year<1880) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Missing birthdate."));
			}
			if(patient.Birthdate>DateTime.Today) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Invalid birthdate."));
			}
			if(patient.Address.Length==0) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Missing address."));
			}
			if(patient.City.Length<2) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Invalid city."));
			}
			if(string.IsNullOrWhiteSpace(patient.State)) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Blank state abbreviation."));
			}
			else if(patient.State.Length<=2 && !USlocales.IsValidAbbr(patient.State)) {//Don't validate state values that are longer than 2 characters.
				stringBuilder.AppendLine(Lans.g("DoseSpot","Invalid state abbreviation."));
			}
			if(string.IsNullOrWhiteSpace(patient.Zip)) {
				stringBuilder.AppendLine(Lans.g("DoseSpot","Blank zip."));
			}
			else if(!IsZipValid(patient.Zip)) {//#####, #####-####, or #########
				stringBuilder.AppendLine(Lans.g("DoseSpot","Invalid zip."));
			}
			if(!IsPhoneNumberValid(primaryPhone)) {//If the primary phone number isn't valid, DoseSpot will break.
				stringBuilder.AppendLine(Lans.g("DoseSpot","Invalid phone number: ")+primaryPhone);
			}
			if(stringBuilder.ToString().Length>0) {
				throw new ODException(Lans.g("DoseSpot","Issues found for current patient:")+"\r\n"+stringBuilder.ToString());
			}
		}

		///<summary>Adds the patient to DoseSpot (saving DoseSpot's patient ID in oidexternal) or updates DoseSpot's copy, and returns the
		///OIDExternal. POST /api/patients is never retried automatically, so a lost answer never creates a duplicate patient.</summary>
		public static OIDExternal AddOrUpdatePatient(DoseSpotCredentials doseSpotCredentials,Patient patient,bool isInteractive) {
			Meth.NoCheckMiddleTierRole();
			OIDExternal oIDExternal=GetDoseSpotPatID(patient.PatNum);
			if(oIDExternal==null) {
				return CreateOIDForPatient(PIn.Int(DoseSpotRESTV2.AddPatient(doseSpotCredentials,patient,isInteractive)),patient.PatNum);
			}
			DoseSpotRESTV2.EditPatient(doseSpotCredentials,patient,oIDExternal.IDExternal,isInteractive);
			return oIDExternal;
		}

		///<summary>Set patient medication history consent for DoseSpot. Optionally pass in parameters for programErx and listProgramProperties.
		///If left null they will be evaluated and set.  Throws exceptions.</summary>
		public static void SetMedicationHistConsent(Patient patient,long clinicNum,Program program=null,List<ProgramProperty> listProgramProperties=null,
			bool isInteractive=true)
		{
			ValidatePatientData(patient);
			string doseSpotUserID=GetUserID(Security.CurUser,clinicNum);
			GetClinicIdAndKey(clinicNum,doseSpotUserID,program,listProgramProperties,out string doseSpotClinicID,out string doseSpotClinicKey);
			DoseSpotCredentials doseSpotCredentials=DoseSpotCredentials.FromConfig(doseSpotClinicID,doseSpotClinicKey,doseSpotUserID);
			OIDExternal oIDExternal=AddOrUpdatePatient(doseSpotCredentials,patient,isInteractive);
			DoseSpotRESTV2.PostMedicationHistoryConsent(doseSpotCredentials,oIDExternal.IDExternal,isInteractive);
		}

		///<summary>Valid values for ordinal are 0 (for primary),1, or 2.</summary>
		public static string GetPhoneAndType(Patient patient,int ordinal,out string phoneType) {
			List<string> listPhoneTypes=new List<string>();
			List<string> listPhoneNumbers=new List<string>();
			if(IsPhoneNumberValid(patient.HmPhone)) {
				listPhoneTypes.Add("Home");
				listPhoneNumbers.Add(patient.HmPhone);
			}
			if(IsPhoneNumberValid(patient.WirelessPhone)) {
				listPhoneTypes.Add("Cell");
				listPhoneNumbers.Add(patient.WirelessPhone);
			}
			if(IsPhoneNumberValid(patient.WkPhone)) {
				listPhoneTypes.Add("Work");
				listPhoneNumbers.Add(patient.WkPhone);
			}
			if(ordinal >= listPhoneNumbers.Count) {
				phoneType="";
				return "";
			}
			phoneType=listPhoneTypes[ordinal];
			string phoneNumber=listPhoneNumbers[ordinal].Replace("(","").Replace(")","").Replace("-","");//remove all formatting as DoseSpot doesn't allow it.
			if(phoneNumber.Length==11 && phoneNumber[0]=='1') {
				phoneNumber=phoneNumber.Substring(1);//Remove leading 1 from phone number since DoseSpot thinks that invalid.
			}
			return phoneNumber;
		}

		public static bool IsPhoneNumberValid(string phoneNumber) {
			string patternPhoneNumber=@"^1?\s*-?\s*(\d{3}|\(\s*\d{3}\s*\))\s*-?\s*\d{3}\s*-?\s*\d{4}(X\d{0,9})?";
			Regex regexPhoneNumber=new Regex(patternPhoneNumber, RegexOptions.IgnoreCase);
			if(phoneNumber!=null) {
				phoneNumber=phoneNumber.Trim();
			}
			if(string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length>=35) {//Max length of 35 is what the DoseSpot example app checks for, there is no documentation supporting it.
				return false;
			}
			if(!regexPhoneNumber.IsMatch(phoneNumber)) {//The regex was taken directly from the DoseSpot example app
				return false;
			}
			string phoneNumberDigits=Regex.Replace(phoneNumber,@"[^0-9]","");//Remove all non-digit characters.
			//Per DoseSpot on 11/15/18, any number starting with 0 or 1 will be rejected by SureScripts
			if(phoneNumberDigits.StartsWith("0") || phoneNumberDigits.StartsWith("1")) {
				return false;
			}
			if(!CheckAreaCode(phoneNumber)) {
				return false;
			}
			return true;
		}

		private static bool CheckAreaCode(string phoneNumber) {
			if(string.IsNullOrWhiteSpace(phoneNumber)) {
				return false;
			}
			phoneNumber=Regex.Replace(phoneNumber,@"[^0-9]","");//Remove all non-digit characters.
			string areaCode="";
			if(phoneNumber.Length<=3) {
				return false;
			}
			if(phoneNumber.Substring(0,1)=="1") {//Remove leading 1 for USA.
				areaCode=phoneNumber.Substring(1,3);
			}
			else {
				areaCode=phoneNumber.Substring(0,3);
			}
			//Per DoseSpot, the only invalid area code combination is 555.
			if(areaCode!="555") {
				return true;
			}
			return false;
		}
	}

	///<summary>DoseSpot's medication and prescription status codes, and the names of two legacy property rows. The v1 REST client that used to live
	///here is gone; DoseSpotRESTV2 and DoseSpotApiClient talk to API v2.</summary>
	public class DoseSpotREST {
		///<summary>Medication statuses used by DoseSpot.  Unknown(0) and Inactive(2) are depricated.  This enum intentionally starts at 0 according to DoseSpot.</summary>
		public enum MedicationStatus {
			Unknown,//Depricated by DoseSpot
			Active,
			Inactive,//Depricated by DoseSpot
			Discontinued,
			Deleted,
			Completed,
			CancelRequested,
			CancelPending,
			Cancelled,
			CancelDenied,
			ChangeD,
		}

		///<summary>Prescription statuses used by DoseSpot, not the same as MedicationStatus.  This enum intentionally starts at 1 according to DoseSpot.</summary>
		public enum PrescriptionStatus {
			Entered=1,
			Printed,
			Sending,
			eRxSent,
			FaxSent,
			Error,
			Deleted,
			Requested,
			Edited,
			EpcsError,
			EpcsSigned,
			ReadyToSign,
			PharmacyVerified,
			///<summary>Dummy status used by DoseSpotMedicationWrapper</summary>
			NotAPresciption,
		}

		public class PropertyDescs {
			///<summary>Set to 2 when eRx Setup saves. No longer read: every call uses API v2.</summary>
			public static string DoseSpotApiVersion="DoseSpotApiVersion";
			///<summary>Left untouched. Was Open Dental's v1-to-v2 migration flag.</summary>
			public static string DoseSpotApiMigrationRequested="DoseSpotApiMigrationRequested";
		}
	}


	///<summary>This is a class to reflect the response object from DoseSpot's RESTful JSON objects.  
	///Fields are nullable due to DoseSpot returning null values in some of their fields during testing, 
	///as well as to safeguard against missing fields in the future.</summary>
	public class DoseSpotPrescription {
		//DO NOT RENAME ANYTHING IN THIS CLASS. IT IS USED FOR SERIALIZING DOSESPOT API RESPONSES AND NEEDS TO MATCH THE DOSESPOT API DOCS.
		public int? PrescriptionId;
		public DateTime? WrittenDate;
		public string Directions;
		public string Quantity;
		public int? DispenseUnitId;
		public string DispenseUnitDescription;
		public string Refills;
		public int? DaysSupply;
		public int? PharmacyId;
		public string PharmacyNotes;
		public bool? NoSubstitutions;
		public DateTime? EffectiveDate;
		public DateTime? LastFillDate;
		public int? PrescriberId;
		public int? PrescriberAgentId;
		public string RxReferenceNumber;
		public DoseSpotREST.PrescriptionStatus Status;
		public bool? Formulary;
		public int? EligibilityId;
		public string Type;
		public string NonDoseSpotPrescriptionId;
		public int? PatientMedicationId;
		public DoseSpotREST.MedicationStatus MedicationStatus;
		public string Comment;
		public DateTime? DateInactive;
		public string Encounter;
		public string DoseForm;
		public string Route;
		public string Strength;
		public string GenericProductName;
		public int? LexiGenProductId;
		public int? LexiDrugSynId;
		public int? LexiSynonymTypeId;
		public string LexiGenDrugId;
		///<summary>According to US Dept. HHS RxCUI is a numeric value with max length of 8. DoseSpot sends this value as a string.</summary>
		public string RxCUI;
		public bool? OTC;
		public string NDC;
		public string Schedule;
		public string DisplayName;
		public string MonographPath;
		public string DrugClassification;
	}

	///<summary>This is a class to reflect the response object from DoseSpot's RESTful JSON objects.  
	///Fields are nullable due to DoseSpot returning null values in some of their fields during testing, 
	///as well as to safeguard against missing fields in the future.</summary>
	public class DoseSpotSelfReported {
		//DO NOT RENAME ANYTHING IN THIS CLASS. IT IS USED FOR SERIALIZING DOSESPOT API RESPONSES AND NEEDS TO MATCH THE DOSESPOT API DOCS.
		public int? SelfReportedMedicationId;
		public DateTime? DateReported;
		public DateTime? WrittenDate;//Changed from V2 to DatePrescribed
		public string Directions;//Changed from V2 to DosageText
		public string Quantity;//Changed from V2 to Dispense
		public int? DispenseUnitId;//Changed from V2 to DispenseUnitTypeId
		public string DispenseUnitDescription;//Changed from V2 to DispenseUnitType
		public string Refills;
		public int? DaysSupply;
		public int? PatientMedicationId;
		public DoseSpotREST.MedicationStatus MedicationStatus;//Changed from V2 to Status
		public string Comment;
		public DateTime? DateInactive;//Changed from V2 to DiscontinuedDate
		public string Encounter;
		public string DoseForm;
		public string Route;
		public string Strength;
		public string GenericProductName;
		public int? LexiGenProductId;
		public int? LexiDrugSynId;
		public int? LexiSynonymTypeId;
		public string LexiGenDrugId;
		public string RxCUI;
		public bool? OTC;
		public string NDC;
		public string Schedule;
		public string DisplayName;
		public string MonographPath;
		public string DrugClassification;

		//Added for DoseSpot V2 DoseSpotSelfReported return object
		[JsonProperty("DatePrescribed")]
		private DateTime? DatePrescribed {set{WrittenDate=value;}}
		[JsonProperty("DispenseUnitType")]
		private string? DispenseUnitType {set{DispenseUnitDescription=value;}}
		[JsonProperty("DosageText")]
		private string? DosageText {set{Directions=value;}}
		[JsonProperty("Dispense")]
		private string? Dispense {set{Quantity=value;}}
		[JsonProperty("DispenseUnitTypeId")]
		private int? DispenseUnitTypeId {set{DispenseUnitId=value;}}
		[JsonProperty("Status")]
		private DoseSpotREST.MedicationStatus Status {set{MedicationStatus=value;}}
		[JsonProperty("DiscontinuedDate")]
		private DateTime? DiscontinuedDate {set{DateInactive=value;}}
		private int DispensableDrugId; 
		private bool RoutedDoseFormDrugID;
		private string StateSchedules;
	}

	///<summary>Wrapper class to hold prescriptions and medications that we recieve from DoseSpot before converting into RxPats.  Not all fields 
	///for the two classes have been made into properties yet, add them as needed.</summary>
	public class DoseSpotMedicationWrapper {
		readonly DoseSpotSelfReported _doseSpotSelfReported;
		readonly DoseSpotPrescription _doseSpotPrescription;

		///<summary>True is a self reported medication, false implies a prescription.</summary>
		public bool IsSelfReported {
			get {
				return _doseSpotSelfReported!=null;
			}
		}
		public DateTime? DateInactive {
			get {
				if(IsSelfReported) {
					return _doseSpotSelfReported.DateInactive;
				}
				return _doseSpotPrescription.DateInactive;
			}
		}
		public string DisplayName {
			get {
				if(IsSelfReported) {
					return _doseSpotSelfReported.DisplayName;
				}
				return _doseSpotPrescription.DisplayName;
			}
		}
		public DateTime? DateReported {
			get {
				if(IsSelfReported) {
					return _doseSpotSelfReported.DateReported;
				}
				return _doseSpotPrescription.EffectiveDate;
			}
		}
		public string GenericProductName {
			get {
				if(IsSelfReported) {
					return _doseSpotSelfReported.GenericProductName;
				}
				return _doseSpotPrescription.GenericProductName;
			}
		}
		public DateTime? DateLastFilled {
			get {
				if(IsSelfReported) {
					return null;
				}
				return _doseSpotPrescription.LastFillDate;
			}
		}
		public long? MedicationId {
			get {
				if(IsSelfReported) {
					return _doseSpotSelfReported.SelfReportedMedicationId;
				}
				return _doseSpotPrescription.PrescriptionId;
			}
		}
		public DoseSpotREST.MedicationStatus MedicationStatus {
			get {
				if(IsSelfReported) {
					return _doseSpotSelfReported.MedicationStatus;
				}
				return _doseSpotPrescription.MedicationStatus;
			}
		}
		public int? PharmacyId {
			get {
				if(IsSelfReported) {
					return null;
				}
				return _doseSpotPrescription.PharmacyId;
			}
		}
		public string Directions {
			get {
				if(IsSelfReported) {
					return _doseSpotSelfReported.Comment;
				}
				return _doseSpotPrescription.Directions;
			}
		}
		public string RxNotes {
			get {
				if(IsSelfReported) {
					return null;
				}
				return _doseSpotPrescription.PharmacyNotes;
			}
		}
		public int? PrescriberId {
			get {
				if(IsSelfReported) {
					return null;
				}
				return _doseSpotPrescription.PrescriberId;
			}
		}
		public DoseSpotREST.PrescriptionStatus PrescriptionStatus {
			get {
				if(IsSelfReported) {
					//This makes it fall through to the base case and do nothing.
					return DoseSpotREST.PrescriptionStatus.NotAPresciption;
				}
				return _doseSpotPrescription.Status;
			}
		}
		public string Quantity {
			get {
				if(IsSelfReported) {
					return _doseSpotSelfReported.Quantity;
				}
				return _doseSpotPrescription.Quantity;
			}
		}
		public string Refills {
			get {
				if(IsSelfReported) {
					return _doseSpotSelfReported.Refills;
				}
				return _doseSpotPrescription.Refills;
			}
		}
		public long RxCUI {
			get {
				if(IsSelfReported) {
					if(!_doseSpotSelfReported.RxCUI.IsNullOrEmpty()) {
						return PIn.Long(_doseSpotSelfReported.RxCUI,false);//Cast from string to long is intentional.
					}
				}
				else {
					if(!_doseSpotPrescription.RxCUI.IsNullOrEmpty()) {
						return PIn.Long(_doseSpotPrescription.RxCUI,false);//Cast from string to long is intentional.
					}
				}
				//Something went wrong.
				return 0;
			}
		}
		public string Schedule {
			get {
				if(IsSelfReported) {
					return _doseSpotSelfReported.Schedule;
				}
				return _doseSpotPrescription.Schedule;
			}
		}
		public DateTime? DateWritten {
			get {
				if(IsSelfReported) {
					return _doseSpotSelfReported.WrittenDate;
				}
				return _doseSpotPrescription.WrittenDate;
			}
		}

		public DoseSpotMedicationWrapper(DoseSpotPrescription doseSpotPrescription, DoseSpotSelfReported doseSpotSelfReported) {
			_doseSpotPrescription=doseSpotPrescription;
			_doseSpotSelfReported=doseSpotSelfReported;
		}
	}
}
