// The committed db/init/new-database.sql as text, and the procedure code texts of the seed dump it is generated from. No database needed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace InitSqlTests {
	[TestClass]
	public class InitSqlFileTests {
		public const string RepoPathFile=@"db\init\new-database.sql";
		public const string RepoPathSeed=@"UnitTests\Resources\dump.txt";
		///<summary>The procedure codes that are not CDT-style codes, with the Open Dental text they keep. A code added here must not carry ADA text.
		///</summary>
		public static readonly Dictionary<string,string> DictOtherCodes=new Dictionary<string,string> {
			{ "~GRP~","Group Note" },
			{ "D8670.auto","Default auto ortho procedure code" },
		};
		private static byte[] _arrayBytes;
		private static string _text;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_arrayBytes=File.ReadAllBytes(ScratchDb.FindRepoFile(RepoPathFile));
			_text=new UTF8Encoding(false,true).GetString(_arrayBytes);
		}

		[TestMethod]
		public void File_IsUtf8WithoutBomAndHasLfLineEnds() {
			Assert.IsFalse(_arrayBytes.Length>=3 && _arrayBytes[0]==0xEF && _arrayBytes[1]==0xBB && _arrayBytes[2]==0xBF,"No byte order mark.");
			Assert.IsFalse(_text.Contains("\r"),"LF line ends only (db/.gitattributes keeps them in every checkout).");
			Assert.IsTrue(_text.EndsWith(";\n"),"The file ends with a complete statement and a line end.");
		}

		[TestMethod]
		public void File_HasTheSeedSessionLines() {
			StringAssert.Contains(_text,"/*!40101 SET NAMES utf8 */;\n");
			StringAssert.Contains(_text,"/*!40101 SET SQL_MODE=''*/;\n");
			StringAssert.Contains(_text,"/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;\n");
			StringAssert.Contains(_text,"/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;\n");
		}

		[TestMethod]
		public void File_CreatesMyIsamTablesAndDropsNothing() {
			List<string> listLines=_text.Split('\n').ToList();
			string[] arrayForbiddenStarts={ "DROP ","TRUNCATE ","DELETE ","UPDATE ","ALTER ","CREATE DATABASE","USE ","GRANT ","CREATE USER" };
			foreach(string line in listLines) {
				foreach(string start in arrayForbiddenStarts) {
					Assert.IsFalse(line.StartsWith(start,StringComparison.OrdinalIgnoreCase),"Only CREATE TABLE and INSERT, no '"+start.Trim()+"': "+line);
				}
			}
			Assert.IsFalse(Regex.IsMatch(_text,@"CREATE TABLE IF NOT EXISTS",RegexOptions.IgnoreCase),
				"Plain CREATE TABLE, so that loading into a database that has tables stops at once.");
			MatchCollection matchesCreate=Regex.Matches(_text,@"^CREATE TABLE `(\w+)` \(\n.*?\n\) (.*?);$",RegexOptions.Multiline|RegexOptions.Singleline);
			Assert.IsTrue(matchesCreate.Count>400,"Every table of the schema: "+matchesCreate.Count);
			Assert.AreEqual(Regex.Matches(_text,"^CREATE TABLE ",RegexOptions.Multiline).Count,matchesCreate.Count);
			foreach(Match match in matchesCreate) {
				StringAssert.StartsWith(match.Groups[2].Value,"ENGINE=MyISAM ","Table "+match.Groups[1].Value);
				Assert.IsFalse(match.Groups[2].Value.Contains("AUTO_INCREMENT="),"No AUTO_INCREMENT counter: "+match.Groups[1].Value);
			}
			List<string> listTables=matchesCreate.Cast<Match>().Select(x => x.Groups[1].Value).ToList();
			CollectionAssert.AreEqual(listTables.OrderBy(x => x,StringComparer.Ordinal).ToList(),listTables,"Tables in name order.");
		}

		[TestMethod]
		public void File_HeaderNamesThisBuildsForkLevelAndSchema() {
			//A fork migration step added without regenerating the file fails here already, in the fast tier.
			string header=_text.Substring(0,_text.IndexOf("*/",StringComparison.Ordinal));
			StringAssert.Contains(header,"at fork schema level "+ForkMigrationHandOff.Runner.LatestLevel+" ",
				"Regenerate the file with tools/New-InitSql.ps1 after a new fork migration step.");
			StringAssert.Contains(header,"(the Open Dental "+SchemaVerifier.ReferenceVersion+" schema");
			StringAssert.Contains(header,"GNU General Public License, version 2");
		}

		[TestMethod]
		public void File_HasNoNameOfThisPcOrTheSeedsPcs() {
			List<string> listNames=new List<string> { Environment.MachineName,Environment.UserName,"JORDANS" };
			foreach(string name in listNames.Where(x => !string.IsNullOrEmpty(x) && x.Length>=4)) {
				Assert.AreEqual(-1,_text.IndexOf(name,StringComparison.OrdinalIgnoreCase),"The file names '"+name+"'.");
			}
			string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if(!string.IsNullOrEmpty(profile)) {
				Assert.AreEqual(-1,_text.IndexOf(profile.Replace("\\","\\\\"),StringComparison.OrdinalIgnoreCase),"The file names "+profile+".");
			}
			//Scratch database names of the generator and of these tests.
			Assert.AreEqual(-1,_text.IndexOf("initsql_",StringComparison.OrdinalIgnoreCase));
			Assert.AreEqual(-1,_text.IndexOf("initsqltest_",StringComparison.OrdinalIgnoreCase));
			Assert.AreEqual(-1,_text.IndexOf("forkmig_",StringComparison.OrdinalIgnoreCase));
		}

		[TestMethod]
		public void File_HasNoOpenDentalHost() {
			foreach(string domain in HqHosts.BlockedDomains) {
				Match match=Regex.Match(_text,@"(?i)(?<![a-z0-9-])(?:[a-z0-9-]+\.)*"+Regex.Escape(domain)+"(?![a-z0-9-])");
				Assert.IsFalse(match.Success,"The file holds the Open Dental host "+match.Value+".");
			}
		}

		[TestMethod]
		public void File_ProcedureCodesAreNamedByCodeOnly() {
			List<string[]> listRows=GetProcedureCodeRows(_text,"INSERT INTO `procedurecode` (");
			AssertNeutral(listRows);
		}

		[TestMethod]
		public void Seed_ProcedureCodesAreNamedByCodeOnly() {
			string seed=File.ReadAllText(ScratchDb.FindRepoFile(RepoPathSeed),new UTF8Encoding(false)).Replace("\r\n","\n");
			List<string[]> listRows=GetProcedureCodeRows(seed,"insert  into `procedurecode`(");
			AssertNeutral(listRows);
		}

		///<summary>Code, Descript, AbbrDesc and LaymanTerm of every row of the procedurecode INSERT statements that start with the prefix.</summary>
		private static List<string[]> GetProcedureCodeRows(string text,string insertPrefix) {
			List<string[]> listRows=new List<string[]>();
			string[] arrayLines=text.Split('\n');
			for(int i=0;i<arrayLines.Length;i++) {
				if(!arrayLines[i].StartsWith(insertPrefix,StringComparison.Ordinal)) {
					continue;
				}
				List<string> listColumns=Regex.Matches(arrayLines[i].Substring(0,arrayLines[i].IndexOf(")",StringComparison.Ordinal)),@"`(\w+)`")
					.Cast<Match>().Select(x => x.Groups[1].Value).Skip(1).ToList();
				int indexLayman=listColumns.IndexOf("LaymanTerm");
				Assert.AreEqual("CodeNum,ProcCode,Descript,AbbrDesc",string.Join(",",listColumns.Take(4)));
				Assert.IsTrue(indexLayman>0);
				for(int j=i+1;j<arrayLines.Length;j++) {
					List<string> listValues=SplitValues(arrayLines[j]);
					Assert.AreEqual(listColumns.Count,listValues.Count,"Values of "+arrayLines[j].Substring(0,Math.Min(60,arrayLines[j].Length)));
					listRows.Add(new[] { Unquote(listValues[1]),Unquote(listValues[2]),Unquote(listValues[3]),Unquote(listValues[indexLayman]) });
					if(arrayLines[j].EndsWith(";")) {
						break;
					}
				}
			}
			Assert.IsTrue(listRows.Count>0,"No procedurecode rows found.");
			return listRows;
		}

		private static void AssertNeutral(List<string[]> listRows) {
			int countCdt=0;
			foreach(string[] row in listRows) {
				string code=row[0];
				if(Regex.IsMatch(code,@"^D\d{4}$")) {
					countCdt++;
					Assert.AreEqual("Procedure "+code,row[1],"Descript of "+code);
					Assert.AreEqual(code,row[2],"AbbrDesc of "+code);
					Assert.AreEqual("",row[3]??"","LaymanTerm of "+code);
					continue;
				}
				Assert.IsTrue(DictOtherCodes.ContainsKey(code),"A procedure code that is not CDT-style: "+code+" '"+row[1]+"'. Check that it carries no ADA "
					+"text, then list it in DictOtherCodes.");
				Assert.AreEqual(DictOtherCodes[code],row[1],"Descript of "+code);
			}
			Assert.IsTrue(countCdt>500,"CDT-style codes: "+countCdt);
		}

		///<summary>The values of one row line "(1,'a',NULL),": quotes and backslash escapes respected.</summary>
		private static List<string> SplitValues(string line) {
			string row=line.TrimEnd(',',';');
			Assert.IsTrue(row.StartsWith("(") && row.EndsWith(")"),"A row line: "+line.Substring(0,Math.Min(60,line.Length)));
			row=row.Substring(1,row.Length-2);
			List<string> listValues=new List<string>();
			StringBuilder sb=new StringBuilder();
			bool isInQuote=false;
			for(int i=0;i<row.Length;i++) {
				char c=row[i];
				if(isInQuote) {
					sb.Append(c);
					if(c=='\\' && i+1<row.Length) {
						sb.Append(row[++i]);
					}
					else if(c=='\'') {
						isInQuote=false;
					}
					continue;
				}
				if(c=='\'') {
					isInQuote=true;
					sb.Append(c);
				}
				else if(c==',') {
					listValues.Add(sb.ToString());
					sb.Clear();
				}
				else {
					sb.Append(c);
				}
			}
			listValues.Add(sb.ToString());
			return listValues;
		}

		///<summary>The text of a quoted value; null for NULL.</summary>
		private static string Unquote(string value) {
			if(value=="NULL") {
				return null;
			}
			Assert.IsTrue(value.Length>=2 && value[0]=='\'' && value[value.Length-1]=='\'',"A quoted value: "+value);
			return Regex.Replace(value.Substring(1,value.Length-2),@"\\(.)",x => x.Groups[1].Value=="n" ? "\n" : x.Groups[1].Value=="r" ? "\r"
				: x.Groups[1].Value=="0" ? "\0" : x.Groups[1].Value);
		}
	}
}
