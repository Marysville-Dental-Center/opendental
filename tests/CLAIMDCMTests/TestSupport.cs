// CLAIM-DCM test support: synthetic X-rays written by the program's own DicomIntraOralWriter, a temporary A to Z patient folder with an
// in-memory preference cache (no database), malformed .dcm files, and pixel checks. All images and values are made up.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using CodeBase;
using ImagingCapture.Dicom;
using ImagingCapture.Metadata;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace CLAIMDCMTests {
	internal static class TestSupport {
		///<summary>The synthetic X-ray: 64 x 32 pixels, so each half is whole 16 x 16 JPEG blocks and a JPEG copy keeps flat halves flat.</summary>
		public const int Width=64;
		public const int Height=32;
		///<summary>16-bit raw values whose 0-255 windowing values (raw/256) are 100 and 150.</summary>
		public const ushort Raw16Left=25600;
		public const ushort Raw16Right=38400;
		///<summary>12-bit raw values whose 0-255 windowing values (raw/16) are 100 and 150.</summary>
		public const ushort Raw12Left=1600;
		public const ushort Raw12Right=2400;
		///<summary>The stored window of the tests: 50 to 200 on the 0-255 scale. 100 shows as 255*(100-50)/150=85 and 150 as 170.</summary>
		public const int WindowMin=50;
		public const int WindowMax=200;
		public const int ExpectedLeft=85;
		public const int ExpectedRight=170;

		///<summary>Fills the preference cache with a local A to Z folder, the only preference the image code reads here.</summary>
		public static void UseLocalAtoZ() {
			DataTable table=new DataTable();
			table.Columns.Add("PrefNum");
			table.Columns.Add("PrefName");
			table.Columns.Add("ValueString");
			table.Rows.Add("1",nameof(PrefName.AtoZfolderUsed),((int)DataStorageType.LocalAtoZ).ToString());
			Prefs.FillCacheFromTable(table);
		}

		public static string NewFolder() {
			string folder=Path.Combine(Path.GetTempPath(),"claimdcmtests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			return folder;
		}

		public static void DeleteFolder(string folder) {
			try {
				Directory.Delete(folder,true);
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}

		///<summary>Left half one raw value, right half another.</summary>
		public static ushort[] Halves(ushort rawLeft,ushort rawRight) {
			ushort[] arrayPixels=new ushort[Width*Height];
			for(int y=0;y<Height;y++) {
				for(int x=0;x<Width;x++) {
					arrayPixels[y*Width+x]=x<Width/2 ? rawLeft : rawRight;
				}
			}
			return arrayPixels;
		}

		///<summary>A file exactly as the capture session writes it: Explicit VR Little Endian, uncompressed MONOCHROME2, Bits Allocated 16.
		///effectiveBits 12 with every value at most 4095 gives Bits Stored 12; null gives 16.</summary>
		public static byte[] WriteXray(ushort[] arrayPixels,int? effectiveBits) {
			DicomIntraOralImage image=new DicomIntraOralImage {
				Width=Width,
				Height=Height,
				Pixels=arrayPixels,
				XDpi=1270,
				YDpi=1270,
				PixelFlavor=AcquiredImage.PixelFlavorChocolate,
				Target=new IntraOralTarget { HasMountSlot=true,ToothNumbers="3,30",DegreesRotated=0 },
				Metadata=CaptureMetadataParser.Parse(@"{""schema"":""capture-metadata/1"",""manufacturer"":""Example"",""serial"":""SN-1"","
					+@"""acquiredAt"":""2026-11-01T09:30:00.000-08:00"",""effectiveBits"":"+(effectiveBits==null ? "null" : effectiveBits.ToString())
					+@",""redelivered"":false}"),
				StudyDateTime=new DateTime(2026,11,1),
				InstanceNumber=1,
			};
			DicomWriteResult result=DicomIntraOralWriter.Write(image);
			Assert.IsTrue(result.IsWritten,result.Error);
			Assert.AreEqual(effectiveBits==12 ? 12 : 16,result.BitsStored,"the writer's Bits Stored");
			return result.Bytes;
		}

		public static byte[] Write16() {
			return WriteXray(Halves(Raw16Left,Raw16Right),null);
		}

		public static byte[] Write12() {
			return WriteXray(Halves(Raw12Left,Raw12Right),12);
		}

		///<summary>Saves the bytes in the folder and returns a document for them with the test window stored.</summary>
		public static Document SaveDocument(string patFolder,string fileName,byte[] arrayBytes) {
			File.WriteAllBytes(Path.Combine(patFolder,fileName),arrayBytes);
			return new Document {
				DocNum=1,
				PatNum=1,
				FileName=fileName,
				ImgType=ImageType.Radiograph,
				WindowingMin=WindowMin,
				WindowingMax=WindowMax,
				DateTStamp=DateTime.Now.AddDays(-1),
			};
		}

		///<summary>Malformed .dcm contents: not DICOM at all, a capture file cut short, and a DICOM preamble and prefix with nothing usable after.</summary>
		public static byte[][] MalformedFiles() {
			byte[] bytesPreambleOnly=new byte[128+4+16];
			Array.Copy(System.Text.Encoding.ASCII.GetBytes("DICM"),0,bytesPreambleOnly,128,4);
			for(int i=132;i<bytesPreambleOnly.Length;i++) {
				bytesPreambleOnly[i]=0xFF;
			}
			return new byte[][] {
				new byte[] { 1,2,3,4,5,6,7,8 },
				Write16().Take(500).ToArray(),
				bytesPreambleOnly,
			};
		}

		public static int Gray(Bitmap bitmap,int x,int y) {
			Color color=bitmap.GetPixel(x,y);
			Assert.AreEqual(color.R,color.G,"gray at "+x+","+y);
			Assert.AreEqual(color.R,color.B,"gray at "+x+","+y);
			return color.R;
		}

		///<summary>Every pixel of the area is the value (exact), as a rendered 8-bit copy is.</summary>
		public static void AssertArea(Bitmap bitmap,Rectangle rectangle,int expected,string message) {
			for(int y=rectangle.Top;y<rectangle.Bottom;y++) {
				for(int x=rectangle.Left;x<rectangle.Right;x++) {
					Assert.AreEqual(expected,Gray(bitmap,x,y),message+" at "+x+","+y);
				}
			}
		}

		///<summary>Every pixel of the area is within 2 of the value, as a JPEG copy of a flat area is.</summary>
		public static void AssertAreaNear(Bitmap bitmap,Rectangle rectangle,int expected,string message) {
			for(int y=rectangle.Top;y<rectangle.Bottom;y++) {
				for(int x=rectangle.Left;x<rectangle.Right;x++) {
					Color color=bitmap.GetPixel(x,y);
					Assert.IsTrue(Math.Abs(color.R-expected)<=2 && Math.Abs(color.G-expected)<=2 && Math.Abs(color.B-expected)<=2,
						message+" at "+x+","+y+": expected about "+expected+", got "+color);
				}
			}
		}

		///<summary>The left and right halves of an unrotated, uncropped copy, a few pixels away from the edge between them.</summary>
		public static readonly Rectangle LeftHalf=new Rectangle(0,0,Width/2-4,Height);
		public static readonly Rectangle RightHalf=new Rectangle(Width/2+4,0,Width/2-4,Height);

		///<summary>The image in JPEG bytes, decoded. The caller disposes it.</summary>
		public static Bitmap DecodeJpeg(byte[] arrayBytes) {
			Assert.IsNotNull(arrayBytes,"no bytes");
			Assert.IsTrue(arrayBytes.Length>4 && arrayBytes[0]==0xFF && arrayBytes[1]==0xD8,"JPEG start of image");
			Assert.IsFalse(ClaimAttachDicom.IsDicomBytes(arrayBytes),"not a DICOM file");
			using MemoryStream memoryStream=new MemoryStream(arrayBytes);
			using Image image=Image.FromStream(memoryStream);
			Assert.AreEqual(System.Drawing.Imaging.ImageFormat.Jpeg.Guid,image.RawFormat.Guid,"decoded as a JPEG");
			return new Bitmap(image);
		}

		///<summary>Runs the action and returns the ODException it throws; fails when it throws nothing (MSTest v1 has no ThrowsException).</summary>
		public static ODException AssertThrowsOD(Action action,string message) {
			try {
				action();
			}
			catch(ODException ex) {
				return ex;
			}
			Assert.Fail("Expected an ODException: "+message);
			return null;
		}

		public static string Hash(string path) {
			using System.Security.Cryptography.SHA256 sha256=System.Security.Cryptography.SHA256.Create();
			return Convert.ToBase64String(sha256.ComputeHash(File.ReadAllBytes(path)));
		}

		///<summary>The fields of a document that rendering must never change.</summary>
		public static string Settings(Document document) {
			return string.Join("|",document.FileName,document.WindowingMin,document.WindowingMax,document.CropX,document.CropY,document.CropW,
				document.CropH,document.DegreesRotated,document.IsFlipped,document.IsCropOld,document.ImgType);
		}

		///<summary>The repository root: the first folder above the test assembly with LICENSE and NOTICE.</summary>
		public static string RepoRoot() {
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null) {
				if(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))) {
					return directoryInfo.FullName;
				}
				directoryInfo=directoryInfo.Parent;
			}
			Assert.Fail("Repository root not found above "+AppDomain.CurrentDomain.BaseDirectory);
			return null;
		}

		public static string ReadSource(params string[] arrayParts) {
			return File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(arrayParts).ToArray()));
		}
	}
}
