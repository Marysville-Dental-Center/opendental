// The windows cannot be opened in a test run (the program is not launched), so these tests read the window sources and check that each P262-TOOLS
// tool is wired to the business methods the other files test, behind the checks the port note promises. Also: the package adds no fork migration
// step (levels 2026110751 to 2026110759 stay free) and no enum value.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using ProjectFileTests;

namespace P262ToolsTests {
	[TestClass]
	public class WiringTests {
		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(ToolsDb.FindRepoFile(Path.Combine(arrayParts))).Replace("\r\n","\n");
		}

		///<summary>The text of one method: from its signature to its closing brace, the first brace at the method's own indent (two tabs).</summary>
		private static string MethodBody(string source,string signature) {
			int idxStart=source.IndexOf(signature);
			Assert.IsTrue(idxStart>=0,signature+" not found.");
			int idxEnd=source.IndexOf("\n\t\t}",idxStart);
			Assert.IsTrue(idxEnd>idxStart,signature+" has no closing brace.");
			return source.Substring(idxStart,idxEnd-idxStart);
		}

		private static void AssertBefore(string body,string before,string after) {
			int idxBefore=body.IndexOf(before);
			int idxAfter=body.IndexOf(after);
			Assert.IsTrue(idxBefore>=0,before+" not found.");
			Assert.IsTrue(idxAfter>=0,after+" not found.");
			Assert.IsTrue(idxBefore<idxAfter,before+" must come before "+after);
		}

		[TestMethod]
		public void DatabaseMaintenance_ToolsTab_HasConversionPaymentDates() {
			string fillGridTools=MethodBody(Read("OpenPmsDental","Forms","FormDatabaseMaintenance.cs"),"private void FillGridTools()");
			StringAssert.Contains(fillGridTools,"Lan.g(this,\"Conversion Payment Dates\")");
			StringAssert.Contains(fillGridTools,"row.Tag=new Action(ConversionPaymentDatesFix);");
			string handler=MethodBody(Read("OpenPmsDental","Forms","FormDatabaseMaintenance.Tools262.cs"),"private void ConversionPaymentDatesFix()");
			AssertBefore(handler,"VerifyPermissionToRunReplicationUnsafeMethods()","new FormDatabaseMaintConvPayDates(canFix)");
			StringAssert.Contains(handler,"_isReplicationPasswordEntered");
			StringAssert.Contains(handler,"SaveLogToFile(formDatabaseMaintConvPayDates.LogText)");
		}

		[TestMethod]
		public void ConversionPaymentDates_ChangeDatesNeedsTheCheckPermissionAndAConfirmation() {
			string source=Read("OpenPmsDental","Forms","FormDatabaseMaintConvPayDates.cs");
			string fix=MethodBody(source,"private void butFix_Click(object sender,EventArgs e)");
			AssertBefore(fix,"if(!_canFix","DatabaseMaintenances.FixConversionPaymentDates(");
			AssertBefore(fix,"dateConversion!=_dateListed","DatabaseMaintenances.FixConversionPaymentDates(");
			AssertBefore(fix,"Security.IsAuthorized(EnumPermType.PaymentEdit,dateConversion)","DatabaseMaintenances.FixConversionPaymentDates(");
			AssertBefore(fix,"Make a backup of the database","DatabaseMaintenances.FixConversionPaymentDates(");
			AssertBefore(fix,"MessageBoxButtons.OKCancel","DatabaseMaintenances.FixConversionPaymentDates(");
			StringAssert.Contains(MethodBody(source,"private void Find(DateTime dateConversion)"),"DatabaseMaintenances.GetConversionPaymentDateRows(dateConversion)");
			StringAssert.Contains(MethodBody(source,"private void FillGrid()"),"if(_listRows[i].IsConversionTransfer) {");
		}

		[TestMethod]
		public void FeeTools_CopyCodeTab_CallsTheBusinessMethodAfterItsChecks() {
			string designer=Read("OpenPmsDental","Forms","FormFeeSchedTools.designer.cs");
			StringAssert.Contains(designer,"this.tabControlTools.Controls.Add(this.tabPageCopyCode);");
			StringAssert.Contains(designer,"this.butCopyCode.Click += new System.EventHandler(this.butCopyCode_Click);");
			StringAssert.Contains(designer,"this.radioCopyCodeAllScheds.Checked = true;");
			string source=Read("OpenPmsDental","Forms","FormFeeSchedTools.cs");
			StringAssert.Contains(MethodBody(source,"private void FormFeeSchedTools_Load(object sender, System.EventArgs e)"),
				"Security.IsAuthorized(EnumPermType.FeeSchedEdit)");
			string copy=MethodBody(source,"private void butCopyCode_Click(object sender,EventArgs e)");
			AssertBefore(copy,"Security.CurUser.ClinicIsRestricted","Fees.CopyFeesToCode(");
			AssertBefore(copy,"codeNumFrom==codeNumTo","Fees.CopyFeesToCode(");
			AssertBefore(copy,"MessageBoxButtons.OKCancel","Fees.CopyFeesToCode(");
			StringAssert.Contains(copy,"feeSchedNum=_listFeeScheds[comboFeeSched.SelectedIndex].FeeSchedNum;");
		}

		[TestMethod]
		public void TestLatency_AverageButtonUsesLatencyMeasure() {
			string source=Read("OpenPmsDental","Forms","FormTestLatency.cs");
			StringAssert.Contains(MethodBody(source,"private void butAverage_Click(object sender,EventArgs e)"),
				"LatencyMeasure.MeasureDatabase(textAverageCount.Value)");
			StringAssert.Contains(MethodBody(source,"private void FormTestLatency_Load(object sender,EventArgs e)"),
				"textAverageCount.Value=LatencyMeasure.COUNT_DEFAULT;");
			StringAssert.Contains(Read("OpenPmsDental","Forms","FormTestLatency.designer.cs"),
				"this.butAverage.Click += new System.EventHandler(this.butAverage_Click);");
		}

		[TestMethod]
		public void IncomeTransferCheck_HasAnExplanation() {
			StringAssert.Contains(Read("OpenPmsDental","Misc","DbmInfo.cs"),"MethodName=nameof(DatabaseMaintenances.PaymentIncomeTransferIssues),");
		}

		[TestMethod]
		public void DbmChecks_AreOrdinaryChecksOfTheChecksTab() {
			foreach(string methodName in new[] { nameof(DatabaseMaintenances.PaymentIncomeTransferIssues),nameof(DatabaseMaintenances.InsVerifyRedundantEntries) }) {
				MethodInfo methodInfo=typeof(DatabaseMaintenances).GetMethod(methodName);
				Assert.IsNotNull(methodInfo,methodName);
				DbmMethodAttr dbmMethodAttr=(DbmMethodAttr)Attribute.GetCustomAttribute(methodInfo,typeof(DbmMethodAttr));
				Assert.IsNotNull(dbmMethodAttr,methodName+" needs DbmMethodAttr to be listed.");
				Assert.IsTrue(dbmMethodAttr.IsReplicationUnsafe,methodName);
				Assert.IsFalse(dbmMethodAttr.IsOneOff,methodName);
				Assert.IsFalse(dbmMethodAttr.HasPatNum,methodName);
				CollectionAssert.AreEqual(new[] { typeof(bool),typeof(DbmMode) },methodInfo.GetParameters().Select(x => x.ParameterType).ToArray(),methodName);
			}
			Assert.IsNull(typeof(DatabaseMaintenances).GetMethods().FirstOrDefault(x => x.Name.Contains("ConversionPayment")
				&& Attribute.GetCustomAttribute(x,typeof(DbmMethodAttr))!=null),"Conversion Payment Dates runs on demand only, never in Run All.");
		}

		[TestMethod]
		public void NoForkMigrationStep_InThePackageRange() {
			long[] arrayLevels=typeof(ForkMigrationRunner).Assembly.GetTypes()
				.Select(x => (ForkMigrationAttribute)Attribute.GetCustomAttribute(x,typeof(ForkMigrationAttribute)))
				.Where(x => x!=null)
				.Select(x => x.Level)
				.ToArray();
			Assert.IsTrue(arrayLevels.Length>0);
			Assert.IsFalse(arrayLevels.Any(x => x>=2026110751 && x<=2026110759),"P262-TOOLS needs no row, setting or step.");
		}

		[TestMethod]
		public void NewFiles_AreInTheProjects() {
			//The evaluated items, so the check holds for the classic project files and the SDK-style ones of the .NET 10 move alike.
			ProjectFile openDental=ProjectFile.Load(ToolsDb.FindRepoFile(Path.Combine("OpenPmsDental","OpenPmsDental.csproj")));
			openDental.AssertCompiles(@"Forms\FormDatabaseMaintConvPayDates.cs",@"Forms\FormDatabaseMaintConvPayDates.Designer.cs",
				@"Forms\FormDatabaseMaintenance.Tools262.cs");
			ProjectFile business=ProjectFile.Load(ToolsDb.FindRepoFile(Path.Combine("OpenPmsDentalBusiness","OpenPmsDentalBusiness.csproj")));
			business.AssertCompiles(@"Data Interface\DatabaseMaintenances.ConversionPayments.cs",@"Data Interface\DatabaseMaintenances.IncomeTransfers.cs",
				@"Data Interface\DatabaseMaintenances.InsVerifyRedundant.cs",@"Data Interface\Fees.CopyToCode.cs",@"Misc\LatencyMeasure.cs");
		}
	}
}
