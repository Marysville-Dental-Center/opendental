// Runs PDF work in a fresh application domain whose configuration turns on the .NET Framework's network tracing (System.Net, System.Net.Sockets,
// System.Net.Http), and records what the library did there: every network trace line, what it wrote to the console and to Trace, and which
// assemblies it loaded. A fresh domain also starts with no PDFsharp licence selected and no font resolver.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Xml.Linq;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using OpenPmsDentalBusiness;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Color=System.Drawing.Color;
using Document=MigraDoc.DocumentObjectModel.Document;

namespace PdfLibraryTests {
	///<summary>What happened in the isolated domain.</summary>
	[Serializable]
	public class IsolatedPdfResult {
		///<summary>The exception type and message of the first PdfDocument made before PdfSharpSetup.Init, or "" when it was made.</summary>
		public string ErrorWithoutLicence="";
		///<summary>Network trace lines written while the licence was selected and the PDFs were made, read and rendered. Must stay empty.</summary>
		public List<string> ListNetworkLinesDuringPdf=new List<string>();
		///<summary>Network trace lines written by the check at the end, which only creates a socket (it connects nowhere): proof that the tracing
		///is on.</summary>
		public List<string> ListNetworkLinesControl=new List<string>();
		///<summary>Console output and Trace messages during the PDF work.</summary>
		public string ConsoleAndTraceOutput="";
		///<summary>Assemblies loaded in the domain.</summary>
		public List<string> ListAssemblies=new List<string>();
		public int PageCountPdfSharp;
		public int PageCountMigraDoc;
		public string Producer="";
	}

	///<summary>Records every line the network trace sources write. Named in the isolated domain's configuration.</summary>
	public class NetworkTraceRecorder:TraceListener {
		private static readonly object _lock=new object();
		private static readonly List<string> _listLines=new List<string>();

		public static List<string> TakeLines() {
			lock(_lock) {
				List<string> listLines=new List<string>(_listLines);
				_listLines.Clear();
				return listLines;
			}
		}

		public override void Write(string message) {
			lock(_lock) {
				_listLines.Add(message);
			}
		}

		public override void WriteLine(string message) {
			Write(message);
		}
	}

	///<summary>Records Trace and Debug messages during the PDF work.</summary>
	public class MessageRecorder:TraceListener {
		public readonly List<string> ListLines=new List<string>();

		public override void Write(string message) {
			ListLines.Add(message);
		}

		public override void WriteLine(string message) {
			ListLines.Add(message);
		}
	}

	///<summary>Lives in the isolated domain and does the PDF work there.</summary>
	public class IsolatedPdfWorker:MarshalByRefObject {
		public IsolatedPdfResult Run() {
			IsolatedPdfResult isolatedPdfResult=new IsolatedPdfResult();
			NetworkTraceRecorder.TakeLines();
			TextWriter textWriterOut=Console.Out;
			TextWriter textWriterError=Console.Error;
			StringWriter stringWriter=new StringWriter();
			MessageRecorder messageRecorder=new MessageRecorder();
			Console.SetOut(stringWriter);
			Console.SetError(stringWriter);
			Trace.Listeners.Add(messageRecorder);
			try {
				try {
					new PdfDocument();
				}
				catch(Exception ex) {
					Exception exceptionBase=ex.GetBaseException();
					isolatedPdfResult.ErrorWithoutLicence=exceptionBase.GetType().FullName+": "+exceptionBase.Message;
				}
				PdfSharpSetup.Init();
				byte[] arrayBytesPdf=MakePdf();
				PdfDocument pdfDocumentRead=PdfRead.Open(arrayBytesPdf,PdfDocumentOpenMode.Import);
				isolatedPdfResult.PageCountPdfSharp=pdfDocumentRead.PageCount;
				isolatedPdfResult.Producer=PdfRead.Open(arrayBytesPdf).Info.Producer;
				isolatedPdfResult.PageCountMigraDoc=PdfRead.Open(MakeMigraDocPdf()).PageCount;
			}
			finally {
				Trace.Listeners.Remove(messageRecorder);
				Console.SetOut(textWriterOut);
				Console.SetError(textWriterError);
			}
			isolatedPdfResult.ListNetworkLinesDuringPdf=NetworkTraceRecorder.TakeLines();
			isolatedPdfResult.ConsoleAndTraceOutput=stringWriter.ToString()+string.Join("",messageRecorder.ListLines);
			//The control: creating a socket (it is never connected) writes a System.Net.Sockets trace line, so an empty list above means no
			//network call, not tracing that is off.
			using(Socket socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp)) {
			}
			isolatedPdfResult.ListNetworkLinesControl=NetworkTraceRecorder.TakeLines();
			isolatedPdfResult.ListAssemblies=AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetName().Name).OrderBy(x => x).ToList();
			return isolatedPdfResult;
		}

		///<summary>Two pages with text in two fonts and a picture.</summary>
		private static byte[] MakePdf() {
			PdfDocument pdfDocument=new PdfDocument();
			for(int i=0;i<2;i++) {
				PdfPage pdfPage=pdfDocument.AddPage();
				using(XGraphics xGraphics=XGraphics.FromPdfPage(pdfPage))
				using(Bitmap bitmap=new Bitmap(40,20)) {
					xGraphics.DrawString("Isolated page "+(i+1),new XFont("Arial",12),XBrushes.Black,72,72);
					xGraphics.DrawString("Fallback font",new XFont("No Such Font LIBPDF",12),XBrushes.Black,72,100);
					using(Graphics graphics=Graphics.FromImage(bitmap)) {
						graphics.Clear(Color.SteelBlue);
					}
					xGraphics.DrawImage(XImage.FromGdiPlusImage(bitmap),72,120,40,20);
				}
			}
			return PdfRead.Save(pdfDocument);
		}

		///<summary>A MigraDoc document with a paragraph and a table, rendered to PDF.</summary>
		private static byte[] MakeMigraDocPdf() {
			Document document=new Document();
			Section section=document.AddSection();
			section.AddParagraph("Isolated MigraDoc");
			Table table=section.AddTable();
			table.AddColumn(Unit.FromInch(2));
			table.AddColumn(Unit.FromInch(2));
			Row row=table.AddRow();
			row.Cells[0].AddParagraph("Cell A");
			row.Cells[1].AddParagraph("Cell B");
			PdfDocumentRenderer pdfDocumentRenderer=new PdfDocumentRenderer();
			pdfDocumentRenderer.Document=document;
			pdfDocumentRenderer.RenderDocument();
			return PdfRead.Save(pdfDocumentRenderer.PdfDocument);
		}
	}

	public static class IsolatedPdfRun {
		///<summary>Runs IsolatedPdfWorker in a new domain with this test assembly's configuration (its binding redirects) plus the network trace
		///sources, and unloads it afterwards.</summary>
		public static IsolatedPdfResult Run() {
			string folder=Path.GetDirectoryName(typeof(IsolatedPdfRun).Assembly.Location);
			string pathConfig=Path.Combine(Path.GetTempPath(),"libpdf_"+Guid.NewGuid().ToString("N")+".config");
			File.WriteAllText(pathConfig,CreateConfig(typeof(IsolatedPdfRun).Assembly.Location+".config"));
			AppDomain appDomain=null;
			try {
				AppDomainSetup appDomainSetup=new AppDomainSetup();
				appDomainSetup.ApplicationBase=folder;
				appDomainSetup.ConfigurationFile=pathConfig;
				appDomain=AppDomain.CreateDomain("PdfLibraryTests.Isolated",null,appDomainSetup);
				IsolatedPdfWorker isolatedPdfWorker=(IsolatedPdfWorker)appDomain.CreateInstanceAndUnwrap(typeof(IsolatedPdfWorker).Assembly.FullName,
					typeof(IsolatedPdfWorker).FullName);
				return isolatedPdfWorker.Run();
			}
			finally {
				if(appDomain!=null) {
					AppDomain.Unload(appDomain);
				}
				try {
					File.Delete(pathConfig);
				}
				catch(Exception) {
				}
			}
		}

		///<summary>The test assembly's own configuration (binding redirects) with system.diagnostics sources for the network tracing, each
		///writing every message (Verbose) to NetworkTraceRecorder.</summary>
		public static string CreateConfig(string pathOwnConfig) {
			XDocument xDocument=File.Exists(pathOwnConfig) ? XDocument.Load(pathOwnConfig) : new XDocument(new XElement("configuration"));
			XElement xElementConfiguration=xDocument.Root;
			xElementConfiguration.Elements("system.diagnostics").Remove();
			XElement xElementSources=new XElement("sources");
			foreach(string source in new[] { "System.Net","System.Net.Sockets","System.Net.Http","System.Net.Cache","System.Net.HttpListener" }) {
				xElementSources.Add(new XElement("source",new XAttribute("name",source),new XAttribute("switchValue","Verbose"),
					new XElement("listeners",new XElement("add",new XAttribute("name","libpdfNetwork")))));
			}
			XElement xElementDiagnostics=new XElement("system.diagnostics",xElementSources,
				new XElement("sharedListeners",new XElement("add",new XAttribute("name","libpdfNetwork"),
					new XAttribute("type",typeof(NetworkTraceRecorder).FullName+", "+typeof(NetworkTraceRecorder).Assembly.GetName().Name))));
			//configSections, when there is one, must stay the first element.
			XElement xElementConfigSections=xElementConfiguration.Element("configSections");
			if(xElementConfigSections!=null) {
				xElementConfigSections.AddAfterSelf(xElementDiagnostics);
			}
			else {
				xElementConfiguration.AddFirst(xElementDiagnostics);
			}
			return xDocument.ToString();
		}
	}
}
