// Tests for the PDMP package: request contents (sender identity, location, SSN), the digest, property filtering, URL rules and report handling.
// No network and no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CodeBase.VendorDirect;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace VendorDirectTests.Pdmp {
	[TestClass]
	public class PdmpRequestTests {
		[TestInitialize]
		public void TestInitialize() {
			PdmpTestData.UseEnUs();
		}

		#region Digest
		[TestMethod]
		public void Digest_MatchesTheDocumentedVector() {
			BambooAuth bambooAuth=new BambooAuth(1516658372,"648db976-6428-459d-873e-891169e5a4e2");
			Assert.AreEqual("1a6bb6be5ecd6c440ea84a0e7a5c5ea742d203c92e1c37e4962dedb49fa72c47",bambooAuth.GetPasswordDigest("1!Password"));
		}

		[TestMethod]
		public void Digest_HashesUtf8() {
			BambooAuth bambooAuth=new BambooAuth(1600000000,"abc");
			string password="Pässwörd-ß";
			string expected;
			using(SHA256 sha256=SHA256.Create()) {
				expected=string.Concat(sha256.ComputeHash(Encoding.UTF8.GetBytes(password+":abc:1600000000")).Select(x => x.ToString("x2")));
			}
			Assert.AreEqual(expected,bambooAuth.GetPasswordDigest(password));
		}

		[TestMethod]
		public void Nonce_HasNoColonAndEpochIsSeconds() {
			for(int i=0;i<20;i++) {
				Assert.IsFalse(BambooAuth.GetNonce().Contains(":"));
			}
			Assert.AreNotEqual(BambooAuth.GetNonce(),BambooAuth.GetNonce());
			Assert.AreEqual(1601256120,BambooAuth.GetEpoch(new DateTime(2020,9,28,1,22,0,DateTimeKind.Utc)));
		}
		#endregion Digest

		#region Sender identity and request contents
		[TestMethod]
		public void Bamboo_PatientRequest_SendsBuiltInIdentity() {
			PdmpProperty pdmpProperty=PdmpTestData.MakeProperty(ProgramName.Bamboo.ToString(),"KS",PdmpTestData.GatewayUrl);
			BambooScript.PatientRequestType request=new PDMPBamboo(pdmpProperty).MakeRequester();
			Assert.AreEqual(ProductIdentity.ProductName,request.Requester.SenderSoftware.Developer);
			Assert.AreEqual(ProductIdentity.ProductName,request.Requester.SenderSoftware.Product);
			Assert.AreEqual(ProductIdentity.Version,request.Requester.SenderSoftware.Version);
			StringAssert.Contains(PdmpHttp.Serialize(request),"SenderSoftware");
		}

		[TestMethod]
		public void Bamboo_PatientRequest_SendsOverrideIdentity() {
			PdmpProperty pdmpProperty=PdmpTestData.MakeProperty(ProgramName.Bamboo.ToString(),"KS",PdmpTestData.GatewayUrl);
			pdmpProperty.SenderDeveloperOverride="Assigned Developer";
			pdmpProperty.SenderProductOverride=" Assigned Product ";
			BambooScript.PatientRequestType request=new PDMPBamboo(pdmpProperty).MakeRequester();
			Assert.AreEqual("Assigned Developer",request.Requester.SenderSoftware.Developer);
			Assert.AreEqual("Assigned Product",request.Requester.SenderSoftware.Product);
		}

		[TestMethod]
		public void Logicoy_Request_NeverNamesOpenDental() {
			PdmpProperty pdmpProperty=PdmpTestData.MakeProperty(ProgramName.PDMP.ToString(),"CA",PdmpTestData.LogicoyUrl);
			PDMPLogicoy pdmpLogicoy=new PDMPLogicoy(pdmpProperty);
			PDMPScript.PrescriptionSummaryRequestType request=new PDMPScript.PrescriptionSummaryRequestType();
			request.Requester=pdmpLogicoy.MakeRequester();
			request.PrescriptionRequest=pdmpLogicoy.MakePrescriptionRequest();
			string xml=PdmpHttp.Serialize(request);
			Assert.IsFalse(xml.IndexOf("Open Dental",StringComparison.OrdinalIgnoreCase)>=0,xml);
			Assert.IsFalse(xml.Contains("PDMP-LINK"),"The old product name is gone.");
			Assert.AreEqual(ProductIdentity.ProductName,request.Requester.SenderSoftware.Developer);
			Assert.AreEqual(ProductIdentity.Version,request.Requester.SenderSoftware.Version);
			pdmpProperty.SenderProductOverride="Registered Product";
			Assert.AreEqual("Registered Product",new PDMPLogicoy(pdmpProperty).MakeRequester().SenderSoftware.Product);
		}

		[TestMethod]
		public void Bamboo_Location_IsTheClinicNeverThePatient() {
			PdmpProperty pdmpProperty=PdmpTestData.MakeProperty(ProgramName.Bamboo.ToString(),"KS",PdmpTestData.GatewayUrl);
			pdmpProperty.LocationStateAbbr="MO";
			PDMPBamboo pdmpBamboo=new PDMPBamboo(pdmpProperty);
			foreach(BambooScript.LocationType location in new[] { pdmpBamboo.MakeRequester().Requester.Location,pdmpBamboo.MakeReportRequest().Requester.Location }) {
				CollectionAssert.AreEqual(new[] { PdmpTestData.ClinicStreet },location.Address.Street);
				Assert.AreEqual("Clinictown",location.Address.City);
				Assert.AreEqual("67000",location.Address.ZipCode);
				Assert.AreEqual(BambooScript.USStateCodeType.MO,location.Address.StateCode);
				Assert.AreEqual("Main Office",location.Name);
			}
			string xmlReport=PdmpHttp.Serialize(pdmpBamboo.MakeReportRequest());
			Assert.IsFalse(xmlReport.Contains(PdmpTestData.PatientStreet),"The report request carries no patient address.");
			//The patient's address belongs only in the patient query's Patient element.
			BambooScript.PatientRequestType patientRequest=pdmpBamboo.MakeRequester();
			patientRequest.PrescriptionRequest=pdmpBamboo.MakePrescriptionRequest();
			string xmlPatient=PdmpHttp.Serialize(patientRequest);
			int indexLocation=xmlPatient.IndexOf("<Location>",StringComparison.Ordinal);
			int indexLocationEnd=xmlPatient.IndexOf("</Location>",StringComparison.Ordinal);
			Assert.IsTrue(indexLocation>=0 && indexLocationEnd>indexLocation,xmlPatient);
			Assert.IsFalse(xmlPatient.Substring(indexLocation,indexLocationEnd-indexLocation).Contains(PdmpTestData.PatientStreet));
		}

		[TestMethod]
		public void Bamboo_Location_UsesFacilityIdentifiersWhenSet() {
			PdmpProperty pdmpProperty=PdmpTestData.MakeProperty(ProgramName.Bamboo.ToString(),"KS",PdmpTestData.GatewayUrl);
			BambooScript.LocationType location=new PDMPBamboo(pdmpProperty).MakeRequester().Requester.Location;
			Assert.AreEqual(pdmpProperty.Dea,location.DEANumber,"Blank facility DEA falls back to the provider's.");
			Assert.AreEqual(pdmpProperty.PdmpProv.NationalProvID,location.NPINumber);
			pdmpProperty.LocationDea="FD7654321";
			pdmpProperty.LocationNpi="1999999992";
			location=new PDMPBamboo(pdmpProperty).MakeRequester().Requester.Location;
			Assert.AreEqual("FD7654321",location.DEANumber);
			Assert.AreEqual("1999999992",location.NPINumber);
		}

		[TestMethod]
		public void Logicoy_SendsSsnOnlyWhereTheStateRequiresIt() {
			Assert.IsTrue(PdmpProperty.RequiresPatientSsn("KY"));
			foreach(string state in new[] { "CA","IL","MD","UT","WA","KS","" }) {
				Assert.IsFalse(PdmpProperty.RequiresPatientSsn(state),state);
			}
			PdmpProperty pdmpPropertyCa=PdmpTestData.MakeProperty(ProgramName.PDMP.ToString(),"CA",PdmpTestData.LogicoyUrl);
			Assert.IsNull(new PDMPLogicoy(pdmpPropertyCa).MakePrescriptionRequest().Patient.SSN);
			PdmpProperty pdmpPropertyKy=PdmpTestData.MakeProperty(ProgramName.PDMP.ToString(),"KY",PdmpTestData.LogicoyUrl);
			Assert.AreEqual("999001234",new PDMPLogicoy(pdmpPropertyKy).MakePrescriptionRequest().Patient.SSN);
		}
		#endregion Sender identity and request contents

		#region Setup grid
		[TestMethod]
		public void FilterAndSort_ShowsUrlsAndHidesOnlyTheCertificateRows() {
			Program programBamboo=new Program { ProgName=ProgramName.Bamboo.ToString() };
			List<ProgramProperty> listBamboo=new[] { PdmpProperty.BambooUrl,PdmpProperty.BambooUserName,PdmpProperty.BambooPassword,
				PdmpProperty.BambooAuthToken,PdmpProperty.BambooAuthPassword,PdmpProperty.BambooClientCertThumbprint,PdmpProperty.PdmpProvLicenseField }
				.Select(x => new ProgramProperty { PropertyDesc=x }).ToList();
			List<string> listShown=PDMP.FilterAndSortProperties(programBamboo,listBamboo).Select(x => x.PropertyDesc).ToList();
			Assert.AreEqual(PdmpProperty.PdmpProvLicenseField,listShown[0],"License field first.");
			CollectionAssert.Contains(listShown,PdmpProperty.BambooUrl);
			CollectionAssert.Contains(listShown,PdmpProperty.BambooClientCertThumbprint);
			CollectionAssert.DoesNotContain(listShown,PdmpProperty.BambooAuthToken);
			CollectionAssert.DoesNotContain(listShown,PdmpProperty.BambooAuthPassword);
			Program programPdmp=new Program { ProgName=ProgramName.PDMP.ToString() };
			List<ProgramProperty> listPdmp=new[] { "Utah PDMP Url","California PDMP Url","California PDMP Username",PdmpProperty.PdmpProvLicenseField }
				.Select(x => new ProgramProperty { PropertyDesc=x }).ToList();
			CollectionAssert.AreEqual(new[] { PdmpProperty.PdmpProvLicenseField,"California PDMP Url","California PDMP Username","Utah PDMP Url" },
				PDMP.FilterAndSortProperties(programPdmp,listPdmp).Select(x => x.PropertyDesc).ToList());
		}

		[TestMethod]
		public void RowDefinitions_CoverEveryLogicoyStateAndMarkSecrets() {
			List<Tuple<string,bool,string>> listRows=PdmpProperty.GetRowDefinitions(ProgramName.PDMP.ToString());
			foreach(string stateFull in PdmpProperty.DictLogicoyStates.Values) {
				Assert.IsTrue(listRows.Any(x => x.Item1==stateFull+" PDMP Url" && !x.Item2),stateFull);
				Assert.IsTrue(listRows.Any(x => x.Item1==stateFull+" PDMP Password" && x.Item2),stateFull);
			}
			List<Tuple<string,bool,string>> listBamboo=PdmpProperty.GetRowDefinitions(ProgramName.Bamboo.ToString());
			Assert.IsTrue(listBamboo.Single(x => x.Item1==PdmpProperty.BambooAuthToken).Item2);
			Assert.IsTrue(listBamboo.Single(x => x.Item1==PdmpProperty.BambooPassword).Item2);
			Assert.IsFalse(listBamboo.Single(x => x.Item1==PdmpProperty.BambooUrl).Item2);
			Assert.AreEqual(0,PdmpProperty.GetRowDefinitions("Other").Count);
		}
		#endregion Setup grid

		#region URL rules
		[TestMethod]
		public void UrlGuard_RequiresHttpsExceptThisComputer() {
			PdmpProperty pdmpProperty=PdmpTestData.MakeProperty(ProgramName.Bamboo.ToString(),"KS","");
			Assert.IsFalse(PdmpProperty.TryValidateUrl(pdmpProperty,out string error));
			StringAssert.Contains(error,"Setup > Program Links > Bamboo");
			Assert.IsFalse(error.Contains("contact support"));
			pdmpProperty.Url="http://gateway.example.test/patient";
			Assert.IsFalse(PdmpProperty.TryValidateUrl(pdmpProperty,out error));
			pdmpProperty.Url="gateway.example.test/patient";
			Assert.IsFalse(PdmpProperty.TryValidateUrl(pdmpProperty,out error),"Relative");
			pdmpProperty.Url=PdmpTestData.GatewayUrl;
			Assert.IsTrue(PdmpProperty.TryValidateUrl(pdmpProperty,out error),error);
			pdmpProperty.Url="http://localhost:8080/patient";
			Assert.IsTrue(PdmpProperty.TryValidateUrl(pdmpProperty,out error),"Loopback for a local stub.");
		}

		[TestMethod]
		public void UrlGuard_RefusesEveryBlockedDomainAndSubdomain() {
			PdmpProperty pdmpProperty=PdmpTestData.MakeProperty(ProgramName.Bamboo.ToString(),"KS","");
			Assert.AreEqual(5,HqHosts.BlockedDomains.Count);
			foreach(string domain in HqHosts.BlockedDomains) {
				foreach(string url in new[] { "https://"+domain+"/pdmp","https://pdmp."+domain+"/gateway","https://A.B."+domain.ToUpperInvariant()+"/x" }) {
					pdmpProperty.Url=url;
					Assert.IsFalse(PdmpProperty.TryValidateUrl(pdmpProperty,out string error),url);
					AssertEx.Throws<PdmpException>(() => PdmpHttp.CheckUrl(url));
				}
			}
		}

		[TestMethod]
		public void ReportLinks_OnBlockedHostsAreRefused() {
			PdmpProperty pdmpProperty=PdmpTestData.MakeProperty(ProgramName.Bamboo.ToString(),"KS",PdmpTestData.GatewayUrl);
			PDMPBamboo pdmpBamboo=new PDMPBamboo(pdmpProperty,new FakeHttpHandler());
			string linkBlocked="https://reports."+HqHosts.BlockedDomains[0]+"/view";
			BambooScript.ReportResponseType reportResponse=new BambooScript.ReportResponseType { Items=new object[] { linkBlocked } };
			PdmpException ex=AssertEx.Throws<PdmpException>(() => pdmpBamboo.ProcessReportResponse(reportResponse));
			Assert.AreEqual(PdmpErrorKind.UrlRefused,ex.Kind);
			reportResponse.Items=new object[] { "http://report.example.test/view" };
			Assert.AreEqual(PdmpErrorKind.UrlRefused,AssertEx.Throws<PdmpException>(() => pdmpBamboo.ProcessReportResponse(reportResponse)).Kind);
			PDMPScript.PrescriptionSummaryResponseType responseLogicoy=new PDMPScript.PrescriptionSummaryResponseType {
				Report=new PDMPScript.ReportType {
					ReportRequestURLs=new PDMPScript.ReportTypeReportRequestURLs {
						ViewableReport=new PDMPScript.ReportURLType { Value=linkBlocked },
					},
				},
			};
			Assert.AreEqual(PdmpErrorKind.UrlRefused,AssertEx.Throws<PdmpException>(() => PDMPLogicoy.ProcessResponse(responseLogicoy)).Kind);
		}
		#endregion URL rules

		#region Report handling
		[TestMethod]
		public void MessageOnlyReport_GivesTheMessageAndNoStep2() {
			FakeHttpHandler fakeHttpHandler=new FakeHttpHandler();
			PdmpProperty pdmpProperty=PdmpTestData.MakeProperty(ProgramName.Bamboo.ToString(),"KS",PdmpTestData.GatewayUrl);
			PDMPBamboo pdmpBamboo=new PDMPBamboo(pdmpProperty,fakeHttpHandler);
			BambooScript.PatientResponseType patientResponse=new BambooScript.PatientResponseType {
				Item=new BambooScript.ReportType { Message="No unique match. Search the state portal." },
			};
			pdmpBamboo.ProcessPatientResponse(patientResponse);
			Assert.IsFalse(pdmpBamboo.HasReport);
			Assert.AreEqual("",pdmpBamboo.ViewableReportUrl);
			StringAssert.Contains(pdmpBamboo.Response,"No unique match. Search the state portal.");
			Assert.IsFalse(pdmpBamboo.Response.Contains("Click 'OK'"),"No report to continue to.");
			AssertEx.Throws<PdmpException>(() => pdmpBamboo.GetReportLink());
			Assert.AreEqual(0,fakeHttpHandler.Requests.Count,"No ReportRequest without a report URL.");
		}

		[TestMethod]
		public void ReportWithScores_KeepsTheViewableUrl() {
			PdmpProperty pdmpProperty=PdmpTestData.MakeProperty(ProgramName.Bamboo.ToString(),"KS",PdmpTestData.GatewayUrl);
			PDMPBamboo pdmpBamboo=new PDMPBamboo(pdmpProperty,new FakeHttpHandler());
			pdmpBamboo.ProcessPatientResponse(PdmpHttpTests.MakeReportResponse());
			Assert.IsTrue(pdmpBamboo.HasReport);
			Assert.AreEqual(PdmpTestData.ViewableReportUrl,pdmpBamboo.ViewableReportUrl);
			StringAssert.Contains(pdmpBamboo.Response,"Narcotics: 310");
			StringAssert.Contains(pdmpBamboo.Response,"Click 'OK'");
		}
		#endregion Report handling
	}
}
