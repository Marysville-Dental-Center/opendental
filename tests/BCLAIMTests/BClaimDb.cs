// One scratch database for the B-CLAIM database tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded to the Open
// Dental 25.3.48 schema by the fork migration runner. Synthetic data only (made-up Z codes and carrier names). Skipped (inconclusive) when the
// local MariaDB does not answer, or when FORKMIGRATION_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.FileIO;
using OpenDentBusiness.ForkMigrations;
using UnitTestsCore;

namespace BCLAIMTests {
	[TestClass]
	public class BClaimDb {
		public static readonly string DbName=TestDb.ScratchName("bclaim");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;
		private static bool _isBuilt;
		///<summary>This run's A to Z folder, under the system temp folder. Deleted at the end.</summary>
		public static readonly string AtoZPath=Path.Combine(Path.GetTempPath(),"bclaim_atoz_"+Guid.NewGuid().ToString("N").Substring(0,8));

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			SkipReason=TestDb.GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			try {
				TestDb.DropStaleScratchDatabases();
				TestDb.CreateFresh24_3(DbName);
				ForkMigrationRunner.Run();
				Cache.Refresh(InvalidType.AllLocal);
				Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1 };
				_isBuilt=true;
			}
			catch(Exception ex) {
				SkipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			try {
				if(Directory.Exists(AtoZPath)) {
					Directory.Delete(AtoZPath,true);
				}
			}
			catch(Exception) {
				//Only a temp folder.
			}
			if(!_isBuilt || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DbName);
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		///<summary>Call from [TestInitialize]: skips the test when there is no database, otherwise connects to it.</summary>
		public static void Begin() {
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			TestDb.Connect(DbName);
			Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1 };
		}

		public static string NewSuffix() {
			return Guid.NewGuid().ToString("N").Substring(0,8);
		}

		///<summary>Logs in a new user whose only group has these permissions, each with its "newer than this many days" restriction (0 for none).
		///</summary>
		public static Userod LogInWithDays(params (EnumPermType perm,int newerDays)[] arrayPerms) {
			long userGroupNum=UserGroupT.CreateUserGroup("B-CLAIM test group "+NewSuffix());
			for(int i=0;i<arrayPerms.Length;i++) {
				GroupPermissions.Insert(new GroupPermission {
					UserGroupNum=userGroupNum,
					PermType=arrayPerms[i].perm,
					NewerDate=DateTime.MinValue,
					NewerDays=arrayPerms[i].newerDays,
				});
			}
			GroupPermissions.RefreshCache();
			Userod userod=UserodT.CreateUser(userGroupNumbers:new List<long> { userGroupNum });
			Security.CurUser=userod;
			return userod;
		}

		///<summary>A made-up carrier and plan. Returns the plan.</summary>
		public static InsPlan CreatePlan(string carrierName,bool isMedical=false) {
			Carrier carrier=CarrierT.CreateCarrier(carrierName);
			return InsPlanT.CreateInsPlan(carrier.CarrierNum,isMedical:isMedical);
		}

		///<summary>A claim of the given type, medical type and status for the patient and plan. Returns it as stored.</summary>
		public static Claim CreateClaim(Patient patient,InsPlan insPlan,long insSubNum,string claimType,EnumClaimMedType medType,string claimStatus,
			DateTime dateService)
		{
			Claim claim=new Claim();
			claim.PatNum=patient.PatNum;
			claim.PlanNum=insPlan.PlanNum;
			claim.InsSubNum=insSubNum;
			claim.ClaimType=claimType;
			claim.MedType=medType;
			claim.ClaimStatus=claimStatus;
			claim.DateService=dateService;
			claim.ProvTreat=patient.PriProv;
			claim.ProvBill=patient.PriProv;
			claim.AttachedFlags="Mail";
			Claims.Insert(claim);
			return Claims.GetClaim(claim.ClaimNum);
		}

		///<summary>A claimproc for the procedure on the claim (claimNum 0 for an estimate).</summary>
		public static ClaimProc CreateClaimProc(Procedure procedure,InsPlan insPlan,long insSubNum,long claimNum,ClaimProcStatus status) {
			ClaimProc claimProc=new ClaimProc();
			claimProc.PatNum=procedure.PatNum;
			claimProc.ProcNum=procedure.ProcNum;
			claimProc.PlanNum=insPlan.PlanNum;
			claimProc.InsSubNum=insSubNum;
			claimProc.ClaimNum=claimNum;
			claimProc.Status=status;
			claimProc.ProvNum=procedure.ProvNum;
			claimProc.ProcDate=procedure.ProcDate;
			claimProc.DateCP=procedure.ProcDate;
			claimProc.FeeBilled=procedure.ProcFee;
			claimProc.CodeSent=ProcedureCodes.GetStringProcCode(procedure.CodeNum);
			claimProc.AllowedOverride=-1;
			claimProc.CopayOverride=-1;
			claimProc.DedEstOverride=-1;
			claimProc.PercentOverride=-1;
			claimProc.PaidOtherInsOverride=-1;
			claimProc.InsEstTotalOverride=-1;
			claimProc.WriteOffEstOverride=-1;
			claimProc.PaidOtherIns=-1;
			claimProc.CopayAmt=-1;
			claimProc.DedEst=-1;
			claimProc.WriteOffEst=-1;
			ClaimProcs.Insert(claimProc);
			return claimProc;
		}

		///<summary>Security log rows of one permission with this FKey, oldest first.</summary>
		public static List<SecurityLog> GetLogs(EnumPermType perm,long fKey) {
			string command="SELECT * FROM securitylog WHERE PermType="+POut.Int((int)perm)+" AND FKey="+POut.Long(fKey)+" ORDER BY SecurityLogNum";
			return OpenDentBusiness.Crud.SecurityLogCrud.SelectMany(command);
		}

		///<summary>Security log rows of one permission for the patient, oldest first.</summary>
		public static List<SecurityLog> GetLogsForPatient(EnumPermType perm,long patNum) {
			string command="SELECT * FROM securitylog WHERE PermType="+POut.Int((int)perm)+" AND PatNum="+POut.Long(patNum)+" ORDER BY SecurityLogNum";
			return OpenDentBusiness.Crud.SecurityLogCrud.SelectMany(command);
		}

		///<summary>Switches image storage: LocalAtoZ uses this run's temp folder as the A to Z folder; InDatabase keeps no files.</summary>
		public static void SetStorage(DataStorageType dataStorageType) {
			Prefs.UpdateInt(PrefName.AtoZfolderUsed,(int)dataStorageType);
			Cache.Refresh(InvalidType.Prefs);
			if(dataStorageType==DataStorageType.LocalAtoZ) {
				Directory.CreateDirectory(AtoZPath);
				FileAtoZ.LocalAtoZpath=AtoZPath;
			}
			else {
				FileAtoZ.LocalAtoZpath=null;
			}
		}
	}

	///<summary>A patient with a dental and a medical plan, and one completed procedure (made-up code) on tooth 3.</summary>
	public class ClaimScenario {
		public Patient Patient_;
		public InsPlan PlanDental;
		public InsPlan PlanMedical;
		public long InsSubNumDental;
		public long InsSubNumMedical;
		public Procedure Procedure_;
		public string CarrierNameDental;

		public static ClaimScenario Create(string procCode="Z9701") {
			ClaimScenario scenario=new ClaimScenario();
			string suffix=BClaimDb.NewSuffix();
			scenario.Patient_=PatientT.CreatePatient(suffix,lName:"Bclaim",fName:"Synthetic");
			scenario.CarrierNameDental="Synthetic Dental Carrier "+suffix;
			scenario.PlanDental=BClaimDb.CreatePlan(scenario.CarrierNameDental);
			scenario.PlanMedical=BClaimDb.CreatePlan("Synthetic Medical Carrier "+suffix,isMedical:true);
			scenario.InsSubNumDental=InsSubT.CreateInsSub(scenario.Patient_.PatNum,scenario.PlanDental.PlanNum).InsSubNum;
			scenario.InsSubNumMedical=InsSubT.CreateInsSub(scenario.Patient_.PatNum,scenario.PlanMedical.PlanNum).InsSubNum;
			scenario.Procedure_=ProcedureT.CreateProcedure(scenario.Patient_,procCode,ProcStat.C,"3",120,DateTime.Today.AddDays(-3));
			return scenario;
		}
	}
}
