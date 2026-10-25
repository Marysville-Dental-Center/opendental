// Wiki Search: limit the number of results (v25_2-wiki-search-limit, package B-WIKI). The per-user setting behind the "Limit results to" box.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenDentBusiness {
	///<summary>Whether a user limits Wiki Search results, and to how many titles. Remembered per user in userodpref, in a second row of the wiki
	///search type: FkeyType WikiSearchIncludeContent (25) with Fkey 1. Fkey 0 stays the Open Dental "include content" row. No new enum value is
	///needed, and an adopted Open Dental database reads as the default (off, 100). ValueString is "1,N" (on) or "0,N" (off).</summary>
	[Serializable]
	public class WikiSearchLimit {
		///<summary>The Fkey of the limit row. The include-content row keeps Fkey 0.</summary>
		public const long FKEY_LIMIT_ROW=1;
		public const int DEFAULT_LIMIT=100;
		public const int MIN_LIMIT=1;
		public const int MAX_LIMIT=5000;
		///<summary>Off by default, so the search behaves as in 24.3 until the user checks the box.</summary>
		public bool IsEnabled=false;
		public int Limit=DEFAULT_LIMIT;

		public WikiSearchLimit() {
		}

		public WikiSearchLimit(bool isEnabled,int limit) {
			IsEnabled=isEnabled;
			Limit=Clamp(limit);
		}

		///<summary>The limit to pass to WikiPages.GetForSearch: 0 (no limit) when off.</summary>
		public int GetLimitForSearch() {
			if(!IsEnabled) {
				return 0;
			}
			return Clamp(Limit);
		}

		///<summary>Keeps a limit inside 1 to 5000.</summary>
		public static int Clamp(int limit) {
			if(limit<MIN_LIMIT) {
				return MIN_LIMIT;
			}
			if(limit>MAX_LIMIT) {
				return MAX_LIMIT;
			}
			return limit;
		}

		///<summary>Reads a stored value ("1,250"). Blank or malformed values read as the default; out-of-range numbers are clamped.</summary>
		public static WikiSearchLimit Parse(string valueString) {
			WikiSearchLimit wikiSearchLimit=new WikiSearchLimit();
			if(string.IsNullOrWhiteSpace(valueString)) {
				return wikiSearchLimit;
			}
			string[] arrayParts=valueString.Split(',');
			if(arrayParts.Length!=2) {
				return wikiSearchLimit;
			}
			int limit;
			if(!int.TryParse(arrayParts[1].Trim(),out limit)) {
				return wikiSearchLimit;
			}
			string flag=arrayParts[0].Trim();
			if(flag!="0" && flag!="1") {
				return wikiSearchLimit;
			}
			wikiSearchLimit.IsEnabled=(flag=="1");
			wikiSearchLimit.Limit=Clamp(limit);
			return wikiSearchLimit;
		}

		///<summary>The stored form, for example "1,100".</summary>
		public string ToValueString() {
			return (IsEnabled ? "1" : "0")+","+Clamp(Limit).ToString();
		}

		///<summary>True when both are on or off with the same number.</summary>
		public bool IsSameAs(WikiSearchLimit wikiSearchLimit) {
			return wikiSearchLimit!=null && wikiSearchLimit.IsEnabled==IsEnabled && Clamp(wikiSearchLimit.Limit)==Clamp(Limit);
		}

		///<summary>The user's setting from the userodpref cache, or the default when the user has none.</summary>
		public static WikiSearchLimit GetForUser(long userNum) {
			Meth.NoCheckMiddleTierRole();
			UserOdPref userOdPref=UserOdPrefs.GetByUserFkeyAndFkeyType(userNum,FKEY_LIMIT_ROW,UserOdFkeyType.WikiSearchIncludeContent).FirstOrDefault();
			if(userOdPref==null) {
				return new WikiSearchLimit();
			}
			return Parse(userOdPref.ValueString);
		}

		///<summary>Stores the user's setting. No row is written while the setting equals the default and the user has no row yet. Returns true if
		///the database changed; the caller then signals InvalidType.UserOdPrefs.</summary>
		public static bool SaveForUser(long userNum,WikiSearchLimit wikiSearchLimit) {
			Meth.NoCheckMiddleTierRole();
			UserOdPref userOdPref=UserOdPrefs.GetByUserFkeyAndFkeyType(userNum,FKEY_LIMIT_ROW,UserOdFkeyType.WikiSearchIncludeContent).FirstOrDefault();
			if(userOdPref==null) {
				if(wikiSearchLimit.IsSameAs(new WikiSearchLimit())) {
					return false;
				}
				UserOdPrefs.Insert(new UserOdPref {
					UserNum=userNum,
					Fkey=FKEY_LIMIT_ROW,
					FkeyType=UserOdFkeyType.WikiSearchIncludeContent,
					ValueString=wikiSearchLimit.ToValueString(),
				});
				return true;
			}
			UserOdPref userOdPrefOld=userOdPref.Clone();
			userOdPref.ValueString=wikiSearchLimit.ToValueString();
			return UserOdPrefs.Update(userOdPref,userOdPrefOld);
		}
	}
}
