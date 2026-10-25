// EDS claim validation (Open Dental 26.1): the EDS-specific local checks run before any network call, and the reading of EDS's attachment
// validation response. Pure functions over EdsClaimSnapshot, so tests need no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;
using OpenDentBusiness.Eclaims;

namespace OpenDentBusiness.VendorDirect.Eds {
	public static class EdsClaimChecks {
		public const string CHECK_PROV_NOT_FOUND="ProvNotFound";
		public const string CHECK_NPI_MISSING="NpiMissing";
		public const string CHECK_NPI_FORMAT="NpiFormat";
		public const string CHECK_NPI_CHECK_DIGIT="NpiCheckDigit";
		public const string CHECK_TAXONOMY_MISSING="TaxonomyMissing";
		public const string CHECK_TAXONOMY_FORMAT="TaxonomyFormat";
		public const string CHECK_PAYER_ID_MISSING="PayerIdMissing";
		public const string CHECK_PAYER_NOT_ON_LIST="PayerNotOnEdsList";
		public const string CHECK_PAYER_PAPER="PayerPaperOnly";
		public const string CHECK_PAYER_MEDICAL="PayerMedicalOnly";
		public const string CHECK_SUBSCRIBER_ID_MISSING="SubscriberIdMissing";
		public const string CHECK_DATE_SERVICE_FUTURE="DateServiceFuture";
		public const string CHECK_PROC_DATE_FUTURE="ProcDateFuture";
		public const string CHECK_ATTACHMENT_REQUIRED="AttachmentRequired";
		public const string CHECK_NARRATIVE_REQUIRED="NarrativeRequired";
		public const string CHECK_OTHER_MISSING_DATA="ClaimMissingData";

		#region NPI and taxonomy
		///<summary>True when npi is 10 digits, optionally after the 80840 prefix, and its last digit is the check digit of CMS's published NPI rule
		///(the Luhn formula over the number with the 80840 prefix).</summary>
		public static bool IsNpiCheckDigitValid(string npi) {
			string digits=GetNpiDigits(npi);
			if(digits==null) {
				return false;
			}
			string withPrefix="80840"+digits;
			int sum=0;
			for(int i=0;i<withPrefix.Length;i++) {
				int digit=withPrefix[withPrefix.Length-1-i]-'0';
				if(i%2==1) {
					digit*=2;
					if(digit>9) {
						digit-=9;
					}
				}
				sum+=digit;
			}
			return sum%10==0;
		}

		///<summary>The 10 NPI digits, without the optional 80840 prefix. Null when npi is not 10 digits (or 80840 and 10 digits).</summary>
		public static string GetNpiDigits(string npi) {
			string value=(npi??"").Trim();
			if(Regex.IsMatch(value,"^80840[0-9]{10}$")) {
				return value.Substring(5);
			}
			if(Regex.IsMatch(value,"^[0-9]{10}$")) {
				return value;
			}
			return null;
		}

		///<summary>True for a code in the published 10-character format of the Health Care Provider Taxonomy code set: nine letters or digits
		///followed by X.</summary>
		public static bool IsTaxonomyFormatValid(string taxonomy) {
			return Regex.IsMatch((taxonomy??"").Trim(),"^[0-9A-Za-z]{9}[Xx]$");
		}
		#endregion NPI and taxonomy

		#region Local checks
		///<summary>The EDS-specific local checks, errors first. Run before any network call. Checks the X12 validator already does (NPI format,
		///subscriber ID present) are left to it when snapshot.IsX12Validated, so a problem is never listed twice.</summary>
		public static List<EdsValidationIssue> GetLocalIssues(EdsClaimSnapshot snapshot) {
			List<EdsValidationIssue> listIssues=new List<EdsValidationIssue>();
			if(snapshot==null) {
				return listIssues;
			}
			//Providers. When one provider is both, it is checked once.
			if(snapshot.ProvBill.ProvNum!=0 && snapshot.ProvBill.ProvNum==snapshot.ProvTreat.ProvNum) {
				AddProviderIssues(listIssues,snapshot.ProvBill,Lans.g("EDS","Billing and treating provider"),snapshot.IsX12Validated);
			}
			else {
				AddProviderIssues(listIssues,snapshot.ProvBill,Lans.g("EDS","Billing provider"),snapshot.IsX12Validated);
				AddProviderIssues(listIssues,snapshot.ProvTreat,Lans.g("EDS","Treating provider"),snapshot.IsX12Validated);
			}
			AddPayerIssues(listIssues,snapshot);
			if(!snapshot.IsX12Validated && string.IsNullOrWhiteSpace(snapshot.SubscriberId)) {
				listIssues.Add(MakeLocal(EdsIssueSeverity.Error,CHECK_SUBSCRIBER_ID_MISSING,Lans.g("EDS","Subscriber"),
					Lans.g("EDS","The subscriber ID is missing."),
					Lans.g("EDS","Enter the subscriber ID in the patient's insurance plan (Family module).")));
			}
			AddDateIssues(listIssues,snapshot);
			return Order(listIssues);
		}

		private static void AddProviderIssues(List<EdsValidationIssue> listIssues,EdsProviderInfo providerInfo,string location,bool isX12Validated) {
			string howToFix=Lans.g("EDS","Fix the provider in Setup > Providers.");
			if(!providerInfo.IsFound) {
				listIssues.Add(MakeLocal(EdsIssueSeverity.Error,CHECK_PROV_NOT_FOUND,location,Lans.g("EDS","The provider was not found."),
					Lans.g("EDS","Choose the provider again in Edit Claim.")));
				return;
			}
			string locationNamed=location+(providerInfo.Abbr=="" ? "" : " ("+providerInfo.Abbr+")");
			string npiDigits=GetNpiDigits(providerInfo.Npi);
			if(string.IsNullOrWhiteSpace(providerInfo.Npi)) {
				if(!isX12Validated) {
					listIssues.Add(MakeLocal(EdsIssueSeverity.Error,CHECK_NPI_MISSING,locationNamed,Lans.g("EDS","The NPI is missing."),howToFix));
				}
			}
			else if(npiDigits==null) {
				if(!isX12Validated) {
					listIssues.Add(MakeLocal(EdsIssueSeverity.Error,CHECK_NPI_FORMAT,locationNamed,
						Lans.g("EDS","The NPI must be 10 digits, optionally after the prefix 80840."),howToFix));
				}
			}
			else if(!IsNpiCheckDigitValid(npiDigits)) {
				listIssues.Add(MakeLocal(EdsIssueSeverity.Error,CHECK_NPI_CHECK_DIGIT,locationNamed,
					Lans.g("EDS","The NPI")+" "+providerInfo.Npi+" "+Lans.g("EDS","is not a valid NPI: its check digit does not match."),
					Lans.g("EDS","Correct the NPI in Setup > Providers. A mistyped digit is the usual cause.")));
			}
			if(string.IsNullOrWhiteSpace(providerInfo.Taxonomy)) {
				listIssues.Add(MakeLocal(EdsIssueSeverity.Error,CHECK_TAXONOMY_MISSING,locationNamed,Lans.g("EDS","The taxonomy code is missing."),
					Lans.g("EDS","Set the provider's specialty or taxonomy code override in Setup > Providers.")));
			}
			else if(!IsTaxonomyFormatValid(providerInfo.Taxonomy)) {
				//The X12 validator already reports an override that is not 10 characters long.
				bool isReportedByX12=isX12Validated && providerInfo.TaxonomyOverride.Length>0 && providerInfo.TaxonomyOverride.Length!=10;
				if(!isReportedByX12) {
					listIssues.Add(MakeLocal(EdsIssueSeverity.Error,CHECK_TAXONOMY_FORMAT,locationNamed,
						Lans.g("EDS","The taxonomy code")+" "+providerInfo.Taxonomy+" "+Lans.g("EDS","is not a 10-character taxonomy code ending in X."),
						Lans.g("EDS","Correct the taxonomy code override in Setup > Providers, or clear it to use the specialty's code.")));
				}
			}
		}

		private static void AddPayerIssues(List<EdsValidationIssue> listIssues,EdsClaimSnapshot snapshot) {
			string location=Lans.g("EDS","Carrier")+(snapshot.CarrierName=="" ? "" : " ("+snapshot.CarrierName+")");
			string payerId=(snapshot.PayerId??"").Trim();
			if(payerId.Length<3) {
				//Shorter IDs go out as the paper-claim payer ID on X12 claims.
				listIssues.Add(MakeLocal(EdsIssueSeverity.Error,CHECK_PAYER_ID_MISSING,location,
					payerId=="" ? Lans.g("EDS","The carrier has no electronic payer ID.") : Lans.g("EDS","The carrier's payer ID")+" "+payerId+" "
						+Lans.g("EDS","is too short to be a payer ID."),
					Lans.g("EDS","Enter the carrier's electronic payer ID in the insurance plan's carrier, or mark the carrier to not send claims electronically.")));
				return;
			}
			if(!snapshot.IsEdsPayerListLoaded) {
				return;//The office never downloaded EDS's payer list: nothing to compare with.
			}
			if(snapshot.ListEdsPayers.Count==0) {
				listIssues.Add(MakeLocal(EdsIssueSeverity.Warning,CHECK_PAYER_NOT_ON_LIST,location,
					Lans.g("EDS","Payer ID")+" "+payerId+" "+Lans.g("EDS","is not on the EDS payer list."),
					Lans.g("EDS","Check the carrier's payer ID against EDS's current payer list.")));
				return;
			}
			List<EdsPayerListEntry> listKnown=snapshot.ListEdsPayers.FindAll(x => x.AreAttributesKnown);
			if(listKnown.Count==0) {
				return;
			}
			if(!listKnown.Any(x => x.ListAttributes.Contains(EnumEDSPayerAttributes.DefaultClaimTP))) {
				listIssues.Add(MakeLocal(EdsIssueSeverity.Warning,CHECK_PAYER_PAPER,location,
					Lans.g("EDS","The EDS payer list shows payer ID")+" "+payerId+" "+Lans.g("EDS","as a paper payer, not an electronic one."),
					Lans.g("EDS","Check the carrier's payer ID. A paper payer's claims may be printed and mailed instead of sent electronically.")));
			}
			if(snapshot.MedType==EnumClaimMedType.Dental && !listKnown.Any(x => x.ListAttributes.Contains(EnumEDSPayerAttributes.PayerType))) {
				listIssues.Add(MakeLocal(EdsIssueSeverity.Warning,CHECK_PAYER_MEDICAL,location,
					Lans.g("EDS","The EDS payer list shows payer ID")+" "+payerId+" "+Lans.g("EDS","as a medical payer, but this is a dental claim."),
					Lans.g("EDS","Check the carrier's payer ID.")));
			}
		}

		private static void AddDateIssues(List<EdsValidationIssue> listIssues,EdsClaimSnapshot snapshot) {
			if(snapshot.ClaimType=="PreAuth") {
				return;//Preauthorizations are for future work.
			}
			DateTime dateToday=snapshot.DateToday.Date;
			string howToFix=Lans.g("EDS","Correct the date, or wait to send the claim until the work is done.");
			if(snapshot.DateService.Year>1880 && snapshot.DateService.Date>dateToday) {
				listIssues.Add(MakeLocal(EdsIssueSeverity.Error,CHECK_DATE_SERVICE_FUTURE,Lans.g("EDS","Claim"),
					Lans.g("EDS","The service date")+" "+snapshot.DateService.ToShortDateString()+" "+Lans.g("EDS","is in the future."),howToFix));
			}
			for(int i=0;i<snapshot.ListProcLines.Count;i++) {
				EdsProcLine edsProcLine=snapshot.ListProcLines[i];
				if(edsProcLine.ProcDate.Year<1880 || edsProcLine.ProcDate.Date<=dateToday) {
					continue;
				}
				listIssues.Add(MakeLocal(EdsIssueSeverity.Error,CHECK_PROC_DATE_FUTURE,GetProcLineLocation(edsProcLine),
					Lans.g("EDS","The procedure date")+" "+edsProcLine.ProcDate.ToShortDateString()+" "+Lans.g("EDS","is in the future."),howToFix));
			}
		}
		#endregion Local checks

		#region EDS response
		///<summary>The problems in EDS's answer to the attachment validation (EDS.ValidateClaim). A payer that requires an attachment, for the claim or
		///for a procedure, is a warning while the claim has no EDS attachment yet; a required narrative is a warning while the claim has neither a
		///narrative nor an attachment. EDS's comments go in the message. Throws nothing; a null response gives no problems (the caller reports it).
		///</summary>
		public static List<EdsValidationIssue> GetIssuesFromAttachmentResponse(EDS.ListPayerResponses listPayerResponses,EdsClaimSnapshot snapshot) {
			List<EdsValidationIssue> listIssues=new List<EdsValidationIssue>();
			if(listPayerResponses?.Payers==null || snapshot==null) {
				return listIssues;
			}
			bool hasAttachments=snapshot.HasAttachments;
			bool hasNarrative=!string.IsNullOrWhiteSpace(snapshot.Narrative);
			string howToFixAttachment=Lans.g("EDS","Add the attachment from Edit Claim's EDS tab (Add Attachment) before sending.");
			for(int i=0;i<listPayerResponses.Payers.Count;i++) {
				EDS.PayerResponse payerResponse=listPayerResponses.Payers[i];
				if(payerResponse==null) {
					continue;
				}
				if(payerResponse.ClaimLevelResponse && !hasAttachments) {
					listIssues.Add(MakeEds(EdsIssueSeverity.Warning,CHECK_ATTACHMENT_REQUIRED,Lans.g("EDS","Claim"),
						Lans.g("EDS","EDS reports that the payer requires an attachment for this claim."),howToFixAttachment));
				}
				if(payerResponse.ProcedureResponses==null) {
					continue;
				}
				for(int j=0;j<payerResponse.ProcedureResponses.Count;j++) {
					EDS.ProcedureResponse procedureResponse=payerResponse.ProcedureResponses[j];
					if(procedureResponse==null || !procedureResponse.Response) {
						continue;
					}
					string location=Lans.g("EDS","Procedure")+" "+(procedureResponse.ProcID??"").Trim();
					List<EDS.ProcedureDocument> listDocuments=(procedureResponse.Documents??new List<EDS.ProcedureDocument>()).FindAll(x => x!=null);
					bool isNarrativeRequired=listDocuments.Any(x => x.DocumentTypeCode==EDS.EnumDocumentTypeCode.Narrative);
					List<EDS.ProcedureDocument> listDocumentsOther=listDocuments.FindAll(x => x.DocumentTypeCode!=EDS.EnumDocumentTypeCode.Narrative);
					string comments=string.IsNullOrWhiteSpace(procedureResponse.Comments) ? "" : " "+Lans.g("EDS","EDS comment:")+" "+procedureResponse.Comments.Trim();
					if(!hasAttachments && (listDocumentsOther.Count>0 || !isNarrativeRequired)) {
						string message;
						if(listDocumentsOther.Count==0) {
							message=Lans.g("EDS","EDS reports that the payer requires an attachment for this procedure.");
						}
						else {
							message=Lans.g("EDS","EDS reports that the payer requires:")+" "
								+string.Join("; ",listDocumentsOther.Select(x => GetDocumentText(x)));
						}
						listIssues.Add(MakeEds(EdsIssueSeverity.Warning,CHECK_ATTACHMENT_REQUIRED,location,message+comments,howToFixAttachment));
					}
					if(isNarrativeRequired && !hasNarrative && !hasAttachments) {
						EDS.ProcedureDocument documentNarrative=listDocuments.First(x => x.DocumentTypeCode==EDS.EnumDocumentTypeCode.Narrative);
						string reason=string.IsNullOrWhiteSpace(documentNarrative.AttachmentReason) ? "" : " ("+documentNarrative.AttachmentReason.Trim()+")";
						listIssues.Add(MakeEds(EdsIssueSeverity.Warning,CHECK_NARRATIVE_REQUIRED,location,
							Lans.g("EDS","EDS reports that the payer requires a narrative for this procedure")+reason+"."+comments,
							Lans.g("EDS","Add a Narrative attachment with the narrative text from Edit Claim's EDS tab (Add Attachment).")));
					}
				}
			}
			return Order(listIssues);
		}

		private static string GetDocumentText(EDS.ProcedureDocument procedureDocument) {
			string text=procedureDocument.DocumentTypeCode.GetDescription();
			if(!string.IsNullOrWhiteSpace(procedureDocument.AttachmentReason)) {
				text+=" ("+procedureDocument.AttachmentReason.Trim()+")";
			}
			return text;
		}
		#endregion EDS response

		#region Helpers
		///<summary>Errors first, then warnings, otherwise in the order found. Duplicates collapse.</summary>
		public static List<EdsValidationIssue> Order(List<EdsValidationIssue> listIssues) {
			List<EdsValidationIssue> listIssuesDistinct=new List<EdsValidationIssue>();
			foreach(EdsValidationIssue issue in (listIssues??new List<EdsValidationIssue>()).OrderBy(x => x.Severity==EdsIssueSeverity.Error ? 0 : 1)) {
				if(listIssuesDistinct.Any(x => x.Key==issue.Key)) {
					continue;
				}
				listIssuesDistinct.Add(issue);
			}
			return listIssuesDistinct;
		}

		public static string GetProcLineLocation(EdsProcLine edsProcLine) {
			string location=Lans.g("EDS","Procedure line")+" "+edsProcLine.LineNumber;
			if(!string.IsNullOrWhiteSpace(edsProcLine.ProcCode)) {
				location+=" ("+edsProcLine.ProcCode.Trim()+")";
			}
			return location;
		}

		public static EdsValidationIssue MakeLocal(EdsIssueSeverity severity,string checkId,string location,string message,string howToFix) {
			return MakeIssue(severity,EdsIssueSource.Local,checkId,location,message,howToFix);
		}

		public static EdsValidationIssue MakeEds(EdsIssueSeverity severity,string checkId,string location,string message,string howToFix) {
			return MakeIssue(severity,EdsIssueSource.Eds,checkId,location,message,howToFix);
		}

		private static EdsValidationIssue MakeIssue(EdsIssueSeverity severity,EdsIssueSource source,string checkId,string location,string message,
			string howToFix)
		{
			EdsValidationIssue issue=new EdsValidationIssue();
			issue.Severity=severity;
			issue.Source=source;
			issue.CheckId=checkId??"";
			issue.Location=location??"";
			issue.Message=message??"";
			issue.HowToFix=howToFix??"";
			return issue;
		}
		#endregion Helpers
	}
}
