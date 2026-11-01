// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	[TestClass]
	public class TwainProtocolTests {
		[TestMethod]
		public void Process_Is32Bit() {
			//The TWAIN structures below are laid out for 32-bit Windows, which is what the program runs as.
			Assert.AreEqual(4,IntPtr.Size);
		}

		[TestMethod]
		public void Structs_HaveTwainSizesFor32BitWindows() {
			Assert.AreEqual(4,Marshal.SizeOf(typeof(TW_FIX32)));
			Assert.AreEqual(42,Marshal.SizeOf(typeof(TW_VERSION)));
			Assert.AreEqual(156,Marshal.SizeOf(typeof(TW_IDENTITY)));
			Assert.AreEqual(8,Marshal.SizeOf(typeof(TW_USERINTERFACE)));
			Assert.AreEqual(6,Marshal.SizeOf(typeof(TW_EVENT)));
			Assert.AreEqual(6,Marshal.SizeOf(typeof(TW_PENDINGXFERS)));
			Assert.AreEqual(4,Marshal.SizeOf(typeof(TW_STATUS)));
			Assert.AreEqual(12,Marshal.SizeOf(typeof(TW_STATUSUTF8)));
			Assert.AreEqual(8,Marshal.SizeOf(typeof(TW_CAPABILITY)));
			Assert.AreEqual(42,Marshal.SizeOf(typeof(TW_IMAGEINFO)));
			Assert.AreEqual(12,Marshal.SizeOf(typeof(TW_SETUPMEMXFER)));
			Assert.AreEqual(12,Marshal.SizeOf(typeof(TW_MEMORY)));
			Assert.AreEqual(38,Marshal.SizeOf(typeof(TW_IMAGEMEMXFER)));
			Assert.AreEqual(1028,Marshal.SizeOf(typeof(TW_PALETTE8)));
			Assert.AreEqual(24,Marshal.SizeOf(typeof(TW_ENTRYPOINT)));
		}

		[TestMethod]
		public void Structs_HaveTwainFieldOffsets() {
			Assert.AreEqual(4,(int)Marshal.OffsetOf(typeof(TW_IDENTITY),"Version"));
			Assert.AreEqual(46,(int)Marshal.OffsetOf(typeof(TW_IDENTITY),"ProtocolMajor"));
			Assert.AreEqual(50,(int)Marshal.OffsetOf(typeof(TW_IDENTITY),"SupportedGroups"));
			Assert.AreEqual(54,(int)Marshal.OffsetOf(typeof(TW_IDENTITY),"Manufacturer"));
			Assert.AreEqual(88,(int)Marshal.OffsetOf(typeof(TW_IDENTITY),"ProductFamily"));
			Assert.AreEqual(122,(int)Marshal.OffsetOf(typeof(TW_IDENTITY),"ProductName"));
			Assert.AreEqual(4,(int)Marshal.OffsetOf(typeof(TW_USERINTERFACE),"hParent"));
			Assert.AreEqual(4,(int)Marshal.OffsetOf(typeof(TW_EVENT),"TWMessage"));
			Assert.AreEqual(2,(int)Marshal.OffsetOf(typeof(TW_PENDINGXFERS),"EOJ"));
			Assert.AreEqual(4,(int)Marshal.OffsetOf(typeof(TW_CAPABILITY),"hContainer"));
			Assert.AreEqual(4,(int)Marshal.OffsetOf(typeof(TW_STATUSUTF8),"Size"));
			Assert.AreEqual(8,(int)Marshal.OffsetOf(typeof(TW_STATUSUTF8),"UTF8string"));
			Assert.AreEqual(18,(int)Marshal.OffsetOf(typeof(TW_IMAGEINFO),"BitsPerSample"));
			Assert.AreEqual(34,(int)Marshal.OffsetOf(typeof(TW_IMAGEINFO),"BitsPerPixel"));
			Assert.AreEqual(38,(int)Marshal.OffsetOf(typeof(TW_IMAGEINFO),"PixelType"));
			Assert.AreEqual(2,(int)Marshal.OffsetOf(typeof(TW_IMAGEMEMXFER),"BytesPerRow"));
			Assert.AreEqual(22,(int)Marshal.OffsetOf(typeof(TW_IMAGEMEMXFER),"BytesWritten"));
			Assert.AreEqual(26,(int)Marshal.OffsetOf(typeof(TW_IMAGEMEMXFER),"Memory"));
		}

		[TestMethod]
		public void Identity_ProductNameRoundTripsThroughUnmanagedMemory() {
			IntPtr p=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TW_IDENTITY)));
			try {
				TW_IDENTITY identity=new TW_IDENTITY { Id=7,ProductName="Scanner 3000",Manufacturer="Maker" };
				identity.Version.Info="v";
				Marshal.StructureToPtr(identity,p,false);
				Assert.AreEqual(7,Marshal.ReadInt32(p,0));
				Assert.AreEqual((byte)'S',Marshal.ReadByte(p,122));
				TW_IDENTITY copy=(TW_IDENTITY)Marshal.PtrToStructure(p,typeof(TW_IDENTITY));
				Assert.AreEqual("Scanner 3000",copy.ProductName);
				Assert.AreEqual("Maker",copy.Manufacturer);
			}
			finally {
				Marshal.FreeHGlobal(p);
			}
		}

		[TestMethod]
		public void Fix32_ConvertsBothWays() {
			TW_FIX32 fix=TW_FIX32.FromDouble(300);
			Assert.AreEqual(300,fix.Whole);
			Assert.AreEqual(0,fix.Frac);
			Assert.AreEqual(300.0,fix.ToDouble());
			fix=TW_FIX32.FromDouble(72.5);
			Assert.AreEqual(72,fix.Whole);
			Assert.AreEqual(32768,fix.Frac);
			Assert.AreEqual(72.5,fix.ToDouble());
			fix=TW_FIX32.FromDouble(-1.5);
			Assert.AreEqual(-1.5,fix.ToDouble());
			//In a TW_ONEVALUE, Whole is the low word of the Item field and Frac the high word.
			uint bits=TW_FIX32.FromDouble(150.25).ToUInt32();
			Assert.AreEqual(150u,bits&0xffff);
			Assert.AreEqual(16384u,bits>>16);
			Assert.AreEqual(150.25,TW_FIX32.FromUInt32(bits).ToDouble());
		}

		[TestMethod]
		public void ErrorCodes_KeepTheNumbersCallersCompareAgainst() {
			Assert.AreEqual(0,(int)EZTwainErrorCode.EZTEC_NONE);
			Assert.AreEqual(5,(int)EZTwainErrorCode.EZTEC_DS_FAILURE);
			Assert.AreEqual(19,(int)EZTwainErrorCode.EZTEC_USER_CANCEL);
			Assert.AreEqual(22,(int)EZTwainErrorCode.EZTEC_JPEG_DLL);
			Assert.AreEqual(38,(int)EZTwainErrorCode.EZTEC_0_PAGES);
			Assert.AreEqual(43,(int)EZTwainErrorCode.EZTEC_NO_PDF);
			Assert.AreEqual(76,(int)EZTwainErrorCode.EZTEC_DEVICE_PAPERJAM);
			Assert.AreEqual(2,EZTwain.XFERMECH_MEMORY);
		}

		[TestMethod]
		public void FindSource_PrefersExactMatchThenPrefix() {
			List<TW_IDENTITY> listIdentities=new List<TW_IDENTITY> {
				new TW_IDENTITY { ProductName="Sensor Pro 2" },
				new TW_IDENTITY { ProductName="Sensor" },
				new TW_IDENTITY { ProductName="Flatbed X" },
			};
			Assert.AreEqual("Sensor",TwainSession.FindSource(listIdentities,"sensor").Value.ProductName);
			Assert.AreEqual("Flatbed X",TwainSession.FindSource(listIdentities,"Flatbed").Value.ProductName);
			Assert.AreEqual("Sensor Pro 2",TwainSession.FindSource(listIdentities," Sensor Pro 2 ").Value.ProductName);
			Assert.IsNull(TwainSession.FindSource(listIdentities,"Camera"));
			Assert.IsNull(TwainSession.FindSource(listIdentities," "));
		}

		[TestMethod]
		public void ChooseBufferSize_RespectsSourceLimits() {
			Assert.AreEqual(65536,TwainSession.ChooseBufferSize(Tw.TWON_DONTCARE32,Tw.TWON_DONTCARE32,Tw.TWON_DONTCARE32,65536,1<<24));
			Assert.AreEqual(32000,TwainSession.ChooseBufferSize(1000,Tw.TWON_DONTCARE32,32000,65536,1<<24));
			Assert.AreEqual(100000,TwainSession.ChooseBufferSize(100000,Tw.TWON_DONTCARE32,Tw.TWON_DONTCARE32,65536,1<<24));
			Assert.AreEqual(20000,TwainSession.ChooseBufferSize(1000,20000,Tw.TWON_DONTCARE32,65536,1<<24));
			Assert.AreEqual(1<<24,TwainSession.ChooseBufferSize(0,Tw.TWON_DONTCARE32,unchecked((uint)int.MaxValue),65536,1<<24));
			Assert.AreEqual(1024,TwainSession.ChooseBufferSize(0,Tw.TWON_DONTCARE32,10,65536,1<<24));
		}

		[TestMethod]
		public void ConditionText_DescribesPaperJam() {
			StringAssert.Contains(TwainSession.GetConditionText(Tw.TWCC_PAPERJAM),"paper jam");
			StringAssert.Contains(TwainSession.GetConditionText(999),"999");
		}

		[TestMethod]
		public void NativeDsm_PrefersTwainDsmOverLegacyTwain32() {
			List<string> listPaths=NativeTwainDsm.GetCandidatePaths();
			Assert.AreEqual(3,listPaths.Count);
			StringAssert.EndsWith(listPaths[0],"TWAINDSM.dll");
			StringAssert.EndsWith(listPaths[1],"TWAINDSM.dll");
			StringAssert.EndsWith(listPaths[2],"twain_32.dll");
			foreach(string path in listPaths) {
				Assert.IsTrue(System.IO.Path.IsPathRooted(path),"DSM paths must be absolute: "+path);
			}
		}

		[TestMethod]
		public void Twain_ActivateEZTwainIsANoOpThatSucceeds() {
			Assert.IsTrue(Twain.ActivateEZTwain());
		}
	}
}
