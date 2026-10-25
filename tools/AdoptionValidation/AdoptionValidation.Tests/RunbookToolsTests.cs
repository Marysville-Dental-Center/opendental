// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SyntheticData.Infrastructure;

namespace AdoptionValidation.Tests {
	///<summary>The commands the runbook runs from the admin PC (copy, connections, float-check, adopt): their arguments, and the pieces that keep
	///them pointed at the right server and honest about what they see. No server needed.</summary>
	[TestClass]
	public class RunbookToolsTests {
		///<summary>Through the SSH forward the tools use a port other than 3306. DataConnection reads the port only from "host:port", so the business
		///layer must get it that way, or adopt would write to whatever answers on 3306 of the admin PC.</summary>
		[TestMethod]
		public void BusinessLayer_PassesThePortToDataConnection() {
			Assert.AreEqual("127.0.0.1:13306",BusinessLayer.ServerAndPort(new ServerSettings { Port=13306 }));
			Assert.AreEqual("localhost:3306",BusinessLayer.ServerAndPort(new ServerSettings { Server="localhost" }));
			StringAssert.Contains(DataConnectionBase.DataConnection.BuildSimpleConnectionString(DataConnectionBase.DatabaseType.MySql,
				BusinessLayer.ServerAndPort(new ServerSettings { Port=13306 }),"synth_x","root",""),"Port=13306");
			AssertThrows<ArgumentException>(() => BusinessLayer.ServerAndPort(new ServerSettings { Server="::1" }));
			AssertThrows<RefusedException>(() => BusinessLayer.ServerAndPort(new ServerSettings { Server="192.168.10.10" }));
		}

		///<summary>Without PROCESS, information_schema.PROCESSLIST lists only the account's own connections, so "nobody is connected" would always be
		///true.</summary>
		[TestMethod]
		public void ConnectionChecks_NeedThePROCESSPrivilege() {
			Assert.IsTrue(HeadlessAdoption.HasProcessPrivilege(new[] { "SELECT","PROCESS","RELOAD" }));
			Assert.IsTrue(HeadlessAdoption.HasProcessPrivilege(new[] { " process " }));
			Assert.IsFalse(HeadlessAdoption.HasProcessPrivilege(new[] { "SELECT","INSERT","UPDATE","DELETE","CREATE","DROP","SUPER" }));
			Assert.IsFalse(HeadlessAdoption.HasProcessPrivilege(new List<string>()));
		}

		[TestMethod]
		public void FloatCheck_ComparesEachValueWithItselfWrittenAsText() {
			Assert.AreEqual("IFNULL(SUM(`FontSize`<>CAST(CAST(`FontSize` AS CHAR) AS FLOAT)),0)",FloatCheck.RoundedExpression("FontSize","float"));
			Assert.AreEqual("IFNULL(SUM(`ClaimFee`<>CAST(CAST(`ClaimFee` AS CHAR) AS DOUBLE)),0)",FloatCheck.RoundedExpression("ClaimFee","double"));
		}

		[TestMethod]
		public void NewCommands_RefuseMissingOrBadArguments() {
			List<string> listLog=new List<string>();
			AssertThrows<ArgumentException>(() => Program.Run(new[] { "copy","--database","synth_x_pms" },listLog.Add));
			AssertThrows<ArgumentException>(() => Program.Run(new[] { "copy","--original","opendental" },listLog.Add));
			AssertThrows<RefusedException>(() => Program.Run(new[] { "copy","--original","opendental","--database","bad name" },listLog.Add));
			AssertThrows<RefusedException>(() => Program.Run(new[] { "copy","--original","open;dental","--database","synth_x" },listLog.Add));
			AssertThrows<ArgumentException>(() => Program.Run(new[] { "connections" },listLog.Add));
			AssertThrows<ArgumentException>(() => Program.Run(new[] { "float-check" },listLog.Add));
			AssertThrows<ArgumentException>(() => Program.Run(new[] { "rehearse","--copy","sideways" },listLog.Add));
			AssertThrows<ArgumentException>(() => Program.Run(new[] { "rehearse","--prefix","opendental_" },listLog.Add));
			AssertThrows<ArgumentException>(() => Program.Run(new[] { "rehearse","--prefix","synth_rv" },listLog.Add));
			AssertThrows<RefusedException>(() => Program.Run(new[] { "connections","--database","opendental","--server","192.168.10.10" },listLog.Add));
			AssertThrows<RefusedException>(() => Program.Run(new[] { "float-check","--database","opendental","--server","fox-db" },listLog.Add));
			StringAssert.Contains(Program.Usage,"copy --original ORIGINAL --database NEW_DB");
			StringAssert.Contains(Program.Usage,"connections --database DB");
			StringAssert.Contains(Program.Usage,"float-check --database DB");
		}

		///<summary>MSTest v1 has no Assert.ThrowsException.</summary>
		private static T AssertThrows<T>(Func<object> func) where T:Exception {
			try {
				object result=func();
				Assert.Fail("Expected "+typeof(T).Name+"; the call returned "+result+".");
			}
			catch(T ex) {
				return ex;
			}
			return null;
		}
	}
}
