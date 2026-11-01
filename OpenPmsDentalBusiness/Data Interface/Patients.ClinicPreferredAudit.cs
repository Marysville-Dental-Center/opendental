// Audit Trail entries for changes to a patient's clinic (Open Dental 25.2) and preferred name (Open Dental 25.3). Package B-AUDIT; see
// docs/port-notes/B-AUDIT.md for the save paths that call these methods.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenPmsDentalBusiness {
	public partial class Patients {
		///<summary>The most PatNums written in one batch of clinic change entries by ChangeClinicsForAll.</summary>
		private const int CLINIC_CHANGE_LOG_BATCH_SIZE=500;

		///<summary>The PatientEdit audit lines for the patient's preferred name and clinic, in the same "Patient ... changed from 'x' to 'y'" form
		///as the other lines of InsertAddressChangeSecurityLogEntry. Empty when neither changed. A blank and a null preferred name are the same.</summary>
		public static string GetPreferredAndClinicChangeLogText(Patient patOld,Patient patCur) {
			Meth.NoCheckMiddleTierRole();
			if(patOld==null || patCur==null) {
				return "";
			}
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(SecurityLogEntryHelper(patOld.Preferred??"",patCur.Preferred??"","preferred name"));
			stringBuilder.Append(GetClinicChangeLogLine(patOld.ClinicNum,patCur.ClinicNum));
			return stringBuilder.ToString();
		}

		///<summary>"Patient clinic changed from 'A' to 'B'" plus a line break, or empty when the clinic did not change. Clinics are named by their
		///abbreviation and clinic 0 by "None". A clinic that is not in the cache, or two clinics with the same abbreviation, also show the ClinicNum
		///so the entry never reads as a change from a clinic to itself.</summary>
		public static string GetClinicChangeLogLine(long clinicNumOld,long clinicNumNew) {
			Meth.NoCheckMiddleTierRole();
			if(clinicNumOld==clinicNumNew) {
				return "";
			}
			string clinicOld=GetClinicDescForLog(clinicNumOld);
			string clinicNew=GetClinicDescForLog(clinicNumNew);
			if(clinicOld==clinicNew) {
				clinicOld+=" (ClinicNum "+clinicNumOld+")";
				clinicNew+=" (ClinicNum "+clinicNumNew+")";
			}
			return "Patient clinic changed from '"+clinicOld+"' to '"+clinicNew+"'\r\n";
		}

		///<summary>"None" for 0, otherwise the clinic's abbreviation, or "ClinicNum N" when the clinic is not in the cache or has no abbreviation.</summary>
		private static string GetClinicDescForLog(long clinicNum) {
			Meth.NoCheckMiddleTierRole();//Private method.
			if(clinicNum==0) {
				return "None";
			}
			string abbr=Clinics.GetAbbr(clinicNum);
			if(string.IsNullOrWhiteSpace(abbr)) {
				return "ClinicNum "+clinicNum;
			}
			return abbr;
		}

		///<summary>Adds a PatientEdit securitylog entry when the patient's preferred name or clinic changed. For the save paths that change these
		///fields without calling InsertAddressChangeSecurityLogEntry (which already includes them), such as sheet and eForm import, inbound HL7 and
		///clone synch. The optional source is added as a last line, "From [source].", so the Audit Trail shows where the change came from.</summary>
		public static void InsertPreferredAndClinicChangeSecurityLogEntry(Patient patOld,Patient patCur,string source="") {
			Meth.NoCheckMiddleTierRole();
			string logText=GetPreferredAndClinicChangeLogText(patOld,patCur);
			if(logText=="") {
				return;
			}
			if(!string.IsNullOrWhiteSpace(source)) {
				logText+="From "+source+".";
			}
			SecurityLogs.MakeLogEntry(EnumPermType.PatientEdit,patCur.PatNum,logText);
		}

		///<summary>The patients that ChangeClinicsForAll is about to move, read just before its update.</summary>
		private static List<long> GetPatNumsForClinicChange(long clinicNumFrom,long clinicNumTo) {
			Meth.NoCheckMiddleTierRole();//Private method, called on the server side of ChangeClinicsForAll.
			if(clinicNumFrom==clinicNumTo) {
				return new List<long>();
			}
			string command="SELECT PatNum FROM patient WHERE ClinicNum="+POut.Long(clinicNumFrom);
			return Db.GetListLong(command);
		}

		///<summary>One PatientEdit entry per patient that ChangeClinicsForAll moved, so the change shows in each patient's own Audit Trail. The
		///summary entry the Clinics window writes for the whole move (PatNum 0) stays as it was. Written in batches.</summary>
		private static void InsertClinicChangeSecurityLogEntries(List<long> listPatNums,long clinicNumFrom,long clinicNumTo) {
			Meth.NoCheckMiddleTierRole();//Private method, called on the server side of ChangeClinicsForAll.
			if(listPatNums==null || listPatNums.Count==0 || clinicNumFrom==clinicNumTo) {
				return;
			}
			string logText=GetClinicChangeLogLine(clinicNumFrom,clinicNumTo)+"From moving all patients of the clinic to another clinic.";
			for(int i=0;i<listPatNums.Count;i+=CLINIC_CHANGE_LOG_BATCH_SIZE) {
				List<long> listPatNumsBatch=listPatNums.Skip(i).Take(CLINIC_CHANGE_LOG_BATCH_SIZE).ToList();
				SecurityLogs.MakeLogEntry(EnumPermType.PatientEdit,listPatNumsBatch,logText);
			}
		}
	}
}
