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
		///<summary>The texts the partial would show in a message box, in order.</summary>
		public List<string> ListMessages=new List<string>();
	}

	///<summary>The database path of OpenDental/Main Modules/ControlImages.Acquire.cs (TryAcquireWithCaptureSession and
	///DeleteEmptyMountAfterSession), statement for statement, without the module's display calls (FillImageSelector, SelectTreeNode1,
	///ThumbnailRefresh, LayoutControls), which need the Imaging module. OpenDental.exe is a WinForms program these tests cannot load, so the
	///partial's business calls run from here; CaptureHookDbTests.Mirror_MatchesThePartial pins every statement copied here to the partial's text,
	///so the two cannot drift apart unnoticed. _patient is the patient argument; the launcher is the one under test instead of
	///CaptureSessionLauncher.Launch (CreateDefault would open the real session window and the TWAIN library).</summary>
	internal static class CaptureHookMirror {
		///<summary>TryAcquireWithCaptureSession(imagingDevice,mountDef) with mount as GetMountShowing() (null for no mount showing) and
		///categoryCurrent as GetCurrentCategory().</summary>
		public static CaptureHookOutcome TryAcquireWithCaptureSession(CaptureSessionLauncher launcher,Patient _patient,Mount mount,MountDef mountDef,
			ImagingDevice imagingDevice,long categoryCurrent,Func<long> funcGetPatNumCur)
		{
			CaptureHookOutcome outcome=new CaptureHookOutcome();
			if(_patient==null){
				return outcome;
			}
			if(mountDef==null || mount==null){
				mountDef=null;//only a mount made from the definition in this call may be deleted
			}
			CaptureSessionLaunchRequest request=new CaptureSessionLaunchRequest{
				Patient=_patient,
				PatNum=_patient.PatNum,
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
			bool isMountDeleted=result.IsEmptyMountToDelete && mount!=null && DeleteEmptyMountAfterSession(_patient,mount,result,outcome);
			outcome.IsMountDeleted=isMountDeleted;
			if(result.Kind==CaptureSessionLaunchKind.Refused){
				if(!isMountDeleted){
					SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,_patient.PatNum,"Capture session refused: "+result.Reason);
				}
				outcome.ListMessages.Add(result.Reason);
				return outcome;
			}
			if(result.Reason!=""){
				outcome.ListMessages.Add(result.Reason);
			}
			return outcome;
		}

		///<summary>DeleteEmptyMountAfterSession(mount,result).</summary>
		public static bool DeleteEmptyMountAfterSession(Patient _patient,Mount mount,CaptureSessionLaunchResult result,CaptureHookOutcome outcome){
			try{
				List<MountItem> listMountItems=MountItems.GetItemsForMount(mount.MountNum);
				Document[] arrayDocuments=Documents.GetDocumentsForMountItems(listMountItems);
				if(arrayDocuments.Any(x => x!=null)){
					SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,_patient.PatNum,"Mount kept: "+mount.Description
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
			SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,_patient.PatNum,logText);
			return true;
		}

		///<summary>The partial's statements this mirror copies, exactly as they are in ControlImages.Acquire.cs (whitespace aside). The mirror
		///differs only where it has to: the launcher and FuncGetPatNumCur are passed in, and the message boxes are recorded.</summary>
		public static readonly string[] ArrayStatementsCopied={
			"if(_patient==null){",
			"if(mountDef==null || mount==null){",
			"mountDef=null;",
			"Patient=_patient,",
			"PatNum=_patient.PatNum,",
			"FuncGetPatNumCur=()=>FormOpenDental.PatNumCur,",
			"Mount=mount,",
			"MountDef=mountDef,",
			"ImagingDevice=imagingDevice,",
			"CategoryDefNum=mount==null ? GetCurrentCategory() : mount.DocCategory,",
			"CaptureSessionLaunchResult result=CaptureSessionLauncher.Launch(request);",
			"if(result.Kind==CaptureSessionLaunchKind.NotStarted){",
			"bool isMountDeleted=result.IsEmptyMountToDelete && mount!=null && DeleteEmptyMountAfterSession(mount,result);",
			"if(result.Kind==CaptureSessionLaunchKind.Refused){",
			"if(!isMountDeleted){",
			"SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,_patient.PatNum,\"Capture session refused: \"+result.Reason);",
			"MsgBox.Show(result.Reason);",
			"if(result.Reason!=\"\"){",
			"List<MountItem> listMountItems=MountItems.GetItemsForMount(mount.MountNum);",
			"Document[] arrayDocuments=Documents.GetDocumentsForMountItems(listMountItems);",
			"if(arrayDocuments.Any(x => x!=null)){",
			"SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,_patient.PatNum,\"Mount kept: \"+mount.Description",
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
			"SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,_patient.PatNum,logText);",
		};

		///<summary>How many statements of each kind the partial has: the mirror's copies would miss one the partial added.</summary>
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
		};
	}
}
