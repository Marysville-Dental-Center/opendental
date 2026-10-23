// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;

namespace SyntheticData.Generation {
	public enum PayerKind {
		///<summary>Pays at the visit or soon after the insurance pays.</summary>
		Prompt,
		///<summary>Pays, but one to four months late.</summary>
		Slow,
		///<summary>Rarely pays; the balance ages, collects finance charges and moves to the collections billing type.</summary>
		NonPayer,
	}

	public class Person {
		public long PatNum;
		public string FName;
		public string LName;
		public string MiddleI;
		public string Preferred;
		public DateTime Birthdate;
		public int Gender;
		public int Position;
		public int Status;
		public bool IsGuarantor;
		public ProviderInfo Dentist;
		public ProviderInfo Hygienist;
		public DateTime DateFirstVisit;
		public string Email="";
		public string WirelessPhone="";
		public bool IsPerio;
		///<summary>No visits after this date (inactive, archived and deceased patients).</summary>
		public DateTime LastAllowedVisit;
		public List<InsCoverage> ListCoverages=new List<InsCoverage>();
		public DateTime? LastHygieneVisit;
		public DateTime? LastBitewings;
		public DateTime? NextHygieneDue;
		public long NextHygieneAptNum;
		public DateTime? DateTimeNextScheduled;
		public readonly Dictionary<(long planNum,int year),decimal> DictInsUsed=new Dictionary<(long,int),decimal>();
		public readonly Dictionary<(long planNum,int year),decimal> DictDedUsed=new Dictionary<(long,int),decimal>();
		public readonly Dictionary<(string group,long planNum,int year),int> DictFreqUsed=new Dictionary<(string,long,int),int>();
		public decimal OwnInsEst;
		public DateTime? DateTimeDeceased;

		public int AgeOn(DateTime date) {
			int age=date.Year-Birthdate.Year;
			if(date<Birthdate.AddYears(age)) {
				age--;
			}
			return age;
		}

		public string NameFL {
			get { return FName+" "+LName; }
		}
	}

	///<summary>A procedure while the family is simulated. Rows are written once, in their final state, at the end of the family.</summary>
	public class ProcRecord {
		public long ProcNum;
		public Person Patient;
		public ProcCodeDef Code;
		public long ProvNum;
		public DateTime ProcDate;
		public DateTime DateTP;
		public int Status;
		public decimal Fee;
		public string ToothNum="";
		public string Surf="";
		public long AptNum;
		public long PlannedAptNum;
		public long StatementNum;
		public decimal InsPaid;
		public decimal WriteOff;
		public decimal InsEstOutstanding;
		public decimal PatPaid;
		public decimal AdjTotal;
		public decimal PayPlanCredit;
		public DateTime DateBillable;
		public string Note="";
		public bool HasCompletedClaim;

		///<summary>What the patient still owes on this procedure, counting the outstanding insurance estimate as covered.</summary>
		public decimal PatientRemainder {
			get { return Fee+AdjTotal-InsPaid-WriteOff-InsEstOutstanding-PatPaid-PayPlanCredit; }
		}

		public string Describe() {
			string prefix="";
			if(ToothNum!="") {
				prefix="#"+ToothNum+"-";
				if(Surf!="") {
					prefix+=Surf+"-";
				}
			}
			return prefix+Code.Abbr;
		}
	}

	///<summary>A money movement on the family account, for aging, statements and the business totals. Positive amounts are charges.</summary>
	public struct LedgerEntry {
		public DateTime Date;
		public decimal Amount;
		public Person Patient;

		public LedgerEntry(DateTime date,decimal amount,Person patient) {
			Date=date;
			Amount=amount;
			Patient=patient;
		}
	}

	public struct AgingResult {
		public decimal Bal0To30;
		public decimal Bal31To60;
		public decimal Bal61To90;
		public decimal BalOver90;
		public decimal Total;
	}

	public static class Aging {
		///<summary>Open Dental style aging: credits pay the oldest charges first; what is left of each charge goes into the bucket for its
		///age on the as-of date. A credit balance shows as a negative 0-30 amount. Entries after the as-of date are ignored.</summary>
		public static AgingResult Compute(IEnumerable<LedgerEntry> entries,DateTime asOf) {
			List<(DateTime date,decimal amount)> listCharges=new List<(DateTime,decimal)>();
			decimal credits=0;
			foreach(LedgerEntry entry in entries) {
				if(entry.Date>asOf) {
					continue;
				}
				if(entry.Amount>0) {
					listCharges.Add((entry.Date,entry.Amount));
				}
				else {
					credits-=entry.Amount;
				}
			}
			listCharges.Sort((x,y) => x.date.CompareTo(y.date));
			AgingResult result=new AgingResult();
			foreach((DateTime date,decimal amount) charge in listCharges) {
				decimal remaining=charge.amount;
				if(credits>0) {
					decimal applied=Math.Min(credits,remaining);
					credits-=applied;
					remaining-=applied;
				}
				if(remaining<=0) {
					continue;
				}
				int days=(asOf-charge.date).Days;
				if(days<=30) {
					result.Bal0To30+=remaining;
				}
				else if(days<=60) {
					result.Bal31To60+=remaining;
				}
				else if(days<=90) {
					result.Bal61To90+=remaining;
				}
				else {
					result.BalOver90+=remaining;
				}
			}
			if(credits>0) {
				result.Bal0To30-=credits;
			}
			result.Total=result.Bal0To30+result.Bal31To60+result.Bal61To90+result.BalOver90;
			return result;
		}
	}
}
