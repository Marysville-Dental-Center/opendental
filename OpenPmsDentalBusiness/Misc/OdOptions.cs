// ODOPTS (owner, 2026-11-07: "why not just build it?"): the eight Open Dental 25.3 options this program had built without, now built and read
// under Open Dental's own setting names so the office's values carry over at the switch-over. See docs/port-notes/ODOPTS.md.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using CodeBase;

namespace OpenPmsDentalBusiness {
	///<summary>The eight Open Dental 25.3 options of package ODOPTS and the shared helpers of their setup windows. Each option is Open Dental's own
	///preference row, read and written by name outside PrefName (PrefC.GetRawOrDefault, PrefC.UpsertRaw), with the default this program gives a
	///missing row (a database from 24.3 or a new database has none). The rows stay as Open Dental wrote them: nothing converts them, and a value is
	///only written when someone changes the setting. docs/port-notes/ODOPTS.md says which meanings and defaults are INFERRED.</summary>
	public static class OdOptions {
		///<summary>Open Dental's names of the eight rows, in the order of docs/port-notes/ODOPTS.md.</summary>
		public static readonly string[] ArrayPrefNames={
			PerioMeasures.PrefNameRedAttGing,
			DomainLogin.PREF_FOR_SIG_BOXES,
			ClaimFeeForSecondary.PREF_NAME,
			EFormImport.PREF_ALLERGIES_AS_OTHER,
			EFormImport.PREF_PROBLEMS_AS_OTHER,
			InsHistCategories.PREF_PERIO_SCALING_CODE,
			StatementAutoSend.PREF_EMAIL,
			StatementAutoSend.PREF_SMS,
		};

		///<summary>Reads a bool row: a missing row, an empty cache or a blank value reads as defaultValue; any other value as PIn.Bool reads it (Open
		///Dental stores 1 and 0). Never throws.</summary>
		public static bool GetBool(string prefName,bool defaultValue) {
			Meth.NoCheckMiddleTierRole();
			return GetBoolFromValue(PrefC.GetRawOrDefault(prefName,null),defaultValue);
		}

		///<summary>The bool a stored value stands for. Null or blank: defaultValue.</summary>
		public static bool GetBoolFromValue(string valueString,bool defaultValue) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrWhiteSpace(valueString)) {
				return defaultValue;
			}
			try {
				return PIn.Bool(valueString.Trim());
			}
			catch(Exception ex) {
				ex.DoNothing();
				return defaultValue;
			}
		}

		///<summary>Saves a bool row and updates this workstation's cache. A missing row already reads as defaultValue, so saving the default writes
		///nothing then. Returns true if the stored value changed; the caller signals InvalidType.Prefs and makes the security log entry.</summary>
		public static bool SetBool(string prefName,bool value,bool defaultValue) {
			Meth.NoCheckMiddleTierRole();
			string valueStored=PrefC.GetRawOrDefault(prefName,null);
			if(valueStored==null && value==defaultValue) {
				return false;
			}
			if(valueStored!=null && GetBoolFromValue(valueStored,defaultValue)==value && valueStored.Trim()==POut.Bool(value)) {
				return false;
			}
			return PrefC.UpsertRaw(prefName,POut.Bool(value));
		}

		///<summary>Saves a text row as it is and updates this workstation's cache. A missing row already reads as blank, so saving blank writes
		///nothing then. Returns true if the stored value changed.</summary>
		public static bool SetString(string prefName,string value) {
			Meth.NoCheckMiddleTierRole();
			value=(value??"").Trim();
			string valueStored=PrefC.GetRawOrDefault(prefName,null);
			if(valueStored==null && value=="") {
				return false;
			}
			if(valueStored==value) {
				return false;
			}
			return PrefC.UpsertRaw(prefName,value);
		}

		///<summary>"On" or "off", for log entries.</summary>
		public static string GetOnOff(bool isOn) {
			return isOn ? "on" : "off";
		}

		///<summary>The security log text of a changed setting: its label as the window shows it, the old and the new value, and Open Dental's name of
		///the row (so the Audit Trail can be matched with Open Dental's documentation). Blank values show as "blank".</summary>
		public static string GetChangeLogText(string label,string prefName,string valueOld,string valueNew) {
			return label+" ("+prefName+") "+Lans.g("OdOptions","changed from")+" '"+GetShown(valueOld)+"' "+Lans.g("OdOptions","to")+" '"
				+GetShown(valueNew)+"'.";
		}

		private static string GetShown(string value) {
			if(string.IsNullOrWhiteSpace(value)) {
				return "blank";
			}
			return value.Trim();
		}

		///<summary>Makes the Setup security log entry of a changed setting (GetChangeLogText). Does nothing when the two values are the same.</summary>
		public static void LogChange(EnumPermType permType,string label,string prefName,string valueOld,string valueNew) {
			if(GetShown(valueOld)==GetShown(valueNew)) {
				return;
			}
			SecurityLogs.MakeLogEntry(permType,0,GetChangeLogText(label,prefName,valueOld,valueNew));
		}
	}
}
