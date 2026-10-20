using System;

namespace OpenDentBusiness {
	///<summary>One message in an intra-office chat. Table added in Open Dental 25.3. Schema compatibility only: intra-office chat is excluded from this
	///program (see Chat).</summary>
	[Serializable]
	public class ChatMsg:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long ChatMsgNum;
		///<summary>FK to chat.ChatNum.</summary>
		public long ChatNum;
		///<summary>FK to userod.UserNum. The author.</summary>
		public long UserNum;
		///<summary>When the message was sent.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateT)]
		public DateTime DateTimeSent;
		///<summary>Rich-text body.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string Message;
		///<summary>Strictly increasing number handed out by sequencecounter, used to fetch new messages. bigint(20) unsigned in the database.</summary>
		public long SeqCount;
		///<summary>FK to chatmsg.ChatMsgNum. The quoted message, or 0.</summary>
		public long Quote;
		///<summary>Kind of event for messages that are event notifications rather than text. The values are not published.</summary>
		public byte EventType;
		///<summary>Importance flag.</summary>
		public bool IsImportant;

		public ChatMsg() {
			Message="";
		}

		public ChatMsg Copy() {
			return (ChatMsg)MemberwiseClone();
		}
	}
}
