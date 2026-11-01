using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The second signature on a group note (Open Dental 26.1, package M2-NOTES): procnote.UserNum2, Signature2 and SigIsTopaz2. Two users,
	///such as a hygienist and a dentist, can each sign one group note. The Group Note window, the Chart's progress notes and Procedures.Update call
	///these rules.</summary>
	public static class GroupNoteSignatures {
		#region Signature key
		///<summary>The data the second signature is bound to: the same fields as the first signature's key (GroupNotes.GetSignatureKey: procedure
		///date, entry date, user, note, attached procedures), with the second box's user (UserNum2) in place of the note's user. So each signature is
		///bound to the note and to its own signer: editing the note, the date or the attached procedures invalidates both, and the first box's user can
		///change without invalidating the second signature. INFERRED: Open Dental's 26.1 key is not public.</summary>
		public static string GetSignatureKey2(Procedure procedureGroup,List<ProcGroupItem> listProcGroupItems) {
			Procedure procedureKey=procedureGroup.Copy();
			procedureKey.UserNum=procedureGroup.UserNum2;
			return GroupNotes.GetSignatureKey(procedureKey,listProcGroupItems);
		}
		#endregion Signature key

		#region Who may edit and sign
		///<summary>True when the user may edit the note text. As in 24.3, a signed note can be edited only by its signer or by a user with Group Note
		///Edit Signed, applied to each signature: every signed box must be the user's own, unless the user has the permission. Editing the text
		///clears both signatures.</summary>
		public static bool CanEditNoteText(bool hasEditSignedPermission,long userNumCur,long userNum1,bool isSig1Blank,long userNum2,bool isSig2Blank) {
			if(hasEditSignedPermission) {
				return true;
			}
			bool isBox1Ok=isSig1Blank || userNum1==userNumCur;
			bool isBox2Ok=isSig2Blank || userNum2==userNumCur;
			return isBox1Ok && isBox2Ok;
		}

		///<summary>True when the user may sign (or clear) the second box: it is blank, or it holds the user's own signature, or the user has Group Note
		///Edit Signed. Signing the second box does not change the text, so a user who may not edit someone else's signed note can still co-sign it.
		///The window also requires the note's permission and that the user may sign notes.</summary>
		public static bool CanSignBox2(bool hasEditSignedPermission,long userNumCur,long userNum2,bool isSig2Blank) {
			return hasEditSignedPermission || isSig2Blank || userNum2==userNumCur;
		}

		///<summary>Null when the two signatures are allowed together, otherwise the message (not yet translated). Both boxes signed by the same user
		///is refused: the second signature is for a second person.</summary>
		public static string GetSignersError(long userNum1,bool isSig1Blank,long userNum2,bool isSig2Blank) {
			if(isSig1Blank || isSig2Blank) {
				return null;
			}
			if(userNum1==userNum2) {
				return "The second signature must be made by a different user than the first signature.";
			}
			return null;
		}
		#endregion Who may edit and sign

		#region Saving
		///<summary>The second signer to alert ("signature cleared") when saving replaced or removed the second signature of the note as it was
		///(procedureOld) and that signer is not one of listUserNumsActing (the user at the keyboard and the users switched to in the window). 0 when
		///nobody is alerted. isSig2Changed: the second box was cleared or signed again in the window.</summary>
		public static long GetSecondSignerToAlert(Procedure procedureOld,bool isSig2Changed,List<long> listUserNumsActing) {
			if(procedureOld==null || !isSig2Changed || string.IsNullOrEmpty(procedureOld.Signature2) || procedureOld.UserNum2==0) {
				return 0;
			}
			if(listUserNumsActing!=null && listUserNumsActing.Contains(procedureOld.UserNum2)) {
				return 0;
			}
			return procedureOld.UserNum2;
		}

		///<summary>Text for the alert to a signer whose signature was cleared, in the words the Group Note window has always used, plus which box.</summary>
		public static string GetSignatureClearedAlertText(Procedure procedureOld,string procCode,string provAbbr,bool isSecondSignature) {
			string text="Group Note Changed for PatNum: "+procedureOld.PatNum
				+" Provider: "+provAbbr
				+" Date: "+procedureOld.ProcDate.ToShortDateString()
				+" Procedure Code: "+procCode;
			if(isSecondSignature) {
				text+=" (Signature 2)";
			}
			return text;
		}

		///<summary>The part of the group note's edit log about the second signature, or "" when it did not change: "Second signature by X" when it
		///was signed, "Second signature by X cleared" when it was removed.</summary>
		public static string GetSecondSignatureLogPart(Procedure procedureOld,Procedure procedureSaved,Func<long,string> funcGetUserName) {
			string sigOld=procedureOld.Signature2??"";
			string sigNew=procedureSaved.Signature2??"";
			if(sigOld==sigNew && procedureOld.UserNum2==procedureSaved.UserNum2) {
				return "";
			}
			if(sigNew!="") {
				return Lans.g("GroupNotes","Second signature by")+" "+funcGetUserName(procedureSaved.UserNum2);
			}
			if(sigOld!="") {
				return Lans.g("GroupNotes","Second signature by")+" "+funcGetUserName(procedureOld.UserNum2)+" "+Lans.g("GroupNotes","cleared");
			}
			return "";
		}

		///<summary>Adds the second signature's log part to the group note's edit log text (GroupNotes.GetEditLogText, "" when nothing else changed).</summary>
		public static string AppendSecondSignatureLog(string logText,string logPart,DateTime dateProc) {
			if(string.IsNullOrEmpty(logPart)) {
				return logText??"";
			}
			if(string.IsNullOrEmpty(logText)) {
				return Lans.g("GroupNotes","Group note")+" "+dateProc.ToShortDateString()+". "+logPart+".";
			}
			return logText+" "+logPart+".";
		}
		#endregion Saving

		#region Progress notes
		///<summary>The progress notes' Signed column for a procedure note. "Signed" when only the first box is signed, as before. When the second box is
		///signed, both signers: "Signed: A, B" (or "Signed: B" when only the second box is signed). Printed progress notes print the column.</summary>
		public static string GetSignedText(bool isSig1Present,bool isSig2Present,string userName1,string userName2) {
			string signed=Lans.g("ChartModule","Signed");
			if(!isSig2Present) {
				return isSig1Present ? signed : "";
			}
			List<string> listNames=new List<string>();
			if(isSig1Present) {
				listNames.Add(userName1??"");
			}
			listNames.Add(userName2??"");
			listNames.RemoveAll(x => x=="");
			if(listNames.Count==0) {
				return signed;
			}
			return signed+": "+string.Join(", ",listNames);
		}

		///<summary>Fills the Signed column of a progress notes row from the newest procnote row (ChartModules: columns SigPresent, Sig2Present and
		///UserNum2). Does nothing when the query has no second signature columns.</summary>
		public static void FillProgNotesSigned(DataRow row,DataRow rowNote,string userName1,Dictionary<string,string> dictUserNames) {
			if(!rowNote.Table.Columns.Contains("Sig2Present") || !rowNote.Table.Columns.Contains("UserNum2")) {
				return;
			}
			bool isSig1Present=rowNote["SigPresent"].ToString()=="1";
			bool isSig2Present=rowNote["Sig2Present"].ToString()=="1";
			row["signature"]=GetSignedText(isSig1Present,isSig2Present,userName1,GetUserName(rowNote["UserNum2"].ToString(),dictUserNames));
		}

		///<summary>Audit mode in the progress notes lists every version of a note. A version with a second signature gets "(signature 2: B)" after
		///its header.</summary>
		public static string GetAuditSecondSignatureText(DataRow rowNote,Dictionary<string,string> dictUserNames) {
			if(!rowNote.Table.Columns.Contains("Sig2Present") || rowNote["Sig2Present"].ToString()!="1") {
				return "";
			}
			return "  ("+Lans.g("ChartModule","signature 2")+": "+GetUserName(rowNote["UserNum2"].ToString(),dictUserNames)+")";
		}

		private static string GetUserName(string userNum,Dictionary<string,string> dictUserNames) {
			if(dictUserNames!=null && dictUserNames.TryGetValue(userNum,out string userName)) {
				return userName;
			}
			return Userods.GetName(PIn.Long(userNum));
		}
		#endregion Progress notes
	}
}
