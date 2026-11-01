// Shared helpers for the M2-TERM tests: repository paths, the files this package owns, the wording table (old text, new text) and the
// source contexts in which a string literal is shown to the user.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace M2TERMTests {
	///<summary>Locates the repository and reads its source files.</summary>
	public static class TestPaths {
		///<summary>Walks up from the test assembly until it finds the repository root (LICENSE and NOTICE).</summary>
		public static string GetRepositoryRoot() {
			string dir=Path.GetDirectoryName(typeof(TestPaths).Assembly.Location);
			while(dir!=null) {
				if(File.Exists(Path.Combine(dir,"LICENSE")) && File.Exists(Path.Combine(dir,"NOTICE"))) {
					return dir;
				}
				dir=Path.GetDirectoryName(dir);
			}
			throw new InvalidOperationException("Could not find the repository root (LICENSE and NOTICE) above the test assembly.");
		}

		///<summary>A file under the repository root, given relative to it with forward slashes, for example "OpenDental/Resources/PrefInfos.xml".
		///</summary>
		public static string GetRepoFile(string relativePath) {
			return Path.Combine(GetRepositoryRoot(),relativePath.Replace('/',Path.DirectorySeparatorChar));
		}

		public static string ReadRepoFile(string relativePath) {
			return File.ReadAllText(GetRepoFile(relativePath));
		}
	}

	///<summary>One wording change: in File, the string literal OldText is gone and NewText is there Count times. IsTail: the texts are the
	///end of a longer (interpolated) literal, like the end of $" ({percent}% of standard provider fee)". ExportKeyCount: report columns whose
	///header changed keep OldText as their export key (the spec keeps report export header keys stable), so OldText stays exactly that many times,
	///and only in the export key statement right after the renamed column (see TermSources.GetReportColumns).</summary>
	public class WordingChange {
		public string File;
		public string OldText;
		public string NewText;
		public int Count;
		public bool IsTail;
		public int ExportKeyCount;

		public WordingChange(string file,string oldText,string newText,int count=1,bool isTail=false,int exportKeyCount=0) {
			File=file;
			OldText=oldText;
			NewText=newText;
			Count=count;
			IsTail=isTail;
			ExportKeyCount=exportKeyCount;
		}

		///<summary>How often the text appears in the source as a literal (or as the end of one, for IsTail).</summary>
		public int CountIn(string source,string text) {
			if(IsTail) {
				return TermSources.CountLiteralEnding(source,text);
			}
			return TermSources.CountLiteral(source,text);
		}

		public override string ToString() {
			return File+": \""+OldText+"\" -> \""+NewText+"\"";
		}
	}

	///<summary>The files M2-TERM owns and the wording it changed in them (docs/port-notes/M2-TERM.md has the same table).</summary>
	public static class TermSources {
		public const string FAMILY_INSURANCE_DESIGNER="OpenDental/User Controls/Preferences/UserControlFamilyInsurance.Designer.cs";
		public const string FAMILY_INSURANCE="OpenDental/User Controls/Preferences/UserControlFamilyInsurance.cs";
		public const string ACCOUNT_CLAIM_SEND_DESIGNER="OpenDental/User Controls/Preferences/UserControlAccountClaimSend.Designer.cs";
		public const string ACCOUNT_CLAIM_SEND="OpenDental/User Controls/Preferences/UserControlAccountClaimSend.cs";
		public const string BLUE_BOOK_RULES="OpenDental/Forms/FormInsBlueBookRules.cs";
		public const string BLUE_BOOK_RULES_DESIGNER="OpenDental/Forms/FormInsBlueBookRules.designer.cs";
		public const string RP_CAPITATION="OpenDental/Forms Reports/FormRpCapitation.cs";
		public const string RP_NET_PROD_DETAIL="OpenDental/Forms Reports/FormRpNetProdDetail.cs";
		public const string RP_PROVIDER_PAYROLL="OpenDental/Forms Reports/FormRpProviderPayroll.cs";
		public const string INS_BLUE_BOOKS="OpenDentBusiness/Data Interface/InsBlueBooks.cs";
		public const string INS_PLAN="OpenDentBusiness/TableTypes/InsPlan.cs";
		public const string RP_PROD_INC="OpenDentBusiness/Reports/RpProdInc.cs";
		public const string PREF_INFOS="OpenDental/Resources/PrefInfos.xml";
		//Not owned by M2-TERM: the Treatment Plan grids that show the "Cat% UCR" display field (headers built with DisplayFields26_1.GetColumnText).
		public const string CONTROL_TREAT="OpenDental/Main Modules/ControlTreat.cs";
		public const string SHEET_DRAWING_JOB="OpenDentBusiness/SheetFramework/SheetDrawingJob.cs";

		public static readonly List<string> ListOwnedSourceFiles=new List<string> {
			FAMILY_INSURANCE_DESIGNER,FAMILY_INSURANCE,ACCOUNT_CLAIM_SEND_DESIGNER,ACCOUNT_CLAIM_SEND,BLUE_BOOK_RULES,BLUE_BOOK_RULES_DESIGNER,
			RP_CAPITATION,RP_NET_PROD_DETAIL,RP_PROVIDER_PAYROLL,INS_BLUE_BOOKS,INS_PLAN,RP_PROD_INC,
		};

		public static readonly List<WordingChange> ListChanges=new List<WordingChange> {
			//Preferences, Family > Insurance.
			new WordingChange(FAMILY_INSURANCE_DESIGNER,"Ins plans with exclusions use UCR fee (zero out write-offs)",
				"Ins plans with exclusions use standard provider fee (no write-offs)"),
			new WordingChange(FAMILY_INSURANCE_DESIGNER,"Ins plans use UCR fee (zero out write-offs) when annual max is met",
				"Ins plans use standard provider fee (no write-offs) when annual max is met"),
			new WordingChange(FAMILY_INSURANCE_DESIGNER,"Ins plans use UCR fee (zero out write-offs) when frequency or age limits are met",
				"Ins plans use standard provider fee (no write-offs) at frequency or age limits"),
			new WordingChange(FAMILY_INSURANCE_DESIGNER,"Insurance plans default to show UCR fee on claims",
				"Insurance plans default to show standard provider fee on claims"),
			new WordingChange(FAMILY_INSURANCE,"Would you like to immediately change all plans to show office UCR fees on claims?",
				"Would you like to immediately change all plans to show the office's standard provider fees on claims?"),
			//Preferences, Account > Claim Send.
			new WordingChange(ACCOUNT_CLAIM_SEND_DESIGNER,"Use UCR fee for billed fee even if PPO fee is higher",
				"Use standard provider fee for billed fee even if PPO fee is higher"),
			//Insurance Blue Book Setup.
			new WordingChange(BLUE_BOOK_RULES,"UCR fee","Standard provider fee"),
			new WordingChange(BLUE_BOOK_RULES,"Please enter a UCR Fee Percent between 0 and 100.",
				"Please enter a Standard Provider Fee Percent between 0 and 100."),
			new WordingChange(BLUE_BOOK_RULES_DESIGNER,"UCR Fee Percent","Standard Provider Fee Percent"),
			//The Blue Book estimate log.
			new WordingChange(INS_BLUE_BOOKS,"Rule Type: UCR Fee","Rule Type: Standard Provider Fee"),
			new WordingChange(INS_BLUE_BOOKS,"% of UCR fee)","% of standard provider fee)",isTail:true),
			//Edit Insurance Plan, Exclusions combo (the enum description).
			new WordingChange(INS_PLAN,"Use UCR Fee","Use Standard Provider Fee"),
			//Report column headers. Space is short there, so "standard fee". The export keeps the old header as its column name.
			new WordingChange(RP_CAPITATION,"UCR Fee","Standard Fee",2,exportKeyCount:2),
			new WordingChange(RP_NET_PROD_DETAIL,"UCR","Standard Fee",exportKeyCount:1),
			new WordingChange(RP_PROVIDER_PAYROLL,"UCR Production","Standard Fee Production",exportKeyCount:1),
			new WordingChange(RP_PROD_INC,"UCR Poduction","Standard Fee Production"),
		};

		///<summary>Whole word UCR, any case. Identifiers such as checkInsPlanExclusionsUseUCR or textUcrFeePercent do not match.</summary>
		public static readonly Regex RegexUcrWord=new Regex(@"\bucr\b",RegexOptions.IgnoreCase);

		///<summary>Source contexts whose string literal is shown to the user: designer Text, message boxes, translations, report column headers,
		///grid cells, enum descriptions and the Blue Book estimate log. Group 1 is the literal's text.</summary>
		private static readonly List<Regex> _listShownContexts=new List<Regex> {
			new Regex(@"\.Text\s*=\s*""((?:[^""\\\r\n]|\\.)*)"""),
			new Regex(@"MsgBox\.Show\(\s*this\s*,\s*(?:MsgBoxButtons\.\w+\s*,\s*)?""((?:[^""\\\r\n]|\\.)*)"""),
			new Regex(@"Lans?\.g\(\s*(?:this|""\w+"")\s*,\s*""((?:[^""\\\r\n]|\\.)*)"""),
			new Regex(@"AddColumn\(\s*""((?:[^""\\\r\n]|\\.)*)"""),
			new Regex(@"new\s+DataColumn\(\s*""((?:[^""\\\r\n]|\\.)*)"""),
			new Regex(@"new\s+GridColumn\(\s*""((?:[^""\\\r\n]|\\.)*)"""),
			new Regex(@"Cells\.Add\(\s*""((?:[^""\\\r\n]|\\.)*)"""),
			new Regex(@"\[Description\(\s*""((?:[^""\\\r\n]|\\.)*)""\s*\)\]"),
			new Regex(@"LogText\s*\+?=\s*\$?""((?:[^""\\\r\n]|\\.)*)"""),
			new Regex(@"MessageBox\.Show\(\s*""((?:[^""\\\r\n]|\\.)*)"""),
		};

		///<summary>Every shown literal in the source text, in order of appearance.</summary>
		public static List<string> GetShownLiterals(string source) {
			List<(int Index,string Text)> listFound=new List<(int,string)>();
			foreach(Regex regex in _listShownContexts) {
				foreach(Match match in regex.Matches(source)) {
					listFound.Add((match.Groups[1].Index,match.Groups[1].Value));
				}
			}
			return listFound.OrderBy(x => x.Index).Select(x => x.Text).Distinct().ToList();
		}

		///<summary>The headers of the report columns added in the source, in order, from query.AddColumn("header",...).</summary>
		public static List<string> GetAddColumnHeaders(string source) {
			return Regex.Matches(source,@"AddColumn\(\s*""((?:[^""\\\r\n]|\\.)*)""").Cast<Match>().Select(x => x.Groups[1].Value).ToList();
		}

		///<summary>query.AddColumn("header",width,FieldValueType.Type,...); optionally followed, as the very next statement, by the one that renames
		///the export column it just added: query.ExportTable.Columns[query.ExportTable.Columns.Count-1].ColumnName="key";</summary>
		private static readonly Regex _regexReportColumn=new Regex(
			@"AddColumn\(\s*""(?<header>(?:[^""\\\r\n]|\\.)*)""\s*,\s*(?<width>\d+)\s*,\s*FieldValueType\.(?<type>\w+)[^;]*;"
			+@"(?:[ \t]*(?://[^\r\n]*)?\s*query\.ExportTable\.Columns\[\s*query\.ExportTable\.Columns\.Count\s*-\s*1\s*\]\.ColumnName\s*=\s*"
			+@"""(?<key>(?:[^""\\\r\n]|\\.)*)""\s*;)?");

		///<summary>Any statement that sets the name of an export column, wherever it is.</summary>
		private static readonly Regex _regexExportKeyStatement=new Regex(@"\.ExportTable\.Columns\[[^\]]*\]\.ColumnName\s*=");

		///<summary>The report columns added in the source, in order of appearance (both branches of an if-else included).</summary>
		public static List<ReportColumn> GetReportColumns(string source) {
			return _regexReportColumn.Matches(source).Cast<Match>().Select(x => new ReportColumn {
				Header=x.Groups["header"].Value,
				Width=int.Parse(x.Groups["width"].Value),
				TypeName=x.Groups["type"].Value,
				HasExportKeyStatement=x.Groups["key"].Success,
				ExportKey=x.Groups["key"].Success ? x.Groups["key"].Value : x.Groups["header"].Value,
			}).ToList();
		}

		///<summary>How many statements in the source set the name of an export column.</summary>
		public static int CountExportKeyStatements(string source) {
			return _regexExportKeyStatement.Matches(source).Count;
		}

		///<summary>How often the exact string literal "text" appears in the source.</summary>
		public static int CountLiteral(string source,string text) {
			string literal="\""+text.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";
			int count=0;
			int index=source.IndexOf(literal,StringComparison.Ordinal);
			while(index>=0) {
				count++;
				index=source.IndexOf(literal,index+literal.Length,StringComparison.Ordinal);
			}
			return count;
		}

		///<summary>Counts a literal that may be the start of an interpolated or concatenated string, like $" ({...}% of UCR fee)": the text must
		///be followed by a closing quote.</summary>
		public static int CountLiteralEnding(string source,string text) {
			string ending=text.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";
			int count=0;
			int index=source.IndexOf(ending,StringComparison.Ordinal);
			while(index>=0) {
				count++;
				index=source.IndexOf(ending,index+ending.Length,StringComparison.Ordinal);
			}
			return count;
		}
	}

	///<summary>A report column as a form adds it with QueryObject.AddColumn. Header is the text on screen and in print. ExportKey is the name of
	///the export table's column, which the report window's Export writes as the file's header line: the header, unless the statement after the
	///AddColumn renames the export column (HasExportKeyStatement).</summary>
	public class ReportColumn {
		public string Header;
		public int Width;
		public string TypeName;
		public string ExportKey;
		public bool HasExportKeyStatement;

		public override string ToString() {
			return "\""+Header+"\" (export key \""+ExportKey+"\")";
		}
	}

	///<summary>A control's Size and Text as its designer file sets them.</summary>
	public class DesignerControl {
		public string Name;
		public int X;
		public int Y;
		public int Width;
		public int Height;
		public string Text;

		///<summary>Reads this.[name].Location, .Size and .Text from a designer file. Fails the test when one is missing.</summary>
		public static DesignerControl Read(string designerSource,string name) {
			DesignerControl control=new DesignerControl { Name=name };
			string prefix=@"this\."+Regex.Escape(name)+@"\.";
			Match matchLocation=Regex.Match(designerSource,prefix+@"Location\s*=\s*new System\.Drawing\.Point\((\d+),\s*(\d+)\)");
			Match matchSize=Regex.Match(designerSource,prefix+@"Size\s*=\s*new System\.Drawing\.Size\((\d+),\s*(\d+)\)");
			Match matchText=Regex.Match(designerSource,prefix+@"Text\s*=\s*""((?:[^""\\\r\n]|\\.)*)""");
			if(!matchLocation.Success || !matchSize.Success || !matchText.Success) {
				throw new InvalidOperationException("The designer does not set Location, Size and Text of "+name+".");
			}
			control.X=int.Parse(matchLocation.Groups[1].Value);
			control.Y=int.Parse(matchLocation.Groups[2].Value);
			control.Width=int.Parse(matchSize.Groups[1].Value);
			control.Height=int.Parse(matchSize.Groups[2].Value);
			control.Text=Regex.Unescape(matchText.Groups[1].Value);
			return control;
		}
	}
}
