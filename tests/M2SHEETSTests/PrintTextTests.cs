// Printing and screen layout of sheet text: SheetTextDrawing.DrawString (the print path) and ApplyDirection (screen, designer and print).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2SHEETSTests {
	[TestClass]
	public class PrintTextTests {
		private const int WIDTH=600;
		private const int HEIGHT=60;

		[TestInitialize]
		public void TestInitialize() {
			Samples.UseUsEnglish();
			Samples.RequireFonts("Arial");
		}

		[TestMethod]
		public void Direction_RightToLeftKeepsTheDesignedSide() {
			using(StringFormat stringFormat=new StringFormat()) {
				stringFormat.Alignment=StringAlignment.Near;
				SheetTextDrawing.ApplyDirection(stringFormat,Samples.Hebrew);
				Assert.IsTrue(stringFormat.FormatFlags.HasFlag(StringFormatFlags.DirectionRightToLeft));
				Assert.AreEqual(StringAlignment.Far,stringFormat.Alignment,"Right-to-left far is the left side, where the designer put it.");
			}
			using(StringFormat stringFormat=new StringFormat()) {
				stringFormat.Alignment=StringAlignment.Far;
				SheetTextDrawing.ApplyDirection(stringFormat,Samples.Arabic);
				Assert.AreEqual(StringAlignment.Near,stringFormat.Alignment);
			}
			using(StringFormat stringFormat=new StringFormat()) {
				stringFormat.Alignment=StringAlignment.Center;
				SheetTextDrawing.ApplyDirection(stringFormat,Samples.Hebrew);
				Assert.AreEqual(StringAlignment.Center,stringFormat.Alignment);
			}
			foreach(string text in new[] { Samples.LatinWinAnsi,Samples.Russian,"Name: "+Samples.Hebrew,"" }) {
				using(StringFormat stringFormat=new StringFormat()) {
					SheetTextDrawing.ApplyDirection(stringFormat,text);
					Assert.IsFalse(stringFormat.FormatFlags.HasFlag(StringFormatFlags.DirectionRightToLeft),text);
					Assert.AreEqual(StringAlignment.Near,stringFormat.Alignment,text);
				}
			}
			SheetTextDrawing.ApplyDirection(null,Samples.Hebrew);//no error
		}

		[TestMethod]
		public void Print_LatinTextIsUnchanged() {
			foreach(HorizontalAlignment horizontalAlignment in new[] { HorizontalAlignment.Left,HorizontalAlignment.Center,HorizontalAlignment.Right }) {
				RectangleF rectangleOld=RectangleF.Empty;
				RectangleF rectangleNew=RectangleF.Empty;
				byte[] arrayOld=Render((g,font,rectangle) => rectangleOld=GraphicsHelper.DrawString(g,Samples.LatinWinAnsi,font,Brushes.Black,rectangle,horizontalAlignment));
				byte[] arrayNew=Render((g,font,rectangle) => rectangleNew=SheetTextDrawing.DrawString(g,Samples.LatinWinAnsi,font,Brushes.Black,rectangle,horizontalAlignment));
				CollectionAssert.AreEqual(arrayOld,arrayNew,horizontalAlignment.ToString());
				Assert.AreEqual(rectangleOld,rectangleNew);
			}
		}

		[TestMethod]
		public void Print_RightToLeftStaysOnTheDesignedSide() {
			byte[] arrayLeft=Render((g,font,rectangle) => SheetTextDrawing.DrawString(g,Samples.Hebrew,font,Brushes.Black,rectangle,HorizontalAlignment.Left));
			byte[] arrayRight=Render((g,font,rectangle) => SheetTextDrawing.DrawString(g,Samples.Hebrew,font,Brushes.Black,rectangle,HorizontalAlignment.Right));
			GetInkColumns(arrayLeft,out int leftOfLeft,out int rightOfLeft);
			GetInkColumns(arrayRight,out int leftOfRight,out int rightOfRight);
			Assert.IsTrue(leftOfLeft>=0 && leftOfLeft<20,"Left-aligned Hebrew starts at the left edge: "+leftOfLeft);
			Assert.IsTrue(rightOfRight>WIDTH*0.8,"Right-aligned Hebrew ends at the right edge: "+rightOfRight);
		}

		[TestMethod]
		public void Print_NonLatinTextDraws() {
			foreach(string text in new[] { Samples.Chinese,Samples.Korean,Samples.Hindi,Samples.Russian,Samples.Arabic }) {
				byte[] array=Render((g,font,rectangle) => SheetTextDrawing.DrawString(g,text,font,Brushes.Black,rectangle,HorizontalAlignment.Left));
				GetInkColumns(array,out int left,out int right);
				Assert.IsTrue(left>=0 && right>left,text);
			}
			Assert.AreEqual(new RectangleF(1,2,3,4),SheetTextDrawing.DrawString(null,"  ",new Font("Arial",11f),Brushes.Black,new Rectangle(1,2,3,4),HorizontalAlignment.Left),
				"Blank text draws nothing, as before.");
		}

		///<summary>Draws onto a white 100 dpi bitmap (sheet pixels) with Arial 11 in a WIDTH by HEIGHT field; returns the pixels.</summary>
		private static byte[] Render(Action<Graphics,Font,Rectangle> actionDraw) {
			using(Bitmap bitmap=new Bitmap(WIDTH+40,HEIGHT+20,PixelFormat.Format32bppArgb)) {
				bitmap.SetResolution(100,100);
				using(Graphics g=Graphics.FromImage(bitmap))
				using(Font font=new Font("Arial",11f)) {
					g.Clear(Color.White);
					actionDraw(g,font,new Rectangle(0,0,WIDTH,HEIGHT));
				}
				BitmapData bitmapData=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
				byte[] array=new byte[bitmapData.Stride*bitmap.Height];
				System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0,array,0,array.Length);
				bitmap.UnlockBits(bitmapData);
				return array;
			}
		}

		///<summary>The leftmost and rightmost columns with dark pixels, or -1 when nothing was drawn.</summary>
		private static void GetInkColumns(byte[] array,out int left,out int right) {
			int width=WIDTH+40;
			int height=HEIGHT+20;
			left=-1;
			right=-1;
			for(int x=0;x<width;x++) {
				for(int y=0;y<height;y++) {
					int i=(y*width+x)*4;
					if(array[i]<128 && array[i+1]<128 && array[i+2]<128) {
						if(left<0) {
							left=x;
						}
						right=x;
						break;
					}
				}
			}
		}
	}
}
