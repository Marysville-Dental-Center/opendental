// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ImagingDeviceManager.Imaging;

namespace ImagingDeviceManager.TwainProtocol {
	///<summary>Loads a data source manager. Returns null and sets errorText when none is available.</summary>
	internal delegate ITwainDsm DsmLoader(out string errorText);

	///<summary>What happened to one image transfer.</summary>
	internal enum TransferOutcome {
		Done,
		Cancelled,
		Failed,
	}

	///<summary>A TWAIN application session: the state machine from the TWAIN specification (state 1, nothing loaded, through
	///state 7, transferring), driven synchronously on the calling thread. While a data source is enabled, a local Windows
	///message loop runs and hands every message to the data source (DAT_EVENT / MSG_PROCESSEVENT), the classic Windows way
	///that every data source supports. Not thread safe: use it from one UI thread, like the callers do.</summary>
	internal sealed class TwainSession:IDisposable {
		private const int TimerIdTimeout=0x7A11;
		private const int MaxConsecutiveEventFailures=50;
		private readonly DsmLoader _dsmLoader;
		private ITwainDsm _dsm;
		private ProxyWindow _proxyWindow;
		///<summary>The application's TW_IDENTITY, in unmanaged memory for the life of the session.</summary>
		private IntPtr _pAppIdentity;
		///<summary>The open data source's TW_IDENTITY, in unmanaged memory for the life of the session.</summary>
		private IntPtr _pSourceIdentity;
		private bool _isAcquiring;
		private ushort _xferMech=Tw.TWSX_NATIVE;
		private bool _isVanilla;

		///<summary>TWAIN state, 1 to 7.</summary>
		public int State { get; private set; }=1;
		public EZTwainErrorCode ErrorCode { get; private set; }
		public string ErrorText { get; private set; }="";
		///<summary>Hide the data source's own dialog when acquiring. Persists until changed.</summary>
		public bool HideUI { get; set; }
		///<summary>Leave the data source open after Acquire so more images can be taken. Reset when the source closes.</summary>
		public bool MultiTransfer { get; set; }
		///<summary>Tests set this so that a broken simulated source cannot hang a test run. 0 means wait forever.</summary>
		public int LoopTimeoutMilliseconds { get; set; }
		///<summary>Product name of the open data source, or "".</summary>
		public string SourceName { get; private set; }="";

		public TwainSession(DsmLoader dsmLoader) {
			_dsmLoader=dsmLoader;
			_pAppIdentity=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_IDENTITY)));
			_pSourceIdentity=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_IDENTITY)));
			WriteIdentity(_pSourceIdentity,new TW_IDENTITY());
		}

		#region Errors
		public void ClearError() {
			ErrorCode=EZTwainErrorCode.EZTEC_NONE;
			ErrorText="";
		}

		public void SetError(EZTwainErrorCode errorCode,string errorText) {
			ErrorCode=errorCode;
			ErrorText=errorText??"";
		}

		///<summary>Reads the condition code after a failed triplet. Pass toSource=false for triplets sent to the DSM.</summary>
		private ushort GetConditionCode(bool toSource) {
			if(_dsm==null || State<3) {
				return Tw.TWCC_BUMMER;
			}
			IntPtr pStatus=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_STATUS)));
			try {
				Marshal.StructureToPtr(new TW_STATUS(),pStatus,false);
				IntPtr dest=(toSource && State>=4) ? _pSourceIdentity : IntPtr.Zero;
				ushort rc=_dsm.Entry(_pAppIdentity,dest,Tw.DG_CONTROL,Tw.DAT_STATUS,Tw.MSG_GET,pStatus);
				if(rc!=Tw.TWRC_SUCCESS) {
					return Tw.TWCC_BUMMER;
				}
				TW_STATUS status=(TW_STATUS)Marshal.PtrToStructure(pStatus,typeof(TW_STATUS));
				return status.ConditionCode;
			}
			finally {
				Marshal.FreeHGlobal(pStatus);
			}
		}

		public static string GetConditionText(ushort conditionCode) {
			switch(conditionCode) {
				case Tw.TWCC_SUCCESS: return "No error was reported.";
				case Tw.TWCC_LOWMEMORY: return "The scanner driver ran out of memory.";
				case Tw.TWCC_NODS: return "The scanner driver could not be found.";
				case Tw.TWCC_MAXCONNECTIONS: return "The scanner is in use by another program.";
				case Tw.TWCC_OPERATIONERROR: return "The scanner driver reported an operation error.";
				case Tw.TWCC_BADCAP:
				case Tw.TWCC_CAPUNSUPPORTED: return "The scanner does not support this setting.";
				case Tw.TWCC_CAPBADOPERATION: return "The scanner does not allow this operation on this setting.";
				case Tw.TWCC_CAPSEQERROR: return "The scanner cannot change this setting right now.";
				case Tw.TWCC_BADPROTOCOL: return "The scanner driver does not support this request.";
				case Tw.TWCC_BADVALUE: return "The scanner does not accept this value.";
				case Tw.TWCC_SEQERROR: return "The scanner driver received a request out of order.";
				case Tw.TWCC_BADDEST: return "The scanner driver is not open.";
				case Tw.TWCC_DENIED: return "The scanner driver denied access.";
				case Tw.TWCC_PAPERJAM: return "The scanner has a paper jam.";
				case Tw.TWCC_PAPERDOUBLEFEED: return "The scanner fed more than one sheet.";
				case Tw.TWCC_CHECKDEVICEONLINE: return "The scanner is not connected or is turned off.";
				case Tw.TWCC_NOMEDIA: return "There is no paper in the scanner.";
				default: return "The scanner driver reported an error (TWAIN condition code "+conditionCode+").";
			}
		}

		private EZTwainErrorCode MapConditionCode(ushort conditionCode,EZTwainErrorCode errorCodeDefault) {
			if(conditionCode==Tw.TWCC_PAPERJAM) {
				return EZTwainErrorCode.EZTEC_DEVICE_PAPERJAM;
			}
			if(conditionCode==Tw.TWCC_PAPERDOUBLEFEED) {
				return EZTwainErrorCode.EZTEC_DEVICE_DOUBLEFEED;
			}
			return errorCodeDefault;
		}

		private void SetErrorFromCondition(EZTwainErrorCode errorCodeDefault,string prefix,bool toSource) {
			ushort conditionCode=GetConditionCode(toSource);
			SetError(MapConditionCode(conditionCode,errorCodeDefault),prefix+" "+GetConditionText(conditionCode));
		}
		#endregion Errors

		#region Data Source Manager
		private void WriteAppIdentity() {
			TW_IDENTITY identity=new TW_IDENTITY();
			identity.Id=0;
			identity.Version.MajorNum=1;
			identity.Version.MinorNum=0;
			identity.Version.Language=Tw.TWLG_USA;
			identity.Version.Country=Tw.TWCY_USA;
			identity.Version.Info="1.0";
			identity.ProtocolMajor=2;
			identity.ProtocolMinor=4;
			identity.SupportedGroups=Tw.DF_APP2|Tw.DG_CONTROL|Tw.DG_IMAGE;
			identity.Manufacturer="Open source";
			identity.ProductFamily="Dental practice management";
			identity.ProductName="Imaging Device Manager";
			WriteIdentity(_pAppIdentity,identity);
		}

		private static void WriteIdentity(IntPtr pIdentity,TW_IDENTITY identity) {
			Marshal.StructureToPtr(identity,pIdentity,false);
		}

		private static TW_IDENTITY ReadIdentity(IntPtr pIdentity) {
			return (TW_IDENTITY)Marshal.PtrToStructure(pIdentity,typeof(TW_IDENTITY));
		}

		///<summary>Moves to state 3 (DSM open). Returns false and sets the error if it cannot.</summary>
		public bool OpenDsm() {
			if(State>=3) {
				return true;
			}
			if(_dsm==null) {
				string errorText;
				try {
					_dsm=_dsmLoader(out errorText);
				}
				catch(Exception ex) {
					_dsm=null;
					errorText=ex.Message;
				}
				if(_dsm==null) {
					SetError(EZTwainErrorCode.EZTEC_LOAD_DSM,errorText);
					return false;
				}
				State=2;
			}
			if(_proxyWindow==null) {
				try {
					_proxyWindow=new ProxyWindow();
				}
				catch(Exception ex) {
					SetError(EZTwainErrorCode.EZTEC_PROXY_WINDOW,"Could not create the window used for scanning: "+ex.Message);
					UnloadDsm();
					return false;
				}
			}
			WriteAppIdentity();
			IntPtr pHwnd=Marshal.AllocHGlobal(IntPtr.Size);
			try {
				Marshal.WriteIntPtr(pHwnd,_proxyWindow.Handle);
				ushort rc=_dsm.Entry(_pAppIdentity,IntPtr.Zero,Tw.DG_CONTROL,Tw.DAT_PARENT,Tw.MSG_OPENDSM,pHwnd);
				if(rc!=Tw.TWRC_SUCCESS) {
					SetError(EZTwainErrorCode.EZTEC_OPEN_DSM,"The TWAIN Data Source Manager ("+_dsm.LibraryName+") could not be opened.");
					UnloadDsm();
					return false;
				}
			}
			finally {
				Marshal.FreeHGlobal(pHwnd);
			}
			State=3;
			try {
				_dsm.OnDsmOpened(_pAppIdentity);
			}
			catch {
				//The Global* memory functions still work on Windows.
			}
			return true;
		}

		///<summary>Moves down to state 1, closing the source first if one is open.</summary>
		public void CloseDsm() {
			if(State>=4) {
				CloseSourceCore();
			}
			if(State==3) {
				IntPtr pHwnd=Marshal.AllocHGlobal(IntPtr.Size);
				try {
					Marshal.WriteIntPtr(pHwnd,_proxyWindow==null ? IntPtr.Zero : _proxyWindow.Handle);
					_dsm.Entry(_pAppIdentity,IntPtr.Zero,Tw.DG_CONTROL,Tw.DAT_PARENT,Tw.MSG_CLOSEDSM,pHwnd);
				}
				finally {
					Marshal.FreeHGlobal(pHwnd);
				}
				State=2;
			}
			UnloadDsm();
		}

		private void UnloadDsm() {
			if(_dsm!=null) {
				try {
					_dsm.Dispose();
				}
				catch {
					//Nothing more can be done.
				}
				_dsm=null;
			}
			if(_proxyWindow!=null) {
				_proxyWindow.Dispose();
				_proxyWindow=null;
			}
			State=1;
		}

		///<summary>All installed data sources. Opens the DSM temporarily if needed. Returns null and sets the error on failure.</summary>
		public List<TW_IDENTITY> GetSources() {
			bool wasOpen=State>=3;
			if(!OpenDsm()) {
				return null;
			}
			try {
				return EnumerateSources();
			}
			finally {
				if(!wasOpen) {
					CloseDsm();
				}
			}
		}

		private List<TW_IDENTITY> EnumerateSources() {
			List<TW_IDENTITY> listIdentities=new List<TW_IDENTITY>();
			IntPtr pIdentity=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_IDENTITY)));
			try {
				WriteIdentity(pIdentity,new TW_IDENTITY());
				ushort rc=_dsm.Entry(_pAppIdentity,IntPtr.Zero,Tw.DG_CONTROL,Tw.DAT_IDENTITY,Tw.MSG_GETFIRST,pIdentity);
				while(rc==Tw.TWRC_SUCCESS) {
					listIdentities.Add(ReadIdentity(pIdentity));
					if(listIdentities.Count>1000) {
						break;//Protect against a DSM that never ends the list.
					}
					rc=_dsm.Entry(_pAppIdentity,IntPtr.Zero,Tw.DG_CONTROL,Tw.DAT_IDENTITY,Tw.MSG_GETNEXT,pIdentity);
				}
			}
			finally {
				Marshal.FreeHGlobal(pIdentity);
			}
			return listIdentities;
		}

		///<summary>Shows the Data Source Manager's "Select Source" dialog. The DSM remembers the choice as the default source.
		///Returns false if the user cancelled or the dialog could not be shown.</summary>
		public bool UserSelect(IntPtr hwnd) {
			bool wasOpen=State>=3;
			if(!OpenDsm()) {
				return false;
			}
			IntPtr pIdentity=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_IDENTITY)));
			IntPtr hwndDisabled=IntPtr.Zero;
			try {
				_proxyWindow.AttachTo(hwnd);
				hwndDisabled=DisableRootWindow(hwnd);
				WriteIdentity(pIdentity,new TW_IDENTITY());
				ushort rc=_dsm.Entry(_pAppIdentity,IntPtr.Zero,Tw.DG_CONTROL,Tw.DAT_IDENTITY,Tw.MSG_USERSELECT,pIdentity);
				if(rc==Tw.TWRC_SUCCESS) {
					return true;
				}
				if(rc==Tw.TWRC_CANCEL) {
					SetError(EZTwainErrorCode.EZTEC_USER_CANCEL,"No scanner was selected.");
					return false;
				}
				SetErrorFromCondition(EZTwainErrorCode.EZTEC_DSM_FAILURE,"The scanner selection dialog could not be shown.",false);
				return false;
			}
			finally {
				ReenableWindow(hwndDisabled);
				if(_proxyWindow!=null) {
					_proxyWindow.AttachTo(IntPtr.Zero);
				}
				Marshal.FreeHGlobal(pIdentity);
				if(!wasOpen) {
					CloseDsm();
				}
			}
		}
		#endregion Data Source Manager

		#region Data Source
		///<summary>Opens the data source whose product name matches sourceName, or the default source when sourceName is null
		///or empty. Closes any source that is already open first. Moves to state 4.</summary>
		public bool OpenSource(string sourceName) {
			if(_isAcquiring) {
				SetError(EZTwainErrorCode.EZTEC_INTERNAL,"A scan is already in progress.");
				return false;
			}
			if(State>=4) {
				CloseSource();
			}
			if(!OpenDsm()) {
				return false;
			}
			bool isDefault=string.IsNullOrEmpty(sourceName);
			if(isDefault) {
				WriteIdentity(_pSourceIdentity,new TW_IDENTITY());
				ushort rcDefault=_dsm.Entry(_pAppIdentity,IntPtr.Zero,Tw.DG_CONTROL,Tw.DAT_IDENTITY,Tw.MSG_GETDEFAULT,_pSourceIdentity);
				if(rcDefault!=Tw.TWRC_SUCCESS) {
					SetError(EZTwainErrorCode.EZTEC_OPEN_DEFAULT_DS,"No default scanner is set, or no TWAIN scanner driver is installed.");
					CloseDsm();
					return false;
				}
			}
			else {
				TW_IDENTITY? identityFound=FindSource(EnumerateSources(),sourceName);
				if(identityFound==null) {
					SetError(EZTwainErrorCode.EZTEC_NO_SUCH_DS,"The scanner \""+sourceName+"\" is not installed on this computer.");
					CloseDsm();
					return false;
				}
				WriteIdentity(_pSourceIdentity,identityFound.Value);
			}
			ushort rc=_dsm.Entry(_pAppIdentity,IntPtr.Zero,Tw.DG_CONTROL,Tw.DAT_IDENTITY,Tw.MSG_OPENDS,_pSourceIdentity);
			if(rc!=Tw.TWRC_SUCCESS) {
				string name=ReadIdentity(_pSourceIdentity).ProductName;
				SetErrorFromCondition(isDefault ? EZTwainErrorCode.EZTEC_OPEN_DEFAULT_DS : EZTwainErrorCode.EZTEC_OPEN_DS,
					"The scanner \""+name+"\" could not be opened.",false);
				CloseDsm();
				return false;
			}
			State=4;
			SourceName=ReadIdentity(_pSourceIdentity).ProductName??"";
			_xferMech=Tw.TWSX_NATIVE;
			_isVanilla=false;
			return true;
		}

		///<summary>Finds a source by product name: an exact match ignoring case first, then a source whose name starts with
		///sourceName. Returns null when there is none.</summary>
		public static TW_IDENTITY? FindSource(List<TW_IDENTITY> listIdentities,string sourceName) {
			string nameTrimmed=sourceName.Trim();
			foreach(TW_IDENTITY identity in listIdentities) {
				if(string.Equals((identity.ProductName??"").Trim(),nameTrimmed,StringComparison.OrdinalIgnoreCase)) {
					return identity;
				}
			}
			foreach(TW_IDENTITY identity in listIdentities) {
				if((identity.ProductName??"").Trim().StartsWith(nameTrimmed,StringComparison.OrdinalIgnoreCase) && nameTrimmed!="") {
					return identity;
				}
			}
			return null;
		}

		///<summary>Ends any transfers, disables and closes the source, and closes the DSM, ending in state 1. Ignored while an
		///acquire is running (for example if a click is handled during the scan's message loop): the acquire cleans up itself.</summary>
		public void CloseSource() {
			if(_isAcquiring) {
				return;
			}
			CloseSourceCore();
		}

		private void CloseSourceCore() {
			if(State>=6) {
				ResetPendingTransfers();
			}
			if(State==5) {
				DisableSource();
			}
			if(State==4) {
				_dsm.Entry(_pAppIdentity,IntPtr.Zero,Tw.DG_CONTROL,Tw.DAT_IDENTITY,Tw.MSG_CLOSEDS,_pSourceIdentity);
				State=3;
			}
			SourceName="";
			MultiTransfer=false;
			_xferMech=Tw.TWSX_NATIVE;
			if(State==3) {
				CloseDsm();
			}
		}
		#endregion Data Source

		#region Capabilities
		private bool RequireState4(string what) {
			if(State<4) {
				SetError(EZTwainErrorCode.EZTEC_NO_DS_OPEN,"No scanner is open ("+what+").");
				return false;
			}
			if(State>4) {
				SetError(EZTwainErrorCode.EZTEC_NOT_STATE_4,"The scanner settings cannot be changed during a scan ("+what+").");
				return false;
			}
			return true;
		}

		///<summary>Sets a capability to a single value (TW_ONEVALUE). Signed values are sign extended so that sources which
		///read the Item field as 16 or as 32 bits both see the same number.</summary>
		public bool SetCapability(ushort capability,ushort itemType,int value) {
			if(!RequireState4("capability 0x"+capability.ToString("X4"))) {
				return false;
			}
			IntPtr hContainer=_dsm.MemAllocate(TwContainer.OneValueSize);
			if(hContainer==IntPtr.Zero) {
				SetError(EZTwainErrorCode.EZTEC_MEMORY,"Out of memory.");
				return false;
			}
			IntPtr pCapability=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_CAPABILITY)));
			try {
				IntPtr pContainer=_dsm.MemLock(hContainer);
				Marshal.WriteInt16(pContainer,0,(short)itemType);
				Marshal.WriteInt32(pContainer,TwContainer.OneValueItemOffset,value);
				_dsm.MemUnlock(hContainer);
				TW_CAPABILITY twCapability=new TW_CAPABILITY { Cap=capability,ConType=Tw.TWON_ONEVALUE,hContainer=hContainer };
				Marshal.StructureToPtr(twCapability,pCapability,false);
				ushort rc=_dsm.Entry(_pAppIdentity,_pSourceIdentity,Tw.DG_CONTROL,Tw.DAT_CAPABILITY,Tw.MSG_SET,pCapability);
				if(rc==Tw.TWRC_SUCCESS || rc==Tw.TWRC_CHECKSTATUS) {
					return true;
				}
				SetErrorFromCondition(EZTwainErrorCode.EZTEC_CAP_SET,"The scanner did not accept a setting (capability 0x"+capability.ToString("X4")+").",true);
				return false;
			}
			finally {
				Marshal.FreeHGlobal(pCapability);
				_dsm.MemFree(hContainer);
			}
		}

		///<summary>Reads the current value of a capability. Supports TW_ONEVALUE, TW_ENUMERATION and TW_RANGE containers holding
		///integer, boolean or FIX32 items (FIX32 is returned as its raw 32 bit pattern).</summary>
		public bool TryGetCapabilityCurrent(ushort capability,out int value) {
			value=0;
			if(State<4) {
				return false;
			}
			IntPtr pCapability=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_CAPABILITY)));
			IntPtr hContainer=IntPtr.Zero;
			try {
				TW_CAPABILITY twCapability=new TW_CAPABILITY { Cap=capability,ConType=Tw.TWON_DONTCARE16,hContainer=IntPtr.Zero };
				Marshal.StructureToPtr(twCapability,pCapability,false);
				ushort rc=_dsm.Entry(_pAppIdentity,_pSourceIdentity,Tw.DG_CONTROL,Tw.DAT_CAPABILITY,Tw.MSG_GETCURRENT,pCapability);
				if(rc!=Tw.TWRC_SUCCESS) {
					return false;
				}
				twCapability=(TW_CAPABILITY)Marshal.PtrToStructure(pCapability,typeof(TW_CAPABILITY));
				hContainer=twCapability.hContainer;
				if(hContainer==IntPtr.Zero) {
					return false;
				}
				IntPtr pContainer=_dsm.MemLock(hContainer);
				try {
					ushort itemType=(ushort)Marshal.ReadInt16(pContainer,0);
					int itemSize=TwContainer.ItemSize(itemType);
					if(itemSize<1 || itemSize>4) {
						return false;
					}
					int offset;
					switch(twCapability.ConType) {
						case Tw.TWON_ONEVALUE:
							offset=TwContainer.OneValueItemOffset;
							break;
						case Tw.TWON_ENUMERATION:
							int numItems=Marshal.ReadInt32(pContainer,TwContainer.EnumNumItemsOffset);
							int currentIndex=Marshal.ReadInt32(pContainer,TwContainer.EnumCurrentIndexOffset);
							if(currentIndex<0 || currentIndex>=numItems) {
								return false;
							}
							offset=TwContainer.EnumItemListOffset+currentIndex*itemSize;
							break;
						case Tw.TWON_RANGE:
							offset=TwContainer.RangeCurrentValueOffset;
							itemSize=4;//Range values are always stored as 32 bits.
							break;
						default:
							return false;
					}
					value=ReadItem(pContainer,offset,itemType,itemSize);
					return true;
				}
				finally {
					_dsm.MemUnlock(hContainer);
				}
			}
			finally {
				Marshal.FreeHGlobal(pCapability);
				if(hContainer!=IntPtr.Zero) {
					_dsm.MemFree(hContainer);
				}
			}
		}

		private static int ReadItem(IntPtr pContainer,int offset,ushort itemType,int itemSize) {
			switch(itemSize) {
				case 1:
					byte b=Marshal.ReadByte(pContainer,offset);
					return itemType==Tw.TWTY_INT8 ? (int)(sbyte)b : (int)b;
				case 2:
					short s=Marshal.ReadInt16(pContainer,offset);
					return itemType==Tw.TWTY_INT16 ? (int)s : (int)(ushort)s;
				default:
					return Marshal.ReadInt32(pContainer,offset);
			}
		}

		public bool SetPixelType(int pixelType) {
			return SetCapability(Tw.ICAP_PIXELTYPE,Tw.TWTY_UINT16,pixelType);
		}

		///<summary>Sets both resolutions, in dots per inch.</summary>
		public bool SetResolution(double dpi) {
			if(!RequireState4("resolution")) {
				return false;
			}
			SetCapability(Tw.ICAP_UNITS,Tw.TWTY_UINT16,Tw.TWUN_INCHES);//Inches is the default; failure is harmless.
			ClearError();
			int fix32=(int)TW_FIX32.FromDouble(dpi).ToUInt32();
			bool isXSet=SetCapability(Tw.ICAP_XRESOLUTION,Tw.TWTY_FIX32,fix32);
			bool isYSet=SetCapability(Tw.ICAP_YRESOLUTION,Tw.TWTY_FIX32,fix32);
			return isXSet && isYSet;
		}

		public bool SetDuplex(bool isEnabled) {
			return SetCapability(Tw.CAP_DUPLEXENABLED,Tw.TWTY_BOOL,isEnabled ? 1 : 0);
		}

		///<summary>Chooses native (0) or memory (2) transfers for the open source. File transfers are not supported.</summary>
		public bool SetTransferMechanism(int mechanism) {
			if(mechanism!=Tw.TWSX_NATIVE && mechanism!=Tw.TWSX_MEMORY) {
				SetError(EZTwainErrorCode.EZTEC_INVALID_PARAM,"Only native and memory transfers are supported.");
				return false;
			}
			if(!SetCapability(Tw.ICAP_XFERMECH,Tw.TWTY_UINT16,mechanism)) {
				_xferMech=Tw.TWSX_NATIVE;
				return false;
			}
			_xferMech=(ushort)mechanism;
			return true;
		}
		#endregion Capabilities

		#region Acquire
		///<summary>Acquires one image. Opens the default source if none is open. In single mode the source and DSM are closed
		///afterwards; with MultiTransfer the source stays open and further calls return the remaining images of the batch
		///(see IsDone). Returns null with ErrorCode set when no image was acquired. ErrorCode is EZTEC_NONE on success.</summary>
		public DibImage Acquire(IntPtr hwnd) {
			ClearError();
			if(_isAcquiring) {
				SetError(EZTwainErrorCode.EZTEC_INTERNAL,"A scan is already in progress.");
				return null;
			}
			if(State<4 && !OpenSource(null)) {
				return null;
			}
			_isAcquiring=true;
			DibImage dib=null;
			try {
				bool isMulti=MultiTransfer;
				if(State==5) {
					DisableSource();//Should not happen; start from a clean state 4.
				}
				if(State==4) {
					if(!EnableAndWaitForTransfer(hwnd,isMulti ? -1 : 1)) {
						return null;
					}
				}
				//State 6: an image is ready.
				TransferOutcome outcome=TransferOne(out dib);
				if(outcome!=TransferOutcome.Done) {
					dib=null;
					if(State>=6) {
						ResetPendingTransfers();
					}
				}
				else if(!isMulti && State>=6) {
					ResetPendingTransfers();//Single mode takes one image and discards the rest of the batch.
				}
				if(State==5) {
					DisableSource();//Batch finished: close the scanner's dialog. IsDone() is now true.
				}
				return dib;
			}
			catch(ImagingDeviceException ex) {
				SetError(ex.ErrorCode==EZTwainErrorCode.EZTEC_NONE ? EZTwainErrorCode.EZTEC_XFER_FAILURE : ex.ErrorCode,ex.Message);
				AbortToState4();
				return null;
			}
			catch(Exception ex) {
				SetError(EZTwainErrorCode.EZTEC_INTERNAL,ex.Message);
				AbortToState4();
				return null;
			}
			finally {
				_isAcquiring=false;
				if(!MultiTransfer && State>=4) {
					CloseSource();
				}
			}
		}

		///<summary>Acquires every image the source delivers in one session and passes each to onPage as it arrives. Opens the
		///default source if none is open, and always closes the source and DSM afterwards. Returns the number of pages.
		///ErrorCode is EZTEC_NONE on success, EZTEC_USER_CANCEL if the user cancelled, EZTEC_0_PAGES if nothing was scanned,
		///or another code if the scan failed part way (pages delivered before the failure have already gone to onPage).</summary>
		public int AcquireAll(IntPtr hwnd,Action<DibImage> onPage) {
			ClearError();
			if(_isAcquiring) {
				SetError(EZTwainErrorCode.EZTEC_INTERNAL,"A scan is already in progress.");
				return 0;
			}
			if(State<4 && !OpenSource(null)) {
				return 0;
			}
			_isAcquiring=true;
			int pageCount=0;
			try {
				if(State==5) {
					DisableSource();
				}
				if(State==4) {
					if(!EnableAndWaitForTransfer(hwnd,-1)) {
						return 0;
					}
				}
				while(State==6) {
					DibImage dib;
					TransferOutcome outcome=TransferOne(out dib);
					if(outcome==TransferOutcome.Cancelled) {
						if(State>=6) {
							ResetPendingTransfers();
						}
						break;
					}
					if(outcome==TransferOutcome.Failed) {
						if(State>=6) {
							ResetPendingTransfers();
						}
						break;
					}
					pageCount++;
					onPage(dib);
				}
				if(State==5) {
					DisableSource();
				}
				if(ErrorCode==EZTwainErrorCode.EZTEC_NONE && pageCount==0) {
					SetError(EZTwainErrorCode.EZTEC_0_PAGES,"No pages were scanned.");
				}
				return pageCount;
			}
			catch(ImagingDeviceException ex) {
				SetError(ex.ErrorCode==EZTwainErrorCode.EZTEC_NONE ? EZTwainErrorCode.EZTEC_XFER_FAILURE : ex.ErrorCode,ex.Message);
				AbortToState4();
				return pageCount;
			}
			catch(Exception ex) {
				SetError(EZTwainErrorCode.EZTEC_INTERNAL,ex.Message);
				AbortToState4();
				return pageCount;
			}
			finally {
				_isAcquiring=false;
				if(State>=4) {
					CloseSource();
				}
			}
		}

		///<summary>True when no transfers are pending, i.e. the batch that the last Acquire took an image from is finished.</summary>
		public bool IsDone() {
			return State<6;
		}

		///<summary>From state 4: negotiates the transfer settings, enables the source and runs the message loop until the source
		///says an image is ready (returns true, state 6) or the session ends without one (returns false, error set, state 4).
		///xferCount is the number of images wanted, -1 for all.</summary>
		private bool EnableAndWaitForTransfer(IntPtr hwnd,int xferCount) {
			SetCapabilityQuietly(Tw.CAP_XFERCOUNT,Tw.TWTY_INT16,xferCount);
			if(_xferMech==Tw.TWSX_MEMORY) {
				if(!SetCapabilityQuietly(Tw.ICAP_XFERMECH,Tw.TWTY_UINT16,Tw.TWSX_MEMORY)) {
					_xferMech=Tw.TWSX_NATIVE;
				}
			}
			if(_xferMech==Tw.TWSX_NATIVE) {
				SetCapabilityQuietly(Tw.ICAP_XFERMECH,Tw.TWTY_UINT16,Tw.TWSX_NATIVE);
			}
			int pixelFlavor;
			_isVanilla=TryGetCapabilityCurrent(Tw.ICAP_PIXELFLAVOR,out pixelFlavor) && pixelFlavor==Tw.TWPF_VANILLA;
			_proxyWindow.AttachTo(hwnd);
			IntPtr hwndDisabled=DisableRootWindow(hwnd);
			ushort dsMessage;
			try {
				if(!EnableSource()) {
					return false;
				}
				dsMessage=RunMessageLoop();
			}
			finally {
				//Re-enable before the source's dialog closes so that Windows hands activation back to the program.
				ReenableWindow(hwndDisabled);
			}
			if(dsMessage==Tw.MSG_XFERREADY) {
				State=6;
				return true;
			}
			if(State==5) {
				DisableSource();
			}
			if(dsMessage==Tw.MSG_CLOSEDSREQ) {
				SetError(EZTwainErrorCode.EZTEC_USER_CANCEL,"The scan was cancelled.");
			}
			//Otherwise RunMessageLoop already set the error (timeout or program closing).
			return false;
		}

		///<summary>Sets a capability, keeping the error code clear if the source refuses. Returns whether it was accepted.</summary>
		private bool SetCapabilityQuietly(ushort capability,ushort itemType,int value) {
			EZTwainErrorCode errorCodeOld=ErrorCode;
			string errorTextOld=ErrorText;
			bool isSet=SetCapability(capability,itemType,value);
			SetError(errorCodeOld,errorTextOld);
			return isSet;
		}

		private bool EnableSource() {
			IntPtr pUserInterface=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_USERINTERFACE)));
			try {
				TW_USERINTERFACE userInterface=new TW_USERINTERFACE();
				userInterface.ShowUI=(ushort)(HideUI ? 0 : 1);
				userInterface.ModalUI=0;
				userInterface.hParent=_proxyWindow.Handle;
				Marshal.StructureToPtr(userInterface,pUserInterface,false);
				ushort rc=_dsm.Entry(_pAppIdentity,_pSourceIdentity,Tw.DG_CONTROL,Tw.DAT_USERINTERFACE,Tw.MSG_ENABLEDS,pUserInterface);
				if(rc==Tw.TWRC_SUCCESS || rc==Tw.TWRC_CHECKSTATUS) {//CHECKSTATUS: the source shows its dialog even though it was asked not to.
					State=5;
					return true;
				}
				if(rc==Tw.TWRC_CANCEL) {
					SetError(EZTwainErrorCode.EZTEC_USER_CANCEL,"The scan was cancelled.");
					return false;
				}
				SetErrorFromCondition(EZTwainErrorCode.EZTEC_ENABLE_FAILED,"The scanner could not be started.",true);
				return false;
			}
			finally {
				Marshal.FreeHGlobal(pUserInterface);
			}
		}

		private void DisableSource() {
			IntPtr pUserInterface=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_USERINTERFACE)));
			try {
				TW_USERINTERFACE userInterface=new TW_USERINTERFACE();
				userInterface.ShowUI=(ushort)(HideUI ? 0 : 1);
				userInterface.hParent=_proxyWindow==null ? IntPtr.Zero : _proxyWindow.Handle;
				Marshal.StructureToPtr(userInterface,pUserInterface,false);
				_dsm.Entry(_pAppIdentity,_pSourceIdentity,Tw.DG_CONTROL,Tw.DAT_USERINTERFACE,Tw.MSG_DISABLEDS,pUserInterface);
			}
			finally {
				Marshal.FreeHGlobal(pUserInterface);
			}
			State=4;
			if(_proxyWindow!=null) {
				_proxyWindow.AttachTo(IntPtr.Zero);
			}
		}

		///<summary>Discards all pending transfers. Moves from state 6 or 7 to state 5.</summary>
		private void ResetPendingTransfers() {
			IntPtr pPending=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_PENDINGXFERS)));
			try {
				Marshal.StructureToPtr(new TW_PENDINGXFERS(),pPending,false);
				if(State==7) {
					_dsm.Entry(_pAppIdentity,_pSourceIdentity,Tw.DG_CONTROL,Tw.DAT_PENDINGXFERS,Tw.MSG_ENDXFER,pPending);
					TW_PENDINGXFERS pending=(TW_PENDINGXFERS)Marshal.PtrToStructure(pPending,typeof(TW_PENDINGXFERS));
					State=pending.Count==0 ? 5 : 6;
				}
				if(State==6) {
					Marshal.StructureToPtr(new TW_PENDINGXFERS(),pPending,false);
					_dsm.Entry(_pAppIdentity,_pSourceIdentity,Tw.DG_CONTROL,Tw.DAT_PENDINGXFERS,Tw.MSG_RESET,pPending);
					State=5;
				}
			}
			finally {
				Marshal.FreeHGlobal(pPending);
			}
		}

		///<summary>Brings the session back to state 4 after an unexpected exception.</summary>
		private void AbortToState4() {
			try {
				if(State>=6) {
					ResetPendingTransfers();
				}
				if(State==5) {
					DisableSource();
				}
			}
			catch {
				//The source is in an unknown state. CloseSource will still try to close it.
			}
		}

		///<summary>Pumps messages while the source is enabled. Returns MSG_XFERREADY or MSG_CLOSEDSREQ from the source, or
		///MSG_NULL (with the error set) if the loop had to stop for another reason.</summary>
		private ushort RunMessageLoop() {
			IntPtr pMsg=Marshal.AllocHGlobal(NativeMethods.MsgBufferSize);
			IntPtr pEvent=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_EVENT)));
			IntPtr hwndProxy=_proxyWindow.Handle;
			bool hasTimer=false;
			int countFailures=0;
			try {
				if(LoopTimeoutMilliseconds>0) {
					NativeMethods.SetTimer(hwndProxy,(UIntPtr)TimerIdTimeout,(uint)LoopTimeoutMilliseconds,IntPtr.Zero);
					hasTimer=true;
				}
				while(true) {
					int result=NativeMethods.GetMessage(pMsg,IntPtr.Zero,0,0);
					if(result==-1) {
						SetError(EZTwainErrorCode.EZTEC_INTERNAL,"Windows message loop error "+Marshal.GetLastWin32Error()+".");
						return Tw.MSG_NULL;
					}
					if(result==0) {
						//WM_QUIT: the program is shutting down. Stop scanning and pass the quit message on to the outer loop.
						int exitCode=Marshal.ReadInt32(pMsg,NativeMethods.MsgOffsetWParam);
						SetError(EZTwainErrorCode.EZTEC_USER_CANCEL,"The program is closing.");
						NativeMethods.PostQuitMessage(exitCode);
						return Tw.MSG_NULL;
					}
					IntPtr hwndMsg=Marshal.ReadIntPtr(pMsg,NativeMethods.MsgOffsetHwnd);
					int message=Marshal.ReadInt32(pMsg,NativeMethods.MsgOffsetMessage);
					if(hasTimer && message==NativeMethods.WM_TIMER && hwndMsg==hwndProxy
						&& Marshal.ReadIntPtr(pMsg,NativeMethods.MsgOffsetWParam)==(IntPtr)TimerIdTimeout)
					{
						SetError(EZTwainErrorCode.EZTEC_INTERNAL,"Timed out waiting for the scanner.");
						return Tw.MSG_NULL;
					}
					TW_EVENT twEvent=new TW_EVENT { pEvent=pMsg,TWMessage=Tw.MSG_NULL };
					Marshal.StructureToPtr(twEvent,pEvent,false);
					ushort rc=_dsm.Entry(_pAppIdentity,_pSourceIdentity,Tw.DG_CONTROL,Tw.DAT_EVENT,Tw.MSG_PROCESSEVENT,pEvent);
					twEvent=(TW_EVENT)Marshal.PtrToStructure(pEvent,typeof(TW_EVENT));
					if(twEvent.TWMessage==Tw.MSG_XFERREADY || twEvent.TWMessage==Tw.MSG_CLOSEDSREQ) {
						return twEvent.TWMessage;
					}
					if(rc==Tw.TWRC_FAILURE) {
						//Normal answers are DSEVENT or NOTDSEVENT. A source that keeps saying it is not open (or not enabled)
						//is gone; without this check the program would wait forever with its window disabled. Other failures
						//are ignored because some sources answer FAILURE for messages that are not theirs.
						ushort conditionCode=GetConditionCode(true);
						if(conditionCode==Tw.TWCC_BADDEST || conditionCode==Tw.TWCC_NODS || conditionCode==Tw.TWCC_SEQERROR) {
							countFailures++;
						}
						else {
							countFailures=0;
						}
						if(countFailures>=MaxConsecutiveEventFailures) {
							SetError(EZTwainErrorCode.EZTEC_DS_FAILURE,"The scanner stopped responding. "+GetConditionText(conditionCode));
							return Tw.MSG_NULL;
						}
					}
					else {
						countFailures=0;
					}
					if(rc==Tw.TWRC_DSEVENT) {
						continue;//The source handled the message (its own dialog).
					}
					NativeMethods.TranslateMessage(pMsg);
					NativeMethods.DispatchMessage(pMsg);
				}
			}
			finally {
				if(hasTimer) {
					NativeMethods.KillTimer(hwndProxy,(UIntPtr)TimerIdTimeout);
				}
				Marshal.FreeHGlobal(pEvent);
				Marshal.FreeHGlobal(pMsg);
			}
		}

		///<summary>Disables the top-level window that contains hwnd, the way a modal dialog does, and returns it so it can be
		///re-enabled. Returns IntPtr.Zero if nothing was disabled.</summary>
		private static IntPtr DisableRootWindow(IntPtr hwnd) {
			if(hwnd==IntPtr.Zero || !NativeMethods.IsWindow(hwnd)) {
				return IntPtr.Zero;
			}
			IntPtr hwndRoot=NativeMethods.GetAncestor(hwnd,NativeMethods.GA_ROOT);
			if(hwndRoot==IntPtr.Zero || !NativeMethods.IsWindowEnabled(hwndRoot)) {
				return IntPtr.Zero;
			}
			NativeMethods.EnableWindow(hwndRoot,false);
			return hwndRoot;
		}

		private static void ReenableWindow(IntPtr hwnd) {
			if(hwnd!=IntPtr.Zero && NativeMethods.IsWindow(hwnd)) {
				NativeMethods.EnableWindow(hwnd,true);
			}
		}

		///<summary>From state 6: transfers one image and ends the transfer. Afterwards the state is 6 if more images are
		///pending, otherwise 5. On failure the error is set.</summary>
		private TransferOutcome TransferOne(out DibImage dib) {
			dib=null;
			TW_IMAGEINFO imageInfo;
			bool hasImageInfo=TryGetImageInfo(out imageInfo);
			TransferOutcome outcome;
			if(_xferMech==Tw.TWSX_MEMORY) {
				outcome=TransferMemory(hasImageInfo,imageInfo,out dib);
			}
			else {
				outcome=TransferNative(out dib);
			}
			EndTransfer();
			if(outcome==TransferOutcome.Cancelled) {
				SetError(EZTwainErrorCode.EZTEC_USER_CANCEL,"The scan was cancelled.");
				return outcome;
			}
			if(outcome==TransferOutcome.Failed) {
				return outcome;
			}
			if(hasImageInfo) {
				if(_xferMech==Tw.TWSX_NATIVE && dib.BitCount==16 && imageInfo.PixelType==Tw.TWPT_GRAY) {
					dib=dib.ConvertGray16ToGray8();
				}
				dib.SetResolutionIfMissing(imageInfo.XResolution.ToDouble(),imageInfo.YResolution.ToDouble());
			}
			return outcome;
		}

		private bool TryGetImageInfo(out TW_IMAGEINFO imageInfo) {
			imageInfo=new TW_IMAGEINFO();
			IntPtr pImageInfo=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_IMAGEINFO)));
			try {
				imageInfo.BitsPerSample=new short[8];
				Marshal.StructureToPtr(imageInfo,pImageInfo,false);
				ushort rc=_dsm.Entry(_pAppIdentity,_pSourceIdentity,Tw.DG_IMAGE,Tw.DAT_IMAGEINFO,Tw.MSG_GET,pImageInfo);
				if(rc!=Tw.TWRC_SUCCESS) {
					return false;
				}
				imageInfo=(TW_IMAGEINFO)Marshal.PtrToStructure(pImageInfo,typeof(TW_IMAGEINFO));
				return true;
			}
			finally {
				Marshal.FreeHGlobal(pImageInfo);
			}
		}

		private TransferOutcome TransferNative(out DibImage dib) {
			dib=null;
			IntPtr pHandle=Marshal.AllocHGlobal(IntPtr.Size);
			try {
				Marshal.WriteIntPtr(pHandle,IntPtr.Zero);
				ushort rc=_dsm.Entry(_pAppIdentity,_pSourceIdentity,Tw.DG_IMAGE,Tw.DAT_IMAGENATIVEXFER,Tw.MSG_GET,pHandle);
				IntPtr hDib=Marshal.ReadIntPtr(pHandle);
				if(rc==Tw.TWRC_XFERDONE) {
					State=7;
					if(hDib==IntPtr.Zero) {
						SetError(EZTwainErrorCode.EZTEC_XFER_FAILURE,"The scanner finished the transfer without an image.");
						return TransferOutcome.Failed;
					}
					try {
						dib=CopyDib(hDib);
					}
					catch(ImagingDeviceException ex) {
						SetError(ex.ErrorCode,ex.Message);
						return TransferOutcome.Failed;
					}
					finally {
						_dsm.MemFree(hDib);
					}
					return TransferOutcome.Done;
				}
				if(hDib!=IntPtr.Zero) {
					_dsm.MemFree(hDib);
				}
				if(rc==Tw.TWRC_CANCEL) {
					State=7;
					return TransferOutcome.Cancelled;
				}
				SetErrorFromCondition(EZTwainErrorCode.EZTEC_XFER_FAILURE,"The image transfer from the scanner failed.",true);
				return TransferOutcome.Failed;
			}
			finally {
				Marshal.FreeHGlobal(pHandle);
			}
		}

		private DibImage CopyDib(IntPtr hDib) {
			IntPtr pDib=_dsm.MemLock(hDib);
			if(pDib==IntPtr.Zero) {
				throw new ImagingDeviceException("The scanner's image memory could not be read.",EZTwainErrorCode.EZTEC_BAD_DIB);
			}
			try {
				long available=(long)NativeMethods.GlobalSize(hDib).ToUInt64();
				return DibImage.FromUnmanaged(pDib,available);
			}
			finally {
				_dsm.MemUnlock(hDib);
			}
		}

		private TransferOutcome TransferMemory(bool hasImageInfo,TW_IMAGEINFO imageInfo,out DibImage dib) {
			dib=null;
			if(!hasImageInfo) {
				SetError(EZTwainErrorCode.EZTEC_BAD_MEMXFER,"The scanner did not describe the image.");
				return TransferOutcome.Failed;
			}
			if(imageInfo.Compression!=Tw.TWCP_NONE || (imageInfo.Planar!=0 && imageInfo.SamplesPerPixel>1)
				|| !DibBuilder.IsSupported(imageInfo.BitsPerPixel,imageInfo.PixelType))
			{
				SetError(EZTwainErrorCode.EZTEC_BAD_MEMXFER,"The scanner sent an image layout that is not supported ("
					+imageInfo.BitsPerPixel+" bits per pixel, pixel type "+imageInfo.PixelType+").");
				return TransferOutcome.Failed;
			}
			byte[] palette=null;
			if(imageInfo.PixelType==Tw.TWPT_PALETTE) {
				palette=TryGetPalette();
			}
			DibBuilder dibBuilder=new DibBuilder(imageInfo.ImageWidth,imageInfo.ImageLength,imageInfo.BitsPerPixel,imageInfo.PixelType,
				palette,_isVanilla,imageInfo.XResolution.ToDouble(),imageInfo.YResolution.ToDouble());
			int bufferSize=GetMemoryBufferSize();
			IntPtr pBuffer=Marshal.AllocHGlobal(bufferSize);
			IntPtr pXfer=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_IMAGEMEMXFER)));
			try {
				while(true) {
					TW_IMAGEMEMXFER xfer=new TW_IMAGEMEMXFER();
					xfer.Compression=Tw.TWON_DONTCARE16;
					xfer.BytesPerRow=Tw.TWON_DONTCARE32;
					xfer.Columns=Tw.TWON_DONTCARE32;
					xfer.Rows=Tw.TWON_DONTCARE32;
					xfer.XOffset=Tw.TWON_DONTCARE32;
					xfer.YOffset=Tw.TWON_DONTCARE32;
					xfer.BytesWritten=Tw.TWON_DONTCARE32;
					xfer.Memory.Flags=Tw.TWMF_APPOWNS|Tw.TWMF_POINTER;
					xfer.Memory.Length=(uint)bufferSize;
					xfer.Memory.TheMem=pBuffer;
					Marshal.StructureToPtr(xfer,pXfer,false);
					ushort rc=_dsm.Entry(_pAppIdentity,_pSourceIdentity,Tw.DG_IMAGE,Tw.DAT_IMAGEMEMXFER,Tw.MSG_GET,pXfer);
					if(rc==Tw.TWRC_SUCCESS || rc==Tw.TWRC_XFERDONE) {
						State=7;
						xfer=(TW_IMAGEMEMXFER)Marshal.PtrToStructure(pXfer,typeof(TW_IMAGEMEMXFER));
						dibBuilder.AddStrip(pBuffer,bufferSize,xfer);
						if(rc==Tw.TWRC_XFERDONE) {
							break;
						}
						continue;
					}
					if(rc==Tw.TWRC_CANCEL) {
						State=7;
						return TransferOutcome.Cancelled;
					}
					SetErrorFromCondition(EZTwainErrorCode.EZTEC_XFER_FAILURE,"The image transfer from the scanner failed.",true);
					return TransferOutcome.Failed;
				}
				try {
					dib=dibBuilder.Build();
				}
				catch(ImagingDeviceException ex) {
					SetError(ex.ErrorCode,ex.Message);
					return TransferOutcome.Failed;
				}
				return TransferOutcome.Done;
			}
			finally {
				Marshal.FreeHGlobal(pXfer);
				Marshal.FreeHGlobal(pBuffer);
			}
		}

		private int GetMemoryBufferSize() {
			const int sizeDefault=64*1024;
			const int sizeMax=16*1024*1024;
			IntPtr pSetup=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_SETUPMEMXFER)));
			try {
				Marshal.StructureToPtr(new TW_SETUPMEMXFER(),pSetup,false);
				ushort rc=_dsm.Entry(_pAppIdentity,_pSourceIdentity,Tw.DG_CONTROL,Tw.DAT_SETUPMEMXFER,Tw.MSG_GET,pSetup);
				if(rc!=Tw.TWRC_SUCCESS) {
					return sizeDefault;
				}
				TW_SETUPMEMXFER setup=(TW_SETUPMEMXFER)Marshal.PtrToStructure(pSetup,typeof(TW_SETUPMEMXFER));
				return ChooseBufferSize(setup.MinBufSize,setup.MaxBufSize,setup.Preferred,sizeDefault,sizeMax);
			}
			finally {
				Marshal.FreeHGlobal(pSetup);
			}
		}

		///<summary>Picks the memory transfer buffer size: the source's preferred size if it has one, otherwise the default,
		///kept within the source's minimum and maximum and an overall cap.</summary>
		public static int ChooseBufferSize(uint minBufSize,uint maxBufSize,uint preferred,int sizeDefault,int sizeMax) {
			long size=(preferred!=Tw.TWON_DONTCARE32 && preferred>0) ? (long)preferred : (long)sizeDefault;
			if(minBufSize!=Tw.TWON_DONTCARE32 && size<minBufSize) {
				size=minBufSize;
			}
			if(maxBufSize!=Tw.TWON_DONTCARE32 && maxBufSize>0 && size>maxBufSize) {
				size=maxBufSize;
			}
			if(size>sizeMax) {
				size=sizeMax;
			}
			if(size<1024) {
				size=1024;
			}
			return (int)size;
		}

		private byte[] TryGetPalette() {
			IntPtr pPalette=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_PALETTE8)));
			try {
				TW_PALETTE8 palette8=new TW_PALETTE8 { Colors=new byte[1024] };
				Marshal.StructureToPtr(palette8,pPalette,false);
				ushort rc=_dsm.Entry(_pAppIdentity,_pSourceIdentity,Tw.DG_IMAGE,Tw.DAT_PALETTE8,Tw.MSG_GET,pPalette);
				if(rc!=Tw.TWRC_SUCCESS) {
					return null;
				}
				palette8=(TW_PALETTE8)Marshal.PtrToStructure(pPalette,typeof(TW_PALETTE8));
				int numColors=Math.Min(256,Math.Max(0,(int)palette8.NumColors));
				if(numColors==0) {
					return null;
				}
				byte[] quads=new byte[numColors*4];
				for(int i=0;i<numColors;i++) {
					//TW_ELEMENT8 is Index, Channel1 (red), Channel2 (green), Channel3 (blue). DIB quads are B,G,R,0.
					quads[i*4]=palette8.Colors[i*4+3];
					quads[i*4+1]=palette8.Colors[i*4+2];
					quads[i*4+2]=palette8.Colors[i*4+1];
				}
				return quads;
			}
			finally {
				Marshal.FreeHGlobal(pPalette);
			}
		}

		///<summary>MSG_ENDXFER after a transfer (state 7, or state 6 after a failure). Moves to state 6 if more images are
		///pending (count unknown counts as pending), otherwise to state 5.</summary>
		private void EndTransfer() {
			IntPtr pPending=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_PENDINGXFERS)));
			try {
				Marshal.StructureToPtr(new TW_PENDINGXFERS(),pPending,false);
				ushort rc=_dsm.Entry(_pAppIdentity,_pSourceIdentity,Tw.DG_CONTROL,Tw.DAT_PENDINGXFERS,Tw.MSG_ENDXFER,pPending);
				if(rc!=Tw.TWRC_SUCCESS) {
					//The source is confused. Discard everything so that it can be disabled.
					Marshal.StructureToPtr(new TW_PENDINGXFERS(),pPending,false);
					_dsm.Entry(_pAppIdentity,_pSourceIdentity,Tw.DG_CONTROL,Tw.DAT_PENDINGXFERS,Tw.MSG_RESET,pPending);
					State=5;
					return;
				}
				TW_PENDINGXFERS pending=(TW_PENDINGXFERS)Marshal.PtrToStructure(pPending,typeof(TW_PENDINGXFERS));
				State=pending.Count==0 ? 5 : 6;
			}
			finally {
				Marshal.FreeHGlobal(pPending);
			}
		}
		#endregion Acquire

		public void Dispose() {
			try {
				if(State>=4) {
					CloseSourceCore();
				}
				if(State>=2) {
					CloseDsm();
				}
			}
			catch {
				//Best effort.
			}
			if(_pAppIdentity!=IntPtr.Zero) {
				Marshal.FreeHGlobal(_pAppIdentity);
				_pAppIdentity=IntPtr.Zero;
			}
			if(_pSourceIdentity!=IntPtr.Zero) {
				Marshal.FreeHGlobal(_pSourceIdentity);
				_pSourceIdentity=IntPtr.Zero;
			}
		}
	}
}
