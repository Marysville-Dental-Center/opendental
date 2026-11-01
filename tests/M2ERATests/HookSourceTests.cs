// The M2-ERA hooks live in files that other packages edit at the same time (ClaimProcs.cs, Etranss.cs, X835.cs, the ERA windows). These tests
// read the source files and fail when a merge drops a hook, which the behaviour tests would only show indirectly.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2ERATests {
	[TestClass]
	public class HookSourceTests {
		private static string ReadSource(string relativePath) {
			return File.ReadAllText(TestDb.FindRepoFile(relativePath),Encoding.GetEncoding(1252));
		}

		private static string GetMethodBody(string source,string signatureStart) {
			int index=source.IndexOf(signatureStart,StringComparison.Ordinal);
			Assert.IsTrue(index>=0,"Not found: "+signatureStart);
			int indexBrace=source.IndexOf('{',index);
			int depth=0;
			for(int i=indexBrace;i<source.Length;i++) {
				if(source[i]=='{') {
					depth++;
				}
				else if(source[i]=='}') {
					depth--;
					if(depth==0) {
						return source.Substring(indexBrace,i-indexBrace+1);
					}
				}
			}
			Assert.Fail("Unbalanced braces after "+signatureStart);
			return "";
		}

		[TestMethod]
		public void IsProcessed_AsksTheLinkFirst() {
			string body=GetMethodBody(ReadSource(Path.Combine("OpenDentBusiness","X12","X835.cs")),
				"public bool IsProcessed(List<Hx835_ShortClaimProc> listEraClaimProcs,List<Etrans835Attach> listAttaches)");
			int indexLink=body.IndexOf("EraClaimProcLinks.IsEnteredFromEraClaim(this,listEraClaimProcs,listAttaches)",StringComparison.Ordinal);
			int indexRemove=body.IndexOf("EraClaimProcLinks.RemoveEnteredFromOtherEraClaims(this,listEraClaimProcs,listAttaches)",StringComparison.Ordinal);
			int indexMatching=body.IndexOf("GetClaimProcsForEraProcs(listEraClaimProcs",StringComparison.Ordinal);
			Assert.IsTrue(indexLink>0 && indexRemove>indexLink && indexMatching>indexRemove,"The link is read before the old matching.");
		}

		[TestMethod]
		public void ShortClaimProc_ReadsTheLink() {
			string source=ReadSource(Path.Combine("OpenDentBusiness","X12","X835.cs"));
			StringAssert.Contains(source,"public long Etrans835AttachNum;");
			StringAssert.Contains(GetMethodBody(source,"public static List<Hx835_ShortClaimProc> RefreshForClaims(List <long> listClaimNums)"),
				"Etrans835AttachNum");
			StringAssert.Contains(GetMethodBody(source,"public Hx835_ShortClaimProc(ClaimProc claimProc)"),"Etrans835AttachNum=claimProc.Etrans835AttachNum;");
			StringAssert.Contains(GetMethodBody(source,"public bool CanClaimBeAutoProcessed("),"EraClaimProcLinks.GetEtransNumsWithSamePayment(");
		}

		[TestMethod]
		public void EraImport_LinksTheClaimProcsItEntered() {
			string body=GetMethodBody(ReadSource(Path.Combine("OpenDentBusiness","Data Interface","Etranss.cs")),"public static bool TryImportEraClaimData(");
			int indexLink=body.IndexOf("ClaimProcs.LinkToEraClaim(x835,claimPaid,claim,listClaimProcsToEdit,isByTotalIncluded ? cpByTotal : null);",
				StringComparison.Ordinal);
			int indexReceive=body.IndexOf("ReceiveEraPayment(",StringComparison.Ordinal);
			Assert.IsTrue(indexLink>0 && indexReceive>indexLink,"Linked before the automatic path saves the claimprocs.");
		}

		[TestMethod]
		public void ClaimProcs_ClearTheLinkWhenUnreceivedAndOnSupplementalCopies() {
			string source=ReadSource(Path.Combine("OpenDentBusiness","Data Interface","ClaimProcs.cs"));
			string bodyUpdate=GetMethodBody(source,"public static void Update(ClaimProc claimProc,ClaimProc claimProcOld=null)");
			int indexClear=bodyUpdate.IndexOf("ClearEtrans835AttachNumIfNotReceived(claimProc);",StringComparison.Ordinal);
			int indexCrud=bodyUpdate.IndexOf("Crud.ClaimProcCrud.Update(",StringComparison.Ordinal);
			Assert.IsTrue(indexClear>0 && indexCrud>indexClear);
			StringAssert.Contains(GetMethodBody(source,"public static List<ClaimProc> CreateSuppClaimProcs("),"claimProc.Etrans835AttachNum=0;");
		}

		[TestMethod]
		public void Attaches_ClearAndKeepLinks() {
			string source=ReadSource(Path.Combine("OpenDentBusiness","Data Interface","Etrans835Attaches.cs"));
			StringAssert.Contains(GetMethodBody(source,"public static void DeleteMany(int clpSegmentIndex,params long[] arrayEtranNums)"),"ClearClaimProcLinks(");
			StringAssert.Contains(GetMethodBody(source,"public static void DeleteMany(List<long> listEtrans835AttachNums)"),
				"ClaimProcs.ClearEtrans835AttachNums(listEtrans835AttachNums);");
			string bodyCreate=GetMethodBody(source,"public static void CreateForClaim(");
			StringAssert.Contains(bodyCreate,"ClaimProcs.GetClaimProcNumsForEraClaim(");
			StringAssert.Contains(bodyCreate,"ClaimProcs.UpdateEtrans835AttachNum(listClaimProcNumsLinked,attach.Etrans835AttachNum);");
		}

		[TestMethod]
		public void EraWindows_UseTheFiltersAndTheWarning() {
			string etransL=ReadSource(Path.Combine("OpenDental","Logic","EtransL.cs"));
			StringAssert.Contains(GetMethodBody(etransL,"public static EraData GetEraDataFiltered("),
				"Etrans835s.RemoveHiddenForAutoProcessedReport(eraData.ListEtrans,eraData.ListEtrans835s,doShowPreAuths,doShowZeroPayments);");
			StringAssert.Contains(GetMethodBody(etransL,"public static void ImportEraClaimData("),"ConfirmEraClaimNotEnteredYet(");
			string form=ReadSource(Path.Combine("OpenDental","Forms","FormRpEraAutoProcessed.cs"));
			StringAssert.Contains(form,"doIncludeAcknowledged,doShowPreAuths,doShowZeroPayments);");
			StringAssert.Contains(form,"EraAutoProcessedReportFilters.SetForUser(");
			StringAssert.Contains(form,"EraAutoProcessedReportFilters.GetForUser(");
			Assert.IsTrue(Regex.IsMatch(form,@"SetFilterControlsAndAction\([^;]*checkShowPreAuths,checkShowZeroPayments\);"),"Ticking a box refreshes the list.");
			string designer=ReadSource(Path.Combine("OpenDental","Forms","FormRpEraAutoProcessed.Designer.cs"));
			StringAssert.Contains(designer,"this.checkShowPreAuths.Checked = true;");
			StringAssert.Contains(designer,"this.checkShowPreAuths.Text = \"Show PreAuths\";");
			StringAssert.Contains(designer,"this.checkShowZeroPayments.Checked = true;");
			StringAssert.Contains(designer,"this.checkShowZeroPayments.Text = \"Show $0 Payments\";");
			StringAssert.Contains(designer,"this.Controls.Add(this.checkShowPreAuths);");
			StringAssert.Contains(designer,"this.Controls.Add(this.checkShowZeroPayments);");
		}
	}
}
