// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImagingDeviceManager.Acquire {
	///<summary>An image that went to the quarantine during a facade call, or should have.</summary>
	internal sealed class QuarantinedImage {
		public DateTime ReceivedAt;
		///<summary>The quarantine file, or null when it could not be written.</summary>
		public string PathImage;
		///<summary>Why it could not be written, or null.</summary>
		public string Error;
		///<summary>The journal copy, or null.</summary>
		public string PathJournal;
	}

	///<summary>What happened during one facade call on a by-name source (an Acquire, or a CloseSource that stopped the source) that the
	///operator must hear about afterwards. The drain sink fills it from inside the message loop; the facade shows the notices when the call
	///returns.</summary>
	internal sealed class CaptureCallState {
		///<summary>Identifies the call in the journal, the quarantine and the capture log.</summary>
		public readonly string AcquireId=Guid.NewGuid().ToString("N");
		public readonly List<QuarantinedImage> ListQuarantined=new List<QuarantinedImage>();
		///<summary>The wait after a refused disable ended without a notification: the source may still hold images.</summary>
		public bool IsMayStillHoldImages;
	}

	///<summary>Which notices a facade call ends with (design 7.1), and in which order. All of them go into one non-modal notice.</summary>
	internal static class AcquireNotices {
		public static List<string> Select(CaptureCallState call) {
			List<string> listNotices=new List<string>();
			if(call==null) {
				return listNotices;
			}
			List<QuarantinedImage> listSaved=call.ListQuarantined.Where(x => x.PathImage!=null).ToList();
			if(listSaved.Count>0) {
				listNotices.Add(CaptureNoticeText.Quarantined(listSaved.Count,listSaved.Min(x => x.ReceivedAt),listSaved.Max(x => x.ReceivedAt)));
			}
			List<QuarantinedImage> listFailed=call.ListQuarantined.Where(x => x.PathImage==null).ToList();
			if(listFailed.Count>0) {
				listNotices.Add(CaptureNoticeText.QuarantineFailed(listFailed.Count,listFailed.Min(x => x.ReceivedAt),listFailed.Max(x => x.ReceivedAt),
					listFailed[0].Error,listFailed.All(x => x.PathJournal!=null)));
			}
			if(call.IsMayStillHoldImages) {
				listNotices.Add(CaptureNoticeText.MayStillHoldImages);
			}
			return listNotices;
		}
	}
}
