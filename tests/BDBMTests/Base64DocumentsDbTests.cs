// The Create Documents From Base64 Notes tool (Open Dental 25.4, backported to 25.3.39), with in-database storage and with an A to Z folder, on
// the scratch database. Synthetic data only. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BDBMTests {
	[TestClass]
	public class Base64DocumentsDbTests {
		private string _atoZPath;

		[TestInitialize]
		public void Init() {
			BDbmDb.Begin();
		}

		[TestCleanup]
		public void Cleanup() {
			if(BDbmDb.SkipReason!=null) {
				return;
			}
			BDbmDb.SetStorage(DataStorageType.InDatabase);
			if(_atoZPath!=null) {
				ODException.SwallowAnyException(() => Directory.Delete(_atoZPath,recursive:true));
			}
		}

		///<summary>A placeholder document like the ones Open Dental's API left behind: the file is in the note, the row has no file of its own.</summary>
		private static Document CreatePlaceholder(Patient patient,string note,string description="API upload") {
			Document document=new Document();
			document.PatNum=patient.PatNum;
			document.FileName=".jpg";
			document.Description=description;
			document.Note="";
			document.DocCategory=BDbmDb.GetImageCategory();
			document.DateCreated=new DateTime(2024,5,6,7,8,9);
			document.ImgType=ImageType.Photo;
			document.ToothNumbers="3,4";
			document.PrintHeading=true;
			document.ProvNum=0;
			Documents.Insert(document,patient);
			BDbmDb.SetText("document","Note","DocNum",document.DocNum,note);
			return Documents.GetByNum(document.DocNum);
		}

		private static string Base64Note(string extension,byte[] arrayBytes) {
			return DatabaseMaintenances.DOCUMENT_NOTE_PREFIX_BASE64+extension+"_"+Convert.ToBase64String(arrayBytes);
		}

		private static byte[] PngBytes() {
			using Bitmap bitmap=new Bitmap(4,3);
			bitmap.SetPixel(1,1,Color.Red);
			using MemoryStream memoryStream=new MemoryStream();
			bitmap.Save(memoryStream,ImageFormat.Png);
			return memoryStream.ToArray();
		}

		[TestMethod]
		public void InDatabase_CreatesDocumentsCopiesFieldsAndDeletesPlaceholders() {
			BDbmDb.SetStorage(DataStorageType.InDatabase);
			Patient patient=BDbmDb.CreatePatient("B64Db");
			byte[] arrayText=Encoding.UTF8.GetBytes("Synthetic referral letter.");
			byte[] arrayPng=PngBytes();
			Document docText=CreatePlaceholder(patient,Base64Note(".txt",arrayText),"Referral");
			Document docPng=CreatePlaceholder(patient,Base64Note("png",arrayPng),"Intraoral photo");
			Document docBad=CreatePlaceholder(patient,"_rawBase64_:.jpg_this*is*not*base64","Broken");
			Document docDownload=CreatePlaceholder(patient,"_download_:https://example.test/file.jpg","Link");
			Document docPlain=CreatePlaceholder(patient,"An ordinary note","Plain");
			List<long> listDocNums=DatabaseMaintenances.GetDocNumsWithBase64Notes();
			CollectionAssert.IsSubsetOf(new[] { docText.DocNum,docPng.DocNum,docBad.DocNum },listDocNums);
			CollectionAssert.DoesNotContain(listDocNums,docDownload.DocNum);
			CollectionAssert.DoesNotContain(listDocNums,docPlain.DocNum);
			Assert.IsTrue(DatabaseMaintenances.CountDocumentsWithDownloadNotes()>=1);
			int countImageCreateLogs=BDbmDb.GetLogs(EnumPermType.ImageCreate,patient.PatNum).Count;
			Base64DocumentsResult result=DatabaseMaintenances.CreateDocumentsFromBase64Notes();
			Assert.IsTrue(result.CountCreated>=2,result.GetLog());
			Assert.IsTrue(result.ListSkipped.Any(x => x.StartsWith("DocNum "+docBad.DocNum+", PatNum "+patient.PatNum+": ")),result.GetLog());
			StringAssert.Contains(result.GetLog(),"Documents with a download link in the note, left unchanged:");
			//Placeholders that converted are gone; the others are untouched.
			Assert.IsNull(Documents.GetByNum(docText.DocNum,doReturnNullIfNotFound:true));
			Assert.IsNull(Documents.GetByNum(docPng.DocNum,doReturnNullIfNotFound:true));
			Assert.AreEqual(docBad.Note,Documents.GetByNum(docBad.DocNum).Note);
			Assert.AreEqual(docDownload.Note,Documents.GetByNum(docDownload.DocNum).Note);
			Assert.AreEqual("An ordinary note",Documents.GetByNum(docPlain.DocNum).Note);
			List<Document> listDocsNew=Documents.GetPatientData(patient.PatNum).FindAll(x => x.DocNum>docPlain.DocNum);
			Assert.AreEqual(2,listDocsNew.Count);
			Document docTextNew=listDocsNew.Single(x => x.Description=="Referral");
			Assert.IsTrue(docTextNew.FileName.EndsWith(".txt"),docTextNew.FileName);
			CollectionAssert.AreEqual(arrayText,Convert.FromBase64String(docTextNew.RawBase64));
			Assert.AreEqual("",docTextNew.Note);
			Assert.AreEqual(docText.DocCategory,docTextNew.DocCategory);
			Assert.AreEqual(ImageType.Photo,docTextNew.ImgType,"ImgType is copied from the placeholder, as the Images module does.");
			Assert.AreEqual(new DateTime(2024,5,6,7,8,9),docTextNew.DateCreated);
			Assert.AreEqual("3,4",docTextNew.ToothNumbers);
			Assert.IsTrue(docTextNew.PrintHeading);
			Document docPngNew=listDocsNew.Single(x => x.Description=="Intraoral photo");
			Assert.IsTrue(docPngNew.FileName.EndsWith(".png"),docPngNew.FileName);
			CollectionAssert.AreEqual(arrayPng,Convert.FromBase64String(docPngNew.RawBase64));
			List<SecurityLog> listLogs=BDbmDb.GetLogs(EnumPermType.ImageCreate,patient.PatNum);
			Assert.AreEqual(countImageCreateLogs+2,listLogs.Count,"One ImageCreate log entry per document created.");
			CollectionAssert.AreEquivalent(new[] { docTextNew.DocNum,docPngNew.DocNum },listLogs.Skip(countImageCreateLogs).Select(x => x.FKey).ToList());
			//A second run finds only the one that can't be converted.
			Base64DocumentsResult resultAgain=DatabaseMaintenances.CreateDocumentsFromBase64Notes();
			Assert.IsTrue(resultAgain.ListSkipped.Any(x => x.StartsWith("DocNum "+docBad.DocNum+",")));
			Assert.AreEqual(2,Documents.GetPatientData(patient.PatNum).FindAll(x => x.DocNum>docPlain.DocNum).Count);
		}

		[TestMethod]
		public void AtoZ_WritesTheFileAndNeverOverwritesAnExistingOne() {
			_atoZPath=Path.Combine(Path.GetTempPath(),"bdbm_atoz_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_atoZPath);
			BDbmDb.SetStorage(DataStorageType.LocalAtoZ,_atoZPath);
			DatabaseMaintenances.CreateDocumentsFromBase64Notes();//Converts placeholders that other tests left, so the DocNum below is predictable.
			Patient patient=BDbmDb.CreatePatient("B64AtoZ");
			string patFolder=ImageStore.GetPatientFolder(patient,ImageStore.GetPreferredAtoZpath());
			patient=Patients.GetPat(patient.PatNum);//Now has its ImageFolder.
			byte[] arrayBytes=Encoding.UTF8.GetBytes("Synthetic file for the A to Z folder.");
			Document docPlaceholder=CreatePlaceholder(patient,Base64Note(".txt",arrayBytes),"From the API");
			//The next document gets the next DocNum, and its file would be named after it. Put a stray file of that name in the folder first.
			DataTable tableStatus=DataCore.GetTable("SHOW TABLE STATUS LIKE 'document'");
			long docNumNext=long.Parse(tableStatus.Rows[0]["Auto_increment"].ToString());
			string fileNameExpected=Documents.GenerateUniqueFileName(".txt",patient,docNumNext.ToString());
			string pathStray=Path.Combine(patFolder,fileNameExpected);
			File.WriteAllText(pathStray,"A file that is not in the database.");
			Base64DocumentsResult result=DatabaseMaintenances.CreateDocumentsFromBase64Notes();
			Assert.AreEqual(0,result.ListSkipped.Count(x => x.StartsWith("DocNum "+docPlaceholder.DocNum+",")),result.GetLog());
			Assert.IsNull(Documents.GetByNum(docPlaceholder.DocNum,doReturnNullIfNotFound:true));
			Document docNew=Documents.GetPatientData(patient.PatNum).Single();
			Assert.AreEqual(docNumNext,docNew.DocNum,"The prediction of the next DocNum held.");
			Assert.AreEqual(Path.GetFileNameWithoutExtension(fileNameExpected)+"_2.txt",docNew.FileName,"A numbered suffix instead of overwriting.");
			Assert.AreEqual("A file that is not in the database.",File.ReadAllText(pathStray),"The stray file is unchanged.");
			CollectionAssert.AreEqual(arrayBytes,File.ReadAllBytes(Path.Combine(patFolder,docNew.FileName)));
			Assert.AreEqual("",docNew.RawBase64??"");
			Assert.AreEqual("From the API",docNew.Description);
			Assert.AreEqual(1,BDbmDb.GetLogs(EnumPermType.ImageCreate,patient.PatNum).Count);
			Assert.AreEqual(0,Directory.GetFiles(PrefC.GetTempFolderPath(),"base64doc_"+docPlaceholder.DocNum+"_*").Length,"The temporary file is deleted.");
		}

		[TestMethod]
		public void SkipsPlaceholdersOfMissingPatientsAndMounts() {
			BDbmDb.SetStorage(DataStorageType.InDatabase);
			Patient patient=BDbmDb.CreatePatient("B64Skip");
			Document docMount=CreatePlaceholder(patient,Base64Note(".txt",Encoding.UTF8.GetBytes("in a mount")));
			DataCore.NonQ("UPDATE document SET MountItemNum=987654 WHERE DocNum="+docMount.DocNum);
			Document docNoPatient=CreatePlaceholder(patient,Base64Note(".txt",Encoding.UTF8.GetBytes("no patient")));
			DataCore.NonQ("UPDATE document SET PatNum=0 WHERE DocNum="+docNoPatient.DocNum);
			Base64DocumentsResult result=DatabaseMaintenances.CreateDocumentsFromBase64Notes();
			Assert.IsTrue(result.ListSkipped.Any(x => x.StartsWith("DocNum "+docMount.DocNum+",") && x.Contains("mount")),result.GetLog());
			Assert.IsTrue(result.ListSkipped.Any(x => x.StartsWith("DocNum "+docNoPatient.DocNum+",") && x.Contains("patient")),result.GetLog());
			Assert.IsNotNull(Documents.GetByNum(docMount.DocNum,doReturnNullIfNotFound:true));
			Assert.IsNotNull(Documents.GetByNum(docNoPatient.DocNum,doReturnNullIfNotFound:true));
			//Leave nothing convertible behind for the other tests.
			DataCore.NonQ("DELETE FROM document WHERE DocNum IN("+docMount.DocNum+","+docNoPatient.DocNum+")");
		}
	}
}
