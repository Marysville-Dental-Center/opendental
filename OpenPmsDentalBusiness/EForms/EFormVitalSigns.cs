// Open Dental 26.2 (release notes, eServices): "Vital signs can be imported from eForms". Package P262-MISC.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>Vital signs on eForms: a text field can link to the patient's height, weight, blood pressure (systolic and diastolic) or pulse, and the
	///reviewed import (FormSheetImport, its "Vital Signs" section) adds one vital sign entry (the vitalsign table, the Vital Signs tab of the Medical
	///window) from the form's answers.
	///<para>Sources: the 26.2 release notes say only that vital signs can be imported from eForms; the vitalsign columns, units and default LOINC codes
	///come from Open Dental's 26.2 database documentation (Height in inches, Weight in pounds, BpSystolic and BpDiastolic in mmHg, Pulse in beats per
	///minute; "We will default to Body Height=8302-2" and "Body Weight=29463-7"). Its list of the links that may Auto Import (eformfield.AutoImport)
	///has no vital sign, so vital signs are never auto imported.</para>
	///<para>INFERRED (no public source states them): the link names (DBLINK_*); the fields start empty, since each form is a new measurement; the
	///accepted formats and the plausible ranges (GetRange); the entry's date (GetDateTaken); one entry per form, made only from the rows the office
	///leaves checked; an entry identical to one already on file for that date is not added again; a field hidden by conditional logic, or left
	///empty, is not imported; when two fields link to the same vital sign, the last one with an answer counts; BMI percentile, weight code and the
	///LOINC exam codes are set the way the vital sign window sets them for a new entry.</para></summary>
	public class EFormVitalSigns {
		public const string DBLINK_HEIGHT="vitalHeight";
		public const string DBLINK_WEIGHT="vitalWeight";
		public const string DBLINK_BP_SYSTOLIC="vitalBpSystolic";
		public const string DBLINK_BP_DIASTOLIC="vitalBpDiastolic";
		public const string DBLINK_PULSE="vitalPulse";
		///<summary>The links, in the order the import window lists them.</summary>
		public static readonly string[] ArrayDbLinks={ DBLINK_HEIGHT,DBLINK_WEIGHT,DBLINK_BP_SYSTOLIC,DBLINK_BP_DIASTOLIC,DBLINK_PULSE };

		#region Links
		///<summary>True for a vital sign link.</summary>
		public static bool IsVitalLink(string dbLink) {
			return !string.IsNullOrEmpty(dbLink) && ArrayDbLinks.Contains(dbLink);
		}

		///<summary>The label a new field gets when the office picks the link, naming the unit the vital sign window uses. The link itself for any other
		///link.</summary>
		public static string GetDefaultLabel(string dbLink) {
			switch(dbLink) {
				case DBLINK_HEIGHT: return "Height (inches)";
				case DBLINK_WEIGHT: return "Weight (pounds)";
				case DBLINK_BP_SYSTOLIC: return "Blood pressure, systolic (mmHg)";
				case DBLINK_BP_DIASTOLIC: return "Blood pressure, diastolic (mmHg)";
				case DBLINK_PULSE: return "Pulse (beats per minute)";
				default: return dbLink;
			}
		}

		///<summary>The name of the vital sign in the import window.</summary>
		public static string GetFieldDisplay(string dbLink) {
			switch(dbLink) {
				case DBLINK_HEIGHT: return "Height (in)";
				case DBLINK_WEIGHT: return "Weight (lb)";
				case DBLINK_BP_SYSTOLIC: return "BP Systolic";
				case DBLINK_BP_DIASTOLIC: return "BP Diastolic";
				case DBLINK_PULSE: return "Pulse";
				default: return dbLink;
			}
		}

		///<summary>The plausible range of a vital sign (INFERRED), inclusive. An answer outside it is not imported unless the office corrects it.</summary>
		public static void GetRange(string dbLink,out double min,out double max) {
			switch(dbLink) {
				case DBLINK_HEIGHT: min=10; max=108; return;//inches
				case DBLINK_WEIGHT: min=1; max=1500; return;//pounds
				case DBLINK_BP_SYSTOLIC: min=50; max=300; return;//mmHg
				case DBLINK_BP_DIASTOLIC: min=20; max=200; return;//mmHg
				case DBLINK_PULSE: min=20; max=300; return;//beats per minute
				default: min=0; max=0; return;
			}
		}

		///<summary>True for the vital signs the vitalsign table stores as whole numbers (blood pressure and pulse).</summary>
		public static bool IsWholeNumber(string dbLink) {
			return dbLink==DBLINK_BP_SYSTOLIC || dbLink==DBLINK_BP_DIASTOLIC || dbLink==DBLINK_PULSE;
		}
		#endregion Links

		#region Parse and validate
		///<summary>Reads an answer. Empty (or blank) is no answer: false with no error. Accepted: a number in the current culture or with a period as the
		///decimal mark, optionally followed by its unit (in, inch, inches, " for height; lb, lbs, pound, pounds, # for weight; mmHg for blood pressure;
		///bpm for pulse); for height also feet and inches (5'10", 5' 10, 5 ft 10 in, 5ft). Blood pressure and pulse must be whole numbers. A value outside
		///GetRange gives an error. Returns true with the value in the vital sign window's unit when the answer can be imported.</summary>
		public static bool TryParse(string dbLink,string answer,out double value,out string error) {
			value=0;
			error="";
			string text=(answer??"").Trim();
			if(text=="") {
				return false;
			}
			string textLower=text.ToLowerInvariant();
			bool isParsed;
			if(dbLink==DBLINK_HEIGHT) {
				isParsed=TryParseHeight(textLower,out value);
			}
			else {
				string number=StripUnit(dbLink,textLower);
				isParsed=TryParseNumber(number,out value);
			}
			if(!isParsed) {
				error=IsWholeNumber(dbLink) ? "not a whole number" : "not a number";
				return false;
			}
			if(IsWholeNumber(dbLink) && value!=Math.Floor(value)) {
				error="not a whole number";
				return false;
			}
			GetRange(dbLink,out double min,out double max);
			if(value<min || value>max) {
				error="outside "+min.ToString(CultureInfo.CurrentCulture)+" to "+max.ToString(CultureInfo.CurrentCulture);
				return false;
			}
			return true;
		}

		private static bool TryParseHeight(string textLower,out double inches) {
			inches=0;
			//Feet and inches: 5'10", 5' 10, 5 ft 10 in, 5ft10in, 5 feet 10 inches, 5', 5 ft.
			Match match=Regex.Match(textLower,@"^(\d+(?:[.,]\d+)?)\s*(?:'|ft\.?|feet|foot)\s*(?:(\d+(?:[.,]\d+)?)\s*(?:""|''|in\.?|inch|inches)?)?$");
			if(match.Success) {
				if(!TryParseNumber(match.Groups[1].Value,out double feet)) {
					return false;
				}
				double inchesPart=0;
				if(match.Groups[2].Success && !TryParseNumber(match.Groups[2].Value,out inchesPart)) {
					return false;
				}
				if(inchesPart>=12) {
					return false;//5'14" is not a height
				}
				inches=feet*12+inchesPart;
				return true;
			}
			return TryParseNumber(StripUnit(DBLINK_HEIGHT,textLower),out inches);
		}

		///<summary>Removes the unit at the end of an answer (lower case). The answer is returned unchanged when it has no unit of that vital sign.</summary>
		private static string StripUnit(string dbLink,string textLower) {
			string[] arrayUnits;
			switch(dbLink) {
				case DBLINK_HEIGHT: arrayUnits=new[] { "inches","inch","in.","in","\"","''" }; break;
				case DBLINK_WEIGHT: arrayUnits=new[] { "pounds","pound","lbs.","lbs","lb.","lb","#" }; break;
				case DBLINK_BP_SYSTOLIC:
				case DBLINK_BP_DIASTOLIC: arrayUnits=new[] { "mmhg","mm hg" }; break;
				case DBLINK_PULSE: arrayUnits=new[] { "bpm","beats per minute","/min" }; break;
				default: return textLower;
			}
			for(int i=0;i<arrayUnits.Length;i++) {
				if(textLower.EndsWith(arrayUnits[i])) {
					return textLower.Substring(0,textLower.Length-arrayUnits[i].Length).Trim();
				}
			}
			return textLower;
		}

		///<summary>A plain number: the current culture first, then a period as the decimal mark. No group separators, signs or exponents.</summary>
		private static bool TryParseNumber(string text,out double value) {
			value=0;
			if(string.IsNullOrWhiteSpace(text) || !Regex.IsMatch(text.Trim(),@"^\d+(?:[.,]\d+)?$")) {
				return false;
			}
			if(double.TryParse(text.Trim(),NumberStyles.AllowDecimalPoint,CultureInfo.CurrentCulture,out value)) {
				return true;
			}
			return double.TryParse(text.Trim(),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value);
		}

		///<summary>Checked when the form is saved (EForms.Validate): a text field linked to a vital sign must hold an answer that can be imported, or
		///nothing. Returns the error, or empty string. Fields hidden by conditional logic are not checked.</summary>
		public static string Validate(EFormField eFormField) {
			if(eFormField==null || !IsVitalLink(eFormField.DbLink) || eFormField.IsHiddenCondit) {
				return "";
			}
			TryParse(eFormField.DbLink,eFormField.ValueString,out double _,out string error);
			if(error=="") {
				return "";
			}
			string label=eFormField.ValueLabel;
			if(string.IsNullOrWhiteSpace(label)) {
				label=GetDefaultLabel(eFormField.DbLink);
			}
			return label+": "+error+".";
		}
		#endregion Parse and validate

		#region Import
		///<summary>The date of the vital sign entry an import makes: the day the form was submitted, else the day it shows, else today.</summary>
		public static DateTime GetDateTaken(EForm eForm,DateTime dateToday) {
			if(eForm.DateTimeSubmitted.Year>1880) {
				return eForm.DateTimeSubmitted.Date;
			}
			if(eForm.DateTimeShown.Year>1880) {
				return eForm.DateTimeShown.Date;
			}
			return dateToday.Date;
		}

		///<summary>What importing the vital sign fields of a filled eForm would do, one item per vital sign in the order of ArrayDbLinks. Fields hidden by
		///conditional logic and empty answers are left out. When two fields link to the same vital sign, the last one with an answer counts. An answer
		///that can't be imported gives an item with an error. A systolic value that is not higher than the diastolic one is an error on the systolic item.
		///vitalsignLatest (may be null) gives the Current Value. Does not touch the database.</summary>
		public static List<EFormVitalImport> GetImports(EForm eForm,Vitalsign vitalsignLatest) {
			List<EFormVitalImport> listImports=new List<EFormVitalImport>();
			if(eForm?.ListEFormFields==null || !eForm.ListEFormFields.Exists(x => IsVitalLink(x.DbLink))) {
				return listImports;
			}
			EFormFields.SetIsHiddenConditFlags(eForm.ListEFormFields);
			for(int l=0;l<ArrayDbLinks.Length;l++) {
				EFormField eFormField=eForm.ListEFormFields
					.LastOrDefault(x => x.DbLink==ArrayDbLinks[l] && !x.IsHiddenCondit && x.FieldType==EnumEFormFieldType.TextField
						&& !string.IsNullOrWhiteSpace(x.ValueString));
				if(eFormField==null) {
					continue;
				}
				EFormVitalImport eFormVitalImport=new EFormVitalImport();
				eFormVitalImport.DbLink=ArrayDbLinks[l];
				eFormVitalImport.FieldDisplay=GetFieldDisplay(ArrayDbLinks[l]);
				eFormVitalImport.Answer=eFormField.ValueString.Trim();
				eFormVitalImport.ValueOld=GetValueDisplay(ArrayDbLinks[l],vitalsignLatest);
				if(TryParse(ArrayDbLinks[l],eFormField.ValueString,out double value,out string error)) {
					eFormVitalImport.Value=value;
				}
				eFormVitalImport.Error=error;
				listImports.Add(eFormVitalImport);
			}
			CheckBloodPressure(listImports);
			return listImports;
		}

		///<summary>Sets the error of the systolic item when both pressures can be imported and the systolic one is not higher.</summary>
		public static void CheckBloodPressure(List<EFormVitalImport> listImports) {
			EFormVitalImport importSys=listImports.Find(x => x.DbLink==DBLINK_BP_SYSTOLIC);
			EFormVitalImport importDia=listImports.Find(x => x.DbLink==DBLINK_BP_DIASTOLIC);
			if(importSys==null || importDia==null || importSys.Error!="" || importDia.Error!="") {
				return;
			}
			if(importSys.Value<=importDia.Value) {
				importSys.Error="not higher than the diastolic value";
			}
		}

		///<summary>A stored vital sign value as the import window shows it, with the entry's date: "64 (11/2/2026)". Empty when there is no entry or the
		///value is 0 (not recorded).</summary>
		public static string GetValueDisplay(string dbLink,Vitalsign vitalsign) {
			if(vitalsign==null) {
				return "";
			}
			double value;
			switch(dbLink) {
				case DBLINK_HEIGHT: value=vitalsign.Height; break;
				case DBLINK_WEIGHT: value=vitalsign.Weight; break;
				case DBLINK_BP_SYSTOLIC: value=vitalsign.BpSystolic; break;
				case DBLINK_BP_DIASTOLIC: value=vitalsign.BpDiastolic; break;
				case DBLINK_PULSE: value=vitalsign.Pulse; break;
				default: return "";
			}
			if(value==0) {
				return "";
			}
			return FormatValue(value)+" ("+vitalsign.DateTaken.ToShortDateString()+")";
		}

		///<summary>A value as the import window shows it: no trailing zeros.</summary>
		public static string FormatValue(double value) {
			return value.ToString("0.##",CultureInfo.CurrentCulture);
		}

		///<summary>The vital sign entry for the given values, dated dateTaken, before it is inserted. Values not imported stay 0, as in the vital sign
		///window. With height and weight: the BMI percentile for a patient aged 3 to 16 on January 1 of that year (else -1) and its LOINC code, and the
		///weight code, as the vital sign window works them out. LOINC exam codes (8302-2 height, 29463-7 weight) only when the value is imported and the
		///code is in the LOINC table; pass loincExists to decide that (null means none is).</summary>
		public static Vitalsign CreateVitalsign(Patient patient,DateTime dateTaken,List<EFormVitalImport> listImports,Func<string,bool> loincExists) {
			Vitalsign vitalsign=new Vitalsign();
			vitalsign.PatNum=patient.PatNum;
			vitalsign.DateTaken=dateTaken.Date;
			vitalsign.Documentation="";
			vitalsign.WeightCode="";
			vitalsign.HeightExamCode="";
			vitalsign.WeightExamCode="";
			vitalsign.BMIExamCode="";
			vitalsign.BMIPercentile=-1;
			for(int i=0;i<listImports.Count;i++) {
				if(listImports[i].Error!="") {
					continue;
				}
				switch(listImports[i].DbLink) {
					case DBLINK_HEIGHT: vitalsign.Height=(float)listImports[i].Value; break;
					case DBLINK_WEIGHT: vitalsign.Weight=(float)listImports[i].Value; break;
					case DBLINK_BP_SYSTOLIC: vitalsign.BpSystolic=(int)listImports[i].Value; break;
					case DBLINK_BP_DIASTOLIC: vitalsign.BpDiastolic=(int)listImports[i].Value; break;
					case DBLINK_PULSE: vitalsign.Pulse=(int)listImports[i].Value; break;
				}
			}
			if(loincExists==null) {
				loincExists=x => false;
			}
			if(vitalsign.Height>0 && loincExists("8302-2")) {
				vitalsign.HeightExamCode="8302-2";//Body height
			}
			if(vitalsign.Weight>0 && loincExists("29463-7")) {
				vitalsign.WeightExamCode="29463-7";//Body weight
			}
			if(vitalsign.Height>0 && vitalsign.Weight>0) {
				float bmi=Vitalsigns.CalcBMI(vitalsign.Weight,vitalsign.Height);
				int ageBeforeJanFirst=dateTaken.Year-patient.Birthdate.Year-1;
				if(patient.Birthdate.Year>1880 && ageBeforeJanFirst>2 && ageBeforeJanFirst<17) {
					try {
						vitalsign.BMIPercentile=Vitalsigns.GetBMIPercentile(bmi,patient,dateTaken.Date,Vitalsigns.GetListLMS());
					}
					catch(Exception ex) {
						ex.DoNothing();
						vitalsign.BMIPercentile=-1;
					}
				}
				if(vitalsign.BMIPercentile>-1 && loincExists("59576-9")) {
					vitalsign.BMIExamCode="59576-9";//Body mass index (BMI) [Percentile] Per age and gender
				}
				if(patient.Birthdate.Year>1880) {
					vitalsign.WeightCode=Vitalsigns.SetWeightCodes(Vitalsigns.GetBMIInterventionCode(bmi,ageBeforeJanFirst));
				}
			}
			return vitalsign;
		}

		///<summary>True when the two entries hold the same date and measurements, so importing the same form again adds nothing.</summary>
		public static bool IsSameMeasurement(Vitalsign vitalsignA,Vitalsign vitalsignB) {
			return vitalsignA.DateTaken.Date==vitalsignB.DateTaken.Date
				&& Math.Abs(vitalsignA.Height-vitalsignB.Height)<0.005f
				&& Math.Abs(vitalsignA.Weight-vitalsignB.Weight)<0.005f
				&& vitalsignA.BpSystolic==vitalsignB.BpSystolic
				&& vitalsignA.BpDiastolic==vitalsignB.BpDiastolic
				&& vitalsignA.Pulse==vitalsignB.Pulse;
		}

		///<summary>Adds one vital sign entry for the patient from the items without an error, dated dateTaken, unless there is nothing to import or the
		///same entry is already on file. Logs it (EFormImport, naming the source, for example the eForm). Returns what happened.</summary>
		public static EFormVitalImportResult Import(long patNum,DateTime dateTaken,List<EFormVitalImport> listImports,string source) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<EFormVitalImportResult>(MethodBase.GetCurrentMethod(),patNum,dateTaken,listImports,source);
			}
			EFormVitalImportResult result=new EFormVitalImportResult();
			List<EFormVitalImport> listImportsOk=(listImports??new List<EFormVitalImport>()).FindAll(x => x.Error=="");
			if(patNum==0 || listImportsOk.Count==0) {
				return result;
			}
			Patient patient=Patients.GetPat(patNum);
			if(patient==null) {
				return result;
			}
			Vitalsign vitalsign=CreateVitalsign(patient,dateTaken,listImportsOk,x => Loincs.GetByCode(x)!=null);
			if(Vitalsigns.Refresh(patNum).Exists(x => IsSameMeasurement(x,vitalsign))) {
				result.IsDuplicate=true;
				result.Description="The same vital signs are already on file for "+vitalsign.DateTaken.ToShortDateString()+".";
				return result;
			}
			result.VitalsignNum=Vitalsigns.Insert(vitalsign);
			string strValues=string.Join(", ",listImportsOk.Select(x => x.FieldDisplay+" "+FormatValue(x.Value)));
			result.Description="Vital signs added from "+source+", taken "+vitalsign.DateTaken.ToShortDateString()+": "+strValues;
			SecurityLogs.MakeLogEntry(EnumPermType.EFormImport,patNum,result.Description);
			return result;
		}
		#endregion Import
	}

	///<summary>What EFormVitalSigns.Import did.</summary>
	[Serializable]
	public class EFormVitalImportResult {
		///<summary>The entry added. 0 when nothing was added.</summary>
		public long VitalsignNum;
		///<summary>True when the same entry was already on file, so nothing was added.</summary>
		public bool IsDuplicate;
		///<summary>A line for the user and the audit trail. Empty when there was nothing to import.</summary>
		public string Description="";
	}

	///<summary>What importing one vital sign from an eForm would do.</summary>
	[Serializable]
	public class EFormVitalImport {
		///<summary>One of EFormVitalSigns.ArrayDbLinks.</summary>
		public string DbLink="";
		///<summary>The vital sign's name in the import window.</summary>
		public string FieldDisplay="";
		///<summary>The answer as the eForm stores it, trimmed.</summary>
		public string Answer="";
		///<summary>The patient's latest stored value, with its date, or empty.</summary>
		public string ValueOld="";
		///<summary>The value to import, in the vital sign window's unit. 0 when there is an error.</summary>
		public double Value;
		///<summary>Why the answer can't be imported. Empty when it can.</summary>
		public string Error="";
	}
}
