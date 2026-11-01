// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using ImagingCapture.Dicom;
using ImagingCapture.Metadata;
using ImagingCapture.Save;
using ImagingCapture.Session;
using ImagingDeviceManager.Capture;

namespace ImagingCapture.UI {
	///<summary>Test capture's automatic byte-order check of a 16-bit image.</summary>
	public enum ByteOrderVerdict {
		///<summary>An 8-bit image: there is no byte order.</summary>
		NotApplicable,
		///<summary>Neighbouring samples differ far less as read than with their bytes swapped: the configured ByteOrder16 looks right.</summary>
		LooksRight,
		///<summary>They differ far less with the bytes swapped: the configured ByteOrder16 looks wrong.</summary>
		LooksSwapped,
		///<summary>Neither order is clearly smoother (a flat or tiny image); the operator decides on a known ramp.</summary>
		Inconclusive,
	}

	///<summary>What Test capture measures on one image (design 7.8 "Test capture measures the effective range before 16-bit is enabled",
	///13 step 8): delivered bit depth, the measured minimum, maximum and effective bits, the BitsStored rule's result, the presentation
	///window, a histogram, the byte-order check for 16-bit samples, and an 8-bit preview. Pure: it reads the image only.
	///
	///Range, effective bits, histogram and byte order are measured on the samples as delivered. The window, the BitsStored rule and the
	///preview use the samples as the saver would store them, so with Polarity=Invert they are measured after the inversion.</summary>
	public sealed class TestCaptureAnalysis {
		///<summary>Bins of the histogram.</summary>
		public const int HistogramBins=256;
		///<summary>Each order's mean neighbour difference must be this many times smaller than the other's for a verdict.</summary>
		public const double ByteOrderRatio=4;
		///<summary>Fewer neighbouring pairs of non-zero samples than this give Inconclusive.</summary>
		public const int ByteOrderMinPairs=16;

		public int Width { get; private set; }
		public int Height { get; private set; }
		public double XDpi { get; private set; }
		public double YDpi { get; private set; }
		public int BitsRequested { get; private set; }
		///<summary>16, 14 or 8: the depth the enable ran at (AcquiredImage.BitDepthInEffect).</summary>
		public int BitDepthInEffect { get; private set; }
		///<summary>Why the enable ran at fewer bits than asked (AcquiredImage.BitDepthReason), or null.</summary>
		public string BitDepthReason { get; private set; }
		///<summary>DAT_IMAGEINFO BitsPerSample.</summary>
		public int BitsDelivered { get; private set; }
		public bool IsGray16 { get; private set; }
		public bool IsDeliveredAs8BitIn16BitEnable { get; private set; }
		public TwainTransferMechanism TransferMechanism { get; private set; }
		public int PixelType { get; private set; }
		public int PixelFlavor { get; private set; }
		public TwainByteOrder ByteOrder16 { get; private set; }
		public TwainPolarity Polarity { get; private set; }
		///<summary>Polarity=Invert was applied to the window, the BitsStored rule and the preview (16-bit only).</summary>
		public bool IsInverted { get; private set; }
		///<summary>An 8-bit image in color (not every pixel grey).</summary>
		public bool IsColor { get; private set; }
		public int CountPixels { get; private set; }
		///<summary>16-bit: the zero-valued samples (masked corners and dead areas, excluded from range and window).</summary>
		public int CountZero { get; private set; }
		///<summary>16-bit: the smallest and largest non-zero sample as delivered; 8-bit: the darkest and brightest grey. 0 when there is none.</summary>
		public int MinValue { get; private set; }
		public int MaxValue { get; private set; }
		///<summary>The bits needed to hold MaxValue (12 for 4095). 8 for an 8-bit image.</summary>
		public int EffectiveBitsMeasured { get; private set; }
		///<summary>16-bit: the BitsStored rule (12 or 16) for the stored samples with the metadata's effectiveBits. 0 for 8-bit.</summary>
		public int BitsStoredRule { get; private set; }
		///<summary>16-bit: the 0.5/99.5 percentile window of the stored samples (DicomIntraOralWriter.GetWindow). 0/0 for 8-bit.</summary>
		public int WindowLow { get; private set; }
		public int WindowHigh { get; private set; }
		///<summary>HistogramBins counts. 16-bit: non-zero delivered samples, bin = value >> HistogramShift; 8-bit: every grey value.</summary>
		public int[] Histogram { get; private set; }
		public int HistogramShift { get; private set; }
		public ByteOrderVerdict ByteOrderVerdict { get; private set; }
		///<summary>The mean difference between horizontally neighbouring non-zero samples, as read and with the bytes swapped.</summary>
		public double RoughnessAsRead { get; private set; }
		public double RoughnessSwapped { get; private set; }
		///<summary>Width*Height grey bytes, rows top to bottom: 16-bit through the fixed LUT of the window, 8-bit as delivered.</summary>
		public byte[] Preview { get; private set; }
		public CaptureMetadata Metadata { get; private set; }
		///<summary>Why the image could not be measured, or "".</summary>
		public string Error { get; private set; }="";

		public bool IsValid => Error=="";

		///<summary>Measures image. polarity: [Device.N] Polarity in effect. Never throws.</summary>
		public static TestCaptureAnalysis Analyze(AcquiredImage image,TwainPolarity polarity) {
			TestCaptureAnalysis analysis=new TestCaptureAnalysis();
			try {
				analysis.AnalyzeChecked(image,polarity);
			}
			catch(Exception ex) {
				analysis.Error="The image could not be measured: "+ex.Message;
			}
			return analysis;
		}

		private void AnalyzeChecked(AcquiredImage image,TwainPolarity polarity) {
			if(image==null) {
				Error="No image.";
				return;
			}
			Width=image.Width;
			Height=image.Height;
			XDpi=image.XDpi;
			YDpi=image.YDpi;
			BitsRequested=image.RequestedBits;
			BitDepthInEffect=TwainBitDepth.Normalize(image.BitDepthInEffect);
			BitDepthReason=string.IsNullOrWhiteSpace(image.BitDepthReason) ? null : image.BitDepthReason.Trim();
			BitsDelivered=image.BitsPerSample;
			IsGray16=image.IsGray16;
			IsDeliveredAs8BitIn16BitEnable=image.IsDeliveredAs8BitIn16BitEnable;
			TransferMechanism=image.TransferMechanism;
			PixelType=image.PixelType;
			PixelFlavor=image.PixelFlavor;
			ByteOrder16=image.ByteOrder16;
			Polarity=polarity;
			Metadata=CaptureMetadataParser.Parse(image.MetadataJson);
			if(image.IsGray16) {
				Analyze16(image.Gray16,image.Width,image.Height);
				return;
			}
			Analyze8(image.Dib);
		}

		private void Analyze16(ushort[] pixels,int width,int height) {
			if(width<=0 || height<=0 || pixels.LongLength!=(long)width*height) {
				Error="The 16-bit image's size does not match its samples.";
				return;
			}
			CountPixels=pixels.Length;
			int min=int.MaxValue;
			int max=0;
			int countZero=0;
			for(int i=0;i<pixels.Length;i++) {
				int value=pixels[i];
				if(value==0) {
					countZero++;
					continue;
				}
				if(value<min) {
					min=value;
				}
				if(value>max) {
					max=value;
				}
			}
			CountZero=countZero;
			MinValue=max==0 ? 0 : min;
			MaxValue=max;
			EffectiveBitsMeasured=GetBitsFor(max);
			HistogramShift=Math.Max(0,EffectiveBitsMeasured-8);
			int[] arrayHistogram=new int[HistogramBins];
			for(int i=0;i<pixels.Length;i++) {
				if(pixels[i]!=0) {
					arrayHistogram[Math.Min(HistogramBins-1,pixels[i]>>HistogramShift)]++;
				}
			}
			Histogram=arrayHistogram;
			CheckByteOrder(pixels,width,height);
			ushort[] arrayStored=pixels;
			if(Polarity==TwainPolarity.Invert) {
				arrayStored=Gray16Samples.Invert(pixels,Metadata?.EffectiveBits);
				IsInverted=true;
			}
			BitsStoredRule=DicomIntraOralWriter.GetBitsStored(arrayStored,Metadata?.EffectiveBits);
			int windowLow;
			int windowHigh;
			DicomIntraOralWriter.GetWindow(arrayStored,out windowLow,out windowHigh);
			WindowLow=windowLow;
			WindowHigh=windowHigh;
			Preview=Gray16Samples.ToPreview(arrayStored,windowLow,windowHigh);
		}

		private void Analyze8(byte[] dib) {
			string error;
			DecodedImage decodedImage=DibDecoder.Decode(dib,out error);
			if(decodedImage==null) {
				Error=error;
				return;
			}
			Width=decodedImage.Width;
			Height=decodedImage.Height;
			IsColor=!decodedImage.IsGray;
			byte[] arrayGray;
			if(decodedImage.IsGray) {
				arrayGray=decodedImage.Pixels;
			}
			else {//luma of B, G, R, only for the measurement and the preview
				arrayGray=new byte[decodedImage.Width*decodedImage.Height];
				for(int i=0;i<arrayGray.Length;i++) {
					int b=decodedImage.Pixels[i*3];
					int g=decodedImage.Pixels[i*3+1];
					int r=decodedImage.Pixels[i*3+2];
					arrayGray[i]=(byte)((r*299+g*587+b*114+500)/1000);
				}
			}
			CountPixels=arrayGray.Length;
			int min=255;
			int max=0;
			int[] arrayHistogram=new int[HistogramBins];
			for(int i=0;i<arrayGray.Length;i++) {
				arrayHistogram[arrayGray[i]]++;
				min=Math.Min(min,arrayGray[i]);
				max=Math.Max(max,arrayGray[i]);
			}
			MinValue=arrayGray.Length==0 ? 0 : min;
			MaxValue=max;
			EffectiveBitsMeasured=8;
			Histogram=arrayHistogram;
			ByteOrderVerdict=ByteOrderVerdict.NotApplicable;
			Preview=arrayGray;
		}

		///<summary>Compares the mean difference of horizontally neighbouring non-zero samples as read with the same with each sample's bytes
		///swapped. In the right order an image is smooth at this scale; in the wrong one the noise of the low byte lands in the high byte, which
		///makes neighbours differ by hundreds or thousands. A known ramp gives the clearest result.</summary>
		private void CheckByteOrder(ushort[] pixels,int width,int height) {
			long sumAsRead=0;
			long sumSwapped=0;
			long countPairs=0;
			for(int y=0;y<height;y++) {
				int offsetRow=y*width;
				for(int x=1;x<width;x++) {
					int a=pixels[offsetRow+x-1];
					int b=pixels[offsetRow+x];
					if(a==0 || b==0) {
						continue;
					}
					sumAsRead+=Math.Abs(a-b);
					int aSwapped=((a>>8)|((a&0xFF)<<8));
					int bSwapped=((b>>8)|((b&0xFF)<<8));
					sumSwapped+=Math.Abs(aSwapped-bSwapped);
					countPairs++;
				}
			}
			if(countPairs==0) {
				ByteOrderVerdict=ByteOrderVerdict.Inconclusive;
				return;
			}
			RoughnessAsRead=(double)sumAsRead/countPairs;
			RoughnessSwapped=(double)sumSwapped/countPairs;
			if(countPairs<ByteOrderMinPairs) {
				ByteOrderVerdict=ByteOrderVerdict.Inconclusive;
			}
			else if(RoughnessAsRead*ByteOrderRatio<RoughnessSwapped) {
				ByteOrderVerdict=ByteOrderVerdict.LooksRight;
			}
			else if(RoughnessSwapped*ByteOrderRatio<RoughnessAsRead) {
				ByteOrderVerdict=ByteOrderVerdict.LooksSwapped;
			}
			else {
				ByteOrderVerdict=ByteOrderVerdict.Inconclusive;
			}
		}

		///<summary>The number of bits needed to hold value: 0 for 0, 12 for 4095, 13 for 4096.</summary>
		public static int GetBitsFor(int value) {
			int bits=0;
			while(value>0) {
				bits++;
				value>>=1;
			}
			return bits;
		}

		///<summary>The results, one line each, for the results box.</summary>
		public List<string> GetResultLines() {
			List<string> listLines=new List<string>();
			if(!IsValid) {
				listLines.Add(Error);
				return listLines;
			}
			listLines.Add("Size: "+Width+" x "+Height+" pixels, "+FormatDpi(XDpi)+" x "+FormatDpi(YDpi)+" dpi.");
			string mechanism=TransferMechanism==TwainTransferMechanism.Memory ? "memory" : "native";
			if(IsGray16 && BitDepthInEffect==TwainBitDepth.Bits14) {
				listLines.Add("Delivered: "+CaptureBitDepthText.Bits14+", grey over "+mechanism+" transfer (asked for "+BitsRequested+")."
					+(BitDepthReason==null ? "" : " "+BitDepthReason.TrimEnd('.')+"."));
			}
			else if(IsGray16) {
				listLines.Add("Delivered: "+CaptureBitDepthText.Bits16+" grey over "+mechanism+" transfer (asked for "+BitsRequested+")."
					+(BitDepthInEffect>8 ? "" : " These 16-bit samples did not come from an enable of more than 8 bits."));
			}
			else {
				string depth=BitsDelivered==8 ? CaptureBitDepthText.Describe(TwainBitDepth.Bits8,BitsRequested>8 ? BitDepthReason : null)
					: BitsDelivered+"-bit";
				listLines.Add("Delivered: "+depth+" "+(IsColor ? "color" : "grey")+" over "+mechanism+" transfer (asked for "+BitsRequested+")."
					+(IsDeliveredAs8BitIn16BitEnable ? " The source had no 16-bit original for this image." : ""));
			}
			listLines.Add("Pixel flavor: "+(PixelFlavor==AcquiredImage.PixelFlavorVanilla ? "vanilla (0 is white)" : "chocolate (0 is black)")
				+". Polarity setting: "+Polarity+(IsInverted ? " (window, BitsStored and preview after the inversion)" : "")+".");
			if(IsGray16) {
				listLines.Add("Byte order (setting "+ByteOrder16+"): "+GetByteOrderText()+".");
				listLines.Add("Measured range of the non-zero samples: "+MinValue+" to "+MaxValue+", "+EffectiveBitsMeasured+" effective bits; "
					+FormatPercent(CountZero,CountPixels)+" of the pixels are 0 (masked).");
				listLines.Add("Window (0.5 / 99.5 percentile): "+WindowLow+" to "+WindowHigh+".");
				listLines.Add("BitsStored rule: "+BitsStoredRule+" (metadata effectiveBits "+(Metadata?.EffectiveBits?.ToString(CultureInfo.InvariantCulture)??"not reported")
					+"). Data that fits 12 bits is stored as 12 only when the source reports 12 or fewer effective bits.");
				if(Metadata?.Window!=null) {
					listLines.Add("The source's own window: "+Metadata.Window.Low+" to "+Metadata.Window.High+".");
				}
			}
			else {
				listLines.Add("Grey range: "+MinValue+" to "+MaxValue+".");
			}
			if(Metadata==null) {
				listLines.Add("Capture metadata: none.");
			}
			else {
				listLines.Add("Capture metadata: serial "+Show(Metadata.Serial)+", model "+Show(Metadata.Model)+", rendering "+Show(Metadata.Rendering)
					+", bitsStored "+(Metadata.BitsStored?.ToString(CultureInfo.InvariantCulture)??"-")+", byteOrder "+Show(Metadata.ByteOrder)
					+", captureId "+Show(Metadata.CaptureId)+", version "+Show(Metadata.DsVersion)+".");
				if(Metadata.HasOrientationAdjustment) {
					listLines.Add(RadiographSavePlanner.OrientationAdjustmentWarning);
				}
				foreach(string warning in Metadata.ListWarnings) {
					listLines.Add("Metadata: "+warning);
				}
			}
			return listLines;
		}

		public string GetByteOrderText() {
			switch(ByteOrderVerdict) {
				case ByteOrderVerdict.LooksRight:
					return "looks right ("+FormatRoughness()+")";
				case ByteOrderVerdict.LooksSwapped:
					return "LOOKS SWAPPED ("+FormatRoughness()+"); check ByteOrder16 against a known ramp";
				case ByteOrderVerdict.Inconclusive:
					return "inconclusive ("+FormatRoughness()+"); check against a known ramp";
				default:
					return "not applicable";
			}
		}

		private string FormatRoughness() {
			return "neighbour difference "+RoughnessAsRead.ToString("0.#",CultureInfo.InvariantCulture)+" as read, "
				+RoughnessSwapped.ToString("0.#",CultureInfo.InvariantCulture)+" swapped";
		}

		private static string FormatDpi(double dpi) {
			return dpi>0 ? dpi.ToString("0.##",CultureInfo.InvariantCulture) : "?";
		}

		private static string FormatPercent(int count,int total) {
			if(total<=0) {
				return "0%";
			}
			return (100.0*count/total).ToString("0.#",CultureInfo.InvariantCulture)+"%";
		}

		private static string Show(string text) {
			return string.IsNullOrWhiteSpace(text) ? "-" : text.Trim();
		}
	}
}
