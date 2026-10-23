using System;

namespace OpenDentBusiness {
	///<summary>A conversation in Open Dental's intra-office chat (group or direct). Table added in Open Dental 25.3. Intra-office chat is excluded from
	///this program (owner decision 2026-10-18): the chat tables exist only for schema compatibility, and existing chat history is not shown. Not to be
	///confused with the HQ web chat table chatuser.</summary>
	[Serializable]
	public class Chat:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long ChatNum;
		///<summary>Optional name of the conversation.</summary>
		public string Name;

		public Chat() {
			Name="";
		}

		public Chat Copy() {
			return (Chat)MemberwiseClone();
		}
	}
}
