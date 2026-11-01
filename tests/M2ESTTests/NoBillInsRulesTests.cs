// The per-procedure Do Not Bill to Ins flag (Open Dental 25.4) without a database: the box state, the audit words, and which 24.3/25.3
// procedures the back-fill flags.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;

namespace M2ESTTests {
	[TestClass]
	public class NoBillInsRulesTests {
		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		[TestMethod]
		public void BoxState_MatchesProcedureInfo() {
			Assert.AreEqual(false,Procedures.GetNoBillInsState(new List<ClaimProc>()));
			Assert.AreEqual(false,Procedures.GetNoBillInsState(null));
			Assert.AreEqual(true,Procedures.GetNoBillInsState(new List<ClaimProc> { new ClaimProc { NoBillIns=true },new ClaimProc { NoBillIns=true } }));
			Assert.AreEqual(null,Procedures.GetNoBillInsState(new List<ClaimProc> { new ClaimProc { NoBillIns=true },new ClaimProc { NoBillIns=false } }));
			Assert.AreEqual(false,Procedures.GetNoBillInsState(new List<ClaimProc> { new ClaimProc(),new ClaimProc() }));
		}

		[TestMethod]
		public void StateText() {
			Assert.AreEqual("checked",Procedures.GetNoBillInsStateText(true));
			Assert.AreEqual("unchecked",Procedures.GetNoBillInsStateText(false));
			Assert.AreEqual("mixed",Procedures.GetNoBillInsStateText(null));
		}

		[TestMethod]
		public void AuditPermission_FollowsTheBoxPermission() {
			Assert.AreEqual(EnumPermType.ProcEdit,Procedures.GetNoBillInsPermType(new Procedure { ProcStatus=ProcStat.TP }));
			Assert.AreEqual(EnumPermType.ProcEdit,Procedures.GetNoBillInsPermType(new Procedure { ProcStatus=ProcStat.TPi }));
			Assert.AreEqual(EnumPermType.ProcCompleteEditMisc,Procedures.GetNoBillInsPermType(new Procedure { ProcStatus=ProcStat.C }));
			Assert.AreEqual(EnumPermType.ProcCompleteEditMisc,Procedures.GetNoBillInsPermType(new Procedure { ProcStatus=ProcStat.EC }));
			Assert.AreEqual(EnumPermType.ProcCompleteEditMisc,Procedures.GetNoBillInsPermType(new Procedure { ProcStatus=ProcStat.EO }));
		}

		[TestMethod]
		public void BackfillStep_IsInactiveUntilTheOwnerDecides() {
			//Its level is dated now (renumbered on 2026-11-01 from the retired package range level 2027002701), but the step stays abstract: the owner has
			//not decided whether the back-fill runs (see the port note).
			Assert.IsTrue(typeof(NoBillInsBackfillStep).IsAbstract);
			Assert.AreEqual(2026110102,NoBillInsBackfillStep.LEVEL);
			Assert.IsFalse(ForkMigrationRunner.GetSteps().Exists(x => x is NoBillInsBackfillStep),"The runner skips it.");
			Assert.IsFalse(ForkMigrationRunner.GetSteps().Exists(x => x.Level==NoBillInsBackfillStep.LEVEL),"No active step takes its level.");
			ForkMigrationAttribute attribute=(ForkMigrationAttribute)Attribute.GetCustomAttribute(typeof(NoBillInsBackfillStep),typeof(ForkMigrationAttribute));
			Assert.AreEqual(NoBillInsBackfillStep.LEVEL,attribute.Level);
		}

		private static DataTable NewTable() {
			DataTable table=new DataTable();
			foreach(string column in new[] { "ProcNum","Status","ClaimNum","NoBillIns","CodeNoBillIns","PlanOverride","CountExclusions" }) {
				table.Columns.Add(column,typeof(object));
			}
			return table;
		}

		private static void AddRow(DataTable table,long procNum,ClaimProcStatus status,bool isNoBillIns,long claimNum=0,bool isCodeNoBillIns=false,
			string planOverride=null,int countExclusions=0)
		{
			table.Rows.Add(procNum,(int)status,claimNum,isNoBillIns ? 1 : 0,isCodeNoBillIns ? 1 : 0,(object)planOverride??DBNull.Value,countExclusions);
		}

		[TestMethod]
		public void Backfill_FlagsOnlyDeliberateDecisions() {
			DataTable table=NewTable();
			AddRow(table,1,ClaimProcStatus.Estimate,true);//every estimate not billed, defaults would bill: flagged
			AddRow(table,1,ClaimProcStatus.Estimate,true);
			AddRow(table,2,ClaimProcStatus.Estimate,true);//mixed: not flagged
			AddRow(table,2,ClaimProcStatus.Estimate,false);
			AddRow(table,3,ClaimProcStatus.Estimate,true,isCodeNoBillIns:true);//the code says not to bill
			AddRow(table,4,ClaimProcStatus.Estimate,true,planOverride:"1");//the plan says not to bill
			AddRow(table,5,ClaimProcStatus.Estimate,true,isCodeNoBillIns:true,planOverride:"0");//the plan overrides the code to bill: flagged
			AddRow(table,6,ClaimProcStatus.Estimate,true);//billed to one plan already
			AddRow(table,6,ClaimProcStatus.NotReceived,false,claimNum:40);
			AddRow(table,7,ClaimProcStatus.Estimate,true);//a preauthorization is not billing: flagged
			AddRow(table,7,ClaimProcStatus.Preauth,true,claimNum:41);
			AddRow(table,8,ClaimProcStatus.CapEstimate,true);//capitation estimates count too: flagged
			AddRow(table,9,ClaimProcStatus.Received,true,claimNum:42);//no estimate at all
			AddRow(table,10,ClaimProcStatus.Estimate,true,countExclusions:2);//a plan with exclusions: flagged unless the exclusions mark no-bill
			CollectionAssert.AreEquivalent(new List<long> { 1,5,7,8,10 },NoBillInsBackfillStep.GetProcNumsToFlag(table,isExclusionMarkOn:false));
			CollectionAssert.AreEquivalent(new List<long> { 1,5,7,8 },NoBillInsBackfillStep.GetProcNumsToFlag(table,isExclusionMarkOn:true),
				"With the exclusions preference on, a plan with exclusions could have marked the estimate itself.");
		}
	}
}
