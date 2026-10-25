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

	///<summary>How a by-name Acquire ended, as the session reports it afterwards.</summary>
	internal sealed class AcquireOutcome {
		public bool HasImage;
		public EZTwainErrorCode ErrorCode;
		///<summary>TwainSession.IsLastTransferFailed: the transfer of the caller's image failed.</summary>
		public bool IsTransferFailed;
		///<summary>TwainSession.EndConditionCode: the end reason read after MSG_CLOSEDSREQ, or null.</summary>
		public ushort? EndConditionCode;
		public string EndConditionText;
	}

	///<summary>Which notices a facade call ends with (design 7.1), in which order, and the LastErrorText a by-name Acquire returns. All the
	///notices of one call go into one non-modal notice.</summary>
	internal static class AcquireNotices {
		///<summary>The LastErrorText for a by-name Acquire that returned no image, or null to keep the session's:
		///- a failed transfer, or a source that stopped responding: "Image not received. Do not re-expose until you have checked the sensor
		///  software.", which the Mount/Acquire loop shows in its message box (it ignores EZTEC_DS_FAILURE, so Select adds a notice for it);
		///- MSG_CLOSEDSREQ with an end code other than TWCC_SUCCESS: the "ended without an image" text. The code stays EZTEC_USER_CANCEL,
		///  so the loop ends the series silently and the notice is what the operator sees.</summary>
		public static string GetErrorText(AcquireOutcome outcome) {
			if(outcome==null || outcome.HasImage) {
				return null;
			}
			if(outcome.IsTransferFailed || outcome.ErrorCode==EZTwainErrorCode.EZTEC_DS_FAILURE) {
				return CaptureNoticeText.ImageNotReceived;
			}
			if(outcome.ErrorCode==EZTwainErrorCode.EZTEC_USER_CANCEL && IsFailureEnd(outcome)) {
				return CaptureNoticeText.EndedWithoutImage(outcome.EndConditionCode.Value,outcome.EndConditionText);
			}
			return null;
		}

		///<summary>An end code other than TWCC_SUCCESS. A plain cancel (TWCC_SUCCESS), and an end whose status could not be read, stay
		///silent.</summary>
		private static bool IsFailureEnd(AcquireOutcome outcome) {
			return outcome.EndConditionCode!=null && outcome.EndConditionCode.Value!=TwainProtocol.Tw.TWCC_SUCCESS;
		}

		///<summary>The notices of a facade call, in order: the source's end reason, an image not received from a source that stopped
		///responding, images saved to the quarantine, images that could not be, and "may still hold images". outcome is null for a call that
		///was not an Acquire.</summary>
		public static List<string> Select(CaptureCallState call,AcquireOutcome outcome) {
			List<string> listNotices=new List<string>();
			if(outcome!=null && IsFailureEnd(outcome)) {
				listNotices.Add(CaptureNoticeText.EndedWithoutImage(outcome.EndConditionCode.Value,outcome.EndConditionText));
			}
			if(outcome!=null && !outcome.HasImage && outcome.ErrorCode==EZTwainErrorCode.EZTEC_DS_FAILURE) {
				listNotices.Add(CaptureNoticeText.ImageNotReceived);
			}
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
