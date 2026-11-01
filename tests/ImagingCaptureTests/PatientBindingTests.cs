// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase;
using ImagingCapture.Video;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests {
	///<summary>PatientBinding: when the Video window must close because the patient changed (design 6.4, 8.10).</summary>
	[TestClass]
	public class PatientBindingTests {
		[TestMethod]
		public void LongTag_DifferentOrZeroIsAChange() {
			PatientBinding patientBinding=new PatientBinding(42);
			Assert.AreEqual(42L,patientBinding.PatNum);
			Assert.IsFalse(patientBinding.IsPatientChange(new ODEventArgs(ODEventType.Patient,42L)),"The same patient again.");
			Assert.IsTrue(patientBinding.IsPatientChange(new ODEventArgs(ODEventType.Patient,43L)));
			Assert.IsTrue(patientBinding.IsPatientChange(new ODEventArgs(ODEventType.Patient,0L)),"0 means no patient.");
		}

		[TestMethod]
		public void PatientTag_OnlyWhenThePatNumDiffers() {
			PatientBinding patientBinding=new PatientBinding(42);
			Assert.IsFalse(patientBinding.IsPatientChange(new ODEventArgs(ODEventType.Patient,new Patient() { PatNum=42 })),
				"The bound patient was edited, or its picture changed.");
			Assert.IsTrue(patientBinding.IsPatientChange(new ODEventArgs(ODEventType.Patient,new Patient() { PatNum=7 })));
		}

		[TestMethod]
		public void OtherEventsAndTags_Ignored() {
			PatientBinding patientBinding=new PatientBinding(42);
			Assert.IsFalse(patientBinding.IsPatientChange(new ODEventArgs(ODEventType.AppointmentEdited,43L)));
			Assert.IsFalse(patientBinding.IsPatientChange(new ODEventArgs(ODEventType.Undefined,0L)));
			Assert.IsFalse(patientBinding.IsPatientChange(new ODEventArgs(ODEventType.Patient,null)));
			Assert.IsFalse(patientBinding.IsPatientChange(new ODEventArgs(ODEventType.Patient,"43")));
			Assert.IsFalse(patientBinding.IsPatientChange(new ODEventArgs(ODEventType.Patient,43)),"Only a long Tag carries a PatNum.");
			Assert.IsFalse(patientBinding.IsPatientChange((ODEventArgs)null));
		}

		[TestMethod]
		public void NoPatientBound() {
			PatientBinding patientBinding=new PatientBinding(0);
			Assert.AreEqual(0L,patientBinding.PatNum);
			Assert.IsTrue(patientBinding.IsPatientChange(5L),"Selecting a patient after opening with none closes the window.");
			Assert.IsTrue(patientBinding.IsPatientChange(0L));
			Assert.AreEqual(0L,new PatientBinding(-3).PatNum);
		}
	}
}
