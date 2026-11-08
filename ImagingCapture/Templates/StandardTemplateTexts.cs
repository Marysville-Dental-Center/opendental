// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ImagingCapture.Templates {
	///<summary>The texts of the "Add standard templates" window: what will be added or left alone, and what was done.</summary>
	public static class StandardTemplateTexts {
		public const string Intro="Adds the standard X-ray mount templates: FMX with horizontal or with vertical bitewings, and 4 bitewings "
			+"horizontal or vertical, with the slots of the program's own mount generator (the Generate button in the mount editor). The slots are in "
			+"exposure order, grouped by sensor size, "
			+"so the sensor is changed at most once. A template whose name a mount already has is left alone. The rotations are starting values: "
			+"confirm them for each sensor size on the phantom.";

		///<summary>One paragraph per template: its slots, its sensor order, its scale, and whether it is added or left alone.</summary>
		public static string GetPlanText(StandardTemplatePlan plan) {
			if(plan==null) {
				return "";
			}
			List<string> listParagraphs=new List<string>();
			foreach(StandardTemplatePlanItem item in plan.ListItems) {
				string status=item.IsAdded ? "Will be added."
					: "Already exists (mount "+item.MountDefExisting.MountDefNum.ToString(CultureInfo.InvariantCulture)+", \""+(item.MountDefExisting.Description??"").Trim()
						+"\"): left alone.";
				listParagraphs.Add(GetTemplateText(item.Template)+"\r\n  "+status);
			}
			return string.Join("\r\n\r\n",listParagraphs);
		}

		///<summary>"FMX horizontal BW: 18 slots; sensor: size 2 for slots 1-12, then size 1 for slots 13-18 (1 change). No scale: ...".</summary>
		public static string GetTemplateText(StandardTemplate template) {
			int countSwaps=template.CountSensorSwaps;
			return template.Description+": "+template.ListSlots.Count.ToString(CultureInfo.InvariantCulture)+" slots; sensor: "+template.GetSensorOrderText()
				+" ("+(countSwaps==0 ? "no change" : countSwaps==1 ? "1 change" : countSwaps.ToString(CultureInfo.InvariantCulture)+" changes")+").\r\n  "
				+template.ScaleNote;
		}

		///<summary>What Install did, for the window.</summary>
		public static string GetResultText(StandardTemplateResult result) {
			if(result==null) {
				return "";
			}
			List<string> listLines=new List<string>();
			if(result.ListAdded.Count>0) {
				listLines.Add("Added: "+string.Join(", ",result.ListAdded.Select(x => x.Description+" (mount "+x.MountDefNum.ToString(CultureInfo.InvariantCulture)+")"))+".");
			}
			List<StandardTemplatePlanItem> listLeftAlone=result.Plan?.ListLeftAlone??new List<StandardTemplatePlanItem>();
			if(listLeftAlone.Count>0) {
				listLines.Add("Left alone, because a mount with the name exists: "+string.Join(", ",listLeftAlone.Select(x => x.Template.Description))+".");
			}
			if(result.ListAdded.Count==0 && listLeftAlone.Count>0 && result.Error=="") {
				listLines.Add("Nothing was added.");
			}
			if(result.Error!="") {
				listLines.Add(result.Error);
			}
			listLines.AddRange(result.ListWarnings);
			return string.Join("\r\n",listLines);
		}
	}
}
