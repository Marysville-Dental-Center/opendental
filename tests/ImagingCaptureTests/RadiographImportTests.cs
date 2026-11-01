// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using ImagingCapture.Metadata;
using ImagingCapture.Save;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace ImagingCapture.Tests {
	///<summary>RadiographImport and RadiographSavePlanner (design 7.8, 7.9, 7.10, 7.12, 9.1, 10.8) against a stand-in for OpenDentBusiness:
	///the order of the calls, the document fields, the 8-bit security-log entry, and the undo of a partial save. No database is used, so
	///ImageStore, Documents, MountItems, Mounts and SecurityLogs themselves are not exercised here.</summary>
	[TestClass]
	public class RadiographImportTests {
		private FakeRadiographStore _store;
		private RadiographImport _radiographImport;
		private static readonly DateTime _dateTimeNow=new DateTime(2026,10,31,10,30,0);

		[TestInitialize]
		public void Init() {
			_store=new FakeRadiographStore();
			_radiographImport=new RadiographImport(_store,() => _dateTimeNow);
		}

		private static Patient Pat() {
			return new Patient { PatNum=55,LName="Test",FName="Pat",PriProv=3 };
		}

		private static Mount Mount(bool flipOnAcquire=false,long provNum=0) {
			return new Mount { MountNum=7,PatNum=55,DocCategory=20,FlipOnAcquire=flipOnAcquire,ProvNum=provNum,DateCreated=new DateTime(2026,10,31,10,0,0) };
		}

		private static MountItem Slot(long mountItemNum=101,int itemOrder=1,string toothNumbers="8,9",int rotateOnAcquire=0) {
			return new MountItem { MountItemNum=mountItemNum,MountNum=7,ItemOrder=itemOrder,ToothNumbers=toothNumbers,RotateOnAcquire=rotateOnAcquire,
				Width=200,Height=300 };
		}

		private static RadiographSaveRequest Request(AcquiredImage image,RadiographTarget target) {
			return new RadiographSaveRequest {
				Image=image,Patient=Pat(),DocCategory=20,ProvNum=4,Target=target,SessionGuid=new Guid("11111111-2222-3333-4444-555555555555"),
				DeviceDescription="Intraoral sensor",ImagingDeviceNum=12,
			};
		}

		private static AcquiredImage Image8(string json=TestImages.Json8Bit) {
			return TestImages.Image8(6,4,(x,y) => (byte)(x*40+y),json);
		}

		#region Png8
		[TestMethod]
		public void Png8_IntoSlot_FieldsPlacementAndLog() {
			Mount mount=Mount(flipOnAcquire:true,provNum:0);
			RadiographSaveResult result=_radiographImport.Save(Request(Image8(),new RadiographTarget { Mount=mount,MountItem=Slot(rotateOnAcquire:90) }));
			Assert.IsTrue(result.IsSaved,result.Error);
			Assert.AreEqual(RadiographFormat.Png8,result.Plan.Format);
			CollectionAssert.AreEqual(new[] { "IsCategoryValid","Import","UpdateDocument","MakeLogEntry","UpdateMount","SendToPearl" },_store.ListCalls);
			Assert.AreEqual(".png",_store.FileExtension);
			Assert.AreEqual("image/png",_store.MimeType);
			Assert.AreEqual(ImageType.Radiograph,_store.ImageType);
			Assert.AreEqual(55,_store.PatientImported.PatNum);
			Document document=_store.DocumentUpdated;
			Assert.AreSame(result.Document,document);
			Assert.AreEqual(101,document.MountItemNum);
			Assert.AreEqual(90f,document.DegreesRotated);
			Assert.IsTrue(document.IsFlipped);
			Assert.AreEqual("8,9",document.ToothNumbers);
			Assert.AreEqual(4,document.ProvNum);
			Assert.IsTrue(document.PrintHeading);
			Assert.AreEqual(0,document.WindowingMin);
			Assert.AreEqual(0,document.WindowingMax);
			Assert.AreEqual(ImageType.Radiograph,document.ImgType);
			Assert.AreEqual("",document.Description,"Empty in a slot (design 9.1).");
			Assert.AreEqual(DateTimeOffset.Parse("2026-10-31T09:15:30.123+00:00").LocalDateTime,document.DateCreated,"acquiredAt from the metadata.");
			Assert.AreEqual(0,_store.DocumentOld.MountItemNum,"Documents.Update gets the row as Import left it.");
			//The 8-bit capture entry (design 7.9).
			Assert.AreEqual(EnumPermType.ImageCreate,_store.LogPermType);
			Assert.AreEqual(55,_store.LogPatNum);
			Assert.AreEqual(document.DocNum,_store.LogFKey);
			StringAssert.StartsWith(_store.LogText,"Capture: ");
			StringAssert.Contains(_store.LogText,"serial \"SN-000123\"");
			StringAssert.Contains(_store.LogText,"model \"M2\"");
			StringAssert.Contains(_store.LogText,"captureId \"c-7f3a91\"");
			StringAssert.Contains(_store.LogText,"rendering=vendor8");
			StringAssert.Contains(_store.LogText,"configured, not measured");
			StringAssert.Contains(_store.LogText,"target Slot 1 (teeth 8,9)");
			StringAssert.Contains(_store.LogText,"session 11111111-2222-3333-4444-555555555555");
			//Mount.ProvNum (design 10.8) and the Pearl gate.
			Assert.AreEqual(4,_store.MountUpdated.ProvNum);
			Assert.AreSame(result.MountItem,_store.MountItemPearl);
			Assert.IsFalse(result.IsMountItemCreated);
		}

		[TestMethod]
		public void Png8_FileIsLosslessGreyscalePng() {
			AcquiredImage image=Image8();
			RadiographSaveResult result=_radiographImport.Save(Request(image,new RadiographTarget { Mount=Mount(),MountItem=Slot() }));
			Assert.IsTrue(result.IsSaved,result.Error);
			byte[] bytes=_store.BytesImported;
			Assert.AreEqual(0x89,bytes[0]);
			Assert.AreEqual("PNG",Encoding.ASCII.GetString(bytes,1,3));
			Assert.AreEqual("IHDR",Encoding.ASCII.GetString(bytes,12,4));
			Assert.AreEqual(8,bytes[24],"Bit depth 8.");
			Assert.AreEqual(0,bytes[25],"Color type 0: greyscale.");
			//The existing loaders (GDI+) read it with the same values.
			using(MemoryStream memoryStream=new MemoryStream(bytes))
			using(Bitmap bitmap=new Bitmap(memoryStream)) {
				Assert.AreEqual(6,bitmap.Width);
				Assert.AreEqual(4,bitmap.Height);
				for(int y=0;y<4;y++) {
					for(int x=0;x<6;x++) {
						Color color=bitmap.GetPixel(x,y);
						Assert.AreEqual(x*40+y,color.R,"x="+x+" y="+y);
						Assert.AreEqual(color.R,color.G);
						Assert.AreEqual(color.R,color.B);
					}
				}
				Assert.AreEqual(1270,bitmap.HorizontalResolution,1);
			}
		}

		[TestMethod]
		public void Png8_MountFull_NewUnmountedItem() {
			RadiographSaveResult result=_radiographImport.Save(Request(Image8(),new RadiographTarget { Mount=Mount(),MountItem=null }));
			Assert.IsTrue(result.IsSaved,result.Error);
			Assert.IsTrue(result.IsMountItemCreated);
			CollectionAssert.AreEqual(new[] { "IsCategoryValid","Import","InsertMountItem","UpdateDocument","MakeLogEntry","UpdateMount","SendToPearl" },
				_store.ListCalls,"Import first, then the item, as PlaceAcquiredBitmapInUI does.");
			MountItem mountItem=_store.MountItemInserted;
			Assert.AreEqual(-1,mountItem.ItemOrder);
			Assert.AreEqual(100,mountItem.Width);
			Assert.AreEqual(7,mountItem.MountNum);
			Assert.AreEqual(mountItem.MountItemNum,_store.DocumentUpdated.MountItemNum);
			Assert.AreEqual("",_store.DocumentUpdated.ToothNumbers);
			StringAssert.Contains(_store.LogText,"target Unmounted");
		}

		[TestMethod]
		public void Png8_Single_ToothPickerNumbers() {
			RadiographSaveResult result=_radiographImport.Save(Request(Image8(),new RadiographTarget { ToothNumbers=" 30 " }));
			Assert.IsTrue(result.IsSaved,result.Error);
			CollectionAssert.AreEqual(new[] { "IsCategoryValid","Import","UpdateDocument","MakeLogEntry","SendToPearl" },_store.ListCalls);
			Assert.AreEqual("30",_store.DocumentUpdated.ToothNumbers);
			Assert.AreEqual(0,_store.DocumentUpdated.MountItemNum);
			Assert.AreEqual(0f,_store.DocumentUpdated.DegreesRotated);
			Assert.IsFalse(_store.DocumentUpdated.IsFlipped);
			Assert.IsNull(_store.MountItemPearl);
		}

		[TestMethod]
		public void Png8_NoMetadata_ReceiveTimeAndPlainLog() {
			AcquiredImage image=Image8(json:null);
			RadiographSaveResult result=_radiographImport.Save(Request(image,new RadiographTarget { Mount=Mount(provNum:4),MountItem=Slot() }));
			Assert.IsTrue(result.IsSaved,result.Error);
			Assert.AreEqual(image.ReceivedAt,_store.DocumentUpdated.DateCreated);
			StringAssert.Contains(_store.LogText,"no capture metadata");
			StringAssert.Contains(_store.LogText,"source \"Example Sensor DS\"");
			Assert.IsNull(_store.MountUpdated,"The mount already has the provider.");
		}

		[TestMethod]
		public void Png8_OrientationAdjustment_Warns() {
			RadiographSaveResult result=_radiographImport.Save(Request(Image8(TestImages.Json8BitRotated),
				new RadiographTarget { Mount=Mount(),MountItem=Slot() }));
			Assert.IsTrue(result.IsSaved,result.Error);
			CollectionAssert.Contains(result.ListWarnings,RadiographSavePlanner.OrientationAdjustmentWarning);
		}

		[TestMethod]
		public void RgbImage_IsPhotoWithoutPrintHeading() {
			AcquiredImage image=Image8();
			image.Dib=TestImages.Dib24(3,2,(x,y) => new byte[] { 10,(byte)(20*x),(byte)(30*y) });
			image.PixelType=AcquiredImage.PixelTypeRgb;
			RadiographSaveResult result=_radiographImport.Save(Request(image,new RadiographTarget { Mount=Mount(),MountItem=Slot() }));
			Assert.IsTrue(result.IsSaved,result.Error);
			Assert.AreEqual(ImageType.Photo,_store.ImageType);
			Assert.IsFalse(_store.DocumentUpdated.PrintHeading);
			Assert.AreEqual(2,_store.BytesImported[25],"Color type 2: RGB.");
		}
		#endregion Png8

		#region Refusals and undo
		[TestMethod]
		public void InvalidCategory_RefusedBeforeWriting() {
			_store.IsCategoryValidResult=false;
			RadiographSaveResult result=_radiographImport.Save(Request(Image8(),new RadiographTarget { Mount=Mount(),MountItem=Slot() }));
			Assert.IsFalse(result.IsSaved);
			Assert.AreEqual("Image category missing; choose a category.",result.Error);
			CollectionAssert.AreEqual(new[] { "IsCategoryValid" },_store.ListCalls);
			RadiographSaveRequest request=Request(Image8(),new RadiographTarget());
			request.DocCategory=0;
			Assert.IsFalse(_radiographImport.Save(request).IsSaved);
		}

		[TestMethod]
		public void NoPatient_Refused() {
			RadiographSaveRequest request=Request(Image8(),new RadiographTarget());
			request.Patient=null;
			RadiographSaveResult result=_radiographImport.Save(request);
			Assert.IsFalse(result.IsSaved);
			Assert.AreEqual(0,_store.ListCalls.Count);
		}

		[TestMethod]
		public void ImportFails_NothingToUndo() {
			_store.ThrowOn="Import";
			RadiographSaveResult result=_radiographImport.Save(Request(Image8(),new RadiographTarget { Mount=Mount(),MountItem=Slot() }));
			Assert.IsFalse(result.IsSaved);
			Assert.AreEqual("Import failed",result.Error);
			Assert.IsFalse(result.IsRowDeleted);
			CollectionAssert.AreEqual(new[] { "IsCategoryValid","Import" },_store.ListCalls);
		}

		[TestMethod]
		public void UpdateFails_RowAndNewItemDeleted() {
			_store.ThrowOn="UpdateDocument";
			RadiographSaveResult result=_radiographImport.Save(Request(Image8(),new RadiographTarget { Mount=Mount(),MountItem=null }));
			Assert.IsFalse(result.IsSaved);
			Assert.IsTrue(result.IsRowDeleted);
			Assert.AreEqual("UpdateDocument failed",result.Error);
			CollectionAssert.AreEqual(new[] { "IsCategoryValid","Import","InsertMountItem","UpdateDocument","DeleteDocument","DeleteMountItem" },
				_store.ListCalls);
			Assert.AreSame(_store.DocumentImported,_store.DocumentDeleted);
			Assert.AreSame(_store.MountItemInserted,_store.MountItemDeleted);
			Assert.IsNull(result.Document);
			Assert.IsFalse(result.IsMountItemCreated);
		}

		[TestMethod]
		public void InsertItemFails_RowDeleted() {
			_store.ThrowOn="InsertMountItem";
			RadiographSaveResult result=_radiographImport.Save(Request(Image8(),new RadiographTarget { Mount=Mount(),MountItem=null }));
			Assert.IsFalse(result.IsSaved);
			Assert.IsTrue(result.IsRowDeleted);
			CollectionAssert.AreEqual(new[] { "IsCategoryValid","Import","InsertMountItem","DeleteDocument" },_store.ListCalls);
		}

		[TestMethod]
		public void UndoFails_ErrorNamesTheDocument() {
			_store.ThrowOn="UpdateDocument|DeleteDocument";
			RadiographSaveResult result=_radiographImport.Save(Request(Image8(),new RadiographTarget { Mount=Mount(),MountItem=Slot() }));
			Assert.IsFalse(result.IsSaved);
			Assert.IsFalse(result.IsRowDeleted);
			StringAssert.Contains(result.Error,"DocNum "+_store.DocumentImported.DocNum);
		}

		[TestMethod]
		public void LogAndMountAndPearlFailures_DoNotUndoTheSave() {
			_store.ThrowOn="MakeLogEntry|UpdateMount|SendToPearl";
			Mount mount=Mount(provNum:2);
			RadiographSaveResult result=_radiographImport.Save(Request(Image8(),new RadiographTarget { Mount=mount,MountItem=Slot() }));
			Assert.IsTrue(result.IsSaved,result.Error);
			Assert.AreEqual(3,result.ListWarnings.Count,string.Join("\r\n",result.ListWarnings));
			Assert.AreEqual(2,mount.ProvNum,"The mount keeps its provider when the update failed.");
			Assert.IsFalse(_store.ListCalls.Contains("DeleteDocument"));
		}
		#endregion Refusals and undo

		#region Dicom16
		private static AcquiredImage Image16(string json=TestImages.Json16Bit12,int pixelFlavor=AcquiredImage.PixelFlavorChocolate) {
			//Two masked corners (0) and a ramp of 12-bit values.
			return TestImages.Image16(40,30,(x,y) => (x<2 && y<2) ? (ushort)0 : (ushort)(100+x*90+y*10),json,pixelFlavor);
		}

		[TestMethod]
		public void Dicom16_WhenAllowed_WindowOnThe255Scale() {
			RadiographSaveRequest request=Request(Image16(),new RadiographTarget { Mount=Mount(),MountItem=Slot(itemOrder:3) });
			request.Is16BitAllowed=true;
			RadiographSaveResult result=_radiographImport.Save(request);
			Assert.IsTrue(result.IsSaved,result.Error);
			Assert.AreEqual(RadiographFormat.Dicom16,result.Plan.Format);
			CollectionAssert.AreEqual(new[] { "IsCategoryValid","Import","UpdateDocument","UpdateMount" },_store.ListCalls,
				"No 8-bit capture entry (the DICOM header holds the device) and no Pearl upload.");
			Assert.AreEqual(".dcm",_store.FileExtension);
			Assert.AreEqual("application/dicom",_store.MimeType);
			Assert.AreEqual("DICM",Encoding.ASCII.GetString(_store.BytesImported,128,4));
			Assert.AreEqual(12,result.Plan.DicomResult.BitsStored);
			int windowingMin;
			int windowingMax;
			Gray16Samples.GetStoredWindow(result.Plan.DicomResult.WindowLow,result.Plan.DicomResult.WindowHigh,12,out windowingMin,out windowingMax);
			Assert.AreEqual(windowingMin,_store.DocumentUpdated.WindowingMin);
			Assert.AreEqual(windowingMax,_store.DocumentUpdated.WindowingMax);
			Assert.IsTrue(_store.DocumentUpdated.WindowingMax<=255);
			Assert.IsTrue(_store.DocumentUpdated.WindowingMin<_store.DocumentUpdated.WindowingMax);
			Assert.IsTrue(_store.DocumentUpdated.PrintHeading);
			Assert.AreEqual(ImageType.Radiograph,_store.ImageType);
			TestDicomFile testDicomFile=TestDicomReader.Read(_store.BytesImported);
			Assert.AreEqual("3",testDicomFile.Get(0x0020,0x0013).Text.Trim(),"Instance Number is the slot's ItemOrder.");
			Assert.AreEqual(RadiographSavePlanner.GetStudyInstanceUid(request.Target.Mount),testDicomFile.Get(0x0020,0x000D).Text.TrimEnd('\0',' '),
				"One Study per mount.");
		}

		[TestMethod]
		public void Dicom16_StudyUidStablePerMount() {
			Mount mount=Mount();
			Assert.AreEqual(RadiographSavePlanner.GetStudyInstanceUid(mount),RadiographSavePlanner.GetStudyInstanceUid(Mount()));
			Mount mountOther=Mount();
			mountOther.MountNum=8;
			Assert.AreNotEqual(RadiographSavePlanner.GetStudyInstanceUid(mount),RadiographSavePlanner.GetStudyInstanceUid(mountOther));
			Assert.IsNull(RadiographSavePlanner.GetStudyInstanceUid(null));
		}

		[TestMethod]
		public void Dicom16_NotAllowed_NotSaved() {
			RadiographSaveRequest request=Request(Image16(),new RadiographTarget { Mount=Mount(),MountItem=Slot() });
			RadiographSaveResult result=_radiographImport.Save(request);
			Assert.IsFalse(result.IsSaved);
			StringAssert.Contains(result.Error,"16-bit saving is not enabled");
			CollectionAssert.AreEqual(new[] { "IsCategoryValid" },_store.ListCalls);
		}

		[TestMethod]
		public void Dicom16_NoMountSlot_NotSaved() {
			RadiographSaveRequest request=Request(Image16(),new RadiographTarget { ToothNumbers="8" });
			request.Is16BitAllowed=true;
			RadiographSaveResult result=_radiographImport.Save(request);
			Assert.IsFalse(result.IsSaved,"A target with no mount slot is never written as DICOM (design 7.10).");
			Assert.AreEqual(1,_store.ListCalls.Count);
			request=Request(Image16(),new RadiographTarget { Mount=Mount(),MountItem=Slot(toothNumbers:"") });
			request.Is16BitAllowed=true;
			Assert.IsFalse(_radiographImport.Save(request).IsSaved,"Nor a slot with no tooth numbers.");
		}

		[TestMethod]
		public void Dicom16_Invert_OnlyWhenVerified() {
			RadiographSaveRequest request=Request(Image16(pixelFlavor:AcquiredImage.PixelFlavorVanilla),new RadiographTarget { Mount=Mount(),MountItem=Slot() });
			request.Is16BitAllowed=true;
			request.Polarity=TwainPolarity.Invert;
			RadiographSaveResult result=_radiographImport.Save(request);
			Assert.IsFalse(result.IsSaved);
			StringAssert.Contains(result.Error,"no verification record");
			request.IsInversionVerified=true;
			result=_radiographImport.Save(request);
			Assert.IsTrue(result.IsSaved,result.Error);
			Assert.IsTrue(result.Plan.IsInverted);
			//The stored samples are the inversion of the delivered ones, within 12 bits.
			TestDicomFile testDicomFile=TestDicomReader.Read(_store.BytesImported);
			byte[] arrayPixelData=testDicomFile.Get(0x7FE0,0x0010).Value;
			ushort[] arrayDelivered=request.Image.Gray16;
			for(int i=0;i<arrayDelivered.Length;i++) {
				ushort stored=BitConverter.ToUInt16(arrayPixelData,i*2);
				Assert.AreEqual(arrayDelivered[i]==0 ? 0 : 4096-arrayDelivered[i],stored);
			}
			StringAssert.Contains(testDicomFile.Get(0x0020,0x4000).Text,"polarity=inverted");
		}

		[TestMethod]
		public void Dicom16_DeliveredChocolate_VerifiedInvert_Saved() {
			//twain-contract 1.1 keeps TWPF_CHOCOLATE and publishes after the clinic run whether the values follow it: if they do not, the
			//verified Polarity=Invert is the clinic's fix, so the writer must accept it.
			RadiographSaveRequest request=Request(Image16(),new RadiographTarget { Mount=Mount(),MountItem=Slot() });
			request.Is16BitAllowed=true;
			request.Polarity=TwainPolarity.Invert;
			request.IsInversionVerified=true;
			RadiographSaveResult result=_radiographImport.Save(request);
			Assert.IsTrue(result.IsSaved,result.Error);
			Assert.IsTrue(result.Plan.IsInverted);
			TestDicomFile testDicomFile=TestDicomReader.Read(_store.BytesImported);
			StringAssert.Contains(testDicomFile.Get(0x0020,0x4000).Text,"polarity=inverted");
			ushort[] arrayDelivered=request.Image.Gray16;
			byte[] arrayPixelData=testDicomFile.Get(0x7FE0,0x0010).Value;
			Assert.AreEqual(4096-arrayDelivered[100],BitConverter.ToUInt16(arrayPixelData,100*2));
		}

		[TestMethod]
		public void Dicom16_NotFromTheNegotiated16BitMemoryTransfer_Refused() {
			AcquiredImage imageNative=Image16();
			imageNative.TransferMechanism=TwainTransferMechanism.Native;//A 16bpp native DIB the library kept as raw samples.
			RadiographSaveRequest request=Request(imageNative,new RadiographTarget { Mount=Mount(),MountItem=Slot() });
			request.Is16BitAllowed=true;
			RadiographSaveResult result=_radiographImport.Save(request);
			Assert.IsFalse(result.IsSaved);
			StringAssert.Contains(result.Error,"verified 16-bit memory transfer");
			CollectionAssert.AreEqual(new[] { "IsCategoryValid" },_store.ListCalls,"Nothing written: the caller quarantines it.");
			AcquiredImage imageIn8BitEnable=Image16();
			imageIn8BitEnable.RequestedBits=8;
			imageIn8BitEnable.IsBitDepth16InEffect=false;//16-bit memory samples in an 8-bit enable.
			request=Request(imageIn8BitEnable,new RadiographTarget { Mount=Mount(),MountItem=Slot() });
			request.Is16BitAllowed=true;
			Assert.IsFalse(_radiographImport.Save(request).IsSaved);
		}

		[TestMethod]
		public void Dicom16_DeliveredVanilla_NotInverted_RefusedByWriter() {
			RadiographSaveRequest request=Request(Image16(pixelFlavor:AcquiredImage.PixelFlavorVanilla),new RadiographTarget { Mount=Mount(),MountItem=Slot() });
			request.Is16BitAllowed=true;
			RadiographSaveResult result=_radiographImport.Save(request);
			Assert.IsFalse(result.IsSaved,"TWPF_VANILLA samples stored as delivered would have 0 as white.");
			StringAssert.Contains(result.Error,"VANILLA");
		}

		[TestMethod]
		public void EightBitInSixteenBitEnable_GoesThePngPath() {
			AcquiredImage image=Image8();
			image.RequestedBits=16;
			image.IsBitDepth16InEffect=true;
			image.IsDeliveredAs8BitIn16BitEnable=true;
			RadiographSaveRequest request=Request(image,new RadiographTarget { Mount=Mount(),MountItem=Slot() });
			request.Is16BitAllowed=true;
			RadiographSaveResult result=_radiographImport.Save(request);
			Assert.IsTrue(result.IsSaved,result.Error);
			Assert.AreEqual(RadiographFormat.Png8,result.Plan.Format);
			StringAssert.Contains(_store.LogText,"delivered as 8-bit in a 16-bit enable");
		}
		#endregion Dicom16

		#region Planner and helpers
		[TestMethod]
		public void ImgType_FromPixelType() {
			Assert.AreEqual(ImageType.Radiograph,RadiographSavePlanner.GetImgType(AcquiredImage.PixelTypeGray,false));
			Assert.AreEqual(ImageType.Radiograph,RadiographSavePlanner.GetImgType(AcquiredImage.PixelTypeBw,false));
			Assert.AreEqual(ImageType.Photo,RadiographSavePlanner.GetImgType(AcquiredImage.PixelTypeRgb,true));
			Assert.AreEqual(ImageType.Radiograph,RadiographSavePlanner.GetImgType(-1,true));
			Assert.AreEqual(ImageType.Photo,RadiographSavePlanner.GetImgType(AcquiredImage.PixelTypePalette,false));
		}

		[TestMethod]
		public void DateCreated_AcquiredAtThenReceiveTimeThenNow() {
			CaptureMetadata captureMetadata=CaptureMetadataParser.Parse(TestImages.Json8Bit);
			DateTime receivedAt=new DateTime(2026,10,31,10,0,5);
			Assert.AreEqual(captureMetadata.AcquiredAt.Value.LocalDateTime,RadiographSavePlanner.GetDateCreated(captureMetadata,receivedAt,_dateTimeNow));
			Assert.AreEqual(receivedAt,RadiographSavePlanner.GetDateCreated(null,receivedAt,_dateTimeNow));
			Assert.AreEqual(_dateTimeNow,RadiographSavePlanner.GetDateCreated(null,DateTime.MinValue,_dateTimeNow));
		}

		[TestMethod]
		public void Plan_BadDib_NotSavable() {
			AcquiredImage image=Image8();
			image.Dib=new byte[10];
			RadiographSavePlan plan=RadiographSavePlanner.Plan(Request(image,new RadiographTarget()),_dateTimeNow);
			Assert.IsFalse(plan.IsSavable);
			Assert.AreEqual(RadiographFormat.None,plan.Format);
			Assert.AreNotEqual("",plan.Error);
			Assert.IsFalse(RadiographSavePlanner.Plan(null,_dateTimeNow).IsSavable);
		}

		[TestMethod]
		public void Target_LabelsAndDicomView() {
			Assert.AreEqual("Single image",new RadiographTarget().GetLabel());
			Assert.AreEqual("Single image (teeth 3)",new RadiographTarget { ToothNumbers="3" }.GetLabel());
			Assert.AreEqual("Unmounted",new RadiographTarget { Mount=Mount() }.GetLabel());
			RadiographTarget target=new RadiographTarget { Mount=Mount(flipOnAcquire:true),MountItem=Slot(rotateOnAcquire:270) };
			Assert.IsTrue(target.HasMountSlot);
			Assert.AreEqual(270,target.ToIntraOralTarget().DegreesRotated);
			Assert.IsTrue(target.ToIntraOralTarget().IsFlipped);
			Assert.AreEqual("8,9",target.ToIntraOralTarget().ToothNumbers);
			Assert.IsFalse(new RadiographTarget { Mount=Mount(),MountItem=Slot(itemOrder:-1) }.HasMountSlot);
			Assert.IsFalse(new RadiographTarget { ToothNumbers="8" }.ToIntraOralTarget().HasMountSlot);
		}

		[TestMethod]
		public void Provider_TodaysAppointmentThenPrimary() {
			Patient patient=Pat();
			DateTime now=new DateTime(2026,10,31,11,0,0);
			List<Appointment> listAppointments=new List<Appointment> {
				new Appointment { AptDateTime=new DateTime(2026,10,30,11,0,0),AptStatus=ApptStatus.Complete,ProvNum=9 },
				new Appointment { AptDateTime=new DateTime(2026,10,31,9,0,0),AptStatus=ApptStatus.Complete,ProvNum=5 },
				new Appointment { AptDateTime=new DateTime(2026,10,31,10,30,0),AptStatus=ApptStatus.Scheduled,ProvNum=6,ProvHyg=7,IsHygiene=true },
				new Appointment { AptDateTime=new DateTime(2026,10,31,10,45,0),AptStatus=ApptStatus.Broken,ProvNum=8 },
				new Appointment { AptDateTime=new DateTime(2026,10,31,14,0,0),AptStatus=ApptStatus.Scheduled,ProvNum=10 },
			};
			Assert.AreEqual(7,RadiographProvider.GetDefaultProvNum(patient,listAppointments,now),"The hygiene appointment that started last: ProvHyg.");
			listAppointments[2].IsHygiene=false;
			Assert.AreEqual(6,RadiographProvider.GetDefaultProvNum(patient,listAppointments,now));
			Assert.AreEqual(5,RadiographProvider.GetDefaultProvNum(patient,listAppointments,new DateTime(2026,10,31,8,0,0)),
				"Before every appointment today: the first one.");
			Assert.AreEqual(3,RadiographProvider.GetDefaultProvNum(patient,listAppointments,new DateTime(2026,11,1,9,0,0)),"None today: PriProv.");
			Assert.AreEqual(3,RadiographProvider.GetDefaultProvNum(patient,null,now));
			Assert.AreEqual(0,RadiographProvider.GetDefaultProvNum(null,null,now));
		}
		#endregion Planner and helpers

		///<summary>Records each call in order. ThrowOn names the calls that throw, separated by '|'.</summary>
		private sealed class FakeRadiographStore:IRadiographStore {
			public List<string> ListCalls=new List<string>();
			public string ThrowOn="";
			public bool IsCategoryValidResult=true;
			public byte[] BytesImported;
			public string MimeType;
			public string FileExtension;
			public ImageType ImageType;
			public Patient PatientImported;
			public Document DocumentImported;
			public Document DocumentUpdated;
			public Document DocumentOld;
			public Document DocumentDeleted;
			public MountItem MountItemInserted;
			public MountItem MountItemDeleted;
			public Mount MountUpdated;
			public MountItem MountItemPearl;
			public EnumPermType LogPermType;
			public long LogPatNum;
			public string LogText;
			public long LogFKey;
			private long _docNumNext=1000;
			private long _mountItemNumNext=5000;

			private void Call(string name) {
				ListCalls.Add(name);
				if(ThrowOn.Split('|').Contains(name)) {
					throw new Exception(name+" failed");
				}
			}

			public bool IsCategoryValid(long defNum) {
				Call("IsCategoryValid");
				return IsCategoryValidResult;
			}

			public Document Import(byte[] bytes,long docCategory,ImageType imageType,Patient patient,string mimeType,string fileExtension) {
				Call("Import");
				BytesImported=bytes;
				MimeType=mimeType;
				FileExtension=fileExtension;
				ImageType=imageType;
				PatientImported=patient;
				//As ImageStore.Import(byte[]) leaves the row.
				DocumentImported=new Document { DocNum=_docNumNext++,PatNum=patient.PatNum,DocCategory=docCategory,ImgType=imageType,
					FileName="TestPat"+_docNumNext+fileExtension,DateCreated=new DateTime(2026,10,31,10,30,1),Description="" };
				return DocumentImported;
			}

			public void InsertMountItem(MountItem mountItem) {
				Call("InsertMountItem");
				mountItem.MountItemNum=_mountItemNumNext++;
				MountItemInserted=mountItem;
			}

			public void UpdateDocument(Document document,Document documentOld) {
				Call("UpdateDocument");
				DocumentUpdated=document;
				DocumentOld=documentOld;
			}

			public void DeleteDocument(Document document,Patient patient) {
				Call("DeleteDocument");
				DocumentDeleted=document;
			}

			public void DeleteMountItem(MountItem mountItem) {
				Call("DeleteMountItem");
				MountItemDeleted=mountItem;
			}

			public void UpdateMount(Mount mount) {
				Call("UpdateMount");
				MountUpdated=mount;
			}

			public void MakeLogEntry(EnumPermType permType,long patNum,string logText,long fKey) {
				Call("MakeLogEntry");
				LogPermType=permType;
				LogPatNum=patNum;
				LogText=logText;
				LogFKey=fKey;
			}

			public void SendToPearlIfConfigured(Patient patient,Document document,byte[] pngBytes,MountItem mountItem) {
				Call("SendToPearl");
				MountItemPearl=mountItem;
			}
		}
	}
}
