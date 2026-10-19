using OpenDentBusiness;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenDentBusiness {
	public class CodeMapping {
		///<summary>Returns the default arch for an arch procedure added with no teeth selected: an empty string for Canada, otherwise "U".
		///There is no built-in list of mandibular codes, so the user picks "L" in the procedure when it applies.</summary>
		public static string GetArchSurfaceFromProcCode(ProcedureCode procedureCode) {
			if(CultureInfo.CurrentCulture.Name.EndsWith("CA")) {
				return "";
			}
			return "U";
		}
	}
}
