// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using OpenDentBusiness;

namespace ImagingCapture.Session {
	///<summary>The capture session's security-log entries (design 10.7), written through ICaptureSessionStore on the UI thread. An entry that
	///cannot be written never stops the session: the failure and the text go to the capture log instead.</summary>
	public sealed partial class CaptureSessionController {
		#region Methods - audit
		///<summary>One security-log entry (SecurityLogs.MakeLogEntry). Never throws.</summary>
		private void WriteAudit(EnumPermType permType,long patNum,string logText,long fKey) {
			ICaptureSessionStore store=_services.Store;
			try {
				if(store==null) {
					throw new InvalidOperationException("No store.");
				}
				store.MakeLogEntry(permType,patNum,logText,fKey);
				Log("Security log ("+permType+"): "+logText);
			}
			catch(Exception ex) {
				Log("The security-log entry ("+permType+") could not be written: "+ex.Message+" Its text: "+logText);
			}
		}
		#endregion Methods - audit
	}
}
