// Main window and cross-module audit items of Open Dental 25.4 and 26.1 (package M2-SHELL in docs/specs/port-backlog.md): the Patient Selected
// audit entry, the Billing/Finance Charges audit text and the patient gender text shown in the Chart and Family modules.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace OpenDentBusiness {
	///<summary>Audit Trail entry when a patient is selected (EnumPermType.PatientSelected, 275, Open Dental 25.4). Audit only: it never blocks a
	///selection. Off unless an administrator turns it on in Global Security Settings (the fork's own preference row PREF_NAME), because it adds one
	///entry for every patient change. The main window calls LogIfEnabled whenever the loaded patient changes (FormOpenDental.PatNumCur), which covers
	///the patient select window, the family menu, search results and every module that changes the patient. Selecting the patient who is already
	///loaded is not a new selection and is not logged.</summary>
	public static class PatientSelectedAudit {
		///<summary>The fork's own preference row: boolean, off by default, global. Open Dental's public pages do not say how the 25.4 entry is switched
		///on, so this setting is not in PrefName (which holds only names Open Dental publishes) and is read and written by name. The "Fork" prefix keeps
		///it from ever clashing with an Open Dental preference. No migration step adds the row, so an adopted Open Dental database is left as it was:
		///a missing row reads as off, and SetEnabled inserts it the first time an administrator turns the audit on.</summary>
		public const string PREF_NAME="ForkAuditTrailPatientSelected";
		///<summary>A user who selects the same patient again within this time gets no second entry (fork rule).</summary>
		public static readonly TimeSpan TimeSpanRepeat=TimeSpan.FromSeconds(60);
		public const string LOG_TEXT="Patient selected.";
		private static readonly object _lock=new object();
		///<summary>When each user last got an entry for each patient on this workstation.</summary>
		private static Dictionary<(long UserNum,long PatNum),DateTime> _dictLastLogged=new Dictionary<(long,long),DateTime>();

		///<summary>True when the office turned the audit on. A missing preference row (the default) reads as off, from the cache and without an
		///exception, because the main window asks on every patient change.</summary>
		public static bool IsEnabled() {
			Meth.NoCheckMiddleTierRole();
			return PIn.Bool(PrefC.GetRawOrDefault(PREF_NAME,"0"));//B-SERVER's shared helper for fork-owned preference rows.
		}

		///<summary>Writes the Patient Selected entry for the current user when the audit is on, unless the same user got one for the same patient within
		///TimeSpanRepeat. Does nothing for PatNum 0 (no patient) or when nobody is logged on. Returns true if an entry was written.</summary>
		public static bool LogIfEnabled(long patNum) {
			Meth.NoCheckMiddleTierRole();
			if(patNum<=0 || Security.CurUser==null || Security.CurUser.UserNum==0 || !IsEnabled()) {
				return false;
			}
			if(!ShouldLog(Security.CurUser.UserNum,patNum,DateTime.Now)) {
				return false;
			}
			SecurityLogs.MakeLogEntry(EnumPermType.PatientSelected,patNum,LOG_TEXT);
			return true;
		}

		///<summary>The repeat rule, without the database: true and remembers the time when this user got no entry for this patient within TimeSpanRepeat
		///before dateTimeNow.</summary>
		public static bool ShouldLog(long userNum,long patNum,DateTime dateTimeNow) {
			Meth.NoCheckMiddleTierRole();
			lock(_lock) {
				(long,long) key=(userNum,patNum);
				if(_dictLastLogged.TryGetValue(key,out DateTime dateTimeLast) && dateTimeNow>=dateTimeLast && dateTimeNow-dateTimeLast<TimeSpanRepeat) {
					return false;
				}
				//Forget entries that can no longer suppress anything, so the dictionary stays small on a workstation that runs all day.
				List<(long,long)> listKeysOld=_dictLastLogged.Where(x => dateTimeNow-x.Value>=TimeSpanRepeat).Select(x => x.Key).ToList();
				for(int i=0;i<listKeysOld.Count;i++) {
					_dictLastLogged.Remove(listKeysOld[i]);
				}
				_dictLastLogged[key]=dateTimeNow;
				return true;
			}
		}

		///<summary>Forgets every remembered selection. For tests.</summary>
		public static void ClearRepeatMemory() {
			Meth.NoCheckMiddleTierRole();
			lock(_lock) {
				_dictLastLogged.Clear();
			}
		}

		///<summary>Turns the audit on or off. Inserts the preference row the first time the audit is turned on (no migration step seeds it); turning it
		///off while there is no row writes nothing, because a missing row already reads as off. Updates the preference cache of the process that writes
		///(this workstation, or the Middle Tier server). Returns true if the stored value changed; the caller then signals InvalidType.Prefs so every
		///workstation reloads (DataValid.SetInvalid).</summary>
		public static bool SetEnabled(bool isEnabled) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),isEnabled);
			}
			if(!isEnabled && PrefC.GetRawOrDefault(PREF_NAME,null)==null) {
				return false;//Saving Global Security Settings without turning the audit on adds no row.
			}
			return PrefC.UpsertRaw(PREF_NAME,POut.Bool(isEnabled));//B-SERVER's shared helper for fork-owned preference rows.
		}
	}

	///<summary>Audit Trail text for Tools, Billing/Finance Charges (Open Dental 26.1): one entry when the window opens and one for every run, with its
	///settings and result. Both go under Setup, the permission that opens the window.</summary>
	public static class FinanceChargeAudit {
		public const string LOG_TEXT_OPENED="Billing/Finance Charges window opened";

		///<summary>"Finance charges run: date ..., APR ..., ... 12 charges created totaling $34.50". Amounts are formatted for the current culture.</summary>
		public static string GetRunLogText(FinanceChargeRun financeChargeRun) {
			Meth.NoCheckMiddleTierRole();
			List<string> listParts=new List<string>();
			listParts.Add("date "+financeChargeRun.DateCharge.ToShortDateString());
			if(financeChargeRun.IsBillingCharge) {
				listParts.Add("amount "+financeChargeRun.BillingChargeAmount.ToString("c"));
			}
			else {
				listParts.Add("APR "+financeChargeRun.Apr.ToString(CultureInfo.CurrentCulture)+"%");
				listParts.Add("at least "+financeChargeRun.AtLeast.ToString("c"));
				listParts.Add("only if over "+financeChargeRun.OnlyIfOver.ToString("c"));
				listParts.Add("compound "+(financeChargeRun.IsCompound ? "on" : "off"));
			}
			listParts.Add("balances over "+financeChargeRun.DaysOver+" days");
			listParts.Add("billing types "+(financeChargeRun.ListBillingTypes.Count==0 ? "none" : string.Join(", ",financeChargeRun.ListBillingTypes)));
			listParts.Add("provider "+(string.IsNullOrWhiteSpace(financeChargeRun.ProviderAbbr) ? "each patient's primary provider" : financeChargeRun.ProviderAbbr));
			string chargeType=financeChargeRun.IsBillingCharge ? "Billing" : "Finance";
			string text=chargeType+" charges run: "+string.Join(", ",listParts)+", "
				+financeChargeRun.CountCharges+" charge"+(financeChargeRun.CountCharges==1 ? "" : "s")+" created totaling "+financeChargeRun.TotalAmount.ToString("c");
			if(financeChargeRun.IsCancelled) {
				text+=" (cancelled before every account was processed)";
			}
			return text;
		}

		///<summary>Writes the "window opened" entry.</summary>
		public static void LogOpened() {
			Meth.NoCheckMiddleTierRole();
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,LOG_TEXT_OPENED);
		}

		///<summary>Writes the entry for one run.</summary>
		public static void LogRun(FinanceChargeRun financeChargeRun) {
			Meth.NoCheckMiddleTierRole();
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,GetRunLogText(financeChargeRun));
		}
	}

	///<summary>The settings and result of one Billing/Finance Charges run, for its Audit Trail entry.</summary>
	public class FinanceChargeRun {
		public bool IsBillingCharge;
		public DateTime DateCharge;
		public double Apr;
		public double AtLeast;
		public double OnlyIfOver;
		public bool IsCompound;
		public double BillingChargeAmount;
		///<summary>30, 60 or 90.</summary>
		public int DaysOver;
		public List<string> ListBillingTypes=new List<string>();
		///<summary>Blank when each patient's primary provider gets the charge.</summary>
		public string ProviderAbbr="";
		public int CountCharges;
		public double TotalAmount;
		///<summary>True when the user cancelled the run part way; the charges already created stay.</summary>
		public bool IsCancelled;
	}

	///<summary>How a patient's gender is shown in a patient information grid (Open Dental 26.1 Chart "Gender" display field): the same translated text as
	///the Edit Patient Information list, and an empty cell for Unknown.</summary>
	public static class PatientGenderDisplay {
		public static string GetText(PatientGender patientGender) {
			Meth.NoCheckMiddleTierRole();
			if(patientGender==PatientGender.Unknown || !Enum.IsDefined(typeof(PatientGender),patientGender)) {
				return "";
			}
			//Edit Patient Information fills its list with ListBox.AddEnums<PatientGender>, which translates each name in the "enumPatientGender" group.
			return Lans.g("enum"+nameof(PatientGender),patientGender.ToString());
		}
	}
}
