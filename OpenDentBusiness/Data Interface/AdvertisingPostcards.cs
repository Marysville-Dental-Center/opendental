using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CodeBase;
using OpenDentBusiness.PostcardMania;

namespace OpenDentBusiness {
	///<summary>Tools > Advertising > Postcards, directly against the office's own PostcardMania (PCM Integrations) DirectMail API v3 account.
	///The office picks patients and a design, sees an estimate, confirms, and the app places the order. Only first name, last name and the mailing
	///address leave the building (plus "P"+PatNum when the office turns on Send Patient Reference). Every call that sends recipient data is blocked
	///until the office records that a business associate agreement with the vendor is on file. Everything here runs on the workstation
	///(Meth.NoCheckMiddleTierRole): the middle tier server never contacts the vendor.</summary>
	public class AdvertisingPostcards {
		///<summary>Only used by the Open Dental HQ proxy methods, which have no callers left; they go with the HQ web service clean-up.</summary>
		public const string MassPostcardResponseNode="MassPostcardResponse";
		///<summary>Only used by the Open Dental HQ proxy methods, which have no callers left; they go with the HQ web service clean-up.</summary>
		public const string MassPostcardRequestNode="MassPostcardRequestNode";
		///<summary>The API takes 1 to 50,000 recipients per order. Larger mailings are split into several orders.</summary>
		public const int MaxRecipientsPerOrder=50000;
		///<summary>The verify endpoint documents no limit; send at most this many addresses per call.</summary>
		public const int VerifyChunkSize=1000;
		public const string ProductTypePostcard="postcard";
		///<summary>Every order reference this program makes starts with this, followed by the clinic number.</summary>
		public const string ExtRefNbrPrefix="ops-";
		private const int DESIGNS_PER_PAGE=100;
		private const int MAX_DESIGN_PAGES=20;
		private static readonly string[] _arrayUsCountries={ "","us","usa","u.s.","u.s.a.","united states","united states of america" };
		private static readonly Random _random=new Random();

		///<summary>Whether the BAA is on file. Tests replace it; the program reads the BAA On File setting.</summary>
		public static Func<bool> FuncIsBaaOnFile=() => PostcardManiaProgram.IsBaaOnFile();

		private static IPostcardManiaApiClient Api {
			get {
				return PostcardManiaApiClient.Inst;
			}
		}

		#region Account and designs
		///<summary>The account a clinic uses: its own, or the default. See PostcardManiaProgram.GetAccount.</summary>
		public static PostcardManiaAccount GetAccount(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			return PostcardManiaProgram.GetAccount(clinicNum);
		}

		///<summary>Logs in and reads the balance.</summary>
		public static PcmConnectionResult TestConnection(PostcardManiaAccount account) {
			Meth.NoCheckMiddleTierRole();
			return Api.TestConnection(account);
		}

		///<summary>All postcard designs on the account (the parent's and every child's: child apps only partly isolate designs).</summary>
		public static List<PcmDesign> GetDesigns(PostcardManiaAccount account) {
			Meth.NoCheckMiddleTierRole();
			List<PcmDesign> listDesigns=new List<PcmDesign>();
			for(int page=1;page<=MAX_DESIGN_PAGES;page++) {
				PcmPagedResult<PcmDesign> pagedResult=Api.GetDesigns(account,ProductTypePostcard,page,DESIGNS_PER_PAGE);
				listDesigns.AddRange(pagedResult.Results.Where(x => x!=null));
				if(!pagedResult.HasMorePages(page) || pagedResult.Results.Count==0) {
					break;
				}
			}
			return listDesigns.GroupBy(x => x.DesignID).Select(x => x.First()).ToList();
		}

		///<summary>An embedded editor URL for a designer design. The URL carries a short-lived token: open it only in the in-app browser window with
		///IsUrlSingleUse, never log it. Editing a template creates a copy, so the returned DesignID can differ.</summary>
		public static PcmDesignEditorResponse GetDesignEditorUrl(PostcardManiaAccount account,long designID,bool duplicate) {
			Meth.NoCheckMiddleTierRole();
			return Api.GetDesignEditUrl(account,designID,duplicate);
		}

		///<summary>Creates a designer design and returns its editor URL (same handling as GetDesignEditorUrl).</summary>
		public static PcmDesignEditorResponse CreateDesign(PostcardManiaAccount account,string name,string size) {
			Meth.NoCheckMiddleTierRole();
			return Api.CreateDesign(account,name,size);
		}

		///<summary>The proof image for a design, or null. Fetched without credentials.</summary>
		public static byte[] GetProofImage(string url) {
			Meth.NoCheckMiddleTierRole();
			return Api.GetProofImage(url);
		}
		#endregion Account and designs

		#region Recipients
		///<summary>True for the country values the vendor can mail to: blank, US, USA or United States. The API has no country field.</summary>
		public static bool IsUsCountry(string country) {
			Meth.NoCheckMiddleTierRole();
			string countryNormalized=Regex.Replace((country??"").Trim().ToLowerInvariant(),@"\s+"," ");
			return _arrayUsCountries.Contains(countryNormalized);
		}

		///<summary>Turns selected patients into recipients with the minimum necessary: first name (or preferred name when usePreferredName and one is
		///set), last name and mailing address, plus "P"+PatNum as extRefNbr only when sendPatientReference. Birthdate, email, status, contact method
		///and appointment dates are never copied. One card per patient (the patient list has a row per email address). Non-US and incomplete addresses
		///are skipped and counted. With isOneCardPerHousehold, patients at the same address (Address, Address2 and Zip, ignoring case and spacing)
		///get one card addressed to their guarantor (dictGuarantorsByPatNum, from GetGuarantorsByPatNum), or to the first patient if the guarantor
		///isn't known.</summary>
		public static PostcardRecipientList BuildRecipients(List<PatientInfo> listPatientInfos,bool usePreferredName,bool sendPatientReference,
			bool isOneCardPerHousehold=false,Dictionary<long,Patient> dictGuarantorsByPatNum=null)
		{
			Meth.NoCheckMiddleTierRole();
			PostcardRecipientList postcardRecipientList=new PostcardRecipientList();
			if(listPatientInfos==null) {
				return postcardRecipientList;
			}
			List<PatientInfo> listDistinct=new List<PatientInfo>();
			HashSet<long> hashSetPatNums=new HashSet<long>();
			for(int i=0;i<listPatientInfos.Count;i++) {
				if(listPatientInfos[i]==null) {
					continue;
				}
				if(!hashSetPatNums.Add(listPatientInfos[i].PatNum)) {
					postcardRecipientList.CountDuplicates++;
					continue;
				}
				listDistinct.Add(listPatientInfos[i]);
			}
			List<PatientInfo> listMailable=new List<PatientInfo>();
			for(int i=0;i<listDistinct.Count;i++) {
				if(!IsUsCountry(listDistinct[i].Country)) {
					postcardRecipientList.CountNonUs++;
					continue;
				}
				if(!HasCompleteAddress(listDistinct[i])) {
					postcardRecipientList.CountIncompleteAddress++;
					continue;
				}
				listMailable.Add(listDistinct[i]);
			}
			if(!isOneCardPerHousehold) {
				for(int i=0;i<listMailable.Count;i++) {
					PatientInfo patientInfo=listMailable[i];
					string firstName=(usePreferredName && !string.IsNullOrWhiteSpace(patientInfo.Preferred)) ? patientInfo.Preferred : patientInfo.FName;
					postcardRecipientList.Add(MakeRecipient(patientInfo,firstName,patientInfo.LName,patientInfo.PatNum,sendPatientReference),patientInfo.PatNum);
				}
				return postcardRecipientList;
			}
			foreach(IGrouping<string,PatientInfo> grouping in listMailable.GroupBy(x => GetHouseholdKey(x))) {
				List<PatientInfo> listHousehold=grouping.OrderBy(x => x.PatNum).ToList();
				postcardRecipientList.CountHouseholdMerged+=listHousehold.Count-1;
				PatientInfo patientInfoFirst=listHousehold[0];
				Patient patientGuarantor=null;
				if(dictGuarantorsByPatNum!=null) {
					for(int i=0;i<listHousehold.Count && patientGuarantor==null;i++) {
						dictGuarantorsByPatNum.TryGetValue(listHousehold[i].PatNum,out patientGuarantor);
					}
				}
				string firstName;
				string lastName;
				long patNumRef;
				if(patientGuarantor!=null) {
					firstName=(usePreferredName && !string.IsNullOrWhiteSpace(patientGuarantor.Preferred)) ? patientGuarantor.Preferred : patientGuarantor.FName;
					lastName=patientGuarantor.LName;
					patNumRef=patientGuarantor.PatNum;
				}
				else {
					firstName=(usePreferredName && !string.IsNullOrWhiteSpace(patientInfoFirst.Preferred)) ? patientInfoFirst.Preferred : patientInfoFirst.FName;
					lastName=patientInfoFirst.LName;
					patNumRef=patientInfoFirst.PatNum;
				}
				postcardRecipientList.Add(MakeRecipient(patientInfoFirst,firstName,lastName,patNumRef,sendPatientReference),patNumRef);
			}
			return postcardRecipientList;
		}

		///<summary>The guarantor (limited fields) of each patient, keyed by the patient's PatNum, for the one-card-per-household option.</summary>
		public static Dictionary<long,Patient> GetGuarantorsByPatNum(List<long> listPatNums) {
			Meth.NoCheckMiddleTierRole();
			Dictionary<long,Patient> dictGuarantors=new Dictionary<long,Patient>();
			if(listPatNums==null || listPatNums.Count==0) {
				return dictGuarantors;
			}
			List<Patient> listPatients=Patients.GetLimForPats(listPatNums.Distinct().ToList());
			List<Patient> listGuarantors=Patients.GetLimForPats(listPatients.Select(x => x.Guarantor).Where(x => x>0).Distinct().ToList());
			for(int i=0;i<listPatients.Count;i++) {
				Patient patientGuarantor=listGuarantors.FirstOrDefault(x => x.PatNum==listPatients[i].Guarantor);
				if(patientGuarantor!=null) {
					dictGuarantors[listPatients[i].PatNum]=patientGuarantor;
				}
			}
			return dictGuarantors;
		}

		private static bool HasCompleteAddress(PatientInfo patientInfo) {
			return (!string.IsNullOrWhiteSpace(patientInfo.Address) || !string.IsNullOrWhiteSpace(patientInfo.Address2))
				&& !string.IsNullOrWhiteSpace(patientInfo.City)
				&& !string.IsNullOrWhiteSpace(patientInfo.State)
				&& !string.IsNullOrWhiteSpace(patientInfo.Zip);
		}

		private static string GetHouseholdKey(PatientInfo patientInfo) {
			return NormalizeForMatch(patientInfo.Address)+"|"+NormalizeForMatch(patientInfo.Address2)+"|"+NormalizeForMatch(patientInfo.Zip);
		}

		private static string NormalizeForMatch(string value) {
			return Regex.Replace((value??"").Trim().ToLowerInvariant(),@"\s+"," ");
		}

		private static PcmRecipient MakeRecipient(PatientInfo patientInfoAddress,string firstName,string lastName,long patNumRef,bool sendPatientReference) {
			PcmRecipient pcmRecipient=new PcmRecipient();
			pcmRecipient.FirstName=(firstName??"").Trim();
			pcmRecipient.LastName=(lastName??"").Trim();
			string address=(patientInfoAddress.Address??"").Trim();
			string address2=(patientInfoAddress.Address2??"").Trim();
			if(address=="") {//The API requires address; the patient list allows a patient with only Address2.
				address=address2;
				address2="";
			}
			pcmRecipient.Address=address;
			pcmRecipient.Address2=(address2=="") ? null : address2;
			pcmRecipient.City=(patientInfoAddress.City??"").Trim();
			pcmRecipient.State=(patientInfoAddress.State??"").Trim();
			pcmRecipient.ZipCode=(patientInfoAddress.Zip??"").Trim();
			pcmRecipient.ExtRefNbr=sendPatientReference ? ("P"+patNumRef) : null;
			return pcmRecipient;
		}

		///<summary>The return address: the clinic's when clinics are on and it has an address, otherwise the practice's.</summary>
		public static PcmReturnAddress BuildReturnAddress(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			PcmReturnAddress pcmReturnAddress=new PcmReturnAddress();
			Clinic clinic=null;
			if(PrefC.HasClinicsEnabled && clinicNum>0) {
				clinic=Clinics.GetClinic(clinicNum);
			}
			if(clinic!=null && !string.IsNullOrWhiteSpace(clinic.Address)) {
				pcmReturnAddress.Company=NullIfBlank(clinic.Description);
				pcmReturnAddress.Address=NullIfBlank(clinic.Address);
				pcmReturnAddress.Address2=NullIfBlank(clinic.Address2);
				pcmReturnAddress.City=NullIfBlank(clinic.City);
				pcmReturnAddress.State=NullIfBlank(clinic.State);
				pcmReturnAddress.ZipCode=NullIfBlank(clinic.Zip);
				return pcmReturnAddress;
			}
			pcmReturnAddress.Company=NullIfBlank(PrefC.GetString(PrefName.PracticeTitle));
			pcmReturnAddress.Address=NullIfBlank(PrefC.GetString(PrefName.PracticeAddress));
			pcmReturnAddress.Address2=NullIfBlank(PrefC.GetString(PrefName.PracticeAddress2));
			pcmReturnAddress.City=NullIfBlank(PrefC.GetString(PrefName.PracticeCity));
			pcmReturnAddress.State=NullIfBlank(PrefC.GetString(PrefName.PracticeST));
			pcmReturnAddress.ZipCode=NullIfBlank(PrefC.GetString(PrefName.PracticeZip));
			return pcmReturnAddress;
		}

		///<summary>The return address on one or more lines, for display.</summary>
		public static string DescribeReturnAddress(PcmReturnAddress pcmReturnAddress) {
			Meth.NoCheckMiddleTierRole();
			if(pcmReturnAddress==null || string.IsNullOrWhiteSpace(pcmReturnAddress.Address)) {
				return "";
			}
			StringBuilder stringBuilder=new StringBuilder();
			if(!string.IsNullOrWhiteSpace(pcmReturnAddress.Company)) {
				stringBuilder.AppendLine(pcmReturnAddress.Company);
			}
			stringBuilder.AppendLine(pcmReturnAddress.Address);
			if(!string.IsNullOrWhiteSpace(pcmReturnAddress.Address2)) {
				stringBuilder.AppendLine(pcmReturnAddress.Address2);
			}
			stringBuilder.Append(pcmReturnAddress.City+", "+pcmReturnAddress.State+" "+pcmReturnAddress.ZipCode);
			return stringBuilder.ToString();
		}

		private static string NullIfBlank(string value) {
			return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
		}

		///<summary>Asks the vendor which recipient addresses are deliverable, VerifyChunkSize at a time. Sends addresses only (no names). Requires the
		///BAA. Returns the indexes (into listRecipients) of the invalid ones.</summary>
		public static PostcardVerifyResult VerifyRecipients(PostcardManiaAccount account,List<PcmRecipient> listRecipients) {
			Meth.NoCheckMiddleTierRole();
			RequireBaaOnFile();
			PostcardVerifyResult postcardVerifyResult=new PostcardVerifyResult();
			for(int start=0;start<listRecipients.Count;start+=VerifyChunkSize) {
				List<PcmRecipient> listChunk=listRecipients.Skip(start).Take(VerifyChunkSize).ToList();
				PcmAddressVerificationResponse response=Api.VerifyAddresses(account,listChunk.Select(x => x.ToAddress()).ToList());
				List<PcmAddress> listInvalid=response?.Results?.Invalid??new List<PcmAddress>();
				HashSet<string> hashSetInvalidKeys=new HashSet<string>(listInvalid.Where(x => x!=null).Select(x => GetAddressKey(x)));
				for(int i=0;i<listChunk.Count;i++) {
					if(hashSetInvalidKeys.Contains(GetAddressKey(listChunk[i].ToAddress()))) {
						postcardVerifyResult.ListInvalidIndexes.Add(start+i);
					}
				}
				postcardVerifyResult.CountValid+=response?.ValidCount??0;
				postcardVerifyResult.CountInvalid+=response?.InvalidCount??listInvalid.Count;
			}
			return postcardVerifyResult;
		}

		private static string GetAddressKey(PcmAddress address) {
			return NormalizeForMatch(address.Address)+"|"+NormalizeForMatch(address.Address2)+"|"+NormalizeForMatch(address.City)+"|"
				+NormalizeForMatch(address.State)+"|"+NormalizeForMatch(address.ZipCode);
		}
		#endregion Recipients

		#region Orders
		///<summary>A new order reference: ops-{clinicNum}-{yyyyMMddHHmmss}-{6 random characters}. Generate it once per mailing and keep it until the
		///Send window closes, so a retry can't place the same mailing twice (the API answers 409 to a repeated reference).</summary>
		public static string NewExtRefNbr(long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			const string chars="abcdefghijkmnpqrstuvwxyz23456789";
			StringBuilder stringBuilder=new StringBuilder();
			lock(_random) {
				for(int i=0;i<6;i++) {
					stringBuilder.Append(chars[_random.Next(chars.Length)]);
				}
			}
			return ExtRefNbrPrefix+clinicNum+"-"+DateTime.Now.ToString("yyyyMMddHHmmss")+"-"+stringBuilder;
		}

		///<summary>True if extRefNbr is one this program made for clinicNum.</summary>
		public static bool IsExtRefNbrForClinic(string extRefNbr,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			return (extRefNbr??"").StartsWith(ExtRefNbrPrefix+clinicNum+"-",StringComparison.OrdinalIgnoreCase);
		}

		///<summary>Splits a mailing into orders of at most MaxRecipientsPerOrder. One order keeps extRefNbrBase; several get -1, -2 and so on.</summary>
		public static List<PcmPostcardOrderRequest> SplitIntoOrders(PcmPostcardOrderRequest requestTemplate,List<PcmRecipient> listRecipients,
			string extRefNbrBase)
		{
			Meth.NoCheckMiddleTierRole();
			List<PcmPostcardOrderRequest> listOrders=new List<PcmPostcardOrderRequest>();
			if(listRecipients==null || listRecipients.Count==0) {
				return listOrders;
			}
			if(listRecipients.Count<=MaxRecipientsPerOrder) {
				listOrders.Add(requestTemplate.CopyWith(listRecipients.ToList(),extRefNbrBase));
				return listOrders;
			}
			int number=1;
			for(int start=0;start<listRecipients.Count;start+=MaxRecipientsPerOrder,number++) {
				listOrders.Add(requestTemplate.CopyWith(listRecipients.Skip(start).Take(MaxRecipientsPerOrder).ToList(),extRefNbrBase+"-"+number));
			}
			return listOrders;
		}

		///<summary>The vendor's price estimate for the orders, summed. Places nothing. Requires the BAA (it sends the recipients).</summary>
		public static PcmPricingResponse Estimate(PostcardManiaAccount account,List<PcmPostcardOrderRequest> listOrders) {
			Meth.NoCheckMiddleTierRole();
			RequireBaaOnFile();
			PcmPricingResponse pcmPricingResponseTotal=new PcmPricingResponse();
			pcmPricingResponseTotal.EstimatedTotalPrice=0;
			for(int i=0;i<listOrders.Count;i++) {
				PcmPricingResponse pcmPricingResponse=Api.EstimatePostcardOrder(account,listOrders[i]);
				if(pcmPricingResponse==null) {
					continue;
				}
				pcmPricingResponseTotal.EstimatedTotalPrice+=pcmPricingResponse.EstimatedTotalPrice??0;
				if(pcmPricingResponse.LineItems!=null) {
					pcmPricingResponseTotal.LineItems.AddRange(pcmPricingResponse.LineItems.Where(x => x!=null));
				}
			}
			return pcmPricingResponseTotal;
		}

		///<summary>Places the orders. This is a purchase: the caller has shown the estimate and the user confirmed. Each order is sent once.
		///With isRetry (this mailing's reference was tried before), each order is first looked up by its reference and skipped if it exists.
		///A 409 means it was already placed: it is looked up and reported, never placed again. After a timeout, network failure or 5xx the order is
		///looked up instead of being sent again; if it isn't found the outcome is unknown, PlaceOrder stops, and the user may retry later with the
		///same reference. Never throws for a single order's failure: see PostcardOrderOutcome.</summary>
		public static PostcardOrderOutcome PlaceOrder(PostcardManiaAccount account,List<PcmPostcardOrderRequest> listOrders,bool isRetry) {
			Meth.NoCheckMiddleTierRole();
			RequireBaaOnFile();
			PostcardOrderOutcome postcardOrderOutcome=new PostcardOrderOutcome();
			for(int i=0;i<listOrders.Count;i++) {
				PcmPostcardOrderRequest request=listOrders[i];
				if(isRetry) {
					PcmOrder pcmOrderExisting;
					try {
						pcmOrderExisting=FindOrder(account,request.ExtRefNbr);
					}
					catch(PostcardManiaException ex) {
						//Can't tell whether it was placed: don't risk a second order.
						postcardOrderOutcome.Error=ex;
						postcardOrderOutcome.IsOutcomeUnknown=true;
						return postcardOrderOutcome;
					}
					if(pcmOrderExisting!=null) {
						postcardOrderOutcome.ListResults.Add(PostcardOrderResult.FromOrder(pcmOrderExisting,request,true));
						continue;
					}
				}
				try {
					PcmOrderResponse pcmOrderResponse=Api.PlacePostcardOrder(account,request);
					PostcardOrderResult postcardOrderResult=new PostcardOrderResult();
					postcardOrderResult.ExtRefNbr=request.ExtRefNbr;
					postcardOrderResult.OrderID=pcmOrderResponse?.OrderID??0;
					postcardOrderResult.BatchID=pcmOrderResponse?.BatchID;
					postcardOrderResult.CountRecipients=request.Recipients.Count;
					postcardOrderOutcome.ListResults.Add(postcardOrderResult);
				}
				catch(PostcardManiaException ex) when(ex.Kind==PostcardManiaErrorKind.Conflict) {
					PcmOrder pcmOrderExisting=null;
					try {
						pcmOrderExisting=FindOrder(account,request.ExtRefNbr);
					}
					catch(PostcardManiaException exLookup) {
						exLookup.DoNothing();//It was placed; only the details are missing.
					}
					PostcardOrderResult postcardOrderResult=(pcmOrderExisting==null)
						? new PostcardOrderResult { ExtRefNbr=request.ExtRefNbr,CountRecipients=request.Recipients.Count,WasAlreadyPlaced=true }
						: PostcardOrderResult.FromOrder(pcmOrderExisting,request,true);
					postcardOrderOutcome.ListResults.Add(postcardOrderResult);
				}
				catch(PostcardManiaException ex) when(ex.IsOutcomeUnknown) {
					PcmOrder pcmOrderExisting=null;
					try {
						pcmOrderExisting=FindOrder(account,request.ExtRefNbr);
					}
					catch(PostcardManiaException exLookup) {
						exLookup.DoNothing();
					}
					if(pcmOrderExisting!=null) {
						postcardOrderOutcome.ListResults.Add(PostcardOrderResult.FromOrder(pcmOrderExisting,request,false));
						continue;
					}
					postcardOrderOutcome.Error=ex;
					postcardOrderOutcome.IsOutcomeUnknown=true;
					return postcardOrderOutcome;
				}
				catch(PostcardManiaException ex) {
					postcardOrderOutcome.Error=ex;
					return postcardOrderOutcome;
				}
			}
			return postcardOrderOutcome;
		}

		///<summary>The order with exactly this reference, or null. Matches the reference itself too, in case the vendor ignores the filter.</summary>
		public static PcmOrder FindOrder(PostcardManiaAccount account,string extRefNbr) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrWhiteSpace(extRefNbr)) {
				return null;
			}
			PcmPagedResult<PcmOrder> pagedResult=Api.GetOrders(account,extRefNbr,1,25);
			return pagedResult.Results.FirstOrDefault(x => x!=null && x.ExtRefNbr==extRefNbr);
		}

		///<summary>One page of the account's orders, newest first as the vendor returns them. With clinicNum given (not null), only orders this
		///program placed for that clinic (GET /order returns every child's orders).</summary>
		public static List<PcmOrder> GetRecentOrders(PostcardManiaAccount account,long? clinicNum,int page,int perPage) {
			Meth.NoCheckMiddleTierRole();
			PcmPagedResult<PcmOrder> pagedResult=Api.GetOrders(account,null,page,perPage);
			List<PcmOrder> listOrders=pagedResult.Results.Where(x => x!=null).ToList();
			if(clinicNum.HasValue) {
				listOrders=listOrders.Where(x => IsExtRefNbrForClinic(x.ExtRefNbr,clinicNum.Value)).ToList();
			}
			return listOrders;
		}

		///<summary>Cancels a pending order. The vendor's cutoff is 11:30 PM Eastern on the day it was placed.</summary>
		public static void CancelOrder(PostcardManiaAccount account,long orderID) {
			Meth.NoCheckMiddleTierRole();
			Api.CancelOrder(account,orderID);
		}

		public static PcmBatch GetBatch(PostcardManiaAccount account,long batchID) {
			Meth.NoCheckMiddleTierRole();
			return Api.GetBatch(account,batchID);
		}

		///<summary>Throws unless the office recorded that a BAA with the vendor is on file. Every call that sends recipient data checks this.</summary>
		public static void RequireBaaOnFile() {
			Meth.NoCheckMiddleTierRole();
			if(!FuncIsBaaOnFile()) {
				throw new ODException("Patient names and addresses can't be sent until Setup records that a signed business associate agreement "
					+"with the vendor is on file.");
			}
		}
		#endregion Orders

		#region Replaced Open Dental HQ calls
		///<summary>Open Dental HQ created vendor accounts for the office; the vendor's public API has no such call. The office signs up itself and
		///enters its own API key in Setup. Only the old account window still refers to this, and nothing opens that window any more.</summary>
		public static void ManageAccount(string accountTitle,string guid,string email,string fName,string lName,string extRefNum="",string company="",
			string phone="",string mobile="")
		{
			Meth.NoCheckMiddleTierRole();
			throw new ODException("Postcard accounts are no longer created from this program. Enter the office's own API key in Setup.");
		}

		///<summary>Open Dental HQ minted single sign-on links; replaced by the vendor's embedded design editor and its portal. Only the old account
		///window still refers to this, and nothing opens that window any more.</summary>
		public static string GetSSO(string guid,string email) {
			Meth.NoCheckMiddleTierRole();
			throw new ODException("Single sign-on links are no longer available. Use Edit design, or open the vendor portal and sign in.");
		}
		#endregion Replaced Open Dental HQ calls
	}

	///<summary>Recipients built from selected patients, with counts of what was left out. ListPatNums parallels ListRecipients: the patient (or the
	///household's guarantor) each card is for, kept in this program only.</summary>
	public class PostcardRecipientList {
		public List<PcmRecipient> ListRecipients=new List<PcmRecipient>();
		public List<long> ListPatNums=new List<long>();
		///<summary>Extra rows for the same patient (the patient list has a row per email address).</summary>
		public int CountDuplicates;
		public int CountNonUs;
		public int CountIncompleteAddress;
		///<summary>Patients who share a card with another household member.</summary>
		public int CountHouseholdMerged;

		public void Add(PcmRecipient pcmRecipient,long patNum) {
			ListRecipients.Add(pcmRecipient);
			ListPatNums.Add(patNum);
		}

		public void RemoveAtIndexes(IEnumerable<int> listIndexes) {
			foreach(int index in listIndexes.Distinct().OrderByDescending(x => x)) {
				if(index>=0 && index<ListRecipients.Count) {
					ListRecipients.RemoveAt(index);
					ListPatNums.RemoveAt(index);
				}
			}
		}
	}

	public class PostcardVerifyResult {
		public int CountValid;
		public int CountInvalid;
		///<summary>Indexes into the recipient list that was checked.</summary>
		public List<int> ListInvalidIndexes=new List<int>();
	}

	///<summary>One placed (or found) order.</summary>
	public class PostcardOrderResult {
		public string ExtRefNbr;
		///<summary>0 if the order was already placed but its details couldn't be read.</summary>
		public long OrderID;
		public long? BatchID;
		public string Status;
		public int CountRecipients;
		///<summary>True if it already existed (409, or found on a retry) rather than being placed by this call.</summary>
		public bool WasAlreadyPlaced;

		public static PostcardOrderResult FromOrder(PcmOrder pcmOrder,PcmPostcardOrderRequest request,bool wasAlreadyPlaced) {
			return new PostcardOrderResult {
				ExtRefNbr=pcmOrder.ExtRefNbr,
				OrderID=pcmOrder.OrderID,
				BatchID=pcmOrder.BatchID,
				Status=pcmOrder.Status,
				CountRecipients=request.Recipients.Count,
				WasAlreadyPlaced=wasAlreadyPlaced,
			};
		}
	}

	///<summary>What PlaceOrder did: the orders placed or found, and the error that stopped it, if any.</summary>
	public class PostcardOrderOutcome {
		public List<PostcardOrderResult> ListResults=new List<PostcardOrderResult>();
		public PostcardManiaException Error;
		///<summary>True if an order may or may not have been placed. Retrying with the same reference is safe: it looks the order up first.</summary>
		public bool IsOutcomeUnknown;

		public bool IsComplete {
			get {
				return Error==null;
			}
		}
	}
}
