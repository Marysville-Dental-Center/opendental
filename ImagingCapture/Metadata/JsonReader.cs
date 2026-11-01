// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ImagingCapture.Metadata {
	///<summary>The kind of a JsonValue.</summary>
	internal enum JsonKind {
		Null,
		Bool,
		Number,
		String,
		Array,
		Object,
	}

	///<summary>One value of a document read by JsonReader. Objects keep their members in document order.</summary>
	internal sealed class JsonValue {
		public JsonKind Kind;
		///<summary>Kind Bool only.</summary>
		public bool Bool;
		///<summary>Kind String: the unescaped text. Kind Number: the number exactly as written (RFC 8259 grammar).</summary>
		public string Text;
		///<summary>Kind Array only.</summary>
		public List<JsonValue> ListItems;
		///<summary>Kind Object only: the members in document order, duplicates included.</summary>
		public List<KeyValuePair<string,JsonValue>> ListMembers;

		///<summary>The value of the member with this name (ordinal comparison), or null when this is not an object or has no such member.
		///When a name appears more than once, the last one counts, as in most readers.</summary>
		public JsonValue Get(string name) {
			if(Kind!=JsonKind.Object) {
				return null;
			}
			for(int i=ListMembers.Count-1;i>=0;i--) {
				if(ListMembers[i].Key==name) {
					return ListMembers[i].Value;
				}
			}
			return null;
		}

		///<summary>A number as a double. False for anything else, and for a number that does not fit a double.</summary>
		public bool TryGetDouble(out double value) {
			value=0;
			if(Kind!=JsonKind.Number) {
				return false;
			}
			if(!double.TryParse(Text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)) {
				return false;
			}
			return !double.IsInfinity(value) && !double.IsNaN(value);
		}

		///<summary>A number with no fractional part that fits an int ("12", "12.0", "1.2e1"). False for anything else.</summary>
		public bool TryGetInt(out int value) {
			value=0;
			double number;
			if(!TryGetDouble(out number)) {
				return false;
			}
			if(number!=Math.Floor(number) || number<int.MinValue || number>int.MaxValue) {
				return false;
			}
			value=(int)number;
			return true;
		}
	}

	///<summary>A small, strict JSON reader (RFC 8259) for the capture metadata, so that ImagingCapture needs no JSON library. It reads a
	///whole document or nothing: Parse returns null for any syntax error, for a document nested deeper than MaxDepth, and for text longer
	///than MaxLength. A leading byte order mark and trailing whitespace or NUL characters are accepted, because the text comes from a native
	///handle.</summary>
	internal static class JsonReader {
		///<summary>Arrays and objects nested deeper than this make the document unreadable, so a hostile document cannot exhaust the stack.</summary>
		public const int MaxDepth=64;
		///<summary>The capture metadata is about a kilobyte; a longer text is not capture metadata.</summary>
		public const int MaxLength=4*1024*1024;

		///<summary>The document's root value, or null when text is null, empty or not one well-formed JSON value.</summary>
		public static JsonValue Parse(string text) {
			if(string.IsNullOrEmpty(text) || text.Length>MaxLength) {
				return null;
			}
			Parser parser=new Parser(text);
			try {
				return parser.ParseDocument();
			}
			catch(FormatException) {
				return null;
			}
		}

		private sealed class Parser {
			private readonly string _text;
			private int _pos;

			public Parser(string text) {
				_text=text;
			}

			public JsonValue ParseDocument() {
				if(_text[0]=='\uFEFF') {
					_pos=1;
				}
				SkipWhitespace();
				JsonValue jsonValue=ParseValue(0);
				while(_pos<_text.Length && (_text[_pos]=='\0' || _text[_pos]==' ' || _text[_pos]=='\t' || _text[_pos]=='\n' || _text[_pos]=='\r')) {
					_pos++;
				}
				if(_pos!=_text.Length) {
					throw Error("Text after the value");
				}
				return jsonValue;
			}

			private JsonValue ParseValue(int depth) {
				if(_pos>=_text.Length) {
					throw Error("Unexpected end");
				}
				char c=_text[_pos];
				switch(c) {
					case '{':
						return ParseObject(depth+1);
					case '[':
						return ParseArray(depth+1);
					case '"':
						return new JsonValue { Kind=JsonKind.String,Text=ParseString() };
					case 't':
						ExpectLiteral("true");
						return new JsonValue { Kind=JsonKind.Bool,Bool=true };
					case 'f':
						ExpectLiteral("false");
						return new JsonValue { Kind=JsonKind.Bool,Bool=false };
					case 'n':
						ExpectLiteral("null");
						return new JsonValue { Kind=JsonKind.Null };
					default:
						if(c=='-' || (c>='0' && c<='9')) {
							return new JsonValue { Kind=JsonKind.Number,Text=ParseNumber() };
						}
						throw Error("Unexpected character");
				}
			}

			private JsonValue ParseObject(int depth) {
				if(depth>MaxDepth) {
					throw Error("Nested too deep");
				}
				_pos++;//'{'
				JsonValue jsonValue=new JsonValue { Kind=JsonKind.Object,ListMembers=new List<KeyValuePair<string,JsonValue>>() };
				SkipWhitespace();
				if(Peek()=='}') {
					_pos++;
					return jsonValue;
				}
				while(true) {
					SkipWhitespace();
					if(Peek()!='"') {
						throw Error("Expected a member name");
					}
					string name=ParseString();
					SkipWhitespace();
					if(Peek()!=':') {
						throw Error("Expected ':'");
					}
					_pos++;
					SkipWhitespace();
					JsonValue jsonValueMember=ParseValue(depth);
					jsonValue.ListMembers.Add(new KeyValuePair<string,JsonValue>(name,jsonValueMember));
					SkipWhitespace();
					char c=Peek();
					_pos++;
					if(c=='}') {
						return jsonValue;
					}
					if(c!=',') {
						throw Error("Expected ',' or '}'");
					}
				}
			}

			private JsonValue ParseArray(int depth) {
				if(depth>MaxDepth) {
					throw Error("Nested too deep");
				}
				_pos++;//'['
				JsonValue jsonValue=new JsonValue { Kind=JsonKind.Array,ListItems=new List<JsonValue>() };
				SkipWhitespace();
				if(Peek()==']') {
					_pos++;
					return jsonValue;
				}
				while(true) {
					SkipWhitespace();
					jsonValue.ListItems.Add(ParseValue(depth));
					SkipWhitespace();
					char c=Peek();
					_pos++;
					if(c==']') {
						return jsonValue;
					}
					if(c!=',') {
						throw Error("Expected ',' or ']'");
					}
				}
			}

			private string ParseString() {
				_pos++;//opening quote
				StringBuilder stringBuilder=new StringBuilder();
				while(true) {
					if(_pos>=_text.Length) {
						throw Error("Unterminated string");
					}
					char c=_text[_pos++];
					if(c=='"') {
						return stringBuilder.ToString();
					}
					if(c<0x20) {
						throw Error("Control character in a string");
					}
					if(c!='\\') {
						stringBuilder.Append(c);
						continue;
					}
					if(_pos>=_text.Length) {
						throw Error("Unterminated escape");
					}
					char cEscape=_text[_pos++];
					switch(cEscape) {
						case '"': stringBuilder.Append('"'); break;
						case '\\': stringBuilder.Append('\\'); break;
						case '/': stringBuilder.Append('/'); break;
						case 'b': stringBuilder.Append('\b'); break;
						case 'f': stringBuilder.Append('\f'); break;
						case 'n': stringBuilder.Append('\n'); break;
						case 'r': stringBuilder.Append('\r'); break;
						case 't': stringBuilder.Append('\t'); break;
						case 'u': stringBuilder.Append(ParseHex4()); break;
						default: throw Error("Invalid escape");
					}
				}
			}

			///<summary>The four hex digits after "\u". Surrogate pairs come as two escapes and are appended one char at a time, which
			///.NET strings hold as UTF-16 anyway.</summary>
			private char ParseHex4() {
				if(_pos+4>_text.Length) {
					throw Error("Short \\u escape");
				}
				int value=0;
				for(int i=0;i<4;i++) {
					char c=_text[_pos++];
					int digit;
					if(c>='0' && c<='9') {
						digit=c-'0';
					}
					else if(c>='a' && c<='f') {
						digit=c-'a'+10;
					}
					else if(c>='A' && c<='F') {
						digit=c-'A'+10;
					}
					else {
						throw Error("Invalid \\u escape");
					}
					value=value*16+digit;
				}
				return (char)value;
			}

			///<summary>-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?</summary>
			private string ParseNumber() {
				int start=_pos;
				if(Peek()=='-') {
					_pos++;
				}
				if(Peek()=='0') {
					_pos++;
				}
				else if(IsDigit(Peek())) {
					SkipDigits();
				}
				else {
					throw Error("Invalid number");
				}
				if(Peek()=='.') {
					_pos++;
					if(!IsDigit(Peek())) {
						throw Error("Invalid number");
					}
					SkipDigits();
				}
				if(Peek()=='e' || Peek()=='E') {
					_pos++;
					if(Peek()=='+' || Peek()=='-') {
						_pos++;
					}
					if(!IsDigit(Peek())) {
						throw Error("Invalid number");
					}
					SkipDigits();
				}
				return _text.Substring(start,_pos-start);
			}

			private void ExpectLiteral(string literal) {
				if(string.CompareOrdinal(_text,_pos,literal,0,literal.Length)!=0) {
					throw Error("Invalid literal");
				}
				_pos+=literal.Length;
			}

			private void SkipDigits() {
				while(IsDigit(Peek())) {
					_pos++;
				}
			}

			private void SkipWhitespace() {
				while(_pos<_text.Length) {
					char c=_text[_pos];
					if(c!=' ' && c!='\t' && c!='\n' && c!='\r') {
						return;
					}
					_pos++;
				}
			}

			///<summary>The current character, or '\0' at the end (which no rule accepts where a character is needed).</summary>
			private char Peek() {
				return _pos<_text.Length ? _text[_pos] : '\0';
			}

			private static bool IsDigit(char c) {
				return c>='0' && c<='9';
			}

			private FormatException Error(string message) {
				return new FormatException(message+" at position "+_pos.ToString(CultureInfo.InvariantCulture)+".");
			}
		}
	}
}
