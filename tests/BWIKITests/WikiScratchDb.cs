// Scratch database for the B-WIKI tests on the local MariaDB (127.0.0.1:3306, root, blank password). Synthetic rows only; nothing here reads or
// copies an office database. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using DataConnectionBase;
using MySqlConnector;
using OpenDentBusiness;

namespace BWIKITests {
	///<summary>Creates, connects to and drops this run's scratch database. Every name starts with "bwiki_" so the tests can never drop anything
	///else.</summary>
	public static class WikiScratchDb {
		private const string CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=5";
		private const string PREFIX="bwiki_";
		///<summary>Unique to this run: start time (UTC), process id and a random number, so parallel runs from other worktrees never share or drop
		///each other's database.</summary>
		public static readonly string DbName=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);
		private static string _skipReason;
		private static bool _isCreated;
		private static readonly object _lock=new object();

		///<summary>Creates the database once per run with the Open Dental 25.3.48 definitions of the tables the wiki code touches. Returns null when
		///ready, otherwise why database tests are skipped.</summary>
		public static string EnsureCreated() {
			lock(_lock) {
				if(_isCreated || _skipReason!=null) {
					return _skipReason;
				}
				if(Environment.GetEnvironmentVariable("BWIKI_TEST_DB")=="skip") {
					_skipReason="BWIKI_TEST_DB=skip";
					return _skipReason;
				}
				try {
					using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
						connection.Open();
						DropStale(connection);
						Exec(connection,"DROP DATABASE IF EXISTS `"+DbName+"`");
						Exec(connection,"CREATE DATABASE `"+DbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
						Exec(connection,"USE `"+DbName+"`");
						Exec(connection,"CREATE TABLE wikipage (WikiPageNum bigint(20) NOT NULL AUTO_INCREMENT,UserNum bigint(20) NOT NULL,"
							+"PageTitle varchar(255) NOT NULL,KeyWords varchar(255) NOT NULL,PageContent mediumtext NOT NULL,"
							+"DateTimeSaved datetime NOT NULL DEFAULT '0001-01-01 00:00:00',IsDraft tinyint(4) NOT NULL,IsLocked tinyint(4) NOT NULL,"
							+"IsDeleted tinyint(4) NOT NULL DEFAULT 0,PageContentPlainText mediumtext NOT NULL,PRIMARY KEY (WikiPageNum),KEY UserNum (UserNum)) "
							+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
						Exec(connection,"CREATE TABLE wikipagehist (WikiPageNum bigint(20) NOT NULL AUTO_INCREMENT,UserNum bigint(20) NOT NULL,"
							+"PageTitle varchar(255) NOT NULL,PageContent mediumtext NOT NULL,DateTimeSaved datetime NOT NULL DEFAULT '0001-01-01 00:00:00',"
							+"IsDeleted tinyint(4) NOT NULL,PRIMARY KEY (WikiPageNum),KEY UserNum (UserNum)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
						Exec(connection,"CREATE TABLE userodpref (UserOdPrefNum bigint(20) NOT NULL AUTO_INCREMENT,UserNum bigint(20) NOT NULL,"
							+"Fkey bigint(20) NOT NULL,FkeyType tinyint(4) NOT NULL,ValueString text NOT NULL,ClinicNum bigint(20) NOT NULL,"
							+"PRIMARY KEY (UserOdPrefNum),KEY UserNum (UserNum),KEY Fkey (Fkey),KEY ClinicNum (ClinicNum)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
						Exec(connection,"CREATE TABLE signalod (SignalNum bigint(20) NOT NULL AUTO_INCREMENT,DateViewing date NOT NULL DEFAULT '0001-01-01',"
							+"SigDateTime datetime NOT NULL DEFAULT '0001-01-01 00:00:00',FKey bigint(20) NOT NULL,FKeyType varchar(255) NOT NULL,"
							+"IType tinyint(4) NOT NULL,RemoteRole tinyint(4) NOT NULL,MsgValue text NOT NULL,PRIMARY KEY (SignalNum),"
							+"KEY indexSigDateTime (SigDateTime),KEY FKey (FKey),KEY IType (IType)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
						Exec(connection,"CREATE TABLE preference (PrefName varchar(255) NOT NULL DEFAULT '',ValueString text NOT NULL,"
							+"PrefNum bigint(20) NOT NULL AUTO_INCREMENT,Comments text DEFAULT NULL,PRIMARY KEY (PrefNum),KEY PrefName (PrefName)) "
							+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
						//The CRUD layer reads RandomPrimaryKeys before every insert. Images live in a local A to Z folder (DataStorageType.LocalAtoZ).
						Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES('RandomPrimaryKeys','0'),('AtoZfolderUsed','1'),"
							+"('DocPath',''),('CorruptedDatabase','0')");
					}
					RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
					new DataConnection().SetDb("127.0.0.1",DbName,"root","","root","",DatabaseType.MySql,true);
					Prefs.RefreshCache();
					_isCreated=true;
				}
				catch(Exception ex) {
					_skipReason="No scratch MariaDB database at 127.0.0.1:3306: "+ex.GetBaseException().Message;
				}
				return _skipReason;
			}
		}

		///<summary>Drops this run's database. Called once from the assembly cleanup.</summary>
		public static void Drop() {
			lock(_lock) {
				if(!_isCreated) {
					return;
				}
				try {
					using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
						connection.Open();
						Exec(connection,"DROP DATABASE IF EXISTS `"+DbName+"`");
					}
				}
				catch(Exception) {
					//A scratch database left behind holds synthetic rows only; a later run drops it once it is stale.
				}
				_isCreated=false;
			}
		}

		///<summary>Drops bwiki_ databases that earlier runs left behind more than 12 hours ago (a crash). Never a database of a run that may still be
		///going.</summary>
		private static void DropStale(MySqlConnection connection) {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+$");
			List<string> listNames=new List<string>();
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'bwiki\\_%'",connection))
			using(MySqlDataReader reader=command.ExecuteReader()) {
				while(reader.Read()) {
					listNames.Add(reader.GetString(0));
				}
			}
			foreach(string name in listNames) {
				Match match=regex.Match(name);
				DateTime dateStarted;
				if(!match.Success || !DateTime.TryParseExact(match.Groups[1].Value,"yyMMddHHmmss",CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out dateStarted))
				{
					continue;
				}
				if(DateTime.UtcNow-dateStarted<TimeSpan.FromHours(12)) {
					continue;
				}
				try {
					Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
				}
				catch(Exception) {
					//Another run may be dropping it at the same time.
				}
			}
		}

		public static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		///<summary>An open connection to this run's database outside the program's data layer and not pooled, so a table lock taken on it ends when
		///it is closed. The caller disposes it.</summary>
		public static MySqlConnection OpenSeparateConnection() {
			MySqlConnection connection=new MySqlConnection(CONNECTION+";Database="+DbName+";Pooling=false");
			connection.Open();
			return connection;
		}

		///<summary>Waits until a query of this run's database that reads the table is waiting for a table lock. False when none does in time.</summary>
		public static bool WaitForQueryWaitingOnLock(string tableName,TimeSpan timeSpanMax) {
			DateTime dateTimeStop=DateTime.Now.Add(timeSpanMax);
			using(MySqlConnection connection=OpenSeparateConnection()) {
				while(true) {
					using(MySqlCommand command=new MySqlCommand("SELECT COUNT(*) FROM information_schema.PROCESSLIST WHERE DB=@db "
						+"AND STATE LIKE 'Waiting for table%' AND INFO LIKE @info",connection))
					{
						command.Parameters.AddWithValue("@db",DbName);
						command.Parameters.AddWithValue("@info","%FROM "+tableName+"%");
						if(Convert.ToInt64(command.ExecuteScalar())>0) {
							return true;
						}
					}
					if(DateTime.Now>dateTimeStop) {
						return false;
					}
					System.Threading.Thread.Sleep(50);
				}
			}
		}

		///<summary>Removes every wiki row, user preference and signal, so each test starts from an empty wiki.</summary>
		public static void ClearWiki() {
			DataCore.NonQ("DELETE FROM wikipage");
			DataCore.NonQ("DELETE FROM wikipagehist");
			DataCore.NonQ("DELETE FROM userodpref");
			DataCore.NonQ("DELETE FROM signalod");
			UserOdPrefs.RefreshCache();
		}

		///<summary>Inserts a wiki row directly, as stored: links are [[WikiPageNum]]. Returns its WikiPageNum.</summary>
		public static long InsertPage(string pageTitle,string pageContent,string keyWords="",bool isDraft=false,bool isDeleted=false,
			DateTime dateTimeSaved=default(DateTime))
		{
			WikiPage wikiPage=new WikiPage();
			wikiPage.UserNum=1;
			wikiPage.PageTitle=pageTitle;
			wikiPage.KeyWords=keyWords;
			wikiPage.PageContent=pageContent;
			wikiPage.PageContentPlainText=MarkupEdit.ConvertToPlainText(pageContent);
			wikiPage.DateTimeSaved=(dateTimeSaved==default(DateTime) ? new DateTime(2026,10,1,8,0,0) : dateTimeSaved);
			wikiPage.IsDraft=isDraft;
			wikiPage.IsDeleted=isDeleted;
			return OpenDentBusiness.Crud.WikiPageCrud.Insert(wikiPage);
		}

		///<summary>Sets a page's content directly (for content that holds links to pages inserted after it).</summary>
		public static void SetContent(long wikiPageNum,string pageContent) {
			DataCore.NonQ("UPDATE wikipage SET PageContent='"+POut.String(pageContent)+"',PageContentPlainText='"
				+POut.String(MarkupEdit.ConvertToPlainText(pageContent))+"' WHERE WikiPageNum="+POut.Long(wikiPageNum));
		}
	}
}
