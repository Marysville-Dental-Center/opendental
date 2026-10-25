// One scratch database for the A-IMG tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded to the Open Dental
// 25.3.48 schema by the fork migration runner. Synthetic data only. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.FileIO;
using OpenDentBusiness.ForkMigrations;
using UnitTestsCore;

namespace ImagingTests {
	[TestClass]
	public class ImagingDb {
		public static readonly string DbName=TestDb.ScratchName("aimg");
		///<summary>Null when the database is ready; otherwise why the tests are skipped.</summary>
		public static string SkipReason;
		///<summary>The groups that held ImageEdit right after the upgrade, before any test added a group of its own.</summary>
		public static List<long> ListGroupsImageEditAtUpgrade=new List<long>();

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			//Lans.g returns the English text without touching the database only in en-US.
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
			SkipReason=TestDb.GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.CreateFresh24_3(DbName);
			ForkMigrationRunner.Run();
			RefreshCaches();
			ListGroupsImageEditAtUpgrade=GroupPermissions.GetWhere(x => x.PermType==EnumPermType.ImageEdit).Select(x => x.UserGroupNum).Distinct().ToList();
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(SkipReason!=null || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
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
			TestDb.Connect(DbName);
		}

		public static void RefreshCaches() {
			Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Defs,InvalidType.Security);
		}

		///<summary>A made-up patient for one test.</summary>
		public static Patient CreatePatient(string suffix) {
			return PatientT.CreatePatient(suffix,lName:"Aimg",fName:"Synthetic");
		}

		///<summary>Logs in a new user whose only group has exactly these permissions, none date-restricted. Returns the user.</summary>
		public static Userod LogInWith(params EnumPermType[] arrayPermTypes) {
			return LogInWithDays(arrayPermTypes.Select(x => (x,0)).ToArray());
		}

		///<summary>Logs in a new user whose only group has these permissions, each with its "newer than this many days" restriction (0 for none).
		///</summary>
		public static Userod LogInWithDays(params (EnumPermType perm,int newerDays)[] arrayPerms) {
			long userGroupNum=UserGroupT.CreateUserGroup("A-IMG test group "+Guid.NewGuid().ToString("N").Substring(0,8));
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

		///<summary>The first image category, which the security log text names.</summary>
		public static long GetImageCategory() {
			return Defs.GetDefsForCategory(DefCat.ImageCats,true).First().DefNum;
		}

		///<summary>A document row for the patient, stored in the database (no A-to-Z folder needed). Signed when signature is not empty.</summary>
		public static Document CreateDocument(Patient patient,string signature,DateTime dateCreated) {
			Document document=new Document();
			document.PatNum=patient.PatNum;
			document.FileName=".jpg";
			document.Description="";
			document.Note="";
			document.Signature=signature;
			document.DocCategory=GetImageCategory();
			document.DateCreated=dateCreated;
			document.ImgType=ImageType.Photo;
			document.RawBase64="";
			document.ToothNumbers="";
			Documents.Insert(document,patient);
			return Documents.GetByNum(document.DocNum);
		}

		///<summary>A preauthorization (claim.ClaimType "PreAuth") for the patient. Returns its ClaimNum.</summary>
		public static long CreatePreAuth(Patient patient,DateTime dateSent) {
			Claim claim=new Claim();
			claim.PatNum=patient.PatNum;
			claim.ClaimType="PreAuth";
			claim.ClaimStatus="S";
			claim.DateSent=dateSent;
			claim.DateService=dateSent;
			return Claims.Insert(claim);
		}

		///<summary>Switches image storage: InDatabase keeps EOBs in eobattach.RawBase64; LocalAtoZ uses atoZPath as this computer's A-to-Z folder.
		///</summary>
		public static void SetStorage(DataStorageType dataStorageType,string atoZPath=null) {
			Prefs.UpdateInt(PrefName.AtoZfolderUsed,(int)dataStorageType);
			Cache.Refresh(InvalidType.Prefs);
			FileAtoZ.LocalAtoZpath=atoZPath;
		}

		///<summary>Security log rows of one permission for the patient (0 for any patient), newest last.</summary>
		public static List<SecurityLog> GetLogs(EnumPermType perm,long patNum) {
			string command="SELECT * FROM securitylog WHERE PermType="+POut.Int((int)perm);
			if(patNum!=0) {
				command+=" AND PatNum="+POut.Long(patNum);
			}
			command+=" ORDER BY SecurityLogNum";
			return OpenDentBusiness.Crud.SecurityLogCrud.SelectMany(command);
		}
	}
}
