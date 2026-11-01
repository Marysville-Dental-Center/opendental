// DentalXChange: PDF documents as claim attachments (Open Dental 26.1.64, package V-DXC2). A PDF goes to DentalXChange as it is, base64-encoded
// in the same image field the existing attachment calls use (partner service addImage and XConnect /attachments/images/add), with a file name
// that ends in .pdf. It is never re-encoded as a JPEG the way images are. Encrypted or password-protected PDFs are refused before any call.
// EDS keeps refusing PDFs. Snip and Paste stay image-only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using CodeBase;
using OpenDentBusiness.Eclaims;

namespace OpenDentBusiness.VendorDirect.DentalXChange {
	///<summary>Checks and builds PDF attachments for DentalXChange (ClaimConnect and XConnect). Nothing here makes a network call.</summary>
	public static class DxcPdfAttachment {
		public const string PdfExtension=".pdf";
		///<summary>The media type of a PDF. DentalXChange's attachment calls have no content-type field, so the type travels as the .pdf file name and
		///the PDF bytes themselves (which start with %PDF-).</summary>
		public const string PdfContentType="application/pdf";
		///<summary>The largest PDF this program sends: 10 MB. INFERRED: DentalXChange publishes no size limit for attachments, so this is our own
		///limit, kept in one place so it can follow DentalXChange's figure once the office has it.</summary>
		public const long MaxPdfBytes=10L*1024*1024;
		///<summary>A PDF file starts with %PDF- within its first 1024 bytes (PDF readers allow leading bytes before the header).</summary>
		private const int HEADER_SEARCH_LENGTH=1024;
		private static readonly byte[] _arrayBytesHeader=Encoding.ASCII.GetBytes("%PDF-");
		private static readonly byte[] _arrayBytesEncrypt=Encoding.ASCII.GetBytes("/Encrypt");
		private static readonly byte[] _arrayBytesType=Encoding.ASCII.GetBytes("/Type");
		private static readonly byte[] _arrayBytesPage=Encoding.ASCII.GetBytes("/Page");

		#region File names and clearinghouses
		///<summary>True when the file name or path ends in .pdf, in any case (".PDF", ".Pdf"). Surrounding spaces are ignored.</summary>
		public static bool IsPdfFileName(string fileName) {
			if(string.IsNullOrWhiteSpace(fileName)) {
				return false;
			}
			return fileName.Trim().EndsWith(PdfExtension,StringComparison.OrdinalIgnoreCase);
		}

		///<summary>The file name DentalXChange receives for a PDF: the display name, trimmed, ending in .pdf. A display name that already ends in .pdf
		///in any case is kept as it is. A blank name becomes "Attachment.pdf".</summary>
		public static string GetFileNameToSend(string fileNameDisplay) {
			string fileName=(fileNameDisplay??"").Trim().TrimEnd('.',' ');
			if(fileName=="") {
				fileName="Attachment";
			}
			if(!IsPdfFileName(fileName)) {
				fileName+=PdfExtension;
			}
			return fileName;
		}

		///<summary>True only for DentalXChange (ClaimConnect, with or without XConnect). EDS documents that it takes images only, so PDFs stay refused
		///there.</summary>
		public static bool IsPdfAccepted(EclaimsCommBridge eclaimsCommBridge) {
			return eclaimsCommBridge==EclaimsCommBridge.ClaimConnect;
		}

		///<summary>What to tell the user when a PDF is chosen for a clearinghouse that does not take PDFs.</summary>
		public static string GetPdfRefusedMessage(EclaimsCommBridge eclaimsCommBridge) {
			if(eclaimsCommBridge==EclaimsCommBridge.EDS) {
				return Lans.g("DxcPdfAttachment","EDS does not accept PDF attachments. Attach the pages as images instead.");
			}
			return Lans.g("DxcPdfAttachment","PDF attachments are only sent through DentalXChange.");
		}
		#endregion File names and clearinghouses

		#region Checking the bytes
		///<summary>True when %PDF- appears within the first 1024 bytes.</summary>
		public static bool HasPdfHeader(byte[] arrayBytes) {
			if(arrayBytes==null || arrayBytes.Length<_arrayBytesHeader.Length) {
				return false;
			}
			int idx=IndexOf(arrayBytes,_arrayBytesHeader,0,Math.Min(arrayBytes.Length,HEADER_SEARCH_LENGTH+_arrayBytesHeader.Length));
			return idx>=0;
		}

		///<summary>True when the PDF is encrypted, which includes every password-protected PDF: its trailer (or cross-reference stream dictionary, which
		///is never compressed) has an /Encrypt entry, either an indirect reference ("/Encrypt 12 0 R") or a dictionary ("/Encrypt &lt;&lt;"). A PDF with
		///only an owner password (no password to open, but restricted permissions) is encrypted too. Longer names that only start with /Encrypt, such
		///as EncryptMetadata, do not count.</summary>
		public static bool IsEncrypted(byte[] arrayBytes) {
			if(arrayBytes==null) {
				return false;
			}
			int idx=IndexOf(arrayBytes,_arrayBytesEncrypt,0,arrayBytes.Length);
			while(idx>=0) {
				int idxAfter=idx+_arrayBytesEncrypt.Length;
				if(idxAfter<arrayBytes.Length && !IsRegularChar(arrayBytes[idxAfter])) {
					int idxValue=SkipWhiteSpace(arrayBytes,idxAfter);
					if(IsIndirectReference(arrayBytes,idxValue) || IsDictionaryStart(arrayBytes,idxValue)) {
						return true;
					}
				}
				idx=IndexOf(arrayBytes,_arrayBytesEncrypt,idx+1,arrayBytes.Length);
			}
			return false;
		}

		///<summary>Best-effort page count for display: the number of "/Type /Page" objects that are not compressed into object streams. 0 when none
		///can be seen (for example when every page object is compressed); callers then show no count.</summary>
		public static int CountPages(byte[] arrayBytes) {
			if(arrayBytes==null) {
				return 0;
			}
			int count=0;
			int idx=IndexOf(arrayBytes,_arrayBytesType,0,arrayBytes.Length);
			while(idx>=0) {
				int idxAfterType=idx+_arrayBytesType.Length;
				if(idxAfterType<arrayBytes.Length && !IsRegularChar(arrayBytes[idxAfterType])) {
					int idxValue=SkipWhiteSpace(arrayBytes,idxAfterType);
					if(StartsWithAt(arrayBytes,idxValue,_arrayBytesPage)) {
						int idxAfterPage=idxValue+_arrayBytesPage.Length;
						if(idxAfterPage>=arrayBytes.Length || !IsRegularChar(arrayBytes[idxAfterPage])) {//"/Pages" is the page tree, not a page
							count++;
						}
					}
				}
				idx=IndexOf(arrayBytes,_arrayBytesType,idx+1,arrayBytes.Length);
			}
			return count;
		}

		///<summary>Null when the bytes can be sent to DentalXChange as a PDF attachment; otherwise the reason, ready to show. Checks, in order: empty,
		///over MaxPdfBytes, not a PDF, encrypted or password-protected.</summary>
		public static string GetProblem(byte[] arrayBytes) {
			if(arrayBytes==null || arrayBytes.Length==0) {
				return Lans.g("DxcPdfAttachment","The PDF file is empty.");
			}
			if(arrayBytes.Length>MaxPdfBytes) {
				return GetTooLargeMessage(arrayBytes.Length);
			}
			if(!HasPdfHeader(arrayBytes)) {
				return Lans.g("DxcPdfAttachment","The file is not a PDF document, or it is damaged.");
			}
			if(IsEncrypted(arrayBytes)) {
				return Lans.g("DxcPdfAttachment","This PDF is encrypted or password-protected, so DentalXChange cannot read it. Save a copy without a password or "
					+"security restrictions and attach that copy instead.");
			}
			return null;
		}

		private static string GetTooLargeMessage(long length) {
			return Lans.g("DxcPdfAttachment","The PDF file is too large to send")+" ("+FormatMegabytes(length)+"). "
				+Lans.g("DxcPdfAttachment","The largest PDF attachment is")+" "+FormatMegabytes(MaxPdfBytes)+".";
		}

		private static string FormatMegabytes(long length) {
			return (length/1024d/1024d).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+" MB";
		}
		#endregion Checking the bytes

		#region Building attachments
		///<summary>Reads a PDF from disk for attaching. The size is checked before the file is read. Throws an ODException, with a message ready to show,
		///when the file is missing, unreadable, empty, too large, not a PDF, or encrypted.</summary>
		public static byte[] ReadPdfFile(string path) {
			byte[] arrayBytes;
			try {
				FileInfo fileInfo=new FileInfo(path);
				if(!fileInfo.Exists) {
					throw new ODException(Lans.g("DxcPdfAttachment","The PDF file could not be found:")+" "+path);
				}
				if(fileInfo.Length>MaxPdfBytes) {
					throw new ODException(GetTooLargeMessage(fileInfo.Length));
				}
				arrayBytes=File.ReadAllBytes(path);
			}
			catch(ODException) {
				throw;
			}
			catch(Exception ex) {
				throw new ODException(Lans.g("DxcPdfAttachment","The PDF file could not be read:")+" "+ex.Message,ex);
			}
			ThrowIfProblem(arrayBytes);
			return arrayBytes;
		}

		///<summary>Reads a PDF document from the patient's images (A to Z folder, cloud storage, or the database), for attaching. Throws an ODException,
		///with a message ready to show, when the document has no file or the file can't be sent.</summary>
		public static byte[] ReadPdfDocument(Document document,string patFolder) {
			byte[] arrayBytes;
			try {
				arrayBytes=ImageStore.GetDocumentFileBytes(document,patFolder);
			}
			catch(Exception ex) {
				throw new ODException(Lans.g("DxcPdfAttachment","The PDF file could not be read:")+" "+ex.Message,ex);
			}
			if(arrayBytes==null || arrayBytes.Length==0) {
				throw new ODException(Lans.g("DxcPdfAttachment","The PDF document's file could not be found in the patient's images."));
			}
			ThrowIfProblem(arrayBytes);
			return arrayBytes;
		}

		///<summary>An attachment that sends the PDF's own bytes, marked IsPdf. The orientation is "right", the default; it only matters for X-ray
		///images. Throws an ODException when the bytes can't be sent (GetProblem).</summary>
		public static ClaimConnect.ImageAttachment CreateImageAttachment(byte[] arrayBytesPdf,string fileNameDisplay,DateTime dateCreated,
			ClaimConnect.ImageTypeCode imageTypeCode)
		{
			ThrowIfProblem(arrayBytesPdf);
			ClaimConnect.ImageAttachment imageAttachment=new ClaimConnect.ImageAttachment();
			imageAttachment.ImageFileNameDisplay=fileNameDisplay;
			imageAttachment.ImageDate=dateCreated;
			imageAttachment.ImageType=imageTypeCode;
			imageAttachment.ImageOrientationType="right";
			imageAttachment.ImageFileAsBase64=arrayBytesPdf;
			imageAttachment.IsPdf=true;
			return imageAttachment;
		}

		///<summary>Throws an ODException for the first PDF attachment in the list that can't be sent. Images are not checked. Called right before
		///anything is sent to DentalXChange.</summary>
		public static void ThrowIfAnyCannotBeSent(List<ClaimConnect.ImageAttachment> listImageAttachments) {
			if(listImageAttachments==null) {
				return;
			}
			for(int i=0;i<listImageAttachments.Count;i++) {
				if(listImageAttachments[i]==null || !listImageAttachments[i].IsPdf) {
					continue;
				}
				string problem=GetProblem(listImageAttachments[i].ImageFileAsBase64);
				if(problem!=null) {
					throw new ODException(listImageAttachments[i].GetFileNameToSend()+": "+problem);
				}
			}
		}

		private static void ThrowIfProblem(byte[] arrayBytes) {
			string problem=GetProblem(arrayBytes);
			if(problem!=null) {
				throw new ODException(problem);
			}
		}
		#endregion Building attachments

		#region Preview
		///<summary>A PDF icon for previews, since this program does not render PDF pages: a page outline with a folded corner, "PDF", the file name and
		///the page count when it is known (pageCount above 0). The caller disposes the bitmap.</summary>
		public static Bitmap CreatePreviewBitmap(int width,int height,string fileName,int pageCount=0) {
			width=Math.Max(width,60);
			height=Math.Max(height,80);
			Bitmap bitmap=new Bitmap(width,height);
			using Graphics g=Graphics.FromImage(bitmap);
			g.SmoothingMode=SmoothingMode.AntiAlias;
			g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAlias;
			g.Clear(Color.White);
			//A letter-size page, centered, using most of the height.
			float pageH=height*0.70f;
			float pageW=pageH*8.5f/11f;
			if(pageW>width*0.8f) {
				pageW=width*0.8f;
				pageH=pageW*11f/8.5f;
			}
			float left=(width-pageW)/2f;
			float top=height*0.06f;
			float fold=pageW*0.22f;
			PointF[] arrayPoints={
				new PointF(left,top),
				new PointF(left+pageW-fold,top),
				new PointF(left+pageW,top+fold),
				new PointF(left+pageW,top+pageH),
				new PointF(left,top+pageH),
			};
			using(SolidBrush brushPage=new SolidBrush(Color.FromArgb(248,248,248)))
			using(Pen penPage=new Pen(Color.FromArgb(120,120,120),Math.Max(1f,width/200f))) {
				g.FillPolygon(brushPage,arrayPoints);
				g.DrawPolygon(penPage,arrayPoints);
				g.DrawLines(penPage,new PointF[] { new PointF(left+pageW-fold,top),new PointF(left+pageW-fold,top+fold),new PointF(left+pageW,top+fold) });
			}
			//The "PDF" band across the page.
			RectangleF rectBand=new RectangleF(left-pageW*0.06f,top+pageH*0.42f,pageW*1.12f,pageH*0.22f);
			using(SolidBrush brushBand=new SolidBrush(Color.FromArgb(178,34,34))) {
				g.FillRectangle(brushBand,rectBand);
			}
			using(StringFormat stringFormat=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter })
			using(Font fontBand=new Font(FontFamily.GenericSansSerif,Math.Max(6f,rectBand.Height*0.45f),FontStyle.Bold,GraphicsUnit.Pixel))
			using(Font fontCaption=new Font(FontFamily.GenericSansSerif,Math.Max(6f,height*0.035f),FontStyle.Regular,GraphicsUnit.Pixel)) {
				g.DrawString("PDF",fontBand,Brushes.White,rectBand,stringFormat);
				string caption=fileName??"";
				if(pageCount>0) {
					caption+=(caption=="" ? "" : "\r\n")+pageCount+" "+(pageCount==1 ? Lans.g("DxcPdfAttachment","page") : Lans.g("DxcPdfAttachment","pages"));
				}
				RectangleF rectCaption=new RectangleF(width*0.05f,top+pageH+height*0.03f,width*0.9f,height-(top+pageH+height*0.03f));
				g.DrawString(caption,fontCaption,Brushes.Black,rectCaption,stringFormat);
			}
			return bitmap;
		}
		#endregion Preview

		#region Byte helpers
		///<summary>The index of the first occurrence of arrayBytesFind at or after idxStart that ends before idxEnd, or -1.</summary>
		private static int IndexOf(byte[] arrayBytes,byte[] arrayBytesFind,int idxStart,int idxEnd) {
			int idxLast=Math.Min(idxEnd,arrayBytes.Length)-arrayBytesFind.Length;
			for(int i=Math.Max(0,idxStart);i<=idxLast;i++) {
				if(arrayBytes[i]!=arrayBytesFind[0]) {
					continue;
				}
				if(StartsWithAt(arrayBytes,i,arrayBytesFind)) {
					return i;
				}
			}
			return -1;
		}

		private static bool StartsWithAt(byte[] arrayBytes,int idx,byte[] arrayBytesFind) {
			if(idx<0 || idx+arrayBytesFind.Length>arrayBytes.Length) {
				return false;
			}
			for(int j=0;j<arrayBytesFind.Length;j++) {
				if(arrayBytes[idx+j]!=arrayBytesFind[j]) {
					return false;
				}
			}
			return true;
		}

		///<summary>PDF white-space characters: NUL, tab, line feed, form feed, carriage return and space.</summary>
		private static bool IsWhiteSpace(byte b) {
			return b==0x00 || b==0x09 || b==0x0A || b==0x0C || b==0x0D || b==0x20;
		}

		///<summary>PDF delimiters end a name: ( ) &lt; &gt; [ ] { } / %.</summary>
		private static bool IsDelimiter(byte b) {
			return b=='(' || b==')' || b=='<' || b=='>' || b=='[' || b==']' || b=='{' || b=='}' || b=='/' || b=='%';
		}

		///<summary>A character that continues a PDF name or number.</summary>
		private static bool IsRegularChar(byte b) {
			return !IsWhiteSpace(b) && !IsDelimiter(b);
		}

		private static int SkipWhiteSpace(byte[] arrayBytes,int idx) {
			while(idx<arrayBytes.Length && IsWhiteSpace(arrayBytes[idx])) {
				idx++;
			}
			return idx;
		}

		private static bool IsDictionaryStart(byte[] arrayBytes,int idx) {
			return idx+1<arrayBytes.Length && arrayBytes[idx]=='<' && arrayBytes[idx+1]=='<';
		}

		///<summary>True for "object-number generation R" at idx, such as "12 0 R".</summary>
		private static bool IsIndirectReference(byte[] arrayBytes,int idx) {
			int idxAfterObj=SkipDigits(arrayBytes,idx);
			if(idxAfterObj==idx || idxAfterObj>=arrayBytes.Length || !IsWhiteSpace(arrayBytes[idxAfterObj])) {
				return false;
			}
			int idxGen=SkipWhiteSpace(arrayBytes,idxAfterObj);
			int idxAfterGen=SkipDigits(arrayBytes,idxGen);
			if(idxAfterGen==idxGen || idxAfterGen>=arrayBytes.Length || !IsWhiteSpace(arrayBytes[idxAfterGen])) {
				return false;
			}
			int idxR=SkipWhiteSpace(arrayBytes,idxAfterGen);
			if(idxR>=arrayBytes.Length || arrayBytes[idxR]!='R') {
				return false;
			}
			return idxR+1>=arrayBytes.Length || !IsRegularChar(arrayBytes[idxR+1]);
		}

		private static int SkipDigits(byte[] arrayBytes,int idx) {
			while(idx<arrayBytes.Length && arrayBytes[idx]>='0' && arrayBytes[idx]<='9') {
				idx++;
			}
			return idx;
		}
		#endregion Byte helpers
	}
}
