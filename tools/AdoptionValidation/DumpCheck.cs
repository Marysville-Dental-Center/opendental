// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AdoptionValidation {
	///<summary>Checks that a logical backup only writes to the database it is loaded into. A dump made with --databases or --all-databases names
	///its databases (CREATE DATABASE, USE, and with --add-drop-database DROP DATABASE). Loaded into a "scratch" database, such a file replays DROP
	///TABLE, CREATE TABLE and INSERT into the database it names, which on the office server is the live one; with MyISAM there is no undo. The
	///restore command refuses any such file before it runs anything. It reads only the first bytes of each line, never prints values, and works on
	///files of any size.</summary>
	public static class DumpCheck {
		///<summary>Bytes of each line that are looked at. A statement's keywords and its first table name are always within them.</summary>
		private const int LINE_START_BYTES=256;

		///<summary>An optional version-conditional comment opener, as mariadb-dump writes them: /*!40000 or /*M!100100.</summary>
		private const string CONDITIONAL=@"^(?:/\*M?!\d*\s*)?";
		private static readonly Regex _regexUse=new Regex(CONDITIONAL+@"USE(?:\s|`|;|$)\s*(?<name>`[^`]*`|[^\s;]*)",
			RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
		private static readonly Regex _regexDatabase=new Regex(CONDITIONAL+@"(?<verb>CREATE|DROP|ALTER)\s+(?:DATABASE|SCHEMA)\b",
			RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
		///<summary>Statements a dump starts with, followed by a table name qualified with a database: `db`.`table` or db.table.</summary>
		private static readonly Regex _regexQualified=new Regex(CONDITIONAL+@"(?<verb>INSERT\s+(?:IGNORE\s+)?INTO|REPLACE\s+INTO|LOCK\s+TABLES|"
			+@"DROP\s+TABLE(?:\s+IF\s+EXISTS)?|CREATE\s+TABLE(?:\s+IF\s+NOT\s+EXISTS)?|ALTER\s+TABLE|TRUNCATE(?:\s+TABLE)?|DELETE\s+FROM|UPDATE|RENAME\s+TABLE)"
			+@"\s+(?<db>`[^`]+`|[A-Za-z0-9_$]+)\s*\.",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);

		public class Problem {
			public long Line;
			public string Description;

			public override string ToString() {
				return "line "+Line+": "+Description;
			}
		}

		///<summary>Every line that would write outside the target database, up to maxProblems.</summary>
		public static List<Problem> Scan(string path,int maxProblems=20) {
			List<Problem> listProblems=new List<Problem>();
			byte[] buffer=new byte[1<<20];
			byte[] lineStart=new byte[LINE_START_BYTES];
			int lineStartLength=0;
			bool isCollecting=true;
			long line=1;
			using(FileStream fileStream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,1<<16,FileOptions.SequentialScan)) {
				int read;
				while((read=fileStream.Read(buffer,0,buffer.Length))>0) {
					for(int i=0;i<read;i++) {
						byte b=buffer[i];
						if(b==(byte)'\n') {
							CheckLine(lineStart,lineStartLength,line,listProblems);
							if(listProblems.Count>=maxProblems) {
								return listProblems;
							}
							line++;
							lineStartLength=0;
							isCollecting=true;
							continue;
						}
						if(!isCollecting) {
							continue;
						}
						lineStart[lineStartLength++]=b;
						if(lineStartLength==LINE_START_BYTES) {
							isCollecting=false;
						}
					}
				}
			}
			CheckLine(lineStart,lineStartLength,line,listProblems);
			return listProblems;
		}

		///<summary>The problem with one line, or null. Public for the tests.</summary>
		public static string CheckLine(string text) {
			string trimmed=text.TrimStart(' ','\t','\r','﻿');
			Match match=_regexUse.Match(trimmed);
			if(match.Success) {
				return "USE "+match.Groups["name"].Value+" (the dump switches to another database)";
			}
			match=_regexDatabase.Match(trimmed);
			if(match.Success) {
				return match.Groups["verb"].Value.ToUpperInvariant()+" DATABASE (the dump creates, drops or changes a database)";
			}
			match=_regexQualified.Match(trimmed);
			if(match.Success) {
				return Regex.Replace(match.Groups["verb"].Value.ToUpperInvariant(),@"\s+"," ")+" on a table of database "+match.Groups["db"].Value
					+" (a qualified table name)";
			}
			return null;
		}

		private static void CheckLine(byte[] lineStart,int length,long line,List<Problem> listProblems) {
			if(length==0) {
				return;
			}
			//Only the statement's keywords and names matter; any bytes that are not UTF-8 come out as replacement characters.
			string problem=CheckLine(Encoding.UTF8.GetString(lineStart,0,length));
			if(problem!=null) {
				listProblems.Add(new Problem { Line=line,Description=problem });
			}
		}
	}
}
