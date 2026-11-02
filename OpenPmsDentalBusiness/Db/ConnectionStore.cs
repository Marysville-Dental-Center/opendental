using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Hosting;
using System.Xml.Serialization;
using CodeBase;
using DataConnectionBase;
using System.Xml;

namespace OpenPmsDentalBusiness {
	///<summary>Thread-safe access to a list of connection store object which is retreived from a given file. If Init is not called then looks for ConnectionStore.xml in working directory.</summary>
	public class ConnectionStore {

		static ConnectionStore() {
			//Because ConnectionStoreBase does not have access to classes in OpenPmsDentalBusiness, we need to "override" a few methods to inject logic from 
			//OpenPmsDentalBusiness.
			ConnectionStoreBase.SetRemotingT=(string serverURI,bool isReportServer,bool isReadOnlyServer) => {
				RemotingClient.SetRemotingT(serverURI,MiddleTierRole.ClientMT,isReportServer,isReadOnlyServer);
			};
			ConnectionStoreBase.SetServerURI=(string serverURI) => {
				RemotingClient.ServerURI=serverURI;
			};
			ConnectionStoreBase.GetDentalOfficeReportServerFromPrefC=() => {
				//Be aware that if PrefC cache is not already filled and/or DataConnection.SetDb() has not already been called, this will fail.
				CentralConnectionBase cn=null;
				ODException.SwallowAnyException(() => {
					//give regular server credentials if the report server is not set up.
					cn=new CentralConnectionBase();
					cn.ServerName=PrefC.ReportingServer.Server=="" ? DataConnection.GetServerName() : PrefC.ReportingServer.Server;
					cn.DatabaseName=PrefC.ReportingServer.Server=="" ? DataConnection.GetDatabaseName() : PrefC.ReportingServer.Database;
					cn.MySqlUser=PrefC.ReportingServer.Server=="" ? DataConnection.GetMysqlUser() : PrefC.ReportingServer.MySqlUser;
					cn.MySqlPassword=PrefC.ReportingServer.Server=="" ? DataConnection.GetMysqlPass() : PrefC.ReportingServer.MySqlPass;
					//no ternary operator because URI will be blank if they're not using a middle tier reporting server.
					cn.ServiceURI=PrefC.ReportingServer.URI;
					//need to add PrefC.ReportingServer.SslCa call later to reporting server implementation Job:45000
					//No server in the preference: the main connection, with the main connection's CA. A reporting server of its own: only the CA of
					//its own preference (none: SslMode=none). Main fell back to the main connection's CA here, which is now always the office CA of a
					//workstation, so a server without a CA preference would no longer connect as before (docs/specs/db-connection-security.md 5.1).
					cn.SslCa=PrefC.ReportingServer.Server=="" ? DataConnection.GetSslCa() : PrefC.ReportingServer.SslCa;
					//Connection string is not currently supported for report servers.
					//If ServerName is null or empty, then the current instance of Open Dental is utilizing a connection string.
					//The connection string should be preserved in order for reports to continue to work for non-report server queries.
					if(string.IsNullOrEmpty(cn.ServerName)) {
						cn.ConnectionString=PrefC.ReportingServer.ConnectionString=="" ? DataConnection.GetConnectionString() : PrefC.ReportingServer.ConnectionString;
					}
					if(PrefC.ReportingServer.Server!="") {
						//A reporting server of its own connects as before the connection rule, with the server, user, password and CA of its preferences
						//(owner decision of 2026-11-02, docs/specs/db-connection-security.md section 5.1). Without a server in the preference this is the
						//main connection, which stays under the rule.
						cn.MarkPreferenceServer();
					}
				});
				return cn;
			};
			ConnectionStoreBase.GetDentalOfficeReadOnlyServerFromPrefC=() => {
				//Be aware that if PrefC cache is not already filled and/or DataConnection.SetDb() has not already been called, this will fail.
				CentralConnectionBase cn=null;
				ODException.SwallowAnyException(() => {
					//give regular server credentials if the read-only server is not set up.
					cn=new CentralConnectionBase();
					cn.ServerName=PrefC.ReadOnlyServer.Server=="" ? DataConnection.GetServerName() : PrefC.ReadOnlyServer.Server;
					cn.DatabaseName=PrefC.ReadOnlyServer.Server=="" ? DataConnection.GetDatabaseName() : PrefC.ReadOnlyServer.Database;
					cn.MySqlUser=PrefC.ReadOnlyServer.Server=="" ? DataConnection.GetMysqlUser() : PrefC.ReadOnlyServer.MySqlUser;
					cn.MySqlPassword=PrefC.ReadOnlyServer.Server=="" ? DataConnection.GetMysqlPass() : PrefC.ReadOnlyServer.MySqlPass;
					//no ternary operator because URI will be blank if they're not using a middle tier read-only server.
					cn.ServiceURI=PrefC.ReadOnlyServer.URI;
					//need to add PrefC.ReportingServer.SslCa call later to reporting server implementation Job:45000
					//As for the reporting server: the main connection's CA for the main connection, otherwise only the CA of its own preference.
					cn.SslCa=PrefC.ReadOnlyServer.Server=="" ? DataConnection.GetSslCa() : PrefC.ReadOnlyServer.SslCa;
					//Connection string is not currently supported for read-only servers.
					//If ServerName is null or empty, then the current instance of Open Dental is utilizing a connection string.
					//The connection string should be preserved in order for reports(stuff) to continue to work for non-read-only server queries.
					if(string.IsNullOrEmpty(cn.ServerName)) {
						cn.ConnectionString=PrefC.ReadOnlyServer.ConnectionString=="" ? DataConnection.GetConnectionString() : PrefC.ReadOnlyServer.ConnectionString;
					}
					if(PrefC.ReadOnlyServer.Server!="") {
						//As for the reporting server: a read-only server of its own connects as before, the main connection stays under the rule.
						cn.MarkPreferenceServer();
					}
				});
				return cn;
			};
		}

		public new static ConnectionNames CurrentConnection {
			get {
				return ConnectionStoreBase.CurrentConnection;
			}
		}

		///<summary>Private ctor prevents this class from being instansiated. We will just use the class for static init.</summary>
		private ConnectionStore() {
		}

		///<summary>Get a central connection by name.</summary>
		public static OpenPmsDentalBusiness.CentralConnection GetConnection(ConnectionNames name) {
			return (CentralConnection)ConnectionStoreBase.GetConnection(name);
		}

		///<summary>Overrides any current connection with the connection passed in.  This should rarely be used.  E.g. used in Unit Testing.</summary>
		public static void OverrideConnection(ConnectionNames name,CentralConnection centralConnection) {
			ConnectionStoreBase.OverrideConnection(name,(CentralConnectionBase)centralConnection);
		}

		///<summary>Sets the connection of the current thread to the ConnectionName indicated. Connection details will be retrieved from ConnectionStore.xml.</summary>
		public static OpenPmsDentalBusiness.CentralConnection SetDb(ConnectionNames dbName,bool skipValidation=false) {
			return (CentralConnection)ConnectionStoreBase.SetDb(dbName,skipValidation);
		}

		///<summary>Sets the connection of the current thread to the ConnectionName indicated. Connection details will be retrieved from ConnectionStore.xml.</summary>
		public static OpenPmsDentalBusiness.CentralConnection SetDbT(ConnectionNames dbName,DataConnection dataConn=null) {
			return (CentralConnection)ConnectionStoreBase.SetDbT(dbName,dataConn);
		}

	}
}
