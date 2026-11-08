// Billing Options' filter defaults per clinic: the clinicpref rows its Save Defaults writes for one clinic, and how they live beside the General
// Message row that Billing Defaults saves for a clinic (Open Dental 26.2 item 18; package P262-BILLING, docs/port-notes/P262-BILLING.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The filter defaults Billing Options saves for a clinic (Save Defaults with one clinic selected), kept as clinicpref rows under the
	///preference names, with the General Message (BillingDefaultsNote) among them. Billing Defaults saves the General Message row too, for a clinic
	///with messages of its own (BillingDefaultsByClinic), and removes it for a clinic that uses the practice defaults. So a clinic whose only row of
	///this list is the General Message has not saved filter defaults: it uses the practice filter defaults, and Billing Options must neither show
	///nor write filter rows for it. Billing Options writes the rows of the selected clinic only, never those of another clinic.</summary>
	public class BillingOptionsClinicDefaults {
		///<summary>The preferences Billing Options saves per clinic, in the order it always used. ClinicBillingOptions (automated billing) reads the
		///same rows, with the practice value for any row a clinic lacks.</summary>
		public static readonly List<PrefName> ListPrefNames=new List<PrefName>() {
			PrefName.BillingIncludeChanged,
			PrefName.BillingSelectBillingTypes,
			PrefName.BillingAgeOfAccount,
			PrefName.BillingDaysExcludeInsPending,
			PrefName.BillingExcludeBadAddresses,
			PrefName.BillingExcludeInactive,
			PrefName.BillingExcludeNegative,
			PrefName.BillingExcludeInsPending,
			PrefName.BillingExcludeIfUnsentProcs,
			PrefName.BillingExcludeLessThan,
			PrefName.BillingIgnoreInPerson,
			PrefName.BillingShowTransSinceBalZero,
			PrefName.BillingDefaultsNote,
			PrefName.BillingSelectInsFilingCodes
		};

		///<summary>Copies of every cached row of ListPrefNames, grouped by clinic: each clinic with at least one row, including a clinic whose only row
		///is the General Message.</summary>
		public static Dictionary<long,List<ClinicPref>> GetRowsByClinic() {
			Meth.NoCheckMiddleTierRole();
			return ClinicPrefs.GetWhere(x => ListPrefNames.Contains(x.PrefName))
				.GroupBy(x => x.ClinicNum)
				.ToDictionary(x => x.Key,x => x.Select(y => y.Clone()).ToList());
		}

		///<summary>True when the rows hold a filter default, that is a row of ListPrefNames other than the General Message: the clinic saved filter
		///defaults in Billing Options. False for a clinic whose only row is the General Message (saved by Billing Defaults).</summary>
		public static bool HasSavedFilterDefaults(List<ClinicPref> listClinicPrefs) {
			Meth.NoCheckMiddleTierRole();
			if(listClinicPrefs==null) {
				return false;
			}
			return listClinicPrefs.Any(x => x.PrefName!=PrefName.BillingDefaultsNote && ListPrefNames.Contains(x.PrefName));
		}

		///<summary>The filter defaults Billing Options shows, for the clinics that saved them (HasSavedFilterDefaults): copies of each clinic's rows,
		///completed with a row holding the practice value of every preference the clinic has none of (a preference added to the list after the
		///clinic saved, or the General Message that Billing Defaults removed when the clinic went back to the practice defaults). The added rows have
		///ClinicPrefNum 0 and are never written by themselves. A clinic whose only row is the General Message is left out: it uses the practice filter
		///defaults.</summary>
		public static Dictionary<long,List<ClinicPref>> GetFilledDefaults(Dictionary<long,List<ClinicPref>> dictionaryRowsByClinic) {
			Meth.NoCheckMiddleTierRole();
			Dictionary<long,List<ClinicPref>> dictionaryFilled=new Dictionary<long,List<ClinicPref>>();
			if(dictionaryRowsByClinic==null) {
				return dictionaryFilled;
			}
			foreach(KeyValuePair<long,List<ClinicPref>> pair in dictionaryRowsByClinic) {
				if(!HasSavedFilterDefaults(pair.Value)) {
					continue;
				}
				List<ClinicPref> listClinicPrefs=pair.Value.Where(x => ListPrefNames.Contains(x.PrefName)).Select(x => x.Clone()).ToList();
				List<PrefName> listPrefNamesExisting=listClinicPrefs.Select(x => x.PrefName).ToList();
				foreach(PrefName prefName in ListPrefNames.FindAll(x => !listPrefNamesExisting.Contains(x))) {
					ClinicPref clinicPref=GetPracticeRow(pair.Key,prefName);
					if(clinicPref!=null) {
						listClinicPrefs.Add(clinicPref);
					}
				}
				dictionaryFilled.Add(pair.Key,listClinicPrefs);
			}
			return dictionaryFilled;
		}

		///<summary>A new row (not written) holding the practice value of the preference, stored as Billing Options always stored it. Null for a
		///preference whose value type is not handled (none of ListPrefNames).</summary>
		private static ClinicPref GetPracticeRow(long clinicNum,PrefName prefName) {
			switch(EnumTools.GetAttributeOrDefault<PrefNameAttribute>(prefName).ValueType) {
				case PrefValueType.BOOL:
					return new ClinicPref(clinicNum,prefName,PrefC.GetBool(prefName));
				case PrefValueType.STRING:
					return new ClinicPref(clinicNum,prefName,PrefC.GetString(prefName));
				case PrefValueType.INT:
					return new ClinicPref(clinicNum,prefName,POut.Int(PrefC.GetInt(prefName)));
				default:
					return null;
			}
		}

		///<summary>The rows to write when Billing Options saves the filter defaults of one clinic: copies of the clinic's existing rows (so a row is
		///updated in place, never deleted or inserted twice) holding the new values, and a new row (ClinicPrefNum 0) for each value the clinic has
		///no row of. The General Message gets a new row only when its text differs from generalMessageWithoutRow, the message the clinic gets without
		///a row (the practice General Message): a clinic that uses the practice defaults in Billing Defaults keeps them unless a message of its own was
		///typed. A value of a preference outside ListPrefNames is ignored, and a row of another clinic in listClinicPrefsOld is left out.</summary>
		public static List<ClinicPref> GetRowsToSave(long clinicNum,List<ClinicPref> listClinicPrefsOld,List<KeyValuePair<PrefName,string>> listValues,
			string generalMessageWithoutRow)
		{
			Meth.NoCheckMiddleTierRole();
			List<ClinicPref> listClinicPrefsNew=(listClinicPrefsOld??new List<ClinicPref>())
				.Where(x => x.ClinicNum==clinicNum && ListPrefNames.Contains(x.PrefName))
				.Select(x => x.Clone())
				.ToList();
			foreach(KeyValuePair<PrefName,string> pair in listValues??new List<KeyValuePair<PrefName,string>>()) {
				if(!ListPrefNames.Contains(pair.Key)) {
					continue;
				}
				string value=pair.Value??"";
				ClinicPref clinicPref=listClinicPrefsNew.FirstOrDefault(x => x.PrefName==pair.Key);
				if(clinicPref!=null) {
					clinicPref.ValueString=value;
					continue;
				}
				if(pair.Key==PrefName.BillingDefaultsNote && value==(generalMessageWithoutRow??"")) {
					continue;//No row of its own for the text the clinic already gets.
				}
				listClinicPrefsNew.Add(new ClinicPref(clinicNum,pair.Key,value));
			}
			return listClinicPrefsNew;
		}

		///<summary>Saves the filter defaults of one clinic (Billing Options' Save Defaults with that clinic selected) and nothing of any other clinic.
		///Reads the clinic's current rows from the cache, writes GetRowsToSave and returns true when a row changed (the clinicpref cache is then
		///refreshed; the caller also signals other workstations with InvalidType.ClinicPrefs).</summary>
		public static bool SaveForClinic(long clinicNum,List<KeyValuePair<PrefName,string>> listValues) {
			Meth.NoCheckMiddleTierRole();
			if(clinicNum<=0) {
				throw new ArgumentException("Billing Options saves the practice filter defaults to the preference rows, not to a clinic: "+clinicNum);
			}
			List<ClinicPref> listClinicPrefsOld=ClinicPrefs.GetWhere(x => x.ClinicNum==clinicNum && ListPrefNames.Contains(x.PrefName));
			List<ClinicPref> listClinicPrefsNew=GetRowsToSave(clinicNum,listClinicPrefsOld,listValues,
				BillingDefaultsByClinic.GetString(PrefName.BillingDefaultsNote,0));
			bool isChanged=ClinicPrefs.Sync(listClinicPrefsNew,listClinicPrefsOld);
			if(isChanged) {
				ClinicPrefs.RefreshCache();
			}
			return isChanged;
		}
	}
}
