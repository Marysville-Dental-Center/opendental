// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ImagingCapture.Uvc {
	///<summary>One video frame in managed memory: top-down rows of 4 bytes per pixel in the order blue, green, red, 0xFF, with no
	///padding (Stride is Width*4). The same layout as a 32bpp GDI+ bitmap.</summary>
	public sealed class UvcFrame {
		public int Width { get; }
		public int Height { get; }
		///<summary>Bytes per row: Width*4.</summary>
		public int Stride { get; }
		///<summary>Stride*Height bytes.</summary>
		public byte[] Pixels { get; }
		///<summary>Numbers the frames of one stream from 1, in the order they arrived. Still samples have their own numbering.</summary>
		public long Sequence { get; internal set; }
		///<summary>The sample's stream time in seconds, as the graph reported it.</summary>
		public double SampleTime { get; internal set; }

		public UvcFrame(int width,int height) {
			if(width<=0 || height<=0) {
				throw new ArgumentOutOfRangeException(nameof(width),"Frame size must be positive.");
			}
			Width=width;
			Height=height;
			Stride=checked(width*4);
			Pixels=new byte[checked(Stride*height)];
		}

		///<summary>A new bitmap with a copy of the pixels (Format32bppRgb). The caller disposes it. The bitmap shares no memory with
		///this frame, so it can be handed to another thread or kept after the frame is reused.</summary>
		public Bitmap ToBitmap() {
			Bitmap bitmap=new Bitmap(Width,Height,PixelFormat.Format32bppRgb);
			BitmapData bitmapData=null;
			try {
				bitmapData=bitmap.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppRgb);
				if(bitmapData.Stride==Stride) {
					Marshal.Copy(Pixels,0,bitmapData.Scan0,Pixels.Length);
				}
				else {
					for(int y=0;y<Height;y++) {
						Marshal.Copy(Pixels,y*Stride,IntPtr.Add(bitmapData.Scan0,y*bitmapData.Stride),Stride);
					}
				}
			}
			catch {
				if(bitmapData!=null) {
					bitmap.UnlockBits(bitmapData);
					bitmapData=null;
				}
				bitmap.Dispose();
				throw;
			}
			finally {
				if(bitmapData!=null) {
					bitmap.UnlockBits(bitmapData);
				}
			}
			return bitmap;
		}

		///<summary>An independent copy.</summary>
		public UvcFrame Clone() {
			UvcFrame frame=new UvcFrame(Width,Height);
			Buffer.BlockCopy(Pixels,0,frame.Pixels,0,Pixels.Length);
			frame.Sequence=Sequence;
			frame.SampleTime=SampleTime;
			return frame;
		}
	}
}
