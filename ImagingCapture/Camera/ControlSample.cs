// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Uvc;

namespace ImagingCapture.Camera {
	///<summary>One poll of several camera controls at one moment: the values that were read successfully. Immutable.</summary>
	public sealed class ControlSample {
		private readonly Dictionary<ControlId,int> _dictValues;

		///<summary>When the sample was taken, on a monotonic clock in milliseconds.</summary>
		public long TimeMs { get; }

		public ControlSample(long timeMs,IDictionary<ControlId,int> dictValues) {
			TimeMs=timeMs;
			_dictValues=dictValues==null ? new Dictionary<ControlId,int>() : new Dictionary<ControlId,int>(dictValues);
		}

		///<summary>The controls read successfully, with their values.</summary>
		public IReadOnlyDictionary<ControlId,int> Values {
			get {
				return _dictValues;
			}
		}

		///<summary>False when the control was not read, or the read failed.</summary>
		public bool TryGetValue(ControlId control,out int value) {
			return _dictValues.TryGetValue(control,out value);
		}
	}

	///<summary>Reads camera controls for Learn and the live test, with Get only.</summary>
	public static class ControlSampler {
		///<summary>Every standard CameraControl and VideoProcAmp property whose Get succeeds now, in ControlId.GetAll order.</summary>
		public static List<ControlId> FindReadable(IFrameSource frameSource) {
			if(frameSource==null) {
				return new List<ControlId>();
			}
			return ControlId.GetAll().Where(x => frameSource.ReadControl(x.Interface,x.Property).IsOk).ToList();
		}

		///<summary>Reads each control once with Get. Controls whose read fails are left out of the sample.</summary>
		public static ControlSample Read(IFrameSource frameSource,IEnumerable<ControlId> controls,long timeMs) {
			Dictionary<ControlId,int> dictValues=new Dictionary<ControlId,int>();
			if(frameSource!=null && controls!=null) {
				foreach(ControlId control in controls) {
					UvcControlValue controlValue=frameSource.ReadControl(control.Interface,control.Property);
					if(controlValue.IsOk) {
						dictValues[control]=controlValue.Value;
					}
				}
			}
			return new ControlSample(timeMs,dictValues);
		}
	}
}
