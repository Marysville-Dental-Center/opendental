using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using CodeBase;
using OpenDentBusiness.UI;
using WebServiceSerializer;

namespace OpenDentBusiness {
	[Serializable]
	public class HqProgram {
		#region private properties
		private static List<HqProgram> _listHqPrograms;
		private static object _lock=new object();
		private ProgramName _programName;
		private string _programNameAsString;
		#endregion
		#region public properties
		public string ProgramNameAsString {
			get {
				return _programNameAsString;
			}
			set {
				_programNameAsString=value;
				Enum.TryParse(value,out _programName);
			}
		}
		
		public bool IsEnabled;
		public string CustErr;
		public string PluginDllName;
		public List<HqProgramProperty> ListProperties;
		#endregion
		#region Methods
		public static ReadOnlyCollection<HqProgram> GetAll() {
			if(IsInitialized()) {
				lock(_lock) {
					return _listHqPrograms.AsReadOnly();
				}	
			}
			return new List<HqProgram>().AsReadOnly();//empty readonly list
		}
		
		public static bool IsInitialized() {
			lock(_lock) {
				return (_listHqPrograms!=null);
			}
		}

		//HqProgram.Download() and DownloadListHqPrograms() were removed. Open Dental HQ used them to disable, delete and overwrite program links
		//and program properties remotely. Nothing fills the list anymore, so GetAll() is always empty and local values are always used.

		///<summary>Unit test methods need to clear out the HqProgram cache between tests.</summary>
		public static void ClearCaches() {
			if(!ODBuild.IsUnitTest) {
				return;
			}
			lock(_lock) {
				_listHqPrograms=null;
			}
		}

	}

	#endregion
	[Serializable]
	public class HqProgramProperty {
		public string PropertyDesc;
		public string PropertyValue;
	}
} 
