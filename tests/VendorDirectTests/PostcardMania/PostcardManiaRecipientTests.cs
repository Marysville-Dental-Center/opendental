// Tests for the PostcardMania (PCM Integrations) DirectMail API v3 integration: recipients (minimum necessary), splitting and references.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.PostcardMania;

namespace VendorDirectTests.PostcardMania {
	[TestClass]
	public class PostcardManiaRecipientTests {
		private static PatientInfo MakePatient(long patNum,string fName,string lName,string address,string zip="98270",string country="",
			string preferred="",string email="")
		{
			return new PatientInfo {
				PatNum=patNum,
				FName=fName,
				LName=lName,
				Preferred=preferred,
				Address=address,
				Address2="",
				City="Marysville",
				State="WA",
				Zip=zip,
				Country=country,
				Email=email,
				Birthdate=new DateTime(1980,1,2),
				Status=PatientStatus.Patient,
				DateTimeLastAppt=new DateTime(2026,1,1),
			};
		}

		[TestMethod]
		public void BuildRecipients_DedupesFiltersAndSendsOnlyNameAndAddress() {
			List<PatientInfo> listPatientInfos=new List<PatientInfo> {
				MakePatient(1,"Ann","Avery","1 Elm St",email:"ann@example.com"),
				MakePatient(1,"Ann","Avery","1 Elm St",email:"ann2@example.com"),//Second email row for the same patient.
				MakePatient(2,"Bob","Baker","2 Oak Ave",country:"Canada"),
				MakePatient(3,"Cy","Cole","3 Pine Rd",zip:""),
				MakePatient(4,"Dee","Dunn","4 Fir Ln",country:" United  States ",preferred:"Dot"),
				MakePatient(5,"Eve","Ezra","5 Ash Ct",country:"usa"),
			};
			PostcardRecipientList postcardRecipientList=AdvertisingPostcards.BuildRecipients(listPatientInfos,usePreferredName:false,sendPatientReference:false);
			Assert.AreEqual(3,postcardRecipientList.ListRecipients.Count);
			Assert.AreEqual(1,postcardRecipientList.CountDuplicates);
			Assert.AreEqual(1,postcardRecipientList.CountNonUs);
			Assert.AreEqual(1,postcardRecipientList.CountIncompleteAddress);
			CollectionAssert.AreEqual(new long[] { 1,4,5 },postcardRecipientList.ListPatNums);
			PcmRecipient pcmRecipient=postcardRecipientList.ListRecipients[1];
			Assert.AreEqual("Dee",pcmRecipient.FirstName,"The preferred name is used only when the setting is on.");
			Assert.IsNull(pcmRecipient.ExtRefNbr,"No patient reference unless the office turns it on.");
			Assert.IsNull(pcmRecipient.Address2,"A blank Address2 is left out.");
			string json=PcmJson.Serialize(postcardRecipientList.ListRecipients);
			foreach(string forbidden in new[] { "ann@example.com","1980","birth","email","status","Patient","2026" }) {
				Assert.IsFalse(json.Contains(forbidden),"The recipients must not carry "+forbidden);
			}
		}

		[TestMethod]
		public void BuildRecipients_PreferredNameAndPatientReferenceWhenTurnedOn() {
			List<PatientInfo> listPatientInfos=new List<PatientInfo> {
				MakePatient(40,"Dorothy","Dunn","4 Fir Ln",preferred:"Dot"),
				MakePatient(41,"Ed","Ely","5 Ash Ct"),
			};
			PostcardRecipientList postcardRecipientList=AdvertisingPostcards.BuildRecipients(listPatientInfos,usePreferredName:true,sendPatientReference:true);
			Assert.AreEqual("Dot",postcardRecipientList.ListRecipients[0].FirstName);
			Assert.AreEqual("Ed",postcardRecipientList.ListRecipients[1].FirstName,"No preferred name: the first name.");
			Assert.AreEqual("P40",postcardRecipientList.ListRecipients[0].ExtRefNbr);
			Assert.AreEqual("P41",postcardRecipientList.ListRecipients[1].ExtRefNbr);
		}

		[TestMethod]
		public void BuildRecipients_OneCardPerHouseholdAddressedToTheGuarantor() {
			List<PatientInfo> listPatientInfos=new List<PatientInfo> {
				MakePatient(11,"Kid","Kerr","10 Maple Dr"),
				MakePatient(10,"Pat","Kerr","10  maple dr"),//Same address, other spacing and case.
				MakePatient(12,"Sam","Solo","12 Birch Way"),
			};
			Dictionary<long,Patient> dictGuarantors=new Dictionary<long,Patient> {
				{ 11,new Patient { PatNum=10,FName="Pat",LName="Kerr" } },
				{ 10,new Patient { PatNum=10,FName="Pat",LName="Kerr" } },
			};
			PostcardRecipientList postcardRecipientList=AdvertisingPostcards.BuildRecipients(listPatientInfos,false,true,isOneCardPerHousehold:true,
				dictGuarantorsByPatNum:dictGuarantors);
			Assert.AreEqual(2,postcardRecipientList.ListRecipients.Count);
			Assert.AreEqual(1,postcardRecipientList.CountHouseholdMerged);
			PcmRecipient pcmRecipientKerr=postcardRecipientList.ListRecipients.Single(x => x.LastName=="Kerr");
			Assert.AreEqual("Pat",pcmRecipientKerr.FirstName);
			Assert.AreEqual("P10",pcmRecipientKerr.ExtRefNbr);
			Assert.AreEqual("Sam",postcardRecipientList.ListRecipients.Single(x => x.LastName=="Solo").FirstName,"No guarantor known: the patient.");
		}

		[TestMethod]
		public void BuildRecipients_UsesAddress2WhenAddressIsBlank() {
			PatientInfo patientInfo=MakePatient(20,"Al","Ames","");
			patientInfo.Address2="PO Box 9";
			PostcardRecipientList postcardRecipientList=AdvertisingPostcards.BuildRecipients(new List<PatientInfo> { patientInfo },false,false);
			Assert.AreEqual("PO Box 9",postcardRecipientList.ListRecipients[0].Address);
			Assert.IsNull(postcardRecipientList.ListRecipients[0].Address2);
		}

		[TestMethod]
		public void IsUsCountry_AcceptsOnlyUsValues() {
			foreach(string country in new[] { null,""," ","US","usa","U.S.A.","United States","united  states of america" }) {
				Assert.IsTrue(AdvertisingPostcards.IsUsCountry(country),country??"null");
			}
			foreach(string country in new[] { "Canada","Mexico","UK","USA East" }) {
				Assert.IsFalse(AdvertisingPostcards.IsUsCountry(country),country);
			}
		}

		[TestMethod]
		public void SplitIntoOrders_50001RecipientsGiveTwoOrdersWithSuffixes() {
			PcmPostcardOrderRequest requestTemplate=PostcardManiaFixture.MakeOrder("unused",0);
			List<PcmPostcardOrderRequest> listOrders=AdvertisingPostcards.SplitIntoOrders(requestTemplate,PostcardManiaFixture.MakeRecipients(50001),"ops-0-x");
			Assert.AreEqual(2,listOrders.Count);
			Assert.AreEqual("ops-0-x-1",listOrders[0].ExtRefNbr);
			Assert.AreEqual("ops-0-x-2",listOrders[1].ExtRefNbr);
			Assert.AreEqual(50000,listOrders[0].Recipients.Count);
			Assert.AreEqual(1,listOrders[1].Recipients.Count);
			Assert.AreEqual(77,listOrders[1].DesignID,"Each order keeps the design and the other settings.");
			listOrders=AdvertisingPostcards.SplitIntoOrders(requestTemplate,PostcardManiaFixture.MakeRecipients(50000),"ops-0-y");
			Assert.AreEqual(1,listOrders.Count);
			Assert.AreEqual("ops-0-y",listOrders[0].ExtRefNbr,"A single order keeps the reference as is.");
			Assert.AreEqual(0,AdvertisingPostcards.SplitIntoOrders(requestTemplate,new List<PcmRecipient>(),"z").Count);
		}

		[TestMethod]
		public void NewExtRefNbr_IsUniqueAndTaggedWithTheClinic() {
			string extRefNbr1=AdvertisingPostcards.NewExtRefNbr(12);
			string extRefNbr2=AdvertisingPostcards.NewExtRefNbr(12);
			Assert.AreNotEqual(extRefNbr1,extRefNbr2);
			StringAssert.StartsWith(extRefNbr1,"ops-12-");
			Assert.IsTrue(AdvertisingPostcards.IsExtRefNbrForClinic(extRefNbr1,12));
			Assert.IsFalse(AdvertisingPostcards.IsExtRefNbrForClinic(extRefNbr1,1));
			Assert.IsFalse(AdvertisingPostcards.IsExtRefNbrForClinic(null,12));
			Assert.IsTrue(extRefNbr1.Length<=50);
		}

		[TestMethod]
		public void RecipientList_RemoveAtIndexesKeepsPatNumsInStep() {
			PostcardRecipientList postcardRecipientList=new PostcardRecipientList();
			for(int i=0;i<5;i++) {
				postcardRecipientList.Add(PostcardManiaFixture.MakeRecipient(i),100+i);
			}
			postcardRecipientList.RemoveAtIndexes(new[] { 3,1,3,9 });
			CollectionAssert.AreEqual(new long[] { 100,102,104 },postcardRecipientList.ListPatNums);
			CollectionAssert.AreEqual(new[] { "First0","First2","First4" },postcardRecipientList.ListRecipients.Select(x => x.FirstName).ToList());
		}
	}
}
