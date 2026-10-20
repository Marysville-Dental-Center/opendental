using System;

namespace OpenDentBusiness {
	///<summary>A named counter that hands out increasing sequence numbers, one row per consumer (for example chatmsg). Table added in Open Dental 25.3,
	///where intra-office chat uses it. Kept for schema compatibility.</summary>
	[Serializable]
	public class SequenceCounter:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long CounterNum;
		///<summary>Unique name, typically the table the counter serves.</summary>
		public string CounterName;
		///<summary>The last number handed out.</summary>
		public long CounterVal;

		public SequenceCounter() {
			CounterName="";
		}

		public SequenceCounter Copy() {
			return (SequenceCounter)MemberwiseClone();
		}
	}
}
