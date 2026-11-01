// Draws sheet text in any script on paper and in PDFs (Open Dental 25.4: sheets support non-Latin languages; backlog package M2-SHEETS).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;
using PdfSharp.Drawing;

namespace OpenDentBusiness {
	///<summary>Text drawing for sheets that works for every script. Latin text goes straight to GraphicsHelper, unchanged. Other text gets the
	///fallback font of SheetFonts and, when it reads right-to-left, a right-to-left layout. In PDFs, text the PDF library can lay out (Cyrillic,
	///Greek, Korean and the like, in a font it can embed) is written as Unicode text with the font embedded; text it cannot lay out (right-to-left
	///and complex scripts, and fonts it cannot embed) is drawn by the print engine at 300 dpi and placed in the PDF as a transparent image.</summary>
	public class SheetTextDrawing {
		///<summary>Device pixels per sheet pixel for text drawn into PDFs as an image. Sheet pixels are 1/100 inch, so 3 gives 300 dpi.</summary>
		public const float RASTER_SCALE=3f;
		///<summary>The largest image, in device pixels (about 48 MB in memory), drawn for one field; larger fields get a lower resolution.</summary>
		public const float MAX_RASTER_PIXELS=12000000f;

		///<summary>Lays out right-to-left text right-to-left: when the text reads right-to-left (SheetScripts.IsRightToLeft), sets the right-to-left
		///flag and swaps near and far alignment, so the text stays on the side of the field the sheet designer chose. Does nothing for other text.
		///</summary>
		public static void ApplyDirection(StringFormat stringFormat,string text) {
			if(stringFormat==null || !SheetScripts.IsRightToLeft(text)) {
				return;
			}
			stringFormat.FormatFlags|=StringFormatFlags.DirectionRightToLeft;
			if(stringFormat.Alignment==StringAlignment.Near) {
				stringFormat.Alignment=StringAlignment.Far;
			}
			else if(stringFormat.Alignment==StringAlignment.Far) {
				stringFormat.Alignment=StringAlignment.Near;
			}
		}

		///<summary>Prints text like GraphicsHelper.DrawString (same 4% larger layout box, tab stops and alignment), with the fallback font and
		///right-to-left layout for text that needs them. Latin text is handed to GraphicsHelper.DrawString unchanged.</summary>
		public static RectangleF DrawString(Graphics g,string str,Font font,Brush brush,Rectangle rectangle,HorizontalAlignment horizontalAlignment) {
			if(str==null || str.Trim()=="") {
				return rectangle;
			}
			string fontName=SheetFonts.GetFontNameForText(font.Name,str);
			bool isSameFont=string.Equals(fontName,font.Name,StringComparison.OrdinalIgnoreCase);
			if(isSameFont && !SheetScripts.IsRightToLeft(str)) {
				return GraphicsHelper.DrawString(g,str,font,brush,rectangle,horizontalAlignment);
			}
			Font fontDraw=font;
			if(!isSameFont) {
				fontDraw=CreateFont(fontName,font.Size,font.Style,font.Unit)??font;
			}
			try {
				RectangleF rectangleActual=new RectangleF(rectangle.X,rectangle.Y,(rectangle.Width*1.04f),(rectangle.Height*1.04f));
				using(StringFormat stringFormat=CreateStringFormat(str,horizontalAlignment)) {
					g.DrawString(str,fontDraw,brush,rectangleActual,stringFormat);
				}
				return rectangleActual;
			}
			finally {
				if(fontDraw!=font) {
					fontDraw.Dispose();
				}
			}
		}

		///<summary>Draws text into a PDF like GraphicsHelper.DrawStringX (rectangleF in sheet pixels). Text that the old PDF encoding can hold goes
		///to GraphicsHelper.DrawStringX unchanged. Other text uses the fallback font (SheetFonts) for fontNameRequested, or for xfont's family when
		///that is null, and is written as embedded Unicode text, or drawn as an image when the PDF library cannot lay it out. Returns rectangleF, as
		///GraphicsHelper.DrawStringX does.</summary>
		public static RectangleF DrawStringX(XGraphics xg,string str,XFont xfont,XBrush xbrush,RectangleF rectangleF,
			HorizontalAlignment horizontalAlignment,string fontNameRequested=null)
		{
			if(str==null || str.Trim()=="") {
				return rectangleF;
			}
			if(SheetScripts.IsWinAnsiEncodable(str)) {
				return GraphicsHelper.DrawStringX(xg,str,xfont,xbrush,rectangleF,horizontalAlignment);
			}
			string fontName=SheetFonts.GetFontNameForText(SheetFonts.NormalizeFontName(fontNameRequested??xfont.Name),str);
			if(!IsVectorInPdf(str,fontName,xfont.Style)) {
				try {
					DrawStringAsImageX(xg,str,fontName,xfont,GetColor(xbrush),rectangleF,horizontalAlignment);
					return rectangleF;
				}
				catch(Exception) {
					//For example out of memory for a huge field. Fall back to writing the text, which is how it looked before 25.4 at worst.
				}
			}
			XFont xfontText=xfont;
			if(SheetFonts.CanEmbedInPdf(fontName,xfont.Style)) {
				xfontText=new XFont(fontName,xfont.Size,xfont.Style,SheetFonts.GetPdfFontOptionsUnicode());
			}
			return GraphicsHelper.DrawStringX(xg,str,xfontText,xbrush,rectangleF,horizontalAlignment);
		}

		///<summary>True when text that the old PDF encoding cannot hold can still be written into a PDF as text in this font: it has no right-to-left
		///or complex-script characters and the PDF library can embed the font. Otherwise DrawStringX draws it as an image.</summary>
		public static bool IsVectorInPdf(string str,string fontName,XFontStyle xFontStyle) {
			return !SheetScripts.NeedsShaping(str) && SheetFonts.CanEmbedInPdf(fontName,xFontStyle);
		}

		///<summary>Draws the text with the print engine (which shapes complex scripts and lays out right-to-left text) onto a transparent 300 dpi
		///image, wrapped as GraphicsHelper.DrawString wraps it on paper, and places the image in the PDF at the field's position, left, right or
		///centered like GraphicsHelper.DrawStringX places text. Only whole lines that fit in the field are drawn, but always at least the first
		///line, as in GraphicsHelper.DrawStringX.</summary>
		private static void DrawStringAsImageX(XGraphics xg,string str,string fontName,XFont xfont,Color color,RectangleF rectangleF,
			HorizontalAlignment horizontalAlignment)
		{
			FontStyle fontStyle=FontStyle.Regular;
			if(xfont.Bold) {
				fontStyle|=FontStyle.Bold;
			}
			if(xfont.Italic) {
				fontStyle|=FontStyle.Italic;
			}
			if(xfont.Underline) {
				fontStyle|=FontStyle.Underline;
			}
			if(xfont.Strikeout) {
				fontStyle|=FontStyle.Strikeout;
			}
			using(Font font=CreateFont(fontName,(float)xfont.Size,fontStyle,GraphicsUnit.Point)??new Font(FontFamily.GenericSansSerif,(float)xfont.Size,fontStyle))
			using(StringFormat stringFormat=CreateStringFormat(str,horizontalAlignment)) {
				stringFormat.FormatFlags|=StringFormatFlags.LineLimit;
				//Sheet pixels are 1/100 inch; the print engine lays text out in a box 4% larger than the field (GraphicsHelper.DrawString).
				float widthLayout=rectangleF.Width*1.04f;
				float heightLayout=rectangleF.Height*1.04f;
				float heightImage;
				using(Bitmap bitmapMeasure=new Bitmap(1,1)) {
					bitmapMeasure.SetResolution(100*RASTER_SCALE,100*RASTER_SCALE);
					using(Graphics gMeasure=Graphics.FromImage(bitmapMeasure)) {
						gMeasure.PageUnit=GraphicsUnit.Pixel;
						gMeasure.TextRenderingHint=TextRenderingHint.AntiAlias;
						float heightLine=font.GetHeight(gMeasure)/RASTER_SCALE;
						heightLayout=Math.Max(heightLayout,heightLine);
						SizeF sizeText=gMeasure.MeasureString(str,font,new SizeF(widthLayout*RASTER_SCALE,heightLayout*RASTER_SCALE),stringFormat);
						heightImage=Math.Min(heightLayout,sizeText.Height/RASTER_SCALE+1);
					}
				}
				//A very large field (a long consent paragraph) gets a lower resolution, so the image stays under MAX_RASTER_PIXELS.
				float scale=RASTER_SCALE;
				if(widthLayout*heightImage*scale*scale>MAX_RASTER_PIXELS) {
					scale=Math.Max(1f,(float)Math.Sqrt(MAX_RASTER_PIXELS/(widthLayout*heightImage)));
				}
				int widthPixels=(int)Math.Ceiling(widthLayout*scale);
				int heightPixels=(int)Math.Ceiling(heightImage*scale);
				if(widthPixels<=0 || heightPixels<=0) {
					return;
				}
				using(Bitmap bitmap=new Bitmap(widthPixels,heightPixels,PixelFormat.Format32bppArgb)) {
					bitmap.SetResolution(100*scale,100*scale);
					using(Graphics g=Graphics.FromImage(bitmap))
					using(SolidBrush solidBrush=new SolidBrush(color)) {
						g.Clear(Color.Transparent);
						g.PageUnit=GraphicsUnit.Pixel;
						g.TextRenderingHint=TextRenderingHint.AntiAlias;
						g.DrawString(str,font,solidBrush,new RectangleF(0,0,widthLayout*scale,heightLayout*scale),stringFormat);
					}
					//The layout box is wider than the field; anchor it like GraphicsHelper.DrawStringX anchors text, so right-aligned and centered text
					//lines up with the other text of the PDF. The print engine also leaves about a sixth of an em free at each end of a line, which
					//PDF text does not, so left- and right-aligned images move out by that much.
					float paddingEm=font.SizeInPoints/72f*100f/6f;
					float xImage=rectangleF.X-paddingEm;
					if(horizontalAlignment==HorizontalAlignment.Right) {
						xImage=rectangleF.Right-widthLayout+paddingEm;
					}
					else if(horizontalAlignment==HorizontalAlignment.Center) {
						xImage=rectangleF.X+(rectangleF.Width-widthLayout)/2f;
					}
					XImage ximage=XImage.FromGdiPlusImage(bitmap);
					xg.DrawImage(ximage,GraphicsHelper.PixelsToPoints(xImage),GraphicsHelper.PixelsToPoints(rectangleF.Y),
						GraphicsHelper.PixelsToPoints(widthPixels/scale),GraphicsHelper.PixelsToPoints(heightPixels/scale));
				}
			}
		}

		///<summary>Tab stops and alignment as GraphicsHelper.DrawString sets them, plus the right-to-left layout of ApplyDirection.</summary>
		private static StringFormat CreateStringFormat(string str,HorizontalAlignment horizontalAlignment) {
			StringFormat stringFormat=new StringFormat();
			stringFormat.SetTabStops(0.0f,new float[] { 50.0f });
			stringFormat.Alignment=StringAlignment.Near;
			if(horizontalAlignment==HorizontalAlignment.Center) {
				stringFormat.Alignment=StringAlignment.Center;
			}
			if(horizontalAlignment==HorizontalAlignment.Right) {
				stringFormat.Alignment=StringAlignment.Far;
			}
			ApplyDirection(stringFormat,str);
			return stringFormat;
		}

		///<summary>The font in the requested style, or in a style the family has when it lacks that one; null when the family is not installed.
		///</summary>
		private static Font CreateFont(string fontName,float emSize,FontStyle fontStyle,GraphicsUnit graphicsUnit) {
			if(!SheetFonts.IsFontInstalled(fontName)) {
				return null;
			}
			try {
				return new Font(fontName,emSize,fontStyle,graphicsUnit);
			}
			catch(ArgumentException) {
				return SheetFonts.CreateFontAnyStyle(fontName,emSize,graphicsUnit);
			}
		}

		private static Color GetColor(XBrush xbrush) {
			if(xbrush is XSolidBrush xsolidBrush) {
				return xsolidBrush.Color.ToGdiColor();
			}
			return Color.Black;
		}
	}
}
