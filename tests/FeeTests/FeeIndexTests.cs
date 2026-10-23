// The in-memory fee index ("current" index plus dated rows) gives the same answers as the list lookup. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace FeeTests {
	[TestClass]
	public class FeeIndexTests {
		///<summary>A deterministic spread of rows: 3 schedules, 4 codes, clinics 0-2, providers 0-2, start dates from always to 2032, a few
		///blank (-1) overrides. Not every key has rows.</summary>
		private static List<Fee> GetRows() {
			List<Fee> listFees=new List<Fee>();
			DateTime[] arrayDates=new DateTime[] { DateTime.MinValue,new DateTime(2024,1,1),new DateTime(2026,7,1),new DateTime(2028,1,1),
				new DateTime(2032,1,1) };
			long feeNum=1;
			for(long sched=1;sched<=3;sched++) {
				for(long code=1;code<=4;code++) {
					for(long clinic=0;clinic<=2;clinic++) {
						for(long prov=0;prov<=2;prov++) {
							for(int d=0;d<arrayDates.Length;d++) {
								//Skip some combinations so that fallbacks are exercised.
								if((sched+code+clinic*3+prov*5+d)%3==0) {
									continue;
								}
								Fee fee=new Fee();
								fee.FeeNum=feeNum++;
								fee.FeeSched=sched;
								fee.CodeNum=code;
								fee.ClinicNum=clinic;
								fee.ProvNum=prov;
								fee.DateEffective=arrayDates[d];
								fee.Amount=(fee.FeeNum%17==0 ? -1 : 10*fee.FeeNum);
								listFees.Add(fee);
							}
						}
					}
				}
			}
			return listFees;
		}

		[TestMethod]
		public void FeeIndex_SameAnswersAsListLookup() {
			List<Fee> listFees=GetRows();
			DateTime dateCurrent=new DateTime(2026,10,19);
			FeeIndex feeIndex=new FeeIndex(listFees,dateCurrent);
			DateTime[] arrayDatesAsOf=new DateTime[] { new DateTime(2000,1,1),new DateTime(2024,1,1),new DateTime(2026,6,30),dateCurrent,
				new DateTime(2028,1,1),new DateTime(2040,1,1) };
			int countCompared=0;
			for(long sched=1;sched<=3;sched++) {
				for(long code=1;code<=5;code++) {
					for(long clinic=0;clinic<=3;clinic++) {
						for(long prov=0;prov<=3;prov++) {
							for(int g=0;g<2;g++) {
								bool isGlobal=(g==1);
								for(int e=0;e<2;e++) {
									bool doGetExactMatch=(e==1);
									foreach(DateTime dateAsOf in arrayDatesAsOf) {
										Fee feeList=Fees.GetFeeFromList(listFees,code,sched,clinic,prov,isGlobal,dateAsOf,doGetExactMatch);
										Fee feeIndexed=feeIndex.GetFee(code,sched,clinic,prov,isGlobal,dateAsOf,doGetExactMatch);
										Assert.AreSame(feeList,feeIndexed,"sched "+sched+" code "+code+" clinic "+clinic+" prov "+prov+" global "+isGlobal
											+" exact "+doGetExactMatch+" as of "+dateAsOf.ToShortDateString());
										countCompared++;
									}
								}
							}
						}
					}
				}
			}
			Assert.IsTrue(countCompared>1000);
		}

		[TestMethod]
		public void FeeIndex_CurrentIndexIsTheDefaultDate() {
			List<Fee> listFees=new List<Fee> {
				new Fee { FeeNum=1,FeeSched=1,CodeNum=1,Amount=100,DateEffective=DateTime.MinValue },
				new Fee { FeeNum=2,FeeSched=1,CodeNum=1,Amount=110,DateEffective=new DateTime(2026,1,1) },
				new Fee { FeeNum=3,FeeSched=1,CodeNum=1,Amount=150,DateEffective=new DateTime(2030,1,1) },
				new Fee { FeeNum=4,FeeSched=1,CodeNum=2,Amount=200,DateEffective=new DateTime(2030,1,1) },
			};
			FeeIndex feeIndex=new FeeIndex(listFees,new DateTime(2026,10,19));
			Assert.AreEqual(new DateTime(2026,10,19),feeIndex.DateCurrent);
			Assert.AreEqual(2,feeIndex.CountKeys);
			//Code 2 only has a future row, so it has nothing in the current index.
			Assert.AreEqual(1,feeIndex.CountCurrent);
			Assert.AreEqual(2,feeIndex.GetExact(1,1,0,0).FeeNum);
			Assert.IsNull(feeIndex.GetExact(1,2,0,0));
			Assert.AreEqual(4,feeIndex.GetExact(1,2,0,0,new DateTime(2030,1,1)).FeeNum);
			Assert.AreEqual(1,feeIndex.GetExact(1,1,0,0,new DateTime(2025,12,31)).FeeNum);
			CollectionAssert.AreEqual(new long[] { 1,2,3 },feeIndex.GetAllDates(1,1,0,0).Select(x => x.FeeNum).ToArray());
			Assert.AreEqual(0,feeIndex.GetAllDates(9,9,0,0).Count);
		}

		[TestMethod]
		public void FeeIndex_DefaultsToToday() {
			FeeIndex feeIndex=new FeeIndex(new List<Fee>());
			Assert.AreEqual(DateTime.Today,feeIndex.DateCurrent);
			Assert.IsNull(feeIndex.GetFee(1,1,0,0,false));
		}
	}
}
