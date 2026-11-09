// Compares chart images by the rules of docs/specs/wpf-tooth-chart-plan.md section 7 ("Tolerances"), and describes a bitmap for the recordings.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ToothChartImageTests {
	///<summary>The result of comparing two chart images.</summary>
	public class ChartImageCompareResult {
		public bool IsSameSize;
		public int CountPixels;
		///<summary>Pixels where a channel (B, G, R or A) differs by more than ChannelTolerance.</summary>
		public int CountDifferent;
		///<summary>The most different pixels in one BlockSizexBlockSize block, and where that block starts.</summary>
		public int CountDifferentInWorstBlock;
		public Point PointWorstBlock;

		///<summary>True when at most 0.1% of the pixels differ and no block has more than MaxDifferentInBlock different pixels.</summary>
		public bool IsPass {
			get {
				if(!IsSameSize) {
					return false;
				}
				return CountDifferent<=CountPixels*ChartImageCompare.MaxFractionDifferent
					&& CountDifferentInWorstBlock<=ChartImageCompare.MaxDifferentInBlock;
			}
		}

		public override string ToString() {
			if(!IsSameSize) {
				return "The images differ in size.";
			}
			return CountDifferent+" of "+CountPixels+" pixels differ by more than "+ChartImageCompare.ChannelTolerance+"; the worst "
				+ChartImageCompare.BlockSize+"x"+ChartImageCompare.BlockSize+" block, at "+PointWorstBlock.X+","+PointWorstBlock.Y+", has "
				+CountDifferentInWorstBlock+".";
		}
	}

	///<summary>Image comparison for the chart tests. A pixel counts as different when a channel differs by more than 8 of 255. A comparison passes
	///when at most 0.1% of the pixels differ and no 16x16 block has more than 24 different pixels; the block rule catches a small local error,
	///such as a missing "S". Images are compared as 32-bit ARGB whatever their own pixel format. The legacy recordings are compared byte for byte
	///(AreFilesIdentical) and by their pixel SHA-256, because the re-recording after a refactor on the same PC and framework must give the same
	///files; across frameworks only the pixel SHA-256 (LegacyComparison, PngWriter).</summary>
	public static class ChartImageCompare {
		public const int ChannelTolerance=8;
		public const double MaxFractionDifferent=0.001;
		public const int BlockSize=16;
		public const int MaxDifferentInBlock=24;

		public static ChartImageCompareResult Compare(Bitmap bitmapExpected,Bitmap bitmapActual) {
			ChartImageCompareResult result=new ChartImageCompareResult();
			result.IsSameSize=bitmapExpected.Size==bitmapActual.Size;
			if(!result.IsSameSize) {
				return result;
			}
			int width=bitmapExpected.Width;
			int height=bitmapExpected.Height;
			result.CountPixels=width*height;
			bool[] arrayDifferent=GetDifferentPixels(bitmapExpected,bitmapActual);
			result.CountDifferent=arrayDifferent.Count(x => x);
			for(int yBlock=0;yBlock<height;yBlock+=BlockSize) {
				for(int xBlock=0;xBlock<width;xBlock+=BlockSize) {
					int countInBlock=0;
					for(int y=yBlock;y<Math.Min(yBlock+BlockSize,height);y++) {
						for(int x=xBlock;x<Math.Min(xBlock+BlockSize,width);x++) {
							if(arrayDifferent[y*width+x]) {
								countInBlock++;
							}
						}
					}
					if(countInBlock>result.CountDifferentInWorstBlock) {
						result.CountDifferentInWorstBlock=countInBlock;
						result.PointWorstBlock=new Point(xBlock,yBlock);
					}
				}
			}
			return result;
		}

		///<summary>One flag per pixel, row by row: true where a channel differs by more than ChannelTolerance. Both bitmaps must be the same size.</summary>
		public static bool[] GetDifferentPixels(Bitmap bitmapExpected,Bitmap bitmapActual) {
			byte[] arrayExpected=GetPixelsArgb(bitmapExpected);
			byte[] arrayActual=GetPixelsArgb(bitmapActual);
			bool[] arrayDifferent=new bool[arrayExpected.Length/4];
			for(int i=0;i<arrayDifferent.Length;i++) {
				for(int c=0;c<4;c++) {
					if(Math.Abs(arrayExpected[i*4+c]-arrayActual[i*4+c])>ChannelTolerance) {
						arrayDifferent[i]=true;
						break;
					}
				}
			}
			return arrayDifferent;
		}

		///<summary>The pixels as 32-bit ARGB, row by row with no row padding: B, G, R and A for each pixel. Converts from any pixel format.</summary>
		public static byte[] GetPixelsArgb(Bitmap bitmap) {
			int width=bitmap.Width;
			int height=bitmap.Height;
			byte[] arrayPixels=new byte[width*height*4];
			BitmapData bitmapData=bitmap.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
			try {
				for(int y=0;y<height;y++) {
					Marshal.Copy(bitmapData.Scan0+y*bitmapData.Stride,arrayPixels,y*width*4,width*4);
				}
			}
			finally {
				bitmap.UnlockBits(bitmapData);
			}
			return arrayPixels;
		}

		///<summary>A copy of bitmapExpected in grey with the different pixels in red, for a failing test.</summary>
		public static Bitmap MakeDiffImage(Bitmap bitmapExpected,Bitmap bitmapActual) {
			int width=bitmapExpected.Width;
			int height=bitmapExpected.Height;
			byte[] arrayExpected=GetPixelsArgb(bitmapExpected);
			bool[] arrayDifferent=GetDifferentPixels(bitmapExpected,bitmapActual);
			byte[] arrayDiff=new byte[arrayExpected.Length];
			for(int i=0;i<arrayDifferent.Length;i++) {
				if(arrayDifferent[i]) {
					arrayDiff[i*4]=0;
					arrayDiff[i*4+1]=0;
					arrayDiff[i*4+2]=255;
				}
				else {
					//Grey at half contrast, so the red stands out.
					byte grey=(byte)(128+(arrayExpected[i*4]*114+arrayExpected[i*4+1]*587+arrayExpected[i*4+2]*299)/1000/2);
					arrayDiff[i*4]=grey;
					arrayDiff[i*4+1]=grey;
					arrayDiff[i*4+2]=grey;
				}
				arrayDiff[i*4+3]=255;
			}
			Bitmap bitmapDiff=new Bitmap(width,height,PixelFormat.Format32bppArgb);
			BitmapData bitmapData=bitmapDiff.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
			try {
				for(int y=0;y<height;y++) {
					Marshal.Copy(arrayDiff,y*width*4,bitmapData.Scan0+y*bitmapData.Stride,width*4);
				}
			}
			finally {
				bitmapDiff.UnlockBits(bitmapData);
			}
			return bitmapDiff;
		}

		///<summary>Writes name.expected.png, name.actual.png and name.diff.png to folder, for a failing comparison. Returns the diff file's path.</summary>
		public static string WriteFailureImages(string folder,string name,Bitmap bitmapExpected,Bitmap bitmapActual) {
			Directory.CreateDirectory(folder);
			PngWriter.Save(bitmapExpected,Path.Combine(folder,name+".expected.png"));
			PngWriter.Save(bitmapActual,Path.Combine(folder,name+".actual.png"));
			string pathDiff=Path.Combine(folder,name+".diff.png");
			if(bitmapExpected.Size==bitmapActual.Size) {
				using Bitmap bitmapDiff=MakeDiffImage(bitmapExpected,bitmapActual);
				PngWriter.Save(bitmapDiff,pathDiff);
			}
			return pathDiff;
		}

		///<summary>True when both files exist and have the same bytes.</summary>
		public static bool AreFilesIdentical(string path1,string path2) {
			if(!File.Exists(path1) || !File.Exists(path2)) {
				return false;
			}
			byte[] array1=File.ReadAllBytes(path1);
			byte[] array2=File.ReadAllBytes(path2);
			return array1.SequenceEqual(array2);
		}

		///<summary>Lower-case hex SHA-256 of the bytes.</summary>
		public static string Sha256(byte[] arrayBytes) {
			using SHA256 sha256=SHA256.Create();
			return string.Concat(sha256.ComputeHash(arrayBytes).Select(x => x.ToString("x2")));
		}

		///<summary>What the recordings say about a returned bitmap: its size and PixelFormat, whether every alpha is 255, how many colours it has and
		///the SHA-256 of its pixels as 32-bit ARGB.</summary>
		public static BitmapFacts DescribeBitmap(Bitmap bitmap) {
			BitmapFacts bitmapFacts=new BitmapFacts();
			bitmapFacts.Size=bitmap.Size;
			bitmapFacts.PixelFormat=bitmap.PixelFormat;
			bitmapFacts.HasAlphaChannel=Image.IsAlphaPixelFormat(bitmap.PixelFormat);
			byte[] arrayPixels=GetPixelsArgb(bitmap);
			bitmapFacts.AlphaMin=255;
			HashSet<int> hashSetColors=new HashSet<int>();
			for(int i=0;i<arrayPixels.Length;i+=4) {
				byte alpha=arrayPixels[i+3];
				if(alpha<255) {
					bitmapFacts.CountAlphaBelow255++;
				}
				if(alpha<bitmapFacts.AlphaMin) {
					bitmapFacts.AlphaMin=alpha;
				}
				hashSetColors.Add(BitConverter.ToInt32(arrayPixels,i));
			}
			bitmapFacts.CountColors=hashSetColors.Count;
			bitmapFacts.PixelsSha256=Sha256(arrayPixels);
			return bitmapFacts;
		}
	}

	///<summary>Facts about one returned chart bitmap. See ChartImageCompare.DescribeBitmap.</summary>
	public class BitmapFacts {
		public Size Size;
		public PixelFormat PixelFormat;
		public bool HasAlphaChannel;
		public int CountAlphaBelow255;
		public byte AlphaMin;
		public int CountColors;
		public string PixelsSha256;

		///<summary>True when every pixel has alpha 255, which is always so for a format without an alpha channel.</summary>
		public bool IsOpaque {
			get {
				return CountAlphaBelow255==0;
			}
		}

		///<summary>A picture of one colour is blank: the chart drew nothing, or a read-back gave nothing.</summary>
		public bool IsBlank {
			get {
				return CountColors<=1;
			}
		}

		///<summary>"every alpha is 255 (format without an alpha channel)", "every alpha is 255" or "1234 pixels have alpha below 255 (lowest 0)".</summary>
		public string GetAlphaText() {
			if(!HasAlphaChannel) {
				return "every alpha is 255 (format without an alpha channel)";
			}
			if(IsOpaque) {
				return "every alpha is 255";
			}
			return CountAlphaBelow255+" pixels have alpha below 255 (lowest "+AlphaMin+")";
		}
	}
}
