// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ImagingCapture.Save;
using ImagingCapture.Session;
using ImagingCapture.Templates;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;
using OpenDentBusiness;

namespace ImagingCapture.Tests.Db {
	///<summary>A capture session on the real database: the launcher with OdCaptureSessionStore and OdRadiographStore, and stand-ins only for what
	///needs hardware or a window (the TWAIN worker, the session window, the UI thread's dispatcher, the clock, ODEvent). A provider, a patient,
	///an X-ray category, an imaging device, a 4BW horizontal mount definition and this test's capture.ini, whose settings a Setup user saved
	///in Capture settings with [Session] Enabled=1, so the session may start.</summary>
	internal sealed class CaptureSessionDbKit:IDisposable {
		public readonly string Folder=Path.Combine(Path.GetTempPath(),"capdb_session_"+Guid.NewGuid().ToString("N"));
		public readonly FakeCaptureClock Clock=new FakeCaptureClock();
		public readonly QueueCaptureDispatcher Dispatcher=new QueueCaptureDispatcher();
		public readonly FakeWindowRunner WindowRunner=new FakeWindowRunner();
		public readonly FakeCapturePatientEvents PatientEvents=new FakeCapturePatientEvents();
		public readonly List<FakeTwainCaptureSession> ListSessions=new List<FakeTwainCaptureSession>();
		public readonly List<string> ListLog=new List<string>();
		public readonly List<string> ListNotices=new List<string>();
		public readonly long ProvNum;
		public readonly Patient Patient;
		public readonly Def Category;
		public readonly ImagingDevice ImagingDevice;
		public readonly MountDef MountDef4Bw;
		public long PatNumCur;
		///<summary>ControlImages' _patient: the patient the Imaging module shows. A window script sets it (with PatNumCur) to stand for the main
		///window changing patient while the session window is open; null for no patient (a logoff).</summary>
		public Patient PatientShown;
		private int _countImages;

		public CaptureSessionDbKit(string tag,bool isSettingsSaved=true) {
			Clock.Now=DateTime.Now;//the mount is created today: no "older mount" question
			ProvNum=CaptureDb.CreateProvider(tag);
			Patient=CaptureDb.CreatePatient(tag,ProvNum);
			PatNumCur=Patient.PatNum;
			PatientShown=Patient;
			Category=CaptureDb.CreateImageCategory(tag+" X-rays");
			ImagingDevice=CaptureDb.CreateImagingDevice(tag);
			MountDef4Bw=CaptureDb.CreateMountDefFromTemplate(StandardTemplateKind.FourBitewingsHorizontal,Category.DefNum,tag);
			if(isSettingsSaved) {
				CaptureDb.SaveSettings(ImagingDevice,Folder,"Session.Enabled=1");
			}
			else {
				CaptureDb.WriteCaptureIni(Folder,"[Session]","Enabled=1");
			}
		}

		public FakeTwainCaptureSession Twain => ListSessions.LastOrDefault();

		///<summary>The launcher on the real stores; the window is WindowRunner (its ActionWhileShown drives the controller).</summary>
		public CaptureSessionLauncher Launcher() {
			return new CaptureSessionLauncher(new CaptureSessionServices {
				Store=new OdCaptureSessionStore(),
				RadiographStore=new OdRadiographStore(),
				FuncCreateSession=() => {
					FakeTwainCaptureSession session=new FakeTwainCaptureSession();
					session.OpenResult.SourceInfo.ProductName=CaptureDb.TwainName;
					ListSessions.Add(session);
					return session;
				},
				FuncCreateDispatcher=() => Dispatcher,
				Clock=Clock,
				FuncLoadConfig=() => CaptureDb.LoadCaptureIni(Folder),
				FuncGetComputerName=() => CaptureDb.ComputerName,
				FuncGetUnassignedText=() => null,
				ActionWriteLog=line => ListLog.Add(line),
				WindowRunner=WindowRunner,
				PatientEvents=PatientEvents,
				ActionShowNotice=(text,owner) => ListNotices.Add(text),
				ActionNoteActivity=() => { },
			});
		}

		///<summary>The new mount from the 4BW definition, as ControlImages makes it right before the hook.</summary>
		public Mount CreateMount4Bw() {
			return CaptureDb.CreateMountFromDef(MountDef4Bw,Patient,Category.DefNum);
		}

		///<summary>The hook's partial with this kit's launcher: mountDef is the definition picked in FrmMountAndAcquire (null for an existing mount).</summary>
		public CaptureHookOutcome RunHook(Mount mount,MountDef mountDef) {
			return CaptureHookMirror.TryAcquireWithCaptureSession(Launcher(),() => PatientShown,mount,mountDef,ImagingDevice,Category.DefNum,() => PatNumCur);
		}

		///<summary>The main window changes to patient (null: no patient, as after a logoff that selects none) while the session window is open:
		///PatNumCur and ControlImages' _patient change, and ODEvent Patient is raised (FormOpenDental.PatNumCur's setter).</summary>
		public void ChangePatientShown(Patient patient) {
			PatNumCur=patient?.PatNum??0;
			PatientShown=patient;
			PatientEvents.FirePatient(PatNumCur);
			Dispatcher.RunPending();
		}

		#region Driving the controller inside the window
		///<summary>The window has loaded (Begin), 2 s pass, and the operator presses Start: the source opens and the first enable is posted.</summary>
		public void Start(CaptureSessionController controller) {
			controller.Begin();
			Clock.Advance(CaptureSessionController.KeyIgnoreMs);
			controller.OnTick();
			if(!controller.RequestStart()) {
				throw new InvalidOperationException("Not started: "+controller.IndicatorText);
			}
			Dispatcher.RunPending();
		}

		///<summary>One exposure of the running per-slot enable: waiting, the image (grey value grey, one second after the previous image, with a
		///captureId), then the enable completes.</summary>
		public AcquiredImage Expose(byte grey) {
			Status(TwainCaptureState.WaitingForImage);
			AcquiredImage image=Image(grey);
			Ended(TwainCaptureEndReason.Completed);
			return image;
		}

		public AcquiredImage Image(byte grey) {
			_countImages++;
			string captureId="capdb-"+_countImages.ToString(CultureInfo.InvariantCulture)+"-"+CaptureDb.NewSuffix();
			string metadata="{\"schema\":\"capture-metadata/1\",\"captureId\":\""+captureId+"\",\"sequence\":"+_countImages.ToString(CultureInfo.InvariantCulture)
				+"}";
			AcquiredImage image=TestImages.Image8(6,4,(x,y) => grey,metadata);
			image.SourceName=CaptureDb.TwainName;
			image.ReceivedAt=new DateTime(2026,10,31,10,0,0).AddSeconds(10*_countImages);
			Twain.RaiseImage(image);
			Dispatcher.RunPending();
			return image;
		}

		public void Status(TwainCaptureState state) {
			Twain.RaiseStatus(state);
			Dispatcher.RunPending();
		}

		public void Ended(TwainCaptureEndReason reason) {
			Twain.RaiseEnded(reason);
			Dispatcher.RunPending();
		}

		public bool Execute(CaptureSessionController controller,CaptureSessionAction action) {
			bool isDone=controller.Execute(action);
			Dispatcher.RunPending();
			return isDone;
		}
		#endregion Driving the controller inside the window

		public void Dispose() {
			try {
				Directory.Delete(Folder,true);
			}
			catch {
			}
		}
	}
}
