// IDicomPixelReader and DicomHelper's reader order (CAP-DR, design 7.8): the in-house subset reader first, fo-dicom for everything else.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Linq;
using Dicom;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace DicomReaderTests {
	[TestClass]
	public class DicomPixelReaderChainTests {
		private sealed class FakeReader : IDicomPixelReader {
			public BitmapDicom Result;
			public int CountFile;
			public int CountStream;
			public long PositionSeen=-1;

			public BitmapDicom ReadFile(string path) {
				CountFile++;
				return Result;
			}

			public BitmapDicom ReadStream(Stream stream) {
				CountStream++;
				PositionSeen=stream.Position;
				stream.ReadByte();//consume something, as a real reader does
				stream.ReadByte();
				return Result;
			}
		}

		private sealed class NonSeekableStream : MemoryStream {
			public NonSeekableStream(byte[] bytes) : base(bytes) {
			}

			public override bool CanSeek => false;

			public override long Position {
				get => throw new NotSupportedException();
				set => throw new NotSupportedException();
			}
		}

		[TestMethod]
		public void Chain_FirstReaderWins_FallbackOnlyWhenItDeclines() {
			BitmapDicom bitmapDicomFirst=new BitmapDicom();
			BitmapDicom bitmapDicomSecond=new BitmapDicom();
			FakeReader readerFirst=new FakeReader { Result=bitmapDicomFirst };
			FakeReader readerSecond=new FakeReader { Result=bitmapDicomSecond };
			DicomPixelReaderChain chain=new DicomPixelReaderChain(readerFirst,readerSecond);
			Assert.AreSame(bitmapDicomFirst,chain.ReadFile("x.dcm"));
			using(MemoryStream memoryStream=new MemoryStream(new byte[10])) {
				Assert.AreSame(bitmapDicomFirst,chain.ReadStream(memoryStream));
			}
			Assert.AreEqual(0,readerSecond.CountFile+readerSecond.CountStream,"fo-dicom is not asked when the subset reader read the file.");
			readerFirst.Result=null;
			Assert.AreSame(bitmapDicomSecond,chain.ReadFile("x.dcm"));
			using(MemoryStream memoryStream=new MemoryStream(new byte[10])) {
				memoryStream.Position=3;
				Assert.AreSame(bitmapDicomSecond,chain.ReadStream(memoryStream));
				Assert.AreEqual(3,readerFirst.PositionSeen);
				Assert.AreEqual(3,readerSecond.PositionSeen,"The fallback starts where the first reader started.");
			}
		}

		[TestMethod]
		public void Chain_NonSeekableStream_IsBufferedSoTheFallbackSeesItAll() {
			FakeReader readerFirst=new FakeReader();
			FakeReader readerSecond=new FakeReader { Result=new BitmapDicom() };
			DicomPixelReaderChain chain=new DicomPixelReaderChain(readerFirst,readerSecond);
			using(NonSeekableStream stream=new NonSeekableStream(new byte[] { 1,2,3,4,5 })) {
				Assert.IsNotNull(chain.ReadStream(stream));
			}
			Assert.AreEqual(0,readerSecond.PositionSeen);
		}

		[TestMethod]
		public void DicomHelper_UsesTheSubsetReaderFirst_ThenFoDicom() {
			Assert.IsInstanceOfType(DicomHelper.SubsetReader,typeof(DicomSubsetReader));
			Assert.IsInstanceOfType(DicomHelper.PixelReader,typeof(DicomPixelReaderChain));
			Assert.AreNotSame(DicomHelper.SubsetReader,DicomHelper.FoDicomReader);
		}

		///<summary>Runs DicomHelper.GetFromFile in a fresh application domain and reports what it read and whether fo-dicom (Dicom.Core) was loaded.</summary>
		public sealed class IsolatedRead : MarshalByRefObject {
			public string Run(string path) {
				BitmapDicom bitmapDicom=DicomHelper.GetFromFile(path);
				bool isFoDicomLoaded=AppDomain.CurrentDomain.GetAssemblies().Any(x => x.GetName().Name=="Dicom.Core");
				string read=bitmapDicom==null ? "null" : bitmapDicom.Width+"x"+bitmapDicom.Height+" sum "+bitmapDicom.ArrayDataRaw.Sum(x => (long)x);
				return read+"|"+(isFoDicomLoaded ? "fo-dicom loaded" : "fo-dicom not loaded");
			}
		}

		private static string RunIsolated(string path) {
			string folder=Path.GetDirectoryName(typeof(IsolatedRead).Assembly.Location);
			AppDomainSetup appDomainSetup=new AppDomainSetup();
			appDomainSetup.ApplicationBase=folder;
			string pathConfig=typeof(IsolatedRead).Assembly.Location+".config";
			if(File.Exists(pathConfig)) {
				appDomainSetup.ConfigurationFile=pathConfig;
			}
			AppDomain appDomain=AppDomain.CreateDomain("DicomReaderTests_"+Guid.NewGuid().ToString("N"),null,appDomainSetup);
			try {
				IsolatedRead isolatedRead=(IsolatedRead)appDomain.CreateInstanceAndUnwrap(typeof(IsolatedRead).Assembly.FullName,typeof(IsolatedRead).FullName);
				return isolatedRead.Run(path);
			}
			finally {
				AppDomain.Unload(appDomain);
			}
		}

		[TestMethod]
		public void CaptureWriterFile_IsReadWithoutFoDicom_OtherFilesStillWithIt() {
			string folder=Path.Combine(Path.GetTempPath(),"dicomreadertests_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			try {
				ushort[] arrayPixels=SyntheticDicom.Ramp(20*10,65535);
				string pathSubset=Path.Combine(folder,"subset.dcm");
				File.WriteAllBytes(pathSubset,TestFiles.FromCaptureWriter(20,10,arrayPixels,null));
				string pathImplicit=Path.Combine(folder,"implicit.dcm");
				File.WriteAllBytes(pathImplicit,TestFiles.FromFoDicom(DicomTransferSyntax.ImplicitVRLittleEndian,10,20,16,16,0,"MONOCHROME2",
					SyntheticDicom.Words(arrayPixels)));
				long sum=arrayPixels.Sum(x => (long)x);
				Assert.AreEqual("20x10 sum "+sum+"|fo-dicom not loaded",RunIsolated(pathSubset),"The subset is read by the in-house reader alone.");
				Assert.AreEqual("20x10 sum "+sum+"|fo-dicom loaded",RunIsolated(pathImplicit),"Other files still go to fo-dicom.");
			}
			finally {
				try {
					Directory.Delete(folder,true);
				}
				catch(Exception) {
				}
			}
		}
	}
}
