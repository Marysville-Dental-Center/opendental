// Open Dental 26.2 (P262-GLIDEWELL): the Glidewell program link row. Inserted at runtime with Enabled=0 like every vendor-direct program link
// (Programs.InsertIfMissing); the office's accounts are rows of glidewellacct, not program properties. Owner decisions of 2026-11-07: patient data
// goes to Glidewell only once Setup records a signed business associate agreement with Glidewell (the postcard service's rule), and the program
// link is hidden in Program Links until a transport exists.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness.VendorDirect.Glidewell {
	///<summary>The Glidewell program link. Off until the office turns it on in Setup, Program Links, and sending stays impossible until the office
	///has entered a Glidewell account with its API key and recorded a business associate agreement with Glidewell. Hidden in Program Links while
	///this build has no transport (GlidewellClients.Current is GlidewellClientBlocked). Nothing here calls Glidewell.</summary>
	public static class GlidewellProgram {
		public const string ProgDesc="Glidewell lab cases";

		///<summary>programproperty.PropertyDesc values (ClinicNum 0 rows of the program row), the same as the postcard service's.</summary>
		public static class PropertyDescs {
			///<summary>"1" once Setup recorded that a signed business associate agreement with Glidewell is on file, else "0".</summary>
			public const string BaaOnFile="BAA On File";
			///<summary>Who recorded it and when ("user, date and time"), or "".</summary>
			public const string BaaConfirmed="BAA Confirmed";
		}

		///<summary>Whether the business associate agreement is on file. Tests may replace it; the program reads the BAA On File row.</summary>
		public static Func<bool> FuncIsBaaOnFile=() => GetSetting(PropertyDescs.BaaOnFile)=="1";

		///<summary>Inserts the program row (Enabled=0) if missing, then its missing ClinicNum 0 property rows (BAA On File "0", BAA Confirmed "").
		///Returns true if it inserted anything; the caller refreshes the caches and signals InvalidType.Programs. Never calls Glidewell.</summary>
		public static bool EnsureProgram() {
			Meth.NoCheckMiddleTierRole();
			bool isInserted=Programs.InsertIfMissing(ProgramName.Glidewell,ProgDesc);
			if(isInserted || Programs.GetCur(ProgramName.Glidewell)==null) {
				Programs.RefreshCache();
			}
			long programNum=GetProgramNum();
			if(programNum==0) {
				return isInserted;
			}
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetListForProgramAndClinic(programNum,0);
			bool isPropertyInserted=false;
			foreach(KeyValuePair<string,string> pair in GetDefaultValues()) {
				if(listProgramProperties.Any(x => x.PropertyDesc==pair.Key)) {
					continue;
				}
				VendorCredentials.EnsureSetting(programNum,pair.Key,0,pair.Value);
				isPropertyInserted=true;
			}
			if(isPropertyInserted) {
				ProgramProperties.RefreshCache();
			}
			return isInserted || isPropertyInserted;
		}

		///<summary>Every ClinicNum 0 property row and its default value.</summary>
		public static Dictionary<string,string> GetDefaultValues() {
			Meth.NoCheckMiddleTierRole();
			return new Dictionary<string,string> {
				{ PropertyDescs.BaaOnFile,"0" },
				{ PropertyDescs.BaaConfirmed,"" },
			};
		}

		///<summary>True when the office turned the program link on. Every Glidewell control in the Edit Lab Case window shows only then.</summary>
		public static bool IsEnabled() {
			Meth.NoCheckMiddleTierRole();
			return Programs.IsEnabled(ProgramName.Glidewell);
		}

		///<summary>The program row's key, or 0 if the row does not exist yet (nobody has opened Program Links).</summary>
		public static long GetProgramNum() {
			Meth.NoCheckMiddleTierRole();
			return Programs.GetProgramNum(ProgramName.Glidewell);
		}

		///<summary>True when this build has a transport to Glidewell (GlidewellClients.Current, or glidewellClient when given, is available).</summary>
		public static bool IsTransportAvailable(IGlidewellClient glidewellClient=null) {
			Meth.NoCheckMiddleTierRole();
			glidewellClient=glidewellClient??GlidewellClients.Current;
			return glidewellClient!=null && glidewellClient.IsAvailable;
		}

		///<summary>False while this build has no transport (owner decision of 2026-11-07): Setup, Program Links does not list the program link, so it
		///cannot be set up or turned on. The row itself stays (Enabled=0).</summary>
		public static bool IsShownInProgramLinks() {
			Meth.NoCheckMiddleTierRole();
			return IsTransportAvailable();
		}

		///<summary>True once Setup recorded that a signed business associate agreement with Glidewell is on file (owner decision of 2026-11-07,
		///the postcard service's rule). Every call that sends patient data to Glidewell, or asks about a patient's lab case, is blocked until then
		///(GlidewellLabCases.GetSendProblems and CheckStatus).</summary>
		public static bool IsBaaOnFile() {
			Meth.NoCheckMiddleTierRole();
			return FuncIsBaaOnFile();
		}

		///<summary>Why nothing can be sent while the business associate agreement is not on file, for the user.</summary>
		public static string GetBaaMissingText() {
			Meth.NoCheckMiddleTierRole();
			return Lans.g("Glidewell","Patient data can't be sent to Glidewell until Setup records that a signed business associate agreement with "
				+"Glidewell is on file.");
		}

		///<summary>The ClinicNum 0 value of a property row from the cache, or its default when the row or the program row is missing.</summary>
		public static string GetSetting(string desc) {
			Meth.NoCheckMiddleTierRole();
			string defaultValue;
			GetDefaultValues().TryGetValue(desc,out defaultValue);
			long programNum=GetProgramNum();
			if(programNum==0) {
				return defaultValue??"";
			}
			ProgramProperty programProperty=ProgramProperties.GetListForProgramAndClinic(programNum,0).FirstOrDefault(x => x.PropertyDesc==desc);
			if(programProperty==null) {
				return defaultValue??"";
			}
			return programProperty.PropertyValue??"";
		}

		///<summary>Stores a ClinicNum 0 property row, inserting it if needed. Does not refresh the cache; the caller signals InvalidType.Programs.
		///Throws when the program row is missing.</summary>
		public static void SaveSetting(string desc,string value) {
			Meth.NoCheckMiddleTierRole();
			long programNum=GetProgramNum();
			if(programNum==0) {
				throw new ODException("The Glidewell lab cases program link is missing.");
			}
			VendorCredentials.WriteSetting(programNum,desc,0,value);
		}
	}
}
