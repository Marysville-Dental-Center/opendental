/*=============================================================================================================
Open Dental GPL license Copyright (C) 2003  Jordan Sparks, DMD.  http://www.open-dent.com,  www.docsparks.com
See header in FormOpenDental.cs for complete text.  Redistributions must retain this text.
===============================================================================================================*/
//Changed in Open PMS - Dental (package RDL-SRC, docs/port-notes/RDL-SRC.md): MySqlConnector replaces MySql.Data, which the program no longer
//ships, and the connection string is the one the running program uses, with FreeDentalConfig.xml as the fallback.
//Changed in Open PMS - Dental (docs/specs/db-connection-security.md): connections go through the program's connection rule, and the
//fallback reads FreeDentalConfig.xml with the program's own reader and connection string builder.
using MySqlConnector;
using System;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using OpenPmsDentalBusiness;
using CodeBase;

namespace ODR{

	///<summary>This is replacing the DataClass as the preferred way to interact with the database.</summary>
	public class DataConnection{//
		///<summary>This data adapter is used for all queries to the database.</summary>
		private MySqlDataAdapter da;
		///<summary>This is the connection that is used by the data adapter for all queries.</summary>
		private MySqlConnection con;
		///<summary>Used to get very small bits of data from the db when the data adapter would be overkill.  For instance retrieving the response after a command is sent.</summary>
		private MySqlDataReader dr;
		///<summary>Stores the string of the command that will be sent to the database.</summary>
		private MySqlCommand cmd;
		///<summary>After inserting a row, this variable will contain the primary key for the newly inserted row.  This can frequently save an additional query to the database.</summary>
		public long InsertID;

		///<summary>Constructor sets the connection values.</summary>
		public DataConnection(){
			//Through the program's connection rule: a server over the network must be encrypted and verified (docs/specs/db-connection-security.md).
		  con=global::DataConnectionBase.DbConnectionSecurity.NewMySqlConnection(GetODConnStr());
			//dr = null;
			cmd = new MySqlCommand();
			cmd.Connection=con;
			//table=new DataTable();
		}

		///<summary>The connection string for report queries. Report definitions name it: =ODR.DataConnection.GetODConnStr(). It is the connection of
		///the program this assembly is loaded into, so a report reads the database that this copy of the program is connected to, even when several
		///copies are open or the database was chosen without saving FreeDentalConfig.xml. Without one (a middle tier client, or ODR loaded outside
		///the program), it is built from FreeDentalConfig.xml in the program folder, as it always was. Empty when neither is available.</summary>
		public static string GetODConnStr() {
			string connStr=GetProgramConnStr();
			if(connStr!="") {
				return connStr;
			}
			return GetConfigFileConnStr();
		}

		///<summary>The connection string of the program's own direct MySQL connection (DataConnectionBase.DataConnection), or empty if it has none.
		///</summary>
		private static string GetProgramConnStr() {
			try {
				if(!global::DataConnectionBase.DataConnection.HasDatabaseConnection
					|| global::DataConnectionBase.DataConnection.DBtype!=global::DataConnectionBase.DatabaseType.MySql)
				{
					return "";
				}
				return global::DataConnectionBase.DataConnection.GetCurrentConnectionString()??"";
			}
			catch {
				return "";
			}
		}

		///<summary>Built from FreeDentalConfig.xml in the program folder with the program's own reader and builder (DbConnectionConfig,
		///BuildSimpleConnectionString), so a server over the network gets the office CA, the client certificate and the password stored on this
		///computer, as the program does (docs/specs/db-connection-security.md). Empty if the file is missing or unreadable, or its settings are
		///refused (an old-style password for a server over the network).</summary>
		private static string GetConfigFileConnStr() {
			string path=ODFileUtils.CombinePaths(Application.StartupPath,ProgramNames.ConfigFileName);
			try {
				global::DataConnectionBase.DbConnectionConfig dbConnectionConfig=global::DataConnectionBase.DbConnectionConfig.ReadFile(path);
				if(dbConnectionConfig==null) {
					return "";
				}
				string password=dbConnectionConfig.GetPassword();
				return global::DataConnectionBase.DataConnection.BuildSimpleConnectionString(global::DataConnectionBase.DatabaseType.MySql,
					dbConnectionConfig.ServerName,dbConnectionConfig.Database,dbConnectionConfig.User,password,dbConnectionConfig.SslCa,
					dbConnectionConfig.ClientCertThumbprint);
			}
			catch {
				return "";
			}
		}

		///<summary>Fills table with data from the database.</summary>
		public DataTable GetTable(string command){
			cmd.CommandText=command;
			DataTable table=new DataTable();
 			try{
 				da=new MySqlDataAdapter(cmd);
 				da.Fill(table);
 			}
			//catch(MySql.Data.Types.MySqlConversionException){
			//	MsgBox.Show(this,"Invalid date found. Please fix dates in the Check Database Integrity tool in your main menu under misc tools");
			//}
			catch(Exception){
				MessageBox.Show(command);
			}
			//catch(MySqlException e){
			//	MessageBox.Show("Error: "+e.Message+","+cmd.CommandText);
			//}
			finally{
				con.Close();
			}
 			return table;
		}

		///<summary>Sends a non query command to the database and returns the number of rows affected. If true, then InsertID will be set to the value of the primary key of the newly inserted row.</summary>
		public long NonQ(string command){
			return NonQ(command,false);
		}

		///<summary>Sends a non query command to the database and returns the number of rows affected. If true, then InsertID will be set to the value of the primary key of the newly inserted row.</summary>
		public long NonQ(string command,bool getInsertID){
 			cmd.CommandText=command;
			int rowsChanged=0;
 			try{
				con.Open();
 				rowsChanged=cmd.ExecuteNonQuery();
 				if(getInsertID){
					cmd.CommandText="SELECT LAST_INSERT_ID()";
					dr=(MySqlDataReader)cmd.ExecuteReader();
					if(dr.Read()) {
						InsertID=PIn.Long(dr[0].ToString());
					}
				}
			}
			catch(MySqlException e){
				MessageBox.Show("Error: "+e.Message+","+cmd.CommandText);
			}
			//catch{
			//	MessageBox.Show("Error: "+);
			//}
			finally{
				con.Close();
			}
 			return rowsChanged;
 		}

		///<summary>Submits an array of commands in sequence. Used in conversions. Throws an exception if unsuccessful.  Returns the number of rows affected</summary>
		public int NonQ(string[] commands){
			int rowsChanged=0;
			con.Open();
			for(int i=0;i<commands.Length;i++){
				cmd.CommandText=commands[i];
				//Debug.WriteLine(cmd.CommandText);
				rowsChanged+=cmd.ExecuteNonQuery();
			}
			con.Close();
			return rowsChanged;
		}

		///<summary>Use this for count(*) queries.  They are always guaranteed to return one and only one value.  Uses datareader instead of datatable, so faster.  Can also be used when retrieving prefs manually, since they will also return exactly one value</summary>
		public string GetCount(string command){
			cmd.CommandText=command;
			con.Open();
			dr=(MySqlDataReader)cmd.ExecuteReader();
			dr.Read();
			string retVal=dr[0].ToString();
			con.Close();
			return retVal;
		}



	}



}









