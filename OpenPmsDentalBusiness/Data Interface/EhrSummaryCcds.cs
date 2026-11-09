using CodeBase;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;

namespace OpenPmsDentalBusiness{
	///<summary></summary>
	public class EhrSummaryCcds{
		#region CachePattern

		private class EhrSummaryCcdCache : CacheListAbs<EhrSummaryCcd> {
			protected override List<EhrSummaryCcd> GetCacheFromDb() {
				string command="SELECT * FROM ehrsummaryccd";
				return Crud.EhrSummaryCcdCrud.SelectMany(command);
			}
			protected override List<EhrSummaryCcd> TableToList(DataTable table) {
				return Crud.EhrSummaryCcdCrud.TableToList(table);
			}
			protected override EhrSummaryCcd Copy(EhrSummaryCcd ehrSummaryCcd) {
				return ehrSummaryCcd.Copy();
			}
			protected override DataTable ListToTable(List<EhrSummaryCcd> listEhrSummaryCcds) {
				return Crud.EhrSummaryCcdCrud.ListToTable(listEhrSummaryCcds,"EhrSummaryCcd");
			}
			protected override void FillCacheIfNeeded() {
				EhrSummaryCcds.GetTableFromCache(false);
			}
		}
		
		///<summary>The object that accesses the cache in a thread-safe manner.</summary>
		private static EhrSummaryCcdCache _ehrSummaryCcdCache=new EhrSummaryCcdCache();

		///<summary>Refreshes the cache and returns it as a DataTable. This will refresh the ClientWeb's cache and the ServerWeb's cache.</summary>
		public static DataTable RefreshCache() {
			return GetTableFromCache(true);
		}

		///<summary>Fills the local cache with the passed in DataTable.</summary>
		public static void FillCacheFromTable(DataTable table) {
			_ehrSummaryCcdCache.FillCacheFromTable(table);
		}

		///<summary>Always refreshes the ClientWeb's cache.</summary>
		public static DataTable GetTableFromCache(bool doRefreshCache) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				DataTable table=Meth.GetTable(MethodBase.GetCurrentMethod(),doRefreshCache);
				_ehrSummaryCcdCache.FillCacheFromTable(table);
				return table;
			}
			return _ehrSummaryCcdCache.GetTableFromCache(doRefreshCache);
		}

		#endregion
		
		///<summary></summary>
		public static List<EhrSummaryCcd> Refresh(long patNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<EhrSummaryCcd>>(MethodBase.GetCurrentMethod(),patNum);
			}
			string command="SELECT * FROM ehrsummaryccd WHERE PatNum = "+POut.Long(patNum)+" ORDER BY DateSummary";
			return Crud.EhrSummaryCcdCrud.SelectMany(command);
		}

		///<summary></summary>
		public static long Insert(EhrSummaryCcd ehrSummaryCcd){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				ehrSummaryCcd.EhrSummaryCcdNum=Meth.GetLong(MethodBase.GetCurrentMethod(),ehrSummaryCcd);
				return ehrSummaryCcd.EhrSummaryCcdNum;
			}
			return Crud.EhrSummaryCcdCrud.Insert(ehrSummaryCcd);
		}

		///<summary>Returns null if no record is found.</summary>
		public static EhrSummaryCcd GetOneForEmailAttach(long emailAttachNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<EhrSummaryCcd>(MethodBase.GetCurrentMethod(),emailAttachNum);
			}
			string command="SELECT * FROM ehrsummaryccd WHERE EmailAttachNum="+POut.Long(emailAttachNum)+" LIMIT 1";
			return Crud.EhrSummaryCcdCrud.SelectOne(command);
		}

		///<summary></summary>
		public static void Update(EhrSummaryCcd ehrSummaryCcd) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),ehrSummaryCcd);
				return;
			}
			Crud.EhrSummaryCcdCrud.Update(ehrSummaryCcd);
		}

		///<summary>The display stylesheets, embedded in this library (Resources\Ehr, docs/port-notes/EHR-3.md): "CCD" gives HL7's CDA.xsl (CDA-core-xsl
		///4.1.0-beta.2), "CCR" the AAFP's ccr.xsl (a copy marked as modified), "CdaL10n" and "CdaNarrativeBlock" the two files CDA.xsl reads beside
		///itself (cda_l10n.xml and cda_narrativeblock.xml). Any other name gives "". Open Dental loaded the first two from its proprietary EHR.dll
		///through reflection; these are the public Apache-2.0 stylesheets. They run only in the child process that EhrStylesheetStarter starts.</summary>
		public static string GetEhrResource(string strResourceName) {
			string text=null;
			if(strResourceName=="CCD") {
				text=EhrStylesheetStarter.GetResourceText(EhrStylesheetStarter.FILE_CDA_XSL);
			}
			else if(strResourceName=="CCR") {
				text=EhrStylesheetStarter.GetResourceText(EhrStylesheetStarter.FILE_CCR_XSL);
			}
			else if(strResourceName=="CdaL10n") {
				text=EhrStylesheetStarter.GetResourceText(EhrStylesheetStarter.FILE_CDA_L10N);
			}
			else if(strResourceName=="CdaNarrativeBlock") {
				text=EhrStylesheetStarter.GetResourceText(EhrStylesheetStarter.FILE_CDA_NARRATIVEBLOCK);
			}
			return text??"";
		}

		///<summary>The name of an exported or attached CCD.</summary>
		public const string FileNameCcdXml="ccd.xml";
		///<summary>The CDA.xsl text under the name the xml-stylesheet instruction of EhrCCD's documents names.</summary>
		public const string FileNameCcdXsl="ccd.xsl";
		///<summary>The page the program's own stylesheet makes of a single exported CCD.</summary>
		public const string FileNameCcdHtml="ccd.html";
		///<summary>The Apache-2.0 text beside file exports (E-11).</summary>
		public const string FileNameStylesheetsLicense="stylesheets-license.txt";

		///<summary>The files that go with an exported or e-mailed CCD, as file name and text, in this order: ccd.xsl (CDA.xsl) and its two companions
		///under their own names (CDA.xsl reads them beside itself), unmodified. With doIncludeLicense, stylesheets-license.txt last: the Apache-2.0
		///text, which file exports write (E-11); e-mails attach the stylesheets only.</summary>
		public static List<KeyValuePair<string,string>> GetCcdStylesheetFiles(bool doIncludeLicense) {
			List<KeyValuePair<string,string>> listFiles=new List<KeyValuePair<string,string>>();
			listFiles.Add(new KeyValuePair<string,string>(FileNameCcdXsl,GetEhrResource("CCD")));
			listFiles.Add(new KeyValuePair<string,string>(EhrStylesheetStarter.FILE_CDA_L10N,GetEhrResource("CdaL10n")));
			listFiles.Add(new KeyValuePair<string,string>(EhrStylesheetStarter.FILE_CDA_NARRATIVEBLOCK,GetEhrResource("CdaNarrativeBlock")));
			if(doIncludeLicense) {
				listFiles.Add(new KeyValuePair<string,string>(FileNameStylesheetsLicense,Properties.Resources.EhrStylesheetsLicense));
			}
			return listFiles;
		}

		/*
		Only pull out the methods below as you need them.  Otherwise, leave them commented out.


		///<summary>Gets one EhrSummaryCcd from the db.</summary>
		public static EhrSummaryCcd GetOne(long ehrSummaryCcdNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				return Meth.GetObject<EhrSummaryCcd>(MethodBase.GetCurrentMethod(),ehrSummaryCcdNum);
			}
			return Crud.EhrSummaryCcdCrud.SelectOne(ehrSummaryCcdNum);
		}

		///<summary></summary>
		public static void Delete(long ehrSummaryCcdNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),ehrSummaryCcdNum);
				return;
			}
			string command= "DELETE FROM ehrsummaryccd WHERE EhrSummaryCcdNum = "+POut.Long(ehrSummaryCcdNum);
			Db.NonQ(command);
		}
		*/
	}

	///<summary>The parent side of the stylesheet child (docs/specs/ehr-rebuild-plan.md, EHR-3). XSLT never runs in the program's own process: a
	///stylesheet that recurses without end overflows the stack, which ends the process whatever catches it, and one that loops never returns. The
	///program's own exe runs the shipped stylesheets in a short-lived child process (ARG_RENDER, OpenPmsDental.EhrRenderChild) that this class
	///starts with no window, feeds one framed document at a time through a pipe and kills when a document takes longer than 15 seconds; an overflow
	///or a loop ends or stalls only the child. A document over the limits (GetLimitError) never reaches a child. The document never goes to disk.
	///A stylesheet that came with an e-mail never runs anywhere (E-14): the child serves only the four embedded files. No database is used, so
	///nothing here goes through the Middle Tier: the child runs on the computer that shows or exports the document.</summary>
	public class EhrStylesheetStarter {
		///<summary>The first command-line argument of the child. ProgramEntry.Main tests for it (as a literal) before anything else.</summary>
		public const string ARG_RENDER="/ehr-render";
		///<summary>The second argument: the version of the frames. A child that gets another one ends with EXIT_BAD_ARGUMENTS.</summary>
		public const string PROTOCOL_VERSION="1";
		///<summary>The kind byte of a frame to the child: a CCD (any CDA R2 document), rendered with CDA.xsl.</summary>
		public const byte KIND_CCD=1;
		///<summary>The kind byte of a frame to the child: a CCR, rendered with ccr.xsl.</summary>
		public const byte KIND_CCR=2;
		///<summary>The status byte of a reply: the bytes are the page, UTF-8.</summary>
		public const byte STATUS_HTML=0;
		///<summary>The status byte of a reply: the bytes are the reason the document was not rendered, UTF-8.</summary>
		public const byte STATUS_FAILED=1;
		///<summary>The child's exit code for missing or unknown arguments.</summary>
		public const int EXIT_BAD_ARGUMENTS=2;
		///<summary>The child's exit code when its watchdog ended it: 20 seconds without progress, which happens only when its parent is gone.</summary>
		public const int EXIT_WATCHDOG=3;
		///<summary>The child's exit code for a frame it cannot read (a wrong kind byte, a length out of range, a frame cut short).</summary>
		public const int EXIT_BROKEN_FRAME=4;
		///<summary>The exit code .NET Framework processes report for a stack overflow (COR_E_STACKOVERFLOW).</summary>
		public const int EXIT_COR_E_STACKOVERFLOW=unchecked((int)0x800703E9);
		///<summary>Windows' exit code for a stack overflow (STATUS_STACK_OVERFLOW). Reports show both codes from .NET Framework processes.</summary>
		public const int EXIT_STATUS_STACK_OVERFLOW=unchecked((int)0xC00000FD);
		///<summary>The largest document a child gets: 32 MB as UTF-8. Real CDA documents are far smaller.</summary>
		public const int MAX_DOCUMENT_BYTES=32*1024*1024;
		///<summary>The deepest element nesting a child gets. Real CDA documents nest a few dozen levels; the stylesheets' templates recurse with the
		///depth, on the child's 16 MB stack.</summary>
		public const int MAX_DEPTH=200;
		///<summary>The longest a child may take on one document, compiling the stylesheet included, before it is killed (15 seconds).</summary>
		public const int TIMEOUT_DOCUMENT_MS=15000;
		///<summary>The largest page a child may send back (128 MB).</summary>
		public const int MAX_REPLY_BYTES=128*1024*1024;
		///<summary>Standard error of the real child is kept up to this many characters (64 KB), and read all the time, so its pipe never fills.</summary>
		public const int MAX_ERROR_CHARS=64*1024;
		///<summary>The scheme of the URIs under which the child serves the four files, for example ehr-res:///CDA.xsl.</summary>
		public const string URI_SCHEME="ehr-res";
		public const string FILE_CDA_XSL="CDA.xsl";
		public const string FILE_CDA_L10N="cda_l10n.xml";
		public const string FILE_CDA_NARRATIVEBLOCK="cda_narrativeblock.xml";
		public const string FILE_CCR_XSL="ccr.xsl";
		///<summary>The seam of the tests: when set, it gives the child instead of a new OpenPmsDental.exe process. Null in the program.</summary>
		public static Func<IEhrRenderChild> FuncStartChild=null;

		///<summary>The text of one of the four embedded files the child serves, by its file name (FILE_CDA_XSL, FILE_CDA_L10N,
		///FILE_CDA_NARRATIVEBLOCK, FILE_CCR_XSL); null for any other name.</summary>
		public static string GetResourceText(string fileName) {
			if(fileName==FILE_CDA_XSL) {
				return Properties.Resources.EhrCdaXsl;
			}
			if(fileName==FILE_CDA_L10N) {
				return Properties.Resources.EhrCdaL10n;
			}
			if(fileName==FILE_CDA_NARRATIVEBLOCK) {
				return Properties.Resources.EhrCdaNarrativeBlock;
			}
			if(fileName==FILE_CCR_XSL) {
				return Properties.Resources.EhrCcrXsl;
			}
			return null;
		}

		///<summary>Null when xml may go to a child; otherwise why not: empty, over 32 MB as UTF-8, nested more than 200 levels deep, with a document
		///type declaration (DTD), whose entities could expand, or not well-formed XML. The depth is counted with an XmlReader, which does not
		///recurse, so a deep document cannot overflow this process's stack here either. It fails closed: a document this reader cannot read to its
		///end is refused, so its depth is never left uncounted. The reader is that of GetXmlReaderSettings; LoadXmlDocument builds the program's own
		///XmlDocument with the same reader, so the parser that builds a DOM is never more lenient than the one that counted (XmlDocument.LoadXml's
		///legacy reader accepts a character reference such as &amp;#0;, which stops this reader).</summary>
		public static string GetLimitError(string xml) {
			if(string.IsNullOrEmpty(xml)) {
				return "The document is empty.";
			}
			if(xml.Length>MAX_DOCUMENT_BYTES || Encoding.UTF8.GetByteCount(xml)>MAX_DOCUMENT_BYTES) {
				return "The document is larger than 32 MB.";
			}
			try {
				using(XmlReader xmlReader=XmlReader.Create(new StringReader(xml),GetXmlReaderSettings())) {
					while(xmlReader.Read()) {
						if(xmlReader.NodeType==XmlNodeType.Element && xmlReader.Depth>=MAX_DEPTH) {
							return "The document is nested more than "+MAX_DEPTH+" levels deep.";
						}
					}
				}
			}
			catch(Exception ex) {
				//DtdProcessing.Prohibit stops at the document type declaration. Anything else is not well-formed: refused as well, because the depth
				//after the place the reader stopped was never counted.
				if(xml.Contains("<!DOCTYPE")) {
					return "The document has a document type declaration (DTD), which is not allowed.";
				}
				return "The document is not well-formed XML: "+ex.Message;
			}
			return null;
		}

		///<summary>The settings of every reader of a received document in the program's process: no DTD, no resolver, characters checked (a
		///character reference to a character XML does not allow, such as &amp;#0;, is refused).</summary>
		public static XmlReaderSettings GetXmlReaderSettings() {
			XmlReaderSettings xmlReaderSettings=new XmlReaderSettings();
			xmlReaderSettings.DtdProcessing=DtdProcessing.Prohibit;
			xmlReaderSettings.XmlResolver=null;
			xmlReaderSettings.CheckCharacters=true;
			return xmlReaderSettings;
		}

		///<summary>The document as an XmlDocument, read with the reader GetLimitError counts with (GetXmlReaderSettings), never with
		///XmlDocument.LoadXml's more lenient one. Call it only for xml that passed GetLimitError: XmlDocument's own methods, InnerXml among them,
		///recurse with the depth. Throws an XmlException when xml is not well-formed.</summary>
		public static XmlDocument LoadXmlDocument(string xml) {
			XmlDocument xmlDocument=new XmlDocument();
			xmlDocument.XmlResolver=null;
			using(XmlReader xmlReader=XmlReader.Create(new StringReader(xml??""),GetXmlReaderSettings())) {
				xmlDocument.Load(xmlReader);
			}
			return xmlDocument;
		}

		///<summary>The page the shipped stylesheet makes of xml (a CCD with CDA.xsl, a CCR with ccr.xsl), made by a child process; null with the
		///reason when there is none. Never throws.</summary>
		public static string Render(EhrRenderKind ehrRenderKind,string xml,out string reason) {
			EhrRenderResult ehrRenderResult=RenderBatch(ehrRenderKind,new List<string> { xml })[0];
			reason=ehrRenderResult.Reason;
			return ehrRenderResult.Html;
		}

		///<summary>The pages of a list of documents of one kind, in its order, made by one child, so the stylesheet is compiled once (Patient Export).
		///A document that fails (over the limits, refused by the stylesheet, a crash, a timeout) gets a null page and its reason; after a crash or a
		///timeout a new child takes the rest. Never throws.</summary>
		public static List<EhrRenderResult> RenderBatch(EhrRenderKind ehrRenderKind,IList<string> listXmls) {
			List<EhrRenderResult> listEhrRenderResults=new List<EhrRenderResult>();
			IEhrRenderChild ehrRenderChild=null;
			string reasonStart=null;//Once a child cannot be started, the rest get the same reason.
			try {
				for(int i=0;i<listXmls.Count;i++) {
					string reason=GetLimitError(listXmls[i]);
					if(reason==null && reasonStart!=null) {
						reason=reasonStart;
					}
					if(reason==null && ehrRenderChild==null) {
						try {
							ehrRenderChild=StartChild();
						}
						catch(Exception ex) {
							reasonStart="The stylesheet process could not be started: "+ex.Message;
							reason=reasonStart;
						}
					}
					if(reason!=null) {
						listEhrRenderResults.Add(new EhrRenderResult(null,reason));
						continue;
					}
					EhrRenderResult ehrRenderResult=Exchange(ehrRenderChild,ehrRenderKind,listXmls[i],out bool isChildUsable);
					listEhrRenderResults.Add(ehrRenderResult);
					if(!isChildUsable) {
						EndChild(ehrRenderChild);
						ehrRenderChild=null;
					}
				}
			}
			finally {
				if(ehrRenderChild!=null) {
					EndChild(ehrRenderChild);
				}
			}
			return listEhrRenderResults;
		}

		///<summary>The reason shown for a child that ended while it had a document: its exit code, named when it is one of the two stack-overflow
		///codes. Every non-zero exit code is treated alike.</summary>
		public static string GetExitReason(int exitCode) {
			string reason="The stylesheet process ended with exit code 0x"+exitCode.ToString("X8",CultureInfo.InvariantCulture);
			if(exitCode==EXIT_COR_E_STACKOVERFLOW) {
				reason+=" (COR_E_STACKOVERFLOW, a stack overflow)";
			}
			else if(exitCode==EXIT_STATUS_STACK_OVERFLOW) {
				reason+=" (STATUS_STACK_OVERFLOW, a stack overflow)";
			}
			return reason+".";
		}

		///<summary>Writes one frame: a kind or status byte, the length as 4 bytes (little-endian) and the bytes.</summary>
		public static void WriteFrame(Stream stream,byte kindOrStatus,byte[] arrayBytes) {
			byte[] arrayHeader=new byte[5];
			arrayHeader[0]=kindOrStatus;
			Array.Copy(GetLittleEndian(arrayBytes.Length),0,arrayHeader,1,4);
			stream.Write(arrayHeader,0,arrayHeader.Length);
			stream.Write(arrayBytes,0,arrayBytes.Length);
			stream.Flush();
		}

		///<summary>Reads one frame of at most maxLength bytes. Null at the end of the stream before the frame starts. Throws when the frame is cut
		///short or its length is out of range.</summary>
		public static byte[] ReadFrame(Stream stream,int maxLength,out byte kindOrStatus) {
			kindOrStatus=0;
			byte[] arrayHeader=new byte[5];
			int countRead=ReadFully(stream,arrayHeader,arrayHeader.Length);
			if(countRead==0) {
				return null;
			}
			if(countRead<arrayHeader.Length) {
				throw new EndOfStreamException("The frame ended after "+countRead+" of 5 header bytes.");
			}
			kindOrStatus=arrayHeader[0];
			int length=arrayHeader[1]|(arrayHeader[2]<<8)|(arrayHeader[3]<<16)|(arrayHeader[4]<<24);
			if(length<0 || length>maxLength) {
				throw new InvalidDataException("The frame's length is out of range: "+length+".");
			}
			byte[] arrayBytes=new byte[length];
			if(ReadFully(stream,arrayBytes,length)<length) {
				throw new EndOfStreamException("The frame ended before its "+length+" bytes.");
			}
			return arrayBytes;
		}

		///<summary>Reads until count bytes are in arrayBytes or the stream ends. Returns how many were read.</summary>
		private static int ReadFully(Stream stream,byte[] arrayBytes,int count) {
			int countRead=0;
			while(countRead<count) {
				int countNow=stream.Read(arrayBytes,countRead,count-countRead);
				if(countNow<=0) {
					break;
				}
				countRead+=countNow;
			}
			return countRead;
		}

		private static byte[] GetLittleEndian(int value) {
			//Masked, because this project checks arithmetic overflow and a cast of a larger value to byte would throw.
			return new byte[] { (byte)(value&0xFF),(byte)((value>>8)&0xFF),(byte)((value>>16)&0xFF),(byte)((value>>24)&0xFF) };
		}

		private static IEhrRenderChild StartChild() {
			if(FuncStartChild!=null) {
				return FuncStartChild();
			}
			return StartProcess();
		}

		///<summary>Starts the real child, OpenPmsDental.exe /ehr-render: what a batch does when FuncStartChild is null. Tests wrap it to count the
		///children a batch starts. Throws when the process cannot be started.</summary>
		public static IEhrRenderChild StartProcess() {
			return EhrRenderChildProcess.Start();
		}

		///<summary>Sends one document and waits for its page, at most 15 seconds for both. isChildUsable is false when the child is gone or was
		///killed, so the caller ends it and starts a new one for the next document.</summary>
		private static EhrRenderResult Exchange(IEhrRenderChild ehrRenderChild,EhrRenderKind ehrRenderKind,string xml,out bool isChildUsable) {
			byte[] arrayDocument=new UTF8Encoding(false).GetBytes(xml);
			byte[] arrayReply=null;
			byte status=0;
			Exception exception=null;
			//One frame out, one frame back, on its own thread, so neither pipe can hold this one beyond the time limit.
			Thread thread=new Thread(() => {
				try {
					WriteFrame(ehrRenderChild.Input,(byte)ehrRenderKind,arrayDocument);
					arrayReply=ReadFrame(ehrRenderChild.Output,MAX_REPLY_BYTES,out status);
				}
				catch(Exception ex) {
					exception=ex;
				}
			});
			thread.IsBackground=true;
			thread.Start();
			if(!thread.Join(TIMEOUT_DOCUMENT_MS)) {
				KillChild(ehrRenderChild);
				thread.Join(5000);//The kill closes the pipes, which ends the reads and writes.
				isChildUsable=false;
				return new EhrRenderResult(null,"The stylesheet did not finish within "+(TIMEOUT_DOCUMENT_MS/1000)+" seconds, so its process was ended.");
			}
			if(exception!=null || arrayReply==null) {
				//The child ended with the document: a crash (a stack overflow among them) or a broken frame.
				isChildUsable=false;
				string reason;
				if(ehrRenderChild.WaitForExit(5000)) {
					reason=GetExitReason(ehrRenderChild.ExitCode);
				}
				else {
					KillChild(ehrRenderChild);
					reason="The stylesheet process stopped answering"+(exception==null ? "" : ": "+exception.Message)+".";
				}
				return new EhrRenderResult(null,reason+GetErrorLine(ehrRenderChild));
			}
			isChildUsable=true;
			string text=new UTF8Encoding(false).GetString(arrayReply);
			if(status==STATUS_HTML) {
				return new EhrRenderResult(text,null);
			}
			return new EhrRenderResult(null,"The stylesheet could not show the document: "+text);
		}

		///<summary>The first line of what the child wrote to standard error, for the reason, or "".</summary>
		private static string GetErrorLine(IEhrRenderChild ehrRenderChild) {
			string error=ehrRenderChild.ErrorText??"";
			string line=error.Split(new[] { '\r','\n' },StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??"";
			if(line.Length>300) {
				line=line.Substring(0,300);
			}
			return line=="" ? "" : " "+line;
		}

		///<summary>Ends a child at the end of a batch: no more input, so it ends by itself; killed after 2 seconds.</summary>
		private static void EndChild(IEhrRenderChild ehrRenderChild) {
			try {
				ehrRenderChild.Input.Close();
				if(!ehrRenderChild.WaitForExit(2000)) {
					KillChild(ehrRenderChild);
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
				KillChild(ehrRenderChild);
			}
			finally {
				ehrRenderChild.Dispose();
			}
		}

		private static void KillChild(IEhrRenderChild ehrRenderChild) {
			try {
				ehrRenderChild.Kill();
			}
			catch(Exception ex) {
				ex.DoNothing();//It has ended already.
			}
		}

		///<summary>The real child: OpenPmsDental.exe /ehr-render from the folder of the program that runs this code (AppDomain's base directory, never
		///the entry assembly, which is the test host in tests), with no window and redirected streams.</summary>
		private class EhrRenderChildProcess:IEhrRenderChild {
			private Process _process;
			private StringBuilder _stringBuilderError=new StringBuilder();
			private object _lockError=new object();

			public static EhrRenderChildProcess Start() {
				EhrRenderChildProcess ehrRenderChildProcess=new EhrRenderChildProcess();
				ProcessStartInfo processStartInfo=new ProcessStartInfo();
				processStartInfo.FileName=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,ProgramNames.ExeFileName);
				processStartInfo.Arguments=ARG_RENDER+" "+PROTOCOL_VERSION;
				processStartInfo.WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory;
				processStartInfo.UseShellExecute=false;//The streams are redirected.
				processStartInfo.RedirectStandardInput=true;
				processStartInfo.RedirectStandardOutput=true;
				processStartInfo.RedirectStandardError=true;
				processStartInfo.CreateNoWindow=true;
				Process process=new Process();
				process.StartInfo=processStartInfo;
				process.ErrorDataReceived+=ehrRenderChildProcess.Process_ErrorDataReceived;
				process.Start();
				process.BeginErrorReadLine();
				ehrRenderChildProcess._process=process;
				return ehrRenderChildProcess;
			}

			private void Process_ErrorDataReceived(object sender,DataReceivedEventArgs e) {
				if(e.Data==null) {
					return;
				}
				lock(_lockError) {
					if(_stringBuilderError.Length<MAX_ERROR_CHARS) {
						_stringBuilderError.AppendLine(e.Data.Length>MAX_ERROR_CHARS ? e.Data.Substring(0,MAX_ERROR_CHARS) : e.Data);
					}
				}
			}

			public Stream Input {
				get {
					return _process.StandardInput.BaseStream;
				}
			}

			public Stream Output {
				get {
					return _process.StandardOutput.BaseStream;
				}
			}

			public string ErrorText {
				get {
					lock(_lockError) {
						return _stringBuilderError.ToString();
					}
				}
			}

			public int ExitCode {
				get {
					return _process.ExitCode;
				}
			}

			public bool WaitForExit(int milliseconds) {
				return _process.WaitForExit(milliseconds);
			}

			public void Kill() {
				if(!_process.HasExited) {
					_process.Kill();
				}
			}

			public void Dispose() {
				_process.Dispose();
			}
		}
	}

	///<summary>One child process of EhrStylesheetStarter, as the starter uses it. The real one is OpenPmsDental.exe /ehr-render; tests give their
	///own through EhrStylesheetStarter.FuncStartChild.</summary>
	public interface IEhrRenderChild:IDisposable {
		///<summary>The child's standard input. Closing it ends the child.</summary>
		Stream Input { get; }
		///<summary>The child's standard output.</summary>
		Stream Output { get; }
		///<summary>What the child wrote to standard error so far, at most 64 KB.</summary>
		string ErrorText { get; }
		///<summary>Valid once WaitForExit has returned true.</summary>
		int ExitCode { get; }
		bool WaitForExit(int milliseconds);
		void Kill();
	}

	///<summary>A page made by the child, or why there is none.</summary>
	public class EhrRenderResult {
		///<summary>The page (UTF-8 HTML text), or null.</summary>
		public string Html;
		///<summary>Null when Html is set; otherwise why it is not.</summary>
		public string Reason;

		public EhrRenderResult(string html,string reason) {
			Html=html;
			Reason=reason;
		}
	}

	///<summary>Which stylesheet the child runs. The values are the kind bytes of the frames; never stored.</summary>
	public enum EhrRenderKind {
		///<summary>1. A CCD, or any CDA R2 document: CDA.xsl.</summary>
		Ccd=1,
		///<summary>2. A CCR: ccr.xsl.</summary>
		Ccr=2,
	}
}