using System;
using System.Collections.Generic;
using System.Drawing;
using CodeBase;
using OpenDentBusiness;
using MigraDoc.DocumentObjectModel;
using PayConnectService = OpenDentBusiness.PayConnectService;


namespace OpenDental {
	class PayConnectL {
		///<summary>Called to void or refund a payment taken through PayConnect 1.0's patient portal. That needed the PayConnect 1.0 service, which was
		///removed, so this only tells the user to reverse the payment in the PayConnect merchant portal. Sends nothing, records nothing, and always
		///returns false.</summary>
		public static bool VoidOrRefundPayConnectPortalTransaction(PayConnectResponseWeb payConnectResponseWeb,Payment payment,PayConnectService.transType transType,string strRefNum,decimal amount) {
			MsgBox.Show(PayConnect.GetPortalVoidRemovedText());
			return false;
		}

		public static void PrintReceipt(string receiptStr,Patient patient) {
			string[] stringArrayReceiptLines=receiptStr.Split(new string[] { Environment.NewLine },StringSplitOptions.None);
			MigraDoc.DocumentObjectModel.Document doc=new MigraDoc.DocumentObjectModel.Document();
			doc.DefaultPageSetup.PageWidth=Unit.FromInch(3.0);
			doc.DefaultPageSetup.PageHeight=Unit.FromInch(0.181*stringArrayReceiptLines.Length+0.56);//enough to print receipt text plus 9/16 inch (0.56) extra space at bottom.
			doc.DefaultPageSetup.TopMargin=Unit.FromInch(0.25);
			doc.DefaultPageSetup.LeftMargin=Unit.FromInch(0.25);
			doc.DefaultPageSetup.RightMargin=Unit.FromInch(0.25);
			MigraDoc.DocumentObjectModel.Font bodyFontx=MigraDocHelper.CreateFont(8,false);
			bodyFontx.Name=FontFamily.GenericMonospace.Name;
			MigraDoc.DocumentObjectModel.Section section=doc.AddSection();
			Paragraph paragraph=section.AddParagraph();
			ParagraphFormat paragraphFormat=new ParagraphFormat();
			paragraphFormat.Alignment=ParagraphAlignment.Left;
			paragraphFormat.Font=bodyFontx;
			paragraph.Format=paragraphFormat;
			paragraph.AddFormattedText(receiptStr,bodyFontx);
			MigraDoc.Rendering.Printing.MigraDocPrintDocument migraDocPrintDocument=new MigraDoc.Rendering.Printing.MigraDocPrintDocument();
			MigraDoc.Rendering.DocumentRenderer documentRenderer=new MigraDoc.Rendering.DocumentRenderer(doc);
			documentRenderer.PrepareDocument();
			migraDocPrintDocument.Renderer=documentRenderer;
			if(ODBuild.IsDebug()) {
				using FormRpPrintPreview formRpPrintPreview=new FormRpPrintPreview(migraDocPrintDocument);
				formRpPrintPreview.ShowDialog();
			}
			else {
				try {
					ODprintout printout=PrinterL.CreateODprintout(
						printSituation:PrintSituation.Receipt,
						auditPatNum:patient.PatNum,
						auditDescription:Lans.g("PayConnectL","PayConnect receipt printed")
					);
					if(PrinterL.TrySetPrinter(printout)) {
						migraDocPrintDocument.PrinterSettings=printout.PrintDoc.PrinterSettings;
						migraDocPrintDocument.Print();
					}
				}
				catch(Exception ex) {
					MessageBox.Show(Lan.g("PayConnectL","Printer not available.")+"\r\n"+Lan.g("PayConnectL","Original error")+": "+ex.Message);
				}
			}
		}
	}
}
