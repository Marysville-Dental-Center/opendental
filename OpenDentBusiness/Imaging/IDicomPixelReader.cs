// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;

namespace OpenDentBusiness {
	///<summary>Reads one DICOM file into a BitmapDicom: the raw pixel values before windowing, the size, the bit depth and the photometric
	///interpretation, which is everything the Imaging module, mounts, thumbnails, printing and export use (design 7.8, CAP-W1 and CAP-DR).
	///Every DICOM display path reads through DicomHelper.GetFromFile, GetFromStream or GetFromBase64, and those use DicomHelper.PixelReader:
	///the in-house DicomSubsetReader first, for the uncompressed Explicit VR Little Endian subset the capture writer emits, then fo-dicom
	///(DicomHelper.FoDicomReader) for every file the subset reader does not read. Documents.GetThumbnail and ImageHelper.GetImageCropped load
	///.dcm documents the same way, through ImageStore.OpenBitmapDicom.</summary>
	public interface IDicomPixelReader {
		///<summary>The pixels of the DICOM file at path, or null when this reader does not read it.</summary>
		BitmapDicom ReadFile(string path);
		///<summary>The pixels of the DICOM file in stream, read from its current position, or null when this reader does not read it.</summary>
		BitmapDicom ReadStream(Stream stream);
	}

	///<summary>Asks one reader, then the other when the first returns null. DicomHelper.PixelReader is the in-house subset reader followed by
	///fo-dicom, so a file the subset reader declines (another transfer syntax, compressed pixels, color, signed or multi-frame data, or a file
	///it finds malformed) is read exactly as it was before the subset reader existed.</summary>
	public sealed class DicomPixelReaderChain : IDicomPixelReader {
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
		///first, because the first reader consumes what it reads.</summary>
		public BitmapDicom ReadStream(Stream stream) {
			if(stream==null) {
				return _readerFallback.ReadStream(stream);
			}
			if(!stream.CanSeek) {
				MemoryStream memoryStream=new MemoryStream();
				stream.CopyTo(memoryStream);
				memoryStream.Position=0;
				stream=memoryStream;
			}
			long position=stream.Position;
			BitmapDicom bitmapDicom=_readerFirst.ReadStream(stream);
			if(bitmapDicom!=null) {
				return bitmapDicom;
			}
			stream.Position=position;
			return _readerFallback.ReadStream(stream);
		}
	}
}
