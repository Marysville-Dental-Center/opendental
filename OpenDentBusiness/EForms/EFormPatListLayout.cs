using System;

namespace OpenDentBusiness {
	///<summary>Layout of a Patient List eForm field (Open Dental 25.2, EnumEFormFieldType.PatientList). Serialized as json into EFormField.ValueLabel and
	///EFormFieldDef.ValueLabel, the way EFormMedListLayout is for the Medication List. Open Dental's 25.3 database documentation says it holds at least
	///the Label; the json reader ignores any other members a stored row may carry.</summary>
	public class EFormPatListLayout {
		///<summary>The label above the read-only box and its Pick button.</summary>
		public string Label="Subscriber";

		///<summary>Reads the layout stored in ValueLabel. A blank or unreadable value gives the default layout, so a malformed row never stops a form from
		///loading.</summary>
		public static EFormPatListLayout Deserialize(string valueLabel) {
			if(string.IsNullOrWhiteSpace(valueLabel)) {
				return new EFormPatListLayout();
			}
			try {
				EFormPatListLayout eFormPatListLayout=Newtonsoft.Json.JsonConvert.DeserializeObject<EFormPatListLayout>(valueLabel);
				if(eFormPatListLayout!=null) {
					eFormPatListLayout.Label=eFormPatListLayout.Label??"";
					return eFormPatListLayout;
				}
			}
			catch(Exception) {
				//Not json. Older or hand-made rows may hold the label as plain text.
				return new EFormPatListLayout() { Label=valueLabel };
			}
			return new EFormPatListLayout();
		}

		public string Serialize() {
			return Newtonsoft.Json.JsonConvert.SerializeObject(this);
		}
	}
}
