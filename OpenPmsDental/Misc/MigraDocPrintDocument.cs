// Prints a MigraDoc document. MigraDoc 1.2 had a PrintDocument for this (MigraDoc.Rendering.Printing.MigraDocPrintDocument); MigraDoc 6 has none,
// so this one does the same: each page rendered with the MigraDoc renderer onto the printer's graphics.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using MigraDoc.Rendering;
using PdfSharp;
using PdfSharp.Drawing;

namespace OpenPmsDental {
	///<summary>A PrintDocument for a MigraDoc document, for printing and for print preview. Set Renderer (prepared with PrepareDocument) and
	///PrinterSettings, then call Print or hand it to a preview. Prints all pages, the FromPage to ToPage range, or SelectedPage, by the printer
	///settings' PrintRange; each page is printed in the orientation MigraDoc laid it out in, at its full size from the paper's corner.</summary>
	public class MigraDocPrintDocument:PrintDocument {
		///<summary>Device capability indexes for GetDeviceCaps: the distance from the paper's corner to the printable area, in device units.</summary>
		private const int PHYSICALOFFSETX=112;
		private const int PHYSICALOFFSETY=113;
		///<summary>The page printed next, 1-based. -1 when not printing.</summary>
		private int _pageNumber=-1;
		///<summary>Pages still to print, this one included.</summary>
		private int _pageCount;

		///<summary>The prepared MigraDoc renderer whose pages are printed.</summary>
		public DocumentRenderer Renderer;
		///<summary>The page printed when PrinterSettings.PrintRange is Selection. 1-based.</summary>
		public int SelectedPage;

		public MigraDocPrintDocument() {
			DefaultPageSettings.Margins=new Margins(0,0,0,0);
			OriginAtMargins=false;
		}

		public MigraDocPrintDocument(DocumentRenderer documentRenderer):this() {
			Renderer=documentRenderer;
		}

		protected override void OnBeginPrint(PrintEventArgs e) {
			base.OnBeginPrint(e);
			if(e.Cancel) {
				return;
			}
			if(Renderer==null) {
				throw new InvalidOperationException("A MigraDoc DocumentRenderer is needed to print.");
			}
			switch(PrinterSettings.PrintRange) {
				case PrintRange.AllPages:
					_pageNumber=1;
					_pageCount=Renderer.FormattedDocument.PageCount;
					break;
				case PrintRange.SomePages:
					_pageNumber=PrinterSettings.FromPage;
					_pageCount=PrinterSettings.ToPage-PrinterSettings.FromPage+1;
					break;
				case PrintRange.Selection:
					_pageNumber=SelectedPage;
					_pageCount=1;
					break;
				default:
					e.Cancel=true;
					break;
			}
		}

		protected override void OnQueryPageSettings(QueryPageSettingsEventArgs e) {
			base.OnQueryPageSettings(e);
			if(e.Cancel) {
				return;
			}
			PageInfo pageInfo=Renderer.FormattedDocument.GetPageInfo(_pageNumber);
			e.PageSettings.Landscape=(pageInfo.Orientation==PageOrientation.Landscape);
		}

		protected override void OnPrintPage(PrintPageEventArgs e) {
			base.OnPrintPage(e);
			if(e.Cancel) {
				return;
			}
			try {
				Graphics graphics=e.Graphics;
				//The graphics start at the printable area; move them back to the paper's corner, where MigraDoc measures from.
				IntPtr hdc=graphics.GetHdc();
				int xOffset=GetDeviceCaps(hdc,PHYSICALOFFSETX);
				int yOffset=GetDeviceCaps(hdc,PHYSICALOFFSETY);
				graphics.ReleaseHdc(hdc);
				graphics.TranslateTransform(-xOffset*100/graphics.DpiX,-yOffset*100/graphics.DpiY);
				//Page bounds are in 1/100 inch, already swapped for landscape; MigraDoc draws in points.
				XSize xSize=new XSize(e.PageSettings.Bounds.Width/100.0*72,e.PageSettings.Bounds.Height/100.0*72);
				graphics.ScaleTransform(100f/72f,100f/72f);
				XGraphics xGraphics=XGraphics.FromGraphics(graphics,xSize);
				Renderer.RenderPage(xGraphics,_pageNumber);
			}
			catch(Exception) {
				e.Cancel=true;
			}
			_pageNumber++;
			_pageCount--;
			e.HasMorePages=(_pageCount>0);
		}

		protected override void OnEndPrint(PrintEventArgs e) {
			base.OnEndPrint(e);
			_pageNumber=-1;
		}

		[DllImport("gdi32.dll")]
		private static extern int GetDeviceCaps(IntPtr hdc,int nIndex);
	}
}
