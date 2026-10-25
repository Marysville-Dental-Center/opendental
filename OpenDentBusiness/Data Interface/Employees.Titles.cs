// Employee job titles (Open Dental 26.1, feature v26_1-lists-employee-title, package M2-EMP in docs/specs/port-backlog.md): employee.Title points at
// a definition in the Employee Titles category (DefCat 56). The title is shown only; nothing else reads it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;

namespace OpenDentBusiness {
	public partial class Employees {
		///<summary>The titles the Title drop-down in Edit Employee offers, in their definition order: every visible title, plus the employee's
		///current title when it is hidden. Pass the whole category, hidden titles included. A title number that is not in the category is not
		///offered.</summary>
		public static List<Def> GetTitlesForPicker(List<Def> listDefsTitles,long titleDefNumCur) {
			Meth.NoCheckMiddleTierRole();
			return listDefsTitles.Where(x => x.Category==DefCat.EmployeeTitles && (!x.IsHidden || (titleDefNumCur!=0 && x.DefNum==titleDefNumCur)))
				.OrderBy(x => x.ItemOrder)
				.ToList();
		}

		///<summary>GetTitlesForPicker with the Employee Titles definitions from the cache.</summary>
		public static List<Def> GetTitlesForPicker(long titleDefNumCur) {
			Meth.NoCheckMiddleTierRole();
			return GetTitlesForPicker(Defs.GetDefsForCategory(DefCat.EmployeeTitles),titleDefNumCur);
		}

		///<summary>The employee's title as the Employees list shows it: the definition's name, with "(hidden)" after it when the title is hidden.
		///Blank when the employee has no title, or when the number is not a title (for example a definition that was removed).</summary>
		public static string GetTitleText(long titleDefNum,List<Def> listDefsTitles) {
			Meth.NoCheckMiddleTierRole();
			if(titleDefNum==0) {
				return "";
			}
			Def def=listDefsTitles.FirstOrDefault(x => x.DefNum==titleDefNum && x.Category==DefCat.EmployeeTitles);
			if(def==null) {
				return "";
			}
			if(def.IsHidden) {
				return def.ItemName+" "+Lans.g("Defs","(hidden)");
			}
			return def.ItemName;
		}

		///<summary>GetTitleText with the Employee Titles definitions from the cache.</summary>
		public static string GetTitleText(long titleDefNum) {
			Meth.NoCheckMiddleTierRole();
			return GetTitleText(titleDefNum,Defs.GetDefsForCategory(DefCat.EmployeeTitles));
		}
	}
}
