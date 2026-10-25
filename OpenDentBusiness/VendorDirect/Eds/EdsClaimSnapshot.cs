// EDS claim validation (Open Dental 26.1): the plain data the local EDS checks look at, read from the database once, so the checks themselves
// are pure functions that tests can call without a database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenDentBusiness.VendorDirect.Eds {
	///<summary>A provider on the claim, as the e-claim sends it.</summary>
	public class EdsProviderInfo {
		public long ProvNum;
		public bool IsFound;
		public string Abbr="";
		public string Npi="";
		///<summary>The provider's own taxonomy override; blank when the code comes from the specialty.</summary>
		public string TaxonomyOverride="";
		///<summary>The taxonomy code the e-claim sends: the override, or the code for the provider's specialty.</summary>
		public string Taxonomy="";
	}

	///<summary>One procedure line of the claim.</summary>
	public class EdsProcLine {
		public int LineNumber;
		public long ProcNum;
		public string ProcCode="";
		public DateTime ProcDate;
	}

	///<summary>One row of the EDS payer list (electid rows that EDS's payer list download wrote, CommBridge=EDS).</summary>
	public class EdsPayerListEntry {
		public string PayorID="";
		public string CarrierName="";
		///<summary>Empty when the row's attributes are unknown (blank in the database).</summary>
		public List<EnumEDSPayerAttributes> ListAttributes=new List<EnumEDSPayerAttributes>();
		public bool AreAttributesKnown;
	}

	///<summary>What the local EDS checks need to know about one claim.</summary>
	public class EdsClaimSnapshot {
		public long ClaimNum;
		public long PatNum;
		///<summary>P, S, Other, PreAuth, Cap.</summary>
		public string ClaimType="";
		public EnumClaimMedType MedType;
		public DateTime DateService;
		public EdsProviderInfo ProvBill=new EdsProviderInfo();
		public EdsProviderInfo ProvTreat=new EdsProviderInfo();
		///<summary>The carrier's electronic payer ID.</summary>
		public string PayerId="";
		public string CarrierName="";
		///<summary>The subscriber ID the e-claim sends (the optional patient ID when the office's preference says so).</summary>
		public string SubscriberId="";
		public string Narrative="";
		public string AttachmentID="";
		public int CountAttachments;
		public List<EdsProcLine> ListProcLines=new List<EdsProcLine>();
		///<summary>True when the office has downloaded EDS's payer list (any electid row with CommBridge=EDS).</summary>
		public bool IsEdsPayerListLoaded;
		///<summary>The EDS payer list rows for PayerId.</summary>
		public List<EdsPayerListEntry> ListEdsPayers=new List<EdsPayerListEntry>();
		///<summary>True when the clearinghouse's X12 validator already checked the claim (NPI format, subscriber ID), so the EDS checks leave those to
		///it and report only what it does not.</summary>
		public bool IsX12Validated;
		public DateTime DateToday;

		///<summary>True when the claim already has an EDS attachment ID or sent attachments.</summary>
		public bool HasAttachments {
			get {
				return !string.IsNullOrWhiteSpace(AttachmentID) || CountAttachments>0;
			}
		}

		///<summary>Reads the snapshot for a claim from the database and the caches. Throws when the claim's plan, subscriber or carrier is missing; the
		///callers already report those as missing data.</summary>
		public static EdsClaimSnapshot FromDb(Claim claim,Clearinghouse clearinghouseClin) {
			EdsClaimSnapshot snapshot=new EdsClaimSnapshot();
			snapshot.ClaimNum=claim.ClaimNum;
			snapshot.PatNum=claim.PatNum;
			snapshot.ClaimType=claim.ClaimType??"";
			snapshot.MedType=claim.MedType;
			snapshot.DateService=claim.DateService;
			snapshot.Narrative=claim.Narrative??"";
			snapshot.AttachmentID=claim.AttachmentID??"";
			snapshot.CountAttachments=claim.Attachments?.Count??0;
			snapshot.DateToday=DateTime_.Today;
			snapshot.IsX12Validated=IsX12Format(clearinghouseClin);
			snapshot.ProvBill=GetProviderInfo(claim.ProvBill);
			snapshot.ProvTreat=GetProviderInfo(claim.ProvTreat);
			InsPlan insPlan=InsPlans.RefreshOne(claim.PlanNum);
			if(insPlan!=null) {
				Carrier carrier=Carriers.GetCarrier(insPlan.CarrierNum);
				if(carrier!=null) {
					snapshot.PayerId=carrier.ElectID??"";
					snapshot.CarrierName=carrier.CarrierName??"";
				}
			}
			InsSub insSub=InsSubs.GetOne(claim.InsSubNum);
			if(insSub!=null) {
				List<PatPlan> listPatPlans=PatPlans.Refresh(claim.PatNum);
				snapshot.SubscriberId=X837_5010.GetSubscriberIDorPatID(listPatPlans,insSub)??"";
			}
			List<ClaimProc> listClaimProcs=ClaimProcs.RefreshForClaim(claim.ClaimNum).FindAll(x => x.ProcNum!=0);
			List<Procedure> listProcedures=Procedures.GetManyProc(listClaimProcs.Select(x => x.ProcNum).Distinct().ToList(),false);
			for(int i=0;i<listClaimProcs.Count;i++) {
				ClaimProc claimProc=listClaimProcs[i];
				Procedure procedure=listProcedures.FirstOrDefault(x => x.ProcNum==claimProc.ProcNum);
				EdsProcLine edsProcLine=new EdsProcLine();
				edsProcLine.LineNumber=i+1;
				edsProcLine.ProcNum=claimProc.ProcNum;
				edsProcLine.ProcCode=claimProc.CodeSent??"";
				if(edsProcLine.ProcCode=="" && procedure!=null) {
					edsProcLine.ProcCode=ProcedureCodes.GetStringProcCode(procedure.CodeNum,doThrowIfMissing:false);
				}
				edsProcLine.ProcDate=procedure?.ProcDate??claimProc.ProcDate;
				snapshot.ListProcLines.Add(edsProcLine);
			}
			List<ElectID> listElectIDsEds=ElectIDs.GetWhere(x => x.CommBridge==EclaimsCommBridge.EDS);
			snapshot.IsEdsPayerListLoaded=listElectIDsEds.Count>0;
			string payerIdTrimmed=snapshot.PayerId.Trim();
			snapshot.ListEdsPayers=listElectIDsEds
				.FindAll(x => string.Equals((x.PayorID??"").Trim(),payerIdTrimmed,StringComparison.OrdinalIgnoreCase) && payerIdTrimmed!="")
				.Select(x => MakePayerListEntry(x.PayorID,x.CarrierName,x.Attributes))
				.ToList();
			return snapshot;
		}

		///<summary>True for the X12 formats whose validator checks NPI format and the subscriber ID.</summary>
		public static bool IsX12Format(Clearinghouse clearinghouseClin) {
			if(clearinghouseClin==null) {
				return false;
			}
			return clearinghouseClin.Eformat==ElectronicClaimFormat.x837D_4010
				|| clearinghouseClin.Eformat==ElectronicClaimFormat.x837D_5010_dental
				|| clearinghouseClin.Eformat==ElectronicClaimFormat.x837_5010_med_inst;
		}

		///<summary>A payer list row from the values stored in electid. attributes is the comma-separated list of EnumEDSPayerAttributes numbers that
		///EDS.GetPayerList writes; blank means unknown.</summary>
		public static EdsPayerListEntry MakePayerListEntry(string payorID,string carrierName,string attributes) {
			EdsPayerListEntry entry=new EdsPayerListEntry();
			entry.PayorID=payorID??"";
			entry.CarrierName=carrierName??"";
			if(string.IsNullOrWhiteSpace(attributes)) {
				return entry;
			}
			string[] arrayParts=attributes.Split(new[] { ',' },StringSplitOptions.RemoveEmptyEntries);
			for(int i=0;i<arrayParts.Length;i++) {
				int value;
				if(!int.TryParse(arrayParts[i].Trim(),out value) || !Enum.IsDefined(typeof(EnumEDSPayerAttributes),value)) {
					continue;
				}
				entry.ListAttributes.Add((EnumEDSPayerAttributes)value);
			}
			entry.AreAttributesKnown=true;//EDS wrote the list; a payer with no flags is a paper, medical payer.
			return entry;
		}

		private static EdsProviderInfo GetProviderInfo(long provNum) {
			EdsProviderInfo providerInfo=new EdsProviderInfo();
			providerInfo.ProvNum=provNum;
			Provider provider=provNum==0 ? null : Providers.GetProv(provNum);
			if(provider==null) {
				return providerInfo;
			}
			providerInfo.IsFound=true;
			providerInfo.Abbr=provider.Abbr??"";
			providerInfo.Npi=(provider.NationalProvID??"").Trim();
			providerInfo.TaxonomyOverride=(provider.TaxonomyCodeOverride??"").Trim();
			try {
				providerInfo.Taxonomy=(X12Generator.GetTaxonomy(provider)??"").Trim();
			}
			catch(Exception ex) {
				ex.DoNothing();
				providerInfo.Taxonomy=providerInfo.TaxonomyOverride;
			}
			return providerInfo;
		}
	}
}
