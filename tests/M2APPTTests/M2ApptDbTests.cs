// Database tests for package M2-APPT on a scratch database (see ScratchDb): the blockout permissions read from real user groups and group
// permissions, the audit entries each blockout action writes, the paste and replace of whole days, and the two 26.1 appointment preferences.
// Synthetic rows only. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2APPTTests {
	[TestClass]
	public class M2ApptDbTests {
		private static readonly string DB_NAME=ScratchDb.NewName();
		private static string _skipReason;
		//A date far from anything in the seed dump, so these rows are the only blockouts on it.
		private static readonly DateTime _date=new DateTime(2032,5,4);
		private static long _defNumUnflagged;
		private static long _defNumNoSched;
		private static long _defNumDontCopy;
		private static long _opNumA;
		private static long _opNumB;
		///<summary>One user per permission set, keyed by a short name. Users exist only in memory; their groups are real rows.</summary>
		private static Dictionary<string,Userod> _dictUsers=new Dictionary<string,Userod>();

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			try {
				ScratchDb.Create(DB_NAME);
			}
			catch(Exception ex) {
				_skipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
				return;
			}
			_defNumUnflagged=Defs.Insert(new Def { Category=DefCat.BlockoutTypes,ItemOrder=90,ItemName="M2Appt Lunch",ItemValue="",ItemColor=Color.Gray });
			_defNumNoSched=Defs.Insert(new Def { Category=DefCat.BlockoutTypes,ItemOrder=91,ItemName="M2Appt Meeting",ItemValue="NS",ItemColor=Color.Gray });
			_defNumDontCopy=Defs.Insert(new Def { Category=DefCat.BlockoutTypes,ItemOrder=92,ItemName="M2Appt Hold",ItemValue="DC",ItemColor=Color.Gray });
			_opNumA=Operatories.Insert(new Operatory { OpName="M2Appt A",Abbrev="MAA",ItemOrder=90 });
			_opNumB=Operatories.Insert(new Operatory { OpName="M2Appt B",Abbrev="MAB",ItemOrder=91 });
			//User groups: one per blockout permission, and one with none.
			long userGroupNumAdd=AddGroup("M2Appt Add",EnumPermType.BlockoutAdd);
			long userGroupNumEdit=AddGroup("M2Appt Edit",EnumPermType.Blockouts);
			long userGroupNumDelete=AddGroup("M2Appt Delete",EnumPermType.BlockoutDelete);
			long userGroupNumFlagged=AddGroup("M2Appt Flagged",EnumPermType.BlockoutsFlagged);
			long userGroupNumNone=AddGroup("M2Appt None");
			AddUser("add",900001,userGroupNumAdd);
			AddUser("edit",900002,userGroupNumEdit);
			AddUser("delete",900003,userGroupNumDelete);
			AddUser("flagged",900004,userGroupNumFlagged);
			AddUser("none",900005,userGroupNumNone);
			AddUser("addDelete",900006,userGroupNumAdd,userGroupNumDelete);
			AddUser("all",900007,userGroupNumAdd,userGroupNumEdit,userGroupNumDelete,userGroupNumFlagged);
			Security.CurUser=null;//The cache refresh would otherwise look for the made-up current user among the users.
			Cache.Refresh(InvalidType.Defs,InvalidType.Operatories,InvalidType.Security);
		}

		private static long AddGroup(string description,params EnumPermType[] arrayPermTypes) {
			long userGroupNum=OpenDentBusiness.Crud.UserGroupCrud.Insert(new UserGroup { Description=description });
			for(int i=0;i<arrayPermTypes.Length;i++) {
				OpenDentBusiness.Crud.GroupPermissionCrud.Insert(new GroupPermission { UserGroupNum=userGroupNum,PermType=arrayPermTypes[i],
					NewerDate=DateTime.MinValue,NewerDays=0,FKey=0 });
			}
			return userGroupNum;
		}

		private static void AddUser(string key,long userNum,params long[] arrayUserGroupNums) {
			for(int i=0;i<arrayUserGroupNums.Length;i++) {
				OpenDentBusiness.Crud.UserGroupAttachCrud.Insert(new UserGroupAttach { UserNum=userNum,UserGroupNum=arrayUserGroupNums[i] });
			}
			_dictUsers[key]=new Userod { UserNum=userNum,UserName="m2appt_"+key };
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("M2APPT_TEST_KEEP_DB")=="1") {
				return;
			}
			try {
				ScratchDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//Harmless to leave behind; the name is unique to this run.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ScratchDb.Connect(DB_NAME);
			Security.CurUser=new Userod { UserNum=1,UserName="m2appttest" };
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			DataCore.NonQ("DELETE scheduleop FROM scheduleop INNER JOIN schedule ON schedule.ScheduleNum=scheduleop.ScheduleNum "
				+"WHERE schedule.SchedDate>="+POut.Date(_date));
			DataCore.NonQ("DELETE FROM schedule WHERE SchedDate>="+POut.Date(_date));
			DataCore.NonQ("DELETE FROM securitylog");
		}

		private static Schedule AddBlockout(long defNum,long opNum,int hour,DateTime? date=null,ScheduleType scheduleType=ScheduleType.Blockout) {
			Schedule schedule=new Schedule { SchedDate=date??_date,StartTime=TimeSpan.FromHours(hour),StopTime=TimeSpan.FromHours(hour+1),
				SchedType=scheduleType,BlockoutType=defNum,Note="",Ops=new List<long> { opNum } };
			Schedules.Insert(schedule,false,false);
			return schedule;
		}

		///<summary>Blockout types left on the date, as "type@op" sorted.</summary>
		private static string Remaining(DateTime date) {
			DataTable table=DataCore.GetTable("SELECT schedule.BlockoutType,scheduleop.OperatoryNum FROM schedule "
				+"INNER JOIN scheduleop ON scheduleop.ScheduleNum=schedule.ScheduleNum "
				+"WHERE schedule.SchedType="+POut.Int((int)ScheduleType.Blockout)+" AND schedule.SchedDate="+POut.Date(date));
			return string.Join(",",table.Select().Select(x => Name(PIn.Long(x[0].ToString()))+"@"+(PIn.Long(x[1].ToString())==_opNumA ? "A" : "B"))
				.OrderBy(x => x));
		}

		private static string Name(long defNum) {
			if(defNum==_defNumUnflagged) {
				return "Lunch";
			}
			if(defNum==_defNumNoSched) {
				return "NS";
			}
			if(defNum==_defNumDontCopy) {
				return "DC";
			}
			return "other";
		}

		private static List<int> LoggedPermTypes() {
			return DataCore.GetTable("SELECT PermType FROM securitylog ORDER BY SecurityLogNum").Select().Select(x => PIn.Int(x[0].ToString())).ToList();
		}

		#region Permissions from user groups
		private static string Perms(string key) {
			BlockoutPerms blockoutPerms=BlockoutPerms.ForUser(_dictUsers[key]);
			return (blockoutPerms.HasAdd ? "A" : "")+(blockoutPerms.HasEdit ? "E" : "")+(blockoutPerms.HasDelete ? "D" : "")+(blockoutPerms.HasFlagged ? "F" : "");
		}

		[TestMethod]
		public void ForUser_ReadsEachSplitPermissionFromTheGroups() {
			Assert.AreEqual("A",Perms("add"));
			Assert.AreEqual("E",Perms("edit"));
			Assert.AreEqual("D",Perms("delete"));
			Assert.AreEqual("F",Perms("flagged"));
			Assert.AreEqual("",Perms("none"));
			Assert.AreEqual("AD",Perms("addDelete"),"Permissions add up across a user's groups.");
			Assert.AreEqual("AEDF",Perms("all"));
		}

		[TestMethod]
		public void ForCurrentUser_UsesSecurityCurUser() {
			Security.CurUser=_dictUsers["delete"];
			BlockoutPerms blockoutPerms=BlockoutPerms.ForCurrentUser();
			Assert.IsTrue(blockoutPerms.CanDo(BlockoutAction.Delete,_defNumUnflagged));
			Assert.IsTrue(blockoutPerms.CanDo(BlockoutAction.Clear,_defNumUnflagged));
			Assert.IsTrue(blockoutPerms.CanDo(BlockoutAction.Cut,_defNumUnflagged));
			Assert.IsFalse(blockoutPerms.CanDo(BlockoutAction.Create,_defNumUnflagged));
			Assert.IsFalse(blockoutPerms.CanDo(BlockoutAction.Edit,_defNumUnflagged));
			Assert.IsFalse(blockoutPerms.CanDo(BlockoutAction.Delete,_defNumNoSched),"Flagged types need BlockoutsFlagged.");
			Assert.IsTrue(Security.IsAuthorized(BlockoutPerms.GetPermType(BlockoutAction.Delete,_defNumUnflagged),true));
			Assert.IsFalse(Security.IsAuthorized(BlockoutPerms.GetPermType(BlockoutAction.Paste,_defNumUnflagged),true));
			Security.CurUser=null;
			BlockoutPerms blockoutPermsNobody=BlockoutPerms.ForCurrentUser();
			Assert.IsFalse(blockoutPermsNobody.HasAdd || blockoutPermsNobody.HasEdit || blockoutPermsNobody.HasDelete || blockoutPermsNobody.HasFlagged,
				"Nobody logged on: nothing.");
		}

		[TestMethod]
		public void Matrix_RealGroupsAgainstTheSpecTable() {
			//user -> actions allowed on an unflagged type, then on a flagged type (enum order: Cut,Copy,Paste,Delete,Create,Edit,Clear)
			Dictionary<string,(string Unflagged,string Flagged)> dictExpected=new Dictionary<string,(string,string)> {
				{ "add",("Copy,Paste,Create","") },
				{ "edit",("Edit","") },
				{ "delete",("Cut,Delete,Clear","") },
				{ "flagged",("","Cut,Copy,Paste,Delete,Create,Edit,Clear") },
				{ "none",("","") },
				{ "all",("Cut,Copy,Paste,Delete,Create,Edit,Clear","Cut,Copy,Paste,Delete,Create,Edit,Clear") },
			};
			List<BlockoutAction> listActions=Enum.GetValues(typeof(BlockoutAction)).Cast<BlockoutAction>().ToList();
			foreach(KeyValuePair<string,(string Unflagged,string Flagged)> kvp in dictExpected) {
				BlockoutPerms blockoutPerms=BlockoutPerms.ForUser(_dictUsers[kvp.Key]);
				Assert.AreEqual(kvp.Value.Unflagged,string.Join(",",listActions.Where(x => blockoutPerms.CanDo(x,_defNumUnflagged))),kvp.Key+" unflagged");
				Assert.AreEqual(kvp.Value.Flagged,string.Join(",",listActions.Where(x => blockoutPerms.CanDo(x,_defNumNoSched))),kvp.Key+" NS");
				Assert.AreEqual(kvp.Value.Flagged,string.Join(",",listActions.Where(x => blockoutPerms.CanDo(x,_defNumDontCopy))),kvp.Key+" DC");
			}
		}

		[TestMethod]
		public void PermType_FromTheDefinitionsCache() {
			Assert.AreEqual(EnumPermType.BlockoutAdd,BlockoutPerms.GetPermType(BlockoutAction.Create,_defNumUnflagged));
			Assert.AreEqual(EnumPermType.Blockouts,BlockoutPerms.GetPermType(BlockoutAction.Edit,_defNumUnflagged));
			Assert.AreEqual(EnumPermType.BlockoutDelete,BlockoutPerms.GetPermType(BlockoutAction.Cut,_defNumUnflagged));
			Assert.AreEqual(EnumPermType.BlockoutsFlagged,BlockoutPerms.GetPermType(BlockoutAction.Create,_defNumNoSched));
			Assert.AreEqual(EnumPermType.BlockoutsFlagged,BlockoutPerms.GetPermType(BlockoutAction.Delete,_defNumDontCopy));
			Assert.AreEqual(EnumPermType.BlockoutAdd,BlockoutPerms.GetPermType(BlockoutAction.Paste,987654321L),"Unknown type counts as unflagged.");
		}

		[TestMethod]
		public void PermTypesToDelete_OnePerKindOfOverlappedBlockout() {
			Schedule scheduleUnflagged=new Schedule { SchedType=ScheduleType.Blockout,BlockoutType=_defNumUnflagged };
			Schedule scheduleNoSched=new Schedule { SchedType=ScheduleType.Blockout,BlockoutType=_defNumNoSched };
			Schedule scheduleDontCopy=new Schedule { SchedType=ScheduleType.Blockout,BlockoutType=_defNumDontCopy };
			CollectionAssert.AreEqual(new List<EnumPermType> { EnumPermType.BlockoutsFlagged,EnumPermType.BlockoutDelete },
				BlockoutPerms.GetPermTypesToDelete(new List<Schedule> { scheduleUnflagged,scheduleNoSched,scheduleDontCopy,scheduleUnflagged }));
			Assert.IsTrue(BlockoutPerms.GetPermTypesToDelete(new List<Schedule> { scheduleUnflagged }).All(x => BlockoutPerms.ForUser(_dictUsers["delete"]).Has(x)));
			Assert.IsFalse(BlockoutPerms.GetPermTypesToDelete(new List<Schedule> { scheduleUnflagged }).All(x => BlockoutPerms.ForUser(_dictUsers["add"]).Has(x)),
				"Blockout Add alone may not replace an overlapped blockout.");
		}
		#endregion Permissions from user groups

		#region Audit entries
		[TestMethod]
		public void Log_EachActionUnderItsOwnPermission() {
			Schedule scheduleUnflagged=AddBlockout(_defNumUnflagged,_opNumA,8);
			foreach(BlockoutAction blockoutAction in new[] { BlockoutAction.Create,BlockoutAction.Paste,BlockoutAction.Copy,BlockoutAction.Edit,
				BlockoutAction.Delete,BlockoutAction.Cut })
			{
				Schedules.BlockoutLogHelper(blockoutAction,scheduleUnflagged);
			}
			CollectionAssert.AreEqual(new List<int> { 272,272,272,13,273,273 },LoggedPermTypes());
		}

		[TestMethod]
		public void Log_FlaggedBlockoutsStayUnderBlockoutsFlagged() {
			Schedule scheduleNoSched=AddBlockout(_defNumNoSched,_opNumA,10);
			Schedule scheduleDontCopy=AddBlockout(_defNumDontCopy,_opNumB,12);
			Schedules.BlockoutLogHelper(BlockoutAction.Create,scheduleNoSched);
			Schedules.BlockoutLogHelper(BlockoutAction.Edit,scheduleNoSched);
			Schedules.BlockoutLogHelper(BlockoutAction.Delete,scheduleDontCopy);
			CollectionAssert.AreEqual(new List<int> { 266,266,266 },LoggedPermTypes());
		}

		[TestMethod]
		public void Log_ClearDefaultsToBlockoutDelete() {
			Schedules.BlockoutLogHelper(BlockoutAction.Clear,dateTime:_date);
			Schedules.BlockoutLogHelper(BlockoutAction.Clear,dateTime:_date,opNum:_opNumA,permTypeClear:EnumPermType.BlockoutsFlagged);
			CollectionAssert.AreEqual(new List<int> { 273,266 },LoggedPermTypes());
		}

		[TestMethod]
		public void Log_WebSchedAsapDeleteIsBlockoutDelete() {
			Schedule scheduleAsap=AddBlockout(0,_opNumA,14,scheduleType:ScheduleType.WebSchedASAP);
			Schedules.BlockoutLogHelper(BlockoutAction.Delete,scheduleAsap);
			CollectionAssert.AreEqual(new List<int> { 273 },LoggedPermTypes());
			StringAssert.Contains(DataCore.GetScalar("SELECT LogText FROM securitylog"),"Web Schedule ASAP");
		}
		#endregion Audit entries

		#region Copy and paste of days
		[TestMethod]
		public void PasteDay_UnflaggedIsLoggedAsBlockoutAdd() {
			AddBlockout(_defNumUnflagged,_opNumA,8);
			AddBlockout(_defNumNoSched,_opNumA,10);
			DateTime dateTo=_date.AddDays(1);
			Assert.AreEqual("",Schedules.CopyBlockouts(0,false,true,false,_date,_date,dateTo,dateTo,1,includeUnflagged:true,includeFlagged:true));
			Assert.AreEqual("Lunch@A,NS@A",Remaining(dateTo));
			CollectionAssert.AreEqual(new List<int> { 266,272 },LoggedPermTypes(),"One paste entry per kind; no replace entry without Replace Existing.");
		}

		[TestMethod]
		public void PasteDay_ReplaceLogsTheDeletionUnderBlockoutDelete() {
			AddBlockout(_defNumUnflagged,_opNumA,8);
			DateTime dateTo=_date.AddDays(1);
			AddBlockout(_defNumUnflagged,_opNumB,15,dateTo);//Replaced.
			AddBlockout(_defNumNoSched,_opNumB,16,dateTo);//Kept: the user is pasting unflagged blockouts only.
			Assert.AreEqual("",Schedules.CopyBlockouts(0,false,true,true,_date,_date,dateTo,dateTo,1,includeUnflagged:true,includeFlagged:false));
			Assert.AreEqual("Lunch@A,NS@B",Remaining(dateTo));
			List<int> listPermTypes=LoggedPermTypes();
			CollectionAssert.AreEqual(new List<int> { 272,273 },listPermTypes);
			StringAssert.Contains(DataCore.GetScalar("SELECT LogText FROM securitylog WHERE PermType=273"),"replaced");
		}

		[TestMethod]
		public void PasteDay_FlaggedOnlyUserNeverTouchesUnflagged() {
			//What FormBlockoutCutCopyPaste passes for a user with BlockoutsFlagged and no Blockout Add.
			BlockoutPerms blockoutPerms=BlockoutPerms.ForUser(_dictUsers["flagged"]);
			AddBlockout(_defNumUnflagged,_opNumA,8);
			AddBlockout(_defNumNoSched,_opNumA,10);
			DateTime dateTo=_date.AddDays(1);
			AddBlockout(_defNumUnflagged,_opNumB,15,dateTo);
			Assert.IsTrue(blockoutPerms.CanReplaceOnPaste());
			Assert.AreEqual("",Schedules.CopyBlockouts(0,false,true,true,_date,_date,dateTo,dateTo,1,
				includeUnflagged:blockoutPerms.HasUnflagged(BlockoutAction.Paste),includeFlagged:blockoutPerms.HasFlagged));
			Assert.AreEqual("Lunch@B,NS@A",Remaining(dateTo),"The unflagged blockout there stays, and the unflagged one is not copied.");
			CollectionAssert.AreEqual(new List<int> { 266 },LoggedPermTypes());
		}
		#endregion Copy and paste of days

		#region Preferences (26.1)
		[TestMethod]
		public void MakeRecallSearchesOpenings_SeededOnKeepingTheOldBehavior() {
			Assert.AreEqual("1",DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='AppointmentMakeRecallSearchesOpenings'"));
			Assert.IsTrue(PrefC.GetBool(PrefName.AppointmentMakeRecallSearchesOpenings));
			try {
				Prefs.UpdateBool(PrefName.AppointmentMakeRecallSearchesOpenings,false);
				Assert.IsFalse(PrefC.GetBool(PrefName.AppointmentMakeRecallSearchesOpenings));
			}
			finally {
				Prefs.UpdateBool(PrefName.AppointmentMakeRecallSearchesOpenings,true);
			}
		}

		[TestMethod]
		public void ScheduleProvUnassigned_StillDecidesHoursOfOperatoriesWithNoProvider() {
			const long provNumX=900101;
			const long provNumY=900102;
			long prefOriginal=PrefC.GetLong(PrefName.ScheduleProvUnassigned);
			List<Schedule> listSchedules=new List<Schedule> {
				new Schedule { SchedDate=_date,SchedType=ScheduleType.Provider,ProvNum=provNumX,StartTime=TimeSpan.FromHours(8),StopTime=TimeSpan.FromHours(12) },
				new Schedule { SchedDate=_date,SchedType=ScheduleType.Provider,ProvNum=provNumY,StartTime=TimeSpan.FromHours(13),StopTime=TimeSpan.FromHours(17) },
			};
			Operatory operatoryNoProv=new Operatory { OperatoryNum=_opNumA,ProvDentist=0,ProvHygienist=0,IsHygiene=false };
			try {
				Prefs.UpdateLong(PrefName.ScheduleProvUnassigned,provNumX);
				CollectionAssert.AreEqual(new List<long> { provNumX },Schedules.GetProvSchedsForOp(listSchedules,operatoryNoProv).Select(x => x.ProvNum).ToList());
				Prefs.UpdateLong(PrefName.ScheduleProvUnassigned,provNumY);
				CollectionAssert.AreEqual(new List<long> { provNumY },Schedules.GetProvSchedsForOp(listSchedules,operatoryNoProv).Select(x => x.ProvNum).ToList());
				Operatory operatoryWithDentist=new Operatory { OperatoryNum=_opNumB,ProvDentist=provNumX,IsHygiene=false };
				CollectionAssert.AreEqual(new List<long> { provNumX },Schedules.GetProvSchedsForOp(listSchedules,operatoryWithDentist).Select(x => x.ProvNum).ToList(),
					"An operatory with its own dentist ignores the preference.");
			}
			finally {
				Prefs.UpdateLong(PrefName.ScheduleProvUnassigned,prefOriginal);
			}
		}
		#endregion Preferences (26.1)
	}
}
