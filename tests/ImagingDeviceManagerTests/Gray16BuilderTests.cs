// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Runtime.InteropServices;
using ImagingDeviceManager.Imaging;
using ImagingDeviceManager.TwainProtocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImagingDeviceManager.Tests {
	///<summary>Raw 16-bit strips (design 7.1 CAP-3): right-justified, top-down, padded rows and tiles, no shift.</summary>
	[TestClass]
	public class Gray16BuilderTests {
		///<summary>Adds a strip built from samples (row-major, columns wide) at the given offsets, with bytesPerRow per row.</summary>
		private static void AddStrip(Gray16Builder builder,ushort[] samples,int columns,int rows,int xOffset,int yOffset,int bytesPerRow,
			bool isBigEndian=false)
		{
			byte[] bytes=new byte[rows*bytesPerRow];
			for(int i=0;i<bytes.Length;i++) {
				bytes[i]=0xEE;//Padding the builder must skip.
			}
			for(int r=0;r<rows;r++) {
				for(int c=0;c<columns;c++) {
					ushort sample=samples[r*columns+c];
					bytes[r*bytesPerRow+c*2]=(byte)(isBigEndian ? sample>>8 : sample&0xFF);
					bytes[r*bytesPerRow+c*2+1]=(byte)(isBigEndian ? sample&0xFF : sample>>8);
				}
			}
			IntPtr p=Marshal.AllocHGlobal(bytes.Length);
			try {
				Marshal.Copy(bytes,0,p,bytes.Length);
				builder.AddStrip(p,bytes.Length,new TW_IMAGEMEMXFER {
					Compression=Tw.TWCP_NONE,BytesPerRow=(uint)bytesPerRow,Columns=(uint)columns,Rows=(uint)rows,XOffset=(uint)xOffset,YOffset=(uint)yOffset,
				});
			}
			finally {
				Marshal.FreeHGlobal(p);
			}
		}

		[TestMethod]
		public void PaddedRows_ValuesKeptExactly() {
			Gray16Builder builder=new Gray16Builder(3,2,false);
			AddStrip(builder,new ushort[] { 0,1,0xFFFF,4095,0x8001,12 },3,2,0,0,10);
			CollectionAssert.AreEqual(new ushort[] { 0,1,0xFFFF,4095,0x8001,12 },builder.Build(),"No shift, no window.");
		}

		[TestMethod]
		public void TilesWithXAndYOffsets_Assembled() {
			Gray16Builder builder=new Gray16Builder(4,2,false);
			AddStrip(builder,new ushort[] { 1,2,5,6 },2,2,0,0,4);
			AddStrip(builder,new ushort[] { 3,4,7,8 },2,2,2,0,6);
			CollectionAssert.AreEqual(new ushort[] { 1,2,3,4,5,6,7,8 },builder.Build());
		}

		[TestMethod]
		public void BigEndian_Swapped() {
			Gray16Builder builder=new Gray16Builder(2,1,true);
			AddStrip(builder,new ushort[] { 0x1234,0xABCD },2,1,0,0,4,true);
			CollectionAssert.AreEqual(new ushort[] { 0x1234,0xABCD },builder.Build());
		}

		[TestMethod]
		public void UnknownHeight_FromTheRowsReceived() {
			Gray16Builder builder=new Gray16Builder(2,-1,false);
			AddStrip(builder,new ushort[] { 1,2 },2,1,0,0,4);
			AddStrip(builder,new ushort[] { 3,4,5,6 },2,2,0,1,4);
			Assert.AreEqual(3,builder.Height);
			CollectionAssert.AreEqual(new ushort[] { 1,2,3,4,5,6 },builder.Build());
		}

		[TestMethod]
		public void RowsBeyondTheExpectedHeight_AndColumnsBeyondTheWidth_Ignored() {
			Gray16Builder builder=new Gray16Builder(2,1,false);
			AddStrip(builder,new ushort[] { 1,2,9,3,4,9 },3,2,0,0,6);
			CollectionAssert.AreEqual(new ushort[] { 1,2 },builder.Build());
		}

		[TestMethod]
		public void NoRows_Throws() {
			Gray16Builder builder=new Gray16Builder(2,2,false);
			try {
				builder.Build();
				Assert.Fail("No data.");
			}
			catch(ImagingDeviceException ex) {
				Assert.AreEqual(EZTwainErrorCode.EZTEC_BAD_MEMXFER,ex.ErrorCode);
			}
		}

		[TestMethod]
		public void Gray16Dib_ReadRawWithoutTheScanShift() {
			FakePage page=FakePage.CreateGray16(3,2,300,(x,y) => x*0x1111+y*0x0F00);
			DibImage dib=DibImage.FromPackedDib(page.NativeDib);
			ushort[] samples=Gray16Builder.FromGray16Dib(dib);
			CollectionAssert.AreEqual(new ushort[] { 0,0x1111,0x2222,0x0F00,0x2011,0x3122 },samples);
		}
	}
}
