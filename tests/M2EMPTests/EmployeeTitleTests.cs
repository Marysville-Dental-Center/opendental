// Employee job titles (Open Dental 26.1, feature v26_1-lists-employee-title, package M2-EMP): what the Title drop-down offers and what the
// Employees list shows. No database: the definitions are passed in.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2EMPTests {
	[TestClass]
	public class EmployeeTitleTests {
		///<summary>Synthetic titles, deliberately out of order: Hygienist (visible), Office Manager (hidden), Assistant (visible), and a definition
		///of another category that has the same shape.</summary>
		private static List<Def> GetDefs() {
			return new List<Def> {
				new Def { DefNum=11,Category=DefCat.EmployeeTitles,ItemName="Hygienist",ItemOrder=1 },
				new Def { DefNum=12,Category=DefCat.EmployeeTitles,ItemName="Office Manager",ItemOrder=2,IsHidden=true },
				new Def { DefNum=10,Category=DefCat.EmployeeTitles,ItemName="Assistant",ItemOrder=0 },
				new Def { DefNum=20,Category=DefCat.TimeCardAdjTypes,ItemName="PTO",ItemOrder=0 },
			};
		}

		[TestMethod]
		public void DefCat_EmployeeTitlesIs56() {
			Assert.AreEqual(56,(int)DefCat.EmployeeTitles);
		}

		[TestMethod]
		public void GetTitlesForPicker_VisibleTitlesInOrder() {
			List<Def> listDefs=Employees.GetTitlesForPicker(GetDefs(),0);
			CollectionAssert.AreEqual(new long[] { 10,11 },listDefs.Select(x => x.DefNum).ToArray());
		}

		[TestMethod]
		public void GetTitlesForPicker_HiddenTitleOnlyWhenItIsTheCurrentOne() {
			CollectionAssert.AreEqual(new long[] { 10,11,12 },Employees.GetTitlesForPicker(GetDefs(),12).Select(x => x.DefNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 10,11 },Employees.GetTitlesForPicker(GetDefs(),11).Select(x => x.DefNum).ToArray());
			//A number from another category, or one that no longer exists, is never offered.
			CollectionAssert.AreEqual(new long[] { 10,11 },Employees.GetTitlesForPicker(GetDefs(),20).Select(x => x.DefNum).ToArray());
			CollectionAssert.AreEqual(new long[] { 10,11 },Employees.GetTitlesForPicker(GetDefs(),99).Select(x => x.DefNum).ToArray());
		}

		[TestMethod]
		public void GetTitleText_NameWithHiddenMarker() {
			Assert.AreEqual("Hygienist",Employees.GetTitleText(11,GetDefs()));
			Assert.AreEqual("Office Manager (hidden)",Employees.GetTitleText(12,GetDefs()));
			Assert.AreEqual("",Employees.GetTitleText(0,GetDefs()));
			Assert.AreEqual("",Employees.GetTitleText(99,GetDefs()),"A removed definition shows blank.");
			Assert.AreEqual("",Employees.GetTitleText(20,GetDefs()),"A definition of another category is not a title.");
		}

		[TestMethod]
		public void Employee_CopyKeepsTitle() {
			Employee employee=new Employee { EmployeeNum=5,FName="Synthetic",Title=11 };
			Assert.AreEqual(11,employee.Copy().Title);
		}
	}
}
