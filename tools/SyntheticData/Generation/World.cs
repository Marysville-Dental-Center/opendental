// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SyntheticData.Infrastructure;

namespace SyntheticData.Generation {
	public class ProviderInfo {
		public long ProvNum;
		public string Abbr;
		public string FName;
		public string LName;
		public bool IsHygienist;
		public long OpNum;
		public long UserNum;
	}

	public class InsPlanInfo {
		public long PlanNum;
		public long CarrierNum;
		public string CarrierName;
		public long EmployerNum;
		public string GroupName;
		public string GroupNum;
		public bool IsPpo;
		///<summary>Allowed-fee schedule for PPO plans, 0 otherwise.</summary>
		public long FeeSched;
		public Dictionary<long,int> DictPercentByCovCat=new Dictionary<long,int>();
		public decimal AnnualMax;
		public decimal Deductible;
		public bool ExcludesImplants;
		///<summary>Third molars excluded from surgical removal (25.3 tooth-range exclusion).</summary>
		public bool ExcludesThirdMolarSurgery;
		public int CrownWaitMonths;
		public bool UsesEft;
	}

	public class InsCoverage {
		public InsPlanInfo Plan;
		public long InsSubNum;
		public long PatPlanNum;
		public int Ordinal;
		public int Relationship;
		public long SubscriberPatNum;
		public DateTime DateEffective;
	}

	///<summary>One insurance check (claimpayment), filled as claims are received.</summary>
	public class ClaimPaymentBatch {
		public long ClaimPaymentNum;
		public long CarrierNum;
		public string CarrierName;
		public DateTime CheckDate;
		public decimal Amount;
		public bool IsEft;
		public long DepositNum;
	}

	///<summary>One bank deposit, filled as payments and insurance checks are made.</summary>
	public class DepositBatch {
		public long DepositNum;
		public DateTime DateDeposit;
		public decimal PatientAmount;
		public decimal InsuranceAmount;
	}

	///<summary>State shared by all generators: settings, the sink, and the practice set-up that families refer to.</summary>
	public class World {
		public GenerationSettings Settings;
		public DataSink Sink;
		public DateTime AsOf;
		///<summary>Value written to timestamp columns that are not set explicitly.</summary>
		public DateTime TimestampValue;
		public readonly Dictionary<(int category,string name),long> DictDefs=new Dictionary<(int,string),long>();
		public readonly Dictionary<string,long> DictUsers=new Dictionary<string,long>();
		public long UserNumAdmin;
		public List<long> ListFrontDeskUsers=new List<long>();
		public List<ProviderInfo> ListDentists=new List<ProviderInfo>();
		public List<ProviderInfo> ListHygienists=new List<ProviderInfo>();
		public List<ProcCodeDef> ListCodes;
		public Dictionary<string,ProcCodeDef> DictCodes;
		public List<CovCatDef> ListCovCats;
		public List<CodeGroupDef> ListCodeGroups;
		public long FeeSchedStandard;
		public long FeeSchedPpoA;
		public long FeeSchedPpoB;
		public long FeeSchedCopay;
		public long FeeSchedOutOfNetwork;
		///<summary>Fee rows by (schedule, code, provider), each list sorted by effective date.</summary>
		public readonly Dictionary<(long feeSched,long codeNum,long provNum),List<(DateTime dateEffective,decimal amount)>> DictFees
			=new Dictionary<(long,long,long),List<(DateTime,decimal)>>();
		public List<InsPlanInfo> ListPlans=new List<InsPlanInfo>();
		public List<long> ListEmployerNums=new List<long>();
		public long RecallTypeProphy;
		public long RecallTypePerio;
		public long RecallTypeBitewing;
		public long SheetDefPayPlan;
		public long SheetDefCheck;
		public long SheetDefConsent;
		public long EFormDefRegistration;
		public long EFormDefMedical;
		public List<(long fieldDefNum,int fieldType,string dbLink,string label,string pickList,int itemOrder)> ListRegistrationFields
			=new List<(long,int,string,string,string,int)>();
		public List<(long fieldDefNum,int fieldType,string dbLink,string label,string pickList,int itemOrder)> ListMedicalFields
			=new List<(long,int,string,string,string,int)>();
		///<summary>Conditional eForm field definitions: the parent field's label and the trigger value(s), copied onto each filled field.</summary>
		public Dictionary<long,(string parent,string value)> DictEFormConditions=new Dictionary<long,(string,string)>();
		public List<(long medicationNum,string name,bool isHidden)> ListMedications=new List<(long,string,bool)>();
		public List<long> ListAllergyDefNums=new List<long>();
		public List<long> ListDiseaseDefNums=new List<long>();
		public List<long> ListReferralNums=new List<long>();
		public List<long> ListTaskListNums=new List<long>();
		public long AccountChecking;
		public long AccountPatientIncome;
		public long AccountInsuranceIncome;
		public List<(long accountNum,string name)> ListExpenseAccounts=new List<(long,string)>();
		public List<long> ListPayPlanTemplateNums=new List<long>();
		public List<DateTime> ListWorkdays=new List<DateTime>();
		public HashSet<DateTime> SetHolidays=new HashSet<DateTime>();
		public SlotBook SlotBook=new SlotBook();
		public readonly Dictionary<(long carrierNum,DateTime checkDate),ClaimPaymentBatch> DictClaimPayments=new Dictionary<(long,DateTime),ClaimPaymentBatch>();
		public readonly Dictionary<DateTime,DepositBatch> DictDeposits=new Dictionary<DateTime,DepositBatch>();
		///<summary>Patients created so far, for the feature generators that need a few real rows to point at.</summary>
		public List<(long patNum,long guarantor,int status,DateTime birthdate)> ListPatients=new List<(long,long,int,DateTime)>();
		public List<(long aptNum,long patNum,DateTime aptDateTime,long opNum)> ListFutureAppointments=new List<(long,long,DateTime,long)>();
		public List<(long procNum,long patNum,long provNum,DateTime date)> ListSampleCompletedProcs=new List<(long,long,long,DateTime)>();
		public List<(long claimNum,long patNum,long planNum,DateTime dateReceived)> ListPreauthClaims=new List<(long,long,long,DateTime)>();
		public List<long> ListEobClaimPayments=new List<long>();
		///<summary>Practice-wide accounts receivable and outstanding insurance at the start of each month (dashboardar).</summary>
		public readonly SortedDictionary<DateTime,(decimal balTotal,decimal insEst)> DictDashboardAr=new SortedDictionary<DateTime,(decimal,decimal)>();
		public long ClinicNum;
		public long UserGroupClinical;

		public void AddDashboardAr(DateTime month,decimal balance,decimal insEst) {
			DictDashboardAr.TryGetValue(month,out (decimal balTotal,decimal insEst) totals);
			DictDashboardAr[month]=(totals.balTotal+balance,totals.insEst+insEst);
		}

		public long Def(int category,string name) {
			if(!DictDefs.TryGetValue((category,name),out long defNum)) {
				throw new InvalidOperationException("Definition "+category+"/"+name+" was not created.");
			}
			return defNum;
		}

		public long User(string userName) {
			return DictUsers[userName];
		}

		public ProcCodeDef Code(string code) {
			return DictCodes[code];
		}

		public IEnumerable<ProviderInfo> AllProviders {
			get { return ListDentists.Concat(ListHygienists); }
		}

		///<summary>The fee in effect on the date: the row with the latest effective date on or before it, a provider-specific row winning
		///over the schedule's general row. Returns null when the schedule has no fee for the code.</summary>
		public decimal? GetFee(long feeSched,long codeNum,long provNum,DateTime date) {
			if(provNum!=0) {
				decimal? feeProv=GetFeeExact(feeSched,codeNum,provNum,date);
				if(feeProv.HasValue) {
					return feeProv;
				}
			}
			return GetFeeExact(feeSched,codeNum,0,date);
		}

		private decimal? GetFeeExact(long feeSched,long codeNum,long provNum,DateTime date) {
			if(!DictFees.TryGetValue((feeSched,codeNum,provNum),out List<(DateTime dateEffective,decimal amount)> listFees)) {
				return null;
			}
			decimal? amount=null;
			foreach((DateTime dateEffective,decimal amount) fee in listFees) {
				if(fee.dateEffective<=date) {
					amount=fee.amount;
				}
			}
			return amount;
		}

		///<summary>The deposit for the week containing the date (deposits are made on the Friday). Null when that Friday is less than a
		///week before the as-of date, so recent money is still undeposited, as in a real office.</summary>
		public DepositBatch DepositFor(DateTime date) {
			DateTime friday=date.Date.AddDays(((int)DayOfWeek.Friday-(int)date.DayOfWeek+7)%7);
			if(friday>AsOf.AddDays(-7)) {
				return null;
			}
			if(!DictDeposits.TryGetValue(friday,out DepositBatch deposit)) {
				deposit=new DepositBatch { DepositNum=Sink.NextId("deposit"),DateDeposit=friday };
				DictDeposits[friday]=deposit;
			}
			return deposit;
		}

		///<summary>Placeholder for Open Dental's proprietary row hash. These are random-looking values, not valid hashes: adoption
		///rehashes every row with the fork's own HMAC, so only their presence matters.</summary>
		public static string PseudoHash(string seedText) {
			using(SHA256 sha=SHA256.Create()) {
				return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes("synthetic:"+seedText)));
			}
		}

		public static string Sha256Hex(string text) {
			using(SHA256 sha=SHA256.Create()) {
				StringBuilder stringBuilder=new StringBuilder();
				foreach(byte b in sha.ComputeHash(Encoding.UTF8.GetBytes(text))) {
					stringBuilder.Append(b.ToString("x2"));
				}
				return stringBuilder.ToString();
			}
		}
	}

	///<summary>Free 5-minute slots per operatory per workday, from 8:00 to 17:00 with lunch from 12:00 to 13:00.</summary>
	public class SlotBook {
		public const int SlotsPerDay=108;
		public const int LunchStartSlot=48;
		public const int LunchEndSlot=60;
		private readonly Dictionary<(long opNum,DateTime date),bool[]> _dictDays=new Dictionary<(long,DateTime),bool[]>();

		private bool[] GetDay(long opNum,DateTime date) {
			if(!_dictDays.TryGetValue((opNum,date),out bool[] arrayTaken)) {
				arrayTaken=new bool[SlotsPerDay];
				for(int i=LunchStartSlot;i<LunchEndSlot;i++) {
					arrayTaken[i]=true;
				}
				_dictDays[(opNum,date)]=arrayTaken;
			}
			return arrayTaken;
		}

		///<summary>Books the first free run of the given length at or after the preferred slot (wrapping to the start of the day).
		///Returns the start time, or null when the day is full.</summary>
		public DateTime? TryBook(long opNum,DateTime date,int slotCount,int preferredSlot) {
			bool[] arrayTaken=GetDay(opNum,date);
			for(int offset=0;offset<SlotsPerDay;offset++) {
				int start=(preferredSlot+offset)%SlotsPerDay;
				if(start+slotCount>SlotsPerDay) {
					continue;
				}
				bool isFree=true;
				for(int i=start;i<start+slotCount;i++) {
					if(arrayTaken[i]) {
						isFree=false;
						break;
					}
				}
				if(!isFree) {
					continue;
				}
				for(int i=start;i<start+slotCount;i++) {
					arrayTaken[i]=true;
				}
				return date.Date.AddHours(8).AddMinutes(start*5);
			}
			return null;
		}
	}
}
