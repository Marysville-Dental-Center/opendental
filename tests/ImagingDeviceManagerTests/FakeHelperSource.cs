// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager.Tests {
	///<summary>How the operator ends a session that does not end by its own count.</summary>
	internal enum FakeHelperEnd {
		///<summary>Done, or closing the window: every image taken is delivered (XFERREADY), or CLOSEDSREQ when there is none.</summary>
		Done,
		///<summary>The sensor or its software fails: CLOSEDSREQ with EndCode as the end reason, or XFERREADY if images were taken (DAT_STATUS
		///then returns TWCC_SUCCESS: an end reason follows only a CLOSEDSREQ, section 8).</summary>
		Fail,
		///<summary>Nobody ends it: the helper runs until the application disables or closes the source.</summary>
		Never,
	}

	///<summary>The [Capture] Mode installation setting of twain-contract 1.1 section 3.</summary>
	internal enum FakeHelperMode {
		///<summary>CAP_XFERCOUNT decides (section 5).</summary>
		Auto,
		///<summary>Every session delivers one image, whatever CAP_XFERCOUNT says.</summary>
		Single,
		///<summary>CAP_XFERCOUNT 1 behaves like -1: the operator ends the batch. The host then gets more images than it asked for.</summary>
		Multi,
	}

	///<summary>The "single-shot helper DS" profile: a simulated source that behaves as twain-contract 1.1 sections 2-11 describe the
	///sensor data source, including its section 9 limitations: ShowUI is ignored (CAP_UICONTROLLABLE FALSE), CAP_DEVICEONLINE is always
	///TRUE, a disable without safe disable loses undelivered images for the host, an image that cannot be read ends the batch
	///(FailReadAtImage), and there is one notification per enable. Each MSG_ENABLEDS starts a simulated helper (the sensor software in
	///its own window); the operator's actions are scripted (ListExposures, End, EndAfterMs, EndCode). The numbers of its custom
	///capabilities and condition codes are the published ones, used here only as data of this simulated source. Script options marked
	///"not contract behaviour" are for tests of the library, not part of the profile.
	///
	///Timed behaviour runs on the FakeTwainDsm clock and wakes the application's message loop with a timer, so the default times
	///are scaled down from the contract's (15 s becomes ForcedEndMs) to keep tests fast.</summary>
	internal sealed class FakeHelperSource {
		#region Numbers from the published contract (data)
		public const ushort CapSafeDisable=0x8002;
		public const ushort CapRedeliver=0x8003;
		public const ushort CapNoActivate=0x8004;
		public const ushort CodeNoCalibration=0x8001;
		public const ushort CodeNoSensor=0x8002;
		public const ushort CodeDeviceNotAccepted=0x8003;
		public const ushort CodeSessionTimeout=0x8004;
		public const ushort CodeSoftwareFailed=0x8005;
		public const ushort CodeDisableImagesPending=0x8006;
		public const ushort CodeDisableClosing=0x8007;
		public const ushort CodeNothingToRedeliver=0x8008;
		///<summary>Reserved in the contract (section 10.1) for continuous mode; implemented here only with SupportsContinuous.</summary>
		public const ushort CapContinuous=0x8001;
		#endregion Numbers from the published contract (data)

		#region Standard capability numbers not used by the library
		public const ushort CapIndicators=0x100b;
		public const ushort CapDeviceOnline=0x100f;
		public const ushort CapEnableDsUiOnly=0x1014;
		public const ushort IcapBitOrder=0x111c;
		public const ushort IcapPlanarChunky=0x1120;
		public const ushort IcapSupportedSizes=0x1122;
		public const ushort IcapBitDepth=0x112b;
		public const ushort IcapUndefinedImageSize=0x112d;
		public const ushort IcapExtImageInfo=0x112f;
		public const ushort IcapSupportedExtImageInfo=0x115e;
		public const ushort TweiDocumentNumber=0x123a;
		public const ushort TweiPageNumber=0x123b;
		public const ushort TweiCamera=0x123c;
		public const ushort TweiFrameNumber=0x123d;
		public const ushort TweiFrame=0x123e;
		public const ushort TweiPixelFlavor=0x123f;
		public const ushort TweiTwainDirectMetadata=0x124b;
		public const ushort TwunPixels=5;
		public const ushort MsgEnableDsUiOnly=0x0503;
		public const int Dpi=1270;
		#endregion Standard capability numbers not used by the library

		#region Script and installation settings
		public string ProductName="Fake Helper Sensor";
		public FakeHelperMode Mode=FakeHelperMode.Auto;
		///<summary>False: the build of contract 1.0, where every custom capability returns TWCC_CAPUNSUPPORTED.</summary>
		public bool SupportsSafeDisable=true;
		///<summary>False: DAT_STATUSUTF8 returns TWCC_BADPROTOCOL, as in contract 1.0 or under a DSM that does not pass it through.</summary>
		public bool SupportsStatusUtf8=true;
		///<summary>The images the operator takes in each session, right after the enable.</summary>
		public List<FakePage> ListExposures=new List<FakePage>();
		public FakeHelperEnd End=FakeHelperEnd.Done;
		///<summary>Milliseconds after the enable at which the session ends, by its own count or by End.</summary>
		public int EndAfterMs;
		///<summary>With End=Fail: the end reason (section 11.1) DAT_STATUS returns after MSG_CLOSEDSREQ.</summary>
		public ushort EndCode=CodeSoftwareFailed;
		///<summary>With safe disable: how long the helper takes to exit after a refused disable asked it to close (finishing an exposure
		///being read out takes at most 10 s in the contract).</summary>
		public int CloseDelayMs=30;
		///<summary>The helper does not exit when asked; the source ends it at ForcedEndMs.</summary>
		public bool IgnoresCloseRequest;
		///<summary>With safe disable: a helper still running this long after the close request is ended, and the images it saved are
		///still offered (15 s in the contract).</summary>
		public int ForcedEndMs=400;
		///<summary>Without safe disable: how long MSG_DISABLEDS blocks while the running helper closes (up to 15 s in the contract).</summary>
		public int BlockingCloseMs;
		///<summary>MSG_ENABLEDS fails with TWCC_OPERATIONERROR (the helper could not be started) and the source stays in state 4.</summary>
		public bool EnableFails;
		///<summary>Sections 5 and 9: the n-th image of a session (n of 2 or more) cannot be read. MSG_ENDXFER after image n-1 returns 0, as at
		///the end of the batch, the source goes to state 5, and image n and the later ones are not delivered: they count as kept for
		///redelivery (section 10.3). 0: every image can be read.</summary>
		public int FailReadAtImage;
		///<summary>Runs inside each MSG_ENABLEDS, after the session started and before the source wakes the host.</summary>
		public Action ActionOnEnable;
		///<summary>Not contract behaviour: after the first refused disable, one MSG_XFERREADY arrives while nothing is offered (pending
		///count 0), before the real notification. Checks that the host ignores a repeated XFERREADY with nothing pending.</summary>
		public bool SpuriousXferReadyAfterRefusal;
		///<summary>Not contract behaviour (the contract leaves TW_STATUS.Data at 0): the Data that DAT_STATUS returns with an end reason, so
		///that a test can check that DAT_STATUSUTF8 gets that whole TW_STATUS back.</summary>
		public ushort EndStatusData;
		///<summary>Not contract behaviour: MSG_SET on CAP_XFERCOUNT fails with TWCC_BADVALUE, so the count stays -1 and the operator ends
		///each session, as with a source that cannot do single acquisitions.</summary>
		public bool RefusesXferCount;
		///<summary>False: the build of contract 1.0, where ICAP_BITDEPTH has 8 only (MSG_GET lists 8, MSG_SET 16 fails with TWCC_BADVALUE).
		///True (contract 1.1 section 6.2): MSG_GET lists 8 and 16, and 16 is accepted only after ICAP_XFERMECH = TWSX_MEMORY.</summary>
		public bool Supports16Bit=true;
		///<summary>False: the build of contract 1.0, with no extended image info: ICAP_EXTIMAGEINFO and ICAP_SUPPORTEDEXTIMAGEINFO return
		///TWCC_CAPUNSUPPORTED and DAT_EXTIMAGEINFO TWCC_BADPROTOCOL. True: section 6.3.</summary>
		public bool SupportsExtImageInfo=true;
		///<summary>Not contract behaviour (1.1 lists it): False leaves TWEI_TWAINDIRECTMETADATA out of ICAP_SUPPORTEDEXTIMAGEINFO, as a TWAIN
		///2.1 to 2.3 source would; DAT_EXTIMAGEINFO still answers it.</summary>
		public bool ListsTwainDirectMetadata=true;
		///<summary>The build of contract 1.0: MSG_SET on a fixed capability succeeds with any value and changes nothing (section 12).</summary>
		public bool FixedCapsAcceptAnyValue;
		///<summary>Not contract 1.1 behaviour (CAP_DEVICEONLINE is always TRUE there, section 9; a presence check is planned, 11.3): the value
		///CAP_DEVICEONLINE reports.</summary>
		public bool DeviceOnline=true;
		///<summary>Not contract behaviour (rows are unpadded there, section 6.1): extra bytes at the end of every memory transfer row, filled with
		///0xEE so that a host that ignores BytesPerRow gets wrong pixels.</summary>
		public int MemoryRowPadding;
		///<summary>Not contract behaviour (whole rows there): memory transfer strips are tiles this many columns wide, with XOffset.</summary>
		public int MemoryColumnsPerStrip;
		///<summary>Not contract 1.1 behaviour: the planned continuous mode of section 11.3 under CapContinuous. The sensor stays armed; each
		///exposure of ListExposures arrives ContinuousIntervalMs after the previous one with its own MSG_XFERREADY, MSG_ENDXFER returns 0 after
		///each image, and the source stays enabled until MSG_DISABLEDS (or End).</summary>
		public bool SupportsContinuous;
		public int ContinuousIntervalMs=20;
		///<summary>Not contract behaviour: in continuous mode, after each MSG_ENDXFER that returns 0, one MSG_XFERREADY arrives with nothing
		///pending.</summary>
		public bool SpuriousXferReadyAfterEachImage;
		///<summary>The redelivery capability as W15 first planned it: it stays TRUE until MSG_CLOSEDS, so every enable after it is a
		///redelivery. Contract 1.1 clears it with the enable that uses it (section 10.3).</summary>
		public bool RedeliverKeptUntilClose;
		///<summary>Not contract behaviour: a successful MSG_ENABLEDS returns TWRC_CHECKSTATUS (TWAIN: the source shows its UI although asked
		///not to).</summary>
		public bool EnableReturnsCheckStatus;
		///<summary>Not contract behaviour: MSG_SET ICAP_XFERMECH TWSX_MEMORY fails with TWCC_BADVALUE, as for a source with native transfers only.</summary>
		public bool RefusesMemoryTransfer;
		#endregion Script and installation settings

		#region Observations
		///<summary>TWAIN state as the source sees it: 3 closed, 4 open, 5 enabled, 6 an image ready, 7 transferring.</summary>
		public int State=3;
		public int CountEnable;
		public ushort LastShowUI;
		///<summary>Images delivered: a transfer that returned TWRC_XFERDONE followed by MSG_ENDXFER (section 10.3).</summary>
		public int CountDelivered;
		///<summary>Undelivered images dropped for the host by a disable or close without safe disable (section 7).</summary>
		public int CountLostForHost;
		///<summary>Undelivered images left with the source by a reset, a failed transfer or a second consecutive disable; a redelivery
		///could still offer them (sections 5, 10.2, 10.3).</summary>
		public int CountKeptForRedelivery;
		public int CountStatusText;
		///<summary>MSG_SET calls on fixed capabilities (section 4), whatever their result. The capture session never sends one.</summary>
		public int CountFixedCapSet;
		///<summary>DAT_EXTIMAGEINFO calls the source answered.</summary>
		public int CountExtImageInfo;
		///<summary>Enables that were redeliveries (section 10.3).</summary>
		public int CountRedeliveryEnables;
		///<summary>The captureId of every delivered image, in order.</summary>
		public readonly List<string> ListDeliveredCaptureIds=new List<string>();
		///<summary>Undelivered images of the most recent session that a redelivery would offer (section 10.3).</summary>
		public int CountRedeliverable => _listRedeliverable.Count;
		///<summary>The TW_STATUS the last DAT_STATUSUTF8 was asked to translate.</summary>
		public TW_STATUS LastStatusTranslated;
		public bool IsHelperRunning => _isRunning;
		///<summary>The condition code of the most recent failed call, until DAT_STATUS reads it.</summary>
		public ushort LastFailureCode => _lastFailureCode;
		public readonly Dictionary<ushort,int> DictCaps=new Dictionary<ushort,int>();
		#endregion Observations

		private FakeTwainDsm _dsm;
		private bool _isRunning;
		///<summary>Images the helper saved that are not delivered yet. The first is the one offered or in transfer in states 6-7.</summary>
		private readonly List<FakePage> _listUndelivered=new List<FakePage>();
		private bool _isCurrentTransferred;
		///<summary>Images of the session ended with MSG_ENDXFER so far.</summary>
		private int _countEnded;
		private int _rowCursor;
		private ushort _notificationPending;
		private bool _isSpuriousPending;
		private bool _hasRefusedOnce;
		private bool _isLastDisableRefused;
		private ushort _endReason;
		private ushort _endReasonScheduled;
		private ushort _lastFailureCode;
		private long _endDueMs=-1;
		private long _closeDueMs=-1;
		private long _forcedEndDueMs=-1;
		private int _colCursor;
		private int _bandRows;
		private bool _isContinuous;
		private bool _isRedeliverySession;
		private long _exposureDueMs=-1;
		private readonly List<FakePage> _listContinuousQueue=new List<FakePage>();
		///<summary>Undelivered images of the most recent capture session (section 10.3).</summary>
		private readonly List<FakePage> _listRedeliverable=new List<FakePage>();
		private readonly Dictionary<FakePage,string> _dictCaptureIds=new Dictionary<FakePage,string>();
		private readonly Dictionary<FakePage,int> _dictSequences=new Dictionary<FakePage,int>();

		public FakeHelperSource() {
		}

		public FakeHelperSource(string productName) {
			ProductName=productName;
		}

		///<summary>Undelivered images the source holds right now.</summary>
		public int CountUndelivered => _listUndelivered.Count;

		#region Open and close
		///<summary>MSG_OPENDS: state 4, every capability back to its default (section 4).</summary>
		public void OnOpen(FakeTwainDsm dsm) {
			_dsm=dsm;
			State=4;
			DictCaps.Clear();
			DictCaps[Tw.CAP_XFERCOUNT]=-1;
			DictCaps[Tw.ICAP_XFERMECH]=Tw.TWSX_NATIVE;
			DictCaps[IcapBitDepth]=8;
			DictCaps[Tw.ICAP_UNITS]=Tw.TWUN_INCHES;
			DictCaps[IcapExtImageInfo]=0;
			DictCaps[CapSafeDisable]=0;
			DictCaps[CapRedeliver]=0;
			DictCaps[CapNoActivate]=0;
			if(SupportsContinuous) {
				DictCaps[CapContinuous]=0;
			}
			_lastFailureCode=0;
		}

		///<summary>MSG_CLOSEDS: ends a running session as a disable without safe disable does, then closes (section 7).</summary>
		public void OnClose() {
			if(State>=5) {
				EndWithoutSafeDisable();
			}
			State=3;
		}
		#endregion Open and close

		public ushort Entry(uint dg,ushort dat,ushort msg,IntPtr data) {
			Advance();
			if(dg==Tw.DG_CONTROL) {
				switch(dat) {
					case Tw.DAT_STATUS: return Status(data);
					case Tw.DAT_STATUSUTF8: return StatusUtf8(data);
					case Tw.DAT_CAPABILITY: return Capability(msg,data);
					case Tw.DAT_USERINTERFACE: return UserInterface(msg,data);
					case Tw.DAT_EVENT: return ProcessEvent(data);
					case Tw.DAT_PENDINGXFERS: return PendingXfers(msg,data);
					case Tw.DAT_SETUPMEMXFER: return SetupMemXfer(data);
				}
			}
			if(dg==Tw.DG_IMAGE) {
				switch(dat) {
					case Tw.DAT_IMAGEINFO: return ImageInfo(data);
					case Tw.DAT_IMAGENATIVEXFER: return NativeXfer(data);
					case Tw.DAT_IMAGEMEMXFER: return MemXfer(data);
					case Tw.DAT_EXTIMAGEINFO: return ExtImageInfo(data);
				}
			}
			return Fail(Tw.TWCC_BADPROTOCOL);
		}

		private ushort Fail(ushort conditionCode) {
			_lastFailureCode=conditionCode;
			return Tw.TWRC_FAILURE;
		}

		#region Session timing
		///<summary>Applies whatever became due since the last call: the session ending by its count or by the operator, the helper
		///exiting after a close request, or being ended at ForcedEndMs. Then sets the wake timer for the next due time.</summary>
		private void Advance() {
			long now=_dsm==null ? 0 : _dsm.NowMs;
			if(_isRunning && _endDueMs>=0 && now>=_endDueMs) {
				EndSession(_endReasonScheduled);
			}
			if(_isRunning && _closeDueMs>=0 && now>=_closeDueMs) {
				EndSession(0);
			}
			if(_isRunning && _forcedEndDueMs>=0 && now>=_forcedEndDueMs) {
				EndSession(0);
			}
			if(_isRunning && _isContinuous && _exposureDueMs>=0 && now>=_exposureDueMs && _listContinuousQueue.Count>0) {
				//The next exposure of continuous mode: offered with its own MSG_XFERREADY when the source waits in state 5.
				_listUndelivered.Add(_listContinuousQueue[0]);
				_listContinuousQueue.RemoveAt(0);
				_exposureDueMs=_listContinuousQueue.Count>0 ? now+ContinuousIntervalMs : -1;
				if(State==5) {
					_notificationPending=Tw.MSG_XFERREADY;
					_dsm?.WakeHostNow();
				}
			}
			Reschedule();
		}

		private void Reschedule() {
			if(_dsm==null) {
				return;
			}
			long due=-1;
			if(_isRunning) {
				foreach(long dueMs in new[] { _endDueMs,_closeDueMs,_forcedEndDueMs,_isContinuous ? _exposureDueMs : -1 }) {
					if(dueMs>=0 && (due<0 || dueMs<due)) {
						due=dueMs;
					}
				}
			}
			if(due<0) {
				_dsm.StopWakeTimer();
				return;
			}
			_dsm.WakeHostAfter(Math.Max(1,due-_dsm.NowMs));
		}

		///<summary>The helper has ended: the source sends exactly one notification (section 5): MSG_XFERREADY if it has images to offer,
		///otherwise MSG_CLOSEDSREQ, which alone carries endReason (section 8: a custom code after a MSG_CLOSEDSREQ that had a reason,
		///otherwise TWCC_SUCCESS).</summary>
		private void EndSession(ushort endReason) {
			_isRunning=false;
			_exposureDueMs=-1;
			_endDueMs=-1;
			_closeDueMs=-1;
			_forcedEndDueMs=-1;
			_notificationPending=_listUndelivered.Count>0 ? Tw.MSG_XFERREADY : Tw.MSG_CLOSEDSREQ;
			_endReason=_notificationPending==Tw.MSG_CLOSEDSREQ ? endReason : (ushort)0;
			_dsm?.WakeHostNow();
		}

		///<summary>A disable or close without safe disable: the running helper is closed, and images not transferred are not delivered
		///to the host (section 7).</summary>
		private void EndWithoutSafeDisable() {
			if(_isRunning && BlockingCloseMs>0) {
				Thread.Sleep(BlockingCloseMs);
			}
			CountLostForHost+=_listUndelivered.Count;
			AddRedeliverable(_listUndelivered);
			_listUndelivered.Clear();
			FinishDisable();
		}

		///<summary>Images that stay undelivered: a redelivery of this, the most recent session, can still offer them (sections 7, 10.3).</summary>
		private void AddRedeliverable(IEnumerable<FakePage> listPages) {
			foreach(FakePage page in listPages) {
				if(!_listRedeliverable.Contains(page)) {
					_listRedeliverable.Add(page);
				}
			}
		}

		private void FinishDisable() {
			_isRunning=false;
			_isContinuous=false;
			_isRedeliverySession=false;
			_exposureDueMs=-1;
			_listContinuousQueue.Clear();
			_endDueMs=-1;
			_closeDueMs=-1;
			_forcedEndDueMs=-1;
			_notificationPending=Tw.MSG_NULL;
			_isSpuriousPending=false;
			_isLastDisableRefused=false;
			_endReason=0;
			State=4;
			_dsm?.StopWakeTimer();
		}
		#endregion Session timing

		#region Status
		///<summary>Section 8: the code of the most recent failed call, cleared by the read; when there is none, the end reason of the
		///session, kept until a disable succeeds; otherwise TWCC_SUCCESS.</summary>
		private ushort Status(IntPtr data) {
			ushort code;
			ushort dataStatus=0;
			if(_lastFailureCode!=0) {
				code=_lastFailureCode;
				_lastFailureCode=0;
			}
			else {
				code=_endReason;
				dataStatus=_endReason==0 ? (ushort)0 : EndStatusData;
			}
			Marshal.StructureToPtr(new TW_STATUS { ConditionCode=code,Data=dataStatus },data,false);
			return Tw.TWRC_SUCCESS;
		}

		public static string GetText(ushort conditionCode) {
			switch(conditionCode) {
				case Tw.TWCC_SUCCESS: return "No error.";
				case CodeNoCalibration: return "The connected sensor has no calibration on this computer.";
				case CodeNoSensor: return "No sensor is connected \u2014 check the cable.";
				case CodeDeviceNotAccepted: return "The connected sensor is not accepted.";
				case CodeSessionTimeout: return "The session timed out without an image.";
				case CodeSoftwareFailed: return "The sensor software failed.";
				case CodeDisableImagesPending: return "Disable refused: images are not transferred yet.";
				case CodeDisableClosing: return "Disable refused: the sensor software is closing.";
				default: return "Condition code "+conditionCode+".";
			}
		}

		///<summary>Section 8: English text for Status.ConditionCode, UTF-8 and NUL-terminated, Size including the NUL, in a handle the
		///host frees (section 6.4).</summary>
		private ushort StatusUtf8(IntPtr data) {
			if(!SupportsStatusUtf8) {
				return Fail(Tw.TWCC_BADPROTOCOL);
			}
			TW_STATUSUTF8 statusUtf8=(TW_STATUSUTF8)Marshal.PtrToStructure(data,typeof(TW_STATUSUTF8));
			LastStatusTranslated=statusUtf8.Status;
			byte[] text=Encoding.UTF8.GetBytes(GetText(statusUtf8.Status.ConditionCode)+"\0");
			IntPtr handle=_dsm.SourceAllocate(text.Length);
			IntPtr p=NativeMethods.GlobalLock(handle);
			Marshal.Copy(text,0,p,text.Length);
			NativeMethods.GlobalUnlock(handle);
			statusUtf8.Size=(uint)text.Length;
			statusUtf8.UTF8string=handle;
			Marshal.StructureToPtr(statusUtf8,data,false);
			CountStatusText++;
			return Tw.TWRC_SUCCESS;
		}
		#endregion Status

		#region Capabilities
		private bool IsCustomCap(ushort cap) {
			return cap==CapSafeDisable || cap==CapRedeliver || cap==CapNoActivate || cap==CapContinuous;
		}

		private bool IsSettable(ushort cap) {
			if(cap==IcapExtImageInfo && !SupportsExtImageInfo) {
				return false;
			}
			return DictCaps.ContainsKey(cap) && (!IsCustomCap(cap) || SupportsSafeDisable);
		}

		///<summary>The 7 items of section 6.3, as ICAP_SUPPORTEDEXTIMAGEINFO lists them.</summary>
		public List<int> GetSupportedExtImageInfo() {
			List<int> listItems=new List<int> { TweiDocumentNumber,TweiPageNumber,TweiCamera,TweiFrameNumber,TweiFrame,TweiPixelFlavor };
			if(ListsTwainDirectMetadata) {
				listItems.Add(TweiTwainDirectMetadata);
			}
			return listItems;
		}

		///<summary>Item type and value of a fixed capability (section 4), or false when the capability is not one.</summary>
		private bool TryGetFixed(ushort cap,out ushort itemType,out int value) {
			itemType=Tw.TWTY_UINT16;
			value=0;
			switch(cap) {
				case Tw.ICAP_PIXELTYPE: value=Tw.TWPT_GRAY; return true;
				case Tw.ICAP_COMPRESSION: value=Tw.TWCP_NONE; return true;
				case Tw.ICAP_PIXELFLAVOR: value=Tw.TWPF_CHOCOLATE; return true;
				case IcapBitOrder: value=0; return true;//TWBO_LSBFIRST
				case IcapPlanarChunky: value=0; return true;//TWPC_CHUNKY
				case IcapSupportedSizes: value=0; return true;//TWSS_NONE
				case Tw.ICAP_XRESOLUTION:
				case Tw.ICAP_YRESOLUTION:
					itemType=Tw.TWTY_FIX32;
					value=(int)TW_FIX32.FromDouble(Dpi).ToUInt32();
					return true;
				case Tw.CAP_UICONTROLLABLE: itemType=Tw.TWTY_BOOL; value=0; return true;
				case CapIndicators: itemType=Tw.TWTY_BOOL; value=1; return true;
				case CapDeviceOnline: itemType=Tw.TWTY_BOOL; value=DeviceOnline ? 1 : 0; return true;
				case CapEnableDsUiOnly: itemType=Tw.TWTY_BOOL; value=0; return true;
				case IcapUndefinedImageSize: itemType=Tw.TWTY_BOOL; value=1; return true;
			}
			return false;
		}

		private static ushort GetSettableItemType(ushort cap) {
			if(cap==Tw.CAP_XFERCOUNT) {
				return Tw.TWTY_INT16;
			}
			if(cap==Tw.ICAP_XFERMECH || cap==IcapBitDepth || cap==Tw.ICAP_UNITS) {
				return Tw.TWTY_UINT16;
			}
			return Tw.TWTY_BOOL;
		}

		private ushort Capability(ushort msg,IntPtr data) {
			if(State<4) {
				return Fail(Tw.TWCC_SEQERROR);
			}
			TW_CAPABILITY capability=(TW_CAPABILITY)Marshal.PtrToStructure(data,typeof(TW_CAPABILITY));
			ushort cap=capability.Cap;
			if(cap==IcapSupportedExtImageInfo) {
				return SupportedExtImageInfo(msg,data,capability);
			}
			ushort itemType;
			int valueFixed;
			bool isFixed=TryGetFixed(cap,out itemType,out valueFixed);
			bool isSettable=IsSettable(cap);
			if(!isFixed && !isSettable) {
				return Fail(Tw.TWCC_CAPUNSUPPORTED);
			}
			if(isSettable) {
				itemType=GetSettableItemType(cap);
			}
			switch(msg) {
				case Tw.MSG_QUERYSUPPORT:
					int support=isSettable ? (Tw.TWQC_GET|Tw.TWQC_SET|Tw.TWQC_GETDEFAULT|Tw.TWQC_GETCURRENT|Tw.TWQC_RESET)
						: (Tw.TWQC_GET|Tw.TWQC_GETDEFAULT|Tw.TWQC_GETCURRENT);
					WriteOneValue(data,capability,Tw.TWTY_INT32,support);
					return Tw.TWRC_SUCCESS;
				case Tw.MSG_GET:
					if(cap==IcapBitDepth) {
						//The values the source offers (section 4), with the current one.
						List<int> listDepths=Supports16Bit ? new List<int> { 8,16 } : new List<int> { 8 };
						WriteEnumeration(data,capability,itemType,listDepths,Math.Max(0,listDepths.IndexOf(DictCaps[cap])));
						return Tw.TWRC_SUCCESS;
					}
					WriteOneValue(data,capability,itemType,isSettable ? DictCaps[cap] : valueFixed);
					return Tw.TWRC_SUCCESS;
				case Tw.MSG_GETCURRENT:
					WriteOneValue(data,capability,itemType,isSettable ? DictCaps[cap] : valueFixed);
					return Tw.TWRC_SUCCESS;
				case Tw.MSG_GETDEFAULT:
					WriteOneValue(data,capability,itemType,isSettable ? GetDefault(cap) : valueFixed);
					return Tw.TWRC_SUCCESS;
				case Tw.MSG_RESET:
					if(State!=4) {
						return Fail(Tw.TWCC_SEQERROR);
					}
					if(isSettable) {
						DictCaps[cap]=GetDefault(cap);
					}
					WriteOneValue(data,capability,itemType,isSettable ? DictCaps[cap] : valueFixed);
					return Tw.TWRC_SUCCESS;
				case Tw.MSG_SET:
					if(State!=4) {
						return Fail(Tw.TWCC_SEQERROR);
					}
					if(capability.ConType!=Tw.TWON_ONEVALUE || capability.hContainer==IntPtr.Zero) {
						return Fail(Tw.TWCC_BADVALUE);
					}
					int value=ReadOneValue(capability.hContainer,itemType);
					return isSettable ? SetSettable(cap,value) : SetFixed(cap,value,valueFixed);
			}
			return Fail(Tw.TWCC_CAPBADOPERATION);
		}

		private static int GetDefault(ushort cap) {
			switch(cap) {
				case Tw.CAP_XFERCOUNT: return -1;
				case Tw.ICAP_XFERMECH: return Tw.TWSX_NATIVE;
				case IcapBitDepth: return 8;
				default: return 0;
			}
		}

		private ushort SetSettable(ushort cap,int value) {
			switch(cap) {
				case Tw.CAP_XFERCOUNT:
					if(value<-1 || RefusesXferCount) {
						return Fail(Tw.TWCC_BADVALUE);
					}
					DictCaps[cap]=value==0 ? -1 : value;//Setting 0 stores -1 (section 4).
					return Tw.TWRC_SUCCESS;
				case Tw.ICAP_XFERMECH:
					if((value!=Tw.TWSX_NATIVE && value!=Tw.TWSX_MEMORY) || (value==Tw.TWSX_MEMORY && RefusesMemoryTransfer)) {
						return Fail(Tw.TWCC_BADVALUE);
					}
					DictCaps[cap]=value;
					if(value==Tw.TWSX_NATIVE && DictCaps[IcapBitDepth]==16) {
						DictCaps[IcapBitDepth]=8;
						return Tw.TWRC_CHECKSTATUS;
					}
					return Tw.TWRC_SUCCESS;
				case IcapBitDepth:
					if(value!=8 && !(value==16 && Supports16Bit && DictCaps[Tw.ICAP_XFERMECH]==Tw.TWSX_MEMORY)) {
						return Fail(Tw.TWCC_BADVALUE);
					}
					DictCaps[cap]=value;
					return Tw.TWRC_SUCCESS;
				case Tw.ICAP_UNITS:
					if(value!=Tw.TWUN_INCHES && value!=TwunPixels) {
						return Fail(Tw.TWCC_BADVALUE);
					}
					DictCaps[cap]=value;
					return Tw.TWRC_SUCCESS;
				default://BOOL capabilities
					if(value!=0 && value!=1) {
						return Fail(Tw.TWCC_BADVALUE);
					}
					DictCaps[cap]=value;
					return Tw.TWRC_SUCCESS;
			}
		}

		///<summary>Section 4: a fixed capability accepts its one value and fails with TWCC_BADVALUE for any other, except that
		///CAP_INDICATORS FALSE and another resolution return TWRC_CHECKSTATUS and change nothing, and ICAP_UNDEFINEDIMAGESIZE accepts
		///TRUE and FALSE.</summary>
		private ushort SetFixed(ushort cap,int value,int valueFixed) {
			CountFixedCapSet++;
			if(value==valueFixed || FixedCapsAcceptAnyValue) {
				return Tw.TWRC_SUCCESS;
			}
			if(cap==CapIndicators && value==0) {
				return Tw.TWRC_CHECKSTATUS;
			}
			if(cap==Tw.ICAP_XRESOLUTION || cap==Tw.ICAP_YRESOLUTION) {
				return Tw.TWRC_CHECKSTATUS;
			}
			if(cap==IcapUndefinedImageSize && (value==0 || value==1)) {
				return Tw.TWRC_SUCCESS;
			}
			return Fail(Tw.TWCC_BADVALUE);
		}

		private static int ReadOneValue(IntPtr hContainer,ushort itemType) {
			IntPtr pContainer=NativeMethods.GlobalLock(hContainer);
			try {
				int raw=Marshal.ReadInt32(pContainer,TwContainer.OneValueItemOffset);
				switch(itemType) {
					case Tw.TWTY_INT16: return (short)(raw&0xffff);
					case Tw.TWTY_UINT16:
					case Tw.TWTY_BOOL: return raw&0xffff;
					default: return raw;
				}
			}
			finally {
				NativeMethods.GlobalUnlock(hContainer);
			}
		}

		///<summary>ICAP_SUPPORTEDEXTIMAGEINFO (section 4: fixed): a TW_ARRAY of the items of section 6.3.</summary>
		private ushort SupportedExtImageInfo(ushort msg,IntPtr data,TW_CAPABILITY capability) {
			if(!SupportsExtImageInfo) {
				return Fail(Tw.TWCC_CAPUNSUPPORTED);
			}
			switch(msg) {
				case Tw.MSG_QUERYSUPPORT:
					WriteOneValue(data,capability,Tw.TWTY_INT32,Tw.TWQC_GET|Tw.TWQC_GETDEFAULT|Tw.TWQC_GETCURRENT);
					return Tw.TWRC_SUCCESS;
				case Tw.MSG_GET:
				case Tw.MSG_GETCURRENT:
				case Tw.MSG_GETDEFAULT:
					WriteArray(data,capability,Tw.TWTY_UINT16,GetSupportedExtImageInfo());
					return Tw.TWRC_SUCCESS;
				case Tw.MSG_SET:
					CountFixedCapSet++;
					return Fail(State!=4 ? Tw.TWCC_SEQERROR : Tw.TWCC_BADVALUE);
			}
			return Fail(Tw.TWCC_CAPBADOPERATION);
		}

		private void WriteEnumeration(IntPtr data,TW_CAPABILITY capability,ushort itemType,List<int> listItems,int currentIndex) {
			int itemSize=TwContainer.ItemSize(itemType);
			IntPtr hContainer=_dsm.SourceAllocate(TwContainer.EnumItemListOffset+itemSize*listItems.Count);
			IntPtr pContainer=NativeMethods.GlobalLock(hContainer);
			Marshal.WriteInt16(pContainer,0,(short)itemType);
			Marshal.WriteInt32(pContainer,TwContainer.EnumNumItemsOffset,listItems.Count);
			Marshal.WriteInt32(pContainer,TwContainer.EnumCurrentIndexOffset,currentIndex);
			Marshal.WriteInt32(pContainer,TwContainer.EnumDefaultIndexOffset,0);
			WriteItems(pContainer,TwContainer.EnumItemListOffset,itemSize,listItems);
			NativeMethods.GlobalUnlock(hContainer);
			capability.ConType=Tw.TWON_ENUMERATION;
			capability.hContainer=hContainer;
			Marshal.StructureToPtr(capability,data,false);
		}

		private void WriteArray(IntPtr data,TW_CAPABILITY capability,ushort itemType,List<int> listItems) {
			int itemSize=TwContainer.ItemSize(itemType);
			IntPtr hContainer=_dsm.SourceAllocate(TwContainer.ArrayItemListOffset+itemSize*listItems.Count);
			IntPtr pContainer=NativeMethods.GlobalLock(hContainer);
			Marshal.WriteInt16(pContainer,0,(short)itemType);
			Marshal.WriteInt32(pContainer,TwContainer.ArrayNumItemsOffset,listItems.Count);
			WriteItems(pContainer,TwContainer.ArrayItemListOffset,itemSize,listItems);
			NativeMethods.GlobalUnlock(hContainer);
			capability.ConType=Tw.TWON_ARRAY;
			capability.hContainer=hContainer;
			Marshal.StructureToPtr(capability,data,false);
		}

		private static void WriteItems(IntPtr pContainer,int offset,int itemSize,List<int> listItems) {
			for(int i=0;i<listItems.Count;i++) {
				if(itemSize==2) {
					Marshal.WriteInt16(pContainer,offset+i*2,(short)listItems[i]);
				}
				else {
					Marshal.WriteInt32(pContainer,offset+i*4,listItems[i]);
				}
			}
		}

		private void WriteOneValue(IntPtr data,TW_CAPABILITY capability,ushort itemType,int value) {
			IntPtr hContainer=_dsm.SourceAllocate(TwContainer.OneValueSize);
			IntPtr pContainer=NativeMethods.GlobalLock(hContainer);
			Marshal.WriteInt16(pContainer,0,(short)itemType);
			Marshal.WriteInt32(pContainer,TwContainer.OneValueItemOffset,value);
			NativeMethods.GlobalUnlock(hContainer);
			capability.ConType=Tw.TWON_ONEVALUE;
			capability.hContainer=hContainer;
			Marshal.StructureToPtr(capability,data,false);
		}
		#endregion Capabilities

		#region Enable and disable
		///<summary>How many images the session takes before it ends by itself, or -1 when the operator ends it (section 5 and the
		///Mode setting of section 3).</summary>
		private int GetSessionLimit() {
			int xferCount=DictCaps[Tw.CAP_XFERCOUNT];
			if(Mode==FakeHelperMode.Single) {
				return 1;
			}
			if(Mode==FakeHelperMode.Multi && xferCount==1) {
				return -1;
			}
			return xferCount;
		}

		private ushort UserInterface(ushort msg,IntPtr data) {
			if(msg==Tw.MSG_ENABLEDS) {
				if(State!=4) {
					return Fail(Tw.TWCC_SEQERROR);
				}
				TW_USERINTERFACE userInterface=(TW_USERINTERFACE)Marshal.PtrToStructure(data,typeof(TW_USERINTERFACE));
				CountEnable++;
				LastShowUI=userInterface.ShowUI;//Ignored: the helper window is always shown (CAP_UICONTROLLABLE FALSE).
				_dsm.HwndParent=userInterface.hParent;
				if(DictCaps[CapRedeliver]==1) {
					return StartRedelivery();
				}
				if(EnableFails) {
					return Fail(Tw.TWCC_OPERATIONERROR);
				}
				//Every enable starts a new session with a fresh notification state and no end reason (sections 5 and 8).
				State=5;
				_isRunning=true;
				_listUndelivered.Clear();
				_notificationPending=Tw.MSG_NULL;
				_isSpuriousPending=false;
				_hasRefusedOnce=false;
				_isLastDisableRefused=false;
				_endReason=0;
				_countEnded=0;
				_closeDueMs=-1;
				_forcedEndDueMs=-1;
				_endDueMs=-1;
				_colCursor=0;
				_isRedeliverySession=false;
				_listRedeliverable.Clear();//This session is now the most recent one (section 10.3).
				for(int i=0;i<ListExposures.Count;i++) {
					FakePage page=ListExposures[i];
					_dictCaptureIds[page]=page.CaptureId??("fake-"+CountEnable+"-"+(i+1));
					_dictSequences[page]=i+1;
				}
				int limit=GetSessionLimit();
				_isContinuous=DictCaps.ContainsKey(CapContinuous) && DictCaps[CapContinuous]==1;
				_listContinuousQueue.Clear();
				if(_isContinuous) {
					_listContinuousQueue.AddRange(ListExposures);
					_exposureDueMs=_dsm.NowMs+ContinuousIntervalMs;
					limit=-1;
				}
				else {
					_listUndelivered.AddRange(ListExposures);
				}
				if(limit>0 && _listUndelivered.Count>=limit) {
					_listUndelivered.RemoveRange(limit,_listUndelivered.Count-limit);
					_endReasonScheduled=0;//Ends by itself after its count.
					_endDueMs=_dsm.NowMs+EndAfterMs;
				}
				else if(End!=FakeHelperEnd.Never) {
					_endReasonScheduled=End==FakeHelperEnd.Fail ? EndCode : (ushort)0;
					_endDueMs=_dsm.NowMs+EndAfterMs;
				}
				Reschedule();
				ActionOnEnable?.Invoke();
				if(_endDueMs>=0 && EndAfterMs==0) {
					_dsm.WakeHostNow();
				}
				return EnableReturnsCheckStatus ? Tw.TWRC_CHECKSTATUS : Tw.TWRC_SUCCESS;
			}
			if(msg==Tw.MSG_DISABLEDS) {
				return Disable();
			}
			if(msg==MsgEnableDsUiOnly) {
				return Fail(Tw.TWCC_CAPUNSUPPORTED);
			}
			return Fail(Tw.TWCC_BADPROTOCOL);
		}

		///<summary>Section 7 without safe disable; with it, the table of section 10.2.</summary>
		private ushort Disable() {
			if(State<5) {
				return Fail(Tw.TWCC_SEQERROR);
			}
			if(DictCaps[CapSafeDisable]==0) {
				EndWithoutSafeDisable();//Accepted in states 5, 6 and 7.
				return Tw.TWRC_SUCCESS;
			}
			if(_isLastDisableRefused) {
				//The second consecutive MSG_DISABLEDS with no MSG_ENDXFER in between succeeds; undelivered images stay with the source.
				CountKeptForRedelivery+=_listUndelivered.Count;
				AddRedeliverable(_listUndelivered);
				_listUndelivered.Clear();
				FinishDisable();
				return Tw.TWRC_SUCCESS;
			}
			if(State>=6) {
				_notificationPending=Tw.MSG_XFERREADY;//Images offered but not transferred: refused, and offered again.
				_dsm.WakeHostNow();
				return Refuse(CodeDisableImagesPending);
			}
			if(_isRunning) {
				if(_closeDueMs<0 && _forcedEndDueMs<0) {
					long now=_dsm.NowMs;
					if(!IgnoresCloseRequest) {
						_closeDueMs=now+CloseDelayMs;
					}
					_forcedEndDueMs=now+ForcedEndMs;
					Reschedule();
				}
				return Refuse(CodeDisableClosing);
			}
			if(_notificationPending!=Tw.MSG_NULL) {
				return Refuse(CodeDisableClosing);//The helper has ended and its notification follows.
			}
			FinishDisable();//Nothing running and nothing undelivered.
			return Tw.TWRC_SUCCESS;
		}

		///<summary>Section 10.3: an enable with the redelivery capability TRUE starts no capture and no sensor software; it offers the
		///undelivered images of the most recent session (XFERREADY, then all of them whatever CAP_XFERCOUNT says), or ends with MSG_CLOSEDSREQ
		///and CodeNothingToRedeliver. The capability is FALSE again afterwards, unless RedeliverKeptUntilClose.</summary>
		private ushort StartRedelivery() {
			CountRedeliveryEnables++;
			if(!RedeliverKeptUntilClose) {
				DictCaps[CapRedeliver]=0;
			}
			State=5;
			_isRunning=false;
			_isRedeliverySession=true;
			_isContinuous=false;
			_listUndelivered.Clear();
			_listUndelivered.AddRange(_listRedeliverable);
			_isSpuriousPending=false;
			_hasRefusedOnce=false;
			_isLastDisableRefused=false;
			_countEnded=0;
			_colCursor=0;
			_endDueMs=-1;
			_closeDueMs=-1;
			_forcedEndDueMs=-1;
			_notificationPending=_listUndelivered.Count>0 ? Tw.MSG_XFERREADY : Tw.MSG_CLOSEDSREQ;
			_endReason=_listUndelivered.Count>0 ? (ushort)0 : CodeNothingToRedeliver;
			ActionOnEnable?.Invoke();
			_dsm.WakeHostNow();
			return Tw.TWRC_SUCCESS;
		}

		private ushort Refuse(ushort code) {
			_isLastDisableRefused=true;
			if(SpuriousXferReadyAfterRefusal && !_hasRefusedOnce) {
				_isSpuriousPending=true;
				_dsm.WakeHostNow();
			}
			_hasRefusedOnce=true;
			return Fail(code);
		}

		///<summary>Returns a pending notification (section 5), whatever message the host passes.</summary>
		private ushort ProcessEvent(IntPtr data) {
			TW_EVENT twEvent=(TW_EVENT)Marshal.PtrToStructure(data,typeof(TW_EVENT));
			twEvent.TWMessage=Tw.MSG_NULL;
			ushort rc=Tw.TWRC_NOTDSEVENT;
			if(_isSpuriousPending && State==5) {
				_isSpuriousPending=false;
				twEvent.TWMessage=Tw.MSG_XFERREADY;//Nothing is offered: the state stays 5 and the pending count 0.
				rc=Tw.TWRC_DSEVENT;
			}
			else if(_notificationPending!=Tw.MSG_NULL && State>=5) {
				twEvent.TWMessage=_notificationPending;
				if(_notificationPending==Tw.MSG_XFERREADY && State==5) {
					State=6;
					_isCurrentTransferred=false;
					_rowCursor=0;
				}
				_notificationPending=Tw.MSG_NULL;
				rc=Tw.TWRC_DSEVENT;
			}
			Marshal.StructureToPtr(twEvent,data,false);
			return rc;
		}
		#endregion Enable and disable

		#region Transfers
		private FakePage CurrentPage => State>=6 && _listUndelivered.Count>0 ? _listUndelivered[0] : null;

		private ushort PendingXfers(ushort msg,IntPtr data) {
			short count;
			switch(msg) {
				case Tw.MSG_GET:
					if(State<4) {
						return Fail(Tw.TWCC_SEQERROR);
					}
					//The images not yet transferred, excluding one in transfer; 0 before the notification.
					count=(short)(State>=6 ? _listUndelivered.Count-(State==7 ? 1 : 0) : 0);
					break;
				case Tw.MSG_ENDXFER:
					if(State<6) {
						return Fail(Tw.TWCC_SEQERROR);
					}
					if(_listUndelivered.Count>0) {
						FakePage pageEnded=_listUndelivered[0];
						if(State==7 && _isCurrentTransferred) {
							CountDelivered++;
							ListDeliveredCaptureIds.Add(GetCaptureId(pageEnded));
							_listRedeliverable.Remove(pageEnded);
						}
						else {
							CountKeptForRedelivery++;//Its transfer failed or was abandoned: it stays undelivered.
							AddRedeliverable(new[] { pageEnded });
						}
						_listUndelivered.RemoveAt(0);
						_countEnded++;
					}
					if(FailReadAtImage>=2 && _countEnded==FailReadAtImage-1 && _listUndelivered.Count>0) {
						//The next image cannot be read: MSG_ENDXFER returns 0 and the rest of the batch is not delivered (sections 5, 9).
						CountKeptForRedelivery+=_listUndelivered.Count;
						AddRedeliverable(_listUndelivered);
						_listUndelivered.Clear();
					}
					_isLastDisableRefused=false;
					_isCurrentTransferred=false;
					_rowCursor=0;
					_colCursor=0;
					count=(short)_listUndelivered.Count;
					State=count>0 ? 6 : 5;//With 0 left it waits in state 5 for MSG_DISABLEDS; it never starts another session.
					if(count==0 && _isContinuous && SpuriousXferReadyAfterEachImage) {
						_isSpuriousPending=true;
						_dsm.WakeHostNow();
					}
					break;
				case Tw.MSG_RESET:
					if(State<6) {
						return Fail(Tw.TWCC_SEQERROR);
					}
					CountKeptForRedelivery+=_listUndelivered.Count;
					AddRedeliverable(_listUndelivered);
					_listUndelivered.Clear();
					count=0;
					State=5;
					break;
				default:
					return Fail(Tw.TWCC_BADPROTOCOL);
			}
			TW_PENDINGXFERS pending=new TW_PENDINGXFERS { Count=count,EOJ=0 };
			Marshal.StructureToPtr(pending,data,false);
			return Tw.TWRC_SUCCESS;
		}

		private ushort SetupMemXfer(IntPtr data) {
			if(State<4) {
				return Fail(Tw.TWCC_SEQERROR);
			}
			FakePage page=CurrentPage??(ListExposures.Count>0 ? ListExposures[0] : null);
			page=page==null ? null : Delivered(page);
			uint row=(uint)(page==null ? 1300 : page.Width*(page.BitsPerPixel/8)+MemoryRowPadding);
			uint whole=row*(uint)(page==null ? 1800 : page.Height);
			Marshal.StructureToPtr(new TW_SETUPMEMXFER { MinBufSize=row,MaxBufSize=whole,Preferred=Math.Min(whole,row*64) },data,false);
			return Tw.TWRC_SUCCESS;
		}

		///<summary>Section 6.2: with ICAP_BITDEPTH 16, the image's 16-bit original; an image without one is delivered as 8-bit.</summary>
		private FakePage Delivered(FakePage page) {
			return DictCaps.ContainsKey(IcapBitDepth) && DictCaps[IcapBitDepth]==16 && page.Original16!=null ? page.Original16 : page;
		}

		public string GetCaptureId(FakePage page) {
			string captureId;
			return _dictCaptureIds.TryGetValue(page,out captureId) ? captureId : (page.CaptureId??"fake-unknown");
		}

		///<summary>Section 6.3: the capture metadata of the image, or FakePage.MetadataJson.</summary>
		public string GetMetadataJson(FakePage page) {
			if(page.MetadataJson!=null) {
				return page.MetadataJson;
			}
			bool is16=Delivered(page).BitsPerPixel==16;
			int sequence;
			_dictSequences.TryGetValue(page,out sequence);
			return "{\"schema\":\"capture-metadata/1\",\"manufacturer\":\"Fake\",\"model\":null,\"serial\":\"FAKE-0001\","
				+"\"captureId\":\""+GetCaptureId(page)+"\",\"sequence\":"+sequence+",\"acquiredAt\":\"2026-10-31T10:00:00.000+01:00\","
				+"\"pixelSpacingMm\":[0.02,0.02],\"bitsStored\":"+(is16 ? 16 : 8)+",\"effectiveBits\":null,"
				+"\"byteOrder\":"+(is16 ? "\"LE\"" : "null")+",\"photometric\":\"MONOCHROME2\","
				+"\"window\":"+(is16 ? "{\"low\":100,\"high\":4000}" : "null")+",\"rendering\":\""+(is16 ? "original" : "vendor8")+"\","
				+"\"detectorType\":null,\"configuredExposure\":null,\"redelivered\":"+(_isRedeliverySession ? "true" : "false")+","
				+"\"dsVersion\":\"Fake 1.1 \u00e9\",\"vendor\":{}}";
		}

		///<summary>Section 6.3: answers in state 7 only, after TWRC_XFERDONE and before MSG_ENDXFER, with ICAP_EXTIMAGEINFO TRUE; otherwise
		///TWCC_SEQERROR. Items it does not have get TWRC_INFONOTSUPPORTED. Handles come from SourceAllocate (section 6.4).</summary>
		private ushort ExtImageInfo(IntPtr data) {
			if(!SupportsExtImageInfo) {
				return Fail(Tw.TWCC_BADPROTOCOL);
			}
			FakePage page=CurrentPage;
			if(State!=7 || !_isCurrentTransferred || page==null || DictCaps[IcapExtImageInfo]!=1) {
				return Fail(Tw.TWCC_SEQERROR);
			}
			CountExtImageInfo++;
			int numInfos=Marshal.ReadInt32(data,0);
			for(int i=0;i<numInfos;i++) {
				IntPtr pInfo=new IntPtr(data.ToInt64()+TwExtImageInfo.GetInfoOffset(i));
				TW_INFO info=(TW_INFO)Marshal.PtrToStructure(pInfo,typeof(TW_INFO));
				info.ReturnCode=Tw.TWRC_SUCCESS;
				info.NumItems=1;
				int sequence;
				_dictSequences.TryGetValue(page,out sequence);
				switch(info.InfoID) {
					case TweiDocumentNumber:
					case TweiPageNumber:
						info.ItemType=Tw.TWTY_UINT32;
						info.Item=new IntPtr(1);
						break;
					case TweiFrameNumber:
						info.ItemType=Tw.TWTY_UINT32;
						info.Item=new IntPtr(sequence);
						break;
					case TweiPixelFlavor:
						info.ItemType=Tw.TWTY_UINT16;
						info.Item=new IntPtr(Tw.TWPF_CHOCOLATE);
						break;
					case TweiCamera:
						info.ItemType=Tw.TWTY_STR255;
						info.Item=_dsm.SourceAllocate(256);
						break;
					case TweiFrame:
						info.ItemType=Tw.TWTY_FRAME;
						info.Item=_dsm.SourceAllocate(16);
						break;
					case TweiTwainDirectMetadata:
						byte[] bytes=Encoding.UTF8.GetBytes(GetMetadataJson(page)+"\0");
						IntPtr handle=_dsm.SourceAllocate(bytes.Length);
						IntPtr p=NativeMethods.GlobalLock(handle);
						Marshal.Copy(bytes,0,p,bytes.Length);
						NativeMethods.GlobalUnlock(handle);
						info.ItemType=Tw.TWTY_HANDLE;
						info.Item=handle;
						break;
					default:
						info.ReturnCode=Tw.TWRC_INFONOTSUPPORTED;
						info.ItemType=0;
						info.NumItems=0;
						info.Item=IntPtr.Zero;
						break;
				}
				Marshal.StructureToPtr(info,pInfo,false);
			}
			return Tw.TWRC_SUCCESS;
		}

		private ushort ImageInfo(IntPtr data) {
			FakePage page=CurrentPage;
			if(page==null) {
				return Fail(Tw.TWCC_SEQERROR);
			}
			page=Delivered(page);
			TW_IMAGEINFO imageInfo=new TW_IMAGEINFO();
			imageInfo.XResolution=TW_FIX32.FromDouble(page.Dpi);
			imageInfo.YResolution=TW_FIX32.FromDouble(page.Dpi);
			imageInfo.ImageWidth=page.Width;
			imageInfo.ImageLength=page.Height;
			imageInfo.SamplesPerPixel=1;
			imageInfo.BitsPerSample=new short[8];
			imageInfo.BitsPerSample[0]=(short)page.BitsPerPixel;
			imageInfo.BitsPerPixel=(short)page.BitsPerPixel;
			imageInfo.PixelType=(short)Tw.TWPT_GRAY;
			imageInfo.Compression=Tw.TWCP_NONE;
			Marshal.StructureToPtr(imageInfo,data,false);
			return Tw.TWRC_SUCCESS;
		}

		private ushort NativeXfer(IntPtr data) {
			FakePage page=CurrentPage;
			if(page==null || State!=6) {
				return Fail(Tw.TWCC_SEQERROR);
			}
			if(page.FailCondition!=0) {
				return Fail(page.FailCondition);//Generic-source behaviour (FakePage.FailCondition); the contract's read failure is FailReadAtImage.
			}
			IntPtr hDib=_dsm.SourceAllocate(page.NativeDib.Length);
			IntPtr pDib=NativeMethods.GlobalLock(hDib);
			Marshal.Copy(page.NativeDib,0,pDib,page.NativeDib.Length);
			NativeMethods.GlobalUnlock(hDib);
			Marshal.WriteIntPtr(data,hDib);
			State=7;
			_isCurrentTransferred=true;
			return Tw.TWRC_XFERDONE;
		}

		///<summary>Memory transfer: chunky, top-down, unpadded rows, as many whole rows per call as fit (sections 6.1, 6.2: 2 bytes per sample
		///at 16 bits, little-endian). MemoryRowPadding and MemoryColumnsPerStrip (not contract behaviour) pad the rows and cut them into tiles.</summary>
		private ushort MemXfer(IntPtr data) {
			FakePage page=CurrentPage;
			if(page==null) {
				return Fail(Tw.TWCC_SEQERROR);
			}
			if(page.FailCondition!=0) {
				return Fail(page.FailCondition);//Generic-source behaviour (FakePage.FailCondition); the contract's read failure is FailReadAtImage.
			}
			page=Delivered(page);
			TW_IMAGEMEMXFER xfer=(TW_IMAGEMEMXFER)Marshal.PtrToStructure(data,typeof(TW_IMAGEMEMXFER));
			int bytesPerPixel=page.BitsPerPixel/8;
			int columnsTile=MemoryColumnsPerStrip>0 ? Math.Min(MemoryColumnsPerStrip,page.Width) : page.Width;
			int columns=Math.Min(columnsTile,page.Width-_colCursor);
			int bytesTile=columns*bytesPerPixel;
			int bytesPerRow=bytesTile+MemoryRowPadding;
			if(_colCursor==0) {
				int rowsFitBand=(int)(xfer.Memory.Length/(uint)(columnsTile*bytesPerPixel+MemoryRowPadding));
				if(rowsFitBand<1) {
					return Fail(Tw.TWCC_BADVALUE);
				}
				_bandRows=Math.Min(rowsFitBand,page.Height-_rowCursor);
			}
			int rows=_bandRows;
			if(xfer.Memory.Length<(uint)(rows*bytesPerRow)) {
				return Fail(Tw.TWCC_BADVALUE);
			}
			byte[] strip=new byte[rows*bytesPerRow];
			for(int r=0;r<rows;r++) {
				Buffer.BlockCopy(page.TwainRows[_rowCursor+r],_colCursor*bytesPerPixel,strip,r*bytesPerRow,bytesTile);
				for(int b=bytesTile;b<bytesPerRow;b++) {
					strip[r*bytesPerRow+b]=0xEE;
				}
			}
			Marshal.Copy(strip,0,xfer.Memory.TheMem,strip.Length);
			xfer.Compression=Tw.TWCP_NONE;
			xfer.BytesPerRow=(uint)bytesPerRow;
			xfer.Columns=(uint)columns;
			xfer.Rows=(uint)rows;
			xfer.XOffset=(uint)_colCursor;
			xfer.YOffset=(uint)_rowCursor;
			xfer.BytesWritten=(uint)strip.Length;
			Marshal.StructureToPtr(xfer,data,false);
			_colCursor+=columns;
			if(_colCursor>=page.Width) {
				_colCursor=0;
				_rowCursor+=rows;
			}
			State=7;
			if(_rowCursor>=page.Height) {
				_isCurrentTransferred=true;
				return Tw.TWRC_XFERDONE;
			}
			return Tw.TWRC_SUCCESS;
		}
		#endregion Transfers
	}
}
