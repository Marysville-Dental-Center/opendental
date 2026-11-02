// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;

namespace OpenPmsDentalBusiness {
	///<summary>Reads one DICOM file into a BitmapDicom: the raw pixel values before windowing, the size, the bit depth and the photometric
	///interpretation, which is everything the Imaging module, mounts, thumbnails, printing and export use (design 7.8, CAP-W1 and CAP-DR).
	///Every DICOM display path reads through DicomHelper.GetFromFile, GetFromStream or GetFromBase64, and those use DicomHelper.PixelReader:
	///the in-house DicomSubsetReader first, for the uncompressed Explicit VR Little Endian subset the capture writer emits, then the in-house
	///DicomImageReader (DicomHelper.FullReader) for every file the subset reader does not read (docs/imaging/dicom-reader.md). No reader
	///throws. Documents.GetThumbnail and ImageHelper.GetImageCropped load .dcm documents the same way, through ImageStore.OpenBitmapDicom.</summary>
	public interface IDicomPixelReader {
		///<summary>The pixels of the DICOM file at path, or null when this reader does not read it.</summary>
		BitmapDicom ReadFile(string path);
		///<summary>The pixels of the DICOM file in stream, read from its current position, or null when this reader does not read it.</summary>
		BitmapDicom ReadStream(Stream stream);
	}

	///<summary>Asks one reader, then the other when the first returns null. DicomHelper.PixelReader is the in-house subset reader followed by
	///the in-house full reader, so a file the subset reader declines (another transfer syntax, compressed pixels, color, signed or multi-frame
	///data, or a file it finds malformed) is read by the full reader from the start.</summary>
	public sealed class DicomPixelReaderChain : IDicomPixelReader {
		///<summary>A stream that cannot seek is copied into memory up to this many bytes, so the second reader can start again from the
		///beginning. A longer one goes to the second reader only, as the copy followed by the rest of the stream, which the full reader reads
		///forward without holding it. The program itself passes only seekable streams (files and memory).</summary>
		public const int MaxReplayBytes=32*1024*1024;

		private readonly IDicomPixelReader _readerFirst;
		private readonly IDicomPixelReader _readerFallback;

		public DicomPixelReaderChain(IDicomPixelReader readerFirst,IDicomPixelReader readerFallback) {
			_readerFirst=readerFirst;
			_readerFallback=readerFallback;
		}

		public BitmapDicom ReadFile(string path) {
			BitmapDicom bitmapDicom=_readerFirst.ReadFile(path);
			if(bitmapDicom!=null) {
				return bitmapDicom;
			}
			return _readerFallback.ReadFile(path);
		}

		///<summary>The fallback reads from the same position the first reader started at. A stream that cannot seek is copied into memory
		///first (at most MaxReplayBytes of it), because the first reader consumes what it reads.</summary>
		public BitmapDicom ReadStream(Stream stream) {
			if(stream==null) {
				return _readerFallback.ReadStream(stream);
			}
			Stream streamReplay;
			bool isFirstReaderPossible;
			try {
				streamReplay=GetReplayableStream(stream,out isFirstReaderPossible);
			}
			catch(Exception) {
				return null;//the stream could not be read
			}
			if(!isFirstReaderPossible) {
				return _readerFallback.ReadStream(streamReplay);
			}
			long position=streamReplay.Position;
			BitmapDicom bitmapDicom=_readerFirst.ReadStream(streamReplay);
			if(bitmapDicom!=null) {
				return bitmapDicom;
			}
			streamReplay.Position=position;
			return _readerFallback.ReadStream(streamReplay);
		}

		///<summary>The stream itself when it can seek. Otherwise a copy in memory of up to MaxReplayBytes; when the stream is longer, the copy
		///followed by the rest of the stream, which cannot seek, and isFirstReaderPossible is false.</summary>
		public static Stream GetReplayableStream(Stream stream,out bool isFirstReaderPossible) {
			isFirstReaderPossible=true;
			if(stream.CanSeek) {
				return stream;
			}
			MemoryStream memoryStream=new MemoryStream();
			byte[] buffer=new byte[81920];
			long total=0;
			while(total<=MaxReplayBytes) {
				int countRead=stream.Read(buffer,0,(int)Math.Min(buffer.Length,MaxReplayBytes+1L-total));
				if(countRead<=0) {
					memoryStream.Position=0;
					return memoryStream;
				}
				memoryStream.Write(buffer,0,countRead);
				total+=countRead;
			}
			memoryStream.Position=0;
			isFirstReaderPossible=false;
			return new ConcatenatedStream(memoryStream,stream);
		}

		///<summary>Reads one stream to its end, then the other. Forward only.</summary>
		private sealed class ConcatenatedStream : Stream {
			private readonly Stream _streamFirst;
			private readonly Stream _streamSecond;
			private bool _isFirstDone;

			public ConcatenatedStream(Stream streamFirst,Stream streamSecond) {
				_streamFirst=streamFirst;
				_streamSecond=streamSecond;
			}

			public override bool CanRead => true;
			public override bool CanSeek => false;
			public override bool CanWrite => false;
			public override long Length => throw new NotSupportedException();

			public override long Position {
				get => throw new NotSupportedException();
				set => throw new NotSupportedException();
			}

			public override int Read(byte[] buffer,int offset,int count) {
				if(!_isFirstDone) {
					int countRead=_streamFirst.Read(buffer,offset,count);
					if(countRead>0) {
						return countRead;
					}
					_isFirstDone=true;
				}
				return _streamSecond.Read(buffer,offset,count);
			}

			public override void Flush() {
			}

			public override long Seek(long offset,SeekOrigin origin) {
				throw new NotSupportedException();
			}

			public override void SetLength(long value) {
				throw new NotSupportedException();
			}

			public override void Write(byte[] buffer,int offset,int count) {
				throw new NotSupportedException();
			}
		}
	}
}
