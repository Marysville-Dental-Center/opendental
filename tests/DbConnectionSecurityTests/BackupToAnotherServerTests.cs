// Owner question DB-SEC 2 (answered 2026-11-07, the recommendation): the Backup window's archive tab backs up to another server first, and
// MiscData.MakeABackup copied the rows there with MySqlBulkLoader (LOAD DATA LOCAL INFILE), which MySqlConnector refuses without
// AllowLoadLocalInfile=true, so it stopped at the first table with rows. It now copies them with ordinary INSERT statements, one parameter per
// value. Two scratch databases on the local MariaDB at 127.0.0.1:3306 stand in for the two servers: the program's connection names the server
// "localhost", the backup "127.0.0.1:3306", so MakeABackup takes the other-server path. Synthetic rows only. Scratch databases test_presw_*.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using CodeBase;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class BackupToAnotherServerTests {
		private const string SERVER="127.0.0.1:3306";
		///<summary>The program's own connection names the server this way, so the backup server's name differs and the other-server path runs.</summary>
		private const string SERVER_PROGRAM="localhost";
		private const int ROWS_LARGE=23456;
		private string _dbSource;
		private List<string> _listProgress;

		private static void ExecRoot(params string[] arrayCommands) {
			using MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(
				DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,SERVER,"","root",""));
			mySqlConnection.Open();
			foreach(string command in arrayCommands) {
				using MySqlCommand mySqlCommand=new MySqlCommand(command,mySqlConnection);
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static List<string> GetDatabases(string prefix) {
			using MySqlConnection mySqlConnection=DbConnectionSecurity.NewMySqlConnection(
				DataConnection.BuildSimpleConnectionString(DatabaseType.MySql,SERVER,"","root",""));
			mySqlConnection.Open();
			using MySqlCommand mySqlCommand=new MySqlCommand("SHOW DATABASES LIKE '"+prefix.Replace("_","\\_")+"%'",mySqlConnection);
			List<string> listNames=new List<string>();
			using MySqlDataReader mySqlDataReader=mySqlCommand.ExecuteReader();
			while(mySqlDataReader.Read()) {
				listNames.Add(mySqlDataReader.GetString(0));
			}
			return listNames;
		}

		private void OnFired(ODEventArgs odEventArgs) {
			if(odEventArgs.EventType==ODEventType.ProgressBar && odEventArgs.Tag is string text) {
				_listProgress.Add(text);
			}
		}

		[TestInitialize]
		public void SetUp() {
			_dbSource="test_presw_bksrc_"+Guid.NewGuid().ToString("N").Substring(0,8);
			_listProgress=new List<string>();
			//Every kind of value an office table holds, with NULLs, quotes, backslashes, line breaks, non-ASCII text and every byte value.
			byte[] arrayBytesAll=Enumerable.Range(0,256).Select(x => (byte)x).ToArray();
			string hexAll=string.Concat(arrayBytesAll.Select(x => x.ToString("X2")));
			string textLarge=new string('A',3*1024*1024)+"end";//one row bigger than a statement's share: a statement of its own
			try {
				CreateSource(hexAll,textLarge);
			}
			catch {
				CleanUp();//MSTest does not run TestCleanup after a failed TestInitialize.
				throw;
			}
			//The program's connection: the source database, named "localhost". The two caches MakeABackup reads are filled in memory, so the scratch
			//database needs no program tables.
			new DataConnection().SetDb(SERVER_PROGRAM,_dbSource,"root","","","",DatabaseType.MySql,true);
			ReplicationServers.FillCacheFromTable(new DataTable());
			ODEvent.Fired+=OnFired;
		}

		private void CreateSource(string hexAll,string textLarge) {
			ExecRoot("CREATE DATABASE `"+_dbSource+"` CHARACTER SET utf8",
				"CREATE TABLE `"+_dbSource+"`.`sample` (SampleNum bigint NOT NULL AUTO_INCREMENT PRIMARY KEY,Txt varchar(255),Note mediumtext,Raw longblob,"
					+"DateTimeX datetime NOT NULL DEFAULT '0001-01-01 00:00:00',DateX date NOT NULL DEFAULT '0001-01-01',TimeX time,Amount decimal(20,10),"
					+"Ratio double,Flag tinyint(4) NOT NULL DEFAULT 0,Big bigint,ItemOrder int(11)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
				"INSERT INTO `"+_dbSource+"`.`sample` (Txt,Note,Raw,DateTimeX,DateX,TimeX,Amount,Ratio,Flag,Big,ItemOrder) VALUES "
					+"('O''Brien \\\\ \"quoted\"','line one\\r\\nline two\\ttab','x',"
					+"'2026-11-07 13:45:59','2026-02-28','08:30:00',12345.6789012345,0.1,1,-9223372036854775808,-5),"
					+"(NULL,NULL,NULL,'0001-01-01 00:00:00','0001-01-01',NULL,NULL,NULL,0,NULL,NULL),"
					+"('Zo\u00EB \u2713 \u00E9t\u00E9','caf\u00E9',0x"+hexAll+",'1999-12-31 23:59:59','2000-01-01','-01:02:03',-0.0000000001,"
					+"1.5E300,-128,9223372036854775807,2147483647),"
					+"('','',X'','2026-01-01 00:00:00','2026-01-01','00:00:00',0,0,127,0,0)",
				"CREATE TABLE `"+_dbSource+"`.`largetable` (LargeNum bigint NOT NULL AUTO_INCREMENT PRIMARY KEY,Txt varchar(50) NOT NULL) ENGINE=MyISAM",
				"INSERT INTO `"+_dbSource+"`.`largetable` (Txt) SELECT CONCAT('row ',seq) FROM `"+_dbSource+"`.seq_1_to_"+ROWS_LARGE,
				"CREATE TABLE `"+_dbSource+"`.`documentmisc` (DocMiscNum bigint NOT NULL AUTO_INCREMENT PRIMARY KEY,FileName varchar(255) NOT NULL,"
					+"RawBase64 longtext NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8",
				"INSERT INTO `"+_dbSource+"`.`documentmisc` (FileName,RawBase64) VALUES ('small.txt','c21hbGw='),('large.txt','"+textLarge+"'),"
					+"('after.txt','YWZ0ZXI=')",
				"CREATE TABLE `"+_dbSource+"`.`emptytable` (EmptyNum bigint NOT NULL AUTO_INCREMENT PRIMARY KEY,Txt varchar(10)) ENGINE=InnoDB",
				"CREATE VIEW `"+_dbSource+"`.`sampleview` AS SELECT SampleNum FROM `"+_dbSource+"`.`sample`");
		}

		[TestCleanup]
		public void CleanUp() {
			ODEvent.Fired-=OnFired;
			ReplicationServers.ClearCache();
			List<string> listDrop=GetDatabases(_dbSource);//the source and every backup made of it (they start with its name)
			ExecRoot(listDrop.Select(x => "DROP DATABASE IF EXISTS `"+x+"`").ToArray());
		}

		///<summary>Every row of the table, ordered by its first column, from the database.</summary>
		private static DataTable ReadAll(string database,string table) {
			DataConnection dataConnection=new DataConnection(SERVER,database,"root","",DatabaseType.MySql);
			return dataConnection.GetTable("SELECT * FROM `"+table+"` ORDER BY 1");
		}

		private static void AssertSameRows(string dbSource,string dbBackup,string table) {
			DataTable tableSource=ReadAll(dbSource,table);
			DataTable tableBackup=ReadAll(dbBackup,table);
			Assert.AreEqual(tableSource.Rows.Count,tableBackup.Rows.Count,table+": rows");
			Assert.AreEqual(tableSource.Columns.Count,tableBackup.Columns.Count,table+": columns");
			for(int r=0;r<tableSource.Rows.Count;r++) {
				for(int c=0;c<tableSource.Columns.Count;c++) {
					object valueSource=tableSource.Rows[r][c];
					object valueBackup=tableBackup.Rows[r][c];
					string where=table+" row "+r+" column "+tableSource.Columns[c].ColumnName;
					if(valueSource is byte[] arrayBytesSource) {
						Assert.IsInstanceOfType(valueBackup,typeof(byte[]),where);
						CollectionAssert.AreEqual(arrayBytesSource,(byte[])valueBackup,where);
						continue;
					}
					Assert.AreEqual(valueSource,valueBackup,where);
				}
			}
		}

		[TestMethod]
		public void MakeABackup_ToAnotherServer_CopiesEveryRow_WithInsertStatements() {
			string dbBackup=MiscData.MakeABackup(serverName:SERVER,user:"root",pass:"",doVerify:true,isAutoBackup:false);
			Assert.IsTrue(dbBackup.StartsWith(_dbSource+"backup_"),dbBackup);
			Assert.IsTrue(GetDatabases(_dbSource).Contains(dbBackup));
			foreach(string table in new[] { "sample","largetable","documentmisc","emptytable" }) {
				AssertSameRows(_dbSource,dbBackup,table);
			}
			Assert.AreEqual(ROWS_LARGE,ReadAll(dbBackup,"largetable").Rows.Count,"More rows than one batch of the source (10000).");
			Assert.AreEqual(3*1024*1024+3,ReadAll(dbBackup,"documentmisc").Rows[1]["RawBase64"].ToString().Length);
			DataConnection dataConnectionBackup=new DataConnection(SERVER,dbBackup,"root","",DatabaseType.MySql);
			Assert.AreEqual("0",dataConnectionBackup.GetCount("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+dbBackup
				+"' AND TABLE_NAME='sampleview'"),"Views are not backed up, as before.");
			Assert.AreEqual("MyISAM",dataConnectionBackup.GetScalar("SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+dbBackup
				+"' AND TABLE_NAME='sample'"),"The backup's tables are MyISAM, as before.");
			//The progress window gets one line per table, as before, then the row checks.
			foreach(string table in new[] { "sample","largetable","documentmisc","emptytable" }) {
				CollectionAssert.Contains(_listProgress,"Backing up table: "+table);
				CollectionAssert.Contains(_listProgress,"Verifying backup: "+table);
			}
			//A second backup the same day gets its own name, and copies again.
			string dbBackup2=MiscData.MakeABackup(serverName:SERVER,user:"root",pass:"",doVerify:true,isAutoBackup:false);
			Assert.AreEqual(dbBackup+"_1",dbBackup2);
			AssertSameRows(_dbSource,dbBackup2,"sample");
		}

		[TestMethod]
		public void MakeABackup_ToAnotherServer_NeverUsesLoadDataLocalInfile() {
			string source=TestHelpers.ReadRepoFile("OpenPmsDentalBusiness","Db Multi Table","MiscData.cs");
			Assert.IsFalse(source.Contains("BulkCopy("));
			string sourceConnection=TestHelpers.ReadRepoFile("DataConnectionBase","DataConnection.cs");
			Assert.IsFalse(sourceConnection.Contains("new MySqlBulkLoader("),"Nothing loads rows with LOAD DATA LOCAL INFILE.");
			foreach(string[] arrayPathParts in new[] { new[] { "DataConnectionBase","DataConnection.cs" },new[] { "DataConnectionBase","DbConnectionSecurity.cs" },
				new[] { "OpenPmsDentalBusiness","Db Multi Table","MiscData.cs" } })
			{
				Assert.IsFalse(TestHelpers.ReadRepoFile(arrayPathParts).Contains("AllowLoadLocalInfile=true"),string.Join("/",arrayPathParts));
			}
		}

		[TestMethod]
		public void GetBackupInsertStatements_SplitsByRowsAndSize_OneParameterPerValue() {
			DataTable table=new DataTable();
			table.Columns.Add("ItemNum",typeof(long));
			table.Columns.Add("Odd`Name",typeof(string));
			table.Columns.Add("Raw",typeof(byte[]));
			for(int i=0;i<7;i++) {
				table.Rows.Add((long)i,i==3 ? null : "text "+i,new byte[] { (byte)i });
			}
			List<MiscData.BackupInsertStatement> listStatements=MiscData.GetBackupInsertStatements(table,"my`table",rowsMax:3);
			Assert.AreEqual(3,listStatements.Count);
			CollectionAssert.AreEqual(new[] { 3,3,1 },listStatements.Select(x => x.CountRows).ToArray());
			Assert.AreEqual("INSERT INTO `my``table` (`ItemNum`,`Odd``Name`,`Raw`) VALUES (@r0c0,@r0c1,@r0c2),(@r1c0,@r1c1,@r1c2),(@r2c0,@r2c1,@r2c2)",
				listStatements[0].Command);
			Assert.AreEqual(9,listStatements[0].ListParameters.Count);
			Assert.AreEqual(DBNull.Value,listStatements[1].ListParameters[1].Value,"Row 3's NULL stays NULL.");
			Assert.AreEqual(6L,listStatements[2].ListParameters[0].Value);
			CollectionAssert.AreEqual(new byte[] { 6 },(byte[])listStatements[2].ListParameters[2].Value);
			//By size: a row larger than a statement's share gets a statement of its own; none is ever empty.
			DataTable tableLarge=new DataTable();
			tableLarge.Columns.Add("Txt",typeof(string));
			tableLarge.Rows.Add("a");
			tableLarge.Rows.Add(new string('x',5000));
			tableLarge.Rows.Add("b");
			tableLarge.Rows.Add("c");
			List<MiscData.BackupInsertStatement> listBySize=MiscData.GetBackupInsertStatements(tableLarge,"t",bytesMax:1000);
			CollectionAssert.AreEqual(new[] { 1,1,2 },listBySize.Select(x => x.CountRows).ToArray());
			Assert.AreEqual(0,MiscData.GetBackupInsertStatements(new DataTable(),"t").Count);
		}
	}
}
