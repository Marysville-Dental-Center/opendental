// V-GP source checks: the payment window's hooks, the V1 rename (display only), the one-processor rule on every setup window, the program link
// registrations, vendor neutrality (no links, logos or promotion), and that nothing here talks to an Open Dental host. Reads the source only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.VendorDirect.GlobalPayments;

namespace VGPTests {
	[TestClass]
	public class GpWiringSourceTests {
		private static readonly string[] _arrayV2Files=new[] {
			@"OpenDentBusiness\VendorDirect\GlobalPayments\GlobalPaymentsV2.cs",
			@"OpenDentBusiness\VendorDirect\GlobalPayments\GlobalPaymentsV2Results.cs",
			@"OpenDentBusiness\VendorDirect\GlobalPayments\GpV2Client.cs",
			@"OpenDentBusiness\VendorDirect\GlobalPayments\GpV2Transactions.cs",
			@"OpenDentBusiness\VendorDirect\GlobalPayments\RecurringChargerator.GlobalPaymentsV2.cs",
			@"OpenDental\Forms\FormGlobalPaymentsV2.cs",
			@"OpenDental\Forms\FormGlobalPaymentsV2.Designer.cs",
			@"OpenDental\Forms\FormGlobalPaymentsV2Setup.cs",
			@"OpenDental\Forms\FormGlobalPaymentsV2Setup.Designer.cs",
			@"OpenDental\Forms\FormPayment.GlobalPaymentsV2.cs",
		};

		[TestMethod]
		public void ProgramName_MeetInTheCloudIsAppended() {
			Assert.IsTrue((int)ProgramName.MeetInTheCloud>(int)ProgramName.PostcardMania);
			Assert.AreEqual("MeetInTheCloud",PayTerminals.CCIntegrationMeetInTheCloud);
			Assert.AreEqual(ProgramName.MeetInTheCloud.ToString(),GlobalPaymentsV2.CCIntegration,"Terminals carry the integration's program name.");
			Assert.AreEqual(23,(int)CreditCardSource.MeetInTheCloudTerminal);
		}

		[TestMethod]
		public void FormPayment_HasTheThreeHooks_AndTheButtonIsNeverShownUnconditionally() {
			string formPayment=RepoFiles.Read(@"OpenDental\Forms\FormPayment.cs");
			StringAssert.Contains(formPayment,"FillGlobalPaymentsV2Button();");
			StringAssert.Contains(formPayment,"DisableGlobalPaymentsV2Button();");
			StringAssert.Contains(formPayment,"if(VoidGlobalPaymentsV2OnClosing()) {");
			Assert.IsTrue(formPayment.IndexOf("if(VoidGlobalPaymentsV2OnClosing())")<formPayment.IndexOf("if(refNum!=\"\") {//Void the PayConnect"),
				"The V2 void runs before the note is read for other processors' voids.");
			string partial=RepoFiles.Read(@"OpenDental\Forms\FormPayment.GlobalPaymentsV2.cs");
			StringAssert.Contains(partial,"GlobalPaymentsV2.ShowButton(");
			Assert.IsTrue(Regex.Matches(partial,@"_butGlobalPaymentsV2\.Visible=true").Count==1,"Shown in one place, after the ShowButton check.");
			Assert.IsTrue(partial.IndexOf("GlobalPaymentsV2.ShowButton(")<partial.IndexOf("_butGlobalPaymentsV2.Visible=true"));
		}

		[TestMethod]
		public void FormPayment_ProjectListsTheNewFiles() {
			string csproj=RepoFiles.Read(@"OpenDental\OpenDental.csproj");
			foreach(string file in new[] { "FormGlobalPaymentsV2.cs","FormGlobalPaymentsV2.Designer.cs","FormGlobalPaymentsV2Setup.cs",
				"FormGlobalPaymentsV2Setup.Designer.cs","FormPayment.GlobalPaymentsV2.cs" })
			{
				StringAssert.Contains(csproj,"<Compile Include=\"Forms\\"+file+"\"",file);
			}
			StringAssert.Contains(RepoFiles.Read(@"OpenDentBusiness\OpenDentBusiness.csproj"),"<Compile Include=\"VendorDirect\\**\\*.cs\" />",
				"The business files are under VendorDirect, which the project compiles by wildcard.");
		}

		[TestMethod]
		public void V1Rename_IsDisplayOnly() {
			Assert.AreEqual("Global Payments V1 Setup",GetDesignerText(@"OpenDental\Forms\FormEdgeExpressSetup.designer.cs","this.Text"));
			Assert.AreEqual("Global Payments V1 Transaction Types",GetDesignerText(@"OpenDental\Forms\FormEdgeExpressTrans.designer.cs","this.Text"));
			Assert.AreEqual("Global Payments V1",GetDesignerText(@"OpenDental\Forms\FormPayment.designer.cs","this.panelEdgeExpress.Text"));
			Assert.AreEqual("EdgeExpress",ProgramName.EdgeExpress.ToString(),"The program name and its rows stay, for data compatibility.");
			Assert.AreEqual("Global Payments V1",GlobalPaymentsV1.ProgDesc);
			string formPayment=RepoFiles.Read(@"OpenDental\Forms\FormPayment.cs");
			Assert.IsFalse(formPayment.Contains("\"EdgeExpress receipt printed\""));
			Assert.IsFalse(formPayment.Contains("\"EdgeExpress must be enabled in Program Links.\""));
			string bridge=RepoFiles.Read(@"OpenDentBusiness\WebBridges\EdgeExpress.cs");
			Assert.IsFalse(bridge.Contains("Program Links > EdgeExpress"));
			StringAssert.Contains(bridge,"Program Links > Global Payments V1");
			string creditCard=RepoFiles.Read(@"OpenDentBusiness\TableTypes\CreditCard.cs");
			StringAssert.Contains(creditCard,"listTokens.Add(\"Global Payments V1\")");
			StringAssert.Contains(creditCard,"listTokens.Add(\"Global Payments V2\")");
		}

		private static string GetDesignerText(string relativePath,string property) {
			string source=RepoFiles.Read(relativePath);
			Match match=Regex.Match(source,Regex.Escape(property)+@" = ""([^""]*)"";");
			Assert.IsTrue(match.Success,property+" in "+relativePath);
			return match.Groups[1].Value;
		}

		[TestMethod]
		public void EverySetupWindowEnforcesOneProcessor() {
			StringAssert.Contains(RepoFiles.Read(@"OpenDental\Forms\FormGlobalPaymentsV2Setup.cs"),
				"GlobalPaymentsV2.GetExclusiveConflictMessage(ProgramName.MeetInTheCloud)");
			StringAssert.Contains(RepoFiles.Read(@"OpenDental\Forms\FormEdgeExpressSetup.cs"),
				"GlobalPaymentsV2.GetExclusiveConflictMessage(ProgramName.EdgeExpress)");
			StringAssert.Contains(RepoFiles.Read(@"OpenDental\Forms\FormXchargeSetup.cs"),
				"GlobalPaymentsV2.GetExclusiveConflictMessage(ProgramName.Xcharge)");
		}

		[TestMethod]
		public void ProgramLinks_V1AndV2AreRegistered() {
			string setup=RepoFiles.Read(@"OpenDental\Forms\FormGlobalPaymentsV2Setup.cs");
			StringAssert.Contains(setup,"public class GlobalPaymentsV2ProgramLinkSetup:IProgramLinkSetup");
			StringAssert.Contains(setup,"public class GlobalPaymentsV1ProgramLinkSetup:IProgramLinkSetup");
			StringAssert.Contains(setup,"GlobalPaymentsV1.RenameProgDescIfDefault()");
		}

		[TestMethod]
		public void Recurring_V2CardsAreRoutedBeforeTheOtherProcessors() {
			string recurring=RepoFiles.Read(@"OpenDentBusiness\Data Interface\RecurringCharges.cs");
			StringAssert.Contains(recurring,"public partial class RecurringChargerator");
			int indexV2=recurring.IndexOf("if(IsGlobalPaymentsV2Charge(chargeData)) {");
			Assert.IsTrue(indexV2>0);
			Assert.IsTrue(indexV2<recurring.IndexOf("else if(useEdgeExpress) {"));
		}

		[TestMethod]
		public void Recurring_InDoubtRetry_IsSeparateFromForceDuplicates() {
			string partial=RepoFiles.Read(@"OpenDentBusiness\VendorDirect\GlobalPayments\RecurringChargerator.GlobalPaymentsV2.cs");
			Match match=Regex.Match(partial,@"bool canRetryInDoubt=([^;]*);");
			Assert.IsTrue(match.Success);
			Assert.IsFalse(match.Groups[1].Value.Contains("forceDuplicates") || match.Groups[1].Value.Contains("ForceDuplicates"),
				"Force Duplicates (pre-checked from the clinic's setting) never retries a charge in doubt.");
			StringAssert.Contains(match.Groups[1].Value,"_isManual");
			StringAssert.Contains(match.Groups[1].Value,"SetRecurringChargeNumsRetryInDoubt.Contains(recurringChargeInDoubt.RecurringChargeNum)");
			Assert.AreEqual(1,Regex.Matches(partial,@"canRetryInDoubt=").Count);
			//The window: one reset per run, and the set filled only when the user answers Yes to its own question, which defaults to No.
			string form=RepoFiles.Read(@"OpenDental\Forms\FormCreditRecurringCharges.cs");
			int indexSend=form.IndexOf("private void butSend_Click(");
			int indexAsk=form.IndexOf("AskRetryGlobalPaymentsV2InDoubt(listRecurringChargeData);",indexSend);
			int indexThread=form.IndexOf("_recurringChargerator.SendCharges(",indexSend);
			Assert.IsTrue(indexSend>0 && indexAsk>indexSend && indexAsk<indexThread,"Asked before the run starts.");
			int indexMethod=form.IndexOf("private void AskRetryGlobalPaymentsV2InDoubt(");
			Assert.IsTrue(indexMethod>0);
			string method=form.Substring(indexMethod,form.IndexOf("\n\t\t}",indexMethod)-indexMethod);
			MatchCollection matchCollectionSets=Regex.Matches(form,@"SetRecurringChargeNumsRetryInDoubt=");
			Assert.AreEqual(2,matchCollectionSets.Count,"Set in two places only, both in AskRetryGlobalPaymentsV2InDoubt.");
			StringAssert.Contains(method,"_recurringChargerator.SetRecurringChargeNumsRetryInDoubt=new HashSet<long>();");
			Assert.IsTrue(method.IndexOf("_recurringChargerator.SetRecurringChargeNumsRetryInDoubt=new HashSet<long>();")
				<method.IndexOf("RecurringChargerator.GetGlobalPaymentsV2InDoubt("),"Reset first, so nothing carries over from an earlier run.");
			StringAssert.Contains(method,"MessageBoxButtons.YesNo");
			StringAssert.Contains(method,"MessageBoxDefaultButton.Button2","The question defaults to No.");
			int indexYes=method.IndexOf("if(dialogResult==DialogResult.Yes) {");
			int indexFill=method.IndexOf("_recurringChargerator.SetRecurringChargeNumsRetryInDoubt=new HashSet<long>(\r\n");
			if(indexFill<0) {
				indexFill=method.IndexOf("_recurringChargerator.SetRecurringChargeNumsRetryInDoubt=new HashSet<long>(\n");
			}
			Assert.IsTrue(indexYes>0 && indexFill>indexYes,"Filled only inside the Yes branch.");
			Assert.IsTrue(indexYes>method.IndexOf("System.Windows.Forms.MessageBox.Show("));
			Assert.IsFalse(method.Contains("checkForceDuplicates"),"Not tied to the Force Duplicates box.");
		}

		[TestMethod]
		public void CreditCardEdit_RecurringSectionShowsForGlobalPaymentsV2() {
			string form=RepoFiles.Read(@"OpenDental\Forms\FormCreditCardEdit.cs");
			StringAssert.Contains(form,"_isGlobalPaymentsV2Enabled=Programs.IsEnabled(ProgramName.MeetInTheCloud);");
			const string conditionOld="_isEdgeExpressEnabled || _isXChargeEnabled || _isPayConnectEnabled || _isPaySimpleEnabled";
			//Load (show the section), FillData (fill it), validation, and save (write ChargeAmt, DateStart, DateStop, ChargeFrequency, PayPlanNum).
			Assert.AreEqual(4,Regex.Matches(form,Regex.Escape(conditionOld)).Count);
			Assert.AreEqual(4,Regex.Matches(form,Regex.Escape(conditionOld+" || _isGlobalPaymentsV2Enabled")).Count,
				"Every recurring-section condition includes Global Payments V2.");
			int indexSave=form.IndexOf(conditionOld+" || _isGlobalPaymentsV2Enabled) {//Only update recurring");
			Assert.IsTrue(indexSave>0);
			Assert.IsTrue(form.IndexOf("CreditCardCur.DateStart=PIn.Date(textDateStart.Text);",indexSave)>indexSave);
		}

		[TestMethod]
		public void VendorNeutrality_NoLinksLogosOrPromotion() {
			foreach(string relativePath in _arrayV2Files) {
				//The one exception: the 24.3 description that the V1 rename looks for (GlobalPaymentsV1.ProgDescOld), never shown.
				string source=string.Join("\n",RepoFiles.Read(relativePath).Split('\n').Where(x => !x.Contains("ProgDescOld=")));
				Assert.IsFalse(Regex.IsMatch(source,@"www\.|globalpaymentsintegrated|sign ?up|learn more|free trial|recommended",RegexOptions.IgnoreCase),
					relativePath);
				Assert.IsFalse(source.Contains("BackgroundImage"),relativePath+" uses no logo.");
				Assert.IsFalse(source.Contains("Image.FromFile") || source.Contains("Properties.Resources"),relativePath);
			}
			Assert.AreEqual("Global Payments V2",GlobalPaymentsV2.ProgDesc,"A neutral description, without the vendor's web address.");
		}

		[TestMethod]
		public void NoOpenDentalHosts_AndOnlyTheTwoGlobalPaymentsHosts() {
			foreach(string relativePath in _arrayV2Files) {
				string source=RepoFiles.Read(relativePath);
				foreach(Match match in Regex.Matches(source,@"https?://[A-Za-z0-9\.\-]+")) {
					Uri uri=new Uri(match.Value);
					Assert.IsFalse(HqHosts.IsBlocked(uri),relativePath+": "+match.Value);
				}
				foreach(Match match in Regex.Matches(source,@"""([a-z0-9\-]+\.)+(com|net|org|cloud)""")) {
					string host=match.Value.Trim('"');
					Assert.IsTrue(host==GpV2Client.HostProduction || host==GpV2Client.HostSandbox,relativePath+": "+host);
				}
			}
		}

		[TestMethod]
		public void DatabaseReads_GoThroughMiddleTier() {
			foreach(string relativePath in _arrayV2Files.Where(x => x.StartsWith(@"OpenDentBusiness\"))) {
				Assert.IsFalse(RepoFiles.Read(relativePath).Contains("DataCore."),relativePath+": DataCore skips the Middle Tier guard.");
			}
			string source=RepoFiles.Read(@"OpenDentBusiness\VendorDirect\GlobalPayments\GlobalPaymentsV2.cs");
			//The type and signature the Middle Tier server resolves from the method name it is sent (DtoProcessor.DtoInformation).
			Type type=Type.GetType("OpenDentBusiness."+"VendorDirect.GlobalPayments.GlobalPaymentsV2"+",OpenDentBusiness");
			Assert.IsNotNull(type);
			foreach(Tuple<string,Type[]> tuple in new[] {
				Tuple.Create("HasSamePaymentOnDate",new[] { typeof(long),typeof(double),typeof(DateTime) }),
				Tuple.Create("HasSameRecurringChargeOnDate",new[] { typeof(long),typeof(double),typeof(DateTime),typeof(long) }),
				Tuple.Create("GetRecurringChargeInDoubt",new[] { typeof(long) }),
				Tuple.Create("GetRecurringChargesInDoubt",new[] { typeof(System.Collections.Generic.List<long>) }) })
			{
				Assert.IsNotNull(type.GetMethod(tuple.Item1,tuple.Item2),tuple.Item1);
				int index=source.IndexOf(" "+tuple.Item1+"(");
				Assert.IsTrue(index>0,tuple.Item1);
				string start=source.Substring(index,source.IndexOf("string command=",index)-index);
				StringAssert.Contains(start,"if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {",tuple.Item1+" runs on the server for a Middle Tier client.");
				Assert.IsFalse(start.Contains("Meth.NoCheckMiddleTierRole()"),tuple.Item1);
			}
			string form=RepoFiles.Read(@"OpenDental\Forms\FormGlobalPaymentsV2.cs");
			int indexCheck=form.IndexOf("GlobalPaymentsV2.HasSamePaymentOnDate(");
			int indexMethod=form.LastIndexOf("private bool ConfirmNotDuplicate(",Math.Max(indexCheck,0));
			Assert.IsTrue(indexCheck>0 && indexMethod>0 && form.LastIndexOf("try {",indexCheck)>indexMethod,
				"The payment window's duplicate check can't throw out of the async Run handler.");
			StringAssert.Contains(form,"!ConfirmNotDuplicate(amount)");
		}

		[TestMethod]
		public void PaymentPortalBox_IsNotBuilt() {
			string designer=RepoFiles.Read(@"OpenDental\Forms\FormGlobalPaymentsV2Setup.Designer.cs");
			Assert.IsFalse(designer.Contains("Payment Portal") || designer.Contains("eClipboard"),"Excluded hosted services have no setting here.");
			string setup=RepoFiles.Read(@"OpenDental\Forms\FormGlobalPaymentsV2Setup.cs");
			Assert.IsFalse(Regex.IsMatch(setup,@"IsOnlinePaymentsEnabled|WebPay"),"No online payment switch.");
		}
	}
}
