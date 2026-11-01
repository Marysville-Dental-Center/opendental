// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using ImagingCapture.Uvc;

namespace ImagingCapture.Camera {
	///<summary>Which state of the masked bits counts as active. High: every bit in the mask is 1. Low: every bit in the mask is 0.
	///For a single-bit mask, the two are simply "bit set" and "bit clear". The names are the ActiveLevel values in capture.ini.</summary>
	public enum ControlLevel {
		High=0,
		Low=1,
	}

	///<summary>One standard camera control: a control interface and one of its property numbers (CameraControl 0-6, VideoProcAmp
	///0-9, the Windows SDK numbering). Nothing here is specific to any camera.</summary>
	public struct ControlId:IEquatable<ControlId> {
		public UvcControlInterface Interface { get; }
		public int Property { get; }

		public ControlId(UvcControlInterface controlInterface,int property) {
			Interface=controlInterface;
			Property=property;
		}

		///<summary>True when the property number is one of the interface's standard properties.</summary>
		public bool IsValid {
			get {
				return UvcControls.IsValidProperty(Interface,Property);
			}
		}

		///<summary>The SDK property name, for example "Focus" or "Gain".</summary>
		public string Name {
			get {
				return UvcControls.GetPropertyName(Interface,Property);
			}
		}

		///<summary>Every standard property of both interfaces: CameraControl 0-6, then VideoProcAmp 0-9.</summary>
		public static List<ControlId> GetAll() {
			List<ControlId> listControls=new List<ControlId>();
			foreach(UvcControlInterface controlInterface in new[] { UvcControlInterface.CameraControl,UvcControlInterface.VideoProcAmp }) {
				for(int property=0;property<UvcControls.GetPropertyCount(controlInterface);property++) {
					listControls.Add(new ControlId(controlInterface,property));
				}
			}
			return listControls;
		}

		///<summary>Orders CameraControl before VideoProcAmp, then by property number, the order GetAll uses.</summary>
		internal int SortKey {
			get {
				return (int)Interface*1000+Property;
			}
		}

		public bool Equals(ControlId other) {
			return Interface==other.Interface && Property==other.Property;
		}

		public override bool Equals(object obj) {
			return obj is ControlId && Equals((ControlId)obj);
		}

		public override int GetHashCode() {
			return ((int)Interface<<16)^Property;
		}

		public static bool operator ==(ControlId controlId1,ControlId controlId2) {
			return controlId1.Equals(controlId2);
		}

		public static bool operator !=(ControlId controlId1,ControlId controlId2) {
			return !controlId1.Equals(controlId2);
		}

		///<summary>"CameraControl 6 (Focus)".</summary>
		public override string ToString() {
			return Interface+" "+Property+" ("+Name+")";
		}
	}

	///<summary>A complete control input: which control is read, which bits of its value matter, and which level of those bits is
	///active. Immutable. Used by the control-poll trigger, level inputs, Learn and the live test.</summary>
	public sealed class ControlBinding {
		public ControlId Control { get; }
		///<summary>The bits that matter. Never 0.</summary>
		public int Mask { get; }
		public ControlLevel ActiveLevel { get; }

		///<summary>Throws ArgumentException when the property is not a standard property of the interface or the mask is 0.</summary>
		public ControlBinding(ControlId control,int mask,ControlLevel activeLevel) {
			if(!control.IsValid) {
				throw new ArgumentException("Not a standard property: "+control,nameof(control));
			}
			if(mask==0) {
				throw new ArgumentException("The mask must have at least one bit.",nameof(mask));
			}
			Control=control;
			Mask=mask;
			ActiveLevel=activeLevel;
		}

		///<summary>True when value is at the active level: every mask bit set (High) or every mask bit clear (Low). A value with some
		///mask bits set and others clear is active for neither.</summary>
		public bool IsActive(int value) {
			int masked=value & Mask;
			if(ActiveLevel==ControlLevel.High) {
				return masked==Mask;
			}
			return masked==0;
		}

		///<summary>True when Mask has exactly one bit.</summary>
		public bool IsSingleBit {
			get {
				return IsOneBit(Mask);
			}
		}

		///<summary>True when value has exactly one bit set.</summary>
		public static bool IsOneBit(int value) {
			return value!=0 && (value & (value-1))==0;
		}

		///<summary>"CameraControl 6 (Focus), mask 0x10, High".</summary>
		public override string ToString() {
			return Control+", mask "+ControlValueFormat.ToHex(Mask)+", "+ActiveLevel;
		}
	}
}
