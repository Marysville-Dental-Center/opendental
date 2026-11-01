// Synthetic rows for the B-ACCT database tests: made-up procedure codes (not code-set codes), patients, procedures, adjustments and insurance.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using OpenPmsDentalBusiness;

namespace BACCTTests {
	public static class SalesTaxData {
		///<summary>The sales-tax procedure code the tests configure. It is marked taxed on purpose: the sales-tax procedure must still never be taxed.</summary>
		public const string CODE_TAX="ZTAX01";
		///<summary>An ordinary procedure, not taxed by automation.</summary>
		public const string CODE_PLAIN="ZEXM01";
		///<summary>A procedure the office taxes automatically (IsTaxed).</summary>
		public const string CODE_TAXED="ZPRD01";
		///<summary>A procedure marked "Do not usually bill to insurance".</summary>
		public const string CODE_NOBILL="ZNBL01";

		///<summary>Inserts the synthetic procedure codes once and refreshes the cache.</summary>
		public static void EnsureCodes() {
			bool isInserted=false;
			isInserted|=InsertCodeIfMissing(CODE_TAX,"Synthetic sales tax",isTaxed:true,noBillIns:false);
			isInserted|=InsertCodeIfMissing(CODE_PLAIN,"Synthetic exam",isTaxed:false,noBillIns:false);
			isInserted|=InsertCodeIfMissing(CODE_TAXED,"Synthetic taxed product",isTaxed:true,noBillIns:false);
			isInserted|=InsertCodeIfMissing(CODE_NOBILL,"Synthetic no-bill item",isTaxed:false,noBillIns:true);
			if(isInserted) {
				Cache.Refresh(InvalidType.ProcCodes);
			}
		}

		private static bool InsertCodeIfMissing(string procCode,string descript,bool isTaxed,bool noBillIns) {
			if(PIn.Long(DataCore.GetScalar("SELECT COUNT(*) FROM procedurecode WHERE ProcCode='"+POut.String(procCode)+"'"))>0) {
				return false;
			}
			ProcedureCode procedureCode=new ProcedureCode();
			procedureCode.ProcCode=procCode;
			procedureCode.Descript=descript;
			procedureCode.AbbrDesc=procCode;
			procedureCode.ProcCat=Defs.GetDefsForCategory(DefCat.ProcCodeCats,true).First().DefNum;
			procedureCode.TreatArea=TreatmentArea.Mouth;
			procedureCode.IsTaxed=isTaxed;
			procedureCode.NoBillIns=noBillIns;
			OpenPmsDentalBusiness.Crud.ProcedureCodeCrud.Insert(procedureCode);
			return true;
		}

		public static long CodeNum(string procCode) {
			return ProcedureCodes.GetCodeNum(procCode);
		}

		///<summary>A positive adjustment type for sales tax, made once.</summary>
		public static long EnsureSalesTaxAdjType() {
			Def def=Defs.GetDefsForCategory(DefCat.AdjTypes).FirstOrDefault(x => x.ItemName=="Synthetic Sales Tax");
			if(def!=null) {
				return def.DefNum;
			}
			def=new Def();
			def.Category=DefCat.AdjTypes;
			def.ItemName="Synthetic Sales Tax";
			def.ItemValue="+";
			def.ItemOrder=Defs.GetDefsForCategory(DefCat.AdjTypes).Count;
			OpenPmsDentalBusiness.Crud.DefCrud.Insert(def);
			Cache.Refresh(InvalidType.Defs);
			return def.DefNum;
		}

		///<summary>Sets the sales-tax preferences the tests rely on and the sales-tax procedure code (blank turns it off).</summary>
		public static void SetUp(double taxPercent,string salesTaxProcCode,bool doAutomate=false,long defaultProvNum=0) {
			EnsureCodes();
			Prefs.UpdateDouble(PrefName.SalesTaxPercentage,taxPercent,false);
			Prefs.UpdateLong(PrefName.SalesTaxAdjustmentType,EnsureSalesTaxAdjType());
			Prefs.UpdateBool(PrefName.SalesTaxDoAutomate,doAutomate);
			Prefs.UpdateLong(PrefName.SalesTaxDefaultProvider,defaultProvNum);
			SalesTaxProcs.SaveProcCodeSetting(salesTaxProcCode);
		}

		public static long ProvNumDefault() {
			return PrefC.GetLong(PrefName.PracticeDefaultProv);
		}

		///<summary>A made-up guarantor.</summary>
		public static Patient CreatePatient(string lName,long clinicNum=0) {
			Patient patient=new Patient();
			patient.LName=lName;
			patient.FName="Synthetic";
			patient.PatStatus=PatientStatus.Patient;
			patient.Address="1 Test Street";
			patient.City="Testville";
			patient.State="OR";
			patient.Zip="97000";
			patient.BillingType=PrefC.GetLong(PrefName.PracticeDefaultBillType);
			patient.Birthdate=new DateTime(1980,1,1);
			patient.PriProv=ProvNumDefault();
			patient.ClinicNum=clinicNum;
			patient.PatNum=OpenPmsDentalBusiness.Crud.PatientCrud.Insert(patient);
			Patient patientOld=patient.Copy();
			patient.Guarantor=patient.PatNum;
			OpenPmsDentalBusiness.Crud.PatientCrud.Update(patient,patientOld);
			return patient;
		}

		public static Procedure CreateProc(Patient patient,string procCode,double fee,ProcStat procStat,DateTime date) {
			Procedure procedure=new Procedure();
			procedure.PatNum=patient.PatNum;
			procedure.CodeNum=CodeNum(procCode);
			procedure.ProcFee=fee;
			procedure.ProcStatus=procStat;
			procedure.ProcDate=date;
			procedure.DateTP=date;
			procedure.DateEntryC=date;
			procedure.ProvNum=ProvNumDefault();
			procedure.ClinicNum=patient.ClinicNum;
			procedure.UnitQty=1;
			procedure.ToothRange="";
			Procedures.Insert(procedure);
			return procedure;
		}

		public static Adjustment CreateAdjustment(Patient patient,double amount,DateTime date,long adjType,long procNum=0) {
			Adjustment adjustment=new Adjustment();
			adjustment.PatNum=patient.PatNum;
			adjustment.AdjAmt=amount;
			adjustment.AdjDate=date;
			adjustment.ProcDate=date;
			adjustment.DateEntry=date;
			adjustment.AdjType=adjType;
			adjustment.ProcNum=procNum;
			adjustment.ProvNum=ProvNumDefault();
			adjustment.AdjNum=OpenPmsDentalBusiness.Crud.AdjustmentCrud.Insert(adjustment);
			return adjustment;
		}

		///<summary>An adjustment type that is not sales tax.</summary>
		public static long OtherAdjType() {
			long salesTaxAdjType=EnsureSalesTaxAdjType();
			return Defs.GetDefsForCategory(DefCat.AdjTypes,true).First(x => x.DefNum!=salesTaxAdjType).DefNum;
		}

		public static List<Adjustment> GetAdjustments(long patNum) {
			return Adjustments.Refresh(patNum).ToList();
		}

		///<summary>A primary dental plan for the patient, with a made-up carrier.</summary>
		public static (InsPlan InsPlan,InsSub InsSub,PatPlan PatPlan) AddInsurance(Patient patient) {
			Carrier carrier=new Carrier();
			carrier.CarrierName="Synthetic Carrier "+patient.PatNum;
			Carriers.Insert(carrier);
			Carriers.RefreshCache();
			InsPlan insPlan=new InsPlan();
			insPlan.CarrierNum=carrier.CarrierNum;
			insPlan.PlanType="";
			insPlan.FeeSched=0;
			insPlan.CobRule=EnumCobRule.Basic;
			InsPlans.Insert(insPlan);
			InsSub insSub=new InsSub();
			insSub.Subscriber=patient.PatNum;
			insSub.PlanNum=insPlan.PlanNum;
			insSub.SubscriberID="SYN"+patient.PatNum;
			InsSubs.Insert(insSub);
			PatPlan patPlan=new PatPlan();
			patPlan.Ordinal=1;
			patPlan.PatNum=patient.PatNum;
			patPlan.InsSubNum=insSub.InsSubNum;
			PatPlans.Insert(patPlan);
			return (insPlan,insSub,patPlan);
		}
	}
}
