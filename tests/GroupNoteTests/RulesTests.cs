// Rule tests for flexible group notes (Open Dental 25.1, package A-GN): which procedures can be attached, the defaults of a new group note, and
// which permission guards it. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace GroupNoteTests {
	[TestClass]
	public class RulesTests {
		private static readonly DateTime _today=new DateTime(2026,10,20);

		[TestInitialize]
		public void TestInitialize() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			TestPrefs.EnsureFilled();
		}

		private static Procedure Proc(long procNum,ProcStat procStat,DateTime date,long provNum=1,long clinicNum=0,bool isLocked=false) {
			return new Procedure { ProcNum=procNum,ProcStatus=procStat,ProcDate=date,ProvNum=provNum,ClinicNum=clinicNum,IsLocked=isLocked,CodeNum=procNum };
		}

		private static string Reason(Procedure procedure,string procCode="D0120",bool isFutureAllowed=false) {
			return GroupNotes.GetAttachBlockReason(procedure,procCode,isFutureAllowed,_today);
		}

		#region Attaching
		[TestMethod]
		public void Attach_AnyStatusIsAllowed() {
			//24.3 accepted only complete procedures. 25.1 accepts every status except deleted.
			foreach(ProcStat procStat in new[] { ProcStat.TP,ProcStat.TPi,ProcStat.C,ProcStat.EC,ProcStat.EO,ProcStat.R,ProcStat.Cn }) {
				Assert.IsNull(Reason(Proc(10,procStat,_today.AddDays(-3))),procStat.ToString());
			}
		}

		[TestMethod]
		public void Attach_FutureTreatmentPlannedIsAllowed() {
			Assert.IsNull(Reason(Proc(10,ProcStat.TP,_today.AddDays(30))));
		}

		[TestMethod]
		public void Attach_GroupNoteIsRefused() {
			Assert.AreEqual("You cannot attach a group note to another group note.",Reason(Proc(10,ProcStat.EC,_today),ProcedureCodes.GroupProcCode));
		}

		[TestMethod]
		public void Attach_LockedIsRefused() {
			Assert.AreEqual("Locked procedures cannot be attached to a group note.",Reason(Proc(10,ProcStat.C,_today,isLocked:true)));
		}

		[TestMethod]
		public void Attach_DeletedOrInvalidatedIsRefused() {
			Assert.IsNotNull(Reason(Proc(10,ProcStat.D,_today)));
			Assert.IsNotNull(Reason(Proc(10,ProcStat.D,_today,isLocked:true)));//invalidated: deleted and locked
		}

		[TestMethod]
		public void Attach_NotAProcedureIsRefused() {
			Assert.IsNotNull(Reason(null));
			Assert.IsNotNull(Reason(Proc(0,ProcStat.C,_today)));
		}

		[TestMethod]
		public void Attach_FutureCompleteFollowsThePreference() {
			Procedure procedure=Proc(10,ProcStat.C,_today.AddDays(1));
			Assert.IsNotNull(Reason(procedure,isFutureAllowed:false));
			Assert.IsNull(Reason(procedure,isFutureAllowed:true));
		}
		#endregion Attaching

		#region Defaults
		[TestMethod]
		public void DefaultDate_LatestAttachedOrToday() {
			Assert.AreEqual(_today,GroupNotes.GetDefaultProcDate(new List<Procedure>(),_today));
			Assert.AreEqual(_today,GroupNotes.GetDefaultProcDate(null,_today.AddHours(15)));
			List<Procedure> listProcedures=new List<Procedure> {
				Proc(1,ProcStat.C,new DateTime(2026,9,1)),Proc(2,ProcStat.TP,new DateTime(2026,9,15,10,30,0)),Proc(3,ProcStat.EO,new DateTime(2025,1,1))
			};
			Assert.AreEqual(new DateTime(2026,9,15),GroupNotes.GetDefaultProcDate(listProcedures,_today));
		}

		[TestMethod]
		public void DefaultProvider_SharedThenUserThenPractice() {
			List<Procedure> listShared=new List<Procedure> { Proc(1,ProcStat.C,_today,provNum:5),Proc(2,ProcStat.TP,_today,provNum:5) };
			Assert.AreEqual(5,GroupNotes.GetDefaultProvNum(listShared,provNumUser:7,provNumPracticeDefault:9));
			List<Procedure> listMixed=new List<Procedure> { Proc(1,ProcStat.C,_today,provNum:5),Proc(2,ProcStat.C,_today,provNum:6) };
			Assert.AreEqual(7,GroupNotes.GetDefaultProvNum(listMixed,provNumUser:7,provNumPracticeDefault:9));
			Assert.AreEqual(9,GroupNotes.GetDefaultProvNum(listMixed,provNumUser:0,provNumPracticeDefault:9));
			Assert.AreEqual(7,GroupNotes.GetDefaultProvNum(new List<Procedure>(),provNumUser:7,provNumPracticeDefault:9));
			Assert.AreEqual(9,GroupNotes.GetDefaultProvNum(null,provNumUser:0,provNumPracticeDefault:9));
		}

		[TestMethod]
		public void DefaultClinic_LikeTheProvider() {
			List<Procedure> listShared=new List<Procedure> { Proc(1,ProcStat.C,_today,clinicNum:3),Proc(2,ProcStat.C,_today,clinicNum:3) };
			List<Procedure> listMixed=new List<Procedure> { Proc(1,ProcStat.C,_today,clinicNum:3),Proc(2,ProcStat.C,_today,clinicNum:4) };
			Assert.AreEqual(3,GroupNotes.GetDefaultClinicNum(listShared,clinicNumCurrent:8,hasClinicsEnabled:true));
			Assert.AreEqual(8,GroupNotes.GetDefaultClinicNum(listMixed,clinicNumCurrent:8,hasClinicsEnabled:true));
			Assert.AreEqual(8,GroupNotes.GetDefaultClinicNum(new List<Procedure>(),clinicNumCurrent:8,hasClinicsEnabled:true));
			Assert.AreEqual(0,GroupNotes.GetDefaultClinicNum(listShared,clinicNumCurrent:8,hasClinicsEnabled:false));
		}
		#endregion Defaults

		#region Permissions
		[TestMethod]
		public void Permission_CompletedAttached_NoteAsIn243() {
			List<ProcStat> listProcStats=new List<ProcStat> { ProcStat.TP,ProcStat.C };
			Assert.AreEqual(EnumPermType.ProcCompleteNote,GroupNotes.GetNotePermission(listProcStats));
			Assert.AreEqual(EnumPermType.ProcCompleteEdit,GroupNotes.GetStructurePermission(listProcStats));
			Assert.AreEqual(EnumPermType.ProcCompleteStatusEdit,GroupNotes.GetDeleteLogPermission(listProcStats));
			//EO and EC count as complete, as ProcGroupItems.GetCountCompletedProcsForGroup counts them.
			Assert.AreEqual(EnumPermType.ProcCompleteNote,GroupNotes.GetNotePermission(new List<ProcStat> { ProcStat.EO }));
			Assert.AreEqual(EnumPermType.ProcCompleteNote,GroupNotes.GetNotePermission(new List<ProcStat> { ProcStat.EC }));
		}

		[TestMethod]
		public void Permission_NoneComplete_TpProcedureDeleteAsIn243() {
			List<ProcStat> listProcStats=new List<ProcStat> { ProcStat.TP,ProcStat.R,ProcStat.D };
			Assert.AreEqual(EnumPermType.ProcDelete,GroupNotes.GetNotePermission(listProcStats));
			Assert.AreEqual(EnumPermType.ProcDelete,GroupNotes.GetStructurePermission(listProcStats));
			Assert.AreEqual(EnumPermType.ProcDelete,GroupNotes.GetDeleteLogPermission(listProcStats));
		}

		[TestMethod]
		public void Permission_NothingAttached_TreatedAsComplete() {
			Assert.IsTrue(GroupNotes.IsTreatedAsComplete(new List<ProcStat>()));
			Assert.IsTrue(GroupNotes.IsTreatedAsComplete(null));
			Assert.AreEqual(EnumPermType.ProcCompleteNote,GroupNotes.GetNotePermission(new List<ProcStat>()));
			Assert.AreEqual(EnumPermType.ProcCompleteEdit,GroupNotes.GetStructurePermission(new List<ProcStat>()));
		}

		[TestMethod]
		public void PermissionDate_OwnDateWhenComplete_EntryDateOtherwise() {
			Procedure procedureGroup=new Procedure { ProcDate=new DateTime(2026,5,1),DateEntryC=new DateTime(2026,5,3) };
			Assert.AreEqual(procedureGroup.ProcDate,GroupNotes.GetPermissionDate(procedureGroup,new List<ProcStat> { ProcStat.C }));
			Assert.AreEqual(procedureGroup.ProcDate,GroupNotes.GetPermissionDate(procedureGroup,new List<ProcStat>()));
			Assert.AreEqual(procedureGroup.DateEntryC,GroupNotes.GetPermissionDate(procedureGroup,new List<ProcStat> { ProcStat.TP }));
		}
		#endregion Permissions

		#region Audit
		[TestMethod]
		public void EditLogText_NamesDateProviderAndProcedures() {
			Procedure procedureOld=new Procedure { ProcDate=new DateTime(2026,10,1),ProvNum=1 };
			Procedure procedureNew=new Procedure { ProcDate=new DateTime(2026,10,2),ProvNum=2 };
			List<Procedure> listAdded=new List<Procedure> { Proc(11,ProcStat.TP,new DateTime(2026,10,5)) };
			List<Procedure> listRemoved=new List<Procedure> { Proc(12,ProcStat.C,new DateTime(2026,9,30)) };
			string text=GroupNotes.GetEditLogText(procedureOld,procedureNew,listAdded,listRemoved,x => "C"+x,x => "P"+x);
			StringAssert.Contains(text,"Date changed from 10/1/2026 to 10/2/2026");
			StringAssert.Contains(text,"Provider changed from P1 to P2");
			StringAssert.Contains(text,"Procedures added: C11 (TP, 10/5/2026)");
			StringAssert.Contains(text,"Procedures removed: C12 (C, 9/30/2026)");
		}

		[TestMethod]
		public void EditLogText_EmptyWhenNothingStructuralChanged() {
			Procedure procedureOld=new Procedure { ProcDate=new DateTime(2026,10,1),ProvNum=1,Note="a" };
			Procedure procedureNew=new Procedure { ProcDate=new DateTime(2026,10,1),ProvNum=1,Note="b" };
			Assert.AreEqual("",GroupNotes.GetEditLogText(procedureOld,procedureNew,new List<Procedure>(),null,x => "",x => ""));
		}
		#endregion Audit
	}
}
