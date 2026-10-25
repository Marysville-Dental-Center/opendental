// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using ImagingDeviceManager.Config;

namespace ImagingDeviceManager.TwainProtocol {
	///<summary>The source gate (design 7.1): with the administrator-set [Twain] RestrictSources=1, only sources whose ProductName is listed
	///in [Twain] AllowedSources may be opened, by name (Mount/Acquire) or as the default source (scans). The default, RestrictSources=0,
	///lets every source through. It guards against accidental use of an untested source; it is not access control, because capture.ini
	///is user-writable (design 9.3).</summary>
	internal sealed class TwainSourceGate {
		///<summary>The error text, and the notice the facade shows, when the gate refuses a source.</summary>
		public const string RefusedText="This TWAIN source is not enabled on this computer.";
		///<summary>[Twain] RestrictSources.</summary>
		public bool IsRestricted;
		///<summary>[Twain] AllowedSources: ProductNames, matched ignoring case and surrounding spaces.</summary>
		public List<string> ListAllowedSources=new List<string>();

		public static TwainSourceGate FromConfig(LocalCaptureConfig config) {
			TwainSourceGate gate=new TwainSourceGate();
			if(config==null) {
				return gate;
			}
			gate.IsRestricted=config.RestrictSources;
			gate.ListAllowedSources=config.AllowedSources;
			return gate;
		}

		///<summary>True when productName may be opened: always without RestrictSources, otherwise only when it is listed. A source without
		///a name is never listed.</summary>
		public bool IsAllowed(string productName) {
			if(!IsRestricted) {
				return true;
			}
			string name=(productName??"").Trim();
			if(name=="") {
				return false;
			}
			foreach(string nameListed in ListAllowedSources) {
				if(string.Equals((nameListed??"").Trim(),name,StringComparison.OrdinalIgnoreCase)) {
					return true;
				}
			}
			return false;
		}
	}
}
