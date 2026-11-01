// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using ImagingCapture.UI;
using ImagingDeviceManager.Capture;

namespace ImagingCapture.Session {
	///<summary>The LogText of the capture session's security-log entries (design 7.11, 7.13, 10.7): the session summary and the "Capture event:"
	///entries (ImageCreate, under the bound patient), the Retake (ImageEdit) and "Always rotate this slot" (Setup). Pure text, so that every
	///entry's wording is pinned by a test. No device or vendor names except the device row's own Description, which the office typed; a source's
	///condition code is written in hex, because its meaning is in the source's published contract.</summary>
	public static class CaptureAuditText {
		///<summary>Design 7.13: every entry for an event that may stand for an exposure with no stored image starts with this.</summary>
		public const string CaptureEventPrefix="Capture event: ";

		#region Session summary
		///<summary>Design 10.7: the session's ImageCreate summary: device, N images, R retakes, the quarantined count and the provider.</summary>
		public static string GetSessionSummaryText(CaptureSessionSummary summary) {
			if(summary==null) {
				return "Capture session summary.";
			}
			string provider=summary.ProvNum==0 ? "none" : (summary.ProviderAbbr=="" ? "" : summary.ProviderAbbr+" ")+"(ProvNum "+ToText(summary.ProvNum)+")";
			return "Capture session summary: device "+GetDeviceText(summary.DeviceDescription,summary.ImagingDeviceNum)+"; "
				+Count(summary.CountImages,"image","images")+", "+Count(summary.CountRetakes,"retake","retakes")+", "+ToText(summary.CountQuarantined)
				+" quarantined; provider "+provider+"; "+(summary.MountNum==0 ? "single images, no mount" : "mount "+ToText(summary.MountNum))
				+"; ended: "+summary.EndReason+"; in effect: "+(summary.EffectiveSettings??new CaptureEffectiveSettings()).GetText(isWithReasons:true)
				+". Session: "+summary.SessionGuid+".";
		}
		///<summary>"Capture session summary updated: an image arrived after the session finished and is in unassigned captures; 2 quarantined in
		///all. Session: ...".</summary>
		public static string GetSessionSummaryUpdateText(CaptureSessionSummary summary) {
			if(summary==null) {
				return "Capture session summary updated.";
			}
			return "Capture session summary updated: an image arrived after the session finished and is in unassigned captures; "
				+ToText(summary.CountQuarantined)+" quarantined in all, "+Count(summary.CountImages,"image","images")+" saved. Session: "
				+summary.SessionGuid+".";
		}
		#endregion Session summary

		#region Capture events (design 7.13)
		///<summary>"Capture event: &lt;what&gt; Device: Intraoral sensor (ImagingDeviceNum 12). Target: Slot 1 (teeth 1,2,3). Session: ...".</summary>
		public static string GetCaptureEventText(string what,string deviceDescription,long imagingDeviceNum,string targetLabel,Guid sessionGuid) {
			string target=(targetLabel??"").Trim();
			return CaptureEventPrefix+(what??"").Trim()+" Device: "+GetDeviceText(deviceDescription,imagingDeviceNum)+". Target: "
				+(target=="" ? "unknown" : target)+". Session: "+sessionGuid+".";
		}

		///<summary>MSG_CLOSEDSREQ after an enable without an image.</summary>
		public static string GetClosedWithoutImageWhat(ushort? conditionCode,string conditionText) {
			const string start="the sensor software closed after an enable without delivering an image";
			if(conditionCode==null) {
				return start+" (its status could not be read).";
			}
			if(conditionCode.Value==0) {
				return start+" (a plain close, condition code "+CaptureEndText.ToHex(0)+").";
			}
			string reason=(conditionText??"").Trim().TrimEnd('.').Trim();
			return start+": "+(reason=="" ? "" : reason+", ")+"condition code "+CaptureEndText.ToHex(conditionCode.Value)+".";
		}

		///<summary>A transfer failure (DAT_IMAGEMEMXFER or DAT_IMAGENATIVEXFER).</summary>
		public static string GetTransferFailedWhat(int countFailed,ushort? conditionCode) {
			return (countFailed>1 ? ToText(countFailed)+" image transfers failed" : "an image transfer failed")
				+(conditionCode==null ? "" : " (condition code "+CaptureEndText.ToHex(conditionCode.Value)+")")
				+"; the sensor software may still hold the image.";
		}

		///<summary>A disable started while the source might hold images (no safe disable in effect).</summary>
		public static string GetDisableStartedUnsafeWhat(TwainCaptureStopReason stopReason) {
			return "the source was disabled while safe disable was not in effect ("+CaptureEndText.GetStopText(stopReason)
				+"); an exposure in progress may not have been delivered.";
		}

		///<summary>A second consecutive MSG_DISABLEDS after the wait bound (W13).</summary>
		public static string GetSecondDisableAfterWaitWhat() {
			return "a refused disable got no notification within the wait bound, and the disable was sent again; the sensor software may still "
				+"hold images.";
		}

		///<summary>Extra images in single mode.</summary>
		public static string GetExtraImagesWhat(int countExtra) {
			return "the sensor software sent "+ToText(countExtra)+" more "+(countExtra==1 ? "image than expected; it is" : "images than expected; they are")
				+" in unassigned captures.";
		}

		///<summary>A quarantine move: the file, and why.</summary>
		public static string GetQuarantinedWhat(string reason,string path) {
			return "an image was moved to unassigned captures on this computer. Reason: "+GetSentence(reason)+" File: "+(path??"").Trim()+".";
		}

		///<summary>A quarantine move that failed: the journal copy, when there is one, is all that is left on this computer.</summary>
		public static string GetQuarantineFailedWhat(string reason,string journalPath) {
			string journal=(journalPath??"").Trim();
			return "an image could not be moved to unassigned captures. Reason: "+GetSentence(reason)+" "
				+(journal=="" ? "It has no journal copy." : "Its journal copy is "+journal+".");
		}

		///<summary>The forced close (design 7.3 step 7, 10.6), the one stop the fork starts without safe disable.</summary>
		public static string GetForcedCloseWhat() {
			return "the capture window closed while the sensor software was enabled without safe disable in effect; the source is stopped in the "
				+"safe order, and an exposure in progress may not be delivered.";
		}
		#endregion Capture events (design 7.13)

		#region Retake and rotation
		///<summary>Design 7.11, 10.7: the ImageEdit entry of a Retake, FKey=DocNum, under the bound patient.</summary>
		public static string GetRetakeText(long docNum,string slotLabel,long mountNum,Guid sessionGuid) {
			string label=(slotLabel??"").Trim();
			return "Retake: image DocNum "+ToText(docNum)+" moved from "+label+" of mount "+ToText(mountNum)+" to an unmounted item, labelled \""
				+CaptureSessionText.GetRetakeLabel(label)+"\". The slot is taken again. Session: "+sessionGuid+".";
		}

		///<summary>Design 7.3 step 5, 10.7: the Setup entry of "Always rotate this slot", with the old and new values of the mount definition's slot
		///and of the same slot of this mount (isMountItemChanged false: that slot could not be saved and keeps degreesItemOld).</summary>
		public static string GetAlwaysRotateText(string mountDefDescription,long mountDefNum,int itemOrder,string toothNumbers,int degreesOld,
			int degreesNew,long mountNum,int degreesItemOld,bool isMountItemChanged,string computerName)
		{
			string teeth=(toothNumbers??"").Trim();
			return "Always rotate this slot: mount definition \""+(mountDefDescription??"").Trim()+"\" (MountDefNum "+ToText(mountDefNum)+"), slot "
				+ToText(itemOrder)+(teeth=="" ? "" : " (teeth "+teeth+")")+": RotateOnAcquire "+ToText(degreesOld)+" -> "+ToText(degreesNew)
				+". The slot of mount "+ToText(mountNum)+(isMountItemChanged
					? ": RotateOnAcquire "+ToText(degreesItemOld)+" -> "+ToText(degreesNew)+"."
					: " could not be changed; it keeps RotateOnAcquire "+ToText(degreesItemOld)+".")
				+" Computer: "+(computerName??"").Trim()+".";
		}

		///<summary>The ImageEdit entry of Rotate (FKey=DocNum, under the bound patient), as the Imaging module logs its Rotate Right: the document,
		///the target and the old and new DegreesRotated, a display transform (the stored pixels do not change).</summary>
		public static string GetRotateText(long docNum,string fileName,string targetLabel,int degreesOld,int degreesNew,Guid sessionGuid) {
			string file=(fileName??"").Trim();
			return "Rotate: image DocNum "+ToText(docNum)+(file=="" ? "" : " ("+file+")")+" in "+(targetLabel??"").Trim()+" rotated right 90 degrees: "
				+"DegreesRotated "+ToText(degreesOld)+" -> "+ToText(degreesNew)+". Session: "+sessionGuid+".";
		}
		#endregion Retake and rotation

		#region Helpers
		private static string GetDeviceText(string deviceDescription,long imagingDeviceNum) {
			return (deviceDescription??"").Trim()+" (ImagingDeviceNum "+ToText(imagingDeviceNum)+")";
		}

		private static string GetSentence(string text) {
			string sentence=(text??"").Trim().TrimEnd('.').Trim();
			return (sentence=="" ? "none" : sentence)+".";
		}

		private static string Count(int count,string singular,string plural) {
			return ToText(count)+" "+(count==1 ? singular : plural);
		}

		private static string ToText(long value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}
		#endregion Helpers
	}
}
