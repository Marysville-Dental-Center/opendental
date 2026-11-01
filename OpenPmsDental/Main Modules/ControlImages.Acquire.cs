// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Session;
using OpenPmsDental.UI;
using OpenPmsDentalBusiness;

namespace OpenPmsDental{
	public partial class ControlImages{
		///<summary>The capture session (design 6.4, 7.3), called from ToolBarMountAcquire_Click after FrmMountAndAcquire. Returns false when the
		///session did not run and the legacy Mount/Acquire loop must run unchanged:
		///  - [Session] Enabled=0, the default and the rollback;
		///  - a device type the session does not drive (TwainMulti keeps AcquireMulti);
		///  - no session window.
		///Returns true when the session ran or refused to start; the legacy loop then does not run.
		///mountDef is not null only when the mount showing was created from it in this call. Such a mount is deleted again when it is still empty
		///(design 7.3 "The hook").
		///Every save in the session is for patientBound, _patient as it is here. The launcher checks it against FormOpenDental.PatNumCur, after
		///[Session] Enabled so that the rollback is unchanged, and again before every enable.
		///The main window's patient can change while the session window is open (a task popup's Go To, a Windows lock that logs off and selects
		///no patient; design 7.6), and with it _patient. So everything after the session is for patientBound, and the module shows the session's
		///mount or image only while it still shows that patient.</summary>
		private bool TryAcquireWithCaptureSession(ImagingDevice imagingDevice,MountDef mountDef){
			if(_patient==null){
				return false;//the button is disabled without a patient; the legacy path is unchanged
			}
			Patient patientBound=_patient;
			Mount mount=IsMountShowing() ? GetMountShowing() : null;
			if(mountDef==null || mount==null){
				mountDef=null;//only a mount made from the definition in this call may be deleted
			}
			CaptureSessionLaunchRequest request=new CaptureSessionLaunchRequest{
				Patient=patientBound,
				PatNum=patientBound.PatNum,
				FuncGetPatNumCur=()=>FormOpenDental.PatNumCur,
				Mount=mount,
				MountDef=mountDef,
				ImagingDevice=imagingDevice,
				CategoryDefNum=mount==null ? GetCurrentCategory() : mount.DocCategory,
				Owner=this,
			};
			CaptureSessionLaunchResult result=CaptureSessionLauncher.Launch(request);//never throws
			if(result.Kind==CaptureSessionLaunchKind.NotStarted){
				return false;
			}
			//The module may have been refreshed for another patient (or none) while the modal session ran (RefreshModuleData sets _patient and _patFolder).
			bool isSamePatient=_patient!=null && _patient.PatNum==patientBound.PatNum && FormOpenDental.PatNumCur==patientBound.PatNum;
			bool isMountDeleted=result.IsEmptyMountToDelete && mount!=null && DeleteEmptyMountAfterSession(patientBound,mount,result,isSamePatient);
			if(result.Kind==CaptureSessionLaunchKind.Refused){
				if(!isMountDeleted){
					//Design 7.3: the refusal is logged under ImageCreate; with an empty new mount, DeleteEmptyMountAfterSession wrote that entry.
					SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,patientBound.PatNum,"Capture session refused: "+result.Reason);
				}
				MsgBox.Show(result.Reason);//the source was never enabled
				return true;
			}
			RefreshAfterCaptureSession(isMountDeleted ? null : mount,result.Summary,isSamePatient);
			if(result.Reason!=""){
				MsgBox.Show(result.Reason);//the window failed while the session ran; what it saved is kept
			}
			return true;
		}

		///<summary>Design 7.3 "The hook": the mount created for the session holds no image. It is deleted, and that is logged under ImageCreate with
		///the refusal or the reason the session saved nothing. The launcher already checked the database (and keeps a mount whose slots a
		///quarantined image of the session may still be assigned into); the documents are read again right before the delete, because
		///Mounts.Delete removes the mount and its mount items but leaves any document, which would then point at a deleted mount item. It leaves
		///the mount's imagedraw rows too (the ScaleValue of its definition), so those are deleted first.
		///The mount is patientBound's, and so are the entries. isSamePatient: the module still shows that patient, so it is cleared of the mount;
		///otherwise it shows another patient (or none) and is left alone.</summary>
		private bool DeleteEmptyMountAfterSession(Patient patientBound,Mount mount,CaptureSessionLaunchResult result,bool isSamePatient){
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
				MsgBox.Show(Lan.g(this,"The empty mount could not be deleted:")+" "+ex.Message);
				return false;
			}
			Def defDocCategory=Defs.GetDef(DefCat.ImageCats,mount.DocCategory);
			string why=result.Kind==CaptureSessionLaunchKind.Refused
				? "the capture session refused to start: "+result.Reason
				: "the capture session saved no image";
			string logText="Mount Deleted: "+mount.Description+" with category "+(defDocCategory?.ItemName??"")+", "+why;
			SecurityLogs.MakeLogEntry(EnumPermType.ImageCreate,patientBound.PatNum,logText);
			if(isSamePatient){
				FillImageSelector(false);
				SelectTreeNode1(null);//clears the deleted mount from the display
			}
			return true;
		}

		///<summary>Design 7.3 step 6: the module after the session. mount is the session's mount, or null in single-target mode (or when it was
		///deleted because it stayed empty). isSamePatient false: the module shows another patient now (or none), so only that module is refreshed;
		///the session's mount and images are not loaded into it, and no thumbnail is written into that patient's folder.</summary>
		private void RefreshAfterCaptureSession(Mount mount,CaptureSessionSummary summary,bool isSamePatient){
			_isAcquiring=false;
			SetUnmountedBarVisibility();
			LayoutControls();
			FillImageSelector(false);//handles a null _patient
			if(!isSamePatient){
				return;
			}
			if(mount!=null){
				SelectTreeNode1(new NodeTypeAndKey(EnumImageNodeType.Mount,mount.MountNum));
			}
			else if(summary!=null && summary.ListDocNums.Count>0){
				SelectTreeNode1(new NodeTypeAndKey(EnumImageNodeType.Document,summary.ListDocNums.Last()));//single-target mode: the last image
			}
			ThumbnailRefresh();
			if(mount!=null && summary!=null && summary.CountImages>0 && IsMountShowing() && GetMountShowing().AdjModeAfterSeries){
				SetCropPanAdj(EnumCropPanAdj.Adj);
				LayoutControls();
			}
		}
	}
}
