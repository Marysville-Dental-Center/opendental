// The preference that allows or prevents backdated repeating charges (Open Dental 26.1, item v26_1-account-repeatcharge-backdate-pref, package
// M2-RECUR in docs/specs/port-backlog.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;

namespace OpenPmsDentalBusiness {
	///<summary>Running repeating charges posts the billing dates it missed, from up to one month and 20 days back, dated on those past dates. The
	///preference RepeatingChargesAllowBackdating (on by default, which keeps that behavior) lets the office turn this off. Off, the run skips every
	///billing date before the day it runs: a missed date is never posted later, and the next billing date posts normally (this fork's reading of the
	///26.1 release note, which only says that the preference exists). Manual runs and any automatic run alike, because both go through
	///RepeatCharges.GetBillingDatesHelper. Duplicate detection is unchanged.</summary>
	public class RepeatChargeBackdating {
		///<summary>True when backdating is allowed: the preference is on, or its row is missing (the default keeps the old behavior).</summary>
		public static bool IsAllowed() {
			Meth.NoCheckMiddleTierRole();
			return PrefC.GetBoolSilent(PrefName.RepeatingChargesAllowBackdating,true);
		}

		///<summary>Called by RepeatCharges.GetBillingDatesHelper. When backdating is off, removes every billing date before the day of dateRun.</summary>
		public static void RemoveBackdatedDates(List<DateTime> listBillingDates,DateTime dateRun) {
			Meth.NoCheckMiddleTierRole();
			if(IsAllowed()) {
				return;
			}
			RemoveDatesBefore(listBillingDates,dateRun);
		}

		///<summary>Removes every date before the day of dateRun. The time of day of either is ignored.</summary>
		public static void RemoveDatesBefore(List<DateTime> listBillingDates,DateTime dateRun) {
			Meth.NoCheckMiddleTierRole();
			listBillingDates?.RemoveAll(x => x.Date<dateRun.Date);
		}

		///<summary>True when backdating is off and the start date is before today, so the billing dates between the start date and today will not be
		///posted. The Edit Repeating Charge window shows a note then.</summary>
		public static bool IsStartDateSkipped(DateTime dateStart,DateTime dateToday) {
			Meth.NoCheckMiddleTierRole();
			return dateStart.Year>1880 && dateStart.Date<dateToday.Date && !IsAllowed();
		}
	}
}
