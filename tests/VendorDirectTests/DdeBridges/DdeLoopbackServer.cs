// S0-R (2026-11-08): a DDE server inside the test process, so the in-house client CodeBase.ODDdeClient is tested against Windows' own DDEML
// end to end, without ProImage, the Dexis Integrator or Vipersoft: a DDEML server instance on a thread of its own with a message loop, a
// service name no program uses, the connections it accepts, the bytes of every execute command it gets (DdeGetData), and a conversation ended
// from the server's side (DdeDisconnect), as when the Integrator is closed. Written from the Win32 DDEML documentation and ddeml.h. Nothing
// leaves this computer: DDE runs between windows of this desktop.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace VendorDirectTests.DdeBridges {
	///<summary>A DDEML server for one service and topic, on its own thread. It accepts every conversation on the topic, records each execute
	///command's bytes and takes it (DDE_FACK). isUnicode: a Unicode instance (DdeInitializeW) instead of an ANSI one (DdeInitializeA). Dispose
	///ends it; dispose the clients first.</summary>
	public sealed class DdeLoopbackServer:IDisposable {
		#region DDEML (ddeml.h)
		private const uint APPCLASS_STANDARD=0x00000000;
		private const uint CBF_SKIP_REGISTRATIONS=0x00080000;
		private const uint CBF_SKIP_UNREGISTRATIONS=0x00100000;
		private const int CP_WINANSI=1004;
		private const int CP_WINUNICODE=1200;
		private const uint DNS_REGISTER=0x0001;
		private const uint DNS_UNREGISTER=0x0002;
		private const int DDE_FACK=0x8000;
		private const uint XTYP_CONNECT=0x0060|0x1000|0x0002;//XCLASS_BOOL, XTYPF_NOBLOCK: 0x1062
		private const uint XTYP_CONNECT_CONFIRM=0x0070|0x8000|0x0002;//XCLASS_NOTIFICATION, XTYPF_NOBLOCK: 0x8072
		private const uint XTYP_EXECUTE=0x0050|0x4000;//XCLASS_FLAGS: 0x4050
		private const uint XTYP_DISCONNECT=0x00C0|0x8000|0x0002;//XCLASS_NOTIFICATION, XTYPF_NOBLOCK: 0x80C2

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate IntPtr DdeCallback(uint uType,uint uFmt,IntPtr hconv,IntPtr hsz1,IntPtr hsz2,IntPtr hdata,UIntPtr dwData1,UIntPtr dwData2);

		[DllImport("user32.dll",EntryPoint="DdeInitializeA",ExactSpelling=true)]
		private static extern uint DdeInitializeA(ref uint pidInst,DdeCallback pfnCallback,uint afCmd,uint ulRes);

		[DllImport("user32.dll",EntryPoint="DdeInitializeW",ExactSpelling=true)]
		private static extern uint DdeInitializeW(ref uint pidInst,DdeCallback pfnCallback,uint afCmd,uint ulRes);

		[DllImport("user32.dll",EntryPoint="DdeUninitialize",ExactSpelling=true)]
		private static extern bool DdeUninitialize(uint idInst);

		[DllImport("user32.dll",EntryPoint="DdeCreateStringHandleA",ExactSpelling=true,CharSet=CharSet.Ansi,BestFitMapping=false)]
		private static extern IntPtr DdeCreateStringHandleA(uint idInst,string psz,int iCodePage);

		[DllImport("user32.dll",EntryPoint="DdeCreateStringHandleW",ExactSpelling=true,CharSet=CharSet.Unicode)]
		private static extern IntPtr DdeCreateStringHandleW(uint idInst,string psz,int iCodePage);

		[DllImport("user32.dll",EntryPoint="DdeFreeStringHandle",ExactSpelling=true)]
		private static extern bool DdeFreeStringHandle(uint idInst,IntPtr hsz);

		[DllImport("user32.dll",EntryPoint="DdeCmpStringHandles",ExactSpelling=true)]
		private static extern int DdeCmpStringHandles(IntPtr hsz1,IntPtr hsz2);

		[DllImport("user32.dll",EntryPoint="DdeNameService",ExactSpelling=true)]
		private static extern IntPtr DdeNameService(uint idInst,IntPtr hsz1,IntPtr hsz2,uint afCmd);

		[DllImport("user32.dll",EntryPoint="DdeGetData",ExactSpelling=true)]
		private static extern uint DdeGetData(IntPtr hData,byte[] pDst,uint cbMax,uint cbOff);

		[DllImport("user32.dll",EntryPoint="DdeDisconnect",ExactSpelling=true)]
		private static extern bool DdeDisconnect(IntPtr hConv);
		#endregion DDEML

		#region Messages
		private const uint WM_APP=0x8000;
		///<summary>Posted to the server's thread: end the open conversation from the server's side.</summary>
		private const uint WM_APP_END_CONVERSATION=WM_APP+1;
		private const uint WM_QUIT=0x0012;
		private const uint PM_NOREMOVE=0x0000;
		private const uint PM_REMOVE=0x0001;
		private const uint QS_ALLINPUT=0x04FF;

		[StructLayout(LayoutKind.Sequential)]
		private struct MSG {
			public IntPtr hwnd;
			public uint message;
			public IntPtr wParam;
			public IntPtr lParam;
			public uint time;
			public int ptX;
			public int ptY;
		}

		[DllImport("user32.dll",EntryPoint="GetMessageW",ExactSpelling=true)]
		private static extern int GetMessage(out MSG lpMsg,IntPtr hWnd,uint wMsgFilterMin,uint wMsgFilterMax);

		[DllImport("user32.dll",EntryPoint="PeekMessageW",ExactSpelling=true)]
		private static extern bool PeekMessage(out MSG lpMsg,IntPtr hWnd,uint wMsgFilterMin,uint wMsgFilterMax,uint wRemoveMsg);

		[DllImport("user32.dll",EntryPoint="TranslateMessage",ExactSpelling=true)]
		private static extern bool TranslateMessage(ref MSG lpMsg);

		[DllImport("user32.dll",EntryPoint="DispatchMessageW",ExactSpelling=true)]
		private static extern IntPtr DispatchMessage(ref MSG lpMsg);

		[DllImport("user32.dll",EntryPoint="PostThreadMessageW",ExactSpelling=true)]
		private static extern bool PostThreadMessage(uint idThread,uint msg,IntPtr wParam,IntPtr lParam);

		[DllImport("user32.dll",EntryPoint="MsgWaitForMultipleObjects",ExactSpelling=true)]
		private static extern uint MsgWaitForMultipleObjects(uint nCount,IntPtr pHandles,bool fWaitAll,uint dwMilliseconds,uint dwWakeMask);

		[DllImport("kernel32.dll",EntryPoint="GetCurrentThreadId",ExactSpelling=true)]
		private static extern uint GetCurrentThreadId();
		#endregion Messages

		///<summary>The service name, unique to this server, so no program on this computer answers it or is reached.</summary>
		public readonly string Service;
		public readonly string Topic;
		public readonly bool IsUnicode;
		private readonly DdeCallback _ddeCallback;
		private readonly Thread _thread;
		private readonly ManualResetEvent _eventStarted=new ManualResetEvent(false);
		private readonly object _lock=new object();
		private readonly List<byte[]> _listExecutes=new List<byte[]>();
		private Exception _exceptionStart;
		private uint _threadIdNative;
		private uint _idInst;
		private IntPtr _hszService;
		private IntPtr _hszTopic;
		///<summary>The open conversation, on the server's thread only.</summary>
		private IntPtr _hConv;
		private int _countConnects;
		private int _countClientDisconnects;
		private int _countServerDisconnects;

		private DdeLoopbackServer(string topic,bool isUnicode) {
			Service="OpmsDdeTest"+Guid.NewGuid().ToString("N").Substring(0,12);
			Topic=topic;
			IsUnicode=isUnicode;
			_ddeCallback=OnDdeCallback;
			_thread=new Thread(Run) { IsBackground=true,Name="DDE loopback server" };
		}

		///<summary>Starts a server and returns once its service name is registered.</summary>
		public static DdeLoopbackServer Start(string topic="Patient",bool isUnicode=false) {
			DdeLoopbackServer server=new DdeLoopbackServer(topic,isUnicode);
			server._thread.Start();
			if(!server._eventStarted.WaitOne(10000)) {
				throw new TimeoutException("The DDE loopback server did not start.");
			}
			if(server._exceptionStart!=null) {
				throw new InvalidOperationException("The DDE loopback server did not start.",server._exceptionStart);
			}
			return server;
		}

		///<summary>The bytes of every execute command, in order.</summary>
		public List<byte[]> GetExecutes() {
			lock(_lock) {
				return new List<byte[]>(_listExecutes);
			}
		}

		///<summary>Conversations the server accepted (XTYP_CONNECT_CONFIRM).</summary>
		public int CountConnects {
			get { lock(_lock) { return _countConnects; } }
		}

		///<summary>Conversations the client ended (XTYP_DISCONNECT).</summary>
		public int CountClientDisconnects {
			get { lock(_lock) { return _countClientDisconnects; } }
		}

		///<summary>Conversations the server ended itself (EndConversation).</summary>
		public int CountServerDisconnects {
			get { lock(_lock) { return _countServerDisconnects; } }
		}

		///<summary>Asks the server's thread to end the open conversation (DdeDisconnect), as a server program does when it closes, and returns at
		///once. The client sees it only while its own thread runs messages: see PumpMessagesUntil.</summary>
		public void EndConversation() {
			if(!PostThreadMessage(_threadIdNative,WM_APP_END_CONVERSATION,IntPtr.Zero,IntPtr.Zero)) {
				throw new InvalidOperationException("The DDE loopback server's thread did not take the message.");
			}
		}

		///<summary>Runs the calling thread's messages (DDEML delivers a client's notifications through its window) until condition is true or
		///timeoutMs passed. Returns condition's last value.</summary>
		public static bool PumpMessagesUntil(Func<bool> condition,int timeoutMs) {
			DateTime dateTimeEnd=DateTime.UtcNow.AddMilliseconds(timeoutMs);
			while(true) {
				while(PeekMessage(out MSG msg,IntPtr.Zero,0,0,PM_REMOVE)) {
					TranslateMessage(ref msg);
					DispatchMessage(ref msg);
				}
				if(condition()) {
					return true;
				}
				if(DateTime.UtcNow>=dateTimeEnd) {
					return condition();
				}
				MsgWaitForMultipleObjects(0,IntPtr.Zero,false,20,QS_ALLINPUT);
			}
		}

		public void Dispose() {
			if(_threadIdNative!=0 && _thread.IsAlive) {
				PostThreadMessage(_threadIdNative,WM_QUIT,IntPtr.Zero,IntPtr.Zero);
				_thread.Join(10000);
			}
			_eventStarted.Dispose();
		}

		private void Run() {
			try {
				_threadIdNative=GetCurrentThreadId();
				PeekMessage(out MSG _,IntPtr.Zero,0,0,PM_NOREMOVE);//gives the thread its message queue before EndConversation can post to it
				uint idInst=0;
				uint result;
				if(IsUnicode) {
					result=DdeInitializeW(ref idInst,_ddeCallback,APPCLASS_STANDARD|CBF_SKIP_REGISTRATIONS|CBF_SKIP_UNREGISTRATIONS,0);
				}
				else {
					result=DdeInitializeA(ref idInst,_ddeCallback,APPCLASS_STANDARD|CBF_SKIP_REGISTRATIONS|CBF_SKIP_UNREGISTRATIONS,0);
				}
				if(result!=0) {
					throw new InvalidOperationException("DdeInitialize failed: 0x"+result.ToString("X4"));
				}
				_idInst=idInst;
				if(IsUnicode) {
					_hszService=DdeCreateStringHandleW(idInst,Service,CP_WINUNICODE);
					_hszTopic=DdeCreateStringHandleW(idInst,Topic,CP_WINUNICODE);
				}
				else {
					_hszService=DdeCreateStringHandleA(idInst,Service,CP_WINANSI);
					_hszTopic=DdeCreateStringHandleA(idInst,Topic,CP_WINANSI);
				}
				if(_hszService==IntPtr.Zero || _hszTopic==IntPtr.Zero) {
					throw new InvalidOperationException("DdeCreateStringHandle failed.");
				}
				if(DdeNameService(idInst,_hszService,IntPtr.Zero,DNS_REGISTER)==IntPtr.Zero) {
					throw new InvalidOperationException("DdeNameService failed.");
				}
			}
			catch(Exception ex) {
				_exceptionStart=ex;
				Uninitialize();
				_eventStarted.Set();
				return;
			}
			_eventStarted.Set();
			while(GetMessage(out MSG msg,IntPtr.Zero,0,0)>0) {
				if(msg.hwnd==IntPtr.Zero && msg.message==WM_APP_END_CONVERSATION) {
					IntPtr hConv=_hConv;
					_hConv=IntPtr.Zero;
					if(hConv!=IntPtr.Zero && DdeDisconnect(hConv)) {
						lock(_lock) {
							_countServerDisconnects++;
						}
					}
					continue;
				}
				TranslateMessage(ref msg);
				DispatchMessage(ref msg);
			}
			Uninitialize();
		}

		private void Uninitialize() {
			if(_idInst==0) {
				return;
			}
			if(_hszService!=IntPtr.Zero) {
				DdeNameService(_idInst,_hszService,IntPtr.Zero,DNS_UNREGISTER);
				DdeFreeStringHandle(_idInst,_hszService);
			}
			if(_hszTopic!=IntPtr.Zero) {
				DdeFreeStringHandle(_idInst,_hszTopic);
			}
			DdeUninitialize(_idInst);
			_idInst=0;
		}

		///<summary>The server's DDEML callback, on the server's thread. Nothing here may throw into DDEML.</summary>
		private IntPtr OnDdeCallback(uint uType,uint uFmt,IntPtr hconv,IntPtr hsz1,IntPtr hsz2,IntPtr hdata,UIntPtr dwData1,UIntPtr dwData2) {
			try {
				switch(uType) {
					case XTYP_CONNECT://hsz1 the topic, hsz2 the service
						if(DdeCmpStringHandles(hsz1,_hszTopic)==0 && DdeCmpStringHandles(hsz2,_hszService)==0) {
							return new IntPtr(1);//TRUE: accepted
						}
						return IntPtr.Zero;
					case XTYP_CONNECT_CONFIRM:
						_hConv=hconv;
						lock(_lock) {
							_countConnects++;
						}
						return IntPtr.Zero;
					case XTYP_EXECUTE:
						uint size=DdeGetData(hdata,null,0,0);
						byte[] arrayBytes=new byte[size];
						if(size>0) {
							DdeGetData(hdata,arrayBytes,size,0);
						}
						lock(_lock) {
							_listExecutes.Add(arrayBytes);
						}
						return new IntPtr(DDE_FACK);
					case XTYP_DISCONNECT:
						if(hconv==_hConv) {
							_hConv=IntPtr.Zero;
						}
						lock(_lock) {
							_countClientDisconnects++;
						}
						return IntPtr.Zero;
				}
			}
			catch {
				//Nothing may throw into DDEML.
			}
			return IntPtr.Zero;
		}
	}
}
