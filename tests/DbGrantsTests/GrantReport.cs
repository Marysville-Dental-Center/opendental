// The record of the manual grants test: every statement or program call it tried, what it expected (allowed, or refused for a missing right)
// and what the server answered.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MySqlConnector;

namespace DbGrantsTests {
	///<summary>Every check of the run, in order. A check passes when the program's call or the statement was allowed and allowed was expected,
	///or it was refused with an "access denied" error and a refusal was expected. Any other outcome fails the run. Messages never hold the
	///account's password (MariaDB's errors do not carry it) and the computer's name is replaced.</summary>
	public class GrantReport {
		///<summary>MariaDB's "access denied" errors: 1044 database, 1045 user (also FILE), 1095 KILL of another account's thread, 1142 table,
		///1143 column, 1211 and 1410 GRANT and CREATE USER, 1227 a right such as SUPER, 1370 a routine.</summary>
		public static readonly int[] ArrayAccessDeniedCodes=new[] { 1044,1045,1095,1142,1143,1211,1227,1370,1410 };
		private readonly List<string> _listLines=new List<string>();
		private string _sectionCur="";
		public int CountPass;
		public int CountTolerated;
		public readonly List<string> ListFailures=new List<string>();

		public void Section(string title) {
			_sectionCur=title;
			_listLines.Add("");
			_listLines.Add("## "+title);
		}

		public void Note(string text) {
			_listLines.Add("   note     "+Clean(text));
		}

		///<summary>Runs the action and expects it to be allowed. With isOtherErrorTolerated, an error that is not "access denied" is recorded and
		///does not fail the run (database maintenance methods on synthetic data); an "access denied" always fails it.</summary>
		public bool Allowed(string what,Action action,bool isOtherErrorTolerated=false) {
			Exception exception=Run(action);
			if(exception==null) {
				Pass("allowed",what,"");
				return true;
			}
			if(IsAccessDenied(exception,out int code,out string message)) {
				Fail("allowed",what,"REFUSED "+code+": "+message);
				return false;
			}
			if(isOtherErrorTolerated) {
				CountTolerated++;
				_listLines.Add("   other    "+_sectionCur+" | "+what+" | not a rights error: "+Clean(Describe(exception)));
				return false;
			}
			Fail("allowed",what,"ERROR: "+Describe(exception));
			return false;
		}

		///<summary>Runs the function, expects it to be allowed, and returns its value (default when it was not).</summary>
		public T Allowed<T>(string what,Func<T> func) {
			T value=default;
			Allowed(what,() => { value=func(); });
			return value;
		}

		///<summary>Runs the action and expects MariaDB to refuse it with an "access denied" error. right names the right that is missing.</summary>
		public bool Refused(string what,string right,Action action) {
			Exception exception=Run(action);
			if(exception==null) {
				Fail("refused",what+" (without "+right+")","ALLOWED");
				return false;
			}
			if(IsAccessDenied(exception,out int code,out string message)) {
				Pass("refused",what+" (without "+right+")",code+": "+message);
				return true;
			}
			Fail("refused",what+" (without "+right+")","ERROR, not access denied: "+Describe(exception));
			return false;
		}

		///<summary>Records a fact that must hold.</summary>
		public void Expect(string what,bool isTrue,string detail="") {
			if(isTrue) {
				Pass("holds",what,detail);
			}
			else {
				Fail("holds",what,"DOES NOT HOLD"+(detail==""?"":": "+detail));
			}
		}

		private void Pass(string kind,string what,string detail) {
			CountPass++;
			_listLines.Add("   "+("ok "+kind).PadRight(9)+_sectionCur+" | "+Clean(what)+(detail==""?"":" | "+Clean(detail)));
		}

		private void Fail(string kind,string what,string detail) {
			string line=_sectionCur+" | "+Clean(what)+" | "+Clean(detail);
			ListFailures.Add(kind+": "+line);
			_listLines.Add("   "+("FAIL "+kind).PadRight(9)+line);
		}

		private static Exception Run(Action action) {
			try {
				action();
				return null;
			}
			catch(Exception ex) {
				return ex;
			}
		}

		///<summary>True when the exception, or one inside it, is MariaDB's "access denied" (ArrayAccessDeniedCodes).</summary>
		public static bool IsAccessDenied(Exception exception,out int code,out string message) {
			code=0;
			message="";
			for(Exception ex=exception;ex!=null;ex=ex.InnerException) {
				if(ex is MySqlException mySqlException) {
					code=mySqlException.Number;
					message=mySqlException.Message;
					if(ArrayAccessDeniedCodes.Contains(code)) {
						return true;
					}
				}
				if(ex is AggregateException aggregateException) {
					foreach(Exception exInner in aggregateException.InnerExceptions) {
						if(IsAccessDenied(exInner,out code,out message)) {
							return true;
						}
					}
				}
			}
			return false;
		}

		private static string Describe(Exception exception) {
			if(exception is TargetInvocationException && exception.InnerException!=null) {
				exception=exception.InnerException;
			}
			string text=exception.GetType().Name+": "+exception.Message;
			Exception exBase=exception.GetBaseException();
			if(exBase!=exception) {
				text+=" ("+exBase.GetType().Name+": "+exBase.Message+")";
			}
			return text;
		}

		///<summary>One line, and the computer's name replaced: the report may be shared.</summary>
		public static string Clean(string text) {
			text=(text??"").Replace("\r\n"," ").Replace("\n"," ").Replace("\r"," ");
			foreach(string name in new[] { Environment.MachineName,Environment.UserName }) {
				if(!string.IsNullOrEmpty(name) && name.Length>2) {
					text=text.Replace(name,"<this computer>").Replace(name.ToLowerInvariant(),"<this computer>");
				}
			}
			if(text.Length>400) {
				text=text.Substring(0,400)+"...";
			}
			return text;
		}

		public string GetSummary() {
			return CountPass+" checks passed, "+ListFailures.Count+" failed, "+CountTolerated+" maintenance methods stopped on an error that is not a "
				+"rights error.";
		}

		public string GetText(string header) {
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine(header);
			stringBuilder.AppendLine(GetSummary());
			if(ListFailures.Count>0) {
				stringBuilder.AppendLine();
				stringBuilder.AppendLine("Failures:");
				foreach(string failure in ListFailures) {
					stringBuilder.AppendLine("  "+failure);
				}
			}
			foreach(string line in _listLines) {
				stringBuilder.AppendLine(line);
			}
			return stringBuilder.ToString();
		}

		public void Write(string path,string header) {
			string folder=Path.GetDirectoryName(path);
			if(!string.IsNullOrEmpty(folder)) {
				Directory.CreateDirectory(folder);
			}
			File.WriteAllText(path,GetText(header),new UTF8Encoding(false));
		}
	}
}
