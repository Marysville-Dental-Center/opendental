// The export and attachment lines of package EHR-3 (docs/specs/ehr-rebuild-plan.md, E-13; docs/port-notes/EHR-3.md): a single-document file
// export writes ccd.xml, the stylesheet files, the license text and ccd.html; Patient Export writes one page per document through one child and
// one set of stylesheet files, each page before it reads the next document; e-mails and the portal attach ccd.xsl with its two companions; the eight lines of the six files use them. The
// files go to a scratch folder under the temp folder, deleted afterwards. The pages come from a real OpenPmsDental.exe /ehr-render. No window.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;

namespace EhrTests {
	[TestClass]
	public class CdaExportTests {
		private string _folder;

		[TestInitialize]
		public void Initialize() {
			_folder=Path.Combine(Path.GetTempPath(),"ehr3_export_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folder);
		}

		[TestCleanup]
		public void Cleanup() {
			EhrStylesheetStarter.FuncStartChild=null;
			try {
				Directory.Delete(_folder,true);
			}
			catch(Exception) {
				//A file still open by a failed assertion; the temp folder is cleaned by Windows.
			}
		}

		///<summary>The bytes of a written file must be the resource's text as UTF-8, with no byte order mark.</summary>
		private void AssertFileIs(string fileName,string text) {
			string path=Path.Combine(_folder,fileName);
			Assert.IsTrue(File.Exists(path),fileName);
			byte[] arrayBytes=File.ReadAllBytes(path);
			Assert.IsTrue(arrayBytes.Length>0,fileName+" is empty.");
			CollectionAssert.AreEqual(new UTF8Encoding(false).GetBytes(text),arrayBytes,fileName);
		}

		[TestMethod]
		public void SingleExport_WritesTheDocumentStylesheetsLicenseAndPage() {
			string ccd=CdaGolden.ReadFixture("CCD-full.modified.xml");
			Assert.IsNull(FormEhrSummaryOfCare.WriteCcdFiles(_folder,ccd));
			AssertFileIs("ccd.xml",ccd);
			AssertFileIs("ccd.xsl",EhrSummaryCcds.GetEhrResource("CCD"));
			AssertFileIs("cda_l10n.xml",EhrSummaryCcds.GetEhrResource("CdaL10n"));
			AssertFileIs("cda_narrativeblock.xml",EhrSummaryCcds.GetEhrResource("CdaNarrativeBlock"));
			AssertFileIs("stylesheets-license.txt",EhrSummaryCcds.GetCcdStylesheetFiles(doIncludeLicense:true).Last().Value);
			AssertFileIs("ccd.html",CdaGolden.Render(EhrRenderKind.Ccd,ccd));
			CollectionAssert.AreEquivalent(new[] { "ccd.xml","ccd.xsl","cda_l10n.xml","cda_narrativeblock.xml","stylesheets-license.txt","ccd.html" },
				Directory.GetFiles(_folder).Select(Path.GetFileName).ToArray(),"Nothing else.");
			Assert.AreEqual("",FormEhrSummaryOfCare.GetPageNotWrittenMessage(null));
		}

		///<summary>When the document cannot be rendered, ccd.html is not written and an older one is deleted (it would show another document); the
		///other files are written, and the message says why.</summary>
		[TestMethod]
		public void SingleExport_NoPage_DeletesTheOlderPage_AndSaysWhy() {
			File.WriteAllText(Path.Combine(_folder,"ccd.html"),"<html>an older export</html>");
			string ccd="<?xml version=\"1.0\"?><!DOCTYPE ClinicalDocument [<!ENTITY a \"a\">]><ClinicalDocument xmlns=\"urn:hl7-org:v3\">&a;</ClinicalDocument>";
			string reason=FormEhrSummaryOfCare.WriteCcdFiles(_folder,ccd);
			Assert.AreEqual("The document has a document type declaration (DTD), which is not allowed.",reason);
			Assert.IsFalse(File.Exists(Path.Combine(_folder,"ccd.html")));
			AssertFileIs("ccd.xml",ccd);
			AssertFileIs("ccd.xsl",EhrSummaryCcds.GetEhrResource("CCD"));
			StringAssert.Contains(FormEhrSummaryOfCare.GetPageNotWrittenMessage(reason),"ccd.html was not written");
			StringAssert.Contains(FormEhrSummaryOfCare.GetPageNotWrittenMessage(reason),reason);
		}

		///<summary>Bulk Patient Export: one HTML beside each XML, made by one child for the whole export, and one set of stylesheet files.</summary>
		[TestMethod]
		public void PatientExport_OnePagePerDocument_OneChild() {
			string ccd=CdaGolden.ReadFixture("CCD-full.modified.xml");
			List<string> listNames=new List<string> { "Test_Anna_1","Test_Bjorn_2","Test_Chloe_3" };
			List<string> listCcds=new List<string> { ccd,ccd.Replace("<given>","<given>B "),ccd.Replace("<given>","<given>C ") };
			for(int i=0;i<listNames.Count;i++) {
				File.WriteAllText(Path.Combine(_folder,listNames[i]+".xml"),listCcds[i]);//As the export's loop writes them.
			}
			using(CountingStarter countingStarter=new CountingStarter()) {
				Assert.AreEqual("",FormEhrSummaryOfCare.WriteCcdPages(_folder,listNames));
				Assert.AreEqual(1,countingStarter.CountStarted,"One child for the whole export.");
			}
			for(int i=0;i<listNames.Count;i++) {
				AssertFileIs(listNames[i]+".html",CdaGolden.Render(EhrRenderKind.Ccd,listCcds[i]));
			}
			Assert.AreNotEqual(File.ReadAllText(Path.Combine(_folder,listNames[0]+".html")),File.ReadAllText(Path.Combine(_folder,listNames[1]+".html")),
				"Each page shows its own document.");
			AssertFileIs("ccd.xsl",EhrSummaryCcds.GetEhrResource("CCD"));
			AssertFileIs("cda_l10n.xml",EhrSummaryCcds.GetEhrResource("CdaL10n"));
			AssertFileIs("cda_narrativeblock.xml",EhrSummaryCcds.GetEhrResource("CdaNarrativeBlock"));
			AssertFileIs("stylesheets-license.txt",EhrSummaryCcds.GetCcdStylesheetFiles(doIncludeLicense:true).Last().Value);
			Assert.AreEqual(3,Directory.GetFiles(_folder,"*.xml").Count(x => !x.EndsWith("cda_l10n.xml") && !x.EndsWith("cda_narrativeblock.xml")));
			Assert.AreEqual(3,Directory.GetFiles(_folder,"*.html").Length,"One HTML per XML.");
			Assert.AreEqual(1,Directory.GetFiles(_folder,"*.xsl").Length,"One set of stylesheet files.");
		}

		///<summary>A document of the batch that cannot be rendered gets no page, and the reason names its file; the others are written.</summary>
		[TestMethod]
		public void PatientExport_DocumentWithoutPage_IsNamed() {
			File.WriteAllText(Path.Combine(_folder,"Test_Anna_1.xml"),CdaGolden.ReadFixture("CCD-full.modified.xml"));
			File.WriteAllText(Path.Combine(_folder,"Test_Bad_2.xml"),"<!DOCTYPE a [<!ENTITY a \"a\">]><a>&a;</a>");
			File.WriteAllText(Path.Combine(_folder,"Test_Chloe_3.xml"),CdaGolden.ReadFixture("CCD-full.modified.xml"));
			string reasons=FormEhrSummaryOfCare.WriteCcdPages(_folder,new List<string> { "Test_Anna_1","Test_Bad_2","Test_Chloe_3" });
			Assert.AreEqual("Test_Bad_2.html: The document has a document type declaration (DTD), which is not allowed.",reasons.TrimEnd());
			Assert.IsTrue(File.Exists(Path.Combine(_folder,"Test_Anna_1.html")));
			Assert.IsFalse(File.Exists(Path.Combine(_folder,"Test_Bad_2.html")));
			Assert.IsTrue(File.Exists(Path.Combine(_folder,"Test_Chloe_3.html")),"The page after a document without one has its own name.");
		}

		///<summary>Patient Export streams (review of fix round 1: holding every document and page until the end ran a 32-bit program out of
		///memory for a few thousand patients). The export names are read one at a time, and each <name>.xml is read back just after its name:
		///when the batch reads the document of name i, the pages of names 0 to i-1 are already written and no other is. One child still makes
		///every page, so CDA.xsl is compiled once.</summary>
		[TestMethod]
		public void PatientExport_WritesEachPage_BeforeReadingTheNextDocument() {
			string ccd=CdaGolden.ReadFixture("CCD-full.modified.xml");
			List<string> listNames=new List<string> { "Test_Anna_1","Test_Bjorn_2","Test_Chloe_3","Test_Dara_4" };
			foreach(string name in listNames) {
				File.WriteAllText(Path.Combine(_folder,name+".xml"),ccd);
			}
			List<string> listLog=new List<string>();
			CountingEnumerable countingEnumerable=new CountingEnumerable(listNames,index => listLog.Add("read "+index+", pages:"
				+string.Concat(Directory.GetFiles(_folder,"*.html").Select(x => " "+Path.GetFileNameWithoutExtension(x)).OrderBy(x => x))));
			using(CountingStarter countingStarter=new CountingStarter()) {
				Assert.AreEqual("",FormEhrSummaryOfCare.WriteCcdPages(_folder,countingEnumerable));
				Assert.AreEqual(1,countingStarter.CountStarted,"One child for the whole export.");
			}
			CollectionAssert.AreEqual(new[] {
					"read 0, pages:",
					"read 1, pages: Test_Anna_1",
					"read 2, pages: Test_Anna_1 Test_Bjorn_2",
					"read 3, pages: Test_Anna_1 Test_Bjorn_2 Test_Chloe_3" },
				listLog,string.Join("\r\n",listLog));
			Assert.AreEqual(1,countingEnumerable.CountEnumerated,"The names are read once, as the batch goes.");
			string html=CdaGolden.Render(EhrRenderKind.Ccd,ccd);
			foreach(string name in listNames) {
				AssertFileIs(name+".html",html);
			}
		}

		///<summary>The eight export and attachment lines of the six files (plan, EHR-3): the single-document exports call WriteCcdFiles, Patient
		///Export WriteCcdPages after its loop with the names it wrote (no documents), and the e-mail, portal and referral attachments
		///GetCcdStylesheetFiles without the license text. None of the six writes or attaches ccd.xsl alone any more.</summary>
		[TestMethod]
		public void ExportLines_UseTheHelpers() {
			Dictionary<string,string[]> dictSites=new Dictionary<string,string[]> {
				[@"OpenPmsDental\Forms\FormContinuityOfCareDocument.cs"]=new[] {
					"string reasonPage=FormEhrSummaryOfCare.WriteCcdFiles(formBrowserDialog.SelectedPath,continuityOfCareText);" },
				[@"OpenPmsDental\Forms\FormEhrClinicalSummary.cs"]=new[] {
					"string reasonPage=FormEhrSummaryOfCare.WriteCcdFiles(dlg.SelectedPath,ccd);",
					"foreach(KeyValuePair<string,string> pairFile in EhrSummaryCcds.GetCcdStylesheetFiles(doIncludeLicense:false)) {" },
				[@"OpenPmsDental\Forms\FormEhrElectronicCopy.cs"]=new[] { "string reasonPage=FormEhrSummaryOfCare.WriteCcdFiles(dlg.SelectedPath,ccd);" },
				[@"OpenPmsDental\Forms\FormEhrPatientExport.cs"]=new[] {
					"pagesNotWritten=FormEhrSummaryOfCare.WriteCcdPages(folderPath,listFileNamesWritten);" },
				[@"OpenPmsDental\Forms\FormEhrSummaryOfCare.cs"]=new[] {
					"string reasonPage=WriteCcdFiles(dlg.SelectedPath,ccd);",
					"foreach(KeyValuePair<string,string> pairFile in EhrSummaryCcds.GetCcdStylesheetFiles(doIncludeLicense:false)) {" },
				[@"WpfControlsOD\Frms\FrmReferralsPatient.xaml.cs"]=new[] {
					"foreach(KeyValuePair<string,string> pairFile in EhrSummaryCcds.GetCcdStylesheetFiles(doIncludeLicense:false)) {" },
			};
			int countLines=0;
			foreach(KeyValuePair<string,string[]> pair in dictSites) {
				string code=CSharpSource.Read(TestPaths.ReadSource(pair.Key)).CodeWithoutComments;
				foreach(string line in pair.Value) {
					StringAssert.Contains(code,line,pair.Key);
					countLines++;
				}
				Assert.IsFalse(code.Contains("GetEhrResource("),pair.Key+": no stylesheet written or attached by itself.");
				Assert.IsFalse(Regex.IsMatch(code,@"(WriteAllText|CreateAttach|DisplayedFileName=)[^;]*""(ccd|CCD)\.xsl"""),pair.Key+": no ccd.xsl written by itself.");
			}
			Assert.AreEqual(8,countLines,"Eight lines in six files.");
			//Patient Export collects the name of each document it wrote, never the document, and renders them after its loop, once; WriteCcdPages
			//reads each document back from its file and writes its page before it reads the next (PatientExport_WritesEachPage_BeforeReadingTheNextDocument).
			string export=CSharpSource.Read(TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEhrPatientExport.cs")).CodeWithoutComments;
			int indexWrite=export.IndexOf("File.WriteAllText(ODFileUtils.CombinePaths(folderPath,fileName+\".xml\"),ccd);");
			int indexAdd=export.IndexOf("listFileNamesWritten.Add(fileName);");
			int indexPages=export.IndexOf("FormEhrSummaryOfCare.WriteCcdPages(");
			Assert.IsTrue(indexWrite>0 && indexAdd>indexWrite && indexPages>indexAdd);
			int indexList=export.IndexOf("List<string> listFileNamesWritten=new List<string>();");
			Assert.IsTrue(indexList>0 && indexList<indexWrite);
			string loop=export.Substring(indexList,indexPages-indexList);
			Assert.AreEqual(1,Regex.Matches(loop,@"new List<").Count,"One list in the export's loop: the names.");
			Assert.AreEqual(1,Regex.Matches(loop,@"\.Add\(").Count,"Only the file names are collected.");
			Assert.IsFalse(export.Contains("listCcdsWritten"),"No list of the documents.");
			//The portal's attachments keep the names CDA.xsl reads them by.
			string clinical=CSharpSource.Read(TestPaths.ReadSource(@"OpenPmsDental\Forms\FormEhrClinicalSummary.cs")).CodeWithoutComments;
			StringAssert.Contains(clinical,"attachSs.DisplayedFileName=pairFile.Key;");
		}
	}
}
