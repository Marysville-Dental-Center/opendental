using System;

namespace OpenDentBusiness {
	///<summary>An emoji reaction by a user to an intra-office chat message. Table added in Open Dental 25.3. Schema compatibility only: intra-office chat
	///is excluded from this program (see Chat).</summary>
	[Serializable]
	public class ChatReaction:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long ChatReactionNum;
		///<summary>FK to chatmsg.ChatMsgNum.</summary>
		public long ChatMsgNum;
		///<summary>FK to userod.UserNum.</summary>
		public long UserNum;
		///<summary>Name of the emoji.</summary>
		public string EmojiName;

		public ChatReaction() {
			EmojiName="";
		}

		public ChatReaction Copy() {
			return (ChatReaction)MemberwiseClone();
		}
	}
}
