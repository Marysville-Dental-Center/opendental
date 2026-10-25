// Database tests for flexible group notes (package A-GN) on a scratch database (see ScratchDb): group notes shaped like the ones a 25.3 office
// database can hold (no procedures, or procedures of mixed status, date and provider, which 24.3 would not have made) load, sort and keep their
// signatures; procedures are added to and removed from an existing group note. Synthetic rows only. Skipped (inconclusive) when the local MariaDB
// does not answer.
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
using OpenDental.UI;

namespace GroupNoteTests {
	[TestClass]
	public class GroupNoteDbTests {
		private static readonly string DB_NAME=ScratchDb.NewName();
		private static string _skipReason;
		private static long _codeNumGroup;
		private static long _codeNumA;
		private static long _codeNumB;
		private static long _provNumA;
		private static long _provNumB;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			try {
				if(!Prefs.DictIsNull()) {
					Prefs.ClearCache();//The tests without a database may have filled it with a few values.
				}
				ScratchDb.Create(DB_NAME);
			}
			catch(Exception ex) {
				_skipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
				return;
			}
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			//Synthetic procedure codes and providers, so nothing depends on the seed rows.
			_codeNumA=OpenDentBusiness.Crud.ProcedureCodeCrud.Insert(new ProcedureCode { ProcCode="GNT01",Descript="Group note test A",AbbrDesc="GNTA",
				ProcTime="/X/",TreatArea=TreatmentArea.Mouth });
			_codeNumB=OpenDentBusiness.Crud.ProcedureCodeCrud.Insert(new ProcedureCode { ProcCode="GNT02",Descript="Group note test B",AbbrDesc="GNTB",
				ProcTime="/X/",TreatArea=TreatmentArea.Mouth });
			_provNumA=OpenDentBusiness.Crud.ProviderCrud.Insert(new Provider { Abbr="GNA",LName="Alpha",FName="Test",ItemOrder=90 });
			_provNumB=OpenDentBusiness.Crud.ProviderCrud.Insert(new Provider { Abbr="GNB",LName="Beta",FName="Test",ItemOrder=91 });
			Cache.Refresh(InvalidType.Prefs,InvalidType.ProcCodes,InvalidType.Providers,InvalidType.Defs);
			_codeNumGroup=ProcedureCodes.GetCodeNum(ProcedureCodes.GroupProcCode);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("GN_TEST_KEEP_DB")=="1") {
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
			Security.CurUser=new Userod { UserNum=1,UserName="gntest" };
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
		}

		private static long NewPatient() {
			return OpenDentBusiness.Crud.PatientCrud.Insert(new Patient { LName="Groupnote",FName="Test"+Guid.NewGuid().ToString("N").Substring(0,6),
				PatStatus=PatientStatus.Patient,PriProv=_provNumA,Birthdate=new DateTime(1980,1,1) });
		}

		private static Procedure NewProc(long patNum,long codeNum,ProcStat procStat,DateTime date,long provNum) {
			Procedure procedure=new Procedure { PatNum=patNum,CodeNum=codeNum,ProcStatus=procStat,ProcDate=date,DateTP=date,DateEntryC=date,ProvNum=provNum,
				ToothNum="",Surf="",ToothRange="" };
			procedure.ProcNum=OpenDentBusiness.Crud.ProcedureCrud.Insert(procedure);
			return procedure;
		}

		private static Procedure NewGroupNote(long patNum,DateTime date,long provNum,params Procedure[] arrayProceduresAttached) {
			Procedure procedureGroup=NewProc(patNum,_codeNumGroup,ProcStat.EC,date,provNum);
			for(int i=0;i<arrayProceduresAttached.Length;i++) {
				ProcGroupItems.Insert(new ProcGroupItem { GroupNum=procedureGroup.ProcNum,ProcNum=arrayProceduresAttached[i].ProcNum });
			}
			return procedureGroup;
		}

		[TestMethod]
		public void SyncForGroup_AddsAndRemoves_KeepsLinksThatStay() {
			long patNum=NewPatient();
			Procedure procA=NewProc(patNum,_codeNumA,ProcStat.C,new DateTime(2026,3,1),_provNumA);
			Procedure procB=NewProc(patNum,_codeNumB,ProcStat.TP,new DateTime(2026,3,8),_provNumB);
			Procedure procC=NewProc(patNum,_codeNumA,ProcStat.EO,new DateTime(2025,12,1),_provNumB);
			Procedure procedureGroup=NewGroupNote(patNum,new DateTime(2026,3,1),_provNumA,procA,procB);
			long procGroupItemNumB=ProcGroupItems.GetForGroup(procedureGroup.ProcNum).Single(x => x.ProcNum==procB.ProcNum).ProcGroupItemNum;
			List<ProcGroupItem> listProcGroupItems=ProcGroupItems.SyncForGroup(procedureGroup.ProcNum,new List<long> { procB.ProcNum,procC.ProcNum,0,procedureGroup.ProcNum });
			CollectionAssert.AreEquivalent(new List<long> { procB.ProcNum,procC.ProcNum },listProcGroupItems.Select(x => x.ProcNum).ToList());
			Assert.AreEqual(procGroupItemNumB,listProcGroupItems.Single(x => x.ProcNum==procB.ProcNum).ProcGroupItemNum,"The link that stays keeps its number.");
			CollectionAssert.AreEqual(listProcGroupItems.Select(x => x.ProcNum).OrderBy(x => x).ToList(),listProcGroupItems.Select(x => x.ProcNum).ToList(),
				"Ordered by ProcNum like GetForGroup, for the signature key.");
			Assert.AreEqual(0,ProcGroupItems.SyncForGroup(procedureGroup.ProcNum,new List<long>()).Count,"Everything removed.");
			Assert.AreEqual(0,ProcGroupItems.GetForGroup(procedureGroup.ProcNum).Count);
		}

		[TestMethod]
		public void OfficeShapedGroupNotes_LoadWithTheirProcedures() {
			//Shapes 24.3 would refuse but a 25.3 database can hold: no procedures; TP and C procedures with different dates and providers.
			long patNum=NewPatient();
			Procedure procC=NewProc(patNum,_codeNumA,ProcStat.C,new DateTime(2026,4,1),_provNumA);
			Procedure procTp=NewProc(patNum,_codeNumB,ProcStat.TP,new DateTime(2026,4,9),_provNumB);
			Procedure procedureMixed=NewGroupNote(patNum,new DateTime(2026,4,1),_provNumA,procC,procTp);
			Procedure procedureEmpty=NewGroupNote(patNum,new DateTime(2026,4,5),_provNumB);
			Assert.AreEqual(2,ProcGroupItems.GetForGroup(procedureMixed.ProcNum).Count);
			Assert.AreEqual(0,ProcGroupItems.GetForGroup(procedureEmpty.ProcNum).Count);
			Assert.AreEqual(1,ProcGroupItems.GetCountCompletedProcsForGroup(procedureMixed.ProcNum));
			List<ProcStat> listProcStats=ProcGroupItems.GetForGroup(procedureMixed.ProcNum)
				.Select(x => Procedures.GetOneProc(x.ProcNum,false).ProcStatus).ToList();
			Assert.AreEqual(EnumPermType.ProcCompleteNote,GroupNotes.GetNotePermission(listProcStats));
			Assert.AreEqual(EnumPermType.ProcCompleteNote,GroupNotes.GetNotePermission(new List<ProcStat>()),"Empty group note");
			//Both can still be attached to and edited: the procedures pass the 25.1 rules.
			Assert.IsNull(GroupNotes.GetAttachBlockReason(Procedures.GetOneProc(procTp.ProcNum,false)));
			Assert.IsNotNull(GroupNotes.GetAttachBlockReason(Procedures.GetOneProc(procedureEmpty.ProcNum,false)),"A group note is never attached.");
		}

		[TestMethod]
		public void ProgressNotes_GroupNoteAfterItsLastProcedure_EmptyOneByItsDate() {
			long patNum=NewPatient();
			Procedure procC=NewProc(patNum,_codeNumA,ProcStat.C,new DateTime(2026,5,1),_provNumA);
			Procedure procTp=NewProc(patNum,_codeNumB,ProcStat.TP,new DateTime(2026,5,20),_provNumB);
			Procedure procOther=NewProc(patNum,_codeNumA,ProcStat.C,new DateTime(2026,5,25),_provNumA);
			Procedure procedureMixed=NewGroupNote(patNum,new DateTime(2026,5,1),_provNumA,procC,procTp);
			Procedure procedureEmpty=NewGroupNote(patNum,new DateTime(2026,5,10),_provNumB);
			DataTable table=ChartModules.GetProgNotes(patNum,false,new ChartModuleFilters());
			List<long> listProcNums=table.Rows.Cast<DataRow>().Select(x => PIn.Long(x["ProcNum"].ToString())).Where(x => x!=0).ToList();
			List<long> listExpected=new List<long> { procC.ProcNum,procedureEmpty.ProcNum,procTp.ProcNum,procedureMixed.ProcNum,procOther.ProcNum };
			CollectionAssert.AreEqual(listExpected,listProcNums);
		}

		[TestMethod]
		public void ProgressNotes_243ShapedGroupNote_StaysAfterItsProcedures() {
			//A group note made by 24.3: completed procedures on its own date. It shows after them, as before.
			long patNum=NewPatient();
			DateTime date=new DateTime(2026,6,2);
			Procedure procA=NewProc(patNum,_codeNumA,ProcStat.C,date,_provNumA);
			Procedure procB=NewProc(patNum,_codeNumB,ProcStat.C,date,_provNumA);
			Procedure procedureGroup=NewGroupNote(patNum,date,_provNumA,procA,procB);
			DataTable table=ChartModules.GetProgNotes(patNum,false,new ChartModuleFilters());
			List<long> listProcNums=table.Rows.Cast<DataRow>().Select(x => PIn.Long(x["ProcNum"].ToString())).Where(x => x!=0).ToList();
			Assert.AreEqual(procedureGroup.ProcNum,listProcNums.Last());
			CollectionAssert.AreEquivalent(new List<long> { procA.ProcNum,procB.ProcNum },listProcNums.Take(2).ToList());
		}

		[TestMethod]
		public void Signature_FromDatabaseLinks_ValidUntilProceduresChange() {
			long patNum=NewPatient();
			Procedure procA=NewProc(patNum,_codeNumA,ProcStat.C,new DateTime(2026,7,1),_provNumA);
			Procedure procB=NewProc(patNum,_codeNumB,ProcStat.TP,new DateTime(2026,7,3),_provNumB);
			Procedure procedureGroup=NewGroupNote(patNum,new DateTime(2026,7,1),_provNumA,procA);
			procedureGroup.Note="Synthetic group note.";
			procedureGroup.UserNum=1;
			string signature=Sign(GroupNotes.GetSignatureKey(procedureGroup,ProcGroupItems.GetForGroup(procedureGroup.ProcNum)));
			//Saving the same procedures leaves the links and the signature alone.
			ProcGroupItems.SyncForGroup(procedureGroup.ProcNum,new List<long> { procA.ProcNum });
			Assert.IsTrue(IsValid(GroupNotes.GetSignatureKey(procedureGroup,ProcGroupItems.GetForGroup(procedureGroup.ProcNum)),signature));
			//Adding a procedure changes the key, so the window clears the old signature.
			ProcGroupItems.SyncForGroup(procedureGroup.ProcNum,new List<long> { procA.ProcNum,procB.ProcNum });
			Assert.IsFalse(IsValid(GroupNotes.GetSignatureKey(procedureGroup,ProcGroupItems.GetForGroup(procedureGroup.ProcNum)),signature));
		}

		///<summary>Logs in a new user whose only group has exactly these permissions, with no date limits.</summary>
		private static Userod LogInWith(params EnumPermType[] arrayPermTypes) {
			string suffix=Guid.NewGuid().ToString("N").Substring(0,8);
			long userGroupNum=UserGroups.Insert(new UserGroup { Description="GN test group "+suffix });
			for(int i=0;i<arrayPermTypes.Length;i++) {
				GroupPermissions.Insert(new GroupPermission { UserGroupNum=userGroupNum,PermType=arrayPermTypes[i],NewerDate=DateTime.MinValue,NewerDays=0 });
			}
			Userod userod=new Userod { UserName="gntest_"+suffix };
			userod.UserNum=Userods.Insert(userod,new List<long> { userGroupNum });
			Cache.Refresh(InvalidType.Security);
			Security.CurUser=userod;
			return userod;
		}

		///<summary>What the Group Note window does when it saves (FormProcGroup.IsAuthorizedForChanges): statuses from the database, then the checks
		///from GroupNotes.GetSaveChecks against the logged-in user, without message boxes.</summary>
		private static bool CanSave(Procedure procedureGroup,List<Procedure> listProceduresAtLoad,List<Procedure> listProceduresNow,DateTime dateProcNew,
			bool isNoteChanged)
		{
			List<ProcStat> listProcStatsAtLoad=listProceduresAtLoad.Select(x => x.ProcStatus).ToList();
			List<ProcStat> listProcStatsNow=Procedures.GetManyProc(listProceduresNow.Select(x => x.ProcNum).ToList(),false).Select(x => x.ProcStatus).ToList();
			bool isAttachedChanged=!new HashSet<long>(listProceduresNow.Select(x => x.ProcNum)).SetEquals(listProceduresAtLoad.Select(x => x.ProcNum));
			List<GroupNotePermCheck> listChecks=GroupNotes.GetSaveChecks(listProcStatsAtLoad,listProcStatsNow,isAttachedChanged,false,
				procedureGroup.ProcDate,dateProcNew,procedureGroup.DateEntryC,isNoteChanged);
			return GroupNotes.IsSaveAuthorized(listChecks,(permType,date) => Security.IsAuthorized(permType,date,true));
		}

		[TestMethod]
		public void Save_TpGroupNoteGetsCompletedProcedure_RefusedWithoutEditCompletedProcedure() {
			long patNum=NewPatient();
			DateTime date=DateTime.Today.AddDays(-2);
			Procedure procTp=NewProc(patNum,_codeNumA,ProcStat.TP,date,_provNumA);
			Procedure procC=NewProc(patNum,_codeNumB,ProcStat.C,date,_provNumA);
			Procedure procedureGroup=NewGroupNote(patNum,date,_provNumA,procTp);
			List<Procedure> listAtLoad=new List<Procedure> { procTp };
			//TP Procedure Delete and Edit Note on Completed Procedure, but not Edit Completed Procedure.
			LogInWith(EnumPermType.ProcDelete,EnumPermType.ProcCompleteNote);
			Assert.IsTrue(Security.IsAuthorized(GroupNotes.GetStructurePermission(new List<ProcStat> { ProcStat.TP }),procedureGroup.DateEntryC,true),
				"The window lets this user change the procedures of a group note on TP procedures.");
			Assert.IsFalse(CanSave(procedureGroup,listAtLoad,new List<Procedure> { procTp,procC },date,isNoteChanged:true),"Completed procedure added.");
			Assert.IsFalse(CanSave(procedureGroup,listAtLoad,new List<Procedure>(),date,isNoteChanged:true),"Every procedure removed.");
			Assert.IsTrue(CanSave(procedureGroup,listAtLoad,new List<Procedure> { procTp },date.AddDays(-1),isNoteChanged:true),"Still on TP procedures only.");
			//With Edit Completed Procedure both are allowed.
			LogInWith(EnumPermType.ProcDelete,EnumPermType.ProcCompleteNote,EnumPermType.ProcCompleteEdit);
			Assert.IsTrue(CanSave(procedureGroup,listAtLoad,new List<Procedure> { procTp,procC },date,isNoteChanged:true));
			Assert.IsTrue(CanSave(procedureGroup,listAtLoad,new List<Procedure>(),date,isNoteChanged:true));
		}

		[TestMethod]
		public void Save_GroupNoteMovedBeforeTheGlobalLockDate_Refused() {
			long patNum=NewPatient();
			DateTime date=DateTime.Today.AddDays(-2);
			Procedure procTp=NewProc(patNum,_codeNumA,ProcStat.TP,date,_provNumA);
			Procedure procC=NewProc(patNum,_codeNumB,ProcStat.C,date,_provNumA);
			Procedure procedureGroupTp=NewGroupNote(patNum,date,_provNumA,procTp);
			Procedure procedureGroupC=NewGroupNote(patNum,date,_provNumA,procC);
			LogInWith(EnumPermType.ProcDelete,EnumPermType.ProcCompleteNote,EnumPermType.ProcCompleteEdit);
			DateTime dateLock=DateTime.Today.AddDays(-30);
			string lockDateOld=PrefC.GetString(PrefName.SecurityLockDate);
			Prefs.UpdateString(PrefName.SecurityLockDate,POut.Date(dateLock,false));
			Cache.Refresh(InvalidType.Prefs);
			try {
				DateTime dateBeforeLock=dateLock.AddDays(-5);
				Assert.IsFalse(CanSave(procedureGroupC,new List<Procedure> { procC },new List<Procedure> { procC },dateBeforeLock,isNoteChanged:false),
					"A group note on a completed procedure can't be moved before the lock date.");
				Assert.IsFalse(CanSave(procedureGroupTp,new List<Procedure> { procTp },new List<Procedure> { procTp,procC },dateBeforeLock,isNoteChanged:false),
					"Nor one on TP procedures that becomes complete.");
				Assert.IsTrue(CanSave(procedureGroupC,new List<Procedure> { procC },new List<Procedure> { procC },date.AddDays(-1),isNoteChanged:false),
					"After the lock date it can.");
			}
			finally {
				Prefs.UpdateString(PrefName.SecurityLockDate,lockDateOld);
				Cache.Refresh(InvalidType.Prefs);
			}
		}

		private static string Sign(string keyData) {
			using(SignatureBox signatureBox=new SignatureBox()) {
				signatureBox.SetKeyString("0000000000000000");
				signatureBox.SetAutoKeyData(keyData);
				signatureBox.SetPointList(new List<PointF> { new PointF(5,5),new PointF(25,18),new PointF(40,9) });
				return signatureBox.GetSigString();
			}
		}

		private static bool IsValid(string keyData,string signature) {
			using(SignatureBox signatureBox=new SignatureBox()) {
				signatureBox.SetKeyString("0000000000000000");
				signatureBox.SetAutoKeyData(keyData);
				signatureBox.SetSigString(signature);
				return signatureBox.NumberOfTabletPoints()>0;
			}
		}
	}
}
