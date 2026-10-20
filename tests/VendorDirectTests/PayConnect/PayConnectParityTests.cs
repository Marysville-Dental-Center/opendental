// Tests for the Open Dental 24.4-25.3 PayConnect features on top of the direct PayConnect 2 library (backlog package V-PC):
//   v25_1-account-payconnect2-receipt-cardholder, v25_1-account-payconnect2-terminal-memory,
//   v25_2-account-payconnect-saved-card-field, v25_3-account-payconnect-error-log.
// No network and no database: requests go to a fake handler, and log lines to a list (or to a temporary Logger folder).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace VendorDirectTests.PayConnectV2 {
	[TestClass]
	public class PayConnectParityTests {
		private const string API_KEY="test-integrator-key-0003";
		private const string SECRET="test-merchant-secret-0003";
		private const string TOKEN="TKN9f8e7d6c5b4a1234";
		///<summary>Not a real CVV: a marker that can't turn up in a log line by chance.</summary>
		private const string CVV="cvv-marker-q7";
		private List<string> _listLogLines;

		[TestInitialize]
		public void TestInitialize() {
			PayConnect2.Mock=null;
			PayConnect2.HttpHandlerForTests=null;
			_listLogLines=new List<string>();
			PayConnectErrorLog.ActionWriteLineForTests=line => _listLogLines.Add(line);
		}

		[TestCleanup]
		public void TestCleanup() {
			PayConnect2.Mock=null;
			PayConnect2.HttpHandlerForTests=null;
			PayConnectErrorLog.ActionWriteLineForTests=null;
		}

		private static FakeHttpHandler UseFakeHandler() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			PayConnect2.HttpHandlerForTests=fakeHttpHandler;
			return fakeHttpHandler;
		}

		private static PayConnect2.CreateTransactionRequest MakeTokenSale(int amountCents) {
			PayConnect2.CreateTransactionRequest request=new PayConnect2.CreateTransactionRequest();
			request.Frequency=PayConnect2.TransactionFrequency.OneTime;
			request.TransType=PayConnect2.TransactionType.Sale;
			request.Amount=amountCents;
			request.CardToken=TOKEN;
			request.Expiry="1230";
			request.CardHolder="Pat Q Holder";
			request.Cvv=CVV;
			return request;
		}

		private static Dictionary<string,string> ParseFields(string line) {
			Dictionary<string,string> dictFields=new Dictionary<string,string>();
			foreach(string field in line.Split('\t')) {
				int index=field.IndexOf('=');
				if(index>0) {
					dictFields[field.Substring(0,index)]=field.Substring(index+1);
				}
			}
			return dictFields;
		}

		#region Terminal memory (25.1)
		private static List<PayTerminal> MakeTerminals() {
			return new List<PayTerminal> {
				new PayTerminal { Name="Front Desk",TerminalID="T-100" },
				new PayTerminal { Name="Checkout",TerminalID="T-200" },
				new PayTerminal { Name="",TerminalID="T-300" },
			};
		}

		[TestMethod]
		public void TerminalMemory_SelectsTheRememberedTerminal() {
			Assert.AreEqual(1,PayTerminals.GetIndexToSelect(MakeTerminals(),"T-200"));
			Assert.AreEqual(2,PayTerminals.GetIndexToSelect(MakeTerminals()," T-300 "),"Stored values are compared trimmed.");
		}

		[TestMethod]
		public void TerminalMemory_FallsBackToTheFirstTerminal() {
			Assert.AreEqual(0,PayTerminals.GetIndexToSelect(MakeTerminals(),""),"Nothing remembered yet.");
			Assert.AreEqual(0,PayTerminals.GetIndexToSelect(MakeTerminals(),null));
			Assert.AreEqual(0,PayTerminals.GetIndexToSelect(MakeTerminals(),"T-999"),"Another clinic's terminal, or a deleted one.");
			Assert.AreEqual(0,PayTerminals.GetIndexToSelect(MakeTerminals(),"t-200"),"Terminal IDs are matched exactly.");
		}

		[TestMethod]
		public void TerminalMemory_NoTerminalsSelectsNothing() {
			Assert.AreEqual(-1,PayTerminals.GetIndexToSelect(new List<PayTerminal>(),"T-100"));
			Assert.AreEqual(-1,PayTerminals.GetIndexToSelect(null,"T-100"));
		}
		#endregion Terminal memory (25.1)

		#region Saved-card field (25.2)
		[TestMethod]
		public void SavedCardField_ShowsTheMaskedNumber() {
			CreditCard creditCard=new CreditCard { CCNumberMasked="XXXXXXXXXXXX1234",PayConnectToken=TOKEN };
			Assert.AreEqual("XXXXXXXXXXXX1234",PayConnect2.GetSavedCardDisplayText(creditCard));
		}

		[TestMethod]
		public void SavedCardField_NeverShowsMoreThanTheLast4() {
			CreditCard creditCard=new CreditCard { CCNumberMasked="4111111111111234",PayConnectToken=TOKEN };
			string text=PayConnect2.GetSavedCardDisplayText(creditCard);
			Assert.AreEqual("XXXXXXXXXXXX1234",text);
			Assert.IsFalse(text.Contains("4111"));
			creditCard.CCNumberMasked="1234";
			Assert.AreEqual("XXXXXXXXXXXX1234",PayConnect2.GetSavedCardDisplayText(creditCard),"A bare last 4 is padded like a masked number.");
		}

		[TestMethod]
		public void SavedCardField_NoMaskedNumberSaysSavedCard() {
			Assert.AreEqual("(saved card)",PayConnect2.GetSavedCardDisplayText(new CreditCard { CCNumberMasked="",PayConnectToken=TOKEN }));
			Assert.AreEqual("(saved card)",PayConnect2.GetSavedCardDisplayText(new CreditCard { CCNumberMasked="X12",PayConnectToken=TOKEN }));
		}

		[TestMethod]
		public void SavedCardField_VisibleOnlyForASavedCardOnTheWebService() {
			CreditCard creditCard=new CreditCard { CCNumberMasked="XXXXXXXXXXXX1234",PayConnectToken=TOKEN };
			Assert.IsTrue(PayConnect2.IsSavedCardFieldVisible(creditCard,isTerminal:false));
			Assert.IsFalse(PayConnect2.IsSavedCardFieldVisible(creditCard,isTerminal:true),"A terminal payment reads its own card.");
			Assert.IsFalse(PayConnect2.IsSavedCardFieldVisible(null,isTerminal:false),"A new card is entered on the hosted page.");
			Assert.IsFalse(PayConnect2.IsSavedCardFieldVisible(new CreditCard { CCNumberMasked="XXXXXXXXXXXX1234" },isTerminal:false),
				"A card with no PayConnect token is not charged by PayConnect.");
		}
		#endregion Saved-card field (25.2)

		#region Cardholder (25.1)
		[TestMethod]
		public void CleanCardHolderName_TrimsAndCollapses() {
			Assert.AreEqual("Jane Q Public",PayConnect2.CleanCardHolderName("  Jane \t Q\r\n Public "));
			Assert.AreEqual("",PayConnect2.CleanCardHolderName(null));
			Assert.AreEqual("",PayConnect2.CleanCardHolderName("   "));
			Assert.AreEqual(255,PayConnect2.CleanCardHolderName(new string('A',300)).Length,"The column is varchar(255).");
		}

		[TestMethod]
		public void CleanCardHolderName_RefusesACardNumber() {
			Assert.AreEqual("",PayConnect2.CleanCardHolderName("4111 1111 1111 1111"));
			Assert.AreEqual("",PayConnect2.CleanCardHolderName("JANE 4111-1111-1111-1111"));
			Assert.AreEqual("Jane Public 3rd",PayConnect2.CleanCardHolderName("Jane Public 3rd"));
		}

		[TestMethod]
		public void SetCardHolderNameFromVendor_OnlyForAnEnteredCard() {
			CreditCard creditCard=new CreditCard { CardHolderName="" };
			Assert.IsFalse(PayConnect2.SetCardHolderNameFromVendor(creditCard,"Jane Public",wasCardEntered:false),
				"A token charge only echoes the name this program sent.");
			Assert.AreEqual("",creditCard.CardHolderName);
			Assert.IsTrue(PayConnect2.SetCardHolderNameFromVendor(creditCard,"  Jane Public ",wasCardEntered:true));
			Assert.AreEqual("Jane Public",creditCard.CardHolderName);
			Assert.IsFalse(PayConnect2.SetCardHolderNameFromVendor(creditCard,"",wasCardEntered:true),"No name returned: the saved one stays.");
			Assert.AreEqual("Jane Public",creditCard.CardHolderName);
			Assert.IsFalse(PayConnect2.SetCardHolderNameFromVendor(null,"Jane Public",wasCardEntered:true));
		}

		[TestMethod]
		public void GetCardHolderForRequest_PrefersTheNameOnTheCard() {
			Patient patient=new Patient { FName="Pat",LName="Patient",MiddleI="" };
			Assert.AreEqual("Jane Holder",PayConnect2.GetCardHolderForRequest(patient,"Jane Holder"));
			Assert.AreEqual(patient.GetNameFLnoPref(),PayConnect2.GetCardHolderForRequest(patient,""),"No saved name: the patient's, as before 25.1.");
			Assert.AreEqual("",PayConnect2.GetCardHolderForRequest(null,""));
		}

		[TestMethod]
		public void GetCardHolderForReceipt_ResponseThenSavedCard() {
			CreditCard creditCard=new CreditCard { CardHolderName="Saved Name" };
			Assert.AreEqual("From Response",PayConnect2.GetCardHolderForReceipt("From Response",creditCard));
			Assert.AreEqual("Saved Name",PayConnect2.GetCardHolderForReceipt("",creditCard));
			Assert.AreEqual("",PayConnect2.GetCardHolderForReceipt(null,null));
		}

		[TestMethod]
		public void ApiResponse_CarriesTheCardholderForTheReceipt() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"{\"referenceId\":\"R-77\",\"transactionId\":77,\"status\":\"Processed\",\"amount\":1500,"
				+"\"type\":\"Sale\",\"frequency\":\"OneTime\",\"paymentMethod\":{\"cardPaymentMethod\":{\"cardToken\":\""+TOKEN+"\","
				+"\"cardLast4Digits\":\"1234\",\"expiry\":\"1230\",\"network\":\"Visa\",\"cardHolder\":\"Jane Q Holder\"}}}");
			PayConnect2.PayConnect2Response response=PayConnect2.PostCreateTransaction(MakeTokenSale(1500),
				PayConnect2.MakeCredentials(API_KEY,SECRET,true));
			Assert.AreEqual(PayConnect2.ResponseType.CreateTransaction,response.ResponseType);
			PayConnectResponse payConnectResponse=PayConnect2.ApiResponseToPayConnectResponse(response);
			Assert.AreEqual("Jane Q Holder",payConnectResponse.CardHolder);
			Assert.AreEqual(0,_listLogLines.Count,"An approval is not an error.");
		}
		#endregion Cardholder (25.1)

		#region Error log (25.3)
		[TestMethod]
		public void ErrorLog_DeclineWritesOneLineWithTheContext() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.PaymentRequired,"{\"errorType\":\"RESPONSE\",\"error\":\"Card declined by issuer\"}");
			PayConnect2.PayConnect2Response response=PayConnect2.PostCreateTransaction(MakeTokenSale(1234),PayConnect2.MakeCredentials(API_KEY,SECRET,true),
				clinicNum:3,patNum:42);
			Assert.AreEqual(PayConnect2.ResponseType.Error,response.ResponseType);
			Assert.AreEqual(1,_listLogLines.Count);
			Dictionary<string,string> dictFields=ParseFields(_listLogLines[0]);
			Assert.IsTrue(_listLogLines[0].StartsWith("ERROR\t"));
			Assert.AreEqual("3",dictFields["Clinic"]);
			Assert.AreEqual("42",dictFields["PatNum"]);
			Assert.AreEqual("sale",dictFields["Operation"]);
			Assert.AreEqual("12.34",dictFields["Amount"]);
			Assert.AreEqual("402",dictFields["HTTP"]);
			Assert.AreEqual("Response",dictFields["Outcome"]);
			Assert.AreEqual("Vendor",dictFields["Kind"]);
			Assert.AreEqual("",dictFields["Code"],"PayConnect sent no code.");
			Assert.AreEqual("1234",dictFields["Card"],"Only the last 4 of the token.");
			Assert.AreEqual(response.CorrelationId,dictFields["Invoice"]);
			Assert.IsTrue(dictFields["Invoice"].StartsWith(PayConnect2.CorrelationIdPrefix));
			Assert.AreEqual("Card declined by issuer",dictFields["Message"]);
			Assert.AreEqual(ODEnvironment.MachineName,dictFields["Workstation"]);
			Assert.IsTrue(dictFields.ContainsKey("User"));
		}

		[TestMethod]
		public void ErrorLog_NeverHoldsTokensKeysCvvOrCardNumbers() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			//A vendor message that echoes a card number must not reach the log.
			fakeHttpHandler.Enqueue(HttpStatusCode.PaymentRequired,"{\"error\":\"Declined for card 4111111111111111\"}");
			PayConnect2.PostCreateTransaction(MakeTokenSale(500),PayConnect2.MakeCredentials(API_KEY,SECRET,false),clinicNum:1,patNum:7);
			Assert.AreEqual(1,_listLogLines.Count);
			string line=_listLogLines[0];
			Assert.IsFalse(line.Contains(TOKEN),"The token.");
			Assert.IsFalse(line.Contains(API_KEY),"The API key.");
			Assert.IsFalse(line.Contains(SECRET),"The Secret.");
			Assert.IsFalse(line.Contains(CVV),"The CVV.");
			Assert.IsFalse(line.Contains("4111111111111111"),"A card number.");
			Assert.IsTrue(line.Contains("[card number removed]"));
			Assert.IsFalse(line.Contains("Pat Q Holder"),"The request body is never logged.");
		}

		[TestMethod]
		public void ErrorLog_A2xxDeclineIsLogged() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.OK,"{\"referenceId\":\"R-78\",\"transactionId\":78,\"status\":\"Declined\",\"amount\":800,"
				+"\"type\":\"Sale\",\"frequency\":\"Recurring\"}");
			PayConnect2.CreateTransactionRequest request=MakeTokenSale(800);
			request.Frequency=PayConnect2.TransactionFrequency.Recurring;
			PayConnect2.PayConnect2Response response=PayConnect2.PostCreateTransaction(request,PayConnect2.MakeCredentials(API_KEY,SECRET,true),
				clinicNum:4,patNum:12);
			Assert.AreEqual(PayConnect2.ResponseType.CreateTransaction,response.ResponseType);
			Assert.AreEqual("Declined",PayConnect2.GetCreateStatusNotApproved(response));
			Assert.AreEqual(1,_listLogLines.Count);
			Dictionary<string,string> dictFields=ParseFields(_listLogLines[0]);
			Assert.AreEqual("recurring sale",dictFields["Operation"]);
			Assert.AreEqual("NotApproved",dictFields["Kind"]);
			Assert.AreEqual("Declined",dictFields["Code"],"The returned status is the vendor code.");
			Assert.AreEqual("200",dictFields["HTTP"]);
			StringAssert.Contains(dictFields["Message"],"Declined");
		}

		[TestMethod]
		public void ErrorLog_RejectedSecretAndValidationAreLogged() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.Unauthorized,"{\"error\":{\"message\":\"Invalid secret\"}}");
			fakeHttpHandler.Enqueue(HttpStatusCode.BadRequest,"{\"errorType\":\"VALIDATION\",\"error\":[{\"param\":\"expiry\",\"message\":\"is invalid\","
				+"\"value\":\"1299\"}]}");
			PayConnect2.ApiCredentials apiCredentials=PayConnect2.MakeCredentials(API_KEY,SECRET,true);
			PayConnect2.PostCreateTransaction(MakeTokenSale(100),apiCredentials);
			PayConnect2.PostCreateTransaction(MakeTokenSale(100),apiCredentials);
			Assert.AreEqual(2,_listLogLines.Count);
			Assert.AreEqual("SecretRejected",ParseFields(_listLogLines[0])["Kind"]);
			Assert.AreEqual("401",ParseFields(_listLogLines[0])["HTTP"]);
			Dictionary<string,string> dictValidation=ParseFields(_listLogLines[1]);
			Assert.AreEqual("Validation/VALIDATION",dictValidation["Kind"]);
			StringAssert.Contains(dictValidation["Message"],"expiry: is invalid");
			Assert.IsFalse(_listLogLines[1].Contains("1299"),"The rejected value is never logged.");
		}

		[TestMethod]
		public void ErrorLog_CarriesPayConnectsOwnCode() {
			PayConnect2.ErrorResponse errorResponse=PayConnect2.ParseErrorResponse(HttpStatusCode.NotFound,"{\"error\":{\"message\":\"x\",\"code\":123}}");
			Assert.AreEqual("123",errorResponse.VendorCode);
			Assert.AreEqual("x",errorResponse.VendorMessage);
			Assert.AreEqual(PayConnect2.PayConnectErrorKind.Vendor,errorResponse.Kind,"Our classification stays separate.");
			PayConnect2.PayConnect2Response response=new PayConnect2.PayConnect2Response { ResponseType=PayConnect2.ResponseType.Error,
				httpStatusCode=HttpStatusCode.NotFound,ErrorResponse=errorResponse };
			string line=PayConnectErrorLog.FormatLine(PayConnect2.MakeErrorLogEntry(response,"sale",1,2,100,TOKEN,"OPMS-1"));
			StringAssert.Contains(line,"Code=123");
			Dictionary<string,string> dictFields=ParseFields(line);
			Assert.AreEqual("123",dictFields["Code"]);
			Assert.AreEqual("Vendor",dictFields["Kind"]);
			Assert.AreEqual("404",dictFields["HTTP"]);
			Assert.AreEqual("x",dictFields["Message"]);
		}

		[TestMethod]
		public void ErrorLog_RejectedKeyOrSecretKeepsTheVendorCodeAndMessage() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue(HttpStatusCode.Unauthorized,"{\"error\":{\"message\":\"Invalid secret\",\"code\":\"E401\"}}");
			fakeHttpHandler.Enqueue(HttpStatusCode.Forbidden,"{\"error\":{\"message\":\"Key not allowed\",\"code\":4031}}");
			fakeHttpHandler.Enqueue(HttpStatusCode.Unauthorized,"{\"error\":\"Authorization field missing\"}");
			fakeHttpHandler.Enqueue(HttpStatusCode.BadRequest,"{\"errorType\":\"VALIDATION\",\"error\":{\"code\":40,\"message\":\"Bad request\","
				+"\"properties\":[{\"param\":\"amount\",\"message\":\"is required\"}]}}");
			PayConnect2.ApiCredentials apiCredentials=PayConnect2.MakeCredentials(API_KEY,SECRET,true);
			for(int i=0;i<4;i++) {
				PayConnect2.PostCreateTransaction(MakeTokenSale(100),apiCredentials);
			}
			Assert.AreEqual(4,_listLogLines.Count);
			Dictionary<string,string> dictSecret=ParseFields(_listLogLines[0]);
			Assert.AreEqual("E401",dictSecret["Code"]);
			Assert.AreEqual("SecretRejected",dictSecret["Kind"]);
			StringAssert.Contains(dictSecret["Message"],"Invalid secret");
			Dictionary<string,string> dictForbidden=ParseFields(_listLogLines[1]);
			Assert.AreEqual("4031",dictForbidden["Code"]);
			Assert.AreEqual("ApiKeyRejected",dictForbidden["Kind"]);
			StringAssert.Contains(dictForbidden["Message"],"Key not allowed");
			Dictionary<string,string> dictKey=ParseFields(_listLogLines[2]);
			Assert.AreEqual("",dictKey["Code"],"A plain-string error has no code.");
			StringAssert.Contains(dictKey["Message"],"Authorization field missing");
			Dictionary<string,string> dictValidation=ParseFields(_listLogLines[3]);
			Assert.AreEqual("40",dictValidation["Code"]);
			Assert.AreEqual("Validation/VALIDATION",dictValidation["Kind"]);
			StringAssert.Contains(dictValidation["Message"],"amount: is required");
			StringAssert.Contains(dictValidation["Message"],"Bad request");
			foreach(string line in _listLogLines) {
				Assert.IsFalse(line.Contains(API_KEY) || line.Contains(SECRET) || line.Contains(TOKEN));
			}
		}

		[TestMethod]
		public void ErrorLog_NotSentIsLogged() {
			FakeHttpHandler fakeHttpHandler=UseFakeHandler();
			fakeHttpHandler.Enqueue((request,ct) => throw new System.Net.Http.HttpRequestException("Simulated.",
				new WebException("Simulated.",WebExceptionStatus.ConnectFailure)));
			PayConnect2.PayConnect2Response response=PayConnect2.PostCreateTransaction(MakeTokenSale(2500),PayConnect2.MakeCredentials(API_KEY,SECRET,true));
			Assert.AreEqual(PayConnect2.PayConnectRequestOutcome.NotSent,response.Outcome);
			Assert.AreEqual(1,_listLogLines.Count);
			Dictionary<string,string> dictFields=ParseFields(_listLogLines[0]);
			Assert.AreEqual("NotSent",dictFields["Outcome"]);
			Assert.AreEqual("Network",dictFields["Kind"]);
			Assert.AreEqual("",dictFields["HTTP"],"No answer, no status.");
			Assert.AreEqual("25.00",dictFields["Amount"]);
		}

		[TestMethod]
		public void ErrorLog_TestConnectionWithoutSecretIsLogged() {
			PayConnect2.TestConnection(API_KEY,"",true);
			Assert.AreEqual(1,_listLogLines.Count);
			Dictionary<string,string> dictFields=ParseFields(_listLogLines[0]);
			Assert.AreEqual("test connection",dictFields["Operation"]);
			Assert.AreEqual("NotConfigured",dictFields["Kind"]);
			Assert.IsFalse(_listLogLines[0].Contains(API_KEY));
		}

		[TestMethod]
		public void ErrorLog_MockedErrorsAreLoggedAndSuccessesAreNot() {
			PayConnect2.Mock=new PayConnect2.MockPayConnect2();
			PayConnect2.Mock.Response=new PayConnect2.PayConnect2Response { ResponseType=PayConnect2.ResponseType.GetStatus,
				GetStatusResponse=new PayConnect2.GetStatusResponse() };
			PayConnect2.ApiCredentials apiCredentials=PayConnect2.MakeCredentials(API_KEY,SECRET,true);
			PayConnect2.PostCreateTransaction(MakeTokenSale(100),apiCredentials);
			Assert.AreEqual(0,_listLogLines.Count);
			PayConnect2.Mock.Response=new PayConnect2.PayConnect2Response { ResponseType=PayConnect2.ResponseType.Error,
				ErrorResponse=new PayConnect2.ErrorResponse { Message="Terminal busy",Kind=PayConnect2.PayConnectErrorKind.Vendor } };
			PayConnect2.GetTerminals(apiCredentials,clinicNum:5);
			Assert.AreEqual(1,_listLogLines.Count);
			Assert.AreEqual("terminal list",ParseFields(_listLogLines[0])["Operation"]);
			Assert.AreEqual("5",ParseFields(_listLogLines[0])["Clinic"]);
		}

		[TestMethod]
		public void ErrorLog_OperationNames() {
			Assert.AreEqual("sale",PayConnect2.GetOperationName(PayConnect2.TransactionType.Sale,PayConnect2.TransactionFrequency.OneTime));
			Assert.AreEqual("authorize",PayConnect2.GetOperationName(PayConnect2.TransactionType.AuthorizeOnly,PayConnect2.TransactionFrequency.OneTime));
			Assert.AreEqual("refund",PayConnect2.GetOperationName(PayConnect2.TransactionType.Refund,PayConnect2.TransactionFrequency.OneTime));
			Assert.AreEqual("recurring sale",PayConnect2.GetOperationName(PayConnect2.TransactionType.Sale,PayConnect2.TransactionFrequency.Recurring));
		}

		[TestMethod]
		public void ErrorLog_HostedFormAndExceptionLines() {
			PayConnect2.LogHostedFormError(2,9,isAddingCard:true,"Card\tnumber\r\ninvalid");
			PayConnect2.LogException("terminal sale",2,9,1000,"OPMS-abc",new InvalidOperationException("boom 5500000000000004"));
			Assert.AreEqual(2,_listLogLines.Count);
			Dictionary<string,string> dictForm=ParseFields(_listLogLines[0]);
			Assert.AreEqual("card token form",dictForm["Operation"]);
			Assert.AreEqual("HostedForm",dictForm["Kind"]);
			Assert.AreEqual("Card number  invalid",dictForm["Message"],"Tabs and line breaks never split a line.");
			Assert.AreEqual(14,_listLogLines[0].Split('\t').Length,"Fixed field count.");
			Dictionary<string,string> dictException=ParseFields(_listLogLines[1]);
			Assert.AreEqual("Exception",dictException["Kind"]);
			Assert.AreEqual("OPMS-abc",dictException["Invoice"]);
			Assert.AreEqual("InvalidOperationException: boom [card number removed]",dictException["Message"]);
		}

		[TestMethod]
		public void ErrorLog_GetLast4() {
			Assert.AreEqual("1234",PayConnectErrorLog.GetLast4("XXXXXXXXXXXX1234"));
			Assert.AreEqual("1234",PayConnectErrorLog.GetLast4("4111 1111 1111 1234"));
			Assert.AreEqual("",PayConnectErrorLog.GetLast4("12"));
			Assert.AreEqual("",PayConnectErrorLog.GetLast4(null));
		}

		[TestMethod]
		public void ErrorLog_RecurringSummary() {
			string line=PayConnectErrorLog.FormatRecurringSummary(10,7,2,1);
			Assert.IsTrue(line.StartsWith("SUMMARY\t"));
			Dictionary<string,string> dictFields=ParseFields(line);
			Assert.AreEqual("recurring charges",dictFields["Operation"]);
			Assert.AreEqual("10",dictFields["Cards"]);
			Assert.AreEqual("7",dictFields["Approved"]);
			Assert.AreEqual("2",dictFields["Declined"]);
			Assert.AreEqual("1",dictFields["Failed"]);
			PayConnectErrorLog.WriteRecurringSummary(0,0,0,0);
			Assert.AreEqual(0,_listLogLines.Count,"A run with no PayConnect card writes nothing.");
			PayConnectErrorLog.WriteRecurringSummary(3,3,0,0);
			Assert.AreEqual(1,_listLogLines.Count);
		}

		[TestMethod]
		public void ErrorLog_WritesToTheLoggerFolderAndReadsBackTheLast50() {
			string dirOld=Logger.LoggerDirOverride;
			string dirTemp=Path.Combine(Path.GetTempPath(),"vpc-payconnect-log-"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dirTemp);
			PayConnectErrorLog.ActionWriteLineForTests=null;
			Logger.CloseLogger();
			try {
				Logger.LoggerDirOverride=dirTemp;
				for(int i=1;i<=60;i++) {
					PayConnectErrorLog.Entry entry=PayConnectErrorLog.MakeEntry("sale",1,i);
					entry.Message="failure "+i;
					PayConnectErrorLog.Write(entry);
				}
				Assert.IsTrue(Directory.Exists(Path.Combine(dirTemp,PayConnectErrorLog.SubDirectory)),"The PayConnect subfolder of the Logger folder.");
				List<string> listLines=PayConnectErrorLog.GetRecentLines();
				Assert.AreEqual(PayConnectErrorLog.RecentLinesShown,listLines.Count);
				StringAssert.Contains(listLines.First(),"PatNum=11\t");
				StringAssert.Contains(listLines.Last(),"PatNum=60\t");
				StringAssert.Contains(listLines.Last(),"Message=failure 60");
				Assert.AreEqual(5,PayConnectErrorLog.GetRecentLines(5).Count);
			}
			finally {
				Logger.CloseLogger();
				Logger.LoggerDirOverride=dirOld;
				try {
					Directory.Delete(dirTemp,true);
				}
				catch(Exception) {
					//The cleanup thread may still hold the folder; the temp folder is harmless.
				}
			}
		}

		[TestMethod]
		public void GetRecentLines_MissingFolderIsEmpty() {
			string dirOld=Logger.LoggerDirOverride;
			try {
				Logger.LoggerDirOverride=Path.Combine(Path.GetTempPath(),"vpc-missing-"+Guid.NewGuid().ToString("N"));
				Assert.AreEqual(0,Logger.GetRecentLines("PayConnect",50).Count);
				Assert.AreEqual(0,Logger.GetRecentLines("PayConnect",0).Count);
			}
			finally {
				Logger.LoggerDirOverride=dirOld;
			}
		}
		#endregion Error log (25.3)
	}
}
