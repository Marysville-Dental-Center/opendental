// Reading the repository's C# files for the chart's source tests (package TC-2): the program's files, and their code without comments, so that
// a comment that names an old engine or a commented-out line is not taken for code. String and character literals are kept.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ToothChartImageTests {
	public static class SourceText {
		///<summary>A repository file with line feeds only and no byte order mark.</summary>
		public static string Read(string relativePath) {
			string path=Path.Combine(TestPaths.RepoRoot,relativePath);
			Assert.IsTrue(File.Exists(path),"Not found: "+relativePath);
			return File.ReadAllText(path).Replace("\r\n","\n").TrimStart('﻿');
		}

		///<summary>The code of a repository file with every comment replaced by a space (line breaks kept).</summary>
		public static string ReadCode(string relativePath) {
			return WithoutComments(Read(relativePath));
		}

		///<summary>The C# files of a project folder (relative paths), without bin and obj.</summary>
		public static List<string> GetCsFiles(string relativeFolder) {
			string root=TestPaths.RepoRoot+Path.DirectorySeparatorChar;
			return Directory.GetFiles(Path.Combine(TestPaths.RepoRoot,relativeFolder),"*.cs",SearchOption.AllDirectories)
				.Select(x => x.Substring(root.Length))
				.Where(x => !x.Split(Path.DirectorySeparatorChar).Any(y => y.Equals("bin",StringComparison.OrdinalIgnoreCase) || y.Equals("obj",StringComparison.OrdinalIgnoreCase)))
				.OrderBy(x => x,StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		///<summary>code with line comments (// and ///) and block comments replaced by a space, line breaks kept. Strings (regular, verbatim and
		///interpolated, without nested strings) and character literals are skipped over, so "//" inside a string stays.</summary>
		public static string WithoutComments(string code) {
			StringBuilder stringBuilder=new StringBuilder(code.Length);
			int i=0;
			while(i<code.Length) {
				char c=code[i];
				char next=i+1<code.Length ? code[i+1] : ' ';
				char third=i+2<code.Length ? code[i+2] : ' ';
				if(c=='/' && next=='/') {
					while(i<code.Length && code[i]!='\n') {
						i++;
					}
					stringBuilder.Append(' ');
					continue;
				}
				if(c=='/' && next=='*') {
					i+=2;
					while(i<code.Length && !(code[i]=='*' && i+1<code.Length && code[i+1]=='/')) {
						if(code[i]=='\n') {
							stringBuilder.Append('\n');
						}
						i++;
					}
					i=Math.Min(i+2,code.Length);
					stringBuilder.Append(' ');
					continue;
				}
				bool isVerbatim=(c=='@' && next=='"') || (c=='$' && next=='@' && third=='"') || (c=='@' && next=='$' && third=='"');
				if(isVerbatim || c=='"' || (c=='$' && next=='"')) {
					int start=i;
					while(code[i]!='"') {
						i++;
					}
					i++;
					while(i<code.Length) {
						if(isVerbatim) {
							if(code[i]=='"') {
								if(i+1<code.Length && code[i+1]=='"') {
									i+=2;
									continue;
								}
								i++;
								break;
							}
						}
						else {
							if(code[i]=='\\' && i+1<code.Length) {
								i+=2;
								continue;
							}
							if(code[i]=='"' || code[i]=='\n') {
								i++;
								break;
							}
						}
						i++;
					}
					stringBuilder.Append(code,start,i-start);
					continue;
				}
				if(c=='\'') {
					int start=i;
					i++;
					while(i<code.Length && code[i]!='\'' && code[i]!='\n') {
						if(code[i]=='\\') {
							i++;
						}
						i++;
					}
					i=Math.Min(i+1,code.Length);
					stringBuilder.Append(code,start,i-start);
					continue;
				}
				stringBuilder.Append(c);
				i++;
			}
			return stringBuilder.ToString();
		}

		///<summary>The text of a method from its signature to the end of its body, found by counting braces in the code without comments. Fails
		///the test when the signature is not there.</summary>
		public static string GetMethod(string code,string signature) {
			int start=code.IndexOf(signature,StringComparison.Ordinal);
			Assert.IsTrue(start>=0,"Not found: "+signature);
			int open=code.IndexOf('{',start+signature.Length-1);
			int depth=0;
			for(int i=open;i<code.Length;i++) {
				if(code[i]=='{') {
					depth++;
				}
				else if(code[i]=='}') {
					depth--;
					if(depth==0) {
						return code.Substring(start,i+1-start);
					}
				}
			}
			Assert.Fail("No end of "+signature);
			return null;
		}

		///<summary>Asserts that text holds each of arrayParts, in this order.</summary>
		public static void AssertInOrder(string text,string what,params string[] arrayParts) {
			int index=0;
			foreach(string part in arrayParts) {
				int found=text.IndexOf(part,index,StringComparison.Ordinal);
				Assert.IsTrue(found>=0,what+": \""+part+"\" not found"+(index>0 ? " after the call before it" : "")+".");
				index=found+part.Length;
			}
		}
	}
}
