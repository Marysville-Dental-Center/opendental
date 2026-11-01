// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;

namespace ImagingCapture.Session {
	///<summary>The LogText of the capture session's security-log entries (design 7.11, 7.13, 10.7): the Retake (ImageEdit) and "Always rotate
	///this slot" (Setup). Pure text, so that every entry's wording is pinned by a test. No device or vendor names except the device row's own
	///Description, which the office typed.</summary>
	public static class CaptureAuditText {
		///<summary>Design 7.11, 10.7: the ImageEdit entry of a Retake, FKey=DocNum, under the bound patient.</summary>
		public static string GetRetakeText(long docNum,string slotLabel,long mountNum,Guid sessionGuid) {
			string label=(slotLabel??"").Trim();
			return "Retake: image DocNum "+ToText(docNum)+" moved from "+label+" of mount "+ToText(mountNum)+" to an unmounted item, labelled \""
				+CaptureSessionText.GetRetakeLabel(label)+"\". The slot is taken again. Session: "+sessionGuid+".";
		}

		///<summary>Design 7.3 step 5, 10.7: the Setup entry of "Always rotate this slot", with the old and new values.</summary>
		public static string GetAlwaysRotateText(string mountDefDescription,long mountDefNum,int itemOrder,string toothNumbers,int degreesOld,
			int degreesNew,long mountNum,string computerName)
		{
			string teeth=(toothNumbers??"").Trim();
			return "Always rotate this slot: mount definition \""+(mountDefDescription??"").Trim()+"\" (MountDefNum "+ToText(mountDefNum)+"), slot "
				+ToText(itemOrder)+(teeth=="" ? "" : " (teeth "+teeth+")")+": RotateOnAcquire "+ToText(degreesOld)+" -> "+ToText(degreesNew)
				+". The slot of mount "+ToText(mountNum)+" was changed the same way. Computer: "+(computerName??"").Trim()+".";
		}

		private static string ToText(long value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}
	}
}
