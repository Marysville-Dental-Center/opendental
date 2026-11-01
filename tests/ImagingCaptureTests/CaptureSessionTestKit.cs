// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Save;
using ImagingCapture.Session;
using ImagingCapture.Settings;
using ImagingDeviceManager.Capture;
using OpenDentBusiness;

namespace ImagingCapture.Tests {
	///<summary>Runs what the controller posts, in order, when the test says so (RunPending), on the test's thread: the "UI thread" of these
	///tests. RunInBackground is queued the same way, so nothing re-enters the controller inside one of its own calls.</summary>
	internal sealed class QueueCaptureDispatcher:ICaptureDispatcher {
		private readonly Queue<Action> _queue=new Queue<Action>();
		public int CountBackground;

		public void Post(Action action) {
			_queue.Enqueue(action);
		}

		public void RunInBackground(Action action) {
			CountBackground++;
			_queue.Enqueue(action);
		}

		///<summary>Runs everything queued, also what the queued actions queue.</summary>
		public void RunPending() {
			int guard=0;
			while(_queue.Count>0) {
				if(++guard>10000) {
					throw new InvalidOperationException("The dispatcher queue does not empty.");
				}
				_queue.Dequeue()();
			}
		}
	}

	internal sealed class FakeCaptureClock:ICaptureClock {
		public DateTime Now { get; set; }=new DateTime(2026,10,31,10,0,0);
		public long NowMs { get; set; }=100000;

		public void Advance(int ms) {
			NowMs+=ms;
			Now=Now.AddMilliseconds(ms);
		}
	}

	internal sealed class FakeCaptureSessionStore:ICaptureSessionStore {
		public bool IsAuthorizedResult=true;
		public EnumPermType PermTypeChecked;
		///<summary>Permissions that IsAuthorized refuses whatever IsAuthorizedResult says.</summary>
		public HashSet<EnumPermType> HashSetPermsDenied=new HashSet<EnumPermType>();
		public Dictionary<long,List<MountItemDef>> DictMountItemDefs=new Dictionary<long,List<MountItemDef>>();
		public readonly List<MountItemDef> ListMountItemDefsUpdated=new List<MountItemDef>();
		public readonly List<MountItem> ListMountItemsUpdated=new List<MountItem>();
		///<summary>Names of calls that throw: "MakeLogEntry", "UpdateMountItemDef", "UpdateMountItem".</summary>
		public string ThrowOn="";
		public HashSet<long> HashSetCategories=new HashSet<long> { 20 };
		public Dictionary<long,List<MountItem>> DictMountItems=new Dictionary<long,List<MountItem>>();
		public Dictionary<long,Document> DictDocumentsByMountItem=new Dictionary<long,Document>();
		public long DefaultProvNum=4;
		public List<SecurityLog> ListSetupLogs=new List<SecurityLog>();
		public List<string> ListLogTexts=new List<string>();
		public bool IsThrowOnMountItems;

		public bool IsAuthorized(EnumPermType permType) {
			PermTypeChecked=permType;
			return IsAuthorizedResult && !HashSetPermsDenied.Contains(permType);
		}

		public bool IsCategoryValid(long defNum) {
			return HashSetCategories.Contains(defNum);
		}

		public List<MountItem> GetMountItems(long mountNum) {
			if(IsThrowOnMountItems) {
				throw new Exception("The database is not reachable.");
			}
			List<MountItem> listMountItems;
			return DictMountItems.TryGetValue(mountNum,out listMountItems) ? listMountItems.ToList() : new List<MountItem>();
		}

		public List<Document> GetDocumentsForMountItems(List<MountItem> listMountItems) {
			return listMountItems.Select(x => {
				Document document;
				return DictDocumentsByMountItem.TryGetValue(x.MountItemNum,out document) ? document : null;
			}).ToList();
		}

		public long GetDefaultProvNum(Patient patient) {
			return DefaultProvNum;
		}

		public string GetProviderAbbr(long provNum) {
			return "DOC"+provNum;
		}

		public List<SecurityLog> GetSetupLogEntries(long imagingDeviceNum) {
			return ListSetupLogs.Where(x => x.FKey==imagingDeviceNum).ToList();
		}

		public void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey) {
			Call("MakeLogEntry");
			ListLogTexts.Add(permType+"|"+patNum+"|"+fKey+"|"+logText);
		}

		public List<MountItemDef> GetMountItemDefs(long mountDefNum) {
			List<MountItemDef> listMountItemDefs;
			return DictMountItemDefs.TryGetValue(mountDefNum,out listMountItemDefs) ? listMountItemDefs.ToList() : new List<MountItemDef>();
		}

		public void UpdateMountItemDef(MountItemDef mountItemDef) {
			Call("UpdateMountItemDef");
			ListMountItemDefsUpdated.Add(mountItemDef.Copy());
		}

		public void UpdateMountItem(MountItem mountItem) {
			Call("UpdateMountItem");
			ListMountItemsUpdated.Add(mountItem.Copy());
		}

		///<summary>The security-log entries of permType, as "patNum|fKey|logText".</summary>
		public List<string> GetLogs(EnumPermType permType) {
			string prefix=permType+"|";
			return ListLogTexts.Where(x => x.StartsWith(prefix)).Select(x => x.Substring(prefix.Length)).ToList();
		}

		private void Call(string name) {
			if(ThrowOn.Split('|').Contains(name)) {
				throw new Exception(name+" failed");
			}
		}
	}

	///<summary>IRadiographStore for the session tests: every import becomes a document with a new DocNum; ThrowOn names calls that fail.</summary>
	internal sealed class FakeSessionRadiographStore:IRadiographStore {
		public string ThrowOn="";
		public readonly List<Document> ListDocumentsUpdated=new List<Document>();
		public readonly List<Patient> ListPatientsImported=new List<Patient>();
		public readonly List<long> ListCategoriesImported=new List<long>();
		public readonly List<string> ListExtensions=new List<string>();
		public readonly List<Mount> ListMountsUpdated=new List<Mount>();
		public readonly List<MountItem> ListMountItemsInserted=new List<MountItem>();
		public readonly List<MountItem> ListMountItemsDeleted=new List<MountItem>();
		private long _docNumNext=1000;
		private long _mountItemNumNext=500;

		private void Call(string name) {
			if(ThrowOn.Split('|').Contains(name)) {
				throw new Exception(name+" failed");
			}
		}

		public bool IsCategoryValid(long defNum) {
			Call("IsCategoryValid");
			return true;
		}

		public Document Import(byte[] bytes,long docCategory,ImageType imageType,Patient patient,string mimeType,string fileExtension) {
			Call("Import");
			ListPatientsImported.Add(patient);
			ListCategoriesImported.Add(docCategory);
			ListExtensions.Add(fileExtension);
			return new Document { DocNum=_docNumNext++,PatNum=patient.PatNum,DocCategory=docCategory,ImgType=imageType,Description="",
				FileName="img"+_docNumNext+fileExtension };
		}

		public void InsertMountItem(MountItem mountItem) {
			Call("InsertMountItem");
			mountItem.MountItemNum=_mountItemNumNext++;
			ListMountItemsInserted.Add(mountItem);
		}

		public void UpdateDocument(Document document,Document documentOld) {
			Call("UpdateDocument");
			ListDocumentsUpdated.Add(document);
		}

		public void DeleteDocument(Document document,Patient patient) {
			Call("DeleteDocument");
		}

		public void DeleteMountItem(MountItem mountItem) {
			Call("DeleteMountItem");
			ListMountItemsDeleted.Add(mountItem);
		}

		public void UpdateMount(Mount mount) {
			Call("UpdateMount");
			ListMountsUpdated.Add(mount);
		}

		public void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey) {
			Call("MakeLogEntry");
		}

		public void SendToPearlIfConfigured(Patient patient,Document document,byte[] pngBytes,MountItem mountItem) {
			Call("SendToPearl");
		}
	}

	///<summary>Stands in for the session window: records the controller and runs the test's script on it while "modal".</summary>
	internal sealed class FakeWindowRunner:ICaptureSessionWindowRunner {
		public CaptureSessionController Controller;
		public object Owner;
		public int CountShown;
		public Action<CaptureSessionController> ActionWhileShown;

		public void ShowModal(CaptureSessionController controller,object owner) {
			CountShown++;
			Controller=controller;
			Owner=owner;
			ActionWhileShown?.Invoke(controller);
		}
	}

	///<summary>A capture session controller on fakes: the patient 55, an FMX-like mount 7 with slots, the device 12, the category 20 and the
	///provider 4. The test drives it like the window, then raises the worker's events through Twain; every helper runs what was posted.</summary>
	internal sealed class CaptureControllerHarness {
		public const long PatNum=55;
		public readonly FakeCaptureClock Clock=new FakeCaptureClock();
		public readonly QueueCaptureDispatcher Dispatcher=new QueueCaptureDispatcher();
		public readonly FakeCaptureSessionStore Store=new FakeCaptureSessionStore();
		public readonly FakeSessionRadiographStore RadiographStore=new FakeSessionRadiographStore();
		public readonly List<FakeTwainCaptureSession> ListSessions=new List<FakeTwainCaptureSession>();
		public readonly List<CaptureSound> ListSounds=new List<CaptureSound>();
		public readonly List<string> ListLogFile=new List<string>();
		public readonly CaptureSessionSetup Setup;
		public long PatNumCur=PatNum;
		///<summary>Configures each new fake session before the controller uses it.</summary>
		public Action<FakeTwainCaptureSession> ActionOnNewSession;
		private CaptureSessionController _controller;

		///<summary>countSlots 0: single-target mode (no mount).</summary>
		public CaptureControllerHarness(int countSlots=3,DateTime? dateMount=null) {
			Setup=new CaptureSessionSetup {
				Patient=Pat(),
				FuncGetPatNumCur=() => PatNumCur,
				ImagingDevice=Device(),
				CategoryDefNum=20,
				ProvNumDefault=4,
				ComputerName="OPERATORY1",
				SessionGuid=new Guid("11111111-2222-3333-4444-555555555555"),
				UnassignedText=null,
			};
			Setup.Options.ImagingDeviceNum=12;
			if(countSlots>0) {
				Mount mount=new Mount { MountNum=7,PatNum=PatNum,Description="FMX",DateCreated=dateMount??Clock.Now.Date.AddHours(9),DocCategory=20 };
				Setup.Mount=mount;
				Setup.MountSlots=new MountSlots(mount,Slots(countSlots),new Document[countSlots+1]);
			}
		}

		public static Patient Pat() {
			return new Patient { PatNum=PatNum,FName="Pat",LName="Test",PriProv=3 };
		}

		public static ImagingDevice Device() {
			return new ImagingDevice { ImagingDeviceNum=12,Description="Intraoral sensor",TwainName="Example Sensor DS",
				DeviceType=EnumImgDeviceType.TwainRadiograph };
		}

		///<summary>A text item (ItemOrder 0) first, then countSlots slots with teeth: slot k has MountItemNum 100+k and teeth from a fixed list.</summary>
		public static List<MountItem> Slots(int countSlots) {
			string[] arrayTeeth={ "1,2,3","4,5","6","7,8,9,10","11","12,13","14,15,16" };
			List<MountItem> listMountItems=new List<MountItem> { new MountItem { MountItemNum=100,MountNum=7,ItemOrder=0,TextShowing="Label",ToothNumbers="" } };
			for(int k=1;k<=countSlots;k++) {
				listMountItems.Add(new MountItem { MountItemNum=100+k,MountNum=7,ItemOrder=k,ToothNumbers=arrayTeeth[(k-1)%arrayTeeth.Length],
					Width=200,Height=300 });
			}
			return listMountItems;
		}

		public CaptureSessionController Controller {
			get {
				if(_controller==null) {
					Create();
				}
				return _controller;
			}
		}

		public FakeTwainCaptureSession Twain => ListSessions.LastOrDefault();

		public CaptureSessionController Create() {
			CaptureSessionServices services=new CaptureSessionServices {
				Store=Store,
				RadiographStore=RadiographStore,
				Clock=Clock,
				FuncCreateSession=() => {
					FakeTwainCaptureSession session=new FakeTwainCaptureSession();
					ActionOnNewSession?.Invoke(session);
					ListSessions.Add(session);
					return session;
				},
				FuncGetUnassignedText=() => "2 unassigned captures on this computer.",
				ActionWriteLog=line => ListLogFile.Add(line),
			};
			_controller=new CaptureSessionController(Setup,services,Dispatcher);
			_controller.SoundRequested+=sound => ListSounds.Add(sound);
			return _controller;
		}

		///<summary>Begin, then 2 s later the banner is armed.</summary>
		public void BeginArmed() {
			Controller.Begin();
			Arm();
		}

		///<summary>Let the 2 s after the banner appeared pass.</summary>
		public void Arm() {
			Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			Controller.OnTick();
		}

		///<summary>RequestStart and run what it posted (the open, then the first enable).</summary>
		public bool Start() {
			bool isStarted=Controller.RequestStart();
			Dispatcher.RunPending();
			return isStarted;
		}

		///<summary>Begin, arm, Start: the source is open and the first enable is posted.</summary>
		public void Started() {
			BeginArmed();
			if(!Start()) {
				throw new InvalidOperationException("Not started: "+Controller.IndicatorText);
			}
		}

		public AcquiredImage Image(AcquiredImageKind kind=AcquiredImageKind.Normal,TwainCaptureStopReason stopReason=TwainCaptureStopReason.None,
			int enableId=0)
		{
			AcquiredImage image=TestImages.Image8(6,4,(x,y) => (byte)(x*40+y));
			Twain.RaiseImage(image,kind,stopReason,enableId);
			Dispatcher.RunPending();
			return image;
		}

		public TwainCaptureEnded Ended(TwainCaptureEndReason reason,Action<TwainCaptureEnded> actionConfigure=null,int enableId=0) {
			TwainCaptureEnded ended=Twain.RaiseEnded(reason,actionConfigure,enableId);
			Dispatcher.RunPending();
			return ended;
		}

		public void Status(TwainCaptureState state,bool isSafeDisableInEffect=false) {
			Twain.RaiseStatus(state,isSafeDisableInEffect);
			Dispatcher.RunPending();
		}

		///<summary>One PerSlot exposure: waiting, the image, then the enable completes.</summary>
		public void Expose() {
			Status(TwainCaptureState.WaitingForImage);
			Image();
			Ended(TwainCaptureEndReason.Completed);
		}

		public CaptureEnableRecord LastRecord => (CaptureEnableRecord)Twain.LastRequest.Tag;

		public bool Execute(CaptureSessionAction action) {
			bool isDone=Controller.Execute(action);
			Dispatcher.RunPending();
			return isDone;
		}

		///<summary>A passed 16-bit verification record for device 12 on OPERATORY1 with the given hash.</summary>
		public static CaptureVerificationRecord PassedRecord(string hash) {
			return new CaptureVerificationRecord {
				ImagingDeviceNum=12,ComputerName="OPERATORY1",SourceName="Example Sensor DS",GatedSettingsHash=hash,
				DateTimeVerified=new DateTime(2026,10,30,9,0,0),BitsRequested=16,BitsDelivered=16,TransferMechanism="Memory",
				ByteOrderCheck="LooksRight",IsByteOrderConfirmed=true,IsPolarityConfirmed=true,PixelFlavor="Chocolate",Polarity="AsDelivered",
			};
		}
	}
}
