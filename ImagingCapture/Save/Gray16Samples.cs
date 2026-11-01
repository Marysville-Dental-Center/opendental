// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;

namespace ImagingCapture.Save {
	///<summary>Pure operations on 16-bit samples (AcquiredImage.Gray16: raw, right-justified, rows top to bottom) for the saver and Test
	///capture: the lossless inversion of [Device.N] Polarity=Invert, the stored 0-255 window, and the fixed preview LUT.</summary>
	public static class Gray16Samples {
		///<summary>The lossless inversion Polarity=Invert applies, only once it has been verified and logged (design 7.8: "Stored pixels change
		///only through a documented lossless inversion"). Zero stays zero, because zero-valued pixels are the masked corners and dead areas the
		///window excludes; every other value v becomes max+1-v, which maps 1..max onto max..1, so it can be undone exactly. max is
		///2^effectiveBits-1 when the source reports effectiveBits (1 to 16) and no sample exceeds it, so data that fits 12 bits still fits
		///BitsStored 12 after the inversion; otherwise 65535. Returns a new array, or null for null.</summary>
		public static ushort[] Invert(ushort[] pixels,int? effectiveBits) {
			if(pixels==null) {
				return null;
			}
			int max=GetInversionMax(pixels,effectiveBits);
			ushort[] arrayInverted=new ushort[pixels.Length];
			for(int i=0;i<pixels.Length;i++) {
				arrayInverted[i]=pixels[i]==0 ? (ushort)0 : (ushort)(max+1-pixels[i]);
			}
			return arrayInverted;
		}

		///<summary>The max Invert uses: 2^effectiveBits-1 when effectiveBits is 1 to 16 and no sample exceeds it, else 65535.</summary>
		public static int GetInversionMax(ushort[] pixels,int? effectiveBits) {
			if(effectiveBits==null || effectiveBits.Value<1 || effectiveBits.Value>16 || pixels==null) {
				return 65535;
			}
			int max=(1<<effectiveBits.Value)-1;
			for(int i=0;i<pixels.Length;i++) {
				if(pixels[i]>max) {
					return 65535;
				}
			}
			return max;
		}

		///<summary>Document.WindowingMin and WindowingMax for a DICOM capture, on the 0-255 scale of the CAP-W reading rule (design 7.8): the raw
		///window divided by 16 for BitsStored 12 and by 256 for BitsStored 16, the low end rounded down and the high end up, so the stored
		///window covers the raw one. Both stay within 0-255 and max is above min.</summary>
		public static void GetStoredWindow(int windowLow,int windowHigh,int bitsStored,out int windowingMin,out int windowingMax) {
			int scale=bitsStored==12 ? 16 : 256;
			if(windowHigh<windowLow) {
				windowHigh=windowLow;
			}
			windowingMin=Clamp(Math.Max(0,windowLow)/scale);
			windowingMax=Clamp((Math.Max(0,windowHigh)+scale-1)/scale);
			if(windowingMax<=windowingMin) {
				if(windowingMin>=255) {
					windowingMin=254;
					windowingMax=255;
				}
				else {
					windowingMax=windowingMin+1;
				}
			}
		}

		///<summary>An 8-bit picture of the samples through a fixed LUT built from the window (design 7.1: the capture API's preview never shifts):
		///values at or below low are black, at or above high white, and linear in between. Rows top to bottom, one byte per pixel.</summary>
		public static byte[] ToPreview(ushort[] pixels,int low,int high) {
			if(pixels==null) {
				return null;
			}
			if(high<=low) {
				high=low+1;
			}
			byte[] arrayLut=new byte[65536];
			for(int v=0;v<65536;v++) {
				if(v<=low) {
					arrayLut[v]=0;
				}
				else if(v>=high) {
					arrayLut[v]=255;
				}
				else {
					arrayLut[v]=(byte)Math.Round(255.0*(v-low)/(high-low));
				}
			}
			byte[] arrayPreview=new byte[pixels.Length];
			for(int i=0;i<pixels.Length;i++) {
				arrayPreview[i]=arrayLut[pixels[i]];
			}
			return arrayPreview;
		}

		///<summary>Each sample with its two bytes swapped: what the image would be in the other byte order.</summary>
		public static ushort[] SwapBytes(ushort[] pixels) {
			if(pixels==null) {
				return null;
			}
			ushort[] arraySwapped=new ushort[pixels.Length];
			for(int i=0;i<pixels.Length;i++) {
				arraySwapped[i]=(ushort)((pixels[i]>>8)|((pixels[i]&0xFF)<<8));
			}
			return arraySwapped;
		}

		private static int Clamp(int value) {
			return Math.Max(0,Math.Min(255,value));
		}
	}
}
