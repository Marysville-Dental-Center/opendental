// Database tests for the B-CLAIM permission rules with real users and user groups: Insurance Write-off Edit (119) with its days limit on create,
// edit and delete (v25_3-security-inswriteoffedit-clarify), and ClaimProcFieldsBilledToInsEdit (252) for Fee Billed and Code Sent
// (v25_3-security-claimproc-billedtoins).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BCLAIMTests {
	[TestClass]
	public class PermissionDbTests {
		[TestInitialize]
		public void TestInitialize() {
			BClaimDb.Begin();
		}

		[TestCleanup]
		public void TestCleanup() {
			if(BClaimDb.SkipReason==null) {
				Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1 };
			}
		}

		private static Procedure NewProcedure(int daysSinceEntry) {
			return new Procedure { ProcNum=9001,ProcDate=DateTime.Today.AddDays(-daysSinceEntry),DateEntryC=DateTime.Today.AddDays(-daysSinceEntry) };
		}

		[TestMethod]
		public void WriteOff_DaysLimitAppliesToEditDeleteAndCreate() {
			BClaimDb.LogInWithDays((EnumPermType.InsWriteOffEdit,30));
			Procedure procedureRecent=NewProcedure(5);
			Procedure procedureOld=NewProcedure(60);
			ClaimProc claimProcRecent=new ClaimProc { ProcNum=procedureRecent.ProcNum };
			ClaimProc claimProcOld=new ClaimProc { ProcNum=procedureOld.ProcNum };
			//Editing the write-off fields and deleting the claimproc (Edit Claim Procedure).
			Assert.IsTrue(ClaimProcs.IsInsWriteOffEditAuthorized(claimProcRecent,procedureRecent,suppressMessage:true));
			Assert.IsFalse(ClaimProcs.IsInsWriteOffEditAuthorized(claimProcOld,procedureOld,suppressMessage:true));
			//Creating a claimproc for the procedure (Procedure window, Add Estimate) checks the procedure's DateEntryC, the same date.
			Assert.IsTrue(Security.IsAuthorized(EnumPermType.InsWriteOffEdit,
				ClaimProcs.GetInsWriteOffEditDate(new ClaimProc { ProcNum=procedureRecent.ProcNum },procedureRecent),true));
			Assert.IsFalse(Security.IsAuthorized(EnumPermType.InsWriteOffEdit,
				ClaimProcs.GetInsWriteOffEditDate(new ClaimProc { ProcNum=procedureOld.ProcNum },procedureOld),true));
		}

		[TestMethod]
		public void WriteOff_TotalPaymentUsesClaimProcEntryDate() {
			BClaimDb.LogInWithDays((EnumPermType.InsWriteOffEdit,30));
			Assert.IsTrue(ClaimProcs.IsInsWriteOffEditAuthorized(new ClaimProc { ProcNum=0,SecDateEntry=DateTime.Today.AddDays(-2) },null,true));
			Assert.IsFalse(ClaimProcs.IsInsWriteOffEditAuthorized(new ClaimProc { ProcNum=0,SecDateEntry=DateTime.Today.AddDays(-45) },null,true));
		}

		[TestMethod]
		public void WriteOff_WithoutThePermissionNothingIsAllowed() {
			BClaimDb.LogInWithDays((EnumPermType.ProcEdit,0));
			Assert.IsFalse(ClaimProcs.IsInsWriteOffEditAuthorized(new ClaimProc { ProcNum=9001 },NewProcedure(1),suppressMessage:true));
		}

		[TestMethod]
		public void WriteOff_NoLimitAllowsOldClaimProcs() {
			BClaimDb.LogInWithDays((EnumPermType.InsWriteOffEdit,0));
			Assert.IsTrue(ClaimProcs.IsInsWriteOffEditAuthorized(new ClaimProc { ProcNum=9001 },NewProcedure(4000),suppressMessage:true));
		}

		[TestMethod]
		public void BilledToIns_UserWithoutPermissionCannotChangeEitherField() {
			BClaimDb.LogInWithDays((EnumPermType.InsWriteOffEdit,0));
			ClaimProc claimProc=new ClaimProc { CodeSent="Z9721",FeeBilled=100 };
			//What Edit Claim Procedure does on Save.
			ClaimProcs.SetBilledToInsFields(claimProc,"Z9722",90,Security.IsAuthorized(EnumPermType.ClaimProcFieldsBilledToInsEdit,true));
			Assert.AreEqual("Z9721",claimProc.CodeSent);
			Assert.AreEqual(100,claimProc.FeeBilled);
		}

		[TestMethod]
		public void BilledToIns_UserWithPermissionChangesBoth() {
			BClaimDb.LogInWithDays((EnumPermType.ClaimProcFieldsBilledToInsEdit,0));
			ClaimProc claimProc=new ClaimProc { CodeSent="Z9721",FeeBilled=100 };
			ClaimProcs.SetBilledToInsFields(claimProc,"Z9722",90,Security.IsAuthorized(EnumPermType.ClaimProcFieldsBilledToInsEdit,true));
			Assert.AreEqual("Z9722",claimProc.CodeSent);
			Assert.AreEqual(90,claimProc.FeeBilled);
		}
	}
}
