// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.TwainProtocol;

namespace ImagingDeviceManager.Tests {
	///<summary>One page or image that the simulated scanner delivers, in both TWAIN memory layout (top-down rows, RGB order)
	///and as the packed DIB a native transfer hands over.</summary>
	internal sealed class FakePage {
		public int Width;
		public int Height;
		public int PixelType;
		public int BitsPerPixel;
		public double Dpi;
		///<summary>Rows top to bottom, in TWAIN memory transfer layout.</summary>
		public byte[][] TwainRows;
		///<summary>Packed DIB for native transfers.</summary>
		public byte[] NativeDib;
		///<summary>Non-zero: the transfer of this page fails with this TWAIN condition code, and MSG_ENDXFER then moves on to the next page.
		///Generic-source behaviour: the contract 1.1 profile (FakeHelperSource) honours it too, but its contract says only that such an
		///image stays undelivered (section 10.3); the contract's own read failure, which ends the batch, is FakeHelperSource.FailReadAtImage.</summary>
		public ushort FailCondition;
		///<summary>The transfer of this page returns TWRC_CANCEL.</summary>
		public bool IsCancelled;
		///<summary>The contract 1.1 profile: the image's 16-bit original (CreateGray16), delivered instead of this 8-bit page when ICAP_BITDEPTH
		///is 16 (section 6.2). Null: the image has no 16-bit original and is delivered as 8-bit even then.</summary>
		public FakePage Original16;
		///<summary>The contract 1.1 profile: the TWEI_TWAINDIRECTMETADATA text of the image. Null: the profile writes capture-metadata/1 JSON.</summary>
		public string MetadataJson;
		///<summary>The contract 1.1 profile: the captureId of the image. Null: one is made up when the image is taken.</summary>
		public string CaptureId;

		///<summary>24 bit color page. colorAt(x,y) gives each pixel; y counts from the top.</summary>
		public static FakePage CreateRgb(int width,int height,double dpi,Func<int,int,Color> colorAt) {
			FakePage page=new FakePage { Width=width,Height=height,PixelType=Tw.TWPT_RGB,BitsPerPixel=24,Dpi=dpi };
			page.TwainRows=new byte[height][];
			int stride=DibImage.GetStride(width,24);
			byte[] pixels=new byte[stride*height];
			for(int y=0;y<height;y++) {
				page.TwainRows[y]=new byte[width*3];
				for(int x=0;x<width;x++) {
					Color color=colorAt(x,y);
					page.TwainRows[y][x*3]=color.R;
					page.TwainRows[y][x*3+1]=color.G;
					page.TwainRows[y][x*3+2]=color.B;
					int dibOffset=(height-1-y)*stride+x*3;
					pixels[dibOffset]=color.B;
					pixels[dibOffset+1]=color.G;
					pixels[dibOffset+2]=color.R;
				}
			}
			int ppm=DibImage.DpiToPelsPerMeter(dpi);
			page.NativeDib=DibImage.Create(width,height,24,null,pixels,ppm,ppm).Data;
			return page;
		}

		///<summary>16 bit grayscale page, as some sensors deliver. sampleAt(x,y) gives each sample.</summary>
		public static FakePage CreateGray16(int width,int height,double dpi,Func<int,int,int> sampleAt) {
			FakePage page=new FakePage { Width=width,Height=height,PixelType=Tw.TWPT_GRAY,BitsPerPixel=16,Dpi=dpi };
			page.TwainRows=new byte[height][];
			int stride=DibImage.GetStride(width,16);
			byte[] pixels=new byte[stride*height];
			for(int y=0;y<height;y++) {
				page.TwainRows[y]=new byte[width*2];
				for(int x=0;x<width;x++) {
					int sample=sampleAt(x,y);
					page.TwainRows[y][x*2]=(byte)sample;
					page.TwainRows[y][x*2+1]=(byte)(sample>>8);
					int dibOffset=(height-1-y)*stride+x*2;
					pixels[dibOffset]=(byte)sample;
					pixels[dibOffset+1]=(byte)(sample>>8);
				}
			}
			int ppm=DibImage.DpiToPelsPerMeter(dpi);
			page.NativeDib=DibImage.Create(width,height,16,null,pixels,ppm,ppm).Data;
			return page;
		}

		///<summary>8 bit grayscale page, as an intraoral sensor delivers it: a 256 entry gray palette in the native DIB (entry i is
		///RGB(i,i,i)) and unpadded rows in memory transfers. sampleAt(x,y) gives each pixel.</summary>
		public static FakePage CreateGray8(int width,int height,double dpi,Func<int,int,byte> sampleAt) {
			FakePage page=new FakePage { Width=width,Height=height,PixelType=Tw.TWPT_GRAY,BitsPerPixel=8,Dpi=dpi };
			page.TwainRows=new byte[height][];
			int stride=DibImage.GetStride(width,8);
			byte[] pixels=new byte[stride*height];
			for(int y=0;y<height;y++) {
				page.TwainRows[y]=new byte[width];
				for(int x=0;x<width;x++) {
					byte sample=sampleAt(x,y);
					page.TwainRows[y][x]=sample;
					pixels[(height-1-y)*stride+x]=sample;
				}
			}
			int ppm=DibImage.DpiToPelsPerMeter(dpi);
			page.NativeDib=DibImage.Create(width,height,8,DibImage.CreateGrayPalette(256,false),pixels,ppm,ppm).Data;
			return page;
		}

		///<summary>1 bit page. isSetAt(x,y) true means the bit is 1.</summary>
		public static FakePage CreateBw(int width,int height,double dpi,Func<int,int,bool> isSetAt) {
			FakePage page=new FakePage { Width=width,Height=height,PixelType=Tw.TWPT_BW,BitsPerPixel=1,Dpi=dpi };
			page.TwainRows=new byte[height][];
			int stride=DibImage.GetStride(width,1);
			byte[] pixels=new byte[stride*height];
			for(int y=0;y<height;y++) {
				page.TwainRows[y]=new byte[(width+7)/8];
				for(int x=0;x<width;x++) {
					if(!isSetAt(x,y)) {
						continue;
					}
					page.TwainRows[y][x/8]|=(byte)(0x80>>(x%8));
					pixels[(height-1-y)*stride+x/8]|=(byte)(0x80>>(x%8));
				}
			}
			int ppm=DibImage.DpiToPelsPerMeter(dpi);
			page.NativeDib=DibImage.Create(width,height,1,DibImage.CreateGrayPalette(2,false),pixels,ppm,ppm).Data;
			return page;
		}
	}

	///<summary>What the simulated source does after it is enabled.</summary>
	internal enum FakeEnableBehavior {
		///<summary>Posts MSG_XFERREADY, as when the user clicks Scan or the UI is hidden.</summary>
		XferReady,
		///<summary>Posts MSG_CLOSEDSREQ, as when the user closes the scanner dialog without scanning.</summary>
		CloseRequest,
		///<summary>Never says anything, like a sensor waiting for an exposure that never comes.</summary>
		Nothing,
	}

	///<summary>One triplet a simulated source received, for tests that check the order of calls.</summary>
	internal sealed class FakeCall {
		public uint Dg;
		public ushort Dat;
		public ushort Msg;
		///<summary>The capability, for DAT_CAPABILITY.</summary>
		public ushort Cap;
		///<summary>The value sent with a capability MSG_SET, or the notification a MSG_PROCESSEVENT returned.</summary>
		public int Value;
		///<summary>The source's TWAIN state before the call.</summary>
		public int StateBefore;
		public ushort Rc;
		///<summary>The condition code a DAT_STATUS returned, or the one a TWRC_FAILURE set.</summary>
		public ushort Condition;
		///<summary>For MSG_PROCESSEVENT: the Windows message (MSG.message) the application handed to the source.</summary>
		public int EventMessage;

		public string Name {
			get {
				switch(Dat) {
					case Tw.DAT_STATUS: return "STATUS";
					case Tw.DAT_STATUSUTF8: return "STATUSUTF8";
					case Tw.DAT_EVENT: return "PROCESSEVENT";
					case Tw.DAT_IMAGEINFO: return "IMAGEINFO";
					case Tw.DAT_IMAGENATIVEXFER: return "NATIVEXFER";
					case Tw.DAT_IMAGEMEMXFER: return "MEMXFER";
					case Tw.DAT_EXTIMAGEINFO: return "EXTIMAGEINFO";
					case Tw.DAT_SETUPMEMXFER: return "SETUPMEMXFER";
					case Tw.DAT_USERINTERFACE: return Msg==Tw.MSG_ENABLEDS ? "ENABLEDS" : (Msg==Tw.MSG_DISABLEDS ? "DISABLEDS" : "USERINTERFACE");
					case Tw.DAT_PENDINGXFERS: return Msg==Tw.MSG_ENDXFER ? "ENDXFER" : (Msg==Tw.MSG_RESET ? "RESET" : "PENDINGXFERS/GET");
					case Tw.DAT_CAPABILITY:
						switch(Msg) {
							case Tw.MSG_SET: return "CAP/SET";
							case Tw.MSG_GET: return "CAP/GET";
							case Tw.MSG_GETCURRENT: return "CAP/GETCURRENT";
							case Tw.MSG_GETDEFAULT: return "CAP/GETDEFAULT";
							case Tw.MSG_RESET: return "CAP/RESET";
							case Tw.MSG_QUERYSUPPORT: return "CAP/QUERYSUPPORT";
						}
						return "CAP/"+Msg;
				}
				return "DAT "+Dat+"/MSG "+Msg;
			}
		}

		public override string ToString() {
			return Name+(Cap!=0 ? " 0x"+Cap.ToString("X4") : "")+(Msg==Tw.MSG_SET || Dat==Tw.DAT_EVENT ? " "+Value : "")+(Dat==Tw.DAT_EVENT ? " msg 0x"+EventMessage.ToString("X4") : "")
				+" @"+StateBefore+" rc "+Rc+(Condition!=0 ? " cc 0x"+Condition.ToString("X4") : "");
		}
	}

	///<summary>A simulated TWAIN Data Source Manager with simulated data sources. It follows the TWAIN state machine closely
	///enough to exercise the session code: identities, capability containers, enabling, posting events through the Windows
	///message queue, native and memory transfers, pending transfer counts, and status codes.
	///
	///Every name in ListSourceNames that is not in DictHelperSources is the built-in source. It is lenient by default, as the
	///original tests expect; IsStrict makes it refuse MSG_DISABLEDS outside state 5, as TWAIN 2.4 allows (the "strict generic"
	///profile). Names in DictHelperSources are FakeHelperSource objects, which follow twain-contract 1.1 (the "single-shot helper
	///DS" profile).</summary>
	internal sealed class FakeTwainDsm:ITwainDsm {
		private const int WakeTimerId=0x5EED;
		public List<string> ListSourceNames=new List<string> { "Fake Scanner","Fake Sensor" };
		public string DefaultSource="Fake Scanner";
		public List<FakePage> ListPages=new List<FakePage>();
		public ushort EnableResult=Tw.TWRC_SUCCESS;
		public ushort EnableFailCondition=Tw.TWCC_OPERATIONERROR;
		public FakeEnableBehavior EnableBehavior=FakeEnableBehavior.XferReady;
		public bool IsPendingCountUnknown;
		public bool IsImageLengthUnknown;
		public ushort? PixelFlavor;
		///<summary>CAP_UICONTROLLABLE of the built-in source. Null: not supported.</summary>
		public bool? UiControllable;
		///<summary>0: the built-in source answers CAP_UICONTROLLABLE to MSG_GET and MSG_GETCURRENT. MSG_GET: only to MSG_GET, the one
		///operation TWAIN requires for it (as a 1.6 to 1.9 source may). MSG_GETCURRENT: only to MSG_GETCURRENT. Any other message fails
		///with TWCC_CAPBADOPERATION.</summary>
		public ushort UiControllableOnlyMsg;
		public HashSet<ushort> HashSetRejectedCaps=new HashSet<ushort>();
		public ushort UserSelectResult=Tw.TWRC_SUCCESS;
		public string UserSelectChoice="Fake Sensor";
		///<summary>Size of the memory transfer buffer the source asks for.</summary>
		public uint MemPreferred=0;
		///<summary>Maximum rows per memory transfer strip.</summary>
		public int MaxRowsPerStrip=7;
		///<summary>Runs inside MSG_ENABLEDS, before the source posts its event.</summary>
		public Action ActionOnEnable;
		///<summary>Runs at the start of each memory transfer strip of the built-in source, with the first row of the strip. A test can
		///throw from it to simulate a failure in the middle of a transfer (state 7).</summary>
		public Action<int> ActionOnMemoryStrip;
		///<summary>Strict generic profile for the built-in source: MSG_DISABLEDS fails with TWCC_SEQERROR outside state 5 (TWAIN 2.4
		///p.7-103: valid in state 5 only). The lenient default accepts it whenever the source is enabled.</summary>
		public bool IsStrict;
		///<summary>Condition codes the next MSG_DISABLEDS calls of the built-in source fail with, one per call.</summary>
		public Queue<ushort> QueueDisableFailures=new Queue<ushort>();
		///<summary>The built-in source refuses MSG_ENDXFER with TWCC_SEQERROR after the page with this index (-1: never).</summary>
		public int FailEndXferOnPage=-1;
		///<summary>Acts as a version 2 DSM: sets DF_DSM2 in the application's identity at MSG_OPENDSM and hands out DsmMemory through
		///DAT_ENTRYPOINT. The simulated sources then allocate every handle they give the application with DSM_MemAllocate, as
		///TWAIN 2 sources do; otherwise with GlobalAlloc.</summary>
		public bool SupplyEntryPoints;
		public FakeDsmMemory DsmMemory=new FakeDsmMemory();
		///<summary>Sources that follow twain-contract 1.1, by product name. Add them with AddHelperSource.</summary>
		public Dictionary<string,FakeHelperSource> DictHelperSources=new Dictionary<string,FakeHelperSource>();

		public Dictionary<ushort,int> DictCapsSet=new Dictionary<ushort,int>();
		public List<string> ListLog=new List<string>();
		///<summary>Every triplet sent to an open source, in order.</summary>
		public List<FakeCall> ListCalls=new List<FakeCall>();
		///<summary>The source's TWAIN state at each MSG_DISABLEDS, whether accepted or not.</summary>
		public List<int> ListDisableStates=new List<int>();
		///<summary>Every handle a simulated source handed to the application (native DIBs, capability containers, status text).</summary>
		public List<IntPtr> ListSourceHandles=new List<IntPtr>();
		public int CountEntryPointGet;
		public bool IsDsmOpen;
		public bool IsSourceOpen;
		public bool IsEnabled;
		public string OpenSourceName="";
		public int CountEnable;
		public int CountDispose;
		public ushort LastShowUI;
		public IntPtr LastParent;
		public IntPtr HwndDsm;
		///<summary>TWAIN state of the built-in source: 3 closed, 4 open, 5 enabled, 6 transfer ready, 7 transferring.</summary>
		public int SourceState=3;
		///<summary>The window the application gave MSG_ENABLEDS. Sources post to it to wake the application's message loop.</summary>
		public IntPtr HwndParent;

		private readonly Stopwatch _stopwatch=Stopwatch.StartNew();
		private FakeHelperSource _helperOpen;
		private int _idxEnum;
		private int _idxPage;
		private int _rowCursor;
		private ushort _conditionCode;
		private ushort _pendingMessage;

		public string LibraryName => "FakeTwainDsm";

		public FakePage CurrentPage => _idxPage<ListPages.Count ? ListPages[_idxPage] : null;

		///<summary>Milliseconds since this DSM was created. Simulated sources use it for their timed behaviour.</summary>
		public long NowMs => _stopwatch.ElapsedMilliseconds;

		///<summary>The TWAIN state of the open source as it sees it, 3 when none is open.</summary>
		public int OpenSourceState => _helperOpen!=null ? _helperOpen.State : SourceState;

		public void AddHelperSource(FakeHelperSource helper) {
			DictHelperSources[helper.ProductName]=helper;
			if(!ListSourceNames.Contains(helper.ProductName)) {
				ListSourceNames.Add(helper.ProductName);
			}
		}

		///<summary>The calls whose Name is name, in order.</summary>
		public List<FakeCall> CallsNamed(string name) {
			return ListCalls.FindAll(x => x.Name==name);
		}

		///<summary>Wakes the application's message loop at once, as a source does when it has a notification.</summary>
		public void WakeHostNow() {
			if(HwndParent!=IntPtr.Zero) {
				NativeMethods.PostMessage(HwndParent,NativeMethods.WM_NULL,IntPtr.Zero,IntPtr.Zero);
			}
		}

		///<summary>Wakes the application's message loop every delayMs until StopWakeTimer, so that timed behaviour happens even when
		///no other message arrives. The loop hands the WM_TIMER to MSG_PROCESSEVENT like any other message.</summary>
		public void WakeHostAfter(long delayMs) {
			if(HwndParent!=IntPtr.Zero) {
				NativeMethods.SetTimer(HwndParent,(UIntPtr)WakeTimerId,(uint)Math.Max(1,Math.Min(delayMs,int.MaxValue)),IntPtr.Zero);
			}
		}

		public void StopWakeTimer() {
			if(HwndParent!=IntPtr.Zero) {
				NativeMethods.KillTimer(HwndParent,(UIntPtr)WakeTimerId);
			}
		}

		///<summary>Allocates a handle that a simulated source hands to the application: with the DSM's memory functions when this DSM
		///hands them out, otherwise with GlobalAlloc (twain-contract 1.1 section 6.4). The application frees it the same way.</summary>
		public IntPtr SourceAllocate(int size) {
			IntPtr handle=SupplyEntryPoints
				? DsmMemory.Allocate((uint)size)
				: NativeMethods.GlobalAlloc(NativeMethods.GHND,(UIntPtr)size);
			ListSourceHandles.Add(handle);
			return handle;
		}

		public ushort Entry(IntPtr origin,IntPtr dest,uint dg,ushort dat,ushort msg,IntPtr data) {
			if(dg==Tw.DG_CONTROL && dat==Tw.DAT_ENTRYPOINT && dest==IntPtr.Zero) {
				if(!SupplyEntryPoints || msg!=Tw.MSG_GET || !IsDsmOpen) {
					return Fail(Tw.TWCC_BADPROTOCOL);
				}
				CountEntryPointGet++;
				DsmMemory.WriteEntryPoint(data);
				return Tw.TWRC_SUCCESS;
			}
			if(dest!=IntPtr.Zero && IsSourceOpen && !(dg==Tw.DG_CONTROL && dat==Tw.DAT_IDENTITY)) {
				return SourceEntry(dg,dat,msg,data);
			}
			if(dg==Tw.DG_CONTROL && dat==Tw.DAT_STATUS) {
				return Status(data);
			}
			if(dg==Tw.DG_CONTROL && dat==Tw.DAT_PARENT) {
				return Parent(origin,msg,data);
			}
			if(dg==Tw.DG_CONTROL && dat==Tw.DAT_IDENTITY) {
				return Identity(msg,data);
			}
			return Fail(Tw.TWCC_BADDEST);
		}

		///<summary>A triplet for the open source: recorded in ListCalls, then handled by the helper source or the built-in one.</summary>
		private ushort SourceEntry(uint dg,ushort dat,ushort msg,IntPtr data) {
			FakeCall call=new FakeCall { Dg=dg,Dat=dat,Msg=msg,StateBefore=OpenSourceState };
			if(dat==Tw.DAT_CAPABILITY) {
				TW_CAPABILITY capability=(TW_CAPABILITY)Marshal.PtrToStructure(data,typeof(TW_CAPABILITY));
				call.Cap=capability.Cap;
				if(msg==Tw.MSG_SET && capability.hContainer!=IntPtr.Zero) {
					IntPtr pContainer=NativeMethods.GlobalLock(capability.hContainer);
					call.Value=Marshal.ReadInt32(pContainer,TwContainer.OneValueItemOffset);
					NativeMethods.GlobalUnlock(capability.hContainer);
				}
			}
			if(dat==Tw.DAT_USERINTERFACE && msg==Tw.MSG_DISABLEDS) {
				ListDisableStates.Add(call.StateBefore);
			}
			ushort rc;
			if(_helperOpen!=null) {
				rc=_helperOpen.Entry(dg,dat,msg,data);
				if(rc==Tw.TWRC_FAILURE) {
					call.Condition=_helperOpen.LastFailureCode;
				}
			}
			else {
				rc=BuiltInSourceEntry(dg,dat,msg,data);
				if(rc==Tw.TWRC_FAILURE) {
					call.Condition=_conditionCode;
				}
			}
			call.Rc=rc;
			if(dat==Tw.DAT_STATUS && rc==Tw.TWRC_SUCCESS) {
				call.Condition=((TW_STATUS)Marshal.PtrToStructure(data,typeof(TW_STATUS))).ConditionCode;
			}
			if(dat==Tw.DAT_EVENT) {
				TW_EVENT twEvent=(TW_EVENT)Marshal.PtrToStructure(data,typeof(TW_EVENT));
				call.Value=twEvent.TWMessage;
				if(twEvent.pEvent!=IntPtr.Zero) {
					call.EventMessage=Marshal.ReadInt32(twEvent.pEvent,NativeMethods.MsgOffsetMessage);
				}
			}
			ListCalls.Add(call);
			return rc;
		}

		private ushort Status(IntPtr data) {
			Marshal.StructureToPtr(new TW_STATUS { ConditionCode=_conditionCode },data,false);
			_conditionCode=Tw.TWCC_SUCCESS;
			return Tw.TWRC_SUCCESS;
		}

		private ushort BuiltInSourceEntry(uint dg,ushort dat,ushort msg,IntPtr data) {
			if(dg==Tw.DG_CONTROL) {
				switch(dat) {
					case Tw.DAT_STATUS: return Status(data);
					case Tw.DAT_CAPABILITY: return Capability(msg,data);
					case Tw.DAT_USERINTERFACE: return UserInterface(msg,data);
					case Tw.DAT_EVENT: return ProcessEvent(data);
					case Tw.DAT_PENDINGXFERS: return PendingXfers(msg,data);
					case Tw.DAT_SETUPMEMXFER:
						Marshal.StructureToPtr(new TW_SETUPMEMXFER { MinBufSize=64,MaxBufSize=Tw.TWON_DONTCARE32,Preferred=MemPreferred==0 ? Tw.TWON_DONTCARE32 : MemPreferred },data,false);
						return Tw.TWRC_SUCCESS;
				}
			}
			if(dg==Tw.DG_IMAGE) {
				switch(dat) {
					case Tw.DAT_IMAGEINFO: return ImageInfo(data);
					case Tw.DAT_IMAGENATIVEXFER: return NativeXfer(data);
					case Tw.DAT_IMAGEMEMXFER: return MemXfer(data);
				}
			}
			return Fail(Tw.TWCC_BADPROTOCOL);
		}

		private ushort Fail(ushort conditionCode) {
			_conditionCode=conditionCode;
			return Tw.TWRC_FAILURE;
		}

		private ushort Parent(IntPtr origin,ushort msg,IntPtr data) {
			if(msg==Tw.MSG_OPENDSM) {
				HwndDsm=Marshal.ReadIntPtr(data);
				TW_IDENTITY appIdentity=(TW_IDENTITY)Marshal.PtrToStructure(origin,typeof(TW_IDENTITY));
				ListLog.Add("OPENDSM "+appIdentity.ProductName);
				appIdentity.Id=1;
				if(SupplyEntryPoints) {
					appIdentity.SupportedGroups|=Tw.DF_DSM2;
				}
				Marshal.StructureToPtr(appIdentity,origin,false);
				IsDsmOpen=true;
				return Tw.TWRC_SUCCESS;
			}
			if(msg==Tw.MSG_CLOSEDSM) {
				ListLog.Add("CLOSEDSM");
				IsDsmOpen=false;
				return Tw.TWRC_SUCCESS;
			}
			return Fail(Tw.TWCC_BADPROTOCOL);
		}

		private void WriteSourceIdentity(IntPtr data,string name) {
			TW_IDENTITY identity=new TW_IDENTITY();
			identity.Id=(uint)(100+ListSourceNames.IndexOf(name));
			identity.Version.Info="1";
			identity.ProtocolMajor=2;
			identity.ProtocolMinor=3;
			identity.SupportedGroups=Tw.DG_CONTROL|Tw.DG_IMAGE|Tw.DF_DS2;
			identity.Manufacturer="Fake Co";
			identity.ProductFamily="Fakes";
			identity.ProductName=name;
			Marshal.StructureToPtr(identity,data,false);
		}

		private ushort Identity(ushort msg,IntPtr data) {
			if(!IsDsmOpen) {
				return Fail(Tw.TWCC_SEQERROR);
			}
			switch(msg) {
				case Tw.MSG_GETFIRST:
					_idxEnum=0;
					goto case Tw.MSG_GETNEXT;
				case Tw.MSG_GETNEXT:
					if(_idxEnum>=ListSourceNames.Count) {
						return Tw.TWRC_ENDOFLIST;
					}
					WriteSourceIdentity(data,ListSourceNames[_idxEnum]);
					_idxEnum++;
					return Tw.TWRC_SUCCESS;
				case Tw.MSG_GETDEFAULT:
					if(DefaultSource==null) {
						return Fail(Tw.TWCC_NODS);
					}
					WriteSourceIdentity(data,DefaultSource);
					return Tw.TWRC_SUCCESS;
				case Tw.MSG_USERSELECT:
					ListLog.Add("USERSELECT");
					if(UserSelectResult==Tw.TWRC_SUCCESS) {
						WriteSourceIdentity(data,UserSelectChoice);
						DefaultSource=UserSelectChoice;
					}
					return UserSelectResult==Tw.TWRC_FAILURE ? Fail(Tw.TWCC_BUMMER) : UserSelectResult;
				case Tw.MSG_OPENDS:
					TW_IDENTITY identity=(TW_IDENTITY)Marshal.PtrToStructure(data,typeof(TW_IDENTITY));
					if(IsSourceOpen || !ListSourceNames.Contains(identity.ProductName)) {
						return Fail(Tw.TWCC_NODS);
					}
					IsSourceOpen=true;
					OpenSourceName=identity.ProductName;
					DictCapsSet.Clear();
					ListLog.Add("OPENDS "+identity.ProductName);
					if(DictHelperSources.TryGetValue(identity.ProductName,out _helperOpen)) {
						_helperOpen.OnOpen(this);
					}
					else {
						SourceState=4;
					}
					return Tw.TWRC_SUCCESS;
				case Tw.MSG_CLOSEDS:
					if(!IsSourceOpen) {
						return Fail(Tw.TWCC_SEQERROR);
					}
					ListLog.Add("CLOSEDS");
					IsSourceOpen=false;
					IsEnabled=false;
					if(_helperOpen!=null) {
						_helperOpen.OnClose();
						_helperOpen=null;
					}
					SourceState=3;
					StopWakeTimer();
					return Tw.TWRC_SUCCESS;
			}
			return Fail(Tw.TWCC_BADPROTOCOL);
		}

		private ushort Capability(ushort msg,IntPtr data) {
			TW_CAPABILITY capability=(TW_CAPABILITY)Marshal.PtrToStructure(data,typeof(TW_CAPABILITY));
			if(msg==Tw.MSG_SET) {
				if(IsEnabled) {
					return Fail(Tw.TWCC_SEQERROR);
				}
				if(HashSetRejectedCaps.Contains(capability.Cap)) {
					return Fail(Tw.TWCC_CAPUNSUPPORTED);
				}
				if(capability.ConType!=Tw.TWON_ONEVALUE) {
					return Fail(Tw.TWCC_BADVALUE);
				}
				IntPtr pContainer=NativeMethods.GlobalLock(capability.hContainer);
				int value=Marshal.ReadInt32(pContainer,TwContainer.OneValueItemOffset);
				NativeMethods.GlobalUnlock(capability.hContainer);
				DictCapsSet[capability.Cap]=value;
				return Tw.TWRC_SUCCESS;
			}
			int? valueCurrent=null;
			ushort itemType=Tw.TWTY_UINT16;
			bool isAnswered=msg==Tw.MSG_GETCURRENT;
			if(capability.Cap==Tw.ICAP_PIXELFLAVOR && PixelFlavor!=null) {
				valueCurrent=PixelFlavor.Value;
			}
			if(capability.Cap==Tw.CAP_UICONTROLLABLE && UiControllable!=null) {
				valueCurrent=UiControllable.Value ? 1 : 0;
				itemType=Tw.TWTY_BOOL;
				isAnswered=(msg==Tw.MSG_GET || msg==Tw.MSG_GETCURRENT) && (UiControllableOnlyMsg==0 || UiControllableOnlyMsg==msg);
				if(!isAnswered) {
					return Fail(Tw.TWCC_CAPBADOPERATION);
				}
			}
			if(isAnswered && valueCurrent!=null) {
				IntPtr hContainer=SourceAllocate(TwContainer.OneValueSize);
				IntPtr pContainer=NativeMethods.GlobalLock(hContainer);
				Marshal.WriteInt16(pContainer,0,(short)itemType);
				Marshal.WriteInt32(pContainer,TwContainer.OneValueItemOffset,valueCurrent.Value);
				NativeMethods.GlobalUnlock(hContainer);
				capability.ConType=Tw.TWON_ONEVALUE;
				capability.hContainer=hContainer;
				Marshal.StructureToPtr(capability,data,false);
				return Tw.TWRC_SUCCESS;
			}
			return Fail(Tw.TWCC_CAPUNSUPPORTED);
		}

		private ushort UserInterface(ushort msg,IntPtr data) {
			TW_USERINTERFACE userInterface=(TW_USERINTERFACE)Marshal.PtrToStructure(data,typeof(TW_USERINTERFACE));
			if(msg==Tw.MSG_ENABLEDS) {
				if(IsEnabled) {
					return Fail(Tw.TWCC_SEQERROR);
				}
				CountEnable++;
				LastShowUI=userInterface.ShowUI;
				LastParent=userInterface.hParent;
				HwndParent=userInterface.hParent;
				ListLog.Add("ENABLEDS");
				ActionOnEnable?.Invoke();
				if(EnableResult==Tw.TWRC_FAILURE) {
					return Fail(EnableFailCondition);
				}
				if(EnableResult!=Tw.TWRC_SUCCESS) {
					return EnableResult;
				}
				IsEnabled=true;
				SourceState=5;
				_idxPage=0;
				_rowCursor=0;
				if(EnableBehavior==FakeEnableBehavior.XferReady) {
					_pendingMessage=Tw.MSG_XFERREADY;
				}
				else if(EnableBehavior==FakeEnableBehavior.CloseRequest) {
					_pendingMessage=Tw.MSG_CLOSEDSREQ;
				}
				if(_pendingMessage!=Tw.MSG_NULL) {
					//A real source posts a message so that the application's message loop wakes up and calls PROCESSEVENT.
					NativeMethods.PostMessage(userInterface.hParent,NativeMethods.WM_NULL,IntPtr.Zero,IntPtr.Zero);
				}
				return Tw.TWRC_SUCCESS;
			}
			if(msg==Tw.MSG_DISABLEDS) {
				if(!IsEnabled) {
					return Fail(Tw.TWCC_SEQERROR);
				}
				if(QueueDisableFailures.Count>0) {
					ListLog.Add("DISABLEDS refused");
					return Fail(QueueDisableFailures.Dequeue());
				}
				if(IsStrict && SourceState!=5) {
					ListLog.Add("DISABLEDS refused");
					return Fail(Tw.TWCC_SEQERROR);
				}
				ListLog.Add("DISABLEDS");
				IsEnabled=false;
				SourceState=4;
				_pendingMessage=Tw.MSG_NULL;
				return Tw.TWRC_SUCCESS;
			}
			return Fail(Tw.TWCC_BADPROTOCOL);
		}

		private ushort ProcessEvent(IntPtr data) {
			if(!IsEnabled) {
				return Fail(Tw.TWCC_SEQERROR);
			}
			TW_EVENT twEvent=(TW_EVENT)Marshal.PtrToStructure(data,typeof(TW_EVENT));
			if(_pendingMessage!=Tw.MSG_NULL) {
				twEvent.TWMessage=_pendingMessage;
				if(_pendingMessage==Tw.MSG_XFERREADY) {
					SourceState=6;
				}
				_pendingMessage=Tw.MSG_NULL;
				Marshal.StructureToPtr(twEvent,data,false);
				return Tw.TWRC_DSEVENT;
			}
			twEvent.TWMessage=Tw.MSG_NULL;
			Marshal.StructureToPtr(twEvent,data,false);
			return Tw.TWRC_NOTDSEVENT;
		}

		private ushort ImageInfo(IntPtr data) {
			FakePage page=CurrentPage;
			if(page==null) {
				return Fail(Tw.TWCC_SEQERROR);
			}
			TW_IMAGEINFO imageInfo=new TW_IMAGEINFO();
			imageInfo.XResolution=TW_FIX32.FromDouble(page.Dpi);
			imageInfo.YResolution=TW_FIX32.FromDouble(page.Dpi);
			imageInfo.ImageWidth=page.Width;
			imageInfo.ImageLength=IsImageLengthUnknown ? -1 : page.Height;
			imageInfo.SamplesPerPixel=(short)(page.PixelType==Tw.TWPT_RGB ? 3 : 1);
			imageInfo.BitsPerSample=new short[8];
			imageInfo.BitsPerSample[0]=(short)(page.BitsPerPixel/imageInfo.SamplesPerPixel);
			imageInfo.BitsPerPixel=(short)page.BitsPerPixel;
			imageInfo.PixelType=(short)page.PixelType;
			imageInfo.Compression=Tw.TWCP_NONE;
			Marshal.StructureToPtr(imageInfo,data,false);
			return Tw.TWRC_SUCCESS;
		}

		private ushort NativeXfer(IntPtr data) {
			FakePage page=CurrentPage;
			if(page==null) {
				return Fail(Tw.TWCC_SEQERROR);
			}
			if(page.FailCondition!=0) {
				return Fail(page.FailCondition);
			}
			if(page.IsCancelled) {
				SourceState=7;
				return Tw.TWRC_CANCEL;
			}
			IntPtr hDib=SourceAllocate(page.NativeDib.Length);
			IntPtr pDib=NativeMethods.GlobalLock(hDib);
			Marshal.Copy(page.NativeDib,0,pDib,page.NativeDib.Length);
			NativeMethods.GlobalUnlock(hDib);
			Marshal.WriteIntPtr(data,hDib);
			ListLog.Add("NATIVEXFER");
			SourceState=7;
			return Tw.TWRC_XFERDONE;
		}

		private ushort MemXfer(IntPtr data) {
			FakePage page=CurrentPage;
			if(page==null) {
				return Fail(Tw.TWCC_SEQERROR);
			}
			if(page.FailCondition!=0) {
				return Fail(page.FailCondition);
			}
			if(page.IsCancelled) {
				SourceState=7;
				return Tw.TWRC_CANCEL;
			}
			ActionOnMemoryStrip?.Invoke(_rowCursor);
			TW_IMAGEMEMXFER xfer=(TW_IMAGEMEMXFER)Marshal.PtrToStructure(data,typeof(TW_IMAGEMEMXFER));
			int rowBytes=page.TwainRows[0].Length;
			int bytesPerRow=(rowBytes+3)&~3;//Padded rows, to check that BytesPerRow is honored.
			int rowsFit=(int)(xfer.Memory.Length/(uint)bytesPerRow);
			int rows=Math.Min(Math.Min(rowsFit,MaxRowsPerStrip),page.Height-_rowCursor);
			if(rows<=0) {
				return Fail(Tw.TWCC_LOWMEMORY);
			}
			byte[] strip=new byte[rows*bytesPerRow];
			for(int r=0;r<rows;r++) {
				Buffer.BlockCopy(page.TwainRows[_rowCursor+r],0,strip,r*bytesPerRow,rowBytes);
			}
			Marshal.Copy(strip,0,xfer.Memory.TheMem,strip.Length);
			xfer.Compression=Tw.TWCP_NONE;
			xfer.BytesPerRow=(uint)bytesPerRow;
			xfer.Columns=(uint)page.Width;
			xfer.Rows=(uint)rows;
			xfer.XOffset=0;
			xfer.YOffset=(uint)_rowCursor;
			xfer.BytesWritten=(uint)strip.Length;
			Marshal.StructureToPtr(xfer,data,false);
			_rowCursor+=rows;
			ListLog.Add("MEMXFER");
			SourceState=7;
			return _rowCursor>=page.Height ? Tw.TWRC_XFERDONE : Tw.TWRC_SUCCESS;
		}

		private ushort PendingXfers(ushort msg,IntPtr data) {
			if(msg==Tw.MSG_ENDXFER) {
				if(_idxPage==FailEndXferOnPage) {
					ListLog.Add("ENDXFER refused");
					return Fail(Tw.TWCC_SEQERROR);
				}
				ListLog.Add("ENDXFER");
				_idxPage++;
				_rowCursor=0;
				int remaining=Math.Max(0,ListPages.Count-_idxPage);
				short count=(short)(remaining>0 && IsPendingCountUnknown ? -1 : remaining);
				SourceState=remaining>0 ? 6 : 5;
				Marshal.StructureToPtr(new TW_PENDINGXFERS { Count=count },data,false);
				return Tw.TWRC_SUCCESS;
			}
			if(msg==Tw.MSG_RESET) {
				ListLog.Add("RESET");
				_idxPage=ListPages.Count;
				SourceState=5;
				Marshal.StructureToPtr(new TW_PENDINGXFERS { Count=0 },data,false);
				return Tw.TWRC_SUCCESS;
			}
			if(msg==Tw.MSG_GET) {
				int remaining=SourceState>=6 ? Math.Max(0,ListPages.Count-_idxPage) : 0;
				Marshal.StructureToPtr(new TW_PENDINGXFERS { Count=(short)remaining },data,false);
				return Tw.TWRC_SUCCESS;
			}
			return Fail(Tw.TWCC_BADPROTOCOL);
		}

		public void Dispose() {
			CountDispose++;
		}
	}
}
