// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;

namespace ImagingCapture.Session {
	///<summary>The capture session's own texts for the indicator area (design 7.5, 7.12), apart from those CaptureEndText already has for how
	///an open or an enable ended. No device or vendor names: "sensor software" is the role of the source's own window.</summary>
	public static class CaptureSessionText {
		public const string Opening="Opening the sensor software";
		public const string Starting="Starting the sensor software";
		///<summary>Design 7.5, after a successful MSG_ENABLEDS. Never "armed": the fork does not know that.</summary>
		public const string WaitingForExposure="Sensor software open, waiting for exposure";
		public const string Receiving="Receiving the image";
		public const string Stopping="Stopping the sensor software";
		public const string WaitingForSource="Waiting for the sensor software to finish";
		public const string Disabled="Disabled.";
		///<summary>Design 7.12 "Extra images in single mode".</summary>
		public const string ExtraImages="The sensor software sent more images than expected; they are in unassigned captures.";
		///<summary>Design 7.12 "Save fails": the red strip starts with this.</summary>
		public const string CouldNotSavePrefix="Could not save: ";
		public const string NotOpen="The sensor software is not open.";
		public const string ReadyStart="Check the patient, then press Start or hold the Confirm key.";
		public const string ReadyReEnable="Press Re-enable or hold the Confirm key to start the sensor software again.";
		public const string ReadyRetry="Press Retry when the problem is fixed.";
		public const string ReadyTakeAnother="Press Take another for one more image, or End.";
		public const string ReadyTakeSkipped="Every slot that was not skipped has an image. Press Take the skipped slots, or End.";

		///<summary>"Start X-rays for Pat Test", "Re-enable for Pat Test", "Retry", "Take another", "Take the skipped slots", or "".</summary>
		public static string GetStartText(CaptureStartKind startKind,string patientName) {
			switch(startKind) {
				case CaptureStartKind.Start: return "Start X-rays for "+patientName;
				case CaptureStartKind.ReEnable: return "Re-enable for "+patientName;
				case CaptureStartKind.Retry: return "Retry";
				case CaptureStartKind.TakeAnother: return "Take another";
				case CaptureStartKind.TakeSkipped: return "Take the skipped slots";
				default: return "";
			}
		}

		///<summary>Design 7.6: the red bar.</summary>
		public static string GetPatientChangedText(string patientName) {
			return "Patient changed in the main window. This session is still for "+patientName+".";
		}

		///<summary>Design 7.6: before a disable that is not safe (Cancel, End, Disable while the source is enabled without DisableIsSafe in
		///effect). The second press of the same command goes ahead.</summary>
		public static string GetUnsafeStopWarning(CaptureSessionAction action) {
			string command=action==CaptureSessionAction.End ? "End" : "Disable";
			return "If an exposure was just taken, wait until it appears before cancelling. Press "+command+" again to stop now.";
		}

		///<summary>Design 7.11: "Next image: slot 4, then slot 7", while an enable runs for another target than the one chosen.</summary>
		public static string GetNextImageText(string enableTargetText,string targetText) {
			return "Next image: "+enableTargetText+", then "+targetText;
		}

		///<summary>Design 7.11: "Next image: &lt;slot N+1&gt;, then retake &lt;slot N&gt;", while the enable for the next slot runs and the Retake
		///waits for it to deliver or end.</summary>
		public static string GetNextImageRetakeText(string enableTargetText,string retakeTargetText) {
			return "Next image: "+enableTargetText+", then retake "+retakeTargetText;
		}

		///<summary>Design 7.11, 9.1: the Description of a retaken document in its unmounted item, "Retake: Slot 3 (teeth 2,3)".</summary>
		public static string GetRetakeLabel(string slotLabel) {
			return "Retake: "+(slotLabel??"").Trim();
		}

		///<summary>After a Retake: where the rejected image went, and what is next.</summary>
		public static string GetRetakeNotice(string slotLabel) {
			string label=(slotLabel??"").Trim();
			return "The image of "+label+" is kept as an unmounted image, \""+GetRetakeLabel(label)+"\". The next image goes to "+label+".";
		}

		///<summary>"Rotated to 90 degrees."</summary>
		public static string GetRotatedText(int degrees) {
			return "Rotated to "+degrees.ToString(CultureInfo.InvariantCulture)+" degrees.";
		}

		///<summary>After "Always rotate this slot": "Slot 3 of "FMX" now turns 90 degrees on acquire."</summary>
		public static string GetAlwaysRotateText(int itemOrder,string mountDefDescription,int degrees) {
			return "Slot "+itemOrder.ToString(CultureInfo.InvariantCulture)+" of \""+(mountDefDescription??"").Trim()+"\" now turns "
				+degrees.ToString(CultureInfo.InvariantCulture)+" degrees on acquire.";
		}

		public const string AlwaysRotateNotAuthorized="Not authorized for Setup: the slot's rotation on acquire was not changed.";

		///<summary>"Saved to slot 3 (teeth 2,3)" from MountPlacement.GetSlotLabel's "Slot 3 (teeth 2,3)".</summary>
		public static string GetSavedText(string slotLabel) {
			string label=slotLabel??"";
			if(label.StartsWith("Slot ",StringComparison.Ordinal)) {
				label="slot "+label.Substring(5);
			}
			return "Saved to "+label;
		}

		public static string GetSavedSingleText(string toothNumbers) {
			string teeth=(toothNumbers??"").Trim();
			return teeth=="" ? "Saved as a single image" : "Saved as a single image (teeth "+teeth+")";
		}

		///<summary>The mount the session fills: "FMX 18, 10/31/2026".</summary>
		public static string GetMountText(string description,DateTime dateCreated) {
			return (description??"").Trim()+", "+dateCreated.ToShortDateString();
		}

		///<summary>Design 7.3 step 1: the explicit question for a mount dated before today.</summary>
		public static string GetOlderMountText(string description,DateTime dateCreated) {
			return "The mount \""+(description??"").Trim()+"\" is dated "+dateCreated.ToShortDateString()+", before today. Add today's X-rays to it?";
		}
	}
}
