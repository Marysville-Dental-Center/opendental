// ODCrypt: open source replacement for the prebuilt ODCrypt.dll.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ODCrypt {
	///<summary>String compression helpers. Despite the class name nothing here encrypts: the only encryption method was used for an upload to
	///Open Dental's HQ servers, which this project no longer talks to.</summary>
	public static class Encryption {
		///<summary>Compresses text: UTF-8, then gzip, then base64. Returns false and sets errorMsg on failure.</summary>
		public static bool CompressString(string text,out string compressedText,ref string errorMsg) {
			compressedText="";
			if(text==null) {
				errorMsg="No text to compress.";
				return false;
			}
			try {
				byte[] arrayPlain=Encoding.UTF8.GetBytes(text);
				using(MemoryStream memoryStream=new MemoryStream()) {
					using(GZipStream gzipStream=new GZipStream(memoryStream,CompressionMode.Compress,leaveOpen:true)) {
						gzipStream.Write(arrayPlain,0,arrayPlain.Length);
					}
					compressedText=Convert.ToBase64String(memoryStream.ToArray());
				}
				return true;
			}
			catch(Exception ex) {
				errorMsg=ex.Message;
				compressedText="";
				return false;
			}
		}

		///<summary>Reverses CompressString. Returns false and sets errorMsg if compressedText is not base64-encoded gzip data, so callers can fall
		///back to treating the input as uncompressed.</summary>
		public static bool DecompressString(string compressedText,out string text,ref string errorMsg) {
			text="";
			if(string.IsNullOrEmpty(compressedText)) {
				errorMsg="No text to decompress.";
				return false;
			}
			try {
				byte[] arrayCompressed=Convert.FromBase64String(compressedText);
				//Check the gzip magic number first so plain text that happens to be valid base64 is rejected quickly.
				if(arrayCompressed.Length<18 || arrayCompressed[0]!=0x1f || arrayCompressed[1]!=0x8b) {
					errorMsg="Text is not gzip compressed.";
					return false;
				}
				using(MemoryStream memoryStreamIn=new MemoryStream(arrayCompressed))
				using(GZipStream gzipStream=new GZipStream(memoryStreamIn,CompressionMode.Decompress))
				using(MemoryStream memoryStreamOut=new MemoryStream()) {
					gzipStream.CopyTo(memoryStreamOut);
					text=Encoding.UTF8.GetString(memoryStreamOut.ToArray());
				}
				return true;
			}
			catch(Exception ex) {
				errorMsg=ex.Message;
				text="";
				return false;
			}
		}

		///<summary>Not supported. The original encrypted a payload for Open Dental's HQ web service, which has been removed from this project.
		///Always throws NotSupportedException.</summary>
		public static bool EncryptString(string text,bool useHqKey,out string encryptedText,ref string errorMsg) {
			throw new NotSupportedException("Encryption.EncryptString was only used for uploads to Open Dental's HQ servers and is not available.");
		}
	}
}
