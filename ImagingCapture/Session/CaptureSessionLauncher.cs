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
		///<summary>CaptureQuarantine.GetUnassignedText. Used when FuncGetUnassignedInfo is null.</summary>
		public Func<string> FuncGetUnassignedText { get; set; }
		///<summary>CaptureQuarantine.GetUnassignedInfo: the line and whether it has turned into the warning after 30 days (design 7.12
		///"Visibility"), which the window then shows in a warning colour. Null: FuncGetUnassignedText, never overdue.</summary>
		public Func<CaptureUnassignedInfo> FuncGetUnassignedInfo { get; set; }
		///<summary>A line for Capture\capture-log.txt (CaptureLogFile.Write).</summary>
		public Action<string> ActionWriteLog { get; set; }
		///<summary>The session window; null shows none, and the session is not started.</summary>
		public ICaptureSessionWindowRunner WindowRunner { get; set; }
		///<summary>ODEvent, for the patient-change monitor (design 7.6). Null means ODEvent itself (ODEventCapturePatientEvents).</summary>
		public ICapturePatientEvents PatientEvents { get; set; }
		///<summary>Shows the operator a notice and returns when it is closed (text, the request's owner). The launcher uses it only before the
		///session would start, when no source is open, so a message box is allowed: the gated capture settings are not verified (design 9.3) and
		///the legacy Mount/Acquire loop runs instead. Null shows nothing; CreateDefault shows a message box.</summary>
		public Action<string,object> ActionShowNotice { get; set; }
		///<summary>Open Dental's automatic logoff counts this as activity (Security.DateTimeLastActivity=now). Design 7.1: in phase 3 the session
		///notes activity on every enable, received image, key or pedal (also a hotkey while the sensor software has the foreground) and status
		///change, which Open Dental would not see otherwise, so a pedal-only series is not interrupted by a logoff. Null notes nothing.</summary>
		public Action ActionNoteActivity { get; set; }

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
				FuncGetUnassignedInfo=() => CaptureQuarantine.GetUnassignedInfo(),
				ActionWriteLog=text => CaptureLogFile.Write(text),
				WindowRunner=new CaptureSessionWindowRunner(),
				PatientEvents=new ODEventCapturePatientEvents(),
				ActionShowNotice=(text,owner) => OpenDental.MsgBox.Show(text),
				ActionNoteActivity=() => Security.DateTimeLastActivity=DateTime.Now,
			};
		}
	}

	///<summary>The entry point of the capture session (design 6.4, 7.3), called by the ControlImages partial (ControlImages.Acquire.cs,
	///TryAcquireWithCaptureSession) on the UI thread, right after FrmMountAndAcquire. Before anything opens it checks, in this order:
	///[Session] Enabled (the rollback: NotStarted), the device type (TwainRadiograph only: NotStarted otherwise), the hash check of the gated
	///settings (design 9.3: settings changed outside Open Dental, or never saved in the Capture settings screen, mean safe values, and the safe
	///value of [Session] Enabled is off, so NotStarted after a notice to the operator), the patient against the main window's current patient,
	///the ImageCreate permission, the category (design 7.12 "Invalid category"), the device's TWAIN source and the source gate (design 7.12
	///"Source not enabled here"), and the mount's slots. Then it builds the controller and shows the window, which is modal, and returns
	///Completed with the summary when the window has closed.
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
					IsEmptyMountToDelete=IsMountStillEmpty(isMountCreatedHere,controller,request?.Mount),
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
					IsEmptyMountToDelete=IsMountStillEmpty(isMountCreatedHere,controller,request?.Mount),
				};
			}
		}

		///<summary>Design 7.3 "The hook": a mount created in this call is deleted after the session only while it is still empty. The session put no
		///document into it and quarantined nothing, nor tried to (Assign files a quarantined image, or the journal copy of one whose move failed,
		///into its original slot, design 7.12, so the slot must stay),
		///and the database agrees: a document filed into it that the session did not hear of (the quarantine window failing after a filing, another
		///window of this computer) keeps it. Mounts.Delete leaves the documents, which would then point at a deleted mount item. A database that
		///cannot be read keeps it too.</summary>
		private bool IsMountStillEmpty(bool isMountCreatedHere,CaptureSessionController controller,Mount mount) {
			if(!isMountCreatedHere || controller==null || mount==null || !controller.IsMountEmpty) {
				return false;
			}
			if(controller.CountQuarantined>0) {
				Log("The mount "+mount.MountNum+" created for the session is kept: "+controller.CountQuarantined+" image(s) of the session went to unassigned "
					+"captures and may be assigned into its slots.");
				return false;
			}
			if(controller.CountQuarantineAttempts>0) {
				//The move failed (a file conflict, folder rights, a full disk): the image's only copy is its journal copy, whose sidecar names this mount
				//and its slot. Without the mount, Assign would file it unmounted, and a 16-bit capture could not be filed at all (design 7.8).
				Log("The mount "+mount.MountNum+" created for the session is kept: "+controller.CountQuarantineAttempts+" image(s) of the session could not "
					+"be moved to unassigned captures; their journal copies name its slots and may be assigned into them.");
				return false;
			}
			if(controller.IsSourceStillDelivering) {
				//Decided before the images the closing source still delivers reach the controller (posted to this thread) or the quarantine: they
				//name this mount and their slots, and Assign files them there only while the mount exists.
				Log("The mount "+mount.MountNum+" created for the session is kept: the session finished while the source was still enabled or stopping, "
					+"and images it still delivers go to unassigned captures for its slots.");
				return false;
			}
			try {
				List<MountItem> listMountItems=_services.Store.GetMountItems(mount.MountNum)??new List<MountItem>();
				List<Document> listDocuments=_services.Store.GetDocumentsForMountItems(listMountItems)??new List<Document>();
				if(listDocuments.Any(x => x!=null)) {
					Log("The mount "+mount.MountNum+" created for the session is kept: it holds a document the session did not save.");
					return false;
				}
				return true;
			}
			catch(Exception ex) {
				Log("The mount "+mount.MountNum+" created for the session is kept: its documents could not be read ("+ex.Message+").");
				return false;
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
			long imagingDeviceNum=request.ImagingDevice.ImagingDeviceNum;
			string computerName=_services.FuncGetComputerName?.Invoke()??"";
			//Design 9.3: the gated block counts only as last saved in the Capture settings screen. Read once: the same Setup entries hold the
			//Test capture verification records.
			List<SecurityLog> listSetupLogs=null;
			string errorSetupLogs="the security log could not be read";
			try {
				listSetupLogs=_services.Store.GetSetupLogEntries(imagingDeviceNum)??new List<SecurityLog>();
			}
			catch(Exception ex) {
				errorSetupLogs+=": "+ex.Message;
			}
			CaptureSettingsCheck settingsCheck=listSetupLogs==null
				? CaptureSettingsCheck.Unknown(imagingDeviceNum,computerName,errorSetupLogs)
				: CaptureSettingsCheck.Check(config,imagingDeviceNum,computerName,listSetupLogs);
			if(!settingsCheck.IsVerified) {
				//The safe value of [Session] Enabled is off, so the legacy loop runs; the operator is told why, once per Mount/Acquire.
				ShowNotice(settingsCheck.Text+" "+CaptureSessionText.LegacyLoopInstead,request.Owner);
				return CaptureSessionLaunchResult.NotStarted("The gated capture settings are not verified ("+settingsCheck.Status+"): "+settingsCheck.Text);
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
				VerificationRecord=CaptureVerificationRecords.FindLatest(listSetupLogs,imagingDeviceNum),//on any computer (owner decision 2026-11-01)
				GatedSettingsHash=settingsCheck.CurrentHash,
				SettingsCheck=settingsCheck,
				SessionGuid=Guid.NewGuid(),
			};
			CaptureUnassignedInfo unassignedInfo=GetUnassignedInfo();
			setup.UnassignedText=unassignedInfo.Text;
			setup.IsUnassignedOverdue=unassignedInfo.IsOverdue;
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

		///<summary>Shows text through ActionShowNotice; a failure is logged, never thrown.</summary>
		private void ShowNotice(string text,object owner) {
			Log("Notice: "+text);
			try {
				_services.ActionShowNotice?.Invoke(text,owner);
			}
			catch(Exception ex) {
				Log("The notice could not be shown: "+ex.Message);
			}
		}

		private CaptureUnassignedInfo GetUnassignedInfo() {
			return GetUnassignedInfo(_services);
		}

		///<summary>The unassigned-captures line and whether it is the 30-day warning, from FuncGetUnassignedInfo, else FuncGetUnassignedText. Never
		///throws: the count is a convenience.</summary>
		internal static CaptureUnassignedInfo GetUnassignedInfo(CaptureSessionServices services) {
			try {
				if(services?.FuncGetUnassignedInfo!=null) {
					return services.FuncGetUnassignedInfo()??new CaptureUnassignedInfo();
				}
				return new CaptureUnassignedInfo { Text=services?.FuncGetUnassignedText?.Invoke() };
			}
			catch {
				return new CaptureUnassignedInfo();
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
