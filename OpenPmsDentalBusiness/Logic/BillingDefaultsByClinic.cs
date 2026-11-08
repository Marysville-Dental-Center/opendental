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
	///clinic with its own value, in a clinicpref row under the same preference name. A clinic without any of the rows uses the practice defaults.
	///Each message a clinic has no row for follows the practice default on its own: a clinic that stops using the practice defaults in Billing
	///Defaults gets all six rows, but a clinic that already had some (for example only the General Message row that Billing Options' Save Defaults
	///wrote before 26.2) gets a row only for a message changed for it there. The General Message row is the one Billing Options already saves with
	///a clinic's filter defaults, so both windows show and change the same value. The rows are read only while clinics are turned on; with clinics
	///off every reader gets the practice default, as before 26.2.</summary>
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
			return GetPrefNamesWithRows(clinicNum).Count>0;
		}

		///<summary>The per-clinic preferences the clinic has a row of its own for, in ListPrefNames order. Reads the rows whether clinics are on or
		///not. Empty for the practice defaults (ClinicNum 0).</summary>
		public static List<PrefName> GetPrefNamesWithRows(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			if(clinicNum<=0) {
				return new List<PrefName>();
			}
			List<ClinicPref> listClinicPrefs=ClinicPrefs.GetWhere(x => x.ClinicNum==clinicNum && ListPrefNames.Contains(x.PrefName));
			return ListPrefNames.FindAll(x => listClinicPrefs.Any(y => y.PrefName==x));
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

		///<summary>Saves the settings and returns true when a row changed. settingsOld are the same clinic's settings as GetSettings read them before
		///they were edited. ClinicNum 0 updates the practice defaults (the preference rows). A clinic that uses the practice defaults loses its rows of
		///the six preferences (including the General Message row Billing Options keeps with its filter defaults: the clinic then gets the practice
		///message there too). A clinic that does not gets the rows GetPrefNamesToWrite names: all six when it stops using the practice defaults,
		///otherwise only the messages changed for it, so every message it has no row for keeps following the practice default. Refreshes the
		///clinicpref cache when a clinic row changed; callers also signal other workstations (InvalidType.Prefs or ClinicPrefs).</summary>
		public static bool SaveSettings(BillingDefaultsSettings settings,BillingDefaultsSettings settingsOld) {
			Meth.NoCheckMiddleTierRole();
			if(settings==null) {
				throw new ArgumentNullException(nameof(settings));
			}
			if(settingsOld==null) {
				throw new ArgumentNullException(nameof(settingsOld));
			}
			if(settingsOld.ClinicNum!=settings.ClinicNum) {
				throw new ArgumentException("The old settings are of clinic "+settingsOld.ClinicNum+", the settings of clinic "+settings.ClinicNum+".");
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
				List<PrefName> listPrefNamesToWrite=GetPrefNamesToWrite(settings,settingsOld);
				foreach(KeyValuePair<PrefName,string> pair in listValues) {
					if(!listPrefNamesToWrite.Contains(pair.Key)) {
						continue;//Unchanged: its row, if it has one, stays as it is; without one it keeps following the practice default.
					}
					isClinicChanged|=ClinicPrefs.Upsert(pair.Key,settings.ClinicNum,pair.Value);
				}
			}
			if(isClinicChanged) {
				ClinicPrefs.RefreshCache();
			}
			return isClinicChanged;
		}

		///<summary>The clinic rows SaveSettings writes for a clinic that does not use the practice defaults. All six when the clinic used them when
		///its settings were read (settingsOld.UsePracticeDefaults): it starts from a copy of the practice defaults (owner question 5 of the port
		///note). Otherwise only the messages changed for it (GetPrefNamesChanged). Unchanged rows it has are left as they are (writing them again
		///would change nothing, or put back an older value over one saved since the settings were read), and a message without a row keeps following
		///the practice default: a clinic whose only row is the General Message that Billing Options saved before 26.2 is not given frozen copies of
		///the other five when its General Message is edited.</summary>
		public static List<PrefName> GetPrefNamesToWrite(BillingDefaultsSettings settings,BillingDefaultsSettings settingsOld) {
			Meth.NoCheckMiddleTierRole();
			if(settingsOld.UsePracticeDefaults) {
				return new List<PrefName>(ListPrefNames);
			}
			return GetPrefNamesChanged(settings,settingsOld);
		}

		///<summary>The per-clinic preferences whose stored text differs between the two settings, with the preferences saved together with them, in
		///ListPrefNames order. The e-mail body and its type are saved together, so a clinic's body is never read with a type other than the one it
		///was saved with (a plain body of the clinic stays plain after the practice changes its type to HTML, and the other way round).</summary>
		public static List<PrefName> GetPrefNamesChanged(BillingDefaultsSettings settings,BillingDefaultsSettings settingsOld) {
			Meth.NoCheckMiddleTierRole();
			List<KeyValuePair<PrefName,string>> listValues=GetValues(settings);
			List<KeyValuePair<PrefName,string>> listValuesOld=GetValues(settingsOld);
			List<PrefName> listPrefNamesDiffer=new List<PrefName>();
			for(int i=0;i<listValues.Count;i++) {
				if(listValues[i].Value!=listValuesOld[i].Value) {
					listPrefNamesDiffer.Add(listValues[i].Key);
				}
			}
			return ListPrefNames.FindAll(x => GetPrefNamesSavedTogether(x).Any(y => listPrefNamesDiffer.Contains(y)));
		}

		///<summary>The preference and the ones saved together with it: the e-mail body and its type go together, every other one is on its own.</summary>
		private static List<PrefName> GetPrefNamesSavedTogether(PrefName prefName) {
			if(prefName==PrefName.BillingEmailBodyText || prefName==PrefName.BillingEmailType) {
				return new List<PrefName>() { PrefName.BillingEmailBodyText,PrefName.BillingEmailType };
			}
			return new List<PrefName>() { prefName };
		}

		///<summary>For a clinic that had settings of its own when they were read (settingsOld): each message it has no row for (not in
		///listPrefNamesWithRows) and that was not changed for it (GetPrefNamesChanged) follows the practice defaults, so it is set to the value in
		///settingsPractice, in both settings and settingsOld (following the practice is not a change to save). Billing Defaults calls it each time it
		///shows a clinic, with the practice defaults as edited in the window, so a clinic never shows a copy of a practice value from before that
		///edit. Does nothing for the practice defaults, for a clinic that used them when read, or for one set to use them in the window.</summary>
		public static void FollowPracticeDefaults(BillingDefaultsSettings settings,BillingDefaultsSettings settingsOld,
			BillingDefaultsSettings settingsPractice,List<PrefName> listPrefNamesWithRows)
		{
			Meth.NoCheckMiddleTierRole();
			if(settings.ClinicNum<=0 || settings.UsePracticeDefaults || settingsOld.UsePracticeDefaults) {
				return;
			}
			List<PrefName> listPrefNamesChanged=GetPrefNamesChanged(settings,settingsOld);
			foreach(PrefName prefName in ListPrefNames) {
				if(listPrefNamesWithRows.Contains(prefName) || listPrefNamesChanged.Contains(prefName)) {
					continue;
				}
				CopyValue(settingsPractice,settings,prefName);
				CopyValue(settingsPractice,settingsOld,prefName);
			}
		}

		///<summary>Copies one per-clinic setting from one settings object to another.</summary>
		private static void CopyValue(BillingDefaultsSettings settingsFrom,BillingDefaultsSettings settingsTo,PrefName prefName) {
			switch(prefName) {
				case PrefName.BillingDefaultsNote:
					settingsTo.GeneralMessage=settingsFrom.GeneralMessage;
					return;
				case PrefName.BillingEmailSubject:
					settingsTo.EmailSubject=settingsFrom.EmailSubject;
					return;
				case PrefName.BillingEmailBodyText:
					settingsTo.EmailBody=settingsFrom.EmailBody;
					return;
				case PrefName.BillingEmailType:
					settingsTo.EmailType=settingsFrom.EmailType;
					return;
				case PrefName.BillingEmailIncludeAutograph:
					settingsTo.IncludeAutograph=settingsFrom.IncludeAutograph;
					return;
				case PrefName.BillingDefaultsInvoiceNote:
					settingsTo.InvoiceNote=settingsFrom.InvoiceNote;
					return;
				default:
					throw new ArgumentException("Not a per-clinic Billing Defaults preference: "+prefName);
			}
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
