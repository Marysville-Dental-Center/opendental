// The repository folder, a reader of C# sources without comments, and the guard that keeps every test away from the computer's clock and its
// Event Log.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace EhrTests {
	///<summary>Runs before any test of this assembly: replaces the clock seam (WindowsTime.FuncSetSystemTimeUtc) and both Event Log seams
	///(NTPv4.ActionWriteEventLog, WindowsTime.ActionWriteEventLog), so no test can reach SetSystemTime or write the Event Log. A test that needs a
	///seam puts its own fake in and calls ClockGuard.Install again in a finally block.</summary>
	[TestClass]
	public class AssemblySetup {
		[AssemblyInitialize]
		public static void Initialize(TestContext testContext) {
			ClockGuard.Install();
		}
	}

	///<summary>The fakes that stand in for the clock and the Event Log in every test.</summary>
	public static class ClockGuard {
		///<summary>The name of the method behind the clock seam before the guard replaced it: read, never called.</summary>
		public static string NameOfOriginalSeam;
		///<summary>The name of the method behind NTPv4's Event Log seam before the guard replaced it: read, never called.</summary>
		public static string NameOfOriginalEventLogSeamNtp;
		///<summary>The name of the method behind WindowsTime's Event Log seam before the guard replaced it: read, never called.</summary>
		public static string NameOfOriginalEventLogSeamWindowsTime;
		///<summary>Calls that reached the guard. Always 0 unless a test forgot its own fake.</summary>
		public static int CountBlockedCalls;

		public static void Install() {
			if(NameOfOriginalSeam==null) {
				NameOfOriginalSeam=WindowsTime.FuncSetSystemTimeUtc.Method.Name;
			}
			if(NameOfOriginalEventLogSeamNtp==null) {
				NameOfOriginalEventLogSeamNtp=NTPv4.ActionWriteEventLog.Method.Name;
			}
			if(NameOfOriginalEventLogSeamWindowsTime==null) {
				NameOfOriginalEventLogSeamWindowsTime=WindowsTime.ActionWriteEventLog.Method.Name;
			}
			WindowsTime.FuncSetSystemTimeUtc=(dateTimeUtc) => {
				CountBlockedCalls++;
				throw new InvalidOperationException("A test reached the clock without a fake of its own. Tests never set the clock.");
			};
			//Both Event Log seams: an entry a test makes (NTPv4.Query writes one per request) goes nowhere.
			NTPv4.ActionWriteEventLog=(text) => { };
			WindowsTime.ActionWriteEventLog=(text) => { };
		}
	}

	///<summary>MSTest v1 has no Assert.ThrowsException.</summary>
	internal static class Expect {
		public static T Throws<T>(Action action,string message="") where T:Exception {
			try {
				action();
			}
			catch(T ex) {
				return ex;
			}
			catch(Exception ex) {
				Assert.Fail("Expected "+typeof(T).Name+" but got "+ex.GetType().Name+": "+ex.Message+" "+message);
			}
			Assert.Fail("Expected "+typeof(T).Name+" but nothing was thrown. "+message);
			return null;
		}
	}

	///<summary>The repository root is found from the test assembly's folder (tests\EhrTests\bin\Debug), so the tests work in any worktree.</summary>
	public static class TestPaths {
		private static string _repoRoot;

		///<summary>The first folder above the test assembly that holds LICENSE and NOTICE (not found by the solution's name, which a rename may
		///change).</summary>
		public static string RepoRoot {
			get {
				if(_repoRoot!=null) {
					return _repoRoot;
				}
				string folder=AppDomain.CurrentDomain.BaseDirectory;
				while(folder!=null && !(File.Exists(Path.Combine(folder,"LICENSE")) && File.Exists(Path.Combine(folder,"NOTICE")))) {
					folder=Path.GetDirectoryName(folder.TrimEnd('\\'));
				}
				if(folder==null) {
					throw new InvalidOperationException("The repository root (LICENSE and NOTICE) was not found above "+AppDomain.CurrentDomain.BaseDirectory);
				}
				_repoRoot=folder.TrimEnd('\\');
				return _repoRoot;
			}
		}

		///<summary>A repository file by its path relative to the root, with either kind of slash.</summary>
		public static string Combine(string pathRelative) {
			return Path.Combine(RepoRoot,pathRelative.Replace('/','\\'));
		}

		///<summary>Reads a repository file by its path relative to the root.</summary>
		public static string ReadSource(string pathRelative) {
			string path=Combine(pathRelative);
			Assert.IsTrue(File.Exists(path),"Missing: "+pathRelative);
			return File.ReadAllText(path);
		}

		///<summary>The path relative to the root, with backslashes.</summary>
		public static string Relative(string path) {
			return path.Substring(RepoRoot.Length+1);
		}

		///<summary>The C# files of the program's own code: every .cs file under the root except build output, hidden folders, packages and the test
		///projects (tests, NoHqTests, UnitTests, UnitTestsCore), whose sources name what they check.</summary>
		public static List<string> ProgramSourceFiles() {
			HashSet<string> hashSetSkipped=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
				"bin","obj","packages","node_modules","tests","NoHqTests","UnitTests","UnitTestsCore" };
			List<string> listFiles=new List<string>();
			Stack<string> stackFolders=new Stack<string>();
			stackFolders.Push(RepoRoot);
			while(stackFolders.Count>0) {
				string folder=stackFolders.Pop();
				listFiles.AddRange(Directory.GetFiles(folder,"*.cs"));
				foreach(string folderChild in Directory.GetDirectories(folder)) {
					string name=Path.GetFileName(folderChild);
					if(name.StartsWith(".") || hashSetSkipped.Contains(name)) {
						continue;
					}
					stackFolders.Push(folderChild);
				}
			}
			listFiles.Sort(StringComparer.OrdinalIgnoreCase);
			return listFiles;
		}
	}

	///<summary>A small C# reader: the code without its comments, and the string literals. It knows regular, verbatim and interpolated strings and
	///character literals, so "//" inside a string is not a comment and a comment that quotes code is not code.</summary>
	public class CSharpSource {
		private const char NEWLINE='\n';
		private const char BACKSLASH='\\';
		private const char QUOTE='"';
		private const char APOSTROPHE='\'';
		///<summary>The code with every comment replaced by a space (line breaks kept, so line numbers stay).</summary>
		public string CodeWithoutComments;
		///<summary>The same text with the contents of every string and character literal blanked (same length as CodeWithoutComments), for counting
		///braces.</summary>
		public string CodeWithoutCommentsOrLiterals;
		///<summary>The contents of every string literal, in order.</summary>
		public List<string> ListStringLiterals=new List<string>();

		public static CSharpSource Read(string code) {
			CSharpSource cSharpSource=new CSharpSource();
			StringBuilder stringBuilderCode=new StringBuilder(code.Length);
			StringBuilder stringBuilderBlank=new StringBuilder(code.Length);
			int i=0;
			while(i<code.Length) {
				char c=code[i];
				char next=i+1<code.Length ? code[i+1] : ' ';
				char third=i+2<code.Length ? code[i+2] : ' ';
				if(c=='/' && next=='/') {
					while(i<code.Length && code[i]!=NEWLINE) {
						i++;
					}
					stringBuilderCode.Append(' ');
					stringBuilderBlank.Append(' ');
					continue;
				}
				if(c=='/' && next=='*') {
					i+=2;
					while(i<code.Length && !(code[i]=='*' && i+1<code.Length && code[i+1]=='/')) {
						if(code[i]==NEWLINE) {
							stringBuilderCode.Append(NEWLINE);
							stringBuilderBlank.Append(NEWLINE);
						}
						i++;
					}
					i=Math.Min(i+2,code.Length);
					stringBuilderCode.Append(' ');
					stringBuilderBlank.Append(' ');
					continue;
				}
				bool isVerbatim=(c=='@' && next==QUOTE) || (c=='$' && next=='@' && third==QUOTE) || (c=='@' && next=='$' && third==QUOTE);
				if(isVerbatim || c==QUOTE || (c=='$' && next==QUOTE)) {
					int start=i;
					while(code[i]!=QUOTE) {
						i++;
					}
					i++;
					StringBuilder stringBuilderLiteral=new StringBuilder();
					while(i<code.Length) {
						if(isVerbatim) {
							if(code[i]==QUOTE) {
								if(i+1<code.Length && code[i+1]==QUOTE) {
									stringBuilderLiteral.Append(QUOTE);
									i+=2;
									continue;
								}
								i++;
								break;
							}
						}
						else {
							if(code[i]==BACKSLASH && i+1<code.Length) {
								stringBuilderLiteral.Append(code[i]).Append(code[i+1]);
								i+=2;
								continue;
							}
							if(code[i]==QUOTE || code[i]==NEWLINE) {
								i++;
								break;
							}
						}
						stringBuilderLiteral.Append(code[i]);
						i++;
					}
					cSharpSource.ListStringLiterals.Add(stringBuilderLiteral.ToString());
					AppendLiteral(code,start,i,stringBuilderCode,stringBuilderBlank);
					continue;
				}
				if(c==APOSTROPHE) {
					int start=i;
					i++;
					while(i<code.Length && code[i]!=APOSTROPHE && code[i]!=NEWLINE) {
						if(code[i]==BACKSLASH) {
							i++;
						}
						i++;
					}
					i=Math.Min(i+1,code.Length);
					AppendLiteral(code,start,i,stringBuilderCode,stringBuilderBlank);
					continue;
				}
				stringBuilderCode.Append(c);
				stringBuilderBlank.Append(c);
				i++;
			}
			cSharpSource.CodeWithoutComments=stringBuilderCode.ToString();
			cSharpSource.CodeWithoutCommentsOrLiterals=stringBuilderBlank.ToString();
			return cSharpSource;
		}

		///<summary>Appends a literal as it is to the code, and blanked (its quotes and line breaks kept) to the blanked code.</summary>
		private static void AppendLiteral(string code,int start,int end,StringBuilder stringBuilderCode,StringBuilder stringBuilderBlank) {
			end=Math.Min(end,code.Length);
			stringBuilderCode.Append(code,start,end-start);
			for(int i=start;i<end;i++) {
				char c=code[i];
				if(c==NEWLINE || c=='\r' || c==QUOTE || c==APOSTROPHE || i==start) {
					stringBuilderBlank.Append(c);
				}
				else {
					stringBuilderBlank.Append(' ');
				}
			}
		}

		///<summary>The text of a method (or any block) from its signature to the end of its body, without comments, found by counting braces outside
		///literals. Fails the test when the signature is not there.</summary>
		public static string GetBlock(string code,string signature) {
			CSharpSource cSharpSource=Read(code);
			int indexStart=cSharpSource.CodeWithoutComments.IndexOf(signature,StringComparison.Ordinal);
			Assert.IsTrue(indexStart>=0,"Not found: "+signature);
			string blank=cSharpSource.CodeWithoutCommentsOrLiterals;
			int indexBrace=blank.IndexOf('{',indexStart);
			int depth=0;
			for(int i=indexBrace;i<blank.Length;i++) {
				if(blank[i]=='{') {
					depth++;
				}
				else if(blank[i]=='}') {
					depth--;
					if(depth==0) {
						return cSharpSource.CodeWithoutComments.Substring(indexStart,i-indexStart+1);
					}
				}
			}
			Assert.Fail("No end of the block: "+signature);
			return "";
		}
	}
}
