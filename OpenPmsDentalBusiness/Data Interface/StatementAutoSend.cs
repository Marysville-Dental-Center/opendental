// ODOPTS: Open Dental 25.3.48's StatementAutoSendEmail and StatementAutoSendSMS (the settings of its Statement Send Setup window of 25.1; the
// meaning of their values is not public, so the values are INFERRED, docs/port-notes/ODOPTS.md).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>How statements are sent when billing creates them. The stored numbers are Open Dental's; their meaning is INFERRED (not public).
	///</summary>
	public enum StatementAutoSendMode {
		///<summary>0. Statements wait in the Billing List until someone sends them, as in 24.3.</summary>
		Never=0,
		///<summary>1. The Billing List asks whether to send the new statements now.</summary>
		Ask=1,
		///<summary>2. The new statements are sent as soon as the Billing List opens with them.</summary>
		Always=2,
	}

	///<summary>A texting provider that can text statements. None exists in this program: Open Dental's hosted texting was removed, and choosing a
	///provider is an owner decision (docs/owner-questions.md). A provider package sets StatementAutoSend.TextSender.</summary>
	public interface IStatementTextSender {
		///<summary>Texts the statements to their patients and marks what was sent. Returns the number texted.</summary>
		int SendTexts(List<long> listStatementNums);
	}

	///<summary>What the Billing List does about texts for the statements it was opened with.</summary>
	public class StatementTextResult {
		///<summary>The number of statements texted. Always 0 without a texting provider.</summary>
		public int CountSent;
		///<summary>Text for the user; blank when there is nothing to say.</summary>
		public string Message="";
	}

	///<summary>Sending statements automatically when Billing creates them (Billing Options, Create List), by e-mail and by text. E-mail goes through
	///the Billing List's own send path for the new statements whose mode is E-mail: the statement PDF to the patient's e-mail address, as the Send
	///button does. Texts need a texting provider (TextSender), which this program does not have, so no text is sent and the Billing List says so.
	///</summary>
	public static class StatementAutoSend {
		///<summary>Open Dental's preference row for e-mail, read and written by name outside PrefName so the office's value carries over. A missing
		///row reads as Never (INFERRED; Open Dental's default is not public).</summary>
		public const string PREF_EMAIL="StatementAutoSendEmail";
		///<summary>Open Dental's preference row for texts. A missing row reads as Never.</summary>
		public const string PREF_SMS="StatementAutoSendSMS";
		///<summary>The window's note about texts.</summary>
		public const string TEXT_NEEDS_PROVIDER="Texting statements needs a texting provider. None is set up, so no statement is texted.";
		///<summary>Set by a texting provider package. Null: no provider, nothing is texted.</summary>
		public static IStatementTextSender TextSender=null;

		///<summary>The mode a stored value stands for: 1 Ask, 2 Always; a missing, blank, unreadable or any other value is Never, so an unknown value
		///never sends anything.</summary>
		public static StatementAutoSendMode GetModeFromValue(string valueString) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrWhiteSpace(valueString) || !int.TryParse(valueString.Trim(),out int value)) {
				return StatementAutoSendMode.Never;
			}
			if(value==(int)StatementAutoSendMode.Ask) {
				return StatementAutoSendMode.Ask;
			}
			if(value==(int)StatementAutoSendMode.Always) {
				return StatementAutoSendMode.Always;
			}
			return StatementAutoSendMode.Never;
		}

		public static StatementAutoSendMode GetEmailMode() {
			Meth.NoCheckMiddleTierRole();
			return GetModeFromValue(PrefC.GetRawOrDefault(PREF_EMAIL,null));
		}

		public static StatementAutoSendMode GetSmsMode() {
			Meth.NoCheckMiddleTierRole();
			return GetModeFromValue(PrefC.GetRawOrDefault(PREF_SMS,null));
		}

		///<summary>The stored number of a mode.</summary>
		public static string GetValue(StatementAutoSendMode mode) {
			Meth.NoCheckMiddleTierRole();
			return ((int)mode).ToString();
		}

		///<summary>Saves both settings and updates this workstation's cache. A missing row already reads as Never, so saving Never writes nothing
		///then. A row whose value reads as the mode chosen is left as it is. Returns true if a stored value changed; the caller signals
		///InvalidType.Prefs and makes the log entries.</summary>
		public static bool SetModes(StatementAutoSendMode modeEmail,StatementAutoSendMode modeSms) {
			Meth.NoCheckMiddleTierRole();
			bool isChanged=SetMode(PREF_EMAIL,modeEmail);
			isChanged|=SetMode(PREF_SMS,modeSms);
			return isChanged;
		}

		private static bool SetMode(string prefName,StatementAutoSendMode mode) {
			string valueStored=PrefC.GetRawOrDefault(prefName,null);
			if(valueStored==null && mode==StatementAutoSendMode.Never) {
				return false;
			}
			if(valueStored!=null && GetModeFromValue(valueStored)==mode) {
				return false;
			}
			return PrefC.UpsertRaw(prefName,GetValue(mode));
		}

		///<summary>The text of a mode in the setup window and in log entries.</summary>
		public static string GetDescription(StatementAutoSendMode mode) {
			Meth.NoCheckMiddleTierRole();
			switch(mode) {
				case StatementAutoSendMode.Ask:
					return Lans.g("StatementAutoSend","Ask");
				case StatementAutoSendMode.Always:
					return Lans.g("StatementAutoSend","Always");
				default:
					return Lans.g("StatementAutoSend","Never");
			}
		}

		///<summary>True when either setting can do something when statements are created, so Billing Options notes which statements it creates.
		///</summary>
		public static bool IsAnyOn() {
			Meth.NoCheckMiddleTierRole();
			return GetEmailMode()!=StatementAutoSendMode.Never || GetSmsMode()!=StatementAutoSendMode.Never;
		}

		///<summary>The statements a billing run created: the unsent statements after it that were not unsent before it. Keeps the order of
		///listUnsentAfter.</summary>
		public static List<long> GetCreatedStatementNums(List<long> listUnsentBefore,List<long> listUnsentAfter) {
			Meth.NoCheckMiddleTierRole();
			HashSet<long> hashSetBefore=new HashSet<long>(listUnsentBefore??new List<long>());
			return (listUnsentAfter??new List<long>()).Where(x => !hashSetBefore.Contains(x)).Distinct().ToList();
		}

		///<summary>The new e-mail statements to send now: none for Never, all for Always, and for Ask all when funcAsk (asked once, with the count)
		///says yes. funcAsk is not called when there is nothing to send.</summary>
		public static List<long> GetEmailStatementNumsToSend(StatementAutoSendMode mode,List<long> listStatementNumsEmailCreated,Func<int,bool> funcAsk) {
			Meth.NoCheckMiddleTierRole();
			List<long> listStatementNums=(listStatementNumsEmailCreated??new List<long>()).Distinct().ToList();
			if(mode==StatementAutoSendMode.Never || listStatementNums.Count==0) {
				return new List<long>();
			}
			if(mode==StatementAutoSendMode.Ask && (funcAsk==null || !funcAsk(listStatementNums.Count))) {
				return new List<long>();
			}
			return listStatementNums;
		}

		///<summary>The question the Billing List asks in Ask mode.</summary>
		public static string GetEmailQuestion(int count) {
			Meth.NoCheckMiddleTierRole();
			return Lans.g("StatementAutoSend","Send the new e-mail statements now?")+" ("+count+")";
		}

		///<summary>The texting hook: what to do about texts for the statements a billing run created. Never, or no statements: nothing. Without a
		///texting provider (TextSender null): nothing is texted and the message says texting needs a provider. With one: Always texts them, Ask texts
		///them when funcAsk says yes.</summary>
		public static StatementTextResult SendTexts(StatementAutoSendMode mode,List<long> listStatementNumsCreated,Func<int,bool> funcAsk,
			IStatementTextSender textSender)
		{
			Meth.NoCheckMiddleTierRole();
			StatementTextResult result=new StatementTextResult();
			List<long> listStatementNums=(listStatementNumsCreated??new List<long>()).Distinct().ToList();
			if(mode==StatementAutoSendMode.Never || listStatementNums.Count==0) {
				return result;
			}
			if(textSender==null) {
				result.Message=Lans.g("StatementAutoSend","The new statements were not texted.")+" "+Lans.g("StatementAutoSend",TEXT_NEEDS_PROVIDER);
				return result;
			}
			if(mode==StatementAutoSendMode.Ask && (funcAsk==null || !funcAsk(listStatementNums.Count))) {
				return result;
			}
			result.CountSent=textSender.SendTexts(listStatementNums);
			result.Message=Lans.g("StatementAutoSend","Statements texted:")+" "+result.CountSent;
			return result;
		}
	}
}
