// The Electronic Billing window (Open Dental 26.2 release notes: "Moved Electronic Billing settings to a new window"; package P262-BILLING,
// docs/port-notes/P262-BILLING.md). The settings and their storage are the ones Billing Defaults had; only the window is new.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;

namespace OpenPmsDentalBusiness {
	///<summary>The stored form of the electronic billing settings, shared by the Electronic Billing window and its tests. Every value is kept where
	///Billing Defaults kept it before 26.2: the preference rows below, and the ebill table (account number, user name, password and the two
	///addresses, per clinic).</summary>
	public class EbillSettings {
		///<summary>The DentalXChange upload URL used when the URL Override box is left blank (the default from before 16.2.19).</summary>
		public const string DXC_URL_DEFAULT="https://claimconnect.dentalxchange.com/dci/upload.svl";

		///<summary>The choices of the method list, in the order of BillingUseElectronicEnum (the list index is the stored value).</summary>
		public static readonly string[] ArrayMethodNames=new string[] { "No electronic billing","DentalXChange","Output to file","ClaimX / ExtraDent","EDS" };

		///<summary>Every preference the Electronic Billing window saves. Billing Defaults saved all of them before 26.2 and saves none of them now.
		///</summary>
		public static readonly List<PrefName> ListPrefNames=new List<PrefName>() {
			PrefName.BillingUseElectronic,
			PrefName.BillingElectVendorId,
			PrefName.BillingElectVendorPMSCode,
			PrefName.BillingElectCreditCardChoices,
			PrefName.BillingElectCreatePDF,
			PrefName.BillingElectStmtUploadURL,
			PrefName.BillingElectStmtOutputPathPos,
			PrefName.BillingElectIncludeClinicNums,
			PrefName.BillingElectStmtOutputPathClaimX,
			PrefName.BillingElectStmtOutputPathEds,
			PrefName.BillingElectIncludeAdjustDescript,
		};

		///<summary>The preference that holds the output path (or, for DentalXChange, the URL override) of a method, by its list index. Null for
		///"No electronic billing" and for an index outside the list.</summary>
		public static PrefName? GetOutputPathPrefName(int methodIndex) {
			Meth.NoCheckMiddleTierRole();
			switch(methodIndex) {
				case 1:
					return PrefName.BillingElectStmtUploadURL;
				case 2:
					return PrefName.BillingElectStmtOutputPathPos;
				case 3:
					return PrefName.BillingElectStmtOutputPathClaimX;
				case 4:
					return PrefName.BillingElectStmtOutputPathEds;
				default:
					return null;
			}
		}

		///<summary>True for the methods that write files to a folder (Output to file, ClaimX / ExtraDent, EDS): the folder must exist to save.</summary>
		public static bool IsOutputPathRequired(int methodIndex) {
			Meth.NoCheckMiddleTierRole();
			return methodIndex==2 || methodIndex==3 || methodIndex==4;
		}

		///<summary>True for the methods that can save a PDF of each statement (DentalXChange, ClaimX / ExtraDent).</summary>
		public static bool CanCreatePdf(int methodIndex) {
			Meth.NoCheckMiddleTierRole();
			return methodIndex==1 || methodIndex==3;
		}

		///<summary>The stored text of the accepted credit cards: "MC", "V", "D" and "A" in that order, comma separated, as Billing Defaults wrote
		///it.</summary>
		public static string FormatCreditCardChoices(bool isMasterCard,bool isVisa,bool isDiscover,bool isAmEx) {
			Meth.NoCheckMiddleTierRole();
			List<string> listCards=new List<string>();
			if(isMasterCard) {
				listCards.Add("MC");
			}
			if(isVisa) {
				listCards.Add("V");
			}
			if(isDiscover) {
				listCards.Add("D");
			}
			if(isAmEx) {
				listCards.Add("A");
			}
			return string.Join(",",listCards);
		}

		///<summary>Reads the stored accepted credit cards the way Billing Defaults read them (a card is accepted when its letters appear anywhere).
		///</summary>
		public static void ParseCreditCardChoices(string creditCardChoices,out bool isMasterCard,out bool isVisa,out bool isDiscover,out bool isAmEx) {
			Meth.NoCheckMiddleTierRole();
			string value=creditCardChoices??"";
			isMasterCard=value.Contains("MC");
			isVisa=value.Contains("V");
			isDiscover=value.Contains("D");
			isAmEx=value.Contains("A");
		}
	}
}
