// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace ImagingDeviceManager.Imaging {
	///<summary>Writes scanned pages into a PDF with PdfSharp, one image per page, each page sized to the scanned area.
	///Black and white pages are stored losslessly (PdfSharp encodes 1 bit images compactly). Gray and color pages are stored
	///as JPEG at the requested quality, or losslessly when the quality is 100 or more. Pages are compressed as they arrive so
	///that a long feeder batch does not keep every raw bitmap in memory.</summary>
	internal sealed class PdfPageWriter:IDisposable {
		///<summary>Used when a page does not say its resolution, so the page still gets a sensible physical size.</summary>
		public const int DefaultDpi=96;
		private readonly int _quality;
		private PdfDocument _pdfDocument;
		///<summary>Images, streams and XImages that PdfSharp may read again when the document is saved.</summary>
		private readonly List<IDisposable> _listKeepAlive=new List<IDisposable>();
		///<summary>Pages added. Kept here because PDFsharp 6 refuses to answer PageCount once the document is saved.</summary>
		private int _pageCount;

		///<summary>quality is the JPEG quality, 0 to 100. 100 or more means lossless.</summary>
		public PdfPageWriter(int quality) {
			_quality=quality;
			_pdfDocument=new PdfDocument();
		}

		///<summary>The number of pages added, also after Save.</summary>
		public int PageCount => _pageCount;

		public void AddPage(DibImage dib) {
			int xDpi=dib.XResolutionDpi>0 ? dib.XResolutionDpi : DefaultDpi;
			int yDpi=dib.YResolutionDpi>0 ? dib.YResolutionDpi : DefaultDpi;
			double widthPoints=dib.Width*72.0/xDpi;
			double heightPoints=dib.Height*72.0/yDpi;
			Image image=CreatePageImage(dib);
			XImage xImage=XImage.FromGdiPlusImage(image);
			_listKeepAlive.Add(xImage);
			PdfPage pdfPage=_pdfDocument.AddPage();
			pdfPage.Width=XUnit.FromPoint(widthPoints);
			pdfPage.Height=XUnit.FromPoint(heightPoints);
			using(XGraphics xGraphics=XGraphics.FromPdfPage(pdfPage)) {
				xGraphics.DrawImage(xImage,0,0,widthPoints,heightPoints);
			}
			_pageCount++;
		}

		private Image CreatePageImage(DibImage dib) {
			Bitmap bitmap=dib.ToBitmap();
			if(dib.BitCount==1 || _quality>=100) {
				_listKeepAlive.Add(bitmap);
				return bitmap;
			}
			MemoryStream memoryStream=new MemoryStream();
			try {
				using(EncoderParameters encoderParameters=new EncoderParameters(1)) {
					encoderParameters.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,(long)Math.Max(0,_quality));
					bitmap.Save(memoryStream,GetJpegCodec(),encoderParameters);
				}
			}
			finally {
				bitmap.Dispose();
			}
			memoryStream.Position=0;
			_listKeepAlive.Add(memoryStream);//Image.FromStream needs the stream for the life of the image.
			Image imageJpeg=Image.FromStream(memoryStream);
			_listKeepAlive.Add(imageJpeg);
			return imageJpeg;
		}

		private static ImageCodecInfo GetJpegCodec() {
			ImageCodecInfo codecInfo=ImageCodecInfo.GetImageEncoders().FirstOrDefault(x => x.FormatID==ImageFormat.Jpeg.Guid);
			if(codecInfo==null) {
				throw new ImagingDeviceException("The Windows JPEG encoder is not available.",EZTwainErrorCode.EZTEC_PDF_WRITE_ERR);
			}
			return codecInfo;
		}

		public void Save(string path) {
			if(PageCount==0) {
				throw new ImagingDeviceException("There are no pages to save.",EZTwainErrorCode.EZTEC_0_PAGES);
			}
			_pdfDocument.Save(path);
		}

		public void Dispose() {
			if(_pdfDocument!=null) {
				_pdfDocument.Dispose();
				_pdfDocument=null;
			}
			for(int i=_listKeepAlive.Count-1;i>=0;i--) {
				try {
					_listKeepAlive[i].Dispose();
				}
				catch {
					//Best effort cleanup.
				}
			}
			_listKeepAlive.Clear();
		}
	}
}
