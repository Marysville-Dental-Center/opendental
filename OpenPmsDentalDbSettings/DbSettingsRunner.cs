// What the connection settings tool does, without its window: check, apply and test this computer's connection settings.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using CodeBase;
using DataConnectionBase;
using MySqlConnector;

namespace OpenPmsDentalDbSettings {
	///<summary>What the user or the switches ask the tool to save. Null means: keep what the settings have.</summary>
	public class DbSettingsRequest {
		public string Server;
		public string Database;
		public string User;
		///<summary>An office CA certificate file to copy in; null keeps the configured SslCa.</summary>
		public string CaFile;
		///<summary>Certificate or Password; Unchanged keeps the mode of the current settings.</summary>
		public DbSettingsAuth Auth=DbSettingsAuth.Unchanged;
		///<summary>A .pfx to import (certificate mode).</summary>
		public string PfxFile;
		public string PfxPassword;
		///<summary>A certificate already in LocalMachine\My (certificate mode); null keeps the configured one.</summary>
		public string Thumbprint;
		///<summary>The database password (password mode); null keeps the stored one when the user stays the same.</summary>
		public string DbPassword;
		///<summary>True when the request comes from the window: messages then name its fields and buttons, not the secrets file or /prompt.
		///</summary>
		public bool IsWindow;
	}

	///<summary>The result of a run: the exit code and a message for the user and the log, never with a secret in it.</summary>
	public class DbSettingsResult {
		public int ExitCode;
		public string Message="";

		public DbSettingsResult(int exitCode,string message) {
			ExitCode=exitCode;
			Message=message??"";
		}
	}

	///<summary>Checks, applies and tests this computer's database connection settings (docs/specs/db-connection-security.md section 4). The
	///window and the silent switches both come here. Everything is checked before anything is changed: the CA file, the .pfx and its password,
	///the certificate in the store, the rule. Then the CA is copied, the .pfx imported, the password protected (DPAPI, machine scope) and
	///FreeDentalConfig.xml written, keeping its other elements; after that, a client certificate the tool imported itself and the saved
	///settings no longer use is removed from the store (PlanImportedCertificate). When the save fails after the import gave the store's entry
	///the key it stored, the import is undone or recorded (UndoImportOfFailedSave), so no key of the tool's is left that the record does not
	///name.</summary>
	public static class DbSettingsRunner {
		public const int EXIT_OK=0;
		public const int EXIT_BAD_ARGS=1;
		public const int EXIT_BAD_VALUE=2;
		public const int EXIT_WRITE_FAILED=3;
		public const int EXIT_TEST_FAILED=4;
		public const int EXIT_REFUSED=5;
		public const int EXIT_CANCELLED=6;
		///<summary>The tests only: called right before ApplyOrThrow resets the rights of the saved FreeDentalConfig.xml (ResetConfigFileRights);
		///an IOException there is rights that cannot be reset, after the settings were saved. Null in the tool.</summary>
		internal static Action ActionBeforeConfigFileRightsResetForTests;

		///<summary>The settings of the file at configPath, or new empty ones when there is no file or no DatabaseConnection element. Throws
		///DbSettingsException (exit code 2) when the file is not XML.</summary>
		public static DbConnectionConfig ReadCurrent(string configPath) {
			try {
				return DbConnectionConfig.ReadFile(configPath)??new DbConnectionConfig();
			}
			catch(Exception ex) when(ex is System.Xml.XmlException || ex is IOException || ex is UnauthorizedAccessException) {
				throw new DbSettingsException(EXIT_BAD_VALUE,ProgramNames.ConfigFileName+" cannot be read: "+ex.Message);
			}
		}

		///<summary>/check: whether the file at configPath passes the rule, without connecting. 0, or 5 with what is missing. A ConnectionString
		///element is checked first, because the program uses it before DatabaseConnection: it may name only a database on this computer
		///(docs/specs/db-connection-security.md section 3.1).</summary>
		public static DbSettingsResult Check(string configPath) {
			DbConnectionConfig dbConnectionConfig;
			string connectionString;
			try {
				if(!File.Exists(configPath)) {
					return new DbSettingsResult(EXIT_REFUSED,ProgramNames.ConfigFileName+" is missing, so it has no database connection.");
				}
				System.Xml.XmlDocument xmlDocument=new System.Xml.XmlDocument();
				xmlDocument.Load(configPath);
				System.Xml.XPath.XPathNavigator xPathNavigator=xmlDocument.CreateNavigator();
				connectionString=DbConnectionConfig.ReadConnectionStringElement(xPathNavigator);
				dbConnectionConfig=DbConnectionConfig.FromNavigator(xPathNavigator);
			}
			catch(Exception ex) {
				return new DbSettingsResult(EXIT_REFUSED,ProgramNames.ConfigFileName+" cannot be read: "+ex.Message);
			}
			if(!string.IsNullOrWhiteSpace(connectionString)) {
				string connectionStringProblem=DbConnectionSecurity.GetConnectionStringProblem(connectionString);
				if(connectionStringProblem!="") {
					return new DbSettingsResult(EXIT_REFUSED,connectionStringProblem);
				}
				return new DbSettingsResult(EXIT_OK,"The connection string in "+ProgramNames.ConfigFileName+" names a database on this computer, which "
					+"needs no encryption.");
			}
			if(dbConnectionConfig==null) {
				return new DbSettingsResult(EXIT_REFUSED,ProgramNames.ConfigFileName+" has no database connection.");
			}
			try {
				dbConnectionConfig.ThrowIfRefused();
			}
			catch(DbConnectionRefusedException ex) {
				return new DbSettingsResult(EXIT_REFUSED,ex.Message);
			}
			return new DbSettingsResult(EXIT_OK,"The connection settings for "+dbConnectionConfig.ServerName+" pass the rule.");
		}

		///<summary>The request the switches make, with secrets from secrets (null when none were given).</summary>
		public static DbSettingsRequest RequestFromArgs(DbSettingsArgs dbSettingsArgs,DbSettingsSecrets dbSettingsSecrets) {
			DbSettingsRequest dbSettingsRequest=new DbSettingsRequest();
			dbSettingsRequest.Server=dbSettingsArgs.Server;
			dbSettingsRequest.Database=dbSettingsArgs.Database;
			dbSettingsRequest.User=dbSettingsArgs.User;
			dbSettingsRequest.CaFile=dbSettingsArgs.CaFile;
			dbSettingsRequest.Auth=dbSettingsArgs.Auth;
			dbSettingsRequest.PfxFile=dbSettingsArgs.PfxFile;
			dbSettingsRequest.Thumbprint=dbSettingsArgs.Thumbprint;
			if(dbSettingsSecrets!=null) {
				dbSettingsRequest.PfxPassword=dbSettingsSecrets.PfxPassword;
				dbSettingsRequest.DbPassword=dbSettingsSecrets.DbPassword;
			}
			return dbSettingsRequest;
		}

		///<summary>The mode the request ends in: its own, or that of the current settings (certificate when they have a thumbprint).</summary>
		public static DbSettingsAuth ResolveAuth(DbSettingsRequest dbSettingsRequest,DbConnectionConfig dbConnectionConfigCurrent) {
			if(dbSettingsRequest.Auth!=DbSettingsAuth.Unchanged) {
				return dbSettingsRequest.Auth;
			}
			if(dbSettingsRequest.PfxFile!=null || dbSettingsRequest.Thumbprint!=null || dbConnectionConfigCurrent.IsCertificateMode) {
				return DbSettingsAuth.Certificate;
			}
			return DbSettingsAuth.Password;
		}

		///<summary>Applies the request to the file at configPath, with the office CA copied to caCopyPath (DbConnectionConfig.GetOfficeCaCopyPath
		///in the program; tests pass their own). Checks everything first, then changes things. Never throws: the result has the exit code.</summary>
		public static DbSettingsResult Apply(DbSettingsRequest dbSettingsRequest,string configPath,string caCopyPath) {
			try {
				string warning=ApplyOrThrow(dbSettingsRequest,configPath,caCopyPath,out DbConnectionConfig dbConnectionConfigNew);
				string message="Saved the connection settings for the database "+dbConnectionConfigNew.Database+" on "+dbConnectionConfigNew.ServerName
					+(dbConnectionConfigNew.IsCertificateMode ? " (client certificate)." : " (password).");
				if(warning!="") {
					message+=" "+warning;
				}
				string warningRoot=GetRootUserWarning(dbConnectionConfigNew.ServerName,dbConnectionConfigNew.User);
				if(warningRoot!="") {
					message+=" "+warningRoot;
				}
				return new DbSettingsResult(EXIT_OK,message);
			}
			catch(DbSettingsException ex) {
				return new DbSettingsResult(ex.ExitCode,ex.Message);
			}
		}

		private static string ApplyOrThrow(DbSettingsRequest dbSettingsRequest,string configPath,string caCopyPath,
			out DbConnectionConfig dbConnectionConfigNew)
		{
			DbConnectionConfig dbConnectionConfigCurrent=ReadCurrent(configPath);
			dbConnectionConfigNew=new DbConnectionConfig();
			dbConnectionConfigNew.ServerName=(dbSettingsRequest.Server??dbConnectionConfigCurrent.ServerName).Trim();
			dbConnectionConfigNew.Database=(dbSettingsRequest.Database??dbConnectionConfigCurrent.Database).Trim();
			dbConnectionConfigNew.User=(dbSettingsRequest.User??dbConnectionConfigCurrent.User).Trim();
			dbConnectionConfigNew.NoShowOnStartup=dbConnectionConfigCurrent.NoShowOnStartup;
			DbSettingsArgs.CheckValue("The server",dbConnectionConfigNew.ServerName,isDatabase:false,isServer:true);
			DbSettingsArgs.CheckValue("The database",dbConnectionConfigNew.Database,isDatabase:true,isServer:false);
			DbSettingsArgs.CheckValue("The user",dbConnectionConfigNew.User,isDatabase:false,isServer:false);
			bool isLoopback=DbConnectionSecurity.IsLoopbackServer(dbConnectionConfigNew.ServerName);
			#region Check everything
			List<X509Certificate2> listCaCertificates=null;
			if(!string.IsNullOrWhiteSpace(dbSettingsRequest.CaFile)) {
				listCaCertificates=OfficeCaFile.Load(dbSettingsRequest.CaFile.Trim());
				dbConnectionConfigNew.SslCa=caCopyPath;
			}
			else {
				dbConnectionConfigNew.SslCa=dbConnectionConfigCurrent.SslCa;
			}
			if(!isLoopback && string.IsNullOrWhiteSpace(dbConnectionConfigNew.SslCa)) {
				throw new DbSettingsException(EXIT_REFUSED,"A database server over the network ("+dbConnectionConfigNew.ServerName+") needs the office "
					+"CA certificate file "+Hint(dbSettingsRequest,"(/ca=).","(Office CA certificate: Browse...)."));
			}
			DbSettingsAuth dbSettingsAuth=ResolveAuth(dbSettingsRequest,dbConnectionConfigCurrent);
			bool isPfxImport=false;
			//Whether the store's entry of the .pfx's certificate uses the private key this save's import stored (ClientCertificateStore.ImportPfx):
			//then the tool made the key the certificate has, records it, and removes it with that key when a later save stops using it; a save
			//that fails after the import undoes it, or records it (UndoImportOfFailedSave). Not when the store held the certificate with a usable
			//key of the tool's own before (the record names it), which the import leaves as it is.
			bool isStoreKeyImported=false;
			//Whether the store held that certificate before the import, and whether its entry's earlier key was left aside: an undo removes the
			//whole entry, and its error says so (UndoImportOfFailedSave).
			ClientCertificateEntryBefore entryBefore=ClientCertificateEntryBefore.None;
			string password="";
			if(dbSettingsAuth==DbSettingsAuth.Certificate) {
				if(!string.IsNullOrWhiteSpace(dbSettingsRequest.PfxFile)) {
					if(dbSettingsRequest.PfxPassword==null) {
						throw new DbSettingsException(EXIT_BAD_VALUE,"The password of the client certificate file is missing "+Hint(dbSettingsRequest,
							"(PfxPassword in the secrets file, or /prompt).","(type it when the window asks for it, or choose the file again with Import "
							+".pfx...)."));
					}
					using(X509Certificate2 x509Certificate2=ClientCertificateStore.LoadPfxForCheck(dbSettingsRequest.PfxFile.Trim(),
						dbSettingsRequest.PfxPassword))
					{
						dbConnectionConfigNew.ClientCertThumbprint=DbConnectionConfig.NormalizeThumbprint(x509Certificate2.Thumbprint);
					}
					isPfxImport=true;
				}
				else {
					string thumbprint=DbConnectionConfig.NormalizeThumbprint(dbSettingsRequest.Thumbprint??dbConnectionConfigCurrent.ClientCertThumbprint);
					if(thumbprint=="") {
						throw new DbSettingsException(EXIT_BAD_VALUE,"No client certificate: "+Hint(dbSettingsRequest,"import a .pfx (/pfx=) or name one in "
							+"the computer's store (/thumbprint=).","import a .pfx with Import .pfx..., or choose Database password."));
					}
					X509Certificate2 x509Certificate2=ClientCertificateStore.FindInStore(thumbprint);
					if(x509Certificate2==null) {
						throw new DbSettingsException(EXIT_BAD_VALUE,"The certificate "+thumbprint+" is not in the computer's certificate store "
							+"(LocalMachine\\My).");
					}
					ClientCertificateStore.CheckClientCertificate(x509Certificate2,"LocalMachine\\My");
					dbConnectionConfigNew.ClientCertThumbprint=thumbprint;
				}
			}
			else {
				if(dbSettingsRequest.DbPassword!=null) {
					password=dbSettingsRequest.DbPassword;
				}
				else if(dbConnectionConfigNew.User==dbConnectionConfigCurrent.User) {
					//Keep the stored password of the same user; an old-style one is moved into DPAPI, but only for a server on this computer.
					try {
						password=dbConnectionConfigCurrent.GetPassword(dbConnectionConfigNew.ServerName);
					}
					catch(DbConnectionRefusedException) {
						throw new DbSettingsException(EXIT_REFUSED,"The stored password cannot be kept for "+dbConnectionConfigNew.ServerName+": it is "
							+"stored the old way or cannot be read on this computer. "+Hint(dbSettingsRequest,"Give the database password (DbPassword in the "
							+"secrets file, or /prompt).","Type the database password in the Password field."));
					}
				}
				if(!isLoopback && password=="") {
					throw new DbSettingsException(EXIT_REFUSED,"A database server over the network ("+dbConnectionConfigNew.ServerName+") needs a client "
						+"certificate or a database password "+Hint(dbSettingsRequest,"(DbPassword in the secrets file, or /prompt).","(import a .pfx with "
						+"Import .pfx..., or type the database password in the Password field)."));
				}
			}
			//The rule, as the program will apply it. The CA copy does not exist yet; the rule checks the file the copy is made from.
			string sslCaToCheck=dbConnectionConfigNew.SslCa;
			if(listCaCertificates!=null) {
				sslCaToCheck=dbSettingsRequest.CaFile.Trim();
			}
			string connectionString=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,dbConnectionConfigNew.ServerName,
				dbConnectionConfigNew.Database,dbConnectionConfigNew.User,password,sslCaToCheck,dbConnectionConfigNew.ClientCertThumbprint);
			List<string> listMissing=DbConnectionSecurity.GetMissing(connectionString);
			if(listMissing.Count>0) {
				throw new DbSettingsException(EXIT_REFUSED,DbConnectionSecurity.GetRefusalMessage(dbConnectionConfigNew.ServerName,listMissing));
			}
			#endregion Check everything
			#region Change things
			string warning="";
			string pathRecord=ImportedCertificateRecord.GetPath(caCopyPath);
			string thumbprintRecorded=ImportedCertificateRecord.Read(pathRecord);
			//The thumbprint this save's import returned, once ImportPfx has returned; "" before, and without an import.
			string thumbprintImported="";
			try {
				if(listCaCertificates!=null) {
					OfficeCaFile.WriteCopy(listCaCertificates,caCopyPath);
				}
				if(isPfxImport) {
					//isStoreKeyImported is set only once ImportPfx has returned: an import that throws has undone itself.
					string thumbprintReturned=ClientCertificateStore.ImportPfx(dbSettingsRequest.PfxFile.Trim(),dbSettingsRequest.PfxPassword,
						thumbprintRecorded,out warning,out bool isStoreKeyImportedReturned,out ClientCertificateEntryBefore entryBeforeReturned);
					thumbprintImported=DbConnectionConfig.NormalizeThumbprint(thumbprintReturned);
					isStoreKeyImported=isStoreKeyImportedReturned;
					entryBefore=entryBeforeReturned;
					dbConnectionConfigNew.ClientCertThumbprint=thumbprintImported;
				}
				if(dbSettingsAuth==DbSettingsAuth.Certificate) {
					dbConnectionConfigNew.SetPassword("");
				}
				else {
					dbConnectionConfigNew.ClientCertThumbprint="";
					dbConnectionConfigNew.SetPassword(password);
				}
				dbConnectionConfigNew.SaveFile(configPath);
			}
			catch(Exception ex) when(isStoreKeyImported && !ClientCertificateStore.IsCriticalException(ex)) {
				//The import gave the store's entry a key the tool made, which the users may read, and the settings were not saved: the import is
				//undone, or recorded (UndoImportOfFailedSave), so that no key of the tool is left that no record names. The import's warning comes
				//first: it names the store entry's earlier key that the import left (with the certutil command that deletes it), a key it could not
				//delete, or a grant that failed, which the undo does not change.
				string message=(ex is DbSettingsException) ? ex.Message : "The connection settings could not be saved: "+ex.Message;
				int exitCode=(ex is DbSettingsException) ? ((DbSettingsException)ex).ExitCode : EXIT_WRITE_FAILED;
				throw new DbSettingsException(exitCode,AppendSentence(AppendSentence(EndSentence(message),warning),UndoImportOfFailedSave(
					thumbprintImported,dbConnectionConfigCurrent.ClientCertThumbprint,thumbprintRecorded,pathRecord,entryBefore)));
			}
			catch(DbSettingsException) {
				throw;
			}
			catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is CryptographicException
				|| ex is System.Xml.XmlException || ex is InvalidOperationException)//InvalidOperationException: an owner Windows does not let this process set
			{
				throw new DbSettingsException(EXIT_WRITE_FAILED,"The connection settings could not be saved: "+ex.Message);
			}
			//The settings are saved: only now does the certificate they replaced go, and only one this tool imported itself. Also when the rights
			//of the file cannot be reset below: the saved settings use the new certificate, so the record must name it, and the replaced one goes.
			PlanImportedCertificate(thumbprintRecorded,dbConnectionConfigCurrent.ClientCertThumbprint,dbConnectionConfigNew.ClientCertThumbprint,isPfxImport,
				isStoreKeyImported,out string thumbprintToRecord,out string thumbprintToRemove);
			string warningRights="";
			try {
				ActionBeforeConfigFileRightsResetForTests?.Invoke();
				ResetConfigFileRights(configPath);
			}
			catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException
				|| ex is System.Security.SecurityException)
			{
				warningRights="The connection settings were saved, but the rights of "+ProgramNames.ConfigFileName+" could not be set back to those "
					+"of the program folder: "+ex.Message+" Remove any right of Users on the file in its Properties, Security tab, then save again.";
			}
			try {
				ImportedCertificateRecord.Write(pathRecord,thumbprintToRecord);
			}
			catch(Exception ex) when(ex is DbSettingsException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException) {
				warning=AppendSentence(warning,"The tool could not record which client certificate it imported ("+ex.Message+"), so it will not remove "
					+"that certificate when it is replaced.");
			}
			warning=AppendSentence(warning,ClientCertificateStore.RemoveImported(thumbprintToRemove));
			if(warningRights!="") {
				throw new DbSettingsException(EXIT_WRITE_FAILED,AppendSentence(warningRights,warning));
			}
			#endregion Change things
			return warning;
		}

		///<summary>After a save that failed once ImportPfx had given the store's entry the key it stored (isStoreKeyImported), before the settings
		///were saved: the tool's key must not be left Users-readable with no record naming it (owner decision of 2026-11-02). The imported
		///certificate is removed with that key (RemoveImported), unless the settings as they still are use it, or the record names it: then it
		///stays, and is recorded at once, so that the save that later stops using it removes it with that key. The removal takes the whole store
		///entry: when the store held the certificate before the import (entryBefore), the text says that this entry was removed, with the key this
		///save gave it, and, when the import left the entry's earlier key aside, that the key stays where the import's warning (which the error
		///gives before this text) says. Returns what was done, or the warning that says what is left; never throws.</summary>
		private static string UndoImportOfFailedSave(string thumbprintImported,string thumbprintCurrent,string thumbprintRecorded,string pathRecord,
			ClientCertificateEntryBefore entryBefore)
		{
			thumbprintImported=DbConnectionConfig.NormalizeThumbprint(thumbprintImported);
			thumbprintCurrent=DbConnectionConfig.NormalizeThumbprint(thumbprintCurrent);
			thumbprintRecorded=DbConnectionConfig.NormalizeThumbprint(thumbprintRecorded);
			if(thumbprintImported=="") {
				return "";
			}
			if(thumbprintImported!=thumbprintCurrent && thumbprintImported!=thumbprintRecorded) {
				string warningRemove=ClientCertificateStore.RemoveImported(thumbprintImported);
				if(warningRemove!="") {
					return warningRemove;
				}
				if(entryBefore==ClientCertificateEntryBefore.InStoreKeyLeft) {
					return "The client certificate was already in the computer's certificate store before this save; it was removed from the store, with "
						+"the private key this save gave it, and its earlier key was left untouched, where said above.";
				}
				if(entryBefore==ClientCertificateEntryBefore.InStore) {
					return "The client certificate was already in the computer's certificate store before this save, without a private key it could use; "
						+"it was removed from the store, with the private key this save gave it.";
				}
				return "The client certificate this save imported was removed from the computer's certificate store again, with its private key.";
			}
			if(thumbprintImported==thumbprintRecorded) {
				return "";//Recorded already: a later save that stops using it removes it.
			}
			try {
				ImportedCertificateRecord.Write(pathRecord,thumbprintImported);
			}
			catch(Exception ex) when(!ClientCertificateStore.IsCriticalException(ex)) {
				return "The client certificate this save imported ("+thumbprintImported+") stays in the computer's certificate store, which the "
					+"settings use, but the tool could not record it ("+ex.Message.Trim()+"), so it will not remove that certificate when it is "
					+"replaced. Remove it with its private key as an administrator once no settings use it, in PowerShell: Remove-Item "
					+"Cert:\\LocalMachine\\My\\"+thumbprintImported+" -DeleteKey.";
			}
			return "";
		}

		///<summary>The text, trimmed, ending with a full stop, so that the next sentence can follow it.</summary>
		private static string EndSentence(string text) {
			text=(text??"").Trim();
			if(text=="" || text.EndsWith(".") || text.EndsWith("!") || text.EndsWith("?")) {
				return text;
			}
			return text+".";
		}

		///<summary>What a save does with the certificate the tool imported itself (owner decision of 2026-11-02). thumbprintToRecord: the
		///certificate the saved settings use when its store entry uses the private key the tool's import stored (isStoreKeyImported, from
		///ClientCertificateStore.ImportPfx: a certificate the store did not have, or one whose entry had no usable key of the tool's own in the
		///import's key set and took the import's, DCFU3), or the one already recorded when the settings keep it; else "". thumbprintToRemove: the
		///recorded certificate when the settings used it and now stop (another certificate, or the password); else "". A certificate whose key
		///the tool did not make and whose entry keeps that key (one named by thumbprint) is never recorded, so never removed. Thumbprints are
		///normalized; "" means none.</summary>
		public static void PlanImportedCertificate(string thumbprintRecorded,string thumbprintCurrent,string thumbprintNew,bool isPfxImported,
			bool isStoreKeyImported,out string thumbprintToRecord,out string thumbprintToRemove)
		{
			thumbprintRecorded=DbConnectionConfig.NormalizeThumbprint(thumbprintRecorded);
			thumbprintCurrent=DbConnectionConfig.NormalizeThumbprint(thumbprintCurrent);
			thumbprintNew=DbConnectionConfig.NormalizeThumbprint(thumbprintNew);
			thumbprintToRecord="";
			if(thumbprintNew!="") {
				if(isPfxImported && isStoreKeyImported) {
					thumbprintToRecord=thumbprintNew;
				}
				else if(thumbprintNew==thumbprintRecorded) {
					thumbprintToRecord=thumbprintRecorded;//The tool's own certificate, kept (or its .pfx imported again).
				}
			}
			thumbprintToRemove="";
			if(thumbprintRecorded!="" && thumbprintRecorded==thumbprintCurrent && thumbprintRecorded!=thumbprintNew) {
				thumbprintToRemove=thumbprintRecorded;
			}
		}

		///<summary>Gives FreeDentalConfig.xml the rights of the program folder it is in, and no others (owner decision of 2026-11-02): every
		///explicit rule is removed (the Users modify right v0.0's setup gave, or any a user added) and the folder's rules are inherited, so in an
		///all-users install only administrators change the connection settings. The owner becomes BUILTIN\Administrators (as for the office CA
		///copy), since an owner may always change a file's rules; not elevated (only the tests run this code that way) the owner stays. Runs at
		///every save; DbConnectionConfig.SaveFile replaces the file with File.Replace, which keeps the old file's rules. Throws IOException,
		///UnauthorizedAccessException, InvalidOperationException.</summary>
		public static void ResetConfigFileRights(string configPath) {
			FileSecurity fileSecurity=new FileSecurity();
			//Not protected: the rules of the folder flow in. No explicit rule: what the file had before is gone.
			fileSecurity.SetAccessRuleProtection(false,false);
			if(OfficeCaFile.IsElevated()) {
				fileSecurity.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null));
			}
			File.SetAccessControl(configPath,fileSecurity);
		}

		private static string AppendSentence(string text,string sentence) {
			if(string.IsNullOrEmpty(sentence)) {
				return text;
			}
			return string.IsNullOrEmpty(text) ? sentence : text+" "+sentence;
		}

		///<summary>The warning for the database user root on a server over the network, or "" when it does not apply (a server on this
		///computer, any other user). Owner decision of 2026-11-02: the program no longer uses root; the office gives the program its own account
		///with the rights the server guide lists (docs/db-connection-security.md section 5). A warning, never a refusal: the settings are saved
		///and the result says it. MariaDB compares user names exactly, but any spelling of root is warned about.</summary>
		public static string GetRootUserWarning(string server,string user) {
			if(!string.Equals((user??"").Trim(),"root",StringComparison.OrdinalIgnoreCase) || DbConnectionSecurity.IsLoopbackServer(server??"")) {
				return "";
			}
			return "Warning: the database user root is MariaDB's administrator account. For a server over the network the office should use its own "
				+"account for the program (for example opmsd), with only the rights the server guide gives it (docs/db-connection-security.md, "
				+"section 5).";
		}

		///<summary>What to do about a missing value: switches for the switches and the secrets file, window for the window's fields and buttons
		///(DbSettingsRequest.IsWindow).</summary>
		private static string Hint(DbSettingsRequest dbSettingsRequest,string switches,string window) {
			return dbSettingsRequest.IsWindow ? window : switches;
		}

		///<summary>Connects with the saved settings at configPath (through the connection rule) and asks the server for its version. Returns the
		///result; never throws.</summary>
		public static DbSettingsResult TestSaved(string configPath) {
			DbConnectionConfig dbConnectionConfig;
			string password;
			try {
				dbConnectionConfig=DbConnectionConfig.ReadFile(configPath);
				if(dbConnectionConfig==null) {
					return new DbSettingsResult(EXIT_TEST_FAILED,ProgramNames.ConfigFileName+" has no database connection.");
				}
				password=dbConnectionConfig.GetPassword();
			}
			catch(DbConnectionRefusedException ex) {
				return new DbSettingsResult(EXIT_REFUSED,ex.Message);
			}
			catch(Exception ex) {
				return new DbSettingsResult(EXIT_TEST_FAILED,ProgramNames.ConfigFileName+" cannot be read: "+ex.Message);
			}
			return TestConnection(dbConnectionConfig.ServerName,dbConnectionConfig.Database,dbConnectionConfig.User,password,dbConnectionConfig.SslCa,
				dbConnectionConfig.ClientCertThumbprint);
		}

		///<summary>Connects with these values (through the connection rule, so a refused connection is never attempted) and asks the server for
		///its version and the database for its program version. Never throws; the message never contains the password.</summary>
		public static DbSettingsResult TestConnection(string server,string database,string user,string password,string sslCa,string thumbprint) {
			string connectionString=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,server,database,user,password,sslCa,thumbprint);
			try {
				using(MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(connectionString)) {
					mySqlConnection.Open();
					string serverVersion;
					using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT VERSION()",mySqlConnection)) {
						serverVersion=Convert.ToString(mySqlCommand.ExecuteScalar());
					}
					string programVersion="";
					try {
						using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT ValueString FROM preference WHERE PrefName='ProgramVersion'",mySqlConnection)) {
							programVersion=Convert.ToString(mySqlCommand.ExecuteScalar());
						}
					}
					catch(MySqlException) {
						//Not a database of the program (yet); the connection itself works.
					}
					string encrypted=DbConnectionSecurity.IsLoopbackServer(server) ? "" : ", encrypted, server verified";
					return new DbSettingsResult(EXIT_OK,"Connected to "+server+" (MariaDB/MySQL "+serverVersion+encrypted+")."
						+(programVersion=="" ? " The database "+database+" has no program version." : " Database "+database+", version "+programVersion+"."));
				}
			}
			catch(DbConnectionRefusedException ex) {
				return new DbSettingsResult(EXIT_REFUSED,ex.Message);
			}
			catch(Exception ex) {
				return new DbSettingsResult(EXIT_TEST_FAILED,"The connection test failed: "+ex.Message);
			}
		}

		///<summary>Appends "time exit=N message" to logPath. Never throws, never writes a secret (messages have none).</summary>
		public static void AppendLog(string logPath,DbSettingsResult dbSettingsResult) {
			if(string.IsNullOrWhiteSpace(logPath)) {
				return;
			}
			try {
				File.AppendAllText(logPath,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" "+ProgramNames.DbSettingsExeFileName+" exit="
					+dbSettingsResult.ExitCode+" "+dbSettingsResult.Message+Environment.NewLine,new UTF8Encoding(false));
			}
			catch(Exception ex) {
				ex.DoNothing();
			}
		}
	}
}
