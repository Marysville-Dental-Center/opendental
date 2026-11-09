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
						if(!Native.PrintWindow(hwnd,hdc,Native.PW_RENDERFULLCONTENT)) {
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
