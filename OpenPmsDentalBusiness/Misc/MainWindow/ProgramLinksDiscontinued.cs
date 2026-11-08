// B-SHELL (v25_2-bridges-dxc-credit-score-removed): program links whose service was discontinued. P262-LINKS (Open Dental 26.2 item 46) adds
// Trojan Express Collect. (From 2026-11-07 to 2026-11-08 it also held the DentX, Dexis Integrator and Vipersoft links as removed; S0-R restored
// them on the in-house DDE client, docs/port-notes/S0-R.md.)
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>Program links whose service no longer exists. Their program rows stay (migrated data refers to them by ProgName), but they cannot be
	///launched or enabled, Program Links lists them with a label, and no toolbar shows their buttons (ToolButItems.GetForToolBar). The fork
	///migration steps DxcCreditScoreRemovedStep and TrojanExpressCollectRemovedStep disable the rows that exist.</summary>
	public static class ProgramLinksDiscontinued {
		///<summary>Added to the description of a discontinued program link where Program Links lists it. The row itself is not changed.</summary>
		public const string LABEL="(discontinued)";

		///<summary>DXC Patient Credit Score: removed in Open Dental 25.2 because the service was discontinued. The bridge posted the ClaimConnect
		///clearinghouse login and password to a DentalXChange registration page.
		///Trojan Express Collect: deprecated in Open Dental 26.2; its public page says the service is no longer offered. The bridge wrote collection
		///requests (with the guarantor's Social Security number) to a folder that Trojan's own communicator program sent on.</summary>
		public static readonly List<ProgramName> ListProgramNames=new List<ProgramName> {
			ProgramName.DXCPatientCreditScore,
			ProgramName.TrojanExpressCollect,
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
