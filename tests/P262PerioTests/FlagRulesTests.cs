// The rule behind adding a flag to the selected teeth (Open Dental 26.2 Perio Chart item 14), without a database: added to every site, or taken
// off every site when all of them already have it; a site's other flags stay.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262PerioTests {
	[TestClass]
	public class FlagRulesTests {
		[TestMethod]
		public void IsFlagOnEverySite() {
			Assert.IsTrue(PerioMeasures.IsFlagOnEverySite(new[] { BleedingFlags.Plaque,BleedingFlags.Plaque|BleedingFlags.Blood },BleedingFlags.Plaque));
			Assert.IsFalse(PerioMeasures.IsFlagOnEverySite(new[] { BleedingFlags.Plaque,BleedingFlags.Blood },BleedingFlags.Plaque));
			Assert.IsFalse(PerioMeasures.IsFlagOnEverySite(new List<BleedingFlags>(),BleedingFlags.Plaque),"No sites: nothing to remove.");
			Assert.IsFalse(PerioMeasures.IsFlagOnEverySite(null,BleedingFlags.Plaque));
			Assert.IsFalse(PerioMeasures.IsFlagOnEverySite(new[] { BleedingFlags.Plaque },BleedingFlags.None));
			Assert.IsTrue(PerioMeasures.IsFlagOnEverySite(new[] { BleedingFlags.Plaque|BleedingFlags.Calculus },BleedingFlags.Plaque|BleedingFlags.Calculus));
			Assert.IsFalse(PerioMeasures.IsFlagOnEverySite(new[] { BleedingFlags.Plaque },BleedingFlags.Plaque|BleedingFlags.Calculus),
				"Several flags at once: every site needs all of them.");
		}

		[TestMethod]
		public void GetBleedingWithFlag() {
			Assert.AreEqual(BleedingFlags.Plaque|BleedingFlags.Calculus,PerioMeasures.GetBleedingWithFlag(BleedingFlags.Calculus,BleedingFlags.Plaque,doRemove:false));
			Assert.AreEqual(BleedingFlags.Plaque,PerioMeasures.GetBleedingWithFlag(BleedingFlags.Plaque,BleedingFlags.Plaque,doRemove:false),"Added once.");
			Assert.AreEqual(BleedingFlags.Calculus,PerioMeasures.GetBleedingWithFlag(BleedingFlags.Plaque|BleedingFlags.Calculus,BleedingFlags.Plaque,doRemove:true));
			Assert.AreEqual(BleedingFlags.None,PerioMeasures.GetBleedingWithFlag(BleedingFlags.None,BleedingFlags.Blood,doRemove:true));
			Assert.AreEqual(BleedingFlags.Suppuration,PerioMeasures.GetBleedingWithFlag(BleedingFlags.Blood|BleedingFlags.Suppuration,BleedingFlags.Blood,
				doRemove:true));
		}
	}
}
