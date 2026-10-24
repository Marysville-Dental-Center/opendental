using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Helpers for the Ins Pick eForm field (Open Dental 25.2, EnumEFormFieldType.InsPick) and the Patient List field it uses for the subscriber.
	///An Ins Pick field asks about one insurance slot with three options: None, New and Existing. Its ValueString holds the answer as Open Dental's
	///25.3 database documentation describes it: empty for None, "New" for New, and "InsSubNum:####" or "InsPendingNum:####" for Existing.</summary>
	public class EFormInsPick {
		public const string VALUE_NEW="New";
		public const string PREFIX_INSSUB="InsSubNum:";
		public const string PREFIX_INSPENDING="InsPendingNum:";
		///<summary>Database link of an Ins Pick field for the first insurance slot. Open Dental's 25.3 database documentation lists it among the links
		///that may Auto Import.</summary>
		public const string DBLINK_INS1="ins1PlanFromList";
		///<summary>Database link of an Ins Pick field for the second insurance slot.</summary>
		public const string DBLINK_INS2="ins2PlanFromList";

		///<summary>The English labels of the three options, in the order the values are stored: None, New, Existing.</summary>
		public static List<string> GetOptionsDefault() {
			return new List<string>() { "None","New","Existing" };
		}

		///<summary>The pick list of the field (translated or not), always three items. A stored list with a different count falls back to the
		///defaults.</summary>
		public static List<string> GetOptions(string pickListVis) {
			List<string> listOptions=(pickListVis??"").Split('|').ToList();
			if(listOptions.Count!=3) {
				return GetOptionsDefault();
			}
			return listOptions;
		}

		///<summary>The insurance slot (ordinal) a field stands for, from its database link: 1 for ins1PlanFromList, 2 for ins2PlanFromList, otherwise 0.
		///Open Dental does not document how a field names its slot; using the documented ins1/ins2 PlanFromList links is our design.</summary>
		public static byte GetOrdinal(string dbLink) {
			if(dbLink==DBLINK_INS1) {
				return 1;
			}
			if(dbLink==DBLINK_INS2) {
				return 2;
			}
			return 0;
		}

		///<summary>0 for None (or no answer), 1 for New, 2 for Existing.</summary>
		public static int GetOptionIndex(string valueString) {
			if(valueString==VALUE_NEW) {
				return 1;
			}
			if(IsExisting(valueString)) {
				return 2;
			}
			return 0;
		}

		public static bool IsExisting(string valueString) {
			return GetInsSubNum(valueString)>0 || GetInsPendingNum(valueString)>0;
		}

		///<summary>The InsSubNum of an "InsSubNum:####" answer, otherwise 0.</summary>
		public static long GetInsSubNum(string valueString) {
			return GetNum(valueString,PREFIX_INSSUB);
		}

		///<summary>The InsPendingNum of an "InsPendingNum:####" answer, otherwise 0.</summary>
		public static long GetInsPendingNum(string valueString) {
			return GetNum(valueString,PREFIX_INSPENDING);
		}

		private static long GetNum(string valueString,string prefix) {
			if(string.IsNullOrEmpty(valueString) || !valueString.StartsWith(prefix)) {
				return 0;
			}
			if(!long.TryParse(valueString.Substring(prefix.Length).Trim(),out long num) || num<=0) {
				return 0;
			}
			return num;
		}

		public static string ToValueInsSub(long insSubNum) {
			return PREFIX_INSSUB+insSubNum;
		}

		public static string ToValueInsPending(long insPendingNum) {
			return PREFIX_INSPENDING+insPendingNum;
		}

		///<summary>The English option that a conditional child compares with, for the current answer. Empty answers are None.</summary>
		public static string GetValParentEnglish(EFormField eFormField) {
			List<string> listEnglish=GetOptions(eFormField.PickListVisEnglish);
			if(string.IsNullOrEmpty(eFormField.PickListVisEnglish)) {
				listEnglish=GetOptions(eFormField.PickListVis);
			}
			return listEnglish[GetOptionIndex(eFormField.ValueString)];
		}

		///<summary>The PatNum picked in a Patient List field, or 0 when nothing was picked.</summary>
		public static long GetPatNumPicked(EFormField eFormField) {
			if(eFormField==null || !long.TryParse((eFormField.ValueString??"").Trim(),out long patNum) || patNum<=0) {
				return 0;
			}
			return patNum;
		}

		///<summary>The fields that belong to an Ins Pick field: the fields whose conditional parent is the Ins Pick field (normally shown when the answer
		///is New). When there are none and the field names a slot, the ins1 or ins2 text and radio fields of that slot are used instead.</summary>
		public static List<EFormField> GetChildFields(EFormField eFormFieldInsPick,List<EFormField> listEFormFields) {
			string label=EFormFields.GetLabelEnglish(eFormFieldInsPick);
			string labelTrunc=label.Substring(0,Math.Min(label.Length,255));
			List<EFormField> listChildren=new List<EFormField>();
			if(labelTrunc!="") {
				listChildren=listEFormFields.FindAll(x => x!=eFormFieldInsPick && x.ConditionalParent!="" && x.ConditionalParent==labelTrunc);
			}
			if(listChildren.Count>0) {
				return listChildren;
			}
			byte ordinal=GetOrdinal(eFormFieldInsPick.DbLink);
			if(ordinal==0) {
				return listChildren;
			}
			string prefix="ins"+ordinal;
			return listEFormFields.FindAll(x => x!=eFormFieldInsPick && x.FieldType.In(EnumEFormFieldType.TextField,EnumEFormFieldType.RadioButtons)
				&& x.DbLink.StartsWith(prefix) && GetInsFieldSuffix(x.DbLink)!="");
		}

		///<summary>For an ins1 or ins2 database link like "ins2GroupNum", the part after the slot ("GroupNum"). Empty for other links.</summary>
		public static string GetInsFieldSuffix(string dbLink) {
			Match match=Regex.Match(dbLink??"","^ins[12]([A-Za-z]+)$");
			if(!match.Success) {
				return "";
			}
			return match.Groups[1].Value;
		}

		///<summary>The value of the child field with this ins suffix (like "CarrierName"), trimmed. Empty when there is no such field.</summary>
		public static string GetChildValue(List<EFormField> listChildren,string suffix) {
			EFormField eFormField=listChildren.Find(x => GetInsFieldSuffix(x.DbLink)==suffix);
			if(eFormField==null) {
				return "";
			}
			return (eFormField.ValueString??"").Trim();
		}

		///<summary>A read-only summary of an Existing answer, read live: carrier name and the subscriber's first and last name (kiosk-safe), with
		///"(pending)" for pending insurance. Empty for None, New, or a row that no longer exists.</summary>
		public static string GetExistingSummary(string valueString) {
			long insSubNum=GetInsSubNum(valueString);
			if(insSubNum>0) {
				InsSub insSub=InsSubs.GetOne(insSubNum);
				if(insSub==null) {
					return "";
				}
				InsPlan insPlan=InsPlans.GetPlan(insSub.PlanNum,null);
				string carrierName=insPlan==null ? "" : Carriers.GetName(insPlan.CarrierNum);
				return carrierName+" ("+Lans.g("EFormInsPick","subscriber")+": "+GetNameFirstLast(insSub.Subscriber)+")";
			}
			long insPendingNum=GetInsPendingNum(valueString);
			if(insPendingNum>0) {
				InsPending insPending=InsPendings.GetOne(insPendingNum);
				if(insPending==null) {
					return "";
				}
				return insPending.CarrierName+" ("+Lans.g("EFormInsPick","subscriber")+": "+GetNameFirstLast(insPending.PatNumSubscriber)+") "
					+Lans.g("EFormInsPick","(pending)");
			}
			return "";
		}

		///<summary>The choices for an Existing answer: every subscription of a family member, then the family's pending insurance (one row per distinct
		///insurance). Each has the value to store ("InsSubNum:####" or "InsPendingNum:####") and a line showing carrier, subscriber and subscriber ID.</summary>
		public static List<EFormInsPickChoice> GetExistingChoices(long patNumForm) {
			List<EFormInsPickChoice> listChoices=new List<EFormInsPickChoice>();
			if(patNumForm==0) {
				return listChoices;
			}
			Family family=Patients.GetFamily(patNumForm);
			List<InsSub> listInsSubs=InsSubs.RefreshForFam(family);
			List<InsPlan> listInsPlans=InsPlans.RefreshForSubList(listInsSubs);
			for(int i=0;i<listInsSubs.Count;i++) {
				InsPlan insPlan=InsPlans.GetPlan(listInsSubs[i].PlanNum,listInsPlans);
				if(insPlan==null || insPlan.IsHidden) {
					continue;
				}
				EFormInsPickChoice choice=new EFormInsPickChoice();
				choice.Value=ToValueInsSub(listInsSubs[i].InsSubNum);
				choice.Display=Carriers.GetName(insPlan.CarrierNum)+" - "+Lans.g("EFormInsPick","subscriber")+": "+GetNameFirstLast(listInsSubs[i].Subscriber)
					+" - "+Lans.g("EFormInsPick","ID")+": "+listInsSubs[i].SubscriberID;
				listChoices.Add(choice);
			}
			List<InsPending> listInsPendings=InsPendings.GetForPats(family.ListPats.Select(x => x.PatNum).ToList());
			List<InsPending> listInsPendingsShown=new List<InsPending>();
			for(int i=0;i<listInsPendings.Count;i++) {
				if(listInsPendingsShown.Exists(x => InsPendings.IsSameInsurance(x,listInsPendings[i]))) {
					continue;
				}
				listInsPendingsShown.Add(listInsPendings[i]);
				EFormInsPickChoice choice=new EFormInsPickChoice();
				choice.Value=ToValueInsPending(listInsPendings[i].InsPendingNum);
				choice.Display=listInsPendings[i].CarrierName+" - "+Lans.g("EFormInsPick","subscriber")+": "+GetNameFirstLast(listInsPendings[i].PatNumSubscriber)
					+" - "+Lans.g("EFormInsPick","ID")+": "+listInsPendings[i].SubscriberID+" "+Lans.g("EFormInsPick","(pending)");
				choice.IsPending=true;
				listChoices.Add(choice);
			}
			return listChoices;
		}

		///<summary>First and last name only.</summary>
		public static string GetNameFirstLast(long patNum) {
			Patient patient=Patients.GetLim(patNum);
			if(patient==null) {
				return "";
			}
			return patient.FName+" "+patient.LName;
		}

		///<summary>Returns an error message for the Patient List Add window, or empty string. First name, last name and a real birthdate are required.</summary>
		public static string ValidateNewFamilyMember(string fName,string lName,DateTime birthdate) {
			if(string.IsNullOrWhiteSpace(fName) || string.IsNullOrWhiteSpace(lName)) {
				return Lans.g("EFormInsPick","First and last name are required.");
			}
			if(birthdate.Year<1880 || birthdate.Date>DateTime.Today) {
				return Lans.g("EFormInsPick","A valid birthdate is required.");
			}
			return "";
		}

		///<summary>The Add button of a Patient List field: creates a person in the form patient's family, with the guarantor's address and home phone,
		///status Non-patient (our design: a subscriber is often not a patient), and the guarantor's provider and clinic. Logs PatientCreate. The caller
		///checks the PatientCreate permission and validates with ValidateNewFamilyMember.</summary>
		public static Patient AddFamilyMember(long patNumForm,string fName,string lName,DateTime birthdate,long eFormNum) {
			Patient patientForm=Patients.GetPat(patNumForm);
			if(patientForm==null) {
				throw new ApplicationException(Lans.g("EFormInsPick","The form has no patient."));
			}
			Patient patientGuar=Patients.GetPat(patientForm.Guarantor)??patientForm;
			string strForm=eFormNum==0 ? "a new eForm" : "eForm #"+eFormNum;
			Patient patient=Patients.CreateNewPatient(lName.Trim(),fName.Trim(),birthdate.Date,patientGuar.PriProv,patientGuar.ClinicNum,
				"Created from eForm Patient List field, "+strForm,patStatus:PatientStatus.NonPatient);
			Patient patientOld=patient.Copy();
			patient.Guarantor=patientGuar.PatNum;
			patient.Address=patientGuar.Address;
			patient.Address2=patientGuar.Address2;
			patient.City=patientGuar.City;
			patient.State=patientGuar.State;
			patient.Zip=patientGuar.Zip;
			patient.HmPhone=patientGuar.HmPhone;
			Patients.Update(patient,patientOld);
			return patient;
		}

		///<summary>Subscriber IDs are stored without dashes or spaces (inspending.SubscriberID: "No dashes").</summary>
		public static string NormalizeSubscriberID(string subscriberID) {
			return (subscriberID??"").Replace("-","").Replace(" ","").Trim();
		}
	}

	///<summary>One row of the Existing insurance picker of an Ins Pick field.</summary>
	public class EFormInsPickChoice {
		///<summary>"InsSubNum:####" or "InsPendingNum:####".</summary>
		public string Value="";
		public string Display="";
		public bool IsPending;
	}
}
