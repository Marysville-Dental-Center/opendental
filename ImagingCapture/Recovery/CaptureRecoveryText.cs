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

		///<summary>"Jane Doe, born 1/2/1980 (PatNum 42)" for a confirmation, or "patient unknown".</summary>
		public static string GetPatientLine(Patient patient) {
			if(patient==null) {
				return "Patient unknown";
			}
			string birthdate=GetBirthdateText(patient);
			return patient.GetNameFL()+", "+char.ToLowerInvariant(birthdate[0])+birthdate.Substring(1)+" (PatNum "+ToText(patient.PatNum)+")";
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
			if(entry.Slot!="") {
				stringBuilder.Append(", slot ").Append(Clean(entry.Slot));
			}
			stringBuilder.Append(")");
			return stringBuilder.ToString();
		}

		///<summary>Design 10.7: Assign (ImageCreate, the original patient, FKey=DocNum) with the session GUID; a journal recovery says so.</summary>
		public static string GetAssignLogText(CaptureFileEntry entry,Patient patient,string targetText,long docNum) {
			string prefix=entry?.Kind==CaptureFileKind.Journal ? RecoverLogPrefix : AssignLogPrefix;
			return prefix+GetFileText(entry)+" filed for the patient the capture session was bound to, "+GetPatientLogText(patient)+", as DocNum "
				+ToText(docNum)+" into "+Clean(targetText)+".";
		}

		///<summary>Design 10.7: Reassign (ImageEdit) names both patients and the reason.</summary>
		public static string GetReassignLogText(CaptureFileEntry entry,Patient patientFrom,Patient patientTo,string reason,long docNum) {
			return ReassignLogPrefix+GetFileText(entry)+" was for "+GetPatientLogText(patientFrom)+(patientFrom==null && entry!=null && entry.PatNum>0
				? " (PatNum "+ToText(entry.PatNum)+", not found)" : "")+"; reassigned to "+GetPatientLogText(patientTo)+" as DocNum "+ToText(docNum)
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
