// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using ImagingDeviceManager.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>Tests for LocalCaptureConfig, the capture.ini reader and writer. Every test works in its own temporary folder through
	///the explicit-path factories; none touches ProgramData or the default LOCALAPPDATA path. Most use a stand-in for the Users
	///Modify rule; the "Real ACL" tests change the ACL only of folders and files they create in their temporary folder.</summary>
	[TestClass]
	public class LocalCaptureConfigTests {
		private string _tempDir;

		[TestInitialize]
		public void Setup() {
			_tempDir=Path.Combine(Path.GetTempPath(),"LocalCaptureConfigTests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_tempDir);
		}

		[TestCleanup]
		public void Cleanup() {
			try {
				foreach(string path in Directory.GetFiles(_tempDir,"*",SearchOption.AllDirectories)) {
					File.SetAttributes(path,FileAttributes.Normal);
				}
				Directory.Delete(_tempDir,true);
			}
			catch {
			}
		}

		#region Helpers
		private string PathIn(params string[] arrayParts) {
			return Path.Combine(new[] { _tempDir }.Concat(arrayParts).ToArray());
		}

		///<summary>Joins lines with CRLF and a final CRLF, as Notepad writes them.</summary>
		private static string Lines(params string[] arrayLines) {
			return string.Join("\r\n",arrayLines)+"\r\n";
		}

		///<summary>Writes UTF-8 without a byte order mark.</summary>
		private static void WriteText(string path,string text) {
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllBytes(path,new UTF8Encoding(false).GetBytes(text));
		}

		private static string ReadText(string path) {
			return new UTF8Encoding(false).GetString(File.ReadAllBytes(path));
		}

		private LocalCaptureConfig LoadText(string text) {
			string path=PathIn("capture.ini");
			WriteText(path,text);
			return LocalCaptureConfig.Load(path);
		}

		private static void AssertTwainDefaults(LocalCaptureConfig config) {
			Assert.IsTrue(config.ThreadModal);
			Assert.AreEqual(0,config.AcquireTimeoutMinutes);
			Assert.IsFalse(config.Allow16Bit);
			Assert.IsFalse(config.RestrictSources);
			Assert.AreEqual(0,config.AllowedSources.Count);
			Assert.AreEqual(200,config.JournalKeep);
			Assert.AreEqual(0,config.DisableIsSafeSources.Count);
			Assert.IsNull(config.SafeDisableCap);
			Assert.AreEqual(0,config.RefusedDisableCodes.Count);
		}

		///<summary>A file whose name is used as a folder in the path, so the folder can never be created.</summary>
		private string BlockedPath(string blockerName) {
			string pathBlocker=PathIn(blockerName);
			File.WriteAllText(pathBlocker,"not a folder");
			return Path.Combine(pathBlocker,"capture.ini");
		}

		///<summary>A stand-in for the Users Modify rule. A path has the rule once Grant succeeded on it or on a folder above it, as
		///inheritance would give it.</summary>
		private sealed class FakeUsersAcl {
			///<summary>Every path Grant was called with, in order.</summary>
			public readonly List<string> ListGrantCalls=new List<string>();
			private readonly HashSet<string> _hashSetGranted=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			///<summary>The result of Grant for a path: "" grants, anything else is the error. Grants everything by default.</summary>
			public Func<string,string> FuncGrantResult=path => "";

			public string Grant(string path) {
				ListGrantCalls.Add(path);
				string error=FuncGrantResult(path);
				if(error=="") {
					_hashSetGranted.Add(path);
				}
				return error;
			}

			public bool Has(string path) {
				for(string pathCur=path;!string.IsNullOrEmpty(pathCur);pathCur=Path.GetDirectoryName(pathCur)) {
					if(_hashSetGranted.Contains(pathCur)) {
						return true;
					}
				}
				return false;
			}

			public LocalCaptureConfig Load(string path,string pathFallback) {
				return LocalCaptureConfig.Load(path,pathFallback,Grant,Has);
			}
		}

		private static readonly SecurityIdentifier _sidUsers=new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null);

		///<summary>The Allow rules for the local Users group on a folder or file.</summary>
		private static List<FileSystemAccessRule> GetUsersAllowRules(string path,bool includeExplicit,bool includeInherited) {
			FileSystemSecurity fileSystemSecurity=Directory.Exists(path)
				? (FileSystemSecurity)new DirectoryInfo(path).GetAccessControl(AccessControlSections.Access)
				: new FileInfo(path).GetAccessControl(AccessControlSections.Access);
			return fileSystemSecurity.GetAccessRules(includeExplicit,includeInherited,typeof(SecurityIdentifier))
				.Cast<FileSystemAccessRule>()
				.Where(x => x.AccessControlType==AccessControlType.Allow && _sidUsers.Equals(x.IdentityReference))
				.ToList();
		}

		private static bool HasModify(FileSystemAccessRule fileSystemAccessRule) {
			return (fileSystemAccessRule.FileSystemRights & FileSystemRights.Modify)==FileSystemRights.Modify;
		}
		#endregion Helpers

		#region Loading and defaults
		[TestMethod]
		public void Load_MissingFile_ReturnsDefaultsAndCreatesNothing() {
			string path=PathIn("sub","capture.ini");
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.AreEqual("",config.LastError);
			Assert.IsNull(config.PathLoaded);
			Assert.AreEqual(path,config.PathPrimary);
			Assert.IsNull(config.PathFallback);
			AssertTwainDefaults(config);
			Assert.AreEqual(0,config.GetDeviceNums().Count);
			Assert.AreEqual(0,config.GetSectionNames().Count);
			Assert.IsFalse(config.HasUnsavedChanges);
			Assert.IsFalse(Directory.Exists(PathIn("sub")));
		}

		[TestMethod]
		public void Load_EmptyFile_ReturnsDefaults() {
			LocalCaptureConfig config=LoadText("");
			Assert.AreEqual("",config.LastError);
			Assert.AreEqual(PathIn("capture.ini"),config.PathLoaded);
			AssertTwainDefaults(config);
		}

		[TestMethod]
		public void Load_DesignSample_ReadsEveryTwainKey() {
			LocalCaptureConfig config=LoadText(Lines(
				"[General]",
				"Version=1",
				"",
				"[Twain]",
				"ThreadModal=0             ; gated",
				"AcquireTimeoutMinutes=25  ; gated. 0 = none",
				"Allow16Bit=1              ; gated. Global 16-bit kill switch",
				"RestrictSources=1         ; gated. 1 = only AllowedSources can be opened",
				"AllowedSources=Flatbed One|Sensor Two ; gated. ProductNames separated by |",
				"JournalKeep=50            ; gated",
				"DisableIsSafeSources=Sensor Two ; gated",
				"SafeDisableCap=0x8105     ; gated",
				"RefusedDisableCodes=0x8101|8102 ; gated",
				"",
				"[Device.12]               ; imagingdevice.ImagingDeviceNum",
				"SessionMode=PerSlot       ; gated. PerSlot | Continuous",
				"Bits=8                    ; gated. 8 | 16"));
			Assert.AreEqual("",config.LastError);
			Assert.IsFalse(config.ThreadModal);
			Assert.AreEqual(25,config.AcquireTimeoutMinutes);
			Assert.IsTrue(config.Allow16Bit);
			Assert.IsTrue(config.RestrictSources);
			CollectionAssert.AreEqual(new[] { "Flatbed One","Sensor Two" },config.AllowedSources);
			Assert.AreEqual(50,config.JournalKeep);
			CollectionAssert.AreEqual(new[] { "Sensor Two" },config.DisableIsSafeSources);
			Assert.AreEqual(0x8105,config.SafeDisableCap);
			CollectionAssert.AreEqual(new[] { 0x8101,0x8102 },config.RefusedDisableCodes);
			Assert.AreEqual("1",config.GetString("General","Version"));
			Assert.AreEqual("PerSlot",config.GetString(LocalCaptureConfig.GetDeviceSection(12),"SessionMode"));
			Assert.AreEqual(8,config.GetInt("Device.12","Bits",0));
			CollectionAssert.AreEqual(new long[] { 12 },config.GetDeviceNums());
			CollectionAssert.AreEqual(new[] { "General","Twain","Device.12" },config.GetSectionNames());
		}

		[TestMethod]
		public void Load_BothFilesExist_ReadsPrimary() {
			string path=PathIn("machine","capture.ini");
			string pathFallback=PathIn("user","capture.ini");
			WriteText(path,Lines("[Twain]","JournalKeep=11"));
			WriteText(pathFallback,Lines("[Twain]","JournalKeep=22"));
			LocalCaptureConfig config=LocalCaptureConfig.Load(path,pathFallback);
			Assert.AreEqual(path,config.PathLoaded);
			Assert.AreEqual(11,config.JournalKeep);
		}

		[TestMethod]
		public void Load_OnlyFallbackExists_ReadsFallback() {
			string path=PathIn("machine","capture.ini");
			string pathFallback=PathIn("user","capture.ini");
			WriteText(pathFallback,Lines("[Twain]","JournalKeep=22"));
			LocalCaptureConfig config=LocalCaptureConfig.Load(path,pathFallback);
			Assert.AreEqual(pathFallback,config.PathLoaded);
			Assert.AreEqual(22,config.JournalKeep);
		}

		[TestMethod]
		public void Load_PathIsAFolder_ReturnsDefaults() {
			string path=PathIn("capture.ini");
			Directory.CreateDirectory(path);
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.IsNull(config.PathLoaded);
			AssertTwainDefaults(config);
		}

		[TestMethod]
		public void Load_NullOrBlankPaths_NeverThrow() {
			LocalCaptureConfig config=LocalCaptureConfig.Load(null);
			AssertTwainDefaults(config);
			Assert.IsTrue(config.Set("Twain","JournalKeep","5"));
			Assert.IsFalse(config.Save());
			Assert.AreNotEqual("",config.LastError);
			config=LocalCaptureConfig.Load("  ","");
			Assert.IsNull(config.PathPrimary);
			Assert.IsNull(config.PathFallback);
			config=LocalCaptureConfig.Load("bad|path<>?\0.ini");
			AssertTwainDefaults(config);
			Assert.IsTrue(config.Set("Twain","JournalKeep","5"));
			Assert.IsFalse(config.Save());
			Assert.AreNotEqual("",config.LastError);
		}

		[TestMethod]
		public void DefaultPaths_AreUnderProgramDataAndLocalAppData() {
			string expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"OpenPMS-Dental","Imaging","capture.ini");
			Assert.AreEqual(expected,LocalCaptureConfig.DefaultPath);
			string expectedFallback=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenPMS-Dental","Imaging","capture.ini");
			Assert.AreEqual(expectedFallback,LocalCaptureConfig.DefaultFallbackPath);
		}
		#endregion Loading and defaults

		#region Parsing
		[TestMethod]
		public void Parse_CommentsBlankLinesAndWhitespace_AreIgnored() {
			LocalCaptureConfig config=LoadText(Lines(
				"; whole-line comment",
				"   ; indented comment, with = sign",
				"",
				"  [ Twain ]   ; header comment",
				"\tThreadModal   =   0   ; trailing comment",
				"JournalKeep=50;no space before the comment",
				"AllowedSources = Scanner A ;comment | not an entry",
				"not a key line",
				"=no key",
				"   "));
			Assert.AreEqual("",config.LastError);
			Assert.IsFalse(config.ThreadModal);
			Assert.AreEqual(50,config.JournalKeep);
			CollectionAssert.AreEqual(new[] { "Scanner A" },config.AllowedSources);
			Assert.IsFalse(config.ContainsKey("Twain","not a key line"));
			Assert.IsFalse(config.ContainsKey("Twain",""));
			CollectionAssert.AreEqual(new[] { "Twain" },config.GetSectionNames());
		}

		[TestMethod]
		public void Parse_SectionAndKeyNames_AreCaseInsensitive() {
			LocalCaptureConfig config=LoadText(Lines("[TWAIN]","threadmodal=no","ALLOW16BIT=Yes"));
			Assert.IsFalse(config.ThreadModal);
			Assert.IsTrue(config.Allow16Bit);
			Assert.AreEqual("no",config.GetString("twain","ThreadModal"));
			Assert.AreEqual("no",config.GetString(" Twain ","  THREADMODAL "));
		}

		[TestMethod]
		public void Parse_TurkishCulture_NamesAndNumbersStillMatch() {
			CultureInfo cultureOld=Thread.CurrentThread.CurrentCulture;
			try {
				//In Turkish, "I".ToLower() is a dotless i, so a culture-sensitive comparison would not match "Twain" with "TWAIN".
				Thread.CurrentThread.CurrentCulture=new CultureInfo("tr-TR");
				LocalCaptureConfig config=LoadText(Lines("[TWAIN]","RESTRICTSOURCES=YES","JOURNALKEEP=-1","THREADMODAL=FALSE","ACQUIRETIMEOUTMINUTES=15",
					"ALLOW16BIT=TRUE","SAFEDISABLECAP=0XA1B2"));
				Assert.IsTrue(config.RestrictSources);
				Assert.IsFalse(config.ThreadModal);
				Assert.AreEqual(200,config.JournalKeep);
				Assert.AreEqual(15,config.AcquireTimeoutMinutes);
				Assert.IsTrue(config.Allow16Bit);
				Assert.AreEqual(0xA1B2,config.SafeDisableCap);
				//German uses '.' as the thousands separator; it must not be accepted.
				Thread.CurrentThread.CurrentCulture=new CultureInfo("de-DE");
				config=LoadText(Lines("[Twain]","JournalKeep=1.000","AcquireTimeoutMinutes=1,5"));
				Assert.AreEqual(200,config.JournalKeep);
				Assert.AreEqual(0,config.AcquireTimeoutMinutes);
				Assert.AreEqual("Device.12",LocalCaptureConfig.GetDeviceSection(12));
			}
			finally {
				Thread.CurrentThread.CurrentCulture=cultureOld;
			}
		}

		[TestMethod]
		public void Parse_DuplicateKeysAndSections_LastWins() {
			LocalCaptureConfig config=LoadText(Lines(
				"[Twain]",
				"JournalKeep=10",
				"JournalKeep=20",
				"[Device.5]",
				"Bits=8",
				"[Twain]",
				"JournalKeep=30",
				"ThreadModal=0",
				"[twain]",
				"ThreadModal=1"));
			Assert.AreEqual(30,config.JournalKeep);
			Assert.IsTrue(config.ThreadModal);
			CollectionAssert.AreEqual(new long[] { 5 },config.GetDeviceNums());
			CollectionAssert.AreEqual(new[] { "Twain","Device.5" },config.GetSectionNames());
		}

		[TestMethod]
		public void Parse_UnclosedHeader_KeysDoNotCountForTheSectionAbove() {
			LocalCaptureConfig config=LoadText(Lines("[Twain]","JournalKeep=10","[Device.5","JournalKeep=99","Bits=16"));
			Assert.AreEqual(10,config.JournalKeep);
			Assert.AreEqual(0,config.GetDeviceNums().Count);
			Assert.AreEqual("",config.GetString("Device.5","Bits"));
		}

		[TestMethod]
		public void Parse_KeysBeforeFirstSection_AreInSectionEmpty() {
			LocalCaptureConfig config=LoadText(Lines("Version=1","[Twain]","JournalKeep=3"));
			Assert.AreEqual("1",config.GetString("","Version"));
			Assert.IsFalse(config.ContainsKey("Twain","Version"));
			Assert.AreEqual(3,config.JournalKeep);
		}

		[TestMethod]
		public void GetString_MissingOrEmpty() {
			LocalCaptureConfig config=LoadText(Lines("[Camera]","Name=","Moniker=  @device:pnp  "));
			Assert.AreEqual("",config.GetString("Camera","Name","default"));
			Assert.IsTrue(config.ContainsKey("Camera","Name"));
			Assert.AreEqual("default",config.GetString("Camera","Width","default"));
			Assert.AreEqual("default",config.GetString("Nowhere","Name","default"));
			Assert.AreEqual("",config.GetString("Camera","Width"));
			Assert.AreEqual("@device:pnp",config.GetString("Camera","Moniker"));
		}

		[TestMethod]
		public void GetBool_AcceptedSpellings() {
			LocalCaptureConfig config=LoadText(Lines("[S]","a=1","b=true","c=TRUE","d=Yes","e=0","f=false","g=No","h=2","i=on","j=","k=maybe"));
			foreach(string key in new[] { "a","b","c","d" }) {
				Assert.IsTrue(config.GetBool("S",key,false),key);
			}
			foreach(string key in new[] { "e","f","g" }) {
				Assert.IsFalse(config.GetBool("S",key,true),key);
			}
			foreach(string key in new[] { "h","i","j","k","missing" }) {
				Assert.IsTrue(config.GetBool("S",key,true),key);
				Assert.IsFalse(config.GetBool("S",key,false),key);
			}
		}

		[TestMethod]
		public void GetInt_DecimalHexPrefixAndInvalid() {
			LocalCaptureConfig config=LoadText(Lines("[S]","a=42","b=-7","c=0x10","d= 12 ","e=1,000","f=1.5","g=","h=abc","i=99999999999","j=+3","k=8002"));
			Assert.AreEqual(42,config.GetInt("S","a",-1));
			Assert.AreEqual(-7,config.GetInt("S","b",-1));
			Assert.AreEqual(16,config.GetInt("S","c",-1));
			Assert.AreEqual(12,config.GetInt("S","d",-1));
			Assert.AreEqual(-1,config.GetInt("S","e",-1));
			Assert.AreEqual(-1,config.GetInt("S","f",-1));
			Assert.AreEqual(-1,config.GetInt("S","g",-1));
			Assert.AreEqual(-1,config.GetInt("S","h",-1));
			Assert.AreEqual(-1,config.GetInt("S","i",-1));
			Assert.AreEqual(3,config.GetInt("S","j",-1));
			Assert.AreEqual(8002,config.GetInt("S","k",-1),"plain digits are decimal for GetInt");
			Assert.AreEqual(-1,config.GetInt("S","missing",-1));
		}

		[TestMethod]
		public void GetHex_WithAndWithoutPrefix() {
			LocalCaptureConfig config=LoadText(Lines("[S]","a=0x8002","b=8002","c=0X80ff","d=  8003  ","e=0x","f=xyz","g=-1","h=FFFFFFFF","i=","j=7FFFFFFF",
				"[Twain]","SafeDisableCap=8002"));
			Assert.AreEqual(0x8002,config.GetHex("S","a"));
			Assert.AreEqual(0x8002,config.GetHex("S","b"));
			Assert.AreEqual(0x80FF,config.GetHex("S","c"));
			Assert.AreEqual(0x8003,config.GetHex("S","d"));
			Assert.IsNull(config.GetHex("S","e"));
			Assert.IsNull(config.GetHex("S","f"));
			Assert.IsNull(config.GetHex("S","g"));
			Assert.IsNull(config.GetHex("S","h"));
			Assert.IsNull(config.GetHex("S","i"));
			Assert.AreEqual(int.MaxValue,config.GetHex("S","j"));
			Assert.IsNull(config.GetHex("S","missing"));
			Assert.AreEqual(5,config.GetHex("S","f",5));
			Assert.AreEqual(0x8002,config.GetHex("S","b",5));
			Assert.AreEqual(0x8002,config.SafeDisableCap);
		}

		[TestMethod]
		public void GetList_TrimsAndDropsEmptyEntries() {
			LocalCaptureConfig config=LoadText(Lines(
				"[Twain]",
				"AllowedSources= Scanner A | Sensor B ||  | Scanner A ",
				"DisableIsSafeSources=",
				"RefusedDisableCodes=0x8001 | 8002 || nope | 0x |80ff"));
			CollectionAssert.AreEqual(new[] { "Scanner A","Sensor B","Scanner A" },config.AllowedSources);
			Assert.AreEqual(0,config.DisableIsSafeSources.Count);
			CollectionAssert.AreEqual(new[] { 0x8001,0x8002,0x80FF },config.RefusedDisableCodes);
			Assert.AreEqual(0,config.GetList("Twain","missing").Count);
			Assert.AreEqual(0,config.GetHexList("Nowhere","missing").Count);
		}

		[TestMethod]
		public void GetDeviceNums_ListsValidDeviceSectionsOnly() {
			LocalCaptureConfig config=LoadText(Lines(
				"[Device.12]","Bits=16",
				"[device.3]","Bits=8",
				"[Device.x]",
				"[Device.]",
				"[Device.-1]",
				"[Device.0]",
				"[Device.012]",
				"[Device.12]",
				"[DeviceX.1]",
				"[ Device.4 ]",
				"[Device.9999999999]",
				"[Device.1.2]"));
			CollectionAssert.AreEqual(new long[] { 3,4,12,9999999999 },config.GetDeviceNums());
			Assert.AreEqual(16,config.GetInt(LocalCaptureConfig.GetDeviceSection(12),"Bits",0));
			Assert.AreEqual(8,config.GetInt(LocalCaptureConfig.GetDeviceSection(3),"Bits",0));
		}

		[TestMethod]
		public void TwainAccessors_OutOfRangeValues() {
			LocalCaptureConfig config=LoadText(Lines("[Twain]","AcquireTimeoutMinutes=-5","JournalKeep=-1","ThreadModal=2","SafeDisableCap=nope"));
			Assert.AreEqual(0,config.AcquireTimeoutMinutes);
			Assert.AreEqual(200,config.JournalKeep);
			Assert.IsTrue(config.ThreadModal);
			Assert.IsNull(config.SafeDisableCap);
			config=LoadText(Lines("[Twain]","JournalKeep=0","JournalKeep=abc"));
			Assert.AreEqual(200,config.JournalKeep);
			config=LoadText(Lines("[Twain]","JournalKeep=0"));
			Assert.AreEqual(0,config.JournalKeep);
		}

		[TestMethod]
		public void Getters_NullArguments_ReturnDefaults() {
			LocalCaptureConfig config=LoadText(Lines("[Twain]","JournalKeep=3"));
			Assert.AreEqual("d",config.GetString(null,"JournalKeep","d"));
			Assert.AreEqual(4,config.GetInt("Twain",null,4));
			Assert.IsTrue(config.GetBool(null,null,true));
			Assert.IsNull(config.GetHex(null,"x"));
			Assert.AreEqual(0,config.GetList(null,null).Count);
			Assert.AreEqual(0,config.GetHexList(null,null).Count);
			Assert.IsFalse(config.ContainsKey(null,null));
		}
		#endregion Parsing

		#region Encodings and corrupt files
		[TestMethod]
		public void Load_Utf8Bom_ParsesAndSaveKeepsTheBom() {
			string path=PathIn("capture.ini");
			byte[] bytes=new byte[] { 0xEF,0xBB,0xBF }.Concat(Encoding.ASCII.GetBytes(Lines("[Twain]","JournalKeep=7"))).ToArray();
			File.WriteAllBytes(path,bytes);
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.AreEqual("",config.LastError);
			Assert.AreEqual(7,config.JournalKeep);
			CollectionAssert.AreEqual(new[] { "Twain" },config.GetSectionNames());
			Assert.IsTrue(config.Set("Twain","JournalKeep","8"));
			Assert.IsTrue(config.Save(),config.LastError);
			byte[] bytesSaved=File.ReadAllBytes(path);
			CollectionAssert.AreEqual(new byte[] { 0xEF,0xBB,0xBF },bytesSaved.Take(3).ToArray());
			Assert.AreEqual(Lines("[Twain]","JournalKeep=8"),Encoding.ASCII.GetString(bytesSaved,3,bytesSaved.Length-3));
		}

		[TestMethod]
		public void Load_Utf16LeBom_ParsesAndSaveKeepsTheEncoding() {
			string path=PathIn("capture.ini");
			byte[] bytes=Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(Lines("[Twain]","AllowedSources=Café Scanner","JournalKeep=7"))).ToArray();
			File.WriteAllBytes(path,bytes);
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.AreEqual("",config.LastError);
			Assert.AreEqual(7,config.JournalKeep);
			CollectionAssert.AreEqual(new[] { "Café Scanner" },config.AllowedSources);
			Assert.IsTrue(config.Set("Twain","JournalKeep","9"));
			Assert.IsTrue(config.Save(),config.LastError);
			byte[] bytesSaved=File.ReadAllBytes(path);
			CollectionAssert.AreEqual(new byte[] { 0xFF,0xFE },bytesSaved.Take(2).ToArray());
			Assert.AreEqual(Lines("[Twain]","AllowedSources=Café Scanner","JournalKeep=9"),Encoding.Unicode.GetString(bytesSaved,2,bytesSaved.Length-2));
		}

		[TestMethod]
		public void Load_InvalidUtf8_ReadsAsWindows1252AndKeepsUntouchedBytes() {
			string path=PathIn("capture.ini");
			Encoding encoding1252=Encoding.GetEncoding(1252);
			byte[] bytes=encoding1252.GetBytes(Lines("; Café notes","[Twain]","AllowedSources=Café Scanner","JournalKeep=5"));
			Assert.IsTrue(bytes.Contains((byte)0xE9));
			File.WriteAllBytes(path,bytes);
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.AreEqual("",config.LastError);
			CollectionAssert.AreEqual(new[] { "Café Scanner" },config.AllowedSources);
			Assert.IsTrue(config.Set("Twain","JournalKeep","6"));
			Assert.IsTrue(config.Save(),config.LastError);
			CollectionAssert.AreEqual(encoding1252.GetBytes(Lines("; Café notes","[Twain]","AllowedSources=Café Scanner","JournalKeep=6")),File.ReadAllBytes(path));
		}

		[TestMethod]
		public void Save_Windows1252FileWithCharactersItLacks_SwitchesToUtf8() {
			string path=PathIn("capture.ini");
			File.WriteAllBytes(path,Encoding.GetEncoding(1252).GetBytes(Lines("; Café","[Twain]","JournalKeep=5")));
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.IsTrue(config.Set("Twain","AllowedSources","传感器"));
			Assert.IsTrue(config.Save(),config.LastError);
			string text=new UTF8Encoding(false,true).GetString(File.ReadAllBytes(path));
			Assert.AreEqual(Lines("; Café","[Twain]","JournalKeep=5","AllowedSources=传感器"),text);
			CollectionAssert.AreEqual(new[] { "传感器" },LocalCaptureConfig.Load(path).AllowedSources);
		}

		[TestMethod]
		public void Load_CorruptBytes_UsesDefaultsAndSaveKeepsACopy() {
			string path=PathIn("capture.ini");
			byte[] bytesCorrupt=new byte[512];
			new Random(1234).NextBytes(bytesCorrupt);
			bytesCorrupt[0]=(byte)'A';//No byte order mark by chance.
			bytesCorrupt[10]=0;//Text editors never write NUL.
			Array.Copy(Encoding.ASCII.GetBytes("[Twain]\r\nJournalKeep=1\r\n"),0,bytesCorrupt,100,24);
			File.WriteAllBytes(path,bytesCorrupt);
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.AreEqual(path,config.PathLoaded);
			StringAssert.Contains(config.LastError,path);
			AssertTwainDefaults(config);
			Assert.AreEqual(0,config.GetSectionNames().Count);
			Assert.IsTrue(config.Set("Twain","JournalKeep","40"));
			Assert.AreEqual("",config.LastError,"Set clears the load error");
			Assert.IsTrue(config.Save(),config.LastError);
			//The save succeeded but whatever was only in the corrupt file is gone from the live file, so it warns.
			StringAssert.Contains(config.LastError,path+".corrupt");
			StringAssert.Contains(config.LastError,"[Twain]");
			Assert.AreEqual(path,config.PathSaved);
			CollectionAssert.AreEqual(bytesCorrupt,File.ReadAllBytes(path+".corrupt"));
			Assert.AreEqual(Lines("[Twain]","JournalKeep=40"),ReadText(path));
			LocalCaptureConfig configLoaded=LocalCaptureConfig.Load(path);
			Assert.AreEqual("",configLoaded.LastError);
			Assert.AreEqual(40,configLoaded.JournalKeep);
			//The next save of a readable file has nothing to warn about.
			Assert.IsTrue(configLoaded.Set("Twain","JournalKeep","41"));
			Assert.IsTrue(configLoaded.Save());
			Assert.AreEqual("",configLoaded.LastError);
		}

		[TestMethod]
		public void Load_InvalidUtf8AfterUtf8Bom_IsCorrupt() {
			string path=PathIn("capture.ini");
			File.WriteAllBytes(path,new byte[] { 0xEF,0xBB,0xBF,(byte)'[',(byte)'T',(byte)']',0xC3,0x28,0x0D,0x0A });
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.AreNotEqual("",config.LastError);
			Assert.AreEqual(0,config.GetSectionNames().Count);
		}

		[TestMethod]
		public void Load_FileTooLarge_IsCorrupt() {
			string path=PathIn("capture.ini");
			StringBuilder stringBuilder=new StringBuilder("[Twain]\r\nJournalKeep=1\r\n");
			while(stringBuilder.Length<=1024*1024) {
				stringBuilder.Append("; padding padding padding padding padding padding padding padding\r\n");
			}
			WriteText(path,stringBuilder.ToString());
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.AreNotEqual("",config.LastError);
			Assert.AreEqual(200,config.JournalKeep);
		}

		[TestMethod]
		public void Load_LockedFile_UsesDefaultsAndSaveDoesNotOverwriteIt() {
			string path=PathIn("machine","capture.ini");
			string pathFallback=PathIn("user","capture.ini");
			string text=Lines("[Twain]","JournalKeep=10");
			WriteText(path,text);
			using(new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
				LocalCaptureConfig config=LocalCaptureConfig.Load(path,pathFallback);
				Assert.AreEqual(path,config.PathLoaded);
				StringAssert.Contains(config.LastError,"Could not read");
				AssertTwainDefaults(config);
				Assert.IsTrue(config.Set("Twain","JournalKeep","20"));
				Assert.IsTrue(config.Save(),config.LastError);
				Assert.AreEqual(pathFallback,config.PathSaved);
				StringAssert.Contains(config.LastError,pathFallback);
			}
			Assert.AreEqual(text,ReadText(path));
			Assert.AreEqual(Lines("[Twain]","JournalKeep=20"),ReadText(pathFallback));
		}
		#endregion Encodings and corrupt files

		#region Set and Save
		[TestMethod]
		public void Save_PreservesCommentsAndChangesOnlyTheSetValues() {
			string path=PathIn("capture.ini");
			WriteText(path,Lines(
				"[General]",
				"Version=1",
				"",
				"[Twain]",
				"ThreadModal=1             ; gated",
				"AcquireTimeoutMinutes=0   ; gated. 0 = none",
				"AllowedSources=           ; gated. ProductNames separated by |",
				"JournalKeep=200           ; gated",
				"",
				"; Per-device settings",
				"[Device.12]               ; imagingdevice.ImagingDeviceNum",
				"Bits=8                    ; gated. 8 | 16"));
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.IsTrue(config.Set("Twain","ThreadModal","0"));
			Assert.IsTrue(config.Set("twain","AllowedSources","Scanner A|Sensor"));
			Assert.IsTrue(config.Set("Twain","RestrictSources","1"));
			Assert.IsTrue(config.Set(LocalCaptureConfig.GetDeviceSection(12),"Headless","0"));
			Assert.IsTrue(config.Set("Camera","Width","640"));
			Assert.IsTrue(config.HasUnsavedChanges);
			//Set takes effect in memory at once.
			Assert.IsFalse(config.ThreadModal);
			Assert.IsTrue(config.RestrictSources);
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual("",config.LastError);
			Assert.AreEqual(path,config.PathSaved);
			Assert.IsFalse(config.HasUnsavedChanges);
			Assert.AreEqual(Lines(
				"[General]",
				"Version=1",
				"",
				"[Twain]",
				"ThreadModal=0             ; gated",
				"AcquireTimeoutMinutes=0   ; gated. 0 = none",
				"AllowedSources=Scanner A|Sensor ; gated. ProductNames separated by |",
				"JournalKeep=200           ; gated",
				"RestrictSources=1",
				"",
				"; Per-device settings",
				"[Device.12]               ; imagingdevice.ImagingDeviceNum",
				"Bits=8                    ; gated. 8 | 16",
				"Headless=0",
				"",
				"[Camera]",
				"Width=640"),ReadText(path));
		}

		[TestMethod]
		public void Save_ValueLengthChanges_KeepTheCommentColumnWhenThereIsRoom() {
			string path=PathIn("capture.ini");
			//The comment starts in column 26 (0-based), as in the sample in the design.
			WriteText(path,Lines("[Twain]","JournalKeep=200"+new string(' ',11)+"; gated","AllowedSources=A ; short gap","RefusedDisableCodes=\t; tab"));
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.IsTrue(config.Set("Twain","JournalKeep","5"));
			Assert.IsTrue(config.Set("Twain","AllowedSources","A much longer name"));
			Assert.IsTrue(config.Set("Twain","RefusedDisableCodes","8101|8102"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual(Lines("[Twain]","JournalKeep=5"+new string(' ',13)+"; gated","AllowedSources=A much longer name ; short gap",
				"RefusedDisableCodes=8101|8102\t; tab"),ReadText(path));
			config=LocalCaptureConfig.Load(path);
			Assert.IsTrue(config.Set("Twain","JournalKeep",""));
			Assert.IsTrue(config.Save(),config.LastError);
			StringAssert.Contains(ReadText(path),"\r\nJournalKeep="+new string(' ',14)+"; gated\r\n");
			Assert.AreEqual(200,LocalCaptureConfig.Load(path).JournalKeep);
		}

		[TestMethod]
		public void Save_DuplicateKeys_RewritesTheOneThatTakesEffect() {
			string path=PathIn("capture.ini");
			WriteText(path,Lines("[Twain]","JournalKeep=10","[Device.1]","Bits=8","[Twain]","JournalKeep=20"));
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.IsTrue(config.Set("Twain","JournalKeep","30"));
			Assert.IsTrue(config.Set("Twain","ThreadModal","0"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual(Lines("[Twain]","JournalKeep=10","[Device.1]","Bits=8","[Twain]","JournalKeep=30","ThreadModal=0"),ReadText(path));
			Assert.AreEqual(30,LocalCaptureConfig.Load(path).JournalKeep);
		}

		[TestMethod]
		public void Save_KeyInSectionEmpty_GoesBeforeTheFirstHeader() {
			string path=PathIn("capture.ini");
			WriteText(path,Lines("Version=1","[Twain]","JournalKeep=3"));
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.IsTrue(config.Set("","Other","2"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual(Lines("Version=1","Other=2","[Twain]","JournalKeep=3"),ReadText(path));
		}

		[TestMethod]
		public void Save_LfOnlyFile_KeepsLf() {
			string path=PathIn("capture.ini");
			WriteText(path,"; unix\n[Twain]\nJournalKeep=3\n");
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.IsTrue(config.Set("Twain","JournalKeep","4"));
			Assert.IsTrue(config.Set("Twain","Allow16Bit","0"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual("; unix\n[Twain]\nJournalKeep=4\nAllow16Bit=0\n",ReadText(path));
		}

		[TestMethod]
		public void Save_RoundTrip_CreatesFolderAndFile() {
			string path=PathIn("a","b","capture.ini");
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.IsTrue(config.Set("Twain","ThreadModal","0"));
			Assert.IsTrue(config.Set("Twain","AcquireTimeoutMinutes","20"));
			Assert.IsTrue(config.Set("Twain","Allow16Bit","1"));
			Assert.IsTrue(config.Set("Twain","RestrictSources","yes"));
			Assert.IsTrue(config.Set("Twain","AllowedSources","Flatbed|Sensor"));
			Assert.IsTrue(config.Set("Twain","JournalKeep","75"));
			Assert.IsTrue(config.Set("Twain","DisableIsSafeSources","Sensor"));
			Assert.IsTrue(config.Set("Twain","SafeDisableCap","0x8105"));
			Assert.IsTrue(config.Set("Twain","RefusedDisableCodes","0x8101|0x8102"));
			Assert.IsTrue(config.Set(LocalCaptureConfig.GetDeviceSection(7),"Bits","16"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual(path,config.PathSaved);
			LocalCaptureConfig configLoaded=LocalCaptureConfig.Load(path);
			Assert.AreEqual(path,configLoaded.PathLoaded);
			Assert.IsFalse(configLoaded.ThreadModal);
			Assert.AreEqual(20,configLoaded.AcquireTimeoutMinutes);
			Assert.IsTrue(configLoaded.Allow16Bit);
			Assert.IsTrue(configLoaded.RestrictSources);
			CollectionAssert.AreEqual(new[] { "Flatbed","Sensor" },configLoaded.AllowedSources);
			Assert.AreEqual(75,configLoaded.JournalKeep);
			CollectionAssert.AreEqual(new[] { "Sensor" },configLoaded.DisableIsSafeSources);
			Assert.AreEqual(0x8105,configLoaded.SafeDisableCap);
			CollectionAssert.AreEqual(new[] { 0x8101,0x8102 },configLoaded.RefusedDisableCodes);
			CollectionAssert.AreEqual(new long[] { 7 },configLoaded.GetDeviceNums());
			Assert.AreEqual(16,configLoaded.GetInt("Device.7","Bits",8));
			//New files are UTF-8 without a byte order mark, with CRLF line ends.
			byte[] bytes=File.ReadAllBytes(path);
			Assert.AreEqual((byte)'[',bytes[0]);
			StringAssert.StartsWith(ReadText(path),"[Twain]\r\nThreadModal=0\r\n");
			Assert.AreEqual(0,Directory.GetFiles(PathIn("a","b"),"*.tmp").Length,"temporary file left behind");
		}

		[TestMethod]
		public void Save_NothingChanged_DoesNotWrite() {
			string path=PathIn("capture.ini");
			string text=Lines("[Twain]","JournalKeep=10   ; gated");
			WriteText(path,text);
			DateTime dateTimeOld=new DateTime(2020,1,2,3,4,5,DateTimeKind.Utc);
			File.SetLastWriteTimeUtc(path,dateTimeOld);
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.IsTrue(config.Save());
			Assert.IsNull(config.PathSaved);
			Assert.IsTrue(config.Set("TWAIN","journalkeep","10"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual(dateTimeOld,File.GetLastWriteTimeUtc(path));
			Assert.AreEqual(text,ReadText(path));
		}

		[TestMethod]
		public void Save_KeepsEditsMadeOnDiskAfterLoad() {
			string path=PathIn("capture.ini");
			WriteText(path,Lines("[Twain]","JournalKeep=10","ThreadModal=1"));
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.IsTrue(config.Set("Twain","JournalKeep","20"));
			//Someone else edits the file (another Open Dental, or an administrator in Notepad).
			WriteText(path,Lines("; edited by hand","[Twain]","JournalKeep=10","ThreadModal=0","[Camera]","Name=Cam"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual(Lines("; edited by hand","[Twain]","JournalKeep=20","ThreadModal=0","[Camera]","Name=Cam"),ReadText(path));
			//Memory now matches the file that was written.
			Assert.IsFalse(config.ThreadModal);
			Assert.AreEqual("Cam",config.GetString("Camera","Name"));
		}

		[TestMethod]
		public void Save_ExistingFile_KeepsItsAttributesAndReplacesInPlace() {
			string path=PathIn("capture.ini");
			WriteText(path,Lines("[Twain]","JournalKeep=10"));
			File.SetAttributes(path,FileAttributes.Archive|FileAttributes.Hidden);
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			Assert.IsTrue(config.Set("Twain","JournalKeep","11"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.IsTrue((File.GetAttributes(path) & FileAttributes.Hidden)!=0);
			Assert.AreEqual(11,LocalCaptureConfig.Load(path).JournalKeep);
		}

		[TestMethod]
		public void Set_InvalidNamesOrValues_ReturnFalseAndChangeNothing() {
			string path=PathIn("capture.ini");
			LocalCaptureConfig config=LocalCaptureConfig.Load(path);
			string[][] arrayInvalid=new[] {
				new[] { "Twain","AllowedSources","a;b" },
				new[] { "Twain","AllowedSources","a\nb" },
				new[] { "Twain","AllowedSources","a\rb" },
				//NUL would make the saved file read as corrupt. A string from native code can easily end in one.
				new[] { "Twain","AllowedSources","a\0b" },
				new[] { "Camera","Name","Cam\0" },
				new[] { "Twain","AllowedSources","a\u0001b" },
				new[] { "Twain","AllowedSources","a\u007Fb" },
				new[] { "Twain","AllowedSources","a\u0085b" },
				new[] { "Tw]ain","K","1" },
				new[] { "Tw;ain","K","1" },
				new[] { "Tw\nain","K","1" },
				new[] { "Tw\0ain","K","1" },
				new[] { "Twain","K\0","1" },
				new[] { "Twain","K\u001Bx","1" },
				new[] { "Twain","K=1","1" },
				new[] { "Twain","K;1","1" },
				new[] { "Twain","[K","1" },
				new[] { "Twain","","1" },
				new[] { "Twain","  ","1" },
				new[] { "Twain",null,"1" },
				new[] { null,"K","1" },
			};
			foreach(string[] arrayArgs in arrayInvalid) {
				Assert.IsFalse(config.Set(arrayArgs[0],arrayArgs[1],arrayArgs[2]),string.Join(",",arrayArgs));
				Assert.AreNotEqual("",config.LastError);
			}
			Assert.IsFalse(config.HasUnsavedChanges);
			Assert.AreEqual(0,config.GetSectionNames().Count);
			Assert.IsTrue(config.Set(" Twain "," JournalKeep ","  15  "));
			Assert.AreEqual("",config.LastError);
			Assert.AreEqual(15,config.JournalKeep);
			Assert.IsTrue(config.Set("Twain","AllowedSources",null));
			Assert.IsTrue(config.ContainsKey("Twain","AllowedSources"));
			//Tab is the one control character allowed inside a value.
			Assert.IsTrue(config.Set("Twain","DisableIsSafeSources","a\tb"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual(Lines("[Twain]","JournalKeep=15","AllowedSources=","DisableIsSafeSources=a\tb"),ReadText(path));
			LocalCaptureConfig configLoaded=LocalCaptureConfig.Load(path);
			Assert.AreEqual("",configLoaded.LastError);
			CollectionAssert.AreEqual(new[] { "a\tb" },configLoaded.DisableIsSafeSources);
		}
		#endregion Set and Save

		#region Fallback
		[TestMethod]
		public void Save_PrimaryFolderCannotBeCreated_SavesToFallback() {
			string path=BlockedPath("blocker");
			string pathFallback=PathIn("user","capture.ini");
			LocalCaptureConfig config=LocalCaptureConfig.Load(path,pathFallback);
			Assert.IsNull(config.PathLoaded);
			Assert.AreEqual("",config.LastError);
			Assert.IsTrue(config.Set("Twain","JournalKeep","33"));
			Assert.IsTrue(config.Save());
			Assert.AreEqual(pathFallback,config.PathSaved);
			StringAssert.Contains(config.LastError,path);
			StringAssert.Contains(config.LastError,pathFallback);
			Assert.AreEqual(Lines("[Twain]","JournalKeep=33"),ReadText(pathFallback));
			LocalCaptureConfig configLoaded=LocalCaptureConfig.Load(path,pathFallback);
			Assert.AreEqual(pathFallback,configLoaded.PathLoaded);
			Assert.AreEqual(33,configLoaded.JournalKeep);
		}

		[TestMethod]
		public void Save_PrimaryIsAFolder_SavesToFallback() {
			string path=PathIn("machine","capture.ini");
			Directory.CreateDirectory(path);
			string pathFallback=PathIn("user","capture.ini");
			LocalCaptureConfig config=LocalCaptureConfig.Load(path,pathFallback);
			Assert.IsTrue(config.Set("Twain","JournalKeep","34"));
			Assert.IsTrue(config.Save());
			Assert.AreEqual(pathFallback,config.PathSaved);
			Assert.AreNotEqual("",config.LastError);
			Assert.AreEqual(34,LocalCaptureConfig.Load(path,pathFallback).JournalKeep);
		}

		[TestMethod]
		public void Save_PrimaryReadOnly_SavesACopyToFallbackAndWarnsThatPrimaryWins() {
			string path=PathIn("machine","capture.ini");
			string pathFallback=PathIn("user","capture.ini");
			string text=Lines("; set by the administrator","[Twain]","JournalKeep=10","RestrictSources=1");
			WriteText(path,text);
			File.SetAttributes(path,FileAttributes.ReadOnly);
			LocalCaptureConfig config=LocalCaptureConfig.Load(path,pathFallback);
			Assert.AreEqual(10,config.JournalKeep);
			Assert.IsTrue(config.Set("Twain","JournalKeep","20"));
			Assert.IsTrue(config.Save());
			Assert.AreEqual(pathFallback,config.PathSaved);
			StringAssert.Contains(config.LastError,"read-only");
			StringAssert.Contains(config.LastError,"Settings are read from "+path);
			Assert.AreEqual(text,ReadText(path));
			Assert.AreEqual(Lines("; set by the administrator","[Twain]","JournalKeep=20","RestrictSources=1"),ReadText(pathFallback));
			//The ProgramData file still exists, so it is what the next load reads.
			Assert.AreEqual(10,LocalCaptureConfig.Load(path,pathFallback).JournalKeep);
		}

		[TestMethod]
		public void Save_LoadedFromFallback_MovesTheSettingsToPrimaryOnceItIsWritable() {
			string path=PathIn("machine","capture.ini");
			string pathFallback=PathIn("user","capture.ini");
			WriteText(pathFallback,Lines("; per-user copy","[Twain]","JournalKeep=22"));
			LocalCaptureConfig config=LocalCaptureConfig.Load(path,pathFallback);
			Assert.AreEqual(pathFallback,config.PathLoaded);
			Assert.IsTrue(config.Set("Twain","ThreadModal","0"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual("",config.LastError);
			Assert.AreEqual(path,config.PathSaved);
			Assert.AreEqual(Lines("; per-user copy","[Twain]","JournalKeep=22","ThreadModal=0"),ReadText(path));
			LocalCaptureConfig configLoaded=LocalCaptureConfig.Load(path,pathFallback);
			Assert.AreEqual(path,configLoaded.PathLoaded);
			Assert.AreEqual(22,configLoaded.JournalKeep);
			Assert.IsFalse(configLoaded.ThreadModal);
		}

		[TestMethod]
		public void Save_NothingWritable_ReturnsFalseAndKeepsTheValuesPending() {
			string path=BlockedPath("blocker1");
			string pathFallback=BlockedPath("blocker2");
			LocalCaptureConfig config=LocalCaptureConfig.Load(path,pathFallback);
			Assert.IsTrue(config.Set("Twain","JournalKeep","44"));
			Assert.IsFalse(config.Save());
			StringAssert.Contains(config.LastError,PathIn("blocker1"));
			StringAssert.Contains(config.LastError,PathIn("blocker2"));
			Assert.IsTrue(config.HasUnsavedChanges);
			Assert.IsNull(config.PathSaved);
			Assert.AreEqual(44,config.JournalKeep);
			File.Delete(PathIn("blocker2"));
			Assert.IsTrue(config.Save());
			Assert.AreEqual(pathFallback,config.PathSaved);
			Assert.IsFalse(config.HasUnsavedChanges);
			Assert.AreEqual(44,LocalCaptureConfig.Load(path,pathFallback).JournalKeep);
		}

		[TestMethod]
		public void Save_GrantFails_RemovesTheCreatedFolderAndSavesToFallback() {
			string folder=PathIn("machine","Imaging");
			string path=Path.Combine(folder,"capture.ini");
			string pathFallback=PathIn("user","capture.ini");
			FakeUsersAcl fakeUsersAcl=new FakeUsersAcl();
			fakeUsersAcl.FuncGrantResult=pathGrant => "access denied for test";
			LocalCaptureConfig config=fakeUsersAcl.Load(path,pathFallback);
			Assert.IsTrue(config.Set("Twain","JournalKeep","55"));
			Assert.IsTrue(config.Save());
			CollectionAssert.AreEqual(new[] { folder },fakeUsersAcl.ListGrantCalls);
			Assert.IsFalse(Directory.Exists(folder),"the folder created without the Users rule should be removed");
			Assert.AreEqual(pathFallback,config.PathSaved);
			StringAssert.Contains(config.LastError,"access denied for test");
			Assert.AreEqual(55,LocalCaptureConfig.Load(path,pathFallback).JournalKeep);
		}

		[TestMethod]
		public void Save_GrantThrows_IsTreatedAsAFailure() {
			string path=PathIn("machine","capture.ini");
			string pathFallback=PathIn("user","capture.ini");
			LocalCaptureConfig config=LocalCaptureConfig.Load(path,pathFallback,pathGrant => {
				throw new UnauthorizedAccessException("thrown for test");
			},pathHas => false);
			Assert.IsTrue(config.Set("Twain","JournalKeep","56"));
			Assert.IsTrue(config.Save());
			Assert.AreEqual(pathFallback,config.PathSaved);
			StringAssert.Contains(config.LastError,"thrown for test");
			//A check that throws is a failure too.
			config=LocalCaptureConfig.Load(path,pathFallback,pathGrant => "denied for test",pathHas => {
				throw new UnauthorizedAccessException("check thrown for test");
			});
			Assert.IsTrue(config.Set("Twain","JournalKeep","57"));
			Assert.IsTrue(config.Save());
			Assert.AreEqual(pathFallback,config.PathSaved);
			StringAssert.Contains(config.LastError,"check thrown for test");
		}

		[TestMethod]
		public void Save_Grant_RunsWheneverThePrimaryFolderLacksTheRule() {
			string folder=PathIn("machine","Imaging");
			string path=Path.Combine(folder,"capture.ini");
			string pathFallback=PathIn("user","capture.ini");
			FakeUsersAcl fakeUsersAcl=new FakeUsersAcl();
			LocalCaptureConfig config=fakeUsersAcl.Load(path,pathFallback);
			Assert.IsTrue(config.Set("Twain","JournalKeep","1"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual("",config.LastError);
			Assert.AreEqual(path,config.PathSaved);
			//The new file inherits the folder's rule, so only the folder is granted.
			CollectionAssert.AreEqual(new[] { folder },fakeUsersAcl.ListGrantCalls);
			Assert.IsTrue(config.Set("Twain","JournalKeep","2"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual(1,fakeUsersAcl.ListGrantCalls.Count,"the folder already has the rule");
			//A folder something else created first (the journal, the quarantine, an administrator) still gets the rule.
			string folder2=PathIn("machine2","Imaging");
			Directory.CreateDirectory(folder2);
			fakeUsersAcl=new FakeUsersAcl();
			config=fakeUsersAcl.Load(Path.Combine(folder2,"capture.ini"),pathFallback);
			Assert.IsTrue(config.Set("Twain","JournalKeep","3"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual("",config.LastError);
			CollectionAssert.AreEqual(new[] { folder2 },fakeUsersAcl.ListGrantCalls);
			//Never for the per-user fallback folder.
			fakeUsersAcl=new FakeUsersAcl();
			config=fakeUsersAcl.Load(BlockedPath("blocker"),PathIn("user2","capture.ini"));
			Assert.IsTrue(config.Set("Twain","JournalKeep","4"));
			Assert.IsTrue(config.Save());
			Assert.AreEqual(PathIn("user2","capture.ini"),config.PathSaved);
			Assert.AreEqual(0,fakeUsersAcl.ListGrantCalls.Count);
		}

		[TestMethod]
		public void Save_ExistingFolderGrantFails_NewFileGetsItsOwnRule() {
			//An administrator created the Imaging folder, so this user cannot change its ACL, but owns the capture.ini it creates.
			string folder=PathIn("machine","Imaging");
			Directory.CreateDirectory(folder);
			string path=Path.Combine(folder,"capture.ini");
			FakeUsersAcl fakeUsersAcl=new FakeUsersAcl();
			fakeUsersAcl.FuncGrantResult=pathGrant => pathGrant==folder ? "folder denied for test" : "";
			LocalCaptureConfig config=fakeUsersAcl.Load(path,PathIn("user","capture.ini"));
			Assert.IsTrue(config.Set("Twain","JournalKeep","5"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual("",config.LastError,"capture.ini has the rule, so other users can save");
			Assert.AreEqual(path,config.PathSaved);
			Assert.IsTrue(Directory.Exists(folder),"a folder that already existed is never removed");
			CollectionAssert.AreEqual(new[] { folder,path },fakeUsersAcl.ListGrantCalls);
			Assert.AreEqual(5,LocalCaptureConfig.Load(path).JournalKeep);
		}

		[TestMethod]
		public void Save_ExistingFolderNewFileGrantFails_RemovesTheFileAndSavesToFallback() {
			string folder=PathIn("machine","Imaging");
			Directory.CreateDirectory(folder);
			string path=Path.Combine(folder,"capture.ini");
			string pathFallback=PathIn("user","capture.ini");
			FakeUsersAcl fakeUsersAcl=new FakeUsersAcl();
			fakeUsersAcl.FuncGrantResult=pathGrant => "denied for test";
			LocalCaptureConfig config=fakeUsersAcl.Load(path,pathFallback);
			Assert.IsTrue(config.Set("Twain","JournalKeep","6"));
			Assert.IsTrue(config.Save());
			CollectionAssert.AreEqual(new[] { folder,path },fakeUsersAcl.ListGrantCalls);
			Assert.IsTrue(Directory.Exists(folder),"a folder that already existed is never removed");
			Assert.IsFalse(File.Exists(path),"the new capture.ini without the Users rule should be removed");
			Assert.AreEqual(0,Directory.GetFiles(folder).Length,"temporary file left behind");
			Assert.AreEqual(pathFallback,config.PathSaved);
			StringAssert.Contains(config.LastError,path);
			StringAssert.Contains(config.LastError,"denied for test");
			Assert.IsFalse(config.HasUnsavedChanges);
			Assert.AreEqual(6,LocalCaptureConfig.Load(path,pathFallback).JournalKeep);
		}

		[TestMethod]
		public void Save_ExistingFileWithoutTheRule_GetsItWhenPossibleElseWarns() {
			string folder=PathIn("machine","Imaging");
			string path=Path.Combine(folder,"capture.ini");
			WriteText(path,Lines("[Twain]","JournalKeep=10"));
			//This user owns the file (it created it), so the file can get the rule even though the folder cannot.
			FakeUsersAcl fakeUsersAcl=new FakeUsersAcl();
			fakeUsersAcl.FuncGrantResult=pathGrant => pathGrant==folder ? "folder denied for test" : "";
			LocalCaptureConfig config=fakeUsersAcl.Load(path,PathIn("user","capture.ini"));
			Assert.IsTrue(config.Set("Twain","JournalKeep","11"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual("",config.LastError);
			CollectionAssert.AreEqual(new[] { folder,path },fakeUsersAcl.ListGrantCalls);
			//Someone else's file that this user can write but whose ACL it cannot change: saved, with a warning.
			fakeUsersAcl=new FakeUsersAcl();
			fakeUsersAcl.FuncGrantResult=pathGrant => "denied for test";
			config=fakeUsersAcl.Load(path,PathIn("user","capture.ini"));
			Assert.IsTrue(config.Set("Twain","JournalKeep","12"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual(path,config.PathSaved);
			StringAssert.Contains(config.LastError,"Other Windows users");
			StringAssert.Contains(config.LastError,path);
			Assert.IsTrue(File.Exists(path),"an existing file is never removed");
			Assert.AreEqual(12,LocalCaptureConfig.Load(path).JournalKeep);
		}
		#endregion Fallback

		#region EnsureImagingFolder
		[TestMethod]
		public void EnsureImagingFolder_CreatesTheFoldersAndGrantsOnlyTheImagingFolder() {
			string folderImaging=PathIn("ProgramData","OpenPMS-Dental","Imaging");
			FakeUsersAcl fakeUsersAcl=new FakeUsersAcl();
			string error;
			Assert.IsTrue(LocalCaptureConfig.EnsureImagingFolder(folderImaging,@"Capture\journal",fakeUsersAcl.Grant,fakeUsersAcl.Has,out error));
			Assert.AreEqual("",error);
			Assert.IsTrue(Directory.Exists(Path.Combine(folderImaging,"Capture","journal")));
			//The subfolder inherits the rule from the Imaging folder.
			CollectionAssert.AreEqual(new[] { folderImaging },fakeUsersAcl.ListGrantCalls);
			//Calling again changes nothing.
			Assert.IsTrue(LocalCaptureConfig.EnsureImagingFolder(folderImaging,"Capture/quarantine",fakeUsersAcl.Grant,fakeUsersAcl.Has,out error));
			Assert.AreEqual("",error);
			Assert.IsTrue(Directory.Exists(Path.Combine(folderImaging,"Capture","quarantine")));
			Assert.IsTrue(LocalCaptureConfig.EnsureImagingFolder(folderImaging,null,fakeUsersAcl.Grant,fakeUsersAcl.Has,out error));
			Assert.AreEqual(1,fakeUsersAcl.ListGrantCalls.Count);
		}

		[TestMethod]
		public void EnsureImagingFolder_ImagingFolderExistsWithoutTheRule_GetsItOrTheSubfolderDoes() {
			string folderImaging=PathIn("Imaging");
			Directory.CreateDirectory(folderImaging);
			FakeUsersAcl fakeUsersAcl=new FakeUsersAcl();
			string error;
			Assert.IsTrue(LocalCaptureConfig.EnsureImagingFolder(folderImaging,"",fakeUsersAcl.Grant,fakeUsersAcl.Has,out error));
			Assert.AreEqual("",error);
			CollectionAssert.AreEqual(new[] { folderImaging },fakeUsersAcl.ListGrantCalls);
			//An administrator's Imaging folder: the subfolder this user creates gets its own rule.
			string folderImaging2=PathIn("Imaging2");
			Directory.CreateDirectory(folderImaging2);
			string folderJournal=Path.Combine(folderImaging2,"Capture","journal");
			fakeUsersAcl=new FakeUsersAcl();
			fakeUsersAcl.FuncGrantResult=pathGrant => pathGrant==folderImaging2 ? "denied for test" : "";
			Assert.IsTrue(LocalCaptureConfig.EnsureImagingFolder(folderImaging2,@"Capture\journal",fakeUsersAcl.Grant,fakeUsersAcl.Has,out error));
			Assert.AreEqual("",error);
			CollectionAssert.AreEqual(new[] { folderImaging2,folderJournal },fakeUsersAcl.ListGrantCalls);
			//Asking for the administrator's Imaging folder itself: usable, with a warning.
			Assert.IsTrue(LocalCaptureConfig.EnsureImagingFolder(folderImaging2,"",fakeUsersAcl.Grant,fakeUsersAcl.Has,out error));
			StringAssert.Contains(error,"denied for test");
			Assert.IsTrue(Directory.Exists(folderImaging2));
		}

		[TestMethod]
		public void EnsureImagingFolder_CreatedButGrantFails_RemovesItAndReturnsFalse() {
			string folderImaging=PathIn("Imaging");
			FakeUsersAcl fakeUsersAcl=new FakeUsersAcl();
			fakeUsersAcl.FuncGrantResult=pathGrant => "denied for test";
			string error;
			Assert.IsFalse(LocalCaptureConfig.EnsureImagingFolder(folderImaging,"Capture",fakeUsersAcl.Grant,fakeUsersAcl.Has,out error));
			StringAssert.Contains(error,"denied for test");
			Assert.IsFalse(Directory.Exists(folderImaging));
			//An existing Imaging folder stays, but a subfolder this call created without the rule is removed.
			Directory.CreateDirectory(folderImaging);
			Assert.IsFalse(LocalCaptureConfig.EnsureImagingFolder(folderImaging,"Capture",fakeUsersAcl.Grant,fakeUsersAcl.Has,out error));
			StringAssert.Contains(error,"denied for test");
			Assert.IsTrue(Directory.Exists(folderImaging));
			Assert.IsFalse(Directory.Exists(Path.Combine(folderImaging,"Capture")));
			//A folder that cannot be created.
			Assert.IsFalse(LocalCaptureConfig.EnsureImagingFolder(Path.GetDirectoryName(BlockedPath("blocker")),"",fakeUsersAcl.Grant,fakeUsersAcl.Has,out error));
			Assert.AreNotEqual("",error);
		}

		[TestMethod]
		public void EnsureImagingFolder_SubfolderOutsideTheImagingFolder_IsRefused() {
			string folderImaging=PathIn("Imaging");
			FakeUsersAcl fakeUsersAcl=new FakeUsersAcl();
			foreach(string subfolder in new[] { @"..\outside",@"Capture\..\..\outside",@"C:\outside",@"\outside","C:outside",@"Capture\\journal",
				@"Capture\.","a|b","a?b",@"Capture\" })
			{
				string error;
				Assert.IsFalse(LocalCaptureConfig.EnsureImagingFolder(folderImaging,subfolder,fakeUsersAcl.Grant,fakeUsersAcl.Has,out error),subfolder);
				Assert.AreNotEqual("",error,subfolder);
			}
			Assert.IsFalse(Directory.Exists(folderImaging));
			Assert.IsFalse(Directory.Exists(PathIn("outside")));
			Assert.AreEqual(0,fakeUsersAcl.ListGrantCalls.Count);
		}
		#endregion EnsureImagingFolder

		#region Real ACL
		//These run the real GrantUsersModify and HasUsersModify, on folders and files the test creates in its own temporary folder.

		[TestMethod]
		public void RealAcl_GrantUsersModifyOnAFolder_IsInheritedByFilesAndSubfolders() {
			string folder=PathIn("grant");
			Directory.CreateDirectory(folder);
			Assert.AreEqual(0,GetUsersAllowRules(folder,true,false).Count,"a new folder has only inherited rules");
			Assert.AreEqual("",LocalCaptureConfig.GrantUsersModify(folder));
			List<FileSystemAccessRule> listRules=GetUsersAllowRules(folder,true,false);
			Assert.IsTrue(listRules.Any(x => HasModify(x)
				&& x.InheritanceFlags==(InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit)
				&& x.PropagationFlags==PropagationFlags.None),
				string.Join("; ",listRules.Select(x => x.FileSystemRights+" "+x.InheritanceFlags+" "+x.PropagationFlags)));
			Assert.IsTrue(LocalCaptureConfig.HasUsersModify(folder));
			//A file created in the folder inherits Modify.
			string pathFile=Path.Combine(folder,"capture.ini");
			File.WriteAllText(pathFile,"[Twain]\r\n");
			Assert.IsTrue(GetUsersAllowRules(pathFile,false,true).Any(x => HasModify(x)));
			Assert.IsTrue(LocalCaptureConfig.HasUsersModify(pathFile));
			//So does a subfolder, and the files in it.
			string folderSub=Path.Combine(folder,"Capture");
			Directory.CreateDirectory(folderSub);
			Assert.IsTrue(GetUsersAllowRules(folderSub,false,true).Any(x => HasModify(x)
				&& x.InheritanceFlags==(InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit)));
			Assert.IsTrue(LocalCaptureConfig.HasUsersModify(folderSub));
			string pathFileSub=Path.Combine(folderSub,"journal.txt");
			File.WriteAllText(pathFileSub,"x");
			Assert.IsTrue(LocalCaptureConfig.HasUsersModify(pathFileSub));
		}

		[TestMethod]
		public void RealAcl_GrantUsersModifyOnAFile_AddsAnExplicitRule() {
			string pathFile=PathIn("capture.ini");
			File.WriteAllText(pathFile,"[Twain]\r\n");
			Assert.AreEqual(0,GetUsersAllowRules(pathFile,true,false).Count,"a new file has only inherited rules");
			Assert.AreEqual("",LocalCaptureConfig.GrantUsersModify(pathFile));
			Assert.IsTrue(GetUsersAllowRules(pathFile,true,false).Any(x => HasModify(x)));
			Assert.IsTrue(LocalCaptureConfig.HasUsersModify(pathFile));
			Assert.AreNotEqual("",LocalCaptureConfig.GrantUsersModify(PathIn("missing.ini")));
			Assert.IsFalse(LocalCaptureConfig.HasUsersModify(PathIn("missing.ini")));
		}

		[TestMethod]
		public void RealAcl_HasUsersModify_NeedsModifyInheritedByFilesAndSubfolders() {
			if(LocalCaptureConfig.HasUsersModify(_tempDir)) {
				Assert.Inconclusive("The temporary folder already gives the Users group Modify, so the folders below it inherit that rule.");
			}
			SecurityIdentifier sidUsers=new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null);
			//Read and execute only, inherited by both: not enough rights.
			string folderRead=PathIn("read");
			AddFolderRule(folderRead,new FileSystemAccessRule(sidUsers,FileSystemRights.ReadAndExecute,
				InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
			Assert.IsFalse(LocalCaptureConfig.HasUsersModify(folderRead));
			//Modify for subfolders only: files would not inherit it.
			string folderNoFiles=PathIn("nofiles");
			AddFolderRule(folderNoFiles,new FileSystemAccessRule(sidUsers,FileSystemRights.Modify,
				InheritanceFlags.ContainerInherit,PropagationFlags.None,AccessControlType.Allow));
			Assert.IsFalse(LocalCaptureConfig.HasUsersModify(folderNoFiles));
			//Modify for the folder's children only, not the folder itself.
			string folderInheritOnly=PathIn("inheritonly");
			AddFolderRule(folderInheritOnly,new FileSystemAccessRule(sidUsers,FileSystemRights.Modify,
				InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.InheritOnly,AccessControlType.Allow));
			Assert.IsFalse(LocalCaptureConfig.HasUsersModify(folderInheritOnly));
			//So Save adds the full rule to such a folder.
			string path=Path.Combine(folderNoFiles,"capture.ini");
			LocalCaptureConfig config=LocalCaptureConfig.Load(path,PathIn("user","capture.ini"),LocalCaptureConfig.GrantUsersModify,
				LocalCaptureConfig.HasUsersModify);
			Assert.IsTrue(config.Set("Twain","JournalKeep","7"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual("",config.LastError);
			Assert.AreEqual(path,config.PathSaved);
			Assert.IsTrue(LocalCaptureConfig.HasUsersModify(folderNoFiles));
			Assert.IsTrue(LocalCaptureConfig.HasUsersModify(path));
		}

		[TestMethod]
		public void RealAcl_Save_GivesUsersModifyOnAnExistingFolderAndItsCaptureIni() {
			//The folder exists first, as when the journal created it, and has no rule for Users.
			string folder=PathIn("machine","Imaging");
			Directory.CreateDirectory(folder);
			Assert.AreEqual(0,GetUsersAllowRules(folder,true,false).Count);
			string path=Path.Combine(folder,"capture.ini");
			LocalCaptureConfig config=LocalCaptureConfig.Load(path,PathIn("user","capture.ini"),LocalCaptureConfig.GrantUsersModify,
				LocalCaptureConfig.HasUsersModify);
			Assert.IsTrue(config.Set("Twain","JournalKeep","8"));
			Assert.IsTrue(config.Save(),config.LastError);
			Assert.AreEqual("",config.LastError);
			Assert.AreEqual(path,config.PathSaved);
			Assert.IsTrue(LocalCaptureConfig.HasUsersModify(folder));
			Assert.IsTrue(GetUsersAllowRules(path,false,true).Any(x => HasModify(x)),"capture.ini inherits Modify from the folder");
			Assert.AreEqual(0,Directory.GetFiles(folder,"*.tmp").Length,"temporary file left behind");
		}

		private static void AddFolderRule(string folder,FileSystemAccessRule fileSystemAccessRule) {
			Directory.CreateDirectory(folder);
			DirectoryInfo directoryInfo=new DirectoryInfo(folder);
			DirectorySecurity directorySecurity=directoryInfo.GetAccessControl(AccessControlSections.Access);
			directorySecurity.AddAccessRule(fileSystemAccessRule);
			directoryInfo.SetAccessControl(directorySecurity);
		}
		#endregion Real ACL
	}
}
