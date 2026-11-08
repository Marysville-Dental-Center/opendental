// CLAIM-DCM 1 (owner question, fixed on 2026-11-07): the file Edit Claim attaches for an image document that is not DICOM. An image that was
// cropped, rotated, flipped or brightness-adjusted in the Imaging module is attached as it shows there; before the fix, a local images folder
// got a claim attachment row but no file (the check in FormImageSelectClaimAttach.SaveAttachment was the wrong way round), and cloud storage
// got the unedited file. DICOM documents go through ClaimAttachDicom. See docs/port-notes/CLAIM-DCM.md.
// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The file Edit Claim attaches for an image document (FormImageSelectClaimAttach): the file itself, or, when the Imaging module shows it
	///with a crop, rotation, flip or brightness and contrast window, the image as shown. Nothing here touches the database or the network: the
	///document is only read, the caller reads the file and stores the bytes in the EmailAttachments folder (local or cloud storage alike).</summary>
	public static class ClaimAttachImage {
		///<summary>The JPEG quality of an edited JPEG: 100, as ImageStore.Import uses for radiographs and claim attachments (ClaimAttachDicom.JpegQuality).
		///</summary>
		public const int JpegQuality=ClaimAttachDicom.JpegQuality;
		///<summary>The extension of an edited image whose own format cannot be written (for example .tga or .wmf).</summary>
		public const string PngExtension=".png";

		///<summary>True when the Imaging module shows the document other than its file: a crop, a rotation, a flip or a brightness and contrast window.
		///The same test the attachment window always made; only its result was used the wrong way round.</summary>
		public static bool HasImageSettings(Document document) {
			if(document==null) {
				return false;
			}
			return document.CropH!=0
				|| document.CropW!=0
				|| document.CropX!=0
				|| document.CropY!=0
				|| document.DegreesRotated!=0
				|| document.IsFlipped
				|| document.WindowingMax!=0
				|| document.WindowingMin!=0;
		}

		///<summary>The image format an edited copy of the file is written in: the format of its extension (JPEG, PNG, BMP, GIF, TIFF), or PNG for an
		///image extension whose format cannot be written. Never null.</summary>
		public static ImageFormat GetImageFormat(string fileName) {
			switch(GetExtensionLower(fileName)) {
				case ".jpg":
				case ".jpeg":
					return ImageFormat.Jpeg;
				case ".bmp":
					return ImageFormat.Bmp;
				case ".gif":
					return ImageFormat.Gif;
				case ".tif":
				case ".tiff":
					return ImageFormat.Tiff;
				default:
					return ImageFormat.Png;
			}
		}

		///<summary>The extension of the attached file for the document: its own, except for an edited image whose format cannot be written, which is
		///attached as PNG (PngExtension), so the extension always says what the file holds.</summary>
		public static string GetAttachExtension(Document document) {
			string fileName=document?.FileName??"";
			string extension=Path.GetExtension(fileName);
			if(!HasImageSettings(document) || !ImageHelper.HasImageExtension(fileName)) {
				return extension;
			}
			if(GetImageFormat(fileName).Guid==ImageFormat.Png.Guid && GetExtensionLower(fileName)!=".png") {
				return PngExtension;
			}
			return extension;
		}

		///<summary>The bytes to attach for the document, given its file's bytes: the file itself when it has no image settings (HasImageSettings) or
		///is not an image, otherwise the image as the Imaging module shows it without drawings (ImageHelper.ApplyDocumentSettingsToImage on a copy of
		///the document, with a crop saved before 21.4 converted in memory only), in GetImageFormat of its name; a JPEG at JpegQuality, with what a
		///rotation or crop leaves uncovered black. The document is never changed. Throws when the bytes are not an image GDI+ reads.</summary>
		public static byte[] BuildFileBytes(Document document,byte[] arrayBytesFile) {
			if(arrayBytesFile==null || arrayBytesFile.Length==0) {
				throw new ODException(Lans.g("ClaimAttachImage","The image file is empty, so it was not attached to the claim."));
			}
			if(document==null || !HasImageSettings(document) || !ImageHelper.HasImageExtension(document.FileName)) {
				return arrayBytesFile;
			}
			Document documentCopy=document.Copy();
			using MemoryStream memoryStreamIn=new MemoryStream(arrayBytesFile);
			using Image imageFile=Image.FromStream(memoryStreamIn);
			using Bitmap bitmapRaw=new Bitmap(imageFile);
			ImageHelper.ConvertCropIfNeeded(documentCopy,bitmapRaw,doSave:false);
			using Bitmap bitmapShown=ImageHelper.ApplyDocumentSettingsToImage(documentCopy,bitmapRaw,ImageSettingFlags.ALL);
			return Encode(bitmapShown,GetImageFormat(document.FileName));
		}

		///<summary>The bytes of bitmap in imageFormat. JPEG through ClaimAttachDicom.EncodeJpeg (quality 100, no alpha); any other format as GDI+
		///writes it.</summary>
		public static byte[] Encode(Bitmap bitmap,ImageFormat imageFormat) {
			if(bitmap==null) {
				throw new ArgumentNullException(nameof(bitmap));
			}
			if(imageFormat==null || imageFormat.Guid==ImageFormat.Jpeg.Guid) {
				return ClaimAttachDicom.EncodeJpeg(bitmap);
			}
			using MemoryStream memoryStream=new MemoryStream();
			bitmap.Save(memoryStream,imageFormat);
			return memoryStream.ToArray();
		}

		private static string GetExtensionLower(string fileName) {
			if(string.IsNullOrWhiteSpace(fileName)) {
				return "";
			}
			try {
				return Path.GetExtension(fileName.Trim()).ToLowerInvariant();
			}
			catch(Exception ex) {//characters a path cannot have
				ex.DoNothing();
				return "";
			}
		}
	}
}
