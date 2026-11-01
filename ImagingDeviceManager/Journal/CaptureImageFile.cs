// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Imaging;

namespace ImagingDeviceManager.Journal {
	///<summary>The image file of a journal or quarantine entry: its bytes, its extension and what the sidecar says about it. A DIB is written
	///as a .bmp file, byte for byte what the library received; 16-bit samples as a 16-bit grayscale .tif file (Gray16Tiff), the samples
	///unchanged.</summary>
	internal sealed class CaptureImageFile {
		public byte[] Bytes;
		public string Extension=CaptureFiles.ImageExtension;
		public string FormatDescription="";
		public int Width;
		public int Height;
		public int BitsPerPixel;
		public double XDpi;
		public double YDpi;

		public static CaptureImageFile FromDib(DibImage dib) {
			return new CaptureImageFile {
				Bytes=CaptureFiles.ToBmpBytes(dib),
				Extension=CaptureFiles.ImageExtension,
				FormatDescription="BMP: the DIB as received, unchanged",
				Width=dib.Width,
				Height=dib.Height,
				BitsPerPixel=dib.BitCount,
				XDpi=dib.XResolutionDpi,
				YDpi=dib.YResolutionDpi,
			};
		}

		///<summary>Throws when the image has neither samples nor a DIB.</summary>
		public static CaptureImageFile FromAcquiredImage(AcquiredImage image) {
			if(image.Gray16!=null) {
				return new CaptureImageFile {
					Bytes=Gray16Tiff.Encode(image.Width,image.Height,image.Gray16,image.XDpi,image.YDpi,image.IsVanilla),
					Extension=Gray16Tiff.Extension,
					FormatDescription="TIFF: the 16-bit samples as received, unchanged (uncompressed, "
						+(image.IsVanilla ? "WhiteIsZero" : "BlackIsZero")+", byte order "+image.ByteOrder16+" as read)",
					Width=image.Width,
					Height=image.Height,
					BitsPerPixel=16,
					XDpi=image.XDpi,
					YDpi=image.YDpi,
				};
			}
			if(image.Dib==null) {
				throw new ArgumentException("The image has no pixels.");
			}
			CaptureImageFile file=FromDib(DibImage.FromPackedDib(image.Dib));
			file.XDpi=image.XDpi;
			file.YDpi=image.YDpi;
			return file;
		}

		///<summary>Size, depth, resolution and SHA-256 of the image file, so a copy can be matched to a stored document or to another copy.</summary>
		public List<SidecarField> GetFields(string pathImage,string sha256) {
			return new List<SidecarField> {
				new SidecarField("ImageFile",System.IO.Path.GetFileName(pathImage??"")),
				new SidecarField("ImageFormat",FormatDescription),
				new SidecarField("Width",Width.ToString(CultureInfo.InvariantCulture)),
				new SidecarField("Height",Height.ToString(CultureInfo.InvariantCulture)),
				new SidecarField("BitsPerPixel",BitsPerPixel.ToString(CultureInfo.InvariantCulture)),
				new SidecarField("XDpi",XDpi.ToString("0.###",CultureInfo.InvariantCulture)),
				new SidecarField("YDpi",YDpi.ToString("0.###",CultureInfo.InvariantCulture)),
				new SidecarField("Sha256",sha256),
			};
		}
	}
}
