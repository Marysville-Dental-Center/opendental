// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;

namespace ImagingDeviceManager.Capture {
	///<summary>How an image came in, apart from an ordinary transfer.</summary>
	public enum AcquiredImageKind {
		///<summary>An image of the enable: the first one in PerSlot mode, and every one in Continuous mode and in a redelivery.</summary>
		Normal,
		///<summary>PerSlot mode asked for one image (CAP_XFERCOUNT=1) and the source delivered more, for example because one of its own
		///settings overrides the count (design 7.12 "Extra images in single mode").</summary>
		Extra,
		///<summary>Transferred while the session stopped the enable in the safe order: pending when the stop began (state 6), or offered after
		///a refused disable (W13). StopReason says why the enable was stopped. The session window places it in the target of that enable,
		///and quarantines it only after a patient change, a forced close or the end of the session (design 7.6, 7.11).</summary>
		Drained,
	}

	///<summary>The byte order of 16-bit samples in a memory transfer. TWAIN and the sensor contract say little-endian ([Device.N]
	///ByteOrder16=LE, the default); BE swaps the bytes of each sample, for a source that has been verified to need it (review flag).</summary>
	public enum TwainByteOrder {
		LE,
		BE,
	}

	public enum TwainTransferMechanism {
		Native,
		Memory,
	}

	///<summary>One image TwainCaptureSession received, exactly as the source delivered it: a 16-bit image as raw samples (Gray16), never
	///shifted or windowed, and anything else as the packed DIB the transfer gave (Dib). The library writes its journal copy before the image
	///is raised (design 7.1, 7.3). Raised on the session's worker thread; the session window owns it from then on.</summary>
	public sealed class AcquiredImage {
		public const int PixelTypeBw=0;
		public const int PixelTypeGray=1;
		public const int PixelTypeRgb=2;
		public const int PixelTypePalette=3;
		///<summary>ICAP_PIXELFLAVOR / TWEI_PIXELFLAVOR TWPF_CHOCOLATE: 0 is black.</summary>
		public const int PixelFlavorChocolate=0;
		///<summary>TWPF_VANILLA: 0 is white.</summary>
		public const int PixelFlavorVanilla=1;

		public int Width { get; set; }
		public int Height { get; set; }
		///<summary>16 for Gray16; the DIB's bit count otherwise.</summary>
		public int BitsPerPixel { get; set; }
		///<summary>DAT_IMAGEINFO BitsPerSample[0]: 16 for a 16-bit sensor image, whatever its effective bits.</summary>
		public int BitsPerSample { get; set; }
		///<summary>DAT_IMAGEINFO PixelType (TWPT_: PixelTypeGray for the sensor), or -1 when the source did not describe the image.</summary>
		public int PixelType { get; set; }
		///<summary>TWEI_PIXELFLAVOR of the image when the source gave it, otherwise ICAP_PIXELFLAVOR at the enable: PixelFlavorChocolate or
		///PixelFlavorVanilla. Gray16 is never inverted by the library (Polarity is the saver's, design 7.8).</summary>
		public int PixelFlavor { get; set; }
		public double XDpi { get; set; }
		public double YDpi { get; set; }
		///<summary>A 16-bit image: Width*Height samples, rows top to bottom, each value right-justified as delivered (no shift, no window,
		///no inversion), in ByteOrder16. Null for every other image.</summary>
		public ushort[] Gray16 { get; set; }
		///<summary>Any other image: the packed DIB (BITMAPINFOHEADER, color table, bottom-up rows) as the transfer gave it, or as built from
		///the strips of a memory transfer. Null for a 16-bit image.</summary>
		public byte[] Dib { get; set; }
		///<summary>The raw TWEI_TWAINDIRECTMETADATA text of the image (UTF-8 JSON, without the NUL), read in state 7 when the source lists the
		///item in ICAP_SUPPORTEDEXTIMAGEINFO. Null when there is none. Not parsed here.</summary>
		public string MetadataJson { get; set; }
		///<summary>Local time the transfer ended.</summary>
		public DateTime ReceivedAt { get; set; }
		///<summary>The enable the image came from (TwainCaptureSession.Enable and RequestRedelivery return it).</summary>
		public int EnableId { get; set; }
		///<summary>The Tag of that enable's TwainEnableRequest.</summary>
		public object Tag { get; set; }
		///<summary>The TargetLabel of that enable's request, for sidecars.</summary>
		public string TargetLabel { get; set; }
		///<summary>None, unless the image was transferred while the enable was being stopped (Kind Drained): then why it was stopped.</summary>
		public TwainCaptureStopReason StopReason { get; set; }
		public AcquiredImageKind Kind { get; set; }
		///<summary>The image came from a redelivery enable (TwainCaptureSession.RequestRedelivery). The source knows no patient: the session
		///window places it only after the operator confirms it (design 7.12).</summary>
		public bool IsRedelivery { get; set; }
		///<summary>8 or 16: what the enable asked for.</summary>
		public int RequestedBits { get; set; }
		///<summary>The enable negotiated 16 bits (ICAP_BITDEPTH 16 accepted over memory transfer).</summary>
		public bool IsBitDepth16InEffect { get; set; }
		///<summary>The enable negotiated 16 bits, but DAT_IMAGEINFO reported 8 for this image (an image with no 16-bit original,
		///twain-contract 1.1 section 6.2). It goes down the 8-bit save path, never the 16-bit DICOM path (design 7.1 CAP-3).</summary>
		public bool IsDeliveredAs8BitIn16BitEnable { get; set; }
		///<summary>The byte order Gray16 was read in (TwainEnableRequest.ByteOrder16).</summary>
		public TwainByteOrder ByteOrder16 { get; set; }
		public TwainTransferMechanism TransferMechanism { get; set; }
		///<summary>The source's ProductName.</summary>
		public string SourceName { get; set; }
		///<summary>1-based position of the image within its enable.</summary>
		public int IndexInEnable { get; set; }
		///<summary>The journal copy the library wrote before raising the image (a .bmp of the DIB, or a 16-bit grayscale .tif of Gray16), or
		///null when none could be written (the capture log says why).</summary>
		public string JournalPath { get; set; }
		///<summary>Set when the library itself put the image in the quarantine (after BeginClose), or QuarantineImage did.</summary>
		public string QuarantinePath { get; set; }
		///<summary>The enable's TwainEnableRequest.SidecarFields, copied to the image's journal and quarantine sidecars. Set by the library
		///when it raises the image; null when the request had none.</summary>
		public List<KeyValuePair<string,string>> SidecarFields { get; set; }

		public bool IsGray16 => Gray16!=null;

		public bool IsVanilla => PixelFlavor==PixelFlavorVanilla;
	}
}
