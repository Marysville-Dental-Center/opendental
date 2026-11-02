// The business layer of the remembered SSH host keys (SshHostKeys and its Store) on a scratch database: first use, refusals, accepting and
// removing keys with their audit entries, the Security Admin checks, and the check with the database store end to end.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalStorage;
using Renci.SshNet.Common;

namespace SshHostKeyTests {
	[TestClass]
	public class StoreDbTests {
		[TestInitialize]
		public void TestInitialize() {
			TestDb.ResetOrSkip();
		}

		///<summary>The audit entries under SSH Host Keys, oldest first: UserNum, FKey and LogText.</summary>
		private static List<DataRow> GetLogs() {
			return DataCore.GetTable("SELECT UserNum,FKey,LogText FROM securitylog WHERE PermType="+(int)EnumPermType.SshHostKeys+" ORDER BY SecurityLogNum")
				.Rows.Cast<DataRow>().ToList();
		}

		private static SshHostKeyFingerprint NewFingerprint() {
			return SshHostKeyFingerprint.FromHostKeyEvent(TestKeys.NewEd25519Event());
		}

		[TestMethod]
		public void FirstUse_StoresTheKey_WithUserTimeAndAnAuditEntry() {
			SshHostKeyFingerprint fingerprintA=NewFingerprint();
			DateTime dateTimeBefore=MiscData.GetNowDateTime().AddSeconds(-1);
			SshHostKeyFingerprint fingerprintStored=SshHostKeys.Store.StoreFirstKey("sftp.example.test",22,fingerprintA);
			Assert.IsTrue(fingerprintA.Matches(fingerprintStored));
			List<SshHostKey> listSshHostKeys=SshHostKeys.GetAll();
			Assert.AreEqual(1,listSshHostKeys.Count);
			SshHostKey sshHostKey=listSshHostKeys[0];
			Assert.AreEqual("sftp.example.test",sshHostKey.Host);
			Assert.AreEqual(22,sshHostKey.Port);
			Assert.AreEqual("ssh-ed25519",sshHostKey.KeyType);
			Assert.AreEqual(fingerprintA.Sha256,sshHostKey.FingerprintSha256);
			Assert.AreEqual(TestDb.USER_ADMIN,sshHostKey.UserNum);
			Assert.IsTrue(sshHostKey.DateTStored>=dateTimeBefore && sshHostKey.DateTStored<=MiscData.GetNowDateTime().AddSeconds(1),
				sshHostKey.DateTStored.ToString());
			Assert.IsFalse(sshHostKey.IsAccepted);
			Assert.IsFalse(sshHostKey.HasRefusedKey());
			Assert.AreEqual("",sshHostKey.RefusedKeyType);
			Assert.AreEqual(DateTime.MinValue,sshHostKey.DateTRefused);
			List<DataRow> listLogs=GetLogs();
			Assert.AreEqual(1,listLogs.Count);
			Assert.AreEqual(TestDb.USER_ADMIN,PIn.Long(listLogs[0]["UserNum"].ToString()));
			Assert.AreEqual(sshHostKey.SshHostKeyNum,PIn.Long(listLogs[0]["FKey"].ToString()));
			Assert.AreEqual("SSH host key stored on first use: sftp.example.test:22, ssh-ed25519 SHA256:"+fingerprintA.Sha256,
				listLogs[0]["LogText"].ToString());
			Assert.IsTrue(fingerprintA.Matches(SshHostKeys.Store.GetStoredKey("sftp.example.test",22)));
			Assert.IsNull(SshHostKeys.Store.GetStoredKey("sftp.example.test",2222),"Another port is another server.");
		}

		[TestMethod]
		public void FirstUse_OfAServerThatHasAKey_ChangesNothing_AndReturnsTheStoredKey() {
			SshHostKeyFingerprint fingerprintA=NewFingerprint();
			SshHostKeyFingerprint fingerprintB=NewFingerprint();
			SshHostKeys.Store.StoreFirstKey("sftp.example.test",22,fingerprintA);
			//Another workstation's connection stores a different key "at the same moment": it gets the stored key back, so its check refuses.
			SshHostKeyFingerprint fingerprintReturned=SshHostKeys.Store.StoreFirstKey(" SFTP.Example.Test ",22,fingerprintB);
			Assert.IsTrue(fingerprintA.Matches(fingerprintReturned));
			Assert.AreEqual(1,SshHostKeys.GetAll().Count);
			Assert.AreEqual(1,GetLogs().Count);
		}

		[TestMethod]
		public void UniqueKey_LetsOneRowPerServerIn_HostWithoutCase() {
			SshHostKeys.InsertFirstUse("sftp.example.test",22,"ssh-ed25519","AAAA");
			string error=null;
			try {
				DataCore.NonQ("INSERT INTO sshhostkey (Host,Port,KeyType,FingerprintSha256,UserNum,DateTStored,IsAccepted,RefusedKeyType,"
					+"RefusedFingerprintSha256,DateTRefused) VALUES ('SFTP.EXAMPLE.TEST',22,'ssh-rsa','BBBB',1,NOW(),0,'','','0001-01-01')");
			}
			catch(Exception ex) {
				error=ex.GetBaseException().Message;
			}
			Assert.IsNotNull(error,"A second row for the same server (host in other case) is refused by the unique key.");
			StringAssert.Contains(error,"Duplicate");
			Assert.AreEqual(1,SshHostKeys.GetAll().Count);
			Assert.IsNotNull(SshHostKeys.GetForServer("SFTP.Example.Test",22));
			Assert.IsNull(SshHostKeys.GetForServer("sftp.example.test",23));
			TestAssert.Throws<ApplicationException>(() => SshHostKeys.InsertFirstUse("  ",22,"ssh-ed25519","AAAA"),"A key needs a host.");
			TestAssert.Throws<ApplicationException>(() => SshHostKeys.InsertFirstUse("sftp2.example.test",22,"ssh-ed25519",""),
				"A key needs a fingerprint.");
		}

		[TestMethod]
		public void RefusedKey_IsRecordedOnTheRow_WithOneAuditEntryPerRefusedKey() {
			SshHostKeyFingerprint fingerprintA=NewFingerprint();
			SshHostKeyFingerprint fingerprintB=NewFingerprint();
			SshHostKeyFingerprint fingerprintC=NewFingerprint();
			SshHostKeys.Store.StoreFirstKey("sftp.example.test",22,fingerprintA);
			SshHostKeys.Store.RecordRefusedKey("sftp.example.test",22,fingerprintA,fingerprintB);
			SshHostKey sshHostKey=SshHostKeys.GetForServer("sftp.example.test",22);
			Assert.IsTrue(sshHostKey.HasRefusedKey());
			Assert.AreEqual("ssh-ed25519",sshHostKey.RefusedKeyType);
			Assert.AreEqual(fingerprintB.Sha256,sshHostKey.RefusedFingerprintSha256);
			Assert.IsTrue(sshHostKey.DateTRefused.Year>2000);
			Assert.AreEqual(fingerprintA.Sha256,sshHostKey.FingerprintSha256,"The stored key stays.");
			List<DataRow> listLogs=GetLogs();
			Assert.AreEqual(2,listLogs.Count);
			Assert.AreEqual("SSH host key refused: sftp.example.test:22 presented ssh-ed25519 SHA256:"+fingerprintB.Sha256
				+", the stored key is ssh-ed25519 SHA256:"+fingerprintA.Sha256,listLogs[1]["LogText"].ToString());
			Assert.AreEqual(sshHostKey.SshHostKeyNum,PIn.Long(listLogs[1]["FKey"].ToString()));
			//The same refused key again (the HL7 service retries every minute): the row's time only, no new entry.
			SshHostKeys.Store.RecordRefusedKey("sftp.example.test",22,fingerprintA,fingerprintB);
			SshHostKeys.Store.RecordRefusedKey("SFTP.example.test",22,fingerprintA,fingerprintB);
			Assert.AreEqual(2,GetLogs().Count);
			//Another refused key: recorded, with an entry.
			SshHostKeys.Store.RecordRefusedKey("sftp.example.test",22,fingerprintA,fingerprintC);
			Assert.AreEqual(fingerprintC.Sha256,SshHostKeys.GetForServer("sftp.example.test",22).RefusedFingerprintSha256);
			Assert.AreEqual(3,GetLogs().Count);
			//A server without a row, and the stored key itself, record nothing.
			SshHostKeys.RecordRefused("other.example.test",22,"ssh-ed25519",fingerprintB.Sha256);
			SshHostKeys.RecordRefused("sftp.example.test",22,"ssh-ed25519",fingerprintA.Sha256);
			Assert.AreEqual(1,SshHostKeys.GetAll().Count);
			Assert.AreEqual(fingerprintC.Sha256,SshHostKeys.GetForServer("sftp.example.test",22).RefusedFingerprintSha256);
			Assert.AreEqual(3,GetLogs().Count);
		}

		[TestMethod]
		public void AcceptRefusedKey_ReplacesTheStoredKey_WithAnAuditEntry() {
			SshHostKeyFingerprint fingerprintA=NewFingerprint();
			SshHostKeyFingerprint fingerprintB=NewFingerprint();
			SshHostKeys.Store.StoreFirstKey("sftp.example.test",22,fingerprintA);
			SshHostKeys.Store.RecordRefusedKey("sftp.example.test",22,fingerprintA,fingerprintB);
			SshHostKey sshHostKeyShown=SshHostKeys.GetForServer("sftp.example.test",22);
			SshHostKeys.AcceptRefusedKey(sshHostKeyShown.SshHostKeyNum,sshHostKeyShown.RefusedKeyType,sshHostKeyShown.RefusedFingerprintSha256);
			SshHostKey sshHostKey=SshHostKeys.GetOne(sshHostKeyShown.SshHostKeyNum);
			Assert.AreEqual(fingerprintB.Sha256,sshHostKey.FingerprintSha256);
			Assert.AreEqual("ssh-ed25519",sshHostKey.KeyType);
			Assert.IsTrue(sshHostKey.IsAccepted);
			Assert.AreEqual(TestDb.USER_ADMIN,sshHostKey.UserNum);
			Assert.IsFalse(sshHostKey.HasRefusedKey(),"The refused key is now the stored key.");
			Assert.AreEqual("",sshHostKey.RefusedKeyType);
			Assert.AreEqual(DateTime.MinValue,sshHostKey.DateTRefused);
			Assert.IsTrue(fingerprintB.Matches(SshHostKeys.Store.GetStoredKey("sftp.example.test",22)));
			List<DataRow> listLogs=GetLogs();
			Assert.AreEqual(3,listLogs.Count);
			Assert.AreEqual("SSH host key accepted: sftp.example.test:22, ssh-ed25519 SHA256:"+fingerprintA.Sha256+" (old) replaced by ssh-ed25519 SHA256:"
				+fingerprintB.Sha256+" (new)",listLogs[2]["LogText"].ToString());
			Assert.AreEqual(TestDb.USER_ADMIN,PIn.Long(listLogs[2]["UserNum"].ToString()));
			Assert.AreEqual(sshHostKey.SshHostKeyNum,PIn.Long(listLogs[2]["FKey"].ToString()));
		}

		[TestMethod]
		public void AcceptRefusedKey_OnlyTheKeyTheWindowShowed() {
			SshHostKeyFingerprint fingerprintA=NewFingerprint();
			SshHostKeyFingerprint fingerprintB=NewFingerprint();
			SshHostKeyFingerprint fingerprintC=NewFingerprint();
			SshHostKeys.Store.StoreFirstKey("sftp.example.test",22,fingerprintA);
			SshHostKey sshHostKeyNoRefusal=SshHostKeys.GetForServer("sftp.example.test",22);
			ApplicationException exNone=TestAssert.Throws<ApplicationException>(
				() => SshHostKeys.AcceptRefusedKey(sshHostKeyNoRefusal.SshHostKeyNum,"ssh-ed25519",fingerprintB.Sha256));
			StringAssert.Contains(exNone.Message,"no key to accept");
			SshHostKeys.Store.RecordRefusedKey("sftp.example.test",22,fingerprintA,fingerprintB);
			SshHostKey sshHostKeyShown=SshHostKeys.GetForServer("sftp.example.test",22);
			//Another connection refuses a third key before the administrator clicks Accept.
			SshHostKeys.Store.RecordRefusedKey("sftp.example.test",22,fingerprintA,fingerprintC);
			int countLogs=GetLogs().Count;
			ApplicationException ex=TestAssert.Throws<ApplicationException>(
				() => SshHostKeys.AcceptRefusedKey(sshHostKeyShown.SshHostKeyNum,sshHostKeyShown.RefusedKeyType,sshHostKeyShown.RefusedFingerprintSha256));
			StringAssert.Contains(ex.Message,"another key");
			Assert.AreEqual(fingerprintA.Sha256,SshHostKeys.GetForServer("sftp.example.test",22).FingerprintSha256,"Nothing changed.");
			Assert.AreEqual(countLogs,GetLogs().Count);
			TestAssert.Throws<ApplicationException>(() => SshHostKeys.AcceptRefusedKey(999999,"ssh-ed25519",fingerprintC.Sha256),"A row that is gone.");
		}

		[TestMethod]
		public void Delete_RemovesTheKey_WithAnAuditEntry_AndTheNextConnectionStoresItsKey() {
			SshHostKeyFingerprint fingerprintA=NewFingerprint();
			SshHostKeys.Store.StoreFirstKey("sftp.example.test",22,fingerprintA);
			SshHostKey sshHostKey=SshHostKeys.GetForServer("sftp.example.test",22);
			SshHostKeys.Delete(sshHostKey.SshHostKeyNum);
			Assert.IsNull(SshHostKeys.GetForServer("sftp.example.test",22));
			List<DataRow> listLogs=GetLogs();
			Assert.AreEqual(2,listLogs.Count);
			Assert.AreEqual("SSH host key removed: sftp.example.test:22, ssh-ed25519 SHA256:"+fingerprintA.Sha256
				+". The next connection stores the key the server presents.",listLogs[1]["LogText"].ToString());
			Assert.AreEqual(sshHostKey.SshHostKeyNum,PIn.Long(listLogs[1]["FKey"].ToString()));
			TestAssert.Throws<ApplicationException>(() => SshHostKeys.Delete(sshHostKey.SshHostKeyNum),"Already gone.");
			//The next connection stores the key it gets, whatever it is.
			HostKeyEventArgs keyNext=TestKeys.NewEd25519Event();
			new SimulatedConnection("sftp.example.test",22,SshHostKeys.Store,keyNext).Check.Connect();
			Assert.AreEqual(1,SshHostKeys.GetAll().Count);
			Assert.AreEqual(keyNext.FingerPrintSHA256,SshHostKeys.GetForServer("sftp.example.test",22).FingerprintSha256);
		}

		[TestMethod]
		public void AcceptAndDelete_NeedSecurityAdmin_InTheBusinessLayer() {
			SshHostKeyFingerprint fingerprintA=NewFingerprint();
			SshHostKeyFingerprint fingerprintB=NewFingerprint();
			SshHostKeys.Store.StoreFirstKey("sftp.example.test",22,fingerprintA);
			SshHostKeys.Store.RecordRefusedKey("sftp.example.test",22,fingerprintA,fingerprintB);
			SshHostKey sshHostKey=SshHostKeys.GetForServer("sftp.example.test",22);
			int countLogs=GetLogs().Count;
			TestDb.LogOn(TestDb.USER_FRONT);
			ApplicationException exAccept=TestAssert.Throws<ApplicationException>(
				() => SshHostKeys.AcceptRefusedKey(sshHostKey.SshHostKeyNum,sshHostKey.RefusedKeyType,sshHostKey.RefusedFingerprintSha256));
			StringAssert.Contains(exAccept.Message,"Security Admin");
			ApplicationException exDelete=TestAssert.Throws<ApplicationException>(() => SshHostKeys.Delete(sshHostKey.SshHostKeyNum));
			StringAssert.Contains(exDelete.Message,"Security Admin");
			SshHostKey sshHostKeyAfter=SshHostKeys.GetOne(sshHostKey.SshHostKeyNum);
			Assert.IsNotNull(sshHostKeyAfter,"Not removed.");
			Assert.AreEqual(fingerprintA.Sha256,sshHostKeyAfter.FingerprintSha256,"Not accepted.");
			Assert.AreEqual(countLogs,GetLogs().Count,"Nothing logged.");
			//No user at all: refused too.
			Security.CurUser=null;
			TestAssert.Throws<ApplicationException>(() => SshHostKeys.Delete(sshHostKey.SshHostKeyNum));
			//The front desk user's connections still store and record keys: that needs no permission.
			TestDb.LogOn(TestDb.USER_FRONT);
			SshHostKeyFingerprint fingerprintOther=NewFingerprint();
			SshHostKeys.Store.StoreFirstKey("other.example.test",22,fingerprintOther);
			Assert.AreEqual(TestDb.USER_FRONT,SshHostKeys.GetForServer("other.example.test",22).UserNum);
			//The administrator can.
			TestDb.LogOn(TestDb.USER_ADMIN);
			SshHostKeys.AcceptRefusedKey(sshHostKey.SshHostKeyNum,sshHostKey.RefusedKeyType,sshHostKey.RefusedFingerprintSha256);
			SshHostKeys.Delete(sshHostKey.SshHostKeyNum);
			Assert.IsNull(SshHostKeys.GetOne(sshHostKey.SshHostKeyNum));
		}

		[TestMethod]
		public void Service_WithoutAUserRow_StoresTheKeyAsUser0() {
			//The HL7 service logs on as a user object without a row (ServiceHL7: UserName ODServiceSecUser, UserNum 0).
			Security.CurUser=new Userod() { UserName="ODServiceSecUser" };
			SshHostKeyFingerprint fingerprintA=NewFingerprint();
			SshHostKeys.Store.StoreFirstKey("hl7.example.test",22,fingerprintA);
			Assert.AreEqual(0,SshHostKeys.GetForServer("hl7.example.test",22).UserNum);
			Assert.AreEqual(1,GetLogs().Count);
		}

		///<summary>A clinic's Transworld program link properties, pointing at the fake SSH server.</summary>
		private static List<ProgramProperty> GetTsiProperties(int port) {
			return new List<ProgramProperty>() {
				new ProgramProperty() { PropertyDesc="SftpServerAddress",PropertyValue="127.0.0.1" },
				new ProgramProperty() { PropertyDesc="SftpServerPort",PropertyValue=port.ToString() },
				new ProgramProperty() { PropertyDesc="SftpUsername",PropertyValue="user" },
				new ProgramProperty() { PropertyDesc="SftpPassword",PropertyValue="synthetic-password" },
				new ProgramProperty() { PropertyDesc="SelectedServices",PropertyValue="0" },
			};
		}

		[TestMethod]
		public void TsiConnectionTest_WithTheDatabaseStore_ReturnsTheRefusal() {
			using(FakeSshServer fakeSshServer=new FakeSshServer()) {
				List<ProgramProperty> listProgramProperties=GetTsiProperties(fakeSshServer.Port);
				//No stored key: the key is accepted, the fake server then closes, so the test fails as a plain connection failure, with no refusal.
				Assert.IsFalse(TsiTransLogs.ValidateClinicSftpDetails(listProgramProperties,out string refusalMessageNone));
				Assert.IsNull(refusalMessageNone);
				Assert.AreEqual(0,SshHostKeys.GetAll().Count,"Only a connection that signed in stores the key.");
				//Another key is stored for the server: the test is refused for its host key, and says so.
				SshHostKeyFingerprint fingerprintStored=NewFingerprint();
				SshHostKeys.Store.StoreFirstKey("127.0.0.1",fakeSshServer.Port,fingerprintStored);
				Assert.IsFalse(TsiTransLogs.ValidateClinicSftpDetails(listProgramProperties,out string refusalMessage));
				Assert.IsNotNull(refusalMessage);
				StringAssert.Contains(refusalMessage,"127.0.0.1:"+fakeSshServer.Port);
				StringAssert.Contains(refusalMessage,"Stored key: ssh-ed25519 SHA256:"+fingerprintStored.Sha256);
				StringAssert.Contains(refusalMessage,"The server now presents: ssh-ed25519 SHA256:"+fakeSshServer.FingerprintSha256);
				StringAssert.Contains(refusalMessage,"Setup, SSH Host Keys");
				SshHostKey sshHostKey=SshHostKeys.GetForServer("127.0.0.1",fakeSshServer.Port);
				Assert.AreEqual(fakeSshServer.FingerprintSha256,sshHostKey.RefusedFingerprintSha256,"The refused key is recorded for the window.");
				Assert.AreEqual(fingerprintStored.Sha256,sshHostKey.FingerprintSha256,"The stored key stays.");
				//The overload without the message still answers only valid or not.
				Assert.IsFalse(TsiTransLogs.ValidateClinicSftpDetails(listProgramProperties));
				//Without the connection test nothing connects.
				Assert.IsTrue(TsiTransLogs.ValidateClinicSftpDetails(listProgramProperties,false));
				Assert.AreEqual(3,fakeSshServer.WaitForConnections(3).Count);
				Assert.AreEqual(2,GetLogs().Count,"Stored on first use, then refused once (the second refusal of the same key adds no entry).");
			}
		}

		[TestMethod]
		public void Check_WithTheDatabaseStore_FirstUse_SameKey_ChangedKeyRefused_Accepted() {
			HostKeyEventArgs keyA=TestKeys.NewEd25519Event();
			HostKeyEventArgs keyB=TestKeys.NewEd25519Event();
			//First use stores A.
			new SimulatedConnection("sftp.example.test",22,SshHostKeys.Store,keyA).Check.Connect();
			Assert.AreEqual(keyA.FingerPrintSHA256,SshHostKeys.GetForServer("sftp.example.test",22).FingerprintSha256);
			//A again connects.
			new SimulatedConnection("SFTP.example.test",22,SshHostKeys.Store,keyA).Check.Connect();
			//B is refused before the password, and recorded on the row.
			SimulatedConnection connectionB=new SimulatedConnection("sftp.example.test",22,SshHostKeys.Store,keyB);
			SshHostKeyRefusedException ex=TestAssert.Throws<SshHostKeyRefusedException>(() => connectionB.Check.Connect());
			Assert.IsFalse(connectionB.WasPasswordSent);
			StringAssert.Contains(ex.Message,"sftp.example.test:22");
			StringAssert.Contains(ex.Message,keyA.FingerPrintSHA256);
			StringAssert.Contains(ex.Message,keyB.FingerPrintSHA256);
			SshHostKey sshHostKey=SshHostKeys.GetForServer("sftp.example.test",22);
			Assert.AreEqual(keyB.FingerPrintSHA256,sshHostKey.RefusedFingerprintSha256);
			//The administrator accepts B: B connects, A is refused.
			SshHostKeys.AcceptRefusedKey(sshHostKey.SshHostKeyNum,sshHostKey.RefusedKeyType,sshHostKey.RefusedFingerprintSha256);
			new SimulatedConnection("sftp.example.test",22,SshHostKeys.Store,keyB).Check.Connect();
			TestAssert.Throws<SshHostKeyRefusedException>(() => new SimulatedConnection("sftp.example.test",22,SshHostKeys.Store,keyA).Check.Connect());
			List<string> listTexts=GetLogs().Select(x => x["LogText"].ToString()).ToList();
			Assert.AreEqual(4,listTexts.Count,string.Join("\r\n",listTexts));
			StringAssert.StartsWith(listTexts[0],"SSH host key stored on first use: sftp.example.test:22");
			StringAssert.StartsWith(listTexts[1],"SSH host key refused: sftp.example.test:22");
			StringAssert.StartsWith(listTexts[2],"SSH host key accepted: sftp.example.test:22");
			StringAssert.StartsWith(listTexts[3],"SSH host key refused: sftp.example.test:22 presented ssh-ed25519 SHA256:"+keyA.FingerPrintSHA256);
		}
	}
}
