// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CodeBase;
using DataConnectionBase;
using OpenDentBusiness;
using SyntheticData.Infrastructure;

namespace AdoptionValidation {
	///<summary>Points OpenDentBusiness at one database and runs the program's own adoption, migration, verifier and rehash code against it, exactly
	///as the program does (Adoption.Run is what FormAdoptDatabase calls). Nothing here re-implements those steps.</summary>
	public static class BusinessLayer {
		///<summary>Tables with a SecurityHash column that the rehash (SecurityHash.RehashNow) writes.</summary>
		public static readonly string[] ArrayHashedTables={ "appointment","claim","claimproc","inssub","insverify","insverifyhist","patient","payplan","paysplit" };

		///<summary>Connects the business layer (DataCore, Db, the caches) directly to the database. Local server only, on the port the command line
		///gives (through an SSH local forward, runbook step 0.0, that port is the database server's MariaDB).</summary>
		public static void Connect(ServerSettings server,string database) {
			LocalOnly.EnsureLocalHost(server.Server);
			LocalOnly.EnsureSafeIdentifier(database);
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(ServerAndPort(server),database,server.User,server.Password,"","",DatabaseType.MySql,true);
			if(Security.CurUser==null) {
				Security.CurUser=new Userod();//Adoption runs before anyone logs in; a few log helpers read the current user.
			}
		}

		///<summary>The server as DataConnection takes it, "host:port". DataConnection reads the port only from this string and otherwise uses 3306, so
		///without it the business layer would connect to a different server than the tools' own connections whenever --port is not 3306 (an SSH local
		///forward). An IPv6 address cannot carry a port there, so it is refused.</summary>
		public static string ServerAndPort(ServerSettings server) {
			LocalOnly.EnsureLocalHost(server.Server);
			string host=server.Server.Trim();
			if(host.Contains(":")) {
				throw new ArgumentException("Connect with --server 127.0.0.1 or localhost: the business layer cannot take a port with the IPv6 address "+host+".");
			}
			return host+":"+server.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}

		public static string CurrentDatabase() {
			return DataCore.GetScalar("SELECT DATABASE()");
		}

		///<summary>A preference straight from the table, or null when there is no row.</summary>
		public static string GetPref(string prefName) {
			return ForkVersions.GetMarker(prefName);
		}

		public static long Count(string command) {
			return PIn.Long(DataCore.GetScalar(command));
		}

		public static bool ColumnExists(string table,string column) {
			return Count("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='"+POut.String(table)+"' "
				+"AND COLUMN_NAME='"+POut.String(column)+"'")>0;
		}

		public static bool IndexExists(string table,string index) {
			return Count("SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='"+POut.String(table)+"' "
				+"AND INDEX_NAME='"+POut.String(index)+"'")>0;
		}

		///<summary>Indexes that include the given column.</summary>
		public static List<string> IndexesOnColumn(string table,string column) {
			DataTable dataTable=DataCore.GetTable("SELECT DISTINCT INDEX_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() "
				+"AND TABLE_NAME='"+POut.String(table)+"' AND COLUMN_NAME='"+POut.String(column)+"'");
			return dataTable.Rows.Cast<DataRow>().Select(x => x[0].ToString()).ToList();
		}

		///<summary>The rows of each hashed table that the rehash covers (SecurityHash.UpdateHashing): every patient, and the other tables' rows on or
		///after SecurityHash.GetHashingDate(), by the date column the rehash uses.</summary>
		public static string GetHashWindow(string table) {
			string date=POut.Date(OpenDentBusiness.Misc.SecurityHash.GetHashingDate());
			switch(table) {
				case "patient": return "1";
				case "appointment": return "AptDateTime>="+date;
				case "claim": return "DateService>="+date;
				case "claimproc": return "SecDateEntry>="+date;
				case "payplan": return "PayPlanDate>="+date;
				case "paysplit": return "DatePay>="+date;
				default: return "SecDateTEdit>="+date;//inssub, insverify, insverifyhist
			}
		}

		///<summary>For each hashed table: rows, and how many SecurityHash values are blank, in the fork's format (osh1$) or something else (Open
		///Dental's, or the synthetic placeholder); and how many rows in the rehash window lack a fork hash.</summary>
		public static List<HashCount> CountHashes() {
			List<HashCount> listCounts=new List<HashCount>();
			string prefix="'"+POut.String(TamperHash.Prefix)+"%'";
			foreach(string table in ArrayHashedTables) {
				if(!ColumnExists(table,"SecurityHash")) {
					continue;
				}
				string window="("+GetHashWindow(table)+")";
				DataTable dataTable=DataCore.GetTable("SELECT COUNT(*),"
					+"IFNULL(SUM(SecurityHash=''),0),"
					+"IFNULL(SUM(SecurityHash LIKE "+prefix+"),0),"
					+"IFNULL(SUM("+window+"),0),"
					+"IFNULL(SUM("+window+" AND SecurityHash NOT LIKE "+prefix+"),0) "
					+"FROM `"+table+"`");
				DataRow row=dataTable.Rows[0];
				HashCount hashCount=new HashCount {
					Table=table,
					Rows=PIn.Long(row[0].ToString()),
					Blank=PIn.Long(row[1].ToString()),
					Current=PIn.Long(row[2].ToString()),
					InWindow=PIn.Long(row[3].ToString()),
					InWindowNotCurrent=PIn.Long(row[4].ToString()),
				};
				hashCount.Other=hashCount.Rows-hashCount.Blank-hashCount.Current;
				listCounts.Add(hashCount);
			}
			return listCounts;
		}
	}

	public class HashCount {
		public string Table;
		public long Rows;
		public long Blank;
		///<summary>Hashes in the fork's TamperHash format.</summary>
		public long Current;
		///<summary>Hashes in any other format: Open Dental's (or the synthetic placeholder standing in for them).</summary>
		public long Other;
		///<summary>Rows the rehash covers (BusinessLayer.GetHashWindow).</summary>
		public long InWindow;
		///<summary>Rows the rehash covers that do not carry a fork hash.</summary>
		public long InWindowNotCurrent;

		public override string ToString() {
			return Table+": "+Rows+" rows, "+InWindow+" in the rehash window ("+InWindowNotCurrent+" without a fork hash); "+Current+" fork hashes, "+Other
				+" other, "+Blank+" blank";
		}
	}
}
