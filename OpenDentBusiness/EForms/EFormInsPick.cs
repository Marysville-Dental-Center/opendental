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

		///<summary>Subscriber IDs are stored without dashes or spaces (inspending.SubscriberID: "No dashes").</summary>
		public static string NormalizeSubscriberID(string subscriberID) {
			return (subscriberID??"").Replace("-","").Replace(" ","").Trim();
		}
	}
}
