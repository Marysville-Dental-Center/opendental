// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ImagingCapture.Recovery;
using ImagingCapture.Save;
using ImagingDeviceManager.Capture;
using OpenDentBusiness;

namespace ImagingCapture.Session {
	///<summary>What the quarantine window and Recover recent captures (design 7.1, 7.12) need from the session: the sidecar fields of every enable,
	///so that an image that went to the quarantine or only to the journal can later be filed where it was meant to go, and the session's own
	///state after the window filed an image into this session's mount. UI thread only, like the rest of the controller.</summary>
	public sealed partial class CaptureSessionController {
		///<summary>The image category the session saves into (the quarantine window's default for a capture with none).</summary>
		public long CategoryDefNum => _setup.CategoryDefNum;

		///<summary>The mount, the slot, the tooth numbers, the category, the provider and the device of an enable, for the journal and quarantine
		///sidecars of its images (CaptureSidecarKeys). The library writes them as given.</summary>
		private List<KeyValuePair<string,string>> GetSidecarFields(CaptureEnableRecord record) {
			List<KeyValuePair<string,string>> listFields=new List<KeyValuePair<string,string>>();
			MountItem mountItem=record.MountItemTarget;
			listFields.Add(Pair(CaptureSidecarKeys.MountNum,_setup.Mount==null ? "" : ToText(_setup.Mount.MountNum)));
			listFields.Add(Pair(CaptureSidecarKeys.MountItemNum,mountItem==null ? "" : ToText(mountItem.MountItemNum)));
			listFields.Add(Pair(CaptureSidecarKeys.SlotItemOrder,mountItem==null ? "" : ToText(mountItem.ItemOrder)));
			listFields.Add(Pair(CaptureSidecarKeys.ToothNumbers,mountItem==null ? record.ToothNumbers??"" : mountItem.ToothNumbers??""));
			listFields.Add(Pair(CaptureSidecarKeys.DocCategory,ToText(_setup.CategoryDefNum)));
			listFields.Add(Pair(CaptureSidecarKeys.ProvNum,ToText(ProvNum)));
			listFields.Add(Pair(CaptureSidecarKeys.ImagingDeviceNum,ToText(_setup.ImagingDevice?.ImagingDeviceNum??0)));
			return listFields;
		}

		///<summary>Sets key to value in the sidecar fields of image (the library writes them into the image's quarantine sidecar when the session
		///quarantines it, and into its journal copy's sidecar then too), replacing a value it had.</summary>
		private static void SetImageSidecarField(AcquiredImage image,string key,string value) {
			if(image==null) {
				return;
			}
			List<KeyValuePair<string,string>> listFields=image.SidecarFields==null ? new List<KeyValuePair<string,string>>()
				: image.SidecarFields.Where(x => !string.Equals(x.Key,key,StringComparison.OrdinalIgnoreCase)).ToList();
			listFields.Add(Pair(key,value));
			image.SidecarFields=listFields;
		}

		///<summary>Records in image's sidecar fields the target the session places it into or meant it for (mountItem, or in single-target mode
		///toothNumbers), with CaptureSidecarKeys.SlotOfImage, so that a quarantine file of it is filed into that target and not into the slot its
		///enable started for: a later image of a continuous enable goes to the target as it is then, and a confirmed redelivered image to the
		///target at the confirmation (design 7.4, 7.12).</summary>
		private void SetImageTarget(AcquiredImage image,MountItem mountItem,string toothNumbers) {
			if(image==null) {
				return;
			}
			SetImageSidecarField(image,CaptureSidecarKeys.MountItemNum,mountItem==null ? "" : ToText(mountItem.MountItemNum));
			SetImageSidecarField(image,CaptureSidecarKeys.SlotItemOrder,mountItem==null ? "" : ToText(mountItem.ItemOrder));
			SetImageSidecarField(image,CaptureSidecarKeys.ToothNumbers,mountItem==null ? toothNumbers??"" : mountItem.ToothNumbers??"");
			SetImageSidecarField(image,CaptureSidecarKeys.SlotOfImage,"1");
			image.TargetLabel=GetRadiographTarget(mountItem,toothNumbers).GetLabel();
		}

		///<summary>The quarantine window or Recover recent captures filed an image (design 7.12). When it went into this session's mount, the
		///mini-map and the target follow at once, so that the session never places another image into that slot, and the mount no longer counts as
		///empty. The target stays where it was unless the filed slot was the target; then it moves on as after a save. The window opens only while
		///the source is not enabled (CaptureSessionViewModel.CanOpenCaptureLinks). Returns true when the session's mount changed.</summary>
		public bool OnCaptureFiled(CaptureFilingResult result) {
			if(result==null || !result.IsDone || result.Document==null || result.PatNum!=PatNum || _mountSlots==null || result.Mount==null
				|| result.Mount.MountNum!=_mountSlots.Mount.MountNum || result.MountItem==null)
			{
				return false;
			}
			if(result.IsMountItemCreated) {
				_mountSlots.AddUnmounted(result.MountItem,result.Document);
				Log("Filed from "+result.Action+": DocNum "+result.Document.DocNum+" as a new unmounted item of this mount.");
				NotifyChanged();
				return true;
			}
			int idx=_mountSlots.IndexOf(result.MountItem);
			if(idx==-1 || _mountSlots.ListDocuments[idx]!=null) {
				return false;
			}
			int idxTargetBefore=_mountSlots.IdxTarget;
			_mountSlots.SetPlaced(idx,result.Document);//moves the target on after idx
			if(idxTargetBefore!=-1 && idxTargetBefore!=idx) {
				_mountSlots.SetTarget(idxTargetBefore);//another slot was the target: it stays the target
			}
			else {
				AdvanceTarget();
			}
			_hashSetSkipped.Remove(idx);
			if(_idxTargetChosen==idx) {
				_idxTargetChosen=null;
			}
			Log("Filed from "+result.Action+": DocNum "+result.Document.DocNum+" into "+MountPlacement.GetSlotLabel(result.MountItem)+".");
			NotifyChanged();
			return true;
		}

		private static KeyValuePair<string,string> Pair(string key,string value) {
			return new KeyValuePair<string,string>(key,value??"");
		}

		private static string ToText(long value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}
	}
}
