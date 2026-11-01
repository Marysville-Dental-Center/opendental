// CLAIM-DCM: the claim attachment windows' side of DICOM X-rays on claims. Every window that lets the user choose a claim image calls these
// for a DICOM document or a .dcm file, gets the 8-bit copy (OpenDentBusiness.ClaimAttachDicom) and attaches it the way it attaches any other
// image. A DICOM file the program cannot read shows a clear message and nothing is attached. See docs/port-notes/CLAIM-DCM.md.
// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental {
	public static class ClaimAttachDicomL {
		///<summary>The 8-bit copy of a DICOM document for a claim, with its stored window, crop, rotation and flip, and its drawings when
		///doIncludeDrawings is true (as the DentalXChange image picker sends other images). Returns null when the file cannot be read, after
		///showing why unless doShowMessage is false (a preview). The caller disposes the bitmap.</summary>
		public static Bitmap GetBitmapForDocument(IWin32Window owner,Document document,string patFolder,bool doIncludeDrawings,bool doShowMessage=true) {
			try {
				BitmapDicom bitmapDicom=ClaimAttachDicom.ReadDocument(document,patFolder);
				if(doIncludeDrawings) {
					return ClaimAttachDicom.Render(document,bitmapDicom,ImageDraws.RefreshForDoc(document.DocNum));
				}
				return ClaimAttachDicom.Render(document,bitmapDicom);
			}
			catch(ODException ex) {
				if(doShowMessage) {
					ShowMessage(owner,ex.Message);
				}
			}
			catch(Exception ex) {
				if(doShowMessage) {
					ShowMessage(owner,ClaimAttachDicom.GetUnreadableMessage(document?.FileName)+"\r\n"+ex.Message);
				}
			}
			return null;
		}

		///<summary>The 8-bit copy of a DICOM file chosen from disk or pasted as a file. When the file is one of the patient's documents in the
		///patient's folder, that document's stored window, crop, rotation and flip are used; otherwise the window the program would store on
		///import. Shows the message and returns null when the file cannot be read. The caller disposes the bitmap.</summary>
		public static Bitmap GetBitmapForFile(IWin32Window owner,Patient patient,string path) {
			try {
				BitmapDicom bitmapDicom=ClaimAttachDicom.ReadFile(path);
				Document document=FindDocumentForFile(patient,path);
				if(document!=null) {
					return ClaimAttachDicom.Render(document,bitmapDicom);
				}
				return ClaimAttachDicom.RenderFile(bitmapDicom);
			}
			catch(ODException ex) {
				ShowMessage(owner,ex.Message);
			}
			catch(Exception ex) {
				ShowMessage(owner,ClaimAttachDicom.GetUnreadableMessage(path)+"\r\n"+ex.Message);
			}
			return null;
		}

		///<summary>True when the DICOM file can be read; otherwise shows the message and returns false. Used before a .dcm file is imported into
		///the patient's images for a claim, so an unreadable file is neither imported nor attached.</summary>
		public static bool CanReadFile(IWin32Window owner,string path) {
			try {
				ClaimAttachDicom.ReadFile(path);
				return true;
			}
			catch(ODException ex) {
				ShowMessage(owner,ex.Message);
			}
			return false;
		}

		///<summary>The patient's document whose file this is, when the file is in the patient's folder (local A to Z folder only), or null.</summary>
		private static Document FindDocumentForFile(Patient patient,string path) {
			if(patient==null || PrefC.AtoZfolderUsed!=DataStorageType.LocalAtoZ) {
				return null;
			}
			try {
				string patFolder=ImageStore.GetPatientFolder(patient,ImageStore.GetPreferredAtoZpath());
				return ClaimAttachDicom.FindDocumentForFile(Documents.GetAllWithPat(patient.PatNum),patFolder,path);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
		}

		///<summary>The message that a mount was not attached because some of its DICOM images cannot be read.</summary>
		public static string GetMountUnreadableMessage(List<Document> listDocumentsDicomUnreadable) {
			List<string> listFileNames=new List<string>();
			for(int i=0;i<listDocumentsDicomUnreadable.Count;i++) {
				listFileNames.Add(listDocumentsDicomUnreadable[i].FileName);
			}
			return Lans.g("ClaimAttachDicom","The mount was not attached to the claim, because these DICOM images in it could not be read:")+"\r\n"
				+string.Join("\r\n",listFileNames)+"\r\n"
				+Lans.g("ClaimAttachDicom","Open the mount in the Imaging module to check them.");
		}

		private static void ShowMessage(IWin32Window owner,string message) {
			if(owner==null) {
				MessageBox.Show(message);
				return;
			}
			MessageBox.Show(owner,message);
		}
	}
}
