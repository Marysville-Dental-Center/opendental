// The secrets file of the connection settings tool: the .pfx password and the database password for silent installs.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text;

namespace OpenPmsDentalDbSettings {
	///<summary>The secrets the tool never takes on the command line (docs/specs/db-connection-security.md section 4.2). The file named by
	////secretsfile is UTF-8 text (a byte order mark is allowed) of lines "PfxPassword=..." and "DbPassword=...": the value is everything after the
	///first '=', spaces included; blank lines and lines starting with '#' are ignored. The tool reads the file and never writes, logs, copies or
	///deletes it: the caller deletes it. Messages never contain a value.</summary>
	public class DbSettingsSecrets {
		public const string KEY_PFX_PASSWORD="PfxPassword";
		public const string KEY_DB_PASSWORD="DbPassword";
		///<summary>A secrets file is small; anything larger is not one.</summary>
		private const int MAX_BYTES=64*1024;

		///<summary>The .pfx password, or null when the file has none.</summary>
		public string PfxPassword;
		///<summary>The database password, or null when the file has none.</summary>
		public string DbPassword;

		///<summary>Reads the secrets file. Throws DbSettingsException (exit code 2) when it is missing, too large or not in the format.</summary>
		public static DbSettingsSecrets ReadFile(string path) {
			byte[] arrayBytes;
			try {
				FileInfo fileInfo=new FileInfo(path);
				if(!fileInfo.Exists) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The secrets file "+path+" does not exist.");
				}
				if(fileInfo.Length>MAX_BYTES) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The secrets file "+path+" is too large to be one.");
				}
				arrayBytes=File.ReadAllBytes(path);
			}
			catch(IOException ex) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The secrets file "+path+" cannot be read: "+ex.Message);
			}
			catch(UnauthorizedAccessException ex) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The secrets file "+path+" cannot be read: "+ex.Message);
			}
			string text;
			try {
				text=new UTF8Encoding(false,true).GetString(arrayBytes);
			}
			catch(ArgumentException) {
				throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The secrets file "+path+" is not UTF-8 text.");
			}
			finally {
				Array.Clear(arrayBytes,0,arrayBytes.Length);
			}
			return Parse(text);
		}

		///<summary>Parses the text of a secrets file. Throws DbSettingsException (exit code 2) for a line that is not "name=value", an unknown name
		///or a name given twice; the message names the line number and the name only.</summary>
		public static DbSettingsSecrets Parse(string text) {
			DbSettingsSecrets dbSettingsSecrets=new DbSettingsSecrets();
			if(text.Length>0 && text[0]=='﻿') {
				text=text.Substring(1);
			}
			string[] arrayLines=text.Replace("\r\n","\n").Split('\n');
			for(int i=0;i<arrayLines.Length;i++) {
				string line=arrayLines[i].TrimEnd('\r');
				if(line.Trim()=="" || line.TrimStart().StartsWith("#")) {
					continue;
				}
				int indexEquals=line.IndexOf('=');
				if(indexEquals<=0) {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"Line "+(i+1)+" of the secrets file is not name=value.");
				}
				string name=line.Substring(0,indexEquals).Trim();
				string value=line.Substring(indexEquals+1);
				if(name.Equals(KEY_PFX_PASSWORD,StringComparison.OrdinalIgnoreCase)) {
					if(dbSettingsSecrets.PfxPassword!=null) {
						throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The secrets file gives "+KEY_PFX_PASSWORD+" twice.");
					}
					dbSettingsSecrets.PfxPassword=value;
				}
				else if(name.Equals(KEY_DB_PASSWORD,StringComparison.OrdinalIgnoreCase)) {
					if(dbSettingsSecrets.DbPassword!=null) {
						throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"The secrets file gives "+KEY_DB_PASSWORD+" twice.");
					}
					dbSettingsSecrets.DbPassword=value;
				}
				else {
					throw new DbSettingsException(DbSettingsRunner.EXIT_BAD_VALUE,"Line "+(i+1)+" of the secrets file has an unknown name. Use "
						+KEY_PFX_PASSWORD+" or "+KEY_DB_PASSWORD+".");
				}
			}
			return dbSettingsSecrets;
		}
	}
}
