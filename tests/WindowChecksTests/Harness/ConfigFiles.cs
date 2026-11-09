// The connection files the tools need (FreeDentalConfig.xml, CentralManagerConfig.xml): made from the repository's tools\FreeDentalConfig.dev.xml
// for this run's scratch copy of the database (never copied from a real configuration), with the local test server's CA when TLS is on, and
// removed again by the check that wrote them (or at Z). Every file written is listed in config-files.txt in the results folder.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Xml;
using DataConnectionBase;

namespace WindowChecksTests {
	public static class ConfigFiles {
		///<summary>Writes the connection file into the folder and returns its path. Fails when a file of that name is already there: it is never
		///overwritten.</summary>
		public static string Write(string folder,string fileName) {
			string path=Path.Combine(folder,fileName);
			if(File.Exists(path)) {
				throw new InvalidOperationException(path+" already exists; the checks never overwrite a configuration file.");
			}
			XmlDocument xmlDocument=new XmlDocument { PreserveWhitespace=true };
			xmlDocument.Load(Path.Combine(WinChkEnv.RepoRoot,"tools","FreeDentalConfig.dev.xml"));
			xmlDocument.SelectSingleNode("/ConnectionSettings/DatabaseConnection/ComputerName").InnerText=ScratchDb.Server;
			xmlDocument.SelectSingleNode("/ConnectionSettings/DatabaseConnection/Database").InnerText=ScratchDb.Name;
			xmlDocument.SelectSingleNode("/ConnectionSettings/DatabaseConnection/User").InnerText="root";
			string sslCa=DevDbTls.GetSslCa(ScratchDb.Server);
			if(!string.IsNullOrEmpty(sslCa)) {
				XmlNode xmlNodeConnection=xmlDocument.SelectSingleNode("/ConnectionSettings/DatabaseConnection");
				XmlElement xmlElementCa=xmlDocument.CreateElement("SslCa");
				xmlElementCa.InnerText=sslCa;
				xmlNodeConnection.InsertAfter(xmlElementCa,xmlNodeConnection.SelectSingleNode("MySQLPassHash"));
			}
			xmlDocument.Save(path);
			File.AppendAllText(Path.Combine(WinChkEnv.OutFolder,"config-files.txt"),DateTime.Now.ToString("s")+" wrote "+path+Environment.NewLine);
			return path;
		}

		public static void Delete(string path) {
			if(path!=null && File.Exists(path)) {
				File.Delete(path);
				File.AppendAllText(Path.Combine(WinChkEnv.OutFolder,"config-files.txt"),DateTime.Now.ToString("s")+" deleted "+path+Environment.NewLine);
			}
		}
	}
}
