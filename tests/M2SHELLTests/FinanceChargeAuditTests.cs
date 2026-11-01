// v26_1-misc-audit-billing-finance: Audit Trail entries for opening and running Tools, Billing/Finance Charges.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2SHELLTests {
	[TestClass]
	public class FinanceChargeAuditTests {
		private CultureInfo _cultureInfoOld;

		[TestInitialize]
		public void TestInitialize() {
			_cultureInfoOld=Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		[TestCleanup]
		public void TestCleanup() {
			Thread.CurrentThread.CurrentCulture=_cultureInfoOld;
		}

		private static FinanceChargeRun NewFinanceRun() {
			return new FinanceChargeRun {
				IsBillingCharge=false,
				DateCharge=new DateTime(2026,10,1),
				Apr=18,
				AtLeast=1,
				OnlyIfOver=0.5,
				IsCompound=true,
				BillingChargeAmount=5,
				DaysOver=30,
				ListBillingTypes=new List<string> { "Standard","Finance" },
				ProviderAbbr="",
				CountCharges=12,
				TotalAmount=34.5,
			};
		}

		[TestMethod]
		public void FinanceRun_ShowsRateSettingsAndResult() {
			Assert.AreEqual("Finance charges run: date 10/1/2026, APR 18%, at least $1.00, only if over $0.50, compound on, balances over 30 days, "
				+"billing types Standard, Finance, provider each patient's primary provider, 12 charges created totaling $34.50",
				FinanceChargeAudit.GetRunLogText(NewFinanceRun()));
		}

		[TestMethod]
		public void BillingRun_ShowsAmountAndProvider() {
			FinanceChargeRun financeChargeRun=NewFinanceRun();
			financeChargeRun.IsBillingCharge=true;
			financeChargeRun.DaysOver=90;
			financeChargeRun.ProviderAbbr="DrA";
			financeChargeRun.ListBillingTypes=new List<string> { "Standard" };
			financeChargeRun.CountCharges=1;
			financeChargeRun.TotalAmount=5;
			Assert.AreEqual("Billing charges run: date 10/1/2026, amount $5.00, balances over 90 days, billing types Standard, provider DrA, "
				+"1 charge created totaling $5.00",FinanceChargeAudit.GetRunLogText(financeChargeRun));
		}

		[TestMethod]
		public void CancelledRun_SaysSo_AndZeroChargesStillLogs() {
			FinanceChargeRun financeChargeRun=NewFinanceRun();
			financeChargeRun.CountCharges=0;
			financeChargeRun.TotalAmount=0;
			financeChargeRun.IsCancelled=true;
			string text=FinanceChargeAudit.GetRunLogText(financeChargeRun);
			StringAssert.Contains(text,"0 charges created totaling $0.00");
			StringAssert.EndsWith(text,"(cancelled before every account was processed)");
		}

		[TestMethod]
		public void OpenedText() {
			Assert.AreEqual("Billing/Finance Charges window opened",FinanceChargeAudit.LOG_TEXT_OPENED);
		}
	}
}
