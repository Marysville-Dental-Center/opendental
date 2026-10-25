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

		private static void AssertPromptBefore(string source,string hook) {
			int indexHook=source.IndexOf(hook,StringComparison.Ordinal);
			Assert.IsTrue(indexHook>=0,"Missing: "+hook);
			Assert.AreEqual(indexHook,source.LastIndexOf(hook,StringComparison.Ordinal),"Called once: "+hook);
			int indexPrompt=source.IndexOf("PrefC.GetBool(PrefName.ClaimMedReceivedPromptForPrimaryClaim)",indexHook,StringComparison.Ordinal);
			Assert.IsTrue(indexPrompt>indexHook && indexPrompt-indexHook<600,"The Medical EOB prompt comes right before the primary claims prompt.");
		}

		[TestMethod]
		public void OverrideAudit_HookedIntoTheSharedSavePaths() {
			string claimProcs=Read("OpenDentBusiness","Data Interface","ClaimProcs.cs");
			StringAssert.Contains(GetMethodBody(claimProcs,"public static void CreateAuditTrailEntryForClaimProcPayment("),
				"CreateAuditTrailEntryForOverrides(claimProcNew,claimProcOld);");
			StringAssert.Contains(GetMethodBody(claimProcs,"public static void SetInsEstTotalOverride("),
				"CreateAuditTrailEntryForOverrides(listClaimProcs[i],claimProcOld);");
			string etranss=Read("OpenDentBusiness","Data Interface","Etranss.cs");
			StringAssert.Contains(GetMethodBody(etranss,"public static bool TryImportEraClaimData("),
				"ClaimProcs.CreateAuditTrailEntriesForOverrides(listClaimProcsForClaim,listClaimProcsOld);");
			string claimEdit=Read("OpenDental","Forms","FormClaimEdit.cs");
			StringAssert.Contains(claimEdit,"ClaimProcs.CreateAuditTrailEntriesForOverrides(formClaimPayPreAuth.ListClaimProcs,listClaimProcsPreAuthOld);");
		}

		[TestMethod]
		public void Preferences_MedicalEobCheckboxFollowsRegistration() {
			string source=Read("OpenDental","User Controls","Preferences","UserControlAccountClaimReceive.cs");
			StringAssert.Contains(source,"checkClaimMedReceivedPromptForMedicalEob.Visible=isMedicalEobPrefRegistered;");
			StringAssert.Contains(source,"Changed|=Prefs.UpdateBool(prefNameMedicalEob,checkClaimMedReceivedPromptForMedicalEob.Checked);");
			string designer=Read("OpenDental","User Controls","Preferences","UserControlAccountClaimReceive.Designer.cs");
			StringAssert.Contains(designer,"this.groupBoxClaimsMedical.Controls.Add(this.checkClaimMedReceivedPromptForMedicalEob);");
			StringAssert.Contains(designer,"\"Prompt for Medical EOB on primary claims\"");
		}
	}
}
