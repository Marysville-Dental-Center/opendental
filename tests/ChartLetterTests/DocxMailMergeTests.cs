// Tests for the .docx letter merge used by chart letters (OpenDentBusiness/Misc/DocxMailMerge.cs). Templates are built in memory; no files and no
// word processor are needed. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ChartLetterTests {
	[TestClass]
	public class DocxMailMergeTests {
		private const string W="http://schemas.openxmlformats.org/wordprocessingml/2006/main";

		///<summary>A minimal word processing package with the given body XML (w: prefix), plus optional settings and a template content type.
		///</summary>
		public static byte[] MakeDocx(string bodyXml,bool isTemplate=false,bool hasMailMergeSettings=false) {
			string contentType=isTemplate
				? "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml"
				: "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
			Dictionary<string,string> dictParts=new Dictionary<string,string>();
			dictParts["[Content_Types].xml"]="<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
				+"<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
				+"<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
				+"<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
				+"<Override PartName=\"/word/document.xml\" ContentType=\""+contentType+"\"/>"
				+(hasMailMergeSettings ? "<Override PartName=\"/word/settings.xml\" "
					+"ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml\"/>" : "")
				+"</Types>";
			dictParts["_rels/.rels"]="<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
				+"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
				+"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" "
				+"Target=\"word/document.xml\"/></Relationships>";
			dictParts["word/document.xml"]="<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
				+"<w:document xmlns:w=\""+W+"\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><w:body>"
				+bodyXml+"<w:sectPr/></w:body></w:document>";
			if(hasMailMergeSettings) {
				dictParts["word/settings.xml"]="<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
					+"<w:settings xmlns:w=\""+W+"\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
					+"<w:mailMerge><w:mainDocumentType w:val=\"formLetters\"/><w:dataSource r:id=\"rId1\"/></w:mailMerge>"
					+"<w:defaultTabStop w:val=\"720\"/></w:settings>";
				dictParts["word/_rels/settings.xml.rels"]="<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
					+"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
					+"<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/mailMergeSource\" "
					+"Target=\"file:///C:/merge/data.txt\" TargetMode=\"External\"/></Relationships>";
			}
			using(MemoryStream memoryStream=new MemoryStream()) {
				using(ZipArchive zipArchive=new ZipArchive(memoryStream,ZipArchiveMode.Create,true)) {
					foreach(KeyValuePair<string,string> pair in dictParts) {
						using(Stream stream=zipArchive.CreateEntry(pair.Key).Open()) {
							byte[] bytes=new UTF8Encoding(false).GetBytes(pair.Value);
							stream.Write(bytes,0,bytes.Length);
						}
					}
				}
				return memoryStream.ToArray();
			}
		}

		public static string ReadPart(byte[] docx,string name) {
			using(MemoryStream memoryStream=new MemoryStream(docx))
			using(ZipArchive zipArchive=new ZipArchive(memoryStream,ZipArchiveMode.Read)) {
				ZipArchiveEntry zipArchiveEntry=zipArchive.GetEntry(name);
				if(zipArchiveEntry==null) {
					return null;
				}
				using(StreamReader streamReader=new StreamReader(zipArchiveEntry.Open())) {
					return streamReader.ReadToEnd();
				}
			}
		}

		private static string SimpleField(string instr,string cached) {
			return "<w:fldSimple w:instr=\""+instr.Replace("\"","&quot;")+"\"><w:r><w:rPr><w:b/></w:rPr><w:t>"+cached+"</w:t></w:r></w:fldSimple>";
		}

		///<summary>A field made of begin/separate/end characters, with the instruction split over two runs as word processors often write it.
		///</summary>
		private static string ComplexField(string instrPart1,string instrPart2,string cached) {
			return "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r>"
				+"<w:r><w:instrText xml:space=\"preserve\">"+instrPart1+"</w:instrText></w:r>"
				+"<w:r><w:instrText xml:space=\"preserve\">"+instrPart2+"</w:instrText></w:r>"
				+"<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>"
				+"<w:r><w:rPr><w:i/></w:rPr><w:t>"+cached+"</w:t></w:r>"
				+"<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>";
		}

		private static Dictionary<string,string> Values() {
			return new Dictionary<string,string> {
				{ "FName","Jane" },{ "LName","Sample" },{ "RefLName","Referrer" },{ "Address","1 Main St\r\nSuite 2" },{ "Empty","" },
			};
		}

		[TestMethod]
		public void Merge_SimpleField_ReplacedWithValue() {
			byte[] template=MakeDocx("<w:p><w:r><w:t xml:space=\"preserve\">Dear </w:t></w:r>"+SimpleField(" MERGEFIELD FName ","«FName»")+"</w:p>");
			byte[] merged=DocxMailMerge.Merge(template,Values(),out List<string> listUnknown);
			Assert.AreEqual("Dear Jane",DocxMailMerge.GetDocumentText(merged));
			Assert.AreEqual(0,listUnknown.Count);
			string documentXml=ReadPart(merged,"word/document.xml");
			Assert.IsFalse(documentXml.Contains("fldSimple"),"The field itself is removed, only its value stays.");
			Assert.IsTrue(documentXml.Contains("<w:b"),"The field's formatting is kept on the value.");
		}

		[TestMethod]
		public void Merge_ComplexFieldSplitInstruction_ReplacedWithValue() {
			byte[] template=MakeDocx("<w:p><w:r><w:t xml:space=\"preserve\">Dr. </w:t></w:r>"+ComplexField(" MERGEFIELD Ref","LName \\* MERGEFORMAT ","«RefLName»")
				+"</w:p>");
			byte[] merged=DocxMailMerge.Merge(template,Values(),out List<string> listUnknown);
			Assert.AreEqual("Dr. Referrer",DocxMailMerge.GetDocumentText(merged));
			string documentXml=ReadPart(merged,"word/document.xml");
			Assert.IsFalse(documentXml.Contains("fldChar"));
			Assert.IsFalse(documentXml.Contains("instrText"));
			Assert.IsTrue(documentXml.Contains("<w:i"),"Formatting of the field result is kept.");
		}

		[TestMethod]
		public void Merge_FieldNamesAreNotCaseSensitive_AndQuotedNamesWork() {
			byte[] template=MakeDocx("<w:p>"+SimpleField(" MERGEFIELD fname ","x")+SimpleField(" MERGEFIELD \"LNAME\" ","y")+"</w:p>");
			byte[] merged=DocxMailMerge.Merge(template,Values(),out List<string> _);
			Assert.AreEqual("JaneSample",DocxMailMerge.GetDocumentText(merged));
		}

		[TestMethod]
		public void Merge_Switches_BeforeAfterAndCase() {
			byte[] template=MakeDocx("<w:p>"+SimpleField(" MERGEFIELD FName \\b \"Hello \" \\f \"!\" \\* Upper ","x")
				+SimpleField(" MERGEFIELD Empty \\b \"never \" ","y")+"</w:p>");
			byte[] merged=DocxMailMerge.Merge(template,Values(),out List<string> _);
			Assert.AreEqual("Hello JANE!",DocxMailMerge.GetDocumentText(merged),"Text before and after appears only with a value.");
		}

		[TestMethod]
		public void Merge_MultiLineValue_UsesLineBreaks() {
			byte[] template=MakeDocx("<w:p>"+SimpleField(" MERGEFIELD Address ","x")+"</w:p>");
			byte[] merged=DocxMailMerge.Merge(template,Values(),out List<string> _);
			Assert.AreEqual("1 Main St\nSuite 2",DocxMailMerge.GetDocumentText(merged));
			Assert.IsTrue(ReadPart(merged,"word/document.xml").Contains("<w:br"));
		}

		[TestMethod]
		public void Merge_UnknownField_LeftEmptyAndReported() {
			byte[] template=MakeDocx("<w:p><w:r><w:t xml:space=\"preserve\">[</w:t></w:r>"+SimpleField(" MERGEFIELD Nope ","x")
				+"<w:r><w:t>]</w:t></w:r></w:p>");
			byte[] merged=DocxMailMerge.Merge(template,Values(),out List<string> listUnknown);
			Assert.AreEqual("[]",DocxMailMerge.GetDocumentText(merged));
			CollectionAssert.AreEqual(new List<string> { "Nope" },listUnknown);
		}

		[TestMethod]
		public void Merge_OtherFieldsAreLeftAlone() {
			byte[] template=MakeDocx("<w:p>"+SimpleField(" PAGE ","1")+ComplexField(" DATE ","\\@ \"M/d/yyyy\" ","1/1/2024")+"</w:p>");
			byte[] merged=DocxMailMerge.Merge(template,Values(),out List<string> _);
			string documentXml=ReadPart(merged,"word/document.xml");
			Assert.IsTrue(documentXml.Contains("PAGE"));
			Assert.IsTrue(documentXml.Contains("DATE"));
		}

		[TestMethod]
		public void Merge_FieldInsideIfInstruction_BecomesFieldCode() {
			//{ IF { MERGEFIELD FName } = "Jane" "yes" "no" }
			string body="<w:p><w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> IF </w:instrText></w:r>"
				+ComplexField(" MERGEFIELD ","FName ","«FName»")
				+"<w:r><w:instrText xml:space=\"preserve\"> = \"Jane\" \"yes\" \"no\" </w:instrText></w:r>"
				+"<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:t>no</w:t></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r></w:p>";
			byte[] merged=DocxMailMerge.Merge(MakeDocx(body),Values(),out List<string> _);
			string documentXml=ReadPart(merged,"word/document.xml");
			Assert.IsTrue(documentXml.Contains("<w:instrText xml:space=\"preserve\">Jane</w:instrText>"),documentXml);
			Assert.IsFalse(documentXml.Contains("MERGEFIELD"));
			Assert.IsTrue(documentXml.Contains(" IF "),"The outer IF field stays for the word processor to evaluate.");
		}

		[TestMethod]
		public void Merge_RemovesMailMergeDataSource_AndTurnsTemplateIntoDocument() {
			byte[] template=MakeDocx("<w:p>"+SimpleField(" MERGEFIELD FName ","x")+"</w:p>",isTemplate:true,hasMailMergeSettings:true);
			byte[] merged=DocxMailMerge.Merge(template,Values(),out List<string> _);
			Assert.IsFalse(ReadPart(merged,"word/settings.xml").Contains("mailMerge"));
			Assert.IsTrue(ReadPart(merged,"word/settings.xml").Contains("defaultTabStop"),"Other settings are kept.");
			Assert.IsFalse(ReadPart(merged,"word/_rels/settings.xml.rels").Contains("mailMergeSource"));
			string contentTypes=ReadPart(merged,"[Content_Types].xml");
			Assert.IsTrue(contentTypes.Contains("wordprocessingml.document.main+xml"));
			Assert.IsFalse(contentTypes.Contains("template.main+xml"));
		}

		[TestMethod]
		public void Merge_KeepsPartOrder() {
			byte[] template=MakeDocx("<w:p>"+SimpleField(" MERGEFIELD FName ","x")+"</w:p>",hasMailMergeSettings:true);
			byte[] merged=DocxMailMerge.Merge(template,Values(),out List<string> _);
			using(MemoryStream memoryStream=new MemoryStream(merged))
			using(ZipArchive zipArchive=new ZipArchive(memoryStream,ZipArchiveMode.Read)) {
				Assert.AreEqual("[Content_Types].xml",zipArchive.Entries[0].FullName);
				Assert.AreEqual(5,zipArchive.Entries.Count);
			}
		}

		[TestMethod]
		public void Merge_NotADocx_Throws() {
			Expect.Throws<ApplicationException>(() => DocxMailMerge.Merge(Encoding.ASCII.GetBytes("not a zip"),Values(),out List<string> _));
			Expect.Throws<ApplicationException>(() => DocxMailMerge.Merge(new byte[0],Values(),out List<string> _));
		}

		[TestMethod]
		public void CreateTemplate_ThenMerge_RoundTrip() {
			byte[] template=DocxMailMerge.CreateTemplate("Referral",new[] { "FName","LName","RefLName" });
			CollectionAssert.AreEqual(new List<string> { "FName","LName","RefLName" },DocxMailMerge.GetFieldNames(template));
			byte[] merged=DocxMailMerge.Merge(template,Values(),out List<string> listUnknown);
			Assert.AreEqual(0,listUnknown.Count);
			Assert.AreEqual("Referral\nFName: Jane\nLName: Sample\nRefLName: Referrer",DocxMailMerge.GetDocumentText(merged));
			Assert.AreEqual(0,DocxMailMerge.GetFieldNames(merged).Count);
		}

		[TestMethod]
		public void GetFieldNames_FindsSimpleAndComplexFields() {
			byte[] template=MakeDocx("<w:p>"+SimpleField(" MERGEFIELD FName ","x")+ComplexField(" MERGEFIELD ","LName ","y")+SimpleField(" PAGE ","1")
				+SimpleField(" MERGEFIELD fname ","again")+"</w:p>");
			CollectionAssert.AreEqual(new List<string> { "FName","LName" },DocxMailMerge.GetFieldNames(template));
		}

		[TestMethod]
		public void Extensions_SupportedAndOutput() {
			Assert.IsTrue(DocxMailMerge.IsSupportedTemplate(@"C:\x\Letter.DOCX"));
			Assert.IsTrue(DocxMailMerge.IsSupportedTemplate("a.dotx"));
			Assert.IsTrue(DocxMailMerge.IsSupportedTemplate("a.dotm"));
			Assert.IsFalse(DocxMailMerge.IsSupportedTemplate("a.doc"));
			Assert.IsFalse(DocxMailMerge.IsSupportedTemplate("a.dot"));
			Assert.IsFalse(DocxMailMerge.IsSupportedTemplate(null));
			Assert.AreEqual(".docx",DocxMailMerge.GetOutputExtension("a.dotx"));
			Assert.AreEqual(".docm",DocxMailMerge.GetOutputExtension("a.dotm"));
			Assert.AreEqual(".docm",DocxMailMerge.GetOutputExtension("a.docm"));
		}
	}
}
