// S0-R (2026-11-08): a small DDE client for the DentX, Dexis Integrator and Vipersoft program links, in place of NDde.dll, whose own shared
// source licence does not fit the GPL (owner question REL-FILES 2; docs/port-notes/S0-R.md). Written from the Win32 documentation of the DDE
// Management Library (DDEML): DdeInitialize, DdeConnect, DdeClientTransaction (XTYP_EXECUTE only), DdeDisconnect and DdeUninitialize. It sends
// what NDde 1.03 sent, so the three links behave as before: an ANSI instance, CP_WINANSI service and topic names, the command as ASCII text
// (CF_TEXT) with its terminating null, and no command, service or topic longer than 255 characters.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CodeBase {
	///<summary>A client's conversation with one DDE server (a service and a topic) that sends it execute commands. All a program link needs: no
	///requests, pokes or advise loops. ODDdeClient is the real one; tests set ODDdeClient.FuncCreate to a fake.</summary>
	public interface IDdeConversation:IDisposable {
		///<summary>The server's service name, e.g. "ProImage".</summary>
		string Service { get; }
		///<summary>The topic, e.g. "Image".</summary>
		string Topic { get; }
		///<summary>True from Connect until Disconnect, Dispose or the server ending the conversation.</summary>
		bool IsConnected { get; }
		///<summary>True once Dispose ran. A disposed conversation throws ObjectDisposedException.</summary>
		bool IsDisposed { get; }
		///<summary>Opens the conversation. Throws ODDdeException when no server answers, InvalidOperationException when already connected.</summary>
		void Connect();
		///<summary>Sends the command (at most 255 characters) and waits up to timeoutMs (more than 0) for the server to take it. Throws ODDdeException
		///when the server refuses it or does not answer in time, InvalidOperationException when not connected.</summary>
		void Execute(string command,int timeoutMs);
		///<summary>Ends the conversation. Throws InvalidOperationException when not connected.</summary>
		void Disconnect();
	}

	///<summary>A DDE client conversation on Windows' DDEML (user32.dll). Each object has its own DDEML instance, opened by the first Connect and
	///closed by Dispose. A DDEML instance belongs to the thread that opened it: without a synchronizing object every call must come from that thread
	///(it throws InvalidOperationException otherwise); with one (a window), every call is run on its thread, as NDde's DdeContext(form) did.
	///Synchronous transactions run DDEML's own message loop while they wait, so the calling window stays painted.</summary>
	public class ODDdeClient:IDdeConversation {
		#region Factory
		///<summary>Makes every conversation the program links open: (service, topic, synchronizing object or null). Tests set a fake that records the
		///calls and set CreateDefault back afterwards.</summary>
		public static Func<string,string,ISynchronizeInvoke,IDdeConversation> FuncCreate=CreateDefault;

		///<summary>A new conversation, not yet connected. synchronizingObject: the window whose thread runs the DDE calls, or null for the calling
		///thread.</summary>
		public static IDdeConversation Create(string service,string topic,ISynchronizeInvoke synchronizingObject=null) {
			return FuncCreate(service,topic,synchronizingObject);
		}

		///<summary>The default of FuncCreate: a real ODDdeClient.</summary>
		public static IDdeConversation CreateDefault(string service,string topic,ISynchronizeInvoke synchronizingObject) {
			return new ODDdeClient(service,topic,synchronizingObject);
		}
		#endregion Factory

		#region DDEML
		///<summary>The longest service name, topic or command. NDde refused longer ones, and the Dexis Integrator's commands are kept within it.</summary>
		public const int MAX_LENGTH=255;

		private const uint APPCMD_CLIENTONLY=0x00000010;
		private const uint CBF_SKIP_REGISTRATIONS=0x00080000;
		private const uint CBF_SKIP_UNREGISTRATIONS=0x00100000;
		private const int CP_WINANSI=1004;
		private const uint CF_TEXT=1;
		//The transaction types are built as ddeml.h builds them: a number, its class and its flags.
		private const uint XCLASS_FLAGS=0x4000;
		private const uint XCLASS_NOTIFICATION=0x8000;
		private const uint XTYPF_NOBLOCK=0x0002;
		///<summary>0x4050.</summary>
		private const uint XTYP_EXECUTE=0x0050|XCLASS_FLAGS;
		///<summary>0x80C2, a notification: what DDEML passes the callback when the server ended the conversation.</summary>
		private const uint XTYP_DISCONNECT=0x00C0|XCLASS_NOTIFICATION|XTYPF_NOBLOCK;
		private const uint DMLERR_NO_ERROR=0;

		///<summary>DDEML calls the instance back with this (stdcall, the WINAPI convention on x86; x64 has one convention).</summary>
		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate IntPtr DdeCallback(uint uType,uint uFmt,IntPtr hconv,IntPtr hsz1,IntPtr hsz2,IntPtr hdata,UIntPtr dwData1,UIntPtr dwData2);

		[DllImport("user32.dll",EntryPoint="DdeInitializeA",ExactSpelling=true)]
		private static extern uint DdeInitialize(ref uint pidInst,DdeCallback pfnCallback,uint afCmd,uint ulRes);

		[DllImport("user32.dll",EntryPoint="DdeUninitialize",ExactSpelling=true)]
		private static extern bool DdeUninitialize(uint idInst);

		[DllImport("user32.dll",EntryPoint="DdeCreateStringHandleA",ExactSpelling=true,CharSet=CharSet.Ansi,BestFitMapping=false)]
		private static extern IntPtr DdeCreateStringHandle(uint idInst,string psz,int iCodePage);

		[DllImport("user32.dll",EntryPoint="DdeFreeStringHandle",ExactSpelling=true)]
		private static extern bool DdeFreeStringHandle(uint idInst,IntPtr hsz);

		[DllImport("user32.dll",EntryPoint="DdeConnect",ExactSpelling=true)]
		private static extern IntPtr DdeConnect(uint idInst,IntPtr hszService,IntPtr hszTopic,IntPtr pCC);

		[DllImport("user32.dll",EntryPoint="DdeClientTransaction",ExactSpelling=true)]
		private static extern IntPtr DdeClientTransaction(byte[] pData,uint cbData,IntPtr hConv,IntPtr hszItem,uint wFmt,uint wType,uint dwTimeout,
			out uint pdwResult);

		[DllImport("user32.dll",EntryPoint="DdeDisconnect",ExactSpelling=true)]
		private static extern bool DdeDisconnect(IntPtr hConv);

		[DllImport("user32.dll",EntryPoint="DdeGetLastError",ExactSpelling=true)]
		private static extern uint DdeGetLastError(uint idInst);
		#endregion DDEML

		private readonly string _service;
		private readonly string _topic;
		private readonly ISynchronizeInvoke _synchronizingObject;
		///<summary>Held for the life of the instance, so the garbage collector never collects the delegate DDEML calls back.</summary>
		private readonly DdeCallback _ddeCallback;
		///<summary>The DDEML instance, 0 until the first Connect.</summary>
		private uint _idInst;
		///<summary>The managed id of the thread that opened _idInst.</summary>
		private int _threadIdInst;
		///<summary>The conversation, IntPtr.Zero when there is none.</summary>
		private IntPtr _hConv=IntPtr.Zero;
		private bool _isDisposed;

		///<summary>A conversation with service and topic (each 1 to 255 characters), not yet connected. synchronizingObject: the window whose thread
		///runs the DDE calls, or null for the calling thread.</summary>
		public ODDdeClient(string service,string topic,ISynchronizeInvoke synchronizingObject=null) {
			ValidateName(service,nameof(service));
			ValidateName(topic,nameof(topic));
			_service=service;
			_topic=topic;
			_synchronizingObject=synchronizingObject;
			_ddeCallback=OnDdeCallback;
		}

		public string Service => _service;

		public string Topic => _topic;

		public bool IsConnected => !_isDisposed && _hConv!=IntPtr.Zero;

		public bool IsDisposed => _isDisposed;

		public void Connect() {
			ThrowIfDisposed();
			RunOnThread(ConnectOnThread);
		}

		public void Execute(string command,int timeoutMs) {
			ThrowIfDisposed();
			if(command==null) {
				throw new ArgumentNullException(nameof(command));
			}
			if(command.Length>MAX_LENGTH) {
				throw new ArgumentException("A DDE command has at most "+MAX_LENGTH+" characters; this one has "+command.Length+".",nameof(command));
			}
			if(timeoutMs<=0) {
				throw new ArgumentException("The timeout must be more than 0 milliseconds.",nameof(timeoutMs));
			}
			RunOnThread(() => ExecuteOnThread(command,timeoutMs));
		}

		public void Disconnect() {
			ThrowIfDisposed();
			RunOnThread(DisconnectOnThread);
		}

		///<summary>Ends the conversation and closes the DDEML instance. Never throws. If the synchronizing window is already gone, or this is the wrong
		///thread, the instance cannot be closed here and is left to Windows, which ends it with the program.</summary>
		public void Dispose() {
			if(_isDisposed) {
				return;
			}
			try {
				RunOnThread(DisposeOnThread);
			}
			catch(InvalidOperationException) {
				//The synchronizing window was disposed or has no handle any more (Invoke cannot run; ObjectDisposedException is one of these), or
				//this is not the instance's thread.
			}
			finally {
				_isDisposed=true;
			}
		}

		///<summary>The bytes of an execute command as DDEML gets them: ASCII text (any other character becomes '?') and the terminating null, as NDde
		///sent them.</summary>
		public static byte[] GetCommandBytes(string command) {
			return Encoding.ASCII.GetBytes(command+"\0");
		}

		public override string ToString() {
			return _service+"|"+_topic;
		}

		private void ConnectOnThread() {
			ThrowIfDisposed();
			if(IsConnected) {
				throw new InvalidOperationException("Already connected to "+this+".");
			}
			if(_idInst==0) {
				uint idInst=0;
				uint result=DdeInitialize(ref idInst,_ddeCallback,APPCMD_CLIENTONLY|CBF_SKIP_REGISTRATIONS|CBF_SKIP_UNREGISTRATIONS,0);
				if(result!=DMLERR_NO_ERROR) {
					throw new ODDdeException("Opening DDE for "+this,result);
				}
				_idInst=idInst;
				_threadIdInst=Thread.CurrentThread.ManagedThreadId;
			}
			ThrowIfWrongThread();
			IntPtr hszService=IntPtr.Zero;
			IntPtr hszTopic=IntPtr.Zero;
			try {
				hszService=DdeCreateStringHandle(_idInst,_service,CP_WINANSI);
				hszTopic=DdeCreateStringHandle(_idInst,_topic,CP_WINANSI);
				if(hszService==IntPtr.Zero || hszTopic==IntPtr.Zero) {
					throw new ODDdeException("Naming the DDE service and topic "+this,DdeGetLastError(_idInst));
				}
				IntPtr hConv=DdeConnect(_idInst,hszService,hszTopic,IntPtr.Zero);
				if(hConv==IntPtr.Zero) {
					throw new ODDdeException("Connecting to the DDE server "+this,DdeGetLastError(_idInst));
				}
				_hConv=hConv;
			}
			finally {
				if(hszTopic!=IntPtr.Zero) {
					DdeFreeStringHandle(_idInst,hszTopic);
				}
				if(hszService!=IntPtr.Zero) {
					DdeFreeStringHandle(_idInst,hszService);
				}
			}
		}

		private void ExecuteOnThread(string command,int timeoutMs) {
			ThrowIfDisposed();
			if(!IsConnected) {
				throw new InvalidOperationException("Not connected to "+this+".");
			}
			ThrowIfWrongThread();
			byte[] arrayData=GetCommandBytes(command);
			IntPtr result=DdeClientTransaction(arrayData,(uint)arrayData.Length,_hConv,IntPtr.Zero,CF_TEXT,XTYP_EXECUTE,(uint)timeoutMs,out uint _);
			if(result==IntPtr.Zero) {//TRUE (not a data handle) on success
				throw new ODDdeException("The DDE command to "+this,DdeGetLastError(_idInst));
			}
		}

		private void DisconnectOnThread() {
			ThrowIfDisposed();
			if(!IsConnected) {
				throw new InvalidOperationException("Not connected to "+this+".");
			}
			ThrowIfWrongThread();
			IntPtr hConv=_hConv;
			_hConv=IntPtr.Zero;
			DdeDisconnect(hConv);//false when the server ended it at the same moment; the conversation is over either way
		}

		private void DisposeOnThread() {
			if(_isDisposed) {
				return;
			}
			if(_idInst!=0) {
				ThrowIfWrongThread();
				if(_hConv!=IntPtr.Zero) {
					DdeDisconnect(_hConv);
					_hConv=IntPtr.Zero;
				}
				DdeUninitialize(_idInst);
				_idInst=0;
			}
			_isDisposed=true;
		}

		///<summary>DDEML's notifications. A client-only instance with registrations skipped gets XTYP_DISCONNECT (the server ended the conversation:
		///it closed, or its user closed it) and XTYP_ERROR. Nothing here may throw into DDEML.</summary>
		private IntPtr OnDdeCallback(uint uType,uint uFmt,IntPtr hconv,IntPtr hsz1,IntPtr hsz2,IntPtr hdata,UIntPtr dwData1,UIntPtr dwData2) {
			if(uType==XTYP_DISCONNECT && hconv!=IntPtr.Zero && hconv==_hConv) {
				_hConv=IntPtr.Zero;
			}
			return IntPtr.Zero;
		}

		///<summary>Runs action on the synchronizing object's thread when there is one and this is another thread, otherwise here. An exception of
		///action is thrown here as it was thrown there.</summary>
		private void RunOnThread(Action action) {
			if(_synchronizingObject==null || !_synchronizingObject.InvokeRequired) {
				action();
				return;
			}
			Exception exception=null;
			_synchronizingObject.Invoke(new Action(() => {
				try {
					action();
				}
				catch(Exception ex) {
					exception=ex;
				}
			}),null);
			if(exception!=null) {
				ExceptionDispatchInfo.Capture(exception).Throw();
			}
		}

		private void ThrowIfDisposed() {
			if(_isDisposed) {
				throw new ObjectDisposedException(nameof(ODDdeClient),"The DDE conversation with "+this+" was closed.");
			}
		}

		private void ThrowIfWrongThread() {
			if(_idInst!=0 && Thread.CurrentThread.ManagedThreadId!=_threadIdInst) {
				throw new InvalidOperationException("The DDE conversation with "+this+" belongs to the thread that opened it.");
			}
		}

		private static void ValidateName(string name,string paramName) {
			if(name==null) {
				throw new ArgumentNullException(paramName);
			}
			if(name.Length==0 || name.Length>MAX_LENGTH) {
				throw new ArgumentException("A DDE service name or topic has 1 to "+MAX_LENGTH+" characters.",paramName);
			}
		}
	}

	///<summary>A DDEML function failed. DmlErrorCode is the DMLERR_ value DdeGetLastError (or DdeInitialize) gave.</summary>
	public class ODDdeException:Exception {
		public uint DmlErrorCode { get; }

		public ODDdeException(string action,uint dmlErrorCode):base(action+" failed: "+GetErrorText(dmlErrorCode)+".") {
			DmlErrorCode=dmlErrorCode;
		}

		///<summary>The name of a DMLERR_ value, its number and what it means (ddeml.h), e.g. "DMLERR_NO_CONV_ESTABLISHED (0x400A): no DDE server
		///answered (is the program running?)".</summary>
		public static string GetErrorText(uint dmlErrorCode) {
			string name;
			string meaning;
			switch(dmlErrorCode) {
				case 0x4000: name="DMLERR_ADVACKTIMEOUT"; meaning="an advise transaction timed out"; break;
				case 0x4001: name="DMLERR_BUSY"; meaning="the server is busy"; break;
				case 0x4002: name="DMLERR_DATAACKTIMEOUT"; meaning="a data transaction timed out"; break;
				case 0x4003: name="DMLERR_DLL_NOT_INITIALIZED"; meaning="DDE was not opened"; break;
				case 0x4004: name="DMLERR_DLL_USAGE"; meaning="a client-only instance tried a server transaction"; break;
				case 0x4005: name="DMLERR_EXECACKTIMEOUT"; meaning="the server did not take the command in time"; break;
				case 0x4006: name="DMLERR_INVALIDPARAMETER"; meaning="a parameter was not valid"; break;
				case 0x4007: name="DMLERR_LOW_MEMORY"; meaning="the server cannot keep up"; break;
				case 0x4008: name="DMLERR_MEMORY_ERROR"; meaning="out of memory"; break;
				case 0x4009: name="DMLERR_NOTPROCESSED"; meaning="the server did not process the command"; break;
				case 0x400A: name="DMLERR_NO_CONV_ESTABLISHED"; meaning="no DDE server answered (is the program running?)"; break;
				case 0x400B: name="DMLERR_POKEACKTIMEOUT"; meaning="a poke transaction timed out"; break;
				case 0x400C: name="DMLERR_POSTMSG_FAILED"; meaning="a message could not be posted"; break;
				case 0x400D: name="DMLERR_REENTRANCY"; meaning="another synchronous transaction is still waiting"; break;
				case 0x400E: name="DMLERR_SERVER_DIED"; meaning="the server ended the conversation"; break;
				case 0x400F: name="DMLERR_SYS_ERROR"; meaning="an internal DDE error"; break;
				case 0x4010: name="DMLERR_UNADVACKTIMEOUT"; meaning="an end of advise timed out"; break;
				case 0x4011: name="DMLERR_UNFOUND_QUEUE_ID"; meaning="an unknown transaction"; break;
				default: name="DDE error"; meaning="unknown"; break;
			}
			return name+" (0x"+dmlErrorCode.ToString("X4")+"): "+meaning;
		}
	}
}
