// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager.Tests {
	///<summary>Sends raw TWAIN triplets to a FakeTwainDsm, without TwainSession, so that tests can check that the simulated sources
	///behave as their profiles say. Opens the DSM and the named source in the constructor (state 4) and closes both in Dispose.
	///Notifications are collected by polling MSG_PROCESSEVENT, so no message loop is needed.</summary>
	internal sealed class FakeRawClient:IDisposable {
		private const int PM_REMOVE=1;
		private readonly FakeTwainDsm _dsm;
		private readonly ProxyWindow _window;
		private IntPtr _pAppIdentity;
		private IntPtr _pSourceIdentity;
		private ITwainMemory _memory=GlobalTwainMemory.Instance;

		public FakeRawClient(FakeTwainDsm dsm,string sourceName) {
			_dsm=dsm;
			_window=new ProxyWindow();
			_pAppIdentity=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_IDENTITY)));
			_pSourceIdentity=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_IDENTITY)));
			TW_IDENTITY appIdentity=new TW_IDENTITY { ProtocolMajor=2,ProtocolMinor=4,SupportedGroups=Tw.DF_APP2|Tw.DG_CONTROL|Tw.DG_IMAGE,ProductName="Raw client" };
			appIdentity.Version.Info="1";
			Marshal.StructureToPtr(appIdentity,_pAppIdentity,false);
			IntPtr pHwnd=Marshal.AllocHGlobal(IntPtr.Size);
			try {
				Marshal.WriteIntPtr(pHwnd,_window.Handle);
				Check(_dsm.Entry(_pAppIdentity,IntPtr.Zero,Tw.DG_CONTROL,Tw.DAT_PARENT,Tw.MSG_OPENDSM,pHwnd),"MSG_OPENDSM");
			}
			finally {
				Marshal.FreeHGlobal(pHwnd);
			}
			if((((TW_IDENTITY)Marshal.PtrToStructure(_pAppIdentity,typeof(TW_IDENTITY))).SupportedGroups & Tw.DF_DSM2)!=0) {
				IntPtr pEntryPoint=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_ENTRYPOINT)));
				try {
					Marshal.StructureToPtr(new TW_ENTRYPOINT(),pEntryPoint,false);
					Check(_dsm.Entry(_pAppIdentity,IntPtr.Zero,Tw.DG_CONTROL,Tw.DAT_ENTRYPOINT,Tw.MSG_GET,pEntryPoint),"DAT_ENTRYPOINT");
					_memory=DsmTwainMemory.TryCreate((TW_ENTRYPOINT)Marshal.PtrToStructure(pEntryPoint,typeof(TW_ENTRYPOINT)));
				}
				finally {
					Marshal.FreeHGlobal(pEntryPoint);
				}
			}
			TW_IDENTITY sourceIdentity=new TW_IDENTITY { ProductName=sourceName };
			sourceIdentity.Version.Info="";
			Marshal.StructureToPtr(sourceIdentity,_pSourceIdentity,false);
			Check(_dsm.Entry(_pAppIdentity,IntPtr.Zero,Tw.DG_CONTROL,Tw.DAT_IDENTITY,Tw.MSG_OPENDS,_pSourceIdentity),"MSG_OPENDS");
		}

		///<summary>The memory functions the client uses for handles: the DSM's when it handed them out, otherwise the Global ones.</summary>
		public ITwainMemory Memory => _memory;

		public IntPtr WindowHandle => _window.Handle;

		private static void Check(ushort rc,string what) {
			if(rc!=Tw.TWRC_SUCCESS) {
				throw new InvalidOperationException(what+" returned "+rc);
			}
		}

		public ushort Call(uint dg,ushort dat,ushort msg,IntPtr data) {
			return _dsm.Entry(_pAppIdentity,_pSourceIdentity,dg,dat,msg,data);
		}

		#region Capabilities
		public ushort SetCap(ushort cap,ushort itemType,int value) {
			IntPtr hContainer=_memory.Allocate(TwContainer.OneValueSize);
			IntPtr pCapability=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_CAPABILITY)));
			try {
				IntPtr pContainer=_memory.Lock(hContainer);
				Marshal.WriteInt16(pContainer,0,(short)itemType);
				Marshal.WriteInt32(pContainer,TwContainer.OneValueItemOffset,value);
				_memory.Unlock(hContainer);
				Marshal.StructureToPtr(new TW_CAPABILITY { Cap=cap,ConType=Tw.TWON_ONEVALUE,hContainer=hContainer },pCapability,false);
				return Call(Tw.DG_CONTROL,Tw.DAT_CAPABILITY,Tw.MSG_SET,pCapability);
			}
			finally {
				Marshal.FreeHGlobal(pCapability);
				_memory.Free(hContainer);
			}
		}

		///<summary>MSG_GET, MSG_GETCURRENT, MSG_GETDEFAULT, MSG_RESET or MSG_QUERYSUPPORT. value is the TW_ONEVALUE item, read as
		///16 bits for 16-bit item types.</summary>
		public ushort GetCap(ushort cap,ushort msg,out int value) {
			value=0;
			IntPtr pCapability=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_CAPABILITY)));
			try {
				Marshal.StructureToPtr(new TW_CAPABILITY { Cap=cap,ConType=Tw.TWON_DONTCARE16 },pCapability,false);
				ushort rc=Call(Tw.DG_CONTROL,Tw.DAT_CAPABILITY,msg,pCapability);
				if(rc!=Tw.TWRC_SUCCESS) {
					return rc;
				}
				TW_CAPABILITY capability=(TW_CAPABILITY)Marshal.PtrToStructure(pCapability,typeof(TW_CAPABILITY));
				IntPtr pContainer=_memory.Lock(capability.hContainer);
				ushort itemType=(ushort)Marshal.ReadInt16(pContainer,0);
				int raw=Marshal.ReadInt32(pContainer,TwContainer.OneValueItemOffset);
				_memory.Unlock(capability.hContainer);
				_memory.Free(capability.hContainer);
				value=itemType==Tw.TWTY_INT16 ? (short)(raw&0xffff) : (TwContainer.ItemSize(itemType)==2 ? raw&0xffff : raw);
				return rc;
			}
			finally {
				Marshal.FreeHGlobal(pCapability);
			}
		}

		public int GetCapValue(ushort cap) {
			int value;
			Check(GetCap(cap,Tw.MSG_GETCURRENT,out value),"MSG_GETCURRENT");
			return value;
		}
		#endregion Capabilities

		#region User interface and events
		public ushort Enable() {
			IntPtr p=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_USERINTERFACE)));
			try {
				Marshal.StructureToPtr(new TW_USERINTERFACE { ShowUI=1,hParent=_window.Handle },p,false);
				return Call(Tw.DG_CONTROL,Tw.DAT_USERINTERFACE,Tw.MSG_ENABLEDS,p);
			}
			finally {
				Marshal.FreeHGlobal(p);
			}
		}

		public ushort Disable() {
			IntPtr p=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_USERINTERFACE)));
			try {
				Marshal.StructureToPtr(new TW_USERINTERFACE { ShowUI=1,hParent=_window.Handle },p,false);
				return Call(Tw.DG_CONTROL,Tw.DAT_USERINTERFACE,Tw.MSG_DISABLEDS,p);
			}
			finally {
				Marshal.FreeHGlobal(p);
			}
		}

		///<summary>One MSG_PROCESSEVENT with a WM_NULL message for the client's window. Returns the notification, or MSG_NULL.</summary>
		public ushort ProcessEvent() {
			IntPtr pMsg=Marshal.AllocHGlobal(NativeMethods.MsgBufferSize);
			IntPtr pEvent=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_EVENT)));
			try {
				for(int i=0;i<NativeMethods.MsgBufferSize;i+=4) {
					Marshal.WriteInt32(pMsg,i,0);
				}
				Marshal.WriteIntPtr(pMsg,NativeMethods.MsgOffsetHwnd,_window.Handle);
				Marshal.StructureToPtr(new TW_EVENT { pEvent=pMsg,TWMessage=Tw.MSG_NULL },pEvent,false);
				ushort rc=Call(Tw.DG_CONTROL,Tw.DAT_EVENT,Tw.MSG_PROCESSEVENT,pEvent);
				TW_EVENT twEvent=(TW_EVENT)Marshal.PtrToStructure(pEvent,typeof(TW_EVENT));
				return rc==Tw.TWRC_DSEVENT ? twEvent.TWMessage : Tw.MSG_NULL;
			}
			finally {
				Marshal.FreeHGlobal(pEvent);
				Marshal.FreeHGlobal(pMsg);
			}
		}

		///<summary>Polls MSG_PROCESSEVENT until the source sends a notification or timeoutMs passes (then MSG_NULL).</summary>
		public ushort WaitForNotification(int timeoutMs) {
			long end=_dsm.NowMs+timeoutMs;
			while(true) {
				ushort notification=ProcessEvent();
				if(notification!=Tw.MSG_NULL || _dsm.NowMs>=end) {
					return notification;
				}
				Thread.Sleep(5);
			}
		}
		#endregion User interface and events

		#region Status
		///<summary>DAT_STATUS to the source.</summary>
		public ushort Status() {
			IntPtr p=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_STATUS)));
			try {
				Marshal.StructureToPtr(new TW_STATUS(),p,false);
				Check(Call(Tw.DG_CONTROL,Tw.DAT_STATUS,Tw.MSG_GET,p),"DAT_STATUS");
				return ((TW_STATUS)Marshal.PtrToStructure(p,typeof(TW_STATUS))).ConditionCode;
			}
			finally {
				Marshal.FreeHGlobal(p);
			}
		}

		///<summary>DAT_STATUSUTF8 to the source. text is everything before the first NUL; size is the Size field.</summary>
		public ushort StatusText(ushort conditionCode,out string text,out uint size) {
			text=null;
			size=0;
			IntPtr p=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_STATUSUTF8)));
			try {
				TW_STATUSUTF8 statusUtf8=new TW_STATUSUTF8();
				statusUtf8.Status.ConditionCode=conditionCode;
				Marshal.StructureToPtr(statusUtf8,p,false);
				ushort rc=Call(Tw.DG_CONTROL,Tw.DAT_STATUSUTF8,Tw.MSG_GET,p);
				if(rc!=Tw.TWRC_SUCCESS) {
					return rc;
				}
				statusUtf8=(TW_STATUSUTF8)Marshal.PtrToStructure(p,typeof(TW_STATUSUTF8));
				size=statusUtf8.Size;
				byte[] bytes=new byte[size];
				IntPtr pText=_memory.Lock(statusUtf8.UTF8string);
				Marshal.Copy(pText,bytes,0,bytes.Length);
				_memory.Unlock(statusUtf8.UTF8string);
				_memory.Free(statusUtf8.UTF8string);
				int length=Array.IndexOf(bytes,(byte)0);
				text=Encoding.UTF8.GetString(bytes,0,length<0 ? bytes.Length : length);
				if(length!=bytes.Length-1) {
					text+=" [Size does not end at the NUL]";
				}
				return rc;
			}
			finally {
				Marshal.FreeHGlobal(p);
			}
		}
		#endregion Status

		#region Transfers
		public ushort Pending(ushort msg,out short count) {
			count=0;
			IntPtr p=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_PENDINGXFERS)));
			try {
				Marshal.StructureToPtr(new TW_PENDINGXFERS(),p,false);
				ushort rc=Call(Tw.DG_CONTROL,Tw.DAT_PENDINGXFERS,msg,p);
				count=((TW_PENDINGXFERS)Marshal.PtrToStructure(p,typeof(TW_PENDINGXFERS))).Count;
				return rc;
			}
			finally {
				Marshal.FreeHGlobal(p);
			}
		}

		public short EndTransfer() {
			short count;
			Check(Pending(Tw.MSG_ENDXFER,out count),"MSG_ENDXFER");
			return count;
		}

		///<summary>DAT_IMAGENATIVEXFER. On TWRC_XFERDONE the DIB handle is copied into dib and freed with Memory.</summary>
		public ushort NativeTransfer(out byte[] dib,out IntPtr hDib) {
			dib=null;
			IntPtr pHandle=Marshal.AllocHGlobal(IntPtr.Size);
			try {
				Marshal.WriteIntPtr(pHandle,IntPtr.Zero);
				ushort rc=Call(Tw.DG_IMAGE,Tw.DAT_IMAGENATIVEXFER,Tw.MSG_GET,pHandle);
				hDib=Marshal.ReadIntPtr(pHandle);
				if(rc==Tw.TWRC_XFERDONE) {
					IntPtr pDib=_memory.Lock(hDib);
					long length=Imaging.DibImage.GetPackedLength(pDib);
					dib=new byte[length];
					Marshal.Copy(pDib,dib,0,(int)length);
					_memory.Unlock(hDib);
					_memory.Free(hDib);
				}
				return rc;
			}
			finally {
				Marshal.FreeHGlobal(pHandle);
			}
		}

		///<summary>One DAT_IMAGEMEMXFER call into a pointer buffer of bufferSize bytes. strip holds the bytes written.</summary>
		public ushort MemoryTransfer(int bufferSize,out TW_IMAGEMEMXFER xfer,out byte[] strip) {
			strip=null;
			IntPtr pBuffer=Marshal.AllocHGlobal(bufferSize);
			IntPtr pXfer=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_IMAGEMEMXFER)));
			try {
				xfer=new TW_IMAGEMEMXFER();
				xfer.Memory.Flags=Tw.TWMF_APPOWNS|Tw.TWMF_POINTER;
				xfer.Memory.Length=(uint)bufferSize;
				xfer.Memory.TheMem=pBuffer;
				Marshal.StructureToPtr(xfer,pXfer,false);
				ushort rc=Call(Tw.DG_IMAGE,Tw.DAT_IMAGEMEMXFER,Tw.MSG_GET,pXfer);
				xfer=(TW_IMAGEMEMXFER)Marshal.PtrToStructure(pXfer,typeof(TW_IMAGEMEMXFER));
				if(rc==Tw.TWRC_SUCCESS || rc==Tw.TWRC_XFERDONE) {
					strip=new byte[xfer.BytesWritten];
					Marshal.Copy(pBuffer,strip,0,strip.Length);
				}
				return rc;
			}
			finally {
				Marshal.FreeHGlobal(pXfer);
				Marshal.FreeHGlobal(pBuffer);
			}
		}
		#endregion Transfers

		public void Dispose() {
			try {
				if(_dsm.IsSourceOpen) {
					_dsm.Entry(_pAppIdentity,IntPtr.Zero,Tw.DG_CONTROL,Tw.DAT_IDENTITY,Tw.MSG_CLOSEDS,_pSourceIdentity);
				}
				IntPtr pHwnd=Marshal.AllocHGlobal(IntPtr.Size);
				Marshal.WriteIntPtr(pHwnd,_window.Handle);
				_dsm.Entry(_pAppIdentity,IntPtr.Zero,Tw.DG_CONTROL,Tw.DAT_PARENT,Tw.MSG_CLOSEDSM,pHwnd);
				Marshal.FreeHGlobal(pHwnd);
			}
			finally {
				//Remove what the simulated source posted to the window, so that it does not reach later tests.
				IntPtr pMsg=Marshal.AllocHGlobal(NativeMethods.MsgBufferSize);
				while(PeekMessage(pMsg,_window.Handle,0,0,PM_REMOVE)) {
				}
				Marshal.FreeHGlobal(pMsg);
				_window.Dispose();
				Marshal.FreeHGlobal(_pAppIdentity);
				Marshal.FreeHGlobal(_pSourceIdentity);
			}
		}

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool PeekMessage(IntPtr lpMsg,IntPtr hWnd,uint wMsgFilterMin,uint wMsgFilterMax,uint wRemoveMsg);
	}
}
