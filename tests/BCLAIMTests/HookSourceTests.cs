// Source checks for the B-CLAIM hooks in the windows (WinForms, never shown in tests) and in the shared data classes, so a merge that drops one
// fails here. They read the repository files; no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BCLAIMTests {
	[TestClass]
	public class HookSourceTests {
		private static string Read(params string[] arrayParts) {
			return File.ReadAllText(TestDb.FindRepoFile(Path.Combine(arrayParts)));
		}

		///<summary>The text of the method whose declaration contains signature, up to the next "private void"/"public static" declaration.</summary>
		private static string GetMethodBody(string source,string signature) {
			int start=source.IndexOf(signature,StringComparison.Ordinal);
			Assert.IsTrue(start>=0,"Not found: "+signature);
			int end=source.Length;
			foreach(string next in new[] { "\n\t\tprivate ","\n\t\tpublic ","\n\t\tprotected ","\n\t\t///<summary>" }) {
				int index=source.IndexOf(next,start+signature.Length,StringComparison.Ordinal);
				if(index>=0 && index<end) {
					end=index;
				}
			}
			return source.Substring(start,end-start);
		}

		[TestMethod]
		public void FormClaimProc_CodeSentGuardedAndAudited() {
			string source=Read("OpenDental","Forms","FormClaimProc.cs").Replace("\r\n","\n");
			StringAssert.Contains(source,"if(!Security.IsAuthorized(EnumPermType.ClaimProcFieldsBilledToInsEdit,true)) {\n"
				+"\t\t\t\t\ttextFeeBilled.ReadOnly=true;\n\t\t\t\t\ttextCodeSent.ReadOnly=true;");
			StringAssert.Contains(source,"ClaimProcs.SetBilledToInsFields(_claimProc,textCodeSent.Text,PIn.Double(textFeeBilled.Text),");
			Assert.IsFalse(source.Contains("_claimProc.CodeSent=textCodeSent.Text;"),"Code Sent is only saved through SetBilledToInsFields.");
			string save=GetMethodBody(source,"private void butSave_Click(");
			StringAssert.Contains(save,"ClaimProcs.CreateAuditTrailEntryForBilledToIns(_claimProc,_claimProcOld);");
			StringAssert.Contains(save,"ClaimProcs.CreateAuditTrailEntryForOverrides(_claimProc,_claimProcOld);");
		}

		[TestMethod]
		public void FormClaimProc_DeleteChecksInsWriteOffEdit() {
			string source=Read("OpenDental","Forms","FormClaimProc.cs");
			string delete=GetMethodBody(source,"private void butDelete_Click(");
			int check=delete.IndexOf("ClaimProcs.IsInsWriteOffEditAuthorized(",StringComparison.Ordinal);
			int deleteCall=delete.IndexOf("ClaimProcs.DeleteAfterValidating(",StringComparison.Ordinal);
			Assert.IsTrue(check>=0 && deleteCall>check,"The permission is checked before the claimproc is deleted.");
			//The load-time rule from 24.3 stays: no permission, no Delete button.
			StringAssert.Contains(source,"cannot edit the writeoff, so block deleting the claimproc");
		}

		[TestMethod]
		public void MedicalEobPrompt_RunsBeforeEachPrimaryClaimsPrompt() {
			AssertPromptBefore(Read("OpenDental","Forms","FormClaimEdit.cs"),"MedicalEobL.PromptToAttachMedicalEob(_claim,_listClaimProcsForClaim);");
			AssertPromptBefore(Read("OpenDental","Forms","FormEtrans835ClaimPay.cs"),"MedicalEobL.PromptToAttachMedicalEob(_claim,ListClaimProcs);");
			AssertPromptBefore(Read("OpenDental","Logic","ClaimL.cs"),"MedicalEobL.PromptToAttachMedicalEob(claimLeadOrdinal,listClaimProcs);");
		}

		[TestMethod]
		public void PrimaryClaimsPrompt_SavesTheClaimRowAndKeepsAttachments() {
			//The claims come from Claims.GetPrimaryOrSecondaryClaimsNotReceived without their attachments; the one-argument Claims.Update would
			//delete the medical EOB the Medical EOB prompt has just attached.
			string prompt=GetMethodBody(Read("OpenDental","Logic","ClaimL.cs"),"public static void PromptForPrimaryOrSecondaryClaim(");
			StringAssert.Contains(prompt,"ClaimAttaches.SetClaimsWaitingToSend(listClaimsPromptingFor,");
			Assert.IsFalse(prompt.Contains("Claims.Update(listClaimsPromptingFor[i]);"),"No one-argument Claims.Update on the reloaded claims.");
			int indexStatus=prompt.IndexOf("ClaimAttaches.SetClaimsWaitingToSend(",StringComparison.Ordinal);
			int indexSend=prompt.IndexOf("Claims.GetQueueList(listClaimsPromptingFor.",StringComparison.Ordinal);
			Assert.IsTrue(indexSend>indexStatus,"The status is set before the claims are queued and sent.");
		}

		[TestMethod]
		public void EraPosting_WriteOffColumnFollowsInsWriteOffEdit() {
			//Open Dental 25.3: 119 also covers creating claimprocs, and manual ERA posting creates (supplemental) or receives them.
			string source=Read("OpenDental","Forms","FormEtrans835ClaimPay.cs");
			string load=GetMethodBody(source,"private void FormEtrans835ClaimPay_Load(");
			int indexProcedures=load.IndexOf("_listProcedures=Procedures.Refresh(_patient.PatNum);",StringComparison.Ordinal);
			int indexPermission=load.IndexOf("_isWriteOffEditable=ClaimProcs.IsInsWriteOffEditAuthorizedForAll(ListClaimProcs,_listProcedures);",
				StringComparison.Ordinal);
			int indexFill=load.IndexOf("FillGridProcedures();",StringComparison.Ordinal);
			Assert.IsTrue(indexProcedures>=0 && indexPermission>indexProcedures && indexFill>indexPermission,
				"The permission is worked out from the procedures before the grid is filled.");
			string fill=GetMethodBody(source,"private void FillGridProcedures(");
			StringAssert.Contains(fill,"col=new GridColumn(Lan.g(\"TableClaimProc\",\"Writeoff\"),62,HorizontalAlignment.Right,"
				+"isEditable:isWOIncluded && _isWriteOffEditable);");
			//The Write-off button fills the same column with each procedure's unpaid amount, so it follows the same flag, as in FormClaimPayTotal.
			int indexButton=load.IndexOf("butWriteOff.Enabled=_isWriteOffEditable;",StringComparison.Ordinal);
			Assert.IsTrue(indexButton>indexPermission,"The Write-off button is disabled without 119.");
			string writeOff=GetMethodBody(source,"private void butWriteOff_Click(");
			int indexGuard=writeOff.IndexOf("if(!_isWriteOffEditable) {",StringComparison.Ordinal);
			int indexAssign=writeOff.IndexOf("claimProc.WriteOff=amtUnpaid;",StringComparison.Ordinal);
			Assert.IsTrue(indexGuard>=0 && indexAssign>indexGuard,"The click is refused without 119 before any write-off is set.");
			Assert.IsTrue(writeOff.Substring(indexGuard,indexAssign-indexGuard).Contains("return;"));
		}

		///<summary>Every statement of manual ERA posting that sets a claimproc's WriteOff is accounted for, so a new write-off entry point (a button,
		///a bulk fill) cannot skip the 119 check unnoticed. Allowed without the flag: reading the grid cell (the column is read-only without 119),
		///restoring the ERA's own write-offs, and the hypothetical copies used to validate credits.</summary>
		[TestMethod]
		public void EraPosting_EveryWriteOffAssignmentFollowsInsWriteOffEdit() {
			string source=Read("OpenDental","Forms","FormEtrans835ClaimPay.cs");
			string[] arrayAllowedMethods=new[] {
				"private void SaveGridChanges(",
				"private List<ClaimProc> GetListClaimProcHypothetical(",
				"private void checkIncludeWOPercCoPay_CheckedChanged(",
			};
			int index=0;
			int count=0;
			while(true) {
				index=source.IndexOf(".WriteOff=",index,StringComparison.Ordinal);
				if(index<0) {
					break;
				}
				if(source[index+".WriteOff=".Length]=='=') {//A comparison, not an assignment.
					index++;
					continue;
				}
				count++;
				string method=GetEnclosingMethodSignature(source,index);
				bool isAllowed=Array.Exists(arrayAllowedMethods,x => method.StartsWith(x,StringComparison.Ordinal));
				if(!isAllowed) {
					string body=GetMethodBody(source,method);
					Assert.IsTrue(body.Contains("if(!_isWriteOffEditable) {"),"Sets WriteOff without the 119 check: "+method);
				}
				index++;
			}
			Assert.IsTrue(count>=4,"The scan found the known write-off assignments.");
		}

		///<summary>The declaration line (from "private "/"public "/"protected ") of the method that contains position index.</summary>
		private static string GetEnclosingMethodSignature(string source,int index) {
			int start=-1;
			foreach(string declaration in new[] { "\n\t\tprivate ","\n\t\tpublic ","\n\t\tprotected " }) {
				start=Math.Max(start,source.LastIndexOf(declaration,index,StringComparison.Ordinal));
			}
			Assert.IsTrue(start>=0,"No enclosing method.");
			start+=3;//Past "\n\t\t".
			int end=source.IndexOf('\n',start);
			return source.Substring(start,end-start).TrimEnd('\r');
		}

		private static void AssertPromptBefore(string source,string hook) {
			int indexHook=source.IndexOf(hook,StringComparison.Ordinal);
			Assert.IsTrue(indexHook>=0,"Missing: "+hook);
			Assert.AreEqual(indexHook,source.LastIndexOf(hook,StringComparison.Ordinal),"Called once: "+hook);
			int indexPrompt=source.IndexOf("PrefC.GetBool(PrefName.ClaimMedReceivedPromptForPrimaryClaim)",indexHook,StringComparison.Ordinal);
			Assert.IsTrue(indexPrompt>indexHook && indexPrompt-indexHook<600,"The Medical EOB prompt comes right before the primary claims prompt.");
		}

		[TestMethod]
		public void OverrideAudit_HookedIntoTheSharedSavePaths() {
			string claimProcs=Read("OpenDentBusiness","Data Interface","ClaimProcs.cs").Replace("\r\n","\n");
			string payment=GetMethodBody(claimProcs,"public static void CreateAuditTrailEntryForClaimProcPayment(");
			StringAssert.Contains(payment,"if(!isPaymentFromERA) {");
			int indexGuard=payment.IndexOf("if(!isPaymentFromERA) {",StringComparison.Ordinal);
			int indexOverrides=payment.IndexOf("CreateAuditTrailEntryForOverrides(claimProcNew,claimProcOld);",StringComparison.Ordinal);
			Assert.IsTrue(indexOverrides>indexGuard && indexOverrides-indexGuard<200,"An ERA posting audits overrides against the database instead.");
			StringAssert.Contains(GetMethodBody(claimProcs,"public static void SetInsEstTotalOverride("),
				"CreateAuditTrailEntryForOverrides(listClaimProcs[i],claimProcOld);");
			string etranss=Read("OpenDentBusiness","Data Interface","Etranss.cs");
			StringAssert.Contains(GetMethodBody(etranss,"public static bool TryImportEraClaimData("),
				"ClaimProcs.CreateAuditTrailEntriesForOverrides(listClaimProcsForClaim,listClaimProcsOld);");
			string claimEdit=Read("OpenDental","Forms","FormClaimEdit.cs");
			StringAssert.Contains(claimEdit,"ClaimProcs.CreateAuditTrailEntriesForOverrides(formClaimPayPreAuth.ListClaimProcs,listClaimProcsPreAuthOld);");
		}

		[TestMethod]
		public void OverrideAudit_ManualEraPostingComparesWithTheDatabase() {
			//ListClaimProcs already holds the 835 allowed amounts when FormEtrans835ClaimPay loads, so its old copies cannot show the change.
			string source=Read("OpenDental","Forms","FormEtrans835ClaimPay.cs");
			string load=GetMethodBody(source,"private void FormEtrans835ClaimPay_Load(");
			StringAssert.Contains(load,"_listClaimProcsDb=ClaimProcs.RefreshForClaims(new List<long> { _claim.ClaimNum });");
			string save=GetMethodBody(source,"private void butSave_Click(");
			int indexReceive=save.IndexOf("Etranss.ReceiveEraPayment(",StringComparison.Ordinal);
			int indexAudit=save.IndexOf("ClaimProcs.CreateAuditTrailEntriesForOverrides(ListClaimProcs,_listClaimProcsDb);",StringComparison.Ordinal);
			Assert.IsTrue(indexReceive>=0 && indexAudit>indexReceive,"The overrides are audited after ReceiveEraPayment saves the claimprocs.");
			Assert.AreEqual(indexAudit,save.LastIndexOf("ClaimProcs.CreateAuditTrailEntriesForOverrides(",StringComparison.Ordinal),"Once per save.");
			StringAssert.Contains(save,"ClaimProcs.CreateAuditTrailEntryForClaimProcPayment(listClaimProcs[i],claimProcOld,isInsPayCreate:true,isPaymentFromERA:true);");
		}

		[TestMethod]
		public void Preferences_MedicalEobCheckboxUsesTheForkOwnedRow() {
			string source=Read("OpenDental","User Controls","Preferences","UserControlAccountClaimReceive.cs");
			StringAssert.Contains(source,"checkClaimMedReceivedPromptForMedicalEob.Checked=ClaimAttaches.IsMedicalEobPromptEnabled();");
			StringAssert.Contains(source,"Changed|=ClaimAttaches.SetMedicalEobPromptEnabled(checkClaimMedReceivedPromptForMedicalEob.Checked);");
			Assert.IsFalse(source.Contains("checkClaimMedReceivedPromptForMedicalEob.Visible"),"The checkbox is always shown.");
			string designer=Read("OpenDental","User Controls","Preferences","UserControlAccountClaimReceive.Designer.cs");
			StringAssert.Contains(designer,"this.groupBoxClaimsMedical.Controls.Add(this.checkClaimMedReceivedPromptForMedicalEob);");
			StringAssert.Contains(designer,"\"Prompt for Medical EOB on primary claims\"");
		}
	}
}
