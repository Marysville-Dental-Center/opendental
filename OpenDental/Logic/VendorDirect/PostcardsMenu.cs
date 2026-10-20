// Vendor-direct integrations: Tools > Advertising > Postcards, and the PostcardMania entry in Setup > Program Links.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Windows.Forms;
using OpenDentBusiness;
using OpenDentBusiness.PostcardMania;

namespace OpenDental.VendorDirect {
	///<summary>Owned by the POSTCARD package (docs/specs/vendor-direct/README.md, 3.2). Ground rule 7: the Advertising menu shows only while the
	///PostcardMania program link is enabled, and Setup lets the office enable it only once an API key and secret are entered and a Test connection
	///has passed, so a visible menu always means "enabled and configured".</summary>
	public static class PostcardsMenu {
		///<summary>True if Tools > Advertising should show. Safe before the program cache is loaded (false), and when the program row doesn't exist
		///yet (false: the row is inserted the first time Setup > Program Links opens).</summary>
		public static bool IsAvailable() {
			if(Programs.HListIsNull()) {
				return false;
			}
			return Programs.IsEnabled(ProgramName.PostcardMania);
		}

		///<summary>Runs when the user clicks Tools > Advertising > Postcards.</summary>
		public static void Open() {
			if(!IsAvailable()) {
				//The menu is hidden in this case; this covers a stale menu after another workstation turned the integration off.
				MsgBox.Show("PostcardsMenu","Postcards are turned off. They can be turned on in Setup, Program Links, PostcardMania.");
				return;
			}
			if(!Security.IsAuthorized(EnumPermType.Advertising)) {
				return;
			}
			if(!PostcardManiaProgram.IsAnyAccountConfigured()) {
				if(!Security.IsAuthorized(EnumPermType.Setup,true)) {
					MsgBox.Show("PostcardsMenu","No PostcardMania API key is set up yet. Someone with Setup permission can enter it in Setup, Program Links.");
					return;
				}
				using FormAdvertisingPostcardsSetup formAdvertisingPostcardsSetup=new FormAdvertisingPostcardsSetup();
				formAdvertisingPostcardsSetup.ShowDialog();
				if(!PostcardManiaProgram.IsAnyAccountConfigured() || !IsAvailable()) {
					return;
				}
			}
			using FormAdvertisingPostcardsSend formAdvertisingPostcardsSend=new FormAdvertisingPostcardsSend();
			formAdvertisingPostcardsSend.ShowDialog();
		}
	}

	///<summary>Registers the PostcardMania setup window with Setup > Program Links (found by ProgramLinkSetupRegistry).</summary>
	public class PostcardManiaProgramLinkSetup:IProgramLinkSetup {
		public ProgramName ProgName {
			get {
				return ProgramName.PostcardMania;
			}
		}

		public bool EnsureProgram() {
			return PostcardManiaProgram.EnsureProgram();
		}

		public DialogResult OpenSetup(IWin32Window owner) {
			using FormAdvertisingPostcardsSetup formAdvertisingPostcardsSetup=new FormAdvertisingPostcardsSetup();
			formAdvertisingPostcardsSetup.ShowDialog();
			return formAdvertisingPostcardsSetup.DialogResult;
		}
	}
}
