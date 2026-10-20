// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using SyntheticData.Infrastructure;

namespace SyntheticData.Generation {
	///<summary>Rows that depend on all families: insurance checks, EOB attachments, bank deposits, the accounting ledger with check
	///numbers, payees, notes and review flags (25.3), reconciliations, the monthly A/R snapshot, and the 25.3 tables for features the fork
	///excludes (chat, dental school), which get a few rows to prove they survive adoption untouched.</summary>
	public static class PracticeWideGenerator {
		private static readonly DateTime _minDate=new DateTime(1,1,1);

		public static void Run(World world) {
			Rng rng=Rng.Derive(world.Settings.Seed,"practicewide");
			WriteClaimPayments(world,rng);
			WriteEobAttachments(world,rng);
			WriteDeposits(world);
			WriteAccounting(world,rng);
			WriteDashboardAr(world);
			WriteChat(world,rng);
			WriteSchool(world,rng);
			WriteMiscellaneous(world,rng);
		}

		private static void WriteClaimPayments(World world,Rng rng) {
			long checkType=world.Def(OdValues.DefInsurancePaymentType,"Insurance Check");
			long eftType=world.Def(OdValues.DefInsurancePaymentType,"Insurance EFT");
			foreach(ClaimPaymentBatch batch in world.DictClaimPayments.Values.OrderBy(x => x.ClaimPaymentNum)) {
				DepositBatch deposit=world.DepositFor(batch.CheckDate);
				if(deposit!=null) {
					deposit.InsuranceAmount+=batch.Amount;
					batch.DepositNum=deposit.DepositNum;
				}
				world.Sink.Insert("claimpayment",new Row {
					["ClaimPaymentNum"]=batch.ClaimPaymentNum,["CheckDate"]=batch.CheckDate,["CheckAmt"]=batch.Amount,
					["CheckNum"]=batch.IsEft ? "EFT"+batch.ClaimPaymentNum : rng.Between(100000,999999).ToString(),["BankBranch"]="",
					["Note"]=batch.Amount==0 ? "Zero payment: all procedures denied" : batch.IsEft ? "EFT trace "+rng.Between(10000000,99999999) : "",["ClinicNum"]=world.ClinicNum,["DepositNum"]=batch.DepositNum,
					["CarrierName"]=batch.CarrierName,["DateIssued"]=batch.CheckDate.AddDays(-3),["IsPartial"]=false,["PayType"]=batch.IsEft ? eftType : checkType,
					["SecUserNumEntry"]=rng.Pick(world.ListFrontDeskUsers),["SecDateEntry"]=batch.CheckDate,["SecDateTEdit"]=batch.CheckDate.AddHours(14),
					["PayGroup"]=0L,
				});
			}
		}

		///<summary>EOBs scanned onto insurance payments, and (25.2) onto preauthorizations through eobattach.ClaimNumPreAuth.</summary>
		private static void WriteEobAttachments(World world,Rng rng) {
			foreach(long claimPaymentNum in world.ListEobClaimPayments.Distinct()) {
				ClaimPaymentBatch batch=world.DictClaimPayments.Values.First(x => x.ClaimPaymentNum==claimPaymentNum);
				long eobNum=world.Sink.NextId("eobattach");
				world.Sink.Insert("eobattach",new Row { ["EobAttachNum"]=eobNum,["ClaimPaymentNum"]=claimPaymentNum,
					["DateTCreated"]=batch.CheckDate.AddHours(15),["FileName"]="EOB_"+eobNum+".pdf",["RawBase64"]="",["ClaimNumPreAuth"]=0L });
			}
			foreach((long claimNum,long patNum,long planNum,DateTime dateReceived) preauth in world.ListPreauthClaims) {
				long eobNum=world.Sink.NextId("eobattach");
				world.Sink.Insert("eobattach",new Row { ["EobAttachNum"]=eobNum,["ClaimPaymentNum"]=0L,["DateTCreated"]=preauth.dateReceived.AddHours(16),
					["FileName"]="PreauthEOB_"+eobNum+".pdf",["RawBase64"]="",["ClaimNumPreAuth"]=preauth.claimNum });
			}
		}

		private static void WriteDeposits(World world) {
			foreach(DepositBatch deposit in world.DictDeposits.Values.OrderBy(x => x.DepositNum)) {
				world.Sink.Insert("deposit",new Row {
					["DepositNum"]=deposit.DepositNum,["DateDeposit"]=deposit.DateDeposit,["BankAccountInfo"]="Operating Checking 000123456789",
					["Amount"]=deposit.PatientAmount+deposit.InsuranceAmount,["Memo"]="",["Batch"]="",["DepositAccountNum"]=0L,["IsSentToQuickBooksOnline"]=false,
				});
			}
		}

		private class AcctEntry {
			public DateTime Date;
			public long AccountNum;
			public decimal Debit;
			public decimal Credit;
			public string Memo;
			public string Splits;
			public string CheckNumber="";
			public string Payee="";
			public string Notes="";
			public bool NeedsReview;
			public long TransactionNum;
		}

		///<summary>Double-entry accounting: one transaction per deposit (checking against patient and insurance income) and monthly expense
		///checks with payees (25.3), notes and review flags. Checking entries are reconciled month by month until two months before the
		///as-of date.</summary>
		private static void WriteAccounting(World world,Rng rng) {
			List<AcctEntry> listEntries=new List<AcctEntry>();
			List<(long transactionNum,DateTime date,long depositNum,bool needsReview)> listTransactions=new List<(long,DateTime,long,bool)>();
			foreach(DepositBatch deposit in world.DictDeposits.Values.OrderBy(x => x.DateDeposit)) {
				decimal total=deposit.PatientAmount+deposit.InsuranceAmount;
				if(total<=0) {
					continue;
				}
				long transactionNum=world.Sink.NextId("transaction");
				listTransactions.Add((transactionNum,deposit.DateDeposit,deposit.DepositNum,false));
				listEntries.Add(new AcctEntry { Date=deposit.DateDeposit,AccountNum=world.AccountChecking,Debit=total,Memo="Deposit",Splits="Patient Fee Income, Insurance Income",
					TransactionNum=transactionNum });
				if(deposit.PatientAmount>0) {
					listEntries.Add(new AcctEntry { Date=deposit.DateDeposit,AccountNum=world.AccountPatientIncome,Credit=deposit.PatientAmount,Memo="Deposit",
						Splits="Operating Checking",TransactionNum=transactionNum });
				}
				if(deposit.InsuranceAmount>0) {
					listEntries.Add(new AcctEntry { Date=deposit.DateDeposit,AccountNum=world.AccountInsuranceIncome,Credit=deposit.InsuranceAmount,Memo="Deposit",
						Splits="Operating Checking",TransactionNum=transactionNum });
				}
			}
			Dictionary<string,(string payee,decimal min,decimal max)> dictExpenses=new Dictionary<string,(string,decimal,decimal)> {
				["Rent"]=("Ambervale Property Partners",4200,4200),
				["Dental Supplies"]=("Pebblecrest Dental Supply",1800,4200),
				["Laboratory Fees"]=("Lanternmere Dental Lab",900,3600),
				["Utilities"]=("Ambervale Public Utility",420,780),
				["Office Supplies"]=("Quillstead Office Goods",90,380),
			};
			//Expenses scale with the size of the practice (the ranges above fit about 600 families).
			decimal scale=Math.Max(0.05m,Math.Round(world.Settings.FamilyCount/600m,2));
			int checkNumber=5001;
			DateTime start=world.Settings.HistoryStart;
			for(DateTime month=new DateTime(start.Year,start.Month,1).AddMonths(1);month<=world.AsOf;month=month.AddMonths(1)) {
				foreach((long accountNum,string name) account in world.ListExpenseAccounts) {
					(string payee,decimal min,decimal max) expense=dictExpenses[account.name];
					DateTime date=month.AddDays(rng.Between(0,9));
					if(date>world.AsOf) {
						continue;
					}
					decimal amount=Math.Round((expense.min==expense.max ? expense.min : rng.Money(expense.min,expense.max))*scale,2);
					bool isRecent=(world.AsOf-date).Days<=45;
					bool needsReview=isRecent && rng.Chance(0.4);
					long transactionNum=world.Sink.NextId("transaction");
					listTransactions.Add((transactionNum,date,0,needsReview));
					string check=(checkNumber++).ToString();
					string notes=date>=new DateTime(2025,9,1) && rng.Chance(0.5) ? "Invoice "+rng.Between(10000,99999)+" approved by the office manager." : "";
					listEntries.Add(new AcctEntry { Date=date,AccountNum=world.AccountChecking,Credit=amount,Memo=account.name,Splits=account.name,CheckNumber=check,
						Payee=expense.payee,Notes=notes,NeedsReview=needsReview,TransactionNum=transactionNum });
					listEntries.Add(new AcctEntry { Date=date,AccountNum=account.accountNum,Debit=amount,Memo=account.name,Splits="Operating Checking",CheckNumber=check,
						Payee=expense.payee,Notes=notes,NeedsReview=needsReview,TransactionNum=transactionNum });
				}
			}
			foreach((long transactionNum,DateTime date,long depositNum,bool needsReview) transaction in listTransactions) {
				world.Sink.Insert("transaction",new Row {
					["TransactionNum"]=transaction.transactionNum,["DateTimeEntry"]=transaction.date.AddHours(16),["UserNum"]=world.UserNumAdmin,
					["DepositNum"]=transaction.depositNum,["PayNum"]=0L,["SecUserNumEdit"]=world.UserNumAdmin,["SecDateTEdit"]=transaction.date.AddHours(16),
					["TransactionInvoiceNum"]=0L,["NeedsReview"]=transaction.needsReview,
				});
			}
			//Monthly reconciliations of checking, up to two months before the as-of date.
			List<(long reconcileNum,DateTime date)> listReconciles=new List<(long,DateTime)>();
			for(DateTime monthEnd=new DateTime(start.Year,start.Month,1).AddMonths(2).AddDays(-1);monthEnd<=world.AsOf.AddMonths(-2);
				monthEnd=monthEnd.AddDays(1).AddMonths(1).AddDays(-1))
			{
				listReconciles.Add((world.Sink.NextId("reconcile"),monthEnd));
			}
			decimal balance=0;
			int reconcileIndex=0;
			decimal startingBalance=0;
			foreach(AcctEntry entry in listEntries.OrderBy(x => x.Date).ThenBy(x => x.TransactionNum)) {
				long reconcileNum=0;
				if(entry.AccountNum==world.AccountChecking) {
					while(reconcileIndex<listReconciles.Count && entry.Date>listReconciles[reconcileIndex].date) {
						world.Sink.Insert("reconcile",new Row { ["ReconcileNum"]=listReconciles[reconcileIndex].reconcileNum,["AccountNum"]=world.AccountChecking,
							["StartingBal"]=startingBalance,["EndingBal"]=balance,["DateReconcile"]=listReconciles[reconcileIndex].date,["IsLocked"]=true });
						startingBalance=balance;
						reconcileIndex++;
					}
					if(reconcileIndex<listReconciles.Count) {
						reconcileNum=listReconciles[reconcileIndex].reconcileNum;
					}
					balance+=entry.Debit-entry.Credit;
				}
				world.Sink.Insert("journalentry",new Row {
					["JournalEntryNum"]=world.Sink.NextId("journalentry"),["TransactionNum"]=entry.TransactionNum,["AccountNum"]=entry.AccountNum,
					["DateDisplayed"]=entry.Date,["DebitAmt"]=entry.Debit,["CreditAmt"]=entry.Credit,["Memo"]=entry.Memo,["Splits"]=entry.Splits,
					["CheckNumber"]=entry.CheckNumber,["ReconcileNum"]=reconcileNum,["SecUserNumEntry"]=world.UserNumAdmin,["SecDateTEntry"]=entry.Date.AddHours(16),
					["SecUserNumEdit"]=world.UserNumAdmin,["SecDateTEdit"]=entry.Date.AddHours(16),["Payee"]=entry.Payee,["Notes"]=entry.Notes,
					["NeedsReview"]=entry.NeedsReview,
				});
			}
			while(reconcileIndex<listReconciles.Count) {
				world.Sink.Insert("reconcile",new Row { ["ReconcileNum"]=listReconciles[reconcileIndex].reconcileNum,["AccountNum"]=world.AccountChecking,
					["StartingBal"]=startingBalance,["EndingBal"]=balance,["DateReconcile"]=listReconciles[reconcileIndex].date,["IsLocked"]=true });
				startingBalance=balance;
				reconcileIndex++;
			}
		}

		private static void WriteDashboardAr(World world) {
			foreach(KeyValuePair<DateTime,(decimal balTotal,decimal insEst)> pair in world.DictDashboardAr) {
				world.Sink.Insert("dashboardar",new Row { ["DashboardARNum"]=world.Sink.NextId("dashboardar"),["DateCalc"]=pair.Key,
					["BalTotal"]=pair.Value.balTotal,["InsEst"]=pair.Value.insEst });
			}
		}

		///<summary>Intra-office chat (25.3). Excluded from the fork; the rows only prove the tables survive. Message bodies use the WPF
		///FlowDocument form the 25.3 client stores; event rows use EventType 1, an assumed number (not published).</summary>
		private static void WriteChat(World world,Rng rng) {
			List<long> listStaff=new List<long> { world.UserNumAdmin };
			listStaff.AddRange(world.ListFrontDeskUsers);
			List<long> listClinical=world.AllProviders.Select(x => x.UserNum).ToList();
			var listChats=new List<(string name,List<long> members)> {
				("Front Office",listStaff),
				("Clinical Team",listClinical.Concat(new[] { world.UserNumAdmin }).ToList()),
				("",new List<long> { world.ListFrontDeskUsers[0],listClinical[0] }),
			};
			DateTime chatStart=world.AsOf.AddDays(-30);
			long seqCount=0;
			HashSet<long> setUsers=new HashSet<long>();
			string[] arrayMessages={"Patient in Op 2 is ready for the exam.","Can someone cover the front desk at noon?","Lab case for tomorrow arrived.",
				"Insurance portal is slow today.","Running 10 minutes behind.","Reminder: staff meeting Monday at 7:15.","Thanks!","On my way."};
			foreach((string name,List<long> members) chat in listChats) {
				long chatNum=world.Sink.NextId("chat");
				world.Sink.Insert("chat",new Row { ["ChatNum"]=chatNum,["Name"]=chat.name });
				foreach(long userNum in chat.members) {
					setUsers.Add(userNum);
					world.Sink.Insert("chatuserattach",new Row { ["ChatUserAttachNum"]=world.Sink.NextId("chatuserattach"),["UserNum"]=userNum,["ChatNum"]=chatNum,
						["IsRead"]=rng.Chance(0.7),["DateTimeRemoved"]=_minDate,["IsMute"]=rng.Chance(0.1) });
				}
				List<long> listMsgNums=new List<long>();
				long eventNum=world.Sink.NextId("chatmsg");
				world.Sink.Insert("chatmsg",new Row { ["ChatMsgNum"]=eventNum,["ChatNum"]=chatNum,["UserNum"]=chat.members[0],["DateTimeSent"]=chatStart.AddHours(9),
					["Message"]="Chat created with "+chat.members.Count+" members",["SeqCount"]=++seqCount,["Quote"]=0L,["EventType"]=1,["IsImportant"]=false });
				int count=rng.Between(8,16);
				for(int i=0;i<count;i++) {
					long msgNum=world.Sink.NextId("chatmsg");
					string text=rng.Pick(arrayMessages);
					world.Sink.Insert("chatmsg",new Row {
						["ChatMsgNum"]=msgNum,["ChatNum"]=chatNum,["UserNum"]=rng.Pick(chat.members),["DateTimeSent"]=chatStart.AddDays(i*2).AddHours(rng.Between(8,17)).AddMinutes(rng.Next(60)),
						["Message"]="<FlowDocument xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Paragraph>"+text+"</Paragraph></FlowDocument>",
						["SeqCount"]=++seqCount,["Quote"]=listMsgNums.Count>0 && rng.Chance(0.2) ? rng.Pick(listMsgNums) : 0L,["EventType"]=0,
						["IsImportant"]=text.StartsWith("Reminder"),
					});
					listMsgNums.Add(msgNum);
					if(rng.Chance(0.25)) {
						world.Sink.Insert("chatreaction",new Row { ["ChatReactionNum"]=world.Sink.NextId("chatreaction"),["ChatMsgNum"]=msgNum,
							["UserNum"]=rng.Pick(chat.members),["EmojiName"]=rng.Pick(new[] { "thumbsup","smile","check" }) });
					}
				}
				if(chat.name=="Front Office") {
					byte[] thumbnail=new byte[] { 0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A }.Concat(rng.Bytes(56)).ToArray();
					world.Sink.Insert("chatattach",new Row { ["ChatAttachNum"]=world.Sink.NextId("chatattach"),["ChatMsgNum"]=listMsgNums[0],
						["FileName"]="schedule-note.png",["Thumbnail"]=thumbnail,["FileData"]=thumbnail.Concat(rng.Bytes(192)).ToArray() });
				}
			}
			foreach(long userNum in setUsers.OrderBy(x => x)) {
				world.Sink.Insert("chatuserod",new Row { ["ChatUserodNum"]=world.Sink.NextId("chatuserod"),["UserNum"]=userNum,
					["UserStatus"]=rng.Pick(new[] { OdValues.ChatUserAvailable,OdValues.ChatUserAvailable,OdValues.ChatUserAway,OdValues.ChatUserDoNotDisturb }),
					["DateTimeStatusReset"]=_minDate,["Photo"]="",["PhotoCrop"]="",["OpenBackground"]=false,["CloseKeepRunning"]=true,["MuteNotifications"]=false,
					["DismissNotifySecs"]=10,["MuteImportantNotifications"]=false,["DismissImportantNotifySecs"]=0 });
			}
			//The counter the 25.3 client uses to number chat messages.
			world.Sink.Insert("sequencecounter",new Row { ["CounterNum"]=world.Sink.NextId("sequencecounter"),["CounterName"]="chatmsg",["CounterVal"]=seqCount });
		}

		///<summary>Dental school data (25.3 course scheduling, approvals and the new requirement columns). Excluded from the fork; a few
		///rows prove the tables and columns survive. Student and instructor providers are hidden.</summary>
		private static void WriteSchool(World world,Rng rng) {
			long gradingScaleNum=world.Sink.NextId("gradingscale");
			world.Sink.Insert("gradingscale",new Row { ["GradingScaleNum"]=gradingScaleNum,["Description"]="Pass/Fail",["ScaleType"]=0 });
			world.Sink.Insert("gradingscaleitem",new Row { ["GradingScaleItemNum"]=world.Sink.NextId("gradingscaleitem"),["GradingScaleNum"]=gradingScaleNum,
				["GradeShowing"]="P",["GradeNumber"]=1f,["Description"]="Pass" });
			world.Sink.Insert("gradingscaleitem",new Row { ["GradingScaleItemNum"]=world.Sink.NextId("gradingscaleitem"),["GradingScaleNum"]=gradingScaleNum,
				["GradeShowing"]="F",["GradeNumber"]=0f,["Description"]="Fail" });
			long schoolClassNum=world.Sink.NextId("schoolclass");
			world.Sink.Insert("schoolclass",new Row { ["SchoolClassNum"]=schoolClassNum,["GradYear"]=world.AsOf.Year+1,["Descript"]="Class of "+(world.AsOf.Year+1) });
			long instructor=AddSchoolProvider(world,rng,"INST1",0,isInstructor:true);
			long student1=AddSchoolProvider(world,rng,"STU1",schoolClassNum,isInstructor:false);
			long student2=AddSchoolProvider(world,rng,"STU2",schoolClassNum,isInstructor:false);
			DateTime termStart=new DateTime(world.AsOf.Year,8,25);
			var listCourses=new[] { ("OPD-101","Operative Dentistry I"),("PER-201","Periodontics Clinic") };
			foreach((string courseId,string descript) course in listCourses) {
				long courseDefNum=world.Sink.NextId("schoolcoursedef");
				world.Sink.Insert("schoolcoursedef",new Row { ["SchoolCourseDefNum"]=courseDefNum,["CourseID"]=course.courseId,["Descript"]=course.descript });
				long courseNum=world.Sink.NextId("schoolcourse");
				world.Sink.Insert("schoolcourse",new Row { ["SchoolCourseNum"]=courseNum,["CourseID"]=course.courseId,["Descript"]=course.descript,
					["DateStart"]=termStart,["DateEnd"]=termStart.AddDays(110),["SchoolClassNum"]=schoolClassNum,["GradingScaleNum"]=gradingScaleNum });
				foreach(int day in new[] { 1,3 }) {
					world.Sink.Insert("schoolcoursesched",new Row { ["SchoolCourseSchedNum"]=world.Sink.NextId("schoolcoursesched"),["SchoolCourseDefNum"]=courseDefNum,
						["SchoolCourseNum"]=courseNum,["TimeStart"]=new TimeSpan(8,0,0),["TimeEnd"]=new TimeSpan(11,30,0),["DayOfTheWeek"]=day,
						["DateOverride"]=_minDate,["IsOverride"]=false,["IsCanceled"]=false });
				}
				world.Sink.Insert("schoolcoursesched",new Row { ["SchoolCourseSchedNum"]=world.Sink.NextId("schoolcoursesched"),["SchoolCourseDefNum"]=courseDefNum,
					["SchoolCourseNum"]=courseNum,["TimeStart"]=new TimeSpan(8,0,0),["TimeEnd"]=new TimeSpan(11,30,0),["DayOfTheWeek"]=1,
					["DateOverride"]=ScheduleGenerator.Holidays(world.AsOf.Year)[3],["IsOverride"]=true,["IsCanceled"]=true });
				long reqNeededNum=world.Sink.NextId("reqneeded");
				world.Sink.Insert("reqneeded",new Row { ["ReqNeededNum"]=reqNeededNum,["Descript"]=course.courseId=="OPD-101" ? "Two-surface white filling" : "Deep cleaning, one quadrant",
					["SchoolCourseNum"]=courseNum,["SchoolClassNum"]=schoolClassNum,["SchoolCourseDefNum"]=courseDefNum });
				if(world.ListSampleCompletedProcs.Count>0) {
					(long procNum,long patNum,long provNum,DateTime date) sample=world.ListSampleCompletedProcs[(int)(courseNum%world.ListSampleCompletedProcs.Count)];
					world.Sink.Insert("reqstudent",new Row { ["ReqStudentNum"]=world.Sink.NextId("reqstudent"),["ReqNeededNum"]=reqNeededNum,
						["Descript"]="Completed in clinic",["SchoolCourseNum"]=courseNum,["ProvNum"]=student1,["AptNum"]=0L,["PatNum"]=sample.patNum,
						["InstructorNum"]=instructor,["DateCompleted"]=sample.date,["ProcNum"]=sample.procNum });
				}
			}
			for(int i=0;i<Math.Min(4,world.ListSampleCompletedProcs.Count);i++) {
				(long procNum,long patNum,long provNum,DateTime date) sample=world.ListSampleCompletedProcs[i];
				world.Sink.Insert("schoolapproval",new Row { ["SchoolApprovalNum"]=world.Sink.NextId("schoolapproval"),["ProvNum"]=i%2==0 ? student1 : student2,
					["SignOffStatus"]=i%3,["InstructorNum"]=instructor,["AptNum"]=0L,["ProcNum"]=sample.procNum,["TreatPlanNum"]=0L,["PerioExamNum"]=0L,
					["AllergyNum"]=0L,["DiseaseNum"]=0L,["DocNum"]=0L,["MountNum"]=0L,["SecDateEntry"]=sample.date,["SecDateTEdit"]=sample.date.AddHours(17) });
			}
		}

		private static long AddSchoolProvider(World world,Rng rng,string abbr,long schoolClassNum,bool isInstructor) {
			long provNum=world.Sink.NextId("provider");
			world.Sink.Insert("provider",new Row { ["ProvNum"]=provNum,["Abbr"]=abbr,["ItemOrder"]=900+(int)(provNum%50),["LName"]=NameBank.Surname(rng),
				["FName"]=NameBank.GivenName(rng),["Suffix"]=isInstructor ? "DDS" : "",["FeeSched"]=world.FeeSchedStandard,["IsHidden"]=true,
				["SchoolClassNum"]=schoolClassNum,["IsInstructor"]=isInstructor,["ProvStatus"]=OdValues.ProvActive,["SigOnFile"]=true });
			return provNum;
		}

		private static void WriteMiscellaneous(World world,Rng rng) {
			//Deleted-object markers for rows deleted in the past: the keys are taken from the sequences so they never collide with live rows.
			foreach((string table,int objectType) item in new[] { ("appointment",OdValues.DelAppointment),("patient",OdValues.DelRecallPatNum),
				("medicationpat",OdValues.DelMedicationPat),("appointment",OdValues.DelAppointment) })
			{
				world.Sink.Insert("deletedobject",new Row { ["DeletedObjectNum"]=world.Sink.NextId("deletedobject"),["ObjectNum"]=world.Sink.NextId(item.table),
					["ObjectType"]=item.objectType,["DateTStamp"]=world.AsOf.AddDays(-rng.Between(1,200)).AddHours(12) });
			}
			//ASAP texts (25.3 records the user who sent them). The eService is cut; the rows remain.
			foreach((long aptNum,long patNum,DateTime aptDateTime,long opNum) appointment in world.ListFutureAppointments.Take(5)) {
				DateTime entry=world.AsOf.AddDays(-rng.Between(1,5)).AddHours(10);
				bool declined=rng.Chance(0.3);
				world.Sink.Insert("asapcomm",new Row {
					["AsapCommNum"]=world.Sink.NextId("asapcomm"),["FKey"]=appointment.aptNum,["FKeyType"]=OdValues.AsapScheduledAppt,["ScheduleNum"]=0L,
					["PatNum"]=appointment.patNum,["ClinicNum"]=world.ClinicNum,["ShortGUID"]="SYN"+rng.Hex(6),["DateTimeEntry"]=entry,
					["DateTimeExpire"]=entry.AddDays(2),["DateTimeSmsScheduled"]=entry,["SmsSendStatus"]=3,["EmailSendStatus"]=1,["DateTimeSmsSent"]=entry.AddMinutes(1),
					["DateTimeEmailSent"]=_minDate,["EmailMessageNum"]=0L,["ResponseStatus"]=declined ? OdValues.AsapDeclined : OdValues.AsapPendingRsvp,
					["DateTimeOrig"]=appointment.aptDateTime,["TemplateText"]="An earlier time opened up. Reply YES to take it.",["TemplateEmail"]="",
					["TemplateEmailSubj"]="",["Note"]="",["GuidMessageToMobile"]="",["EmailTemplateType"]="Regular",["UserNum"]=rng.Pick(world.ListFrontDeskUsers),
				});
			}
		}
	}
}
