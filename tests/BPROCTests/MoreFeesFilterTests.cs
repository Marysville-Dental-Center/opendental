// The Provider and Clinic filters of More Fees (Open Dental 25.1) without a database. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BPROCTests {
	[TestClass]
	public class MoreFeesFilterTests {
		private static List<Fee> Fees() {
			return new List<Fee> {
				new Fee { FeeNum=1,FeeSched=10,ClinicNum=0,ProvNum=0,Amount=100 },
				new Fee { FeeNum=2,FeeSched=10,ClinicNum=0,ProvNum=7,Amount=110 },
				new Fee { FeeNum=3,FeeSched=10,ClinicNum=5,ProvNum=0,Amount=120 },
				new Fee { FeeNum=4,FeeSched=10,ClinicNum=5,ProvNum=7,Amount=130 },
				new Fee { FeeNum=5,FeeSched=20,ClinicNum=6,ProvNum=8,Amount=140 },
			};
		}

		private static long[] Nums(List<Fee> listFees) {
			return listFees.Select(x => x.FeeNum).ToArray();
		}

		[TestMethod]
		public void Filter_AllAll_KeepsTodaysList() {
			CollectionAssert.AreEqual(new long[] { 1,2,3,4,5 },Nums(MoreFeesFilters.Filter(Fees(),MoreFeesFilters.ALL,MoreFeesFilters.ALL)));
			Assert.IsTrue(MoreFeesFilters.IsDefaultFeeShown(MoreFeesFilters.ALL,MoreFeesFilters.ALL));
		}

		[TestMethod]
		public void Filter_Provider() {
			CollectionAssert.AreEqual(new long[] { 2,4 },Nums(MoreFeesFilters.Filter(Fees(),7,MoreFeesFilters.ALL)));
			CollectionAssert.AreEqual(new long[] { 1,3 },Nums(MoreFeesFilters.Filter(Fees(),0,MoreFeesFilters.ALL)),"Default/none: no provider.");
			Assert.IsFalse(MoreFeesFilters.IsDefaultFeeShown(7,MoreFeesFilters.ALL),"No blank default rows when one provider is picked.");
			Assert.IsTrue(MoreFeesFilters.IsDefaultFeeShown(0,MoreFeesFilters.ALL));
		}

		[TestMethod]
		public void Filter_Clinic() {
			CollectionAssert.AreEqual(new long[] { 3,4 },Nums(MoreFeesFilters.Filter(Fees(),MoreFeesFilters.ALL,5)));
			CollectionAssert.AreEqual(new long[] { 1,2 },Nums(MoreFeesFilters.Filter(Fees(),MoreFeesFilters.ALL,0)),"Default/HQ: clinic 0.");
			Assert.IsFalse(MoreFeesFilters.IsDefaultFeeShown(MoreFeesFilters.ALL,5));
			Assert.IsTrue(MoreFeesFilters.IsDefaultFeeShown(0,0));
		}

		[TestMethod]
		public void Filter_Both() {
			CollectionAssert.AreEqual(new long[] { 4 },Nums(MoreFeesFilters.Filter(Fees(),7,5)));
			CollectionAssert.AreEqual(new long[] { 1 },Nums(MoreFeesFilters.Filter(Fees(),0,0)));
			Assert.AreEqual(0,MoreFeesFilters.Filter(Fees(),8,5).Count);
			Assert.AreEqual(0,MoreFeesFilters.Filter(null,MoreFeesFilters.ALL,MoreFeesFilters.ALL).Count);
		}

		[TestMethod]
		public void GetProvidersForFilter_HiddenOnlyWithFees() {
			List<Provider> listProviders=new List<Provider> {
				new Provider { ProvNum=7,Abbr="SynA" },
				new Provider { ProvNum=8,Abbr="SynB",IsHidden=true },
				new Provider { ProvNum=9,Abbr="SynC",IsHidden=true },
			};
			CollectionAssert.AreEqual(new long[] { 7,8 },MoreFeesFilters.GetProvidersForFilter(listProviders,Fees()).Select(x => x.ProvNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 7 },MoreFeesFilters.GetProvidersForFilter(listProviders,null).Select(x => x.ProvNum).ToArray());
		}
	}
}
