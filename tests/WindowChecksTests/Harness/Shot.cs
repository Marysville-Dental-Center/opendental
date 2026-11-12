// Screenshots without the screen: a window is printed into a bitmap with PrintWindow (PW_RENDERFULLCONTENT, which also renders DirectX and
// WPF content), a WinForms control with DrawToBitmap and a WPF visual with RenderTargetBitmap. Each picture is saved as PNG under the
// checklist's file name in the results folder, and measured, so a check can tell a drawn window from a blank one.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace WindowChecksTests {
	[Serializable]
	public class ShotInfo {
		public string FileName="";
		public int Width;
		public int Height;
		///<summary>Distinct colours on a sampling grid: a blank (one-colour) picture has 1 or 2.</summary>
		public int CountColors;
		///<summary>The share (0 to 1) of sampled pixels that differ from the most common colour.</summary>
		public double ShareNonBackground;
		public string Error="";

		public bool IsDrawn {
			get {
				return Error=="" && Width>0 && Height>0 && CountColors>5;
			}
		}

		public override string ToString() {
			if(Error!="") {
				return FileName+" (not taken: "+Error+")";
			}
			return FileName+" ("+Width+"x"+Height+", "+CountColors+" colours, "+Math.Round(ShareNonBackground*100)+"% non-background)";
		}
	}

	public static class Shot {
		///<summary>Prints the window (its whole rectangle, borders included) and saves it; null fileName: measured only.</summary>
		public static ShotInfo Window(IntPtr hwnd,string fileName) {
			ShotInfo shotInfo=new ShotInfo { FileName=fileName??"" };
			try {
				Rectangle rectangle=Native.GetRect(hwnd);
				if(rectangle.Width<=0 || rectangle.Height<=0) {
					shotInfo.Error="the window has no size";
					return shotInfo;
				}
				using Bitmap bitmap=new Bitmap(rectangle.Width,rectangle.Height,PixelFormat.Format32bppArgb);
				using(Graphics g=Graphics.FromImage(bitmap)) {
					g.Clear(Color.Magenta);
					IntPtr hdc=g.GetHdc();
					try {
						//PrintWindow waits for the window's thread; a hung window must not hang the check.
						System.Threading.Tasks.Task<bool> task=System.Threading.Tasks.Task.Run(() => Native.PrintWindow(hwnd,hdc,Native.PW_RENDERFULLCONTENT));
						if(!task.Wait(20000)) {
							shotInfo.Error="PrintWindow did not return within 20 s (the window does not answer)";
						}
						else if(!task.Result) {
							shotInfo.Error="PrintWindow failed";
						}
					}
					finally {
						g.ReleaseHdc(hdc);
					}
				}
				Save(bitmap,shotInfo);
			}
			catch(Exception ex) {
				shotInfo.Error=ex.GetType().Name+": "+ex.Message;
			}
			return shotInfo;
		}

		///<summary>Saves an image someone else drew (DrawToBitmap, RenderTargetBitmap converted) and measures it.</summary>
		public static ShotInfo Bitmap(Bitmap bitmap,string fileName) {
			ShotInfo shotInfo=new ShotInfo { FileName=fileName??"" };
			try {
				Save(bitmap,shotInfo);
			}
			catch(Exception ex) {
				shotInfo.Error=ex.GetType().Name+": "+ex.Message;
			}
			return shotInfo;
		}

		///<summary>Measures a part of a saved picture (for example the chart's area of the main window): distinct colours on a grid.</summary>
		public static int CountColorsIn(string fileName,Rectangle rectangle) {
			using Bitmap bitmap=new Bitmap(Path.Combine(WinChkEnv.OutFolder,fileName));
			rectangle.Intersect(new Rectangle(0,0,bitmap.Width,bitmap.Height));
			HashSet<int> hashSet=new HashSet<int>();
			for(int x=rectangle.Left;x<rectangle.Right;x+=3) {
				for(int y=rectangle.Top;y<rectangle.Bottom;y+=3) {
					hashSet.Add(bitmap.GetPixel(x,y).ToArgb());
				}
			}
			return hashSet.Count;
		}

		///<summary>The share (0 to 1) of pixels that differ between two saved pictures of the same size, or -1 when either is missing or the sizes
		///differ (the text says which).</summary>
		public static double CompareFiles(string fileNameA,string fileNameB,out string text) {
			string pathA=Path.Combine(WinChkEnv.OutFolder,fileNameA);
			string pathB=Path.Combine(WinChkEnv.OutFolder,fileNameB);
			if(!File.Exists(pathA) || !File.Exists(pathB)) {
				text="missing: "+(File.Exists(pathA) ? fileNameB : fileNameA);
				return -1;
			}
			using Bitmap bitmapA=new Bitmap(pathA);
			using Bitmap bitmapB=new Bitmap(pathB);
			if(bitmapA.Size!=bitmapB.Size) {
				text="sizes differ: "+bitmapA.Width+"x"+bitmapA.Height+" and "+bitmapB.Width+"x"+bitmapB.Height;
				return -1;
			}
			long countDiffer=0;
			for(int x=0;x<bitmapA.Width;x++) {
				for(int y=0;y<bitmapA.Height;y++) {
					if(bitmapA.GetPixel(x,y).ToArgb()!=bitmapB.GetPixel(x,y).ToArgb()) {
						countDiffer++;
					}
				}
			}
			double share=(double)countDiffer/((long)bitmapA.Width*bitmapA.Height);
			text=bitmapA.Width+"x"+bitmapA.Height+", "+countDiffer+" pixels differ ("+(share*100).ToString("0.00")+"%)";
			return share;
		}

		private static void Save(Bitmap bitmap,ShotInfo shotInfo) {
			shotInfo.Width=bitmap.Width;
			shotInfo.Height=bitmap.Height;
			Dictionary<int,int> dictionaryCounts=new Dictionary<int,int>();
			int countSamples=0;
			int step=Math.Max(1,Math.Min(bitmap.Width,bitmap.Height)/120);
			for(int x=0;x<bitmap.Width;x+=step) {
				for(int y=0;y<bitmap.Height;y+=step) {
					int argb=bitmap.GetPixel(x,y).ToArgb();
					dictionaryCounts.TryGetValue(argb,out int count);
					dictionaryCounts[argb]=count+1;
					countSamples++;
				}
			}
			int countMost=0;
			foreach(int count in dictionaryCounts.Values) {
				countMost=Math.Max(countMost,count);
			}
			shotInfo.CountColors=dictionaryCounts.Count;
			shotInfo.ShareNonBackground=countSamples==0 ? 0 : 1-(double)countMost/countSamples;
			if(!string.IsNullOrEmpty(shotInfo.FileName)) {
				bitmap.Save(Path.Combine(WinChkEnv.OutFolder,shotInfo.FileName),ImageFormat.Png);
			}
		}
	}
}
