// Glidewell accounts, lab case products and sending a lab case with a fake client, on a scratch database at the 26.2.0 reference schema.
// Skipped (inconclusive) when the local MariaDB does not answer. Synthetic data only; Glidewell is never called.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect;
using OpenPmsDentalBusiness.VendorDirect.Glidewell;

namespace P262GlidewellTests {
	[TestClass]
	public class GlidewellDbTests {
		private const string API_KEY="synthetic-api-key-0001";

		[TestInitialize]
		public void TestInitialize() {
			GlidewellDb.Begin();
		}

		[TestCleanup]
		public void TestCleanup() {
			GlidewellClients.Current=null;
		}

		///<summary>MSTest v1 has no Assert.ThrowsException. Returns the exception, which must be exactly of type T.</summary>
		private static T AssertThrows<T>(Action action) where T:Exception {
			try {
				action();
			}
			catch(Exception ex) {
				Assert.AreEqual(typeof(T),ex.GetType(),ex.ToString());
				return (T)ex;
			}
			Assert.Fail(typeof(T).Name+" was not thrown.");
			return null;
		}

		#region Program link
		[TestMethod]
		public void Program_InsertedOnceAndOff() {
			Assert.IsTrue(GlidewellProgram.EnsureProgram());
			Assert.IsFalse(GlidewellProgram.EnsureProgram(),"A second call inserts nothing.");
			Assert.AreEqual(1,GlidewellDb.CountRows("program","ProgName='Glidewell'"));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT Enabled FROM program WHERE ProgName='Glidewell'"),"Off until the office turns it on.");
			Assert.AreEqual(GlidewellProgram.ProgDesc,DataCore.GetScalar("SELECT ProgDesc FROM program WHERE ProgName='Glidewell'"));
			Assert.IsFalse(GlidewellProgram.IsEnabled());
			Assert.AreEqual(0,GlidewellDb.CountRows("programproperty"),"The accounts are not program properties.");
		}
		#endregion Program link

		#region Accounts
		[TestMethod]
		public void Account_ApiKeyIsStoredEncrypted() {
			GlidewellAcct glidewellAcct=GlidewellDb.CreateAccount("10-0000001",API_KEY);
			string stored=DataCore.GetScalar("SELECT ApiKey FROM glidewellacct WHERE GlidewellAcctNum="+glidewellAcct.GlidewellAcctNum);
			Assert.IsTrue(stored.StartsWith("osd1$"),"This program's format.");
			Assert.IsFalse(stored.Contains(API_KEY));
			GlidewellAcct glidewellAcctLoaded=GlidewellAccts.GetOne(glidewellAcct.GlidewellAcctNum);
			Assert.AreEqual(API_KEY,GlidewellAccts.ReadApiKey(glidewellAcctLoaded).PlainText);
			Assert.IsTrue(GlidewellAccts.IsComplete(glidewellAcctLoaded));
			Assert.AreEqual("Entered",GlidewellAccts.GetApiKeyStatusText(glidewellAcctLoaded));
		}

		[TestMethod]
		public void Account_KeysOfOtherProgramsAreNeverUsed() {
			foreach(string stored in new[] { "odv2e$c3ludGhldGlj","c3ludGhldGljLWNpcGhlcnRleHQ=","plain-text-key" }) {
				GlidewellAcct glidewellAcct=new GlidewellAcct { AccountNumber="10-0000002",ApiKey=stored };
				SecretReadResult secretReadResult=GlidewellAccts.ReadApiKey(glidewellAcct);
				Assert.IsFalse(secretReadResult.HasValue,stored);
				Assert.IsTrue(secretReadResult.NeedsReentry,stored);
				Assert.IsFalse(GlidewellAccts.IsComplete(glidewellAcct),stored);
				Assert.AreEqual("Enter again",GlidewellAccts.GetApiKeyStatusText(glidewellAcct),stored);
			}
			GlidewellAcct glidewellAcctBlank=new GlidewellAcct { AccountNumber="10-0000003" };
			Assert.AreEqual("Not entered",GlidewellAccts.GetApiKeyStatusText(glidewellAcctBlank));
			Assert.IsFalse(GlidewellAccts.IsComplete(new GlidewellAcct { AccountNumber=" ",ApiKey=GlidewellDb.CreateAccount("x").ApiKey }),
				"No account number.");
		}

		[TestMethod]
		public void Account_ProviderDefaults() {
			Provider provider1=GlidewellDb.CreateProvider("P1");
			Provider provider2=GlidewellDb.CreateProvider("P2");
			Provider provider3=GlidewellDb.CreateProvider("P3");
			GlidewellAcct glidewellAcctA=GlidewellDb.CreateAccount("10-000000A");
			GlidewellAcct glidewellAcctB=GlidewellDb.CreateAccount("10-000000B");
			GlidewellAccts.SetProvidersForAcct(glidewellAcctA.GlidewellAcctNum,new List<long> { provider1.ProvNum,provider2.ProvNum });
			GlidewellAccts.SetProvidersForAcct(glidewellAcctB.GlidewellAcctNum,new List<long> { provider2.ProvNum });
			CollectionAssert.AreEqual(new[] { provider1.ProvNum },GlidewellAccts.GetProvNumsForAcct(glidewellAcctA.GlidewellAcctNum).ToArray(),
				"A provider has one default: B took provider 2.");
			CollectionAssert.AreEqual(new[] { provider2.ProvNum },GlidewellAccts.GetProvNumsForAcct(glidewellAcctB.GlidewellAcctNum).ToArray());
			GlidewellAccts.SetProvidersForAcct(glidewellAcctA.GlidewellAcctNum,new List<long>());
			Assert.AreEqual(0,GlidewellAccts.GetProvNumsForAcct(glidewellAcctA.GlidewellAcctNum).Count);
			Assert.AreEqual(1,GlidewellAccts.GetProvNumsForAcct(glidewellAcctB.GlidewellAcctNum).Count,"Another account's providers are untouched.");
			GlidewellAccts.SetProvidersForAcct(glidewellAcctA.GlidewellAcctNum,new List<long> { provider1.ProvNum });
			Providers.RefreshCache();
			List<GlidewellAcct> listGlidewellAccts=GlidewellAccts.GetAll();
			Assert.AreEqual(glidewellAcctA.GlidewellAcctNum,GlidewellAccts.GetDefaultAcctNum(provider1.ProvNum,listGlidewellAccts));
			Assert.AreEqual(glidewellAcctB.GlidewellAcctNum,GlidewellAccts.GetDefaultAcctNum(provider2.ProvNum,listGlidewellAccts));
			Assert.AreEqual(0,GlidewellAccts.GetDefaultAcctNum(provider3.ProvNum,listGlidewellAccts),"No default and two accounts: the user picks.");
			Assert.AreEqual(glidewellAcctB.GlidewellAcctNum,GlidewellAccts.GetDefaultAcctNum(provider3.ProvNum,
				listGlidewellAccts.Where(x => x.GlidewellAcctNum==glidewellAcctB.GlidewellAcctNum).ToList()),"The office's only account.");
			Assert.AreEqual(0,GlidewellAccts.GetDefaultAcctNum(provider1.ProvNum,new List<GlidewellAcct>()));
		}

		[TestMethod]
		public void Account_DeleteClearsDefaults_RefusedOnceACaseWasSent() {
			Provider provider=GlidewellDb.CreateProvider("D1");
			GlidewellAcct glidewellAcct=GlidewellDb.CreateAccount("10-000000D");
			GlidewellAccts.SetProvidersForAcct(glidewellAcct.GlidewellAcctNum,new List<long> { provider.ProvNum });
			GlidewellAccts.Delete(glidewellAcct.GlidewellAcctNum);
			Assert.IsNull(GlidewellAccts.GetOne(glidewellAcct.GlidewellAcctNum));
			Assert.AreEqual("0",DataCore.GetScalar("SELECT GlidewellAcctNum FROM provider WHERE ProvNum="+provider.ProvNum));
			GlidewellAcct glidewellAcctSent=GlidewellDb.CreateAccount("10-000000E");
			GlidewellMsgs.Insert(new GlidewellMsg { LabCaseNum=1,PatNum=1,GlidewellAcctNum=glidewellAcctSent.GlidewellAcctNum });
			AssertThrows<ApplicationException>(() => GlidewellAccts.Delete(glidewellAcctSent.GlidewellAcctNum));
			Assert.IsNotNull(GlidewellAccts.GetOne(glidewellAcctSent.GlidewellAcctNum));
		}
		#endregion Accounts

		#region Lab case products
		[TestMethod]
		public void Products_SyncInsertsUpdatesAndDeletes() {
			Patient patient=GlidewellDb.CreatePatient();
			Provider provider=GlidewellDb.CreateProvider("S1");
			LabCase labCase=GlidewellDb.CreateLabCase(patient,provider);
			LabCase labCaseOther=GlidewellDb.CreateLabCase(patient,provider);
			LabCaseProducts.Insert(new LabCaseProduct { LabCaseNum=labCaseOther.LabCaseNum,PatNum=patient.PatNum,Material="Other case" });
			List<LabCaseProduct> listSaved=LabCaseProducts.Sync(labCase.LabCaseNum,patient.PatNum,new List<LabCaseProduct> {
				new LabCaseProduct { Material="BruxZir",Shade="A1",ToothRange="8" },
				new LabCaseProduct { Material="PFM",Notes="Second" },
			});
			Assert.IsTrue(listSaved.All(x => x.LabCaseProductNum!=0));
			List<LabCaseProduct> listDb=LabCaseProducts.GetForLabCase(labCase.LabCaseNum);
			CollectionAssert.AreEqual(new[] { "BruxZir","PFM" },listDb.Select(x => x.Material).ToArray());
			Assert.IsTrue(listDb.All(x => x.PatNum==patient.PatNum && x.LabCaseNum==labCase.LabCaseNum));
			listDb[0].Shade="A2";
			LabCaseProduct labCaseProductNew=new LabCaseProduct { Material="Night guard" };
			LabCaseProducts.Sync(labCase.LabCaseNum,patient.PatNum,new List<LabCaseProduct> { listDb[0],labCaseProductNew });
			listDb=LabCaseProducts.GetForLabCase(labCase.LabCaseNum);
			CollectionAssert.AreEqual(new[] { "BruxZir","Night guard" },listDb.Select(x => x.Material).ToArray(),"PFM was removed.");
			Assert.AreEqual("A2",listDb[0].Shade);
			//A key of another lab case's product is never used to overwrite that row.
			LabCaseProduct labCaseProductForeign=LabCaseProducts.GetForLabCase(labCaseOther.LabCaseNum)[0].Copy();
			labCaseProductForeign.Material="Hijack";
			LabCaseProducts.Sync(labCase.LabCaseNum,patient.PatNum,new List<LabCaseProduct> { labCaseProductForeign });
			Assert.AreEqual("Other case",LabCaseProducts.GetForLabCase(labCaseOther.LabCaseNum).Single().Material);
			Assert.AreEqual("Hijack",LabCaseProducts.GetForLabCase(labCase.LabCaseNum).Single().Material,"Inserted as this lab case's own row.");
			LabCaseProducts.Sync(labCase.LabCaseNum,patient.PatNum,new List<LabCaseProduct>());
			Assert.AreEqual(0,LabCaseProducts.GetForLabCase(labCase.LabCaseNum).Count);
			Assert.AreEqual(1,LabCaseProducts.GetForLabCase(labCaseOther.LabCaseNum).Count);
		}

		[TestMethod]
		public void Products_StoredProductListValueIsKept() {
			Patient patient=GlidewellDb.CreatePatient();
			LabCase labCase=GlidewellDb.CreateLabCase(patient,GlidewellDb.CreateProvider("K1"));
			//A row another program wrote with a value of Glidewell's (unpublished) product list.
			DataCore.NonQ("INSERT INTO labcaseproduct (LabCaseNum,PatNum,GlidewellProduct,Material,Shade,ToothRange,Notes) "
				+"VALUES ("+labCase.LabCaseNum+","+patient.PatNum+",9,'Zirconia','A1','3','')");
			List<LabCaseProduct> listDb=LabCaseProducts.GetForLabCase(labCase.LabCaseNum);
			Assert.AreEqual(9,(int)listDb[0].GlidewellProduct);
			listDb[0].Notes="Edited here";
			LabCaseProducts.Sync(labCase.LabCaseNum,patient.PatNum,listDb);
			Assert.AreEqual("9",DataCore.GetScalar("SELECT GlidewellProduct FROM labcaseproduct WHERE LabCaseNum="+labCase.LabCaseNum));
		}

		[TestMethod]
		public void LabCaseDelete_DeletesProducts_RefusedOnceSent() {
			Patient patient=GlidewellDb.CreatePatient();
			Provider provider=GlidewellDb.CreateProvider("L1");
			LabCase labCase=GlidewellDb.CreateLabCase(patient,provider);
			LabCaseProducts.Sync(labCase.LabCaseNum,patient.PatNum,new List<LabCaseProduct> { new LabCaseProduct { Material="Crown" } });
			LabCases.Delete(labCase.LabCaseNum);
			Assert.IsNull(LabCases.GetOne(labCase.LabCaseNum));
			Assert.AreEqual(0,GlidewellDb.CountRows("labcaseproduct","LabCaseNum="+labCase.LabCaseNum));
			LabCase labCaseSent=GlidewellDb.CreateLabCase(patient,provider);
			LabCaseProducts.Sync(labCaseSent.LabCaseNum,patient.PatNum,new List<LabCaseProduct> { new LabCaseProduct { Material="Crown" } });
			GlidewellMsgs.Insert(new GlidewellMsg { LabCaseNum=labCaseSent.LabCaseNum,PatNum=patient.PatNum,GlidewellAcctNum=1 });
			Exception exception=AssertThrows<Exception>(() => LabCases.Delete(labCaseSent.LabCaseNum));
			StringAssert.Contains(exception.Message,"Glidewell");
			Assert.IsNotNull(LabCases.GetOne(labCaseSent.LabCaseNum));
			Assert.AreEqual(1,GlidewellDb.CountRows("labcaseproduct","LabCaseNum="+labCaseSent.LabCaseNum),"Nothing was deleted.");
		}

		[TestMethod]
		public void PatientMerge_MovesTheNewPatNumColumns() {
			Assert.IsTrue(Patients.StringArrayPatNumForeignKeys.Contains("labcaseproduct.PatNum"));
			Assert.IsTrue(Patients.StringArrayPatNumForeignKeys.Contains("glidewellmsg.PatNum"));
			Assert.IsTrue(Patients.StringArrayPatNumForeignKeys.Contains("labcase.PatNum"));
		}
		#endregion Lab case products

		#region Sending
		///<summary>A lab case ready to send: program on, one account with a key, a patient, a provider with a license and two products.</summary>
		private static (LabCase labCase,GlidewellAcct glidewellAcct,Patient patient,Provider provider) Arrange() {
			GlidewellDb.SetProgramEnabled(true);
			Patient patient=GlidewellDb.CreatePatient();
			Provider provider=GlidewellDb.CreateProvider("G1","LIC-77");
			LabCase labCase=GlidewellDb.CreateLabCase(patient,provider,"Crown on 8");
			LabCaseProducts.Sync(labCase.LabCaseNum,patient.PatNum,new List<LabCaseProduct> {
				new LabCaseProduct { Material="BruxZir",Shade="A1",ToothRange="8" },
			});
			GlidewellAcct glidewellAcct=GlidewellDb.CreateAccount("10-2000001",API_KEY);
			return (labCase,glidewellAcct,patient,provider);
		}

		[TestMethod]
		public void Send_InsertsOneRowWithTheRequestAndTheAnswer() {
			var arranged=Arrange();
			FakeGlidewellClient fake=new FakeGlidewellClient();
			fake.FuncRegister=() => GlidewellCallResult.Success("{\"VendorRxId\":\"RX-SYN-42\",\"VendorName\":\"Medit\"}");
			GlidewellLabCaseResult result=GlidewellLabCases.Send(arranged.labCase.LabCaseNum,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.Medit,fake);
			Assert.IsTrue(result.IsSuccess,result.Message);
			Assert.AreEqual(1,fake.ListCalls.Count);
			Assert.AreEqual("10-2000001",fake.ListCalls[0].AccountNumber);
			Assert.AreEqual(API_KEY,fake.ListCalls[0].ApiKey,"The decrypted key of the chosen account.");
			Assert.AreEqual(1,GlidewellDb.CountRows("glidewellmsg"));
			GlidewellMsg glidewellMsg=GlidewellMsgs.GetForLabCase(arranged.labCase.LabCaseNum);
			Assert.AreEqual(result.GlidewellMsg.GlidewellMsgNum,glidewellMsg.GlidewellMsgNum);
			Assert.AreEqual(arranged.patient.PatNum,glidewellMsg.PatNum);
			Assert.AreEqual(arranged.glidewellAcct.GlidewellAcctNum,glidewellMsg.GlidewellAcctNum);
			Assert.AreEqual(EnumGlidewellOrderStatus.Initialized,glidewellMsg.OrderStatus,"Registered, waiting for the scan.");
			Assert.AreEqual("RX-SYN-42",glidewellMsg.VendorRxId);
			Assert.IsTrue(glidewellMsg.SentMedit);
			Assert.IsFalse(glidewellMsg.SentITero);
			Assert.AreEqual(fake.ListCalls[0].RequestJson,glidewellMsg.RawJsonRequest,"The request is stored as sent.");
			Assert.AreEqual("{\"VendorRxId\":\"RX-SYN-42\",\"VendorName\":\"Medit\"}",glidewellMsg.RawJsonResponse);
			JObject jObject=GlidewellJsonTests.Parse(glidewellMsg.RawJsonRequest);
			Assert.AreEqual("Medit",(string)jObject["VendorName"]);
			Assert.AreEqual(arranged.labCase.LabCaseNum,(long)jObject["OrderId"]);
			Assert.AreEqual(arranged.provider.ProvNum,(long)jObject["DoctorId"]);
			Assert.AreEqual("LIC-77",(string)jObject["LicenseNumber"]);
			Assert.AreEqual(arranged.patient.PatNum,(long)jObject["PatientId"]);
			Assert.AreEqual("Crown on 8\nProducts:\n- BruxZir, shade A1, teeth 8",(string)jObject["Notes"]);
			Assert.IsFalse(glidewellMsg.RawJsonRequest.Contains(API_KEY),"The key is never stored in the request.");
			StringAssert.Contains(result.Message,"Medit");
		}

		[TestMethod]
		public void Send_StatusFromTheAnswerWins() {
			var arranged=Arrange();
			FakeGlidewellClient fake=new FakeGlidewellClient();
			fake.FuncRegister=() => GlidewellCallResult.Success("{\"VendorRxId\":\"RX-2\",\"OrderStatus\":\"Received\"}");
			GlidewellLabCaseResult result=GlidewellLabCases.Send(arranged.labCase.LabCaseNum,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.ITero,fake);
			Assert.IsTrue(result.IsSuccess);
			GlidewellMsg glidewellMsg=GlidewellMsgs.GetForLabCase(arranged.labCase.LabCaseNum);
			Assert.AreEqual(EnumGlidewellOrderStatus.Received,glidewellMsg.OrderStatus);
			Assert.IsTrue(glidewellMsg.SentITero);
			Assert.IsFalse(glidewellMsg.SentMedit);
		}

		[TestMethod]
		public void Send_OnlyOnce() {
			var arranged=Arrange();
			FakeGlidewellClient fake=new FakeGlidewellClient();
			Assert.IsTrue(GlidewellLabCases.Send(arranged.labCase.LabCaseNum,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.ITero,fake).IsSuccess);
			GlidewellLabCaseResult result=GlidewellLabCases.Send(arranged.labCase.LabCaseNum,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.ITero,fake);
			Assert.IsFalse(result.IsSuccess);
			StringAssert.Contains(result.Message,"already sent");
			Assert.AreEqual(1,fake.ListCalls.Count,"Glidewell is not called again.");
			Assert.AreEqual(1,GlidewellDb.CountRows("glidewellmsg"));
		}

		[TestMethod]
		public void Send_FailureWritesNothing() {
			var arranged=Arrange();
			FakeGlidewellClient fake=new FakeGlidewellClient();
			fake.FuncRegister=() => GlidewellCallResult.Failed("Refused (synthetic).","{\"error\":\"x\"}");
			GlidewellLabCaseResult result=GlidewellLabCases.Send(arranged.labCase.LabCaseNum,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.ITero,fake);
			Assert.IsFalse(result.IsSuccess);
			Assert.IsFalse(result.IsBlocked);
			StringAssert.Contains(result.Message,"Refused (synthetic).");
			Assert.AreEqual(0,GlidewellDb.CountRows("glidewellmsg"));
			fake.FuncRegister=() => throw new TimeoutException("Synthetic timeout.");
			result=GlidewellLabCases.Send(arranged.labCase.LabCaseNum,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.ITero,fake);
			Assert.IsFalse(result.IsSuccess,"A client that throws is a failed call, not a crash.");
			StringAssert.Contains(result.Message,"Synthetic timeout.");
			fake.FuncRegister=() => null;
			Assert.IsFalse(GlidewellLabCases.Send(arranged.labCase.LabCaseNum,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.ITero,fake).IsSuccess);
			Assert.AreEqual(0,GlidewellDb.CountRows("glidewellmsg"));
		}

		[TestMethod]
		public void Send_BlockedClientSendsNothing() {
			var arranged=Arrange();
			GlidewellClients.Current=null;
			GlidewellLabCaseResult result=GlidewellLabCases.Send(arranged.labCase.LabCaseNum,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.ITero);
			Assert.IsFalse(result.IsSuccess);
			Assert.IsTrue(result.IsBlocked);
			Assert.AreEqual(new GlidewellClientBlocked().UnavailableReason,result.Message);
			Assert.AreEqual(0,GlidewellDb.CountRows("glidewellmsg"));
		}

		[TestMethod]
		public void Send_OffUntilTheOfficeEntersItsCredentials() {
			var arranged=Arrange();
			FakeGlidewellClient fake=new FakeGlidewellClient();
			//The program link is off.
			GlidewellDb.SetProgramEnabled(false);
			GlidewellLabCaseResult result=GlidewellLabCases.Send(arranged.labCase.LabCaseNum,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.ITero,fake);
			Assert.IsFalse(result.IsSuccess);
			StringAssert.Contains(result.Message,"turned off");
			GlidewellDb.SetProgramEnabled(true);
			//No key, a key another program stored, no account number, no account.
			GlidewellAcct glidewellAcctNoKey=GlidewellDb.CreateAccount("10-2000002",null);
			GlidewellAcct glidewellAcctForeignKey=new GlidewellAcct { AccountNumber="10-2000003",ApiKey="odv2e$c3ludGhldGlj" };
			GlidewellAccts.Insert(glidewellAcctForeignKey);
			GlidewellAcct glidewellAcctNoNumber=GlidewellDb.CreateAccount("");
			foreach(long glidewellAcctNum in new[] { glidewellAcctNoKey.GlidewellAcctNum,glidewellAcctForeignKey.GlidewellAcctNum,
				glidewellAcctNoNumber.GlidewellAcctNum,0L })
			{
				result=GlidewellLabCases.Send(arranged.labCase.LabCaseNum,glidewellAcctNum,GlidewellScanner.ITero,fake);
				Assert.IsFalse(result.IsSuccess,glidewellAcctNum.ToString());
			}
			StringAssert.Contains(GlidewellLabCases.Send(arranged.labCase.LabCaseNum,glidewellAcctNoKey.GlidewellAcctNum,GlidewellScanner.ITero,fake).Message,
				"Enter the API key");
			StringAssert.Contains(GlidewellLabCases.Send(arranged.labCase.LabCaseNum,glidewellAcctForeignKey.GlidewellAcctNum,GlidewellScanner.ITero,fake)
				.Message,"cannot be read here");
			Assert.AreEqual(0,fake.ListCalls.Count,"Nothing is sent without the office's own credentials.");
			Assert.AreEqual(0,GlidewellDb.CountRows("glidewellmsg"));
		}

		[TestMethod]
		public void Send_RequiresThePatientsNames() {
			var arranged=Arrange();
			DataCore.NonQ("UPDATE patient SET FName='' WHERE PatNum="+arranged.patient.PatNum);
			FakeGlidewellClient fake=new FakeGlidewellClient();
			GlidewellLabCaseResult result=GlidewellLabCases.Send(arranged.labCase.LabCaseNum,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.ITero,fake);
			Assert.IsFalse(result.IsSuccess);
			StringAssert.Contains(result.Message,"first and last name");
			Assert.AreEqual(0,fake.ListCalls.Count);
			Assert.IsFalse(GlidewellLabCases.Send(999999,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.ITero,fake).IsSuccess,"No such lab case.");
			Assert.AreEqual(0,fake.ListCalls.Count);
		}
		#endregion Sending

		#region Status
		[TestMethod]
		public void Status_UpdatesTheSameRow_WithTheAccountItWasSentWith() {
			var arranged=Arrange();
			FakeGlidewellClient fake=new FakeGlidewellClient();
			Assert.IsTrue(GlidewellLabCases.Send(arranged.labCase.LabCaseNum,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.ITero,fake).IsSuccess);
			long glidewellMsgNum=GlidewellMsgs.GetForLabCase(arranged.labCase.LabCaseNum).GlidewellMsgNum;
			string requestJson=GlidewellMsgs.GetOne(glidewellMsgNum).RawJsonRequest;
			//The provider's default is now another account: status still uses the account the case was sent with.
			GlidewellAcct glidewellAcctOther=GlidewellDb.CreateAccount("10-2999999","synthetic-other-key");
			GlidewellAccts.SetProvidersForAcct(glidewellAcctOther.GlidewellAcctNum,new List<long> { arranged.provider.ProvNum });
			Providers.RefreshCache();
			fake.FuncGetStatus=() => GlidewellCallResult.Success("{\"OrderStatus\":\"Shipped\",\"OutboundCarrier\":\"UPS\",\"OutboundTrackingNumber\":\"1ZSYN\"}");
			GlidewellLabCaseResult result=GlidewellLabCases.CheckStatus(arranged.labCase.LabCaseNum,fake);
			Assert.IsTrue(result.IsSuccess,result.Message);
			FakeGlidewellClient.Call call=fake.ListCalls.Last();
			Assert.AreEqual("GetStatus",call.Method);
			Assert.AreEqual("10-2000001",call.AccountNumber);
			Assert.AreEqual(API_KEY,call.ApiKey);
			Assert.AreEqual(glidewellMsgNum,call.GlidewellMsgNum);
			Assert.AreEqual(1,GlidewellDb.CountRows("glidewellmsg"),"No history: the row is updated.");
			GlidewellMsg glidewellMsg=GlidewellMsgs.GetOne(glidewellMsgNum);
			Assert.AreEqual(EnumGlidewellOrderStatus.Shipped,glidewellMsg.OrderStatus);
			StringAssert.Contains(glidewellMsg.RawJsonResponse,"1ZSYN");
			Assert.AreEqual(requestJson,glidewellMsg.RawJsonRequest,"The request stays as sent.");
			Assert.AreEqual("RX-SYN-1",glidewellMsg.VendorRxId,"The register response's id stays.");
			StringAssert.Contains(GlidewellLabCases.GetStatusLine(glidewellMsg),"UPS 1ZSYN");
			StringAssert.Contains(GlidewellLabCases.GetDetailsText(glidewellMsg,arranged.glidewellAcct),"Tracking number: 1ZSYN");
			Assert.IsFalse(GlidewellLabCases.GetDetailsText(glidewellMsg,arranged.glidewellAcct).Contains(API_KEY));
		}

		[TestMethod]
		public void Status_UnknownOrMissingStatusKeepsTheStoredOne() {
			var arranged=Arrange();
			FakeGlidewellClient fake=new FakeGlidewellClient();
			Assert.IsTrue(GlidewellLabCases.Send(arranged.labCase.LabCaseNum,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.ITero,fake).IsSuccess);
			fake.FuncGetStatus=() => GlidewellCallResult.Success("{\"OrderStatus\":\"Teleported\"}");
			GlidewellLabCaseResult result=GlidewellLabCases.CheckStatus(arranged.labCase.LabCaseNum,fake);
			Assert.IsTrue(result.IsSuccess);
			StringAssert.Contains(result.Message,"Teleported");
			GlidewellMsg glidewellMsg=GlidewellMsgs.GetForLabCase(arranged.labCase.LabCaseNum);
			Assert.AreEqual(EnumGlidewellOrderStatus.Initialized,glidewellMsg.OrderStatus);
			Assert.AreEqual("{\"OrderStatus\":\"Teleported\"}",glidewellMsg.RawJsonResponse,"The most recent answer is kept.");
			fake.FuncGetStatus=() => GlidewellCallResult.Success("{}");
			StringAssert.Contains(GlidewellLabCases.CheckStatus(arranged.labCase.LabCaseNum,fake).Message,"names no status");
			Assert.AreEqual(EnumGlidewellOrderStatus.Initialized,GlidewellMsgs.GetForLabCase(arranged.labCase.LabCaseNum).OrderStatus);
		}

		[TestMethod]
		public void Status_FailureWritesNothing() {
			var arranged=Arrange();
			FakeGlidewellClient fake=new FakeGlidewellClient();
			Assert.IsTrue(GlidewellLabCases.Send(arranged.labCase.LabCaseNum,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.ITero,fake).IsSuccess);
			GlidewellMsg glidewellMsgBefore=GlidewellMsgs.GetForLabCase(arranged.labCase.LabCaseNum);
			fake.FuncGetStatus=() => GlidewellCallResult.Failed("Synthetic outage.");
			GlidewellLabCaseResult result=GlidewellLabCases.CheckStatus(arranged.labCase.LabCaseNum,fake);
			Assert.IsFalse(result.IsSuccess);
			StringAssert.Contains(result.Message,"Synthetic outage.");
			fake.FuncGetStatus=() => throw new InvalidOperationException("Synthetic bug.");
			Assert.IsFalse(GlidewellLabCases.CheckStatus(arranged.labCase.LabCaseNum,fake).IsSuccess);
			GlidewellMsg glidewellMsgAfter=GlidewellMsgs.GetForLabCase(arranged.labCase.LabCaseNum);
			Assert.AreEqual(glidewellMsgBefore.RawJsonResponse,glidewellMsgAfter.RawJsonResponse);
			Assert.AreEqual(glidewellMsgBefore.OrderStatus,glidewellMsgAfter.OrderStatus);
			//The blocked client and a lab case that was never sent.
			GlidewellClients.Current=null;
			Assert.IsTrue(GlidewellLabCases.CheckStatus(arranged.labCase.LabCaseNum).IsBlocked);
			LabCase labCaseNotSent=GlidewellDb.CreateLabCase(arranged.patient,arranged.provider);
			StringAssert.Contains(GlidewellLabCases.CheckStatus(labCaseNotSent.LabCaseNum,fake).Message,"not sent");
		}

		[TestMethod]
		public void Status_AccountDeletedOrKeyLost() {
			var arranged=Arrange();
			FakeGlidewellClient fake=new FakeGlidewellClient();
			Assert.IsTrue(GlidewellLabCases.Send(arranged.labCase.LabCaseNum,arranged.glidewellAcct.GlidewellAcctNum,GlidewellScanner.ITero,fake).IsSuccess);
			int countCalls=fake.ListCalls.Count;
			DataCore.NonQ("UPDATE glidewellacct SET ApiKey='' WHERE GlidewellAcctNum="+arranged.glidewellAcct.GlidewellAcctNum);
			StringAssert.Contains(GlidewellLabCases.CheckStatus(arranged.labCase.LabCaseNum,fake).Message,"Enter the API key");
			DataCore.NonQ("DELETE FROM glidewellacct");
			StringAssert.Contains(GlidewellLabCases.CheckStatus(arranged.labCase.LabCaseNum,fake).Message,"no longer exists");
			Assert.AreEqual(countCalls,fake.ListCalls.Count,"Glidewell is not called without the account's credentials.");
		}

		[TestMethod]
		public void Display_TextsOfAStoredRow() {
			Assert.AreEqual("Not sent to Glidewell.",GlidewellLabCases.GetStatusLine(null));
			GlidewellMsg glidewellMsg=new GlidewellMsg { OrderStatus=EnumGlidewellOrderStatus.Initialized,SentMedit=true,VendorRxId="RX-9",
				RawJsonResponse="not json" };
			Assert.AreEqual(GlidewellScanner.Medit,GlidewellLabCases.GetScanner(glidewellMsg));
			StringAssert.Contains(GlidewellLabCases.GetStatusLine(glidewellMsg),"(Medit)");
			StringAssert.Contains(GlidewellLabCases.GetScanInstruction(glidewellMsg),"Medit");
			string details=GlidewellLabCases.GetDetailsText(glidewellMsg,null);
			StringAssert.Contains(details,"VendorRxId: RX-9");
			StringAssert.Contains(details,"Account: deleted account");
			StringAssert.Contains(details,"Last answer: not json");
			glidewellMsg.OrderStatus=EnumGlidewellOrderStatus.Shipped;
			Assert.AreEqual("",GlidewellLabCases.GetScanInstruction(glidewellMsg),"Only a case waiting for its scan.");
			Assert.AreEqual(GlidewellScanner.ITero,GlidewellLabCases.GetScanner(new GlidewellMsg { SentITero=true }));
		}
		#endregion Status
	}
}
