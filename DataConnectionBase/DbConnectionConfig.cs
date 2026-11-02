// The DatabaseConnection element of FreeDentalConfig.xml: this computer's database connection settings, read and written in one place.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using CodeBase;

namespace DataConnectionBase {
	///<summary>The DatabaseConnection element of FreeDentalConfig.xml (docs/specs/db-connection-security.md section 3). The program, ODR, the
	///connection settings tool and ServiceManager read it here; the tool and Debug builds of the program write it here. Writes keep every other
	///element of the file, and never write a password in plain text or with the osd1$ obfuscation: the password goes into MySQLPassDpapi
	///(MachineSecret).</summary>
	public class DbConnectionConfig {
		#region Element names
		public const string ELEMENT_DATABASE_CONNECTION="DatabaseConnection";
		public const string ELEMENT_COMPUTER_NAME="ComputerName";
		public const string ELEMENT_DATABASE="Database";
		public const string ELEMENT_USER="User";
		public const string ELEMENT_PASSWORD="Password";
		public const string ELEMENT_PASS_HASH="MySQLPassHash";
		public const string ELEMENT_PASS_DPAPI="MySQLPassDpapi";
		public const string ELEMENT_SSL_CA="SslCa";
		public const string ELEMENT_CLIENT_CERT_THUMBPRINT="ClientCertThumbprint";
		public const string ELEMENT_NO_SHOW_ON_STARTUP="NoShowOnStartup";
		#endregion Element names

		///<summary>The server, "host" or "host:port". Element ComputerName.</summary>
		public string ServerName="";
		///<summary>Element Database.</summary>
		public string Database="";
		///<summary>Element User.</summary>
		public string User="";
		///<summary>The office CA certificate file (PEM). Element SslCa.</summary>
		public string SslCa="";
		///<summary>The SHA-1 thumbprint of the client certificate in LocalMachine\My, upper-case hex without spaces. Element ClientCertThumbprint.
		///Not empty: certificate mode.</summary>
		public string ClientCertThumbprint="";
		///<summary>The password protected with MachineSecret ("dpapi1$..."). Element MySQLPassDpapi.</summary>
		public string PasswordDpapi="";
		///<summary>The old-style plain text password. Element Password. Used only for a loopback server.</summary>
		public string PasswordLegacy="";
		///<summary>The old-style obfuscated password (osd1$). Element MySQLPassHash. Used only for a loopback server.</summary>
		public string PasswordHashLegacy="";
		///<summary>The NoShowOnStartup element as it is ("True", "False"), or null when the file has none. Written back unchanged.</summary>
		public string NoShowOnStartup=null;

		///<summary>True when a client certificate is set (certificate mode).</summary>
		public bool IsCertificateMode {
			get {
				return !string.IsNullOrWhiteSpace(ClientCertThumbprint);
			}
		}

		///<summary>True when the file holds a password the old way (Password or MySQLPassHash not empty).</summary>
		public bool HasLegacyPassword {
			get {
				return !string.IsNullOrEmpty(PasswordLegacy) || !string.IsNullOrEmpty(PasswordHashLegacy);
			}
		}

		#region Paths
		///<summary>FreeDentalConfig.xml in folder (the program folder).</summary>
		public static string GetConfigPath(string folder) {
			return Path.Combine(folder,ProgramNames.ConfigFileName);
		}

		///<summary>Where the connection settings tool keeps its copy of the office CA certificate:
		///%ProgramData%\OpenPMS-Dental\Database\office-ca.pem.</summary>
		public static string GetOfficeCaCopyPath() {
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),ProgramNames.ProgramDataFolderName,
				ProgramNames.DbSettingsProgramDataFolderName,ProgramNames.OfficeCaFileName);
		}
		#endregion Paths

		#region Read
		///<summary>Reads the DatabaseConnection element below navigator (any depth). Null when there is none. Missing child elements read as "".
		///</summary>
		public static DbConnectionConfig FromNavigator(XPathNavigator xPathNavigator) {
			XPathNavigator xPathNavigatorConn=xPathNavigator.SelectSingleNode("//"+ELEMENT_DATABASE_CONNECTION);
			if(xPathNavigatorConn==null) {
				return null;
			}
			DbConnectionConfig dbConnectionConfig=new DbConnectionConfig();
			dbConnectionConfig.ServerName=ReadChild(xPathNavigatorConn,ELEMENT_COMPUTER_NAME);
			dbConnectionConfig.Database=ReadChild(xPathNavigatorConn,ELEMENT_DATABASE);
			dbConnectionConfig.User=ReadChild(xPathNavigatorConn,ELEMENT_USER);
			dbConnectionConfig.PasswordLegacy=ReadChild(xPathNavigatorConn,ELEMENT_PASSWORD);
			dbConnectionConfig.PasswordHashLegacy=ReadChild(xPathNavigatorConn,ELEMENT_PASS_HASH);
			dbConnectionConfig.PasswordDpapi=ReadChild(xPathNavigatorConn,ELEMENT_PASS_DPAPI).Trim();
			dbConnectionConfig.SslCa=ReadChild(xPathNavigatorConn,ELEMENT_SSL_CA).Trim();
			dbConnectionConfig.ClientCertThumbprint=NormalizeThumbprint(ReadChild(xPathNavigatorConn,ELEMENT_CLIENT_CERT_THUMBPRINT));
			XPathNavigator xPathNavigatorNoShow=xPathNavigatorConn.SelectSingleNode(ELEMENT_NO_SHOW_ON_STARTUP);
			if(xPathNavigatorNoShow!=null) {
				dbConnectionConfig.NoShowOnStartup=xPathNavigatorNoShow.Value;
			}
			return dbConnectionConfig;
		}

		///<summary>Reads FreeDentalConfig.xml at path. Null when the file does not exist or has no DatabaseConnection element. Throws XmlException
		///(the file cannot be read as XML) and IOException.</summary>
		public static DbConnectionConfig ReadFile(string path) {
			if(!File.Exists(path)) {
				return null;
			}
			XmlDocument xmlDocument=new XmlDocument();
			xmlDocument.Load(path);
			return FromNavigator(xmlDocument.CreateNavigator());
		}

		private static string ReadChild(XPathNavigator xPathNavigatorParent,string name) {
			XPathNavigator xPathNavigatorChild=xPathNavigatorParent.SelectSingleNode(name);
			if(xPathNavigatorChild==null) {
				return "";
			}
			return xPathNavigatorChild.Value??"";
		}

		///<summary>A thumbprint as certificate tools show it (spaces, colons, lower case, an invisible left-to-right mark from certlm.msc) in the
		///form the store and MySqlConnector compare: upper-case hex only.</summary>
		public static string NormalizeThumbprint(string thumbprint) {
			StringBuilder stringBuilder=new StringBuilder();
			foreach(char c in thumbprint??"") {
				if(Uri.IsHexDigit(c)) {
					stringBuilder.Append(char.ToUpperInvariant(c));
				}
			}
			return stringBuilder.ToString();
		}
		#endregion Read

		#region The password
		///<summary>The password to connect to server with (the configured server when server is null). From MySQLPassDpapi when it is set; else
		///the old-style Password or MySQLPassHash, but only for a loopback server. Throws DbConnectionRefusedException (a message for the user, no
		///secret) for an old-style password with a server over the network, or a MySQLPassDpapi value this computer cannot read.</summary>
		public string GetPassword(string server=null) {
			if(server==null) {
				server=ServerName;
			}
			bool isLoopback=DbConnectionSecurity.IsLoopbackServer(server);
			if(!isLoopback && HasLegacyPassword) {
				throw new DbConnectionRefusedException(server,GetLegacyPasswordMessage(server));
			}
			if(!string.IsNullOrEmpty(PasswordDpapi)) {
				if(!MachineSecret.TryUnprotect(PasswordDpapi,out string password)) {
					throw new DbConnectionRefusedException(server,"The database password stored on this computer cannot be read: it was stored on "
						+"another computer, or this computer's key changed. "+DbConnectionSecurity.ToolSentence());
				}
				return password;
			}
			if(!isLoopback) {
				return "";
			}
			if(PasswordLegacy!="") {
				return PasswordLegacy;
			}
			if(PasswordHashLegacy!="" && Secrets.Decrypt(PasswordHashLegacy,out string decryptedPassword)) {
				return decryptedPassword;
			}
			return "";
		}

		///<summary>The refusal of an old-style password for a server over the network.</summary>
		public static string GetLegacyPasswordMessage(string server) {
			return "The database password in "+ProgramNames.ConfigFileName+" is stored the old way ("+ELEMENT_PASSWORD+" or "+ELEMENT_PASS_HASH
				+"). For the database server "+string.Join(", ",DbConnectionSecurity.GetHosts(server))+" over the network it must be stored by the "
				+"connection settings tool. "+DbConnectionSecurity.ToolSentence();
		}

		///<summary>Sets the password: protected for this computer (MachineSecret) into MySQLPassDpapi, and the old-style elements emptied. A blank
		///password clears MySQLPassDpapi. Throws CryptographicException (no secret in it) when DPAPI fails.</summary>
		public void SetPassword(string password) {
			PasswordDpapi=MachineSecret.Protect(password);
			PasswordLegacy="";
			PasswordHashLegacy="";
		}
		#endregion The password

		#region The rule
		///<summary>What the rule finds missing for these settings with password (DbConnectionSecurity.GetMissing), as the connection string
		///BuildSimpleConnectionString makes from them would have it. Makes no connection.</summary>
		public List<string> GetMissing(bool hasPassword) {
			if(DbConnectionSecurity.IsLoopbackServer(ServerName)) {
				return new List<string>();
			}
			string connectionString=DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,ServerName,Database,User,
				hasPassword ? "x" : "",SslCa,ClientCertThumbprint);
			return DbConnectionSecurity.GetMissing(connectionString);
		}

		///<summary>Throws DbConnectionRefusedException when these settings cannot be used: an old-style password or an unreadable stored password
		///(GetPassword), or what the rule finds missing. Makes no connection.</summary>
		public void ThrowIfRefused() {
			string password=GetPassword();
			List<string> listMissing=GetMissing(password!="");
			if(listMissing.Count>0) {
				throw new DbConnectionRefusedException(ServerName,listMissing);
			}
		}
		#endregion The rule

		#region Write
		///<summary>Writes these settings into the DatabaseConnection element of xmlDocument, creating the element (and a ConnectionSettings root)
		///when needed. Elements of the file this class does not own are kept. Password and MySQLPassHash are always written empty (older readers
		///expect them); MySQLPassDpapi, SslCa and ClientCertThumbprint are written when set and removed when not.</summary>
		public void WriteTo(XmlDocument xmlDocument) {
			XmlElement xmlElementRoot=xmlDocument.DocumentElement;
			if(xmlElementRoot==null) {
				xmlElementRoot=xmlDocument.CreateElement("ConnectionSettings");
				xmlDocument.AppendChild(xmlElementRoot);
			}
			XmlElement xmlElementConn=xmlElementRoot.SelectSingleNode("//"+ELEMENT_DATABASE_CONNECTION) as XmlElement;
			if(xmlElementConn==null) {
				xmlElementConn=xmlDocument.CreateElement(ELEMENT_DATABASE_CONNECTION);
				xmlElementRoot.PrependChild(xmlElementConn);
			}
			SetChild(xmlElementConn,ELEMENT_COMPUTER_NAME,ServerName,true);
			SetChild(xmlElementConn,ELEMENT_DATABASE,Database,true);
			SetChild(xmlElementConn,ELEMENT_USER,User,true);
			SetChild(xmlElementConn,ELEMENT_PASSWORD,"",true);
			SetChild(xmlElementConn,ELEMENT_PASS_HASH,"",true);
			SetChild(xmlElementConn,ELEMENT_PASS_DPAPI,PasswordDpapi,false);
			if(NoShowOnStartup!=null) {
				SetChild(xmlElementConn,ELEMENT_NO_SHOW_ON_STARTUP,NoShowOnStartup,true);
			}
			SetChild(xmlElementConn,ELEMENT_SSL_CA,SslCa,false);
			SetChild(xmlElementConn,ELEMENT_CLIENT_CERT_THUMBPRINT,ClientCertThumbprint,false);
			//A ConnectionString element would take precedence over DatabaseConnection, and a ServerConnection (Middle Tier) over both.
			foreach(string name in new[] { "ConnectionString","ServerConnection" }) {
				XmlNode xmlNode=xmlElementRoot.SelectSingleNode("//"+name);
				if(xmlNode!=null) {
					xmlNode.ParentNode.RemoveChild(xmlNode);
				}
			}
			if(xmlElementRoot.SelectSingleNode("DatabaseType")==null) {
				SetChild(xmlElementRoot,"DatabaseType","MySql",true);
			}
		}

		///<summary>Sets the text of the child element name, creating it at the end when it is missing. With isKeptWhenEmpty false an empty value
		///removes the element.</summary>
		private static void SetChild(XmlElement xmlElementParent,string name,string value,bool isKeptWhenEmpty) {
			XmlElement xmlElementChild=xmlElementParent.SelectSingleNode(name) as XmlElement;
			if(string.IsNullOrEmpty(value) && !isKeptWhenEmpty) {
				if(xmlElementChild!=null) {
					xmlElementParent.RemoveChild(xmlElementChild);
				}
				return;
			}
			if(xmlElementChild==null) {
				xmlElementChild=xmlElementParent.OwnerDocument.CreateElement(name);
				xmlElementParent.AppendChild(xmlElementChild);
			}
			xmlElementChild.InnerText=value??"";
		}

		///<summary>Writes these settings into FreeDentalConfig.xml at path, keeping the rest of the file (a new file when there is none or it is
		///empty). Writes a temporary file next to it and replaces the old one, so a failure never leaves half a file. Throws XmlException (the
		///existing file is not XML; it is left as it is) and IOException or UnauthorizedAccessException.</summary>
		public void SaveFile(string path) {
			XmlDocument xmlDocument=new XmlDocument();
			xmlDocument.PreserveWhitespace=false;
			if(File.Exists(path) && new FileInfo(path).Length>0) {
				xmlDocument.Load(path);
			}
			WriteTo(xmlDocument);
			XmlWriterSettings xmlWriterSettings=new XmlWriterSettings();
			xmlWriterSettings.Indent=true;
			xmlWriterSettings.IndentChars="    ";
			xmlWriterSettings.Encoding=new UTF8Encoding(false);
			string pathTemp=path+".new";
			using(XmlWriter xmlWriter=XmlWriter.Create(pathTemp,xmlWriterSettings)) {
				xmlDocument.Save(xmlWriter);
			}
			if(File.Exists(path)) {
				File.Replace(pathTemp,path,null);
			}
			else {
				File.Move(pathTemp,path);
			}
		}
		#endregion Write
	}
}
