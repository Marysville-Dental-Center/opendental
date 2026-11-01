// One scratch database for the V-PEARL1 tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded to the current
// schema (Open Dental 26.1.70) by the fork migration runner, with made-up Pearl settings. Synthetic data only. Skipped (inconclusive) when the local MariaDB
// does not answer. The rule tests need no database and always run.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using CodeBase;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;
using UnitTestsCore;

namespace VPEARL1Tests {
	[TestClass]
	public class PearlDb {
		public static readonly string DbName=TestDb.ScratchName("vpearl1");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;
		///<summary>Made-up Pearl settings. Not credentials of any account.</summary>
		public const string CLIENT_ID="vpearl1-client";
		public const string CLIENT_SECRET="vpearl1-not-a-secret";
		public const string ORGANIZATION_ID="vpearl1-org";
		public const string OFFICE_ID="vpearl1-office";

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			//Lans.g returns the English text without touching the database only in en-US.
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
			//Pearl.cs simulates Pearl in Debug builds unless this is set; the tests run the real client on the fake HTTP layer instead.
			ODBuild.IsUnitTest=true;
			APIRequest.Inst=new FakePearlHttp();
			SkipReason=TestDb.GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			TestDb.DropStaleScratchDatabases();
			TestDb.CreateFresh24_3(DbName);
			ForkMigrationRunner.Run();
			SetPearlProperty(OpenPmsDentalBusiness.Bridges.Pearl.PEARL_CLIENT_ID_PROPERTY,CLIENT_ID);
			SetPearlProperty(OpenPmsDentalBusiness.Bridges.Pearl.PEARL_CLIENT_SECRET_PROPERTY,CLIENT_SECRET);
			SetPearlProperty(OpenPmsDentalBusiness.Bridges.Pearl.PEARL_ORGANIZATION_ID_PROPERTY,ORGANIZATION_ID);
			SetPearlProperty(OpenPmsDentalBusiness.Bridges.Pearl.PEARL_OFFICE_ID_PROPERTY,OFFICE_ID);
			RefreshCaches();
			//PrefC.GetTempFolderPath empties the whole user temp folder the first time it creates temp\opendental. The real client saves the image
			//there before the (fake) upload, so make sure the folder is there and nothing else in the temp folder is touched.
			Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"opendental"));
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
			Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Defs,InvalidType.Security,InvalidType.Programs);
		}

		private static void SetPearlProperty(string desc,string value) {
			DataCore.NonQ("UPDATE programproperty SET PropertyValue='"+POut.String(value)+"' WHERE PropertyDesc='"+POut.String(desc)+"' "
				+"AND ProgramNum=(SELECT ProgramNum FROM program WHERE ProgName='Pearl')");
		}

		///<summary>A made-up patient for one test.</summary>
		public static Patient CreatePatient(string suffix) {
			return PatientT.CreatePatient(suffix,lName:"Vpearl",fName:"Synthetic");
		}

		///<summary>A radiograph row for the patient, stored in the database only (no image file is needed: the tests pass the bitmap).</summary>
		public static Document CreateDocument(Patient patient) {
			Document document=new Document();
			document.PatNum=patient.PatNum;
			document.FileName="vpearl1.jpg";
			document.Description="";
			document.Note="";
			document.Signature="";
			document.DocCategory=Defs.GetDefsForCategory(DefCat.ImageCats,true)[0].DefNum;
			document.DateCreated=DateTime.Now;
			document.ImgType=ImageType.Radiograph;
			document.RawBase64="";
			document.ToothNumbers="";
			Documents.Insert(document,patient);
			return Documents.GetByNum(document.DocNum);
		}

		///<summary>Inserts a request for the document as another session left it.</summary>
		public static PearlRequest InsertRequest(long docNum,EnumPearlStatus pearlStatus,string requestId=null) {
			PearlRequest pearlRequest=new PearlRequest();
			pearlRequest.RequestId=requestId??Guid.NewGuid().ToString();
			pearlRequest.DocNum=docNum;
			pearlRequest.RequestStatus=pearlStatus;
			pearlRequest.DateTSent=DateTime.Now.AddMinutes(-30);
			PearlRequests.Insert(pearlRequest);
			return pearlRequest;
		}

		///<summary>A small blank image to send.</summary>
		public static Bitmap CreateBitmap() {
			return new Bitmap(40,40);
		}
	}
}
