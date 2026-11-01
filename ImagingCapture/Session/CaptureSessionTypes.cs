// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ImagingCapture.Dicom;
using ImagingCapture.Save;
using OpenDentBusiness;

namespace ImagingCapture.Session {
	///<summary>Where the capture session is, for the window's layout (which panel shows, which commands are offered).</summary>
	public enum CaptureSessionPhase {
		///<summary>Not begun: the window has not called Begin.</summary>
		Created,
		///<summary>The target mount is dated before today: the operator confirms it as the target, or declines (design 7.3 step 1).</summary>
		ConfirmingOlderMount,
		///<summary>The banner shows and the source is not enabled: waiting for a deliberate start (the on-screen button, or the Confirm key held
		///about 1 s), first for the start and then for every re-enable after a disable (design 7.3 step 2). StartKind says which.</summary>
		Ready,
		///<summary>TwainCaptureSession.Open runs on a background thread.</summary>
		Opening,
		///<summary>An enable is posted or running; session keys are live (the window registers its hotkeys).</summary>
		Enabled,
		///<summary>The controller stopped the enable (End, Disable) and waits for it to end.</summary>
		Stopping,
		///<summary>Done: Summary is set, the source is closing. The window shows the summary and closes.</summary>
		Finished,
	}

	///<summary>The indicator (design 7.5). The text never says the sensor is armed on the fork's own inference.</summary>
	public enum CaptureIndicatorState {
		///<summary>Before the first start, or waiting for a re-enable with nothing wrong.</summary>
		Ready,
		///<summary>Opening the source or starting the sensor software; also "Sensor not connected" (design 7.12: state after is Connecting).</summary>
		Connecting,
		///<summary>"Sensor software open, waiting for exposure" (after a successful MSG_ENABLEDS).</summary>
		WaitingForExposure,
		Receiving,
		///<summary>"Saved to slot N".</summary>
		Saved,
		Disabled,
		SensorSoftwareClosed,
		///<summary>"Image not received. Do not re-expose yet."</summary>
		ImageNotReceived,
		Error,
		Finished,
	}

	///<summary>What a deliberate start does next, for the Start button's text.</summary>
	public enum CaptureStartKind {
		///<summary>"Start X-rays for &lt;name&gt;": the first start.</summary>
		Start,
		///<summary>"Re-enable for &lt;name&gt;": after the source was disabled or the sensor software closed.</summary>
		ReEnable,
		///<summary>"Retry": after the sensor was not connected, or the open or the enable failed.</summary>
		Retry,
		///<summary>"Take another": single-target mode, after an image was saved (design 7.3 step 2, 10.3).</summary>
		TakeAnother,
		///<summary>"Take the skipped slots": every slot that was not skipped has an image.</summary>
		TakeSkipped,
		///<summary>Nothing can be started (the source gate refused it, or the session finished).</summary>
		None,
	}

	///<summary>An operator command bound to a button, a key or a pedal (design 7.3 step 5, 9.3 [Keys]). None of them confirms a start.</summary>
	public enum CaptureSessionAction {
		Retake,
		Skip,
		NextSlot,
		PreviousSlot,
		Rotate,
		Disable,
		End,
	}

	///<summary>How the session ended, in the summary.</summary>
	public enum CaptureSessionEndReason {
		///<summary>Every slot of the mount has an image.</summary>
		MountFull,
		///<summary>The operator ended it.</summary>
		Ended,
		///<summary>The operator declined the older mount, or ended before anything was enabled.</summary>
		CancelledBeforeStart,
		///<summary>The window was closed (FormClosing, a forced logoff).</summary>
		Closed,
	}

	///<summary>One enable the controller posted: the target it was for, fixed when it started, so that its images are placed there whatever
	///the target is by the time they arrive (design 7.11). It is the TwainEnableRequest.Tag, so it comes back on every image and the Ended.</summary>
	public sealed class CaptureEnableRecord {
		public int EnableId { get; set; }
		///<summary>The index of the target slot in MountSlots.ListMountItems, or -1 in single-target mode.</summary>
		public int IdxTarget { get; set; }
		///<summary>The target slot, or null in single-target mode.</summary>
		public MountItem MountItemTarget { get; set; }
		///<summary>Single-target mode: the banner's tooth numbers when the enable started.</summary>
		public string ToothNumbers { get; set; }
		///<summary>The bit depth asked for.</summary>
		public CaptureBitDepthChoice BitDepth { get; set; }
		public bool IsRedelivery { get; set; }
		///<summary>From the status of the enable (TwainCaptureStatus.IsSafeDisableInEffect): the controller may disable it on its own.</summary>
		public bool IsSafeDisableInEffect { get; set; }
		///<summary>When the controller posted the enable (local time).</summary>
		public DateTime PostedAt { get; set; }
		///<summary>Images of this enable that were saved, quarantined.</summary>
		public int CountSaved { get; set; }
		public int CountQuarantined { get; set; }
		///<summary>Images of this enable that reached the controller while the session ran, wherever they went.</summary>
		public int CountReceived { get; set; }
		///<summary>The controller asked it to stop, and why.</summary>
		public ImagingDeviceManager.Capture.TwainCaptureStopReason StopRequested { get; set; }
	}

	///<summary>One slot of the mount, for the slot mini-map (design 7.3 step 3): every capture slot in ItemOrder, the target highlighted.</summary>
	public sealed class CaptureSlotView {
		///<summary>The index in MountSlots.ListMountItems, for SelectSlot.</summary>
		public int Idx { get; set; }
		public MountItem MountItem { get; set; }
		///<summary>"Slot 3 (teeth 2,3)".</summary>
		public string Label { get; set; }
		///<summary>The document in it, or null.</summary>
		public Document Document { get; set; }
		public bool IsFilled => Document!=null;
		///<summary>The target of the next enable.</summary>
		public bool IsTarget { get; set; }
		///<summary>The target of the enable that is running (its images go here).</summary>
		public bool IsEnableTarget { get; set; }
		public bool IsSkipped { get; set; }
	}

	///<summary>The redelivered image (W15, design 7.12) that waits for the operator: placed only when the operator confirms it, with its
	///acquiredAt and captureId shown, because the source has no patient context; declined, it goes to the quarantine as "patient unknown".</summary>
	public sealed class CaptureRedeliveredView {
		public ImagingDeviceManager.Capture.AcquiredImage Image { get; set; }
		///<summary>The metadata's acquiredAt as the source wrote it, or "unknown".</summary>
		public string AcquiredAtText { get; set; }="";
		///<summary>The metadata's captureId, or "unknown".</summary>
		public string CaptureId { get; set; }="";
		///<summary>Where Confirm places it: "2 of 3: Max R, #4,5", "Single image, #8", or "" when no slot is free.</summary>
		public string TargetText { get; set; }="";
		///<summary>The question the window shows with the image.</summary>
		public string Text { get; set; }="";
		///<summary>Redelivered images waiting, this one included.</summary>
		public int CountPending { get; set; }
	}

	///<summary>What the session did, for the window's summary and for the session's ImageCreate audit entry (design 7.3 step 6, 10.7).</summary>
	public sealed class CaptureSessionSummary {
		public long PatNum { get; set; }
		public string PatientName { get; set; }="";
		public long ProvNum { get; set; }
		public string ProviderAbbr { get; set; }="";
		public long ImagingDeviceNum { get; set; }
		public string DeviceDescription { get; set; }="";
		public Guid SessionGuid { get; set; }
		///<summary>The mount, or 0 in single-target mode.</summary>
		public long MountNum { get; set; }
		///<summary>Images saved as documents.</summary>
		public int CountImages { get; set; }
		///<summary>Retakes (CAP-4 implementer A2 counts them).</summary>
		public int CountRetakes { get; set; }
		///<summary>Images moved to the quarantine (unassigned captures).</summary>
		public int CountQuarantined { get; set; }
		public List<long> ListDocNums { get; set; }=new List<long>();
		public List<string> ListQuarantinePaths { get; set; }=new List<string>();
		public CaptureSessionEndReason EndReason { get; set; }
		public DateTime DateTimeStarted { get; set; }
		public DateTime DateTimeEnded { get; set; }

		///<summary>"5 images, 1 retake, 0 quarantined. Patient: Pat Test. Provider: DOC1."</summary>
		public string GetText() {
			string text=Count(CountImages,"image","images")+", "+Count(CountRetakes,"retake","retakes")+", "+CountQuarantined.ToString(CultureInfo.InvariantCulture)
				+" quarantined. Patient: "+PatientName+". Provider: "+(ProviderAbbr=="" ? "none" : ProviderAbbr)+".";
			if(CountQuarantined>0) {
				text+=" The quarantined images are in unassigned captures on this computer.";
			}
			return text;
		}

		private static string Count(int count,string singular,string plural) {
			return count.ToString(CultureInfo.InvariantCulture)+" "+(count==1 ? singular : plural);
		}
	}

	///<summary>The target in words for the indicator (design 10.2: "1 of 18: Max R canine PA, #6"): the slot's position among the mount's
	///slots in ItemOrder, a short description of its teeth (arch and side), and the tooth numbers. No name comes from a device or a template:
	///mount items have no label, so the words are derived from ToothNumbers only.</summary>
	public static class CaptureTargetText {
		///<summary>"3 of 18: Max R, #1,2,3", or "3 of 18: Slot 3" for a slot without teeth. listMountItems: the mount's items (any order; only
		///slots count, by ItemOrder). "" when mountItem is not one of its slots.</summary>
		public static string GetSlotText(IEnumerable<MountItem> listMountItems,MountItem mountItem) {
			if(mountItem==null || !MountPlacement.IsSlot(mountItem)) {
				return "";
			}
			List<MountItem> listSlots=(listMountItems??Enumerable.Empty<MountItem>()).Where(x => MountPlacement.IsSlot(x)).OrderBy(x => x.ItemOrder).ToList();
			int idx=listSlots.FindIndex(x => ReferenceEquals(x,mountItem) || (mountItem.MountItemNum!=0 && x.MountItemNum==mountItem.MountItemNum));
			if(idx==-1) {
				return "";
			}
			return (idx+1).ToString(CultureInfo.InvariantCulture)+" of "+listSlots.Count.ToString(CultureInfo.InvariantCulture)+": "
				+GetDescription(mountItem);
		}

		///<summary>"Max R, #1,2,3"; "Slot 3" for a slot without (readable) teeth.</summary>
		public static string GetDescription(MountItem mountItem) {
			if(mountItem==null) {
				return "";
			}
			string teeth=GetTeethText(mountItem.ToothNumbers);
			if(teeth=="") {
				return "Slot "+mountItem.ItemOrder.ToString(CultureInfo.InvariantCulture);
			}
			return teeth;
		}

		///<summary>Single-target mode: "Single image, #8,9" or "Single image".</summary>
		public static string GetSingleText(string toothNumbers) {
			string teeth=GetTeethText(toothNumbers);
			return teeth=="" ? "Single image" : "Single image, "+teeth;
		}

		///<summary>"Max R, #1,2,3", "Mand anterior, #24,25", "R bitewing, #2,3,30,31", or "#..." when the teeth do not parse; "" for none.</summary>
		public static string GetTeethText(string toothNumbers) {
			string text=(toothNumbers??"").Trim();
			if(text=="") {
				return "";
			}
			List<ToothInfo> listTeeth;
			string error;
			if(!ToothNumbers.TryParseList(text,out listTeeth,out error) || listTeeth.Count==0) {
				return "#"+text;
			}
			string numbers="#"+string.Join(",",listTeeth.Select(x => x.Universal));
			bool isMax=listTeeth.All(x => x.IsMaxillary);
			bool isMand=listTeeth.All(x => !x.IsMaxillary);
			string side;
			if(listTeeth.All(x => x.IsAnterior)) {
				side=listTeeth.All(x => x.IsPatientRight) ? "R anterior" : (listTeeth.All(x => !x.IsPatientRight) ? "L anterior" : "anterior");
			}
			else if(listTeeth.All(x => x.IsPatientRight)) {
				side="R";
			}
			else if(listTeeth.All(x => !x.IsPatientRight)) {
				side="L";
			}
			else {
				side="";
			}
			string words;
			if(isMax || isMand) {
				words=((isMax ? "Max" : "Mand")+" "+side).Trim();
			}
			else {
				words=(side+" bitewing").Trim();
			}
			return words+", "+numbers;
		}
	}
}
