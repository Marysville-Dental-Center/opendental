using System;

namespace OpenDentBusiness {
	///<summary>Per-user intra-office chat profile and preferences. Table added in Open Dental 25.3. Schema compatibility only: intra-office chat is
	///excluded from this program (see Chat).</summary>
	[Serializable]
	public class ChatUserod:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long ChatUserodNum;
		///<summary>FK to userod.UserNum.</summary>
		public long UserNum;
		///<summary>Enum:EnumUserStatus Presence status.</summary>
		public EnumUserStatus UserStatus;
		///<summary>When the status resets.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateT)]
		public DateTime DateTimeStatusReset;
		///<summary>Profile photo.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string Photo;
		///<summary>Crop of the photo.</summary>
		public string PhotoCrop;
		///<summary>Start-up behavior flag.</summary>
		public bool OpenBackground;
		///<summary>Keep running when closed.</summary>
		public bool CloseKeepRunning;
		///<summary>Mute notifications.</summary>
		public bool MuteNotifications;
		///<summary>Seconds before a notification is dismissed.</summary>
		public int DismissNotifySecs;
		///<summary>Mute notifications for important messages.</summary>
		public bool MuteImportantNotifications;
		///<summary>Seconds before an important notification is dismissed.</summary>
		public int DismissImportantNotifySecs;

		public ChatUserod() {
			Photo="";
			PhotoCrop="";
		}

		public ChatUserod Copy() {
			return (ChatUserod)MemberwiseClone();
		}
	}

	///<summary>Stored in chatuserod.UserStatus. The Open Dental 25.3 database documentation names the values in this order without printing numbers,
	///so the numbers are inferred from the order.</summary>
	public enum EnumUserStatus {
		///<summary>0</summary>
		Available=0,
		///<summary>1</summary>
		Away=1,
		///<summary>2</summary>
		DoNotDisturb=2,
	}
}
