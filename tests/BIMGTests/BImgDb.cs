// One scratch database for the B-IMG tests: a fresh 24.3.63 database (built like tools/New-DevDatabase.ps1) upgraded to the Open Dental 25.3.48
// schema by the fork migration runner. Synthetic data only. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using UnitTestsCore;

namespace BIMGTests {
	[TestClass]
	public class BImgDb {
		public static readonly string DbName=TestDb.ScratchName("bimg");
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			//The font size boxes parse and format in the current culture, and Lans.g returns the English text without the database only in en-US.
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
			Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Defs,InvalidType.Security);
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

		///<summary>A made-up patient for one test.</summary>
		public static Patient CreatePatient(string suffix) {
			return PatientT.CreatePatient(suffix,lName:"Bimg",fName:"Synthetic");
		}

		///<summary>A document row for the patient with the given file name (no file is needed: the rules read the row only).</summary>
		public static Document CreateDocument(Patient patient,string fileName) {
			Document document=new Document();
			document.PatNum=patient.PatNum;
			document.FileName=fileName;
			document.Description="";
			document.Note="";
			document.Signature="";
			document.DocCategory=Defs.GetDefsForCategory(DefCat.ImageCats,true)[0].DefNum;
			document.DateCreated=DateTime.Today;
			document.ImgType=ImageType.Photo;
			document.RawBase64="";
			document.ToothNumbers="";
			Documents.Insert(document,patient);
			return Documents.GetByNum(document.DocNum);
		}

		///<summary>A drawing on the document: a text drawing made in this program, or an AI annotation from another vendor.</summary>
		public static ImageDraw AddDrawing(Document document,EnumImageAnnotVendor imageAnnotVendor) {
			ImageDraw imageDraw=new ImageDraw();
			imageDraw.DocNum=document.DocNum;
			imageDraw.ColorDraw=Color.Red;
			imageDraw.ColorBack=Color.Transparent;
			imageDraw.DrawType=ImageDrawType.Text;
			imageDraw.FontSize=12;
			imageDraw.ImageAnnotVendor=imageAnnotVendor;
			imageDraw.Details="";
			imageDraw.DrawingSegment="";
			imageDraw.SetLocAndText(new Point(10,20),"Synthetic note");
			ImageDraws.Insert(imageDraw);
			return imageDraw;
		}
	}
}
