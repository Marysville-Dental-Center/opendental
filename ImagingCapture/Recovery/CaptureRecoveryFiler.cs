// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ImagingCapture.Metadata;
using ImagingCapture.Save;
using ImagingCapture.Session;
using ImagingCapture.Settings;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using OpenDentBusiness;

namespace ImagingCapture.Recovery {
	///<summary>Where an Assign files a capture (design 7.12 "Assign"): the originating patient, and the original slot if it is still empty,
	///otherwise an unmounted item of the original mount labelled "Recovered: &lt;slot&gt;".</summary>
	public sealed class CaptureAssignPlan {
		///<summary>The originating patient (the sidecar's PatNum). Never another one.</summary>
		public Patient Patient { get; set; }
		///<summary>The original mount, or null for a single document (the session had no mount, or the mount no longer exists).</summary>
		public Mount Mount { get; set; }
		///<summary>The original slot when it is still empty; null otherwise.</summary>
		public MountItem Slot { get; set; }
		///<summary>With a Mount and no Slot: the taken slot, whose tooth numbers and rotation the recovered document keeps.</summary>
		public MountItem SlotRecovered { get; set; }
		///<summary>A single document's tooth numbers.</summary>
		public string ToothNumbers { get; set; }="";
		///<summary>Document.Description: "" in the original slot or as the original single document, otherwise "Recovered: &lt;slot&gt;".</summary>
		public string Description { get; set; }="";
		///<summary>The category the session saved into when it is still valid, else the mount's, else 0 (the user chooses one).</summary>
		public long DocCategory { get; set; }
		///<summary>The session's provider, else the patient's default provider (design 10.8).</summary>
		public long ProvNum { get; set; }
		///<summary>The target in words, for the confirmation and the security log.</summary>
		public string TargetText { get; set; }="";
		///<summary>Why the target is not the original slot, or "".</summary>
		public string Note { get; set; }="";

		public bool IsIntoOriginalSlot => Mount!=null && Slot!=null;

		public RadiographTarget ToRadiographTarget() {
			return new RadiographTarget { Mount=Mount,MountItem=Slot,ToothNumbers=Mount==null ? ToothNumbers : "",SlotRecovered=Mount!=null && Slot==null
				? SlotRecovered : null };
		}
	}

	public enum CaptureRecoveryAction {
		Assign,
		Reassign,
		Discard,
	}

	///<summary>What one Assign, Reassign or Discard did.</summary>
	public sealed class CaptureFilingResult {
		public CaptureRecoveryAction Action { get; set; }
		public CaptureFileEntry Entry { get; set; }
		///<summary>The capture was filed (or discarded). False: Error says why, and nothing was changed.</summary>
		public bool IsDone { get; set; }
		public string Error { get; set; }="";
		///<summary>Problems after the filing that did not undo it (a security-log entry, the file's move), for the window.</summary>
		public List<string> ListWarnings { get; set; }=new List<string>();
		///<summary>The patient the capture was filed for (0 for a Discard).</summary>
		public long PatNum { get; set; }
		public Document Document { get; set; }
		public Mount Mount { get; set; }
		///<summary>The slot, or the new unmounted item, the document went into; null for a single document.</summary>
		public MountItem MountItem { get; set; }
		public bool IsMountItemCreated { get; set; }
		public string TargetText { get; set; }="";
		///<summary>Where the file is now (Capture\quarantine-resolved for a quarantine file).</summary>
		public string PathResolved { get; set; }

		public static CaptureFilingResult Refused(CaptureRecoveryAction action,CaptureFileEntry entry,string error) {
			return new CaptureFilingResult { Action=action,Entry=entry,Error=error??"" };
		}
	}

	///<summary>The filing rules of the quarantine window and Recover recent captures (design 7.1, 7.12, 10.6, 10.7). UI thread only. Each action
	///checks its permission itself, so the window cannot skip it:
	///- Assign (ImageCreate): only to the originating patient, never for "patient unknown"; a journal copy only from that patient's capture
	///  session. Into the original slot if it is still empty, otherwise an unmounted item of the original mount labelled "Recovered: &lt;slot&gt;".
	///  Saved through RadiographImport, so CAP-3's 8-bit PNG and DICOM rules apply, and a 16-bit capture only under the session's 16-bit conditions
	///  (CaptureBitDepthDecision for its device and target). Logged under ImageCreate with the session GUID.
	///- Reassign (ImageEdit, date-restricted by the receive time): to a patient the user found with the patient search (the caller passes it; this
	///  class never picks one), with a typed reason, as an unmounted document with no tooth numbers. Logged under ImageEdit for the target
	///  patient with both patients and the reason, and the same text for the originating patient when known.
	///- Discard (ImageDelete, date-restricted by the receive time): quarantine files only. Logged first, under the bound patient (or none), naming
	///  the session, the patient and the file; a discard that cannot be logged is not done.
	///A filed or discarded file is moved out of the quarantine with its disposition (CaptureFileStore.Resolve); nothing is deleted.</summary>
	public sealed class CaptureRecoveryFiler {
		private readonly CaptureRecoveryServices _services;
		private readonly RadiographImport _radiographImport;

		public CaptureRecoveryFiler(CaptureRecoveryServices services) {
			_services=services??throw new ArgumentNullException(nameof(services));
			_radiographImport=new RadiographImport(services.RadiographStore,() => Now);
		}

		///<summary>Why a capture is filed with no slot and no tooth numbers.</summary>
		public const string SlotUnknownNote="The session did not record which slot this image was for (a later image of a continuous series, or a "
			+"redelivered image), so it is filed unmounted with no tooth numbers; set them after checking the image.";

		private ICaptureRecoveryStore Store => _services.Store;
		private DateTime Now => _services.FuncNow?.Invoke()??DateTime.Now;

		#region Assign
		///<summary>Whether Assign is offered for entry at all (design 7.12, 10.6): a capture with an originating patient that was not filed yet; a
		///journal copy only when patNumSession is that patient (Recover recent captures in the patient's session). A redelivered image the operator
		///did not confirm is "patient unknown" whatever PatNum its sidecar has (a sidecar written before the library left it out): Reassign only.</summary>
		public static bool IsAssignOffered(CaptureFileEntry entry,long patNumSession) {
			if(entry==null || entry.IsPatientUnknown || entry.IsResolved || IsRedeliveredUnconfirmed(entry)) {
				return false;
			}
			if(entry.Kind==CaptureFileKind.Journal) {
				return !IsJournalCopyOfQuarantined(entry) && patNumSession>0 && entry.PatNum==patNumSession;
			}
			return true;
		}

		///<summary>Design 7.12: a redelivered image is placed only after the operator confirmed it; until then its patient is unknown.</summary>
		public static bool IsRedeliveredUnconfirmed(CaptureFileEntry entry) {
			return entry!=null && entry.IsRedelivered && !entry.IsRedeliveryConfirmed;
		}

		///<summary>A journal copy whose image went to the quarantine is filed from there, never from the journal, so that one exposure is not filed
		///twice.</summary>
		public static bool IsJournalCopyOfQuarantined(CaptureFileEntry entry) {
			return entry!=null && entry.IsCopyOfQuarantined;
		}

		///<summary>Where Assign would file entry now, or null with error. Reads the mount and its documents, so the slot rule uses the current
		///state; Assign plans again before it saves.</summary>
		public CaptureAssignPlan PlanAssign(CaptureFileEntry entry,long patNumSession,out string error) {
			error="";
			if(!IsAssignOffered(entry,patNumSession)) {
				error=GetAssignNotOfferedText(entry,patNumSession);
				return null;
			}
			Patient patient=Store.GetPatient(entry.PatNum);
			if(patient==null) {
				error="The originating patient (PatNum "+entry.PatNum+") no longer exists. Use Reassign.";
				return null;
			}
			if(!CheckNoDocumentYet(entry,patient,out error)) {
				return null;
			}
			CaptureAssignPlan plan=new CaptureAssignPlan { Patient=patient };
			//A later image of a continuous enable, or a redelivered one, whose target the session did not record: its sidecar names the slot its
			//enable started for, which is not its own, so neither that slot nor its teeth and rotation are used.
			bool isSlotKnown=entry.IsSlotKnown;
			string slotLabel=isSlotKnown ? entry.Slot : "";
			string toothNumbers=isSlotKnown ? entry.ToothNumbers : "";
			Mount mount=entry.MountNum>0 ? Store.GetMount(entry.MountNum) : null;
			if(entry.MountNum>0 && (mount==null || mount.PatNum!=patient.PatNum)) {
				//The mount was deleted (or is not the patient's any more): the image keeps its slot's teeth on a single document.
				plan.ToothNumbers=toothNumbers;
				plan.Description=CaptureRecoveryText.GetRecoveredLabel(slotLabel);
				plan.Note="The original mount (MountNum "+entry.MountNum+") no longer exists."+(isSlotKnown ? "" : " "+SlotUnknownNote);
			}
			else if(mount!=null && !isSlotKnown) {
				plan.Mount=mount;
				plan.SlotRecovered=new MountItem { MountNum=mount.MountNum,ItemOrder=0,ToothNumbers="" };
				plan.Description=CaptureRecoveryText.GetRecoveredLabel("");
				plan.Note=SlotUnknownNote;
			}
			else if(mount!=null) {
				plan.Mount=mount;
				List<MountItem> listMountItems=Store.GetMountItems(mount.MountNum)??new List<MountItem>();
				List<Document> listDocuments=Store.GetDocumentsForMountItems(listMountItems)??new List<Document>();
				int idx=FindSlot(listMountItems,entry);
				MountItem mountItem=idx==-1 ? null : listMountItems[idx];
				bool isEmpty=idx!=-1 && idx<listDocuments.Count && listDocuments[idx]==null;
				if(mountItem!=null && MountPlacement.IsSlot(mountItem) && isEmpty) {
					plan.Slot=mountItem;
				}
				else {
					plan.SlotRecovered=mountItem!=null && MountPlacement.IsSlot(mountItem) ? mountItem
						: new MountItem { MountNum=mount.MountNum,ItemOrder=entry.SlotItemOrder,ToothNumbers=entry.ToothNumbers };
					plan.Description=CaptureRecoveryText.GetRecoveredLabel(slotLabel);
					plan.Note=mountItem==null ? "The original slot is no longer in the mount." : "The original slot already holds an image.";
				}
			}
			else {
				plan.ToothNumbers=toothNumbers;//a single image of the session: its banner's teeth
			}
			plan.DocCategory=GetDefaultCategory(entry,mount);
			plan.ProvNum=entry.ProvNum>0 ? entry.ProvNum : GetDefaultProvNum(patient);
			plan.TargetText=CaptureRecoveryText.GetTargetText(plan.Mount,plan.Slot,plan.Description,plan.ToothNumbers);
			return plan;
		}

		///<summary>Files entry for its originating patient (see the class). docCategory 0: the plan's. patNumSession: the capture session's patient,
		///or 0 outside a session (then a journal copy is refused).</summary>
		public CaptureFilingResult Assign(CaptureFileEntry entry,long docCategory,long patNumSession) {
			const CaptureRecoveryAction action=CaptureRecoveryAction.Assign;
			string message;
			if(!IsAuthorized(EnumPermType.ImageCreate,DateTime.MinValue,out message)) {
				return CaptureFilingResult.Refused(action,entry,message);
			}
			string error;
			CaptureAssignPlan plan=PlanAssign(entry,patNumSession,out error);
			if(plan==null) {
				return CaptureFilingResult.Refused(action,entry,error);
			}
			long category=docCategory>0 ? docCategory : plan.DocCategory;
			if(category<=0) {
				return CaptureFilingResult.Refused(action,entry,"Image category missing; choose a category.");
			}
			AcquiredImage image=_services.FileStore.ReadImage(entry,out error);
			if(image==null) {
				return CaptureFilingResult.Refused(action,entry,error);
			}
			RadiographTarget target=plan.ToRadiographTarget();
			RadiographSaveRequest request=BuildRequest(entry,image,plan.Patient,category,plan.ProvNum,target,plan.Description);
			if(image.IsGray16 && !Apply16BitRules(entry,target,request,out error)) {
				return CaptureFilingResult.Refused(action,entry,error);
			}
			RadiographSaveResult resultSave=_radiographImport.Save(request);
			if(!resultSave.IsSaved) {
				return CaptureFilingResult.Refused(action,entry,"The image was not filed: "+(resultSave.Error??"").Trim());
			}
			CaptureFilingResult result=NewResult(action,entry,plan.Patient.PatNum,resultSave,plan.Mount,plan.TargetText);
			WriteLog(EnumPermType.ImageCreate,plan.Patient.PatNum,CaptureRecoveryText.GetAssignLogText(entry,plan.Patient,plan.TargetText,
				resultSave.Document.DocNum),resultSave.Document.DocNum,result);
			Resolve(entry,CaptureFileDisposition.Assigned,new List<KeyValuePair<string,string>> {
				Pair("DispositionUser",Store.GetUserName()),
				Pair("DispositionPatNum",ToText(plan.Patient.PatNum)),
				Pair("DispositionDocNum",ToText(resultSave.Document.DocNum)),
				Pair("DispositionTarget",plan.TargetText),
			},result);
			return result;
		}

		private static string GetAssignNotOfferedText(CaptureFileEntry entry,long patNumSession) {
			if(entry==null) {
				return "No capture is selected.";
			}
			if(entry.IsResolved) {
				return "This capture was already filed.";
			}
			if(IsJournalCopyOfQuarantined(entry)) {
				return "This copy's image is in unassigned captures; file it from there.";
			}
			if(entry.IsPatientUnknown || IsRedeliveredUnconfirmed(entry)) {
				return "This capture has no originating patient, so it can only be reassigned to a patient you find.";
			}
			return "This journal copy belongs to another patient's session (PatNum "+entry.PatNum+"), so it can only be reassigned.";
		}

		///<summary>Design 10.6 "Duplicate capture": one document per TWAIN transfer. False with error when the patient already has a document of this
		///exposure (CaptureJournalMatch: its captureId in an ImageCreate entry, or its time): its journal copy was filed before, another computer
		///or window assigned the file, or its earlier move out of the quarantine failed. Also false when the documents cannot be read, because then
		///nothing can be said to be missing.</summary>
		private bool CheckNoDocumentYet(CaptureFileEntry entry,Patient patient,out string error) {
			Document document;
			string how;
			if(!FindDocumentOfExposure(entry,patient,out document,out how,out error)) {
				return false;
			}
			if(document==null) {
				return true;
			}
			error="The patient already has a document of this exposure (DocNum "+ToText(document.DocNum)+", matched by its "
				+(how=="captureId" ? "capture ID" : "time")+"), so it is not filed again.";
			return false;
		}

		///<summary>Design 10.6 "One document per TWAIN transfer", for Reassign: the exposure is not a document of the patient it goes to, nor of its
		///originating patient (a journal copy of an image saved normally, a confirmed redelivered image whose copy records no patient). False with
		///error when it is, or when the documents cannot be read.</summary>
		private bool CheckNotFiledForEither(CaptureFileEntry entry,Patient patientTarget,out string error) {
			List<Patient> listPatients=new List<Patient> { patientTarget };
			if(entry.PatNum>0 && entry.PatNum!=patientTarget.PatNum) {
				Patient patientFrom=Store.GetPatient(entry.PatNum);
				if(patientFrom!=null) {
					listPatients.Add(patientFrom);
				}
			}
			foreach(Patient patient in listPatients) {
				Document document;
				string how;
				if(!FindDocumentOfExposure(entry,patient,out document,out how,out error)) {
					return false;
				}
				if(document!=null) {
					error="This exposure is already DocNum "+ToText(document.DocNum)+" of "+patient.GetNameFL()+" (PatNum "+ToText(patient.PatNum)
						+", matched by its "+(how=="captureId" ? "capture ID" : "time")+"); move that document in the Imaging module instead.";
					return false;
				}
			}
			error="";
			return true;
		}

		///<summary>The document of patient that entry was saved as (CaptureJournalMatch), or null in document. False with error when the
		///patient's documents cannot be read.</summary>
		private bool FindDocumentOfExposure(CaptureFileEntry entry,Patient patient,out Document document,out string how,out string error) {
			document=null;
			how="";
			error="";
			try {
				List<Document> listDocuments=Store.GetPatientDocuments(patient.PatNum)??new List<Document>();
				List<SecurityLog> listSecurityLogs=Store.GetImageCreateLogEntries(patient.PatNum,
					CaptureJournalMatch.GetDocNumsToCheck(new[] { entry },listDocuments))??new List<SecurityLog>();
				document=CaptureJournalMatch.FindDocument(entry,listDocuments,listSecurityLogs,out how);
				return true;
			}
			catch(Exception ex) {
				error="The patient's documents could not be read, so it cannot be checked that this capture was not filed already: "+ex.Message;
				return false;
			}
		}

		///<summary>The original slot: by MountItemNum, else by its ItemOrder.</summary>
		private static int FindSlot(List<MountItem> listMountItems,CaptureFileEntry entry) {
			if(entry.MountItemNum>0) {
				int idx=listMountItems.FindIndex(x => x!=null && x.MountItemNum==entry.MountItemNum);
				if(idx!=-1) {
					return idx;
				}
			}
			if(entry.SlotItemOrder>0) {
				return listMountItems.FindIndex(x => x!=null && x.ItemOrder==entry.SlotItemOrder);
			}
			return -1;
		}

		private long GetDefaultCategory(CaptureFileEntry entry,Mount mount) {
			if(entry.DocCategory>0 && IsCategoryValid(entry.DocCategory)) {
				return entry.DocCategory;
			}
			if(mount!=null && mount.DocCategory>0 && IsCategoryValid(mount.DocCategory)) {
				return mount.DocCategory;
			}
			return 0;
		}

		///<summary>Design 7.8 "When 16-bit is enabled": the same decision as the session's for this device, this computer and this target. A 16-bit
		///capture is never downconverted; one that may not be stored as DICOM stays where it is.</summary>
		private bool Apply16BitRules(CaptureFileEntry entry,RadiographTarget target,RadiographSaveRequest request,out string error) {
			error="";
			long imagingDeviceNum=entry.ImagingDeviceNum;
			LocalCaptureConfig config=_services.FuncLoadConfig?.Invoke()??LocalCaptureConfig.Load((string)null);
			TwainCaptureOptions options=TwainCaptureOptions.FromConfig(config,imagingDeviceNum);
			string computerName=_services.FuncGetComputerName?.Invoke()??"";
			ImagingDevice imagingDevice=imagingDeviceNum>0 ? Store.GetImagingDevice(imagingDeviceNum) : null;
			List<SecurityLog> listSetupLogs=imagingDeviceNum>0 ? Store.GetSetupLogEntries(imagingDeviceNum) : new List<SecurityLog>();
			CaptureVerificationRecord record=imagingDeviceNum>0 ? CaptureVerificationRecords.FindLatest(listSetupLogs,imagingDeviceNum,computerName) : null;
			CaptureSettingsCheck settingsCheck=imagingDeviceNum>0
				? CaptureSettingsCheck.Check(config,imagingDeviceNum,computerName,listSetupLogs)
				: CaptureSettingsCheck.Unknown(imagingDeviceNum,computerName,"the capture names no imaging device");
			CaptureBitDepthChoice choice=CaptureBitDepthDecision.Decide(new CaptureBitDepthInput {
				Options=options,
				Record=record,
				ImagingDeviceNum=imagingDeviceNum,
				ComputerName=computerName,
				TwainName=imagingDevice?.TwainName??"",
				GatedSettingsHash=settingsCheck.CurrentHash,
				SettingsCheck=settingsCheck,
				Target=target.ToIntraOralTarget(),
			});
			if(choice.Bits!=16) {
				error="This 16-bit capture can be filed only where the capture session could store it at 16 bits, and it is never converted to 8 bits. "
					+(choice.Reason8Bit??"").Trim()+" It stays where it is.";
				return false;
			}
			request.Is16BitAllowed=true;
			request.Polarity=options.Polarity;
			request.IsInversionVerified=choice.IsInversionVerified;
			return true;
		}
		#endregion Assign

		#region Reassign
		///<summary>Whether Reassign is offered: any capture that was not filed yet and can be read back (a .dcm cannot). A 16-bit capture is
		///offered, and refused with the reason when it is chosen.</summary>
		public static bool IsReassignOffered(CaptureFileEntry entry) {
			return entry!=null && !entry.IsResolved && entry.Extension!=".dcm" && !IsJournalCopyOfQuarantined(entry);
		}

		///<summary>Files entry for patientTarget, whom the user found with the patient search (design 7.12 "Reassign"): ImageEdit, a typed reason,
		///an unmounted document with no tooth numbers, logged with both patients and the reason.</summary>
		public CaptureFilingResult Reassign(CaptureFileEntry entry,Patient patientTarget,string reason,long docCategory) {
			const CaptureRecoveryAction action=CaptureRecoveryAction.Reassign;
			if(!IsReassignOffered(entry)) {
				return CaptureFilingResult.Refused(action,entry,entry?.IsResolved==true ? "This capture was already filed." : "This file cannot be reassigned.");
			}
			string message;
			if(!IsAuthorized(EnumPermType.ImageEdit,entry.ReceivedAt,out message)) {
				return CaptureFilingResult.Refused(action,entry,message);
			}
			if(patientTarget==null || patientTarget.PatNum<=0) {
				return CaptureFilingResult.Refused(action,entry,"Find the patient to reassign it to first.");
			}
			string reasonTrimmed=(reason??"").Trim();
			if(reasonTrimmed=="") {
				return CaptureFilingResult.Refused(action,entry,"Type the reason for the reassignment.");
			}
			if(entry.Kind==CaptureFileKind.Quarantine && !entry.IsPatientUnknown && !IsRedeliveredUnconfirmed(entry) && patientTarget.PatNum==entry.PatNum) {
				return CaptureFilingResult.Refused(action,entry,"This is the capture's originating patient: use Assign, which keeps its slot.");
			}
			if(entry.Kind==CaptureFileKind.Journal && !entry.IsPatientUnknown && !IsRedeliveredUnconfirmed(entry) && patientTarget.PatNum==entry.PatNum) {
				//Reassign is for a different patient (design 7.12). Recover recent captures in a session of the patient files a copy that has no
				//document yet, into its slot.
				return CaptureFilingResult.Refused(action,entry,"This is the copy's originating patient: use Recover recent captures in a capture session "
					+"for the patient, which files only a copy that has no document yet, into its slot.");
			}
			string errorFiled;
			if(!CheckNotFiledForEither(entry,patientTarget,out errorFiled)) {
				return CaptureFilingResult.Refused(action,entry,errorFiled);
			}
			if(docCategory<=0 || !IsCategoryValid(docCategory)) {
				return CaptureFilingResult.Refused(action,entry,"Image category missing; choose a category.");
			}
			string error;
			AcquiredImage image=_services.FileStore.ReadImage(entry,out error);
			if(image==null) {
				return CaptureFilingResult.Refused(action,entry,error);
			}
			if(image.IsGray16) {
				return CaptureFilingResult.Refused(action,entry,"A 16-bit capture is stored only as DICOM in its mount slot (design 7.8), and an unmounted "
					+"document has no slot, so it cannot be reassigned. It stays where it is.");
			}
			Patient patientFrom=entry.IsPatientUnknown ? null : Store.GetPatient(entry.PatNum);
			RadiographTarget target=new RadiographTarget { ToothNumbers="" };
			RadiographSaveRequest request=BuildRequest(entry,image,patientTarget,docCategory,GetDefaultProvNum(patientTarget),target,
				CaptureRecoveryText.ReassignedDescription);
			RadiographSaveResult resultSave=_radiographImport.Save(request);
			if(!resultSave.IsSaved) {
				return CaptureFilingResult.Refused(action,entry,"The image was not filed: "+(resultSave.Error??"").Trim());
			}
			string targetText=CaptureRecoveryText.GetTargetText(null,null,CaptureRecoveryText.ReassignedDescription,"");
			CaptureFilingResult result=NewResult(action,entry,patientTarget.PatNum,resultSave,null,targetText);
			long docNum=resultSave.Document.DocNum;
			string logText=CaptureRecoveryText.GetReassignLogText(entry,patientFrom,patientTarget,reasonTrimmed,docNum);
			WriteLog(EnumPermType.ImageEdit,patientTarget.PatNum,logText,docNum,result);
			if(patientFrom!=null && patientFrom.PatNum!=patientTarget.PatNum) {
				WriteLog(EnumPermType.ImageEdit,patientFrom.PatNum,logText,0,result);//the originating patient's audit shows where the image went
			}
			Resolve(entry,CaptureFileDisposition.Reassigned,new List<KeyValuePair<string,string>> {
				Pair("DispositionUser",Store.GetUserName()),
				Pair("DispositionPatNumFrom",entry.IsPatientUnknown ? "" : ToText(entry.PatNum)),
				Pair("DispositionPatNum",ToText(patientTarget.PatNum)),
				Pair("DispositionDocNum",ToText(docNum)),
				Pair("DispositionReason",reasonTrimmed),
			},result);
			return result;
		}
		#endregion Reassign

		#region Discard
		///<summary>Discard is offered for a quarantine file that was not filed. A journal copy is not discarded: rotation removes it.</summary>
		public static bool IsDiscardOffered(CaptureFileEntry entry) {
			return entry!=null && !entry.IsResolved && entry.Kind==CaptureFileKind.Quarantine;
		}

		///<summary>Design 7.12 "Discard": ImageDelete, a security-log entry naming the session, the bound patient and the file, written before the
		///file moves out of the quarantine (CaptureFileStore.Resolve keeps it in Capture\quarantine-resolved).</summary>
		public CaptureFilingResult Discard(CaptureFileEntry entry) {
			const CaptureRecoveryAction action=CaptureRecoveryAction.Discard;
			if(!IsDiscardOffered(entry)) {
				return CaptureFilingResult.Refused(action,entry,entry?.Kind==CaptureFileKind.Journal
					? "A journal copy is not discarded; the journal's rotation removes it." : "This capture was already filed or discarded.");
			}
			string message;
			if(!IsAuthorized(EnumPermType.ImageDelete,entry.ReceivedAt,out message)) {
				return CaptureFilingResult.Refused(action,entry,message);
			}
			if(!File.Exists(entry.ImagePath)) {
				return CaptureFilingResult.Refused(action,entry,"The file "+entry.FileName+" is no longer there (another user may have filed it).");
			}
			string folderKept=GetResolvedFolderText(entry);
			try {
				Store.MakeLogEntry(EnumPermType.ImageDelete,entry.IsPatientUnknown ? 0 : entry.PatNum,CaptureRecoveryText.GetDiscardLogText(entry,folderKept),0);
			}
			catch(Exception ex) {
				return CaptureFilingResult.Refused(action,entry,"The discard could not be written to the security log, so the capture was not discarded: "
					+ex.Message);
			}
			CaptureFilingResult result=new CaptureFilingResult { Action=action,Entry=entry,IsDone=true,PatNum=0 };
			string pathResolved;
			string error;
			if(!_services.FileStore.Resolve(entry,CaptureFileDisposition.Discarded,new List<KeyValuePair<string,string>> {
				Pair("DispositionUser",Store.GetUserName()),
			},out pathResolved,out error))
			{
				//Logged, but still in the quarantine: the window says so, and it can be discarded again.
				result.IsDone=false;
				result.Error="The discard was logged, but the file could not be moved out of unassigned captures: "+error;
				Log("Discard of "+entry.ImagePath+" logged, but the file was not moved: "+error);
				return result;
			}
			result.PathResolved=pathResolved;
			if(error!="") {
				result.ListWarnings.Add(error);
			}
			Log("Discarded "+entry.ImagePath+" (kept as "+pathResolved+").");
			return result;
		}
		#endregion Discard

		#region Methods - private
		private RadiographSaveRequest BuildRequest(CaptureFileEntry entry,AcquiredImage image,Patient patient,long docCategory,long provNum,
			RadiographTarget target,string description)
		{
			ImagingDevice imagingDevice=null;
			try {
				imagingDevice=entry.ImagingDeviceNum>0 ? Store.GetImagingDevice(entry.ImagingDeviceNum) : null;
			}
			catch(Exception ex) {
				Log("The imaging device "+entry.ImagingDeviceNum+" could not be read: "+ex.Message);
			}
			return new RadiographSaveRequest {
				Image=image,
				Metadata=CaptureMetadataParser.Parse(image.MetadataJson),
				Patient=patient,
				DocCategory=docCategory,
				ProvNum=provNum,
				Target=target,
				Description=description,
				SessionGuid=entry.SessionGuid??Guid.Empty,
				DeviceDescription=imagingDevice?.Description??"",
				ImagingDeviceNum=entry.ImagingDeviceNum,
			};
		}

		private static CaptureFilingResult NewResult(CaptureRecoveryAction action,CaptureFileEntry entry,long patNum,RadiographSaveResult resultSave,
			Mount mount,string targetText)
		{
			CaptureFilingResult result=new CaptureFilingResult {
				Action=action,
				Entry=entry,
				IsDone=true,
				PatNum=patNum,
				Document=resultSave.Document,
				Mount=mount,
				MountItem=resultSave.MountItem,
				IsMountItemCreated=resultSave.IsMountItemCreated,
				TargetText=targetText,
			};
			result.ListWarnings.AddRange(resultSave.ListWarnings);
			return result;
		}

		private void WriteLog(EnumPermType permType,long patNum,string logText,long fKey,CaptureFilingResult result) {
			try {
				Store.MakeLogEntry(permType,patNum,logText,fKey);
			}
			catch(Exception ex) {
				result.ListWarnings.Add("The security-log entry could not be written: "+ex.Message);
				Log("Security log failed ("+ex.Message+"): "+logText);
			}
		}

		///<summary>Moves a filed capture out of the quarantine (or marks its journal copy). A failure leaves the document filed: the window says so,
		///and the item is not offered again in this window.</summary>
		private void Resolve(CaptureFileEntry entry,CaptureFileDisposition disposition,List<KeyValuePair<string,string>> listFields,CaptureFilingResult result) {
			string pathResolved;
			string error;
			if(!_services.FileStore.Resolve(entry,disposition,listFields,out pathResolved,out error)) {
				result.ListWarnings.Add("The image was filed as DocNum "+result.Document?.DocNum+", but its file could not be marked as filed, so it may be "
					+"listed again: "+error);
				Log(disposition+" "+entry.ImagePath+" as DocNum "+result.Document?.DocNum+"; the file could not be marked: "+error);
				return;
			}
			result.PathResolved=pathResolved;
			if(error!="") {
				result.ListWarnings.Add(error);
			}
			Log(disposition+" "+entry.ImagePath+" as DocNum "+result.Document?.DocNum+" for PatNum "+result.PatNum+".");
		}

		private bool IsAuthorized(EnumPermType permType,DateTime date,out string message) {
			try {
				if(Store.IsAuthorized(permType,date,out message)) {
					return true;
				}
				if(string.IsNullOrWhiteSpace(message)) {
					message="Not authorized for "+permType+".";
				}
				return false;
			}
			catch(Exception ex) {
				message="The permission could not be checked: "+ex.Message;
				return false;
			}
		}

		private bool IsCategoryValid(long defNum) {
			try {
				return Store.IsCategoryValid(defNum);
			}
			catch {
				return false;
			}
		}

		private long GetDefaultProvNum(Patient patient) {
			try {
				return Store.GetDefaultProvNum(patient);
			}
			catch(Exception ex) {
				Log("The default provider could not be read: "+ex.Message);
				return patient?.PriProv??0;
			}
		}

		///<summary>Where CaptureFileStore.Resolve keeps a discarded file: the same session folder under Capture\quarantine-resolved.</summary>
		private static string GetResolvedFolderText(CaptureFileEntry entry) {
			string folderName=Path.GetFileName(Path.GetDirectoryName(entry.ImagePath)??"")??"";
			bool isRoot=string.Equals(folderName,"quarantine",StringComparison.OrdinalIgnoreCase);
			return @"Capture\quarantine-resolved"+(isRoot || folderName=="" ? "" : "\\"+folderName);
		}

		private void Log(string text) {
			try {
				_services.ActionWriteLog?.Invoke("Unassigned captures: "+text);
			}
			catch {
			}
		}

		private static KeyValuePair<string,string> Pair(string key,string value) {
			return new KeyValuePair<string,string>(key,value??"");
		}

		private static string ToText(long value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}
		#endregion Methods - private
	}
}
