// Database tests for package M2-CARRIER on a scratch database (see ScratchDb): preauthorization needs read from the database, the appointment
// table's PreAuthFlag columns, Carrier Combine, the carrier field in the insurance edit log, and long insurance edit log values written and read
// back. Synthetic rows only, with made-up "Z" procedure codes. Skipped (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2CARRIERTests {
	[TestClass]
	public class CarrierPreAuthDbTests {
		private static readonly string DB_NAME=ScratchDb.NewName();
		private static string _skipReason;
		//A date far from anything in the seed dump, so these rows are the only appointments on it.
		private static readonly DateTime _date=new DateTime(2031,5,6);
		private static long _opNum;
		private static long _codeNumExam;
		private static long _codeNumCrown;
		private static long _codeNumCleaning;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			try {
				Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
				ScratchDb.Create(DB_NAME);
			}
			catch(Exception ex) {
				_skipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
				return;
			}
			_opNum=Operatories.Insert(new Operatory { OpName="M2C Op",Abbrev="M2C",ItemOrder=90 });
			_codeNumExam=AddProcCode("Z7001","Synthetic exam");
			_codeNumCrown=AddProcCode("Z7012","Synthetic crown");
			_codeNumCleaning=AddProcCode("Z7100","Synthetic cleaning");
			Cache.Refresh(InvalidType.Operatories,InvalidType.ProcCodes);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("M2CARRIER_TEST_KEEP_DB")=="1") {
				return;
			}
			try {
				ScratchDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//Harmless to leave behind; the name is unique to this run.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ScratchDb.Connect(DB_NAME);
			Security.CurUser=new Userod { UserNum=1,UserName="m2carrier" };
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			//Every test starts with no carrier list in use; each test sets the lists it needs.
			DataCore.NonQ("UPDATE carrier SET PreAuthCodes=''");
			Carriers.RefreshCache();
		}

		#region Helpers
		private static long AddProcCode(string procCode,string descript) {
			return ProcedureCodes.Insert(new ProcedureCode { ProcCode=procCode,Descript=descript,AbbrDesc=procCode,ProcTime="/X/",
				DefaultNote="",DefaultClaimNote="",DefaultTPNote="",AlternateCode1="",MedicalCode="",SubstitutionCode="",LaymanTerm="",
				CanadaTimeUnits=1,BaseUnits=0,DrugNDC="",RevenueCodeDefault="",DiagnosticCodes="",PaintText="",TaxCode="" });
		}

		private static Carrier AddCarrier(string name,string preAuthCodes,Color colorAppt) {
			Carrier carrier=new Carrier { CarrierName=name,Address="",Address2="",City="",State="",Zip="",Phone="",ElectID="",CDAnetVersion="",TIN="",
				PreAuthCodes=preAuthCodes,ApptTextBackColor=colorAppt };
			Carriers.Insert(carrier);
			Carriers.RefreshCache();
			return carrier;
		}

		///<summary>A patient with one plan of the carrier passed in, as primary. Returns the patient; the plan and subscription come back as out.</summary>
		private static Patient AddInsuredPatient(Carrier carrier,out InsPlan insPlan,out InsSub insSub,out PatPlan patPlan) {
			Patient patient=new Patient { LName="Preauth",FName="Synthetic",PatStatus=PatientStatus.Patient,Birthdate=new DateTime(1980,2,3) };
			Patients.Insert(patient,false);
			insPlan=new InsPlan { CarrierNum=carrier.CarrierNum,GroupName="Synthetic Group",GroupNum="SG-1",PlanNote="",PlanType="",ClaimFormNum=0 };
			InsPlans.Insert(insPlan);
			insSub=new InsSub { PlanNum=insPlan.PlanNum,Subscriber=patient.PatNum,SubscriberID="SYN0001",SubscNote="",BenefitNotes="" };
			InsSubs.Insert(insSub);
			patPlan=new PatPlan { PatNum=patient.PatNum,InsSubNum=insSub.InsSubNum,Ordinal=1,Relationship=Relat.Self,PatID="" };
			patPlan.PatPlanNum=PatPlans.Insert(patPlan);
			return patient;
		}

		private static Procedure AddProc(long patNum,long codeNum,ProcStat procStat,long aptNum=0) {
			Procedure procedure=new Procedure { PatNum=patNum,CodeNum=codeNum,ProcStatus=procStat,ProcDate=_date,DateTP=_date,ProcFee=100,ToothNum="",
				Surf="",ToothRange="",AptNum=aptNum,ProvNum=1,ClinicNum=0 };
			procedure.ProcNum=OpenDentBusiness.Crud.ProcedureCrud.Insert(procedure);
			return procedure;
		}

		private static Appointment AddAppt(long patNum,int hour) {
			Appointment appointment=new Appointment { PatNum=patNum,AptStatus=ApptStatus.Scheduled,AptDateTime=_date.AddHours(hour),Op=_opNum,
				Pattern="XXXX",PatternSecondary="",ProvNum=1,ProcDescript="",Note="",ProvBarText="",ProcsColored="",ColorOverride=Color.Empty };
			Appointments.Insert(appointment);
			return appointment;
		}

		private static void AddPreauthClaimProc(Procedure procedure,InsPlan insPlan,InsSub insSub) {
			OpenDentBusiness.Crud.ClaimProcCrud.Insert(new ClaimProc { ProcNum=procedure.ProcNum,PatNum=procedure.PatNum,PlanNum=insPlan.PlanNum,
				InsSubNum=insSub.InsSubNum,Status=ClaimProcStatus.Preauth,ProcDate=_date,CodeSent="",Remarks="",DateCP=_date });
		}
		#endregion Helpers

		#region Needs read from the database
		[TestMethod]
		public void Needs_ReadFromTheDatabaseAndClearedByAPreauth() {
			Carrier carrier=AddCarrier("Synthetic Preauth Dental","Z7001,Z7010-Z7019",Color.Black);
			Patient patient=AddInsuredPatient(carrier,out InsPlan insPlan,out InsSub insSub,out PatPlan patPlan);
			Procedure procedureExam=AddProc(patient.PatNum,_codeNumExam,ProcStat.TP);
			Procedure procedureCrown=AddProc(patient.PatNum,_codeNumCrown,ProcStat.TPi);
			Procedure procedureCleaning=AddProc(patient.PatNum,_codeNumCleaning,ProcStat.TP);
			Procedure procedureDone=AddProc(patient.PatNum,_codeNumExam,ProcStat.C);
			List<long> listProcNums=new List<long> { procedureExam.ProcNum,procedureCrown.ProcNum,procedureCleaning.ProcNum,procedureDone.ProcNum };
			List<PreAuthNeed> listPreAuthNeeds=CarrierPreAuths.GetNeedsForProcNums(listProcNums);
			CollectionAssert.AreEquivalent(new[] { procedureExam.ProcNum,procedureCrown.ProcNum },listPreAuthNeeds.Select(x => x.ProcNum).ToList());
			PreAuthNeed preAuthNeed=listPreAuthNeeds.Single(x => x.ProcNum==procedureCrown.ProcNum);
			Assert.AreEqual("Z7012",preAuthNeed.ProcCode);
			Assert.AreEqual(insPlan.PlanNum,preAuthNeed.PlanNum);
			Assert.AreEqual(insSub.InsSubNum,preAuthNeed.InsSubNum);
			Assert.AreEqual(patPlan.PatPlanNum,preAuthNeed.PatPlanNum);
			Assert.AreEqual("Synthetic Preauth Dental",preAuthNeed.CarrierName);
			AddPreauthClaimProc(procedureExam,insPlan,insSub);
			CollectionAssert.AreEqual(new[] { procedureCrown.ProcNum },CarrierPreAuths.GetNeedsForProcNums(listProcNums).Select(x => x.ProcNum).ToList(),
				"A preauth claimproc, sent or not, clears the flag.");
		}

		[TestMethod]
		public void Needs_NoneWhenNoCarrierHasAList() {
			Carrier carrier=AddCarrier("Synthetic Plain Dental","",Color.Black);
			Patient patient=AddInsuredPatient(carrier,out _,out _,out _);
			Procedure procedure=AddProc(patient.PatNum,_codeNumExam,ProcStat.TP);
			Assert.IsFalse(CarrierPreAuths.IsInUse());
			Assert.AreEqual(0,CarrierPreAuths.GetNeedsForProcNums(new List<long> { procedure.ProcNum }).Count);
		}

		[TestMethod]
		public void Needs_ForAppointmentsAndPlannedAppointments() {
			Carrier carrier=AddCarrier("Synthetic Appt Dental","Z7012",Color.Black);
			Patient patient=AddInsuredPatient(carrier,out _,out _,out _);
			Appointment appointment=AddAppt(patient.PatNum,9);
			Procedure procedure=AddProc(patient.PatNum,_codeNumCrown,ProcStat.TP,appointment.AptNum);
			List<PreAuthNeed> listPreAuthNeeds=CarrierPreAuths.GetNeedsForAppointments(new List<long> { appointment.AptNum,0 },false);
			Assert.AreEqual(1,listPreAuthNeeds.Count);
			Assert.AreEqual(procedure.ProcNum,listPreAuthNeeds[0].ProcNum);
			Assert.AreEqual(appointment.AptNum,listPreAuthNeeds[0].AptNum);
			Assert.AreEqual(0,CarrierPreAuths.GetNeedsForAppointments(new List<long> { appointment.AptNum },true).Count,"Not attached as a planned appointment.");
			DataCore.NonQ("UPDATE procedurelog SET PlannedAptNum="+POut.Long(appointment.AptNum)+" WHERE ProcNum="+POut.Long(procedure.ProcNum));
			Assert.AreEqual(1,CarrierPreAuths.GetNeedsForAppointments(new List<long> { appointment.AptNum },true).Count);
		}
		#endregion Needs read from the database

		#region Appointment table
		[TestMethod]
		public void ApptTable_FlagCodesAndCarrierColor() {
			Color colorCarrier=Color.FromArgb(255,255,220,120);
			Carrier carrier=AddCarrier("Synthetic Colored Dental","Z7010-Z7019,Z7001",colorCarrier);
			Patient patient=AddInsuredPatient(carrier,out InsPlan insPlan,out InsSub insSub,out _);
			Appointment appointmentFlagged=AddAppt(patient.PatNum,8);
			AddProc(patient.PatNum,_codeNumCrown,ProcStat.TP,appointmentFlagged.AptNum);
			AddProc(patient.PatNum,_codeNumExam,ProcStat.TP,appointmentFlagged.AptNum);
			AddProc(patient.PatNum,_codeNumCleaning,ProcStat.TP,appointmentFlagged.AptNum);
			Appointment appointmentPlain=AddAppt(patient.PatNum,11);
			AddProc(patient.PatNum,_codeNumCleaning,ProcStat.TP,appointmentPlain.AptNum);
			Appointment appointmentPreauthed=AddAppt(patient.PatNum,13);
			Procedure procedurePreauthed=AddProc(patient.PatNum,_codeNumCrown,ProcStat.TP,appointmentPreauthed.AptNum);
			AddPreauthClaimProc(procedurePreauthed,insPlan,insSub);
			DataTable table=Appointments.GetPeriodApptsTable(_date,_date,0,false,null,new List<long> { _opNum });
			DataRow dataRowFlagged=GetRow(table,appointmentFlagged.AptNum);
			Assert.AreEqual("PreAuth",dataRowFlagged[Appointments.PreAuthFlagColumn].ToString());
			CollectionAssert.AreEquivalent(new[] { "Z7012","Z7001" },dataRowFlagged[Appointments.PreAuthCodesColumn].ToString().Split(','));
			Assert.AreEqual(colorCarrier.ToArgb().ToString(),dataRowFlagged[Appointments.PreAuthColorColumn].ToString());
			foreach(Appointment appointment in new[] { appointmentPlain,appointmentPreauthed }) {
				DataRow dataRow=GetRow(table,appointment.AptNum);
				Assert.AreEqual("",dataRow[Appointments.PreAuthFlagColumn].ToString());
				Assert.AreEqual("",dataRow[Appointments.PreAuthCodesColumn].ToString());
				Assert.AreEqual("",dataRow[Appointments.PreAuthColorColumn].ToString());
			}
			//The single-appointment form of the table (used to redraw one appointment) carries the flag too.
			DataTable tableOne=Appointments.GetPeriodApptsTable(DateTime.MinValue,DateTime.MinValue,appointmentFlagged.AptNum,false);
			Assert.AreEqual("PreAuth",GetRow(tableOne,appointmentFlagged.AptNum)[Appointments.PreAuthFlagColumn].ToString());
		}

		[TestMethod]
		public void ApptTable_ColumnsPresentAndEmptyWhenNotInUse() {
			Carrier carrier=AddCarrier("Synthetic Unused Dental","",Color.FromArgb(255,10,200,10));
			Patient patient=AddInsuredPatient(carrier,out _,out _,out _);
			Appointment appointment=AddAppt(patient.PatNum,15);
			AddProc(patient.PatNum,_codeNumCrown,ProcStat.TP,appointment.AptNum);
			DataTable table=Appointments.GetPeriodApptsTable(_date,_date,0,false,null,new List<long> { _opNum });
			Assert.IsTrue(table.Columns.Contains(Appointments.PreAuthFlagColumn));
			Assert.IsTrue(table.Columns.Contains(Appointments.PreAuthCodesColumn));
			Assert.IsTrue(table.Columns.Contains(Appointments.PreAuthColorColumn));
			Assert.IsTrue(table.Rows.Cast<DataRow>().All(x => x[Appointments.PreAuthFlagColumn].ToString()==""));
		}

		[TestMethod]
		public void ApptTable_BlackCarrierColorMeansNoHighlight() {
			Carrier carrier=AddCarrier("Synthetic Black Dental","Z7012",Color.Black);
			Patient patient=AddInsuredPatient(carrier,out _,out _,out _);
			Appointment appointment=AddAppt(patient.PatNum,16);
			AddProc(patient.PatNum,_codeNumCrown,ProcStat.TP,appointment.AptNum);
			DataRow dataRow=GetRow(Appointments.GetPeriodApptsTable(DateTime.MinValue,DateTime.MinValue,appointment.AptNum,false),appointment.AptNum);
			Assert.AreEqual("PreAuth",dataRow[Appointments.PreAuthFlagColumn].ToString());
			Assert.AreEqual("",dataRow[Appointments.PreAuthColorColumn].ToString());
		}

		private static DataRow GetRow(DataTable table,long aptNum) {
			return table.Rows.Cast<DataRow>().Single(x => PIn.Long(x["AptNum"].ToString())==aptNum);
		}
		#endregion Appointment table

		#region Carriers and the insurance edit log
		[TestMethod]
		public void CarrierEdit_ListChangeIsLoggedLikeOtherFields() {
			Carrier carrier=AddCarrier("Synthetic Logged Dental","",Color.Black);
			Carrier carrierOld=carrier.Copy();
			carrier.PreAuthCodes="Z7001,Z7010-Z7019";
			Carriers.Update(carrier,carrierOld);
			Assert.AreEqual("Z7001,Z7010-Z7019",Carriers.GetCarrierDB(carrier.CarrierNum).PreAuthCodes);
			DataTable table=DataCore.GetTable("SELECT OldValue,NewValue,OldValueBig,NewValueBig FROM inseditlog WHERE LogType="+POut.Int((int)InsEditLogType.Carrier)
				+" AND FKey="+POut.Long(carrier.CarrierNum)+" AND FieldName='PreAuthCodes'");
			Assert.AreEqual(1,table.Rows.Count);
			Assert.AreEqual("",table.Rows[0]["OldValue"].ToString());
			Assert.AreEqual("Z7001,Z7010-Z7019",table.Rows[0]["NewValue"].ToString());
			//A list longer than 255 characters is logged in full, in NewValueBig.
			carrierOld=carrier.Copy();
			carrier.PreAuthCodes=string.Join(",",Enumerable.Range(7200,60).Select(x => "Z"+x));
			Carriers.Update(carrier,carrierOld);
			table=DataCore.GetTable("SELECT OldValue,NewValue,OldValueBig,NewValueBig FROM inseditlog WHERE LogType="+POut.Int((int)InsEditLogType.Carrier)
				+" AND FKey="+POut.Long(carrier.CarrierNum)+" AND FieldName='PreAuthCodes' ORDER BY InsEditLogNum");
			Assert.AreEqual(2,table.Rows.Count);
			Assert.AreEqual("Z7001,Z7010-Z7019",table.Rows[1]["OldValue"].ToString());
			Assert.AreEqual("",table.Rows[1]["NewValue"].ToString());
			Assert.AreEqual(carrier.PreAuthCodes,table.Rows[1]["NewValueBig"].ToString());
		}

		[TestMethod]
		public void CarrierCombine_KeptCarrierGetsTheUnion() {
			Carrier carrierKept=AddCarrier("Synthetic Combine Dental","Z7001,Z7010-Z7019",Color.Black);
			Carrier carrierOther=AddCarrier("Synthetic Combine Dental","Z7012,Z7100",Color.Black);
			Carrier carrierBlank=AddCarrier("Synthetic Combine Dental","",Color.Black);
			Carriers.Combine(new List<long> { carrierKept.CarrierNum,carrierOther.CarrierNum,carrierBlank.CarrierNum },carrierKept.CarrierNum);
			Carriers.RefreshCache();
			Assert.AreEqual("Z7001,Z7010-Z7019,Z7012,Z7100",Carriers.GetCarrierDB(carrierKept.CarrierNum).PreAuthCodes);
			Assert.IsNull(Carriers.GetCarrierDB(carrierOther.CarrierNum));
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM inseditlog WHERE LogType="+POut.Int((int)InsEditLogType.Carrier)
				+" AND FKey="+POut.Long(carrierKept.CarrierNum)+" AND FieldName='PreAuthCodes'"));
		}

		[TestMethod]
		public void CarrierCombine_NothingChangesWhenTheOthersHaveNoList() {
			Carrier carrierKept=AddCarrier("Synthetic Combine Two","Z7001",Color.Black);
			Carrier carrierOther=AddCarrier("Synthetic Combine Two","",Color.Black);
			Carriers.Combine(new List<long> { carrierKept.CarrierNum,carrierOther.CarrierNum },carrierKept.CarrierNum);
			Assert.AreEqual("Z7001",Carriers.GetCarrierDB(carrierKept.CarrierNum).PreAuthCodes);
			Assert.AreEqual("0",DataCore.GetScalar("SELECT COUNT(*) FROM inseditlog WHERE LogType="+POut.Int((int)InsEditLogType.Carrier)
				+" AND FKey="+POut.Long(carrierKept.CarrierNum)+" AND FieldName='PreAuthCodes'"));
		}

		[TestMethod]
		public void InsEditLog_LongPlanNoteIsKeptInFull() {
			Carrier carrier=AddCarrier("Synthetic Note Dental","",Color.Black);
			AddInsuredPatient(carrier,out InsPlan insPlan,out _,out _);
			InsPlan insPlanOld=insPlan.Copy();
			string noteLong=string.Join(" ",Enumerable.Range(1,120).Select(x => "word"+x));//Well over 255 characters.
			insPlan.PlanNote=noteLong;
			InsPlans.Update(insPlan,insPlanOld);
			InsPlan insPlanOld2=insPlan.Copy();
			insPlan.PlanNote="Short note";
			InsPlans.Update(insPlan,insPlanOld2);
			DataTable table=DataCore.GetTable("SELECT OldValue,NewValue,OldValueBig,NewValueBig FROM inseditlog WHERE LogType="+POut.Int((int)InsEditLogType.InsPlan)
				+" AND FKey="+POut.Long(insPlan.PlanNum)+" AND FieldName='PlanNote' ORDER BY InsEditLogNum");
			Assert.AreEqual(2,table.Rows.Count);
			Assert.AreEqual("",table.Rows[0]["OldValue"].ToString());
			Assert.AreEqual("",table.Rows[0]["NewValue"].ToString());
			Assert.AreEqual(noteLong,table.Rows[0]["NewValueBig"].ToString());
			Assert.AreEqual("",table.Rows[1]["OldValue"].ToString());
			Assert.AreEqual(noteLong,table.Rows[1]["OldValueBig"].ToString());
			Assert.AreEqual("Short note",table.Rows[1]["NewValue"].ToString());
			Assert.AreEqual("",table.Rows[1]["NewValueBig"].ToString());
			//The log window's data: whichever column is filled.
			List<InsEditLog> listInsEditLogs=InsEditLogs.GetLogsForPlan(insPlan.PlanNum,insPlan.CarrierNum,insPlan.EmployerNum).FindAll(x => x.FieldName=="PlanNote");
			Assert.AreEqual(2,listInsEditLogs.Count);
			Assert.AreEqual(noteLong,InsEditLogBigValues.GetNewValue(listInsEditLogs[0]));
			Assert.AreEqual(noteLong,InsEditLogBigValues.GetOldValue(listInsEditLogs[1]));
			Assert.AreEqual("Short note",InsEditLogBigValues.GetNewValue(listInsEditLogs[1]));
		}

		[TestMethod]
		public void InsEditPatLog_LongSubscriberNoteIsKeptInFull() {
			Carrier carrier=AddCarrier("Synthetic Subscriber Dental","",Color.Black);
			AddInsuredPatient(carrier,out _,out InsSub insSub,out PatPlan patPlan);
			InsSub insSubOld=insSub.Copy();
			string noteLong=new string('n',700);
			insSub.SubscNote=noteLong;
			InsSubs.Update(insSub);
			InsEditPatLogs.MakeLogEntry(insSub,insSubOld,InsEditPatLogType.Subscriber);
			DataTable table=DataCore.GetTable("SELECT OldValue,NewValue,OldValueBig,NewValueBig FROM inseditpatlog WHERE LogType="
				+POut.Int((int)InsEditPatLogType.Subscriber)+" AND FKey="+POut.Long(insSub.InsSubNum)+" AND FieldName='SubscNote'");
			Assert.AreEqual(1,table.Rows.Count);
			Assert.AreEqual("",table.Rows[0]["NewValue"].ToString());
			Assert.AreEqual(noteLong,table.Rows[0]["NewValueBig"].ToString());
			Assert.AreEqual("",table.Rows[0]["OldValueBig"].ToString());
			InsEditPatLog insEditPatLog=InsEditPatLogs.GetLogsForPatPlan(patPlan.PatPlanNum,insSub.InsSubNum).Single(x => x.FieldName=="SubscNote");
			Assert.AreEqual(noteLong,InsEditLogBigValues.GetNewValue(insEditPatLog));
		}
		#endregion Carriers and the insurance edit log
	}
}
