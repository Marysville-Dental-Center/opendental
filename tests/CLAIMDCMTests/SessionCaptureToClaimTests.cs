// M16 integration (CLAIM-DCM with CAP-16): a 16-bit X-ray, and a 14-bit one in a 16-bit file, saved the way the capture session saves them
// (ImagingCapture's RadiographImport and RadiographSavePlanner, which write the file with DicomIntraOralWriter and store its window on the
// 0-255 scale) go onto a claim through every claim path: Edit Claim's copy (ClaimAttachDicom.BuildClaimAttachFile), DentalXChange
// (ClaimConnect.ImageAttachment.Create) and EDS (EDS.ImageAttachment.Create). Each gets an 8-bit JPEG of the right size whose values are what
// the Imaging module shows for the stored window; the DICOM file and the document are never changed and never sent. The store behind
// RadiographImport is an in-memory fake that writes the file into a temporary A to Z patient folder: no database and no network. All images
// and values are made up.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using ImagingCapture.Save;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.Eclaims;

namespace CLAIMDCMTests {
	[TestClass]
	public class SessionCaptureToClaimTests {
		///<summary>Three bands of 32 x 32 pixels, so every JPEG block is flat and a JPEG copy keeps each band's value.</summary>
		private const int Width=96;
		private const int Height=32;
		private const int BandWidth=32;
		private string _patFolder;

		///<summary>The fake behind RadiographImport: ImageStore.Import writes the file into the patient folder and returns its document; the
		///other calls only record. No database.</summary>
		private sealed class FolderRadiographStore : IRadiographStore {
			private readonly string _patFolder;
			private long _docNumNext=1;
			public List<Document> ListDocumentsUpdated=new List<Document>();
			public List<string> ListLogTexts=new List<string>();
			public int CountPearlSends;

			public FolderRadiographStore(string patFolder) {
				_patFolder=patFolder;
			}

			public bool IsCategoryValid(long defNum) {
				return defNum==5;
			}

			public Document Import(byte[] bytes,long docCategory,ImageType imageType,Patient patient,string mimeType,string fileExtension) {
				Document document=new Document {
					DocNum=_docNumNext++,
					PatNum=patient.PatNum,
					DocCategory=docCategory,
					ImgType=imageType,
					FileName="capture"+_docNumNext.ToString()+fileExtension,
					DateTStamp=new DateTime(2026,11,1,9,31,0),
				};
				File.WriteAllBytes(Path.Combine(_patFolder,document.FileName),bytes);
				return document;
			}

			public void InsertMountItem(MountItem mountItem) {
			}

			public void UpdateDocument(Document document,Document documentOld) {
				ListDocumentsUpdated.Add(document.Copy());
			}

			public void DeleteDocument(Document document,Patient patient) {
				throw new InvalidOperationException("No save in these tests is undone.");
			}

			public void DeleteMountItem(MountItem mountItem) {
				throw new InvalidOperationException("No save in these tests is undone.");
			}

			public void UpdateMount(Mount mount) {
			}

			public void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey) {
				ListLogTexts.Add(logText);
			}

			public void SendToPearlIfConfigured(Patient patient,Document document,byte[] pngBytes,MountItem mountItem) {
				CountPearlSends++;
			}
		}

		[TestInitialize]
		public void SetUp() {
			TestSupport.UseLocalAtoZ();
			_patFolder=TestSupport.NewFolder();
		}

		[TestCleanup]
		public void TearDown() {
			TestSupport.DeleteFolder(_patFolder);
		}

		///<summary>A capture as TwainCaptureSession raises it from a memory enable at bits (16, or 14 right-justified in 16-bit containers), with
		///three bands of raw values left to right.</summary>
		private static AcquiredImage Capture(int bits,ushort rawLeft,ushort rawMiddle,ushort rawRight) {
			ushort[] arrayPixels=new ushort[Width*Height];
			for(int y=0;y<Height;y++) {
				for(int x=0;x<Width;x++) {
					arrayPixels[y*Width+x]=x<BandWidth ? rawLeft : (x<2*BandWidth ? rawMiddle : rawRight);
				}
			}
			return new AcquiredImage {
				Width=Width,Height=Height,BitsPerPixel=16,BitsPerSample=bits,PixelType=AcquiredImage.PixelTypeGray,
				PixelFlavor=AcquiredImage.PixelFlavorChocolate,XDpi=1270,YDpi=1270,Gray16=arrayPixels,
				MetadataJson=@"{""schema"":""capture-metadata/1"",""manufacturer"":""Example"",""serial"":""SN-1"","
					+@"""acquiredAt"":""2026-11-01T09:30:00.000-08:00"",""effectiveBits"":null,""redelivered"":false}",
				ReceivedAt=new DateTime(2026,11,1,9,30,1),EnableId=1,Kind=AcquiredImageKind.Normal,RequestedBits=16,BitDepthInEffect=bits,
				ByteOrder16=TwainByteOrder.LE,TransferMechanism=TwainTransferMechanism.Memory,SourceName="Example Sensor DS",IndexInEnable=1,
			};
		}

		///<summary>Saves the capture into slot 1 of a mount, as the session does once every 16-bit gate holds (Is16BitAllowed).</summary>
		private static RadiographSaveResult SaveAsTheSessionDoes(FolderRadiographStore store,AcquiredImage image) {
			Mount mount=new Mount { MountNum=7,PatNum=1,ProvNum=2,DateCreated=new DateTime(2026,11,1,9,0,0),FlipOnAcquire=false };
			MountItem mountItem=new MountItem { MountItemNum=70,MountNum=7,ItemOrder=1,ToothNumbers="3,30",RotateOnAcquire=0 };
			RadiographSaveRequest request=new RadiographSaveRequest {
				Image=image,
				Patient=new Patient { PatNum=1 },
				DocCategory=5,
				ProvNum=2,
				Target=new RadiographTarget { Mount=mount,MountItem=mountItem },
				Is16BitAllowed=true,
				Polarity=TwainPolarity.AsDelivered,
				SessionGuid=new Guid("6a1f0c2e-3b5d-4f7a-9c8e-1d2b3a4c5e6f"),
				DeviceDescription="Example sensor",
				ImagingDeviceNum=1,
			};
			RadiographSaveResult result=new RadiographImport(store,() => new DateTime(2026,11,1,9,31,0)).Save(request);
			Assert.IsTrue(result.IsSaved,result.Error);
			return result;
		}

		///<summary>What the Imaging module shows for raw under the stored 0-255 window of a BitsStored 16 file: raw/256 through the window
		///(BitmapDicom.ApplyWindowing).</summary>
		private static int Displayed16(int raw,int windowingMin,int windowingMax) {
			double value=raw/256.0;
			if(value<windowingMin) {
				return 0;
			}
			if(value>windowingMax) {
				return 255;
			}
			return (int)Math.Round(255.0*(value-windowingMin)/(windowingMax-windowingMin));
		}

		///<summary>Each band, away from the edges between them, within 2 of the value (a JPEG of flat blocks), and the bands clearly apart.</summary>
		private static void AssertBands(Bitmap bitmap,int[] arrayExpected,string message) {
			Assert.AreEqual(Width,bitmap.Width,message+": width");
			Assert.AreEqual(Height,bitmap.Height,message+": height");
			for(int band=0;band<3;band++) {
				Rectangle rectangle=new Rectangle(band*BandWidth+4,0,BandWidth-8,Height);
				TestSupport.AssertAreaNear(bitmap,rectangle,arrayExpected[band],message+" band "+band);
			}
			Assert.IsTrue(arrayExpected[0]+60<arrayExpected[1] && arrayExpected[1]+60<arrayExpected[2],message+": the bands stay apart");
		}

		private void AssertGoesOntoEveryClaimPath(RadiographSaveResult result,int[] arrayExpected,string label) {
			Document document=result.Document;
			string path=Path.Combine(_patFolder,document.FileName);
			string hashBefore=TestSupport.Hash(path);
			string settingsBefore=TestSupport.Settings(document);
			//Edit Claim (FormImageSelectClaimAttach.SaveAttachmentDicom): the JPEG copy, named .jpg.
			ClaimAttachDicomFile claimAttachDicomFile=ClaimAttachDicom.BuildClaimAttachFile(document,_patFolder);
			Assert.AreEqual(Path.ChangeExtension(document.FileName,".jpg"),claimAttachDicomFile.DisplayedFileName,label);
			using(Bitmap bitmap=TestSupport.DecodeJpeg(claimAttachDicomFile.FileBytes)) {
				AssertBands(bitmap,arrayExpected,label+" Edit Claim");
			}
			//DentalXChange (FormImagePickerDXC): the document with its drawings (none), sent as a JPEG.
			ClaimConnect.ImageAttachment imageAttachmentDxc;
			using(Bitmap bitmap=ClaimAttachDicom.Render(document,ClaimAttachDicom.ReadDocument(document,_patFolder),new List<ImageDraw>())) {
				imageAttachmentDxc=ClaimConnect.ImageAttachment.Create("Periapical",document.DateCreated,ClaimConnect.ImageTypeCode.XRays,bitmap);
			}
			using(Bitmap bitmap=TestSupport.DecodeJpeg(imageAttachmentDxc.ImageFileAsBase64)) {
				AssertBands(bitmap,arrayExpected,label+" DentalXChange");
			}
			ClaimAttachDicom.ThrowIfAnyIsDicom(new List<ClaimConnect.ImageAttachment> { imageAttachmentDxc });//does not throw
			//EDS (FormClaimAttachment): the .dcm chosen in the patient's folder is that document, so its stored window is used.
			Document documentFound=ClaimAttachDicom.FindDocumentForFile(new[] { document },_patFolder,path);
			Assert.AreSame(document,documentFound,label);
			EDS.ImageAttachment imageAttachmentEds;
			using(Bitmap bitmap=ClaimAttachDicom.Render(documentFound,ClaimAttachDicom.ReadFile(path))) {
				imageAttachmentEds=EDS.ImageAttachment.Create("Periapical",document.DateCreated,EDS.EnumDocumentTypeCode.XRay,bitmap,"",isRightOriented:true);
			}
			Assert.AreEqual(".jpg",imageAttachmentEds.FileExt,label);
			using(Bitmap bitmap=TestSupport.DecodeJpeg(imageAttachmentEds.FileData)) {
				AssertBands(bitmap,arrayExpected,label+" EDS");
			}
			ClaimAttachDicom.ThrowIfAnyIsDicom(new List<EDS.ImageAttachment> { imageAttachmentEds });//does not throw
			//The saved X-ray itself is a DICOM file that every send refuses, and nothing above changed it or its document.
			Assert.IsTrue(ClaimAttachDicom.IsDicomBytes(File.ReadAllBytes(path)),label+": the session saved a DICOM file");
			Assert.AreEqual(hashBefore,TestSupport.Hash(path),label+": the DICOM file is not changed.");
			Assert.AreEqual(settingsBefore,TestSupport.Settings(document),label+": the document is not changed.");
		}

		[TestMethod]
		public void SessionSaved16Bit_GoesOntoEveryClaimPath_AsAnEightBitJpeg() {
			FolderRadiographStore store=new FolderRadiographStore(_patFolder);
			RadiographSaveResult result=SaveAsTheSessionDoes(store,Capture(TwainBitDepth.Bits16,8000,24000,40000));
			Assert.AreEqual(RadiographFormat.Dicom16,result.Plan.Format);
			Assert.AreEqual(16,result.Plan.DicomResult.BitsStored);
			Assert.AreEqual(".dcm",Path.GetExtension(result.Document.FileName));
			//The session's window is the 0.5/99.5 percentile window of the raw values: 8000 to 40000, stored as 31 to 157 on the 0-255 scale.
			Assert.AreEqual(8000,result.Plan.DicomResult.WindowLow);
			Assert.AreEqual(40000,result.Plan.DicomResult.WindowHigh);
			Assert.AreEqual(31,result.Document.WindowingMin);
			Assert.AreEqual(157,result.Document.WindowingMax);
			int[] arrayExpected=new[] { 8000,24000,40000 }.Select(x => Displayed16(x,result.Document.WindowingMin,result.Document.WindowingMax)).ToArray();
			CollectionAssert.AreEqual(new[] { 1,127,253 },arrayExpected,"the Imaging module's view of the stored window");
			//What the session showed (Gray16Samples.ToPreview through the same window) is 0, 128 and 255; the claim copy is within 2 of it.
			byte[] arrayPreview=Gray16Samples.ToPreview(new ushort[] { 8000,24000,40000 },result.Plan.DicomResult.WindowLow,result.Plan.DicomResult.WindowHigh);
			for(int i=0;i<3;i++) {
				Assert.IsTrue(Math.Abs(arrayPreview[i]-arrayExpected[i])<=2,"band "+i+": the claim copy shows what the session showed");
			}
			AssertGoesOntoEveryClaimPath(result,arrayExpected,"16-bit");
			Assert.AreEqual(0,store.CountPearlSends);
		}

		[TestMethod]
		public void SessionSaved14BitInA16BitFile_GoesOntoEveryClaimPath_AsAnEightBitJpeg() {
			FolderRadiographStore store=new FolderRadiographStore(_patFolder);
			RadiographSaveResult result=SaveAsTheSessionDoes(store,Capture(TwainBitDepth.Bits14,2000,6000,10000));
			Assert.AreEqual(RadiographFormat.Dicom16,result.Plan.Format);
			//CAP-16: 14-bit data is stored with BitsStored 16, unchanged, with effectiveBits=14 in Image Comments.
			Assert.AreEqual(16,result.Plan.DicomResult.BitsStored);
			StringAssert.Contains(Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(_patFolder,result.Document.FileName))),"effectiveBits=14");
			Assert.AreEqual(2000,result.Plan.DicomResult.WindowLow);
			Assert.AreEqual(10000,result.Plan.DicomResult.WindowHigh);
			Assert.AreEqual(7,result.Document.WindowingMin);
			Assert.AreEqual(40,result.Document.WindowingMax);
			//The 0-255 window of a BitsStored 16 file is coarse for 14-bit data (design 7.8, BitsStored rule), so the claim copy follows the
			//stored window, as the Imaging module does: 6, 127 and 248.
			int[] arrayExpected=new[] { 2000,6000,10000 }.Select(x => Displayed16(x,result.Document.WindowingMin,result.Document.WindowingMax)).ToArray();
			CollectionAssert.AreEqual(new[] { 6,127,248 },arrayExpected,"the Imaging module's view of the stored window");
			AssertGoesOntoEveryClaimPath(result,arrayExpected,"14-bit");
		}
	}
}
