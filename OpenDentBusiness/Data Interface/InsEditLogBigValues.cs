// Insurance edit logs keep values longer than 255 characters (Open Dental 25.4, feature v25_4-family-inseditlog-bigvalues, package M2-CARRIER in
// docs/specs/port-backlog.md). inseditlog and inseditpatlog have varchar(255) OldValue/NewValue columns; 25.4 added the text columns
// OldValueBig/NewValueBig (M2-01's columns). A long value goes in the Big column and the short one is left empty.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenDentBusiness {
	///<summary>Puts long insurance edit log values in the OldValueBig/NewValueBig columns, and reads back whichever column is filled.</summary>
	public class InsEditLogBigValues {
		///<summary>The width of the OldValue and NewValue columns (varchar(255), counted in characters).</summary>
		public const int MaxShortLength=255;
		///<summary>A MySQL text column holds 65,535 bytes.</summary>
		public const int MaxTextBytes=65535;

		///<summary>Moves a value longer than 255 characters from OldValue/NewValue to OldValueBig/NewValueBig and leaves the short column empty. A value
		///of 255 characters or fewer stays where it is. A value that is already in a Big column is kept. Called by InsEditLogs.Insert and InsertMany,
		///so every way a log is written follows the rule.</summary>
		public static void MoveLongValues(InsEditLog insEditLog) {
			Meth.NoCheckMiddleTierRole();
			if(insEditLog==null) {
				return;
			}
			insEditLog.OldValueBig=GetBig(insEditLog.OldValue,insEditLog.OldValueBig,out string oldValue);
			insEditLog.OldValue=oldValue;
			insEditLog.NewValueBig=GetBig(insEditLog.NewValue,insEditLog.NewValueBig,out string newValue);
			insEditLog.NewValue=newValue;
		}

		///<summary>The same as MoveLongValues, for the patient-level log (inseditpatlog). Called by InsEditPatLogs.InsertMany.</summary>
		public static void MoveLongValues(InsEditPatLog insEditPatLog) {
			Meth.NoCheckMiddleTierRole();
			if(insEditPatLog==null) {
				return;
			}
			insEditPatLog.OldValueBig=GetBig(insEditPatLog.OldValue,insEditPatLog.OldValueBig,out string oldValue);
			insEditPatLog.OldValue=oldValue;
			insEditPatLog.NewValueBig=GetBig(insEditPatLog.NewValue,insEditPatLog.NewValueBig,out string newValue);
			insEditPatLog.NewValue=newValue;
		}

		///<summary>The old value to show: OldValueBig when it is filled, otherwise OldValue.</summary>
		public static string GetOldValue(InsEditLog insEditLog) {
			Meth.NoCheckMiddleTierRole();
			return GetShown(insEditLog?.OldValue,insEditLog?.OldValueBig);
		}

		///<summary>The new value to show: NewValueBig when it is filled, otherwise NewValue.</summary>
		public static string GetNewValue(InsEditLog insEditLog) {
			Meth.NoCheckMiddleTierRole();
			return GetShown(insEditLog?.NewValue,insEditLog?.NewValueBig);
		}

		///<summary>The old value to show: OldValueBig when it is filled, otherwise OldValue.</summary>
		public static string GetOldValue(InsEditPatLog insEditPatLog) {
			Meth.NoCheckMiddleTierRole();
			return GetShown(insEditPatLog?.OldValue,insEditPatLog?.OldValueBig);
		}

		///<summary>The new value to show: NewValueBig when it is filled, otherwise NewValue.</summary>
		public static string GetNewValue(InsEditPatLog insEditPatLog) {
			Meth.NoCheckMiddleTierRole();
			return GetShown(insEditPatLog?.NewValue,insEditPatLog?.NewValueBig);
		}

		///<summary>Cuts a value to what a text column holds (65,535 bytes in UTF-8), never in the middle of a surrogate pair. Shorter values are
		///returned unchanged.</summary>
		public static string TruncateToText(string value) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(value) || value.Length*3<=MaxTextBytes || Encoding.UTF8.GetByteCount(value)<=MaxTextBytes) {
				return value??"";
			}
			int lengthLow=0;
			int lengthHigh=Math.Min(value.Length,MaxTextBytes);
			while(lengthLow<lengthHigh) {//The longest prefix that fits.
				int lengthMid=(lengthLow+lengthHigh+1)/2;
				if(Encoding.UTF8.GetByteCount(value.Substring(0,lengthMid))<=MaxTextBytes) {
					lengthLow=lengthMid;
				}
				else {
					lengthHigh=lengthMid-1;
				}
			}
			if(lengthLow>0 && char.IsHighSurrogate(value[lengthLow-1])) {
				lengthLow--;
			}
			return value.Substring(0,lengthLow);
		}

		///<summary>Returns the Big column's value and sets valueShort to what stays in the short column.</summary>
		private static string GetBig(string value,string valueBig,out string valueShort) {
			value=value??"";
			valueBig=valueBig??"";
			if(value.Length<=MaxShortLength) {
				valueShort=value;
				return TruncateToText(valueBig);
			}
			valueShort="";
			return TruncateToText(value);
		}

		private static string GetShown(string value,string valueBig) {
			if(!string.IsNullOrEmpty(valueBig)) {
				return valueBig;
			}
			return value??"";
		}
	}
}
