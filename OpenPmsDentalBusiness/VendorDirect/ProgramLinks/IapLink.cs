// V-BRIDGE1, Open Dental 25.2 item v25_2-family-iaplus-bridge-update: the Insurance Answers Plus (IAP) button on the Insurance Plan window opens
// the IAP web sign-in page in the default browser. The bridge no longer reads the IAP desktop database (iaplus10.dll) and no longer copies plan
// data or benefits into the plan. No patient or plan data is sent.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace OpenPmsDentalBusiness.VendorDirect.ProgramLinks {
	///<summary>The Insurance Answers Plus program link (ProgName IAP, a row every database has had since the early conversions).
	///"Path of file to open" holds the IAP sign-in address, which the office enters (our choice: the public setup page does not say where the
	///address is kept, and gives no address).</summary>
	public static class IapLink {
		public const string ProgDesc="Insurance Answers Plus";

		public const string Note="The IAP button on the Insurance Plan window opens the address in Path of file to open in the web browser. Enter the "
			+"IAP sign-in address there; it must start with https://. No patient or plan data is sent. The IAP desktop program is no longer used.";

		///<summary>The message for a blank address (from the 25.2 feature spec).</summary>
		public const string MessagePathBlank="Set the IAP login address in Program Links.";

		public static BridgeLinkDefinition GetDefinition() {
			Meth.NoCheckMiddleTierRole();
			BridgeLinkDefinition definition=new BridgeLinkDefinition();
			definition.ProgName=ProgramName.IAP;
			definition.ProgDesc=ProgDesc;
			definition.Note=Note;
			//No toolbars: the button lives on the Insurance Plan window.
			return definition;
		}

		///<summary>True when the stored path is left over from the IAP desktop program: not blank and not a web address (for example C:\IAPlus\).
		///An http or https address is the office's own entry and is never cleared; launching refuses plain http.</summary>
		public static bool IsLegacyDesktopPath(string path) {
			if(string.IsNullOrWhiteSpace(path)) {
				return false;
			}
			Uri uri;
			if(Uri.TryCreate(path.Trim(),UriKind.Absolute,out uri) && (uri.Scheme==Uri.UriSchemeHttps || uri.Scheme==Uri.UriSchemeHttp)) {
				return false;
			}
			return true;
		}

		///<summary>Inserts the IAP row if it is missing (Enabled=0). If the row still has a desktop path, disables it, blanks the path and replaces the
		///note, so the button stays hidden until the office enters the web address (25.2 conversion rule). Runs each time Program Links opens and
		///changes nothing once the path is blank or a web address. Returns true if anything changed.</summary>
		public static bool EnsureProgram() {
			Meth.NoCheckMiddleTierRole();
			bool isChanged=BridgeLinkRows.EnsureProgram(GetDefinition());
			Program program=Programs.GetCur(ProgramName.IAP);
			if(program==null || !IsLegacyDesktopPath(program.Path)) {
				return isChanged;
			}
			Program programOld=program.Copy();
			program.Enabled=false;
			program.Path="";
			program.Note=Note;
			Programs.Update(program,programOld);
			Programs.RefreshCache();
			return true;
		}

		///<summary>The address for the IAP button, from the program row with this computer's path override.</summary>
		public static BridgeLaunch GetLaunch(Program program) {
			Meth.NoCheckMiddleTierRole();
			if(program==null) {
				return BridgeLaunch.Refused(BridgeLaunchProblem.PathBlank);
			}
			return GetLaunch(Programs.GetProgramPath(program));
		}

		///<summary>The address as it is, nothing added. A blank address, or a desktop path left over from the IAP desktop program, gives PathBlank.
		///Anything that is not a full https:// address on an allowed host is refused.</summary>
		public static BridgeLaunch GetLaunch(string path) {
			string url=(path??"").Trim();
			if(url=="" || IsLegacyDesktopPath(url)) {
				return BridgeLaunch.Refused(BridgeLaunchProblem.PathBlank,url);
			}
			BridgeLaunchProblem problem=BridgeLaunch.CheckWebAddress(url);
			if(problem!=BridgeLaunchProblem.None) {
				return BridgeLaunch.Refused(problem,url);
			}
			return new BridgeLaunch { FileName=url };
		}

		///<summary>The message for a refused IAP launch.</summary>
		public static string Describe(BridgeLaunchProblem problem) {
			if(problem==BridgeLaunchProblem.PathBlank) {
				return MessagePathBlank;
			}
			return BridgeLaunch.Describe(problem);
		}
	}
}
