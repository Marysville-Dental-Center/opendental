// Open Dental 26.2 (P262-GLIDEWELL): the Glidewell program link row. Inserted at runtime with Enabled=0 like every vendor-direct program link
// (Programs.InsertIfMissing); the office's accounts are rows of glidewellacct, not program properties.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness.VendorDirect.Glidewell {
	///<summary>The Glidewell program link. Off until the office turns it on in Setup, Program Links, and sending stays impossible until the office
	///has entered a Glidewell account with its API key. Nothing here calls Glidewell.</summary>
	public static class GlidewellProgram {
		public const string ProgDesc="Glidewell lab cases";

		///<summary>Inserts the program row (Enabled=0) if missing. Returns true if it inserted it; the caller refreshes the caches and signals
		///InvalidType.Programs. Never calls Glidewell.</summary>
		public static bool EnsureProgram() {
			Meth.NoCheckMiddleTierRole();
			bool isInserted=Programs.InsertIfMissing(ProgramName.Glidewell,ProgDesc);
			if(isInserted || Programs.GetCur(ProgramName.Glidewell)==null) {
				Programs.RefreshCache();
			}
			return isInserted;
		}

		///<summary>True when the office turned the program link on. Every Glidewell control in the Edit Lab Case window shows only then.</summary>
		public static bool IsEnabled() {
			Meth.NoCheckMiddleTierRole();
			return Programs.IsEnabled(ProgramName.Glidewell);
		}

		///<summary>The program row's key, or 0 if the row does not exist yet (nobody has opened Program Links).</summary>
		public static long GetProgramNum() {
			Meth.NoCheckMiddleTierRole();
			return Programs.GetProgramNum(ProgramName.Glidewell);
		}
	}
}
