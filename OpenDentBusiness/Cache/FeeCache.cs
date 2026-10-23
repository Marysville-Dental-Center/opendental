using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Linq;
using CodeBase;
using System.Data;
using System.Threading;
using System.Collections.Concurrent;
using System.Xml.Serialization;
using System.ComponentModel;

namespace OpenDentBusiness {


	///<summary>A limited fee class that eliminates unused fields. Used by the FeeCache to keep memory usage down.</summary>
	[XmlType(TypeName="A")]
	public class FeeLim {
		///<summary>Primary key.</summary>
		[XmlElement("A",typeof(long))]
		public long FeeNum;
		///<summary>The amount usually charged.  If an amount is unknown, then the entire Fee entry is deleted from the database.  
		///The absence of a fee is shown in the user interface as a blank entry.
		///For clinic and/or provider fees, amount can be set to -1 which indicates that their fee should be blank and not use the default fee.
		///</summary>
		[XmlElement("B",typeof(double))]
		public double Amount;
		///<summary>FK to feesched.FeeSchedNum.</summary>
		[XmlElement("C",typeof(long))]
		public long FeeSched;
		///<summary>FK to procedurecode.CodeNum.</summary>
		[XmlElement("D",typeof(long))]
		public long CodeNum;
		///<summary>FK to clinic.ClinicNum. (Used only if localization of fees for a feesched is enabled)</summary>
		[XmlElement("E",typeof(long)),DefaultValue(0L)]
		public long ClinicNum;
		///<summary>FK to provider.ProvNum. (Used only if localization of fees for a feesched is enabled)</summary>
		[XmlElement("F",typeof(long)),DefaultValue(0L)]
		public long ProvNum;
		///<summary>Date. Used in securitylogs to track when fees are updated or modified.</summary>
		[XmlElement("G",typeof(DateTime))]
		public DateTime SecDateTEdit;
		///<summary>The date from which this fee applies. 0001-01-01 means always. See Fee.DateEffective.</summary>
		[XmlElement("H",typeof(DateTime))]
		public DateTime DateEffective;

		/// <summary>Enable casting between Fee and Cached Fee. Will fail if attempting to cast a null. Casting will always return a deep copy.</summary>
		public static explicit operator Fee(FeeLim f) {
			return new Fee() {
				FeeNum=f.FeeNum,
				Amount=f.Amount,
				FeeSched=f.FeeSched,
				CodeNum=f.CodeNum,
				ClinicNum=f.ClinicNum,
				ProvNum=f.ProvNum,
				SecDateTEdit=f.SecDateTEdit,
				DateEffective=f.DateEffective
			};
		}

		/// <summary>Enable casting between Fee and Cached Fee. Will fail if attempting to cast a null. Casting will always return a deep copy.</summary>
		public static explicit operator FeeLim(Fee f) {
			return new FeeLim() {
				FeeNum=f.FeeNum,
				Amount=f.Amount,
				FeeSched=f.FeeSched,
				CodeNum=f.CodeNum,
				ClinicNum=f.ClinicNum,
				ProvNum=f.ProvNum,
				SecDateTEdit=f.SecDateTEdit,
				DateEffective=f.DateEffective
			};
		}

		///<summary>Return a deep copy.</summary>
		public FeeLim Copy() {
			return (FeeLim)this.MemberwiseClone();
		}
	}

	///<summary>An in-memory index over a fixed list of fee rows, for screens and tools that look up many fees (the Procedure Codes grid, fee
	///schedule export). Rows are grouped by fee schedule, code, clinic and provider and sorted by DateEffective, and a "current" index holds the row
	///in effect on DateCurrent (normally today) for each key, so the common lookup is a dictionary hit instead of a scan of the whole list. Lookups for
	///another date search the dated rows of each key. Gives the same answers as Fees.GetFeeFromList. The index does not see changes made to the list
	///after it was built; build a new one.</summary>
	public class FeeIndex {
		private readonly Dictionary<FeeIndexKey,List<Fee>> _dictFeesByKey=new Dictionary<FeeIndexKey,List<Fee>>();
		private readonly Dictionary<FeeIndexKey,Fee> _dictFeesCurrent=new Dictionary<FeeIndexKey,Fee>();
		///<summary>The as-of date of the current index.</summary>
		public DateTime DateCurrent { get; private set; }

		///<summary>Builds the index. dateCurrent defaults to today. Null rows are skipped.</summary>
		public FeeIndex(IEnumerable<Fee> listFees,DateTime dateCurrent=default(DateTime)) {
			DateCurrent=(dateCurrent.Year>1880 ? dateCurrent.Date : DateTime.Today);
			foreach(Fee fee in listFees) {
				if(fee==null) {
					continue;
				}
				FeeIndexKey feeIndexKey=new FeeIndexKey(fee.FeeSched,fee.CodeNum,fee.ClinicNum,fee.ProvNum);
				if(!_dictFeesByKey.TryGetValue(feeIndexKey,out List<Fee> listFeesKey)) {
					listFeesKey=new List<Fee>();
					_dictFeesByKey[feeIndexKey]=listFeesKey;
				}
				listFeesKey.Add(fee);
			}
			foreach(KeyValuePair<FeeIndexKey,List<Fee>> keyValuePair in _dictFeesByKey) {
				//Stable sort by date, so that among rows with the same date the first one in the list stays first, as in Fees.GetFeeFromList.
				List<Fee> listFeesSorted=keyValuePair.Value.Select((fee,index) => new { fee,index })
					.OrderBy(x => x.fee.DateEffective).ThenBy(x => x.index).Select(x => x.fee).ToList();
				keyValuePair.Value.Clear();
				keyValuePair.Value.AddRange(listFeesSorted);
				Fee feeCurrent=FindInEffect(listFeesSorted,DateCurrent);
				if(feeCurrent!=null) {
					_dictFeesCurrent[keyValuePair.Key]=feeCurrent;
				}
			}
		}

		///<summary>Number of distinct fee schedule, code, clinic and provider keys.</summary>
		public int CountKeys {
			get {
				return _dictFeesByKey.Count;
			}
		}

		///<summary>Number of keys with a row in effect on DateCurrent.</summary>
		public int CountCurrent {
			get {
				return _dictFeesCurrent.Count;
			}
		}

		///<summary>The row in effect on dateAsOf (DateCurrent if not given) for exactly this key, or null.</summary>
		public Fee GetExact(long feeSchedNum,long codeNum,long clinicNum,long provNum,DateTime dateAsOf=default(DateTime)) {
			FeeIndexKey feeIndexKey=new FeeIndexKey(feeSchedNum,codeNum,clinicNum,provNum);
			if(dateAsOf.Year<1880 || dateAsOf.Date==DateCurrent) {
				_dictFeesCurrent.TryGetValue(feeIndexKey,out Fee feeCurrent);
				return feeCurrent;
			}
			if(!_dictFeesByKey.TryGetValue(feeIndexKey,out List<Fee> listFeesKey)) {
				return null;
			}
			return FindInEffect(listFeesKey,dateAsOf.Date);
		}

		///<summary>Every row of exactly this key, oldest start date first. Empty if none.</summary>
		public List<Fee> GetAllDates(long feeSchedNum,long codeNum,long clinicNum,long provNum) {
			if(!_dictFeesByKey.TryGetValue(new FeeIndexKey(feeSchedNum,codeNum,clinicNum,provNum),out List<Fee> listFeesKey)) {
				return new List<Fee>();
			}
			return new List<Fee>(listFeesKey);
		}

		///<summary>The fee for a code, with the clinic and provider override order of Fees.GetFeeFromList, among rows in effect on dateAsOf (DateCurrent
		///if not given). For a global schedule clinicNum and provNum are ignored. Pass isGlobal from FeeScheds.IsGlobal.</summary>
		public Fee GetFee(long codeNum,long feeSchedNum,long clinicNum,long provNum,bool isGlobal,DateTime dateAsOf=default(DateTime),
			bool doGetExactMatch=false)
		{
			if(isGlobal && !doGetExactMatch) {
				clinicNum=0;
				provNum=0;
			}
			Fee fee=GetExact(feeSchedNum,codeNum,clinicNum,provNum,dateAsOf);
			if(fee!=null) {
				return fee;
			}
			if(doGetExactMatch || isGlobal) {
				return null;
			}
			return GetExact(feeSchedNum,codeNum,0,provNum,dateAsOf)
				??GetExact(feeSchedNum,codeNum,clinicNum,0,dateAsOf)
				??GetExact(feeSchedNum,codeNum,0,0,dateAsOf);
		}

		///<summary>listFeesSorted is sorted by date. Returns the last row starting on or before dateAsOf; the first of equal dates wins.</summary>
		private static Fee FindInEffect(List<Fee> listFeesSorted,DateTime dateAsOf) {
			Fee feeRet=null;
			for(int i=0;i<listFeesSorted.Count;i++) {
				if(listFeesSorted[i].DateEffective.Date>dateAsOf) {
					break;
				}
				if(feeRet==null || listFeesSorted[i].DateEffective>feeRet.DateEffective) {
					feeRet=listFeesSorted[i];
				}
			}
			return feeRet;
		}

		private struct FeeIndexKey:IEquatable<FeeIndexKey> {
			public readonly long FeeSched;
			public readonly long CodeNum;
			public readonly long ClinicNum;
			public readonly long ProvNum;

			public FeeIndexKey(long feeSched,long codeNum,long clinicNum,long provNum) {
				FeeSched=feeSched;
				CodeNum=codeNum;
				ClinicNum=clinicNum;
				ProvNum=provNum;
			}

			public bool Equals(FeeIndexKey other) {
				return FeeSched==other.FeeSched && CodeNum==other.CodeNum && ClinicNum==other.ClinicNum && ProvNum==other.ProvNum;
			}

			public override bool Equals(object obj) {
				return obj is FeeIndexKey && Equals((FeeIndexKey)obj);
			}

			public override int GetHashCode() {
				unchecked {
					int hash=FeeSched.GetHashCode();
					hash=hash*31+CodeNum.GetHashCode();
					hash=hash*31+ClinicNum.GetHashCode();
					hash=hash*31+ProvNum.GetHashCode();
					return hash;
				}
			}
		}
	}

}
