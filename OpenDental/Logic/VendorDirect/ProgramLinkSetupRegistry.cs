// Vendor-direct integrations: the list of setup windows that vendor packages registered for Setup > Program Links.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CodeBase;
using CodeBase.VendorDirect;
using OpenDentBusiness;

namespace OpenDental.VendorDirect {
	///<summary>Every IProgramLinkSetup in this program, found by reflection the first time it is needed, plus any registered explicitly.
	///FormProgramLinks calls EnsureAllPrograms before refreshing its list, and Find on double-click before its own switch.</summary>
	public static class ProgramLinkSetupRegistry {
		private static readonly object _lock=new object();
		private static List<IProgramLinkSetup> _listSetups;

		///<summary>All registered setups, in a stable order.</summary>
		public static List<IProgramLinkSetup> GetAll() {
			lock(_lock) {
				if(_listSetups==null) {
					_listSetups=TypeDiscovery.CreateImplementations<IProgramLinkSetup>(Assembly.GetExecutingAssembly());
				}
				return _listSetups.ToList();
			}
		}

		///<summary>Adds a setup that discovery can't create (no parameterless constructor), or replaces the one for the same program.</summary>
		public static void Register(IProgramLinkSetup programLinkSetup) {
			if(programLinkSetup==null) {
				throw new ArgumentNullException(nameof(programLinkSetup));
			}
			List<IProgramLinkSetup> listSetups=GetAll();
			lock(_lock) {
				listSetups.RemoveAll(x => x.ProgName==programLinkSetup.ProgName);
				listSetups.Add(programLinkSetup);
				_listSetups=listSetups;
			}
		}

		///<summary>The setup for a program row's ProgName, or null if no vendor-direct package handles it.</summary>
		public static IProgramLinkSetup Find(string progName) {
			return GetAll().FirstOrDefault(x => x.ProgName.ToString()==progName);
		}

		///<summary>Calls EnsureProgram on every setup. A setup that fails is logged and skipped, so one broken package never keeps Program Links from
		///opening. Returns true if anything was inserted; the program property cache is then refreshed here, and the caller signals the change.</summary>
		public static bool EnsureAllPrograms() {
			bool isAnyInserted=false;
			List<IProgramLinkSetup> listSetups=GetAll();
			for(int i=0;i<listSetups.Count;i++) {
				try {
					isAnyInserted|=listSetups[i].EnsureProgram();
				}
				catch(Exception ex) {
					Logger.WriteLine("EnsureProgram failed for "+listSetups[i].ProgName+": "+ex.Message,"VendorDirect");
				}
			}
			if(isAnyInserted) {
				ProgramProperties.RefreshCache();
			}
			return isAnyInserted;
		}
	}
}
