// Guards that no tracked file names the dental practice this fork was first written for, its town, its servers or the developer's computer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NoHqTests {
	///<summary>The repository is a general open-source project. No tracked file names the practice it was first written for or its town, the
	///practice's servers or their addresses, or folders of the developer's computer. The one exception is this repository's address on GitHub,
	///which the GPL needs as the source address (release notes, installer and their tests).
	///
	///The deny-list holds SHA-256 hashes, not the names, so that this file names nothing either (tests\ImagingCaptureTests\VendorNeutralityTests.cs
	///does the same for vendor names). A name is hashed after it is lowercased and stripped of everything but ASCII letters and digits
	///("Ab-Cd 1.2" becomes "abcd12"). Every tracked text file (git ls-files) is checked line by line the same way: every word, every part of a
	///word (a new part starts at a camelCase capital and where letters and digits meet), and every run of up to five parts joined inside a word
	///or by one space, tab, '-', '_', '.', '/', '\' or ':'. Only runs as long as a denied name are hashed, so each entry also gives that length.
	///To add a name, append the hash and length printed by this PowerShell line, with a comment that describes the entry without naming it:
	///  $n='name'.ToLower() -replace '[^a-z0-9]',''; [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($n))).Replace('-','').ToLower(); $n.Length
	///
	///A name that is a folder or file name, whose words are generic in ordinary prose, is not matched across a space or tab.</summary>
	[TestClass]
	public class PracticeNeutralityTests {
		///<summary>Names of the practice, its infrastructure and the developer's computer.</summary>
		private static readonly DeniedName[] _arrayDeniedNames={
			new DeniedName("26840feed23ca71cef4491f5af02bdacda039625a0cbdfe3d5c8f206db76142a",5,true),//the office database server's host name
			new DeniedName("5013394d3a610fc4b2f08768d2f5aaf63925198c5cb91f02ec60ff2a7d483c27",10,true),//that server's network address
			new DeniedName("23ff605cc0ed6a046dcc1395abddd9f3c75f3daa0dc09db67e9b909fd89f63c9",10,true),//the practice's town, the first word of its name
			new DeniedName("1cb5102be030f509902157eb5cd078d0002af134fb320946e7f9a4afe02df64a",22,true),//the practice's full name
			new DeniedName("2397e5a2a678394557c6289a6d3f56446a5c21560387f6d496ca9b924576e883",16,false),//the developer computer's folder of schema dumps
		};
		///<summary>SHA-256 of the lowercased address of this repository on GitHub ("https://github.com/owner/name", without a trailing ".git").
		///Only exactly that prefix is allowed: the same owner in any other text, another of its repositories, or the address without "https://",
		///is still a hit.</summary>
		private static readonly string[] _arrayAllowedUrlHashes={
			"773adf02c67270d8a1776bb5cb2848df3461c83a83669ad3ee70719eb61f9eba",//this repository on GitHub
		};
		///<summary>A GitHub repository address: owner and repository name. The name does not end with a '.', so a full stop after it stays out.</summary>
		private static readonly Regex _regexGitHubRepositoryUrl=new Regex(@"https://github\.com/[A-Za-z0-9-]+/[A-Za-z0-9_.-]*[A-Za-z0-9_-]",
			RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
		private static readonly string[] _arrayExtensionsBinary={ ".dll",".exe",".pdb",".lib",".winmd",".png",".jpg",".jpeg",".gif",".bmp",".ico",
			".cur",".tif",".tiff",".dcm",".wav",".zip",".7z",".gz",".cache",".pfx",".snk",".pdf",".ttf",".woff" };
		///<summary>Characters that join two words of one name besides a space or tab: hyphenated names, host names, file names, paths and URLs.</summary>
		private const string SeparatorsJoining="-_./\\:";
		private const int MaxPartsInRun=5;

		///<summary>One denied name: the SHA-256 of its normalised form, the length of that form, and whether a run joined by a space or tab counts.</summary>
		internal sealed class DeniedName {
			public readonly string Hash;
			public readonly int Length;
			public readonly bool IsMatchedAcrossSpaces;

			public DeniedName(string hash,int length,bool isMatchedAcrossSpaces) {
				Hash=hash;
				Length=length;
				IsMatchedAcrossSpaces=isMatchedAcrossSpaces;
			}

			///<summary>For tests: the entry for a made-up name.</summary>
			public static DeniedName FromName(string name,bool isMatchedAcrossSpaces) {
				string normalized=Normalize(name);
				return new DeniedName(GetHash(normalized),normalized.Length,isMatchedAcrossSpaces);
			}
		}

		///<summary>One denied name found in a file.</summary>
		internal sealed class PracticeHit {
			public string Path;
			public int Line;
			public string Text;

			public override string ToString() {
				return Path+":"+Line+" ("+Text+")";
			}
		}

		///<summary>How a part is joined to the part before it.</summary>
		private enum Join {
			None,
			///<summary>The same word: a camelCase part, or where letters and digits meet.</summary>
			InWord,
			///<summary>One of SeparatorsJoining.</summary>
			Separator,
			///<summary>One space or tab.</summary>
			Space,
		}

		#region Tests
		[TestMethod]
		public void DenyList_IsWellFormed() {
			Assert.IsTrue(_arrayDeniedNames.Length>=5);
			Assert.AreEqual(_arrayDeniedNames.Length,_arrayDeniedNames.Select(x => x.Hash).Distinct().Count(),"No duplicates.");
			foreach(string hash in _arrayDeniedNames.Select(x => x.Hash).Concat(_arrayAllowedUrlHashes)) {
				Assert.IsTrue(hash.Length==64 && hash.All(x => (x>='0' && x<='9') || (x>='a' && x<='f')),"Not a lowercase SHA-256: "+hash);
			}
			foreach(DeniedName deniedName in _arrayDeniedNames) {
				Assert.IsTrue(deniedName.Length>=4,"A name of fewer than four letters and digits would match ordinary words: "+deniedName.Hash);
			}
		}

		[TestMethod]
		public void Scanner_FindsAMadeUpNameInEveryForm() {
			//Made-up names, hashed here, stand in for the denied ones: a name of two words, a host name, an address, and a folder name whose words
			//are generic in prose. The allowed address is a made-up repository of the made-up owner.
			DeniedName[] arrayDenied={
				DeniedName.FromName("Quillmere Bay",true),
				DeniedName.FromName("zorbex-db",true),
				DeniedName.FromName("203.0.113.77",true),
				DeniedName.FromName("vantrel-dumps",false),
			};
			HashSet<string> hashSetAllowed=new HashSet<string> { GetHash("https://github.com/quillmere-bay/opendental") };
			string[] arrayFound={
				"Quillmere Bay Dental",//spaced
				"Written for QUILLMERE   BAY? No: for Quillmere\tBay.",//a tab
				"the quillmere-bay office",//hyphenated
				"QUILLMERE_BAY",
				"QuillmereBayDental",//camelCase
				"quillmerebay",//one word
				"quillmerebay2026",//letters and digits
				@"D:\Work\quillmere-bay\notes.txt",//Windows path
				"/srv/quillmere_bay/notes",//Unix path
				"https://quillmere-bay.example.test/x",//URL
				"https://www.quillmerebay.example.test",
				"https://github.com/quillmere-bay/other",//another repository of the allowed owner
				"https://github.com/quillmere-bay/opendental-tools",
				"https://github.com/quillmere-bay/opendental.tools",
				"http://github.com/quillmere-bay/opendental",//not exactly the allowed prefix
				"github.com/quillmere-bay/opendental",
				"`quillmere-bay/opendental` is a public fork",
				"ssh admin@zorbex-db",
				"Zorbex DB",
				"ZorbexDb01",
				"mysql -h zorbex.db.lan",
				"server=203.0.113.77;port=3306",
				"203.0.113.77:3306",
				@"C:\Data\vantrel-dumps\x.sql",
				"vantrel_dumps",
				"VantrelDumps",
				"/home/dev/vantrel.dumps/x",
			};
			PracticeScanner practiceScanner=new PracticeScanner(arrayDenied,hashSetAllowed);
			foreach(string text in arrayFound) {
				List<PracticeHit> listHits=practiceScanner.Scan(text,"test");
				Assert.AreEqual(1,listHits.Count,"Should find one name in: "+text);
				Assert.AreEqual(1,listHits[0].Line);
			}
			string[] arrayNotFound={
				"Quillmere",//half a name
				"the bay of quillmere",
				"quillmerebays",//inside a longer word
				"Quillmere, Bay",//not joined
				"https://github.com/quillmere-bay/opendental",//the allowed prefix
				"https://github.com/Quillmere-Bay/opendental/tree/v0.0",
				"See https://github.com/quillmere-bay/opendental.",
				"git clone https://github.com/quillmere-bay/opendental.git",
				"zorbex",
				"203.0.113.7",
				"203.0.113.770",
				"Vantrel dumps are generic prose.",//a folder name is not matched across a space
			};
			foreach(string text in arrayNotFound) {
				List<PracticeHit> listHits=practiceScanner.Scan(text,"test");
				Assert.AreEqual(0,listHits.Count,"Should find nothing in: "+text+" ("+string.Join(", ",listHits)+")");
			}
			//Line numbers, one hit per name per line, and the allowed prefix next to a hit on the same line.
			List<PracticeHit> listHitsLines=practiceScanner.Scan(
				"one\r\nQuillmere Bay and quillmere-bay\r\nthree https://github.com/quillmere-bay/opendental zorbex-db\n","test");
			Assert.AreEqual("test:2 (quillmerebay), test:3 (zorbexdb)",string.Join(", ",listHitsLines));
		}

		[TestMethod]
		public void ReadText_SkipsBinaryFilesAndReadsUtf16() {
			Assert.IsNull(ReadText("a.png",Encoding.UTF8.GetBytes("Quillmere Bay")),"A binary extension is skipped.");
			Assert.IsNull(ReadText("a.dat",new byte[] { 0x51,0x00,0x75,0x00,0x69 }),"A file with a NUL byte and no byte order mark is skipped.");
			Assert.AreEqual("Quillmere Bay",ReadText("a.rc",new byte[] { 0xFF,0xFE }.Concat(Encoding.Unicode.GetBytes("Quillmere Bay")).ToArray()));
			Assert.AreEqual("Quillmere Bay",ReadText("a.txt",Encoding.UTF8.GetBytes("Quillmere Bay")));
		}

		[TestMethod]
		public void TrackedFiles_NameNoPracticeServerOrDeveloperFolder() {
			string pathRoot=RemovedAssembliesTests.GetRepositoryRoot();
			List<string> listFiles=GetTrackedFiles(pathRoot);
			PracticeScanner practiceScanner=new PracticeScanner(_arrayDeniedNames,new HashSet<string>(_arrayAllowedUrlHashes));
			List<PracticeHit> listHits=new List<PracticeHit>();
			int countScanned=0;
			foreach(string pathRelative in listFiles) {
				string path=Path.Combine(pathRoot,pathRelative.Replace('/',Path.DirectorySeparatorChar));
				if(!File.Exists(path)) {
					continue;//Deleted in the working tree.
				}
				string text=ReadText(pathRelative,File.ReadAllBytes(path));
				if(text==null) {
					continue;
				}
				countScanned++;
				listHits.AddRange(practiceScanner.Scan(text,pathRelative));
			}
			Assert.IsTrue(countScanned>1000,"Only "+countScanned+" text files scanned of "+listFiles.Count+" tracked.");
			Assert.AreEqual(0,listHits.Count,"Practice-specific names (replace them with neutral wording or a placeholder; only this repository's "
				+"GitHub address is allowed):\r\n"+string.Join("\r\n",listHits));
		}
		#endregion Tests

		#region Scanning
		///<summary>The repository's tracked files, relative to its root with '/'. Inconclusive when git or the repository is not there (for example
		///in a source archive).</summary>
		private static List<string> GetTrackedFiles(string pathRoot) {
			ProcessStartInfo processStartInfo=new ProcessStartInfo("git","ls-files -z") {
				WorkingDirectory=pathRoot,
				UseShellExecute=false,
				RedirectStandardOutput=true,
				RedirectStandardError=true,
				CreateNoWindow=true,
				StandardOutputEncoding=new UTF8Encoding(false),
			};
			Process process;
			try {
				process=Process.Start(processStartInfo);
			}
			catch(Win32Exception ex) {
				Assert.Inconclusive("git could not be started: "+ex.Message);
				return null;
			}
			using(process) {
				Task<string> taskError=process.StandardError.ReadToEndAsync();
				string output=process.StandardOutput.ReadToEnd();
				process.WaitForExit();
				if(process.ExitCode!=0) {
					Assert.Inconclusive("git ls-files failed in "+pathRoot+" (exit code "+process.ExitCode+"): "+taskError.Result);
				}
				return output.Split(new[] { '\0' },StringSplitOptions.RemoveEmptyEntries).ToList();
			}
		}

		///<summary>The file's text, or null for a binary file: one with a binary extension, or a NUL byte without a UTF-16 byte order mark.</summary>
		internal static string ReadText(string path,byte[] arrayBytes) {
			if(_arrayExtensionsBinary.Contains(Path.GetExtension(path).ToLowerInvariant())) {
				return null;
			}
			if(arrayBytes.Length>=2 && arrayBytes[0]==0xFF && arrayBytes[1]==0xFE) {
				return Encoding.Unicode.GetString(arrayBytes,2,arrayBytes.Length-2);
			}
			if(arrayBytes.Length>=2 && arrayBytes[0]==0xFE && arrayBytes[1]==0xFF) {
				return Encoding.BigEndianUnicode.GetString(arrayBytes,2,arrayBytes.Length-2);
			}
			if(Array.IndexOf(arrayBytes,(byte)0)>=0) {
				return null;
			}
			return Encoding.UTF8.GetString(arrayBytes);
		}

		///<summary>Finds denied names in text. Remembers what each candidate it has seen hashes to, so a repeated word is hashed once.</summary>
		internal sealed class PracticeScanner {
			private readonly Dictionary<string,DeniedName> _dictDenied;
			private readonly HashSet<string> _hashSetAllowedUrls;
			private readonly HashSet<int> _hashSetLengths;
			private readonly int _lengthMax;
			///<summary>Each candidate seen, with its denied name, or null when it is not denied.</summary>
			private readonly Dictionary<string,DeniedName> _dictCandidates=new Dictionary<string,DeniedName>();
			private readonly SHA256 _sha256=SHA256.Create();
			private readonly List<int> _listStarts=new List<int>();
			private readonly List<int> _listLengths=new List<int>();
			private readonly List<Join> _listJoins=new List<Join>();

			public PracticeScanner(DeniedName[] arrayDenied,HashSet<string> hashSetAllowedUrls) {
				_dictDenied=arrayDenied.ToDictionary(x => x.Hash);
				_hashSetAllowedUrls=hashSetAllowedUrls;
				_hashSetLengths=new HashSet<int>(arrayDenied.Select(x => x.Length));
				_lengthMax=arrayDenied.Max(x => x.Length);
			}

			///<summary>The denied names in text, one hit per name per line. An allowed GitHub address is blanked out first.</summary>
			public List<PracticeHit> Scan(string text,string pathRelative) {
				List<PracticeHit> listHits=new List<PracticeHit>();
				string[] arrayLines=text.Split('\n');
				for(int idxLine=0;idxLine<arrayLines.Length;idxLine++) {
					string line=MaskAllowedUrls(arrayLines[idxLine]);
					GetParts(line,_listStarts,_listLengths,_listJoins);
					HashSet<string> hashSetLineHits=null;
					for(int i=0;i<_listStarts.Count;i++) {
						int length=0;
						bool isSpaced=false;
						for(int n=0;n<MaxPartsInRun && i+n<_listStarts.Count;n++) {
							int j=i+n;
							if(n>0) {
								if(_listJoins[j]==Join.None) {
									break;
								}
								if(_listJoins[j]==Join.Space) {
									isSpaced=true;
								}
							}
							length+=_listLengths[j];
							if(length>_lengthMax) {
								break;
							}
							if(!_hashSetLengths.Contains(length)) {
								continue;
							}
							StringBuilder stringBuilder=new StringBuilder(length);
							for(int k=i;k<=j;k++) {
								stringBuilder.Append(line,_listStarts[k],_listLengths[k]);
							}
							string candidate=stringBuilder.ToString().ToLowerInvariant();
							DeniedName deniedName=GetDeniedName(candidate);
							if(deniedName==null || (isSpaced && !deniedName.IsMatchedAcrossSpaces)) {
								continue;
							}
							if(hashSetLineHits==null) {
								hashSetLineHits=new HashSet<string>();
							}
							if(hashSetLineHits.Add(deniedName.Hash)) {
								listHits.Add(new PracticeHit { Path=pathRelative,Line=idxLine+1,Text=candidate });
							}
						}
					}
				}
				return listHits;
			}

			private DeniedName GetDeniedName(string candidate) {
				DeniedName deniedName;
				if(_dictCandidates.TryGetValue(candidate,out deniedName)) {
					return deniedName;
				}
				_dictDenied.TryGetValue(GetHash(_sha256,candidate),out deniedName);
				if(_dictCandidates.Count>=1000000) {
					_dictCandidates.Clear();//Bounds the memory; every candidate is still checked.
				}
				_dictCandidates[candidate]=deniedName;
				return deniedName;
			}

			///<summary>The line with every allowed GitHub repository address replaced by spaces.</summary>
			private string MaskAllowedUrls(string line) {
				if(line.IndexOf("github.com",StringComparison.OrdinalIgnoreCase)<0) {
					return line;
				}
				return _regexGitHubRepositoryUrl.Replace(line,match => {
					string url=match.Value.ToLowerInvariant();
					if(url.EndsWith(".git")) {
						url=url.Substring(0,url.Length-4);
					}
					return _hashSetAllowedUrls.Contains(GetHash(_sha256,url)) ? new string(' ',match.Length) : match.Value;
				});
			}
		}

		///<summary>Fills the lists with the parts of the line's words (start, length) and how each part joins the one before it.</summary>
		private static void GetParts(string line,List<int> listStarts,List<int> listLengths,List<Join> listJoins) {
			listStarts.Clear();
			listLengths.Clear();
			listJoins.Clear();
			int idx=0;
			int idxEndPrev=-1;
			while(idx<line.Length) {
				if(!IsAsciiLetterOrDigit(line[idx])) {
					idx++;
					continue;
				}
				int idxStart=idx;
				while(idx<line.Length && IsAsciiLetterOrDigit(line[idx])) {
					idx++;
				}
				Join joinFirst=Join.None;
				if(idxEndPrev>=0 && idxStart-idxEndPrev==1) {
					char separator=line[idxEndPrev];
					if(separator==' ' || separator=='\t') {
						joinFirst=Join.Space;
					}
					else if(SeparatorsJoining.IndexOf(separator)>=0) {
						joinFirst=Join.Separator;
					}
				}
				int idxPart=idxStart;
				for(int i=idxStart+1;i<idx;i++) {
					if(IsPartBoundary(line,i,idx)) {
						listStarts.Add(idxPart);
						listLengths.Add(i-idxPart);
						listJoins.Add(idxPart==idxStart ? joinFirst : Join.InWord);
						idxPart=i;
					}
				}
				listStarts.Add(idxPart);
				listLengths.Add(idx-idxPart);
				listJoins.Add(idxPart==idxStart ? joinFirst : Join.InWord);
				idxEndPrev=idx;
			}
		}

		///<summary>True where a new part of a word starts at idx: a capital after a lowercase letter or a digit, the last capital of a run of capitals
		///that starts a new word ("XMLHttp" gives "XML" and "Http"), and where letters and digits meet ("Db01" gives "Db" and "01").</summary>
		private static bool IsPartBoundary(string word,int idx,int idxEnd) {
			char cPrev=word[idx-1];
			char c=word[idx];
			bool isNextLower=idx+1<idxEnd && IsAsciiLower(word[idx+1]);
			return ((IsAsciiLower(cPrev) || IsAsciiDigit(cPrev)) && IsAsciiUpper(c))
				|| (IsAsciiUpper(cPrev) && IsAsciiUpper(c) && isNextLower)
				|| (IsAsciiDigit(cPrev)!=IsAsciiDigit(c));
		}

		private static string Normalize(string text) {
			return new string(text.Where(IsAsciiLetterOrDigit).ToArray()).ToLowerInvariant();
		}

		private static bool IsAsciiLower(char c) {
			return c>='a' && c<='z';
		}

		private static bool IsAsciiUpper(char c) {
			return c>='A' && c<='Z';
		}

		private static bool IsAsciiDigit(char c) {
			return c>='0' && c<='9';
		}

		private static bool IsAsciiLetterOrDigit(char c) {
			return IsAsciiLower(c) || IsAsciiUpper(c) || IsAsciiDigit(c);
		}

		private static string GetHash(string text) {
			using(SHA256 sha256=SHA256.Create()) {
				return GetHash(sha256,text);
			}
		}

		private static string GetHash(SHA256 sha256,string text) {
			byte[] arrayHash=sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
			StringBuilder stringBuilder=new StringBuilder(64);
			foreach(byte b in arrayHash) {
				stringBuilder.Append(b.ToString("x2"));
			}
			return stringBuilder.ToString();
		}
		#endregion Scanning
	}
}
