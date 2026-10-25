// The Open Dental 24.4 secondary-insurance static text fields, filled on a patient letter in the scratch database. Synthetic data only
// (backlog package A-SHEET).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using UnitTestsCore;

namespace SheetTests {
	[TestClass]
	public class SecondaryInsStaticTextDbTests {
		private const string LETTER_TEXT="Primary: [insFeeSchedule]|[insPlanNote]|[insSubBirthDate]|[insSubNote]|[insType]|[subscriberID]\r\n"
			+"Secondary: [ins2FeeSchedule]|[ins2PlanNote]|[ins2SubBirthDate]|[ins2SubNote]|[ins2Type]|[subscriber2ID]";

		[TestInitialize]
		public void TestInitialize() {
			SheetDb.Begin();
		}

		[TestMethod]
		public void PatientLetter_FillsPrimaryAndSecondaryFields() {
			string suffix=Guid.NewGuid().ToString("N").Substring(0,6);
			Patient patient=PatientT.CreatePatient(suffix,lName:"Asheet",fName:"Synthetic",birthDate:new DateTime(1980,4,12));
			Patient spouse=PatientT.CreatePatient(suffix+"s",lName:"Asheet",fName:"Spouse",birthDate:new DateTime(1979,11,3),guarantor:patient.PatNum);
			long feeSched1=FeeSchedT.CreateFeeSched(FeeScheduleType.Normal,"AsheetPrimary"+suffix);
			long feeSched2=FeeSchedT.CreateFeeSched(FeeScheduleType.Normal,"AsheetSecondary"+suffix);
			Carrier carrier1=CarrierT.CreateCarrier("Primary Carrier (synthetic) "+suffix);
			Carrier carrier2=CarrierT.CreateCarrier("Secondary Carrier (synthetic) "+suffix);
			InsPlan plan1=InsPlanT.CreateInsPlan(carrier1.CarrierNum,feeSched:feeSched1,planNote:"Primary plan note",planType:"p");
			InsPlan plan2=InsPlanT.CreateInsPlan(carrier2.CarrierNum,feeSched:feeSched2,planNote:"Secondary plan note",planType:"c");
			InsSub sub1=InsSubT.CreateInsSub(patient.PatNum,plan1.PlanNum,"PRI-"+suffix);
			InsSub sub2=InsSubT.CreateInsSub(spouse.PatNum,plan2.PlanNum,"SEC-"+suffix);
			sub1.SubscNote="Primary subscriber note";
			InsSubs.Update(sub1);
			sub2.SubscNote="Secondary subscriber note";
			InsSubs.Update(sub2);
			PatPlanT.CreatePatPlan(1,patient.PatNum,sub1.InsSubNum);
			PatPlanT.CreatePatPlan(2,patient.PatNum,sub2.InsSubNum);
			string text=FillLetter(patient);
			string[] arrayLines=text.Split(new[] { "\r\n" },StringSplitOptions.None);
			Assert.AreEqual("Primary: FeeSchedAsheetPrimary"+suffix+"|Primary plan note|4/12/1980|Primary subscriber note|PPO Percentage|PRI-"+suffix,arrayLines[0]);
			Assert.AreEqual("Secondary: FeeSchedAsheetSecondary"+suffix+"|Secondary plan note|11/3/1979|Secondary subscriber note|Capitation|SEC-"+suffix,arrayLines[1]);
		}

		[TestMethod]
		public void PatientLetter_NoSecondaryPlan_SecondaryFieldsBlank() {
			string suffix=Guid.NewGuid().ToString("N").Substring(0,6);
			Patient patient=PatientT.CreatePatient(suffix,lName:"Asheet",fName:"Single",birthDate:new DateTime(1990,1,2));
			long feeSched1=FeeSchedT.CreateFeeSched(FeeScheduleType.Normal,"AsheetOnly"+suffix);
			Carrier carrier1=CarrierT.CreateCarrier("Only Carrier (synthetic) "+suffix);
			InsPlan plan1=InsPlanT.CreateInsPlan(carrier1.CarrierNum,feeSched:feeSched1,planNote:"Only plan note",planType:"");
			InsSub sub1=InsSubT.CreateInsSub(patient.PatNum,plan1.PlanNum,"ONLY-"+suffix);
			PatPlanT.CreatePatPlan(1,patient.PatNum,sub1.InsSubNum);
			string text=FillLetter(patient);
			Assert.AreEqual("Secondary: |||||",text.Split(new[] { "\r\n" },StringSplitOptions.None)[1]);
			StringAssert.StartsWith(text,"Primary: FeeSchedAsheetOnly"+suffix+"|Only plan note|1/2/1990||Category Percentage|ONLY-"+suffix);
		}

		///<summary>Fills a patient letter holding LETTER_TEXT the way the Patient Letter window does, and returns the filled text.</summary>
		private static string FillLetter(Patient patient) {
			SheetDef sheetDef=new SheetDef(SheetTypeEnum.PatientLetter) {
				Description="Secondary insurance letter (synthetic)",
				FontName="Arial",
				FontSize=10,
				Width=850,
				Height=1100,
			};
			sheetDef.SheetFieldDefs.Add(SheetFieldDef.NewStaticText(LETTER_TEXT,10,"Arial",false,50,50,750,60));
			Sheet sheet=SheetUtil.CreateSheet(sheetDef,patient.PatNum);
			SheetParameter.SetParameter(sheet,"PatNum",patient.PatNum);
			Assert.AreEqual("",SheetFiller.FillFields(sheet));
			return sheet.SheetFields.Single(x => x.FieldType==SheetFieldType.StaticText).FieldValue;
		}
	}
}
