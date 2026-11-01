// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingDeviceManager.Capture;

namespace ImagingCapture.Tests {
	///<summary>One QuarantineImage call of FakeTwainCaptureSession.</summary>
	internal sealed class FakeQuarantineCall {
		public AcquiredImage Image;
		public string Reason;
		public bool IsPatientUnknown;
		public TwainCaptureQuarantineContext Context;
		public string Path;
	}

	///<summary>A stand-in for TwainCaptureSession with no DSM and no worker thread: it records every command, and the test raises the events
	///itself (RaiseImage, RaiseEnded, RaiseStatus), as the worker would, after the commands. Open answers OpenResult at once.</summary>
	internal sealed class FakeTwainCaptureSession:ITwainCaptureSession {
		public TwainCaptureOpenResult OpenResult=new TwainCaptureOpenResult { IsOpen=true,SourceInfo=new TwainCaptureSourceInfo { ProductName="Example Sensor DS" } };
		public int CountOpen;
		public string OpenProductName;
		public TwainCaptureOptions OpenOptions;
		///<summary>What SessionGuid, QuarantineContext and FuncReadMetadataKeys were when Open was called.</summary>
		public Guid SessionGuidAtOpen;
		public TwainCaptureQuarantineContext QuarantineContextAtOpen;
		public Func<string,CaptureMetadataKeys> FuncReadMetadataKeysAtOpen;
		public readonly List<TwainEnableRequest> ListEnables=new List<TwainEnableRequest>();
		public readonly List<int> ListEnableIds=new List<int>();
		public readonly List<TwainCaptureStopReason> ListDisables=new List<TwainCaptureStopReason>();
		public readonly List<FakeQuarantineCall> ListQuarantined=new List<FakeQuarantineCall>();
		public int CountBeginClose;
		///<summary>Enable returns 0 (the session is not open).</summary>
		public bool IsEnableRefused;
		///<summary>Not null: QuarantineImage fails with this error.</summary>
		public string QuarantineError;
		private int _enableIdLast;
		private readonly Dictionary<int,int> _dictImagesPerEnable=new Dictionary<int,int>();

		public TwainCaptureState State { get; set; }=TwainCaptureState.Closed;
		public Guid SessionGuid { get; set; }
		public string ProductName { get; private set; }="";
		public TwainCaptureOptions Options { get; private set; }=new TwainCaptureOptions();
		public TwainCaptureSourceInfo SourceInfo { get; private set; }
		public TwainCaptureQuarantineContext QuarantineContext { get; set; }=new TwainCaptureQuarantineContext();
		public Func<string,CaptureMetadataKeys> FuncReadMetadataKeys { get; set; }

		public event Action<AcquiredImage> ImageAcquired;
		public event Action<TwainCaptureEnded> Ended;
		public event Action<TwainCaptureStatus> StatusChanged;
		public event Action<AcquiredImage> ImageQuarantined;
		public event Action<string> Logged;

		public TwainCaptureOpenResult Open(string productName,TwainCaptureOptions options) {
			CountOpen++;
			OpenProductName=productName;
			OpenOptions=options;
			SessionGuidAtOpen=SessionGuid;
			QuarantineContextAtOpen=QuarantineContext;
			FuncReadMetadataKeysAtOpen=FuncReadMetadataKeys;
			if(OpenResult.IsOpen) {
				State=TwainCaptureState.Open;
				ProductName=productName;
				Options=options;
				SourceInfo=OpenResult.SourceInfo;
			}
			return OpenResult;
		}

		public int Enable(TwainEnableRequest request) {
			if(IsEnableRefused || State==TwainCaptureState.Closed) {
				return 0;
			}
			_enableIdLast++;
			ListEnables.Add(request);
			ListEnableIds.Add(_enableIdLast);
			State=TwainCaptureState.Enabling;
			return _enableIdLast;
		}

		public int RequestRedelivery(TwainEnableRequest request) {
			return Enable(request);
		}

		public void Disable(TwainCaptureStopReason reason) {
			ListDisables.Add(reason);
		}

		public void Cancel() {
			Disable(TwainCaptureStopReason.Cancel);
		}

		public void BeginClose() {
			CountBeginClose++;
			State=TwainCaptureState.Closed;
		}

		public bool WaitForClose(TimeSpan timeout) {
			return true;
		}

		public void Dispose() {
			BeginClose();
		}

		public string QuarantineImage(AcquiredImage image,string reason,bool isPatientUnknown,out string error) {
			FakeQuarantineCall call=new FakeQuarantineCall { Image=image,Reason=reason,IsPatientUnknown=isPatientUnknown,Context=QuarantineContext };
			ListQuarantined.Add(call);
			if(QuarantineError!=null) {
				error=QuarantineError;
				return null;
			}
			error="";
			call.Path=@"C:\Quarantine\"+SessionGuid.ToString("N")+@"\image-"+ListQuarantined.Count+".bmp";
			image.QuarantinePath=call.Path;
			return call.Path;
		}

		#region Raising events (as the worker would)
		public TwainEnableRequest LastRequest => ListEnables.LastOrDefault();
		public int LastEnableId => ListEnableIds.LastOrDefault();

		///<summary>Raises image as an image of the last enable (its id, Tag, label and a running index), with kind and the stop reason.</summary>
		public AcquiredImage RaiseImage(AcquiredImage image,AcquiredImageKind kind=AcquiredImageKind.Normal,
			TwainCaptureStopReason stopReason=TwainCaptureStopReason.None,int enableId=0)
		{
			int id=enableId==0 ? LastEnableId : enableId;
			TwainEnableRequest request=ListEnables[ListEnableIds.IndexOf(id)];
			int count;
			_dictImagesPerEnable.TryGetValue(id,out count);
			count++;
			_dictImagesPerEnable[id]=count;
			image.EnableId=id;
			image.Tag=request.Tag;
			image.TargetLabel=request.TargetLabel;
			image.RequestedBits=request.Bits;
			image.Kind=kind;
			image.StopReason=stopReason;
			image.IndexInEnable=count;
			ImageAcquired?.Invoke(image);
			return image;
		}

		///<summary>Raises the end of the last enable (or of enableId), with ImageCount from the images raised for it.</summary>
		public TwainCaptureEnded RaiseEnded(TwainCaptureEndReason reason,Action<TwainCaptureEnded> actionConfigure=null,int enableId=0) {
			int id=enableId==0 ? LastEnableId : enableId;
			TwainEnableRequest request=ListEnables[ListEnableIds.IndexOf(id)];
			int count;
			_dictImagesPerEnable.TryGetValue(id,out count);
			TwainCaptureEnded ended=new TwainCaptureEnded {
				EnableId=id,
				Tag=request.Tag,
				Reason=reason,
				ImageCount=count,
				EndedAt=new DateTime(2026,10,31,10,0,9),
			};
			actionConfigure?.Invoke(ended);
			State=TwainCaptureState.Open;
			Ended?.Invoke(ended);
			return ended;
		}

		public void RaiseStatus(TwainCaptureState state,bool isSafeDisableInEffect=false,int enableId=-1) {
			State=state;
			StatusChanged?.Invoke(new TwainCaptureStatus {
				State=state,
				EnableId=enableId==-1 ? LastEnableId : enableId,
				Text=TwainCaptureSession.GetStateText(state),
				IsSafeDisableInEffect=isSafeDisableInEffect,
			});
		}

		public void RaiseQuarantinedByLibrary(AcquiredImage image) {
			image.QuarantinePath=@"C:\Quarantine\late.bmp";
			ImageQuarantined?.Invoke(image);
		}

		public void RaiseLogged(string line) {
			Logged?.Invoke(line);
		}
		#endregion Raising events (as the worker would)
	}
}
