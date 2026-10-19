// Tests for the vendor background job scheduler and its lease rules. No database: the lease and the enabled check are faked.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.Foundation {
	[TestClass]
	public class VendorJobTests {
		private sealed class FakeJob : IVendorBackgroundJob {
			public string Name { get; set; }
			public ProgramName ProgramName { get; set; }
			public TimeSpan Interval { get; set; }
			public bool IsReady=true;
			public int CountRuns;
			public bool DoThrow;

			public bool ShouldRun() {
				return IsReady;
			}

			public void Run() {
				CountRuns++;
				if(DoThrow) {
					throw new InvalidOperationException("boom");
				}
			}
		}

		private sealed class FakeLease : IVendorJobLease {
			public bool IsGranted=true;
			public List<string> ListRequested=new List<string>();

			public bool TryAcquire(IVendorBackgroundJob job,TimeSpan duration) {
				ListRequested.Add(job.Name+"/"+duration.TotalMinutes);
				return IsGranted;
			}
		}

		[TestMethod]
		public void Scheduler_RunsOnlyEnabledReadyLeasedJobsWhenDue() {
			DateTime nowUtc=new DateTime(2026,10,19,12,0,0,DateTimeKind.Utc);
			FakeJob jobPaySimple=new FakeJob { Name="PaySimpleAch",ProgramName=ProgramName.PaySimple,Interval=TimeSpan.FromMinutes(15) };
			FakeJob jobEdge=new FakeJob { Name="EdgeReconcile",ProgramName=ProgramName.EdgeExpress,Interval=TimeSpan.FromMinutes(5) };
			FakeJob jobNotReady=new FakeJob { Name="NotReady",ProgramName=ProgramName.PaySimple,Interval=TimeSpan.FromMinutes(5),IsReady=false };
			FakeLease lease=new FakeLease();
			List<string> listLog=new List<string>();
			VendorJobScheduler scheduler=new VendorJobScheduler(new IVendorBackgroundJob[] { jobPaySimple,jobEdge,jobNotReady },
				x => x==ProgramName.PaySimple,lease,() => nowUtc,listLog.Add);
			CollectionAssert.AreEqual(new[] { "PaySimpleAch" },scheduler.RunDueJobs());
			Assert.AreEqual(0,jobEdge.CountRuns,"EdgeExpress is not enabled.");
			Assert.AreEqual(0,jobNotReady.CountRuns);
			CollectionAssert.AreEqual(new[] { "PaySimpleAch/15" },lease.ListRequested,"The lease lasts one interval.");
			nowUtc=nowUtc.AddMinutes(10);
			Assert.AreEqual(0,scheduler.RunDueJobs().Count,"Not due yet.");
			nowUtc=nowUtc.AddMinutes(5);
			lease.IsGranted=false;
			Assert.AreEqual(0,scheduler.RunDueJobs().Count,"Another workstation holds the lease.");
			Assert.AreEqual(1,jobPaySimple.CountRuns);
			Assert.AreEqual(0,listLog.Count);
		}

		[TestMethod]
		public void Scheduler_FailingJobIsLoggedAndOthersStillRun() {
			FakeJob jobBad=new FakeJob { Name="Bad",ProgramName=ProgramName.PaySimple,Interval=TimeSpan.FromMinutes(1),DoThrow=true };
			FakeJob jobGood=new FakeJob { Name="Good",ProgramName=ProgramName.PaySimple,Interval=TimeSpan.FromMinutes(1) };
			List<string> listLog=new List<string>();
			VendorJobScheduler scheduler=new VendorJobScheduler(new IVendorBackgroundJob[] { jobBad,jobGood },x => true,new FakeLease(),
				() => DateTime.UtcNow,listLog.Add);
			CollectionAssert.AreEqual(new[] { "Good" },scheduler.RunDueJobs());
			Assert.AreEqual(1,listLog.Count);
			StringAssert.Contains(listLog[0],"Bad failed");
		}

		[TestMethod]
		public void Discovery_FindsNoJobsYet() {
			//The foundation adds no job of its own; vendor packages add theirs. Discovery must not throw on the business assembly.
			List<IVendorBackgroundJob> listJobs=VendorJobScheduler.DiscoverJobs();
			Assert.IsNotNull(listJobs);
		}

		[TestMethod]
		public void LeaseValue_FreeExpiredOwnOrTaken() {
			DateTime nowUtc=new DateTime(2026,10,19,12,0,0,DateTimeKind.Utc);
			TimeSpan duration=TimeSpan.FromMinutes(15);
			VendorJobLeaseValue next=VendorJobLeaseValue.GetNext("",@"WS1",nowUtc,duration);
			Assert.AreEqual("WS1",next.ComputerName);
			Assert.AreEqual(nowUtc.AddMinutes(15),next.ExpiresUtc);
			string text=next.ToString();
			VendorJobLeaseValue parsed=VendorJobLeaseValue.Parse(text);
			Assert.AreEqual("WS1",parsed.ComputerName);
			Assert.AreEqual(next.ExpiresUtc,parsed.ExpiresUtc);
			Assert.IsNull(VendorJobLeaseValue.GetNext(text,"WS2",nowUtc.AddMinutes(5),duration),"Held by WS1.");
			Assert.IsNotNull(VendorJobLeaseValue.GetNext(text,"ws1",nowUtc.AddMinutes(5),duration),"The holder may renew.");
			Assert.IsNotNull(VendorJobLeaseValue.GetNext(text,"WS2",nowUtc.AddMinutes(16),duration),"Expired.");
			Assert.IsNotNull(VendorJobLeaseValue.GetNext("garbage",@"WS2",nowUtc,duration),"Unreadable counts as free.");
			Assert.IsTrue(VendorJobLease.IsLeaseProperty("VendorJobLease.PaySimpleAch"));
			Assert.IsFalse(VendorJobLease.IsLeaseProperty("PaySimpleApiKey"));
		}
	}
}
