// Average latency (LatencyMeasure, Open Dental 26.2 item 26): the arithmetic without a database, and one real run against the scratch database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262ToolsTests {
	[TestClass]
	public class LatencyMeasureTests {
		[TestMethod]
		public void Measure_RunsTheRoundTripCountTimes_AndAveragesThem() {
			int countCalls=0;
			LatencyResult latencyResult=LatencyMeasure.Measure(7,() => {
				countCalls++;
				if(countCalls==3) {
					Thread.Sleep(30);//one slow round trip
				}
			});
			Assert.AreEqual(7,countCalls);
			Assert.AreEqual(7,latencyResult.Count);
			Assert.IsTrue(latencyResult.MaxMs>=25,"The slow round trip is the slowest: "+latencyResult.MaxMs);
			Assert.IsTrue(latencyResult.MinMs<=latencyResult.AverageMs && latencyResult.AverageMs<=latencyResult.MaxMs);
			Assert.IsTrue(latencyResult.AverageMs*7>=latencyResult.MaxMs-0.001,"The average includes the slow round trip.");
		}

		[TestMethod]
		public void Measure_OneRoundTrip_MinAverageAndMaxAreTheSame() {
			LatencyResult latencyResult=LatencyMeasure.Measure(1,() => { });
			Assert.AreEqual(latencyResult.MinMs,latencyResult.AverageMs);
			Assert.AreEqual(latencyResult.MaxMs,latencyResult.AverageMs);
		}

		[TestMethod]
		public void Measure_CountOutOfRange_Throws() {
			foreach(int count in new[] { 0,-1,LatencyMeasure.COUNT_MAX+1 }) {
				try {
					LatencyMeasure.Measure(count,() => { });
					Assert.Fail("No exception for "+count);
				}
				catch(ArgumentOutOfRangeException) {
				}
			}
			Assert.AreEqual(1,LatencyMeasure.COUNT_MIN);
			Assert.AreEqual(1000,LatencyMeasure.COUNT_MAX);
			Assert.AreEqual(10,LatencyMeasure.COUNT_DEFAULT);
		}

		[TestMethod]
		public void Format_OneDecimal() {
			ToolsDb.SetCulture();
			Assert.AreEqual("12.3",LatencyResult.Format(12.345));
			Assert.AreEqual("0.0",LatencyResult.Format(0.04));
		}

		[TestMethod]
		public void MeasureDatabase_RunsTheSmallQuery() {
			ToolsDb.Begin();
			LatencyResult latencyResult=LatencyMeasure.MeasureDatabase(5);
			Assert.AreEqual(5,latencyResult.Count);
			Assert.IsTrue(latencyResult.AverageMs>0);
			Assert.IsTrue(latencyResult.MinMs<=latencyResult.MaxMs);
		}
	}
}
