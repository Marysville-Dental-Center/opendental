// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using SyntheticData.Infrastructure;

namespace SyntheticData.Generation {
	///<summary>Simulates one family over the history window: members, insurance, visits, procedures, claims, payments, adjustments,
	///payment plans, statements and aging, plus the per-patient records of the 24.4 to 25.3 features. Each family draws from its own
	///random stream, so families do not depend on each other except for appointment slots and insurance-check batches.</summary>
	public partial class FamilySimulator {
		private readonly World _world;
		private readonly DataSink _sink;
		private readonly Rng _rng;
		private readonly int _familyIndex;
		private readonly List<Person> _listMembers=new List<Person>();
		private Person _guarantor;
		private PayerKind _payerKind;
		private readonly List<ProcRecord> _listProcs=new List<ProcRecord>();
		private readonly List<LedgerEntry> _listLedger=new List<LedgerEntry>();
		///<summary>Insurance estimates per claim: when the claim was made, when it was paid (null while outstanding), and the estimate.</summary>
		private readonly List<(DateTime dateClaim,DateTime? dateReceived,decimal estimate)> _listInsTimeline=new List<(DateTime,DateTime?,decimal)>();
		private long _billingType;
		private int _statementMode;
		private ProviderInfo _dentist;
		private ProviderInfo _hygienist;
		private static readonly DateTime _dateMirroringStarted=new DateTime(2025,9,1);
		private static readonly DateTime _dateChartLettersStarted=new DateTime(2024,12,1);
		///<summary>Family 0 is the showcase family: its random choices are steered so that every 24.4 to 25.3 feature gets at least one
		///row in any size and seed (pre-authorized crown on a payment plan with a down payment and a chosen sheet, signed consent,
		///chart letter, eForms with Ins Pick and pending insurance, task category, BetterDiagnostics drawing, hidden medication, card on
		///file, bounced statement e-mail). The data stays consistent; only the dice are loaded.</summary>
		private readonly bool _isShowcase;
		private static readonly DateTime _dateShowcaseFirstVisit=new DateTime(2025,8,4);

		private FamilySimulator(World world,int familyIndex) {
			_world=world;
			_sink=world.Sink;
			_familyIndex=familyIndex;
			_rng=Rng.Derive(world.Settings.Seed,"family:"+familyIndex);
			_isShowcase=familyIndex==0;
		}

		///<summary>A random event that always happens in the showcase family.</summary>
		private bool Chance(double probability) {
			bool roll=_rng.Chance(probability);
			return _isShowcase || roll;
		}

		private DateTime AsOf {
			get { return _world.AsOf; }
		}

		public static void RunAll(World world,Action<int,int> progress=null) {
			int count=world.Settings.FamilyCount;
			for(int i=0;i<count;i++) {
				new FamilySimulator(world,i).Run();
				progress?.Invoke(i+1,count);
			}
		}

		private void Run() {
			AssignFamilyAddress();
			CreateMembers();
			CreateInsurance();
			foreach(Person person in _listMembers) {
				SimulateClinical(person);
			}
			CreatePaymentPlans();
			FollowUpPayments();
			FinanceCharges();
			WriteProcedures();
			WriteStatements();
			WritePatients();
			WriteExtras();
		}

		#region Members
		private void CreateMembers() {
			int size=_isShowcase ? 5 : _rng.PickWeighted(new List<(int,double)> { (1,0.38),(2,0.24),(3,0.16),(4,0.15),(5,0.07) });
			string surname=NameBank.Surname(_rng);
			_dentist=_rng.Pick(_world.ListDentists);
			_hygienist=_rng.Pick(_world.ListHygienists);
			_payerKind=_rng.PickWeighted(new List<(PayerKind,double)> { (PayerKind.Prompt,0.74),(PayerKind.Slow,0.17),(PayerKind.NonPayer,0.09) });
			if(_isShowcase) {
				_payerKind=PayerKind.Slow;
			}
			_billingType=_world.Def(OdValues.DefBillingTypes,"Standard Account");
			_statementMode=_rng.PickWeighted(new List<(int,double)> { (OdValues.StmtMail,0.55),(OdValues.StmtEmail,0.38),(OdValues.StmtInPerson,0.04),
				(OdValues.StmtElectronic,0.02),(OdValues.StmtApi,0.01) });
			int guarantorAge=_rng.PickWeighted(new List<(int,double)> { (_rng.Between(19,29),0.15),(_rng.Between(30,49),0.4),(_rng.Between(50,64),0.25),
				(_rng.Between(65,90),0.2) });
			if(_isShowcase) {
				guarantorAge=52;
				_statementMode=OdValues.StmtEmail;
			}
			Person guarantor=NewPerson(surname,guarantorAge);
			guarantor.IsGuarantor=true;
			_guarantor=guarantor;
			bool hasSpouse=size>=2 && guarantorAge>=22 && Chance(0.7);
			guarantor.Position=hasSpouse ? OdValues.PosMarried
				: guarantorAge>70 && _rng.Chance(0.4) ? OdValues.PosWidowed
				: _rng.Chance(0.2) ? OdValues.PosDivorced : OdValues.PosSingle;
			_listMembers.Add(guarantor);
			if(hasSpouse) {
				Person spouse=NewPerson(_rng.Chance(0.85) ? surname : NameBank.Surname(_rng),Math.Max(19,guarantorAge+_rng.Between(-6,6)));
				spouse.Position=OdValues.PosMarried;
				spouse.Gender=guarantor.Gender==OdValues.GenderFemale ? OdValues.GenderMale : _rng.Chance(0.9) ? OdValues.GenderFemale : OdValues.GenderMale;
				_listMembers.Add(spouse);
			}
			while(_listMembers.Count<size) {
				int maxChildAge=Math.Max(1,Math.Min(24,guarantorAge-18));
				int childAge=_rng.Between(1,maxChildAge);
				if(_isShowcase) {
					//An adult child past the dependent age (no coverage, reports insurance on the eForm) and two young children.
					childAge=new[] { 0,0,27,11,7 }[_listMembers.Count];
				}
				Person child=NewPerson(surname,childAge);
				child.Position=OdValues.PosChild;
				_listMembers.Add(child);
			}
			//Some guarantors only pay for their children and are not patients themselves.
			if(_listMembers.Any(x => x.Position==OdValues.PosChild) && _rng.Chance(0.07) && !_isShowcase) {
				guarantor.Status=OdValues.PatNonPatient;
			}
			foreach(Person person in _listMembers) {
				person.PatNum=_sink.NextId("patient");
				AssignStatusAndDates(person);
			}
			AssignContactDetails();
		}

		private Person NewPerson(string surname,int age) {
			DateTime birthdate=AsOf.AddYears(-age).AddDays(-_rng.Next(365));
			Person person=new Person {
				LName=surname,
				FName=NameBank.GivenName(_rng),
				MiddleI=NameBank.MiddleInitial(_rng),
				Birthdate=birthdate,
				Gender=_rng.Chance(0.5) ? OdValues.GenderFemale : _rng.Chance(0.97) ? OdValues.GenderMale : OdValues.GenderUnknown,
				Position=OdValues.PosSingle,
				Status=OdValues.PatPatient,
				Dentist=_dentist,
				Hygienist=_hygienist,
			};
			person.Preferred=_rng.Chance(0.08) ? person.FName.Substring(0,Math.Min(4,person.FName.Length)) : "";
			return person;
		}

		private void AssignStatusAndDates(Person person) {
			DateTime historyStart=_world.Settings.HistoryStart;
			int earliestDays=Math.Max(30,(person.AgeOn(AsOf)-1)*365);
			int daysBack=_rng.Chance(0.3) ? _rng.Between(20,(AsOf-historyStart).Days) : _rng.Between(30,Math.Min(earliestDays,365*8));
			person.DateFirstVisit=AsOf.AddDays(-daysBack);
			if(person.DateFirstVisit<person.Birthdate.AddYears(1)) {
				person.DateFirstVisit=person.Birthdate.AddYears(1);
			}
			person.LastAllowedVisit=AsOf;
			if(person.Status==OdValues.PatNonPatient) {
				return;
			}
			double roll=_rng.NextDouble();
			if(_isShowcase) {
				roll=1;
				if(person.IsGuarantor || person.AgeOn(AsOf)>=26) {
					person.DateFirstVisit=Max(historyStart.AddDays(10),_dateShowcaseFirstVisit);
				}
			}
			if(roll<0.07) {
				person.Status=OdValues.PatInactive;
			}
			else if(roll<0.10) {
				person.Status=OdValues.PatArchived;
			}
			else if(roll<0.11 && person.AgeOn(AsOf)>70) {
				person.Status=OdValues.PatDeceased;
			}
			else if(roll<0.115 && !person.IsGuarantor) {
				person.Status=OdValues.PatDeleted;
			}
			else if(roll<0.13 && person.IsGuarantor && _listMembers.Count==1) {
				person.Status=OdValues.PatProspective;
				person.DateFirstVisit=new DateTime(1,1,1);
			}
			if(person.Status==OdValues.PatInactive || person.Status==OdValues.PatArchived || person.Status==OdValues.PatDeceased) {
				person.LastAllowedVisit=AsOf.AddDays(-_rng.Between(240,600));
				if(person.DateFirstVisit>person.LastAllowedVisit) {
					person.DateFirstVisit=person.LastAllowedVisit.AddDays(-_rng.Between(200,900));
				}
				if(person.Status==OdValues.PatDeceased) {
					person.DateTimeDeceased=person.LastAllowedVisit.AddDays(_rng.Between(30,200)).AddHours(6);
				}
			}
		}

		///<summary>E-mail addresses are unique per active patient, because the office schema's generated column
		///_portal_active_email has a unique key over active patients' addresses. Inactive members may share the guarantor's address,
		///which that key allows (the generated value is NULL for them).</summary>
		private void AssignContactDetails() {
			foreach(Person person in _listMembers) {
				int age=person.AgeOn(AsOf);
				bool isAdult=age>=18;
				if(person.IsGuarantor ? Chance(0.88) : isAdult && _rng.Chance(0.6)) {
					person.Email=NameBank.Email(person.FName,person.LName,person.PatNum);
				}
				if(isAdult && _rng.Chance(0.8)) {
					person.WirelessPhone=NameBank.Phone(_rng);
				}
			}
			if(_guarantor.Email!="" && _guarantor.Status==OdValues.PatPatient) {
				foreach(Person person in _listMembers.Where(x => !x.IsGuarantor && x.Status!=OdValues.PatPatient && x.Email=="")) {
					if(_rng.Chance(0.6)) {
						person.Email=_guarantor.Email;
					}
				}
			}
		}
		#endregion Members

		#region Insurance
		private void CreateInsurance() {
			if(!Chance(0.66)) {
				return;
			}
			Person spouse=_listMembers.FirstOrDefault(x => !x.IsGuarantor && x.Position==OdValues.PosMarried);
			InsPlanInfo planGuarantor=_rng.Pick(_world.ListPlans);
			if(_isShowcase) {
				planGuarantor=_world.ListPlans.FirstOrDefault(x => x.IsPpo)??planGuarantor;
			}
			(long insSubNum,DateTime dateEffective) subGuarantor=AddInsSub(planGuarantor,_guarantor);
			(long insSubNum,DateTime dateEffective) subSpouse=(0,DateTime.MinValue);
			InsPlanInfo planSpouse=null;
			if(spouse!=null && Chance(0.3)) {
				planSpouse=_rng.Pick(_world.ListPlans);
				subSpouse=AddInsSub(planSpouse,spouse);
			}
			foreach(Person person in _listMembers) {
				if(person.Status==OdValues.PatNonPatient) {
					continue;
				}
				if(person==spouse && planSpouse!=null) {
					AddPatPlan(person,planSpouse,subSpouse,1,OdValues.RelatSelf,spouse.PatNum);
					AddPatPlan(person,planGuarantor,subGuarantor,2,OdValues.RelatSpouse,_guarantor.PatNum);
					continue;
				}
				int relationship=person.IsGuarantor ? OdValues.RelatSelf : person==spouse ? OdValues.RelatSpouse : OdValues.RelatChild;
				if(relationship==OdValues.RelatChild && person.AgeOn(AsOf)>=26) {
					continue;
				}
				AddPatPlan(person,planGuarantor,subGuarantor,1,relationship,_guarantor.PatNum);
				if(planSpouse!=null && relationship==OdValues.RelatChild) {
					AddPatPlan(person,planSpouse,subSpouse,2,OdValues.RelatChild,spouse.PatNum);
				}
			}
		}

		private (long,DateTime) AddInsSub(InsPlanInfo plan,Person subscriber) {
			long insSubNum=_sink.NextId("inssub");
			DateTime dateEffective=new DateTime(Math.Min(_world.Settings.HistoryStart.Year,subscriber.DateFirstVisit.Year<1900 ? AsOf.Year : subscriber.DateFirstVisit.Year)
				-_rng.Next(3),_rng.Between(1,12),1);
			if(_rng.Chance(0.15) && !_isShowcase) {
				//InsCoverage that started recently: crown waiting periods apply.
				dateEffective=AsOf.AddMonths(-_rng.Between(2,10));
				dateEffective=new DateTime(dateEffective.Year,dateEffective.Month,1);
			}
			DateTime secDateEntry=dateEffective.AddDays(_rng.Between(0,20));
			_sink.Insert("inssub",new Row {
				["InsSubNum"]=insSubNum,
				["PlanNum"]=plan.PlanNum,
				["Subscriber"]=subscriber.PatNum,
				["DateEffective"]=dateEffective,
				["ReleaseInfo"]=true,
				["AssignBen"]=true,
				["SubscriberID"]="SYN"+_rng.Between(100000000,999999999),
				["BenefitNotes"]=_rng.Chance(0.2) ? "Benefits verified by phone; frequency on file." : "",
				["SubscNote"]="",
				["SecUserNumEntry"]=_rng.Pick(_world.ListFrontDeskUsers),
				["SecDateEntry"]=secDateEntry,
				["SecDateTEdit"]=secDateEntry.AddHours(10),
				//25.2 column. Placeholder value; see World.PseudoHash.
				["SecurityHash"]=secDateEntry.Year>=2025 || _isShowcase ? World.PseudoHash("inssub"+insSubNum) : "",
			});
			if(_rng.Chance(0.4)) {
				AddInsVerify(OdValues.VerifyInsuranceBenefit,plan.PlanNum);
			}
			return (insSubNum,dateEffective);
		}

		private void AddPatPlan(Person person,InsPlanInfo plan,(long insSubNum,DateTime dateEffective) sub,int ordinal,int relationship,long subscriberPatNum) {
			long patPlanNum=_sink.NextId("patplan");
			_sink.Insert("patplan",new Row {
				["PatPlanNum"]=patPlanNum,
				["PatNum"]=person.PatNum,
				["Ordinal"]=ordinal,
				["IsPending"]=false,
				["Relationship"]=relationship,
				["PatID"]="",
				["InsSubNum"]=sub.insSubNum,
				["SecDateTEntry"]=sub.dateEffective.AddHours(11),
				["SecDateTEdit"]=sub.dateEffective.AddHours(11),
			});
			person.ListCoverages.Add(new InsCoverage { Plan=plan,InsSubNum=sub.insSubNum,PatPlanNum=patPlanNum,Ordinal=ordinal,Relationship=relationship,
				SubscriberPatNum=subscriberPatNum,DateEffective=sub.dateEffective });
			if(_rng.Chance(0.03)) {
				//A patient-level override of the annual maximum.
				PracticeGenerator.AddBenefit(_world,plan.PlanNum,0,OdValues.BenLimitations,-1,plan.AnnualMax+500,OdValues.TimeCalendarYear,OdValues.QtyNone,0,
					OdValues.CovIndividual,patPlanNum:patPlanNum);
			}
			if(_rng.Chance(0.5)) {
				AddInsVerify(OdValues.VerifyPatientEnrollment,patPlanNum);
			}
		}

		///<summary>Insurance verification with its 25.1 SecurityHash, and a history row for some.</summary>
		private void AddInsVerify(int verifyType,long fKey) {
			long insVerifyNum=_sink.NextId("insverify");
			DateTime dateVerified=AsOf.AddDays(-_rng.Between(1,200));
			long userNum=_rng.Pick(_world.ListFrontDeskUsers);
			long defNum=_world.Def(OdValues.DefInsuranceVerificationStatus,_rng.Chance(0.85) ? "Verified" : "Needs Follow-up");
			_sink.Insert("insverify",new Row {
				["InsVerifyNum"]=insVerifyNum,["DateLastVerified"]=dateVerified,["UserNum"]=userNum,["VerifyType"]=verifyType,["FKey"]=fKey,
				["DefNum"]=defNum,["Note"]="",["DateLastAssigned"]=dateVerified.AddDays(-2),["DateTimeEntry"]=dateVerified.AddDays(-30).AddHours(8),
				["HoursAvailableForVerification"]=0.0,["SecDateTEdit"]=dateVerified.AddHours(14),["SecurityHash"]=World.PseudoHash("insverify"+insVerifyNum),
			});
			if(_rng.Chance(0.5)) {
				long histNum=_sink.NextId("insverifyhist");
				_sink.Insert("insverifyhist",new Row {
					["InsVerifyHistNum"]=histNum,["InsVerifyNum"]=insVerifyNum,["DateLastVerified"]=dateVerified,["UserNum"]=userNum,["VerifyType"]=verifyType,
					["FKey"]=fKey,["DefNum"]=defNum,["Note"]="Verified online",["DateLastAssigned"]=dateVerified.AddDays(-2),
					["DateTimeEntry"]=dateVerified.AddHours(14),["HoursAvailableForVerification"]=0.0,["VerifyUserNum"]=userNum,
					["SecDateTEdit"]=dateVerified.AddHours(14),["SecurityHash"]=World.PseudoHash("insverifyhist"+histNum),
				});
			}
		}
		#endregion Insurance

		#region Clinical
		private void SimulateClinical(Person person) {
			if(person.Status==OdValues.PatNonPatient || person.Status==OdValues.PatDeleted) {
				return;
			}
			if(person.Status==OdValues.PatProspective) {
				DateTime desired=AsOf.AddDays(_rng.Between(3,60));
				List<ProcRecord> listPlanned=new List<ProcRecord> {
					NewProc(person,"Z0150",person.Dentist.ProvNum,desired),
					NewProc(person,"Z0210",person.Dentist.ProvNum,desired),
				};
				BookFuture(person,listPlanned,desired,isHygiene:false,isNewPatient:true);
				return;
			}
			DateTime historyStart=_world.Settings.HistoryStart;
			bool isNewPatient=person.DateFirstVisit>=historyStart;
			DateTime date=isNewPatient ? person.DateFirstVisit : historyStart.AddDays(_rng.Next(170));
			if(!isNewPatient) {
				AddExistingConditions(person);
			}
			else if(person.ListCoverages.Count>0 && _rng.Chance(0.25)) {
				AddInsuranceHistory(person);
			}
			bool isFirst=true;
			while(date<=person.LastAllowedVisit) {
				if(_rng.Chance(0.04)) {
					BookBroken(person,date.AddDays(-_rng.Between(10,40)));
				}
				DateTime? visit=HygieneVisit(person,date,isFirst && isNewPatient);
				if(visit==null) {
					date=date.AddDays(21);
					continue;
				}
				isFirst=false;
				List<ProcRecord> listFindings=Findings(person,visit.Value);
				if(_isShowcase && person.IsGuarantor && listFindings.Count(x => x.Code.IsProsth)==0 && visit.Value<AsOf.AddDays(-150)) {
					string tooth=new[] { "30","3","14","19" }[_listProcs.Count(x => x.Patient==person && x.Code.Code=="Z2740")%4];
					foreach(string code in new[] { "Z2950","Z2740" }) {
						ProcRecord crown=NewProc(person,code,person.Dentist.ProvNum,visit.Value);
						crown.ToothNum=tooth;
						listFindings.Add(crown);
					}
				}
				if(listFindings.Count>0) {
					HandleTreatment(person,listFindings,visit.Value);
				}
				DateTime next=visit.Value.AddMonths(person.IsPerio ? 4 : 6).AddDays(_rng.Between(-10,25));
				if(_rng.Chance(0.05)) {
					DateTime dateEmergency=visit.Value.AddDays(_rng.Between(20,Math.Max(21,(next-visit.Value).Days-10)));
					if(dateEmergency<=person.LastAllowedVisit) {
						EmergencyVisit(person,dateEmergency);
					}
				}
				date=next;
			}
			if(person.Status==OdValues.PatPatient) {
				person.NextHygieneDue=date;
				if(date>AsOf && date<=_world.Settings.FutureEnd && _rng.Chance(0.85)) {
					List<ProcRecord> listPlanned=HygieneProcs(person,date,false);
					person.NextHygieneAptNum=BookFuture(person,listPlanned,date,isHygiene:true,isNewPatient:false);
				}
				else if(date<=AsOf && _rng.Chance(0.3)) {
					//Overdue for recall and on the unscheduled list.
					WriteAppointment(person,_sink.NextId("appointment"),OdValues.AptUnschedList,new DateTime(1,1,1),0,person.Dentist.ProvNum,
						person.Hygienist.ProvNum,true,"/XXXXXX/","","Recall overdue",false,"Will call when schedule allows",false);
				}
			}
		}

		private List<ProcRecord> HygieneProcs(Person person,DateTime date,bool isNewPatient) {
			int age=person.AgeOn(date);
			long dentist=person.Dentist.ProvNum;
			long hygienist=person.Hygienist.ProvNum;
			List<ProcRecord> listProcs=new List<ProcRecord>();
			bool bitewingsDue=person.LastBitewings==null || (date-person.LastBitewings.Value).Days>=330;
			if(isNewPatient) {
				listProcs.Add(NewProc(person,"Z0150",dentist,date));
				listProcs.Add(NewProc(person,age>=12 ? "Z0210" : "Z0272",dentist,date));
			}
			else {
				listProcs.Add(NewProc(person,person.IsPerio ? "Z0180" : "Z0120",dentist,date));
				if(bitewingsDue) {
					listProcs.Add(NewProc(person,age>=12 ? "Z0274" : "Z0272",dentist,date));
				}
				else if(age>=18 && _rng.Chance(0.04)) {
					listProcs.Add(NewProc(person,"Z0330",dentist,date));
				}
			}
			if(age<14) {
				listProcs.Add(NewProc(person,"Z1120",hygienist,date));
				listProcs.Add(NewProc(person,"Z1206",hygienist,date));
			}
			else {
				listProcs.Add(NewProc(person,person.IsPerio ? "Z4910" : "Z1110",hygienist,date));
			}
			if(age>=6 && age<16 && _rng.Chance(0.12)) {
				ProcRecord sealant=NewProc(person,"Z1351",hygienist,date);
				sealant.ToothNum=_rng.Pick(new[] { "3","14","19","30" });
				listProcs.Add(sealant);
			}
			return listProcs;
		}

		private DateTime? HygieneVisit(Person person,DateTime desired,bool isNewPatient) {
			List<ProcRecord> listProcs=HygieneProcs(person,desired,isNewPatient);
			bool hasExam=listProcs.Any(x => x.Code.Code.StartsWith("Z01"));
			(string pattern,string patternSecondary)=BuildPattern(listProcs,true);
			(DateTime dateTime,ProviderInfo opProvider)? booking=Book(person.Hygienist,_world.ListHygienists,desired,pattern.Length,_rng.Next(SlotBook.SlotsPerDay-pattern.Length));
			if(booking==null) {
				return null;
			}
			DateTime aptDateTime=booking.Value.dateTime;
			if(aptDateTime.Date>person.LastAllowedVisit || aptDateTime.Date>AsOf) {
				return null;
			}
			foreach(ProcRecord proc in listProcs) {
				if(proc.ProvNum==person.Hygienist.ProvNum) {
					proc.ProvNum=booking.Value.opProvider.ProvNum;
				}
				RedateProc(proc,aptDateTime.Date);
			}
			bool isMirrored=hasExam && aptDateTime>=_dateMirroringStarted && Chance(0.6);
			long aptNum=_sink.NextId("appointment");
			WriteAppointment(person,aptNum,OdValues.AptComplete,aptDateTime,booking.Value.opProvider.OpNum,person.Dentist.ProvNum,booking.Value.opProvider.ProvNum,
				true,pattern,hasExam ? patternSecondary : "",string.Join(", ",listProcs.Select(x => x.Describe())),isNewPatient,"",isMirrored);
			CompleteVisit(person,listProcs,aptNum,aptDateTime.Date);
			person.LastHygieneVisit=aptDateTime.Date;
			if(listProcs.Any(x => x.Code.Code=="Z0272" || x.Code.Code=="Z0274" || x.Code.Code=="Z0210")) {
				person.LastBitewings=aptDateTime.Date;
			}
			return aptDateTime.Date;
		}

		private void EmergencyVisit(Person person,DateTime desired) {
			List<ProcRecord> listProcs=new List<ProcRecord> {
				NewProc(person,"Z0140",person.Dentist.ProvNum,desired),
				NewProc(person,"Z0220",person.Dentist.ProvNum,desired),
				NewProc(person,"Z9110",person.Dentist.ProvNum,desired),
			};
			string tooth=_rng.Pick(new[] { "3","14","19","30","8","12" });
			listProcs[1].ToothNum=tooth;
			(string pattern,string _)=BuildPattern(listProcs,false);
			(DateTime dateTime,ProviderInfo opProvider)? booking=Book(person.Dentist,_world.ListDentists,desired,pattern.Length,_rng.Next(SlotBook.SlotsPerDay-pattern.Length));
			if(booking==null || booking.Value.dateTime.Date>AsOf) {
				return;
			}
			foreach(ProcRecord proc in listProcs) {
				proc.ProvNum=booking.Value.opProvider.ProvNum;
				RedateProc(proc,booking.Value.dateTime.Date);
			}
			long aptNum=_sink.NextId("appointment");
			WriteAppointment(person,aptNum,OdValues.AptComplete,booking.Value.dateTime,booking.Value.opProvider.OpNum,booking.Value.opProvider.ProvNum,0,false,
				pattern,"",string.Join(", ",listProcs.Select(x => x.Describe())),false,"Emergency: tooth pain #"+tooth,false);
			CompleteVisit(person,listProcs,aptNum,booking.Value.dateTime.Date);
		}

		private void BookBroken(Person person,DateTime desired) {
			if(desired<_world.Settings.HistoryStart || desired>AsOf) {
				return;
			}
			(DateTime dateTime,ProviderInfo opProvider)? booking=Book(person.Hygienist,_world.ListHygienists,desired,12,_rng.Next(SlotBook.SlotsPerDay-12));
			if(booking==null || booking.Value.dateTime.Date>AsOf) {
				return;
			}
			long aptNum=_sink.NextId("appointment");
			WriteAppointment(person,aptNum,OdValues.AptBroken,booking.Value.dateTime,booking.Value.opProvider.OpNum,person.Dentist.ProvNum,
				booking.Value.opProvider.ProvNum,true,"/XXXXXXXXXX/","","RecExam, AdClean",false,"Did not show",false);
			AddCommlog(person,booking.Value.dateTime.AddHours(2),"Appointment Related",OdValues.CommModePhone,OdValues.CommSent,
				"Patient missed the appointment. Left a message to reschedule.");
			AddHistAppointment(person,aptNum,OdValues.HistMissed,booking.Value.dateTime.AddHours(1),OdValues.AptBroken,booking.Value.dateTime,
				booking.Value.opProvider.OpNum,"RecExam, AdClean",false);
		}

		///<summary>Treatment found at an exam, as treatment-planned procedures.</summary>
		private List<ProcRecord> Findings(Person person,DateTime date) {
			List<ProcRecord> listFindings=new List<ProcRecord>();
			int age=person.AgeOn(date);
			long dentist=person.Dentist.ProvNum;
			string[] arrayPosterior={"2","3","4","5","12","13","14","15","18","19","20","21","28","29","30","31"};
			string[] arrayAnterior={"6","7","8","9","10","11","22","23","24","25","26","27"};
			if(age<14) {
				if(_rng.Chance(0.1)) {
					ProcRecord proc=NewProc(person,"Z2391",dentist,date);
					proc.ToothNum=_rng.Pick(new[] { "A","B","I","J","K","L","S","T" });
					proc.Surf="O";
					listFindings.Add(proc);
				}
				if(_rng.Chance(0.01)) {
					ProcRecord proc=NewProc(person,"Z3220",dentist,date);
					proc.ToothNum=_rng.Pick(new[] { "A","J","K","T" });
					listFindings.Add(proc);
				}
				if(age>=12 && _rng.Chance(0.01)) {
					listFindings.Add(NewProc(person,"Z8080",dentist,date));
				}
				return listFindings;
			}
			if(age>=12 && age<=16 && _rng.Chance(0.02)) {
				listFindings.Add(NewProc(person,"Z8080",dentist,date));
			}
			if(_rng.Chance(0.25)) {
				int count=_rng.Between(1,3);
				for(int i=0;i<count;i++) {
					bool isPosterior=_rng.Chance(0.75);
					string[] arraySurfaces=isPosterior ? new[] { "O","MO","DO","MOD","MODB" } : new[] { "M","D","MF","DL" };
					string surf=_rng.Pick(arraySurfaces);
					string code=isPosterior
						? (_rng.Chance(0.15) ? (surf.Length==1 ? "Z2140" : "Z2150") : "Z239"+Math.Min(4,surf.Length))
						: (surf.Length==1 ? "Z2330" : "Z2331");
					ProcRecord proc=NewProc(person,code,dentist,date);
					proc.ToothNum=_rng.Pick(isPosterior ? arrayPosterior : arrayAnterior);
					proc.Surf=surf;
					listFindings.Add(proc);
				}
			}
			if(_rng.Chance(0.06)) {
				string tooth=_rng.Pick(arrayPosterior);
				if(_rng.Chance(0.3)) {
					ProcRecord rct=NewProc(person,_rng.Chance(0.5) ? "Z3330" : "Z3320",dentist,date);
					rct.ToothNum=tooth;
					listFindings.Add(rct);
				}
				ProcRecord buildup=NewProc(person,"Z2950",dentist,date);
				buildup.ToothNum=tooth;
				listFindings.Add(buildup);
				ProcRecord crown=NewProc(person,_rng.Chance(0.6) ? "Z2740" : "Z2750",dentist,date);
				crown.ToothNum=tooth;
				listFindings.Add(crown);
			}
			if(_rng.Chance(0.025)) {
				bool isThirdMolar=_rng.Chance(0.4);
				ProcRecord extraction=NewProc(person,isThirdMolar || _rng.Chance(0.3) ? "Z7210" : "Z7140",dentist,date);
				extraction.ToothNum=isThirdMolar ? _rng.Pick(new[] { "1","16","17","32" }) : _rng.Pick(arrayPosterior);
				listFindings.Add(extraction);
			}
			if(age>=25 && _rng.Chance(0.012)) {
				string tooth=_rng.Pick(new[] { "3","14","19","30" });
				ProcRecord implant=NewProc(person,"Z6010",dentist,date);
				implant.ToothNum=tooth;
				listFindings.Add(implant);
				ProcRecord implantCrown=NewProc(person,"Z6065",dentist,date);
				implantCrown.ToothNum=tooth;
				listFindings.Add(implantCrown);
			}
			if(!person.IsPerio && age>=30 && _rng.Chance(0.03)) {
				foreach(string quad in new[] { "UR","UL","LR","LL" }.Take(_rng.Between(2,4))) {
					ProcRecord srp=NewProc(person,"Z4341",person.Hygienist.ProvNum,date);
					srp.Surf=quad;
					listFindings.Add(srp);
				}
				person.IsPerio=true;
			}
			if(age>=55 && _rng.Chance(0.01)) {
				listFindings.Add(NewProc(person,"Z5213",dentist,date));
			}
			if(_rng.Chance(0.012)) {
				listFindings.Add(NewProc(person,"Z9944",dentist,date));
			}
			if(age>=18 && _rng.Chance(0.02)) {
				listFindings.Add(NewProc(person,"Z9972",dentist,date));
			}
			return listFindings;
		}

		private void HandleTreatment(Person person,List<ProcRecord> listFindings,DateTime dateExam) {
			foreach(ProcRecord proc in listFindings) {
				proc.Status=OdValues.ProcTP;
				proc.DateTP=dateExam;
				_listProcs.Add(proc);
			}
			if(person.ListCoverages.Count>0 && listFindings.Any(x => x.Code.IsProsth || x.Code.Code=="Z8080") && Chance(0.5)) {
				CreatePreauth(person,listFindings.Where(x => x.Code.IsProsth || x.Code.Code=="Z8080" || x.Code.Code=="Z6010").ToList(),dateExam);
			}
			//Two-stage treatment: an implant crown or a crown after a root canal comes months later.
			List<ProcRecord> listFirst=listFindings.Where(x => x.Code.Code!="Z6065" && !(x.Code.IsProsth && listFindings.Any(y => y.Code.Code.StartsWith("Z33")))).ToList();
			List<ProcRecord> listLater=listFindings.Except(listFirst).ToList();
			ScheduleTreatment(person,listFirst,dateExam.AddDays(_rng.Between(7,42)));
			if(listLater.Count>0) {
				ScheduleTreatment(person,listLater,dateExam.AddDays(_rng.Between(70,120)));
			}
		}

		private void ScheduleTreatment(Person person,List<ProcRecord> listProcs,DateTime desired) {
			if(listProcs.Count==0) {
				return;
			}
			double roll=_rng.NextDouble();
			if(_isShowcase) {
				roll=0;
			}
			if(roll<0.72 && desired<=person.LastAllowedVisit && desired<=AsOf) {
				(string pattern,string _)=BuildPattern(listProcs,false);
				(DateTime dateTime,ProviderInfo opProvider)? booking=Book(person.Dentist,_world.ListDentists,desired,pattern.Length,_rng.Next(SlotBook.SlotsPerDay-pattern.Length));
				if(booking!=null && booking.Value.dateTime.Date<=AsOf) {
					foreach(ProcRecord proc in listProcs) {
						if(proc.ProvNum==person.Dentist.ProvNum) {
							proc.ProvNum=booking.Value.opProvider.ProvNum;
						}
						RedateProc(proc,booking.Value.dateTime.Date);
					}
					long aptNum=_sink.NextId("appointment");
					WriteAppointment(person,aptNum,OdValues.AptComplete,booking.Value.dateTime,booking.Value.opProvider.OpNum,booking.Value.opProvider.ProvNum,0,false,
						pattern,"",string.Join(", ",listProcs.Select(x => x.Describe())),false,"",false);
					CompleteVisit(person,listProcs,aptNum,booking.Value.dateTime.Date);
					return;
				}
			}
			if(roll<0.72 && desired>AsOf && desired<=_world.Settings.FutureEnd && person.Status==OdValues.PatPatient) {
				BookFuture(person,listProcs,desired,isHygiene:false,isNewPatient:false);
				return;
			}
			if(roll<0.86 && person.Status==OdValues.PatPatient) {
				//Planned appointment: treatment the office wants scheduled, without a date yet.
				long plannedAptNum=_sink.NextId("appointment");
				(string pattern,string _)=BuildPattern(listProcs,false);
				WriteAppointment(person,plannedAptNum,OdValues.AptPlanned,new DateTime(1,1,1),0,person.Dentist.ProvNum,0,false,pattern,"",
					string.Join(", ",listProcs.Select(x => x.Describe())),false,"",false);
				foreach(ProcRecord proc in listProcs) {
					proc.PlannedAptNum=plannedAptNum;
				}
			}
			if(_rng.Chance(0.02)) {
				//Occasionally the plan is abandoned and the procedure deleted.
				listProcs[0].Status=OdValues.ProcDeleted;
			}
		}

		///<summary>Books a scheduled appointment after the as-of date. The procedures stay treatment-planned and get insurance estimates.</summary>
		private long BookFuture(Person person,List<ProcRecord> listProcs,DateTime desired,bool isHygiene,bool isNewPatient) {
			(string pattern,string patternSecondary)=BuildPattern(listProcs,isHygiene);
			ProviderInfo provider=isHygiene ? person.Hygienist : person.Dentist;
			List<ProviderInfo> listProviders=isHygiene ? _world.ListHygienists : _world.ListDentists;
			if(desired<=AsOf) {
				desired=AsOf.AddDays(1);
			}
			(DateTime dateTime,ProviderInfo opProvider)? booking=Book(provider,listProviders,desired,pattern.Length,_rng.Next(SlotBook.SlotsPerDay-pattern.Length));
			if(booking==null) {
				//No room: treatment stays planned without an appointment; recall procedures are simply not created.
				if(!isHygiene && !isNewPatient) {
					foreach(ProcRecord proc in listProcs.Where(x => !_listProcs.Contains(x))) {
						proc.Status=OdValues.ProcTP;
						proc.DateTP=AsOf.AddDays(-_rng.Between(0,10));
						_listProcs.Add(proc);
					}
				}
				return 0;
			}
			long aptNum=_sink.NextId("appointment");
			bool hasExam=listProcs.Any(x => x.Code.Code.StartsWith("Z01"));
			bool isMirrored=isHygiene && hasExam && _rng.Chance(0.6);
			WriteAppointment(person,aptNum,OdValues.AptScheduled,booking.Value.dateTime,booking.Value.opProvider.OpNum,
				isHygiene ? person.Dentist.ProvNum : booking.Value.opProvider.ProvNum,isHygiene ? booking.Value.opProvider.ProvNum : 0,isHygiene,pattern,
				isHygiene && hasExam ? patternSecondary : "",string.Join(", ",listProcs.Select(x => x.Describe())),isNewPatient,"",isMirrored);
			foreach(ProcRecord proc in listProcs) {
				proc.Status=OdValues.ProcTP;
				if(proc.DateTP.Year<1900) {
					proc.DateTP=AsOf.AddDays(-_rng.Between(0,30));
				}
				proc.ProcDate=booking.Value.dateTime.Date;
				proc.AptNum=aptNum;
				if(!_listProcs.Contains(proc)) {
					_listProcs.Add(proc);
				}
			}
			person.DateTimeNextScheduled=booking.Value.dateTime;
			_world.ListFutureAppointments.Add((aptNum,person.PatNum,booking.Value.dateTime,booking.Value.opProvider.OpNum));
			return aptNum;
		}

		///<summary>Finds the first workday on or after the desired date (up to three weeks later) with room in the provider's operatory, then in
		///the other operatories of the same kind.</summary>
		private (DateTime,ProviderInfo)? Book(ProviderInfo preferred,List<ProviderInfo> listProviders,DateTime desired,int slotCount,int preferredSlot) {
			List<DateTime> listWorkdays=_world.ListWorkdays;
			int index=listWorkdays.BinarySearch(desired.Date);
			if(index<0) {
				index=~index;
			}
			List<ProviderInfo> listOrder=new List<ProviderInfo> { preferred };
			listOrder.AddRange(listProviders.Where(x => x!=preferred));
			for(int day=0;day<15 && index+day<listWorkdays.Count;day++) {
				DateTime date=listWorkdays[index+day];
				foreach(ProviderInfo provider in listOrder) {
					DateTime? start=_world.SlotBook.TryBook(provider.OpNum,date,slotCount,preferredSlot);
					if(start.HasValue) {
						return (start.Value,provider);
					}
				}
			}
			return null;
		}

		///<summary>Pattern and secondary pattern in 5-minute steps. On a hygiene appointment the dentist's exam and x-ray time shows as 'X'
		///in the secondary pattern (the part a mirror covers) and as '/' in the main pattern.</summary>
		private static (string pattern,string patternSecondary) BuildPattern(List<ProcRecord> listProcs,bool isHygiene) {
			string pattern="";
			string patternSecondary="";
			foreach(ProcRecord proc in listProcs) {
				string time=proc.Code.ProcTime;
				bool isDentistPart=isHygiene && !proc.Code.IsHygiene;
				if(isDentistPart) {
					pattern+=new string('/',time.Length);
					patternSecondary+=time;
				}
				else {
					pattern+=time;
					patternSecondary+=new string('/',time.Length);
				}
			}
			while(pattern.Length<6) {
				pattern+="/";
				patternSecondary+="/";
			}
			if(pattern.Length>36) {
				pattern=pattern.Substring(0,36);
				patternSecondary=patternSecondary.Substring(0,36);
			}
			return (pattern,patternSecondary);
		}

		private ProcRecord NewProc(Person person,string code,long provNum,DateTime date) {
			ProcRecord proc=new ProcRecord {
				ProcNum=_sink.NextId("procedurelog"),
				Patient=person,
				Code=_world.Code(code),
				ProvNum=provNum,
				ProcDate=date,
				DateTP=new DateTime(1,1,1),
				Status=OdValues.ProcTP,
			};
			proc.Fee=FeeFor(proc,date);
			return proc;
		}

		///<summary>Moves a procedure to its final date and re-reads the fee in effect on that date (dated fees, 24.4).</summary>
		private void RedateProc(ProcRecord proc,DateTime date) {
			proc.ProcDate=date;
			proc.Fee=FeeFor(proc,date);
		}

		private decimal FeeFor(ProcRecord proc,DateTime date) {
			return _world.GetFee(_world.FeeSchedStandard,proc.Code.CodeNum,proc.ProvNum,date)??proc.Code.Ucr;
		}

		///<summary>Existing work that came with patients who were seen before the history window: existing-other procedures, no fee.</summary>
		private void AddExistingConditions(Person person) {
			if(person.AgeOn(AsOf)<18 || !_rng.Chance(0.35)) {
				return;
			}
			int count=_rng.Between(1,4);
			for(int i=0;i<count;i++) {
				string code=_rng.Pick(new[] { "Z2392","Z2393","Z2750","Z3330","Z7140" });
				ProcRecord proc=NewProc(person,code,person.Dentist.ProvNum,person.DateFirstVisit);
				proc.Status=_rng.Chance(0.9) ? OdValues.ProcExistingOther : OdValues.ProcExistingCurrent;
				proc.Fee=0;
				proc.ToothNum=_rng.Pick(new[] { "2","3","14","15","18","19","30","31" });
				if(proc.Code.TreatArea==OdValues.AreaSurf) {
					proc.Surf="MO";
				}
				_listProcs.Add(proc);
			}
		}

		///<summary>Insurance history for a new insured patient: last bitewings taken elsewhere, recorded as an existing procedure with an
		///InsHist claimproc, so frequency checks know about it.</summary>
		private void AddInsuranceHistory(Person person) {
			InsCoverage coverage=person.ListCoverages.First();
			ProcRecord proc=NewProc(person,"Z0274",person.Dentist.ProvNum,person.DateFirstVisit.AddMonths(-_rng.Between(3,10)));
			proc.Status=OdValues.ProcExistingOther;
			proc.Fee=0;
			_listProcs.Add(proc);
			WriteClaimProc(new Row {
				["ClaimProcNum"]=_sink.NextId("claimproc"),["ProcNum"]=proc.ProcNum,["ClaimNum"]=0L,["PatNum"]=person.PatNum,["ProvNum"]=proc.ProvNum,
				["Status"]=OdValues.CpInsHist,["PlanNum"]=coverage.Plan.PlanNum,["InsSubNum"]=coverage.InsSubNum,["ProcDate"]=proc.ProcDate,
				["DateCP"]=proc.ProcDate,["DateEntry"]=person.DateFirstVisit,["SecDateEntry"]=person.DateFirstVisit,
			});
		}

		private void WriteAppointment(Person person,long aptNum,int aptStatus,DateTime aptDateTime,long opNum,long provNum,long provHyg,bool isHygiene,string pattern,
			string patternSecondary,string procDescript,bool isNewPatient,string note,bool isMirrored)
		{
			bool isComplete=aptStatus==OdValues.AptComplete;
			DateTime dateEntry=aptDateTime.Year>1900 ? aptDateTime.AddDays(-_rng.Between(7,120)).Date.AddHours(9).AddMinutes(_rng.Next(480)) : AsOf.AddDays(-_rng.Between(1,200)).AddHours(10);
			if(dateEntry<_world.Settings.HistoryStart.AddDays(-150)) {
				dateEntry=_world.Settings.HistoryStart.AddDays(-150).AddHours(10);
			}
			string confirmed=isComplete ? "Arrived" : aptStatus==OdValues.AptScheduled ? _rng.Pick(new[] { "Not Called","Left Message","Confirmed" }) : "Not Called";
			InsCoverage primary=person.ListCoverages.FirstOrDefault(x => x.Ordinal==1);
			InsCoverage secondary=person.ListCoverages.FirstOrDefault(x => x.Ordinal==2);
			long userNum=_rng.Pick(_world.ListFrontDeskUsers);
			_sink.Insert("appointment",new Row {
				["AptNum"]=aptNum,
				["PatNum"]=person.PatNum,
				["AptStatus"]=aptStatus,
				["Pattern"]=pattern,
				["Confirmed"]=_world.Def(OdValues.DefApptConfirmed,confirmed),
				["TimeLocked"]=false,
				["Op"]=opNum,
				["Note"]=note,
				["ProvNum"]=provNum,
				["ProvHyg"]=provHyg,
				["AptDateTime"]=aptDateTime,
				["IsNewPatient"]=isNewPatient,
				["ProcDescript"]=procDescript,
				["ClinicNum"]=_world.ClinicNum,
				["IsHygiene"]=isHygiene,
				["DateTStamp"]=isComplete ? aptDateTime.AddHours(2) : dateEntry,
				["DateTimeArrived"]=isComplete ? aptDateTime.AddMinutes(-_rng.Between(0,15)) : new DateTime(1,1,1),
				["DateTimeSeated"]=isComplete ? aptDateTime.AddMinutes(_rng.Between(0,10)) : new DateTime(1,1,1),
				["DateTimeDismissed"]=isComplete ? aptDateTime.AddMinutes(pattern.Length*5+_rng.Between(0,10)) : new DateTime(1,1,1),
				["InsPlan1"]=primary?.Plan.PlanNum??0,
				["InsPlan2"]=secondary?.Plan.PlanNum??0,
				["ProcsColored"]="",
				["SecUserNumEntry"]=userNum,
				["SecDateTEntry"]=dateEntry,
				["ProvBarText"]="",
				["PatternSecondary"]=patternSecondary,
				["SecurityHash"]=dateEntry.Year>=2025 ? World.PseudoHash("appointment"+aptNum) : "",
				["IsMirrored"]=isMirrored,
			});
			if(_rng.Chance(0.3) || isMirrored) {
				AddHistAppointment(person,aptNum,OdValues.HistCreated,dateEntry,OdValues.AptScheduled,aptDateTime,opNum,procDescript,isMirrored);
			}
			if(aptDateTime.Year>1900 && _rng.Chance(0.15)) {
				AddSecurityLog(OdValues.PermAppointmentCreate,userNum,dateEntry,person.PatNum,aptNum,
					"New appointment for "+aptDateTime.ToString("yyyy-MM-dd HH:mm")+" ("+procDescript+")");
			}
			if(aptStatus==OdValues.AptScheduled && _rng.Chance(0.35)) {
				AddCommlog(person,aptDateTime.AddDays(-2).Date.AddHours(10),"Appointment Related",OdValues.CommModeText,OdValues.CommSent,
					"Reminder sent for the appointment on "+aptDateTime.ToString("MMM d")+".");
			}
		}

		private void AddHistAppointment(Person person,long aptNum,int action,DateTime histDateTime,int aptStatus,DateTime aptDateTime,long opNum,string procDescript,
			bool isMirrored)
		{
			_sink.Insert("histappointment",new Row {
				["HistApptNum"]=_sink.NextId("histappointment"),
				["HistUserNum"]=_rng.Pick(_world.ListFrontDeskUsers),
				["HistDateTStamp"]=histDateTime,
				["HistApptAction"]=action,
				["ApptSource"]=0,
				["AptNum"]=aptNum,
				["PatNum"]=person.PatNum,
				["AptStatus"]=aptStatus,
				["Pattern"]="/XXXXXX/",
				["Op"]=opNum,
				["Note"]="",
				["ProvNum"]=person.Dentist.ProvNum,
				["AptDateTime"]=aptDateTime,
				//text since 25.2
				["ProcDescript"]=procDescript,
				["ProcsColored"]="",
				["DateTStamp"]=histDateTime,
				["ProvBarText"]="",
				["PatternSecondary"]="",
				["SecurityHash"]="",
				["IsMirrored"]=isMirrored,
			});
		}
		#endregion Clinical
	}
}
