// Test helper for V-PEARL2: points PearlLog at made-up program properties and an in-memory list of lines, and puts the defaults back.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using OpenPmsDentalBusiness.Pearl;

namespace VPEARL2Tests {
	///<summary>While alive, PearlLog reads its properties from DictProps and writes its lines to Lines instead of the program link and the Logger
	///folder. Dispose restores both defaults.</summary>
	public sealed class LogCapture:IDisposable {
		public readonly Dictionary<string,string> DictProps=new Dictionary<string,string>(StringComparer.Ordinal);
		private readonly List<string> _listLines=new List<string>();

		public LogCapture(string logLevel) {
			DictProps[PearlLog.LOG_LEVEL_PROPERTY]=logLevel;
			PearlLog.FuncGetPropVal=desc => DictProps.TryGetValue(desc,out string value) ? value : "";
			PearlLog.ActionWriteLine=line => {
				lock(_listLines) {
					_listLines.Add(line);
				}
			};
		}

		public string LogLevel {
			set {
				DictProps[PearlLog.LOG_LEVEL_PROPERTY]=value;
			}
		}

		public List<string> Lines {
			get {
				lock(_listLines) {
					return _listLines.ToList();
				}
			}
		}

		public string AllText {
			get {
				return string.Join("\n",Lines);
			}
		}

		public void Clear() {
			lock(_listLines) {
				_listLines.Clear();
			}
		}

		public void Dispose() {
			PearlLog.FuncGetPropVal=PearlLog.GetPropValFromCache;
			PearlLog.ActionWriteLine=PearlLog.WriteLineToLoggerFolder;
		}
	}
}
