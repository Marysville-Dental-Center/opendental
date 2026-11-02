// The open licence check that replaces empira's closed PdfSharp.License.dll (third-party\build\PdfSharp-gdi\PdfSharpLicense.cs): a modified
// version of empira's wrapper with empira's copyright and a dated notice, the wrapper's API, and the AGPL path as the prebuilt library had it
// (observed from outside on 2026-11-02 with empira's 6.5.0 files): no key, no network, no message; without SetAgplLicense() PDFsharp refuses
// with the same exception, after it every PDF names the licence in its Producer and Creator exactly as before; every licence key is refused.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp;
using PdfSharp.License;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfLibraryTests {
	[TestClass]
	public class StandInTests {
		private const string PRODUCER="PDFsharp 6.5.0-gdi (AGPL license)";
		private const string CREATOR="PDFsharp 6.5.0 (www.pdfsharp.com) (AGPL license)";

		private static PdfDocument NewDocument() {
			PdfDocument pdfDocument=new PdfDocument();
			pdfDocument.AddPage();
			return pdfDocument;
		}

		///<summary>Saves the document and returns the Producer and Creator it was saved with, read back, and the saved bytes.</summary>
		private static (string Producer,string Creator,byte[] Bytes) SaveAndRead(PdfDocument pdfDocument) {
			byte[] arrayBytes=PdfRead.Save(pdfDocument);
			PdfDocument pdfDocumentRead=PdfRead.Open(arrayBytes);
			return (pdfDocumentRead.Info.Producer,pdfDocumentRead.Info.Creator,arrayBytes);
		}

		private static MethodInfo GetInternal(string name) {
			MethodInfo methodInfo=typeof(PdfSharpLicense).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic);
			Assert.IsNotNull(methodInfo,"PdfSharpLicense."+name);
			return methodInfo;
		}

		[TestMethod]
		public void Pdfs_NameTheAgplLicence_InProducerAndCreator_AsEmpirasLibraryDid() {
			//A new document: PDFsharp sets both, the licence check adds the licence.
			(string producer,string creator,byte[] arrayBytes)=SaveAndRead(NewDocument());
			Assert.AreEqual(PRODUCER,producer);
			Assert.AreEqual(CREATOR,creator);
			string raw=Encoding.GetEncoding(28591).GetString(arrayBytes);
			StringAssert.Contains(raw,"<pdf:Producer>"+PRODUCER+"<","The XMP metadata says the same.");
			StringAssert.Contains(raw,"<xmp:CreatorTool>"+CREATOR+"<");
			//A Creator the application sets is left alone.
			PdfDocument pdfDocument=NewDocument();
			pdfDocument.Info.Creator="Custom App";
			(producer,creator,_)=SaveAndRead(pdfDocument);
			Assert.AreEqual(PRODUCER,producer);
			Assert.AreEqual("Custom App",creator);
			//A Producer the document had is kept as the original.
			pdfDocument=NewDocument();
			pdfDocument.Info.Creator="Bar";
			pdfDocument.Info.Elements.SetString("/Producer","Foo");
			(producer,creator,_)=SaveAndRead(pdfDocument);
			Assert.AreEqual(PRODUCER+" (Original: Foo)",producer);
			Assert.AreEqual("Bar",creator);
			//Saved again: the Producer keeps one licence note; the Creator gets a second one, as empira's library wrote it.
			PdfDocument pdfDocumentModify=PdfReader.Open(new MemoryStream(arrayBytes),PdfDocumentOpenMode.Modify);
			(producer,creator,_)=SaveAndRead(pdfDocumentModify);
			Assert.AreEqual(PRODUCER,producer);
			Assert.AreEqual(CREATOR+" (AGPL license)",creator);
			//MigraDoc's Creator.
			Document document=new Document();
			document.AddSection().AddParagraph("Only this line");
			PdfDocumentRenderer pdfDocumentRenderer=new PdfDocumentRenderer();
			pdfDocumentRenderer.Document=document;
			pdfDocumentRenderer.RenderDocument();
			(producer,creator,_)=SaveAndRead(pdfDocumentRenderer.PdfDocument);
			Assert.AreEqual(PRODUCER,producer);
			Assert.AreEqual("empira MigraDoc 6.5.0 (www.pdfsharp.com) (AGPL license)",creator);
		}

		[TestMethod]
		public void WithoutTheAgplLicence_PdfSharpRefuses_WithEmpirasException() {
			Assert.IsTrue((bool)GetInternal("HasValidLicense").Invoke(null,null),"PdfSharpSetup.Init selected the licence (AssemblyInitialize).");
			byte[] arrayBytes=PdfRead.Save(NewDocument());
			try {
				GetInternal("ResetLicense").Invoke(null,null);
				Assert.IsFalse((bool)GetInternal("HasValidLicense").Invoke(null,null));
				PdfSharpLicenseException exception=Expect.Throws<PdfSharpLicenseException>(() => new PdfDocument());
				Assert.AreEqual("The loaded license is not valid.",exception.Message);
				Assert.AreEqual(typeof(Exception),typeof(PdfSharpLicenseException).BaseType,"Derived from Exception, as empira's.");
				Expect.Throws<PdfSharpLicenseException>(() => PdfReader.Open(new MemoryStream(arrayBytes),PdfDocumentOpenMode.Import));
				TargetInvocationException targetInvocationException=Expect.Throws<TargetInvocationException>(
					() => GetInternal("CheckLicense").Invoke(null,null));
				Assert.IsInstanceOfType(targetInvocationException.InnerException,typeof(PdfSharpLicenseException));
			}
			finally {
				PdfSharpLicense.SetAgplLicense();
			}
			Assert.IsTrue((bool)GetInternal("HasValidLicense").Invoke(null,null));
			Assert.AreEqual(PRODUCER,SaveAndRead(NewDocument()).Producer,"SetAgplLicense() brings everything back.");
		}

		[TestMethod]
		public void LoadLicense_RefusesEveryKey_AndKeepsTheAgplLicence() {
			foreach(string key in new[] { "","not-a-key",Convert.ToBase64String(Encoding.UTF8.GetBytes("<License><Type>Commercial</Type></License>")) }) {
				PdfSharpLicenseException exception=Expect.Throws<PdfSharpLicenseException>(() => PdfSharpLicense.LoadLicense(key));
				StringAssert.Contains(exception.Message,"takes no license key");
				StringAssert.Contains(exception.Message,"PdfSharpLicense.SetAgplLicense()");
			}
			//Still the AGPL licence: PDFs are made and say so, nothing of a commercial licence.
			(string producer,string creator,_)=SaveAndRead(NewDocument());
			Assert.AreEqual(PRODUCER,producer);
			Assert.AreEqual(CREATOR,creator);
			Assert.IsFalse(producer.Contains("Commercial"));
		}

		[TestMethod]
		public void StandIn_IsEmpirasWrapper_Modified_WithItsApi_AndNoKeyNetworkOrMessage() {
			string standIn=File.ReadAllText(SourceBuild.Combine(SourceBuild.REPLACEMENT_FILE));
			string upstream=File.ReadAllText(Path.Combine(SourceBuild.Combine(SourceBuild.PDFSHARP_PATH),SourceBuild.REPLACED_FILE.Replace('/','\\')));
			//empira's header and copyright stay; the modification is marked and dated (GNU AGPL version 3, section 5a).
			foreach(string line in upstream.Split('\n').Select(x => x.Trim()).TakeWhile(x => x.StartsWith("//"))) {
				StringAssert.Contains(standIn,line,"empira's header line: "+line);
			}
			StringAssert.Contains(standIn,"// Copyright (c) 2005-2026 empira Software GmbH, Troisdorf (Cologne Area), Germany.");
			StringAssert.Contains(standIn,"// MODIFIED FILE (GNU AGPL version 3, section 5a). Changed by the Open PMS - Dental contributors on 2026-11-02.");
			StringAssert.Contains(standIn,"// This is a modified version of "+SourceBuild.REPLACED_FILE+" of PDFsharp 6.5.0");
			StringAssert.Contains(standIn,"tag "+SourceBuild.PDFSHARP_TAG+", commit "+SourceBuild.PDFSHARP_COMMIT);
			//The wrapper's methods, with the same signatures and the same documentation.
			Regex regexMethod=new Regex(@"(?m)^\s*(public|internal) static (\w+) (\w+)\(([^)]*)\)");
			List<string> listUpstream=regexMethod.Matches(upstream).Cast<Match>().Select(x => x.Value.Trim()).ToList();
			List<string> listStandIn=regexMethod.Matches(standIn.Substring(0,standIn.IndexOf("namespace PdfSharp.License"))).Cast<Match>().Select(x => x.Value.Trim()).ToList();
			Assert.AreEqual(5,listUpstream.Count,string.Join("\r\n",listUpstream));
			CollectionAssert.AreEqual(listUpstream,listStandIn,"The public and internal API of empira's wrapper.");
			StringAssert.Contains(standIn,"public static class PdfSharpLicense");
			//Only the AGPL path: it reads nothing, connects to nothing, writes nothing.
			string code=Regex.Replace(standIn,@"//[^\n]*","");
			foreach(string forbidden in new[] { "System.Net","Socket","Http","WebRequest","Registry","Environment.","File.","Directory.","Console.","Trace.","Debug.",
				"Process.","Assembly.Load","DllImport" })
			{
				Assert.IsFalse(code.Contains(forbidden),"The licence check uses "+forbidden);
			}
			//The public API as the program and the tests see it: the two public methods, and the exception type of empira's library.
			CollectionAssert.AreEquivalent(new[] { "LoadLicense","SetAgplLicense" },
				typeof(PdfSharpLicense).GetMethods(BindingFlags.Static|BindingFlags.Public).Select(x => x.Name).ToArray());
			Assert.AreEqual("PdfSharp.License.PdfSharpLicenseException",typeof(PdfSharpLicenseException).FullName);
			Assert.IsFalse(typeof(PdfSharpLicense).Assembly.GetTypes().Any(x => x.Namespace=="PdfSharp.License" && x.IsPublic && x!=typeof(PdfSharpLicenseException)),
				"No other public licence type.");
		}
	}
}
