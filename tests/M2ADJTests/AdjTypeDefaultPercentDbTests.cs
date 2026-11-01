// Integration tests for adjustment type default percentages (definition.Supp, added by package M2-01) and Percent of Fee adjustments, against a
// scratch MariaDB database on this computer. The database gets the Open Dental 26.1.70 schema from the reference snapshot embedded in
// OpenDentBusiness, and synthetic rows only. Its name is unique to the run and it is dropped afterwards. The tests are skipped (inconclusive)
// when no server answers at 127.0.0.1:3306 as root with a blank password, or when M2ADJ_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace M2ADJTests {
	[TestClass]
	public class AdjTypeDefaultPercentDbTests {
		private const string SERVER="127.0.0.1";
		private const string CONNECTION="Server="+SERVER+";Port=3306;User ID=root;Password=;Connection Timeout=15";
		///<summary>Unique to this run (time, process id, random), because several worktrees and test runners share the one local server.</summary>
		private static readonly string _dbName="m2adj_"+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("M2ADJ_TEST_DB")=="skip") {
				_skipReason="M2ADJ_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"CREATE DATABASE `"+_dbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				}
			}
			catch(Exception ex) {
				_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
				return;
			}
			//The server answers, so from here on a failure is a real failure, not a reason to skip.
			using(MySqlConnection connection=new MySqlConnection(CONNECTION+";Database="+_dbName)) {
				connection.Open();
				//The default reference is 26.1.70 (package M2-01), so definition.Supp is part of it.
				foreach(SchemaTable table in SchemaVerifier.GetReference().Tables) {
					Exec(connection,table.ToDdl());
				}
				//The CRUD layer reads RandomPrimaryKeys on every insert; a new Procedure reads DxIcdVersion.
				Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0'),('DxIcdVersion','9')");
			}
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(SERVER,_dbName,"root","","","",DatabaseType.MySql,true);
			//The tests without a database may have filled the preference cache with a few values; load it from the scratch database instead.
			Prefs.RefreshCache();
			Security.CurUser=new Userod { UserNum=1,UserName="Synthetic" };
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS `"+_dbName+"`");
				}
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless; its name is unique to this run.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			foreach(string table in new[] { "definition","adjustment","procedurelog" }) {
				DataCore.NonQ("DELETE FROM "+table);
			}
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static Def InsertAdjType(string name,string itemValue,string supp,int itemOrder) {
			Def def=new Def { Category=DefCat.AdjTypes,ItemOrder=itemOrder,ItemName=name,ItemValue=itemValue,Supp=supp };
			Defs.Insert(def);
			return def;
		}

		private static string GetSuppFromDb(long defNum) {
			return DataCore.GetScalar("SELECT Supp FROM definition WHERE DefNum="+POut.Long(defNum));
		}

		[TestMethod]
		public void Supp_IsSavedAndLoadedThroughTheDefinitionCache() {
			Def defDiscount=InsertAdjType("Synthetic senior discount","-","10.00",0);
			Def defSurcharge=InsertAdjType("Synthetic surcharge","+","150.00",1);
			Def defNoDefault=InsertAdjType("Synthetic write-down","-","",2);
			Def defImageCat=new Def { Category=DefCat.ImageCats,ItemOrder=0,ItemName="Synthetic X-rays",ItemValue="X" };
			Defs.Insert(defImageCat);
			Assert.AreEqual("10.00",GetSuppFromDb(defDiscount.DefNum));
			Assert.AreEqual("",GetSuppFromDb(defImageCat.DefNum),"Other categories leave Supp empty.");
			Defs.RefreshCache();
			Assert.AreEqual(10m,Adjustments.GetDefaultPercent(Defs.GetDef(DefCat.AdjTypes,defDiscount.DefNum)));
			Assert.AreEqual(150m,Adjustments.GetDefaultPercent(Defs.GetDef(DefCat.AdjTypes,defSurcharge.DefNum)));
			Assert.IsNull(Adjustments.GetDefaultPercent(Defs.GetDef(DefCat.AdjTypes,defNoDefault.DefNum)));
			Assert.IsNull(Adjustments.GetDefaultPercent(Defs.GetDef(DefCat.ImageCats,defImageCat.DefNum)));
			//The two lists the adjustment windows show carry the default.
			Assert.AreEqual(10m,Adjustments.GetDefaultPercent(Defs.GetNegativeAdjTypes().Single(x => x.DefNum==defDiscount.DefNum)));
			Assert.AreEqual(150m,Adjustments.GetDefaultPercent(Defs.GetPositiveAdjTypes().Single(x => x.DefNum==defSurcharge.DefNum)));
		}

		[TestMethod]
		public void Supp_ChangesAndClearsOnUpdate() {
			Def def=InsertAdjType("Synthetic courtesy","-","",0);
			Assert.IsTrue(Adjustments.TryGetSuppFromPercentInput("12.5",System.Globalization.CultureInfo.GetCultureInfo("en-US"),out string supp));
			def.Supp=supp;
			Defs.Update(def);
			Assert.AreEqual("12.50",GetSuppFromDb(def.DefNum));
			Defs.RefreshCache();
			Assert.AreEqual(12.5m,Adjustments.GetDefaultPercent(Defs.GetDef(DefCat.AdjTypes,def.DefNum)));
			Assert.IsTrue(Adjustments.TryGetSuppFromPercentInput("",System.Globalization.CultureInfo.GetCultureInfo("en-US"),out supp));
			def.Supp=supp;
			Defs.Update(def);
			Assert.AreEqual("",GetSuppFromDb(def.DefNum));
			Defs.RefreshCache();
			Assert.IsNull(Adjustments.GetDefaultPercent(Defs.GetDef(DefCat.AdjTypes,def.DefNum)));
		}

		[TestMethod]
		public void PercentOfFeeAdjustment_StoresOnlyTheDollarAmount() {
			Def defDiscount=InsertAdjType("Synthetic senior discount","-","15.00",0);
			Defs.RefreshCache();
			Procedure procedure=new Procedure { PatNum=1001,ProcDate=new DateTime(2026,10,1),ProcFee=240,UnitQty=1,ProcStatus=ProcStat.C,CodeNum=1,ProvNum=1 };
			OpenDentBusiness.Crud.ProcedureCrud.Insert(procedure);
			Def defAdjType=Defs.GetDef(DefCat.AdjTypes,defDiscount.DefNum);
			decimal percent=Adjustments.GetDefaultPercent(defAdjType).Value;
			Adjustment adjustment=new Adjustment {
				PatNum=procedure.PatNum,
				ProcNum=procedure.ProcNum,
				ProcDate=procedure.ProcDate,
				AdjDate=new DateTime(2026,10,2),
				AdjType=defAdjType.DefNum,
				ProvNum=procedure.ProvNum,
				AdjAmt=Adjustments.GetPercentOfFeeAdjAmt(procedure,percent,defAdjType),
			};
			Adjustments.Insert(adjustment);
			Adjustment adjustmentDb=Adjustments.GetOne(adjustment.AdjNum);
			Assert.AreEqual(-36.0,adjustmentDb.AdjAmt);
			Assert.AreEqual(procedure.ProcNum,adjustmentDb.ProcNum);
			//Nothing about the percentage is stored with the adjustment: its row has the same columns as before 26.1.
			List<string> listColumns=DataCore.GetTable("SHOW COLUMNS FROM adjustment").Select().Select(x => x[0].ToString()).ToList();
			Assert.IsFalse(listColumns.Any(x => x.IndexOf("percent",StringComparison.OrdinalIgnoreCase)>=0 || x.IndexOf("supp",StringComparison.OrdinalIgnoreCase)>=0),
				string.Join(",",listColumns));
		}
	}
}
