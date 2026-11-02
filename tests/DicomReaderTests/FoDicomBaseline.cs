// What fo-dicom 4.0.7, DICOM reader of Open Dental and of this program until the in-house reader replaced it, read from each synthetic case
// of ReaderCases.cs through the old DicomHelper path (open, decode to Explicit VR Little Endian, frame 0, photometric interpretation
// MONOCHROME1, MONOCHROME2 or RGB only, 8 or 16 bits allocated, 8, 12 or 16 bits stored, color averaged to gray). Recorded on 2026-11-02 by
// running fo-dicom 4.0.7 (with its native codecs) on these exact bytes, before fo-dicom was removed, so the in-house reader is checked against
// the old behaviour. The cases added after review (ReaderCases.MislabelledEncodings, jpeg-ybr-partial-422, jpeg-progressive-gray) were recorded
// the same day the same way, outside the repository, with the Dicom.Core.dll and Dicom.Native.dll of version 0.0 and the old path's code; that
// run gave every earlier value below again. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OpenPmsDentalBusiness;

namespace DicomReaderTests {
	internal static class FoDicomBaseline {
		///<summary>The JPEG Baseline stream of the gray JPEG cases, as the .NET Framework's JpegBitmapEncoder made it (ReaderCases.JpegGrayFixed).</summary>
		public const string JpegGrayBase64="/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/"
			+"wAALCAANABUBAREA/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2Jy"
			+"ggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW"
			+"19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/9oACAEBAAA/APjj4aW/2v4jeFYMZ83VrRMfWZRX6z/C34f/AOp/d+navprQvD0Gm2CRsgLEAn2r8D/gjEJ/jP4Bjb7r+INPU/jcx1+6"
			+"nw+0K2tLNZVXLAccV2tf/9k=";
		///<summary>The JPEG Baseline stream of the color JPEG cases (ReaderCases.JpegRgbFixed).</summary>
		public const string JpegRgbBase64="/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/"
			+"2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAAOABYDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEA"
			+"AAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdI"
			+"SUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QA"
			+"HwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygp"
			+"KjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq"
			+"8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD5m0T4X/d/c/pXq3gX4X/639z/AHe31r13RPAFt8vK/lXq/gXwBbfveV/h7fWvjuHuKp/23Q1f2v8A0iR8hwzxrP2S1fT9Dx/Svhf+6/1P"
			+"b0or6z0r4f23ldV6elFfr1TimfM9Wf0fg+NZ+wjqz//Z";

		///<summary>Case name, then what fo-dicom read: "null" (no image), "throws" (fo-dicom threw while opening or decoding), or
		///"Width x Height BitsAllocated/BitsStored PhotometricInterp hash" (Fingerprint).</summary>
		public static readonly Dictionary<string,string> DictRecorded=new Dictionary<string,string> {
			{ "enc-explicit-le-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-explicit-le-8","5x3 8/8 MONOCHROME2 7ff76828c8aeb09e" },
			{ "enc-implicit-le-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-implicit-le-8","5x3 8/8 MONOCHROME2 7ff76828c8aeb09e" },
			{ "enc-explicit-be-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-explicit-be-8-ob","5x3 8/8 MONOCHROME2 7ff76828c8aeb09e" },
			{ "enc-explicit-be-8-ow","4x3 8/8 MONOCHROME2 19cffaff1d189b4a" },
			{ "enc-explicit-be-16","6x4 16/16 MONOCHROME2 b25a93181be866a8" },
			{ "enc-deflate-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-deflate-8","5x3 8/8 MONOCHROME2 7ff76828c8aeb09e" },
			{ "enc-no-preamble-meta-explicit-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-no-preamble-meta-implicit-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-prefix-no-preamble-12","throws" },
			{ "enc-dataset-only-implicit-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-dataset-only-explicit-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-dataset-only-explicit-be-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-meta-without-ts-implicit-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-meta-without-ts-explicit-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-explicit-le-sequences-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-implicit-le-sequences-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-explicit-be-sequences-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-deflate-sequences-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-explicit-le-un-undefined-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-ts-explicit-dataset-implicit-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-ts-deflate-dataset-implicit-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-explicit-le-vr-spaces-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-explicit-be-vr-spaces-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-explicit-le-vr-spaces-pixel-data-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-meta-implicit-ts-implicit-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-meta-implicit-ts-explicit-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-meta-implicit-no-preamble-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-meta-implicit-no-preamble-ts-explicit-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-dataset-only-implicit-private-first-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-dataset-only-explicit-private-first-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "enc-dataset-only-implicit-group-0040-first-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "img-mono1-16-16-15","5x4 16/16 MONOCHROME1 2094dcbb07427982" },
			{ "img-mono1-16-12-11","5x4 16/12 MONOCHROME1 e133c4abfd5b6d23" },
			{ "img-mono1-8-8-7","5x3 8/8 MONOCHROME1 7ff76828c8aeb09e" },
			{ "img-mono2-16-12-11-noise","5x4 16/12 MONOCHROME2 e133c4abfd5b6d23" },
			{ "img-mono2-16-8-7","5x4 16/8 MONOCHROME2 b7e7fec5d6b595cf" },
			{ "img-mono2-16-8-15","5x4 16/8 MONOCHROME2 b7e7fec5d6b595cf" },
			{ "img-mono2-16-10-9","null" },
			{ "img-mono2-16-12-15","5x4 16/12 MONOCHROME2 e133c4abfd5b6d23" },
			{ "img-mono2-16-12-13","5x4 16/12 MONOCHROME2 e133c4abfd5b6d23" },
			{ "img-mono2-16-13-12","null" },
			{ "img-mono2-16-14-13","null" },
			{ "img-mono2-16-15-14","null" },
			{ "img-mono2-16-16-15","5x4 16/16 MONOCHROME2 2094dcbb07427982" },
			{ "img-mono2-16-1-0","null" },
			{ "img-mono2-16-4-3","null" },
			{ "img-mono2-8-8-7","5x3 8/8 MONOCHROME2 7ff76828c8aeb09e" },
			{ "img-mono2-8-6-5","null" },
			{ "img-mono2-8-4-7","null" },
			{ "img-mono2-8-1-0","null" },
			{ "img-mono2-8-7-6","null" },
			{ "img-signed-16-16-15","null" },
			{ "img-signed-16-12-11","null" },
			{ "img-signed-16-12-11-masked","null" },
			{ "img-signed-16-14-13","null" },
			{ "img-signed-8-8-7","5x3 8/8 MONOCHROME2 7827e3c798ab1286" },
			{ "img-signed-mono1-16-16-15","null" },
			{ "img-rescale-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "img-window-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "img-signed-rescale-window-16","null" },
			{ "img-rgb-planar0","4x3 8/8 RGB 522a12c130101ec8" },
			{ "img-rgb-planar1","4x3 8/8 RGB 522a12c130101ec8" },
			{ "img-rgb-implicit","4x3 8/8 RGB 522a12c130101ec8" },
			{ "img-rgb-big-endian","4x3 8/8 RGB 522a12c130101ec8" },
			{ "img-rgb-16","3x2 16/16 RGB 98983bc277948d8e" },
			{ "img-ybr-full-planar0","null" },
			{ "img-ybr-full-planar1","null" },
			{ "img-ybr-full-422","null" },
			{ "img-palette-8-lut16","null" },
			{ "img-palette-8-lut8","null" },
			{ "img-palette-16-lut16","null" },
			{ "img-palette-8-first-mapped","null" },
			{ "img-multiframe-12","5x4 16/12 MONOCHROME2 e133c4abfd5b6d23" },
			{ "img-multiframe-12-implicit","5x4 16/12 MONOCHROME2 e133c4abfd5b6d23" },
			{ "img-multiframe-rgb","4x3 8/8 RGB 522a12c130101ec8" },
			{ "img-multiframe-8-odd","5x3 8/8 MONOCHROME2 7ff76828c8aeb09e" },
			{ "img-large-16","1200x1000 16/16 MONOCHROME2 2ae22a4a1354b2c6" },
			{ "img-out-of-order-12","7x5 16/12 MONOCHROME2 0189a2a473ab1eb9" },
			{ "rle-mono2-8","10x6 8/8 MONOCHROME2 6657b7bac622d160" },
			{ "rle-mono2-16","10x6 16/16 MONOCHROME2 ca5c78c8a1ed1a66" },
			{ "rle-mono1-12-bot","10x6 16/12 MONOCHROME1 906e7a10a099af74" },
			{ "rle-signed-16","null" },
			{ "rle-rgb","10x6 8/8 RGB 5aaba9753a9a6b8f" },
			{ "rle-rgb-planar1-declared","10x6 8/8 RGB 5aaba9753a9a6b8f" },
			{ "rle-ybr-full","null" },
			{ "rle-multiframe-bot","10x6 16/16 MONOCHROME2 ca5c78c8a1ed1a66" },
			{ "rle-multiframe-no-bot","10x6 16/16 MONOCHROME2 ca5c78c8a1ed1a66" },
			{ "jpeg-gray","21x13 8/8 MONOCHROME2 9cfc1e581081a3c8" },
			{ "jpeg-gray-mono1","21x13 8/8 MONOCHROME1 9cfc1e581081a3c8" },
			{ "jpeg-gray-two-fragments","21x13 8/8 MONOCHROME2 9cfc1e581081a3c8" },
			{ "jpeg-gray-multiframe-no-bot","21x13 8/8 MONOCHROME2 9cfc1e581081a3c8" },
			{ "jpeg-ybr-full-422","null" },
			{ "jpeg-ybr-full","null" },
			{ "jpeg-rgb-declared","22x14 8/8 RGB 070cdf1958404a12" },
			{ "jpeg-ybr-partial-422","null" },
			{ "jpeg-progressive-gray","24x16 8/8 MONOCHROME2 984e782c9f840fd3" },
			{ "encapsulated-uncompressed","throws" },
			{ "unsupported-1.2.840.10008.1.2.4.51","throws" },
			{ "unsupported-1.2.840.10008.1.2.4.57","throws" },
			{ "unsupported-1.2.840.10008.1.2.4.70","throws" },
			{ "unsupported-1.2.840.10008.1.2.4.80","throws" },
			{ "unsupported-1.2.840.10008.1.2.4.81","throws" },
			{ "unsupported-1.2.840.10008.1.2.4.90","throws" },
			{ "unsupported-1.2.840.10008.1.2.4.91","throws" },
			{ "unsupported-1.2.840.10008.1.2.4.201","throws" },
			{ "unsupported-1.2.840.10008.1.2.4.203","throws" },
			{ "unsupported-1.2.840.10008.1.2.4.112","throws" },
			{ "unsupported-1.2.840.10008.1.2.4.100","throws" },
			{ "unsupported-1.2.840.10008.1.2.8.1","throws" },
		};

		///<summary>What a BitmapDicom holds, in one line: size, bit depth, photometric interpretation and the first 16 hex digits of the SHA-256 of
		///the raw values (each as two little-endian bytes). "null" for no image.</summary>
		public static string Fingerprint(BitmapDicom bitmapDicom) {
			if(bitmapDicom==null) {
				return "null";
			}
			return bitmapDicom.Width+"x"+bitmapDicom.Height+" "+bitmapDicom.BitsAllocated+"/"+bitmapDicom.BitsStored+" "+bitmapDicom.PhotometricInterp+" "
				+HashValues(bitmapDicom.ArrayDataRaw);
		}

		public static string HashValues(ushort[] arrayValues) {
			if(arrayValues==null) {
				return "no-values";
			}
			byte[] bytes=new byte[arrayValues.Length*2];
			for(int i=0;i<arrayValues.Length;i++) {
				bytes[2*i]=(byte)arrayValues[i];
				bytes[2*i+1]=(byte)(arrayValues[i]>>8);
			}
			using(SHA256 sha256=SHA256.Create()) {
				byte[] bytesHash=sha256.ComputeHash(bytes);
				return string.Concat(bytesHash.Take(8).Select(x => x.ToString("x2")));
			}
		}

		///<summary>True when fo-dicom showed an image for the case.</summary>
		public static bool IsRead(string recorded) {
			return recorded!="null" && recorded!="throws";
		}
	}
}
