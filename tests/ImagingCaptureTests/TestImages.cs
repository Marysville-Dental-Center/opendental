// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using ImagingDeviceManager.Capture;

namespace ImagingCapture.Tests {
	///<summary>Made-up images as TwainCaptureSession raises them, for the saver and Test capture tests: packed DIBs (as a native transfer gives
	///them) and 16-bit samples (as a memory transfer gives them).</summary>
	internal static class TestImages {
		///<summary>Capture metadata of an 8-bit image, made-up values.</summary>
		public const string Json8Bit=@"{""schema"":""capture-metadata/1"",""manufacturer"":""Example Imaging"",""model"":""M2"",""serial"":""SN-000123"",
""captureId"":""c-7f3a91"",""sequence"":3,""acquiredAt"":""2026-10-31T09:15:30.123+00:00"",""pixelSpacingMm"":[0.02,0.02],""bitsStored"":8,
""effectiveBits"":null,""byteOrder"":null,""photometric"":""MONOCHROME2"",""window"":null,""rendering"":""vendor8"",""detectorType"":null,
""redelivered"":false,""configuredExposure"":{""kv"":70,""ma"":7,""s"":0.125,""source"":""configured""},""dsVersion"":""1.1.0"",""vendor"":{}}";

		///<summary>Capture metadata of an 8-bit image whose sensor software rotated it (adjustments not neutral).</summary>
		public const string Json8BitRotated=@"{""schema"":""capture-metadata/1"",""manufacturer"":""Example Imaging"",""model"":null,""serial"":""SN-9"",
""captureId"":""c-1"",""sequence"":1,""acquiredAt"":""2026-10-31T09:00:00.000Z"",""bitsStored"":8,""rendering"":""vendor8"",
""adjustments"":{""brightness"":50,""contrast"":50,""rotate"":90,""flip"":""none""},""redelivered"":false,""configuredExposure"":null}";

		///<summary>Capture metadata of a 16-bit image with effectiveBits 12.</summary>
		public const string Json16Bit12=@"{""schema"":""capture-metadata/1"",""manufacturer"":""Example Imaging"",""model"":null,""serial"":""SN-000123"",
""captureId"":""c-16"",""sequence"":2,""acquiredAt"":""2026-10-31T09:20:00.000+00:00"",""pixelSpacingMm"":[0.02,0.02],""bitsStored"":16,
""effectiveBits"":12,""byteOrder"":""LE"",""photometric"":""MONOCHROME2"",""window"":{""low"":100,""high"":4000},""rendering"":""original"",
""detectorType"":null,""redelivered"":false,""configuredExposure"":null,""dsVersion"":""1.1.0""}";

		///<summary>An 8 bits per pixel DIB with a 256-entry grey palette, bottom-up (positive height) unless isTopDown, rows padded to 4 bytes,
		///as twain-contract 1.1 section 6.1 describes. grey(x,y) gives each pixel, y counted from the top.</summary>
		public static byte[] Dib8Gray(int width,int height,Func<int,int,byte> grey,bool isTopDown=false,int pelsPerMeter=50000) {
			int stride=(width+3)/4*4;
			using(MemoryStream memoryStream=new MemoryStream())
			using(BinaryWriter binaryWriter=new BinaryWriter(memoryStream)) {
				WriteHeader(binaryWriter,width,isTopDown ? -height : height,8,256,pelsPerMeter);
				for(int i=0;i<256;i++) {
					binaryWriter.Write(new byte[] { (byte)i,(byte)i,(byte)i,0 });
				}
				for(int row=0;row<height;row++) {
					int y=isTopDown ? row : height-1-row;
					byte[] arrayRow=new byte[stride];
					for(int x=0;x<width;x++) {
						arrayRow[x]=grey(x,y);
					}
					binaryWriter.Write(arrayRow);
				}
				return memoryStream.ToArray();
			}
		}

		///<summary>A 24 bits per pixel DIB, bottom-up. color(x,y) gives B, G, R.</summary>
		public static byte[] Dib24(int width,int height,Func<int,int,byte[]> color) {
			int stride=(width*3+3)/4*4;
			using(MemoryStream memoryStream=new MemoryStream())
			using(BinaryWriter binaryWriter=new BinaryWriter(memoryStream)) {
				WriteHeader(binaryWriter,width,height,24,0,0);
				for(int row=0;row<height;row++) {
					int y=height-1-row;
					byte[] arrayRow=new byte[stride];
					for(int x=0;x<width;x++) {
						byte[] bgr=color(x,y);
						arrayRow[x*3]=bgr[0];
						arrayRow[x*3+1]=bgr[1];
						arrayRow[x*3+2]=bgr[2];
					}
					binaryWriter.Write(arrayRow);
				}
				return memoryStream.ToArray();
			}
		}

		///<summary>A 1 bit per pixel DIB with a black and white palette, bottom-up. isOn(x,y) picks palette entry 1 (white).</summary>
		public static byte[] Dib1(int width,int height,Func<int,int,bool> isOn) {
			int stride=(width+31)/32*4;
			using(MemoryStream memoryStream=new MemoryStream())
			using(BinaryWriter binaryWriter=new BinaryWriter(memoryStream)) {
				WriteHeader(binaryWriter,width,height,1,2,0);
				binaryWriter.Write(new byte[] { 0,0,0,0,255,255,255,0 });
				for(int row=0;row<height;row++) {
					int y=height-1-row;
					byte[] arrayRow=new byte[stride];
					for(int x=0;x<width;x++) {
						if(isOn(x,y)) {
							arrayRow[x/8]|=(byte)(0x80>>(x%8));
						}
					}
					binaryWriter.Write(arrayRow);
				}
				return memoryStream.ToArray();
			}
		}

		public static void WriteHeader(BinaryWriter binaryWriter,int width,int height,int bitCount,int clrUsed,int pelsPerMeter,int compression=0) {
			binaryWriter.Write(40);
			binaryWriter.Write(width);
			binaryWriter.Write(height);
			binaryWriter.Write((ushort)1);
			binaryWriter.Write((ushort)bitCount);
			binaryWriter.Write(compression);
			binaryWriter.Write(0);
			binaryWriter.Write(pelsPerMeter);
			binaryWriter.Write(pelsPerMeter);
			binaryWriter.Write(clrUsed);
			binaryWriter.Write(0);
		}

		///<summary>An 8-bit image as a native transfer in an 8-bit enable delivers it.</summary>
		public static AcquiredImage Image8(int width,int height,Func<int,int,byte> grey,string metadataJson=null) {
			return new AcquiredImage {
				Width=width,Height=height,BitsPerPixel=8,BitsPerSample=8,PixelType=AcquiredImage.PixelTypeGray,
				PixelFlavor=AcquiredImage.PixelFlavorChocolate,XDpi=1270,YDpi=1270,Dib=Dib8Gray(width,height,grey),MetadataJson=metadataJson,
				ReceivedAt=new DateTime(2026,10,31,10,0,5),EnableId=1,Kind=AcquiredImageKind.Normal,RequestedBits=8,
				TransferMechanism=TwainTransferMechanism.Native,SourceName="Example Sensor DS",IndexInEnable=1,
			};
		}

		///<summary>A 16-bit image as a memory transfer in a 16-bit enable delivers it.</summary>
		public static AcquiredImage Image16(int width,int height,Func<int,int,ushort> sample,string metadataJson=null,
			int pixelFlavor=AcquiredImage.PixelFlavorChocolate)
		{
			ushort[] arrayPixels=new ushort[width*height];
			for(int y=0;y<height;y++) {
				for(int x=0;x<width;x++) {
					arrayPixels[y*width+x]=sample(x,y);
				}
			}
			return new AcquiredImage {
				Width=width,Height=height,BitsPerPixel=16,BitsPerSample=16,PixelType=AcquiredImage.PixelTypeGray,PixelFlavor=pixelFlavor,
				XDpi=1270,YDpi=1270,Gray16=arrayPixels,MetadataJson=metadataJson,ReceivedAt=new DateTime(2026,10,31,10,0,6),EnableId=2,
				Kind=AcquiredImageKind.Normal,RequestedBits=16,BitDepthInEffect=16,ByteOrder16=TwainByteOrder.LE,
				TransferMechanism=TwainTransferMechanism.Memory,SourceName="Example Sensor DS",IndexInEnable=1,
			};
		}
	}
}
