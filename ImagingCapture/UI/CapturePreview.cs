// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using ImagingCapture.Dicom;
using ImagingCapture.Save;
using ImagingDeviceManager.Capture;

namespace ImagingCapture.UI {
	///<summary>An 8-bit picture of an image for the session window's brief preview: top-down rows with no padding, one grey byte per pixel
	///(IsGray, stride Width) or three bytes B, G, R (stride 3*Width).</summary>
	public sealed class CapturePreviewPixels {
		public int Width { get; set; }
		public int Height { get; set; }
		public bool IsGray { get; set; }
		public byte[] Pixels { get; set; }
		public int Stride => IsGray ? Width : Width*3;
	}

	///<summary>The preview of a received image (design 7.3 step 4): an 8-bit image as delivered (DibDecoder), a 16-bit one through the fixed LUT
	///of its 0.5/99.5 percentile window, as stored (inverted when Polarity=Invert applied). The display transform (the slot's rotation, the
	///operator's Rotate, the mount's flip) is the window's; these are the stored, unrotated rows.</summary>
	public static class CapturePreview {
		///<summary>The picture, or null with error. isInverted: a 16-bit image that was saved inverted (Polarity=Invert in effect).</summary>
		public static CapturePreviewPixels FromImage(AcquiredImage image,bool isInverted,out string error) {
			error="";
			if(image==null) {
				error="No image.";
				return null;
			}
			try {
				if(image.IsGray16) {
					return FromGray16(image,isInverted,out error);
				}
				DecodedImage decodedImage=DibDecoder.Decode(image.Dib,out error);
				if(decodedImage==null) {
					return null;
				}
				return new CapturePreviewPixels { Width=decodedImage.Width,Height=decodedImage.Height,IsGray=decodedImage.IsGray,Pixels=decodedImage.Pixels };
			}
			catch(Exception ex) {
				error="The image could not be shown: "+ex.Message;
				return null;
			}
		}

		private static CapturePreviewPixels FromGray16(AcquiredImage image,bool isInverted,out string error) {
			error="";
			ushort[] arrayPixels=image.Gray16;
			if(image.Width<1 || image.Height<1 || arrayPixels.Length<image.Width*image.Height) {
				error="The 16-bit image has fewer samples than its size.";
				return null;
			}
			if(isInverted) {
				arrayPixels=Gray16Samples.Invert(arrayPixels,Gray16Samples.GetEffectiveBits(Metadata.CaptureMetadataParser.Parse(image.MetadataJson),
					image.BitDepthInEffect));
			}
			int low;
			int high;
			DicomIntraOralWriter.GetWindow(arrayPixels,out low,out high);
			byte[] arrayPreview=Gray16Samples.ToPreview(arrayPixels,low,high);
			if(arrayPreview.Length>image.Width*image.Height) {
				Array.Resize(ref arrayPreview,image.Width*image.Height);
			}
			return new CapturePreviewPixels { Width=image.Width,Height=image.Height,IsGray=true,Pixels=arrayPreview };
		}
	}
}
