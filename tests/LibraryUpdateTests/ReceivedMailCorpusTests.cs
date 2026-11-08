// The received-mail corpus (package S0-3 of the .NET 10 plan): every message of ReceivedMailCorpus goes through ProcessRawEmailMessageIn on a
// scratch database, and the e-mail preview's parts (GetMimePartsForMimeTypes, ProcessMimeTextPart, the image helpers) are read from it. The
// fields that come out are compared with Corpus/ReceivedMailCorpus.expected.txt, which was recorded with the Direct Project parser before
// received mail moved to MimeKit (step 0 of S0-3). A field that differs on purpose is listed in _dictExplained with the new value and the reason;
// any other difference fails the test, and so does an explanation that no longer matches. Each run writes what it got to
// %TEMP%\s03_corpus_actual.txt. The database is named s03_*, made from db/init/new-database.sql, and dropped afterwards; inconclusive without
// the local MariaDB. Synthetic data only. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace LibraryUpdateTests {
	[TestClass]
	public class ReceivedMailCorpusTests {
		private const string PREFIX="s03_";
		private static readonly string DB_NAME=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
			+Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000)+"_corpus";
		private const string EXPECTED_FILE=@"tests\LibraryUpdateTests\Corpus\ReceivedMailCorpus.expected.txt";
		private static string _skipReason;
		private static bool _isBuilt;
		///<summary>The A to Z folder of this class (with the A folder FileAtoZ looks for): the attachments of the corpus are written here.</summary>
		private static string _folderAtoZ;
		private string _localAtoZpathOld;
		private bool _isUnitTestOld;

		///<summary>The fields that differ from the Direct-based parser on purpose: "case|field" gives the new value (as the actual file writes it)
		///and why. Filled by step 2 of S0-3 (received mail on MimeKit).</summary>
		private static readonly Dictionary<string,Tuple<string,string>> _dictExplained=new Dictionary<string,Tuple<string,string>>();

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			try {
				DropDatabase(DB_NAME);
				using(MySqlConnection connection=ScratchDb.OpenServer()) {
					ScratchDb.Exec(connection,"CREATE DATABASE `"+DB_NAME+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				}
				_isBuilt=true;
				ScratchDb.LoadNewDatabase(DB_NAME);
				ScratchDb.Connect(DB_NAME);
				Security.CurUser=new Userod { UserNum=1,UserName="s03" };
				ForkMigrationRunner.Run();
				Cache.Refresh(InvalidType.AllLocal);
				_folderAtoZ=Path.Combine(Path.GetTempPath(),"s03_corpus_atoz_"+Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(Path.Combine(_folderAtoZ,"A"));
			}
			catch(Exception ex) {
				_skipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_isBuilt) {
				try {
					DropDatabase(DB_NAME);
				}
				catch(Exception) {
					//Nothing else uses it; it holds synthetic data only.
				}
			}
			if(_folderAtoZ!=null) {
				try {
					Directory.Delete(_folderAtoZ,true);
				}
				catch(Exception) {
				}
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			_isUnitTestOld=ODBuild.IsUnitTest;
			ODBuild.IsUnitTest=true;
			ODInitialize.Initialize();
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			bool isOtherDatabase=DataConnection.GetDatabaseName()!=DB_NAME;
			ScratchDb.Connect(DB_NAME);
			if(isOtherDatabase) {
				Cache.Refresh(InvalidType.AllLocal);
			}
			Security.CurUser=new Userod { UserNum=1,UserName="s03" };
			_localAtoZpathOld=OpenPmsDentalBusiness.FileIO.FileAtoZ.LocalAtoZpath;
			OpenPmsDentalBusiness.FileIO.FileAtoZ.LocalAtoZpath=_folderAtoZ;
		}

		[TestCleanup]
		public void TestCleanup() {
			if(_skipReason!=null) {
				return;
			}
			OpenPmsDentalBusiness.FileIO.FileAtoZ.LocalAtoZpath=_localAtoZpathOld;
			ODBuild.IsUnitTest=_isUnitTestOld;
		}

		private static void DropDatabase(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("These tests only drop databases whose name starts with "+PREFIX+".");
			}
			using(MySqlConnection connection=ScratchDb.OpenServer()) {
				ScratchDb.Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
		}

		///<summary>Text as one line of ASCII: backslash, CR, LF, tab and every character outside printable ASCII as an escape.</summary>
		public static string Escape(string text) {
			if(text==null) {
				return "(null)";
			}
			StringBuilder stringBuilder=new StringBuilder();
			foreach(char c in text) {
				if(c=='\\') {
					stringBuilder.Append("\\\\");
				}
				else if(c=='\r') {
					stringBuilder.Append("\\r");
				}
				else if(c=='\n') {
					stringBuilder.Append("\\n");
				}
				else if(c=='\t') {
					stringBuilder.Append("\\t");
				}
				else if(c<32 || c>126) {
					stringBuilder.Append("\\u"+((int)c).ToString("x4"));
				}
				else {
					stringBuilder.Append(c);
				}
			}
			return stringBuilder.ToString();
		}

		private static string Sha(byte[] arrayBytes) {
			using(SHA256 sha256=SHA256.Create()) {
				return BitConverter.ToString(sha256.ComputeHash(arrayBytes)).Replace("-","").Substring(0,16).ToLowerInvariant();
			}
		}

		private static string DescribeDate(DateTime dateTime) {
			if(Math.Abs((dateTime-DateTime.Now).TotalMinutes)<10) {
				return "(now)";
			}
			if(dateTime.Kind==DateTimeKind.Local) {
				return "utc "+dateTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
			}
			return dateTime.Kind.ToString().ToLowerInvariant()+" "+dateTime.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
		}

		///<summary>The fields of one message as "field: value" lines, in a fixed order.</summary>
		private static List<KeyValuePair<string,string>> Describe(CorpusMessage corpusMessage) {
			List<KeyValuePair<string,string>> listFields=new List<KeyValuePair<string,string>>();
			void add(string field,string value) {
				listFields.Add(new KeyValuePair<string,string>(field,value));
			}
			EmailAddress emailAddressInbox=new EmailAddress() { EmailUsername=corpusMessage.Inbox };
			EmailMessage emailMessage=null;
			try {
				emailMessage=EmailMessages.ProcessRawEmailMessageIn(corpusMessage.Raw,0,emailAddressInbox,false);
			}
			catch(Exception ex) {
				add("Error",Escape(ex.GetType().Name+": "+ex.Message));
			}
			if(emailMessage!=null) {
				add("FromAddress",Escape(emailMessage.FromAddress));
				add("ToAddress",Escape(emailMessage.ToAddress));
				add("CcAddress",Escape(emailMessage.CcAddress));
				add("BccAddress",Escape(emailMessage.BccAddress));
				add("Subject",Escape(emailMessage.Subject));
				add("MsgDateTime",DescribeDate(emailMessage.MsgDateTime));
				add("BodyText",Escape(emailMessage.BodyText));
				add("SentOrReceived",emailMessage.SentOrReceived.ToString());
				add("RecipientAddress",Escape(emailMessage.RecipientAddress));
				if(emailMessage.RawEmailIn==corpusMessage.Raw) {
					add("RawEmailIn","(as received)");
				}
				else {
					//The Direct-based parser replaced boundaries that start with another boundary by new GUIDs (32 hex digits), different each run.
					add("RawEmailIn","(changed) "+Escape(System.Text.RegularExpressions.Regex.Replace(emailMessage.RawEmailIn,"[0-9a-f]{32}","(guid)")));
				}
				add("Attachments",emailMessage.Attachments.Count.ToString());
				for(int i=0;i<emailMessage.Attachments.Count;i++) {
					EmailAttach emailAttach=emailMessage.Attachments[i];
					byte[] arrayBytes=File.ReadAllBytes(Path.Combine(EmailAttaches.GetAttachPath(),emailAttach.ActualFileName));
					add("Attachment "+i,Escape(emailAttach.DisplayedFileName)+" "+arrayBytes.Length+" bytes sha "+Sha(arrayBytes));
				}
			}
			//The e-mail preview (EmailPreviewControl.ParseMIME and Show Images).
			var listListParts=EmailMessages.GetMimePartsForMimeTypes(corpusMessage.Raw,emailAddressInbox,
				"text/html","text/plain","image/","application/octet-stream");
			add("Preview parts",listListParts[0].Count+" html, "+listListParts[1].Count+" text, "+listListParts[2].Count+" image, "
				+listListParts[3].Count+" octet-stream");
			if(listListParts[0].Count>0) {
				add("Preview body","html "+Escape(EmailMessages.ProcessMimeTextPart(listListParts[0][0])));
			}
			else if(listListParts[1].Count>0) {
				add("Preview body","text "+Escape(EmailMessages.ProcessMimeTextPart(listListParts[1][0])));
			}
			else {
				add("Preview body","(the stored body text)");
			}
			for(int i=2;i<4;i++) {
				for(int j=0;j<listListParts[i].Count;j++) {
					add("Preview "+(i==2 ? "image " : "octet-stream ")+j,"cid "+Escape(EmailMessages.GetMimeImageContentId(listListParts[i][j]))
						+" name "+Escape(EmailMessages.GetMimeImageFileName(listListParts[i][j])));
				}
			}
			return listFields;
		}

		///<summary>Reads the recorded file: case name to its "field: value" lines.</summary>
		private static Dictionary<string,List<KeyValuePair<string,string>>> ReadRecording(string text) {
			Dictionary<string,List<KeyValuePair<string,string>>> dictCases=new Dictionary<string,List<KeyValuePair<string,string>>>();
			List<KeyValuePair<string,string>> listFields=null;
			foreach(string line in text.Replace("\r\n","\n").Split('\n')) {
				if(line.StartsWith("=== ")) {
					listFields=new List<KeyValuePair<string,string>>();
					dictCases[line.Substring(4)]=listFields;
					continue;
				}
				int index=line.IndexOf(": ",StringComparison.Ordinal);
				if(listFields==null || index<0) {
					continue;
				}
				listFields.Add(new KeyValuePair<string,string>(line.Substring(0,index),line.Substring(index+2)));
			}
			return dictCases;
		}

		[TestMethod]
		public void Corpus_ParsesAsRecordedWithTheDirectParser() {
			List<CorpusMessage> listMessages=ReceivedMailCorpus.GetAll();
			StringBuilder stringBuilderActual=new StringBuilder();
			Dictionary<string,List<KeyValuePair<string,string>>> dictActual=new Dictionary<string,List<KeyValuePair<string,string>>>();
			foreach(CorpusMessage corpusMessage in listMessages) {
				List<KeyValuePair<string,string>> listFields=Describe(corpusMessage);
				dictActual[corpusMessage.Name]=listFields;
				stringBuilderActual.Append("=== "+corpusMessage.Name+"\r\n");
				foreach(KeyValuePair<string,string> pair in listFields) {
					stringBuilderActual.Append(pair.Key+": "+pair.Value+"\r\n");
				}
			}
			string pathActual=Path.Combine(Path.GetTempPath(),"s03_corpus_actual.txt");
			File.WriteAllText(pathActual,stringBuilderActual.ToString(),Encoding.ASCII);
			string pathExpected;
			try {
				pathExpected=ScratchDb.FindRepoFile(EXPECTED_FILE);
			}
			catch(FileNotFoundException) {
				Assert.Inconclusive("Nothing recorded yet. This run wrote "+pathActual);
				return;
			}
			Dictionary<string,List<KeyValuePair<string,string>>> dictExpected=ReadRecording(File.ReadAllText(pathExpected,Encoding.ASCII));
			List<string> listProblems=new List<string>();
			HashSet<string> hashSetExplainedUsed=new HashSet<string>();
			foreach(CorpusMessage corpusMessage in listMessages) {
				if(!dictExpected.TryGetValue(corpusMessage.Name,out List<KeyValuePair<string,string>> listExpected)) {
					listProblems.Add(corpusMessage.Name+": not recorded");
					continue;
				}
				List<KeyValuePair<string,string>> listActual=dictActual[corpusMessage.Name];
				List<string> listFieldNames=listExpected.Select(x => x.Key).Union(listActual.Select(x => x.Key)).ToList();
				foreach(string field in listFieldNames) {
					string valueExpected=listExpected.Where(x => x.Key==field).Select(x => x.Value).FirstOrDefault()??"(none)";
					string valueActual=listActual.Where(x => x.Key==field).Select(x => x.Value).FirstOrDefault()??"(none)";
					string key=corpusMessage.Name+"|"+field;
					if(_dictExplained.TryGetValue(key,out Tuple<string,string> tupleExplained)) {
						hashSetExplainedUsed.Add(key);
						if(valueActual!=tupleExplained.Item1) {
							listProblems.Add(key+": explained as \""+tupleExplained.Item1+"\" but is \""+valueActual+"\"");
						}
						continue;
					}
					if(valueActual!=valueExpected) {
						listProblems.Add(key+": recorded \""+valueExpected+"\", now \""+valueActual+"\"");
					}
				}
			}
			foreach(string key in _dictExplained.Keys.Where(x => !hashSetExplainedUsed.Contains(x))) {
				listProblems.Add(key+": explained, but not a field of the corpus");
			}
			Assert.AreEqual(0,listProblems.Count,"Differences from the recording (all results are in "+pathActual+"):\r\n"+string.Join("\r\n",listProblems));
		}
	}
}
