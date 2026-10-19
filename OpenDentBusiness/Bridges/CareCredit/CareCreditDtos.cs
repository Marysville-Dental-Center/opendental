// CareCredit (Synchrony) patient financing: request and response data, and a tolerant reader for Synchrony's JSON.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CodeBase;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenDentBusiness.Bridges {
	///<summary>The consumer data sent with a prefill or batch request. Only what the landing page needs: the date of birth only for a credit
	///application, the last four SSN digits only for a Lookup Only batch (carecredit.md 3.8, "Least data").</summary>
	public sealed class CareCreditConsumer {
		public string FirstName="";
		public string LastName="";
		public string Address1="";
		public string Address2="";
		public string City="";
		public string State="";
		public string Zip="";
		public string Phone="";
		public string Email="";
		///<summary>yyyy-MM-dd, or "" when not sent.</summary>
		public string DateOfBirth="";
		///<summary>Four digits, or "" when not sent.</summary>
		public string SsnLast4="";

		///<summary>The consumer fields of a patient. Phone: home, else wireless, else work. Digits only for the phone.</summary>
		public static CareCreditConsumer FromPatient(Patient patient,bool includeDateOfBirth,bool includeSsnLast4) {
			CareCreditConsumer consumer=new CareCreditConsumer();
			if(patient==null) {
				return consumer;
			}
			consumer.FirstName=(patient.FName??"").Trim();
			consumer.LastName=(patient.LName??"").Trim();
			consumer.Address1=(patient.Address??"").Trim();
			consumer.Address2=(patient.Address2??"").Trim();
			consumer.City=(patient.City??"").Trim();
			consumer.State=(patient.State??"").Trim();
			consumer.Zip=(patient.Zip??"").Trim();
			consumer.Phone=GetPhoneDigits(patient);
			consumer.Email=(patient.Email??"").Trim();
			if(includeDateOfBirth && patient.Birthdate.Year>1880) {
				consumer.DateOfBirth=patient.Birthdate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
			}
			if(includeSsnLast4) {
				consumer.SsnLast4=GetSsnLast4(patient.SSN);
			}
			return consumer;
		}

		///<summary>The first phone with at least 10 digits: home, wireless, work. Digits only. "" if none.</summary>
		public static string GetPhoneDigits(Patient patient) {
			foreach(string phone in new[] { patient.HmPhone,patient.WirelessPhone,patient.WkPhone }) {
				string digits=new string((phone??"").Where(x => char.IsDigit(x)).ToArray());
				if(digits.Length>=10) {
					return digits;
				}
			}
			return "";
		}

		///<summary>The last four digits of an SSN with 9 digits, or "".</summary>
		public static string GetSsnLast4(string ssn) {
			string digits=new string((ssn??"").Where(x => char.IsDigit(x)).ToArray());
			if(digits.Length!=9) {
				return "";
			}
			return digits.Substring(5);
		}

		///<summary>The JSON object sent to Synchrony. Field names are this program's (the gated reference may name them differently; map them in
		///CareCreditApiClient once the guide is read).</summary>
		public JObject ToJson() {
			JObject jObject=new JObject();
			jObject["firstName"]=FirstName;
			jObject["lastName"]=LastName;
			jObject["address1"]=Address1;
			if(Address2!="") {
				jObject["address2"]=Address2;
			}
			jObject["city"]=City;
			jObject["state"]=State;
			jObject["zipCode"]=Zip;
			jObject["phone"]=Phone;
			if(Email!="") {
				jObject["email"]=Email;
			}
			if(DateOfBirth!="") {
				jObject["dateOfBirth"]=DateOfBirth;
			}
			if(SsnLast4!="") {
				jObject["ssnLast4"]=SsnLast4;
			}
			return jObject;
		}
	}

	///<summary>A prefill request: push patient data into a hosted page and get its URL back.</summary>
	public sealed class CareCreditPrefillRequest {
		public CareCreditLandingPage LandingPage;
		public EnumCareCreditPrefillServiceType ServiceType=EnumCareCreditPrefillServiceType.C;
		public string MerchantNumber="";
		public string PartnerCode="";
		///<summary>Null for the home pages, which carry no patient.</summary>
		public CareCreditConsumer Consumer;
		public string InvoiceNumber="";
		public double PurchaseAmount;
		public double RefundAmount;
		public double EstimatedFeeAmount;
		public string NoteMemo="";
		public string PromoCode="";
		public string UserName="";
		///<summary>The prior batch or original transaction reference.</summary>
		public string OriginalReferenceId="";

		public JObject ToJson() {
			JObject jObject=new JObject();
			jObject["partnerCode"]=PartnerCode;
			jObject["merchantNumber"]=MerchantNumber;
			jObject["landingPage"]=LandingPage.ToString();
			jObject["serviceType"]=ServiceType.ToString();
			if(Consumer!=null) {
				jObject["consumer"]=Consumer.ToJson();
			}
			AddIfNotBlank(jObject,"invoiceNumber",InvoiceNumber);
			AddAmount(jObject,"purchaseAmount",PurchaseAmount);
			AddAmount(jObject,"refundAmount",RefundAmount);
			AddAmount(jObject,"estimatedFeeAmount",EstimatedFeeAmount);
			AddIfNotBlank(jObject,"noteMemo",NoteMemo);
			AddIfNotBlank(jObject,"promoCode",PromoCode);
			AddIfNotBlank(jObject,"userName",UserName);
			AddIfNotBlank(jObject,"originalReferenceId",OriginalReferenceId);
			return jObject;
		}

		internal static void AddIfNotBlank(JObject jObject,string name,string value) {
			if(!string.IsNullOrWhiteSpace(value)) {
				jObject[name]=value.Trim();
			}
		}

		internal static void AddAmount(JObject jObject,string name,double amount) {
			if(amount>0) {
				jObject[name]=Math.Round(amount,2,MidpointRounding.AwayFromZero).ToString("0.00",CultureInfo.InvariantCulture);
			}
		}
	}

	///<summary>What a prefill call returned.</summary>
	public sealed class CareCreditPrefillResult {
		public string ReferenceId="";
		public string PageUrl="";
		public string SessionId="";
		public string RawJson="";
	}

	///<summary>One Batch QuickScreen (or Lookup Only) request for one consumer.</summary>
	public sealed class CareCreditBatchRequest {
		public string MerchantNumber="";
		public string PartnerCode="";
		public bool IsLookupOnly;
		public CareCreditConsumer Consumer=new CareCreditConsumer();
		///<summary>Rounded to the nearest dollar.</summary>
		public double EstimatedFeeAmount;

		public JObject ToJson() {
			JObject jObject=new JObject();
			jObject["partnerCode"]=PartnerCode;
			jObject["merchantNumber"]=MerchantNumber;
			jObject["requestType"]=IsLookupOnly ? "LOOKUP" : "QUICKSCREEN";
			jObject["consumer"]=Consumer.ToJson();
			if(!IsLookupOnly) {
				jObject["estimatedFeeAmount"]=Math.Round(EstimatedFeeAmount,0,MidpointRounding.AwayFromZero).ToString("0",CultureInfo.InvariantCulture);
			}
			return jObject;
		}
	}

	///<summary>What a pullback (or batch result) said about a reference ID.</summary>
	public sealed class CareCreditPullbackResult {
		///<summary>The vendor's status text, as sent.</summary>
		public string VendorStatus="";
		public CareCreditWebStatus Status=CareCreditWebStatus.Unknown;
		///<summary>None if the response didn't say.</summary>
		public CareCreditTransType TransType=CareCreditTransType.None;
		public string ReferenceId="";
		public double Amount;
		///<summary>Positive. FormRpPaySheet shows it negated.</summary>
		public double MerchantFee;
		public string PromoCode="";
		public string UserName="";
		public double PreApprovalAmount;
		public double AvailableCredit;
		public bool IsInvalidInput;
		public bool IsMerchantClosed;
		public string ErrorDescription="";
		///<summary>The response with SSN and date-of-birth fields removed. Stored in LastResponseStr.</summary>
		public string ScrubbedJson="";
	}

	///<summary>The result of one batch QuickScreen row, read from carecreditwebresponse.LastResponseStr. Reads this program's responses and the
	///JSON Open Dental's bridge stored in migrated rows (property names are matched without regard to case or nesting).</summary>
	public sealed class CareCreditQSBatchResponse {
		public bool IsInvalidInput;
		public string ResponseDesc="";
		public bool IsMerchantClosed;

		///<summary>Null when the row has no readable JSON.</summary>
		public static CareCreditQSBatchResponse From(CareCreditWebResponse careCreditWebResponse) {
			if(careCreditWebResponse==null) {
				return null;
			}
			JObject jObject=CareCreditJson.TryParse(careCreditWebResponse.LastResponseStr);
			if(jObject==null) {
				return null;
			}
			CareCreditQSBatchResponse response=new CareCreditQSBatchResponse();
			response.IsInvalidInput=CareCreditJson.GetBool(jObject,"isInvalidInput","invalidInput");
			response.ResponseDesc=CareCreditJson.GetString(jObject,"responseDesc","responseDescription","errorDescription","errorMessage","message",
				"error_description","description");
			response.IsMerchantClosed=CareCreditJson.GetBool(jObject,"isMerchantClosed","merchantClosed")
				|| response.ResponseDesc.ToLowerInvariant().Contains("merchant closed");
			return response;
		}

		///<summary>The JSON stored for a batch row this program rejected before sending (missing data, no merchant number, ...).</summary>
		public static string ToLocalErrorJson(string responseDesc,bool isInvalidInput,bool isMerchantClosed=false) {
			JObject jObject=new JObject();
			jObject["responseDesc"]=responseDesc??"";
			jObject["isInvalidInput"]=isInvalidInput;
			if(isMerchantClosed) {
				jObject["isMerchantClosed"]=true;
			}
			jObject["source"]="local";
			return jObject.ToString(Formatting.None);
		}
	}

	///<summary>Reads Synchrony's JSON without depending on exact field names or nesting, which come from the gated reference. Names are matched
	///without regard to case, '_' or '-', searching nested objects breadth-first.</summary>
	public static class CareCreditJson {
		private static readonly string[] _arrayScrubNames={ "ssn","ssnlast4","socialsecuritynumber","social","taxid","dob","dateofbirth","birthdate",
			"birthday" };

		public static JObject TryParse(string json) {
			if(string.IsNullOrWhiteSpace(json)) {
				return null;
			}
			try {
				JToken jToken=JToken.Parse(json);
				return jToken as JObject;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
		}

		private static string Normalize(string name) {
			return new string((name??"").Where(x => char.IsLetterOrDigit(x)).ToArray()).ToLowerInvariant();
		}

		///<summary>The first property, breadth-first, whose normalized name is one of the names. Null if none.</summary>
		public static JToken Find(JObject jObject,params string[] arrayNames) {
			if(jObject==null) {
				return null;
			}
			List<string> listNames=arrayNames.Select(x => Normalize(x)).ToList();
			Queue<Tuple<JObject,int>> queue=new Queue<Tuple<JObject,int>>();
			queue.Enqueue(Tuple.Create(jObject,0));
			while(queue.Count>0) {
				Tuple<JObject,int> item=queue.Dequeue();
				//Look at this level first, in the caller's order of preference.
				for(int n=0;n<listNames.Count;n++) {
					JProperty jProperty=item.Item1.Properties().FirstOrDefault(x => Normalize(x.Name)==listNames[n]);
					if(jProperty!=null && jProperty.Value.Type!=JTokenType.Null && jProperty.Value.Type!=JTokenType.Object) {
						return jProperty.Value;
					}
				}
				if(item.Item2>=4) {
					continue;
				}
				foreach(JProperty jProperty in item.Item1.Properties()) {
					if(jProperty.Value is JObject jObjectChild) {
						queue.Enqueue(Tuple.Create(jObjectChild,item.Item2+1));
					}
					else if(jProperty.Value is JArray jArray && jArray.Count>0 && jArray[0] is JObject jObjectFirst) {
						queue.Enqueue(Tuple.Create(jObjectFirst,item.Item2+1));
					}
				}
			}
			return null;
		}

		public static string GetString(JObject jObject,params string[] arrayNames) {
			JToken jToken=Find(jObject,arrayNames);
			if(jToken==null || jToken.Type==JTokenType.Array) {
				return "";
			}
			return (jToken.ToString()??"").Trim();
		}

		public static double GetDouble(JObject jObject,params string[] arrayNames) {
			string value=GetString(jObject,arrayNames).Replace("$","").Replace(",","");
			double amount;
			if(double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out amount)) {
				return amount;
			}
			return 0;
		}

		public static bool GetBool(JObject jObject,params string[] arrayNames) {
			string value=GetString(jObject,arrayNames).ToLowerInvariant();
			return value=="true" || value=="1" || value=="y" || value=="yes";
		}

		///<summary>A copy of json without SSN or date-of-birth fields, at any depth. Not JSON: returned unchanged, cut to 4000 characters.</summary>
		public static string Scrub(string json) {
			JToken jToken;
			try {
				jToken=JToken.Parse(json??"");
			}
			catch(Exception ex) {
				ex.DoNothing();
				string text=json??"";
				return text.Length>4000 ? text.Substring(0,4000) : text;
			}
			ScrubToken(jToken);
			return jToken.ToString(Formatting.None);
		}

		private static void ScrubToken(JToken jToken) {
			if(jToken is JObject jObject) {
				foreach(JProperty jProperty in jObject.Properties().ToList()) {
					if(_arrayScrubNames.Contains(Normalize(jProperty.Name))) {
						jProperty.Remove();
						continue;
					}
					ScrubToken(jProperty.Value);
				}
			}
			else if(jToken is JArray jArray) {
				foreach(JToken jTokenChild in jArray) {
					ScrubToken(jTokenChild);
				}
			}
		}

		///<summary>Reads a pullback or batch-result answer. isQuickScreen maps a bare "approved" to PreApproved rather than Completed.</summary>
		public static CareCreditPullbackResult ReadPullback(string json,bool isQuickScreen) {
			CareCreditPullbackResult result=new CareCreditPullbackResult();
			result.ScrubbedJson=Scrub(json??"");
			JObject jObject=TryParse(json);
			if(jObject==null) {
				result.Status=CareCreditWebStatus.Unknown;
				return result;
			}
			result.VendorStatus=GetString(jObject,"status","transactionStatus","processingStatus","decision","quickScreenStatus","prescreenStatus",
				"lookupStatus","result");
			result.Status=CareCreditStatusMap.Map(result.VendorStatus,isQuickScreen);
			result.TransType=MapTransType(GetString(jObject,"transactionType","transType","type"));
			result.ReferenceId=GetString(jObject,"referenceId","refId","referenceNumber","refNumber");
			result.Amount=Math.Abs(GetDouble(jObject,"amount","transactionAmount","purchaseAmount","purchaseAmt","refundAmount","refundAmt"));
			result.MerchantFee=Math.Abs(GetDouble(jObject,"merchantFee","merchantDiscountFee","discountFee","fee"));
			result.PromoCode=GetString(jObject,"promoCode","promotionCode","promotion","promoDescription");
			result.UserName=GetString(jObject,"userName","user");
			result.PreApprovalAmount=GetDouble(jObject,"preApprovalAmount","preApprovedAmount","approvedAmount","creditLimit");
			result.AvailableCredit=GetDouble(jObject,"availableCredit","availableCreditAmount","openToBuy");
			result.IsInvalidInput=GetBool(jObject,"isInvalidInput","invalidInput");
			result.ErrorDescription=GetString(jObject,"errorDescription","errorMessage","responseDesc","error_description","message");
			result.IsMerchantClosed=GetBool(jObject,"isMerchantClosed","merchantClosed")
				|| result.ErrorDescription.ToLowerInvariant().Contains("merchant closed")
				|| result.VendorStatus.ToLowerInvariant().Contains("merchant closed");
			return result;
		}

		public static CareCreditTransType MapTransType(string value) {
			string normal=Normalize(value);
			if(normal.Contains("refund") || normal.Contains("return") || normal=="rf") {
				return CareCreditTransType.Refund;
			}
			if(normal.Contains("purchase") || normal.Contains("sale") || normal=="pr") {
				return CareCreditTransType.Purchase;
			}
			return CareCreditTransType.None;
		}
	}

	///<summary>Maps a vendor status to CareCreditWebStatus. The status vocabulary is in Synchrony's gated reference; this table accepts this
	///program's own names and descriptions plus common synonyms, and gives Unknown for anything else (the raw response is kept).</summary>
	public static class CareCreditStatusMap {
		private static readonly Dictionary<string,CareCreditWebStatus> _dictSynonyms=new Dictionary<string,CareCreditWebStatus> {
			{ "complete",CareCreditWebStatus.Completed },
			{ "completed",CareCreditWebStatus.Completed },
			{ "success",CareCreditWebStatus.Completed },
			{ "successful",CareCreditWebStatus.Completed },
			{ "processed",CareCreditWebStatus.Completed },
			{ "authorized",CareCreditWebStatus.Completed },
			{ "settled",CareCreditWebStatus.Completed },
			{ "pending",CareCreditWebStatus.Pending },
			{ "inprogress",CareCreditWebStatus.Pending },
			{ "open",CareCreditWebStatus.Pending },
			{ "created",CareCreditWebStatus.Pending },
			{ "started",CareCreditWebStatus.Pending },
			{ "submitted",CareCreditWebStatus.Pending },
			{ "preapproved",CareCreditWebStatus.PreApproved },
			{ "preapproval",CareCreditWebStatus.PreApproved },
			{ "declined",CareCreditWebStatus.Declined },
			{ "decline",CareCreditWebStatus.Declined },
			{ "notapproved",CareCreditWebStatus.Declined },
			{ "unabletopreapprove",CareCreditWebStatus.Declined },
			{ "callforauth",CareCreditWebStatus.CallForAuth },
			{ "callforauthorization",CareCreditWebStatus.CallForAuth },
			{ "referral",CareCreditWebStatus.CallForAuth },
			{ "duplicate",CareCreditWebStatus.DupQS },
			{ "dupqs",CareCreditWebStatus.DupQS },
			{ "duplicateqs",CareCreditWebStatus.DupQS },
			{ "cardholder",CareCreditWebStatus.AccountFound },
			{ "accountfound",CareCreditWebStatus.AccountFound },
			{ "existingcardholder",CareCreditWebStatus.AccountFound },
			{ "accountnotfound",CareCreditWebStatus.AccountNotFoundQS },
			{ "notfound",CareCreditWebStatus.AccountNotFoundQS },
			{ "noaccount",CareCreditWebStatus.AccountNotFoundQS },
			{ "cancelled",CareCreditWebStatus.Cancelled },
			{ "canceled",CareCreditWebStatus.Cancelled },
			{ "abandoned",CareCreditWebStatus.Cancelled },
			{ "voided",CareCreditWebStatus.Cancelled },
			{ "expired",CareCreditWebStatus.Expired },
			{ "timedout",CareCreditWebStatus.Expired },
			{ "timeout",CareCreditWebStatus.Expired },
			{ "error",CareCreditWebStatus.UnknownError },
			{ "failed",CareCreditWebStatus.UnknownError },
			{ "failure",CareCreditWebStatus.UnknownError },
		};

		public static CareCreditWebStatus Map(string vendorStatus,bool isQuickScreen) {
			string normal=new string((vendorStatus??"").Where(x => char.IsLetterOrDigit(x)).ToArray()).ToLowerInvariant();
			if(normal=="") {
				return CareCreditWebStatus.Unknown;
			}
			if(normal=="approved") {
				return isQuickScreen ? CareCreditWebStatus.PreApproved : CareCreditWebStatus.Completed;
			}
			CareCreditWebStatus status;
			if(_dictSynonyms.TryGetValue(normal,out status)) {
				return status;
			}
			foreach(CareCreditWebStatus statusEnum in Enum.GetValues(typeof(CareCreditWebStatus))) {
				string nameNormal=statusEnum.ToString().ToLowerInvariant();
				string descNormal=new string(statusEnum.GetDescription().Where(x => char.IsLetterOrDigit(x)).ToArray()).ToLowerInvariant();
				if(normal==nameNormal || normal==descNormal) {
					return statusEnum;
				}
			}
			return CareCreditWebStatus.Unknown;
		}
	}
}
