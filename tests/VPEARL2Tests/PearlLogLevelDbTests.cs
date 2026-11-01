// V-PEARL2: the "Log Level" program property row on a scratch database: inserted once when missing, read through the program link cache, and
// the stored client secret kept out of the log. Synthetic data only. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;
using ForkMigrationTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.Pearl;

namespace VPEARL2Tests {
	[TestClass]
	public class PearlLogLevelDbTests {
		private static readonly string _dbName=TestDb.ScratchName("vpearl2");
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			try {
				TestDb.DropStaleScratchDatabases();
				TestDb.Recreate(_dbName);
				using(MySqlConnection connection=TestDb.OpenServer()) {
					TestDb.Exec(connection,"USE `"+_dbName+"`");
					//The same definitions as Open Dental 24.3's program, programproperty, preference and signalod tables.
					TestDb.Exec(connection,"CREATE TABLE program (ProgramNum bigint(20) NOT NULL AUTO_INCREMENT,ProgName varchar(100) DEFAULT '',"
						+"ProgDesc varchar(100) DEFAULT '',Enabled tinyint(1) unsigned NOT NULL DEFAULT 0,Path text NOT NULL DEFAULT '',"
						+"CommandLine text NOT NULL DEFAULT '',Note text DEFAULT NULL,PluginDllName varchar(255) NOT NULL,ButtonImage text NOT NULL,"
						+"FileTemplate text NOT NULL,FilePath varchar(255) NOT NULL,IsDisabledByHq tinyint(4) NOT NULL,CustErr varchar(255) NOT NULL,"
						+"PRIMARY KEY (ProgramNum)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					TestDb.Exec(connection,"CREATE TABLE programproperty (ProgramPropertyNum bigint(20) NOT NULL AUTO_INCREMENT,ProgramNum bigint(20) NOT NULL,"
						+"PropertyDesc varchar(255) DEFAULT '',PropertyValue text DEFAULT NULL,ComputerName varchar(255) NOT NULL,ClinicNum bigint(20) NOT NULL,"
						+"IsMasked tinyint(4) NOT NULL,IsHighSecurity tinyint(4) NOT NULL,PRIMARY KEY (ProgramPropertyNum),KEY ClinicNum (ClinicNum)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					TestDb.Exec(connection,"CREATE TABLE preference (PrefName varchar(255) NOT NULL DEFAULT '',ValueString text NOT NULL,"
						+"PrefNum bigint(20) NOT NULL AUTO_INCREMENT,Comments text DEFAULT NULL,PRIMARY KEY (PrefNum),KEY PrefName (PrefName)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					TestDb.Exec(connection,"CREATE TABLE signalod (SignalNum bigint(20) NOT NULL AUTO_INCREMENT,DateViewing date NOT NULL DEFAULT '0001-01-01',"
						+"SigDateTime datetime NOT NULL DEFAULT '0001-01-01 00:00:00',FKey bigint(20) NOT NULL,FKeyType varchar(255) NOT NULL,"
						+"IType tinyint(4) NOT NULL,RemoteRole tinyint(4) NOT NULL,MsgValue text NOT NULL,PRIMARY KEY (SignalNum)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					TestDb.Exec(connection,"CREATE TABLE pearlrequest (PearlRequestNum bigint(20) NOT NULL AUTO_INCREMENT,RequestId varchar(255) NOT NULL,"
						+"DocNum bigint(20) NOT NULL,RequestStatus tinyint(4) NOT NULL,DateTSent datetime NOT NULL,DateTChecked datetime NOT NULL,"
						+"PRIMARY KEY (PearlRequestNum),KEY RequestId (RequestId)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					TestDb.Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
				}
				TestDb.Connect(_dbName);
				Prefs.RefreshCache();
				//The Pearl program link as the 24.3 convert script made it (ConvertDatabases8.cs), with made-up credentials.
				Programs.InsertIfMissing(ProgramName.Pearl,"Pearl AI");
				Programs.RefreshCache();
				long programNum=Programs.GetProgramNum(ProgramName.Pearl);
				foreach(KeyValuePair<string,string> pair in new Dictionary<string,string> {
					{ "Text font size","18" },
					{ "Client ID","synthetic-client-id" },
					{ "Client Secret","synthetic-secret-from-24-3" },
					{ "Organization ID","synthetic-org" },
					{ "Office ID","synthetic-office" },
					{ "Show Pearl annotations by default","true" },
					{ "Image categories for automatic upload","BWs,FMXs,Panos" },
				})
				{
					ProgramProperties.Insert(new ProgramProperty { ProgramNum=programNum,PropertyDesc=pair.Key,PropertyValue=pair.Value,ComputerName="" });
				}
				ProgramProperties.RefreshCache();
			}
			catch(Exception ex) {
				_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("FORKMIGRATION_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(_dbName);
			}
			catch(Exception) {
				//A later run drops it once it is stale (TestDb.DropStaleScratchDatabases).
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(_dbName);
			DataCore.NonQ("DELETE FROM programproperty WHERE PropertyDesc='"+POut.String(PearlLog.LOG_LEVEL_PROPERTY)+"'");
			Programs.RefreshCache();
			ProgramProperties.RefreshCache();
		}

		private static Program Pearl {
			get {
				return Programs.GetCur(ProgramName.Pearl);
			}
		}

		private static List<ProgramProperty> GetLogLevelRows() {
			return ProgramProperties.GetWhere(x => x.ProgramNum==Pearl.ProgramNum && x.PropertyDesc==PearlLog.LOG_LEVEL_PROPERTY);
		}

		[TestMethod]
		public void MissingRow_ReadsAsErrorsOnly() {
			Assert.AreEqual(0,GetLogLevelRows().Count);
			Assert.AreEqual("",PearlLog.GetPropValFromCache(PearlLog.LOG_LEVEL_PROPERTY));
			Assert.AreEqual(LogLevel.Error,PearlLog.GetLogLevel(),"A database from 24.3 or 25.3 has no row yet.");
		}

		[TestMethod]
		public void EnsureLogLevelProperty_InsertsThePracticeRowOnce() {
			Assert.IsTrue(PearlLog.EnsureLogLevelProperty(Pearl));
			ProgramProperty programProperty=GetLogLevelRows().Single();
			Assert.AreEqual("0",programProperty.PropertyValue);
			Assert.AreEqual(0,programProperty.ClinicNum);
			Assert.IsFalse(programProperty.IsMasked,"A plain setting.");
			Assert.IsFalse(programProperty.IsHighSecurity);
			Assert.IsFalse(PearlLog.EnsureLogLevelProperty(Pearl),"Idempotent.");
			//A stale cache (another workstation added the row) still gives one row: the insert checks the database.
			ProgramProperties.FillCacheFromTable(DataCore.GetTable("SELECT * FROM programproperty WHERE PropertyDesc!='"+PearlLog.LOG_LEVEL_PROPERTY+"'"));
			PearlLog.EnsureLogLevelProperty(Pearl);
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM programproperty WHERE PropertyDesc='"+PearlLog.LOG_LEVEL_PROPERTY+"'"));
			Assert.AreEqual(1,GetLogLevelRows().Count);
			Assert.AreEqual(7+1,ProgramProperties.GetWhere(x => x.ProgramNum==Pearl.ProgramNum).Count,"The bridge's other rows are untouched.");
		}

		[TestMethod]
		public void StoredLevel_IsReadThroughTheCache() {
			PearlLog.EnsureLogLevelProperty(Pearl);
			Assert.AreEqual(LogLevel.Error,PearlLog.GetLogLevel());
			foreach(Tuple<string,LogLevel> tuple in new[] { Tuple.Create("2",LogLevel.Verbose),Tuple.Create("1",LogLevel.Information),
				Tuple.Create("0",LogLevel.Error) })
			{
				ProgramProperty programProperty=GetLogLevelRows().Single();
				ProgramProperty programPropertyOld=programProperty.Copy();
				programProperty.PropertyValue=tuple.Item1;
				ProgramProperties.Update(programProperty,programPropertyOld);
				ProgramProperties.RefreshCache();
				Assert.AreEqual(tuple.Item2,PearlLog.GetLogLevel(),tuple.Item1);
			}
		}

		[TestMethod]
		public void StoredClientSecret_IsKeptOutOfTheLog() {
			List<string> listLines=new List<string>();
			PearlLog.ActionWriteLine=line => listLines.Add(line);
			try {
				PearlLog.Error("server said: invalid client synthetic-secret-from-24-3");
			}
			finally {
				PearlLog.ActionWriteLine=PearlLog.WriteLineToLoggerFolder;
			}
			Assert.AreEqual("ERROR\tserver said: invalid client [redacted]",listLines.Single());
		}

		[TestMethod]
		public void PollingAnOlderRequest_FindsItsDocumentInPearlRequest() {
			string requestId=Guid.NewGuid().ToString();
			PearlRequests.Insert(new PearlRequest { RequestId=requestId,DocNum=314,RequestStatus=EnumPearlStatus.TimedOut,
				DateTSent=new DateTime(2026,10,25,8,0,0),DateTChecked=new DateTime(2026,10,25,8,10,0) });
			PearlApiClientMock pearlApiClientMock=new PearlApiClientMock();
			pearlApiClientMock.FuncGetImageByRequestIdOutput=(org,id) => new ImageRequestIdResponse { result=new OpenPmsDentalBusiness.Pearl.Result() };
			PearlApiClientLogging pearlApiClientLogging=new PearlApiClientLogging(pearlApiClientMock);//The default lookup, against the database.
			List<string> listLines=new List<string>();
			PearlLog.ActionWriteLine=line => listLines.Add(line);
			Func<string,string> funcGetPropValOld=PearlLog.FuncGetPropVal;
			PearlLog.FuncGetPropVal=desc => desc==PearlLog.LOG_LEVEL_PROPERTY ? "2" : funcGetPropValOld(desc);
			try {
				pearlApiClientLogging.GetOneImageFromPearl(requestId);
				pearlApiClientLogging.GetOneImageFromPearl("not-in-the-table");
			}
			finally {
				PearlLog.ActionWriteLine=PearlLog.WriteLineToLoggerFolder;
				PearlLog.FuncGetPropVal=PearlLog.GetPropValFromCache;
			}
			Assert.AreEqual(2,listLines.Count,string.Join("\n",listLines));
			StringAssert.EndsWith(listLines[0],"\tdocument 314, request "+requestId);
			StringAssert.EndsWith(listLines[1],"\trequest not-in-the-table");
		}

		[TestMethod]
		public void MissingProperty_ReadsBlank() {
			Assert.AreEqual("",PearlLog.GetPropValFromCache("No such property"));
		}
	}
}
