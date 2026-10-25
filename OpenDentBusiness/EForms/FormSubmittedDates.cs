// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace OpenDentBusiness {
	///<summary>Open Dental 25.4 sheet.DateTimeSubmitted and eform.DateTimeSubmitted: when a patient form was submitted. eClipboard set it in Open
	///Dental (cut in this program); here the office sets it in Fill Sheet and Fill eForm, and a new form gets the current time when it is saved with a
	///signature and no date was entered (fork decision). MinValue means not submitted. Not the same as DateTSheetEdited or DateTEdited, which record
	///the last save.</summary>
	public class FormSubmittedDates {
		///<summary>The text shown in the Submitted box and the Submitted column: date and short time, or empty when not submitted.</summary>
		public static string ToDisplay(DateTime dateTimeSubmitted) {
			if(dateTimeSubmitted.Year<1880) {
				return "";
			}
			return dateTimeSubmitted.ToShortDateString()+" "+dateTimeSubmitted.ToShortTimeString();
		}

		///<summary>Reads the Submitted box. Empty text is valid and means not submitted (MinValue). Returns false for text that is not a date and
		///time, or a date before 1880.</summary>
		public static bool TryParse(string text,out DateTime dateTimeSubmitted) {
			dateTimeSubmitted=DateTime.MinValue;
			if(string.IsNullOrWhiteSpace(text)) {
				return true;
			}
			if(!DateTime.TryParse(text.Trim(),CultureInfo.CurrentCulture,DateTimeStyles.None,out DateTime dateTimeParsed)) {
				return false;
			}
			if(dateTimeParsed.Year<1880) {
				return false;
			}
			dateTimeSubmitted=dateTimeParsed;
			return true;
		}

		///<summary>Reads the Submitted box of a form whose stored value is dateTimeStored. Text that still shows the stored value (the box shows no
		///seconds) gives the stored value back unchanged, so opening and saving a form never alters it.</summary>
		public static bool TryParse(string text,DateTime dateTimeStored,out DateTime dateTimeSubmitted) {
			if((text??"").Trim()==ToDisplay(dateTimeStored)) {
				dateTimeSubmitted=dateTimeStored.Year<1880 ? DateTime.MinValue : dateTimeStored;
				return true;
			}
			return TryParse(text,out dateTimeSubmitted);
		}

		///<summary>The value to save. A date the user entered (or that was already stored) wins. Otherwise a new form that is being saved with a
		///signature gets dateTimeNow. Otherwise it stays not submitted.</summary>
		public static DateTime GetToSave(DateTime dateTimeEntered,bool isNewForm,bool isSigned,DateTime dateTimeNow) {
			if(dateTimeEntered.Year>1880) {
				return dateTimeEntered;
			}
			if(isNewForm && isSigned) {
				return dateTimeNow;
			}
			return DateTime.MinValue;
		}

		///<summary>The sheet types that are patient forms and get a Submitted date (INFERRED): patient forms, medical histories and consents, the
		///types the Patient Forms window lists.</summary>
		public static bool IsPatientFormSheetType(SheetTypeEnum sheetType) {
			return sheetType==SheetTypeEnum.PatientForm || sheetType==SheetTypeEnum.MedicalHistory || sheetType==SheetTypeEnum.Consent;
		}

		///<summary>True if any signature box of the eForm holds a signature.</summary>
		public static bool IsEFormSigned(List<EFormField> listEFormFields) {
			return listEFormFields.Exists(x => EFormFields.IsSigned(x));
		}

		///<summary>The submitted dates of a patient's sheets and eForms, for the Submitted column of the Patient Forms window. Columns: SheetNum,
		///EFormNum (one of them 0) and DateTimeSubmitted. Only rows that were submitted are returned.</summary>
		public static DataTable GetForPatient(long patNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetTable(MethodBase.GetCurrentMethod(),patNum);
			}
			string command="SELECT SheetNum,0 EFormNum,DateTimeSubmitted FROM sheet "
				+"WHERE PatNum="+POut.Long(patNum)+" AND DateTimeSubmitted>"+POut.DateT(new DateTime(1880,1,1))+" "
				+"UNION ALL "
				+"SELECT 0 SheetNum,EFormNum,DateTimeSubmitted FROM eform "
				+"WHERE PatNum="+POut.Long(patNum)+" AND DateTimeSubmitted>"+POut.DateT(new DateTime(1880,1,1));
			return Db.GetTable(command);
		}

		///<summary>The Submitted column text for one row of Sheets.GetPatientFormsTable, looked up in a table from GetForPatient. Empty for documents
		///and forms that were not submitted.</summary>
		public static string GetDisplayForRow(DataTable tableSubmitted,long sheetNum,long eFormNum) {
			if(tableSubmitted==null || (sheetNum==0 && eFormNum==0)) {
				return "";
			}
			for(int i=0;i<tableSubmitted.Rows.Count;i++) {
				long sheetNumRow=PIn.Long(tableSubmitted.Rows[i]["SheetNum"].ToString());
				long eFormNumRow=PIn.Long(tableSubmitted.Rows[i]["EFormNum"].ToString());
				if((sheetNum!=0 && sheetNumRow==sheetNum) || (eFormNum!=0 && eFormNumRow==eFormNum)) {
					return ToDisplay(PIn.DateT(tableSubmitted.Rows[i]["DateTimeSubmitted"].ToString()));
				}
			}
			return "";
		}
	}
}
