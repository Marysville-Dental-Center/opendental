using System;
using System.Collections.Generic;
using System.Text;
using OpenPmsDentalBusiness;

namespace OpenPmsDental.SmartCards {
    public class PatientCardInsertedEventArgs : EventArgs {
        public PatientCardInsertedEventArgs(Patient patient) {
            this.patient = patient;
        }

        private Patient patient;
        public Patient Patient {
            get { return patient; }
        }
    }
}
