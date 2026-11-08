// Rule and text tests for B-CLAIM that need no database: the override audit text and permission (v25_1-misc-claimproc-override-audit), the
// billed-to-insurance fields (v25_3-security-claimproc-billedtoins), the Insurance Write-off Edit date (v25_3-security-inswriteoffedit-clarify)
// and the Medical EOB preference gate and paperwork flag (v25_2-account-medical-eob-prompt).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.Threading;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BCLAIMTests {
	[TestClass]
	public class RuleTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		private static ClaimProc NewClaimProc(double insEstTotalOverride,double allowedOverride) {
			return new ClaimProc { ClaimProcNum=11,InsEstTotalOverride=insEstTotalOverride,AllowedOverride=allowedOverride,CodeSent="Z9701",FeeBilled=120 };
		}

		#region Override audit text (25.1)
		[TestMethod]
		public void OverrideText_BlankForMinusOne() {
			Assert.AreEqual("blank",ClaimProcs.FormatOverrideForAudit(-1));
			Assert.AreEqual("$50.00",ClaimProcs.FormatOverrideForAudit(50));
			Assert.AreEqual("$0.00",ClaimProcs.FormatOverrideForAudit(0),"Zero is an override, not blank.");
		}

		[TestMethod]
		public void OverrideText_BlankToValueAndBack() {
			string text=ClaimProcs.GetOverrideChangeText(NewClaimProc(50,-1),NewClaimProc(-1,-1));
			Assert.AreEqual("Ins Est Total override changed from blank to $50.00.",text);
			text=ClaimProcs.GetOverrideChangeText(NewClaimProc(-1,-1),NewClaimProc(50,-1));
			Assert.AreEqual("Ins Est Total override changed from $50.00 to blank.",text);
			text=ClaimProcs.GetOverrideChangeText(NewClaimProc(-1,80),NewClaimProc(-1,95.5));
			Assert.AreEqual("Allowed override changed from $95.50 to $80.00.",text);
		}

		[TestMethod]
		public void OverrideText_BothFieldsInOneEntry() {
			string text=ClaimProcs.GetOverrideChangeText(NewClaimProc(40,60),NewClaimProc(-1,-1));
			StringAssert.Contains(text,"Ins Est Total override changed from blank to $40.00.");
			StringAssert.Contains(text,"Allowed override changed from blank to $60.00.");
		}

		[TestMethod]
		public void OverrideText_EmptyWhenUnchanged() {
			Assert.AreEqual("",ClaimProcs.GetOverrideChangeText(NewClaimProc(40,-1),NewClaimProc(40,-1)));
			Assert.AreEqual("",ClaimProcs.GetOverrideChangeText(NewClaimProc(40.1+0.2,-1),NewClaimProc(40.3,-1)),"Rounding noise is not a change.");
			Assert.AreEqual("",ClaimProcs.GetOverrideChangeText(null,NewClaimProc(40,-1)));
			//Other overrides are not part of this item.
			ClaimProc claimProcOld=NewClaimProc(40,-1);
			ClaimProc claimProcNew=claimProcOld.Copy();
			claimProcNew.CopayOverride=12;
			claimProcNew.WriteOffEstOverride=3;
			Assert.AreEqual("",ClaimProcs.GetOverrideChangeText(claimProcNew,claimProcOld));
		}

		[TestMethod]
		public void OverridePermType_ReceivedVersusEstimate() {
			Assert.AreEqual(EnumPermType.ClaimProcReceivedEdit,ClaimProcs.GetOverrideAuditPermType(new ClaimProc { Status=ClaimProcStatus.Received }));
			Assert.AreEqual(EnumPermType.ClaimProcReceivedEdit,ClaimProcs.GetOverrideAuditPermType(new ClaimProc { Status=ClaimProcStatus.Supplemental }));
			foreach(ClaimProcStatus status in new[] { ClaimProcStatus.Estimate,ClaimProcStatus.NotReceived,ClaimProcStatus.Preauth,
				ClaimProcStatus.CapEstimate,ClaimProcStatus.CapClaim,ClaimProcStatus.InsHist })
			{
				Assert.AreEqual(EnumPermType.ProcEdit,ClaimProcs.GetOverrideAuditPermType(new ClaimProc { Status=status }),status.ToString());
			}
			Assert.AreEqual(100,(int)EnumPermType.ProcEdit);
			Assert.AreEqual(125,(int)EnumPermType.ClaimProcReceivedEdit);
		}
		#endregion Override audit text (25.1)

		#region Billed to insurance (25.3)
		[TestMethod]
		public void BilledToIns_PermissionNumberUnchanged() {
			Assert.AreEqual(252,(int)EnumPermType.ClaimProcFieldsBilledToInsEdit);
			Assert.IsTrue(GroupPermissions.HasAuditTrail(EnumPermType.ClaimProcFieldsBilledToInsEdit),
				"Fee Billed and Code Sent changes are logged under 252, so it belongs in the audit trail filter.");
		}

		[TestMethod]
		public void BilledToIns_FieldsChangeOnlyWhenAuthorized() {
			ClaimProc claimProc=NewClaimProc(-1,-1);
			ClaimProcs.SetBilledToInsFields(claimProc,"Z9799",150,isAuthorized:false);
			Assert.AreEqual("Z9701",claimProc.CodeSent,"Code Sent is guarded like Fee Billed since 25.3.");
			Assert.AreEqual(120,claimProc.FeeBilled);
			ClaimProcs.SetBilledToInsFields(claimProc,"Z9799",150,isAuthorized:true);
			Assert.AreEqual("Z9799",claimProc.CodeSent);
			Assert.AreEqual(150,claimProc.FeeBilled);
		}

		[TestMethod]
		public void BilledToIns_ChangeText() {
			ClaimProc claimProcOld=NewClaimProc(-1,-1);
			ClaimProc claimProcNew=claimProcOld.Copy();
			Assert.AreEqual("",ClaimProcs.GetBilledToInsChangeText(claimProcNew,claimProcOld));
			claimProcNew.FeeBilled=135;
			Assert.AreEqual("Fee billed changed from $120.00 to $135.00.",ClaimProcs.GetBilledToInsChangeText(claimProcNew,claimProcOld));
			claimProcNew.CodeSent="Z9702";
			Assert.AreEqual("Fee billed changed from $120.00 to $135.00. Code sent changed from Z9701 to Z9702.",
				ClaimProcs.GetBilledToInsChangeText(claimProcNew,claimProcOld));
			claimProcNew=claimProcOld.Copy();
			claimProcNew.CodeSent="";
			Assert.AreEqual("Code sent changed from Z9701 to blank.",ClaimProcs.GetBilledToInsChangeText(claimProcNew,claimProcOld));
		}
		#endregion Billed to insurance (25.3)

		#region Insurance Write-off Edit date (25.3)
		[TestMethod]
		public void WriteOffDate_ProcedureEntryDateForAProcedure() {
			Procedure procedure=new Procedure { ProcNum=7,ProcDate=new DateTime(2026,9,1),DateEntryC=new DateTime(2026,9,2) };
			ClaimProc claimProc=new ClaimProc { ProcNum=7,SecDateEntry=new DateTime(2026,10,1) };
			Assert.AreEqual(new DateTime(2026,9,2),ClaimProcs.GetInsWriteOffEditDate(claimProc,procedure));
			//Creating an estimate for the procedure (Procedure window, Add Estimate) checks the same date.
			Assert.AreEqual(procedure.DateEntryC,ClaimProcs.GetInsWriteOffEditDate(new ClaimProc { ProcNum=7 },procedure));
		}

		[TestMethod]
		public void WriteOffDate_ClaimProcEntryDateForATotalPayment() {
			ClaimProc claimProc=new ClaimProc { ProcNum=0,SecDateEntry=new DateTime(2026,10,1) };
			Assert.AreEqual(new DateTime(2026,10,1),ClaimProcs.GetInsWriteOffEditDate(claimProc,null));
			//A procedure with no date, or a procedure that is not the claimproc's, falls back the same way as Edit Claim Procedure.
			Assert.AreEqual(new DateTime(2026,10,1),ClaimProcs.GetInsWriteOffEditDate(new ClaimProc { ProcNum=7,SecDateEntry=new DateTime(2026,10,1) },
				new Procedure { ProcNum=7,ProcDate=DateTime.MinValue,DateEntryC=new DateTime(2026,9,2) }));
			Assert.AreEqual(new DateTime(2026,10,1),ClaimProcs.GetInsWriteOffEditDate(new ClaimProc { ProcNum=7,SecDateEntry=new DateTime(2026,10,1) },
				new Procedure { ProcNum=8,ProcDate=new DateTime(2026,9,1),DateEntryC=new DateTime(2026,9,2) }));
		}

		[TestMethod]
		public void WriteOff_PermissionTextAndNumber() {
			Assert.AreEqual(119,(int)EnumPermType.InsWriteOffEdit);
			StringAssert.Contains(EnumPermType.InsWriteOffEdit.GetDescription(),"create or delete");
		}
		#endregion Insurance Write-off Edit date (25.3)

		#region Medical EOB (25.2)
		[TestMethod]
		public void MedicalEob_PreferenceIsOpenDentalsRow() {
			//Open Dental's name, from the setting names of the office's Open Dental 25.3.48 database (docs/port-notes/SWMAP.md), read by name outside
			//PrefName, so the office's value carries over as it is.
			Assert.AreEqual("ClaimCheckForMedicalEOB",ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT);
			Assert.IsFalse(Enum.IsDefined(typeof(PrefName),ClaimAttaches.PREF_NAME_MEDICAL_EOB_PROMPT));
		}

		[TestMethod]
		public void MedicalEob_OnlyMedicalAndInstitutionalClaims() {
			Assert.IsTrue(ClaimAttaches.IsMedicalClaim(new Claim { MedType=EnumClaimMedType.Medical }));
			Assert.IsTrue(ClaimAttaches.IsMedicalClaim(new Claim { MedType=EnumClaimMedType.Institutional }));
			Assert.IsFalse(ClaimAttaches.IsMedicalClaim(new Claim { MedType=EnumClaimMedType.Dental }));
			Assert.IsFalse(ClaimAttaches.IsMedicalClaim(null));
			Assert.AreEqual(0,ClaimAttaches.GetDentalClaimsForMedicalEob(new Claim { MedType=EnumClaimMedType.Dental },
				new System.Collections.Generic.List<ClaimProc> { new ClaimProc { ProcNum=1 } }).Count);
			//A medical preauthorization's EOB is an estimate, not a payment: not asked about (no database call is made).
			Assert.AreEqual(0,ClaimAttaches.GetDentalClaimsForMedicalEob(new Claim { MedType=EnumClaimMedType.Medical,ClaimType="PreAuth" },
				new System.Collections.Generic.List<ClaimProc> { new ClaimProc { ProcNum=1 } }).Count);
		}

		[TestMethod]
		public void MedicalEob_EobFlagKeepsOtherKeywords() {
			Assert.AreEqual("EoB",ClaimAttaches.AddEobFlag(""));
			Assert.AreEqual("EoB,Mail",ClaimAttaches.AddEobFlag("Mail"));
			Assert.AreEqual("EoB,Misc,Elect,Submitted",ClaimAttaches.AddEobFlag("Misc,Elect,Submitted"));
			Assert.AreEqual("Note,EoB,Mail",ClaimAttaches.AddEobFlag("Note,EoB,Mail"),"Already flagged: unchanged.");
		}

		[TestMethod]
		public void MedicalEob_NewAttachFileNamesAreUnique() {
			string name1=ClaimAttaches.GetNewAttachFileName(".pdf");
			Thread.Sleep(2);
			string name2=ClaimAttaches.GetNewAttachFileName(".pdf");
			StringAssert.EndsWith(name1,".pdf");
			Assert.AreNotEqual(name1,name2);
		}
		#endregion Medical EOB (25.2)
	}
}
