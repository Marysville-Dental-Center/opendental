// Makes sheet PDFs in memory and reads back what their first page holds: the fonts (and how their text is encoded) and the images.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenPmsDentalBusiness;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace M2SHEETSTests {
	public class PdfFontInfo {
		public string Subtype;
		public string BaseFont;
		public string Encoding;
		///<summary>The ToUnicode map of a Unicode font, decoded; empty for others.</summary>
		public string ToUnicode="";
	}

	public class PdfImageInfo {
		public int Width;
		public int Height;
		public bool HasSoftMask;
	}

	public class PdfPageInfo {
		public List<PdfFontInfo> ListFonts=new List<PdfFontInfo>();
		public List<PdfImageInfo> ListImages=new List<PdfImageInfo>();
		public byte[] Content;
	}

	public static class PdfInspect {
		///<summary>A letter-size patient form with the given fields, made into a PDF by the sheet PDF code (SheetDrawingJob.CreatePdf).</summary>
		public static PdfDocument CreateSheetPdf(params SheetField[] arraySheetFields) {
			Sheet sheet=new Sheet {
				SheetType=SheetTypeEnum.PatientForm,
				FontName="Arial",
				FontSize=11f,
				Width=850,
				Height=1100,
				Description="M2-SHEETS synthetic",
				Parameters=new List<SheetParameter>(),
				SheetFields=new List<SheetField>(arraySheetFields),
			};
			return new SheetDrawingJob().CreatePdf(sheet);
		}

		public static byte[] Save(PdfDocument pdfDocument) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				pdfDocument.Save(memoryStream,false);
				return memoryStream.ToArray();
			}
		}

		///<summary>Saves the PDF (a PDF can be saved only once) and reads its first page back. With reviewFileName, the saved file is also written
		///where the environment variable M2SHEETS_PDF_DIR points, for a person to look at.</summary>
		public static PdfPageInfo ReadFirstPage(PdfDocument pdfDocumentWritten,string reviewFileName=null) {
			byte[] arrayBytes=Save(pdfDocumentWritten);
			string dir=Environment.GetEnvironmentVariable("M2SHEETS_PDF_DIR");
			if(reviewFileName!=null && !string.IsNullOrWhiteSpace(dir)) {
				Directory.CreateDirectory(dir);
				File.WriteAllBytes(Path.Combine(dir,reviewFileName),arrayBytes);
			}
			PdfPageInfo pdfPageInfo=new PdfPageInfo();
			using(MemoryStream memoryStream=new MemoryStream(arrayBytes)) {
				PdfDocument pdfDocument=PdfReader.Open(memoryStream,PdfDocumentOpenMode.Import);
				PdfPage pdfPage=pdfDocument.Pages[0];
				PdfDictionary dictResources=pdfPage.Elements.GetDictionary("/Resources");
				PdfDictionary dictFonts=dictResources?.Elements.GetDictionary("/Font");
				if(dictFonts!=null) {
					foreach(string key in dictFonts.Elements.Keys) {
						PdfDictionary dictFont=dictFonts.Elements.GetDictionary(key);
						PdfFontInfo pdfFontInfo=new PdfFontInfo {
							Subtype=dictFont.Elements.GetName("/Subtype"),
							BaseFont=dictFont.Elements.GetName("/BaseFont"),
							Encoding=dictFont.Elements.GetName("/Encoding"),
						};
						PdfDictionary dictToUnicode=dictFont.Elements.GetDictionary("/ToUnicode");
						if(dictToUnicode?.Stream!=null) {
							pdfFontInfo.ToUnicode=Encoding.ASCII.GetString(dictToUnicode.Stream.UnfilteredValue);
						}
						pdfPageInfo.ListFonts.Add(pdfFontInfo);
					}
				}
				PdfDictionary dictXObjects=dictResources?.Elements.GetDictionary("/XObject");
				if(dictXObjects!=null) {
					foreach(string key in dictXObjects.Elements.Keys) {
						PdfDictionary dictXObject=dictXObjects.Elements.GetDictionary(key);
						if(dictXObject.Elements.GetName("/Subtype")!="/Image") {
							continue;
						}
						pdfPageInfo.ListImages.Add(new PdfImageInfo {
							Width=dictXObject.Elements.GetInteger("/Width"),
							Height=dictXObject.Elements.GetInteger("/Height"),
							HasSoftMask=dictXObject.Elements.ContainsKey("/SMask"),
						});
					}
				}
				List<byte> listContent=new List<byte>();
				for(int i=0;i<pdfPage.Contents.Elements.Count;i++) {
					PdfDictionary dictContent=pdfPage.Contents.Elements.GetDictionary(i);
					if(dictContent?.Stream!=null) {
						listContent.AddRange(dictContent.Stream.UnfilteredValue);
					}
				}
				pdfPageInfo.Content=listContent.ToArray();
			}
			return pdfPageInfo;
		}

		///<summary>The hex code of a character as the ToUnicode map writes it, like "041F".</summary>
		public static string Hex(char c) {
			return ((int)c).ToString("X4");
		}
	}
}
