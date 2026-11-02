// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>How a transfer syntax encodes the data set and its pixels (NEMA PS3.5 section 10 and Annex A).</summary>
	public enum DicomTransferSyntaxKind {
		///<summary>Implicit VR Little Endian (PS3.5 A.1): native pixel data.</summary>
		ImplicitLittleEndian,
		///<summary>Explicit VR Little Endian (A.2): native pixel data.</summary>
		ExplicitLittleEndian,
		///<summary>Explicit VR Big Endian (A.3, retired): native pixel data.</summary>
		ExplicitBigEndian,
		///<summary>Deflated Explicit VR Little Endian (A.5): the whole data set after the File Meta Information is deflated (RFC 1951).</summary>
		DeflatedExplicitLittleEndian,
		///<summary>Explicit VR Little Endian with encapsulated pixel data (A.4): compressed images, and Encapsulated Uncompressed.</summary>
		EncapsulatedImage,
		///<summary>Encapsulated video (MPEG-2, H.264, H.265): never a still image this program shows.</summary>
		EncapsulatedVideo,
		///<summary>The pixel data is not in the file (JPIP Referenced), or the file is not a binary data set at all (MIME, XML), or it is a
		///SMPTE ST 2110 stream.</summary>
		NoPixelsInFile,
	}

	///<summary>One transfer syntax of PS3.6 Table A-1.</summary>
	public sealed class DicomTransferSyntaxInfo {
		public string Uid { get; }
		///<summary>The UID Name of PS3.6 Table A-1, without its "Default Transfer Syntax for ..." note.</summary>
		public string Name { get; }
		public DicomTransferSyntaxKind Kind { get; }
		///<summary>Each frame is exactly one fragment (RLE Lossless, PS3.5 G.5; Encapsulated Uncompressed, A.4.11), so a frame is found without
		///looking inside the fragments.</summary>
		public bool IsOneFragmentPerFrame { get; }

		public DicomTransferSyntaxInfo(string uid,string name,DicomTransferSyntaxKind kind,bool isOneFragmentPerFrame=false) {
			Uid=uid;
			Name=name;
			Kind=kind;
			IsOneFragmentPerFrame=isOneFragmentPerFrame;
		}

		///<summary>True for the transfer syntaxes whose pixel data is native (not encapsulated).</summary>
		public bool IsNative {
			get {
				return Kind==DicomTransferSyntaxKind.ImplicitLittleEndian || Kind==DicomTransferSyntaxKind.ExplicitLittleEndian
					|| Kind==DicomTransferSyntaxKind.ExplicitBigEndian || Kind==DicomTransferSyntaxKind.DeflatedExplicitLittleEndian;
			}
		}

		public override string ToString() {
			return Name+" ("+Uid+")";
		}
	}

	///<summary>The transfer syntaxes of PS3.6 Table A-1 (2026 edition), with the names the standard gives them.</summary>
	public static class DicomTransferSyntaxes {
		public const string ImplicitVrLittleEndian="1.2.840.10008.1.2";
		public const string ExplicitVrLittleEndian="1.2.840.10008.1.2.1";
		public const string EncapsulatedUncompressed="1.2.840.10008.1.2.1.98";
		public const string DeflatedExplicitVrLittleEndian="1.2.840.10008.1.2.1.99";
		public const string ExplicitVrBigEndian="1.2.840.10008.1.2.2";
		public const string JpegBaseline="1.2.840.10008.1.2.4.50";
		public const string RleLossless="1.2.840.10008.1.2.5";

		private static readonly List<DicomTransferSyntaxInfo> _listAll=Build();

		private static List<DicomTransferSyntaxInfo> Build() {
			DicomTransferSyntaxKind image=DicomTransferSyntaxKind.EncapsulatedImage;
			DicomTransferSyntaxKind video=DicomTransferSyntaxKind.EncapsulatedVideo;
			DicomTransferSyntaxKind none=DicomTransferSyntaxKind.NoPixelsInFile;
			List<DicomTransferSyntaxInfo> list=new List<DicomTransferSyntaxInfo>();
			list.Add(new DicomTransferSyntaxInfo(ImplicitVrLittleEndian,"Implicit VR Little Endian",DicomTransferSyntaxKind.ImplicitLittleEndian));
			list.Add(new DicomTransferSyntaxInfo(ExplicitVrLittleEndian,"Explicit VR Little Endian",DicomTransferSyntaxKind.ExplicitLittleEndian));
			list.Add(new DicomTransferSyntaxInfo(EncapsulatedUncompressed,"Encapsulated Uncompressed Explicit VR Little Endian",image,isOneFragmentPerFrame:true));
			list.Add(new DicomTransferSyntaxInfo(DeflatedExplicitVrLittleEndian,"Deflated Explicit VR Little Endian",DicomTransferSyntaxKind.DeflatedExplicitLittleEndian));
			list.Add(new DicomTransferSyntaxInfo(ExplicitVrBigEndian,"Explicit VR Big Endian (Retired)",DicomTransferSyntaxKind.ExplicitBigEndian));
			list.Add(new DicomTransferSyntaxInfo(JpegBaseline,"JPEG Baseline (Process 1)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.51","JPEG Extended (Process 2 & 4)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.52","JPEG Extended (Process 3 & 5) (Retired)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.53","JPEG Spectral Selection, Non-Hierarchical (Process 6 & 8) (Retired)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.54","JPEG Spectral Selection, Non-Hierarchical (Process 7 & 9) (Retired)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.55","JPEG Full Progression, Non-Hierarchical (Process 10 & 12) (Retired)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.56","JPEG Full Progression, Non-Hierarchical (Process 11 & 13) (Retired)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.57","JPEG Lossless, Non-Hierarchical (Process 14)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.58","JPEG Lossless, Non-Hierarchical (Process 15) (Retired)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.59","JPEG Extended, Hierarchical (Process 16 & 18) (Retired)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.60","JPEG Extended, Hierarchical (Process 17 & 19) (Retired)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.61","JPEG Spectral Selection, Hierarchical (Process 20 & 22) (Retired)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.62","JPEG Spectral Selection, Hierarchical (Process 21 & 23) (Retired)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.63","JPEG Full Progression, Hierarchical (Process 24 & 26) (Retired)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.64","JPEG Full Progression, Hierarchical (Process 25 & 27) (Retired)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.65","JPEG Lossless, Hierarchical (Process 28) (Retired)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.66","JPEG Lossless, Hierarchical (Process 29) (Retired)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.70","JPEG Lossless, Non-Hierarchical, First-Order Prediction (Process 14 [Selection Value 1])",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.80","JPEG-LS Lossless Image Compression",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.81","JPEG-LS Lossy (Near-Lossless) Image Compression",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.90","JPEG 2000 Image Compression (Lossless Only)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.91","JPEG 2000 Image Compression",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.92","JPEG 2000 Part 2 Multi-component Image Compression (Lossless Only)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.93","JPEG 2000 Part 2 Multi-component Image Compression",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.94","JPIP Referenced",none));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.95","JPIP Referenced Deflate",none));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.100","MPEG2 Main Profile / Main Level",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.100.1","Fragmentable MPEG2 Main Profile / Main Level",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.101","MPEG2 Main Profile / High Level",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.101.1","Fragmentable MPEG2 Main Profile / High Level",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.102","MPEG-4 AVC/H.264 High Profile / Level 4.1",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.102.1","Fragmentable MPEG-4 AVC/H.264 High Profile / Level 4.1",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.103","MPEG-4 AVC/H.264 BD-compatible High Profile / Level 4.1",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.103.1","Fragmentable MPEG-4 AVC/H.264 BD-compatible High Profile / Level 4.1",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.104","MPEG-4 AVC/H.264 High Profile / Level 4.2 For 2D Video",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.104.1","Fragmentable MPEG-4 AVC/H.264 High Profile / Level 4.2 For 2D Video",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.105","MPEG-4 AVC/H.264 High Profile / Level 4.2 For 3D Video",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.105.1","Fragmentable MPEG-4 AVC/H.264 High Profile / Level 4.2 For 3D Video",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.106","MPEG-4 AVC/H.264 Stereo High Profile / Level 4.2",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.106.1","Fragmentable MPEG-4 AVC/H.264 Stereo High Profile / Level 4.2",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.107","HEVC/H.265 Main Profile / Level 5.1",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.108","HEVC/H.265 Main 10 Profile / Level 5.1",video));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.110","JPEG XL Lossless",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.111","JPEG XL JPEG Recompression",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.112","JPEG XL",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.201","High-Throughput JPEG 2000 Image Compression (Lossless Only)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.202","High-Throughput JPEG 2000 with RPCL Options Image Compression (Lossless Only)",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.203","High-Throughput JPEG 2000 Image Compression",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.204","JPIP HTJ2K Referenced",none));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.4.205","JPIP HTJ2K Referenced Deflate",none));
			list.Add(new DicomTransferSyntaxInfo(RleLossless,"RLE Lossless",image,isOneFragmentPerFrame:true));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.6.1","RFC 2557 MIME encapsulation (Retired)",none));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.6.2","XML Encoding (Retired)",none));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.7.1","SMPTE ST 2110-20 Uncompressed Progressive Active Video",none));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.7.2","SMPTE ST 2110-20 Uncompressed Interlaced Active Video",none));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.7.3","SMPTE ST 2110-30 PCM Digital Audio",none));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.2.8.1","Deflated Image Frame Compression",image));
			list.Add(new DicomTransferSyntaxInfo("1.2.840.10008.1.20","Papyrus 3 Implicit VR Little Endian (Retired)",DicomTransferSyntaxKind.ImplicitLittleEndian));
			return list;
		}

		///<summary>Every transfer syntax of PS3.6 Table A-1.</summary>
		public static IReadOnlyList<DicomTransferSyntaxInfo> All {
			get { return _listAll; }
		}

		///<summary>The transfer syntax of the UID, or null for a UID the standard does not list (a private one, or a mistake).</summary>
		public static DicomTransferSyntaxInfo Find(string uid) {
			if(uid==null) {
				return null;
			}
			return _listAll.FirstOrDefault(x => x.Uid==uid);
		}
	}
}
