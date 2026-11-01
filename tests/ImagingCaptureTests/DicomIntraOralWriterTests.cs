// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ImagingCapture.Dicom;
using ImagingCapture.Metadata;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingCapture.Tests {
	///<summary>DicomIntraOralWriter against design 7.8's attribute checklist, read back with the test-only TestDicomReader (no DICOM
	///library).</summary>
	[TestClass]
	public class DicomIntraOralWriterTests {
		///<summary>Every element the writer may emit, with its VR from PS3.6, keyed by tag. A file that holds any other tag fails.</summary>
		private static readonly Dictionary<uint,string> _dictVrs=new Dictionary<uint,string> {
			{ 0x00080008,"CS" },//Image Type
			{ 0x00080012,"DA" },//Instance Creation Date
			{ 0x00080013,"TM" },//Instance Creation Time
			{ 0x00080016,"UI" },//SOP Class UID
			{ 0x00080018,"UI" },//SOP Instance UID
			{ 0x00080020,"DA" },//Study Date
			{ 0x00080023,"DA" },//Content Date
			{ 0x0008002A,"DT" },//Acquisition DateTime
			{ 0x00080030,"TM" },//Study Time
			{ 0x00080033,"TM" },//Content Time
			{ 0x00080050,"SH" },//Accession Number
			{ 0x00080060,"CS" },//Modality
			{ 0x00080068,"CS" },//Presentation Intent Type
			{ 0x00080070,"LO" },//Manufacturer
			{ 0x00080090,"PN" },//Referring Physician's Name
			{ 0x00081090,"LO" },//Manufacturer's Model Name
			{ 0x00082218,"SQ" },//Anatomic Region Sequence
			{ 0x00082228,"SQ" },//Primary Anatomic Structure Sequence
			{ 0x00100010,"PN" },//Patient's Name
			{ 0x00100020,"LO" },//Patient ID
			{ 0x00100030,"DA" },//Patient's Birth Date
			{ 0x00100040,"CS" },//Patient's Sex
			{ 0x00181000,"LO" },//Device Serial Number
			{ 0x00181164,"DS" },//Imager Pixel Spacing
			{ 0x00181508,"CS" },//Positioner Type
			{ 0x00187004,"CS" },//Detector Type
			{ 0x0018700A,"SH" },//Detector ID
			{ 0x0020000D,"UI" },//Study Instance UID
			{ 0x0020000E,"UI" },//Series Instance UID
			{ 0x00200010,"SH" },//Study ID
			{ 0x00200011,"IS" },//Series Number
			{ 0x00200013,"IS" },//Instance Number
			{ 0x00200020,"CS" },//Patient Orientation
			{ 0x00200062,"CS" },//Image Laterality
			{ 0x00204000,"LT" },//Image Comments
			{ 0x00280002,"US" },//Samples per Pixel
			{ 0x00280004,"CS" },//Photometric Interpretation
			{ 0x00280010,"US" },//Rows
			{ 0x00280011,"US" },//Columns
			{ 0x00280100,"US" },//Bits Allocated
			{ 0x00280101,"US" },//Bits Stored
			{ 0x00280102,"US" },//High Bit
			{ 0x00280103,"US" },//Pixel Representation
			{ 0x00280301,"CS" },//Burned In Annotation
			{ 0x00281040,"CS" },//Pixel Intensity Relationship
			{ 0x00281041,"SS" },//Pixel Intensity Relationship Sign
			{ 0x00281050,"DS" },//Window Center
			{ 0x00281051,"DS" },//Window Width
			{ 0x00281052,"DS" },//Rescale Intercept
			{ 0x00281053,"DS" },//Rescale Slope
			{ 0x00281054,"LO" },//Rescale Type
			{ 0x00282110,"CS" },//Lossy Image Compression
			{ 0x00400555,"SQ" },//Acquisition Context Sequence
			{ 0x20500020,"CS" },//Presentation LUT Shape
			{ 0x7FE00010,"OW" },//Pixel Data
		};
		///<summary>Elements inside the code sequence items.</summary>
		private static readonly Dictionary<uint,string> _dictVrsItem=new Dictionary<uint,string> {
			{ 0x00080100,"SH" },//Code Value
			{ 0x00080102,"SH" },//Coding Scheme Designator
			{ 0x00080104,"LO" },//Code Meaning
		};
		///<summary>Elements that are written for every image, whatever its metadata (Types 1 and 2 of the IOD, and the writer's fixed ones).</summary>
		private static readonly uint[] _arrayAlwaysWritten={ 0x00080008,0x00080012,0x00080013,0x00080016,0x00080018,0x00080020,0x00080023,
			0x00080030,0x00080033,0x00080050,0x00080060,0x00080068,0x00080070,0x00080090,0x00082218,0x00082228,0x00100010,0x00100020,0x00100030,
			0x00100040,0x00181164,0x00181508,0x00187004,0x0020000D,0x0020000E,0x00200010,0x00200011,0x00200013,0x00200020,0x00200062,0x00204000,
			0x00280002,0x00280004,0x00280010,0x00280011,0x00280100,0x00280101,0x00280102,0x00280103,0x00280301,0x00281040,0x00281041,0x00281050,
			0x00281051,0x00281052,0x00281053,0x00281054,0x00282110,0x00400555,0x20500020,0x7FE00010 };

		private const string MetadataJson=@"{""schema"":""capture-metadata/1"",""manufacturer"":""Example Imaging"",""model"":""Model 2"",
""serial"":""SN-000123"",""captureId"":""c-1"",""sequence"":2,""acquiredAt"":""2026-10-31T14:25:30.123+01:00"",""pixelSpacingMm"":[0.03,0.04],
""bitsStored"":16,""effectiveBits"":null,""byteOrder"":""LE"",""photometric"":""MONOCHROME2"",""window"":{""low"":10,""high"":3000},
""rendering"":""original"",""detectorType"":""scintillator"",""redelivered"":false,
""configuredExposure"":{""kv"":70,""ma"":7,""s"":0.125,""dap"":12.5,""source"":""configured""},""dsVersion"":""1.1.0"",""vendor"":{}}";

		///<summary>A width x height image: a masked (zero) first row, then values that step through 1-60000.</summary>
		private static ushort[] MakePixels(int width,int height,int maxValue=60000) {
			ushort[] pixels=new ushort[width*height];
			for(int i=width;i<pixels.Length;i++) {
				pixels[i]=(ushort)(1+(long)(i-width)*(maxValue-1)/Math.Max(1,pixels.Length-width-1));
			}
			return pixels;
		}

		private static DicomIntraOralImage MakeImage(string teeth="8,9",int rotate=90,bool isFlipped=false,string metadataJson=MetadataJson) {
			return new DicomIntraOralImage {
				Width=40,
				Height=30,
				Pixels=MakePixels(40,30),
				XDpi=1270,
				YDpi=1270,
				PixelFlavor=AcquiredImage.PixelFlavorChocolate,
				Target=new IntraOralTarget { HasMountSlot=true,ToothNumbers=teeth,DegreesRotated=rotate,IsFlipped=isFlipped },
				Metadata=CaptureMetadataParser.Parse(metadataJson),
				StudyInstanceUid=DicomUid.FromName("mount:test:1"),
				StudyDateTime=new DateTime(2026,10,30,8,15,0),
				SeriesInstanceUid=DicomUid.FromGuid(new Guid("11111111-2222-3333-4444-555555555555")),
				SeriesNumber=1,
				InstanceNumber=14,
				ReceivedAt=new DateTime(2026,10,31,14,25,31),
				CreatedAt=new DateTime(2026,10,31,14,25,32,500),
			};
		}

		private static TestDicomFile WriteAndRead(DicomIntraOralImage image,out DicomWriteResult result) {
			result=DicomIntraOralWriter.Write(image);
			Assert.IsTrue(result.IsWritten,result.Error);
			Assert.AreEqual("",result.Error);
			return TestDicomReader.Read(result.Bytes);
		}

		private static TestDicomElement Required(TestDicomFile testDicomFile,uint tag) {
			TestDicomElement element=TestDicomFile.Find(testDicomFile.ListDataset,(ushort)(tag>>16),(ushort)(tag & 0xFFFF));
			Assert.IsNotNull(element,"Missing "+TestDicomReader.FormatTag(tag));
			return element;
		}

		#region File structure
		[TestMethod]
		public void File_PreamblePrefixAndFileMeta() {
			DicomWriteResult result;
			TestDicomFile testDicomFile=WriteAndRead(MakeImage(),out result);
			Assert.IsTrue(testDicomFile.Preamble.All(x => x==0),"The preamble is 128 zero bytes.");
			Dictionary<uint,string> dictMetaVrs=new Dictionary<uint,string> { { 0x00020000,"UL" },{ 0x00020001,"OB" },{ 0x00020002,"UI" },
				{ 0x00020003,"UI" },{ 0x00020010,"UI" },{ 0x00020012,"UI" },{ 0x00020013,"SH" } };
			CollectionAssert.AreEqual(dictMetaVrs.Keys.ToList(),testDicomFile.ListMeta.Select(x => x.Tag).ToList());
			foreach(TestDicomElement element in testDicomFile.ListMeta) {
				Assert.AreEqual(dictMetaVrs[element.Tag],element.Vr,element.ToString());
			}
			CollectionAssert.AreEqual(new byte[] { 0x00,0x01 },testDicomFile.Meta(0x0002,0x0001).Value,"File Meta Information Version 00 01.");
			Assert.AreEqual(2u,testDicomFile.Meta(0x0002,0x0001).Length);
			Assert.AreEqual("1.2.840.10008.5.1.4.1.1.1.3",testDicomFile.Meta(0x0002,0x0002).Text);
			Assert.AreEqual(result.SopInstanceUid,testDicomFile.Meta(0x0002,0x0003).Text);
			Assert.AreEqual("1.2.840.10008.1.2.1",testDicomFile.Meta(0x0002,0x0010).Text);
			Assert.AreEqual(DicomUid.ImplementationClassUid,testDicomFile.Meta(0x0002,0x0012).Text);
			Assert.AreEqual("OPENPMSD_CAP1",testDicomFile.Meta(0x0002,0x0013).Text);
			//UI values are padded with NUL, other strings with a space (PS3.5 6.2).
			byte[] bytesTransferSyntax=testDicomFile.Meta(0x0002,0x0010).Value;
			Assert.AreEqual(20,bytesTransferSyntax.Length);
			Assert.AreEqual(0,bytesTransferSyntax[19]);
			Assert.AreEqual(result.SopInstanceUid,Required(testDicomFile,0x00080018).Text,"Media Storage SOP Instance UID matches.");
			Assert.AreEqual(testDicomFile.Meta(0x0002,0x0002).Text,Required(testDicomFile,0x00080016).Text);
		}

		[TestMethod]
		public void File_EveryElementHasItsVrAndNoOtherTagIsWritten() {
			DicomWriteResult result;
			TestDicomFile testDicomFile=WriteAndRead(MakeImage(),out result);
			foreach(TestDicomElement element in testDicomFile.ListDataset) {
				Assert.IsTrue(_dictVrs.ContainsKey(element.Tag),"Unexpected element "+element);
				Assert.AreEqual(_dictVrs[element.Tag],element.Vr,element.ToString());
				if(element.ListItems!=null) {
					foreach(TestDicomElement elementItem in element.ListItems.SelectMany(x => x)) {
						Assert.IsTrue(_dictVrsItem.ContainsKey(elementItem.Tag),"Unexpected item element "+elementItem);
						Assert.AreEqual(_dictVrsItem[elementItem.Tag],elementItem.Vr,elementItem.ToString());
					}
				}
			}
			foreach(uint tag in _arrayAlwaysWritten) {
				Required(testDicomFile,tag);
			}
			//With full metadata, the optional ones are there too.
			foreach(uint tag in new uint[] { 0x0008002A,0x00081090,0x00181000,0x0018700A }) {
				Required(testDicomFile,tag);
			}
			Assert.AreEqual(_dictVrs.Count,testDicomFile.ListDataset.Count);
		}
		#endregion File structure

		#region Modules
		[TestMethod]
		public void Patient_AllType2Empty() {
			DicomWriteResult result;
			TestDicomFile testDicomFile=WriteAndRead(MakeImage(),out result);
			foreach(uint tag in new uint[] { 0x00100010,0x00100020,0x00100030,0x00100040 }) {
				Assert.AreEqual(0u,Required(testDicomFile,tag).Length,TestDicomReader.FormatTag(tag)+" is present and empty.");
			}
			Assert.IsFalse(testDicomFile.ListDataset.Any(x => x.Group==0x0010 && x.Length>0),"No patient data at all.");
		}

		[TestMethod]
		public void Study_Series_Equipment() {
			DicomWriteResult result;
			DicomIntraOralImage image=MakeImage();
			TestDicomFile testDicomFile=WriteAndRead(image,out result);
			Assert.AreEqual(image.StudyInstanceUid,Required(testDicomFile,0x0020000D).Text);
			Assert.AreEqual(image.StudyInstanceUid,result.StudyInstanceUid);
			Assert.AreEqual("20261030",Required(testDicomFile,0x00080020).Text);
			Assert.AreEqual("081500.000",Required(testDicomFile,0x00080030).Text);
			foreach(uint tag in new uint[] { 0x00080090,0x00200010,0x00080050 }) {
				Assert.AreEqual(0u,Required(testDicomFile,tag).Length,"Referring Physician's Name, Study ID and Accession Number are empty.");
			}
			Assert.AreEqual("IO",Required(testDicomFile,0x00080060).Text);
			Assert.AreEqual(image.SeriesInstanceUid,Required(testDicomFile,0x0020000E).Text);
			Assert.AreEqual("1",Required(testDicomFile,0x00200011).Text);
			Assert.AreEqual("FOR PRESENTATION",Required(testDicomFile,0x00080068).Text);
			Assert.AreEqual("Example Imaging",Required(testDicomFile,0x00080070).Text);
			Assert.AreEqual("Model 2",Required(testDicomFile,0x00081090).Text);
			Assert.AreEqual("SN-000123",Required(testDicomFile,0x00181000).Text);
			Assert.AreEqual("SN-000123",Required(testDicomFile,0x0018700A).Text,"Detector ID.");
			Assert.AreEqual("SCINTILLATOR",Required(testDicomFile,0x00187004).Text,"Detector Type from the metadata, as a DICOM term.");
		}

		[TestMethod]
		public void Image_DxImageModule() {
			DicomWriteResult result;
			TestDicomFile testDicomFile=WriteAndRead(MakeImage(),out result);
			TestDicomElement elementImageType=Required(testDicomFile,0x00080008);
			CollectionAssert.AreEqual(new[] { "ORIGINAL","PRIMARY","" },elementImageType.Values,"Value 3 present and empty (PS3.3 C.8.11.3.1.1).");
			Assert.AreEqual("ORIGINAL\\PRIMARY\\ ",Encoding.ASCII.GetString(elementImageType.Value),"17 characters padded to 18.");
			Assert.AreEqual("14",Required(testDicomFile,0x00200013).Text,"Instance Number.");
			Assert.AreEqual(@"F\R",Required(testDicomFile,0x00200020).Text,"Anterior slot rotated 90 degrees.");
			Assert.AreEqual(@"F\R",result.PatientOrientation);
			Assert.AreEqual(1,Required(testDicomFile,0x00280002).US);
			Assert.AreEqual("MONOCHROME2",Required(testDicomFile,0x00280004).Text);
			Assert.AreEqual(30,Required(testDicomFile,0x00280010).US,"Rows = height.");
			Assert.AreEqual(40,Required(testDicomFile,0x00280011).US,"Columns = width.");
			Assert.AreEqual(16,Required(testDicomFile,0x00280100).US);
			Assert.AreEqual(16,Required(testDicomFile,0x00280101).US,"effectiveBits null: 16.");
			Assert.AreEqual(15,Required(testDicomFile,0x00280102).US);
			Assert.AreEqual(0,Required(testDicomFile,0x00280103).US);
			Assert.AreEqual("LIN",Required(testDicomFile,0x00281040).Text);
			Assert.AreEqual(-1,Required(testDicomFile,0x00281041).SS);
			Assert.AreEqual("0",Required(testDicomFile,0x00281052).Text);
			Assert.AreEqual("1",Required(testDicomFile,0x00281053).Text);
			Assert.AreEqual("US",Required(testDicomFile,0x00281054).Text);
			Assert.AreEqual("IDENTITY",Required(testDicomFile,0x20500020).Text);
			Assert.AreEqual("NO",Required(testDicomFile,0x00280301).Text);
			Assert.AreEqual("00",Required(testDicomFile,0x00282110).Text);
			//Content time is the acquisition time, local; Acquisition DateTime keeps the source's UTC offset.
			DateTime dateTimeLocal=new DateTimeOffset(2026,10,31,14,25,30,123,TimeSpan.FromHours(1)).LocalDateTime;
			Assert.AreEqual(dateTimeLocal.ToString("yyyyMMdd"),Required(testDicomFile,0x00080023).Text);
			Assert.AreEqual(dateTimeLocal.ToString("HHmmss")+".123",Required(testDicomFile,0x00080033).Text);
			Assert.AreEqual("20261031142530.123+0100",Required(testDicomFile,0x0008002A).Text);
			Assert.AreEqual("20261031",Required(testDicomFile,0x00080012).Text);
			Assert.AreEqual("142532.500",Required(testDicomFile,0x00080013).Text);
		}

		[TestMethod]
		public void Image_LogarithmicSource() {
			DicomIntraOralImage image=MakeImage();
			image.IsLogarithmic=true;
			DicomWriteResult result;
			Assert.AreEqual("LOG",Required(WriteAndRead(image,out result),0x00281040).Text);
		}

		[TestMethod]
		public void Detector_ImagerPixelSpacing() {
			DicomWriteResult result;
			Assert.AreEqual(@"0.02\0.02",Required(WriteAndRead(MakeImage(),out result),0x00181164).Text,"25.4/1270 mm, from the resolution first.");
			//Row spacing (between rows) comes first: 25.4/YDpi, then 25.4/XDpi.
			DicomIntraOralImage imageAnisotropic=MakeImage();
			imageAnisotropic.XDpi=1000;
			imageAnisotropic.YDpi=500;
			Assert.AreEqual(@"0.0508\0.0254",Required(WriteAndRead(imageAnisotropic,out result),0x00181164).Text);
			//No resolution: the metadata's pixelSpacingMm [x, y] = [0.03, 0.04], again as row\column.
			DicomIntraOralImage imageNoDpi=MakeImage();
			imageNoDpi.XDpi=0;
			imageNoDpi.YDpi=0;
			Assert.AreEqual(@"0.04\0.03",Required(WriteAndRead(imageNoDpi,out result),0x00181164).Text);
			DicomIntraOralImage image600=MakeImage();
			image600.XDpi=600;
			image600.YDpi=600;
			string spacing=Required(WriteAndRead(image600,out result),0x00181164).Text;
			Assert.AreEqual(@"0.0423333333\0.0423333333",spacing);
			Assert.AreEqual("NONE",Required(WriteAndRead(MakeImage(),out result),0x00181508).Text,"Positioner Type.");
		}

		[TestMethod]
		public void Anatomy_LateralityRegionAndToothCodes() {
			DicomWriteResult result;
			TestDicomFile testDicomFile=WriteAndRead(MakeImage("8,9"),out result);
			Assert.AreEqual("B",Required(testDicomFile,0x00200062).Text,"Teeth on both sides.");
			List<List<TestDicomElement>> listRegion=Required(testDicomFile,0x00082218).ListItems;
			Assert.AreEqual(1,listRegion.Count,"Anatomic Region Sequence: a single item.");
			Assert.AreEqual("70925003",TestDicomFile.Find(listRegion[0],0x0008,0x0100).Text);
			Assert.AreEqual("SCT",TestDicomFile.Find(listRegion[0],0x0008,0x0102).Text);
			Assert.AreEqual("Maxilla",TestDicomFile.Find(listRegion[0],0x0008,0x0104).Text);
			List<List<TestDicomElement>> listTeeth=Required(testDicomFile,0x00082228).ListItems;
			Assert.AreEqual(2,listTeeth.Count);
			CollectionAssert.AreEqual(new[] { "11","21" },listTeeth.Select(x => TestDicomFile.Find(x,0x0008,0x0100).Text).ToList());
			Assert.IsTrue(listTeeth.All(x => TestDicomFile.Find(x,0x0008,0x0102).Text=="ISO3950"));
			Assert.AreEqual("Permanent maxillary right central incisor",TestDicomFile.Find(listTeeth[0],0x0008,0x0104).Text);
			Assert.AreEqual("R",Required(WriteAndRead(MakeImage("2,3,30,31",0),out result),0x00200062).Text);
			Assert.AreEqual(@"A\F",result.PatientOrientation);
			Assert.AreEqual("L",Required(WriteAndRead(MakeImage("14,15",180),out result),0x00200062).Text);
			TestDicomElement elementContext=Required(testDicomFile,0x00400555);
			Assert.AreEqual(0u,elementContext.Length,"Acquisition Context Sequence: Type 2, empty.");
			Assert.AreEqual(0,elementContext.ListItems.Count);
		}

		[TestMethod]
		public void Window_PercentilesExcludingZero() {
			DicomWriteResult result;
			DicomIntraOralImage image=MakeImage();
			//1200 pixels: 200 zeros (masked), then 1-1000 once each.
			image.Pixels=new ushort[1200];
			for(int i=200;i<1200;i++) {
				image.Pixels[i]=(ushort)(i-199);
			}
			TestDicomFile testDicomFile=WriteAndRead(image,out result);
			Assert.AreEqual(5,result.WindowLow,"Rank ceil(0.005*1000)=5.");
			Assert.AreEqual(995,result.WindowHigh,"Rank ceil(0.995*1000)=995.");
			Assert.AreEqual("500.5",Required(testDicomFile,0x00281050).Text,"Center (low+high+1)/2.");
			Assert.AreEqual("991",Required(testDicomFile,0x00281051).Text,"Width high-low+1.");
			int low;
			int high;
			DicomIntraOralWriter.GetWindow(new ushort[100],out low,out high);
			Assert.AreEqual(0,low,"All zero: no window to compute.");
			Assert.AreEqual(0,high);
			double center;
			double width;
			DicomIntraOralWriter.GetWindowCenterWidth(0,0,out center,out width);
			Assert.AreEqual(1,width,"Width is at least 1 (PS3.3 C.11.2.1.2).");
			DicomIntraOralWriter.GetWindow(new ushort[] { 0,0,7 },out low,out high);
			Assert.AreEqual(7,low);
			Assert.AreEqual(7,high);
			//A few outliers at both ends fall outside the window.
			ushort[] arrayPixels=Enumerable.Repeat((ushort)2000,1000).ToArray();
			arrayPixels[0]=1;
			arrayPixels[1]=65535;
			DicomIntraOralWriter.GetWindow(arrayPixels,out low,out high);
			Assert.AreEqual(2000,low);
			Assert.AreEqual(2000,high);
		}

		[TestMethod]
		public void PixelData_UnchangedLittleEndian() {
			DicomWriteResult result;
			DicomIntraOralImage image=MakeImage();
			image.Pixels[45]=0xABCD;
			image.Pixels[46]=65535;
			TestDicomFile testDicomFile=WriteAndRead(image,out result);
			TestDicomElement elementPixels=Required(testDicomFile,0x7FE00010);
			Assert.AreEqual((uint)(40*30*2),elementPixels.Length);
			Assert.AreEqual(testDicomFile.ListDataset.Last(),elementPixels,"Pixel Data is the last element.");
			for(int i=0;i<image.Pixels.Length;i++) {
				Assert.AreEqual(image.Pixels[i],BitConverter.ToUInt16(elementPixels.Value,i*2),"Sample "+i);
			}
			Assert.AreEqual(0xCD,elementPixels.Value[90]);
			Assert.AreEqual(0xAB,elementPixels.Value[91]);
		}

		[TestMethod]
		public void BitsStored_Rule() {
			ushort[] arrayLow=Enumerable.Range(0,100).Select(x => (ushort)(x*41)).ToArray();//0-4059
			Assert.AreEqual(12,DicomIntraOralWriter.GetBitsStored(arrayLow,12));
			Assert.AreEqual(12,DicomIntraOralWriter.GetBitsStored(arrayLow,10));
			Assert.AreEqual(16,DicomIntraOralWriter.GetBitsStored(arrayLow,null),"Unknown effective bits: 16.");
			Assert.AreEqual(16,DicomIntraOralWriter.GetBitsStored(arrayLow,14),"14-bit data is stored as 16.");
			Assert.AreEqual(16,DicomIntraOralWriter.GetBitsStored(arrayLow,16));
			ushort[] array4096=(ushort[])arrayLow.Clone();
			array4096[50]=4096;
			Assert.AreEqual(16,DicomIntraOralWriter.GetBitsStored(array4096,12),"A value above 4095 needs 16.");
			ushort[] array4095=(ushort[])arrayLow.Clone();
			array4095[50]=4095;
			Assert.AreEqual(12,DicomIntraOralWriter.GetBitsStored(array4095,12));
			//Through the file: Bits Stored and High Bit, values unchanged.
			DicomIntraOralImage image=MakeImage(metadataJson:MetadataJson.Replace(@"""effectiveBits"":null",@"""effectiveBits"":12"));
			image.Pixels=MakePixels(40,30,4095);
			DicomWriteResult result;
			TestDicomFile testDicomFile=WriteAndRead(image,out result);
			Assert.AreEqual(12,result.BitsStored);
			Assert.AreEqual(12,Required(testDicomFile,0x00280101).US);
			Assert.AreEqual(11,Required(testDicomFile,0x00280102).US);
			Assert.AreEqual(16,Required(testDicomFile,0x00280100).US,"Bits Allocated stays 16.");
			Assert.AreEqual(4095,BitConverter.ToUInt16(Required(testDicomFile,0x7FE00010).Value,(40*30-1)*2),"Never rescaled.");
			DicomIntraOralImage image14=MakeImage(metadataJson:MetadataJson.Replace(@"""effectiveBits"":null",@"""effectiveBits"":14"));
			image14.Pixels=MakePixels(40,30,16383);
			Assert.AreEqual(16,Required(WriteAndRead(image14,out result),0x00280101).US);
			Assert.AreEqual(16383,BitConverter.ToUInt16(Required(TestDicomReader.Read(result.Bytes),0x7FE00010).Value,(40*30-1)*2));
		}
		#endregion Modules

		#region Metadata
		[TestMethod]
		public void ConfiguredExposure_OnlyInImageCommentsLabelled() {
			DicomWriteResult result;
			TestDicomFile testDicomFile=WriteAndRead(MakeImage(),out result);
			string comments=Required(testDicomFile,0x00204000).Text;
			Assert.AreEqual("rendering=original; configured exposure (configured, not measured): 70 kV, 7 mA, 0.125 s, DAP 12.5 mGy.cm2",comments);
			Assert.AreEqual(comments,result.ImageComments);
			//Never in the X-ray acquisition or dose attributes.
			uint[] arrayTagsNever={ 0x00180060,0x00181150,0x00181151,0x00181152,0x00181153,0x0018115E,0x00181405,0x00189332,0x00400302,0x0018115A };
			foreach(uint tag in arrayTagsNever) {
				Assert.IsNull(TestDicomFile.Find(testDicomFile.ListDataset,(ushort)(tag>>16),(ushort)(tag & 0xFFFF)),
					"No "+TestDicomReader.FormatTag(tag));
			}
		}

		[TestMethod]
		public void NoMetadata_Type2EmptyAndOptionalLeftOut() {
			DicomIntraOralImage image=MakeImage(metadataJson:null);
			Assert.IsNull(image.Metadata);
			DicomWriteResult result;
			TestDicomFile testDicomFile=WriteAndRead(image,out result);
			Assert.AreEqual(0u,Required(testDicomFile,0x00080070).Length,"Manufacturer: Type 2, empty.");
			Assert.AreEqual(0u,Required(testDicomFile,0x00187004).Length,"Detector Type: Type 2, empty.");
			foreach(uint tag in new uint[] { 0x0008002A,0x00081090,0x00181000,0x0018700A }) {
				Assert.IsNull(TestDicomFile.Find(testDicomFile.ListDataset,(ushort)(tag>>16),(ushort)(tag & 0xFFFF)),"Type 3 left out: "
					+TestDicomReader.FormatTag(tag));
			}
			Assert.AreEqual("rendering=original",Required(testDicomFile,0x00204000).Text);
			Assert.AreEqual("20261031",Required(testDicomFile,0x00080023).Text,"Content Date from ReceivedAt.");
			Assert.AreEqual("142531.000",Required(testDicomFile,0x00080033).Text);
			foreach(uint tag in _arrayAlwaysWritten) {
				Required(testDicomFile,tag);
			}
		}

		[TestMethod]
		public void DeviceText_CleanedForItsVr() {
			string json=MetadataJson.Replace(@"""Example Imaging""",@"""Ex"+(char)0xE4+@"mple\\Imaging""")
				.Replace(@"""SN-000123""",@"""SERIAL-NUMBER-LONGER-THAN-16""").Replace(@"""scintillator""",@"""cmos""");
			DicomWriteResult result;
			TestDicomFile testDicomFile=WriteAndRead(MakeImage(metadataJson:json),out result);
			Assert.AreEqual("Ex?mple/Imaging",Required(testDicomFile,0x00080070).Text,"Default repertoire only, no backslash.");
			Assert.AreEqual("SERIAL-NUMBER-LONGER-THAN-16",Required(testDicomFile,0x00181000).Text);
			Assert.IsNull(testDicomFile.Get(0x0018,0x700A),"A serial too long for Detector ID (SH) is not cut.");
			Assert.AreEqual(0u,Required(testDicomFile,0x00187004).Length,"Not a DICOM Detector Type term: empty.");
		}

		[TestMethod]
		public void Inversion_VanillaInvertedIsWrittenAndNoted() {
			DicomIntraOralImage image=MakeImage();
			image.PixelFlavor=AcquiredImage.PixelFlavorVanilla;
			image.IsInverted=true;
			DicomWriteResult result;
			TestDicomFile testDicomFile=WriteAndRead(image,out result);
			StringAssert.StartsWith(Required(testDicomFile,0x00204000).Text,"rendering=original; polarity=inverted; ");
			Assert.AreEqual(-1,Required(testDicomFile,0x00281041).SS);
		}

		[TestMethod]
		public void Inversion_ChocolateInvertedIsWrittenAndNoted() {
			//twain-contract 1.1 keeps TWPF_CHOCOLATE and publishes after the clinic run whether the values follow it: a verified Polarity=Invert
			//of a chocolate source is the clinic's fix, and the stored values then follow the verified polarity.
			DicomIntraOralImage image=MakeImage();
			image.IsInverted=true;
			DicomWriteResult result;
			TestDicomFile testDicomFile=WriteAndRead(image,out result);
			Assert.IsTrue(result.IsWritten,result.Error);
			StringAssert.StartsWith(Required(testDicomFile,0x00204000).Text,"rendering=original; polarity=inverted; ");
			Assert.AreEqual(-1,Required(testDicomFile,0x00281041).SS);
			Assert.AreEqual("MONOCHROME2",Required(testDicomFile,0x00280004).Text.Trim());
		}

		[TestMethod]
		public void Uids_NewWhenNotGivenAndUnique() {
			DicomIntraOralImage image=MakeImage();
			image.StudyInstanceUid=null;
			image.SeriesInstanceUid=null;
			DicomWriteResult result1;
			DicomWriteResult result2;
			TestDicomFile testDicomFile1=WriteAndRead(image,out result1);
			TestDicomFile testDicomFile2=WriteAndRead(image,out result2);
			List<string> listUids=new List<string> { result1.SopInstanceUid,result2.SopInstanceUid,result1.StudyInstanceUid,result2.StudyInstanceUid,
				result1.SeriesInstanceUid,result2.SeriesInstanceUid };
			Assert.AreEqual(6,listUids.Distinct().Count());
			foreach(string uid in listUids) {
				Assert.IsTrue(uid.StartsWith("2.25.") && DicomUid.IsValid(uid),uid);
			}
			Assert.AreEqual(result1.StudyInstanceUid,Required(testDicomFile1,0x0020000D).Text);
			Assert.AreEqual(result2.SeriesInstanceUid,Required(testDicomFile2,0x0020000E).Text);
			image.SopInstanceUid="2.25.12345";
			DicomWriteResult result3;
			Assert.AreEqual("2.25.12345",Required(WriteAndRead(image,out result3),0x00080018).Text);
		}
		#endregion Metadata

		#region Refusals
		[TestMethod]
		public void Refuses_TargetWithNoSlotOrNoTeeth() {
			DicomIntraOralImage imageNoSlot=MakeImage();
			imageNoSlot.Target.HasMountSlot=false;
			AssertRefused(imageNoSlot,"no mount slot");
			DicomIntraOralImage imageNoTarget=MakeImage();
			imageNoTarget.Target=null;
			AssertRefused(imageNoTarget,"no mount slot");
			AssertRefused(MakeImage(""),"no tooth numbers");
			AssertRefused(MakeImage(" , "),"no tooth numbers");
			AssertRefused(MakeImage("2,51",0),"cannot be coded");
			AssertRefused(MakeImage("5,6",0),"no single view");
			string reason;
			Assert.IsFalse(DicomIntraOralWriter.CanWrite(new IntraOralTarget { HasMountSlot=false,ToothNumbers="8,9" },out reason));
			StringAssert.Contains(reason,"8 bits");
			Assert.IsFalse(DicomIntraOralWriter.CanWrite(new IntraOralTarget { HasMountSlot=true,ToothNumbers="" },out reason));
			StringAssert.Contains(reason,"8 bits");
			Assert.IsFalse(DicomIntraOralWriter.CanWrite(null,out reason));
			Assert.IsTrue(DicomIntraOralWriter.CanWrite(new IntraOralTarget { HasMountSlot=true,ToothNumbers="8,9",DegreesRotated=90 },out reason));
			Assert.AreEqual("",reason);
		}

		[TestMethod]
		public void Refuses_PixelsThatCannotBeDescribed() {
			DicomIntraOralImage imageNoPixels=MakeImage();
			imageNoPixels.Pixels=null;
			AssertRefused(imageNoPixels,"16-bit");
			DicomIntraOralImage imageWrongLength=MakeImage();
			imageWrongLength.Pixels=new ushort[10];
			AssertRefused(imageWrongLength,"samples");
			DicomIntraOralImage imageZero=MakeImage();
			imageZero.Width=0;
			imageZero.Pixels=new ushort[0];
			AssertRefused(imageZero,"size");
			DicomIntraOralImage imageWide=MakeImage();
			imageWide.Width=70000;
			imageWide.Height=1;
			imageWide.Pixels=new ushort[70000];
			AssertRefused(imageWide,"size");
			DicomIntraOralImage imageVanilla=MakeImage();
			imageVanilla.PixelFlavor=AcquiredImage.PixelFlavorVanilla;
			AssertRefused(imageVanilla,"VANILLA");
			DicomIntraOralImage imageUnknownFlavor=MakeImage();
			imageUnknownFlavor.PixelFlavor=5;
			AssertRefused(imageUnknownFlavor,"flavor");
			DicomIntraOralImage imageNoSpacing=MakeImage(metadataJson:null);
			imageNoSpacing.XDpi=0;
			AssertRefused(imageNoSpacing,"Imager Pixel Spacing");
			DicomIntraOralImage imageBadUid=MakeImage();
			imageBadUid.StudyInstanceUid="1.02.3";
			AssertRefused(imageBadUid,"UID");
			AssertRefused(null,"No image");
		}

		[TestMethod]
		public void Refuses_ImagesThatAreNotTheOriginal() {
			AssertRefused(MakeImage(metadataJson:MetadataJson.Replace(@"""rendering"":""original""",@"""rendering"":""vendor8""")),"vendor8");
			AssertRefused(MakeImage(metadataJson:MetadataJson.Replace(@"""detectorType""",@"""adjustments"":{""rotate"":90},""detectorType""")),
				"adjustments");
			//Neutral adjustments change nothing and are accepted.
			DicomIntraOralImage imageNeutral=MakeImage(metadataJson:MetadataJson.Replace(@"""detectorType""",
				@"""adjustments"":{""brightness"":50,""contrast"":50,""rotate"":0,""flip"":""none""},""detectorType"""));
			Assert.IsNotNull(imageNeutral.Metadata.Adjustments);
			Assert.IsTrue(DicomIntraOralWriter.Write(imageNeutral).IsWritten);
		}

		private static void AssertRefused(DicomIntraOralImage image,string errorPart) {
			DicomWriteResult result=DicomIntraOralWriter.Write(image);
			Assert.IsFalse(result.IsWritten);
			Assert.IsNull(result.Bytes);
			StringAssert.Contains(result.Error,errorPart);
		}
		#endregion Refusals
	}
}
