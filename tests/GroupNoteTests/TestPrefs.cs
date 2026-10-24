// Preferences for the tests that run without a database. The Procedure constructor reads one.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using OpenDentBusiness;

namespace GroupNoteTests {
	public static class TestPrefs {
		///<summary>Fills the preference cache with the few values the tests without a database need, unless a database test already filled it.
		///Database tests clear it again before they build their database (GroupNoteDbTests).</summary>
		public static void EnsureFilled() {
			if(!Prefs.DictIsNull()) {
				return;
			}
			Prefs.FillCacheFromTable(OpenDentBusiness.Crud.PrefCrud.ListToTable(new List<Pref> {
				new Pref { PrefNum=1,PrefName=PrefName.DxIcdVersion.ToString(),ValueString="9" },
			}));
		}
	}
}
