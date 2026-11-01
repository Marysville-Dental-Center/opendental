// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Text;
using ImagingCapture.Save;
using ImagingDeviceManager.Capture;
using OpenDentBusiness;

namespace ImagingCapture.Recovery {
	///<summary>The texts of the quarantine window and Recover recent captures (design 7.1, 7.12, 10.7): the document labels, the security-log
	///entries of Assign, Reassign and Discard, and the patient lines of the confirmations. Pure text, so that every entry's wording is pinned by a
	///test. No device or vendor names; a source's ProductName appears only as the sidecar recorded it.</summary>
	public static class CaptureRecoveryText {
		public const string RecoveredLabelPrefix="Recovered: ";
		///<summary>Document.Description of a reassigned capture: it says where the image came from without naming the other patient.</summary>
		public const string ReassignedDescription="Reassigned capture";
		public const string AssignLogPrefix="Unassigned capture assigned: ";
		public const string RecoverLogPrefix="Journal capture recovered: ";
		public const string ReassignLogPrefix="Unassigned capture reassigned: ";
		public const string DiscardLogPrefix="Unassigned capture discarded: ";
		public const string PatientUnknown="patient unknown";

		///<summary>Design 7.12: the label of an unmounted item that stands in for a taken slot.</summary>
		public static string GetRecoveredLabel(string slot) {
			return RecoveredLabelPrefix+(string.IsNullOrWhiteSpace(slot) ? "slot unknown" : slot.Trim());
		}

		///<summary>"Doe, Jane (PatNum 42)", or "patient unknown".</summary>
		public static string GetPatientLogText(Patient patient) {
			if(patient==null) {
				return PatientUnknown;
			}
			return patient.GetNameLF()+" (PatNum "+ToText(patient.PatNum)+")";
		}

		///<summary>"Born 1/2/1980", or "Date of birth not entered", for the confirmations (design 7.12: name and date of birth).</summary>
		public static string GetBirthdateText(Patient patient) {
			if(patient==null || patient.Birthdate.Year<1880) {
				return "Date of birth not entered";
			}
			return "Born "+patient.Birthdate.ToShortDateString();
		}

		///<summary>"Jane Doe, born 1/2/1980 (PatNum 42)" for a confirmation, or "patient unknown". A status other than Patient follows the PatNum
		///("(PatNum 42, Archived)"), so that an archived or inactive chart does not look like the active one of the same person.</summary>
		public static string GetPatientLine(Patient patient) {
			if(patient==null) {
				return "Patient unknown";
			}
			string birthdate=GetBirthdateText(patient);
			string status="";
			if(patient.PatStatus!=PatientStatus.Patient) {
				status=", "+(patient.PatStatus==PatientStatus.NonPatient ? "Non-patient" : patient.PatStatus.ToString());
			}
			return patient.GetNameFL()+", "+char.ToLowerInvariant(birthdate[0])+birthdate.Substring(1)+" (PatNum "+ToText(patient.PatNum)+status+")";
		}

		public const string SlotUnknown="Slot unknown";
		///<summary>Owner decision 2026-11-01 (the 16-bit filing route): into its own empty slot a 16-bit capture goes as 16-bit DICOM (design 7.8);
		///anywhere else, and when reassigned, as an explicit 8-bit copy, and the 16-bit original is kept.</summary>
		public const string Gray16Note="16-bit: into its empty slot as 16-bit DICOM; anywhere else, or reassigned, as an 8-bit copy (the 16-bit original "
			+"is kept).";
		public const string Gray16NoSlotNote="16-bit with no slot to go into (no mount, the slot not recorded, or no patient): it is filed as an 8-bit "
			+"copy, and the 16-bit original is kept.";
		public const string ToothNumbersUnknown="Tooth numbers unknown";
		///<summary>A Test capture image (CaptureRecoveryFiler.IsTestCapture) is neither assigned nor reassigned.</summary>
		public const string TestCaptureNote="A Test capture image is never filed to a patient.";

		///<summary>Where the 16-bit original of entry is kept once its 8-bit copy is filed: its session folder under Capture\quarantine-resolved, or
		///Capture\quarantine-resolved\journal for a journal copy (copied there; the journal's rotation removes the journal's own).</summary>
		public static string GetOriginalKeptFolder(CaptureFileEntry entry) {
			if(entry?.Kind==CaptureFileKind.Journal) {
				return CaptureFileStore.SubfolderJournalKept;
			}
			string folderName=System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(entry?.ImagePath??"")??"")??"";
			bool isRoot=folderName=="" || string.Equals(folderName,"quarantine",StringComparison.OrdinalIgnoreCase);
			return @"Capture\quarantine-resolved"+(isRoot ? "" : "\\"+folderName);
		}

		///<summary>Assign refused because the capture would no longer go where the confirmation said (CaptureFilingResult.IsPlanChanged).</summary>
		public const string AssignPlanChanged="The slot was filled or changed meanwhile, so nothing was filed; review where the capture goes now, then "
			+"confirm again.";

		///<summary>The window's words after a filing whose document is the 8-bit copy of a 16-bit capture: " as an 8-bit copy of a 16-bit capture;
		///the 16-bit original is kept in ...", else "".</summary>
		public static string GetCopyDoneText(CaptureFilingResult result) {
			if(result==null || !result.IsCopy8BitOf16Bit) {
				return "";
			}
			return " as an 8-bit copy of a "+(result.Entry!=null && result.Entry.BitDepthInEffect==14 ? "14-bit" : "16-bit")+" capture; the 16-bit "
				+"original is kept in "+GetOriginalKeptFolder(result.Entry);
		}

		///<summary>What the confirmation says plainly before a 16-bit capture is filed as its 8-bit copy (owner decision 2026-11-01).</summary>
		public static string GetCopy8BitNote(CaptureFileEntry entry) {
			string depth=entry!=null && entry.BitDepthInEffect==14 ? "14-bit (in a 16-bit file)" : "16-bit";
			return "This is a "+depth+" capture, and it is filed as an 8-bit copy: a gray PNG made with its 0.5-99.5 % window, not the 16-bit "
				+"original. The 16-bit original and its sidecar stay in "+GetOriginalKeptFolder(entry)+" on this computer.";
		}

		///<summary>The audit entry's words for an 8-bit copy: "an 8-bit copy of a 16-bit capture (made through the window 120 to 3900 of its values;
		///the 16-bit original is kept in Capture\quarantine-resolved\...)".</summary>
		public static string GetCopyLogText(CaptureFileEntry entry,int windowLow,int windowHigh) {
			return "an "+RadiographSavePlanner.Copy8BitText+" (made through the window "+ToText(windowLow)+" to "+ToText(windowHigh)
				+" of its values; the 16-bit original is kept in "+GetOriginalKeptFolder(entry)+")";
		}

		///<summary>The target an entry is shown with: its sidecar's slot (or single-image target) when it is the image's own (IsSlotKnown). For a
		///later image of a continuous enable, or a redelivered one, whose target the session did not record, the sidecar names the target its
		///enable began at, which is not the image's: "Slot unknown (its enable began at Slot 1 (teeth 1,2))", or "Tooth numbers unknown (...)"
		///without a mount.</summary>
		public static string GetSlotText(CaptureFileEntry entry) {
			if(entry==null) {
				return "";
			}
			if(entry.IsSlotKnown) {
				return entry.Slot;
			}
			string unknown=entry.MountNum>0 ? SlotUnknown : ToothNumbersUnknown;
			return entry.Slot=="" ? unknown : unknown+" (its enable began at "+entry.Slot+")";
		}

		///<summary>The capture's file in words for the security log: "file 20261031-...-a3f9.bmp (quarantine, session ..., received
		///2026-10-31 10:00:05, source "Example Sensor DS", captureId "cap-7", sequence 3, slot Slot 3 (teeth 2,3))".</summary>
		public static string GetFileText(CaptureFileEntry entry) {
			if(entry==null) {
				return "no file";
			}
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append("file ").Append(entry.FileName).Append(" (");
			stringBuilder.Append(entry.Kind==CaptureFileKind.Quarantine ? "unassigned captures" : "capture journal");
			stringBuilder.Append(", session ").Append(entry.SessionGuid==null ? "none" : entry.SessionGuid.Value.ToString("D"));
			stringBuilder.Append(", received ").Append(entry.ReceivedAt.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture));
			stringBuilder.Append(", source ").Append(Quote(entry.Source));
			if(entry.CaptureId!="") {
				stringBuilder.Append(", captureId ").Append(Quote(entry.CaptureId));
			}
			if(entry.Sequence!=null) {
				stringBuilder.Append(", sequence ").Append(ToText(entry.Sequence.Value));
			}
			string slotText=GetSlotText(entry);
			if(slotText!="") {
				stringBuilder.Append(", slot ").Append(Clean(slotText));
			}
			stringBuilder.Append(")");
			return stringBuilder.ToString();
		}

		///<summary>Design 10.7: Assign (ImageCreate, the original patient, FKey=DocNum) with the session GUID; a journal recovery says so. copyText:
		///GetCopyLogText when the document is the 8-bit copy of a 16-bit capture, else null.</summary>
		public static string GetAssignLogText(CaptureFileEntry entry,Patient patient,string targetText,long docNum,string copyText=null) {
			string prefix=entry?.Kind==CaptureFileKind.Journal ? RecoverLogPrefix : AssignLogPrefix;
			return prefix+GetFileText(entry)+" filed for the patient the capture session was bound to, "+GetPatientLogText(patient)+", as DocNum "
				+ToText(docNum)+(string.IsNullOrEmpty(copyText) ? "" : ", "+copyText+",")+" into "+Clean(targetText)+".";
		}

		///<summary>Design 10.7: Reassign (ImageEdit) names both patients and the reason. copyText: as for GetAssignLogText.</summary>
		public static string GetReassignLogText(CaptureFileEntry entry,Patient patientFrom,Patient patientTo,string reason,long docNum,string copyText=null) {
			return ReassignLogPrefix+GetFileText(entry)+" was for "+GetPatientLogText(patientFrom)+(patientFrom==null && entry!=null && entry.PatNum>0
				? " (PatNum "+ToText(entry.PatNum)+", not found)" : "")+"; reassigned to "+GetPatientLogText(patientTo)+" as DocNum "+ToText(docNum)
				+(string.IsNullOrEmpty(copyText) ? "" : ", "+copyText)
				+", an unmounted document with no tooth numbers. Reason: "+Clean(reason).TrimEnd('.').Trim()+".";
		}

		///<summary>Design 10.7: Discard (ImageDelete) names the session, the bound patient and the file, and where the file is kept.</summary>
		public static string GetDiscardLogText(CaptureFileEntry entry,string folderKept) {
			string patient=entry==null || entry.IsPatientUnknown ? PatientUnknown : Clean(entry.PatientName)+" (PatNum "+ToText(entry.PatNum)+")";
			return DiscardLogPrefix+GetFileText(entry)+", bound patient: "+patient+". The image and its sidecar are kept in "+Clean(folderKept)+".";
		}

		///<summary>The target of an Assign in words: "Slot 3 (teeth 2,3) of the mount FMX (10/31/2026)", "an unmounted item of the mount ...
		///labelled Recovered: ...", or "a single document (teeth 8,9)".</summary>
		public static string GetTargetText(Mount mount,MountItem slot,string description,string toothNumbers) {
			if(mount==null) {
				string teeth=(toothNumbers??"").Trim();
				return "a single document"+(teeth=="" ? "" : " (teeth "+teeth+")")+(string.IsNullOrEmpty(description) ? "" : " labelled \""+description+"\"");
			}
			string mountText="the mount "+Quote(mount.Description)+" ("+mount.DateCreated.ToShortDateString()+")";
			if(slot!=null) {
				return MountPlacement.GetSlotLabel(slot)+" of "+mountText;
			}
			return "an unmounted item of "+mountText+" labelled \""+(description??"")+"\"";
		}

		private static string Quote(string text) {
			string clean=Clean(text);
			return clean=="" ? "(not recorded)" : "\""+clean.Replace("\"","'")+"\"";
		}

		private static string Clean(string text) {
			StringBuilder stringBuilder=new StringBuilder();
			foreach(char c in (text??"").Trim()) {
				stringBuilder.Append(char.IsControl(c) ? ' ' : c);
			}
			return stringBuilder.ToString();
		}

		private static string ToText(long value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}
	}
}
