// CLAIM-DCM: DICOM X-rays on insurance claims. A DICOM document (for example a 16-bit X-ray that ImagingCapture's DicomIntraOralWriter
// wrote: Explicit VR Little Endian, uncompressed MONOCHROME2, with its window stored in Document.WindowingMin and WindowingMax) is never
// sent or stored as a claim attachment. Every place a claim image can be chosen renders an 8-bit copy through the program's own DICOM
// display path (ImageStore.OpenBitmapDicom and DicomHelper, whose IDicomPixelReader asks DicomSubsetReader first, then fo-dicom), with the
// stored window and the crop, rotation and flip the Imaging module applies, and attaches that copy in the format the path already uses.
// The stored DICOM file and its document row are never changed. See docs/port-notes/CLAIM-DCM.md.
// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using CodeBase;
using OpenDentBusiness.Eclaims;

namespace OpenDentBusiness {
	///<summary>The 8-bit copy of a DICOM image made for a claim attachment, and the checks that keep a DICOM file itself off every claim.
	///Nothing here touches the database or the network: the document row is only read, and rendering happens on a copy of it.</summary>
	public static class ClaimAttachDicom {
		///<summary>The extension of the copy attached in Edit Claim (the claimattach row's file in the EmailAttachments folder).</summary>
		public const string JpegExtension=".jpg";
		///<summary>The JPEG quality of the Edit Claim copy: 100, as ImageStore.Import uses for radiographs and claim attachments.</summary>
		public const int JpegQuality=100;
		///<summary>A PS3.10 DICOM file has a 128-byte preamble, then "DICM".</summary>
		private const int DICM_OFFSET=128;
		private static readonly byte[] _arrayBytesDicm=Encoding.ASCII.GetBytes("DICM");

		#region File names
		///<summary>True when the file name or path ends in .dcm, in any case. This is how ImageStore.Import decides that a file is DICOM, so a file
		///imported as DICOM is also treated as DICOM on a claim. Surrounding spaces are ignored.</summary>
		public static bool IsDicomFileName(string fileName) {
			if(string.IsNullOrWhiteSpace(fileName)) {
				return false;
			}
			return fileName.Trim().EndsWith(".dcm",StringComparison.OrdinalIgnoreCase);
		}

		///<summary>The name shown in Edit Claim for the copy of a DICOM document: the document's file name with the .jpg extension, so the list
		///says what is attached ("BobSmith12.dcm" becomes "BobSmith12.jpg").</summary>
		public static string GetDisplayedFileName(string fileNameDicom) {
			if(string.IsNullOrWhiteSpace(fileNameDicom)) {
				return "Radiograph"+JpegExtension;
			}
			return Path.ChangeExtension(Path.GetFileName(fileNameDicom.Trim()),JpegExtension);
		}

		///<summary>The message for a DICOM file the program cannot read. It is shown, and nothing is attached.</summary>
		public static string GetUnreadableMessage(string fileName) {
			string fileNameShort="";
			if(!string.IsNullOrWhiteSpace(fileName)) {
				try {
					fileNameShort=Path.GetFileName(fileName.Trim());
				}
				catch(Exception ex) {//characters a path cannot have: show the name as it is
					ex.DoNothing();
					fileNameShort=fileName.Trim();
				}
			}
			return Lans.g("ClaimAttachDicom","The DICOM image could not be read, so it was not attached to the claim.")
				+(fileNameShort=="" ? "" : " ("+fileNameShort+")")+"\r\n"
				+Lans.g("ClaimAttachDicom","Open it in the Imaging module to check it, or attach another image.");
		}
		#endregion File names

		#region Reading
		///<summary>The raw pixels of a DICOM document, read through the Imaging module's own path: ImageStore.OpenBitmapDicom, which uses the
		///patient's folder, cloud storage or the database row, and DicomHelper's reader (DicomSubsetReader first, then fo-dicom). A document
		///whose name ends in .DCM in another case, which OpenBitmapDicom does not open, is read from its file bytes with the same reader.
		///Throws an ODException with GetUnreadableMessage when the file is missing or cannot be read. The document is not changed.</summary>
		public static BitmapDicom ReadDocument(Document document,string patFolder) {
			if(document==null) {
				throw new ODException(GetUnreadableMessage(""));
			}
			BitmapDicom bitmapDicom;
			try {
				if(ImageHelper.IsDicomFileName(document.FileName)) {
					bitmapDicom=ImageStore.OpenBitmapDicom(document,patFolder);
				}
				else {
					byte[] arrayBytes=ImageStore.GetDocumentFileBytes(document,patFolder);
					bitmapDicom=ReadBytes(arrayBytes);
				}
			}
			catch(Exception ex) {
				throw new ODException(GetUnreadableMessage(document.FileName),ex);
			}
			return CheckRead(bitmapDicom,document.FileName);
		}

		///<summary>The raw pixels of a DICOM file on disk, read with DicomHelper.GetFromFile (DicomSubsetReader first, then fo-dicom). Throws an
		///ODException with GetUnreadableMessage when the file is missing or cannot be read.</summary>
		public static BitmapDicom ReadFile(string path) {
			BitmapDicom bitmapDicom;
			try {
				bitmapDicom=DicomHelper.GetFromFile(path);
			}
			catch(Exception ex) {
				throw new ODException(GetUnreadableMessage(path),ex);
			}
			return CheckRead(bitmapDicom,path);
		}

		///<summary>DicomHelper.GetFromStream on the bytes, or null when there are none. May throw, as fo-dicom does for a file it cannot parse.</summary>
		private static BitmapDicom ReadBytes(byte[] arrayBytes) {
			if(arrayBytes==null || arrayBytes.Length==0) {
				return null;
			}
			using MemoryStream memoryStream=new MemoryStream(arrayBytes);
			return DicomHelper.GetFromStream(memoryStream);
		}

		///<summary>Refuses what no display could show: nothing read, no size, or fewer pixels than the size says.</summary>
		private static BitmapDicom CheckRead(BitmapDicom bitmapDicom,string fileName) {
			if(bitmapDicom==null || bitmapDicom.Width<1 || bitmapDicom.Height<1 || bitmapDicom.ArrayDataRaw==null
				|| bitmapDicom.ArrayDataRaw.Length<(long)bitmapDicom.Width*bitmapDicom.Height)
			{
				throw new ODException(GetUnreadableMessage(fileName));
			}
			return bitmapDicom;
		}
		#endregion Reading

		#region Rendering
		///<summary>The 8-bit copy of a DICOM document as the Imaging module shows it: windowed with the stored WindowingMin and WindowingMax
		///(DicomHelper.ApplyWindowing, which also reads legacy 12-bit scale values), then cropped, rotated and flipped
		///(ImageHelper.GetImageCroppedDicom). Drawings are not included, as the Edit Claim attachment of any other image leaves them out.
		///Rendered on a copy of the document, so the document and its file stay exactly as they were, even for a pre-21.4 crop. The caller
		///disposes the bitmap.</summary>
		public static Bitmap Render(Document document,BitmapDicom bitmapDicom) {
			if(document==null || bitmapDicom==null) {
				return null;
			}
			Document documentCopy=document.Copy();
			return ImageHelper.GetImageCroppedDicom(documentCopy,bitmapDicom,doSaveCropConversion:false);
		}

		///<summary>Render with the document's drawings, as the DentalXChange image picker sends every other image
		///(ImageHelper.GetBitmapOfDocumentFromDb). listImageDraws may be null or empty. The caller disposes the bitmap.</summary>
		public static Bitmap Render(Document document,BitmapDicom bitmapDicom,List<ImageDraw> listImageDraws) {
			if(document==null || bitmapDicom==null) {
				return null;
			}
			Document documentCopy=document.Copy();
			using Bitmap bitmapWindowed=DicomHelper.ApplyWindowing(bitmapDicom,documentCopy.WindowingMin,documentCopy.WindowingMax);
			ImageHelper.ConvertCropIfNeeded(documentCopy,bitmapWindowed,doSave:false);
			return ImageHelper.GetBitmapOfDocument(documentCopy,bitmapWindowed,listImageDraws??new List<ImageDraw>());
		}

		///<summary>The 8-bit copy of a DICOM file that is not one of the patient's documents (chosen from disk or pasted as a file): windowed with
		///the window the program would store if the file were imported (DicomHelper.CalculateWindowingOnImport), with no crop, rotation or flip.
		///bitmapDicom is not changed. The caller disposes the bitmap.</summary>
		public static Bitmap RenderFile(BitmapDicom bitmapDicom) {
			if(bitmapDicom==null) {
				return null;
			}
			BitmapDicom bitmapDicomCopy=new BitmapDicom {
				ArrayDataRaw=bitmapDicom.ArrayDataRaw,
				Width=bitmapDicom.Width,
				Height=bitmapDicom.Height,
				BitsAllocated=bitmapDicom.BitsAllocated,
				BitsStored=bitmapDicom.BitsStored,
				PhotometricInterp=bitmapDicom.PhotometricInterp,
			};
			try {
				DicomHelper.CalculateWindowingOnImport(bitmapDicomCopy);
			}
			catch(Exception ex) {//values outside the bits the file says it stores: show the full range, as a window never set does
				ex.DoNothing();
				bitmapDicomCopy.WindowingMin=0;
				bitmapDicomCopy.WindowingMax=0;
			}
			Document document=new Document {
				WindowingMin=bitmapDicomCopy.WindowingMin,
				WindowingMax=bitmapDicomCopy.WindowingMax,
			};
			return Render(document,bitmapDicomCopy);
		}

		///<summary>The patient's document for a file chosen from disk or the clipboard: the one whose file is that file in the patient's folder.
		///Null when the file is not in the patient's folder or no document has its name (it is then rendered with RenderFile).</summary>
		public static Document FindDocumentForFile(IEnumerable<Document> listDocuments,string patFolder,string path) {
			if(listDocuments==null || string.IsNullOrWhiteSpace(patFolder) || string.IsNullOrWhiteSpace(path)) {
				return null;
			}
			string folderFile;
			string folderPatient;
			try {
				folderFile=Path.GetFullPath(Path.GetDirectoryName(path)).TrimEnd('\\','/');
				folderPatient=Path.GetFullPath(patFolder).TrimEnd('\\','/');
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
			if(!string.Equals(folderFile,folderPatient,StringComparison.OrdinalIgnoreCase)) {
				return null;
			}
			string fileName=Path.GetFileName(path);
			return listDocuments.FirstOrDefault(x => x!=null && string.Equals(x.FileName,fileName,StringComparison.OrdinalIgnoreCase));
		}
		#endregion Rendering

		#region Attachment files
		///<summary>The JPEG bytes of an 8-bit copy, at JpegQuality, without alpha.</summary>
		public static byte[] EncodeJpeg(Bitmap bitmap) {
			if(bitmap==null) {
				throw new ArgumentNullException(nameof(bitmap));
			}
			ImageCodecInfo imageCodecInfo=ImageCodecInfo.GetImageEncoders().First(x => x.FormatID==ImageFormat.Jpeg.Guid);
			using EncoderParameters encoderParameters=new EncoderParameters(1);
			encoderParameters.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,(long)JpegQuality);
			using Bitmap bitmapRgb=new Bitmap(bitmap.Width,bitmap.Height,PixelFormat.Format24bppRgb);
			using(Graphics g=Graphics.FromImage(bitmapRgb)) {
				g.Clear(Color.Black);//what a rotation or crop leaves uncovered is black, as an X-ray's background
				g.DrawImage(bitmap,0,0,bitmap.Width,bitmap.Height);
			}
			using MemoryStream memoryStream=new MemoryStream();
			bitmapRgb.Save(memoryStream,imageCodecInfo,encoderParameters);
			return memoryStream.ToArray();
		}

		///<summary>The file Edit Claim attaches for a DICOM document (FormImageSelectClaimAttach): the 8-bit copy (Render) as a JPEG, and the name
		///to show. Throws an ODException with GetUnreadableMessage when the file cannot be read. Nothing is written; the caller stores the
		///bytes in the EmailAttachments folder under a new .jpg name.</summary>
		public static ClaimAttachDicomFile BuildClaimAttachFile(Document document,string patFolder) {
			BitmapDicom bitmapDicom=ReadDocument(document,patFolder);
			return BuildClaimAttachFile(document,bitmapDicom);
		}

		///<summary>BuildClaimAttachFile for pixels already read.</summary>
		public static ClaimAttachDicomFile BuildClaimAttachFile(Document document,BitmapDicom bitmapDicom) {
			using Bitmap bitmap=Render(document,bitmapDicom);
			if(bitmap==null) {
				throw new ODException(GetUnreadableMessage(document?.FileName));
			}
			ClaimAttachDicomFile claimAttachDicomFile=new ClaimAttachDicomFile();
			claimAttachDicomFile.DisplayedFileName=GetDisplayedFileName(document.FileName);
			claimAttachDicomFile.FileBytes=EncodeJpeg(bitmap);
			claimAttachDicomFile.Width=bitmap.Width;
			claimAttachDicomFile.Height=bitmap.Height;
			return claimAttachDicomFile;
		}
		#endregion Attachment files

		#region Never the DICOM file itself
		///<summary>True when the bytes are a DICOM file: "DICM" right after the 128-byte preamble (PS3.10).</summary>
		public static bool IsDicomBytes(byte[] arrayBytes) {
			if(arrayBytes==null || arrayBytes.Length<DICM_OFFSET+_arrayBytesDicm.Length) {
				return false;
			}
			for(int i=0;i<_arrayBytesDicm.Length;i++) {
				if(arrayBytes[DICM_OFFSET+i]!=_arrayBytesDicm[i]) {
					return false;
				}
			}
			return true;
		}

		private static string GetDicomRefusedMessage(string fileName) {
			return (string.IsNullOrWhiteSpace(fileName) ? "" : fileName+": ")
				+Lans.g("ClaimAttachDicom","A DICOM file is never sent as a claim attachment. Attach the image from the Imaging module, which sends an 8-bit copy.");
		}

		///<summary>Throws an ODException for the first DentalXChange attachment whose bytes are a DICOM file. Called before anything is sent, next
		///to the PDF check. Every window builds its images as JPEGs, so this only stops a DICOM file that reached the list some other way.</summary>
		public static void ThrowIfAnyIsDicom(List<ClaimConnect.ImageAttachment> listImageAttachments) {
			if(listImageAttachments==null) {
				return;
			}
			for(int i=0;i<listImageAttachments.Count;i++) {
				if(listImageAttachments[i]!=null && IsDicomBytes(listImageAttachments[i].ImageFileAsBase64)) {
					throw new ODException(GetDicomRefusedMessage(listImageAttachments[i].GetFileNameToSend()));
				}
			}
		}

		///<summary>Throws an ODException for the first EDS attachment whose bytes are a DICOM file. Called before anything is sent.</summary>
		public static void ThrowIfAnyIsDicom(List<EDS.ImageAttachment> listImageAttachments) {
			if(listImageAttachments==null) {
				return;
			}
			for(int i=0;i<listImageAttachments.Count;i++) {
				if(listImageAttachments[i]!=null && IsDicomBytes(listImageAttachments[i].FileData)) {
					throw new ODException(GetDicomRefusedMessage(listImageAttachments[i].FileDisplayName));
				}
			}
		}
		#endregion Never the DICOM file itself
	}

	///<summary>What Edit Claim attaches for a DICOM document: the 8-bit JPEG copy and the name it shows.</summary>
	public class ClaimAttachDicomFile {
		public string DisplayedFileName;
		public byte[] FileBytes;
		public int Width;
		public int Height;
	}
}
