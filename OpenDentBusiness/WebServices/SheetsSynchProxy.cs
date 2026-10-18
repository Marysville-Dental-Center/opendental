using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using OpenDentBusiness;
using WebServiceSerializer;

namespace OpenDentBusiness {
	public class SheetsSynchProxy {

		///<summary></summary>
		private static int _timeoutOverride=100000;//100 seconds
		///<summary></summary>
		private static string _urlOverride="";

		///<summary>Kept so existing callers compile. Has no effect because the Web Forms service is never contacted.</summary>
		public static int TimeoutOverride {
			get {
				return _timeoutOverride;
			}
			set {
				_timeoutOverride=value;
			}
		}

		///<summary>Kept so existing callers compile. Has no effect because the Web Forms service is never contacted.</summary>
		public static string UrlOverride {
			get {
				return _urlOverride;
			}
			set {
				_urlOverride=value;
			}
		}

		///<summary>Unit tests may set this to a fake implementation. When null, GetWebServiceInstance() returns SheetsSynchNoHq.</summary>
		public static ISheetsSynch MockSheetSynchService {
			private get;//Use GetWebServiceInstance()
			set;
		}

		///<summary>Returns the "no HQ" stand-in for Open Dental's hosted Web Forms service (SheetsSynch.asmx). It never makes a network call.</summary>
		public static ISheetsSynch GetWebServiceInstance() {
			if(MockSheetSynchService!=null) {
				return MockSheetSynchService;
			}
			//Reset the one-shot overrides the same way the hosted service did.
			TimeoutOverride=100000;
			UrlOverride="";
			return new SheetsSynchNoHq();
		}

	}
}
