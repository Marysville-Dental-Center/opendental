// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using ImagingCapture.Uvc;
using ImagingCapture.Uvc.DirectShow;
using ImagingCapture.Uvc.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>The DirectShow backend without a camera: the Windows components it builds graphs from, device enumeration, mode
	///parsing and the worker thread. A camera is never needed or opened here.</summary>
	[TestClass]
	public class DirectShowBackendTests {
		private sealed class NullCallback:ISampleGrabberCB {
			public int SampleCB(double SampleTime,IntPtr pSample) {
				return 0;
			}

			public int BufferCB(double SampleTime,IntPtr pBuffer,int BufferLen) {
				return 0;
			}
		}

		private static object CreateOrInconclusive(Guid clsid) {
			try {
				return DsUtil.CreateComObject(clsid);
			}
			catch(COMException ex) {
				Assert.Inconclusive("DirectShow class "+clsid+" is not available here ("+UvcErrors.FormatHResult(ex.ErrorCode)+").");
				return null;
			}
		}

		///<summary>Builds the camera-less part of the graph and calls each declared member that works without a source filter.
		///A wrong IID fails the casts; a wrong vtable slot returns nonsense or crashes.</summary>
		[TestMethod]
		public void GraphComponents_AnswerTheDeclaredInterfaces() {
			object objGraph=null;
			object objBuilder=null;
			object objGrabber=null;
			object objNull=null;
			IEnumPins enumPins=null;
			IPin pinNullInput=null;
			try {
				objGraph=CreateOrInconclusive(Ds.CLSID_FilterGraph);
				IGraphBuilder graphBuilder=(IGraphBuilder)objGraph;
				IMediaControl mediaControl=(IMediaControl)objGraph;
				IMediaEventEx mediaEventEx=(IMediaEventEx)objGraph;
				objBuilder=CreateOrInconclusive(Ds.CLSID_CaptureGraphBuilder2);
				ICaptureGraphBuilder2 captureGraphBuilder=(ICaptureGraphBuilder2)objBuilder;
				Assert.AreEqual(Ds.S_OK,captureGraphBuilder.SetFiltergraph(graphBuilder));
				objGrabber=CreateOrInconclusive(Ds.CLSID_SampleGrabber);
				ISampleGrabber sampleGrabber=(ISampleGrabber)objGrabber;
				IBaseFilter filterGrabber=(IBaseFilter)objGrabber;
				AM_MEDIA_TYPE mediaType=new AM_MEDIA_TYPE { majortype=Ds.MEDIATYPE_Video,subtype=Ds.MEDIASUBTYPE_RGB32 };
				Assert.AreEqual(Ds.S_OK,sampleGrabber.SetMediaType(ref mediaType));
				Assert.AreEqual(Ds.S_OK,sampleGrabber.SetBufferSamples(false));
				Assert.AreEqual(Ds.S_OK,sampleGrabber.SetOneShot(false));
				Assert.AreEqual(Ds.S_OK,sampleGrabber.SetCallback(new NullCallback(),Ds.SampleGrabberCallBufferCB));
				Assert.AreEqual(Ds.S_OK,graphBuilder.AddFilter(filterGrabber,"Grabber"));
				AM_MEDIA_TYPE mediaTypeConnected=new AM_MEDIA_TYPE();
				Assert.AreEqual(Ds.VFW_E_NOT_CONNECTED,sampleGrabber.GetConnectedMediaType(ref mediaTypeConnected));
				objNull=CreateOrInconclusive(Ds.CLSID_NullRenderer);
				IBaseFilter filterNull=(IBaseFilter)objNull;
				Assert.AreEqual(Ds.S_OK,graphBuilder.AddFilter(filterNull,"Null renderer"));
				Assert.AreEqual(Ds.S_OK,filterNull.EnumPins(out enumPins));
				IPin[] arrayPins=new IPin[1];
				Assert.AreEqual(Ds.S_OK,enumPins.Next(1,arrayPins,IntPtr.Zero));
				pinNullInput=arrayPins[0];
				PIN_DIRECTION pinDirection;
				Assert.AreEqual(Ds.S_OK,pinNullInput.QueryDirection(out pinDirection));
				Assert.AreEqual(PIN_DIRECTION.PINDIR_INPUT,pinDirection);
				IPin pinPeer;
				Assert.AreEqual(Ds.VFW_E_NOT_CONNECTED,pinNullInput.ConnectedTo(out pinPeer));
				Assert.IsNull(pinPeer);
				Assert.AreEqual(Ds.S_FALSE,enumPins.Next(1,arrayPins,IntPtr.Zero),"The Null Renderer has one pin.");
				//No capture pin on a grabber, and no camera control upstream of a renderer.
				Guid guidCapture=Ds.PIN_CATEGORY_CAPTURE;
				Guid guidVideo=Ds.MEDIATYPE_Video;
				IPin pinFound;
				Assert.IsTrue(captureGraphBuilder.FindPin(filterGrabber,PIN_DIRECTION.PINDIR_OUTPUT,ref guidCapture,ref guidVideo,false,0,out pinFound)<0);
				Assert.IsNull(pinFound);
				Guid guidUpstream=Ds.LOOK_UPSTREAM_ONLY;
				Guid iidCameraControl=typeof(IAMCameraControl).GUID;
				object objControl;
				Assert.IsTrue(captureGraphBuilder.FindInterface(ref guidUpstream,IntPtr.Zero,filterNull,ref iidCameraControl,out objControl)<0);
				Assert.IsNull(objControl);
				//An idle graph: stopped, no events queued, a valid event handle.
				int state;
				Assert.AreEqual(Ds.S_OK,mediaControl.GetState(0,out state));
				Assert.AreEqual(0,state,"State_Stopped");
				IntPtr hEvent;
				Assert.AreEqual(Ds.S_OK,mediaEventEx.GetEventHandle(out hEvent));
				Assert.AreNotEqual(IntPtr.Zero,hEvent);
				int eventCode;
				IntPtr param1;
				IntPtr param2;
				Assert.AreEqual(Ds.E_ABORT,mediaEventEx.GetEvent(out eventCode,out param1,out param2,0));
				Assert.AreEqual(Ds.S_OK,mediaControl.Stop());
				Assert.AreEqual(Ds.S_OK,sampleGrabber.SetCallback(null,Ds.SampleGrabberCallBufferCB));
				Assert.AreEqual(Ds.S_OK,graphBuilder.RemoveFilter(filterNull));
				Assert.AreEqual(Ds.S_OK,graphBuilder.RemoveFilter(filterGrabber));
			}
			finally {
				DsUtil.Release(ref pinNullInput);
				DsUtil.Release(ref enumPins);
				DsUtil.Release(ref objNull);
				DsUtil.Release(ref objGrabber);
				DsUtil.Release(ref objBuilder);
				DsUtil.Release(ref objGraph);
			}
		}

		[TestMethod]
		public void GetDevices_ListsWithoutError() {
			List<UvcDeviceInfo> listDevices;
			try {
				listDevices=new DirectShowUvcBackend().GetDevices();
			}
			catch(UvcException ex) when(ex.Kind==UvcErrorKind.NotAvailable) {
				Assert.Inconclusive("DirectShow device enumeration is not available here.");
				return;
			}
			Assert.IsNotNull(listDevices);
			foreach(UvcDeviceInfo device in listDevices) {
				Assert.IsTrue(device.MonikerDisplayName.StartsWith("@device:",StringComparison.OrdinalIgnoreCase),device.MonikerDisplayName);
			}
		}

		[TestMethod]
		public void Open_DeviceNotPresent_ThrowsNoDevice() {
			UvcDeviceInfo device=new UvcDeviceInfo("Absent","",@"@device:pnp:\\?\absent#"+Guid.NewGuid().ToString("N"));
			UvcException exception=TestUtil.AssertThrows<UvcException>(() => new DirectShowUvcBackend().Open(device));
			Assert.AreEqual(UvcErrorKind.NoDevice,exception.Kind);
			TestUtil.AssertThrows<ArgumentNullException>(() => new DirectShowUvcBackend().Open(null));
		}

		private static VIDEOINFOHEADER MakeHeader(int width,int height,long avgTimePerFrame) {
			VIDEOINFOHEADER videoInfoHeader=new VIDEOINFOHEADER();
			videoInfoHeader.AvgTimePerFrame=avgTimePerFrame;
			videoInfoHeader.bmiHeader.biWidth=width;
			videoInfoHeader.bmiHeader.biHeight=height;
			videoInfoHeader.bmiHeader.biBitCount=16;
			return videoInfoHeader;
		}

		[TestMethod]
		public void CreateMode_UsesTheFastestIntervalTheCapsAllow() {
			UvcMode mode=DirectShowFrameSource.CreateMode(3,Ds.MEDIASUBTYPE_YUY2,MakeHeader(1280,720,666666),333333);
			Assert.AreEqual(3,mode.Index);
			Assert.AreEqual(1280,mode.Width);
			Assert.AreEqual(720,mode.Height);
			Assert.AreEqual(16,mode.BitCount);
			Assert.AreEqual("YUY2",mode.SubtypeName);
			Assert.AreEqual(333333,mode.FrameIntervalMin);
			mode=DirectShowFrameSource.CreateMode(0,Ds.MEDIASUBTYPE_YUY2,MakeHeader(1280,-720,666666),0);
			Assert.AreEqual(720,mode.Height,"A top-down format has a positive height.");
			Assert.AreEqual(666666,mode.FrameIntervalMin,"Without caps the format's own interval is used.");
			Assert.IsNull(DirectShowFrameSource.CreateMode(0,Ds.MEDIASUBTYPE_YUY2,MakeHeader(0,720,666666),0));
		}

		[TestMethod]
		public void ComWorker_RunsWorkOnOneMtaThreadAndRethrows() {
			using(ComWorker comWorker=new ComWorker("test")) {
				int threadIdCaller=Thread.CurrentThread.ManagedThreadId;
				int threadIdWorker=comWorker.Invoke(() => Thread.CurrentThread.ManagedThreadId);
				Assert.AreNotEqual(threadIdCaller,threadIdWorker);
				Assert.AreEqual(threadIdWorker,comWorker.Invoke(() => Thread.CurrentThread.ManagedThreadId));
				Assert.AreEqual(ApartmentState.MTA,comWorker.Invoke(() => Thread.CurrentThread.GetApartmentState()));
				Assert.IsTrue(comWorker.Invoke(() => comWorker.Invoke(() => comWorker.IsWorkerThread)),"Nested Invoke runs inline.");
				InvalidOperationException exception=TestUtil.AssertThrows<InvalidOperationException>(
					() => comWorker.Invoke(() => { throw new InvalidOperationException("from worker"); }));
				Assert.AreEqual("from worker",exception.Message);
				TestUtil.AssertThrows<InvalidOperationException>(() => comWorker.SetSignalHandler(null,null));
			}
		}

		[TestMethod]
		public void ComWorker_RunsTheSignalHandlerOnTheWorker() {
			using(ComWorker comWorker=new ComWorker("test"))
			using(ManualResetEvent eventSignal=new ManualResetEvent(false))
			using(ManualResetEvent eventHandled=new ManualResetEvent(false)) {
				int threadIdWorker=comWorker.Invoke(() => Thread.CurrentThread.ManagedThreadId);
				int threadIdHandler=0;
				comWorker.Invoke(() => comWorker.SetSignalHandler(eventSignal,() => {
					threadIdHandler=Thread.CurrentThread.ManagedThreadId;
					eventSignal.Reset();
					eventHandled.Set();
				}));
				eventSignal.Set();
				Assert.IsTrue(eventHandled.WaitOne(5000));
				Assert.AreEqual(threadIdWorker,threadIdHandler);
			}
		}

		[TestMethod]
		public void ComWorker_RefusesWorkAfterDispose() {
			ComWorker comWorker=new ComWorker("test");
			comWorker.Dispose();
			comWorker.Dispose();
			TestUtil.AssertThrows<ObjectDisposedException>(() => comWorker.Invoke(() => { }));
		}
	}
}
