// Database tests for the eForm Patient List and Ins Pick fields and pending insurance (package A-EF2) on a scratch MariaDB database upgraded to the
// Open Dental 25.3.48 schema by the fork migration runner. Synthetic families, carriers and plans only. Skipped (inconclusive) when the local MariaDB
// does not answer, or when EFORM_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace EFormTests {
	[TestClass]
	public class InsPendingDbTests {
		private static readonly string DB_NAME=TestDb.ScratchName("ef2");
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			TestDb.CreateFresh25_3(DB_NAME);
			Security.CurUser=Userods.GetFirstOrDefault(x => true)??new Userod() { UserNum=1,UserName="Admin" };
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("EFORM_KEEP_DB")=="1") {
				return;
			}
			try {
				TestDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//Harmless; the name is unique to this run.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.Connect(DB_NAME);
		}

		#region Helpers
		private static Patient InsertPatient(string fName,string lName,long guarantor=0,PatientPosition position=PatientPosition.Single) {
			Patient patient=new Patient();
			patient.FName=fName;
			patient.LName=lName;
			patient.PatStatus=PatientStatus.Patient;
			patient.Position=position;
			patient.Birthdate=new DateTime(1980,1,1);
			patient.Address="";
			patient.City="";
			patient.State="";
			patient.Zip="";
			patient.WirelessPhone="";
			Patients.Insert(patient,false);
			Patient patientOld=patient.Copy();
			patient.Guarantor=guarantor==0 ? patient.PatNum : guarantor;
			Patients.Update(patient,patientOld);
			return patient;
		}

		private static EForm SaveForm(long patNum,params EFormField[] arrayFields) {
			EForm eForm=new EForm();
			eForm.IsNew=true;
			eForm.PatNum=patNum;
			eForm.Description="Synthetic registration";
			eForm.DateTimeShown=DateTime.Now;
			eForm.Status=EnumEFormStatus.Filled;
			eForm.ListEFormFields=arrayFields.ToList();
			for(int i=0;i<arrayFields.Length;i++) {
				arrayFields[i].PatNum=patNum;
				arrayFields[i].ItemOrder=i;
			}
			EForms.SaveNewEForms(new List<EForm>() { eForm });
			return eForm;
		}

		///<summary>An Ins Pick field for slot 1 answered New, with its child fields: subscriber, relationship, carrier, phone, group and ID.</summary>
		private static EFormField[] NewInsFields(long patNumSubscriber,string relat,string subscriberID,string carrier) {
			EFormField eFormFieldInsPick=InsPickLogicTests.InsPick("ins1PlanFromList","New");
			EFormField eFormFieldSubscriber=InsPickLogicTests.Child(EnumEFormFieldType.PatientList,"",patNumSubscriber==0 ? "" : patNumSubscriber.ToString());
			eFormFieldSubscriber.ValueLabel=new EFormPatListLayout().Serialize();
			EFormField eFormFieldRelat=InsPickLogicTests.Child(EnumEFormFieldType.RadioButtons,"ins1Relat",relat);
			eFormFieldRelat.PickListDb="Self|Spouse|Child|Dependent";
			eFormFieldRelat.PickListVis="Self|Spouse|Child|Dependent";
			return new EFormField[] {
				eFormFieldInsPick,
				eFormFieldSubscriber,
				eFormFieldRelat,
				InsPickLogicTests.Child(EnumEFormFieldType.TextField,"ins1CarrierName",carrier),
				InsPickLogicTests.Child(EnumEFormFieldType.TextField,"ins1CarrierPhone","(555)555-0123"),
				InsPickLogicTests.Child(EnumEFormFieldType.TextField,"ins1GroupNum","G-100"),
				InsPickLogicTests.Child(EnumEFormFieldType.TextField,"ins1EmployerName","Synthetic Works"),
				InsPickLogicTests.Child(EnumEFormFieldType.TextField,"ins1SubscriberID",subscriberID),
			};
		}

		private static long Count(string table,string where="1") {
			return PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM "+table+" WHERE "+where));
		}

		///<summary>A carrier, plan and subscription for the subscriber, and a patplan for each patient given.</summary>
		private static InsSub InsertInsurance(string carrierName,long patNumSubscriber,params long[] arrayPatNumsCovered) {
			Carrier carrier=new Carrier() { CarrierName=carrierName,Address="",Address2="",City="",State="",Zip="",Phone="",ElectID="",CDAnetVersion="",TIN="" };
			Carriers.Insert(carrier);
			Carriers.RefreshCache();
			InsPlan insPlan=new InsPlan() { CarrierNum=carrier.CarrierNum,GroupName="Synthetic Group",GroupNum="SG1",PlanNote="",PlanType="",TrojanID="",
				DivisionNo="",CanadianPlanFlag="",CanadianDiagnosticCode="",CanadianInstitutionCode="",RxBIN="",SopCode="" };
			InsPlans.Insert(insPlan);
			InsSub insSub=new InsSub() { PlanNum=insPlan.PlanNum,Subscriber=patNumSubscriber,SubscriberID="SUB777",BenefitNotes="",SubscNote="",
				ReleaseInfo=true,AssignBen=true };
			InsSubs.Insert(insSub);
			for(int i=0;i<arrayPatNumsCovered.Length;i++) {
				PatPlan patPlan=new PatPlan() { PatNum=arrayPatNumsCovered[i],InsSubNum=insSub.InsSubNum,PatID="",
					Relationship=arrayPatNumsCovered[i]==patNumSubscriber ? Relat.Self : Relat.Child };
				patPlan.Ordinal=(byte)(PatPlans.Refresh(arrayPatNumsCovered[i]).Count+1);
				PatPlans.Insert(patPlan);
			}
			return insSub;
		}
		#endregion Helpers

		#region Import
		[TestMethod]
		public void ImportNew_MakesPendingRowOnly_AndNotTwice() {
			Patient patientGuar=InsertPatient("Pat","Newins",position:PatientPosition.Married);
			Patient patientChild=InsertPatient("Kid","Newins",patientGuar.PatNum,PatientPosition.Child);
			EForm eForm=SaveForm(patientChild.PatNum,NewInsFields(patientGuar.PatNum,"Child"," ab-12 34","Synthetic Dental Co"));
			long countPlans=Count("insplan");
			long countSubs=Count("inssub");
			long countPatPlans=Count("patplan");
			List<InsPickImportResult> listResults=EFormImport.ImportInsPickFields(eForm);
			Assert.AreEqual(1,listResults.Count);
			Assert.IsTrue(listResults[0].IsCreated,listResults[0].Error);
			List<InsPending> listInsPendings=InsPendings.GetForPat(patientChild.PatNum);
			Assert.AreEqual(1,listInsPendings.Count);
			Assert.AreEqual(patientGuar.PatNum,listInsPendings[0].PatNumSubscriber);
			Assert.AreEqual(Relat.Child,listInsPendings[0].Relationship);
			Assert.AreEqual(1,listInsPendings[0].Ordinal);
			Assert.AreEqual("ab1234",listInsPendings[0].SubscriberID);
			Assert.AreEqual("Synthetic Dental Co",listInsPendings[0].CarrierName);
			Assert.AreEqual("G-100",listInsPendings[0].GroupNum);
			Assert.AreEqual("Synthetic Works",listInsPendings[0].Employer);
			Assert.AreEqual("(555)555-0123",listInsPendings[0].Phone);
			Assert.AreEqual(countPlans,Count("insplan"),"import never makes plans");
			Assert.AreEqual(countSubs,Count("inssub"));
			Assert.AreEqual(countPatPlans,Count("patplan"));
			Assert.AreEqual(1,Count("securitylog","PermType="+(int)EnumPermType.EFormImport+" AND PatNum="+patientChild.PatNum
				+" AND LogText LIKE 'Pending insurance created from eForm #"+eForm.EFormNum+"%'"));
			//Importing the same form again does not make a second row.
			listResults=EFormImport.ImportInsPickFields(eForm);
			Assert.IsFalse(listResults[0].IsCreated);
			Assert.AreEqual(1,InsPendings.GetForPat(patientChild.PatNum).Count);
		}

		[TestMethod]
		public void ImportNew_BlankIDOrConflictingRelationship_NotImported() {
			Patient patientGuar=InsertPatient("Pat","Blankid");
			EForm eForm=SaveForm(patientGuar.PatNum,NewInsFields(patientGuar.PatNum,"Self"," - ","Synthetic Dental Co"));
			List<InsPickImportResult> listResults=EFormImport.ImportInsPickFields(eForm);
			StringAssert.Contains(listResults[0].Error,"subscriber ID is blank");
			Assert.AreEqual(0,InsPendings.GetForPat(patientGuar.PatNum).Count);
			//The patient is the subscriber but says Spouse.
			EForm eForm2=SaveForm(patientGuar.PatNum,NewInsFields(patientGuar.PatNum,"Spouse","X1","Synthetic Dental Co"));
			listResults=EFormImport.ImportInsPickFields(eForm2);
			Assert.AreNotEqual("",listResults[0].Error);
			Assert.AreEqual(0,InsPendings.GetForPat(patientGuar.PatNum).Count);
			//Self with no Patient List pick means the patient.
			EForm eForm3=SaveForm(patientGuar.PatNum,NewInsFields(0,"Self","X1","Synthetic Dental Co"));
			listResults=EFormImport.ImportInsPickFields(eForm3);
			Assert.IsTrue(listResults[0].IsCreated,listResults[0].Error);
			Assert.AreEqual(patientGuar.PatNum,InsPendings.GetForPat(patientGuar.PatNum)[0].PatNumSubscriber);
		}

		[TestMethod]
		public void ImportNoneOrHidden_DoesNothing() {
			Patient patientGuar=InsertPatient("Pat","Noneins");
			EFormField[] arrayFields=NewInsFields(patientGuar.PatNum,"Self","X1","Synthetic Dental Co");
			arrayFields[0].ValueString="";//None
			EForm eForm=SaveForm(patientGuar.PatNum,arrayFields);
			List<InsPickImportResult> listResults=EFormImport.ImportInsPickFields(eForm);
			Assert.IsFalse(listResults[0].IsCreated);
			Assert.AreEqual("",listResults[0].Error);
			Assert.AreEqual(0,InsPendings.GetForPat(patientGuar.PatNum).Count);
		}

		[TestMethod]
		public void ImportExistingInsSub_PrefillsPending_UnlessAlreadyCovered() {
			Patient patientGuar=InsertPatient("Pat","Existing",position:PatientPosition.Married);
			Patient patientSpouse=InsertPatient("Spouse","Existing",patientGuar.PatNum,PatientPosition.Married);
			InsSub insSub=InsertInsurance("Existing Carrier Co",patientGuar.PatNum,patientGuar.PatNum);
			EFormField eFormFieldInsPick=InsPickLogicTests.InsPick("ins1PlanFromList",EFormInsPick.ToValueInsSub(insSub.InsSubNum));
			EForm eFormSpouse=SaveForm(patientSpouse.PatNum,eFormFieldInsPick);
			List<InsPickImportResult> listResults=EFormImport.ImportInsPickFields(eFormSpouse);
			Assert.IsTrue(listResults[0].IsCreated,listResults[0].Error);
			InsPending insPending=InsPendings.GetForPat(patientSpouse.PatNum).Single();
			Assert.AreEqual("Existing Carrier Co",insPending.CarrierName);
			Assert.AreEqual("SUB777",insPending.SubscriberID);
			Assert.AreEqual("SG1",insPending.GroupNum);
			Assert.AreEqual(Relat.Spouse,insPending.Relationship,"both married: our guess is Spouse");
			//The guarantor already has a patplan on that subscription: nothing to do.
			EFormField eFormFieldInsPickGuar=InsPickLogicTests.InsPick("ins1PlanFromList",EFormInsPick.ToValueInsSub(insSub.InsSubNum));
			EForm eFormGuar=SaveForm(patientGuar.PatNum,eFormFieldInsPickGuar);
			listResults=EFormImport.ImportInsPickFields(eFormGuar);
			Assert.IsFalse(listResults[0].IsCreated);
			Assert.AreEqual(0,InsPendings.GetForPat(patientGuar.PatNum).Count);
		}

		[TestMethod]
		public void ImportExistingInsPending_CopiesForThisPatient() {
			Patient patientGuar=InsertPatient("Pat","Copyins",position:PatientPosition.Married);
			Patient patientChild=InsertPatient("Kid","Copyins",patientGuar.PatNum,PatientPosition.Child);
			InsPending insPendingGuar=new InsPending() { PatNum=patientGuar.PatNum,PatNumSubscriber=patientGuar.PatNum,Relationship=Relat.Self,Ordinal=1,
				SubscriberID="C-55",CarrierName="Copy Carrier" };
			InsPendings.Insert(insPendingGuar);
			EFormField eFormFieldInsPick=InsPickLogicTests.InsPick("ins2PlanFromList",EFormInsPick.ToValueInsPending(insPendingGuar.InsPendingNum));
			EForm eForm=SaveForm(patientChild.PatNum,eFormFieldInsPick);
			List<InsPickImportResult> listResults=EFormImport.ImportInsPickFields(eForm);
			Assert.IsTrue(listResults[0].IsCreated,listResults[0].Error);
			InsPending insPendingChild=InsPendings.GetForPat(patientChild.PatNum).Single();
			Assert.AreNotEqual(insPendingGuar.InsPendingNum,insPendingChild.InsPendingNum);
			Assert.AreEqual(patientGuar.PatNum,insPendingChild.PatNumSubscriber);
			Assert.AreEqual(Relat.Child,insPendingChild.Relationship,"the source row was Self; the child is not the subscriber");
			Assert.AreEqual(2,insPendingChild.Ordinal,"the slot of the field");
			Assert.AreEqual("C55",insPendingChild.SubscriberID);
			Assert.AreEqual(1,InsPendings.GetForPat(patientGuar.PatNum).Count,"the source row stays");
		}

		[TestMethod]
		public void AutoImport_InsPickMakesPending() {
			Patient patientGuar=InsertPatient("Pat","Autoins");
			EFormField[] arrayFields=NewInsFields(patientGuar.PatNum,"Self","AUTO1","Auto Carrier");
			arrayFields[0].AutoImport=true;
			EForm eForm=SaveForm(patientGuar.PatNum,arrayFields);
			List<string> listChanges=EFormImport.ImportAutoFields(eForm);
			Assert.AreEqual(1,listChanges.Count);
			Assert.AreEqual("Auto Carrier",InsPendings.GetForPat(patientGuar.PatNum).Single().CarrierName);
		}

		[TestMethod]
		public void Filler_StartsAtCurrentPlanAndClearsChildFields() {
			Patient patientGuar=InsertPatient("Pat","Filler");
			InsSub insSub=InsertInsurance("Filler Carrier",patientGuar.PatNum,patientGuar.PatNum);
			EFormDef eFormDef=new EFormDef();
			eFormDef.Description="Synthetic insurance form";
			EFormFieldDef eFormFieldDefInsPick=new EFormFieldDef() { FieldType=EnumEFormFieldType.InsPick,DbLink="ins1PlanFromList",ValueLabel="Primary Insurance",
				PickListVis="None|New|Existing" };
			EFormFieldDef eFormFieldDefCarrier=new EFormFieldDef() { FieldType=EnumEFormFieldType.TextField,DbLink="ins1CarrierName",ValueLabel="Carrier",
				ConditionalParent="Primary Insurance",ConditionalValue="New" };
			EFormFieldDef eFormFieldDefInsPick2=new EFormFieldDef() { FieldType=EnumEFormFieldType.InsPick,DbLink="ins2PlanFromList",ValueLabel="Secondary",
				PickListVis="None|New|Existing" };
			eFormDef.ListEFormFieldDefs=new List<EFormFieldDef>() { eFormFieldDefInsPick,eFormFieldDefCarrier,eFormFieldDefInsPick2 };
			EForm eForm=EForms.CreateEFormFromEFormDef(eFormDef,patientGuar.PatNum,EnumEFormStatus.None);
			EFormFiller.FillFields(eForm);
			Assert.AreEqual(EFormInsPick.ToValueInsSub(insSub.InsSubNum),eForm.ListEFormFields[0].ValueString);
			Assert.AreEqual("",eForm.ListEFormFields[1].ValueString,"the New fields are not prefilled from the current plan");
			Assert.AreEqual("",eForm.ListEFormFields[2].ValueString,"no secondary insurance: None");
			StringAssert.Contains(EFormInsPick.GetExistingSummary(eForm.ListEFormFields[0].ValueString),"Filler Carrier");
		}

		[TestMethod]
		public void ExistingChoices_ListFamilySubscriptionsAndDistinctPending() {
			Patient patientGuar=InsertPatient("Pat","Choices");
			Patient patientChild=InsertPatient("Kid","Choices",patientGuar.PatNum);
			Patient patientChild2=InsertPatient("Kid2","Choices",patientGuar.PatNum);
			InsSub insSub=InsertInsurance("Choice Carrier",patientGuar.PatNum,patientGuar.PatNum);
			InsPendings.Insert(new InsPending() { PatNum=patientChild.PatNum,PatNumSubscriber=patientGuar.PatNum,Relationship=Relat.Child,SubscriberID="P1",
				CarrierName="Pending Carrier" });
			InsPendings.Insert(new InsPending() { PatNum=patientChild2.PatNum,PatNumSubscriber=patientGuar.PatNum,Relationship=Relat.Child,SubscriberID="P1",
				CarrierName="Pending Carrier" });
			List<EFormInsPickChoice> listChoices=EFormInsPick.GetExistingChoices(patientChild.PatNum);
			Assert.AreEqual(2,listChoices.Count,"one subscription and one distinct pending insurance");
			Assert.AreEqual(EFormInsPick.ToValueInsSub(insSub.InsSubNum),listChoices[0].Value);
			Assert.IsTrue(listChoices[1].IsPending);
			StringAssert.Contains(listChoices[1].Display,"Pending Carrier");
		}
		#endregion Import

		#region Conversion
		[TestMethod]
		public void Convert_NewCarrierAndPlan_AlsoForSibling() {
			Patient patientGuar=InsertPatient("Pat","Convert");
			Patient patientChild=InsertPatient("Kid","Convert",patientGuar.PatNum);
			Patient patientChild2=InsertPatient("Kid2","Convert",patientGuar.PatNum);
			InsPending insPending=new InsPending() { PatNum=patientChild.PatNum,PatNumSubscriber=patientGuar.PatNum,Relationship=Relat.Child,Ordinal=1,
				SubscriberID="CV-1",CarrierName="Brand New Synthetic Carrier",Phone="(555)555-0144",GroupNum="NG1",GroupName="New Group",Employer="NewCo" };
			InsPendings.Insert(insPending);
			InsPending insPendingSibling=InsPendings.CopyForPatient(insPending,patientChild2.PatNum,Relat.Child,1);
			InsPendings.Insert(insPendingSibling);
			List<InsPending> listDuplicates=InsPendings.GetFamilyDuplicates(insPending,
				InsPendings.GetForPats(new List<long>() { patientGuar.PatNum,patientChild.PatNum,patientChild2.PatNum }));
			Assert.AreEqual(1,listDuplicates.Count);
			Assert.AreEqual(0,InsPendings.GetCarrierMatches("Brand New Synthetic Carrier").Count);
			InsPendingConvertResult result=InsPendings.Convert(insPending,carrierNum:0,planNum:0,ordinal:1,listDuplicates);
			Assert.IsTrue(result.IsCarrierCreated);
			Assert.IsTrue(result.IsPlanCreated);
			Assert.IsTrue(result.IsInsSubCreated);
			Assert.AreEqual(2,result.ListPatPlanNums.Count);
			Carrier carrier=Carriers.GetCarrier(result.CarrierNum);
			Assert.AreEqual("Brand New Synthetic Carrier",carrier.CarrierName);
			Assert.AreEqual("(555)555-0144",carrier.Phone);
			InsPlan insPlan=InsPlans.GetPlan(result.PlanNum,null);
			Assert.AreEqual("NG1",insPlan.GroupNum);
			Assert.AreEqual("NewCo",Employers.GetName(insPlan.EmployerNum));
			InsSub insSub=InsSubs.GetOne(result.InsSubNum);
			Assert.AreEqual(patientGuar.PatNum,insSub.Subscriber);
			Assert.AreEqual("CV1",insSub.SubscriberID);
			PatPlan patPlanChild=PatPlans.Refresh(patientChild.PatNum).Single();
			Assert.AreEqual(insSub.InsSubNum,patPlanChild.InsSubNum);
			Assert.AreEqual(Relat.Child,patPlanChild.Relationship);
			Assert.AreEqual(1,patPlanChild.Ordinal);
			Assert.AreEqual(insSub.InsSubNum,PatPlans.Refresh(patientChild2.PatNum).Single().InsSubNum,"the sibling reuses the subscription");
			Assert.AreEqual(0,InsPendings.GetForPats(new List<long>() { patientChild.PatNum,patientChild2.PatNum }).Count,"pending rows are removed");
			Assert.AreEqual("I",DataCore.GetScalar("SELECT HasIns FROM patient WHERE PatNum="+patientChild.PatNum));
			//The carrier is now an exact match for the next pending row.
			Assert.AreEqual(result.CarrierNum,InsPendings.GetCarrierMatches("brand new synthetic carrier").First().CarrierNum);
			Assert.AreEqual(1,InsPendings.GetPlanMatches(result.CarrierNum,"NG-1","newco",onlyMatching:true).Count);
		}

		[TestMethod]
		public void Convert_ExistingPlan_ReusesSubscriptionAndTakesOrdinal() {
			Patient patientGuar=InsertPatient("Pat","Ordinal");
			InsSub insSubOld=InsertInsurance("Old Primary Carrier",patientGuar.PatNum,patientGuar.PatNum);
			InsSub insSubOther=InsertInsurance("Other Carrier",patientGuar.PatNum);//subscribed, not attached
			InsPlan insPlanOther=InsPlans.GetPlan(insSubOther.PlanNum,null);
			InsPending insPending=new InsPending() { PatNum=patientGuar.PatNum,PatNumSubscriber=patientGuar.PatNum,Relationship=Relat.Self,Ordinal=1,
				SubscriberID="ignored",CarrierName="Other Carrier" };
			InsPendings.Insert(insPending);
			InsPendingConvertResult result=InsPendings.Convert(insPending,insPlanOther.CarrierNum,insPlanOther.PlanNum,ordinal:1);
			Assert.IsFalse(result.IsCarrierCreated);
			Assert.IsFalse(result.IsPlanCreated);
			Assert.IsFalse(result.IsInsSubCreated);
			Assert.AreEqual(insSubOther.InsSubNum,result.InsSubNum);
			List<PatPlan> listPatPlans=PatPlans.Refresh(patientGuar.PatNum).OrderBy(x => x.Ordinal).ToList();
			Assert.AreEqual(2,listPatPlans.Count);
			Assert.AreEqual(insSubOther.InsSubNum,listPatPlans[0].InsSubNum,"the converted plan is primary");
			Assert.AreEqual(insSubOld.InsSubNum,listPatPlans[1].InsSubNum,"the old primary moved down");
			//A plan of another carrier is refused.
			InsPending insPending2=new InsPending() { PatNum=patientGuar.PatNum,PatNumSubscriber=patientGuar.PatNum,Relationship=Relat.Self,Ordinal=2,
				SubscriberID="Z9",CarrierName="Whatever" };
			InsPendings.Insert(insPending2);
			InsPlan insPlanOld=InsPlans.GetPlan(insSubOld.PlanNum,null);
			try {
				InsPendings.Convert(insPending2,insPlanOther.CarrierNum,insPlanOld.PlanNum,ordinal:2);
				Assert.Fail("a plan of another carrier must be refused");
			}
			catch(ApplicationException) {
				//expected
			}
		}
		#endregion Conversion

		#region Patient merge, delete, Database Maintenance, Patient List Add
		[TestMethod]
		public void PatientMerge_MovesRowsAndFixesSelf() {
			Patient patientTo=InsertPatient("Keep","Merge");
			Patient patientFrom=InsertPatient("Dupe","Merge");
			Patient patientOther=InsertPatient("Other","Merge");
			InsPending insPendingFromAsPatient=new InsPending() { PatNum=patientFrom.PatNum,PatNumSubscriber=patientTo.PatNum,Relationship=Relat.Spouse,
				SubscriberID="M1",CarrierName="Merge Carrier" };
			InsPendings.Insert(insPendingFromAsPatient);
			InsPending insPendingFromAsSub=new InsPending() { PatNum=patientOther.PatNum,PatNumSubscriber=patientFrom.PatNum,Relationship=Relat.Child,
				SubscriberID="M2",CarrierName="Merge Carrier" };
			InsPendings.Insert(insPendingFromAsSub);
			Patients.MergeTwoPatientPointOfNoReturn(patientTo.PatNum,patientFrom.PatNum,new List<PatField>(),new List<PatField>());
			InsPending insPendingMoved=InsPendings.GetOne(insPendingFromAsPatient.InsPendingNum);
			Assert.AreEqual(patientTo.PatNum,insPendingMoved.PatNum);
			Assert.AreEqual(Relat.Self,insPendingMoved.Relationship,"patient and subscriber are now the same person");
			Assert.AreEqual(patientTo.PatNum,InsPendings.GetOne(insPendingFromAsSub.InsPendingNum).PatNumSubscriber);
		}

		[TestMethod]
		public void PatientDelete_RemovesPendingRows() {
			Patient patient=InsertPatient("Gone","Delete");
			InsPendings.Insert(new InsPending() { PatNum=patient.PatNum,PatNumSubscriber=patient.PatNum,Relationship=Relat.Self,SubscriberID="D1",
				CarrierName="Delete Carrier" });
			Patients.Delete(patient);
			Assert.AreEqual(0,InsPendings.GetForPat(patient.PatNum).Count);
		}

		[TestMethod]
		public void DatabaseMaintenance_FindsAndDeletesOrphans() {
			Patient patient=InsertPatient("Real","Orphan");
			InsPending insPendingGood=new InsPending() { PatNum=patient.PatNum,PatNumSubscriber=patient.PatNum,Relationship=Relat.Self,SubscriberID="O1",
				CarrierName="Orphan Carrier" };
			InsPendings.Insert(insPendingGood);
			InsPending insPendingOrphan=new InsPending() { PatNum=patient.PatNum,PatNumSubscriber=987654321,Relationship=Relat.Spouse,SubscriberID="O2",
				CarrierName="Orphan Carrier" };
			InsPendings.Insert(insPendingOrphan);
			string log=DatabaseMaintenances.InsPendingWithInvalidPatient(verbose:false,DbmMode.Check);
			StringAssert.Contains(log,": 1");
			DatabaseMaintenances.InsPendingWithInvalidPatient(verbose:false,DbmMode.Fix);
			Assert.IsNull(InsPendings.GetOne(insPendingOrphan.InsPendingNum));
			Assert.IsNotNull(InsPendings.GetOne(insPendingGood.InsPendingNum));
			Assert.AreEqual("",DatabaseMaintenances.InsPendingWithInvalidPatient(verbose:false,DbmMode.Check));
		}

		[TestMethod]
		public void PatientListAdd_CreatesNonPatientInFamily() {
			Patient patientGuar=InsertPatient("Pat","Addfam");
			Patient patientOld=patientGuar.Copy();
			patientGuar.Address="300 Synthetic Ave";
			patientGuar.City="Faketown";
			patientGuar.HmPhone="(555)555-0177";
			Patients.Update(patientGuar,patientOld);
			Patient patientChild=InsertPatient("Kid","Addfam",patientGuar.PatNum);
			Assert.AreNotEqual("",EFormInsPick.ValidateNewFamilyMember("","Addfam",new DateTime(1970,5,5)));
			Assert.AreNotEqual("",EFormInsPick.ValidateNewFamilyMember("New","Addfam",DateTime.MinValue));
			Patient patientNew=EFormInsPick.AddFamilyMember(patientChild.PatNum,"newperson","Addfam",new DateTime(1970,5,5),eFormNum:12);
			Patient patientDb=Patients.GetPat(patientNew.PatNum);
			Assert.AreEqual(PatientStatus.NonPatient,patientDb.PatStatus);
			Assert.AreEqual(patientGuar.PatNum,patientDb.Guarantor);
			Assert.AreEqual("300 Synthetic Ave",patientDb.Address);
			Assert.AreEqual("Faketown",patientDb.City);
			Assert.AreEqual("Newperson",patientDb.FName);
			Assert.AreEqual(1,Count("securitylog","PermType="+(int)EnumPermType.PatientCreate+" AND PatNum="+patientNew.PatNum
				+" AND LogText LIKE '%Created from eForm Patient List field, eForm #12%'"));
			Assert.IsTrue(Patients.GetFamily(patientChild.PatNum).ListPats.Any(x => x.PatNum==patientNew.PatNum));
		}
		#endregion Patient merge, delete, Database Maintenance, Patient List Add
	}
}
