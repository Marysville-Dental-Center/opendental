// Tests for the office's own DentalXChange partner identity, which replaced Open Dental's compiled-in Client and DCI service ID. No database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.Eclaims;
using OpenDentBusiness.VendorDirect.DentalXChange;

namespace VendorDirectTests.DentalXChange {
	[TestClass]
	public class DxcPartnerIdentityTests {
		[TestMethod]
		public void IsConfigured_NeedsBothValues() {
			Assert.IsFalse(new DxcPartnerIdentity().IsConfigured,"Blank by default.");
			Assert.IsFalse(new DxcPartnerIdentity("Test Partner","").IsConfigured);
			Assert.IsFalse(new DxcPartnerIdentity("","TEST-SERVICE-0001").IsConfigured);
			Assert.IsFalse(new DxcPartnerIdentity("  ","  ").IsConfigured);
			Assert.IsTrue(new DxcPartnerIdentity("Test Partner","TEST-SERVICE-0001").IsConfigured);
		}

		[TestMethod]
		public void Constructor_TrimsAndNeverHoldsNull() {
			DxcPartnerIdentity identity=new DxcPartnerIdentity(" Test Partner ",null);
			Assert.AreEqual("Test Partner",identity.Client);
			Assert.AreEqual("",identity.ServiceID);
		}

		[TestMethod]
		public void DxcCredentials_Create_RefusesWithoutAnIdentity() {
			Clearinghouse clearinghouse=new Clearinghouse { LoginID="office-login",Password="office-password" };
			ODException ex=AssertEx.Throws<ODException>(() => ClaimConnect.DxcCredentials.Create(new DxcPartnerIdentity(),clearinghouse));
			StringAssert.Contains(ex.Message,"partner");
			AssertEx.Throws<ODException>(() => ClaimConnect.DxcCredentials.Create(null,clearinghouse));
		}

		[TestMethod]
		public void DxcCredentials_Create_SendsTheOfficesIdentityAndLogin() {
			Clearinghouse clearinghouse=new Clearinghouse { LoginID="office-login",Password="office-password" };
			ClaimConnect.DxcCredentials cred=ClaimConnect.DxcCredentials.Create(new DxcPartnerIdentity("Test Partner","TEST-SERVICE-0001"),clearinghouse);
			Assert.AreEqual("Test Partner",cred.Client);
			Assert.AreEqual("TEST-SERVICE-0001",cred.ServiceID);
			Assert.AreEqual("office-login",cred.Username);
			Assert.AreEqual("office-password",cred.Password);
			//Both SOAP services get the same values.
			OpenDentBusiness.Dentalxchange2016.Credentials credDws=cred;
			Assert.AreEqual("Test Partner",credDws.Client);
			Assert.AreEqual("TEST-SERVICE-0001",credDws.ServiceID);
			OpenDentBusiness.DentalxchangePartnerService.Credentials credDea=cred;
			Assert.AreEqual("Test Partner",credDea.Client);
			Assert.AreEqual("TEST-SERVICE-0001",credDea.ServiceID);
		}

		[TestMethod]
		public void Source_NoPartnerIdentityIsCompiledIn() {
			//Open Dental's and Practice-Web's DCI service IDs and client names must not come back in any DentalXChange caller.
			foreach(string relativePath in new[] { @"OpenDentBusiness\Eclaims\ClaimConnect.cs",@"OpenDental\Forms\FormInsPlan.cs" }) {
				string source=DxcTestFiles.ReadRepoFile(relativePath);
				Assert.IsFalse(source.Contains("002778"),relativePath+": Open Dental's DCI service ID.");
				Assert.IsFalse(source.Contains("001513"),relativePath+": Practice-Web's DCI service ID.");
				Assert.IsFalse(source.Contains("Client=\"OpenDental\""),relativePath);
				Assert.IsFalse(source.Contains("\"Practice-Web\""),relativePath);
				Assert.IsFalse(source.Contains("UploaderName=OpenDental"),relativePath);
			}
			string claimConnect=DxcTestFiles.ReadRepoFile(@"OpenDentBusiness\Eclaims\ClaimConnect.cs");
			StringAssert.Contains(claimConnect,"DxcPartnerIdentity.GetConfiguredOrThrow()");
		}

		[TestMethod]
		public void PrefNames_AreForkOnlyAndNotInThePrefNameEnum() {
			//The rows are fork rows like ForkSchemaLevel: an Open Dental database never has them, so adoption leaves the identity blank.
			Assert.IsFalse(System.Enum.IsDefined(typeof(PrefName),DxcPartnerIdentity.PREF_CLIENT));
			Assert.IsFalse(System.Enum.IsDefined(typeof(PrefName),DxcPartnerIdentity.PREF_SERVICE_ID));
			StringAssert.StartsWith(DxcPartnerIdentity.PREF_CLIENT,"Fork");
			StringAssert.StartsWith(DxcPartnerIdentity.PREF_SERVICE_ID,"Fork");
		}
	}
}
