// V-DXC2 (Open Dental 26.1.64): a PDF attachment sent the whole way through ClaimConnect.SendAttachmentImages, against a scratch database with a
// synthetic claim, through XConnect and through the partner service. DentalXChange is never called: the HTTP layer of both services is replaced
// by the fakes in FakeDentalXChange.cs. Also reading PDF documents from the patient's images and saving a PDF attachment there.
// Skipped (inconclusive) when no local MariaDB answers at 127.0.0.1:3306 as root with a blank password, or when FORKMIGRATION_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Xml;
using CodeBase;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenPmsDentalBusiness;
using Dea=OpenPmsDentalBusiness.DentalxchangePartnerService;
using OpenPmsDentalBusiness.Eclaims;
using OpenPmsDentalBusiness.FileIO;
using OpenPmsDentalBusiness.ForkMigrations;
using OpenPmsDentalBusiness.VendorDirect.DentalXChange;
using OpenPmsDentalBusiness.WebBridges;
using UnitTestsCore;

namespace VDXC2Tests {
	[TestClass]
	public class PdfSendDbTests {
		private static readonly string DB_NAME=TestDb.ScratchName("vdxc2");
		private static string _skipReason;
		private static IApiRequest _apiRequestOriginal;
		private RecordingApiRequest _recordingApiRequest;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			try {
				TestDb.DropStaleScratchDatabases();
				TestDb.CreateFresh24_3(DB_NAME);
				ForkMigrationRunner.Run();
				Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Defs,InvalidType.Security,InvalidType.ClearHouses,InvalidType.Carriers);
			}
			catch(Exception ex) {
				_skipReason="Scratch database could not be built: "+ex.GetBaseException().Message;
			}
			_apiRequestOriginal=APIRequest.Inst;
			FakeDeaWebRequestCreate.EnsureRegistered();
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			APIRequest.Inst=_apiRequestOriginal??new APIRequest();
			FakeDeaWebRequestCreate.Inst.Reset();
			if(_skipReason!=null || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DB_NAME);
			Security.CurUser=Security.CurUser??new Userod();
			_recordingApiRequest=new RecordingApiRequest();
			APIRequest.Inst=_recordingApiRequest;
			FakeDeaWebRequestCreate.Inst.Reset();
			SetStorage(DataStorageType.InDatabase);
		}

		[TestCleanup]
		public void TestCleanup() {
			APIRequest.Inst=_apiRequestOriginal??new APIRequest();
			FakeDeaWebRequestCreate.Inst.Reset();
			FileAtoZ.LocalAtoZpath=null;
		}

		#region Synthetic claim
		///<summary>A dental claim, waiting to send, for a new synthetic patient, with its own carrier (made-up payer ID) and a ClaimConnect
		///clearinghouse that is the default for dental claims. XConnect is on when withXConnect (the office's API key in LocationID).</summary>
		private static Claim CreateClaim(bool withXConnect) {
			string suffix=Guid.NewGuid().ToString("N").Substring(0,8);
			Clearinghouse clearinghouse=new Clearinghouse();
			clearinghouse.Description="VDXC2 test "+suffix;
			clearinghouse.CommBridge=EclaimsCommBridge.ClaimConnect;
			clearinghouse.Eformat=ElectronicClaimFormat.x837D_5010_dental;
			clearinghouse.LoginID="office-login";
			clearinghouse.Password="office-password";
			clearinghouse.IsAttachmentSendAllowed=true;
			clearinghouse.EnableXConnect=withXConnect;
			clearinghouse.LocationID=withXConnect ? "test-xconnect-key" : "";
			clearinghouse.ExportPath=Path.GetTempPath();
			Clearinghouses.Insert(clearinghouse);
			Clearinghouses.RefreshCache();
			Prefs.UpdateLong(PrefName.ClearinghouseDefaultDent,clearinghouse.ClearinghouseNum);
			Cache.Refresh(InvalidType.Prefs);
			long provNum=ProviderT.CreateProvider("V"+suffix.Substring(0,4),fName:"Synthetic",lName:"Dentist",nationalProvID:"1234567893");
			Patient patient=PatientT.CreatePatient(suffix,lName:"Pdf",fName:"Synthetic",birthDate:new DateTime(1980,1,2));
			Carrier carrier=CarrierT.CreateCarrier("VDXC2 Carrier "+suffix,electID:"ZZ9"+suffix.Substring(0,2));
			InsPlan insPlan=InsPlanT.CreateInsPlan(carrier.CarrierNum);
			InsSub insSub=InsSubT.CreateInsSub(patient.PatNum,insPlan.PlanNum,"SUB"+suffix);
			Claim claim=new Claim();
			claim.PatNum=patient.PatNum;
			claim.PlanNum=insPlan.PlanNum;
			claim.InsSubNum=insSub.InsSubNum;
			claim.ProvBill=provNum;
			claim.ProvTreat=provNum;
			claim.ClaimType="P";
			claim.ClaimStatus="W";
			claim.MedType=EnumClaimMedType.Dental;
			claim.DateService=new DateTime(2026,10,1);
			claim.ClaimIdentifier=patient.PatNum+"/1";
			claim.AttachedFlags="Mail";
			claim.AttachmentID="";
			claim.Narrative="";
			Claims.Insert(claim);
			return Claims.GetClaim(claim.ClaimNum);
		}

		private static ClaimConnect.ImageAttachment CreatePdfAttachment(byte[] arrayBytesPdf) {
			return DxcPdfAttachment.CreateImageAttachment(arrayBytesPdf,"Perio chart",new DateTime(2026,10,2),ClaimConnect.ImageTypeCode.PeriodontalCharts);
		}
		#endregion Synthetic claim

		#region XConnect
		private void AnswerXConnect() {
			_recordingApiRequest.DictResponses["/attachments/create"]=
				"{\"status\":{\"code\":0,\"description\":\"OK\"},\"success\":true,\"response\":{\"dxcAttachmentId\":\"DXC-TEST-PDF-1\"}}";
			_recordingApiRequest.DictResponses["/attachments/images/add"]=
				"{\"status\":{\"code\":0,\"description\":\"OK\"},\"success\":true,\"response\":{\"dxcAttachmentImageId\":4711}}";
			_recordingApiRequest.DictResponses["/attachments/submit"]="{\"status\":{\"code\":0,\"description\":\"OK\"},\"success\":true}";
		}

		[TestMethod]
		public void XConnect_PdfIsSentAsItIs() {
			Claim claim=CreateClaim(withXConnect:true);
			AnswerXConnect();
			byte[] arrayBytesPdf=SyntheticPdf.Build(pageCount:2);
			List<int> listImageReferenceIds=ClaimConnect.SendAttachmentImages(claim,new List<ClaimConnect.ImageAttachment> { CreatePdfAttachment(arrayBytesPdf) },
				"Synthetic narrative");
			CollectionAssert.AreEqual(new List<int> { 4711 },listImageReferenceIds);
			List<ApiCall> listCalls=_recordingApiRequest.ListCalls;
			CollectionAssert.AreEqual(new[] { "/attachments/create","/attachments/images/add","/attachments/submit" },listCalls.Select(x => x.Endpoint).ToArray());
			Assert.IsTrue(listCalls.All(x => x.BaseAddress==new Uri("https://api.dentalxchange.com")),"Every call goes to DentalXChange only.");
			Assert.IsTrue(listCalls.All(x => x.ApiKey=="test-xconnect-key" && x.Username=="office-login"),"The office's own login and key.");
			ApiCall apiCallAdd=listCalls[1];
			Assert.AreEqual(HttpMethod.Put,apiCallAdd.Method);
			CollectionAssert.Contains(apiCallAdd.ListQueryParameters,"dxcAttachmentId=DXC-TEST-PDF-1");
			JObject jObjectAdd=JObject.Parse(apiCallAdd.Body);
			Assert.AreEqual(Convert.ToBase64String(arrayBytesPdf),(string)jObjectAdd["imageFileAsBase64"],"The PDF's own bytes, base64-encoded.");
			Assert.AreEqual("Perio chart.pdf",(string)jObjectAdd["imageFileName"]);
			Assert.AreEqual("P6",(string)jObjectAdd["imageTypeCode"]);
			Assert.AreEqual("2026-10-02",(string)jObjectAdd["imageDate"]);
			//The claim, in memory, as the attachment windows then save it.
			Assert.AreEqual("DXC-TEST-PDF-1",claim.AttachmentID);
			Assert.AreEqual("Synthetic narrative",claim.Narrative);
			Assert.IsTrue(ClaimAttaches.IsDxcAttachmentSubmitted(claim.AttachedFlags),claim.AttachedFlags);
			StringAssert.Contains(claim.AttachedFlags,"Misc");
		}

		[TestMethod]
		public void XConnect_PdfAndImageTogether() {
			Claim claim=CreateClaim(withXConnect:true);
			AnswerXConnect();
			byte[] arrayBytesPdf=SyntheticPdf.Build();
			ClaimConnect.ImageAttachment imageAttachmentImage;
			using(System.Drawing.Bitmap bitmap=new System.Drawing.Bitmap(20,20)) {
				imageAttachmentImage=ClaimConnect.ImageAttachment.Create("Bitewing",new DateTime(2026,10,2),ClaimConnect.ImageTypeCode.XRays,bitmap);
			}
			ClaimConnect.SendAttachmentImages(claim,new List<ClaimConnect.ImageAttachment> { imageAttachmentImage,CreatePdfAttachment(arrayBytesPdf) },"");
			List<ApiCall> listAdds=_recordingApiRequest.ListCalls.FindAll(x => x.Endpoint=="/attachments/images/add");
			Assert.AreEqual(2,listAdds.Count,"One call per file.");
			JObject jObjectImage=JObject.Parse(listAdds[0].Body);
			Assert.AreEqual("Bitewing",(string)jObjectImage["imageFileName"],"Images are sent as before, without an extension.");
			byte[] arrayBytesImage=Convert.FromBase64String((string)jObjectImage["imageFileAsBase64"]);
			Assert.AreEqual(0xFF,arrayBytesImage[0],"The image is still a JPEG.");
			Assert.AreEqual(0xD8,arrayBytesImage[1]);
			JObject jObjectPdf=JObject.Parse(listAdds[1].Body);
			Assert.AreEqual(Convert.ToBase64String(arrayBytesPdf),(string)jObjectPdf["imageFileAsBase64"]);
			Assert.AreEqual("Perio chart.pdf",(string)jObjectPdf["imageFileName"]);
		}

		[TestMethod]
		public void XConnect_EncryptedPdfIsRefusedBeforeAnyCall() {
			Claim claim=CreateClaim(withXConnect:true);
			AnswerXConnect();
			ClaimConnect.ImageAttachment imageAttachment=CreatePdfAttachment(SyntheticPdf.Build());
			imageAttachment.ImageFileAsBase64=SyntheticPdf.BuildEncryptedIndirect();//Swapped after it was checked.
			string attachedFlagsBefore=claim.AttachedFlags;
			try {
				ClaimConnect.SendAttachmentImages(claim,new List<ClaimConnect.ImageAttachment> { imageAttachment },"");
				Assert.Fail("Expected an ODException.");
			}
			catch(ODException ex) {
				StringAssert.Contains(ex.Message,"password-protected");
			}
			Assert.AreEqual(0,_recordingApiRequest.ListCalls.Count,"Nothing was sent.");
			Assert.AreEqual(attachedFlagsBefore,claim.AttachedFlags);
			Assert.AreEqual("",claim.AttachmentID);
		}
		#endregion XConnect

		#region Partner service (SOAP)
		[TestMethod]
		public void PartnerService_PdfIsSentAsItIs() {
			DxcPartnerIdentity.Save("Test Partner","TEST-SERVICE-0001");
			try {
				Claim claim=CreateClaim(withXConnect:false);
				FakeDeaWebRequestCreate fake=FakeDeaWebRequestCreate.Inst;
				fake.IsOn=true;
				fake.DictReturns["openAttachment"]=new Dea.AttachmentReferenceResponse {
					MsgSuccess=true,
					Status=new Dea.Status { code=0,description="OK" },
					AttachmentReference=new Dea.AttachmentReference { AttachmentID="DXC-TEST-PDF-2" },
				};
				fake.DictReturns["addImage"]=new Dea.ImageReferenceResponses {
					MsgSuccess=true,
					Status=new Dea.Status { code=0,description="OK" },
					ImageReferenceResponses1=new Dea.ImageReferenceResponse[] {
						new Dea.ImageReferenceResponse { MsgSuccess=true,Status=new Dea.Status { code=0 },
							ImageReference=new Dea.ImageReference { ImageReferenceId=8080,ImageReferenceIdSpecified=true } },
					},
				};
				fake.DictReturns["submitAttachment"]=new Dea.DeaResponse { MsgSuccess=true,Status=new Dea.Status { code=0,description="OK" } };
				byte[] arrayBytesPdf=SyntheticPdf.Build(pageCount:3);
				List<int> listImageReferenceIds=ClaimConnect.SendAttachmentImages(claim,
					new List<ClaimConnect.ImageAttachment> { CreatePdfAttachment(arrayBytesPdf) },"Synthetic narrative");
				CollectionAssert.AreEqual(new List<int> { 8080 },listImageReferenceIds);
				CollectionAssert.AreEqual(new[] { "openAttachment","addImage","submitAttachment" },fake.ListCalls.Select(x => x.Operation).ToArray());
				Assert.IsTrue(fake.ListCalls.All(x => x.Uri.Host.EndsWith(".dentalxchange.com")),"Every call goes to DentalXChange only.");
				Assert.AreEqual(0,_recordingApiRequest.ListCalls.Count,"XConnect is off for this clearinghouse.");
				XmlDocument xmlDocumentAdd=fake.ListCalls[1].XmlDocumentRequest;
				XmlNode xmlNodeImage=xmlDocumentAdd.GetElementsByTagName("attachmentImage")[0];
				Assert.IsNotNull(xmlNodeImage,xmlDocumentAdd.OuterXml);
				Assert.AreEqual(Convert.ToBase64String(arrayBytesPdf),Child(xmlNodeImage,"ImageFileAsBase64"),"The PDF's own bytes, base64-encoded.");
				Assert.AreEqual("Perio chart.pdf",Child(xmlNodeImage,"ImageFileName"));
				Assert.AreEqual("P6",Child(xmlNodeImage,"ImageTypeCode"));
				Assert.IsNull(Child(xmlNodeImage,"ImageOrientationType"),"Orientation is only sent for X-rays.");
				//The office's partner identity and login travel in the credentials, as for images.
				XmlNode xmlNodeCredentials=xmlDocumentAdd.GetElementsByTagName("Credentials")[0];
				Assert.AreEqual("Test Partner",Child(xmlNodeCredentials,"Client"));
				Assert.AreEqual("office-login",Child(xmlNodeCredentials,"Username"));
				Assert.AreEqual("DXC-TEST-PDF-2",claim.AttachmentID);
				Assert.IsTrue(ClaimAttaches.IsDxcAttachmentSubmitted(claim.AttachedFlags),claim.AttachedFlags);
			}
			finally {
				DxcPartnerIdentity.Save("","");
			}
		}

		private static string Child(XmlNode xmlNode,string localName) {
			XmlNode xmlNodeChild=xmlNode.ChildNodes.Cast<XmlNode>().FirstOrDefault(x => x.LocalName==localName);
			return xmlNodeChild?.InnerText;
		}
		#endregion Partner service (SOAP)

		#region The patient's images
		private static void SetStorage(DataStorageType dataStorageType) {
			Prefs.UpdateInt(PrefName.AtoZfolderUsed,(int)dataStorageType);
			Cache.Refresh(InvalidType.Prefs);
		}

		[TestMethod]
		public void ReadPdfDocument_FromTheDatabase() {
			SetStorage(DataStorageType.InDatabase);
			byte[] arrayBytesPdf=SyntheticPdf.Build(pageCount:2);
			Document document=new Document { FileName="perio.PDF",RawBase64=Convert.ToBase64String(arrayBytesPdf) };
			CollectionAssert.AreEqual(arrayBytesPdf,DxcPdfAttachment.ReadPdfDocument(document,""));
			Document documentLocked=new Document { FileName="locked.pdf",RawBase64=Convert.ToBase64String(SyntheticPdf.BuildEncryptedDirect()) };
			try {
				DxcPdfAttachment.ReadPdfDocument(documentLocked,"");
				Assert.Fail("Expected an ODException.");
			}
			catch(ODException ex) {
				StringAssert.Contains(ex.Message,"encrypted");
			}
			Document documentNoFile=new Document { FileName="gone.pdf",RawBase64="" };
			try {
				DxcPdfAttachment.ReadPdfDocument(documentNoFile,"");
				Assert.Fail("Expected an ODException.");
			}
			catch(ODException ex) {
				StringAssert.Contains(ex.Message,"could not be found");
			}
		}

		[TestMethod]
		public void ReadPdfDocument_FromTheAtoZFolder() {
			string atoZPath=Path.Combine(Path.GetTempPath(),"vdxc2_atoz_"+Guid.NewGuid().ToString("N"));
			string patFolder=Path.Combine(atoZPath,"P","PdfSynthetic1");
			Directory.CreateDirectory(patFolder);
			try {
				SetStorage(DataStorageType.LocalAtoZ);
				FileAtoZ.LocalAtoZpath=atoZPath;
				byte[] arrayBytesPdf=SyntheticPdf.Build();
				File.WriteAllBytes(Path.Combine(patFolder,"chart.pdf"),arrayBytesPdf);
				CollectionAssert.AreEqual(arrayBytesPdf,DxcPdfAttachment.ReadPdfDocument(new Document { FileName="chart.pdf" },patFolder));
			}
			finally {
				SetStorage(DataStorageType.InDatabase);
				Directory.Delete(atoZPath,true);
			}
		}

		[TestMethod]
		public void SavedAttachment_IsStoredAsAPdf() {
			//What the attachment windows do when "Save DXC attachments" is on and the PDF came from disk.
			SetStorage(DataStorageType.InDatabase);
			Patient patient=PatientT.CreatePatient("vdxc2save",lName:"Pdf",fName:"Synthetic");
			long defNum=Defs.GetDefsForCategory(DefCat.ImageCats,true).First().DefNum;
			byte[] arrayBytesPdf=SyntheticPdf.Build();
			Document document=ImageStore.Import(arrayBytesPdf,defNum,ImageType.Document,patient,fileExtension:DxcPdfAttachment.PdfExtension);
			Document documentDb=Documents.GetByNum(document.DocNum);
			Assert.IsTrue(DxcPdfAttachment.IsPdfFileName(documentDb.FileName),documentDb.FileName);
			CollectionAssert.AreEqual(arrayBytesPdf,Convert.FromBase64String(documentDb.RawBase64));
			//And it can be sent again later from the patient's images.
			CollectionAssert.AreEqual(arrayBytesPdf,DxcPdfAttachment.ReadPdfDocument(documentDb,""));
		}
		#endregion The patient's images
	}
}
