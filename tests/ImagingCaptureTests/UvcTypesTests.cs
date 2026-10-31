// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using ImagingCapture.Uvc;
using ImagingCapture.Uvc.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	[TestClass]
	public class UvcTypesTests {
		[TestMethod]
		public void SubtypeName_FourCCAndRgb() {
			Assert.AreEqual("MJPG",UvcMode.GetSubtypeName(Ds.MEDIASUBTYPE_MJPG));
			Assert.AreEqual("YUY2",UvcMode.GetSubtypeName(Ds.MEDIASUBTYPE_YUY2));
			Assert.AreEqual("RGB24",UvcMode.GetSubtypeName(Ds.MEDIASUBTYPE_RGB24));
			Assert.AreEqual("RGB32",UvcMode.GetSubtypeName(Ds.MEDIASUBTYPE_RGB32));
			Assert.AreEqual("NV12",UvcMode.GetSubtypeName(new Guid("3231564E-0000-0010-8000-00AA00389B71")));
			Guid guidOther=new Guid("12345678-1234-1234-1234-123456789ABC");
			Assert.AreEqual("{12345678-1234-1234-1234-123456789ABC}",UvcMode.GetSubtypeName(guidOther));
			//FourCC base with non-printable bytes is not a FourCC.
			Assert.AreEqual("{00000003-0000-0010-8000-00AA00389B71}",UvcMode.GetSubtypeName(new Guid("00000003-0000-0010-8000-00AA00389B71")));
		}

		[TestMethod]
		public void Mode_FramesPerSecondAreaAndText() {
			UvcMode mode=new UvcMode(4,1920,1080,Ds.MEDIASUBTYPE_MJPG,24,333333);
			Assert.AreEqual(30.0,mode.MaxFramesPerSecond,0.001);
			Assert.AreEqual(1920L*1080,mode.Area);
			Assert.AreEqual("1920x1080 MJPG 30 fps",mode.ToString());
			UvcMode modeUnknownRate=new UvcMode(0,640,480,Ds.MEDIASUBTYPE_YUY2,16,0);
			Assert.AreEqual(0,modeUnknownRate.MaxFramesPerSecond);
			Assert.AreEqual("640x480 YUY2",modeUnknownRate.ToString());
			Assert.IsTrue(mode.IsSameFormat(new UvcMode(9,1920,1080,Ds.MEDIASUBTYPE_MJPG,24,666666)));
			Assert.IsFalse(mode.IsSameFormat(new UvcMode(4,1920,1080,Ds.MEDIASUBTYPE_YUY2,16,333333)));
		}

		[TestMethod]
		public void DeviceInfo_SameDeviceByMonikerThenPath() {
			UvcDeviceInfo device=new UvcDeviceInfo("Camera",@"\\?\usb#a","@device:pnp:a");
			Assert.IsTrue(device.IsSameDevice(new UvcDeviceInfo("Other name","","@DEVICE:PNP:A")));
			Assert.IsTrue(device.IsSameDevice(new UvcDeviceInfo("Camera",@"\\?\USB#A","@device:pnp:moved")));
			Assert.IsFalse(device.IsSameDevice(new UvcDeviceInfo("Camera",@"\\?\usb#b","@device:pnp:b")),"Same name is not the same device.");
			Assert.IsFalse(new UvcDeviceInfo("Camera","","").IsSameDevice(new UvcDeviceInfo("Camera","","")));
			Assert.IsFalse(device.IsSameDevice(null));
		}

		[TestMethod]
		public void Controls_StandardPropertyNumbers() {
			Assert.AreEqual(7,UvcControls.GetPropertyCount(UvcControlInterface.CameraControl));
			Assert.AreEqual(10,UvcControls.GetPropertyCount(UvcControlInterface.VideoProcAmp));
			Assert.AreEqual("Pan",UvcControls.GetPropertyName(UvcControlInterface.CameraControl,0));
			Assert.AreEqual("Focus",UvcControls.GetPropertyName(UvcControlInterface.CameraControl,6));
			Assert.AreEqual("Brightness",UvcControls.GetPropertyName(UvcControlInterface.VideoProcAmp,0));
			Assert.AreEqual("Gain",UvcControls.GetPropertyName(UvcControlInterface.VideoProcAmp,9));
			Assert.IsFalse(UvcControls.IsValidProperty(UvcControlInterface.CameraControl,7));
			Assert.IsFalse(UvcControls.IsValidProperty(UvcControlInterface.VideoProcAmp,-1));
			Assert.IsTrue(UvcControls.IsValidProperty(UvcControlInterface.VideoProcAmp,9));
		}

		[TestMethod]
		public void Controls_StatusFromHResult() {
			Assert.AreEqual(UvcControlStatus.Ok,UvcControls.GetStatusFromHResult(0));
			Assert.AreEqual(UvcControlStatus.Unsupported,UvcControls.GetStatusFromHResult(Ds.E_PROP_ID_UNSUPPORTED));
			Assert.AreEqual(UvcControlStatus.Unsupported,UvcControls.GetStatusFromHResult(Ds.E_PROP_SET_UNSUPPORTED));
			Assert.AreEqual(UvcControlStatus.Unsupported,UvcControls.GetStatusFromHResult(unchecked((int)0x80070492)));
			Assert.AreEqual(UvcControlStatus.Unsupported,UvcControls.GetStatusFromHResult(Ds.E_NOTIMPL));
			Assert.AreEqual(UvcControlStatus.NoInterface,UvcControls.GetStatusFromHResult(Ds.E_NOINTERFACE));
			Assert.AreEqual(UvcControlStatus.DeviceLost,UvcControls.GetStatusFromHResult(unchecked((int)0x8007048F)));
			Assert.AreEqual(UvcControlStatus.Failed,UvcControls.GetStatusFromHResult(Ds.E_FAIL));
		}

		[TestMethod]
		public void Errors_ClassifyStandardCodes() {
			Assert.AreEqual(UvcErrorKind.None,UvcErrors.Classify(0));
			Assert.AreEqual(UvcErrorKind.None,UvcErrors.Classify(1));
			Assert.AreEqual(UvcErrorKind.InUse,UvcErrors.Classify(unchecked((int)0x800705AA)));
			Assert.AreEqual(UvcErrorKind.InUse,UvcErrors.Classify(unchecked((int)0x80070020)));
			Assert.AreEqual(UvcErrorKind.InUse,UvcErrors.Classify(unchecked((int)0x800700AA)));
			Assert.AreEqual(UvcErrorKind.InUse,UvcErrors.Classify(unchecked((int)0xC00D3704)));
			Assert.AreEqual(UvcErrorKind.InUse,UvcErrors.Classify(unchecked((int)0xC00DABE5)));
			Assert.AreEqual(UvcErrorKind.AccessDenied,UvcErrors.Classify(unchecked((int)0x80070005)));
			Assert.AreEqual(UvcErrorKind.DeviceLost,UvcErrors.Classify(unchecked((int)0x8007048F)));
			Assert.AreEqual(UvcErrorKind.DeviceLost,UvcErrors.Classify(unchecked((int)0x800701B1)));
			Assert.AreEqual(UvcErrorKind.DeviceLost,UvcErrors.Classify(unchecked((int)0xC00DABE4)));
			Assert.AreEqual(UvcErrorKind.NotAvailable,UvcErrors.Classify(unchecked((int)0x80040154)));
			Assert.AreEqual(UvcErrorKind.Failed,UvcErrors.Classify(unchecked((int)0x80004005)));
			Assert.AreEqual("0x800705AA",UvcErrors.FormatHResult(unchecked((int)0x800705AA)));
			UvcException exception=new UvcException(UvcErrorKind.InUse,"busy",unchecked((int)0x800705AA));
			Assert.AreEqual(UvcErrorKind.InUse,exception.Kind);
			Assert.AreEqual(unchecked((int)0x800705AA),exception.HResult);
		}

		[TestMethod]
		public void Frame_ToBitmapCopiesPixels() {
			UvcFrame frame=new UvcFrame(3,2);
			Assert.AreEqual(12,frame.Stride);
			Assert.AreEqual(24,frame.Pixels.Length);
			for(int y=0;y<2;y++) {
				for(int x=0;x<3;x++) {
					int index=y*frame.Stride+x*4;
					frame.Pixels[index]=(byte)(10*x);//blue
					frame.Pixels[index+1]=(byte)(100+y);//green
					frame.Pixels[index+2]=(byte)(200+x+y);//red
					frame.Pixels[index+3]=0xFF;
				}
			}
			using(Bitmap bitmap=frame.ToBitmap()) {
				Assert.AreEqual(3,bitmap.Width);
				Assert.AreEqual(2,bitmap.Height);
				Color color=bitmap.GetPixel(2,1);
				Assert.AreEqual(20,color.B);
				Assert.AreEqual(101,color.G);
				Assert.AreEqual(203,color.R);
				frame.Pixels[0]=77;
				Assert.AreEqual(0,bitmap.GetPixel(0,0).B,"The bitmap holds a copy.");
			}
			UvcFrame frameCopy=frame.Clone();
			frame.Pixels[1]=55;
			Assert.AreEqual(77,frameCopy.Pixels[0]);
			Assert.AreEqual(100,frameCopy.Pixels[1],"The clone holds a copy.");
		}

		[TestMethod]
		public void Frame_RejectsEmptySize() {
			TestUtil.AssertThrows<ArgumentOutOfRangeException>(() => new UvcFrame(0,10));
			TestUtil.AssertThrows<ArgumentOutOfRangeException>(() => new UvcFrame(10,-1));
		}

		[TestMethod]
		public void FakeBackend_OpensListedDevicesAndScriptsControls() {
			FakeUvcBackend backend=new FakeUvcBackend();
			UvcDeviceInfo device=backend.AddDevice();
			backend.ListModes.Add(new UvcMode(0,640,480,Ds.MEDIASUBTYPE_YUY2,16,333333));
			Assert.AreEqual(1,backend.GetDevices().Count);
			UvcException exception=TestUtil.AssertThrows<UvcException>(() => backend.Open(new UvcDeviceInfo("Gone","","@device:fake:9")));
			Assert.AreEqual(UvcErrorKind.NoDevice,exception.Kind);
			using(IFrameSource frameSource=backend.Open(device)) {
				FakeFrameSource fake=backend.ListOpened[0];
				Assert.AreEqual(1,frameSource.Modes.Count);
				Assert.AreEqual(UvcControlStatus.Unsupported,frameSource.ReadControl(UvcControlInterface.CameraControl,6).Status);
				fake.SetControlScript(UvcControlInterface.VideoProcAmp,3,n => n<2 ? 0 : 4);
				Assert.AreEqual(0,frameSource.ReadControl(UvcControlInterface.VideoProcAmp,3).Value);
				Assert.AreEqual(0,frameSource.ReadControl(UvcControlInterface.VideoProcAmp,3).Value);
				Assert.AreEqual(4,frameSource.ReadControl(UvcControlInterface.VideoProcAmp,3).Value);
				Assert.AreEqual(3,fake.GetReadCount(UvcControlInterface.VideoProcAmp,3));
				frameSource.Start(frameSource.Modes[0],false);
				frameSource.Stop();
				frameSource.Start(frameSource.Modes[0],false);
				TestUtil.AssertThrows<InvalidOperationException>(() => frameSource.Start(null,false));
				int countFrameReady=0;
				frameSource.FrameReady+=(sender,e) => countFrameReady++;
				fake.PushFrame(2,2,(x,y) => 0x102030);
				Assert.AreEqual(1,countFrameReady);
				UvcFrame frame=frameSource.TakeLatestFrame();
				Assert.AreEqual(0x30,frame.Pixels[0]);
				Assert.AreEqual(0x20,frame.Pixels[1]);
				Assert.AreEqual(0x10,frame.Pixels[2]);
				Assert.IsNull(frameSource.TakeLatestFrame());
			}
			Assert.IsTrue(backend.ListOpened[0].IsDisposed);
		}
	}
}
