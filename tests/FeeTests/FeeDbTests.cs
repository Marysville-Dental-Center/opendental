// Fee effective dates against a scratch database: lookups, duplicate rule, fee schedule groups, fee tools (copy, import, increase, export),
// list methods, procedure fees, estimates scope, database maintenance and the Procedure Codes report. Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace FeeTests {
	[TestClass]
	public class FeeDbTests {
		private static string _skipReason;
		private static int _counter;
		private static readonly DateTime _date2025=new DateTime(2025,1,1);
		private static readonly DateTime _date2027=new DateTime(2027,1,1);
		private static readonly DateTime _date2028=new DateTime(2028,1,1);
		private static readonly DateTime _date2099=new DateTime(2099,1,1);

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=TestDb.EnsureDatabase();
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			TestDb.EnsureDatabase();
		}

		#region Helpers
		internal static FeeSched NewFeeSched(FeeScheduleType feeScheduleType=FeeScheduleType.Normal,bool isGlobal=false) {
			FeeSched feeSched=new FeeSched();
			feeSched.Description="FeeTest sched "+(++_counter);
			feeSched.FeeSchedType=feeScheduleType;
			feeSched.IsGlobal=isGlobal;
			feeSched.ItemOrder=1000+_counter;
			FeeScheds.Insert(feeSched);
			FeeScheds.RefreshCache();
			return feeSched;
		}

		internal static ProcedureCode NewCode() {
			ProcedureCode procedureCode=new ProcedureCode();
			procedureCode.ProcCode="FT"+(++_counter).ToString("0000");
			procedureCode.Descript="Fee test code "+_counter;
			procedureCode.AbbrDesc="FT"+_counter;
			procedureCode.ProcCat=Defs.GetDefsForCategory(DefCat.ProcCodeCats,true).FirstOrDefault()?.DefNum??0;
			ProcedureCodes.Insert(procedureCode);
			ProcedureCodes.RefreshCache();
			return procedureCode;
		}

		internal static Clinic NewClinic() {
			Clinic clinic=new Clinic();
			clinic.Abbr="FTC"+(++_counter);
			clinic.Description="Fee test clinic "+_counter;
			Clinics.Insert(clinic);
			Clinics.RefreshCache();
			return clinic;
		}

		internal static Fee InsertFee(long feeSchedNum,long codeNum,double amount,DateTime dateEffective,long clinicNum=0,long provNum=0) {
			Fee fee=new Fee();
			fee.FeeSched=feeSchedNum;
			fee.CodeNum=codeNum;
			fee.ClinicNum=clinicNum;
			fee.ProvNum=provNum;
			fee.Amount=amount;
			fee.DateEffective=dateEffective;
			Fees.Insert(fee);
			return fee;
		}

		///<summary>MSTest v1 has no Assert.ThrowsException.</summary>
		private static void AssertThrowsApplicationException(Action action) {
			try {
				action();
			}
			catch(ApplicationException) {
				return;
			}
			Assert.Fail("Expected an ApplicationException.");
		}

		private static List<string> Describe(List<Fee> listFees) {
			return listFees.OrderBy(x => x.ClinicNum).ThenBy(x => x.DateEffective)
				.Select(x => x.ClinicNum+"|"+(x.DateEffective.Year<1880 ? "always" : x.DateEffective.ToString("yyyy-MM-dd"))+"|"+x.Amount.ToString("0.00"))
				.ToList();
		}
		#endregion Helpers

		[TestMethod]
		public void Db_DateEffectiveRoundTripsAndDefaultsToAlways() {
			FeeSched feeSched=NewFeeSched();
			ProcedureCode procedureCode=NewCode();
			Fee feeAlways=InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,10,DateTime.MinValue);
			Fee feeDated=InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,11,_date2027);
			Assert.AreEqual(DateTime.MinValue,Fees.GetOneByFeeNum(feeAlways.FeeNum).DateEffective);
			Assert.AreEqual(_date2027,Fees.GetOneByFeeNum(feeDated.FeeNum).DateEffective);
			Assert.AreEqual("0001-01-01",DataCore.GetScalar("SELECT DATE_FORMAT(DateEffective,'%Y-%m-%d') FROM fee WHERE FeeNum="+feeAlways.FeeNum));
		}

		[TestMethod]
		public void Db_GetFee_AsOfDate_NeverShowsFutureFeesToday() {
			FeeSched feeSched=NewFeeSched();
			ProcedureCode procedureCode=NewCode();
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,100,DateTime.MinValue);
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,110,_date2025);
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,150,_date2099);
			Assert.AreEqual(100,Fees.GetFee(procedureCode.CodeNum,feeSched.FeeSchedNum,0,0,dateAsOf:new DateTime(2024,12,31)).Amount);
			Assert.AreEqual(110,Fees.GetFee(procedureCode.CodeNum,feeSched.FeeSchedNum,0,0).Amount);//today
			Assert.AreEqual(110,Fees.GetAmount(procedureCode.CodeNum,feeSched.FeeSchedNum,0,0));
			Assert.AreEqual(150,Fees.GetAmount0(procedureCode.CodeNum,feeSched.FeeSchedNum,dateAsOf:_date2099));
			//Exact match and the list path agree with the database path.
			Assert.AreEqual(110,Fees.GetFeeFromDb(procedureCode.CodeNum,feeSched.FeeSchedNum,0,0,doGetExactMatch:true).Amount);
			List<Fee> listFees=Fees.GetListForScheds(feeSched.FeeSchedNum);
			Assert.AreEqual(3,listFees.Count);
			Assert.AreEqual(110,Fees.GetFee(procedureCode.CodeNum,feeSched.FeeSchedNum,0,0,listFees).Amount);
			Assert.AreEqual(150,Fees.GetFee(procedureCode.CodeNum,feeSched.FeeSchedNum,0,0,listFees,_date2099).Amount);
		}

		[TestMethod]
		public void Db_GetFee_ClinicOverrideWithFutureDateFallsBackToDefault() {
			FeeSched feeSched=NewFeeSched();
			ProcedureCode procedureCode=NewCode();
			Clinic clinic=NewClinic();
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,100,DateTime.MinValue);
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,130,_date2099,clinic.ClinicNum);
			Assert.AreEqual(100,Fees.GetFee(procedureCode.CodeNum,feeSched.FeeSchedNum,clinic.ClinicNum,0).Amount);
			Assert.AreEqual(130,Fees.GetFee(procedureCode.CodeNum,feeSched.FeeSchedNum,clinic.ClinicNum,0,dateAsOf:_date2099).Amount);
			Assert.IsNull(Fees.GetFeeFromDb(procedureCode.CodeNum,feeSched.FeeSchedNum,clinic.ClinicNum,0,doGetExactMatch:true));
		}

		[TestMethod]
		public void Db_ScopeDateAsOf_ReachesLookupsWithoutADate() {
			FeeSched feeSched=NewFeeSched();
			ProcedureCode procedureCode=NewCode();
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,100,DateTime.MinValue);
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,110,_date2025);
			using(Fees.ScopeDateAsOf(new DateTime(2024,6,1))) {
				Assert.AreEqual(100,Fees.GetAmount(procedureCode.CodeNum,feeSched.FeeSchedNum,0,0));
			}
			Assert.AreEqual(110,Fees.GetAmount(procedureCode.CodeNum,feeSched.FeeSchedNum,0,0));
		}

		[TestMethod]
		public void Db_SameKeyAndDate_Rejected_OtherDateAllowed() {
			FeeSched feeSched=NewFeeSched();
			ProcedureCode procedureCode=NewCode();
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,100,DateTime.MinValue);
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,110,_date2027);//same key, other date: fine
			AssertThrowsApplicationException(() => InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,120,_date2027));
			AssertThrowsApplicationException(() => InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,120,DateTime.MinValue));
			List<Fee> listBatch=new List<Fee> {
				new Fee { FeeSched=feeSched.FeeSchedNum,CodeNum=procedureCode.CodeNum,Amount=1,DateEffective=_date2028 },
				new Fee { FeeSched=feeSched.FeeSchedNum,CodeNum=procedureCode.CodeNum,Amount=2,DateEffective=_date2028 },
			};
			AssertThrowsApplicationException(() => Fees.InsertMany(listBatch));
			listBatch=new List<Fee> { new Fee { FeeSched=feeSched.FeeSchedNum,CodeNum=procedureCode.CodeNum,Amount=1,DateEffective=_date2027 } };
			AssertThrowsApplicationException(() => Fees.InsertMany(listBatch));
			Assert.AreEqual(2,Fees.GetListExactAllDates(feeSched.FeeSchedNum,0,0).Count);
			//SynchList (Procedure Codes grid) refuses a duplicate too.
			List<Fee> listFeesDb=Fees.GetListExactAllDates(feeSched.FeeSchedNum,0,0);
			List<Fee> listFeesNew=listFeesDb.Select(x => x.Copy()).ToList();
			listFeesNew.Add(new Fee { FeeSched=feeSched.FeeSchedNum,CodeNum=procedureCode.CodeNum,Amount=3,DateEffective=_date2027 });
			AssertThrowsApplicationException(() => Fees.SynchList(listFeesNew,listFeesDb));
		}

		[TestMethod]
		public void Db_ListMethods_ShowRowsInEffectToday() {
			FeeSched feeSched=NewFeeSched();
			ProcedureCode procedureCode=NewCode();
			ProcedureCode procedureCode2=NewCode();
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,100,DateTime.MinValue);
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,110,_date2025);
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,150,_date2099);
			InsertFee(feeSched.FeeSchedNum,procedureCode2.CodeNum,70,_date2099);//future only
			List<Fee> listExact=Fees.GetListExact(feeSched.FeeSchedNum,0,0);
			Assert.AreEqual(1,listExact.Count);
			Assert.AreEqual(110,listExact[0].Amount);
			Assert.AreEqual(1,Fees.GetListExact(feeSched.FeeSchedNum,new List<long> { 0 },0).Count);
			Assert.AreEqual(4,Fees.GetListExactAllDates(feeSched.FeeSchedNum,0,0).Count);
			Assert.AreEqual(100,Fees.GetListExactAsOf(feeSched.FeeSchedNum,0,0,new DateTime(2024,1,1)).Single().Amount);
			Assert.AreEqual(2,Fees.GetListExactAsOf(feeSched.FeeSchedNum,0,0,_date2099).Count);
			Assert.AreEqual(110,Fees.GetFeesForCode(procedureCode.CodeNum).Single(x => x.FeeSched==feeSched.FeeSchedNum).Amount);
			Assert.AreEqual(110,Fees.GetFeesForCodeNoOverrides(procedureCode.CodeNum).Single(x => x.FeeSched==feeSched.FeeSchedNum).Amount);
			Assert.AreEqual(0,Fees.GetFeesForCode(procedureCode2.CodeNum).Count);
			//Dated rows of one code count once.
			Assert.AreEqual(2,Fees.GetCountByFeeSchedNum(feeSched.FeeSchedNum));
		}

		[TestMethod]
		public void Db_FeeSchedGroups_SyncCopiesDateEffective() {
			bool isShowGroupsOld=PrefC.GetBool(PrefName.ShowFeeSchedGroups);
			try {
				Prefs.UpdateBool(PrefName.ShowFeeSchedGroups,true);
				FeeSched feeSched=NewFeeSched();
				ProcedureCode procedureCode=NewCode();
				Clinic clinicA=NewClinic();
				Clinic clinicB=NewClinic();
				FeeSchedGroups.Insert(new FeeSchedGroup { Description="FeeTest group "+_counter,FeeSchedNum=feeSched.FeeSchedNum,
					ClinicNums=clinicA.ClinicNum+","+clinicB.ClinicNum });
				Fee feeBaseA=InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,50,DateTime.MinValue,clinicA.ClinicNum);
				Fee feeDatedA=InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,70,_date2027,clinicA.ClinicNum);
				CollectionAssert.AreEqual(new[] { "always|50.00","2027-01-01|70.00" },
					Describe(Fees.GetListExactAllDates(feeSched.FeeSchedNum,clinicB.ClinicNum,0)).Select(x => x.Substring(x.IndexOf('|')+1)).ToArray());
				//Updating the dated fee of clinic A changes only the dated fee of clinic B.
				feeDatedA.Amount=75;
				Fees.Update(feeDatedA);
				CollectionAssert.AreEqual(new[] { "always|50.00","2027-01-01|75.00" },
					Describe(Fees.GetListExactAllDates(feeSched.FeeSchedNum,clinicB.ClinicNum,0)).Select(x => x.Substring(x.IndexOf('|')+1)).ToArray());
				//Deleting it deletes only the dated fee of clinic B.
				Fees.Delete(feeDatedA);
				CollectionAssert.AreEqual(new[] { "always|50.00" },
					Describe(Fees.GetListExactAllDates(feeSched.FeeSchedNum,clinicB.ClinicNum,0)).Select(x => x.Substring(x.IndexOf('|')+1)).ToArray());
				Assert.AreEqual(1,Fees.GetListExactAllDates(feeSched.FeeSchedNum,clinicA.ClinicNum,0).Count);
				Assert.AreEqual(feeBaseA.FeeNum,Fees.GetListExactAllDates(feeSched.FeeSchedNum,clinicA.ClinicNum,0)[0].FeeNum);
			}
			finally {
				Prefs.UpdateBool(PrefName.ShowFeeSchedGroups,isShowGroupsOld);
			}
		}

		[TestMethod]
		public void Db_CopyFeeSchedule_WithDateKeepsEarlierRows() {
			FeeSched feeSchedFrom=NewFeeSched();
			FeeSched feeSchedTo=NewFeeSched();
			ProcedureCode procedureCode=NewCode();
			InsertFee(feeSchedFrom.FeeSchedNum,procedureCode.CodeNum,10,DateTime.MinValue);
			InsertFee(feeSchedFrom.FeeSchedNum,procedureCode.CodeNum,12,_date2025);//in effect today: this is what gets copied
			InsertFee(feeSchedFrom.FeeSchedNum,procedureCode.CodeNum,99,_date2099);
			InsertFee(feeSchedTo.FeeSchedNum,procedureCode.CodeNum,20,DateTime.MinValue);
			InsertFee(feeSchedTo.FeeSchedNum,procedureCode.CodeNum,25,_date2028);
			FeeScheds.CopyFeeSchedule(feeSchedFrom,0,0,feeSchedTo,null,0,_date2027);
			CollectionAssert.AreEqual(new[] { "0|always|20.00","0|2027-01-01|12.00" },Describe(Fees.GetListExactAllDates(feeSchedTo.FeeSchedNum,0,0)));
			//Without a date, everything of the "to" combination is replaced.
			FeeScheds.CopyFeeSchedule(feeSchedFrom,0,0,feeSchedTo,null,0);
			CollectionAssert.AreEqual(new[] { "0|always|12.00" },Describe(Fees.GetListExactAllDates(feeSchedTo.FeeSchedNum,0,0)));
		}

		[TestMethod]
		public void Db_CopyFeeSchedule_ToAGroupDoesNotDuplicate() {
			bool isShowGroupsOld=PrefC.GetBool(PrefName.ShowFeeSchedGroups);
			try {
				Prefs.UpdateBool(PrefName.ShowFeeSchedGroups,true);
				FeeSched feeSchedFrom=NewFeeSched();
				FeeSched feeSchedTo=NewFeeSched();
				ProcedureCode procedureCode=NewCode();
				Clinic clinicA=NewClinic();
				Clinic clinicB=NewClinic();
				FeeSchedGroups.Insert(new FeeSchedGroup { Description="FeeTest group "+_counter,FeeSchedNum=feeSchedTo.FeeSchedNum,
					ClinicNums=clinicA.ClinicNum+","+clinicB.ClinicNum });
				InsertFee(feeSchedFrom.FeeSchedNum,procedureCode.CodeNum,40,DateTime.MinValue);
				FeeScheds.CopyFeeSchedule(feeSchedFrom,0,0,feeSchedTo,new List<long> { clinicA.ClinicNum,clinicB.ClinicNum },0,_date2027);
				Assert.AreEqual(1,Fees.GetListExactAllDates(feeSchedTo.FeeSchedNum,clinicA.ClinicNum,0).Count);
				Assert.AreEqual(1,Fees.GetListExactAllDates(feeSchedTo.FeeSchedNum,clinicB.ClinicNum,0).Count);
				Assert.AreEqual(_date2027,Fees.GetListExactAllDates(feeSchedTo.FeeSchedNum,clinicB.ClinicNum,0)[0].DateEffective);
			}
			finally {
				Prefs.UpdateBool(PrefName.ShowFeeSchedGroups,isShowGroupsOld);
			}
		}

		[TestMethod]
		public void Db_ImportFees_DatedRowsLeaveCurrentFeesAlone() {
			FeeSched feeSched=NewFeeSched();
			ProcedureCode procedureCode=NewCode();
			ProcedureCode procedureCode2=NewCode();
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,30,DateTime.MinValue);
			List<string> listLines=new List<string> {
				procedureCode.ProcCode+"\t40.00\tA\tB",
				procedureCode.ProcCode+"\t45.00\tA\tB\t2028-01-01",
				procedureCode.ProcCode+"\t41.00\tA\tB",//same code and date again: the first line wins
				procedureCode2.ProcCode+"\t\tA\tB",//blank, nothing to delete
				"NOSUCHCODE\t1.00\tA\tB",
			};
			int countChanged=Fees.ImportFees(listLines,feeSched.FeeSchedNum,0,0,_date2027);
			Assert.AreEqual(2,countChanged);
			CollectionAssert.AreEqual(new[] { "0|always|30.00","0|2027-01-01|40.00","0|2028-01-01|45.00" },
				Describe(Fees.GetListExactAllDates(feeSched.FeeSchedNum,0,0)));
			//Importing again at the same date updates instead of inserting; a blank amount deletes only the row of that date.
			countChanged=Fees.ImportFees(new List<string> { procedureCode.ProcCode+"\t42.00\tA\tB",procedureCode.ProcCode+"\t\tA\tB\t" },
				feeSched.FeeSchedNum,0,0,_date2027);
			Assert.AreEqual(1,countChanged);//the second line has the same code and date as the first
			countChanged=Fees.ImportFees(new List<string> { procedureCode.ProcCode+"\t\tA\tB" },feeSched.FeeSchedNum,0,0,DateTime.MinValue);
			Assert.AreEqual(1,countChanged);
			CollectionAssert.AreEqual(new[] { "0|2027-01-01|42.00","0|2028-01-01|45.00" },Describe(Fees.GetListExactAllDates(feeSched.FeeSchedNum,0,0)));
		}

		[TestMethod]
		public void Db_SaveIncrease_NewEffectiveDateOrInPlace() {
			FeeSched feeSched=NewFeeSched();
			ProcedureCode procedureCode=NewCode();
			ProcedureCode procedureCode2=NewCode();
			ProcedureCode procedureCode3=NewCode();
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,100,DateTime.MinValue);
			InsertFee(feeSched.FeeSchedNum,procedureCode2.CodeNum,200,DateTime.MinValue);
			InsertFee(feeSched.FeeSchedNum,procedureCode3.CodeNum,-1,DateTime.MinValue);//blank override: left alone
			List<Fee> listFeesSaved=Fees.SaveIncrease(Fees.GetListExactAsOf(feeSched.FeeSchedNum,0,0,DateTime.Today),10,2,_date2027);
			Assert.AreEqual(2,listFeesSaved.Count);
			CollectionAssert.AreEqual(new[] { "always|100.00","2027-01-01|110.00" },Describe(Fees.GetListExactAllDates(feeSched.FeeSchedNum,0,0)
				.FindAll(x => x.CodeNum==procedureCode.CodeNum)).Select(x => x.Substring(x.IndexOf('|')+1)).ToArray());
			//Again from the same base with another percent: the 2027 rows are updated, not duplicated.
			Fees.SaveIncrease(Fees.GetListExactAsOf(feeSched.FeeSchedNum,0,0,DateTime.Today),20,2,_date2027);
			Assert.AreEqual(240,Fees.GetFee(procedureCode2.CodeNum,feeSched.FeeSchedNum,0,0,dateAsOf:_date2027).Amount);
			Assert.AreEqual(5,Fees.GetListExactAllDates(feeSched.FeeSchedNum,0,0).Count);
			//The base date picks the base rows: increasing the 2027 fees by 50% for 2028.
			Fees.SaveIncrease(Fees.GetListExactAsOf(feeSched.FeeSchedNum,0,0,_date2027),50,2,_date2028);
			Assert.AreEqual(180,Fees.GetFee(procedureCode.CodeNum,feeSched.FeeSchedNum,0,0,dateAsOf:_date2028).Amount);
			//No new date: in place, as before.
			Fees.SaveIncrease(Fees.GetListExactAsOf(feeSched.FeeSchedNum,0,0,DateTime.Today),5,2,DateTime.MinValue);
			Assert.AreEqual(105,Fees.GetFee(procedureCode.CodeNum,feeSched.FeeSchedNum,0,0).Amount);
			Assert.AreEqual(-1,Fees.GetFee(procedureCode3.CodeNum,feeSched.FeeSchedNum,0,0).Amount);
		}

		[TestMethod]
		public void Db_ExportWithFutureFees_ImportReproducesTheSchedule() {
			FeeSched feeSched=NewFeeSched();
			FeeSched feeSchedCopy=NewFeeSched();
			ProcedureCode procedureCode=NewCode();
			ProcedureCode procedureCode2=NewCode();
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,100,DateTime.MinValue);
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,110,_date2025);
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,150,_date2099);
			InsertFee(feeSched.FeeSchedNum,procedureCode2.CodeNum,50,DateTime.MinValue);
			string path=Path.Combine(Path.GetTempPath(),"FeeTests_export_"+Guid.NewGuid().ToString("N")+".txt");
			try {
				FeeScheds.ExportFeeSchedule(feeSched.FeeSchedNum,0,0,path,doIncludeFutureFees:true);
				List<string> listLines=File.ReadAllLines(path).ToList();
				List<string> listLinesCode=listLines.FindAll(x => x.StartsWith(procedureCode.ProcCode+"\t"));
				Assert.AreEqual(2,listLinesCode.Count);
				Assert.IsTrue(listLinesCode[0].EndsWith("\t2025-01-01"),listLinesCode[0]);
				Assert.IsTrue(listLinesCode[1].EndsWith("\t2099-01-01"),listLinesCode[1]);
				Assert.IsTrue(listLines.Single(x => x.StartsWith(procedureCode2.ProcCode+"\t")).EndsWith("\t"),"always-row has a blank date");
				Fees.ImportFees(listLines,feeSchedCopy.FeeSchedNum,0,0,DateTime.MinValue);
				foreach(DateTime dateAsOf in new[] { DateTime.Today,_date2099 }) {
					Assert.AreEqual(Fees.GetAmount(procedureCode.CodeNum,feeSched.FeeSchedNum,0,0,dateAsOf:dateAsOf),
						Fees.GetAmount(procedureCode.CodeNum,feeSchedCopy.FeeSchedNum,0,0,dateAsOf:dateAsOf));
					Assert.AreEqual(Fees.GetAmount(procedureCode2.CodeNum,feeSched.FeeSchedNum,0,0,dateAsOf:dateAsOf),
						Fees.GetAmount(procedureCode2.CodeNum,feeSchedCopy.FeeSchedNum,0,0,dateAsOf:dateAsOf));
				}
				//Without the option, the old four-column format with only today's fees.
				FeeScheds.ExportFeeSchedule(feeSched.FeeSchedNum,0,0,path);
				string line=File.ReadAllLines(path).Single(x => x.StartsWith(procedureCode.ProcCode+"\t"));
				Assert.AreEqual(4,line.Split('\t').Length);
				Assert.AreEqual(110,double.Parse(line.Split('\t')[1]));
			}
			finally {
				File.Delete(path);
			}
		}

		[TestMethod]
		public void Db_GetProcFee_CompletedUsesProcDate_TreatmentPlannedUsesToday() {
			FeeSched feeSched=NewFeeSched();
			ProcedureCode procedureCode=NewCode();
			Provider provider=new Provider();
			provider.Abbr="FTP"+(++_counter);
			provider.LName="Fictional";
			provider.FName="Provider"+_counter;
			provider.FeeSched=feeSched.FeeSchedNum;
			Providers.Insert(provider);
			Providers.RefreshCache();
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,50,DateTime.MinValue);
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,60,_date2025);
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,90,_date2099);
			Patient patient=new Patient { PriProv=provider.ProvNum };
			Procedure procedure=new Procedure { CodeNum=procedureCode.CodeNum,ProvNum=provider.ProvNum,ProcStatus=ProcStat.C,
				ProcDate=new DateTime(2024,6,1),DateComplete=DateTime.Today };
			double procFee=Procedures.GetProcFee(patient,new List<PatPlan>(),new List<InsSub>(),new List<InsPlan>(),procedure);
			Assert.AreEqual(50,procFee);
			procedure.ProcStatus=ProcStat.TP;
			procedure.DateComplete=DateTime.MinValue;
			procFee=Procedures.GetProcFee(patient,new List<PatPlan>(),new List<InsSub>(),new List<InsPlan>(),procedure);
			Assert.AreEqual(60,procFee);
		}

		[TestMethod]
		public void Db_DatabaseMaintenance_DatedRowsAreNotDuplicates() {
			FeeSched feeSched=NewFeeSched();
			ProcedureCode procedureCode=NewCode();
			ProcedureCode procedureCode2=NewCode();
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,100,DateTime.MinValue);
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,110,_date2027);
			//A real duplicate (same key and date), only possible by writing SQL directly.
			string insert="INSERT INTO fee (Amount,OldCode,FeeSched,UseDefaultFee,UseDefaultCov,CodeNum,ClinicNum,ProvNum,SecUserNumEntry,SecDateEntry,DateEffective) "
				+"VALUES (7,'',"+feeSched.FeeSchedNum+",0,0,"+procedureCode2.CodeNum+",0,0,0,CURDATE(),'2027-01-01')";
			DataCore.NonQ(insert);
			DataCore.NonQ(insert);
			DatabaseMaintenances.FeeDeleteDuplicates(false,DbmMode.Fix);
			Assert.AreEqual(2,Fees.GetListExactAllDates(feeSched.FeeSchedNum,0,0).FindAll(x => x.CodeNum==procedureCode.CodeNum).Count);
			Assert.AreEqual(1,Fees.GetListExactAllDates(feeSched.FeeSchedNum,0,0).FindAll(x => x.CodeNum==procedureCode2.CodeNum).Count);
		}

		[TestMethod]
		public void Db_ProcCodesReport_ShowsTheFeeInEffectToday() {
			FeeSched feeSched=NewFeeSched();
			ProcedureCode procedureCode=NewCode();
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,100,DateTime.MinValue);
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,110,_date2025);
			InsertFee(feeSched.FeeSchedNum,procedureCode.CodeNum,150,_date2099);
			DataTable table=RpProcCodes.GetDataSet(feeSched.FeeSchedNum,0,0);
			List<DataRow> listRows=table.Select().Where(x => x["ProcCode"].ToString()==procedureCode.ProcCode).ToList();
			Assert.AreEqual(1,listRows.Count);
			Assert.AreEqual(110,PIn.Double(listRows[0]["Amount"].ToString()));
		}

		[AssemblyCleanup]
		public static void AssemblyCleanup() {
			TestDb.DropIfBuilt();
		}
	}
}
