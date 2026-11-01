// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using ImagingCapture.Save;
using ImagingCapture.Settings;
using ImagingCapture.UI;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using OpenDentBusiness;

namespace ImagingCapture.Session {
	///<summary>What the ControlImages partial passes to the capture session (design 6.4), from ToolBarMountAcquire_Click.</summary>
	public sealed class CaptureSessionLaunchRequest {
		///<summary>ControlImages' patient.</summary>
		public Patient Patient { get; set; }
		///<summary>The PatNum the session is bound to: Patient.PatNum.</summary>
		public long PatNum { get; set; }
		///<summary>Returns FormOpenDental.PatNumCur, checked now and before every enable.</summary>
		public Func<long> FuncGetPatNumCur { get; set; }
		///<summary>The mount showing (new or selected), or null for a single image.</summary>
		public Mount Mount { get; set; }
		///<summary>Not null when the mount was created from this definition in this call (FrmMountAndAcquire); the partial deletes it again when
		///IsEmptyMountToDelete says so.</summary>
		public MountDef MountDef { get; set; }
		public ImagingDevice ImagingDevice { get; set; }
		///<summary>The image category for the documents (the mount's category, or the current one for a single image).</summary>
		public long CategoryDefNum { get; set; }
		///<summary>The window the session window is modal to (a WinForms control or a window handle), passed to the window runner as it is.</summary>
		public object Owner { get; set; }
	}

	///<summary>What the partial does after CaptureSessionLauncher.Launch.</summary>
	public enum CaptureSessionLaunchKind {
		///<summary>The capture session did not run, and the legacy Mount/Acquire loop runs unchanged: the rollback ([Session] Enabled=0, the
		///default), a device type the session does not drive (TwainMulti keeps its batch path), or no session window in this build.</summary>
		NotStarted,
		///<summary>The session refused to start (Reason): the partial shows Reason and returns; the legacy loop does not run.</summary>
		Refused,
		///<summary>The session window ran; Summary says what it did. The partial refreshes the module (design 7.3 step 6).</summary>
		Completed,
	}

	public sealed class CaptureSessionLaunchResult {
		public CaptureSessionLaunchKind Kind { get; set; }
		///<summary>Refused: the sentence to show. NotStarted: why, for the log. Completed: "" unless the window failed while the session ran
		///(the partial shows it).</summary>
		public string Reason { get; set; }="";
		///<summary>Completed: what the session did.</summary>
		public CaptureSessionSummary Summary { get; set; }
		///<summary>The mount was created in this call (MountDef not null) and holds no image: the partial deletes it and logs that under ImageCreate
		///(design 7.3 "The hook"). Always false for NotStarted, where the legacy loop uses the mount.</summary>
		public bool IsEmptyMountToDelete { get; set; }

		public static CaptureSessionLaunchResult NotStarted(string reason) {
			return new CaptureSessionLaunchResult { Kind=CaptureSessionLaunchKind.NotStarted,Reason=reason??"" };
		}

		public static CaptureSessionLaunchResult Refused(string reason,bool isEmptyMountToDelete) {
			return new CaptureSessionLaunchResult { Kind=CaptureSessionLaunchKind.Refused,Reason=reason??"",IsEmptyMountToDelete=isEmptyMountToDelete };
		}
	}

	///<summary>Shows the capture session window, modal to its owner, and returns when it has closed. The window calls controller.Begin when it
	///has loaded and controller.OnClosing when it closes (both on the UI thread), and closes itself after SessionFinished. CaptureSessionWindowRunner
	///(ImagingCapture.UI) shows FrmCaptureSession; without a runner the launcher reports NotStarted.</summary>
	public interface ICaptureSessionWindowRunner {
		void ShowModal(CaptureSessionController controller,object owner);
	}

	///<summary>The capture session's connections to the rest of the program, so that the launcher and the controller can be tested with
	///stand-ins. CreateDefault gives the real ones.</summary>
	public sealed class CaptureSessionServices {
		public ICaptureSessionStore Store { get; set; }
		public IRadiographStore RadiographStore { get; set; }
		///<summary>A new TWAIN session (TwainCaptureSession).</summary>
		public Func<ITwainCaptureSession> FuncCreateSession { get; set; }
		///<summary>A dispatcher for the calling UI thread (SynchronizationContextCaptureDispatcher); called on that thread.</summary>
		public Func<ICaptureDispatcher> FuncCreateDispatcher { get; set; }
		public ICaptureClock Clock { get; set; }
		///<summary>capture.ini (LocalCaptureConfig.Load).</summary>
		public Func<LocalCaptureConfig> FuncLoadConfig { get; set; }
		///<summary>This computer (ODEnvironment.MachineName).</summary>
		public Func<string> FuncGetComputerName { get; set; }
		///<summary>CaptureQuarantine.GetUnassignedText.</summary>
		public Func<string> FuncGetUnassignedText { get; set; }
		///<summary>A line for Capture\capture-log.txt (CaptureLogFile.Write).</summary>
		public Action<string> ActionWriteLog { get; set; }
		///<summary>The session window; null shows none, and the session is not started.</summary>
		public ICaptureSessionWindowRunner WindowRunner { get; set; }
		///<summary>ODEvent, for the patient-change monitor (design 7.6). Null means ODEvent itself (ODEventCapturePatientEvents).</summary>
		public ICapturePatientEvents PatientEvents { get; set; }

		///<summary>OpenDentBusiness, TwainCaptureSession, the UI thread's SynchronizationContext, capture.ini, the capture log and the session window
		///(FrmCaptureSession through CaptureSessionWindowRunner).</summary>
		public static CaptureSessionServices CreateDefault() {
			return new CaptureSessionServices {
				Store=new OdCaptureSessionStore(),
				RadiographStore=new OdRadiographStore(),
				FuncCreateSession=() => new TwainCaptureSession(),
				FuncCreateDispatcher=() => new SynchronizationContextCaptureDispatcher(),
				Clock=new SystemCaptureClock(),
				FuncLoadConfig=() => LocalCaptureConfig.Load(),
				FuncGetComputerName=() => ODEnvironment.MachineName,
				FuncGetUnassignedText=() => CaptureQuarantine.GetUnassignedText(),
				ActionWriteLog=text => CaptureLogFile.Write(text),
				WindowRunner=new CaptureSessionWindowRunner(),
				PatientEvents=new ODEventCapturePatientEvents(),
			};
		}
	}

	///<summary>The entry point of the capture session (design 6.4, 7.3), called by the ControlImages partial (ControlImages.Acquire.cs,
	///TryAcquireWithCaptureSession) on the UI thread, right after FrmMountAndAcquire. Before anything opens it checks, in this order:
	///[Session] Enabled (the rollback: NotStarted), the device type (TwainRadiograph only: NotStarted otherwise), the patient against the main
	///window's current patient, the ImageCreate permission, the category (design 7.12 "Invalid category"), the device's TWAIN source and the
	///source gate (design 7.12 "Source not enabled here"), and the mount's slots. Then it builds the controller and shows the window, which is
	///modal, and returns Completed with the summary when the window has closed.
	///
	///A mount created in this call (MountDef not null) that is still empty when the launcher returns Refused or Completed is reported in
	///IsEmptyMountToDelete; the partial deletes it and logs that (design 7.3 "The hook"). An unexpected exception before the window opens gives
	///Refused with its message, not NotStarted: the operator expected the session, so the legacy loop does not run instead without a word.</summary>
	public sealed class CaptureSessionLauncher {
		private readonly CaptureSessionServices _services;

		public CaptureSessionLauncher(CaptureSessionServices services) {
			_services=services??throw new ArgumentNullException(nameof(services));
		}

		///<summary>Launch with CaptureSessionServices.CreateDefault().</summary>
		public static CaptureSessionLaunchResult Launch(CaptureSessionLaunchRequest request) {
			return new CaptureSessionLauncher(CaptureSessionServices.CreateDefault()).Run(request);
		}

		///<summary>Runs the checks and, when they pass, the session window. Never throws.</summary>
		public CaptureSessionLaunchResult Run(CaptureSessionLaunchRequest request) {
			bool isMountCreatedHere=request?.MountDef!=null && request.Mount!=null;
			CaptureSessionController controller=null;
			try {
				CaptureSessionSetup setup;
				CaptureSessionLaunchResult resultRefused=Prepare(request,out setup);
				if(resultRefused!=null) {
					Log("Not started for patient "+(request?.PatNum??0)+": "+resultRefused.Kind+", "+resultRefused.Reason);
					return resultRefused;
				}
				ICaptureDispatcher dispatcher=_services.FuncCreateDispatcher();
				controller=new CaptureSessionController(setup,_services,dispatcher);
				_services.WindowRunner.ShowModal(controller,request.Owner);
				if(controller.Phase!=CaptureSessionPhase.Finished) {
					controller.OnClosing();//the window closed without telling the controller
				}
				return new CaptureSessionLaunchResult {
					Kind=CaptureSessionLaunchKind.Completed,
					Summary=controller.Summary,
					IsEmptyMountToDelete=isMountCreatedHere && controller.IsMountEmpty,
				};
			}
			catch(Exception ex) {
				Log("The capture session failed: "+ex);
				if(controller==null) {
					return CaptureSessionLaunchResult.Refused("The capture session could not start: "+ex.Message,isMountCreatedHere);
				}
				//The window failed while the session ran: the source closes in the safe order, and what was saved stays (the mount is kept
				//unless it is still empty).
				try {
					if(controller.Phase!=CaptureSessionPhase.Finished) {
						controller.OnClosing();
					}
				}
				catch(Exception exClose) {
					Log("The capture session could not be closed: "+exClose.Message);
				}
				return new CaptureSessionLaunchResult {
					Kind=CaptureSessionLaunchKind.Completed,
					Reason="The capture session window failed: "+ex.Message,
					Summary=controller.Summary,
					IsEmptyMountToDelete=isMountCreatedHere && controller.IsMountEmpty,
				};
			}
		}

		///<summary>The checks before anything opens. Returns null with setup filled when the session may start, otherwise the result to return.</summary>
		private CaptureSessionLaunchResult Prepare(CaptureSessionLaunchRequest request,out CaptureSessionSetup setup) {
			setup=null;
			bool isMountCreatedHere=request?.MountDef!=null && request.Mount!=null;
			if(request==null || request.ImagingDevice==null) {
				return CaptureSessionLaunchResult.NotStarted("No imaging device.");
			}
			LocalCaptureConfig config=_services.FuncLoadConfig();
			CaptureSessionConfig sessionConfig=CaptureSessionConfig.Read(config);
			if(!sessionConfig.IsEnabled) {
				return CaptureSessionLaunchResult.NotStarted("[Session] Enabled is 0 on this computer: the legacy Mount/Acquire loop runs.");
			}
			if(request.ImagingDevice.DeviceType!=EnumImgDeviceType.TwainRadiograph) {
				return CaptureSessionLaunchResult.NotStarted("The capture session drives TwainRadiograph devices only; "+request.ImagingDevice.DeviceType
					+" keeps the legacy path.");
			}
			if(_services.WindowRunner==null) {
				return CaptureSessionLaunchResult.NotStarted("The capture session window is not available in this build.");
			}
			//Design 6.4, 10.6: the session is bound to an explicit patient, the one the main window shows.
			if(request.Patient==null || request.PatNum==0 || request.Patient.PatNum!=request.PatNum) {
				return CaptureSessionLaunchResult.Refused("Select a patient first.",isMountCreatedHere);
			}
			long patNumCur=request.FuncGetPatNumCur==null ? 0 : request.FuncGetPatNumCur();
			if(patNumCur!=request.PatNum) {
				return CaptureSessionLaunchResult.Refused("The patient in the main window is not "+request.Patient.GetNameFL()
					+" any more. Select the patient again before taking X-rays.",isMountCreatedHere);
			}
			if(request.Mount!=null && request.Mount.PatNum!=request.PatNum) {
				return CaptureSessionLaunchResult.Refused("The mount belongs to another patient.",isMountCreatedHere);
			}
			if(!_services.Store.IsAuthorized(EnumPermType.ImageCreate)) {
				return CaptureSessionLaunchResult.Refused("Not authorized for Image Create.",isMountCreatedHere);
			}
			if(request.CategoryDefNum==0 || !_services.Store.IsCategoryValid(request.CategoryDefNum)) {
				return CaptureSessionLaunchResult.Refused("Image category missing; choose a category.",isMountCreatedHere);//design 7.12
			}
			string twainName=(request.ImagingDevice.TwainName??"").Trim();
			if(twainName=="") {
				return CaptureSessionLaunchResult.Refused("The imaging device '"+(request.ImagingDevice.Description??"")+"' has no TWAIN source. Check "
					+"Setup > Imaging > Devices.",isMountCreatedHere);
			}
			long imagingDeviceNum=request.ImagingDevice.ImagingDeviceNum;
			TwainCaptureOptions options=TwainCaptureOptions.FromConfig(config,imagingDeviceNum);
			if(!options.IsSourceAllowed(twainName)) {
				return CaptureSessionLaunchResult.Refused(CaptureEndText.SourceNotEnabledHere,isMountCreatedHere);
			}
			foreach(string warning in options.ListWarnings) {
				Log("capture.ini: "+warning);
			}
			MountSlots mountSlots=null;
			if(request.Mount!=null) {
				List<MountItem> listMountItems=_services.Store.GetMountItems(request.Mount.MountNum)??new List<MountItem>();
				List<Document> listDocuments=_services.Store.GetDocumentsForMountItems(listMountItems)??new List<Document>();
				mountSlots=new MountSlots(request.Mount,listMountItems,listDocuments);
				if(mountSlots.Target==null) {
					bool hasSlots=listMountItems.Any(x => MountPlacement.IsSlot(x));
					return CaptureSessionLaunchResult.Refused(hasSlots
						? "Every slot of the mount '"+(request.Mount.Description??"")+"' already has an image. Select a category or another mount."
						: "The mount '"+(request.Mount.Description??"")+"' has no slot for an image.",
						isMountCreatedHere && listDocuments.All(x => x==null));
				}
			}
			string computerName=_services.FuncGetComputerName?.Invoke()??"";
			setup=new CaptureSessionSetup {
				Patient=request.Patient,
				FuncGetPatNumCur=request.FuncGetPatNumCur,
				Mount=request.Mount,
				MountDef=request.MountDef,
				MountSlots=mountSlots,
				ImagingDevice=request.ImagingDevice,
				CategoryDefNum=request.CategoryDefNum,
				ProvNumDefault=GetDefaultProvNum(request.Patient),
				Config=config,
				Options=options,
				SessionConfig=sessionConfig,
				ComputerName=computerName,
				VerificationRecord=GetVerificationRecord(imagingDeviceNum,computerName),
				GatedSettingsHash=GetGatedSettingsHash(config,imagingDeviceNum),
				UnassignedText=GetUnassignedText(),
				SessionGuid=Guid.NewGuid(),
			};
			return null;
		}

		private long GetDefaultProvNum(Patient patient) {
			try {
				return _services.Store.GetDefaultProvNum(patient);
			}
			catch(Exception ex) {
				Log("The default provider could not be read: "+ex.Message);
				return patient.PriProv;
			}
		}

		///<summary>The latest verification record for the device on this computer, or null (then no 16 bits).</summary>
		private CaptureVerificationRecord GetVerificationRecord(long imagingDeviceNum,string computerName) {
			try {
				return CaptureVerificationRecords.FindLatest(_services.Store.GetSetupLogEntries(imagingDeviceNum),imagingDeviceNum,computerName);
			}
			catch(Exception ex) {
				Log("The Test capture verification records could not be read: "+ex.Message);
				return null;
			}
		}

		private string GetGatedSettingsHash(LocalCaptureConfig config,long imagingDeviceNum) {
			try {
				return GatedSettingsHash.Compute(config,imagingDeviceNum);
			}
			catch(Exception ex) {
				Log("The gated capture settings could not be hashed: "+ex.Message);
				return "";
			}
		}

		private string GetUnassignedText() {
			try {
				return _services.FuncGetUnassignedText?.Invoke();
			}
			catch {
				return null;
			}
		}

		private void Log(string text) {
			try {
				_services.ActionWriteLog?.Invoke("Capture session: "+text);
			}
			catch {
			}
		}
	}
}
