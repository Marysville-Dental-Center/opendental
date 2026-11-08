// Open Dental 26.2 (P262-GLIDEWELL): the JSON this program sends to Glidewell and reads back, stored in glidewellmsg.RawJsonRequest and
// RawJsonResponse. The field names are the ones Open Dental's 26.2 database documents list for those two columns; everything the documents do not
// say (number or text, date and gender formats, the name of the status field) is INFERRED and named in docs/port-notes/P262-GLIDEWELL.md.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenPmsDentalBusiness.VendorDirect.Glidewell {
	///<summary>The register request (glidewellmsg.RawJsonRequest). Field names and order as Open Dental's 26.2 documents list them: CreateVendorOrder,
	///VendorName, OrderId, DoctorId, LicenseNumber, PatientFirstName, PatientLastName, PatientId, PatientGender, PatientDateOfBirth, Notes.</summary>
	public sealed class GlidewellRegisterRequest {
		///<summary>Required, true by default (documents).</summary>
		public bool CreateVendorOrder=true;
		///<summary>Required: "iTero" or "Medit".</summary>
		public string VendorName="";
		///<summary>Required: labcase.LabCaseNum.</summary>
		public long OrderId;
		///<summary>provider.ProvNum of the lab case.</summary>
		public long DoctorId;
		///<summary>The provider's license number (provider.StateLicense).</summary>
		public string LicenseNumber="";
		///<summary>Required.</summary>
		public string PatientFirstName="";
		///<summary>Required.</summary>
		public string PatientLastName="";
		///<summary>patient.PatNum.</summary>
		public long PatientId;
		///<summary>"Male", "Female", "Other" or "Unknown" (INFERRED: the names of this program's gender values).</summary>
		public string PatientGender="";
		///<summary>yyyy-MM-dd, or null when the birthdate is not known (INFERRED format).</summary>
		public string PatientDateOfBirth;
		public string Notes="";

		///<summary>The JSON body, one line, fields in the documented order. Numbers are JSON numbers, CreateVendorOrder a JSON boolean (INFERRED).</summary>
		public string ToJson() {
			JObject jObject=new JObject();
			jObject.Add("CreateVendorOrder",CreateVendorOrder);
			jObject.Add("VendorName",VendorName??"");
			jObject.Add("OrderId",OrderId);
			jObject.Add("DoctorId",DoctorId);
			jObject.Add("LicenseNumber",LicenseNumber??"");
			jObject.Add("PatientFirstName",PatientFirstName??"");
			jObject.Add("PatientLastName",PatientLastName??"");
			jObject.Add("PatientId",PatientId);
			jObject.Add("PatientGender",PatientGender??"");
			jObject.Add("PatientDateOfBirth",PatientDateOfBirth==null ? JValue.CreateNull() : new JValue(PatientDateOfBirth));
			jObject.Add("Notes",Notes??"");
			return jObject.ToString(Formatting.None);
		}
	}

	///<summary>What this program reads from a stored or received response (glidewellmsg.RawJsonResponse). The documents list VendorOrderId,
	///VendorName, VendorPracticeId, VendorDoctorId, DoctorId, patientId, PatientFirstName, PatientLastName, PatientDateOfBirth, Patientgender,
	///ToothRange, Products, Shade, OutboundTrackingNumber, OutboundCarrier and Notes, with inconsistent capitals, so names are matched ignoring
	///case. VendorRxId (from the register response) and the order status are not in that list; the names "VendorRxId", "OrderStatus" and "Status"
	///are INFERRED.</summary>
	public sealed class GlidewellResponse {
		///<summary>False when the text is blank or not a JSON object; every field is then "".</summary>
		public bool IsJsonObject;
		public string VendorRxId="";
		public string VendorOrderId="";
		public string VendorName="";
		public string VendorPracticeId="";
		public string VendorDoctorId="";
		public string OutboundTrackingNumber="";
		public string OutboundCarrier="";
		///<summary>The products, joined with ", " when Glidewell sends a list.</summary>
		public string Products="";
		public string Shade="";
		public string ToothRange="";
		public string Notes="";
		///<summary>The status text as Glidewell sent it, or "".</summary>
		public string StatusText="";

		///<summary>Never throws.</summary>
		public static GlidewellResponse Parse(string json) {
			GlidewellResponse response=new GlidewellResponse();
			if(string.IsNullOrWhiteSpace(json)) {
				return response;
			}
			JObject jObject;
			try {
				JToken jToken=JToken.Parse(json);
				jObject=jToken as JObject;
			}
			catch(Exception) {
				return response;
			}
			if(jObject==null) {
				return response;
			}
			response.IsJsonObject=true;
			response.VendorRxId=GetText(jObject,"VendorRxId");
			response.VendorOrderId=GetText(jObject,"VendorOrderId");
			response.VendorName=GetText(jObject,"VendorName");
			response.VendorPracticeId=GetText(jObject,"VendorPracticeId");
			response.VendorDoctorId=GetText(jObject,"VendorDoctorId");
			response.OutboundTrackingNumber=GetText(jObject,"OutboundTrackingNumber");
			response.OutboundCarrier=GetText(jObject,"OutboundCarrier");
			response.Products=GetText(jObject,"Products");
			response.Shade=GetText(jObject,"Shade");
			response.ToothRange=GetText(jObject,"ToothRange");
			response.Notes=GetText(jObject,"Notes");
			response.StatusText=GetText(jObject,"OrderStatus");
			if(response.StatusText=="") {
				response.StatusText=GetText(jObject,"Status");
			}
			return response;
		}

		///<summary>A top-level property's value as text, matched ignoring case. Arrays are joined with ", " (objects in them as compact JSON); null
		///and missing give "".</summary>
		private static string GetText(JObject jObject,string name) {
			JToken jToken=jObject.GetValue(name,StringComparison.OrdinalIgnoreCase);
			return TokenToText(jToken);
		}

		private static string TokenToText(JToken jToken) {
			if(jToken==null || jToken.Type==JTokenType.Null || jToken.Type==JTokenType.Undefined) {
				return "";
			}
			if(jToken.Type==JTokenType.Array) {
				return string.Join(", ",jToken.Children().Select(x => TokenToText(x)).Where(x => x!=""));
			}
			if(jToken.Type==JTokenType.Object) {
				return jToken.ToString(Formatting.None);
			}
			if(jToken.Type==JTokenType.Date) {
				return ((DateTime)jToken).ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
			}
			return Convert.ToString(((JValue)jToken).Value,CultureInfo.InvariantCulture)??"";
		}
	}

	///<summary>Reading Glidewell's order status into EnumGlidewellOrderStatus.</summary>
	public static class GlidewellOrderStatusText {
		///<summary>True when the text names one of the eight statuses: the member names of EnumGlidewellOrderStatus, ignoring case, spaces, hyphens and
		///underscores ("In Progress", "on_hold", "CREDIT-PENDING"), plus "In Lab", the documents' example, as InProgress and the spelling "Canceled"
		///as Cancelled (both INFERRED). Numbers are not read: neither Glidewell's numbers nor Open Dental's are published.</summary>
		public static bool TryParse(string text,out EnumGlidewellOrderStatus orderStatus) {
			orderStatus=EnumGlidewellOrderStatus.Initialized;
			if(string.IsNullOrWhiteSpace(text)) {
				return false;
			}
			string key=new string(text.Where(x => char.IsLetter(x)).ToArray()).ToLowerInvariant();
			if(key=="") {
				return false;
			}
			if(key=="inlab") {
				orderStatus=EnumGlidewellOrderStatus.InProgress;
				return true;
			}
			if(key=="canceled") {
				orderStatus=EnumGlidewellOrderStatus.Cancelled;
				return true;
			}
			foreach(EnumGlidewellOrderStatus status in Enum.GetValues(typeof(EnumGlidewellOrderStatus))) {
				if(status.ToString().ToLowerInvariant()==key) {
					orderStatus=status;
					return true;
				}
			}
			return false;
		}

		///<summary>The status for the user.</summary>
		public static string GetDisplayText(EnumGlidewellOrderStatus orderStatus) {
			switch(orderStatus) {
				case EnumGlidewellOrderStatus.Initialized:
					return Lans.g("Glidewell","Registered, waiting for the scan");
				case EnumGlidewellOrderStatus.Received:
					return Lans.g("Glidewell","Received by the lab");
				case EnumGlidewellOrderStatus.InProgress:
					return Lans.g("Glidewell","In progress at the lab");
				case EnumGlidewellOrderStatus.OnHold:
					return Lans.g("Glidewell","On hold, the lab needs more information");
				case EnumGlidewellOrderStatus.Shipped:
					return Lans.g("Glidewell","Shipped");
				case EnumGlidewellOrderStatus.Cancelled:
					return Lans.g("Glidewell","Cancelled");
				case EnumGlidewellOrderStatus.CreditPending:
					return Lans.g("Glidewell","Returned, credit pending");
				case EnumGlidewellOrderStatus.Credited:
					return Lans.g("Glidewell","Returned, credited");
				default:
					return Lans.g("Glidewell","Status")+" "+((int)orderStatus).ToString(CultureInfo.InvariantCulture);
			}
		}
	}
}
