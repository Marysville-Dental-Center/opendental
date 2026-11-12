// The embedded stylesheets and their notices (package EHR-3 of docs/specs/ehr-rebuild-plan.md, docs/port-notes/EHR-3.md): each resource's
// text, encoded as UTF-8, has the SHA-256 of its file and of the version named in THIRD-PARTY-NOTICES.txt; GetEhrResource's names; the copy of
// ccr.xsl marked as modified; the license text that file exports write; the files' line ends.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace EhrTests {
	[TestClass]
	public class CdaResourceTests {
		///<summary>The pinned SHA-256 of each embedded file: CDA-core-xsl v4.1.0-beta.2 as tagged (MANIFEST of download batch E), the copy of ccr.xsl
		///marked as modified, and the license text.</summary>
		private static readonly Dictionary<string,string> _dictSha256=new Dictionary<string,string> {
			["CDA.xsl"]="f1c32f11186cf69d64f874cc4c94eb51ca8f696b54b4b6833fb54c6afdd7acc1",
			["cda_l10n.xml"]="d8677885ef78afc0842484ecc70a65f8f17eea286e990bd39fa948cac7f1a884",
			["cda_narrativeblock.xml"]="608b518182a1418e8865e5e8437083bad034762cf078750ec27c10d17a2285a9",
			["ccr.xsl"]="1d84391fd7761a15d90f0b8a866ebf099a960f2b0c75ebdbb2e5b357b59af739",
		};
		///<summary>The SHA-256 of ccr.xsl as its "CCR XSL V2.0" file release published it.</summary>
		private const string SHA256_CCR_RELEASED="4ab70db9f8684f6b738f1e9cfa17f0165622f40c48a9f46105cd31a862cca4c0";
		private const string FOLDER_RESOURCES=@"OpenPmsDentalBusiness\Resources\Ehr\";

		private static string Sha256(byte[] arrayBytes) {
			using(SHA256 sha256=SHA256.Create()) {
				return string.Concat(sha256.ComputeHash(arrayBytes).Select(x => x.ToString("x2")));
			}
		}

		///<summary>The bytes of a repository file, without a UTF-8 byte order mark.</summary>
		private static byte[] ReadFileBytes(string pathRelative) {
			byte[] arrayBytes=File.ReadAllBytes(TestPaths.Combine(pathRelative));
			if(arrayBytes.Length>=3 && arrayBytes[0]==0xEF && arrayBytes[1]==0xBB && arrayBytes[2]==0xBF) {
				return arrayBytes.Skip(3).ToArray();
			}
			return arrayBytes;
		}

		[TestMethod]
		public void Resources_HaveTheSha256OfTheirFiles() {
			foreach(KeyValuePair<string,string> pair in _dictSha256) {
				string text=EhrStylesheetStarter.GetResourceText(pair.Key);
				Assert.IsFalse(string.IsNullOrEmpty(text),pair.Key);
				Assert.AreEqual(pair.Value,Sha256(new UTF8Encoding(false).GetBytes(text)),pair.Key+": the embedded text (read as UTF-8, ;utf-8 in the .resx).");
				Assert.AreEqual(pair.Value,Sha256(ReadFileBytes(FOLDER_RESOURCES+pair.Key)),pair.Key+": the file in the repository (LF line ends kept).");
			}
			Assert.IsNull(EhrStylesheetStarter.GetResourceText("ccd.xsl"),"Only the four files.");
			Assert.IsNull(EhrStylesheetStarter.GetResourceText("stylesheets-license.txt"),"The license text is not served to the child.");
			//cda_l10n.xml is mostly non-ASCII: read in another encoding, its text would differ from the file.
			StringAssert.Contains(EhrStylesheetStarter.GetResourceText("cda_l10n.xml"),"Patiënt");
		}

		[TestMethod]
		public void Resx_EmbedsEachFileAsUtf8Text() {
			string resx=CdaGolden.Normalize(TestPaths.ReadSource(@"OpenPmsDentalBusiness\Properties\Resources.resx")).Replace("\n","\r\n");
			foreach(string pair in new[] { "EhrCdaXsl|CDA.xsl","EhrCdaL10n|cda_l10n.xml","EhrCdaNarrativeBlock|cda_narrativeblock.xml","EhrCcrXsl|ccr.xsl",
				"EhrStylesheetsLicense|stylesheets-license.txt" })
			{
				string[] arrayParts=pair.Split('|');
				StringAssert.Contains(resx,"<data name=\""+arrayParts[0]+"\" type=\"System.Resources.ResXFileRef, System.Windows.Forms\">\r\n    <value>..\\Resources\\Ehr\\"
					+arrayParts[1]+";System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089;utf-8</value>");
			}
			//No line-ending conversion for the folder, so a checkout keeps upstream's bytes.
			StringAssert.Contains(TestPaths.ReadSource(".gitattributes"),"OpenPmsDentalBusiness/Resources/Ehr/** -text");
		}

		[TestMethod]
		public void GetEhrResource_GivesTheStylesheetsAgain() {
			Assert.AreEqual(EhrStylesheetStarter.GetResourceText("CDA.xsl"),EhrSummaryCcds.GetEhrResource("CCD"));
			Assert.AreEqual(EhrStylesheetStarter.GetResourceText("ccr.xsl"),EhrSummaryCcds.GetEhrResource("CCR"));
			Assert.AreEqual(EhrStylesheetStarter.GetResourceText("cda_l10n.xml"),EhrSummaryCcds.GetEhrResource("CdaL10n"));
			Assert.AreEqual(EhrStylesheetStarter.GetResourceText("cda_narrativeblock.xml"),EhrSummaryCcds.GetEhrResource("CdaNarrativeBlock"));
			Assert.AreEqual("",EhrSummaryCcds.GetEhrResource("ccd"));
			Assert.AreEqual("",EhrSummaryCcds.GetEhrResource(""));
			Assert.AreEqual("",EhrSummaryCcds.GetEhrResource(null));
			//What exports write and e-mails attach, in order: ccd.xsl (CDA.xsl), its companions under their own names, then the license text.
			List<KeyValuePair<string,string>> listFiles=EhrSummaryCcds.GetCcdStylesheetFiles(doIncludeLicense:true);
			CollectionAssert.AreEqual(new[] { "ccd.xsl","cda_l10n.xml","cda_narrativeblock.xml","stylesheets-license.txt" },listFiles.Select(x => x.Key).ToArray());
			Assert.AreEqual(EhrSummaryCcds.GetEhrResource("CCD"),listFiles[0].Value);
			Assert.AreEqual(EhrSummaryCcds.GetEhrResource("CdaL10n"),listFiles[1].Value);
			Assert.AreEqual(EhrSummaryCcds.GetEhrResource("CdaNarrativeBlock"),listFiles[2].Value);
			CollectionAssert.AreEqual(new[] { "ccd.xsl","cda_l10n.xml","cda_narrativeblock.xml" },
				EhrSummaryCcds.GetCcdStylesheetFiles(doIncludeLicense:false).Select(x => x.Key).ToArray());
			//The program's CCDs point to ccd.xsl beside them, and CDA.xsl reads its companions by these names beside itself.
			StringAssert.Contains(TestPaths.ReadSource(@"OpenPmsDentalBusiness\Misc\EhrCCD.cs"),
				"_w.WriteProcessingInstruction(\"xml-stylesheet\",\"type=\\\"text/xsl\\\" href=\\\"ccd.xsl\\\"\");");
			string cda=EhrSummaryCcds.GetEhrResource("CCD");
			StringAssert.Contains(cda,"<xsl:param name=\"vocFile\" select=\"'cda_l10n.xml'\"/>");
			StringAssert.Contains(cda,"document('cda_narrativeblock.xml')");
		}

		///<summary>The copy of ccr.xsl marked as modified (E-11): the AAFP's header unchanged, a notice of the five changes after it with the released
		///file's SHA-256, the date fix, and the derived work's footer the header asks for.</summary>
		[TestMethod]
		public void CcrStylesheet_IsTheCopyMarkedAsModified() {
			string xsl=EhrSummaryCcds.GetEhrResource("CCR");
			StringAssert.StartsWith(xsl,"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!-- \n\n   Copyright 2007 American Academy of Family Physicians \n");
			StringAssert.Contains(xsl,"Licensed under the Apache License, Version 2.0 (the \"License\"); ");
			int indexNotice=xsl.IndexOf("MODIFIED COPY. This file is a derived work");
			Assert.IsTrue(indexNotice>xsl.IndexOf("Version: \t2.0") && indexNotice<xsl.IndexOf("<xsl:stylesheet "),"The notice follows the header.");
			StringAssert.Contains(xsl,SHA256_CCR_RELEASED);
			for(int i=1;i<=5;i++) {
				Assert.IsTrue(Regex.IsMatch(xsl,@"\n   "+i+@"\. "),"Change "+i+" is listed.");
			}
			StringAssert.Contains(xsl,"<xsl:if test=\"(substring($tz, 1, 1) = '-' or substring($tz, 1, 1) = '+') and substring($tz, 4, 1) = ':'\">");
			//A year, a year and month, and a date without a time show no part they do not have (change 1).
			StringAssert.Contains(xsl,"<xsl:with-param name=\"pattern\" select=\"'yyyy'\"/>");
			StringAssert.Contains(xsl,"<xsl:with-param name=\"pattern\" select=\"'MMM yyyy'\"/>");
			StringAssert.Contains(xsl,"<xsl:with-param name=\"pattern\" select=\"string($pattern-date)\"/>");
			StringAssert.Contains(xsl,"is a modified version, derived from the one provided by the American Academy of Family Physicians");
			Assert.IsFalse(xsl.Contains("was provided by the American Academy of Family Physicians"),"The released footer is gone.");
		}

		///<summary>THIRD-PARTY-NOTICES.txt names both components with their versions, the SHA-256 of every file and Apache-2.0, and the license text
		///that file exports write is the notices' own Apache-2.0 text below a short header.</summary>
		[TestMethod]
		public void Notices_NameTheFilesAndTheirSha256() {
			string notices=CdaGolden.Normalize(TestPaths.ReadSource("THIRD-PARTY-NOTICES.txt"));
			int indexCda=notices.IndexOf("--- Component: HL7 CDA-core-xsl\n");
			int indexCcr=notices.IndexOf("--- Component: AAFP CCR stylesheet\n");
			Assert.IsTrue(indexCda>0 && indexCcr>indexCda);
			string entryCda=notices.Substring(indexCda,indexCcr-indexCda);
			string entryCcr=notices.Substring(indexCcr,notices.IndexOf("\n--- ",indexCcr+1)-indexCcr);
			foreach(string fileName in new[] { "CDA.xsl","cda_l10n.xml","cda_narrativeblock.xml" }) {
				StringAssert.Contains(entryCda,fileName+" "+_dictSha256[fileName]);
			}
			StringAssert.Contains(entryCda,"4.1.0-beta.2 (tag v4.1.0-beta.2, commit bfb832026c19055faa182a7a8a4e5a2d6bd8796b), unmodified");
			StringAssert.Contains(entryCda,"License:   Apache License 2.0");
			StringAssert.Contains(entryCcr,SHA256_CCR_RELEASED);
			StringAssert.Contains(entryCcr,_dictSha256["ccr.xsl"]);
			StringAssert.Contains(entryCcr,"MODIFIED by this project");
			StringAssert.Contains(entryCcr,"Copyright: Copyright 2007 American Academy of Family Physicians");
			StringAssert.Contains(entryCcr,"License:   Apache License 2.0");
			//The license file of exports: a header, then the Apache-2.0 text of the notices, word for word.
			string license=CdaGolden.Normalize(EhrSummaryCcds.GetCcdStylesheetFiles(doIncludeLicense:true).Last().Value);
			string apache=notices.Substring(notices.IndexOf("--- License: Apache License 2.0\n")+"--- License: Apache License 2.0\n".Length).TrimEnd();
			StringAssert.StartsWith(apache,"                                 Apache License\n                           Version 2.0, January 2004");
			StringAssert.EndsWith(license.TrimEnd(),apache);
			StringAssert.StartsWith(license,"The stylesheet files beside this file\n");
			StringAssert.Contains(license,"tag v4.1.0-beta.2");
			Assert.IsTrue(license.All(x => x<128),"ASCII only.");
		}
	}
}
