// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Session;
using ImagingCapture.UI;
using ImagingDeviceManager.Capture;
using ImagingDeviceManager.Config;

namespace ImagingCapture.Settings {
	///<summary>Checks the values of the Capture settings screen with the parsers the program reads them with, so that what is saved and logged is
	///what takes effect: the values are written into an in-memory LocalCaptureConfig (whose Set refuses what cannot be written to an INI line),
	///then read back with LocalCaptureConfig's getters (as LocalCaptureConfig, TwainCaptureSettings and CaptureSessionConfig read them), with
	///TwainCaptureOptions.FromConfig and its warnings for [Device.N], with CaptureKeyParser and CaptureKeyBindings for [Keys] (a key that does not
	///parse, or one that repeats another's chord, would be left out by the session), and with CaptureSoundNames for [Sounds]. An empty value
	///is always valid: it reads as the default.</summary>
	public static class CaptureSettingsValidator {
		///<summary>The problem of each value that is not valid, by definition: one sentence each. dictTexts holds the values by definition
		///(missing ones count as empty).</summary>
		public static Dictionary<CaptureSettingDef,string> Validate(IDictionary<CaptureSettingDef,string> dictTexts,long imagingDeviceNum) {
			Dictionary<CaptureSettingDef,string> dictProblems=new Dictionary<CaptureSettingDef,string>();
			LocalCaptureConfig config=LocalCaptureConfig.Load((string)null);
			foreach(CaptureSettingDef settingDef in CaptureSettingsCatalog.ListSettings) {
				string text=GetText(dictTexts,settingDef);
				if(!config.Set(settingDef.GetSectionName(imagingDeviceNum),settingDef.Key,text)) {
					dictProblems[settingDef]=config.LastError;
				}
			}
			//TwainCaptureOptions' own warnings for [Device.N], in the library's words.
			List<string> listOptionWarnings=imagingDeviceNum>0 ? TwainCaptureOptions.FromConfig(config,imagingDeviceNum).ListWarnings : new List<string>();
			Dictionary<CaptureSessionKey,string> dictKeyTexts=new Dictionary<CaptureSessionKey,string>();
			foreach(CaptureSettingDef settingDef in CaptureSettingsCatalog.ListSettings) {
				if(dictProblems.ContainsKey(settingDef)) {
					continue;
				}
				string text=GetText(dictTexts,settingDef);
				if(settingDef.Kind==CaptureSettingKind.Key) {
					CaptureSessionKey key;
					if(Enum.TryParse(settingDef.Key,out key)) {
						dictKeyTexts[key]=text;
					}
				}
				if(text=="") {
					continue;
				}
				string problem=GetProblem(settingDef,text,config,imagingDeviceNum,listOptionWarnings);
				if(problem!="") {
					dictProblems[settingDef]=problem;
				}
			}
			//A key the session would leave out because another key has its chord (CaptureKeyBindings), when the key itself parses.
			CaptureKeyBindings bindings=CaptureKeyBindings.FromTexts(dictKeyTexts);
			foreach(string warning in bindings.ListWarnings) {
				foreach(CaptureSettingDef settingDef in CaptureSettingsCatalog.ListSettings.Where(x => x.Kind==CaptureSettingKind.Key)) {
					if(!dictProblems.ContainsKey(settingDef) && warning.StartsWith("[Keys] "+settingDef.Key+" is not used:",StringComparison.Ordinal)) {
						dictProblems[settingDef]=warning;
					}
				}
			}
			return dictProblems;
		}

		private static string GetText(IDictionary<CaptureSettingDef,string> dictTexts,CaptureSettingDef settingDef) {
			string text;
			if(dictTexts==null || !dictTexts.TryGetValue(settingDef,out text)) {
				return "";
			}
			return (text??"").Trim();
		}

		private static string GetProblem(CaptureSettingDef settingDef,string text,LocalCaptureConfig config,long imagingDeviceNum,List<string> listOptionWarnings) {
			string section=settingDef.GetSectionName(imagingDeviceNum);
			string name=settingDef.GetDisplayName(imagingDeviceNum)+"="+text;
			if(settingDef.Section==CaptureSettingsCatalog.SectionDevice) {
				string warning=listOptionWarnings.FirstOrDefault(x => x.StartsWith("["+section+"] "+settingDef.Key+"=",StringComparison.OrdinalIgnoreCase));
				if(warning!=null) {
					return warning;
				}
			}
			switch(settingDef.Kind) {
				case CaptureSettingKind.Switch:
					//GetBool falls back to its default for anything it cannot read: a value read differently by the two defaults is invalid.
					if(config.GetBool(section,settingDef.Key,true)!=config.GetBool(section,settingDef.Key,false)) {
						return name+" is not 0 or 1.";
					}
					return "";
				case CaptureSettingKind.WholeNumber:
					int valueMin=config.GetInt(section,settingDef.Key,int.MinValue);
					if(valueMin!=config.GetInt(section,settingDef.Key,int.MaxValue)) {
						return name+" is not a whole number.";
					}
					if(valueMin<0) {
						return name+" is negative.";
					}
					if(valueMin<settingDef.MinValue) {
						return name+" "+(settingDef.TextBelowMinimum!="" ? settingDef.TextBelowMinimum : "is below "+settingDef.MinValue+".");
					}
					return "";
				case CaptureSettingKind.Choice:
					if(!settingDef.ArrayChoices.Any(x => string.Equals(x,text,StringComparison.OrdinalIgnoreCase))) {
						return name+" is not one of "+string.Join(", ",settingDef.ArrayChoices)+".";
					}
					return "";
				case CaptureSettingKind.Capability:
					int? number=config.GetHex(section,settingDef.Key);
					if(number==null || number.Value<=0 || number.Value>ushort.MaxValue) {
						return name+" is not a capability number (hex, 0x0001-0xFFFF).";
					}
					return "";
				case CaptureSettingKind.CodeList:
					List<string> listEntries=config.GetList(section,settingDef.Key);
					List<int> listCodes=config.GetHexList(section,settingDef.Key);
					if(listCodes.Count!=listEntries.Count || listCodes.Any(x => x<=0 || x>ushort.MaxValue)) {
						return name+" is not a list of condition codes (hex, 0x0001-0xFFFF, separated by |).";
					}
					return "";
				case CaptureSettingKind.SourceList:
					return "";
				case CaptureSettingKind.Key:
					CaptureKeyChord chord;
					string error;
					if(!CaptureKeyParser.TryParse(text,out chord,out error)) {
						return "[Keys] "+settingDef.Key+": "+error;
					}
					return "";
				case CaptureSettingKind.Sound:
					CaptureSystemSound? systemSound;
					if(!CaptureSoundNames.TryParse(text,out systemSound)) {
						return name+" is not a sound name: SystemAsterisk, SystemHand, SystemExclamation, SystemQuestion, SystemDefault or Beep, or empty.";
					}
					return "";
				default:
					return "";
			}
		}
	}
}
