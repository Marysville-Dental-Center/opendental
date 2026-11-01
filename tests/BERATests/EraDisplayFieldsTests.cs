// v25_3-account-era-displayfields and its 25.4 twin v25_4-claims-era-displayfields: the ERA category of Display Fields (defaults, available
// columns, required columns, cleaning a stored list, and the cell text of each column). No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using static BERATests.EraObjects;

namespace BERATests {
	[TestClass]
	public class EraDisplayFieldsTests {
		private static readonly string[] _arrayNames243=new[] {
			"Recd","Patient","DateService","Claim Identifier","Payor Control#","Status","ClaimFee","InsPaid","PatPort","Deduct","Writeoff" };

		[TestInitialize]
		public void SetCulture() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		[TestMethod]
		public void Category_IsOpenDentalNumber23() {
			Assert.AreEqual(23,(int)DisplayFieldCategory.ERA);
		}

		[TestMethod]
		public void DefaultList_Is243GridInOrderAndWidths() {
			List<DisplayField> listDefaults=DisplayFields.GetDefaultList(DisplayFieldCategory.ERA);
			CollectionAssert.AreEqual(_arrayNames243,listDefaults.Select(x => x.InternalName).ToArray());
			CollectionAssert.AreEqual(new[] { 32,70,80,50,56,70,70,70,70,70,70 },listDefaults.Select(x => x.ColumnWidth).ToArray());
			Assert.IsTrue(listDefaults.All(x => x.Category==DisplayFieldCategory.ERA && x.Description==""));
		}

		[TestMethod]
		public void AvailableList_Is243GridPlusForkExtras() {
			List<string> listNames=DisplayFields.GetAllAvailableList(DisplayFieldCategory.ERA).Select(x => x.InternalName).ToList();
			CollectionAssert.AreEqual(_arrayNames243.Concat(new[] { "Subscriber ID","Allowed","Matched Claim" }).ToArray(),listNames.ToArray());
		}

		[TestMethod]
		public void RequiredFields_RecdAndPatientOnlyForEra() {
			Assert.IsTrue(DisplayFields.IsRequiredField(DisplayFieldCategory.ERA,"Recd"));
			Assert.IsTrue(DisplayFields.IsRequiredField(DisplayFieldCategory.ERA,"Patient"));
			Assert.IsFalse(DisplayFields.IsRequiredField(DisplayFieldCategory.ERA,"Status"));
			Assert.IsFalse(DisplayFields.IsRequiredField(DisplayFieldCategory.AccountModule,"Patient"));
		}

		[TestMethod]
		public void GridColumns_StoredListKeepsOrderWidthsAndDescriptions() {
			List<DisplayField> listStored=new List<DisplayField> {
				new DisplayField("Patient",120,DisplayFieldCategory.ERA) { Description="Who" },
				new DisplayField("Recd",40,DisplayFieldCategory.ERA),
				new DisplayField("Allowed",65,DisplayFieldCategory.ERA),
			};
			List<DisplayField> listColumns=DisplayFields.GetEraClaimPaidColumns(listStored);
			CollectionAssert.AreEqual(new[] { "Patient","Recd","Allowed" },listColumns.Select(x => x.InternalName).ToArray());
			Assert.AreEqual("Who",listColumns[0].Description);
			Assert.AreEqual(120,listColumns[0].ColumnWidth);
			Assert.AreNotSame(listStored[0],listColumns[0]);
		}

		[TestMethod]
		public void GridColumns_MissingRequiredPutBack_UnknownDropped() {
			List<DisplayField> listStored=new List<DisplayField> {
				new DisplayField("InsPaid",70,DisplayFieldCategory.ERA),
				new DisplayField("Some Newer Column",70,DisplayFieldCategory.ERA),
				new DisplayField("Writeoff",70,DisplayFieldCategory.ERA),
			};
			List<DisplayField> listColumns=DisplayFields.GetEraClaimPaidColumns(listStored);
			CollectionAssert.AreEqual(new[] { "Recd","Patient","InsPaid","Writeoff" },listColumns.Select(x => x.InternalName).ToArray());
		}

		[TestMethod]
		public void GridColumns_PatientGoesRightAfterRecd() {
			List<DisplayField> listStored=new List<DisplayField> {
				new DisplayField("Status",70,DisplayFieldCategory.ERA),
				new DisplayField("Recd",32,DisplayFieldCategory.ERA),
			};
			CollectionAssert.AreEqual(new[] { "Status","Recd","Patient" },DisplayFields.GetEraClaimPaidColumns(listStored).Select(x => x.InternalName).ToArray());
		}

		[TestMethod]
		public void GridColumns_NothingUsable_Defaults() {
			CollectionAssert.AreEqual(_arrayNames243,DisplayFields.GetEraClaimPaidColumns(new List<DisplayField>()).Select(x => x.InternalName).ToArray());
			CollectionAssert.AreEqual(_arrayNames243,DisplayFields.GetEraClaimPaidColumns(null).Select(x => x.InternalName).ToArray());
			List<DisplayField> listUnknown=new List<DisplayField> { new DisplayField("Unknown",50,DisplayFieldCategory.ERA) };
			CollectionAssert.AreEqual(_arrayNames243,DisplayFields.GetEraClaimPaidColumns(listUnknown).Select(x => x.InternalName).ToArray());
		}

		[TestMethod]
		public void Headers_243TitlesWithLineBreaks() {
			Assert.AreEqual("Claim\r\nIdentifier",DisplayFields.EraClaimPaid.GetDefaultHeader("Claim Identifier"));
			Assert.AreEqual("Payor\r\nControl#",DisplayFields.EraClaimPaid.GetDefaultHeader("Payor Control#"));
			Assert.AreEqual("Recd",DisplayFields.EraClaimPaid.GetDefaultHeader("Recd"));
		}

		[TestMethod]
		public void AlignmentAndDynamicWidth_As243() {
			Assert.AreEqual(HorizontalAlignment.Center,DisplayFields.EraClaimPaid.GetAlignment("Recd"));
			Assert.AreEqual(HorizontalAlignment.Center,DisplayFields.EraClaimPaid.GetAlignment("DateService"));
			Assert.AreEqual(HorizontalAlignment.Center,DisplayFields.EraClaimPaid.GetAlignment("Payor Control#"));
			Assert.AreEqual(HorizontalAlignment.Left,DisplayFields.EraClaimPaid.GetAlignment("Patient"));
			Assert.AreEqual(HorizontalAlignment.Left,DisplayFields.EraClaimPaid.GetAlignment("Claim Identifier"));
			Assert.AreEqual(HorizontalAlignment.Right,DisplayFields.EraClaimPaid.GetAlignment("InsPaid"));
			Assert.AreEqual(HorizontalAlignment.Right,DisplayFields.EraClaimPaid.GetAlignment("Allowed"));
			Assert.IsTrue(DisplayFields.EraClaimPaid.IsWidthDynamic("Patient"));
			Assert.IsTrue(DisplayFields.EraClaimPaid.IsWidthDynamic("Status"));
			Assert.IsFalse(DisplayFields.EraClaimPaid.IsWidthDynamic("Recd"));
		}

		[TestMethod]
		public void CellText_EveryColumn() {
			Hx835_Claim claim=EraClaim(EraProc("Z0120",60,40,1001,EraAdj("CO","45",12),EraAdj("PR","1",8)),EraProc("Z0220",40,0,1002,EraAdj("CO","97",35),EraAdj("PR","2",5)));
			claim.DateServiceEnd=new System.DateTime(2026,9,3);
			claim.SubscriberName=new Hx835_Name { Fname="Sub",Mname="",Lname="Scriber",Suffix="",SubscriberId="SUBX9",SubscriberIdTypeDesc="Member ID" };
			claim.ClaimNum=0;
			claim.IsAttachedToClaim=false;
			Assert.AreEqual("X",DisplayFields.EraClaimPaid.GetCellText("Recd",claim,"X"));
			Assert.AreEqual("Synthetic Patient - Member ID: SUB123",DisplayFields.EraClaimPaid.GetCellText("Patient",claim,""));
			Assert.AreEqual("9/1/2026 to 9/3/2026",DisplayFields.EraClaimPaid.GetCellText("DateService",claim,""));
			Assert.AreEqual("SYN1001",DisplayFields.EraClaimPaid.GetCellText("Claim Identifier",claim,""));
			Assert.AreEqual("PCN77",DisplayFields.EraClaimPaid.GetCellText("Payor Control#",claim,""));
			Assert.AreEqual("Processed as Primary",DisplayFields.EraClaimPaid.GetCellText("Status",claim,""));
			Assert.AreEqual("100.00",DisplayFields.EraClaimPaid.GetCellText("ClaimFee",claim,""));
			Assert.AreEqual("40.00",DisplayFields.EraClaimPaid.GetCellText("InsPaid",claim,""));
			Assert.AreEqual("5.00",DisplayFields.EraClaimPaid.GetCellText("PatPort",claim,""));
			Assert.AreEqual("8.00",DisplayFields.EraClaimPaid.GetCellText("Deduct",claim,""));
			Assert.AreEqual("47.00",DisplayFields.EraClaimPaid.GetCellText("Writeoff",claim,""));
			Assert.AreEqual("SUBX9",DisplayFields.EraClaimPaid.GetCellText("Subscriber ID",claim,""));
			Assert.AreEqual("53.00",DisplayFields.EraClaimPaid.GetCellText("Allowed",claim,""));
			Assert.AreEqual("",DisplayFields.EraClaimPaid.GetCellText("Matched Claim",claim,""));
			Assert.AreEqual("",DisplayFields.EraClaimPaid.GetCellText("Unknown",claim,""));
		}

		[TestMethod]
		public void CellText_SubscriberIdFallsBackToPatientAndMatchedClaim() {
			Hx835_Claim claim=EraClaim(EraProc("Z0120",60,48,1001));
			claim.SubscriberName=new Hx835_Name { Fname="",Mname="",Lname="",Suffix="",SubscriberId="",SubscriberIdTypeDesc="" };
			Assert.AreEqual("SUB123",DisplayFields.EraClaimPaid.GetCellText("Subscriber ID",claim,""));
			claim.ClaimNum=4321;
			claim.IsAttachedToClaim=true;
			Assert.AreEqual("4321",DisplayFields.EraClaimPaid.GetCellText("Matched Claim",claim,""));
			claim.ClaimNum=0;
			Assert.AreEqual("Detached",DisplayFields.EraClaimPaid.GetCellText("Matched Claim",claim,""));
		}

		[TestMethod]
		public void CellText_SingleDayService() {
			Hx835_Claim claim=EraClaim(EraProc("Z0120",60,48,1001));
			Assert.AreEqual("9/1/2026",DisplayFields.EraClaimPaid.GetCellText("DateService",claim,""));
		}

		[TestMethod]
		public void HasAvailableFields_EraYes_PlaceholderOnlyOnceItHasFields() {
			Assert.IsTrue(DisplayFields.HasAvailableFields(DisplayFieldCategory.ERA));
			Assert.IsTrue(DisplayFields.HasAvailableFields(DisplayFieldCategory.OrthoChart));
			//22 is the 25.4 Account service date view, built by another package: listed once it has fields.
			DisplayFieldCategory category22=DisplayFieldCategory.AccountModuleServiceDateView;
			Assert.AreEqual(DisplayFields.GetAllAvailableList(category22).Count>0,DisplayFields.HasAvailableFields(category22));
		}
	}
}
