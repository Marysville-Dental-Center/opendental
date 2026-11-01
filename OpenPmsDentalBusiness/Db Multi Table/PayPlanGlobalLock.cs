// Open Dental 25.3 (backported to 25.2.44): a preference that lets payment plans ignore the global lock date and lock days. Backlog package B-PAY
// (docs/specs/port-backlog.md); behavior in docs/specs/features-25.3.md (v25_3-account-payplan-bypass-globallock).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Reflection;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The "payment plans are not affected by the global lock" setting. When it is on, Security.IsGlobalDateLock never blocks PayPlanEdit, so
	///editing, deleting, closing and signing payment plans dated inside the global lock work again. User-group lock dates (date and days limits on the
	///permission), the plan's own Locked box and Pay Plan Unlock still apply, and payments, adjustments and procedures stay locked.
	///
	///Open Dental's preference name is not public. The spec proposes SecurityLockExcludesPayPlans and the registry tests keep that guess out of the
	///PrefName enum until it is confirmed, so the value lives in a preference row that is not in PrefName, the pattern DxcPartnerIdentity uses. The
	///row is inserted the first time an office turns the setting on, so a database without it reads as off (the default) and needs no migration. When
	///the real name is confirmed, only PREF_NAME changes (and the registry adds it to PrefName).</summary>
	public class PayPlanGlobalLock {
		///<summary>Proposed name (docs/specs/features-25.3.md). Unconfirmed: see the class summary.</summary>
		public const string PREF_NAME="SecurityLockExcludesPayPlans";

		///<summary>True when the office turned the setting on. A missing row, or a cache that is not loaded yet, reads as off. Never throws.</summary>
		public static bool IsOn() {
			Meth.NoCheckMiddleTierRole();
			try {
				if(Prefs.DictIsNull() || !Prefs.GetContainsKey(PREF_NAME)) {
					return false;
				}
				return PIn.Bool(Prefs.GetOne(PREF_NAME).ValueString);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>True when the global lock must not block this permission: the setting is on and the permission is PayPlanEdit, the only payment
		///plan permission the global lock covers.</summary>
		public static bool IsExcludedFromGlobalLock(EnumPermType permType) {
			Meth.NoCheckMiddleTierRole();
			return permType==EnumPermType.PayPlanEdit && IsOn();
		}

		///<summary>Stores the setting, inserting the row if needed, and updates this workstation's cache. Returns true if the value changed. The caller
		///signals InvalidType.Prefs so other workstations reload.</summary>
		public static bool Save(bool isOn) {
			Meth.NoCheckMiddleTierRole();
			if(IsOn()==isOn) {
				return false;
			}
			string value=POut.Bool(isOn);
			Upsert(value);
			Prefs.UpdateValueForKey(new Pref { PrefName=PREF_NAME,ValueString=value });
			return true;
		}

		///<summary>Writes the preference row straight to the table, inserting it when it is missing.</summary>
		public static void Upsert(string value) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),value);
				return;
			}
			string count=Db.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+POut.String(PREF_NAME)+"'");
			if(PIn.Long(count)==0) {
				Db.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('"+POut.String(PREF_NAME)+"','"+POut.String(value)+"')");
				return;
			}
			Db.NonQ("UPDATE preference SET ValueString='"+POut.String(value)+"' WHERE PrefName='"+POut.String(PREF_NAME)+"'");
		}

		///<summary>The audit line for the Global Lock Date window, or "" when unchanged. Same "changed from 'X' to 'Y'" form as the other lock lines.</summary>
		public static string GetChangeLogLine(bool isOnOld,bool isOnNew) {
			if(isOnOld==isOnNew) {
				return "";
			}
			return "Payment plans are not affected by the global lock changed from '"+isOnOld+"' to '"+isOnNew+"'";
		}
	}
}
