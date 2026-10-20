using System;

namespace OpenDentBusiness {
	///<summary>A file attached to an intra-office chat message. Table added in Open Dental 25.3. Schema compatibility only: intra-office chat is excluded
	///from this program (see Chat).
	///The table also has two mediumblob columns, Thumbnail (a small PNG) and FileData (the file). They are deliberately not mapped here: the CRUD
	///generator has no binary column type, and nothing in this program reads chat attachments. The schema migration creates both columns.</summary>
	[Serializable]
	public class ChatAttach:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long ChatAttachNum;
		///<summary>FK to chatmsg.ChatMsgNum.</summary>
		public long ChatMsgNum;
		///<summary>Original file name.</summary>
		public string FileName;

		public ChatAttach() {
			FileName="";
		}

		public ChatAttach Copy() {
			return (ChatAttach)MemberwiseClone();
		}
	}
}
