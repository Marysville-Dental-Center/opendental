// PostcardMania (PCM Integrations) DirectMail API v3: request and response shapes, as the vendor's public reference documents them.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OpenDentBusiness.PostcardMania {
	///<summary>JSON settings for the DirectMail API: camelCase names, nulls left out, unknown fields ignored (the v3 reference still changes).</summary>
	public static class PcmJson {
		public static readonly JsonSerializerSettings Settings=new JsonSerializerSettings {
			ContractResolver=new CamelCasePropertyNamesContractResolver(),
			NullValueHandling=NullValueHandling.Ignore,
			MissingMemberHandling=MissingMemberHandling.Ignore,
			DateParseHandling=DateParseHandling.None,
		};

		public static string Serialize(object value) {
			return JsonConvert.SerializeObject(value,Settings);
		}

		public static T Deserialize<T>(string json) {
			return JsonConvert.DeserializeObject<T>(json,Settings);
		}
	}

	///<summary>Writes a date as yyyy-MM-dd (the API's mailDate format) and reads it back.</summary>
	public class PcmDateOnlyConverter:JsonConverter {
		public override bool CanConvert(Type objectType) {
			return objectType==typeof(DateTime) || objectType==typeof(DateTime?);
		}

		public override object ReadJson(JsonReader reader,Type objectType,object existingValue,JsonSerializer serializer) {
			if(reader.TokenType==JsonToken.Null) {
				return null;
			}
			if(reader.Value is DateTime) {
				return ((DateTime)reader.Value).Date;
			}
			DateTime date;
			if(DateTime.TryParse(reader.Value?.ToString(),CultureInfo.InvariantCulture,DateTimeStyles.None,out date)) {
				return date.Date;
			}
			return null;
		}

		public override void WriteJson(JsonWriter writer,object value,JsonSerializer serializer) {
			if(value==null) {
				writer.WriteNull();
				return;
			}
			writer.WriteValue(((DateTime)value).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));
		}
	}

	///<summary>Mail classes the order endpoint accepts.</summary>
	public static class PcmMailClass {
		public const string FirstClass="FirstClass";
		public const string Standard="Standard";
	}

	///<summary>POST /auth/login body.</summary>
	public class PcmAuthRequest {
		public string ApiKey { get; set; }
		public string ApiSecret { get; set; }
		///<summary>Optional child app reference, at most 50 characters. Left out when blank.</summary>
		public string ChildRefNbr { get; set; }
	}

	///<summary>POST /auth/login response. The token lasts about 15 minutes; there is no refresh endpoint, so the client logs in again.</summary>
	public class PcmAuthResponse {
		public string Token { get; set; }
		///<summary>ISO-8601 date and time, kept as text and parsed by the client.</summary>
		public string Expires { get; set; }
	}

	///<summary>GET /integration/balance response.</summary>
	public class PcmBalanceResponse {
		///<summary>Dollars.</summary>
		public decimal? MoneyOnAccount { get; set; }
	}

	///<summary>A US mailing address, as /recipient/verify takes it.</summary>
	public class PcmAddress {
		public string Address { get; set; }
		public string Address2 { get; set; }
		public string City { get; set; }
		public string State { get; set; }
		public string ZipCode { get; set; }
	}

	///<summary>A key and value for a design's variable fields.</summary>
	public class PcmVariable {
		public string Key { get; set; }
		public string Value { get; set; }
	}

	///<summary>One postcard recipient. Only name and mailing address are ever sent; extRefNbr only when the office turns on
	///"Send patient reference". There is no country field: the vendor mails US addresses only.</summary>
	public class PcmRecipient {
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string Company { get; set; }
		public string Address { get; set; }
		public string Address2 { get; set; }
		public string City { get; set; }
		public string State { get; set; }
		public string ZipCode { get; set; }
		public string ExtRefNbr { get; set; }
		public List<PcmVariable> Variables { get; set; }

		public PcmAddress ToAddress() {
			return new PcmAddress {
				Address=Address,
				Address2=Address2,
				City=City,
				State=State,
				ZipCode=ZipCode,
			};
		}
	}

	///<summary>The return address printed on the card. USPS recommends one for First Class mail.</summary>
	public class PcmReturnAddress {
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public string Company { get; set; }
		public string Address { get; set; }
		public string Address2 { get; set; }
		public string City { get; set; }
		public string State { get; set; }
		public string ZipCode { get; set; }
	}

	///<summary>Order-level addressing options. ExceptionalAddressingType is resident, occupant or business; it adds an "or Current ..." line.</summary>
	public class PcmAddressingConfig {
		public string ExceptionalAddressingType { get; set; }
	}

	///<summary>POST /order/postcard and POST /order/postcard/pricing body.</summary>
	public class PcmPostcardOrderRequest {
		public long? DesignID { get; set; }
		///<summary>FirstClass or Standard.</summary>
		public string MailClass { get; set; }
		///<summary>Our idempotency key for the order. The API answers 409 if an order with this reference already exists.</summary>
		public string ExtRefNbr { get; set; }
		[JsonConverter(typeof(PcmDateOnlyConverter))]
		public DateTime? MailDate { get; set; }
		public PcmReturnAddress ReturnAddress { get; set; }
		public PcmAddressingConfig Addressing { get; set; }
		public List<PcmVariable> GlobalDesignVariables { get; set; }
		///<summary>1 to 50,000 recipients.</summary>
		public List<PcmRecipient> Recipients { get; set; }=new List<PcmRecipient>();

		///<summary>A copy with another set of recipients and another reference, for splitting a large mailing into several orders.</summary>
		public PcmPostcardOrderRequest CopyWith(List<PcmRecipient> listRecipients,string extRefNbr) {
			PcmPostcardOrderRequest request=(PcmPostcardOrderRequest)MemberwiseClone();
			request.Recipients=listRecipients;
			request.ExtRefNbr=extRefNbr;
			return request;
		}
	}

	///<summary>POST /order/postcard response (201).</summary>
	public class PcmOrderResponse {
		public long OrderID { get; set; }
		public long? BatchID { get; set; }
		public string ExtRefNbr { get; set; }
	}

	///<summary>An order, as GET /order lists it.</summary>
	public class PcmOrder {
		public long OrderID { get; set; }
		public long? BatchID { get; set; }
		///<summary>Pending, Processing, Processed, Delivered, Undeliverable or Canceled.</summary>
		public string Status { get; set; }
		public string OrderDate { get; set; }
		public long? DesignID { get; set; }
		public string ExtRefNbr { get; set; }
		public string MailClass { get; set; }
		public int? RecipientCount { get; set; }

		///<summary>Only pending orders can be canceled, and only before the vendor's daily cutoff.</summary>
		public bool IsCancelable {
			get {
				return string.Equals(Status,"Pending",StringComparison.OrdinalIgnoreCase);
			}
		}
	}

	///<summary>GET /batch/{batchID} response.</summary>
	public class PcmBatch {
		public long BatchID { get; set; }
		///<summary>Includes "Pending Payment" and "Failed Payment", which are resolved in the vendor portal.</summary>
		public string Status { get; set; }
		public string InvoiceURL { get; set; }

		public bool NeedsPayment {
			get {
				return string.Equals(Status,"Pending Payment",StringComparison.OrdinalIgnoreCase)
					|| string.Equals(Status,"Failed Payment",StringComparison.OrdinalIgnoreCase);
			}
		}
	}

	public class PcmDesignSize {
		public string Key { get; set; }
		public string Label { get; set; }
	}

	public class PcmDesignField {
		public string FieldKey { get; set; }
		public bool Mandatory { get; set; }
	}

	///<summary>A design, as GET /design lists it.</summary>
	public class PcmDesign {
		public long DesignID { get; set; }
		public string FriendlyName { get; set; }
		public PcmDesignSize Size { get; set; }
		public List<string> MailClasses { get; set; }=new List<string>();
		public string ProofFront { get; set; }
		public string ProofBack { get; set; }
		public List<PcmDesignField> DesignFields { get; set; }=new List<PcmDesignField>();
		public string ApprovalDateTime { get; set; }
		///<summary>Uploaded designs can't be opened in the designer.</summary>
		public bool IsUpload { get; set; }

		///<summary>Field keys the API fills from the recipient itself. Any other mandatory field needs a variable, which v1 doesn't send.</summary>
		private static readonly string[] _arrayAddressFieldKeys={ "firstname","lastname","company","address","address2","city","state","zipcode",
			"fullname","name" };

		///<summary>Mandatory fields other than the name and address fields. Orders for such a design need variables this version doesn't fill.</summary>
		public List<string> GetMandatoryCustomFields() {
			List<string> listFields=new List<string>();
			if(DesignFields==null) {
				return listFields;
			}
			for(int i=0;i<DesignFields.Count;i++) {
				if(DesignFields[i]==null || !DesignFields[i].Mandatory || string.IsNullOrEmpty(DesignFields[i].FieldKey)) {
					continue;
				}
				string key=DesignFields[i].FieldKey.Replace("_","").Replace(" ","").ToLowerInvariant();
				if(Array.IndexOf(_arrayAddressFieldKeys,key)>=0) {
					continue;
				}
				listFields.Add(DesignFields[i].FieldKey);
			}
			return listFields;
		}

		public override string ToString() {
			string name=string.IsNullOrWhiteSpace(FriendlyName) ? ("Design "+DesignID) : FriendlyName;
			if(Size!=null && !string.IsNullOrWhiteSpace(Size.Label)) {
				return name+" ("+Size.Label+")";
			}
			return name;
		}
	}

	///<summary>GET /design/{id}/edit and POST /design/custom response: a design ID and an editor URL that already carries a short-lived token.
	///Open the URL only in the in-app browser window with IsUrlSingleUse, never in the system browser, and never log it.</summary>
	public class PcmDesignEditorResponse {
		public long DesignID { get; set; }
		public string Url { get; set; }
	}

	///<summary>POST /design/custom body.</summary>
	public class PcmNewDesignRequest {
		public string Name { get; set; }
		///<summary>46, 57, 58, 68, 69, 611, 811 or BRO. The postcard sizes are 46 through 611.</summary>
		public string Size { get; set; }
	}

	public class PcmPricingLineItem {
		public string Name { get; set; }
		public decimal? Price { get; set; }
		public int? Quantity { get; set; }
		public decimal? LineTotal { get; set; }
	}

	///<summary>POST /order/postcard/pricing response.</summary>
	public class PcmPricingResponse {
		public decimal? EstimatedTotalPrice { get; set; }
		public List<PcmPricingLineItem> LineItems { get; set; }=new List<PcmPricingLineItem>();
	}

	public class PcmAddressVerificationResults {
		public List<PcmAddress> Valid { get; set; }=new List<PcmAddress>();
		public List<PcmAddress> Invalid { get; set; }=new List<PcmAddress>();
	}

	///<summary>POST /recipient/verify response.</summary>
	public class PcmAddressVerificationResponse {
		public int ValidCount { get; set; }
		public int InvalidCount { get; set; }
		public PcmAddressVerificationResults Results { get; set; }=new PcmAddressVerificationResults();
	}

	public class PcmPagination {
		public int? CurrentPage { get; set; }
		public int? PerPage { get; set; }
		public int? TotalPages { get; set; }
		public int? TotalResults { get; set; }
	}

	///<summary>One page of a list endpoint.</summary>
	public class PcmPagedResult<T> {
		public List<T> Results { get; set; }=new List<T>();
		public PcmPagination Pagination { get; set; }

		///<summary>True if the vendor said there are more pages after this one.</summary>
		public bool HasMorePages(int pageCur) {
			if(Pagination?.TotalPages!=null) {
				return pageCur<Pagination.TotalPages.Value;
			}
			return false;
		}
	}

	public class PcmApiErrorData {
		public string Message { get; set; }
		public string Property { get; set; }
	}

	public class PcmApiErrorBody {
		public int Code { get; set; }
		public string Message { get; set; }
		public List<PcmApiErrorData> Data { get; set; }=new List<PcmApiErrorData>();
	}

	///<summary>The API's one error shape (_ApiError).</summary>
	public class PcmApiError {
		public PcmApiErrorBody Error { get; set; }
		public string Documentation { get; set; }
	}

	///<summary>What Test connection reports.</summary>
	public class PcmConnectionResult {
		public string Environment;
		public decimal? MoneyOnAccount;
	}
}
