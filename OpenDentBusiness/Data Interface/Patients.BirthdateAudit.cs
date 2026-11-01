using System;

namespace OpenDentBusiness {
	///<summary>Birthdate audit (Open Dental 26.1, Tools: the Audit Trail shows birthdate additions and changes). Patients.Insert and Patients.Update
	///call InsertBirthdateChangeSecurityLogEntry after they write the patient row, so every window and import that saves a patient is covered: Edit
	///Patient Information, Add Family Member, the Add Family window, sheet and eForm import, HL7, the 834 import and the bridges.</summary>
	public partial class Patients {
		///<summary>Birthdates before 1880 count as blank, as everywhere else in the program (patient.Birthdate is 0001-01-01 when not set).</summary>
		public static bool IsBirthdateBlank(DateTime dateBirth) {
			Meth.NoCheckMiddleTierRole();
			return dateBirth.Year<1880;
		}

		///<summary>The audit text for a birthdate that went from dateOld to dateNew, or "" when the date did not change. A blank date is shown as '',
		///like the other patient fields in the audit trail; dates are in the short-date format of the computer that saves the patient. The real
		///dates are logged, also when Edit Patient Information shows the birthdate masked.</summary>
		public static string GetBirthdateChangeLogText(DateTime dateOld,DateTime dateNew) {
			Meth.NoCheckMiddleTierRole();
			bool isBlankOld=IsBirthdateBlank(dateOld);
			bool isBlankNew=IsBirthdateBlank(dateNew);
			if(isBlankOld && isBlankNew) {
				return "";
			}
			if(!isBlankOld && !isBlankNew && dateOld.Date==dateNew.Date) {
				return "";
			}
			string textOld=isBlankOld ? "" : dateOld.ToShortDateString();
			string textNew=isBlankNew ? "" : dateNew.ToShortDateString();
			return "Patient birthdate changed from '"+textOld+"' to '"+textNew+"'";
		}

		///<summary>Adds a PatientEdit securitylog entry when the patient's birthdate was added, changed or removed. Pass a null patOld for a patient
		///that was just inserted: a birthdate it was created with counts as added. Called by Insert and Update on the server side after the row is
		///written, so callers never call it themselves. Without a signed-in user (a background service) the entry is made for user 0.</summary>
		public static void InsertBirthdateChangeSecurityLogEntry(Patient patOld,Patient patCur) {
			Meth.NoCheckMiddleTierRole();
			if(patCur==null) {
				return;
			}
			DateTime dateOld=patOld==null ? DateTime.MinValue : patOld.Birthdate;
			string logText=GetBirthdateChangeLogText(dateOld,patCur.Birthdate);
			if(logText=="") {
				return;
			}
			if(Security.CurUser==null) {
				SecurityLogs.MakeLogEntryNoCache(EnumPermType.PatientEdit,patCur.PatNum,logText);
				return;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.PatientEdit,patCur.PatNum,logText);
		}
	}
}
