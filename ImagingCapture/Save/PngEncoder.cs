// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImagingCapture.Save {
	///<summary>Lossless PNG through WIC (the WPF PNG encoder): 8-bit greyscale for radiographs (design 7.8 "Until then": "lossless 8-bit
	///grayscale PNG encoded with WIC", about four times smaller than the 32bpp TIFF of the legacy loop), and 24-bit color for an RGB image.
	///The resolution goes into the pHYs chunk.</summary>
	public static class PngEncoder {
		///<summary>The resolution written when the image has none, as GDI+ and WPF assume.</summary>
		public const double DpiDefault=96;

		///<summary>A PNG of decodedImage: greyscale 8-bit when IsGray, else 24-bit color. dpiX and dpiY of 0 or less use the image's own, or
		///DpiDefault.</summary>
		public static byte[] Encode(DecodedImage decodedImage,double dpiX=0,double dpiY=0) {
			if(decodedImage==null) {
				throw new ArgumentNullException(nameof(decodedImage));
			}
			return Encode(decodedImage.Width,decodedImage.Height,decodedImage.Pixels,decodedImage.IsGray,
				dpiX>0 ? dpiX : decodedImage.XDpi,dpiY>0 ? dpiY : decodedImage.YDpi);
		}

		///<summary>A PNG of pixels: top-down rows without padding, one grey byte per pixel when isGray, else three (B, G, R).</summary>
		public static byte[] Encode(int width,int height,byte[] pixels,bool isGray,double dpiX,double dpiY) {
			if(width<=0 || height<=0) {
				throw new ArgumentException("The image has no size.");
			}
			int stride=isGray ? width : width*3;
			if(pixels==null || pixels.LongLength<(long)stride*height) {
				throw new ArgumentException("The pixels do not fill the image.");
			}
			PixelFormat pixelFormat=isGray ? PixelFormats.Gray8 : PixelFormats.Bgr24;
			BitmapSource bitmapSource=BitmapSource.Create(width,height,GetDpi(dpiX),GetDpi(dpiY),pixelFormat,null,pixels,stride);
			PngBitmapEncoder pngBitmapEncoder=new PngBitmapEncoder();
			pngBitmapEncoder.Interlace=PngInterlaceOption.Off;
			pngBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmapSource));
			using(MemoryStream memoryStream=new MemoryStream()) {
				pngBitmapEncoder.Save(memoryStream);
				return memoryStream.ToArray();
			}
		}

		private static double GetDpi(double dpi) {
			if(double.IsNaN(dpi) || double.IsInfinity(dpi) || dpi<=0) {
				return DpiDefault;
			}
			return dpi;
		}
	}
}
