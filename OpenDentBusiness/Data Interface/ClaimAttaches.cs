using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace OpenDentBusiness{
	///<summary></summary>
	public class ClaimAttaches{
		public static long Insert(ClaimAttach claimAttach) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				claimAttach.ClaimAttachNum=Meth.GetLong(MethodBase.GetCurrentMethod(),claimAttach);
				return claimAttach.ClaimAttachNum;
			}
			return Crud.ClaimAttachCrud.Insert(claimAttach);
		}

		#region claim.AttachedFlags
		//claim.AttachedFlags is a comma list of keywords. The Edit Claim check boxes manage EoB, Note, Perio and Misc, and the Mail/Elect radio buttons.
		//Other keywords are set by the program: Unsup (the payer does not support attachments) and, since Open Dental 24.4, Submitted (the claim's
		//DentalXChange attachment has been sent). Every writer must keep keywords it does not manage.

		public const string FlagEoB="EoB";
		public const string FlagNote="Note";
		public const string FlagPerio="Perio";
		public const string FlagMisc="Misc";
		public const string FlagMail="Mail";
		public const string FlagElect="Elect";
		public const string FlagUnsup="Unsup";
		///<summary>Open Dental 24.4: the claim's DentalXChange attachment was sent. Not a paperwork type, so it never asks for a PWK segment.</summary>
		public const string FlagSubmitted="Submitted";

		///<summary>The keywords the Edit Claim check boxes and radio buttons own.</summary>
		private static readonly string[] _arrayFlagsFromCheckBoxes={ FlagEoB,FlagNote,FlagPerio,FlagMisc,FlagMail,FlagElect };

		///<summary>The keywords in attachedFlags, trimmed, without blanks or duplicates, in their original order. Null gives an empty list.</summary>
		public static List<string> ParseAttachedFlags(string attachedFlags) {
			List<string> listFlags=new List<string>();
			if(string.IsNullOrEmpty(attachedFlags)) {
				return listFlags;
			}
			string[] arrayParts=attachedFlags.Split(',');
			for(int i=0;i<arrayParts.Length;i++) {
				string flag=arrayParts[i].Trim();
				if(flag=="" || listFlags.Contains(flag)) {
					continue;
				}
				listFlags.Add(flag);
			}
			return listFlags;
		}

		public static string JoinAttachedFlags(List<string> listFlags) {
			return string.Join(",",listFlags.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct());
		}

		public static bool HasFlag(string attachedFlags,string flag) {
			return ParseAttachedFlags(attachedFlags).Contains(flag);
		}

		///<summary>True if the claim's DentalXChange attachment has been sent.</summary>
		public static bool IsDxcAttachmentSubmitted(string attachedFlags) {
			return HasFlag(attachedFlags,FlagSubmitted);
		}

		///<summary>The flags rebuilt from the Edit Claim check boxes (listFlagsFromCheckBoxes), keeping every keyword in attachedFlagsOld that the check
		///boxes do not manage (Submitted, Unsup and anything unknown), after the check box keywords.</summary>
		public static string RebuildFromCheckBoxes(string attachedFlagsOld,List<string> listFlagsFromCheckBoxes) {
			List<string> listFlags=new List<string>();
			for(int i=0;i<listFlagsFromCheckBoxes.Count;i++) {
				if(!listFlags.Contains(listFlagsFromCheckBoxes[i])) {
					listFlags.Add(listFlagsFromCheckBoxes[i]);
				}
			}
			List<string> listFlagsOld=ParseAttachedFlags(attachedFlagsOld);
			for(int i=0;i<listFlagsOld.Count;i++) {
				if(_arrayFlagsFromCheckBoxes.Contains(listFlagsOld[i]) || listFlags.Contains(listFlagsOld[i])) {
					continue;
				}
				listFlags.Add(listFlagsOld[i]);
			}
			return JoinAttachedFlags(listFlags);
		}

		///<summary>The flags after the claim's DentalXChange attachment has been sent: Mail is dropped (the attachment is electronic), Misc is added so
		///the 837 writes the attachment ID in its PWK segment, and Submitted is added. Other keywords are kept.</summary>
		public static string MarkDxcAttachmentSubmitted(string attachedFlags) {
			List<string> listFlags=ParseAttachedFlags(attachedFlags);
			listFlags.Remove(FlagMail);
			if(!listFlags.Contains(FlagMisc)) {
				listFlags.Insert(0,FlagMisc);
			}
			if(!listFlags.Contains(FlagSubmitted)) {
				listFlags.Add(FlagSubmitted);
			}
			return JoinAttachedFlags(listFlags);
		}

		///<summary>The flags without Submitted. Use when attachments still have to be sent, or when every attachment is gone.</summary>
		public static string ClearDxcAttachmentSubmitted(string attachedFlags) {
			List<string> listFlags=ParseAttachedFlags(attachedFlags);
			listFlags.Remove(FlagSubmitted);
			return JoinAttachedFlags(listFlags);
		}

		///<summary>Keeps Submitted only while the claim still has attachments: an empty listClaimAttaches (all deleted) clears it. Otherwise unchanged.</summary>
		public static string SyncDxcSubmittedWithAttachments(string attachedFlags,List<ClaimAttach> listClaimAttaches) {
			if(listClaimAttaches==null || listClaimAttaches.Count==0) {
				return ClearDxcAttachmentSubmitted(attachedFlags);
			}
			return attachedFlags??"";
		}

		///<summary>True if the flags call for paperwork (a PWK segment): any keyword other than Mail and Submitted. This is the 837 builders' old
		///"AttachedFlags!=Mail" test, made to ignore Submitted, so "Mail,Submitted" does not ask for paperwork.</summary>
		public static bool HasPaperworkFlags(string attachedFlags) {
			List<string> listFlags=ParseAttachedFlags(attachedFlags);
			if(listFlags.Count==0) {
				return attachedFlags!=FlagMail;//Blank behaves as before: blank is not "Mail".
			}
			return listFlags.Any(x => x!=FlagMail && x!=FlagSubmitted);
		}

		///<summary>True when the "Attachment ID missing" warning applies: paperwork is needed and there is no attachment ID, unless the claim's
		///DentalXChange attachment has already been sent.</summary>
		public static bool IsAttachmentIdMissing(string attachedFlags,string attachmentID,bool isPwkNeeded) {
			if(!isPwkNeeded || !string.IsNullOrEmpty(attachmentID)) {
				return false;
			}
			return !IsDxcAttachmentSubmitted(attachedFlags);
		}
		#endregion claim.AttachedFlags
	}




}
