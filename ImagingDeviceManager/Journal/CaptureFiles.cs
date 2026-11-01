// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ImagingDeviceManager.Imaging;

namespace ImagingDeviceManager.Journal {
	///<summary>One field of a sidecar: a "Key=Value" line.</summary>
	internal struct SidecarField {
		public string Key;
		public string Value;

		public SidecarField(string key,string value) {
			Key=key;
			Value=value;
		}
	}

	///<summary>The image and sidecar files of the journal and the quarantine. An image is the packed DIB exactly as the library received it,
	///written as a .bmp file (a 14-byte BITMAPFILEHEADER in front of it): lossless, byte for byte what the caller got, with no codec
	///involved, and readable by Windows, by Open Dental's Import and by any viewer. Its sidecar is a UTF-8 text file with the same base
	///name and ".txt": one "Key=Value" per line, ';' lines are comments.</summary>
	internal static class CaptureFiles {
		public const string ImageExtension=".bmp";
		///<summary>The image extensions journal rotation counts: a DIB copy, and a 16-bit copy (Gray16Tiff).</summary>
		public static readonly string[] ArrayImageExtensions={ ImageExtension,Imaging.Gray16Tiff.Extension };
		public const string SidecarExtension=".txt";
		private const int BitmapFileHeaderSize=14;
		private static int _sequence;
		private static readonly Regex _regexBaseName=new Regex(@"^\d{8}-\d{6}-\d{3}-\d{6}-[0-9a-f]{4}$",RegexOptions.CultureInvariant);

		///<summary>"20261025-143012-123-000017-a3f9": the local receive time to the millisecond, then a counter so that images received in
		///the same millisecond keep their order, then random hex against other processes. Names sort in the order images arrived.</summary>
		public static string NewBaseName(DateTime receivedAt) {
			int sequence=Interlocked.Increment(ref _sequence)%1000000;
			string random=Guid.NewGuid().ToString("N").Substring(0,4);
			return receivedAt.ToString("yyyyMMdd-HHmmss-fff",CultureInfo.InvariantCulture)+"-"+sequence.ToString("D6",CultureInfo.InvariantCulture)
				+"-"+random;
		}

		///<summary>True for a base name NewBaseName makes, without a suffix. Rotation touches only those.</summary>
		public static bool IsCaptureBaseName(string baseName) {
			return baseName!=null && _regexBaseName.IsMatch(baseName);
		}

		///<summary>The .bmp file of a DIB: BITMAPFILEHEADER ("BM", file size, reserved, offset of the pixels), then the packed DIB unchanged.</summary>
		public static byte[] ToBmpBytes(DibImage dib) {
			byte[] bytes=new byte[BitmapFileHeaderSize+dib.Data.Length];
			bytes[0]=(byte)'B';
			bytes[1]=(byte)'M';
			Array.Copy(BitConverter.GetBytes(bytes.Length),0,bytes,2,4);
			Array.Copy(BitConverter.GetBytes(BitmapFileHeaderSize+dib.PixelOffset),0,bytes,10,4);
			Buffer.BlockCopy(dib.Data,0,bytes,BitmapFileHeaderSize,dib.Data.Length);
			return bytes;
		}

		public static string Sha256Hex(byte[] bytes) {
			using(SHA256 sha256=SHA256.Create()) {
				byte[] hash=sha256.ComputeHash(bytes);
				StringBuilder stringBuilder=new StringBuilder(hash.Length*2);
				foreach(byte b in hash) {
					stringBuilder.Append(b.ToString("x2",CultureInfo.InvariantCulture));
				}
				return stringBuilder.ToString();
			}
		}

		///<summary>A value for a sidecar line: cut at the first NUL (strings from native code may carry one), other control characters
		///replaced by spaces, trimmed. "" for null.</summary>
		public static string Clean(string value) {
			if(value==null) {
				return "";
			}
			int idxNul=value.IndexOf('\0');
			if(idxNul>=0) {
				value=value.Substring(0,idxNul);
			}
			StringBuilder stringBuilder=new StringBuilder(value.Length);
			foreach(char c in value) {
				stringBuilder.Append(char.IsControl(c) ? ' ' : c);
			}
			return stringBuilder.ToString().Trim();
		}

		///<summary>"2026-10-25T14:30:12.123+02:00": a receive time in a sidecar, local with its offset.</summary>
		public static string FormatTimestamp(DateTime dateTime) {
			return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz",CultureInfo.InvariantCulture);
		}

		///<summary>The sidecar text: comment lines, then one "Key=Value" line per field.</summary>
		public static string BuildSidecar(IEnumerable<string> listComments,IEnumerable<SidecarField> listFields) {
			StringBuilder stringBuilder=new StringBuilder();
			foreach(string comment in listComments) {
				stringBuilder.Append("; ").Append(Clean(comment)).Append("\r\n");
			}
			foreach(SidecarField sidecarField in listFields) {
				stringBuilder.Append(Clean(sidecarField.Key)).Append('=').Append(Clean(sidecarField.Value)).Append("\r\n");
			}
			return stringBuilder.ToString();
		}

		///<summary>The fields of a sidecar file, "" for a missing key. Comment and other lines are skipped. Never throws; empty when the
		///file cannot be read.</summary>
		public static Dictionary<string,string> ReadSidecar(string path) {
			Dictionary<string,string> dictFields=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			try {
				foreach(string line in File.ReadAllLines(path,Encoding.UTF8)) {
					if(line.StartsWith(";")) {
						continue;
					}
					int idxEquals=line.IndexOf('=');
					if(idxEquals<=0) {
						continue;
					}
					dictFields[line.Substring(0,idxEquals).Trim()]=line.Substring(idxEquals+1).Trim();
				}
			}
			catch {
			}
			return dictFields;
		}

		///<summary>Writes an image and its sidecar into folder under a new base name, followed by baseNameSuffix (for example the journal's
		///"-only"); funcSidecar gets the base name and returns the sidecar's text. The image is written to a temporary file and then moved
		///to its name, so a crash never leaves half an image under a capture name; an existing file is never replaced. Returns the image's
		///path, or null with error when the image could not be written. When only the sidecar fails, the image is kept (it may be the only
		///copy), the path is returned and error says so.</summary>
		public static string WritePair(string folder,DateTime receivedAt,byte[] bytesImage,Func<string,string> funcSidecar,out string error,
			string baseNameSuffix="",string imageExtension=ImageExtension)
		{
			error="";
			string pathImage=null;
			string baseName=null;
			Exception exceptionLast=null;
			for(int attempt=0;attempt<3 && pathImage==null;attempt++) {
				baseName=NewBaseName(receivedAt)+(baseNameSuffix??"");
				string pathCandidate=Path.Combine(folder,baseName+(imageExtension??ImageExtension));
				string pathTemp=Path.Combine(folder,baseName+".tmp");
				try {
					File.WriteAllBytes(pathTemp,bytesImage);
					File.Move(pathTemp,pathCandidate);//Fails if the name exists.
					pathImage=pathCandidate;
				}
				catch(Exception ex) {
					exceptionLast=ex;
					try {
						File.Delete(pathTemp);
					}
					catch {
					}
				}
			}
			if(pathImage==null) {
				error="The image could not be written to "+folder+": "+(exceptionLast?.Message??"unknown error");
				return null;
			}
			try {
				File.WriteAllText(Path.Combine(folder,baseName+SidecarExtension),funcSidecar(baseName),new UTF8Encoding(false));
			}
			catch(Exception ex) {
				error="The image was written to "+pathImage+", but its sidecar could not be: "+ex.Message;
			}
			return pathImage;
		}
	}
}
