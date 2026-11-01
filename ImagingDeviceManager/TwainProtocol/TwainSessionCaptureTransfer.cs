// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Imaging;

namespace ImagingDeviceManager.TwainProtocol {
	///<summary>What the capture path negotiates in state 4 before an enable (design 7.1 CAP-3).</summary>
	internal sealed class TwainCaptureNegotiation {
		public bool Want16Bit;
		public bool IsByteOrderBigEndian;
		public bool WantExtImageInfo=true;
		///<summary>CAP_XFERCOUNT: 1 in both session modes (design 7.4).</summary>
		public int XferCount=1;
	}

	///<summary>What the source accepted.</summary>
	internal sealed class TwainCaptureNegotiationResult {
		public bool IsXferCountSet;
		///<summary>16 when ICAP_BITDEPTH 16 was accepted over memory transfer, else 8.</summary>
		public int Bits=8;
		public ushort XferMech=Tw.TWSX_NATIVE;
		///<summary>Why 16 bits were asked and the enable runs at 8, else null.</summary>
		public string Reason8Bit;
		public bool IsExtImageInfoSet;
		///<summary>ICAP_SUPPORTEDEXTIMAGEINFO lists TWEI_TWAINDIRECTMETADATA, so it is read for each image.</summary>
		public bool IsMetadataListed;
		public int PixelFlavor=Tw.TWPF_CHOCOLATE;
	}

	//The transfers of the capture path (design 7.1 CAP-3): the raw 16-bit memory path, DAT_IMAGEINFO for every image, and
	//DAT_EXTIMAGEINFO in state 7, after TWRC_XFERDONE and before MSG_ENDXFER.
	internal sealed partial class TwainSession {
		///<summary>Metadata larger than this is not read (a capture-metadata object is a few hundred bytes).</summary>
		private const int MaxMetadataBytes=4*1024*1024;
		///<summary>Set by the capture worker before OpenSource: every transfer, including those of a stop, goes through TransferCaptureImage
		///and reaches the drain sink as an AcquiredImage (TwainDrainedImage.Acquired).</summary>
		internal bool IsCaptureMode { get; set; }
		///<summary>ICAP_BITDEPTH 16 was accepted since the source was opened; capabilities return to their defaults only at MSG_OPENDS
		///(twain-contract 1.1 section 4), so an 8-bit enable must set 8 again.</summary>
		private bool _isBitDepth16Set;
		private int _captureBits=8;
		private bool _isCaptureBigEndian;
		///<summary>The TWEI_ items DAT_EXTIMAGEINFO asks for in state 7, or null when extended image info is off.</summary>
		private List<ushort> _listExtInfoIds;

		private void ResetCaptureTransferState() {
			_isBitDepth16Set=false;
			_captureBits=8;
			_isCaptureBigEndian=false;
			_listExtInfoIds=null;
		}

		///<summary>The bit depth the last NegotiateCapture left in effect.</summary>
		internal int CaptureBits => _captureBits;

		#region Negotiation
		///<summary>State 4, before each capture enable: CAP_XFERCOUNT, the transfer mechanism and bit depth in the order of design 7.1 CAP-3,
		///and ICAP_EXTIMAGEINFO. The pixel type is never set: it is read, and 16 bits are asked only when it is GRAY.</summary>
		internal TwainCaptureNegotiationResult NegotiateCapture(TwainCaptureNegotiation negotiation) {
			TwainCaptureNegotiationResult result=new TwainCaptureNegotiationResult();
			if(State!=4) {
				result.Reason8Bit=negotiation.Want16Bit ? "the source is not in state 4" : null;
				return result;
			}
			result.IsXferCountSet=SetCapabilityQuietly(Tw.CAP_XFERCOUNT,Tw.TWTY_INT16,negotiation.XferCount);
			NegotiateBitDepth(negotiation,result);
			NegotiateExtImageInfo(negotiation.WantExtImageInfo,result);
			int pixelFlavor;
			_isVanilla=TryGetCapabilityCurrent(Tw.ICAP_PIXELFLAVOR,out pixelFlavor) && pixelFlavor==Tw.TWPF_VANILLA;
			result.PixelFlavor=_isVanilla ? Tw.TWPF_VANILLA : Tw.TWPF_CHOCOLATE;
			_isCaptureBigEndian=negotiation.IsByteOrderBigEndian;
			return result;
		}

		///<summary>16 bits: only when ICAP_PIXELTYPE is GRAY, ICAP_XFERMECH=TWSX_MEMORY first, then ICAP_BITDEPTH=16 only if MSG_GET lists 16.
		///16 is never set while the mechanism is native (a 16bpp DIB is RGB555/565 by definition, TWAIN 2.4 p.12-7). A refusal is a silent
		///fallback to 8 bits, logged (design 7.12). 8 bits: ICAP_BITDEPTH=8 first when an earlier enable on this open source set 16, then
		///native transfers. Otherwise the bit depth is left as the source delivers it.</summary>
		private void NegotiateBitDepth(TwainCaptureNegotiation negotiation,TwainCaptureNegotiationResult result) {
			if(negotiation.Want16Bit) {
				result.Reason8Bit=TryNegotiate16Bit();
				if(result.Reason8Bit==null) {
					result.Bits=16;
					result.XferMech=Tw.TWSX_MEMORY;
					_captureBits=16;
					return;
				}
				Log("16 bits were asked of \""+SourceName+"\", but the enable runs at 8 bits: "+result.Reason8Bit+".");
			}
			_captureBits=8;
			if(_isBitDepth16Set) {
				ushort? rcDepth=SendCapabilitySet(Tw.ICAP_BITDEPTH,Tw.TWTY_UINT16,8);
				if(rcDepth==Tw.TWRC_SUCCESS || rcDepth==Tw.TWRC_CHECKSTATUS) {
					_isBitDepth16Set=false;
				}
				else {
					Log("\""+SourceName+"\" did not accept ICAP_BITDEPTH 8 after an earlier 16-bit enable ("+DescribeRc(rcDepth)
						+"); setting native transfers resets it.");
				}
			}
			ushort? rc=SendCapabilitySet(Tw.ICAP_XFERMECH,Tw.TWTY_UINT16,Tw.TWSX_NATIVE);
			if(rc==Tw.TWRC_CHECKSTATUS && _isBitDepth16Set) {
				_isBitDepth16Set=false;//twain-contract 1.1 section 4: native while the depth is 16 resets it to 8.
			}
			int mechanism;
			if(rc!=Tw.TWRC_SUCCESS && TryGetCapabilityCurrent(Tw.ICAP_XFERMECH,out mechanism) && mechanism==Tw.TWSX_MEMORY) {
				_xferMech=Tw.TWSX_MEMORY;//The source keeps memory transfers; 8-bit memory transfers work too.
			}
			else {
				_xferMech=Tw.TWSX_NATIVE;
			}
			result.XferMech=_xferMech;
		}

		///<summary>The 16-bit order. Returns null when ICAP_BITDEPTH 16 is in effect, otherwise why not.</summary>
		private string TryNegotiate16Bit() {
			int pixelType;
			if(!TryGetCapabilityCurrent(Tw.ICAP_PIXELTYPE,out pixelType) && !TryGetCapability(Tw.ICAP_PIXELTYPE,Tw.MSG_GET,out pixelType)) {
				return "ICAP_PIXELTYPE could not be read (the pixel type is never set on this path)";
			}
			if(pixelType!=Tw.TWPT_GRAY) {
				return "ICAP_PIXELTYPE is "+pixelType+", not TWPT_GRAY (the pixel type is never set on this path)";
			}
			ushort? rcMech=SendCapabilitySet(Tw.ICAP_XFERMECH,Tw.TWTY_UINT16,Tw.TWSX_MEMORY);
			int mechanism;
			bool isMemory=rcMech==Tw.TWRC_SUCCESS
				|| (rcMech==Tw.TWRC_CHECKSTATUS && TryGetCapabilityCurrent(Tw.ICAP_XFERMECH,out mechanism) && mechanism==Tw.TWSX_MEMORY);
			if(!isMemory) {
				_xferMech=Tw.TWSX_NATIVE;
				return "the source did not accept memory transfers ("+DescribeRc(rcMech)+"), and 16 bits are never asked over native transfers";
			}
			_xferMech=Tw.TWSX_MEMORY;
			TwainCapabilityValues depths;
			if(!TryGetCapabilityValues(Tw.ICAP_BITDEPTH,Tw.MSG_GET,out depths)) {
				return "ICAP_BITDEPTH MSG_GET failed (condition code "+ToHex(LastConditionCode)+")";
			}
			if(!depths.Contains(16)) {
				return "ICAP_BITDEPTH MSG_GET does not list 16 ("+depths.Describe(false)+")";
			}
			ushort? rcDepth=SendCapabilitySet(Tw.ICAP_BITDEPTH,Tw.TWTY_UINT16,16);
			if(rcDepth!=Tw.TWRC_SUCCESS) {
				return "the source did not accept ICAP_BITDEPTH 16 ("+DescribeRc(rcDepth)+")";
			}
			_isBitDepth16Set=true;
			return null;
		}

		///<summary>ICAP_EXTIMAGEINFO=TRUE in state 4 when MSG_QUERYSUPPORT allows it (FALSE by default, TWAIN 2.4 p.10-145). DAT_EXTIMAGEINFO then
		///asks for TWEI_PIXELFLAVOR, and for TWEI_TWAINDIRECTMETADATA only when ICAP_SUPPORTEDEXTIMAGEINFO lists it. A source that supports
		///neither (twain-contract 1.0: TWCC_CAPUNSUPPORTED) gives no metadata.</summary>
		private void NegotiateExtImageInfo(bool isWanted,TwainCaptureNegotiationResult result) {
			_listExtInfoIds=null;
			if(!isWanted) {
				return;
			}
			ushort conditionQuery;
			int? support=GetQuerySupport(Tw.ICAP_EXTIMAGEINFO,out conditionQuery);
			if(support==null && conditionQuery==Tw.TWCC_CAPUNSUPPORTED) {
				Log("\""+SourceName+"\" does not support ICAP_EXTIMAGEINFO (TWCC_CAPUNSUPPORTED): its images have no metadata.");
				return;
			}
			if(support!=null && (support.Value & Tw.TWQC_SET)==0) {
				Log("\""+SourceName+"\" does not allow setting ICAP_EXTIMAGEINFO (MSG_QUERYSUPPORT "+ToHex(support.Value)+"): its images have no metadata.");
				return;
			}
			ushort? rc=SendCapabilitySet(Tw.ICAP_EXTIMAGEINFO,Tw.TWTY_BOOL,1);
			if(rc!=Tw.TWRC_SUCCESS) {
				Log("\""+SourceName+"\" did not accept ICAP_EXTIMAGEINFO TRUE ("+DescribeRc(rc)+"): its images have no metadata.");
				return;
			}
			result.IsExtImageInfoSet=true;
			_listExtInfoIds=new List<ushort> { Tw.TWEI_PIXELFLAVOR };
			TwainCapabilityValues listed;
			if(TryGetCapabilityValues(Tw.ICAP_SUPPORTEDEXTIMAGEINFO,Tw.MSG_GET,out listed) && listed.Contains(Tw.TWEI_TWAINDIRECTMETADATA)) {
				_listExtInfoIds.Add(Tw.TWEI_TWAINDIRECTMETADATA);
				result.IsMetadataListed=true;
			}
			else {
				Log("\""+SourceName+"\" does not list TWEI_TWAINDIRECTMETADATA in ICAP_SUPPORTEDEXTIMAGEINFO: its images have no capture metadata.");
			}
		}

		private string DescribeRc(ushort? rc) {
			if(rc==null) {
				return "out of memory";
			}
			return rc==Tw.TWRC_FAILURE ? "condition code "+ToHex(LastConditionCode) : "return code "+rc;
		}
		#endregion Negotiation

		#region Enable and wait
		///<summary>True when the last WaitForCaptureNotification returned because FuncIsInterruptRequested asked it to.</summary>
		internal bool IsLoopInterrupted { get; private set; }
		///<summary>Asked on every pass of the message loop while the source is enabled (not during the wait after a refused disable, when the
		///stop is already under way). True makes WaitForCaptureNotification return MSG_NULL with IsLoopInterrupted, so that the capture worker
		///can carry out a command. Must not throw; null for the scan and Mount/Acquire paths.</summary>
		internal Func<bool> FuncIsInterruptRequested { get; set; }
		///<summary>Why the last WaitForCaptureNotification returned MSG_NULL when it was not interrupted: Quit, or Error (the error is set).</summary>
		internal TwainStopReason LoopStopReason => _stopReason;
		///<summary>The proxy window, which the source gets as its parent; the capture worker posts to it to wake the message loop.</summary>
		internal IntPtr ProxyWindowHandle => _proxyWindow==null ? IntPtr.Zero : _proxyWindow.Handle;
		///<summary>The message the capture worker posts to the proxy window to wake the message loop. The loop does not pass it to the source.</summary>
		internal const int WM_APP_WAKE=WM_APP_CANCEL;

		private bool IsInterruptRequested() {
			try {
				return FuncIsInterruptRequested!=null && FuncIsInterruptRequested();
			}
			catch {
				return false;
			}
		}

		///<summary>MSG_ENABLEDS from state 4 for a capture. Returns the source's return code: TWRC_SUCCESS, or TWRC_CHECKSTATUS (the source
		///shows its window although it was asked not to), leaves the source in state 5; anything else leaves it in state 4 with
		///LastConditionCode set.</summary>
		internal ushort EnableForCapture(bool isShowUI) {
			EndConditionCode=null;
			EndConditionText=null;
			IntPtr pUserInterface=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_USERINTERFACE)));
			try {
				TW_USERINTERFACE userInterface=new TW_USERINTERFACE();
				userInterface.ShowUI=(ushort)(isShowUI ? 1 : 0);
				userInterface.ModalUI=0;
				userInterface.hParent=_proxyWindow.Handle;
				Marshal.StructureToPtr(userInterface,pUserInterface,false);
				ushort rc=SourceEntry(Tw.DG_CONTROL,Tw.DAT_USERINTERFACE,Tw.MSG_ENABLEDS,pUserInterface);
				if(rc==Tw.TWRC_SUCCESS || rc==Tw.TWRC_CHECKSTATUS) {
					State=5;
					if(rc==Tw.TWRC_CHECKSTATUS) {
						Log("\""+SourceName+"\" answered MSG_ENABLEDS with TWRC_CHECKSTATUS: it shows its own window although ShowUI="
							+userInterface.ShowUI+". The enable goes on.");
					}
				}
				return rc;
			}
			finally {
				Marshal.FreeHGlobal(pUserInterface);
			}
		}

		///<summary>CAP_UICONTROLLABLE as for a by-name acquisition: a source that reports FALSE gets ShowUI=1 whatever was asked.</summary>
		internal bool GetCaptureShowUI(bool isShowUIRequested) {
			IsUiControllable=null;
			return GetShowUIForSource(isShowUIRequested);
		}

		///<summary>State 4, before a capture enable: the safe-disable capability when Settings names one for this source (design 7.1; true
		///only when MSG_SET returned TWRC_SUCCESS).</summary>
		internal bool PrepareSafeDisable() {
			SetSafeDisableCapability();
			return IsSafeDisableInEffect;
		}

		///<summary>State 5: runs the message loop until the source sends a notification, FuncIsInterruptRequested asks it to return, or the loop
		///fails. MSG_XFERREADY moves to state 6. isRepeat: a MSG_XFERREADY after an earlier image of the same enable (Continuous mode), which is
		///ignored when DAT_PENDINGXFERS reports nothing pending. MSG_CLOSEDSREQ reads the end condition (DAT_STATUS, then DAT_STATUSUTF8) before
		///anything disables. MSG_NULL: IsLoopInterrupted, or LoopStopReason says why.</summary>
		internal ushort WaitForCaptureNotification(bool isRepeat) {
			while(true) {
				_stopReason=TwainStopReason.None;
				IsLoopInterrupted=false;
				ushort notification=PumpMessages(false,0);
				if(notification==Tw.MSG_XFERREADY) {
					short pending;
					if(isRepeat && TryGetPendingCount(out pending) && pending==0) {
						Log("\""+SourceName+"\" sent MSG_XFERREADY with nothing pending; ignored.");
						continue;
					}
					State=6;
					return notification;
				}
				if(notification==Tw.MSG_CLOSEDSREQ) {
					ReadEndCondition();
				}
				return notification;
			}
		}

		///<summary>Resets the counters of WasDisableRefused and WasRefusedDisableUnanswered before an enable.</summary>
		internal void ResetCaptureStopFlags() {
			CountRefusedDisables=0;
			WasRefusedDisableUnanswered=false;
			SourceMayHoldImages=false;
			DrainedImageCount=0;
			LastStopReason=TwainStopReason.None;
		}

		///<summary>A stop of a running capture enable in the safe order (StopToState4). In state 5, a notification the source already has is
		///picked up first, so that an image it offered is transferred instead of dropped by the disable.</summary>
		internal void StopCapture(TwainStopReason reason) {
			if(State==5) {
				ushort notification=PollNotification();
				if(notification==Tw.MSG_XFERREADY) {
					State=6;
				}
				else if(notification==Tw.MSG_CLOSEDSREQ) {
					ReadEndCondition();
				}
			}
			StopToState4(reason==TwainStopReason.None ? TwainStopReason.Cancel : reason);
		}

		///<summary>MSG_DISABLEDS from state 5 after Count=0 or MSG_CLOSEDSREQ, with the wait after a refused disable.</summary>
		internal void DisableCapture() {
			if(State==5) {
				DisableSourceSafely(TwainStopReason.None);
			}
		}

		///<summary>DAT_PENDINGXFERS / MSG_GET.</summary>
		internal bool TryGetPendingTransfers(out short count) {
			return TryGetPendingCount(out count);
		}
		#endregion Enable and wait

		#region Transfer
		///<summary>From state 6: DAT_IMAGEINFO for this image, the transfer (the raw 16-bit memory path when the image is 16-bit gray), then,
		///in state 7 after TWRC_XFERDONE and before MSG_ENDXFER, DAT_EXTIMAGEINFO, then MSG_ENDXFER (state 6 when more are pending, otherwise
		///5). A failed transfer is ended with MSG_ENDXFER like any other; the error is then set.</summary>
		internal TransferOutcome TransferCaptureImage(out AcquiredImage image) {
			image=null;
			TW_IMAGEINFO imageInfo;
			bool hasImageInfo=TryGetImageInfo(out imageInfo);
			DibImage dib=null;
			ushort[] gray16=null;
			int heightGray16=0;
			int widthGray16=hasImageInfo ? imageInfo.ImageWidth : 0;
			TransferOutcome outcome;
			ushort xferMech=_xferMech;
			if(xferMech==Tw.TWSX_MEMORY) {
				outcome=TransferMemoryForCapture(hasImageInfo,imageInfo,out dib,out gray16,out heightGray16);
			}
			else {
				outcome=TransferNative(out dib);
			}
			DateTime receivedAt=GetReceivedTime();
			string metadataJson=null;
			ushort? pixelFlavorImage=null;
			if(outcome==TransferOutcome.Done && State==7) {
				ReadExtImageInfo(out metadataJson,out pixelFlavorImage);
			}
			EndTransfer();
			if(outcome==TransferOutcome.Cancelled) {
				SetError(EZTwainErrorCode.EZTEC_USER_CANCEL,"The transfer was cancelled.");
				return outcome;
			}
			if(outcome!=TransferOutcome.Done) {
				return outcome;
			}
			image=new AcquiredImage {
				ReceivedAt=receivedAt,
				MetadataJson=metadataJson,
				SourceName=SourceName,
				PixelType=hasImageInfo ? imageInfo.PixelType : -1,
				PixelFlavor=pixelFlavorImage ?? (_isVanilla ? Tw.TWPF_VANILLA : Tw.TWPF_CHOCOLATE),
				RequestedBits=_captureBits,
				IsBitDepth16InEffect=_captureBits==16,
				ByteOrder16=_isCaptureBigEndian ? TwainByteOrder.BE : TwainByteOrder.LE,
				TransferMechanism=xferMech==Tw.TWSX_MEMORY ? TwainTransferMechanism.Memory : TwainTransferMechanism.Native,
			};
			if(dib!=null && dib.BitCount==16 && hasImageInfo && imageInfo.PixelType==Tw.TWPT_GRAY) {
				gray16=Gray16Builder.FromGray16Dib(dib);//Never asked for; kept raw, without the scan path's shift.
				heightGray16=dib.Height;
				widthGray16=dib.Width;
				image.XDpi=dib.XResolutionDpi;
				image.YDpi=dib.YResolutionDpi;
				dib=null;
				image.ByteOrder16=TwainByteOrder.LE;
			}
			if(gray16!=null) {
				image.Gray16=gray16;
				image.Width=widthGray16;
				image.Height=heightGray16;
				image.BitsPerPixel=16;
				image.BitsPerSample=hasImageInfo && imageInfo.BitsPerSample!=null && imageInfo.BitsPerSample[0]>0 ? imageInfo.BitsPerSample[0] : 16;
			}
			else {
				if(hasImageInfo) {
					dib.SetResolutionIfMissing(imageInfo.XResolution.ToDouble(),imageInfo.YResolution.ToDouble());
				}
				image.Dib=dib.Data;
				image.Width=dib.Width;
				image.Height=dib.Height;
				image.BitsPerPixel=dib.BitCount;
				image.BitsPerSample=hasImageInfo && imageInfo.BitsPerSample!=null && imageInfo.BitsPerSample[0]>0 ? imageInfo.BitsPerSample[0] : dib.BitCount;
				image.XDpi=dib.XResolutionDpi;
				image.YDpi=dib.YResolutionDpi;
			}
			if(hasImageInfo && imageInfo.XResolution.ToDouble()>0) {
				image.XDpi=imageInfo.XResolution.ToDouble();
				image.YDpi=imageInfo.YResolution.ToDouble()>0 ? imageInfo.YResolution.ToDouble() : image.XDpi;
			}
			image.IsDeliveredAs8BitIn16BitEnable=_captureBits==16 && image.BitsPerPixel<=8;
			if(image.IsDeliveredAs8BitIn16BitEnable) {
				Log("\""+SourceName+"\" delivered an "+image.BitsPerPixel+"-bit image in a 16-bit enable (DAT_IMAGEINFO); it is returned as "
					+image.BitsPerPixel+"-bit.");
			}
			return TransferOutcome.Done;
		}

		///<summary>A memory transfer for the capture path: 16-bit gray goes to Gray16Builder (raw samples, no shift), anything else to
		///DibBuilder as on the scan path.</summary>
		private TransferOutcome TransferMemoryForCapture(bool hasImageInfo,TW_IMAGEINFO imageInfo,out DibImage dib,out ushort[] gray16,
			out int heightGray16)
		{
			dib=null;
			gray16=null;
			heightGray16=0;
			if(!hasImageInfo) {
				SetError(EZTwainErrorCode.EZTEC_BAD_MEMXFER,"The device did not describe the image.");
				return TransferOutcome.Failed;
			}
			bool isGray16=imageInfo.PixelType==Tw.TWPT_GRAY && imageInfo.BitsPerPixel==16 && imageInfo.SamplesPerPixel<=1;
			if(!isGray16) {
				return TransferMemory(true,imageInfo,out dib);
			}
			if(imageInfo.Compression!=Tw.TWCP_NONE) {
				SetError(EZTwainErrorCode.EZTEC_BAD_MEMXFER,"The device sent a compressed 16-bit image, which is not supported.");
				return TransferOutcome.Failed;
			}
			Gray16Builder builder=new Gray16Builder(imageInfo.ImageWidth,imageInfo.ImageLength,_isCaptureBigEndian);
			TransferOutcome outcome=TransferMemoryStrips(builder.AddStrip);
			if(outcome!=TransferOutcome.Done) {
				return outcome;
			}
			try {
				gray16=builder.Build();
				heightGray16=builder.Height;
			}
			catch(ImagingDeviceException ex) {
				SetError(ex.ErrorCode,ex.Message);
				return TransferOutcome.Failed;
			}
			return TransferOutcome.Done;
		}

		///<summary>State 7, after TWRC_XFERDONE and before MSG_ENDXFER: DAT_EXTIMAGEINFO for the items NegotiateExtImageInfo chose (twain-contract
		///1.1 answers it only then, section 6.3). Missing items, TWRC_INFONOTSUPPORTED, TWRC_DATANOTAVAILABLE, and a failure of the triplet
		///(TWCC_SEQERROR, or TWCC_BADPROTOCOL from a source without it) all mean no metadata (TWAIN 2.4 pp.8-24 to 8-26, 9-19). Every handle a
		///successful item carries is freed like every other handle from the source (ITwainMemory), whether it was used or not. Never throws.</summary>
		private void ReadExtImageInfo(out string metadataJson,out ushort? pixelFlavor) {
			metadataJson=null;
			pixelFlavor=null;
			if(_listExtInfoIds==null || _listExtInfoIds.Count==0) {
				return;
			}
			int count=_listExtInfoIds.Count;
			IntPtr pExtImageInfo=Marshal.AllocHGlobal(TwExtImageInfo.GetSize(count));
			try {
				Marshal.WriteInt32(pExtImageInfo,0,count);
				for(int i=0;i<count;i++) {
					Marshal.StructureToPtr(new TW_INFO { InfoID=_listExtInfoIds[i] },OffsetPointer(pExtImageInfo,TwExtImageInfo.GetInfoOffset(i)),false);
				}
				ushort rc=SourceEntry(Tw.DG_IMAGE,Tw.DAT_EXTIMAGEINFO,Tw.MSG_GET,pExtImageInfo);
				if(rc!=Tw.TWRC_SUCCESS) {
					Log("\""+SourceName+"\" did not answer DAT_EXTIMAGEINFO ("+DescribeRc(rc)+"): no metadata for this image.");
					return;
				}
				for(int i=0;i<count;i++) {
					TW_INFO info=(TW_INFO)Marshal.PtrToStructure(OffsetPointer(pExtImageInfo,TwExtImageInfo.GetInfoOffset(i)),typeof(TW_INFO));
					if(info.ReturnCode!=Tw.TWRC_SUCCESS) {
						if(_listExtInfoIds[i]==Tw.TWEI_TWAINDIRECTMETADATA) {
							Log("\""+SourceName+"\" has no TWEI_TWAINDIRECTMETADATA for this image (return code "+info.ReturnCode+").");
						}
						continue;
					}
					bool isHandle=TwExtImageInfo.IsItemHandle(info.ItemType,info.NumItems);
					try {
						if(_listExtInfoIds[i]==Tw.TWEI_TWAINDIRECTMETADATA) {
							if(info.ItemType==Tw.TWTY_HANDLE && info.Item!=IntPtr.Zero) {
								metadataJson=ReadNulTerminatedUtf8(info.Item);
							}
							else {
								Log("\""+SourceName+"\" sent TWEI_TWAINDIRECTMETADATA with item type "+info.ItemType+", not TWTY_HANDLE; ignored.");
							}
						}
						else if(_listExtInfoIds[i]==Tw.TWEI_PIXELFLAVOR && !isHandle && info.NumItems>=1) {
							pixelFlavor=(ushort)(info.Item.ToInt64() & 0xFFFF);
						}
					}
					finally {
						if(isHandle && info.Item!=IntPtr.Zero) {
							_memory.Free(info.Item);
						}
					}
				}
			}
			catch(Exception ex) {
				Log("DAT_EXTIMAGEINFO of \""+SourceName+"\" could not be read: "+ex.Message);
			}
			finally {
				Marshal.FreeHGlobal(pExtImageInfo);
			}
		}

		private static IntPtr OffsetPointer(IntPtr p,int offset) {
			return new IntPtr(p.ToInt64()+offset);
		}

		///<summary>NUL-terminated UTF-8 text in a handle with no size beside it. It is read only as far as the allocator's block size, when it
		///knows it, the committed readable memory at the pointer (VirtualQuery), and MaxMetadataBytes: a malformed handle can never make the
		///read run past the memory the source gave. The text is returned raw, without the NUL; null when empty or unreadable.</summary>
		private string ReadNulTerminatedUtf8(IntPtr handle) {
			long sizeBlock=_memory.GetSize(handle);
			IntPtr p=_memory.Lock(handle);
			if(p==IntPtr.Zero) {
				return null;
			}
			try {
				long limit=MaxMetadataBytes;
				if(sizeBlock>0) {
					limit=Math.Min(limit,sizeBlock);
				}
				limit=Math.Min(limit,NativeMethods.GetReadableBytes(p,limit));
				const int sizeChunk=4096;
				List<byte> listBytes=new List<byte>();
				byte[] chunk=new byte[sizeChunk];
				for(long offset=0;offset<limit;offset+=sizeChunk) {
					int sizeRead=(int)Math.Min(sizeChunk,limit-offset);
					Marshal.Copy(OffsetPointer(p,(int)offset),chunk,0,sizeRead);
					int idxNul=Array.IndexOf(chunk,(byte)0,0,sizeRead);
					for(int i=0;i<(idxNul<0 ? sizeRead : idxNul);i++) {
						listBytes.Add(chunk[i]);
					}
					if(idxNul>=0) {
						break;
					}
				}
				if(listBytes.Count==0) {
					return null;
				}
				return Encoding.UTF8.GetString(listBytes.ToArray());
			}
			finally {
				_memory.Unlock(handle);
			}
		}
		#endregion Transfer
	}
}
