// Average latency for the Test Latency window (Open Dental 26.2 item 26, "Added a new method to the latency test tool to measure average latency";
// package P262-TOOLS). Written from the public release note and the public Test Latency manual page (latency is the round trip between the computer
// and the database, in milliseconds). The number of round trips and the figures shown are this program's own choices (INFERRED,
// docs/port-notes/P262-TOOLS.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Diagnostics;

namespace OpenPmsDentalBusiness {
	///<summary>Measures the round trip of a very small query several times and reports the average, the fastest and the slowest.</summary>
	public static class LatencyMeasure {
		///<summary>The number of round trips the window starts with.</summary>
		public const int COUNT_DEFAULT=10;
		public const int COUNT_MIN=1;
		public const int COUNT_MAX=1000;

		///<summary>Runs the same very small query as the single latency test (the server version) count times, through the same connection the
		///program uses (the Middle Tier, if this workstation uses one), and returns the times. Throws for a count outside COUNT_MIN to COUNT_MAX.</summary>
		public static LatencyResult MeasureDatabase(int count) {
			Meth.NoCheckMiddleTierRole();
			return Measure(count,() => MiscData.GetMySqlVersion());
		}

		///<summary>Runs actionRoundTrip count times, timing each run on its own, and returns the times in milliseconds (fractions included).</summary>
		public static LatencyResult Measure(int count,Action actionRoundTrip) {
			Meth.NoCheckMiddleTierRole();
			if(count<COUNT_MIN || count>COUNT_MAX) {
				throw new ArgumentOutOfRangeException(nameof(count),"The number of round trips must be from "+COUNT_MIN+" to "+COUNT_MAX+".");
			}
			LatencyResult latencyResult=new LatencyResult();
			Stopwatch stopwatch=new Stopwatch();
			double total=0;
			for(int i=0;i<count;i++) {
				stopwatch.Restart();
				actionRoundTrip();
				stopwatch.Stop();
				double milliseconds=stopwatch.Elapsed.TotalMilliseconds;
				total+=milliseconds;
				if(i==0 || milliseconds<latencyResult.MinMs) {
					latencyResult.MinMs=milliseconds;
				}
				if(i==0 || milliseconds>latencyResult.MaxMs) {
					latencyResult.MaxMs=milliseconds;
				}
			}
			latencyResult.Count=count;
			latencyResult.AverageMs=total/count;
			return latencyResult;
		}
	}

	///<summary>The result of LatencyMeasure: times in milliseconds.</summary>
	public class LatencyResult {
		public int Count;
		public double AverageMs;
		public double MinMs;
		public double MaxMs;

		///<summary>"12.3" style text, one decimal.</summary>
		public static string Format(double milliseconds) {
			return milliseconds.ToString("0.0");
		}
	}
}
