using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace UnitTests.MiscUtils_Tests {
	[TestClass]
	public class MiscUtilsTests:TestBase {

		[TestMethod]
		public void MiscUtils_GetDatesInRange() {
			DateTime dateTimeStart=DateTime.Today;
			//DateEnd less than DateStart should return an empty list
			DateTime dateTimeEnd=dateTimeStart.AddDays(-1);
			List<DateTime> listDateTimes=MiscUtils.GetDatesInRange(dateTimeStart,dateTimeEnd);
			Assert.AreEqual(0,listDateTimes.Count);
			//Equal dates should return one date
			dateTimeEnd=dateTimeStart;
			listDateTimes=MiscUtils.GetDatesInRange(dateTimeStart,dateTimeEnd);
			Assert.AreEqual(1,listDateTimes.Count);
			//A range should return the list including start / end dates
			dateTimeEnd=dateTimeStart.AddDays(7);
			listDateTimes=MiscUtils.GetDatesInRange(dateTimeStart,dateTimeEnd);
			Assert.AreEqual(8,listDateTimes.Count);
		}
	}
}
