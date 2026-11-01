using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>The Provider and Clinic filters of the More Fees window opened from Edit Procedure Code (Open Dental 25.1). A filter value is ALL (-1),
	///0 for the default fees (no provider, or the default/HQ clinic), or one ProvNum or ClinicNum.</summary>
	public class MoreFeesFilters {
		public const long ALL=-1;

		///<summary>The fees that pass both filters, in the order passed in.</summary>
		public static List<Fee> Filter(List<Fee> listFees,long provNumFilter,long clinicNumFilter) {
			Meth.NoCheckMiddleTierRole();
			if(listFees==null) {
				return new List<Fee>();
			}
			return listFees.FindAll(x => IsMatch(x.ProvNum,provNumFilter) && IsMatch(x.ClinicNum,clinicNumFilter));
		}

		///<summary>Whether the window shows a blank placeholder row for a fee schedule that has no default fee (no provider, default clinic). Only
		///when such a fee would pass the filters, so a provider or clinic filter never shows rows that are not that provider's or clinic's.</summary>
		public static bool IsDefaultFeeShown(long provNumFilter,long clinicNumFilter) {
			Meth.NoCheckMiddleTierRole();
			return IsMatch(0,provNumFilter) && IsMatch(0,clinicNumFilter);
		}

		///<summary>The providers the Provider filter lists: every provider that is not hidden, plus hidden providers that have one of the fees, in the
		///order of listProvidersAll.</summary>
		public static List<Provider> GetProvidersForFilter(List<Provider> listProvidersAll,List<Fee> listFees) {
			Meth.NoCheckMiddleTierRole();
			HashSet<long> hashSetProvNumsWithFees=new HashSet<long>((listFees??new List<Fee>()).Select(x => x.ProvNum));
			return listProvidersAll.FindAll(x => !x.IsHidden || hashSetProvNumsWithFees.Contains(x.ProvNum));
		}

		private static bool IsMatch(long value,long filter) {
			return filter==ALL || value==filter;
		}
	}
}
