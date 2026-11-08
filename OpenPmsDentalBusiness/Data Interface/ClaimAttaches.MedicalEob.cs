// B-CLAIM, v25_2-account-medical-eob-prompt: attach a received medical claim's EOB to the patient's primary dental claims.
// Open Dental 25.2 added a preference that prompts for a Medical EOB on primary claims; only the release-note line is public, so the flow here is
// the fork's design (docs/specs/features-25.2.md). The setting is Open Dental's own preference row (PREF_NAME_MEDICAL_EOB_PROMPT), read by name
// like ApptTimeBars and ModuleSwitchBlur; its name comes from the office's Open Dental 25.3.48 database (docs/port-notes/SWMAP.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	public partial class ClaimAttaches {
		///<summary>Open Dental's preference row for "Prompt for Medical EOB on primary claims" (bool, off when missing). Its name is confirmed by the
		///setting names of the office's Open Dental 25.3.48 database (owner question Switch-over 1, 2026-11-07), so the office's value carries over as
		///it is. Read and written by name, outside PrefName: a database from 24.3 or a new database has no such row, and the row is written the first
		///time the setting is turned on in Preferences. Release v0.0 kept the setting in the row ForkClaimMedReceivedPromptForMedicalEob; the fork
		///migration step OdSettingNamesStep moves it here.</summary>
		public const string PREF_NAME_MEDICAL_EOB_PROMPT="ClaimCheckForMedicalEOB";
		///<summary>Prefix of the displayed name of an EOB file attached by the Medical EOB prompt.</summary>
		public const string MEDICAL_EOB_DISPLAY_PREFIX="MedicalEOB_";

		///<summary>True when "Prompt for Medical EOB on primary claims" is turned on. A missing row, an empty cache or an unreadable value reads as
		///off.</summary>
		public static bool IsMedicalEobPromptEnabled() {
			Meth.NoCheckMiddleTierRole();
			try {
				string valueString=PrefC.GetRawOrDefault(PREF_NAME_MEDICAL_EOB_PROMPT,null);//B-SERVER's shared helper for preference rows outside PrefName.
				return valueString!=null && PIn.Bool(valueString);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return false;
			}
		}

		///<summary>Saves "Prompt for Medical EOB on primary claims" and updates this workstation's cache. Inserts the row the first time the setting
		///is turned on; a missing row already reads as off, so saving the default writes nothing. Returns true if the stored value changed, so the
		///caller sends the Prefs signal.</summary>
		public static bool SetMedicalEobPromptEnabled(bool isEnabled) {
			Meth.NoCheckMiddleTierRole();
			string prefName=PREF_NAME_MEDICAL_EOB_PROMPT;
			if(PrefC.GetRawOrDefault(prefName,null)==null && !isEnabled) {
				return false;
			}
			if(IsMedicalEobPromptEnabled()==isEnabled) {
				return false;
			}
			return PrefC.UpsertRaw(prefName,POut.Bool(isEnabled));//B-SERVER's shared helper for fork-owned preference rows.
		}

		///<summary>True for a medical or institutional claim, the claims that start the "prompt for primary claims" flow.</summary>
		public static bool IsMedicalClaim(Claim claim) {
			Meth.NoCheckMiddleTierRole();
			return claim!=null && claim.MedType.In(EnumClaimMedType.Medical,EnumClaimMedType.Institutional);
		}

		///<summary>The primary dental claims (ClaimType P, MedType Dental) that are Unsent or Hold Until Pri Received and share a procedure with the
		///received medical claim: the claims the Medical EOB prompt asks about. Empty when claimMedical is not a medical or institutional claim, or is
		///a preauthorization (its EOB is an estimate, not the payment the dental carrier asks for).</summary>
		public static List<Claim> GetDentalClaimsForMedicalEob(Claim claimMedical,List<ClaimProc> listClaimProcsMedical) {
			Meth.NoCheckMiddleTierRole();
			if(!IsMedicalClaim(claimMedical) || claimMedical.ClaimType=="PreAuth" || listClaimProcsMedical==null || listClaimProcsMedical.Count==0) {
				return new List<Claim>();
			}
			return Claims.GetPrimaryOrSecondaryClaimsNotReceived(listClaimProcsMedical,isSecondaryClaim:false)
				.FindAll(x => x.ClaimNum!=claimMedical.ClaimNum && x.MedType==EnumClaimMedType.Dental)
				.OrderBy(x => x.DateService).ThenBy(x => x.ClaimNum).ToList();
		}

		///<summary>The EOB files already attached to the payments of the medical claim (eobattach rows of every claim payment its claimprocs are
		///attached to), oldest first. Empty while the medical claim's payment is not finalized.</summary>
		public static List<EobAttach> GetMedicalEobAttaches(long claimNumMedical) {
			Meth.NoCheckMiddleTierRole();
			if(claimNumMedical==0) {
				return new List<EobAttach>();
			}
			List<long> listClaimPaymentNums=ClaimProcs.RefreshForClaims(new List<long> { claimNumMedical })
				.Where(x => x.ClaimPaymentNum>0).Select(x => x.ClaimPaymentNum).Distinct().ToList();
			List<EobAttach> listEobAttaches=new List<EobAttach>();
			for(int i=0;i<listClaimPaymentNums.Count;i++) {
				listEobAttaches.AddRange(EobAttaches.Refresh(listClaimPaymentNums[i]));
			}
			return listEobAttaches.OrderBy(x => x.DateTCreated).ThenBy(x => x.EobAttachNum).ToList();
		}

		///<summary>The claim's AttachedFlags with the EoB paperwork keyword added. Every other keyword (Mail or Elect, Submitted, Unsup...) is kept.
		///</summary>
		public static string AddEobFlag(string attachedFlags) {
			Meth.NoCheckMiddleTierRole();
			List<string> listFlags=ParseAttachedFlags(attachedFlags);
			if(!listFlags.Contains(FlagEoB)) {
				listFlags.Insert(0,FlagEoB);
			}
			return JoinAttachedFlags(listFlags);
		}

		///<summary>A new unique file name for the claim attachment folder, built like the Edit Claim window builds them (date, ticks, random).
		///</summary>
		public static string GetNewAttachFileName(string extension) {
			Meth.NoCheckMiddleTierRole();
			Random random=new Random();
			return DateTime.Now.ToString("yyyyMMdd")+"_"+DateTime.Now.TimeOfDay.Ticks.ToString()+random.Next(1000).ToString()+(extension??"");
		}

		///<summary>Copies a file into the claim attachment folder (EmailAttachments in the A to Z folder), adds it to the claim's attachments, sets the
		///EoB paperwork flag, saves the claim and writes a ClaimEdit audit entry. Needs the A to Z folder on this computer. Throws ODException when
		///it is not used, when the claim no longer exists or when the file is missing.</summary>
		public static ClaimAttach AttachFileToClaim(long claimNum,string sourceFilePath,string displayedFileName) {
			Meth.NoCheckMiddleTierRole();
			if(PrefC.AtoZfolderUsed!=DataStorageType.LocalAtoZ) {
				throw new ODException(Lans.g("ClaimAttaches","Claim attachments need the A to Z images folder."));
			}
			if(string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath)) {
				throw new ODException(Lans.g("ClaimAttaches","File not found:")+" "+sourceFilePath);
			}
			Claim claim=Claims.GetClaim(claimNum);
			if(claim==null) {
				throw new ODException(Lans.g("ClaimAttaches","The claim has been deleted by another user."));
			}
			string actualFileName=GetNewAttachFileName(Path.GetExtension(sourceFilePath));
			File.Copy(sourceFilePath,ODFileUtils.CombinePaths(EmailAttaches.GetAttachPath(),actualFileName));
			ClaimAttach claimAttach=new ClaimAttach();
			claimAttach.ClaimNum=claim.ClaimNum;
			claimAttach.DisplayedFileName=string.IsNullOrWhiteSpace(displayedFileName) ? Path.GetFileName(sourceFilePath) : displayedFileName;
			claimAttach.ActualFileName=actualFileName;
			claim.Attachments.Add(claimAttach);
			claim.AttachedFlags=AddEobFlag(claim.AttachedFlags);
			Claims.Update(claim);//Rewrites the claim's attachment rows from claim.Attachments.
			SecurityLogs.MakeLogEntry(EnumPermType.ClaimEdit,claim.PatNum,"Medical EOB attached to the claim from "+claim.DateService.ToShortDateString()
				+": "+claimAttach.DisplayedFileName,claim.ClaimNum,claim.SecDateTEdit);
			return claimAttach;
		}

		///<summary>Sets the claims of the "Outstanding primary/secondary claims" prompt (ClaimL.PromptForPrimaryOrSecondaryClaim) to Waiting to Send
		///("W") before it queues, sends or prints them, and keeps their attachments. Those claims come from Claims.GetPrimaryOrSecondaryClaimsNotReceived,
		///which does not load Claim.Attachments, and the one-argument Claims.Update rewrites claimattach from that list: on these claims it deleted every
		///attachment, among them a medical EOB the Medical EOB prompt had attached just before. With doSaveToDb, each claim row is saved with
		///Claims.Update(claim,claimOld), which does not touch claimattach. Without it (the caller recalculates the secondary claims and saves them
		///through Claims.CalculateAndUpdate, which uses the one-argument Claims.Update), each claim's attachments are loaded so that save keeps them.
		///</summary>
		public static void SetClaimsWaitingToSend(List<Claim> listClaims,bool doSaveToDb) {
			Meth.NoCheckMiddleTierRole();
			for(int i=0;i<listClaims.Count;i++) {
				Claim claimOld=listClaims[i].Copy();
				listClaims[i].ClaimStatus="W";
				if(doSaveToDb) {
					Claims.Update(listClaims[i],claimOld);
					continue;
				}
				Claim claimDb=Claims.GetClaim(listClaims[i].ClaimNum);
				if(claimDb!=null) {
					listClaims[i].Attachments=claimDb.Attachments;
				}
			}
		}

		///<summary>Attaches one of the medical claim payment's EOB files to the dental claim (AttachFileToClaim). The displayed name is
		///"MedicalEOB_" plus the EOB's file name.</summary>
		public static ClaimAttach AttachMedicalEob(long claimNumDental,EobAttach eobAttach) {
			Meth.NoCheckMiddleTierRole();
			if(eobAttach==null) {
				throw new ODException(Lans.g("ClaimAttaches","No EOB file."));
			}
			string path=EobAttaches.GetPath(eobAttach.EobAttachNum);
			return AttachFileToClaim(claimNumDental,path,MEDICAL_EOB_DISPLAY_PREFIX+Path.GetFileName(eobAttach.FileName));
		}
	}
}
