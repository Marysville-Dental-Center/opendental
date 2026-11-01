// Tests for the Percent of Fee calculation (v26_1-account-adjust-percent-of-fee) and the default percentage of an adjustment type
// (v26_1-setup-adjtype-default-percent), in OpenDentBusiness/Data Interface/Adjustments.cs. No database: these methods only compute.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2ADJTests {
	[TestClass]
	public class PercentOfFeeTests {
		private static readonly CultureInfo _cultureUs=CultureInfo.GetCultureInfo("en-US");
		///<summary>A culture with a decimal comma and a period as group separator.</summary>
		private static readonly CultureInfo _cultureDe=CultureInfo.GetCultureInfo("de-DE");

		[TestInitialize]
		public void TestInitialize() {
			TestPrefs.EnsureFilled();//new Procedure() reads a preference.
		}

		private static Def AdjType(string itemValue,string supp) {
			return new Def { DefNum=1,Category=DefCat.AdjTypes,ItemName="Synthetic type",ItemValue=itemValue,Supp=supp };
		}

		#region Amount
		[TestMethod]
		public void Amount_IsPercentOfFeeRoundedToCents() {
			Assert.AreEqual(15.00,Adjustments.GetPercentOfFeeAmount(150,10m));
			Assert.AreEqual(12.50,Adjustments.GetPercentOfFeeAmount(99.99,12.5m));//12.49875
			Assert.AreEqual(33.33,Adjustments.GetPercentOfFeeAmount(100,33.33m));
			Assert.AreEqual(0.00,Adjustments.GetPercentOfFeeAmount(250,0m));
			Assert.AreEqual(0.00,Adjustments.GetPercentOfFeeAmount(0,50m));
		}

		[TestMethod]
		public void Amount_HalfCentRoundsAwayFromZero() {
			Assert.AreEqual(0.01,Adjustments.GetPercentOfFeeAmount(0.05,10m));//0.005
			Assert.AreEqual(1.23,Adjustments.GetPercentOfFeeAmount(12.25,10m));//1.225
			Assert.AreEqual(-1.23,Adjustments.GetPercentOfFeeAmount(-12.25,10m));//a negative fee keeps its sign
		}

		[TestMethod]
		public void Amount_OverOneHundredPercentForSurcharges() {
			Assert.AreEqual(150.00,Adjustments.GetPercentOfFeeAmount(100,150m));
			Assert.AreEqual(1234.50,Adjustments.GetPercentOfFeeAmount(123.45,1000m));
		}

		[TestMethod]
		public void Sign_ComesFromTheAdjustmentType() {
			Assert.AreEqual(25.0,Adjustments.ApplyAdjTypeSign(25,AdjType("+","")));
			Assert.AreEqual(-25.0,Adjustments.ApplyAdjTypeSign(25,AdjType("-","")));
			Assert.AreEqual(-25.0,Adjustments.ApplyAdjTypeSign(25,AdjType("dp","")));
			Assert.AreEqual(25.0,Adjustments.ApplyAdjTypeSign(25,null));
		}

		[TestMethod]
		public void Basis_IsTheProcedureTotalFee_AsAddMultipleAdjustmentsShowsIt() {
			Procedure procedure=new Procedure { ProcNum=7,PatNum=3,ProcFee=80,UnitQty=3,BaseUnits=0,ProcDate=new DateTime(2026,10,1) };
			Assert.AreEqual(240.0,Adjustments.GetPercentOfFeeBasis(procedure));
			//Add Multiple Adjustments reads the Fee column, AccountEntry.AmountOriginal, as its basis.
			Assert.AreEqual((double)new AccountEntry(procedure).AmountOriginal,Adjustments.GetPercentOfFeeBasis(procedure));
			Assert.AreEqual(0.0,Adjustments.GetPercentOfFeeBasis(null));
		}

		[TestMethod]
		public void AdjAmt_SubtractionTypeIsNegative_AdditionTypeIsPositive() {
			Procedure procedure=new Procedure { ProcNum=7,PatNum=3,ProcFee=200,UnitQty=1 };
			Assert.AreEqual(-20.0,Adjustments.GetPercentOfFeeAdjAmt(procedure,10m,AdjType("-","")));
			Assert.AreEqual(20.0,Adjustments.GetPercentOfFeeAdjAmt(procedure,10m,AdjType("+","")));
			Assert.AreEqual(-30.5,Adjustments.GetPercentOfFeeAdjAmt(procedure,15.25m,AdjType("-","")));
		}

		[TestMethod]
		public void Amount_MatchesTheOldAddMultipleAdjustmentsFormula_AwayFromHalfCents() {
			//Add Multiple Adjustments used Math.Round(percent/100*fee,2) on doubles. Both windows now share GetPercentOfFeeAmount; the results are the
			//same except where the exact result ends in half a cent.
			double[] arrayFees={ 0,1,12.34,99.99,100,149.5,250,1234.56,5000 };
			decimal[] arrayPercents={ 0,1,5,10,12.5m,15.75m,33.33m,50,99.99m,100,150,1000 };
			foreach(double fee in arrayFees) {
				foreach(decimal percent in arrayPercents) {
					decimal exact=(decimal)fee*percent/100m;
					if(exact*100m%1m==0.5m) {
						continue;
					}
					double old=Math.Round(((double)percent/100)*fee,2);
					Assert.AreEqual(old,Adjustments.GetPercentOfFeeAmount(fee,percent),0.0000001,"fee "+fee+", percent "+percent);
				}
			}
		}
		#endregion Amount

		#region Percentage entry
		[TestMethod]
		public void Valid_ZeroToOneThousandWithTwoDecimals() {
			Assert.IsTrue(Adjustments.IsValidPercentOfFee(0m));
			Assert.IsTrue(Adjustments.IsValidPercentOfFee(12.34m));
			Assert.IsTrue(Adjustments.IsValidPercentOfFee(100m));
			Assert.IsTrue(Adjustments.IsValidPercentOfFee(1000m));
			Assert.IsTrue(Adjustments.IsValidPercentOfFee(12.50m));
			Assert.IsFalse(Adjustments.IsValidPercentOfFee(1000.01m));
			Assert.IsFalse(Adjustments.IsValidPercentOfFee(-0.01m));
			Assert.IsFalse(Adjustments.IsValidPercentOfFee(12.345m));
		}

		[TestMethod]
		public void Parse_UsesTheUsersCulture() {
			Assert.IsTrue(Adjustments.TryParsePercentInput("12.5",_cultureUs,out decimal percent));
			Assert.AreEqual(12.5m,percent);
			Assert.IsTrue(Adjustments.TryParsePercentInput("12,5",_cultureDe,out percent));
			Assert.AreEqual(12.5m,percent);
			Assert.IsTrue(Adjustments.TryParsePercentInput("1,000",_cultureUs,out percent));
			Assert.AreEqual(1000m,percent);
			Assert.IsTrue(Adjustments.TryParsePercentInput(" 15 % ",_cultureUs,out percent));
			Assert.AreEqual(15m,percent);
		}

		[TestMethod]
		public void Parse_RejectsEmptyTextNegativeTooLargeAndTooManyDecimals() {
			foreach(string text in new[] { "","  ","%","abc","-5","1000.01","1001","12.345",null }) {
				Assert.IsFalse(Adjustments.TryParsePercentInput(text,_cultureUs,out decimal percent),"'"+text+"'");
				Assert.AreEqual(0m,percent);
			}
		}

		[TestMethod]
		public void Display_UsesTheUsersCultureWithoutTrailingZeros() {
			Assert.AreEqual("12.5",Adjustments.FormatPercentForDisplay(12.50m,_cultureUs));
			Assert.AreEqual("12,5",Adjustments.FormatPercentForDisplay(12.50m,_cultureDe));
			Assert.AreEqual("10",Adjustments.FormatPercentForDisplay(10m,_cultureUs));
			Assert.AreEqual("12.25",Adjustments.FormatPercentForDisplay(12.25m,_cultureUs));
			Assert.AreEqual("1000",Adjustments.FormatPercentForDisplay(1000m,_cultureDe));//no group separator
		}

		[TestMethod]
		public void LogNote_NamesThePercentage() {
			Assert.AreEqual(" (12.5% of fee)",Adjustments.GetPercentOfFeeLogNote(12.50m));
			Assert.AreEqual(" (10% of fee)",Adjustments.GetPercentOfFeeLogNote(10m));
		}
		#endregion Percentage entry

		#region Default percentage (definition.Supp)
		[TestMethod]
		public void Supp_IsInvariantWithTwoDecimals() {
			Assert.AreEqual("90.50",Adjustments.FormatSuppPercent(90.5m));
			Assert.AreEqual("10.00",Adjustments.FormatSuppPercent(10m));
			Assert.AreEqual("1000.00",Adjustments.FormatSuppPercent(1000m));
			Assert.IsTrue(Adjustments.TryParseSuppPercent("90.50",out decimal percent));
			Assert.AreEqual(90.5m,percent);
			Assert.IsTrue(Adjustments.TryParseSuppPercent("15",out percent));
			Assert.AreEqual(15m,percent);
		}

		[TestMethod]
		public void Supp_EmptyOrInvalidMeansNoDefault() {
			foreach(string supp in new[] { "","  ",null,"abc","90,50","-5.00","1000.01","1.234","1,000.00" }) {
				Assert.IsFalse(Adjustments.TryParseSuppPercent(supp,out decimal _),"'"+supp+"'");
				Assert.IsNull(Adjustments.GetDefaultPercent(AdjType("-",supp)),"'"+supp+"'");
			}
		}

		[TestMethod]
		public void Supp_ReadDoesNotDependOnTheUsersCulture() {
			CultureInfo cultureBefore=CultureInfo.CurrentCulture;
			try {
				System.Threading.Thread.CurrentThread.CurrentCulture=_cultureDe;
				Assert.AreEqual(90.5m,Adjustments.GetDefaultPercent(AdjType("-","90.50")));
				Assert.AreEqual("90.50",Adjustments.FormatSuppPercent(90.5m));
			}
			finally {
				System.Threading.Thread.CurrentThread.CurrentCulture=cultureBefore;
			}
		}

		[TestMethod]
		public void DefaultPercent_OnlyForAdjustmentTypes() {
			Assert.AreEqual(10m,Adjustments.GetDefaultPercent(AdjType("-","10.00")));
			Assert.AreEqual(150m,Adjustments.GetDefaultPercent(AdjType("+","150.00")));
			Assert.IsNull(Adjustments.GetDefaultPercent(null));
			Def defOtherCategory=new Def { DefNum=2,Category=DefCat.ImageCats,ItemName="X-rays",ItemValue="X",Supp="10.00" };
			Assert.IsNull(Adjustments.GetDefaultPercent(defOtherCategory));
		}

		[TestMethod]
		public void DefaultPercentBox_EmptyClearsTheDefault() {
			Assert.IsTrue(Adjustments.TryGetSuppFromPercentInput("",_cultureUs,out string supp));
			Assert.AreEqual("",supp);
			Assert.IsTrue(Adjustments.TryGetSuppFromPercentInput("   ",_cultureUs,out supp));
			Assert.AreEqual("",supp);
			Assert.IsTrue(Adjustments.TryGetSuppFromPercentInput(null,_cultureUs,out supp));
			Assert.AreEqual("",supp);
		}

		[TestMethod]
		public void DefaultPercentBox_StoresInvariantText() {
			Assert.IsTrue(Adjustments.TryGetSuppFromPercentInput("12,5",_cultureDe,out string supp));
			Assert.AreEqual("12.50",supp);
			Assert.IsTrue(Adjustments.TryGetSuppFromPercentInput("90.5",_cultureUs,out supp));
			Assert.AreEqual("90.50",supp);
			Assert.IsFalse(Adjustments.TryGetSuppFromPercentInput("abc",_cultureUs,out supp));
			Assert.AreEqual("",supp);
			Assert.IsFalse(Adjustments.TryGetSuppFromPercentInput("1200",_cultureUs,out supp));
			Assert.IsFalse(Adjustments.TryGetSuppFromPercentInput("10.125",_cultureUs,out supp));
		}

		[TestMethod]
		public void DefaultPercent_RoundTripsFromTheBoxToTheAdjustmentWindow() {
			foreach(CultureInfo culture in new[] { _cultureUs,_cultureDe }) {
				foreach(decimal percentTyped in new[] { 0m,5m,12.5m,33.33m,100m,999.99m,1000m }) {
					string typed=Adjustments.FormatPercentForDisplay(percentTyped,culture);
					Assert.IsTrue(Adjustments.TryGetSuppFromPercentInput(typed,culture,out string supp),typed);
					decimal? percentDefault=Adjustments.GetDefaultPercent(AdjType("-",supp));
					Assert.AreEqual(percentTyped,percentDefault,culture.Name+" "+typed);
					Assert.AreEqual(typed,Adjustments.FormatPercentForDisplay(percentDefault.Value,culture));
				}
			}
		}

		[TestMethod]
		public void DefaultPercent_GivesTheAdjustmentAmount() {
			//Edit Adjustment: a new adjustment of a type with a default of 10% on a $185 procedure.
			Procedure procedure=new Procedure { ProcNum=11,PatNum=4,ProcFee=185,UnitQty=1 };
			Def defAdjType=AdjType("-","10.00");
			decimal? percentDefault=Adjustments.GetDefaultPercent(defAdjType);
			Assert.IsNotNull(percentDefault);
			Assert.AreEqual(-18.5,Adjustments.GetPercentOfFeeAdjAmt(procedure,percentDefault.Value,defAdjType));
		}
		#endregion Default percentage (definition.Supp)
	}
}
