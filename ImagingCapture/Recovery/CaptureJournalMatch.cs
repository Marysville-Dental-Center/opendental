// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ImagingCapture.Metadata;
using ImagingCapture.Save;
using ImagingDeviceManager.Capture;
using OpenDentBusiness;

namespace ImagingCapture.Recovery {
	///<summary>Whether a journal copy already has its document (design 7.1: Recover recent captures lists only journal copies "that have no
	///matching document yet"). A copy matches a document of its patient in either of two ways:
	///- captureId: the copy's sidecar has the source's captureId, and an ImageCreate entry of the patient with FKey=DocNum names it (the 8-bit
	///  saver writes captureId into its "Capture:" entry, design 7.9, and so does a recovery), for a document that still exists. A redelivered
	///  image keeps its captureId (design 7.9), so this also finds the earlier copy.
	///- time: a document of the patient whose DateCreated is within 1 second of the time the saver would have given the copy
	///  (RadiographSavePlanner.GetDateCreated: the metadata's acquiredAt, else the receive time). DateCreated keeps whole seconds; two exposures
	///  of one patient within a second do not happen, and a redelivered image keeps its acquiredAt. This covers 16-bit DICOM saves, which write
	///  no "Capture:" entry, and copies without metadata.
	///A pixel hash is not used: the document's file is a PNG or DICOM made from the pixels, not the journal's bytes, and comparing pixels would
	///mean reading every image of the patient. The pixel size is not used either: the document row does not store it. A document deleted on
	///purpose (a rejected retake) no longer matches, so its copy is listed again; listing is harmless, filing needs a deliberate Assign.</summary>
	public static class CaptureJournalMatch {
		///<summary>DateCreated is a whole-second column; within this the times are the same.</summary>
		public const double ToleranceSeconds=1;

		///<summary>The DateCreated the saver gives an image saved from entry (design 10.8).</summary>
		public static DateTime GetExpectedDateCreated(CaptureFileEntry entry) {
			CaptureMetadata metadata=CaptureMetadataParser.Parse(entry?.MetadataJson);
			DateTime receivedAt=entry?.ReceivedAt??DateTime.MinValue;
			return RadiographSavePlanner.GetDateCreated(metadata,receivedAt,receivedAt);
		}

		///<summary>The document of the patient that entry was saved as, or null. how says which rule matched ("captureId" or "time"). listDocuments:
		///the patient's documents; listSecurityLogs: their ImageCreate entries (FKey=DocNum).</summary>
		public static Document FindDocument(CaptureFileEntry entry,IList<Document> listDocuments,IList<SecurityLog> listSecurityLogs,out string how) {
			how="";
			if(entry==null || listDocuments==null || listDocuments.Count==0) {
				return null;
			}
			Dictionary<long,Document> dictDocuments=new Dictionary<long,Document>();
			foreach(Document document in listDocuments.Where(x => x!=null)) {
				dictDocuments[document.DocNum]=document;
			}
			string captureId=entry.CaptureId;
			if(captureId!="" && listSecurityLogs!=null) {
				string fragment="captureId "+QuoteLikeTheSaver(captureId);
				foreach(SecurityLog securityLog in listSecurityLogs.Where(x => x!=null && x.FKey!=0)) {
					Document document;
					if(securityLog.LogText!=null && securityLog.LogText.Contains(fragment) && dictDocuments.TryGetValue(securityLog.FKey,out document)) {
						how="captureId";
						return document;
					}
				}
			}
			DateTime expected=Truncate(GetExpectedDateCreated(entry));
			if(expected.Year<1880) {
				return null;
			}
			foreach(Document document in dictDocuments.Values) {
				if(Math.Abs((Truncate(document.DateCreated)-expected).TotalSeconds)<=ToleranceSeconds) {
					how="time";
					return document;
				}
			}
			return null;
		}

		///<summary>The DocNums whose ImageCreate entries FindDocument needs: the patient's documents created from a day before the oldest copy on,
		///so that the query stays short for a patient with many documents.</summary>
		public static List<long> GetDocNumsToCheck(IEnumerable<CaptureFileEntry> listEntries,IEnumerable<Document> listDocuments) {
			List<CaptureFileEntry> list=(listEntries??Enumerable.Empty<CaptureFileEntry>()).ToList();
			if(list.Count==0 || listDocuments==null) {
				return new List<long>();
			}
			DateTime oldest=list.Min(x => x.ReceivedAt).AddDays(-1);
			return listDocuments.Where(x => x!=null && x.DateCreated>=oldest).Select(x => x.DocNum).ToList();
		}

		///<summary>As RadiographSavePlanner.BuildCaptureLogText quotes it: trimmed, control characters as spaces, '"' as '\''.</summary>
		public static string QuoteLikeTheSaver(string text) {
			StringBuilder stringBuilder=new StringBuilder();
			foreach(char c in (text??"").Trim()) {
				stringBuilder.Append(char.IsControl(c) ? ' ' : c);
			}
			return "\""+stringBuilder.ToString().Replace("\"","'")+"\"";
		}

		private static DateTime Truncate(DateTime dateTime) {
			return new DateTime(dateTime.Ticks-dateTime.Ticks%TimeSpan.TicksPerSecond,dateTime.Kind);
		}
	}
}
