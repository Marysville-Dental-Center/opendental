// B-PRINT: a small Code 39 encoder for the statement inserter barcode (Open Dental 25.3). Written for this fork from the public symbology;
// no barcode library is needed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenPmsDentalBusiness {
	///<summary>Code 39 (also called 3 of 9): each character is 9 elements, 5 bars and 4 spaces alternating and starting with a bar; 3 of the 9 are
	///wide. Characters are separated by one narrow space, and the text is framed by the start/stop character '*'. Only the characters the inserter
	///barcode needs are supported: the digits, '-', '.', space and the capital letters.</summary>
	public static class Code39Barcode {
		///<summary>The start and stop character.</summary>
		public const char START_STOP='*';
		///<summary>Width of a wide element in narrow units. The symbology allows 2 to 3; 3 reads most reliably at small sizes.</summary>
		public const double WIDE_RATIO=3;
		///<summary>Quiet zone on each side, in narrow units (the symbology asks for at least 10).</summary>
		public const double QUIET_ZONE=10;

		///<summary>Element patterns: n = narrow, w = wide, in the order bar, space, bar, space, bar, space, bar, space, bar.</summary>
		private static readonly Dictionary<char,string> _dictPatterns=new Dictionary<char,string> {
			{'0',"nnnwwnwnn"},{'1',"wnnwnnnnw"},{'2',"nnwwnnnnw"},{'3',"wnwwnnnnn"},{'4',"nnnwwnnnw"},
			{'5',"wnnwwnnnn"},{'6',"nnwwwnnnn"},{'7',"nnnwnnwnw"},{'8',"wnnwnnwnn"},{'9',"nnwwnnwnn"},
			{'A',"wnnnnwnnw"},{'B',"nnwnnwnnw"},{'C',"wnwnnwnnn"},{'D',"nnnnwwnnw"},{'E',"wnnnwwnnn"},
			{'F',"nnwnwwnnn"},{'G',"nnnnnwwnw"},{'H',"wnnnnwwnn"},{'I',"nnwnnwwnn"},{'J',"nnnnwwwnn"},
			{'K',"wnnnnnnww"},{'L',"nnwnnnnww"},{'M',"wnwnnnnwn"},{'N',"nnnnwnnww"},{'O',"wnnnwnnwn"},
			{'P',"nnwnwnnwn"},{'Q',"nnnnnnwww"},{'R',"wnnnnnwwn"},{'S',"nnwnnnwwn"},{'T',"nnnnwnwwn"},
			{'U',"wwnnnnnnw"},{'V',"nwwnnnnnw"},{'W',"wwwnnnnnn"},{'X',"nwnnwnnnw"},{'Y',"wwnnwnnnn"},
			{'Z',"nwwnwnnnn"},{'-',"nwnnnnwnw"},{'.',"wwnnnnwnn"},{' ',"nwwnnnwnn"},{START_STOP,"nwnnwnwnn"},
		};

		///<summary>True if every character of the text can be encoded (the start/stop character itself is not allowed inside the text).</summary>
		public static bool IsValid(string text) {
			if(string.IsNullOrEmpty(text)) {
				return false;
			}
			return text.All(x => x!=START_STOP && _dictPatterns.ContainsKey(x));
		}

		///<summary>The element pattern of one character (9 letters, n or w). Throws for a character Code 39 cannot encode here.</summary>
		public static string GetPattern(char c) {
			if(!_dictPatterns.TryGetValue(c,out string pattern)) {
				throw new ArgumentException("Code 39 cannot encode '"+c+"'.");
			}
			return pattern;
		}

		///<summary>The barcode as modules, with a wide element drawn as 2 modules: "1" for a bar module and "0" for a space module, start and
		///stop characters included, quiet zones not included. This is the common way Code 39 is printed in references, so it is easy to check.</summary>
		public static string GetModules(string text) {
			if(!IsValid(text)) {
				throw new ArgumentException("Code 39 cannot encode \""+text+"\".");
			}
			StringBuilder stringBuilder=new StringBuilder();
			string framed=START_STOP+text+START_STOP;
			for(int i=0;i<framed.Length;i++) {
				if(i>0) {
					stringBuilder.Append('0');//narrow space between characters
				}
				string pattern=GetPattern(framed[i]);
				for(int j=0;j<pattern.Length;j++) {
					char module=(j%2==0) ? '1' : '0';
					stringBuilder.Append(module);
					if(pattern[j]=='w') {
						stringBuilder.Append(module);
					}
				}
			}
			return stringBuilder.ToString();
		}

		///<summary>The bars to draw, as (left edge, width) pairs in narrow units, measured from the left edge of the quiet zone. totalWidth is the
		///full width including both quiet zones, so a caller can scale everything into its rectangle.</summary>
		public static List<Tuple<double,double>> GetBars(string text,out double totalWidth,double wideRatio=WIDE_RATIO,double quietZone=QUIET_ZONE) {
			if(!IsValid(text)) {
				throw new ArgumentException("Code 39 cannot encode \""+text+"\".");
			}
			List<Tuple<double,double>> listBars=new List<Tuple<double,double>>();
			double x=quietZone;
			string framed=START_STOP+text+START_STOP;
			for(int i=0;i<framed.Length;i++) {
				if(i>0) {
					x+=1;//narrow space between characters
				}
				string pattern=GetPattern(framed[i]);
				for(int j=0;j<pattern.Length;j++) {
					double width=(pattern[j]=='w') ? wideRatio : 1;
					if(j%2==0) {
						listBars.Add(Tuple.Create(x,width));
					}
					x+=width;
				}
			}
			totalWidth=x+quietZone;
			return listBars;
		}
	}
}
