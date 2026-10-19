// Vendor-direct integrations: background jobs (PaySimple ACH status, EdgeExpress reconciliation, ...) and the lease that keeps one workstation
// running each of them at a time.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CodeBase;
using CodeBase.VendorDirect;

namespace OpenDentBusiness.VendorDirect {
	///<summary>A periodic job for one integration. Implement it in the vendor package's own file with a public parameterless constructor; the job
	///host finds it by reflection. It runs only while its program link is enabled, on one workstation at a time, on the client (never the middle
	///tier server). This is the interim until a local service host exists (decoupling plan, rule 6).</summary>
	public interface IVendorBackgroundJob {
		///<summary>Unique, stable name, e.g. "PaySimpleAchStatus". Used for the lease row; keep it under 40 characters.</summary>
		string Name { get; }
		///<summary>The job runs only while this program link is enabled.</summary>
		ProgramName ProgramName { get; }
		///<summary>How often to run. Also the lease length, so another workstation doesn't run it within the interval.</summary>
		TimeSpan Interval { get; }
		///<summary>Extra conditions, checked after the program is enabled and before the lease: settings present, this computer chosen, and so on.
		///Must be cheap and must not call the vendor.</summary>
		bool ShouldRun();
		///<summary>Does the work. Exceptions are caught and logged by the host.</summary>
		void Run();
	}

	///<summary>Something that can hold one lease per job across workstations.</summary>
	public interface IVendorJobLease {
		///<summary>True if this workstation now holds the job's lease for duration.</summary>
		bool TryAcquire(IVendorBackgroundJob job,TimeSpan duration);
	}

	///<summary>The value stored in a lease row: "{computer}|{expiry, UTC, round-trip format}".</summary>
	public sealed class VendorJobLeaseValue {
		public string ComputerName;
		public DateTime ExpiresUtc;

		public override string ToString() {
			return ComputerName+"|"+ExpiresUtc.ToString("o",CultureInfo.InvariantCulture);
		}

		///<summary>Null for blank or unreadable text, which counts as "free".</summary>
		public static VendorJobLeaseValue Parse(string text) {
			if(string.IsNullOrEmpty(text)) {
				return null;
			}
			int indexBar=text.LastIndexOf('|');
			if(indexBar<0) {
				return null;
			}
			DateTime expires;
			if(!DateTime.TryParse(text.Substring(indexBar+1),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out expires)) {
				return null;
			}
			VendorJobLeaseValue value=new VendorJobLeaseValue();
			value.ComputerName=text.Substring(0,indexBar);
			value.ExpiresUtc=expires.ToUniversalTime();
			return value;
		}

		///<summary>The lease decision, kept apart from the database for tests: returns the value to write, or null if another computer holds an
		///unexpired lease.</summary>
		public static VendorJobLeaseValue GetNext(string currentText,string computerName,DateTime nowUtc,TimeSpan duration) {
			VendorJobLeaseValue current=Parse(currentText);
			if(current!=null && current.ExpiresUtc>nowUtc && !string.Equals(current.ComputerName,computerName,StringComparison.OrdinalIgnoreCase)) {
				return null;
			}
			VendorJobLeaseValue next=new VendorJobLeaseValue();
			next.ComputerName=computerName;
			next.ExpiresUtc=nowUtc.Add(duration);
			return next;
		}
	}

	///<summary>The lease in a programproperty row of the job's program (PropertyDesc "VendorJobLease.{Name}", ClinicNum 0), taken with
	///ProgramProperties.UpdateValueIfUnchanged. Times come from the database server's clock, so workstation clocks don't matter. These rows are
	///hidden from the program link property grid and never copied to clinics.</summary>
	public sealed class VendorJobLease : IVendorJobLease {
		public const string PropertyDescPrefix="VendorJobLease.";

		///<summary>True for a lease row's PropertyDesc.</summary>
		public static bool IsLeaseProperty(string propertyDesc) {
			return propertyDesc!=null && propertyDesc.StartsWith(PropertyDescPrefix,StringComparison.Ordinal);
		}

		public bool TryAcquire(IVendorBackgroundJob job,TimeSpan duration) {
			Meth.NoCheckMiddleTierRole();
			long programNum=Programs.GetProgramNum(job.ProgramName);
			if(programNum==0) {
				return false;
			}
			ProgramProperty programProperty=ProgramProperties.InsertIfMissing(programNum,PropertyDescPrefix+job.Name,0,"",false,false);
			DateTime nowUtc=MiscData.GetNowDateTime().ToUniversalTime();
			VendorJobLeaseValue next=VendorJobLeaseValue.GetNext(programProperty.PropertyValue,ODEnvironment.MachineName,nowUtc,duration);
			if(next==null) {
				return false;
			}
			return ProgramProperties.UpdateValueIfUnchanged(programProperty.ProgramPropertyNum,programProperty.PropertyValue,next.ToString());
		}
	}

	///<summary>Runs the vendor background jobs that are due. The OpenDental job host calls RunDueJobs about once a minute on a background thread.
	///Each job runs when its interval has passed, its program link is enabled, ShouldRun is true and this workstation takes its lease.</summary>
	public sealed class VendorJobScheduler {
		private readonly List<IVendorBackgroundJob> _listJobs;
		private readonly Func<ProgramName,bool> _funcIsEnabled;
		private readonly IVendorJobLease _lease;
		private readonly Func<DateTime> _funcUtcNow;
		private readonly Action<string> _actionLog;
		private readonly Dictionary<string,DateTime> _dictLastAttemptUtc=new Dictionary<string,DateTime>();

		///<summary>funcIsEnabled defaults to Programs.IsEnabled, lease to VendorJobLease, and actionLog to the "VendorJobs" log folder.</summary>
		public VendorJobScheduler(IEnumerable<IVendorBackgroundJob> listJobs,Func<ProgramName,bool> funcIsEnabled=null,IVendorJobLease lease=null,
			Func<DateTime> funcUtcNow=null,Action<string> actionLog=null)
		{
			_listJobs=(listJobs??Enumerable.Empty<IVendorBackgroundJob>()).Where(x => x!=null).ToList();
			_funcIsEnabled=funcIsEnabled ?? (x => Programs.IsEnabled(x));
			_lease=lease ?? new VendorJobLease();
			_funcUtcNow=funcUtcNow ?? (() => DateTime.UtcNow);
			_actionLog=actionLog ?? (x => Logger.WriteLine(x,"VendorJobs"));
		}

		///<summary>Every job class in OpenDentBusiness and in the given assemblies (pass the OpenDental assembly).</summary>
		public static List<IVendorBackgroundJob> DiscoverJobs(params Assembly[] arrayAssemblies) {
			List<Assembly> listAssemblies=new List<Assembly> { typeof(IVendorBackgroundJob).Assembly };
			listAssemblies.AddRange(arrayAssemblies.Where(x => x!=null));
			return TypeDiscovery.CreateImplementations<IVendorBackgroundJob>(listAssemblies.ToArray());
		}

		public IReadOnlyList<IVendorBackgroundJob> Jobs {
			get {
				return _listJobs;
			}
		}

		///<summary>Runs each due job once. Returns the names of the jobs that ran. A failing job is logged and doesn't stop the others.</summary>
		public List<string> RunDueJobs() {
			List<string> listRan=new List<string>();
			for(int i=0;i<_listJobs.Count;i++) {
				IVendorBackgroundJob job=_listJobs[i];
				DateTime nowUtc=_funcUtcNow();
				DateTime lastAttemptUtc;
				if(_dictLastAttemptUtc.TryGetValue(job.Name,out lastAttemptUtc) && nowUtc-lastAttemptUtc<job.Interval) {
					continue;
				}
				_dictLastAttemptUtc[job.Name]=nowUtc;
				try {
					if(!_funcIsEnabled(job.ProgramName) || !job.ShouldRun()) {
						continue;
					}
					if(!_lease.TryAcquire(job,job.Interval)) {
						continue;
					}
					job.Run();
					listRan.Add(job.Name);
				}
				catch(Exception ex) {
					_actionLog(job.Name+" failed: "+ex.GetType().Name+": "+ex.Message);
				}
			}
			return listRan;
		}
	}
}
