// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Video {
	///<summary>The patient a capture window belongs to, and the rule for when the patient changed (design 6.4 and 8.10).
	///
	///The window records the main window's current PatNum when it opens. An ODEventType.Patient event is a patient change when
	///its Tag is a long that differs from the recorded PatNum, or is 0 (no patient). A Patient Tag (raised after a patient is
	///edited, or after the patient picture changed) is ignored unless its PatNum differs. Any other Tag, and any other event
	///type, is ignored. The window closes on a patient change, so a photo is never saved to a patient other than the one shown
	///in its title.</summary>
	public sealed class PatientBinding {
		///<summary>The patient the window was opened for. 0 when none was selected.</summary>
		public long PatNum { get; }

		public PatientBinding(long patNum) {
			PatNum=Math.Max(0,patNum);
		}

		///<summary>True when the event means the window must close (see the class summary).</summary>
		public bool IsPatientChange(ODEventArgs odEventArgs) {
			if(odEventArgs==null || odEventArgs.EventType!=ODEventType.Patient) {
				return false;
			}
			return IsPatientChange(odEventArgs.Tag);
		}

		///<summary>The Tag part of IsPatientChange(ODEventArgs).</summary>
		public bool IsPatientChange(object tag) {
			if(tag is long) {
				long patNum=(long)tag;
				return patNum==0 || patNum!=PatNum;
			}
			Patient patient=tag as Patient;
			if(patient!=null) {
				return patient.PatNum!=PatNum;
			}
			return false;
		}
	}
}
