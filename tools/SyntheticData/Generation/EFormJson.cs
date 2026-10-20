// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace SyntheticData.Generation {
	///<summary>Stored shapes of the eForm fields whose ValueLabel or ValueString is not plain text. The shapes follow the published database
	///docs and the eForm classes in OpenDentBusiness/TableTypes/EFormField.cs; see README.md, "eForm value formats".</summary>
	public static class EFormJson {
		///<summary>Pick lists (PickListVis, PickListDb) and multiple conditional values are pipe-delimited.</summary>
		public const char Delimiter='|';

		public static string JoinPickList(IEnumerable<string> items) {
			return string.Join(Delimiter.ToString(),items);
		}

		public static string[] SplitPickList(string pickList) {
			return pickList.Split(Delimiter);
		}

		///<summary>ValueLabel of a field (or field def). MedicationList holds an EFormMedListLayout and PatientList (25.1) an
		///EFormPatListLayout, both serialized as JSON. Every other type holds the label itself.</summary>
		public static string ValueLabel(int fieldType,string label) {
			if(fieldType==OdValues.EfMedicationList) {
				return JsonConvert.SerializeObject(new MedListLayout { Title=label });
			}
			if(fieldType==OdValues.EfPatientList) {
				return JsonConvert.SerializeObject(new PatListLayout { Label=label });
			}
			return label;
		}

		///<summary>ValueString of a MedicationList field: a JSON list of EFormMed objects. An empty list is "[]", as prefill writes it.</summary>
		public static string MedicationList(IEnumerable<string> medNames) {
			return JsonConvert.SerializeObject(medNames.Select(x => new Med { MedName=x }).ToList());
		}

		///<summary>ValueString of an Ins Pick field (25.2) that names an existing subscription.</summary>
		public static string InsPickExistingSub(long insSubNum) {
			return "InsSubNum:"+insSubNum;
		}

		///<summary>ValueString of an Ins Pick field (25.2) that names an existing pending insurance row.</summary>
		public static string InsPickExistingPending(long insPendingNum) {
			return "InsPendingNum:"+insPendingNum;
		}

		///<summary>ValueString of an Ins Pick field (25.2) when the patient reports new insurance. None is "".</summary>
		public const string InsPickNew="New";

		///<summary>Modeled on EFormMed.</summary>
		private class Med {
			public string MedName="";
			public string StrengthFreq="";
		}

		///<summary>Modeled on EFormMedListLayout, with its defaults.</summary>
		private class MedListLayout {
			public string Title="Medication List";
			public string HeaderCol1="Medication";
			public string HeaderCol2="Strength/Freq";
			public string StrDelete="Delete";
			public string StrAdd="Add";
			public string StrNone="None";
			public int WidthCol1=0;
			public int WidthCol2=0;
			public bool IsCol2Visible=true;
			public bool PrefillCol1=true;
			public bool PrefillCol2=true;
			public bool ImportCol1=true;
			public bool ImportCol2Overwrite=false;
			public bool ImportCol2OverwriteDate=false;
			public bool ImportCol2Append=false;
			public bool ImportCol2AppendDate=false;
		}

		///<summary>EFormPatListLayout, the class the 25.2 database docs name; the docs only say it holds the label.</summary>
		private class PatListLayout {
			public string Label="";
		}
	}
}
