// B-PRINT: inserter barcode on printed statements (Open Dental 25.3).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using CodeBase;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace OpenDentBusiness {
	///<summary>A machine-readable barcode on every printed statement page, so an envelope-inserting machine can keep the pages of one statement
	///together. Open Dental's release notes give no symbology, content or placement, so all three are this fork's design (port note B-PRINT):
	///- Off by default. The Billing Defaults window turns it on (preference row BillingStatementInserterBarcode, a fork preference that is written
	///  the first time the office saves the setting; a database without the row reads as off).
	///- Code 39, digits only, 12 characters: the statement's sequence number in the print run (5 digits, starting at 1), the page number (3), the
	///  page count (3), and an end-of-set flag (1 on the statement's last page, else 0). For example 000070020021 is page 2 of 2 of the 7th statement.
	///- Placement: a Special field named StatementInserterBarcode on a custom statement layout sets the position and size (on the first page; the
	///  same spot is used on every page). Without one (the internal layout, or a custom layout without the field) it goes in the top-right margin.
	///- Printed statements only: the print document of the Billing window (one sequence for the whole run) and a statement printed on its own (each
	///  copy is a set). The copy saved to Imaging, and e-mailed or electronic statements, never carry it.</summary>
	public static class StatementInserterBarcode {
		///<summary>The Special sheet field that places the barcode on a custom statement layout.</summary>
		public const string FIELD_NAME="StatementInserterBarcode";
		///<summary>The preference row (fork preference; "1" turns the barcode on).</summary>
		public const string PREF_NAME="BillingStatementInserterBarcode";
		///<summary>Default size of the Special field on a custom layout, in sheet units (1/100 inch): 2 inches wide, a quarter inch high.</summary>
		public const int DEFAULT_WIDTH=200;
		public const int DEFAULT_HEIGHT=25;
		///<summary>The default barcode in the top margin: its distance from the right edge and the top edge of the page, and its height, in sheet units.
		///Statements leave the top 0.4 inch free (print margin), and most printers cannot print the top sixth of an inch, so the barcode sits between
		///0.16 and 0.39 inch from the top.</summary>
		public const int DEFAULT_MARGIN_RIGHT=25;
		public const int DEFAULT_MARGIN_TOP=16;
		public const int DEFAULT_MARGIN_HEIGHT=23;
		///<summary>Largest numbers the fixed-width fields hold. Sequence numbers wrap after 99999; page numbers and counts stop at 999.</summary>
		public const int MAX_SEQUENCE=99999;
		public const int MAX_PAGE=999;

		#region Setting
		///<summary>True if the office turned the inserter barcode on. False when the preference row is missing or the cache is not loaded.</summary>
		public static bool IsEnabled() {
			Meth.NoCheckMiddleTierRole();
			try {
				if(Prefs.DictIsNull() || !Prefs.GetContainsKey(PREF_NAME)) {
					return false;
				}
				return PIn.Bool(Prefs.GetOne(PREF_NAME).ValueString);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>Saves the setting, inserting the preference row the first time it is turned on, and updates this workstation's cache. Returns true
		///if it changed. Turning it off on a database without the row writes nothing. The caller signals InvalidType.Prefs so other workstations
		///reload.</summary>
		public static bool SetEnabled(bool isEnabled) {
			Meth.NoCheckMiddleTierRole();
			if(IsEnabled()==isEnabled) {
				return false;
			}
			string value=POut.Bool(isEnabled);
			UpsertPref(value);
			if(!Prefs.DictIsNull()) {
				Prefs.UpdateValueForKey(new Pref { PrefName=PREF_NAME,ValueString=value });
			}
			return true;
		}

		///<summary>Writes the preference row straight to the table, inserting it when missing.</summary>
		public static void UpsertPref(string value) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),value);
				return;
			}
			string count=Db.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+POut.String(PREF_NAME)+"'");
			if(PIn.Long(count)==0) {
				Db.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('"+POut.String(PREF_NAME)+"','"+POut.String(value)+"')");
				return;
			}
			Db.NonQ("UPDATE preference SET ValueString='"+POut.String(value)+"' WHERE PrefName='"+POut.String(PREF_NAME)+"'");
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
			SheetFieldDef sheetFieldDef=listSheetFieldDefs?.FirstOrDefault(x => x!=null && IsBarcodeField(x.FieldType,x.FieldName));
			if(sheetFieldDef==null || sheetFieldDef.Width<=0 || sheetFieldDef.Height<=0) {
				return null;
			}
			return new Rectangle(sheetFieldDef.XPos,sheetFieldDef.YPos,sheetFieldDef.Width,sheetFieldDef.Height);
		}

		///<summary>The barcode rectangle in sheet units from a filled sheet's StatementInserterBarcode field. Null if there is none.</summary>
		public static Rectangle? GetFieldBounds(IEnumerable<SheetField> listSheetFields) {
			SheetField sheetField=listSheetFields?.FirstOrDefault(x => x!=null && IsBarcodeField(x.FieldType,x.FieldName));
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
			return new Rectangle(pageWidth-DEFAULT_MARGIN_RIGHT-DEFAULT_WIDTH,DEFAULT_MARGIN_TOP,DEFAULT_WIDTH,DEFAULT_MARGIN_HEIGHT);
		}

		///<summary>Where the barcode goes on every page: the layout's field when there is one and it fits on the first page, otherwise the default spot
		///in the top-right margin. All in sheet units from the top-left corner of the page.</summary>
		public static Rectangle GetPageBounds(Rectangle? rectangleField,int pageWidth,int pageHeight) {
			if(rectangleField!=null) {
				Rectangle rectangle=rectangleField.Value;
				if(rectangle.X>=0 && rectangle.Y>=0 && rectangle.Right<=pageWidth && (pageHeight<=0 || rectangle.Bottom<=pageHeight)) {
					return rectangle;
				}
			}
			return GetDefaultBounds(pageWidth);
		}

		///<summary>The fields of the layout that the barcode rectangle touches, so the sheet editor can warn that the barcode needs a clear zone.
		///Only fields of the same language and layout mode as the barcode field that print something count; the barcode field itself and Parameter
		///fields are skipped.</summary>
		public static List<SheetFieldDef> GetOverlappingFields(SheetDef sheetDef) {
			List<SheetFieldDef> listOverlaps=new List<SheetFieldDef>();
			if(sheetDef?.SheetFieldDefs==null) {
				return listOverlaps;
			}
			List<SheetFieldDef> listBarcodes=sheetDef.SheetFieldDefs.FindAll(x => x!=null && IsBarcodeField(x.FieldType,x.FieldName) && x.Width>0 && x.Height>0);
			for(int b=0;b<listBarcodes.Count;b++) {
				SheetFieldDef sheetFieldDefBarcode=listBarcodes[b];
				Rectangle rectangleBarcode=new Rectangle(sheetFieldDefBarcode.XPos,sheetFieldDefBarcode.YPos,sheetFieldDefBarcode.Width,sheetFieldDefBarcode.Height);
				for(int i=0;i<sheetDef.SheetFieldDefs.Count;i++) {
					SheetFieldDef sheetFieldDef=sheetDef.SheetFieldDefs[i];
					if(sheetFieldDef==null
						|| IsBarcodeField(sheetFieldDef.FieldType,sheetFieldDef.FieldName)
						|| sheetFieldDef.FieldType==SheetFieldType.Parameter
						|| sheetFieldDef.LayoutMode!=sheetFieldDefBarcode.LayoutMode
						|| (sheetFieldDef.Language??"")!=(sheetFieldDefBarcode.Language??""))
					{
						continue;
					}
					//Lines have no area; give them a width of 1 so a line through the barcode counts.
					Rectangle rectangleField=new Rectangle(sheetFieldDef.XPos,sheetFieldDef.YPos,Math.Max(1,sheetFieldDef.Width),Math.Max(1,sheetFieldDef.Height));
					if(rectangleBarcode.IntersectsWith(rectangleField) && !listOverlaps.Contains(sheetFieldDef)) {
						listOverlaps.Add(sheetFieldDef);
					}
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

		///<summary>Sheet units of a PDF page's width and height (points to 1/100 inch).</summary>
		public static Size GetPageSizeSheetUnits(PdfPage pdfPage) {
			return new Size((int)Math.Round(pdfPage.Width.Point*100d/72d),(int)Math.Round(pdfPage.Height.Point*100d/72d));
		}

		///<summary>Draws the barcode on the PDF page, placed by the layout's field when listSheetFieldDefs has one, otherwise in the top-right margin
		///of the page.</summary>
		public static void StampPdfPage(PdfPage pdfPage,IEnumerable<SheetFieldDef> listSheetFieldDefs,string content) {
			Size sizePage=GetPageSizeSheetUnits(pdfPage);
			Rectangle rectangle=GetPageBounds(GetFieldBounds(listSheetFieldDefs),sizePage.Width,sizePage.Height);
			using(XGraphics xGraphics=XGraphics.FromPdfPage(pdfPage,XGraphicsPdfPageOptions.Append)) {
				Draw(xGraphics,rectangle,content);
			}
		}

		///<summary>Stamps the Billing window's print document, which holds the pages of every printed statement one after the other.
		///listPageCounts holds each statement's page count in print order; listLayouts holds each statement's layout fields in the same order (null,
		///shorter, or a null entry: the default spot). Sequence numbers start at 1 for the run. Pages past the counted ones are left alone, and so is
		///a statement whose pages are missing. Returns the number of pages stamped.</summary>
		public static int StampPrintDocument(PdfDocument pdfDocument,IList<int> listPageCounts,IList<IEnumerable<SheetFieldDef>> listLayouts=null) {
			if(pdfDocument==null || listPageCounts==null) {
				return 0;
			}
			int idxPage=0;
			int countStamped=0;
			for(int s=0;s<listPageCounts.Count;s++) {
				int pageCount=listPageCounts[s];
				if(pageCount<1) {
					continue;
				}
				if(idxPage+pageCount>pdfDocument.PageCount) {
					break;//The document does not hold this statement's pages; never stamp a page of another statement.
				}
				IEnumerable<SheetFieldDef> listSheetFieldDefs=null;
				if(listLayouts!=null && s<listLayouts.Count) {
					listSheetFieldDefs=listLayouts[s];
				}
				for(int p=1;p<=pageCount;p++) {
					StampPdfPage(pdfDocument.Pages[idxPage],listSheetFieldDefs,GetContent(s+1,p,pageCount));
					idxPage++;
					countStamped++;
				}
			}
			return countStamped;
		}

		///<summary>Draws the barcode on a printed statement page (System.Drawing printing, sheet units from the top-left corner of the page).
		///sequence is the copy's 1-based number when one statement is printed on its own. Does nothing when the barcode is off or the sheet is not a
		///statement. Never throws: a problem is logged and the page prints without a barcode. Returns true if drawn.</summary>
		public static bool DrawOnPrintedPage(Graphics g,Sheet sheet,int sequence,int pageNumber,int pageCount) {
			try {
				if(g==null || sheet==null || sheet.SheetType!=SheetTypeEnum.Statement || !IsEnabled()) {
					return false;
				}
				Rectangle rectangle=GetPageBounds(GetFieldBounds(sheet.SheetFields),sheet.WidthPage,sheet.HeightPage);
				Draw(g,rectangle,GetContent(sequence,pageNumber,Math.Max(pageNumber,pageCount)));
				return true;
			}
			catch(Exception ex) {
				Logger.WriteException(ex,"StatementInserterBarcode");
				return false;
			}
		}
		#endregion Drawing
	}

	///<summary>Collects what the Billing window needs to stamp its print document: each printed statement's page count, in the order the pages are
	///added. Attach before BillingL.SendStatements, Stamp after it and before the document is saved. Does nothing when the barcode is off.</summary>
	public class StatementInserterBarcodeBatch {
		private readonly List<int> _listPageCounts=new List<int>();
		private readonly bool _isEnabled;

		private StatementInserterBarcodeBatch(bool isEnabled) {
			_isEnabled=isEnabled;
		}

		///<summary>Page counts of the statements added to the print document, in order.</summary>
		public List<int> ListPageCounts {
			get {
				return _listPageCounts.ToList();
			}
		}

		///<summary>Wraps sendStatementsIO.FuncGetPdfDocument, which BillingL calls once for every statement it adds to the print document, right
		///before adding its pages.</summary>
		public static StatementInserterBarcodeBatch Attach(SendStatementsIO sendStatementsIO) {
			StatementInserterBarcodeBatch statementInserterBarcodeBatch=new StatementInserterBarcodeBatch(StatementInserterBarcode.IsEnabled());
			if(!statementInserterBarcodeBatch._isEnabled || sendStatementsIO==null || sendStatementsIO.FuncGetPdfDocument==null) {
				return statementInserterBarcodeBatch;
			}
			Func<string,string,PdfDocument> funcGetPdfDocumentInner=sendStatementsIO.FuncGetPdfDocument;
			sendStatementsIO.FuncGetPdfDocument=(rawBase64,savedPdfPath) => {
				PdfDocument pdfDocument=funcGetPdfDocumentInner(rawBase64,savedPdfPath);
				statementInserterBarcodeBatch._listPageCounts.Add(pdfDocument?.PageCount??0);
				return pdfDocument;
			};
			return statementInserterBarcodeBatch;
		}

		///<summary>The printed statements in print order: the sent statements whose mode is Mail or In Person, from the run's batches. BillingL adds
		///a printed statement to ListStatementNumsSent right after its pages, so this order matches the page counts. Null if a sent statement is not
		///in the batches.</summary>
		public static List<Statement> GetPrintedStatements(SendStatementsIO sendStatementsIO) {
			Dictionary<long,Statement> dictStatements=new Dictionary<long,Statement>();
			List<StatementBatch> listStatementBatches=new List<StatementBatch>(sendStatementsIO.ListStatementBatches??new List<StatementBatch>());
			listStatementBatches.Add(sendStatementsIO.CurStatementBatch);
			foreach(StatementBatch statementBatch in listStatementBatches) {
				if(statementBatch?.ListStatements==null) {
					continue;
				}
				foreach(Statement statement in statementBatch.ListStatements) {
					if(statement!=null) {
						dictStatements[statement.StatementNum]=statement;
					}
				}
			}
			List<Statement> listStatements=new List<Statement>();
			foreach(long statementNum in sendStatementsIO.ListStatementNumsSent) {
				if(!dictStatements.TryGetValue(statementNum,out Statement statement)) {
					return null;
				}
				if(statement.Mode_.In(StatementMode.Mail,StatementMode.InPerson)) {
					listStatements.Add(statement);
				}
			}
			return listStatements;
		}

		///<summary>Stamps the print document. Each statement's barcode goes where its layout puts it; when the printed statements cannot be matched
		///to the page counts, every barcode goes in the default spot. Never throws. Returns the number of pages stamped.</summary>
		public int Stamp(SendStatementsIO sendStatementsIO) {
			if(!_isEnabled || sendStatementsIO?.PdfMasterDocument==null || _listPageCounts.Count==0) {
				return 0;
			}
			try {
				List<IEnumerable<SheetFieldDef>> listLayouts=new List<IEnumerable<SheetFieldDef>>();
				List<Statement> listStatements=GetPrintedStatements(sendStatementsIO);
				if(listStatements!=null && listStatements.Count==_listPageCounts.Count) {
					for(int i=0;i<listStatements.Count;i++) {
						listLayouts.Add(GetLayoutFields(listStatements[i]));
					}
				}
				return StatementInserterBarcode.StampPrintDocument(sendStatementsIO.PdfMasterDocument,_listPageCounts,listLayouts);
			}
			catch(Exception ex) {
				Logger.WriteException(ex,"StatementInserterBarcode");
				return 0;
			}
		}

		///<summary>The fields of the statement's layout, or null (default spot) when it cannot be read.</summary>
		private static IEnumerable<SheetFieldDef> GetLayoutFields(Statement statement) {
			try {
				return SheetUtil.GetStatementSheetDef(statement)?.SheetFieldDefs;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return null;
			}
		}
	}
}
