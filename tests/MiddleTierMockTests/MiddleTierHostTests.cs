// Tests of the Middle Tier host seam, package S0-4 of the .NET 10 plan (docs/specs/net10-plan.md, stage 0). MiddleTierHost (DataConnectionBase)
// holds what the libraries read from System.Web before (HostingEnvironment, HttpContext.Current), and the ASMX code-behind gives it the System.Web
// values through ProcessDto's host form. The tests show that the values reach every place that read System.Web: DtoProcessor (the config file of
// the application name, the client address in the verbose log), Userods.LoadDatabaseInfoFromFile (the log folder, the ApplicationName of the
// config file and the mismatch message), ConnectionStoreBase (the content root's ConnectionStore.xml, on any thread) and Plugins (the request
// folder); that a request's values are cleared when it ends, also when it fails, so the next request on the thread does not see them; and that
// with no host every value is what System.Web gave outside a hosted request. The runs through DtoProcessor and the business caches happen in a new
// AppDomain of this test's folder (HostSeamProbe), which stands for a Middle Tier worker that has just started (no database connection, nothing
// cached) and leaves this assembly's own state alone: its scratch database connection, the program cache and the logger. No database is used:
// the config files written here hold a connection string that does not parse, so no connection is attempted. Files go to a folder of the
// temporary folder, deleted afterwards. PluginLoader (WpfControlsOD, which this assembly does not reference) and ServiceMain.asmx.cs (an ASP.NET
// page) are checked in their source. Synthetic values only; the client addresses are from the documentation range 192.0.2.0/24.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace MiddleTierMockTests {
	[TestClass]
	public class MiddleTierHostTests {
		private const string APP_NAME="/S04Seam";
		private const string CLIENT_IP="192.0.2.10";
		///<summary>A folder of this test under the temporary folder: the content root of the requests, and where the config files go.</summary>
		private string _pathFolder;

		[TestInitialize]
		public void SetupTest() {
			_pathFolder=Path.Combine(Path.GetTempPath(),"s04_seam_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_pathFolder);
		}

		[TestCleanup]
		public void TearDownTest() {
			try {
				Directory.Delete(_pathFolder,true);
			}
			catch(Exception) {
				//A file the logger of a test domain still held. The temporary folder is cleaned up by Windows.
			}
		}

		#region The host context itself (this domain; what each test sets is put back)
		///<summary>A request's values are this thread's from BeginRequest to EndRequest, and EndRequest clears them, so a pooled thread does not carry
		///them into its next request. The content root is the process's and stays, as HostingEnvironment.ApplicationPhysicalPath did. The request
		///folder (HttpContext.Current.Server.MapPath(null) before) is there only during the request.</summary>
		[TestMethod]
		public void MiddleTierHost_BeginRequestSetsTheValues_EndRequestClearsThem() {
			string contentRoot=_pathFolder+"\\";//As ApplicationPhysicalPath gives it: with the trailing backslash.
			string contentRootBefore=MiddleTierHost.ContentRoot;
			try {
				MiddleTierHost.BeginRequest(contentRoot,APP_NAME,CLIENT_IP);
				Assert.IsTrue(MiddleTierHost.IsInRequest);
				Assert.AreEqual(APP_NAME,MiddleTierHost.ApplicationName);
				Assert.AreEqual(CLIENT_IP,MiddleTierHost.ClientIp);
				Assert.AreEqual(contentRoot,MiddleTierHost.ContentRoot);
				Assert.AreEqual(contentRoot,MiddleTierHost.GetRequestContentRoot());
				Assert.AreEqual(contentRoot,ConnectionStoreBase.GetWebAppPath());
				MiddleTierHost.EndRequest();
				Assert.IsFalse(MiddleTierHost.IsInRequest);
				Assert.IsNull(MiddleTierHost.ApplicationName);
				Assert.IsNull(MiddleTierHost.ClientIp);
				Assert.AreEqual(nameof(InvalidOperationException),HostSeamProbe.Describe(() => MiddleTierHost.GetRequestContentRoot()),
					"Outside a request there is no request folder, as HttpContext.Current was null there.");
				Assert.AreEqual(contentRoot,MiddleTierHost.ContentRoot,"The content root is the process's, as HostingEnvironment's was.");
				Assert.AreEqual(contentRoot,ConnectionStoreBase.GetWebAppPath());
			}
			finally {
				MiddleTierHost.EndRequest();
				MiddleTierHost.ContentRoot=contentRootBefore;
			}
		}

		///<summary>The request values are the thread's: another thread, like one a request starts, has no request (HttpContext.Current did not flow
		///to new threads either), while it sees the process's content root, as every thread saw HostingEnvironment's. A request on the other thread
		///leaves this thread's request alone.</summary>
		[TestMethod]
		public void MiddleTierHost_RequestValuesAreThisThreadsOnly_TheContentRootIsTheProcesss() {
			string contentRoot=_pathFolder+"\\";
			string contentRootBefore=MiddleTierHost.ContentRoot;
			try {
				MiddleTierHost.BeginRequest(contentRoot,APP_NAME,CLIENT_IP);
				Dictionary<string,string> dictOther=new Dictionary<string,string>();
				Thread thread=new Thread(() => {
					HostSeamProbe.AddContext(dictOther,"Other");
					dictOther["Other.ContentRoot"]=HostSeamProbe.Show(MiddleTierHost.ContentRoot);
					dictOther["Other.WebAppPath"]=HostSeamProbe.Show(ConnectionStoreBase.GetWebAppPath());
					dictOther["Other.RequestFolder"]=HostSeamProbe.Describe(() => MiddleTierHost.GetRequestContentRoot());
					MiddleTierHost.BeginRequest(contentRoot,"/S04Other","192.0.2.20");
					MiddleTierHost.EndRequest();
				});
				thread.Start();
				thread.Join();
				Assert.AreEqual("False",dictOther["Other.IsInRequest"]);
				Assert.AreEqual("(null)",dictOther["Other.ApplicationName"]);
				Assert.AreEqual("(null)",dictOther["Other.ClientIp"]);
				Assert.AreEqual(nameof(InvalidOperationException),dictOther["Other.RequestFolder"]);
				Assert.AreEqual(contentRoot,dictOther["Other.ContentRoot"]);
				Assert.AreEqual(contentRoot,dictOther["Other.WebAppPath"]);
				Assert.IsTrue(MiddleTierHost.IsInRequest,"The other thread's request ended; this one goes on.");
				Assert.AreEqual(APP_NAME,MiddleTierHost.ApplicationName);
				Assert.AreEqual(CLIENT_IP,MiddleTierHost.ClientIp);
				Assert.AreEqual(contentRoot,MiddleTierHost.GetRequestContentRoot());
			}
			finally {
				MiddleTierHost.EndRequest();
				MiddleTierHost.ContentRoot=contentRootBefore;
			}
		}
		#endregion

		#region Through the business layer, in a new domain
		///<summary>With no host, as in the program, the mock server and a Middle Tier worker before its first request, every value is what
		///System.Web gave outside a hosted request: null, and no request.</summary>
		[TestMethod]
		public void NoHost_EveryValueIsWhatSystemWebGaveOutsideAHostedRequest() {
			Dictionary<string,string> dict=RunInNewDomain(probe => probe.NoHost());
			Assert.AreEqual("(null)",dict["ContentRoot"]);
			Assert.AreEqual("(null)",dict["WebAppPath"]);
			Assert.AreEqual("False",dict["NoHost.IsInRequest"]);
			Assert.AreEqual("(null)",dict["NoHost.ApplicationName"]);
			Assert.AreEqual("(null)",dict["NoHost.ClientIp"]);
			Assert.AreEqual(nameof(InvalidOperationException),dict["RequestFolder"]);
		}

		///<summary>Four requests on one thread, as a pooled thread of the server takes them, each failing at the config file. The application name
		///picks its own config file in the content root (DtoProcessor), else the default one there; the root application (/) takes the default one;
		///Userods checks the file's ApplicationName against the request's and names both in its message; the log folder is the content root's
		///Logging folder; the verbose log line of each request has that request's client address. The fourth request uses the form the mock server
		///calls, with no host values: its client address is logged as [INVALID], and the ApplicationName check fails with a NullReferenceException,
		///as it did with no hosted request before (HostingEnvironment.ApplicationVirtualPath was null). After each request no request value is left
		///on the thread.</summary>
		[TestMethod]
		public void ProcessDto_HostForm_TheValuesReachDtoProcessorAndUserods_AndAreClearedAfterEachRequest() {
			Dictionary<string,string> dict=RunInNewDomain(probe => probe.ProcessDtoRequests(_pathFolder));
			AssertConfigMismatch(dict,"Own",Path.Combine(_pathFolder,"S04SeamConfig.xml"),"/S04Seam");
			AssertConfigMismatch(dict,"Absent",Path.Combine(_pathFolder,"S04AbsentConfig.xml"),"/S04Absent");
			AssertConfigMismatch(dict,"Root",Path.Combine(_pathFolder,"Config.xml"),"/");
			Assert.AreEqual(nameof(NullReferenceException),dict["NoHost.Type"],dict["NoHost.Message"]);
			foreach(string key in new[] { "Own","Absent","Root","NoHost" }) {
				Assert.AreEqual("False",dict[key+".IsInRequest"],key);
				Assert.AreEqual("(null)",dict[key+".ApplicationName"],key);
				Assert.AreEqual("(null)",dict[key+".ClientIp"],key);
			}
			Assert.AreEqual(Path.Combine(_pathFolder,"Logging"),dict["LoggerDirOverride"],"Userods: the content root's Logging folder.");
			if(dict["HasRoomToWrite"]!="True") {
				Assert.Inconclusive("The logger needs 5 GB free on the drive of "+_pathFolder+" to write the verbose log; the client addresses were not checked.");
			}
			List<string> listClientIps=Regex.Matches(dict["Log"],@"Client IP:\s*([^,\s]+),").Cast<Match>().Select(x => x.Groups[1].Value).ToList();
			CollectionAssert.AreEqual(new List<string> { "192.0.2.10","192.0.2.11","192.0.2.12","[INVALID]" },listClientIps,dict["Log"]);
		}

		///<summary>A config file without an ApplicationName, or with an empty one, gets the request's application name written into it (Userods),
		///then the connection fails on its connection string, and the message names the file.</summary>
		[TestMethod]
		public void ProcessDto_HostForm_AMissingOrEmptyApplicationNameGetsTheRequestsName() {
			Dictionary<string,string> dict=RunInNewDomain(probe => probe.ApplicationNameWritten(_pathFolder));
			foreach(string key in new[] { "Missing","Empty" }) {
				StringAssert.Contains(dict[key+".Message"],"Connection to database failed",key);
				StringAssert.Contains(dict[key+".Message"],Path.Combine(_pathFolder,"S04"+key+"Config.xml"),key);
				Assert.AreEqual("/S04"+key,dict[key+".File"],key);
				Assert.AreEqual("False",dict[key+".IsInRequest"],key);
			}
		}

		///<summary>ConnectionStoreBase reads ConnectionStore.xml from the content root (it read HostingEnvironment.ApplicationPhysicalPath), also on
		///a thread that handles no request, as on any thread of the ASMX host before.</summary>
		[TestMethod]
		public void ConnectionStore_ReadsTheContentRootsFile_OnAThreadWithoutARequest() {
			Dictionary<string,string> dict=RunInNewDomain(probe => probe.ConnectionStoreOnAnyThread(_pathFolder));
			Assert.AreEqual("False",dict["IsInRequest"]);
			Assert.AreEqual(_pathFolder+"\\",dict["WebAppPath"]);
			Assert.AreEqual("db.example.test",dict["ServerName"],dict.ContainsKey("Error") ? dict["Error"] : "");
			Assert.AreEqual("s04_seam",dict["DatabaseName"]);
		}

		///<summary>On the Middle Tier, Plugins looks for a plug-in in the request folder (HttpContext.Current.Server.MapPath(null) before): the
		///versioned file found there is copied to the plug-in's name there (the load of the copy fails, it is not an assembly, and is skipped). On a
		///thread that handles no request it throws, as HttpContext.Current was null there (InvalidOperationException in place of the
		///NullReferenceException).</summary>
		[TestMethod]
		public void Plugins_TakeTheRequestFolder_AndThrowOutsideARequest() {
			Dictionary<string,string> dict=RunInNewDomain(probe => probe.PluginFolder(_pathFolder));
			Assert.AreEqual("ok",dict["InRequest"]);
			Assert.AreEqual("True",dict["Copied"],"The versioned file in the request folder is copied to S04SeamPlugin.dll there.");
			Assert.AreEqual(nameof(InvalidOperationException),dict["NoRequest"]);
		}
		#endregion

		#region Source checks
		///<summary>No library the Middle Tier or the program loads reads HostingEnvironment or HttpContext any more (12 uses in 5 files before):
		///only the ASMX code-behind does, and it gives the values to ProcessDto. Comment lines are not counted.</summary>
		[TestMethod]
		public void Source_NoLibraryReadsHostingEnvironmentOrHttpContext() {
			string pathRoot=Path.GetDirectoryName(ScratchDb.FindRepoFile("OpenDental.Local.slnf"));
			List<string> listHits=new List<string>();
			foreach(string folder in new[] { "CodeBase","DataConnectionBase","OpenPmsDentalBusiness","WpfControlsOD","OpenPmsDental" }) {
				foreach(string path in Directory.EnumerateFiles(Path.Combine(pathRoot,folder),"*.cs",SearchOption.AllDirectories)) {
					string pathRelative=path.Substring(pathRoot.Length+1);
					if(Regex.IsMatch(pathRelative,@"\\(bin|obj)\\")) {
						continue;
					}
					string[] arrayLines=File.ReadAllLines(path);
					for(int i=0;i<arrayLines.Length;i++) {
						if(arrayLines[i].TrimStart().StartsWith("//")) {
							continue;
						}
						if(Regex.IsMatch(arrayLines[i],@"\b(HostingEnvironment|HttpContext)\b")) {
							listHits.Add(pathRelative+":"+(i+1)+": "+arrayLines[i].Trim());
						}
					}
				}
			}
			Assert.AreEqual("",string.Join("\r\n",listHits));
		}

		///<summary>The ASMX code-behind gives ProcessDto's host form the System.Web values the libraries read themselves before, and the mock server
		///keeps the form without host values. Both plug-in loaders take the request folder from the host context.</summary>
		[TestMethod]
		public void Source_TheHostFillsTheContext_TheMockServerDoesNot_BothPluginLoadersReadIt() {
			StringAssert.Contains(ReadCode(@"OpenPmsDentalServer\ServiceMain.asmx.cs"),"returnDtoProcessor.ProcessDto(dtoString,"
				+"HostingEnvironment.ApplicationPhysicalPath,HostingEnvironment.ApplicationVirtualPath,Context.Request.UserHostAddress);");
			StringAssert.Contains(ReadCode(@"OpenPmsDentalBusiness\WebServices\OpenDentalServerMockIIS.cs"),
				"returnRunWebMethod(()=>DtoProcessor.ProcessDto(dtoString));");
			foreach(string path in new[] { @"OpenPmsDentalBusiness\Plugins\Plugins.cs",@"WpfControlsOD\Bridges\PluginLoader.cs" }) {
				StringAssert.Contains(ReadCode(path),"if(RemotingClient.MiddleTierRole==MiddleTierRole.ServerMT){"
					+"dllPath=ODFileUtils.CombinePaths(MiddleTierHost.GetRequestContentRoot(),listPrograms[i].PluginDllName);}",path);
			}
		}
		#endregion

		#region Helpers
		///<summary>Runs the probe in a new AppDomain of this test's folder and configuration (its binding redirects), and unloads it afterwards.</summary>
		private static Dictionary<string,string> RunInNewDomain(Func<HostSeamProbe,Dictionary<string,string>> funcRun) {
			string folder=Path.GetDirectoryName(typeof(HostSeamProbe).Assembly.Location);
			AppDomainSetup appDomainSetup=new AppDomainSetup();
			appDomainSetup.ApplicationBase=folder;
			string pathConfig=typeof(HostSeamProbe).Assembly.Location+".config";
			if(File.Exists(pathConfig)) {
				appDomainSetup.ConfigurationFile=pathConfig;
			}
			AppDomain appDomain=AppDomain.CreateDomain("MiddleTierHostTests-"+Guid.NewGuid().ToString("N"),null,appDomainSetup);
			try {
				HostSeamProbe probe=(HostSeamProbe)appDomain.CreateInstanceAndUnwrap(typeof(HostSeamProbe).Assembly.FullName,typeof(HostSeamProbe).FullName);
				return funcRun(probe);
			}
			finally {
				try {
					AppDomain.Unload(appDomain);
				}
				catch(CannotUnloadAppDomainException) {
					//A thread of the domain (the logger's clean-up) did not end in time. The domain goes with the test process.
				}
			}
		}

		///<summary>A request that failed at the config file because its ApplicationName is another application's: the message names the config file
		///the request's application name gives and that name.</summary>
		private static void AssertConfigMismatch(Dictionary<string,string> dict,string key,string pathExpected,string applicationName) {
			string message=dict[key+".Message"];
			Assert.AreEqual("Exception",dict[key+".Type"],key+": "+message);
			StringAssert.Contains(message,"Multiple middle tier servers",key);
			StringAssert.Contains(message,"should be using the following config file:",key);
			StringAssert.Contains(message,pathExpected,key);
			StringAssert.Matches(message,new Regex(@"expecting an ApplicationName of:\s*"+Regex.Escape(applicationName)+@"\s*$"),key);
		}

		///<summary>A source file of the repository without its comment lines and without any white space, so a check does not depend on the
		///layout or the comments.</summary>
		private static string ReadCode(string pathRelative) {
			IEnumerable<string> listLines=File.ReadAllLines(ScratchDb.FindRepoFile(pathRelative)).Where(x => !x.TrimStart().StartsWith("//"));
			return Regex.Replace(string.Join("\n",listLines),@"\s+","");
		}
		#endregion
	}

	///<summary>Runs in a new AppDomain (MiddleTierHostTests.RunInNewDomain): a Middle Tier worker that has just started, with no database
	///connection and nothing cached. Every method returns what it saw as text, since only serializable values cross the domain boundary.</summary>
	public class HostSeamProbe:MarshalByRefObject {
		///<summary>Any payload: each request below fails at the config file, before the payload is read.</summary>
		private const string DTO="<S04Seam />";

		public Dictionary<string,string> NoHost() {
			Dictionary<string,string> dict=new Dictionary<string,string>();
			dict["ContentRoot"]=Show(MiddleTierHost.ContentRoot);
			dict["WebAppPath"]=Show(ConnectionStoreBase.GetWebAppPath());
			AddContext(dict,"NoHost");
			dict["RequestFolder"]=Describe(() => MiddleTierHost.GetRequestContentRoot());
			return dict;
		}

		public Dictionary<string,string> ProcessDtoRequests(string folder) {
			Dictionary<string,string> dict=new Dictionary<string,string>();
			string contentRoot=folder+"\\";
			//The application's own config file names another application, with verbose logging on and no LogDirectory (so the log goes under the
			//content root). The default config file names this application.
			WriteConfig(Path.Combine(folder,"S04SeamConfig.xml"),"<ApplicationName>/S04Other</ApplicationName>");
			WriteConfig(Path.Combine(folder,ProgramNames.MiddleTierConfigFileName),"<ApplicationName>/S04Seam</ApplicationName>");
			AddResponse(dict,"Own",DtoProcessor.ProcessDto(DTO,contentRoot,"/S04Seam","192.0.2.10"));
			AddResponse(dict,"Absent",DtoProcessor.ProcessDto(DTO,contentRoot,"/S04Absent","192.0.2.11"));
			AddResponse(dict,"Root",DtoProcessor.ProcessDto(DTO,contentRoot,"/","192.0.2.12"));
			//The form the mock server calls, with no host values, as the next request of the same thread.
			AddResponse(dict,"NoHost",DtoProcessor.ProcessDto(DTO,contentRoot));
			dict["LoggerDirOverride"]=Logger.LoggerDirOverride;
			dict["HasRoomToWrite"]=Logger.HasRoomToWrite().ToString();
			Logger.CloseLogger();
			dict["Log"]=ReadFiles(Path.Combine(folder,"Logging","Verbose"));
			return dict;
		}

		public Dictionary<string,string> ApplicationNameWritten(string folder) {
			Dictionary<string,string> dict=new Dictionary<string,string>();
			string contentRoot=folder+"\\";
			string pathMissing=Path.Combine(folder,"S04MissingConfig.xml");
			WriteConfig(pathMissing,"");
			string pathEmpty=Path.Combine(folder,"S04EmptyConfig.xml");
			WriteConfig(pathEmpty,"<ApplicationName></ApplicationName>");
			AddResponse(dict,"Missing",DtoProcessor.ProcessDto(DTO,contentRoot,"/S04Missing","192.0.2.30"));
			dict["Missing.File"]=ReadApplicationName(pathMissing);
			AddResponse(dict,"Empty",DtoProcessor.ProcessDto(DTO,contentRoot,"/S04Empty","192.0.2.31"));
			dict["Empty.File"]=ReadApplicationName(pathEmpty);
			return dict;
		}

		public Dictionary<string,string> ConnectionStoreOnAnyThread(string folder) {
			File.WriteAllText(Path.Combine(folder,"ConnectionStore.xml"),"<?xml version=\"1.0\"?>\r\n<ListCentralConnections>\r\n"
				+"  <CentralConnection>\r\n"
				+"    <ServerName>db.example.test</ServerName>\r\n"
				+"    <DatabaseName>s04_seam</DatabaseName>\r\n"
				+"    <MySqlUser>s04</MySqlUser>\r\n"
				+"    <MySqlPassword></MySqlPassword>\r\n"
				+"    <Note>DentalOffice</Note>\r\n"
				+"  </CentralConnection>\r\n"
				+"</ListCentralConnections>\r\n",new UTF8Encoding(false));
			//The host's first request gives the content root, then ends.
			MiddleTierHost.BeginRequest(folder+"\\","/S04Seam","192.0.2.40");
			MiddleTierHost.EndRequest();
			Dictionary<string,string> dict=new Dictionary<string,string>();
			//Another thread of the worker, which handles no request.
			Thread thread=new Thread(() => {
				dict["IsInRequest"]=MiddleTierHost.IsInRequest.ToString();
				dict["WebAppPath"]=Show(ConnectionStoreBase.GetWebAppPath());
				try {
					CentralConnectionBase centralConnectionBase=ConnectionStoreBase.GetConnection(ConnectionNames.DentalOffice);
					dict["ServerName"]=centralConnectionBase.ServerName;
					dict["DatabaseName"]=centralConnectionBase.DatabaseName;
				}
				catch(Exception ex) {
					dict["ServerName"]="(failed)";
					dict["Error"]=ex.GetType().Name+": "+ex.Message;
				}
			});
			thread.Start();
			thread.Join();
			return dict;
		}

		public Dictionary<string,string> PluginFolder(string folder) {
			Dictionary<string,string> dict=new Dictionary<string,string>();
			Version version=typeof(Db).Assembly.GetName().Version;
			//Not an assembly: the load of the copy fails, and Plugins skips it.
			File.WriteAllBytes(Path.Combine(folder,"S04SeamPlugin"+version.Major+"."+version.Minor+".dll"),Encoding.ASCII.GetBytes("S04 not an assembly"));
			Program program=new Program();
			program.ProgramNum=1;
			program.ProgName="S04Seam";
			program.ProgDesc="S04 seam plug-in";
			program.Enabled=true;
			program.PluginDllName="S04SeamPlugin[VersionMajMin].dll";
			Programs.FillCacheFromTable(OpenPmsDentalBusiness.Crud.ProgramCrud.ListToTable(new List<Program> { program },"Program"));
			//This thread is the Middle Tier's, as a request's thread is.
			RemotingClient.SetRemotingRoleT(MiddleTierRole.ServerMT);
			MiddleTierHost.BeginRequest(folder+"\\","/S04Seam","192.0.2.50");
			try {
				dict["InRequest"]=Describe(() => Plugins.LoadAllPlugins());
			}
			finally {
				MiddleTierHost.EndRequest();
			}
			dict["Copied"]=File.Exists(Path.Combine(folder,"S04SeamPlugin.dll")).ToString();
			dict["NoRequest"]=Describe(() => Plugins.LoadAllPlugins());
			return dict;
		}

		///<summary>The request values of this thread, under the given key.</summary>
		public static void AddContext(Dictionary<string,string> dict,string key) {
			dict[key+".IsInRequest"]=MiddleTierHost.IsInRequest.ToString();
			dict[key+".ApplicationName"]=Show(MiddleTierHost.ApplicationName);
			dict[key+".ClientIp"]=Show(MiddleTierHost.ClientIp);
		}

		public static string Show(string value) {
			return value??"(null)";
		}

		///<summary>"ok", or the type name of the exception the action threw.</summary>
		public static string Describe(Action action) {
			try {
				action();
				return "ok";
			}
			catch(Exception ex) {
				return ex.GetType().Name;
			}
		}

		///<summary>Same for a function; its value is not needed.</summary>
		public static string Describe(Func<string> func) {
			return Describe(new Action(() => func()));
		}

		///<summary>The exception a response carries (ProcessDto returns every failure as a DtoException), then the request values left on this thread
		///after the request.</summary>
		private static void AddResponse(Dictionary<string,string> dict,string key,string response) {
			DtoException dtoException=null;
			try {
				dtoException=DataTransferObject.Deserialize(response) as DtoException;
			}
			catch(Exception) {
				//Not an exception; shown as it is below.
			}
			dict[key+".Type"]=dtoException==null ? "(no exception)" : dtoException.ExceptionType;
			dict[key+".Message"]=dtoException==null ? response : dtoException.Message;
			AddContext(dict,key);
		}

		///<summary>A Middle Tier config file whose connection string does not parse (no connection is attempted), with verbose logging on and no
		///LogDirectory, and the given ApplicationName element, or none.</summary>
		private static void WriteConfig(string path,string elementApplicationName) {
			File.WriteAllText(path,"<?xml version=\"1.0\"?>\r\n<ConnectionSettings>\r\n"
				+"  <DatabaseConnection>\r\n"
				+"    <ConnectionString>S04NotAnOption=1</ConnectionString>\r\n"
				+"    <DatabaseType>MySql</DatabaseType>\r\n"
				+(elementApplicationName=="" ? "" : "    "+elementApplicationName+"\r\n")
				+"    <VerboseLogging>true</VerboseLogging>\r\n"
				+"    <LogDirectory></LogDirectory>\r\n"
				+"  </DatabaseConnection>\r\n"
				+"</ConnectionSettings>\r\n",new UTF8Encoding(false));
		}

		private static string ReadApplicationName(string path) {
			XmlDocument xmlDocument=new XmlDocument();
			xmlDocument.Load(path);
			XmlNode xmlNode=xmlDocument.SelectSingleNode("//DatabaseConnection/ApplicationName");
			return xmlNode==null ? "(none)" : xmlNode.InnerText;
		}

		///<summary>The text of every file under the folder, oldest name first; empty when the folder does not exist.</summary>
		private static string ReadFiles(string folder) {
			if(!Directory.Exists(folder)) {
				return "";
			}
			StringBuilder stringBuilder=new StringBuilder();
			foreach(string path in Directory.GetFiles(folder,"*",SearchOption.AllDirectories).OrderBy(x => x)) {
				using(FileStream fileStream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
				using(StreamReader streamReader=new StreamReader(fileStream)) {
					stringBuilder.Append(streamReader.ReadToEnd());
				}
			}
			return stringBuilder.ToString();
		}
	}
}
