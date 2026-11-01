using System;
using System.Collections.Generic;
using System.Text;
using OpenPmsDentalBusiness;

namespace PluginExample {
	class ContrFamilyP {
		public static void gridPat_CellDoubleClick(OpenPmsDental.ControlFamily sender,Patient patient) {//again, named much like the original
			FormPatientEditP formPatientEditP=new FormPatientEditP();
			formPatientEditP.PatientCur=patient;
			formPatientEditP.ShowDialog();
			sender.ModuleSelected(patient.PatNum);
		}
	}
}
