using System;

namespace OpenDentBusiness {
	///<summary>A saved Query Monitor filter: any running query that contains FilterText is hidden. Table added in Open Dental 24.4. The Query Monitor
	///feature package builds the UI.</summary>
	[Serializable]
	public class QueryFilter:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long QueryFilterNum;
		///<summary>A plain label used to group filters.</summary>
		public string GroupName;
		///<summary>Text to look for in running queries.</summary>
		public string FilterText;

		public QueryFilter() {
			GroupName="";
			FilterText="";
		}

		public QueryFilter Copy() {
			return (QueryFilter)MemberwiseClone();
		}
	}
}
