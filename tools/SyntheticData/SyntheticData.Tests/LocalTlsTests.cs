// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using SyntheticData.Infrastructure;

namespace SyntheticData.Tests {
	///<summary>LocalTls (O-12, docs/port-notes/DB-TLS.md): the TLS options of the tool's connections, without a server.</summary>
	[TestClass]
	public class LocalTlsTests {
		private string _folder;
		private string _ca;

		[TestInitialize]
		public void SetUp() {
			_folder=Path.Combine(Path.GetTempPath(),"synth_localtls_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_folder);
			_ca=Path.Combine(_folder,"ca.pem");
			File.WriteAllText(_ca,"-----BEGIN CERTIFICATE-----\nnot read by these tests\n-----END CERTIFICATE-----\n");
		}

		[TestCleanup]
		public void CleanUp() {
			Directory.Delete(_folder,true);
		}

		private static MySqlConnectionStringBuilder Build(string host,int port,string caPath,bool isTlsRequired,string relayPorts) {
			MySqlConnectionStringBuilder builder=new MySqlConnectionStringBuilder { Server=host,Port=(uint)port,SslMode=MySqlSslMode.Preferred };
			LocalTls.Apply(builder,host,port,caPath,isTlsRequired,relayPorts);
			return builder;
		}

		[TestMethod]
		public void TheLocalTestServerWithTheCa_VerifyCa() {
			foreach(string host in new[] { "127.0.0.1","localhost","::1" }) {
				MySqlConnectionStringBuilder builder=Build(host,3306,_ca,false,"");
				Assert.AreEqual(MySqlSslMode.VerifyCA,builder.SslMode,host);
				Assert.AreEqual(_ca,builder.SslCa,host);
			}
			Assert.AreEqual(MySqlSslMode.VerifyCA,Build("127.0.0.1",13306,_ca,true,"13306").SslMode,"A test's relay to it.");
		}

		[TestMethod]
		public void WithoutTheCa_SslModeNone_AsBefore_UnlessRequired() {
			Assert.AreEqual(MySqlSslMode.None,Build("127.0.0.1",3306,"",false,"").SslMode);
			TestUtil.AssertThrows<RefusedException>(() => Build("127.0.0.1",3306,"",true,""));
		}

		[TestMethod]
		public void AnSshForwardsPort_SslModeNone_AsBefore_TheTestCaIsNeverOffered() {
			MySqlConnectionStringBuilder builder=Build("127.0.0.1",13306,_ca,true,"");
			Assert.AreEqual(MySqlSslMode.None,builder.SslMode);
			Assert.AreEqual("",builder.SslCa);
		}

		[TestMethod]
		public void FindCaPath_AndParseRequireTls_AsDevDbTls() {
			Assert.AreEqual(_ca,LocalTls.FindCaPath(_ca,Path.Combine(_folder,"none.pem")));
			Assert.AreEqual(_ca,LocalTls.FindCaPath("",_ca));
			Assert.AreEqual("",LocalTls.FindCaPath(null,Path.Combine(_folder,"none.pem")));
			TestUtil.AssertThrows<RefusedException>(() => LocalTls.FindCaPath(Path.Combine(_folder,"none.pem"),_ca));
			Assert.IsTrue(LocalTls.ParseRequireTls(" 1 "));
			Assert.IsFalse(LocalTls.ParseRequireTls(null));
			Assert.IsFalse(LocalTls.ParseRequireTls("no"));
			TestUtil.AssertThrows<ArgumentException>(() => LocalTls.ParseRequireTls("maybe"));
			Assert.AreEqual(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenPmsDental","DevDbTls","ca.pem"),
				LocalTls.GetDefaultCaPath());
		}
	}
}
