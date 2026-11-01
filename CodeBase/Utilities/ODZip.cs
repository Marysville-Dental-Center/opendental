// Zip archives with the framework's System.IO.Compression, in place of DotNetZip (Ionic.Zip: abandoned, MS-PL, CVE-2024-48510).
// Extraction never writes outside the folder it extracts to.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace CodeBase {
	///<summary>Creating and extracting zip archives. Entry names use '/' as the separator. Every extraction goes through GetSafeExtractPath, which
	///refuses absolute paths, drive letters, alternate data streams and ".." parts that would leave the target folder ("zip slip").</summary>
	public static class ODZip {
		///<summary>Builds a zip archive in memory holding the entries in the given order, deflate-compressed.</summary>
		public static byte[] CreateArchive(IEnumerable<KeyValuePair<string,byte[]>> entries) {
			using(MemoryStream memoryStream=new MemoryStream()) {
				using(ZipArchive zipArchive=new ZipArchive(memoryStream,ZipArchiveMode.Create,leaveOpen:true)) {
					foreach(KeyValuePair<string,byte[]> keyValuePair in entries) {
						AddEntry(zipArchive,keyValuePair.Key,keyValuePair.Value);
					}
				}
				return memoryStream.ToArray();
			}
		}

		///<summary>Writes the zip archive holding the entries in the given order to the file, replacing it if it exists.</summary>
		public static void CreateArchiveFile(string zipFilePath,IEnumerable<KeyValuePair<string,byte[]>> entries) {
			File.WriteAllBytes(zipFilePath,CreateArchive(entries));
		}

		///<summary>Adds one entry with the bytes as its content. Backslashes in the name become '/'.</summary>
		public static void AddEntry(ZipArchive zipArchive,string entryName,byte[] arrayBytes) {
			ZipArchiveEntry zipArchiveEntry=zipArchive.CreateEntry(entryName.Replace('\\','/'),CompressionLevel.Optimal);
			using(Stream stream=zipArchiveEntry.Open()) {
				stream.Write(arrayBytes,0,arrayBytes.Length);
			}
		}

		///<summary>The bytes DotNetZip stored for an entry it was given as a string: the text in the system's default (ANSI) code page. Callers that
		///zipped text with DotNetZip use this so the archive content stays the same.</summary>
		public static byte[] GetDefaultEncodingBytes(string text) {
			return Encoding.Default.GetBytes(text??"");
		}

		///<summary>The entry name for a file added together with its folders, as DotNetZip's AddFile(path) stored it: the full path without its drive
		///or share root, with '/' separators. Example: C:\Exports\claims\x.txt gives Exports/claims/x.txt.</summary>
		public static string GetEntryNameWithFolders(string filePath) {
			string pathFull=Path.GetFullPath(filePath);
			string root=Path.GetPathRoot(pathFull)??"";
			return pathFull.Substring(root.Length).TrimStart('\\','/').Replace('\\','/');
		}

		///<summary>True when the entry is a folder entry (its name ends with a separator) rather than a file.</summary>
		public static bool IsDirectory(ZipArchiveEntry zipArchiveEntry) {
			return zipArchiveEntry.FullName.EndsWith("/") || zipArchiveEntry.FullName.EndsWith("\\");
		}

		///<summary>The full path the entry would be extracted to under folderDest, or null when that path would not be inside folderDest: an empty
		///name, an absolute or rooted path, a drive letter or other ':' (alternate data streams), a ".." that climbs out, a name that resolves to the
		///folder itself, or a name that is not a valid path. Both '/' and '\' count as separators.</summary>
		public static string GetSafeExtractPath(string folderDest,string entryName) {
			if(string.IsNullOrWhiteSpace(folderDest) || string.IsNullOrWhiteSpace(entryName)) {
				return null;
			}
			string relative=entryName.Replace('/','\\');
			if(relative.StartsWith("\\") || relative.Contains(":")) {
				return null;
			}
			try {
				if(Path.IsPathRooted(relative)) {
					return null;
				}
				string folderFull=Path.GetFullPath(folderDest).TrimEnd('\\')+"\\";
				string pathFull=Path.GetFullPath(Path.Combine(folderFull,relative));
				if(!pathFull.StartsWith(folderFull,StringComparison.OrdinalIgnoreCase) || pathFull.Length<=folderFull.Length) {
					return null;
				}
				return pathFull;
			}
			catch(Exception ex) {
				ex.DoNothing();//Invalid characters or a path that is too long: never extracted.
				return null;
			}
		}

		///<summary>Extracts one file entry under folderDest, creating its folders. Throws an IOException, without writing anything, when the entry's
		///path would leave folderDest (see GetSafeExtractPath), or when the file exists and overwrite is false. Returns the full path written.</summary>
		public static string ExtractEntry(ZipArchiveEntry zipArchiveEntry,string folderDest,bool overwrite) {
			string pathDest=GetSafeExtractPath(folderDest,zipArchiveEntry.FullName);
			if(pathDest==null) {
				throw new IOException("Unsafe path in the zip archive, not extracted: "+zipArchiveEntry.FullName);
			}
			if(!overwrite && File.Exists(pathDest)) {
				throw new IOException("The file already exists: "+pathDest);
			}
			Directory.CreateDirectory(Path.GetDirectoryName(pathDest));
			using(Stream stream=zipArchiveEntry.Open())
			using(FileStream fileStream=new FileStream(pathDest,FileMode.Create,FileAccess.Write)) {
				stream.CopyTo(fileStream);
			}
			return pathDest;
		}

		///<summary>Extracts every file entry of the archive under folderDest. Every entry's path is checked first, so an archive with any unsafe
		///path throws an IOException before anything is written. Folder entries only create their folder. Returns the full paths written.</summary>
		public static List<string> ExtractAll(Stream streamArchive,string folderDest,bool overwrite) {
			List<string> listPaths=new List<string>();
			using(ZipArchive zipArchive=new ZipArchive(streamArchive,ZipArchiveMode.Read,leaveOpen:true)) {
				List<ZipArchiveEntry> listUnsafe=zipArchive.Entries.Where(x => GetSafeExtractPath(folderDest,x.FullName)==null).ToList();
				if(listUnsafe.Count>0) {
					throw new IOException("Unsafe path in the zip archive, nothing was extracted: "+string.Join(", ",listUnsafe.Select(x => x.FullName)));
				}
				Directory.CreateDirectory(folderDest);
				foreach(ZipArchiveEntry zipArchiveEntry in zipArchive.Entries) {
					if(IsDirectory(zipArchiveEntry)) {
						Directory.CreateDirectory(GetSafeExtractPath(folderDest,zipArchiveEntry.FullName));
						continue;
					}
					listPaths.Add(ExtractEntry(zipArchiveEntry,folderDest,overwrite));
				}
			}
			return listPaths;
		}

		///<summary>Extracts every file entry of the zip file under folderDest. See ExtractAll(Stream,...).</summary>
		public static List<string> ExtractAll(string zipFilePath,string folderDest,bool overwrite) {
			using(FileStream fileStream=new FileStream(zipFilePath,FileMode.Open,FileAccess.Read,FileShare.Read)) {
				return ExtractAll(fileStream,folderDest,overwrite);
			}
		}

		///<summary>Reads the entry's content, refusing (EntryTooLargeException) one that expands to more than maxBytes, whatever its header claims.
		///</summary>
		public static byte[] ReadEntry(ZipArchiveEntry zipArchiveEntry,long maxBytes) {
			using(Stream stream=zipArchiveEntry.Open())
			using(MemoryStream memoryStream=new MemoryStream()) {
				CopyBounded(stream,memoryStream,maxBytes);
				return memoryStream.ToArray();
			}
		}

		///<summary>Copies the source to the destination, throwing an EntryTooLargeException as soon as more than maxBytes come out of the source,
		///before writing them. Returns the bytes copied. Guards against an entry that inflates beyond the size it claims.</summary>
		public static long CopyBounded(Stream streamSource,Stream streamDest,long maxBytes) {
			byte[] arrayBuffer=new byte[81920];
			long total=0;
			while(true) {
				int count=streamSource.Read(arrayBuffer,0,arrayBuffer.Length);
				if(count<=0) {
					return total;
				}
				total+=count;
				if(total>maxBytes) {
					throw new EntryTooLargeException(maxBytes);
				}
				streamDest.Write(arrayBuffer,0,count);
			}
		}

		///<summary>A zip entry inflated to more bytes than allowed.</summary>
		[Serializable]
		public class EntryTooLargeException:IOException {
			public EntryTooLargeException(long maxBytes):base("The zip entry is larger than the "+maxBytes+" bytes allowed.") {
			}
		}
	}
}
