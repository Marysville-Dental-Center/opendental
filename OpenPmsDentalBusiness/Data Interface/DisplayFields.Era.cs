// B-ERA: Display Fields for the Claims Paid grid of the ERA window (Open Dental 25.3.78 and 25.4, DisplayFieldCategory.ERA = 23). The office
// chooses, orders and sizes the grid's columns in Setup, Display Fields. Rules: docs/specs/features-25.3.md (v25_3-account-era-displayfields),
// docs/specs/features-25.4.md (v25_4-claims-era-displayfields) and docs/port-notes/B-ERA.md.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenPmsDentalBusiness {
	public partial class DisplayFields {
		///<summary>The columns of the Claims Paid grid in the ERA window (FormEtrans835Edit). The internal names are the 24.3 column titles (the two
		///titles that wrap are stored without the line break). Recd and Patient can't be removed.</summary>
		public static class EraClaimPaid {
			public const string Recd="Recd";
			public const string Patient="Patient";
			public const string DateService="DateService";
			public const string ClaimIdentifier="Claim Identifier";
			public const string PayorControlNum="Payor Control#";
			public const string Status="Status";
			public const string ClaimFee="ClaimFee";
			public const string InsPaid="InsPaid";
			public const string PatPort="PatPort";
			public const string Deduct="Deduct";
			public const string Writeoff="Writeoff";
			///<summary>Fork extra, not shown by default. The subscriber ID the payer reported (NM1*IL, or NM1*QC when there is none).</summary>
			public const string SubscriberId="Subscriber ID";
			///<summary>Fork extra, not shown by default. The claim's allowed amount: insurance paid plus patient responsibility.</summary>
			public const string Allowed="Allowed";
			///<summary>Fork extra, not shown by default. The ClaimNum of the claim the ERA claim is attached to, or "Detached".</summary>
			public const string MatchedClaim="Matched Claim";

			///<summary>Columns that are always in the grid. Display Fields does not let them be removed.</summary>
			public static readonly string[] ArrayRequired=new[] { Recd,Patient };

			///<summary>The 24.3 columns, in 24.3 order and widths. This is the default list.</summary>
			public static readonly List<(string InternalName,int Width)> ListDefaults=new List<(string,int)> {
				(Recd,32),(Patient,70),(DateService,80),(ClaimIdentifier,50),(PayorControlNum,56),(Status,70),
				(ClaimFee,70),(InsPaid,70),(PatPort,70),(Deduct,70),(Writeoff,70),
			};

			///<summary>Columns that are available but not in the default list.</summary>
			public static readonly List<(string InternalName,int Width)> ListExtras=new List<(string,int)> {
				(SubscriberId,100),(Allowed,70),(MatchedClaim,70),
			};

			///<summary>The 24.3 title of the column (before translation), used when the office gave the field no description.</summary>
			public static string GetDefaultHeader(string internalName) {
				switch(internalName) {
					case ClaimIdentifier:
						return "Claim\r\nIdentifier";
					case PayorControlNum:
						return "Payor\r\nControl#";
					default:
						return internalName;
				}
			}

			public static HorizontalAlignment GetAlignment(string internalName) {
				switch(internalName) {
					case Recd:
					case DateService:
					case PayorControlNum:
						return HorizontalAlignment.Center;
					case ClaimFee:
					case InsPaid:
					case PatPort:
					case Deduct:
					case Writeoff:
					case Allowed:
						return HorizontalAlignment.Right;
					default:
						return HorizontalAlignment.Left;
				}
			}

			///<summary>Patient and Status take up the free width, as in 24.3.</summary>
			public static bool IsWidthDynamic(string internalName) {
				return internalName==Patient || internalName==Status;
			}

			///<summary>The text of the cell for the column. recdText is the Recd column's text ("X" when processed, "N/A" when detached), which the
			///window works out from the claimprocs and attaches.</summary>
			public static string GetCellText(string internalName,Hx835_Claim claimPaid,string recdText) {
				switch(internalName) {
					case Recd:
						return recdText??"";
					case Patient:
						return claimPaid.PatientName?.ToString()??"";
					case DateService:
						string strDateService=claimPaid.DateServiceStart.ToShortDateString();
						if(claimPaid.DateServiceEnd>claimPaid.DateServiceStart) {
							strDateService+=" to "+claimPaid.DateServiceEnd.ToShortDateString();
						}
						return strDateService;
					case ClaimIdentifier:
						return claimPaid.ClaimTrackingNumber??"";
					case PayorControlNum:
						return claimPaid.PayerControlNumber??"";
					case Status:
						return claimPaid.StatusCodeDescript??"";
					case ClaimFee:
						return claimPaid.ClaimFee.ToString("f2");
					case InsPaid:
						return claimPaid.InsPaid.ToString("f2");
					case PatPort:
						return claimPaid.PatientPortionAmt.ToString("f2");
					case Deduct:
						return claimPaid.PatientDeductAmt.ToString("f2");
					case Writeoff:
						return claimPaid.WriteoffAmt.ToString("f2");
					case SubscriberId:
						string subscriberId=claimPaid.SubscriberName?.SubscriberId??"";
						if(string.IsNullOrWhiteSpace(subscriberId)) {
							subscriberId=claimPaid.PatientName?.SubscriberId??"";
						}
						return subscriberId;
					case Allowed:
						return claimPaid.AllowedAmt.ToString("f2");
					case MatchedClaim:
						if(claimPaid.ClaimNum!=0) {
							return claimPaid.ClaimNum.ToString();
						}
						if(claimPaid.IsAttachedToClaim) {
							return Lans.g("FormEtrans835Edit","Detached");
						}
						return "";
					default:
						return "";
				}
			}
		}

		private static List<DisplayField> GetDefaultListEra() {
			return EraClaimPaid.ListDefaults.Select(x => new DisplayField(x.InternalName,x.Width,DisplayFieldCategory.ERA)).ToList();
		}

		private static List<DisplayField> GetAllAvailableListEra() {
			return EraClaimPaid.ListDefaults.Concat(EraClaimPaid.ListExtras)
				.Select(x => new DisplayField(x.InternalName,x.Width,DisplayFieldCategory.ERA)).ToList();
		}

		///<summary>True for a column that Display Fields must not let the office remove (ERA: Recd and Patient).</summary>
		public static bool IsRequiredField(DisplayFieldCategory displayFieldCategory,string internalName) {
			Meth.NoCheckMiddleTierRole();
			if(displayFieldCategory==DisplayFieldCategory.ERA) {
				return EraClaimPaid.ArrayRequired.Contains(internalName);
			}
			return false;
		}

		///<summary>The columns of the ERA Claims Paid grid: the office's list for the ERA category, or the default list.</summary>
		public static List<DisplayField> GetForEraClaimPaidGrid() {
			Meth.NoCheckMiddleTierRole();
			return GetEraClaimPaidColumns(GetForCategory(DisplayFieldCategory.ERA));
		}

		///<summary>Cleans a stored ERA list for the grid: drops rows whose internal name this program does not know (for example a column a newer
		///version added), puts back a missing required column (Recd first, Patient after it), and falls back to the default list when nothing is
		///left. Never changes the stored rows.</summary>
		public static List<DisplayField> GetEraClaimPaidColumns(List<DisplayField> listDisplayFieldsStored) {
			Meth.NoCheckMiddleTierRole();
			List<string> listKnownNames=GetAllAvailableListEra().Select(x => x.InternalName).ToList();
			List<DisplayField> listDisplayFields=(listDisplayFieldsStored??new List<DisplayField>())
				.Where(x => listKnownNames.Contains(x.InternalName))
				.Select(x => x.Copy())
				.ToList();
			if(listDisplayFields.Count==0) {
				return GetDefaultListEra();
			}
			if(!listDisplayFields.Any(x => x.InternalName==EraClaimPaid.Recd)) {
				listDisplayFields.Insert(0,new DisplayField(EraClaimPaid.Recd,32,DisplayFieldCategory.ERA));
			}
			if(!listDisplayFields.Any(x => x.InternalName==EraClaimPaid.Patient)) {
				int indexRecd=listDisplayFields.FindIndex(x => x.InternalName==EraClaimPaid.Recd);
				listDisplayFields.Insert(indexRecd+1,new DisplayField(EraClaimPaid.Patient,70,DisplayFieldCategory.ERA));
			}
			return listDisplayFields;
		}

		///<summary>False for a category this program has no fields for yet (a placeholder for a later Open Dental version), so Setup does not list it.
		///The ortho chart has its own window and is always listed.</summary>
		public static bool HasAvailableFields(DisplayFieldCategory displayFieldCategory) {
			Meth.NoCheckMiddleTierRole();
			if(displayFieldCategory==DisplayFieldCategory.OrthoChart) {
				return true;
			}
			return GetAllAvailableList(displayFieldCategory).Count>0;
		}
	}
}
