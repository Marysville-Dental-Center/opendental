// The routine maintenance grant (docs/specs/routine-maintenance.md, section 7; owner decision of 2026-11-02): fork migration step 2026110206 gives
// Maintenance Setup and Maintenance Record Completion to every user group with Security Admin, inserting only the missing rows, once per
// database. The plan without a database, then the step on the scratch database (synthetic rows only).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;

namespace MaintenanceTests {
	[TestClass]
	public class GrantStepTests {
		private const long GROUP_ADMIN=30;
		private const long GROUP_ADMIN_OTHER=40;
		private const int PERM_SECURITY_ADMIN=24;

		[TestInitialize]
		public void TestInitialize() {
			TestDb.SetCultureUs();
		}

		private static string Describe(List<MaintPermissionGrants.Grant> listGrants) {
			return string.Join(", ",listGrants.Select(x => x.UserGroupNum+":"+x.PermType));
		}

		#region Plan (no database)
		[TestMethod]
		public void Plan_OnlySecurityAdminGroups_EachMissingPermissionOnce() {
			List<(long UserGroupNum,int PermType)> listRows=new List<(long,int)> {
				(GROUP_ADMIN,PERM_SECURITY_ADMIN),
				(GROUP_ADMIN,PERM_SECURITY_ADMIN),//Two Security Admin rows: still one grant of each.
				(GROUP_ADMIN,(int)EnumPermType.AppointmentEdit),
				(TestDb.GROUP_STERILIZATION,(int)EnumPermType.AppointmentEdit),
				(TestDb.GROUP_FRONT,(int)EnumPermType.PaymentCreate),
				(GROUP_ADMIN_OTHER,PERM_SECURITY_ADMIN),
				(GROUP_ADMIN_OTHER,(int)EnumPermType.MaintenanceRecord),//Has one of the two already.
			};
			Assert.AreEqual(PERM_SECURITY_ADMIN,(int)EnumPermType.SecurityAdmin);
			List<MaintPermissionGrants.Grant> listGrants=MaintPermissionGrants.Plan(listRows);
			Assert.AreEqual(GROUP_ADMIN+":MaintenanceSetup, "+GROUP_ADMIN+":MaintenanceRecord, "+GROUP_ADMIN_OTHER+":MaintenanceSetup",Describe(listGrants),
				"Security Admin groups only, in UserGroupNum order, the missing rows only.");
			//Once the rows are in, nothing is planned.
			listRows.AddRange(listGrants.Select(x => (x.UserGroupNum,(int)x.PermType)));
			Assert.AreEqual(0,MaintPermissionGrants.Plan(listRows).Count);
			Assert.AreEqual(0,MaintPermissionGrants.Plan(new List<(long,int)>()).Count);
			Assert.AreEqual(0,MaintPermissionGrants.Plan(new List<(long,int)> { (TestDb.GROUP_FRONT,(int)EnumPermType.MaintenanceSetup) }).Count,
				"Without Security Admin, nothing.");
		}

		[TestMethod]
		public void Describe_IsTheLogLine() {
			Assert.AreEqual("Granted MaintenanceSetup to 2 Security Admin user group(s)",MaintPermissionGrants.Describe(EnumPermType.MaintenanceSetup,2));
			CollectionAssert.AreEqual(new[] { EnumPermType.MaintenanceSetup,EnumPermType.MaintenanceRecord },MaintPermissionGrants.ArrayPermTypes);
		}
		#endregion Plan (no database)

		#region The step on the scratch database
		///<summary>Every grouppermission row as "GroupPermNum|UserGroupNum|PermType|FKey|NewerDate|NewerDays", in GroupPermNum order.</summary>
		private static List<string> ReadGroupPermissions() {
			DataTable table=DataCore.GetTable("SELECT GroupPermNum,UserGroupNum,PermType,FKey,NewerDate,NewerDays FROM grouppermission ORDER BY GroupPermNum");
			return table.Select().Select(x => x["GroupPermNum"]+"|"+x["UserGroupNum"]+"|"+x["PermType"]+"|"+x["FKey"]+"|"
				+PIn.Date(x["NewerDate"].ToString()).ToString("yyyy-MM-dd")+"|"+x["NewerDays"]).ToList();
		}

		private static long CountRows(long userGroupNum,EnumPermType permType) {
			return TestDb.GetLong("SELECT COUNT(*) FROM grouppermission WHERE UserGroupNum="+userGroupNum+" AND PermType="+(int)permType);
		}

		///<summary>Two Security Admin groups (one already has Maintenance Record Completion, with a date limit) and two other groups.</summary>
		private static void AddGroups() {
			TestDb.AddGroup(GROUP_ADMIN,"Administrators",TestDb.USER_ADMIN);
			TestDb.AddGroup(GROUP_ADMIN_OTHER,"Office managers");
			TestDb.Grant(GROUP_ADMIN,EnumPermType.SecurityAdmin);
			TestDb.Grant(GROUP_ADMIN,EnumPermType.AppointmentEdit);
			TestDb.Grant(GROUP_ADMIN_OTHER,EnumPermType.SecurityAdmin);
			DataCore.NonQ("INSERT INTO grouppermission (NewerDate,NewerDays,UserGroupNum,PermType,FKey) VALUES('0001-01-01',30,"+GROUP_ADMIN_OTHER+","
				+(int)EnumPermType.MaintenanceRecord+",0)");
			TestDb.Grant(TestDb.GROUP_STERILIZATION,EnumPermType.AppointmentEdit);
			TestDb.Grant(TestDb.GROUP_FRONT,EnumPermType.PaymentCreate);
		}

		[TestMethod]
		public void Step_GrantsTheSecurityAdminGroupsTheMissingRowsOnly_AndChangesNothingElse() {
			TestDb.ResetOrSkip();
			AddGroups();
			List<string> listBefore=ReadGroupPermissions();
			ForkMigrationContext context=new ForkMigrationContext(TestDb.DbName);
			new MaintPermissionGrantStep().Apply(context);
			CollectionAssert.AreEqual(new[] { "Granted MaintenanceSetup to 2 Security Admin user group(s)","Granted MaintenanceRecord to 1 Security Admin user group(s)" },
				context.ListChanges.ToList());
			List<string> listAfter=ReadGroupPermissions();
			CollectionAssert.AreEqual(listBefore,listAfter.Take(listBefore.Count).ToList(),"Every existing row is unchanged.");
			List<string> listAdded=listAfter.Skip(listBefore.Count).Select(x => x.Substring(x.IndexOf('|')+1)).ToList();
			CollectionAssert.AreEqual(new[] {
					GROUP_ADMIN+"|10000|0|0001-01-01|0",
					GROUP_ADMIN+"|10001|0|0001-01-01|0",
					GROUP_ADMIN_OTHER+"|10000|0|0001-01-01|0",
				},listAdded,"FKey 0 and no date limit, as the other grant steps write theirs; the group that had Maintenance Record Completion keeps its row.");
			Assert.AreEqual(30,TestDb.GetLong("SELECT NewerDays FROM grouppermission WHERE UserGroupNum="+GROUP_ADMIN_OTHER+" AND PermType="
				+(int)EnumPermType.MaintenanceRecord),"An administrator's own row is not touched.");
			foreach(long userGroupNum in new[] { TestDb.GROUP_STERILIZATION,TestDb.GROUP_FRONT }) {
				Assert.AreEqual(0,CountRows(userGroupNum,EnumPermType.MaintenanceSetup)+CountRows(userGroupNum,EnumPermType.MaintenanceRecord),
					"Groups without Security Admin get nothing.");
			}
			//The business layer sees the grant.
			GroupPermissions.RefreshCache();
			Assert.IsTrue(GroupPermissions.HasPermission(GROUP_ADMIN,EnumPermType.MaintenanceSetup,0));
			Assert.IsTrue(GroupPermissions.HasPermission(GROUP_ADMIN,EnumPermType.MaintenanceRecord,0));
			Assert.IsTrue(GroupPermissions.HasPermission(Userods.GetUser(TestDb.USER_ADMIN),EnumPermType.MaintenanceSetup,0));
		}

		[TestMethod]
		public void Step_IsIdempotent_EachRowOnlyOnce() {
			TestDb.ResetOrSkip();
			AddGroups();
			new MaintPermissionGrantStep().Apply(new ForkMigrationContext(TestDb.DbName));
			List<string> listAfterFirst=ReadGroupPermissions();
			ForkMigrationContext context=new ForkMigrationContext(TestDb.DbName);
			new MaintPermissionGrantStep().Apply(context);
			Assert.AreEqual(0,context.ListChanges.Count,string.Join("\r\n",context.ListChanges));
			CollectionAssert.AreEqual(listAfterFirst,ReadGroupPermissions(),"A second run on the same rows inserts nothing.");
			foreach(EnumPermType permType in MaintPermissionGrants.ArrayPermTypes) {
				Assert.AreEqual(1,CountRows(GROUP_ADMIN,permType),permType+": one row.");
				Assert.AreEqual(1,CountRows(GROUP_ADMIN_OTHER,permType),permType+": one row.");
			}
		}

		[TestMethod]
		public void Step_RunsOncePerDatabase_ARemovedGrantIsNotGivenBack() {
			TestDb.ResetOrSkip();
			AddGroups();
			ForkMigrationOptions options=new ForkMigrationOptions { ListSteps=new List<ForkMigrationStep> { new MaintPermissionGrantStep() } };
			ForkMigrationResult result=ForkMigrationRunner.Run(options);
			Assert.AreEqual(1,result.ListStepsChanged.Count);
			Assert.IsTrue(ForkMigrationRunner.GetAppliedLevels().Contains(2026110206));
			//An administrator takes Maintenance Setup away from the second group afterwards.
			DataCore.NonQ("DELETE FROM grouppermission WHERE UserGroupNum="+GROUP_ADMIN_OTHER+" AND PermType="+(int)EnumPermType.MaintenanceSetup);
			List<string> listBefore=ReadGroupPermissions();
			//The runner skips it as done, and a forced run of every step (RunAll) skips it too, because its level is recorded.
			ForkMigrationResult resultAgain=ForkMigrationRunner.Run(options);
			Assert.AreEqual(0,resultAgain.ListStepsRun.Count);
			ForkMigrationResult resultAll=ForkMigrationRunner.Run(new ForkMigrationOptions { ListSteps=options.ListSteps,RunAll=true });
			Assert.AreEqual(1,resultAll.ListStepsRun.Count);
			Assert.AreEqual(0,resultAll.ListChanges.Count,string.Join("\r\n",resultAll.ListChanges));
			CollectionAssert.AreEqual(listBefore,ReadGroupPermissions(),"The removed grant is not given back.");
			Assert.AreEqual(0,CountRows(GROUP_ADMIN_OTHER,EnumPermType.MaintenanceSetup));
		}

		[TestMethod]
		public void Step_WithoutSecurityAdminGroups_ChangesNothing() {
			TestDb.ResetOrSkip();
			TestDb.Grant(TestDb.GROUP_FRONT,EnumPermType.PaymentCreate);
			List<string> listBefore=ReadGroupPermissions();
			ForkMigrationContext context=new ForkMigrationContext(TestDb.DbName);
			new MaintPermissionGrantStep().Apply(context);
			Assert.AreEqual(0,context.ListChanges.Count);
			CollectionAssert.AreEqual(listBefore,ReadGroupPermissions());
		}
		#endregion The step on the scratch database
	}
}
