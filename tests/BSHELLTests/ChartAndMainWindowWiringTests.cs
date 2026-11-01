// v25_2-chart-medical-rows-bold, and the main window's switch to the shared domain login helper (backlog notes for B-SHELL).
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BSHELLTests {
	[TestClass]
	public class ChartAndMainWindowWiringTests {
		[TestMethod]
		public void PatientInfo_MedUrgentAndMedicalSummaryLabelsAreBold() {
			string source=TestPaths.ReadRepoFile("OpenDental","Main Modules","ControlChart.cs");
			string fill=TestPaths.GetMethodBody(source,"public void FillPtInfo(bool isRefreshData=true)");
			foreach(string internalName in new[] { "Med Urgent","Medical Summary" }) {
				int indexCase=fill.IndexOf("case \""+internalName+"\":",StringComparison.Ordinal);
				Assert.IsTrue(indexCase>0,internalName);
				int indexBreak=fill.IndexOf("break;",indexCase,StringComparison.Ordinal);
				string caseBody=fill.Substring(indexCase,indexBreak-indexCase);
				StringAssert.Contains(caseBody,"row.Cells[0].Bold=YN.Yes;",internalName+": the label cell is bold.");
			}
			//The Med Urgent value keeps its red bold text.
			int indexMedUrgent=fill.IndexOf("case \"Med Urgent\":",StringComparison.Ordinal);
			string medUrgent=fill.Substring(indexMedUrgent,fill.IndexOf("break;",indexMedUrgent,StringComparison.Ordinal)-indexMedUrgent);
			StringAssert.Contains(medUrgent,"cell.ColorText=Color.Red;");
			StringAssert.Contains(medUrgent,"cell.Bold=YN.Yes;");
			//The label cell the cases make bold is the one every row gets before the switch.
			int indexLabel=fill.IndexOf("row.Cells.Add(displayField.Description);",StringComparison.Ordinal);
			int indexSwitch=fill.IndexOf("switch(displayField.InternalName) {",StringComparison.Ordinal);
			Assert.IsTrue(indexLabel>0 && indexLabel<indexSwitch);
		}

		[TestMethod]
		public void StartupDomainLogin_UsesTheSharedHelper() {
			string source=TestPaths.ReadRepoFile("OpenDental","Main Modules","FormOpenDental.cs");
			StringAssert.Contains(source,"else if(DomainLogin.IsEnabled()) {");
			StringAssert.Contains(source,"string domainUser=DomainLogin.BuildDomainUser(domainGuidPref,Environment.UserName);");
			Assert.IsFalse(source.Contains("(domainGuidPref+'\\\\'+Environment.UserName)"));
		}

		[TestMethod]
		public void DomainHelper_MatchesTheOldStartupRules() {
			//The startup condition was "DomainLoginEnabled and a non-blank DomainLoginPath"; the helper's is the same.
			Assert.IsTrue(new OpenPmsDentalBusiness.DomainLoginSettings { IsEnabled=true,LdapPath="LDAP://DC=example,DC=local" }.IsUsable);
			Assert.IsFalse(new OpenPmsDentalBusiness.DomainLoginSettings { IsEnabled=true,LdapPath="  " }.IsUsable);
			Assert.IsFalse(new OpenPmsDentalBusiness.DomainLoginSettings { IsEnabled=false,LdapPath="LDAP://DC=example,DC=local" }.IsUsable);
			Assert.AreEqual("{guid}\\jsmith",OpenPmsDentalBusiness.DomainLogin.BuildDomainUser("{guid}","jsmith"),"Same \"{guid}\\{user}\" key userod.DomainUser holds.");
		}
	}
}
