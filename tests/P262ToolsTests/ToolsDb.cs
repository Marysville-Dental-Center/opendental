// One scratch database for the P262-TOOLS tests on the local MariaDB (127.0.0.1:3306, root, blank password): the committed
// db/init/new-database.sql with every fork migration of this build run on it. Its name starts with "test_p262tools_" plus this run's id, so a test
// can never drop anything else and runs from other worktrees never collide. Synthetic rows only. Skipped (inconclusive) when the server does not
// answer, or when P262_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using Crud=OpenPmsDentalBusiness.Crud;
using OpenPmsDentalBusiness.ForkMigrations;
using UnitTestsCore;

namespace P262ToolsTests {
	[TestClass]
	public class ToolsDb {
		public const string Server="127.0.0.1";
		public const string User="root";
		public const string Password="";
		public const string PREFIX="test_p262tools_";
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		///<summary>Start time (UTC, yyMMddHHmmss), process id and a random number.</summary>
		private static readonly string _runId=DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"
			+new Random().Next(1000,10000);
		public static readonly string DbName=PREFIX+_runId+"_main";
		///<summary>Null when the database is ready; otherwise why the database tests are skipped.</summary>
		public static string SkipReason;
		private static bool _isBuilt;
		private static int _counter;

		[AssemblyInitialize]
		public static void AssemblyInitialize(TestContext testContext) {
			SetCulture();
			SkipReason=GetSkipReason();
			if(SkipReason!=null) {
				return;
			}
			try {
				DropStale();
				CreateFromInitFile(DbName);
				ForkMigrationRunner.Run();
				Cache.Refresh(InvalidType.Prefs,InvalidType.ProcCodes,InvalidType.Providers,InvalidType.Defs,InvalidType.FeeScheds);
				Userods.RefreshCache();
				Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1,UserName="Admin" };
				_isBuilt=true;
			}
			catch(Exception ex) {
				SkipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
			}
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			if(!_isBuilt || Environment.GetEnvironmentVariable("P262_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(DbName);
			}
			catch(Exception) {
				//A later run drops it once it is stale.
			}
		}

		///<summary>Call from [TestInitialize]: skips the test when there is no database, otherwise connects to it as user 1.</summary>
		public static void Begin() {
			if(SkipReason!=null) {
				Assert.Inconclusive(SkipReason);
			}
			SetCulture();
			Connect(DbName);
			Security.CurUser=Userods.GetUser(1)??new Userod { UserNum=1,UserName="Admin" };
		}

		///<summary>Lans.g returns the English text without touching the database only in en-US, and the tests compare dates and amounts as text.</summary>
		public static void SetCulture() {
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentCulture=new CultureInfo("en-US");
			CultureInfo.DefaultThreadCurrentUICulture=new CultureInfo("en-US");
		}

		#region Synthetic rows
		public static int Next() {
			return Interlocked.Increment(ref _counter);
		}

		public static Patient NewPatient() {
			return PatientT.CreatePatient(Next().ToString(),lName:"P262Tools",fName:"Synthetic");
		}

		///<summary>A payment type definition made for the tests (the cache is refreshed).</summary>
		public static long NewPayType() {
			Def def=new Def();
			def.Category=DefCat.PaymentTypes;
			def.ItemName="P262 test type "+Next();
			def.ItemOrder=100+_counter;
			Crud.DefCrud.Insert(def);
			Cache.Refresh(InvalidType.Defs);
			return def.DefNum;
		}

		///<summary>A payment row exactly as given, written with the CRUD so that no business rule changes it.</summary>
		public static Payment InsertPayment(long patNum,long payType,double payAmt,DateTime payDate,long depositNum=0,
			CreditCardSource creditCardSource=CreditCardSource.None,bool isRecurringCC=false)
		{
			Payment payment=new Payment();
			payment.PatNum=patNum;
			payment.PayType=payType;
			payment.PayAmt=payAmt;
			payment.PayDate=payDate;
			payment.DepositNum=depositNum;
			payment.PaymentSource=creditCardSource;
			payment.IsRecurringCC=isRecurringCC;
			payment.PayNote="P262-TOOLS test";
			Crud.PaymentCrud.Insert(payment);
			return payment;
		}

		///<summary>A paysplit row exactly as given (DatePay is the payment's date unless given), written with the CRUD.</summary>
		public static PaySplit InsertSplit(Payment payment,double amount,long procNum=0,long adjNum=0,long fSplitNum=0,long payPlanNum=0,
			long unearnedType=0,DateTime datePay=default(DateTime))
		{
			PaySplit paySplit=new PaySplit();
			paySplit.PayNum=payment.PayNum;
			paySplit.PatNum=payment.PatNum;
			paySplit.SplitAmt=amount;
			paySplit.DatePay=(datePay.Year>1 ? datePay : payment.PayDate);
			paySplit.ProcNum=procNum;
			paySplit.AdjNum=adjNum;
			paySplit.FSplitNum=fSplitNum;
			paySplit.PayPlanNum=payPlanNum;
			paySplit.UnearnedType=unearnedType;
			paySplit.ProvNum=1;
			Crud.PaySplitCrud.Insert(paySplit);
			return paySplit;
		}

		///<summary>A completed procedure of the first procedure code on the given date.</summary>
		public static Procedure InsertProcedure(long patNum,DateTime dateProc) {
			Procedure procedure=new Procedure();
			procedure.PatNum=patNum;
			procedure.ProcDate=dateProc;
			procedure.DateEntryC=dateProc;
			procedure.ProcStatus=ProcStat.C;
			procedure.ProcFee=100;
			procedure.ProvNum=1;
			procedure.CodeNum=ProcedureCodes.GetFirstOrDefaultFromList(x => true).CodeNum;
			Crud.ProcedureCrud.Insert(procedure);
			return procedure;
		}

		public static Adjustment InsertAdjustment(long patNum,DateTime dateAdj,double amount) {
			Adjustment adjustment=new Adjustment();
			adjustment.PatNum=patNum;
			adjustment.AdjDate=dateAdj;
			adjustment.ProcDate=dateAdj;
			adjustment.AdjAmt=amount;
			adjustment.ProvNum=1;
			Crud.AdjustmentCrud.Insert(adjustment);
			return adjustment;
		}

		public static Payment GetPayment(long payNum) {
			return Crud.PaymentCrud.SelectOne(payNum);
		}

		public static List<PaySplit> GetSplits(long payNum) {
			return Crud.PaySplitCrud.SelectMany("SELECT * FROM paysplit WHERE PayNum="+POut.Long(payNum)+" ORDER BY SplitNum");
		}

		///<summary>DBM log rows of one method for one FKey.</summary>
		public static List<DbmLog> GetDbmLogs(string methodName,long fKey) {
			return Crud.DbmLogCrud.SelectMany("SELECT * FROM dbmlog WHERE MethodName='"+POut.String(methodName)+"' AND FKey="+POut.Long(fKey));
		}

		///<summary>Security log rows of one permission for one FKey.</summary>
		public static List<SecurityLog> GetSecurityLogs(EnumPermType permType,long fKey) {
			return Crud.SecurityLogCrud.SelectMany("SELECT * FROM securitylog WHERE PermType="+POut.Int((int)permType)+" AND FKey="+POut.Long(fKey)
				+" ORDER BY SecurityLogNum");
		}
		#endregion Synthetic rows

		#region Scratch database
		public static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("P262_TEST_DB")=="skip") {
				return "P262_TEST_DB=skip";
			}
			try {
				using(MySqlConnection connection=OpenServer()) {
					string sqlMode=Scalar(connection,"SELECT @@GLOBAL.sql_mode");
					if(sqlMode!="" && sqlMode.ToUpperInvariant()!="NO_AUTO_CREATE_USER") {
						return "Global sql_mode is '"+sqlMode+"'; the business layer expects a blank sql_mode.";
					}
				}
				return null;
			}
			catch(Exception ex) {
				return "No local MariaDB at "+Server+":3306 as "+User+" with a blank password: "+ex.GetBaseException().Message;
			}
		}

		public static MySqlConnection OpenServer(string database="") {
			MySqlConnection connection=new MySqlConnection("Server="+Server+";Port=3306;User ID="+User+";Password="+Password
				+(database=="" ? "" : ";Database="+database)+";Connection Timeout=15;Allow User Variables=true;Default Command Timeout=600");
			connection.Open();
			return connection;
		}

		///<summary>Drops leftovers of earlier runs (a crash, or P262_KEEP_DB=1) once they are older than 12 hours.</summary>
		private static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=OpenServer())
			using(MySqlCommand command=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'test\\_p262tools\\_%'",
				connection))
			using(MySqlDataReader reader=command.ExecuteReader())
			{
				while(reader.Read()) {
					listNames.Add(reader.GetString(0));
				}
			}
			foreach(string name in listNames) {
				Match match=regex.Match(name);
				if(!match.Success || !DateTime.TryParseExact(match.Groups[1].Value,"yyMMddHHmmss",CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out DateTime dateStarted))
				{
					continue;
				}
				if(DateTime.UtcNow-dateStarted<_timeSpanStale) {
					continue;
				}
				try {
					Drop(name);
				}
				catch(Exception) {
					//Another run may be dropping it at the same time.
				}
			}
		}

		///<summary>An empty utf8mb3 database with the committed new-database file loaded into it, statement by statement. Leaves the business layer
		///connected to it.</summary>
		private static void CreateFromInitFile(string name) {
			Drop(name);
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"CREATE DATABASE `"+name+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			}
			string text=File.ReadAllText(FindRepoFile(Path.Combine("db","init","new-database.sql")),new UTF8Encoding(false)).Replace("\r\n","\n");
			using(MySqlConnection connection=OpenServer(name)) {
				//Every statement of the generated file ends with a semicolon at the end of a line, and no value holds a raw line break.
				foreach(string statement in text.Split(new[] { ";\n" },StringSplitOptions.None)) {
					if(statement.Trim()=="") {
						continue;
					}
					Exec(connection,statement);
				}
			}
			Connect(name);
			if(!Prefs.DictIsNull()) {
				Prefs.ClearCache();
			}
			Cache.Refresh(InvalidType.Prefs);
		}

		public static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("Tests only drop databases whose name starts with "+PREFIX+".");
			}
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
		}

		public static void Connect(string name) {
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,name,User,Password,"","",DatabaseType.MySql,true);
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.CommandTimeout=600;
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static string Scalar(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				object value=mySqlCommand.ExecuteScalar();
				return value==null || value==DBNull.Value ? null : Convert.ToString(value,CultureInfo.InvariantCulture);
			}
		}

		///<summary>Walks up from the test assembly's folder to the repository root and returns the path of a file in it.</summary>
		public static string FindRepoFile(string relativePath) {
			DirectoryInfo dir=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(dir!=null) {
				string candidate=Path.Combine(dir.FullName,relativePath);
				if(File.Exists(candidate)) {
					return candidate;
				}
				dir=dir.Parent;
			}
			throw new FileNotFoundException("Not found above the test folder: "+relativePath);
		}
		#endregion Scratch database
	}
}
