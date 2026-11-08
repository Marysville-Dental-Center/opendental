// Open Dental 26.2 (release notes, Miscellaneous): "New definition category for Referral Commlog Types". Package P262-MISC.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The types of referral commlogs (the Communication Item for Referral window, from a referral source's Edit Referral window). Open Dental
	///26.2 gave them their own definition category, DefCat.CommLogReferralTypes (57): Open Dental's 26.2 database documentation says commlog.CommType
	///is a definition of CommLogTypes when ReferralNum is 0 and of CommLogReferralTypes when ReferralNum is set ("Similar to Commlog Types, but are used
	///for Referral Commlogs"). Before 26.2 referral commlogs had CommType 0, and those still load with no type.
	///<para>INFERRED (no public source states them): a new referral commlog starts with the first visible type of the category in list order, or no
	///type when the category is empty (no definition ships with this program; the office adds its own in Setup, Definitions); the type can be set to
	///None; a hidden type stays on the commlogs that use it and is offered only on those; a CommType that is not a definition of the category (a
	///definition deleted outside this program) is kept as it is when the commlog is saved.</para></summary>
	public class CommlogReferralTypes {
		///<summary>The definitions of the category, hidden ones included, from the cache, in list order.</summary>
		public static List<Def> GetDefsAll() {
			Meth.NoCheckMiddleTierRole();
			return Defs.GetDefsForCategory(DefCat.CommLogReferralTypes,isShort:false).OrderBy(x => x.ItemOrder).ToList();
		}

		///<summary>The CommType a new referral commlog starts with: the first visible definition in list order, or 0 when there is none.</summary>
		public static long GetDefaultCommType(List<Def> listDefsCat) {
			Def def=(listDefsCat??new List<Def>()).Where(x => x.Category==DefCat.CommLogReferralTypes && !x.IsHidden).OrderBy(x => x.ItemOrder).FirstOrDefault();
			return def==null ? 0 : def.DefNum;
		}

		///<summary>Same as the other overload, from the cache.</summary>
		public static long GetDefaultCommType() {
			Meth.NoCheckMiddleTierRole();
			return GetDefaultCommType(GetDefsAll());
		}

		///<summary>The types offered for a referral commlog whose CommType is commTypeCur: the visible definitions in list order, plus the current one
		///when it is hidden (in its list position), so that saving never drops it.</summary>
		public static List<Def> GetPickList(long commTypeCur,List<Def> listDefsCat) {
			return (listDefsCat??new List<Def>())
				.Where(x => x.Category==DefCat.CommLogReferralTypes && (!x.IsHidden || (commTypeCur!=0 && x.DefNum==commTypeCur)))
				.OrderBy(x => x.ItemOrder)
				.ToList();
		}

		///<summary>The name shown for a referral commlog's type: the definition's name, with " (hidden)" for a hidden one; "" for 0 or a number that is
		///not a definition of the category.</summary>
		public static string GetTypeName(long commType,List<Def> listDefsCat) {
			if(commType==0) {
				return "";
			}
			Def def=(listDefsCat??new List<Def>()).FirstOrDefault(x => x.Category==DefCat.CommLogReferralTypes && x.DefNum==commType);
			if(def==null) {
				return "";
			}
			return def.ItemName+(def.IsHidden ? " (hidden)" : "");
		}

		///<summary>True when the number is 0 (no type) or a definition of the category. False for anything else, which the edit window keeps as it
		///is.</summary>
		public static bool IsKnownCommType(long commType,List<Def> listDefsCat) {
			if(commType==0) {
				return true;
			}
			return (listDefsCat??new List<Def>()).Exists(x => x.Category==DefCat.CommLogReferralTypes && x.DefNum==commType);
		}
	}
}
