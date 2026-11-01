// A synthetic Medi-Cal claim printed on the installed Denti-Cal DC217_2019 claim form (backlog package M2-CLAIMFORM): which value lands in which
// box, line by line and form by form, as FormClaimPrint fills them. Nothing is printed and no window is shown. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace M2CLAIMFORMTests {
	[TestClass]
	public class Dc217PrintDbTests {
		private const string NPI="1234567893";
		private const string MEDI_CAL_ID="90000000A95001";
		private static readonly DateTime _dateFirstService=new DateTime(2026,9,1);

		[TestInitialize]
		public void TestInitialize() {
			ClaimFormDb.Begin();
			ClaimForms.InsertInternalIfMissing(ClaimForms.DescriptionDentiCalDC217_2019);
			ClaimFormDb.RefreshCaches();
		}

		[TestMethod]
		public void SeventeenProcedures_PrintOnTwoFormsInLineOrder() {
			SyntheticClaim syntheticClaim=SyntheticClaim.Create("P",17);
			//The claim form comes from the insurance plan, as for any claim printed from the Account or Manage module.
			ClaimPrintHarness claimPrintHarness=new ClaimPrintHarness(null,syntheticClaim.ClaimNum,syntheticClaim.PatNum);
			Assert.IsTrue(claimPrintHarness.FillDisplayStrings());
			Assert.AreEqual(2,claimPrintHarness.PageCount(17));
			//Form 1: procedures 1 to 15.
			Dictionary<string,string> dictPage1=claimPrintHarness.FillPage(0);
			for(int line=1;line<=15;line++) {
				AssertLine(dictPage1,line,syntheticClaim.ListLines[line-1]);
			}
			Assert.AreEqual(syntheticClaim.ListLines.Take(15).Sum(x => x.Fee).ToString("F"),dictPage1["TotalFee"],"Each form totals its own lines.");
			//Form 2: procedures 16 and 17, then empty lines.
			Dictionary<string,string> dictPage2=claimPrintHarness.FillPage(1);
			AssertLine(dictPage2,1,syntheticClaim.ListLines[15]);
			AssertLine(dictPage2,2,syntheticClaim.ListLines[16]);
			for(int line=3;line<=15;line++) {
				foreach(string field in Dc217.ArrayLineFields) {
					Assert.AreEqual("",dictPage2["P"+line+field],"Form 2, P"+line+field);
				}
			}
			Assert.AreEqual(syntheticClaim.ListLines.Skip(15).Sum(x => x.Fee).ToString("F"),dictPage2["TotalFee"]);
			//The header repeats on every form.
			foreach(Dictionary<string,string> dict in new[] { dictPage1,dictPage2 }) {
				Assert.AreEqual(MEDI_CAL_ID,dict["PatientMedicaidID"]);
				Assert.AreEqual(syntheticClaim.PatientLName+", "+syntheticClaim.PatientFName+" ",dict["PatientLastFirst"]);
				Assert.AreEqual("05          04      16",dict["PatientDOB"]);
				Assert.AreEqual("X",dict["PatientIsFemale"]);
				Assert.AreEqual("",dict["PatientIsMale"]);
				Assert.AreEqual(NPI,dict["BillingDentistNPI"]);
				Assert.AreEqual("X",dict["PlaceIsOffice"]);
				Assert.AreEqual("",dict["PlaceIsSkilledNursFac"]);
				Assert.IsTrue(dict["TreatingDentistSignature"]=="Signature on File" || dict["TreatingDentistSignature"].Contains("Synthetic"),
					dict["TreatingDentistSignature"]);
				Assert.AreEqual(_dateFirstService.AddDays(1).ToString("MM/dd/yyyy"),dict["TreatingDentistSigDate"],
					"An unsent claim is signed with the date of its first procedure.");
			}
		}

		[TestMethod]
		public void Tar_PrintsWithoutServiceDates() {
			//A DC-217 without dates of service is a treatment authorization request (TAR); the printout leaves dates off preauthorizations.
			SyntheticClaim syntheticClaim=SyntheticClaim.Create("PreAuth",3);
			ClaimPrintHarness claimPrintHarness=new ClaimPrintHarness(null,syntheticClaim.ClaimNum,syntheticClaim.PatNum);
			Assert.IsTrue(claimPrintHarness.FillDisplayStrings());
			Dictionary<string,string> dict=claimPrintHarness.FillPage(0);
			for(int line=1;line<=3;line++) {
				Assert.AreEqual("",dict["P"+line+"Date"],"Line "+line);
				Assert.AreEqual(syntheticClaim.ListLines[line-1].Code,dict["P"+line+"Code"]);
				Assert.AreEqual(syntheticClaim.ListLines[line-1].Fee.ToString("F"),dict["P"+line+"Fee"]);
			}
		}

		[TestMethod]
		public void ClaimPrintedFromTheClaim_FindsThePlansFormBeforeReadingIt() {
			//Printing from the claim or the send list leaves the form to the printout, which takes the plan's. The box 18 check (Open Dental 24.4)
			//read the form before that and failed on every such claim; a 1500 medical claim shows the dates now come out as well.
			//The shipped 1500 form, which has the box 18 items (a copy installed by an older convert script may not).
			ClaimForm claimForm1500=ClaimForms.GetInternalClaim("1500_02_12");
			claimForm1500.Description="1500 box 18 "+Guid.NewGuid().ToString("N").Substring(0,6);
			long claimFormNum1500=ClaimForms.Insert(claimForm1500,includeClaimFormItems:true);
			SyntheticClaim syntheticClaim=SyntheticClaim.Create("P",2,claimFormNum1500,EnumClaimMedType.Medical,new DateTime(2026,8,30));
			ClaimPrintHarness claimPrintHarness=new ClaimPrintHarness(null,syntheticClaim.ClaimNum,syntheticClaim.PatNum);
			Assert.IsTrue(claimPrintHarness.FillDisplayStrings());
			Dictionary<string,string> dict=claimPrintHarness.FillPage(0);
			Assert.AreEqual("08    30    26",dict["HospitalizationDateFrom"]);
			Assert.AreEqual(syntheticClaim.ListLines[0].Code,dict["P1Code"]);
		}

		private static void AssertLine(Dictionary<string,string> dict,int line,SyntheticLine syntheticLine) {
			string p="P"+line;
			Assert.AreEqual(syntheticLine.Code,dict[p+"Code"],p+"Code");
			Assert.AreEqual(syntheticLine.Fee.ToString("F"),dict[p+"Fee"],p+"Fee");
			Assert.AreEqual(syntheticLine.DateService.ToString("MM/dd/yy"),dict[p+"Date"],p+"Date");
			Assert.AreEqual(syntheticLine.ToothOrArea,dict[p+"ToothNumOrArea"],p+"ToothNumOrArea");
			Assert.AreEqual(syntheticLine.Surface,dict[p+"Surface"],p+"Surface");
			Assert.AreEqual(syntheticLine.Description,dict[p+"Description"],p+"Description");
			Assert.AreEqual(NPI,dict[p+"TreatProvNPI"],p+"TreatProvNPI");
		}

		///<summary>One procedure on the synthetic claim and what its line should print.</summary>
		private class SyntheticLine {
			public string Code;
			public decimal Fee;
			public DateTime DateService;
			public string ToothOrArea;
			public string Surface;
			public string Description;
		}

		///<summary>A Medi-Cal patient with one plan that uses the installed DC217_2019 form, and one claim with the given number of procedures.
		///</summary>
		private class SyntheticClaim {
			public long ClaimNum;
			public long PatNum;
			public string PatientLName;
			public string PatientFName;
			public List<SyntheticLine> ListLines=new List<SyntheticLine>();

			///<summary>claimFormNum 0: the installed DC217_2019 form.</summary>
			public static SyntheticClaim Create(string claimType,int procCount,long claimFormNum=0,EnumClaimMedType medType=EnumClaimMedType.Dental,
				DateTime admitDate=default)
			{
				string suffix=Guid.NewGuid().ToString("N").Substring(0,6);
				if(claimFormNum==0) {
					claimFormNum=ClaimForms.InsertInternalIfMissing(ClaimForms.DescriptionDentiCalDC217_2019);
				}
				Assert.IsTrue(claimFormNum>0);
				Provider provider=new Provider { Abbr="DC"+suffix,FName="Alex",LName="Synthetic",NationalProvID=NPI,SigOnFile=true };
				OpenPmsDentalBusiness.Crud.ProviderCrud.Insert(provider);
				Patient patient=new Patient { LName="Dcpatient"+suffix,FName="Medi",Gender=PatientGender.Female,Birthdate=new DateTime(2016,5,4),
					MedicaidID=MEDI_CAL_ID,Address="1 Synthetic Street",City="Synthetic Valley",State="CA",Zip="95901",PatStatus=PatientStatus.Patient,
					PriProv=provider.ProvNum,ChartNumber="",AdmitDate=admitDate };
				OpenPmsDentalBusiness.Crud.PatientCrud.Insert(patient);
				patient.Guarantor=patient.PatNum;
				OpenPmsDentalBusiness.Crud.PatientCrud.Update(patient);
				Carrier carrier=new Carrier { CarrierName="Synthetic Medi-Cal Dental "+suffix,State="CA" };
				OpenPmsDentalBusiness.Crud.CarrierCrud.Insert(carrier);
				InsPlan insPlan=new InsPlan { CarrierNum=carrier.CarrierNum,ClaimFormNum=claimFormNum,PlanType="f" };
				OpenPmsDentalBusiness.Crud.InsPlanCrud.Insert(insPlan);
				InsSub insSub=new InsSub { PlanNum=insPlan.PlanNum,Subscriber=patient.PatNum,SubscriberID=MEDI_CAL_ID,ReleaseInfo=true,AssignBen=true };
				OpenPmsDentalBusiness.Crud.InsSubCrud.Insert(insSub);
				OpenPmsDentalBusiness.Crud.PatPlanCrud.Insert(new PatPlan { PatNum=patient.PatNum,InsSubNum=insSub.InsSubNum,Ordinal=1,Relationship=Relat.Self });
				Claim claim=new Claim { PatNum=patient.PatNum,PlanNum=insPlan.PlanNum,InsSubNum=insSub.InsSubNum,ClaimType=claimType,ClaimStatus="W",
					DateService=_dateFirstService.AddDays(1),ProvTreat=provider.ProvNum,ProvBill=provider.ProvNum,PatRelat=Relat.Self,
					PlaceService=PlaceOfService.Office,MedType=medType };
				OpenPmsDentalBusiness.Crud.ClaimCrud.Insert(claim);
				SyntheticClaim syntheticClaim=new SyntheticClaim { ClaimNum=claim.ClaimNum,PatNum=patient.PatNum,PatientLName=patient.LName,
					PatientFName=patient.FName };
				for(int n=1;n<=procCount;n++) {
					//Line 1 a surface filling, line 2 a quadrant, line 3 an arch, the rest single teeth.
					TreatmentArea treatmentArea=n==1 ? TreatmentArea.Surf : n==2 ? TreatmentArea.Quad : n==3 ? TreatmentArea.Arch : TreatmentArea.Tooth;
					ProcedureCode procedureCode=new ProcedureCode { ProcCode="Z"+suffix.Substring(0,3).ToUpperInvariant()+n.ToString("00"),
						Descript="Synthetic service "+n,AbbrDesc="Syn"+n,TreatArea=treatmentArea };
					OpenPmsDentalBusiness.Crud.ProcedureCodeCrud.Insert(procedureCode);
					SyntheticLine syntheticLine=new SyntheticLine { Code=procedureCode.ProcCode,Fee=100m+n+0.25m,DateService=_dateFirstService.AddDays(n),
						Surface="",Description=procedureCode.Descript };
					Procedure procedure=new Procedure { PatNum=patient.PatNum,CodeNum=procedureCode.CodeNum,ProcDate=syntheticLine.DateService,
						ProcStatus=ProcStat.C,ProcFee=(double)syntheticLine.Fee,ProvNum=provider.ProvNum,PlaceService=PlaceOfService.Office };
					switch(treatmentArea) {
						case TreatmentArea.Surf:
							procedure.ToothNum="14";
							procedure.Surf="MOD";
							syntheticLine.ToothOrArea="14";
							syntheticLine.Surface="MOD";
							break;
						case TreatmentArea.Quad:
							procedure.Surf="UR";
							syntheticLine.ToothOrArea="10";//The area code the printout uses for the upper right quadrant.
							syntheticLine.Description="UR "+procedureCode.Descript;//The printout puts the quadrant before the description.
							break;
						case TreatmentArea.Arch:
							procedure.Surf="U";
							syntheticLine.ToothOrArea="01";//The area code for the upper arch.
							break;
						default:
							procedure.ToothNum=(n+10).ToString();
							syntheticLine.ToothOrArea=procedure.ToothNum;
							break;
					}
					OpenPmsDentalBusiness.Crud.ProcedureCrud.Insert(procedure);
					ClaimProc claimProc=new ClaimProc { ProcNum=procedure.ProcNum,ClaimNum=claim.ClaimNum,PatNum=patient.PatNum,PlanNum=insPlan.PlanNum,
						InsSubNum=insSub.InsSubNum,ProvNum=provider.ProvNum,FeeBilled=(double)syntheticLine.Fee,CodeSent=procedureCode.ProcCode,
						ProcDate=syntheticLine.DateService,Status=claimType=="PreAuth" ? ClaimProcStatus.Preauth : ClaimProcStatus.NotReceived,LineNumber=(byte)n };
					OpenPmsDentalBusiness.Crud.ClaimProcCrud.Insert(claimProc);
					syntheticClaim.ListLines.Add(syntheticLine);
				}
				claim.ClaimFee=(double)syntheticClaim.ListLines.Sum(x => x.Fee);
				OpenPmsDentalBusiness.Crud.ClaimCrud.Update(claim);
				ClaimFormDb.RefreshCaches();
				return syntheticClaim;
			}
		}
	}
}
