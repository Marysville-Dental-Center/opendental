// The register request and the stored answers (glidewellmsg.RawJsonRequest, RawJsonResponse): the field names Open Dental's 26.2 database
// documents list, and the INFERRED formats. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.Glidewell;

namespace P262GlidewellTests {
	[TestClass]
	public class GlidewellJsonTests {
		private static LabCase NewLabCase() {
			return new LabCase { LabCaseNum=4711,PatNum=88,ProvNum=5,Instructions="Full contour\r\nCheck occlusion" };
		}

		private static Patient NewPatient() {
			return new Patient { PatNum=88,FName=" Synthetic ",LName="Testpatient",Gender=PatientGender.Female,Birthdate=new DateTime(1980,2,3) };
		}

		///<summary>Parses without turning date text into dates, so the text is compared as sent.</summary>
		public static JObject Parse(string json) {
			using(JsonTextReader jsonTextReader=new JsonTextReader(new StringReader(json))) {
				jsonTextReader.DateParseHandling=DateParseHandling.None;
				return JObject.Load(jsonTextReader);
			}
		}

		[TestMethod]
		public void Request_HasTheDocumentedFieldsInTheDocumentedOrder() {
			GlidewellRegisterRequest request=GlidewellLabCases.BuildRegisterRequest(NewLabCase(),NewPatient(),new Provider { ProvNum=5,StateLicense="AB12" },
				GlidewellScanner.ITero,new List<LabCaseProduct>());
			JObject jObject=Parse(request.ToJson());
			CollectionAssert.AreEqual(new[] { "CreateVendorOrder","VendorName","OrderId","DoctorId","LicenseNumber","PatientFirstName","PatientLastName",
				"PatientId","PatientGender","PatientDateOfBirth","Notes" },jObject.Properties().Select(x => x.Name).ToArray());
			Assert.AreEqual(true,(bool)jObject["CreateVendorOrder"]);
			Assert.AreEqual("iTero",(string)jObject["VendorName"]);
			Assert.AreEqual(4711L,(long)jObject["OrderId"],"OrderId is the lab case number.");
			Assert.AreEqual(5L,(long)jObject["DoctorId"],"DoctorId is the provider number.");
			Assert.AreEqual("AB12",(string)jObject["LicenseNumber"]);
			Assert.AreEqual("Synthetic",(string)jObject["PatientFirstName"],"Names are trimmed.");
			Assert.AreEqual("Testpatient",(string)jObject["PatientLastName"]);
			Assert.AreEqual(88L,(long)jObject["PatientId"]);
			Assert.AreEqual("Female",(string)jObject["PatientGender"]);
			Assert.AreEqual("1980-02-03",jObject["PatientDateOfBirth"].ToString());
			Assert.AreEqual("Full contour\nCheck occlusion",(string)jObject["Notes"]);
		}

		[TestMethod]
		public void Request_MeditScannerAndUnknownBirthdate() {
			Patient patient=NewPatient();
			patient.Birthdate=DateTime.MinValue;
			patient.Gender=PatientGender.Unknown;
			GlidewellRegisterRequest request=GlidewellLabCases.BuildRegisterRequest(NewLabCase(),patient,null,GlidewellScanner.Medit,null);
			JObject jObject=Parse(request.ToJson());
			Assert.AreEqual("Medit",(string)jObject["VendorName"]);
			Assert.AreEqual(JTokenType.Null,jObject["PatientDateOfBirth"].Type,"An unknown birthdate is sent as null, not 0001-01-01.");
			Assert.AreEqual("Unknown",(string)jObject["PatientGender"]);
			Assert.AreEqual("",(string)jObject["LicenseNumber"],"No provider: blank license.");
		}

		[TestMethod]
		public void Request_GenderTexts() {
			Assert.AreEqual("Male",GlidewellLabCases.GetGenderText(PatientGender.Male));
			Assert.AreEqual("Female",GlidewellLabCases.GetGenderText(PatientGender.Female));
			Assert.AreEqual("Other",GlidewellLabCases.GetGenderText(PatientGender.Other));
			Assert.AreEqual("Unknown",GlidewellLabCases.GetGenderText(PatientGender.Unknown));
		}

		[TestMethod]
		public void Request_SpecialCharactersAreEscaped() {
			LabCase labCase=NewLabCase();
			labCase.Instructions="Quote \" backslash \\ tab \t done";
			Patient patient=NewPatient();
			patient.LName="O'Brien";
			string json=GlidewellLabCases.BuildRegisterRequest(labCase,patient,null,GlidewellScanner.ITero,null).ToJson();
			JObject jObject=Parse(json);
			Assert.AreEqual("Quote \" backslash \\ tab \t done",(string)jObject["Notes"]);
			Assert.AreEqual("O'Brien",(string)jObject["PatientLastName"]);
			Assert.IsFalse(json.Contains("\n"),"One line.");
		}

		[TestMethod]
		public void Notes_ListTheProductsAfterTheInstructions() {
			List<LabCaseProduct> listProducts=new List<LabCaseProduct> {
				new LabCaseProduct { Material="BruxZir",Shade="A1",ToothRange="8-9",Notes="Full\r\ncontour" },
				new LabCaseProduct { Material="",Shade="",ToothRange="",Notes="" },
				new LabCaseProduct { Notes="Night guard, hard" },
			};
			Assert.AreEqual("Crown\nProducts:\n- BruxZir, shade A1, teeth 8-9: Full contour\n- Night guard, hard",
				GlidewellLabCases.BuildNotes("Crown",listProducts));
			Assert.AreEqual("Products:\n- Night guard, hard",GlidewellLabCases.BuildNotes("  ",listProducts.Skip(2).ToList()));
			Assert.AreEqual("Only instructions",GlidewellLabCases.BuildNotes("Only instructions\r\n",new List<LabCaseProduct>()));
			Assert.AreEqual("",GlidewellLabCases.BuildNotes(null,null));
		}

		[TestMethod]
		public void Response_FieldsAreReadIgnoringCase() {
			string json="{\"vendorRxId\":\"RX-1\",\"VendorOrderId\":12345,\"VENDORNAME\":\"Medit\",\"OutboundTrackingNumber\":\"1Z999\",\"OutboundCarrier\":\"UPS\","
				+"\"Products\":[\"Crown\",\"Night Guard\"],\"Shade\":\"A2\",\"ToothRange\":\"3\",\"Notes\":null,\"Patientgender\":\"F\",\"Status\":\"Shipped\"}";
			GlidewellResponse response=GlidewellResponse.Parse(json);
			Assert.IsTrue(response.IsJsonObject);
			Assert.AreEqual("RX-1",response.VendorRxId);
			Assert.AreEqual("12345",response.VendorOrderId,"Numbers are read as text.");
			Assert.AreEqual("Medit",response.VendorName);
			Assert.AreEqual("1Z999",response.OutboundTrackingNumber);
			Assert.AreEqual("UPS",response.OutboundCarrier);
			Assert.AreEqual("Crown, Night Guard",response.Products);
			Assert.AreEqual("A2",response.Shade);
			Assert.AreEqual("3",response.ToothRange);
			Assert.AreEqual("",response.Notes,"null reads as blank.");
			Assert.AreEqual("Shipped",response.StatusText,"Status is read when there is no OrderStatus.");
			Assert.AreEqual("Received",GlidewellResponse.Parse("{\"OrderStatus\":\"Received\",\"Status\":\"Shipped\"}").StatusText,"OrderStatus wins.");
		}

		[TestMethod]
		public void Response_NotAnObjectReadsAsBlank() {
			foreach(string text in new[] { null,"","   ","not json","[1,2]","\"text\"","{broken" }) {
				GlidewellResponse response=GlidewellResponse.Parse(text);
				Assert.IsFalse(response.IsJsonObject,text??"null");
				Assert.AreEqual("",response.VendorRxId);
				Assert.AreEqual("",response.StatusText);
			}
		}

		[TestMethod]
		public void OrderStatus_NamesAreRead_NumbersAreNot() {
			Dictionary<string,EnumGlidewellOrderStatus> dict=new Dictionary<string,EnumGlidewellOrderStatus> {
				{ "Initialized",EnumGlidewellOrderStatus.Initialized },
				{ "received",EnumGlidewellOrderStatus.Received },
				{ "In Progress",EnumGlidewellOrderStatus.InProgress },
				{ "In Lab",EnumGlidewellOrderStatus.InProgress },
				{ "on_hold",EnumGlidewellOrderStatus.OnHold },
				{ "SHIPPED",EnumGlidewellOrderStatus.Shipped },
				{ "Cancelled",EnumGlidewellOrderStatus.Cancelled },
				{ "Canceled",EnumGlidewellOrderStatus.Cancelled },
				{ "Credit-Pending",EnumGlidewellOrderStatus.CreditPending },
				{ "Credited",EnumGlidewellOrderStatus.Credited },
			};
			foreach(KeyValuePair<string,EnumGlidewellOrderStatus> pair in dict) {
				Assert.IsTrue(GlidewellOrderStatusText.TryParse(pair.Key,out EnumGlidewellOrderStatus status),pair.Key);
				Assert.AreEqual(pair.Value,status,pair.Key);
			}
			foreach(string text in new[] { null,"","2","7","Lost","Delivered" }) {
				Assert.IsFalse(GlidewellOrderStatusText.TryParse(text,out EnumGlidewellOrderStatus _),text??"null");
			}
		}

		[TestMethod]
		public void OrderStatus_EveryValueHasItsOwnDisplayText() {
			List<string> listTexts=Enum.GetValues(typeof(EnumGlidewellOrderStatus)).Cast<EnumGlidewellOrderStatus>()
				.Select(x => GlidewellOrderStatusText.GetDisplayText(x)).ToList();
			Assert.AreEqual(8,listTexts.Count);
			Assert.AreEqual(8,listTexts.Distinct().Count());
			Assert.IsFalse(listTexts.Any(x => x.StartsWith("Status ")),"No value falls through to the number.");
		}

		[TestMethod]
		public void Credentials_ToStringNeverShowsTheKey() {
			GlidewellCredentials credentials=new GlidewellCredentials(" 10-0000001 ","synthetic-secret");
			Assert.AreEqual("10-0000001",credentials.AccountNumber);
			Assert.IsFalse(credentials.ToString().Contains("synthetic-secret"));
		}

		[TestMethod]
		public void BlockedClient_SendsNothing() {
			GlidewellClientBlocked client=new GlidewellClientBlocked();
			Assert.IsFalse(client.IsAvailable);
			Assert.AreNotEqual("",client.UnavailableReason);
			Assert.AreEqual(GlidewellCallOutcome.Blocked,client.Register(new GlidewellCredentials("1","k"),"{}").Outcome);
			Assert.AreEqual(GlidewellCallOutcome.Blocked,client.GetStatus(new GlidewellCredentials("1","k"),new GlidewellMsg()).Outcome);
			GlidewellClients.Current=null;
			Assert.IsInstanceOfType(GlidewellClients.Current,typeof(GlidewellClientBlocked),"The program's client is the blocked one.");
		}

		[TestMethod]
		public void ProductSummary() {
			Assert.AreEqual("",LabCaseProducts.GetSummary(null));
			Assert.AreEqual("",LabCaseProducts.GetSummary(new LabCaseProduct()));
			Assert.AreEqual("Zirconia, shade B1",LabCaseProducts.GetSummary(new LabCaseProduct { Material=" Zirconia ",Shade="B1" }));
			Assert.AreEqual("teeth 30: Long notes",LabCaseProducts.GetSummary(new LabCaseProduct { ToothRange="30",Notes="Long\nnotes" }));
		}
	}
}
