// Billing Defaults per clinic (Open Dental 26.2 release notes: "Reconfigured Billing Defaults and added ability to set some settings per
// clinic"; package P262-BILLING, docs/port-notes/P262-BILLING.md). Which settings are per clinic is this program's choice (INFERRED there).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The Billing Defaults settings a clinic can have of its own: the statement General Message, the e-mail subject, body, body type and
	///autograph box of statements sent by e-mail, and the invoice note. Each is kept in its usual preference row (the practice default) and, for a
	///clinic with its own settings, in a clinicpref row under the same preference name. A clinic either has its own settings (all six rows, once it
	///is saved from Billing Defaults) or uses the practice defaults (none of its rows). The General Message row is the one Billing Options already
	///saves with a clinic's filter defaults, so both windows show and change the same value. The rows are read only while clinics are turned on;
	///with clinics off every reader gets the practice default, as before 26.2.</summary>
	public class BillingDefaultsByClinic {
		///<summary>The preferences a clinic can have its own value of, in the order Billing Defaults shows them.</summary>
		public static readonly List<PrefName> ListPrefNames=new List<PrefName>() {
			PrefName.BillingDefaultsNote,
			PrefName.BillingEmailSubject,
			PrefName.BillingEmailBodyText,
			PrefName.BillingEmailType,
			PrefName.BillingEmailIncludeAutograph,
			PrefName.BillingDefaultsInvoiceNote,
		};

		///<summary>True for the preferences in ListPrefNames.</summary>
		public static bool IsPerClinic(PrefName prefName) {
			Meth.NoCheckMiddleTierRole();
			return ListPrefNames.Contains(prefName);
		}

		///<summary>The clinic whose settings apply: the clinic itself while clinics are on and it is a real clinic, otherwise 0 (the practice
		///defaults).</summary>
		public static long GetSettingsClinicNum(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			if(clinicNum<=0 || !PrefC.HasClinicsEnabled) {
				return 0;
			}
			return clinicNum;
		}

		///<summary>The value of a per-clinic Billing Defaults preference for the clinic: its own row when clinics are on and it has one, otherwise the
		///practice default. Throws for a preference that is not per clinic, so a caller cannot read a clinic row of another setting by mistake.</summary>
		public static string GetString(PrefName prefName,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			if(!IsPerClinic(prefName)) {
				throw new ArgumentException("Not a per-clinic Billing Defaults preference: "+prefName);
			}
			long clinicNumSettings=GetSettingsClinicNum(clinicNum);
			if(clinicNumSettings>0) {
				ClinicPref clinicPref=ClinicPrefs.GetWhere(x => x.ClinicNum==clinicNumSettings && x.PrefName==prefName).FirstOrDefault();
				if(clinicPref!=null) {
					return clinicPref.ValueString??"";
				}
			}
			if(!PrefC.ContainsKey(prefName.ToString())) {
				return "";
			}
			return PrefC.GetString(prefName);
		}

		///<summary>GetString read as a boolean (the e-mail autograph box).</summary>
		public static bool GetBool(PrefName prefName,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			return PIn.Bool(GetString(prefName,clinicNum));
		}

		///<summary>The type of the Billing Defaults e-mail body for the clinic (BillingEmailType). Regular when the stored value is missing or not an
		///EmailType, as BillingEmailHtml.GetBillingDefaultsEmailType.</summary>
		public static EmailType GetEmailType(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			string value=GetString(PrefName.BillingEmailType,clinicNum);
			if(string.IsNullOrWhiteSpace(value)) {
				return EmailType.Regular;
			}
			return BillingEmailHtml.ToEmailType(PIn.Int(value,hasExceptions:false));
		}

		///<summary>True when the clinic has a row of its own for any of the per-clinic preferences. Reads the rows whether clinics are on or not, so
		///Billing Defaults can show what a clinic has saved.</summary>
		public static bool HasOwnSettings(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			if(clinicNum<=0) {
				return false;
			}
			return ClinicPrefs.GetWhere(x => x.ClinicNum==clinicNum && ListPrefNames.Contains(x.PrefName)).Count>0;
		}

		///<summary>The settings to show in Billing Defaults. ClinicNum 0 is the practice defaults. A clinic without rows of its own gets the practice
		///values with UsePracticeDefaults set; a clinic with some rows gets them, and the practice value for any row it lacks (for example a clinic
		///whose only row is the General Message that Billing Options saved).</summary>
		public static BillingDefaultsSettings GetSettings(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			BillingDefaultsSettings settings=new BillingDefaultsSettings();
			settings.ClinicNum=Math.Max(0,clinicNum);
			settings.UsePracticeDefaults=(settings.ClinicNum>0 && !HasOwnSettings(settings.ClinicNum));
			settings.GeneralMessage=GetRowOrDefault(PrefName.BillingDefaultsNote,settings.ClinicNum);
			settings.EmailSubject=GetRowOrDefault(PrefName.BillingEmailSubject,settings.ClinicNum);
			settings.EmailBody=GetRowOrDefault(PrefName.BillingEmailBodyText,settings.ClinicNum);
			string emailType=GetRowOrDefault(PrefName.BillingEmailType,settings.ClinicNum);
			settings.EmailType=string.IsNullOrWhiteSpace(emailType) ? EmailType.Regular : BillingEmailHtml.ToEmailType(PIn.Int(emailType,hasExceptions:false));
			settings.IncludeAutograph=PIn.Bool(GetRowOrDefault(PrefName.BillingEmailIncludeAutograph,settings.ClinicNum));
			settings.InvoiceNote=GetRowOrDefault(PrefName.BillingDefaultsInvoiceNote,settings.ClinicNum);
			return settings;
		}

		///<summary>The clinic's row when it has one (whether clinics are on or not), otherwise the practice default.</summary>
		private static string GetRowOrDefault(PrefName prefName,long clinicNum) {
			if(clinicNum>0) {
				ClinicPref clinicPref=ClinicPrefs.GetWhere(x => x.ClinicNum==clinicNum && x.PrefName==prefName).FirstOrDefault();
				if(clinicPref!=null) {
					return clinicPref.ValueString??"";
				}
			}
			if(!PrefC.ContainsKey(prefName.ToString())) {
				return "";
			}
			return PrefC.GetString(prefName);
		}

		///<summary>Saves the settings and returns true when a row changed. ClinicNum 0 updates the practice defaults (the preference rows). A clinic
		///that uses the practice defaults loses its rows of the six preferences (including the General Message row Billing Options keeps with its
		///filter defaults: the clinic then gets the practice message there too); a clinic with its own settings gets all six rows. Refreshes the
		///clinicpref cache when a clinic row changed; callers also signal other workstations (InvalidType.Prefs or ClinicPrefs).</summary>
		public static bool SaveSettings(BillingDefaultsSettings settings) {
			Meth.NoCheckMiddleTierRole();
			if(settings==null) {
				throw new ArgumentNullException(nameof(settings));
			}
			List<KeyValuePair<PrefName,string>> listValues=GetValues(settings);
			if(settings.ClinicNum<=0) {
				bool isChanged=false;
				foreach(KeyValuePair<PrefName,string> pair in listValues) {
					isChanged|=Prefs.UpdateString(pair.Key,pair.Value);
				}
				return isChanged;
			}
			bool isClinicChanged=false;
			if(settings.UsePracticeDefaults) {
				isClinicChanged=ClinicPrefs.DeletePrefs(settings.ClinicNum,ListPrefNames)>0;
			}
			else {
				foreach(KeyValuePair<PrefName,string> pair in listValues) {
					isClinicChanged|=ClinicPrefs.Upsert(pair.Key,settings.ClinicNum,pair.Value);
				}
			}
			if(isClinicChanged) {
				ClinicPrefs.RefreshCache();
			}
			return isClinicChanged;
		}

		///<summary>The stored text of each per-clinic preference for these settings, in ListPrefNames order, written the way the preference rows hold
		///them (booleans "0"/"1", the e-mail type as its number).</summary>
		public static List<KeyValuePair<PrefName,string>> GetValues(BillingDefaultsSettings settings) {
			Meth.NoCheckMiddleTierRole();
			List<KeyValuePair<PrefName,string>> listValues=new List<KeyValuePair<PrefName,string>>();
			listValues.Add(new KeyValuePair<PrefName,string>(PrefName.BillingDefaultsNote,settings.GeneralMessage??""));
			listValues.Add(new KeyValuePair<PrefName,string>(PrefName.BillingEmailSubject,settings.EmailSubject??""));
			listValues.Add(new KeyValuePair<PrefName,string>(PrefName.BillingEmailBodyText,settings.EmailBody??""));
			listValues.Add(new KeyValuePair<PrefName,string>(PrefName.BillingEmailType,POut.Int((int)settings.EmailType)));
			listValues.Add(new KeyValuePair<PrefName,string>(PrefName.BillingEmailIncludeAutograph,POut.Bool(settings.IncludeAutograph)));
			listValues.Add(new KeyValuePair<PrefName,string>(PrefName.BillingDefaultsInvoiceNote,settings.InvoiceNote??""));
			return listValues;
		}
	}

	///<summary>The per-clinic Billing Defaults of one clinic, or of the practice (ClinicNum 0), as the Billing Defaults window edits them.</summary>
	public class BillingDefaultsSettings {
		///<summary>0 for the practice defaults.</summary>
		public long ClinicNum;
		///<summary>Only for a clinic: true when it has no settings of its own and uses the practice defaults.</summary>
		public bool UsePracticeDefaults;
		///<summary>PrefName.BillingDefaultsNote, the General Message on statements.</summary>
		public string GeneralMessage="";
		///<summary>PrefName.BillingEmailSubject.</summary>
		public string EmailSubject="";
		///<summary>PrefName.BillingEmailBodyText.</summary>
		public string EmailBody="";
		///<summary>PrefName.BillingEmailType, the type of EmailBody (Open Dental 26.1).</summary>
		public EmailType EmailType=EmailType.Regular;
		///<summary>PrefName.BillingEmailIncludeAutograph.</summary>
		public bool IncludeAutograph;
		///<summary>PrefName.BillingDefaultsInvoiceNote.</summary>
		public string InvoiceNote="";

		public BillingDefaultsSettings Copy() {
			return (BillingDefaultsSettings)MemberwiseClone();
		}

		///<summary>True when every field is the same.</summary>
		public bool IsSameAs(BillingDefaultsSettings other) {
			if(other==null) {
				return false;
			}
			return ClinicNum==other.ClinicNum
				&& UsePracticeDefaults==other.UsePracticeDefaults
				&& (GeneralMessage??"")==(other.GeneralMessage??"")
				&& (EmailSubject??"")==(other.EmailSubject??"")
				&& (EmailBody??"")==(other.EmailBody??"")
				&& EmailType==other.EmailType
				&& IncludeAutograph==other.IncludeAutograph
				&& (InvoiceNote??"")==(other.InvoiceNote??"");
		}
	}
}
