// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ImagingDeviceManager.TwainProtocol {
	///<summary>A capability container as a source returned it, with every item read: TW_ONEVALUE (one item), TW_ENUMERATION and TW_ARRAY
	///(all items, and for an enumeration the current and default index) or TW_RANGE (its bounds). Only integer, boolean and FIX32 items are
	///read (FIX32 as its raw 32-bit pattern); a container of strings or frames has no items here.</summary>
	internal sealed class TwainCapabilityValues {
		public ushort ConType;
		public ushort ItemType;
		///<summary>TW_ONEVALUE: the value. TW_ENUMERATION and TW_ARRAY: the items in order. TW_RANGE: empty.</summary>
		public List<int> ListItems=new List<int>();
		///<summary>TW_ENUMERATION only; -1 otherwise.</summary>
		public int CurrentIndex=-1;
		public int DefaultIndex=-1;
		public int RangeMin;
		public int RangeMax;
		public int RangeStep;
		public int RangeDefault;
		public int RangeCurrent;

		///<summary>True when value is one the container allows: the one value, an item of the list, or a step of the range.</summary>
		public bool Contains(int value) {
			if(ConType==Tw.TWON_RANGE) {
				if(value<RangeMin || value>RangeMax) {
					return false;
				}
				return RangeStep<=0 || ((long)value-RangeMin)%RangeStep==0;
			}
			return ListItems.Contains(value);
		}

		///<summary>The current value: the one value, the current item of an enumeration, the current value of a range. Null for an array, or
		///an enumeration whose current index is out of bounds.</summary>
		public int? Current {
			get {
				switch(ConType) {
					case Tw.TWON_ONEVALUE:
						return ListItems.Count>0 ? ListItems[0] : (int?)null;
					case Tw.TWON_ENUMERATION:
						return CurrentIndex>=0 && CurrentIndex<ListItems.Count ? ListItems[CurrentIndex] : (int?)null;
					case Tw.TWON_RANGE:
						return RangeCurrent;
					default:
						return null;
				}
			}
		}

		///<summary>"8|16", "0x123A|0x124B" (hex), or "1..100 step 1": the values for a log line.</summary>
		public string Describe(bool isHex) {
			Func<int,string> format=x => isHex ? TwainSession.ToHex(x) : x.ToString(CultureInfo.InvariantCulture);
			if(ConType==Tw.TWON_RANGE) {
				return format(RangeMin)+".."+format(RangeMax)+" step "+RangeStep.ToString(CultureInfo.InvariantCulture);
			}
			return string.Join("|",ListItems.Select(format));
		}
	}

	///<summary>What happened to a MSG_SET on a custom capability named in the device profile (design 7.1 CAP-3: QUERYSUPPORT first, then
	///MSG_SET in state 4; TWCC_CAPUNSUPPORTED is tolerated and logged, and the feature counts as absent).</summary>
	internal enum TwainCustomCapOutcome {
		///<summary>MSG_SET returned TWRC_SUCCESS.</summary>
		Set,
		///<summary>The source does not have the capability (TWCC_CAPUNSUPPORTED to MSG_QUERYSUPPORT or MSG_SET).</summary>
		Unsupported,
		///<summary>MSG_QUERYSUPPORT says the capability cannot be set.</summary>
		NotSettable,
		///<summary>MSG_SET failed with another condition code, or returned TWRC_CHECKSTATUS (the value was changed to another one).</summary>
		Refused,
		///<summary>Nothing was sent: the session is not in state 4, or memory for the container ran out.</summary>
		NotSent,
	}
}
