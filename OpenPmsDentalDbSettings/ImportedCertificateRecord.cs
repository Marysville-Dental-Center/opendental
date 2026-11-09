// The record of the client certificate this tool imported itself, so that it removes only that one when the settings stop using it.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text;
using CodeBase;
using DataConnectionBase;

namespace OpenPmsDentalDbSettings {
	///<summary>The thumbprint of the client certificate the tool imported into LocalMachine\My from a .pfx (one whose store entry then uses the
	///private key the import stored: a certificate the store did not have, or one whose entry had no usable key, DbSettingsRunner), in
	///ConnectionSettings\imported-client-certificate.txt next to the office CA copy (owner decision of 2026-11-02;
	///docs/specs/db-connection-security.md section 4.3). Kept there, not in FreeDentalConfig.xml, because the ConnectionSettings folder is
	///administrator-only with a protected DACL (OfficeCaFile.WriteProtectedFile): what the tool may delete from the computer's store must never
	///come from a file a standard user could have written. A record that is missing, unreadable, a link or not owned by an administrator reads
	///as none, and with none the tool removes nothing.</summary>
	public static class ImportedCertificateRecord {
		///<summary>The record file in the folder of the office CA copy (DbConnectionConfig.GetOfficeCaCopyPath; tests pass their own).</summary>
		public static string GetPath(string caCopyPath) {
			return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(caCopyPath)),ProgramNames.ImportedClientCertFileName);
		}

		///<summary>The recorded thumbprint (upper-case hex), or "" when there is no trustworthy record. Never throws.</summary>
		public static string Read(string path) {
			try {
				if(!File.Exists(path)) {
					return "";
				}
				OfficeCaFile.CheckExisting(Path.GetDirectoryName(Path.GetFullPath(path)),true,"the record of the imported client certificate");
				OfficeCaFile.CheckExisting(path,false,"the record of the imported client certificate");
				string thumbprint=DbConnectionConfig.NormalizeThumbprint(File.ReadAllText(path,Encoding.UTF8));
				return thumbprint.Length==40 ? thumbprint : "";//A SHA-1 thumbprint, or nothing.
			}
			catch(Exception ex) {
				ex.DoNothing();
				return "";
			}
		}

		///<summary>Records the thumbprint, or deletes the record for "". Protected like the office CA copy. Throws DbSettingsException (exit code 3),
		///IOException or UnauthorizedAccessException.</summary>
		public static void Write(string path,string thumbprint) {
			thumbprint=DbConnectionConfig.NormalizeThumbprint(thumbprint);
			if(thumbprint=="") {
				if(File.Exists(path)) {
					OfficeCaFile.CheckExisting(path,false,"the record of the imported client certificate");
					File.Delete(path);
				}
				return;
			}
			OfficeCaFile.WriteProtectedFile(path,new UTF8Encoding(false).GetBytes(thumbprint+"\r\n"),"the record of the imported client certificate");
		}
	}
}
