// V-PEARL2 (Open Dental 26.1): the Pearl bridge's "Log Level" program property and the local troubleshooting log it controls.
// The file sits in the VendorDirect folder so the business project compiles it without a csproj edit; it belongs to the Pearl bridge and uses the
// bridge's namespace, OpenDentBusiness.Pearl, like PearlApiClient.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using CodeBase;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness.Pearl {
	///<summary>Writes the Pearl bridge's troubleshooting lines to the local Logger folder, in its "Pearl" subfolder (Logger\Pearl next to the program),
	///with Logger's own dated files and age-based cleanup. Nothing is sent anywhere. How much is written follows the bridge's "Log Level" property
	///(Setup > Program Links > Pearl), read on every line so a change applies at once:
	///0 (the default, also for a missing or unreadable value): errors only, which are failed calls, timeouts and rejected or deleted results.
	///1: errors, plus one line per uploaded image and per completed result.
	///2: verbose, for troubleshooting: also every API call (endpoint, HTTP status, time taken) and each check for results.
	///Every line is scrubbed first: the client secret, auth tokens, presigned upload URLs and signatures are never written, and image bytes or request
	///bodies are never passed in. Logging never throws.</summary>
	public static class PearlLog {
		///<summary>The program property's description, as the public Pearl bridge page shows it.</summary>
		public const string LOG_LEVEL_PROPERTY="Log Level";
		///<summary>The default value: errors only.</summary>
		public const string LOG_LEVEL_DEFAULT="0";
		///<summary>The Logger subfolder.</summary>
		public const string SUBDIRECTORY="Pearl";
		///<summary>Longer lines are cut, so a large error response cannot flood the log.</summary>
		public const int MAX_LINE_LENGTH=2000;
		///<summary>Reads a Pearl program property value from the cache, "" when the row or the program is missing. Replaceable for tests.</summary>
		public static Func<string,string> FuncGetPropVal=GetPropValFromCache;
		///<summary>Writes one finished line. Replaceable for tests.</summary>
		public static Action<string> ActionWriteLine=WriteLineToLoggerFolder;
		///<summary>The document and request a line belongs to. AsyncLocal, so it also reaches the HTTP handler, which APIRequest runs on a task.</summary>
		private static readonly AsyncLocal<PearlLogContext> _asyncLocalContext=new AsyncLocal<PearlLogContext>();

		#region Level
		///<summary>Maps a stored "Log Level" value to a level. Blank, unreadable or negative values, and 0, are Error (the default); 1 is Information;
		///2 is Verbose. Values above 2 also read as Verbose (inferred: the public page documents only 0 and 2).</summary>
		public static LogLevel ParseLogLevel(string value) {
			int level;
			if(!int.TryParse((value??"").Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out level) || level<=0) {
				return LogLevel.Error;
			}
			if(level==1) {
				return LogLevel.Information;
			}
			return LogLevel.Verbose;
		}

		///<summary>The level set in the Pearl program link. Error when it can't be read. Never throws.</summary>
		public static LogLevel GetLogLevel() {
			try {
				return ParseLogLevel(FuncGetPropVal(LOG_LEVEL_PROPERTY));
			}
			catch(Exception ex) {
				ex.DoNothing();
				return LogLevel.Error;
			}
		}

		///<summary>True if a line of this level would be written now.</summary>
		public static bool IsEnabled(LogLevel logLevel) {
			return logLevel<=GetLogLevel();
		}

		///<summary>The values offered for the "Log Level" property, in the order of GetLogLevelDisplays.</summary>
		public static List<string> GetLogLevelValues() {
			return new List<string> { "0","1","2" };
		}

		///<summary>English display text for each value of GetLogLevelValues. The setup window translates it.</summary>
		public static List<string> GetLogLevelDisplays() {
			return new List<string> {
				"0 - Errors only (default)",
				"1 - Errors, uploads and results",
				"2 - Verbose, for troubleshooting",
			};
		}
		#endregion Level

		#region Property row
		///<summary>Inserts the practice-wide "Log Level" row (value 0) for the Pearl program if it is missing, so it shows in the program link's grid. A
		///database from Open Dental 26.1 already has it; one from 24.3 or 25.3 does not, and reads as 0 until the row exists. Returns true if this call
		///found the row missing (the row is then in the database and the cache, and other workstations are signaled). False for any other program.
		///</summary>
		public static bool EnsureLogLevelProperty(Program program) {
			Meth.NoCheckMiddleTierRole();
			if(program==null || program.ProgramNum==0 || program.ProgName!=ProgramName.Pearl.ToString()) {
				return false;
			}
			if(ProgramProperties.GetWhere(x => x.ProgramNum==program.ProgramNum && x.ClinicNum==0 && x.PropertyDesc==LOG_LEVEL_PROPERTY).Count>0) {
				return false;
			}
			//InsertIfMissing checks the database too, so a row another workstation just added is not duplicated.
			VendorCredentials.EnsureSetting(program.ProgramNum,LOG_LEVEL_PROPERTY,0,LOG_LEVEL_DEFAULT);
			ProgramProperties.RefreshCache();
			Signalods.SetInvalid(InvalidType.Programs);
			return true;
		}

		///<summary>The default FuncGetPropVal: the practice-wide (ClinicNum 0) value of a Pearl property from the cache, "" if missing.</summary>
		public static string GetPropValFromCache(string propertyDesc) {
			long programNum=Programs.GetProgramNum(ProgramName.Pearl);
			if(programNum==0) {
				return "";
			}
			return ProgramProperties.GetPropVal(programNum,propertyDesc,0);
		}
		#endregion Property row

		#region Writing
		public static void Error(string message) {
			Write(LogLevel.Error,message);
		}

		public static void Info(string message) {
			Write(LogLevel.Information,message);
		}

		public static void Verbose(string message) {
			Write(LogLevel.Verbose,message);
		}

		///<summary>Writes the message if the current level includes logLevel: the level label, the scrubbed message and the document and request of the
		///current scope, tab separated. Logger adds the time stamp. Never throws.</summary>
		public static void Write(LogLevel logLevel,string message) {
			try {
				if(logLevel>GetLogLevel()) {
					return;
				}
				string line=GetLevelLabel(logLevel)+"\t"+Scrub(message);
				PearlLogContext pearlLogContext=_asyncLocalContext.Value;
				if(pearlLogContext!=null && !pearlLogContext.IsEmpty) {
					line+="\t"+Scrub(pearlLogContext.ToString());
				}
				ActionWriteLine?.Invoke(line);
			}
			catch(Exception ex) {
				ex.DoNothing();//The log must never stop an upload or a check for results.
			}
		}

		///<summary>The default ActionWriteLine: Logger's dated files in the Pearl subfolder, with Logger's usual cleanup of old files.</summary>
		public static void WriteLineToLoggerFolder(string line) {
			Logger.WriteLine(line,SUBDIRECTORY);
		}

		public static string GetLevelLabel(LogLevel logLevel) {
			switch(logLevel) {
				case LogLevel.Error:
					return "ERROR";
				case LogLevel.Information:
					return "INFO";
				default:
					return "VERBOSE";
			}
		}

		///<summary>Logs, as an error, why the Pearl worker thread for one image stopped: an upload or results check that threw, an image already sent,
		///or the 10 minute wait for results running out. Called from the worker's exception handler, before the user's alert is made.</summary>
		public static void LogWorkerFailure(long docNum,string requestId,Exception ex) {
			using(BeginScope(docNum,requestId)) {
				Error("Stopped: "+DescribeException(ex));
			}
		}

		///<summary>One line for an exception: its message, with the inner exception's type when there is one. Scrubbed later by Write.</summary>
		public static string DescribeException(Exception ex) {
			if(ex==null) {
				return "unknown error";
			}
			string text=ex.Message;
			if(ex.InnerException!=null) {
				text+=" ("+ex.InnerException.GetType().Name+": "+ex.InnerException.Message+")";
			}
			if(ex.GetType()!=typeof(Exception)) {
				text=ex.GetType().Name+": "+text;
			}
			return text;
		}
		#endregion Writing

		#region Context
		///<summary>The document and request lines written on this logical call path belong to, or null.</summary>
		public static PearlLogContext ContextCur {
			get {
				return _asyncLocalContext.Value;
			}
		}

		///<summary>Tags the lines written until the returned object is disposed with docNum (0 for none) and requestId (null or "" for none), including
		///lines from the HTTP handler. Restores the previous scope on dispose.</summary>
		public static IDisposable BeginScope(long docNum,string requestId) {
			PearlLogContext pearlLogContextPrevious=_asyncLocalContext.Value;
			_asyncLocalContext.Value=new PearlLogContext(docNum,requestId);
			return new ScopeRestorer(pearlLogContextPrevious);
		}

		private sealed class ScopeRestorer:IDisposable {
			private readonly PearlLogContext _pearlLogContextPrevious;
			private bool _isDisposed;

			public ScopeRestorer(PearlLogContext pearlLogContextPrevious) {
				_pearlLogContextPrevious=pearlLogContextPrevious;
			}

			public void Dispose() {
				if(_isDisposed) {
					return;
				}
				_isDisposed=true;
				_asyncLocalContext.Value=_pearlLogContextPrevious;
			}
		}
		#endregion Context

		#region Scrubbing
		private const string REDACTED="[redacted]";
		///<summary>Text is cut to this length before it is scrubbed (a large HTML error page, say), then to MAX_LINE_LENGTH after.</summary>
		private const int MAX_INPUT_LENGTH=MAX_LINE_LENGTH*20;
		private static readonly Regex _regexXmlSecret=new Regex(
			@"(?is)<(Signature|SignatureProvided|StringToSign|StringToSignBytes|CanonicalRequest|CanonicalRequestBytes|AWSAccessKeyId|Policy|Credential|X-Amz-[A-Za-z-]+)>.*?</\1>",
			RegexOptions.Compiled);
		private static readonly Regex _regexUrl=new Regex(@"(?i)\bhttps?://[^\s""'<>\\]+",RegexOptions.Compiled);
		private static readonly Regex _regexAuthScheme=new Regex(@"(?i)\b(Bearer|Basic)\s+[A-Za-z0-9\-._~+/]+=*",RegexOptions.Compiled);
		private static readonly Regex _regexJwt=new Regex(@"\beyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+(\.[A-Za-z0-9_\-]*)?",RegexOptions.Compiled);
		///<summary>A secret-looking key followed by : or = and its value, in JSON (also JSON escaped inside another string), form or header text.</summary>
		private static readonly Regex _regexKeyValue=new Regex(
			@"(?i)([\\""']*\b(?:client[_ ]?secret|access[_ ]?token|refresh[_ ]?token|id[_ ]?token|auth[_ ]?token|token|secret|password|signature|policy|credential|authorization|x-amz-[a-z0-9\-]+|awsaccesskeyid)\b[\\""']*\s*[:=]\s*)"
			+@"(\\?""(?:[^""\\]|\\(?!"")[\s\S])*\\?""|'[^']*'|[^\s&,;""'<>\\]+)",
			RegexOptions.Compiled);
		///<summary>Long opaque runs (keys, tokens, base64 data). GUIDs, paths and words are shorter or broken up by other characters.</summary>
		private static readonly Regex _regexLongOpaque=new Regex(@"[A-Za-z0-9+_\-]{40,}={0,2}",RegexOptions.Compiled);
		private static readonly Regex _regexLineBreaks=new Regex(@"\r\n|\r|\n",RegexOptions.Compiled);

		///<summary>Returns text safe for the local log, on one line: the configured client secret, Bearer/Basic credentials, JWTs, secret-looking
		///key/value pairs, S3 signature elements and long opaque strings are replaced with [redacted]; URLs lose their query string, and storage
		///(amazonaws.com) URLs, which are presigned, are replaced entirely. Never throws.</summary>
		public static string Scrub(string text) {
			if(string.IsNullOrEmpty(text)) {
				return "";
			}
			try {
				string result=text;
				if(result.Length>MAX_INPUT_LENGTH) {
					//Far beyond what is kept, so the cut can't leave part of a secret in the line.
					result=result.Substring(0,MAX_INPUT_LENGTH);
				}
				foreach(string secret in GetKnownSecrets()) {
					result=result.Replace(secret,REDACTED);
				}
				result=_regexXmlSecret.Replace(result,"<$1>"+REDACTED+"</$1>");
				result=_regexUrl.Replace(result,match => ScrubUrl(match.Value));
				result=_regexAuthScheme.Replace(result,"$1 "+REDACTED);
				result=_regexJwt.Replace(result,REDACTED);
				result=_regexKeyValue.Replace(result,"$1"+REDACTED);
				result=_regexLongOpaque.Replace(result,REDACTED);
				result=_regexLineBreaks.Replace(result," | ").Replace('\t',' ');
				if(result.Length>MAX_LINE_LENGTH) {
					result=result.Substring(0,MAX_LINE_LENGTH)+"...";
				}
				return result;
			}
			catch(Exception ex) {
				ex.DoNothing();
				return "[unloggable text]";
			}
		}

		///<summary>scheme://host/path without the query string or fragment. Presigned storage URLs (any amazonaws.com host) are not kept at all.</summary>
		public static string ScrubUrl(string url) {
			Uri uri;
			if(!Uri.TryCreate(url,UriKind.Absolute,out uri)) {
				return "[URL]";
			}
			string host=uri.Host.ToLowerInvariant();
			if(host=="amazonaws.com" || host.EndsWith(".amazonaws.com")) {
				return "[presigned storage URL]";
			}
			//Authority is host and port only, so a user name or password in the address is dropped too.
			string kept=uri.Scheme+"://"+uri.Authority+uri.AbsolutePath;
			if(uri.UserInfo!="" || uri.Query!="" || uri.Fragment!="") {
				kept+="?"+REDACTED;
			}
			return kept;
		}

		///<summary>The client secret as stored (the 24.3 bridge keeps it as typed) and, if it was saved encrypted (osd1$), its plain text. Values shorter
		///than 4 characters are skipped, so a near-empty value cannot blank out ordinary text.</summary>
		private static List<string> GetKnownSecrets() {
			List<string> listSecrets=new List<string>();
			string stored;
			try {
				stored=FuncGetPropVal(Bridges.Pearl.PEARL_CLIENT_SECRET_PROPERTY)??"";
			}
			catch(Exception ex) {
				ex.DoNothing();
				return listSecrets;
			}
			listSecrets.Add(stored);
			listSecrets.Add(SecretValue.ReadOrBlank(stored,LegacyPolicy.AcceptPlaintext));
			return listSecrets.Where(x => x!=null && x.Trim().Length>=4).Distinct().OrderByDescending(x => x.Length).ToList();
		}
		#endregion Scrubbing

		#region HTTP
		///<summary>A handler for the Pearl bridge's HttpClients that logs each call (see PearlLoggingHandler). isPresignedUpload is true for the client
		///that uploads images to the presigned storage URL, whose address is never logged.</summary>
		public static HttpMessageHandler CreateHttpHandler(bool isPresignedUpload) {
			PearlLoggingHandler pearlLoggingHandler=new PearlLoggingHandler(isPresignedUpload);
			pearlLoggingHandler.InnerHandler=new HttpClientHandler();
			return pearlLoggingHandler;
		}
		#endregion HTTP
	}

	///<summary>The document and Pearl request a log line belongs to.</summary>
	public sealed class PearlLogContext {
		///<summary>FK to document. 0 when not known.</summary>
		public long DocNum { get; }
		///<summary>Our request GUID sent to Pearl. "" when not known yet.</summary>
		public string RequestId { get; }

		public PearlLogContext(long docNum,string requestId) {
			DocNum=docNum;
			RequestId=requestId??"";
		}

		public bool IsEmpty {
			get {
				return DocNum<=0 && RequestId=="";
			}
		}

		public override string ToString() {
			List<string> listParts=new List<string>();
			if(DocNum>0) {
				listParts.Add("document "+DocNum.ToString(CultureInfo.InvariantCulture));
			}
			if(RequestId!="") {
				listParts.Add("request "+RequestId);
			}
			return string.Join(", ",listParts);
		}
	}
}
