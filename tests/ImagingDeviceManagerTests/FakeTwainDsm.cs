// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
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
		///<summary>Non-zero: the transfer of this page fails with this TWAIN condition code.</summary>
		public ushort FailCondition;
		///<summary>The transfer of this page returns TWRC_CANCEL.</summary>
		public bool IsCancelled;

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

	///<summary>A simulated TWAIN Data Source Manager with simulated data sources. It follows the TWAIN state machine closely
	///enough to exercise the session code: identities, capability containers, enabling, posting events through the Windows
	///message queue, native and memory transfers, pending transfer counts, and status codes.</summary>
	internal sealed class FakeTwainDsm:ITwainDsm {
		public List<string> ListSourceNames=new List<string> { "Fake Scanner","Fake Sensor" };
		public string DefaultSource="Fake Scanner";
		public List<FakePage> ListPages=new List<FakePage>();
		public ushort EnableResult=Tw.TWRC_SUCCESS;
		public ushort EnableFailCondition=Tw.TWCC_OPERATIONERROR;
		public FakeEnableBehavior EnableBehavior=FakeEnableBehavior.XferReady;
		public bool IsPendingCountUnknown;
		public bool IsImageLengthUnknown;
		public ushort? PixelFlavor;
		public HashSet<ushort> HashSetRejectedCaps=new HashSet<ushort>();
		public ushort UserSelectResult=Tw.TWRC_SUCCESS;
		public string UserSelectChoice="Fake Sensor";
		///<summary>Size of the memory transfer buffer the source asks for.</summary>
		public uint MemPreferred=0;
		///<summary>Maximum rows per memory transfer strip.</summary>
		public int MaxRowsPerStrip=7;
		///<summary>Runs inside MSG_ENABLEDS, before the source posts its event.</summary>
		public Action ActionOnEnable;

		public Dictionary<ushort,int> DictCapsSet=new Dictionary<ushort,int>();
		public List<string> ListLog=new List<string>();
		public bool IsDsmOpen;
		public bool IsSourceOpen;
		public bool IsEnabled;
		public string OpenSourceName="";
		public int CountEnable;
		public int CountDispose;
		public ushort LastShowUI;
		public IntPtr LastParent;
		public IntPtr HwndDsm;

		private int _idxEnum;
		private int _idxPage;
		private int _rowCursor;
		private ushort _conditionCode;
		private ushort _pendingMessage;

		public string LibraryName => "FakeTwainDsm";

		public FakePage CurrentPage => _idxPage<ListPages.Count ? ListPages[_idxPage] : null;

		public ushort Entry(IntPtr origin,IntPtr dest,uint dg,ushort dat,ushort msg,IntPtr data) {
			if(dg==Tw.DG_CONTROL && dat==Tw.DAT_STATUS) {
				Marshal.StructureToPtr(new TW_STATUS { ConditionCode=_conditionCode },data,false);
				_conditionCode=Tw.TWCC_SUCCESS;
				return Tw.TWRC_SUCCESS;
			}
			if(dg==Tw.DG_CONTROL && dat==Tw.DAT_PARENT) {
				return Parent(origin,msg,data);
			}
			if(dg==Tw.DG_CONTROL && dat==Tw.DAT_IDENTITY) {
				return Identity(msg,data);
			}
			if(!IsSourceOpen || dest==IntPtr.Zero) {
				return Fail(Tw.TWCC_BADDEST);
			}
			if(dg==Tw.DG_CONTROL) {
				switch(dat) {
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
					return Tw.TWRC_SUCCESS;
				case Tw.MSG_CLOSEDS:
					if(!IsSourceOpen) {
						return Fail(Tw.TWCC_SEQERROR);
					}
					ListLog.Add("CLOSEDS");
					IsSourceOpen=false;
					IsEnabled=false;
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
			if(msg==Tw.MSG_GETCURRENT && capability.Cap==Tw.ICAP_PIXELFLAVOR && PixelFlavor!=null) {
				IntPtr hContainer=NativeMethods.GlobalAlloc(NativeMethods.GHND,(UIntPtr)TwContainer.OneValueSize);
				IntPtr pContainer=NativeMethods.GlobalLock(hContainer);
				Marshal.WriteInt16(pContainer,0,(short)Tw.TWTY_UINT16);
				Marshal.WriteInt32(pContainer,TwContainer.OneValueItemOffset,PixelFlavor.Value);
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
				ListLog.Add("ENABLEDS");
				ActionOnEnable?.Invoke();
				if(EnableResult==Tw.TWRC_FAILURE) {
					return Fail(EnableFailCondition);
				}
				if(EnableResult!=Tw.TWRC_SUCCESS) {
					return EnableResult;
				}
				IsEnabled=true;
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
				ListLog.Add("DISABLEDS");
				IsEnabled=false;
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
				return Tw.TWRC_CANCEL;
			}
			IntPtr hDib=NativeMethods.GlobalAlloc(NativeMethods.GHND,(UIntPtr)page.NativeDib.Length);
			IntPtr pDib=NativeMethods.GlobalLock(hDib);
			Marshal.Copy(page.NativeDib,0,pDib,page.NativeDib.Length);
			NativeMethods.GlobalUnlock(hDib);
			Marshal.WriteIntPtr(data,hDib);
			ListLog.Add("NATIVEXFER");
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
				return Tw.TWRC_CANCEL;
			}
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
			return _rowCursor>=page.Height ? Tw.TWRC_XFERDONE : Tw.TWRC_SUCCESS;
		}

		private ushort PendingXfers(ushort msg,IntPtr data) {
			if(msg==Tw.MSG_ENDXFER) {
				ListLog.Add("ENDXFER");
				_idxPage++;
				_rowCursor=0;
				int remaining=Math.Max(0,ListPages.Count-_idxPage);
				short count=(short)(remaining>0 && IsPendingCountUnknown ? -1 : remaining);
				Marshal.StructureToPtr(new TW_PENDINGXFERS { Count=count },data,false);
				return Tw.TWRC_SUCCESS;
			}
			if(msg==Tw.MSG_RESET) {
				ListLog.Add("RESET");
				_idxPage=ListPages.Count;
				Marshal.StructureToPtr(new TW_PENDINGXFERS { Count=0 },data,false);
				return Tw.TWRC_SUCCESS;
			}
			return Fail(Tw.TWCC_BADPROTOCOL);
		}

		public void OnDsmOpened(IntPtr appIdentity) {
		}

		public IntPtr MemAllocate(uint size) {
			return NativeMethods.GlobalAlloc(NativeMethods.GHND,(UIntPtr)size);
		}

		public void MemFree(IntPtr handle) {
			NativeMethods.GlobalFree(handle);
		}

		public IntPtr MemLock(IntPtr handle) {
			return NativeMethods.GlobalLock(handle);
		}

		public void MemUnlock(IntPtr handle) {
			NativeMethods.GlobalUnlock(handle);
		}

		public void Dispose() {
			CountDispose++;
		}
	}
}
