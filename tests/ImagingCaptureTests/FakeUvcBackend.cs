// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Uvc;

namespace ImagingCapture.Tests {
	///<summary>A scripted camera API for tests: devices, modes, still pin, control values, frames and errors are all set by the
	///test. Nothing here describes any real camera.</summary>
	internal sealed class FakeUvcBackend:IUvcBackend {
		public List<UvcDeviceInfo> ListDevices=new List<UvcDeviceInfo>();
		///<summary>Modes given to each source opened from now on.</summary>
		public List<UvcMode> ListModes=new List<UvcMode>();
		public bool HasStillPin=false;
		///<summary>When not None, Open throws a UvcException of this kind.</summary>
		public UvcErrorKind OpenErrorKind=UvcErrorKind.None;
		///<summary>Given to each source opened from now on: see FakeFrameSource.StartErrorKindWithStillPin.</summary>
		public UvcErrorKind StartErrorKindWithStillPin=UvcErrorKind.None;
		public List<FakeFrameSource> ListOpened=new List<FakeFrameSource>();

		public string Name {
			get {
				return "Fake";
			}
		}

		///<summary>Adds a device named "Camera N" with a made-up moniker and returns it.</summary>
		public UvcDeviceInfo AddDevice(string friendlyName=null) {
			int number=ListDevices.Count+1;
			UvcDeviceInfo device=new UvcDeviceInfo(friendlyName??("Camera "+number),@"\\?\fake#camera#"+number,"@device:fake:"+number);
			ListDevices.Add(device);
			return device;
		}

		public List<UvcDeviceInfo> GetDevices() {
			return new List<UvcDeviceInfo>(ListDevices);
		}

		public IFrameSource Open(UvcDeviceInfo device) {
			if(OpenErrorKind!=UvcErrorKind.None) {
				throw new UvcException(OpenErrorKind,"Scripted open failure.");
			}
			if(!ListDevices.Any(x => x.IsSameDevice(device))) {
				throw new UvcException(UvcErrorKind.NoDevice,"No such device.");
			}
			FakeFrameSource frameSource=new FakeFrameSource(device,ListModes,HasStillPin);
			frameSource.StartErrorKindWithStillPin=StartErrorKindWithStillPin;
			ListOpened.Add(frameSource);
			return frameSource;
		}
	}

	///<summary>An opened fake camera. Controls default to Unsupported until a value or script is set.</summary>
	internal sealed class FakeFrameSource:IFrameSource {
		private readonly FrameExchange _frameExchange=new FrameExchange();
		private readonly Dictionary<int,FakeControl> _dictControls=new Dictionary<int,FakeControl>();
		///<summary>Every ReadControl call per control, whatever its outcome. Locked: a poll thread may read while the test asks.</summary>
		private readonly Dictionary<int,int> _dictReadCalls=new Dictionary<int,int>();
		private readonly List<UvcMode> _listModes;
		private long _sequenceStill;
		private bool _isStillRequested;

		public UvcDeviceInfo Device { get; }
		public bool HasStillPin { get; }
		public bool IsRunning { get; private set; }
		public UvcMode ModeStarted { get; private set; }
		public bool IsStillPinConnected { get; private set; }
		public long FramesReceived { get; private set; }
		public bool IsDisposed { get; private set; }
		public int CountStart { get; private set; }
		public int CountStop { get; private set; }
		///<summary>When not None, Start throws a UvcException of this kind.</summary>
		public UvcErrorKind StartErrorKind=UvcErrorKind.None;
		///<summary>When not None, Start with connectStillPin throws a UvcException of this kind, as a camera does whose graph does
		///not run with the still pin connected. Start without it works.</summary>
		public UvcErrorKind StartErrorKindWithStillPin=UvcErrorKind.None;

		public event EventHandler FrameReady;
		public event EventHandler<UvcStillEventArgs> StillCaptured;
		public event EventHandler<UvcErrorEventArgs> Error;

		public FakeFrameSource(UvcDeviceInfo device,List<UvcMode> listModes,bool hasStillPin) {
			Device=device;
			_listModes=new List<UvcMode>(listModes);
			HasStillPin=hasStillPin;
		}

		public IReadOnlyList<UvcMode> Modes {
			get {
				return _listModes;
			}
		}

		public void Start(UvcMode mode,bool connectStillPin) {
			ThrowIfDisposed();
			if(StartErrorKind!=UvcErrorKind.None) {
				throw new UvcException(StartErrorKind,"Scripted start failure.");
			}
			if(StartErrorKindWithStillPin!=UvcErrorKind.None && connectStillPin) {
				throw new UvcException(StartErrorKindWithStillPin,"Scripted start failure with the still pin.");
			}
			if(CountStart>0 && (mode!=ModeStarted || connectStillPin!=_isStillRequested)) {
				throw new InvalidOperationException("The mode and still choice are fixed by the first Start.");
			}
			CountStart++;
			if(CountStart==1) {
				ModeStarted=mode;
				_isStillRequested=connectStillPin;
				IsStillPinConnected=connectStillPin && HasStillPin;
			}
			IsRunning=true;
		}

		public void Stop() {
			if(IsRunning) {
				CountStop++;
			}
			IsRunning=false;
		}

		public UvcFrame TakeLatestFrame() {
			return _frameExchange.TakeLatest();
		}

		public UvcControlValue ReadControl(UvcControlInterface controlInterface,int property) {
			lock(_dictReadCalls) {
				int key=GetKey(controlInterface,property);
				int countCalls;
				_dictReadCalls.TryGetValue(key,out countCalls);
				_dictReadCalls[key]=countCalls+1;
			}
			if(IsDisposed) {
				return new UvcControlValue(UvcControlStatus.Closed,0,0,0);
			}
			FakeControl fakeControl=GetControl(controlInterface,property,false);
			if(fakeControl==null || fakeControl.Status!=UvcControlStatus.Ok) {
				UvcControlStatus status=fakeControl?.Status??UvcControlStatus.Unsupported;
				return new UvcControlValue(status,0,0,unchecked((int)0x80070490));
			}
			int value=fakeControl.FuncValue!=null ? fakeControl.FuncValue(fakeControl.CountReads) : fakeControl.Value;
			fakeControl.CountReads++;
			return new UvcControlValue(UvcControlStatus.Ok,value,2,0);
		}

		public UvcControlRange ReadControlRange(UvcControlInterface controlInterface,int property) {
			FakeControl fakeControl=GetControl(controlInterface,property,false);
			if(IsDisposed || fakeControl==null || fakeControl.Status!=UvcControlStatus.Ok) {
				return new UvcControlRange(IsDisposed ? UvcControlStatus.Closed : UvcControlStatus.Unsupported,0,0,0,0,0,0);
			}
			return new UvcControlRange(UvcControlStatus.Ok,fakeControl.Min,fakeControl.Max,1,fakeControl.Default,2,0);
		}

		public void Dispose() {
			IsRunning=false;
			IsDisposed=true;
		}

		#region Scripting
		///<summary>The property reads value from now on.</summary>
		public void SetControlValue(UvcControlInterface controlInterface,int property,int value) {
			FakeControl fakeControl=GetControl(controlInterface,property,true);
			fakeControl.Status=UvcControlStatus.Ok;
			fakeControl.FuncValue=null;
			fakeControl.Value=value;
		}

		///<summary>The property reads funcValue(n) on its n-th read (from 0).</summary>
		public void SetControlScript(UvcControlInterface controlInterface,int property,Func<int,int> funcValue) {
			FakeControl fakeControl=GetControl(controlInterface,property,true);
			fakeControl.Status=UvcControlStatus.Ok;
			fakeControl.FuncValue=funcValue;
		}

		public void SetControlStatus(UvcControlInterface controlInterface,int property,UvcControlStatus status) {
			GetControl(controlInterface,property,true).Status=status;
		}

		///<summary>ReadControl calls for this control so far, including those that failed.</summary>
		public int GetReadCount(UvcControlInterface controlInterface,int property) {
			lock(_dictReadCalls) {
				int countCalls;
				_dictReadCalls.TryGetValue(GetKey(controlInterface,property),out countCalls);
				return countCalls;
			}
		}

		///<summary>Publishes a preview frame whose pixels come from funcColor(x,y) as 0xRRGGBB, and raises FrameReady.</summary>
		public void PushFrame(int width,int height,Func<int,int,int> funcColor,double sampleTime=0) {
			UvcFrame frame=_frameExchange.GetBackBuffer(width,height);
			FillFrame(frame,funcColor);
			_frameExchange.Publish(sampleTime);
			FramesReceived++;
			FrameReady?.Invoke(this,EventArgs.Empty);
		}

		///<summary>Raises StillCaptured with a new frame.</summary>
		public void PushStill(int width,int height,Func<int,int,int> funcColor) {
			UvcFrame frame=new UvcFrame(width,height);
			FillFrame(frame,funcColor);
			_sequenceStill++;
			frame.Sequence=_sequenceStill;
			StillCaptured?.Invoke(this,new UvcStillEventArgs(frame));
		}

		public void RaiseError(UvcErrorKind kind,int hResult=0) {
			Error?.Invoke(this,new UvcErrorEventArgs(kind,hResult,"Scripted error."));
		}
		#endregion Scripting

		private static void FillFrame(UvcFrame frame,Func<int,int,int> funcColor) {
			for(int y=0;y<frame.Height;y++) {
				for(int x=0;x<frame.Width;x++) {
					int rgb=funcColor(x,y);
					int index=y*frame.Stride+x*4;
					frame.Pixels[index]=(byte)rgb;
					frame.Pixels[index+1]=(byte)(rgb>>8);
					frame.Pixels[index+2]=(byte)(rgb>>16);
					frame.Pixels[index+3]=0xFF;
				}
			}
		}

		private FakeControl GetControl(UvcControlInterface controlInterface,int property,bool create) {
			int key=GetKey(controlInterface,property);
			FakeControl fakeControl;
			if(!_dictControls.TryGetValue(key,out fakeControl) && create) {
				fakeControl=new FakeControl();
				_dictControls[key]=fakeControl;
			}
			return fakeControl;
		}

		private static int GetKey(UvcControlInterface controlInterface,int property) {
			return ((int)controlInterface<<16)|(property&0xFFFF);
		}

		private void ThrowIfDisposed() {
			if(IsDisposed) {
				throw new ObjectDisposedException(nameof(FakeFrameSource));
			}
		}

		private sealed class FakeControl {
			public UvcControlStatus Status=UvcControlStatus.Unsupported;
			public int Value;
			public Func<int,int> FuncValue;
			public int CountReads;
			public int Min=0;
			public int Max=255;
			public int Default=0;
		}
	}
}
