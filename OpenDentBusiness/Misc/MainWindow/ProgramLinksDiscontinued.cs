// B-SHELL (v25_2-bridges-dxc-credit-score-removed): program links whose service was discontinued.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDentBusiness {
	///<summary>Program links whose service no longer exists. Their program rows stay (migrated data refers to them by ProgName), but they are kept
	///disabled, have no toolbar buttons and cannot be launched or enabled. The fork migration step DxcCreditScoreRemovedStep disables the rows that
	///exist.</summary>
	public static class ProgramLinksDiscontinued {
		///<summary>Added to the description of a discontinued program link where Program Links lists it. The row itself is not changed.</summary>
		public const string LABEL="(discontinued)";

		///<summary>DXC Patient Credit Score: removed in Open Dental 25.2 because the service was discontinued. The bridge posted the ClaimConnect
		///clearinghouse login and password to a DentalXChange registration page.</summary>
		public static readonly List<ProgramName> ListProgramNames=new List<ProgramName> {
			ProgramName.DXCPatientCreditScore,
		};

		public static bool IsDiscontinued(string progName) {
			if(string.IsNullOrEmpty(progName)) {
				return false;
			}
			return ListProgramNames.Any(x => x.ToString()==progName);
		}

		public static bool IsDiscontinued(Program program) {
			return program!=null && IsDiscontinued(program.ProgName);
		}

		///<summary>The description with the discontinued label added once.</summary>
		public static string GetLabeledDesc(string progDesc) {
			string desc=(progDesc??"").Trim();
			if(desc.EndsWith(LABEL,StringComparison.OrdinalIgnoreCase)) {
				return desc;
			}
			if(desc=="") {
				return LABEL;
			}
			return desc+" "+LABEL;
		}
	}
}
