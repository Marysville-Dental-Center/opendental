// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using ImagingDeviceManager.Capture;

namespace ImagingCapture.UI {
	///<summary>The operator's texts for how TwainCaptureSession.Open failed and how an enable ended (design 7.12), shared by Test capture and
	///the capture session window. There are no device or vendor names: "sensor software" is the role of the source's own window, and a
	///source's condition code is shown in hex when it gave no text, because its meaning is in the source's published contract.</summary>
	public static class CaptureEndText {
		public const string NoDsm="TWAIN is not installed on this computer. See the runbook.";
		public const string SourceNotEnabledHere="This TWAIN source is not enabled on this computer.";
		public const string SensorNotConnected="Sensor not connected.";
		public const string SensorSoftwareClosed="Sensor software closed.";
		public const string ImageNotReceived="Image not received. Do not re-expose yet.";
		public const string MayStillHoldImages="The sensor software may still hold images. Do not re-expose until you have checked it.";
		public const string ExtraImages="The sensor software sent more images than expected.";
		public const string CancelWarning="If an exposure was just taken, wait until it appears before cancelling.";

		///<summary>Why Open failed, or "" when it succeeded.</summary>
		public static string GetOpenText(TwainCaptureOpenResult result,string twainName,TimeSpan openTimeout) {
			if(result==null) {
				return "The sensor software could not be opened.";
			}
			if(result.IsOpen) {
				return "";
			}
			switch(result.Error) {
				case TwainCaptureOpenError.NoDsm:
					return NoDsm;
				case TwainCaptureOpenError.SourceNotEnabledHere:
					return SourceNotEnabledHere;
				case TwainCaptureOpenError.SourceNotFound:
					return "Imaging device '"+(twainName??"")+"' not found. Check Setup > Imaging > Devices.";
				case TwainCaptureOpenError.AlreadyOpen:
					return "The sensor software is already open.";
				case TwainCaptureOpenError.Timeout:
					return "The sensor software did not open within "+((int)openTimeout.TotalSeconds).ToString(CultureInfo.InvariantCulture)+" seconds.";
				default:
					string text=(result.ErrorText??"").Trim();
					if(result.ConditionCode!=null) {
						text+=(text=="" ? "" : " ")+"(condition code "+ToHex(result.ConditionCode.Value)+")";
					}
					return text=="" ? "The sensor software could not be opened." : text;
			}
		}

		///<summary>How the enable ended, as one or more sentences; "" for a plain completed enable.</summary>
		public static string GetEndedText(TwainCaptureEnded ended) {
			if(ended==null) {
				return "";
			}
			List<string> listParts=new List<string>();
			switch(ended.Reason) {
				case TwainCaptureEndReason.Completed:
					break;
				case TwainCaptureEndReason.CloseRequested:
					if(ended.ConditionCode==null || ended.ConditionCode.Value==0) {
						listParts.Add(SensorSoftwareClosed);
					}
					else {
						listParts.Add("Sensor software closed without an image: "+GetReason(ended.ConditionCode.Value,ended.ConditionText)
							+". Do not re-expose until you have checked the sensor software.");
					}
					break;
				case TwainCaptureEndReason.Stopped:
					listParts.Add("Stopped ("+GetStopText(ended.StopReason)+").");
					break;
				case TwainCaptureEndReason.TransferFailed:
					listParts.Add(ImageNotReceived);
					break;
				case TwainCaptureEndReason.EnableFailed:
					listParts.Add(GetErrorText(ended,"The sensor software could not be started"));
					break;
				case TwainCaptureEndReason.DeviceOffline:
					listParts.Add(SensorNotConnected);
					break;
				case TwainCaptureEndReason.RedeliveryUnavailable:
					listParts.Add(GetErrorText(ended,"The sensor software cannot deliver images again"));
					break;
				case TwainCaptureEndReason.Refused:
					listParts.Add(GetErrorText(ended,"Not started: the sensor software is busy or closing"));
					break;
				default:
					listParts.Add(GetErrorText(ended,"The capture stopped with an error"));
					break;
			}
			if(ended.Reason!=TwainCaptureEndReason.TransferFailed && (ended.SourceMayHoldImages || ended.WasSecondDisableAfterWait)) {
				listParts.Add(MayStillHoldImages);
			}
			if(ended.ExtraImageCount>0) {
				listParts.Add(ExtraImages);
			}
			if(!string.IsNullOrWhiteSpace(ended.Reason8Bit)) {
				listParts.Add("Delivered at 8 bits: "+ended.Reason8Bit.Trim().TrimEnd('.')+".");
			}
			return string.Join(" ",listParts);
		}

		public static string GetStopText(TwainCaptureStopReason stopReason) {
			switch(stopReason) {
				case TwainCaptureStopReason.Cancel:
					return "cancelled";
				case TwainCaptureStopReason.End:
					return "ended";
				case TwainCaptureStopReason.Idle:
					return "no exposure for too long";
				case TwainCaptureStopReason.PatientChanged:
					return "the patient changed";
				case TwainCaptureStopReason.TargetChanged:
					return "the target changed";
				case TwainCaptureStopReason.SessionTimeout:
					return "session timeout";
				case TwainCaptureStopReason.Close:
					return "the window closed";
				case TwainCaptureStopReason.Error:
					return "an error";
				default:
					return "stopped";
			}
		}

		///<summary>"0x8001".</summary>
		public static string ToHex(ushort code) {
			return "0x"+code.ToString("X4",CultureInfo.InvariantCulture);
		}

		private static string GetReason(ushort conditionCode,string conditionText) {
			string reason=(conditionText??"").Trim().TrimEnd('.').Trim();
			return reason=="" ? ToHex(conditionCode) : reason;
		}

		private static string GetErrorText(TwainCaptureEnded ended,string textDefault) {
			string text=(ended.ErrorText??"").Trim();
			if(text=="") {
				text=textDefault;
				if(ended.ConditionCode!=null) {
					text+=": "+GetReason(ended.ConditionCode.Value,ended.ConditionText);
				}
				text=text.TrimEnd('.')+".";
			}
			return text;
		}
	}
}
