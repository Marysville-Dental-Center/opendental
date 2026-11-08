// S0-R (2026-11-08): what the DentX (ProImage), Dexis Integrator and Vipersoft program links send over DDE, now through the in-house client
// CodeBase.ODDdeClient instead of NDde.dll: each bridge's command, and the service, topic, calls and timeouts of its conversation, on a fake
// conversation put in through ODDdeClient.FuncCreate. The client's own rules are tested without opening a DDE conversation, its DDEML
// constants against ddeml.h, and the client itself against Windows' DDEML through DdeLoopbackServer, a DDE server inside the test process under
// a service name no program uses (no vendor program is needed or reached). Synthetic patients only; no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental.Bridges;
using OpenPmsDentalBusiness;

namespace VendorDirectTests.DdeBridges {
	[TestClass]
	public class DdeCommandTests {
		///<summary>Every call on every fake conversation, in order: "Create service|topic", "Connect", "Execute timeout command", "Disconnect",
		///"Dispose".</summary>
		private List<string> _listCalls;
		///<summary>The fakes FuncCreate made, in order.</summary>
		private List<FakeDdeConversation> _listConversations;
		///<summary>Set up each new fake before it is returned, with its index.</summary>
		private Action<FakeDdeConversation,int> _actionSetUp;
		private List<ISynchronizeInvoke> _listSynchronizingObjects;

		[TestInitialize]
		public void SetUp() {
			_listCalls=new List<string>();
			_listConversations=new List<FakeDdeConversation>();
			_listSynchronizingObjects=new List<ISynchronizeInvoke>();
			_actionSetUp=null;
			ODDdeClient.FuncCreate=(service,topic,synchronizingObject) => {
				_listCalls.Add("Create "+service+"|"+topic);
				_listSynchronizingObjects.Add(synchronizingObject);
				FakeDdeConversation fake=new FakeDdeConversation(service,topic,_listCalls);
				_actionSetUp?.Invoke(fake,_listConversations.Count);
				_listConversations.Add(fake);
				return fake;
			};
			DexisIntegrator.CloseConversation();
		}

		[TestCleanup]
		public void TearDown() {
			DexisIntegrator.CloseConversation();
			ODDdeClient.FuncCreate=ODDdeClient.CreateDefault;
		}

		///<summary>Asserts that action throws exactly T (not a subclass: ArgumentNullException is an ArgumentException, ObjectDisposedException an
		///InvalidOperationException) and returns it. MSTest v1 has no Assert.ThrowsException.</summary>
		private static T ThrowsExactly<T>(Action action,string message="") where T:Exception {
			T ex=AssertEx.Throws<T>(action);
			Assert.AreEqual(typeof(T),ex.GetType(),message);
			return ex;
		}

		private static Patient GetPatient() {
			return new Patient {
				PatNum=1234,
				LName="Example",
				FName="Pat",
				MiddleI="Q",
				ChartNumber="AB123",
				Birthdate=new DateTime(1980,2,5),
				Gender=PatientGender.Female,
				Address="1 Main St",
				City="Anytown",
				State="ST",
				Zip="00000",
				SSN="000-00-0000",
			};
		}

		#region DentX (ProImage)
		[TestMethod]
		public void DentX_Command_WithPatNum() {
			Patient pat=GetPatient();
			string expected="Xray,1234,Pat,Example,"+pat.Birthdate.ToShortDateString()+",F,1 Main St,Anytown,ST,00000";
			Assert.AreEqual(expected,DentX.GetCommand(pat,isChartNum:false));
		}

		[TestMethod]
		public void DentX_Command_WithChartNumber_DropsCommasInValues_AndSendsMForAnythingButFemale() {
			Patient pat=GetPatient();
			pat.ChartNumber="AB,12";
			pat.FName="Mary,Ann";
			pat.LName="Example, Jr";
			pat.Address="1 Main St, Apt 2";
			pat.City="Any,town";
			pat.State="S,T";
			pat.Zip="00,000";
			pat.Gender=PatientGender.Male;
			string expected="Xray,AB12,MaryAnn,Example Jr,"+pat.Birthdate.ToShortDateString()+",M,1 Main St Apt 2,Anytown,ST,00000";
			Assert.AreEqual(expected,DentX.GetCommand(pat,isChartNum:true));
			Assert.AreEqual(10,DentX.GetCommand(pat,isChartNum:true).Split(',').Length,"Ten fields, no comma inside one.");
			pat.Gender=PatientGender.Unknown;
			StringAssert.Contains(DentX.GetCommand(pat,isChartNum:true),",M,1 Main St");
		}

		[TestMethod]
		public void DentX_SendCommand_ConnectsToProImage_ExecutesWithTwoSeconds_AndCloses() {
			DentX.SendCommand("Xray,1234");
			CollectionAssert.AreEqual(new[] { "Create ProImage|Image","Connect","Execute 2000 Xray,1234","Dispose" },_listCalls,string.Join("\r\n",_listCalls));
			Assert.IsNull(_listSynchronizingObjects.Single(),"On the calling thread (the button click).");
		}

		[TestMethod]
		public void DentX_SendCommand_IgnoresErrors_AsBefore() {
			_actionSetUp=(fake,index) => fake.ExceptionOnConnect=new ODDdeException("Connecting to the DDE server ProImage|Image",0x400A);
			DentX.SendCommand("Xray,1234");//no exception
			CollectionAssert.AreEqual(new[] { "Create ProImage|Image","Connect","Dispose" },_listCalls,string.Join("\r\n",_listCalls));
		}
		#endregion DentX (ProImage)

		#region Vipersoft
		[TestMethod]
		public void Vipersoft_Command_HasEighteenBarredFields_StartingWithVersion4() {
			Patient pat=GetPatient();
			Provider prov=new Provider { LName="Doe",FName="Jane" };
			string command=Vipersoft.GetCommand(pat,"1234",prov,new IntPtr(4660));
			string expected=((char)4).ToString()+"4660|"+"OpenDental|"+"1234|"+"Example|"+"Pat|"+"Q|"+"|"+"Doe, Jane|"+"|||||||"+"|"+"000-00-0000|"+"1|";
			Assert.AreEqual(expected,command);
			Assert.AreEqual(19,command.Split('|').Length,"18 fields, every one followed by a bar, the last one too.");
			Assert.AreEqual("",command.Split('|').Last());
			byte[] arrayBytes=ODDdeClient.GetCommandBytes(command);
			Assert.AreEqual(4,arrayBytes[0],"Version 4: one byte, 0x04.");
			Assert.AreEqual(command.Length+1,arrayBytes.Length);
		}

		[TestMethod]
		public void Vipersoft_SendCommand_ConnectsToAdvancedIntraOral_ExecutesWithTwoSeconds_ThenDisconnects() {
			string command=((char)4).ToString()+"1|OpenDental|1|";
			Vipersoft.SendCommand(command);
			CollectionAssert.AreEqual(new[] { "Create Vipersoft|Advanced IntraOral","Connect","Execute 2000 "+command,"Disconnect","Dispose" },
				_listCalls,string.Join("\r\n",_listCalls));
		}

		[TestMethod]
		public void Vipersoft_SendCommand_IgnoresErrors_AsBefore() {
			_actionSetUp=(fake,index) => fake.ExceptionOnExecute=new ODDdeException("The DDE command to Vipersoft|Advanced IntraOral",0x4005);
			Vipersoft.SendCommand("x");//no exception
			CollectionAssert.AreEqual(new[] { "Create Vipersoft|Advanced IntraOral","Connect","Execute 2000 x","Dispose" },_listCalls,
				string.Join("\r\n",_listCalls));
		}
		#endregion Vipersoft

		#region Dexis Integrator
		[TestMethod]
		public void DexisIntegrator_Command_WithBirthdate() {
			Assert.AreEqual("SET 1234 LN=\"Example\" FN=\"Pat\" MI=\"Q\" BD=19800205",DexisIntegrator.GetDdeCommand(GetPatient(),"1234"));
			Assert.AreEqual("SET AB123 LN=\"Example\" FN=\"Pat\" MI=\"Q\" BD=19800205",DexisIntegrator.GetDdeCommand(GetPatient(),"AB123"),"The ChartNumber.");
		}

		[TestMethod]
		public void DexisIntegrator_Command_LeavesOutAnInvalidBirthdate() {
			Patient pat=GetPatient();
			pat.Birthdate=DateTime.MinValue;
			Assert.AreEqual("SET 1234 LN=\"Example\" FN=\"Pat\" MI=\"Q\"",DexisIntegrator.GetDdeCommand(pat,"1234"));
			pat.Birthdate=new DateTime(1880,12,31);
			Assert.IsFalse(DexisIntegrator.GetDdeCommand(pat,"1234").Contains("BD="),"1880 and before is not a valid birthdate.");
		}

		[TestMethod]
		public void DexisIntegrator_Command_AddsTheBirthdateOnlyWithinTwoHundredFiftyFiveCharacters() {
			Patient pat=GetPatient();
			string commandShort=DexisIntegrator.GetDdeCommand(new Patient { LName="",FName="Pat",MiddleI="Q",Birthdate=DateTime.MinValue },"1234");
			//Without BD the command is the fixed part plus the last name. 243 characters take BD (12 more: 255); 244 do not.
			pat.LName=new string('L',243-commandShort.Length);
			string command=DexisIntegrator.GetDdeCommand(pat,"1234");
			Assert.AreEqual(255,command.Length);
			StringAssert.EndsWith(command," BD=19800205");
			pat.LName=new string('L',244-commandShort.Length);
			command=DexisIntegrator.GetDdeCommand(pat,"1234");
			Assert.AreEqual(244,command.Length);
			Assert.IsFalse(command.Contains("BD="));
		}

		[TestMethod]
		public void DexisIntegrator_ExecuteDDE_OpensOneConversation_AndKeepsItForTheNextPatient() {
			DexisIntegrator.ExecuteDDE("SET 1 LN=\"A\" FN=\"B\" MI=\"\"");
			DexisIntegrator.ExecuteDDE("SET 2 LN=\"C\" FN=\"D\" MI=\"\"");
			CollectionAssert.AreEqual(new[] { "Create Dexis|Patient","Connect","Execute 30000 SET 1 LN=\"A\" FN=\"B\" MI=\"\"",
				"Execute 30000 SET 2 LN=\"C\" FN=\"D\" MI=\"\"" },_listCalls,string.Join("\r\n",_listCalls));
			Assert.IsNull(_listSynchronizingObjects.Single(),"No window is open in a test; in the program it is the main window.");
		}

		[TestMethod]
		public void DexisIntegrator_ExecuteDDE_ReconnectsTheSameConversation_AfterTheServerEndedIt() {
			DexisIntegrator.ExecuteDDE("SET 1");
			//The Integrator ended the conversation (XTYP_DISCONNECT). That the real client turns IsConnected false then is tested on Windows' DDEML
			//(DexisIntegrator_ExecuteDDE_OnWindowsDdeml_ConnectsAgain_AfterTheIntegratorEndedTheConversation).
			_listConversations.Single().IsConnected=false;
			DexisIntegrator.ExecuteDDE("SET 2");
			CollectionAssert.AreEqual(new[] { "Create Dexis|Patient","Connect","Execute 30000 SET 1","Connect","Execute 30000 SET 2" },_listCalls,
				string.Join("\r\n",_listCalls));
		}

		[TestMethod]
		public void DexisIntegrator_ExecuteDDE_RetriesOnceOnANewConversation_AfterObjectDisposedException() {
			_actionSetUp=(fake,index) => {
				if(index==0) {
					fake.ExceptionOnExecute=new ObjectDisposedException("conversation");
				}
			};
			DexisIntegrator.ExecuteDDE("SET 1");
			CollectionAssert.AreEqual(new[] { "Create Dexis|Patient","Connect","Execute 30000 SET 1","Disconnect","Dispose",
				"Create Dexis|Patient","Connect","Execute 30000 SET 1" },_listCalls,string.Join("\r\n",_listCalls));
			Assert.AreEqual(2,_listConversations.Count);
		}

		[TestMethod]
		public void DexisIntegrator_ExecuteDDE_Throws_WhenTheRetryFailsToo() {
			_actionSetUp=(fake,index) => fake.ExceptionOnExecute=new ObjectDisposedException("conversation");
			ThrowsExactly<ObjectDisposedException>(() => DexisIntegrator.ExecuteDDE("SET 1"),"SendDataDDE shows it.");
			Assert.AreEqual(2,_listConversations.Count,"One retry, not more.");
		}

		[TestMethod]
		public void DexisIntegrator_ExecuteDDE_ThrowsOtherErrors_AndTriesTheSameConversationAgainNextTime() {
			_actionSetUp=(fake,index) => fake.ExceptionOnConnect=new ODDdeException("Connecting to the DDE server Dexis|Patient",0x400A);
			ODDdeException ex=ThrowsExactly<ODDdeException>(() => DexisIntegrator.ExecuteDDE("SET 1"));
			StringAssert.Contains(ex.Message,"DMLERR_NO_CONV_ESTABLISHED");
			_listConversations.Single().ExceptionOnConnect=null;//the Integrator was started
			DexisIntegrator.ExecuteDDE("SET 1");
			CollectionAssert.AreEqual(new[] { "Create Dexis|Patient","Connect","Connect","Execute 30000 SET 1" },_listCalls,string.Join("\r\n",_listCalls));
		}

		[TestMethod]
		public void DexisIntegrator_CloseConversation_DisconnectsAndDisposes_AndTheNextCommandOpensANewOne() {
			DexisIntegrator.ExecuteDDE("SET 1");
			DexisIntegrator.CloseConversation();
			DexisIntegrator.CloseConversation();//nothing left to close
			DexisIntegrator.ExecuteDDE("SET 2");
			CollectionAssert.AreEqual(new[] { "Create Dexis|Patient","Connect","Execute 30000 SET 1","Disconnect","Dispose",
				"Create Dexis|Patient","Connect","Execute 30000 SET 2" },_listCalls,string.Join("\r\n",_listCalls));
		}

		[TestMethod]
		public void DexisIntegrator_ExecuteDDE_OnWindowsDdeml_ConnectsAgain_AfterTheIntegratorEndedTheConversation() {
			using(DdeLoopbackServer server=DdeLoopbackServer.Start("Patient")) {
				IDdeConversation ddeConversation=null;
				ODDdeClient.FuncCreate=(service,topic,synchronizingObject) => {
					_listCalls.Add("Create "+service+"|"+topic);
					ddeConversation=new ODDdeClient(server.Service,topic,synchronizingObject);//the real client; the loopback server stands in for "Dexis"
					return ddeConversation;
				};
				try {
					DexisIntegrator.ExecuteDDE("SET 1 LN=\"A\"");//the button
					Assert.IsTrue(ddeConversation.IsConnected);
					server.EndConversation();//the Integrator was closed
					Assert.IsTrue(DdeLoopbackServer.PumpMessagesUntil(() => !ddeConversation.IsConnected,5000),
						"The client saw the server end the conversation (XTYP_DISCONNECT): the next patient change closes it, the button opens a new one.");
					DexisIntegrator.ExecuteDDE("SET 2 LN=\"B\"");//the button again, before any patient change: the same conversation connects again
					CollectionAssert.AreEqual(new[] { "Create Dexis|Patient" },_listCalls,string.Join("\r\n",_listCalls));
					Assert.AreEqual(2,server.CountConnects);
					List<string> listCommands=server.GetExecutes().Select(x => Encoding.ASCII.GetString(x)).ToList();
					CollectionAssert.AreEqual(new[] { "SET 1 LN=\"A\"\0","SET 2 LN=\"B\"\0" },listCommands,string.Join("\r\n",listCommands));
				}
				finally {
					DexisIntegrator.CloseConversation();//on this thread, the conversation's, and before the server ends
				}
			}
		}
		#endregion Dexis Integrator

		#region The DDE client
		[TestMethod]
		public void Client_CommandBytes_AreAsciiWithTheTerminatingNull_AsNDdeSentThem() {
			CollectionAssert.AreEqual(new byte[] { (byte)'X',(byte)'r',(byte)'a',(byte)'y',(byte)',',(byte)'1',0 },ODDdeClient.GetCommandBytes("Xray,1"));
			string nameAccented="J"+(char)0xE9+"r"+(char)0xF4+"me";//e acute, o circumflex
			CollectionAssert.AreEqual(new byte[] { (byte)'J',(byte)'?',(byte)'r',(byte)'?',(byte)'m',(byte)'e',0 },ODDdeClient.GetCommandBytes(nameAccented),
				"Not ASCII: '?', as Encoding.ASCII gives.");
			CollectionAssert.AreEqual(new byte[] { 4,0 },ODDdeClient.GetCommandBytes(((char)4).ToString()));
		}

		[TestMethod]
		public void Client_Default_IsTheRealClient_NotConnected() {
			ODDdeClient.FuncCreate=ODDdeClient.CreateDefault;
			using(IDdeConversation ddeConversation=ODDdeClient.Create("ProImage","Image")) {
				Assert.IsInstanceOfType(ddeConversation,typeof(ODDdeClient));
				Assert.AreEqual("ProImage",ddeConversation.Service);
				Assert.AreEqual("Image",ddeConversation.Topic);
				Assert.IsFalse(ddeConversation.IsConnected);
				Assert.IsFalse(ddeConversation.IsDisposed);
			}
		}

		[TestMethod]
		public void Client_RefusesWhatNDdeRefused_BeforeAnyDdeCall() {
			ThrowsExactly<ArgumentNullException>(() => new ODDdeClient(null,"Image"));
			ThrowsExactly<ArgumentException>(() => new ODDdeClient("","Image"));
			ThrowsExactly<ArgumentException>(() => new ODDdeClient("ProImage",new string('t',256)));
			using(ODDdeClient client=new ODDdeClient(new string('s',255),"Image")) {
				ThrowsExactly<ArgumentNullException>(() => client.Execute(null,2000));
				ThrowsExactly<ArgumentException>(() => client.Execute(new string('c',256),2000),"At most 255 characters.");
				ThrowsExactly<ArgumentException>(() => client.Execute("Xray",0),"The timeout must be positive.");
				ThrowsExactly<InvalidOperationException>(() => client.Execute(new string('c',255),2000),"Not connected yet.");
				ThrowsExactly<InvalidOperationException>(() => client.Disconnect(),"Not connected yet.");
			}
		}

		[TestMethod]
		public void Client_AfterDispose_ThrowsObjectDisposedException() {
			ODDdeClient client=new ODDdeClient("Dexis","Patient");
			client.Dispose();
			client.Dispose();//twice is fine
			Assert.IsTrue(client.IsDisposed);
			Assert.IsFalse(client.IsConnected);
			ThrowsExactly<ObjectDisposedException>(() => client.Connect(),"DexisIntegrator.ExecuteDDE retries on a new conversation.");
			ThrowsExactly<ObjectDisposedException>(() => client.Execute("SET 1",30000));
			ThrowsExactly<ObjectDisposedException>(() => client.Disconnect());
		}

		[TestMethod]
		public void Client_RunsItsCallsOnTheSynchronizingObject_AndThrowsTheirErrorsHere() {
			FakeSynchronizeInvoke fakeSync=new FakeSynchronizeInvoke();
			ODDdeClient client=new ODDdeClient("Dexis","Patient",fakeSync);
			ThrowsExactly<InvalidOperationException>(() => client.Execute("SET 1",30000),"Not connected: thrown there, rethrown here.");
			Assert.AreEqual(1,fakeSync.CountInvoke);
			client.Dispose();
			Assert.AreEqual(2,fakeSync.CountInvoke,"Dispose too.");
			Assert.IsTrue(client.IsDisposed);
		}

		[TestMethod]
		public void Client_DeclaresOnlyDdemlFunctions_ThatUser32Exports() {
			List<MethodInfo> listExterns=typeof(ODDdeClient).GetMethods(BindingFlags.NonPublic|BindingFlags.Static)
				.Where(x => x.GetCustomAttribute<DllImportAttribute>()!=null).ToList();
			List<string> listEntryPoints=listExterns.Select(x => x.GetCustomAttribute<DllImportAttribute>().EntryPoint).OrderBy(x => x,StringComparer.Ordinal).ToList();
			CollectionAssert.AreEqual(new[] { "DdeClientTransaction","DdeConnect","DdeCreateStringHandleA","DdeDisconnect","DdeFreeStringHandle",
				"DdeGetLastError","DdeInitializeA","DdeUninitialize" },listEntryPoints,string.Join(", ",listEntryPoints));
			IntPtr hModule=LoadLibrary("user32.dll");
			Assert.AreNotEqual(IntPtr.Zero,hModule);
			foreach(MethodInfo methodInfo in listExterns) {
				DllImportAttribute dllImportAttribute=methodInfo.GetCustomAttribute<DllImportAttribute>();
				Assert.AreEqual("user32.dll",dllImportAttribute.Value);
				Assert.IsTrue(dllImportAttribute.ExactSpelling,dllImportAttribute.EntryPoint);
				Assert.AreNotEqual(IntPtr.Zero,GetProcAddress(hModule,dllImportAttribute.EntryPoint),dllImportAttribute.EntryPoint+" is not in user32.dll.");
			}
			//The ANSI functions, as NDde 1.03 used: the server sees CP_WINANSI names.
			Assert.AreEqual(CharSet.Ansi,listExterns.Single(x => x.GetCustomAttribute<DllImportAttribute>().EntryPoint=="DdeCreateStringHandleA")
				.GetCustomAttribute<DllImportAttribute>().CharSet);
		}

		[TestMethod]
		public void Client_DeclarationsFitX64Too() {
			//Handles are pointer-sized, DWORDs are uint, ULONG_PTR is UIntPtr: the same declarations work in a 64-bit process.
			MethodInfo methodTransaction=typeof(ODDdeClient).GetMethod("DdeClientTransaction",BindingFlags.NonPublic|BindingFlags.Static);
			CollectionAssert.AreEqual(new[] { typeof(byte[]),typeof(uint),typeof(IntPtr),typeof(IntPtr),typeof(uint),typeof(uint),typeof(uint),typeof(uint).MakeByRefType() },
				methodTransaction.GetParameters().Select(x => x.ParameterType).ToArray());
			Assert.AreEqual(typeof(IntPtr),methodTransaction.ReturnType);
			Type typeCallback=typeof(ODDdeClient).GetNestedType("DdeCallback",BindingFlags.NonPublic);
			CollectionAssert.AreEqual(new[] { typeof(uint),typeof(uint),typeof(IntPtr),typeof(IntPtr),typeof(IntPtr),typeof(IntPtr),typeof(UIntPtr),typeof(UIntPtr) },
				typeCallback.GetMethod("Invoke").GetParameters().Select(x => x.ParameterType).ToArray());
			Assert.AreEqual(typeof(IntPtr),typeCallback.GetMethod("Invoke").ReturnType);
		}

		[TestMethod]
		public void Client_DdemlConstants_AreThoseOfDdemlH() {
			//ddeml.h (Windows SDK 10.0.26100) and winuser.h; NDde 1.03 used the same values. Every private constant of the client is listed, so a new
			//one is checked too.
			Dictionary<string,object> dictExpected=new Dictionary<string,object> {
				{ "APPCMD_CLIENTONLY",(uint)0x00000010 },
				{ "CBF_SKIP_REGISTRATIONS",(uint)0x00080000 },
				{ "CBF_SKIP_UNREGISTRATIONS",(uint)0x00100000 },
				{ "CP_WINANSI",1004 },
				{ "CF_TEXT",(uint)1 },
				{ "XCLASS_FLAGS",(uint)0x4000 },
				{ "XCLASS_NOTIFICATION",(uint)0x8000 },
				{ "XTYPF_NOBLOCK",(uint)0x0002 },
				{ "XTYP_EXECUTE",(uint)0x4050 },//(0x0050 | XCLASS_FLAGS)
				{ "XTYP_DISCONNECT",(uint)0x80C2 },//(0x00C0 | XCLASS_NOTIFICATION | XTYPF_NOBLOCK)
				{ "DMLERR_NO_ERROR",(uint)0 },
			};
			List<FieldInfo> listConstants=typeof(ODDdeClient).GetFields(BindingFlags.NonPublic|BindingFlags.Static).Where(x => x.IsLiteral).ToList();
			CollectionAssert.AreEquivalent(dictExpected.Keys.ToList(),listConstants.Select(x => x.Name).ToList(),
				string.Join(", ",listConstants.Select(x => x.Name)));
			foreach(FieldInfo fieldInfo in listConstants) {
				object value=fieldInfo.GetRawConstantValue();
				Assert.AreEqual(dictExpected[fieldInfo.Name],value,fieldInfo.Name+" is 0x"+Convert.ToInt64(value).ToString("X")+", of type "+value.GetType().Name+".");
			}
		}

		[TestMethod]
		public void Client_XtypDisconnectForItsConversation_EndsIt_AndNothingElseDoes() {
			FieldInfo fieldHConv=typeof(ODDdeClient).GetField("_hConv",BindingFlags.NonPublic|BindingFlags.Instance);
			MethodInfo methodCallback=typeof(ODDdeClient).GetMethod("OnDdeCallback",BindingFlags.NonPublic|BindingFlags.Instance);
			using(ODDdeClient client=new ODDdeClient("Dexis","Patient")) {
				//The conversation handle as DdeConnect gives it. No DDEML instance is opened, so nothing here (Dispose neither) makes a DDE call.
				IntPtr hConv=new IntPtr(0x1234);
				fieldHConv.SetValue(client,hConv);
				Assert.IsTrue(client.IsConnected);
				Func<uint,IntPtr,object> funcCallback=(uType,hconv) => methodCallback.Invoke(client,
					new object[] { uType,(uint)0,hconv,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,UIntPtr.Zero,UIntPtr.Zero });
				Assert.AreEqual(IntPtr.Zero,funcCallback(0x8002,hConv));//XTYP_ERROR
				Assert.IsTrue(client.IsConnected,"XTYP_ERROR does not end the conversation.");
				funcCallback(0x80C2,new IntPtr(0x5678));
				Assert.IsTrue(client.IsConnected,"XTYP_DISCONNECT for another conversation.");
				funcCallback(0x00C2,hConv);
				Assert.IsTrue(client.IsConnected,"0x00C2 (XTYP_DISCONNECT without XCLASS_NOTIFICATION) is not what DDEML passes.");
				Assert.AreEqual(IntPtr.Zero,funcCallback(0x80C2,hConv));//XTYP_DISCONNECT
				Assert.IsFalse(client.IsConnected,"The server ended this conversation.");
				ThrowsExactly<InvalidOperationException>(() => client.Execute("SET 1",30000),"Not connected any more: Connect opens a new one.");
			}
		}

		[TestMethod]
		public void Client_OnWindowsDdeml_SendsAsciiWithTheNull_AndSeesTheServerEndTheConversation() {
			using(DdeLoopbackServer server=DdeLoopbackServer.Start()) {
				using(ODDdeClient client=new ODDdeClient(server.Service,server.Topic)) {
					client.Connect();
					Assert.IsTrue(client.IsConnected);
					Assert.AreEqual(1,server.CountConnects);
					string command="SET 1234 LN=\"J"+(char)0xE9+"r"+(char)0xF4+"me\" FN=\"Pat\"";//e acute, o circumflex
					client.Execute(command,5000);
					List<byte[]> listExecutes=server.GetExecutes();
					Assert.AreEqual(1,listExecutes.Count);
					//An ANSI server gets the client's bytes as they are: ASCII ('?' for the rest) and the terminating null.
					byte[] arrayExpected=Encoding.ASCII.GetBytes("SET 1234 LN=\"J?r?me\" FN=\"Pat\"\0");
					CollectionAssert.AreEqual(arrayExpected,listExecutes[0],BitConverter.ToString(listExecutes[0]));
					//The server ends the conversation, as the Dexis Integrator does when it is closed.
					server.EndConversation();
					Assert.IsTrue(DdeLoopbackServer.PumpMessagesUntil(() => !client.IsConnected && server.CountServerDisconnects==1,5000),
						"XTYP_DISCONNECT reached the client: IsConnected is false.");
					ThrowsExactly<InvalidOperationException>(() => client.Execute("SET 2",5000),"Not connected.");
					//The same client connects again, as the Dexis Integrator link does on the next button press.
					client.Connect();
					client.Execute("SET 2",5000);
					Assert.AreEqual(2,server.CountConnects);
					listExecutes=server.GetExecutes();
					Assert.AreEqual(2,listExecutes.Count);
					CollectionAssert.AreEqual(new byte[] { (byte)'S',(byte)'E',(byte)'T',(byte)' ',(byte)'2',0 },listExecutes[1],BitConverter.ToString(listExecutes[1]));
				}
			}
		}

		[TestMethod]
		public void Client_OnWindowsDdeml_UnicodeServerGetsTheCommandToo() {
			//A Unicode DDE server (DdeInitializeW). Windows translates an execute command between an ANSI and a Unicode partner, so such a server
			//gets the same text, as it did from NDde's ANSI instance.
			using(DdeLoopbackServer server=DdeLoopbackServer.Start(isUnicode:true)) {
				using(ODDdeClient client=new ODDdeClient(server.Service,server.Topic)) {
					client.Connect();
					client.Execute("Xray,1234,Pat",5000);
					byte[] arrayBytes=server.GetExecutes().Single();
					CollectionAssert.AreEqual(Encoding.Unicode.GetBytes("Xray,1234,Pat\0"),arrayBytes,BitConverter.ToString(arrayBytes));
				}
			}
		}

		[TestMethod]
		public void Client_OnWindowsDdeml_DisconnectAndDispose_EndTheConversationForTheServer() {
			using(DdeLoopbackServer server=DdeLoopbackServer.Start()) {
				ODDdeClient client=new ODDdeClient(server.Service,server.Topic);
				try {
					client.Connect();
					client.Disconnect();
					Assert.IsFalse(client.IsConnected);
					Assert.IsTrue(DdeLoopbackServer.PumpMessagesUntil(() => server.CountClientDisconnects==1,5000),"The server saw Disconnect end it.");
					client.Connect();
					Assert.AreEqual(2,server.CountConnects,"Connect again after Disconnect, on the same DDEML instance.");
				}
				finally {
					client.Dispose();
				}
				Assert.IsTrue(DdeLoopbackServer.PumpMessagesUntil(() => server.CountClientDisconnects==2,5000),"The server saw Dispose end it.");
				Assert.IsTrue(client.IsDisposed);
			}
		}

		[TestMethod]
		public void ClientException_NamesTheDdemlError() {
			ODDdeException ex=new ODDdeException("Connecting to the DDE server ProImage|Image",0x400A);
			Assert.AreEqual((uint)0x400A,ex.DmlErrorCode);
			Assert.AreEqual("Connecting to the DDE server ProImage|Image failed: DMLERR_NO_CONV_ESTABLISHED (0x400A): no DDE server answered (is the program running?).",
				ex.Message);
			StringAssert.StartsWith(ODDdeException.GetErrorText(0x4005),"DMLERR_EXECACKTIMEOUT (0x4005)");
			Assert.AreEqual("DDE error (0x1234): unknown",ODDdeException.GetErrorText(0x1234));
		}
		#endregion The DDE client

		[DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
		private static extern IntPtr LoadLibrary(string lpFileName);

		[DllImport("kernel32.dll",CharSet=CharSet.Ansi,ExactSpelling=true,BestFitMapping=false)]
		private static extern IntPtr GetProcAddress(IntPtr hModule,string procName);

		///<summary>A DDE conversation that records its calls instead of talking to a server.</summary>
		private class FakeDdeConversation:IDdeConversation {
			private readonly List<string> _listCalls;
			public Exception ExceptionOnConnect;
			public Exception ExceptionOnExecute;

			public FakeDdeConversation(string service,string topic,List<string> listCalls) {
				Service=service;
				Topic=topic;
				_listCalls=listCalls;
			}

			public string Service { get; }
			public string Topic { get; }
			public bool IsConnected { get; set; }
			public bool IsDisposed { get; private set; }

			public void Connect() {
				ThrowIfDisposed();
				_listCalls.Add("Connect");
				if(ExceptionOnConnect!=null) {
					throw ExceptionOnConnect;
				}
				IsConnected=true;
			}

			public void Execute(string command,int timeoutMs) {
				ThrowIfDisposed();
				_listCalls.Add("Execute "+timeoutMs+" "+command);
				if(ExceptionOnExecute!=null) {
					throw ExceptionOnExecute;
				}
			}

			public void Disconnect() {
				ThrowIfDisposed();
				_listCalls.Add("Disconnect");
				IsConnected=false;
			}

			public void Dispose() {
				_listCalls.Add("Dispose");
				IsConnected=false;
				IsDisposed=true;
			}

			private void ThrowIfDisposed() {
				if(IsDisposed) {
					throw new ObjectDisposedException(nameof(FakeDdeConversation));
				}
			}
		}

		///<summary>Says every call needs Invoke and runs it at once, counting the calls.</summary>
		private class FakeSynchronizeInvoke:ISynchronizeInvoke {
			public int CountInvoke;

			public bool InvokeRequired => true;

			public IAsyncResult BeginInvoke(Delegate method,object[] args) {
				throw new NotSupportedException();
			}

			public object EndInvoke(IAsyncResult result) {
				throw new NotSupportedException();
			}

			public object Invoke(Delegate method,object[] args) {
				CountInvoke++;
				return method.DynamicInvoke(args);
			}
		}
	}
}
