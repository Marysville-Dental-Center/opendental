// Small synthetic PDF files for the V-DXC2 tests, built in memory: plain one- and several-page PDFs with a correct cross-reference table, and
// variants that are encrypted (an /Encrypt entry in the trailer or in a cross-reference stream dictionary). No real documents, no patient data.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VDXC2Tests {
	public static class SyntheticPdf {
		///<summary>A well-formed PDF 1.4 file with pageCount letter-size pages, each showing pageText. trailerExtra is added inside the trailer
		///dictionary (for example "/Encrypt 99 0 R"); extraObject, when not blank, is added as object 99. leadingBytes go before the %PDF- header.
		///</summary>
		public static byte[] Build(int pageCount=1,string pageText="Synthetic test page",string trailerExtra="",string extraObject="",
			string leadingBytes="")
		{
			List<string> listObjects=new List<string>();//index 0 is object 1
			StringBuilder stringBuilderKids=new StringBuilder();
			int firstPageObj=4;
			for(int i=0;i<pageCount;i++) {
				stringBuilderKids.Append((firstPageObj+i*2)+" 0 R ");
			}
			listObjects.Add("<< /Type /Catalog /Pages 2 0 R >>");
			listObjects.Add("<< /Type /Pages /Kids [ "+stringBuilderKids.ToString()+"] /Count "+pageCount+" >>");
			listObjects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
			for(int i=0;i<pageCount;i++) {
				int contentObj=firstPageObj+i*2+1;
				listObjects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents "+contentObj+" 0 R >>");
				string content="BT /F1 18 Tf 72 720 Td ("+pageText+" "+(i+1)+") Tj ET";
				listObjects.Add("<< /Length "+content.Length+" >>\nstream\n"+content+"\nendstream");
			}
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(leadingBytes);
			stringBuilder.Append("%PDF-1.4\n%âãÏÓ\n");
			List<int> listOffsets=new List<int>();
			for(int i=0;i<listObjects.Count;i++) {
				listOffsets.Add(stringBuilder.Length);
				stringBuilder.Append((i+1)+" 0 obj\n"+listObjects[i]+"\nendobj\n");
			}
			int extraOffset=-1;
			if(!string.IsNullOrEmpty(extraObject)) {
				extraOffset=stringBuilder.Length;
				stringBuilder.Append("99 0 obj\n"+extraObject+"\nendobj\n");
			}
			int xrefOffset=stringBuilder.Length;
			stringBuilder.Append("xref\n0 "+(listObjects.Count+1)+"\n");
			stringBuilder.Append("0000000000 65535 f \n");
			for(int i=0;i<listOffsets.Count;i++) {
				stringBuilder.Append(listOffsets[i].ToString("0000000000",CultureInfo.InvariantCulture)+" 00000 n \n");
			}
			if(extraOffset>=0) {
				stringBuilder.Append("99 1\n"+extraOffset.ToString("0000000000",CultureInfo.InvariantCulture)+" 00000 n \n");
			}
			stringBuilder.Append("trailer\n<< /Size "+(extraOffset>=0 ? 100 : listObjects.Count+1)+" /Root 1 0 R "+trailerExtra+" >>\n");
			stringBuilder.Append("startxref\n"+xrefOffset+"\n%%EOF\n");
			return Latin1(stringBuilder.ToString());
		}

		///<summary>A standard security handler dictionary, as an encrypted file's object 99. The O and U strings are synthetic.</summary>
		public const string EncryptDictionary="<< /Filter /Standard /V 1 /R 2 /Length 40 /P -44 "
			+"/O <0000000000000000000000000000000000000000000000000000000000000000> "
			+"/U <1111111111111111111111111111111111111111111111111111111111111111> >>";

		///<summary>Password-protected, the usual way: the trailer points to the encryption dictionary.</summary>
		public static byte[] BuildEncryptedIndirect() {
			return Build(trailerExtra:"/Encrypt 99 0 R /ID [<0123456789ABCDEF0123456789ABCDEF> <0123456789ABCDEF0123456789ABCDEF>]",
				extraObject:EncryptDictionary);
		}

		///<summary>The encryption dictionary written straight into the trailer.</summary>
		public static byte[] BuildEncryptedDirect() {
			return Build(trailerExtra:"/Encrypt "+EncryptDictionary);
		}

		///<summary>A PDF 1.5-style file whose trailer is a cross-reference stream: its dictionary (never compressed) carries /Encrypt. The stream
		///data here is not a working cross-reference table; only the dictionary matters to the check.</summary>
		public static byte[] BuildEncryptedXRefStream() {
			string pdf="%PDF-1.5\n"
				+"1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n"
				+"2 0 obj\n<< /Type /Pages /Kids [] /Count 0 >>\nendobj\n"
				+"99 0 obj\n"+EncryptDictionary+"\nendobj\n"
				+"100 0 obj\n<</Type/XRef/Size 101/W[1 2 1]/Root 1 0 R/Encrypt 99 0 R/Length 0>>\nstream\n\nendstream\nendobj\n"
				+"startxref\n0\n%%EOF\n";
			return Latin1(pdf);
		}

		private static byte[] Latin1(string text) {
			return Encoding.GetEncoding(28591).GetBytes(text);
		}
	}
}
