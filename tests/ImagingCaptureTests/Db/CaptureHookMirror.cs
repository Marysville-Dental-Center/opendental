// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Session;
using OpenDentBusiness;

namespace ImagingCapture.Tests.Db {
	///<summary>What the hook's partial did, for the tests.</summary>
	internal sealed class CaptureHookOutcome {
		///<summary>TryAcquireWithCaptureSession's return value: false means the legacy Mount/Acquire loop runs.</summary>
		public bool IsHandled;
		public CaptureSessionLaunchResult Result;
		public bool IsMountDeleted;
		///<summary>isSamePatient after the session: the module still showed the bound patient (only set when the session ran or refused).</summary>
		public bool IsSamePatient;
		///<summary>DeleteEmptyMountAfterSession cleared the deleted mount from the module (FillImageSelector, SelectTreeNode1(null)).</summary>
		public bool IsDisplayClearedAfterDelete;
		///<summary>What RefreshAfterCaptureSession selected in the module: "Mount n" or "Document n", "" for nothing, null when it did not run.</summary>
		public string NodeSelectedAfter;
		///<summary>RefreshAfterCaptureSession called ThumbnailRefresh, which writes the mount's thumbnail into the module's patient folder.</summary>
		public bool IsThumbnailRefreshed;
		///<summary>The texts the partial would show in a message box, in order.</summary>
		public List<string> ListMessages=new List<string>();
	}

	///<summary>The database path of OpenDental/Main Modules/ControlImages.Acquire.cs (TryAcquireWithCaptureSession, DeleteEmptyMountAfterSession
	///and RefreshAfterCaptureSession), statement for statement, with the module's display calls (FillImageSelector, SelectTreeNode1,
	///ThumbnailRefresh, LayoutControls), which need the Imaging module, recorded in the outcome instead. OpenDental.exe is a WinForms program
	///these tests cannot load, so the partial's business calls run from here; CaptureHookDbTests.Mirror_MatchesThePartial pins every statement
	///copied here to the partial's text, so the two cannot drift apart unnoticed. funcGetPatient stands for ControlImages' _patient field, read
	///before the session and again after it (the module may have been refreshed for another patient, or none, while the modal session ran); the
	///launcher is the one under test instead of CaptureSessionLauncher.Launch (CreateDefault would open the real session window and the TWAIN
	///library).</summary>
	internal static class CaptureHookMirror {
		///<summary>TryAcquireWithCaptureSession(imagingDevice,mountDef) with mount as GetMountShowing() (null for no mount showing) and
		///categoryCurrent as GetCurrentCategory().</summary>
		public static CaptureHookOutcome TryAcquireWithCaptureSession(CaptureSessionLauncher launcher,Func<Patient> funcGetPatient,Mount mount,
			MountDef mountDef,ImagingDevice imagingDevice,long categoryCurrent,Func<long> funcGetPatNumCur)
		{
			CaptureHookOutcome outcome=new CaptureHookOutcome();
			Patient _patient=funcGetPatient();
			if(_patient==null){
				return outcome;
			}
			Patient patientBound=_patient;
			if(mountDef==null || mount==null){
				mountDef=null;//only a mount made from the definition in this call may be deleted
			}
			CaptureSessionLaunchRequest request=new CaptureSessionLaunchRequest{
				Patient=patientBound,
				PatNum=patientBound.PatNum,
				FuncGetPatNumCur=funcGetPatNumCur,
				Mount=mount,
				MountDef=mountDef,
				ImagingDevice=imagingDevice,
				CategoryDefNum=mount==null ? categoryCurrent : mount.DocCategory,
				Owner="owner",
			};
			CaptureSessionLaunchResult result=launcher.Run(request);
			outcome.Result=result;
			if(result.Kind==CaptureSessionLaunchKind.NotStarted){
				return outcome;
			}
			outcome.IsHandled=true;
			_patient=funcGetPatient();//the field after the modal session
			bool isSamePatient=_patient!=null && _patient.PatNum==patientBound.PatNum && funcGetPatNumCur()==patientBound.PatNum;
			outcome.IsSamePatient=isSamePatient;
			bool isMountDeleted=result.IsEmptyMountToDelete && mount!=null && DeleteEmptyMountAfterSession(patientBound,mount,result,isSamePatient,outcome);
			outcome.IsMountDeleted=isMountDeleted;
			if(result.Kind==CaptureSessionLaunchKind.Refused){
				if(!isMountDeleted){
					SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,patientBound.PatNum,"Capture session refused: "+result.Reason);
				}
				outcome.ListMessages.Add(result.Reason);
				return outcome;
			}
			RefreshAfterCaptureSession(isMountDeleted ? null : mount,result.Summary,isSamePatient,outcome);
			if(result.Reason!=""){
				outcome.ListMessages.Add(result.Reason);
			}
			return outcome;
		}

		///<summary>DeleteEmptyMountAfterSession(patientBound,mount,result,isSamePatient).</summary>
		public static bool DeleteEmptyMountAfterSession(Patient patientBound,Mount mount,CaptureSessionLaunchResult result,bool isSamePatient,
			CaptureHookOutcome outcome)
		{
			try{
				List<MountItem> listMountItems=MountItems.GetItemsForMount(mount.MountNum);
				Document[] arrayDocuments=Documents.GetDocumentsForMountItems(listMountItems);
				if(arrayDocuments.Any(x => x!=null)){
					SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,patientBound.PatNum,"Mount kept: "+mount.Description
						+", created for a capture session, holds a document after the session, so it was not deleted.");
					return false;
				}
				foreach(ImageDraw imageDraw in ImageDraws.RefreshForMount(mount.MountNum)){
					ImageDraws.Delete(imageDraw.ImageDrawNum);//the scale Mounts.CreateMountFromDef added for a definition with a ScaleValue
				}
				Mounts.Delete(mount);//also deletes its mount items; it has no documents (checked just now)
			}
			catch(Exception ex){
				outcome.ListMessages.Add("The empty mount could not be deleted:"+" "+ex.Message);
				return false;
			}
			Def defDocCategory=Defs.GetDef(DefCat.ImageCats,mount.DocCategory);
			string why=result.Kind==CaptureSessionLaunchKind.Refused
				? "the capture session refused to start: "+result.Reason
				: "the capture session saved no image";
			string logText="Mount Deleted: "+mount.Description+" with category "+(defDocCategory?.ItemName??"")+", "+why;
			SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,patientBound.PatNum,logText);
			if(isSamePatient){
				outcome.IsDisplayClearedAfterDelete=true;//FillImageSelector(false); SelectTreeNode1(null);
			}
			return true;
		}

		///<summary>RefreshAfterCaptureSession(mount,summary,isSamePatient): what it selects and whether it refreshes the thumbnail.</summary>
		public static void RefreshAfterCaptureSession(Mount mount,CaptureSessionSummary summary,bool isSamePatient,CaptureHookOutcome outcome){
			outcome.NodeSelectedAfter="";//_isAcquiring=false; SetUnmountedBarVisibility(); LayoutControls(); FillImageSelector(false);
			if(!isSamePatient){
				return;
			}
			if(mount!=null){
				outcome.NodeSelectedAfter="Mount "+mount.MountNum;
			}
			else if(summary!=null && summary.ListDocNums.Count>0){
				outcome.NodeSelectedAfter="Document "+summary.ListDocNums.Last();
			}
			outcome.IsThumbnailRefreshed=true;
		}

		///<summary>The partial's statements this mirror copies, exactly as they are in ControlImages.Acquire.cs (whitespace aside). The mirror
		///differs only where it has to: the launcher, _patient and FuncGetPatNumCur are passed in, and the message boxes and display calls are
		///recorded.</summary>
		public static readonly string[] ArrayStatementsCopied={
			"if(_patient==null){",
			"Patient patientBound=_patient;",
			"if(mountDef==null || mount==null){",
			"mountDef=null;",
			"Patient=patientBound,",
			"PatNum=patientBound.PatNum,",
			"FuncGetPatNumCur=()=>FormOpenDental.PatNumCur,",
			"Mount=mount,",
			"MountDef=mountDef,",
			"ImagingDevice=imagingDevice,",
			"CategoryDefNum=mount==null ? GetCurrentCategory() : mount.DocCategory,",
			"CaptureSessionLaunchResult result=CaptureSessionLauncher.Launch(request);",
			"if(result.Kind==CaptureSessionLaunchKind.NotStarted){",
			"bool isSamePatient=_patient!=null && _patient.PatNum==patientBound.PatNum && FormOpenDental.PatNumCur==patientBound.PatNum;",
			"bool isMountDeleted=result.IsEmptyMountToDelete && mount!=null && DeleteEmptyMountAfterSession(patientBound,mount,result,isSamePatient);",
			"if(result.Kind==CaptureSessionLaunchKind.Refused){",
			"if(!isMountDeleted){",
			"SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,patientBound.PatNum,\"Capture session refused: \"+result.Reason);",
			"MsgBox.Show(result.Reason);",
			"RefreshAfterCaptureSession(isMountDeleted ? null : mount,result.Summary,isSamePatient);",
			"if(result.Reason!=\"\"){",
			"private bool DeleteEmptyMountAfterSession(Patient patientBound,Mount mount,CaptureSessionLaunchResult result,bool isSamePatient){",
			"List<MountItem> listMountItems=MountItems.GetItemsForMount(mount.MountNum);",
			"Document[] arrayDocuments=Documents.GetDocumentsForMountItems(listMountItems);",
			"if(arrayDocuments.Any(x => x!=null)){",
			"SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,patientBound.PatNum,\"Mount kept: \"+mount.Description",
			"+\", created for a capture session, holds a document after the session, so it was not deleted.\");",
			"foreach(ImageDraw imageDraw in ImageDraws.RefreshForMount(mount.MountNum)){",
			"ImageDraws.Delete(imageDraw.ImageDrawNum);",
			"Mounts.Delete(mount);",
			"MsgBox.Show(Lan.g(this,\"The empty mount could not be deleted:\")+\" \"+ex.Message);",
			"Def defDocCategory=Defs.GetDef(DefCat.ImageCats,mount.DocCategory);",
			"string why=result.Kind==CaptureSessionLaunchKind.Refused",
			"? \"the capture session refused to start: \"+result.Reason",
			": \"the capture session saved no image\";",
			"string logText=\"Mount Deleted: \"+mount.Description+\" with category \"+(defDocCategory?.ItemName??\"\")+\", \"+why;",
			"SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,patientBound.PatNum,logText);",
			"if(isSamePatient){",
			"private void RefreshAfterCaptureSession(Mount mount,CaptureSessionSummary summary,bool isSamePatient){",
			"if(!isSamePatient){",
			"if(mount!=null){",
			"SelectTreeNode1(new NodeTypeAndKey(EnumImageNodeType.Mount,mount.MountNum));",
			"else if(summary!=null && summary.ListDocNums.Count>0){",
			"SelectTreeNode1(new NodeTypeAndKey(EnumImageNodeType.Document,summary.ListDocNums.Last()));",
			"ThumbnailRefresh();",
		};

		///<summary>How many statements of each kind the partial has: the mirror's copies would miss one the partial added. _patient.PatNum is read
		///once, in isSamePatient: everything else uses patientBound, because _patient may be another patient, or null, after the modal session.</summary>
		public static readonly Dictionary<string,int> DictCallCounts=new Dictionary<string,int> {
			{ "SecurityLogs.MakeLogEntry(",3 },
			{ "Mounts.Delete(",1 },
			{ "ImageDraws.",2 },
			{ "MountItems.",1 },
			{ "Documents.",1 },
			{ "CaptureSessionLauncher.",1 },
			{ "Mounts.Update(",0 },
			{ "Documents.Update(",0 },
			{ "ImageStore.",0 },
			{ "_patient.PatNum",1 },
			{ "_patient.",1 },
			{ "SelectTreeNode1(",3 },
			{ "ThumbnailRefresh(",1 },
			{ "FillImageSelector(",2 },
			{ "SetCropPanAdj(",1 },
		};
	}
}
