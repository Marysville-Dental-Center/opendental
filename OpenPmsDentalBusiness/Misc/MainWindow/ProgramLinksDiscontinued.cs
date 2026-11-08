// B-SHELL (v25_2-bridges-dxc-credit-score-removed): program links whose service was discontinued. P262-LINKS (Open Dental 26.2 item 46) adds
// Trojan Express Collect. Since 2026-11-07 also the program links removed from this program (owner question REL-FILES 2: DentX, Dexis Integrator
// and Vipersoft, which needed NDde.dll).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>Program links whose service no longer exists (ListProgramNames), and program links this program no longer has
	///(ListProgramNamesRemoved). Their program rows stay (migrated data refers to them by ProgName), but they cannot be launched or enabled, Program
	///Links lists them with a label, and no toolbar shows their buttons (ToolButItems.GetForToolBar). The fork migration steps DxcCreditScoreRemovedStep
	///and TrojanExpressCollectRemovedStep disable the rows of the discontinued links that exist; a removed link's row is left as it is, and a click
	///on it says it was removed.</summary>
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

		///<summary>Added to the description of a program link removed from this program where Program Links lists it. The row itself is not changed.
		///</summary>
		public const string LABEL_REMOVED="(removed)";

		///<summary>Program links removed on 2026-11-07 (owner question REL-FILES 2): DentX (ProImage), Dexis Integrator and Vipersoft sent the patient
		///over DDE through NDde.dll, whose licence does not fit the GPL, and the office uses none of them. Their bridge code and NDde.dll are gone; the
		///ProgramName values stay because program rows name them.</summary>
		public static readonly List<ProgramName> ListProgramNamesRemoved=new List<ProgramName> {
			ProgramName.DentX,
			ProgramName.DexisIntegrator,
			ProgramName.Vipersoft,
		};

		///<summary>True for a program link whose service was discontinued, and for one removed from this program (IsRemoved): neither can be launched,
		///enabled or given toolbar buttons.</summary>
		public static bool IsDiscontinued(string progName) {
			if(string.IsNullOrEmpty(progName)) {
				return false;
			}
			return ListProgramNames.Any(x => x.ToString()==progName) || IsRemoved(progName);
		}

		public static bool IsDiscontinued(Program program) {
			return program!=null && IsDiscontinued(program.ProgName);
		}

		///<summary>True for a program link removed from this program (ListProgramNamesRemoved).</summary>
		public static bool IsRemoved(string progName) {
			if(string.IsNullOrEmpty(progName)) {
				return false;
			}
			return ListProgramNamesRemoved.Any(x => x.ToString()==progName);
		}

		public static bool IsRemoved(Program program) {
			return program!=null && IsRemoved(program.ProgName);
		}

		///<summary>The label Program Links and the edit window add for the program link: LABEL_REMOVED for a removed one, otherwise LABEL.</summary>
		public static string GetLabel(Program program) {
			return IsRemoved(program) ? LABEL_REMOVED : LABEL;
		}

		///<summary>What a click on the program link's button says instead of launching it.</summary>
		public static string GetLaunchMessage(Program program) {
			if(IsRemoved(program)) {
				return Lans.g("Program","This program link was removed from this version of Open PMS - Dental and can no longer be used.");
			}
			return Lans.g("Program","This program link was discontinued and can no longer be used.");
		}

		///<summary>The description with the discontinued label added once.</summary>
		public static string GetLabeledDesc(string progDesc) {
			return GetLabeledDesc(progDesc,LABEL);
		}

		///<summary>The description with label added once.</summary>
		public static string GetLabeledDesc(string progDesc,string label) {
			string desc=(progDesc??"").Trim();
			if(desc.EndsWith(label,StringComparison.OrdinalIgnoreCase)) {
				return desc;
			}
			if(desc=="") {
				return label;
			}
			return desc+" "+label;
		}
	}
}
