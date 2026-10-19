// Tests for the QuickBooks Online settings snapshot and the "Name,Id|Name,Id" lists. No database: values are passed in.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.Qbo;
using OpenDentBusiness.VendorDirect;

namespace VendorDirectTests.Qbo {
	[TestClass]
	public class QboSettingsTests {
		private static QboSettings FromDictionary(Dictionary<string,string> dict) {
			return QboSettings.FromValues(desc => dict.TryGetValue(desc,out string value) ? value : null);
		}

		private static Dictionary<string,string> GetConnectedValues() {
			return new Dictionary<string,string> {
				{ QboSettings.PropClientId,"client-1" },
				{ QboSettings.PropClientSecret,SecretsTestHelper.Protect("secret-1") },
				{ QboSettings.PropEnvironment,"Sandbox" },
				{ QboSettings.PropRedirectUri,"http://localhost:8765/callback" },
				{ QboSettings.PropRefreshToken,SecretsTestHelper.Protect("rt-1") },
				{ QboSettings.PropRealmId,SecretsTestHelper.Protect("123") },
				{ QboSettings.PropRefreshTokenExpires,"2027-01-27T12:00:00Z" },
				{ QboSettings.PropCompanyName,"Sandbox Company" },
				{ QboSettings.PropDepositAccounts,"Checking,35" },
			};
		}

		[TestMethod]
		public void NoRows_IsNotConnected_AndProduction() {
			QboSettings settings=FromDictionary(new Dictionary<string,string>());
			Assert.IsFalse(settings.IsConnected);
			Assert.IsFalse(settings.HasUnreadableLegacyValues);
			Assert.AreEqual(QboEnvironment.Production,settings.Environment);
			Assert.AreEqual(QboAuthException.MessageNotConnected,settings.GetNotConnectedMessage());
			Assert.AreEqual("",settings.DepositAccounts);
		}

		[TestMethod]
		public void AllValues_IsConnected() {
			QboSettings settings=FromDictionary(GetConnectedValues());
			Assert.IsTrue(settings.IsConnected);
			Assert.AreEqual(QboEnvironment.Sandbox,settings.Environment);
			Assert.AreEqual("secret-1",settings.ClientSecret);
			Assert.AreEqual("123",settings.RealmId);
			Assert.AreEqual(new DateTime(2027,1,27,12,0,0,DateTimeKind.Utc),settings.RefreshTokenExpiresUtc);
			Assert.AreEqual(DateTimeKind.Utc,settings.RefreshTokenExpiresUtc.Value.Kind);
			Assert.AreEqual("",settings.GetNotConnectedMessage());
		}

		[TestMethod]
		public void MigratedOpenDentalValues_AreUnusable_ButTheListsStay() {
			Dictionary<string,string> dict=GetConnectedValues();
			dict[QboSettings.PropRefreshToken]="odv2e$AAAAencryptedbyopendental";
			dict[QboSettings.PropRealmId]="odv2e$BBBBencryptedbyopendental";
			QboSettings settings=FromDictionary(dict);
			Assert.IsFalse(settings.IsConnected);
			Assert.IsTrue(settings.HasUnreadableLegacyValues);
			Assert.AreEqual(QboAuthException.MessageLegacy,settings.GetNotConnectedMessage());
			Assert.AreEqual("Checking,35",settings.DepositAccounts,"Account Ids stay valid for the same company.");
			Assert.AreEqual(SecretReadStatus.Unreadable,settings.RealmIdStatus);
		}

		[TestMethod]
		public void PlainTextRefreshToken_IsNeverUsed() {
			Dictionary<string,string> dict=GetConnectedValues();
			dict[QboSettings.PropRefreshToken]="AB11plainrefreshtoken";
			QboSettings settings=FromDictionary(dict);
			Assert.IsFalse(settings.HasRefreshToken);
			Assert.IsFalse(settings.IsConnected);
			Assert.IsTrue(settings.HasUnreadableLegacyValues);
		}

		[TestMethod]
		public void CorruptCiphertext_IsUnreadable() {
			Dictionary<string,string> dict=GetConnectedValues();
			string stored=dict[QboSettings.PropClientSecret];
			dict[QboSettings.PropClientSecret]=stored.Substring(0,stored.Length-4)+(stored.EndsWith("AAAA") ? "BBBB" : "AAAA");
			QboSettings settings=FromDictionary(dict);
			Assert.AreEqual("",settings.ClientSecret,"A damaged ciphertext is never sent as the secret.");
			Assert.IsFalse(settings.IsConnected);
		}

		[TestMethod]
		public void Fingerprint_ChangesWithTheSecretOrEnvironment() {
			Dictionary<string,string> dict=GetConnectedValues();
			string fingerprint=FromDictionary(dict).GetFingerprint();
			Assert.AreEqual(fingerprint,FromDictionary(dict).GetFingerprint());
			dict[QboSettings.PropClientSecret]=SecretsTestHelper.Protect("secret-2");
			Assert.AreNotEqual(fingerprint,FromDictionary(dict).GetFingerprint());
			dict=GetConnectedValues();
			dict[QboSettings.PropEnvironment]="Production";
			Assert.AreNotEqual(fingerprint,FromDictionary(dict).GetFingerprint());
			Assert.IsFalse(fingerprint.Contains("secret"),"The fingerprint is a hash.");
		}

		[TestMethod]
		public void Parsers() {
			Assert.AreEqual(QboEnvironment.Sandbox,QboSettings.ParseEnvironment(" sandbox "));
			Assert.AreEqual(QboEnvironment.Production,QboSettings.ParseEnvironment("anything else"));
			Assert.AreEqual(8765,QboSettings.ParsePort("8765"));
			Assert.AreEqual(0,QboSettings.ParsePort("abc"));
			Assert.AreEqual(0,QboSettings.ParsePort("65536"));
			Assert.IsNull(QboSettings.ParseUtc(""));
			DateTime dateTime=new DateTime(2026,10,19,1,2,3,DateTimeKind.Utc);
			Assert.AreEqual(dateTime,QboSettings.ParseUtc(QboSettings.FormatUtc(dateTime)));
		}

		[TestMethod]
		public void EntityLists_RoundTrip() {
			string stored="Checking,35|Savings:Reserve,36";
			List<QuickBooksOnlineAccount> listAccounts=QuickBooksOnlineAccount.StringAccountsToList(stored);
			Assert.AreEqual(2,listAccounts.Count);
			Assert.AreEqual("Savings:Reserve",listAccounts[1].Name);
			Assert.AreEqual("36",listAccounts[1].Id);
			Assert.AreEqual(stored,QuickBooksOnlineAccount.ListAccountsToString(listAccounts));
			Assert.AreEqual(0,QuickBooksOnlineAccount.StringAccountsToList("").Count);
			Assert.AreEqual(0,QuickBooksOnlineAccount.StringAccountsToList(null).Count);
			Assert.AreEqual("",QuickBooksOnlineAccount.ListAccountsToString(new List<QuickBooksOnlineAccount>()));
			List<QuickBooksOnlineClassRef> listClassRefs=QuickBooksOnlineClassRef.StringClassRefsToList("Hygiene,5000");
			Assert.AreEqual("Hygiene,5000",QuickBooksOnlineClassRef.ListClassRefsToString(listClassRefs));
			//The existing lookups read the same format.
			Assert.AreEqual("36",ProgramProperties.GetQuickBooksOnlineEntityIdByName(stored,"Savings:Reserve"));
			Assert.AreEqual("Savings:Reserve",ProgramProperties.GetQuickBooksOnlineEntityNameById(stored,"36"));
			Assert.IsFalse(QboEntityList.IsStorableName("A, B"));
			Assert.IsFalse(QboEntityList.IsStorableName("A|B"));
			Assert.IsTrue(QboEntityList.IsStorableName("Parent:Child"));
		}
	}
}
