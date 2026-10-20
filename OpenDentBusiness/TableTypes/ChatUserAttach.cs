using System;

namespace OpenDentBusiness {
	///<summary>Membership of a user in an intra-office chat, with read and mute state. Table added in Open Dental 25.3. Schema compatibility only:
	///intra-office chat is excluded from this program (see Chat).</summary>
	[Serializable]
	public class ChatUserAttach:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long ChatUserAttachNum;
		///<summary>FK to userod.UserNum.</summary>
		public long UserNum;
		///<summary>FK to chat.ChatNum.</summary>
		public long ChatNum;
		///<summary>Whether the user has read the chat.</summary>
		public bool IsRead;
		///<summary>When the user left the chat. 0001-01-01 while still a member.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateT)]
		public DateTime DateTimeRemoved;
		///<summary>Whether the user muted the chat.</summary>
		public bool IsMute;

		public ChatUserAttach Copy() {
			return (ChatUserAttach)MemberwiseClone();
		}
	}
}
