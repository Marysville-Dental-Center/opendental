// Connections of the manual grants test: the local MariaDB at 127.0.0.1:3306 only, built by the program's own connection string builder and
// opened through the connection rule (a loopback server, exempt from TLS).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DataConnectionBase;
using MySqlConnector;

namespace DbGrantsTests {
	///<summary>Opens connections to the local server, as root (blank password, the developer configuration) or as the temporary account, and
	///runs statements on them. Nothing here connects to any other host or port.</summary>
	internal static class GrantServer {
		public const string SERVER="127.0.0.1";
		public const string ROOT_USER="root";
		public const string ROOT_PASSWORD="";

		///<summary>The program's connection string for this server, database and user (DataConnection.BuildSimpleConnectionString).</summary>
		public static string GetConnectionString(string database,string user,string password) {
			return DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,SERVER,database,user,password);
		}

		///<summary>An open connection through the connection rule (DbConnectionSecurity.NewMySqlConnection).</summary>
		public static MySqlConnection Open(string database,string user,string password) {
			MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(GetConnectionString(database,user,password));
			mySqlConnection.Open();
			return mySqlConnection;
		}

		public static MySqlConnection OpenRoot(string database="") {
			return Open(database,ROOT_USER,ROOT_PASSWORD);
		}

		public static void Exec(MySqlConnection mySqlConnection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,mySqlConnection)) {
				mySqlCommand.CommandTimeout=3600;
				mySqlCommand.ExecuteNonQuery();
			}
		}

		///<summary>The first column of the first row, or null.</summary>
		public static string Scalar(MySqlConnection mySqlConnection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,mySqlConnection)) {
				object value=mySqlCommand.ExecuteScalar();
				return value==null || value==DBNull.Value ? null : Convert.ToString(value,CultureInfo.InvariantCulture);
			}
		}

		///<summary>The first column of every row.</summary>
		public static List<string> Column(MySqlConnection mySqlConnection,string command) {
			List<string> listValues=new List<string>();
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,mySqlConnection))
			using(MySqlDataReader mySqlDataReader=mySqlCommand.ExecuteReader())
			{
				while(mySqlDataReader.Read()) {
					listValues.Add(mySqlDataReader.IsDBNull(0) ? null : Convert.ToString(mySqlDataReader.GetValue(0),CultureInfo.InvariantCulture));
				}
			}
			return listValues;
		}

		public static void ExecRoot(params string[] arrayCommands) {
			using(MySqlConnection mySqlConnection=OpenRoot()) {
				foreach(string command in arrayCommands) {
					Exec(mySqlConnection,command);
				}
			}
		}

		public static string ScalarRoot(string command) {
			using(MySqlConnection mySqlConnection=OpenRoot()) {
				return Scalar(mySqlConnection,command);
			}
		}

		public static List<string> ColumnRoot(string command) {
			using(MySqlConnection mySqlConnection=OpenRoot()) {
				return Column(mySqlConnection,command);
			}
		}

		///<summary>A database name as a GRANT writes it between backticks: '_' and '%' are wildcards there, so each is escaped with '\' (the server
		///guide, section 5).</summary>
		public static string EscapeForGrant(string name) {
			return name.Replace("\\","\\\\").Replace("_","\\_").Replace("%","\\%");
		}

		///<summary>A value for a LIKE pattern in single quotes: '_' and '%' escaped, quotes and backslashes doubled for the string literal.</summary>
		public static string EscapeForLike(string value) {
			return value.Replace("\\","\\\\\\\\").Replace("_","\\_").Replace("%","\\%").Replace("'","''");
		}

		///<summary>A string literal's content: quotes and backslashes escaped.</summary>
		public static string EscapeString(string value) {
			return value.Replace("\\","\\\\").Replace("'","''");
		}

		///<summary>Whether a database name falls under the database pattern of a GRANT (as SHOW GRANTS prints it between backticks): '_' is any one
		///character, '%' any characters, '\' makes the next character literal. Case-insensitive, as names are on Windows.</summary>
		public static bool IsMatchGrantPattern(string pattern,string databaseName) {
			StringBuilder stringBuilder=new StringBuilder("^");
			for(int i=0;i<pattern.Length;i++) {
				char c=pattern[i];
				if(c=='\\' && i+1<pattern.Length) {
					i++;
					stringBuilder.Append(Regex.Escape(pattern[i].ToString()));
				}
				else if(c=='_') {
					stringBuilder.Append('.');
				}
				else if(c=='%') {
					stringBuilder.Append(".*");
				}
				else {
					stringBuilder.Append(Regex.Escape(c.ToString()));
				}
			}
			stringBuilder.Append('$');
			return Regex.IsMatch(databaseName,stringBuilder.ToString(),RegexOptions.IgnoreCase|RegexOptions.Singleline);
		}

		///<summary>The database patterns of the GRANT lines (ON `pattern`.*), with doubled backticks undone.</summary>
		public static List<string> GetDatabasePatterns(List<string> listGrantLines) {
			List<string> listPatterns=new List<string>();
			foreach(string line in listGrantLines) {
				foreach(Match match in Regex.Matches(line??"",@" ON `((?:[^`]|``)+)`\.")) {
					listPatterns.Add(match.Groups[1].Value.Replace("``","`"));
				}
			}
			return listPatterns;
		}

		///<summary>A SHOW GRANTS line without the password hash (IDENTIFIED BY PASSWORD '...').</summary>
		public static string WithoutPasswordHash(string grantLine) {
			return Regex.Replace(grantLine??"",@"IDENTIFIED BY PASSWORD '[^']*'","IDENTIFIED BY PASSWORD '<hash>'");
		}

		///<summary>A random password of 32 letters and digits.</summary>
		public static string NewPassword() {
			const string CHARS="ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
			byte[] arrayBytes=new byte[32];
			using(System.Security.Cryptography.RandomNumberGenerator randomNumberGenerator=System.Security.Cryptography.RandomNumberGenerator.Create()) {
				randomNumberGenerator.GetBytes(arrayBytes);
			}
			StringBuilder stringBuilder=new StringBuilder();
			foreach(byte b in arrayBytes) {
				stringBuilder.Append(CHARS[b%CHARS.Length]);
			}
			return stringBuilder.ToString();
		}
	}
}
