<#
.SYNOPSIS
	Renames the internal code names to Open PMS Dental. Re-runnable and idempotent, so it can be run again on a branch that still has the
	old names (a straggling branch made before the rename).

.DESCRIPTION
	The mapping (owner decisions):

		OpenDental        -> OpenPmsDental            (OpenDental.exe -> OpenPmsDental.exe; the OpenDental namespace everywhere it is declared)
		OpenDentBusiness  -> OpenPmsDentalBusiness
		OpenDentalGraph   -> OpenPmsDentalGraph
		OpenDentalCloud   -> OpenPmsDentalCloud
		OpenDentalServer  -> OpenPmsDentalServer
		OpenDentHL7       -> OpenPmsDentalHL7

	Each new name is one word, because the Middle Tier needs every root namespace to equal its assembly name.

	What it changes, by category:
	- Moves (git mv, so history follows): the six project folders and their project files. On a straggling branch, files that are still
	  in an old folder are moved one by one into the new one.
	- C# code: whole-word identifier tokens in code, so namespace declarations, using directives and fully qualified references. A small
	  C# lexer finds the code: string literals, character literals, comments and preprocessor lines are never touched by this pass. Tokens
	  after a dot (enum members such as AccountingSoftware.OpenDental, the OpenDentalServer web reference) and every member of an enum body
	  are kept, because enum names are stored in the database. Longer identifiers such as FormOpenDental, OpenDentalVendor or
	  OpenDentBusiness_OpenDentalServer_ServiceMain are not whole-word tokens and are never touched.
	- C# string literals: only the explicit list in $StringRules below (ResourceManager names in the designers, AssemblyTitle, the CrudTable
	  overrides, xCrudGenerator's paths and namespaces, the XAML namespace strings of WpfConverter, the ProgramNames values).
	- XAML: x:Class, clr-namespace:, assembly= and an xmlns prefix named after the namespace.
	- Project files: AssemblyName, RootNamespace, the Name of a ProjectReference, StartupObject, the assembly name of a Reference, the first
	  folder of a relative path (..\OpenDental\...) and project, dll, exe, pdb and xml file names in paths. XML comments are kept.
	- Solutions: the project entries (names and paths) of every .sln and .slnf. The OpenDental.sln and OpenDental.Local.slnf file names stay.
	- app.config section names, Settings.settings namespaces, ServiceMain.asmx Class=, data source type names, class diagram type names,
	  the old Middle Tier publish record, relative paths in batch files, and the project folder in the repository paths of the tools
	  (Test-NoHqHosts.ps1's allow-list, New-DevDatabase.ps1, New-OdReferenceDatabase.ps1).

	Kept on purpose: everything else, in particular text, comments and docs (a later step), string literals outside the list, enum
	members, preference names and values, the database, OpenDental.sln and OpenDental.Local.slnf as file names, FreeDentalConfig.xml and
	OpenDentalServerConfig.xml, the Middle Tier SOAP namespace, IIS path, web reference name and Credential Manager prefix, and values
	sent to vendors.

	It prints the number of changes per category, and the old names it left in C# strings, comments and enum members (for review).

	It refuses to run on a dirty tree (uncommitted or untracked files), so its result is one reviewable commit. -DryRun changes nothing and
	may run on a dirty tree.

	Exit code 0: done (or nothing left to do). 1: the tree is dirty. 2: the script could not run.

.PARAMETER Root
	Repository root. Defaults to the parent of the folder that contains this script.

.PARAMETER DryRun
	Count what would change and print it, but change and move nothing.

.PARAMETER ReportFile
	Write the changes per file, and every old name left in a C# string literal, comment, enum member or other kept place, to this file.

.EXAMPLE
	powershell -NoProfile -ExecutionPolicy Bypass -File tools\Rename-ToOpenPmsDental.ps1 -DryRun

.EXAMPLE
	powershell -NoProfile -ExecutionPolicy Bypass -File tools\Rename-ToOpenPmsDental.ps1 -ReportFile $env:TEMP\rename-report.txt
#>
[CmdletBinding()]
param(
	[string]$Root='',
	[switch]$DryRun,
	[string]$ReportFile=''
)

Set-StrictMode -Version 2.0
$ErrorActionPreference='Stop'

#Old name -> new name. Also the folder and project file names.
$NameMap=[ordered]@{
	'OpenDental'='OpenPmsDental'
	'OpenDentBusiness'='OpenPmsDentalBusiness'
	'OpenDentalGraph'='OpenPmsDentalGraph'
	'OpenDentalCloud'='OpenPmsDentalCloud'
	'OpenDentalServer'='OpenPmsDentalServer'
	'OpenDentHL7'='OpenPmsDentalHL7'
}
$OldAlt='(?:'+(($NameMap.Keys | ForEach-Object { [regex]::Escape($_) }) -join '|')+')'
#A whole-word old name: not inside a longer identifier.
$W0='(?<![A-Za-z0-9_\u0080-\uFFFF])'
$W1='(?![A-Za-z0-9_\u0080-\uFFFF])'
$Old="$W0(?<old>$OldAlt)$W1"

#Explicit C# string literals that change. Path: wildcard on the repository relative path after the move (backslashes). Regex: the old
#names in its "old" captures are renamed by the map, or its "v" captures are replaced by Value. A capture changes only when it lies inside a
#string literal (code is the code pass's job).
$StringRules=@(
	@{ Category='C# strings: ResourceManager names (designers)'; Path='*.cs'; Regex="ResourceManager\(\s*@?`"$Old\." }
	@{ Category='C# strings: AssemblyTitle'; Path='*\Properties\AssemblyInfo.cs'; Regex="AssemblyTitle\(\s*`"$Old`"\s*\)" }
	@{ Category='C# strings: AssemblyTitle'; Path='*\Properties\AssemblyInfo.cs'; Regex='AssemblyTitle\(\s*"(?<v>OpenDentalHL7)"\s*\)'; Value='OpenPmsDentalHL7' }
	@{ Category='C# strings: CrudTable overrides'; Path='*.cs'; Regex="CrudLocationOverride\s*=\s*@`"(?:\.\.\\)+$Old\\" }
	@{ Category='C# strings: CrudTable overrides'; Path='*.cs'; Regex="NamespaceOverride\s*=\s*`"$Old\." }
	#xCrudGenerator writes into the business layer's folder and namespace: every old name in its string literals is a path or a namespace.
	@{ Category='C# strings: xCrudGenerator paths and namespaces'; Path='xCrudGenerator\*.cs'; Regex=$Old }
	#WpfConverter (a developer tool in the exe) writes XAML and code behind for converted windows, in the program's namespaces, and reads
	#the window's source from the exe's project folder. Its default repository root ("...\Versioned\OpenDental\") is a developer's checkout
	#folder, not a project folder, and stays.
	@{ Category='C# strings: WpfConverter namespaces and paths'; Path='*\WpfConverter.cs'; Regex="(?<!Versioned\\\\)$Old" }
	#The program's run-time names (CodeBase\Utilities\ProgramNames.cs). LegacyLocalAppDataFolderName stays "OpenDental" and
	#HL7ServiceFallbackName stays "OpenDentalHL7" (see the notes in that file).
	@{ Category='C# strings: ProgramNames values'; Path='CodeBase\Utilities\ProgramNames.cs'; Regex='\bProcessName="(?<v>OpenDental)"'; Value='OpenPmsDental' }
	@{ Category='C# strings: ProgramNames values'; Path='CodeBase\Utilities\ProgramNames.cs'; Regex='\bBusinessDllFileName="(?<v>OpenDentBusiness\.dll)"'; Value='OpenPmsDentalBusiness.dll' }
	@{ Category='C# strings: ProgramNames values'; Path='CodeBase\Utilities\ProgramNames.cs'; Regex='\bGraphDllFileName="(?<v>OpenDentalGraph\.dll)"'; Value='OpenPmsDentalGraph.dll' }
	@{ Category='C# strings: ProgramNames values'; Path='CodeBase\Utilities\ProgramNames.cs'; Regex='\bCloudDllFileName="(?<v>OpenDentalCloud\.dll)"'; Value='OpenPmsDentalCloud.dll' }
	@{ Category='C# strings: ProgramNames values'; Path='CodeBase\Utilities\ProgramNames.cs'; Regex='\bLocalAppDataFolderName="(?<v>OpenDental)"'; Value='OpenPMS-Dental' }
	@{ Category='C# strings: ProgramNames values'; Path='CodeBase\Utilities\ProgramNames.cs'; Regex='\bProgramDataFolderName="(?<v>OpenDental)"'; Value='OpenPMS-Dental' }
	@{ Category='C# strings: ProgramNames values'; Path='CodeBase\Utilities\ProgramNames.cs'; Regex='\bTempFolderName="(?<v>opendental)"'; Value='openpms-dental' }
	@{ Category='C# strings: ProgramNames values'; Path='CodeBase\Utilities\ProgramNames.cs'; Regex='\bEventLogSource="(?<v>OpenDental)"'; Value='OpenPMS-Dental' }
	@{ Category='C# strings: ProgramNames values'; Path='CodeBase\Utilities\ProgramNames.cs'; Regex='\bHL7ServiceName="(?<v>OpenDentHL7)"'; Value='OpenPmsDentalHL7' }
	@{ Category='C# strings: ProgramNames values'; Path='CodeBase\Utilities\ProgramNames.cs'; Regex='\bServicePrefix="(?<v>OpenDent)"'; Value='OpenPmsDental' }
	@{ Category='C# strings: ProgramNames values'; Path='CodeBase\Utilities\ProgramNames.cs'; Regex='\bDefaultInstallFolder=@"(?<v>C:\\Program Files\\Open Dental)"'; Value='C:\Program Files (x86)\Open PMS - Dental' }
)

#Rules for the other files. Path: wildcard on the relative path after the move. Regex: its "old" captures are renamed by the map.
#XmlComments: captures inside <!-- --> are kept.
$TextRules=@(
	#--- Project files.
	@{ Category='Projects: AssemblyName, RootNamespace, Name, StartupObject'; Path='*.*proj'; XmlComments=$true; Regex="<(?:AssemblyName|RootNamespace|Name|StartupObject)>\s*$Old" }
	@{ Category='Projects: assembly name of a Reference'; Path='*.*proj'; XmlComments=$true; Regex="<Reference\s+Include=`"$Old(?=[,`"])" }
	@{ Category='Projects: relative paths (folder)'; Path='*.*proj'; XmlComments=$true; Regex="(?<=\.\.[\\/])$Old(?=[\\/])" }
	@{ Category='Projects: file names in paths'; Path='*.*proj'; XmlComments=$true; Regex="(?<=[\\/])$Old(?=\.(?:csproj|dll|exe|pdb|xml)\b)" }
	#--- Solutions and solution filters: the project entries.
	@{ Category='Solutions: project entries'; Path='*.sln'; Regex="(?m)^Project\(`"\{[^}]+\}`"\)\s*=\s*`"$Old`"" }
	@{ Category='Solutions: project entries'; Path='*.sln'; Regex="(?m)^Project\(`"\{[^}]+\}`"\)\s*=\s*`"[^`"]*`",\s*`"(?:\.\.\\)*$Old\\" }
	@{ Category='Solutions: project entries'; Path='*.sln'; Regex="(?m)^Project\(`"\{[^}]+\}`"\)\s*=\s*`"[^`"]*`",\s*`"[^`"]*\\$Old\.(?:cs|vcx)proj`"" }
	@{ Category='Solutions: project entries'; Path='*.slnf'; Regex="`"$Old\\\\" }
	@{ Category='Solutions: project entries'; Path='*.slnf'; Regex="\\\\$Old\.csproj`"" }
	#--- XAML.
	@{ Category='XAML: x:Class'; Path='*.xaml'; XmlComments=$true; Regex="x:Class=`"$Old\." }
	@{ Category='XAML: clr-namespace and assembly='; Path='*.xaml'; XmlComments=$true; Regex="clr-namespace:$Old" }
	@{ Category='XAML: clr-namespace and assembly='; Path='*.xaml'; XmlComments=$true; Regex="assembly=$Old" }
	@{ Category='XAML: xmlns prefix'; Path='*.xaml'; XmlComments=$true; Regex="xmlns:$Old(?==)" }
	@{ Category='XAML: xmlns prefix'; Path='*.xaml'; XmlComments=$true; Regex="(?<=</?|\{)$Old(?=:)" }
	#--- Configuration.
	@{ Category='app.config section names'; Path='*.config'; XmlComments=$true; Regex="<section\s+name=`"$Old\.Properties\.Settings`"" }
	@{ Category='app.config section names'; Path='*.config'; XmlComments=$true; Regex="</?$Old\.Properties\.Settings>" }
	@{ Category='Settings.settings namespaces'; Path='*.settings'; XmlComments=$true; Regex="GeneratedClassNamespace=`"$Old\." }
	@{ Category='ServiceMain.asmx Class='; Path='*.asmx'; Regex="\bClass=`"$Old\." }
	#--- Design-time files.
	@{ Category='Data source type names'; Path='*.datasource'; XmlComments=$true; Regex="<TypeInfo>$Old\." }
	@{ Category='Class diagram type names'; Path='*.cd'; XmlComments=$true; Regex="\bName=`"$Old\." }
	@{ Category='Middle Tier publish record (file names)'; Path='*\*.Publish.xml'; XmlComments=$true; Regex="relUrl=`"bin/$Old\." }
	@{ Category='Batch files: relative paths (folder)'; Path='*.bat'; Regex="(?<=\.\.[\\/])$Old(?=[\\/`"]|$)" }
	@{ Category='Batch files: relative paths (folder)'; Path='*.cmd'; Regex="(?<=\.\.[\\/])$Old(?=[\\/`"]|$)" }
	#--- The repository's PowerShell tools: the project folder at the start of an allow-list path (Test-NoHqHosts.ps1) and of a path joined
	#    to the repository root (New-DevDatabase.ps1, New-OdReferenceDatabase.ps1). Their comments and messages are kept.
	@{ Category='Tools: project folders in repository paths'; Path='tools\*.ps1'; Regex="(?<=\bPath='|\bPath=`")$Old(?=\\)" }
	@{ Category='Tools: project folders in repository paths'; Path='tools\*.ps1'; Regex="(?<=Join-Path\s+\`$repoRoot\s+['`"])$Old(?=\\)" }
)

#Never edited: this script, the leftover-name scanner, and the history the scanner allows as whole files.
$SkipPaths=@('tools\Rename-ToOpenPmsDental.ps1','tools\Test-NoOldNames.ps1')

$EngineSource=@'
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

public class RenRule {
	public string Category;
	public Regex Regex;
	///<summary>Null: each "old" capture is renamed by the map. Otherwise each "v" capture is replaced by this text.</summary>
	public string Value;
	public bool XmlComments;
}

public class RenEdit {
	public int Index;
	public int Length;
	public string NewText;
	public string Category;
}

public class RenLeftover {
	public int Line;
	public string Kind;
	public string Name;
	public string Text;
}

public class RenResult {
	public string Text;
	public bool Changed;
	public Dictionary<string,int> Counts=new Dictionary<string,int>();
	public List<RenLeftover> Leftovers=new List<RenLeftover>();
}

public static class OpmsRename {
	static readonly string[] _arrayOld={"OpenDental","OpenDentBusiness","OpenDentalGraph","OpenDentalCloud","OpenDentalServer","OpenDentHL7"};
	static readonly string[] _arrayNew={"OpenPmsDental","OpenPmsDentalBusiness","OpenPmsDentalGraph","OpenPmsDentalCloud","OpenPmsDentalServer","OpenPmsDentalHL7"};
	public static readonly Regex RegexOld=new Regex(@"(?<![A-Za-z0-9_\u0080-\uFFFF])(?:OpenDental|OpenDentBusiness|OpenDentalGraph|OpenDentalCloud|OpenDentalServer|OpenDentHL7)(?![A-Za-z0-9_\u0080-\uFFFF])");

	public static string Map(string name) {
		int i=Array.IndexOf(_arrayOld,name);
		return i<0 ? null : _arrayNew[i];
	}

	public static bool IsWordChar(char c) {
		return c=='_' || (c>='0' && c<='9') || (c>='A' && c<='Z') || (c>='a' && c<='z') || c>=0x80;
	}

	public static bool IsIdentStart(char c) {
		return c=='_' || (c>='A' && c<='Z') || (c>='a' && c<='z') || c>=0x80;
	}

	public const byte KindCode=0;
	public const byte KindComment=1;
	public const byte KindString=2;
	public const byte KindChar=3;
	public const byte KindPreproc=4;

	public class CsToken {
		public int Index;
		public int Length;
		public string Name;
		public bool AfterDot;
		public bool InEnum;
		public string StatementFirstWord;
		public int ParenDepth;
		public char NextSig;
	}

	///<summary>A small C# lexer: the kind of every character (code, comment, string, character literal, preprocessor line) and every old
	///name in code with its context. Handles regular, verbatim and interpolated strings (the holes of an interpolated string are code).</summary>
	public class CsLex {
		public byte[] Kind;
		public List<CsToken> Tokens=new List<CsToken>();
		readonly string _s;
		readonly int _n;
		readonly Stack<bool> _stackBraceIsEnum=new Stack<bool>();
		bool _isEnumPending;
		char _lastSig=' ';
		string _statementFirstWord;
		int _parenDepth;

		public CsLex(string text) {
			_s=text;
			_n=text.Length;
			Kind=new byte[_n];
			int i=0;
			LexCode(ref i,false);
			foreach(CsToken token in Tokens) {
				int j=token.Index+token.Length;
				while(j<_n && (char.IsWhiteSpace(_s[j]) || Kind[j]==KindComment)) {
					j++;
				}
				token.NextSig=j<_n ? _s[j] : '\0';
			}
		}

		void Mark(int from,int to,byte kind) {
			for(int x=Math.Max(0,from);x<to && x<_n;x++) {
				Kind[x]=kind;
			}
		}

		bool IsAtLineStart(int i) {
			int j=i-1;
			while(j>=0 && (_s[j]==' ' || _s[j]=='\t')) {
				j--;
			}
			return j<0 || _s[j]=='\n';
		}

		char At(int i) {
			return i<_n ? _s[i] : '\0';
		}

		void LexCode(ref int i,bool isHole) {
			int depth=0;
			while(i<_n) {
				char c=_s[i];
				char c1=At(i+1);
				char c2=At(i+2);
				if(isHole && depth==0) {
					if(c=='}') {
						return;
					}
					if(c==':' && c1!=':' && (i==0 || _s[i-1]!=':')) {
						return;//the format part of an interpolation hole
					}
				}
				if(c=='/' && c1=='/') {
					int start=i;
					while(i<_n && _s[i]!='\n') {
						i++;
					}
					Mark(start,i,KindComment);
					continue;
				}
				if(c=='/' && c1=='*') {
					int start=i;
					int end=_s.IndexOf("*/",i+2,StringComparison.Ordinal);
					i=end<0 ? _n : end+2;
					Mark(start,i,KindComment);
					continue;
				}
				if(c=='#' && !isHole && IsAtLineStart(i)) {
					int start=i;
					while(i<_n && _s[i]!='\n') {
						i++;
					}
					Mark(start,i,KindPreproc);
					continue;
				}
				if(c=='"') {
					LexString(ref i);
					_lastSig='"';
					continue;
				}
				if(c=='@' && c1=='"') {
					LexVerbatim(ref i);
					_lastSig='"';
					continue;
				}
				if(c=='$' && c1=='"') {
					LexInterpolated(ref i,2,false);
					_lastSig='"';
					continue;
				}
				if((c=='$' && c1=='@' && c2=='"') || (c=='@' && c1=='$' && c2=='"')) {
					LexInterpolated(ref i,3,true);
					_lastSig='"';
					continue;
				}
				if(c=='\'') {
					int start=i;
					i++;
					while(i<_n && _s[i]!='\'' && _s[i]!='\n') {
						if(_s[i]=='\\') {
							i++;
						}
						i++;
					}
					if(i<_n && _s[i]=='\'') {
						i++;
					}
					Mark(start,i,KindChar);
					_lastSig='\'';
					continue;
				}
				if(IsIdentStart(c) || (c=='@' && IsIdentStart(c1))) {
					if(c=='@') {
						i++;
					}
					int start=i;
					while(i<_n && IsWordChar(_s[i])) {
						i++;
					}
					string word=_s.Substring(start,i-start);
					if(Map(word)!=null) {
						CsToken token=new CsToken();
						token.Index=start;
						token.Length=i-start;
						token.Name=word;
						token.AfterDot=_lastSig=='.';
						token.InEnum=_stackBraceIsEnum.Count>0 && _stackBraceIsEnum.Peek();
						token.StatementFirstWord=_statementFirstWord;
						token.ParenDepth=_parenDepth;
						Tokens.Add(token);
					}
					if(word=="enum" && _lastSig!='.') {
						_isEnumPending=true;
					}
					if(_statementFirstWord==null) {
						_statementFirstWord=word;
					}
					_lastSig='a';
					continue;
				}
				if(c>='0' && c<='9') {
					while(i<_n && (IsWordChar(_s[i]) || (_s[i]=='.' && At(i+1)>='0' && At(i+1)<='9'))) {
						i++;
					}
					_lastSig='0';
					continue;
				}
				if(c=='{') {
					_stackBraceIsEnum.Push(_isEnumPending);
					_isEnumPending=false;
					_statementFirstWord=null;
					if(isHole) {
						depth++;
					}
				}
				else if(c=='}') {
					if(_stackBraceIsEnum.Count>0) {
						_stackBraceIsEnum.Pop();
					}
					_statementFirstWord=null;
					if(isHole) {
						depth--;
					}
				}
				else if(c==';') {
					_isEnumPending=false;
					_statementFirstWord=null;
				}
				else if(c=='(' || c=='[') {
					_parenDepth++;
					if(isHole) {
						depth++;
					}
				}
				else if(c==')' || c==']') {
					_parenDepth--;
					if(isHole) {
						depth--;
					}
				}
				if(!char.IsWhiteSpace(c)) {
					_lastSig=c;
				}
				i++;
			}
		}

		void LexString(ref int i) {
			int start=i;
			i++;
			while(i<_n) {
				char c=_s[i];
				if(c=='\\') {
					i+=2;
					continue;
				}
				if(c=='"') {
					i++;
					break;
				}
				if(c=='\n') {
					break;
				}
				i++;
			}
			Mark(start,i,KindString);
		}

		void LexVerbatim(ref int i) {
			int start=i;
			i+=2;
			while(i<_n) {
				if(_s[i]=='"') {
					if(At(i+1)=='"') {
						i+=2;
						continue;
					}
					i++;
					break;
				}
				i++;
			}
			Mark(start,i,KindString);
		}

		void LexInterpolated(ref int i,int lengthPrefix,bool isVerbatim) {
			int start=i;
			i+=lengthPrefix;
			while(i<_n) {
				char c=_s[i];
				char c1=At(i+1);
				if(!isVerbatim && c=='\\') {
					i+=2;
					continue;
				}
				if(isVerbatim && c=='"' && c1=='"') {
					i+=2;
					continue;
				}
				if(c=='"') {
					i++;
					break;
				}
				if(!isVerbatim && c=='\n') {
					break;
				}
				if((c=='{' && c1=='{') || (c=='}' && c1=='}')) {
					i+=2;
					continue;
				}
				if(c=='{') {
					i++;
					Mark(start,i,KindString);
					LexCode(ref i,true);
					if(i<_n && _s[i]==':') {
						while(i<_n && _s[i]!='}' && _s[i]!='"' && _s[i]!='\n') {
							i++;
						}
					}
					start=i;
					if(i<_n && _s[i]=='}') {
						i++;
					}
					continue;
				}
				i++;
			}
			Mark(start,i,KindString);
		}
	}

	static void Count(RenResult result,string category) {
		int count;
		result.Counts.TryGetValue(category,out count);
		result.Counts[category]=count+1;
	}

	static int[] LineStarts(string text) {
		List<int> listStarts=new List<int>();
		listStarts.Add(0);
		for(int i=0;i<text.Length;i++) {
			if(text[i]=='\n') {
				listStarts.Add(i+1);
			}
		}
		return listStarts.ToArray();
	}

	static void AddLeftover(RenResult result,string text,int[] arrayLineStarts,int index,string kind,string name) {
		int line=Array.BinarySearch(arrayLineStarts,index);
		if(line<0) {
			line=~line-1;
		}
		int start=arrayLineStarts[line];
		int end=text.IndexOf('\n',start);
		if(end<0) {
			end=text.Length;
		}
		RenLeftover leftover=new RenLeftover();
		leftover.Line=line+1;
		leftover.Kind=kind;
		leftover.Name=name;
		leftover.Text=text.Substring(start,end-start).Trim();
		result.Leftovers.Add(leftover);
	}

	///<summary>Applies the edits (they never overlap; the first one at an index wins) and returns the new text.</summary>
	static string Apply(string text,SortedDictionary<int,RenEdit> dictEdits,RenResult result) {
		if(dictEdits.Count==0) {
			return text;
		}
		StringBuilder stringBuilder=new StringBuilder(text.Length+dictEdits.Count*8);
		int pos=0;
		foreach(RenEdit edit in dictEdits.Values) {
			if(edit.Index<pos) {
				continue;
			}
			stringBuilder.Append(text,pos,edit.Index-pos);
			stringBuilder.Append(edit.NewText);
			pos=edit.Index+edit.Length;
			Count(result,edit.Category);
		}
		stringBuilder.Append(text,pos,text.Length-pos);
		result.Changed=true;
		return stringBuilder.ToString();
	}

	///<summary>Adds the edits of one rule. isAllowed decides whether a capture at an index may change.</summary>
	static void AddRuleEdits(string text,RenRule rule,SortedDictionary<int,RenEdit> dictEdits,Func<int,bool> isAllowed) {
		string group=rule.Value==null ? "old" : "v";
		foreach(Match match in rule.Regex.Matches(text)) {
			foreach(Capture capture in match.Groups[group].Captures) {
				if(dictEdits.ContainsKey(capture.Index) || !isAllowed(capture.Index)) {
					continue;
				}
				string newText=rule.Value ?? Map(capture.Value);
				if(newText==null || newText==capture.Value) {
					continue;
				}
				RenEdit edit=new RenEdit();
				edit.Index=capture.Index;
				edit.Length=capture.Length;
				edit.NewText=newText;
				edit.Category=rule.Category;
				dictEdits[capture.Index]=edit;
			}
		}
	}

	///<summary>A C# file: the old names in code (see CsLex), then the string rules on string literals. Lists the old names left.</summary>
	public static RenResult ProcessCSharp(string text,RenRule[] arrayStringRules) {
		RenResult result=new RenResult();
		CsLex lex=new CsLex(text);
		SortedDictionary<int,RenEdit> dictEdits=new SortedDictionary<int,RenEdit>();
		Dictionary<int,CsToken> dictTokens=new Dictionary<int,CsToken>();
		foreach(CsToken token in lex.Tokens) {
			dictTokens[token.Index]=token;
			if(token.InEnum || token.AfterDot) {
				continue;
			}
			string category;
			if(token.StatementFirstWord=="namespace") {
				category="C# code: namespace declarations";
			}
			else if(token.StatementFirstWord=="using" && token.ParenDepth==0) {
				category="C# code: using directives";
			}
			else if(token.NextSig=='.' || token.NextSig==':') {
				category="C# code: qualified references";
			}
			else {
				category="C# code: other identifiers";
			}
			RenEdit edit=new RenEdit();
			edit.Index=token.Index;
			edit.Length=token.Length;
			edit.NewText=Map(token.Name);
			edit.Category=category;
			dictEdits[token.Index]=edit;
		}
		foreach(RenRule rule in arrayStringRules) {
			AddRuleEdits(text,rule,dictEdits,index => lex.Kind[index]==KindString);
		}
		int[] arrayLineStarts=LineStarts(text);
		foreach(RenEdit edit in dictEdits.Values) {
			if(edit.Category=="C# code: other identifiers") {
				AddLeftover(result,text,arrayLineStarts,edit.Index,"changed: other identifier (review)",text.Substring(edit.Index,edit.Length));
			}
		}
		foreach(Match match in RegexOld.Matches(text)) {
			if(dictEdits.ContainsKey(match.Index)) {
				continue;
			}
			string kind;
			byte kindChar=lex.Kind[match.Index];
			CsToken token;
			if(kindChar==KindString || kindChar==KindChar) {
				kind="kept: string literal";
			}
			else if(kindChar==KindComment) {
				kind="kept: comment";
			}
			else if(kindChar==KindPreproc) {
				kind="kept: preprocessor line";
			}
			else if(dictTokens.TryGetValue(match.Index,out token) && token.InEnum) {
				kind="kept: enum member";
			}
			else if(dictTokens.TryGetValue(match.Index,out token) && token.AfterDot) {
				kind="kept: after a dot";
			}
			else {
				kind="kept: code (not an identifier)";
			}
			AddLeftover(result,text,arrayLineStarts,match.Index,kind,match.Value);
		}
		result.Text=Apply(text,dictEdits,result);
		return result;
	}

	///<summary>Any other text file: the rules, never inside XML comments when a rule says so. Lists the old names left.</summary>
	public static RenResult ProcessText(string text,RenRule[] arrayRules) {
		RenResult result=new RenResult();
		bool[] arrayInComment=null;
		foreach(RenRule rule in arrayRules) {
			if(rule.XmlComments && arrayInComment==null) {
				arrayInComment=new bool[text.Length];
				int pos=0;
				while(true) {
					int start=text.IndexOf("<!--",pos,StringComparison.Ordinal);
					if(start<0) {
						break;
					}
					int end=text.IndexOf("-->",start+4,StringComparison.Ordinal);
					end=end<0 ? text.Length : end+3;
					for(int x=start;x<end;x++) {
						arrayInComment[x]=true;
					}
					pos=end;
				}
			}
		}
		SortedDictionary<int,RenEdit> dictEdits=new SortedDictionary<int,RenEdit>();
		foreach(RenRule rule in arrayRules) {
			bool[] arrayComment=rule.XmlComments ? arrayInComment : null;
			AddRuleEdits(text,rule,dictEdits,index => arrayComment==null || !arrayComment[index]);
		}
		int[] arrayLineStarts=LineStarts(text);
		foreach(Match match in RegexOld.Matches(text)) {
			if(dictEdits.ContainsKey(match.Index)) {
				continue;
			}
			string kind=(arrayInComment!=null && arrayInComment[match.Index]) ? "kept: XML comment" : "kept: text";
			AddLeftover(result,text,arrayLineStarts,match.Index,kind,match.Value);
		}
		result.Text=Apply(text,dictEdits,result);
		return result;
	}

	///<summary>Reads a file without changing any byte it does not edit: UTF-16 by its byte order mark, everything else one byte per
	///character (ISO-8859-1), so UTF-8 and ANSI files both come back byte for byte. The byte order mark is returned apart.</summary>
	public static string Read(string path,out Encoding encoding,out byte[] preamble) {
		byte[] bytes=System.IO.File.ReadAllBytes(path);
		if(bytes.Length>=2 && bytes[0]==0xFF && bytes[1]==0xFE) {
			encoding=new UnicodeEncoding(false,false);
			preamble=new byte[] { 0xFF,0xFE };
		}
		else if(bytes.Length>=2 && bytes[0]==0xFE && bytes[1]==0xFF) {
			encoding=new UnicodeEncoding(true,false);
			preamble=new byte[] { 0xFE,0xFF };
		}
		else if(bytes.Length>=3 && bytes[0]==0xEF && bytes[1]==0xBB && bytes[2]==0xBF) {
			encoding=Encoding.GetEncoding(28591);
			preamble=new byte[] { 0xEF,0xBB,0xBF };
		}
		else {
			encoding=Encoding.GetEncoding(28591);
			preamble=new byte[0];
		}
		return encoding.GetString(bytes,preamble.Length,bytes.Length-preamble.Length);
	}

	public static void Write(string path,string text,Encoding encoding,byte[] preamble) {
		byte[] bytesText=encoding.GetBytes(text);
		byte[] bytes=new byte[preamble.Length+bytesText.Length];
		Buffer.BlockCopy(preamble,0,bytes,0,preamble.Length);
		Buffer.BlockCopy(bytesText,0,bytes,preamble.Length,bytesText.Length);
		System.IO.File.WriteAllBytes(path,bytes);
	}
}
'@

function Invoke-Git {
	param([string[]]$Arguments)
	$output=& git @Arguments 2>&1
	if($LASTEXITCODE -ne 0) {
		throw "git $($Arguments -join ' ') failed: $output"
	}
	return $output
}

#The relative path a file has after the move (backslashes): the old project folder and project file names mapped.
function Get-NewRelativePath([string]$relPath) {
	$parts=$relPath -split '\\'
	if($parts.Count -ge 2 -and $NameMap.Contains($parts[0])) {
		$folderOld=$parts[0]
		$parts[0]=$NameMap[$folderOld]
		if($parts.Count -eq 2 -and $parts[1] -eq "$folderOld.csproj") {
			$parts[1]="$($NameMap[$folderOld]).csproj"
		}
	}
	return ($parts -join '\')
}

try {
	if(-not ('OpmsRename' -as [type])) {
		Add-Type -TypeDefinition $EngineSource -Language CSharp
	}
	if([string]::IsNullOrEmpty($Root)) {
		$Root=Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
	}
	$Root=(Resolve-Path -LiteralPath $Root).Path.TrimEnd('\')
	if(-not ((Test-Path -LiteralPath (Join-Path $Root 'LICENSE')) -and (Test-Path -LiteralPath (Join-Path $Root 'NOTICE')))) {
		throw "Not the repository root (no LICENSE and NOTICE): $Root"
	}
	if(-not (Test-Path -LiteralPath (Join-Path $Root '.git'))) {
		throw "Not a git working tree: $Root"
	}
	function New-Rule($rule) {
		$r=New-Object RenRule
		$r.Category=$rule.Category
		$r.Regex=New-Object System.Text.RegularExpressions.Regex($rule.Regex)
		if($rule.ContainsKey('Value')) {
			$r.Value=$rule.Value
		}
		$r.XmlComments=($rule.ContainsKey('XmlComments') -and $rule.XmlComments)
		return [pscustomobject]@{ Path=$rule.Path; Rule=$r }
	}
	$stringRulesCompiled=@($StringRules | ForEach-Object { New-Rule $_ })
	$textRulesCompiled=@($TextRules | ForEach-Object { New-Rule $_ })
	#git runs in the repository root.
	Set-Location -LiteralPath $Root
}
catch {
	Write-Host "Rename-ToOpenPmsDental: could not start: $($_.Exception.Message)"
	exit 2
}

if(-not $DryRun) {
	$status=Invoke-Git @('status','--porcelain','--untracked-files=normal')
	if($status) {
		Write-Host 'Rename-ToOpenPmsDental: the working tree is not clean. Commit or remove these first:'
		$status | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" }
		exit 1
	}
}

$countsTotal=[ordered]@{}
function Add-Count([string]$category,[int]$count) {
	if($count -le 0) {
		return
	}
	if(-not $countsTotal.Contains($category)) {
		$countsTotal[$category]=0
	}
	$countsTotal[$category]+=$count
}

try {
	#--- 1. Moves.
	foreach($folderOld in $NameMap.Keys) {
		$folderNew=$NameMap[$folderOld]
		$tracked=@(Invoke-Git @('ls-files','--',"$folderOld/") | Where-Object { $_ })
		if($tracked.Count -gt 0) {
			if(-not (Test-Path -LiteralPath (Join-Path $Root $folderNew))) {
				if(-not $DryRun) {
					Invoke-Git @('mv','--',$folderOld,$folderNew) | Out-Null
				}
				Add-Count 'Moves: project folders (git mv)' 1
				Add-Count 'Moves: files in the moved folders' $tracked.Count
			}
			else {
				#A straggling branch: the new folder exists, and files were added to the old one. Move them one by one.
				foreach($file in $tracked) {
					$fileNew=$folderNew+$file.Substring($folderOld.Length)
					if(-not $DryRun) {
						$dirNew=Split-Path -Parent (Join-Path $Root $fileNew)
						if(-not (Test-Path -LiteralPath $dirNew)) {
							New-Item -ItemType Directory -Path $dirNew -Force | Out-Null
						}
						Invoke-Git @('mv','--',$file,$fileNew) | Out-Null
					}
					Add-Count 'Moves: files left in an old folder (git mv)' 1
				}
			}
		}
		$projectOld="$folderNew/$folderOld.csproj"
		$projectNew="$folderNew/$folderNew.csproj"
		$projectTracked=@(Invoke-Git @('ls-files','--',$projectOld) | Where-Object { $_ })
		if($projectTracked.Count -gt 0 -and -not $DryRun) {
			Invoke-Git @('mv','--',$projectOld,$projectNew) | Out-Null
		}
		if($projectTracked.Count -gt 0 -or ($DryRun -and $tracked -contains "$folderOld/$folderOld.csproj")) {
			Add-Count 'Moves: project files (git mv)' 1
		}
	}

	#--- 2. Text.
	$files=@(Invoke-Git @('-c','core.quotepath=off','ls-files') | Where-Object { $_ })
	$report=New-Object System.Collections.Generic.List[string]
	$leftoverCounts=[ordered]@{}
	$filesChanged=0
	foreach($file in $files) {
		$relPath=$file.Replace('/','\')
		if($SkipPaths -contains $relPath) {
			continue
		}
		$relNew=$(if($DryRun) { Get-NewRelativePath $relPath } else { $relPath })
		$isCSharp=$relNew.EndsWith('.cs',[StringComparison]::OrdinalIgnoreCase)
		$rules=@(foreach($r in $(if($isCSharp) { $stringRulesCompiled } else { $textRulesCompiled })) { if($relNew -like $r.Path) { $r.Rule } })
		if(-not $isCSharp -and $rules.Count -eq 0) {
			continue
		}
		$path=Join-Path $Root $relPath
		if(-not (Test-Path -LiteralPath $path -PathType Leaf)) {
			continue
		}
		$encoding=$null
		$preamble=$null
		$text=[OpmsRename]::Read($path,[ref]$encoding,[ref]$preamble)
		if($text.IndexOf('opendent',[StringComparison]::OrdinalIgnoreCase) -lt 0) {
			continue
		}
		if($isCSharp) {
			$result=[OpmsRename]::ProcessCSharp($text,[RenRule[]]$rules)
		}
		else {
			$result=[OpmsRename]::ProcessText($text,[RenRule[]]$rules)
		}
		if($result.Changed) {
			$filesChanged++
			if(-not $DryRun) {
				[OpmsRename]::Write($path,$result.Text,$encoding,$preamble)
			}
			$parts=@()
			foreach($key in $result.Counts.Keys) {
				Add-Count $key $result.Counts[$key]
				$parts+="$key $($result.Counts[$key])"
			}
			$report.Add("CHANGED $relNew : $($parts -join '; ')")
		}
		foreach($leftover in $result.Leftovers) {
			if(-not $leftoverCounts.Contains($leftover.Kind)) {
				$leftoverCounts[$leftover.Kind]=0
			}
			$leftoverCounts[$leftover.Kind]++
			$text160=$leftover.Text
			if($text160.Length -gt 200) {
				$text160=$text160.Substring(0,200)+'...'
			}
			$report.Add("$($leftover.Kind.PadRight(36)) $($relNew):$($leftover.Line): $text160")
		}
	}
}
catch {
	Write-Host "Rename-ToOpenPmsDental: failed: $($_.Exception.Message)"
	Write-Host $_.ScriptStackTrace
	exit 2
}

$mode=$(if($DryRun) { 'dry run, nothing changed' } else { 'applied' })
Write-Host "Rename-ToOpenPmsDental ($mode): $filesChanged file(s) with text changes."
if($countsTotal.Count -eq 0) {
	Write-Host '  Nothing to change: the rename is already applied.'
}
foreach($key in $countsTotal.Keys) {
	Write-Host ('  {0,7}  {1}' -f $countsTotal[$key],$key)
}
if($leftoverCounts.Count -gt 0) {
	Write-Host 'Old names kept or to review (see -ReportFile for each line):'
	foreach($key in $leftoverCounts.Keys) {
		Write-Host ('  {0,7}  {1}' -f $leftoverCounts[$key],$key)
	}
}
if($ReportFile) {
	$report | Sort-Object | Set-Content -LiteralPath $ReportFile -Encoding UTF8
	Write-Host "Report: $ReportFile"
}
exit 0
