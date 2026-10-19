// Vendor-direct integrations: the one background thread that runs the vendor packages' periodic jobs on this workstation.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Reflection;
using CodeBase;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;

namespace OpenDental.VendorDirect {
	///<summary>Starts one ODThread that asks VendorJobScheduler to run the due jobs once a minute. Jobs run only while their program link is enabled,
	///one workstation at a time (VendorJobLease). With no job classes in the program, no thread is started at all.</summary>
	public static class VendorJobHost {
		///<summary>How often the thread checks for due jobs.</summary>
		public static readonly TimeSpan CheckInterval=TimeSpan.FromMinutes(1);
		private static VendorJobScheduler _vendorJobScheduler;

		///<summary>Starts the thread in groupName, unless there are no jobs. The caller makes sure it isn't already running.</summary>
		public static void Start(string groupName) {
			if(_vendorJobScheduler==null) {
				List<IVendorBackgroundJob> listJobs=VendorJobScheduler.DiscoverJobs(Assembly.GetExecutingAssembly());
				_vendorJobScheduler=new VendorJobScheduler(listJobs);
			}
			if(_vendorJobScheduler.Jobs.Count==0) {
				return;
			}
			ODThread odThread=new ODThread((int)CheckInterval.TotalMilliseconds,(o) => {
				_vendorJobScheduler.RunDueJobs();
			});
			odThread.AddExceptionHandler((e) => Logger.WriteLine("Vendor job host: "+e.Message,"VendorJobs"));
			odThread.GroupName=groupName;
			odThread.Name=groupName;
			odThread.Start();
		}
	}
}
