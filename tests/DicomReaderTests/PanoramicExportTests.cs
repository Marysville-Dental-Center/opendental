// A DICOM file shaped like a real panoramic export (Explicit VR Little Endian, 16 bits allocated, 14 stored, High Bit 13, unsigned
// MONOCHROME2, 2800 x 1300, Window Center 8192 and Width 16384) through the whole display path: the reader, ImageStore's initial window on
// import, the thumbnail, the Imaging module's image and DicomHelper.ApplyWindowing. The image is made up (an arch of teeth over soft tissue
// and air, an unexposed edge and a metal filling); no patient image is used. A local A to Z folder in a temporary directory and an in-memory
// preference cache stand in for an office; no database is used.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace DicomReaderTests {
	[TestClass]
	public class PanoramicExportTests {
		private const int WIDTH=2800;
		private const int HEIGHT=1300;
		private const int MAX_14BIT=16383;
		///<summary>Digital X-Ray Image Storage - For Presentation (PS3.4 B.5), the SOP class panoramic units export.</summary>
		private const string SOP_CLASS_DX_PRESENTATION="1.2.840.10008.5.1.4.1.1.1.1";
		private string _patFolder;

		[TestInitialize]
		public void SetUp() {
			DataTable table=new DataTable();
			table.Columns.Add("PrefNum");
			table.Columns.Add("PrefName");
			table.Columns.Add("ValueString");
			table.Rows.Add("1",nameof(PrefName.AtoZfolderUsed),((int)DataStorageType.LocalAtoZ).ToString());
			Prefs.FillCacheFromTable(table);
			_patFolder=Path.Combine(Path.GetTempPath(),"dicomreadertests_pano_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_patFolder);
		}

		[TestCleanup]
		public void TearDown() {
			try {
				Directory.Delete(_patFolder,true);
			}
			catch(Exception) {
			}
		}

		///<summary>The 14-bit samples of a made-up panoramic image, row by row: dark air with a little noise at the top and bottom, soft tissue
		///in the middle, a bright band of teeth along an arch with darker gaps between the teeth, an unexposed strip (0) at the left and right
		///edges, and a metal filling (the 14-bit maximum).</summary>
		private static ushort[] MakePanoramicSamples() {
			ushort[] arraySamples=new ushort[WIDTH*HEIGHT];
			Random random=new Random(20261102);
			for(int y=0;y<HEIGHT;y++) {
				double v=y/(double)(HEIGHT-1);
				for(int x=0;x<WIDTH;x++) {
					double u=x/(double)(WIDTH-1);
					double archCenter=0.38+0.9*(u-0.5)*(u-0.5);
					double distance=v-archCenter;
					double teeth=Math.Exp(-distance*distance/(2*0.07*0.07))*(0.75+0.25*Math.Cos(u*2*Math.PI*16));
					double softTissue=0.45*Math.Exp(-(u-0.5)*(u-0.5)/(2*0.28*0.28))*Math.Exp(-(v-0.5)*(v-0.5)/(2*0.35*0.35));
					double value=0.03+softTissue+0.62*teeth+(random.NextDouble()-0.5)*0.02;
					value=Math.Max(0,Math.Min(1,value));
					arraySamples[y*WIDTH+x]=(ushort)Math.Round(value*(MAX_14BIT-200));
				}
			}
			for(int y=0;y<HEIGHT;y++) {
				for(int x=0;x<30;x++) {
					arraySamples[y*WIDTH+x]=0;
					arraySamples[y*WIDTH+WIDTH-1-x]=0;
				}
			}
			for(int y=480;y<520;y++) {
				for(int x=900;x<960;x++) {
					arraySamples[y*WIDTH+x]=MAX_14BIT;
				}
			}
			return arraySamples;
		}

		private static byte[] MakePanoramicFile(ushort[] arraySamples) {
			List<SynElement> listElements=SyntheticDicom.ImageDataset(HEIGHT,WIDTH,16,14,13,0,"MONOCHROME2",SyntheticDicom.Words(arraySamples));
			listElements=SyntheticDicom.With(listElements,SyntheticDicom.Text(0x0008,0x0016,"UI",SOP_CLASS_DX_PRESENTATION));
			listElements=SyntheticDicom.With(listElements,SyntheticDicom.Text(0x0008,0x0060,"CS","PX"));
			listElements=SyntheticDicom.With(listElements,SyntheticDicom.Text(0x0028,0x1050,"DS","8192"));
			listElements=SyntheticDicom.With(listElements,SyntheticDicom.Text(0x0028,0x1051,"DS","16384"));
			return SyntheticDicom.MakeFile(listElements);
		}

		///<summary>The gray value (R, which equals G and B) of every pixel, read with LockBits.</summary>
		private static byte[] GetGrays(Bitmap bitmap) {
			using(Bitmap bitmap32=new Bitmap(bitmap.Width,bitmap.Height,PixelFormat.Format32bppArgb)) {
				using(Graphics graphics=Graphics.FromImage(bitmap32)) {
					graphics.DrawImageUnscaled(bitmap,0,0);
				}
				BitmapData bitmapData=bitmap32.LockBits(new Rectangle(0,0,bitmap32.Width,bitmap32.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
				try {
					byte[] arrayBytes=new byte[bitmapData.Stride*bitmap32.Height];
					Marshal.Copy(bitmapData.Scan0,arrayBytes,0,arrayBytes.Length);
					byte[] arrayGrays=new byte[bitmap32.Width*bitmap32.Height];
					for(int y=0;y<bitmap32.Height;y++) {
						for(int x=0;x<bitmap32.Width;x++) {
							int i=y*bitmapData.Stride+x*4;
							if(arrayBytes[i+2]!=arrayBytes[i+1] || arrayBytes[i+2]!=arrayBytes[i]) {
								Assert.Fail("Not gray at "+x+","+y+".");
							}
							arrayGrays[y*bitmap32.Width+x]=arrayBytes[i+2];
						}
					}
					return arrayGrays;
				}
				finally {
					bitmap32.UnlockBits(bitmapData);
				}
			}
		}

		///<summary>A sensible image uses the gray range: near black and near white are both there, with many levels in between, and most pixels
		///are neither clipped to black nor to white.</summary>
		private static void AssertUsesTheGrayRange(byte[] arrayGrays,int minDistinctLevels,string what) {
			int min=arrayGrays.Min(x => (int)x);
			int max=arrayGrays.Max(x => (int)x);
			int countDistinct=arrayGrays.Distinct().Count();
			double mean=arrayGrays.Average(x => (double)x);
			double standardDeviation=Math.Sqrt(arrayGrays.Average(x => (x-mean)*(x-mean)));
			double fractionClipped=arrayGrays.Count(x => x==0 || x==255)/(double)arrayGrays.Length;
			string summary=what+": min "+min+", max "+max+", "+countDistinct+" levels, mean "+mean.ToString("F1")+", sd "+standardDeviation.ToString("F1")
				+", clipped "+(fractionClipped*100).ToString("F1")+"%";
			Assert.IsTrue(min<=8,"Not dark enough. "+summary);
			Assert.IsTrue(max>=247,"Not bright enough. "+summary);
			Assert.IsTrue(countDistinct>=minDistinctLevels,"Too few gray levels: flat or posterized. "+summary);
			Assert.IsTrue(standardDeviation>=30,"Flat. "+summary);
			Assert.IsTrue(mean>=25 && mean<=200,"Mostly black or mostly white. "+summary);
			Assert.IsTrue(fractionClipped<0.10,"Too much clipped to black or white. "+summary);
		}

		[TestMethod]
		public void PanoramicExport_14BitsStored_IsShownWithTheFullGrayRange_Everywhere() {
			ushort[] arraySamples=MakePanoramicSamples();
			byte[] arrayFile=MakePanoramicFile(arraySamples);
			string path=Path.Combine(_patFolder,"pano.dcm");
			File.WriteAllBytes(path,arrayFile);
			//The reader: the file is read, every sample at 16 bits (14 bits moved up by 2, as the reader documents), nothing clipped or lost.
			DicomImageReadResult result=DicomHelper.ReadFileResult(path);
			Assert.AreEqual(DicomImageReadStatus.Read,result.Status,result.ToString());
			BitmapDicom bitmapDicom=DicomHelper.GetFromFile(path);
			Assert.IsNotNull(bitmapDicom);
			Assert.AreEqual(WIDTH,bitmapDicom.Width);
			Assert.AreEqual(HEIGHT,bitmapDicom.Height);
			Assert.AreEqual(16,bitmapDicom.BitsAllocated);
			Assert.AreEqual(16,bitmapDicom.BitsStored,"14 bits stored are shown on the 16 bit scale.");
			Assert.AreEqual(EnumDicomPhotometricInterp.MONOCHROME2,bitmapDicom.PhotometricInterp);
			for(int i=0;i<arraySamples.Length;i+=997) {
				Assert.AreEqual(arraySamples[i]<<2,bitmapDicom.ArrayDataRaw[i],"sample "+i);
			}
			Assert.AreEqual(MAX_14BIT<<2,bitmapDicom.ArrayDataRaw.Max(x => (int)x),"The metal filling is the top of the range.");
			Assert.AreEqual(0,bitmapDicom.ArrayDataRaw.Min(x => (int)x),"The unexposed edge is the bottom.");
			//ImageStore's initial window on import: the whole range, the same window the file's own Window Center 8192 and Width 16384 give.
			Document document=new Document { FileName="pano.dcm",DateTStamp=DateTime.Now.AddDays(-1) };
			BitmapDicom bitmapDicomImport=ImageStore.SetDicomImportFields(document,path);
			Assert.IsNotNull(bitmapDicomImport);
			Assert.AreEqual(ImageType.Radiograph,document.ImgType);
			Assert.AreEqual(0,document.WindowingMin);
			Assert.AreEqual(255,document.WindowingMax);
			//DicomHelper.ApplyWindowing with that window: a sensible image, the full gray range used.
			using(Bitmap bitmap=DicomHelper.ApplyWindowing(bitmapDicom,document.WindowingMin,document.WindowingMax)) {
				Assert.AreEqual(WIDTH,bitmap.Width);
				Assert.AreEqual(HEIGHT,bitmap.Height);
				byte[] arrayGrays=GetGrays(bitmap);
				AssertUsesTheGrayRange(arrayGrays,200,"ApplyWindowing");
				Assert.AreEqual(0,arrayGrays[10*WIDTH+10],"The unexposed edge is black.");
				Assert.AreEqual(255,arrayGrays[500*WIDTH+930],"The metal filling is white.");
				//The arch of teeth is brighter than the air above it.
				Assert.IsTrue(arrayGrays[(int)(0.38*(HEIGHT-1))*WIDTH+WIDTH/2]>arrayGrays[40*WIDTH+WIDTH/2]+80,"Teeth brighter than air.");
			}
			//A narrower window still works on this image: the lower half of the range stretched over the gray scale.
			using(Bitmap bitmap=DicomHelper.ApplyWindowing(bitmapDicom,0,128)) {
				byte[] arrayGrays=GetGrays(bitmap);
				Assert.IsTrue(arrayGrays.Distinct().Count()>=150,"The narrow window keeps many levels.");
			}
			//The Imaging module's image (ImageHelper.GetImageCropped) with the imported window.
			using(Bitmap bitmap=ImageHelper.GetImageCropped(document,_patFolder)) {
				Assert.AreEqual(WIDTH,bitmap.Width);
				Assert.AreEqual(HEIGHT,bitmap.Height);
				AssertUsesTheGrayRange(GetGrays(bitmap),200,"GetImageCropped");
			}
			//The thumbnail: a real one, with the full range too (the image fills the width, centered in 100 x 100).
			using(Bitmap bitmapNotAvailable=Documents.NoAvailablePhoto())
			using(Bitmap bitmapThumb=Documents.GetThumbnail(document,_patFolder)) {
				Assert.AreEqual(100,bitmapThumb.Width);
				Assert.AreEqual(100,bitmapThumb.Height);
				byte[] arrayThumb=GetGrays(bitmapThumb);
				//The image is 100 x 46 in the middle of the thumbnail; the rows above and below are the thumbnail's background.
				int rowsImage=(int)Math.Round(100.0*HEIGHT/WIDTH);
				int rowTop=(100-rowsImage)/2+1;
				byte[] arrayImageRows=arrayThumb.Skip(rowTop*100).Take((rowsImage-2)*100).ToArray();
				int max=arrayImageRows.Max(x => (int)x);
				int min=arrayImageRows.Min(x => (int)x);
				//Scaled down, the unexposed edge and the filling are blended with their surroundings, so the ends are a little inside 0 and 255.
				Assert.IsTrue(min<=30 && max>=220,"Thumbnail range: min "+min+", max "+max);
				Assert.IsTrue(arrayImageRows.Distinct().Count()>=60,"Thumbnail levels: "+arrayImageRows.Distinct().Count());
			}
		}
	}
}
