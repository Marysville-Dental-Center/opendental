// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdoptionValidation.Tests {
	///<summary>The restore's check that a backup file only writes to the database it is loaded into (runbook steps 0.6 and 3.2).</summary>
	[TestClass]
	public class DumpCheckTests {
		///<summary>What a --databases or --all-databases dump, or a hand-made one, writes outside the target database.</summary>
		[TestMethod]
		public void CheckLine_FlagsEveryWayADumpNamesADatabase() {
			string[] arrayLines={
				"USE `opendental`;",
				"use opendental;",
				"CREATE DATABASE /*!32312 IF NOT EXISTS*/ `opendental` /*!40100 DEFAULT CHARACTER SET utf8mb3 */;",
				"/*!40000 DROP DATABASE IF EXISTS `opendental`*/;",
				"ALTER DATABASE `opendental` CHARACTER SET latin1 COLLATE latin1_swedish_ci ;",
				"CREATE SCHEMA opendental;",
				"INSERT INTO `opendental`.`patient` VALUES (1,'x');",
				"insert ignore into opendental.patient values (1);",
				"REPLACE INTO `opendental`.`preference` VALUES (1);",
				"DROP TABLE IF EXISTS `opendental`.`patient`;",
				"CREATE TABLE IF NOT EXISTS opendental.patient (",
				"LOCK TABLES `opendental`.`patient` WRITE;",
				"/*!40000 ALTER TABLE `opendental`.`patient` DISABLE KEYS */;",
				"TRUNCATE TABLE `opendental`.`patient`;",
				"DELETE FROM opendental.patient;",
				"UPDATE `opendental` . `patient` SET LName='';",
				"  USE `opendental`;",
			};
			foreach(string line in arrayLines) {
				Assert.IsNotNull(DumpCheck.CheckLine(line),line);
			}
			StringAssert.Contains(DumpCheck.CheckLine("USE `opendental`;"),"`opendental`");
			StringAssert.Contains(DumpCheck.CheckLine("INSERT INTO `opendental`.`patient` VALUES (1,'Smith');"),"`opendental`");
			Assert.IsFalse(DumpCheck.CheckLine("INSERT INTO `opendental`.`patient` VALUES (1,'Smith');").Contains("Smith"),"Values are never shown.");
		}

		///<summary>What a single-database mariadb-dump (runbook step 3.1) writes: none of it names a database.</summary>
		[TestMethod]
		public void CheckLine_AcceptsASingleDatabaseDump() {
			string[] arrayLines={
				"/*M!999999\\- enable the sandbox mode */ ",
				"-- MariaDB dump 10.19-10.11.19-MariaDB, for Win64 (AMD64)",
				"-- Host: 127.0.0.1    Database: opendental",
				"/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;",
				"/*!40101 SET NAMES utf8mb4 */;",
				"DROP TABLE IF EXISTS `patient`;",
				"/*!40101 SET @saved_cs_client     = @@character_set_client */;",
				"CREATE TABLE `patient` (",
				"  `PatNum` bigint(20) NOT NULL AUTO_INCREMENT,",
				"  `Email` varchar(255) NOT NULL DEFAULT '',",
				") ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;",
				"LOCK TABLES `patient` WRITE;",
				"/*!40000 ALTER TABLE `patient` DISABLE KEYS */;",
				"INSERT INTO `patient` VALUES (1,'USE opendental; CREATE DATABASE x','a.b@example.com'),(2,'opendental.patient','');",
				"INSERT INTO `userod` VALUES (1,'Admin');",
				"INSERT INTO `user_x` VALUES (1);",
				"UNLOCK TABLES;",
				"-- Dumping routines for database 'opendental'",
				"UPDATE `patient` SET LName='' WHERE PatNum=1;",
				"/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;",
				"",
				"USER_DEFINED_NOT_A_STATEMENT",
			};
			foreach(string line in arrayLines) {
				Assert.IsNull(DumpCheck.CheckLine(line),line);
			}
		}

		[TestMethod]
		public void Scan_FindsTheLine_PastLongLines_AndWithoutAFinalNewline() {
			string path=Path.Combine(Path.GetTempPath(),"DumpCheckTests_"+Guid.NewGuid().ToString("N")+".sql");
			try {
				using(StreamWriter writer=new StreamWriter(path,false,new UTF8Encoding(false))) {
					writer.Write("-- MariaDB dump\r\n");
					//A 3 MB extended INSERT whose values contain the words the check looks for: only the start of a line counts.
					writer.Write("INSERT INTO `patient` VALUES ");
					for(int i=0;i<100000;i++) {
						writer.Write("("+i+",'USE `opendental`; DROP DATABASE x'),");
					}
					writer.Write("(0,'');\r\n");
					writer.Write("USE `opendental`;\r\n");
					writer.Write("INSERT INTO `opendental`.`patient` VALUES (1);");
				}
				List<DumpCheck.Problem> listProblems=DumpCheck.Scan(path);
				CollectionAssert.AreEqual(new long[] { 3,4 },listProblems.Select(x => x.Line).ToArray(),string.Join("; ",listProblems));
			}
			finally {
				File.Delete(path);
			}
		}

		[TestMethod]
		public void Scan_StopsAtMaxProblems_AndPassesACleanFile() {
			string path=Path.Combine(Path.GetTempPath(),"DumpCheckTests_"+Guid.NewGuid().ToString("N")+".sql");
			try {
				File.WriteAllText(path,string.Concat(Enumerable.Range(0,50).Select(x => "USE `db"+x+"`;\n")),new UTF8Encoding(false));
				Assert.AreEqual(20,DumpCheck.Scan(path).Count);
				Assert.AreEqual(5,DumpCheck.Scan(path,5).Count);
				File.WriteAllText(path,"DROP TABLE IF EXISTS `patient`;\nCREATE TABLE `patient` (\n  `PatNum` bigint(20)\n);\nINSERT INTO `patient` VALUES (1);\n",
					new UTF8Encoding(false));
				Assert.AreEqual(0,DumpCheck.Scan(path).Count);
				File.WriteAllText(path,"",new UTF8Encoding(false));
				Assert.AreEqual(0,DumpCheck.Scan(path).Count);
			}
			finally {
				File.Delete(path);
			}
		}
	}
}
