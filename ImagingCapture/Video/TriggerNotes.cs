// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ImagingCapture.Camera;

namespace ImagingCapture.Video {
	///<summary>The trigger lines of the Video window's settings (design 8.9: an unsupported trigger property turns the trigger off,
	///logs one line and shows a note in settings). English text; the problem texts come from TriggerEngine.</summary>
	public static class TriggerNotes {
		public const string TextNoneSetUp="No camera button is set up. Use Learn to set one up.";
		public const string TextNotRunning="Camera buttons work while the camera is running.";

		///<summary>One line for each trigger and level that capture.ini enables: "on", or "off, because ..." with the reason. When
		///none is enabled, TextNoneSetUp. When the camera is not running (engine null) and some are enabled, TextNotRunning.</summary>
		public static List<string> GetLines(TriggerEngine triggerEngine,CameraConfig cameraConfig) {
			cameraConfig=cameraConfig??new CameraConfig();
			List<LevelInputConfig> listLevels=(cameraConfig.Levels??new List<LevelInputConfig>()).Where(x => x!=null && x.Enabled).OrderBy(x => x.Number).ToList();
			bool isAnyEnabled=cameraConfig.StillEnabled || (cameraConfig.ControlPoll?.Enabled??false) || listLevels.Count>0;
			List<string> listLines=new List<string>();
			if(!isAnyEnabled) {
				listLines.Add(TextNoneSetUp);
				return listLines;
			}
			if(triggerEngine==null) {
				listLines.Add(TextNotRunning);
				return listLines;
			}
			if(cameraConfig.StillEnabled) {
				listLines.Add(GetLine("Still-pin camera button",triggerEngine.StillState,triggerEngine.StillProblem));
			}
			if(cameraConfig.ControlPoll?.Enabled??false) {
				listLines.Add(GetLine("Control camera button",triggerEngine.ControlPollState,triggerEngine.ControlPollProblem));
			}
			foreach(LevelInputConfig levelInputConfig in listLevels) {
				string label="Level "+levelInputConfig.Number.ToString(CultureInfo.InvariantCulture)+" (pause preview)";
				listLines.Add(GetLine(label,triggerEngine.GetLevelState(levelInputConfig.Number),triggerEngine.GetLevelProblem(levelInputConfig.Number)));
			}
			return listLines;
		}

		///<summary>"Label: on", "Label: off" or "Label: off, because problem".</summary>
		public static string GetLine(string label,TriggerInputState triggerInputState,string problem) {
			if(triggerInputState==TriggerInputState.Armed) {
				return label+": on";
			}
			problem=(problem??"").Trim();
			if(triggerInputState==TriggerInputState.Off || problem=="") {
				return label+": off";
			}
			return label+": off, because "+problem;
		}
	}
}
