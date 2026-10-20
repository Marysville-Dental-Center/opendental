// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using MySqlConnector;

namespace SyntheticData.Infrastructure {
	///<summary>Where to connect. The password comes from the SYNTH_DB_PASSWORD environment variable, never the command line.</summary>
	public class ServerSettings {
		public string Server="127.0.0.1";
		public int Port=3306;
		public string User="root";
		public string Password=Environment.GetEnvironmentVariable("SYNTH_DB_PASSWORD")??"";

		public string Describe() {
			return Server+":"+Port;
		}
	}

	///<summary>One connection to the local server. Every session uses UTC, strict SQL mode and utf8mb4, so stored values and
	///checksums do not depend on the machine's settings.</summary>
	public sealed class Db : IDisposable {
		private readonly MySqlConnection _connection;
		public string Database { get; private set; }

		private Db(MySqlConnection connection,string database) {
			_connection=connection;
			Database=database;
		}

		///<summary>Opens a connection to the local server, with the given default database or none.</summary>
		public static Db Open(ServerSettings settings,string database=null) {
			LocalOnly.EnsureLocalHost(settings.Server);
			if(database!=null) {
				LocalOnly.EnsureSafeIdentifier(database);
			}
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder {
				Server=settings.Server,
				Port=(uint)settings.Port,
				UserID=settings.User,
				Password=settings.Password,
				AllowUserVariables=true,
				//Zero dates such as 0000-00-00 are read as DateTime.MinValue instead of failing.
				ConvertZeroDateTime=true,
				CharacterSet="utf8mb4",
				DefaultCommandTimeout=1800,
				ConnectionTimeout=10,
				Pooling=false,
				SslMode=MySqlSslMode.None,
				AllowPublicKeyRetrieval=true,
			};
			if(database!=null) {
				builder.Database=database;
			}
			MySqlConnection connection=new MySqlConnection(builder.ConnectionString);
			connection.Open();
			Db db=new Db(connection,database);
			db.Execute("SET SESSION time_zone='+00:00'");
			db.Execute("SET SESSION sql_mode='STRICT_ALL_TABLES,NO_AUTO_VALUE_ON_ZERO,NO_ENGINE_SUBSTITUTION'");
			return db;
		}

		///<summary>True when a local server answers. Used by tests to skip when no server is running.</summary>
		public static bool CanConnect(ServerSettings settings,out string error) {
			try {
				using(Open(settings)) {
				}
				error=null;
				return true;
			}
			catch(Exception ex) {
				error=ex.Message;
				return false;
			}
		}

		public void Use(string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			Execute("USE `"+database+"`");
			Database=database;
		}

		public int Execute(string sql) {
			using(MySqlCommand command=new MySqlCommand(sql,_connection)) {
				return command.ExecuteNonQuery();
			}
		}

		public object Scalar(string sql) {
			using(MySqlCommand command=new MySqlCommand(sql,_connection)) {
				object value=command.ExecuteScalar();
				return value==DBNull.Value ? null : value;
			}
		}

		public long ScalarLong(string sql) {
			object value=Scalar(sql);
			return value==null ? 0 : Convert.ToInt64(value);
		}

		public DataTable Table(string sql) {
			using(MySqlCommand command=new MySqlCommand(sql,_connection))
			using(MySqlDataReader reader=command.ExecuteReader()) {
				DataTable table=new DataTable();
				table.Load(reader);
				return table;
			}
		}

		///<summary>Rows as arrays of raw values (DBNull becomes null), in result order.</summary>
		public List<object[]> Rows(string sql) {
			List<object[]> listRows=new List<object[]>();
			using(MySqlCommand command=new MySqlCommand(sql,_connection))
			using(MySqlDataReader reader=command.ExecuteReader()) {
				while(reader.Read()) {
					object[] row=new object[reader.FieldCount];
					for(int i=0;i<reader.FieldCount;i++) {
						object value=reader.GetValue(i);
						row[i]=value==DBNull.Value ? null : value;
					}
					listRows.Add(row);
				}
			}
			return listRows;
		}

		public bool DatabaseExists(string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			return ScalarLong("SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='"+database+"'")>0;
		}

		public string ServerVersion() {
			return Convert.ToString(Scalar("SELECT VERSION()"));
		}

		public void Dispose() {
			_connection.Dispose();
		}
	}
}
