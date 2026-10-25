// B-PRINT: inserter barcode on printed statements (Open Dental 25.3).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CodeBase;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace OpenDentBusiness {
	///<summary>A machine-readable barcode on every printed statement page, so an envelope-inserting machine can keep the pages of one statement
	///together. Open Dental's release notes give no symbology, content or placement, so all three are this fork's design (port note B-PRINT):
	///- Off by default. The Billing Defaults window turns it on (preference BillingStatementInserterBarcode, a fork preference stored as a plain
	///  preference row by the fork migration step InserterBarcodePrefStep).
	///- Code 39, digits only, 12 characters: the statement's sequence number in the print run (5 digits, starting at 1), the page number (3), the
	///  page count (3), and an end-of-set flag (1 on the statement's last page, else 0). For example 000070020021 is page 2 of 2 of the 7th statement.
	///- Placement: a Special field named StatementInserterBarcode on a custom statement layout sets the position and size. Without one (the internal
	///  layout, or a custom layout without the field) it goes in the top-right margin.
	///- Printed statements only: the batch print document of the Billing window and a statement printed on its own. The copy saved to Imaging and
	///  e-mailed or electronic statements never carry it.</summary>
	public static class StatementInserterBarcode {
		///<summary>The Special sheet field that places the barcode on a custom statement layout.</summary>
		public const string FIELD_NAME="StatementInserterBarcode";
		///<summary>The preference row (fork preference; "1" turns the barcode on).</summary>
		public const string PREF_NAME="BillingStatementInserterBarcode";
		///<summary>Default size in sheet units (1/100 inch): 2 inches wide, a quarter inch high.</summary>
		public const int DEFAULT_WIDTH=200;
		public const int DEFAULT_HEIGHT=25;
		///<summary>Distance of the default barcode from the right edge and the top edge of the page, in sheet units. Statements leave the top 0.4 inch
		///free (print margin), so the barcode sits between 0.12 and 0.37 inch from the top.</summary>
		public const int DEFAULT_MARGIN_RIGHT=25;
		public const int DEFAULT_MARGIN_TOP=12;
		///<summary>Largest numbers the fixed-width fields hold. Sequence numbers wrap after 99999; page numbers and counts stop at 999.</summary>
		public const int MAX_SEQUENCE=99999;
		public const int MAX_PAGE=999;

		#region Setting
		///<summary>True if the office turned the inserter barcode on. False when the preference row is missing.</summary>
		public static bool IsEnabled() {
			if(!PrefC.ContainsKey(PREF_NAME)) {
				return false;
			}
			return PIn.Bool(PrefC.GetRaw(PREF_NAME));
		}

		///<summary>True if the database has the preference row (the fork migration step InserterBarcodePrefStep adds it).</summary>
		public static bool IsAvailable() {
			return PrefC.ContainsKey(PREF_NAME);
		}

		///<summary>Saves the setting. Returns true if it changed. Does nothing and returns false when the preference row is missing.</summary>
		public static bool SetEnabled(bool isEnabled) {
			if(!IsAvailable()) {
				return false;
			}
			return Prefs.UpdateRaw(PREF_NAME,POut.Bool(isEnabled));
		}
		#endregion Setting

		#region Content
		///<summary>The 12 digits encoded for one page. sequence is 1-based within the print run (wraps after 99999, so 100000 is 00000);
		///pageNumber is 1-based. Throws for a page outside 1..pageCount.</summary>
		public static string GetContent(int sequence,int pageNumber,int pageCount) {
			if(pageCount<1 || pageNumber<1 || pageNumber>pageCount) {
				throw new ArgumentOutOfRangeException(nameof(pageNumber),"Page "+pageNumber+" of "+pageCount+" is not a page of the statement.");
			}
			if(sequence<1) {
				throw new ArgumentOutOfRangeException(nameof(sequence),"Sequence numbers start at 1.");
			}
			int sequenceWrapped=sequence%(MAX_SEQUENCE+1);
			string endOfSet=(pageNumber==pageCount) ? "1" : "0";
			return sequenceWrapped.ToString("00000")+Math.Min(pageNumber,MAX_PAGE).ToString("000")+Math.Min(pageCount,MAX_PAGE).ToString("000")+endOfSet;
		}
		#endregion Content

		#region Placement
		///<summary>The barcode rectangle in sheet units (1/100 inch) from the layout's StatementInserterBarcode field. Null if there is none.</summary>
		public static Rectangle? GetFieldBounds(IEnumerable<SheetFieldDef> listSheetFieldDefs) {
			SheetFieldDef sheetFieldDef=listSheetFieldDefs?.FirstOrDefault(x => IsBarcodeField(x.FieldType,x.FieldName));
			if(sheetFieldDef==null || sheetFieldDef.Width<=0 || sheetFieldDef.Height<=0) {
				return null;
			}
			return new Rectangle(sheetFieldDef.XPos,sheetFieldDef.YPos,sheetFieldDef.Width,sheetFieldDef.Height);
		}

		///<summary>The barcode rectangle in sheet units from a filled sheet's StatementInserterBarcode field. Null if there is none.</summary>
		public static Rectangle? GetFieldBounds(IEnumerable<SheetField> listSheetFields) {
			SheetField sheetField=listSheetFields?.FirstOrDefault(x => IsBarcodeField(x.FieldType,x.FieldName));
			if(sheetField==null || sheetField.Width<=0 || sheetField.Height<=0) {
				return null;
			}
			return new Rectangle(sheetField.XPos,sheetField.YPos,sheetField.Width,sheetField.Height);
		}

		public static bool IsBarcodeField(SheetFieldType sheetFieldType,string fieldName) {
			return sheetFieldType==SheetFieldType.Special && fieldName==FIELD_NAME;
		}

		///<summary>The default rectangle, in the top-right margin of a page of the given width (sheet units).</summary>
		public static Rectangle GetDefaultBounds(int pageWidth) {
			return new Rectangle(pageWidth-DEFAULT_MARGIN_RIGHT-DEFAULT_WIDTH,DEFAULT_MARGIN_TOP,DEFAULT_WIDTH,DEFAULT_HEIGHT);
		}

		///<summary>The fields of the layout that the barcode rectangle touches, so the sheet editor can warn that the barcode needs a clear zone.
		///Only fields that print something count; the barcode field itself and Parameter fields are skipped.</summary>
		public static List<SheetFieldDef> GetOverlappingFields(SheetDef sheetDef) {
			List<SheetFieldDef> listOverlaps=new List<SheetFieldDef>();
			if(sheetDef?.SheetFieldDefs==null) {
				return listOverlaps;
			}
			Rectangle? rectangleBarcode=GetFieldBounds(sheetDef.SheetFieldDefs);
			if(rectangleBarcode==null) {
				return listOverlaps;
			}
			for(int i=0;i<sheetDef.SheetFieldDefs.Count;i++) {
				SheetFieldDef sheetFieldDef=sheetDef.SheetFieldDefs[i];
				if(IsBarcodeField(sheetFieldDef.FieldType,sheetFieldDef.FieldName) || sheetFieldDef.FieldType==SheetFieldType.Parameter) {
					continue;
				}
				//Lines have no area; give them a width of 1 so a line through the barcode counts.
				Rectangle rectangleField=new Rectangle(sheetFieldDef.XPos,sheetFieldDef.YPos,Math.Max(1,sheetFieldDef.Width),Math.Max(1,sheetFieldDef.Height));
				if(rectangleBarcode.Value.IntersectsWith(rectangleField)) {
					listOverlaps.Add(sheetFieldDef);
				}
			}
			return listOverlaps;
		}
		#endregion Placement

		#region Drawing
		///<summary>Draws the barcode with System.Drawing (printing). bounds are in the graphics' units, for example sheet units on a printer page.
		///The rectangle is first filled white, so nothing under it disturbs the scan.</summary>
		public static void Draw(Graphics g,RectangleF bounds,string content) {
			List<Tuple<double,double>> listBars=Code39Barcode.GetBars(content,out double totalWidth);
			double scale=bounds.Width/totalWidth;
			g.FillRectangle(Brushes.White,bounds);
			for(int i=0;i<listBars.Count;i++) {
				g.FillRectangle(Brushes.Black,(float)(bounds.X+listBars[i].Item1*scale),bounds.Y,(float)(listBars[i].Item2*scale),bounds.Height);
			}
		}

		///<summary>Draws the barcode on a PDF page. boundsSheetUnits is in sheet units (1/100 inch) from the top-left corner of the page.</summary>
		public static void Draw(XGraphics xGraphics,RectangleF boundsSheetUnits,string content) {
			List<Tuple<double,double>> listBars=Code39Barcode.GetBars(content,out double totalWidth);
			double x=ToPoints(boundsSheetUnits.X);
			double y=ToPoints(boundsSheetUnits.Y);
			double width=ToPoints(boundsSheetUnits.Width);
			double height=ToPoints(boundsSheetUnits.Height);
			double scale=width/totalWidth;
			xGraphics.DrawRectangle(XBrushes.White,x,y,width,height);
			for(int i=0;i<listBars.Count;i++) {
				xGraphics.DrawRectangle(XBrushes.Black,x+listBars[i].Item1*scale,y,listBars[i].Item2*scale,height);
			}
		}

		///<summary>Sheet units (1/100 inch) to PDF points (1/72 inch).</summary>
		public static double ToPoints(double sheetUnits) {
			return sheetUnits*72d/100d;
		}

		///<summary>Stamps one page of the Billing window's print document. sequence is the statement's 1-based place among the statements printed in
		///this run; pageNumber is 1-based within the statement. Uses the statement's layout to place the barcode. Does nothing when the barcode is
		///off. Never throws: a problem is logged and the page prints without a barcode. Returns true if the page was stamped.</summary>
		public static bool StampBatchPage(PdfPage pdfPage,Statement statement,int sequence,int pageNumber,int pageCount) {
			try {
				if(!IsEnabled()) {
					return false;
				}
				SheetDef sheetDef=null;
				try {
					sheetDef=SheetUtil.GetStatementSheetDef(statement);
				}
				catch(Exception ex) {
					ex.DoNothing();//Fall back to the default placement.
				}
				return StampPdfPage(pdfPage,sheetDef?.SheetFieldDefs,GetContent(sequence,pageNumber,pageCount));
			}
			catch(Exception ex) {
				Logger.WriteException(ex,"StatementInserterBarcode");
				return false;
			}
		}

		///<summary>Draws the barcode on the PDF page, placed by the layout's field when listSheetFieldDefs has one, otherwise in the top-right margin
		///of the page. Returns true if drawn.</summary>
		public static bool StampPdfPage(PdfPage pdfPage,IEnumerable<SheetFieldDef> listSheetFieldDefs,string content) {
			Rectangle? rectangleField=GetFieldBounds(listSheetFieldDefs);
			int pageWidthSheetUnits=(int)Math.Round(pdfPage.Width.Point*100d/72d);
			Rectangle rectangle=rectangleField??GetDefaultBounds(pageWidthSheetUnits);
			using(XGraphics xGraphics=XGraphics.FromPdfPage(pdfPage,XGraphicsPdfPageOptions.Append)) {
				Draw(xGraphics,rectangle,content);
			}
			return true;
		}

		///<summary>Draws the barcode on a printed statement page (System.Drawing printing, sheet units). sequence is the copy's 1-based number when
		///one statement is printed on its own. Does nothing when the barcode is off or the sheet is not a statement. Never throws.</summary>
		public static bool DrawOnPrintedPage(Graphics g,Sheet sheet,int sequence,int pageNumber,int pageCount) {
			try {
				if(sheet==null || sheet.SheetType!=SheetTypeEnum.Statement || !IsEnabled()) {
					return false;
				}
				int pageWidth=sheet.IsLandscape ? sheet.Height : sheet.Width;
				Rectangle rectangle=GetFieldBounds(sheet.SheetFields)??GetDefaultBounds(pageWidth);
				Draw(g,rectangle,GetContent(sequence,pageNumber,pageCount));
				return true;
			}
			catch(Exception ex) {
				Logger.WriteException(ex,"StatementInserterBarcode");
				return false;
			}
		}
		#endregion Drawing
	}
}
