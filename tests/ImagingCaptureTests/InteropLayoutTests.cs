// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using ImagingCapture.Uvc.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>Pins the hand-written DirectShow declarations to the Windows SDK: structure sizes and offsets in a 32-bit process,
	///and the vtable slot of every member that is called. A wrong slot would call the wrong method at run time.</summary>
	[TestClass]
	public class InteropLayoutTests {
		private static int Offset(Type type,string field) {
			return Marshal.OffsetOf(type,field).ToInt32();
		}

		[TestMethod]
		public void Process_Is32Bit() {
			Assert.AreEqual(4,IntPtr.Size,"Run the tests with /Platform:x86.");
		}

		[TestMethod]
		public void StructSizes_MatchTheSdk() {
			Assert.AreEqual(72,Marshal.SizeOf(typeof(AM_MEDIA_TYPE)));
			Assert.AreEqual(16,Marshal.SizeOf(typeof(RECT)));
			Assert.AreEqual(8,Marshal.SizeOf(typeof(SIZE)));
			Assert.AreEqual(40,Marshal.SizeOf(typeof(BITMAPINFOHEADER)));
			Assert.AreEqual(88,Marshal.SizeOf(typeof(VIDEOINFOHEADER)));
			Assert.AreEqual(128,Marshal.SizeOf(typeof(VIDEO_STREAM_CONFIG_CAPS)));
		}

		[TestMethod]
		public void MediaType_FieldOffsets() {
			Assert.AreEqual(16,Offset(typeof(AM_MEDIA_TYPE),"subtype"));
			Assert.AreEqual(44,Offset(typeof(AM_MEDIA_TYPE),"formattype"));
			Assert.AreEqual(60,Offset(typeof(AM_MEDIA_TYPE),"pUnk"));
			Assert.AreEqual(64,Offset(typeof(AM_MEDIA_TYPE),"cbFormat"));
			Assert.AreEqual(68,Offset(typeof(AM_MEDIA_TYPE),"pbFormat"));
		}

		[TestMethod]
		public void VideoInfoHeader_FieldOffsets() {
			Assert.AreEqual(16,Offset(typeof(VIDEOINFOHEADER),"rcTarget"));
			Assert.AreEqual(40,Offset(typeof(VIDEOINFOHEADER),"AvgTimePerFrame"));
			Assert.AreEqual(48,Offset(typeof(VIDEOINFOHEADER),"bmiHeader"));
			Assert.AreEqual(14,Offset(typeof(BITMAPINFOHEADER),"biBitCount"));
			Assert.AreEqual(16,Offset(typeof(BITMAPINFOHEADER),"biCompression"));
		}

		[TestMethod]
		public void StreamConfigCaps_FieldOffsets() {
			Assert.AreEqual(60,Offset(typeof(VIDEO_STREAM_CONFIG_CAPS),"MinOutputSize"));
			Assert.AreEqual(68,Offset(typeof(VIDEO_STREAM_CONFIG_CAPS),"MaxOutputSize"));
			Assert.AreEqual(104,Offset(typeof(VIDEO_STREAM_CONFIG_CAPS),"MinFrameInterval"));
			Assert.AreEqual(112,Offset(typeof(VIDEO_STREAM_CONFIG_CAPS),"MaxFrameInterval"));
			Assert.AreEqual(124,Offset(typeof(VIDEO_STREAM_CONFIG_CAPS),"MaxBitsPerSecond"));
		}

		///<summary>(interface, member, slot). Slots count from 0 and include IUnknown (3 members) and, for dual interfaces,
		///IDispatch (4 more). Each expected slot is the member's position in the SDK header's vtable.</summary>
		private static readonly List<Tuple<Type,string,int>> _listSlots=new List<Tuple<Type,string,int>> {
			Tuple.Create(typeof(ICreateDevEnum),"CreateClassEnumerator",3),
			Tuple.Create(typeof(IEnumMoniker),"Next",3),
			//IMoniker: IPersist (1) and IPersistStream (4) come before IMoniker's own members.
			Tuple.Create(typeof(IMoniker),"BindToObject",8),
			Tuple.Create(typeof(IMoniker),"BindToStorage",9),
			Tuple.Create(typeof(IMoniker),"GetDisplayName",20),
			Tuple.Create(typeof(IPropertyBag),"Read",3),
			Tuple.Create(typeof(IGraphBuilder),"AddFilter",3),
			Tuple.Create(typeof(IGraphBuilder),"RemoveFilter",4),
			Tuple.Create(typeof(IGraphBuilder),"Disconnect",9),
			Tuple.Create(typeof(ICaptureGraphBuilder2),"SetFiltergraph",3),
			Tuple.Create(typeof(ICaptureGraphBuilder2),"FindInterface",6),
			Tuple.Create(typeof(ICaptureGraphBuilder2),"RenderStream",7),
			Tuple.Create(typeof(ICaptureGraphBuilder2),"FindPin",11),
			Tuple.Create(typeof(IMediaControl),"Run",7),
			Tuple.Create(typeof(IMediaControl),"Stop",9),
			Tuple.Create(typeof(IMediaControl),"GetState",10),
			Tuple.Create(typeof(IMediaEventEx),"GetEventHandle",7),
			Tuple.Create(typeof(IMediaEventEx),"GetEvent",8),
			Tuple.Create(typeof(IMediaEventEx),"FreeEventParams",12),
			//IBaseFilter: IPersist (1) and IMediaFilter (6) come before EnumPins.
			Tuple.Create(typeof(IBaseFilter),"EnumPins",10),
			Tuple.Create(typeof(IEnumPins),"Next",3),
			Tuple.Create(typeof(IPin),"ConnectedTo",6),
			Tuple.Create(typeof(IPin),"QueryDirection",9),
			Tuple.Create(typeof(IAMStreamConfig),"SetFormat",3),
			Tuple.Create(typeof(IAMStreamConfig),"GetNumberOfCapabilities",5),
			Tuple.Create(typeof(IAMStreamConfig),"GetStreamCaps",6),
			Tuple.Create(typeof(ISampleGrabber),"SetOneShot",3),
			Tuple.Create(typeof(ISampleGrabber),"SetMediaType",4),
			Tuple.Create(typeof(ISampleGrabber),"GetConnectedMediaType",5),
			Tuple.Create(typeof(ISampleGrabber),"SetBufferSamples",6),
			Tuple.Create(typeof(ISampleGrabber),"SetCallback",9),
			Tuple.Create(typeof(ISampleGrabberCB),"SampleCB",3),
			Tuple.Create(typeof(ISampleGrabberCB),"BufferCB",4),
			//IAMCameraControl and IAMVideoProcAmp: GetRange, Set, Get. Set is a placeholder that is never called.
			Tuple.Create(typeof(IAMCameraControl),"GetRange",3),
			Tuple.Create(typeof(IAMCameraControl),"Unused_Set",4),
			Tuple.Create(typeof(IAMCameraControl),"Get",5),
			Tuple.Create(typeof(IAMVideoProcAmp),"GetRange",3),
			Tuple.Create(typeof(IAMVideoProcAmp),"Unused_Set",4),
			Tuple.Create(typeof(IAMVideoProcAmp),"Get",5),
		};

		[TestMethod]
		public void VtableSlots_MatchTheSdkOrder() {
			foreach(Tuple<Type,string,int> tuple in _listSlots) {
				MethodInfo methodInfo=tuple.Item1.GetMethod(tuple.Item2);
				Assert.IsNotNull(methodInfo,tuple.Item1.Name+"."+tuple.Item2+" is not declared.");
				Assert.AreEqual(tuple.Item3,Marshal.GetComSlotForMethodInfo(methodInfo),tuple.Item1.Name+"."+tuple.Item2);
			}
		}

		[TestMethod]
		public void Interfaces_DeclareOnlyCalledMembersAndPlaceholders() {
			//Every member is either listed above (it is called, and its slot is pinned) or a placeholder (Unused_*). The last
			//declared member of each interface is a called one, so no placeholder trails the vtable.
			List<Type> listTypes=typeof(Ds).Assembly.GetTypes()
				.Where(x => x.IsInterface && x.Namespace==typeof(Ds).Namespace && x.IsDefined(typeof(ComImportAttribute),false))
				.ToList();
			Assert.AreEqual(16,listTypes.Count,"COM interfaces declared in ImagingCapture.Uvc.Interop.");
			foreach(Type type in listTypes) {
				MethodInfo[] arrayMethods=type.GetMethods();
				Assert.IsTrue(arrayMethods.Length>0,type.Name);
				foreach(MethodInfo methodInfo in arrayMethods) {
					bool isPinned=_listSlots.Any(x => x.Item1==type && x.Item2==methodInfo.Name);
					bool isPlaceholder=methodInfo.Name.StartsWith("Unused_");
					Assert.IsTrue(isPinned || isPlaceholder,type.Name+"."+methodInfo.Name+" is neither pinned nor a placeholder.");
					if(isPlaceholder) {
						Assert.AreEqual(0,methodInfo.GetParameters().Length,type.Name+"."+methodInfo.Name+" placeholder has parameters.");
					}
				}
				MethodInfo methodLast=arrayMethods.OrderBy(x => Marshal.GetComSlotForMethodInfo(x)).Last();
				Assert.IsFalse(methodLast.Name.StartsWith("Unused_"),type.Name+" ends with a placeholder.");
			}
		}

		[TestMethod]
		public void FourCCSubtypes_ShareTheFourCCBase() {
			byte[] arrayBase=Ds.FourCCBase.ToByteArray();
			foreach(Guid guid in new[] { Ds.MEDIASUBTYPE_MJPG,Ds.MEDIASUBTYPE_YUY2,Ds.MEDIATYPE_Video }) {
				byte[] arrayBytes=guid.ToByteArray();
				CollectionAssert.AreEqual(arrayBase.Skip(4).ToArray(),arrayBytes.Skip(4).ToArray(),guid.ToString());
			}
			Assert.AreEqual(0x47504A4D,BitConverter.ToInt32(Ds.MEDIASUBTYPE_MJPG.ToByteArray(),0));//'M','J','P','G'
			Assert.AreEqual(0x32595559,BitConverter.ToInt32(Ds.MEDIASUBTYPE_YUY2.ToByteArray(),0));//'Y','U','Y','2'
		}
	}
}
